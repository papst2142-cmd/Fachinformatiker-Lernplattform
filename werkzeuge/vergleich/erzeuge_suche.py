#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G19 (Phase 2, Welle 3, Paket L8 "Suche"), gerechnet
vom echten Python-Code der Python-Linie (v0.63.2, eingefroren):

  klein        - str.lower() fuer jedes Zeichen, das sich dabei aendert (alle
                 Codepunkte ausser Ersatzzeichen U+D800-U+DFFF). Das ist die
                 Tabelle, die C# statt ToLowerInvariant nutzt (Core/Suche).
  sigma        - Schluss-Sigma: Python schreibt das grosse Sigma am Wortende
                 als "ς" (Final_Sigma). Dafuer braucht es zwei Zeichenmengen,
                 gemessen mit lower() selbst: "gross/klein und nicht
                 uebergehbar" (c + "Σ" endet auf "ς") und "uebergehbar"
                 ("A" + c + "Σ" endet auf "ς", c + "Σ" nicht).
  klein_faelle - lower() fuer feste Texte (Sigma, ß, İ, Emoji ...).
  suche        - globale Suche wie app_gui.SearchView.search bzw.
                 mobile/src/main.SearchScreen.search ohne die Hilfe-Treffer
                 (die kommen mit der Hilfe in Phase 4, Plan E3):
                 fisi_optionen.search_options(anfrage, pc) und danach
                 fisi_core.search_content(anfrage). Anfrage = Begriff mit
                 str.strip() (PC: TopBar._search, Handy: SearchScreen.search;
                 leer = keine Suche). Je Treffer Art, Fachbereich, Titel,
                 Kopfzeile ("%s · %s" mit CATEGORY_SHORT), Ausschnitt des
                 Details (hoechstens 140 Zeichen, sonst 138 + "…") und bei Optionen
                 das Ziel fisi_optionen.hit_target (Bereich, Vorlagen).
                 Gespeichert werden die ersten 60 Zeilen (SEARCH_MAX_ROWS),
                 die Anzahl aller Treffer und der Infotext. Gleicht das Handy
                 dem PC, steht dort "wie_pc".
  texte        - Beschriftungen der Suche aus app_gui.py (PC-Wortlaut) und
                 fisi_optionen.py, mit ast gelesen: je Text Datei, Zeile und
                 Nummer in der Zeile.

