"""Phase 7 W4 (Weisung P7-9 Nachtrag 3/4, Plan Phase 7 Abschnitt 3 und 6): CC0-Grafiken fuer das Spiel.

Liest die genutzten Modelle aus den Original-ZIPs (OBJ + MTL, Farbatlas als PNG) und schreibt sie in ein
kleines eigenes Format, damit das Programm keine Grafik-Bibliothek braucht:

  inhalte_csharp/grafik/modelle.bin   alle Modelle hintereinander (Aufbau unten)
  inhalte_csharp/grafik/modelle.json  Liste: Name, Paket, Datei im ZIP, Dreiecke, Kasten, SHA-256 von modelle.bin

Die Original-ZIPs kommen nie ins Repo (liegen auf Fi-Austausch anlagen/grafik_cc0/, Lizenz CC0). Welche Modelle
gewandelt werden, steht in auswahl.json (neben diesem Werkzeug); "atlas" waehlt dort eine Farbvariante des
Pakets (z. B. variation-a.png der Commercial-Haeuser). Farbe je Dreieck: Kd des Materials; hat das
Material einen Farbatlas (map_Kd), die Farbe des Atlas an der Mitte der UV-Koordinaten des Dreiecks (die
Pakete malen jede Flaeche einfarbig aus dem Atlas). Nur Python-Standardbibliothek.

Aufbau modelle.bin (Little Endian): "FLGM", uint32 Version 1, uint32 Anzahl Modelle; je Modell: uint16 Laenge
+ Name (UTF-8), uint32 Dreiecke, je Dreieck 9 x float32 (drei Ecken x, y, z) und 4 Byte Farbe (R, G, B, 0).
Koordinaten wie im Paket (Y nach oben, Vorderseite +Z); "drehen": 180 in auswahl.json dreht ein Modell um die
Hochachse, wenn das Paket die Vorderseite nach -Z legt (Furniture Kit: Lehnen liegen bei +Z).

Figur (Phase 7 W5, Weisung P7-10 Frage 1a): Animated Characters 3 gibt es nur als binaeres FBX. fbx_lesen liest
es selbst (Knoten, Eigenschaften, mit zlib gepackte Felder), fbx_figur nimmt das Netz (Vertices,
PolygonVertexIndex, UV), die Farbe je Dreieck aus der Haut-PNG ("haut") an der UV-Mitte und senkt die Arme aus
der T-Haltung ("arme": Winkel in Grad) ueber die Gewichte der Knochen (Cluster) um das Schultergelenk
(TransformLink des Arm-Knochens). Danach Y nach oben, Vorderseite +Z wie die anderen Modelle. Bewegungen spaeter.

Aufruf: python3 -I werkzeuge/grafik/wandle_modelle.py <ordner mit den ZIPs> [<ausgabeordner>]
"""

import hashlib
import json
import math
import os
import struct
import sys
import zipfile
import zlib

HIER = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HIER))


