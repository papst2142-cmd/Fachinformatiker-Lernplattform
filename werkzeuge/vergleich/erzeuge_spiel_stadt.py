#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G48 Teil "Stadt" (Phase 7, Weisung P7-6, Stadt-Kern
ohne Darstellung), gerechnet vom echten Python-Code der Python-Linie (v0.63.2,
eingefroren): welche Orte der Weltkarte zu sehen sind (place_unlocked) und mit
welchem Namen, Kurznamen, welcher Rolle, Ansicht und welchem Hinweis
(map_places: die Felder id, name, kurz, rolle, ansicht, hinweis).

Die Spielstaende sind dieselben wie in G44 (erzeuge_spiel_neuaufbau.py): alle
Protokolle werden mit denselben Funktionen und derselben festen uuid neu
gespielt; fuer jeden Stichpunkt n gilt GameState(events[:n]). Damit C# sicher
dasselbe Protokoll nimmt, steht je Protokoll die SHA-256 von
json.dumps(events, ensure_ascii=False, separators=(",", ":")) dabei; dazu der
Fall ohne Spielstand (state None). Dazu das Szenario 045 aus G43 (mit Filiale,
Ereignisse ohne Zeitstempel wie in spiel_fingerabdruck.json), alle 50 Ereignisse,
und dasselbe mit einem weiteren Feierabend, der die Serverfarm freischaltet
(Feld "zusatz": das angehaengte Ereignis).
Keine Lerndaten; alle Daten- und Einstellungsordner zeigen in einen leeren
Temp-Ordner, der am Ende wieder weg ist.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_stadt.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_stadt.json
"""
import contextlib
import copy
import hashlib
import io
import json
import os
import runpy
import sys
import tempfile
import uuid

HIER = os.path.dirname(os.path.abspath(__file__))
FELDER = ("id", "name", "kurz", "rolle", "ansicht", "hinweis")


def sicht(fg, state):
    regeln = fg.map_rules().get("orte") or []
    return {"frei": [fg.place_unlocked(regel, state) for regel in regeln],
            "orte": [[ort.get(k) for k in FELDER] for ort in fg.map_places(state)]}


def szenario045(fg, core, repo):
    import erzeuge_spiel_fingerabdruck as finger
    zaehler = [0]

    def stempel(_self):
        zaehler[0] += 7
        return finger.zeitstempel(zaehler[0]), 120
    core.DBManager._stamp = stempel
    spiele = []
    original = fg.Game

    class Mitschnitt(original):
        def __init__(self, *args, **kwargs):
            super().__init__(*args, **kwargs)
            spiele.append(self)
    fg.Game = Mitschnitt
    argv = sys.argv
    try:
        sys.argv = [os.path.join(HIER, "fingerabdruck.py"), repo]
        with contextlib.redirect_stdout(io.StringIO()):
            runpy.run_path(os.path.join(HIER, "fingerabdruck.py"), run_name="__main__")
    finally:
        sys.argv = argv
        fg.Game = original
    if len(spiele) != 1:
        raise SystemExit("%d Spiel-Objekte statt 1" % len(spiele))
    return [[kind, data] for _ts, kind, data in spiele[0].state.history]


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    with tempfile.TemporaryDirectory(prefix="vergleich_") as tmp:
        return erzeugen(repo, os.path.abspath(ziel), tmp)


def erzeugen(repo, ziel, tmp):
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    zufall = [0]

    # wie erzeuge_spiel_neuaufbau.main
    def feste_uuid():
        zufall[0] += 1
        return uuid.UUID(int=zufall[0] * 7919)
    uuid.uuid4 = feste_uuid
    import fisi_core as core
    import fisi_game as fg
    sys.path.insert(1, HIER)
    import erzeuge_spiel_neuaufbau as neu
    import erzeuge_spiel_firma as firma

    protokolle = []
    for name, eintraege in neu.von_hand(fg):
        events = [[neu.zeitstempel(i * 61), art, daten] for i, (art, daten) in enumerate(eintraege)]
        protokolle.append((name, events, list(range(0, len(events) + 1))))
    for name, tage, muster, schwierigkeit, wohnen in (
            ("frueh", 12, "r", "normal", False),
            ("gemischt", 45, "rrfrvrrf", "einfach", True),
            ("lang", 130, "rrrrrrrrrf", "normal", True)):
        events, stich = neu.gespielt(fg, core, tmp, name, tage, muster, schwierigkeit, wohnen)
        protokolle.append((name, [[t, k, d] for t, k, d in events], [0, 1, 2] + stich))
    for name, seed, schwierigkeit, tage in (("firma", 4711, fg.DIFF_NORMAL, 30),
                                            ("firma_einfach", 815, fg.DIFF_EASY, 20)):
        lauf = firma.Lauf(fg, core, tmp, name, seed, schwierigkeit)
        events = lauf.spielen(tage)["ereignisse"]
        protokolle.append((name, events, sorted(set(range(0, len(events), 40)) | {len(events)})))

    # Szenario 045 (G43, mit Filiale): wie erzeuge_spiel_fingerabdruck.main, zuletzt, weil es
    # die Uhr der Klasse DBManager ersetzt; Ereignisse ohne Zeitstempel wie in spiel_fingerabdruck.json
    events = szenario045(fg, core, repo)
    protokolle.append(("szenario045", events, sorted(set(range(0, len(events), 50)) | {len(events)})))
    # Serverfarm frei: ein weiterer Feierabend wie der letzte, mit firma.serverfarm_frei
    ende = copy.deepcopy([e for e in events if e[0] == fg.EV_DAY_END][-1])
    ende[1]["tag"] = int(ende[1]["tag"]) + 1
    ende[1].setdefault("firma", {})["serverfarm_frei"] = True
    zusatz = {"szenario045_farm": ende}
    protokolle.append(("szenario045_farm", events + [ende], [len(events), len(events) + 1]))

    daten = {"ohne_stand": sicht(fg, None), "protokolle": []}
    for name, events, stich in protokolle:
        text = json.dumps(events, ensure_ascii=False, separators=(",", ":"))
        staende = []
        for n in stich:
            state = fg.GameState([tuple(e) if len(e) == 3 else ("",) + tuple(e) for e in events[:n]])
            eintrag = {"n": n}
            eintrag.update(sicht(fg, state))
            staende.append(eintrag)
        eintrag = {"name": name, "sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(), "staende": staende}
        if name in zusatz:
            eintrag["zusatz"] = zusatz[name]
        daten["protokolle"].append(eintrag)
    os.makedirs(os.path.dirname(ziel), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G48 Stadt: %d Protokolle, %d Staende -> %s"
          % (len(protokolle), sum(len(p[2]) for p in protokolle), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
