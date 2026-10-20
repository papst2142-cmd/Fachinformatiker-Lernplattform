#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G11 (Phase 1, K2 Lernstand-Datenbank und K3
Einstellungen), gerechnet vom echten Python-Code der Python-Linie:

  1. Aufbau einer leeren Lernstand-Datenbank, wie fisi_core.DBManager sie
     anlegt: Tabellen, Spalten (PRAGMA table_info), Indizes (PRAGMA
     index_list/index_info), CREATE-Texte mit zusammengefasstem Leerraum,
     PRAGMA user_version.
  2. Datenbank-Fehlertexte und Fehlerarten (DB_ERROR_TEXTS,
     DB_READ_ONLY_LINES, db_error_kind fuer Beispieltexte von SQLite).
  3. Einstellungen: Vorgabewert von load_settings und das Ergebnis von
     saved_mode/saved_preset/saved_background/saved_font_size
     (fisi_theme) und goal_settings (fisi_lernen) fuer Beispiel-Einstellungen.

Es werden nur Testdaten benutzt; alle Daten- und Einstellungsordner zeigen in
einen leeren Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_datenbank.py <pfad_fisi_lernplattform> tests/Vergleich/datenbank.json
"""
import json
import os
import sqlite3
import sys
import tempfile


def schema(pfad):
    """Aufbau der Datenbank als JSON (gleiche Funktion im C#-Test)."""
    conn = sqlite3.connect(pfad)
    try:
        def normal(sql):
            return " ".join(sql.split()) if sql else None
        tabellen = []
        for name, sql in conn.execute(
                "SELECT name, sql FROM sqlite_master WHERE type = 'table' ORDER BY name"):
            spalten = [list(row) for row in conn.execute("PRAGMA table_info(%s)" % name)]
            indizes = []
            for row in conn.execute("PRAGMA index_list(%s)" % name):
                # seq, name, unique, origin, partial
                spalten_index = [r[2] for r in conn.execute("PRAGMA index_info(%s)" % row[1])]
                indizes.append({"name": row[1], "unique": row[2], "origin": row[3],
                                "partial": row[4], "spalten": spalten_index})
            indizes.sort(key=lambda item: item["name"])
            tabellen.append({"name": name, "sql": normal(sql), "spalten": spalten,
                             "indizes": indizes})
        index_sql = [[name, tbl, normal(sql)] for name, tbl, sql in conn.execute(
            "SELECT name, tbl_name, sql FROM sqlite_master WHERE type = 'index' ORDER BY name")]
        version = conn.execute("PRAGMA user_version").fetchone()[0]
        return {"tabellen": tabellen, "index_sql": index_sql, "user_version": version}
    finally:
        conn.close()


SQLITE_FEHLER = [
    "attempt to write a readonly database",
    "SQLite Error 8: 'attempt to write a readonly database'.",
    "database disk image is malformed",
    "file is not a database",
    "SQLite Error 26: 'file is not a database'.",
    "unable to open database file",
    "SQLite Error 14: 'unable to open database file'.",
    "database or disk is full",
    "database is locked",
    "SQLite Error 5: 'database is locked'.",
    "database table is locked",
    "no such table: x",
    "Read-Only file system",
    "",
]

EINSTELLUNGEN = [
    {},
    {"darstellung": "hell", "grundfarbe": "gruen_lime", "hintergrund": "schwarz",
     "schriftgroesse": "riesig"},
    {"darstellung": "HELL", "grundfarbe": "blau", "hintergrund": 3, "schriftgroesse": None},
    {"darstellung": ["hell"], "grundfarbe": True, "hintergrund": "", "schriftgroesse": "extra_gross"},
    {"ziel_an": False, "ziel_anzahl": 35, "serie_an": 0, "erinnerung_an": "",
     "erinnerung_zeit": "7:5"},
    {"ziel_anzahl": "15", "erinnerung_zeit": " 23:59 "},
    {"ziel_anzahl": 1, "erinnerung_zeit": "24:00"},
    {"ziel_anzahl": 1000, "erinnerung_zeit": "12:60"},
    {"ziel_anzahl": "abc", "erinnerung_zeit": "1800"},
    {"ziel_anzahl": None, "erinnerung_zeit": None, "ziel_an": None},
    {"ziel_anzahl": 12.9, "erinnerung_zeit": "-0:05", "serie_an": "nein"},
    {"ziel_anzahl": True, "erinnerung_zeit": "+8:+5", "erinnerung_an": []},
    {"ziel_anzahl": " 40 ", "erinnerung_zeit": "08:00:00", "ziel_an": [1]},
    {"ziel_anzahl": "1_0", "erinnerung_zeit": "1_2:3_0", "serie_an": {}},
    {"ziel_anzahl": -3, "erinnerung_zeit": "ab:cd", "serie_an": {"a": 1}},
    {"ziel_anzahl": "7.5", "erinnerung_zeit": 18, "ziel_an": 0.0},
]


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    db_pfad = os.path.join(tmp, "leer.db")
    os.environ["FISI_DB_PATH"] = db_pfad
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_lernen as lernen
    import fisi_theme as theme
    import fisi_update as update

    core.DBManager(db_pfad)
    aufbau = schema(db_pfad)

    # Vorgabe ohne Datei
    vorgabe = update.load_settings()
    faelle = []
    for werte in EINSTELLUNGEN:
        with open(os.path.join(tmp, "einstellungen.json"), "w", encoding="utf-8") as handle:
            json.dump(werte, handle)
        faelle.append({
            "einstellungen": werte,
            "darstellung": theme.saved_mode(),
            "grundfarbe": theme.saved_preset(),
            "hintergrund": theme.saved_background(),
            "schriftgroesse": theme.saved_font_size(),
            "tagesziel": lernen.goal_settings(update.load_settings()),
        })
    daten = {
        "quelle": "fisi_core.DBManager, fisi_update, fisi_theme, fisi_lernen (Python v0.63.2)",
        "schema": aufbau,
        "fehlertexte": core.DB_ERROR_TEXTS,
        "nur_lesen_zeilen": core.DB_READ_ONLY_LINES,
        "fehlerarten": [[text, core.db_error_kind(text)] for text in SQLITE_FEHLER],
        "einstellungen_vorgabe": vorgabe,
        "schriftgroessen": [[item["id"], item["name"], item["faktor"]] for item in theme.FONT_SIZES],
        "darstellungen": theme.MODE_IDS,
        "tagesziel_vorgabe": lernen.GOAL_SETTINGS,
        "einstellungen": faelle,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    print("G11: %d Tabellen, %d Indizes, %d Einstellungsfaelle -> %s"
          % (len(aufbau["tabellen"]), len(aufbau["index_sql"]), len(faelle), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
