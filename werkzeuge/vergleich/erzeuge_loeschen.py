#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G40 (Phase 5, Paket BE4 "Loeschen"): "Alle Lerndaten
loeschen" und "Historie loeschen" (fisi_core.DBManager.reset_all, clear_history,
_marker_now) und den Hinweis nach einer Loeschung mit falscher Geraeteuhr
(fisi_sync.clock_fix_text), gerechnet vom echten Python-Code der Python-Linie
(v0.63.2). Nur synthetische Testdaten (Auflage A3).

Aufbau:
  marken     - je gespeicherter Messung der Uhrabweichung (sync_meta "uhr_abweichung",
               wie G21 "stempel"): [Messung, Marke reset_at, last_clock_fix, clock_fix_text]
  loeschen   - Datenbank aus G20 (quelle_db): Stand nach clear_history und nach reset_all
               (Zeilen aller Tabellen mit id, sync_meta, sqlite_sequence)
  uhrtexte   - clock_fix_text(Sekunden) fuer eine Reihe von Werten
  texte      - Beschriftungen und Rueckfragen von PC und Handy (fisi_optionen, fisi_lernen,
               app_gui; jeder Text muss genau so im Python-Quelltext stehen)

Uhr: device_now ist fest 2026-10-20 10:00:00 UTC (Europe/Berlin, TZ und time.tzset).

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_loeschen.py <pfad_fisi_lernplattform> tests/Vergleich/uebernahme.json tests/Vergleich/loeschen.json
"""
import ast
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
JETZT_UTC = "2026-10-20T10:00:00+00:00"
LAUF = 1000.0
START = "start-test"
PROZESS = "prozess-test"


def quelltext_texte(pfad):
    """Alle Textkonstanten einer Python-Datei (aneinandergehaengte Teile schon verbunden)."""
    with open(pfad, encoding="utf-8") as handle:
        baum = ast.parse(handle.read())
    return {k.value for k in ast.walk(baum) if isinstance(k, ast.Constant) and isinstance(k.value, str)}


def main():
    if len(sys.argv) != 4:
        print(__doc__)
        return 2
    repo, g20_pfad, ziel = sys.argv[1], sys.argv[2], sys.argv[3]
    os.environ["TZ"] = ZONE
    time.tzset()
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    atexit.register(shutil.rmtree, tmp, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_lernen as lernen
    import fisi_optionen as fo
    import fisi_sync as sync

    jetzt = datetime.datetime.fromisoformat(JETZT_UTC)
    core.device_now = sync.device_now = lambda: jetzt.astimezone()
    core._boot_clock = lambda: LAUF
    core._boot_id = lambda: START
    core.PROCESS_TOKEN = PROZESS

    def messung(s, gemessen, lauf, start=START, prozess="frueher"):
        return json.dumps({"s": s, "gemessen": gemessen, "lauf": lauf, "start": start, "prozess": prozess})

    # -- Marken mit gemessener Uhrabweichung (_marker_now) --------------------------
    marken = []
    jetzt_iso = "2026-10-20T12:00:00+02:00"
    for wert in [None, "kein json", messung(0.0, jetzt_iso, LAUF),
                 messung(200.0, jetzt_iso, LAUF), messung(-200.0, jetzt_iso, LAUF),
                 messung(301.0, jetzt_iso, LAUF), messung(-301.0, jetzt_iso, LAUF),
                 messung(300.5, jetzt_iso, LAUF), messung(-300.5, jetzt_iso, LAUF),
                 messung(3600.0, jetzt_iso, LAUF), messung(-7200.0, jetzt_iso, LAUF),
                 messung(5430.3, jetzt_iso, LAUF), messung(-9090.7, jetzt_iso, LAUF),
                 messung(90000.0, jetzt_iso, LAUF), messung(-200000.0, jetzt_iso, LAUF),
                 # ueber die Zeitumstellung am 25.10. hinweg: Marke in MEZ (+01:00)
                 messung(-500000.0, jetzt_iso, LAUF),
                 # Messung vor 2 h, Uhr seither nicht gestellt (Sprung 0): Messung gilt
                 messung(3600.0, "2026-10-20T10:00:00+02:00", LAUF - 7200.0),
                 # anderer Systemstart, Sprung nicht feststellbar: Vorlauf gilt, Nachlauf nicht
                 messung(3600.0, "2026-10-20T11:00:00+02:00", LAUF - 600.0, start="anderer"),
                 messung(-3600.0, "2026-10-20T11:00:00+02:00", LAUF - 600.0, start="anderer"),
                 # Uhr seit der Messung gestellt (Sprung 3600 - 600 = 3000 s zurueck): kleine Abweichung
                 messung(3600.0, "2026-10-20T11:00:00+02:00", LAUF - 600.0, start=START, prozess=PROZESS),
                 # aelter als 24 h und in der Zukunft: gilt nicht
                 messung(3600.0, "2026-10-19T11:00:00+02:00", LAUF - 90000.0),
                 messung(3600.0, "2026-10-20T12:30:00+02:00", LAUF)]:
        pfad = os.path.join(tmp, "marke_%d.db" % len(marken))
        db = core.DBManager(pfad)
        if wert is not None:
            db.set_meta(core.CLOCK_KEY, wert)
        assert db.reset_all()
        marken.append([wert, db.get_meta("reset_at"), db.last_clock_fix, sync.clock_fix_text(db.last_clock_fix)])

    # -- Loeschen auf der Datenbank aus G20 -------------------------------------------
    with open(g20_pfad, encoding="utf-8") as handle:
        g20 = json.load(handle)
    quelle = g20["quelle_db"]

    def baue(pfad):
        core.DBManager(pfad)
        conn = sqlite3.connect(pfad)
        conn.execute("DELETE FROM sync_meta")
        for tabelle, inhalt in quelle["tabellen"].items():
            spalten = inhalt["spalten"]
            for zeile in inhalt["zeilen"]:
                conn.execute("INSERT INTO %s (%s) VALUES (%s)" % (tabelle, ", ".join(spalten), ", ".join("?" * len(spalten))), zeile)
        for key, value in quelle["sync_meta"]:
            conn.execute("INSERT INTO sync_meta (key, value) VALUES (?, ?)", (key, value))
        conn.execute("DELETE FROM sqlite_sequence")
        for name, seq in quelle["sqlite_sequence"]:
            conn.execute("INSERT INTO sqlite_sequence (name, seq) VALUES (?, ?)", (name, seq))
        conn.commit()
        conn.close()
        db = core.DBManager.__new__(core.DBManager)     # ohne init_db: Datei bleibt wie gebaut
        db.db_path = pfad
        db.error_handler = None
        db.last_clock_fix = 0
        db.read_only_reason = None
        db.error_counts = {}
        db.refused_writes = 0
        return db

    def stand_von(pfad):
        conn = sqlite3.connect(pfad)
        try:
            tabellen = {}
            for tabelle in core.SYNC_TABLES:
                cur = conn.execute("SELECT * FROM %s ORDER BY id" % tabelle)
                tabellen[tabelle] = {"spalten": [d[0] for d in cur.description],
                                     "zeilen": [list(r) for r in cur.fetchall()]}
            return {"tabellen": tabellen,
                    "sync_meta": [list(r) for r in conn.execute("SELECT key, value FROM sync_meta ORDER BY key")],
                    "sqlite_sequence": [list(r) for r in conn.execute(
                        "SELECT name, seq FROM sqlite_sequence ORDER BY name")]}
        finally:
            conn.close()

    loeschen = {}
    for art in ("historie", "alles"):
        db = baue(os.path.join(tmp, "loeschen_%s.db" % art))
        assert db.clear_history() if art == "historie" else db.reset_all()
        loeschen[art] = stand_von(db.db_path)

    # -- clock_fix_text ---------------------------------------------------------------
    uhrtexte = [[s, sync.clock_fix_text(s)] for s in
                [0, 0.0, 1, 29.9, 30, 30.5, 31, 89, 90, 150, 3599, 3600, 5399, 5400, 9000, 86399, 86400,
                 129599, 129600, 216000, 216000.1, 1e7, -0.4, -45, -3600, -7200, -172800, -301.0, 301.0]]

    # -- Texte (PC = Handy; Rueckfragen und Rueckmeldungen wie am PC) ----------------
    app = quelltext_texte(os.path.join(repo, "app_gui.py"))
    handy = quelltext_texte(os.path.join(repo, "mobile", "src", "main.py"))
    texte = {
        "bereich_titel": lernen.DELETE_TITLE,
        "bereich_untertitel": lernen.DELETE_SUBTITLE,
        "lerndaten": "LERNDATEN",
        "lerndaten_text": fo.RESET_ALL_TEXT,
        "lerndaten_knopf": "Alle Lerndaten löschen",
        "lerndaten_frage_titel": "Alles zurücksetzen",
        "lerndaten_frage": "Wirklich ALLE Lerndaten unwiderruflich löschen?",
        "lerndaten_erfolg_titel": "Zurückgesetzt",
        "lerndaten_erfolg": "Alle Lerndaten wurden gelöscht.",
        "historie": lernen.HISTORY_LABEL.upper(),
        "historie_text": lernen.HISTORY_TEXT,
        "historie_knopf": lernen.HISTORY_BUTTON,
        "historie_frage_titel": "Historie löschen",
        "historie_frage": "Wirklich alle gespeicherten Testergebnisse und Prüfungen löschen?\n\n"
                          "Die Lern-Ereignisse für das Dashboard bleiben erhalten.",
        "historie_erfolg_titel": "Gelöscht",
        "historie_erfolg": fo.DELETE_HISTORY_DONE,
        "nichts_geloescht": fo.DELETE_NOTHING,
        "spielstand": "SPIELSTAND",
        "spielstand_knopf": "Spielstand zurücksetzen",
        "bestenliste": "BESTENLISTE",
        "bestenliste_knopf": "Bestenliste löschen",
        "datenbank_titel": "Speicherort der Lernfortschritte:",
        "datenbank_hinweis": "Der Pfad lässt sich über die Umgebungsvariable FISI_DB_PATH überschreiben, "
                             "zum Beispiel um die Datenbank auf einem Netzlaufwerk abzulegen.",
    }
    for name in ("lerndaten", "lerndaten_knopf", "lerndaten_frage_titel", "lerndaten_frage", "lerndaten_erfolg_titel",
                 "lerndaten_erfolg", "historie_frage_titel", "historie_frage", "historie_erfolg_titel", "spielstand",
                 "spielstand_knopf", "bestenliste", "bestenliste_knopf", "datenbank_titel", "datenbank_hinweis"):
        assert texte[name] in app, name + " steht nicht in app_gui.py"
    for name in ("lerndaten_knopf", "lerndaten_frage_titel", "lerndaten_frage", "lerndaten_erfolg", "historie_frage_titel"):
        assert texte[name] in handy, name + " steht nicht in mobile/src/main.py"

    daten = {
        "quelle": "fisi_core.DBManager.reset_all, clear_history, _marker_now, clock_deviation, clock_jump_unknown; "
                  "fisi_sync.clock_fix_text; fisi_optionen, fisi_lernen, app_gui (Python v0.63.2)",
        "python": sys.version.split()[0],
        "zone": ZONE,
        "jetzt_utc": JETZT_UTC,
        "lauf": LAUF,
        "start": START,
        "prozess": PROZESS,
        "ereignistabellen": list(core.EVENT_TABLES),
        "historientabellen": list(core.HISTORY_TABLES),
        "marken": marken,
        "loeschen": loeschen,
        "uhrtexte": uhrtexte,
        "texte": texte,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    print("G40: %d Marken, %d Uhrtexte -> %s" % (len(marken), len(uhrtexte), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
