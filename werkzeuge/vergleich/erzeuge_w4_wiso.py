#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# Erzeugt die Vergleichswerte G30 (sechs kaufmaennische WiSo-Rechner:
# Bezug, Verkauf, Deckungsbeitrag, Abschreibung, Zinsen, Amortisation)
# fuer Phase 3 / Welle W4. Unabhaengig geschrieben nur aus Plan P3-15
# Abschnitt 4.4 und der Praezisierung G30, ohne C#-Code.
# Python 3.12, nur Standardbibliothek. Aufruf:
#   python3 -I erzeuge_w4_wiso.py <ziel.json>
# Ausgabe ist deterministisch (zweimal laufen = bytegleich).
#
# Entscheidungen bei Unklarheiten (siehe auch Kommentare im Code):
#  E-a) Tage bei Zinsen 1 bis 18000 (Praezisierung) statt 1 bis 360 (Plan);
#       die Praezisierung gilt bei Widerspruch.
#  E-b) "mehr als 2 Nachkommastellen" bei Geld wird am Wert geprueft
#       (x != x auf Cent gerundet), "nicht ganz" ebenso (x % 1 != 0);
#       so auch der Nachtrag zur Praezisierung: "19.990" ist gueltig,
#       "19.995" Fehler, "12.0" und "12.000" sind ganz (je Feld ein Fall in
#       den Grenzfaellen). Die Zufallsfaelle schreiben keine Endnullen in den Nachkommateil
#       (also nie "2.50", sondern "2.5"), damit kein Leser sie als
#       Tausenderpunkt deuten kann.
#  E-c) Pruefreihenfolge: alle Felder in Schluesselreihenfolge (je Feld Form,
#       dann Bereich); erst wenn alle Felder gueltig sind, folgen kein_db bzw.
#       nie. Ein Fehler-Eintrag hat nur rechner, eingabe, fehler.
#  E-d) Alle Geldausgaben gehen durch str(q(x)); bei schon exakten Summen
#       aendert q nur die Schreibweise auf genau 2 Stellen ("28" -> "28.00").
#  E-e) Gewinnschwelle exakt in ganzen Cent: -(-fix_cent // db_cent).
#  E-f) Rundungsgrenzen werden je Rechner am ERSTEN gerundeten Schritt gesucht
#       (bezug: Rabatt, verkauf: Handlungskosten, deckung: gs_dezimal,
#       afa: jaehrlich, zinsen: zinsen, amort: jahre). "Genau halber Cent"
#       wird mit fractions.Fraction exakt geprueft. Gegenrechnung HALF_EVEN:
#       gleicher Decimal-Wert mit ROUND_HALF_EVEN. Gegenrechnung float: die
#       gleiche Formel in float (gleiche Reihenfolge: erst mal, dann durch),
#       danach round(..., 2). Die Suche laeuft ueber eine feste Liste und
#       nimmt je Art den ersten Treffer: (1) beide weichen ab, (2) nur
#       HALF_EVEN weicht ab, (3) nur float weicht ab, (4) keine weicht ab.
#       Gefordert (assert): mindestens 3 Treffer, darunter mindestens einer
#       mit HALF_EVEN- und einer mit float-Abweichung.
#  E-g) Division in Decimal mit prec 50: bei Nennern aus Cent, 12, nd, 36000
#       liegt ein Ergebnis nie so nah an einem halben Cent, dass prec 50 die
#       Rundung kippen koennte.
#  E-h) AfA: ein Fall, in dem min(geplant, rest_vorher) greift
#       (ak 0.01, nd 2, monat 6: jaehrlich 0.01, Jahr 1 0.01, danach 0.00).
#  E-i) Reihenfolge im JSON: je Rechner (bezug, verkauf, deckung, afa,
#       zinsen, amort) erst die Pflichtfaelle in der Reihenfolge der
#       Praezisierung, dann direkt die 50 Zufallsfaelle dieses Rechners.
#  E-j) Grenzfaelle je Feld gehen von einem gueltigen Grundfall aus und
#       aendern nur dieses Feld. Ergibt sich dabei kein_db oder nie (z. B.
#       preis 0), steht genau dieser Fehler im Eintrag.
#  E-k) Zufallsbereiche (alle Grenzen eingeschlossen): siehe ZUFALL unten.
#       Geld: ganzer Teil gleichverteilt, dann 0 bis 2 Nachkommastellen;
#       Prozent: ganzer Teil, dann 0 bis 3 Nachkommastellen; liegt der Wert
#       ueber der oberen Grenze, wird neu gezogen. Deckung zieht vk so lange,
#       bis vk < preis; Amortisation zieht laufend so lange, bis
#       laufend < einsparung.

