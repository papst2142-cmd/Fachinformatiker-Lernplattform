#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G22 (Phase 2, Welle 2, Pakete L4/L5/L6): Texte und
Textregeln der Karteikarten, des Pruefungstrainers (Uebung) und des Dialogs
"Lernstand uebernehmen", gelesen bzw. gerechnet aus dem echten Python-Code der
Python-Linie (v0.63.2, eingefroren):

  texte          - Beschriftungen aus app_gui.py (PC-Wortlaut). app_gui.py
                   importiert customtkinter und wird deshalb nicht importiert,
                   sondern mit ast gelesen: je Text Datei, Zeile und Nummer des
                   Textes in dieser Zeile (der Stand v0.63.2 aendert sich nie).
  konstanten     - Texte aus reinen Modulen (fisi_core, fisi_rechtliches,
                   fisi_sicherung, fisi_sync), normal importiert.
  listen         - Q_STATUS_TABS, CardsView.MODES, QUIZ_MODES.
  status_zeilen  - fisi_core.status_label fuer feste Antwortfolgen.
  zaehltexte     - app_gui.count_text (Funktion aus dem ast ausgefuehrt).
  pool           - "%s verfuegbar  .  %s" mit fisi_core.plural und count_text.
  ergebnisse     - Ergebnisblock von finish_quiz (Muster aus app_gui, Note
                   fisi_core.ihk_note, Prozent fisi_core.de_number).
  eintraege      - plural(n, "Eintrag", "Eintraege"), fisi_sync.removed_text,
                   Saetze von merge_message ohne Loeschungen.
  klein          - str.lower() fuer Sonderfaelle (Freitext-Pruefung).
  mehrfachwahl   - E1: random.Random(int(md5(frage)) & 0xFFFF).shuffle(optionen)
                   fuer alle Karteikarten (Regel des Plans, gerechnet mit Pythons
                   Zufall).
  uebung         - Pruefungstrainer (Uebung) mit festem Startwert des Moduls
                   random: start_quiz mit Statusfilter (random.sample aus dem
                   Pool), danach je Frage random.shuffle der Antworten
                   (_load_question), und practice (random.shuffle, hoechstens 50).
                   Nachgestellt aus app_gui 3185-3226/3266 mit QUIZ_QUESTIONS.
  sicherungen    - fisi_sicherung.backup_summary fuer die synthetischen
                   Sicherungen a, b, c aus tests/Vergleich/uebernahme.json (G20).

