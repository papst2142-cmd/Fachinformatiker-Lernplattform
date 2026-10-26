#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G24 (Phase 3, Welle 1, Paket R1 "Zahlen und Zeilen"),
gerechnet vom echten Python-Code der Python-Linie (v0.63.2, eingefroren):

  zeichen   - Pythons Textregeln je Zeichen (alle Codepunkte ausser U+D800-U+DFFF):
              isdigit, isdecimal (mit Wert), isspace, Zeilenenden von splitlines,
              casefold (nur Zeichen, die sich aendern).
  texte     - str.split(), strip(), splitlines(), casefold(), len() und int() fuer
              feste Texte (Emoji, "\\x1c", "²", fremde Ziffern, "_").
  lesen     - read_number(text, "Feld") und read_number(text, "Feld", ganz=True)
              mit Ergebnis (Kommazahl als 64-Bit-Muster, ganze Zahl als Text) oder
              Meldung, dazu _thousands_point_lines(text). Faelle: alle kurzen
              Texte aus den Python-Tests der Rechner und feste Grenzfaelle.
  zahltext  - de_number(x, n) fuer n = 0 bis 4, _de_short(x, n), de_int(x),
              plural(n, "Strang", "Stränge").
  kasten    - fit_lines(text, zeichen, einheitlich) fuer feste Ergebnistexte und
              lange Texte aus den Python-Tests (mit ": " oder Zeilen), Breiten 10 bis 75.
  helfer    - _fit_lines, _wrap_para, _join_items, _pc_table, _handy_address.

