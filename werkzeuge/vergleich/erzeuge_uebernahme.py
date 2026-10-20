#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G20 (Phase 2, Paket L3 "Uebernahme"): Ergebnis von
"Sicherung zusammenfuehren" (fisi_sicherung.read_backup, merge_backup) und
fisi_sync.merge_into_local(check_future=True), gerechnet vom echten Python-Code
der Python-Linie (v0.63.2). Nur synthetische Testdaten (Auflage A3).

Aufbau:
  quelle     - eine Python-Datenbank mit allen 12 Abgleich-Tabellen, alten
               Fragetexten (umbenennungen.json), Eintraegen mit und ohne utc_offset,
               Loeschmarken mit und ohne Zone, einer Aufhebung ("aufgehoben"),
               geloeschtem Spieldurchgang und aelteren Fassungen im Abschlussprojekt
  ziele      - leer, gefuellt (eigene Eintraege, teils mit gleicher uid, eigene
               Marken, eine ungueltige Marke), mischbetrieb (gefuellt, dazu ein
               Geraet vor 0.62.4 vor einem Tag gesehen: Marken als Text)
  dateien    - Sicherungen als JSON-Text (vor gzip; gzip-Bytes haengen von zlib ab):
               a (create_backup), b (Zukunftsmarke und ungueltige Marke, Pruefsumme
               neu), c (ohne "schreiber" wie vor 0.62.4), Abgleich-Datei d
               (export_local), alte Datenbank e (Zeilen fuer den Nachbau)
  faelle     - Zusammenfuehren je Quelle in je ein Ziel: Zeilen aller Tabellen danach
               (mit id), sync_meta, sqlite_sequence, Zaehlwerte (received von
               merge_into_local, Zahl von merge_backup, kept, removed, invalid,
               future, entfernt = Loeschmarke traf vorhandene Eintraege); dazu
               zweimal dieselbe Datei (zweites Mal keine neuen Zeilen)
  fehler     - read_backup fuer kaputte Dateien: Code der Fehlermeldung

