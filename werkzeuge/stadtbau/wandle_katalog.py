"""Phase 13 Welle 3b (Weisungen P12-19, P12-23 1a 2a 3a, P12-24): voller CC0-Katalog des Stadtbau-Werkzeugs.

Wandelt ALLE Modelle aller Pakete aus Fi-Austausch anlagen/grafik_cc0/ (Kenney und KayKit, OBJ mit Farbatlas; die Figur
aus animated-characters-3 nur als FBX) mit denselben Lesern wie die Spielgrafik (werkzeuge/grafik/wandle_modelle.py:
obj_wandeln, fbx_figur) und schreibt je Paket eine eigene Datei des Werkzeugs:

  inhalte_stadtbau/grafik/<paket>.bin   Modelle des Pakets, Aufbau wie modelle.bin ("FLGM", Version 2, je Modell Name,
                                        Dreiecke mit 9 x float32, 4 Byte Farbe und 12 Byte Eckfarben, S 0.9.1)
  inhalte_stadtbau/grafik/<paket>.png   Vorschaubilder des Pakets, je Modell ein Feld 96 x 96 (16 je Zeile, gleiche
                                        Farben und Blickrichtung: schraeg von vorn rechts oben, flach beleuchtet)
  inhalte_stadtbau/grafik/katalog.json  Liste: Pakete (ZIP, SHA-256, Quelle, Lizenz, Pruefsummen der Dateien) und je Modell
                                        Name, Anzeige, Paket, Gruppe, Art, Dreiecke, Kasten, Vorgabegroesse innen/aussen,
                                        Feld im Vorschaubild, Suchwoerter (deutsch und englisch)

Partikel-Paket (nur 2D-Bilder, Weisung P12-23 3a): je Bild ein Eintrag der Art "effekt" (Gruppe Effekte, "Nur Aussehen"),
Vorschau aus dem Originalbild (Transparent), Hauptfarbe fuer das leuchtende Zeichen in 3D; keine .bin.

Gleiche Eingabe ergibt bytegleich dieselben Dateien (feste Reihenfolge, feste Rechenwege, zlib Stufe 9). Die Original-ZIPs
kommen nie ins Repo. Nur Python-Standardbibliothek; parallel je Paket (multiprocessing).
Ins Spiel kommt aus dem Katalog nur, was verbaut ist (ab Welle 4, angehaengt an modelle.bin).

Aufruf: python3 -I werkzeuge/stadtbau/wandle_katalog.py <ordner mit den ZIPs> [<ausgabeordner>]
"""

import hashlib
import json
import math
import multiprocessing
import os
import re
import struct
import sys
import zipfile
import zlib

HIER = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HIER))
sys.path.insert(0, os.path.join(REPO, "werkzeuge", "grafik"))
import wandle_modelle as wm  # noqa: E402  (gleiche Leser wie die Spielgrafik)

FELD = 96          # Vorschau je Modell in Bildpunkten
UEBER = 2          # Ueberabtastung (Kanten glatt)
SPALTEN = 16       # Felder je Zeile im Vorschaubild
INNEN_M = 0.55     # Meter je Einheit eines Innenraums (Bett 3,0 x 3,6 = 1,65 x 2 m)
AUSSEN_M = 4.0     # Meter je Einheit der Stadtkarte (Auto 4,5 m = gut eine Einheit)

# Gruppen (Art) in fester Reihenfolge; Effekte zuletzt
GRUPPEN = ["Möbel", "Küche und Restaurant", "Bäume und Natur", "Häuser und Bauteile", "Straßen und Schienen",
           "Fahrzeuge", "Technik und Freizeit", "Feste", "Tiere", "Figuren und Verkleidung", "Effekte"]

