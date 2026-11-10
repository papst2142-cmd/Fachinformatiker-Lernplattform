"""Welle A 0.61.0 / S 0.36.0 (Weisung P12-171, Nico 4b): Linienbus fuer Stadtbus und Haltestellen.

Kein Modell aus einem Paket: der Bus wird hier aus Kaesten gebaut, nur die vier Raeder sind die Raeder des
Lieferwagens aus dem Car Kit (van.obj, CC0). wandle_modelle.py ruft bus() fuer den Eintrag mit "erzeugt": "bus"
in auswahl.json und haengt das Modell "bus" hinten an modelle.bin an (nur angehaengt, die anderen Modelle bleiben
bytegleich).

Aussehen (Nico 4b): gelb, klar anders als das Taxi (Taxi orange-gelb #FFB247, Bus zitronengelb #EFCB10), helles
Dach, dunkle Fensterbaender, Linienanzeige vorn (leuchtet nachts wie die Scheinwerfer). Tueren rechts in
Fahrtrichtung (-X), Front +Z wie alle Fahrzeuge. Der Lack ist so gewaehlt, dass er nie als Licht gilt
(FestVerkehr.IstLichtFarbe: Rot ab 240, Gruen ab 120, Blau unter 120): Rot 239.

Radkaesten (Nico 4b: "behebe das Problem, dass man bei den Reifen durchgucken kann"): im Entwurf aus Meldung
P12-102 war ueber jeder Achse zwischen den Raedern nichts, man sah quer durch den Bus. Jetzt schliesst ein dunkler
Kasten zwischen den Raedern die ganze Oeffnung (innere Wand), die Stirnwaende der Oeffnung und die Decke sind
dunkel. Test: SpielBusTests.Radkaesten_ZuUndDunkel (Strahlen quer durch jede Oeffnung treffen den Bus; Gegenprobe
mit dem Entwurf P12-102 = entwurf=True).

Nur Python-Standardbibliothek.
"""

L = 2.8        # halbe Laenge (Modellraum, im Spiel x Meter*1,1)
B = 0.64       # halbe Breite
ACH_V = 1.55   # Vorderachse (z)
ACH_H = -1.65  # Hinterachse (z)
RAD = 0.36     # halbe Laenge der Radoeffnung
INNEN = 0.38   # innere Kante der Raeder (|x|); die Raeder des Lieferwagens liegen bei |x| 0,4 bis 0,65

LACK = (0xEF, 0xCB, 0x10)
DACH = (0xF2, 0xEE, 0xE6)
BAND = (0x2B, 0x2F, 0x36)     # dunkles Fensterband (Saeulen)
GLAS = (0x4E, 0x5F, 0x72)     # dunkel getoent
TUER = (0x5D, 0x71, 0x84)
DUNKEL = (0x35, 0x35, 0x39)
RADKASTEN = (0x1E, 0x1E, 0x22)
ANZEIGE = (0xFF, 0xB0, 0x20)  # Linienanzeige, leuchtet nachts
LICHT = (0xFF, 0xD3, 0x30)    # Scheinwerfer, leuchten nachts
RUECK = (0xE2, 0x48, 0x3E)


def _dreieck(a, b, c, farbe):
    return ((tuple(a), tuple(b), tuple(c)), farbe, (farbe, farbe, farbe))


def viereck(a, b, c, d, farbe):
    return [_dreieck(a, b, c, farbe), _dreieck(a, c, d, farbe)]


def kasten(x0, y0, z0, x1, y1, z1, farbe, unten=False, stirn=None):
    """Quader ohne Boden (unter dem Bus sieht niemand hin); stirn: Farbe der beiden Stirnseiten (z0, z1)."""
    s = stirn or farbe
    t = []
    t += viereck((x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1), s)
    t += viereck((x1, y0, z0), (x0, y0, z0), (x0, y1, z0), (x1, y1, z0), s)
    t += viereck((x1, y0, z1), (x1, y0, z0), (x1, y1, z0), (x1, y1, z1), farbe)
    t += viereck((x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0), farbe)
    t += viereck((x0, y1, z1), (x1, y1, z1), (x1, y1, z0), (x0, y1, z0), farbe)
    if unten:
        t += viereck((x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1), farbe)
    return t


def _rad(van, seite, z):
    """Rad des Lieferwagens vorn links (|x| > 0,38, Mitte y 0,26, z 0,76) an Seite +1/-1 und Achse z."""
    aus = []
    for ecken, farbe, *rest in van:
        if all(p[0] > 0.38 for p in ecken) and all(((p[1] - 0.26) ** 2 + (p[2] - 0.76) ** 2) ** 0.5 < 0.31 for p in ecken):
            je = rest[0] if rest else (farbe, farbe, farbe)
            q = [(p[0], p[1], p[2] + z - 0.76) for p in ecken]
            if seite < 0:
                q = [(-p[0], p[1], p[2]) for p in q]
                q = [q[0], q[2], q[1]]
                je = (je[0], je[2], je[1])
            aus.append((tuple(q), farbe, tuple(je)))
    return aus


