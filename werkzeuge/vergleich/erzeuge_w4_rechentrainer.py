#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# Erzeugt die Vergleichswerte G31 (Rechentrainer, Aufgabenarten "einheiten",
# "dauer" und "video", Stufen 1-3) fuer Phase 3 / Welle W4. Unabhaengig
# geschrieben nur aus der Spezifikation "Auftrag 2, Teil B" (ohne C#-Code).
# Herkunft "video" (Welle W4b): Spezifikation "G31-Erweiterung: Trainer-Art
# video" auf Grundlage der Wortliste W4b Abschnitt 6 (P3-20, freigegeben mit
# Weisung P3-21: Schwer unter 1 GiB in MiB, sonst GiB), ebenfalls ohne C#-Code.
# Die Aufgaben von "einheiten" und "dauer" bleiben bytegleich.
# Python 3.12, nur Standardbibliothek. Aufruf:
#   python3 -I erzeuge_w4_rechentrainer.py <ziel.json>
# Ausgabe ist deterministisch (zweimal laufen = bytegleich).
#
# Entscheidungen bei Unklarheiten:
#  T-a) Basis in den Rechenschritten ("{basis}^{exp}") wird ohne
#       Tausenderpunkt geschrieben: "1000^2", "1024^3" (es ist eine
#       Potenzschreibweise, kein Zahltext aus de()).
#  T-b) Der Prozentwert p erscheint in Text und Schritten als str(p)
#       (p ist immer int), wie in der Spezifikation "{p}".
#  T-c) Zahltext de() wie G28: Decimal(repr(x)), ROUND_HALF_UP, mit lokalem
#       Kontext prec 200; fuer int liefert repr die Ziffern, also identisch.
#       Die Pruefungen "3. Nachkommastelle" und "% 60" laufen dagegen im
#       Standardkontext (prec 28), genau wie in der Spezifikation notiert;
#       die Werte sind dort klein genug, dass das Ergebnis exakt ist.
#  T-d) Byte/x werden genau wie notiert gerechnet: byte = w * F[von] ist
#       bei int-Faktoren ein int (de(byte,0) also ein int-Text), x = byte / F
#       ist ein float. Bei von == "Bit" ist byte = w / 8 (float).
#  T-e) Nicht-ASCII-Zeichen (ae, ue) stehen in den Zeichenketten als
#       \u-Escape, damit der Quelltext ASCII bleibt; im JSON stehen sie
#       als echte Zeichen (ensure_ascii=False).
#  T-f) Die Schleife bei "dauer" Stufe 3 zieht in jedem Durchlauf e, g, r, p
#       neu (in dieser Reihenfolge) und rechnet t erst danach.
#  T-h) "video": Breite, Hoehe, Farbtiefe, Bildrate, Minuten und
#       Kompression stehen in Text und Schritten als str(w) ohne
#       Tausenderpunkt (Spezifikation "Zahlen im Text"); die Potenz in
#       Stufe 3 als "1024^2" bzw. "1024^3" wie T-a.
#  T-i) "video" Stufe 2/3: jede Schleifenrunde zieht alle Werte neu in der
#       Reihenfolge der Spezifikation (w,h; d; fps; m; y), auch wenn nur
#       das Ergebnis die Abbruchbedingung verletzt hat (wie T-f).
#  T-j) "video" Stufe 3: komp = unkomp / y ist immer float (auch wenn die
#       Teilung aufgeht); ganz_text(komp) zeigt dann trotzdem eine ganze
#       Zahl ohne Komma. Die Einheitenwahl vergleicht komp < 1024 ** 3
#       als float gegen int, ohne Rundung.
#  T-k) "video": "1.000.000" im Schritt der Stufe 2 ist fester Text, keine
#       Zahl aus de(); "gross" steht im String als \u00df-Escape (T-e).

import json
import random
import sys
from decimal import Decimal, ROUND_HALF_UP, localcontext

F = {
    "Byte": 1,
    "kB": 1000, "MB": 1000 ** 2, "GB": 1000 ** 3, "TB": 1000 ** 4,
    "KiB": 1024, "MiB": 1024 ** 2, "GiB": 1024 ** 3, "TiB": 1024 ** 4,
}
BASIS_EXP = {
    "kB": (1000, 1), "MB": (1000, 2), "GB": (1000, 3), "TB": (1000, 4),
    "KiB": (1024, 1), "MiB": (1024, 2), "GiB": (1024, 3), "TiB": (1024, 4),
}
BINAER = "Bin\u00e4r"
BINAER_KLEIN = "bin\u00e4r"
UEBERTRAGEN = "\u00fcbertragen"
# T-g) Stufennamen (Leicht/Mittel/Schwer) gehoeren nicht ins JSON-Schema
#      der Spezifikation; sie stehen nur zur Doku hier.
STUFEN = {1: "Leicht", 2: "Mittel", 3: "Schwer"}