import json
import platform
import random
import sys
from decimal import Decimal, ROUND_HALF_UP, ROUND_HALF_EVEN, getcontext
from fractions import Fraction

getcontext().prec = 50

CENT = Decimal("0.01")
GELD_MAX = Decimal("1000000000")
PROZ_MAX_KLEIN = Decimal("99.999")
PROZ_MAX = Decimal("100")


def q(x):
    return x.quantize(CENT, rounding=ROUND_HALF_UP)


def q_even(x):
    return x.quantize(CENT, rounding=ROUND_HALF_EVEN)


def g(x):
    return str(q(x))


class Fehler(Exception):
    def __init__(self, feld, art):
        super().__init__(feld, art)
        self.feld = feld
        self.art = art


# Feldarten: ("geld", groesser_null) / ("proz_klein",) / ("proz",)
# / ("ganz", min, max)
FELDER = {
    "bezug": [("lep", ("geld", False)), ("rabatt", ("proz_klein",)),
              ("skonto", ("proz_klein",)), ("bezugskosten", ("geld", False))],
    "verkauf": [("bp", ("geld", False)), ("hk", ("proz_klein",)),
                ("gewinn", ("proz_klein",)), ("kskonto", ("proz_klein",)),
                ("krabatt", ("proz_klein",)), ("ust", ("proz",))],
    "deckung": [("preis", ("geld", False)), ("vk", ("geld", False)),
                ("fix", ("geld", False)), ("menge", ("ganz", 1, 10000000))],
    "afa": [("ak", ("geld", True)), ("nd", ("ganz", 1, 50)),
            ("monat", ("ganz", 1, 12))],
    "zinsen": [("kapital", ("geld", False)), ("zinssatz", ("proz",)),
               ("tage", ("ganz", 1, 18000))],
    "amort": [("investition", ("geld", True)), ("einsparung", ("geld", False)),
              ("laufend", ("geld", False))],
}
RECHNER = ["bezug", "verkauf", "deckung", "afa", "zinsen", "amort"]


def pruefe_feld(feld, art, x):
    if art[0] == "geld":
        if x != q(x):
            raise Fehler(feld, "nachkommastellen")
        if x < 0 or (art[1] and x == 0):
            raise Fehler(feld, "zu_klein")
        if x > GELD_MAX:
            raise Fehler(feld, "zu_gross")
    elif art[0] == "proz_klein":
        if x < 0:
            raise Fehler(feld, "zu_klein")
        if x > PROZ_MAX_KLEIN:
            raise Fehler(feld, "zu_gross")
    elif art[0] == "proz":
        if x < 0:
            raise Fehler(feld, "zu_klein")
        if x > PROZ_MAX:
            raise Fehler(feld, "zu_gross")
    elif art[0] == "ganz":
        if x % 1 != 0:
            raise Fehler(feld, "nicht_ganz")
        if x < art[1]:
            raise Fehler(feld, "zu_klein")
        if x > art[2]:
            raise Fehler(feld, "zu_gross")
    else:
        raise AssertionError(art)


def lies(rechner, eingabe):
    werte = {}
    for feld, art in FELDER[rechner]:
        x = Decimal(eingabe[feld])
        pruefe_feld(feld, art, x)
        werte[feld] = x
    return werte


# ---------------------------------------------------------------- Rechner

