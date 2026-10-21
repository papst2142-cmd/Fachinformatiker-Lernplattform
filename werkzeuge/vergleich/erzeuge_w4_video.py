#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# Erzeugt die Vergleichswerte G29 (Bild und Video mit Dauer und Kompression,
# Uebernahme in den Rechner Uebertragungsdauer) fuer Phase 3 / Welle W4b.
# Unabhaengig geschrieben nur aus Plan P3-15 Abschnitt 4.3 und den
# Praezisierungen der Spezifikation G29, ohne C#-Code.
# Python 3.12, nur Standardbibliothek. Aufruf:
#   python3 -I erzeuge_w4_video.py <ziel.json>
# Ausgabe ist deterministisch (zweimal laufen = bytegleich).
#
# Entscheidungen bei Unklarheiten (siehe auch Kommentare im Code):
#  E-a) Bereichspruefungen rechnen mit den float-Werten: not (0 < m <= 10000),
#       fps == 0, not (1 <= y <= 1000). "10000.001" und "1000.001" liegen
#       als float ueber der Grenze -> Fehler.
#  E-b) Mit Fehler stehen nur die sechs Eingaben und "fehler" im Eintrag;
#       ohne Fehler steht "fehler": null und danach werte, text, klein,
#       klein3, uebernahme (Reihenfolge wie im JSON-Aufbau der Spezifikation).
#  E-c) "werte" enthaelt alle Rechengroessen als float.hex; "sekunden" ist
#       m * 60 als float (auch bei ganzer Minutenzahl). byte_bild ist immer
#       float (bit_bild / 8), auch wenn bit_bild durch 8 teilbar ist.
#  E-d) "klein" prueft die sieben 2-Stellen-Texte in dieser festen Reihenfolge:
#       mbit_unkomp, mbit_komp, mib_s_komp, unkomp_mib, unkomp_gib, komp_mib,
#       komp_gib. "klein3" prueft byte_s, unkomp, komp in dieser Reihenfolge.
#       Beide sind immer Listen (ggf. leer).
#  E-e) Uebernahme in Byte: "passt" vergleicht die Byte-Zahl mit 1e12, in
#       MiB die MiB-Zahl mit 1e12 (Grenze gilt fuer den Zahlwert im Feld
#       Groesse, unabhaengig von der Einheit). Die Dauer wird auch bei
#       passt = false berechnet (Decimal-Kontext prec 200).
#  E-f) Der MiB-Text nimmt format(q, "f") (nie Exponentenschreibweise), dann
#       Endnullen und ein allein stehendes Komma weg. Ergibt das "0" (nur
#       moeglich bei mib < 0,0005, also nie in diesem Zweig), bliebe "0".
#  E-g) Zufallsfaelle: minuten = ganzer Teil 1-600 plus 0 bis 3 zufaellige
#       Nachkommastellen, kompression = ganzer Teil 1-1000 plus 0 bis 2
#       Nachkommastellen; liegt der Wert dann ueber 600 bzw. 1000, wird neu
#       gezogen. Nachkommastellen werden so geschrieben, wie gezogen
#       (z. B. "12.50" bleibt "12.50"). breite/hoehe/tiefe/fps je einmal
#       gezogen in der Reihenfolge breite, hoehe, tiefe, fps, minuten,
#       kompression.
#  E-h) Rundungsgrenze (Fall 7): das Skript sucht deterministisch Eingaben,
#       deren mib = komp / 1048576 als repr genau 4 Nachkommastellen hat und
#       auf 5 endet (fester Bildaufbau, Suche ueber kompression mit bis zu
#       2 Nachkommastellen und minuten). Die ersten drei Treffer gehen ein.
#  E-i) Winzig: Grenze 0,0005 MiB = 524,288 Byte. Knapp darunter komp = 524
#       Byte (524 x 1 x 8 Bit, 1 fps, 1 min, 60-fach), knapp darueber
#       komp = 525 Byte; dazu ein Fall mit nicht ganzer Byte-Zahl (Aufrunden).

import json
import math
import random
import sys
from decimal import Decimal, ROUND_HALF_UP, localcontext

ZWEI_STELLEN = ["mbit_unkomp", "mbit_komp", "mib_s_komp",
                "unkomp_mib", "unkomp_gib", "komp_mib", "komp_gib"]
GANZ_TEXTE = ["byte_bild", "byte_s", "sekunden", "unkomp", "komp"]
KLEIN3 = ["byte_s", "unkomp", "komp"]
WERTE_REIHENFOLGE = ["byte_bild", "byte_s", "unkomp", "komp", "mbit_unkomp",
                     "mbit_komp", "mib_s_komp", "unkomp_mib", "unkomp_gib",
                     "komp_mib", "komp_gib", "sekunden"]
GRENZE_GROESSE = 1e12


