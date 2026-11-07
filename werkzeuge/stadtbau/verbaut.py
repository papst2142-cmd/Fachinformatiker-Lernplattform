"""Phase 13 Welle 4a (Weisung P12-28): nur Verbautes kommt ins Spiel.

Liest alle Fassungen der Staedte (inhalte_csharp/spiel/stadtbau/*.fistadt) und schreibt genau die Stuecke aus dem vollen
Katalog des Stadtbau-Werkzeugs (inhalte_stadtbau/grafik/), auf die eine Fassung verweist, in eine eigene Datei der
Spielgrafik:

  inhalte_csharp/grafik/katalog_verbaut.bin   Aufbau wie modelle.bin ("FLGM", Version 2), je Modell der Name
                                              "cc0:paket/name", die Dreiecke Byte fuer Byte aus <paket>.bin
  inhalte_csharp/grafik/katalog_verbaut.json  SHA-256 der .bin, Liste der Modelle (sortiert), Quelle je Paket

Verweise: innen jede Einrichtung mit "typ" = "cc0:paket/name" (in allen Abschnitten der Innenraeume, auch Zusaetze),
draussen jeder Eintrag von stadtdaten.katalog ("id" = "paket/name"). Ein Effekt (Paket "particles") ist ein Stern aus zwei
Dreiecken in seiner Farbe (gleich Katalogstueck.Stern im Spiel). Ein Verweis, den der Katalog nicht kennt, bricht ab.

Ablauf bytegleich wiederholbar (sortiert, keine Zeitangaben). Aufruf:
  python3 -I werkzeuge/stadtbau/verbaut.py <repo> [schreiben|pruefen] [<ordner der fassungen> <zielordner>]
pruefen (Vorgabe) vergleicht die Dateien im Repo mit dem neu Erzeugten und gibt 1 zurueck, wenn etwas abweicht. Die beiden
letzten Angaben (relativ zum Repo) nur fuer die Testdatei des Werkzeugs (tests/Stadtbau.Tests/Testdaten).
"""

import hashlib
import json
import os
import struct
import sys

PRAEFIX = "cc0:"
EFFEKT_PAKET = "particles"
FASSUNGEN = "inhalte_csharp/spiel/stadtbau"
KATALOG = "inhalte_stadtbau/grafik"
ZIEL = "inhalte_csharp/grafik"

# Stern: Spitze nach Norden, Spitze nach Sueden (gleich Katalogstueck.Stern)
STERN = [
    (0.5, 0.0, 0.0, 0.0669873, 0.0, 0.75, 0.9330127, 0.0, 0.75),
    (0.5, 0.0, 1.0, 0.9330127, 0.0, 0.25, 0.0669873, 0.0, 0.25),
]
VERSION = 2  # Aufbau der FLGM-Dateien (2 = mit Eckfarben, Weisung P12-50), gleich wandle_modelle.VERSION


def verweise_innen(knoten, menge):
    if isinstance(knoten, dict):
        typ = knoten.get("typ")
        if isinstance(typ, str) and typ.startswith(PRAEFIX):
            menge.add(typ[len(PRAEFIX):])
        for wert in knoten.values():
            verweise_innen(wert, menge)
    elif isinstance(knoten, list):
        for wert in knoten:
            verweise_innen(wert, menge)


def verweise(repo, fassungen=FASSUNGEN):
    menge = set()
    ordner = os.path.join(repo, fassungen)
    for name in sorted(os.listdir(ordner)):
        if not name.endswith(".fistadt"):
            continue
        with open(os.path.join(ordner, name), encoding="utf-8") as f:
            fassung = json.load(f)
        verweise_innen(fassung.get("innenraeume"), menge)
        for eintrag in (fassung.get("stadtdaten") or {}).get("katalog") or []:
            menge.add(eintrag["id"])
    return sorted(menge)


def paket_lesen(daten):
    """Name -> Bytes der Dreiecke (ab der Zahl der Dreiecke) aus einer FLGM-Datei."""
    if daten[:4] != b"FLGM" or struct.unpack("<I", daten[4:8])[0] != VERSION:
        raise SystemExit("unbekannter Aufbau einer Paket-Datei")
    anzahl = struct.unpack("<I", daten[8:12])[0]
    pos = 12
    modelle = {}
    for _ in range(anzahl):
        laenge = struct.unpack("<H", daten[pos:pos + 2])[0]
        name = daten[pos + 2:pos + 2 + laenge].decode("utf-8")
        pos += 2 + laenge
        dreiecke = struct.unpack("<I", daten[pos:pos + 4])[0]
        ende = pos + 4 + dreiecke * 52  # Version 2: 9 x float32, Farbe, drei Eckfarben
        modelle[name] = daten[pos:ende]
        pos = ende
    return modelle


