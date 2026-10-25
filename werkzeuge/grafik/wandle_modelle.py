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
Koordinaten wie im Paket (Y nach oben, Vorderseite +Z).

Aufruf: python3 -I werkzeuge/grafik/wandle_modelle.py <ordner mit den ZIPs> [<ausgabeordner>]
"""

import hashlib
import json
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
    zips = {}
    for eintrag in auswahl["modelle"]:
        paket = auswahl["pakete"][eintrag["paket"]]
        if paket["zip"] not in zips:
            zips[paket["zip"]] = zipfile.ZipFile(os.path.join(quelle, paket["zip"]))
        z = zips[paket["zip"]]
        dreiecke = obj_wandeln(z, eintrag["datei"], eintrag.get("atlas"))
        name = eintrag["name"].encode("utf-8")
        bin_teile.append(struct.pack("<H", len(name)) + name + struct.pack("<I", len(dreiecke)))
        alle = [p for d in dreiecke for p in d[0]]
        for ecken, farbe in dreiecke:
            bin_teile.append(struct.pack("<9f", *[w for p in ecken for w in p]) + bytes((farbe[0], farbe[1], farbe[2], 0)))
        liste.append({
            "name": eintrag["name"], "paket": eintrag["paket"], "datei": eintrag["datei"], "atlas": eintrag.get("atlas"), "dreiecke": len(dreiecke),
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
