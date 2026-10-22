"""Unabhaengiges Pruefskript G34 (abschlussprojekt.json).

Nur aus der Spezifikation P4-8 (Teil D) und dem Nachtrag P4-9 geschrieben,
ohne Blick in die Python-Linie oder in den C#-Code. Die CI vergleicht die
Ausgabe mit den gleichnamigen Teilen, die der Erzeuger
erzeuge_abschlussprojekt.py aus dem echten Python 0.63.2 schreibt.

Aufruf:
    python3 -I werkzeuge/vergleich/pruefe_abschlussprojekt.py <inhalte-ordner> <ausgabe.json>

Python 3.12 (Unicode 15.0), nur Standardbibliothek.
Schluessel der Ausgabe: zahl, rechnen, export, projekte, fragen, rueckfrage,
pdf, texte, grenze (speichern und zeichen schreibt nur der Erzeuger).
"""

import json
import os
import re
import sqlite3
import sys
import unicodedata
import zlib

# ---------------------------------------------------------------------------
# Feste Werte (Spezifikation Abschnitte 2, 3, 7a)
# ---------------------------------------------------------------------------

MAX_STUNDEN = 40
DOKU_ANTEIL = 20
ZEICHEN_JE_SEITE = 2800
SEITEN_VORGABE = 15
SACHKOSTEN_ZEILEN = 5

PHASEN = [
    ("analyse", "Analyse (Ist-Aufnahme, Anforderungen)", 5),
    ("planung", "Planung und Entwurf (Soll-Konzept, Wirtschaftlichkeit)", 7),
    ("durchfuehrung", "Durchführung / Umsetzung", 14),
    ("test", "Test und Qualitätssicherung", 4),
    ("uebergabe", "Übergabe, Einweisung, Abnahme", 2),
    ("dokumentation", "Dokumentation", 8),
    ("frei1", "", 0),
    ("frei2", "", 0),
]

KOSTEN = [
    ("satz_azubi", "Stundensatz Auszubildende/r (EUR)", "25"),
    ("satz_ma", "Stundensatz Mitarbeiter/in (EUR)", "60"),
    ("stunden_ma", "Stunden Mitarbeiter/in (Abnahme, Einweisung)", "4"),
    ("einsparung", "Einsparung pro Monat (EUR)", "0"),
    ("laufend", "Laufende Kosten pro Monat (EUR)", "0"),
]
KOSTEN_VORGABE = {k: v for k, _b, v in KOSTEN}

ANTRAG = [
    ("bezeichnung", "Projektbezeichnung",
     "Kurzer, aussagekräftiger Titel: Was wird für wen gemacht?"),
    ("ausgangssituation", "Ausgangssituation",
     "Ist-Zustand und Problem beim Auftraggeber, warum das Projekt nötig ist."),
    ("ziel", "Projektziel",
     "Sachziel, Kostenziel, Zeitziel und Qualitätsziel - möglichst messbar."),
    ("umfeld", "Auftraggeber und Projektumfeld",
     "Wer beauftragt, wer ist beteiligt, Schnittstellen, Rahmenbedingungen."),
    ("alternativen", "Lösungsalternativen",
     "Mindestens zwei Möglichkeiten und nach welchen Kriterien entschieden wird."),
    ("systeme", "Technische Systeme und Ressourcen",
     "Hard- und Software, Netz, Werkzeuge, Personal."),
    ("einfuehrung", "Einführung und Pflege",
     "Übergabe, Einweisung, Betrieb und Wartung nach dem Projekt."),
    ("sicherheit", "IT-Sicherheit und Datenschutz",
     "Schutzbedarf, Schwachstellen, geplante Maßnahmen."),
    ("praesentationsmittel", "Dokumentation und Präsentationsmittel",
     "Welche Unterlagen entstehen, womit wird präsentiert."),
]

KAPITEL = [
    ("einleitung", "1. Einleitung",
     "Projektumfeld, Projektziel, Projektbegründung, Projektschnittstellen, Abgrenzung."),
    ("ist", "2. Ist-Analyse", "Bestandsaufnahme und Schwachstellen."),
    ("soll", "3. Soll-Konzept und Lösungsauswahl",
     "Anforderungen, Alternativen, Entscheidung (z. B. Nutzwertanalyse)."),
    ("wirtschaft", "4. Wirtschaftlichkeit", "Projektkosten, Make-or-Buy, Amortisation."),
    ("durchfuehrung", "5. Durchführung",
     "Umsetzung Schritt für Schritt, Abweichungen vom Plan."),
    ("test", "6. Test und Qualitätssicherung", "Testfälle, Testergebnisse, Abnahme."),
    ("uebergabe", "7. Übergabe und Einführung",
     "Einweisung, Dokumentation für Anwender und Administration."),
    ("fazit", "8. Fazit und Ausblick", "Soll-Ist-Vergleich, Lessons Learned, Ausblick."),
]

UEBERSICHT = [
    ("titel", "Projekttitel"),
    ("betrieb", "Ausbildungsbetrieb / Auftraggeber"),
    ("zeitraum", "Durchführungszeitraum"),
    ("frist_antrag", "Abgabe Projektantrag bis"),
    ("frist_doku", "Abgabe Dokumentation bis"),
]

REITER = [
    ("uebersicht", "Übersicht"),
    ("antrag", "Antrag"),
    ("zeitplan", "Zeitplan"),
    ("kosten", "Kosten"),
    ("doku", "Dokumentation"),
    ("fachgespraech", "Fachgespräch"),
]

