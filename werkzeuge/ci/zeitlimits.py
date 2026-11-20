#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Zeitlimits der Pruefschritte (Welle „Pruefverfahren waechst mit“, Weisung P12-324 T2, Nico 2a).

Eine Quelle: werkzeuge/ci/zeitlimits.json nennt je Pruefschritt des Jobs "tests" in ci.yml das Zeitlimit in Minuten
("standard" und "macos-15-intel"), ob der Schritt einen Wachhund hat (STARTTEST_WACHHUND_MINUTEN = Limit minus 1) und den
Ausgangswert "basis" (Stand bei der Einfuehrung, A 0.99.0). Regeln (Nico 2a):
  - ein Limit aendert sich nur per Commit (dieses Skript schreibt ci.yml aus der Datei, "schreiben"), jede Aenderung steht
    in der Meldung als Zeile "Zeitlimit <Schritt> <alt> -> <neu> min, Median <s>";
  - nie kleiner als heute, hoechstens doppelt so gross wie "basis";
  - Vorschlag = 1,5 x Median der letzten 5 gruenen vollen Laeufe (je System das langsamste), aufgerundet auf Minuten.
Die Programm-Grenzen (400 ms, Dreiecke, Aufrufe, 2 000 ms) stehen nicht hier und aendern sich nie dadurch.

Aufruf (aus dem Repo-Wurzelordner):
  python -I werkzeuge/ci/zeitlimits.py pruefen      ci.yml gleich der Datei? (Rueckgabe 0 = gleich)
  python -I werkzeuge/ci/zeitlimits.py schreiben    Zeitlimits und Wachhunde in ci.yml aus der Datei setzen
  python -I werkzeuge/ci/zeitlimits.py selbsttest   feste Faelle mit Gegenproben
