#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G44 Teil "Plaetze" (Phase 7, W2 "Spiel-Kern
Teil 1"), gerechnet vom echten Python-Code der Python-Linie (v0.63.2,
eingefroren): Spielstand-Plaetze und Durchgaenge.

In einer leeren Temp-Datenbank (fisi_core.DBManager) entstehen Schritt fuer
Schritt: alte Ereignisse von vor 0.48, neue Durchgaenge auf Plaetzen (auch
zwei Geraete auf demselben Platz und ein Durchgang ueber alle Plaetze
hinaus), Umbenennungen (lang, leer, falscher Platz, Zahl als Name), ein
geloeschter Durchgang, kaputte JSON-Daten. Nach jedem Schritt werden die
Tabellen spiel_ereignisse, spiel_plaetze und sync_meta festgehalten und dazu,
was Python daraus liest: slot_rows, has_legacy_events, slot_layout,
event_counts, game_events je Durchgang, Game.slots() und slot_line, der
gespeicherte und der gewaehlte Durchgang (_resolve_run).

Keine Lerndaten; alle Daten- und Einstellungsordner zeigen in einen leeren
Temp-Ordner. Uhr und uuid4 sind fest.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_plaetze.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_plaetze.json
"""
import json
import os
import sqlite3
import sys
import tempfile
import uuid

AUSSEHEN = {"haut": "hell", "haare": "kurz", "haarfarbe": "braun", "oberteil": "hoodie",
            "farbe": "blau", "hose": "jeans", "brille": False, "bart": False,
            "extra": "headset", "kreis": "gelb"}


def tabellen(pfad):
    with sqlite3.connect(pfad) as verbindung:
        ereignisse = [list(z) for z in verbindung.execute(
            "SELECT id, timestamp, utc_offset, typ, daten, geraet, uid, lauf FROM spiel_ereignisse ORDER BY id")]
        plaetze = [list(z) for z in verbindung.execute(
            "SELECT id, timestamp, platz, lauf, aktion, daten, geraet, uid FROM spiel_plaetze ORDER BY id")]
        meta = [list(z) for z in verbindung.execute("SELECT key, value FROM sync_meta ORDER BY key")]
    return {"spiel_ereignisse": ereignisse, "spiel_plaetze": plaetze, "sync_meta": meta}


def stufe(name, fg, core, db, pfad):
    tab = tabellen(pfad)            # vor dem Start des Spiels, das sync_meta schreiben kann
    rows = db.slot_rows()
    legacy = db.has_legacy_events()
    layout = fg.slot_layout(rows, legacy)
    gespeichert = db.get_meta(core.ACTIVE_RUN_KEY)
    game = fg.Game(db, "PC")        # _resolve_run wie beim Start (schreibt sync_meta, wenn noetig)
    plaetze = game.slots()
    laeufe = sorted(set(layout["plaetze"].values()) | set(layout["extra"]) | {core.LEGACY_RUN})
    return {
        "name": name,
        "tabellen": tab,
        "zeilen": [[t, u, s, r, a, d] for t, u, s, r, a, d in rows],
        "alte": legacy,
        "belegung": {"plaetze": [[k, v] for k, v in layout["plaetze"].items()],
                     "namen": [[k, v] for k, v in layout["namen"].items()],
                     "extra": layout["extra"], "geloescht": sorted(layout["geloescht"])},
        "anzahl": sorted([k, v] for k, v in db.event_counts().items()),
        "ereignisse": {lauf: [list(e) for e in db.game_events(lauf)] for lauf in laeufe},
        "gespeichert": gespeichert,
        "aktiv": game.run,
        "plaetze": plaetze,
        "zeilen_text": [fg.slot_line(p) for p in plaetze],
    }


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    zufall = [0]

    def feste_uuid():
        zufall[0] += 1
        return uuid.UUID(int=zufall[0] * 104729)
    uuid.uuid4 = feste_uuid
    import fisi_core as core
    import fisi_game as fg

    uhr = [0]

    def stempel():
        uhr[0] += 7
        return "2026-10-02 %02d:%02d:%02d" % (uhr[0] // 3600, (uhr[0] // 60) % 60, uhr[0] % 60), 120

    def fein():
        uhr[0] += 3
        return "2026-10-02 %02d:%02d:%02d.%06d" % (uhr[0] // 3600, (uhr[0] // 60) % 60, uhr[0] % 60, uhr[0] * 37 % 1000000)
    core.DBManager._now_fine = staticmethod(fein)

    pfad = os.path.join(tmp, "plaetze.db")
    db = core.DBManager(pfad)
    db._stamp = stempel
    stufen = [stufe("leer", fg, core, db, pfad)]

    db.log_game_event(fg.EV_DIFFICULTY, json.dumps({"stufe": fg.DIFF_NORMAL}), "PC", run="")
    db.log_game_event(fg.EV_PROFILE, json.dumps({"name": "Alt", "aussehen": {}}), "PC", run="")
    stufen.append(stufe("alte Ereignisse", fg, core, db, pfad))

    game = fg.Game(db, "PC")
    game.new_run(2)
    stufen.append(stufe("Platz 2 angelegt, ohne Profil", fg, core, db, pfad))
    game.set_profile("Anna", AUSSEHEN, fg.DIFF_EASY)
    game.defer(game.state.open_tickets()[0]["id"])
    stufen.append(stufe("Platz 2 mit Profil", fg, core, db, pfad))

    # Ein zweites Geraet hat offline ebenfalls Platz 2 belegt, ein drittes Platz 1
    db.log_slot(2, "lfremd000000000002", fg.SLOT_CREATED, "{}", "Handy")
    db.log_game_event(fg.EV_PROFILE, json.dumps({"name": "Handy-Ben", "aussehen": AUSSEHEN}), "Handy",
                      run="lfremd000000000002")
    db.log_slot(1, "lfremd000000000001", fg.SLOT_CREATED, "{}", "Tablet")
    stufen.append(stufe("zwei Geraete, ein Extra", fg, core, db, pfad))

    game = fg.Game(db, "PC")
    game.rename_slot(1, "   Mein erster Platz mit einem sehr langen Namen   ")
    game.rename_slot(3, "   ")
    game.rename_slot(2, "Zwei")
    db.log_slot(7, "", fg.SLOT_RENAMED, json.dumps({"name": "falscher Platz"}), "PC")
    db.log_slot(2, "", fg.SLOT_RENAMED, json.dumps({"name": 42}), "PC")
    db.log_slot(3, "", fg.SLOT_RENAMED, "kaputt{", "PC")
    db.log_slot(0, "", fg.SLOT_CREATED, "{}", "PC")
    stufen.append(stufe("Umbenannt", fg, core, db, pfad))

    with sqlite3.connect(pfad) as verbindung:
        verbindung.execute("INSERT INTO spiel_ereignisse (timestamp, utc_offset, typ, daten, geraet, uid, lauf)"
                           " VALUES ('2026-10-02 09:00:00', 120, 'tag_beendet', 'kaputt{', 'PC', 'u1', ?)", (game.run,))
        verbindung.execute("INSERT INTO spiel_ereignisse (timestamp, utc_offset, typ, daten, geraet, uid, lauf)"
                           " VALUES ('2026-10-02 09:00:01', 120, 'tag_beendet', '[1, 2]', 'PC', 'u2', ?)", (game.run,))
        verbindung.execute("INSERT INTO spiel_ereignisse (timestamp, utc_offset, typ, daten, geraet, uid, lauf)"
                           " VALUES ('2026-10-02 09:00:02', 120, 'tag_beendet', '', 'PC', 'u3', ?)", (game.run,))
    stufen.append(stufe("kaputte Daten", fg, core, db, pfad))

    db.delete_run(2, game.run, "PC")
    stufen.append(stufe("Platz 2 geloescht", fg, core, db, pfad))
    db.set_meta(core.ACTIVE_RUN_KEY, "lgibtsnicht")
    stufen.append(stufe("gespeicherter Durchgang fehlt", fg, core, db, pfad))
    db.delete_run(1, core.LEGACY_RUN, "PC")
    stufen.append(stufe("alter Durchgang geloescht", fg, core, db, pfad))

    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump({"stufen": stufen}, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G44 Plaetze: %d Stufen -> %s" % (len(stufen), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