CHECK_UEBERSICHT = [
    ("antrag_entwurf", "Projektantrag geschrieben"),
    ("antrag_betrieb", "Antrag vom Ausbildungsbetrieb geprüft"),
    ("antrag_eingereicht", "Antrag bei der IHK eingereicht"),
    ("antrag_genehmigt", "Antrag genehmigt"),
    ("durchgefuehrt", "Projekt durchgeführt"),
    ("doku_fertig", "Dokumentation fertig und korrekturgelesen"),
    ("doku_abgegeben", "Dokumentation abgegeben"),
    ("praesentation", "Präsentation erstellt (höchstens 15 Minuten)"),
    ("geprobt", "Präsentation mit Zeitmessung geprobt"),
    ("fachgespraech", "Fachgespräch geübt"),
]


def check_antrag(max_stunden):
    """Antrag-Checkliste; der Text von max40 haengt von max_stunden ab (Nachtrag N1.3)."""
    return [
        ("phasen", "Zeitplan in Phasen mit Stunden"),
        ("max40", "Summe höchstens %d Stunden inklusive Dokumentation" % max_stunden),
        ("eigenleistung", "Eigene Leistung klar erkennbar"),
        ("messbar", "Projektziel messbar beschrieben"),
        ("ki", "Einsatz von KI-Hilfsmitteln gekennzeichnet (frag bei deiner IHK nach)"),
    ]


CHECK_PRAESENTATION = [
    ("p_dauer", "Dauer 12 bis 15 Minuten"),
    ("p_gliederung", "Gliederung: Ausgangslage, Ziel, Vorgehen, Ergebnis, Fazit"),
    ("p_wirtschaft", "Wirtschaftlichkeit kurz dargestellt"),
    ("p_folien", "Folien gut lesbar, wenig Text"),
    ("p_technik", "Technik und Ersatz (USB-Stick, PDF) vorbereitet"),
]

KATEGORIEN = [
    ("netzwerk", "Netzwerk & Protokolle"),
    ("sicherheit", "IT-Sicherheit & Datenschutz"),
    ("systeme", "Systeme, RAID & Hardware"),
    ("wirtschaft", "Wirtschaft & Prozesse"),
    ("datenbanken", "Datenbanken & SQL"),
]
KATEGORIE = dict(KATEGORIEN)

RUECKFRAGE_TITEL = "Projekt löschen"

# ---------------------------------------------------------------------------
# Abschnitt 1: Zahl aus einer Eingabe
# ---------------------------------------------------------------------------

_TAUSEND_LEER = re.compile("[1-9][0-9]{0,2}([   ][0-9]{3})+(,[0-9]+)?")
_TAUSEND_PUNKT = re.compile(r"[1-9][0-9]{0,2}(\.[0-9]{3})+")
_NULL_PUNKT = re.compile(r"0[0-9]{1,2}(\.[0-9]{3})+")
_ERLAUBT = re.compile(r"[0-9.,]+")
_ZIFFERN = re.compile(r"[0-9]+")


class _Wirft(Exception):
    pass


def _lies(t):
    t = t.strip()
    if not t or len(t) > 20:
        raise _Wirft()
    minus = False
    if t.startswith("-"):
        minus = True
        t = t[1:]
    if _TAUSEND_LEER.fullmatch(t):
        t = t.replace(" ", ".").replace(" ", ".").replace(" ", ".")
    if not _ERLAUBT.fullmatch(t) or t.count(",") > 1:
        raise _Wirft()
    ganz, komma, rest = t.partition(",")
    dezimal = bool(komma)
    if komma:
        if "." in ganz:
            if not _TAUSEND_PUNKT.fullmatch(ganz):
                raise _Wirft()
            ganz = ganz.replace(".", "")
    elif "." in ganz:
        if _NULL_PUNKT.fullmatch(ganz):
            raise _Wirft()
        if _TAUSEND_PUNKT.fullmatch(ganz):
            ganz = ganz.replace(".", "")
        elif ganz.count(".") == 1:
            ganz, _p, rest = ganz.partition(".")
            dezimal = True
        else:
            raise _Wirft()
    if dezimal:
        # Dezimalteil: nur Ziffern, mindestens 1, hoechstens 3
        if not _ZIFFERN.fullmatch(rest) or len(rest) > 3:
            raise _Wirft()
    ganz = ganz or "0"
    if not _ZIFFERN.fullmatch(ganz):
        raise _Wirft()
    wert = float(ganz + ("." + rest if rest else ""))
    if wert == 0:
        return 0.0
    return -wert if minus else wert


def to_number(text, vorgabe=0.0):
    t = str(text if text is not None else "")
    t = t.strip().replace("€", "").replace(" ", "")
    if not t:
        return vorgabe
    try:
        return _lies(t)
    except _Wirft:
        return vorgabe


ZAHL_TEXTE = [
    "", " ", "25", "25,5", "1.234,56", "1.000", "1.000.000", "01.000", "0.125",
    "1.5", "1.2.3", "5,", ",5", "1,1234", "1e3", "abc", "inf", "nan", "-0",
    "-0,04", "-12", "12 €", "1 000", "1 000,5", "1 000",
    "999.999,999", "123456789012345678901", "٣",
]


def raster_zahl():
    aus = []
    for text in ZAHL_TEXTE:
        for vorgabe in (0.0, 15):
            aus.append([text, vorgabe, to_number(text, vorgabe)])
    return aus

# ---------------------------------------------------------------------------
# Abschnitt 2: Rechnungen
# ---------------------------------------------------------------------------


