#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G2 (Spike B): Zufallsfolgen von Pythons
random.Random mit festen Startwerten und Werte des Spiel-Wuerfels _dice()
aus der Python-Linie. Die C#-Tests (tests/Lernplattform.Tests) pruefen
gegen genau diese Datei.

Aufruf (Python 3.12 wie die Python-Linie, Pfad zum eingefrorenen Repo
Fisi-Lernplattform auf Stand v0.63.2):
  python3 -I werkzeuge/vergleich/erzeuge_zufall.py <pfad_fisi_lernplattform> tests/Vergleich/zufall.json

Der Python-Code wird nur gelesen, nie veraendert.
"""
import json
import random
import sys

STARTWERTE_TEXT = ["", "0", "trainer-1-abc", "subnetz-3-2026-10-20", "255.255.255.0",
                   "RAID 5", "HTTPSd", "Prüfung Übung äöüß", "192.168.0.0/24",
                   "a" * 300]
STARTWERTE_ZAHL = [0, 1, 42, -7, 4294967295, 4294967296, 2 ** 100 + 12345]
NAMEN = ["Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel",
         "India", "Juliett"]


def folge(rng):
    """Feste Reihenfolge aller Aufrufe; jede Zeile ist ein Schritt."""
    schritte = []
    schritte.append(["random", [repr(rng.random()) for _ in range(50)]])
    for k in (1, 5, 31, 32, 33, 64, 100):
        schritte.append(["getrandbits:%d" % k, [str(rng.getrandbits(k)) for _ in range(5)]])
    schritte.append(["randint:1:6", [rng.randint(1, 6) for _ in range(50)]])
    schritte.append(["randint:0:1000", [rng.randint(0, 1000) for _ in range(20)]])
    schritte.append(["randint:-50:50", [rng.randint(-50, 50) for _ in range(20)]])
    schritte.append(["randrange:10", [rng.randrange(10) for _ in range(20)]])
    schritte.append(["randrange:5:105:7", [rng.randrange(5, 105, 7) for _ in range(10)]])
    schritte.append(["choice:namen", [rng.choice(NAMEN) for _ in range(20)]])
    for _ in range(3):
        liste = list(range(30))
        rng.shuffle(liste)
        schritte.append(["shuffle:30", liste])
    for n, k in ((20, 5), (100, 10), (1000, 50), (8, 3), (6, 6)):
        schritte.append(["sample:%d:%d" % (n, k), rng.sample(range(n), k)])
    schritte.append(["random", [repr(rng.random()) for _ in range(5)]])
    return schritte


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    sys.path.insert(0, repo)
    import fisi_game  # nur lesen: _dice der Python-Linie

    daten = {
        "_hinweis": "Vergleichswerte G2 (Plan Phase 0, Spike B). Erzeugt von "
                    "werkzeuge/vergleich/erzeuge_zufall.py, nie von Hand aendern.",
        "python": sys.version.split()[0],
        "text": [{"startwert": s, "schritte": folge(random.Random(s))} for s in STARTWERTE_TEXT],
        "zahl": [{"startwert": str(z), "schritte": folge(random.Random(z))} for z in STARTWERTE_ZAHL],
        "wuerfel": [],
    }
    for i in range(1000):
        seed = ["", "Firma GmbH|3", "Muster AG|12", "Prüf-Ä|7"][i % 4]
        tag = i // 4
        salz = ["chance", "wahl", "thema|Netzwerk", "staerke0", "bewerber%d" % i][i % 5]
        daten["wuerfel"].append([seed, tag, salz, repr(fisi_game._dice(seed, tag, salz))])
    with open(ziel, "w", encoding="utf-8", newline="\n") as f:
        json.dump(daten, f, ensure_ascii=False, indent=1)
        f.write("\n")
    print("geschrieben: %s" % ziel)
    return 0


if __name__ == "__main__":
    sys.exit(main())
