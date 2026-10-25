#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G50 (Phase 7, W6 "Spiel-Ansichten", Weisung P7-11),
gerechnet vom echten Python-Code der Python-Linie (v0.63.2, eingefroren): die
Funktionen von fisi_game, die die Ansichten der Handy-App (mobile/src/spiel.py)
zum Anzeigen rufen.

  ergebnis - je Auftrag und Antwort von G46 (gleiche Reihenfolge wie
             spiel_bewertung.json "aufgaben"/"antworten", Lage A: ohne Hilfe,
             kein Wissen, Tag 3) result_text und reaction_text der Nutzdaten;
             fuer Racks rack_bars und rack_checks, fuer Formulare form_check,
             fuer Diagnosen mit Austausch spare_catalog (Kennungen).
  ersatz   - spare_kinds und spare_delivery_days.
  wissen   - knowledge, topic_knowledge (mit topic_params) und
             Game.learned_projects fuer die Testdatenbank testdaten.db (G6)
             und eine leere Datenbank.
  figur    - avatar_shapes fuer die Grundfigur und je Teil jede Auswahl.

Ausnahmen in Python werden als {"ausnahme": Klassenname} gespeichert. Keine
Lerndaten ausser der Testdatenbank; alle Daten- und Einstellungsordner zeigen in
einen leeren Temp-Ordner, uuid4 ist fest.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_ansichten.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_ansichten.json
"""
import copy
import json
import os
import shutil
import sys
import tempfile
import uuid

HIER = os.path.dirname(os.path.abspath(__file__))


def liste(v):
    """Tupel als Liste (JSON), sonst unveraendert."""
    if isinstance(v, (tuple, list)):
        return [liste(x) for x in v]
    if isinstance(v, dict):
        return {k: liste(x) for k, x in v.items()}
    return v


def ergebnis(fg, bewertung):
    """Wie erzeuge_spiel_bewertung.aufgaben (Lage A), nur die Texte und Anzeigewerte."""
    personen = [p["id"] for p in fg.all_people()] if hasattr(fg, "all_people") else []
    zaehler = {}
    auftraege = []
    for task in fg.GAME["aufgaben"]:
        auftraege.append(task)
        zaehler[task["typ"]] = zaehler.get(task["typ"], 0) + 1
        if zaehler[task["typ"]] > 3:
            continue
        extra = {"zwischenfall": True, "verlust": 7, "achsen": ["sicherheit", "fachkompetenz", "sicherheit"]}
        if task["typ"] == "rack":
            extra["lager_ab"] = max(0, len(task["geraete"]) - 2)
        if task["typ"] == "bestellung":
            extra.update(kein_sparbonus=True, empfaenger=personen[0] if personen else "kunde")
        auftraege.append(dict(task, **extra))
    result = []
    for task in auftraege:
        loesung = bewertung.sicher(fg.find_solution, task)
        antworten = bewertung.mutationen(fg, task, loesung if not isinstance(loesung, dict) or "ausnahme" not in loesung else None)
        loesung_b = bewertung.sicher(fg.find_solution, task, list(task.get("teile", [])) + [
            pid for i, pid in enumerate(bewertung.hardware_ids(fg)) if i % 3 > 0])
        if task["typ"] == "bauteile" and loesung_b and loesung_b != loesung:
            antworten.append(("loesung_b", loesung_b))
        eintrag = {"id": task["id"], "antworten": []}
        if task["typ"] == "diagnose" and task.get("austausch"):
            eintrag["ersatz"] = [item["id"] for item in fg.spare_catalog(task)]
        for name, answer in antworten:
            daten = bewertung.sicher(fg.evaluate, task, copy.deepcopy(answer), False, {}, 3)
            zeile = {"name": name}
            if "ausnahme" in daten:
                zeile["ausnahme"] = daten["ausnahme"]
            else:
                zeile["text"] = bewertung.sicher(fg.result_text, task, daten)
                zeile["reaktion"] = bewertung.sicher(fg.reaction_text, task, daten)
            if task["typ"] == "rack":
                zeile["balken"] = liste(bewertung.sicher(fg.rack_bars, task, copy.deepcopy(answer)))
                zeile["regeln"] = liste(bewertung.sicher(fg.rack_checks, task, copy.deepcopy(answer)))
            if task["typ"] == "formular":
                zeile["felder"] = bewertung.sicher(fg.form_check, task, copy.deepcopy(answer))
            eintrag["antworten"].append(zeile)
        result.append(eintrag)
    return result


def wissen(fg, core, tmp, quelle):
    bal = fg.GAME["balancing"]
    werte = {}
    for name, pfad in (("testdaten", quelle), ("leer", None)):
        ziel = os.path.join(tmp, "wissen_" + name + ".db")
        if pfad:
            shutil.copyfile(pfad, ziel)
        db = core.DBManager(ziel)
        game = fg.Game(db, "PC")
        werte[name] = {
            "fachbereiche": fg.knowledge(db, bal["wissen"]),
            "themen": fg.topic_knowledge(db, fg.topic_params(bal)),
            "projekte": sorted(game.learned_projects()),
        }
    return werte


def figur(fg):
    faelle = [["grund", dict(fg.DEFAULT_APPEARANCE)]]
    for teil, optionen in fg.APPEARANCE.items():
        for wert, _name in optionen:
            faelle.append(["%s=%s" % (teil, wert), dict(fg.DEFAULT_APPEARANCE, **{teil: wert})])
    faelle.append(["unbekannt", {"haut": "gibtsnicht", "frisur": None}])
    return [[name, aussehen, liste(fg.avatar_shapes(aussehen))] for name, aussehen in faelle]


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    quelle = os.path.join(HIER, "..", "..", "tests", "Vergleich", "testdaten.db")
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    zufall = [0]

    def feste_uuid():
        zufall[0] += 1
        return uuid.UUID(int=zufall[0] * 7919)
    uuid.uuid4 = feste_uuid
    import fisi_core as core
    import fisi_game as fg
    sys.path.insert(0, HIER)
    import erzeuge_spiel_bewertung as bewertung

    daten = {
        "ergebnis": ergebnis(fg, bewertung),
        "ersatz": {"arten": fg.spare_kinds(), "liefertage": fg.spare_delivery_days()},
        "wissen": wissen(fg, core, tmp, quelle),
        "figur": figur(fg),
    }
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    anzahl = sum(len(a["antworten"]) for a in daten["ergebnis"])
    print("G50: %d Auftraege, %d Antworten, %d Figuren -> %s" % (len(daten["ergebnis"]), anzahl, len(daten["figur"]), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