def fmt1(x):
    return "%.1f" % x


def stunden_text(x):
    return fmt1(x).replace(".", ",").replace(",0", "") + " Std."


def euro(x):
    s = "{:,.2f}".format(x)
    s = s.replace(",", "X").replace(".", ",").replace("X", ".")
    return s + " €"


def strip(t):
    return t.strip()


def titel_von(fs):
    return strip(fs.get("titel") or "") or "Projekt ohne Titel"


def zeitplan(fs):
    zeilen = []
    for k, name, vorgabe in PHASEN:
        if k.startswith("frei"):
            name = fs.get("phase_name_" + k, "")
        roh = fs.get("phase_" + k)
        stunden = to_number(roh, vorgabe if roh is None else 0.0)
        zeilen.append([k, name, stunden])
    return zeilen


def summe_von(zeilen):
    return sum(z[2] for z in zeilen if z[1] or z[2])


def warnungen(fs, max_stunden=MAX_STUNDEN):
    zeilen = zeitplan(fs)
    summe = summe_von(zeilen)
    aus = []
    if summe > max_stunden:
        aus.append("Die Summe von %s liegt über den erlaubten %d Stunden."
                   % (stunden_text(summe), max_stunden))
    d = [z[2] for z in zeilen if z[0] == "dokumentation"][0]
    if summe != 0 and d / summe * 100 > DOKU_ANTEIL:
        aus.append("Die Dokumentation nimmt %d %% der Zeit ein (üblich höchstens 15 bis 20 %%)."
                   % round(d / summe * 100))
    return aus


def wert(fs, k):
    if "kosten_" + k in fs:
        return to_number(fs["kosten_" + k])
    return to_number(KOSTEN_VORGABE[k])


def sachkosten(fs):
    aus = []
    for n in range(SACHKOSTEN_ZEILEN):
        name = strip(fs.get("material_name_%d" % n) or "")
        betrag = to_number(fs.get("material_betrag_%d" % n))
        if name or betrag:
            aus.append([name or "Position %d" % (n + 1), betrag])
    return aus


def kosten(fs):
    summe = summe_von(zeitplan(fs))
    personal = summe * wert(fs, "satz_azubi") + wert(fs, "stunden_ma") * wert(fs, "satz_ma")
    material = sachkosten(fs)
    material_summe = sum(b for _n, b in material)
    gesamt = personal + material_summe
    netto = wert(fs, "einsparung") - wert(fs, "laufend")
    amortisation = gesamt / netto if netto > 0 else None
    return {
        "stunden": summe,
        "personal": personal,
        "material": material,
        "material_summe": material_summe,
        "gesamt": gesamt,
        "netto_monat": netto,
        "amortisation": amortisation,
    }


def kostenzeilen(fs):
    k = kosten(fs)
    zeilen = [
        "Projektstunden laut Zeitplan: %s" % stunden_text(k["stunden"]),
        "Personalkosten: %s" % euro(k["personal"]),
        "Sachkosten: %s" % euro(k["material_summe"]),
        "Projektkosten gesamt: %s" % euro(k["gesamt"]),
    ]
    if k["amortisation"] is None:
        zeilen.append("Amortisation: keine Einsparung eingetragen")
    else:
        zeilen.append("Amortisation nach %s Monaten (%s Einsparung netto pro Monat)"
                      % (fmt1(k["amortisation"]).replace(".", ","), euro(k["netto_monat"])))
    return zeilen


def seiten(fs):
    zeichen = sum(len(fs.get("kapitel_" + k, "")) for k, _t, _h in KAPITEL)
    ziel = int(to_number(fs.get("seitenziel"), SEITEN_VORGABE)) or SEITEN_VORGABE
    return [zeichen / 2800.0, ziel]


def erledigt(fs):
    return [sum(1 for k, _t in CHECK_UEBERSICHT if fs.get("check_" + k) == "1"), 10]


def antrag_offen(fs):
    return [name for k, name, _h in ANTRAG if not strip(fs.get(k) or "")]


def schaetzung(fs):
    roh = strip(fs.get("schaetzung") or "")
    if not roh:
        return None
    return max(0.0, min(100.0, to_number(roh)))


def export_name(fs, endung):
    basis = re.sub(r"[^A-Za-z0-9ÄÖÜäöüß_-]+", "_", titel_von(fs)).strip("_")
    return "Abschlussprojekt_%s.%s" % (basis[:40] or "Projekt", endung)


def dateiname(fs):
    return [export_name(fs, "pdf"), export_name(fs, "txt")]