Uhr: device_now ist fest 2026-10-20 12:00:00 Ortszeit (Europe/Berlin, TZ und
time.tzset), Zeitstempel und uid beim Schreiben sind fest.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_uebernahme.py <pfad_fisi_lernplattform> tests/Vergleich/uebernahme.json
"""
import base64
import copy
import datetime
import gzip
import json
import os
import shutil
import sqlite3
import sys
import tempfile
import time

ZONE = "Europe/Berlin"
JETZT_UTC = "2026-10-20T10:00:00+00:00"
SICHERUNG_ERSTELLT = datetime.datetime(2026, 10, 20, 11, 30, 0)


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
    sich._now = lambda: SICHERUNG_ERSTELLT
    sich.load_settings = lambda: {"ziel_anzahl": 25, "serie_an": True}

    stand = ["", 0, "x"]

    def stempel(_self=None):
        return stand[0], stand[1]

    zaehler = [0]

    def uid():
        zaehler[0] += 1
        return "%s-%04d" % (stand[2], zaehler[0])

    core.DBManager._uid = staticmethod(uid)

    def schreibe(db, zeit, abstand, aktion):
        stand[0], stand[1] = zeit, abstand
        db._stamp = stempel
        aktion()

    alt_karte, neu_karte = next(iter(core.QUESTION_RENAMES["card_events"].items()))
    alt_quiz, neu_quiz = list(core.QUESTION_RENAMES["quiz_answers"].items())[1]
    karten = core.KARTEIKARTEN[10:16]
    fragen = core.QUIZ_QUESTIONS[20:26]

    def roh(db, sql, werte):
        conn = sqlite3.connect(db.db_path)
        conn.execute(sql, werte)
        conn.commit()
        conn.close()

    # -- Quelle ------------------------------------------------------------
    stand[2] = "q"
    quelle = core.DBManager(os.path.join(tmp, "quelle.db"))
    q = quelle
    for nummer, (zeit, abstand) in enumerate([("2026-10-01 08:00:00", 120), ("2026-10-05 11:59:59", 120),
                                               ("2026-10-05 12:00:00", 120), ("2026-10-05 13:00:01", 60),
                                               ("2026-10-12 09:30:00", 120), ("2026-10-19 23:59:59", 120)]):
        karte = karten[nummer]
        schreibe(q, zeit, abstand, lambda: q.log_card(karte["cat"], karte["q"], "lernen", nummer % 3 != 1))
        frage = fragen[nummer]
        schreibe(q, zeit, abstand, lambda: q.log_quiz_answer(frage["cat"], frage["q"], nummer % 2 == 0))
    schreibe(q, "2026-10-15 10:00:00", 120, lambda: q.log_card("Netzwerk", alt_karte, "lernen", True))
    schreibe(q, "2026-10-16 10:00:00", 120, lambda: q.log_quiz_answer("Netzwerk", alt_quiz, False))
    # alte Eintraege ohne utc_offset (vor 0.62.4): Vergleich als Text
    roh(q, "INSERT INTO card_events (timestamp, category, question, mode, correct, uid)"
           " VALUES (?, ?, ?, ?, ?, ?)", ("2026-10-05 11:00:00", karten[0]["cat"], karten[0]["q"], "lernen", 1, "q-alt-1"))
    roh(q, "INSERT INTO card_events (timestamp, category, question, mode, correct, uid)"
           " VALUES (?, ?, ?, ?, ?, ?)", ("2026-10-05 12:30:00", karten[1]["cat"], karten[1]["q"], "lernen", 0, "q-alt-2"))
    for zeit, punkte in [("2026-10-02 18:00:00", 12), ("2026-10-04 18:00:00", 15), ("2026-10-18 18:00:00", 19)]:
        schreibe(q, zeit, 120, lambda: q.save_test_result(punkte, 20, punkte * 5.0, core.ihk_note(punkte * 5.0), 600))
    schreibe(q, "2026-10-10 20:00:00", 120, lambda: q.log_scenario(3, "Szenario", "Netzwerk", True))
    schreibe(q, "2026-10-10 20:05:00", 120, lambda: q.log_project(1, "Projekt", "Netzwerk", None))
    schreibe(q, "2026-10-10 20:10:00", 120, lambda: q.log_ap1(2, "AP1", "Grundlagen", False))
    schreibe(q, "2026-10-11 07:00:00", 120, lambda: q.log_trainer("subnetz", 2, True))
    roh(q, "INSERT INTO pruefungen (timestamp, utc_offset, art, punkte, note, dauer, daten, uid)"
           " VALUES (?, ?, ?, ?, ?, ?, ?, ?)", ("2026-10-02 09:00:00", 120, "netzwerke", 61.5, "3", 5400, "{}", "q-pr-1"))
    roh(q, "INSERT INTO pruefungen (timestamp, utc_offset, art, punkte, note, dauer, daten, uid)"
           " VALUES (?, ?, ?, ?, ?, ?, ?, ?)", ("2026-10-09 09:00:00", 120, "wiso", 80.25, "2", 3600, "{\"a\": 1}", "q-pr-2"))
    for zeit, platz, lauf, aktion, daten in [
            ("2026-10-01 10:00:00.000001", 1, "l1", "angelegt", "{\"name\": \"Platz eins\"}"),
            ("2026-10-01 10:00:00.000002", 2, "l2", "angelegt", "{}"),
            ("2026-10-03 10:00:00.500000", 2, "", "umbenannt", "{\"name\": \"Zweiter\"}"),
            ("2026-10-08 10:00:00.000000", 2, "l2", "geloescht", "{}")]:
        roh(q, "INSERT INTO spiel_plaetze (timestamp, platz, lauf, aktion, daten, geraet, uid)"
               " VALUES (?, ?, ?, ?, ?, ?, ?)", (zeit, platz, lauf, aktion, daten, "PC", uid()))
    for zeit, lauf in [("2026-10-01 11:00:00", "l1"), ("2026-10-03 11:00:00", "l1"), ("2026-10-02 11:00:00", "l2"),
                       ("2026-09-30 11:00:00", None), ("2026-10-06 11:00:00", "l1")]:
        roh(q, "INSERT INTO spiel_ereignisse (timestamp, utc_offset, typ, daten, geraet, lauf, uid)"
               " VALUES (?, ?, ?, ?, ?, ?, ?)", (zeit, 120, "auftrag", "{}", "PC", lauf, uid()))
    for zeit in ("2026-09-29 10:00:00", "2026-10-07 10:00:00"):
        roh(q, "INSERT INTO spiel_bestenliste (timestamp, utc_offset, durchgang, art, schluessel, wert, daten, uid)"
               " VALUES (?, ?, ?, ?, ?, ?, ?, ?)", (zeit, 120, "l1", "erfolg", "erster", 1, "{}", uid()))
    for zeit, projekt, feld, wert in [
            ("2026-10-01 10:00:00", "p1", "titel", "Erster Titel"), ("2026-10-02 10:00:00", "p1", "titel", "Zweiter Titel"),
            ("2026-10-02 10:00:00", "p2", "titel", "Weg"), ("2026-10-03 10:00:00", "p2", "_geloescht", "1"),
            ("2026-10-04 10:00:00", "p2", "titel", "Nach dem Loeschen")]:
        roh(q, "INSERT INTO abschlussprojekt (timestamp, projekt, feld, wert, uid) VALUES (?, ?, ?, ?, ?)",
            (zeit, projekt, feld, wert, uid()))
    for key, value in [("reset_at", "2026-10-05 12:00:00+02:00"),
                       ("history_cleared_at", "2026-10-03 08:00:00+02:00"),
                       ("spiel_reset_at", "2026-10-02 00:00:00+02:00"),
                       ("bestenliste_reset_at", "2026-10-01 09:00:00"),
                       ("aufgehoben:reset_at", json.dumps(["2026-10-06 00:00:00+02:00", "2026-10-05 12:00:00+02:00"])),
                       ("gekappt:reset_at", "[]")]:
        roh(q, "INSERT OR REPLACE INTO sync_meta (key, value) VALUES (?, ?)", (key, value))

    # -- Ziele ---------------------------------------------------------------
    def baue_ziel(name, mischbetrieb=False):
        stand[2] = "z"
        zaehler[0] = 0
        db = core.DBManager(os.path.join(tmp, name + ".db"))
        if name == "leer":
            return db
        for nummer, (zeit, abstand) in enumerate([("2026-10-03 08:00:00", 120), ("2026-10-05 10:59:00", 60),
                                                   ("2026-10-14 17:00:00", 120)]):
            karte = karten[nummer + 2]
            schreibe(db, zeit, abstand, lambda: db.log_card(karte["cat"], karte["q"], "mc", True))
        schreibe(db, "2026-10-04 12:00:00", 120, lambda: db.save_test_result(5, 10, 50.0, core.ihk_note(50.0), 100))
        # gleiche uid wie in der Quelle: schon vorhanden
        conn = sqlite3.connect(quelle.db_path)
        zeile = conn.execute("SELECT timestamp, utc_offset, category, question, correct, uid FROM quiz_answers"
                             " ORDER BY id DESC LIMIT 1").fetchone()
        conn.close()
        roh(db, "INSERT INTO quiz_answers (timestamp, utc_offset, category, question, correct, uid)"
                " VALUES (?, ?, ?, ?, ?, ?)", zeile)
        roh(db, "INSERT INTO spiel_ereignisse (timestamp, utc_offset, typ, daten, geraet, lauf, uid)"
                " VALUES (?, ?, ?, ?, ?, ?, ?)", ("2026-10-01 12:00:00", 120, "auftrag", "{}", "Handy", None, "z-spiel-1"))
        roh(db, "INSERT INTO abschlussprojekt (timestamp, projekt, feld, wert, uid) VALUES (?, ?, ?, ?, ?)",
            ("2026-10-05 10:00:00", "p1", "titel", "Eigener Titel", "z-proj-1"))
        # eine geloeschte Zeile: sqlite_sequence liegt hoeher als die groesste id
        roh(db, "INSERT INTO card_events (timestamp, utc_offset, category, question, mode, correct, uid)"
                " VALUES (?, ?, ?, ?, ?, ?, ?)", ("2026-10-15 10:00:00", 120, "x", "x", "mc", 1, "z-weg"))
        roh(db, "DELETE FROM card_events WHERE uid = ?", ("z-weg",))
        for key, value in [("reset_at", "2026-10-04 00:00:00+02:00"),
                           ("spiel_reset_at", "2026-10-06 00:00:00+02:00"),
                           ("history_cleared_at", "kaputt"),
                           ("aufgehoben:history_cleared_at", "[\"2026-10-01 00:00:00+02:00\", \"\"]"),
                           ("spiel_aktiver_lauf", "l1")]:
            roh(db, "INSERT OR REPLACE INTO sync_meta (key, value) VALUES (?, ?)", (key, value))
        if mischbetrieb:
            gesehen = (jetzt - datetime.timedelta(days=1)).astimezone(datetime.timezone.utc).isoformat()
            roh(db, "INSERT OR REPLACE INTO sync_meta (key, value) VALUES (?, ?)", ("mischbetrieb", gesehen))
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

    ziel_namen = ["leer", "gefuellt", "mischbetrieb"]
    for name in ziel_namen:
        baue_ziel(name, name == "mischbetrieb").db_path   # anlegen
    ziele = {name: stand_von(os.path.join(tmp, name + ".db")) for name in ziel_namen}

    # -- Dateien ---------------------------------------------------------------
    sicherung_a = json.loads(gzip.decompress(sich.create_backup(quelle, "0.63.2", "PC")).decode("utf-8"))
    sicherung_b = copy.deepcopy(sicherung_a)
    sicherung_b["daten"]["markers"]["spiel_reset_at"] = "2026-10-21 12:00:00+02:00"   # Zukunft
    sicherung_b["daten"]["markers"]["bestenliste_reset_at"] = "kein Datum"            # ungueltig
    sicherung_b["pruefsumme"] = sich._checksum(sicherung_b["daten"], sicherung_b["einstellungen"])
    sicherung_c = copy.deepcopy(sicherung_a)
    del sicherung_c["daten"]["schreiber"]
    sicherung_c["daten"]["markers"]["reset_at"] = "2026-10-05 12:00:00"
    sicherung_c["pruefsumme"] = sich._checksum(sicherung_c["daten"], sicherung_c["einstellungen"])
    abgleich_d = sync.export_local(quelle)
    dateien = {"a": sicherung_a, "b": sicherung_b, "c": sicherung_c, "d": abgleich_d}
    texte = {name: json.dumps(inhalt, ensure_ascii=False, separators=(",", ":")) for name, inhalt in dateien.items()}

    # -- Faelle ----------------------------------------------------------------
    faelle = []
    # merge_backup gibt nur die Zahl neuer Eintraege; den Rueckgabewert von
    # merge_into_local (received) haelt diese Huelle fest
    erhalten = [None]
    original_merge = sich.merge_into_local

    def merge_mit_rueckgabe(*args, **kwargs):
        erhalten[0] = original_merge(*args, **kwargs)
        return erhalten[0]

    sich.merge_into_local = merge_mit_rueckgabe

    def fall(name, quelle_name, ziel_name, start_pfad=None):
        pfad = os.path.join(tmp, "fall_%s.db" % name)
        shutil.copyfile(start_pfad or os.path.join(tmp, ziel_name + ".db"), pfad)
        db = core.DBManager.__new__(core.DBManager)     # ohne init_db: Datei bleibt wie gebaut
        db.db_path = pfad
        db.error_handler = None
        db.last_clock_fix = 0
        db.read_only_reason = None
        db.error_counts = {}
        db.refused_writes = 0
        vorher = sich._uid_set(db)
        stats = sync.MergeStats()
        if quelle_name in ("a", "b", "c"):
            backup = sich.read_backup(gzip.compress(texte[quelle_name].encode("utf-8")))
            anzahl = sich.merge_backup(db, backup)
            stats = sync.LAST_MERGE
            received = erhalten[0]
        else:
            daten = abgleich_d if quelle_name == "d" else sync.export_local(quelle)
            received = sync.merge_into_local(db, json.loads(json.dumps(daten)), stats=stats)
            anzahl = sich._entry_count(sich._uid_set(db) - vorher)
        faelle.append({
            "name": name, "quelle": quelle_name, "ziel": ziel_name,
            "start": None if start_pfad is None else name.replace("_zweimal", ""),
            "neu": anzahl, "uebernommen": received,
            "nicht_uebernommen": len(stats.gone - vorher), "entfernt_durch_marke": len(stats.gone & vorher),
            "ungueltig": stats.invalid, "zukunft": stats.future,
            "nachher": stand_von(pfad)})
        return pfad

    pfad_a = fall("a_gefuellt", "a", "gefuellt")
    fall("a_gefuellt_zweimal", "a", "gefuellt", pfad_a)
    fall("a_leer", "a", "leer")
    fall("a_mischbetrieb", "a", "mischbetrieb")
    fall("b_gefuellt", "b", "gefuellt")
    fall("c_gefuellt", "c", "gefuellt")
    pfad_d = fall("d_gefuellt", "d", "gefuellt")
    fall("d_gefuellt_zweimal", "d", "gefuellt", pfad_d)
    fall("d_leer", "d", "leer")
    fall("e_gefuellt", "e", "gefuellt")
    fall("e_leer", "e", "leer")

    # -- Fehler (read_backup) ------------------------------------------------
    codes = {sich.MSG_NOT_BACKUP: "KeineSicherung", sich.MSG_DAMAGED: "PruefsummeFalsch",
             sich.MSG_BROKEN: "InhaltKaputt"}

    def code(text):
        if text in codes:
            return codes[text]
        if text.startswith(sich.MSG_NEWER.split("%s")[0]):
            return "ZuNeu"
        raise SystemExit("unbekannte Meldung: " + text)

    def neu_pruefsumme(inhalt):
        inhalt["pruefsumme"] = sich._checksum(inhalt.get("daten"), inhalt.get("einstellungen"))
        return inhalt

    kaputt = []

    def kaputt_fall(name, inhalt=None, text=None, roh=None, kuerzen=None):
        if roh is None:
            if text is None:
                text = json.dumps(inhalt, ensure_ascii=False, separators=(",", ":"))
            daten = gzip.compress(text.encode("utf-8"))
        else:
            daten = roh
        if kuerzen:
            daten = daten[:len(daten) // 2]
        try:
            sich.read_backup(daten)
            ergebnis = "ok"
        except sich.BackupError as fehler:
            ergebnis = code(str(fehler))
        kaputt.append({"name": name, "text": text if roh is None else None,
                       "roh": base64.b64encode(roh).decode("ascii") if roh is not None else None,
                       "kuerzen": bool(kuerzen), "erwartet": ergebnis})

    a = sicherung_a
    kaputt_fall("gut", a)
    kaputt_fall("kein_gzip", roh=b"hallo, keine Sicherung")
    kaputt_fall("abgeschnitten", a, kuerzen=True)
    kaputt_fall("kein_json", text="{kein json")
    kaputt_fall("bom", text="﻿" + json.dumps(a, ensure_ascii=False))
    kaputt_fall("liste", text="[1, 2]")
    kaputt_fall("falsche_art", dict(a, art="Andere-Sicherung"))
    kaputt_fall("formatversion_zu_neu", neu_pruefsumme(dict(copy.deepcopy(a), formatversion=2)))
    b = copy.deepcopy(a)
    b["daten"]["format"] = 3
    kaputt_fall("austauschformat_zu_neu", b)
    kaputt_fall("formatversion_text", neu_pruefsumme(dict(copy.deepcopy(a), formatversion="1")))
    kaputt_fall("formatversion_kommazahl", neu_pruefsumme(dict(copy.deepcopy(a), formatversion=1.9)))
    kaputt_fall("formatversion_null", neu_pruefsumme(dict(copy.deepcopy(a), formatversion=None)))
    kaputt_fall("formatversion_wort", neu_pruefsumme(dict(copy.deepcopy(a), formatversion="eins")))
    kaputt_fall("formatversion_fehlt", neu_pruefsumme({k: v for k, v in a.items() if k != "formatversion"}))
    kaputt_fall("pruefsumme_falsch", dict(a, pruefsumme="0" * 64))
    kaputt_fall("pruefsumme_zahl", dict(a, pruefsumme=5))
    c = copy.deepcopy(a)
    c["daten"]["tables"]["card_events"][0][3] = "veraendert"
    kaputt_fall("inhalt_veraendert", c)
    c = copy.deepcopy(a)
    c["daten"]["tables"] = []
    kaputt_fall("tabellen_liste", neu_pruefsumme(c))
    kaputt_fall("einstellungen_liste", neu_pruefsumme(dict(copy.deepcopy(a), einstellungen=[])))
    c = copy.deepcopy(a)
    c["daten"]["tables"]["card_events"][0][3] = {"x": 1}
    kaputt_fall("zelle_objekt", neu_pruefsumme(c))
    c = copy.deepcopy(a)
    c["daten"]["tables"]["card_events"][0] = "keine Zeile"
    kaputt_fall("zeile_text", neu_pruefsumme(c))
    c = copy.deepcopy(a)
    c["daten"]["tables"]["spiel_plaetze"][0][2] = "zwei"
    kaputt_fall("platz_wort", neu_pruefsumme(c))
    c = copy.deepcopy(a)
    c["daten"]["tables"]["spiel_plaetze"][0][2] = " 3 "
    kaputt_fall("platz_text_zahl", neu_pruefsumme(c))
    c = copy.deepcopy(a)
    c["daten"]["tables"]["spiel_plaetze"][0][2] = 2.7
    kaputt_fall("platz_kommazahl", neu_pruefsumme(c))
    c = copy.deepcopy(a)
    c["daten"]["markers"]["reset_at"] = 5
    kaputt_fall("marke_zahl", neu_pruefsumme(c))
    c = copy.deepcopy(a)
    c["daten"]["columns"]["card_events"] = "timestamp"
    kaputt_fall("spalten_text", neu_pruefsumme(c))
    c = copy.deepcopy(a)
    c["daten"]["tables"]["fremde_tabelle"] = "egal"
    kaputt_fall("fremde_tabelle", neu_pruefsumme(c))

    daten = {
        "quelle": "fisi_sicherung.create_backup, read_backup, merge_backup, _uid_set, _entry_count; "
                  "fisi_sync.export_local, merge_into_local, MergeStats (Python v0.63.2)",
        "python": sys.version.split()[0],
        "zone": ZONE,
        "jetzt_utc": JETZT_UTC,
        "umbenennungen": {"karte": [alt_karte, neu_karte], "quiz": [alt_quiz, neu_quiz]},
        "quelle_db": stand_von(quelle.db_path),
        "ziele": ziele,
        "dateien": texte,
        "austausch_e": sync.export_local(quelle),
        "faelle": faelle,
        "fehler": kaputt,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    print("G20: %d Faelle, %d Fehlerfaelle -> %s" % (len(faelle), len(kaputt), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