Es werden keine Lerndaten benutzt; alle Daten- und Einstellungsordner zeigen
in einen leeren Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_rechner_zahlen.py <pfad_fisi_lernplattform> tests/Vergleich/rechner_zahlen.json
"""
import ast
import atexit
import json
import os
import shutil
import struct
import sys
import tempfile
import unicodedata

# Testdateien der Python-Linie zum Rechner (Plan Phase 3, 2.1) und zum Umbruch
TESTDATEIEN = [
    "test_rechner_0632.py", "test_rechner_bedarf.py", "test_rechner_pc_0632.py",
    "test_rechner_seite_breite.py", "test_subnetz_aufteilen.py", "test_subnetz_handy.py",
    "test_subnetz_pc.py", "test_subnetz_pc_breite.py", "test_usv.py", "test_rechenweg.py",
    "test_trainer_breite_0632.py", "test_trainer_nibble.py", "test_trainer_richtig_waere_pc.py",
    "test_ipv6_details.py", "test_tausenderpunkt.py", "test_zahl_einheit_0632.py",
    "test_anzeige_rfc.py", "test_anzeige_handy.py", "test_handy_anzeige_0632.py",
    "test_umbruch_bindung.py",
]

LESEN_FEST = [
    "", " ", "0", "-0", "-0,0", "1", "-1", "1,5", "1.5", "0.125", ",5", ".5", "5,", "5.",
    "1.000", "01.000", "001.000", "0.000", "12.500", "1.000.000", "1.000,5", "1,000.5",
    "1.00,5", "1.000.5", "1.2.3", "1 000", "1 000", "1 000", "1 000,5", "1  000",
    "1e3", "1E3", "1_000", "inf", "nan", "-inf", "١٠٠٠", "²", "1²", "12😀", "😀", "\x1c5\x1c",
    "　5　", "\t7\n", "1,0000", "1,000", "1,234", "23.976", "23,976", "29,97",
    "99999999999999999999", "999999999999999999999", "9" * 400, "-99999999999999999999",
    "1.000.000.000.000.000", "18.446.744.073.709.551.616", "9.007.199.254.740.993",
    "+5", "--5", "-", ",", ".", "1,,5", "1..5", "0x10", "1/2", "١", "٣.٥", "1.0000",
    "1000000,001", "0,0001", "0,001", "4,5e", " 1", "5 ", "1 000 000",
]

ZAHLEN_FEST = [
    0.0, -0.0, 0.5, 1.5, 2.5, 0.125, 0.005, 0.015, 0.045, 1.005, 2.675, 390.625, 1.0 / 3, 2.0 / 3,
    -0.001, -0.005, -0.004, -1.5, -2.675, 1e15, 1e16, 1e20, 1e22, 1e300, 1.7976931348623157e308,
    5e-324, 1e-7, 123456.789, 999.995, 999.994999, 0.9, 1.25, 12.345, 100.0, 1234567.891,
    float("inf"), float("-inf"), float("nan"), 29.97, 23.976, 666.6666666666666, 88.88888888888889,
    9007199254740993.0, 0.1 + 0.2, 1e-5, 4.35, 8.345, 10.5, 11.5, 1.0e6, 75.0, 80.0,
]

GANZ_FEST = [0, 1, -1, 12, 999, 1000, -1000, 1234567, 2 ** 32, 2 ** 53 + 1, 2 ** 64, 2 ** 127,
             2 ** 128, 10 ** 30, -(10 ** 20)]

TEXTE_FEST = ["", " a  b ", "\x1ca\x1db\x1e", "a b c", "a\r\nb\rc\nd\x0be\x0cf\x85g",
              "x\n", "\n", "😀abc", "ab😀cd", "²³¹", "١٢٣", "1_000", "_1", "1__0", "+7", " -7 ",
              "0x10", "ß", "ẞ", "STRASSE", "İ", "Σ", "ﬁ", "Å", "ǅ", "ΣΑΣ", "Netz 1", "NETZ 1",
              "　x　", "\xa0y\xa0", "z​z", "²", "٣", "𝟓", "Ⅻ"]

BREITEN = [10, 16, 20, 24, 28, 40, 75]


def bits(wert):
    return struct.unpack("<q", struct.pack("<d", wert))[0]


def literale(repo):
    texte = []
    for name in TESTDATEIEN:
        with open(os.path.join(repo, name), encoding="utf-8") as handle:
            baum = ast.parse(handle.read())
        for knoten in ast.walk(baum):
            if isinstance(knoten, ast.Constant) and isinstance(knoten.value, str):
                texte.append(knoten.value)
    gesehen, ergebnis = set(), []
    for text in texte:
        if text not in gesehen:
            gesehen.add(text)
            ergebnis.append(text)
    return ergebnis


def bereiche(werte):
    ergebnis = []
    for wert in werte:
        if ergebnis and ergebnis[-1][1] == wert - 1:
            ergebnis[-1][1] = wert
        else:
            ergebnis.append([wert, wert])
    return ergebnis


def versuch(aufruf, fehlerart):
    try:
        return {"wert": aufruf()}
    except fehlerart as fehler:
        return {"fehler": str(fehler)}


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
    import fisi_core as fc

    alle = [chr(c) for c in range(0x110000) if not 0xD800 <= c < 0xE000]
    zeichen = {
        "isdigit": bereiche([ord(c) for c in alle if c.isdigit()]),
        "isdecimal": [[ord(c), int(c)] for c in alle if c.isdecimal()],
        "isspace": [ord(c) for c in alle if c.isspace()],
        "zeilenende": [ord(c) for c in alle if len(("a" + c + "b").splitlines()) > 1],
        "casefold": [[ord(c), c.casefold()] for c in alle if c.casefold() != c],
    }

    def int_text(text):
        try:
            return str(int(text))
        except ValueError:
            return None

    texte = [{"text": t, "split": t.split(), "strip": t.strip(), "splitlines": t.splitlines(),
              "casefold": t.casefold(), "len": len(t), "isdigit": t.isdigit(), "int": int_text(t)}
             for t in TEXTE_FEST]

    kurz = [t for t in literale(repo) if len(t) <= 40 and "\n" not in t]
    faelle = []
    for text in LESEN_FEST + kurz:
        if text in faelle:
            continue
        faelle.append(text)
    lesen = []
    for text in faelle:
        komma = versuch(lambda: bits(fc.read_number(text, "Feld")), fc.InputError)
        ganz = versuch(lambda: str(fc.read_number(text, "Feld", ganz=True)), fc.InputError)
        lesen.append({"text": text, "komma": komma, "ganz": ganz,
                      "hinweis": fc._thousands_point_lines(text)})
    lesen.append({"text": None, "komma": versuch(lambda: bits(fc.read_number(None, "Feld")), fc.InputError),
                  "ganz": versuch(lambda: str(fc.read_number(None, "Feld", ganz=True)), fc.InputError),
                  "hinweis": fc._thousands_point_lines(None)})
    mehrere = [["1.000", "0.5"], ["0.5", "1 000"], ["-1.000,5"], ["", None, "7.200"], ["1.00"]]
    hinweise = [[liste, fc._thousands_point_lines(*liste)] for liste in mehrere]

    zahlen = list(ZAHLEN_FEST)
    for eintrag in lesen:
        if "wert" in eintrag["komma"]:
            zahlen.append(struct.unpack("<d", struct.pack("<q", eintrag["komma"]["wert"]))[0])
    zahltext = []
    gesehen = set()
    for wert in zahlen:
        if bits(wert) in gesehen:
            continue
        gesehen.add(bits(wert))
        zahltext.append({"bits": bits(wert),
                         "de_number": [fc.de_number(wert, n) for n in range(5)],
                         "de_short": [fc._de_short(wert, n) for n in range(5)],
                         "de_int": fc.de_int(wert) if wert == wert and abs(wert) != float("inf") else None})
    ganzzahlen = [{"wert": str(n), "de_int": fc.de_int(n), "plural": fc.plural(n, "Strang", "Stränge")}
                  for n in GANZ_FEST]

    # Kasten: feste Ergebnisse und lange Texte aus den Tests
    quellen = [fc.CALC_EXPLAIN_SUBNET, fc.CALC_EXPLAIN_SUBNET_V6, fc.CALC_EXPLAIN_RAID,
               fc.CALC_EXPLAIN_SCREEN, fc.CALC_EXPLAIN_UPS, fc.UPS_RULE_TEXT, fc.UPS_SIMPLE_TEXT,
               fc.raid_report("RAID 10", "1000", "1000000"), fc.raid_report("RAID 5", "4", "1.000"),
               fc.screen_report("3840", "2160", "32", "29,97"),
               fc.ups_calculate("empfehlung", fc.UPS_FIELD_DEFAULTS)["text"],
               fc.subnet_report("2001:db8::1/64"), fc.subnet_report("10.0.0.0/8"),
               "Noch keine Berechnung durchgeführt.",
               "Hinweis: An der nächsten 4-Bit-Grenze (Nibble): /44 = 4.096 Subnetze. In der Praxis",
               "Adresstyp             : Dokumentation (2001:db8::/32, RFC 3849)",
               "Ausgeschrieben        : 2001:0db8:0000:0000:0000:0000:0000:0001/64",
               "Wert                  : 1.234.567.890,12 Wh",
               "Wert                  : 1.234.567,12 Wh und noch mehr Text = hex 80 -> 7",
               "Wert                  : 18.446.744.073.709.551.616",
               "Zeile ohne Doppelpunkt aber mit sehr vielen Woertern = hex ff → 255 und RFC 3021",
               "  : nur Folgezeile ohne Kopf mit langem Wert 340.282.366.920.938.463.463",
               "Tabelle  mit  Spalten  bleibt  wie  sie  ist  auch  wenn  sie  zu  lang  ist",
               "😀😀😀 Emoji : 😀😀😀😀😀😀😀😀😀😀😀😀😀😀 😀😀😀😀😀😀",
               "Name: x\nName : y\n    Netz : fe80:0000:0000:0000:abcd:ef01:2345:6789\n    : 2001:db8::\nX: \n: leer"]
    quellen += [t for t in literale(repo) if len(t) > 30 and (": " in t or "\n" in t)]
    kasten = []
    for text in quellen:
        eintrag = {"text": text, "breiten": {}}
        for breite in BREITEN:
            werte = []
            for einheitlich in (False, True):
                try:
                    werte.append(fc.fit_lines(text, breite, einheitlich=einheitlich))
                except ValueError as fehler:
                    werte.append({"fehler": type(fehler).__name__})
            eintrag["breiten"][str(breite)] = werte
        kasten.append(eintrag)

    zeilen = [t for t in quellen if "\n" not in t] + [z for t in quellen for z in t.split("\n")][:400]
    fit = []
    for zeile in zeilen[:600]:
        # nur die Breiten der Python-Linie: bei schmaler Breite und grossem Einzug haengt
        # _fit_line in Python (die Folgezeile wird nie kuerzer)
        fit.append({"zeile": zeile, "28": fc._fit_lines([zeile], 28), "75": fc._fit_lines([zeile], 75)})
    absatz = [[text, lead, breite, fc._wrap_para(text, lead, breite)]
              for text in (fc.SUBNET_NO_SPLIT, fc.SUBNET_EXPLAIN_ANYCAST, fc.SUBNET_HINT_P2P,
                           fc.SUBNET_EXPLAIN_BOOKS, "Wort" * 30, "a b")
              for lead in ("", " ", "   ") for breite in (28, 75)]
    aufzaehlen = [[erster, items, kleber, mehr, breite, fc._join_items(erster, items, kleber, mehr, breite)]
                  for erster, items, kleber, mehr in (
                      ("Bedarf: ", ["A: 50 Hosts", "B: 20 Hosts", "C: 10 Hosts", "Verwaltung: 1 Host"], ", ", "        "),
                      ("   Summe der Größen: ", ["64", "32", "16", "4", "4", "4", "4", "1.024", "65.536"], " + ", "      "),
                      (" ", ["A: 50"], ", ", " "),
                      (" Summe: ", ["64", "32"], " + ", "  "))
                  for breite in (28, 40, 75)]
    tabellen = []
    for koepfe, reihen, breiten in (
            (["Nr", "Netzadresse", "Erster Host", "Letzter Host", "Broadcast"],
             [["1", "192.168.10.0/27", "192.168.10.1", "192.168.10.30", "192.168.10.31"]], [2, 18, 15, 15]),
            (["Nr", "Name", "Netzadresse", "Erster Host", "Letzter Host", "Broadcast"],
             [["1", "Sehr langer Name", "10.0.0.0/8", "10.0.0.1", "10.255.255.254", "10.255.255.255"]],
             [2, 16, 18, 15, 15]),
            (["A", "B"], [["😀😀", "x"]], [1])):
        tabellen.append([koepfe, reihen, breiten, fc._pc_table(koepfe, reihen, breiten)])
    adressen = [[b, a, fc._handy_address(b, a)]
                for b in (" Netz: ", " bis: ", " ", " Nr. 65.536: ", "Sehr lange Beschriftung: ")
                for a in ("2001:db8:abcd:12::/64", "2001:db8:abcd:ffff:ffff:ffff:ffff:ffff",
                          "fe80::/10", "192.168.10.0/24", "1234:5678:9abc:def0:1234:5678:9abc:def0/128", "a:b")]

    daten = {
        "quelle": "fisi_core read_number, _thousands_point_lines, de_number, de_int, _de_short, plural, "
                  "fit_lines, _fit_lines, _wrap_para, _join_items, _pc_table, _handy_address (Python v0.63.2)",
        "python": sys.version.split()[0],
        "unicode": unicodedata.unidata_version,
        "zeichen": zeichen,
        "texte": texte,
        "lesen": lesen,
        "hinweise": hinweise,
        "zahltext": zahltext,
        "ganzzahlen": ganzzahlen,
        "kasten": kasten,
        "fit": fit,
        "absatz": absatz,
        "aufzaehlen": aufzaehlen,
        "tabellen": tabellen,
        "adressen": adressen,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G24: %d Lesefaelle, %d Zahlen, %d Kaesten, %d Zeilen -> %s"
          % (len(lesen), len(zahltext), len(kasten), len(fit), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