def baue_faelle():
    satz = "Wort (Klammer) \\ Pfad → ≤ ✓ ﬁ ń 中 € "
    voll = (satz * 100)[:3000]
    lang = [("titel", "x" * 50)]
    for k, _n, _h in ANTRAG:
        lang.append((k, voll))
    for k, _t, _h in KAPITEL:
        if k == "ist":
            lang.append(("kapitel_" + k, voll + "\n\t" + "W" * 200))
        else:
            lang.append(("kapitel_" + k, voll))
    return [
        ("leer", []),
        ("festwert", [
            ("titel", "Testprojekt"), ("kosten_einsparung", "500"), ("kosten_laufend", "40"),
            ("material_name_0", "Server"), ("material_betrag_0", "4999"),
            ("material_name_1", "Lizenz"), ("material_betrag_1", "1234,5"),
        ]),
        ("warnung", [("phase_dokumentation", "12"), ("phase_durchfuehrung", "20")]),
        ("halb", [
            ("phase_analyse", "3"), ("phase_planung", "0"), ("phase_durchfuehrung", "0"),
            ("phase_test", "0"), ("phase_uebergabe", "0"), ("phase_dokumentation", "5"),
        ]),
        ("frei", [
            ("phase_name_frei1", "Puffer"), ("phase_frei1", "2"), ("phase_frei2", "3"),
            ("phase_analyse", "0,25"), ("phase_test", "-0,04"), ("kosten_satz_azubi", ""),
            ("kosten_stunden_ma", "abc"), ("material_name_2", "  "), ("material_betrag_2", "0"),
            ("material_betrag_3", "-0"), ("material_name_4", "Kabel"), ("material_betrag_4", ""),
            ("material_betrag_0", "0,125"),
        ]),
        ("unicode", [
            ("titel", " Titel \U0001F600 äöü\x1c"),
            ("kapitel_ist", "\U0001F600" * 2800),
            ("schaetzung", " 85,5 "), ("fachbereich", "Netzwerk & Protokolle"),
            ("check_antrag_entwurf", "1"), ("check_max40", "1"), ("check_p_dauer", "0"),
            ("fg_a01", "1"), ("fg_a02", "0"), ("fg_a03", ""), ("bezeichnung", "B"),
            ("ziel", "  "), ("seitenziel", "2,7"),
        ]),
        ("gross", [
            ("kosten_einsparung", "1.000"), ("kosten_laufend", "1.200"),
            ("kosten_satz_azubi", "1.234,5"), ("phase_durchfuehrung", "1.000"),
            ("material_name_0", "Viel"), ("material_betrag_0", "999.999,999"),
            ("seitenziel", "-3"), ("schaetzung", "150"),
        ]),
        ("lang", lang),
    ]


def rechnen(faelle):
    je_fall = []
    for name, felder in faelle:
        fs = dict(felder)
        zp = zeitplan(fs)
        je_fall.append([name, {
            "zeitplan": zp,
            "summe": summe_von(zp),
            "warnungen": warnungen(fs),
            "kosten": kosten(fs),
            "kostenzeilen": kostenzeilen(fs),
            "seiten": seiten(fs),
            "erledigt": erledigt(fs),
            "antrag_offen": antrag_offen(fs),
            "schaetzung": schaetzung(fs),
            "titel": titel_von(fs),
            "dateiname": dateiname(fs),
        }])
    dateititel = ["Mein Projekt: Netz/WLAN äöü \U0001F600", "", "\U0001F600",
                  "x" * 50, "  Ä-ß_1 ", "Ä"]
    return {
        "faelle": [[n, [[f, w] for f, w in felder]] for n, felder in faelle],
        "je_fall": je_fall,
        "stunden_text": [[x, stunden_text(x)] for x in (0, 0.25, 2.04, -0.04, 12.5, 0.05, 40, 36.21, 1026)],
        "euro": [[x, euro(x)] for x in (0, 0.125, 0.005, 1234.5, -1234.5, 999999.999, 1e20)],
        "dateiname": [[t, dateiname({"titel": t})] for t in dateititel],
    }

# ---------------------------------------------------------------------------
# Abschnitt 3: Export
# ---------------------------------------------------------------------------


def bloecke(fs, max_stunden=MAX_STUNDEN):
    b = [["h1", titel_von(fs)]]
    for k, beschriftung in UEBERSICHT[1:]:
        w = strip(fs.get(k) or "")
        if w:
            b.append(["p", "%s: %s" % (beschriftung, w)])
    b.append(["h2", "Projektantrag"])
    for k, name, _h in ANTRAG:
        b.append(["h3", name])
        b.append(["p", strip(fs.get(k) or "") or "(noch offen)"])
    b.append(["h2", "Zeitplanung"])
    zp = zeitplan(fs)
    for _k, name, stunden in zp:
        if name or stunden:
            b.append(["li", "%s: %s" % (name or "Weitere Phase", stunden_text(stunden))])
    b.append(["p", "Summe: %s (höchstens %d Stunden)" % (stunden_text(summe_von(zp)), max_stunden)])
    b.append(["h2", "Kostenrechnung"])
    for name, betrag in sachkosten(fs):
        b.append(["li", "%s: %s" % (name, euro(betrag))])
    for z in kostenzeilen(fs):
        b.append(["p", z])
    b.append(["h2", "Dokumentation"])
    for k, titel, _h in KAPITEL:
        b.append(["h3", titel])
        b.append(["p", strip(fs.get("kapitel_" + k) or "") or "(noch offen)"])
    b.append(["h2", "Checkliste"])
    for liste in (CHECK_UEBERSICHT, check_antrag(max_stunden), CHECK_PRAESENTATION):
        for k, text in liste:
            b.append(["li", "[%s] %s" % ("x" if fs.get("check_" + k) == "1" else " ", text)])
    return b


def text_aus(bl):
    zeilen = []
    for art, t in bl:
        if art == "h1":
            zeilen += [t, "=" * len(t), ""]
        elif art == "h2":
            zeilen += ["", t, "-" * len(t)]
        elif art == "h3":
            zeilen += ["", t]
        elif art == "li":
            zeilen.append("  - " + t)
        else:
            zeilen.append(t)
    return "\n".join(zeilen).strip() + "\n"


