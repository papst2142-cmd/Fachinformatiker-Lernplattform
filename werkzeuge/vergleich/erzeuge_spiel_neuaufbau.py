#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G44 (Phase 7, W2 "Spiel-Kern Teil 1": Spielstand
aus dem Ereignisprotokoll), gerechnet vom echten Python-Code der Python-Linie
(v0.63.2, eingefroren).

  protokolle  - Ereignisprotokolle [zeit, typ, daten]:
                * von Hand: die Faelle aus test_spiel_doppelt.py (D5 als
                  Block und verzahnt, Doppel von Ticket und Feierabend,
                  ungueltige Tagesangaben, Ticket ohne Tag), dazu Lieferung,
                  Lager, Falschlieferung, Ruecksendung, Ersatzteil, Wohnung,
                  Moebel, Profil, Schwierigkeitsgrad, Erfolge, Zwischenfaelle,
                  Reputation an den Grenzen;
                * gespielt mit der echten Spiel-API (fisi_game.Game, eigene
                  Wegwerf-Datenbank, feste Uhr und feste uuid): fruehe Tage,
                  ein gemischter Lauf (richtig, falsch, verschoben, Moebel,
                  Umzug) und ein langer Lauf bis zum hoechsten Rang;
                * W3: zwei Firmen-Laeufe mit dem Spieler von G47
                  (erzeuge_spiel_firma.Lauf: Gruendung, Personal, Ausbau,
                  Filiale, Kredite, Werbung, Zertifizierung, Serverfarm,
                  Projekte), Stichpunkte alle 40 Ereignisse und am Ende.
                Je Protokoll gibt es Stichpunkte (Anzahl der Ereignisse), an
                denen der Spielstand neu aufgebaut wird: GameState(events[:n]).
  stand       - je Stichpunkt alle Felder von vars(GameState) (ohne content,
                _pressure, _topic_base wie GameState.differences; history nur
                Anzahl und SHA-256) und abgeleitete Werte (Tag, Rang, Lager,
                offene Tickets, vorbereitete Auftraege als SHA-256 ...).
                SHA-256 von json.dumps(wert, ensure_ascii=False,
                separators=(",", ":")) ohne Sortieren.

