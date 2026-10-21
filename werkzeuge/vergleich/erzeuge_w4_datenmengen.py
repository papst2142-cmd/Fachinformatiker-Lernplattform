#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# Erzeugt die Vergleichswerte G28 (Datenmengen umrechnen, Uebertragungsdauer)
# fuer Phase 3 / Welle W4. Unabhaengig geschrieben nur aus Plan P3-15
# Abschnitt 4 (4.1/4.2) und den Praezisierungen der Spezifikation G28.
# Python 3.12, nur Standardbibliothek. Aufruf:
#   python3 -I erzeuge_w4_datenmengen.py <ziel.json>
# Ausgabe ist deterministisch (zweimal laufen = bytegleich).
#
# Entscheidungen bei Unklarheiten (siehe auch Kommentare im Code):
#  E-a) Vorschlag E2 ("< 0,01") ist im Plan nur ein Vorschlag. Der Text
#       bleibt daher immer de(x, 2) (also "0,00"); die Anzeige "< 0,01"
#       kann C# aus dem Merker "klein" ableiten.
#  E-b) Der Zahltext rechnet mit einem lokalen Decimal-Kontext hoher
#       Genauigkeit (prec 200). Mit dem Standardkontext (prec 28) wuerde
#       quantize bei sehr grossen Werten (z. B. Dauer 8,8e27 s auf 2 Stellen)
#       InvalidOperation werfen. Bei allen Werten, die in prec 28 passen,
#       ist das Ergebnis identisch.
#  E-c) Bit/Byte/Rate mit Nachkommateil: de(x, 3), dann Nullen am Ende und
#       ein allein stehendes Komma entfernen. Ergibt de(x,3) "0,000"
#       (z. B. 0,000125 Byte), ist der Text "0" (Regel woertlich angewandt).
#  E-d) Bereichspruefung mit den float-Werten: not (0 < x <= Grenze).
#       "1000000000000.001" ergibt als float einen Wert > 1e12 -> Fehler.
#  E-e) Fehlerfall in (a) heisst "bereich"; in (b) "groesse", "rate",
#       "prozent" in dieser Pruefreihenfolge.
#  E-f) Zufallswerte werden immer im gueltigen Bereich erzeugt (Bereichsfaelle
#       stehen gesondert); ein erzeugter Wert 0 wird neu gezogen.

import json
import random
import sys
from decimal import Decimal, ROUND_HALF_UP, localcontext

FAKTOREN = {
    "Byte": 1,
    "kB": 1000, "MB": 1000 ** 2, "GB": 1000 ** 3, "TB": 1000 ** 4,
    "KiB": 1024, "MiB": 1024 ** 2, "GiB": 1024 ** 3, "TiB": 1024 ** 4,
}
EINHEITEN_A = ["Bit", "Byte", "kB", "MB", "GB", "TB", "KiB", "MiB", "GiB", "TiB"]
ZIELE = ["kB", "MB", "GB", "TB", "KiB", "MiB", "GiB", "TiB"]
EINHEITEN_B = ["Byte", "kB", "MB", "GB", "TB", "KiB", "MiB", "GiB", "TiB"]
RATEN = {"kbit/s": 3, "Mbit/s": 6, "Gbit/s": 9}

GRENZE_WERT = 1e12
GRENZE_RATE = 1e6
GRENZE_PROZENT = 100.0


def de(x, n):
    # wie Python de_number: Decimal(repr(x)), kaufmaennisch auf n Stellen,
    # Tausenderpunkt, Dezimalkomma (E-b: hohe Genauigkeit im Kontext)
    with localcontext() as ctx:
        ctx.prec = 200
        q = Decimal(repr(x)).quantize(Decimal(1).scaleb(-n), rounding=ROUND_HALF_UP)
        s = format(q, ",.%df" % n)
    return s.replace(",", "\x00").replace(".", ",").replace("\x00", ".")


def ganz_text(x):
    # Bit, Byte, Rate: ganz -> de(x,0), sonst de(x,3) ohne Endnullen (E-c)
    if x == int(x):
        return de(x, 0)
    s = de(x, 3)
    if "," in s:
        s = s.rstrip("0")
        if s.endswith(","):
            s = s[:-1]
    return s


