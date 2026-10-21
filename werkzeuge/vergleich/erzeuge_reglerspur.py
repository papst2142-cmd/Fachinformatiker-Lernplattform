#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G23 (farbige Reglerspur der eigenen Farben, ab
Python 0.58, Plan 3a), gerechnet vom echten Python-Code der Python-Linie
(fisi_theme.track_color, track_colors, track_fraction, knob_colors,
knob_contrast, _safe_triple; Weisung P2-14 A5):

  1. Konstanten: KNOB_LIGHT, KNOB_DARK, KNOB_MIN_CONTRAST, die Regler-Kanaele.
  2. Spuren: fuer feste und zufaellige Reglerwerte (fester Seed) je Kanal die
     Spurfarbe an jedem ganzzahligen Wert (Stuetzfarben, Weisung P2-14 Frage 4 a)
     und an 11 gleich verteilten Stellen (track_colors(..., 11)); dazu
     track_fraction und knob_colors (Fuellung, Rand).
  3. Knopf-Raster wie test_darstellung.test_knopf_mindestens_3_zu_1 (Farbton
     0..345 in 15er-Schritten, Saettigung 0..100 in 10er-, Helligkeit 0..100 in
     5er-Schritten, je Kanal): "H" = Fuellung hell, "D" = dunkel; dazu der
     kleinste knob_contrast.
  4. Startwerte der 72 Farbwelten (custom_start): knob_colors und knob_contrast
     je Teil und Kanal (Messwert fuer den Bericht).
  5. Grenzfaelle fuer _safe_triple (ausserhalb der Grenzen, NaN, zu kurz).

Es werden nur Testdaten benutzt; alle Daten- und Einstellungsordner zeigen in
einen leeren Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_reglerspur.py <pfad_fisi_lernplattform> tests/Vergleich/reglerspur.json
"""
import json
import os
import random
import sys
import tempfile

TEILE = ("akzent1", "akzent2", "hintergrund")

FESTE_WERTE = [
    (0, 0, 0), (0, 0, 50), (0, 0, 100), (359, 100, 100), (359, 100, 0),
    (0, 100, 50), (60, 100, 50), (120, 100, 50), (180, 100, 50), (240, 100, 50), (300, 100, 50),
    (210, 45, 50), (263, 83, 58), (330, 81, 60), (60, 100, 90), (240, 100, 20),
    (30, 5, 97), (220, 30, 12), (45, 100, 51), (200, 80, 40),
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
    import fisi_theme as t

    t.custom_colors.clear()
    kanaele = [list(c[:4]) for c in t.CUSTOM_CHANNELS]

    # ------------------------------------------------------------ 2. Spuren
    zufall = random.Random(20261021)
    werte_liste = list(FESTE_WERTE)
    for _ in range(12):
        werte_liste.append((zufall.randint(0, 359), zufall.randint(0, 100), zufall.randint(0, 100)))
    spuren = []
    for werte in werte_liste:
        eintrag = {"werte": list(werte), "kanaele": {}}
        for key, _name, low, high in kanaele:
            stuetzen = [t.track_color(werte, key, (wert - low) / float(high - low))
                        for wert in range(low, high + 1)]
            fuellung, rand = t.knob_colors(werte, key)
            eintrag["kanaele"][key] = {
                "stuetzen": stuetzen,
                "elf": t.track_colors(werte, key, 11),
                "stelle": t.track_fraction(werte, key),
                "knopf": [fuellung, rand],
                "kontrast": t.knob_contrast(werte, key),
            }
        spuren.append(eintrag)

    # ------------------------------------------------------------ 3. Knopf-Raster
    raster = []
    kleinster = 99.0
    for key in "hsl":
        for hue in range(0, 360, 15):
            for sat in range(0, 101, 10):
                zeile = []
                for light in range(0, 101, 5):
                    fuellung = t.knob_colors((hue, sat, light), key)[0]
                    zeile.append("H" if fuellung == t.KNOB_LIGHT else "D")
                    kleinster = min(kleinster, t.knob_contrast((hue, sat, light), key))
                raster.append({"kanal": key, "farbton": hue, "saettigung": sat, "knopf": "".join(zeile)})

    # ------------------------------------------------------------ 4. Startwerte der Farbwelten
    welten = []
    for modus in t.MODE_IDS:
        for grund in t.PRESET_IDS:
            for hinter in t.BACKGROUND_IDS:
                werte = t.custom_start(modus, (grund, hinter))
                teile = {}
                for teil in TEILE:
                    teile[teil] = {key: {"knopf": list(t.knob_colors(werte[teil], key)),
                                         "kontrast": t.knob_contrast(werte[teil], key)}
                                   for key in "hsl"}
                welten.append({"darstellung": modus, "grundfarbe": grund, "hintergrund": hinter,
                               "start": {k: list(werte[k]) for k in TEILE}, "teile": teile})

    # ------------------------------------------------------------ 5. Grenzfaelle
    grenz = []
    for werte in ((999, -5, 300), (-1, 101, -0.5), (359.6, 100.4, 50.5), (float("nan"), 50, 50), (10, 20)):
        sicher = t._safe_triple(werte)
        grenz.append({"werte": [None if v != v else v for v in werte], "sicher": list(sicher),
                      "mitte": {key: t.track_color(werte, key, 0.5) for key in "hsl"},
                      "knopf": {key: t.knob_colors(werte, key)[0] for key in "hsl"}})

    daten = {
        "quelle": "fisi_theme track_color/knob_colors (Python-Linie)",
        "python": sys.version.split()[0],
        "knopf_hell": t.KNOB_LIGHT,
        "knopf_dunkel": t.KNOB_DARK,
        "knopf_mindestkontrast": t.KNOB_MIN_CONTRAST,
        "kanaele": kanaele,
        "spuren": spuren,
        "raster": raster,
        "raster_kleinster_kontrast": kleinster,
        "welten": welten,
        "grenzfaelle": grenz,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=1, allow_nan=False)
        handle.write("\n")
    print("G23: %d Spuren, %d Rasterzeilen, %d Farbwelten, %d Grenzfaelle -> %s"
          % (len(spuren), len(raster), len(welten), len(grenz), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
