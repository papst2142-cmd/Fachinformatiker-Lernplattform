#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G37 (Phase 5, Paket BE2/BE3 "Sicherung einspielen, Alles
ersetzen"): fisi_sicherung.replace_all, fisi_sync.replace_lifts und die Texte von
"Sicherung einspielen" (preview_text, replace_question, replace_message, merge_message),
gerechnet vom echten Python-Code der Python-Linie (v0.63.2). Nur synthetische Testdaten
(Auflage A3). Eingabe sind die Staende und Sicherungen von G20 (uebernahme.json).

Aufbau:
  ziele        - Staende von G20 (leer, gefuellt, mischbetrieb) und gefuellt_aufhebung
                 (gefuellt mit einer frueheren Aufhebung von spiel_reset_at)
  dateien      - Sicherungen als JSON-Text (vor gzip): a, b, c aus G20, d (a ohne Marken),
                 e (a mit ungewoehnlichem Tagesziel)
  einstellungen_vorher - einstellungen.json vor jedem Ersetzen (mit Zugangsschluessel)
  faelle       - replace_all je Sicherung in je ein Ziel: Stand danach (Zeilen mit id,
                 sync_meta, sqlite_sequence), einstellungen.json danach, Name und Inhalt der
                 Sicherheitskopie, replace_message; dazu zweimal in derselben Minute
  aufhebungen  - replace_lifts(alte Marken, alte Aufhebungen, neue Marken) -> Ergebnis
  zusammenfuehren - merge_message nach merge_backup fuer die Faelle a/b/c von G20
  texte        - Konstanten und Texte (fisi_sicherung; WAIT_* aus app_gui, Quelltext)