def de(x, n):
    with localcontext() as ctx:
        ctx.prec = 200
        q = Decimal(repr(x)).quantize(Decimal(1).scaleb(-n), rounding=ROUND_HALF_UP)
        s = format(q, ",.%df" % n)
    return s.replace(",", "\x00").replace(".", ",").replace("\x00", ".")


def ganz_text(x):
    # G28-Regel: ganz -> de(x,0), sonst de(x,3) ohne End-Nullen
    if x == int(x):
        return de(x, 0)
    s = de(x, 3)
    if "," in s:
        s = s.rstrip("0")
        if s.endswith(","):
            s = s[:-1]
    return s


def system(e):
    return "Dezimal" if BASIS_EXP[e][0] == 1000 else BINAER


def system_klein(e):
    return "dezimal" if BASIS_EXP[e][0] == 1000 else BINAER_KLEIN


def potenz(e):
    b, x = BASIS_EXP[e]
    return f"{b}^{x}"   # T-a


def dritte_stelle_5(x):
    return int(Decimal(repr(x)) * 1000) % 10 == 5


def einheiten_x(w, von, nach):
    if von == "Bit":
        byte = w / 8
    else:
        byte = w * F[von]
    if nach == "Bit":
        x = byte * 8
    else:
        x = byte / F[nach]
    return byte, x


def aufgabe_einheiten(stufe, rng):
    typ = "dez"
    if stufe == 1:
        typ = rng.choice(["dez", "bit"])
        if typ == "dez":
            von, nach = rng.choice([("kB", "Byte"), ("MB", "kB"), ("GB", "MB"), ("TB", "GB"),
                                    ("Byte", "kB"), ("kB", "MB"), ("MB", "GB"), ("GB", "TB")])
            if F[von] > F[nach]:
                w = rng.randint(2, 999)
            else:
                w = rng.randint(2, 999) * 1000
        else:
            von, nach = rng.choice([("Byte", "Bit"), ("Bit", "Byte")])
            if von == "Byte":
                w = rng.randint(2, 4096)
            else:
                w = rng.randint(2, 4096) * 8
        byte, x = einheiten_x(w, von, nach)
    elif stufe == 2:
        von, nach = rng.choice([("KiB", "Byte"), ("MiB", "KiB"), ("GiB", "MiB"), ("TiB", "GiB"),
                                ("MiB", "Byte"), ("GiB", "KiB"), ("TiB", "MiB"), ("Byte", "KiB"),
                                ("KiB", "MiB"), ("MiB", "GiB"), ("GiB", "TiB")])
        if F[von] > F[nach]:
            w = rng.randint(2, 64)
        else:
            w = rng.randint(1, 64) * 1024
        byte, x = einheiten_x(w, von, nach)
    else:
        while True:
            von, nach = rng.choice([("GB", "GiB"), ("TB", "TiB"), ("MB", "MiB"), ("GiB", "GB"),
                                    ("TiB", "TB"), ("MiB", "MB"), ("TB", "GiB"), ("GB", "MiB")])
            w = rng.randint(1, 2000)
            byte, x = einheiten_x(w, von, nach)
            if x >= 0.01 and not dritte_stelle_5(x):
                break
    L = de(x, 2) if stufe == 3 else de(x, 0)
    if typ == "bit":
        if von == "Byte":
            text = f"Wie viele Bit sind {de(w, 0)} Byte?"
            schritte = [f"Byte -> Bit: {de(w, 0)} x 8 = {L} Bit"]
        else:
            text = f"Wie viele Byte sind {de(w, 0)} Bit?"
            schritte = [f"Bit -> Byte: {de(w, 0)} / 8 = {L} Byte"]
    else:
        text = f"Rechne {de(w, 0)} {von} in {nach} um."
        if stufe == 3:
            text += " Runde auf 2 Nachkommastellen."
        schritte = []
        if von != "Byte":
            schritte.append(f"{system(von)} -> Byte: {de(w, 0)} x {potenz(von)} = {de(byte, 0)} Byte")
        if nach != "Byte":
            schritte.append(f"Byte -> {system_klein(nach)}: {de(byte, 0)} / {potenz(nach)} = {L} {nach}")
    return text, [["ergebnis", f"Ergebnis ({nach})"]], {"ergebnis": L}, schritte


