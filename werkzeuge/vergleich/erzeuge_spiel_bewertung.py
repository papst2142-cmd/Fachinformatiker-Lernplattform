#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G46 (Phase 7, W2 "Spiel-Kern Teil 1": Bewertung
der Tickets), gerechnet vom echten Python-Code der Python-Linie (v0.63.2,
eingefroren).

  balancing_einfach - fisi_game._eased_balancing fuer einen Spielstand mit
                      Schwierigkeit "einfach".
  aufgaben          - je Auftrag aus GAME die Loesung (find_solution, mit und
                      ohne bereitliegende Lagerteile) und eine feste Reihe
                      Antworten (Loesung, leer, gezielt falsch je Aufgabentyp).
                      Je Antwort: answer_problems, check_answer und evaluate in
                      zwei Lagen: A (ohne Hilfe, kein Wissen, Tag 3, Standard-
                      Balancing, nichts im Lager) mit allen Nutzdaten, B (mit
                      Hilfe, Wissen 100 %, Tag 17, Einfach-Balancing, Lager und
                      bereitliegende Teile) als SHA-256 der Nutzdaten.
  gespielt          - mit der echten Spiel-API gespielt (Game.solve); fuer jede
                      Auswertung die Zahl der Ereignisse davor, Auftrag,
                      Antwort, Hilfe, Wissen und die Nutzdaten. Der C#-Test baut
                      den Spielstand aus dem Protokoll auf und bereitet den
                      Auftrag selbst vor (prepared_task, available_parts, stock).
  texte             - parse_number, parse_prefix, parse_ip, _num, _euro,
                      plural, psu_needed fuer feste Eingaben.

