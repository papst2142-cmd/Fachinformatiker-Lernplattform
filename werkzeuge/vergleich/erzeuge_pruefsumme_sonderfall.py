#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt tests/Vergleich/pruefsumme_sonderfall.json (Phase 1, G11 Teil
"Pruefsumme der Sicherung"; Erzeuger nachgereicht in Phase 2, Paket L3,
Sammelliste 15). Inhalt unveraendert: Daten und Einstellungen mit
Sonderzeichen, Sortierung nach Codepunkt (auch ausserhalb der BMP und U+FFFF)
und Zahlen (sehr kleine und grosse, -0.0001, 1e-07, 3e+20, ganze Zahl ueber
2**64), Pruefsumme gerechnet von fisi_sicherung._checksum der Python-Linie.
Geschrieben wie json.dumps(..., ensure_ascii=False) in einer Zeile ohne
Zeilenende.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_pruefsumme_sonderfall.py <pfad_fisi_lernplattform> tests/Vergleich/pruefsumme_sonderfall.json
"""
import os
import sys
import tempfile


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import json
    import fisi_sicherung as sicherung

    daten = {"tables": {"z": [[1, 2.5, None, True, "ä\"\\\n\t\u0001\x7f€😀"]], "a": []},
             "Zeta": -0.0001, "éa": 1e-07, "ea": 12345678901234567890, "😀": 1, "￿": 2}
    einstellungen = {"farbe": "cyan_pink", "n": [1.0, 3e+20]}
    inhalt = {"erzeugt": "Python json.dumps wie fisi_sicherung._checksum (Sonderzeichen, "
                         "Sortierung nach Codepunkt, Zahlen)",
              "daten": daten, "einstellungen": einstellungen,
              "pruefsumme": sicherung._checksum(daten, einstellungen)}
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(json.dumps(inhalt, ensure_ascii=False))
    print("pruefsumme_sonderfall: %s -> %s" % (inhalt["pruefsumme"], ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
