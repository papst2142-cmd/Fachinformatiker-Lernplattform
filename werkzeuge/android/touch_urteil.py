"""Welle A 0.98.0 (Weisung P12-315, Sammelliste 391): Urteil der Touch-Probe im Emulator (werkzeuge/android/touch_probe.sh).

Liest die Zeilen "SCHRITT <name> VOR <x> <z> <bx> <bz> NACH <x> <z> <bx> <bz> GEHT <ja|nein> STICK <ja|nein>" der Probe
(Stelle der Figur, waagrechter Blick der Kamera vor und nach der Geste; ob die Figur danach losging; ob ein Stick erschien)
und prueft je Schritt Nicos Ziel:
  links_vor        linker Stick nach vorn: die Figur geht vorwaerts (mindestens 0,15 in Blickrichtung, 70 % ihres Wegs)
  rechts_rechts    rechter Stick nach rechts: der Blick dreht nach rechts (393)
  tippen           Tippen ohne Ziehen: kein Stick, die Figur geht los (Antippen)
  rechts_umgekehrt rechter Stick nach rechts mit „Umsehen links/rechts umkehren“: der Blick dreht nach links (394)
Welle A 0.99.0 (Sammelliste 400): dazu Zeilen "HALTUNG <name> ERWARTET <haltung> IST <haltung> AENDERUNGEN <n>" fuer den
runden Haltung-Knopf: haltung_tippen (Ducken), haltung_tippen2 (Stehen), haltung_lang (Kriechen), haltung_auf (Stehen);
seit A 1.7.0 (Sammelliste 447, Nico 1a 2a) ist der Haltung-Knopf „Hinknien“ und „haltung_auf“ tippt „Hinlegen“ (liegend =
aufstehen); dazu haltung_wisch (nach unten wischen auf Hinknien = Kriechen) und haltung_auf2 (Hinlegen tippen = Stehen);
gruen nur, wenn IST = ERWARTET und genau eine Aenderung kam.
Rechts vom Blick (bx, bz) liegt (-bz, bx) wie in SpielUmsehenTests. Fehlt ein Schritt, ist das Urteil rot.
Aufruf: touch_urteil.py <ergebnisdatei>  |  touch_urteil.py selbsttest (Gegenproben: jedes vertauschte Vorzeichen wird rot)
"""
import sys

SCHRITTE = ("links_vor", "rechts_rechts", "tippen", "rechts_umgekehrt")
HALTUNGEN = (("haltung_tippen", "Ducken"), ("haltung_tippen2", "Stehen"), ("haltung_lang", "Kriechen"), ("haltung_auf", "Stehen"),
             ("haltung_wisch", "Kriechen"), ("haltung_auf2", "Stehen"))


def haltungen_lesen(zeilen):
    aus = {}
    for z in zeilen:
        t = z.split()
        if len(t) == 8 and t[0] == "HALTUNG" and t[2] == "ERWARTET" and t[4] == "IST" and t[6] == "AENDERUNGEN" and t[7].isdigit():
            aus[t[1]] = (t[3], t[5], int(t[7]))
    return aus


def lesen(zeilen):
    schritte = {}
    for z in zeilen:
        t = z.split()
        if len(t) == 16 and t[0] == "SCHRITT" and t[2] == "VOR" and t[7] == "NACH" and t[12] == "GEHT" and t[14] == "STICK":
            vor = tuple(float(w) for w in t[3:7])
            nach = tuple(float(w) for w in t[8:12])
            schritte[t[1]] = (vor, nach, t[13] == "ja", t[15] == "ja")
    return schritte


def pruefen(schritt, vor, nach, geht, stick):
    (x0, z0, bx, bz), (x1, z1, nx, nz) = vor, nach
    rechts = (-bz, bx)
    zur_rechten = nx * rechts[0] + nz * rechts[1]
    if schritt == "links_vor":
        dx, dz = x1 - x0, z1 - z0
        weg = (dx * dx + dz * dz) ** 0.5
        vorn = dx * bx + dz * bz
        return stick and vorn >= 0.15 and vorn >= 0.7 * weg, f"Stick {stick}, Weg {weg:.3f}, davon nach vorn {vorn:.3f}"
    if schritt == "rechts_rechts":
        return stick and zur_rechten > 0.05, f"Stick {stick}, neuer Blick zur rechten Seite {zur_rechten:+.3f}"
    if schritt == "rechts_umgekehrt":
        return stick and zur_rechten < -0.05, f"Stick {stick}, neuer Blick zur rechten Seite {zur_rechten:+.3f} (umgekehrt: nach links)"
    if schritt == "tippen":
        return geht and not stick, f"Stick {stick}, Figur geht {geht}"
    return False, "unbekannter Schritt"


def urteil(zeilen):
    schritte = lesen(zeilen)
    aus, gruen = [], True
    for name in SCHRITTE:
        if name not in schritte:
            aus.append(f"FEHLER: {name}: Schritt fehlt")
            gruen = False
            continue
        ok, text = pruefen(name, *schritte[name])
        aus.append(("OK" if ok else "FEHLER") + f": {name}: {text}")
        gruen = gruen and ok
    haltungen = haltungen_lesen(zeilen)
    for name, soll in HALTUNGEN:
        if name not in haltungen:
            aus.append(f"FEHLER: {name}: Schritt fehlt")
            gruen = False
            continue
        erwartet, ist, n = haltungen[name]
        ok = erwartet == soll and ist == soll and n == 1
        aus.append(("OK" if ok else "FEHLER") + f": {name}: erwartet {soll}, ist {ist}, Aenderungen {n}")
        gruen = gruen and ok
    aus.append("ERGEBNIS: " + ("gruen" if gruen else "rot"))
    return gruen, aus


