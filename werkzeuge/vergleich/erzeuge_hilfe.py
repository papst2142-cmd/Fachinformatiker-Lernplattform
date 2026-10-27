#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G35 (Phase 4, Welle 4: Hilfe, Rundgang, Einrichten),
gerechnet vom echten Python-Code der Python-Linie (v0.63.2, eingefroren):
fisi_hilfe, fisi_rahmenplan, fisi_update (load_settings/save_settings) und
fisi_core.DBManager._execute (fuer db_has_data).

Teile (Spezifikation P4-10_Spezifikation_G35.md mit Nachtrag P4-11 Abschnitt 3 und 5):

  texte        - Konstanten, Rahmenplan-Texte, Rundgang-Seiten, Hilfe-Abschnitte
                 (Python-Werte, ohne die Paare)
  datum        - parse_date, date_text, check_date fuer feste Faelle
  suche        - search_help ueber die 16 Abschnitte MIT eingesetzten Paaren
  name         - clean_name und greeting fuer JSON-Werte (str(x or ""))
  einstellungen- rp_settings, save_setup (Faelle 1-5), save_rp_settings, mark_tour_seen
  rundgang     - db_has_data, tour_seen, tour_due fuer 5 Datenbanken und 7 Merker
  oberflaeche  - Namen der Texte je W4-Ansicht (PC = Handy, ausser B13)
  paare        - die feste Tabelle "alt -> neu" der C#-Linie (Nachtrag Abschnitt 5)

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_hilfe.py <pfad_fisi_lernplattform> tests/Vergleich/hilfe.json
"""
import atexit
import datetime
import json
import os
import shutil
import sqlite3
import sys
import tempfile
import types

# Feste Tabelle der C#-Linie (Nachtrag P4-11 Abschnitt 5, freigegeben mit Weisung P4-11)
REL_PY = "github.com/papst2142-cmd/Fisi-Lernplattform/releases"
REL_CS = "github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases"
PAARE = [
    {"nr": "N1", "thema": "abgleich", "alt": "Namen fisi-lernstand.", "neu": "Namen Fi-Lernstand."},
    {"nr": "N1", "thema": "abgleich", "alt": "deinname/fisi-lernstand", "neu": "deinname/Fi-Lernstand"},
    {"nr": "N2", "thema": "schutzprogramm", "alt": REL_PY, "neu": REL_CS},
    {"nr": "N2", "thema": "update_schutz", "alt": REL_PY, "neu": REL_CS},
    {"nr": "W1", "thema": "update",
     "alt": "\n\nUnter Linux legt „Desktop-Verknüpfung anlegen“ eine Verknüpfung auf dem "
            "Desktop an oder erneuert sie, zum Beispiel nachdem du das AppImage verschoben hast.",
     # Phase 6 W2 (Weisung P6-7, Nachtrag Nico 23.10.2026 20:07): W2-12, Wortliste Spalte "C# a"
     "neu": "\n\nUnter Linux legt „Verknüpfung anlegen“ eine Verknüpfung im Anwendungsmenü und "
            "auf dem Desktop an oder erneuert sie, zum Beispiel nachdem du das AppImage verschoben hast."},
    {"nr": "W2", "thema": "haenger",
     "alt": "\n\nAb Version 0.59.2 unter Linux: Hängt das Programm, öffne ein Terminal und "
            "gib diesen Befehl ein:\nkill -USR1 $(pgrep -i -x -o fisi-lernplattf)\nDas Programm schreibt "
            "dann in die Datei haenger.log, an welcher Stelle es gerade steht. Es wird dabei nicht beendet. "
            "Achtung: In Version 0.59.1 und älter beendet derselbe Befehl das Programm. In den "
            "„Optionen“ unter „Diagnose und Werkzeuge“ kopiert „Befehl kopieren“ "
            "ihn für dich.",
     "neu": ""},
    {"nr": "W3", "thema": "haenger",
     "alt": "Unter Linux mit IBus schaltet das Programm für sich die IBus-Eingabe ab (X11 ab 0.60, "
            "bisher nur im Test geprüft), sonst kann es einfrieren. Umlaute, ß und AltGr gehen "
            "weiter. Wer IBus braucht, startet es mit\nFISI_XIM=1 fisi-lernplattform\nMit FISI_XIM=1 kann "
            "das Programm wieder langsamer sein oder hängen.",
     "neu": "Hängt unter Linux das Tippen, startest du das Programm mit dem Startschalter\n"
            "--ohne-eingabemethode\nDann nutzt es keine Eingabemethode, zum Beispiel IBus."},
    {"nr": "W4", "thema": "tastatur",
     "alt": " Die Spielkarte und die Grundrisse im Spiel brauchen noch die Maus.", "neu": ""},
    # Phase 7 W6 (Weisung P7-11, Hilfe-Abschnitt Spiel): Satz der Stadt, freigegeben mit Weisung P7-9
    {"nr": "S1", "thema": "spiel",
     "alt": "Der Lernfortschritt gilt für alle gemeinsam.",
     "neu": "Der Lernfortschritt gilt für alle gemeinsam.\n\nIm Spiel vergeht nur Spielzeit: Jeder Schritt kostet Minuten, die echte Uhr spielt keine Rolle."},
    # Phase 8 W2 (Weisung P8-3, Sammelliste 72, Nico 23.10.2026): "Vorladen" streichen, die C#-Linie laedt nicht vor
    {"nr": "W5", "thema": "haenger", "alt": ", Vorladen)", "neu": ")"},
    # Phase 8 W5 (Weisung P8-7 B2, Plan Phase 8 Abschnitt 4 und 11, Nico 4a): Stadt in 3D mit Kaufhaus und Bett (H1),
    # Spielstand im Abgleich (H2), Knopf "Lizenzen" in "Über das Programm" (L1)
    {"nr": "H1", "thema": "spiel",
     "alt": "die echte Uhr spielt keine Rolle.",
     "neu": "die echte Uhr spielt keine Rolle.\n\nDu bewegst dich durch eine Stadt in 3D: Tippe oder klicke auf einen Weg oder ein Haus, dann geht deine Figur dorthin. Im Möbelhaus und im Elektromarkt kaufst du ein, im Kaufhaus Kleidung für deine Figur. In der Bank nimmst du Kredite auf, auf dem Amt gründest du deine Firma. Die Häuser haben Öffnungszeiten, ab 20 Uhr geht es nur noch nach Hause. Zum Feierabend gehst du zu Hause ins Bett. Kann dein Gerät keine 3D-Grafik zeigen, stehen die Orte in einer Liste."},
    {"nr": "H2", "thema": "spiel",
     "alt": "Der Lernfortschritt gilt für alle gemeinsam.",
     "neu": "Der Lernfortschritt gilt für alle gemeinsam. Mit eingerichtetem Abgleich spielst du denselben Spielstand auf PC und Handy weiter."},
    {"nr": "L1", "thema": "rechtliches",
     "alt": "„Lizenz“ und „Hinweise zu Fremdbestandteilen“ (Bestandteile anderer Hersteller und ihre Lizenzen).",
     "neu": "„Lizenz“, „Hinweise zu Fremdbestandteilen“ (Bestandteile anderer Hersteller und ihre Lizenzen) und „Lizenzen“ (die vollständigen Lizenztexte dieser Bestandteile)."},
    {"nr": "N2", "thema": "RELEASE_PAGE_TEXT", "alt": REL_PY, "neu": REL_CS},
]

KONSTANTEN = ["TOUR_TITLE", "TOUR_STEP", "TOUR_SKIP", "TOUR_BACK", "TOUR_NEXT", "TOUR_FINISH", "TOUR_KEYS",
              "SETUP_NAME", "SETUP_NAME_HINT", "SETUP_PLAN", "SETUP_PLAN_HINT", "SETUP_DATES", "SETUP_DATES_HINT",
              "GREETING", "NAME_OPTION_HINT", "OPTIONS_TITLE", "OPTIONS_SUBTITLE", "OPTIONS_TEXT", "BTN_TOUR",
              "BTN_HELP", "HELP_TITLE", "HELP_SUBTITLE", "HELP_INTRO", "SEARCH_KIND", "UPDATE_PROTECTION_HINT"]
RAHMENPLAN = ["OPTIONS_TITLE", "OPTIONS_SUBTITLE", "WEIGHT_OPTION_TEXT", "WEIGHT_OPTION_HINT", "FIELD_OPTION_TEXT",
              "FIELD_OPTION_OFF", "AP1_DATE_TEXT", "AP2_DATE_TEXT", "DATE_HINT", "DATE_INVALID"]

DATUM = ["", " ", "1.2.2027", " 1.2.2027 ", "01.02.2027", "2027-02-01", "2027-2-1", "2027-02- 1", " 1.02.2027",
         "01. 2.2027", "31.02.2027", "29.02.2027", "29.02.2028", "01.13.2027", "01.01.0000", "01.01.0001", "1.2.0999",
         "31.12.9999", "1.1.99", "01.02.20271", "1.2.2027.", "2027-02-01T00", "1.2.２０２７",
         "١.٢.٢٠٢٧", "1.2.2027 ", "1.2.2027\x1c", "﻿1.2.2027",
         "1.2.2027​", "15.6.2027"]

SUCHE = ["", "   ", "rundgang", "RUNDGANG", "Abgleich", "github_pat_", "ß", "GRÖSSE", "größe",
         "İ", "Prüfung (AP1", "„Optionen“", "kill -USR1", "Fisi-Lernplattform", "\tHilfe\n",
         "\x1chilfe", "Bitweiche", "zzz", "SHA-256", "a b", "Akku  berechnen", "Optionen\nSicherung", "Fine-grained",
         "ki-hilfsmittel", "Esc", "e",
         # Nachtrag P4-11 Abschnitt 3
         "fisi", "eingabemethode", "Vorladen", "Desktop", "Fachinformatiker-Lernplattform/releases", "Spielkarte"]

NAMEN = [None, "", "  Alex   Muster ", "a\u001cb\u001fc", "y" * 80, "\U0001F600" * 41, " Kim Lee​",
         123, 0, True, False, 1.0, [1], {"a": 1}, [], "Ünal Şahin"]

RP_FAELLE = [{}, {"rp_abschnitt_b": "ja", "rp_gewichtung": 0, "rp_lernfeld": "LF 10a", "rp_termin_ap1": 20270101,
                  "rp_termin_ap2": "1.2.2028"},
             {"rp_abschnitt_e": [], "rp_lernfeld": 5, "rp_termin_ap2": "2028-02-30"},
             {"rp_termin_ap1": "1.2.2027\u001c", "rp_termin_ap2": "1.2.２０２７"}]

SETUP_FAELLE = [
    (None, ("  Alex  ", {"B": True, "D": False, "E": True}, "29.09.2027", "26.04.2028")),
    ({"darstellung": "hell", "rp_abschnitt_d": True, "x": [1]}, ("Kim", {"B": True}, "", " ")),
    ({"darstellung": "hell"}, ("Kim", {"B": True}, "31.02.2027", "1.1.2028")),
    ({"rp_abschnitt_b": "ja", "rp_termin_ap1": "kaputt", "rp_lernfeld": "LF 99"}, ("", {}, "1.2.2027", "2028-4-26")),
    (None, ("x", {"B": True}, "", "29.02.2027")),
]

MERKER = [{}, {"rundgang_gesehen": "2026-10-22"}, {"rundgang_gesehen": ""}, {"rundgang_gesehen": 0},
          {"rundgang_gesehen": True}, {"rundgang_gesehen": []}, {"rundgang_gesehen": "0"}]

# Teil oberflaeche: Texte je Ansicht in der Reihenfolge der Oberflaeche (app_gui 9097-9187, 7266-7287,
# 7397-7437, 8402-8417; main.py 4204-4258, 4979-5089), nur die Namen; PC = Handy ausser B13
ANSICHTEN = {
    "rundgang_kopf": ["k:TOUR_TITLE", "k:TOUR_STEP"],
    "rundgang_knoepfe": ["k:TOUR_SKIP", "k:TOUR_BACK", "k:TOUR_NEXT", "k:TOUR_FINISH"],
    "einrichten": ["k:SETUP_NAME", "k:SETUP_NAME_HINT", "k:SETUP_PLAN", "r:SECTION_OPTION_TEXT.B",
                   "r:SECTION_OPTION_TEXT.D", "r:SECTION_OPTION_TEXT.E", "k:SETUP_PLAN_HINT", "k:SETUP_DATES",
                   "r:AP1_DATE_TEXT", "r:AP2_DATE_TEXT", "k:SETUP_DATES_HINT", "r:DATE_INVALID"],
    "hilfe_kopf": ["k:HELP_TITLE", "k:HELP_SUBTITLE", "k:HELP_INTRO", "k:BTN_TOUR"],
    "optionen_rundgang": ["k:OPTIONS_TITLE", "k:OPTIONS_SUBTITLE", "k:OPTIONS_TEXT", "k:BTN_TOUR", "k:BTN_HELP",
                          "k:SETUP_NAME", "k:SETUP_NAME_HINT", "k:NAME_OPTION_HINT"],
    "optionen_rahmenplan": ["r:OPTIONS_TITLE", "r:NUR_DIESES_GERAET", "r:OPTIONS_SUBTITLE",
                            "r:SECTION_OPTION_TEXT.B", "r:SECTION_OPTION_TEXT.D", "r:SECTION_OPTION_TEXT.E",
                            "r:WEIGHT_OPTION_TEXT", "r:WEIGHT_OPTION_HINT", "r:FIELD_OPTION_TEXT",
                            "r:FIELD_OPTION_OFF", "r:AP1_DATE_TEXT", "r:AP2_DATE_TEXT", "r:DATE_INVALID"],
}


def lies(pfad):
    if not os.path.exists(pfad):
        return None
    with open(pfad, encoding="utf-8") as datei:
        return json.load(datei)


def schreibe(pfad, inhalt):
    if os.path.exists(pfad):
        os.remove(pfad)
    if inhalt is not None:
        with open(pfad, "w", encoding="utf-8") as datei:
            json.dump(inhalt, datei)


def main():
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp()
    atexit.register(shutil.rmtree, tmp, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as fc
    import fisi_hilfe as fh
    import fisi_rahmenplan as frp
    import fisi_update as fu

    einst = os.path.join(tmp, "einstellungen.json")
    assert fu._settings_path() == einst

    texte = {
        "konstanten": {name: getattr(fh, name) for name in KONSTANTEN},
        "rahmenplan": dict({name: getattr(frp, name) for name in RAHMENPLAN},
                           SECTION_OPTION_TEXT=dict(frp.SECTION_OPTION_TEXT),
                           NUR_DIESES_GERAET="nur für dieses Gerät"),
        "seiten": [{"id": s["id"], "titel": s["titel"], "text": s["text"]} for s in fh.TOUR_PAGES],
        "abschnitte": [{"id": s["id"], "titel": s["titel"], "reiter": list(s["reiter"]), "text": s["text"]}
                       for s in fh.HELP_SECTIONS],
        "options_subtitle": fh.OPTIONS_SUBTITLE,
        "release_page_text": fh.RELEASE_PAGE_TEXT,
    }

    def iso(text):
        tag = frp.parse_date(text)
        return tag.isoformat() if tag else None

    datum = {fall: [iso(fall), frp.date_text(fall), list(fh.check_date(fall))] for fall in DATUM}

    # Suche ueber die Abschnitte mit eingesetzten Paaren (Nachtrag P4-11 Abschnitt 3)
    original = fh.HELP_SECTIONS
    eingesetzt = []
    for abschnitt in original:
        neu = dict(abschnitt)
        for paar in PAARE:
            if paar["thema"] == abschnitt["id"]:
                assert neu["text"].count(paar["alt"]) == 1, (paar["nr"], abschnitt["id"])
                neu["text"] = neu["text"].replace(paar["alt"], paar["neu"])
        eingesetzt.append(neu)
    assert fh.RELEASE_PAGE_TEXT == REL_PY
    fh.HELP_SECTIONS = eingesetzt
    suche = {fall: [list(t) for t in fh.search_help(fall)] for fall in SUCHE}
    fh.HELP_SECTIONS = original

    name = [[wert, fh.clean_name(str(wert or "")), fh.greeting(str(wert or ""))] for wert in NAMEN]

    rp = [frp.rp_settings(fall) for fall in RP_FAELLE]
    faelle = []
    for start, aufruf in SETUP_FAELLE:
        schreibe(einst, start)
        fehler = fh.save_setup(*aufruf)
        faelle.append([fehler, lies(einst), fh.setup_values()])
    schreibe(einst, {"a": 1})
    frp.save_rp_settings(rp_abschnitt_b=True, foo=1, rp_lernfeld="LF 3")
    rp_speichern = lies(einst)
    fh.mark_tour_seen(datetime.date(2026, 10, 22))
    merker = lies(einst)
    einstellungen = {"rp_settings": rp, "faelle": faelle, "rp_speichern": rp_speichern, "rundgang_merker": merker}

    def datenbank(name, aufbau):
        pfad = os.path.join(tmp, name + ".db")
        if aufbau == "kaputt":
            with open(pfad, "wb") as datei:
                datei.write(b"x" * 100)
        else:
            verbindung = sqlite3.connect(pfad)
            for sql in aufbau:
                verbindung.execute(sql)
            verbindung.commit()
            verbindung.close()
        # db_has_data ruft nur db._execute (fisi_core.DBManager._execute, unveraendert);
        # DBManager selbst wuerde beim Oeffnen Tabellen anlegen
        attrappe = types.SimpleNamespace(read_only_reason=None, refused_writes=0,
                                         get_connection=lambda: sqlite3.connect(pfad), _report=lambda text: None)
        attrappe._execute = types.MethodType(fc.DBManager._execute, attrappe)
        return attrappe

    dbs = {
        "leer": datenbank("leer", []),
        "tabellen": datenbank("tabellen", ["CREATE TABLE t1 (a)", "CREATE TABLE t2 (a)"]),
        "zweite": datenbank("zweite", ["CREATE TABLE t1 (a)", "CREATE TABLE t2 (a)", "INSERT INTO t2 VALUES (1)"]),
        "anfuehrung": datenbank("anfuehrung", ['CREATE TABLE "a""b" (x)', 'INSERT INTO "a""b" VALUES (1)']),
        "kaputt": datenbank("kaputt", "kaputt"),
    }
    rundgang = {
        "daten": {name: fh.db_has_data(db) for name, db in dbs.items()},
        "gesehen": [fh.tour_seen(m) for m in MERKER],
        "faellig": {name: [fh.tour_due(db, m) for m in MERKER] for name, db in dbs.items()},
    }

    oberflaeche = {ansicht: {"pc": namen, "handy": namen} for ansicht, namen in ANSICHTEN.items()}
    oberflaeche["rundgang_tasten"] = {"pc": ["k:TOUR_KEYS"], "handy": []}

    ergebnis = {"python": sys.version.split()[0], "texte": texte, "datum": datum, "suche": suche, "name": name,
                "einstellungen": einstellungen, "rundgang": rundgang, "oberflaeche": oberflaeche, "paare": PAARE}
    with open(ziel, "w", encoding="utf-8", newline="\n") as datei:
        json.dump(ergebnis, datei, ensure_ascii=False, separators=(",", ":"))
        datei.write("\n")


if __name__ == "__main__":
    main()