def dauer_t(g, e, r, p):
    bit = g * F[e] * 8
    rate = r * 10 ** 6 * p / 100
    t = bit / rate
    return bit, rate, t


def aufgabe_dauer(stufe, rng):
    if stufe == 1:
        while True:
            r = rng.choice([2, 4, 5, 6, 8, 10, 16, 20, 25, 50, 100])
            s = rng.randint(10, 900)
            if (s * r) % 8 == 0:
                break
        g = s * r // 8
        e, p = "MB", 100
        bit, rate, t = dauer_t(g, e, r, p)
    elif stufe == 2:
        while True:
            g = rng.randint(1, 50)
            r = rng.choice([10, 16, 20, 25, 40, 50, 80, 100, 200, 250, 500])
            if (g * 8000) % r == 0 and 60 <= g * 8000 // r < 3600:
                break
        e, p = "GB", 100
        bit, rate, t = dauer_t(g, e, r, p)
    else:
        while True:   # T-f
            e = rng.choice(["GB", "GiB"])
            g = rng.randint(1, 100)
            r = rng.choice([16, 25, 50, 100, 250, 500, 1000])
            p = rng.choice([50, 60, 70, 75, 80, 85, 90, 95])
            bit, rate, t = dauer_t(g, e, r, p)
            if (60 <= t < 360000 and not dritte_stelle_5(t)
                    and Decimal(repr(t)) % 60 < Decimal("59.995")):
                break
    with localcontext() as ctx:
        ctx.prec = 200
        c = int(Decimal(repr(t)).quantize(Decimal("0.01"), rounding=ROUND_HALF_UP) * 100)
    std = c // 360000
    minute = (c % 360000) // 6000
    sek = c % 6000
    sek_text = f"{sek // 100},{sek % 100:02d}"
    if stufe == 1:
        text = (f"Eine Datei mit {de(g, 0)} MB wird mit {de(r, 0)} Mbit/s {UEBERTRAGEN}. "
                "Die nutzbare Leistung ist 100 %. Wie viele Sekunden dauert das?")
        felder = [["sekunden", "Sekunden"]]
        loesung = {"sekunden": de(t, 0)}
    elif stufe == 2:
        text = (f"Eine Datei mit {de(g, 0)} GB wird mit {de(r, 0)} Mbit/s {UEBERTRAGEN}. "
                "Die nutzbare Leistung ist 100 %. Wie lange dauert das?")
        felder = [["minuten", "Minuten"], ["sekunden", "Sekunden"]]
        loesung = {"minuten": str(minute), "sekunden": str(sek // 100)}
    else:
        text = (f"Eine Datei mit {de(g, 0)} {e} wird mit {de(r, 0)} Mbit/s {UEBERTRAGEN}. "
                f"Die nutzbare Leistung ist {p} %. Wie lange dauert das? "
                "Runde die Sekunden auf 2 Nachkommastellen.")
        felder = [["stunden", "Stunden"], ["minuten", "Minuten"], ["sekunden", "Sekunden"]]
        loesung = {"stunden": str(std), "minuten": str(minute), "sekunden": sek_text}
    bt = ganz_text(bit)
    rt = ganz_text(rate)
    schritte = [f"Bit: {de(g, 0)} x {potenz(e)} x 8 = {bt} Bit",
                f"Wirksame Datenrate: {de(r, 0)} x 1.000.000 x {p} / 100 = {rt} bit/s",
                f"Dauer: {bt} / {rt} = {de(t, 2)} Sekunden"]
    if stufe in (2, 3):
        schritte.append(f"Aufteilen: {de(t, 2)} Sek. = {std} Std. {minute} Min. {sek_text} Sek.")
    return text, felder, loesung, schritte


AUFLOESUNGEN = [(640, 480), (800, 600), (1024, 768), (1280, 720), (1366, 768),
                (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160)]
FARBTIEFEN = [8, 16, 24, 32]
BILDRATEN = [24, 25, 30, 50, 60]
KOMPRESSIONEN = [10, 20, 25, 40, 50, 100, 200]
GROSS = "gro\u00df"


def aufgabe_video(stufe, rng):
    if stufe == 1:
        w, h = rng.choice(AUFLOESUNGEN)
        d = rng.choice(FARBTIEFEN)
        bit = w * h * d
        byte = bit // 8
        text = (f"Ein Bild hat {w} x {h} Pixel und {d} Bit Farbtiefe. "
                "Wie viele Byte braucht es unkomprimiert?")
        felder = [["ergebnis", "Ergebnis (Byte)"]]
        loesung = {"ergebnis": de(byte, 0)}
        schritte = [f"Bit je Bild: {w} x {h} x {d} = {de(bit, 0)} Bit",
                    f"Byte je Bild: {de(bit, 0)} / 8 = {de(byte, 0)} Byte"]
    elif stufe == 2:
        while True:   # T-i
            w, h = rng.choice(AUFLOESUNGEN)
            d = rng.choice(FARBTIEFEN)
            fps = rng.choice(BILDRATEN)
            byte = w * h * d // 8
            rate = byte * fps * 8 / 1000000
            if not dritte_stelle_5(rate):
                break
        text = (f"Ein Video hat {w} x {h} Pixel, {d} Bit Farbtiefe und {fps} Bilder pro Sekunde. "
                f"Wie {GROSS} ist die Datenrate unkomprimiert? Runde auf 2 Nachkommastellen.")
        felder = [["ergebnis", "Ergebnis (Mbit/s)"]]
        loesung = {"ergebnis": de(rate, 2)}
        schritte = [f"Byte je Bild: {w} x {h} x {d} / 8 = {de(byte, 0)} Byte",
                    f"Datenrate: {de(byte, 0)} x {fps} x 8 / 1.000.000 = {de(rate, 2)} Mbit/s"]
    else:
        while True:   # T-i
            w, h = rng.choice(AUFLOESUNGEN)
            d = rng.choice(FARBTIEFEN)
            fps = rng.choice(BILDRATEN)
            m = rng.randint(1, 10)
            y = rng.choice(KOMPRESSIONEN)
            byte = w * h * d // 8
            unkomp = byte * fps * m * 60
            komp = unkomp / y   # T-j
            if komp < 1024 ** 3:
                einheit, x, pot = "MiB", komp / 1024 ** 2, "1024^2"
            else:
                einheit, x, pot = "GiB", komp / 1024 ** 3, "1024^3"
            if not dritte_stelle_5(x):
                break
        minutenwort = "1 Minute" if m == 1 else f"{m} Minuten"
        text = (f"Ein Video hat {w} x {h} Pixel, {d} Bit Farbtiefe und {fps} Bilder pro Sekunde. "
                f"Es dauert {minutenwort} und wird {y}-fach komprimiert. "
                f"Wie {GROSS} ist die Datei? Runde auf 2 Nachkommastellen.")
        felder = [["ergebnis", f"Ergebnis ({einheit})"]]
        loesung = {"ergebnis": de(x, 2)}
        kt = ganz_text(komp)
        schritte = [f"Byte je Bild: {w} x {h} x {d} / 8 = {de(byte, 0)} Byte",
                    f"Datenmenge: {de(byte, 0)} x {fps} x {m} x 60 = {de(unkomp, 0)} Byte",
                    f"Komprimiert: {de(unkomp, 0)} / {y} = {kt} Byte",
                    f"In {einheit}: {kt} / {pot} = {de(x, 2)} {einheit}"]
    return text, felder, loesung, schritte


def main():
    if len(sys.argv) != 2:
        print("Aufruf: python3 -I erzeuge_w4_rechentrainer.py <ziel.json>", file=sys.stderr)
        return 2
    aufgaben = []
    bauer = {"einheiten": aufgabe_einheiten, "dauer": aufgabe_dauer, "video": aufgabe_video}
    for art in ["einheiten", "dauer", "video"]:
        for stufe in [1, 2, 3]:
            for startwert in [f"{s}-{n}" for s in ["0", "1", "4711", "123456789"] for n in range(10)]:
                rng = random.Random(f"{art}-{stufe}-{startwert}")
                bau = bauer[art]
                text, felder, loesung, schritte = bau(stufe, rng)
                aufgaben.append({"art": art, "stufe": stufe, "startwert": startwert,
                                 "text": text, "felder": felder, "loesung": loesung,
                                 "schritte": schritte})
    daten = {
        "quelle": "Unabhaengig aus der Spezifikation G31 (Auftrag 2, Teil B) geschrieben, ohne C#-Code."
                  "; video aus Wortliste W4b Abschnitt 6",
        "python": sys.version.split()[0],
        "aufgaben": aufgaben,
    }
    with open(sys.argv[1], "w", encoding="utf-8", newline="\n") as d:
        json.dump(daten, d, ensure_ascii=False, indent=1, sort_keys=False)
        d.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
