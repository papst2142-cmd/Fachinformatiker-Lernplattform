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

Seit W3 ("Spiel-Kern Teil 2") laufen auch die Pruefungen der Firma, Kredite,
des Geschaefts, der Spielwelt, der Erfolge und der Serverfarm mit (vorher
durch leere Listen ersetzt), dazu kaputte Fassungen fuer jede ihrer Meldungen.

Seit Phase 9 W1 (Sammelliste 84) dazu der Teil "format": Gegenproben fuer
text.format(**werte) mit genau den Werten, die validate_game_content einsetzt
(Text und ganze Zahlen). Je Probe der Ausgang "ok", "fehler" (KeyError,
IndexError, ValueError: Python meldet ein Problem) oder "abbruch" (jede andere
Ausnahme: die Pruefung bricht ab). Die Proben sind von Hand gewaehlt und mit
festem Startwert zufaellig zusammengesetzt; dazu dir(str) und dir(int).

Keine Lerndaten; alle Daten- und Einstellungsordner zeigen in einen leeren
Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_pruefung.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_pruefung.json
"""
import atexit
import copy
import json
import os
import shutil
import sys
import tempfile

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


# Diese Meldungen entstehen in Python beim Durchgehen einer Menge (set von Texten): ihre
# Reihenfolge haengt vom Hash-Zufall des Laufs ab. Damit die Datei bei jedem Lauf gleich ist,
# stehen sie hier sortiert; C# gibt sie ebenso sortiert aus.
MENGEN_MELDUNGEN = ("Spiel-Karte: Kundenort '", "Spiel-Karte: kein Gebaeude fuer Wohnung '")


def ordnen(probleme):
    ergebnis = list(probleme)
    for anfang in MENGEN_MELDUNGEN:
        stellen = [i for i, text in enumerate(ergebnis) if text.startswith(anfang)]
        if stellen and stellen != list(range(stellen[0], stellen[-1] + 1)):
            raise SystemExit("Mengen-Meldungen nicht am Stueck: %s" % anfang)
        for i, text in zip(stellen, sorted(ergebnis[i] for i in stellen)):
            ergebnis[i] = text
    return ergebnis


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
    fall("Falschlieferung Titel Zahl", ["set", fl + ["titel"], 5])
    fall("Falschlieferung Titel None", ["set", fl + ["titel"], None])
    fall("Falschlieferung Ticket Zahl", ["set", fl + ["ticket"], 7])
    fall("Falschlieferung Ticket Zeilen Zahl", ["set", fl + ["ticket"], ["Zeile", 7]])
    fall("Falschlieferung Titel Index", ["set", fl + ["titel"], "{ware[0]} fuer {bestellung}"])
    fall("Falschlieferung Titel Index zu weit", ["set", fl + ["titel"], "{ware[1]}"])
    fall("Falschlieferung Titel Methode", ["set", fl + ["titel"], "{ware.upper}"])
    fall("Falschlieferung Titel Methode Angabe", ["set", fl + ["titel"], "{ware.upper:5}"])
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
    faelle_w3(fall, g)
    return f


def faelle_w3(fall, g):
    """W3: Firma, Kredite, Geschaeft, Spielwelt, Erfolge, Serverfarm."""
    F = ["firma"]
    st = F + ["gebaeude", "stufen"]
    fall("Firma fehlt", ["del", F])
    fall("Firma Abschnitt", ["del", F + ["gruendung"]])
    fall("Firma Stufe Nummer", ["set", st + [1, "stufe"], 7])
    fall("Firma Startstufe Preis", ["set", st + [0, "preis"], 100])
    fall("Firma Stufe ohne Plaetze", ["set", st + [1, "plaetze"], g["firma"]["gebaeude"]["stufen"][0]["plaetze"]])
    platz = g["firma"]["gebaeude"]["stufen"][1]["plaetze"][0]
    fall("Firma Platz verstellt", ["set", st + [1, "plaetze", 0], [0.05, 0.05, platz[2]]])
    fall("Firma Platz Raum", ["set", st + [1, "plaetze", 0], [platz[0], platz[1], "nirgends"]])
    fall("Firma Stufen leer", ["set", st, []])
    fall("Firma Vornamen", ["set", F + ["namen", "vornamen"], ["a"]])
    fall("Firma Bewerber", ["set", F + ["bewerbung", "werte_min"], 95])
    fall("Kunden fehlen", ["del", F + ["kunden"]])
    fall("Kunde doppelt", ["set", F + ["kunden", 1, "id"], g["firma"]["kunden"][0]["id"]])
    fall("Kunde Art", ["set", F + ["kunden", 0, "art"], "geizig"])
    fall("Kunde Kontakt", ["set", F + ["kunden", 0, "kontakt"], ""])
    fall("Kunden wenige", ["set", F + ["kunden"], g["firma"]["kunden"][:2]])
    fall("Kundenart Spanne", ["set", F + ["angebote", "arten", "normal", "von"], 120])
    fall("Laune", ["set", F + ["angebote", "laune", "kampfpreis", "chance"], 0.9])
    fall("Zuschlaege leer", ["set", F + ["angebote", "zuschlaege"], []])
    fall("Zuschlag hoch", ["add", F + ["angebote", "zuschlaege"], 150])
    fall("Artikel leer", ["set", F + ["angebote", "artikel"], []])
    fall("Artikel Spanne", ["set", F + ["angebote", "artikel", 0, "preis"], [10, 5]])
    fall("Artikel Menge", ["set", F + ["angebote", "artikel", 0, "menge"], [0, 5]])
    fall("Anfragetext", ["add", F + ["angebote", "texte"], "Hallo {wer}"])
    fall("Anfragetext Angabe", ["add", F + ["angebote", "texte"], "{menge:05d} Stueck {artikel:>10}"])
    fall("Anfragetext Angabe falsch", ["add", F + ["angebote", "texte"], "{menge:s} Stueck"])
    fall("Anfragetext Index Zahl", ["add", F + ["angebote", "texte"], "{lieferzeit[0]} Tage"])
    fall("Anfragetext Index leer", ["add", F + ["angebote", "texte"], "{kontakt[0]}"])
    fall("Anfragetexte leer", ["set", F + ["angebote", "texte"], []])
    tv = F + ["tickets", "vorlagen"]
    fall("Kundenticket doppelt", ["set", tv + [1, "id"], g["firma"]["tickets"]["vorlagen"][0]["id"]])
    fall("Kundenticket Fach", ["set", tv + [0, "cat"], "kochen"])
    fall("Kundenticket Thema", ["set", tv + [0, "thema"], "kochen"])
    fall("Kundenticket Stufe", ["set", tv + [0, "stufe"], "extrem"])
    fall("Kundenticket Kunde", ["set", tv + [0, "kunde"], "niemand"])
    fall("Kundenticket lang", ["set", tv + [0, "text"], "Ä" * 701])
    fall("Kundenticket wenige", ["set", F + ["tickets", "mindestens"], 99999])
    mb = F + ["mitbewerber", "firmen"]
    fall("Mitbewerber fehlen", ["del", F + ["mitbewerber"]])
    fall("Bitweiche fehlt", ["set", mb + [0, "id"], "weichebit"])
    fall("Mitbewerber Name", ["set", mb + [1, "name"], ""])
    fall("Mitbewerber Spanne", ["set", mb + [1, "von"], 50], ["set", mb + [1, "bis"], 10], ["set", mb + [1, "kundenart"], ""])
    fall("Mitbewerber Fach", ["set", mb + [1, "fach"], {"kochen": 1}])
    fall("Mitbewerber Laune", ["set", mb + [1, "laune"], {"wild": {"chance": 2, "text": "x"}}])
    fall("Mitbewerber bietet nie", ["set", mb + [1, "gewicht"], {}])
    fall("Projekt-Bieter", ["set", F + ["mitbewerber", "projekt_bieter"], {"von": 0, "bis": 2}])
    fall("Projekte Abschnitt", ["del", F + ["projekte", "stufen"]])
    fall("Projektarbeit Stufe", ["set", ["projektarbeiten", 0, "schwierigkeit"], "Extrem"])
    fall("Projektarbeit Fach", ["set", ["projektarbeiten", 0, "cat"], "Kochen"])
    fall("Projektstufe Frist", ["set", F + ["projekte", "stufen", "Leicht", "frist"], 0])
    fall("Projektstufe Material", ["set", F + ["projekte", "stufen", "Leicht", "material"], [5, 1]])
    fall("Projektstufe Aufwand Text", ["set", F + ["projekte", "stufen", "Leicht", "aufwand"], "5"])
    fall("Projektstufe Frist Text", ["set", F + ["projekte", "stufen", "Leicht", "frist"], "3"])
    fall("Projektstufe Material Text", ["set", F + ["projekte", "stufen", "Leicht", "material"], ["1", "2"]])
    fall("Projektstufe Material gemischt", ["set", F + ["projekte", "stufen", "Leicht", "material"], [1, "2"]])
    fall("Projektstufe Aufwand wahr", ["set", F + ["projekte", "stufen", "Leicht", "aufwand"], True])
    fall("Projektstufe Frist None", ["set", F + ["projekte", "stufen", "Leicht", "frist"], None])
    fall("Ausschreibungstext", ["add", F + ["projekte", "texte"], "{was}"])
    fall("Buerosatz", ["add", F + ["projekte", "buero"], "{wer}"])
    fall("Ausschreibung kurz", ["set", F + ["projekte", "gilt_tage"], 0])
    sr = F + ["sonderraeume", "raeume"]
    fall("Sonderraum doppelt", ["set", sr + [1, "id"], g["firma"]["sonderraeume"]["raeume"][0]["id"]])
    fall("Sonderraum Name", ["set", sr + [0, "name"], ""])
    fall("Sonderraum Stufe", ["set", sr + [0, "ab_stufe"], 99])
    fall("Sonderraum Stufe Text", ["set", sr + [0, "ab_stufe"], "3"])
    fall("Sonderraum Preis", ["set", sr + [0, "preis"], 0])
    fall("Sonderraum Flaeche", ["set", sr + [0, "raum"], "nirgends"])
    fall("Sonderraum Arbeitsplatz", ["set", sr + [0, "raum"], platz[2]], ["set", sr + [0, "ab_stufe"], 2])
    fall("Sonderraum Vorteil", ["set", sr + [0, "effekt"], {"glueck": 1}])
    fall("Sonderraum ohne Vorteil", ["set", sr + [0, "effekt"], {}])
    fall("Wechsel Person", ["set", F + ["wechsel", 0, "kollege"], "niemand"])
    fall("Wechsel Werte", ["set", F + ["wechsel", 0, "werte"], {}])
    fall("Wechsel Staerke", ["set", F + ["wechsel", 0, "staerken"], ["kochen"]])
    mk = F + ["macken"]
    fall("Macke Text", ["add", mk, "alt"])
    fall("Macke doppelt", ["set", mk + [1, "id"], g["firma"]["macken"][0]["id"]])
    fall("Macke ohne Staerke", ["set", mk + [0, "plus"], {}])
    fall("Macke Wirkung", ["set", mk + [0, "minus"], {"glueck": 1}])
    fall("Macke Person", ["set", mk + [0, "kollege"], "niemand"])
    fall("Macken wenige", ["set", mk, g["firma"]["macken"][:4]])
    fall("Personal Abschnitt", ["del", F + ["personal", "coaching"]])
    fall("Personal Stufen", ["set", F + ["personal", "macken", "stufen"], {"1": 1}])
    fall("Personal Stufen Liste Text", ["set", F + ["personal", "macken", "stufen"], ["1", "2", "3"]])
    fall("Personal Stufen Liste Zahlen", ["set", F + ["personal", "macken", "stufen"], [1, 2, 3]])
    fall("Personal Stufen Liste gemischt", ["set", F + ["personal", "macken", "stufen"], [1, "2", 3]])
    fall("Personal Stufen Text", ["set", F + ["personal", "macken", "stufen"], "123"])
    fall("Personal Stufen Zahl", ["set", F + ["personal", "macken", "stufen"], 3])
    fall("Konflikt Texte", ["set", F + ["personal", "konflikt", "texte"], g["firma"]["personal"]["konflikt"]["texte"][:2]])
    fall("Konflikt Summe", ["set", F + ["personal", "konflikt", "summen"], "{c} und {a}"])
    rh = F + ["rivalitaet", "rueckhol"]
    fall("Rueckhol Chance", ["set", rh + ["chance"], -1])
    fall("Rueckhol Spanne", ["set", rh + ["angebot_plus"], [5, 1]])
    fall("Rueckhol Texte", ["set", rh + ["texte"], g["firma"]["rivalitaet"]["rueckhol"]["texte"][:1]])
    fall("Rueckhol Platzhalter", ["add", rh + ["texte"], "{wer}"])
    gw = F + ["rivalitaet", "gegenwind"]
    fall("Gegenwind Fenster", ["set", gw + ["fenster"], "x"])
    fall("Gegenwind Faktor", ["set", gw + ["vorteil_faktor"], 2])
    fall("Gegenwind Mindestens", ["set", gw + ["mindestens"], 999])
    kj = F + ["markt", "konjunktur", "phasen"]
    fall("Konjunktur Phase", ["del", kj + ["neutral"]])
    fall("Konjunktur Feld", ["del", kj + ["aufschwung", "name"]])
    tr = F + ["markt", "trends"]
    fall("Trend Dauer", ["set", tr + ["dauer"], [5, 1]])
    fall("Trend Pause", ["set", tr + ["pause"], [0, 1]])
    fall("Trend doppelt", ["set", tr + ["liste", 1, "id"], g["firma"]["markt"]["trends"]["liste"][0]["id"]])
    fall("Trend Fach", ["set", tr + ["liste", 0, "cat"], "kochen"])
    fall("Trend Artikel", ["set", tr + ["liste", 0, "artikel", 0, "preis"], [1]])
    fall("Trends wenige", ["set", tr + ["liste"], g["firma"]["markt"]["trends"]["liste"][:1]])
    fi = F + ["filiale"]
    fall("Filiale Voraussetzung", ["set", fi + ["voraussetzung", "mitarbeiter"], -1])
    fall("Filiale Stufe", ["set", fi + ["voraussetzung", "stufe"], 99])
    fall("Filiale Name", ["set", fi + ["name_vorschlag"], ""])
    fall("Filiale Preis", ["set", fi + ["stufen", 0, "preis"], 0])
    fall("Filiale Nummer", ["set", fi + ["stufen", 1, "stufe"], 5])
    fall("Filiale Plaetze", ["set", fi + ["stufen", 1, "plaetze"], g["firma"]["filiale"]["stufen"][0]["plaetze"]])
    fall("Filiale Naehe", ["add", fi + ["naehe", "orte"], "atlantis"])
    fall("Filiale Stufen leer", ["set", fi + ["stufen"], []])
    fall("Lernen Deckel", ["set", F + ["lernen", "deckel"], 999])
    fall("Lernen fehlt", ["del", F + ["lernen", "halb_ab"]])
    fall("Weiterbildung Preis", ["set", F + ["weiterbildung", "fachbereich", "preis"], 0])

    k = ["balancing", "kredite"]
    fall("Kredite fehlen", ["del", k])
    fall("Kredite Bank", ["del", k + ["bank"]])
    fall("Kredite Laufzeit", ["set", k + ["zins", "basis"], {"x": 1}])
    fall("Kredite Zins", ["set", k + ["zins", "basis", "100"], 1.0])
    zb = g["balancing"]["kredite"]["zins"]["basis"]
    fall("Kredite Laufzeit hoch zwei", ["set", k + ["zins", "basis"], dict(zb, **{"\u00b2": 7.0})])
    fall("Kredite Laufzeit arabisch", ["set", k + ["zins", "basis"], dict(zb, **{"\u0663\u0660": 7.0})])
    fall("Kredite Laufzeit Null vorn", ["set", k + ["zins", "basis"], dict(zb, **{"020": 6.5})])
    fall("Kredite Zins Text", ["set", k + ["zins", "basis", "100"], "10.5"])
    fall("Kredite Zins Text kaputt", ["set", k + ["zins", "basis", "100"], "zehn"])
    fall("Kredite Zins None", ["set", k + ["zins", "basis", "100"], None])
    fall("Kredit doppelt", ["set", k + ["pakete", 1, "id"], g["balancing"]["kredite"]["pakete"][0]["id"]])
    fall("Kredit frei", ["set", k + ["pakete", 0, "id"], "frei"])
    fall("Kredit Laufzeit", ["set", k + ["pakete", 0, "laufzeit"], 999])
    fall("Kredit Summe", ["set", k + ["pakete", 0, "summe"], 0])
    fall("Mahnstufe Nummer", ["set", k + ["mahnung", "stufen", 0, "stufe"], 5])
    fall("Mahnstufen leer", ["set", k + ["mahnung", "stufen"], []])
    fall("Mahnstufe Achse", ["set", k + ["mahnung", "stufen", 0, "reputation"], {"glueck": -1}])

    b = ["balancing"]
    fall("Steuern fehlen", ["del", b + ["steuern"]])
    fall("Steuern Finanzamt", ["del", b + ["steuern", "finanzamt"]])
    fall("Steuersatz", ["set", b + ["steuern", "satz"], 150])
    fall("Steuer faellig", ["set", b + ["steuern", "faellig_tage"], 0])
    # Phase 9 W1 (Sammelliste 85): Werte mit falscher Art; Python meldet oder bricht ab, C# genauso
    fall("Steuersatz Text", ["set", b + ["steuern", "satz"], "19"])
    fall("Steuersatz Text Leerzeichen", ["set", b + ["steuern", "satz"], " 19.5 "])
    fall("Steuersatz Text Strich", ["set", b + ["steuern", "satz"], "1_9"])
    fall("Steuersatz Text kaputt", ["set", b + ["steuern", "satz"], "neunzehn"])
    fall("Steuersatz Text unendlich", ["set", b + ["steuern", "satz"], "inf"])
    fall("Steuersatz Text arabisch", ["set", b + ["steuern", "satz"], "\u0661\u0669"])
    fall("Steuersatz wahr", ["set", b + ["steuern", "satz"], True])
    fall("Steuersatz Liste", ["set", b + ["steuern", "satz"], [19]])
    fall("Steuersatz None", ["set", b + ["steuern", "satz"], None])
    fall("Steuer faellig Text", ["set", b + ["steuern", "faellig_tage"], "12"])
    fall("Steuer faellig Text Komma", ["set", b + ["steuern", "faellig_tage"], "12.0"])
    fall("Steuer faellig Kommazahl", ["set", b + ["steuern", "faellig_tage"], 0.5])
    fall("Steuer faellig arabisch", ["set", b + ["steuern", "faellig_tage"], "\u0661\u0662"])
    wb = b + ["marketing", "formen"]
    fall("Werbung doppelt", ["set", wb + [1, "id"], g["balancing"]["marketing"]["formen"][0]["id"]])
    fall("Werbung laufend", ["set", wb + [0, "laufend"], 0])
    fall("Werbung einmalig", ["set", wb + [0, "einmalig"], {}])
    fall("Werbung Wirkung", ["set", wb + [0, "wirkung"], {"glueck": 1}])
    fall("Werbung leer", ["set", wb, []])
    zl = b + ["zertifizierungen", "liste"]
    fall("Zertifizierung doppelt", ["set", zl + [1, "id"], g["balancing"]["zertifizierungen"]["liste"][0]["id"]])
    fall("Zertifizierung Preis", ["set", zl + [0, "preis"], 0])
    fall("Zertifizierung Vorteil", ["set", zl + [0, "vorteil"], {"kochen": 1}])
    gl = b + ["zertifizierungen", "grossauftraege"]
    fall("Grossauftraege Abstand", ["del", gl + ["abstand_tage"]])
    fall("Grossauftrag doppelt", ["set", gl + ["liste", 1, "id"], g["balancing"]["zertifizierungen"]["grossauftraege"]["liste"][0]["id"]])
    fall("Grossauftrag Fach", ["set", gl + ["liste", 0, "cat"], "kochen"])
    fall("Grossauftrag Thema", ["set", gl + ["liste", 0, "thema"], "kochen"])
    fall("Grossauftrag braucht", ["set", gl + ["liste", 0, "braucht"], ["iso1"]])
    fall("Grossauftrag Titel", ["set", gl + ["liste", 0, "titel"], ""])
    fall("Zertifizierungen leer", ["set", zl, []])

    o = ["orte"]
    orte = g["orte"]["orte"]
    zu = next(i for i, x in enumerate(orte) if x.get("ansicht") == "zuhause")
    ku = next(i for i, x in enumerate(orte) if (x.get("frei") or {}).get("kundenort"))
    fall("Karte fehlt", ["del", o])
    fall("Karte Breite", ["set", o + ["welt", "breite"], 0])
    fall("Landschaft", ["add", o + ["welt", "landschaft"], {"typ": "vulkan"}])
    fall("Landschaft Modell", ["add", o + ["welt", "landschaft"], {"typ": "gebaeude", "modell": "ufo"}])
    fall("Ort doppelt", ["set", o + ["orte", 1, "id"], orte[0]["id"]])
    fall("Ort Name", ["set", o + ["orte", 1, "name"], ""])
    fall("Ort Modell", ["set", o + ["orte", 1, "modell"], "ufo"])
    fall("Ort ausserhalb", ["set", o + ["orte", 1, "x"], 999])
    fall("Ort Schild", ["set", o + ["orte", 1, "schild"], [1]])
    fall("Ort Rolle", ["set", o + ["orte", 1, "rolle"], "pirat"])
    fall("Ort Ansicht", ["set", o + ["orte", 1, "ansicht"], "keller"])
    fall("Ort Ansicht Kunde", ["set", o + ["orte", 1, "ansicht"], "kunde:mond"])
    fall("Ort Ansicht Liste", ["set", o + ["orte", 1, "ansicht"], ["kunde:mond"]])
    fall("Ort Ansicht Zahl", ["set", o + ["orte", 1, "ansicht"], 5])
    fall("Ort Ansicht Kommazahl", ["set", o + ["orte", 1, "ansicht"], 0.5])
    fall("Ort Ansicht wahr", ["set", o + ["orte", 1, "ansicht"], True])
    fall("Ort Ansicht Objekt", ["set", o + ["orte", 1, "ansicht"], {"kunde": 1}])
    fall("Ort Ansicht Kunde Liste", ["set", o + ["orte", 1, "vor_gruendung"], {"ansicht": ["zuhause"]}])
    fall("Ort Variante", ["set", o + ["orte", 1, "vor_gruendung"], {"rolle": "pirat", "ansicht": "keller"}])
    fall("Ort ohne Freischaltung", ["set", o + ["orte", 1, "frei"], {}])
    fall("Ort Freischaltung", ["set", o + ["orte", 1, "frei"], {"zaubern": True}])
    fall("Ort Kundenort", ["set", o + ["orte", ku, "frei"], {"kundenort": "mond"}])
    fall("Ort Aufgabe", ["set", o + ["orte", 1, "frei"], {"aufgabe": "gibtsnicht"}])
    fall("Kundenort fehlt", ["set", o + ["orte", ku, "frei"], {"immer": True}])
    fall("Modell leer", ["set", o + ["modelle", "leer"], []])
    m = o + ["modelle", "stellwerk", 0]
    fall("Modell Teil", ["add", o + ["modelle", "stellwerk"], {"art": "ufo"}])
    fall("Modell Block", ["set", m + ["w"], 0])
    fall("Modell Dach", ["set", m + ["dach"], "kuppel"])
    fall("Modell Angabe", ["set", m + ["glitzer"], True])
    fall("Modell Sonderraum", ["set", m + ["raum"], "keller"])
    fall("Modell Wohnung", ["set", m + ["wohnung"], ["schloss"]])
    fall("Modell Aufgabe", ["set", m + ["erledigt"], "gibtsnicht"])
    fall("Zuhause ohne Gebaeude", ["set", o + ["modelle", "nur_schild"], [{"art": "schild"}]],
         ["set", o + ["orte", zu, "modell"], "nur_schild"])
    fall("Zuhause Block spaeter", ["set", o + ["modelle", "spaet"], [{"art": "block", "w": 1, "t": 1, "h": 1, "ab_stufe": 2},
                                                                   {"art": "block", "w": 1, "t": 1, "h": 1, "gegruendet": True},
                                                                   {"art": "block", "w": 1, "t": 1, "h": 1, "wohnung": ["schloss"]}]],
         ["set", o + ["orte", zu, "modell"], "spaet"])

    e = ["erfolge"]
    fall("Erfolge fehlen", ["del", e])
    fall("Erfolg doppelt", ["set", e + ["erfolge", 1, "id"], g["erfolge"]["erfolge"][0]["id"]])
    fall("Erfolg Gruppe", ["set", e + ["erfolge", 0, "gruppe"], "sport"])
    fall("Erfolg Wert", ["set", e + ["erfolge", 0, "wert"], "glueck"])
    fall("Erfolg Bild", ["set", e + ["erfolge", 0, "bild"], "ufo"])
    fall("Erfolg Ort", ["set", e + ["erfolge", 0, "ort"], "mond"])
    fall("Erfolg Bestwert", ["set", e + ["erfolge", 0, "bestwert"], "glueck"])
    fall("Erfolg Moment", ["set", e + ["erfolge", 0, "moment_text"], "{wer} am Tag {tag}"])
    # Phase 9 W1 (Sammelliste 84): Platzhalter mit Format-Angabe, Index, Attribut und Verschachtelung
    fall("Erfolg Moment Angabe", ["set", e + ["erfolge", 0, "moment_text"], "{ziel:d} am Tag {tag}"])
    fall("Erfolg Moment Index", ["set", e + ["erfolge", 0, "moment_text"], "{ziel[0]} am Tag {tag}"])
    fall("Erfolg Moment Zahl Index", ["set", e + ["erfolge", 0, "moment_text"], "{tag[0]}"])
    fall("Erfolg Moment Attribut", ["set", e + ["erfolge", 0, "moment_text"], "{ziel.gibtsnicht}"])
    fall("Erfolg Moment Attribut gut", ["set", e + ["erfolge", 0, "moment_text"], "{tag.real:03d}. Tag"])
    fall("Erfolg Moment verschachtelt", ["set", e + ["erfolge", 0, "moment_text"], "{ziel:>{tag}}"])
    fall("Erfolg Moment Umwandlung", ["set", e + ["erfolge", 0, "moment_text"], "{ziel!q}"])
    fall("Erfolg Moment Zahl", ["set", e + ["erfolge", 0, "moment_text"], 5])
    fall("Erfolg ohne Stufen", ["set", e + ["erfolge", 0, "stufen"], []])
    fall("Erfolg Stufen absteigend", ["set", e + ["erfolge", 0, "stufen"], [{"stufe": "gold", "ab": 1}, {"stufe": "bronze", "ab": 2}]])
    fall("Erfolg Stufe Platin", ["set", e + ["erfolge", 0, "stufen"], [{"stufe": "platin"}]])
    fall("Erfolg Stufen gleich", ["set", e + ["erfolge", 0, "stufen"], [{"stufe": "bronze", "ab": 3}, {"stufe": "silber", "ab": 3.0}]])
    fall("Erfolg vier Stufen", ["set", e + ["erfolge", 0, "stufen"], [{"stufe": "bronze", "ab": i} for i in range(1, 5)]])
    fall("Bestwert Wert", ["set", e + ["bestwerte", 0, "id"], "glueck"])
    fall("Bestwert besser", ["set", e + ["bestwerte", 0, "besser"], "egal"])

    s = ["serverfarm"]
    r = ["balancing", "serverfarm"]
    a = s + ["aufgaben"]
    auf = g["serverfarm"]["aufgaben"]

    def nr(typ):
        return next(i for i, x in enumerate(auf) if x["typ"] == typ)
    fall("Serverfarm fehlt", ["del", s])
    fall("Serverfarm Regel", ["del", r + ["bonus"]])
    fall("Serverfarm Raum", ["set", r + ["freischaltung", "raum"], "keller"])
    fall("Serverfarm Zertifizierung", ["add", r + ["freischaltung", "zertifizierungen"], "iso1"])
    fall("Serverfarm Verzug", ["set", r + ["verzug"], [[0.5, 0], [0.4, 2]]])
    fall("Serverfarm Aussehen", ["set", s + ["personen", 0, "aussehen", "haut"], "gruen"])
    fall("Serverfarm Aussehen Teil", ["set", s + ["personen", 0, "aussehen", "fluegel"], "ja"])
    fall("Serverfarm Phase doppelt", ["set", s + ["phasen", 2, "id"], "planung"])
    fall("Serverfarm Phase fehlt", ["set", s + ["phasen", 3, "id"], "aufbau"])
    fall("Serverfarm soll_tag", ["set", s + ["phasen", 1, "soll_tag"], 99])
    fall("Serverfarm Thema", ["add", s + ["phasen", 1, "themen"], "kochen"])
    fall("Serverfarm Team ohne Themen", ["set", s + ["phasen", 1, "themen"], []])
    fall("Serverfarm Teile Art", ["set", s + ["teile"], [t for t in g["serverfarm"]["teile"] if t["typ"] != "nic"]])
    fall("Serverfarm Teil", ["set", s + ["teile", 0, "preis"], 0])
    fall("Serverfarm Teil doppelt", ["set", s + ["teile", 1, "id"], g["serverfarm"]["teile"][0]["id"]])
    fall("Serverfarm Aufgabe Feld", ["set", a + [0, "hilfe"], ""])
    fall("Serverfarm Aufgabe Ticket", ["set", a + [0, "ticket"], ""])
    fall("Serverfarm Aufgabe doppelt", ["set", a + [1, "id"], auf[0]["id"]])
    fall("Serverfarm Aufgabe Typ", ["set", a + [0, "typ"], "raten"])
    fall("Serverfarm Aufgabe Phase", ["set", a + [0, "phase"], "pause"])
    fall("Serverfarm Aufgabe Person", ["set", a + [0, "person"], "niemand"])
    fall("Serverfarm Aufgabe Fach", ["set", a + [0, "cat"], "kochen"])
    fall("Serverfarm Anforderung", ["set", a + [0, "anforderungen"], {"kochen": 10}])
    fall("Serverfarm Anforderung Wert", ["set", a + [0, "anforderungen"], {"ipv4": 150}])
    fall("Serverfarm Ticket lang", ["set", a + [0, "ticket"], "x" * 701])
    au = nr("auswahl")
    fall("Serverfarm Auswahl", ["set", a + [au, "optionen"], ["a"]])
    fall("Serverfarm Auswahl lang", ["add", a + [au, "optionen"], "Lang " * 30])
    zo = nr("zuordnung")
    fall("Serverfarm Zuordnung", ["set", a + [zo, "paare"], [["a", "b"], ["a", "c"]]])
    fall("Serverfarm Angebot", ["del", s + ["angebot"]])
    fall("Serverfarm Suchbegriff", ["add", a + [0, "suchbegriffe"], "xyzzy-gibt-es-nicht"])
    fall("Serverfarm Formular", ["set", a + [nr("formular"), "formular"], "zauber"])
    fall("Serverfarm Rack", ["set", a + [nr("rack"), "geraete"], ["gibtsnicht"]])
    fall("Serverfarm Terminal", ["set", a + [nr("terminal"), "schritte"], []])
    fall("Serverfarm Diagnose", ["set", a + [nr("diagnose"), "ursache"], "keine"])
    fall("Serverfarm Bestellung", ["set", a + [nr("bestellung"), "angebote"], []])
    be = a + [nr("bestueckung"), "bestueckung"]
    vorlage = g["serverfarm"]["aufgaben"][nr("bestueckung")]["bestueckung"]["vorlagen"][0]
    fall("Bestueckung ohne Vorlagen", ["set", be + ["vorlagen"], []])
    fall("Bestueckung Rack", ["set", be + ["rack"], "gibtsnicht"])
    fall("Bestueckung Feld", ["del", be + ["vorlagen", 0, "name"]])
    fall("Bestueckung Geraet", ["set", be + ["vorlagen", 0, "geraet"], "toaster"])
    fall("Bestueckung RAID", ["set", be + ["vorlagen", 0, "soll"], dict(vorlage.get("soll") or {}, raid=["RAID 7"])])
    fall("Bestueckung ohne Loesung", ["set", be + ["vorlagen", 0, "schaechte"], 0])
    fall("Bestueckung zu wenige Geraete", ["set", be + ["vorlagen", 0, "anzahl"], 99])


# Werte wie in fisi_game.py (Z. 786-787, 10068, 10150, 10155, 10360, 10384, 13089)
FORMAT_WERTE = {
    "falschlieferung": {"bestellung": "x", "ware": "y"},
    "anfrage": {"kontakt": "", "menge": 1, "artikel": "", "lieferzeit": 1},
    "ausschreibung": {"kunde": "", "auftrag": ""},
    "buero": {"phase": "", "titel": "", "kunde": ""},
    "konflikt": {"a": "A", "b": "B", "a_von": "As", "b_von": "Bs"},
    "rueckhol": {"firma": "F", "name": "N", "plus": 10},
    "moment": {"ziel": "", "tag": 1, "spieler": "", "firma": "", "filiale": ""},
}

FORMAT_HAND = [
    "{kontakt:d}", "{kontakt[0]}", "{menge[0]}", "{menge:d}", "{menge:s}", "{menge:.2}", "{menge:.2f}",
    "{menge:,}", "{menge:_x}", "{menge:,x}", "{menge:c}", "{menge:+c}", "{menge:#x}", "{menge:=5}", "{menge:05}",
    "{menge:z}", "{menge:zf}", "{kontakt:z}", "{kontakt:=5}", "{kontakt:05}", "{kontakt:+}", "{kontakt:#}",
    "{kontakt:,}", "{kontakt:_}", "{kontakt:.}", "{kontakt:.3}", "{kontakt:ss}", "{kontakt:x<5}", "{kontakt:\U0001F600<5}",
    "{kontakt!r}", "{kontakt!a}", "{kontakt!s}", "{kontakt!x}", "{kontakt!}", "{kontakt!rs}", "{kontakt!r:5}",
    "{kontakt.upper}", "{kontakt.upper:5}", "{kontakt.foo}", "{kontakt.__doc__:5}", "{kontakt.__class__}",
    "{menge.real}", "{menge.imag:x}", "{menge.denominator}", "{menge.foo}", "{menge.real.real}",
    "{kontakt[]}", "{kontakt.}", "{kontakt..x}", "{kontakt[0]x}", "{kontakt[0}", "{kontakt[}]}", "{kontakt[{]}",
    "{kontakt{x}}", "{}", "{0}", "{.x}", "{[0]}", "{kontakt]}", "{kontakt[0]]}", "{artikel:{menge}}",
    "{artikel:>{lieferzeit}}", "{artikel:{menge:x}}", "{artikel:{menge}{menge}}", "{artikel:{kontakt!r}}",
    "{artikel:{menge:{menge}}}", "{artikel:{}}", "{artikel:{{}}}", "{artikel:}}", "{artikel:{}", "{", "}",
    "{{", "}}", "{{kontakt}}", "x{kontakt}y", "{kontakt:99999999999999999999}", "{kontakt:.99999999999999999999}",
    "{kontakt[99999999999999999999]}", "{kontakt[\u0663]}", "{kontakt[\u00b2]}", "{kontakt[-1]}",
    "{bestellung[0]}", "{bestellung[1]}", "{bestellung[\u0660]}", "{ware[0][0]}", "{ware[0].upper}",
    "{plus:x}", "{plus:#b}", "{plus:_b}", "{plus:c}", "{plus:n}", "{plus:,n}", "{plus:%}", "{plus:e}",
    "{tag:{tag}}", "{tag:0{tag}}", "{tag:{tag}d}", "{ziel:{tag}.{tag}}",
]

FORMAT_STUECKE = ["{", "}", "{", "}", "[", "]", ".", ":", "!", "0", "1", "2", "5", "r", "s", "a", "x", "d",
                  "b", "c", "X", "o", "n", "<", ">", "=", "^", "+", "-", " ", "#", ",", "_", "z", "%", "f",
                  "e", "\u0663", "\u00b2", "real", "imag", "upper"]

FORMAT_TEILE = ["[0]", "[1]", "[\u0663]", "[x]", "[]", ".real", ".imag", ".upper", ".foo", ".", "!r", "!s",
                "!a", "!x", "!", ":", ":5", ":>5", ":<3", ":^7", ":05", ":x<4", ":0=5", ":+", ": ", ":-",
                ":#x", ":#o", ":#b", ":X", ":c", ":d", ":n", ":,", ":_", ":_x", ":,d", ":.2", ":.", ":e",
                ":f", ":%", ":s", ":z", ":#", ":=", ":{", "}", "{", "[{]", "[}]", "]", "[0]x"]


def format_ausgang(text, werte):
    try:
        text.format(**werte)
        return "ok"
    except (KeyError, IndexError, ValueError):
        return "fehler"
    except Exception:       # die Pruefung in Python bricht ab
        return "abbruch"


def format_proben():
    import random
    zufall = random.Random(20261029)
    proben = []
    for gruppe, werte in FORMAT_WERTE.items():
        for text in FORMAT_HAND:
            proben.append([gruppe, text])
    for _ in range(3000):
        gruppe = zufall.choice(sorted(FORMAT_WERTE))
        namen = list(FORMAT_WERTE[gruppe])
        stuecke = FORMAT_STUECKE + namen * 4 + ["{%s}" % n for n in namen] + ["{%s:" % n for n in namen] \
            + ["{%s[" % n for n in namen]
        proben.append([gruppe, "".join(zufall.choice(stuecke) for _ in range(zufall.randint(1, 8)))])
    for _ in range(3000):
        gruppe = zufall.choice(sorted(FORMAT_WERTE))
        namen = list(FORMAT_WERTE[gruppe])
        text = "{" + zufall.choice(namen + ["", "0", "q"]) \
            + "".join(zufall.choice(FORMAT_TEILE) for _ in range(zufall.randint(0, 3))) \
            + zufall.choice(["}", "}", "}", "", "}}"]) + zufall.choice(["", "x", "{%s}" % zufall.choice(namen), "}"])
        proben.append([gruppe, text])
    for probe in proben:
        probe.append(format_ausgang(probe[1], FORMAT_WERTE[probe[0]]))
    return {"werte": FORMAT_WERTE, "attribute": {"str": dir(str), "int": dir(int)}, "proben": proben}


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
    import fisi_game as fg

    echt = ordnen(fg.validate_game_content(copy.deepcopy(fg.GAME)))
    liste = []
    for fall in faelle(fg, fg.GAME):
        inhalt = anwenden(copy.deepcopy(fg.GAME), fall["aenderungen"])
        try:
            fall["probleme"] = ordnen(fg.validate_game_content(inhalt))
        except Exception as fehler:     # Python bricht ab: C# muss ebenfalls abbrechen
            fall["ausnahme"] = type(fehler).__name__
        liste.append(fall)
    daten = {"echt": echt, "faelle": liste, "format": format_proben()}
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    leer = [f["name"] for f in liste if not f.get("probleme") and "ausnahme" not in f]
    print("G42 Pruefung: %d Faelle, %d Meldungen, ohne Meldung: %s -> %s"
          % (len(liste), sum(len(f.get("probleme", [])) for f in liste), ", ".join(leer) or "keiner", ziel))
    ausgaenge = [p[2] for p in daten["format"]["proben"]]
    print("G42 Format: %d Proben, ok %d, fehler %d, abbruch %d" % (len(ausgaenge), ausgaenge.count("ok"),
                                                                 ausgaenge.count("fehler"), ausgaenge.count("abbruch")))
    return 0


if __name__ == "__main__":
    sys.exit(main())
