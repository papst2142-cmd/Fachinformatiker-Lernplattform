#!/usr/bin/env python3
"""Welle A 1.18.0 (L0, Weisung P12-424, Nico 1b): Eintraege in inhalte_csharp/lernen wirklich loeschen und in die
Loeschliste inhalte_csharp/lernen/geloescht.json eintragen.

    python3 -I werkzeuge/inhalte/loeschliste.py l0 <repo>
    python3 -I werkzeuge/inhalte/loeschliste.py vorher <repo> <ordner mit den Dateien vor L0> <ziel.json>

vorher: schreibt die Testdaten "Stand vor L0" (je Datei die geloeschten Textstuecke mit ihrer Byte-Stelle in der alten Datei,
SHA-256 vorher und nachher, dazu ausschluss.json ganz). Daraus setzen die Tests die alte Datei bytegleich wieder zusammen
(Tests der Wellen davor rechnen weiter auf ihr) und pruefen, dass die heutige Datei genau die alte ohne diese Stuecke ist.

l0: einmalig fuer L0. Loescht alles, was ausschluss.json nennt (Karten, Quizfragen, Pruefungssaetze), dazu jede aeltere
Fassung eines Pruefungssatzes, deren neuere Fassung in der Datei steht. Geloescht wird im Text der Datei: genau der Text
des Eintrags samt Trennzeichen faellt weg, alle anderen Bytes bleiben (Probe: json.loads danach = vorher ohne die
Eintraege). Die geloeschten Eintraege kommen ans Ende von geloescht.json. ausschluss.json entfaellt danach (Nico 1b). Die Stelle in der Loeschliste ist die Stelle in der Datei
VOR diesem Loeschen; "hash16" ist der Eintrag der Tabelle Eingetragen in LerninhalteTests.cs an dieser Stelle (Listen) bzw.
die ersten 16 Zeichen von SHA-256 des Satzes als json.dumps(sort_keys=True, ensure_ascii=False, separators=(",", ":")).
"""
import hashlib
import json
import os
import re
import sys

DATUM = "2026-11-27"
WEISUNG = "P12-424"


def lesen(pfad):
    with open(pfad, encoding="utf-8") as f:
        return json.load(f)


def schreiben(pfad, daten):
    with open(pfad, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(daten, ensure_ascii=False, indent=1))


def spannen(text, start):
    """(Anfang, Ende) jedes Eintrags der JSON-Liste, deren "[" bei start steht."""
    dec = json.JSONDecoder()
    assert text[start] == "["
    i = start + 1
    ergebnis = []
    while True:
        while text[i] in " \t\r\n":
            i += 1
        if text[i] == "]":
            return ergebnis
        _, ende = dec.raw_decode(text, i)
        ergebnis.append((i, ende))
        i = ende
        while text[i] in " \t\r\n":
            i += 1
        if text[i] == ",":
            i += 1


