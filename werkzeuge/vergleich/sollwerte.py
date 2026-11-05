#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Welle Sammelliste 217 (Weisung P12-14): feste Sollwerte "Stand C#".

Die Dateien in tests/Vergleich/ (G1-G52 und Testdaten), alle Dateien unter
inhalte/ (byte-gleich mit Python v0.63.2), die fuenf erzeugten Zeichen-Tabellen
und inhalte_csharp/lernen/einteilung_bisher.json sind feste Sollwerte. Ihre
SHA-256 stehen in tests/Vergleich/SOLLWERTE.sha256 (Format von sha256sum, dazu
Kopfzeilen mit '#'). Ein Sollwert aendert sich nur mit Nicos Freigabe und einem
neuen Abschnitt '## Nr. <n>' in tests/Vergleich/AENDERUNGEN_SOLLWERTE.md; die
Kopfzeile '# Aenderung: <n>' ist gleich der hoechsten Nummer dort.

Aufruf (Python 3.12, nur Standardbibliothek, im Repo-Ordner):
  python3 -I werkzeuge/vergleich/sollwerte.py pruefen
  python3 -I werkzeuge/vergleich/sollwerte.py schreiben <n>   (nur nach Freigabe)
Rueckgabe 0 gruen, 1 rot (jede Abweichung als Zeile 'FEHLER:'), 2 Bedienfehler.
Gleiche Regeln prueft der C#-Test SollwerteTests.
"""
import hashlib
import os
import re
import sys

LISTE = "tests/Vergleich/SOLLWERTE.sha256"
AENDERUNGEN = "tests/Vergleich/AENDERUNGEN_SOLLWERTE.md"
ORDNER = ("tests/Vergleich", "inhalte")
EINZELN = (
    "inhalte_csharp/lernen/einteilung_bisher.json",
    "src/Lernplattform.Core/Projekt/PdfZeichen.Tabelle.cs",
    "src/Lernplattform.Core/Rechner/PythonZeichen.Tabelle.cs",
    "src/Lernplattform.Core/Spiel/PyTitel.Tabelle.cs",
    "src/Lernplattform.Core/Suche/PythonKlein.Tabelle.cs",
    "src/Lernplattform.Core/Texte/RechnerTexte.Python.cs",
)
KOPF = ("# Sollwerte Stand C# A 0.41.1 (main 71de14c), Welle Sammelliste 217 (Weisung P12-14)",
        "# Aendern nur mit Nicos Freigabe: neuer Abschnitt in AENDERUNGEN_SOLLWERTE.md, dann",
        "# python3 -I werkzeuge/vergleich/sollwerte.py schreiben <n> (nie von Hand)")


def geschuetzt(wurzel):
    pfade = set(EINZELN)
    for ordner in ORDNER:
        for basis, _, namen in os.walk(os.path.join(wurzel, ordner)):
            for name in namen:
                pfad = os.path.relpath(os.path.join(basis, name), wurzel).replace(os.sep, "/")
                if pfad not in (LISTE, AENDERUNGEN):
                    pfade.add(pfad)
    return sorted(pfade)


def summe(pfad):
    with open(pfad, "rb") as datei:
        return hashlib.sha256(datei.read()).hexdigest()


def hoechste_nummer(wurzel):
    with open(os.path.join(wurzel, AENDERUNGEN), encoding="utf-8") as datei:
        nummern = [int(n) for n in re.findall(r"^## Nr\. (\d+)\s*$", datei.read(), re.M)]
    return nummern


def pruefen(wurzel):
    fehler = []
    with open(os.path.join(wurzel, LISTE), encoding="utf-8") as datei:
        zeilen = datei.read().splitlines()
    nummer = [z for z in zeilen if z.startswith("# Aenderung: ")]
    eintraege = {}
    for z in zeilen:
        if z.startswith("#"):
            continue
        treffer = re.fullmatch(r"([0-9a-f]{64})  (\S.*)", z)
        if not treffer:
            fehler.append("Zeile ohne Form '<sha256>  <pfad>': " + repr(z))
            continue
        if treffer.group(2) in eintraege:
            fehler.append("doppelt: " + treffer.group(2))
        eintraege[treffer.group(2)] = treffer.group(1)
    nummern = hoechste_nummer(wurzel)
    if nummern != list(range(1, len(nummern) + 1)):
        fehler.append("Nummern in %s nicht fortlaufend ab 1: %s" % (AENDERUNGEN, nummern))
    if len(nummer) != 1:
        fehler.append("genau eine Zeile '# Aenderung: <n>' noetig, gefunden %d" % len(nummer))
    elif nummer[0] != "# Aenderung: %d" % (max(nummern) if nummern else 0):
        fehler.append("%s nennt %s, hoechste Nummer in %s ist %s" % (
            LISTE, nummer[0][2:], AENDERUNGEN, max(nummern) if nummern else 0))
    soll = geschuetzt(wurzel)
    for pfad in soll:
        if pfad not in eintraege:
            fehler.append("ohne Zeile in %s: %s" % (LISTE, pfad))
    for pfad, wert in sorted(eintraege.items()):
        voll = os.path.join(wurzel, pfad)
        if not os.path.isfile(voll):
            fehler.append("fehlt: " + pfad)
        elif pfad not in soll:
            fehler.append("nicht geschuetzt, aber gelistet: " + pfad)
        elif summe(voll) != wert:
            fehler.append("abweichend: %s (Soll %s, Ist %s)" % (pfad, wert, summe(voll)))
    return fehler, len(eintraege)


def schreiben(wurzel, n):
    zeilen = list(KOPF) + ["# Aenderung: %d" % n]
    zeilen += ["%s  %s" % (summe(os.path.join(wurzel, p)), p) for p in geschuetzt(wurzel)]
    with open(os.path.join(wurzel, LISTE), "w", encoding="utf-8", newline="\n") as datei:
        datei.write("\n".join(zeilen) + "\n")


def main():
    wurzel = "."
    if sys.argv[1:] == ["pruefen"]:
        fehler, anzahl = pruefen(wurzel)
        for f in fehler:
            print("FEHLER: " + f)
        if not fehler:
            print("Sollwerte: %d Dateien unveraendert" % anzahl)
        return 1 if fehler else 0
    if len(sys.argv) == 3 and sys.argv[1] == "schreiben" and sys.argv[2].isdigit():
        schreiben(wurzel, int(sys.argv[2]))
        fehler, anzahl = pruefen(wurzel)
        for f in fehler:
            print("FEHLER: " + f)
        print("geschrieben: %d Dateien, Aenderung %s" % (anzahl, sys.argv[2]))
        return 1 if fehler else 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main())
