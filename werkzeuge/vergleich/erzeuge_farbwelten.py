#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G6 (Auflage A2): alle 72 Farbwelten
(6 Grundfarben x 6 Hintergruende x 2 Darstellungen) mit allen Farbwerten
und Verlaeufen, berechnet vom echten Python-Code (fisi_theme.farbwelt_palette,
ohne eigene Farben).

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_farbwelten.py <pfad_fisi_lernplattform> tests/Vergleich/farbwelten.json
"""
import json
import os
import sys
import tempfile


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
    welten = []
    for modus in t.MODE_IDS:
        for grund in t.PRESET_IDS:
            for hinter in t.BACKGROUND_IDS:
                p = t.farbwelt_palette(modus, (grund, hinter))
                welten.append({
                    "darstellung": modus, "grundfarbe": grund, "hintergrund": hinter,
                    "hell": p["light"],
                    "farben": dict(sorted(p["colors"].items())),
                    "verlaeufe": {k: list(v) for k, v in sorted(p["gradients"].items())},
                })
    with open(ziel, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"_hinweis": "Vergleichswerte G6, erzeugt von werkzeuge/vergleich/erzeuge_farbwelten.py",
                   "welten": welten}, f, ensure_ascii=False, indent=1)
        f.write("\n")
    print("geschrieben: %s (%d Farbwelten)" % (ziel, len(welten)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