def im_text_loeschen(pfad, stellen, schluessel=None):
    """Loescht die Eintraege an stellen (ab 0) aus der Liste der Datei (schluessel: Liste in einem Objekt) im Text."""
    with open(pfad, encoding="utf-8", newline="") as f:
        text = f.read()
    vorher = json.loads(text)
    start = text.index("[") if schluessel is None else text.index("[", text.index('"%s":' % schluessel))
    spann = spannen(text, start)
    for stelle in sorted(stellen, reverse=True):
        anfang, ende = spann[stelle]
        if stelle + 1 < len(spann):
            text = text[:anfang] + text[spann[stelle + 1][0]:]
        else:
            text = text[:spann[stelle - 1][1]] + text[ende:]
    nachher = json.loads(text)
    liste_vorher = vorher if schluessel is None else vorher[schluessel]
    soll = [e for i, e in enumerate(liste_vorher) if i not in stellen]
    assert (nachher if schluessel is None else nachher[schluessel]) == soll, pfad
    if schluessel is not None:
        assert {k: v for k, v in nachher.items() if k != schluessel} == {k: v for k, v in vorher.items() if k != schluessel}
    with open(pfad, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def eingetragen(repo):
    """Tabelle Eingetragen aus tests/Lernplattform.Tests/LerninhalteTests.cs: Datei -> Liste der hash16."""
    text = open(os.path.join(repo, "tests", "Lernplattform.Tests", "LerninhalteTests.cs"), encoding="utf-8").read()
    start = text.index("Eingetragen = new Dictionary<string, string[]>")
    ende = text.index("};", start)
    tabelle = {}
    for m in re.finditer(r'\["(\w+)"\]\s*=\s*\[(.*?)\]', text[start:ende], re.S):
        tabelle[m.group(1)] = re.findall(r'"([^"]+)"', m.group(2))
    return tabelle


def l0(repo):
    ordner = os.path.join(repo, "inhalte_csharp", "lernen")
    ausschluss = lesen(os.path.join(ordner, "ausschluss.json"))["eintraege"]
    tabelle = eingetragen(repo)
    geloescht_pfad = os.path.join(ordner, "geloescht.json")
    loeschliste = lesen(geloescht_pfad) if os.path.exists(geloescht_pfad) else {
        "_hinweis": [
            "Welle A 1.18.0 (L0, Weisung P12-424, Nico 1b): Loeschliste der Lerninhalte in inhalte_csharp/lernen. Eine neue "
            "Fassung ersetzt die alte sofort: die alte wird im selben Commit geloescht und hier eingetragen. Nur anhaengen.",
            "Je Eintrag: datei (ohne .json), stelle in der Datei VOR dem Loeschen (ab 0), q (Listen) bzw. kennung und fassung "
            "(Pruefungssaetze), hash16, grund, datum, weisung. Die Tests vergleichen Datei, Tabelle Eingetragen und diese Liste.",
        ],
        "eintraege": [],
    }
    neu = []
    for datei in ("karteikarten", "quizfragen"):
        pfad = os.path.join(ordner, datei + ".json")
        liste = lesen(pfad)
        weg = {}
        for a in ausschluss:
            if a["datei"] != datei:
                continue
            e = liste[a["stelle"]]
            assert e["q"] == a["q"], (datei, a["stelle"])
            weg[a["stelle"]] = a
        assert len(tabelle[datei]) == len(liste), datei
        for stelle in sorted(weg):
            a = weg[stelle]
            neu.append({"datei": datei, "stelle": stelle, "q": a["q"], "hash16": tabelle[datei][stelle],
                        "fachrichtung": liste[stelle].get("fachrichtung"),
                        "grund": "alte Fassung, seit " + a["grund"].split(":")[0] + " ausgeblendet (" + a["grund"] + ")",
                        "datum": DATUM, "weisung": WEISUNG})
        im_text_loeschen(pfad, set(weg))
    pfad = os.path.join(ordner, "pruefungssaetze.json")
    saetze_datei = lesen(pfad)
    saetze = saetze_datei["saetze"]
    weg = {}
    for a in ausschluss:
        if a["datei"] == "pruefungssaetze":
            assert saetze[a["stelle"]]["kennung"] == a["kennung"]
            weg[a["stelle"]] = "alte FIAE-Struktur, seit A 0.94.0 ausgeblendet (" + a["grund"] + ")"
    for i, s in enumerate(saetze):
        spaeter = [j for j in range(i + 1, len(saetze)) if saetze[j]["kennung"] == s["kennung"]]
        if spaeter and i not in weg:
            weg[i] = "Fassung %d, ersetzt durch Fassung %d (Stelle %d)" % (s.get("fassung", 1), saetze[spaeter[-1]].get("fassung", 1), spaeter[-1])
    for stelle in sorted(weg):
        s = saetze[stelle]
        roh = json.dumps(s, sort_keys=True, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        neu.append({"datei": "pruefungssaetze", "stelle": stelle, "kennung": s["kennung"], "fassung": s.get("fassung", 1),
                    "hash16": hashlib.sha256(roh).hexdigest()[:16], "fachrichtung": s.get("fachrichtung"),
                    "grund": weg[stelle], "datum": DATUM, "weisung": WEISUNG})
    im_text_loeschen(pfad, set(weg), "saetze")
    loeschliste["eintraege"].extend(neu)
    schreiben(geloescht_pfad, loeschliste)
    os.remove(os.path.join(ordner, "ausschluss.json"))
    from collections import Counter
    print("geloescht:", dict(Counter(e["datei"] for e in neu)), "gesamt", len(neu))


def vorher(repo, alt, ziel):
    ordner = os.path.join(repo, "inhalte_csharp", "lernen")
    ergebnis = {"_hinweis": "Welle A 1.18.0 (L0): Stand vor dem Loeschen, erzeugt von werkzeuge/inhalte/loeschliste.py vorher",
                "dateien": {}}
    for name in ("karteikarten", "quizfragen", "pruefungssaetze"):
        roh_alt = open(os.path.join(alt, name + ".json"), "rb").read()
        roh_neu = open(os.path.join(ordner, name + ".json"), "rb").read()
        # gemeinsame Teile suchen: die neue Datei ist die alte ohne Stuecke (der Reihe nach)
        stuecke = []
        i = j = 0
        text_alt = roh_alt.decode("utf-8")
        start = text_alt.index("[") if name != "pruefungssaetze" else text_alt.index("[", text_alt.index('"saetze":'))
        spann = spannen(text_alt, start)
        weg = [g["stelle"] for g in lesen(os.path.join(ordner, "geloescht.json"))["eintraege"] if g["datei"] == name]
        for stelle in sorted(weg):
            anfang, ende = spann[stelle]
            a, b = (anfang, spann[stelle + 1][0]) if stelle + 1 < len(spann) else (spann[stelle - 1][1], ende)
            stuecke.append((len(text_alt[:a].encode("utf-8")), text_alt[a:b]))
        # zusammenhaengende Stuecke zusammenlegen
        zusammen = []
        for off, t in stuecke:
            if zusammen and zusammen[-1][0] + len(zusammen[-1][1].encode("utf-8")) == off:
                zusammen[-1] = (zusammen[-1][0], zusammen[-1][1] + t)
            else:
                zusammen.append((off, t))
        # Probe: alte Datei ohne die Stuecke = neue Datei, neue plus Stuecke = alte
        rest = bytearray(roh_alt)
        for off, t in reversed(zusammen):
            del rest[off:off + len(t.encode("utf-8"))]
        assert bytes(rest) == roh_neu, name
        wieder = bytearray(roh_neu)
        for off, t in zusammen:
            wieder[off:off] = t.encode("utf-8")
        assert bytes(wieder) == roh_alt, name
        ergebnis["dateien"][name] = {"sha256_vorher": hashlib.sha256(roh_alt).hexdigest(),
                                     "sha256_nachher": hashlib.sha256(roh_neu).hexdigest(),
                                     "stuecke": [{"byte": off, "text": t} for off, t in zusammen]}
    ergebnis["ausschluss"] = open(os.path.join(alt, "ausschluss.json"), encoding="utf-8").read()
    with open(ziel, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(ergebnis, ensure_ascii=False, indent=1))
        f.write("\n")


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "l0":
        l0(sys.argv[2])
    elif len(sys.argv) == 5 and sys.argv[1] == "vorher":
        vorher(*sys.argv[2:])
    else:
        sys.exit(__doc__)