def stern(farbe):
    h = farbe.lstrip("#")
    r, g, b = int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)
    teile = [struct.pack("<I", len(STERN))]
    for d in STERN:
        teile.append(struct.pack("<9f", *d) + bytes((r, g, b, 0)) * 4)  # Farbe und drei gleiche Eckfarben
    return b"".join(teile)


def erzeugen(repo, fassungen=FASSUNGEN):
    with open(os.path.join(repo, KATALOG, "katalog.json"), encoding="utf-8") as f:
        katalog = json.load(f)
    eintraege = {m["id"]: m for m in katalog["modelle"]}
    pakete = {}
    namen = verweise(repo, fassungen)
    binteile = []
    quellen = {}
    for kennung in namen:
        e = eintraege.get(kennung)
        if e is None:
            raise SystemExit("unbekanntes Katalog-Stueck: " + kennung)
        paket = e["paket"]
        if paket == EFFEKT_PAKET:
            if e["art"] != "effekt":
                raise SystemExit("Paket particles enthaelt ein Modell: " + kennung)
            dreiecke = stern(e["farbe"])
        else:
            if e["art"] != "modell":
                raise SystemExit("Effekt ausserhalb des Pakets particles: " + kennung)
            if paket not in pakete:
                datei = paket + ".bin"
                with open(os.path.join(repo, KATALOG, datei), "rb") as f:
                    daten = f.read()
                soll = katalog["pakete"][paket]["dateien"][datei]
                if hashlib.sha256(daten).hexdigest() != soll:
                    raise SystemExit("Pruefsumme stimmt nicht: " + datei)
                pakete[paket] = paket_lesen(daten)
            dreiecke = pakete[paket][e["name"]]
        quellen.setdefault(paket, katalog["pakete"][paket]["titel"])
        kodiert = (PRAEFIX + kennung).encode("utf-8")
        binteile.append(struct.pack("<H", len(kodiert)) + kodiert + dreiecke)
    daten = b"FLGM" + struct.pack("<II", VERSION, len(binteile)) + b"".join(binteile)
    index = {
        "_hinweis": ["Phase 13 Welle 4a (Weisung P12-28): nur die Stuecke aus dem Katalog des Stadtbau-Werkzeugs, auf die eine "
                     "Fassung in inhalte_csharp/spiel/stadtbau/ verweist; erzeugt mit werkzeuge/stadtbau/verbaut.py, nie von Hand."],
        "sha256": hashlib.sha256(daten).hexdigest(),
        "modelle": [PRAEFIX + n for n in namen],
        "pakete": dict(sorted(quellen.items())),
    }
    text = json.dumps(index, ensure_ascii=False, indent=1, sort_keys=False) + "\n"
    return daten, text.encode("utf-8")


def main():
    repo = sys.argv[1] if len(sys.argv) > 1 else "."
    art = sys.argv[2] if len(sys.argv) > 2 else "pruefen"
    fassungen = sys.argv[3] if len(sys.argv) > 4 else FASSUNGEN
    ziel = sys.argv[4] if len(sys.argv) > 4 else ZIEL
    daten, index = erzeugen(repo, fassungen)
    ziele = [(os.path.join(repo, ziel, "katalog_verbaut.bin"), daten), (os.path.join(repo, ziel, "katalog_verbaut.json"), index)]
    if art == "schreiben":
        for pfad, inhalt in ziele:
            with open(pfad, "wb") as f:
                f.write(inhalt)
        print("geschrieben:", len(json.loads(index)["modelle"]), "Stuecke,", len(daten), "Byte")
        return 0
    falsch = 0
    for pfad, inhalt in ziele:
        try:
            with open(pfad, "rb") as f:
                ist = f.read()
        except FileNotFoundError:
            ist = None
        if ist != inhalt:
            print("FEHLER: " + os.path.relpath(pfad, repo) + " passt nicht zu den Fassungen (neu erzeugen: verbaut.py <repo> schreiben)")
            falsch = 1
    if not falsch:
        print("katalog_verbaut stimmt:", len(json.loads(index)["modelle"]), "Stuecke")
    return falsch


if __name__ == "__main__":
    sys.exit(main())
