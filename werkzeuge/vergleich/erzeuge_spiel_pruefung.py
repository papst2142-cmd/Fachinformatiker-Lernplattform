#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G42 Teil "Pruefung" (Phase 7, W2 "Spiel-Kern
Teil 1"), gerechnet vom echten Python-Code der Python-Linie (v0.63.2,
eingefroren): fisi_game.validate_game_content() fuer die echten Spielinhalte
und fuer absichtlich kaputte Fassungen davon.

Jeder Fall ist eine Liste von Aenderungen am geladenen Inhalt (wie GAME):
  ["set", pfad, wert]  - Wert setzen (dict-Schluessel oder Listenplatz)
  ["del", pfad]        - Schluessel oder Listeneintrag entfernen
  ["add", pfad, wert]  - an eine Liste anhaengen
Ein Pfad ist eine Liste aus Schluesseln (Text) und Listenplaetzen (Zahl). Die
C#-Seite macht dieselben Aenderungen und vergleicht die Liste der Probleme.

Die Pruefungen der Firma, Kredite, des Geschaefts, der Spielwelt, der Erfolge
und der Serverfarm kommen in W3; sie sind hier durch leere Listen ersetzt,
"w3" nennt, wie viele Meldungen sie fuer den Fall haetten (oder ihre Ausnahme).

