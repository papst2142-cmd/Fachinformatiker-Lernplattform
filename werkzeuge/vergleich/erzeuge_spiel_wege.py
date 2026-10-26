#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G48 Teil "Wege" (Phase 7, W2 "Spiel-Kern Teil 1"),
gerechnet vom echten Python-Code der Python-Linie (v0.63.2, eingefroren).

Fuer jeden Grundriss aus den Spielinhalten (Buero mit den Kollegen, jeder
Kundenort mit seinen Personen, jede Ausbaustufe der eigenen Firma und der
Filiale ohne Personen):

  raster      - fisi_game._base_grid und _walk_grid: Anzahl der begehbaren
                Zellen und die Zellen in Pythons Reihenfolge (Reihenfolge
                einer Python-Menge, die _nearest_free bei gleichem Abstand
                entscheidet).
  punkte      - fuer ein festes Punktgitter (auch ausserhalb und in Waenden):
                can_stand, _nearest_free, room_at (Kennung), person_at und
                person_near (Kennung).
  wege        - walk_path fuer feste Start-/Zielpaare: Startplatz der Figur
                zu jeder Raummitte, jeder Person (reach=REACH) und jedem
                Gitterpunkt (jeder 7.), Raummitte zu Raummitte, dazu reach
                0.5 und 3.0.
  hash        - hash() einiger Zellen (Python-Tupel), zur Pruefung des
                Nachbaus.

Es werden keine Lerndaten benutzt; alle Daten- und Einstellungsordner zeigen
in einen leeren Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_wege.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_wege.json
"""
import atexit
import json
import os
import shutil
import sys
import tempfile


def grundrisse(game, inhalt):
    """(Name, content) fuer jeden Grundriss; content wie site_content ohne Spielstand."""
    ergebnis = [("buero", {"gebaeude": inhalt["gebaeude"], "kollegen": inhalt["kollegen"]})]
    for ort in game.customer_places(inhalt):
        ergebnis.append(("kunde_" + ort["id"], {"gebaeude": ort["gebaeude"],
                                               "kollegen": ort.get("personen", [])}))
    for nummer, stufe in enumerate(inhalt["firma"]["gebaeude"]["stufen"]):
        ergebnis.append(("firma_%d" % nummer, {"gebaeude": stufe["gebaeude"], "kollegen": []}))
    for nummer, stufe in enumerate(inhalt["firma"]["filiale"]["stufen"]):
        ergebnis.append(("filiale_%d" % nummer, {"gebaeude": stufe["gebaeude"], "kollegen": []}))
    return ergebnis


def kennung(eintrag):
    return eintrag["id"] if eintrag else None


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
    import fisi_game as game

    inhalt = game.GAME
    daten = {"konstanten": {"WALK_STEP": game.WALK_STEP, "BODY_RADIUS": game.BODY_RADIUS,
                            "PERSON_RADIUS": game.PERSON_RADIUS, "REACH": game.REACH,
                            "WALK_FREE": list(game.WALK_FREE)},
             "hash": [[[a, b], hash((a, b))] for a in (0, 1, 2, 7, 63, 119, -1, -2, -5)
                      for b in (0, 1, 3, 55, -1, -3)],
             "grundrisse": []}
    for name, site in grundrisse(game, inhalt):
        bau = site["gebaeude"]
        basis = game._base_grid(bau)
        frei = game._walk_grid(site)
        breite, hoehe = bau["breite"], bau["hoehe"]
        punkte = []
        y = -0.55
        while y < hoehe + 0.6:
            x = -0.45
            while x < breite + 0.6:
                p = (round(x, 2), round(y, 2))
                punkte.append({"p": list(p),
                               "stehen": game.can_stand(p[0], p[1], site),
                               "naechste": list(game._nearest_free(p[0], p[1], frei)),
                               "raum": kennung(game.room_at(p[0], p[1], site)),
                               "person": kennung(game.person_at(p[0], p[1], site)),
                               "nahe": kennung(game.person_near(p[0], p[1], site))})
                x += 0.7
            y += 0.9
        start = tuple(bau["flur"].get("spieler", [2.4, 7.0]))
        mitten = [(r["x"] + r["w"] / 2.0, r["y"] + r["h"] / 2.0) for r in bau["raeume"]]
        paare = [(start, m, 0.0) for m in mitten]
        paare += [(start, tuple(p["platz"]), game.REACH) for p in site["kollegen"] if p.get("platz")]
        paare += [(start, tuple(p["p"]), 0.0) for p in punkte[::7]]
        paare += [(mitten[i], mitten[(i + 3) % len(mitten)], 0.0) for i in range(len(mitten))]
        paare += [(start, m, 0.5) for m in mitten[::2]] + [(start, m, 3.0) for m in mitten[1::2]]
        paare += [((-3.0, -3.0), start, 0.0), (start, (breite + 5.0, hoehe / 2.0), 0.0)]
        wege = [{"start": list(a), "ziel": list(b), "reach": r,
                 "weg": [list(p) for p in game.walk_path(a, b, reach=r, content=site)]}
                for a, b, r in paare]
        daten["grundrisse"].append({
            "name": name, "start": list(start),
            "raster": {"basis_anzahl": len(basis), "frei_anzahl": len(frei),
                       "basis": [list(c) for c in basis], "frei": [list(c) for c in frei]},
            "punkte": punkte, "wege": wege})
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G48 Wege: %d Grundrisse, %d Punkte, %d Wege -> %s"
          % (len(daten["grundrisse"]), sum(len(g["punkte"]) for g in daten["grundrisse"]),
             sum(len(g["wege"]) for g in daten["grundrisse"]), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
