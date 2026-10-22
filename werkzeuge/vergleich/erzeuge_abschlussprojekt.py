#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G34 (Phase 4), gerechnet vom echten Python-Code der
Python-Linie (v0.63.2, eingefroren). Welle 1 (Paket PB1) braucht nur den Teil

  speichern - Zeilen, die DBManager.save_project_field (mit
              purge_superseded_project_rows in derselben Buchung), log_ap1,
              log_scenario, log_project und save_exam mit festem Zeitpunkt
              und fester uid schreiben. Die Faelle folgen test_n16_0632.py
              (E1 Loeschen, E3 gleiche Zeit wie die Loeschung bleibt, E4 eine
              Mikrosekunde spaeter faellt weg, E5 lebendes Projekt
              unberuehrt, E7 "_geloescht" = "0" ohne Wirkung, E8 frueheste
              Loeschung zaehlt, E9 spaetere leere Fassung bleibt) und dem
              Pflichtfall der Weisung P4-3 (gleiche Mikrosekunde: die
              groessere uid gilt, in beiden Richtungen). fisi_projekt
              .delete_project wird aufgerufen und die dabei entstehenden
              save_project_field-Aufrufe werden aufgezeichnet; C# spielt
              genau diese Aufrufe nach. Danach je Tabelle Zeilen und
              Spaltentypen (ohne id) und project_fields in dict-Reihenfolge.

