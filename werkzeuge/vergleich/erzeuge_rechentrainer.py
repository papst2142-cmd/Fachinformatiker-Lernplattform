#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G27 (Phase 3, Welle 3, Paket R4 "Rechen-Trainer"),
gerechnet vom echten Python-Code der Python-Linie (v0.63.2, eingefroren) mit
Python 3.12 (CI):

  konstanten - TRAINER_KINDS, TRAINER_LEVELS, TRAINER_ROUND, TRAINER_TITLE,
               TRAINER_NAMES, TRAINER_GAP_OLD/NEW (fisi_lernen).
  aufgaben   - trainer_task(kind, level, seed, schmal) fuer 6 Arten x 3 Stufen x
               feste Startwerte (Text), IPv4 auch schmal: Text, Felder, Loesung,
               Rechenweg, Handy-Rechenweg (steps_schmal) und check() fuer vier
               Antwortsaetze (Loesung, Loesung in Grossbuchstaben mit Leerraum,
               Loesung mit vertauschten Feldern, leer).
  runden     - trainer_round(kind, level, seed, count, schmal) mit ganzzahligem
               Startwert wie app_gui/main.py (random.randrange(1 << 30)): je
               Aufgabe Text und Loesung.
  summen     - trainer_summary(results).
  bereinigen - _clean(text) (Leerraum wie str.split(), Kleinschreibung wie str.lower()).
  int_basis  - int(text, basis) fuer basis 2, 10, 16 (Grundlage von _base_matcher; auch
               Eingaben, die _clean vorher schon aendert).
  speichern  - (Paket R5) Zeilen, die DBManager.log_trainer mit festem Zeitpunkt
               (Zone Europe/Berlin, auch Zeitumstellung) und fester uid schreibt,
               trainer_stats() danach und die Statistikzeile wie show_stats
               (app_gui 5552, main.py 2992); dazu die Statistikzeile fuer feste
               Zaehlwerte.
  gross      - Pruefer mit Eingaben um Pythons Grenze von 4300 Ziffern fuer int() (Python 3.12
               wirft ValueError, den der Trainer nicht faengt: "ValueError"; C# wertet
               nach Weisung P3-12 Frage 3 als falsch).
  wischen    - (R6-Nachtrag) FIT_HINT aus mobile/src/ui.py (per ast gelesen, Flet wird nicht
               geladen) und fuer die Rechenwege einiger Aufgaben: fit_lines(text, zeichen,
               einheitlich) und ob danach eine Zeile laenger als `zeichen` ist (dann zeigt die
               FitBox "→ wischen").
  pruefen    - alle Texte in TrainerPanel.check() von app_gui.py und mobile/src/main.py (per ast;
               G26 "oberflaeche" laesst kleingeschriebene Einzelwoerter wie "richtig" aus).
  pruefer    - je Pruefer (_ip_matcher, _int_matcher, _base_matcher,
               _network_matcher, _ipv6_text_matcher, _prefix_matcher,
               _count_matcher) eine Liste Eingabe -> richtig/falsch.

