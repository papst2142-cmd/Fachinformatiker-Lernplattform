#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Sollwert-Aenderung Nr. 23 (Welle A 1.2.0, Weisungen P12-335/P12-336): die C#-Sollwerte der Spiel-Erzeuger auf den breiten
Grundrissen nachrechnen.

Legt eine KOPIE der eingefrorenen Python-Linie (v0.63.2) an und setzt dort in fisi_game.load_game_content die Ersatzstuecke
aus inhalte_csharp/spiel/grundrisse_breit.json (Bereich "inhalt") ein, genau wie GrundrisseBreit.Einsetzen in C#: ein Stueck
gilt nur, wenn der alte Wert die Pruefsumme "alt" hat; passt eines nicht, bricht die Kopie beim Laden ab. Das Original wird
nie veraendert. Danach die Erzeuger werkzeuge/vergleich/erzeuge_spiel_*.py mit der Kopie aufrufen (geaendert haben sich
spiel_inhalte, spiel_pruefung, spiel_aktionen, spiel_firma, spiel_neuaufbau, spiel_orte, spiel_wege; alle anderen und der
Fingerabdruck bleiben byte-gleich).

Aufruf (Python 3.12, im Repo-Ordner):
  python3 -I werkzeuge/vergleich/breite_grundrisse_python.py <pfad_fisi_lernplattform_v0.63.2> <ziel_ordner>
"""
import os
import shutil
import sys

PATCH = '# --- Sollwert-Aenderung Nr. 23 (Weisung P12-335): breite Grundrisse der C#-Linie einsetzen ---\n_lgc_ohne_breit = load_game_content\n\n\ndef load_game_content(folder=None):\n    import hashlib as _h\n    content = _lgc_ohne_breit(folder)\n    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "_grundrisse_breit.json"), encoding="utf-8") as f:\n        daten = json.load(f)\n\n    def summe(v):\n        return "sha256:" + _h.sha256(json.dumps(v, ensure_ascii=False, separators=(",", ":"), sort_keys=True).encode("utf-8")).hexdigest()\n\n    gesetzt = 0\n    for e in daten["ersatz"]:\n        if e["bereich"] != "inhalt":\n            continue\n        halter, schluessel, knoten = None, None, content\n        for teil in e["pfad"]:\n            halter = knoten\n            if isinstance(teil, str) and teil.startswith("@id:"):\n                schluessel = next(i for i, x in enumerate(knoten) if isinstance(x, dict) and x.get("id") == teil[4:])\n            else:\n                schluessel = teil\n            knoten = halter[schluessel]\n        if summe(knoten) == e["alt"]:\n            halter[schluessel] = json.loads(json.dumps(e["neu"]))\n            gesetzt += 1\n    if gesetzt != sum(1 for e in daten["ersatz"] if e["bereich"] == "inhalt"):\n        raise RuntimeError(f"breite Grundrisse: nur {gesetzt} Ersatzstuecke passten")\n    return content\n\n'

def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    quelle, ziel = sys.argv[1], sys.argv[2]
    if os.path.exists(ziel):
        print("FEHLER: Ziel gibt es schon: " + ziel)
        return 2
    shutil.copytree(quelle, ziel, ignore=shutil.ignore_patterns(".git"))
    shutil.copyfile("inhalte_csharp/spiel/grundrisse_breit.json", os.path.join(ziel, "_grundrisse_breit.json"))
    pfad = os.path.join(ziel, "fisi_game.py")
    with open(pfad, encoding="utf-8") as datei:
        text = datei.read()
    anker = "\nGAME = load_game_content()\n"
    if text.count(anker) != 1:
        print("FEHLER: Anker GAME = load_game_content() nicht genau einmal gefunden")
        return 1
    with open(pfad, "w", encoding="utf-8", newline="\n") as datei:
        datei.write(text.replace(anker, "\n\n" + PATCH + anker))
    print("Kopie mit breiten Grundrissen: " + ziel)
    return 0


if __name__ == "__main__":
    sys.exit(main())
