#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G51 Spiel-Abgleich (Phase 8, W3, Weisung P8-5), gerechnet vom
echten Python-Code der Python-Linie (v0.63.2, eingefroren): zwei Geraete spielen offline im
selben und in verschiedenen Durchgaengen, fisi_sync gleicht ab (ein Versuch wie fisi_sync.sync
ohne Netz und ohne Uhrabweichung, wie G21), danach rechnet jedes Geraet seinen Spielstand neu.

Vor dem Abgleich (nur Python, festgehalten als fertige Datenbanken):
  PC     legt "gemein" auf Platz 1 an, spielt zwei Tage; Abgleich
  Handy  legt "drei" auf Platz 3 an, spielt einen Tag, waehlt wieder "gemein"; Abgleich
  dann offline, abwechselnd: beide loesen am Tag 3 dasselbe Ticket und machen beide Feierabend 3
  (E-5: zaehlt nur einmal), dazu je ein eigenes Ticket; das Handy waehlt "drei" und spielt
  weiter, der PC loescht "drei" und spielt einen neuen Durchgang "pc" auf Platz 2
  Tablet  ein drittes Geraet ohne Spiel (Sammelliste 95: schreibt nach dem Abgleich ohne reload)

Ab hier rechnen Python und C# dasselbe (festgehalten je Schritt):
  oeffnen   - Game je Geraet (aktiver Durchgang)
  runde1    - Abgleich PC, Handy, Tablet, PC, Handy: je Schritt export_local als Text,
              received, gone; danach die drei Spiel-Tabellen je Geraet (Anzahl, Pruefsumme)
  nach1     - reload auf PC und Handy: aktiver Durchgang, je Durchgang Ereignisse und jedes Feld
              des Spielstands (wie G44), Plaetze, Bestenliste
  schritt1  - je Geraet ein geloestes Ticket (Tablet ohne reload: _ensure_run), Kurzstand
  runde2    - Abgleich wie runde1 (nur received, gone)
  schritt2  - PC macht Feierabend 4 (Spielschritt nach dem Abgleich), Handy und Tablet reload
  runde3    - Abgleich wie runde1, danach reload auf allen: Kurzstand, Bestenliste ohne Zeiten
Kurzstand: Durchgang, Geld, Ansehen, Tag, Laenge der Historie, uebersprungene Doppel,
Fingerabdruck (Ereignisse ohne Zeiten und Werte wie fingerabdruck.py).

Dazu:
  bestenliste_text - eine Zeile mit wert "12.5" (Text) und eine mit "abc": was records() liefert
              und ob record_board wirft (Sammelliste 93)
  unbekannte_arten - ein Durchgang mit den C#-Ereignissen stadt_gegangen, stadt_gewartet,
              teil_gekauft: was GameState 0.63.2 daraus macht (Weisung P8-5 Punkt 4)

