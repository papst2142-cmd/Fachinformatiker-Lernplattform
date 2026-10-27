#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G47 (Phase 7, W3 "Spiel-Kern Teil 2": die eigene
Firma ueber viele Tage), gerechnet vom echten Python-Code der Python-Linie
(v0.63.2, eingefroren).

Mit der Spiel-API (fisi_game.Game) werden zwei Durchgaenge mit eigener Firma
gespielt (Normal und Einfach, Personal-Ereignisse an): Gruendung, Bewerber,
Entscheidungen zum Personal, Angebote mit wechselndem Gewinnzuschlag und
falschen Rechnungen, Kundentickets (auch an sich selbst), Ausschreibungen und
Projektteams, Weiterbildung, Coaching, Ausbau, Sonderraeume, Filiale,
Versetzen, Kredite (Paket, frei, Abloesen), Werbung (laufend, einmalig,
kuendigen), Zertifizierungen, die Serverfarm (Start, Team, Aufgaben),
Entlassen und Feierabend, dazu absichtlich falsche Aufrufe. Die Wahl trifft
random.Random mit festem Startwert.

  faelle      - je Aufruf: n (Ereignisse davor), Methode, Argumente und die
                geschriebenen Ereignisse [Art, Daten] (ohne Erfolge) oder der
                Text des ValueError. C# baut den Spielstand aus den ersten n
                Ereignissen und ruft dasselbe auf.
  staende     - nach jedem Feierabend der Firma: n und die Werte vieler
                Funktionen des Kerns (Finanzen, Personal, Kredite, Steuer,
                Werbung, Zertifikate, Markt, Projekte, Serverfarm, Erfolge,
                Bestenliste, Rueckblick, Texte) als SHA-256, die ersten und
                letzten Staende auch voll.
  bestenliste - alle Zeilen der Bestenliste [zeit, durchgang, art,
                schluessel, wert, daten]; ein Stand sieht die Zeilen bis zu
                seiner Zeit (zeit).
  wissen      - der feste Wissensstand des Laufs (knowledge, topic_knowledge,
                learned_projects).

Mit KORREKTUR 83 (Phase 8 W5): journey() rechnet ohne den Fehler der Python-
Linie (Rangliste im Konflikt-Zweig ueberschrieben), siehe korrigiere_83.

Werte wie G44: set -> {"menge": sortiert}, Tupel -> Liste, dict mit anderen
als Text-Schluesseln -> {"paare": [[k, v]]}. Keine Lerndaten; alle Daten-
und Einstellungsordner zeigen in einen leeren Temp-Ordner. Uhr und uuid4
sind fest.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_firma.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_firma.json
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

AUSSEHEN = {"haut": "hell", "frisur": "kurz", "haarfarbe": "braun", "oberteil": "cyan", "kreis": "cyan"}
VOLL = 2          # so viele Staende am Anfang und am Ende voll, sonst nur SHA-256


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


