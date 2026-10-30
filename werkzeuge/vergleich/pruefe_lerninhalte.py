#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Phase 9 W2 (Weisung P9-5 Punkt 1, Plan Phase 9 Abschnitt 5): unabhaengige
Gegenkontrolle der Ladepruefung von inhalte_csharp/lernen. Erzeugt die
Vergleichswerte G52 (tests/Vergleich/lerninhalte_pruefung.json).

Geschrieben nur aus der Weisung, dem Plan (Abschnitte 5 und 12) und den
Pruefregeln von fisi_core.validate_content; liest keinen C#-Code. Aus der
Python-Linie v0.63.2 nimmt es nur die Lerninhalte (inhalte/) und die Liste
QUIZ_QUESTIONS (mit den im Programm erzeugten Aufgaben, die ebenfalls
eindeutig bleiben muessen).

Inhalt:
  echt    - Meldungen fuer die echten Dateien in inhalte_csharp/lernen
  probe   - Meldungen fuer die Probe-Inhalte in tests/Lerninhalte/probe
  faelle  - je Fehlerfall aus tests/Lerninhalte/faelle.json die Meldungen
  sichten - je Fachrichtung die Zahl der sichtbaren Inhalte (echt und Probe)
  einteilung - Zahl der Eintraege je Art und Wert in einteilung_bisher.json

Aufruf (Python 3.12, nur Standardbibliothek):
  python3 -I werkzeuge/vergleich/pruefe_lerninhalte.py <python_linie> <repo> <ziel.json>