# Pakete: Schluessel = Dateiname im Repo; "m" = Meter je Modell-Einheit (Vorgabegroesse, aenderbar), "gruppe" = Vorgabe,
# "regeln" = (Muster auf den Namen, Gruppe) der Reihe nach, "drehen" wie in auswahl.json (Vorderseite nach +Z)
PAKETE = {
    "furniture": {"zip": "kenney_furniturePack.zip", "titel": "Kenney Furniture Kit", "m": 0.18, "gruppe": "Möbel", "drehen": 180,
                  "regeln": [(r"^kitchen", "Küche und Restaurant"), (r"^(plant|pottedPlant)", "Bäume und Natur"),
                             (r"^(computer|laptop|television|radio|speaker|washer|dryer)", "Technik und Freizeit"),
                             (r"^(doorway|wall|floor|stairs)", "Häuser und Bauteile"), (r"^bear$", "Tiere")]},
    "kaykit_moebel": {"zip": "KayKit_Furniture_Bits_1.0_FREE.zip", "titel": "KayKit Furniture Bits", "m": 0.5, "gruppe": "Möbel",
                      "regeln": [(r"^cactus", "Bäume und Natur")]},
    "kaykit_restaurant": {"zip": "KayKit_Restaurant_Bits_1.0_FREE.zip", "titel": "KayKit Restaurant Bits", "m": 0.5,
                          "gruppe": "Küche und Restaurant", "regeln": [(r"^(door|floor|wall)", "Häuser und Bauteile")]},
    "commercial": {"zip": "kenney_city-kit-commercial_2.1.zip", "titel": "Kenney City Kit Commercial", "m": 8.0,
                   "gruppe": "Häuser und Bauteile"},
    "suburban": {"zip": "kenney_cityKitSuburban.zip", "titel": "Kenney City Kit Suburban", "m": 8.0, "gruppe": "Häuser und Bauteile",
                 "regeln": [(r"^tree", "Bäume und Natur"), (r"^(driveway|path)", "Straßen und Schienen")]},
    "industrial": {"zip": "kenney_city-kit-industrial_2.0.zip", "titel": "Kenney City Kit Industrial", "m": 8.0,
                   "gruppe": "Häuser und Bauteile", "regeln": [(r"^(solar|windmill|water-tower)", "Technik und Freizeit")]},
    "modular": {"zip": "kenney_modular-buildings.zip", "titel": "Kenney Modular Buildings", "m": 4.0, "gruppe": "Häuser und Bauteile"},
    "building_kit": {"zip": "kenney_building-kit.zip", "titel": "Kenney Building Kit", "m": 3.0, "gruppe": "Häuser und Bauteile"},
    "kaykit_stadt": {"zip": "KayKit_City_Builder_Bits_1.0_FREE.zip", "titel": "KayKit City Builder Bits", "m": 4.0,
                     "gruppe": "Straßen und Schienen",
                     "regeln": [(r"^building", "Häuser und Bauteile"), (r"^car_", "Fahrzeuge"), (r"^(bush|tree)", "Bäume und Natur"),
                                (r"^(bench|box|dumpster|firehydrant|trash|watertower|streetlight|trafficlight)", "Straßen und Schienen")]},
    "roads": {"zip": "kenney_cityKitRoads_1.1.zip", "titel": "Kenney City Kit Roads", "m": 8.0, "gruppe": "Straßen und Schienen"},
    "train": {"zip": "kenney_train-kit.zip", "titel": "Kenney Train Kit", "m": 4.0, "gruppe": "Straßen und Schienen",
              "regeln": [(r"^(train|tram|wagon|locomotive)", "Fahrzeuge")]},
    "car_kit": {"zip": "kenney_car-kit.zip", "titel": "Kenney Car Kit", "m": 1.64, "gruppe": "Fahrzeuge",
                "regeln": [(r"^(cone|box)", "Straßen und Schienen")]},
    "conveyor": {"zip": "kenney_conveyor-kit.zip", "titel": "Kenney Conveyor Kit", "m": 1.0, "gruppe": "Technik und Freizeit"},
    "arcade": {"zip": "kenney_mini-arcade.zip", "titel": "Kenney Mini Arcade", "m": 1.0, "gruppe": "Technik und Freizeit",
               "regeln": [(r"^character", "Figuren und Verkleidung"), (r"^(floor|wall|column)", "Häuser und Bauteile")]},
    "kaykit_wald": {"zip": "KayKit_Forest_Nature_Pack_1.0_FREE.zip", "titel": "KayKit Forest Nature Pack", "m": 2.0,
                    "gruppe": "Bäume und Natur"},
    "kaykit_halloween": {"zip": "KayKit_HalloweenBits_1.0_FREE.zip", "titel": "KayKit Halloween Bits", "m": 0.5, "gruppe": "Feste"},
    "kaykit_holiday": {"zip": "KayKit_Holiday_Bits_1.0_FREE.zip", "titel": "KayKit Holiday Bits", "m": 0.5, "gruppe": "Feste"},
    "graveyard": {"zip": "kenney_graveyard-kit_5.0.zip", "titel": "Kenney Graveyard Kit", "m": 1.0, "gruppe": "Feste",
                  "regeln": [(r"^character", "Figuren und Verkleidung")]},
    "cube_pets": {"zip": "kenney_cube-pets.zip", "titel": "Kenney Cube Pets", "m": 0.3, "gruppe": "Tiere"},
    "kaykit_abenteurer": {"zip": "KayKit_Adventurers_2.0_FREE.zip", "titel": "KayKit Adventurers", "m": 0.5,
                          "gruppe": "Figuren und Verkleidung"},
    "kaykit_skelette": {"zip": "KayKit_Skeletons_1.1_FREE.zip", "titel": "KayKit Skeletons", "m": 0.5, "gruppe": "Figuren und Verkleidung"},
    "characters": {"zip": "kenney_animated-characters-3.zip", "titel": "Kenney Animated Characters 3", "m": 0.48,
                   "gruppe": "Figuren und Verkleidung", "fbx": "Model/characterMedium.fbx", "arme": 72},
    "particles": {"zip": "kenney_particle-pack.zip", "titel": "Kenney Particle Pack", "m": 1.0, "gruppe": "Effekte",
                  "bilder": "PNG (Transparent)/"},
    # Welle S 0.29.0 (Weisungen P12-124, P12-125, P12-130 Punkt 4a): drei weitere Pakete am Ende, die vorhandenen bleiben
    # bytegleich (gleiche Reihenfolge, gleiche Feldnummern)
    "factory": {"zip": "kenney_factory-kit_3.0.zip", "titel": "Kenney Factory Kit", "m": 1.25, "gruppe": "Technik und Freizeit",
                "regeln": [(r"^(structure|door|floor|top|catwalk)", "Häuser und Bauteile"),
                           (r"^(cone|warning|indicator|arrow)", "Straßen und Schienen")]},
    "nature": {"zip": "kenney_nature-kit_2.1.zip", "titel": "Kenney Nature Kit", "m": 3.5, "gruppe": "Bäume und Natur",
               "regeln": [(r"^(bridge|path_|ground_path)", "Straßen und Schienen"), (r"^(fence|statue)", "Häuser und Bauteile"),
                          (r"^(bed|tent|campfire|canoe|sign$|pot_)", "Technik und Freizeit")]},
    "survival": {"zip": "kenney_survival-kit_2.0.zip", "titel": "Kenney Survival Kit", "m": 3.0, "gruppe": "Technik und Freizeit",
                 "regeln": [(r"^(tree|rock|grass|patch)", "Bäume und Natur"), (r"^fish", "Tiere"),
                            (r"^(structure|fence|floor|metal-panel)", "Häuser und Bauteile"), (r"^signpost", "Straßen und Schienen")]},
}