def export(faelle):
    aus = []
    for name, felder in faelle:
        fs = dict(felder)
        bl = bloecke(fs)
        text = text_aus(bl)
        eintrag = {"bloecke": bl, "text": text}
        if name in ("leer", "unicode"):
            eintrag["datei_pc"] = (b"\xef\xbb\xbf" + text.replace("\n", "\r\n").encode("utf-8")).hex()
            eintrag["datei_handy"] = text.encode("utf-8").hex()
        aus.append([name, eintrag])
    return aus

# ---------------------------------------------------------------------------
# Abschnitt 4: Projekte in SQLite
# ---------------------------------------------------------------------------

_LOESCH_1 = ("DELETE FROM abschlussprojekt WHERE feld != '_geloescht' AND wert != '' AND EXISTS "
             "(SELECT 1 FROM abschlussprojekt AS loesch WHERE loesch.projekt = abschlussprojekt.projekt "
             "AND loesch.feld = '_geloescht' AND loesch.wert = '1' AND loesch.timestamp < abschlussprojekt.timestamp);")
_LOESCH_2 = ("DELETE FROM abschlussprojekt WHERE EXISTS (SELECT 1 FROM abschlussprojekt AS neu WHERE "
             "neu.projekt = abschlussprojekt.projekt AND neu.feld = abschlussprojekt.feld AND "
             "(neu.timestamp > abschlussprojekt.timestamp OR (neu.timestamp = abschlussprojekt.timestamp "
             "AND neu.uid > abschlussprojekt.uid)));")


class Projektspeicher:
    def __init__(self):
        self.db = sqlite3.connect(":memory:")
        self.db.execute(
            "CREATE TABLE abschlussprojekt (id INTEGER PRIMARY KEY AUTOINCREMENT, timestamp TEXT NOT NULL, "
            "projekt TEXT NOT NULL, feld TEXT NOT NULL, wert TEXT NOT NULL DEFAULT '', uid TEXT)")
        self.db.commit()
        self.zaehler = 0
        self.zeit = ""

    def speichere(self, p, f, w):
        self.zaehler += 1
        uid = "u%02d" % self.zaehler
        with self.db:
            self.db.execute(
                "INSERT INTO abschlussprojekt (timestamp, projekt, feld, wert, uid) VALUES (?, ?, ?, ?, ?)",
                (self.zeit, p, f, w, uid))
            self.db.execute(_LOESCH_1)
            self.db.execute(_LOESCH_2)

    def felder(self):
        aus = {}
        for p, f, w in self.db.execute(
                "SELECT projekt, feld, wert FROM abschlussprojekt ORDER BY timestamp, uid"):
            aus.setdefault(p, {})[f] = w
        return aus

    def projekte(self):
        return {p: fs for p, fs in self.felder().items() if fs.get("_geloescht") != "1"}

    def loeschen(self, p):
        self.speichere(p, "_geloescht", "1")
        for f, w in list(self.felder().get(p, {}).items()):
            if f != "_geloescht" and w != "":
                self.speichere(p, f, "")
        return True

    def vorlage(self, p, position, projektarbeiten):
        t = projektarbeiten[position]

        def text(schluessel):
            v = t.get(schluessel, "")
            if isinstance(v, list):
                v = "\n".join(v)
            return v

        alt = self.projekte().get(p, {})
        werte = [
            ("titel", t.get("title", "")),
            ("bezeichnung", t.get("title", "")),
            ("betrieb", t.get("branche", "")),
            ("ausgangssituation", text("ausgangssituation")),
            ("ziel", text("auftrag")),
            ("umfeld", "\n".join("- " + r for r in t.get("rahmenbedingungen", []))),
            ("fachbereich", KATEGORIE.get(t.get("cat"), "")),
        ]
        anzahl = 0
        for f, w in werte:
            if w and not strip(alt.get(f) or ""):
                self.speichere(p, f, w)
                anzahl += 1
        return anzahl

    def menue(self):
        pr = self.projekte()
        return sorted(pr, key=lambda p: titel_von(pr[p]).lower())

    def aktiv(self, e):
        pr = self.projekte()
        if e.get("aktives_projekt") in pr:
            return e.get("aktives_projekt")
        if pr:
            return sorted(pr)[0]
        return "neu"


def projekte_ablauf(projektarbeiten):
    s = Projektspeicher()
    tag = "2026-10-22 "
    ergebnisse = []

    def zu(uhr):
        s.zeit = tag + uhr

    zu("08:00:00.000001"); s.speichere("p-b", "titel", "Beta")
    zu("08:00:00.000002"); s.speichere("p-a", "titel", "alpha")
    zu("08:00:00.000003"); s.speichere("p-c", "titel", "")
    zu("08:00:00.000004"); s.speichere("p-d", "titel", "Delta")
    zu("08:00:00.000005"); s.speichere("p-d", "ziel", "Text D")
    zu("08:01:00.000000"); ergebnisse.append(s.loeschen("p-d"))
    zu("08:02:00.000000"); ergebnisse.append(s.vorlage("p-c", 0, projektarbeiten))
    zu("08:02:30.000000"); ergebnisse.append(s.vorlage("p-c", 0, projektarbeiten))
    zu("08:03:00.000000"); ergebnisse.append(s.vorlage("p-a", 49, projektarbeiten))
    zu("08:04:00.000000"); ergebnisse.append(s.loeschen("p-b"))
    for i, (p, t) in enumerate([("p-h", "Alpha"), ("p-e", "Äpfel"),
                                ("p-f", "\U0001F600 Emoji"), ("p-g", " Privat")], start=1):
        zu("08:05:00.%06d" % i)
        s.speichere(p, "titel", t)

    zeilen = [list(r) for r in s.db.execute(
        "SELECT timestamp, projekt, feld, wert, uid FROM abschlussprojekt ORDER BY id")]
    pr = s.projekte()
    aktiv = [s.aktiv(e) for e in ({}, {"aktives_projekt": "p-d"},
                                  {"aktives_projekt": "p-c"}, {"aktives_projekt": "p-b"})]
    aktiv.append(Projektspeicher().aktiv({}))
    return {
        "zeilen": zeilen,
        "ergebnisse": ergebnisse,
        "projekte": [[p, [[f, w] for f, w in fs.items()]] for p, fs in pr.items()],
        "menue": s.menue(),
        "titel": [[p, titel_von(fs)] for p, fs in pr.items()],
        "aktiv": aktiv,
    }

