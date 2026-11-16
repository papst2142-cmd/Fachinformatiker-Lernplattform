"""Welle Nachladen A 0.82.0 / S 0.54.0 (Weisung P12-280, Nico 2b): grosse Testkarte 256 x 256 aus Stadt 1.

Stadt 1 (Fassung 2, 64 x 64) wird 4 x 4 Mal nebeneinander gelegt. Die Kopie links oben ist Stadt 1 ganz (mit Orten,
Zugaengen, Haltestellen, Gelaende). Die anderen 15 Kopien bekommen Strassen, Parks, Bahn, Fluss, Baeume, Teiche, Stege,
Fusswege, Zebrastreifen, Laternen und Beete, verschoben; jeder Ort wird dort ein Gebaeude ohne Ort mit demselben Modell und
derselben Drehung (gleich viele Haeuser je Kachel wie in Stadt 1). Gelaende (hoechstens 16 Huegel) und Haltestellen bleiben
nur in der ersten Kopie. Kopf: Stadt "neu_testkarte" ohne Basis (wie eine neue Stadt des Werkzeugs, oeffnet dort als eigenes Projekt), Fassung 0,
Format 3 (Karte groesser als 64).

Seit A 0.83.0 / S 0.55.0 (Weisung P12-282): jede der 16 Kopien ist ein Stadtteil mit Namen (stadtdaten.stadtteile, nur
Anzeige: Karte im Spiel-Handy, Schild beim Betreten), damit Spiel, Werkzeug und Start-Test die Namen an einer echten
grossen Karte zeigen.

Aufruf: python3 -I werkzeuge/stadtbau/testkarte.py <stadt1_fassung2.fistadt> <ziel.fistadt>
"""
import copy
import hashlib
import json
import sys

N = 4
SEITE = 64.0

# Namen der 16 Stadtteile, Spalte fuer Spalte (i = Spalte von links, j = Reihe von oben)
NAMEN = [
    ["Altstadt", "Lindenhof", "Am Mühlbach", "Südhang"],
    ["Nordviertel", "Gartenstadt", "Am Bahndamm", "Talgrund"],
    ["Hafenviertel", "Neustadt", "Sonnenfeld", "Westend"],
    ["Ostend", "Birkenau", "Am Weiher", "Bergfeld"],
]


def verschoben(punkte, dx, dy):
    return [[round(p[0] + dx, 4), round(p[1] + dy, 4)] + list(p[2:]) for p in punkte]


def main(quelle, ziel):
    with open(quelle, encoding="utf-8") as f:
        d = json.load(f)
    karte = d["karte"]
    sd = d["stadtdaten"]
    landschaft = karte["welt"]["landschaft"]
    dreh = sd["darstellung"].get("hausdreh", {})
    grad = sd["darstellung"].get("hausgrad", {})
    neu_land = list(landschaft)
    fuss = list(sd.get("fusswege", []))
    teiche = list(sd["wasser"].get("teiche", []))
    stege = list(sd["wasser"].get("stege", []))
    zebras = list(sd.get("zebrastreifen", []))
    laternen = list(d.get("laternen", []))
    beete = list(d.get("beete", []))
    for i in range(N):
        for j in range(N):
            if i == 0 and j == 0:
                continue
            dx, dy = i * SEITE, j * SEITE
            for t in landschaft:
                t2 = copy.deepcopy(t)
                t2.pop("bahnhof", None)
                if "punkte" in t2:
                    t2["punkte"] = verschoben(t2["punkte"], dx, dy)
                else:
                    t2["x"] = round(t2["x"] + dx, 4)
                    t2["y"] = round(t2["y"] + dy, 4)
                neu_land.append(t2)
            for o in karte["orte"]:
                g = {"typ": "gebaeude", "modell": o["modell"], "x": round(o["x"] + dx, 4), "y": round(o["y"] + dy, 4)}
                if o["id"] in dreh:
                    g["dreh"] = dreh[o["id"]]
                if o["id"] in grad:
                    g["grad"] = grad[o["id"]]
                neu_land.append(g)
            for w in sd.get("fusswege", []):
                w2 = copy.deepcopy(w)
                w2["punkte"] = verschoben(w2["punkte"], dx, dy)
                fuss.append(w2)
            for t in sd["wasser"].get("teiche", []):
                t2 = copy.deepcopy(t)
                t2["punkte"] = verschoben(t2["punkte"], dx, dy)
                teiche.append(t2)
            for s in sd["wasser"].get("stege", []):
                s2 = copy.deepcopy(s)
                s2["x"] = round(s2["x"] + dx, 4)
                s2["y"] = round(s2["y"] + dy, 4)
                stege.append(s2)
            for z in sd.get("zebrastreifen", []):
                z2 = copy.deepcopy(z)
                z2["x"] = round(z2["x"] + dx, 4)
                z2["y"] = round(z2["y"] + dy, 4)
                zebras.append(z2)
            laternen += verschoben(d.get("laternen", []), dx, dy)
            beete += verschoben(d.get("beete", []), dx, dy)
    karte["welt"]["breite"] = SEITE * N
    karte["welt"]["hoehe"] = SEITE * N
    karte["welt"]["landschaft"] = neu_land
    sd["fusswege"] = fuss
    sd["wasser"]["teiche"] = teiche
    sd["wasser"]["stege"] = stege
    sd["zebrastreifen"] = zebras
    sd["stadtteile"] = [
        {"name": NAMEN[i][j], "x1": i * SEITE, "x2": (i + 1) * SEITE, "z1": j * SEITE, "z2": (j + 1) * SEITE}
        for j in range(N) for i in range(N)
    ]
    d["laternen"] = laternen
    d["beete"] = beete
    d["formatversion"] = 3
    d["stadt"] = {"id": "neu_testkarte", "fassung": 0, "basis": None}
    d["nachricht"] = "Testkarte 256 x 256 aus Stadt 1 (Fassung 2), 4 x 4 Kopien (werkzeuge/stadtbau/testkarte.py)."
    d.pop("pruefsumme", None)
    text = json.dumps(d, ensure_ascii=False, separators=(",", ":"), sort_keys=True)
    d["pruefsumme"] = "sha256:" + hashlib.sha256(text.encode("utf-8")).hexdigest()
    with open(ziel, "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False, indent=1)
        f.write("\n")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