def de(x, n):
    # wie Python de_number: Decimal(repr(x)), kaufmaennisch auf n Stellen,
    # Tausenderpunkt, Dezimalkomma (lokaler Kontext prec 200)
    with localcontext() as ctx:
        ctx.prec = 200
        q = Decimal(repr(x)).quantize(Decimal(1).scaleb(-n), rounding=ROUND_HALF_UP)
        s = format(q, ",.%df" % n)
    return s.replace(",", "\x00").replace(".", ",").replace("\x00", ".")


def ganz_text(x):
    # ganz -> de(x,0), sonst de(x,3) ohne Endnullen und ohne einzelnes Komma
    if x == int(x):
        return de(x, 0)
    s = de(x, 3)
    if "," in s:
        s = s.rstrip("0")
        if s.endswith(","):
            s = s[:-1]
    return s


def uebernahme(komp):
    mib = komp / 1048576
    if mib < 0.0005:
        einheit = "Byte"
        wert = math.ceil(komp)
        text = str(wert)
        faktor = 1
    else:
        einheit = "MiB"
        with localcontext() as ctx:
            ctx.prec = 200
            q = Decimal(repr(mib)).quantize(Decimal("0.001"), rounding=ROUND_HALF_UP)
            s = format(q, "f")
        if "." in s:
            s = s.rstrip("0")
            if s.endswith("."):
                s = s[:-1]
        text = s.replace(".", ",")
        faktor = 1048576
    groesse = float(text.replace(",", "."))
    bit = (groesse * faktor) * 8
    rate = 16 * 10 ** 6 * 100 / 100
    t = bit / rate
    with localcontext() as ctx:
        ctx.prec = 200
        c = int(Decimal(repr(t)).quantize(Decimal("0.01"), rounding=ROUND_HALF_UP) * 100)
    std = c // 360000
    minute = (c % 360000) // 6000
    sek = c % 6000
    return {"einheit": einheit, "text": text,
            "passt": groesse <= GRENZE_GROESSE,
            "dauer": {"sekunden": de(t, 2), "std": std, "min": minute,
                      "sek": f"{sek // 100},{sek % 100:02d}"}}


def rechne(breite, hoehe, tiefe, fps, minuten, kompression):
    # gibt die float-Werte zurueck (genau die Reihenfolge der Spezifikation)
    b, h, d = int(breite), int(hoehe), int(tiefe)
    f, m, y = float(fps), float(minuten), float(kompression)
    bit_bild = b * h * d
    byte_bild = bit_bild / 8
    byte_s = byte_bild * f
    sekunden = m * 60
    unkomp = byte_s * m * 60
    komp = unkomp / y
    mbit_unkomp = byte_s * 8 / 1000000
    rate_komp = byte_s / y
    mbit_komp = rate_komp * 8 / 1000000
    mib_s_komp = rate_komp / 1048576
    return {"byte_bild": byte_bild, "byte_s": byte_s, "unkomp": unkomp,
            "komp": komp, "mbit_unkomp": mbit_unkomp, "mbit_komp": mbit_komp,
            "mib_s_komp": mib_s_komp,
            "unkomp_mib": unkomp / 1048576, "unkomp_gib": unkomp / 1073741824,
            "komp_mib": komp / 1048576, "komp_gib": komp / 1073741824,
            "sekunden": sekunden}


def fall(breite, hoehe, tiefe, fps, minuten, kompression):
    eintrag = {"breite": breite, "hoehe": hoehe, "tiefe": tiefe, "fps": fps,
               "minuten": minuten, "kompression": kompression, "fehler": None}
    m, f, y = float(minuten), float(fps), float(kompression)
    if not (0 < m <= 10000):
        eintrag["fehler"] = "minuten"
        return eintrag
    if f == 0:
        eintrag["fehler"] = "bildrate"
        return eintrag
    if not (1 <= y <= 1000):
        eintrag["fehler"] = "kompression"
        return eintrag
    w = rechne(breite, hoehe, tiefe, fps, minuten, kompression)
    text = {}
    for n in WERTE_REIHENFOLGE:
        text[n] = ganz_text(w[n]) if n in GANZ_TEXTE else de(w[n], 2)
    eintrag["werte"] = {n: float.hex(w[n]) for n in WERTE_REIHENFOLGE}
    eintrag["text"] = text
    eintrag["klein"] = [n for n in ZWEI_STELLEN if w[n] > 0 and text[n] == "0,00"]
    eintrag["klein3"] = [n for n in KLEIN3
                         if w[n] > 0 and w[n] != int(w[n]) and text[n] == "0"]
    eintrag["uebernahme"] = uebernahme(w["komp"])
    return eintrag


def endet_auf_5_an_4(x):
    r = repr(x)
    if "e" in r or "." not in r:
        return False
    nk = r.split(".")[1]
    return len(nk) == 4 and nk[-1] == "5"