"""
import atexit
import copy
import json
import os
import shutil
import sys
import tempfile

FACHRICHTUNGEN = ["si", "ae", "dpa", "dv"]
ABSCHNITT = {"si": "C", "ae": "B", "dpa": "D", "dv": "E"}
# Fachrichtung.cs (Spezifikation P4-5, Tabelle 1): Art und Kurzname der Bereiche b1/b2
BEREICHE = {
    "ae": {"b1": ("softwareprodukt", "Softwareprodukt"), "b2": ("algorithmen", "Algorithmen")},
    "dpa": {"b1": ("prozessanalyse", "Prozessanalyse"), "b2": ("datenqualitaet", "Datenqualität")},
    "dv": {"b1": ("diagnose", "Diagnose"), "b2": ("betrieb", "Betrieb")},
}
KATEGORIEN = {"netzwerk", "sicherheit", "systeme", "wirtschaft", "datenbanken"}
FB_REIHE = ["netzwerk", "sicherheit", "systeme", "wirtschaft", "datenbanken"]
AP2_THEMEN = ["Subnetting & Routing", "IT-Sicherheit", "Storage & RAID", "Netzwerkdesign", "Wirtschaft & Beratung"]
THEMEN_BLOCK = {"Virtualisierung": "Storage & RAID", "Projektmanagement": "Wirtschaft & Beratung",
                "Systemadministration": "Storage & RAID", "Automatisierung & Skripte": "Storage & RAID",
                "Arbeitsplatz einrichten: Bedarf, Beschaffung, Übergabe": "Projektplanung"}
LISTEN = [  # Datei, Pflichtfelder, Auswahl, Tabelle in umbenennungen.json, Felder, die Listen sein muessen
    ("karteikarten", ("cat", "q", "a", "a_full", "options"), True, "karte", ("rahmenplan", "options")),
    ("quizfragen", ("cat", "q", "a", "exp", "options"), True, "quiz", ("rahmenplan", "options")),
    ("szenarien_ap2", ("cat", "title", "theme", "text", "solution"), False, None, ("rahmenplan",)),
    ("projektarbeiten", ("cat", "title", "branche", "schwierigkeit", "ausgangssituation", "auftrag",
                         "rahmenbedingungen", "aufgaben", "hinweise"), False, None,
     ("rahmenplan", "rahmenbedingungen", "aufgaben", "hinweise")),
]
# Phase 10 W2 (Weisung P10-1): neue Fragen fuers Fachgespraech in lernen/fachgespraech.json
FACHFRAGE_FELDER = ("id", "q", "hinweis")


def lesen(pfad):
    with open(pfad, encoding="utf-8") as datei:
        return json.load(datei)


def py(wert):
    """'%s' % wert fuer die Meldungen; Listen und Objekte als kompaktes JSON (Phase 10 W2, Sammelliste 173)."""
    if isinstance(wert, (list, dict)):
        return json.dumps(wert, ensure_ascii=False, separators=(",", ":"))
    if wert is None:
        return "None"
    if isinstance(wert, bool):
        return "True" if wert else "False"
    return str(wert)


def text_oder_leer(wert):
    return wert if isinstance(wert, str) and wert else ""


def schluessel(eintrag):
    return (text_oder_leer(eintrag.get("q")) or text_oder_leer(eintrag.get("title"))).strip().lower()


def als_liste(wert):
    return wert if isinstance(wert, list) else []


def pools(zusatz, fr):
    if fr == "si":
        return list(AP2_THEMEN)
    teil = ((zusatz.get("pruefungsbereiche") or {}).get(fr) or {})
    erg = []
    for rolle in ("b1", "b2"):
        themen = (teil.get(rolle) or {}).get("themen")
        if isinstance(themen, list):
            erg.extend(py(t) for t in themen)
    return erg


def pruefen(bisher, quiz_alle, lernen, umbenennungen):
    """lernen: {name: inhalt} der Dateien in inhalte_csharp/lernen (fehlt = nicht da)."""
    zusatz = lernen.get("rahmenplan_zusatz") or {}
    themen_bisher = bisher["themen"]["themen"]
    themen_neu = (lernen.get("themen") or {}).get("themen") or {}
    alle_themen = {fb: [t["id"] for t in themen_bisher.get(fb, [])] + [t["id"] for t in themen_neu.get(fb, [])]
                   for fb in FB_REIHE}
    neue_themen = {}
    for fb in FB_REIHE:
        for t in themen_neu.get(fb, []):
            fr = t.get("fachrichtung")
            neue_themen[t["id"]] = fr if isinstance(fr, str) else None
    punkte_plan = bisher["rahmenplan"]["punkte"]
    punkt_ids = {p["id"] for p in punkte_plan}
    meldungen = []
    for datei, felder, auswahl, tabelle, listenfelder in LISTEN:
        alte = quiz_alle if datei == "quizfragen" else bisher[datei]
        gesehen = {schluessel(e) for e in alte}
        alte_texte = {t.strip().lower() for t in (umbenennungen.get(tabelle) or {})} if tabelle else set()
        for k, e in enumerate(lernen.get(datei) or [], start=1):
            wo = "lernen/%s.json Nr. %d" % (datei, k)
            for feld in listenfelder:
                if feld in e and not isinstance(e[feld], list):
                    meldungen.append("%s: Feld '%s' ist keine Liste" % (wo, feld))
            for feld in felder + ("fachrichtung",):
                if not e.get(feld):
                    meldungen.append("%s: Feld '%s' fehlt oder ist leer" % (wo, feld))
            fr = e.get("fachrichtung")
            gueltig = fr if isinstance(fr, str) and fr in FACHRICHTUNGEN else ""
            if fr and not gueltig:
                meldungen.append("%s: unbekannte Fachrichtung '%s'" % (wo, py(fr)))
            s = schluessel(e)
            if s in gesehen:
                meldungen.append("%s: doppelt vorhanden (%s)" % (wo, s[:60]))
            gesehen.add(s)
            if tabelle and s in alte_texte:
                meldungen.append("%s: Fragetext steht als alter Text in umbenennungen.json" % wo)
            if auswahl:
                optionen = als_liste(e.get("options"))
                if len(optionen) != 4 or len({json.dumps(o, ensure_ascii=False) for o in optionen}) != 4:
                    meldungen.append("%s: braucht genau 4 verschiedene Antworten" % wo)
                if e.get("a") not in optionen:
                    meldungen.append("%s: richtige Antwort steht nicht in den Optionen" % wo)
            thema = e.get("thema")
            if not (isinstance(thema, str) and thema in alle_themen.get(e.get("cat"), [])):
                meldungen.append("%s: Thema '%s' fehlt oder gehoert nicht zum Fachbereich" % (wo, py(thema)))
            elif thema in neue_themen and neue_themen[thema] != gueltig:
                meldungen.append("%s: Thema '%s' gehoert zu einer anderen Fachrichtung" % (wo, thema))
            if datei == "szenarien_ap2":
                theme = e.get("theme")
                if gueltig == "si":
                    passt = isinstance(theme, str) and THEMEN_BLOCK.get(theme, theme) in AP2_THEMEN
                else:
                    passt = isinstance(theme, str) and theme in pools(zusatz, gueltig)
                if not passt:
                    meldungen.append("%s: unbekanntes Thema '%s' (Themen der Prüfungsbereiche)" % (wo, py(theme)))
            if datei == "projektarbeiten" and len(als_liste(e.get("aufgaben"))) != len(als_liste(e.get("hinweise"))):
                meldungen.append("%s: je Aufgabe wird genau ein Hinweis gebraucht" % wo)
            for punkt in als_liste(e.get("rahmenplan")):
                if punkt not in punkt_ids:
                    meldungen.append("%s: unbekannter Rahmenplan-Punkt %s" % (wo, py(punkt)))

    fragen_alt = bisher["fachgespraech"]["fragen"]
    kennungen = {f["id"] for f in fragen_alt}
    gesehen = {f["q"].strip().lower() for f in fragen_alt}
    for k, e in enumerate((lernen.get("fachgespraech") or {}).get("fragen") or [], start=1):
        wo = "lernen/fachgespraech.json Nr. %d" % k
        for feld in FACHFRAGE_FELDER + ("fachrichtung",):
            if not e.get(feld):
                meldungen.append("%s: Feld '%s' fehlt oder ist leer" % (wo, feld))
        fr = e.get("fachrichtung")
        if fr and not (isinstance(fr, str) and fr in FACHRICHTUNGEN):
            meldungen.append("%s: unbekannte Fachrichtung '%s'" % (wo, py(fr)))
        cat = e.get("cat")
        if cat and not (isinstance(cat, str) and cat in KATEGORIEN):
            meldungen.append("%s: unbekannter Fachbereich '%s'" % (wo, py(cat)))
        if e.get("id"):
            kennung = py(e["id"])
            if kennung in kennungen:
                meldungen.append("%s: Kennung %s doppelt" % (wo, kennung))
            kennungen.add(kennung)
        s = text_oder_leer(e.get("q")).strip().lower()
        if s in gesehen:
            meldungen.append("%s: doppelt vorhanden (%s)" % (wo, s[:60]))
        gesehen.add(s)

    gesehen = set()
    for fb in FB_REIHE:
        for tid in alle_themen[fb]:
            neu = tid in neue_themen
            if tid in gesehen and neu:
                meldungen.append("lernen/themen.json %s: Thema doppelt" % tid)
            gesehen.add(tid)
            if neu and neue_themen[tid] not in FACHRICHTUNGEN:
                meldungen.append("lernen/themen.json %s: unbekannte Fachrichtung '%s'" % (tid, py(neue_themen[tid])))

    datei = "rahmenplan_zusatz.json"
    lernfelder = {f["id"] for f in bisher["rahmenplan"]["lernfelder"]}
    for f in zusatz.get("lernfelder") or []:
        fid, fr = py(f.get("id")), py(f.get("fachrichtung"))
        if fid in lernfelder:
            meldungen.append("%s: Lernfeld %s doppelt" % (datei, fid))
        lernfelder.add(fid)
        if fr == "si" or fr not in ABSCHNITT:
            meldungen.append("%s: Lernfeld %s: unbekannte Fachrichtung %s" % (datei, fid, fr))
        elif py(f.get("abschnitt")) != ABSCHNITT[fr]:
            meldungen.append("%s: Lernfeld %s: Abschnitt passt nicht zur Fachrichtung" % (datei, fid))
        if not f.get("titel") or not f.get("beleg"):
            meldungen.append("%s: Lernfeld %s: Titel oder Beleg fehlt" % (datei, fid))
    zuordnung = zusatz.get("ap2") or {}
    pruefung = zusatz.get("pruefungsbereiche") or {}
    for fr in ("ae", "dpa", "dv"):
        abschnitt = ABSCHNITT[fr]
        erlaubt = [BEREICHE[fr]["b1"][1], BEREICHE[fr]["b2"][1], "Projekt", "WiSo"]
        eigene = zuordnung.get(fr) or {}
        for pid, eintrag in eigene.items():
            punkt = next((p for p in punkte_plan if p["id"] == pid), None)
            if punkt is None or punkt["abschnitt"] != abschnitt:
                meldungen.append("%s: AP2 %s %s: Punkt fehlt oder gehoert nicht zu Abschnitt %s" % (datei, fr, pid, abschnitt))
            bereiche = [py(b) for b in (eintrag or {}).get("bereiche") or []]
            if not bereiche:
                meldungen.append("%s: AP2 %s %s: ohne Prüfungsbereich" % (datei, fr, pid))
            for b in bereiche:
                if b not in erlaubt:
                    meldungen.append("%s: AP2 %s %s: unbekannter Prüfungsbereich %s" % (datei, fr, pid, b))
            if not (eintrag or {}).get("beleg"):
                meldungen.append("%s: AP2 %s %s: Beleg fehlt" % (datei, fr, pid))
        for punkt in punkte_plan:
            if punkt["abschnitt"] == abschnitt and punkt["id"] not in eigene:
                meldungen.append("%s: AP2 %s: Punkt %s ohne Zuordnung" % (datei, fr, punkt["id"]))
        themen = set()
        for rolle in ("b1", "b2"):
            bereich = (pruefung.get(fr) or {}).get(rolle)
            if not isinstance(bereich, dict):
                meldungen.append("%s: Prüfungsbereich %s %s fehlt" % (datei, fr, rolle))
                continue
            art, kurz = BEREICHE[fr][rolle]
            if py(bereich.get("art")) != art or py(bereich.get("kurz")) != kurz:
                meldungen.append("%s: Prüfungsbereich %s %s: Art oder Kurzname passt nicht" % (datei, fr, rolle))
            liste = [py(t) for t in bereich.get("themen")] if isinstance(bereich.get("themen"), list) else []
            if len(liste) != 4 or len(set(liste)) != 4 or any(t == "" for t in liste):
                meldungen.append("%s: Prüfungsbereich %s %s: braucht genau 4 verschiedene Themen" % (datei, fr, rolle))
            for t in liste:
                doppelt = t in themen
                themen.add(t)
                if doppelt or t in AP2_THEMEN or t in THEMEN_BLOCK:
                    meldungen.append("%s: Prüfungsbereich %s %s: Thema %s doppelt" % (datei, fr, rolle, t))
            if not bereich.get("beleg"):
                meldungen.append("%s: Prüfungsbereich %s %s: Beleg fehlt" % (datei, fr, rolle))
    return meldungen


def sichten(bisher, quiz_alle, lernen):
    """Je Fachrichtung: sichtbare Inhalte (gemeinsam plus eigene; ohne Eintrag gilt si)."""
    einteilung = lernen.get("einteilung_bisher") or {}

    def wert(art, s):
        return (einteilung.get(art) or {}).get(s, "si")

    erg = {}
    for fr in FACHRICHTUNGEN:
        def zahl(art, eintraege, feld):
            return sum(1 for e in eintraege if wert(art, e.get(feld)) in ("gemeinsam", fr))

        def neue(datei):
            return sum(1 for e in lernen.get(datei) or [] if e.get("fachrichtung") == fr)

        erzeugt = quiz_alle[len(bisher["quizfragen"]):]   # die im Programm erzeugten Subnetz-Aufgaben (Weisung P9-6)
        erg[fr] = {
            "karteikarten": zahl("karteikarten", bisher["karteikarten"], "q") + neue("karteikarten"),
            "quizfragen": zahl("quizfragen", bisher["quizfragen"], "q") + zahl("quizfragen_erzeugt", erzeugt, "q") + neue("quizfragen"),
            "szenarien_ap1": zahl("szenarien_ap1", bisher["szenarien_ap1"], "title"),
            "szenarien_ap2": zahl("szenarien_ap2", bisher["szenarien_ap2"], "title") + neue("szenarien_ap2"),
            "projektarbeiten": zahl("projektarbeiten", bisher["projektarbeiten"], "title") + neue("projektarbeiten"),
            "fachgespraech": sum(1 for f in bisher["fachgespraech"]["fragen"] if wert("fachgespraech", f["id"]) in ("gemeinsam", fr))
            + sum(1 for f in (lernen.get("fachgespraech") or {}).get("fragen") or [] if f.get("fachrichtung") == fr),
        }
    return erg


def setzen(daten, pfad, art, wert):
    ziel = daten
    for teil in pfad[:-1]:
        ziel = ziel[teil]
    if art == "setze":
        ziel[pfad[-1]] = copy.deepcopy(wert)
    elif art == "entferne":
        del ziel[pfad[-1]]
    else:
        raise SystemExit("unbekannte Art " + art)


def main():
    if len(sys.argv) != 4:
        print(__doc__)
        return 2
    linie, repo, ziel = sys.argv[1:]
    tmp = tempfile.mkdtemp(prefix="lerninhalte_")
    atexit.register(shutil.rmtree, tmp, True)
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, linie)
    import fisi_core as core
    quiz_alle = [{"q": q["q"]} for q in core.QUIZ_QUESTIONS]

    inhalte = os.path.join(repo, "inhalte")
    bisher = {name: lesen(os.path.join(inhalte, name + ".json"))
              for name in ("karteikarten", "quizfragen", "szenarien_ap1", "szenarien_ap2", "projektarbeiten",
                           "themen", "rahmenplan", "fachgespraech", "umbenennungen")}
    umbenennungen = bisher["umbenennungen"]

    def ordner(pfad, namen):
        return {n: lesen(os.path.join(pfad, n + ".json")) for n in namen
                if os.path.exists(os.path.join(pfad, n + ".json"))}

    namen = ["karteikarten", "quizfragen", "szenarien_ap2", "projektarbeiten", "fachgespraech", "themen",
             "rahmenplan_zusatz", "einteilung_bisher"]
    echt = ordner(os.path.join(repo, "inhalte_csharp", "lernen"), namen)
    probe = ordner(os.path.join(repo, "tests", "Lerninhalte", "probe"), namen)
    for n in ("rahmenplan_zusatz", "einteilung_bisher"):
        probe[n] = echt[n]

    faelle = {}
    for fall in lesen(os.path.join(repo, "tests", "Lerninhalte", "faelle.json"))["faelle"]:
        kopie = copy.deepcopy(probe)
        for datei, pfad, art, wert in fall["aenderungen"]:
            setzen(kopie[datei], pfad, art, wert)
        faelle[fall["name"]] = pruefen(bisher, quiz_alle, kopie, umbenennungen)

    einteilung = {art: {w: sum(1 for x in teil.values() if x == w) for w in ("si", "gemeinsam")}
                  for art, teil in echt["einteilung_bisher"].items() if not art.startswith("_")}
    ergebnis = {
        "python": sys.version.split()[0],
        "echt": pruefen(bisher, quiz_alle, echt, umbenennungen),
        "probe": pruefen(bisher, quiz_alle, probe, umbenennungen),
        "faelle": faelle,
        "sichten": {"echt": sichten(bisher, quiz_alle, echt), "probe": sichten(bisher, quiz_alle, probe)},
        "einteilung": einteilung,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as datei:
        json.dump(ergebnis, datei, ensure_ascii=False, indent=1)
        datei.write("\n")
    for name, meldungen in faelle.items():
        print(name, len(meldungen))
    return 0


if __name__ == "__main__":
    sys.exit(main())
