#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G43 (Phase 7, W3 "Spiel-Kern Teil 2"): den
Spielwerte-Fingerabdruck der Python-Linie (v0.63.2, eingefroren).

Das Werkzeug fingerabdruck.py (mit szenario045.py, beide unveraendert aus
dem Bericht 0.54 uebernommen) laeuft hier selbst: Es spielt den spaeten
Demo-Stand (Szenario 045: Firma, Ausbau, Filiale, Raeume, Werbung,
Zertifizierung, Kredit, 8 weitere Tage) und druckt den Hash. Dieser
Erzeuger faengt dabei nur das Spiel-Objekt ab und schreibt zusaetzlich
jeden Einzelwert: alle Ereignisse [Art, Daten], die Werte des Hashes, den
Hash selbst, den Wissensstand der leeren Datenbank (knowledge,
topic_knowledge) und die Bestenliste. Die C#-Seite spielt dasselbe
Szenario und vergleicht jeden Einzelwert und den Hash.

Keine Lerndaten; alle Daten- und Einstellungsordner zeigen in einen leeren
Temp-Ordner. Die Uhr der Datenbank ist fest (jede Zeile 7 s spaeter), damit
die Datei bei jedem Lauf gleich ist; der Hash haengt nicht von der Uhr ab.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_fingerabdruck.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_fingerabdruck.json
"""
import atexit
import contextlib
import io
import json
import os
import runpy
import shutil
import sys
import tempfile

SOLL = "f2b59ce8f78a533cc3ec565ab983d418959e788d8ff16d35a907de328a626656"
HIER = os.path.dirname(os.path.abspath(__file__))


def zeitstempel(nummer):
    return "2026-10-%02d %02d:%02d:%02d" % (1 + nummer // 86400, (nummer // 3600) % 24,
                                            (nummer // 60) % 60, nummer % 60)


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    atexit.register(shutil.rmtree, tmp, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
    tempfile.tempdir = tmp   # Sammelliste 91: auch die Datenbank von fingerabdruck.py (f.db) liegt im Temp-Ordner
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    sys.path.insert(1, HIER)
    import fisi_core as core
    import fisi_game as fg

    zaehler = [0]

    def stempel(_self):
        zaehler[0] += 7
        return zeitstempel(zaehler[0]), 120
    core.DBManager._stamp = stempel

    spiele = []
    original = fg.Game

    class Mitschnitt(original):
        def __init__(self, *args, **kwargs):
            super().__init__(*args, **kwargs)
            spiele.append(self)
    fg.Game = Mitschnitt
    ausgabe = io.StringIO()
    argv = sys.argv
    try:
        sys.argv = [os.path.join(HIER, "fingerabdruck.py"), repo]
        with contextlib.redirect_stdout(ausgabe):
            runpy.run_path(os.path.join(HIER, "fingerabdruck.py"), run_name="__main__")
    finally:
        sys.argv = argv
        fg.Game = original
    zeilen = ausgabe.getvalue().splitlines()
    hash_text = next(z.split()[1] for z in zeilen if z.startswith("Fingerabdruck "))
    if hash_text != SOLL:
        raise SystemExit("Fingerabdruck %s statt %s" % (hash_text, SOLL))
    if len(spiele) != 1:
        raise SystemExit("%d Spiel-Objekte statt 1" % len(spiele))
    game = spiele[0]
    s = game.state
    events = [[kind, data] for _ts, kind, data in s.history]
    werte = {
        "tag": s.day, "geld": round(s.money, 6), "ansehen": s.reputation,
        "mitarbeiter": sorted((m["id"], m.get("gehalt"), json.dumps(m.get("werte"), sort_keys=True))
                              for m in s.staff_list()),
        "stimmung": sorted(s.mood.items()),
    }
    bestenliste = [[art, schluessel, wert, daten] for _ts, _lauf, art, schluessel, wert, daten
                   in game.records()]
    daten = {"hash": hash_text, "ausgabe": zeilen, "ereignisse": events, "werte": werte,
             "wissen": game.knowledge(), "themen_wissen": game.topic_knowledge(),
             "lernprojekte": sorted(game.learned_projects()), "bestenliste": bestenliste}
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G43: %d Ereignisse, Tag %d, Hash %s -> %s" % (len(events), s.day, hash_text[:8], ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
