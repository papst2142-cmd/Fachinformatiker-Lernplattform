#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G16 (Phase 2, Paket L1 "Status und Reihenfolge"),
gerechnet vom echten Python-Code der Python-Linie (v0.63.2):

  status       - fisi_core.question_status fuer alle Antwortfolgen bis Laenge 5
                 (richtig, falsch, nicht bewertet) und StatusBook.streak/touched
  reihenfolge  - StatusBook.preferred_order ohne Zufall, mit random.Random(Startwert)
                 und mit fresh_order (Gewichtung nach Rahmenplan) auf echten Inhalten,
                 dazu counts und matches (Statusfilter)
  punkte       - fisi_rahmenplan.item_points fuer ALLE Karteikarten und Fragen des
                 Pruefungstrainers (Reihenfolge der Inhalte, je Eintrag die Punkte)
  gewichte     - item_weight fuer alle Inhalte (Vorgabe-Einstellungen, 20.10.2026)
                 und fuer jeden 7. Inhalt unter weiteren Einstellungen und Stichtagen
                 (Pruefungsphase AP1/AP2, Abschnitte B/D/E an)
  gewichtet    - weighted_order fuer feste Auswahlen je Tag und Einstellung
  phase        - months_before und exam_phase an den Grenzen
  einstellungen - rp_settings fuer verschiedene einstellungen.json-Inhalte

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_reihenfolge.py <pfad_fisi_lernplattform> tests/Vergleich/reihenfolge.json
"""
import datetime
import itertools
import json
import os
import random
import sys
import tempfile


def tag(text):
    return datetime.date.fromisoformat(text)


EINSTELLUNGEN = [
    {},
    {"rp_abschnitt_b": True, "rp_abschnitt_d": 1, "rp_abschnitt_e": "ja"},
    {"rp_gewichtung": False, "rp_lernfeld": "LF 3", "rp_termin_ap1": "1.2.2027"},
    {"rp_termin_ap1": "", "rp_termin_ap2": "2027-06-30", "rp_lernfeld": "LF 99"},
    {"rp_termin_ap1": "31.02.2027", "rp_termin_ap2": " 26.04.2028 ", "rp_abschnitt_b": 0},
    {"rp_termin_ap1": None, "rp_termin_ap2": 20280426, "rp_abschnitt_e": []},
    {"rp_termin_ap1": "2027-9-29", "rp_termin_ap2": "2028-04-26x"},
]

STICHTAGE = ["2026-10-20", "2027-03-28", "2027-03-29", "2027-09-28", "2027-09-29",
             "2027-10-26", "2027-10-27", "2028-04-25", "2028-04-26"]


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
    import fisi_core as core
    import fisi_rahmenplan as rp

    # question_status, streak, touched
    status = []
    for laenge in range(6):
        for folge in itertools.product([True, False, None], repeat=laenge):
            eintraege = [("2026-10-01 10:00:%02d" % i, w) for i, w in enumerate(folge)]
            book = core.StatusBook(results={("quiz", "x"): eintraege})
            status.append({"werte": list(folge), "status": list(core.question_status(eintraege)),
                           "folge": book.streak("quiz", "x"), "beruehrt": book.touched("quiz", "x")})

    # preferred_order
    karten = [k["q"] for k in core.KARTEIKARTEN]
    fragen = [q["q"] for q in core.QUIZ_QUESTIONS]
    werte_vorgabe = dict(rp.RP_SETTINGS)
    reihenfolge = []
    rng = random.Random(1601)
    for nummer in range(12):
        quelle, pool = (core.SRC_CARD, karten) if nummer % 2 == 0 else (core.SRC_QUIZ, fragen)
        keys = rng.sample(pool, rng.choice([0, 1, 4, 5, 9, 30, 120]))
        results = {}
        for key in keys:
            art = rng.random()
            if art < 0.35:
                continue            # unbearbeitet
            folge = [rng.choice([True, False, None]) for _ in range(rng.randint(1, 4))]
            results[(quelle, key)] = [("2026-10-%02d 09:00:00" % (i + 1), w) for i, w in enumerate(folge)]
        book = core.StatusBook(results=results)
        heute = tag(["2026-10-20", "2027-05-01", "2028-01-10"][nummer % 3])
        fall = {"quelle": quelle, "schluessel": keys, "heute": heute.isoformat(),
                "antworten": [[key, [[t, v] for t, v in e]] for (_q, key), e in results.items()],
                "ohne_zufall": book.preferred_order(quelle, keys),
                "zaehlen": book.counts(quelle, keys),
                "filter": {name: [k for k in keys if book.matches(quelle, k, name)]
                           for name in ("Alle", core.Q_OPEN, core.Q_PRACTICE, core.Q_DONE)}}
        startwert = 1700 + nummer
        fall["startwert"] = startwert
        fall["mit_zufall"] = book.preferred_order(quelle, keys, rng=random.Random(startwert))
        fall["gewichtet"] = book.preferred_order(
            quelle, keys, fresh_order=rp.fresh_order(quelle, werte_vorgabe, heute))
        fall["zufall_und_gewichtet"] = book.preferred_order(
            quelle, keys, rng=random.Random(startwert),
            fresh_order=rp.fresh_order(quelle, werte_vorgabe, heute))
        reihenfolge.append(fall)

    # item_points fuer alle Inhalte (Reihenfolge der Inhalte)
    punkte = rp.item_points()
    alle_punkte = {"karte": [punkte.get((core.SRC_CARD, k), []) for k in karten],
                   "quiz": [punkte.get((core.SRC_QUIZ, q), []) for q in fragen]}

    # Einstellungen
    einstellungen = [{"eingabe": e, "werte": rp.rp_settings(e)} for e in EINSTELLUNGEN]

    # item_weight
    gewichte = {"vorgabe_2026-10-20": {
        "karte": [rp.item_weight(core.SRC_CARD, k, werte_vorgabe, tag("2026-10-20")) for k in karten],
        "quiz": [rp.item_weight(core.SRC_QUIZ, q, werte_vorgabe, tag("2026-10-20")) for q in fragen]}}
    stichproben = []
    for nummer, eingabe in enumerate(EINSTELLUNGEN):
        werte = rp.rp_settings(eingabe)
        for heute in STICHTAGE[nummer % 3::3]:
            stichproben.append({
                "einstellungen": nummer, "heute": heute,
                "karte": [rp.item_weight(core.SRC_CARD, k, werte, tag(heute)) for k in karten[::7]],
                "quiz": [rp.item_weight(core.SRC_QUIZ, q, werte, tag(heute)) for q in fragen[::7]]})
    gewichte["stichproben"] = stichproben

    # weighted_order
    gewichtet = []
    rng = random.Random(1602)
    for nummer, eingabe in enumerate(EINSTELLUNGEN):
        werte = rp.rp_settings(eingabe)
        for heute in STICHTAGE[nummer % 4::4]:
            quelle, pool = (core.SRC_CARD, karten) if nummer % 2 == 0 else (core.SRC_QUIZ, fragen)
            keys = rng.sample(pool, 150)
            gewichtet.append({"einstellungen": nummer, "heute": heute, "quelle": quelle,
                              "schluessel": keys,
                              "zufall": [rp._day_random(quelle, k, tag(heute)) for k in keys[:20]],
                              "ergebnis": rp.weighted_order(quelle, keys, werte, tag(heute))})

    # months_before, exam_phase
    phase = []
    for text in ["2027-09-29", "2028-04-26", "2027-08-31", "2027-03-31", "2028-02-29", "2027-01-15"]:
        day = tag(text)
        phase.append({"tag": text, "monate_vorher": [rp.months_before(day, m).isoformat()
                                                     for m in (0, 1, 6, 11, 12, 13, 25)]})
    pruefung = []
    for nummer, eingabe in enumerate(EINSTELLUNGEN):
        werte = rp.rp_settings(eingabe)
        for heute in STICHTAGE:
            pruefung.append([nummer, heute, rp.exam_phase(werte, tag(heute))])

    daten = {
        "quelle": "fisi_core.question_status, StatusBook; fisi_rahmenplan.item_points, "
                  "item_weight, weighted_order, fresh_order, rp_settings, exam_phase (Python v0.63.2)",
        "python": sys.version.split()[0],
        "einstellungen_eingaben": EINSTELLUNGEN,
        "status": status,
        "reihenfolge": reihenfolge,
        "punkte": alle_punkte,
        "einstellungen": einstellungen,
        "gewichte": gewichte,
        "gewichtet": gewichtet,
        "monate": phase,
        "pruefungsphase": pruefung,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G16: %d Status, %d Reihenfolgen, %d Inhalte, %d gewichtete Reihenfolgen -> %s"
          % (len(status), len(reihenfolge), len(karten) + len(fragen), len(gewichtet), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