def datenmenge(wert_text, einheit):
    eintrag = {"wert": wert_text, "einheit": einheit, "fehler": None}
    wert = float(wert_text)
    if not (0 < wert <= GRENZE_WERT):
        eintrag["fehler"] = "bereich"
        return eintrag
    if einheit == "Bit":
        byte = wert / 8
    else:
        byte = wert * FAKTOREN[einheit]
    bit = byte * 8
    werte = {z: byte / FAKTOREN[z] for z in ZIELE}
    eintrag["byte"] = float.hex(byte)
    eintrag["bit"] = float.hex(bit)
    eintrag["werte"] = {z: float.hex(werte[z]) for z in ZIELE}
    text = {"Bit": ganz_text(bit), "Byte": ganz_text(byte)}
    for z in ZIELE:
        text[z] = de(werte[z], 2)   # E-a: kein "< 0,01" im Text
    eintrag["text"] = text
    eintrag["klein"] = [z for z in ZIELE if 0 < werte[z] < 0.01]
    return eintrag


def uebertragung(groesse_text, einheit, rate_text, rate_einheit, prozent_text):
    eintrag = {"groesse": groesse_text, "einheit": einheit,
               "datenrate": rate_text, "rate_einheit": rate_einheit,
               "prozent": prozent_text, "fehler": None}
    groesse = float(groesse_text)
    datenrate = float(rate_text)
    prozent = float(prozent_text)
    if not (0 < groesse <= GRENZE_WERT):
        eintrag["fehler"] = "groesse"
        return eintrag
    if not (0 < datenrate <= GRENZE_RATE):
        eintrag["fehler"] = "rate"
        return eintrag
    if not (0 < prozent <= GRENZE_PROZENT):
        eintrag["fehler"] = "prozent"
        return eintrag
    bit = (groesse * FAKTOREN[einheit]) * 8
    rate = datenrate * 10 ** RATEN[rate_einheit] * prozent / 100
    t = bit / rate
    with localcontext() as ctx:
        ctx.prec = 200
        c = int(Decimal(repr(t)).quantize(Decimal("0.01"), rounding=ROUND_HALF_UP) * 100)
    std = c // 360000
    minute = (c % 360000) // 6000
    sek = c % 6000
    eintrag["bit"] = float.hex(bit)
    eintrag["rate"] = float.hex(rate)
    eintrag["sekunden"] = float.hex(t)
    eintrag["text"] = {"bit": ganz_text(bit), "rate": ganz_text(rate),
                       "sekunden": de(t, 2), "std": std, "min": minute,
                       "sek": f"{sek // 100},{sek % 100:02d}"}
    return eintrag


def zahl_text(rng, max_stellen):
    # Zahl als Text mit 0 bis 3 Nachkommastellen (E-f: nie 0)
    while True:
        ganz = rng.randint(0, 10 ** rng.randint(0, max_stellen) - 1)
        nk = rng.randint(0, 3)
        if nk:
            s = "%d.%0*d" % (ganz, nk, rng.randint(0, 10 ** nk - 1))
        else:
            s = "%d" % ganz
        if float(s) > 0:
            return s


def faelle_a():
    f = []
    # Planbeispiele 4.1
    for w, e in [("1", "TB"), ("500", "GB"), ("3", "GB"), ("256", "MiB"), ("1", "GiB")]:
        f.append((w, e))
    # jede Einheit mit festen Werten
    for e in EINHEITEN_A:
        for w in ["1", "0.001", "0.5", "999.999", "1000", "1024", "123456.789", "1000000000000"]:
            f.append((w, e))
    # Bereich
    for w in ["0", "-1", "1000000000000.001", "10000000000000"]:
        f.append((w, "GB"))
    f.append(("0", "Bit"))
    f.append(("1000000000000.001", "Bit"))
    # Rundungsgrenzen
    f += [("0.125", "Byte"), ("1", "Bit"), ("3", "Bit"), ("5", "Bit"), ("7", "Bit"),
          ("1.005", "kB"), ("2.345", "kB"), ("2.675", "MB"), ("1.125", "GB"),
          ("0.005", "MB"), ("0.015", "MB"), ("9.999", "kB"), ("10", "kB"),
          ("9.994", "MB"), ("9.995", "MB"), ("10.239", "KiB"), ("10.24", "KiB"),
          ("0.001", "Bit"), ("0.004", "Bit"), ("12.345", "Bit"), ("0.333", "Byte")]
    rng = random.Random("G28 Datenmengen")
    for _ in range(300):
        f.append((zahl_text(rng, 9), rng.choice(EINHEITEN_A)))
    return f