def png_lesen(daten):
    """Liest ein PNG (8 Bit, Graustufe/RGB/RGBA/Palette, ohne Zeilensprung) -> (breite, hoehe, pixel(x, y))."""
    if daten[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("kein PNG")
    pos, idat, palette = 8, b"", None
    breite = hoehe = tiefe = art = sprung = 0
    while pos < len(daten):
        laenge, typ = struct.unpack(">I4s", daten[pos:pos + 8])
        inhalt = daten[pos + 8:pos + 8 + laenge]
        pos += 12 + laenge
        if typ == b"IHDR":
            breite, hoehe, tiefe, art, _, _, sprung = struct.unpack(">IIBBBBB", inhalt)
        elif typ == b"PLTE":
            palette = [tuple(inhalt[i:i + 3]) for i in range(0, len(inhalt), 3)]
        elif typ == b"IDAT":
            idat += inhalt
        elif typ == b"IEND":
            break
    if tiefe != 8 or sprung != 0 or art not in (0, 2, 3, 6):
        raise ValueError("PNG-Art nicht unterstuetzt: Tiefe %d, Art %d, Zeilensprung %d" % (tiefe, art, sprung))
    kanaele = {0: 1, 2: 3, 3: 1, 6: 4}[art]
    roh = zlib.decompress(idat)
    zeile = breite * kanaele
    bild, vorher, p = [], bytearray(zeile), 0
    for _ in range(hoehe):
        filt = roh[p]
        z = bytearray(roh[p + 1:p + 1 + zeile])
        p += 1 + zeile
        for i in range(zeile):
            a = z[i - kanaele] if i >= kanaele else 0
            b = vorher[i]
            c = vorher[i - kanaele] if i >= kanaele else 0
            if filt == 1:
                z[i] = (z[i] + a) & 255
            elif filt == 2:
                z[i] = (z[i] + b) & 255
            elif filt == 3:
                z[i] = (z[i] + ((a + b) >> 1)) & 255
            elif filt == 4:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                z[i] = (z[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        bild.append(bytes(z))
        vorher = z

    def pixel(x, y):
        x = min(max(x, 0), breite - 1)
        y = min(max(y, 0), hoehe - 1)
        werte = bild[y][x * kanaele:(x + 1) * kanaele]
        if art == 3:
            return palette[werte[0]]
        if art == 0:
            return (werte[0],) * 3
        return tuple(werte[:3])

    return breite, hoehe, pixel


def mtl_lesen(text):
    materialien, aktuell = {}, None
    for zeile in text.splitlines():
        teile = zeile.split()
        if not teile:
            continue
        if teile[0] == "newmtl":
            aktuell = " ".join(teile[1:])
            materialien[aktuell] = {"kd": (1.0, 1.0, 1.0), "map": None}
        elif aktuell and teile[0] == "Kd":
            materialien[aktuell]["kd"] = tuple(float(v) for v in teile[1:4])
        elif aktuell and teile[0] == "map_Kd":
            materialien[aktuell]["map"] = " ".join(teile[1:])
    return materialien


def obj_wandeln(z, pfad, atlas=None):
    """Ein OBJ aus dem ZIP -> Liste von Dreiecken ((x, y, z) * 3, (r, g, b)); atlas ersetzt den Farbatlas (Pfad im ZIP)."""
    ordner = os.path.dirname(pfad)
    text = z.read(pfad).decode("utf-8", "replace")
    ecken, uvs, materialien, material, dreiecke, atlanten = [], [], {}, None, [], {}
    for zeile in text.splitlines():
        teile = zeile.split()
        if not teile:
            continue
        if teile[0] == "mtllib":
            name = " ".join(teile[1:])
            mtl = os.path.normpath(os.path.join(ordner, name)).replace(os.sep, "/")
            materialien.update(mtl_lesen(z.read(mtl).decode("utf-8", "replace")))
        elif teile[0] == "v":
            ecken.append(tuple(float(v) for v in teile[1:4]))
        elif teile[0] == "vt":
            uvs.append((float(teile[1]), float(teile[2]) if len(teile) > 2 else 0.0))
        elif teile[0] == "usemtl":
            material = " ".join(teile[1:])
        elif teile[0] == "f":
            punkte = []
            for ecke in teile[1:]:
                nummern = ecke.split("/")
                v = int(nummern[0])
                t = int(nummern[1]) if len(nummern) > 1 and nummern[1] else None
                punkte.append((ecken[v - 1 if v > 0 else v], uvs[t - 1 if t > 0 else t] if t else None))
            farbe = farbe_fuer(z, ordner, materialien.get(material), punkte, atlanten, atlas)
            for i in range(1, len(punkte) - 1):
                dreiecke.append(((punkte[0][0], punkte[i][0], punkte[i + 1][0]), farbe))
    return dreiecke


def farbe_fuer(z, ordner, mat, punkte, atlanten, ersatz=None):
    if mat is None:
        return (200, 200, 200)
    kd = mat["kd"]
    if mat["map"]:
        atlas = ersatz or os.path.normpath(os.path.join(ordner, mat["map"])).replace(os.sep, "/")
        if atlas not in atlanten:
            atlanten[atlas] = png_lesen(z.read(atlas))
        breite, hoehe, pixel = atlanten[atlas]
        uv = [p[1] for p in punkte if p[1] is not None]
        if uv:
            u = sum(t[0] for t in uv) / len(uv)
            v = sum(t[1] for t in uv) / len(uv)
            u -= int(u // 1)
            v -= int(v // 1)
            r, g, b = pixel(int(u * breite), int((1 - v) * hoehe))
            return (round(r * kd[0]), round(g * kd[1]), round(b * kd[2]))
    return tuple(min(255, max(0, round(c * 255))) for c in kd)


def fbx_lesen(daten):
    """Binaeres FBX (Version 7.x) -> Liste der obersten Knoten (name, werte, kinder)."""
    if daten[:21] != b"Kaydara FBX Binary  \x00":
        raise ValueError("kein binaeres FBX")
    breit = struct.unpack_from("<I", daten, 23)[0] >= 7500
    knoten, pos = [], 27
    while True:
        k, pos = _fbx_knoten(daten, pos, breit)
        if k is None:
            return knoten
        knoten.append(k)


def _fbx_knoten(d, pos, breit):
    if breit:
        ende, anzahl, _ = struct.unpack_from("<QQQ", d, pos)
        pos += 24
    else:
        ende, anzahl, _ = struct.unpack_from("<III", d, pos)
        pos += 12
    laenge = d[pos]
    pos += 1
    if ende == 0:
        return None, pos
    name = d[pos:pos + laenge].decode("ascii")
    pos += laenge
    werte = []
    for _ in range(anzahl):
        art = chr(d[pos])
        pos += 1
        if art in "YCIFDL":
            fmt = {"Y": "<h", "C": "<B", "I": "<i", "F": "<f", "D": "<d", "L": "<q"}[art]
            werte.append(struct.unpack_from(fmt, d, pos)[0])
            pos += struct.calcsize(fmt)
        elif art in "SR":
            n = struct.unpack_from("<I", d, pos)[0]
            werte.append(d[pos + 4:pos + 4 + n])
            pos += 4 + n
        elif art in "fdlib":
            n, packung, groesse = struct.unpack_from("<III", d, pos)
            roh = d[pos + 12:pos + 12 + groesse]
            pos += 12 + groesse
            if packung == 1:
                roh = zlib.decompress(roh)
            werte.append(list(struct.unpack("<%d%s" % (n, {"f": "f", "d": "d", "l": "q", "i": "i", "b": "B"}[art]), roh)))
        else:
            raise ValueError("FBX: unbekannter Werttyp %r" % art)
    kinder = []
    while pos < ende:
        k, pos = _fbx_knoten(d, pos, breit)
        if k is None:
            break
        kinder.append(k)
    return (name, werte, kinder), ende


def _fbx_kind(knoten, name):
    return next((k for k in knoten[2] if k[0] == name), None)


def _fbx_feld(knoten, name):
    k = _fbx_kind(knoten, name)
    return k[1][0] if k else None


def fbx_figur(z, pfad, haut, arme):
    """Figur aus einem FBX im ZIP -> Dreiecke wie obj_wandeln (Y oben, Vorderseite +Z)."""
    wurzel = fbx_lesen(z.read(pfad))
    objekte = next(k for k in wurzel if k[0] == "Objects")
    netz = next(k for k in objekte[2] if k[0] == "Geometry" and k[1][2] == b"Mesh")
    ecken = _fbx_feld(netz, "Vertices")
    vielecke = _fbx_feld(netz, "PolygonVertexIndex")
    uv_knoten = _fbx_kind(netz, "LayerElementUV")
    uvs, uv_index = _fbx_feld(uv_knoten, "UV"), _fbx_feld(uv_knoten, "UVIndex")
    punkte = [tuple(ecken[i * 3:i * 3 + 3]) for i in range(len(ecken) // 3)]
    # Arme senken: je Ecke das gewichtete Mittel aus "bleibt" und "um die Schulter gedreht"
    drehung = {}
    gewichte = [0.0] * len(punkte)
    for k in objekte[2]:
        if k[0] != "Deformer" or k[1][2] != b"Cluster":
            continue
        knochen = k[1][1].split(b"\x00")[0].decode("ascii")
        seite = "Left" if knochen.startswith(("LeftArm", "LeftForeArm", "LeftHand")) else \
            "Right" if knochen.startswith(("RightArm", "RightForeArm", "RightHand")) else None
        indizes, werte = _fbx_feld(k, "Indexes"), _fbx_feld(k, "Weights")
        if not indizes:
            continue
        for i, g in zip(indizes, werte):
            gewichte[i] += g
            if seite:
                drehung.setdefault(i, {}).setdefault(seite, 0.0)
                drehung[i][seite] += g
    gelenke = {}
    for k in objekte[2]:
        if k[0] == "Deformer" and k[1][2] == b"Cluster":
            knochen = k[1][1].split(b"\x00")[0].decode("ascii")
            if knochen in ("LeftArm", "RightArm"):
                m = _fbx_feld(k, "TransformLink")
                # Welt (Y oben, x100) -> Netz (Z oben): x = X/100, y = -Z/100, z = Y/100
                gelenke[knochen[:-3]] = (m[12] / 100.0, -m[14] / 100.0, m[13] / 100.0)
    winkel = {"Left": -math.radians(arme), "Right": math.radians(arme)}
    gesenkt = []
    for i, (x, y, zz) in enumerate(punkte):
        anteile = drehung.get(i, {})
        summe = gewichte[i] or 1.0
        nx, nz = x, zz
        for seite, g in anteile.items():
            gx, _, gz = gelenke[seite]
            c, s = math.cos(winkel[seite]), math.sin(winkel[seite])
            dx, dz = x - gx, zz - gz
            rx, rz = gx + dx * c - dz * s, gz + dx * s + dz * c
            nx += (rx - x) * g / summe
            nz += (rz - zz) * g / summe
        gesenkt.append((nx, nz, -y))
    breite, hoehe, pixel = png_lesen(z.read(haut))
    dreiecke, vieleck = [], []
    for nummer, wert in enumerate(vielecke):
        vieleck.append((wert if wert >= 0 else ~wert, nummer))
        if wert >= 0:
            continue
        u = sum(uvs[uv_index[n] * 2] for _, n in vieleck) / len(vieleck)
        v = sum(uvs[uv_index[n] * 2 + 1] for _, n in vieleck) / len(vieleck)
        u -= int(u // 1)
        v -= int(v // 1)
        farbe = pixel(int(u * breite), int((1 - v) * hoehe))
        for i in range(1, len(vieleck) - 1):
            dreiecke.append(((gesenkt[vieleck[0][0]], gesenkt[vieleck[i][0]], gesenkt[vieleck[i + 1][0]]), farbe))
        vieleck = []
    return dreiecke

def mul(a, b):  # 4x4, Zeilen-Listen, Spaltenvektoren
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]

def ident():
    return [[1.0 if i == j else 0.0 for j in range(4)] for i in range(4)]

def trans(t):
    m = ident(); m[0][3], m[1][3], m[2][3] = t; return m

def skal(s):
    m = ident(); m[0][0], m[1][1], m[2][2] = s; return m

def rot(achse, grad):
    c, s = math.cos(math.radians(grad)), math.sin(math.radians(grad))
    m = ident()
    if achse == 0: m[1][1], m[1][2], m[2][1], m[2][2] = c, -s, s, c
    elif achse == 1: m[0][0], m[0][2], m[2][0], m[2][2] = c, s, -s, c
    else: m[0][0], m[0][1], m[1][0], m[1][1] = c, -s, s, c
    return m

def euler(r):  # FBX eEulerXYZ: erst X, dann Y, dann Z
    return mul(rot(2, r[2]), mul(rot(1, r[1]), rot(0, r[0])))

def aus_fbx(f):  # 16 Werte spaltenweise
    return [[f[j * 4 + i] for j in range(4)] for i in range(4)]

def invers(m):
    # allgemeine 4x4-Inverse (Gauss)
    a = [row[:] + [1.0 if i == j else 0.0 for j in range(4)] for i, row in enumerate(m)]
    for c in range(4):
        p = max(range(c, 4), key=lambda r: abs(a[r][c])); a[c], a[p] = a[p], a[c]
        d = a[c][c]; a[c] = [x / d for x in a[c]]
        for r in range(4):
            if r != c:
                f = a[r][c]; a[r] = [x - f * y for x, y in zip(a[r], a[c])]
    return [row[4:] for row in a]

def anwenden(m, p):
    return tuple(m[i][0] * p[0] + m[i][1] * p[1] + m[i][2] * p[2] + m[i][3] for i in range(3))

def name(k):
    return k[1][1].split(b'\x00')[0].decode()

class Skelett:
    def __init__(self, wurzel):
        obj = next(k for k in wurzel if k[0] == 'Objects')
        con = next(k for k in wurzel if k[0] == 'Connections')
        self.modelle = {}
        ids = {}
        for k in obj[2]:
            if k[0] == 'Model':
                p = {q[1][0].decode(): q[1][4:] for q in _fbx_kind(k, 'Properties70')[2]}
                self.modelle[name(k)] = {'T': p.get('Lcl Translation', [0, 0, 0]), 'R': p.get('Lcl Rotation', [0, 0, 0]),
                                         'S': p.get('Lcl Scaling', [1, 1, 1]), 'id': k[1][0]}
                ids[k[1][0]] = name(k)
        self.eltern = {}
        alle = {k[1][0]: k for k in obj[2]}
        knoten = {}  # curvenode id -> (modell, eigenschaft)
        kurven = {}  # curve id -> (curvenode, achse)
        for c in con[2]:
            v = c[1]
            if v[0] == b'OO' and v[1] in ids and v[2] in ids:
                self.eltern[ids[v[1]]] = ids[v[2]]
            if v[0] == b'OP' and v[2] in ids and v[1] in alle and alle[v[1]][0] == 'AnimationCurveNode':
                knoten[v[1]] = (ids[v[2]], v[3].decode())
            if v[0] == b'OP' and v[2] in alle and alle[v[2]][0] == 'AnimationCurveNode' and alle[v[1]][0] == 'AnimationCurve':
                kurven[v[1]] = (v[2], v[3].decode())
        self.anim = {}  # (modell, eig) -> [kx, ky, kz] je (zeiten, werte)
        for kid, (cn, achse) in kurven.items():
            if cn not in knoten: continue
            k = alle[kid]
            zeiten, werte = _fbx_feld(k, 'KeyTime'), _fbx_feld(k, 'KeyValueFloat')
            self.anim.setdefault(knoten[cn], {})['XYZ'.index(achse[-1])] = (zeiten, werte)

    def wert(self, modell, eig, t):
        basis = list(self.modelle[modell]['T' if eig == 'Lcl Translation' else 'R' if eig == 'Lcl Rotation' else 'S'])
        for i, (zeiten, werte) in self.anim.get((modell, eig), {}).items():
            if t <= zeiten[0]: basis[i] = werte[0]; continue
            if t >= zeiten[-1]: basis[i] = werte[-1]; continue
            for j in range(1, len(zeiten)):
                if zeiten[j] >= t:
                    a = (t - zeiten[j - 1]) / (zeiten[j] - zeiten[j - 1]); basis[i] = werte[j - 1] + (werte[j] - werte[j - 1]) * a; break
        return basis

    def global_(self, t=None, extra=None):
        erg = {}
        def g(n):
            if n in erg: return erg[n]
            m = self.modelle[n]
            T = self.wert(n, 'Lcl Translation', t) if t is not None else m['T']
            R = self.wert(n, 'Lcl Rotation', t) if t is not None else m['R']
            S = self.wert(n, 'Lcl Scaling', t) if t is not None else m['S']
            lokal = mul(trans(T), mul(euler(R), skal(S)))
            if extra and n in extra:
                lokal = mul(lokal, extra[n])
            erg[n] = mul(g(self.eltern[n]), lokal) if n in self.eltern else lokal
            return erg[n]
        for n in self.modelle: g(n)
        return erg



def fbx_haut(wurzel):
    """Haut des Netzes: je Knochen (Name) die Ecken-Gewichte, Transform und TransformLink (Bindehaltung)."""
    obj = next(k for k in wurzel if k[0] == 'Objects')
    con = next(k for k in wurzel if k[0] == 'Connections')
    namen = {k[1][0]: name(k) for k in obj[2] if k[0] == 'Model'}
    knochen_von = {c[1][2]: namen[c[1][1]] for c in con[2] if c[1][0] == b'OO' and c[1][1] in namen}
    haut = []
    for k in obj[2]:
        if k[0] == 'Deformer' and k[1][2] == b'Cluster' and _fbx_feld(k, 'Indexes'):
            haut.append((knochen_von[k[1][0]], _fbx_feld(k, 'Indexes'), _fbx_feld(k, 'Weights'),
                         aus_fbx(_fbx_feld(k, 'Transform')), aus_fbx(_fbx_feld(k, 'TransformLink'))))
    return haut


def welt_drehen(G, eltern, knochen, achse, grad):
    """Dreht einen Knochen samt allen Kindern in Weltlage um sein Gelenk (eigene Bewegung)."""
    p = (G[knochen][0][3], G[knochen][1][3], G[knochen][2][3])
    m = mul(trans(p), mul(rot(achse, grad), trans((-p[0], -p[1], -p[2]))))
    def unter(n):
        while n is not None:
            if n == knochen:
                return True
            n = eltern.get(n)
        return False
    for n in list(G):
        if unter(n):
            G[n] = mul(m, G[n])


def gehen_haltung(sk, phase, arme):
    """Eigene Gehbewegung (Entwurf): Ruhehaltung des Modells, Arme gesenkt, Beine und Arme schwingen."""
    G = sk.global_()
    s, c = math.sin(2 * math.pi * phase), math.cos(2 * math.pi * phase)
    welt_drehen(G, sk.eltern, 'LeftArm', 2, -arme)
    welt_drehen(G, sk.eltern, 'RightArm', 2, arme)
    welt_drehen(G, sk.eltern, 'LeftArm', 0, 22 * s)
    welt_drehen(G, sk.eltern, 'RightArm', 0, -22 * s)
    welt_drehen(G, sk.eltern, 'LeftForeArm', 0, -18 - 8 * max(0, -s))
    welt_drehen(G, sk.eltern, 'RightForeArm', 0, -18 - 8 * max(0, s))
    welt_drehen(G, sk.eltern, 'LeftUpLeg', 0, -26 * s)
    welt_drehen(G, sk.eltern, 'RightUpLeg', 0, 26 * s)
    welt_drehen(G, sk.eltern, 'LeftLeg', 0, 4 + 40 * max(0, c) ** 1.5)
    welt_drehen(G, sk.eltern, 'RightLeg', 0, 4 + 40 * max(0, -c) ** 1.5)
    welt_drehen(G, sk.eltern, 'LeftFoot', 0, -10 * max(0, c))
    welt_drehen(G, sk.eltern, 'RightFoot', 0, -10 * max(0, -c))
    # Huefte: leicht auf und ab (tiefster Punkt, wenn die Beine gespreizt sind)
    hub = trans((0, -2.2 * abs(s), 0))
    for n in G:
        G[n] = mul(hub, G[n])
    return G


def sitzen_haltung(sk, arme):
    """Phase 9 W1 (Weisung P9-3): sitzend am Schreibtisch. Oberschenkel fast waagrecht nach vorn (80 Grad), Unterschenkel
    fast senkrecht (85 Grad zurueck), Arme gesenkt und nach vorn, Haende ueber der Tischplatte (Werte so gewaehlt, dass
    Stuhl, Tisch und Figur sich nicht durchdringen, Test SpielSitzenTests; Hoehe stellt bewegung_bilder ein)."""
    G = sk.global_()
    welt_drehen(G, sk.eltern, 'LeftArm', 2, -arme)
    welt_drehen(G, sk.eltern, 'RightArm', 2, arme)
    welt_drehen(G, sk.eltern, 'LeftArm', 0, -40)
    welt_drehen(G, sk.eltern, 'RightArm', 0, -40)
    welt_drehen(G, sk.eltern, 'LeftForeArm', 0, -60)
    welt_drehen(G, sk.eltern, 'RightForeArm', 0, -60)
    welt_drehen(G, sk.eltern, 'LeftUpLeg', 0, -80)
    welt_drehen(G, sk.eltern, 'RightUpLeg', 0, -80)
    welt_drehen(G, sk.eltern, 'LeftLeg', 0, 85)
    welt_drehen(G, sk.eltern, 'RightLeg', 0, 85)
    return G


def auf_sitz(dreiecke, G, sitz):
    """Sitzende Figur senken: die Unterseite der Oberschenkel (Ecken zwischen Huefte und Knie) liegt auf
    Hoehe <sitz> (Einheiten des Modells, die Figur steht 3,77 hoch)."""
    huefte, hoehe = G['LeftUpLeg'][2][3] / 100.0, G['LeftUpLeg'][1][3] / 100.0
    knie = G['LeftLeg'][2][3] / 100.0
    # nur die Oberschenkel: zwischen Huefte und Knie und hoeher als die Fuesse (die haengen darunter)
    unten = min(p[1] for e, _ in dreiecke for p in e if huefte + 0.1 < p[2] < knie - 0.1 and p[1] > hoehe - 0.35)
    d = sitz - unten
    return [(tuple((p[0], p[1] + d, p[2]) for p in e), f) for e, f in dreiecke]


def eintrag_netz(wurzel):
    """Name des Modells, unter dem das Netz haengt."""
    obj = next(k for k in wurzel if k[0] == 'Objects')
    con = next(k for k in wurzel if k[0] == 'Connections')
    netz = next(k for k in obj[2] if k[0] == "Geometry" and k[1][2] == b"Mesh")
    namen = {k[1][0]: name(k) for k in obj[2] if k[0] == 'Model'}
    return next(namen[c[1][2]] for c in con[2] if c[1][0] == b'OO' and c[1][1] == netz[1][0])


def fbx_figur_haltung(z, pfad, haut_png, G):
    """Figur mit Haut nach Haltung G (Knochen -> Weltmatrix) -> Dreiecke (Y oben, Vorderseite +Z)."""
    wurzel = fbx_lesen(z.read(pfad))
    objekte = next(k for k in wurzel if k[0] == "Objects")
    netz = next(k for k in objekte[2] if k[0] == "Geometry" and k[1][2] == b"Mesh")
    ecken = _fbx_feld(netz, "Vertices")
    vielecke = _fbx_feld(netz, "PolygonVertexIndex")
    uv_knoten = _fbx_kind(netz, "LayerElementUV")
    uvs, uv_index = _fbx_feld(uv_knoten, "UV"), _fbx_feld(uv_knoten, "UVIndex")
    punkte = [tuple(ecken[i * 3:i * 3 + 3]) for i in range(len(ecken) // 3)]
    summe = [[0.0, 0.0, 0.0] for _ in punkte]
    gewicht = [0.0] * len(punkte)
    # Transform der Cluster ist in diesem Paket die Einheitsmatrix; das Netz liegt unter dem Modell
    # "characterMedium" (gedreht und x100), dessen Weltlage gilt
    netz_lage = Skelett(wurzel).global_()[eintrag_netz(wurzel)]
    for knochen, indizes, werte, _, TL in fbx_haut(wurzel):
        m = mul(G[knochen], mul(invers(TL), netz_lage))
        for i, g in zip(indizes, werte):
            q = anwenden(m, punkte[i])
            for a in range(3):
                summe[i][a] += q[a] * g
            gewicht[i] += g
    welt = [tuple(v / 100.0 / (gewicht[i] or 1.0) for v in summe[i]) for i in range(len(punkte))]
    breite, hoehe, pixel = png_lesen(z.read(haut_png))
    dreiecke, vieleck = [], []
    for nummer, wert in enumerate(vielecke):
        vieleck.append((wert if wert >= 0 else ~wert, nummer))
        if wert >= 0:
            continue
        u = sum(uvs[uv_index[n] * 2] for _, n in vieleck) / len(vieleck)
        v = sum(uvs[uv_index[n] * 2 + 1] for _, n in vieleck) / len(vieleck)
        u -= int(u // 1)
        v -= int(v // 1)
        farbe = pixel(int(u * breite), int((1 - v) * hoehe))
        for i in range(1, len(vieleck) - 1):
            dreiecke.append(((welt[vieleck[0][0]], welt[vieleck[i][0]], welt[vieleck[i + 1][0]]), farbe))
        vieleck = []
    return dreiecke


def bewegung_bilder(z, eintrag):
    """Bilder einer Bewegung: "lauf" aus Animations/run.fbx (Paket), "gehen" (eigene Gelenk-Bewegung) oder "sitzen"
    (ein Bild, Phase 9 W1)."""
    bilder = eintrag["bilder"]
    modell = Skelett(fbx_lesen(z.read(eintrag["datei"])))
    erg = []
    if eintrag["bewegung"] == "sitzen":
        G = sitzen_haltung(modell, eintrag.get("arme", 72))
        return [auf_sitz(fbx_figur_haltung(z, eintrag["datei"], eintrag["haut"], G), G, eintrag["sitz"])]
    if eintrag["bewegung"] == "gehen":
        for b in range(bilder):
            erg.append(fbx_figur_haltung(z, eintrag["datei"], eintrag["haut"], gehen_haltung(modell, b / bilder, eintrag.get("arme", 72))))
        return erg
    anim = Skelett(fbx_lesen(z.read(eintrag["animation"])))
    ende = eintrag["ende"]
    for b in range(bilder):
        G = anim.global_(t=ende * b // bilder)
        erg.append(fbx_figur_haltung(z, eintrag["datei"], eintrag["haut"], G))
    return erg


def gedreht(dreiecke, grad):
    """Um die Hochachse drehen (nur 180: x und z spiegeln, Umlaufsinn bleibt)."""
    if grad % 360 == 0:
        return dreiecke
    if grad % 360 != 180:
        raise ValueError("drehen: nur 180 Grad")
    return [(tuple((-p[0], p[1], -p[2]) for p in e), f) for e, f in dreiecke]


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    quelle = sys.argv[1]
    aus = sys.argv[2] if len(sys.argv) > 2 else os.path.join(REPO, "inhalte_csharp", "grafik")
    with open(os.path.join(HIER, "auswahl.json"), encoding="utf-8") as f:
        auswahl = json.load(f)
    bin_teile = [b"FLGM", struct.pack("<II", 1, len(auswahl["modelle"]))]
    liste = []
    ausgabe = []
    zips = {}
    for eintrag in auswahl["modelle"]:
        paket = auswahl["pakete"][eintrag["paket"]]
        if paket["zip"] not in zips:
            zips[paket["zip"]] = zipfile.ZipFile(os.path.join(quelle, paket["zip"]))
        z = zips[paket["zip"]]
        if "teile" in eintrag:
            # Phase 8 W5 (Kaufhaus, Weisung P8-8c): aus Bauteilen zusammengesetzt (Modular Buildings), je Teil Versatz x, y, z
            dreiecke = []
            for teil in eintrag["teile"]:
                dx, dy, dz = teil["v"]
                for ecken, farbe in gedreht(obj_wandeln(z, teil["datei"], eintrag.get("atlas")), teil.get("drehen", 0)):
                    dreiecke.append((tuple((q[0] + dx, q[1] + dy, q[2] + dz) for q in ecken), farbe))
        elif "bewegung" in eintrag:
            # Phase 8 W7: je Bild ein eigenes Modell name00, name01, ...
            for nr, dreiecke in enumerate(bewegung_bilder(z, eintrag)):
                ausgabe.append((eintrag, "%s%02d" % (eintrag["name"], nr), dreiecke))
            continue
        elif eintrag["datei"].endswith(".fbx"):
            dreiecke = fbx_figur(z, eintrag["datei"], eintrag["haut"], eintrag.get("arme", 0))
        else:
            dreiecke = obj_wandeln(z, eintrag["datei"], eintrag.get("atlas"))
        ausgabe.append((eintrag, eintrag["name"], gedreht(dreiecke, eintrag.get("drehen", 0))))
    bin_teile[1] = struct.pack("<II", 1, len(ausgabe))
    for eintrag, modellname, dreiecke in ausgabe:
        name = modellname.encode("utf-8")
        bin_teile.append(struct.pack("<H", len(name)) + name + struct.pack("<I", len(dreiecke)))
        alle = [p for d in dreiecke for p in d[0]]
        for ecken, farbe in dreiecke:
            bin_teile.append(struct.pack("<9f", *[w for p in ecken for w in p]) + bytes((farbe[0], farbe[1], farbe[2], 0)))
        liste.append({
            "name": modellname, "paket": eintrag["paket"], "datei": eintrag["datei"], "atlas": eintrag.get("atlas") or eintrag.get("haut"), "dreiecke": len(dreiecke),
            "min": [round(min(p[i] for p in alle), 4) for i in range(3)],
            "max": [round(max(p[i] for p in alle), 4) for i in range(3)],
        })
    daten = b"".join(bin_teile)
    os.makedirs(aus, exist_ok=True)
    with open(os.path.join(aus, "modelle.bin"), "wb") as f:
        f.write(daten)
    index = {
        "_hinweis": "Erzeugt von werkzeuge/grafik/wandle_modelle.py aus den CC0-Paketen (Fi-Austausch anlagen/grafik_cc0). Nicht von Hand aendern.",
        "pakete": {k: {"zip": v["zip"], "sha256": v["sha256"], "quelle": v["quelle"], "lizenz": "CC0 1.0"} for k, v in auswahl["pakete"].items()},
        "sha256": hashlib.sha256(daten).hexdigest(),
        "bytes": len(daten),
        "modelle": liste,
    }
    with open(os.path.join(aus, "modelle.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(index, f, ensure_ascii=False, indent=1)
        f.write("\n")
    for zip_ in zips.values():
        zip_.close()
    print("modelle.bin: %d Modelle, %d Bytes, SHA-256 %s" % (len(liste), len(daten), index["sha256"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
