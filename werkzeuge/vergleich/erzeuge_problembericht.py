#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G38 (Phase 5, Paket BE5 "Problem melden"): Kopf und ganzer Bericht
(fisi_diagnose.report_lines, build_report), Groessen (_size_text, _db_size), Geraet (device_text)
und Dateiname (default_name), gerechnet vom echten Python-Code der Python-Linie (v0.63.2). Nur
synthetische Testdaten (Auflage A3).

Aufbau:
  groessen    - [Bytes, _size_text]
  dbgroesse   - je Fall die Groessen von Datenbank, -wal, -journal (null = fehlt) und _db_size
  geraet      - [geraet, system, release, machine, device_text] (platform.* fest vorgegeben)
  namen       - [Zeitpunkt, default_name]
  berichte    - je Fall: einstellungen, jetzt, version, geraet, Datenbank (null oder Zeilen je
                Tabelle, "kaputt" = Datei ohne SQLite-Kopf), Dateien (fehler.log, haenger.log,
                laeuft.info im Protokollordner, update.log im Datenordner; "{HEIM}" steht fuer das
                Benutzerverzeichnis des Laufs) und der
                erwartete Bericht. Fest: platform.system/release/machine = Linux, 6.8.0-45-generic,
                x86_64; platform.python_version = 3.12.0 (C# schreibt dort "Laufzeit: ...",
                Wortliste W2); HOME = <tmp>/home/kim, Datenordner = HOME/daten, Protokollordner =
                HOME/logs; APPDATA und LOCALAPPDATA = feste Windows-Pfade (siehe UMGEBUNG).
                Die Zeile "Datenbankgröße" haengt an der Datei, die Python bzw. C# beim Oeffnen
                anlegt; der C#-Test prueft sie gegen die eigene Groesse (_size_text in "groessen").
  tabellen    - Spalten je Tabelle (EVENT_TABLES, GAME_TABLES) zum Fuellen im C#-Test

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_problembericht.py <pfad_fisi_lernplattform> tests/Vergleich/problembericht.json
"""
import atexit
import datetime
import json
import os
import platform
import shutil
import sqlite3
import sys
import tempfile

UMGEBUNG = {"APPDATA": "C:\\Users\\kim\\AppData\\Roaming", "LOCALAPPDATA": "C:\\Users\\kim\\AppData\\Local"}
ZEIT = "2026-10-20 10:00:00"


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    atexit.register(shutil.rmtree, tmp, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
    heim = os.path.join(tmp, "home", "kim")
    daten = os.path.join(heim, "daten")
    logs = os.path.join(heim, "logs")
    os.makedirs(daten)
    os.makedirs(logs)
    for name in ("XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["HOME"] = heim
    os.environ.update(UMGEBUNG)
    os.environ["FISI_DB_PATH"] = os.path.join(daten, "lernplattform.db")
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_diagnose as fdg

    platform.system = lambda: "Linux"
    platform.release = lambda: "6.8.0-45-generic"
    platform.machine = lambda: "x86_64"
    platform.python_version = lambda: "3.12.0"

    # -- Groessen ------------------------------------------------------------------------
    groessen = [[n, fdg._size_text(n)] for n in
                [0, 1, 512, 1023, 1024, 1075, 1126, 1280, 1331, 1536, 10240, 10291, 102400, 1048063,
                 1048575, 1048576, 1101005, 1153434, 1310720, 1572864, 10485760, 1073741824, 5000000000]]

    dbgroesse = []
    for nr, (haupt, wal, journal) in enumerate([(1000, None, None), (4096, 32768, None), (4096, None, 512),
                                                 (None, 100, 200), (None, None, None), (0, 0, 0)]):
        pfad = os.path.join(tmp, "groesse_%d.db" % nr)
        for endung, n in (("", haupt), ("-wal", wal), ("-journal", journal)):
            if n is not None:
                with open(pfad + endung, "wb") as handle:
                    handle.write(b"x" * n)
        dbgroesse.append({"db": haupt, "wal": wal, "journal": journal, "summe": fdg._db_size(pfad),
                          "text": fdg._size_text(fdg._db_size(pfad))})

    # -- Geraet ---------------------------------------------------------------------------
    geraet = []
    for g, s, r, m in [("PC", "Linux", "6.8.0-45-generic", "x86_64"), ("PC", "Windows", "11", "AMD64"),
                       ("Handy", "Linux", "5.15.123-android14", "aarch64"), ("", "Darwin", "24.0.0", "arm64"),
                       ("PC", "Linux", "", "x86_64"), ("PC", "", "", ""), ("PC", "Windows", "10", "")]:
        platform.system, platform.release, platform.machine = (lambda v=s: v), (lambda v=r: v), (lambda v=m: v)
        geraet.append([g, s, r, m, fdg.device_text(g)])
    platform.system = lambda: "Linux"
    platform.release = lambda: "6.8.0-45-generic"
    platform.machine = lambda: "x86_64"

    namen = [[t, fdg.default_name(datetime.datetime.fromisoformat(t))] for t in
             ["2026-10-02T14:23:59", "2026-01-01T00:00:00", "2026-12-31T23:59:30"]]

    # -- Berichte -------------------------------------------------------------------------
    spalten = {**core.EVENT_TABLES, **core.GAME_TABLES}

    def datenbank(inhalt):
        pfad = os.path.join(daten, "lernplattform.db")
        for endung in ("", "-wal", "-journal"):
            if os.path.exists(pfad + endung):
                os.remove(pfad + endung)
        if inhalt is None:
            return None
        if inhalt == "kaputt":
            with open(pfad, "wb") as handle:
                handle.write(b"Das ist keine Datenbank. " * 40)
            return core.DBManager(pfad, error_handler=lambda *_: None)
        core.DBManager(pfad)
        conn = sqlite3.connect(pfad)
        for tabelle, anzahl in inhalt.items():
            namen_ = spalten[tabelle]
            werte = [ZEIT if s == "timestamp" else "x" for s in namen_]
            for _ in range(anzahl):
                conn.execute("INSERT INTO %s (%s) VALUES (%s)" % (tabelle, ", ".join(namen_), ", ".join("?" * len(namen_))), werte)
        conn.commit()
        conn.close()
        return core.DBManager(pfad)

    def lang(kopf, zeilen, laenge):
        return "".join("%s %05d %s\n" % (kopf, i, "ä" * (laenge % 7)) for i in range(zeilen))

    token = "geheimertoken123"
    faelle = [
        {"name": "leer", "einstellungen": {}, "db": None, "dateien": {}},
        {"name": "mit_datenbank", "einstellungen": {"ziel_an": False}, "db": {"card_events": 3, "quiz_answers": 2, "spiel_ereignisse": 1},
         "dateien": {"fehler.log": "=== 2026-10-19 08:00:00 | Version A 0.10.0 | linux ===\nFehler in /home/anna/projekt/datei.py\n"}},
        {"name": "einzahl", "einstellungen": {"ziel_anzahl": 1}, "db": {"test_results": 1, "spiel_ereignisse": 1}, "dateien": {}},
        {"name": "tausend", "einstellungen": {"ziel_anzahl": "abc", "sync_repo": "kim/lernstand", "sync_token": token},
         "db": {"card_events": 1234, "pruefungen": 2, "trainer_aufgaben": 5, "ap1_events": 1, "scenario_events": 1,
                "project_events": 1},
         "dateien": {"fehler.log": "Zugang " + token + " und ghp_abcdefghijklmnopqrstuvwxyz0123456789 und Authorization: Bearer abcdefghijkl\n"
                                   "Pfad C:\\Users\\kim\\AppData\\Roaming\\Lernplattform\\x.db und {HEIM}/daten/lernplattform.db\n"}},
        {"name": "kaputt", "einstellungen": {"ziel_anzahl": 100, "sync_repo": "kim/lernstand"}, "db": "kaputt", "dateien": {}},
        {"name": "alle_protokolle", "einstellungen": {"ziel_anzahl": 35},
         "db": {"quiz_answers": 7},
         "dateien": {"fehler.log": "\n\n  nur ein Eintrag mit Leerraum  \n\n",
                     "update.log": "2026-10-19 08:00:00 | Programm | Update gesucht\n",
                     "haenger.log": "=== 2026-10-19 08:00:00 | Start | Version A 0.10.0 | linux | pid 4711 ===\n",
                     "laeuft.info": "pid=4711\nstart=2026-10-19 08:00:00\nversion=A 0.10.0\nsystem=linux\n"}},
        {"name": "gekuerzt", "einstellungen": {"sync_repo": "  ", "sync_token": token},
         "db": None,
         "dateien": {"update.log": lang("update", 900, 3),
                     "haenger.log": lang("haenger", 800, 5),
                     "laeuft.info": lang("info", 300, 2),
                     "fehler.log": ""}},
        {"name": "ohne_umbruch", "einstellungen": {},
         "db": None,
         "dateien": {"update.log": "u" * 20000, "haenger.log": "h" * 20000, "laeuft.info": "i" * 5000}},
    ]
    berichte = []
    for nr, fall in enumerate(faelle):
        for name in ("fehler.log", "haenger.log", "laeuft.info"):
            pfad = os.path.join(logs, name)
            if os.path.exists(pfad):
                os.remove(pfad)
        update = os.path.join(daten, "update.log")
        if os.path.exists(update):
            os.remove(update)
        db = datenbank(fall["db"])
        for name, inhalt in fall["dateien"].items():
            pfad = update if name == "update.log" else os.path.join(logs, name)
            with open(pfad, "w", encoding="utf-8", newline="\n") as handle:
                handle.write(inhalt.replace("{HEIM}", heim))
        jetzt = datetime.datetime(2026, 10, 20, 10, nr, 30)
        geraet_ = "Handy" if nr % 2 else "PC"
        version = "A 0.10.0"
        text = fdg.build_report(db, version, geraet_, fall["einstellungen"], log_path=os.path.join(logs, "fehler.log"), now=jetzt)
        berichte.append({"name": fall["name"], "einstellungen": fall["einstellungen"], "jetzt": jetzt.isoformat(),
                         "version": version, "geraet": geraet_, "db": fall["db"], "dateien": fall["dateien"],
                         "bericht": text})

    ergebnis = {"quelle": "fisi_diagnose.py (v0.63.2)", "umgebung": UMGEBUNG,
                "angaben": ["Linux", "6.8.0-45-generic", "x86_64"], "python": "3.12.0",
                "groessen": groessen, "dbgroesse": dbgroesse, "geraet": geraet, "namen": namen,
                "tabellen": {t: list(s) for t, s in spalten.items()}, "berichte": berichte}
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(ergebnis, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    print("G38: %d Groessen, %d Berichte -> %s" % (len(groessen), len(berichte), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