Es werden keine Lerndaten benutzt; alle Daten- und Einstellungsordner zeigen
in einen leeren Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_lerntexte.py <pfad_fisi_lernplattform> tests/Vergleich/uebernahme.json tests/Vergleich/lerntexte.json
"""
import ast
import hashlib
import json
import os
import random
import sys
import tempfile

# Schluessel -> (Datei, Zeile, Nummer des Textes in der Zeile)
TEXTE = {
    "KartenTexte.Fachbereich": ("app_gui.py", 2683, 0),
    "KartenTexte.Status": ("app_gui.py", 2697, 0),
    "KartenTexte.Thema": ("app_gui.py", 2703, 0),
    "KartenTexte.Lernmodus": ("app_gui.py", 2712, 0),
    "KartenTexte.FrageTitel": ("app_gui.py", 2718, 0),
    "KartenTexte.Untertitel": ("app_gui.py", 2919, 0),
    "KartenTexte.AntwortTitel": ("app_gui.py", 2731, 0),
    "KartenTexte.FreitextHinweis": ("app_gui.py", 2743, 0),
    "KartenTexte.AufdeckenHinweis": ("app_gui.py", 2755, 0),
    "KartenTexte.Wusstest": ("app_gui.py", 491, 0),
    "KartenTexte.Gewusst": ("app_gui.py", 493, 0),
    "KartenTexte.NichtGewusst": ("app_gui.py", 495, 0),
    "KartenTexte.Zurueck": ("app_gui.py", 2773, 0),
    "KartenTexte.Pruefen": ("app_gui.py", 2776, 0),
    "KartenTexte.LoesungAufdecken": ("app_gui.py", 2935, 0),
    "KartenTexte.Naechste": ("app_gui.py", 2779, 0),
    "KartenTexte.Zaehler": ("app_gui.py", 2916, 0),
    "KartenTexte.ZaehlerLeer": ("app_gui.py", 2908, 0),
    "KartenTexte.Leer": ("app_gui.py", 2905, 0),
    "KartenTexte.HinweisTitel": ("app_gui.py", 2946, 0),
    "KartenTexte.EingabeFehlt": ("app_gui.py", 2946, 1),
    "KartenTexte.AuswahlFehlt": ("app_gui.py", 2962, 1),
    "KartenTexte.FreitextRichtig": ("app_gui.py", 2950, 0),
    "KartenTexte.FreitextVergleich": ("app_gui.py", 2954, 0),
    "KartenTexte.MusterloesungVor": ("app_gui.py", 2957, 0),
    "KartenTexte.McRichtig": ("app_gui.py", 2967, 0),
    "KartenTexte.McFalschVor": ("app_gui.py", 2970, 0),
    "KartenTexte.Musterloesung": ("app_gui.py", 2975, 0),
    "KartenTexte.WiederholungTitel": ("app_gui.py", 11024, 0),
    "KartenTexte.NichtsFaellig": ("app_gui.py", 11024, 1),
    "KartenTexte.Alle": ("app_gui.py", 2684, 0),
    "TrainerTexte.SessionTitel": ("app_gui.py", 3067, 0),
    "TrainerTexte.ImKatalog": ("app_gui.py", 3068, 0),
    "TrainerTexte.Fragenanzahl": ("app_gui.py", 3095, 0),
    "TrainerTexte.Starten": ("app_gui.py", 3100, 0),
    "TrainerTexte.Verfuegbar": ("app_gui.py", 3179, 0),
    "TrainerTexte.FrageEinzahl": ("app_gui.py", 3180, 0),
    "TrainerTexte.FrageMehrzahl": ("app_gui.py", 3180, 1),
    "TrainerTexte.FortschrittLeer": ("app_gui.py", 3111, 0),
    "TrainerTexte.Fortschritt": ("app_gui.py", 3262, 0),
    "TrainerTexte.UhrLeer": ("app_gui.py", 3114, 0),
    "TrainerTexte.Richtig": ("app_gui.py", 3264, 0),
    "TrainerTexte.AufgabeTitel": ("app_gui.py", 3121, 0),
    "TrainerTexte.StartHinweis": ("app_gui.py", 3126, 0),
    "TrainerTexte.FrageNummer": ("app_gui.py", 3257, 0),
    "TrainerTexte.Einreichen": ("app_gui.py", 3140, 0),
    "TrainerTexte.NaechsteFrage": ("app_gui.py", 3294, 1),
    "TrainerTexte.Auswertung": ("app_gui.py", 3294, 0),
    "TrainerTexte.Abbrechen": ("app_gui.py", 3144, 0),
    "TrainerTexte.KeineFragen": ("app_gui.py", 3210, 1),
    "TrainerTexte.ErklaerungRichtig": ("app_gui.py", 3286, 0),
    "TrainerTexte.ErklaerungFalsch": ("app_gui.py", 3286, 1),
    "TrainerTexte.AbbrechenFrage": ("app_gui.py", 3307, 0),
    "TrainerTexte.Abgebrochen": ("app_gui.py", 3314, 0),
    "TrainerTexte.Gespeichert": ("app_gui.py", 3331, 0),
    "TrainerTexte.NichtGespeichert": ("app_gui.py", 3332, 0),
    "UebernahmeTexte.FilterAlle": ("app_gui.py", 8073, 1),
    "Python.merge_uebernommen": ("fisi_sicherung.py", 560, 0),
    "Python.merge_nichts": ("fisi_sicherung.py", 562, 0),
    "Python.merge_alles_da": ("fisi_sicherung.py", 564, 0),
    "Python.merge_eintrag": ("fisi_sicherung.py", 560, 1),
    "Python.merge_eintraege": ("fisi_sicherung.py", 560, 2),
}

# Muster des Ergebnisblocks (finish_quiz)
ERGEBNIS = ("app_gui.py", 3334, 0)

KONSTANTEN = {
    "TrainerTexte.NoteLang": ("fisi_rechtliches", "NOTE_LANG"),
    "TrainerTexte.HinweisKurz": ("fisi_rechtliches", "HINWEIS_KURZ"),
    "UebernahmeTexte.Zusammenfuehren": ("fisi_sicherung", "BTN_MERGE"),
    "UebernahmeTexte.Abbrechen": ("fisi_sicherung", "BTN_CANCEL"),
    "UebernahmeTexte.ZuNeu": ("fisi_sicherung", "MSG_NEWER"),
    "UebernahmeTexte.PruefsummeFalsch": ("fisi_sicherung", "MSG_DAMAGED"),
    "UebernahmeTexte.InhaltKaputt": ("fisi_sicherung", "MSG_BROKEN"),
    "UebernahmeTexte.UngueltigAbgleich": ("fisi_sync", "MSG_INVALID_MARKER"),
    "UebernahmeTexte.ZukunftSicherung": ("fisi_sync", "MSG_FUTURE_BACKUP"),
    "Python.FILE_TYPE": ("fisi_sicherung", "FILE_TYPE"),
    "Python.BACKUP_EXT": ("fisi_sicherung", "BACKUP_EXT"),
    "Python.PREVIEW_HINT": ("fisi_sicherung", "PREVIEW_HINT"),
}

# Antwortfolgen fuer status_label (None = angesehen)
FOLGEN = [[], [None], [None, None], [True], [False], [True, True], [True, True, True], [False, True],
          [True, False], [True, None], [None, True], [True, None, True], [False, None], [True, True, False],
          [False, True, True], [True, False, True, True], [None, False], [True, None, None, True, True]]

ZAEHLUNGEN = [(0, 0, 0), (1, 2, 3), (1234, 0, 56), (12345, 1001, 999999)]

KLEIN = ["ß", "ẞ", "STRASSE", "İstanbul", "İ", "I", "ΟΔΟΣ", "ΟΔΟΣ ΚΑΙ", "Σ", "ΑΣ.", "ΣΑ", "Ǆ", "ǅ",
         "ﬁ", "Ⅻ", "Ⓐ", "Ꭰ", "K", "Å", "ΐ", "ΪΣ", "TCP/IP", "Ä Ö Ü", "Dürfen"]


def literal(baeume, datei, zeile, nummer):
    """Nummer-ter Text (nach Spalte) mit Anfang in dieser Zeile."""
    knoten = sorted((k for k in ast.walk(baeume[datei])
                     if isinstance(k, ast.Constant) and isinstance(k.value, str) and k.lineno == zeile),
                    key=lambda k: k.col_offset)
    return knoten[nummer].value


def funktion(baum, name, ns):
    """Funktion aus dem ast in ns ausfuehren und zurueckgeben."""
    for knoten in baum.body:
        if isinstance(knoten, ast.FunctionDef) and knoten.name == name:
            exec(compile(ast.Module(body=[knoten], type_ignores=[]), "<ast>", "exec"), ns)
            return ns[name]
    raise KeyError(name)


def klassenwert(baum, klasse, name):
    for knoten in ast.walk(baum):
        if isinstance(knoten, ast.ClassDef) and knoten.name == klasse:
            for teil in knoten.body:
                if isinstance(teil, ast.Assign) and any(getattr(z, "id", None) == name for z in teil.targets):
                    return ast.literal_eval(teil.value)
    raise KeyError(klasse + "." + name)


def modulwert(baum, name):
    for teil in baum.body:
        if isinstance(teil, ast.Assign) and any(getattr(z, "id", None) == name for z in teil.targets):
            return ast.literal_eval(teil.value)
    raise KeyError(name)


def main():
    if len(sys.argv) != 4:
        print(__doc__)
        return 2
    repo, g20, ziel = sys.argv[1], sys.argv[2], sys.argv[3]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_rechtliches
    import fisi_sicherung
    import fisi_sync
    module = {"fisi_rechtliches": fisi_rechtliches, "fisi_sicherung": fisi_sicherung, "fisi_sync": fisi_sync}

    baeume = {}
    for datei in ("app_gui.py", "fisi_sicherung.py"):
        with open(os.path.join(repo, datei), encoding="utf-8") as handle:
            baeume[datei] = ast.parse(handle.read(), datei)
    gui = baeume["app_gui.py"]

    texte = {schluessel: literal(baeume, *ort) for schluessel, ort in TEXTE.items()}
    konstanten = {schluessel: getattr(module[m], name) for schluessel, (m, name) in KONSTANTEN.items()}
    listen = {
        "Q_STATUS_TABS": [list(p) for p in core.Q_STATUS_TABS],
        "CardsView.MODES": [list(p) for p in klassenwert(gui, "CardsView", "MODES")],
        "QUIZ_MODES": [list(p) for p in modulwert(gui, "QUIZ_MODES")],
        "Q_OPEN": core.Q_OPEN, "Q_PRACTICE": core.Q_PRACTICE, "Q_DONE": core.Q_DONE,
        "LEVEL_RED": core.LEVEL_RED, "LEVEL_YELLOW": core.LEVEL_YELLOW, "FILTER_ALL": core.FILTER_ALL,
        "DONE_STREAK": core.DONE_STREAK,
    }

    status_zeilen = []
    for folge in FOLGEN:
        antworten = [("2026-10-%02d 10:00:00" % (i + 1), wert) for i, wert in enumerate(folge)]
        buch = core.StatusBook(results={(core.SRC_CARD, "x"): antworten} if antworten else {})
        text, farbe = core.status_label(buch, core.SRC_CARD, "x")
        status_zeilen.append({"folge": folge, "text": text, "farbe": farbe})

    ns = {"fisi_core": core, "Q_OPEN": core.Q_OPEN, "Q_PRACTICE": core.Q_PRACTICE, "Q_DONE": core.Q_DONE}
    count_text = funktion(gui, "count_text", ns)
    zaehltexte = []
    pool = []
    muster_pool = texte["TrainerTexte.Verfuegbar"]
    for offen, ueben, fertig in ZAEHLUNGEN:
        zahlen = {core.Q_OPEN: offen, core.Q_PRACTICE: ueben, core.Q_DONE: fertig}
        zaehltexte.append({"zahlen": [offen, ueben, fertig], "text": count_text(zahlen)})
        for verfuegbar in (0, 1, 2, 1234):
            pool.append({"zahlen": [offen, ueben, fertig], "verfuegbar": verfuegbar,
                         "text": muster_pool % (core.plural(verfuegbar, "Frage", "Fragen"), count_text(zahlen))})

    muster = literal(baeume, *ERGEBNIS)
    ergebnisse = []
    for score, total, elapsed, saved in ((0, 5, 0, True), (5, 5, 59, True), (9, 10, 61, False), (23, 25, 3599, True),
                                         (11, 12, 6000, True), (46, 50, 754, False), (1, 3, 125, True)):
        percentage = (score / total) * 100 if total else 0.0
        note = core.ihk_note(percentage)
        hint = texte["TrainerTexte.Gespeichert"] if saved else texte["TrainerTexte.NichtGespeichert"]
        ergebnisse.append({"richtig": score, "gesamt": total, "sekunden": elapsed, "gespeichert": saved,
                           "text": muster % (score, total, core.de_number(percentage, 1), fisi_rechtliches.NOTE_LANG,
                                             note, elapsed // 60, elapsed % 60, hint)})

    eintraege = []
    for anzahl in (0, 1, 2, 3, 1000, 1234567):
        eintraege.append({
            "anzahl": anzahl,
            "plural": core.plural(anzahl, texte["Python.merge_eintrag"], texte["Python.merge_eintraege"]),
            "entfernt": fisi_sync.removed_text(anzahl) + ".",
            "uebernommen": texte["Python.merge_uebernommen"]
                           % core.plural(anzahl, texte["Python.merge_eintrag"], texte["Python.merge_eintraege"]),
        })

    klein = [{"text": t, "klein": t.lower()} for t in KLEIN]

    with open(os.path.join(repo, "inhalte", "karteikarten.json"), encoding="utf-8") as handle:
        karten = json.load(handle)
    mehrfachwahl = []
    for karte in karten:
        optionen = list(karte["options"])
        random.Random(int(hashlib.md5(karte["q"].encode("utf-8")).hexdigest(), 16) & 0xFFFF).shuffle(optionen)
        mehrfachwahl.append({"frage": karte["q"], "optionen": optionen})

    # Pruefungstrainer (Uebung): Ablauf von start_quiz (Statusfilter) und practice
    uebung = []
    for startwert, bereich, anzahl in ((1, core.CATEGORIES[0], 10), (2026, core.CATEGORIES[3], 15),
                                        (7, None, 50)):
        quiz_pool = [q for q in core.QUIZ_QUESTIONS if bereich is None or q["cat"] == bereich]
        random.seed(startwert)
        runde = random.sample(quiz_pool, min(anzahl, len(quiz_pool)))
        antworten = []
        for frage in runde:
            gemischt = list(frage["options"])
            random.shuffle(gemischt)
            antworten.append(gemischt)
        uebung.append({"art": "start", "startwert": startwert, "fachbereich": bereich, "anzahl": anzahl,
                       "runde": [q["q"] for q in runde], "antworten": antworten})
    for startwert, auswahl in ((3, list(range(0, 120, 2))), (11, [5, 2, 9])):
        fragen = [core.QUIZ_QUESTIONS[i]["q"] for i in auswahl] + ["gibt es nicht"]
        random.seed(startwert)
        nach = {q["q"]: q for q in core.QUIZ_QUESTIONS}
        runde = [nach[k] for k in fragen if k in nach]
        random.shuffle(runde)
        runde = runde[:50]
        antworten = []
        for frage in runde:
            gemischt = list(frage["options"])
            random.shuffle(gemischt)
            antworten.append(gemischt)
        uebung.append({"art": "ueben", "startwert": startwert, "fragen": fragen,
                       "runde": [q["q"] for q in runde], "antworten": antworten})

    with open(g20, encoding="utf-8") as handle:
        dateien = json.load(handle)["dateien"]
    sicherungen = []
    for name in ("a", "b", "c"):
        sicherung = json.loads(dateien[name])
        sicherungen.append({"name": name, "zeilen": fisi_sicherung.backup_summary(sicherung)["zeilen"]})

    daten = {
        "quelle": "app_gui.py (ast), fisi_core, fisi_rechtliches, fisi_sicherung, fisi_sync (Python v0.63.2)",
        "python": sys.version.split()[0],
        "texte": texte,
        "konstanten": konstanten,
        "listen": listen,
        "status_zeilen": status_zeilen,
        "zaehltexte": zaehltexte,
        "pool": pool,
        "ergebnisse": ergebnisse,
        "eintraege": eintraege,
        "klein": klein,
        "mehrfachwahl": mehrfachwahl,
        "uebung": uebung,
        "sicherungen": sicherungen,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G22: %d Texte, %d Konstanten, %d Statuszeilen, %d Karten -> %s"
          % (len(texte), len(konstanten), len(status_zeilen), len(mehrfachwahl), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
