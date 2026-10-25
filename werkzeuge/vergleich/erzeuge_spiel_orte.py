#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G49 (Phase 7, W5 "Innenraeume", Weisung P7-10),
gerechnet vom echten Python-Code der Python-Linie (v0.63.2, eingefroren).

Die Innenraeume der 3D-Darstellung nehmen Grundriss, Personen und Texte aus
denselben Funktionen wie die 2D-Ansichten in Python. Gespielt wird hier nichts:
die Ereignisse kommen aus den schon erzeugten Vergleichswerten G44 Aktionen
(spiel_aktionen.json: Moebel, Umzuege, Tickets ohne Firma) und G47 Firma
(spiel_firma.json: Firma, Personal, Filiale, Serverfarm). Fuer jeden
Stichpunkt n gilt GameState(events[:n]):

  orte        - je Ort (zuhause, buero, die offenen Kundenorte, filiale, wenn
                eroeffnet): site_content(...)["gebaeude"] (der Grundriss mit
                der sichtbaren Einrichtung, zu Hause home_building), die Personen
                (people_at_site: id, name, rolle, platz und die Felder, die
                office_message liest) und place_message auf einem Raster von
                Punkten (ohne Person) und bei jeder Person (mit ihr).
  wohnung     - path_check, path_warning und fuer jedes eigene Moebelstueck an
                festen Punkten in zwei Drehungen placement_preview mit
                preview_text.

Vor jedem Stichpunkt wird fisi_game._SITE_CACHE geleert (er haelt sonst die
Personen eines frueheren Spielstands mit gleichen Plaetzen fest).

Die ersten und letzten Stichpunkte jedes Laufs stehen voll da, die uebrigen
nur als SHA-256 (Werte wie G47: set -> {"menge"}, Tupel -> Liste).

