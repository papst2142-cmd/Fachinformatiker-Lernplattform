#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G26 (Phase 3, Welle 1, Paket R3 "RAID, Bildschirm, USV"),
gerechnet vom echten Python-Code der Python-Linie (v0.63.2, eingefroren) mit Python 3.12:

  raid       - raid_report(level, disks_text, size_text) fuer alle Level, Grenzen und
               Fehler; raid_values, raid_min_disks, raid_input_problem,
               raid_tolerance_lines fuer feste Plattenzahlen.
  bildschirm - screen_report(width, height, depth, fps) (Farbtiefen, fps leer, Komma,
               "23.976", zu gross, Fehler).
  usv        - ups_calculate(mode, fields, unit) fuer alle Modi und Einheiten, jede
               Feldgrenze und Fehleingabe, feste Zufallswerte; Text, Bild (Kommazahlen
               als 64-Bit-Muster), passend und Vorlesetext ups_picture_summary.
  aufgaben   - ups_task_text(i) und ups_task_text(i, True) fuer i = -7 bis 12,
               ups_strings_needed fuer feste Werte.
  rechenwege - die festen Rechenwege CALC_EXPLAIN_* (PC und Handy).
  oberflaeche - Oberflaechentexte der Rechner-Seite aus app_gui.py (TrainerPanel bis
               CalcView, Zeilen 5490-6567) und mobile/src/main.py (TrainerPanel bis
               CalcScreen, Zeilen 2950-3581) mit allen Unterschieden PC gegen Handy
               (Weisung P3-2 A2). Handy-Ueberschriften (ui.label) sind Grossbuchstaben.

