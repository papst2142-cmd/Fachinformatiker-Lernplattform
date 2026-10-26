#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G45 (Phase 7, W2 "Spiel-Kern Teil 1": Tickets je
Tag), gerechnet vom echten Python-Code der Python-Linie (v0.63.2, eingefroren).

  raenge      - fisi_game.tickets_per_day fuer jeden Rang (und einen
                unbekannten), rank_for und next_rank fuer feste Werte.
  wuerfel     - fisi_game._dice fuer feste Startwerte, Tage und Salze.
  laeufe      - je Startwert (Zeitstempel des ersten Ereignisses = Wuerfel
                des Zwischenfalls) ein Lauf ueber 75 Arbeitstage: Jeden Tag
                wird GameState aus dem Protokoll neu aufgebaut; gespeichert
                werden offene Tickets, Zwischenfall des Tages, Tickets je Tag,
                Rang und Ansehen. Dann werden alle offenen Tickets als
                ticket_erledigt geschrieben (richtig, ausser jedes 7. falsch,
                Zwischenfaelle mit "zwischenfall"), jedes 11. Ticket als
                ticket_verschoben, und der Tag endet (tag_beendet).

Keine Lerndaten; alle Daten- und Einstellungsordner zeigen in einen leeren
Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_tickets.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_tickets.json
"""
import atexit
import json
import os
import shutil
import sys
import tempfile

STARTWERTE = ["2026-10-01 08:00:00", "2026-10-01 08:00:01", "2025-01-31 23:59:59",
              "2026-12-24 18:30:00", "2027-02-28 07:15:42", "2026-06-15 12:00:00",
              "2026-10-23 09:41:07", "2026-03-01 00:00:00", "", "x"]
TAGE = 75


def lauf(fg, start):
    events = [(start, fg.EV_DIFFICULTY, {"stufe": "normal"}),
              (start, fg.EV_PROFILE, {"name": "Nico", "aussehen": {}})]
    nummer = 0
    tage = []

    def stempel():
        return "%s~%06d" % (start, len(events))
    for _ in range(TAGE):
        state = fg.GameState(events)
        day = state.day
        offen = state.open_tickets()
        incident = state.incident_today()
        tage.append({"tag": day, "offen": [t["id"] for t in offen],
                     "zwischenfall": incident["id"] if incident else None,
                     "tickets_heute": state.tickets_today, "rang": state.day_rank,
                     "ansehen": state.mean_reputation, "pool": len(state._pool()),
                     "warten": [t["id"] for t in state.waiting_for_delivery()]})
        for task in offen:
            nummer += 1
            if nummer % 11 == 0:
                events.append((stempel(), fg.EV_DEFERRED, fg.defer_payload(task, day)))
                continue
            right = nummer % 7 != 0
            data = {"aufgabe": task["id"], "tag": day, "richtig": right, "geld": 40 if right else -10,
                    "reputation": {k: (2 if right else -3) for k in fg.AXIS_KEYS}}
            if task.get("zwischenfall"):
                data["zwischenfall"] = True
            events.append((stempel(), fg.EV_SOLVED, data))
        events.append((stempel(), fg.EV_DAY_END, {"tag": day, "gehalt": state.salary, "rang": state.rank}))
    return tage


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
    import fisi_game as fg

    balancing = fg.GAME["balancing"]
    namen = [r["name"] for r in balancing["raenge"]] + ["gibtsnicht", None]
    werte = [0, 10, 34.99, 35, 54.5, 55, 74.999, 75, 100]
    daten = {
        "raenge": {
            "tickets": [[n, fg.tickets_per_day(n, balancing)] for n in namen],
            "rang": [[v, d, fg.rank_for(v, balancing, d)] for v in werte for d in (None, 1, 9, 10, 29, 30, 59, 60, 200)],
            "naechster": [[v, d, fg.next_rank(v, d, balancing)] for v in werte for d in (1, 10, 30, 60, 61)],
        },
        "wuerfel": [[s, d, salz, fg._dice(s, d, salz)] for s in STARTWERTE[:4] + ["mischung"]
                    for d in (0, 1, 2, 30, 365) for salz in ("chance", "wahl", "aufgabe-1", "")],
        "laeufe": [{"start": start, "tage": lauf(fg, start)} for start in STARTWERTE],
    }
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G45: %d Laeufe je %d Tage -> %s" % (len(STARTWERTE), TAGE, ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
