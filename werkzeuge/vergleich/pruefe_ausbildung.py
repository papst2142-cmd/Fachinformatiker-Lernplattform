#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Beleg fuer Phase 4, W2 (Weisung P4-6 Frage 2 a, Testplan P4-5 Teil A Abschnitt 5): Python v0.63.2
liest eine C#-Sicherung mit der reservierten Zeile zz-einstellungen/fachrichtung (Test
AusbildungTests.BelegDateiFuerPython). Die C#-Linie zaehlt die Zeile nicht als Projekt
(Zusammenfassung projekte 1); Python zaehlt sie mit (Vorschau "Abschlussprojekte: 2", +1,
dokumentierte Abweichung). Beim Zusammenfuehren kommt die Zeile an wie jedes Projektfeld.
Nur synthetische Daten (A3).

Aufruf:
  python3 -I werkzeuge/vergleich/pruefe_ausbildung.py <pfad_fisi_lernplattform> <ordner_mit_sicherung>
Rueckgabe 0 = alles belegt, sonst 1 (mit Grund).
"""
import os
import sqlite3
import sys
import tempfile
import time


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ordner = sys.argv[1], sys.argv[2]
    os.environ["TZ"] = "Europe/Berlin"
    time.tzset()
    tmp = tempfile.mkdtemp(prefix="beleg_ausbildung_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_sicherung as sich
    fehler = []

    def pruefe(bedingung, text):
        print(("ok    " if bedingung else "FEHLT ") + text)
        if not bedingung:
            fehler.append(text)

    with open(os.path.join(ordner, "ausbildung.fisisicherung"), "rb") as handle:
        roh = handle.read()
    backup = sich.read_backup(roh)
    pruefe(backup["zusammenfassung"]["projekte"] == 1, "C#: gespeicherte Zusammenfassung projekte 1")
    zusammenfassung = sich.backup_summary(backup)
    pruefe("Abschlussprojekte: 2" in zusammenfassung["zeilen"],
           "Python zaehlt die Zeile als Projekt mit (+1, dokumentierte Abweichung): %s"
           % [z for z in zusammenfassung["zeilen"] if z.startswith("Abschlussprojekte")])
    pruefe(zusammenfassung["lerneintraege"] == 1, "Lern-Eintraege unveraendert (1)")
    db = core.DBManager(os.path.join(tmp, "leer.db"))
    neu = sich.merge_backup(db, backup)
    pruefe(neu == 3, "merge_backup uebernimmt 3 Eintraege (1 Karte, 2 Projektfelder), ist %d" % neu)
    conn = sqlite3.connect(db.db_path)
    try:
        zeilen = conn.execute("SELECT feld, wert FROM abschlussprojekt WHERE projekt = 'zz-einstellungen'").fetchall()
    finally:
        conn.close()
    pruefe(zeilen == [("fachrichtung", "ae")], "Zeile zz-einstellungen/fachrichtung = ae angekommen, ist %r" % zeilen)
    pruefe(sich.merge_backup(db, sich.read_backup(roh)) == 0, "zweites Zusammenfuehren bringt nichts Neues")

    if fehler:
        print("NICHT BELEGT: %d Punkt(e)" % len(fehler))
        return 1
    print("Alles belegt (Python %s)." % sys.version.split()[0])
    return 0


if __name__ == "__main__":
    sys.exit(main())