def faelle_b():
    f = []
    # Planbeispiele 4.2
    f += [("4.7", "GB", "16", "Mbit/s", "100"), ("4.7", "GiB", "16", "Mbit/s", "100"),
          ("4.7", "GB", "16", "Mbit/s", "80"), ("2", "GiB", "100", "Mbit/s", "90"),
          ("700", "MB", "6", "Mbit/s", "100")]
    # Bereich je Feld: 0, Grenze, knapp ueber Grenze
    f += [("0", "GB", "16", "Mbit/s", "100"), ("1000000000000", "GB", "16", "Mbit/s", "100"),
          ("1000000000000.001", "GB", "16", "Mbit/s", "100"), ("-1", "GB", "16", "Mbit/s", "100"),
          ("1", "GB", "0", "Mbit/s", "100"), ("1", "GB", "1000000", "Gbit/s", "100"),
          ("1", "GB", "1000000.001", "Mbit/s", "100"), ("1", "GB", "-1", "Mbit/s", "100"),
          ("1", "GB", "16", "Mbit/s", "0"), ("1", "GB", "16", "Mbit/s", "100"),
          ("1", "GB", "16", "Mbit/s", "100.001"), ("1", "GB", "16", "Mbit/s", "-5"),
          ("0", "GB", "0", "Mbit/s", "0"), ("1", "GB", "0", "Mbit/s", "0"),
          ("1000000000000", "TiB", "0.001", "kbit/s", "0.001")]
    # Sekunden mit ,995 und Uebertrag (Rate 0,008 kbit/s = 8 bit/s -> t = Byte)
    for g in ["0.995", "59.995", "59.994", "3599.995", "86399.995", "119.995", "0.005", "0.004"]:
        f.append((g, "Byte", "0.008", "kbit/s", "100"))
    # ueber 24 Stunden
    f += [("100000", "Byte", "0.008", "kbit/s", "100"), ("1", "TB", "1", "Mbit/s", "100"),
          ("10", "TiB", "16", "kbit/s", "12.5"), ("1", "GB", "1", "Gbit/s", "100"),
          ("1", "Byte", "1", "Gbit/s", "100"), ("1", "Byte", "3", "kbit/s", "33.3")]
    rng = random.Random("G28 Uebertragung")
    for _ in range(300):
        g = zahl_text(rng, 9)
        e = rng.choice(EINHEITEN_B)
        r = zahl_text(rng, 6)
        re_ = rng.choice(list(RATEN))
        if rng.random() < 0.5:
            p = "%d" % rng.randint(1, 100)
        else:
            p = "%d.%d" % (rng.randint(0, 99), rng.randint(1, 9))
        f.append((g, e, r, re_, p))
    return f


def main():
    if len(sys.argv) != 2:
        print("Aufruf: python3 -I erzeuge_w4_datenmengen.py <ziel.json>", file=sys.stderr)
        return 2
    daten = {
        "quelle": "Unabhaengig aus Plan P3-15 Abschnitt 4 (4.1/4.2) und den Praezisierungen G28 geschrieben, ohne C#-Code.",
        "python": sys.version.split()[0],
        "datenmengen": [datenmenge(w, e) for w, e in faelle_a()],
        "uebertragung": [uebertragung(*x) for x in faelle_b()],
    }
    with open(sys.argv[1], "w", encoding="utf-8", newline="\n") as d:
        json.dump(daten, d, ensure_ascii=False, indent=1, sort_keys=False)
        d.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
