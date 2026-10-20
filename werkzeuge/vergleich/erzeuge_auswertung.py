#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G18 (Phase 2, Welle 3, Paket L7 "Fortschritt"),
gerechnet vom echten Python-Code der Python-Linie (v0.63.2, eingefroren):

  faelle      - je Datenbank, Stichtag und Einstellungen alles, was
                app_gui.ProgressView.refresh/_paint_days zeigt:
                Kacheln (get_stats, get_all_results, de_number, ihk_note),
                "Aufgaben pro Tag" und "Richtig und falsch pro Tag" fuer 7 und
                30 Tage (daily_series, day_labels, goal_line_text,
                daily_summary, daily_split_series, split_percent,
                split_summary), "Ergebnisse im Pruefungstrainer"
                (result_series, letzte 20), Tabelle der Sessions, Tabelle der
                Pruefungen (fp.EXAM, punkte_text) und die schwachen Themen der
                letzten Pruefung (fp.latest_weak).
  abdeckung   - fisi_rahmenplan.Coverage je Datenbank und Rahmenplan-
                Einstellungen: sections(filter) fuer alle Filter, fields(),
                any_answers; Werte als Zahl (repr) plus percent_text und level.
  ueben       - "Jetzt ueben": topic_practice_parts bzw. point_practice_parts
                ohne Zufall (rng=None, nur die Rangfolge rot, gelb,
                unbearbeitet, angefangen, fertig), practice_next_text.
  runden      - Rundungen wie Python: round(x) (halb gerade), round(x, 1)
                (exakter Wert der Gleitkommazahl), de_number(x, 1) (Decimal,
                kaufmaennisch), punkte_text (Decimal, abgerundet),
                split_percent und percent_text.
  texte       - Beschriftungen aus app_gui.py (PC-Wortlaut), fisi_widgets.py,
                fisi_pruefung.py, fisi_lernen.py, fisi_rahmenplan.py und
                fisi_rechtliches.py, mit ast gelesen (Datei, Zeile, Nummer).

Datenbanken: leer (frisch von DBManager), voll (Kopie von
tests/Vergleich/testdaten.db), gemischt (fest erzeugt, Zeilen stehen unter
"datenbanken"), sonder (letzte Pruefung mit kaputten Daten) und ohne_tabelle
(gemischt ohne test_results und pruefungen). Zeitstempel und uid sind fest.
Es werden keine echten Lerndaten benutzt; alle Daten- und Einstellungsordner
zeigen in einen leeren Temp-Ordner.

Aufruf (Python 3.12 wie die Python-Linie):
  python3 -I werkzeuge/vergleich/erzeuge_auswertung.py <pfad_fisi_lernplattform> tests/Vergleich/testdaten.db tests/Vergleich/auswertung.json
