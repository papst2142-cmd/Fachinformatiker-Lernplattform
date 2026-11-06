#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Urteil des Start-Tests (Start ohne Bedienung) mit Neustart-Regel, Weisung P4-13 (Nico 22.10.2026).

Endet der 1. Lauf mit Rueckgabe 3 (Messwert-Rot oder Layoutfund), folgt ein 2. Lauf in einem
neuen Prozess mit frischem Datenordner. Rot nur, wenn derselbe Messpunkt in BEIDEN Laeufen als
Fehler steht. Messpunkt = Text vor dem ersten ": " einer FEHLER-Zeile (nach "FEHLER: ");
ein Layoutfund zaehlt mit seinem ganzen Text, ein Kontrastfund ("Kontrast ...: ... UNTER ...")
als "Kontrast <Schluessel>"; bei beiden ohne Ziffern (Weisung P4-14: 1 px Abweichung zwischen
den Laeufen ist derselbe Punkt). Ein Absturz (Rueckgabe ungleich 0 und 3, Ausnahme
im Start-Test, keine Messdatei) startet nicht neu und ist sofort rot. Die Grenzen bleiben im
Programm (Messwiederholung, Farbreglerregel) und werden hier nicht beruehrt.

Aufruf:
  python -I werkzeuge/starttest/urteil.py lauf1 <rueckgabe> <messdatei_lauf1>
      Rueckgabe 0 = gruen oder Neustart noetig, sonst rot. Die Zeile "rc=<n>" nach $GITHUB_OUTPUT
      schreibt lauf.sh (eine Quelle, ohne \r); dieses Skript schreibt dorthin "zweiter_lauf=ja|nein"
      (Weisung P11-38: nein nur am Intel-Mac, wenn der 1. Lauf nur Messzeiten der Ausnahme P11-19 hat).
  python -I werkzeuge/starttest/urteil.py lauf2 <rueckgabe_lauf2> <messdatei_lauf1> <messdatei_lauf2>
      Rueckgabe 0 = gruen, 3 = derselbe Punkt in beiden Laeufen rot, 1 = Absturz im 2. Lauf.
  python -I werkzeuge/starttest/urteil.py selbsttest
      feste Faelle der Neustart-Regel und der Ausnahme Intel-Mac (Rueckgabe 0 = alle richtig).

Feste, benannte Ausnahme Intel-Mac (Weisung P11-19, Nico 03.11.2026, Antwort 1a): nur wenn
LERNPLATTFORM_ZEITEN_NUR_INFO genau "macos-15-intel" ist (setzt ci.yml nur fuer tests (macos-15-intel)),
zaehlen Messzeiten ueber ihrer Grenze nicht als Rot. Sie stehen mit Wert, Grenze und allen
Einzelmessungen als INFO-Zeile in der Ausgabe. Layout-, Kontrast- und alle anderen Funde und jeder
Absturz bleiben rot. Messzeit = FEHLER-Zeile mit "<n> ms ueber [Grenze] <n> ms" (Messwiederholung,
Farbreglerregel) oder "(Grenze Median <n> ms" (Spiel Zeit je Bild).