def r_bezug(e):
    lep, rabatt, skonto, bk = e["lep"], e["rabatt"], e["skonto"], e["bezugskosten"]
    rabatt_eur = q(lep * rabatt / 100)
    zep = lep - rabatt_eur
    skonto_eur = q(zep * skonto / 100)
    bep = zep - skonto_eur
    bezugspreis = bep + bk
    return {"rabatt": g(rabatt_eur), "zep": g(zep), "skonto": g(skonto_eur),
            "bep": g(bep), "bezugspreis": g(bezugspreis)}


def r_verkauf(e):
    bp = e["bp"]
    hk_eur = q(bp * e["hk"] / 100)
    sk = bp + hk_eur
    gewinn_eur = q(sk * e["gewinn"] / 100)
    bvp = sk + gewinn_eur
    ks = e["kskonto"]
    kskonto_eur = q(bvp * ks / (100 - ks))
    zvp = bvp + kskonto_eur
    kr = e["krabatt"]
    krabatt_eur = q(zvp * kr / (100 - kr))
    lvp = zvp + krabatt_eur
    ust_eur = q(lvp * e["ust"] / 100)
    brutto = lvp + ust_eur
    return {"hk": g(hk_eur), "sk": g(sk), "gewinn": g(gewinn_eur),
            "bvp": g(bvp), "kskonto": g(kskonto_eur), "zvp": g(zvp),
            "krabatt": g(krabatt_eur), "lvp": g(lvp), "ust": g(ust_eur),
            "brutto": g(brutto)}