# englische Wortteile der Namen -> deutsche Suchwoerter (Suche nach Namen, Weisung P12-19 2)
DEUTSCH = {
    "chair": "Stuhl", "armchair": "Sessel", "stool": "Hocker", "table": "Tisch", "desk": "Schreibtisch", "bed": "Bett",
    "sofa": "Sofa", "couch": "Sofa Couch", "lounge": "Sofa Lounge", "bench": "Bank", "shelf": "Regal", "bookcase": "Regal Buecherregal",
    "cabinet": "Schrank", "kitchen": "Kueche", "fridge": "Kuehlschrank", "stove": "Herd", "oven": "Ofen", "sink": "Spuele Waschbecken",
    "bathroom": "Bad", "bathtub": "Badewanne", "shower": "Dusche", "toilet": "Toilette WC", "lamp": "Lampe", "light": "Licht Lampe",
    "plant": "Pflanze", "potted": "Topf", "rug": "Teppich", "carpet": "Teppich", "pillow": "Kissen", "book": "Buch", "books": "Buecher",
    "television": "Fernseher", "computer": "Computer PC", "screen": "Bildschirm", "keyboard": "Tastatur", "mouse": "Maus",
    "laptop": "Laptop", "radio": "Radio", "speaker": "Lautsprecher", "washer": "Waschmaschine", "dryer": "Trockner",
    "door": "Tuer", "doorway": "Tuer", "window": "Fenster", "wall": "Wand", "floor": "Boden", "roof": "Dach", "stairs": "Treppe",
    "column": "Saeule", "pillar": "Saeule Pfeiler", "fence": "Zaun", "gate": "Tor", "path": "Weg", "road": "Strasse",
    "street": "Strasse", "bridge": "Bruecke", "railroad": "Gleis Schiene", "rail": "Gleis Schiene", "track": "Gleis", "train": "Zug",
    "tram": "Strassenbahn", "wagon": "Wagen", "locomotive": "Lok", "car": "Auto", "sedan": "Auto Limousine", "taxi": "Taxi",
    "police": "Polizei", "ambulance": "Krankenwagen", "firetruck": "Feuerwehr", "truck": "Lastwagen LKW", "van": "Transporter",
    "delivery": "Lieferwagen", "garbage": "Muell Muellwagen", "tractor": "Traktor", "bus": "Bus", "wheel": "Rad Reifen",
    "tree": "Baum", "bush": "Busch Strauch", "grass": "Gras", "rock": "Stein Fels", "stone": "Stein", "flower": "Blume",
    "mushroom": "Pilz", "log": "Baumstamm", "stump": "Baumstumpf", "cactus": "Kaktus", "pine": "Tanne Kiefer", "dead": "kahl tot",
    "building": "Haus Gebaeude", "house": "Haus", "skyscraper": "Hochhaus", "tower": "Turm", "chimney": "Schornstein",
    "container": "Container", "tank": "Tank", "solar": "Solar", "windmill": "Windrad Windmuehle", "awning": "Markise",
    "parasol": "Sonnenschirm", "conveyor": "Foerderband", "robot": "Roboter", "scanner": "Scanner", "box": "Kiste Karton",
    "crate": "Kiste", "arcade": "Spielautomat", "pinball": "Flipper", "vending": "Automat", "machine": "Maschine Automat",
    "register": "Kasse", "pumpkin": "Kuerbis", "candle": "Kerze", "coffin": "Sarg", "grave": "Grab", "gravestone": "Grabstein",
    "skull": "Schaedel", "bone": "Knochen", "lantern": "Laterne", "crypt": "Gruft", "ghost": "Geist", "zombie": "Zombie",
    "skeleton": "Skelett", "vampire": "Vampir", "christmas": "Weihnachten", "present": "Geschenk", "gingerbread": "Lebkuchen",
    "candycane": "Zuckerstange", "mistletoe": "Mistelzweig", "snowman": "Schneemann", "bell": "Glocke", "cookie": "Keks",
    "animal": "Tier", "cat": "Katze", "dog": "Hund", "cow": "Kuh", "pig": "Schwein", "bunny": "Hase", "chick": "Kueken",
    "fish": "Fisch", "bee": "Biene", "fox": "Fuchs", "deer": "Reh Hirsch", "lion": "Loewe", "tiger": "Tiger", "elephant": "Elefant",
    "monkey": "Affe", "penguin": "Pinguin", "panda": "Panda", "parrot": "Papagei", "crab": "Krabbe", "bear": "Baer",
    "sword": "Schwert", "shield": "Schild", "axe": "Axt", "bow": "Bogen", "arrow": "Pfeil", "staff": "Stab", "wand": "Zauberstab",
    "mug": "Becher Krug", "plate": "Teller", "bowl": "Schuessel", "pot": "Topf", "pan": "Pfanne", "food": "Essen",
    "burger": "Burger", "pizza": "Pizza", "bread": "Brot", "cheese": "Kaese", "tomato": "Tomate", "carrot": "Karotte Moehre",
    "spark": "Funken", "smoke": "Rauch", "fire": "Feuer Flamme", "flame": "Flamme Feuer", "flare": "Licht Blitz", "magic": "Zauber",
    "star": "Stern", "circle": "Kreis", "dirt": "Staub Erde", "muzzle": "Muendungsfeuer", "scorch": "Brandfleck", "slash": "Hieb",
    "symbol": "Zeichen", "trace": "Spur", "twirl": "Wirbel", "window_": "Fenster", "barrier": "Absperrung", "construction": "Baustelle",
    "pylon": "Leitkegel Pylon", "cone": "Leitkegel Kegel", "sign": "Schild", "hydrant": "Hydrant", "dumpster": "Container Muell",
    "trash": "Muell Muelleimer", "bin": "Eimer", "character": "Figur Person", "employee": "Mitarbeiter", "gamer": "Spieler",
    "coat": "Mantel Garderobe", "rack": "Staender Garderobe", "fan": "Ventilator", "mirror": "Spiegel", "picture": "Bild",
    "pictureframe": "Bild Rahmen", "clock": "Uhr", "trophy": "Pokal", "hay": "Heu", "altar": "Altar", "cross": "Kreuz",
    # Welle S 0.29.0: Woerter der Pakete Factory, Nature, Survival
    "cog": "Zahnrad", "crane": "Kran", "hopper": "Trichter", "pipe": "Rohr Leitung", "piston": "Kolben", "catwalk": "Laufsteg",
    "lever": "Hebel", "button": "Knopf Taster", "cliff": "Klippe Fels", "crop": "Feld Ernte", "crops": "Feld Acker", "tent": "Zelt",
    "canoe": "Kanu Boot", "campfire": "Lagerfeuer", "barrel": "Fass Tonne", "bucket": "Eimer", "tool": "Werkzeug",
    "hammer": "Hammer", "shovel": "Schaufel", "pickaxe": "Spitzhacke", "hoe": "Hacke", "workbench": "Werkbank", "anvil": "Amboss",
    "signpost": "Wegweiser", "chest": "Truhe", "bottle": "Flasche", "statue": "Statue Denkmal", "moss": "Moos", "lily": "Seerose",
    "palm": "Palme", "wheat": "Weizen Getreide", "corn": "Mais", "bamboo": "Bambus", "river": "Fluss Bach", "waterfall": "Wasserfall",
    "cave": "Hoehle", "ground": "Boden Gelaende", "platform": "Plattform", "bedroll": "Schlafsack", "planks": "Bretter",
    "resource": "Vorrat", "melon": "Melone", "turnip": "Ruebe", "oak": "Eiche", "stack": "Stapel", "magnet": "Magnet",
}


