#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Phase 9 W2 (Plan Phase 9 Abschnitt 12.1, Nico 29.10.2026 Antwort 2a):
Einteilung der bisherigen Lerninhalte aus inhalte/ in "si" (nur
Systemintegration) und "gemeinsam" (alle Fachrichtungen). Ergebnis ist
inhalte_csharp/lernen/einteilung_bisher.json; die CI erzeugt die Datei
neu und verlangt Byte-Gleichheit.

Regeln (12.4 Punkt 2):
  1. AP1-Szenarien: alle gemeinsam (Teil 1 ist fuer alle Fachrichtungen gleich).
  2. AP2-Szenarien und Testprojekte: alle si (SI-Pruefungsbereiche).
  3. Karteikarten und Quizfragen (aus der Datei): Themen der WiSo-Pruefung
     (recht, arbeitswelt) immer gemeinsam. Sonst nach der Rahmenplan-
     Zuordnung der Python-Linie (fisi_rahmenplan.item_points): trifft der
     Inhalt nur Punkte aus Abschnitt C, ist er si; trifft er einen Punkt aus
     A, B, D, E oder F, ist er gemeinsam. Ohne jeden Treffer: si (wie "ohne
     Eintrag gilt si").
  4. Fachgespraech: allgemeine Fragen (cat null) und die Faecher Wirtschaft
     und Datenbanken gemeinsam, Netzwerk, Sicherheit und Systeme si.
Die 54 im Programm erzeugten Subnetz-Aufgaben stehen nicht in der Datei
(ohne Eintrag gilt si). Abweichungen von den Regeln nur einzeln begruendet
in AUSNAHMEN.

Liest nur die eingefrorene Python-Linie v0.63.2.

Aufruf (Python 3.12):
  python3 -I werkzeuge/inhalte/einteilen.py <python_linie> <ziel.json> [<grenzfaelle.json>]
"""
import atexit
import json
import os
import shutil
import sys
import tempfile

WISO_THEMEN = ("recht", "arbeitswelt")
FACH_GEMEINSAM = (None, "wirtschaft", "datenbanken")
FACH_SI = ("netzwerk", "sicherheit", "systeme")

# (Art, Schluessel) -> (Wert, Begruendung). Leer: bisher keine Ausnahme noetig.
AUSNAHMEN = {}

HINWEIS = [
    "Einteilung der bisherigen Lerninhalte aus inhalte/ (Phase 9, Plan Abschnitt 12.1): "
    "si = nur Systemintegration, gemeinsam = alle Fachrichtungen. Ohne Eintrag gilt si.",
    "Erzeugt von werkzeuge/inhalte/einteilen.py aus der Python-Linie v0.63.2, nie von Hand aendern.",
    "Schluessel: Fragetext (karteikarten, quizfragen), Titel (szenarien_ap1, szenarien_ap2, "
    "projektarbeiten), Kennung (fachgespraech).",
]


def laden(ordner, name):
    with open(os.path.join(ordner, name + ".json"), encoding="utf-8") as handle:
        return json.load(handle)


def main():
    if len(sys.argv) not in (3, 4):
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    grenz_ziel = sys.argv[3] if len(sys.argv) == 4 else None
    tmp = tempfile.mkdtemp(prefix="einteilen_")
    atexit.register(shutil.rmtree, tmp, True)
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_rahmenplan as frp

    inhalte = os.path.join(repo, "inhalte")
    abschnitt = {punkt["id"]: punkt["abschnitt"] for punkt in frp.POINTS}
    punkte = frp.item_points()
    ergebnis = {"_hinweis": HINWEIS}
    grenzfaelle = []
    zaehler = {}

    def setze(art, schluessel, wert, grund):
        teil = ergebnis.setdefault(art, {})
        assert schluessel not in teil, (art, schluessel)
        if (art, schluessel) in AUSNAHMEN:
            wert, grund = AUSNAHMEN[(art, schluessel)][0], "Ausnahme: " + AUSNAHMEN[(art, schluessel)][1]
        teil[schluessel] = wert
        zaehler.setdefault(art, {"si": 0, "gemeinsam": 0})[wert] += 1
        return grund

    for art, quelle in (("karteikarten", core.SRC_CARD), ("quizfragen", core.SRC_QUIZ)):
        for eintrag in laden(inhalte, art):
            frage = eintrag["q"]
            treffer = punkte[(quelle, frage)]
            bereiche = sorted({abschnitt[p][0] for p in treffer})
            if eintrag.get("thema") in WISO_THEMEN:
                wert, grund = "gemeinsam", "WiSo-Thema"
            elif bereiche == ["C"]:
                wert, grund = "si", "nur Abschnitt C"
            elif not bereiche:
                wert, grund = "si", "kein Treffer"
            else:
                wert, grund = "gemeinsam", "Abschnitte " + "".join(bereiche)
            grund = setze(art, frage, wert, grund)
            if len(bereiche) > 1 or not bereiche:
                grenzfaelle.append({"art": art, "schluessel": frage, "wert": wert, "grund": grund,
                                    "punkte": treffer, "thema": eintrag.get("thema")})
    for eintrag in laden(inhalte, "szenarien_ap1"):
        setze("szenarien_ap1", eintrag["title"], "gemeinsam", "AP1")
    for eintrag in laden(inhalte, "szenarien_ap2"):
        setze("szenarien_ap2", eintrag["title"], "si", "AP2")
    for eintrag in laden(inhalte, "projektarbeiten"):
        setze("projektarbeiten", eintrag["title"], "si", "Testprojekt")
    for eintrag in laden(inhalte, "fachgespraech")["fragen"]:
        fach = eintrag.get("cat")
        assert fach in FACH_GEMEINSAM + FACH_SI, fach
        setze("fachgespraech", eintrag["id"], "gemeinsam" if fach in FACH_GEMEINSAM else "si", "Fach")

    text = json.dumps(ergebnis, ensure_ascii=False, indent=1) + "\n"
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    if grenz_ziel:
        with open(grenz_ziel, "w", encoding="utf-8", newline="\n") as handle:
            json.dump(grenzfaelle, handle, ensure_ascii=False, indent=1)
    for art, werte in zaehler.items():
        print(art, "si", werte["si"], "gemeinsam", werte["gemeinsam"])
    print("Grenzfaelle", len(grenzfaelle))
    return 0


if __name__ == "__main__":
    sys.exit(main())
