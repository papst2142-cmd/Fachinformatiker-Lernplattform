#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G15 (Phase 2, Paket L1 "Wiederholung"): das
Wiederholungssystem der Python-Linie (fisi_lernen v0.63.2), gerechnet vom
echten Python-Code.

Inhalt:
  zustand    - review_state fuer feste Antwortfolgen (Faelle aus
               test_spiel.Wiederholung051Test) und Zufallsfolgen mit festen
               Startwerten (random.Random), auch mit Uhrzeiten 00:00:00 und
               23:59:59 (Mitternacht: es zaehlt der Tag des Zeitstempels)
  plaene     - ReviewPlan auf festen Antworten (StatusBook(results=...)) mit
               eigener Menge gueltiger Schluessel: Altbestand, Kontingent 20,
               heute schon umgestellte Altkarten, Stichtag vor SR_START
  datenbanken - ReviewPlan.from_db auf testdaten.db und auf einer erzeugten
               Datenbank "gemischt" (Zeilen werden mit ausgegeben, damit der
               C#-Test dieselbe Datenbank aufbauen kann) zu den Stichtagen
               30.09., 01.10., 14.10., 15.10., 20.11.2026 und weiteren; je
               Fall die faelligen Schluessel in Reihenfolge, Sitzung (50),
               Altbestand wartend, first_source, due_text und Stufen
Zeitstempel und uid beim Schreiben sind fest (DBManager._stamp/_uid ersetzt).

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_wiederholung.py <pfad_fisi_lernplattform> tests/Vergleich/testdaten.db tests/Vergleich/wiederholung.json
"""
import datetime
import json
import os
import random
import shutil
import sqlite3
import sys
import tempfile

STICHTAGE = ["2026-09-30", "2026-10-01", "2026-10-02", "2026-10-14", "2026-10-15",
             "2026-10-20", "2026-10-21", "2026-11-20", "2027-01-15"]


def tag(text):
    return datetime.date.fromisoformat(text)


def iso(day):
    return day.isoformat() if day is not None else None


def zustand_faelle(lernen):
    faelle = []
    # test_intervalle
    tage = ["2026-10-01", "2026-10-02", "2026-10-05", "2026-10-12", "2026-10-26", "2026-11-25"]
    eintraege = []
    for nummer, day in enumerate(tage):
        eintraege.append([day + " 10:00:00", True])
        faelle.append(("intervalle_%d" % nummer, list(eintraege)))
    faelle.append(("leer", []))
    faelle.append(("zu_frueh", [["2026-10-01 10:00:00", True], ["2026-10-01 18:00:00", True]]))
    faelle.append(("fehler", [["2026-10-01 10:00:00", True], ["2026-10-02 10:00:00", True],
                              ["2026-10-05 10:00:00", False]]))
    faelle.append(("nur_angesehen", [["2026-10-01 10:00:00", None], ["2026-10-03 10:00:00", None]]))
    faelle.append(("mitternacht", [["2026-10-01 23:59:59", True], ["2026-10-02 00:00:00", True],
                                   ["2026-10-04 23:59:59", True], ["2026-10-05 00:00:00", True]]))
    faelle.append(("t_trenner", [["2026-10-01T08:00:00", True], ["2026-10-02T09:00:00", True]]))
    faelle.append(("stufe_5_bleibt", [["2026-10-%02d 10:00:00" % d, True] for d in (1, 2, 5, 12, 26)]
                   + [["2026-11-25 10:00:00", True], ["2026-12-25 10:00:00", True],
                      ["2027-01-24 10:00:00", True]]))
    rng = random.Random(1501)
    start = tag("2026-09-10")
    for nummer in range(60):
        anzahl = rng.randint(1, 14)
        tag_nr = 0
        eintraege = []
        for _ in range(anzahl):
            tag_nr += rng.choice([0, 0, 1, 1, 2, 3, 5, 7, 14, 31])
            zeit = rng.choice(["00:00:00", "08:15:00", "12:00:00", "23:59:59"])
            wert = rng.choice([True, True, True, False, None])
            eintraege.append([iso(start + datetime.timedelta(days=tag_nr)) + " " + zeit, wert])
        faelle.append(("zufall_%02d" % nummer, eintraege))
    ergebnis = []
    for name, eintraege in faelle:
        stufe, faellig = lernen.review_state([tuple(e) for e in eintraege])
        info = lernen.review_info([tuple(e) for e in eintraege], tag(lernen.SR_START))
        ergebnis.append({"name": name, "eintraege": eintraege, "stufe": stufe,
                         "faellig": iso(faellig),
                         "info": None if info is None else
                         [info[0], iso(info[1]), info[2], iso(info[3])]})
    return ergebnis


def plan_werte(lernen, core, plan, mit_stufen=True):
    werte = {
        "karte": plan.due[core.SRC_CARD],
        "quiz": plan.due[core.SRC_QUIZ],
        "anzahl": plan.count(),
        "sitzung_karte": plan.session(core.SRC_CARD),
        "sitzung_quiz": plan.session(core.SRC_QUIZ),
        "altbestand_wartet": plan.legacy_waiting,
        "erste_quelle": plan.first_source(),
        "text": lernen.due_text(plan),
    }
    if mit_stufen:
        werte["stufen"] = sorted([[quelle, key, stufe, iso(due)]
                                  for (quelle, key), (stufe, due) in plan.state.items()])
    return werte


def plaene(lernen, core):
    faelle = []

    def plan(name, results, day):
        valid = {core.SRC_CARD: {key for (_s, key) in results},
                 core.SRC_QUIZ: {key for (_s, key) in results}}
        p = lernen.ReviewPlan(core.StatusBook(results=results), today=tag(day), valid=valid)
        faelle.append({"name": name, "heute": day,
                       "antworten": [[quelle, key, [[t, v] for t, v in eintraege]]
                                     for (quelle, key), eintraege in results.items()],
                       "werte": plan_werte(lernen, core, p)})

    results = {(core.SRC_CARD, "k%03d" % n): [("2026-09-20 10:00:00", n % 3 != 0)]
               for n in range(100)}
    plan("altbestand", dict(results), lernen.SR_START)
    first = lernen.ReviewPlan(core.StatusBook(results=dict(results)), today=tag(lernen.SR_START),
                              valid={core.SRC_CARD: {k for (_s, k) in results},
                                     core.SRC_QUIZ: set()}).session(core.SRC_CARD, 30)
    for key in first[:10]:
        results[(core.SRC_CARD, key)] = results[(core.SRC_CARD, key)] + [
            (lernen.SR_START + " 12:00:00", True)]
    plan("altbestand_umgestellt", dict(results), lernen.SR_START)
    plan("vor_start", dict(results), "2026-09-30")
    plan("neu_faellig_morgen", {(core.SRC_QUIZ, "q"): [("2026-10-02 09:00:00", True)]}, "2026-10-02")
    plan("neu_faellig", {(core.SRC_QUIZ, "q"): [("2026-10-02 09:00:00", True)]}, "2026-10-03")
    # Zufaellige Antworten, Karten und Fragen gemischt, mit Altbestand (gelb, rot, fertig)
    rng = random.Random(1502)
    for nummer, day in enumerate(["2026-10-01", "2026-10-09", "2026-10-20", "2026-12-01"]):
        results = {}
        for n in range(140):
            quelle = core.SRC_CARD if n % 3 else core.SRC_QUIZ
            key = "%s Frage %03d äöü" % (quelle, n)
            eintraege = []
            tag_nr = rng.randint(0, 30)
            for _ in range(rng.randint(1, 6)):
                tag_nr += rng.choice([0, 1, 2, 4, 9])
                zeit = iso(tag("2026-09-15") + datetime.timedelta(days=tag_nr))
                eintraege.append((zeit + rng.choice([" 00:00:00", " 13:30:00", " 23:59:59"]),
                                  rng.choice([True, True, False, None])))
            eintraege.sort(key=lambda e: e[0])
            results[(quelle, key)] = eintraege
        plan("zufall_%d" % nummer, results, day)
    return faelle


def baue_gemischt(core, pfad, rng, schreibe):
    db = core.DBManager(pfad)
    karten = rng.sample(core.KARTEIKARTEN, 70)
    fragen = rng.sample(core.QUIZ_QUESTIONS[:1500], 45) + rng.sample(core.QUIZ_QUESTIONS[-400:], 15)
    # Altbestand vor SR_START (rot, gelb, offen, fertig) - mehr als 20, damit gestaffelt wird
    for nummer, karte in enumerate(karten[:40]):
        zeit = "2026-09-%02d %02d:%02d:00" % (12 + nummer % 18, 7 + nummer % 15, nummer)
        werte = [[True, True], [False], [True, False, True], [None, True], [False, False]][nummer % 5]
        for schritt, wert in enumerate(werte):
            schreibe(db, zeit[:-2] + "%02d" % schritt,
                     lambda: db.log_card(karte["cat"], karte["q"], "lernen", wert))
    for nummer, frage in enumerate(fragen[:30]):
        zeit = "2026-09-%02d %02d:%02d:10" % (14 + nummer % 16, 9 + nummer % 9, nummer)
        schreibe(db, zeit, lambda: db.log_quiz_answer(frage["cat"], frage["q"], nummer % 4 != 1))
    # Ein Teil des Altbestands wird ab SR_START wieder bearbeitet (auch an Stichtagen)
    for nummer, karte in enumerate(karten[:12]):
        zeit = "2026-10-%02d %s" % ((1, 14, 15, 20)[nummer % 4], ("00:00:00", "23:59:59", "12:00:00")[nummer % 3])
        schreibe(db, zeit, lambda: db.log_card(karte["cat"], karte["q"], "lernen", nummer % 3 != 2))
    for nummer, frage in enumerate(fragen[:6]):
        zeit = "2026-10-%02d 13:%02d:00" % ((1, 15)[nummer % 2], nummer)
        schreibe(db, zeit, lambda: db.log_quiz_answer(frage["cat"], frage["q"], nummer != 3))
    # Neu ab SR_START: Folgen mit Stufen, Fehlern und Faelligkeiten, Mitternacht
    neu_tage = ["2026-10-01", "2026-10-02", "2026-10-05", "2026-10-09", "2026-10-13", "2026-10-14",
                "2026-10-15", "2026-10-19", "2026-10-20", "2026-11-03"]
    for nummer, karte in enumerate(karten[40:]):
        start = nummer % len(neu_tage)
        for schritt in range(1 + nummer % 5):
            index = min(len(neu_tage) - 1, start + schritt * 2)
            uhr = ("00:00:00", "23:59:59", "%02d:%02d:%02d" % (7 + nummer % 12, nummer, schritt))[(nummer + schritt) % 3]
            richtig = None if (nummer + schritt) % 7 == 0 else (nummer + schritt) % 5 != 0
            schreibe(db, "%s %s" % (neu_tage[index], uhr),
                     lambda: db.log_card(karte["cat"], karte["q"], "lernen", richtig))
    for nummer, frage in enumerate(fragen[30:]):
        start = (nummer * 2) % len(neu_tage)
        for schritt in range(1 + nummer % 4):
            index = min(len(neu_tage) - 1, start + schritt * 3)
            schreibe(db, "%s %02d:%02d:%02d" % (neu_tage[index], 8 + nummer % 11, nummer, 30 + schritt),
                     lambda: db.log_quiz_answer(frage["cat"], frage["q"], (nummer + schritt) % 4 != 0))
    # Gleicher Zeitpunkt zweimal fuer dieselbe Karte (Reihenfolge nach id)
    schreibe(db, "2026-10-14 10:00:00", lambda: db.log_card(karten[41]["cat"], karten[41]["q"], "lernen", False))
    schreibe(db, "2026-10-14 10:00:00", lambda: db.log_card(karten[41]["cat"], karten[41]["q"], "lernen", True))
    # Unbekannte Frage (nicht in den Inhalten): zaehlt nicht
    schreibe(db, "2026-10-02 10:00:00", lambda: db.log_card("Netzwerk", "Gibt es nicht?", "lernen", True))
    return db


def zeilen(pfad):
    conn = sqlite3.connect(pfad)
    try:
        ergebnis = {}
        for tabelle in ("card_events", "quiz_answers"):
            cur = conn.execute("SELECT * FROM %s ORDER BY id" % tabelle)
            spalten = [d[0] for d in cur.description]
            ergebnis[tabelle] = {"spalten": spalten, "zeilen": [list(r) for r in cur.fetchall()]}
        return ergebnis
    finally:
        conn.close()


def main():
    if len(sys.argv) != 4:
        print(__doc__)
        return 2
    repo, testdaten, ziel = sys.argv[1], sys.argv[2], sys.argv[3]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_lernen as lernen

    jetzt = ["", 0]

    def stempel(_self=None):
        return jetzt[0], 120

    def uid():
        jetzt[1] += 1
        return "wiederholung-uid-%04d" % jetzt[1]

    core.DBManager._uid = staticmethod(uid)

    def schreibe(db, zeit, aktion):
        jetzt[0] = zeit
        db._stamp = stempel
        aktion()

    pfade = {"gemischt": os.path.join(tmp, "gemischt.db"), "voll": os.path.join(tmp, "voll.db")}
    dbs = {"gemischt": baue_gemischt(core, pfade["gemischt"], random.Random(1510), schreibe)}
    shutil.copyfile(testdaten, pfade["voll"])
    dbs["voll"] = core.DBManager(pfade["voll"])

    datenbanken = []
    for name in ("voll", "gemischt"):
        for heute in STICHTAGE:
            plan = lernen.ReviewPlan.from_db(dbs[name], today=tag(heute))
            index = lernen.ReviewIndex(dbs[name]).plan(today=tag(heute))
            werte = plan_werte(lernen, core, plan)
            if plan_werte(lernen, core, index) != werte:
                raise SystemExit("ReviewIndex weicht von ReviewPlan ab: %s %s" % (name, heute))
            datenbanken.append({"db": name, "heute": heute, "werte": werte})

    daten = {
        "quelle": "fisi_lernen.review_state, review_info, ReviewPlan, ReviewIndex, due_text "
                  "(Python v0.63.2)",
        "python": sys.version.split()[0],
        "konstanten": {"abstaende": list(lernen.SR_INTERVALS), "start": lernen.SR_START,
                       "altbestand_je_tag": lernen.SR_LEGACY_PER_DAY,
                       "sitzung_max": lernen.SR_SESSION_MAX},
        "zustand": zustand_faelle(lernen),
        "plaene": plaene(lernen, core),
        "zeilen": {"gemischt": zeilen(pfade["gemischt"])},
        "datenbanken": datenbanken,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    print("G15: %d Zustaende, %d Plaene, %d Datenbank-Faelle -> %s"
          % (len(daten["zustand"]), len(daten["plaene"]), len(datenbanken), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
