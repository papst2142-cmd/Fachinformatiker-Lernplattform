#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G9 (Phase 1, K6/K8 Teil "Beschriftungen"):
alle Beschriftungen fuer Navigation, Kopfleiste, Optionen und die Schrift-
und Darstellungs-Auswahl, gelesen aus dem echten Python-Code der Python-Linie:

  1. PC-Navigation (app_gui.NAV_ITEMS, NAV_SYMBOLS, CATEGORY_NAV_SYMBOL,
     CATEGORY_NAV_ICON, CATEGORY_SHORT), Seitenleiste, Kopfleiste
     (VIEW_TITLES, Suchfeld).
  2. Handy-Navigation (mobile/src/main.NAV), Sammelseite "Lernen"
     (LearnScreen.ENTRIES mit Muster und fertigem Text), Brotkrumen der
     Seiten, Kopfleisten-Knoepfe Hilfe und Suche.
  3. Optionen (fisi_optionen.AREAS, SECTIONS, DIAGNOSE_SECTIONS, Untertitel
     wie PC und Handy sie beim Aufbau setzen), Texte der Bereiche
     Optische Anpassungen (Schrift, Darstellung, Farben/Vorlagen), Tagesziel,
     Diagnose (Problem melden), Ueber das Programm, Klapptexte.

app_gui.py und mobile/src/main.py importieren customtkinter bzw. flet und
werden deshalb NICHT importiert, sondern mit dem Modul ast gelesen. Ausdruecke,
die keine reinen Literale sind (z. B. fh.HELP_TITLE, CATEGORIES,
ft.Icons.STYLE_ROUNDED, "%d Aufgaben" % len(SZENARIEN)), werden mit eval im
Namensraum der reinen Python-Module ausgewertet (fisi_core, fisi_theme,
fisi_optionen, ...), ft.Icons.X ergibt dabei den Namen "X". Reine Module
werden normal importiert.

