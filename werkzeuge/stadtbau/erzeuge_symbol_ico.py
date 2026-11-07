"""Weisung P12-51 (Nico 07.11.2026 09:40 MEZ): icon.ico des Stadtbau-Werkzeugs aus dem freigegebenen Bild.

src/Stadtbau.App/Assets/icon.png ist das freigegebene Bild (bytegleich mit Fi-Austausch anlagen/stadtbau_symbol/
Stadtbau_Symbol_512.png, auch src/Stadtbau.Android/Icon.png). Daraus entsteht icon.ico mit 16, 24, 32, 48, 64, 128 und 256
Bildpunkten (PIL, bytegleich wiederholbar). Aufruf:
  python3 -I werkzeuge/stadtbau/erzeuge_symbol_ico.py <repo> [schreiben|pruefen]
"""
import io
import os
import sys

from PIL import Image

GROESSEN = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]


def main():
    repo = sys.argv[1] if len(sys.argv) > 1 else "."
    art = sys.argv[2] if len(sys.argv) > 2 else "pruefen"
    ordner = os.path.join(repo, "src", "Stadtbau.App", "Assets")
    puffer = io.BytesIO()
    Image.open(os.path.join(ordner, "icon.png")).save(puffer, format="ICO", sizes=GROESSEN)
    ziel = os.path.join(ordner, "icon.ico")
    if art == "schreiben":
        with open(ziel, "wb") as f:
            f.write(puffer.getvalue())
        print("geschrieben:", ziel)
        return 0
    with open(ziel, "rb") as f:
        gleich = f.read() == puffer.getvalue()
    print("icon.ico stimmt" if gleich else "FEHLER: icon.ico passt nicht zu icon.png")
    return 0 if gleich else 1


if __name__ == "__main__":
    sys.exit(main())
