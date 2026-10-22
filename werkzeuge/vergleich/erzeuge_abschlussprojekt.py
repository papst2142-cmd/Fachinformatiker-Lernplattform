#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G34 (Phase 4), gerechnet vom echten Python-Code der
Python-Linie (v0.63.2, eingefroren). Welle 1 (Paket PB1) braucht nur den Teil

  speichern - Zeilen, die DBManager.save_project_field (mit
              purge_superseded_project_rows in derselben Buchung), log_ap1,
              log_scenario, log_project und save_exam mit festem Zeitpunkt
              und fester uid schreiben. Die Faelle folgen test_n16_0632.py
              (E1 Loeschen, E3 gleiche Zeit wie die Loeschung bleibt, E4 eine
              Mikrosekunde spaeter faellt weg, E5 lebendes Projekt
              unberuehrt, E7 "_geloescht" = "0" ohne Wirkung, E8 frueheste
              Loeschung zaehlt, E9 spaetere leere Fassung bleibt) und dem
              Pflichtfall der Weisung P4-3 (gleiche Mikrosekunde: die
              groessere uid gilt, in beiden Richtungen). fisi_projekt
              .delete_project wird aufgerufen und die dabei entstehenden
              save_project_field-Aufrufe werden aufgezeichnet; C# spielt
              genau diese Aufrufe nach. Danach je Tabelle Zeilen und
              Spaltentypen (ohne id) und project_fields in dict-Reihenfolge.

