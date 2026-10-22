#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G33 (Phase 4, Welle 1, Paket PB2 "Notizblock- und
Listen-Kern"), gerechnet vom echten Python-Code der Python-Linie (v0.63.2,
eingefroren). Grundlage ist eine Kopie von tests/Vergleich/testdaten.db mit
den Zusatzzeilen "zusatz" (Karteikarten, Pruefungsfragen, AP1, AP2,
Testprojekte; rot, gelb, abgeschlossen, angesehen, gleiche Zeit, gemischte
Zeitformen). C# liest dieselbe Kopie mit denselben Zusatzzeilen.

  zusatz       - [Tabelle, {Spalte: Wert}] in Einfuegereihenfolge
  status       - fisi_core.status_label und StatusBook.status fuer jede
                 bearbeitete Frage und einige unbearbeitete
  eintraege    - notebook_entries je Filter (Status, Fachbereich, Thema,
                 Quellen). Ein Eintrag ist [Quelle, Nummer in source_items,
                 Status, Stufe, Zeit]
  uebersicht   - notebook_summary je Filter
  suche        - notebook_search auf den Eintraegen "Alle" und "Zu ueben":
                 [Quelle, Nummer] der Treffer je Stichwort (NFC, casefold,
                 strip; Umlaute, ß/SS, zerlegte Umlaute)
  titel_muster - item_title und model_answer je Quelle fuer feste Nummern
  listen       - je Quelle (ap1, ap2, projekt) position_statuses,
                 group_values und filter_positions je Suche, Gruppe, Status
                 und Fachbereich (Gruppenfeld wie app_gui: theme bzw.
                 schwierigkeit), page_slice und PagedList.next_after
                 (app_gui 738, ohne Oberflaeche nachgebaut, Zeilen unten)
  texte        - feste Texte der vier Seiten aus app_gui.py (PC-Wortlaut) und
                 fisi_core.py, mit ast gelesen: je Text Datei, Zeile und
                 Nummer in der Zeile (C#-Name -> Text)

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_notizblock.py <pfad_fisi_lernplattform> tests/Vergleich/testdaten.db tests/Vergleich/notizblock.json
"""
import ast
import json
import os
import shutil
import sqlite3
import sys
import tempfile

# Texte (C#-Name: Datei, Zeile, Nummer des Textes in der Zeile)
TEXTE = {
    "SzenarienTexte.SucheHinweis": ("app_gui.py", 527, 0),
    "SzenarienTexte.Fachbereich": ("app_gui.py", 535, 1),
    "SzenarienTexte.Thema": ("app_gui.py", 512, 0),
    "SzenarienTexte.Schwierigkeit": ("app_gui.py", 4410, 1),
    "SzenarienTexte.Status": ("app_gui.py", 540, 1),
    "SzenarienTexte.KeineTreffer": ("app_gui.py", 561, 0),
    "SzenarienTexte.Zaehler": ("app_gui.py", 714, 0),
    "SzenarienTexte.Seite": ("app_gui.py", 717, 0),
    "SzenarienTexte.Fertig": ("app_gui.py", 700, 0),
    "SzenarienTexte.StatusTrenner": ("app_gui.py", 706, 0),
    "SzenarienTexte.Aufgaben": ("app_gui.py", 513, 0),
    "SzenarienTexte.Kundenauftraegen": ("app_gui.py", 4411, 0),
    "SzenarienTexte.SeiteZurueck": ("app_gui.py", 160, 0),
    "SzenarienTexte.SeiteWeiter": ("app_gui.py", 160, 1),
    "SzenarienTexte.Ap1Titel": ("app_gui.py", 4363, 0),
    "SzenarienTexte.Ap2Titel": ("app_gui.py", 4353, 0),
    "SzenarienTexte.AnzahlAufgaben": ("app_gui.py", 4188, 0),
    "SzenarienTexte.Aufgabenstellung": ("app_gui.py", 4212, 0),
    "SzenarienTexte.DeineLoesung": ("app_gui.py", 4226, 0),
    "SzenarienTexte.DeineLoesungUnter": ("app_gui.py", 4227, 0),
    "SzenarienTexte.DeineLoesungHinweis": ("app_gui.py", 4232, 0),
    "SzenarienTexte.Musterloesung": ("app_gui.py", 4244, 0),
    "SzenarienTexte.Anzeigen": ("app_gui.py", 4252, 0),
    "SzenarienTexte.Ausblenden": ("app_gui.py", 4335, 0),
    "SzenarienTexte.Naechstes": ("app_gui.py", 4255, 0),
    "SzenarienTexte.NochAusgeblendet": ("app_gui.py", 4316, 0),
    "SzenarienTexte.Ausgeblendet": ("app_gui.py", 4330, 0),
    "TestprojektTexte.ListeTitel": ("app_gui.py", 4394, 0),
    "TestprojektTexte.AnzahlAuftraege": ("app_gui.py", 4395, 0),
    "TestprojektTexte.Kundenauftrag": ("app_gui.py", 4436, 0),
    "TestprojektTexte.Loesungsansaetze": ("app_gui.py", 4443, 0),
    "TestprojektTexte.Anzeigen": ("app_gui.py", 4451, 0),
    "TestprojektTexte.Ausblenden": ("app_gui.py", 4564, 0),
    "TestprojektTexte.Naechstes": ("app_gui.py", 4454, 0),
    "TestprojektTexte.NochAusgeblendet": ("app_gui.py", 4546, 0),
    "TestprojektTexte.Ausgeblendet": ("app_gui.py", 4559, 0),
    "TestprojektTexte.HinweisVorweg": ("app_gui.py", 4522, 0),
    "NotizblockTexte.UebersichtTitel": ("app_gui.py", 5156, 0),
    "NotizblockTexte.UebersichtUnter": ("app_gui.py", 5158, 0),
    "NotizblockTexte.Suche": ("app_gui.py", 5173, 0),
    "NotizblockTexte.SucheHinweis": ("fisi_core.py", 2467, 0),
    "NotizblockTexte.KeineTrefferMuster": ("fisi_core.py", 2468, 0),
    "NotizblockTexte.Fachbereich": ("app_gui.py", 5179, 0),
    "NotizblockTexte.Bereich": ("app_gui.py", 5183, 0),
    "NotizblockTexte.Thema": ("app_gui.py", 5192, 0),
    "NotizblockTexte.Status": ("app_gui.py", 5199, 0),
    "NotizblockTexte.Musterantworten": ("app_gui.py", 5208, 0),
    "NotizblockTexte.AusText": ("app_gui.py", 5209, 1),
    "NotizblockTexte.AnText": ("app_gui.py", 5209, 3),
    "NotizblockTexte.UebenHinweis": ("app_gui.py", 5387, 0),
    "NotizblockTexte.HinweisTitel": ("app_gui.py", 5393, 0),
    "NotizblockTexte.KeineFragenDazu": ("app_gui.py", 5393, 1),
    "NotizblockTexte.NichtsZuUeben": ("app_gui.py", 5421, 0),
    "NotizblockTexte.KeineFragen": ("app_gui.py", 5423, 0),
    "NotizblockTexte.AntwortZeigen": ("app_gui.py", 5467, 0),
    "NotizblockTexte.AntwortAusblenden": ("app_gui.py", 5466, 0),
    "NotizblockTexte.JetztUeben": ("app_gui.py", 5469, 0),
}


def literal(baum, zeile, nummer):
    knoten = sorted((k for k in ast.walk(baum)
                     if isinstance(k, ast.Constant) and isinstance(k.value, str) and k.lineno == zeile),
                    key=lambda k: k.col_offset)
    return knoten[nummer].value


# Feste Nummern in den Inhalten; dazu die erste Karteikarte mit "ß" in der Frage
KARTEN = [0, 3, 7, 12, 40, 41, 42]
QUIZ = [0, 5, 9, 10]
AP1 = [0, 2, 5, 264]
AP2 = [1, 3, 329]
PROJEKT = [0, 4, 49]

# Antwortfolgen (None = angesehen); je Folge die Zeiten
FOLGEN = {
    "rot": [False],
    "gelb": [False, True],
    "fertig": [True, True],
    "angesehen": [None],
    "eins": [True],
    "fertig_dann_angesehen": [True, True, None],
    "rot_nach_fertig": [True, True, False],
}

STICHWORTE = ["", "raid", "RAID", "  Raid\t", "straße", "STRASSE", "Strasse", "ß", "ẞ",
              "über", "ÜBER", "Über", "subnetz", "IPv6", "%", "zz-nichts", "İ", "ﬁ",
              "dhcp", "Richtig:", "Lösungsansatz", "Prüfwert", "äöüß", "netz", "Kunde",
              "firewall", "backup", "vlan", "1", "  "]

LISTEN_SUCHE = ["", "netz", " NETZ ", "1", "15", "265", "330", "50", "ß", "SS", "straße", "İ",
                "zz", "Prüfwert", "server", "  "]


def zeit(nummer):
    """Feste Zeiten in gemischter Form (Leerzeichen, T, mit Zone)."""
    tag = 1 + nummer % 9
    stunde = 8 + nummer % 11
    form = nummer % 3
    if form == 0:
        return "2026-10-%02d %02d:%02d:00" % (tag, stunde, nummer % 60)
    if form == 1:
        return "2026-10-%02dT%02d:%02d:00" % (tag, stunde, nummer % 60)
    return "2026-10-%02d %02d:%02d:00+02:00" % (tag, stunde, nummer % 60)


def zusatz_zeilen(core):
    zeilen = []
    folgen = list(FOLGEN.values())
    nummer = [0]

    def neu():
        nummer[0] += 1
        return nummer[0]

    karten = list(KARTEN)
    for i, karte in enumerate(core.KARTEIKARTEN):
        if "ß" in karte["q"]:
            karten.append(i)
            break
    for n, i in enumerate(karten):
        karte = core.KARTEIKARTEN[i]
        for wert in folgen[n % len(folgen)]:
            zeilen.append(["card_events", {"timestamp": zeit(neu()), "category": karte["cat"],
                                           "question": karte["q"], "mode": "learn",
                                           "correct": None if wert is None else int(wert),
                                           "uid": "nb-%04d" % nummer[0], "utc_offset": 120}])
    for n, i in enumerate(QUIZ):
        frage = core.QUIZ_QUESTIONS[i]
        for wert in folgen[(n + 2) % len(folgen)]:
            if wert is None:
                continue   # quiz_answers.correct ist NOT NULL
            zeilen.append(["quiz_answers", {"timestamp": zeit(neu()), "category": frage["cat"],
                                            "question": frage["q"], "correct": int(wert),
                                            "uid": "nb-%04d" % nummer[0], "utc_offset": 120}])
    for tabelle, spalte, daten, positionen, start in (
            ("ap1_events", "scenario_index", core.AP1_SZENARIEN, AP1, 1),
            ("scenario_events", "scenario_index", core.SZENARIEN, AP2, 3),
            ("project_events", "project_index", core.PROJEKTARBEITEN, PROJEKT, 5)):
        for n, position in enumerate(positionen):
            eintrag = daten[position]
            for wert in folgen[(n + start) % len(folgen)]:
                zeile = {"timestamp": zeit(neu()), spalte: position, "title": eintrag["title"],
                         "correct": None if wert is None else int(wert),
                         "uid": "nb-%04d" % nummer[0], "utc_offset": 120}
                zeile["category" if tabelle == "project_events" else "theme"] = \
                    eintrag["cat"] if tabelle == "project_events" else eintrag["theme"]
                zeilen.append([tabelle, zeile])
    # zwei rote Karteikarten mit gleicher Zeit (stabile Sortierung)
    for i in (100, 101):
        karte = core.KARTEIKARTEN[i]
        zeilen.append(["card_events", {"timestamp": "2026-10-05 12:00:00", "category": karte["cat"],
                                       "question": karte["q"], "mode": "learn", "correct": 0,
                                       "uid": "nb-gleich-%d" % i, "utc_offset": 120}])
    return zeilen


def next_after(position, filtered, anzahl, status, statuses, core):
    """PagedList.next_after (app_gui 738-752) ohne Oberflaeche, Zeile fuer Zeile."""
    if not filtered:
        return (position + 1) % anzahl
    order = [p for p in filtered if p > position] + [p for p in filtered if p <= position]
    if status == core.FILTER_ALL:
        for candidate in order:
            if candidate != position and statuses.get(candidate, (core.Q_OPEN, ""))[0] != core.Q_DONE:
                return candidate
    return order[0]


def main():
    if len(sys.argv) != 4:
        print(__doc__)
        return 2
    repo, quelle, ziel = sys.argv[1], sys.argv[2], sys.argv[3]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as core

    pfad = os.path.join(tmp, "notizblock.db")
    shutil.copyfile(quelle, pfad)
    zusatz = zusatz_zeilen(core)
    conn = sqlite3.connect(pfad)
    for tabelle, werte in zusatz:
        spalten = list(werte)
        conn.execute("INSERT INTO %s (%s) VALUES (%s)" % (tabelle, ", ".join(spalten),
                                                          ", ".join("?" * len(spalten))),
                     [werte[s] for s in spalten])
    conn.commit()
    conn.close()
    db = core.DBManager(pfad)
    book = core.StatusBook(db)

    nummern = {}
    for source in core.SOURCES:
        for nummer, (key, _item) in enumerate(core.source_items(source)):
            nummern.setdefault((source, key), nummer)

    def kurz(entry):
        return [entry["source"], nummern[(entry["source"], entry["key"])], entry["status"],
                entry["level"], entry["time"]]

    # Status je bearbeiteter Frage (auch Schluessel ohne Frage, z.B. aus testdaten.db)
    status = []
    schluessel = list(book.results) + [(core.SRC_CARD, core.KARTEIKARTEN[1]["q"]),
                                      (core.SRC_AP1, 1000), (core.SRC_PROJECT, 1)]
    for source, key in schluessel:
        text, farbe = core.status_label(book, source, key)
        wert, stufe = book.status(source, key)
        status.append([source, key, wert, stufe, text, farbe, book.last_time(source, key)])

    themen = [core.KARTEIKARTEN[KARTEN[0]]["thema"], core.AP1_SZENARIEN[AP1[1]]["thema"]]
    filter_liste = []
    for st in ("Standard", None, core.FILTER_ALL, core.Q_OPEN, core.Q_PRACTICE, core.Q_DONE):
        for cat in (core.FILTER_ALL, core.CAT_NET, core.CAT_DB):
            for topic in [core.FILTER_ALL] + themen:
                for sources in (None, [core.SRC_AP1, core.SRC_AP2], [core.SRC_CARD],
                                [core.SRC_PROJECT, core.SRC_QUIZ]):
                    filter_liste.append((st, cat, topic, sources))
    eintraege = []
    for st, cat, topic, sources in filter_liste:
        argumente = {"category": cat, "topic": topic, "sources": sources}
        if st != "Standard":
            argumente["status"] = st
        liste = core.notebook_entries(book, **argumente)
        eintraege.append({"status": st, "fachbereich": cat, "thema": topic, "quellen": sources,
                          "eintraege": [kurz(e) for e in liste]})

    uebersicht = []
    for cat in (core.FILTER_ALL, core.CAT_NET, core.CAT_SEC):
        for topic in [core.FILTER_ALL] + themen:
            uebersicht.append({"fachbereich": cat, "thema": topic,
                               "werte": core.notebook_summary(book, cat, topic)})

    suche = []
    for st in (core.FILTER_ALL, core.Q_PRACTICE):
        basis = core.notebook_entries(book, status=st)
        for wort in STICHWORTE:
            treffer = core.notebook_search(basis, wort)
            suche.append({"status": st, "anfrage": wort, "schluessel": core.search_key(wort),
                          "treffer": [[e["source"], nummern[(e["source"], e["key"])]] for e in treffer]})

    titel_muster = []
    for source in core.SOURCES:
        items = core.source_items(source)
        for nummer in sorted({0, 1, len(items) - 1, 12 % len(items)}):
            key, item = items[nummer]
            titel_muster.append([source, nummer, core.item_title(source, item),
                                 core.model_answer(source, item)])

    listen = {}
    for source, items, gruppe_feld, mit_fach in (
            (core.SRC_AP1, core.AP1_SZENARIEN, "theme", False),
            (core.SRC_AP2, core.SZENARIEN, "theme", False),
            (core.SRC_PROJECT, core.PROJEKTARBEITEN, "schwierigkeit", True)):
        statuses = core.position_statuses(book, source)
        gruppen = core.group_values(items, gruppe_feld)
        faelle = []
        kategorien = [core.FILTER_ALL] + (list(core.CATEGORIES) if mit_fach else [])
        for anfrage in LISTEN_SUCHE:
            for st in core.STATUS_FILTERS:
                for cat in kategorien:
                    faelle.append([anfrage, cat, core.FILTER_ALL, st,
                                   core.filter_positions(items, anfrage, cat, gruppe_feld,
                                                         core.FILTER_ALL, st, statuses)])
        for gruppe in gruppen[:3] + ["gibt es nicht"]:
            for st in core.STATUS_FILTERS:
                faelle.append(["", core.FILTER_ALL, gruppe, st,
                               core.filter_positions(items, "", core.FILTER_ALL, gruppe_feld,
                                                     gruppe, st, statuses)])
        # ohne Gruppenfeld (Suche nur im Titel)
        ohne_gruppe = [[anfrage, core.filter_positions(items, anfrage)] for anfrage in LISTEN_SUCHE]
        seiten = []
        for fall in faelle[:40:7] + faelle[-3:]:
            for seite in (-1, 0, 1, 2, 3, 99):
                for groesse in (15, 12):
                    sichtbar, gueltig, anzahl = core.page_slice(fall[4], seite, groesse)
                    seiten.append([fall[4], seite, groesse, sichtbar, gueltig, anzahl])
        naechste = []
        for fall in faelle:
            if fall[0] not in ("", "netz", "zz"):
                continue
            for position in sorted({0, 1, 2, 5, len(items) - 1} | set(fall[4][:2])):
                naechste.append([position, fall[4], fall[3],
                                 next_after(position, fall[4], len(items), fall[3], statuses, core)])
        listen[source] = {
            "gruppenfeld": gruppe_feld,
            "stati": sorted([k, v[0], v[1]] for k, v in statuses.items()),
            "gruppen": gruppen,
            "faelle": faelle,
            "ohne_gruppe": ohne_gruppe,
            "seiten": seiten,
            "naechste": naechste,
        }

    baeume = {}
    texte = {}
    for name, (datei, zeile, nummer) in TEXTE.items():
        if datei not in baeume:
            with open(os.path.join(repo, datei), encoding="utf-8") as handle:
                baeume[datei] = ast.parse(handle.read())
        texte[name] = literal(baeume[datei], zeile, nummer)

    daten = {
        "quelle": "fisi_core StatusBook, status_label, notebook_entries, notebook_summary, "
                  "notebook_search, search_key, item_title, model_answer, source_items, "
                  "position_statuses, group_values, filter_positions, page_slice; "
                  "app_gui PagedList.next_after (Python v0.63.2)",
        "python": sys.version.split()[0],
        "konstanten": {"LIST_PAGE_SIZE": core.LIST_PAGE_SIZE, "STATUS_FILTERS": core.STATUS_FILTERS,
                       "Q_STATUS_NAME": core.Q_STATUS_NAME, "SOURCES": core.SOURCES},
        "zusatz": zusatz,
        "status": status,
        "eintraege": eintraege,
        "uebersicht": uebersicht,
        "suche": suche,
        "titel_muster": titel_muster,
        "listen": listen,
        "texte": texte,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    shutil.rmtree(tmp, ignore_errors=True)
    print("G33: %d Zusatzzeilen, %d Status, %d Filter, %d Suchen, %d Listenfaelle -> %s"
          % (len(zusatz), len(status), len(eintraege), len(suche),
             sum(len(v["faelle"]) for v in listen.values()), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