GUT = [
    "SCHRITT links_vor VOR 10.000 5.000 0.0000 1.0000 NACH 10.020 5.600 0.0000 1.0000 GEHT ja STICK ja",
    "SCHRITT rechts_rechts VOR 10.020 5.600 0.0000 1.0000 NACH 10.020 5.600 -0.5000 0.8660 GEHT nein STICK ja",
    "SCHRITT tippen VOR 10.020 5.600 -0.5000 0.8660 NACH 9.000 7.000 -0.5000 0.8660 GEHT ja STICK nein",
    "SCHRITT rechts_umgekehrt VOR 3.000 3.000 0.0000 1.0000 NACH 3.000 3.000 0.5000 0.8660 GEHT nein STICK ja",
    "HALTUNG haltung_tippen ERWARTET Ducken IST Ducken AENDERUNGEN 1",
    "HALTUNG haltung_tippen2 ERWARTET Stehen IST Stehen AENDERUNGEN 1",
    "HALTUNG haltung_lang ERWARTET Kriechen IST Kriechen AENDERUNGEN 1",
    "HALTUNG haltung_auf ERWARTET Stehen IST Stehen AENDERUNGEN 1",
    "HALTUNG haltung_wisch ERWARTET Kriechen IST Kriechen AENDERUNGEN 1",
    "HALTUNG haltung_auf2 ERWARTET Stehen IST Stehen AENDERUNGEN 1",
]


def selbsttest():
    ok, aus = urteil(GUT)
    assert ok, aus
    # rechts von (0, 1) ist (-1, 0): ein Blick (-0,5, 0,87) liegt rechts; Gegenproben mit vertauschtem Vorzeichen werden rot
    def ersetzt(nr, alt, neu):
        z = list(GUT)
        assert alt in z[nr]
        z[nr] = z[nr].replace(alt, neu)
        return z
    gegen = {
        "rechter Stick dreht nach links": ersetzt(1, "NACH 10.020 5.600 -0.5000", "NACH 10.020 5.600 0.5000"),
        "umgekehrt dreht doch nach rechts": ersetzt(3, "NACH 3.000 3.000 0.5000", "NACH 3.000 3.000 -0.5000"),
        "linker Stick geht rueckwaerts": ersetzt(0, "NACH 10.020 5.600", "NACH 10.020 4.400"),
        "linker Stick geht zur Seite": ersetzt(0, "NACH 10.020 5.600", "NACH 10.600 5.020"),
        "linker Stick ohne Stick": ersetzt(0, "GEHT ja STICK ja", "GEHT ja STICK nein"),
        "Tippen zeigt einen Stick": ersetzt(2, "GEHT ja STICK nein", "GEHT ja STICK ja"),
        "Tippen geht nicht": ersetzt(2, "GEHT ja STICK nein", "GEHT nein STICK nein"),
        "rechter Stick ohne Drehung": ersetzt(1, "NACH 10.020 5.600 -0.5000 0.8660", "NACH 10.020 5.600 0.0000 1.0000"),
        "Schritt fehlt": GUT[:3] + GUT[4:],
        "kaputte Zeile": [GUT[0], GUT[1][:-3]] + GUT[2:],
        "Tippen auf Haltung ohne Wirkung": ersetzt(4, "IST Ducken AENDERUNGEN 1", "IST - AENDERUNGEN 0"),
        "Lang druecken gibt Ducken": ersetzt(6, "IST Kriechen", "IST Ducken"),
        "Lang druecken: erst Ducken, dann Kriechen": ersetzt(6, "AENDERUNGEN 1", "AENDERUNGEN 2"),
        "Aufstehen bleibt liegen": ersetzt(7, "IST Stehen", "IST Kriechen"),
        "Haltung-Schritt fehlt": GUT[:6] + GUT[7:],
        "falsch erwartet": ersetzt(5, "ERWARTET Stehen IST Stehen", "ERWARTET Ducken IST Stehen"),
        "Wischen ohne Wirkung": ersetzt(8, "IST Kriechen AENDERUNGEN 1", "IST - AENDERUNGEN 0"),
        "Wischen tippt nur (Ducken)": ersetzt(8, "IST Kriechen", "IST Ducken"),
        "Wisch-Schritt fehlt": GUT[:8] + GUT[9:],
    }
    for name, zeilen in gegen.items():
        rot, aus = urteil(zeilen)
        assert not rot, f"Gegenprobe '{name}' bleibt gruen: {aus}"
    print(f"selbsttest gruen ({len(gegen)} Gegenproben rot)")


def main():
    if len(sys.argv) == 2 and sys.argv[1] == "selbsttest":
        selbsttest()
        return 0
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    with open(sys.argv[1], encoding="utf-8") as f:
        ok, aus = urteil(f.read().splitlines())
    print("\n".join(aus))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
