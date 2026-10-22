"""Erzeugt die Vergleichswerte G36 (Fachrichtungen, Klausur-Bewertung,
Gesamtergebnis und Ergaenzung) als tests/Vergleich/fachrichtungen.json.

Das Skript ist unabhaengig entstanden, nur aus der Spezifikation
Fi-Austausch/anlagen/P4-5_Spezifikation_G32_G36.md (Stand 22.10.2026),
ohne Code der Python-Linie oder der C#-Linie zu lesen. Es ist der zweite
Beleg neben dem Python-Erzeuger von G32 (Abschnitt 6 nur SI).

Aufruf aus dem Repo-Wurzelordner (Python 3.12, nur Standardbibliothek):
    python3 -I werkzeuge/vergleich/erzeuge_fachrichtungen.py inhalte tests/Vergleich/fachrichtungen.json
"""

import itertools
import json
import math
import os
import random
import re
import sys
from decimal import ROUND_FLOOR, Decimal

ROLLEN = ["ap1", "projekt", "b1", "b2", "wiso"]
TEIL2 = ["projekt", "b1", "b2", "wiso"]
ERGAENZBAR = ["b1", "b2", "wiso"]

# ---------------------------------------------------------------- 1. Konfiguration

FR_DATEN = [
    ("si", "Systemintegration",
     "Planen und Umsetzen eines Projektes der Systemintegration", 40, "ap2",
     ("konzeption", "Konzeption", "AP2 · Konzeption und Administration von IT-Systemen"),
     ("netzwerke", "Netzwerke", "AP2 · Analyse und Entwicklung von Netzwerken")),
    ("ae", "Anwendungsentwicklung",
     "Planen und Umsetzen eines Softwareprojektes", 80, "keine",
     ("softwareprodukt", "Softwareprodukt", "AP2 · Planen eines Softwareproduktes"),
     ("algorithmen", "Algorithmen", "AP2 · Entwicklung und Umsetzung von Algorithmen")),
    ("dpa", "Daten- und Prozessanalyse",
     "Planen und Durchführen eines Projektes der Datenanalyse", 40, "keine",
     ("prozessanalyse", "Prozessanalyse", "AP2 · Durchführen einer Prozessanalyse"),
     ("datenqualitaet", "Datenqualität", "AP2 · Sicherstellen der Datenqualität")),
    ("dv", "Digitale Vernetzung",
     "Planen und Umsetzen eines Projektes der digitalen Vernetzung", 40, "keine",
     ("diagnose", "Diagnose", "AP2 · Diagnose und Störungsbeseitigung in vernetzten Systemen"),
     ("betrieb", "Betrieb", "AP2 · Betrieb und Erweiterung von vernetzten Systemen")),
]


def bereich(rolle, art, kurz, name, minuten, gewicht, ergaenzung, quelle):
    return {"rolle": rolle, "art": art, "kurz": kurz, "name": name,
            "minuten": minuten, "gewicht": gewicht,
            "ergaenzung": ergaenzung, "quelle": quelle}


def konfiguration():
    erg = {}
    for kenn, name, projekt, stunden, quelle_b, b1, b2 in FR_DATEN:
        bereiche = [
            bereich("ap1", "ap1", "AP1 (Teil 1)",
                    "AP1 · Einrichten eines IT-gestützten Arbeitsplatzes",
                    90, 20, False, "ap1"),
            bereich("projekt", "projekt", "Projekt", projekt, 0, 50, False, "keine"),
            bereich("b1", b1[0], b1[1], b1[2], 90, 10, True, quelle_b),
            bereich("b2", b2[0], b2[1], b2[2], 90, 10, True, quelle_b),
            bereich("wiso", "wiso", "WiSo", "AP2 · Wirtschafts- und Sozialkunde",
                    60, 10, True, "quiz"),
        ]
        assert [b["rolle"] for b in bereiche] == ROLLEN
        assert sum(b["gewicht"] for b in bereiche) == 100
        assert sum(b["gewicht"] for b in bereiche if b["rolle"] in TEIL2) == 80
        assert [b["rolle"] for b in bereiche if b["ergaenzung"]] == ERGAENZBAR
        erg[kenn] = {"name": name, "projekt": projekt,
                     "projekt_stunden": stunden, "bereiche": bereiche}
    return erg


# ---------------------------------------------------------------- 2. Bewertung

def teilpunkte(anzahl, gesamt=25):
    basis, rest = divmod(gesamt, anzahl)
    return [basis + (1 if i < rest else 0) for i in range(anzahl)]


