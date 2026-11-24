"""Welle A 1.7.0 (Sammelliste 447, Weisung P12-367): welche Dreiecke der Spielfigur zu Brust, Bauch und Becken gehoeren.

Jedes Dreieck gehoert zu dem Knochen, der die meisten seiner Ecken am staerksten bewegt (FigurNetz in wandle_modelle.py). Die
Dreiecke liegen in jedem Bild jeder Bewegung der Figur in derselben Reihenfolge (modelle.bin). Der Test
KriechenBodenTests liest die Liste und misst, ob der Rumpf der Kriech-Bilder auf dem Boden liegt (Meldung P12-233).

Aufruf: python3 -I werkzeuge/grafik/rumpf_figur.py <ordner mit den ZIPs> <ausgabe.json>
"""
import json
import os
import sys
import zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wandle_modelle as w  # noqa: E402


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    with open(os.path.join(w.HIER, "auswahl.json"), encoding="utf-8") as f:
        paket = json.load(f)["pakete"]["characters"]
    with zipfile.ZipFile(os.path.join(sys.argv[1], paket["zip"])) as z:
        figur = w.FigurNetz(z, "Model/characterMedium.fbx", "Skins/humanMaleA.png")
    teile = {"brust": w.ROBBEN_BRUST, "bauch": w.ROBBEN_BAUCH, "becken": w.ROBBEN_BECKEN}
    aus = {
        "_hinweis": "Erzeugt von werkzeuge/grafik/rumpf_figur.py (Welle A 1.7.0, Sammelliste 447). Nicht von Hand aendern.",
        "datei": "Model/characterMedium.fbx",
        "dreiecke": len(figur.knochen),
    }
    for name, knochen in teile.items():
        aus[name] = [i for i, k in enumerate(figur.knochen) if k in knochen]
    with open(sys.argv[2], "w", encoding="utf-8", newline="\n") as f:
        json.dump(aus, f, ensure_ascii=False)
        f.write("\n")
    print("Rumpf: %s" % ", ".join("%s %d" % (n, len(aus[n])) for n in teile))
    return 0


if __name__ == "__main__":
    sys.exit(main())
