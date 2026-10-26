#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G17 (Phase 2, Pakete L2 "Antworten speichern" und
L6 "Pruefungstrainer"), gerechnet vom echten Python-Code der Python-Linie (v0.63.2):

  noten     - Ergebnis einer Session wie finish_quiz (app_gui.QuizView und
              mobile/src/main.py gleich): percentage = (score / total) * 100,
              fisi_core.ihk_note(percentage), de_number(percentage, 1); fuer alle
              Kombinationen bis 60 Fragen und an den Notengrenzen
  zahlen    - fisi_core.de_number fuer Rundungsfaelle (kaufmaennisch) mit 0, 1, 2 Stellen
  stempel   - DBManager._stamp (Ortszeit, Abstand zu UTC in Minuten) und
              format_with_zone fuer feste Zeitpunkte in der Zone Europe/Berlin, auch
              in der Stunde der Zeitumstellung; ohne gemessene Uhrabweichung
  zeilen    - Zeilen, die log_card, log_quiz_answer und save_test_result schreiben
              (alle Spalten ausser id), mit festem Zeitpunkt und fester uid

Die Zeitzone wird ueber TZ=Europe/Berlin und time.tzset() gesetzt (nur Linux/macOS),
device_now liefert den festen Zeitpunkt in dieser Zone.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_trainer.py <pfad_fisi_lernplattform> tests/Vergleich/trainer.json
"""
import atexit
import datetime
import json
import os
import shutil
import sqlite3
import sys
import tempfile
import time

ZONE = "Europe/Berlin"

MOMENTE = [
    "2026-10-20T08:15:30.000000+00:00",
    "2026-10-20T21:59:59.999999+00:00",   # 23:59:59 Ortszeit, Bruchteil faellt weg
    "2026-10-20T22:00:00.000000+00:00",   # Mitternacht Ortszeit
    "2026-10-25T00:59:59.000000+00:00",   # 02:59:59 Sommerzeit
    "2026-10-25T01:00:00.000000+00:00",   # 02:00:00 Winterzeit (doppelte Stunde)
    "2026-10-25T01:30:00.000000+00:00",
    "2027-03-28T00:59:59.000000+00:00",   # 01:59:59 Winterzeit
    "2027-03-28T01:00:00.000000+00:00",   # 03:00:00 Sommerzeit
    "2026-12-31T23:00:00.000000+00:00",   # Jahreswechsel Ortszeit
]

NOTENGRENZEN = [100.0, 92.0, 91.99999999999999, 81.0, 80.99999999999999, 67.0, 66.99999999999999,
                50.0, 49.99999999999999, 30.0, 29.999999999999996, 0.0]

ZAHLEN = [0.0, -0.0, 0.05, 0.15, 0.25, 0.35, 2.675, 1.005, 12345.675, 1234567.891, -0.04, -1.25,
          99.95, 99.94999999999999, 33.333333333333336, 66.66666666666667, 1e-7, 4.5, 5.5]


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    os.environ["TZ"] = ZONE
    time.tzset()
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    atexit.register(shutil.rmtree, tmp, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as core

    noten = []
    for total in range(1, 61):
        for score in range(total + 1):
            percentage = (score / total) * 100 if total else 0.0
            noten.append([score, total, percentage, core.ihk_note(percentage),
                          core.de_number(percentage, 1)])
    grenzen = [[p, core.ihk_note(p), core.de_number(p, 1)] for p in NOTENGRENZEN]
    zahlen = [[x, d, core.de_number(x, d)] for x in ZAHLEN for d in (0, 1, 2)]

    jetzt = [None]
    core.device_now = lambda: jetzt[0].astimezone()
    zaehler = [0]

    def uid():
        zaehler[0] += 1
        return "trainer-uid-%04d" % zaehler[0]

    core.DBManager._uid = staticmethod(uid)
    db = core.DBManager(os.path.join(tmp, "trainer.db"))
    stempel = []
    for text in MOMENTE:
        jetzt[0] = datetime.datetime.fromisoformat(text)
        zeit, abstand = db._stamp()
        stempel.append({"utc": text, "zeit": zeit, "abstand": abstand,
                        "mit_zone": core.format_with_zone(core.device_now())})

    aufrufe = []
    karte = core.KARTEIKARTEN[3]
    frage = core.QUIZ_QUESTIONS[5]
    plan = [
        ("karte", [karte["cat"], karte["q"], "lernen", True]),
        ("karte", [karte["cat"], karte["q"], "mc", False]),
        ("karte", [karte["cat"], karte["q"], "aufdecken", None]),
        ("karte", ["Netzwerk", "Frage mit „Anführungszeichen“ und 😀", "frei", True]),
        ("quiz", [frage["cat"], frage["q"], True]),
        ("quiz", [frage["cat"], frage["q"], False]),
        ("ergebnis", [7, 10, 70.0, core.ihk_note(70.0), 185]),
        ("ergebnis", [1, 3, (1 / 3) * 100, core.ihk_note((1 / 3) * 100), 0]),
        ("ergebnis", [0, 0, 0.0, core.ihk_note(0.0), 59]),
    ]
    for nummer, (art, argumente) in enumerate(plan):
        jetzt[0] = datetime.datetime.fromisoformat(MOMENTE[nummer % len(MOMENTE)])
        if art == "karte":
            db.log_card(*argumente)
        elif art == "quiz":
            db.log_quiz_answer(*argumente)
        else:
            db.save_test_result(*argumente)
        aufrufe.append({"art": art, "argumente": argumente, "utc": MOMENTE[nummer % len(MOMENTE)]})
    conn = sqlite3.connect(db.db_path)
    zeilen = {}
    for tabelle in ("card_events", "quiz_answers", "test_results"):
        cur = conn.execute("SELECT * FROM %s ORDER BY id" % tabelle)
        spalten = [d[0] for d in cur.description]
        zeilen[tabelle] = {"spalten": [s for s in spalten if s != "id"],
                           "zeilen": [[w for s, w in zip(spalten, r) if s != "id"] for r in cur.fetchall()],
                           "typen": [list(r) for r in conn.execute(
                               "SELECT %s FROM %s ORDER BY id" % (
                                   ", ".join("typeof(%s)" % s for s in spalten if s != "id"), tabelle))]}
    conn.close()

    daten = {
        "quelle": "fisi_core.ihk_note, de_number, DBManager._stamp, format_with_zone, log_card, "
                  "log_quiz_answer, save_test_result; finish_quiz (app_gui, mobile) (Python v0.63.2)",
        "python": sys.version.split()[0],
        "zone": ZONE,
        "noten": noten,
        "notengrenzen": grenzen,
        "zahlen": zahlen,
        "stempel": stempel,
        "aufrufe": aufrufe,
        "zeilen": zeilen,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G17: %d Noten, %d Zahlen, %d Stempel, %d Aufrufe -> %s"
          % (len(noten), len(zahlen), len(stempel), len(aufrufe), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
