#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G44 Teil "Verwaltung" (Phase 7, Weisung P7-6,
Sammelliste 94), gerechnet vom echten Python-Code der Python-Linie (v0.63.2,
eingefroren): die Platzverwaltung der Klasse Game (_resolve_run, new_run mit
Doppel-Tipp, _ensure_run, select_run, rename_slot, delete_run mit und ohne
eigenen Namen, reset, reload nach einem Loeschen auf dem anderen Geraet,
slot_of, active_label, slots).

In einer leeren Temp-Datenbank (fisi_core.DBManager) spielt ein Geraet "PC"
Schritt fuer Schritt. Nach jedem Schritt werden alle vier beteiligten Tabellen
(spiel_ereignisse, spiel_plaetze, spiel_bestenliste, sync_meta) vollstaendig
festgehalten, dazu der aktive Durchgang, slot_of, active_label, slots() und
die Rueckgabe oder der Fehlertext des Schritts.

Uhr und uuid4 sind fest: eine gemeinsame Uhr laeuft bei jedem Ablesen
(_stamp und _now_fine) 1,5 s weiter (Ortszeit UTC+2, utc_offset 120), uuid4
zaehlt UUID(int=(n * 104729) << 64 | n * 7919) hoch (fuer uid und Durchgang gemeinsam). So kann
C# dieselben Zeilen bis auf das letzte Zeichen nachrechnen.
Keine Lerndaten; alle Daten- und Einstellungsordner zeigen in einen leeren
Temp-Ordner, der am Ende wieder weg ist.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_verwaltung.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_verwaltung.json
"""
import datetime
import json
import os
import sqlite3
import sys
import tempfile
import uuid

AUSSEHEN = {"haut": "hell", "haare": "kurz", "haarfarbe": "braun", "oberteil": "hoodie",
            "farbe": "blau", "hose": "jeans", "brille": False, "bart": False,
            "extra": "headset", "kreis": "gelb"}
START = datetime.datetime(2026, 10, 2, 0, 0, 0)
SCHRITT_US = 1500000


def tabellen(pfad):
    with sqlite3.connect(pfad) as verbindung:
        def alle(sql):
            return [list(z) for z in verbindung.execute(sql)]
        return {
            "spiel_ereignisse": alle("SELECT id, timestamp, utc_offset, typ, daten, geraet, uid, lauf"
                                     " FROM spiel_ereignisse ORDER BY id"),
            "spiel_plaetze": alle("SELECT id, timestamp, platz, lauf, aktion, daten, geraet, uid"
                                  " FROM spiel_plaetze ORDER BY id"),
            "spiel_bestenliste": alle("SELECT id, timestamp, utc_offset, durchgang, art, schluessel, wert,"
                                      " daten, uid FROM spiel_bestenliste ORDER BY id"),
            "sync_meta": alle("SELECT key, value FROM sync_meta ORDER BY key"),
        }


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    with tempfile.TemporaryDirectory(prefix="vergleich_") as tmp:
        return erzeugen(repo, os.path.abspath(ziel), tmp)


def erzeugen(repo, ziel, tmp):
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    zufall = [0]

    def feste_uuid():
        zufall[0] += 1
        return uuid.UUID(int=((zufall[0] * 104729) << 64) | (zufall[0] * 7919))
    uuid.uuid4 = feste_uuid
    import fisi_core as core
    import fisi_game as fg

    uhr = [0]

    def jetzt():
        uhr[0] += SCHRITT_US
        return START + datetime.timedelta(microseconds=uhr[0])

    def stempel():
        return jetzt().strftime("%Y-%m-%d %H:%M:%S"), 120

    def fein():
        return jetzt().strftime("%Y-%m-%d %H:%M:%S.%f")
    core.DBManager._now_fine = staticmethod(fein)

    pfad = os.path.join(tmp, "verwaltung.db")
    db = core.DBManager(pfad)
    db._stamp = stempel
    schritte = []
    spiel = [None]

    def schritt(name, aktion):
        try:
            ergebnis = {"wert": aktion()}
        except ValueError as fehler:
            ergebnis = {"fehler": str(fehler)}
        game = spiel[0]
        state = game.state
        schritte.append({
            "name": name,
            "ergebnis": ergebnis,
            "tabellen": tabellen(pfad),
            "lauf": game.run,
            "platz": game.slot_of(),
            "anzeige": game.active_label(),
            "plaetze": game.slots(),
            "spieler": (state.profile or {}).get("name") if state is not None else None,
            "ereignisse": len(state.history) if state is not None else None,
        })

    def starten():
        spiel[0] = fg.Game(db, "PC")
        return spiel[0].run

    def g():
        return spiel[0]

    def offen():
        return g().state.open_tickets()[0]["id"]

    schritt("Start leer", starten)
    schritt("Profil ohne Platz (_ensure_run)", lambda: g().set_profile("Anna", AUSSEHEN, fg.DIFF_EASY) and None)
    schritt("Ticket verschoben", lambda: g().defer(offen()))
    schritt("Platz 1 belegt", lambda: g().new_run(1))
    schritt("Platz 4 gibt es nicht", lambda: g().new_run(4))
    schritt("Platz 0 gibt es nicht", lambda: g().new_run(0))
    schritt("Platz 3 neu", lambda: g().new_run(3))
    schritt("Platz 3 doppelt getippt", lambda: g().new_run(3))
    schritt("Platz 3 Profil", lambda: g().set_profile("  Ben mit einem sehr langen Namen fuer die Figur  ",
                                                       AUSSEHEN, fg.DIFF_NORMAL) and None)
    schritt("Platz 3 Ticket verschoben", lambda: g().defer(offen()))
    schritt("Platz 2 neu", lambda: g().new_run(2))
    schritt("Platz 3 noch einmal (belegt)", lambda: g().new_run(3))
    schritt("Platz 1 waehlen", lambda: g().select_run(g().layout()["plaetze"][1]) and None)
    schritt("Unbekannten Durchgang waehlen", lambda: g().select_run("lgibtsnicht0000000"))
    schritt("Platz 1 umbenannt", lambda: g().rename_slot(1, "   Hauptspiel   "))
    schritt("Platz 3 umbenannt (lang)", lambda: g().rename_slot(3, "Ein Name, der viel zu lang fuer den Platz ist"))
    schritt("Platz 2 umbenannt (leer)", lambda: g().rename_slot(2, "   "))
    schritt("Neu gestartet", starten)
    schritt("Platz 3 loeschen (nicht aktiv)", lambda: g().delete_run(g().layout()["plaetze"][3]))
    schritt("Unbekannten Durchgang loeschen", lambda: g().delete_run("lgibtsnicht0000000"))
    schritt("Platz 1 zuruecksetzen", lambda: g().reset())
    schritt("Platz 1 neues Profil", lambda: g().set_profile("Cara", AUSSEHEN, fg.DIFF_NORMAL) and None)
    schritt("Platz 2 waehlen", lambda: g().select_run(g().layout()["plaetze"][2]) and None)
    schritt("Platz 2 loeschen (aktiv, ohne Profil)", lambda: g().delete_run(g().run))
    schritt("Zuruecksetzen ohne Durchgang", lambda: g().reset())
    schritt("Neu gestartet ohne gespeicherten", starten)
    schritt("Platz 1 auf dem anderen Geraet geloescht",
            lambda: db.delete_run(1, g().run, "Handy") and g().reload() and None)
    schritt("Profil danach (_ensure_run)", lambda: g().set_profile("Dora", AUSSEHEN, None) and None)
    schritt("Aktiven Durchgang loeschen (mit Profil)", lambda: g().delete_run(g().run, keep_name=True))
    schritt("Neu gestartet am Ende", starten)

    os.makedirs(os.path.dirname(ziel), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump({"schritte": schritte}, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G44 Verwaltung: %d Schritte -> %s" % (len(schritte), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
