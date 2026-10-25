#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G42 (Phase 7, W2 "Spiel-Kern Teil 1"), gerechnet
vom echten Python-Code der Python-Linie (v0.63.2, eingefroren):

  schluessel  - Schluessel von fisi_game.load_game_content() in ihrer
                Reihenfolge.
  werte       - je Schluessel Typ, Anzahl (len) und SHA-256 von
                json.dumps(wert, sort_keys=True, ensure_ascii=False,
                separators=(",", ":")).
  aufgaben, zwischenfaelle
              - je Auftrag Kennung, Schluessel in ihrer Reihenfolge und
                SHA-256 (damit eine Abweichung genau einen Auftrag nennt).
  story       - je Story-Schluessel die SHA-256 des Wertes.
  pruefung    - Ergebnis von fisi_game.validate_game_content() fuer die
                echten Inhalte (leere Liste = in Ordnung).
  faelle      - feste Beispiele fuer expand_variants, _lines und
                _story_value (Eingabe und Ergebnis), auch Sonderfaelle
                (Zahl als Platzhalterwert, wiederholen, auffaellig, Vorlage).

Es werden keine Lerndaten benutzt; alle Daten- und Einstellungsordner zeigen
in einen leeren Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_inhalte.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_inhalte.json
"""
import copy
import hashlib
import json
import os
import sys
import tempfile

FAELLE_VARIANTEN = [
    {"name": "ohne_varianten", "vorlage": False,
     "eintraege": [{"id": "a1", "ticket": "x", "varianten": []}]},
    {"name": "vorlage_ohne_varianten", "vorlage": True,
     "eintraege": [{"id": "v1", "ticket": ["a", "b"]}]},
    {"name": "platzhalter", "vorlage": False,
     "eintraege": [{"id": "p", "ticket": "Hallo {{name}}, {{n}} PCs, {{f}} GB, {{w}}",
                    "daten": {"liste": ["{{name}}", 3, None], "{{name}}": "{{name}}"},
                    "varianten": [{"werte": {"name": "Anna", "n": 3, "f": 1.5, "w": True}},
                                  {"werte": {"name": "Bert", "n": 10, "f": 2.0, "w": None},
                                   "raum": "lager", "ab_tag": 7},
                                  {"raum": "buero"}]}]},
    {"name": "wiederholen", "vorlage": False,
     "eintraege": [{"id": "w", "ab_tag": 1, "wiederholen": {"start": 18, "abstand": 12},
                    "varianten": [{}, {"ab_tag": 5}, {}, {"werte": {}}]},
                   {"id": "w2", "wiederholen": {"abstand": 2.5}, "varianten": [{}, {}]},
                   {"id": "w3", "wiederholen": {}, "varianten": [{}, {}]}]},
    {"name": "auffaellig", "vorlage": True,
     "eintraege": [{"id": "wartung", "pruefpunkte": [{"id": "lueft"}, {"id": "akku"}, {"id": 3}],
                    "varianten": [{"auffaellig": ["akku"]}, {"auffaellig": []},
                                  {"auffaellig": ["lueft", 3.0]}]},
                   {"id": "ohne_punkte", "varianten": [{"auffaellig": ["x"]}]}]},
    {"name": "id_zahl", "vorlage": False,
     "eintraege": [{"id": 7, "varianten": [{}, {}]}]},
]

FAELLE_ZEILEN = [["a", "b"], [], "", None, "Text", 0, 5, [""], ["x"], {"a": 1}, False, True]

FAELLE_STORY = [["a", "b"], [["a", "b"], ["c"]], [[], ["d"]], {"1": ["x", "y"], "2": "z"},
                [], "Text", None, [["a"], "b"]]


def pruefsumme(wert):
    text = json.dumps(wert, sort_keys=True, ensure_ascii=False, separators=(",", ":"))
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


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
    import fisi_game as game

    inhalt = game.load_game_content()
    werte = {}
    for schluessel, wert in inhalt.items():
        werte[schluessel] = {"typ": type(wert).__name__, "anzahl": len(wert),
                             "sha256": pruefsumme(wert)}
    auftraege = {}
    for liste in ("aufgaben", "zwischenfaelle"):
        auftraege[liste] = [{"id": a["id"], "felder": list(a), "sha256": pruefsumme(a)}
                            for a in inhalt[liste]]
    faelle = {
        "varianten": [{"name": f["name"], "vorlage": f["vorlage"], "eintraege": f["eintraege"],
                       "ergebnis": game.expand_variants(copy.deepcopy(f["eintraege"]),
                                                        template=f["vorlage"])}
                      for f in FAELLE_VARIANTEN],
        "zeilen": [{"eingabe": e, "ergebnis": game._lines(copy.deepcopy(e))} for e in FAELLE_ZEILEN],
        "story": [{"eingabe": e, "ergebnis": game._story_value(copy.deepcopy(e))}
                  for e in FAELLE_STORY],
    }
    daten = {
        "schluessel": list(inhalt),
        "werte": werte,
        "aufgaben": auftraege["aufgaben"],
        "zwischenfaelle": auftraege["zwischenfaelle"],
        "story": {k: pruefsumme(v) for k, v in inhalt["story"].items()},
        "pruefung": game.validate_game_content(inhalt),
        "faelle": faelle,
    }
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G42: %d Schluessel, %d Auftraege, %d Zwischenfaelle, %d Pruefmeldungen -> %s"
          % (len(werte), len(auftraege["aufgaben"]), len(auftraege["zwischenfaelle"]),
             len(daten["pruefung"]), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