Welle 3 (Weisung P4-8, Spezifikation P4-8 Teil D mit Nachtrag P4-9) fuegt hinzu,
ebenfalls vom echten Python gerechnet (fisi_projekt, fisi_pdf):

  zahl       - to_number fuer ein Raster von Eingaben (Vorgabe 0.0 und 15)
  rechnen    - schedule, schedule_warnings, costs, cost_lines, page_estimate,
               overview_progress, proposal_missing, estimate, project_title,
               export_name fuer feste Felder-Faelle; Raster hours_text, euro,
               export_name
  export     - export_blocks und export_text je Fall, Dateibytes PC/Handy
  projekte   - Projekte, Loeschen, Vorlage, Menue und aktives Projekt in einer
               Datenbank mit fester Zeit und fester uid
  fragen     - questions_for und question_rating
  rueckfrage - delete_question mit und ohne Abgleich
  pdf        - build_pdf mit festem Datum, entpackte Inhalte und Objekte
  texte      - die festen Listen von fisi_projekt
  grenze     - Python mit gesetzter Grenze: MAX_HOURS und der Text "max40" von
               aussen auf 40 bzw. 80 gesetzt (Projektstunden je Ausbildung,
               Weisung P4-8 Frage 1 b; Python kennt nur 40)
  zeichen    - fisi_pdf._clean fuer jeden einzelnen Codepunkt (Laeufe)

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_abschlussprojekt.py <pfad_fisi_lernplattform> tests/Vergleich/abschlussprojekt.json
"""
import datetime
import hashlib
import json
import os
import re
import shutil
import sqlite3
import sys
import tempfile
import time
import unicodedata
import zlib

ZONE = "Europe/Berlin"

TABELLEN = [
    ("abschlussprojekt", ["timestamp", "projekt", "feld", "wert", "uid"]),
    ("ap1_events", ["timestamp", "utc_offset", "scenario_index", "title", "theme", "correct", "uid"]),
    ("scenario_events", ["timestamp", "utc_offset", "scenario_index", "title", "theme", "correct", "uid"]),
    ("project_events", ["timestamp", "utc_offset", "project_index", "title", "category", "correct", "uid"]),
    ("pruefungen", ["timestamp", "utc_offset", "art", "punkte", "note", "dauer", "daten", "uid"]),
]


def speichern_werte(tmp):
    import fisi_core as fc
    import fisi_projekt as fpj
    jetzt = [None]
    uid = [None, None]
    fc.device_now = lambda: jetzt[0].astimezone()
    fc.DBManager._now_fine = staticmethod(
        lambda: jetzt[0].astimezone().replace(tzinfo=None).strftime("%Y-%m-%d %H:%M:%S.%f"))
    fc.DBManager._uid = staticmethod(lambda: uid[0])
    db = fc.DBManager(os.path.join(tmp, "abschlussprojekt.db"))
    aufrufe = []
    original = db.save_project_field
    zaehler = [0]

    def feld(projekt, name, wert):
        zaehler[0] += 1
        if uid[1] is None:
            uid[0] = "abschluss-uid-%04d" % zaehler[0]
        aufrufe.append({"art": "feld", "utc": jetzt[0].isoformat(), "uid": uid[0],
                        "projekt": projekt, "feld": name, "wert": wert})
        return original(projekt, name, wert)

    db.save_project_field = feld

    def setze(moment, feste_uid=None):
        jetzt[0] = datetime.datetime.fromisoformat(moment)
        uid[1] = feste_uid
        if feste_uid is not None:
            uid[0] = feste_uid

    def ereignis(art, moment, kennung, *werte):
        setze(moment, kennung)
        if art == "ap1":
            db.log_ap1(*werte)
        elif art == "szenario":
            db.log_scenario(*werte)
        elif art == "projekt":
            db.log_project(*werte)
        else:
            db.save_exam(*werte)
        aufrufe.append({"art": art, "utc": moment, "uid": kennung, "werte": list(werte)})

    # E1: Projekt mit drei Feldern, dann loeschen (delete_project)
    setze("2026-10-21T08:00:00.000001+00:00")
    db.save_project_field("p-e1", fpj.TITLE_FIELD, "Titel E1 äöüß")
    setze("2026-10-21T08:00:01.000000+00:00")
    db.save_project_field("p-e1", "ausgangslage", "Ausgangslage E1")
    setze("2026-10-21T08:00:02.250000+00:00")
    db.save_project_field("p-e1", "ziel", "Ziel E1")
    setze("2026-10-21T08:00:03.000000+00:00")
    db.save_project_field("p-e1", fpj.PAGES_FIELD, "15")
    setze("2026-10-21T08:10:00.000000+00:00")
    fpj.delete_project(db, "p-e1")
    # E3: gleiche Zeit wie die Loeschung bleibt; E4: eine Mikrosekunde spaeter faellt weg
    setze("2026-10-21T08:10:00.000000+00:00")
    db.save_project_field("p-e1", "zusatz", "gleiche Zeit")
    setze("2026-10-21T08:10:00.000001+00:00")
    db.save_project_field("p-e1", "zusatz2", "eine Mikrosekunde spaeter")
    # E9: spaetere leere Fassung bleibt
    setze("2026-10-21T10:10:00.000000+00:00")
    db.save_project_field("p-e1", "ausgangslage", "")
    # E5: lebendes Projekt bleibt unberuehrt (auch spaeter als die Loeschung)
    setze("2026-10-21T07:00:00.000000+00:00")
    db.save_project_field("p-e5", fpj.TITLE_FIELD, "Lebt")
    setze("2026-10-21T12:00:00.000000+00:00")
    db.save_project_field("p-e5", "ziel", "bleibt")
    setze("2026-10-21T12:00:00.500000+00:00")
    db.save_project_field("p-e5", "ziel", "bleibt, neuere Fassung")
    # E7: _geloescht = "0" ohne Wirkung
    setze("2026-10-21T09:00:00.000000+00:00")
    db.save_project_field("p-e7", fpj.TITLE_FIELD, "Titel E7")
    db.save_project_field("p-e7", fpj.DELETED_FIELD, "0")
    setze("2026-10-21T11:00:00.000000+00:00")
    db.save_project_field("p-e7", "ziel", "spaeter")
    # E8: frueheste Loeschung zaehlt (zwei Loeschungen, Text dazwischen und danach)
    setze("2026-10-21T13:00:00.000000+00:00")
    db.save_project_field("p-e8", fpj.TITLE_FIELD, "Titel E8")
    setze("2026-10-21T13:01:00.000000+00:00")
    db.save_project_field("p-e8", fpj.DELETED_FIELD, "1")
    setze("2026-10-21T13:02:00.000000+00:00")
    db.save_project_field("p-e8", "ziel", "dazwischen")
    setze("2026-10-21T13:03:00.000000+00:00")
    db.save_project_field("p-e8", fpj.DELETED_FIELD, "1")
    setze("2026-10-21T13:04:00.000000+00:00")
    db.save_project_field("p-e8", "", "")
    # Pflichtfall P4-3 (b): gleiche Mikrosekunde, die groessere uid gilt (beide Richtungen)
    setze("2026-10-21T14:00:00.123456+00:00", "m-uid-b")
    db.save_project_field("p-uid", "ziel", "zuerst, groessere uid")
    setze("2026-10-21T14:00:00.123456+00:00", "m-uid-a")
    db.save_project_field("p-uid", "ziel", "danach, kleinere uid")
    setze("2026-10-21T14:00:00.654321+00:00", "n-uid-a")
    db.save_project_field("p-uid", "titel", "zuerst, kleinere uid")
    setze("2026-10-21T14:00:00.654321+00:00", "n-uid-b")
    db.save_project_field("p-uid", "titel", "danach, groessere uid")
    # Sommerzeit endet (doppelte Stunde) und Sonderzeichen
    setze("2026-10-25T00:59:59.999999+00:00")
    db.save_project_field("p-zeit", "ziel", "vor dem Wechsel")
    setze("2026-10-25T01:00:00.000000+00:00")
    db.save_project_field("p-zeit", "ziel", "nach dem Wechsel")
    setze("2026-10-25T01:30:00.000000+00:00")
    db.save_project_field("p-zeit", "notiz", "Zeile 1\nZeile 2\t😀 ' \" ; DROP")

    # Ereignisse: None = angesehen, True/False = bewertet; Pruefung
    ereignis("ap1", "2026-10-21T15:00:00.000000+00:00", "e-uid-01", 3, "AP1 Titel", "Netzwerk", None)
    ereignis("ap1", "2026-10-21T15:00:10.000000+00:00", "e-uid-02", 3, "AP1 Titel", "Netzwerk", True)
    ereignis("ap1", "2026-10-21T15:00:20.000000+00:00", "e-uid-03", 4, "AP1 Titel 2", "Wirtschaft", False)
    ereignis("szenario", "2026-10-21T21:59:59.000000+00:00", "e-uid-04", 7, "Szenario äöü", "Sicherheit", None)
    ereignis("szenario", "2026-10-21T22:00:00.000000+00:00", "e-uid-05", 7, "Szenario äöü", "Sicherheit", False)
    ereignis("projekt", "2026-10-25T00:30:00.000000+00:00", "e-uid-06", 2, "Projekt", fc.CAT_NET, None)
    ereignis("projekt", "2026-10-25T01:30:00.000000+00:00", "e-uid-07", 2, "Projekt", fc.CAT_NET, True)
    ereignis("pruefung", "2026-10-21T16:00:00.000000+00:00", "e-uid-08",
             "ap1", 61, "4 (Ausreichend)", 3600, {"a": 1, "ü": [1, 2]})
    ereignis("pruefung", "2026-10-21T16:30:00.000000+00:00", "e-uid-09",
             "ap2", 87.5, "2 (Gut)", 5400.9, {})

    conn = sqlite3.connect(db.db_path)
    tabellen = {}
    for tabelle, spalten in TABELLEN:
        tabellen[tabelle] = {
            "spalten": spalten,
            "zeilen": [list(r) for r in conn.execute("SELECT %s FROM %s ORDER BY id"
                                                      % (", ".join(spalten), tabelle))],
            "typen": [list(r) for r in conn.execute("SELECT %s FROM %s ORDER BY id"
                                                     % (", ".join("typeof(%s)" % s for s in spalten),
                                                        tabelle))],
        }
    # Text, den save_exam in daten schrieb (C# bekommt ihn fertig, die Klausur baut ihn in W2)
    for aufruf, daten in zip([a for a in aufrufe if a["art"] == "pruefung"],
                             [r[0] for r in conn.execute("SELECT daten FROM pruefungen ORDER BY id")]):
        aufruf["daten_text"] = daten
    conn.close()
    return {
        "zone": ZONE,
        "aufrufe": aufrufe,
        "tabellen": tabellen,
        "felder": [[projekt, [[k, v] for k, v in felder.items()]]
                   for projekt, felder in db.project_fields().items()],
    }


ZAHL_TEXTE = ["", " ", "25", "25,5", "1.234,56", "1.000", "1.000.000", "01.000", "0.125", "1.5",
              "1.2.3", "5,", ",5", "1,1234", "1e3", "abc", "inf", "nan", "-0", "-0,04", "-12",
              "12 €", "1 000", "1 000,5", "1 000", "999.999,999",
              "123456789012345678901", "٣"]


def faelle():
    satz = "Wort (Klammer) \\ Pfad → ≤ ✓ ﬁ ń 中 € "
    voll = (satz * 100)[:3000]
    lang = [("titel", "x" * 50)]
    for key in ("bezeichnung", "ausgangssituation", "ziel", "umfeld", "alternativen", "systeme",
                "einfuehrung", "sicherheit", "praesentationsmittel"):
        lang.append((key, voll))
    for key in ("einleitung", "ist", "soll", "wirtschaft", "durchfuehrung", "test", "uebergabe", "fazit"):
        lang.append(("kapitel_" + key, voll + "\n\t" + "W" * 200 if key == "ist" else voll))
    return [
        ("leer", []),
        ("festwert", [("titel", "Testprojekt"), ("kosten_einsparung", "500"), ("kosten_laufend", "40"),
                      ("material_name_0", "Server"), ("material_betrag_0", "4999"),
                      ("material_name_1", "Lizenz"), ("material_betrag_1", "1234,5")]),
        ("warnung", [("phase_dokumentation", "12"), ("phase_durchfuehrung", "20")]),
        ("halb", [("phase_analyse", "3"), ("phase_planung", "0"), ("phase_durchfuehrung", "0"),
                  ("phase_test", "0"), ("phase_uebergabe", "0"), ("phase_dokumentation", "5")]),
        ("frei", [("phase_name_frei1", "Puffer"), ("phase_frei1", "2"), ("phase_frei2", "3"),
                  ("phase_analyse", "0,25"), ("phase_test", "-0,04"), ("kosten_satz_azubi", ""),
                  ("kosten_stunden_ma", "abc"), ("material_name_2", "  "), ("material_betrag_2", "0"),
                  ("material_betrag_3", "-0"), ("material_name_4", "Kabel"), ("material_betrag_4", ""),
                  ("material_betrag_0", "0,125")]),
        ("unicode", [("titel", " Titel \U0001F600 äöü\x1c"), ("kapitel_ist", "\U0001F600" * 2800),
                     ("schaetzung", " 85,5 "), ("fachbereich", "Netzwerk & Protokolle"),
                     ("check_antrag_entwurf", "1"), ("check_max40", "1"), ("check_p_dauer", "0"),
                     ("fg_a01", "1"), ("fg_a02", "0"), ("fg_a03", ""), ("bezeichnung", "B"),
                     ("ziel", "  "), ("seitenziel", "2,7")]),
        ("gross", [("kosten_einsparung", "1.000"), ("kosten_laufend", "1.200"),
                   ("kosten_satz_azubi", "1.234,5"), ("phase_durchfuehrung", "1.000"),
                   ("material_name_0", "Viel"), ("material_betrag_0", "999.999,999"),
                   ("seitenziel", "-3"), ("schaetzung", "150")]),
        ("lang", lang),
    ]


def rechnen_werte(fpj):
    liste = faelle()
    je_fall = []
    for name, paare in liste:
        felder = dict(paare)
        zeilen, summe = fpj.schedule(felder)
        kosten = fpj.costs(felder)
        je_fall.append([name, {
            "zeitplan": [list(z) for z in zeilen],
            "summe": summe,
            "warnungen": fpj.schedule_warnings(felder),
            "kosten": {"stunden": kosten["stunden"], "personal": kosten["personal"],
                       "material": [list(m) for m in kosten["material"]],
                       "material_summe": kosten["material_summe"], "gesamt": kosten["gesamt"],
                       "netto_monat": kosten["netto_monat"], "amortisation": kosten["amortisation"]},
            "kostenzeilen": fpj.cost_lines(felder),
            "seiten": list(fpj.page_estimate(felder)),
            "erledigt": list(fpj.overview_progress(felder)),
            "antrag_offen": fpj.proposal_missing(felder),
            "schaetzung": fpj.estimate(felder),
            "titel": fpj.project_title(felder),
            "dateiname": [fpj.export_name(felder, "pdf"), fpj.export_name(felder, "txt")],
        }])
    titel = ["Mein Projekt: Netz/WLAN äöü \U0001F600", "", "\U0001F600", "x" * 50,
             "  Ä-ß_1 ", "Ä"]
    return {
        "faelle": [[name, [list(p) for p in paare]] for name, paare in liste],
        "je_fall": je_fall,
        "stunden_text": [[x, fpj.hours_text(x)] for x in (0, 0.25, 2.04, -0.04, 12.5, 0.05, 40, 36.21, 1026)],
        "euro": [[x, fpj.euro(x)] for x in (0, 0.125, 0.005, 1234.5, -1234.5, 999999.999, 1e20)],
        "dateiname": [[t, [fpj.export_name({"titel": t}, "pdf"), fpj.export_name({"titel": t}, "txt")]]
                      for t in titel],
    }


def export_werte(fpj):
    ergebnis = []
    for name, paare in faelle():
        felder = dict(paare)
        text = fpj.export_text(felder)
        eintrag = {"bloecke": [list(b) for b in fpj.export_blocks(felder)], "text": text}
        if name in ("leer", "unicode"):
            # wie app_gui _export (PC) und main.py (Handy, ohne BOM, LF)
            eintrag["datei_pc"] = text.replace("\n", "\r\n").encode("utf-8-sig").hex()
            eintrag["datei_handy"] = text.encode("utf-8").hex()
        ergebnis.append([name, eintrag])
    return ergebnis


def projekte_werte(tmp):
    import fisi_core as fc
    import fisi_projekt as fpj
    zeit = [None]
    zaehler = [0]

    def uid():
        zaehler[0] += 1
        return "u%02d" % zaehler[0]

    fc.DBManager._now_fine = staticmethod(lambda: zeit[0])
    fc.DBManager._uid = staticmethod(uid)
    db = fc.DBManager(os.path.join(tmp, "projekte.db"))

    def setze(uhrzeit):
        zeit[0] = "2026-10-22 " + uhrzeit

    ergebnisse = []
    for uhrzeit, projekt, titel in (("08:00:00.000001", "p-b", "Beta"), ("08:00:00.000002", "p-a", "alpha"),
                                    ("08:00:00.000003", "p-c", ""), ("08:00:00.000004", "p-d", "Delta")):
        setze(uhrzeit)
        db.save_project_field(projekt, "titel", titel)
    setze("08:00:00.000005")
    db.save_project_field("p-d", "ziel", "Text D")
    setze("08:01:00.000000")
    ergebnisse.append(fpj.delete_project(db, "p-d"))
    setze("08:02:00.000000")
    ergebnisse.append(fpj.apply_template(db, "p-c", 0))
    setze("08:02:30.000000")
    ergebnisse.append(fpj.apply_template(db, "p-c", 0))
    setze("08:03:00.000000")
    ergebnisse.append(fpj.apply_template(db, "p-a", 49))
    setze("08:04:00.000000")
    ergebnisse.append(fpj.delete_project(db, "p-b"))
    for uhrzeit, projekt, titel in (("08:05:00.000001", "p-h", "Alpha"), ("08:05:00.000002", "p-e", "Äpfel"),
                                    ("08:05:00.000003", "p-f", "\U0001F600 Emoji"),
                                    ("08:05:00.000004", "p-g", " Privat")):
        setze(uhrzeit)
        db.save_project_field(projekt, "titel", titel)
    projekte = fpj.projects(db)
    # Menue wie app_gui _fill_project_menu (sorted nach project_title(...).lower())
    menue = sorted(projekte, key=lambda key: fpj.project_title(projekte[key]).lower())
    aktiv = [fpj.active_project(db, e) for e in
             ({}, {fpj.ACTIVE_KEY: "p-d"}, {fpj.ACTIVE_KEY: "p-c"}, {fpj.ACTIVE_KEY: "p-b"})]
    leer = fc.DBManager(os.path.join(tmp, "leer.db"))
    neu = fpj.active_project(leer, {})
    assert neu not in fpj.projects(leer) and len(neu) == 12
    aktiv.append("neu")
    conn = sqlite3.connect(db.db_path)
    zeilen = [list(r) for r in conn.execute(
        "SELECT timestamp, projekt, feld, wert, uid FROM abschlussprojekt ORDER BY id")]
    conn.close()
    return {
        "zeilen": zeilen,
        "ergebnisse": ergebnisse,
        "projekte": [[p, [list(f) for f in felder.items()]] for p, felder in projekte.items()],
        "menue": menue,
        "titel": [[p, fpj.project_title(felder)] for p, felder in projekte.items()],
        "aktiv": aktiv,
    }


def fragen_werte(fpj, fc):
    auswahl = []
    for fachbereich in [""] + list(fc.CATEGORIES) + ["Unbekannt"]:
        auswahl.append([fachbereich, [q["id"] for q in fpj.questions_for({"fachbereich": fachbereich})]])
    bewertung = []
    for wert in (None, "", "1", "0", "2", "ja"):
        felder = {} if wert is None else {"fg_a01": wert}
        bewertung.append([wert, fpj.question_rating(felder, "a01")])
    return {"auswahl": auswahl, "bewertung": bewertung}


def rueckfrage_werte(fpj):
    return [[t, s, fpj.delete_question({"titel": t}, synced=s)]
            for t in ("X", "", " \U0001F600 ") for s in (True, False)]


def pdf_zerlegen(daten):
    """Objekte ueber die xref-Stellen; Stroeme entpackt."""
    xref = int(daten[daten.rindex(b"startxref\n") + 10:].split(b"\n")[0])
    teile = daten[xref:].split(b"\n")
    anzahl = int(teile[1].split(b" ")[1])
    stellen = [int(t[:10]) for t in teile[3:3 + anzahl - 1]]
    objekte, inhalte = [], []
    for nummer, stelle in enumerate(stellen, start=1):
        ende = stellen[nummer] if nummer < len(stellen) else xref
        roh = daten[stelle:ende]
        kopf = b"%d 0 obj\n" % nummer
        assert roh.startswith(kopf) and roh.endswith(b"\nendobj\n"), nummer
        inhalt = roh[len(kopf):-len(b"\nendobj\n")]
        if b"\nstream\n" in inhalt:
            vor, strom = inhalt.split(b"\nstream\n", 1)
            assert strom.endswith(b"\nendstream")
            inhalte.append(zlib.decompress(strom[:-len(b"\nendstream")]).decode("latin-1"))
            vor = re.sub(rb"/Length \d+", b"/Length #", vor)
            objekte.append(vor.decode("latin-1"))
        else:
            objekte.append(inhalt.decode("latin-1"))
    return objekte, inhalte


def pdf_werte(fpj):
    import fisi_pdf

    class Tag(datetime.date):
        @classmethod
        def today(cls):
            return cls(2026, 10, 14)

    class Uhr:
        date = Tag

    alt = fisi_pdf.datetime
    fisi_pdf.datetime = Uhr
    try:
        ergebnis = []
        for name, paare in faelle():
            if name not in ("leer", "festwert", "unicode", "lang"):
                continue
            felder = dict(paare)
            daten = fpj.export_pdf(felder)
            objekte, inhalte = pdf_zerlegen(daten)
            ergebnis.append([name, {"seiten": len(inhalte), "inhalte": inhalte, "objekte": objekte,
                                    "kopf": daten[:15].hex(), "info": objekte[-1]}])
        return ergebnis
    finally:
        fisi_pdf.datetime = alt


def texte_werte(fpj):
    return {
        "reiter": [list(t) for t in fpj.TABS],
        "uebersicht": [[k, n] for k, n, _m in fpj.OVERVIEW_FIELDS],
        "antrag": [list(t) for t in fpj.PROPOSAL_FIELDS],
        "phasen": [list(t) for t in fpj.PHASES],
        "kosten": [list(t) for t in fpj.COST_FIELDS],
        "kapitel": [list(t) for t in fpj.CHAPTERS],
        "checklisten": [[list(t) for t in liste] for liste in
                        (fpj.CHECKLIST_OVERVIEW, fpj.CHECKLIST_PROPOSAL, fpj.CHECKLIST_PRESENTATION)],
        "zahlen": {"max_stunden": fpj.MAX_HOURS, "doku_anteil": fpj.DOC_SHARE_MAX,
                   "zeichen_je_seite": fpj.CHARS_PER_PAGE, "seiten": fpj.DEFAULT_PAGES,
                   "sachkosten_zeilen": fpj.MATERIAL_ROWS},
        "rueckfrage_titel": fpj.DELETE_TITLE,
    }


def grenze_werte(fpj):
    """Python mit gesetzter Grenze (Nachtrag P4-9): MAX_HOURS und der Text von max40."""
    alt_max, alt_liste = fpj.MAX_HOURS, fpj.CHECKLIST_PROPOSAL
    ergebnis = []
    try:
        for grenze in (40, 80):
            fpj.MAX_HOURS = grenze
            fpj.CHECKLIST_PROPOSAL = [(k, "Summe höchstens %d Stunden inklusive Dokumentation" % grenze
                                       if k == "max40" else t) for k, t in alt_liste]
            for soll in (30, 40, 41, 80, 81):
                felder = {"phase_durchfuehrung": str(soll - 26)}
                _zeilen, summe = fpj.schedule(felder)
                ergebnis.append([grenze, soll, {
                    "felder": [list(p) for p in felder.items()], "summe": summe,
                    "warnungen": fpj.schedule_warnings(felder),
                    "bloecke": [list(b) for b in fpj.export_blocks(felder)],
                    "text": fpj.export_text(felder)}])
    finally:
        fpj.MAX_HOURS, fpj.CHECKLIST_PROPOSAL = alt_max, alt_liste
    assert alt_liste[1] == ("max40", "Summe höchstens 40 Stunden inklusive Dokumentation")
    return ergebnis


def zeichen_werte():
    """fisi_pdf._clean fuer jeden einzelnen Codepunkt ohne Surrogate."""
    import fisi_pdf
    laeufe, einzeln = [], []
    pruef = hashlib.sha256()
    for c in range(0x110000):
        if 0xD800 <= c <= 0xDFFF:
            continue
        zeichen = chr(c)
        text = fisi_pdf._clean(zeichen)
        pruef.update((text + "\x00").encode("utf-8"))
        art = "gleich" if text == zeichen else "leer" if text == "" else "frage" if text == "?" else None
        if art is None:
            einzeln.append([c, text])
        elif laeufe and laeufe[-1][2] == art and laeufe[-1][1] == c - 1:
            laeufe[-1][1] = c
        else:
            laeufe.append([c, c, art])
    return {"unicode": unicodedata.unidata_version, "laeufe": laeufe, "einzeln": einzeln,
            "sha256": pruef.hexdigest()}


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    os.environ["TZ"] = ZONE
    time.tzset()
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    daten = {
        "quelle": "fisi_core DBManager.save_project_field, purge_superseded_project_rows, "
                  "project_fields, log_ap1, log_scenario, log_project, save_exam; "
                  "fisi_projekt.delete_project (Python v0.63.2)",
        "python": sys.version.split()[0],
        "speichern": speichern_werte(tmp),
    }
    import fisi_core as fc
    import fisi_projekt as fpj
    daten["zahl"] = [[t, v, fpj.to_number(t, v)] for t in ZAHL_TEXTE for v in (0.0, 15)]
    daten["rechnen"] = rechnen_werte(fpj)
    daten["export"] = export_werte(fpj)
    daten["projekte"] = projekte_werte(tmp)
    daten["fragen"] = fragen_werte(fpj, fc)
    daten["rueckfrage"] = rueckfrage_werte(fpj)
    daten["pdf"] = pdf_werte(fpj)
    daten["texte"] = texte_werte(fpj)
    daten["grenze_quelle"] = "Python mit gesetzter Grenze (fisi_projekt.MAX_HOURS und Text max40 von aussen)"
    daten["grenze"] = grenze_werte(fpj)
    daten["zeichen"] = zeichen_werte()
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    shutil.rmtree(tmp, ignore_errors=True)
    print("G34: %d Aufrufe, %d Projektzeilen -> %s"
          % (len(daten["speichern"]["aufrufe"]),
             len(daten["speichern"]["tabellen"]["abschlussprojekt"]["zeilen"]), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