# ---------------------------------------------------------------------------
# Abschnitt 5: Fachgespraech
# ---------------------------------------------------------------------------


def fragen_teil(fachgespraech):
    fragen = []
    for q in fachgespraech["fragen"]:
        cat = q.get("cat")
        fragen.append({"id": q["id"], "cat": None if cat is None else KATEGORIE.get(cat)})

    def fragen_fuer(fs):
        ziel = fs.get("fachbereich") or None
        return [q["id"] for q in fragen if q["cat"] is None or q["cat"] == ziel]

    auswahl = [[fb, fragen_fuer({"fachbereich": fb})]
               for fb in [""] + [n for _k, n in KATEGORIEN] + ["Unbekannt"]]
    bewertung = []
    for w in (None, "", "1", "0", "2", "ja"):
        bewertung.append([w, None if w is None or w == "" else w == "1"])
    return {"auswahl": auswahl, "bewertung": bewertung}

# ---------------------------------------------------------------------------
# Abschnitt 6: Rueckfrage Loeschen
# ---------------------------------------------------------------------------

_MIT_ABGLEICH = ("„%s“ löschen? Das Projekt und alle seine Eingaben werden gelöscht, "
                 "nach dem Abgleich auch auf deinen anderen Geräten. Das lässt sich in der App "
                 "nicht rückgängig machen.\n\nHinweis: Ältere Sicherungsdateien und frühere "
                 "Stände im Verlauf des Abgleich-Repositorys auf GitHub enthalten die Eingaben weiterhin.")
_OHNE_ABGLEICH = ("„%s“ löschen? Das Projekt und alle seine Eingaben werden gelöscht. "
                  "Das lässt sich in der App nicht rückgängig machen.\n\nHinweis: Ältere "
                  "Sicherungsdateien enthalten die Eingaben weiterhin. Falls du den Abgleich früher "
                  "genutzt hast, enthalten frühere Stände im Abgleich-Repository auf GitHub die "
                  "Eingaben weiterhin.")


def rueckfrage():
    aus = []
    for t in ("X", "", " \U0001F600 "):
        for mit in (True, False):
            aus.append([t, mit, (_MIT_ABGLEICH if mit else _OHNE_ABGLEICH) % titel_von({"titel": t})])
    return aus

# ---------------------------------------------------------------------------
# Abschnitt 7: PDF
# ---------------------------------------------------------------------------

_W_REGULAR = [
    278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
    556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
    1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
    667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
    333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
    556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584, 761,
    556, 0, 222, 556, 333, 1000, 556, 556, 333, 1000, 667, 333, 1000, 0, 611, 0,
    0, 222, 222, 333, 333, 350, 556, 1000, 333, 1000, 500, 333, 944, 0, 500, 667,
    278, 333, 556, 556, 556, 556, 260, 556, 333, 737, 370, 556, 584, 333, 737, 333,
    400, 584, 333, 333, 333, 556, 537, 278, 333, 333, 365, 556, 834, 834, 834, 611,
    667, 667, 667, 667, 667, 667, 1000, 722, 667, 667, 667, 667, 278, 278, 278, 278,
    722, 722, 778, 778, 778, 778, 778, 584, 778, 722, 722, 722, 722, 667, 667, 611,
    556, 556, 556, 556, 556, 556, 889, 500, 556, 556, 556, 556, 278, 278, 278, 278,
    556, 556, 556, 556, 556, 556, 556, 584, 611, 556, 556, 556, 556, 500, 556, 500,
]
_W_BOLD = [
    278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
    556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611,
    975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
    667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556,
    333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
    611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584, 761,
    556, 0, 278, 556, 500, 1000, 556, 556, 333, 1000, 667, 333, 1000, 0, 611, 0,
    0, 278, 278, 500, 500, 350, 556, 1000, 333, 1000, 556, 333, 944, 0, 500, 667,
    278, 333, 556, 556, 556, 556, 280, 556, 333, 737, 370, 556, 584, 333, 737, 333,
    400, 584, 333, 333, 333, 611, 556, 278, 333, 333, 365, 556, 834, 834, 834, 611,
    722, 722, 722, 722, 722, 722, 1000, 722, 667, 667, 667, 667, 278, 278, 278, 278,
    722, 722, 778, 778, 778, 778, 778, 584, 778, 722, 722, 722, 722, 667, 667, 611,
    556, 556, 556, 556, 556, 556, 889, 556, 556, 556, 556, 556, 278, 278, 278, 278,
    611, 611, 611, 611, 611, 611, 611, 584, 611, 611, 611, 611, 611, 556, 611, 556,
]
assert len(_W_REGULAR) == 224 and len(_W_BOLD) == 224
BREITEN = {"Regular": _W_REGULAR, "Bold": _W_BOLD}