Es werden keine Lerndaten benutzt; alle Daten- und Einstellungsordner zeigen
in einen leeren Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_suche.py <pfad_fisi_lernplattform> tests/Vergleich/suche.json
"""
import ast
import atexit
import json
import os
import shutil
import sys
import tempfile
import unicodedata

# Feste Begriffsliste aus test_leistung_suche_0632.py (TERMS) und Sonderzeichen
BEGRIFFE = ["Subnetz", "RAID", "zz-nichts", "a", "IPv6", "ä", "Datenschutz", "Farbe",
            "Hilfe", "z. B.", "z.B.", "RAID ", " raid", "192.168", "%", "(", "Maß", "groß",
            "ss", "Port", "OSI", "e", "Firewall", "–",
            # Sonderzeichen (Plan G19)
            "ß", "ẞ", "STRASSE", "İ", "i̇", "I", "ı", "😀", "Σ", "σ", "ς", "ΟΔΟΣ", "K", "ﬁ",
            "Ä", "SUBNETZ", "dns", "DSGVO", " ", "  ", "\tVLAN\n", "Vorlagen", "Hell",
            "Dunkel", "Schrift", "Update", "Token", "Grundfarbe", "Datenbank", "Hänger",
            "Optische", "Rahmenplan", "Lernserie", "Sicherung", "Spiel", "über", "Über",
            "Ozean", "Mitternacht", "vlan", "Prüfung", "OSI-Modell", "TCP/IP", "x" * 200]

KLEIN_FAELLE = ["", "Σ", "ΑΣ", "ΑΣ.", "ΑΣ Β", "ΣΑ", "Α.Σ", "Α'Σ", "ΑΣ'", "ΑΣ'Β", "ΆΣ",
                "ΟΔΟΣ ΚΑΙ ΟΔΟΣ", "1Σ", "ἈΣ", "ꭰΣ", "ʰΣ", "AΣ­", "AΣ­B", "İstanbul", "ẞ",
                "Ǆ ǅ ǆ", "Ⅻ Ⓐ", "K Å", "😀Σ", "Σ😀", "ΣΣ", "ΑΣΣ", "TCP/IP", "Ä Ö Ü ß", "İ̇"]

# Texte der Suche (Datei, Zeile, Nummer des Textes in der Zeile)
TEXTE = {
    "SucheTexte.Titel": ("app_gui.py", 8262, 0),
    "SucheTexte.Info": ("app_gui.py", 8303, 0),
    "SucheTexte.Keine": ("app_gui.py", 8306, 0),
    "SucheTexte.Weitere": ("app_gui.py", 8339, 0),
    "SucheTexte.Feld": ("app_gui.py", 1031, 0),
    "SucheTexte.Kopf": ("app_gui.py", 8360, 0),
    "SucheTexte.Auslassung": ("app_gui.py", 8369, 0),
    "SucheTexte.OptionenDetail": ("fisi_optionen.py", 214, 0),
}

ZEILEN = 60      # SEARCH_MAX_ROWS (app_gui 8253), Handy hits[:60]


def literal(baum, zeile, nummer):
    knoten = sorted((k for k in ast.walk(baum)
                     if isinstance(k, ast.Constant) and isinstance(k.value, str) and k.lineno == zeile),
                    key=lambda k: k.col_offset)
    return knoten[nummer].value


def bereiche(werte):
    ergebnis = []
    for wert in werte:
        if ergebnis and ergebnis[-1][1] == wert - 1:
            ergebnis[-1][1] = wert
        else:
            ergebnis.append([wert, wert])
    return ergebnis


def zeichen():
    return (chr(c) for c in range(0x110000) if not 0xD800 <= c < 0xE000)


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    atexit.register(shutil.rmtree, tmp, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_optionen as fo

    baeume = {}
    for datei in ("app_gui.py", "fisi_optionen.py"):
        with open(os.path.join(repo, datei), encoding="utf-8") as handle:
            baeume[datei] = ast.parse(handle.read(), datei)
    texte = {schluessel: literal(baeume[datei], zeile, nummer)
             for schluessel, (datei, zeile, nummer) in TEXTE.items()}
    assert texte["SucheTexte.Kopf"] == "%s · %s", texte["SucheTexte.Kopf"]

    klein = [[ord(c), c.lower()] for c in zeichen() if c.lower() != c]

    def schluss(text):
        return text.lower()[-1:] == "ς"

    gross_klein = [ord(c) for c in zeichen() if schluss(c + "Σ")]
    uebergehbar = [ord(c) for c in zeichen() if schluss("A" + c + "Σ") and not schluss(c + "Σ")]

    def zeile(art, fachbereich, titel, detail, anfrage):
        kopf = (texte["SucheTexte.Kopf"] % (art, core.CATEGORY_SHORT.get(fachbereich, ""))
                if fachbereich else art)
        ausschnitt = detail if len(detail) <= 140 else detail[:138] + "…"
        eintrag = {"art": art, "fachbereich": fachbereich, "titel": titel, "kopf": kopf,
                   "ausschnitt": ausschnitt}
        if art == fo.SEARCH_KIND:
            eintrag["ziel"] = list(fo.hit_target(titel, anfrage))
        return eintrag

    suche = []
    for begriff in BEGRIFFE:
        anfrage = begriff.strip()
        eintrag = {"begriff": begriff, "anfrage": anfrage}
        for geraet, pc in (("pc", True), ("handy", False)):
            if not anfrage:
                eintrag[geraet] = None
                continue
            treffer = [(fo.SEARCH_KIND, None, titel, text)
                       for _id, titel, text, _vorlagen in fo.search_options(anfrage, pc=pc)]
            treffer += core.search_content(anfrage)
            eintrag[geraet] = {
                "anzahl": len(treffer),
                "info": texte["SucheTexte.Info"] % (len(treffer), anfrage),
                "zeilen": [zeile(*t, anfrage) for t in treffer[:ZEILEN]],
                "weitere": (texte["SucheTexte.Weitere"] % (len(treffer) - ZEILEN)
                            if len(treffer) > ZEILEN else None),
            }
        if eintrag["handy"] == eintrag["pc"]:
            eintrag["handy"] = "wie_pc"       # Datei kleiner halten
        suche.append(eintrag)

    daten = {
        "quelle": "fisi_core.search_content, fisi_optionen.search_options/hit_target, "
                  "app_gui.SearchView (ast), str.lower (Python v0.63.2)",
        "python": sys.version.split()[0],
        "unicode": unicodedata.unidata_version,
        "zeilen": ZEILEN,
        "texte": texte,
        "konstanten": {"KIND_QUIZ": core.KIND_QUIZ, "SEARCH_KIND": fo.SEARCH_KIND,
                       "TEMPLATES_TITLE": fo.TEMPLATES_TITLE,
                       "TEMPLATE_WORDS": list(fo.TEMPLATE_WORDS)},
        "klein": klein,
        "sigma": {"gross_klein": bereiche(gross_klein), "uebergehbar": bereiche(uebergehbar)},
        "klein_faelle": [[t, t.lower()] for t in KLEIN_FAELLE],
        "suche": suche,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G19: %d Zeichen, %d Begriffe -> %s" % (len(klein), len(suche), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