def bus(van, entwurf=False):
    """Dreiecke des Busses; van = Dreiecke des Lieferwagens (car-van). entwurf=True: Radkaesten offen wie in P12-102 (nur Gegenprobe)."""
    t = []
    zs = [-L, ACH_H - RAD, ACH_H + RAD, ACH_V - RAD, ACH_V + RAD, L]
    # Unterbau zwischen den Radoeffnungen: Stirnseiten zur Oeffnung dunkel
    for a, b in ((zs[0], zs[1]), (zs[2], zs[3]), (zs[4], zs[5])):
        t += kasten(-B, 0.2, a, B, 0.62, b, LACK, stirn=LACK if entwurf else RADKASTEN)
    for z in (ACH_H, ACH_V):
        t += kasten(-B, 0.58, z - RAD, B, 0.62, z + RAD, LACK)
        t += kasten(-B + 0.02, 0.56, z - RAD, B - 0.02, 0.58, z + RAD, RADKASTEN if not entwurf else DUNKEL, unten=not entwurf)
        if not entwurf:
            # innere Wand: schliesst die Oeffnung zwischen den Raedern ueber die ganze Hoehe
            t += kasten(-INNEN, 0.12, z - RAD, INNEN, 0.56, z + RAD, RADKASTEN)
    t += kasten(-B, 0.62, -L, B, 0.86, L, LACK)                         # Bruestung
    t += kasten(-B + 0.01, 0.86, -L + 0.02, B - 0.01, 1.50, L - 0.02, BAND)  # Fensterband
    t += kasten(-B, 1.50, -L, B, 1.68, L, DACH)                         # Dach
    # Seitenfenster
    n = 5
    z0, z1 = -L + 0.15, ACH_V - 0.55
    w = (z1 - z0) / n
    for s in (-1, 1):
        x = s * (B + 0.003)
        for i in range(n):
            za, zb = z0 + i * w + 0.05, z0 + (i + 1) * w - 0.05
            if s < 0 and za < 0.3 and zb > -0.4:
                continue  # Mitteltuer
            if s > 0:
                t += viereck((x, 0.92, zb), (x, 0.92, za), (x, 1.40, za), (x, 1.40, zb), GLAS)
            else:
                t += viereck((x, 0.92, za), (x, 0.92, zb), (x, 1.40, zb), (x, 1.40, za), GLAS)
    x = B + 0.003
    t += viereck((x, 0.92, L - 0.12), (x, 0.92, ACH_V - 0.45), (x, 1.40, ACH_V - 0.45), (x, 1.40, L - 0.12), GLAS)
    # Tueren rechts (-X): vorn und Mitte
    x = -(B + 0.004)
    for za, zb in ((ACH_V + 0.42, L - 0.1), (-0.35, 0.25)):
        t += viereck((x, 0.24, za), (x, 0.24, zb), (x, 1.42, zb), (x, 1.42, za), DUNKEL)
        t += viereck((x - 0.002, 0.28, za + 0.04), (x - 0.002, 0.28, zb - 0.04), (x - 0.002, 1.38, zb - 0.04), (x - 0.002, 1.38, za + 0.04), TUER)
    # Front: Scheibe, Linienanzeige, Scheinwerfer, Stossstange
    z = L + 0.003
    t += viereck((-0.58, 0.70, z), (0.58, 0.70, z), (0.58, 1.42, z), (-0.58, 1.42, z), GLAS)
    t += viereck((-0.5, 1.47, z), (0.5, 1.47, z), (0.5, 1.63, z), (-0.5, 1.63, z), DUNKEL)
    t += viereck((-0.44, 1.495, z + 0.002), (0.44, 1.495, z + 0.002), (0.44, 1.605, z + 0.002), (-0.44, 1.605, z + 0.002), ANZEIGE)
    for s in (-1, 1):
        t += kasten(s * 0.5 - 0.08, 0.36, L, s * 0.5 + 0.08, 0.46, L + 0.02, LICHT)
        t += kasten(s * 0.5 - 0.08, 0.36, -L - 0.02, s * 0.5 + 0.08, 0.50, -L, RUECK)
        # Spiegel
        t += kasten((0 if s > 0 else -0.08) + s * B, 1.05, L - 0.25, s * B + (0.08 if s > 0 else 0), 1.25, L - 0.18, DUNKEL)
    t += kasten(-B, 0.12, L - 0.05, B, 0.32, L + 0.06, DUNKEL)
    t += kasten(-B, 0.12, -L - 0.06, B, 0.32, -L + 0.05, DUNKEL)
    # Heckfenster
    z = -L - 0.003
    t += viereck((0.5, 0.95, z), (-0.5, 0.95, z), (-0.5, 1.40, z), (0.5, 1.40, z), GLAS)
    for s in (-1, 1):
        for z in (ACH_V, ACH_H):
            t += _rad(van, s, z)
    return t