_REPLACE = {
    0x2192: "->", 0x2190: "<-", 0x2264: "<=", 0x2265: ">=", 0x2713: "x", 0x2717: "-",
    0x2022: "•", 0x00A0: " ", 0x2011: "-", 0x202F: " ", 0x2212: "-", 0x221A: "Wurzel ",
    0x2248: "~", 0x21D2: "=>", 0x21D0: "<=", 0x21D4: "<=>", 0x2194: "<->", 0x27F6: "->",
    0x27F5: "<-", 0x2260: "!=", 0x2714: "OK", 0x2705: "OK", 0x2718: "-", 0x274C: "-",
    0x0394: "Delta", 0x03A9: "Ohm", 0x2126: "Ohm", 0x03BC: "µ", 0x03C0: "pi",
    0x221E: "unendlich", 0x2044: "/", 0x2032: "'", 0x2033: "\"", 0x2009: " ", 0x2002: " ",
    0x2003: " ", 0x2007: " ", 0x2008: " ", 0x200A: " ", 0x200B: "", 0xFEFF: "",
    0x0141: "L", 0x0142: "l",
}
_ERSATZ = {chr(k): v for k, v in _REPLACE.items()}
_LEER_KATEGORIEN = {"So", "Sk", "Cf", "Cs", "Co", "Mn", "Me"}


def _geht_cp1252(t):
    try:
        t.encode("cp1252")
        return True
    except UnicodeEncodeError:
        return False


def ersatz(c):
    if ord(c) > 0xFFFF or unicodedata.category(c) in _LEER_KATEGORIEN:
        return ""
    zerlegt = "".join(z for z in unicodedata.normalize("NFKD", c) if not unicodedata.combining(z))
    if _geht_cp1252(zerlegt):
        return zerlegt or "?"
    return "?"


def bereinige(t):
    aus = []
    for c in unicodedata.normalize("NFC", str(t)):
        r = _ERSATZ.get(c, c)
        if not _geht_cp1252(r):
            r = ersatz(c)
        aus.append(r)
    return "".join(aus)


def kodiere(t):
    return bereinige(t).encode("cp1252", errors="replace")


def breite(t, schrift, groesse):
    w = BREITEN[schrift]
    s = 0
    for b in kodiere(t):
        if 32 <= b <= 255 and w[b - 32] != 0:
            s += w[b - 32]
        else:
            s += 556
    return s * groesse / 1000.0


def umbrich(t, schrift, groesse, b):
    aus = []
    for absatz in str(t).split("\n"):
        zeile = ""
        for wort in absatz.split(" "):
            kandidat = wort if zeile == "" else zeile + " " + wort
            if breite(kandidat, schrift, groesse) <= b:
                zeile = kandidat
                continue
            if zeile != "":
                aus.append(zeile)
            while breite(wort, schrift, groesse) > b and len(wort) > 1:
                n = 1
                for m in range(len(wort), 0, -1):
                    if breite(wort[:m], schrift, groesse) <= b:
                        n = m
                        break
                aus.append(wort[:n])
                wort = wort[n:]
            zeile = wort
        aus.append(zeile)
    return aus


STILE = {
    "h1": ("Bold", 18, 6, 10, 0),
    "h2": ("Bold", 13, 12, 6, 0),
    "h3": ("Bold", 11, 8, 3, 0),
    "p": ("Regular", 10.5, 0, 6, 0),
    "li": ("Regular", 10.5, 0, 3, 14),
}
FONT = {"Regular": b"F1", "Bold": b"F2"}
SEITE_B = 595.28
SEITE_H = 841.89
RAND = 56
FUSS = 30
NUTZBAR = SEITE_B - 112


def esc(b):
    return b.replace(b"\\", b"\\\\").replace(b"(", b"\\(").replace(b")", b"\\)")


