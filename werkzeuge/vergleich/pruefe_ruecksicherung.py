#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Beleg fuer Phase 2, Welle 4 (Weisung P2-11): Python v0.63.2 liest die Sicherungen, die die
C#-Linie mit "Sicherung für die alte App erstellen" schreibt (Test BelegDateienFuerPython),
und fuehrt sie mit "Sicherung zusammenführen" zusammen. Nur synthetische Daten (A3).

  R16  read_backup nimmt jede Datei an, backup_summary zeigt Version, Geraet, Zahlen,
       merge_backup uebernimmt die Eintraege (zweites Mal: nichts Neues).
  F5   app_version "A 0.5.0" wird angenommen und unveraendert angezeigt ("App-Version: A 0.5.0").
  A3   Das Feld "schreiber" wertet Python nur auf "vorhanden" aus (Quelltext: jede Stelle ist
       .get(WRITER_KEY) is None / is not None), und dieselbe Sicherung mit anderem Wert
       ("99.9.9") fuehrt zum selben Ergebnis. Der Wert muss also nicht mit der Version wachsen.
  A4   Eine Sicherung mit gesetzter Marke (Verlauf geleert bzw. zurueckgesetzt) entfernt beim
       Zusammenfuehren aeltere Eintraege der alten App (dritter Satz von T2).

Aufruf:
  python3 -I werkzeuge/vergleich/pruefe_ruecksicherung.py <pfad_fisi_lernplattform> <ordner_mit_sicherungen>
