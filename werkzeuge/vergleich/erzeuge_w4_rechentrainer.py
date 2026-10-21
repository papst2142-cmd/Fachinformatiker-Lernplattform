#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# Erzeugt die Vergleichswerte G31 (Rechentrainer, Aufgabenarten "einheiten",
# "dauer" und "video", Stufen 1-3) fuer Phase 3 / Welle W4. Unabhaengig
# geschrieben nur aus der Spezifikation "Auftrag 2, Teil B" (ohne C#-Code).
# Herkunft "video" (Welle W4b): Spezifikation "G31-Erweiterung: Trainer-Art
# video" auf Grundlage der Wortliste W4b Abschnitt 6 (P3-20, freigegeben mit
# Weisung P3-21: Schwer unter 1 GiB in MiB, sonst GiB), ebenfalls ohne C#-Code.
# Die Aufgaben von "einheiten" und "dauer" bleiben bytegleich.
# Herkunft WiSo (Welle W4c): Arten bezug, verkauf, deckung, afa, zinsen,
# amort nach Plan P3-15 Abschnitt 7, Wortliste W4c Abschnitt 6 (P3-22) und
# der Praezisierung "G31-Erweiterung: Trainer-Arten WiSo" (gilt vor), ohne
# C#-Code. Die Aufgaben von einheiten, dauer und video bleiben bytegleich.
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
#  T-l) WiSo rechnet mit Decimal in einem lokalen Kontext prec 50 (nur in
#       den WiSo-Bauern; einheiten/dauer/video bleiben im Standardkontext).
#  T-m) Halber-Cent-Regel gilt fuer JEDEN gerundeten Schritt, auch fuer die
#       gezeigten Zwischenwerte "fix / db" (deckung Stufe 3) und
#       "i x 12 / r" (amort Stufe 3); Treffer -> ganze Aufgabe neu ziehen.
#  T-n) In den Schleifen werden alle Werte jeder Runde neu gezogen (wie T-i);
#       die Reihenfolge der Abbruchpruefungen ist ohne Einfluss, weil jede
#       Pruefung nur "neu ziehen" ausloest.
#  T-o) Stufen ohne Schleife (bezug 1, verkauf 1, zinsen 1, afa 1/2) pruefen
#       die Halber-Cent-Regel per assert (tritt dort nie auf).
#  T-p) Teilung mit Decimal: erst multiplizieren, dann teilen, dann q();
#       die Halber-Cent-Pruefung laeuft exakt mit Fraction auf denselben
#       Zaehler/Nenner.

import json
import random
import sys
from decimal import Decimal, ROUND_HALF_UP, localcontext
from fractions import Fraction

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


# ---------------------------------------------------------------------------
# WiSo (Welle W4c): bezug, verkauf, deckung, afa, zinsen, amort
# ---------------------------------------------------------------------------

EURO = "\u20ac"
FELD_EURO = [["ergebnis", f"Ergebnis ({EURO})"]]
FELD_STUECK = [["ergebnis", "Ergebnis (St\u00fcck)"]]
FELD_JAHRE = [["ergebnis", "Ergebnis (Jahre)"]]
FELD_JM = [["jahre", "Jahre"], ["monate", "Monate"]]
RUNDE_ZWISCHEN = "Runde jeden Zwischenschritt auf volle Cent."
CENT = Decimal("0.01")
MONATSNAMEN = ["Januar", "Februar", "M\u00e4rz", "April", "Mai", "Juni", "Juli",
               "August", "September", "Oktober", "November", "Dezember"]


class Halb(Exception):
    """Ein gerundeter Schritt liegt genau auf einem halben Cent."""


def q(x):
    return x.quantize(CENT, rounding=ROUND_HALF_UP)


def halb(zaehler, nenner):
    return (Fraction(zaehler) * 100 / Fraction(nenner)).denominator == 2


def qt(zaehler, nenner):
    # T-p: erst multiplizieren (zaehler), dann teilen, dann runden
    if halb(zaehler, nenner):
        raise Halb()
    return q(Decimal(zaehler) / Decimal(nenner))


def geld(x):
    s = format(q(Decimal(x)), ",.2f")
    return s.replace(",", "\x00").replace(".", ",").replace("\x00", ".")