def r_deckung(e):
    db = e["preis"] - e["vk"]
    if db <= 0:
        raise Fehler(None, "kein_db")
    menge = e["menge"]
    fix = e["fix"]
    db_gesamt = db * menge
    ergebnis = db_gesamt - fix
    gs_dezimal = q(fix / db)
    fix_c = int(fix * 100)
    db_c = int(db * 100)
    gewinnschwelle = -(-fix_c // db_c)  # E-e: exakt aufrunden
    return {"db": g(db), "db_gesamt": g(db_gesamt), "ergebnis": g(ergebnis),
            "gs_dezimal": g(gs_dezimal), "gewinnschwelle": gewinnschwelle}


def r_afa(e):
    ak = e["ak"]
    nd = int(e["nd"])
    monat = int(e["monat"])
    jaehrlich = q(ak / nd)
    n = nd if monat == 1 else nd + 1
    rest = ak
    jahre = []
    for i in range(1, n + 1):
        if i == n:
            geplant = rest
            if n == nd + 1:
                monate = monat - 1
            else:
                monate = 12
        elif i == 1:
            if monat == 1:
                geplant = jaehrlich
            else:
                geplant = q(jaehrlich * (13 - monat) / 12)
            monate = 13 - monat
        else:
            geplant = jaehrlich
            monate = 12
        betrag = min(geplant, rest)
        rest = rest - betrag
        jahre.append({"jahr": i, "monate": monate, "betrag": g(betrag),
                      "rest": g(rest)})
    return {"jaehrlich": g(jaehrlich), "jahre": jahre}


def r_zinsen(e):
    k = e["kapital"]
    z = q(k * e["zinssatz"] * e["tage"] / 36000)
    return {"zinsen": g(z), "endkapital": g(k + z)}


def r_amort(e):
    rueck = e["einsparung"] - e["laufend"]
    if rueck <= 0:
        raise Fehler(None, "nie")
    jahre = q(e["investition"] / rueck)
    return {"rueckfluss": g(rueck), "jahre": g(jahre)}


RECHNE = {"bezug": r_bezug, "verkauf": r_verkauf, "deckung": r_deckung,
          "afa": r_afa, "zinsen": r_zinsen, "amort": r_amort}


def fall(rechner, eingabe):
    eingabe = {f: eingabe[f] for f, _ in FELDER[rechner]}
    try:
        werte = RECHNE[rechner](lies(rechner, eingabe))
    except Fehler as f:
        return {"rechner": rechner, "eingabe": eingabe,
                "fehler": {"feld": f.feld, "art": f.art}}
    return {"rechner": rechner, "eingabe": eingabe, "fehler": None,
            "werte": werte}


def ein(rechner, *werte):
    namen = [f for f, _ in FELDER[rechner]]
    assert len(namen) == len(werte)
    return dict(zip(namen, werte))


def pruefe(f, **soll):
    assert f["fehler"] is None, f
    for k, v in soll.items():
        assert f["werte"][k] == v, (k, f["werte"][k], v)
    return f


def pruefe_fehler(f, feld, art):
    assert f["fehler"] == {"feld": feld, "art": art}, f
    return f


# ---------------------------------------------- Rundungsgrenzen (E-f)

def erster_schritt(rechner, e):
    """Liefert (exakt als Fraction, Decimal-Wert, float-Wert) des ersten
    gerundeten Schritts, oder None, wenn der Fall ungueltig ist."""
    F = lambda s: Fraction(s)
    fl = lambda s: float(s)
    if rechner == "bezug":
        a, b = e["lep"], e["rabatt"]
        return (F(a) * F(b) / 100, Decimal(a) * Decimal(b) / 100,
                fl(a) * fl(b) / 100)
    if rechner == "verkauf":
        a, b = e["bp"], e["hk"]
        return (F(a) * F(b) / 100, Decimal(a) * Decimal(b) / 100,
                fl(a) * fl(b) / 100)
    if rechner == "deckung":
        db = Decimal(e["preis"]) - Decimal(e["vk"])
        return (F(e["fix"]) / F(str(db)), Decimal(e["fix"]) / db,
                fl(e["fix"]) / float(db))
    if rechner == "afa":
        return (F(e["ak"]) / F(e["nd"]), Decimal(e["ak"]) / Decimal(e["nd"]),
                fl(e["ak"]) / fl(e["nd"]))
    if rechner == "zinsen":
        k, p, t = e["kapital"], e["zinssatz"], e["tage"]
        return (F(k) * F(p) * F(t) / 36000,
                Decimal(k) * Decimal(p) * Decimal(t) / 36000,
                fl(k) * fl(p) * fl(t) / 36000)
    if rechner == "amort":
        r = Decimal(e["einsparung"]) - Decimal(e["laufend"])
        return (F(e["investition"]) / F(str(r)), Decimal(e["investition"]) / r,
                fl(e["investition"]) / float(r))
    raise AssertionError(rechner)


def geld_text(cent):
    return "%d.%02d" % (cent // 100, cent % 100)


def kandidaten(rechner):
    if rechner == "bezug":
        for c in range(100, 300000):
            for r in ("12.5", "2.5", "7.5", "0.5"):
                yield ein("bezug", geld_text(c), r, "2", "10")
    elif rechner == "verkauf":
        for c in range(100, 300000):
            for h in ("12.5", "22.5", "7.5"):
                yield ein("verkauf", geld_text(c), h, "10", "2", "5", "19")
    elif rechner == "deckung":
        for c in range(1, 300000):
            for db in (8, 40, 2):
                yield ein("deckung", "100", str(100 - db), geld_text(c), "1000")
    elif rechner == "afa":
        for c in range(1, 300000):
            for nd in ("2", "8", "4"):
                yield ein("afa", geld_text(c), nd, "1")
    elif rechner == "zinsen":
        for c in range(100, 300000):
            for p, t in (("1", "180"), ("4", "90"), ("2.5", "72")):
                yield ein("zinsen", geld_text(c), p, t)
    elif rechner == "amort":
        for c in range(1, 300000):
            for r in (8, 40, 2):
                yield ein("amort", geld_text(c), str(100 + r), "100")


def rundungsgrenzen(rechner):
    treffer = {}
    for e in kandidaten(rechner):
        exakt, dez, flt = erster_schritt(rechner, e)
        # genau halber Cent: exakt * 1000 ist ganz und endet auf 5
        tausend = exakt * 1000
        if tausend.denominator != 1 or tausend.numerator % 10 != 5:
            continue
        hu = q(dez)
        assert Fraction(str(hu)) - exakt == Fraction(1, 200)  # weg von 0
        even_ab = q_even(dez) != hu
        float_ab = Decimal(repr(round(flt, 2))) != hu
        art = {(True, True): 1, (True, False): 2, (False, True): 3,
               (False, False): 4}[(even_ab, float_ab)]
        if art not in treffer:
            treffer[art] = (e, even_ab, float_ab)
        if len(treffer) == 4:
            break
    assert len(treffer) >= 3, (rechner, treffer)
    assert any(v[1] for v in treffer.values()), rechner
    assert any(v[2] for v in treffer.values()), rechner
    return [fall(rechner, treffer[k][0]) for k in sorted(treffer)]


# ------------------------------------------------- Grenzen und Fehler

GRUNDFALL = {
    "bezug": ein("bezug", "1200", "15", "2", "25"),
    "verkauf": ein("verkauf", "1024.60", "30", "10", "2", "10", "19"),
    "deckung": ein("deckung", "250", "150", "40000", "600"),
    "afa": ein("afa", "1800", "3", "4"),
    "zinsen": ein("zinsen", "5000", "4.5", "90"),
    "amort": ein("amort", "12000", "4800", "800"),
}


def varianten(art):
    """(text, erwarteter Feldfehler oder None)"""
    if art[0] == "geld":
        v = [("0", "zu_klein" if art[1] else None), ("0.01", None),
             ("1000000000", None), ("1000000000.01", "zu_gross"),
             ("-1", "zu_klein"), ("19.990", None),
             ("19.995", "nachkommastellen")]
        return v
    if art[0] == "proz_klein":
        return [("0", None), ("0.001", None), ("99.999", None),
                ("100", "zu_gross"), ("-1", "zu_klein")]
    if art[0] == "proz":
        return [("0", None), ("0.001", None), ("100", None),
                ("100.001", "zu_gross"), ("-1", "zu_klein")]
    if art[0] == "ganz":
        lo, hi = art[1], art[2]
        v = [("0", "zu_klein"), (str(lo), None), (str(hi), None),
             (str(hi + 1), "zu_gross"), ("-1", "zu_klein"),
             ("2.5", "nicht_ganz"), ("12.0" if hi >= 12 else "1.0", None),
             ("12.000" if hi >= 12 else "1.000", None)]
        return v
    raise AssertionError(art)


def grenzfaelle(rechner):
    aus = []
    for feld, art in FELDER[rechner]:
        for text, erwartet in varianten(art):
            e = dict(GRUNDFALL[rechner])
            e[feld] = text
            f = fall(rechner, e)
            if erwartet is not None:
                pruefe_fehler(f, feld, erwartet)
            else:
                assert f["fehler"] is None or f["fehler"]["feld"] is None, f
            aus.append(f)
    return aus


# ------------------------------------------------------------- Zufall

ZUFALL = {
    # Feld: (art, min ganzer Teil, max Wert)
    "bezug": [("lep", "geld", 1, 100000), ("rabatt", "proz", 0, 40),
              ("skonto", "proz", 0, 5), ("bezugskosten", "geld", 0, 500)],
    "verkauf": [("bp", "geld", 1, 100000), ("hk", "proz", 0, 80),
                ("gewinn", "proz", 0, 50), ("kskonto", "proz", 0, 5),
                ("krabatt", "proz", 0, 30), ("ust", "proz", 0, 25)],
    "deckung": [("preis", "geld", 1, 5000), ("vk", "geld", 0, 5000),
                ("fix", "geld", 0, 1000000), ("menge", "ganz", 1, 100000)],
    "afa": [("ak", "geld", 1, 1000000), ("nd", "ganz", 1, 50),
            ("monat", "ganz", 1, 12)],
    "zinsen": [("kapital", "geld", 1, 1000000), ("zinssatz", "proz", 0, 15),
               ("tage", "ganz", 1, 1800)],
    "amort": [("investition", "geld", 1, 1000000),
              ("einsparung", "geld", 1, 500000), ("laufend", "geld", 0, 500000)],
}


def zieh(rnd, art, lo, hi):
    if art == "ganz":
        return str(rnd.randint(lo, hi))
    stellen_max = 2 if art == "geld" else 3
    while True:
        ganz = rnd.randint(lo, hi)
        k = rnd.randint(0, stellen_max)
        if k == 0:
            text = str(ganz)
        else:
            nach = ("%0*d" % (k, rnd.randrange(10 ** k))).rstrip("0")  # E-b
            text = str(ganz) + ("." + nach if nach else "")
        if Decimal(text) <= hi:
            return text


def zufall(rechner):
    rnd = random.Random("G30-" + rechner)
    aus = []
    while len(aus) < 50:
        e = {}
        for feld, art, lo, hi in ZUFALL[rechner]:
            while True:
                t = zieh(rnd, art, lo, hi)
                if rechner == "deckung" and feld == "vk" \
                        and Decimal(t) >= Decimal(e["preis"]):
                    continue
                if rechner == "amort" and feld == "laufend" \
                        and Decimal(t) >= Decimal(e["einsparung"]):
                    continue
                break
            e[feld] = t
        f = fall(rechner, e)
        assert f["fehler"] is None, f
        aus.append(f)
    return aus


# ------------------------------------------------------- Pflichtfaelle

def faelle_bezug():
    r = "bezug"
    a = [pruefe(fall(r, ein(r, "1200", "15", "2", "25")), rabatt="180.00",
                zep="1020.00", skonto="20.40", bep="999.60",
                bezugspreis="1024.60"),
         pruefe(fall(r, ein(r, "840", "25", "3", "18")), rabatt="210.00",
                zep="630.00", skonto="18.90", bep="611.10",
                bezugspreis="629.10")]
    a += rundungsgrenzen(r)
    a += grenzfaelle(r)
    a.append(fall(r, ein(r, "1000000000", "99.999", "99.999", "1000000000")))
    a.append(fall(r, ein(r, "1000000000", "0", "0", "1000000000")))
    return a


def faelle_verkauf():
    r = "verkauf"
    a = [pruefe(fall(r, ein(r, "1024.60", "30", "10", "2", "10", "19")),
                hk="307.38", sk="1331.98", gewinn="133.20", bvp="1465.18",
                kskonto="29.90", zvp="1495.08", krabatt="166.12",
                lvp="1661.20", ust="315.63", brutto="1976.83"),
         pruefe(fall(r, ein(r, "629.10", "25", "12", "2", "5", "19")),
                hk="157.28", sk="786.38", gewinn="94.37", bvp="880.75",
                kskonto="17.97", zvp="898.72", krabatt="47.30",
                lvp="946.02", ust="179.74", brutto="1125.76")]
    a += rundungsgrenzen(r)
    # Kundenskonto vom Hundert waere anders als im Hundert
    e = ein(r, "1000", "20", "10", "3", "0", "19")
    f = fall(r, e)
    bvp = Decimal(f["werte"]["bvp"])
    assert q(bvp * 3 / 100) != Decimal(f["werte"]["kskonto"]), f
    a.append(f)
    # dasselbe fuer Kundenrabatt
    e = ein(r, "1000", "20", "10", "0", "15", "19")
    f = fall(r, e)
    zvp = Decimal(f["werte"]["zvp"])
    assert q(zvp * 15 / 100) != Decimal(f["werte"]["krabatt"]), f
    a.append(f)
    a += grenzfaelle(r)
    a.append(fall(r, ein(r, "1000000000", "99.999", "99.999", "99.999",
                         "99.999", "100")))
    a.append(fall(r, ein(r, "1000000000", "0", "0", "0", "0", "0")))
    return a


def faelle_deckung():
    r = "deckung"
    a = [pruefe(fall(r, ein(r, "250", "150", "40000", "600")), db="100.00",
                db_gesamt="60000.00", ergebnis="20000.00",
                gs_dezimal="400.00", gewinnschwelle=400),
         pruefe(fall(r, ein(r, "80", "52", "20000", "900")), db="28.00",
                db_gesamt="25200.00", ergebnis="5200.00",
                gs_dezimal="714.29", gewinnschwelle=715)]
    a += rundungsgrenzen(r)
    # glatte Gewinnschwelle, nicht glatte (abrunden waere falsch), fix 0
    f = pruefe(fall(r, ein(r, "19.99", "7.49", "1000", "10")),
               gewinnschwelle=80)
    a.append(f)
    f = pruefe(fall(r, ein(r, "19.99", "7.49", "1000.01", "10")),
               gewinnschwelle=81)
    assert int(Decimal("1000.01") / Decimal("12.50")) == 80
    a.append(f)
    a.append(pruefe(fall(r, ein(r, "10", "9.99", "0.02", "1")),
                    gewinnschwelle=2, ergebnis="-0.01"))
    a.append(pruefe(fall(r, ein(r, "10", "0", "0", "1")), gewinnschwelle=0,
                    gs_dezimal="0.00"))
    # Verlust (Ergebnis negativ)
    a.append(pruefe(fall(r, ein(r, "50", "45", "10000", "1000")),
                    ergebnis="-5000.00"))
    # kein_db mit db = 0 und db < 0
    a.append(pruefe_fehler(fall(r, ein(r, "100", "100", "500", "10")),
                           None, "kein_db"))
    a.append(pruefe_fehler(fall(r, ein(r, "100", "100.01", "500", "10")),
                           None, "kein_db"))
    a.append(pruefe_fehler(fall(r, ein(r, "0", "0", "0", "1")),
                           None, "kein_db"))
    a += grenzfaelle(r)
    a.append(fall(r, ein(r, "1000000000", "0", "1000000000", "10000000")))
    a.append(fall(r, ein(r, "1000000000", "999999999.99", "1000000000", "1")))
    return a


def faelle_afa():
    r = "afa"
    f = pruefe(fall(r, ein(r, "1800", "3", "4")), jaehrlich="600.00")
    assert [j["betrag"] for j in f["werte"]["jahre"]] == \
        ["450.00", "600.00", "600.00", "150.00"], f
    assert [j["monate"] for j in f["werte"]["jahre"]] == [9, 12, 12, 3], f
    a = [f]
    f = pruefe(fall(r, ein(r, "4500", "5", "10")), jaehrlich="900.00")
    jahre = f["werte"]["jahre"]
    assert [j["betrag"] for j in jahre] == \
        ["225.00", "900.00", "900.00", "900.00", "900.00", "675.00"], f
    assert jahre[2]["rest"] == "2475.00", f
    assert jahre[-1]["rest"] == "0.00", f
    a.append(f)
    a += rundungsgrenzen(r)
    # monat 1, 2, 12; nd 1 und 50
    for e in (ein(r, "1000", "4", "1"), ein(r, "1000", "4", "2"),
              ein(r, "1000", "4", "12"), ein(r, "999.99", "1", "1"),
              ein(r, "999.99", "1", "7"), ein(r, "1000", "50", "1"),
              ein(r, "1000", "50", "12"), ein(r, "1234567.89", "50", "6")):
        a.append(fall(r, e))
    f = a[-5]  # nd 1, monat 1
    assert f["werte"]["jahre"] == [{"jahr": 1, "monate": 12,
                                    "betrag": "999.99", "rest": "0.00"}], f
    f = a[-4]  # nd 1, monat 7
    assert [j["monate"] for j in f["werte"]["jahre"]] == [6, 6], f
    # ohne (13 - monat)/12 kaeme ein anderes erstes Jahr heraus
    f = fall(r, ein(r, "1000", "3", "8"))
    j = Decimal(f["werte"]["jaehrlich"])
    assert f["werte"]["jahre"][0]["betrag"] != str(j), f
    assert f["werte"]["jahre"][0]["betrag"] == "138.89", f
    a.append(f)
    # E-h: min(geplant, rest_vorher) greift
    f = fall(r, ein(r, "0.01", "2", "6"))
    assert [x["betrag"] for x in f["werte"]["jahre"]] == \
        ["0.01", "0.00", "0.00"], f
    a.append(f)
    # Summe der Betraege = ak in allen bisherigen gueltigen Faellen
    for x in a:
        if x["fehler"] is None:
            s = sum(Decimal(j["betrag"]) for j in x["werte"]["jahre"])
            assert s == Decimal(x["eingabe"]["ak"]), x
    a += grenzfaelle(r)
    a.append(fall(r, ein(r, "1000000000", "50", "12")))
    a.append(fall(r, ein(r, "1000000000", "1", "1")))
    return a


def faelle_zinsen():
    r = "zinsen"
    a = [pruefe(fall(r, ein(r, "5000", "4.5", "90")), zinsen="56.25",
                endkapital="5056.25"),
         pruefe(fall(r, ein(r, "8000", "3.6", "45")), zinsen="36.00",
                endkapital="8036.00"),
         pruefe(fall(r, ein(r, "12500", "2.4", "100")), zinsen="83.33",
                endkapital="12583.33")]
    a += rundungsgrenzen(r)
    # 365 statt 360 Tage gaebe ein anderes Ergebnis
    for e in (ein(r, "5000", "4.5", "90"), ein(r, "10000", "5", "200")):
        f = fall(r, e)
        k, p, t = (Decimal(e[x]) for x in ("kapital", "zinssatz", "tage"))
        assert q(k * p * t / 36500) != Decimal(f["werte"]["zinsen"]), f
        a.append(f)
    a.append(pruefe(fall(r, ein(r, "1000", "0", "360")), zinsen="0.00"))
    a.append(pruefe(fall(r, ein(r, "1000", "100", "360")), zinsen="1000.00"))
    a += grenzfaelle(r)
    a.append(fall(r, ein(r, "1000000000", "100", "18000")))
    return a


def faelle_amort():
    r = "amort"
    a = [pruefe(fall(r, ein(r, "12000", "4800", "800")), rueckfluss="4000.00",
                jahre="3.00"),
         pruefe(fall(r, ein(r, "18000", "7500", "1500")), rueckfluss="6000.00",
                jahre="3.00")]
    a += rundungsgrenzen(r)
    a.append(pruefe(fall(r, ein(r, "10000", "4000", "1000")), jahre="3.33"))
    # nie mit rueckfluss = 0 und < 0
    a.append(pruefe_fehler(fall(r, ein(r, "10000", "1000", "1000")),
                           None, "nie"))
    a.append(pruefe_fehler(fall(r, ein(r, "10000", "1000", "1000.01")),
                           None, "nie"))
    a.append(pruefe_fehler(fall(r, ein(r, "10000", "0", "0")), None, "nie"))
    a += grenzfaelle(r)
    a.append(fall(r, ein(r, "1000000000", "1000000000", "0")))
    a.append(fall(r, ein(r, "1000000000", "0.01", "0")))
    return a


PFLICHT = {"bezug": faelle_bezug, "verkauf": faelle_verkauf,
           "deckung": faelle_deckung, "afa": faelle_afa,
           "zinsen": faelle_zinsen, "amort": faelle_amort}


def selbsttest():
    # q rundet halbe Cent weg von 0, HALF_EVEN und float taeten das nicht
    assert q(Decimal("0.125")) == Decimal("0.13")
    assert q_even(Decimal("0.125")) == Decimal("0.12")
    assert round(2.675, 2) == 2.67 and q(Decimal("2.675")) == Decimal("2.68")


def main():
    if len(sys.argv) != 2:
        print("Aufruf: erzeuge_w4_wiso.py <ziel.json>", file=sys.stderr)
        return 2
    selbsttest()
    faelle = []
    for r in RECHNER:
        faelle += PFLICHT[r]()
        faelle += zufall(r)
    daten = {"quelle": "werkzeuge/vergleich/erzeuge_w4_wiso.py (G30, Plan "
                       "P3-15 Abschnitt 4.4 und Praezisierung G30)",
             "python": platform.python_version(),
             "faelle": faelle}
    with open(sys.argv[1], "w", encoding="utf-8", newline="\n") as d:
        json.dump(daten, d, ensure_ascii=False, indent=1)
        d.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
