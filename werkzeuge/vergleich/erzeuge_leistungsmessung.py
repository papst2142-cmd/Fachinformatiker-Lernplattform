#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G39 (Phase 5, Paket BE7 "Leistungsmessung"): Messdatei aus festen
record-Aufrufen, Kuerzen ueber 1 MB, Kopfzeile einer aelteren Datei ersetzen, state_text, _number,
_clean und die Auswertung (read_rows, messart_of, page_times), gerechnet vom echten Python-Code
der Python-Linie (v0.63.2, fisi_leistung.Recorder). Keine Lernstand-Daten.

Fest vorgegeben (der C#-Test gibt dieselben Werte vor):
  Uhr        - Zeitpunkt i = 2026-10-20T10:00:00 + i * 1,0015 s (fortlaufend je Aufruf von now())
  System     - "Linux 6.8.0-45-generic"
  Speicher   - memory_mb() = (123.45, None) bzw. im Fall "windows" (100.25, 50.75)
  Verfuegbar - available_mb() = 2047.5 (Fall "windows" 1022.5)

Aufbau:
  zahlen     - [Wert, _number(Wert), _number(Wert, 0)]
  sauber     - [Text, _clean(Text)]
  faelle     - je Fall: version, geraet, toolkit, start_task, Aufrufe [Ereignis, kwargs, Vorgang aus
               task_source], ob vorher eine alte Datei lag (alt), Inhalt danach, info, state_text
  kuerzen    - Muster fuer n Aufrufe (C# wiederholt sie), danach Anzahl Zeilen, erste und letzte
               Datenzeile, Groesse, SHA-256 der Datei, state_text
  auswertung - Datei (Text), read_rows, messart_of je Zeile, page_times fuer Messart 1 und 2

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_leistungsmessung.py <pfad_fisi_lernplattform> tests/Vergleich/leistungsmessung.json
"""
import atexit
import datetime
import hashlib
import json
import os
import shutil
import sys
import tempfile

START = datetime.datetime(2026, 10, 20, 10, 0, 0)
SCHRITT_US = 1001500
SYSTEM = "Linux 6.8.0-45-generic"
ALT_KOPF = "zeit;version;geraet;system;ereignis;von;nach;dauer_ms;max_ms;anzahl;ereignisse;speicher_mb;privat_mb;elemente;aufgaben;customtkinter"


class Uhr:
    def __init__(self):
        self.i = 0

    def now(self):
        wert = START + datetime.timedelta(microseconds=SCHRITT_US * self.i)
        self.i += 1
        return wert


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
    import fisi_leistung as fle

    uhr = Uhr()

    class FesteZeit:
        class datetime:
            @staticmethod
            def now():
                return uhr.now()

    fle.datetime = FesteZeit
    fle.system_text = lambda: SYSTEM
    speicher = {"werte": (123.45, None), "frei": 2047.5}
    fle.memory_mb = lambda: speicher["werte"]
    fle.available_mb = lambda: speicher["frei"]
    fle.enabled = lambda: True
    fle.set_enabled = lambda flag: True

    zahlen = [[w, fle._number(w), fle._number(w, 0)] for w in
              [None, 0, 7, 1234, 0.0, 0.05, 0.15, 0.25, 0.35, 1.25, 2.5, 3.5, 12.345, 99.95, 1022.5, 1023.5, 2047.5,
               -0.05, -1.25, 123.45, 1e-7, 123456.789]]
    sauber = [[t, fle._clean(t)] for t in ["", "a;b", "zeile1\nzeile2", "a\r\nb", "x;y;z\r", None, 0]]

    def lauf(name, aufrufe, version="A 0.10.0", geraet="PC", toolkit="Avalonia 12.1.0", start_task="", alt=None,
             windows=False):
        uhr.i = 0
        speicher["werte"], speicher["frei"] = ((100.25, 50.75), 1022.5) if windows else ((123.45, None), 2047.5)
        pfad = os.path.join(tmp, name + ".csv")
        if alt is not None:
            with open(pfad, "w", encoding="utf-8", newline="\n") as handle:
                handle.write(alt)
        rec = fle.Recorder(version, geraet, path=pfad, toolkit=toolkit)
        rec.start_task = start_task
        quelle = {"vorgang": ""}
        rec.task_source = lambda: quelle["vorgang"]
        for ereignis, kwargs, vorgang in aufrufe:
            quelle["vorgang"] = vorgang
            if ereignis == "AUS":
                rec.set_active(False)
            elif ereignis == "AN":
                rec.set_active(True)
            else:
                rec.record(ereignis, **kwargs)
        rec.flush()
        with open(pfad, encoding="utf-8") as handle:
            inhalt = handle.read()
        return {"name": name, "version": version, "geraet": geraet, "toolkit": toolkit, "start_task": start_task,
                "windows": windows, "alt": alt, "aufrufe": aufrufe, "inhalt": inhalt, "info": list(rec.info()),
                "zustand": rec.state_text()}

    faelle = [
        lauf("einfach", [
            ["start", {"elemente": 3300, "aufgaben": 25}, ""],
            ["seite", {"von": "dashboard", "nach": "settings", "dauer_ms": 42.05, "elemente": 3301, "aufgaben": 24, "messart": 2}, "seite:settings"],
            ["darstellung", {"von": "dunkel", "nach": "hell", "dauer_ms": 812.25, "max_ms": 401.75}, "darstellung"],
            ["groesse", {"von": "1360x880", "nach": "1200x800", "dauer_ms": 12.5, "max_ms": 30.25, "anzahl": 14, "ereignisse": 220}, ""],
            ["blockiert", {"nach": "dashboard", "dauer_ms": 2512.0, "vorgang": "abgleich"}, "bereit"],
            ["vorladen", {"nach": "help:teil", "dauer_ms": 3.35, "speicher": False}, "vorladen:help"],
            ["vorladen_fertig", {"dauer_ms": 1999.95}, ""],
            ["seite", {"von": "a;b", "nach": "c\nd", "dauer_ms": 0.05}, "x;y"],
            ["ende", {}, ""],
        ]),
        lauf("startvorgang", [
            ["start", {}, "anders"],
            ["start", {"vorgang": "gesetzt"}, ""],
            ["seite", {"nach": "x"}, "laufend"],
        ], geraet="Handy", toolkit="", start_task="eingabe-umgehung"),
        lauf("windows", [["start", {}, ""], ["seite", {"von": "a", "nach": "b", "dauer_ms": 1.25}, ""]], windows=True),
        lauf("aus_an", [
            ["seite", {"nach": "vorher"}, ""],
            ["AUS", {}, ""],
            ["seite", {"nach": "nicht"}, ""],
            ["AN", {}, "an"],
            ["seite", {"nach": "danach"}, ""],
        ]),
        lauf("alter_kopf", [["seite", {"nach": "neu", "dauer_ms": 5.5}, ""]],
             alt=ALT_KOPF + "\n2026-01-01T00:00:00.000;0.58.1;PC;Windows 11;start;;;;;;;;;;;4.2\n"
                            "2026-01-01T00:00:01.000;0.58.1;PC;Windows 11;seite;a;b;1,0;;;;;;;;\n"),
        lauf("gleicher_kopf", [["seite", {"nach": "neu"}, ""]],
             alt=";".join(fle.COLUMNS) + "\nalte;zeile\n"),
        lauf("puffer_voll", [["seite", {"nach": "s%d" % i, "dauer_ms": i * 0.5}, ""] for i in range(60)]),
    ]

    # -- Kuerzen ueber 1 MB -------------------------------------------------------------
    uhr.i = 0
    speicher["werte"], speicher["frei"] = (123.45, None), 2047.5
    pfad = os.path.join(tmp, "kuerzen.csv")
    rec = fle.Recorder("A 0.10.0", "PC", path=pfad, toolkit="Avalonia 12.1.0")
    rec.task_source = lambda: "vorladen:settings"
    anzahl = 9000
    for i in range(anzahl):
        rec.record("seite", von="seite%04d" % (i % 997), nach="ziel%04d" % ((i * 7) % 1009), dauer_ms=i * 0.25,
                   elemente=i, aufgaben=i % 50, messart=2 if i % 3 else None)
    rec.flush()
    with open(pfad, "rb") as handle:
        roh = handle.read()
    zeilen = roh.decode("utf-8").splitlines()
    kuerzen = {"anzahl": anzahl,
               "muster": "record('seite', von='seite%04d' % (i % 997), nach='ziel%04d' % ((i * 7) % 1009), dauer_ms=i * 0.25, "
                         "elemente=i, aufgaben=i % 50, messart=2 if i % 3 else None); task_source = 'vorladen:settings'",
               "zeilen": len(zeilen) - 1, "erste": zeilen[1], "letzte": zeilen[-1], "groesse": len(roh),
               "sha256": hashlib.sha256(roh).hexdigest(), "zustand": rec.state_text()}

    # -- Auswertung ------------------------------------------------------------------------
    datei = ";".join(fle.COLUMNS) + "\n" + "\n".join([
            "t;v;PC;s;seite;a;dashboard;10,5;;;;;;;;",
            "t;v;PC;s;seite;a;settings;20,0;;;;;;;;",
            "t;v;PC;s;seite;a;dashboard;11,5;;;;;;;;;;;2",
            "t;v;PC;s;seite;a;dashboard;12,25;;;;;;;;;;;2",
            "t;v;PC;s;seite;a;settings;7;;;;;;;;;;; 2 ",
            "t;v;PC;s;seite;a;dashboard;13;;;;;;;;;;;1",
            "t;v;PC;s;seite;a;help;kaputt;;;;;;;;;;;2",
            "t;v;PC;s;seite;a;help;;;;;;;;;;;;2",
            "t;v;PC;s;darstellung;a;help;99;;;;;;;;;;;2",
            "t;v;PC;s;seite;a;quiz;3,5;;;;;;;;;;;x",
            "t;v;PC;s;seite;a;quiz;4,5;;;;;;;;;;;",
            "",
            "t;v;PC;s;seite;a;stats;1e3;;;;;;;;;;;2",
            "kurz;zeile",
        ]) + "\n"
    pfad = os.path.join(tmp, "auswertung.csv")
    with open(pfad, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(datei)
    zeilen = fle.read_rows(pfad)
    auswertung = {"datei": datei, "zeilen": zeilen, "messart": [fle.messart_of(z) for z in zeilen],
                  "seiten": {str(art): [[seite, werte] for seite, werte in fle.page_times(zeilen, art).items()]
                             for art in (1, 2)}}

    ergebnis = {"quelle": "fisi_leistung.py (v0.63.2)", "start": START.isoformat(), "schritt_us": SCHRITT_US,
                "system": SYSTEM, "spalten": list(fle.COLUMNS), "max_bytes": fle.MAX_BYTES, "trim_to": fle.TRIM_TO,
                "flush_lines": fle.FLUSH_LINES, "texte": {"state_none": fle.STATE_NONE, "state_file": fle.STATE_FILE},
                "zahlen": zahlen, "sauber": sauber, "faelle": faelle, "kuerzen": kuerzen, "auswertung": auswertung}
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(ergebnis, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    print("G39: %d Faelle, Kuerzen %d -> %d Zeilen -> %s" % (len(faelle), anzahl, kuerzen["zeilen"], ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