def zeitstempel(nummer):
    return "2026-10-%02d %02d:%02d:%02d" % (1 + nummer // 86400, (nummer // 3600) % 24,
                                            (nummer // 60) % 60, nummer % 60)


def werte(fg, game, state):
    """Werte vieler Funktionen auf einem Spielstand (Name -> Wert)."""
    levels = game.firm_levels()
    topics = game.topic_knowledge()
    records = game.records()
    w = {}

    def auf(name, f):
        w[name] = wert(f())

    auf("firm_day", state.firm_day)
    auf("staff_list", state.staff_list)
    auf("capacity", lambda: state.capacity)
    auf("firm_costs", state.firm_costs)
    auf("firm_summary", lambda: fg.firm_summary(state))
    auf("firm_orders_summary", lambda: fg.firm_orders_summary(state))
    auf("projects_summary", lambda: fg.projects_summary(state))
    auf("finance_days", lambda: fg.finance_days(state))
    auf("finance_totals", lambda: fg.finance_totals(state))
    auf("balance_series", lambda: fg.balance_series(state))
    auf("fixed_costs_text", lambda: fg.fixed_costs_text(state))
    auf("dunning_text", lambda: fg.dunning_text(state))
    auf("tax_debt_text", lambda: fg.tax_debt_text(state))
    auf("tax_next_due", lambda: fg.tax_next_due(state))
    auf("tax_status", lambda: fg.tax_status(state))
    auf("rate_limit", lambda: fg.rate_limit(state))
    auf("credit_check", lambda: fg.credit_check(state))
    auf("loan_packages", lambda: fg.loan_packages(state))
    auf("loan_status", lambda: [fg.loan_status(state, loan) for loan in state.loans.values()])
    auf("marketing_effects", lambda: fg.marketing_effects(state))
    auf("marketing_summary_text", lambda: fg.marketing_summary_text(state))
    auf("ad_status", lambda: fg.ad_status(state))
    auf("cert_status", lambda: fg.cert_status(state))
    auf("certs_summary_text", lambda: fg.certs_summary_text(state))
    auf("cert_advantage", lambda: [fg.cert_advantage(state, cat) for cat in [None] + list(fg.CAT_ORDER)])
    auf("offer_advantage", lambda: [fg.offer_advantage(state, cat=cat) for cat in [None] + list(fg.CAT_ORDER)])
    auf("offer_advantage_parts", lambda: fg.offer_advantage_parts(state))
    auf("market_state", lambda: fg.market_state(state, state.day))
    auf("market_shift", lambda: [fg.market_shift(state, state.day), fg.market_shift(state, state.day, trend=True)])
    auf("lost_to", lambda: fg.lost_to(state))
    auf("rivalry_pressure", lambda: fg.rivalry_pressure(state, state.day))
    auf("pressure_text", lambda: fg.pressure_text(fg.rivalry_pressure(state, state.day)))
    auf("market_status", lambda: fg.market_status(state))
    auf("market_news", lambda: fg.market_news(state))
    auf("own_ticket_limit", lambda: fg.own_ticket_limit(state))
    auf("inquiries", state.inquiries)
    auf("customer_tickets", state.customer_tickets)
    auf("ticket_candidates", lambda: [fg.ticket_candidates(state, t, levels) for t in state.customer_tickets()])
    auf("project_limit", lambda: fg.project_limit(state))
    auf("project_offer_problem", lambda: fg.project_offer_problem(state))
    auf("tenders", state.tenders)
    auf("running_projects", state.running_projects)
    auf("project_candidates", lambda: [fg.project_candidates(state, p, levels) for p in state.running_projects()])
    auf("project_status_text", lambda: [fg.project_status_text(state, p, levels) for p in state.running_projects()])
    auf("project_team_text", lambda: [fg.project_team_text(state, p) for p in state.running_projects()])
    auf("applicants", lambda: fg.applicants(state))
    auf("switchers", lambda: fg.switchers(state))
    auf("switch_news", lambda: fg.switch_news(state))
    auf("personal_news", lambda: fg.personal_news(state))
    auf("open_decisions", state.open_decisions)
    auf("firm_tabs", lambda: fg.firm_tabs(state))
    auf("room_status", lambda: fg.room_status(state))
    auf("firm_building", lambda: fg.firm_building(state))
    auf("branch_possible", lambda: fg.branch_possible(state))
    auf("branch_status", lambda: fg.branch_status(state))
    auf("branch_inquiries", lambda: fg.branch_inquiries(state))
    auf("farm_unlock_missing", lambda: fg.farm_unlock_missing(state))
    auf("farm_unlock_text", lambda: fg.farm_unlock_text(state))
    auf("farm_status", lambda: fg.farm_status(state, topics))
    auf("farm_badge_count", lambda: fg.farm_badge_count(state))
    auf("farm_place_text", lambda: fg.farm_place_text(state))
    auf("farm_phase_list", lambda: fg.farm_phase_list(state) if state.farm else None)
    auf("farm_open_tasks", lambda: fg.farm_open_tasks(state) if state.farm else None)
    auf("farm_candidates", lambda: fg.farm_candidates(state, topics) if state.farm else None)
    auf("staff_money_text", lambda: [fg.staff_money_text(item) for item in state.staff_list()])
    auf("training_text", lambda: [fg.training_text(state, item) for item in state.staff_list()])
    auf("strengths_text", lambda: [fg.strengths_text(item) for item in state.staff_list()])
    auf("training_offer", lambda: [fg.training_offer(state, item["id"], target)
                                   for item in state.staff_list()[:2]
                                   for target in (fg.TOPIC_ORDER[3], fg.CAT_ORDER[1])])
    auf("coaching_offer", lambda: [fg.coaching_offer(state, item["id"]) for item in state.staff_list()[:3]])
    auf("run_metrics", lambda: fg.run_metrics(state, topics))
    auf("new_achievements", lambda: [[rule["id"], level] for rule, level in fg.new_achievements(state, topics)])
    auf("achievement_overview", lambda: fg.achievement_overview(state, topics, records))
    auf("record_board", lambda: fg.record_board(records, state))
    auf("record_rows", lambda: fg.record_rows(state, records))
    auf("journey", lambda: fg.journey(state))
    auf("journey_stats", lambda: fg.journey_stats(state))
    auf("morning_text", lambda: fg.morning_text(state.day, None, state))
    auf("day_end_text", lambda: fg.day_end_text(state.day, None, bool(state.firm)))
    return w


class Lauf:
    """Spielt mit der echten Game-API und schreibt jeden Aufruf mit."""

    def __init__(self, fg, core, tmp, name, seed, schwierigkeit):
        self.fg = fg
        self.zaehler = 0
        self.db = core.DBManager(os.path.join(tmp, name + ".db"))
        self.db._stamp = self.stempel
        self.game = fg.Game(self.db, "PC")
        self.zufall = random.Random(seed)
        self.name = name
        self.schwierigkeit = schwierigkeit
        self.faelle = []
        self.staende = []
        self.geschrieben = None
        cats = list(fg.CAT_ORDER)
        self.wissen = {k: float(20 + (i * 17) % 70) for i, k in enumerate(cats)}
        self.themen = {t: float((i * 23) % 97) for i, t in enumerate(fg.TOPIC_ORDER)}
        self.projekte = {0, 2}
        self.game.knowledge = lambda: dict(self.wissen)
        self.game.topic_knowledge = lambda: dict(self.themen)
        self.game.learned_projects = lambda: set(self.projekte)
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

    def rufe(self, methode, *args):
        fall = {"n": self.anzahl(), "m": methode, "a": copy.deepcopy(list(args))}
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

    def wahl(self, liste):
        return liste[self.zufall.randrange(len(liste))] if liste else None

    def ja(self, anteil):
        return self.zufall.random() < anteil

    # -- Vorlauf: angestellt bei Bitweiche (rohe Ereignisse wie szenario045) ------

    def vorlauf(self):
        fg, game = self.fg, self.game
        self.rufe("set_profile", "Nico", AUSSEHEN, self.schwierigkeit)
        self.rufe("found_firm", "Zu frueh GmbH")
        for i, task in enumerate(fg.GAME["aufgaben"]):
            game._log(fg.EV_SOLVED, {"aufgabe": task["id"], "tag": min(60, 1 + i // 8),
                                     "richtig": True, "geld": 60,
                                     "reputation": {k: 1 for k in fg.AXIS_KEYS}})
        for day in range(1, 61):
            game._log(fg.EV_DAY_END, {"tag": day, "gehalt": 180, "rang": "Senior"})
        game._log(fg.EV_SOLVED, {"aufgabe": "probe-erspartes", "tag": 60, "richtig": False,
                                 "geld": 600000})
        game.reload()
        self.rufe("hire", "niemand")
        self.rufe("found_firm", "   ")
        self.rufe("found_firm", "  Nico   IT-Service und Beratung mit sehr langem Namen  ")
        self.rufe("found_firm", "Zweite Firma")

    # -- ein Arbeitstag der Firma -----------------------------------------------

    def arbeitstag(self, nummer):
        fg, game = self.fg, self.game
        state = game.state
        # Ausbau, Raeume, Zertifizierungen, Filiale (frueh, damit die Serverfarm frei wird)
        if state.next_stage() is not None and (nummer < 6 or self.ja(0.1)):
            self.rufe("expand")
        for item in fg.room_status(game.state):
            if not item.get("gebaut") and (not item.get("problem") or self.ja(0.2)) and self.ja(0.5):
                self.rufe("build_room", item["id"])
        if nummer == 2:
            self.rufe("build_room", "gibt_es_nicht")
        for cert in fg.certificates():
            if self.ja(0.25):
                self.rufe("start_cert", cert["id"])
        if nummer == 3:
            self.rufe("start_cert", "gibt_es_nicht")
        state = game.state
        if not state.branch and fg.branch_possible(state) and self.ja(0.5):
            if self.ja(0.3):
                self.rufe("open_branch", "  ")
            self.rufe("open_branch", "Filiale Lindenau")
        elif state.branch and self.ja(0.2):
            self.rufe("expand_branch")
        # Bewerber
        state = game.state
        for item in fg.applicants(state):
            state = game.state
            if len(state.staff) < state.capacity and self.ja(0.7):
                self.rufe("hire", item["id"])
        # Entscheidungen zum Personal
        if game.state.open_decisions() and self.ja(0.5):
            self.rufe("end_day")
        for decision in game.state.open_decisions():
            if self.ja(0.1):
                self.rufe("decide", decision["id"], "gibt_es_nicht")
            option = self.wahl(decision["optionen"])
            self.rufe("decide", decision["id"], option["id"])
        self.rufe("decide", "gibt_es_nicht", "ja")
        state = game.state
        # Kredite, Werbung
        if self.ja(0.15):
            paket = self.wahl([p["id"] for p in fg.loan_rules()["pakete"]] + ["gibt_es_nicht"])
            self.rufe("take_loan", 0, 0, paket)
        if self.ja(0.1):
            self.rufe("take_loan", self.wahl([500, 5000, 20000, 10 ** 9]), self.wahl([1, 10, 50, 100, 400]), fg.FREE_LOAN)
        laufend = [loan["id"] for loan in game.state.loans.values() if loan["ende"] is None]
        if laufend and self.ja(0.08):
            self.rufe("repay_loan", self.wahl(laufend))
        if nummer == 4:
            self.rufe("repay_loan", "gibt_es_nicht")
        if self.ja(0.2):
            self.rufe("book_ad", self.wahl([f["id"] for f in fg.ad_forms()] + ["gibt_es_nicht"]),
                      self.wahl([fg.AD_RUNNING, fg.AD_ONCE, "falsch"]))
        if self.ja(0.1):
            self.rufe("stop_ad", self.wahl([f["id"] for f in fg.ad_forms()]))
        # Weiterbildung und Coaching
        staff = [item["id"] for item in game.state.staff_list()]
        if staff and self.ja(0.25):
            self.rufe("train", self.wahl(staff), self.wahl(list(fg.TOPIC_ORDER) + list(fg.CAT_ORDER) + ["unbekannt"]))
        if staff and self.ja(0.1):
            self.rufe("coach", self.wahl(staff))
        # Versetzen
        if game.state.branch and staff and self.ja(0.2):
            self.rufe("transfer", self.wahl(staff), self.wahl([fg.SITE_OFFICE, fg.SITE_BRANCH, "mond"]))
        # Kundenanfragen
        markups = list(fg.offer_rules()["zuschlaege"])
        for inquiry in game.state.inquiries():
            if inquiry.get("ergebnis") or self.ja(0.15):
                continue
            markup = self.wahl(markups)
            task = fg.inquiry_task(inquiry, markup)
            answer = fg.find_solution(task) if self.ja(0.8) else {}
            if self.ja(0.05):
                markup = 7
            self.rufe("send_offer", inquiry["id"], markup, answer)
        # Kundentickets
        for ticket in game.state.customer_tickets():
            if ticket.get("an"):
                continue
            options = fg.ticket_candidates(game.state, ticket, game.firm_levels())
            if not options or self.ja(0.1):
                continue
            option = self.wahl(options)
            self.rufe("delegate", ticket["id"], option["an"])
        # Ausschreibungen und Projektteams
        for project in game.state.tenders():
            if project.get("ergebnis") or self.ja(0.4):
                continue
            markup = self.wahl(markups)
            answer = fg.find_solution(fg.project_task(project, markup)) if self.ja(0.85) else {}
            self.rufe("send_project_offer", project["id"], markup, answer)
        for project in game.state.running_projects():
            options = fg.project_candidates(game.state, project, game.firm_levels())
            if options and self.ja(0.4):
                team = [o["an"] for o in options if not o["problem"]][:self.zufall.randrange(1, 4)]
                self.rufe("set_project_team", project["projekt"], team)
            elif options and self.ja(0.3):
                self.rufe("toggle_project_member", project["projekt"], self.wahl(options)["an"])
        # Serverfarm
        state = game.state
        if state.farm_unlocked is not None and state.farm is None and self.ja(0.6):
            self.rufe("farm_start")
        elif state.farm_unlocked is None and self.ja(0.05):
            self.rufe("farm_start")
        if game.state.farm_running():
            options = fg.farm_candidates(game.state, game.topic_knowledge())
            if options and (not game.state.farm["team"] or self.ja(0.2)):
                team = [o["an"] for o in options if not o["problem"]][:self.zufall.randrange(1, 5)]
                self.rufe("farm_set_team", team)
            elif options and self.ja(0.2):
                self.rufe("farm_toggle_member", self.wahl(options)["an"])
            for task in fg.farm_open_tasks(game.state):
                if self.ja(0.25):
                    continue
                markup = None
                if task["typ"] == fg.FARM_OFFER_TYPE:
                    markup = self.wahl(fg.farm_markups())
                    answer = fg.find_solution(fg.farm_check_task(task, markup))
                else:
                    answer = fg.farm_solution(task)
                if self.ja(0.15):
                    answer = {}
                self.rufe("farm_solve", task["id"], answer, self.ja(0.2), markup)
        if nummer == 5:
            self.rufe("farm_solve", "gibt_es_nicht", {}, False, None)
        # Entlassen (selten)
        staff = [item["id"] for item in game.state.staff_list()]
        if len(staff) > 3 and self.ja(0.04):
            self.rufe("fire", self.wahl(staff))
        if nummer == 7:
            self.rufe("fire", "niemand")
        for decision in game.state.open_decisions():
            self.rufe("decide", decision["id"], self.wahl(decision["optionen"])["id"])
        fall = self.rufe("end_day")
        if "e" in fall:
            self.stand()

    def stand(self):
        state = self.game.state
        w = werte(self.fg, self.game, state)
        self.staende.append({"n": self.anzahl(), "zeit": zeitstempel(self.zaehler), "sha256": {k: pruefsumme(v) for k, v in w.items()},
                             "werte": w})

    def spielen(self, tage):
        self.vorlauf()
        for nummer in range(tage):
            self.arbeitstag(nummer)
        for i, eintrag in enumerate(self.staende):
            if VOLL <= i < len(self.staende) - VOLL:
                del eintrag["werte"]
        events = [[t, k, d] for t, k, d in self.db.game_events(self.game.run)]
        bestenliste = [list(z) for z in self.game.records()]
        return {"name": self.name, "wissen": self.wissen, "themen_wissen": self.themen,
                "lernprojekte": sorted(self.projekte), "ereignisse": events, "bestenliste": bestenliste,
                "faelle": self.faelle, "staende": self.staende}


# ---- KORREKTUR 83 (Sammelliste 83, Nico 3a, Weisung P8-7 B5) -------------------
# Python 0.63.2 ueberschreibt in journey() im Konflikt-Zweig die Rangliste "names"
# mit _pair_names(...) (ein dict). Danach gilt in der Rang-Schleife jeder Rang als
# Stufe 0, und jeder Rangwechsel heisst "Zurueckgestuft". Die C#-Linie korrigiert
# das; der Vergleichswert rechnet mit derselben Korrektur. Die Python-Linie bleibt
# unberuehrt: nur in diesem Erzeuger wird journey() aus dem Quelltext neu gebaut,
# mit eigenem Namen fuer das Paar im Konflikt-Zweig. Jede Ersetzung muss genau
# einmal passen, sonst bricht der Erzeuger ab.
KORREKTUR_83 = (
    ('            names = _pair_names(data.get("name_a", ""), data.get("name_b", ""))\n'
     '            add(tag, JOURNEY_FIRM, EV_CONFLICT, "Streit: %s und %s" % (names["a"], names["b"]),\n',
     '            paar = _pair_names(data.get("name_a", ""), data.get("name_b", ""))\n'
     '            add(tag, JOURNEY_FIRM, EV_CONFLICT, "Streit: %s und %s" % (paar["a"], paar["b"]),\n'),
)


def korrigiere_83(fg):
    import inspect
    quelle = inspect.getsource(fg.journey)
    for alt, neu in KORREKTUR_83:
        if quelle.count(alt) != 1:
            raise SystemExit("KORREKTUR 83: Stelle in journey() nicht genau einmal gefunden")
        quelle = quelle.replace(alt, neu)
    raum = {}
    exec(compile(quelle, "journey_korrektur_83", "exec"), fg.__dict__, raum)
    fg.journey = raum["journey"]
# ---- Ende KORREKTUR 83 --------------------------------------------------------


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
    korrigiere_83(fg)   # KORREKTUR 83, siehe oben

    laeufe = [Lauf(fg, core, tmp, "normal", 4711, fg.DIFF_NORMAL).spielen(70),
              Lauf(fg, core, tmp, "einfach", 815, fg.DIFF_EASY).spielen(45)]
    daten = {"laeufe": laeufe}
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    faelle = sum(len(lauf["faelle"]) for lauf in laeufe)
    fehler = sum(1 for lauf in laeufe for f in lauf["faelle"] if "f" in f)
    arten = {}
    for lauf in laeufe:
        for e in lauf["ereignisse"]:
            arten[e[1]] = arten.get(e[1], 0) + 1
    print("G47: %d Aufrufe (%d mit Fehlertext), %d Staende, Ereignisse %s -> %s" % (
        faelle, fehler, sum(len(lauf["staende"]) for lauf in laeufe),
        ", ".join("%s %d" % p for p in sorted(arten.items())), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