Es werden keine Lerndaten benutzt; alle Daten- und Einstellungsordner zeigen
in einen leeren Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_rechentrainer.py <pfad_fisi_lernplattform> tests/Vergleich/rechentrainer.json
"""
import ast
import atexit
import datetime
import ipaddress
import json
import os
import shutil
import sqlite3
import sys
import tempfile
import time

ZONE = "Europe/Berlin"

# Feste Startwerte je (Art, Stufe); dazu Grenzfaelle des Textes
STARTWERTE = ["%d-%d" % (runde, nummer) for runde in (0, 1, 7, 42, 1000, 123456789, (1 << 30) - 1)
              for nummer in range(0, 10, 3)] + ["", "x", "ä", "-1", "0", " 5", "1-2-3", "😀"]
BEREINIGEN = ["", " ", "AbC", " a b\tc\n", "\x1c1\x1d2\x1e3\x1f4", "\u2003x\u3000y\xa0z\x85", "a\u200bb",
              "\u0130", "K\u212a", "\u03a3\u0391\u03a3", "\u1e9e\xdf", "FE80::1%ETH0", "\u2028\u2029x",
              "\uff21\uff26", "\U0001f600A", "\u0049\u0307", "\u01c5\u01c4"]
INT_BASIS = ["ff", " ff ", "\x1cff", "ff\x1f", "\u2003ff\u3000", "\xa0ff", "\x85ff", "+ff", "-ff", "0xff", "0XFF",
             "0x_ff", "0x__ff", "_ff", "ff_", "f_f", "f__f", "0b101", "0B101", "0b_1", "0o17", "0x", "0b", "",
             " ", "٢٥٥", "0x٢٥", "ｆｆ", "²", "1²", "\x7f1", "1\x7f", "+-1", "--1", "- 1", "1 0", "1\t", "\v1\f",
             "\r\n1", "10", "١٠", "z", "Z", "g", "0x0x1", "0b0b1", "00ff", "1_000", "\U0001d7ce", "١_٢"]
RUNDEN = [0, 1, 99, 536870911, (1 << 30) - 1]

PRUEFER = [
    # (art, erwartet, power) und Eingaben
    ("ip", "192.168.1.0", ["192.168.1.0", " 192.168.1.0 ", "192.168. 1.0", "192.168.001.0", "192.168.1.00",
                           "192.168.01.0", "::ffff:192.168.1.0", "::ffff:c0a8:100", "192.168.1.0/24",
                           "192.168.1.0/32", "", " ", "١٩٢.168.1.0", "192.168.1.0.", "192.168.1", "3232235776",
                           "0xc0.168.1.0", "192.168.1.0%1", "192.168.1.0 ", "　192.168.1.0",
                           "192\x1c.168.1.0", "192.168.1.1", "192.168.1.0\n"]),
    ("ip", "255.255.255.192", ["255.255.255.192", "/26", "26", "255.255.255.192/32", "255.255.255.0192"]),
    ("ip", "2001:db8::1", ["2001:db8::1", "2001:DB8::1", "2001:0db8:0:0:0:0:0:1", "2001:0db8:0000:0000:0000:0000:0000:0001",
                           "2001:db8::0:1", "2001:db8:0::1", "2001 : db8 :: 1", "2001:db8::1%eth0", "2001:db8::1%",
                           "2001:db8::01", "2001:db8::00001", "２００１:db8::1", "2001:db8::١", "2001:db8:::1",
                           "2001:db8::1/128", "::2001:db8:0:0:1", "2001:db8::0.0.0.1", "2001:DB8::1".lower(),
                           "2001:dB8::1", "2001:db8::1:0", "[2001:db8::1]"]),
    ("ip", "::ffff:102:304", ["::ffff:102:304", "::ffff:1.2.3.4", "1.2.3.4", "0:0:0:0:0:ffff:0102:0304"]),
    ("ip", "fe80::1", ["fe80::1", "FE80::1", "fe80::1%1", "fe80::1%eth0", "fe80:0::1"]),
    ("int", "62", ["62", "6.2", "0062", "+62", "62_", "6_2", "٦٢", "62.0", "", " 6 2 ", "-62", "6²", "62e0",
                   "６２", ".62", "62.", "6..2", "0x3e", "62h", "62\x1f"]),
    ("int", "123", ["1.2.3", "123", "12.3", "1.23", "1,23"]),
    ("int", "0", ["0", "00", "", ".", "-0", "+0"]),
    ("int", "1022", ["1022", "1.022", "1022", "10.22", "1.0.2.2"]),
    ("bin", "255", ["11111111", "0b11111111", "0B11111111", "1111_1111", "1111__1111", "_11111111",
                    "11111111_", "0b_11111111", "0b0b11111111", "b11111111", "11111111b", "+11111111",
                    "-11111111", "0011111111", "1111 1111", "١١١١١١١١", "11111112", "", "0b", "ff",
                    "0x11111111", "11111111h"]),
    ("bin", "5", ["101", "0101", "0b101", "1 0 1", "0b 101", "0 b101", "١٠١", "5"]),
    ("hex", "255", ["ff", "FF", "0xff", "0XFF", "0x0xff", "ffh", "FFh", "hff", "0xffh", "h0xff", "hffh", "ffhh",
                    "+ff", "-ff", "f f", "0x_ff", "", "0x", "h", "ｆｆ", "f_f", "f__f", "0x0x", "00ff", "0b11111111",
                    "255", "0xfF", "ff ", "0xff_", "x ff", "#ff", "$ff"]),
    ("hex", "597", ["255", "٢٥٥", "0x٢٥٥", "２５５", "0x255", "255h"]),
    ("hex", "177", ["b1", "0b1", "0xb1", "0b1h", "b1h"]),
    ("hex", "4095", ["fff", "FFF", "0fff", "0x0fff", "4095"]),
    ("netz", "192.168.1.64/26", ["192.168.1.64/26", "192.168.1.65/26", "192.168.1.64/255.255.255.192",
                                 "192.168.1.64/0.0.0.63", "192.168.1.64/026", "192.168.1.64", "192.168.1.64 / 26",
                                 "192.168.1.64/26 ", "192.168.1.64/27", "192.168.1.064/26", "192.168.1.64/٢٦",
                                 "192.168.1.64/+26", "192.168.1.64//26", "", "192.168.1.0/26",
                                 "::ffff:192.168.1.64/122"]),
    ("netz", "10.0.0.0/8", ["10.0.0.0/8", "10.0.0.0/255.0.0.0", "10.0.0.0/0.255.255.255", "10.0.0.0/08",
                            "10.0.0.0/8.0", "10.0.0.0/255.0.0.0.0"]),
    ("netz", "192.168.4.0/22", ["192.168.4.0/22", "192.168.4.0/255.255.252.0", "192.168.5.0/22",
                                "192.168.4.0/22\t", "192.168.4.0/0022"]),
    ("ipv6text", "2001:db8::1", ["2001:db8::1", "2001:DB8::1", " 2001:db8::1 ", "2001:db8:0::1", "2001:0db8::1",
                                 "2001:db8::1\n", "", "2001:db8::1%1"]),
    ("ipv6text", "2001:0db8:0000:0000:0000:0000:0000:0001",
     ["2001:0db8:0000:0000:0000:0000:0000:0001", "2001:0DB8:0000:0000:0000:0000:0000:0001", "2001:db8::1",
      "2001:0db8:0000:0000:0000:0000:0000:0001 ", "2001:0db8:0000:0000:0000:0000:0000:00001"]),
    ("praefix", "26", ["/26", "26", "//26", "/ 26", "026", "/026", "٢٦", "/٢٦", "+26", "/26/", "", "/", "2 6",
                       "26.0", "/26 ", "\\26", "/27"]),
    ("praefix", "8", ["/8", "8", "08", "/08", "/0008", "/8.", "８"]),
    ("anzahl", "1024", ["1024", "1.024", "2^10", "2^010", "2^0010", "2^00010", "1024.", "10.24", "1,024",
                        "01.024", "١٠٢٤", "2 ^ 10", "2^1٠", "2^", "2^+10", "2**10", "+1024", "1.024.",
                        ".1024", "1024.000", "1.0.2.4", "0001.024", "1 024", "1_024", "2^10.0"]),
    ("anzahl_potenz", "1024", ["1024", "1.024", "2^10", "2^010", "2^0010", "2^00010", "2^1٠", "2^", "2^-10",
                               "2^ 10", "2 ^10", "2^10 ", "x2^10", "2^10^1", "2^9", "2^11", "1.024.0"]),
    ("anzahl_potenz", "18446744073709551616", ["18446744073709551616", "18.446.744.073.709.551.616", "2^64",
                                               "2^064", "2^0064", "18.446.744.073.709.551.6160", "1.8446744073709551616"]),
    ("anzahl_potenz", "1", ["1", "2^0", "2^00", "2^000", "2^0000", "01", "1.000"]),
    ("anzahl", "254", ["254", "2^8", "0254", "254.", "2.54"]),
]


SPEICHERN = [
    # (Art, Stufe, alles richtig, Zeitpunkt UTC)
    ("ipv4", 1, True, "2026-10-20T08:15:30.000000+00:00"),
    ("ipv4", 1, False, "2026-10-20T21:59:59.999999+00:00"),
    ("teilen", 3, True, "2026-10-20T22:00:00.000000+00:00"),
    ("bedarf", 2, False, "2026-10-25T00:59:59.000000+00:00"),
    ("zahlen", 3, True, "2026-10-25T01:00:00.000000+00:00"),
    ("ipv6", 2, True, "2027-03-28T01:00:00.000000+00:00"),
    ("vlsm", 1, False, "2026-12-31T23:00:00.000000+00:00"),
    ("ipv4", 3, True, "2026-10-21T10:00:00.000000+00:00"),
]
STATISTIK = [{}, {"ipv4": (1, 1)}, {"ipv4": (1, 0)}, {"ipv4": (2, 1), "vlsm": (3, 3)},
             {"zahlen": (999, 500)}, {"zahlen": (1000, 1000)}, {"a": (1234567, 3), "b": (1, 0)},
             {"ipv4": (0, 0)}, {"ipv4": (1, 0.5), "vlsm": (1, 0.5)}, {"ipv4": (3, 2), "vlsm": (2, 0.75)},
             {"a": (1, 0.1), "b": (1, 0.2), "c": (1, 0.7)}]


def statistik_zeile(fc, stats):
    """show_stats ohne Oberflaeche (app_gui 5552-5557, main.py 2992-2997 gleich)."""
    count = sum(total for total, _right in stats.values())
    right = sum(right for _total, right in stats.values())
    return ("bisher %s, %d richtig" % (fc.plural(count, "Aufgabe", "Aufgaben"), right)
            if count else "")


def speichern_werte(tmp):
    import fisi_core as fc
    import fisi_lernen as fl
    jetzt = [None]
    fc.device_now = lambda: jetzt[0].astimezone()
    zaehler = [0]

    def uid():
        zaehler[0] += 1
        return "rechentrainer-uid-%04d" % zaehler[0]

    fc.DBManager._uid = staticmethod(uid)
    db = fc.DBManager(os.path.join(tmp, "rechentrainer.db"))
    vorher = sorted([k, v[0], v[1]] for k, v in db.trainer_stats().items())
    aufrufe = []
    for art, stufe, richtig, moment in SPEICHERN:
        jetzt[0] = datetime.datetime.fromisoformat(moment)
        aufrufe.append({"art": art, "stufe": stufe, "richtig": richtig, "utc": moment,
                        "ergebnis": bool(db.log_trainer(art, stufe, richtig))})
    conn = sqlite3.connect(db.db_path)
    cur = conn.execute("SELECT * FROM trainer_aufgaben ORDER BY id")
    spalten = [d[0] for d in cur.description if d[0] != "id"]
    zeilen = [[w for d, w in zip(cur.description, r) if d[0] != "id"] for r in cur.fetchall()]
    typen = [list(r) for r in conn.execute("SELECT %s FROM trainer_aufgaben ORDER BY id"
                                           % ", ".join("typeof(%s)" % s for s in spalten))]
    conn.close()
    stats = db.trainer_stats()
    return {
        "zone": ZONE,
        "vorher": vorher,
        "aufrufe": aufrufe,
        "spalten": spalten,
        "zeilen": zeilen,
        "typen": typen,
        "statistik": sorted([k, v[0], v[1]] for k, v in stats.items()),
        "zeile": statistik_zeile(fc, stats),
        "zeilen_fest": [[sorted([k, v[0], v[1]] for k, v in s.items()), statistik_zeile(fc, s)]
                        for s in STATISTIK],
    }


def pruefen_texte(repo):
    """Texte in TrainerPanel.check() (PC und Handy), in Reihenfolge des Quelltextes."""
    ergebnis = {}
    for geraet, datei in (("pc", "app_gui.py"), ("handy", os.path.join("mobile", "src", "main.py"))):
        with open(os.path.join(repo, datei), encoding="utf-8") as handle:
            baum = ast.parse(handle.read())
        klasse = [k for k in baum.body if isinstance(k, ast.ClassDef) and k.name == "TrainerPanel"][0]
        methode = [m for m in klasse.body if isinstance(m, ast.FunctionDef) and m.name == "check"][0]
        texte = [k for k in ast.walk(methode) if isinstance(k, ast.Constant) and isinstance(k.value, str)]
        ergebnis[geraet] = [k.value for k in sorted(texte, key=lambda k: (k.lineno, k.col_offset))]
    return ergebnis


def wischen_werte(repo, aufgaben):
    """FIT_HINT (mobile/src/ui.py) und die Regel der FitBox (_refit): Hinweis, wenn nach
    fit_lines eine Zeile laenger als der Kasten ist."""
    import fisi_core as fc
    with open(os.path.join(repo, "mobile", "src", "ui.py"), encoding="utf-8") as handle:
        baum = ast.parse(handle.read())
    hinweis = [k.value.value for k in baum.body if isinstance(k, ast.Assign) and len(k.targets) == 1
               and isinstance(k.targets[0], ast.Name) and k.targets[0].id == "FIT_HINT"]
    texte = []
    for eintrag in aufgaben:
        if eintrag["startwert"] == "0-0":
            # wie TrainerPanel.check: "\n".join(steps_schmal or steps)
            texte.append("\n".join(eintrag["schritte"]))
            if eintrag["schritte_schmal"]:
                texte.append("\n".join(eintrag["schritte_schmal"]))
    faelle = []
    for text in texte:
        for zeichen in (12, 20, 28, 40, 75):
            for einheitlich in (False, True):
                gesetzt = fc.fit_lines(text, zeichen, einheitlich=einheitlich)
                zu_lang = any(len(zeile) > zeichen for zeile in gesetzt.split("\n"))
                faelle.append([text, zeichen, einheitlich, gesetzt, zu_lang])
    return {"hinweis": hinweis[0], "faelle": faelle}


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    os.environ["TZ"] = ZONE
    time.tzset()
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    atexit.register(shutil.rmtree, tmp, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_lernen as fl

    def antworten_saetze(task):
        keys = [key for key, _label in task.fields]
        loesung = dict(task.solution)
        gross = {key: "  " + value.upper().replace(".", " .") + "\t" for key, value in loesung.items()}
        vertauscht = {key: loesung[keys[(i + 1) % len(keys)]] for i, key in enumerate(keys)}
        return [loesung, gross, vertauscht, {}]

    def aufgabe_info(task):
        saetze = antworten_saetze(task)
        return {
            "text": task.text,
            "felder": [[key, label] for key, label in task.fields],
            "loesung": [[key, task.solution[key]] for key, _label in task.fields],
            "schritte": task.steps,
            "schritte_schmal": getattr(task, "steps_schmal", None),
            "antworten": [[[key, value] for key, value in satz.items()] for satz in saetze],
            "pruefung": [[[key, ok] for key, ok in task.check(satz).items()] for satz in saetze],
            "alle": [task.all_correct(satz) for satz in saetze],
        }

    aufgaben = []
    for kind, _name in fl.TRAINER_KINDS:
        for level, _stufe in fl.TRAINER_LEVELS:
            for seed in STARTWERTE:
                for schmal in ((False, True) if kind == "ipv4" else (False,)):
                    task = fl.trainer_task(kind, level, seed, schmal)
                    eintrag = {"art": kind, "stufe": level, "startwert": seed, "schmal": schmal}
                    eintrag.update(aufgabe_info(task))
                    aufgaben.append(eintrag)

    runden = []
    for kind, _name in fl.TRAINER_KINDS:
        for level, _stufe in fl.TRAINER_LEVELS:
            for seed in RUNDEN:
                for count, schmal in ((fl.TRAINER_ROUND, False), (3, True)):
                    tasks = fl.trainer_round(kind, level, seed, count, schmal)
                    runden.append({"art": kind, "stufe": level, "startwert": seed, "anzahl": count,
                                   "schmal": schmal,
                                   "aufgaben": [[t.text, [[k, t.solution[k]] for k, _l in t.fields],
                                                 len(t.steps)] for t in tasks]})

    summen = []
    for werte in ([], [True], [False], [True, False, True], [True] * 10, [False] * 10,
                  [True, True, False, True, False, True, True, True, False, True], [1, 0, "", "x", None]):
        summen.append([[bool(w) for w in werte], fl.trainer_summary(werte)])

    def pruefer_bauen(art, erwartet):
        if art == "ip":
            return fl._ip_matcher(ipaddress.ip_address(erwartet))
        if art == "int":
            return fl._int_matcher(int(erwartet))
        if art == "bin":
            return fl._base_matcher(int(erwartet), 2)
        if art == "hex":
            return fl._base_matcher(int(erwartet), 16)
        if art == "netz":
            return fl._network_matcher(ipaddress.ip_network(erwartet))
        if art == "ipv6text":
            return fl._ipv6_text_matcher(erwartet)
        if art == "praefix":
            return fl._prefix_matcher(int(erwartet))
        if art == "anzahl":
            return fl._count_matcher(int(erwartet))
        if art == "anzahl_potenz":
            return fl._count_matcher(int(erwartet), power=True)
        raise ValueError(art)

    bereinigen = [[text, fl._clean(text)] for text in BEREINIGEN]

    def int_wert(text, basis):
        try:
            return str(int(text, basis))
        except ValueError:
            return None

    int_basis = [[text, basis, int_wert(text, basis)] for text in INT_BASIS for basis in (2, 10, 16)]

    pruefer = []
    for art, erwartet, eingaben in PRUEFER:
        match = pruefer_bauen(art, erwartet)
        pruefer.append({"art": art, "erwartet": erwartet,
                        "faelle": [[eingabe, bool(match(eingabe))] for eingabe in eingaben]})

    speichern = speichern_werte(tmp)

    gross = []
    for art, erwartet, ziffern in (("int", "62", "62"), ("praefix", "26", "26"), ("anzahl", "1024", "1024"),
                                   ("anzahl_potenz", "1024", "1024"), ("hex", "255", "ff"), ("bin", "5", "101")):
        match = pruefer_bauen(art, erwartet)
        for laenge in (4299, 4300, 4301, 5000):
            for eingabe in ("0" * (laenge - len(ziffern)) + ziffern,
                            "/" + "0" * (laenge - len(ziffern)) + ziffern,
                            "0" * (laenge - len(ziffern) - 1) + "." + ziffern):
                try:
                    ergebnis = bool(match(eingabe))
                except ValueError:
                    ergebnis = "ValueError"
                gross.append([art, erwartet, len(eingabe), eingabe, ergebnis])

    wischen = wischen_werte(repo, aufgaben)

    daten = {
        "quelle": "fisi_lernen trainer_task, trainer_round, trainer_summary, TrainerTask.check, "
                  "_ip_matcher, _int_matcher, _base_matcher, _network_matcher, _ipv6_text_matcher, "
                  "_prefix_matcher, _count_matcher (Python v0.63.2)",
        "python": sys.version.split()[0],
        "konstanten": {
            "TRAINER_KINDS": [list(k) for k in fl.TRAINER_KINDS],
            "TRAINER_LEVELS": [list(k) for k in fl.TRAINER_LEVELS],
            "TRAINER_ROUND": fl.TRAINER_ROUND,
            "TRAINER_TITLE": fl.TRAINER_TITLE,
            "TRAINER_NAMES": fl.TRAINER_NAMES,
            "TRAINER_GAP_OLD": fl.TRAINER_GAP_OLD,
            "TRAINER_GAP_NEW": fl.TRAINER_GAP_NEW,
        },
        "aufgaben": aufgaben,
        "runden": runden,
        "summen": summen,
        "bereinigen": bereinigen,
        "int_basis": int_basis,
        "gross": gross,
        "speichern": speichern,
        "wischen": wischen,
        "pruefen": pruefen_texte(repo),
        "pruefer": pruefer,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G27: %d Aufgaben, %d Runden, %d Zusammenfassungen, %d Pruefer (%d Faelle) -> %s"
          % (len(aufgaben), len(runden), len(summen), len(pruefer),
             sum(len(p["faelle"]) for p in pruefer), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