def _hex(farbe):
    return "#%02X%02X%02X" % tuple(farbe[:3])


def woerter(name):
    """Name in Wortteile: chairDesk -> chair desk, tree_large -> tree large, building-a -> building a."""
    s = re.sub(r"([a-z])([A-Z])", r"\1 \2", name)
    return [w.lower() for w in re.split(r"[\s_\-.]+", s) if w]


def anzeige(name):
    """Lesbarer Name fuer die Anzeige (englisch wie im Paket, Wortteile mit Leerzeichen, erster Buchstabe gross)."""
    w = " ".join(woerter(name))
    return w[:1].upper() + w[1:]


def suchtext(name, gruppe, titel):
    teile = woerter(name)
    deutsch = [DEUTSCH[w] for w in teile if w in DEUTSCH]
    return " ".join([name.lower()] + teile + [d.lower() for d in deutsch] + [gruppe.lower(), titel.lower()])


def gruppe_fuer(paket, name):
    for muster, gruppe in paket.get("regeln", []):
        if re.search(muster, name):
            return gruppe
    return paket["gruppe"]


# ---- Vorschau (Software-Zeichnung, fest) --------------------------------------------------------------------------

_DREH = math.radians(35.0)   # um die Hochachse: Blick von vorn rechts
_NEIG = math.radians(28.0)   # von oben
_LICHT = (0.35, 0.75, 0.56)