Es werden keine Lerndaten benutzt; alle Daten- und Einstellungsordner zeigen
in einen leeren Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_beschriftungen.py <pfad_fisi_lernplattform> tests/Vergleich/beschriftungen.json
"""
import ast
import importlib
import json
import os
import sys
import tempfile

# Reine Module (ohne Oberflaeche), die app_gui.py/main.py importieren und hier
# fuer die Auswertung der Ausdruecke importiert werden duerfen
REINE_MODULE = {"fisi_core", "fisi_lernen", "fisi_theme", "fisi_optionen", "fisi_hilfe",
                "fisi_sicherung", "fisi_diagnose", "fisi_leistung", "fisi_rahmenplan",
                "fisi_rechtliches", "fisi_update"}


class _Symbole:
    """Ersatz fuer ft.Icons: ft.Icons.STYLE_ROUNDED -> "STYLE_ROUNDED"."""

    def __getattr__(self, name):
        return name


class _Ft:
    Icons = _Symbole()


def lies_modul(pfad):
    with open(pfad, encoding="utf-8") as handle:
        return ast.parse(handle.read(), pfad)


def namensraum(baum):
    """Namen aus den Import-Zeilen des Moduls, beschraenkt auf REINE_MODULE."""
    ns = {"ft": _Ft()}
    for knoten in baum.body:
        if isinstance(knoten, ast.Import):
            for alias in knoten.names:
                if alias.name in REINE_MODULE:
                    ns[alias.asname or alias.name] = importlib.import_module(alias.name)
        elif isinstance(knoten, ast.ImportFrom) and knoten.module in REINE_MODULE:
            modul = importlib.import_module(knoten.module)
            for alias in knoten.names:
                ns[alias.asname or alias.name] = getattr(modul, alias.name)
    return ns


def werte(knoten, ns):
    return eval(compile(ast.Expression(knoten), "<ast>", "eval"), ns)


def zuweisung(koerper, name):
    """Wert-Knoten der Zuweisung name = ... in einer Liste von Anweisungen (genau eine)."""
    treffer = [k.value for k in koerper if isinstance(k, ast.Assign)
               and any(isinstance(z, ast.Name) and z.id == name for z in k.targets)]
    if len(treffer) != 1:
        raise SystemExit("Zuweisung %s: %d Treffer" % (name, len(treffer)))
    return treffer[0]


def modulwert(baum, name, ns):
    wert = werte(zuweisung(baum.body, name), ns)
    ns[name] = wert
    return wert


def klasse(baum, name):
    treffer = [k for k in baum.body if isinstance(k, ast.ClassDef) and k.name == name]
    if len(treffer) != 1:
        raise SystemExit("Klasse %s: %d Treffer" % (name, len(treffer)))
    return treffer[0]


def methode(kl, name):
    treffer = [k for k in kl.body if isinstance(k, ast.FunctionDef) and k.name == name]
    if len(treffer) != 1:
        raise SystemExit("Methode %s.%s: %d Treffer" % (kl.name, name, len(treffer)))
    return treffer[0]


def punktname(knoten):
    if isinstance(knoten, ast.Name):
        return knoten.id
    if isinstance(knoten, ast.Attribute):
        vorne = punktname(knoten.value)
        return vorne + "." + knoten.attr if vorne else knoten.attr
    return ""


def aufrufe(knoten, name):
    """Alle Aufrufe ...name(...) unter knoten in Quelltext-Reihenfolge."""
    liste = [k for k in ast.walk(knoten) if isinstance(k, ast.Call)
             and (punktname(k.func) == name or punktname(k.func).endswith("." + name))]
    return sorted(liste, key=lambda k: (k.lineno, k.col_offset))


def text_oder_muster(knoten, ns):
    """Text eines Ausdrucks; bei "muster" % (...) das Muster selbst (die
    eingesetzten Werte, z. B. die Version, setzt C# ein)."""
    if isinstance(knoten, ast.BinOp) and isinstance(knoten.op, ast.Mod) \
            and isinstance(knoten.left, ast.Constant) and isinstance(knoten.left.value, str):
        return knoten.left.value
    wert = werte(knoten, ns)
    if not isinstance(wert, str):
        raise SystemExit("kein Text: %s" % ast.unparse(knoten))
    return wert


# Bedienelemente mit sichtbarem Text: Name -> (Stelle des Arguments, Schluesselwort)
PC_TEXTE = {"make_label": (1, None), "NeoButton": (1, None), "CTkSwitch": (None, "text")}
HANDY_TEXTE = {"ui.text": (0, None), "ui.label": (0, None), "_switch": (0, None),
               "ui.GradientButton": (0, None)}


def sichtbare_texte(knoten, tabelle, ns):
    """Texte der Bedienelemente unter knoten (Quelltext-Reihenfolge). Leere
    Texte und Ausdruecke, die erst zur Laufzeit feststehen (z. B. eine
    Schleifenvariable), werden uebergangen."""
    liste = []
    for name, (stelle, wort) in tabelle.items():
        for aufruf in aufrufe(knoten, name):
            if name == "ui.text" and punktname(aufruf.func) != "ui.text":
                continue
            arg = None
            if stelle is not None and len(aufruf.args) > stelle:
                arg = aufruf.args[stelle]
            for kw in aufruf.keywords:
                if kw.arg == wort:
                    arg = kw.value
            if arg is None:
                continue
            try:
                text = text_oder_muster(arg, ns)
            except (NameError, AttributeError, TypeError, KeyError, SystemExit):
                continue
            if text:
                liste.append(((aufruf.lineno, aufruf.col_offset), text))
    return [text for _stelle, text in sorted(liste)]


def bereich_aufrufe(fn, ns, soll):
    """self._area(id, ..., subtitle=...) im Aufbau der Optionen: {id: Untertitel}.
    soll: die Kennungen, die genau einmal vorkommen muessen."""
    ergebnis = {}
    for aufruf in aufrufe(fn, "self._area"):
        bereich = werte(aufruf.args[0], ns)
        untertitel = None
        for kw in aufruf.keywords:
            if kw.arg == "subtitle":
                untertitel = text_oder_muster(kw.value, ns)
        if bereich in ergebnis:
            raise SystemExit("Bereich doppelt: %s" % bereich)
        ergebnis[bereich] = untertitel
    if sorted(ergebnis) != sorted(soll):
        raise SystemExit("Bereiche passen nicht: %s" % sorted(ergebnis))
    return ergebnis


def zuweisung_in(fn, ziel):
    """Wert-Knoten von self.x = ... bzw. x = ... in einer Methode (genau einer)."""
    treffer = [k.value for k in ast.walk(fn) if isinstance(k, ast.Assign)
               and any(ast.unparse(z) == ziel for z in k.targets)]
    if len(treffer) != 1:
        raise SystemExit("Zuweisung %s: %d Treffer" % (ziel, len(treffer)))
    return treffer[0]


def ein_aufruf(knoten, name):
    liste = aufrufe(knoten, name)
    if len(liste) != 1:
        raise SystemExit("Aufruf %s: %d Treffer" % (name, len(liste)))
    return liste[0]


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "leer.db")
    sys.path.insert(0, repo)
    import fisi_core as fc
    import fisi_diagnose as fdg
    import fisi_lernen as fl
    import fisi_optionen as fo
    import fisi_rechtliches as fr
    import fisi_theme as ft_

    # ---------------------------------------------------------------- PC
    pc = lies_modul(os.path.join(repo, "app_gui.py"))
    ns_pc = namensraum(pc)
    nav_items = modulwert(pc, "NAV_ITEMS", ns_pc)
    nav_symbole = modulwert(pc, "NAV_SYMBOLS", ns_pc)
    fach_symbol = modulwert(pc, "CATEGORY_NAV_SYMBOL", ns_pc)
    fach_zeichen = modulwert(pc, "CATEGORY_NAV_ICON", ns_pc)
    view_titel = modulwert(pc, "VIEW_TITLES", ns_pc)
    navigation_pc = []
    for key, zeichen, text, unter in nav_items:
        punkte = []
        for item in unter or []:
            if isinstance(item, tuple):
                sub_key, sub_zeichen, sub_text = item
                punkte.append({"id": sub_key, "zeichen": sub_zeichen,
                               "symbol": nav_symbole[sub_key], "titel": sub_text})
            else:
                # Fachbereich: Kennung = voller Name, Beschriftung = Kurzname
                punkte.append({"id": item, "zeichen": fach_zeichen.get(item, "dot"),
                               "symbol": fach_symbol[item],
                               "titel": fc.CATEGORY_SHORT.get(item, item)})
        navigation_pc.append({"id": key, "zeichen": zeichen, "symbol": nav_symbole[key],
                              "titel": text, "unterpunkte": punkte})

    seitenleiste = methode(klasse(pc, "Sidebar"), "__init__")
    seite_tabelle = {"make_label": (1, None), "ctk.CTkLabel": (None, "text")}
    seite_texte = (sichtbare_texte(seitenleiste, seite_tabelle, ns_pc)
                   + sichtbare_texte(methode(klasse(pc, "Sidebar"), "_build_status"),
                                     seite_tabelle, ns_pc))
    kopf = methode(klasse(pc, "Header"), "__init__")
    such_platzhalter = [kw.value.value for aufruf in aufrufe(kopf, "ctk.CTkEntry")
                        for kw in aufruf.keywords if kw.arg == "placeholder_text"]
    if len(such_platzhalter) != 1:
        raise SystemExit("Suchfeld PC")

    pc_optionen = klasse(pc, "SettingsView")
    pc_aufbau = methode(pc_optionen, "build")
    pc_untertitel = bereich_aufrufe(pc_aufbau, ns_pc, [a["id"] for a in fo.areas(pc=True)])
    ns_pc["APP_VERSION"] = modulwert(pc, "APP_VERSION", ns_pc)
    # Bereich "Updates": alle Anweisungen vor dem Bereich "Rundgang und Hilfe"
    vor_rundgang = []
    for anweisung in pc_aufbau.body:
        if any(werte(a.args[0], ns_pc) == "rundgang" for a in aufrufe(anweisung, "self._area")):
            break
        vor_rundgang.append(anweisung)
    pc_updates = sichtbare_texte(ast.Module(body=vor_rundgang, type_ignores=[]),
                                 {"NeoButton": (1, None), "CTkSwitch": (None, "text")}, ns_pc)
    pc_schrift = methode(pc_optionen, "_build_schrift")
    pc_farben = [methode(pc_optionen, n) for n in
                 ("_build_farben_mode", "_build_farben_presets", "_build_farben_backgrounds")]
    pc_ziel = methode(pc_optionen, "_build_tagesziel")
    ziel_schleife = [k for k in ast.walk(pc_ziel) if isinstance(k, ast.For)]
    if len(ziel_schleife) != 1:
        raise SystemExit("Tagesziel-Schleife PC")
    pc_ziel_schalter = ast.literal_eval(ziel_schleife[0].iter)
    pc_ueber = methode(pc_optionen, "_build_ueber")
    pc_ueber_plattform = werte(ein_aufruf(pc_ueber, "fr.about_text").args[2], ns_pc)
    pc_diagnose = methode(pc_optionen, "_build_diagnose")

    # ------------------------------------------------------------- Handy
    handy = lies_modul(os.path.join(repo, "mobile", "src", "main.py"))
    ns_h = namensraum(handy)
    nav = modulwert(handy, "NAV", ns_h)
    such_text = modulwert(handy, "SEARCH_TEXT", ns_h)
    lernen = klasse(handy, "LearnScreen")
    lernen_kopf = werte(zuweisung(lernen.body, "crumbs"), ns_h)
    eintraege_knoten = zuweisung(lernen.body, "ENTRIES")
    eintraege = []
    for element in eintraege_knoten.elts:
        key, symbol, titel, detail, farben = element.elts
        eintraege.append({
            "id": werte(key, ns_h), "symbol": werte(symbol, ns_h), "titel": werte(titel, ns_h),
            "muster": text_oder_muster(detail, ns_h), "text": werte(detail, ns_h),
            "farben": [werte(farben, ns_h)] if isinstance(werte(farben, ns_h), str)
            else list(werte(farben, ns_h)),
        })
    # Brotkrumen der Seiten (crumbs = (...)) je Kennung aus SCREEN_CLASSES
    klassen = zuweisung(handy.body, "SCREEN_CLASSES")
    handy_kopf = []
    for schluessel, wert in zip(klassen.keys, klassen.values):
        name = punktname(wert)
        kl = [k for k in handy.body if isinstance(k, ast.ClassDef) and k.name == name]
        if not kl:
            continue   # z. B. spiel.GameScreen (eigenes Modul, Spiel kommt spaeter)
        krumen = [k.value for k in kl[0].body if isinstance(k, ast.Assign)
                  and any(isinstance(z, ast.Name) and z.id == "crumbs" for z in k.targets)]
        if krumen:
            handy_kopf.append([werte(schluessel, ns_h)] + list(werte(krumen[0], ns_h)))
    app = [k for k in handy.body if isinstance(k, ast.ClassDef)
           and any(isinstance(f, ast.FunctionDef) and f.name == "_appbar" for f in k.body)]
    if len(app) != 1:
        raise SystemExit("Kopfleiste Handy")
    appbar = methode(app[0], "_appbar")
    kopf_knoepfe = []
    for aufruf in aufrufe(appbar, "ft.IconButton"):
        tip = [kw.value for kw in aufruf.keywords if kw.arg == "tooltip"][0]
        kopf_knoepfe.append({"symbol": werte(aufruf.args[0], ns_h), "titel": werte(tip, ns_h)})

    h_optionen = klasse(handy, "SettingsScreen")
    h_aufbau = methode(h_optionen, "build")
    ns_h["APP_VERSION"] = "%s"
    h_untertitel = bereich_aufrufe(h_aufbau, ns_h, [a["id"] for a in fo.areas(pc=False)])
    h_optik = zuweisung_in(h_aufbau, "optik")
    h_vorlagen = zuweisung_in(h_aufbau, "templates")
    h_ziel = zuweisung_in(h_aufbau, "goal")
    h_ziel_schalter = zuweisung_in(h_aufbau, "self.goal_rows")
    h_erinnerung = zuweisung_in(h_aufbau, "self.entry_reminder")
    h_ueber = zuweisung_in(h_aufbau, "about")
    h_ueber_plattform = werte(ein_aufruf(h_ueber, "fr.about_text").args[2], ns_h)
    h_diagnose = zuweisung_in(h_aufbau, "report_controls")
    h_updates = zuweisung_in(h_aufbau, "updates")

    # ---------------------------------------------------------- Optionen
    def bereiche(pc_seite):
        liste = []
        quelle = pc_untertitel if pc_seite else h_untertitel
        for area in fo.areas(pc=pc_seite):
            liste.append({
                "id": area["id"], "titel": area["titel"],
                "untertitel": quelle[area["id"]],
                "nur_pc": bool(area.get("nur_pc")),
                "offen": fo.opened_at_start(area["id"]),
                "aufgeklappt": fo.unfolded_at_start(area["id"]),
                "abschnitte": [{"id": s["id"], "titel": s["titel"], "nur_pc": bool(s.get("nur_pc"))}
                               for s in fo.sections(area["id"], pc=pc_seite)],
                "stichwoerter": area["stichwoerter"]
                + (area.get("stichwoerter_pc", []) if pc_seite else []),
            })
        return liste

    alle_titel = sorted({a["titel"] for a in fo.AREAS}
                        | {s["titel"] for a in fo.AREAS for s in fo.sections(a["id"])}
                        | {fo.TEMPLATES_TITLE, ft_.FONT_TITLE}
                        | {e["titel"] for e in navigation_pc})

    daten = {
        "quelle": "app_gui.py und mobile/src/main.py (per ast), fisi_optionen, fisi_theme, "
                  "fisi_lernen, fisi_core, fisi_hilfe, fisi_diagnose, fisi_rechtliches "
                  "(Python v0.63.2)",
        "python": sys.version.split()[0],
        "navigation_pc": navigation_pc,
        "seitenleiste_pc": {"logo": fc.APP_LOGO_TEXT, "texte": seite_texte},
        "kopfleiste_pc": {"titel": [[k] + list(v) for k, v in view_titel.items()],
                          "suche": such_platzhalter[0]},
        "navigation_handy": [{"id": k, "symbol": aus, "symbol_aktiv": an, "titel": t}
                             for k, aus, an, t in nav],
        "kopfleiste_handy": {"titel": handy_kopf, "knoepfe": kopf_knoepfe, "suche": such_text},
        "lernen": {"kopf": list(lernen_kopf), "eintraege": eintraege, "note_kurz": fr.NOTE_KURZ,
                   "anzahlen": {"karteikarten": len(fc.KARTEIKARTEN),
                                "fachbereiche": len(fc.CATEGORIES),
                                "quizfragen": len(fc.QUIZ_QUESTIONS),
                                "ap1": len(fc.AP1_SZENARIEN), "ap2": len(fc.SZENARIEN),
                                "testprojekte": len(fc.PROJEKTARBEITEN)}},
        "optionen": {
            "bereiche_pc": bereiche(True),
            "bereiche_handy": bereiche(False),
            "speichern_fehlgeschlagen_titel": fo.SAVE_FAILED_TITLE,
            "speichern_fehlgeschlagen": fo.SAVE_FAILED,
            "suche_art": fo.SEARCH_KIND,
            "updates_pc": pc_updates,
            "updates_handy": sichtbare_texte(zuweisung_in(h_aufbau, "self.btn_update"),
                                             HANDY_TEXTE, ns_h)
            + sichtbare_texte(h_updates, HANDY_TEXTE, ns_h),
        },
        "klappen": {"auf": ft_.FOLD_OPEN_TEXT, "zu": ft_.FOLD_CLOSE_TEXT,
                    "zustand_zu": ft_.FOLD_STATE_CLOSED, "zustand_auf": ft_.FOLD_STATE_OPEN,
                    "lernen_auf": fl.FOLD_OPEN, "lernen_zu": fl.FOLD_CLOSE,
                    "beispiele": [[t, o, ft_.fold_label(t, o)]
                                  for t in (fo.COLORS_TITLE, fo.TEMPLATES_TITLE)
                                  for o in (False, True)]},
        "darstellung": {"modi": [list(m) for m in ft_.MODES],
                        "pc": sichtbare_texte(pc_farben[0], {"make_label": (1, None)}, ns_pc),
                        "handy": sichtbare_texte(h_optik, {"ui.label": (0, None)}, ns_h)},
        "farbwelten": {
            "grundfarben": [[p["id"], p["name"]] for p in ft_.PRESETS],
            "hintergruende": [[b["id"], b["name"], ft_.LIGHT_NAMES[b["id"]]]
                              for b in ft_.BACKGROUNDS],
            "vorlagen_titel": fo.TEMPLATES_TITLE, "vorlagen_hinweis": fo.TEMPLATES_HINT,
            "vorlagen_text": fo.TEMPLATES_TEXT,
            "farben_titel": fo.COLORS_TITLE, "optik_titel": fo.OPTIK_TITLE,
            "pc": sichtbare_texte(pc_farben[1], {"make_label": (1, None)}, ns_pc)
            + sichtbare_texte(pc_farben[2], {"make_label": (1, None)}, ns_pc),
            "handy": sichtbare_texte(h_vorlagen, HANDY_TEXTE, ns_h),
            "beschaeftigt_titel": ft_.BUSY_TITLE, "beschaeftigt_text": ft_.BUSY_TEXT,
            "laden_text": ft_.LOADING_TEXT,
        },
        "schrift": {
            "stufen": [[s["id"], s["name"], s["faktor"]] for s in ft_.FONT_SIZES],
            "titel": ft_.FONT_TITLE, "untertitel": ft_.FONT_SUBTITLE, "hinweis": ft_.FONT_HINT,
            "beschaeftigt_titel": ft_.BUSY_FONT_TITLE, "beschaeftigt_text": ft_.BUSY_FONT_TEXT,
            "pc": sichtbare_texte(pc_schrift, {"make_label": (1, None)}, ns_pc),
        },
        "tagesziel": {
            "vorgabe": fl.GOAL_DEFAULT, "min": fl.GOAL_MIN, "max": fl.GOAL_MAX,
            "schritt": fl.GOAL_STEP, "erinnerung_vorgabe": fl.REMINDER_DEFAULT,
            "schalter_pc": [list(p) for p in pc_ziel_schalter],
            "pc": sichtbare_texte(pc_ziel, {"make_label": (1, None)}, ns_pc),
            "schalter_handy": sichtbare_texte(h_ziel_schalter, {"_switch": (0, None)}, ns_h),
            "handy": sichtbare_texte(h_ziel, {"ui.text": (0, None)}, ns_h),
            "erinnerung_hinweis_handy": [kw.value.value for kw in h_erinnerung.keywords
                                         if kw.arg == "hint"][0],
        },
        "diagnose": {
            "titel": fdg.TITLE, "untertitel": fdg.SUBTITLE, "hilfe": fdg.HELP,
            "kopieren": fdg.BTN_COPY, "speichern": fdg.BTN_SAVE, "ordner": fdg.BTN_FOLDER,
            "kopiert": fdg.MSG_COPIED, "ordner_fehler": fdg.MSG_FOLDER_ERROR,
            "kein_protokoll": fdg.NO_LOG, "gekuerzt": fdg.LOG_CUT, "dateityp": fdg.FILE_TYPE,
            "bereich_titel": fo.DIAGNOSE_TITLE, "bereich_untertitel": fo.DIAGNOSE_SUBTITLE,
            # nur die Knoepfe von "Problem melden" (Texte aus fisi_diagnose)
            "pc": [werte(a.args[1], ns_pc) for a in aufrufe(pc_diagnose, "NeoButton")
                   if ast.unparse(a.args[1]).startswith("fdg.")],
            "handy": sichtbare_texte(h_diagnose, {"ui.GradientButton": (0, None)}, ns_h),
        },
        "ueber": {
            "titel": fo.AREA_BY_ID[fo.ABOUT_ID]["titel"],
            "programmname": fc.APP_DISPLAY_NAME,
            "plattform_pc": pc_ueber_plattform, "plattform_handy": h_ueber_plattform,
            "beispielversion": "A 0.1.0",
            "text_pc": fr.about_text(fc.APP_DISPLAY_NAME, "A 0.1.0", pc_ueber_plattform),
            "text_handy": fr.about_text(fc.APP_DISPLAY_NAME, "A 0.1.0", h_ueber_plattform),
            "copyright": fr.COPYRIGHT, "hinweis": fr.HINWEIS, "claude": fr.CLAUDE_HINWEIS,
            "knoepfe": [fr.BTN_LIZENZ, fr.BTN_FREMD],
            "fenster": [fr.TITEL_LIZENZ, fr.TITEL_FREMD],
        },
        # Ueberschriften stehen in Grossbuchstaben (PC .upper(), Handy ui.label):
        # Python macht aus "ß" "SS", C# ToUpperInvariant nicht
        "gross": [[t, t.upper()] for t in alle_titel],
        "zahlwoerter": [[n, fc.count_word(n)] for n in range(-1, 16)],
        "tausender": [[n, fc.de_int(n)] for n in (0, 7, 999, 1000, 1234, 99999, 123456, 1234567)],
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    print("G9: %d PC-Menuepunkte, %d Handy-Reiter, %d Lernen-Eintraege, %d Optionen-Bereiche -> %s"
          % (len(navigation_pc), len(nav), len(eintraege), len(daten["optionen"]["bereiche_pc"]),
             ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
