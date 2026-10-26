#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G13 (Phase 1, K7 Teil "Dashboard-Werte"): alle
Zahlen und Texte, die das Dashboard (PC: app_gui.DashboardView, Handy:
mobile/src/main.DashboardScreen) aus der Lernstand-Datenbank zeigt, gerechnet
vom echten Python-Code der Python-Linie.

Gerechnet wird mit den Funktionen, die die beiden Oberflaechen aufrufen:
  fisi_core.DBManager: distinct_cards_learned, distinct_quiz_questions,
    distinct_ap1, distinct_scenarios, quiz_success_rate, streak,
    daily_counts, category_coverage, category_stats, recent_activities,
    month_activity, theme_progress, topic_daily, topic_stats;
  fisi_core: content_totals, theme_totals, ap1_theme_totals, plural, de_int;
  fisi_lernen: goal_settings, DailyGoal, ReviewPlan (Handy),
    ReviewIndex (PC; beide werden gerechnet und muessen gleich sein),
    due_text, dash_quote_text, DASH_*, LEARN_CHART_SERIES;
  fisi_game.topic_knowledge (Reinzoom "Wissensstand je Thema");
  fisi_hilfe.greeting (Banner "Hallo <Name>");
  mobile/src/ui.keep_last_words und mobile/src/main.german_time werden mit
    dem Modul ast aus den Quelldateien gelesen und ausgefuehrt (ui.py und
    main.py importieren flet und lassen sich hier nicht laden).
Die GUI-Klassen selbst importieren customtkinter bzw. flet und werden nicht
geladen. Die wenigen Formatier-Ausdruecke, die direkt in den Klassen stehen
(z. B. "%d %%" % round(rate), "%s\\n%d %% richtig", Kuerzung der
Aktivitaeten am PC auf 34 Zeichen, "%d\\u00a0%%" in MiniRing, ThemeTimeline,
ThemeProgress und den Handy-Ringen), stehen unten in werte() woertlich wie
in app_gui.py, fisi_widgets.py, mobile/src/main.py und mobile/src/ui.py
(Stand v0.63.2); jede Stelle ist mit ihrer Quelle bezeichnet.

Stichtag: Python liest "heute" mit datetime.date.today() (Ortszeit; die
Zeitstempel stehen schon in Ortszeit in der Datenbank). Hier wird
datetime.date in fisi_core und fisi_lernen gezielt durch eine Unterklasse
ersetzt, deren today() den festen Stichtag des Falls liefert (monkeypatch,
siehe FesterTag). Eine Zeitzone wird nicht gerechnet; C# bekommt denselben
Stichtag als DateOnly.

Datenbanken (nur Testdaten, Auflage A3):
  leer       - frisch von DBManager angelegt
  teilweise  - nur Karteikarten-Ereignisse (DBManager.log_card)
  voll       - Kopie von tests/Vergleich/testdaten.db (alle Tabellen,
               Pruefwerte mit Sonderwerten wie correct = 8)
  gemischt   - alle Arten von Ereignissen mit echten Inhalten
               (log_card, log_quiz_answer, log_scenario, log_ap1,
               log_project, log_trainer, save_test_result), ueber mehrere
               Wochen, mit Altbestand vor SR_START
  ohne_tabelle - gemischt, danach Tabelle quiz_answers entfernt (Python
               liefert dann fuer jede scheiternde Abfrage den Vorgabewert)