"""
import json
import math
import os
import re
import sys

DATEI = "werkzeuge/ci/zeitlimits.json"
CI = ".github/workflows/ci.yml"
INTEL = "macos-15-intel"
FAKTOR = 1.5
HOECHSTENS = 2
ANZAHL_LAEUFE = 5
WARNGRENZE = 25  # Prozent Reserve (Nico 1a: darunter nur Warnung)

ZEITLIMIT = re.compile(r"^(\s*)timeout-minutes: (?:\$\{\{ matrix\.os == 'macos-15-intel' && (\d+) \|\| (\d+) \}\}|(\d+))\s*$")
WACHHUND = re.compile(r"^(\s*)STARTTEST_WACHHUND_MINUTEN: (?:\$\{\{ matrix\.os == 'macos-15-intel' && (\d+) \|\| (\d+) \}\}|(\d+))\s*$")
SCHRITT = re.compile(r"^\s*- (?:name|uses): (.*?)\s*$")
JOB = re.compile(r"^  ([a-z][\w-]*):\s*$")


def laden(pfad=DATEI):
    with open(pfad, encoding="utf-8") as f:
        return json.load(f)


def schluessel(job_name):
    """Systemschluessel eines Jobs fuer die Limits: "macos-15-intel" oder "standard"."""
    return INTEL if "(" + INTEL in job_name else "standard"


def _werte(treffer):
    """(intel, standard) aus einer Zeile mit oder ohne Ausdruck."""
    if treffer.group(4):
        wert = int(treffer.group(4))
        return wert, wert
    return int(treffer.group(2)), int(treffer.group(3))


def aus_ci(text):
    """Schritte des Jobs "tests" mit Zeitlimit: Name -> {"limit": (intel, standard), "wachhund": (intel, standard)|None}."""
    schritte, job, name = {}, None, None
    for zeile in text.splitlines():
        j = JOB.match(zeile)
        if j:
            job, name = j.group(1), None
            continue
        if job != "tests":
            continue
        s = SCHRITT.match(zeile)
        if s:
            name = s.group(1)
            continue
        z = ZEITLIMIT.match(zeile)
        if z and name:
            schritte.setdefault(name, {"limit": None, "wachhund": None})["limit"] = _werte(z)
        w = WACHHUND.match(zeile)
        if w and name:
            schritte.setdefault(name, {"limit": None, "wachhund": None})["wachhund"] = _werte(w)
    return schritte


def regeln(daten):
    """Feste Regeln der Datei: Limit nie unter basis, hoechstens 2 x basis, beide Systeme da. Liste der Verstoesse."""
    falsch = []
    for e in daten["schritte"]:
        for k in ("standard", INTEL):
            alt, neu = e["basis"].get(k), e["limit"].get(k)
            if not isinstance(alt, int) or not isinstance(neu, int):
                falsch.append("%s (%s): Limit oder basis fehlt" % (e["schritt"], k))
            elif not erlaubt(alt, alt, neu):
                falsch.append("%s (%s): Limit %d ausserhalb basis %d bis %d" % (e["schritt"], k, neu, alt, HOECHSTENS * alt))
    return falsch


def erlaubt(basis, alt, neu):
    """Nico 2a: nie kleiner als heute (alt), hoechstens doppelt so gross wie der Ausgangswert (basis)."""
    return alt <= neu <= HOECHSTENS * basis


def pruefen(text, daten, still=False):
    """ci.yml (Job tests) gleich der Datei: jeder Schritt mit Zeitlimit steht in der Datei und umgekehrt, gleiche Werte,
    Wachhund genau dort, wo die Datei ihn nennt, und dann Limit minus 1. Rueckgabe 0 = gleich."""
    def melde(text_):
        if not still:
            print(text_)
    rot = 0
    ci = aus_ci(text)
    soll = {e["schritt"]: e for e in daten["schritte"]}
    for name in sorted(set(ci) - set(soll)):
        melde("FEHLER: Zeitlimits: Schritt %r hat in ci.yml ein Zeitlimit, steht aber nicht in %s" % (name, DATEI))
        rot = 1
    for name in sorted(set(soll) - set(ci)):
        melde("FEHLER: Zeitlimits: Schritt %r aus %s fehlt in ci.yml (oder ohne Zeitlimit)" % (name, DATEI))
        rot = 1
    for name in sorted(set(soll) & set(ci)):
        e, c = soll[name], ci[name]
        limit = (e["limit"][INTEL], e["limit"]["standard"])
        if c["limit"] != limit:
            melde("FEHLER: Zeitlimits: %r in ci.yml %s, in der Datei %s (intel, standard)" % (name, c["limit"], limit))
            rot = 1
        wachhund = (limit[0] - 1, limit[1] - 1) if e["wachhund"] else None
        if c["wachhund"] != wachhund:
            melde("FEHLER: Zeitlimits: %r Wachhund in ci.yml %s, erwartet %s" % (name, c["wachhund"], wachhund))
            rot = 1
    for f in regeln(daten):
        melde("FEHLER: Zeitlimits: " + f)
        rot = 1
    if not rot:
        melde("Zeitlimits: %d Pruefschritte in ci.yml gleich %s, jedes Limit zwischen basis und 2 x basis" % (len(soll), DATEI))
    return rot


def _ausdruck(intel, standard):
    return str(standard) if intel == standard else "${{ matrix.os == 'macos-15-intel' && %d || %d }}" % (intel, standard)


def schreiben_text(text, daten):
    """ci.yml mit den Limits (und Wachhunden = Limit minus 1) der Datei; nur Schritte, die schon ein Zeitlimit haben."""
    soll = {e["schritt"]: e for e in daten["schritte"]}
    raus, job, name = [], None, None
    for zeile in text.splitlines(keepends=True):
        j = JOB.match(zeile.rstrip("\n"))
        if j:
            job, name = j.group(1), None
        s = SCHRITT.match(zeile.rstrip("\n"))
        if s:
            name = s.group(1)
        e = soll.get(name) if job == "tests" else None
        if e:
            z, w = ZEITLIMIT.match(zeile.rstrip("\n")), WACHHUND.match(zeile.rstrip("\n"))
            if z:
                zeile = "%stimeout-minutes: %s\n" % (z.group(1), _ausdruck(e["limit"][INTEL], e["limit"]["standard"]))
            elif w and e["wachhund"]:
                zeile = "%sSTARTTEST_WACHHUND_MINUTEN: %s\n" % (w.group(1), _ausdruck(e["limit"][INTEL] - 1, e["limit"]["standard"] - 1))
        raus.append(zeile)
    return "".join(raus)


def grenze_sekunden(eintrag, job_name, runner_windows):
    """Grenze eines Schritts fuer die Reserve: Wachhund (Limit minus 1 min), unter Windows ohne Wachhund das Limit."""
    minuten = eintrag["limit"][schluessel(job_name)]
    if eintrag["wachhund"] and not runner_windows:
        minuten -= 1
    return minuten * 60


def median(werte):
    w = sorted(werte)
    n = len(w)
    return None if n == 0 else (w[n // 2] if n % 2 else (w[n // 2 - 1] + w[n // 2]) / 2)


def vorschlag(daten, dauern):
    """Nico 2a. dauern: {(schritt, schluessel): {system: [sekunden der letzten gruenen vollen Laeufe]}}.
    Je Schritt und Schluessel: das langsamste System (Median seiner letzten 5 Werte) x 1,5, aufgerundet auf Minuten,
    nie kleiner als heute, hoechstens 2 x basis. Liste (schritt, schluessel, alt, neu, median) nur dort, wo neu > alt."""
    raus = []
    for e in daten["schritte"]:
        for k in ("standard", INTEL):
            systeme = dauern.get((e["schritt"], k)) or {}
            mediane = [median(w[-ANZAHL_LAEUFE:]) for w in systeme.values() if w]
            if not mediane:
                continue
            m = max(mediane)
            alt, basis = e["limit"][k], e["basis"][k]
            neu = min(max(math.ceil(FAKTOR * m / 60), alt), HOECHSTENS * basis)
            if neu > alt:
                raus.append((e["schritt"], k, alt, neu, m))
    return raus


def vorschlag_zeile(schritt, k, alt, neu, m):
    return "Zeitlimit %s (%s) %d -> %d min, Median %.0f s" % (schritt, k, alt, neu, m)


def selbsttest():
    """Feste Faelle mit Gegenproben (Weisung P12-324 T2): echte ci.yml gleich der Datei; geaenderte ci.yml, fehlender
    Schritt, falscher Wachhund, Limit ueber 2 x basis und unter basis = rot; Vorschlag nie kleiner, hoechstens 2 x."""
    rot = 0
    daten = laden()
    with open(CI, encoding="utf-8") as f:
        text = f.read()
    if pruefen(text, daten):
        rot = 1
    if schreiben_text(text, daten) != text:
        print("FEHLER: Selbsttest Zeitlimits: schreiben aendert die heutige ci.yml (Datei und ci.yml nicht gleich)")
        rot = 1
    # Gegenproben: jede muss rot sein
    def kopie():
        return json.loads(json.dumps(daten))
    erster = daten["schritte"][0]["schritt"]
    zu_gross, zu_klein, ohne_wh, mehr = kopie(), kopie(), kopie(), kopie()
    zu_gross["schritte"][0]["limit"]["standard"] = 2 * zu_gross["schritte"][0]["basis"]["standard"] + 1
    zu_klein["schritte"][0]["limit"]["standard"] = zu_klein["schritte"][0]["basis"]["standard"] - 1
    ohne_wh["schritte"][0]["wachhund"] = not ohne_wh["schritte"][0]["wachhund"]
    mehr["schritte"].append({"schritt": "Gibt es nicht", "limit": {"standard": 5, INTEL: 5}, "basis": {"standard": 5, INTEL: 5}, "wachhund": False})
    geaendert = schreiben_text(text, zu_gross)  # ci.yml mit anderem Limit gegen die echte Datei
    faelle = [("Limit ueber 2 x basis", text, zu_gross), ("Limit unter basis", text, zu_klein),
              ("Wachhund falsch", text, ohne_wh), ("Schritt fehlt in ci.yml", text, mehr),
              ("ci.yml anders als die Datei", geaendert, daten)]
    weg = kopie()
    weg["schritte"] = [e for e in weg["schritte"] if e["schritt"] != erster]
    faelle.append(("Schritt mit Zeitlimit fehlt in der Datei", text, weg))
    for name, t, d in faelle:
        if pruefen(t, d, still=True) != 1:
            print("FEHLER: Selbsttest Zeitlimits: Gegenprobe '%s' nicht rot" % name)
            rot = 1
    # Vorschlagsregel: 1,5 x Median, aufgerundet, nie kleiner, hoechstens 2 x basis
    probe = {"schritte": [{"schritt": "S", "limit": {"standard": 10, INTEL: 20}, "basis": {"standard": 10, INTEL: 20}, "wachhund": True}]}
    erwartet = [
        ({("S", "standard"): {"ubuntu": [400] * 5}}, []),                                   # 600 s = 10 min, nicht mehr
        ({("S", "standard"): {"ubuntu": [420] * 5}}, [("S", "standard", 10, 11, 420)]),     # 630 s -> 11
        ({("S", "standard"): {"ubuntu": [100] * 5}}, []),                                   # nie kleiner
        ({("S", "standard"): {"ubuntu": [2000] * 5}}, [("S", "standard", 10, 20, 2000)]),   # hoechstens 2 x basis
        ({("S", "standard"): {"ubuntu": [300] * 5, "windows": [500, 520, 510, 530, 505]}}, [("S", "standard", 10, 13, 510)]),
        ({("S", INTEL): {"intel": [2000, 600, 610, 620, 630, 640]}}, []),                    # nur die letzten 5 zaehlen: Median 630 -> 16 min < 20
    ]
    for nr, (dauern, soll) in enumerate(erwartet, 1):
        ist = vorschlag(probe, dauern)
        if ist != soll:
            print("FEHLER: Selbsttest Zeitlimits: Vorschlag Fall %d %s statt %s" % (nr, ist, soll))
            rot = 1
    for basis, alt, neu, soll in ((10, 10, 9, False), (10, 10, 21, False), (10, 10, 20, True), (10, 12, 11, False), (10, 10, 10, True)):
        if erlaubt(basis, alt, neu) != soll:
            print("FEHLER: Selbsttest Zeitlimits: erlaubt(%d, %d, %d) nicht %s" % (basis, alt, neu, soll))
            rot = 1
    print("Selbsttest Zeitlimits: %d Gegenproben, %d Vorschlagsfaelle, %s" % (len(faelle), len(erwartet), "rot" if rot else "gruen"))
    return rot


def main():
    sys.stdout.reconfigure(newline="\n")
    if len(sys.argv) == 2 and sys.argv[1] == "pruefen":
        with open(CI, encoding="utf-8") as f:
            return pruefen(f.read(), laden())
    if len(sys.argv) == 2 and sys.argv[1] == "schreiben":
        with open(CI, encoding="utf-8") as f:
            text = f.read()
        neu = schreiben_text(text, laden())
        with open(CI, "w", encoding="utf-8", newline="\n") as f:
            f.write(neu)
        print("ci.yml geschrieben" if neu != text else "ci.yml war schon gleich")
        return 0
    if len(sys.argv) == 2 and sys.argv[1] == "selbsttest":
        return selbsttest()
    print(__doc__)
    return 2


if __name__ == "__main__":
    os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    sys.exit(main())
