#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G6b (eigene Farben, ab Python 0.57), gerechnet
vom echten Python-Code der Python-Linie (fisi_theme):

  1. Texte: alle CUSTOM_*-Texte, Teile (CUSTOM_PARTS), Regler-Kanaele
     (CUSTOM_CHANNELS), Kontrastziele, Fachbereichs-Mindestabstand.
  2. Startwerte (custom_start) fuer alle 72 Farbwelten und values_palette am
     Startwert (= Farbwelt) sowie einen Schritt daneben (= Ableitungsregel).
  3. Paletten fuer viele Reglerwerte (custom_palette): Randwerte, sehr hell,
     sehr dunkel, Grautoene, kontrastarme Akzente, Hintergrund an der
     Kippgrenze helle/dunkle Schrift, zufaellige Werte (fester Seed). Dazu
     custom_checks, custom_problems, custom_warning (ohne Karte und bei jedem
     vierten Fall mit den Kartenfarben aus fisi_game.map_palette).
  4. valid_custom fuer gueltige und ungueltige gespeicherte Werte (als
     JSON-Text wie in einstellungen.json; NaN/Infinity als Marker
     {"__zahl__": "NaN"}, weil sie kein gueltiges JSON sind).
  5. Zustand der Regler (custom_state, custom_state_text), custom_to_save,
     tile_sets_sliders, is_start.
  6. save_custom (Wert "eigene_farben" in einstellungen.json danach) und
     saved_custom fuer verschiedene bisherige Werte.
  7. Regler-Beschriftungen (slider_label, slider_text).
  8. hsl_to_hex/hex_to_hsl und color_distance (auch fuer zufaellige Farben).

