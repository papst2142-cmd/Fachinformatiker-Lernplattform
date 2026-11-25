#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Testdauer-Waechter (Welle CI schneller und fester, Weisung P12-369, Nico 5a).

Die Unit-Tests schreiben je Projekt einen CTRF-Bericht (JSON, Dauer je Test in Millisekunden):
  dotnet test --project tests/Lernplattform.Tests -c Release --results-directory <ordner> \
      --report-xunit-ctrf --report-xunit-ctrf-filename lernplattform.json

Aufruf (aus dem Repo-Wurzelordner):
  python -I werkzeuge/ci/testdauer.py zeigen <ordner|datei> [anzahl]
      die langsamsten Tests (Vorgabe 20) und die Dauer des ganzen Laufs je Bericht. Nur Anzeige: Rueckgabe immer 0
      (so in der CI, Schritt "Langsamste Tests (testdauer.py)"; aendert kein Urteil).
  python -I werkzeuge/ci/testdauer.py pruefen <ordner|datei> [grenze_minuten]
      lokale Regel vor jedem vollen Lauf (docs/REGELN.md "Ablauf einer Welle"): der ganze Lauf je Bericht hoechstens
      die Haelfte der CI-Grenze (Vorgabe 34 Minuten aus werkzeuge/ci/zeitlimits.json, Schritt "Tests"), kein einzelner
      Test ueber 90 s. Rueckgabe 1 mit Zeilen "FEHLER:", sonst 0.
  python -I werkzeuge/ci/testdauer.py selbsttest
      feste Faelle mit Gegenproben.