2. Lauf am Intel-Mac (Weisung P11-38, Nico 04.11.2026): hat der 1. Lauf dort NUR solche Messzeiten (jede andere
Zeile, z. B. Layout, Kontrast, Grenzen Dreiecke/Aufrufe, Rueckgabe 3 ohne erkannte Fehlerzeile, ist ein Fund),
folgt kein 2. Lauf: diese Messzeiten koennen den Lauf nicht rot machen, der 2. Lauf aenderte am Urteil nichts.
Jeder andere Fund und jedes andere System: 2. Lauf wie bisher.
"""
import contextlib
import io
import os
import tempfile
import re
import sys

AUSNAHME = "URTEIL: rot (Ausnahme"
LAYOUT_KOPF = "Layoutpruefung ("
# Beschreibende Zeilen unter der Layout-Kopfzeile (AutoMessung.cs), keine Funde; "W4-Ansichten"
# ist die Zeile zur 7,5-Minuten-Grenze der W4-Ansichten (Weisung P4-16, sonst waere jeder 2. Lauf rot)
LAYOUT_TEXT = ("  darin ", "  dazu ", "  und ", "  Groessen: ", "  W4-Ansichten ")
FEHLER = re.compile(r"FEHLER(?: \(([^)]*)\))?: ?(.*)")
OHNE_ZEILE = "Rueckgabe 3 ohne erkannte Fehlerzeile"
KONTRAST = re.compile(r"\s*(Kontrast [^:]*): .* UNTER ")
ZIFFERN = re.compile(r"[0-9]")
AUSNAHME_INTEL_MAC = "macos-15-intel"
# Sammelliste 199: Rueckgabe des Wachhunds in lauf.sh (Programm nach STARTTEST_WACHHUND_MINUTEN noch nicht fertig)
HAENGER = 4
WACHHUND = re.compile(r"STARTTEST_WACHHUND_MINUTEN: (?:\$\{\{ matrix\.os == 'macos-15-intel' && (\d+) \|\| (\d+) \}\}|(\d+))")
ZEITLIMIT = re.compile(r"timeout-minutes: (?:\$\{\{ matrix\.os == 'macos-15-intel' && (\d+) \|\| (\d+) \}\}|(\d+))")
MESSZEIT = re.compile(r"[0-9][0-9.,]* ms ueber (?:Grenze )?[0-9][0-9.,]* ms|\(Grenze Median [0-9][0-9.,]* ms")


def zeiten_nur_info():
    """Ausnahme Intel-Mac aktiv? Nur bei genau diesem Wert (Weisung P11-19)."""
    return os.environ.get("LERNPLATTFORM_ZEITEN_NUR_INFO", "") == AUSNAHME_INTEL_MAC


def messzeit(zeile):
    return "FEHLER" in zeile and MESSZEIT.search(zeile) is not None


def lesen(datei):
    try:
        with open(datei, encoding="utf-8") as f:
            return f.read().splitlines()
    except OSError:
        return None


def punkte(zeilen):
    """Messpunkt -> erste Zeile dazu (mit Wert, Grenze und Einzelmessungen)."""
    gefunden = {}
    im_layout = False
    for zeile in zeilen:
        if zeile.startswith(LAYOUT_KOPF):
            im_layout = True
            continue
        if im_layout:
            if zeile.startswith("  ") and not zeile.startswith("   "):
                if not zeile.startswith(LAYOUT_TEXT):
                    gefunden.setdefault(ZIFFERN.sub("", "Layout: " + zeile.strip()), zeile.strip())
                continue
            im_layout = False
        kontrast = KONTRAST.match(zeile)
        if kontrast:
            gefunden.setdefault(ZIFFERN.sub("", kontrast.group(1)), zeile.strip())
            continue
        if "FEHLER" not in zeile:
            continue
        text = zeile.strip()
        treffer = FEHLER.match(text)
        if treffer:
            vorne = treffer.group(1) + ": " if treffer.group(1) else ""
            punkt = vorne + treffer.group(2).split(": ", 1)[0]
        else:
            punkt = text.split(": ", 1)[0]
        gefunden.setdefault(punkt, text)
    return gefunden


def absturz(rc, zeilen, lauf):
    if rc == HAENGER:
        return "Haenger im %s. Lauf: der Wachhund von lauf.sh hat den Lauf vor dem Schrittzeitlimit beendet (Rueckgabe 4, Angaben in der Ausgabe darueber), kein Neustart" % lauf
    if rc not in (0, 3):
        return "Rueckgabe %d im %s. Lauf (Absturz oder Abbruch), kein Neustart" % (rc, lauf)
    if zeilen is None:
        return "Messdatei des %s. Laufs fehlt (Absturz), kein Neustart" % lauf
    for zeile in zeilen:
        if zeile.startswith(AUSNAHME):
            return "Ausnahme im %s. Lauf (%s), kein Neustart" % (lauf, zeile)
    return None


def zweiter_lauf_noetig(zeilen):
    """Weisung P11-38: nach Rueckgabe 3 ein 2. Lauf, ausser am Intel-Mac bei nur Messzeiten der Ausnahme P11-19."""
    gefunden = punkte(zeilen)
    if not gefunden or not zeiten_nur_info():
        return True
    return not all(messzeit(zeile) for zeile in gefunden.values())


def ausgabe_setzen(name, wert):
    """Zeile fuer GitHub Actions (steps.<id>.outputs.<name>); ohne GITHUB_OUTPUT nichts."""
    ziel = os.environ.get("GITHUB_OUTPUT", "")
    if ziel:
        with open(ziel, "a", encoding="utf-8", newline="\n") as f:
            f.write("%s=%s\n" % (name, wert))


def lauf1(rc, datei):
    zeilen = lesen(datei)
    grund = absturz(rc, zeilen, 1)
    if grund:
        print("Start-Test ROT: " + grund)
        ausgabe_setzen("zweiter_lauf", "nein")
        return 1
    if rc == 0:
        print("Start-Test gruen im 1. Lauf")
        ausgabe_setzen("zweiter_lauf", "nein")
        return 0
    if not zweiter_lauf_noetig(zeilen):
        print("Start-Test gruen im 1. Lauf: nur Messzeiten der Ausnahme Intel-Mac, kein 2. Lauf (Weisung P11-38):")
        for zeile in punkte(zeilen).values():
            print("  INFO Intel-Mac (Ausnahme P11-19, zaehlt nicht): " + zeile)
        ausgabe_setzen("zweiter_lauf", "nein")
        return 0
    print("1. Lauf rot (Rueckgabe 3), es folgt ein 2. Lauf in neuem Prozess mit frischem Datenordner:")
    for punkt, zeile in punkte(zeilen).items():
        if zeiten_nur_info() and messzeit(zeile):
            print("  INFO Intel-Mac (Ausnahme P11-19, zaehlt nicht): " + zeile)
        else:
            print("  1. Lauf rot: " + zeile)
    ausgabe_setzen("zweiter_lauf", "ja")
    return 0


def lauf2(rc, datei1, datei2):
    zeilen2 = lesen(datei2)
    grund = absturz(rc, zeilen2, 2)
    if grund:
        print("Start-Test ROT: " + grund)
        return 1
    erste = punkte(lesen(datei1) or []) or {OHNE_ZEILE: OHNE_ZEILE}
    zweite = punkte(zeilen2)
    if rc == 3 and not zweite:
        zweite = {OHNE_ZEILE: OHNE_ZEILE}
    beide = [p for p in erste if p in zweite]
    if zeiten_nur_info():
        # Ausnahme Intel-Mac: Messzeiten ueber der Grenze nur als Information, alles andere bleibt rot
        for punkt in [p for p in beide if messzeit(erste[p]) and messzeit(zweite[p])]:
            print("INFO Intel-Mac (Ausnahme P11-19, zaehlt nicht): in beiden Laeufen ueber der Grenze: %s" % punkt)
            print("    1. Lauf: " + erste[punkt])
            print("    2. Lauf: " + zweite[punkt])
            beide.remove(punkt)
    for punkt in erste:
        if punkt not in zweite:
            print("im 1. Lauf rot, im 2. Lauf gruen: %s, Einzelwerte: %s" % (punkt, erste[punkt]))
    for punkt in zweite:
        if punkt not in erste:
            print("im 2. Lauf rot, im 1. Lauf gruen: %s, Einzelwerte: %s" % (punkt, zweite[punkt]))
    if beide:
        print("Start-Test ROT: derselbe Messpunkt in beiden Laeufen rot:")
        for punkt in beide:
            print("  %s" % punkt)
            print("    1. Lauf: " + erste[punkt])
            print("    2. Lauf: " + zweite[punkt])
        return 3
    print("Start-Test gruen: kein Messpunkt in beiden Laeufen rot")
    return 0


def wachhund_unter_zeitlimit(pfad=".github/workflows/ci.yml"):
    """Sammelliste 199: jeder Schritt mit STARTTEST_WACHHUND_MINUTEN hat ein Zeitlimit, und der Wachhund liegt fuer
    jedes System darunter (sonst bricht GitHub wieder ohne Angaben ab). Ohne ci.yml (Aufruf ausserhalb des Repos)
    keine Pruefung."""
    if not os.path.exists(pfad):
        return 0
    with open(pfad, encoding="utf-8") as f:
        zeilen = f.read().splitlines()
    rot = 0
    limit = None
    gesehen = 0
    for zeile in zeilen:
        if zeile.lstrip().startswith("- name:") or zeile.lstrip().startswith("- uses:"):
            limit = None
        z = ZEITLIMIT.search(zeile)
        if z:
            limit = z.groups()
        w = WACHHUND.search(zeile)
        if w:
            gesehen += 1
            wert = w.groups()
            werte = (int(wert[2]), int(wert[2])) if wert[2] else (int(wert[0]), int(wert[1]))
            if limit is None:
                print("FEHLER: Selbsttest Wachhund: Schritt ohne timeout-minutes: " + zeile.strip())
                rot = 1
                continue
            grenzen = (int(limit[2]), int(limit[2])) if limit[2] else (int(limit[0]), int(limit[1]))
            if not all(0 < w_ < g for w_, g in zip(werte, grenzen)):
                print("FEHLER: Selbsttest Wachhund %s nicht unter Zeitlimit %s" % (werte, grenzen))
                rot = 1
    if gesehen == 0:
        print("FEHLER: Selbsttest Wachhund: kein STARTTEST_WACHHUND_MINUTEN in " + pfad)
        rot = 1
    else:
        print("Selbsttest Wachhund: %d Schritte, jeder Wachhund unter seinem Zeitlimit" % gesehen)
    return rot


def matrix_listen(ci):
    """Weisung P12-20: die Listen der tests-Matrix in ci.yml in der Reihenfolge voll, werkzeug, schnell."""
    import json
    with open(ci, encoding="utf-8") as f:
        text = f.read()
    zeile = re.search(r"include: \$\{\{ fromJSON\((.*)\) \}\}", text)
    listen = re.findall(r"'(\[\{.*?\}\])'", zeile.group(1)) if zeile else []
    if len(listen) != 3:
        return None
    return [sorted("tests (%s%s)" % (e["os"], ", " + e["teil"] if e["teil"] else "") for e in json.loads(l)) for l in listen]


def soll_liste(release, name="soll"):
    """Liste <name>='...' (eine Zeile je Job) aus einem Release-Workflow, None ohne Liste."""
    with open(release, encoding="utf-8") as f:
        block = re.search(r"(?<![A-Za-z_])" + name + r"='(.*?)'", f.read(), re.S)
    return None if not block else [z.strip() for z in block.group(1).splitlines() if z.strip()]


def tests_jobs_gleich_release(ci=".github/workflows/ci.yml", release=".github/workflows/release.yml"):
    """Weisung P11-38: die Namen der tests-Jobs des vollen Laufs (Matrix in ci.yml) stehen genau so in der Liste,
    die release.yml vor dem Release als gruen verlangt (sonst wuerde ein Teil still fehlen oder das Release nie
    starten). Ohne beide Dateien keine Pruefung."""
    if not (os.path.exists(ci) and os.path.exists(release)):
        return 0
    listen = matrix_listen(ci)
    if not listen:
        print("FEHLER: Selbsttest Jobs: Matrix der tests-Jobs (voll, werkzeug, schnell) in " + ci + " nicht gefunden")
        return 1
    voll = listen[0]
    soll = sorted(z for z in (soll_liste(release) or []) if z.startswith("tests ("))
    if voll != soll:
        print("FEHLER: Selbsttest Jobs: ci.yml %s, release.yml %s" % (voll, soll))
        return 1
    print("Selbsttest Jobs: %d tests-Jobs im vollen Lauf, gleich der Liste in %s" % (len(voll), os.path.basename(release)))
    return 0


def jobs_werkzeug(ci):
    """Weisung P12-20: alle Jobs der Stufe werkzeug laut ci.yml: die tests-Jobs der Werkzeug-Matrix und jeder Job mit
    festem Namen, dessen if die Stufe werkzeug nennt oder keine Stufe einschraenkt (sollwerte)."""
    listen = matrix_listen(ci)
    if not listen:
        return None
    with open(ci, encoding="utf-8") as f:
        text = f.read()
    jobs = list(listen[1])
    teile = re.split(r"^  [a-z][a-z0-9_-]*:\n", text.split("\njobs:\n", 1)[1], flags=re.M)[1:]
    for teil in teile:
        name = re.search(r"^    name: (.+)$", teil, re.M)
        bedingung = re.search(r"^    if: (.+)$", teil, re.M)
        if not name or "${{" in name.group(1):
            continue
        b = bedingung.group(1) if bedingung else ""
        if "inputs.stufe == 'werkzeug'" in b or "inputs.stufe" not in b:
            jobs.append(name.group(1).strip())
    return sorted(jobs)


def werkzeug_jobs_gleich_release(ci=".github/workflows/ci.yml", release=".github/workflows/stadtbau_release.yml"):
    """Weisung P12-20: der Werkzeug-Release nimmt auch den gruenen Lauf werkzeug; seine Liste soll_werkzeug in
    stadtbau_release.yml ist genau die Jobs der Stufe werkzeug in ci.yml (keine Mac-Jobs, keine Lernplattform-Pakete)."""
    if not (os.path.exists(ci) and os.path.exists(release)):
        return 0
    ist = jobs_werkzeug(ci)
    soll = soll_liste(release, "soll_werkzeug")
    if ist is None or soll is None:
        print("FEHLER: Selbsttest Werkzeug-Jobs: Matrix in %s oder Liste soll_werkzeug in %s nicht gefunden" % (ci, release))
        return 1
    soll = sorted(soll)
    if ist != soll:
        print("FEHLER: Selbsttest Werkzeug-Jobs: ci.yml %s, %s %s" % (ist, os.path.basename(release), soll))
        return 1
    mac = [j for j in ist if "macos" in j or re.fullmatch(r"pakete \((win-x64|linux-x64|osx-arm64|osx-x64)\)", j)]
    if mac:
        print("FEHLER: Selbsttest Werkzeug-Jobs: Mac-Jobs oder Lernplattform-Pakete in der Stufe werkzeug: %s" % mac)
        return 1
    print("Selbsttest Werkzeug-Jobs: %d Jobs der Stufe werkzeug, gleich der Liste in %s" % (len(ist), os.path.basename(release)))
    return 0


def selbsttest_werkzeug_gegenprobe():
    """Gegenprobe: eine stadtbau_release.yml, deren Liste soll_werkzeug einen Mac-Job nennt, ist rot."""
    import tempfile
    ci, release = ".github/workflows/ci.yml", ".github/workflows/stadtbau_release.yml"
    if not (os.path.exists(ci) and os.path.exists(release)):
        return 0
    with open(release, encoding="utf-8") as f:
        text = f.read()
    falsch = text.replace("soll_werkzeug='", "soll_werkzeug='tests (macos-15, Spiel und Metal)\n", 1)
    if falsch == text:
        print("FEHLER: Selbsttest Werkzeug-Jobs: Gegenprobe ohne Liste soll_werkzeug in " + release)
        return 1
    with tempfile.TemporaryDirectory() as ordner:
        pfad = os.path.join(ordner, "stadtbau_release.yml")
        with open(pfad, "w", encoding="utf-8") as f:
            f.write(falsch)
        with contextlib.redirect_stdout(io.StringIO()):
            rc = werkzeug_jobs_gleich_release(ci, pfad)
    if rc != 1:
        print("FEHLER: Selbsttest Werkzeug-Jobs: Gegenprobe mit Mac-Job nicht rot")
        return 1
    print("Selbsttest Werkzeug-Jobs: Gegenprobe mit Mac-Job rot")
    return 0


def still(aufruf, *argumente, ausgabe=None):
    """Fall des Selbsttests ohne seine Ausgabe (Sammelliste 212: die festen Zeilen wie "Netzwerkdesig|n" standen im
    Protokoll und sahen wie echte Funde aus); Rueckgabe und Text, der Text nur bei einem falschen Ergebnis gezeigt.
    GITHUB_OUTPUT des Schritts bleibt unberuehrt; ausgabe = eigene Datei fuer die Zeile zweiter_lauf."""
    puffer = io.StringIO()
    alt = os.environ.pop("GITHUB_OUTPUT", None)
    if ausgabe:
        os.environ["GITHUB_OUTPUT"] = ausgabe
    try:
        with contextlib.redirect_stdout(puffer):
            ist = aufruf(*argumente)
    finally:
        os.environ.pop("GITHUB_OUTPUT", None)
        if alt is not None:
            os.environ["GITHUB_OUTPUT"] = alt
    return ist, puffer.getvalue()


def feste_jobs_gleich_release(ci=".github/workflows/ci.yml", release=".github/workflows/release.yml"):
    """Welle Sammelliste 217 (Weisung P12-14): die Jobs mit festem Namen (ohne Matrix, z. B. "sollwerte (Pruefsummen)"
    und android) stehen genau so in der Liste von release.yml; ein umbenannter oder entfernter Job (vorher
    "vergleichswerte") wuerde sonst das Release nie starten lassen. Ohne beide Dateien keine Pruefung."""
    if not (os.path.exists(ci) and os.path.exists(release)):
        return 0
    with open(ci, encoding="utf-8") as f:
        fest = sorted(set(m.strip() for m in re.findall(r"^    name: (.+)$", f.read(), re.M) if "${{" not in m))
    liste = soll_liste(release)
    if liste is None:
        print("FEHLER: Selbsttest feste Jobs: Liste soll='...' in " + release + " nicht gefunden")
        return 1
    # Matrix-Jobs (tests (...), pakete (<rid>)) prueft tests_jobs_gleich_release bzw. die Matrix; feste Namen wie
    # "pakete (stadtbau win-x64)" (Phase 13 Welle 2, mit Leerzeichen in der Klammer) zaehlen hier mit
    soll = sorted(z for z in liste
                  if not z.startswith("tests (") and not re.fullmatch(r"pakete \([a-z0-9-]+\)", z))
    if fest != soll:
        print("FEHLER: Selbsttest feste Jobs: ci.yml %s, release.yml %s" % (fest, soll))
        return 1
    print("Selbsttest feste Jobs: %s gleich der Liste in %s" % (", ".join(fest), os.path.basename(release)))
    return 0


def selbsttest_feste_jobs_gegenprobe():
    """Gegenprobe: eine release.yml mit dem alten Job "vergleichswerte (Python 3.12)" ist rot."""
    import tempfile
    ci, release = ".github/workflows/ci.yml", ".github/workflows/release.yml"
    if not (os.path.exists(ci) and os.path.exists(release)):
        return 0
    with open(release, encoding="utf-8") as f:
        text = f.read()
    falsch = text.replace("sollwerte (Pruefsummen)'", "vergleichswerte (Python 3.12)'")
    if falsch == text:
        print("FEHLER: Selbsttest feste Jobs: Gegenprobe ohne Job sollwerte in " + release)
        return 1
    with tempfile.TemporaryDirectory() as ordner:
        pfad = os.path.join(ordner, "release.yml")
        with open(pfad, "w", encoding="utf-8") as f:
            f.write(falsch)
        import contextlib
        import io
        with contextlib.redirect_stdout(io.StringIO()):
            rc = feste_jobs_gleich_release(ci, pfad)
    if rc != 1:
        print("FEHLER: Selbsttest feste Jobs: Gegenprobe mit altem Job nicht rot")
        return 1
    print("Selbsttest feste Jobs: Gegenprobe mit altem Job rot")
    return 0


def selbsttest():
    """Feste Faelle (Weisung P11-19): Intel-Mac Messzeit-Rot = gruen mit INFO, Layout-Rot = rot; dasselbe
    ohne Ausnahme (macos-15 und alle anderen) bleibt rot."""
    zeit = "  FEHLER: Inhalte laden (kalt, einmalig): 1073 ms ueber Grenze 400 ms; Einzelmessungen 1073 ms (einmaliger Messpunkt, nicht wiederholt)"
    hoechst = "  FEHLER: Seitenwechsel (gebaute Seite) (Hoechstwert): 1341 ms ueber Grenze 1000 ms; Einzelmessungen 1341 ms (einmaliger Messpunkt, nicht wiederholt)"
    farbe = "  FEHLER: Farbregler Schritt 7: 120.5 ms ueber 100 ms; Einzelmessungen 120.5, 118.0, 130.2 ms"
    bild = "  FEHLER: Spiel Zeit je Bild: Median 61 ms, 95 % 90 ms (Grenze Median 50 ms, 95 % 80 ms; Einzelmessungen 61/90)"
    layout = ["Layoutpruefung (Woerter, Knoepfe, Rand), 5 Schriftstufen x 25 Seiten x 3 Groessen: 1 Funde",
              '  pc 1360x880 gross dashboard: Wort getrennt: "Netzwerkdesig|n" in "Netzwerkdesign"']
    andere = "  FEHLER: Spiel Gesamtansicht: 45 Zeichenaufrufe, 170000 Dreiecke (Grenzen 40, 160000)"
    faelle = [  # (Ausnahme, Zeilen beider Laeufe, erwartete Rueckgabe von lauf2)
        ("macos-15-intel", [zeit, hoechst, farbe, bild], 0),
        ("macos-15-intel", [zeit] + layout, 3),
        ("macos-15-intel", [andere], 3),
        ("", [zeit, hoechst, farbe, bild], 3),
        ("macos-15", [zeit], 3),
        ("MACOS-15-INTEL", [zeit], 3),
        ("", layout, 3),
    ]
    rot = 0
    alt = os.environ.get("LERNPLATTFORM_ZEITEN_NUR_INFO")
    with tempfile.TemporaryDirectory() as ordner:
        for nr, (wert, zeilen, soll) in enumerate(faelle, 1):
            os.environ["LERNPLATTFORM_ZEITEN_NUR_INFO"] = wert
            dateien = []
            for lauf in (1, 2):
                pfad = os.path.join(ordner, "fall%d_lauf%d.txt" % (nr, lauf))
                with open(pfad, "w", encoding="utf-8") as d:
                    d.write("\n".join(zeilen + ["URTEIL: rot (%d Funde)" % len(zeilen)]) + "\n")
                dateien.append(pfad)
            ist, text = still(lauf2, 3, dateien[0], dateien[1])
            if ist != soll:
                print(text, end="")
                print("FEHLER: Selbsttest Fall %d (Ausnahme '%s'): Rueckgabe %d statt %d" % (nr, wert, ist, soll))
                rot = 1
            else:
                print("Selbsttest Fall %d (Ausnahme '%s', feste Zeilen, kein Programmfund): Rueckgabe %d wie erwartet" % (nr, wert, ist))
        # Weisung P11-38: 2. Lauf nach Rueckgabe 3; nur am Intel-Mac bei nur Messzeiten keiner (Gegenprobe ohne Ausnahme)
        neustart = [  # (Ausnahme, Zeilen des 1. Laufs, 2. Lauf erwartet)
            ("macos-15-intel", [zeit, hoechst, farbe, bild], False),
            ("macos-15-intel", [zeit] + layout, True),
            ("macos-15-intel", [zeit, andere], True),
            ("macos-15-intel", ["  Kontrast Text auf Karte: 3.9 UNTER 4.5"], True),
            ("macos-15-intel", [], True),
            ("", [zeit, hoechst, farbe, bild], True),
            ("macos-15", [zeit], True),
            ("MACOS-15-INTEL", [zeit], True),
        ]
        for nr, (wert, zeilen, soll) in enumerate(neustart, 1):
            os.environ["LERNPLATTFORM_ZEITEN_NUR_INFO"] = wert
            pfad = os.path.join(ordner, "neustart%d.txt" % nr)
            with open(pfad, "w", encoding="utf-8") as d:
                d.write("\n".join(zeilen + ["URTEIL: rot (%d Funde)" % len(zeilen)]) + "\n")
            ausgabe = os.path.join(ordner, "ausgabe%d.txt" % nr)
            ist, text = still(lauf1, 3, pfad, ausgabe=ausgabe)
            with open(ausgabe, encoding="utf-8") as f:
                gesetzt = f.read()
            erwartet = "zweiter_lauf=%s\n" % ("ja" if soll else "nein")
            if ist != 0 or gesetzt != erwartet:
                print(text, end="")
                print("FEHLER: Selbsttest Neustart Fall %d (Ausnahme '%s'): Rueckgabe %d, Ausgabe %r statt %r" % (nr, wert, ist, gesetzt, erwartet))
                rot = 1
            else:
                print("Selbsttest Neustart Fall %d (Ausnahme '%s'): %s" % (nr, wert, "2. Lauf" if soll else "kein 2. Lauf"))
        # Sammelliste 199: Rueckgabe 4 (Wachhund) ist in beiden Laeufen sofort rot, auch mit gueltiger Messdatei
        pfad = os.path.join(ordner, "haenger.txt")
        with open(pfad, "w", encoding="utf-8") as d:
            d.write("URTEIL: gruen\n")
        if still(lauf1, HAENGER, pfad)[0] != 1 or still(lauf2, HAENGER, pfad, pfad)[0] != 1:
            print("FEHLER: Selbsttest Wachhund: Rueckgabe 4 nicht rot")
            rot = 1
    rot |= wachhund_unter_zeitlimit()
    rot |= tests_jobs_gleich_release()
    rot |= feste_jobs_gleich_release()
    # Phase 13 Welle 2 (Weisung P12-17): der Werkzeug-Release verlangt denselben vollen Lauf 17/17
    rot |= tests_jobs_gleich_release(release=".github/workflows/stadtbau_release.yml")
    rot |= feste_jobs_gleich_release(release=".github/workflows/stadtbau_release.yml")
    rot |= selbsttest_feste_jobs_gegenprobe()
    # Weisung P12-20: Stufe werkzeug, Liste soll_werkzeug in stadtbau_release.yml gleich den Jobs der Stufe, mit Gegenprobe
    rot |= werkzeug_jobs_gleich_release()
    rot |= selbsttest_werkzeug_gegenprobe()
    if alt is None:
        os.environ.pop("LERNPLATTFORM_ZEITEN_NUR_INFO", None)
    else:
        os.environ["LERNPLATTFORM_ZEITEN_NUR_INFO"] = alt
    print("Selbsttest Urteil: %d Faelle, %d Neustart-Faelle, %s" % (len(faelle), len(neustart), "rot" if rot else "gruen"))
    return rot


def main():
    # Windows: Ausgabe ohne \r (Weisung P4-14 b)
    sys.stdout.reconfigure(newline="\n")
    if len(sys.argv) == 4 and sys.argv[1] == "lauf1":
        return lauf1(int(sys.argv[2]), sys.argv[3])
    if len(sys.argv) == 2 and sys.argv[1] == "selbsttest":
        return selbsttest()
    if len(sys.argv) == 5 and sys.argv[1] == "lauf2":
        return lauf2(int(sys.argv[2]), sys.argv[3], sys.argv[4])
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main())
