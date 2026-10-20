#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte fuer Spike C (Python-Daten in C# lesen):

  1. G1 inhalte.json: je Inhaltsdatei SHA-256 der Bytes, Art (Liste/Objekt),
     Zahl der Eintraege, Zahl aller Texte und deren UTF-8-Laenge.
  2. testdaten.db: eine Lernstand-Datenbank, angelegt vom echten Python-Code
     (fisi_core.DBManager), mit festen Beispielzeilen in jeder Tabelle.
     Dazu testdaten.json mit allen Zeilen zum Vergleich.
  3. sicherung_test.json.gz: Sicherungsdatei dieser Datenbank, erzeugt von
     fisi_sicherung.create_backup (gleiches Format wie die echte Sicherung).

Es werden NUR Testdaten erzeugt. Nicos echter Lernstand wird hier nie
gelesen (Auflage A3). Alle Daten- und Einstellungsordner zeigen in einen
leeren Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_daten.py <pfad_fisi_lernplattform> <inhalte_ordner> tests/Vergleich
"""
import datetime
import glob
import hashlib
import json
import os
import sqlite3
import sys
import tempfile


def zaehle(wert, stand):
    if isinstance(wert, str):
        stand[0] += 1
        stand[1] += len(wert.encode("utf-8"))
    elif isinstance(wert, list):
        for x in wert:
            zaehle(x, stand)
    elif isinstance(wert, dict):
        for k, x in wert.items():
            zaehle(x, stand)


def inhalte_g1(ordner):
    ergebnis = {}
    for pfad in sorted(glob.glob(os.path.join(ordner, "**", "*.json"), recursive=True)):
        name = os.path.relpath(pfad, ordner).replace(os.sep, "/")
        roh = open(pfad, "rb").read()
        obj = json.loads(roh.decode("utf-8"))
        stand = [0, 0]
        zaehle(obj, stand)
        ergebnis[name] = {
            "sha256": hashlib.sha256(roh).hexdigest(),
            "art": "liste" if isinstance(obj, list) else "objekt",
            "eintraege": len(obj),
            "texte": stand[0],
            "text_bytes_utf8": stand[1],
        }
    return ergebnis


def beispielwert(spalte, typ, zeile, tabelle):
    typ = (typ or "").upper()
    if spalte == "uid":
        return "%s-uid-%d" % (tabelle, zeile)
    if spalte == "timestamp":
        return (datetime.datetime(2026, 10, 1, 8, 0, 0) + datetime.timedelta(hours=zeile)).isoformat()
    if "INT" in typ:
        return zeile * 7 + 1
    if "REAL" in typ:
        return zeile + 0.25
    return "Prüfwert äöüß %s/%s/%d" % (tabelle, spalte, zeile)


def main():
    if len(sys.argv) != 4:
        print(__doc__)
        return 2
    repo, inhalte, ziel = sys.argv[1:4]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    db_pfad = os.path.join(tmp, "testdaten.db")
    os.environ["FISI_DB_PATH"] = db_pfad
    sys.path.insert(0, repo)
    import fisi_core
    import fisi_sicherung

    db = fisi_core.DBManager(db_pfad)
    conn = sqlite3.connect(db_pfad)
    tabellen = [r[0] for r in conn.execute(
        "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name")]
    vergleich = {"_hinweis": "Vergleichswerte Spike C, erzeugt von werkzeuge/vergleich/erzeuge_daten.py",
                 "tabellen": {}}
    for tabelle in tabellen:
        spalten = [(r[1], r[2], r[5]) for r in conn.execute("PRAGMA table_info(%s)" % tabelle)]
        frei = [s for s in spalten if not (s[2] and s[0] == "id")]
        if tabelle == "sync_meta":
            werte = [("testschluessel", "testwert")]
            conn.executemany("INSERT INTO sync_meta (key, value) VALUES (?, ?)", werte)
        else:
            for zeile in range(3):
                conn.execute("INSERT INTO %s (%s) VALUES (%s)" % (
                    tabelle, ", ".join(s[0] for s in frei), ", ".join("?" for _ in frei)),
                    [beispielwert(s[0], s[1], zeile, tabelle) for s in frei])
        conn.commit()
        namen = [s[0] for s in spalten]
        zeilen = [list(r) for r in conn.execute("SELECT * FROM %s ORDER BY rowid" % tabelle)]
        vergleich["tabellen"][tabelle] = {"spalten": namen, "typen": [s[1] for s in spalten],
                                          "zeilen": zeilen}
    conn.close()

    roh = fisi_sicherung.create_backup(db, "0.63.2", "Testgeraet")
    sicherung = fisi_sicherung.read_backup(roh)
    vergleich["sicherung"] = {
        "formatversion": sicherung["formatversion"],
        "format_abgleich": sicherung["daten"]["format"],
        "zusammenfassung": sicherung["zusammenfassung"],
        "zeilen_je_tabelle": {t: len(z) for t, z in sicherung["daten"]["tables"].items()},
    }

    os.makedirs(ziel, exist_ok=True)
    with open(os.path.join(ziel, "inhalte.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump({"_hinweis": "Vergleichswerte G1, erzeugt von werkzeuge/vergleich/erzeuge_daten.py",
                   "dateien": inhalte_g1(inhalte)}, f, ensure_ascii=False, indent=1)
        f.write("\n")
    with open(os.path.join(ziel, "testdaten.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(vergleich, f, ensure_ascii=False, indent=1)
        f.write("\n")
    with open(os.path.join(ziel, "testdaten.db"), "wb") as f:
        f.write(open(db_pfad, "rb").read())
    with open(os.path.join(ziel, "sicherung_test.json.gz"), "wb") as f:
        f.write(roh)
    print("geschrieben nach %s (Tabellen: %d)" % (ziel, len(tabellen)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