def vorschlag(maximum, haken):
    if not haken:
        return 0
    k = sum(1 for h in haken if h)
    n = len(haken)
    return int(round(maximum * k / n))


def wiso_punkte(richtig, anzahl):
    return round(100.0 * richtig / max(1, anzahl), 1)


def ap_teil(teilmax, wert):
    return max(0, min(teilmax, int(wert)))


def note(p):
    if p >= 92:
        return "1 (Sehr gut)"
    if p >= 81:
        return "2 (Gut)"
    if p >= 67:
        return "3 (Befriedigend)"
    if p >= 50:
        return "4 (Ausreichend)"
    if p >= 30:
        return "5 (Mangelhaft)"
    return "6 (Ungenügend)"


def schwach(themen):
    paare = []
    for thema, (erreicht, moeglich) in themen.items():
        anteil = erreicht / moeglich if moeglich else 0
        paare.append((anteil, thema))
    return [thema for anteil, thema in sorted(paare) if anteil < 0.5]


SCHWACH_FAELLE = [
    {},
    {"A": [0, 0]},
    {"A": [1, 2]},
    {"A": [49, 100]},
    {"B": [1, 4], "A": [1, 4]},
    {"Üben": [0, 10], "Zeta": [0, 10]},
    {"a": [1, 10], "B": [1, 10]},
    {"X": [3, 10], "Y": [1, 10], "Z": [6, 10]},
    {"A": [25, 25], "B": [0, 25]},
    {"A": [0, 0], "B": [0, 5]},
    {"Recht & Verträge": [2, 5], "Arbeitswelt": [3, 5]},
    {"T1": [12, 25], "T2": [13, 25], "T3": [12.5, 25]},
]


# ---------------------------------------------------------------- 3. Zeit

def rest(minuten, vergangen):
    return max(0, min(minuten * 60, int(minuten * 60 - vergangen)))


def dauer(minuten, vergangen):
    return min(minuten * 60, int(vergangen))


