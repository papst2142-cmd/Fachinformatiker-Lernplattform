#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G21 (Phase 2, Welle 4: L9 Ruecksicherung, L10 Abgleich),
gerechnet vom echten Python-Code der Python-Linie (v0.63.2). Nur synthetische
Testdaten (Auflage A3).

Inhalt:
  zahlen       - repr/json.dumps von Kommazahlen (Bitmuster als ganze Zahl, Text wie Python)
  sicherung    - ein Lernstand (stand), create_backup daraus als JSON-Text (vor gzip),
                 export_local als Text und die Pruefsumme
  faelle       - je Fall ein Versuch des Abgleichs wie fisi_sync.sync (ohne Netz):
                 _note_mixed, _correct_own_entries, clock_measurement,
                 _clamp_future_markers, merge_into_local(check_future=False), export_local,
                 Vergleich mit der Datei (gleich, sent). Eingabe: Stand vorher, Datei der
                 anderen Geraete (Text, "fehlt" = noch keine), Uhrabweichung (null = kein
                 Date-Kopf). Ergebnis: Stand nachher, export_local als Text, received,
                 gone, invalid, clamped, mixed, gleich, sent.

Uhr: device_now ist fest 2026-10-20 12:00:00 Ortszeit (Europe/Berlin), Laufzeit-Uhr 1000.0,
Systemstart "start-test", Prozess "prozess-test". Die Uhrzeit von GitHub ist device_now
minus Abweichung (wie _SERVER_CLOCK nach einer Antwort).

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_abgleich.py <pfad_fisi_lernplattform> tests/Vergleich/abgleich.json
"""
import copy
import datetime
import gzip
import json
import os
import sqlite3
import struct
import sys
import tempfile
import time

ZONE = "Europe/Berlin"
JETZT_UTC = "2026-10-20T10:00:00+00:00"
LAUF = 1000.0
START = "start-test"
PROZESS = "prozess-test"


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    os.environ["TZ"] = ZONE
    time.tzset()
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_sicherung as sich
    import fisi_sync as sync

    jetzt = datetime.datetime.fromisoformat(JETZT_UTC)
    core.device_now = sync.device_now = lambda: jetzt.astimezone()
    core._boot_clock = lambda: LAUF
    core._boot_id = lambda: START
    core.PROCESS_TOKEN = PROZESS
    sich._now = lambda: datetime.datetime(2026, 10, 20, 12, 0, 5)
    sich.load_settings = lambda: {"ziel_anzahl": 30, "erinnerung_an": False, "sync_token": "nie in der Sicherung"}
    sync._log_missing_date = lambda: None

    # -- Zahlen ------------------------------------------------------------------
    werte = [0.0, -0.0, 0.1, 0.5, 1.0, 2.0, 100.0, 1234.5, 1 / 3, 2 / 3, 1e15, 1e16, 1e17, 1.5e16, 9007199254740993.0,
             123456789012345678.0, 0.0001, 0.00012345, 0.00001, 1.5e-5, 5e-324, 2.2250738585072014e-308,
             1.7976931348623157e308, 61.5, 80.25, 99.99999999999999, 3.14159, -273.15, 1e-7, 12345678.9,
             float("inf"), float("-inf"), float("nan")]
    zahlen = [[struct.unpack("<q", struct.pack("<d", w))[0], json.dumps(w), repr(w)] for w in werte]

    zaehler = [0]
    stempel = ["", 0]

    def uid():
        zaehler[0] += 1
        return "u-%04d" % zaehler[0]

    core.DBManager._uid = staticmethod(uid)

    def schreibe(db, zeit, abstand, aktion):
        stempel[0], stempel[1] = zeit, abstand
        db._stamp = lambda: (stempel[0], stempel[1])
        aktion()

    def roh(pfad, sql, werte=()):
        conn = sqlite3.connect(pfad)
        conn.execute(sql, werte)
        conn.commit()
        conn.close()

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

    def oeffnen(pfad):
        db = core.DBManager.__new__(core.DBManager)     # ohne init_db: Datei bleibt wie gebaut
        db.db_path = pfad
        db.error_handler = None
        db.last_clock_fix = 0
        db.read_only_reason = None
        db.error_counts = {}
        db.refused_writes = 0
        return db

    karten = core.KARTEIKARTEN[30:40]
    alt_karte = next(iter(core.QUESTION_RENAMES["card_events"]))

    # -- Lokaler Lernstand: eigene Eintraege der letzten Tage, einige in den letzten 24 h ------------
    def baue_lokal(name):
        zaehler[0] = 0
        pfad = os.path.join(tmp, name + ".db")
        db = core.DBManager(pfad)
        for nummer, (zeit, abstand) in enumerate([
                ("2026-10-17 09:00:00", 120), ("2026-10-19 08:00:00", 120), ("2026-10-19 11:30:00", 120),
                ("2026-10-19 13:00:00", 120), ("2026-10-20 09:00:00", 120), ("2026-10-20 10:59:00", 120),
                ("2026-10-20 11:30:00", 60), ("2026-10-20 11:55:00", 120)]):
            karte = karten[nummer]
            schreibe(db, zeit, abstand, lambda: db.log_card(karte["cat"], karte["q"], "lernen", nummer % 2 == 0))
        schreibe(db, "2026-10-20 08:00:00", 120, lambda: db.log_quiz_answer("Netzwerk", "Eigene Quizfrage", True))
        schreibe(db, "2026-10-20 08:30:00", 120, lambda: db.log_card("Netzwerk", alt_karte, "lernen", True))
        schreibe(db, "2026-10-18 18:00:00", 120, lambda: db.save_test_result(15, 20, 75.0, core.ihk_note(75.0), 900))
        roh(pfad, "INSERT INTO spiel_ereignisse (timestamp, utc_offset, typ, daten, geraet, lauf, uid)"
                  " VALUES (?, ?, ?, ?, ?, ?, ?)", ("2026-10-19 12:00:00", 120, "auftrag", "{}", "PC", "l9", "lok-spiel"))
        roh(pfad, "INSERT INTO abschlussprojekt (timestamp, projekt, feld, wert, uid) VALUES (?, ?, ?, ?, ?)",
            ("2026-10-18 10:00:00", "p1", "titel", "Lokaler Titel", "lok-proj"))
        roh(pfad, "INSERT INTO card_events (timestamp, category, question, mode, correct, uid)"
                  " VALUES (?, ?, ?, ?, ?, ?)", ("2026-10-20 07:00:00", "Alt", "Ohne Abstand", "lernen", 1, "lok-ohne"))
        roh(pfad, "INSERT OR REPLACE INTO sync_meta (key, value) VALUES (?, ?)", ("spiel_reset_at", "2026-10-01 00:00:00+02:00"))
        return pfad

    # -- Andere Geraete: eigene Datenbank, daraus export_local -----------------------------------
    andere_pfad = os.path.join(tmp, "andere.db")
    zaehler[0] = 100
    andere = core.DBManager(andere_pfad)
    for nummer, (zeit, abstand) in enumerate([("2026-10-18 07:00:00", 120), ("2026-10-19 20:00:00", 120),
                                               ("2026-10-20 11:00:00", 120)]):
        karte = karten[nummer]
        schreibe(andere, zeit, abstand, lambda: andere.log_card(karte["cat"], "Andere " + karte["q"], "mc", True))
    schreibe(andere, "2026-10-19 21:00:00", 120, lambda: andere.log_quiz_answer("Netzwerk", "Andere Quizfrage", False))
    roh(andere_pfad, "INSERT INTO spiel_plaetze (timestamp, platz, lauf, aktion, daten, geraet, uid)"
                     " VALUES (?, ?, ?, ?, ?, ?, ?)", ("2026-10-19 14:00:00.000001", 1, "l9", "geloescht", "{}", "Handy", "and-platz"))
    roh(andere_pfad, "INSERT INTO abschlussprojekt (timestamp, projekt, feld, wert, uid) VALUES (?, ?, ?, ?, ?)",
        ("2026-10-19 10:00:00", "p1", "titel", "Neuerer Titel", "and-proj"))
    datei_andere = sync.export_local(oeffnen(andere_pfad))

    def datei(**aenderungen):
        inhalt = copy.deepcopy(datei_andere)
        for schluessel, wert in aenderungen.items():
            if wert is None:
                inhalt.pop(schluessel, None)
            elif schluessel == "markers":
                inhalt["markers"].update(wert)
            else:
                inhalt[schluessel] = wert
        return inhalt

    # -- Sicherung (L9) ---------------------------------------------------------------------------
    lokal_pfad = baue_lokal("sicherung")
    roh(lokal_pfad, "INSERT OR REPLACE INTO sync_meta (key, value) VALUES (?, ?)",
        ("aufgehoben:reset_at", json.dumps(["2026-10-06 00:00:00+02:00", ""])))
    roh(lokal_pfad, "INSERT OR REPLACE INTO sync_meta (key, value) VALUES (?, ?)",
        ("gekappt:reset_at", json.dumps(["2026-10-25 00:00:00+02:00", "2026-10-20 12:00:00+02:00"])))
    roh(lokal_pfad, "INSERT OR REPLACE INTO sync_meta (key, value) VALUES (?, ?)", ("mischbetrieb", "2026-10-19T10:00:00+00:00"))
    roh(lokal_pfad, "INSERT INTO pruefungen (timestamp, utc_offset, art, punkte, note, dauer, daten, uid)"
                    " VALUES (?, ?, ?, ?, ?, ?, ?, ?)", ("2026-10-02 09:00:00", 120, "netzwerke", 61.5, "3", 5400, "{\"ä\": \"\\u00fc\"}", "lok-pr"))
    sicherung_db = oeffnen(lokal_pfad)
    huelle = json.loads(gzip.decompress(sich.create_backup(sicherung_db, "A 0.5.0", "PC Test")).decode("utf-8"))
    sicherung = {
        "stand": stand_von(lokal_pfad),
        "app_version": "A 0.5.0", "geraet": "PC Test", "erstellt": "2026-10-20T12:00:05",
        "einstellungen_eingabe": {"ziel_anzahl": 30, "erinnerung_an": False, "sync_token": "nie in der Sicherung"},
        "text": json.dumps(huelle, ensure_ascii=False, separators=(",", ":")),
        "export": json.dumps(sync.export_local(sicherung_db), ensure_ascii=False, separators=(",", ":")),
        "pruefsumme": huelle["pruefsumme"],
    }

    # -- Faelle (ein Versuch von sync) -------------------------------------------------------------
    def versuch(db, remote, deviation):
        sync._SERVER_CLOCK.update(server=None, device=None)
        if deviation is not None:
            geraet = jetzt.astimezone(datetime.timezone.utc)
            sync._SERVER_CLOCK.update(server=geraet - datetime.timedelta(seconds=deviation), device=geraet)
        stats = sync.MergeStats()
        raw_markers = dict((remote or {}).get("markers") or {}) if remote else {}
        deviation = sync._clock_deviation()
        mixed = sync._note_mixed(db, remote, deviation)
        clamped = False
        if deviation is not None:
            sync._correct_own_entries(db, remote, deviation)
            db.set_meta(core.CLOCK_KEY, core.clock_measurement(deviation))
            clamped = sync._clamp_future_markers(db, remote, deviation, mixed) or clamped
        received = 0
        if remote:
            received = sync.merge_into_local(db, remote, check_future=False, stats=stats, mixed=mixed)
        local = sync.export_local(db)
        remote_uids = sync._uids(remote) if remote else {}
        local_uids = sync._uids(local)
        gleich = bool(remote and local_uids == remote_uids and local["markers"] == raw_markers)
        sent = sum(len(uids - remote_uids.get(table, set())) for table, uids in local_uids.items())
        return {"export": json.dumps(local, ensure_ascii=False, separators=(",", ":")),
                "received": received, "gone": stats.removed, "invalid": stats.invalid,
                "clamped": clamped, "mixed": mixed, "gleich": gleich, "sent": sent}

    faelle = []

    def fall(name, remote, deviation, meta=(), vorbereiten=None):
        pfad = baue_lokal("fall_" + name)
        for schluessel, wert in meta:
            roh(pfad, "INSERT OR REPLACE INTO sync_meta (key, value) VALUES (?, ?)", (schluessel, wert))
        if vorbereiten:
            vorbereiten(pfad)
        vorher = stand_von(pfad)
        text = None if remote is None else json.dumps(remote, ensure_ascii=False, separators=(",", ":"))
        ergebnis = versuch(oeffnen(pfad), None if remote is None else json.loads(text), deviation)
        ergebnis["nachher"] = stand_von(pfad)
        faelle.append({"name": name, "vorher": vorher, "datei": text, "abweichung": deviation, **ergebnis})

    def messung(s, gemessen, lauf, start=START, prozess="frueher"):
        return json.dumps({"s": s, "gemessen": gemessen, "lauf": lauf, "start": start, "prozess": prozess})

    fall("datei_fehlt", None, 0.0)
    fall("leere_datei", {}, 0.0)
    fall("andere_geraete", datei(), 0.0)
    fall("marke_verlauf", datei(markers={"history_cleared_at": "2026-10-19 12:00:00+02:00"}), 0.0)
    fall("marke_ohne_zone", datei(markers={"reset_at": "2026-10-19 12:00:00"}), 0.0)
    fall("marke_ungueltig", datei(markers={"reset_at": "gestern"}), 0.0)
    fall("uhr_vor_ohne_messung", datei(), 3600.0)
    fall("uhr_nach", datei(), -7200.0)
    fall("uhr_klein", datei(), 120.0)
    fall("uhr_vor_messung_bestaetigt", datei(), 3600.0,
         [(core.CLOCK_KEY, messung(0.0, "2026-10-20T10:00:00+02:00", LAUF - 3600.0))])
    fall("uhr_vor_sprung_unbekannt", datei(), 3600.0,
         [(core.CLOCK_KEY, messung(0.0, "2026-10-20T10:00:00+02:00", LAUF - 3600.0, start="anderer"))])
    fall("uhr_vor_schon_falsch", datei(), 3600.0,
         [(core.CLOCK_KEY, messung(3600.0, "2026-10-20T11:00:00+02:00", LAUF - 3600.0))])
    fall("uhr_vor_nicht_gestellt", datei(), 3600.0,
         [(core.CLOCK_KEY, messung(3500.0, "2026-10-20T10:00:00+02:00", LAUF - 7200.0))])
    fall("zukunftsmarke_lokal", datei(), 0.0, [("history_cleared_at", "2026-10-22 08:00:00+02:00")])
    fall("zukunftsmarke_datei", datei(markers={"reset_at": "2026-10-23 08:00:00+02:00"}), 0.0)
    fall("zukunftsmarke_gleichzeitig",
         datei(markers={"reset_at": "2026-10-23 08:00:00+02:00"},
               gekappt={"reset_at": ["2026-10-23 08:00:00+02:00", "2026-10-20 10:00:00+02:00"]}), 0.0,
         [("reset_at", "2026-10-20 11:00:00+02:00"),
          ("gekappt:reset_at", json.dumps(["2026-10-23 08:00:00+02:00", "2026-10-20 11:00:00+02:00"]))])
    fall("zukunftsmarke_bekannt",
         datei(markers={"reset_at": "2026-10-23 08:00:00+02:00"},
               gekappt={"reset_at": json.dumps(["2026-10-23 08:00:00+02:00", "2026-10-19 09:00:00+02:00"])}), 0.0)
    fall("mischbetrieb_alt", datei(schreiber=None, markers={"reset_at": "2026-10-19 09:00:00"}), 0.0)
    fall("mischbetrieb_alt_zukunft", datei(schreiber=None, markers={"reset_at": "2026-10-23 08:00:00+02:00"}), 0.0)
    fall("mischbetrieb_feld", datei(mischbetrieb="2026-10-19T09:00:00+00:00",
                                    markers={"reset_at": "2026-10-23 08:00:00+02:00"}), 0.0)
    fall("mischbetrieb_lokal_zukunft", datei(), 0.0, [("mischbetrieb", "2026-10-21T10:00:00+00:00")])
    fall("ohne_datum", datei(), None, [("mischbetrieb", "2026-10-21T10:00:00+00:00")])
    fall("ohne_datum_zukunftsmarke", datei(markers={"reset_at": "2026-10-23 08:00:00+02:00"}), None)
    fall("aufhebung", datei(aufgehoben={"spiel_reset_at": ["2026-10-21 00:00:00+02:00", ""]},
                            markers={"spiel_reset_at": "2026-10-21 00:00:00+02:00"}), 600.0)
    # gleich: Datei ist genau der lokale Stand
    pfad_gleich = baue_lokal("gleich_quelle")
    fall("schon_gleich", sync.export_local(oeffnen(pfad_gleich)), 0.0)

    daten = {
        "quelle": "fisi_sync.sync (ein Versuch ohne Netz), export_local, _note_mixed, _correct_own_entries, "
                  "_clamp_future_markers, merge_into_local; fisi_core.clock_measurement; "
                  "fisi_sicherung.create_backup (Python v0.63.2)",
        "python": sys.version.split()[0],
        "zone": ZONE,
        "jetzt_utc": JETZT_UTC,
        "lauf": LAUF, "start": START, "prozess": PROZESS,
        "zahlen": zahlen,
        "sicherung": sicherung,
        "faelle": faelle,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    print("G21: %d Zahlen, %d Faelle -> %s" % (len(zahlen), len(faelle), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