"""
import ast
import datetime
import json
import os
import random
import shutil
import sqlite3
import sys
import tempfile

TABELLEN = ("card_events", "quiz_answers", "scenario_events", "project_events",
            "ap1_events", "trainer_aufgaben", "test_results", "pruefungen")

# Texte (Datei, Zeile, Nummer des Textes in der Zeile)
TEXTE = {
    "Sessions": ("app_gui.py", 6575, 0),
    "Durchschnitt": ("app_gui.py", 6576, 0),
    "Bestes": ("app_gui.py", 6578, 0),
    "SessionsTitel": ("app_gui.py", 6615, 0),
    "SpalteDatum": ("app_gui.py", 6624, 1),
    "SpalteErgebnis": ("app_gui.py", 6624, 3),
    "SpalteQuote": ("app_gui.py", 6625, 1),
    "SpalteDauer": ("app_gui.py", 6626, 1),
    "PruefungenTitel": ("app_gui.py", 6639, 0),
    "PruefungenUnter": ("app_gui.py", 6640, 1),
    "SpaltePruefung": ("app_gui.py", 6646, 1),
    "SpaltePunkte": ("app_gui.py", 6646, 3),
    "Aktualisieren": ("app_gui.py", 6657, 0),
    "SessionsUnter": ("app_gui.py", 6695, 0),
    "ProzentMuster": ("app_gui.py", 6697, 0),
    "DurchschnittUnter": ("app_gui.py", 6698, 0),
    "OhneWert": ("app_gui.py", 6705, 0),
    "BestesLeer": ("app_gui.py", 6706, 0),
    "ErgebnisMuster": ("app_gui.py", 6728, 0),
    "DauerMuster": ("app_gui.py", 6731, 0),
    "BalkenLegende": ("fisi_widgets.py", 1997, 0),
    "SchwachTitel": ("fisi_pruefung.py", 405, 0),
}

KONSTANTEN_LERNEN = ["DAY_CHART_TITLE", "DAY_CHART_SUBTITLE", "DAY_CHART_RANGES", "DAY_CHART_SERIES",
                     "SPLIT_CHART_TITLE", "SPLIT_RIGHT", "SPLIT_WRONG", "SPLIT_EMPTY_DAY",
                     "RESULT_CHART_TITLE", "RESULT_CHART_SUBTITLE", "RESULT_CHART_SERIES",
                     "RESULT_CHART_EMPTY", "PRACTICE_ROUND", "PRACTICE_BUTTON", "PRACTICE_NONE",
                     "PRACTICE_NEXT_TITLE", "WEEKDAYS", "EXAM_THEME_TOPICS"]
KONSTANTEN_RAHMENPLAN = ["COVERAGE_TITLE", "COVERAGE_SUBTITLE", "COVERAGE_VIEWS", "COVERAGE_FILTERS",
                         "NO_CONTENT", "NO_ANSWERS", "CURRENT_FIELD_TEXT", "FILTER_EMPTY"]

RUNDEN = [0.0, 0.5, 1.5, 2.5, 12.5, 37.5, 62.5, 87.5, 99.5, 0.05, 0.15, 0.25, 0.35, 2.675,
          12.25, 12.35, 49.95, 49.94999999999999, 66.66666666666667, 33.333333333333336,
          99.95, 99.94999999999999, 100.0, 1e-12, -0.0, -0.05, 14.285714285714286,
          57.14285714285714, 28.57142857142857, 83.33333333333334, 16.666666666666664]
PUNKTE = [0, 0.0, -0.0, -1e-12, 49.95, 49.99999999999999, 50.0, 72.5, 33.3, 0.05, 99.99,
          100, 12.349999999999998, 1e-10, 66.66666666666667, float("inf"), float("nan")]
SPLIT = [(0, 0), (1, 0), (0, 1), (1, 1), (1, 7), (3, 5), (5, 3), (1, 2), (2, 1), (5, 7),
         (7, 1), (1, 199), (199, 1), (17, 3), (265, 55), (1, 39), (3, 13)]

EINSTELLUNGEN = [
    {},
    {"ziel_an": False},
    {"ziel_an": True, "ziel_anzahl": 5},
    {"ziel_an": True, "ziel_anzahl": 3},          # wird auf GOAL_MIN 5 gehoben
    {"ziel_an": True, "ziel_anzahl": "x"},
]

RP_EINSTELLUNGEN = [
    {},
    {"rp_abschnitt_b": True, "rp_abschnitt_d": True, "rp_abschnitt_e": True, "rp_lernfeld": "LF 4"},
    {"rp_lernfeld": "LF 10a"},
    {"rp_abschnitt_b": True, "rp_lernfeld": "LF 12a"},
]

UEBEN_THEMEN = ["Subnetting & Routing", "Arbeitswelt", "Recht & Verträge", "ipv4", "Unbekannt",
                "IPv4 & Subnetting", "Vertragsrecht & Unternehmensformen"]


def literal(baum, zeile, nummer):
    knoten = sorted((k for k in ast.walk(baum)
                     if isinstance(k, ast.Constant) and isinstance(k.value, str) and k.lineno == zeile),
                    key=lambda k: k.col_offset)
    return knoten[nummer].value


def zeilen(pfad):
    conn = sqlite3.connect(pfad)
    try:
        ergebnis = {}
        for tabelle in TABELLEN:
            try:
                cur = conn.execute("SELECT * FROM %s ORDER BY id" % tabelle)
            except sqlite3.OperationalError:
                continue
            spalten = [d[0] for d in cur.description]
            daten = [list(row) for row in cur.fetchall()]
            if daten:
                ergebnis[tabelle] = {"spalten": spalten, "zeilen": daten}
        return ergebnis
    finally:
        conn.close()


def zahl(wert):
    """Zahl fuer JSON (None bleibt None, inf/nan als Text)."""
    if wert is None:
        return None
    if isinstance(wert, float) and (wert != wert or wert in (float("inf"), float("-inf"))):
        return repr(wert)
    return wert


def baue_gemischt(core, db, rng, schreibe):
    karten = [k for k in core.KARTEIKARTEN if k.get("thema") in ("ipv4", "routing", "arbeitswelt")]
    fragen = [q for q in core.QUIZ_QUESTIONS if q.get("thema") in ("ipv4", "routing", "recht", "arbeitswelt")]
    andere = rng.sample(core.QUIZ_QUESTIONS[:1500], 20)
    karten = rng.sample(karten, 24)
    fragen = rng.sample(fragen, 30)
    start = datetime.date(2026, 9, 10)
    # Karten: je Karte eine Folge, damit rot, gelb, angefangen und fertig vorkommen
    folgen = [[False], [True, False], [True, True], [None], [True], [False, True],
              [True, True, True], [None, False], [True, None]]
    for nummer, karte in enumerate(karten):
        for schritt, richtig in enumerate(folgen[nummer % len(folgen)]):
            tag = start + datetime.timedelta(days=(nummer * 3 + schritt * 5) % 43)
            zeit = "%s %02d:%02d:%02d" % (tag.isoformat(), 7 + nummer % 12, nummer, schritt)
            schreibe(db, zeit, lambda: db.log_card(karte["cat"], karte["q"], "lernen", richtig))
    for nummer, frage in enumerate(fragen + andere):
        for schritt, richtig in enumerate(folgen[(nummer + 4) % len(folgen)]):
            if richtig is None:
                richtig = nummer % 2 == 0
            tag = start + datetime.timedelta(days=(nummer * 2 + schritt * 7) % 42)
            zeit = "%s %02d:%02d:%02d" % (tag.isoformat(), 8 + nummer % 11, nummer % 60, 30 + schritt)
            schreibe(db, zeit, lambda: db.log_quiz_answer(frage["cat"], frage["q"], richtig))
    # Gleicher Zeitpunkt zweimal, Uhr vorgestellt (spaeterer Tag)
    schreibe(db, "2026-10-20 06:00:00", lambda: db.log_card(karten[0]["cat"], karten[0]["q"], "lernen", True))
    schreibe(db, "2026-10-20 06:00:00", lambda: db.log_quiz_answer(fragen[0]["cat"], fragen[0]["q"], False))
    schreibe(db, "2026-10-22 10:00:00", lambda: db.log_quiz_answer(fragen[1]["cat"], fragen[1]["q"], True))
    # Szenarien, Projekte, Trainer (zaehlen bei "Aufgaben pro Tag")
    for zeit, index, richtig in [("2026-10-09 10:00:00", 0, None), ("2026-10-09 10:20:00", 0, True),
                                 ("2026-10-16 19:00:00", 7, False), ("2026-10-19 19:30:00", 40, True)]:
        szenario = core.SZENARIEN[index]
        schreibe(db, zeit, lambda: db.log_scenario(index, szenario["title"], szenario["theme"], richtig))
    for zeit, index, richtig in [("2026-10-05 11:00:00", 3, True), ("2026-10-17 11:45:00", 12, False)]:
        szenario = core.AP1_SZENARIEN[index]
        schreibe(db, zeit, lambda: db.log_ap1(index, szenario["title"], szenario["theme"], richtig))
    for zeit, index, richtig in [("2026-10-10 15:00:00", 1, None), ("2026-10-19 15:30:00", 2, True)]:
        projekt = core.PROJEKTARBEITEN[index]
        schreibe(db, zeit, lambda: db.log_project(index, projekt["title"], projekt["cat"], richtig))
    for zeit, richtig in [("2026-10-17 17:00:00", True), ("2026-10-20 17:05:00", False),
                          ("2026-10-20 17:06:00", True), ("2026-09-21 09:00:00", False)]:
        schreibe(db, zeit, lambda: db.log_trainer("subnetz", 2, richtig))
    # 23 Sessions (die Kurve zeigt die letzten 20), Rundungsfaelle bei de_number(x, 1)
    sessions = [(1, 8), (2, 3), (7, 8), (0, 5), (10, 10), (1, 3), (2, 3), (999, 2000), (1999, 2000),
                (1, 2000), (49, 400), (247, 2000), (13, 20), (5, 7), (4, 7), (2, 7), (5, 6),
                (1, 6), (17, 20), (3, 5), (9, 10), (1, 1), (6, 50)]
    dauer = [0, 59, 60, 61, 3599, 3600, 7322, 125, 300, 45]
    for nummer, (richtig, gesamt) in enumerate(sessions):
        prozent = richtig / gesamt * 100 if gesamt else 0.0
        zeit = "2026-%02d-%02d %02d:%02d:00" % (9 + nummer // 15, 1 + (nummer * 2) % 28, 10 + nummer % 9, nummer)
        schreibe(db, zeit, lambda: db.save_test_result(richtig, gesamt, prozent, core.ihk_note(prozent),
                                                       dauer[nummer % len(dauer)]))
    # Pruefungen (Klausursimulation), neueste zuletzt geschrieben; zwei mit gleichem Zeitpunkt
    pruefungen = [
        ("2026-09-20 10:00:00", "ap1", 49.95, 1800, {"themen": {"Rechnertechnik & Zahlensysteme": [20, 25],
                                                              "Projektplanung": [10, 25]}}),
        ("2026-10-01 10:00:00", "konzeption", 72.5, 5400, {"themen": {"Storage & RAID": [20, 25],
                                                                     "Virtualisierung": [5, 25]}}),
        ("2026-10-01 10:00:00", "altform", 49.99999999999999, 3599, {"themen": {}}),
        ("2026-10-12 10:00:00", "wiso", 100, 3600, {"themen": {"Recht & Verträge": [9, 10],
                                                             "Arbeitswelt": [4, 10]}}),
        ("2026-10-19 10:00:00", "netzwerke", 33.3, 5401,
         {"themen": {"IT-Sicherheit": [9, 10], "Subnetting & Routing": [5, 20], "Netzwerkdesign": [3, 10],
                     "Unbekannt": [0, 0], "Arbeitswelt": [1, 4]}}),
    ]
    for zeit, art, punkte, sekunden, daten in pruefungen:
        schreibe(db, zeit, lambda: db.save_exam(art, punkte, "Note %s" % art, sekunden, daten))


def baue_sonder(core, db, schreibe):
    schreibe(db, "2026-10-10 10:00:00", lambda: db.save_exam("ap1", 80.0, "2", 60, {"themen": {"Projektplanung": [1, 10]}}))
    schreibe(db, "2026-10-11 10:00:00", lambda: db.save_exam("netzwerke", 0.05, "6", 61, [1, 2]))
    schreibe(db, "2026-10-11 09:00:00", lambda: db.save_test_result(0, 0, 0.0, core.ihk_note(0.0), 0))


def main():
    if len(sys.argv) != 4:
        print(__doc__)
        return 2
    if sys.version_info[:2] != (3, 12):
        print("Bitte mit Python 3.12 erzeugen (wie die Python-Linie)")
        return 2
    repo, testdaten, ziel = sys.argv[1], sys.argv[2], sys.argv[3]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_lernen as lernen
    import fisi_pruefung as fp
    import fisi_rahmenplan as frp
    import fisi_rechtliches as fr

    baeume = {}
    for datei in ("app_gui.py", "fisi_widgets.py", "fisi_pruefung.py"):
        with open(os.path.join(repo, datei), encoding="utf-8") as handle:
            baeume[datei] = ast.parse(handle.read(), datei)
    texte = {schluessel: literal(baeume[datei], zeile, nummer)
             for schluessel, (datei, zeile, nummer) in TEXTE.items()}
    assert texte["SchwachTitel"] == "Üben (letzte Prüfung: %s%s)", texte["SchwachTitel"]
    assert texte["SpaltePunkte"] == "Punkte" and texte["DauerMuster"] == "%02d:%02d min", texte
    texte["NoteKurz"] = fr.NOTE_KURZ

    jetzt = ["", 0]

    def stempel(_self=None):
        return jetzt[0], 120

    def uid():
        jetzt[1] += 1
        return "auswertung-uid-%04d" % jetzt[1]

    core.DBManager._uid = staticmethod(uid)

    def schreibe(db, zeit, aktion):
        jetzt[0] = zeit
        db._stamp = stempel
        aktion()

    ordner = os.path.join(tmp, "db")
    os.makedirs(ordner)
    pfade = {name: os.path.join(ordner, name + ".db")
             for name in ("leer", "voll", "gemischt", "sonder", "ohne_tabelle")}
    dbs = {"leer": core.DBManager(pfade["leer"])}
    shutil.copyfile(testdaten, pfade["voll"])
    dbs["voll"] = core.DBManager(pfade["voll"])
    dbs["gemischt"] = core.DBManager(pfade["gemischt"])
    baue_gemischt(core, dbs["gemischt"], random.Random(1810), schreibe)
    dbs["sonder"] = core.DBManager(pfade["sonder"])
    baue_sonder(core, dbs["sonder"], schreibe)
    shutil.copyfile(pfade["gemischt"], pfade["ohne_tabelle"])
    conn = sqlite3.connect(pfade["ohne_tabelle"])
    conn.execute("DROP TABLE test_results")
    conn.execute("DROP TABLE pruefungen")
    conn.commit()
    conn.close()
    stderr = sys.stderr
    sys.stderr = open(os.devnull, "w")    # DBManager meldet die fehlenden Tabellen
    dbs["ohne_tabelle"] = db_ohne = core.DBManager.__new__(core.DBManager)
    db_ohne.db_path = pfade["ohne_tabelle"]
    db_ohne.error_handler = None
    db_ohne.last_clock_fix = 0
    db_ohne.read_only_reason = None
    db_ohne.error_counts = {}
    db_ohne.refused_writes = 0

    def fall(db, heute, einstellungen):
        results = db.get_all_results()
        count, average = db.get_stats()
        kacheln = {"sessions": str(count), "sessions_unter": texte["SessionsUnter"],
                   "durchschnitt": "%s %%" % core.de_number(average, 1),
                   "durchschnitt_unter": texte["DurchschnittUnter"]}
        if results:
            best = max(row[3] for row in results)
            kacheln.update(bestes="%s %%" % core.de_number(best, 1), bestes_unter=core.ihk_note(best))
        else:
            kacheln.update(bestes="-", bestes_unter="noch keine Session")
        activity = db.activity_days()
        split = db.activity_split_days()
        goal = lernen.goal_settings(einstellungen)
        target = goal["ziel_anzahl"] if goal["ziel_an"] else None
        tage = {}
        for days, _name in lernen.DAY_CHART_RANGES:
            series = lernen.daily_series(activity, days, heute)
            reihe = lernen.daily_split_series(split, days, heute)
            labels = lernen.day_labels([(day, 0) for day, _r, _w in reihe])
            tage[str(days)] = {
                "beschriftungen": lernen.day_labels(series),
                "werte": [count for _day, count in series],
                "ziel": target, "ziel_text": lernen.goal_line_text(target) if target else None,
                "zusammenfassung": lernen.daily_summary(series, target),
                "anteile": [[label, *lernen.split_percent(right, wrong), right, wrong]
                            for label, (_day, right, wrong) in zip(labels, reihe)],
                "anteile_text": lernen.split_summary(reihe),
            }
        labels, values = lernen.result_series(results)
        sessions = [[timestamp, "%d / %d" % (score, total), "%s %%" % core.de_number(percentage, 1), note,
                     "%02d:%02d min" % (duration // 60, duration % 60)]
                    for timestamp, score, total, percentage, note, duration in results]
        exams = db.exams()
        pruefungen = []
        for entry in exams:
            exam = fp.EXAM.get(entry["art"])
            pruefungen.append([entry["timestamp"], exam["name"] if exam else entry["art"],
                               exam["kurz"] if exam else None, fp.punkte_text(entry["punkte"]),
                               entry["note"],
                               "%02d:%02d min" % (entry["dauer"] // 60, entry["dauer"] % 60)])
        titel, schwach = fp.latest_weak(exams)
        return {"kacheln": kacheln, "tage": tage,
                "ergebnisse": {"beschriftungen": labels, "werte": [zahl(v) for v in values]},
                "sessions": sessions, "pruefungen": pruefungen,
                "schwach": {"titel": titel, "themen": schwach} if schwach else None}

    faelle_plan = [
        ("leer", "2026-10-20", 0), ("leer", "2026-10-20", 1),
        ("voll", "2026-10-01", 0), ("voll", "2026-10-07", 2),
        ("gemischt", "2026-10-20", 0), ("gemischt", "2026-10-20", 1), ("gemischt", "2026-10-21", 2),
        ("gemischt", "2026-10-25", 3), ("gemischt", "2026-11-03", 4), ("gemischt", "2026-09-25", 0),
        ("gemischt", "2026-03-01", 0),
        ("sonder", "2026-10-11", 0),
        ("ohne_tabelle", "2026-10-20", 0),
    ]
    faelle = []
    for db_name, heute, nummer in faelle_plan:
        einstellungen = EINSTELLUNGEN[nummer]
        faelle.append({"name": "%s_%s_%d" % (db_name, heute, nummer), "db": db_name, "heute": heute,
                       "einstellungen": einstellungen,
                       "werte": fall(dbs[db_name], datetime.date.fromisoformat(heute), einstellungen)})

    def wert(value):
        return {"wert": zahl(value), "text": frp.percent_text(value),
                "level": frp.level(value) if value is not None else None}

    def abdeckung(db, rp, alle_filter):
        values = frp.rp_settings(rp)
        cov = frp.Coverage(core.StatusBook(db), values)
        filter_namen = [f for f, _n in frp.COVERAGE_FILTERS] if alle_filter else [frp.FILTER_ALL]
        sections = {}
        for name in filter_namen:
            sections[name] = [{"id": s["id"], "titel": frp.section_label(s["id"]), **wert(s["wert"]),
                               "positionen": [{"id": p["id"], "titel": "%s %s" % (p["id"], p["titel"]),
                                               **wert(p["wert"]),
                                               "punkte": [{"id": r["id"], "titel": frp.point_label(r["id"]),
                                                           "anzahl": r["anzahl"],
                                                           "anzahl_text": frp.count_text(r["anzahl"]),
                                                           **wert(r["wert"])}
                                                          for r in p["punkte"]]}
                                              for p in s["positionen"]]}
                              for s in cov.sections(name)]
        felder = []
        for year in cov.fields():
            felder.append({"jahr": year["jahr"], "titel": frp.year_label(year["jahr"]),
                           **wert(frp._weighted_mean([(f["wert"], f["stunden"]) for f in year["felder"]])),
                           "felder": [{"id": f["id"], "titel": frp.field_label(f), "stunden": f["stunden"],
                                       "aktuell": f["aktuell"], **wert(f["wert"]),
                                       "themen": [{"id": t["id"], "titel": t["titel"], "anzahl": t["anzahl"],
                                                   "anzahl_text": frp.count_text(t["anzahl"]), **wert(t["wert"])}
                                                  for t in f["themen"]]}
                                      for f in year["felder"]]})
        return {"any_answers": cov.any_answers, "sections": sections, "fields": felder}

    abdeckungen = []
    for db_name, rp_nummer, alle in [("leer", 0, False), ("voll", 0, False), ("gemischt", 0, True),
                                      ("gemischt", 1, True), ("gemischt", 2, False), ("gemischt", 3, False)]:
        abdeckungen.append({"name": "%s_%d" % (db_name, rp_nummer), "db": db_name,
                            "einstellungen": RP_EINSTELLUNGEN[rp_nummer],
                            "werte": abdeckung(dbs[db_name], RP_EINSTELLUNGEN[rp_nummer], alle)})

    def teile(parts):
        return [{"quelle": quelle, "schluessel": keys} for quelle, keys in parts]

    ueben = []
    for db_name in ("leer", "gemischt"):
        book = core.StatusBook(dbs[db_name])
        for name in UEBEN_THEMEN:
            parts = lernen.topic_practice_parts(book, lernen.practice_topics(name))
            ueben.append({"db": db_name, "art": "thema", "name": name,
                          "themen": lernen.practice_topics(name), "teile": teile(parts)})
        for punkt in ("C3e", "A1a", "F1", "B1a"):
            if punkt in frp.POINT:
                ueben.append({"db": db_name, "art": "punkt", "name": punkt,
                              "teile": teile(frp.point_practice_parts(book, punkt))})
    sys.stderr.close()
    sys.stderr = stderr

    daten = {
        "quelle": "app_gui.ProgressView/CoverageCard/practice_topic, fisi_lernen, fisi_rahmenplan, "
                  "fisi_pruefung, fisi_core.DBManager (Python v0.63.2)",
        "python": sys.version.split()[0],
        "texte": texte,
        "konstanten": {**{n: getattr(lernen, n) for n in KONSTANTEN_LERNEN},
                       **{n: getattr(frp, n) for n in KONSTANTEN_RAHMENPLAN},
                       "EXAMS": {e["art"]: [e["name"], e["kurz"]] for e in fp.EXAMS}},
        "folgetexte": [[quelle, n, lernen.practice_next_text(quelle, n)]
                       for quelle in (core.SRC_QUIZ, core.SRC_CARD) for n in (1, 2, 10)],
        "jahre": [[j, frp.year_label(j)] for j in (1, 2, 3)],
        "runden": {
            "round": [[x, round(x)] for x in RUNDEN],
            "round1": [[x, round(x, 1)] for x in RUNDEN],
            "de_number1": [[x, core.de_number(x, 1)] for x in RUNDEN],
            "percent_text": [[x, frp.percent_text(x)] for x in RUNDEN + [None]],
            "level": [[x, frp.level(x)] for x in (0.0, 49.94999999999999, 49.95, 50.0, 79.9, 80.0, 100.0)],
            "punkte_text": [[zahl(x), fp.punkte_text(x)] for x in PUNKTE],
            "split_percent": [[r, w, *lernen.split_percent(r, w)] for r, w in SPLIT],
        },
        "datenbanken": {"gemischt": zeilen(pfade["gemischt"]), "sonder": zeilen(pfade["sonder"]),
                        "ohne_tabelle": {"basis": "gemischt", "ohne": ["test_results", "pruefungen"]}},
        "faelle": faelle,
        "abdeckung": abdeckungen,
        "ueben": ueben,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    print("G18: %d Faelle, %d Abdeckungen, %d Uebungen -> %s" % (len(faelle), len(abdeckungen), len(ueben), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
