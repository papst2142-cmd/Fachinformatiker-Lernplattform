#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G44 Teil "Uebernahme" (Phase 7, W8 "Spielstand
aus Python"), gerechnet vom echten Python-Code der Python-Linie (v0.63.2,
eingefroren): Spielstaende, die in einer echten Python-Datenbank entstanden
sind, kommen ueber den Uebernahme-Weg in die C#-Linie und muessen dort gleich
nachgerechnet werden (Datenverlust-Pruefung R3).

Eine leere Temp-Datenbank (fisi_core.DBManager) wird mit der Spiel-API von
Python gefuellt:
  alt     - Ereignisse von vor 0.48 (ohne Durchgang, Geraet Handy): Platz 1
  firma   - Platz 2: das Szenario 045 des Fingerabdrucks (szenario045.spaet,
            Raeume, Werbung, Zertifizierung, Kredit, 8 weitere Tage mit
            Personal-Ereignissen), Platz umbenannt; der Hash des Spielstands
            aus der Datenbank muss f2b59ce8...6656 sein
  weg     - ein Durchgang auf Platz 3, gespielt und wieder geloescht (seine
            Ereignisse sind weg, seine Bestenliste-Zeilen bleiben)
  einfach - danach Platz 3 neu: Schwierigkeit Einfach, 8 Tage (szenario045.frueh)
Die Bestenliste fuellt das Spiel selbst (check_achievements).

Festgehalten wird:
  quelle_db  - die Python-Datenbank zum Nachbau: ihr Aufbau (sqlite_master)
               und alle Zeilen aller Tabellen mit id, sqlite_sequence
  sicherung  - Sicherung dieser Datenbank (create_backup, JSON vor gzip); die
               Abgleich-Datei (export_local) ist genau ihr Teil "daten" (der
               Erzeuger prueft das), C# baut sie daraus
  laeufe     - je Durchgang, was Python aus seiner Datenbank liest:
               game_events (Anzahl, Pruefsumme) und GameState (jedes Feld wie
               G44 Neuaufbau als Pruefsumme von json.dumps, kleine Felder auch
               im Klartext, dazu Tag, Rang, Stufe, Gehalt, mittleres Ansehen);
               beim Durchgang firma der Hash des Fingerabdrucks
  plaetze    - slot_layout, Game.slots() und slot_line, event_counts
  bestenliste- records() [Zeit, Durchgang, Art, Schluessel, Wert, Daten]
  ziele      - Python fuehrt Sicherung und Abgleich-Datei selbst in eine leere
               Datenbank zusammen (merge_backup, merge_into_local); die drei
               Spiel-Tabellen danach sind gleich denen der Quelle (der Erzeuger
               prueft das), festgehalten je Tabelle Anzahl und Pruefsumme der
               Zeilen ohne id (nach uid)

