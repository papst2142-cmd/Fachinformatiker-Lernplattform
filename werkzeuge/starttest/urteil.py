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
      schreibt lauf.sh (eine Quelle, ohne \r).
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
"""
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
    if rc not in (0, 3):
        return "Rueckgabe %d im %s. Lauf (Absturz oder Abbruch), kein Neustart" % (rc, lauf)
    if zeilen is None:
        return "Messdatei des %s. Laufs fehlt (Absturz), kein Neustart" % lauf
    for zeile in zeilen:
        if zeile.startswith(AUSNAHME):
            return "Ausnahme im %s. Lauf (%s), kein Neustart" % (lauf, zeile)
    return None


def lauf1(rc, datei):
    zeilen = lesen(datei)
    grund = absturz(rc, zeilen, 1)
    if grund:
        print("Start-Test ROT: " + grund)
        return 1
    if rc == 0:
        print("Start-Test gruen im 1. Lauf")
        return 0
    print("1. Lauf rot (Rueckgabe 3), es folgt ein 2. Lauf in neuem Prozess mit frischem Datenordner:")
    for punkt, zeile in punkte(zeilen).items():
        if zeiten_nur_info() and messzeit(zeile):
            print("  INFO Intel-Mac (Ausnahme P11-19, zaehlt nicht): " + zeile)
        else:
            print("  1. Lauf rot: " + zeile)
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
            ist = lauf2(3, dateien[0], dateien[1])
            if ist != soll:
                print("FEHLER: Selbsttest Fall %d (Ausnahme '%s'): Rueckgabe %d statt %d" % (nr, wert, ist, soll))
                rot = 1
    if alt is None:
        os.environ.pop("LERNPLATTFORM_ZEITEN_NUR_INFO", None)
    else:
        os.environ["LERNPLATTFORM_ZEITEN_NUR_INFO"] = alt
    print("Selbsttest Urteil: %d Faelle, %s" % (len(faelle), "rot" if rot else "gruen"))
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