def build_pdf(bl, titel, autor="", datum="14.10.2026"):
    seiten_ops = [[]]
    oben = SEITE_H - RAND
    y = oben
    for art, text in bl:
        schrift, groesse, davor, danach, einzug = STILE[art]
        zeilenabstand = groesse * 1.35
        zeilen = umbrich(text, schrift, groesse, NUTZBAR - einzug) if text else [""]
        y -= davor
        bedarf = zeilenabstand * (2 if art.startswith("h") else 1)
        for i, zeile in enumerate(zeilen):
            if y - bedarf < RAND + FUSS:
                seiten_ops.append([])
                y = oben
            bedarf = zeilenabstand
            y -= zeilenabstand
            ops = seiten_ops[-1]
            if art == "li" and i == 0:
                ops.append(b"BT /F1 %.1f Tf %.2f %.2f Td (%s) Tj ET"
                           % (groesse, RAND + 3, y, esc(kodiere("•"))))
            ops.append(b"BT /%s %.1f Tf %.2f %.2f Td (%s) Tj ET"
                       % (FONT[schrift], groesse, RAND + einzug, y, esc(kodiere(zeile))))
        y -= danach
    n_seiten = len(seiten_ops)
    for n, ops in enumerate(seiten_ops, start=1):
        fuss = ("%s  ·  Seite %d von %d  ·  %s" % (titel, n, n_seiten, datum)).strip(" ·")
        ops.append(b"0.45 g BT /F1 8 Tf %.2f %.2f Td (%s) Tj ET 0 g" % (RAND, 36, esc(kodiere(fuss))))

    objekte = []
    objekte.append(b"<< /Type /Catalog /Pages 2 0 R >>")
    seiten_nummern = [5 + 2 * i + 1 for i in range(n_seiten)]
    kids = b" ".join(b"%d 0 R" % k for k in seiten_nummern)
    objekte.append(b"<< /Type /Pages /Kids [%s] /Count %d >>" % (kids, n_seiten))
    objekte.append(b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>")
    objekte.append(b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>")
    ohne_strom = [o for o in objekte]
    inhalte = []
    for i, ops in enumerate(seiten_ops):
        roh = b"\n".join(ops)
        inhalte.append(roh.decode("latin-1"))
        gepackt = zlib.compress(roh)
        kopf = b"<< /Length %d /Filter /FlateDecode >>" % len(gepackt)
        objekte.append(kopf + b"\nstream\n" + gepackt + b"\nendstream")
        ohne_strom.append(b"<< /Length # /Filter /FlateDecode >>")
        c = 5 + 2 * i
        seite = (b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Resources << /Font "
                 b"<< /F1 3 0 R /F2 4 0 R >> >> /Contents %d 0 R >>" % c)
        objekte.append(seite)
        ohne_strom.append(seite)
    info = (b"<< /Title <FEFF%s> /Author <FEFF%s> /Producer (Fachinformatiker Lernplattform) >>"
            % (titel.encode("utf-16-be").hex().upper().encode("ascii"),
               autor.encode("utf-16-be").hex().upper().encode("ascii")))
    objekte.append(info)
    ohne_strom.append(info)

    datei = bytearray(b"%PDF-1.4\n%\xe2\xe3\xcf\xd3\n")
    stellen = []
    for nr, o in enumerate(objekte, start=1):
        stellen.append(len(datei))
        datei += b"%d 0 obj\n%s\nendobj\n" % (nr, o)
    xref_stelle = len(datei)
    m = len(objekte) + 1
    datei += b"xref\n0 %d\n0000000000 65535 f \n" % m
    for s in stellen:
        datei += b"%010d 00000 n \n" % s
    datei += (b"trailer\n<< /Size %d /Root 1 0 R /Info %d 0 R >>\nstartxref\n%d\n%%%%EOF\n"
              % (m, len(objekte), xref_stelle))
    return bytes(datei), {
        "seiten": n_seiten,
        "inhalte": inhalte,
        "objekte": [o.decode("latin-1") for o in ohne_strom],
        "kopf": bytes(datei[:15]).hex(),
        "info": info.decode("latin-1"),
    }


def pdf_teil(faelle):
    aus = []
    for name, felder in faelle:
        if name not in ("leer", "festwert", "unicode", "lang"):
            continue
        fs = dict(felder)
        _datei, werte = build_pdf(bloecke(fs), titel_von(fs))
        aus.append([name, werte])
    return aus

# ---------------------------------------------------------------------------
# Abschnitt 7a: Texte
# ---------------------------------------------------------------------------


def texte():
    return {
        "reiter": [list(x) for x in REITER],
        "uebersicht": [list(x) for x in UEBERSICHT],
        "antrag": [list(x) for x in ANTRAG],
        "phasen": [list(x) for x in PHASEN],
        "kosten": [list(x) for x in KOSTEN],
        "kapitel": [list(x) for x in KAPITEL],
        "checklisten": [[list(x) for x in liste]
                        for liste in (CHECK_UEBERSICHT, check_antrag(MAX_STUNDEN), CHECK_PRAESENTATION)],
        "zahlen": {"max_stunden": MAX_STUNDEN, "doku_anteil": DOKU_ANTEIL,
                   "zeichen_je_seite": ZEICHEN_JE_SEITE, "seiten": SEITEN_VORGABE,
                   "sachkosten_zeilen": SACHKOSTEN_ZEILEN},
        "rueckfrage_titel": RUECKFRAGE_TITEL,
    }

# ---------------------------------------------------------------------------
# Nachtrag P4-9 N2: grenze
# ---------------------------------------------------------------------------


def grenze():
    aus = []
    for max_stunden in (40, 80):
        for summe_soll in (30, 40, 41, 80, 81):
            felder = [("phase_durchfuehrung", str(summe_soll - 26))]
            fs = dict(felder)
            bl = bloecke(fs, max_stunden)
            aus.append([max_stunden, summe_soll, {
                "felder": [[f, w] for f, w in felder],
                "summe": summe_von(zeitplan(fs)),
                "warnungen": warnungen(fs, max_stunden),
                "bloecke": bl,
                "text": text_aus(bl),
            }])
    return aus

# ---------------------------------------------------------------------------
# Hauptteil
# ---------------------------------------------------------------------------


def main():
    if len(sys.argv) != 3:
        sys.stderr.write("Aufruf: pruefe_abschlussprojekt.py <inhalte-ordner> <ausgabe.json>\n")
        return 2
    ordner, ziel = sys.argv[1], sys.argv[2]
    with open(os.path.join(ordner, "fachgespraech.json"), encoding="utf-8") as f:
        fachgespraech = json.load(f)
    with open(os.path.join(ordner, "projektarbeiten.json"), encoding="utf-8") as f:
        projektarbeiten = json.load(f)
    faelle = baue_faelle()
    ausgabe = {
        "zahl": raster_zahl(),
        "rechnen": rechnen(faelle),
        "export": export(faelle),
        "projekte": projekte_ablauf(projektarbeiten),
        "fragen": fragen_teil(fachgespraech),
        "rueckfrage": rueckfrage(),
        "pdf": pdf_teil(faelle),
        "texte": texte(),
        "grenze": grenze(),
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as f:
        json.dump(ausgabe, f, ensure_ascii=False, separators=(",", ":"))
        f.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