Werte: set -> {"menge": sortiert nach json.dumps(ensure_ascii=True)},
Tupel -> Liste, dict mit anderen als Text-Schluesseln -> {"paare": [[k, v]]}.
Keine Lerndaten; alle Daten- und Einstellungsordner zeigen in einen leeren
Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_neuaufbau.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_neuaufbau.json
"""
import hashlib
import json
import os
import sys
import tempfile
import uuid

HIER = os.path.dirname(os.path.abspath(__file__))

AUSSEHEN = {"haut": "oliv", "frisur": "zopf", "haarfarbe": "rot", "oberteil": "gruen",
            "extra": "headset", "kreis": "gelb"}


def wert(v):
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


def kennungen(tasks):
    return [task["id"] for task in tasks]


def stand(fg, state):
    felder = {k: wert(v) for k, v in vars(state).items()
              if k not in ("content", "_pressure", "_topic_base")}
    # history = die angewendeten Ereignisse selbst: Anzahl und Pruefsumme genuegen
    felder["history"] = {"anzahl": len(state.history), "sha256": pruefsumme(felder["history"])}
    offen = state.open_tickets()
    incident = state.incident_today()
    abgeleitet = {
        "day": state.day, "mean_reputation": state.mean_reputation, "rank": state.rank,
        "day_rank": state.day_rank, "level": state.level, "easy": state.easy,
        "tickets_today": state.tickets_today, "next_rank": wert(state.next_rank()),
        "salary": state.salary, "counted_once": wert(state.counted_once()),
        "stock": wert(state.stock()), "warehouse": wert(state.warehouse()),
        "pool": kennungen(state._pool())[:40],
        "open_tickets": kennungen(offen),
        "todays_tickets": [[task["id"], status] for task, status in state.todays_tickets()],
        "incident_today": incident["id"] if incident else None,
        "wrong_deliveries": kennungen(state.wrong_deliveries()),
        "waiting_for_delivery": kennungen(state.waiting_for_delivery()),
        "quests": {k: kennungen(v) for k, v in state.quests().items()},
        "quests_buero": {k: kennungen(v) for k, v in state.quests(fg.SITE_OFFICE).items()},
        "open_count_by_site": wert(state.open_count_by_site()),
        "open_count_by_room": wert(state.open_count_by_room()),
        "can_end_day": state.can_end_day(), "all_done": state.all_done(),
        "prepared": [pruefsumme(wert(state.prepared_task(task))) for task in offen],
        "available_parts": [state.available_parts(state.prepared_task(task)) for task in offen],
    }
    return {"felder": felder, "abgeleitet": abgeleitet}


def _solved(task, day, money=50, right=True, **extra):
    data = {"aufgabe": task, "geld": money, "richtig": right, "tag": day}
    data.update(extra)
    return ["ticket_erledigt", data]


def _day_end(day, salary=100, rent=0, **extra):
    data = {"tag": day, "gehalt": salary, "miete": rent}
    data.update(extra)
    return ["tag_beendet", data]


def von_hand(fg):
    """(Name, [typ, daten]...) - Zeitstempel kommen beim Schreiben dazu."""
    aufgaben = fg.GAME["aufgaben"]
    faelle = fg.GAME["zwischenfaelle"]
    bestellung = next(t for t in aufgaben if t["typ"] == "bestellung")
    rack = next(t for t in aufgaben if t["typ"] == "rack" and t.get("aus_lager"))
    tausch = next(t for t in aufgaben if t.get("austausch"))
    ram = next(t["id"] for t in fg.GAME["hardware"]["teile"] if t["typ"] == "ram")
    ssd = next(t["id"] for t in fg.GAME["hardware"]["teile"] if t["typ"] == "ssd")
    lieferung = [{"teil": ram, "menge": 1, "ankunft": 2, "haendler": "x"},
                 {"teil": ssd, "menge": 2, "ankunft": 3, "haendler": "y"}]
    d5_start = [_solved("g4-ticket-1", 1), _day_end(1)]
    d5_tag2 = [_solved("g4-ticket-2", 2), _day_end(2)]
    profil = ["profil_gesetzt", {"name": "Nico", "aussehen": AUSSEHEN}]
    return [
        ("d5_block", d5_start + d5_tag2 + d5_tag2),
        ("d5_verzahnt", d5_start + [d5_tag2[0], d5_tag2[0], d5_tag2[1], d5_tag2[1]]),
        ("ticket_falsch_doppelt", [_solved("hw-01", 1, money=-15, right=False,
                                           reputation={"kompetenz": -4})] * 2),
        ("ticket_richtig_doppelt", [_solved("hw-02", 1, money=80, lieferung=lieferung[:1],
                                            aus_lager=["akku_12v7_f2"])] * 2),
        ("feierabend_doppelt_miete", [_day_end(1, 120, 30), _day_end(1, 120, 30), _day_end(2, 120, 30)]),
        ("ohne_tag", [["tag_beendet", {"gehalt": 50}], ["tag_beendet", {"gehalt": 50}],
                      _day_end(2), ["ticket_erledigt", {"aufgabe": "x", "geld": 5, "richtig": True}],
                      ["ticket_erledigt", {"aufgabe": "x", "geld": 5, "richtig": True}]]),
        ("ungueltige_tage", [_day_end("1"), _day_end(0), _day_end(True), _day_end(1.0),
                             _solved("a", "1"), _solved("a", True), _solved("a", 1.0), _solved("", 1),
                             _solved(None, 1), _day_end(-3), _solved("b", -1), _solved("b", -1)]),
        ("gleiche_aufgabe_anderer_tag", [_solved("hw-03", 1), _day_end(1), _solved("hw-03", 2),
                                         _day_end(2), _solved("hw-04", 3), _solved("hw-05", 3),
                                         ["ticket_verschoben", {"aufgabe": "hw-06", "tag": 3,
                                                                "reputation": {"zuverlaessigkeit": -3}}],
                                         ["ticket_verschoben", {"aufgabe": "hw-06", "tag": 3,
                                                                "reputation": {"zuverlaessigkeit": -3}}]]),
        ("lager", [profil, _solved(bestellung["id"], 1, money=-200, lieferung=lieferung),
                   _solved(rack["id"], 1, aus_lager=["akku_12v7_f2", "akku_12v7_f2", "pk_cat6a_2"]),
                   _day_end(1), _day_end(2),
                   _solved(aufgaben[3]["id"], 3, ruecksendung={"fan_120_pwm": 1, "neu_teil": "2"}),
                   _day_end(3)]),
        ("falschlieferung", [profil,
                             _solved(bestellung["id"], 1, money=-150, right=False, fehllieferung=True,
                                     lieferung=lieferung[:1]),
                             _day_end(1), _day_end(2),
                             _solved("falschlieferung:" + bestellung["id"], 3, money=0, right=True,
                                     falschlieferung=bestellung["id"], zwischenfall=True),
                             _day_end(3), _solved(bestellung["id"], 4, money=-120, lieferung=lieferung[1:]),
                             _day_end(4)]),
        ("ersatzteil", [profil, ["ersatzteil_bestellt", {"aufgabe": tausch["id"], "tag": 1, "teil": "fan_120_pwm",
                                                        "geld": -25, "haendler": "h1", "ankunft": 3}],
                        _day_end(1), _day_end(2), _day_end(3),
                        ["ersatzteil_bestellt", {"aufgabe": tausch["id"], "tag": 4, "teil": "fan_120_pwm",
                                                 "geld": "-25", "ankunft": 6.0}],
                        _day_end(4)]),
        ("wohnung", [profil, _day_end(1, 3000),
                     ["moebel_gekauft", {"stueck": "m1", "moebel": "sofa", "geld": -450}],
                     ["moebel_gekauft", {"stueck": "m2", "moebel": "sessel", "geld": -160}],
                     ["einrichtung_gesetzt", {"wohnung": "apartment", "moebel": {"m1": [1, 2, 0]}}],
                     ["moebel_verkauft", {"stueck": "m1", "moebel": "sofa", "geld": 225}],
                     ["moebel_gekauft", {"stueck": "m1", "moebel": "sofa", "geld": -450}],
                     ["umzug", {"wohnung": "zweizimmer", "geld": -600, "miete": 40, "kaution": 600}],
                     ["einrichtung_gesetzt", {"wohnung": "zweizimmer", "moebel": {}}],
                     _day_end(2, 100, 40), ["umzug", {"geld": 600, "kaution_zurueck": 600}],
                     ["moebel_verkauft", {"stueck": "gibtsnicht", "geld": 0}], _day_end(3)]),
        ("profil_schwierigkeit_erfolge", [
            ["schwierigkeit_gewaehlt", {"stufe": "schwer"}], ["schwierigkeit_gewaehlt", {"stufe": "einfach"}],
            ["schwierigkeit_gewaehlt", {"stufe": "normal"}],
            ["profil_gesetzt", {"name": "A", "aussehen": {"haut": "blau", "frisur": "glatze"}}],
            ["profil_gesetzt", {"aussehen": None}], profil,
            ["erfolg_freigeschaltet", {"erfolg": "tickets", "stufe": 1, "tag": 1}],
            ["erfolg_freigeschaltet", {"erfolg": "tickets", "stufe": 1, "tag": 2}],
            ["erfolg_freigeschaltet", {"erfolg": "tickets", "stufe": "2"}],
            ["erfolg_freigeschaltet", {"erfolg": "", "stufe": 3}],
            ["erfolg_freigeschaltet", {"erfolg": "geld", "stufe": 0}],
            ["erfolg_freigeschaltet", {"erfolg": "geld", "stufe": 2.7}],
            ["unbekannt", {"tag": 5, "geld": 1000}], _day_end(1)]),
        ("zwischenfaelle", [profil, _solved(faelle[0]["id"], 2, zwischenfall=True),
                            ["ticket_verschoben", {"aufgabe": faelle[1]["id"], "tag": 3, "zwischenfall": True,
                                                   "reputation": {"zuverlaessigkeit": -5}}],
                            _solved(faelle[2]["id"], 1, zwischenfall=True), _day_end(1),
                            _solved("gibtsnicht", 4, zwischenfall=True)]),
        ("reputation_grenzen", [profil] + [_solved("r%d" % i, 1 + i // 4, money=10 * i,
                                                    reputation={"fachkompetenz": 30, "sicherheit": -40,
                                                                "zuverlaessigkeit": 7.9, "kundenzufriedenheit": "3",
                                                                "gibtsnicht": 99})
                                           for i in range(12)] + [_day_end(d, 90) for d in range(1, 70)]),
    ]


def zeitstempel(nummer):
    return "2026-10-%02d %02d:%02d:%02d" % (1 + nummer // 86400, (nummer // 3600) % 24,
                                            (nummer // 60) % 60, nummer % 60)


def gespielt(fg, core, tmp, name, tage, muster, schwierigkeit="normal", wohnen=False):
    """Mit der echten Spiel-API spielen; muster: je Ticket r (richtig), f (falsch), v (verschoben)."""
    zaehler = [0]

    def stempel():
        zaehler[0] += 7
        return zeitstempel(zaehler[0]), 120

    db = core.DBManager(os.path.join(tmp, name + ".db"))
    db._stamp = stempel
    game = fg.Game(db, "PC")
    game.set_profile("Nico", AUSSEHEN, schwierigkeit)
    nummer = 0
    stichpunkte = []
    for tag in range(1, tage + 1):
        for _ in range(60):
            offen = game.state.open_tickets()
            if not offen:
                break
            task = offen[0]
            art = muster[nummer % len(muster)]
            nummer += 1
            if art == "v":
                game.defer(task["id"])
                continue
            answer = {}
            if art == "r":
                prepared = game.state.prepared_task(task)
                found = fg.find_solution(prepared, game.state.available_parts(prepared))
                if found is not None:
                    answer = found
            try:
                game.solve(task["id"], answer, nummer % 5 == 0)
            except ValueError:
                game.defer(task["id"])
        if wohnen and tag in (6, 14):
            for item in ("sessel", "sitzsack", "couchtisch"):
                if game.state.money > 400:
                    game.buy_furniture(item)
        if wohnen and tag == 20 and game.state.furniture:
            game.sell_furniture(sorted(game.state.furniture)[0])
        if wohnen and tag == 25:
            try:
                game.move_home("zweizimmer", rent=True)
            except ValueError:
                pass
        game.end_day()
        if tag % 5 == 0 or tag in (1, 2, 3):
            stichpunkte.append(len(game.db.game_events(game.run)))
    events = game.db.game_events(game.run)
    return events, stichpunkte


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
        return uuid.UUID(int=zufall[0] * 7919)
    uuid.uuid4 = feste_uuid
    import fisi_core as core
    import fisi_game as fg

    protokolle = []
    for name, eintraege in von_hand(fg):
        events = [[zeitstempel(i * 61), art, daten] for i, (art, daten) in enumerate(eintraege)]
        protokolle.append((name, events, list(range(0, len(events) + 1))))
    for name, tage, muster, schwierigkeit, wohnen in (
            ("frueh", 12, "r", "normal", False),
            ("gemischt", 45, "rrfrvrrf", "einfach", True),
            ("lang", 130, "rrrrrrrrrf", "normal", True)):
        events, stich = gespielt(fg, core, tmp, name, tage, muster, schwierigkeit, wohnen)
        protokolle.append((name, [[t, k, d] for t, k, d in events], [0, 1, 2] + stich))
    sys.path.insert(1, HIER)
    import erzeuge_spiel_firma as firma
    for name, seed, schwierigkeit, tage in (("firma", 4711, fg.DIFF_NORMAL, 30),
                                            ("firma_einfach", 815, fg.DIFF_EASY, 20)):
        lauf = firma.Lauf(fg, core, tmp, name, seed, schwierigkeit)
        events = lauf.spielen(tage)["ereignisse"]
        protokolle.append((name, events, sorted(set(range(0, len(events), 40)) | {len(events)})))
    daten = {"protokolle": []}
    for name, events, stich in protokolle:
        staende = []
        for n in stich:
            state = fg.GameState([tuple(e) for e in events[:n]])
            staende.append({"n": n, "stand": stand(fg, state)})
        daten["protokolle"].append({"name": name, "ereignisse": events, "staende": staende})
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G44: %d Protokolle, %d Ereignisse, %d Staende -> %s"
          % (len(protokolle), sum(len(p[1]) for p in protokolle),
             sum(len(p[2]) for p in protokolle), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
