#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G49 (Phase 7, W2 "Spiel-Kern Teil 1":
Anzeigetexte des Kerns fuer die Ticket-Bretter), gerechnet vom echten
Python-Code der Python-Linie (v0.63.2, eingefroren).

  aufgaben  - je Auftrag aus GAME (dazu je Rack-Auftrag einer mit lager_ab):
              gap_warning fuer drei Wissensstaende, solution_text (ohne und mit
              Lagerteilen) und die Texte seines Typs: rack_header,
              rack_source_text, rack_lines, rack_summary, rack_place,
              rack_occupant; form_given; terminal_step_label, terminal_log,
              terminal_current_step, terminal_commands; diagnosis_counter,
              spare_parts_text, exchange_steps; maintenance_counter;
              order_header, cart_lines, cart_summary; build_rules_text. Die
              Antworten sind dieselben wie in G46 (erzeuge_spiel_bewertung.py).
  teile     - part_specs fuer alle Bauteile, Ersatzteile und Rack-Geraete.
  raenge    - rank_hint fuer Spielstaende mit steigendem Ansehen.

Keine Lerndaten; alle Daten- und Einstellungsordner zeigen in einen leeren
Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_texte.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_texte.json
"""
import atexit
import json
import os
import shutil
import sys
import tempfile


def texte_je_aufgabe(fg, bw, task, lager_ids):
    typ = task["typ"]
    loesung = fg.find_solution(task)
    antworten = [a for _n, a in bw.mutationen(fg, task, loesung)]
    themen = list(task.get("anforderungen") or {})
    wissen = [{}, {k: 100.0 for k in themen}, {k: (v - 0.5 if i % 2 == 0 else v + 1.7)
                                               for i, (k, v) in enumerate((task.get("anforderungen") or {}).items())}]
    result = {"id": task["id"],
              "luecken": [fg.gap_warning(fg.requirement_gaps(task, w)) for w in wissen],
              "loesung": fg.solution_text(task), "loesung_b": fg.solution_text(task, lager_ids)}
    if typ == "rack":
        result["kopf"] = fg.rack_header(task)
        result["herkunft"] = [fg.rack_source_text(task, i) for i in range(len(task["geraete"]))]
        result["zeilen"] = [fg.rack_lines(task, a) for a in antworten]
        result["zusammen"] = [list(fg.rack_summary(task, a)) for a in antworten]
        groesse = task["schrank"]["he"]
        setzen = []
        for a in antworten[:3]:
            if not isinstance(a, dict) or any(not str(k).lstrip("-").isdigit() or int(k) not in range(len(task["geraete"]))
                                              for k in a):
                continue
            for i in range(len(task["geraete"])):
                for unten in (1, groesse // 2, groesse, groesse + 5, -3):
                    setzen.append([a, i, unten, fg.rack_place(task, a, i, unten)])
            setzen.append([a, "belegt", [fg.rack_occupant(task, a, u) for u in range(0, groesse + 2)]])
        result["setzen"] = setzen
    elif typ == "formular":
        result["ausgang"] = fg.form_given(task)
    elif typ == "terminal":
        result["schritte"] = [fg.terminal_step_label(task, i) for i in range(len(task["schritte"]))]
        versuche = [fg.terminal_attempts(task, a) for a in antworten]
        result["versuche"] = versuche
        result["protokoll"] = [[list(z) for z in fg.terminal_log(task, v)] for v in versuche]
        result["aktuell"] = [fg.terminal_current_step(task, v[:k]) for v in versuche for k in range(len(v) + 1)]
        result["befehle"] = fg.terminal_commands(task)
    elif typ == "diagnose":
        result["zaehler"] = [fg.diagnosis_counter(task, ["x"] * n) for n in (0, 1, 2, 5)]
        result["ersatz"] = [fg.spare_parts_text(task, v) for v in (None, [], lager_ids, list(reversed(lager_ids)))]
        result["schritte_wahl"] = fg.exchange_steps(task)
    elif typ == "wartung":
        result["zaehler"] = [fg.maintenance_counter(task, (a or {}).get("bewertung") if isinstance(a, dict) else None)
                             for a in antworten] + [fg.maintenance_counter(task, {"gibtsnicht": "auffaellig"})]
    elif typ == "bestellung":
        result["kopf"] = fg.order_header(task)
        result["korb"] = [fg.cart_lines(task, a) for a in antworten if all(str(v).isdigit() for v in (a or {}).values())]
        result["zusammen"] = [list(fg.cart_summary(task, a)) for a in antworten if all(str(v).isdigit() for v in (a or {}).values())]
    elif typ == "bauteile":
        result["vorgaben"] = fg.build_rules_text(task)
    return result


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    atexit.register(shutil.rmtree, tmp, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import fisi_game as fg
    import erzeuge_spiel_bewertung as bw

    hw = fg.GAME["hardware"]
    ids = [item["id"] for item in hw["teile"] + hw.get("ersatzteile", [])]
    lager_ids = [pid for i, pid in enumerate(ids) if i % 3]
    aufgaben = []
    for task in fg.GAME["aufgaben"]:
        aufgaben.append(texte_je_aufgabe(fg, bw, task, lager_ids))
        if task["typ"] == "rack":
            extra = dict(task, lager_ab=max(0, len(task["geraete"]) - 2))
            eintrag = texte_je_aufgabe(fg, bw, extra, lager_ids)
            eintrag["extra"] = {"lager_ab": extra["lager_ab"]}
            aufgaben.append(eintrag)
    teile = [[item["id"], fg.part_specs(item)] for item in hw["teile"] + hw.get("ersatzteile", []) + hw.get("rack_geraete", [])]

    events = [("2026-10-01 08:00:00", fg.EV_DIFFICULTY, {"stufe": "normal"}),
              ("2026-10-01 08:00:01", fg.EV_PROFILE, {"name": "Nico", "aussehen": {}})]
    raenge = []
    for tag in range(1, 75):
        state = fg.GameState(events)
        raenge.append([len(events), fg.rank_hint(state)])
        events.append(("2026-10-01 09:%02d:%02d" % (tag // 60, tag % 60), fg.EV_SOLVED,
                       {"aufgabe": "r%d" % tag, "tag": state.day, "richtig": True, "geld": 10,
                        "reputation": {k: (4 if tag % 9 else -9) for k in fg.AXIS_KEYS}}))
        events.append(("2026-10-01 10:%02d:%02d" % (tag // 60, tag % 60), fg.EV_DAY_END,
                       {"tag": state.day, "gehalt": state.salary, "rang": state.rank}))
    daten = {"aufgaben": aufgaben, "teile": teile, "raenge": {"ereignisse": [list(e) for e in events], "hinweise": raenge}}
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G49: %d Auftraege, %d Teile, %d Rang-Hinweise -> %s" % (len(aufgaben), len(teile), len(raenge), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