def zeittext(s):
    if s >= 3600:
        return "%d:%02d:%02d" % (s // 3600, s % 3600 // 60, s % 60)
    return "%02d:%02d" % (s // 60, s % 60)


# ---------------------------------------------------------------- 4. Anzeige

def abrunden(w):
    return math.floor(round(w, 9) * 10) / 10


def punkte_text(w):
    if not math.isfinite(w):
        return "-"
    d = Decimal(repr(round(w, 9))).quantize(Decimal("0.1"), rounding=ROUND_FLOOR)
    if d == 0:
        d = d.copy_abs()
    text = str(d).replace(".", ",")
    if text.endswith(",0"):
        text = text[:-2]
    return text


def punkte_eintrag(w):
    endlich = math.isfinite(w)
    return {"wert": repr(w),
            "abgerundet": abrunden(w) if endlich else None,
            "text": punkte_text(w),
            "note": note(w) if endlich else None}


PUNKTE_LISTE = [0.0, -0.0, -1e-12, 1e-10, 0.05, 0.15, 12.25, 29.95, 29.99,
                33.333333333333336, 49.9, 49.95, 49.97, 49.99, 49.99999999999999,
                50.0, 50.05, 66.95, 80.95, 91.95, 99.99, 100.0,
                float("inf"), float("-inf"), float("nan")]


# ---------------------------------------------------------------- 5. Gesamtergebnis

def gewichte(fr):
    return {b["rolle"]: b["gewicht"] for b in fr["bereiche"]}


def kurznamen(fr):
    return {b["rolle"]: b["kurz"] for b in fr["bereiche"]}


def regeln(werte, gew):
    gesamt = sum(werte[r] * gew[r] for r in ROLLEN) / 100.0
    teil2 = sum(werte[r] * gew[r] for r in TEIL2) / sum(gew[r] for r in TEIL2)
    a = sum(1 for r in TEIL2 if werte[r] >= 50)
    liste = [
        ["Gesamtergebnis Teil 1 und 2 mindestens ausreichend (50)", gesamt >= 50],
        ["Ergebnis Teil 2 mindestens ausreichend (50)", teil2 >= 50],
        ["Mindestens drei Bereiche von Teil 2 ausreichend (%d von 4)" % a, a >= 3],
        ["Kein Bereich von Teil 2 ungenügend (unter 30)",
         all(werte[r] >= 30 for r in TEIL2)],
    ]
    return gesamt, teil2, a, liste


def alle_erfuellt(werte, gew):
    return all(ok for _, ok in regeln(werte, gew)[3])


def ergebnis(werte, fr):
    gew = gewichte(fr)
    kurz = kurznamen(fr)
    fehlend = [kurz[r] for r in ROLLEN if r not in werte]
    if fehlend:
        return {"vollstaendig": False, "fehlend": fehlend}
    gesamt, teil2, a, liste = regeln(werte, gew)
    bestanden = all(ok for _, ok in liste)
    paare = []
    if not bestanden:
        for r in ERGAENZBAR:
            w = werte[r]
            if w < 50:
                best = dict(werte)
                best[r] = (2 * w + 100) / 3.0
                if alle_erfuellt(best, gew):
                    for oral in range(0, 101):
                        probe = dict(werte)
                        probe[r] = (2 * w + oral) / 3.0
                        if alle_erfuellt(probe, gew):
                            paare.append((oral, kurz[r]))
                            break
    if bestanden:
        art, rolle, oral, text = 0, None, None, ""
    elif not paare:
        art, rolle, oral = 1, None, None
        text = ("Eine mündliche Ergänzungsprüfung würde hier nicht reichen "
                "(nur in %s, %s oder WiSo möglich, Gewichtung 2:1)."
                % (kurz["b1"], kurz["b2"]))
    else:
        oral, k = min(paare)
        rolle = [r for r in ERGAENZBAR if kurz[r] == k][0]
        n = "1 Punkt" if oral == 1 else "%d Punkte" % oral
        art = 2
        text = ("Eine mündliche Ergänzungsprüfung in %s könnte reichen: "
                "Dafür wären dort mindestens %s nötig (alt zu mündlich 2:1)."
                % (k, n))
    return {"vollstaendig": True, "gesamt": gesamt, "teil2": teil2,
            "gesamt_ab": abrunden(gesamt), "teil2_ab": abrunden(teil2),
            "note": note(gesamt), "bestanden": bestanden, "regeln": liste,
            "a": a, "ergaenzung": text, "art": art, "rolle": rolle, "oral": oral}


def gesamt_faelle():
    werte_raster = [0.0, 29.99, 30.0, 49.95, 50.0, 100.0]
    faelle = [list(k) for k in itertools.product(werte_raster, repeat=5)]
    rng = random.Random("g36")
    for _ in range(300):
        faelle.append([rng.randint(0, 10000) / 100 for _ in ROLLEN])
    return faelle


# ---------------------------------------------------------------- 6. Kriterien

ABKUERZUNGEN = ["z.B.", "z. B.", "d.h.", "d. h.", "ca.", "bzw.", "u.a.", "usw.",
                "ggf.", "inkl.", "Nr.", "evtl.", "vgl.", "bspw.", "max.", "min.",
                "o.ä.", "u.U.", "Abs.", "Std.", "Min.", "Mio.", "Tsd.", "etc.",
                "z.T."]
NUMMER = re.compile(r"^\s*(\d+)\.\s*")
SPIEGELSTRICH = re.compile(r"^[-•*]+(?=\s|$)")
ZB = re.compile(r"(?<![^\W\d_])Z\.( ?)B\.")
ZIFFERPUNKT = re.compile(r"(\d)\.(\d)")
SATZ = re.compile(r"(?<=[.!?])\s+(?=[A-ZÄÖÜ0-9„\"(])")


def aufteilen(zeilen):
    einleitung = []
    teile = []
    for zeile in zeilen.split("\n"):
        if NUMMER.match(zeile):
            teile.append([NUMMER.sub("", zeile, count=1)])
        elif teile:
            teile[-1].append(zeile)
        else:
            einleitung.append(zeile)
    return "\n".join(einleitung).strip(), ["\n".join(t).strip() for t in teile]


def kriterien(text):
    erg = []
    for zeile in text.split("\n"):
        z = zeile.strip()
        z = SPIEGELSTRICH.sub("", z)
        z = z.strip()
        if not z:
            continue
        z = ZB.sub("Z\x02\\1B\x02", z)
        for i, abk in enumerate(ABKUERZUNGEN):
            z = z.replace(abk, "\x00%d\x00" % i)
        z = ZIFFERPUNKT.sub("\\1\x01\\2", z)
        for stueck in SATZ.split(z):
            for i, abk in enumerate(ABKUERZUNGEN):
                stueck = stueck.replace("\x00%d\x00" % i, abk)
            stueck = stueck.replace("\x01", ".").replace("\x02", ".").strip()
            if len(stueck) > 2:
                erg.append(stueck)
    if not erg:
        return [text.strip()]
    return erg


def als_text(wert):
    if isinstance(wert, list):
        return "\n".join(wert)
    return wert


def kriterien_raster(inhalte):
    erg = []
    for quelle in ("ap1", "ap2"):
        pfad = os.path.join(inhalte, "szenarien_%s.json" % quelle)
        with open(pfad, encoding="utf-8") as f:
            szenarien = json.load(f)
        for index, sz in enumerate(szenarien):
            einleitung, teile = aufteilen(als_text(sz["text"]))
            _, loesungen = aufteilen(als_text(sz["solution"]))
            brauchbar = bool(teile) and len(teile) == len(loesungen)
            liste = []
            if brauchbar:
                punkte = teilpunkte(len(teile))
                for teil, p, loesung in zip(teile, punkte, loesungen):
                    liste.append([teil, p, loesung, kriterien(loesung)])
            erg.append([quelle, index, brauchbar, einleitung, liste])
    return erg


# ---------------------------------------------------------------- Ausgabe

def erzeugen(inhalte):
    frs = konfiguration()
    daten = {"fachrichtungen": frs}

    daten["teilpunkte"] = [[n, teilpunkte(n)] for n in range(1, 13)]

    vs = [[m, 0, 0, vorschlag(m, [])] for m in range(1, 26)]
    for m in range(1, 26):
        for n in range(1, 13):
            for k in range(0, n + 1):
                haken = [True] * k + [False] * (n - k)
                vs.append([m, n, k, vorschlag(m, haken)])
    daten["vorschlag"] = vs

    wiso = []
    for anzahl in range(0, 31):
        for richtig in range(0, anzahl + 1):
            wiso.append([richtig, anzahl, wiso_punkte(richtig, anzahl)])
    daten["wiso"] = wiso

    daten["ap"] = [[t, w, ap_teil(t, w)]
                   for t in (6, 8, 9, 13, 25)
                   for w in (-3, -0.5, 0, 0.9, 1, 7.99, 8, 8.5, 9, 30)]

    werte = [k / 10 for k in range(0, 1001)] + PUNKTE_LISTE + [e[2] for e in wiso]
    daten["punkte"] = [punkte_eintrag(w) for w in werte]

    daten["schwach"] = [[t, schwach(t)] for t in SCHWACH_FAELLE]

    start_utc = 1700000000
    zeit_rest = []
    for minuten in (60, 90):
        for d in (-5, 0, 1, 59.9, 60, 3599, 3600, 5399.5, 5400, 5401, 10000):
            jetzt = start_utc + d
            vergangen = max(0.0, jetzt - start_utc)
            zeit_rest.append([minuten, d, rest(minuten, vergangen),
                              dauer(minuten, vergangen)])
    daten["zeit"] = {
        "start_utc": start_utc,
        "rest": zeit_rest,
        "text": [[s, zeittext(s)] for s in (0, 1, 59, 60, 299, 300, 3599, 3600, 3661, 5400)],
    }

    faelle = gesamt_faelle()
    standard = frs["si"]
    gesamt = []
    for w in faelle:
        e = ergebnis(dict(zip(ROLLEN, w)), standard)
        gesamt.append([w, e["gesamt_ab"], e["teil2_ab"], e["note"], e["bestanden"],
                       [ok for _, ok in e["regeln"]], e["a"], e["art"],
                       e["rolle"], e["oral"]])
    daten["gesamt"] = gesamt

    art1 = [g[0] for g in gesamt if g[7] == 1][:40]
    art2 = [g[0] for g in gesamt if g[7] == 2][:40]
    texte = {}
    for kenn, fr in frs.items():
        liste = []
        for w in art1 + art2:
            e = ergebnis(dict(zip(ROLLEN, w)), fr)
            liste.append([w, [t for t, _ in e["regeln"]], e["ergaenzung"]])
        texte[kenn] = liste
    daten["texte"] = texte

    fehlend = {}
    for kenn, fr in frs.items():
        liste = []
        for weg in ROLLEN:
            vorhanden = {r: 50.0 for r in ROLLEN if r != weg}
            liste.append([vorhanden, ergebnis(vorhanden, fr)["fehlend"]])
        liste.append([{}, ergebnis({}, fr)["fehlend"]])
        fehlend[kenn] = liste
    daten["fehlend"] = fehlend

    daten["kriterien"] = kriterien_raster(inhalte)
    return daten


def main():
    if len(sys.argv) != 3:
        print("Aufruf: erzeuge_fachrichtungen.py <inhalte-ordner> <ausgabe.json>")
        return 2
    daten = erzeugen(sys.argv[1])
    text = json.dumps(daten, ensure_ascii=False, separators=(",", ":"))
    with open(sys.argv[2], "w", encoding="utf-8", newline="\n") as f:
        f.write(text + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
