#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G12 (Phase 1, K1 Datenmodell Lerninhalte),
gerechnet vom echten Python-Code der Python-Linie:

  1. Echte Inhalte: Ergebnis von fisi_core.validate_content() (ohne den
     Spielteil validate_game_content, Abweichung laut Plan Phase 1; zur
     Information auch mit Spielteil), Zaehlwerte content_totals,
     topic_totals, theme_totals, ap1_theme_totals, Anzahl je Liste,
     Pruefsumme jeder geladenen Liste (nach dem Umwandeln wie load_content)
     und alle automatisch erzeugten Fragen des Pruefungstrainers
     (build_quiz_database).
  2. Absichtlich kaputte Beispiele: je Fall werden die JSON-Dateien in einer
     Kopie veraendert (Anweisungen stehen in der Datei), dann prueft
     validate_content() (ohne Spielteil); die Meldungen sind die Sollwerte.

Die Inhalte werden nur gelesen. Jeder Fall laeuft in einem eigenen
Python-Prozess mit eigener Kopie.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_inhalte_pruefung.py <pfad_fisi_lernplattform> <inhalte_ordner> tests/Vergleich/inhalte_pruefung.json
"""
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile

# Jede Aenderung: [datei, pfad, art, wert]. pfad = Liste aus Schluesseln und
# Indizes. art: "setze" (Wert setzen), "entferne" (Schluessel/Eintrag weg),
# "anhaengen" (an Liste anhaengen), "kopiere" (Wert von Pfad wert holen).
FAELLE = [
    {"name": "karte_feld_leer", "aenderungen": [["karteikarten", [0, "a_full"], "setze", ""]]},
    {"name": "karte_feld_fehlt", "aenderungen": [["karteikarten", [1, "a_full"], "entferne", None]]},
    {"name": "karte_drei_antworten", "aenderungen": [["karteikarten", [2, "options", 3], "entferne", None]]},
    {"name": "karte_antwort_fehlt", "aenderungen": [["karteikarten", [3, "a"], "setze", "gibt es nicht"]]},
    {"name": "karte_doppelt", "aenderungen": [["karteikarten", [5, "q"], "kopiere", [4, "q"]]]},
    {"name": "karte_doppelt_gross", "aenderungen": [["karteikarten", [7, "q"], "setze", "  ABC doppelt  "],
                                                    ["karteikarten", [8, "q"], "setze", "abc DOPPELT"]]},
    {"name": "quiz_gleiche_antworten", "aenderungen": [["quizfragen", [3, "options", 1], "kopiere", [3, "options", 0]]]},
    {"name": "quiz_thema_falsch", "aenderungen": [["quizfragen", [0, "thema"], "setze", "gibtsnicht"]]},
    {"name": "quiz_thema_fehlt", "aenderungen": [["quizfragen", [1, "thema"], "entferne", None]]},
    {"name": "quiz_erklaerung_liste_leer", "aenderungen": [["quizfragen", [2, "exp"], "setze", []]]},
    {"name": "ap2_thema_unbekannt", "aenderungen": [["szenarien_ap2", [0, "theme"], "setze", "Unbekannt"]]},
    {"name": "ap2_thema_block", "aenderungen": [["szenarien_ap2", [1, "theme"], "setze", "Virtualisierung"]]},
    {"name": "ap1_loesung_leer", "aenderungen": [["szenarien_ap1", [0, "solution"], "setze", []]]},
    {"name": "ap1_thema_fehlt", "aenderungen": [["szenarien_ap1", [1, "theme"], "entferne", None]]},
    {"name": "projekt_hinweis_fehlt", "aenderungen": [["projektarbeiten", [0, "hinweise", -1], "entferne", None]]},
    {"name": "projekt_branche_leer", "aenderungen": [["projektarbeiten", [1, "branche"], "setze", ""]]},
    {"name": "projekt_doppelt", "aenderungen": [["projektarbeiten", [2, "title"], "kopiere", [3, "title"]]]},
    {"name": "rahmenplan_doppelt", "aenderungen": [["rahmenplan", ["punkte", 1, "id"], "kopiere", ["punkte", 0, "id"]]]},
    {"name": "rahmenplan_abschnitt", "aenderungen": [["rahmenplan", ["punkte", 4, "abschnitt"], "setze", "Z"]]},
    {"name": "rahmenplan_lernfeld", "aenderungen": [["rahmenplan", ["punkte", 2, "lernfelder"], "anhaengen", "LF 99"]]},
    {"name": "rahmenplan_pruefungsbereich", "aenderungen": [["rahmenplan", ["punkte", 3, "ap2"], "anhaengen", "Sport"]]},
    {"name": "zuordnung_punkt", "aenderungen": [["rahmenplan_zuordnung", ["punkte", 0, "id"], "setze", "Q9z"]]},
    {"name": "zuordnung_thema", "aenderungen": [["rahmenplan_zuordnung", ["punkte", 1, "themen"], "anhaengen", "nix"]]},
    {"name": "zuordnung_standard", "aenderungen": [["rahmenplan_zuordnung", ["standard_punkt", "ipv4"], "setze", "XX"]]},
    {"name": "zuordnung_standard_fehlt", "aenderungen": [["rahmenplan_zuordnung", ["standard_punkt", "grundlagen"], "entferne", None]]},
    {"name": "zuordnung_lernfeld", "aenderungen": [["rahmenplan_zuordnung", ["themen_lernfelder", "ipv4"], "anhaengen", "LF 0"]]},
    {"name": "karte_rahmenplan", "aenderungen": [["karteikarten", [10, "rahmenplan"], "setze", ["Q9z", "A1a"]]]},
    {"name": "mehrere_fehler", "aenderungen": [["karteikarten", [11, "options"], "setze", ["a", "b", "c", "d"]],
                                              ["quizfragen", [12, "q"], "setze", ""],
                                              ["szenarien_ap2", [2, "text"], "setze", []],
                                              ["projektarbeiten", [4, "aufgaben"], "anhaengen", "Zusatz"]]},
]

LISTEN = ["KARTEIKARTEN", "QUIZ_QUESTIONS", "SZENARIEN", "AP1_SZENARIEN", "PROJEKTARBEITEN"]


def wert_text(wert):
    """Kanonischer Text eines Wertes (Inhalte enthalten nur Texte und Listen von Texten)."""
    if isinstance(wert, str):
        return wert
    if isinstance(wert, list):
        return "[" + "\u001f".join(wert_text(x) for x in wert) + "]"
    raise TypeError("unerwarteter Typ %r" % type(wert))


def pruefsumme(items):
    teile = []
    for item in items:
        teile.append("\u001d".join("%s=%s" % (k, wert_text(item[k])) for k in sorted(item)))
    return hashlib.sha256("\u001e".join(teile).encode("utf-8")).hexdigest()


def kind(repo, inhalte, ausgabe, mit_spiel):
    """Laeuft im eigenen Prozess: Inhalte aus inhalte laden, pruefen, Ergebnis schreiben."""
    sys._MEIPASS = os.path.dirname(inhalte)
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_game
    assert core.CONTENT_DIR == inhalte, core.CONTENT_DIR
    ergebnis = {}
    if mit_spiel:
        ergebnis["mit_spiel"] = core.validate_content()
        ergebnis["spiel"] = fisi_game.validate_game_content()
    fisi_game.validate_game_content = lambda content=None: []
    ergebnis["meldungen"] = core.validate_content()
    if mit_spiel:
        ergebnis["zaehlwerte"] = {
            "content_totals": core.content_totals(),
            "topic_totals": core.topic_totals(),
            "theme_totals": core.theme_totals(),
            "ap1_theme_totals": core.ap1_theme_totals(),
        }
        ergebnis["listen"] = {name: {"anzahl": len(getattr(core, name)),
                                     "pruefsumme": pruefsumme(getattr(core, name))}
                              for name in LISTEN}
        anzahl_fest = len(json.load(open(os.path.join(inhalte, "quizfragen.json"), encoding="utf-8")))
        ergebnis["erzeugte_fragen"] = core.QUIZ_QUESTIONS[anzahl_fest:]
        ergebnis["anzahl_quizfragen_datei"] = anzahl_fest
    with open(ausgabe, "w", encoding="utf-8") as handle:
        json.dump(ergebnis, handle, ensure_ascii=False)


def gehe(daten, pfad):
    for teil in pfad:
        daten = daten[teil]
    return daten


def aendere(ordner, aenderungen):
    for datei, pfad, art, wert in aenderungen:
        name = os.path.join(ordner, datei + ".json")
        with open(name, encoding="utf-8") as handle:
            daten = json.load(handle)
        eltern = gehe(daten, pfad[:-1])
        if art == "setze":
            eltern[pfad[-1]] = wert
        elif art == "entferne":
            del eltern[pfad[-1]]
        elif art == "anhaengen":
            eltern[pfad[-1]].append(wert)
        elif art == "kopiere":
            eltern[pfad[-1]] = gehe(daten, wert)
        else:
            raise ValueError(art)
        with open(name, "w", encoding="utf-8") as handle:
            json.dump(daten, handle, ensure_ascii=False, indent=1)


def lauf(repo, quelle, aenderungen, mit_spiel):
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    ziel = os.path.join(tmp, "inhalte")
    shutil.copytree(quelle, ziel)
    aendere(ziel, aenderungen)
    ausgabe = os.path.join(tmp, "ergebnis.json")
    umgebung = dict(os.environ)
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        umgebung[name] = tmp
    subprocess.run([sys.executable, "-I", os.path.abspath(__file__), "--kind", repo, ziel, ausgabe,
                    "1" if mit_spiel else "0"], check=True, env=umgebung)
    with open(ausgabe, encoding="utf-8") as handle:
        ergebnis = json.load(handle)
    shutil.rmtree(tmp)
    return ergebnis


def main():
    if len(sys.argv) == 6 and sys.argv[1] == "--kind":
        kind(sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5] == "1")
        return 0
    if len(sys.argv) != 4:
        print(__doc__)
        return 2
    repo, quelle, ziel = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2]), sys.argv[3]
    echt = lauf(repo, quelle, [], True)
    faelle = []
    for fall in FAELLE:
        ergebnis = lauf(repo, quelle, fall["aenderungen"], False)
        faelle.append({"name": fall["name"], "aenderungen": fall["aenderungen"],
                       "meldungen": ergebnis["meldungen"]})
    daten = {
        "quelle": "fisi_core.validate_content ohne validate_game_content, build_quiz_database, "
                  "content_totals/topic_totals/theme_totals/ap1_theme_totals (Python v0.63.2)",
        "echt": echt,
        "faelle": faelle,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    leer = [f["name"] for f in faelle if not f["meldungen"]]
    print("G12: echte Inhalte %d Meldungen, %d kaputte Faelle (ohne Meldung: %s) -> %s"
          % (len(echt["meldungen"]), len(faelle), leer or "keiner", ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