Keine Lerndaten; alle Daten- und Einstellungsordner zeigen in einen leeren
Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_pruefung.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_pruefung.json
"""
import copy
import json
import os
import sys
import tempfile

W3 = ("_validate_firm", "_validate_loans", "_validate_business", "_validate_world",
      "_validate_achievements", "_validate_farm")


def anwenden(inhalt, aenderungen):
    for schritt in aenderungen:
        art, pfad = schritt[0], schritt[1]
        ziel = inhalt
        for teil in pfad[:-1]:
            ziel = ziel[teil]
        if art == "set":
            ziel[pfad[-1]] = copy.deepcopy(schritt[2])
        elif art == "del":
            del ziel[pfad[-1]]
        elif art == "add":
            ziel[pfad[-1]].append(copy.deepcopy(schritt[2]))
        else:
            raise ValueError(art)
    return inhalt


def faelle(fg, g):
    aufgaben = g["aufgaben"]

    def nr(bedingung):
        return next(i for i, a in enumerate(aufgaben) if bedingung(a))

    def typ(name, extra=lambda a: True):
        return nr(lambda a: a["typ"] == name and extra(a))

    A = ["aufgaben"]
    f = []

    def fall(name, *aenderungen):
        f.append({"name": name, "aenderungen": [list(a) for a in aenderungen]})

    buero = ["gebaeude", "raeume", 0]
    fall("Raum ohne Fachbereich", ["set", buero + ["cat"], "gibtsnicht"])
    fall("Raum ausserhalb", ["set", buero + ["x"], -1])
    fall("Raum zu breit", ["set", buero + ["w"], 999.5])
    fall("Tuer nicht an der Wand", ["set", buero + ["tuer"], {"von": 50, "bis": 51}])
    fall("Seitentuer", ["set", buero + ["tuer"], {"seite": "w", "von": 0, "bis": 1}])
    fall("Einrichtung unbekannt", ["add", buero + ["deko"], {"typ": "ufo", "x": 1, "y": 1, "w": 1, "h": 1}])
    fall("Einrichtung ragt heraus", ["add", buero + ["deko"], {"typ": "pflanze", "x": -1, "y": 1, "w": 1, "h": 1}])
    fall("Flur-Einrichtung", ["add", ["gebaeude", "flur", "deko"], {"typ": "pflanze", "x": 0, "y": -50, "w": 1, "h": 1}])
    fall("Person unbekannter Raum", ["set", ["kollegen", 0, "raum"], "gibtsnicht"])
    fall("Person falscher Platz", ["set", ["kollegen", 0, "platz"], [0.1, 0.1]])
    kunde = ["kunden", "orte", 0, "gebaeude", "raeume", 0]
    fall("Kunde Raum ohne Fachbereich", ["set", kunde + ["cat"], None])
    fall("Kunde Person Raum", ["set", ["kunden", "orte", 0, "personen", 0, "raum"], "nirgends"])
    fall("Raum doppelt", ["set", kunde + ["id"], g["gebaeude"]["raeume"][0]["id"]])
    fall("Person doppelt", ["set", ["kollegen", 1, "id"], g["kollegen"][0]["id"]])

    w = ["wohnungen"]
    fall("Moebel Typ", ["set", w + ["moebel", 1, "typ"], "ufo"])
    fall("Moebel Groesse", ["set", w + ["moebel", 1, "w"], 0])
    fall("Moebel Preis", ["del", w + ["moebel", 1, "preis"]])
    fall("Moebel Aktion", ["set", w + ["moebel", 1, "aktion"], "tanzen"])
    fall("Start-Moebel", ["set", w + ["start_moebel", "start-matratze"], "gibtsnicht"])
    fall("Start-Wohnung", ["set", w + ["start"], "gibtsnicht"])
    fall("Wohnung doppelt", ["set", w + ["wohnungen", 1, "id"], g["wohnungen"]["wohnungen"][0]["id"]])
    fall("Wohnung Farbe", ["del", w + ["wohnungen", 0, "gebaeude", "raeume", 0, "farbe"]])
    fall("Kartons", ["set", w + ["wohnungen", 0, "kartons"], [-5, 0, 1, 1]])
    fall("Start-Einrichtung", ["set", w + ["wohnungen", 0, "einrichtung", "moebel", "start-matratze"], [-3, 0, 0]])
    fall("Start-Einrichtung Tuer", ["set", w + ["wohnungen", 0, "einrichtung", "moebel", "start-matratze"], [0.25, 5.5, 1]])

    b = ["balancing"]
    fall("Miete fehlt", ["del", b + ["miete"]])
    fall("Kaution", ["set", b + ["miete", "kaution_anteil"], 1.5])
    fall("Kaution Text", ["set", b + ["miete", "kaution_anteil"], "x"])
    fall("Miete Start", ["set", b + ["miete", "pro_tag", g["wohnungen"]["start"]], 10])
    fall("Miete ungueltig", ["set", b + ["miete", "pro_tag", g["wohnungen"]["wohnungen"][1]["id"]], -1])
    fall("Miete Kommazahl", ["set", b + ["miete", "pro_tag", g["wohnungen"]["wohnungen"][1]["id"]], 10.5])
    fall("Miete unbekannt", ["set", b + ["miete", "pro_tag", "schloss"], 5])
    rang = g["balancing"]["raenge"][1]["name"]
    fall("Gehalt fehlt", ["del", b + ["gehalt_pro_tag", rang]])
    fall("Tickets fehlen", ["set", b + ["tickets_pro_tag", rang], 0])
    fall("ab_tag", ["set", b + ["raenge", 1, "ab_tag"], 9999])
    fall("ab_tag Anfang", ["set", b + ["raenge", 0, "ab_tag"], 2])

    h = ["hardware"]
    fall("Bauteil doppelt", ["set", h + ["teile", 1, "id"], g["hardware"]["teile"][0]["id"]])
    fall("Bauteil Typ", ["set", h + ["teile", 0, "typ"], "toaster"])
    fall("Bauteil Name", ["set", h + ["teile", 0, "name"], ""])
    fall("Bauteil Preis", ["set", h + ["teile", 0, "preis"], 0])
    fall("Bauteil Feld", ["del", h + ["teile", 0, "sockel"]])
    fall("Formfaktor", ["set", h + ["teile", 0, "formfaktor"], "riesig"])
    fall("Haendler doppelt", ["set", h + ["haendler", 1, "id"], g["hardware"]["haendler"][0]["id"]])
    fall("Ersatzteil Typ", ["set", h + ["ersatzteile", 0, "typ"], "toaster"])
    merkmal = g["hardware"]["ersatz_merkmale"][g["hardware"]["ersatzteile"][0]["typ"]][0][0]
    fall("Ersatzteil Merkmal", ["del", h + ["ersatzteile", 0, merkmal]])
    fall("Ersatzteil doppelt", ["set", h + ["ersatzteile", 0, "id"], g["hardware"]["teile"][0]["id"]])
    fall("Ersatz-Typ ohne Merkmale", ["set", h + ["ersatz_typen", "toaster"], "Toaster"])
    fall("Grundbestand", ["set", h + ["grundbestand", "gibtsnicht"], 1])
    fall("Nachbestellung", ["set", h + ["nachbestellung"], {"haendler": "x", "lieferzeit": 0}])
    fall("Rack-Feld", ["del", h + ["rack_geraete", 0, "watt"]])
    fall("Rack-Typ", ["set", h + ["rack_geraete", 0, "typ"], "toaster"])
    usv = next(i for i, d in enumerate(g["hardware"]["rack_geraete"]) if d["typ"] == "usv")
    fall("USV", ["set", h + ["rack_geraete", usv, "leistung"], 0])
    switch = next(i for i, d in enumerate(g["hardware"]["rack_geraete"]) if d["typ"] == "switch")
    fall("Switch", ["del", h + ["rack_geraete", switch, "ports"]])
    fall("Rack-Regeln", ["del", h + ["rack_regeln"]])
    fall("Rack doppelt", ["set", h + ["rack_geraete", 1, "id"], g["hardware"]["rack_geraete"][0]["id"]])
    fl = h + ["falschlieferung"]
    fall("Falschlieferung fehlt", ["del", fl])
    fall("Falschlieferung Feld", ["set", fl + ["hilfe"], ""])
    fall("Falschlieferung Antwort", ["set", fl + ["antwort"], "keine"])
    fall("Falschlieferung Person", ["set", fl + ["auftraggeber"], "niemand"])
    fall("Falschlieferung Raum", ["set", fl + ["raum"], "keller"])
    for i, text in enumerate(("{foo}", "{0}", "{}", "x { y", "x } y", "{ware!x}", "{{frei}} {ware} {bestellung!r:>3}",
                              "{ware.upper}", "{bestellung[0]}")):
        fall("Falschlieferung Format %d" % i, ["set", fl + ["titel"], text])

    i = 0
    fall("Feld fehlt", ["set", A + [i, "hilfe"], ""])
    fall("Feld None", ["set", A + [i, "frage"], None])
    fall("Kennung doppelt", ["set", A + [1, "id"], aufgaben[0]["id"]])
    fall("Platzhalter", ["set", A + [i, "frage"], "Was ist {{wert}}?"])
    fall("Tickettext doppelt", ["set", A + [1, "ticket"], aufgaben[0]["ticket"]])
    fall("Typ unbekannt", ["set", A + [i, "typ"], "raten"])
    fall("Raum unbekannt", ["set", A + [i, "raum"], "keller"])
    fall("Auftraggeber unbekannt", ["set", A + [i, "auftraggeber"], "niemand"])
    spaet = next(p for p in g["kollegen"] if p.get("ab_tag", 1) > 1)
    fall("Person noch nicht da", ["set", A + [i, "auftraggeber"], spaet["id"]], ["set", A + [i, "ab_tag"], 1])
    weg = next(p for p in g["kollegen"] if p.get("bis_tag"))
    fall("Person schon weg", ["set", A + [i, "auftraggeber"], weg["id"]], ["set", A + [i, "ab_tag"], weg["bis_tag"] + 5],
         ["del", ["kollegen", g["kollegen"].index(weg), "nachfolger"]])
    ort = next(o for o in fg.customer_places(g) if o.get("ab_tag", 1) > 1)
    fall("Ort noch nicht da", ["set", A + [i, "ort"], ort["id"]], ["set", A + [i, "ab_tag"], 1])
    fall("Ort unbekannt", ["set", A + [i, "ort"], "mond"])
    fall("Raum am falschen Ort", ["set", A + [i, "ort"], fg.customer_places(g)[0]["id"]])
    s = nr(lambda a: a.get("stelle"))
    fall("Stelle ausserhalb", ["set", A + [s, "stelle"], [-1, -1]])
    raum = next(r for site in [g["gebaeude"]] + [o["gebaeude"] for o in fg.customer_places(g)]
                for r in site["raeume"] if r["id"] == aufgaben[s]["raum"])
    fall("Stelle in Einrichtung", ["add", ["kunden", "orte", 0, "gebaeude", "raeume", 0, "deko"],
                                   {"typ": "tisch", "x": 0, "y": 0, "w": 0.1, "h": 0.1}],
         ["set", A + [s, "stelle"], [raum["x"] + 0.02, raum["y"] + 0.02]])
    fall("Stelle in Wand", ["set", A + [s, "stelle"], [raum["x"] + 0.01, raum["y"] + raum["h"] / 2]])
    fall("Prioritaet", ["set", A + [i, "prioritaet"], "egal"])
    fall("Fachbereich", ["set", A + [i, "cat"], "kochen"])
    fall("Anforderung Thema", ["set", A + [i, "anforderungen"], {"kochen": 10}])
    fall("Anforderung Wert", ["set", A + [i, "anforderungen"], {list(aufgaben[i]["anforderungen"])[0]: 150.5}])
    fall("Achse", ["set", A + [i, "achsen"], ["fachkompetenz", "glueck"]])
    fall("Ticket zu lang", ["set", A + [i, "ticket"], "Ä" * 701])
    fall("Ticket Emoji", ["set", A + [i, "ticket"], "\U0001F600" * 400])
    fall("Voraussetzung", ["set", A + [i, "nach"], "gibtsnicht"])
    fall("Empfaenger", ["set", A + [i, "empfaenger"], "niemand"])
    au = typ("auswahl")
    fall("Auswahl zu wenig", ["set", A + [au, "optionen"], ["a"]], ["set", A + [au, "antwort"], "a"])
    fall("Auswahl doppelt", ["set", A + [au, "optionen"], ["a", "a", "b"]])
    fall("Auswahl lang", ["add", A + [au, "optionen"], "Lang " * 30])
    zu = typ("zuordnung")
    fall("Zuordnung", ["set", A + [zu, "paare"], [["a", "b"], ["a", "c"]]])
    fall("Zuordnung lang", ["add", A + [zu, "paare"], ["x", "Sehr lang " * 15]])
    fall("Suchbegriff", ["add", A + [i, "suchbegriffe"], "xyzzy-gibt-es-nicht"])

    be = typ("bestellung")
    fall("Bestellung leer", ["set", A + [be, "angebote"], []])
    fall("Budget", ["set", A + [be, "budget"], 0])
    fall("Angebot doppelt", ["set", A + [be, "angebote", 1, "id"], aufgaben[be]["angebote"][0]["id"]])
    fall("Bedarf Typ", ["set", A + [be, "bedarf", 0, "typ"], "toaster"])
    fall("Bedarf fuer", ["set", A + [be, "bedarf", 0, "fuer"], ["gibtsnicht"]])
    fall("Angebot Teil", ["set", A + [be, "angebote", 0, "teil"], "gibtsnicht"])
    fall("Angebot Haendler", ["set", A + [be, "angebote", 0, "haendler"], "gibtsnicht"])
    fall("Angebot Preis", ["set", A + [be, "angebote", 0, "preis"], 0])
    fall("Angebot mehrfach", ["add", A + [be, "bedarf"], dict(aufgaben[be]["bedarf"][0], text="Nochmal")])
    fall("Keine Bestellung", ["set", A + [be, "budget"], 1])
    ba = typ("bauteile")
    fall("Steckplatz", ["set", A + [ba, "slots"], ["mainboard", "toaster"]])
    fall("Bauteil unbekannt", ["add", A + [ba, "teile"], "gibtsnicht"])
    fall("Optional", ["set", A + [ba, "optional"], ["laufwerk"]])
    fall("Vorgabe", ["set", A + [ba, "vorgaben", "farbe"], "rot"])
    fall("Ohne Teile", ["set", A + [ba, "teile"], aufgaben[ba]["teile"][:1]])
    bl = typ("bauteile", lambda a: a.get("aus_lager"))
    fall("Lager ohne Bestellung", ["set", A + [bl, "nach"], aufgaben[ba]["id"]])
    fall("Lager Bestellung kaputt", ["set", A + [nr(lambda a: a["id"] == aufgaben[bl]["nach"]), "budget"], 1])
    ra = typ("rack")
    fall("Rack HE", ["set", A + [ra, "schrank", "he"], 2])
    fall("Rack leer", ["set", A + [ra, "geraete"], []])
    fall("Rack Geraet", ["add", A + [ra, "geraete"], "gibtsnicht"])
    fall("Rack Vorgabe", ["set", A + [ra, "vorgaben", "farbe"], 1])
    fall("Rack mindestens", ["set", A + [ra, "vorgaben", "mindestens"], {"toaster": 1}])
    fall("Rack Traglast", ["set", A + [ra, "schrank", "traglast"], 1])
    rl = typ("rack", lambda a: a.get("aus_lager"))
    fall("Rack Lager", ["set", A + [rl, "schrank", "he"], 4])
    fall("Rack Lager ohne Bestellung", ["set", A + [rl, "nach"], aufgaben[ra]["id"]])
    fo = typ("formular")
    fall("Formular Art", ["set", A + [fo, "art"], "steuer"])
    fall("Formular Daten", ["set", A + [fo, "daten"], {}])
    te = typ("terminal")
    fall("Terminal System", ["set", A + [te, "system"], "dos"])
    fall("Terminal Prompt", ["set", A + [te, "prompt"], ""])
    fall("Terminal Prompt lang", ["set", A + [te, "prompt"], "root@ein-sehr-langer-rechner:~#"])
    fall("Terminal ein Schritt", ["set", A + [te, "schritte"], aufgaben[te]["schritte"][:1]])
    fall("Terminal Ziel", ["set", A + [te, "schritte", 0, "ziel"], ""])
    fall("Terminal ein Befehl", ["set", A + [te, "schritte", 0, "befehle"], aufgaben[te]["schritte"][0]["befehle"][:1]])
    fall("Terminal doppelt", ["set", A + [te, "schritte", 0, "befehle", 1, "befehl"], aufgaben[te]["schritte"][0]["befehle"][0]["befehl"]])
    befehle = aufgaben[te]["schritte"][0]["befehle"]
    rechts = next(k for k, c in enumerate(befehle) if c.get("richtig"))
    falsch = next(k for k, c in enumerate(befehle) if not c.get("richtig"))
    fall("Terminal zwei richtig", ["set", A + [te, "schritte", 0, "befehle", falsch, "richtig"], True])
    fall("Terminal gefaehrlich", ["set", A + [te, "schritte", 0, "befehle", rechts, "gefaehrlich"], True])
    fall("Terminal Hinweis", ["del", A + [te, "schritte", 0, "befehle", falsch, "hinweis"]])
    fall("Terminal Befehl lang", ["set", A + [te, "schritte", 0, "befehle", falsch, "befehl"], "ls " * 41])
    fall("Terminal Zeile lang", ["add", A + [te, "schritte", 0, "befehle", rechts, "ausgabe"], "x" * 61])
    fall("Terminal Start lang", ["set", A + [te, "start"], ["y" * 70]])
    fall("Terminal Hinweis lang", ["set", A + [te, "schritte", 0, "befehle", falsch, "hinweis"], "h" * 221])
    di = typ("diagnose")
    fall("Diagnose wenig", ["set", A + [di, "pruefungen"], aufgaben[di]["pruefungen"][:3]])
    fall("Diagnose Text", ["set", A + [di, "pruefungen", 0, "ergebnis"], ""])
    fall("Diagnose lang", ["set", A + [di, "pruefungen", 0, "text"], "t" * 111])
    fall("Diagnose entscheidend", *[["set", A + [di, "pruefungen", k, "entscheidend"], False]
                                    for k in range(len(aufgaben[di]["pruefungen"]))])
    fall("Diagnose Ziel", ["set", A + [di, "ziel_pruefungen"], 99])
    fall("Diagnose Ursachen", ["set", A + [di, "ursachen"], ["a", "b"]])
    fall("Diagnose Massnahme", ["set", A + [di, "massnahme"], "beten"])
    fall("Diagnose Ursache lang", ["add", A + [di, "ursachen"], "u" * 111])
    fall("Diagnose unsicher", ["set", A + [di, "unsicher"], [aufgaben[di]["massnahme"], "irgendwas"]])
    dl = typ("diagnose", lambda a: a.get("aus_lager"))
    fall("Diagnose Lager", ["set", A + [dl, "nach"], aufgaben[di]["id"]])
    fall("Diagnose Lager Typ", ["set", A + [dl, "aus_lager"], ["toaster"]])
    da = typ("diagnose", lambda a: a.get("austausch"))
    fall("Austausch Typ", ["set", A + [da, "austausch", "typ"], "toaster"])
    fall("Austausch Schritte", ["set", A + [da, "austausch", "schritte"], ["a", "b"]])
    fall("Austausch doppelt", ["add", A + [da, "austausch", "falsch"], aufgaben[da]["austausch"]["schritte"][0]])
    fall("Austausch Test", ["del", A + [da, "austausch", "test"]])
    fall("Austausch Test lang", ["set", A + [da, "austausch", "test"], "t" * 221])
    fall("Austausch Merkmal", ["set", A + [da, "austausch", "passt", "farbe"], "rot"])
    fall("Austausch Zwischenfall", ["set", A + [da, "zwischenfall"], True])
    fall("Austausch Schritt lang", ["add", A + [da, "austausch", "falsch"], "s" * 111])
    wa = typ("wartung")
    fall("Wartung wenig", ["set", A + [wa, "pruefpunkte"], aufgaben[wa]["pruefpunkte"][:2]])
    fall("Wartung Text", ["set", A + [wa, "pruefpunkte", 0, "text"], ""])
    fall("Wartung auffaellig", ["set", A + [wa, "pruefpunkte", 0, "auffaellig"], "ja"])
    fall("Wartung lang", ["set", A + [wa, "pruefpunkte", 0, "ergebnis"], "e" * 221])
    fall("Wartung Abschluss", ["set", A + [wa, "abschluss"], ["a", "b"]])
    fall("Wartung Antwort", ["set", A + [wa, "abschluss_antwort"], "keine"])
    fall("Wartung Abschluss lang", ["add", A + [wa, "abschluss"], "a" * 111])
    fall("Zwischenfall Feld", ["set", ["zwischenfaelle", 0, "hilfe"], ""])
    return f


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
    import fisi_game as fg

    original = {name: getattr(fg, name) for name in W3}

    def nur_w3(inhalt):
        try:
            return len(sum((original[name](inhalt) for name in W3), []))
        except Exception as fehler:
            return type(fehler).__name__

    for name in W3:
        setattr(fg, name, lambda inhalt: [])
    echt = fg.validate_game_content(copy.deepcopy(fg.GAME))
    liste = []
    for fall in faelle(fg, fg.GAME):
        inhalt = anwenden(copy.deepcopy(fg.GAME), fall["aenderungen"])
        try:
            fall["probleme"] = fg.validate_game_content(inhalt)
        except Exception as fehler:     # Python bricht ab: C# muss ebenfalls abbrechen
            fall["ausnahme"] = type(fehler).__name__
        fall["w3"] = (nur_w3(anwenden(copy.deepcopy(fg.GAME), fall["aenderungen"])))
        liste.append(fall)
    daten = {"echt": echt, "echt_w3": nur_w3(copy.deepcopy(fg.GAME)), "faelle": liste}
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    leer = [f["name"] for f in liste if not f.get("probleme") and "ausnahme" not in f]
    print("G42 Pruefung: %d Faelle, %d Meldungen, ohne Meldung: %s -> %s"
          % (len(liste), sum(len(f.get("probleme", [])) for f in liste), ", ".join(leer) or "keiner", ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
