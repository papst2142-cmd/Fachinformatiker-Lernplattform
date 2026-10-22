#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G32 (Phase 4, Welle 2: Klausursimulation), gerechnet vom
echten Python-Code der Python-Linie (v0.63.2, eingefroren; fisi_pruefung, fisi_core).
Zeitzone fest Europe/Berlin. Teile:

  kriterien  - split_numbered, subtask_points, criteria und scenario_parts fuer alle
               Musterloesungen (AP1 und AP2), dazu usable_indices je Thema
  neu        - new_exam je Pruefungsbereich x feste Startwerte x Verlauf (leer, teilweise,
               aufgebraucht, fremde Art, alte Fragetexte) mit festem Zeitpunkt
  zeit       - elapsed_seconds, seconds_left, submit (dauer) mit festen Zeitpunkten
               (Zeitumstellung, Uhr zurueck, nur "start" ohne "start_utc"), time_text
  bewertung  - suggested_points, wiso_result, evaluate, weak_topics, latest_weak
  gesamt     - _abrunden, punkte_text, ihk_note, overall (mit _rules und
               _supplement_hint) ueber das Raster der Spezifikation P4-5 (SI)
  migration  - migrate_running fuer alle Eintraege von CRITERIA_BEFORE, Verlaengern,
               WiSo-Umbenennungen und Antwortmoeglichkeiten; align_marks
  abschluss  - Zeilen, die finish mit fester Uhr und festen uid in eine Wegwerf-
               Datenbank schreibt (quiz_answers, ap1_events, scenario_events, pruefungen)
  kontrolle  - Faelle fuer die Kontrollfehler, die das Raster von G36 nicht trifft (Summe,
               Note aus dem abgerundeten Wert, round(x, 1), Satztrennung mit .NET-Zeichenklassen)
  texte      - feste Texte der Klausur aus app_gui.py und mobile/src/main.py (mit ast
               gelesen: Datei, Zeile, Nummer in der Zeile; C#-Name -> Text)

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_pruefung.py <pfad_fisi_lernplattform> tests/Vergleich/pruefung.json
"""
import ast
import datetime
import itertools
import json
import os
import random
import shutil
import sqlite3
import sys
import tempfile
import time

ZONE = "Europe/Berlin"
STARTWERTE = [0, 1, 4711, 123456789, (1 << 30) - 1]
JETZT = "2026-10-22T09:15:42.654321+02:00"

# C#-Name -> (Datei, Zeile, Nummer des Textes in der Zeile)
TEXTE = {
    "Uebung": ("app_gui.py", 3375, 1),
    "Pruefung": ("app_gui.py", 3375, 3),
    "AuswahlTitel": ("app_gui.py", 3438, 0),
    "AuswahlUntertitel": ("app_gui.py", 3439, 0),
    "Einleitung": ("app_gui.py", 3442, 0),
    "AngabeWiso": ("app_gui.py", 3462, 0),
    "AngabeOffen": ("app_gui.py", 3465, 0),
    "Starten": ("app_gui.py", 162, 0),
    "LetztePruefungen": ("app_gui.py", 3532, 0),
    "Insgesamt": ("app_gui.py", 3533, 0),
    "KeinePruefung": ("app_gui.py", 3536, 0),
    "PunkteMuster": ("app_gui.py", 3546, 0),
    "StartTitel": ("app_gui.py", 3562, 0),
    "StartFrage": ("app_gui.py", 3563, 0),
    "StartFrageHandy": ("mobile/src/main.py", 1541, 0),
    "Verbleibend": ("app_gui.py", 3588, 0),
    "Abbrechen": ("app_gui.py", 3590, 0),
    "Abgeben": ("app_gui.py", 3591, 0),
    "FragenBeantwortet": ("app_gui.py", 3622, 0),
    "TeileBearbeitet": ("app_gui.py", 3626, 0),
    "AufgabeNummer": ("app_gui.py", 3600, 0),
    "AufgabeUntertitel": ("app_gui.py", 3638, 1),
    "Teilaufgabe": ("app_gui.py", 3646, 0),
    "WisoTitel": ("app_gui.py", 3662, 0),
    "Seite": ("app_gui.py", 3663, 0),
    "FrageNummer": ("app_gui.py", 3668, 0),
    "Zurueck": ("app_gui.py", 3684, 0),
    "Weiter": ("app_gui.py", 3687, 0),
    "LaeuftTitel": ("app_gui.py", 3190, 0),
    "LaeuftText": ("app_gui.py", 3190, 1),
    "AbbrechenTitel": ("app_gui.py", 3730, 0),
    "AbbrechenFrage": ("app_gui.py", 3731, 0),
    "AbgebenTitel": ("app_gui.py", 3740, 0),
    "AbgebenFrage": ("app_gui.py", 3740, 1),
    "ZeitTitel": ("app_gui.py", 3750, 0),
    "ZeitWiso": ("app_gui.py", 3750, 1),
    "ZeitOffen": ("app_gui.py", 3755, 1),
    "BewertungTitel": ("app_gui.py", 3767, 0),
    "BewertungAnleitung": ("app_gui.py", 3770, 0),
    "BewertungAbschliessen": ("app_gui.py", 3778, 0),
    "Zwischenstand": ("app_gui.py", 3798, 0),
    "TitelThema": ("app_gui.py", 3805, 1),
    "DeineAntwort": ("app_gui.py", 3814, 0),
    "KeineAntwort": ("app_gui.py", 3818, 2),
    "Musterloesung": ("app_gui.py", 3820, 0),
    "Punkte": ("app_gui.py", 3843, 0),
    "VonMax": ("app_gui.py", 3855, 0),
    "AbschliessenFrage": ("app_gui.py", 3862, 1),
    "ErgebnisTitel": ("app_gui.py", 3877, 0),
    "NoteDauer": ("app_gui.py", 3883, 0),
    "FragenRichtig": ("app_gui.py", 3889, 0),
    "AufgabeErgebnis": ("app_gui.py", 3894, 0),
    "NachThemen": ("app_gui.py", 3898, 0),
    "Schwachstellen": ("app_gui.py", 3899, 0),
    "BalkenWert": ("app_gui.py", 3907, 0),
    "Ueben": ("app_gui.py", 3910, 0),
    "ZurAuswahl": ("app_gui.py", 3913, 0),
    "Gesamtergebnis": ("app_gui.py", 4118, 0),
    "Gewichtung": ("app_gui.py", 4119, 0),
    "BereichZeile": ("app_gui.py", 4124, 0),
    "NochOffen": ("app_gui.py", 4126, 0),
    "FehltNoch": ("app_gui.py", 4131, 0),
    "GesamtZeile": ("app_gui.py", 4137, 0),
    "Bestanden": ("app_gui.py", 4139, 1),
    "NichtBestanden": ("app_gui.py", 4139, 2),
    "Erfuellt": ("app_gui.py", 4143, 0),
    "Offen": ("app_gui.py", 4143, 1),
    # Nr 81 (Wortliste W2): erst mit der Ansicht "Uebersicht" des Abschlussprojekts (W3)
    "ProjektHinweisW3": ("app_gui.py", 4133, 0),
}


def literal(baum, zeile, nummer):
    knoten = sorted((k for k in ast.walk(baum)
                     if isinstance(k, ast.Constant) and isinstance(k.value, str) and k.lineno == zeile),
                    key=lambda k: k.col_offset)
    return knoten[nummer].value


def kriterien_werte(fp, fc):
    eintraege = []
    for quelle, daten in (("ap1", fc.AP1_SZENARIEN), ("ap2", fc.SZENARIEN)):
        for index, szenario in enumerate(daten):
            einleitung, teile = fp.split_numbered(szenario["text"])
            _e, loesungen = fp.split_numbered(szenario["solution"])
            teile_daten = fp.scenario_parts(szenario)
            eintraege.append({
                "quelle": quelle, "index": index,
                # Teile aus Aufgabe und Loesung nur, wenn scenario_parts nichts liefert
                "split": [[einleitung, teile], [_e, loesungen]] if teile_daten is None else None,
                "teile": None if teile_daten is None else {
                    "titel": teile_daten["titel"], "thema": teile_daten["thema"],
                    "einleitung": teile_daten["einleitung"],
                    "teile": [[t["text"], t["punkte"], t["loesung"], t["kriterien"]]
                              for t in teile_daten["teile"]]},
            })
    nutzbar = {}
    for exam in fp.EXAMS:
        if exam["art"] == fp.WISO:
            continue
        themen = exam["pool"] if exam["themen"] == "4ausPool" else \
            sorted({t for wahl in exam["themen"] for t in wahl})
        for thema in themen:
            nutzbar["%s|%s" % (exam["quelle"], thema)] = fp.usable_indices(exam["quelle"], thema)
    zusatz = ["", "Kein Satz", "1. eins\n2. zwei", "  3.  drei\nweiter\n4.vier", "Text ohne Nummer.\n\n",
              "- Punkt eins. Punkt zwei!\n* Stern\n• Kreis", "-c 1 schickt ein Paket. -rw-r-----",
              "Z.B. eine Firewall. Z. B. ein Proxy. XYZ.B. bleibt.", "Ca. 3.5 GB, d.h. genug. Nr. 5 gilt.",
              "Termin. Danach geht es weiter. \"Zitat\" folgt. (Klammer) auch. „Deutsch“ ebenso.",
              "1.5 und 2.0 bleiben. Ende? Ja! 3 Dinge.", "ab", "a. b. c.", "Äpfel. Öl. Übel. ölig."]
    return {"szenarien": eintraege, "nutzbar": nutzbar,
            "zusatz": [[t, fp.split_numbered(t), fp.criteria(t)] for t in zusatz],
            "teilpunkte": [[n, fp.subtask_points(n)] for n in range(1, 13)]}


def verlaeufe(fp, fc, art):
    """Feste Verlaeufe fuer new_exam/used_items (neueste zuerst wie exams)."""
    exam = fp.EXAM[art]
    if art == fp.WISO:
        pool = fp.wiso_pool(exam)
        renames = fc.QUESTION_RENAMES.get("quiz_answers") or {}
        alt = [a for a, n in renames.items() if fp.current_question(a) in pool][:3]
        return {
            "leer": [],
            "teilweise": [{"art": art, "daten": {"fragen": [{"q": q, "richtig": True} for q in pool[:20]]}}],
            "alte_texte": [{"art": art, "daten": {"fragen": [{"q": q} for q in alt] + [q for q in pool[20:25]]}}],
            "aufgebraucht": [{"art": art, "daten": {"fragen": [{"q": q} for q in pool]}}],
            "fast_aufgebraucht": [{"art": art, "daten": {"fragen": [{"q": q} for q in pool[:-10]]}}],
            "fremde_art": [{"art": fp.AP1, "daten": {"fragen": [{"q": q} for q in pool]}}],
        }
    alle = sorted({i for t in (exam["pool"] if exam["themen"] == "4ausPool"
                                else {t for w in exam["themen"] for t in w})
                   for i in fp.usable_indices(exam["quelle"], t)})
    return {
        "leer": [],
        "teilweise": [{"art": art, "daten": {"aufgaben": [{"index": i} for i in alle[::3]]}}],
        "aufgebraucht": [{"art": art, "daten": {"aufgaben": [{"index": i} for i in alle]}},
                         {"art": art, "daten": None}],
        "fremde_art": [{"art": fp.WISO, "daten": {"aufgaben": [{"index": i} for i in alle]}}],
    }


def neu_werte(fp, fc):
    jetzt = datetime.datetime.fromisoformat(JETZT).astimezone().replace(tzinfo=None)
    faelle, alle_verlaeufe = [], {}
    for exam in fp.EXAMS:
        for name, verlauf in verlaeufe(fp, fc, exam["art"]).items():
            alle_verlaeufe["%s|%s" % (exam["art"], name)] = verlauf
            for startwert in STARTWERTE:
                zustand = fp.new_exam(exam["art"], verlauf, seed=startwert, now=jetzt)
                faelle.append({"art": exam["art"], "verlauf": name, "startwert": startwert, "zustand": zustand,
                               "benutzt": sorted(map(str, fp.used_items(verlauf, exam["art"])))})
    # Zeitumstellung beim Start (Ortszeit ohne Mikrosekunden, start_utc aus der Ortszeit)
    momente = []
    for moment in ("2026-03-29T00:59:59+00:00", "2026-03-29T01:00:00+00:00",
                   "2026-10-25T00:30:00+00:00", "2026-10-25T01:30:00+00:00", "2026-12-31T22:59:59.999999+00:00"):
        lokal = datetime.datetime.fromisoformat(moment).astimezone()
        z = fp.new_exam(fp.WISO, [], seed=7, now=lokal.replace(tzinfo=None))
        za = fp.new_exam(fp.WISO, [], seed=7, now=lokal)
        momente.append([moment, z["start"], z["start_utc"], za["start"], za["start_utc"]])
    return {"jetzt": JETZT, "verlaeufe": alle_verlaeufe, "faelle": faelle, "momente": momente}


def zeit_werte(fp):
    zustaende = [
        {"start": "2026-10-22 09:00:00", "start_utc": 1792652400, "minuten": 90},
        {"start": "2026-10-22 09:00:00", "start_utc": 1792652400.5, "minuten": 60},
        {"start": "2026-10-25 01:30:00", "minuten": 90},              # vor der Rueckstellung
        {"start": "2026-10-25 02:30:00", "minuten": 90},              # doppelte Stunde (fold 0)
        {"start": "2026-03-29 02:30:00", "minuten": 60},              # gibt es nicht (Luecke)
        {"start": "2026-03-29 01:30:00", "minuten": 60},
        {"start": "2026-10-22 09:00:00", "start_utc": "kaputt", "minuten": 90},
        {"start": "2026-10-22 09:00:00", "start_utc": True, "minuten": 60},
    ]
    abstaende = [-3600, -1, 0, 0.4, 1, 59.9, 60, 299, 300.5, 3599, 3600, 3600.999, 5399, 5399.5, 5400, 5401, 7200, 100000]
    faelle = []
    for zustand in zustaende:
        basis = zustand.get("start_utc")
        if not isinstance(basis, (int, float)) or isinstance(basis, bool):
            basis = datetime.datetime.strptime(zustand["start"], "%Y-%m-%d %H:%M:%S").timestamp()
        for abstand in abstaende:
            jetzt = datetime.datetime.fromtimestamp(basis + abstand, datetime.timezone.utc)
            kopie = dict(zustand)
            vergangen = fp.elapsed_seconds(kopie, jetzt)
            rest = fp.seconds_left(kopie, jetzt)
            fp.submit(kopie, jetzt)
            faelle.append({"zustand": zustand, "jetzt_utc": jetzt.timestamp(), "vergangen": vergangen,
                           "rest": rest, "dauer": kopie["dauer"], "phase": kopie["phase"],
                           "frist": fp.deadline(zustand).strftime("%Y-%m-%d %H:%M:%S")})
    texte = [[s, fp.time_text(s)] for s in (0, 1, 59, 60, 61, 299, 300, 599, 3599, 3600, 3601, 3661, 5400, 35999, 36000, 86399)]
    return {"faelle": faelle, "texte": texte}


def bewertung_werte(fp, fc):
    vorschlag = []
    for maximum in range(0, 26):
        for n in range(0, 13):
            for k in range(0, n + 1):
                vorschlag.append([maximum, n, k, fp.suggested_points(maximum, [True] * k + [False] * (n - k))])
    by_question = {q["q"]: q for q in fc.QUIZ_QUESTIONS}
    renames = fc.QUESTION_RENAMES.get("quiz_answers") or {}
    jetzt = datetime.datetime.fromisoformat(JETZT).astimezone().replace(tzinfo=None)
    zustaende = []
    for startwert in (3, 11, 4711):
        z = fp.new_exam(fp.WISO, [], seed=startwert, now=jetzt)
        rng = random.Random(startwert)
        for nummer, frage in enumerate(z["fragen"]):
            wahl = rng.random()
            if wahl < 0.5:
                z["antworten"][frage] = by_question[frage]["a"]
            elif wahl < 0.8:
                z["antworten"][frage] = z["optionen"][frage][nummer % len(z["optionen"][frage])]
        z["dauer"] = 1234
        zustaende.append(z)
    # alte Fragetexte und eine Frage, die es nicht mehr gibt
    alt = [(a, n) for a, n in renames.items() if n in by_question and by_question[n].get("thema") in ("recht", "arbeitswelt")]
    z = fp.new_exam(fp.WISO, [], seed=5, now=jetzt)
    for nummer, (a, n) in enumerate(alt[:2]):
        z["fragen"][nummer] = a
        z["antworten"][a] = by_question[n]["a"] if nummer == 0 else "falsch"
    z["fragen"][2] = "Diese Frage gibt es nicht mehr?"
    z["antworten"]["Diese Frage gibt es nicht mehr?"] = "irgendwas"
    z["dauer"] = 3600
    zustaende.append(z)
    leer = fp.new_exam(fp.WISO, [], seed=9, now=jetzt)
    leer["fragen"] = []
    zustaende.append(leer)
    for art, startwert in ((fp.AP1, 2), (fp.KONZEPTION, 8), (fp.NETZWERKE, 21), (fp.AP1, 99)):
        z = fp.new_exam(art, [], seed=startwert, now=jetzt)
        z["phase"] = "bewertung"
        z["dauer"] = 4000 + startwert
        rng = random.Random(startwert)
        for nummer in range(len(z["aufgaben"])):
            teile = fp.task_details(z, nummer)["teile"]
            for teil_nummer, teil in enumerate(teile):
                schluessel = fp.answer_key(nummer, teil_nummer)
                wahl = rng.randrange(6)
                if wahl == 0:
                    continue
                z["punkte"][schluessel] = [teil["punkte"], 0, -2, teil["punkte"] + 5, 3, 7.9][wahl]
        zustaende.append(z)
    auswertungen = []
    for z in zustaende:
        ergebnis = fp.evaluate(json.loads(json.dumps(z)))
        auswertungen.append({"zustand": z,
                             "wiso": [list(r) for r in fp.wiso_result(z)] if z["art"] == fp.WISO else None,
                             "ergebnis": ergebnis, "schwach": fp.weak_topics(ergebnis),
                             "daten_text": json.dumps(ergebnis["daten"], ensure_ascii=False)})
    schwach = [
        {}, {"A": [0, 0]}, {"A": [1, 2]}, {"A": [49, 100]}, {"B": [1, 4], "A": [1, 4]},
        {"Üben": [0, 10], "Zeta": [0, 10]}, {"a": [1, 10], "B": [1, 10]},
        {"X": [3, 10], "Y": [1, 10], "Z": [6, 10]}, {"A": [25, 25], "B": [0, 25]},
        {"A": [0, 0], "B": [0, 5]}, {"Recht & Verträge": [2, 5], "Arbeitswelt": [3, 5]},
        {"T1": [12, 25], "T2": [13, 25], "T3": [12.5, 25]}]
    schwach = [[t, fp.weak_topics({"daten": {"themen": t}})] for t in schwach]
    letzte = []
    for exams in ([], [{"art": "wiso", "timestamp": "2026-10-01 12:00:00", "daten": {"themen": {"Arbeitswelt": [1, 5]}}}],
                  [{"art": "ap1", "timestamp": "2026-10-02 08:00:00", "daten": {"themen": {"A": [5, 5]}}}],
                  [{"art": "unbekannt", "timestamp": "2026-10", "daten": {"themen": {"A": [0, 5], "B": [1, 5]}}}],
                  [{"art": "netzwerke", "timestamp": "", "daten": {}}],
                  [{"art": "konzeption", "timestamp": "2026-10-03 10:00:00", "daten": {"themen": None}}],
                  [{"art": "konzeption", "timestamp": "2026-10-03 10:00:00", "daten": {"themen": {"A": "x"}}}],
                  [{"art": "konzeption", "timestamp": "2026-10-03 10:00:00", "daten": {"themen": {"A": [1, 0], "B": [0, 4]}}},
                   {"art": "wiso", "timestamp": "2026-10-04 10:00:00", "daten": {"themen": {"C": [0, 4]}}}]):
            kopf, themen = fp.latest_weak(exams)
            letzte.append([exams, kopf, themen])
    verlauf = [{"art": "wiso", "punkte": 50.0}, {"art": "ap1", "punkte": 33.5}, {"art": "wiso", "punkte": 90.0}]
    return {"vorschlag": vorschlag, "auswertungen": auswertungen, "schwach": schwach,
            "letzte_schwach": letzte, "letzte_ergebnisse": [verlauf, fp.latest_results(verlauf)]}


def gesamt_werte(fp, fc):
    punkte = [k / 10 for k in range(0, 1001)] + [
        0.0, -0.0, -1e-12, 1e-10, 0.05, 0.15, 12.25, 29.95, 29.99, 33.333333333333336, 49.9, 49.95, 49.97,
        49.99, 49.99999999999999, 50.0, 50.05, 66.95, 80.95, 91.95, 99.99, 100.0,
        float("inf"), float("-inf"), float("nan")]
    punkte += [round(100.0 * r / max(1, n), 1) for n in range(0, 31) for r in range(0, n + 1)]
    texte = []
    for w in punkte:
        endlich = w == w and w not in (float("inf"), float("-inf"))
        texte.append({"wert": repr(w), "text": fp.punkte_text(w),
                      "abgerundet": fp._abrunden(w) if endlich else None,
                      "note": fc.ihk_note(w) if endlich else None})
    rollen = ["ap1", "projekt", fp.KONZEPTION, fp.NETZWERKE, fp.WISO]
    raster = [0.0, 29.99, 30.0, 49.95, 50.0, 100.0]
    kombinationen = [list(t) for t in itertools.product(raster, repeat=5)]
    rng = random.Random("g36")
    kombinationen += [[rng.randint(0, 10000) / 100 for _ in range(5)] for _ in range(300)]
    faelle = []
    for werte in kombinationen:
        r = fp.overall(dict(zip(rollen, werte)))
        # Regeltexte sind fest bis auf die Zahl in Regel 3 (steht im Text von Regel 3)
        faelle.append([werte, r["gesamt"], r["teil2"], r["note"], r["bestanden"],
                       [ok for _t, ok in r["regeln"]], r["regeln"][2][0], r["ergaenzung"]])
    fehlend = []
    for weg in rollen:
        werte = {r: 50.0 for r in rollen if r != weg}
        fehlend.append([werte, fp.overall(werte)])
    fehlend.append([{}, fp.overall({})])
    regeln = [t for t, _ok in fp._rules({r: 50.0 for r in rollen})]
    return {"punkte": texte, "raster": raster, "regeln": regeln, "faelle": faelle, "fehlend": fehlend,
            "gewichte": fp.WEIGHTS, "teil2": fp.PART2, "bereiche": fp.AREA_NAME}


def migration_werte(fp, fc):
    faelle = []
    for (quelle, index, teil), alt in fp.CRITERIA_BEFORE.items():
        art = fp.AP1 if quelle == "ap1" else fp.KONZEPTION
        n = len(alt)
        muster = [[False] * n, [True] * n, [i % 2 == 0 for i in range(n)]]
        muster += [[i == j for i in range(n)] for j in range(n)]
        for haken in muster:
            z = {"art": art, "phase": "bewertung", "aufgaben": [{"quelle": quelle, "index": index}],
                 "kriterien": {fp.answer_key(0, teil): list(haken), "0-9": [True]}, "punkte": {}, "antworten": {}}
            vorher = json.loads(json.dumps(z))
            faelle.append([vorher, fp.migrate_running(z)])
    # unbekannte Aenderungen: kuerzer (verlaengern), laenger (so lassen), kein Eintrag, keine Liste
    z0 = {"art": fp.AP1, "aufgaben": [{"quelle": "ap1", "index": 0}, {"quelle": "ap1", "index": 1}]}
    teile0 = fp.task_details(z0, 0)["teile"]
    for kriterien in ({"0-0": [True]}, {"0-0": [True] * (len(teile0[0]["kriterien"]) + 3)},
                      {"0-0": "kaputt"}, {"1-0": [False, True]}, None, [], {"5-0": [True]}):
        z = {"art": fp.AP1, "aufgaben": [{"quelle": "ap1", "index": 0}, {"quelle": "ap1", "index": 1}],
             "kriterien": kriterien}
        vorher = json.loads(json.dumps(z))
        faelle.append([vorher, fp.migrate_running(z)])
    # WiSo: alte Fragetexte (umbenennungen.json) und eine umformulierte Antwortmoeglichkeit
    by_question = {q["q"]: q for q in fc.QUIZ_QUESTIONS}
    renames = fc.QUESTION_RENAMES.get("quiz_answers") or {}
    paare = [(a, n) for a, n in renames.items() if n in by_question][:4]
    jetzt = datetime.datetime.fromisoformat(JETZT).astimezone().replace(tzinfo=None)
    basis = fp.new_exam(fp.WISO, [], seed=13, now=jetzt)
    for variante in range(5):
        z = json.loads(json.dumps(basis))
        for nummer, (a, n) in enumerate(paare):
            z["fragen"][nummer] = a
            optionen = list(by_question[n]["options"])
            if variante == 1:
                z["optionen"].pop(z["fragen"][nummer] if False else basis["fragen"][nummer], None)
                z["optionen"][a] = optionen
                z["antworten"][a] = optionen[0]
            elif variante == 2:
                z["optionen"][a] = optionen
                z["optionen"][n] = list(optionen)
                z["antworten"][n] = optionen[1]
                z["antworten"][a] = optionen[0]
        frage = z["fragen"][10]
        optionen = list(by_question[frage]["options"])
        if variante == 3:
            alt = optionen[1] + " (alte Fassung)"
            z["optionen"][frage] = [alt if o == optionen[1] else o for o in optionen]
            z["antworten"][frage] = alt
        if variante == 4:
            z["optionen"][frage] = [o + " alt" for o in optionen[:2]] + optionen[2:]
            z["antworten"][frage] = optionen[0] + " alt"
            z["optionen"][z["fragen"][11]] = "keine Liste"
        vorher = json.loads(json.dumps(z))
        faelle.append([vorher, fp.migrate_running(z)])
    ausrichten = []
    for haken, alt, neu in (
            ([True, False], ["a", "b"], ["a", "b"]),
            ([True, False], ["a", "b"], ["a", "x", "b"]),
            ([True, True, False], ["a", "b", "c"], ["a", "c"]),
            ([True, False, True], ["a", "b", "c"], ["a", "B", "c"]),
            ([True, True], ["a", "b"], ["x", "y", "z"]),
            ([True, True, True], ["a", "b", "c"], ["z"]),
            ([True, False, True, True], ["a", "a", "b", "a"], ["a", "b", "a", "a", "c"]),
            ([], [], ["a"]), ([True], ["a"], []),
            ([True, False, True], ["x", "y", "z"], ["y", "z", "x"]),
            ([True, True, False, False, True], ["p", "q", "r", "s", "t"], ["q", "p", "r", "t", "s", "u"])):
        ausrichten.append([haken, alt, neu, fp.align_marks(haken, alt, neu)])
    return {"faelle": faelle, "ausrichten": ausrichten}


def abschluss_werte(fp, fc, tmp):
    jetzt = [None]
    uid = [0]
    fc.device_now = lambda: jetzt[0].astimezone()

    def neue_uid():
        uid[0] += 1
        return "abschluss-uid-%04d" % uid[0]

    fc.DBManager._uid = staticmethod(neue_uid)
    fp.clear_running = lambda: True
    datei = os.path.join(tmp, "abschluss.db")
    db = fc.DBManager(datei)
    bw = bewertung_werte(fp, fc)
    aufrufe = []
    for nummer, fall in enumerate(bw["auswertungen"]):
        zustand = json.loads(json.dumps(fall["zustand"]))
        if zustand["art"] == fp.WISO and not zustand["fragen"]:
            continue
        jetzt[0] = datetime.datetime.fromisoformat("2026-10-22T10:%02d:07+02:00" % nummer)
        erste = uid[0] + 1
        fp.finish(db, zustand)
        aufrufe.append({"utc": jetzt[0].isoformat(), "zustand": fall["zustand"],
                        "uids": ["abschluss-uid-%04d" % i for i in range(erste, uid[0] + 1)]})
    verbindung = sqlite3.connect(datei)
    tabellen = {}
    for tabelle, spalten in (("quiz_answers", ["timestamp", "utc_offset", "category", "question", "correct", "uid"]),
                             ("ap1_events", ["timestamp", "utc_offset", "scenario_index", "title", "theme", "correct", "uid"]),
                             ("scenario_events", ["timestamp", "utc_offset", "scenario_index", "title", "theme", "correct", "uid"]),
                             ("pruefungen", ["timestamp", "utc_offset", "art", "punkte", "note", "dauer", "daten", "uid"])):
        zeilen = verbindung.execute("SELECT %s FROM %s ORDER BY id" % (", ".join(spalten), tabelle)).fetchall()
        typen = verbindung.execute("SELECT %s FROM %s ORDER BY id" % (
            ", ".join("typeof(%s)" % s for s in spalten), tabelle)).fetchall()
        tabellen[tabelle] = {"spalten": spalten, "zeilen": [list(z) for z in zeilen], "typen": [list(t) for t in typen]}
    verbindung.close()
    return {"aufrufe": aufrufe, "tabellen": tabellen}


def kontrolle_werte(fp, fc):
    """Faelle fuer die Kontrollfehler (Spezifikation P4-5 Abschnitt 7), die das Raster von G36 nicht
    trifft: einfache Summe statt sum(), Note aus dem abgerundeten Wert (Werte mit random.Random(1)
    gesucht, hier fest), round(x, 1) ueber viele Werte, Satztrennung mit Zeichenklassen von .NET."""
    rollen = ["ap1", "projekt", fp.KONZEPTION, fp.NETZWERKE, fp.WISO]
    werte = [[60.87, 56.1, 24.97, 66.91, 5.88], [43.32, 64.64, 40.53, 15.04, 34.59], [71.52, 54.35, 6.57, 61.65, 16.99],
             [56.39, 16.58, 36.15, 27.31, 40.86], [31.06, 30.69, 5.1, 50.86, 28.47], [9.91, 39.3, 89.63, 86.76, 27.11],
             [43.85, 38.68, 95.97, 83.97, 26.66], [42.17, 38.23, 32.22, 84.91, 91.72], [70.91, 64.49, 24.55, 92.05, 89.13]]
    gesamt = []
    for w in werte:
        r = fp.overall(dict(zip(rollen, w)))
        total = sum(v * g for v, g in zip(w, fp.WEIGHTS.values())) / 100.0
        teil2 = sum(v * fp.WEIGHTS[a] for v, a in zip(w[1:], fp.PART2)) / 80
        gesamt.append([w, r["gesamt"], r["teil2"], r["note"], r["bestanden"], [ok for _t, ok in r["regeln"]],
                       repr(total), repr(teil2)])
    zahlen = [k / 1000 for k in range(0, 20001)] + [k / 8 + 0.05 for k in range(0, 801)] + \
        [100.0 * r / n for n in range(1, 101) for r in range(0, n + 1)]
    runden = [[repr(x), round(x, 1)] for x in zahlen]
    texte = ["\u00b2Z.B. Zwei.", "\u216bZ.B. Zwoelf.", "_Z.B. Strich.", "\u00e9Z.B. Eins.", "1Z.B. Eins.",
             "Satz eins.\x1fZwei folgt.", "Satz.\u00a0Neu hier.", "Satz.\u2003Neu dort.", "Satz.\x85Neu da.",
             "Wert 1.5 und \u0661.\u0662 bleiben. Neu.", "\u00b2.3 hoch. Ende."]
    return {"gesamt": gesamt, "runden": runden, "kriterien": [[t, fp.criteria(t)] for t in texte]}


def main():
    repo, ziel = sys.argv[1], sys.argv[2]
    os.environ["TZ"] = ZONE
    time.tzset()
    sys.path.insert(0, os.path.abspath(repo))
    tmp = tempfile.mkdtemp(prefix="g32_")
    try:
        os.environ["FISI_DATA_DIR"] = tmp
        import fisi_core as fc
        import fisi_pruefung as fp
        baeume, texte = {}, {}
        for name, (datei, zeile, nummer) in TEXTE.items():
            if datei not in baeume:
                with open(os.path.join(repo, datei), encoding="utf-8") as handle:
                    baeume[datei] = ast.parse(handle.read())
            texte[name] = literal(baeume[datei], zeile, nummer)
        daten = {
            "quelle": "fisi_pruefung (split_numbered, subtask_points, criteria, scenario_parts, usable_indices, "
                      "used_items, new_exam, Zeit, suggested_points, wiso_result, evaluate, weak_topics, latest_weak, "
                      "latest_results, _abrunden, punkte_text, overall, migrate_running, align_marks, finish), "
                      "fisi_core.ihk_note; Python v0.63.2",
            "python": sys.version.split()[0],
            "zone": ZONE,
            "pruefungen": [{k: v for k, v in e.items()} for e in fp.EXAMS],
            "kriterien": kriterien_werte(fp, fc),
            "neu": neu_werte(fp, fc),
            "zeit": zeit_werte(fp),
            "bewertung": bewertung_werte(fp, fc),
            "gesamt": gesamt_werte(fp, fc),
            "migration": migration_werte(fp, fc),
            "abschluss": abschluss_werte(fp, fc, tmp),
            "kontrolle": kontrolle_werte(fp, fc),
            "ergaenzung_texte": [fp._supplement_hint({"ap1": 100.0, "projekt": 100.0, "konzeption": 0.0,
                                                      "netzwerke": 0.0, "wiso": 0.0})],
            "texte": texte,
        }
        with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
            json.dump(daten, handle, ensure_ascii=False, separators=(",", ":"))
            handle.write("\n")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    main()