Keine Lerndaten (Auflage A3); alle Daten- und Einstellungsordner zeigen in einen leeren
Temp-Ordner. Uhr, Zeitstempel und uuid4 sind fest, damit die Datei bei jedem Lauf gleich ist.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_abgleich.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_abgleich.json
"""
import atexit
import datetime
import hashlib
import json
import os
import shutil
import sqlite3
import sys
import tempfile
import uuid

ZONE = "Europe/Berlin"
JETZT_UTC = "2026-10-20T10:00:00+00:00"
HIER = os.path.dirname(os.path.abspath(__file__))
SPIEL_TABELLEN = (
    ("spiel_ereignisse", "uid, timestamp, utc_offset, typ, daten, geraet, lauf"),
    ("spiel_plaetze", "uid, timestamp, platz, lauf, aktion, daten, geraet"),
    ("spiel_bestenliste", "uid, timestamp, utc_offset, durchgang, art, schluessel, wert, daten"),
)
GERAETE = ("PC", "Handy", "Tablet")
RUNDE = ("PC", "Handy", "Tablet", "PC", "Handy")


def wert(v):
    """Wie wert() in erzeuge_spiel_neuaufbau.py (G44)."""
    if isinstance(v, (set, frozenset)):
        return {"menge": sorted((wert(x) for x in v),
                                key=lambda x: json.dumps(x, ensure_ascii=True, separators=(",", ":")))}
    if isinstance(v, (tuple, list)):
        return [wert(x) for x in v]
    if isinstance(v, dict):
        if all(isinstance(k, str) for k in v):
            return {k: wert(x) for k, x in v.items()}
        return {"paare": [[wert(k), wert(x)] for k, x in v.items()]}
    return v


def pruefsumme(v):
    text = json.dumps(v, ensure_ascii=False, separators=(",", ":"))
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def stand(state):
    """Wie stand() in erzeuge_spiel_uebernahme.py (G44)."""
    felder = {k: wert(v) for k, v in vars(state).items()
              if k not in ("content", "_pressure", "_topic_base")}
    return {"pruefsummen": {k: pruefsumme(v) for k, v in felder.items()},
            "klartext": {k: v for k, v in felder.items() if len(json.dumps(v, ensure_ascii=False)) <= 200},
            "anzahl_history": len(state.history),
            "abgeleitet": {"day": state.day, "rank": state.rank, "level": state.level,
                           "salary": state.salary, "mean_reputation": state.mean_reputation}}


def fingerabdruck(state):
    """Wie fingerabdruck.py: Hash aus Ereignissen und Werten (ohne Zeiten)."""
    events = [(kind, data) for _ts, kind, data in state.history]
    values = {
        "tag": state.day, "geld": round(state.money, 6), "ansehen": state.reputation,
        "mitarbeiter": sorted((m["id"], m.get("gehalt"), json.dumps(m.get("werte"), sort_keys=True))
                              for m in state.staff_list()),
        "stimmung": sorted(state.mood.items()),
    }
    blob = json.dumps({"events": events, "werte": values}, sort_keys=True, default=str, ensure_ascii=False)
    return hashlib.sha256(blob.encode("utf-8")).hexdigest()


def kurzstand(game):
    """Was ein Geraet nach einem Schritt zeigt (ohne Zeitstempel, die in C# eigene sind)."""
    state = game.state
    anzahl = len(game.db.game_events(game.run)) if game.run else 0
    return {"lauf": game.run, "geld": state.money, "ansehen": wert(state.reputation), "tag": state.day,
            "history": len(state.history), "uebersprungen": anzahl - len(state.history),
            "fingerabdruck": fingerabdruck(state)}


def datenbank(pfad):
    """Aufbau und alle Zeilen einer Datenbank (zum Nachbau in C#), wie G44."""
    conn = sqlite3.connect(pfad)
    try:
        aufbau = [list(z) for z in conn.execute(
            "SELECT type, name, sql FROM sqlite_master WHERE sql IS NOT NULL"
            " AND name != 'sqlite_sequence' ORDER BY rowid")]
        tabellen = {}
        for (name,) in conn.execute("SELECT name FROM sqlite_master WHERE type = 'table'"
                                    " AND name != 'sqlite_sequence' ORDER BY name"):
            cur = conn.execute("SELECT * FROM %s ORDER BY rowid" % name)
            tabellen[name] = {"spalten": [d[0] for d in cur.description],
                              "zeilen": [list(r) for r in cur.fetchall()]}
        folge = [list(r) for r in conn.execute("SELECT name, seq FROM sqlite_sequence ORDER BY name")]
        return {"aufbau": aufbau, "tabellen": tabellen, "sqlite_sequence": folge}
    finally:
        conn.close()


def spiel_tabellen(pfad):
    conn = sqlite3.connect(pfad)
    try:
        return {name: [list(r) for r in conn.execute("SELECT %s FROM %s ORDER BY uid" % (spalten, name))]
                for name, spalten in SPIEL_TABELLEN}
    finally:
        conn.close()


def kurz(tabellen):
    return {name: {"anzahl": len(zeilen), "sha256": pruefsumme(zeilen)} for name, zeilen in tabellen.items()}


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    os.environ["TZ"] = ZONE
    import time
    time.tzset()
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    atexit.register(shutil.rmtree, tmp, True)   # nur Testdaten, nach dem Lauf weg
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    sys.path.insert(1, HIER)
    zufall = [0]

    def feste_uuid():
        zufall[0] += 1
        return uuid.UUID(bytes=hashlib.sha256(b"abgleich %d" % zufall[0]).digest()[:16])
    uuid.uuid4 = feste_uuid
    import fisi_core as core
    import fisi_game as fg
    import fisi_sync as sync
    import szenario045 as sz

    jetzt = datetime.datetime.fromisoformat(JETZT_UTC)
    core.device_now = sync.device_now = lambda: jetzt.astimezone()

    uhr = [0]
    start = datetime.datetime(2026, 10, 2, 0, 0, 0)

    def stempel(_self=None):
        uhr[0] += 7
        return (start + datetime.timedelta(seconds=uhr[0])).strftime("%Y-%m-%d %H:%M:%S"), 120

    def fein():
        uhr[0] += 3
        return (start + datetime.timedelta(seconds=uhr[0], microseconds=uhr[0] * 37 % 1000000)).strftime(
            "%Y-%m-%d %H:%M:%S.%f")
    core.DBManager._stamp = stempel
    core.DBManager._now_fine = staticmethod(fein)

    aufgaben = [t["id"] for t in fg.GAME["aufgaben"]]
    rep = {k: 1 for k in fg.AXIS_KEYS}

    def loesen(game, nummer, tag, richtig=True):
        game._log(fg.EV_SOLVED, {"aufgabe": aufgaben[nummer], "tag": tag, "richtig": richtig,
                                 "geld": 60, "reputation": rep})

    pfade = {g: os.path.join(tmp, g.lower() + ".db") for g in GERAETE}
    dbs = {g: core.DBManager(pfade[g]) for g in GERAETE}
    server = [None]

    def versuch(geraet):
        """Ein Versuch von fisi_sync.sync ohne Netz und ohne Uhrabweichung (wie G21), danach hochladen."""
        db = dbs[geraet]
        sync._SERVER_CLOCK.update(server=None, device=None)
        remote = json.loads(server[0]) if server[0] is not None else None
        stats = sync.MergeStats()
        deviation = sync._clock_deviation()
        mixed = sync._note_mixed(db, remote, deviation)
        received = 0
        if remote:
            received = sync.merge_into_local(db, remote, check_future=False, stats=stats, mixed=mixed)
        text = json.dumps(sync.export_local(db), ensure_ascii=False, separators=(",", ":"))
        server[0] = text
        return {"geraet": geraet, "export": text, "received": received, "gone": stats.removed}

    def runde(mit_export):
        schritte = [versuch(g) for g in RUNDE]
        if not mit_export:
            for s in schritte:
                s.pop("export")
        return schritte

    # -- vor dem Abgleich (nur Python) -------------------------------------------------
    pc = fg.Game(dbs["PC"], "PC")
    gemein = pc.new_run(1)
    pc.set_profile("Nico", sz.LOOK, fg.DIFF_NORMAL)
    for i in range(6):
        loesen(pc, i, 1 + i // 3, richtig=i != 4)
    pc._log(fg.EV_DAY_END, {"tag": 1, "gehalt": 140})
    pc._log(fg.EV_DAY_END, {"tag": 2, "gehalt": 140})
    versuch("PC")
    versuch("Handy")
    handy = fg.Game(dbs["Handy"], "Handy")
    drei = handy.new_run(3)
    handy.set_profile("Handy", sz.LOOK, fg.DIFF_EASY)
    loesen(handy, 40, 1)
    handy._log(fg.EV_DAY_END, {"tag": 1, "gehalt": 140})
    handy.select_run(gemein)
    versuch("Handy")
    versuch("PC")
    # offline, abwechselnd
    pc.reload()
    handy.reload()
    loesen(pc, 10, 3)
    loesen(handy, 10, 3)            # E-5: dasselbe Ticket am selben Tag
    loesen(pc, 11, 3)
    loesen(handy, 12, 3, richtig=False)
    pc._log(fg.EV_DAY_END, {"tag": 3, "gehalt": 140})
    handy._log(fg.EV_DAY_END, {"tag": 3, "gehalt": 140})   # E-5: zweiter Feierabend 3
    handy.select_run(drei)
    loesen(handy, 41, 2)
    pc.delete_run(drei)
    eigen = pc.new_run(2)
    pc.set_profile("PC", sz.LOOK, fg.DIFF_NORMAL)
    loesen(pc, 20, 1)
    pc._log(fg.EV_DAY_END, {"tag": 1, "gehalt": 140})
    pc.select_run(gemein)
    loesen(handy, 42, 2)            # nach dem Loeschen auf dem PC, noch im alten Durchgang
    del pc, handy
    vorher = {g: datenbank(pfade[g]) for g in GERAETE}

    # -- ab hier rechnet C# dasselbe ---------------------------------------------------
    spiele = {g: fg.Game(dbs[g], g) for g in GERAETE}
    oeffnen = {g: spiele[g].run for g in GERAETE}

    r1 = runde(True)
    tabellen1 = {g: spiel_tabellen(pfade[g]) for g in GERAETE}
    for g in GERAETE:
        if tabellen1[g] != tabellen1["PC"]:
            raise SystemExit("Runde 1: %s hat andere Spielzeilen als PC" % g)
    for g in ("PC", "Handy"):
        spiele[g].reload()
    layout = spiele["PC"].layout()
    laeufe = {}
    for lauf in sorted(set(layout["plaetze"].values()) | set(layout["extra"]) | {drei}):
        events = dbs["PC"].game_events(lauf)
        state = fg.GameState(events, fg.GAME)
        laeufe[lauf] = {"anzahl": len(events), "ereignisse": pruefsumme([list(e) for e in events]),
                        "uebersprungen": len(events) - len(state.history), "stand": stand(state)}
    nach1 = {
        "aktiv": {g: spiele[g].run for g in ("PC", "Handy")},
        "laeufe": laeufe,
        "belegung": {"plaetze": [[k, v] for k, v in layout["plaetze"].items()],
                     "namen": [[k, v] for k, v in layout["namen"].items()],
                     "extra": layout["extra"], "geloescht": sorted(layout["geloescht"])},
        "plaetze": {g: spiele[g].slots() for g in ("PC", "Handy")},
        "bestenliste": [[t, d, a, s, w, x] for t, d, a, s, w, x in spiele["PC"].records()],
        "tabellen": kurz(tabellen1["PC"]),
    }

    loesen(spiele["PC"], 50, 4)
    loesen(spiele["Handy"], 51, 4)
    loesen(spiele["Tablet"], 52, 4)  # Sammelliste 95: ohne reload, _ensure_run waehlt den Durchgang
    schritt1 = {g: kurzstand(spiele[g]) for g in GERAETE}

    r2 = runde(False)
    spiele["PC"]._log(fg.EV_DAY_END, {"tag": 4, "gehalt": 140})
    schritt2 = {"PC": kurzstand(spiele["PC"])}
    for g in ("Handy", "Tablet"):
        spiele[g].reload()
        schritt2[g] = kurzstand(spiele[g])

    r3 = runde(False)
    ende = {}
    for g in GERAETE:
        spiele[g].reload()
        ende[g] = kurzstand(spiele[g])
    tabellen3 = {g: spiel_tabellen(pfade[g]) for g in GERAETE}
    for g in GERAETE:
        if tabellen3[g] != tabellen3["PC"]:
            raise SystemExit("Runde 3: %s hat andere Spielzeilen als PC" % g)
    anzahl3 = {name: len(zeilen) for name, zeilen in tabellen3["PC"].items()}
    besten3 = sorted([[d, a, s, w] for _t, d, a, s, w, _x in spiele["PC"].records()],
                     key=lambda z: json.dumps(z, ensure_ascii=False))

    # -- Sammelliste 93: Text in der Spalte wert ----------------------------------------
    textpfad = os.path.join(tmp, "text.db")
    textdb = core.DBManager(textpfad)
    conn = sqlite3.connect(textpfad)
    for nummer, roh in enumerate(("12.5", "abc")):
        conn.execute("INSERT INTO spiel_bestenliste (timestamp, utc_offset, durchgang, art, schluessel, wert, daten, uid)"
                     " VALUES (?, 120, 'ltext', ?, 'diensttage', ?, '{}', ?)",
                     ("2026-10-02 09:00:0%d" % nummer, fg.REC_BEST, roh, "text%d" % nummer))
    conn.commit()
    conn.close()
    zeilen = textdb.records()
    try:
        fg.record_board(zeilen, None, fg.GAME)
        tafel = "ok"
    except Exception as fehler:   # Python 0.63.2 rechnet int("abc")
        tafel = type(fehler).__name__
    bestenliste_text = {"werte": [[z[4], type(z[4]).__name__] for z in zeilen], "record_board": tafel}

    # -- Weisung P8-5 Punkt 4: Ereignisarten, die nur C# schreibt ------------------------
    unbekannt = [
        ("2026-10-02 10:00:00", fg.EV_PROFILE, {"name": "Neu", "aussehen": sz.LOOK}),
        ("2026-10-02 10:00:01", "stadt_gegangen", {"tag": 1, "von": "zuhause", "nach": "elektromarkt", "minuten": 30}),
        ("2026-10-02 10:00:02", "stadt_gewartet", {"tag": 1, "ort": "elektromarkt", "minuten": 60}),
        ("2026-10-02 10:00:03", "teil_gekauft", {"teil": "ram", "menge": 2, "geld": -80, "tag": 1, "laden": "elektromarkt"}),
        ("2026-10-02 10:00:04", fg.EV_DAY_END, {"tag": 1, "gehalt": 140}),
    ]
    zustand = fg.GameState(unbekannt, fg.GAME)
    unbekannte_arten = {
        "ereignisse": [[t, k, d] for t, k, d in unbekannt],
        "geld": zustand.money, "tag": zustand.day, "history": len(zustand.history),
        "arten": [k for _t, k, _d in zustand.history],
        "stand": stand(zustand),
    }

    daten = {
        "jetzt_utc": JETZT_UTC,
        "laeufe_namen": {"gemein": gemein, "drei": drei, "pc": eigen},
        "vorher": vorher,
        "oeffnen": oeffnen,
        "runde1": r1,
        "nach1": nach1,
        "schritt1": schritt1,
        "runde2": r2,
        "schritt2": schritt2,
        "runde3": r3,
        "ende": {"stand": ende, "anzahl": anzahl3, "bestenliste": besten3},
        "bestenliste_text": bestenliste_text,
        "unbekannte_arten": unbekannte_arten,
    }
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G51 Spiel-Abgleich: %d Durchgaenge, %d Ereignisse nach Runde 3, Handy waehlt %s -> %s" % (
        len(laeufe), anzahl3["spiel_ereignisse"], nach1["aktiv"]["Handy"], ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