Rueckgabe 0 = alles belegt, sonst 1 (mit Grund).
"""
import ast
import atexit
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


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ordner = sys.argv[1], sys.argv[2]
    os.environ["TZ"] = "Europe/Berlin"
    time.tzset()
    tmp = tempfile.mkdtemp(prefix="beleg_")
    atexit.register(shutil.rmtree, tmp, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_sicherung as sich
    import fisi_sync as sync

    jetzt = datetime.datetime.fromisoformat("2026-10-20T10:30:00+00:00")
    core.device_now = sync.device_now = lambda: jetzt.astimezone()
    fehler = []

    def pruefe(bedingung, text):
        print(("ok    " if bedingung else "FEHLT ") + text)
        if not bedingung:
            fehler.append(text)

    def lesen(name):
        with open(os.path.join(ordner, name + ".fisisicherung"), "rb") as handle:
            return handle.read()

    def zeilen(db, tabelle):
        conn = sqlite3.connect(db.db_path)
        try:
            return conn.execute("SELECT uid, timestamp FROM %s ORDER BY id" % tabelle).fetchall()
        finally:
            conn.close()

    stempel = ["", 0]

    def schreibe(db, zeit, abstand, aktion):
        stempel[0], stempel[1] = zeit, abstand
        db._stamp = lambda: (stempel[0], stempel[1])
        aktion()

    def alte_app(name):
        """Lernstand der alten App mit aelteren Eintraegen (vor den Marken der Belege)."""
        db = core.DBManager(os.path.join(tmp, name + ".db"))
        schreibe(db, "2026-10-19 09:00:00", 120, lambda: db.log_card("Netzwerk", "Alte Karte", "lernen", True))
        schreibe(db, "2026-10-19 09:05:00", 120, lambda: db.save_test_result(10, 20, 50.0, core.ihk_note(50.0), 300))
        schreibe(db, "2026-10-20 13:00:00", 120, lambda: db.log_quiz_answer("Netzwerk", "Spaetere Quizfrage", True))
        return db

    # -- R16 und F5 -----------------------------------------------------------
    roh = lesen("voll")
    backup = sich.read_backup(roh)
    pruefe(backup["app_version"] == "A 0.5.0", "F5: read_backup nimmt app_version \"A 0.5.0\" an")
    zusammenfassung = sich.backup_summary(backup)
    pruefe("App-Version: A 0.5.0" in zusammenfassung["zeilen"], "F5: Vorschau zeigt \"App-Version: A 0.5.0\"")
    pruefe("Gerät: PC Beleg" in zusammenfassung["zeilen"], "R16: Vorschau zeigt das Geraet")
    pruefe(zusammenfassung["lerneintraege"] == 4, "R16: 4 Lern-Eintraege in der Vorschau")
    pruefe(backup["daten"]["schreiber"] == "0.62.4", "A3: Datei traegt schreiber 0.62.4")
    leer = core.DBManager(os.path.join(tmp, "leer.db"))
    neu = sich.merge_backup(leer, backup)
    pruefe(neu == 4 + 1 + 1, "R16: merge_backup uebernimmt 6 Eintraege (4 Lernen, 1 Abzeichen, 1 Projekt), ist %d" % neu)
    pruefe(sich.merge_backup(leer, sich.read_backup(roh)) == 0, "R16: zweites Zusammenfuehren bringt nichts Neues")
    karten = [z[0] for z in zeilen(leer, "card_events")]
    pruefe(len(karten) == 2 and all(len(u) == 32 for u in karten), "R16: Karten mit Kennung (uid) uebernommen")

    # -- A3: schreiber ---------------------------------------------------------
    with open(os.path.join(repo, "fisi_sync.py"), encoding="utf-8") as handle:
        baum = ast.parse(handle.read())
    stellen = []
    for knoten in ast.walk(baum):
        if isinstance(knoten, ast.Compare) and isinstance(knoten.left, ast.Call) \
                and getattr(knoten.left.func, "attr", "") == "get" and knoten.left.args \
                and getattr(knoten.left.args[0], "id", "") == "WRITER_KEY":
            stellen.append(type(knoten.ops[0]).__name__ + ":" + repr(getattr(knoten.comparators[0], "value", "?")))
    alle = sum(1 for k in ast.walk(baum) if isinstance(k, ast.Name) and k.id == "WRITER_KEY")
    # Verwendungen: Definition, export_local ({WRITER_KEY: WRITER}) und die Vergleiche
    pruefe(stellen and all(s in ("Is:None", "IsNot:None") for s in stellen) and alle == len(stellen) + 2,
           "A3: fisi_sync prueft \"schreiber\" nur auf vorhanden (%s)" % ", ".join(stellen))
    anders = copy.deepcopy(backup)
    anders["daten"]["schreiber"] = "99.9.9"
    anders["pruefsumme"] = sich._checksum(anders["daten"], anders["einstellungen"])
    a = alte_app("schreiber_a")
    shutil.copyfile(a.db_path, os.path.join(tmp, "schreiber_b.db"))      # gleicher Ausgangsstand
    b = core.DBManager(os.path.join(tmp, "schreiber_b.db"))
    sich.merge_backup(a, sich.read_backup(gzip.compress(json.dumps(backup, ensure_ascii=False).encode("utf-8"))))
    sich.merge_backup(b, sich.read_backup(gzip.compress(json.dumps(anders, ensure_ascii=False).encode("utf-8"))))
    gleich = all(zeilen(a, t) == zeilen(b, t) for t in core.SYNC_TABLES)
    pruefe(gleich, "A3: schreiber \"0.62.4\" und \"99.9.9\" fuehren zum selben Ergebnis")

    # -- A4: Marken entfernen aeltere Eintraege der alten App -----------------------------
    for name, tabelle, bleibt in (("verlauf_geleert", "test_results", "card_events"),
                                  ("zurueckgesetzt", "card_events", None)):
        db = alte_app("a4_" + name)
        vorher = len(zeilen(db, tabelle))
        sich.merge_backup(db, sich.read_backup(lesen(name)))
        alt = [z for z in zeilen(db, tabelle) if z[1] < "2026-10-20"]
        pruefe(vorher >= 1 and not alt, "A4 %s: aeltere Eintraege in %s entfernt" % (name, tabelle))
        pruefe(any(z[0] == "beleg-" + name for z in zeilen(db, "card_events")), "A4 %s: neuer Eintrag uebernommen" % name)
        pruefe(len(zeilen(db, "quiz_answers")) == 1, "A4 %s: spaeterer Eintrag der alten App bleibt" % name)
        if bleibt:
            pruefe(any(z[1].startswith("2026-10-19") for z in zeilen(db, bleibt)),
                   "A4 %s: Lernkarten bleiben (Verlauf betrifft nur Ergebnisse)" % name)

    if fehler:
        print("NICHT BELEGT: %d Punkt(e)" % len(fehler))
        return 1
    print("Alles belegt (Python %s)." % sys.version.split()[0])
    return 0


if __name__ == "__main__":
    sys.exit(main())
