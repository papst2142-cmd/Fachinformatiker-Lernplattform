"""A 0.12.2 (Weisung P6-16, Frage 1 A): erzeugt die Symbolschrift fisi_symbole.otf der C#-Linie.

Teilschrift aus "Material Icons" (Google, Apache-2.0), dieselbe Quelle wie die Handy-App der
Python-Linie (MaterialIcons-Regular.otf im Paket flet-web 1.0.1, wie die Ergaenzung in
fisi_symbole_LIZENZ.txt ab 0.56). Enthaelt die 22 Zeichen der Python-Datei fisi_symbole.otf
(Umrisse gleich, beim Erzeugen geprueft; "workspace_premium" dort U+E7AF aus dem Google-Repo,
hier die gerundete Handy-Variante U+F03C0) und die Zeichen, die die Handy-App zusaetzlich zeigt.

Aufruf (pip install fonttools; flet_web-1.0.1-py3-none-any.whl entpackt):
  python3 -I werkzeuge/symbole/erzeuge_symbole.py <MaterialIcons-Regular.otf> <ziel.otf>
Gepruefte Eingabe: MaterialIcons-Regular.otf SHA-256 e2db925853e13f87523787ccbc992688cfec9a33805a6c6b711e3f79c3311268.
"""
import sys

from fontTools import subset
from fontTools.ttLib import TTFont

# Name (Flutter/Flet) -> Zeichencode; gleiche Liste wie Views/Symbole.cs
ZEICHEN = {
    # 22 Zeichen wie fisi_symbole.otf der Python-Linie (PC-Seitenleiste)
    "business_rounded": 0xF5F8, "calculate_rounded": 0xF5FD, "dashboard_rounded": 0xF68E,
    "dns_rounded": 0xF6C1, "euro_rounded": 0xF716, "flag_rounded": 0xF768,
    "help_outline_rounded": 0xF7E4, "home_rounded": 0xF7F5, "insights_rounded": 0xF820,
    "layers_rounded": 0xF847, "security_rounded": 0xF013E, "settings_rounded": 0xF0164,
    "sports_esports_rounded": 0xF01BC, "storage_rounded": 0xF01DE, "storefront_rounded": 0xF01E1,
    "style_rounded": 0xF01E8, "track_changes_rounded": 0xF0248, "work_rounded": 0xF02C7,
    "diamond_rounded": 0xF0306, "edit_note_rounded": 0xF030F, "lan_rounded": 0xF034B,
    "route_rounded": 0xF0377,
    # Handy-App (mobile/src/main.py NAV, LearnScreen, Kopfzeile, FoldCard, CalendarPanel)
    "workspace_premium_rounded": 0xF03C0, "dashboard_outlined": 0xEFA1, "school_outlined": 0xF33C,
    "school_rounded": 0xF012E, "calculate_outlined": 0xEF10, "sports_esports_outlined": 0xF3CA,
    "insights_outlined": 0xF132, "settings_outlined": 0xF36E, "search_rounded": 0xF013D,
    "chevron_right": 0xE15F, "chevron_left": 0xE15E, "arrow_right": 0xE09E, "arrow_drop_down": 0xE098,
}


def main(quelle, ziel):
    schrift = TTFont(quelle)
    # Zeitstempel der Quelle behalten, damit jeder Lauf dieselbe Datei erzeugt
    schrift.recalcTimestamp = False
    namen = schrift.getBestCmap()
    for name, code in ZEICHEN.items():
        glyphe = namen.get(code, "")
        if glyphe not in (name, name + "_baseline"):
            raise SystemExit("Zeichen %s: U+%X ist %r" % (name, code, glyphe))
    optionen = subset.Options()
    optionen.glyph_names = True
    optionen.name_IDs = ["*"]
    optionen.name_languages = ["*"]
    optionen.notdef_outline = True
    optionen.layout_features = []
    teil = subset.Subsetter(optionen)
    teil.populate(unicodes=sorted(ZEICHEN.values()))
    teil.subset(schrift)
    schrift.save(ziel)
    print("%d Zeichen nach %s" % (len(TTFont(ziel).getBestCmap()), ziel))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
