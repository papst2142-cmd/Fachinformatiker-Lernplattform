#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G44 Teil "Aktionen" (Phase 7, W2 "Spiel-Kern
Teil 1"), gerechnet vom echten Python-Code der Python-Linie (v0.63.2,
eingefroren): Mit der Spiel-API (fisi_game.Game) werden zwei Durchgaenge
gespielt, mit Profil, Tickets (loesen, verschieben, Ersatzteil nachbestellen),
Feierabend, Moebel (kaufen, verkaufen, aufstellen, an die Wand ruecken,
drehen, schieben, einpacken, Boden, Rueckgaengig) und Umzug (Miete und Kauf),
dazu absichtlich falsche Aufrufe.

Je Aufruf wird festgehalten: n (Anzahl Ereignisse davor), Methode, Argumente
und entweder die geschriebenen Ereignisse [Art, Daten] (ohne Erfolge, die
kommen mit W3) oder der Text des ValueError. Die C#-Seite baut den Spielstand
aus den ersten n Ereignissen und vergleicht. Dazu je Lauf Stichproben der
Wohnung (placed_furniture, boxed_furniture, furniture_at und home_message auf
einem Raster, sleep_text, rent_text, move_offer, move_texts) und
day_end_money_text fuer jeden Feierabend.

Keine Lerndaten; alle Daten- und Einstellungsordner zeigen in einen leeren
Temp-Ordner. Uhr, uuid4 und die Wahl der Aufrufe (random.Random mit festem
Startwert) sind fest.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_aktionen.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_aktionen.json
"""
import atexit
import copy
import hashlib
import json
import os
import random
import shutil
import sys
import tempfile
import uuid

AUSSEHEN = {"haut": "hell", "haare": "kurz", "haarfarbe": "braun", "oberteil": "hoodie",
            "farbe": "blau", "hose": "jeans", "brille": False, "bart": False,
            "extra": "headset", "kreis": "gelb"}


def pruefsumme(wert):
    text = json.dumps(wert, ensure_ascii=False, sort_keys=False, separators=(",", ":"))
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def zeitstempel(nummer):
    return "2026-10-%02d %02d:%02d:%02d" % (1 + nummer // 86400, (nummer // 3600) % 24,
                                            (nummer // 60) % 60, nummer % 60)


class Lauf:
    """Spielt mit der echten Game-API und schreibt jeden Aufruf mit."""

    def __init__(self, fg, core, tmp, name, seed, miete):
        self.fg = fg
        self.miete = miete
        self.zaehler = 0
        self.db = core.DBManager(os.path.join(tmp, name + ".db"))
        self.db._stamp = self.stempel
        self.game = fg.Game(self.db, "PC")
        self.zufall = random.Random(seed)
        self.name = name
        self.faelle = []
        self.geschrieben = None
        original = self.game._log

        def mitschreiben(kind, data):
            if self.geschrieben is not None and kind != fg.EV_ACHIEVEMENT:
                self.geschrieben.append([kind, copy.deepcopy(data)])
            return original(kind, data)
        self.game._log = mitschreiben

    def stempel(self):
        self.zaehler += 7
        return zeitstempel(self.zaehler), 120

    def anzahl(self):
        return len(self.db.game_events(self.game.run))

    def rufe(self, methode, *args, wissen=None):
        fall = {"n": self.anzahl(), "m": methode, "a": copy.deepcopy(list(args))}
        if wissen is not None:
            fall["wissen"] = dict(wissen)
        self.geschrieben = []
        try:
            getattr(self.game, methode)(*args)
            fall["e"] = self.geschrieben
        except ValueError as fehler:
            if self.geschrieben:
                raise RuntimeError("Fehler nach dem Schreiben: %s %s" % (methode, fehler))
            fall["f"] = str(fehler)
        finally:
            self.geschrieben = None
        self.faelle.append(fall)
        return fall

    # -- Tickets ------------------------------------------------------------

    def arbeitstag(self, tag):
        fg, game = self.fg, self.game
        self.rufe("solve", "gibt_es_nicht", {}, False, wissen=game.topic_knowledge())
        self.rufe("defer", "gibt_es_nicht")
        nummer = 0
        for _ in range(60):
            offen = game.state.open_tickets()
            if not offen:
                break
            if nummer == 1:
                self.rufe("end_day")
            task = offen[0]
            nummer += 1
            prepared = game.state.prepared_task(task)
            if prepared.get("austausch") and not prepared.get("zwischenfall"):
                hw = self.fg.GAME["hardware"]
                teile = [t["id"] for t in hw["teile"] + hw.get("ersatzteile", [])]
                passend = [t for t in teile if fg.part(t)["typ"] == prepared["austausch"]["typ"]]
                self.rufe("order_spare", task["id"], "gibt_es_nicht")
                self.rufe("order_spare", task["id"], [t for t in teile if t not in passend][tag % 7])
                if passend and (tag + nummer) % 3 == 0:
                    self.rufe("order_spare", task["id"], passend[(tag + nummer) % len(passend)])
                    continue
            elif nummer == 1:
                self.rufe("order_spare", task["id"], "gibt_es_nicht")
            if (tag + nummer) % 6 == 5:
                self.rufe("defer", task["id"])
                continue
            found = fg.find_solution(prepared, game.state.available_parts(prepared))
            fall = self.rufe("solve", task["id"], found if found is not None else {}, (tag + nummer) % 4 == 0,
                             wissen=game.topic_knowledge())
            if "f" in fall:
                self.rufe("defer", task["id"])
        if nummer == 0:
            self.rufe("solve", "gibt_es_nicht", {}, False, wissen=game.topic_knowledge())
        self.rufe("end_day")

    # -- Wohnung ------------------------------------------------------------

    def zahl(self, unten, oben):
        z = self.zufall.random()
        wahl = self.zufall.randrange(3)
        wert = unten + (oben - unten) * z
        if wahl == 0:
            return round(wert * 4) / 4          # auf dem Raster
        if wahl == 1:
            return round(wert * 8) / 8          # genau zwischen zwei Rasterpunkten
        return round(wert, 3)

    def moebelrunde(self, tag):
        fg, game, z = self.fg, self.game, self.zufall
        inhalt = fg.GAME["wohnungen"]
        laden = [m["id"] for m in inhalt["moebel"]]
        gebaeude = fg.apartment(game.state.home_id)["gebaeude"]
        breite, hoehe = gebaeude["breite"], gebaeude["hoehe"]
        if tag == 1:
            self.rufe("buy_furniture", "gibt_es_nicht")
            self.rufe("buy_furniture", "matratze")
            for _ in range(3):
                self.rufe("buy_furniture", "fernseher")
            self.rufe("sell_furniture", "gibt_es_nicht")
            self.rufe("rotate_furniture", "gibt_es_nicht")
            self.rufe("nudge_furniture", "gibt_es_nicht", 1, 0)
            self.rufe("place_furniture", "gibt_es_nicht", 1, 1, 0)
            self.rufe("place_furniture_at", "gibt_es_nicht", 1, 1, 0)
            self.rufe("box_furniture", "gibt_es_nicht")
        for _ in range(1 if tag % 3 == 0 else 0):
            self.rufe("buy_furniture", z.choice(laden))
        if tag % 5 == 0:
            self.rufe("move_home", "gibt_es_nicht", False)
            self.rufe("move_home", game.state.home_id, True)
        for home in fg.moves_available(game.state)[:2]:
            if tag % 4 == 0 or game.state.money > home.get("preis", 0) * 0.6:
                fall = self.rufe("move_home", home["id"], self.miete and bool((tag // 4) % 2))
                if "e" in fall:
                    break
        frueher = fg.home_layout(game.state)
        for _ in range(12):
            stuecke = sorted(game.state.furniture)
            if not stuecke:
                break
            stueck = z.choice(stuecke)
            art = z.randrange(10)
            if art <= 2:
                self.rufe("place_furniture", stueck, self.zahl(-0.5, breite), self.zahl(-0.5, hoehe), z.randrange(-1, 6))
            elif art <= 4:
                self.rufe("place_furniture_at", stueck, self.zahl(0, breite), self.zahl(0, hoehe), z.randrange(4))
            elif art == 5:
                self.rufe("rotate_furniture", stueck)
            elif art == 6:
                self.rufe("nudge_furniture", stueck, z.randrange(-1, 2), z.randrange(-1, 2))
            elif art == 7:
                self.rufe("box_furniture", stueck)
            elif art == 8:
                raeume = [r["id"] for r in gebaeude["raeume"]]
                self.rufe("set_floor", z.choice(raeume), z.choice(["holz", "fliesen", "teppich"]),
                          z.choice(["#a0522d", "#dddddd", "#335577"]))
            elif z.randrange(3) == 0:
                self.rufe("sell_furniture", stueck)
        self.rufe("restore_layout", frueher)
        self.rufe("restore_layout", fg.home_layout(game.state))
        self.rufe("restore_layout", {"moebel": {"gibt_es_nicht": [1, 1, 0]}, "boeden": {}})

    # -- Texte und Abfragen der Wohnung ---------------------------------------

    def wohnung(self, state):
        """placed/boxed furniture, furniture_at und home_message auf einem Raster,
        sleep_text, rent_text, moves_available mit move_offer und move_texts."""
        fg = self.fg
        gebaeude = fg.apartment(state.home_id)["gebaeude"]
        punkte = [(x / 4.0, y / 4.0) for x in range(-1, int(gebaeude["breite"] * 4) + 2, 2)
                  for y in range(-1, int(gebaeude["hoehe"] * 4) + 2, 2)]
        bei = []
        for x, y in punkte:
            item = fg.furniture_at(state, x, y)
            titel, text, knoepfe = fg.home_message((x, y), state)
            bei.append([x, y, item["stueck"] if item else None, [titel, text, [list(k) for k in knoepfe]]])
        umzuege = [[home["id"], fg.move_offer(state, home, False), fg.move_offer(state, home, True),
                    list(fg.move_texts(state, home, False)), list(fg.move_texts(state, home, True))]
                   for home in fg.moves_available(state)]
        return {"aufgestellt": fg.placed_furniture(state), "karton": [list(z) for z in fg.boxed_furniture(state)],
                "raster": bei, "schlafen": fg.sleep_text(state), "miete": fg.rent_text(state), "umzuege": umzuege}

    def stichproben(self):
        """Wohnung zu jedem 12. Spielstand; voll bei den ersten vier, sonst als sha256."""
        fg = self.fg
        events = self.db.game_events(self.game.run)
        ergebnis = []
        nummern = sorted({f["n"] for f in self.faelle})
        for i, n in enumerate(nummern[::12] + [len(events)]):
            werte = self.wohnung(fg.GameState(events[:n]))
            eintrag = {"n": n, "sha256": pruefsumme(werte)}
            if i < 4:
                eintrag["werte"] = werte
            ergebnis.append(eintrag)
        geld = [[d, fg.day_end_money_text(d)] for _t, k, d in events if k == fg.EV_DAY_END]
        return ergebnis, geld

    def spielen(self, tage, schwierigkeit):
        self.rufe("set_profile", "   ", AUSSEHEN, schwierigkeit)
        self.rufe("set_profile", "  Nico mit einem sehr langen Namen, der gekürzt wird  ", AUSSEHEN, schwierigkeit)
        self.rufe("set_profile", "Nico", dict(AUSSEHEN, haare="gibt_es_nicht"), None)
        for tag in range(1, tage + 1):
            self.arbeitstag(tag)
            self.moebelrunde(tag)
        events = [[t, k, d] for t, k, d in self.db.game_events(self.game.run)]
        wohnung, geld = self.stichproben()
        return {"name": self.name, "ereignisse": events, "faelle": self.faelle, "wohnung": wohnung,
                "feierabend_geld": geld}


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    atexit.register(shutil.rmtree, tmp, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
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

    laeufe = [Lauf(fg, core, tmp, "normal", 4711, True).spielen(60, fg.DIFF_NORMAL),
              Lauf(fg, core, tmp, "voreinstellung", 815, False).spielen(40, "unbekannt")]
    daten = {"laeufe": laeufe}
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    faelle = sum(len(lauf["faelle"]) for lauf in laeufe)
    fehler = sum(1 for lauf in laeufe for f in lauf["faelle"] if "f" in f)
    arten = sorted({f["m"] for lauf in laeufe for f in lauf["faelle"]})
    print("G44 Aktionen: %d Aufrufe (%d mit Fehlertext), Methoden %s -> %s" % (faelle, fehler, ", ".join(arten), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