Ausnahmen in Python werden als {"ausnahme": Klassenname} gespeichert. Keine
Lerndaten; alle Daten- und Einstellungsordner zeigen in einen leeren
Temp-Ordner, Uhr und uuid4 sind fest.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_spiel_bewertung.py <pfad_fisi_lernplattform> tests/Vergleich/spiel_bewertung.json
"""
import copy
import hashlib
import json
import os
import sys
import tempfile
import uuid

AUSSEHEN = {"haut": "hell", "haare": "kurz", "haarfarbe": "braun", "oberteil": "hoodie",
            "farbe": "blau", "hose": "jeans", "brille": False, "bart": False,
            "extra": "headset", "kreis": "gelb"}


def pruefsumme(v):
    text = json.dumps(v, ensure_ascii=False, separators=(",", ":"))
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def sicher(funktion, *args, **kwargs):
    try:
        return funktion(*args, **kwargs)
    except Exception as error:   # noqa: BLE001 - die Art der Ausnahme ist der Vergleichswert
        return {"ausnahme": type(error).__name__}


def hardware_ids(fg):
    hw = fg.GAME["hardware"]
    return [item["id"] for item in hw["teile"] + hw.get("ersatzteile", [])]


def mutationen(fg, task, loesung):
    """Feste Antworten je Aufgabentyp: (name, antwort)."""
    typ = task["typ"]
    result = [("loesung", loesung)]
    sol = copy.deepcopy(loesung)
    if typ == "auswahl":
        falsch = [o for o in task.get("optionen") or [] if o != task["antwort"]]
        result += [("falsch", falsch[0] if falsch else "x"), ("leer", ""), ("none", None)]
    elif typ == "zuordnung":
        paare = task["paare"]
        tausch = dict(paare)
        if len(paare) >= 2:
            tausch[paare[0][0]], tausch[paare[1][0]] = paare[1][1], paare[0][1]
        result += [("tausch", tausch), ("halb", {paare[0][0]: paare[0][1]}), ("leer", {})]
    elif typ == "bauteile":
        result.append(("leer", {}))
        if sol:
            for slot in list(sol):
                ohne = dict(sol)
                del ohne[slot]
                result.append(("ohne_" + slot, ohne))
            for slot in task["slots"]:
                andere = [i["id"] for i in fg.GAME["hardware"]["teile"] if i["typ"] == slot and i["id"] != sol.get(slot)]
                for pid in andere[:4]:
                    result.append(("%s=%s" % (slot, pid), dict(sol, **{slot: pid})))
            if "ram" in sol:
                result.append(("kombi", dict(sol, ram=sol["ram"] + "*2")))
            result.append(("unbekannt", dict(sol, cpu="gibtsnicht")))
    elif typ == "bestellung":
        result += [("leer", {}), ("null", {o["id"]: 0 for o in task["angebote"]}),
                   ("alle", {o["id"]: 1 for o in task["angebote"]})]
        for offer in task["angebote"]:
            result.append(("nur_" + offer["id"], {offer["id"]: 1}))
            if sol:
                result.append(("plus_" + offer["id"], dict(sol, **{offer["id"]: sol.get(offer["id"], 0) + 1})))
        if sol:
            result.append(("text", {k: str(v) for k, v in sol.items()}))
            result.append(("ein_weniger", {k: v - 1 for k, v in sol.items()}))
    elif typ == "rack":
        n = len(task["geraete"])
        size = task["schrank"]["he"]
        result += [("leer", {}), ("eins", {str(i): "1" for i in range(n)}),
                   ("oben", {str(i): size for i in range(n)}), ("erstes", {"0": 1}),
                   ("unsinn", {"x": 1, "99": 2, "0": "a", "-1": 3, "1": 0})]
        stapel, unten = {}, 1
        for i in range(n):
            stapel[str(i)] = unten
            unten += fg.rack_device(task["geraete"][i])["he"]
        result.append(("alle", stapel))
        stapel, unten = {}, 1
        for i in reversed(range(n)):
            stapel[str(i)] = unten
            unten += fg.rack_device(task["geraete"][i])["he"]
        result.append(("umgekehrt", stapel))
        if sol:
            result.append(("hoch", {k: v + 1 for k, v in sol.items()}))
            result.append(("zahlen", {int(k): float(v) for k, v in sol.items()}))
    elif typ == "formular":
        result.append(("leer", {}))
        if sol:
            felder = {f["id"]: f for f in fg.form_fields(task)}
            for fid in list(sol)[:6]:
                result.append(("falsch_" + fid, dict(sol, **{fid: "1"})))
                result.append(("leer_" + fid, dict(sol, **{fid: "  "})))
            anders = {}
            for fid, text in sol.items():
                art = felder[fid]["art"]
                if art == "geld":
                    anders[fid] = fg._euro(felder[fid]["soll"])
                elif art == "zahl":
                    anders[fid] = " %s TB " % text
                elif art == "prozent":
                    anders[fid] = "%s %%" % text
                elif art == "praefix":
                    p = felder[fid]["soll"]
                    mask = (0xFFFFFFFF << (32 - p)) & 0xFFFFFFFF
                    anders[fid] = "%d.%d.%d.%d" % (mask >> 24, (mask >> 16) & 255, (mask >> 8) & 255, mask & 255)
                elif art == "ip":
                    anders[fid] = " %s/24" % text
                else:
                    anders[fid] = "  %s " % text.upper()
            result.append(("anders", anders))
    elif typ == "terminal":
        schritte = task["schritte"]
        rechts = [fg.terminal_right_index(s) for s in schritte]
        falsch = [[i for i, b in enumerate(s["befehle"]) if not b.get("richtig")] for s in schritte]
        result += [("leer", {}), ("erster", {"schritte": [[r] for r in rechts[:1]]}),
                   ("umweg", {"schritte": [([f[0]] if f else []) + [r] for f, r in zip(falsch, rechts)]}),
                   ("zwei_umwege", {"schritte": [f[:2] + [r] for f, r in zip(falsch, rechts)]}),
                   ("nur_falsch", {"schritte": [f for f in falsch]}),
                   ("text", {"schritte": [[str(r), "x", None, 99] for r in rechts]}),
                   ("danach", {"schritte": [[r] + f for f, r in zip(falsch, rechts)]})]
    elif typ == "diagnose":
        result.append(("leer", {}))
        if sol:
            ursachen = [u for u in task.get("ursachen") or [] if u != task["ursache"]]
            massnahmen = [m for m in task.get("massnahmen") or [] if m != task["massnahme"]
                          and m not in (task.get("unsicher") or [])]
            if ursachen:
                result.append(("ursache_falsch", dict(sol, ursache=ursachen[0])))
            if massnahmen:
                result.append(("massnahme_falsch", dict(sol, massnahme=massnahmen[0])))
            if task.get("unsicher"):
                result.append(("unsicher", dict(sol, massnahme=task["unsicher"][0])))
            result.append(("alle_pruefungen", dict(sol, pruefungen=[p["id"] for p in task["pruefungen"]] * 2)))
            result.append(("keine_pruefung", dict(sol, pruefungen=[])))
            result.append(("ohne_ursache", dict(sol, ursache="")))
            if task.get("austausch"):
                ex = task["austausch"]
                katalog = [i["id"] for i in fg.spare_catalog(task)]
                passt_nicht = [pid for pid in katalog if fg.spare_fits(task, pid)]
                if passt_nicht:
                    result.append(("teil_falsch", dict(sol, teil=passt_nicht[0])))
                result.append(("teil_fremd", dict(sol, teil=hardware_ids(fg)[0])))
                result.append(("kein_teil", dict(sol, teil=None)))
                result.append(("reihenfolge_falsch", dict(sol, reihenfolge=list(reversed(ex["schritte"])))))
                result.append(("falscher_schritt", dict(sol, reihenfolge=list(ex["schritte"]) + list(ex.get("falsch") or [])[:1])))
    elif typ == "wartung":
        result.append(("leer", {}))
        if sol:
            punkte = list(sol["bewertung"])
            for pid in punkte[:3]:
                kipp = dict(sol["bewertung"])
                kipp[pid] = fg.RATING_OK if kipp[pid] == fg.RATING_ISSUE else fg.RATING_ISSUE
                result.append(("kipp_" + pid, dict(sol, bewertung=kipp)))
            result.append(("fehlt", dict(sol, bewertung={k: v for k, v in sol["bewertung"].items() if k != punkte[0]})))
            andere = [a for a in task.get("abschluss") or [] if a != task["abschluss_antwort"]]
            if andere:
                result.append(("abschluss_falsch", dict(sol, abschluss=andere[0])))
    return result


def aufgaben(fg, eased):
    ids = hardware_ids(fg)
    lager = {pid: i % 3 for i, pid in enumerate(ids)}
    alle_themen = {}
    for task in fg.GAME["aufgaben"]:
        for key in task.get("anforderungen") or {}:
            alle_themen[key] = 100.0
    result = []
    personen = [p["id"] for p in fg.all_people()] if hasattr(fg, "all_people") else []
    zaehler = {}
    liste = []
    for task in fg.GAME["aufgaben"]:
        liste.append((task, None))
        zaehler[task["typ"]] = zaehler.get(task["typ"], 0) + 1
        if zaehler[task["typ"]] > 3:
            continue
        # Zusaetze, die sonst erst der Spielstand setzt (prepared_task) oder nur selten vorkommen
        extra = {"zwischenfall": True, "verlust": 7, "achsen": ["sicherheit", "fachkompetenz", "sicherheit"]}
        if task["typ"] == "rack":
            extra["lager_ab"] = max(0, len(task["geraete"]) - 2)
        if task["typ"] == "bestellung":
            extra.update(kein_sparbonus=True, empfaenger=personen[0] if personen else "kunde")
        liste.append((dict(task, **extra), extra))
    for task, extra in liste:
        bereit_b = list(task.get("teile", [])) + [pid for pid in ids if lager[pid] > 0]
        loesung = sicher(fg.find_solution, task)
        loesung_b = sicher(fg.find_solution, task, bereit_b)
        eintrag = {"id": task["id"], "extra": extra, "loesung": loesung, "loesung_b": loesung_b, "antworten": []}
        if task["typ"] == "bestellung":
            koerbe = fg.valid_carts(task)
            eintrag["koerbe"] = {"anzahl": len(koerbe), "erste": koerbe[:3], "sha256": pruefsumme(koerbe)}
        antworten = mutationen(fg, task, loesung if not isinstance(loesung, dict) or "ausnahme" not in loesung else None)
        if task["typ"] == "bauteile" and loesung_b and loesung_b != loesung:
            antworten.append(("loesung_b", loesung_b))
        for name, answer in antworten:
            a = sicher(fg.evaluate, task, copy.deepcopy(answer), False, {}, 3)
            b = sicher(fg.evaluate, task, copy.deepcopy(answer), True, alle_themen, 17, eased, bereit_b, None, lager)
            pruef = sicher(fg.check_answer, task, copy.deepcopy(answer))
            probleme = sicher(fg.answer_problems, task, copy.deepcopy(answer))
            zeile = {"name": name, "antwort": answer, "pruefung": list(pruef) if isinstance(pruef, tuple) else pruef,
                     "a": a, "b": pruefsumme(b), "b_richtig": b.get("richtig") if "ausnahme" not in b else None}
            if probleme != a.get("probleme", []):
                zeile["probleme"] = probleme   # sonst gleich den Nutzdaten (a["probleme"] oder [])
            eintrag["antworten"].append(zeile)
        result.append(eintrag)
    return result


def zeitstempel(nummer):
    return "2026-10-%02d %02d:%02d:%02d" % (1 + nummer // 86400, (nummer // 3600) % 24,
                                            (nummer // 60) % 60, nummer % 60)


def gespielt(fg, core, tmp, name, tage, muster, schwierigkeit):
    """Mit der echten Spiel-API spielen; muster je Ticket: r (Loesung), f (gezielt falsch), v (verschoben)."""
    zaehler = [0]

    def stempel():
        zaehler[0] += 7
        return zeitstempel(zaehler[0]), 120

    db = core.DBManager(os.path.join(tmp, name + ".db"))
    db._stamp = stempel
    game = fg.Game(db, "PC")
    game.set_profile("Nico", AUSSEHEN, schwierigkeit)
    faelle = []
    original = fg.evaluate

    def mitschreiben(task, answer, used_help, levels, day, *rest, **kw):
        payload = original(task, answer, used_help, levels, day, *rest, **kw)
        faelle.append({"n": len(game.db.game_events(game.run)), "aufgabe": task["id"],
                       "antwort": copy.deepcopy(answer), "hilfe": used_help, "wissen": dict(levels),
                       "tag": day, "daten": copy.deepcopy(payload)})
        return payload
    fg.evaluate = mitschreiben
    try:
        nummer = 0
        for _tag in range(1, tage + 1):
            for _ in range(60):
                offen = game.state.open_tickets()
                if not offen:
                    break
                task = offen[0]
                art = muster[nummer % len(muster)]
                nummer += 1
                if art == "v":
                    game.defer(task["id"])
                    continue
                prepared = game.state.prepared_task(task)
                found = fg.find_solution(prepared, game.state.available_parts(prepared))
                answer = found if found is not None else {}
                if art == "f":
                    falsche = mutationen(fg, prepared, found)[1:]
                    if falsche:
                        answer = falsche[nummer % len(falsche)][1]
                try:
                    game.solve(task["id"], answer, nummer % 5 == 0)
                except (ValueError, TypeError, KeyError, AttributeError):
                    game.defer(task["id"])
            game.end_day()
    finally:
        fg.evaluate = original
    events = [[t, k, d] for t, k, d in game.db.game_events(game.run)]
    return {"name": name, "ereignisse": events, "faelle": faelle}


def endlich(v):
    """inf und nan als Text (JSON kennt sie nicht)."""
    if isinstance(v, float) and (v != v or v in (float("inf"), float("-inf"))):
        return repr(v)
    return v


def texte(fg):
    zahlen = ["", "  ", "12000", "1.234,50 €", "16 TB", "66,7 %", "0.125", "1.234", "12.500", "-1.234.567",
              "1,5", "1.5", "abc", "3 Platten", "1 Platte", "4 hosts", "2 HOSTS", "inf", "-Infinity", "nan",
              "1_000", "1__0", "_1", "1e3", "1E-2", ".5", "5.", "+7", "١٢٣", "1 000", "12 eur",
              "0012.500", "1.23", "1.2345", "1,234.5", "€ 5", "5 €€", "—", "5 Hoſts", "7 hoſt", "İnf", "INFINITY", "NaN", None]
    praefixe = ["/24", "24", " 26 ", "/33", "32", "0", "/", "", "255.255.255.0", "255.255.255.192",
                "255.0.255.0", "0.0.0.0", "255.255.255.255", "256.0.0.0", "1.2.3", "01", "/024", "abc", None,
                "255.255.255.128/25", "١٢"]
    ips = ["192.168.0.1", " 10.0.0.5/24 ", "10.0.0.256", "010.0.0.1", "1.2.3", "::1", "", None, "1.2.3.4/x",
           "192.168.1.1 ", "0.0.0.0", "255.255.255.255"]
    nums = [0, 1, -1, 1234, 1234567, -1234.5, 0.5, 2.25, 2.675, 1e16, 12.0, -0.0, -0.001, 0.005, 0.015,
            1234.5678, 999.995, True]
    euros = [0, 1, 1234, -1234.5, 0.005, 0.015, 2.675, 1234567.891, 12.0, -0.001, 1e9]
    hw = fg.GAME["hardware"]["teile"]
    cpus = [i for i in hw if i["typ"] == "cpu"]
    gpus = [i for i in hw if i["typ"] == "gpu"]
    return {
        "zahl": [[t, endlich(fg.parse_number(t))] for t in zahlen],
        "praefix": [[t, sicher(fg.parse_prefix, t)] for t in praefixe],
        "ip": [[t, fg.parse_ip(t)] for t in ips],
        "num": [[v, fg._num(v)] for v in nums] + [[repr(v), fg._num(v)] for v in (float("inf"), float("nan"))],
        "euro": [[v, fg._euro(v)] for v in euros],
        "plural": [[n, fg.plural(n, "Arbeitstag", "Arbeitstage")] for n in (0, 1, 2, 1000, -1, 1234567)],
        "netzteil": [[c["id"], g["id"] if g else None, fg.psu_needed(c, g)] for c in cpus for g in gpus[:3] + [None]],
    }


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
    zufall = [0]

    def feste_uuid():
        zufall[0] += 1
        return uuid.UUID(int=zufall[0] * 7919)
    uuid.uuid4 = feste_uuid
    import fisi_core as core
    import fisi_game as fg

    einfach = [("2026-10-01 08:00:00", fg.EV_DIFFICULTY, {"stufe": fg.DIFF_EASY}),
               ("2026-10-01 08:00:01", fg.EV_PROFILE, {"name": "Nico", "aussehen": AUSSEHEN})]
    eased = fg._eased_balancing(fg.GameState(einfach))
    daten = {
        "einfach_ereignisse": [list(e) for e in einfach],
        "balancing_einfach": {"fehlerkosten": eased["fehlerkosten"], "verlust_faktor": eased.get("verlust_faktor"),
                              "sha256": pruefsumme(eased)},
        "aufgaben": aufgaben(fg, eased),
        "gespielt": [gespielt(fg, core, tmp, "normal", 70, "rrfrvrf", fg.DIFF_NORMAL),
                     gespielt(fg, core, tmp, "einfach", 45, "rfrrff", fg.DIFF_EASY)],
        "texte": texte(fg),
    }
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    anzahl = sum(len(a["antworten"]) for a in daten["aufgaben"])
    faelle = sum(len(g["faelle"]) for g in daten["gespielt"])
    print("G46: %d Auftraege, %d Antworten, %d gespielte Auswertungen -> %s"
          % (len(daten["aufgaben"]), anzahl, faelle, ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