Die Grenze der CI und die Tests selbst aendern sich dadurch nie; der Waechter warnt nur frueher (Lehre Lauf 1202: ein neuer
Test mit ueber 6 Minuten liess den ganzen Lauf in die 34-Minuten-Grenze laufen).
"""
import glob
import json
import os
import sys

EINZEL_GRENZE_S = 90
ANTEIL = 0.5
ZEITLIMITS = "werkzeuge/ci/zeitlimits.json"


def ci_grenze_minuten(pfad=ZEITLIMITS):
    """Grenze des Schritts "Tests" (standard) aus zeitlimits.json; 34, wenn die Datei fehlt."""
    try:
        with open(pfad, encoding="utf-8") as f:
            for s in json.load(f)["schritte"]:
                if s["schritt"] == "Tests":
                    return s["limit"]["standard"]
    except (OSError, ValueError, KeyError):
        pass
    return 34


def berichte(ziel):
    """CTRF-Dateien: eine Datei oder alle *.json in einem Ordner (sortiert)."""
    if os.path.isdir(ziel):
        return sorted(glob.glob(os.path.join(ziel, "*.json")))
    return [ziel] if os.path.exists(ziel) else []


def laden(pfad):
    """(Name des Berichts, Dauer des Laufs in s oder None, [(Dauer s, Testname, Status)])."""
    with open(pfad, encoding="utf-8") as f:
        daten = json.load(f)
    ergebnis = daten.get("results", {})
    summe = ergebnis.get("summary", {})
    lauf = None
    if isinstance(summe.get("start"), (int, float)) and isinstance(summe.get("stop"), (int, float)):
        lauf = (summe["stop"] - summe["start"]) / 1000.0
    tests = [((t.get("duration") or 0) / 1000.0, t.get("name", "?"), t.get("status", "?")) for t in ergebnis.get("tests", [])]
    tests.sort(key=lambda x: (-x[0], x[1]))
    return os.path.basename(pfad), lauf, tests


def zeigen(ziel, anzahl=20):
    dateien = berichte(ziel)
    if not dateien:
        print("Testdauer: kein CTRF-Bericht in %s (Tests nicht gelaufen?)" % ziel)
        return 0
    for pfad in dateien:
        try:
            name, lauf, tests = laden(pfad)
        except (OSError, ValueError) as e:
            print("Testdauer: %s nicht lesbar (%s)" % (pfad, e))
            continue
        gesamt = "%.0f s (%.1f min)" % (lauf, lauf / 60) if lauf is not None else "unbekannt"
        print("Testdauer %s: %d Tests, ganzer Lauf %s; die %d langsamsten:" % (name, len(tests), gesamt, min(anzahl, len(tests))))
        for dauer, test, status in tests[:anzahl]:
            print("  %7.1f s  %s%s" % (dauer, test, "" if status == "passed" else "  [" + status + "]"))
    return 0


def funde(dateien, grenze_min):
    aus = []
    for pfad in dateien:
        name, lauf, tests = laden(pfad)
        if lauf is not None and lauf > grenze_min * 60 * ANTEIL:
            aus.append("%s: ganzer Lauf %.1f min ueber der Haelfte der CI-Grenze (%d min -> %.1f min)"
                       % (name, lauf / 60, grenze_min, grenze_min * ANTEIL))
        for dauer, test, _ in tests:
            if dauer > EINZEL_GRENZE_S:
                aus.append("%s: Test %s %.1f s ueber %d s" % (name, test, dauer, EINZEL_GRENZE_S))
    return aus


def pruefen(ziel, grenze_min=None):
    grenze_min = grenze_min or ci_grenze_minuten()
    dateien = berichte(ziel)
    if not dateien:
        print("FEHLER: Testdauer: kein CTRF-Bericht in " + ziel)
        return 1
    liste = funde(dateien, grenze_min)
    for z in liste:
        print("FEHLER: Testdauer " + z)
    if not liste:
        print("Testdauer: %d Bericht(e) gruen (ganzer Lauf hoechstens %.1f min, kein Test ueber %d s)"
              % (len(dateien), grenze_min * ANTEIL, EINZEL_GRENZE_S))
    return 1 if liste else 0


def selbsttest():
    import contextlib
    import io
    import tempfile

    def bericht(ordner, name, lauf_s, dauern_s):
        pfad = os.path.join(ordner, name)
        tests = [{"name": "T%d" % i, "status": "passed", "duration": int(d * 1000)} for i, d in enumerate(dauern_s)]
        with open(pfad, "w", encoding="utf-8") as f:
            json.dump({"results": {"summary": {"tests": len(tests), "start": 1000, "stop": 1000 + int(lauf_s * 1000)},
                                   "tests": tests}}, f)
        return pfad

    rot = 0
    faelle = [  # (Name, Laufdauer s, Testdauern s, Grenze min, erwartete Rueckgabe)
        ("gruen", 600, [5, 89.9, 1], 34, 0),
        ("genau die Haelfte", 1020, [1], 34, 0),
        ("Gegenprobe Lauf zu lang", 1021, [1], 34, 1),
        ("Gegenprobe Einzeltest ueber 90 s", 300, [90.1, 2], 34, 1),
        ("Gegenprobe Lauf 1202 (Test 6 min)", 900, [370, 3], 34, 1),
    ]
    with tempfile.TemporaryDirectory() as ordner:
        for name, lauf, dauern, grenze, soll in faelle:
            unter = os.path.join(ordner, name.replace(" ", "_"))
            os.mkdir(unter)
            bericht(unter, "a.json", lauf, dauern)
            with contextlib.redirect_stdout(io.StringIO()):
                ist = pruefen(unter, grenze)
            if ist != soll:
                print("FEHLER: Selbsttest Testdauer: Fall '%s' Rueckgabe %d statt %d" % (name, ist, soll))
                rot = 1
        # zeigen: sortiert, hoechstens anzahl Zeilen, Rueckgabe 0 auch ohne Bericht
        unter = os.path.join(ordner, "zeigen")
        os.mkdir(unter)
        bericht(unter, "b.json", 100, [1, 30, 2, 400])
        puffer = io.StringIO()
        with contextlib.redirect_stdout(puffer):
            rc = zeigen(unter, 2)
            rc_leer = zeigen(os.path.join(ordner, "fehlt"))
        zeilen = [z for z in puffer.getvalue().splitlines() if z.startswith("  ")]
        if rc != 0 or rc_leer != 0 or len(zeilen) != 2 or "T3" not in zeilen[0] or "T1" not in zeilen[1]:
            print("FEHLER: Selbsttest Testdauer: zeigen falsch: %s" % zeilen)
            rot = 1
        with contextlib.redirect_stdout(io.StringIO()):
            if pruefen(os.path.join(ordner, "fehlt"), 34) != 1:
                print("FEHLER: Selbsttest Testdauer: Gegenprobe ohne Bericht nicht rot")
                rot = 1
    with open(ZEITLIMITS, encoding="utf-8") as f:
        aus_datei = [x["limit"]["standard"] for x in json.load(f)["schritte"] if x["schritt"] == "Tests"]
    if aus_datei != [ci_grenze_minuten()]:
        print("FEHLER: Selbsttest Testdauer: Grenze %s nicht aus %s gelesen (%s)" % (ci_grenze_minuten(), ZEITLIMITS, aus_datei))
        rot = 1
    print("Selbsttest Testdauer: %d Faelle (4 Gegenproben), zeigen, ohne Bericht, %s" % (len(faelle), "rot" if rot else "gruen"))
    return rot


def main():
    a = sys.argv[1:]
    if a[:1] == ["zeigen"] and len(a) in (2, 3):
        return zeigen(a[1], int(a[2]) if len(a) == 3 else 20)
    if a[:1] == ["pruefen"] and len(a) in (2, 3):
        return pruefen(a[1], int(a[2]) if len(a) == 3 else None)
    if a == ["selbsttest"]:
        return selbsttest()
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main())
