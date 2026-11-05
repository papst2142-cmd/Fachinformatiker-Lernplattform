#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# eingefroren, nur Nachweis (Welle Sammelliste 217, Weisung P12-14): laeuft nicht in der CI.
"""
Welle Sammelliste 217 (Weisung P12-14): friert einmalig die Fragetexte von
fisi_core.QUIZ_QUESTIONS der Python-Linie v0.63.2 ein (mit den im Programm
erzeugten Aufgaben). pruefe_lerninhalte.py (G52) liest danach diese Datei
statt der Python-Linie. Die Datei steht in tests/Vergleich/SOLLWERTE.sha256.

Aufruf (Python 3.12, nur Standardbibliothek):
  python3 -I werkzeuge/vergleich/erzeuge_quiz_python.py <python_linie> tests/Vergleich/quiz_python.json
"""
import atexit
import json
import os
import shutil
import sys
import tempfile


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    linie, ziel = sys.argv[1:]
    tmp = tempfile.mkdtemp(prefix="quiz_python_")
    atexit.register(shutil.rmtree, tmp, True)
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, linie)
    import fisi_core as core
    ergebnis = {"quelle": "fisi_core.QUIZ_QUESTIONS v0.63.2", "fragen": [q["q"] for q in core.QUIZ_QUESTIONS]}
    with open(ziel, "w", encoding="utf-8", newline="\n") as datei:
        json.dump(ergebnis, datei, ensure_ascii=False, indent=1)
        datei.write("\n")
    print(len(ergebnis["fragen"]), "Fragen")
    return 0


if __name__ == "__main__":
    sys.exit(main())
