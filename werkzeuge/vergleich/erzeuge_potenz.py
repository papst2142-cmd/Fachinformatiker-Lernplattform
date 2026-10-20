#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G14 (Potenz x ** y wie Python unter Linux).

Python rechnet x ** y fuer Gleitkommazahlen mit der Funktion pow der
Systembibliothek (unter Linux glibc). Die C#-Fassung (Core/Mathe/PythonPow.cs)
muss auf allen Systemen bitgleich dazu sein. Hier entstehen Wertepaare fuer
alle Stellen, an denen die Python-Linie ** benutzt (fisi_theme: Leuchtdichte
und Lab mit 2.4 und 1/3, Farbabstand mit 2 und 0.5; fisi_game: Abdeckung mit
dem Exponenten aus balancing.json), dazu Grenz- und Zufallswerte (fester Seed).
Alle Zahlen als float.hex, damit nichts beim Umwandeln verloren geht. Wo Python
statt einer Zahl eine komplexe Zahl liefert oder einen Fehler wirft, steht
"complex", "OverflowError" oder "ZeroDivisionError" (C#: NaN bzw. Unendlich).

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_potenz.py tests/Vergleich/potenz.json
"""
import json
import math
import platform
import random
import sys


def main(ziel):
    rnd = random.Random("G14 Potenz")
    paare = []
    # Leuchtdichte und Lab: ((k / 255 + 0.055) / 1.055) ** 2.4 fuer alle 256 Kanalwerte
    for k in range(256):
        paare.append((((k / 255.0) + 0.055) / 1.055, 2.4))
    # Lab: v ** (1 / 3)
    for _ in range(1500):
        paare.append((rnd.uniform(0.008856, 1.1), 1.0 / 3.0))
    # Farbabstand: Differenzen ** 2 (auch negativ) und Summe ** 0.5
    for _ in range(1500):
        paare.append((rnd.uniform(-200.0, 200.0), 2.0))
        paare.append((rnd.uniform(0.0, 120000.0), 0.5))
    # Abdeckung: anteil ** exponent mit anteil in [0, 1]
    for _ in range(500):
        paare.append((rnd.random(), 0.5))
    # Grenzwerte
    besondere = [0.0, -0.0, 1.0, -1.0, 2.0, -2.0, 0.5, -0.5, 3.0, -3.0, 1e-310, 5e-324,
                 1e300, -1e300, 1.7976931348623157e308, float("inf"), float("-inf")]
    for x in besondere:
        for y in besondere + [2.4, 1.0 / 3.0, 0.25, -0.5, 1075.5]:
            paare.append((x, y))
    # Zufaellige Werte ueber viele Groessenordnungen
    for _ in range(1500):
        paare.append((rnd.uniform(0.0, 4.0), rnd.uniform(-50.0, 50.0)))
        paare.append((2.0 ** rnd.uniform(-1000.0, 1000.0), rnd.uniform(-1.2, 1.2)))
    werte = []
    for x, y in paare:
        # Python float_pow: negative endliche Basis mit nicht ganzem endlichen Exponenten
        # rechnet komplex weiter (auch wenn das dann ueberlaeuft); reell gibt es dort keinen Wert
        if x < 0 and math.isfinite(x) and math.isfinite(y) and y != math.floor(y):
            werte.append([x.hex(), y.hex(), "complex"])
            continue
        try:
            wert = x ** y
            ergebnis = "complex" if isinstance(wert, complex) else wert.hex()
        except (OverflowError, ZeroDivisionError) as fehler:
            ergebnis = type(fehler).__name__
        werte.append([x.hex(), y.hex(), ergebnis])
    daten = {
        "python": {"version": platform.python_version(), "system": platform.system(),
                   "libc": "-".join(platform.libc_ver())},
        "potenz": werte,
    }
    with open(ziel, "w", encoding="utf-8") as datei:
        json.dump(daten, datei, ensure_ascii=False, indent=0)
        datei.write("\n")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