Es werden keine Lerndaten benutzt; alle Daten- und Einstellungsordner zeigen
in einen leeren Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_rechner.py <pfad_fisi_lernplattform> tests/Vergleich/rechner.json
"""
import ast
import json
import os
import random
import re
import struct
import sys
import tempfile

PC_BEREICH = ("app_gui.py", 5490, 6567)
HANDY_BEREICH = ("mobile/src/main.py", 2950, 3581)


def bits(wert):
    return str(struct.unpack("<q", struct.pack("<d", wert))[0])


def zahlen_als_bits(wert):
    """Kommazahlen als 64-Bit-Muster ("f:<bits>"), ganze Zahlen und Texte bleiben."""
    if isinstance(wert, float):
        return "f:" + bits(wert)
    if isinstance(wert, dict):
        return {k: zahlen_als_bits(v) for k, v in wert.items()}
    if isinstance(wert, (list, tuple)):
        return [zahlen_als_bits(v) for v in wert]
    return wert


def oberflaechentexte(repo, datei, von, bis, handy):
    """Sichtbare Texte (Grossbuchstabe, Nicht-ASCII oder Leerzeichen) ohne Docstrings
    und ohne Tk-Ereignisnamen."""
    with open(os.path.join(repo, datei), encoding="utf-8") as handle:
        baum = ast.parse(handle.read())
    doc = set()
    for knoten in ast.walk(baum):
        if isinstance(knoten, (ast.FunctionDef, ast.ClassDef, ast.Module)) and knoten.body \
                and isinstance(knoten.body[0], ast.Expr) and isinstance(knoten.body[0].value, ast.Constant):
            doc.add(id(knoten.body[0].value))
    gross = set()
    if handy:
        # ui.label(text) zeigt text.upper()
        for knoten in ast.walk(baum):
            if isinstance(knoten, ast.Call) and isinstance(knoten.func, ast.Attribute) \
                    and knoten.func.attr == "label" and isinstance(knoten.func.value, ast.Name) \
                    and knoten.func.value.id == "ui" and knoten.args and isinstance(knoten.args[0], ast.Constant):
                gross.add(id(knoten.args[0]))
    texte = []
    for knoten in ast.walk(baum):
        if isinstance(knoten, ast.Constant) and isinstance(knoten.value, str) and von <= knoten.lineno <= bis \
                and id(knoten) not in doc:
            wert = knoten.value
            if re.fullmatch(r"<[A-Za-z-]+>", wert):
                continue        # Tk-Ereignisnamen wie "<Return>", nicht sichtbar
            if re.search(r"[A-ZÄÖÜ]", wert) or re.search(r"[^\x00-\x7f]", wert) or " " in wert.strip():
                texte.append([knoten.lineno, wert.upper() if id(knoten) in gross else wert])
    return sorted(texte)


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as fc

    def versuch(aufruf):
        try:
            return {"wert": aufruf()}
        except fc.InputError as fehler:
            return {"fehler": str(fehler)}

    # --- RAID ---
    raid = []
    platten = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "16", "1.000", "1000", "1001", "-1", "abc", "",
               "2,5", "1 000", "4,0", " 4 ", "٤"]
    groessen = ["1000", "0,5", "0,01", "0,009", "1.000.000", "1000000", "1000001", "0", "abc", "1,5", "2.000",
                "3,333", "0,015", "999999,999", "1.234,5", "-1", ""]
    for level in fc.RAID_LEVELS:
        for p in platten:
            for g in groessen:
                raid.append([level, p, g, versuch(lambda: fc.raid_report(level, p, g))])
    zufall = random.Random(26)
    for _ in range(300):
        level = zufall.choice(fc.RAID_LEVELS)
        p = str(zufall.randint(1, 40))
        g = ("%.3f" % (zufall.random() * zufall.choice([1, 100, 10000, 1000000]))).replace(".", ",")
        raid.append([level, p, g, versuch(lambda: fc.raid_report(level, p, g))])
    raid_werte = []
    for level in fc.RAID_LEVELS:
        for p in range(0, 13):
            for g in (1.0, 2.5, 1000.0, 0.01, 3.3):
                werte = fc.raid_values(level, p, g)
                raid_werte.append([level, p, bits(g), None if werte is None else zahlen_als_bits(werte),
                                   fc.raid_min_disks(level), fc.raid_input_problem(level, p),
                                   None if werte is None else fc.raid_tolerance_lines(level, p, werte["toleranz"])])

    # --- Bildschirm ---
    bildschirm = []
    fps_faelle = ["", " ", "30", "29,97", "23.976", "23,976", "1000", "1000,5", "1.000", "-1", "0", "abc", "60",
                  "0,001", "999,9999", "1,0005", "144"]
    tiefen = ["8", "16", "24", "32", "1", "64", "0", "65", "abc", " 24 ", "+24", "2_4", "٢٤", "", "24,0", "-8"]
    masse = ["1920", "0", "100.000", "100000", "100001", "abc", "", "1.920", "-5", "1", "3840", "1,5"]
    for fps in fps_faelle:
        bildschirm.append(["1920", "1080", "24", fps])
    for tiefe in tiefen:
        for fps in ("", "60", "23.976"):
            bildschirm.append(["1920", "1080", tiefe, fps])
    for b in masse:
        for h in masse:
            bildschirm.append([b, h, "24", "30"])
    for _ in range(200):
        bildschirm.append([str(zufall.randint(1, 100000)), str(zufall.randint(1, 100000)),
                           zufall.choice(["8", "16", "24", "32", "1", "64", "12"]),
                           zufall.choice(["", str(zufall.randint(1, 1000)),
                                          ("%.3f" % (zufall.random() * 240)).replace(".", ",")])])
    bildschirm = [[b, h, t, f, versuch(lambda: fc.screen_report(b, h, t, f))] for b, h, t, f in bildschirm]

    # --- USV ---
    def usv_eintrag(modus, felder, einheit):
        eintrag = {"modus": modus, "felder": felder, "einheit": einheit}
        try:
            r = fc.ups_calculate(modus, felder, einheit)
        except fc.InputError as fehler:
            eintrag["fehler"] = str(fehler)
            return eintrag
        eintrag["text"] = r["text"]
        eintrag["bild"] = zahlen_als_bits(r["bild"])
        eintrag["passend"] = r["passend"]
        eintrag["vorlesen"] = fc.ups_picture_summary(r["bild"])
        return eintrag

    vorgabe = dict(fc.UPS_FIELD_DEFAULTS)
    usv = []
    for modus in ("akku", "laufzeit", "empfehlung", "unbekannt"):
        for einheit in ("W", "VA", "kW"):
            usv.append(usv_eintrag(modus, dict(vorgabe), einheit))
    feldwerte = ["", " ", "abc", "0", "-1", "0,1", "0,09", "1", "1,5", "10", "9,9", "12", "20", "21", "24", "48",
                 "80", "99,9", "100", "100,1", "101", "1.000", "1440", "1441", "10000", "10001", "1.000.000",
                 "1000001", "2.500,5", "0,5"]
    for schluessel in fc.UPS_FIELD_DEFAULTS:
        for wert in feldwerte:
            felder = dict(vorgabe)
            felder[schluessel] = wert
            usv.append(usv_eintrag("empfehlung", felder, "W"))
        felder = dict(vorgabe)
        del felder[schluessel]
        usv.append(usv_eintrag("empfehlung", felder, "VA"))
    for modus in ("akku", "laufzeit"):
        for schluessel in ("last", "minuten", "alterung", "block_ah", "nenn_va"):
            for wert in ("abc", "0", "1.000", "7,5"):
                felder = dict(vorgabe)
                felder[schluessel] = wert
                usv.append(usv_eintrag(modus, felder, "VA"))
    for _ in range(300):
        felder = {
            "last": str(zufall.choice([zufall.randint(1, 5000), zufall.randint(1, 1000000)])),
            "pf": ("%.2f" % zufall.uniform(0.1, 1)).replace(".", ","),
            "eta": str(zufall.randint(10, 100)),
            "minuten": str(zufall.randint(1, 1440)),
            "alterung": str(zufall.choice([0, 10, 25, 50, zufall.randint(0, 100)])),
            "block_v": str(zufall.choice([2, 6, 12])),
            "block_ah": ("%.1f" % zufall.uniform(1, 200)).replace(".", ","),
            "reihe": str(zufall.randint(1, 40)),
            "parallel": str(zufall.randint(1, 20)),
            "nenn_va": str(zufall.choice([500, 1000, 1500, 3000, 10000, zufall.randint(100, 1000000)])),
            "nenn_w": zufall.choice(["", str(zufall.randint(100, 100000))]),
        }
        usv.append(usv_eintrag(zufall.choice(["akku", "laufzeit", "empfehlung"]), felder, zufall.choice(["W", "VA"])))

    aufgaben = [[i, fc.ups_task_text(i), fc.ups_task_text(i, True)] for i in range(-7, 13)]
    straenge = []
    for bedarf, strang in ((1250.0, 288), (288.0, 288), (288.0000001, 288), (288.001, 288), (0.0, 288), (577.0, 288),
                           (1e-9, 1.0), (2.0000005, 1.0), (2.000002, 1.0), (1000.0, 3.0)):
        straenge.append([bits(bedarf), bits(float(strang)), fc.ups_strings_needed(bedarf, strang)])

    rechenwege = {name: getattr(fc, name) for name in sorted(dir(fc))
                  if name.startswith("CALC_EXPLAIN_") and isinstance(getattr(fc, name), str)}

    pc = oberflaechentexte(repo, *PC_BEREICH, handy=False)
    handy = oberflaechentexte(repo, *HANDY_BEREICH, handy=True)
    pc_menge = {t for _z, t in pc}
    handy_menge = {t for _z, t in handy}
    unterschiede = [["pc", z, t] for z, t in pc if t not in handy_menge] \
        + [["handy", z, t] for z, t in handy if t not in pc_menge]

    daten = {
        "quelle": "fisi_core raid_report, raid_values, screen_report, ups_calculate, ups_picture_summary, "
                  "ups_task_text, ups_strings_needed, CALC_EXPLAIN_*; app_gui.py %d-%d, mobile/src/main.py %d-%d "
                  "(Python v0.63.2)" % (PC_BEREICH[1], PC_BEREICH[2], HANDY_BEREICH[1], HANDY_BEREICH[2]),
        "python": sys.version.split()[0],
        "raid": raid,
        "raid_werte": raid_werte,
        "bildschirm": bildschirm,
        "usv": usv,
        "aufgaben": aufgaben,
        "straenge": straenge,
        "rechenwege": rechenwege,
        "oberflaeche": {"pc": pc, "handy": handy, "unterschiede": unterschiede},
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G26: %d RAID, %d Bildschirm, %d USV, %d Aufgaben, %d Rechenwege, %d Unterschiede PC/Handy -> %s"
          % (len(raid), len(bildschirm), len(usv), len(aufgaben), len(rechenwege), len(unterschiede), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