Keine Lerndaten; alle Daten- und Einstellungsordner zeigen in einen leeren
Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_orte.py <pfad_fisi_lernplattform> <spiel_aktionen.json> <spiel_firma.json> tests/Vergleich/spiel_orte.json
"""
import hashlib
import json
import os
import sys
import tempfile

VOLL = 2          # so viele Stichpunkte am Anfang und am Ende voll, sonst nur SHA-256
PERSON_FELDER = ("id", "name", "rolle", "platz", "mitarbeiter", "projekt_text", "kundenticket", "umsatz",
                 "in_weiterbildung", "im_coaching", "lagerist", "macke")


def wert(v):
    if isinstance(v, (set, frozenset)):
        return {"menge": sorted((wert(x) for x in v),
                                key=lambda x: json.dumps(x, ensure_ascii=True, separators=(",", ":")))}
    if isinstance(v, (tuple, list)):
        return [wert(x) for x in v]
    if isinstance(v, dict):
        if all(isinstance(k, str) for k in v):
            return {k: wert(x) for k, x in v.items()}
        return {"paare": [[wert(k), wert(x)] for k, x in v.items()]}
    return v


def pruefsumme(v):
    text = json.dumps(v, ensure_ascii=False, separators=(",", ":"))
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def raster(gebaeude):
    """Punkte im Abstand 1,25 (versetzt), dazu die Eingangstuer und knapp davor."""
    eingang = gebaeude["flur"]["eingang"]
    mitte = (eingang["von"] + eingang["bis"]) / 2.0
    punkte = [(0.6, mitte), (1.6, mitte)]
    x = 0.4
    while x < gebaeude["breite"]:
        y = 0.45
        while y < gebaeude["hoehe"]:
            punkte.append((x, y))
            y += 1.25
        x += 1.25
    return punkte


def personen(liste):
    return [{k: p.get(k) for k in PERSON_FELDER if k in p} for p in liste]


def ort_werte(fg, state, site):
    content = fg.site_content(site, state)
    gebaeude = content["gebaeude"]
    leute = content["kollegen"]
    nachrichten = []
    for x, y in raster(gebaeude):
        titel, text, knoepfe = fg.place_message(site, (x, y), None, state, content)
        nachrichten.append([x, y, None, titel, text, [list(k) for k in knoepfe]])
    for person in leute:
        x, y = person["platz"]
        titel, text, knoepfe = fg.place_message(site, (x, y), person, state, content)
        nachrichten.append([x, y, person["id"], titel, text, [list(k) for k in knoepfe]])
    return {"ort": site, "gebaeude": gebaeude, "personen": personen(leute), "nachrichten": nachrichten}


def wohnung_werte(fg, state):
    blocked = fg.path_check(state)
    gebaeude = fg.apartment(state.home_id)["gebaeude"]
    punkte = [(area["x"] + area["w"] / 2.0, area["y"] + area["h"] / 2.0) for area in gebaeude["raeume"]]
    punkte += [(0.3, 0.3), (gebaeude["breite"] - 0.4, gebaeude["hoehe"] / 2.0), (2.0, gebaeude["flur"]["y"] + 0.5)]
    vorschau = []
    for piece in sorted(state.furniture):
        for cx, cy in punkte:
            for turn in (0, 1):
                p = fg.placement_preview(state, piece, cx, cy, turn)
                vorschau.append([piece, cx, cy, turn, p, list(fg.preview_text(p))])
    return {"wohnung": state.home_id, "gesperrt": blocked, "warnung": fg.path_warning(blocked), "vorschau": vorschau}


def stichpunkt(fg, events, n):
    # Der Zwischenspeicher von site_content haelt die Personen-Eintraege eines frueheren Spielstands,
    # solange Ort, Grundriss und Plaetze gleich sind (Umsatz oder Kundenticket waeren dann veraltet);
    # jeder Stichpunkt rechnet darum frisch, wie ein neu gestartetes Spiel
    fg._SITE_CACHE.clear()
    state = fg.GameState([tuple(e) for e in events[:n]])
    sites = [fg.SITE_HOME, fg.SITE_OFFICE] + [p["id"] for p in fg.open_places(state)]
    if state.branch:
        sites.append(fg.SITE_BRANCH)
    return {"orte": [ort_werte(fg, state, s) for s in sites], "wohnung": wohnung_werte(fg, state)}


def lauf(fg, quelle, name, events, nummern):
    staende = []
    for i, n in enumerate(nummern):
        werte = wert(stichpunkt(fg, events, n))
        eintrag = {"n": n, "sha256": pruefsumme(werte)}
        if i < VOLL or i >= len(nummern) - VOLL:
            eintrag["werte"] = werte
        staende.append(eintrag)
    return {"quelle": quelle, "name": name, "staende": staende}


def main():
    if len(sys.argv) != 5:
        print(__doc__)
        return 2
    repo, aktionen_pfad, firma_pfad, ziel = sys.argv[1:5]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_game as fg

    with open(aktionen_pfad, encoding="utf-8") as handle:
        aktionen = json.load(handle)
    with open(firma_pfad, encoding="utf-8") as handle:
        firma = json.load(handle)
    laeufe = []
    for eintrag in aktionen["laeufe"]:
        events = eintrag["ereignisse"]
        nummern = sorted(set(range(2, len(events), 40)) | {len(events)})
        laeufe.append(lauf(fg, "spiel_aktionen", eintrag["name"], events, nummern))
    for eintrag in firma["laeufe"]:
        events = eintrag["ereignisse"]
        nummern = [s["n"] for s in eintrag["staende"]]
        nummern = sorted(set(nummern[::4]) | {nummern[-1], len(events)})
        laeufe.append(lauf(fg, "spiel_firma", eintrag["name"], events, nummern))
    daten = {"laeufe": laeufe}
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    orte = sum(len(s["werte"]["orte"]) for l in laeufe for s in l["staende"] if "werte" in s)
    print("G49: %d Laeufe, %d Stichpunkte (%d Orte voll) -> %s" % (
        len(laeufe), sum(len(l["staende"]) for l in laeufe), orte, ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