Uhr: _now ist fest 2026-10-20 12:00:00 Ortszeit (Europe/Berlin), device_now 10:00 UTC.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_sicherung_ersetzen.py <pfad_fisi_lernplattform> tests/Vergleich/uebernahme.json tests/Vergleich/sicherung_ersetzen.json
"""
import ast
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
JETZT_ORT = datetime.datetime(2026, 10, 20, 12, 0, 0)
APP_VERSION = "A 0.10.0"
GERAET = "PC"
EINSTELLUNGEN_VORHER = {"auto_check": True, "sync_token": "geheim-123456", "sync_repo": "nico/lernstand",
                        "grundfarbe": "blau", "ziel_anzahl": 10, "erinnerung_zeit": "07:30", "serie_an": False}


def wait_texte(pfad):
    """WAIT_BACKUP_TEXT und WAIT_REPLACE_TEXT aus dem Quelltext von app_gui (ohne Tk zu laden)."""
    with open(pfad, encoding="utf-8") as handle:
        baum = ast.parse(handle.read())
    werte = {}
    for knoten in baum.body:
        if isinstance(knoten, ast.Assign) and len(knoten.targets) == 1 and isinstance(knoten.targets[0], ast.Name) \
                and knoten.targets[0].id in ("WAIT_BACKUP_TEXT", "WAIT_REPLACE_TEXT"):
            werte[knoten.targets[0].id] = knoten.value.value
    return werte


def main():
    if len(sys.argv) != 4:
        print(__doc__)
        return 2
    repo, g20_pfad, ziel = sys.argv[1], sys.argv[2], sys.argv[3]
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
    sich._now = lambda: JETZT_ORT
    einstellungen_pfad = sich._settings_path()
    assert os.path.dirname(einstellungen_pfad) == tmp

    with open(g20_pfad, encoding="utf-8") as handle:
        g20 = json.load(handle)

    def baue(stand, pfad):
        core.DBManager(pfad)
        conn = sqlite3.connect(pfad)
        conn.execute("DELETE FROM sync_meta")
        for tabelle, inhalt in stand["tabellen"].items():
            spalten = inhalt["spalten"]
            for zeile in inhalt["zeilen"]:
                conn.execute("INSERT INTO %s (%s) VALUES (%s)" % (tabelle, ", ".join(spalten), ", ".join("?" * len(spalten))), zeile)
        for key, value in stand["sync_meta"]:
            conn.execute("INSERT INTO sync_meta (key, value) VALUES (?, ?)", (key, value))
        conn.execute("DELETE FROM sqlite_sequence")
        for name, seq in stand["sqlite_sequence"]:
            conn.execute("INSERT INTO sqlite_sequence (name, seq) VALUES (?, ?)", (name, seq))
        conn.commit()
        conn.close()
        return pfad

    def oeffnen(pfad):
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

    # -- Ziele ---------------------------------------------------------------------
    ziele = dict(g20["ziele"])
    aufhebung = copy.deepcopy(g20["ziele"]["gefuellt"])
    aufhebung["sync_meta"].append(["aufgehoben:spiel_reset_at",
                                   json.dumps(["2026-10-07 00:00:00+02:00", "2026-10-01 00:00:00+02:00"])])
    aufhebung["sync_meta"].sort()
    ziele["gefuellt_aufhebung"] = aufhebung
    for name, stand in ziele.items():
        baue(stand, os.path.join(tmp, "ziel_%s.db" % name))
        assert stand_von(os.path.join(tmp, "ziel_%s.db" % name)) == stand, name

    # -- Dateien -------------------------------------------------------------------
    dateien = {name: json.loads(g20["dateien"][name]) for name in ("a", "b", "c")}
    d = copy.deepcopy(dateien["a"])
    d["daten"]["markers"] = {}
    d["pruefsumme"] = sich._checksum(d["daten"], d["einstellungen"])
    dateien["d"] = d
    e = copy.deepcopy(dateien["a"])
    e["einstellungen"] = {"ziel_an": 0, "ziel_anzahl": 300, "serie_an": "ja", "erinnerung_an": None,
                          "erinnerung_zeit": " 7:5 ", "sync_token": "nie uebernehmen"}
    e["pruefsumme"] = sich._checksum(e["daten"], e["einstellungen"])
    dateien["e"] = e
    texte_dateien = {name: json.dumps(inhalt, ensure_ascii=False, separators=(",", ":")) for name, inhalt in dateien.items()}

    def lesen(name):
        return sich.read_backup(gzip.compress(texte_dateien[name].encode("utf-8")))

    # -- Faelle (replace_all) --------------------------------------------------------
    faelle = []

    def fall(name, datei, ziel_name, start=None):
        ordner = os.path.join(tmp, "fall_" + name)
        os.makedirs(ordner)
        pfad = os.path.join(ordner, "lernstand.db")
        shutil.copyfile(start or os.path.join(tmp, "ziel_%s.db" % ziel_name), pfad)
        if start is None:
            with open(einstellungen_pfad, "w", encoding="utf-8") as handle:
                json.dump(EINSTELLUNGEN_VORHER, handle)
        kopien = os.path.join(ordner, "sicherungen")
        if start is not None:
            shutil.copytree(os.path.join(os.path.dirname(start), "sicherungen"), kopien)
        backup = lesen(datei)
        kopie = sich.replace_all(oeffnen(pfad), backup, kopien, APP_VERSION, GERAET)
        with open(einstellungen_pfad, encoding="utf-8") as handle:
            einstellungen = json.load(handle)
        assert not os.path.exists(einstellungen_pfad + sich.SETTINGS_TEMP_SUFFIX)
        with open(kopie, "rb") as handle:
            kopie_inhalt = json.loads(gzip.decompress(handle.read()).decode("utf-8"))
        faelle.append({"name": name, "datei": datei, "ziel": ziel_name,
                       "start": None if start is None else name.replace("_zweimal", ""),
                       "nachher": stand_von(pfad), "einstellungen": einstellungen,
                       "kopie_name": os.path.basename(kopie), "kopie": kopie_inhalt,
                       "meldung": sich.replace_message(backup, kopie)})
        return pfad

    pfad_a = fall("a_gefuellt", "a", "gefuellt")
    fall("a_gefuellt_zweimal", "a", "gefuellt", pfad_a)
    fall("a_leer", "a", "leer")
    fall("a_mischbetrieb", "a", "mischbetrieb")
    fall("b_gefuellt", "b", "gefuellt")
    fall("c_gefuellt", "c", "gefuellt")
    fall("d_gefuellt", "d", "gefuellt")
    fall("d_aufhebung", "d", "gefuellt_aufhebung")
    fall("e_gefuellt", "e", "gefuellt")
    fall("a_aufhebung", "a", "gefuellt_aufhebung")

    # -- replace_lifts ---------------------------------------------------------------
    m = "2026-10-05 12:00:00+02:00"
    frueher = "2026-10-04 12:00:00+02:00"
    spaeter = "2026-10-06 12:00:00+02:00"
    aufhebungen = []
    for alt, alt_lifts, neu in [
            ({}, {}, {}),
            ({"reset_at": m}, {}, {}),
            ({"reset_at": m}, {}, {"reset_at": m}),
            ({"reset_at": m}, {}, {"reset_at": frueher}),
            ({"reset_at": m}, {}, {"reset_at": spaeter}),
            ({"reset_at": m}, {}, {"reset_at": "kaputt"}),
            ({"reset_at": "kaputt"}, {}, {}),
            ({"reset_at": 5}, {}, {}),
            ({"reset_at": "2026-10-05 12:00:00"}, {}, {"reset_at": "2026-10-04 12:00:00"}),
            ({"reset_at": "2026-10-05 12:00:00"}, {}, {"reset_at": "2026-10-05 11:00:00+02:00"}),
            ({"reset_at": "2026-10-05 10:30:00+00:00"}, {}, {"reset_at": "2026-10-05 12:00:00+02:00"}),
            ({"reset_at": "2026-10-05 10:30:00+00:00"}, {}, {"reset_at": "2026-10-05 12:30:00+02:00"}),
            ({"history_cleared_at": m}, {"history_cleared_at": json.dumps([spaeter, frueher])}, {}),
            ({"history_cleared_at": m}, {"history_cleared_at": json.dumps(["2026-10-01 00:00:00+02:00", ""])},
             {"history_cleared_at": frueher}),
            ({"spiel_reset_at": spaeter}, {"spiel_reset_at": json.dumps([m, frueher])}, {"spiel_reset_at": "2026-10-03 00:00:00+02:00"}),
            ({"spiel_reset_at": spaeter}, {"spiel_reset_at": "kein json"}, {}),
            ({"bestenliste_reset_at": m}, {"bestenliste_reset_at": json.dumps(["kaputt", ""])}, {}),
            ({"reset_at": m, "history_cleared_at": m, "spiel_reset_at": m, "bestenliste_reset_at": m}, {},
             {"reset_at": frueher, "history_cleared_at": spaeter, "bestenliste_reset_at": ""}),
            ({"fremd": m}, {}, {})]:
        aufhebungen.append([alt, alt_lifts, neu, sync.replace_lifts(
            {key: alt.get(key) for key in sync.MARKERS}, {key: alt_lifts.get(key) for key in sync.MARKERS}, neu)])

    # -- merge_message (Zusammenfuehren) fuer die Faelle von G20 ---------------------
    zusammenfuehren = []
    for name, datei, ziel_name in [("a_gefuellt", "a", "gefuellt"), ("a_leer", "a", "leer"), ("b_gefuellt", "b", "gefuellt"),
                                   ("c_gefuellt", "c", "gefuellt"), ("a_mischbetrieb", "a", "mischbetrieb")]:
        pfad = os.path.join(tmp, "merge_%s.db" % name)
        shutil.copyfile(os.path.join(tmp, "ziel_%s.db" % ziel_name), pfad)
        backup = lesen(datei)
        db = oeffnen(pfad)
        anzahl = sich.merge_backup(db, backup)
        texte = [sich.merge_message(anzahl)]
        anzahl = sich.merge_backup(db, backup)
        texte.append(sich.merge_message(anzahl))   # zweimal: nichts mehr neu
        zusammenfuehren.append({"name": name, "datei": datei, "ziel": ziel_name, "meldungen": texte})

    wait = wait_texte(os.path.join(repo, "app_gui.py"))
    texte = {
        "titel": sich.TITLE, "untertitel": sich.SUBTITLE, "hilfe": sich.HELP,
        "knopf_erstellen": sich.BTN_CREATE, "knopf_einspielen": sich.BTN_RESTORE,
        "knopf_zusammenfuehren": sich.BTN_MERGE, "knopf_ersetzen": sich.BTN_REPLACE, "knopf_abbrechen": sich.BTN_CANCEL,
        "dateityp": sich.FILE_TYPE, "endung": sich.BACKUP_EXT, "praefix": sich.SAFETY_PREFIX,
        "hilfsdatei_endung": sich.SETTINGS_TEMP_SUFFIX,
        "vorschau_hinweis": sich.PREVIEW_HINT, "ersetzen_titel": sich.REPLACE_TITLE,
        "ersetzen_rueckfrage_titel": sich.REPLACE_CONFIRM_TITLE, "ersetzen_rueckfrage": sich.REPLACE_CONFIRM,
        "fehler_titel": sich.ERROR_TITLE, "speichern_fehler_titel": sich.SAVE_ERROR_TITLE,
        "keine_sicherung": sich.MSG_NOT_BACKUP, "beschaedigt": sich.MSG_DAMAGED, "kaputt": sich.MSG_BROKEN,
        "einspielen_gescheitert": sich.MSG_RESTORE_FAILED, "nur_lesen": sich.MSG_READ_ONLY, "zu_neu": sich.MSG_NEWER,
        "warten_erstellen": wait["WAIT_BACKUP_TEXT"], "warten_ersetzen": wait["WAIT_REPLACE_TEXT"],
        "vorschau_a": sich.preview_text(lesen("a")),
        "ersetzen_frage_a": sich.replace_question(lesen("a")),
        "bleibt_geloescht_1": sich.kept_deleted_text(1), "bleibt_geloescht_3": sich.kept_deleted_text(3),
    }

    daten = {
        "quelle": "fisi_sicherung.replace_all, read_backup, create_backup, merge_backup, merge_message, "
                  "replace_question, replace_message, preview_text; fisi_sync.replace_lifts (Python v0.63.2)",
        "python": sys.version.split()[0],
        "zone": ZONE,
        "jetzt_utc": JETZT_UTC,
        "app_version": APP_VERSION,
        "geraet": GERAET,
        "einstellungen_vorher": EINSTELLUNGEN_VORHER,
        "ziele": ziele,
        "dateien": texte_dateien,
        "faelle": faelle,
        "aufhebungen": aufhebungen,
        "zusammenfuehren": zusammenfuehren,
        "texte": texte,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    print("G37: %d Faelle, %d Aufhebungen, %d Zusammenfuehren -> %s" % (len(faelle), len(aufhebungen), len(zusammenfuehren), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