def pz(p):
    s = format(Decimal(p), "f")
    if "." in s:
        s = s.rstrip("0").rstrip(".")
    return s.replace(".", ",")


def zahl(n):
    return format(n, ",").replace(",", ".")


def jahre_text(n):
    return "1 Jahr" if n == 1 else f"{n} Jahre"


def monate_text(n):
    return "1 Monat" if n == 1 else f"{n} Monate"


def aufrunden(zaehler, nenner):
    f = Fraction(zaehler) / Fraction(nenner)
    return -((-f.numerator) // f.denominator)


def ziehe(rng, ziehen, rechnen, schleife):
    # T-n/T-o: Schleife zieht alle Werte neu; ohne Schleife assert
    while True:
        werte = ziehen(rng)
        try:
            erg = rechnen(*werte)
        except Halb:
            assert schleife, "halber Cent in Stufe ohne Schleife"
            continue
        if erg is None:
            assert schleife
            continue
        return erg


R_BEZUG = [5, 10, 15, 20, 25, 30]
S_BEZUG = [1, 2, 3]


def bezug_rechnen(stufe, lep, r, s=None, bk=None):
    rab = qt(lep * r, 100)
    zep = lep - rab
    schritte = [f"Rabatt: {geld(lep)} x {r} / 100 = {geld(rab)} {EURO}",
                f"Zieleinkaufspreis: {geld(lep)} - {geld(rab)} = {geld(zep)} {EURO}"]
    if stufe == 1:
        text = (f"Ein Lieferant bietet eine Ware f\u00fcr {geld(lep)} {EURO} an und gew\u00e4hrt "
                f"{r} % Rabatt. Wie hoch ist der Zieleinkaufspreis?")
        return text, FELD_EURO, {"ergebnis": geld(zep)}, schritte
    sk = qt(zep * s, 100)
    bep = zep - sk
    schritte += [f"Skonto: {geld(zep)} x {s} / 100 = {geld(sk)} {EURO}",
                 f"Bareinkaufspreis: {geld(zep)} - {geld(sk)} = {geld(bep)} {EURO}"]
    if stufe == 2:
        text = (f"Listeneinkaufspreis {geld(lep)} {EURO}, Rabatt {r} %, Skonto {s} %. "
                f"Wie hoch ist der Bareinkaufspreis? {RUNDE_ZWISCHEN}")
        return text, FELD_EURO, {"ergebnis": geld(bep)}, schritte
    bzp = bep + bk
    schritte.append(f"Bezugspreis: {geld(bep)} + {geld(bk)} = {geld(bzp)} {EURO}")
    text = (f"Listeneinkaufspreis {geld(lep)} {EURO}, Rabatt {r} %, Skonto {s} %, "
            f"Bezugskosten {geld(bk)} {EURO}. Wie hoch ist der Bezugspreis? {RUNDE_ZWISCHEN}")
    return text, FELD_EURO, {"ergebnis": geld(bzp)}, schritte


def aufgabe_bezug(stufe, rng):
    if stufe == 1:
        z = lambda g: (Decimal(g.randint(10, 500) * 10), g.choice(R_BEZUG))
    elif stufe == 2:
        z = lambda g: (Decimal(g.randint(100, 5000)), g.choice(R_BEZUG), g.choice(S_BEZUG))
    else:
        z = lambda g: (Decimal(g.randint(10000, 500000)) / 100, g.choice(R_BEZUG),
                       g.choice(S_BEZUG), Decimal(g.randint(1, 20) * 5))
    return ziehe(rng, z, lambda *w: bezug_rechnen(stufe, *w), stufe != 1)


H_VERKAUF = [10, 15, 20, 25, 30, 40, 50]
G_VERKAUF = [5, 8, 10, 12, 15, 20, 25]
KS_VERKAUF = [2, 3]
KR_VERKAUF = [5, 10, 15, 20]
U_VERKAUF = [7, 19]


def verkauf_rechnen(stufe, bp, h, g=None, ks=None, kr=None, u=None):
    hk = qt(bp * h, 100)
    sk = bp + hk
    schritte = [f"Handlungskosten: {geld(bp)} x {h} / 100 = {geld(hk)} {EURO}",
                f"Selbstkosten: {geld(bp)} + {geld(hk)} = {geld(sk)} {EURO}"]
    if stufe == 1:
        text = (f"Der Bezugspreis einer Ware ist {geld(bp)} {EURO}. Die Handlungskosten "
                f"betragen {h} %. Wie hoch sind die Selbstkosten?")
        return text, FELD_EURO, {"ergebnis": geld(sk)}, schritte
    gw = qt(sk * g, 100)
    bvp = sk + gw
    schritte += [f"Gewinn: {geld(sk)} x {g} / 100 = {geld(gw)} {EURO}",
                 f"Barverkaufspreis: {geld(sk)} + {geld(gw)} = {geld(bvp)} {EURO}"]
    if stufe == 2:
        text = (f"Bezugspreis {geld(bp)} {EURO}, Handlungskosten {h} %, Gewinn {g} %. "
                f"Wie hoch ist der Barverkaufspreis? {RUNDE_ZWISCHEN}")
        return text, FELD_EURO, {"ergebnis": geld(bvp)}, schritte
    ksk = qt(bvp * ks, 100 - ks)
    zvp = bvp + ksk
    krb = qt(zvp * kr, 100 - kr)
    lvp = zvp + krb
    ust = qt(lvp * u, 100)
    brutto = lvp + ust
    schritte += [f"Kundenskonto: {geld(bvp)} x {ks} / {100 - ks} = {geld(ksk)} {EURO}",
                 f"Zielverkaufspreis: {geld(bvp)} + {geld(ksk)} = {geld(zvp)} {EURO}",
                 f"Kundenrabatt: {geld(zvp)} x {kr} / {100 - kr} = {geld(krb)} {EURO}",
                 f"Listenverkaufspreis netto: {geld(zvp)} + {geld(krb)} = {geld(lvp)} {EURO}",
                 f"Umsatzsteuer: {geld(lvp)} x {u} / 100 = {geld(ust)} {EURO}",
                 f"Bruttoverkaufspreis: {geld(lvp)} + {geld(ust)} = {geld(brutto)} {EURO}"]
    text = (f"Bezugspreis {geld(bp)} {EURO}, Handlungskosten {h} %, Gewinn {g} %, "
            f"Kundenskonto {ks} %, Kundenrabatt {kr} %, Umsatzsteuer {u} %. "
            f"Wie hoch ist der Bruttoverkaufspreis? {RUNDE_ZWISCHEN}")
    return text, FELD_EURO, {"ergebnis": geld(brutto)}, schritte


def aufgabe_verkauf(stufe, rng):
    if stufe == 1:
        z = lambda g: (Decimal(g.randint(100, 5000)), g.choice(H_VERKAUF))
    elif stufe == 2:
        z = lambda g: (Decimal(g.randint(100, 5000)), g.choice(H_VERKAUF), g.choice(G_VERKAUF))
    else:
        z = lambda g: (Decimal(g.randint(10000, 500000)) / 100, g.choice(H_VERKAUF),
                       g.choice(G_VERKAUF), g.choice(KS_VERKAUF), g.choice(KR_VERKAUF),
                       g.choice(U_VERKAUF))
    return ziehe(rng, z, lambda *w: verkauf_rechnen(stufe, *w), stufe != 1)


def deckung_rechnen(stufe, p, vk, m=None, fix=None):
    db = p - vk
    schritte = [f"Deckungsbeitrag: {geld(p)} - {geld(vk)} = {geld(db)} {EURO}"]
    if stufe == 1:
        text = (f"Ein Artikel wird f\u00fcr {geld(p)} {EURO} je St\u00fcck verkauft. Die variablen "
                f"Kosten betragen {geld(vk)} {EURO} je St\u00fcck. Wie hoch ist der "
                "Deckungsbeitrag je St\u00fcck?")
        return text, FELD_EURO, {"ergebnis": geld(db)}, schritte
    if stufe == 2:
        ges = db * m
        erg = ges - fix
        if erg <= 0:
            return None
        schritte += [f"Gesamt: {geld(db)} x {zahl(m)} = {geld(ges)} {EURO}",
                     f"Betriebsergebnis: {geld(ges)} - {geld(fix)} = {geld(erg)} {EURO}"]
        text = (f"Preis {geld(p)} {EURO} je St\u00fcck, variable Kosten {geld(vk)} {EURO} je "
                f"St\u00fcck, Fixkosten {geld(fix)} {EURO}, Menge {zahl(m)} St\u00fcck. "
                "Wie hoch ist das Betriebsergebnis?")
        return text, FELD_EURO, {"ergebnis": geld(erg)}, schritte
    if fix % db == 0:
        return None
    gs = aufrunden(fix, db)
    gq = qt(fix, db)   # T-m
    if gq == gq.to_integral_value():
        w = "mehr als " + zahl(gs - 1)
    else:
        w = geld(gq)
    schritte.append(f"Gewinnschwelle: {geld(fix)} / {geld(db)} = {w}, aufgerundet {zahl(gs)} St\u00fcck")
    text = (f"Preis {geld(p)} {EURO} je St\u00fcck, variable Kosten {geld(vk)} {EURO} je "
            f"St\u00fcck, Fixkosten {geld(fix)} {EURO}. Wie hoch ist die Gewinnschwelle? "
            "Runde auf ganze St\u00fcck auf.")
    return text, FELD_STUECK, {"ergebnis": zahl(gs)}, schritte


def aufgabe_deckung(stufe, rng):
    def z(g):
        p = g.randint(20, 500)
        vk = g.randint(5, p - 5)
        if stufe == 1:
            return (Decimal(p), Decimal(vk))
        if stufe == 2:
            m = g.randint(10, 2000)
            fix = g.randint(1, 200) * 500
            return (Decimal(p), Decimal(vk), m, Decimal(fix))
        fix = g.randint(1, 200) * 500
        return (Decimal(p), Decimal(vk), None, Decimal(fix))
    return ziehe(rng, z, lambda *w: deckung_rechnen(stufe, *w), stufe != 1)


def afa_rechnen(stufe, j, nd, x=None):
    ak = j * nd
    schritte = [f"J\u00e4hrlich: {geld(ak)} / {nd} = {geld(j)} {EURO}"]
    if stufe == 1:
        text = (f"Ein Server kostet {geld(ak)} {EURO}. Die Nutzungsdauer betr\u00e4gt {nd} Jahre. "
                "Wie hoch ist die j\u00e4hrliche lineare Abschreibung?")
        return text, FELD_EURO, {"ergebnis": geld(j)}, schritte
    if stufe == 2:
        n = x
        ab = j * n
        rest = ak - ab
        schritte += [f"Abgeschrieben: {geld(j)} x {n} = {geld(ab)} {EURO}",
                     f"Restbuchwert: {geld(ak)} - {geld(ab)} = {geld(rest)} {EURO}"]
        text = (f"Ein Server kostet {geld(ak)} {EURO}, Nutzungsdauer {nd} Jahre, Anschaffung im "
                f"Januar. Wie hoch ist der Restbuchwert am Ende von Jahr {n}?")
        return text, FELD_EURO, {"ergebnis": geld(rest)}, schritte
    m = x
    e1 = qt(j * (13 - m), 12)
    schritte += [f"Monate: 13 - {m} = {13 - m}",
                 f"Erstes Jahr: {geld(j)} x {13 - m} / 12 = {geld(e1)} {EURO}"]
    text = (f"Ein Server kostet {geld(ak)} {EURO}, Nutzungsdauer {nd} Jahre, Anschaffung im "
            f"{MONATSNAMEN[m - 1]}. Wie hoch ist die Abschreibung im ersten Jahr (monatsgenau)? "
            "Runde auf volle Cent.")
    return text, FELD_EURO, {"ergebnis": geld(e1)}, schritte


def aufgabe_afa(stufe, rng):
    def z(g):
        if stufe == 1:
            return (Decimal(g.randint(10, 500) * 10), g.randint(3, 10))
        if stufe == 2:
            j = g.randint(10, 500) * 10
            nd = g.randint(3, 10)
            return (Decimal(j), nd, g.randint(1, nd - 1))
        j = g.randint(10, 1000) * 10
        nd = g.randint(3, 10)
        return (Decimal(j), nd, g.randint(2, 12))
    return ziehe(rng, z, lambda *w: afa_rechnen(stufe, *w), stufe == 3)


P_ZINS = [Decimal("1"), Decimal("1.5"), Decimal("2"), Decimal("2.5"), Decimal("3"),
          Decimal("3.5"), Decimal("4"), Decimal("4.5"), Decimal("5"), Decimal("6")]


def zinsen_rechnen(stufe, k, p, x):
    if stufe == 1:
        t = x * 360
    elif stufe == 2:
        t = x * 30
    else:
        t = x
    z = qt(k * p * t, 36000)
    zeile = f"Zinsen: {geld(k)} x {pz(p)} x {zahl(t)} / 36.000 = {geld(z)} {EURO}"
    if stufe == 1:
        text = (f"Ein Kapital von {geld(k)} {EURO} wird {jahre_text(x)} lang zu {pz(p)} % "
                "verzinst. Wie hoch sind die Zinsen?")
        schritte = [f"Tage: {x} x 360 = {zahl(t)}", zeile]
    elif stufe == 2:
        text = (f"Ein Kapital von {geld(k)} {EURO} wird {monate_text(x)} lang zu {pz(p)} % "
                "verzinst. Wie hoch sind die Zinsen? Runde auf volle Cent.")
        schritte = [f"Tage: {x} x 30 = {zahl(t)}", zeile]
    else:
        text = (f"Ein Kapital von {geld(k)} {EURO} wird {t} Tage lang zu {pz(p)} % verzinst. "
                "Rechne kaufm\u00e4nnisch (1 Monat = 30 Tage, 1 Jahr = 360 Tage). "
                "Wie hoch sind die Zinsen? Runde auf volle Cent.")
        schritte = [zeile]
    return text, FELD_EURO, {"ergebnis": geld(z)}, schritte


def aufgabe_zinsen(stufe, rng):
    def z(g):
        if stufe == 1:
            return (Decimal(g.randint(10, 500) * 100), g.choice(P_ZINS), g.randint(1, 5))
        if stufe == 2:
            return (Decimal(g.randint(10, 500) * 100), g.choice(P_ZINS), g.randint(1, 11))
        return (Decimal(g.randint(10, 1000) * 50), g.choice(P_ZINS), g.randint(10, 359))
    return ziehe(rng, z, lambda *w: zinsen_rechnen(stufe, *w), stufe != 1)


def amort_rechnen(stufe, a, b, c=None):
    if stufe == 1:
        e, n = a, b
        i = e * n
        text = (f"Eine neue Anlage kostet {geld(i)} {EURO} und spart {geld(e)} {EURO} pro Jahr. "
                "Nach wie vielen Jahren hat sie sich amortisiert?")
        schritte = [f"Amortisation: {geld(i)} / {geld(e)} = {n} Jahre"]
        return text, FELD_JAHRE, {"ergebnis": str(n)}, schritte
    if stufe == 2:
        r, l, n = a, b, c
        e = r + l
        i = r * n
        text = (f"Eine neue Anlage kostet {geld(i)} {EURO}, spart {geld(e)} {EURO} pro Jahr und hat "
                f"laufende Kosten von {geld(l)} {EURO} pro Jahr. Nach wie vielen Jahren hat sie "
                "sich amortisiert?")
        schritte = [f"R\u00fcckfluss: {geld(e)} - {geld(l)} = {geld(r)} {EURO}",
                    f"Amortisation: {geld(i)} / {geld(r)} = {n} Jahre"]
        return text, FELD_JAHRE, {"ergebnis": str(n)}, schritte
    i, r, l = a, b, c
    e = r + l
    mon = aufrunden(i * 12, r)
    if mon % 12 == 0 or mon > 240:
        return None
    if Fraction(i * 12) / Fraction(r) == mon:
        w = zahl(mon)
    else:
        mq = qt(i * 12, r)   # T-m
        if mq == mq.to_integral_value():
            w = "mehr als " + zahl(mon - 1) + ", aufgerundet " + zahl(mon)
        else:
            w = geld(mq) + ", aufgerundet " + zahl(mon)
    jj, mm = mon // 12, mon % 12
    teile = []
    if jj > 0:
        teile.append(jahre_text(jj))
    teile.append(monate_text(mm))
    text = (f"Eine neue Anlage kostet {geld(i)} {EURO}, spart {geld(e)} {EURO} pro Jahr und hat "
            f"laufende Kosten von {geld(l)} {EURO} pro Jahr. Wie lange dauert die Amortisation? "
            "Angefangene Monate z\u00e4hlen voll.")
    schritte = [f"R\u00fcckfluss: {geld(e)} - {geld(l)} = {geld(r)} {EURO}",
                f"Monate: {geld(i)} x 12 / {geld(r)} = {w}",
                f"Ergebnis: {zahl(mon)} Monate = {' und '.join(teile)}"]
    return text, FELD_JM, {"jahre": str(jj), "monate": str(mm)}, schritte


def aufgabe_amort(stufe, rng):
    def z(g):
        if stufe == 1:
            return (Decimal(g.randint(5, 100) * 100), g.randint(2, 8))
        if stufe == 2:
            return (Decimal(g.randint(5, 100) * 100), Decimal(g.randint(1, 20) * 100),
                    g.randint(2, 8))
        return (Decimal(g.randint(10, 500) * 100), Decimal(g.randint(5, 100) * 100),
                Decimal(g.randint(1, 20) * 100))
    return ziehe(rng, z, lambda *w: amort_rechnen(stufe, *w), stufe == 3)


def wiso(bau):
    # T-l: WiSo rechnet im lokalen Kontext prec 50
    def mit_kontext(stufe, rng):
        with localcontext() as ctx:
            ctx.prec = 50
            return bau(stufe, rng)
    return mit_kontext


def pruefe_wiso(art, stufe, loesung):
    # Selbstpruefung: Loesungen positiv, amort Stufe 3 Monate 1-11
    for v in loesung.values():
        if art == "amort" and stufe == 3:
            continue
        assert Decimal(v.replace(".", "").replace(",", ".")) > 0, (art, stufe, loesung)
    if art == "amort" and stufe == 3:
        assert 1 <= int(loesung["monate"]) <= 11, loesung
        assert int(loesung["jahre"]) * 12 + int(loesung["monate"]) > 0, loesung


def main():
    if len(sys.argv) != 2:
        print("Aufruf: python3 -I erzeuge_w4_rechentrainer.py <ziel.json>", file=sys.stderr)
        return 2
    aufgaben = []
    bauer = {"einheiten": aufgabe_einheiten, "dauer": aufgabe_dauer, "video": aufgabe_video,
             "bezug": wiso(aufgabe_bezug), "verkauf": wiso(aufgabe_verkauf),
             "deckung": wiso(aufgabe_deckung), "afa": wiso(aufgabe_afa),
             "zinsen": wiso(aufgabe_zinsen), "amort": wiso(aufgabe_amort)}
    for art in ["einheiten", "dauer", "video", "bezug", "verkauf", "deckung", "afa", "zinsen", "amort"]:
        for stufe in [1, 2, 3]:
            for startwert in [f"{s}-{n}" for s in ["0", "1", "4711", "123456789"] for n in range(10)]:
                rng = random.Random(f"{art}-{stufe}-{startwert}")
                bau = bauer[art]
                text, felder, loesung, schritte = bau(stufe, rng)
                if art not in ("einheiten", "dauer", "video"):
                    pruefe_wiso(art, stufe, loesung)
                aufgaben.append({"art": art, "stufe": stufe, "startwert": startwert,
                                 "text": text, "felder": felder, "loesung": loesung,
                                 "schritte": schritte})
    daten = {
        "quelle": "Unabhaengig aus der Spezifikation G31 (Auftrag 2, Teil B) geschrieben, ohne C#-Code."
                  "; video aus Wortliste W4b Abschnitt 6"
                  "; WiSo aus Wortliste W4c Abschnitt 6",
        "python": sys.version.split()[0],
        "aufgaben": aufgaben,
    }
    with open(sys.argv[1], "w", encoding="utf-8", newline="\n") as d:
        json.dump(daten, d, ensure_ascii=False, indent=1, sort_keys=False)
        d.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