Die Zeilen von teilweise und gemischt werden mit ausgegeben (Abschnitt
"datenbanken"), damit der C#-Test dieselbe Datenbank aufbauen kann.
Zeitstempel und uid beim Schreiben sind fest (DBManager._stamp/_uid ersetzt),
die Auswahl der Inhalte kommt aus random.Random mit festem Startwert.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_dashboard.py <pfad_fisi_lernplattform> tests/Vergleich/testdaten.db tests/Vergleich/dashboard.json
"""
import ast
import atexit
import calendar
import datetime
import json
import os
import random
import shutil
import sqlite3
import sys
import tempfile
import types

# Tabellen, die das Dashboard liest
TABELLEN = ("card_events", "quiz_answers", "scenario_events", "project_events",
            "ap1_events", "trainer_aufgaben", "test_results")

TAGE = 14            # DashboardView.DAYS / DashboardScreen.DAYS
AKTIVITAETEN = 7     # recent_activities(7) am PC, DashboardScreen.ACTIVITIES

_HEUTE = [datetime.date(2026, 10, 20)]


class FesterTag(datetime.date):
    """datetime.date mit festem today() (Stichtag des Falls)."""

    @classmethod
    def today(cls):
        tag = _HEUTE[0]
        return cls(tag.year, tag.month, tag.day)


def setze_heute(tag):
    _HEUTE[0] = tag


def ersetze_datum(modul):
    """modul.datetime durch ein Modul ersetzen, dessen date.today() fest ist."""
    ersatz = types.ModuleType("datetime")
    for name in dir(datetime):
        if not name.startswith("__"):
            setattr(ersatz, name, getattr(datetime, name))
    ersatz.date = FesterTag
    modul.datetime = ersatz


def lies_funktion(pfad, name, ns):
    """Eine reine Funktion aus einer Quelldatei per ast lesen und ausfuehren."""
    with open(pfad, encoding="utf-8") as handle:
        baum = ast.parse(handle.read(), pfad)
    for knoten in baum.body:
        if isinstance(knoten, ast.FunctionDef) and knoten.name == name:
            modul = ast.Module(body=[knoten], type_ignores=[])
            exec(compile(modul, pfad, "exec"), ns)
            return ns[name]
    raise SystemExit("Funktion %s nicht gefunden in %s" % (name, pfad))


# Feste Texte der Kacheln. Sie stehen direkt in den GUI-Klassen; hier wird
# per ast geprueft, dass jeder Text dort als Literal vorkommt (PC:
# app_gui.DashboardView, Handy: mobile/src/main.DashboardScreen). Ausgegeben
# wird je Geraet nur, was dort vorkommt.
KARTENTEXTE = {
    "heute_titel": "Heute",
    "heute_unter": "Tagesziel und Wiederholung",
    "wiederholen": "Jetzt wiederholen",
    "verlauf_titel": "Lernverlauf",
    "verlauf_unter": "letzte %d Tage",
    "abdeckung_titel": "Abdeckung",
    "abdeckung_unter": "Material",
    "fach_titel": "Fortschritt je Fachbereich",
    "fach_unter": "Auswahl zeigt die Themen",
    "fach_detail": "%s\n%d %% richtig",
    "fach_detail_leer": "noch nicht\nbearbeitet",
    "aktivitaeten_titel": "Aktivitäten",
    "aktivitaeten_unter": "zuletzt",
    "aktivitaeten_leer_pc": "Noch keine Aktivitäten.\nStarte mit den Karteikarten\n"
                            "oder dem Prüfungstrainer.",
    "aktivitaeten_leer_handy": "Noch keine Aktivitäten. Starte mit den Karteikarten oder dem "
                               "Prüfungstrainer.",
    "kalender_titel": "Lerntage",
    "kalender_unter": "Monatsübersicht",
    "ap1_titel": "AP1 Prüfungsthemen",
    "ap1_unter": "bearbeitete Grundlagenaufgaben",
    "ap2_titel": "AP2 Prüfungsthemen",
    "ap2_unter": "bearbeitete Szenarien",
    "reinzoom_titel": "Themen · %s",
    "reinzoom_unter": "Aktivität und Wissen",
    "reinzoom_aktivitaet": "Aktivität je Thema (letzte %d Tage)",
    "reinzoom_wissen": "Wissensstand je Thema (zählt für die Aufträge im Spiel)",
    "reinzoom_schliessen": "Schließen",
    "reinzoom_notiz": "Wissen %d %%  ·  %s",
    "reinzoom_leer": "noch nicht bearbeitet",
    "banner_ohne_name": "Dein Lernstand",
    "lernserie_pc": "Lernserie: %s   ·   ",
    "lernserie_handy": "Lernserie: %s  ·  ",
}


def klassentexte(pfad, klasse):
    """Alle Text-Literale in einer Klasse (per ast, ohne Import)."""
    with open(pfad, encoding="utf-8") as handle:
        baum = ast.parse(handle.read(), pfad)
    for knoten in baum.body:
        if isinstance(knoten, ast.ClassDef) and knoten.name == klasse:
            return {k.value for k in ast.walk(knoten)
                    if isinstance(k, ast.Constant) and isinstance(k.value, str)}
    raise SystemExit("Klasse %s nicht gefunden in %s" % (klasse, pfad))


def kartentexte(repo):
    pc = klassentexte(os.path.join(repo, "app_gui.py"), "DashboardView")
    handy = klassentexte(os.path.join(repo, "mobile", "src", "main.py"), "DashboardScreen")
    ergebnis = {"pc": {}, "handy": {}}
    for name, text in KARTENTEXTE.items():
        if text in pc:
            ergebnis["pc"][name] = text
        if text in handy:
            ergebnis["handy"][name] = text
        if text not in pc and text not in handy:
            raise SystemExit("Text %r steht weder am PC noch am Handy" % text)
    return ergebnis


def zeilen(pfad):
    """Zeilen der Dashboard-Tabellen (fuer den Aufbau im C#-Test)."""
    conn = sqlite3.connect(pfad)
    try:
        ergebnis = {}
        for tabelle in TABELLEN:
            cur = conn.execute("SELECT * FROM %s ORDER BY id" % tabelle)
            spalten = [d[0] for d in cur.description]
            daten = [list(row) for row in cur.fetchall()]
            if daten:
                ergebnis[tabelle] = {"spalten": spalten, "zeilen": daten}
        return ergebnis
    finally:
        conn.close()


def zustand(pfad, kaputt):
    if kaputt:
        return "Kaputt"
    conn = sqlite3.connect(pfad)
    try:
        leer = all(conn.execute("SELECT COUNT(*) FROM %s" % t).fetchone()[0] == 0
                   for t in TABELLEN)
    finally:
        conn.close()
    return "Leer" if leer else "Daten"


def baue_teilweise(core, pfad, rng, schreibe):
    db = core.DBManager(pfad)
    karten = rng.sample(core.KARTEIKARTEN, 12)
    plan = [
        ("2026-10-12 09:00:00", 0, True), ("2026-10-12 09:01:00", 1, None),
        ("2026-10-13 18:30:00", 2, False), ("2026-10-13 18:31:00", 0, True),
        ("2026-10-18 07:00:00", 3, True), ("2026-10-18 07:00:00", 4, None),
        ("2026-10-19 21:59:59", 5, False), ("2026-10-19 22:00:00", 6, True),
        ("2026-10-19 22:05:00", 7, True), ("2026-10-20 06:00:00", 8, True),
        ("2026-10-20 06:10:00", 9, False), ("2026-10-20 06:20:00", 10, None),
        ("2026-10-20 06:30:00", 11, True), ("2026-10-20 06:40:00", 2, True),
    ]
    for zeit, index, richtig in plan:
        karte = karten[index]
        schreibe(db, zeit, lambda: db.log_card(karte["cat"], karte["q"], "lernen", richtig))
    return db


def baue_gemischt(core, pfad, rng, schreibe):
    db = core.DBManager(pfad)
    karten = rng.sample(core.KARTEIKARTEN, 60)
    # Fragen aus der Datei und erzeugte Aufgaben (Ende der Liste)
    fragen = rng.sample(core.QUIZ_QUESTIONS[:1500], 40) + rng.sample(core.QUIZ_QUESTIONS[-400:], 12)

    def tag(text):
        return datetime.date.fromisoformat(text)

    # Altbestand: vor SR_START (2026-10-01) bearbeitet
    for nummer, karte in enumerate(karten[:34]):
        zeit = "2026-09-%02d %02d:%02d:00" % (18 + nummer % 12, 8 + nummer % 10, nummer)
        werte = [True, False, None, True, True][nummer % 5]
        schreibe(db, zeit, lambda: db.log_card(karte["cat"], karte["q"], "lernen", werte))
        if nummer % 3 == 0:
            schreibe(db, zeit[:-2] + "30",
                     lambda: db.log_card(karte["cat"], karte["q"], "lernen", nummer % 2 == 0))
    for nummer, frage in enumerate(fragen[:26]):
        zeit = "2026-09-%02d %02d:%02d:10" % (19 + nummer % 11, 9 + nummer % 9, nummer)
        schreibe(db, zeit, lambda: db.log_quiz_answer(frage["cat"], frage["q"], nummer % 4 != 1))
    # Ein Teil des Altbestands wird ab SR_START wieder bearbeitet (auch heute)
    for nummer, karte in enumerate(karten[:8]):
        zeit = "2026-10-%02d 12:%02d:00" % (14 + nummer % 7, nummer)
        schreibe(db, zeit, lambda: db.log_card(karte["cat"], karte["q"], "lernen", nummer % 3 != 2))
    for nummer, frage in enumerate(fragen[:5]):
        zeit = "2026-10-%02d 13:%02d:00" % (16 + nummer % 5, nummer)
        schreibe(db, zeit, lambda: db.log_quiz_answer(frage["cat"], frage["q"], nummer != 3))
    # Neu ab SR_START: Folgen mit Stufen, Fehlern und Faelligkeiten
    neu_tage = ["2026-10-02", "2026-10-05", "2026-10-09", "2026-10-15", "2026-10-16",
                "2026-10-17", "2026-10-18", "2026-10-19", "2026-10-20"]
    for nummer, karte in enumerate(karten[34:]):
        start = nummer % len(neu_tage)
        for schritt in range(1 + nummer % 4):
            index = min(len(neu_tage) - 1, start + schritt * 2)
            zeit = "%s %02d:%02d:%02d" % (neu_tage[index], 7 + nummer % 12, nummer, schritt)
            richtig = None if (nummer + schritt) % 7 == 0 else (nummer + schritt) % 5 != 0
            schreibe(db, zeit, lambda: db.log_card(karte["cat"], karte["q"], "lernen", richtig))
    for nummer, frage in enumerate(fragen[26:]):
        start = (nummer * 2) % len(neu_tage)
        for schritt in range(1 + nummer % 3):
            index = min(len(neu_tage) - 1, start + schritt * 3)
            zeit = "%s %02d:%02d:%02d" % (neu_tage[index], 8 + nummer % 11, nummer, 30 + schritt)
            schreibe(db, zeit, lambda: db.log_quiz_answer(frage["cat"], frage["q"],
                                                          (nummer + schritt) % 4 != 0))
    # Gleicher Zeitpunkt in zwei Tabellen
    schreibe(db, "2026-10-20 06:00:00", lambda: db.log_card(
        karten[40]["cat"], karten[40]["q"], "lernen", True))
    schreibe(db, "2026-10-20 06:00:00", lambda: db.log_quiz_answer(
        fragen[30]["cat"], fragen[30]["q"], False))
    # Eintrag mit spaeterem Datum (Uhr vorgestellt): zaehlt nicht fuer die Serie
    schreibe(db, "2026-10-22 10:00:00", lambda: db.log_quiz_answer(
        fragen[31]["cat"], fragen[31]["q"], True))
    # Szenarien, Testprojekte, Trainer, Sessions
    ap2 = core.SZENARIEN
    for zeit, index, richtig in [("2026-10-09 10:00:00", 0, None), ("2026-10-09 10:20:00", 0, True),
                                 ("2026-10-16 19:00:00", 7, False), ("2026-10-19 19:30:00", 40, True),
                                 ("2026-10-20 07:30:00", 101, None)]:
        szenario = ap2[index]
        schreibe(db, zeit, lambda: db.log_scenario(index, szenario["title"], szenario["theme"], richtig))
    # Themen ohne eigenen Block (theme_block) und das lange AP1-Thema
    for index, szenario in enumerate(ap2):
        if szenario["theme"] in ("Virtualisierung", "Projektmanagement"):
            schreibe(db, "2026-10-18 20:%02d:00" % (index % 60), lambda: db.log_scenario(
                index, szenario["title"], szenario["theme"], True))
            break
    ap1 = core.AP1_SZENARIEN
    lang = [i for i, s in enumerate(ap1) if len(s["theme"]) > 34][:1]
    for zeit, index, richtig in [("2026-10-05 11:00:00", 3, True), ("2026-10-15 11:30:00", 12, None),
                                 ("2026-10-17 11:45:00", 12, False)] + \
            [("2026-10-20 08:15:00", i, True) for i in lang]:
        szenario = ap1[index]
        schreibe(db, zeit, lambda: db.log_ap1(index, szenario["title"], szenario["theme"], richtig))
    projekte = core.PROJEKTARBEITEN
    for zeit, index, richtig in [("2026-10-10 15:00:00", 1, None), ("2026-10-19 15:30:00", 2, True)]:
        projekt = projekte[index]
        schreibe(db, zeit, lambda: db.log_project(index, projekt["title"], projekt["cat"], richtig))
    for zeit, richtig in [("2026-10-17 17:00:00", True), ("2026-10-20 17:05:00", False),
                          ("2026-10-20 17:06:00", True)]:
        schreibe(db, zeit, lambda: db.log_trainer("subnetz", 2, richtig))
    # Notizen mit Zeichen ausserhalb der BMP: genau 34 Zeichen (bleibt ganz)
    # und laenger (am PC auf 32 Zeichen + "…" gekuerzt)
    for zeit, punkte, note in [("2026-10-11 20:00:00", 7, "Prüfungstrainer · 10 Fragen"),
                               ("2026-10-20 20:00:00", 18, "😀" * 4 + "x" * 30),
                               ("2026-10-20 20:01:00", 19,
                                "Lange 😀 Notiz zur Session mit Umlauten äöü und 😀 am Ende")]:
        schreibe(db, zeit, lambda: db.save_test_result(punkte, 20, punkte * 5.0, note, 300))
    return db


def werte(core, lernen, game, hilfe, keep_last_words, german_time, db, heute,
          einstellungen, name, kaputt, pfad):
    """Alle Werte des Dashboards fuer einen Fall (PC und Handy)."""
    setze_heute(heute)
    settings = lernen.goal_settings(einstellungen)
    plural, de_int = core.plural, core.de_int
    totals = core.content_totals()
    total_content = sum(totals.values())
    total_cards, total_quiz = len(core.KARTEIKARTEN), len(core.QUIZ_QUESTIONS)
    total_ap1, total_scen = len(core.AP1_SZENARIEN), len(core.SZENARIEN)

    learned_cards = db.distinct_cards_learned()
    quiz_distinct = db.distinct_quiz_questions()
    ap1_done = db.distinct_ap1()
    scen_done = db.distinct_scenarios()
    rate, correct, answered = db.quiz_success_rate()
    learned = learned_cards + quiz_distinct

    # Kachel "Heute" (DashboardView._refresh_today / DashboardScreen._refresh_today)
    goal = lernen.DailyGoal.from_db(db, settings["ziel_anzahl"])
    plan = lernen.ReviewPlan.from_db(db)                  # Handy
    plan_pc = lernen.ReviewIndex(db).plan()               # PC (ab 0.63.2)
    assert plan.due == plan_pc.due and plan.legacy_waiting == plan_pc.legacy_waiting
    assert lernen.due_text(plan) == lernen.due_text(plan_pc)

    # Banner (refresh): PC "   ·   ", Handy "  ·  " und keep_last_words
    streak_count = db.streak()
    streak_pc = ("Lernserie: %s   ·   " % plural(streak_count, "Tag", "Tage")
                 if settings["serie_an"] else "")
    streak_handy = ("Lernserie: %s  ·  " % plural(streak_count, "Tag", "Tage")
                    if settings["serie_an"] else "")
    text_pc = (("%s" + lernen.DASH_LEARNED + "   ·   " + lernen.DASH_RATE)
               % (streak_pc, de_int(learned), de_int(total_content), round(rate)))
    text_handy = (("%s" + lernen.DASH_LEARNED + "  ·  " + lernen.DASH_RATE)
                  % (streak_handy, de_int(learned), de_int(total_content), round(rate)))

    def kennzahl(titel, wert, gesamt, unter):
        return {"titel": titel, "wert": wert, "gesamt": gesamt,
                "anteil": wert / max(1, gesamt), "gross": de_int(wert), "unter": unter % de_int(gesamt),
                "balken_prozent": wert / max(1, gesamt) * 100,
                "balken_text": "%s / %s" % (de_int(wert), de_int(gesamt))}

    daily = db.daily_counts(TAGE)
    coverage = db.category_coverage(totals)
    stats = db.category_stats()
    fach = []
    for category in core.CATEGORIES:
        data = stats.get(category, {"answered": 0, "correct": 0})
        if data["answered"]:
            detail = "%s\n%d %% richtig" % (plural(data["answered"], "Antwort", "Antworten"),
                                             round(data["correct"] / data["answered"] * 100))
        else:
            detail = "noch nicht\nbearbeitet"
        share = coverage.get(category, 0.0)
        fach.append({"name": category, "kurz": core.CATEGORY_SHORT[category], "abdeckung": share,
                     # MiniRing (PC, begrenzt 0..100) und Handy-Ring
                     "ring_text": "%d %%" % round(max(0.0, min(100.0, share))),
                     "ring_text_handy": "%d %%" % round(share),
                     "antworten": data["answered"], "richtig": data["correct"], "detail": detail})

    aktivitaeten = []
    for timestamp, kind, detail, extra in db.recent_activities(AKTIVITAETEN):
        # PC: DashboardView._activity_row, Handy: DashboardScreen.refresh
        short_detail = detail if len(str(detail)) <= 34 else str(detail)[:32] + "…"
        aktivitaeten.append({
            "zeit": timestamp, "art": kind, "detail": detail, "extra": extra,
            "pc1": "%s · %s" % (kind, short_detail),
            "pc2": "%s  ·  %s" % (timestamp[:16].replace("-", "."), extra),
            "handy1": "%s · %s" % (kind, detail),
            "handy2": "%s  ·  %s" % (german_time(timestamp), extra)})

    namen = ["Januar", "Februar", "März", "April", "Mai", "Juni", "Juli",
             "August", "September", "Oktober", "November", "Dezember"]
    kalender = []
    for verschiebung in (0, -1, 1):
        monat = heute.month + verschiebung
        jahr = heute.year + (monat - 1) // 12
        monat = (monat - 1) % 12 + 1
        kalender.append({"jahr": jahr, "monat": monat, "titel": "%s %d" % (namen[monat - 1], jahr),
                         "lerntage": sorted(db.month_activity(jahr, monat)),
                         "wochen": calendar.Calendar(firstweekday=0).monthdayscalendar(jahr, monat)})

    def themen(progress, names):
        return [{"name": n, "prozent": progress.get(n, 0.0),
                 "ring_text": "%d %%" % round(progress.get(n, 0.0))} for n in names]

    progress = db.theme_progress(core.theme_totals())
    progress_ap1 = db.theme_progress(core.ap1_theme_totals(), themes=core.AP1_THEMES,
                                     table="ap1_events")

    levels = game.topic_knowledge(db)
    topic_stats = db.topic_stats()
    reinzoom = []
    for category in core.CATEGORIES:
        matrix = db.topic_daily(category, TAGE)
        eintraege = []
        for topic in core.TOPICS[category]:
            answered_t = topic_stats[topic]["answered"]
            eintraege.append({
                "id": topic, "name": core.TOPIC_NAME[topic], "kurz": core.TOPIC_SHORT[topic],
                "heatmap": matrix[topic], "wissen": levels[topic], "antworten": answered_t,
                "richtig": topic_stats[topic]["correct"],
                "notiz": ("Wissen %d %%  ·  %s" % (levels[topic],
                                                    plural(answered_t, "Antwort", "Antworten"))
                          if answered_t else "noch nicht bearbeitet")})
        reinzoom.append({"fachbereich": category, "titel": "Themen · %s" % category,
                         "themen": eintraege})

    return {
        "zustand": zustand(pfad, kaputt),
        "banner": {"titel": hilfe.greeting(name) or "Dein Lernstand", "text_pc": text_pc,
                   "text_handy": keep_last_words(text_handy),
                   "gross": "%d %%" % round(learned / max(1, total_content) * 100),
                   "gross_unter": lernen.DASH_TOTAL},
        "bearbeitet": learned, "inhalte_gesamt": total_content, "lernserie": streak_count,
        "tagesziel": {"sichtbar": settings["ziel_an"], "erledigt": goal.done, "ziel": goal.target,
                      "erreicht": goal.reached, "anteil": goal.fraction, "text": goal.text(),
                      "ring_text": "%d %%" % round(goal.fraction * 100)},
        "faellig": {"karten": plan.count(core.SRC_CARD), "fragen": plan.count(core.SRC_QUIZ),
                    "text": lernen.due_text(plan), "wiederholen": plan.count() > 0,
                    "altbestand_wartet": plan.legacy_waiting,
                    "erste_quelle": plan.first_source()},
        "kennzahlen": [kennzahl(lernen.DASH_RING_CARDS, learned_cards, total_cards, lernen.DASH_CARDS_SUB),
                       kennzahl(lernen.DASH_RING_QUIZ, quiz_distinct, total_quiz, lernen.DASH_QUIZ_SUB),
                       kennzahl(lernen.DASH_RING_AP1, ap1_done, total_ap1, lernen.DASH_SCEN_SUB),
                       kennzahl(lernen.DASH_RING_AP2, scen_done, total_scen, lernen.DASH_SCEN_SUB)],
        "quote": {"wert": rate, "richtig": correct, "antworten": answered,
                  "text": "%d %%" % round(rate), "unter": lernen.dash_quote_text(correct, answered)},
        "verlauf": [[day.isoformat(), day.strftime("%d.%m"), count] for day, count in daily],
        "fachbereiche": fach,
        "aktivitaeten": aktivitaeten,
        "kalender": kalender,
        "ap1_themen": themen(progress_ap1, core.AP1_THEMES),
        "ap2_themen": themen(progress, core.AP2_THEMES),
        "reinzoom": reinzoom,
    }


RUNDEN = [0.0, 0.05, 0.15, 0.25, 0.35, 0.45, 0.75, 1.25, 2.675, 12.35, 99.95, 99.94999999999999,
          100.0, 33.33333333333333, 66.66666666666667, 5e-324, 1e-7, 0.04999999999999999,
          -0.25, -2.675, 1234567.85]
FLOAT_TEXT = [0.1, 1.0, 100.0, 1e16, 1e15, 123456789012345.6, 1e-4, 1e-5, 0.00012345, 1.5e300,
              -2.5, 5e-324, 1.7976931348623157e308, 2.0 ** 60, 0.30000000000000004]
LETZTE_WORTE = ["", "a", "a b", "a b c", "0 von 3 Karten", "Prüfungstrainer 0 % richtig",
                "x · y · z", "eins  zwei drei", "Lernserie: 1 Tag  ·  2 von 3 % richtig"]

EINSTELLUNGEN = [
    ({}, None),
    ({"ziel_an": False, "serie_an": False}, "  Nico \t H  "),
    ({"ziel_anzahl": 5}, "Ein sehr langer Name mit vielen Woertern und mehr als vierzig Zeichen"),
    ({"ziel_anzahl": 100, "serie_an": True}, ""),
]


def main():
    if len(sys.argv) != 4:
        print(__doc__)
        return 2
    repo, testdaten, ziel = sys.argv[1], sys.argv[2], sys.argv[3]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    atexit.register(shutil.rmtree, tmp, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_game as game
    import fisi_hilfe as hilfe
    import fisi_lernen as lernen

    ersetze_datum(core)
    ersetze_datum(lernen)
    keep_last_words = lies_funktion(os.path.join(repo, "mobile", "src", "ui.py"),
                                    "keep_last_words", {})
    german_time = lies_funktion(os.path.join(repo, "mobile", "src", "main.py"),
                                "german_time", {})

    # Feste Zeitstempel und uid beim Schreiben
    jetzt = ["", 0]

    def stempel(_self=None):
        return jetzt[0], 120

    def uid():
        jetzt[1] += 1
        return "dashboard-uid-%04d" % jetzt[1]

    core.DBManager._uid = staticmethod(uid)

    def schreibe(db, zeit, aktion):
        jetzt[0] = zeit
        db._stamp = stempel
        aktion()

    ordner = os.path.join(tmp, "db")
    os.makedirs(ordner)
    pfade = {name: os.path.join(ordner, name + ".db")
             for name in ("leer", "teilweise", "voll", "gemischt", "ohne_tabelle")}
    dbs = {"leer": core.DBManager(pfade["leer"])}
    dbs["teilweise"] = baue_teilweise(core, pfade["teilweise"], random.Random(1310), schreibe)
    dbs["gemischt"] = baue_gemischt(core, pfade["gemischt"], random.Random(1320), schreibe)
    shutil.copyfile(testdaten, pfade["voll"])
    dbs["voll"] = core.DBManager(pfade["voll"])
    shutil.copyfile(pfade["gemischt"], pfade["ohne_tabelle"])
    conn = sqlite3.connect(pfade["ohne_tabelle"])
    conn.execute("DROP TABLE quiz_answers")
    conn.commit()
    conn.close()
    stderr = sys.stderr
    sys.stderr = open(os.devnull, "w")    # DBManager meldet die fehlende Tabelle
    dbs["ohne_tabelle"] = db_ohne = core.DBManager.__new__(core.DBManager)
    db_ohne.db_path = pfade["ohne_tabelle"]
    db_ohne.error_handler = None
    db_ohne.last_clock_fix = 0
    db_ohne.read_only_reason = None
    db_ohne.error_counts = {}
    db_ohne.refused_writes = 0
    # ohne init_db: sonst legte DBManager die Tabelle neu an

    def tag(text):
        return datetime.date.fromisoformat(text)

    faelle_plan = [
        ("leer", "2026-10-20", 0), ("leer", "2026-10-20", 1),
        ("teilweise", "2026-10-20", 0), ("teilweise", "2026-10-21", 2),
        ("teilweise", "2026-10-23", 3),
        ("voll", "2026-10-01", 0), ("voll", "2026-10-02", 1), ("voll", "2026-09-30", 2),
        ("gemischt", "2026-10-20", 0), ("gemischt", "2026-10-20", 1),
        ("gemischt", "2026-10-21", 2), ("gemischt", "2026-10-25", 3),
        ("gemischt", "2026-09-25", 0), ("gemischt", "2026-11-03", 0),
        ("ohne_tabelle", "2026-10-20", 0),
    ]
    faelle = []
    for db_name, heute, nummer in faelle_plan:
        einstellungen, name = EINSTELLUNGEN[nummer]
        faelle.append({
            "name": "%s_%s_%d" % (db_name, heute, nummer), "db": db_name, "heute": heute,
            "einstellungen": einstellungen, "nutzername": name,
            "werte": werte(core, lernen, game, hilfe, keep_last_words, german_time,
                           dbs[db_name], tag(heute), einstellungen, name,
                           db_name == "ohne_tabelle", pfade[db_name])})
    sys.stderr.close()
    sys.stderr = stderr

    daten = {
        "quelle": "app_gui.DashboardView, mobile/src/main.DashboardScreen (Formeln), "
                  "fisi_core.DBManager, fisi_lernen, fisi_game.topic_knowledge, "
                  "fisi_hilfe.greeting (Python v0.63.2)",
        "python": sys.version.split()[0],
        "tage": TAGE,
        "aktivitaeten": AKTIVITAETEN,
        "konstanten": {name: getattr(lernen, name) for name in sorted(dir(lernen))
                       if name.startswith("DASH_") or name == "LEARN_CHART_SERIES"},
        "kartentexte": kartentexte(repo),
        # Hilfsrechnungen: round(x, 1) (Wissensstand) und str(float) (Werte
        # aus der Datenbank, die Python mit %s ausgibt)
        "runden": [[x, round(x, 1)] for x in RUNDEN],
        "float_text": [[x, str(x)] for x in FLOAT_TEXT],
        "letzte_worte": [[t, keep_last_words(t)] for t in LETZTE_WORTE],
        "datenbanken": {
            "teilweise": zeilen(pfade["teilweise"]),
            "gemischt": zeilen(pfade["gemischt"]),
            "ohne_tabelle": {"basis": "gemischt", "ohne": ["quiz_answers"]},
        },
        "faelle": faelle,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    print("G13: %d Faelle -> %s" % (len(faelle), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