Es werden nur Testdaten benutzt; alle Daten- und Einstellungsordner zeigen in
einen leeren Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_eigene_farben.py <pfad_fisi_lernplattform> tests/Vergleich/eigene_farben.json
"""
import json
import math
import os
import random
import sys
import tempfile

TEILE = ("akzent1", "akzent2", "hintergrund")


def marker(value):
    """NaN/Infinity (kein gueltiges JSON) als {"__zahl__": ...} schreiben."""
    if isinstance(value, float) and not math.isfinite(value):
        return {"__zahl__": "NaN" if value != value else ("Infinity" if value > 0 else "-Infinity")}
    if isinstance(value, dict):
        return {k: marker(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return [marker(v) for v in value]
    return value


def werte_json(values):
    return None if values is None else {k: list(values[k]) for k in TEILE}


def palette_json(p):
    return {"hell": p["light"],
            "farben": dict(sorted(p["colors"].items())),
            "verlaeufe": {k: list(v) for k, v in sorted(p["gradients"].items())}}


def pruef_json(items):
    return [[name, wert, ziel] for name, wert, ziel in items]


# Gespeicherte Werte als Text (wie in einstellungen.json)
GUELTIG = '{"akzent1": [10, 20, 30], "akzent2": [0, 0, 0], "hintergrund": [359, 100, 100]}'
PRUEF_TEXTE = [
    GUELTIG,
    '{"akzent1": [0, 0, 0], "akzent2": [359, 100, 100], "hintergrund": [180, 50, 50], "neu": 1}',
    '{"hintergrund": [1, 2, 3], "akzent2": [4, 5, 6], "akzent1": [7, 8, 9]}',
    '{"akzent1": [10, 20, 30], "akzent2": [0, 0, 0]}',
    '{"akzent1": [10, 20, 30], "akzent2": [0, 0, 0], "hintergrund": null}',
    '{"akzent1": [10, 20], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [10, 20, 30, 40], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": {"h": 1, "s": 2, "l": 3}, "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": "10,20,30", "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [true, 20, 30], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [10, false, 30], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": ["10", 20, 30], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [null, 20, 30], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [[1], 20, 30], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [NaN, 20, 30], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [10, 20, 30], "akzent2": [0, 0, Infinity], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [10, 20, 30], "akzent2": [0, 0, 0], "hintergrund": [-Infinity, 2, 3]}',
    '{"akzent1": [-1, 20, 30], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [-0.4, 20, 30], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [-0.0, -0, 0.0], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [360, 20, 30], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [359, 101, 30], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [359, 100, 101], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [359, 100, 100.00000000000001], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [359.4, 99.5, 100.0], "akzent2": [0.5, 1.5, 2.5], "hintergrund": [3.5, 4.5, 50.5]}',
    '{"akzent1": [358.4, 99.5, 100.0], "akzent2": [0.5, 1.5, 2.5], "hintergrund": [3.5, 4.5, 50.5]}',
    '{"akzent1": [359.5, 20, 30], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [358.5, 0.49999999999999994, 2.5000000000000004], "akzent2": [1e2, 1E1, 5e-1], "hintergrund": [0.51, 1.49, 99.50000000000001]}',
    '{"akzent1": [100000000000000000000000, 20, 30], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [1e400, 20, 30], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '{"akzent1": [12.000, 20.999, 30.001], "akzent2": [0, 0, 0], "hintergrund": [1, 2, 3]}',
    '[[10, 20, 30], [0, 0, 0], [1, 2, 3]]',
    '"eigene"',
    'null',
    '5',
    'true',
    '{}',
]

# Bisherige Werte "eigene_farben" fuer save_custom/saved_custom (None = Schluessel fehlt)
FEHLT = "__fehlt__"
GUELTIG_W = {"akzent1": [10, 20, 30], "akzent2": [0, 0, 0], "hintergrund": [359, 100, 100]}
BISHER = [
    FEHLT,
    {},
    {"dunkel": GUELTIG_W},
    {"hell": GUELTIG_W},
    {"dunkel": GUELTIG_W, "hell": {"akzent1": [1, 2, 3], "akzent2": [4, 5, 6],
                                   "hintergrund": [7, 8, 90]}},
    {"hell": GUELTIG_W, "zukunft": {"a": 1, "b": [1, 2]}, "dunkel": "kaputt"},
    {"zukunft": "x"},
    {"dunkel": {"akzent1": [10, 20, 30]}, "hell": {"akzent1": [400, 0, 0], "akzent2": [0, 0, 0],
                                                   "hintergrund": [0, 0, 0]}},
    {"hell": {"akzent1": [1.5, 2.5, 3.5], "akzent2": [0, 0, 0], "hintergrund": [0, 0, 0]}},
    "kaputt",
    [1, 2, 3],
    None,
]
NEUE_WERTE = [
    None,
    {"akzent1": (200, 80, 40), "akzent2": (20, 90, 55), "hintergrund": (220, 30, 12)},
    {"akzent1": (400, 80, 40), "akzent2": (20, 90, 55), "hintergrund": (220, 30, 12)},
]


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    sys.path.insert(0, repo)
    import fisi_game as game
    import fisi_theme as t
    import fisi_update as update

    t.custom_colors.clear()

    # ------------------------------------------------------------ 1. Texte
    texte = {name: getattr(t, name) for name in sorted(dir(t))
             if name.startswith("CUSTOM_") and isinstance(getattr(t, name), str)}
    kanaele = [list(c) for c in t.CUSTOM_CHANNELS]

    # ------------------------------------------------------------ 2. Startwerte
    start = []
    for modus in t.MODE_IDS:
        for grund in t.PRESET_IDS:
            for hinter in t.BACKGROUND_IDS:
                welt = (grund, hinter)
                werte = t.custom_start(modus, welt)
                daneben = dict(werte)
                h, s, l = werte["hintergrund"]
                daneben["hintergrund"] = (h, s, l + 1 if l < 100 else l - 1)
                start.append({
                    "darstellung": modus, "grundfarbe": grund, "hintergrund": hinter,
                    "start": werte_json(werte),
                    "ist_start": t.is_start(werte, modus, welt),
                    "palette": palette_json(t.values_palette(werte, modus, welt)),
                    "daneben": werte_json(daneben),
                    "daneben_ist_start": t.is_start(daneben, modus, welt),
                    "daneben_palette": palette_json(t.values_palette(daneben, modus, welt)),
                })
    # Unbekannte Kennungen fallen auf den Standard
    unbekannt = []
    for modus, grund, hinter in (("dunkel", "gibtsnicht", "gibtsnicht"),
                                 ("hell", "rot_pink", "gibtsnicht"),
                                 ("irgendwas", "gibtsnicht", "schwarz")):
        unbekannt.append({"darstellung": modus, "grundfarbe": grund, "hintergrund": hinter,
                          "start": werte_json(t.custom_start(modus, (grund, hinter)))})

    # ------------------------------------------------------------ 3. Paletten
    akzente = [((0, 0, 0), (359, 100, 100)), ((60, 100, 50), (0, 0, 100)),
               ((120, 100, 90), (240, 100, 20)), ((263, 83, 58), (330, 81, 60)),
               ((180, 0, 50), (90, 100, 50)), ((0, 100, 50), (300, 100, 75)),
               ((45, 100, 95), (200, 10, 5))]
    gruende = [(0, 0, 0), (359, 100, 100), (0, 0, 100), (359, 0, 0), (0, 100, 0),
               (180, 100, 50), (60, 100, 50), (240, 100, 50), (0, 100, 100),
               (0, 0, 40), (0, 0, 45), (0, 0, 50), (0, 0, 55), (0, 0, 60),
               (270, 50, 3), (270, 50, 9), (270, 50, 97), (120, 20, 99), (30, 100, 98)]
    # Kippgrenze helle/dunkle Schrift: erstes L mit dunkler Schrift und eins davor
    for hue in (0, 60, 120, 180, 240, 300):
        for sat in (0, 50, 100):
            for lum in range(101):
                p = t.custom_palette({"akzent1": (0, 0, 0), "akzent2": (0, 0, 0),
                                      "hintergrund": (hue, sat, lum)})
                if p["light"]:
                    gruende += [(hue, sat, lum - 1), (hue, sat, lum)]
                    break
    faelle_werte = []
    for index, grund in enumerate(gruende):
        a1, a2 = akzente[index % len(akzente)]
        faelle_werte.append({"akzent1": a1, "akzent2": a2, "hintergrund": grund})
    zufall = random.Random(20261020)
    for _ in range(60):
        faelle_werte.append({part: (zufall.randint(0, 359), zufall.randint(0, 100),
                                    zufall.randint(0, 100)) for part in TEILE})
    paletten = []
    for index, werte in enumerate(faelle_werte):
        p = t.custom_palette(werte)
        fall = {"werte": werte_json(werte), "palette": palette_json(p),
                "pruefungen": pruef_json(t.custom_checks(p)),
                "probleme": pruef_json(t.custom_problems(p)),
                "warnung": t.custom_warning(p)}
        clash = t.category_clash(p)
        fall["fachbereich_nah"] = None if clash is None else list(clash)
        if index % 4 == 0:
            with t.preview_colors(p):
                karte = game.map_palette()
            karte = {k: karte[k] for k in ("boden", "schrift", "strasse", "wasser")}
            fall["karte"] = karte
            fall["pruefungen_karte"] = pruef_json(t.custom_checks(p, karte))
            fall["probleme_karte"] = pruef_json(t.custom_problems(p, karte))
            fall["warnung_karte"] = t.custom_warning(p, karte)
        paletten.append(fall)

    # Kurze Problemlisten fuer warning_lines (Grenze 4)
    zeilen = []
    for anzahl in (0, 1, 4, 5, 9):
        probleme = [("Element %d" % i, 4.5 - i * 0.37, 4.5 if i % 2 else 3.0)
                    for i in range(anzahl)]
        zeilen.append({"probleme": pruef_json(probleme), "zeilen": t.warning_lines(probleme)})
    verhaeltnisse = [0.0, 1.0, 1.05, 2.999999, 3.0, 4.49, 4.5, 4.55, 4.6, 12.345, 21.0]
    format_faelle = [[v, t.format_ratio(v)] for v in verhaeltnisse]

    # ------------------------------------------------------------ 4. valid_custom
    pruefen = []
    for text in PRUEF_TEXTE:
        wert = json.loads(text)
        pruefen.append({"text": text, "eingabe": marker(wert),
                        "ergebnis": werte_json(t.valid_custom(wert))})

    # ------------------------------------------------------------ 5. Zustand
    zustand = []
    andere = {"akzent1": (200, 80, 40), "akzent2": (20, 90, 55), "hintergrund": (220, 30, 12)}
    noch_andere = {"akzent1": (201, 80, 40), "akzent2": (20, 90, 55), "hintergrund": (220, 30, 12)}
    for modus in t.MODE_IDS:
        for welt in (("cyan_pink", "violett"), ("gruen_lime", "schwarz")):
            t.current_preset, t.current_background = welt
            start_w = t.custom_start(modus)
            for gespeichert in (None, start_w, andere):
                t.custom_colors.clear()
                if gespeichert is not None:
                    t.custom_colors[modus] = gespeichert
                for regler in (start_w, andere, noch_andere):
                    for klick in (None, ("rot_pink", "aubergine"), welt):
                        zustand.append({
                            "darstellung": modus, "grundfarbe": welt[0], "hintergrund": welt[1],
                            "gespeichert": werte_json(gespeichert), "regler": werte_json(regler),
                            "klick": None if klick is None else list(klick),
                            "zustand": t.custom_state(regler, modus),
                            "zustand_text": t.custom_state_text(regler, modus),
                            "ist_start": t.is_start(regler, modus, klick),
                            "zu_speichern": werte_json(t.custom_to_save(modus, regler, klick)),
                            "zu_speichern_ohne": werte_json(t.custom_to_save(modus, None, klick)),
                            "kachel_setzt_regler": t.tile_sets_sliders(modus, regler, klick),
                        })
    t.custom_colors.clear()
    t.current_preset, t.current_background = t.DEFAULT_PRESET, t.DEFAULT_BACKGROUND

    # ------------------------------------------------------------ 6. Speichern
    pfad = update._settings_path()
    speichern = []
    gespeichert_faelle = []
    for bisher in BISHER:
        einstellungen = {"auto_check": True, "darstellung": "dunkel"}
        if bisher != FEHLT:
            einstellungen["eigene_farben"] = bisher
        with open(pfad, "w", encoding="utf-8") as handle:
            json.dump(einstellungen, handle)
        gespeichert_faelle.append({
            "bisher": None if bisher == FEHLT else bisher, "fehlt": bisher == FEHLT,
            "ergebnis": {m: werte_json(v) for m, v in t.saved_custom().items()}})
        for modus in ("dunkel", "hell", "zukunft"):
            for neu in NEUE_WERTE:
                with open(pfad, "w", encoding="utf-8") as handle:
                    json.dump(einstellungen, handle)
                t.custom_colors.clear()
                rueck = t.save_custom(modus, neu)
                danach = update.load_settings()
                speichern.append({
                    "bisher": None if bisher == FEHLT else bisher, "fehlt": bisher == FEHLT,
                    "darstellung": modus, "werte": werte_json(neu),
                    "rueckgabe": werte_json(rueck),
                    "vorhanden": "eigene_farben" in danach,
                    "ergebnis": danach.get("eigene_farben"),
                })
    t.custom_colors.clear()

    # ------------------------------------------------------------ 7. Regler-Texte
    regler_texte = []
    for teil_id, teil_name in t.CUSTOM_PARTS:
        for kanal in t.CUSTOM_CHANNELS:
            for wert in sorted({kanal[2], kanal[3], 45, 7}):
                regler_texte.append({"teil": teil_id, "kanal": kanal[0], "wert": wert,
                                     "beschriftung": t.slider_label(teil_name, kanal, wert),
                                     "text": t.slider_text(kanal, wert)})

    # HSL-Hilfen direkt
    hsl = []
    for werte in [(0, 0, 0), (359, 100, 100), (210, 45, 50), (720, 50, 50), (-30, 50, 50),
                  (10, 150, 50), (10, 50, -5), (12, 33.6, 47.25), (0.5, 99.99, 0.01)]:
        hsl.append({"hsl": list(werte), "hex": t.hsl_to_hex(*werte)})
    for farbe in ("#000000", "#FFFFFF", "#7C3AED", "#DB2777", "#120A24", "#F4F0FB",
                  "#808080", "#FF0000", "#00FF01", "#0100FF", "#FEFEFE", "#010000"):
        hsl.append({"hex": farbe, "zurueck": list(t.hex_to_hsl(farbe))})

    # Farbabstand (Delta E) und hex_to_hsl fuer viele zufaellige Farben (fester Seed)
    zufall = random.Random(57)
    abstaende = []
    for _ in range(400):
        a, b = ("#%06X" % zufall.randint(0, 0xFFFFFF) for _ in range(2))
        abstaende.append([a, b, t.color_distance(a, b)])
    for _ in range(400):
        farbe = "#%06X" % zufall.randint(0, 0xFFFFFF)
        hsl.append({"hex": farbe, "zurueck": list(t.hex_to_hsl(farbe))})

    daten = {
        "_hinweis": "Vergleichswerte G6b, erzeugt von werkzeuge/vergleich/erzeuge_eigene_farben.py",
        "quelle": "fisi_theme (eigene Farben), fisi_update, fisi_game.map_palette (Python v0.63.2)",
        "python": sys.version.split()[0],
        "texte": texte,
        "teile": [list(p) for p in t.CUSTOM_PARTS],
        "kanaele": kanaele,
        "darstellungen": [list(m) for m in t.MODES],
        "ziele": {"schrift": t.TEXT_TARGET, "flaeche": t.AREA_TARGET,
                  "mindestkontrast": t.MIN_CONTRAST, "fachbereich_abstand": t.CATEGORY_DISTANCE},
        "start": start,
        "start_unbekannt": unbekannt,
        "paletten": paletten,
        "warnzeilen": zeilen,
        "verhaeltnisse": format_faelle,
        "pruefen": pruefen,
        "zustand": zustand,
        "gespeichert": gespeichert_faelle,
        "speichern": speichern,
        "regler_texte": regler_texte,
        "hsl": hsl,
        "farbabstaende": abstaende,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=1, allow_nan=False)
        handle.write("\n")
    print("G6b: %d Startwerte, %d Paletten, %d Pruef-, %d Zustands-, %d Speicherfaelle -> %s"
          % (len(start), len(paletten), len(pruefen), len(zustand), len(speichern), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