Die weiteren Teile (Projekte, Zeitplan, Kosten, Export, PDF) kommen mit Welle 3.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_abschlussprojekt.py <pfad_fisi_lernplattform> tests/Vergleich/abschlussprojekt.json
"""
import datetime
import json
import os
import shutil
import sqlite3
import sys
import tempfile
import time

ZONE = "Europe/Berlin"

TABELLEN = [
    ("abschlussprojekt", ["timestamp", "projekt", "feld", "wert", "uid"]),
    ("ap1_events", ["timestamp", "utc_offset", "scenario_index", "title", "theme", "correct", "uid"]),
    ("scenario_events", ["timestamp", "utc_offset", "scenario_index", "title", "theme", "correct", "uid"]),
    ("project_events", ["timestamp", "utc_offset", "project_index", "title", "category", "correct", "uid"]),
    ("pruefungen", ["timestamp", "utc_offset", "art", "punkte", "note", "dauer", "daten", "uid"]),
]


def speichern_werte(tmp):
    import fisi_core as fc
    import fisi_projekt as fpj
    jetzt = [None]
    uid = [None, None]
    fc.device_now = lambda: jetzt[0].astimezone()
    fc.DBManager._now_fine = staticmethod(
        lambda: jetzt[0].astimezone().replace(tzinfo=None).strftime("%Y-%m-%d %H:%M:%S.%f"))
    fc.DBManager._uid = staticmethod(lambda: uid[0])
    db = fc.DBManager(os.path.join(tmp, "abschlussprojekt.db"))
    aufrufe = []
    original = db.save_project_field
    zaehler = [0]

    def feld(projekt, name, wert):
        zaehler[0] += 1
        if uid[1] is None:
            uid[0] = "abschluss-uid-%04d" % zaehler[0]
        aufrufe.append({"art": "feld", "utc": jetzt[0].isoformat(), "uid": uid[0],
                        "projekt": projekt, "feld": name, "wert": wert})
        return original(projekt, name, wert)

    db.save_project_field = feld

    def setze(moment, feste_uid=None):
        jetzt[0] = datetime.datetime.fromisoformat(moment)
        uid[1] = feste_uid
        if feste_uid is not None:
            uid[0] = feste_uid

    def ereignis(art, moment, kennung, *werte):
        setze(moment, kennung)
        if art == "ap1":
            db.log_ap1(*werte)
        elif art == "szenario":
            db.log_scenario(*werte)
        elif art == "projekt":
            db.log_project(*werte)
        else:
            db.save_exam(*werte)
        aufrufe.append({"art": art, "utc": moment, "uid": kennung, "werte": list(werte)})

    # E1: Projekt mit drei Feldern, dann loeschen (delete_project)
    setze("2026-10-21T08:00:00.000001+00:00")
    db.save_project_field("p-e1", fpj.TITLE_FIELD, "Titel E1 äöüß")
    setze("2026-10-21T08:00:01.000000+00:00")
    db.save_project_field("p-e1", "ausgangslage", "Ausgangslage E1")
    setze("2026-10-21T08:00:02.250000+00:00")
    db.save_project_field("p-e1", "ziel", "Ziel E1")
    setze("2026-10-21T08:00:03.000000+00:00")
    db.save_project_field("p-e1", fpj.PAGES_FIELD, "15")
    setze("2026-10-21T08:10:00.000000+00:00")
    fpj.delete_project(db, "p-e1")
    # E3: gleiche Zeit wie die Loeschung bleibt; E4: eine Mikrosekunde spaeter faellt weg
    setze("2026-10-21T08:10:00.000000+00:00")
    db.save_project_field("p-e1", "zusatz", "gleiche Zeit")
    setze("2026-10-21T08:10:00.000001+00:00")
    db.save_project_field("p-e1", "zusatz2", "eine Mikrosekunde spaeter")
    # E9: spaetere leere Fassung bleibt
    setze("2026-10-21T10:10:00.000000+00:00")
    db.save_project_field("p-e1", "ausgangslage", "")
    # E5: lebendes Projekt bleibt unberuehrt (auch spaeter als die Loeschung)
    setze("2026-10-21T07:00:00.000000+00:00")
    db.save_project_field("p-e5", fpj.TITLE_FIELD, "Lebt")
    setze("2026-10-21T12:00:00.000000+00:00")
    db.save_project_field("p-e5", "ziel", "bleibt")
    setze("2026-10-21T12:00:00.500000+00:00")
    db.save_project_field("p-e5", "ziel", "bleibt, neuere Fassung")
    # E7: _geloescht = "0" ohne Wirkung
    setze("2026-10-21T09:00:00.000000+00:00")
    db.save_project_field("p-e7", fpj.TITLE_FIELD, "Titel E7")
    db.save_project_field("p-e7", fpj.DELETED_FIELD, "0")
    setze("2026-10-21T11:00:00.000000+00:00")
    db.save_project_field("p-e7", "ziel", "spaeter")
    # E8: frueheste Loeschung zaehlt (zwei Loeschungen, Text dazwischen und danach)
    setze("2026-10-21T13:00:00.000000+00:00")
    db.save_project_field("p-e8", fpj.TITLE_FIELD, "Titel E8")
    setze("2026-10-21T13:01:00.000000+00:00")
    db.save_project_field("p-e8", fpj.DELETED_FIELD, "1")
    setze("2026-10-21T13:02:00.000000+00:00")
    db.save_project_field("p-e8", "ziel", "dazwischen")
    setze("2026-10-21T13:03:00.000000+00:00")
    db.save_project_field("p-e8", fpj.DELETED_FIELD, "1")
    setze("2026-10-21T13:04:00.000000+00:00")
    db.save_project_field("p-e8", "", "")
    # Pflichtfall P4-3 (b): gleiche Mikrosekunde, die groessere uid gilt (beide Richtungen)
    setze("2026-10-21T14:00:00.123456+00:00", "m-uid-b")
    db.save_project_field("p-uid", "ziel", "zuerst, groessere uid")
    setze("2026-10-21T14:00:00.123456+00:00", "m-uid-a")
    db.save_project_field("p-uid", "ziel", "danach, kleinere uid")
    setze("2026-10-21T14:00:00.654321+00:00", "n-uid-a")
    db.save_project_field("p-uid", "titel", "zuerst, kleinere uid")
    setze("2026-10-21T14:00:00.654321+00:00", "n-uid-b")
    db.save_project_field("p-uid", "titel", "danach, groessere uid")
    # Sommerzeit endet (doppelte Stunde) und Sonderzeichen
    setze("2026-10-25T00:59:59.999999+00:00")
    db.save_project_field("p-zeit", "ziel", "vor dem Wechsel")
    setze("2026-10-25T01:00:00.000000+00:00")
    db.save_project_field("p-zeit", "ziel", "nach dem Wechsel")
    setze("2026-10-25T01:30:00.000000+00:00")
    db.save_project_field("p-zeit", "notiz", "Zeile 1\nZeile 2\t😀 ' \" ; DROP")

    # Ereignisse: None = angesehen, True/False = bewertet; Pruefung
    ereignis("ap1", "2026-10-21T15:00:00.000000+00:00", "e-uid-01", 3, "AP1 Titel", "Netzwerk", None)
    ereignis("ap1", "2026-10-21T15:00:10.000000+00:00", "e-uid-02", 3, "AP1 Titel", "Netzwerk", True)
    ereignis("ap1", "2026-10-21T15:00:20.000000+00:00", "e-uid-03", 4, "AP1 Titel 2", "Wirtschaft", False)
    ereignis("szenario", "2026-10-21T21:59:59.000000+00:00", "e-uid-04", 7, "Szenario äöü", "Sicherheit", None)
    ereignis("szenario", "2026-10-21T22:00:00.000000+00:00", "e-uid-05", 7, "Szenario äöü", "Sicherheit", False)
    ereignis("projekt", "2026-10-25T00:30:00.000000+00:00", "e-uid-06", 2, "Projekt", fc.CAT_NET, None)
    ereignis("projekt", "2026-10-25T01:30:00.000000+00:00", "e-uid-07", 2, "Projekt", fc.CAT_NET, True)
    ereignis("pruefung", "2026-10-21T16:00:00.000000+00:00", "e-uid-08",
             "ap1", 61, "4 (Ausreichend)", 3600, {"a": 1, "ü": [1, 2]})
    ereignis("pruefung", "2026-10-21T16:30:00.000000+00:00", "e-uid-09",
             "ap2", 87.5, "2 (Gut)", 5400.9, {})

    conn = sqlite3.connect(db.db_path)
    tabellen = {}
    for tabelle, spalten in TABELLEN:
        tabellen[tabelle] = {
            "spalten": spalten,
            "zeilen": [list(r) for r in conn.execute("SELECT %s FROM %s ORDER BY id"
                                                      % (", ".join(spalten), tabelle))],
            "typen": [list(r) for r in conn.execute("SELECT %s FROM %s ORDER BY id"
                                                     % (", ".join("typeof(%s)" % s for s in spalten),
                                                        tabelle))],
        }
    # Text, den save_exam in daten schrieb (C# bekommt ihn fertig, die Klausur baut ihn in W2)
    for aufruf, daten in zip([a for a in aufrufe if a["art"] == "pruefung"],
                             [r[0] for r in conn.execute("SELECT daten FROM pruefungen ORDER BY id")]):
        aufruf["daten_text"] = daten
    conn.close()
    return {
        "zone": ZONE,
        "aufrufe": aufrufe,
        "tabellen": tabellen,
        "felder": [[projekt, [[k, v] for k, v in felder.items()]]
                   for projekt, felder in db.project_fields().items()],
    }


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
    daten = {
        "quelle": "fisi_core DBManager.save_project_field, purge_superseded_project_rows, "
                  "project_fields, log_ap1, log_scenario, log_project, save_exam; "
                  "fisi_projekt.delete_project (Python v0.63.2)",
        "python": sys.version.split()[0],
        "speichern": speichern_werte(tmp),
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    shutil.rmtree(tmp, ignore_errors=True)
    print("G34: %d Aufrufe, %d Projektzeilen -> %s"
          % (len(daten["speichern"]["aufrufe"]),
             len(daten["speichern"]["tabellen"]["abschlussprojekt"]["zeilen"]), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