def _ansicht(p):
    x, y, z = p
    c, s = math.cos(_DREH), math.sin(_DREH)
    x, z = c * x + s * z, -s * x + c * z
    c2, s2 = math.cos(_NEIG), math.sin(_NEIG)
    y, z = c2 * y - s2 * z, s2 * y + c2 * z
    return x, y, z   # z zum Betrachter


def vorschau(dreiecke):
    """Dreiecke ((p1, p2, p3), farbe[, eckfarben]) -> FELD x FELD Bildpunkte RGBA (bytes); je Dreieck seine Farbe (die
    Vorschaubilder bleiben wie vor dem Farbverlauf)."""
    g = FELD * UEBER
    if not dreiecke:
        return bytes(FELD * FELD * 4)
    ansicht = [tuple(_ansicht(p) for p in d[0]) for d in dreiecke]
    xs = [p[0] for e in ansicht for p in e]
    ys = [p[1] for e in ansicht for p in e]
    breite = max(max(xs) - min(xs), max(ys) - min(ys), 1e-6)
    mass = g * 0.88 / breite
    mx, my = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
    tiefe = [-1e30] * (g * g)
    bild = [None] * (g * g)
    lx, ly, lz = _LICHT
    ll = math.sqrt(lx * lx + ly * ly + lz * lz)
    lx, ly, lz = lx / ll, ly / ll, lz / ll
    for (a, b, c), (_, farbe, *_) in zip(ansicht, dreiecke):
        ux, uy, uz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
        vx, vy, vz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
        nx, ny, nz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
        nl = math.sqrt(nx * nx + ny * ny + nz * nz)
        if nl < 1e-12:
            continue
        nx, ny, nz = nx / nl, ny / nl, nz / nl
        if nz < 0:
            nx, ny, nz = -nx, -ny, -nz
        hell = 0.5 + 0.5 * max(0.0, nx * lx + ny * ly + nz * lz)
        rgb = (min(255, int(farbe[0] * hell + 0.5)), min(255, int(farbe[1] * hell + 0.5)), min(255, int(farbe[2] * hell + 0.5)))
        pts = [((p[0] - mx) * mass + g / 2, g / 2 - (p[1] - my) * mass, p[2]) for p in (a, b, c)]
        (x0, y0, z0), (x1, y1, z1), (x2, y2, z2) = pts
        flaeche = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
        if abs(flaeche) < 1e-12:
            continue
        xa = max(0, int(math.floor(min(x0, x1, x2))))
        xb = min(g - 1, int(math.ceil(max(x0, x1, x2))))
        ya = max(0, int(math.floor(min(y0, y1, y2))))
        yb = min(g - 1, int(math.ceil(max(y0, y1, y2))))
        inv = 1.0 / flaeche
        for py in range(ya, yb + 1):
            cy = py + 0.5
            zeile = py * g
            for px in range(xa, xb + 1):
                cx = px + 0.5
                w0 = ((x1 - cx) * (y2 - cy) - (x2 - cx) * (y1 - cy)) * inv
                w1 = ((x2 - cx) * (y0 - cy) - (x0 - cx) * (y2 - cy)) * inv
                w2 = 1.0 - w0 - w1
                if w0 < 0 or w1 < 0 or w2 < 0:
                    continue
                z = w0 * z0 + w1 * z1 + w2 * z2
                i = zeile + px
                if z > tiefe[i]:
                    tiefe[i] = z
                    bild[i] = rgb
    aus = bytearray(FELD * FELD * 4)
    for y in range(FELD):
        for x in range(FELD):
            r = gg = b = n = 0
            for dy in range(UEBER):
                for dx in range(UEBER):
                    p = bild[(y * UEBER + dy) * g + x * UEBER + dx]
                    if p is not None:
                        r += p[0]
                        gg += p[1]
                        b += p[2]
                        n += 1
            if n:
                o = (y * FELD + x) * 4
                aus[o:o + 4] = bytes((r // n, gg // n, b // n, 255 * n // (UEBER * UEBER)))
    return bytes(aus)


def png_rgba_lesen(daten):
    """PNG (8 Bit, RGB/RGBA/Grau/Grau+Alpha/Palette, ohne Zeilensprung) -> (breite, hoehe, zeilen RGBA)."""
    pos, idat, palette, trns = 8, b"", None, None
    breite = hoehe = tiefe = art = 0
    while pos < len(daten):
        laenge, typ = struct.unpack(">I4s", daten[pos:pos + 8])
        inhalt = daten[pos + 8:pos + 8 + laenge]
        pos += 12 + laenge
        if typ == b"IHDR":
            breite, hoehe, tiefe, art, _, _, sprung = struct.unpack(">IIBBBBB", inhalt)
            if tiefe != 8 or sprung != 0:
                raise ValueError("PNG-Art nicht unterstuetzt")
        elif typ == b"PLTE":
            palette = [tuple(inhalt[i:i + 3]) for i in range(0, len(inhalt), 3)]
        elif typ == b"tRNS":
            trns = inhalt
        elif typ == b"IDAT":
            idat += inhalt
        elif typ == b"IEND":
            break
    k = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[art]
    roh = zlib.decompress(idat)
    zl = breite * k
    vorher, p, zeilen = bytearray(zl), 0, []
    for _ in range(hoehe):
        f = roh[p]
        z = bytearray(roh[p + 1:p + 1 + zl])
        p += 1 + zl
        for i in range(zl):
            a = z[i - k] if i >= k else 0
            b = vorher[i]
            c = vorher[i - k] if i >= k else 0
            if f == 1:
                z[i] = (z[i] + a) & 255
            elif f == 2:
                z[i] = (z[i] + b) & 255
            elif f == 3:
                z[i] = (z[i] + ((a + b) >> 1)) & 255
            elif f == 4:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                z[i] = (z[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        vorher = z
        out = bytearray(breite * 4)
        for x in range(breite):
            if art == 6:
                out[x * 4:x * 4 + 4] = z[x * 4:x * 4 + 4]
            elif art == 2:
                out[x * 4:x * 4 + 4] = bytes(z[x * 3:x * 3 + 3]) + b"\xff"
            elif art == 4:
                out[x * 4:x * 4 + 4] = bytes((z[x * 2],) * 3) + bytes((z[x * 2 + 1],))
            elif art == 0:
                out[x * 4:x * 4 + 4] = bytes((z[x],) * 3) + b"\xff"
            else:
                i = z[x]
                al = trns[i] if trns is not None and i < len(trns) else 255
                out[x * 4:x * 4 + 4] = bytes(palette[i]) + bytes((al,))
        zeilen.append(bytes(out))
    return breite, hoehe, zeilen


def bild_verkleinern(breite, hoehe, zeilen):
    """Bild auf FELD x FELD (Mittelwert je Block, mit Alpha gewichtet) und Hauptfarbe (mit Alpha gewichteter Mittelwert)."""
    aus = bytearray(FELD * FELD * 4)
    sr = sg = sb = sa = 0
    for y in range(FELD):
        y0, y1 = y * hoehe // FELD, max(y * hoehe // FELD + 1, (y + 1) * hoehe // FELD)
        for x in range(FELD):
            x0, x1 = x * breite // FELD, max(x * breite // FELD + 1, (x + 1) * breite // FELD)
            r = g = b = a = n = 0
            for yy in range(y0, y1):
                z = zeilen[yy]
                for xx in range(x0, x1):
                    o = xx * 4
                    al = z[o + 3]
                    r += z[o] * al
                    g += z[o + 1] * al
                    b += z[o + 2] * al
                    a += al
                    n += 1
            o = (y * FELD + x) * 4
            if a:
                aus[o:o + 4] = bytes((r // a, g // a, b // a, a // n))
            sr, sg, sb, sa = sr + r, sg + g, sb + b, sa + a
    haupt = (sr // sa, sg // sa, sb // sa) if sa else (255, 255, 255)
    return bytes(aus), haupt


def png_schreiben(breite, hoehe, rgba):
    def stueck(typ, inhalt):
        return struct.pack(">I", len(inhalt)) + typ + inhalt + struct.pack(">I", zlib.crc32(typ + inhalt) & 0xFFFFFFFF)
    roh = b"".join(b"\x00" + rgba[y * breite * 4:(y + 1) * breite * 4] for y in range(hoehe))
    return (b"\x89PNG\r\n\x1a\n" + stueck(b"IHDR", struct.pack(">IIBBBBB", breite, hoehe, 8, 6, 0, 0, 0))
            + stueck(b"IDAT", zlib.compress(roh, 9)) + stueck(b"IEND", b""))


def atlas(felder):
    zeilen = max(1, (len(felder) + SPALTEN - 1) // SPALTEN)
    breite, hoehe = SPALTEN * FELD, zeilen * FELD
    bild = bytearray(breite * hoehe * 4)
    for n, f in enumerate(felder):
        fx, fy = (n % SPALTEN) * FELD, (n // SPALTEN) * FELD
        for y in range(FELD):
            o = ((fy + y) * breite + fx) * 4
            bild[o:o + FELD * 4] = f[y * FELD * 4:(y + 1) * FELD * 4]
    return png_schreiben(breite, hoehe, bytes(bild))


# ---- je Paket -----------------------------------------------------------------------------------------------------

def paket_wandeln(arg):
    schluessel, quelle = arg
    p = PAKETE[schluessel]
    pfad = os.path.join(quelle, p["zip"])
    with open(pfad, "rb") as f:
        zip_sha = hashlib.sha256(f.read()).hexdigest()
    z = zipfile.ZipFile(pfad)
    eintraege, felder, binteile = [], [], []
    if "bilder" in p:
        namen = sorted(n for n in z.namelist() if n.startswith(p["bilder"]) and n.lower().endswith(".png"))
        for n in namen:
            name = os.path.basename(n)[:-4]
            feld, haupt = bild_verkleinern(*png_rgba_lesen(z.read(n)))
            felder.append(feld)
            eintraege.append({"name": name, "datei": n, "art": "effekt", "farbe": _hex(haupt)})
    else:
        modelle = []
        if "fbx" in p:
            for haut in sorted(n for n in z.namelist() if n.startswith("Skins/") and n.endswith(".png")):
                modelle.append((os.path.basename(haut)[:-4], p["fbx"], wm.fbx_figur(z, p["fbx"], haut, p["arme"])))
        else:
            for n in sorted(x for x in z.namelist() if x.lower().endswith(".obj")):
                modelle.append((os.path.basename(n)[:-4], n, wm.gedreht(wm.obj_wandeln(z, n), p.get("drehen", 0))))
        doppelt = {m[0] for m in modelle if sum(1 for x in modelle if x[0] == m[0]) > 1}
        if doppelt:
            raise ValueError("%s: doppelte Namen %s" % (schluessel, sorted(doppelt)))
        for name, datei, dreiecke in modelle:
            felder.append(vorschau(dreiecke))
            kodiert = name.encode("utf-8")
            teil = [struct.pack("<H", len(kodiert)) + kodiert + struct.pack("<I", len(dreiecke))]
            for dreieck in dreiecke:
                teil.append(wm.dreieck_bytes(dreieck))
            binteile.append(b"".join(teil))
            alle = [q for d in dreiecke for q in d[0]] or [(0.0, 0.0, 0.0)]
            mn = [min(q[i] for q in alle) for i in range(3)]
            mx = [max(q[i] for q in alle) for i in range(3)]
            eintraege.append({"name": name, "datei": datei, "art": "modell", "dreiecke": len(dreiecke),
                              "min": [round(v, 4) for v in mn], "max": [round(v, 4) for v in mx]})
    z.close()
    dateien = {}
    if binteile:
        daten = b"FLGM" + struct.pack("<II", wm.VERSION, len(binteile)) + b"".join(binteile)
        dateien[schluessel + ".bin"] = daten
    dateien[schluessel + ".png"] = atlas(felder)
    return schluessel, zip_sha, eintraege, dateien


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    quelle = sys.argv[1]
    aus = sys.argv[2] if len(sys.argv) > 2 else os.path.join(REPO, "inhalte_stadtbau", "grafik")
    os.makedirs(aus, exist_ok=True)
    with multiprocessing.Pool(min(4, os.cpu_count() or 1)) as pool:
        ergebnisse = pool.map(paket_wandeln, [(k, quelle) for k in PAKETE], chunksize=1)
    pakete, modelle = {}, []
    for schluessel, zip_sha, eintraege, dateien in ergebnisse:
        p = PAKETE[schluessel]
        for name, daten in dateien.items():
            with open(os.path.join(aus, name), "wb") as f:
                f.write(daten)
        pakete[schluessel] = {"titel": p["titel"], "zip": p["zip"], "sha256_zip": zip_sha, "lizenz": "CC0 1.0",
                              "dateien": {n: hashlib.sha256(d).hexdigest() for n, d in sorted(dateien.items())}}
        for nr, e in enumerate(eintraege):
            gruppe = gruppe_fuer(p, e["name"])
            if gruppe not in GRUPPEN:
                raise ValueError("unbekannte Gruppe " + gruppe)
            eintrag = {"id": schluessel + "/" + e["name"], "name": e["name"], "anzeige": anzeige(e["name"]), "paket": schluessel,
                       "gruppe": gruppe, "art": e["art"], "feld": nr}
            if e["art"] == "modell":
                g = [e["max"][i] - e["min"][i] for i in range(3)]
                eintrag.update({"dreiecke": e["dreiecke"], "min": e["min"], "max": e["max"],
                                "innen": [round(max(0.1, g[0] * p["m"] / INNEN_M), 1), round(max(0.1, g[2] * p["m"] / INNEN_M), 1),
                                          round(max(0.05, g[1] * p["m"] / INNEN_M), 2)],
                                "aussen": [round(max(0.05, g[0] * p["m"] / AUSSEN_M), 2), round(max(0.05, g[2] * p["m"] / AUSSEN_M), 2),
                                           round(max(0.02, g[1] * p["m"] / AUSSEN_M), 2)]})
            else:
                eintrag.update({"dreiecke": 2, "farbe": e["farbe"], "innen": [1.0, 1.0, 1.0], "aussen": [0.3, 0.3, 0.3]})
            eintrag["suche"] = suchtext(e["name"], gruppe, p["titel"])
            modelle.append(eintrag)
    index = {
        "_hinweis": "Erzeugt von werkzeuge/stadtbau/wandle_katalog.py aus den CC0-Paketen (Fi-Austausch anlagen/grafik_cc0). "
                    "Nicht von Hand aendern. innen/aussen = Vorgabe Breite, Tiefe, Hoehe in Einheiten von Innenraum bzw. Stadtkarte.",
        "format": 1, "feld": FELD, "spalten": SPALTEN, "gruppen": GRUPPEN, "pakete": pakete, "modelle": modelle,
    }
    with open(os.path.join(aus, "katalog.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(index, f, ensure_ascii=False, indent=1)
        f.write("\n")
    gesamt = sum(os.path.getsize(os.path.join(aus, n)) for n in os.listdir(aus))
    print("Katalog: %d Eintraege aus %d Paketen, %d Bytes" % (len(modelle), len(pakete), gesamt))
    return 0


if __name__ == "__main__":
    sys.exit(main())