Keine Lerndaten (Auflage A3); alle Daten- und Einstellungsordner zeigen in
einen leeren Temp-Ordner. Uhr, Zeitstempel und uuid4 sind fest, damit die
Datei bei jedem Lauf gleich ist.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_uebernahme.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_uebernahme.json
"""
import atexit
import datetime
import gzip
import hashlib
import json
import os
import shutil
import sqlite3
import sys
import tempfile
import uuid

SOLL = "f2b59ce8f78a533cc3ec565ab983d418959e788d8ff16d35a907de328a626656"
HIER = os.path.dirname(os.path.abspath(__file__))
ZONE = "Europe/Berlin"
JETZT_UTC = "2026-10-20T10:00:00+00:00"
SICHERUNG_ERSTELLT = datetime.datetime(2026, 10, 20, 11, 30, 0)
SPIEL_TABELLEN = (
    ("spiel_ereignisse", "uid, timestamp, utc_offset, typ, daten, geraet, lauf"),
    ("spiel_plaetze", "uid, timestamp, platz, lauf, aktion, daten, geraet"),
    ("spiel_bestenliste", "uid, timestamp, utc_offset, durchgang, art, schluessel, wert, daten"),
)


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
    """Alle Felder wie G44 Neuaufbau, je Feld als Pruefsumme von json.dumps(wert(feld)) (streng:
    1 und 1.0 verschieden); die kleinen Felder zusaetzlich im Klartext."""
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


def datenbank(pfad):
    """Aufbau und alle Zeilen einer Datenbank (zum Nachbau in C#)."""
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
    """Je Spiel-Tabelle Anzahl und Pruefsumme der Zeilen (ohne id, nach uid)."""
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
        # fest, aber vorne verschieden (Game.new_run nimmt die ersten 16 Zeichen)
        return uuid.UUID(bytes=hashlib.sha256(b"uid %d" % zufall[0]).digest()[:16])
    uuid.uuid4 = feste_uuid
    import fisi_core as core
    import fisi_game as fg
    import fisi_sicherung as sich
    import fisi_sync as sync
    import szenario045 as sz

    jetzt = datetime.datetime.fromisoformat(JETZT_UTC)
    core.device_now = sync.device_now = lambda: jetzt.astimezone()
    sich._now = lambda: SICHERUNG_ERSTELLT
    sich.load_settings = lambda: {"ziel_anzahl": 25, "serie_an": True}

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

    pfad = os.path.join(tmp, "quelle.db")
    db = core.DBManager(pfad)

    # -- alt: Ereignisse von vor 0.48 (ohne Durchgang) --------------------------
    db.log_game_event(fg.EV_PROFILE, json.dumps({"name": "Alt", "aussehen": sz.LOOK}, ensure_ascii=False),
                      "Handy", run="")
    for i, task in enumerate(fg.GAME["aufgaben"][:6]):
        db.log_game_event(fg.EV_SOLVED, json.dumps({"aufgabe": task["id"], "tag": 1 + i // 3, "richtig": i != 4,
                                                    "geld": 60, "reputation": {k: 1 for k in fg.AXIS_KEYS}}),
                          "Handy", run="")
    for tag in (1, 2):
        db.log_game_event(fg.EV_DAY_END, json.dumps({"tag": tag, "gehalt": 140}), "Handy", run="")

    # -- firma: Szenario 045 wie fingerabdruck.py auf Platz 2 ----------------------
    game = fg.Game(db, "PC")
    lauf_firma = game.new_run(2)
    sz.spaet(game, branch=True)
    for room in ("lager", "besprechung", "serverraum"):
        game.build_room(room)
    game.book_ad("online", fg.AD_RUNNING)
    game.start_cert("iso9001")
    game.take_loan(15000, 100, fg.FREE_LOAN)
    for _ in range(8):
        sz.play_day(game)
    game.rename_slot(2, "Nico IT-Service")

    # -- weg: gespielt und geloescht -------------------------------------------
    lauf_weg = game.new_run(3)
    game.set_profile("Weg", sz.LOOK, fg.DIFF_NORMAL)
    for i, task in enumerate(fg.GAME["aufgaben"][60:63]):
        game._log(fg.EV_SOLVED, {"aufgabe": task["id"], "tag": 1, "richtig": True, "geld": 60,
                                 "reputation": {k: 1 for k in fg.AXIS_KEYS}})
    game._log(fg.EV_DAY_END, {"tag": 1, "gehalt": 140})
    game.delete_run(lauf_weg)

    # -- einfach: Platz 3 neu ----------------------------------------------------
    lauf_einfach = game.new_run(3)
    game.set_profile("Anna", sz.LOOK, fg.DIFF_EASY)
    for i, task in enumerate(fg.GAME["aufgaben"][30:54]):
        game._log(fg.EV_SOLVED, {"aufgabe": task["id"], "tag": 1 + i // 3, "richtig": i % 5 != 2, "geld": 60,
                                 "reputation": {k: 1 for k in fg.AXIS_KEYS}})
    for tag in range(1, 9):
        game._log(fg.EV_DAY_END, {"tag": tag, "gehalt": 140})
    game.select_run(lauf_firma)
    del game

    quelle = datenbank(pfad)

    # -- was Python aus seiner Datenbank liest -------------------------------------
    lesen = os.path.join(tmp, "lesen.db")
    shutil.copyfile(pfad, lesen)
    leser = core.DBManager(lesen)
    rows = leser.slot_rows()
    layout = fg.slot_layout(rows, leser.has_legacy_events())
    laeufe = {}
    for lauf in sorted(set(layout["plaetze"].values()) | set(layout["extra"]) | {lauf_weg}):
        events = leser.game_events(lauf)
        state = fg.GameState(events, fg.GAME)
        eintrag = {"anzahl": len(events), "ereignisse": pruefsumme([list(e) for e in events]),
                   "stand": stand(state)}
        if lauf == lauf_firma:
            eintrag["fingerabdruck"] = fingerabdruck(state)
            if eintrag["fingerabdruck"] != SOLL:
                raise SystemExit("Fingerabdruck %s statt %s" % (eintrag["fingerabdruck"], SOLL))
        laeufe[lauf] = eintrag
    spiel = fg.Game(leser, "PC")
    plaetze = spiel.slots()
    gelesen = {
        "belegung": {"plaetze": [[k, v] for k, v in layout["plaetze"].items()],
                     "namen": [[k, v] for k, v in layout["namen"].items()],
                     "extra": layout["extra"], "geloescht": sorted(layout["geloescht"])},
        "anzahl": sorted([k, v] for k, v in leser.event_counts().items()),
        "aktiv": spiel.run,
        "plaetze": plaetze,
        "zeilen_text": [fg.slot_line(p) for p in plaetze],
    }
    bestenliste = [[t, d, a, s, w, x] for t, d, a, s, w, x in leser.records()]

    # -- Dateien und Pythons eigenes Zusammenfuehren -----------------------------
    sicherung = json.loads(gzip.decompress(sich.create_backup(db, "0.63.2", "PC")).decode("utf-8"))
    abgleich = sync.export_local(db)
    # Die Abgleich-Datei ist genau der Teil "daten" der Sicherung (create_backup ruft export_local)
    if json.dumps(abgleich, sort_keys=True) != json.dumps(sicherung["daten"], sort_keys=True):
        raise SystemExit("Abgleich-Datei ist nicht der Teil daten der Sicherung")
    texte = {name: json.dumps(inhalt, ensure_ascii=False, separators=(",", ":"))
             for name, inhalt in (("sicherung", sicherung), ("abgleich", abgleich))}
    ziele = {}
    for name in ("sicherung", "abgleich"):
        zielpfad = os.path.join(tmp, "ziel_%s.db" % name)
        zieldb = core.DBManager(zielpfad)
        if name == "sicherung":
            sich.merge_backup(zieldb, sich.read_backup(gzip.compress(texte[name].encode("utf-8"))))
        else:
            sync.merge_into_local(zieldb, json.loads(texte[name]))
        ziele[name] = spiel_tabellen(zielpfad)
    quelle_spiel = spiel_tabellen(pfad)
    for name, tabellen in ziele.items():
        if tabellen != quelle_spiel:
            raise SystemExit("Python-Zusammenfuehren (%s) verliert Spielzeilen" % name)
        ziele[name] = kurz(tabellen)

    daten = {
        "jetzt_utc": JETZT_UTC,
        "laeufe_namen": {"alt": core.LEGACY_RUN, "firma": lauf_firma, "weg": lauf_weg, "einfach": lauf_einfach},
        "quelle_db": quelle,
        "sicherung": texte["sicherung"],
        "laeufe": laeufe,
        "gelesen": gelesen,
        "bestenliste": bestenliste,
        "ziele": ziele,
    }
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G44 Uebernahme: %d Durchgaenge, %d Ereignisse, %d Bestenliste-Zeilen, Hash %s -> %s" % (
        len(laeufe), len(quelle_spiel["spiel_ereignisse"]), len(bestenliste), SOLL[:8], ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