def faelle_rundung():
    # E-h: deterministische Suche
    treffer = []
    for b, h, d, fps in [("1920", "1080", "24", "25"), ("1280", "720", "24", "30"),
                         ("640", "480", "8", "10")]:
        for minuten in ["1", "2", "3", "5", "7", "10"]:
            for k in range(100, 100001):
                y = "%d.%02d" % (k // 100, k % 100) if k % 100 else "%d" % (k // 100)
                if float(y) > 1000:
                    break
                w = rechne(b, h, d, fps, minuten, y)
                mib = w["komp"] / 1048576
                if mib >= 0.0005 and endet_auf_5_an_4(mib):
                    treffer.append((b, h, d, fps, minuten, y))
                    if len(treffer) == 3:
                        return treffer
                    break
    return treffer


def faelle_zufall():
    rng = random.Random("g29")
    tiefen = [1, 8, 16, 24, 30, 32, 48, 64]
    raten = ["1", "10", "23.976", "24", "25", "29.97", "30", "50",
             "59.94", "60", "120", "144"]
    f = []
    for _ in range(300):
        b = "%d" % rng.randint(1, 7680)
        h = "%d" % rng.randint(1, 4320)
        d = "%d" % rng.choice(tiefen)
        fps = rng.choice(raten)
        while True:
            ganz = rng.randint(1, 600)
            nk = rng.randint(0, 3)
            m = "%d.%0*d" % (ganz, nk, rng.randint(0, 10 ** nk - 1)) if nk else "%d" % ganz
            if float(m) <= 600:
                break
        while True:
            ganz = rng.randint(1, 1000)
            nk = rng.randint(0, 2)
            y = "%d.%0*d" % (ganz, nk, rng.randint(0, 10 ** nk - 1)) if nk else "%d" % ganz
            if float(y) <= 1000:
                break
        f.append((b, h, d, fps, m, y))
    return f


def gruppen():
    g = []
    # 1. Plan-Beispiel
    g.append(("plan", [("1920", "1080", "24", "25", "2", "50")]))
    # 2. Pruefaufgabe 7
    g.append(("pruefaufgabe7", [("1280", "720", "24", "30", "1", "40")]))
    # 3. ohne Kompression, krumme Bildraten
    g.append(("ohne_kompression",
              [("1920", "1080", "24", r, "10", "1") for r in ["23.976", "29.97", "59.94", "0.5"]]
              + [("1920", "1080", "24", "25", "2", "1")]))
    # 4. Grenzen
    grenzen = [("1920", "1080", "24", "25", m, "50")
               for m in ["0", "0.001", "10000", "10000.001", "-1"]]
    grenzen += [("1920", "1080", "24", "25", "2", y)
                for y in ["0.999", "1", "1000", "1000.001"]]
    grenzen += [("1920", "1080", "24", "0", "1", "50"),
                ("1920", "1080", "24", "0", "0", "0.5"),   # minuten gewinnt
                ("1920", "1080", "24", "0", "1", "0.5")]   # bildrate vor kompression
    g.append(("grenzen", grenzen))
    # 5. winzig (E-i)
    g.append(("winzig", [("1", "1", "1", "0.001", "0.001", "1000"),
                         ("524", "1", "8", "1", "1", "60"),
                         ("525", "1", "8", "1", "1", "60"),
                         ("3", "1", "1", "1", "1", "7"),
                         ("1", "1", "1", "1", "0.001", "1")]))
    # 6. sehr gross
    g.append(("gross", [("100000", "100000", "64", "1000", "10000", "1"),
                        ("100000", "100000", "64", "1000", "10000", "1000"),
                        ("7680", "4320", "48", "144", "600", "1"),
                        ("7680", "4320", "64", "1000", "10000", "1"),
                        ("3840", "2160", "30", "120", "10000", "1.5")]))
    # 7. Rundungsgrenze der Uebernahme (E-h)
    g.append(("rundung", faelle_rundung()))
    # 8. Zufall
    g.append(("zufall", faelle_zufall()))
    return g


def main():
    if len(sys.argv) != 2:
        print("Aufruf: python3 -I erzeuge_w4_video.py <ziel.json>", file=sys.stderr)
        return 2
    faelle = []
    for _, liste in gruppen():
        faelle += [fall(*x) for x in liste]
    daten = {
        "quelle": "Unabhaengig aus Plan P3-15 Abschnitt 4.3 und den Praezisierungen G29 geschrieben, ohne C#-Code.",
        "python": sys.version.split()[0],
        "faelle": faelle,
    }
    with open(sys.argv[1], "w", encoding="utf-8", newline="\n") as d:
        json.dump(daten, d, ensure_ascii=False, indent=1, sort_keys=False)
        d.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
