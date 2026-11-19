#!/usr/bin/env python3
"""Baut ein Inhaltspaket (.fipaket) immer gleich (gleiche Bytes bei gleichem Inhalt).

Welle A 0.87.0 (Weisung P12-294, Plan P12-175 Abschnitt 1): ein Paket ist ein ZIP-Archiv mit dem Kopf paket.json
(format, art, kennung, name, stand, braucht_app, text, dateien mit sha256 und groesse) und den Dateien des Inhalts.
Feste Zeitstempel (1980-01-01), feste Reihenfolge (paket.json zuerst, dann nach Namen), Deflate.

Aufruf:
  python3 -I werkzeuge/pakete/paket_bauen.py <ziel.fipaket> --art stadt --kennung <k> --name <n> --stand <s> \
      --braucht-app "A 0.87.0" [--text <t>] [--format 1] <datei>=<quelle> ...
Beispiel: ... stadt.fistadt=tests/Lernplattform.Tests/Testdaten/testkarte_256.fistadt

Welle A 0.89.0 (Weisung P12-297 W3-2): Pakete liegen im Repo unter pakete/<kennung>/ (paket.json ohne "dateien" plus die
Dateien des Inhalts, keine Unterordner).
  python3 -I werkzeuge/pakete/paket_bauen.py ordner pakete/<kennung> <zielordner>
      baut <zielordner>/<kennung>_<stand>.fipaket und gibt den Pfad aus.
  python3 -I werkzeuge/pakete/paket_bauen.py selbsttest [pakete]
      baut jedes Paket zweimal (gleiche Bytes), prueft Kopf, Pruefsummen und Reihenfolge, dazu Gegenproben; seit A 0.91.0
      auch die drei Fachrichtungs-Pakete aus inhalte_csharp/lernen (Schnitt, Rueckbau, echte Testdatei, Gegenproben).

Welle A 0.91.0 (Weisung P12-300, Kurzplan P12-180, Nico 2c 5a): Fachrichtungs-Pakete (Art lernen, Kennung lernen_<fr>).
Eine Quelle: das Werkzeug schneidet das Paket aus inhalte_csharp/lernen/ im Repo, immer gleich (gleiche Bytes bei gleichem
Inhalt). Jeder Eintrag traegt seine feste Stelle in der ganzen Datei (der Lernstand merkt sich Szenarien und Testprojekte
ueber diese Stelle).
  python3 -I werkzeuge/pakete/paket_bauen.py lernen <ae|dpa|dv> <zielordner> --stand <n> --braucht-app "A 0.91.0" [--repo .]
      baut <zielordner>/lernen_<fr>_<stand>.fipaket und gibt den Pfad aus.
"""
import argparse
import hashlib
import io
import json
import os
import re
import sys
import tempfile
import zipfile

KOPF_FELDER = {"art", "braucht_app", "format", "kennung", "name", "stand", "text"}
FACHRICHTUNGEN = ("ae", "dpa", "dv")
LERNEN_LISTEN = ("karteikarten", "quizfragen", "szenarien_ap2", "projektarbeiten", "fachgespraech")
LERNEN_ORDNER = os.path.join("inhalte_csharp", "lernen")
# echte Testdateien der App (Stand 1, je Fachrichtung); nach jeder Inhalte-Aenderung einer Fachrichtung neu bauen
# (selbsttest prueft die Gleichheit mit dem heutigen Bau)
LERNEN_TESTDATEI = (1, "A 0.91.0")
KENNUNG = re.compile(r"^[a-z][a-z0-9_]{1,31}\Z")
RESERVIERT = {"stadt1", "falschlieferung"}


def bauen(ziel, kopf, dateien):
    eintraege = {}
    for name, quelle in dateien:
        with open(quelle, "rb") as f:
            eintraege[name] = f.read()
    kopf = dict(kopf)
    kopf["dateien"] = {n: {"sha256": hashlib.sha256(b).hexdigest(), "groesse": len(b)} for n, b in sorted(eintraege.items())}
    kopf_bytes = (json.dumps(kopf, ensure_ascii=False, indent=1, sort_keys=True) + "\n").encode("utf-8")
    with zipfile.ZipFile(ziel, "w", compression=zipfile.ZIP_DEFLATED) as z:
        for name, inhalt in [("paket.json", kopf_bytes)] + sorted(eintraege.items()):
            info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            z.writestr(info, inhalt, compresslevel=9)


def quelle_lesen(ordner):
    """Kopf und Dateien aus pakete/<kennung>/; jede Abweichung = ValueError mit Grund."""
    kennung = os.path.basename(os.path.normpath(ordner))
    with open(os.path.join(ordner, "paket.json"), encoding="utf-8") as f:
        kopf = json.load(f)
    if not isinstance(kopf, dict) or set(kopf) != KOPF_FELDER:
        raise ValueError(kennung + ": paket.json braucht genau die Felder " + ", ".join(sorted(KOPF_FELDER)))
    if kopf["kennung"] != kennung or not KENNUNG.match(kennung) or kennung in RESERVIERT:
        raise ValueError(kennung + ": Kennung passt nicht zum Ordner oder ist nicht erlaubt")
    if kopf["format"] != 1 or kopf["art"] != "stadt" or not isinstance(kopf["stand"], int) or kopf["stand"] < 1:
        raise ValueError(kennung + ": format 1, art stadt und stand ab 1 noetig")
    if not isinstance(kopf["name"], str) or not 0 < len(kopf["name"]) <= 40 or kopf["name"].strip() != kopf["name"]:
        raise ValueError(kennung + ": name braucht 1 bis 40 Zeichen")
    dateien = []
    for name in sorted(os.listdir(ordner)):
        pfad = os.path.join(ordner, name)
        if name == "paket.json":
            continue
        if not os.path.isfile(pfad):
            raise ValueError(kennung + ": Unterordner sind nicht erlaubt: " + name)
        dateien.append((name, pfad))
    if [n for n, _ in dateien] != ["stadt.fistadt"]:
        raise ValueError(kennung + ": ein Stadt-Paket enthaelt genau stadt.fistadt")
    return kopf, dateien


def _json(obj):
    return (json.dumps(obj, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")


def _laden(repo, name):
    with open(os.path.join(repo, LERNEN_ORDNER, name + ".json"), encoding="utf-8") as f:
        return json.load(f)


def _liste(repo, name):
    daten = _laden(repo, name)
    return daten["fragen"] if name == "fachgespraech" else daten


def lernen_schnitt(repo, fr):
    """(lernen.json, pruefungssaetze.json) als Bytes: alle Eintraege mit fachrichtung == fr und ihrer Stelle."""
    if fr not in FACHRICHTUNGEN:
        raise ValueError("Fachrichtung nur ae, dpa oder dv: " + str(fr))
    listen = {}
    for name in LERNEN_LISTEN:
        eintraege = [{"stelle": i, "eintrag": e} for i, e in enumerate(_liste(repo, name))
                     if isinstance(e, dict) and e.get("fachrichtung") == fr and e.get("im_paket") is not True]
        if eintraege:
            listen[name] = eintraege
    stellen = {name: {e["stelle"]: e["eintrag"] for e in liste} for name, liste in listen.items()}
    ausschluss = [a for a in _laden(repo, "ausschluss")["eintraege"]
                  if a["stelle"] in stellen.get(a["datei"], {}) and stellen[a["datei"]][a["stelle"]].get("q") == a["q"]]
    lernen = {"format": 1, "fachrichtung": fr, "listen": listen}
    saetze_quelle = _laden(repo, "pruefungssaetze")
    # A 0.94.0 (Weisung P12-304): ersetzte Pruefungssaetze dieser Fachrichtung (Stelle und Kennung) kommen mit
    satz_stellen = {i: s for i, s in enumerate(saetze_quelle["saetze"]) if isinstance(s, dict) and s.get("fachrichtung") == fr}
    ausschluss += [a for a in _laden(repo, "ausschluss")["eintraege"]
                   if a["datei"] == "pruefungssaetze" and a["stelle"] in satz_stellen
                   and satz_stellen[a["stelle"]].get("kennung") == a.get("kennung")]
    if ausschluss:
        lernen["ausschluss"] = [{"datei": a["datei"], "stelle": a["stelle"], "kennung": a["kennung"]} if a["datei"] == "pruefungssaetze"
                                else {"datei": a["datei"], "stelle": a["stelle"], "q": a["q"]} for a in ausschluss]
    eigene = [{"stelle": i, "satz": s} for i, s in enumerate(saetze_quelle["saetze"])
              if isinstance(s, dict) and s.get("fachrichtung") == fr and s.get("im_paket") is not True]
    # A 0.94.0: Format 3 nur, wenn ein Satz das Satzpaar ("aufbau") braucht; sonst bleibt das Paket im Format 2
    # (DPA/DV bytegleich, auch fuer Apps vor A 0.94.0 lesbar)
    saetze = {"format": saetze_quelle["format"] if any("aufbau" in e["satz"] for e in eigene) else min(saetze_quelle["format"], 2),
              "saetze": eigene}
    return _json(lernen), _json(saetze)


def lernen_bauen(repo, fr, ziel_ordner, stand, braucht_app):
    if not isinstance(stand, int) or stand < 1:
        raise ValueError("stand ab 1 noetig")
    lernen, saetze = lernen_schnitt(repo, fr)
    kopf = {"format": 1, "art": "lernen", "kennung": "lernen_" + fr, "name": "Fachrichtungs-Paket " + fr.upper(),
            "stand": stand, "braucht_app": braucht_app,
            "text": "Lerninhalte der Fachrichtung " + fr.upper()
            + ": Karteikarten, Quiz, AP2-Szenarien, Testprojekte, Fachgespräch und Prüfungssätze."}
    ziel = os.path.join(ziel_ordner, "lernen_%s_%d.fipaket" % (fr, stand))
    with tempfile.TemporaryDirectory() as tmp:
        dateien = []
        for name, inhalt in (("lernen.json", lernen), ("pruefungssaetze.json", saetze)):
            pfad = os.path.join(tmp, name)
            with open(pfad, "wb") as f:
                f.write(inhalt)
            dateien.append((name, pfad))
        bauen(ziel, kopf, dateien)
    return ziel


def lernen_rueckbau(repo, fr, lernen, saetze):
    """Funde, wenn App-Dateien mit Platzhaltern + Paket nicht genau die Dateien im Repo ergeben."""
    funde = []
    paket = json.loads(lernen)
    for name in LERNEN_LISTEN:
        original = _liste(repo, name)
        rueck = [{"fachrichtung": fr, "im_paket": True} if isinstance(e, dict) and e.get("fachrichtung") == fr else e for e in original]
        for e in paket["listen"].get(name, []):
            if e["stelle"] >= len(rueck) or rueck[e["stelle"]] != {"fachrichtung": fr, "im_paket": True}:
                funde.append("%s %s: Stelle %d passt nicht" % (fr, name, e["stelle"]))
                continue
            rueck[e["stelle"]] = e["eintrag"]
        if rueck != original:
            funde.append("%s %s: Rueckbau ungleich" % (fr, name))
    s = json.loads(saetze)
    original = _laden(repo, "pruefungssaetze")["saetze"]
    rueck = [None if isinstance(x, dict) and x.get("fachrichtung") == fr else x for x in original]
    for e in s["saetze"]:
        if e["stelle"] < len(rueck) and rueck[e["stelle"]] is None:
            rueck[e["stelle"]] = e["satz"]
    if rueck != original:
        funde.append(fr + " pruefungssaetze: Rueckbau ungleich")
    return funde


def lernen_selbsttest(repo):
    funde = []
    if not os.path.isdir(os.path.join(repo, LERNEN_ORDNER)):
        return [repo + ": " + LERNEN_ORDNER + " fehlt"]
    with tempfile.TemporaryDirectory() as tmp:
        for fr in FACHRICHTUNGEN:
            a, b = os.path.join(tmp, "a"), os.path.join(tmp, "b")
            os.makedirs(a, exist_ok=True)
            os.makedirs(b, exist_ok=True)
            pa, pb = lernen_bauen(repo, fr, a, 1, "A 0.91.0"), lernen_bauen(repo, fr, b, 1, "A 0.91.0")
            with open(pa, "rb") as f:
                ba = f.read()
            with open(pb, "rb") as f:
                if f.read() != ba:
                    funde.append("lernen_" + fr + ": zweimal gebaut, aber nicht bytegleich")
            with zipfile.ZipFile(io.BytesIO(ba)) as z:
                if z.namelist() != ["paket.json", "lernen.json", "pruefungssaetze.json"]:
                    funde.append("lernen_" + fr + ": Dateien im Archiv " + ",".join(z.namelist()))
                kopf = json.loads(z.read("paket.json"))
                if kopf["art"] != "lernen" or kopf["kennung"] != "lernen_" + fr or kopf["name"] != "Fachrichtungs-Paket " + fr.upper():
                    funde.append("lernen_" + fr + ": Kopf falsch")
                lernen, saetze = z.read("lernen.json"), z.read("pruefungssaetze.json")
                for n, angabe in kopf["dateien"].items():
                    inhalt = z.read(n)
                    if hashlib.sha256(inhalt).hexdigest() != angabe["sha256"] or len(inhalt) != angabe["groesse"]:
                        funde.append("lernen_" + fr + ": Pruefsumme von " + n + " falsch")
            paket = json.loads(lernen)
            zahlen = {n: len(paket["listen"].get(n, [])) for n in LERNEN_LISTEN}
            erwartet = {n: sum(1 for e in _liste(repo, n) if isinstance(e, dict) and e.get("fachrichtung") == fr) for n in LERNEN_LISTEN}
            if zahlen != erwartet or not all(zahlen[n] for n in LERNEN_LISTEN):
                funde.append("lernen_%s: Zahlen %s statt %s" % (fr, zahlen, erwartet))
            if any(e["eintrag"].get("fachrichtung") != fr for n in LERNEN_LISTEN for e in paket["listen"].get(n, [])):
                funde.append("lernen_" + fr + ": fremder Eintrag im Paket")
            funde.extend(lernen_rueckbau(repo, fr, lernen, saetze))
            # Gegenprobe: eine verschobene Stelle faellt im Rueckbau auf
            verschoben = json.loads(lernen)
            verschoben["listen"]["szenarien_ap2"][0]["stelle"] += 1
            if not lernen_rueckbau(repo, fr, _json(verschoben), saetze):
                funde.append("lernen_" + fr + ": Gegenprobe (Stelle verschoben) nicht erkannt")
            # Gegenprobe: ein anderer Text faellt auf
            anders = json.loads(lernen)
            anders["listen"]["karteikarten"][0]["eintrag"]["q"] = anders["listen"]["karteikarten"][0]["eintrag"].get("q", "") + " "
            if not lernen_rueckbau(repo, fr, _json(anders), saetze):
                funde.append("lernen_" + fr + ": Gegenprobe (Eintrag geaendert) nicht erkannt")
            # echte Testdatei der App: gleiche Bytes wie der heutige Bau
            echt = os.path.join(repo, "tests", "Lernplattform.Tests", "Testdaten", "pakete_echt", "lernen_%s_%d.fipaket" % (fr, LERNEN_TESTDATEI[0]))
            c = os.path.join(tmp, "c")
            os.makedirs(c, exist_ok=True)
            with open(lernen_bauen(repo, fr, c, LERNEN_TESTDATEI[0], LERNEN_TESTDATEI[1]), "rb") as f:
                neu = f.read()
            if not os.path.exists(echt):
                funde.append("Testdatei " + os.path.basename(echt) + " fehlt")
            else:
                with open(echt, "rb") as f:
                    if f.read() != neu:
                        funde.append("Testdatei " + os.path.basename(echt) + " weicht vom Bau ab (neu bauen: paket_bauen.py lernen " + fr + " "
                                     + "tests/Lernplattform.Tests/Testdaten/pakete_echt --stand 1 --braucht-app \"A 0.91.0\")")
        try:
            lernen_schnitt(repo, "si")
            funde.append("Gegenprobe Fachrichtung si: nicht abgelehnt")
        except ValueError:
            pass
    return funde


def ordner_bauen(ordner, ziel_ordner):
    kopf, dateien = quelle_lesen(ordner)
    ziel = os.path.join(ziel_ordner, "%s_%d.fipaket" % (kopf["kennung"], kopf["stand"]))
    bauen(ziel, kopf, dateien)
    return ziel


def selbsttest(wurzel):
    funde = []
    kennungen = sorted(k for k in os.listdir(wurzel) if os.path.isdir(os.path.join(wurzel, k)))
    if not kennungen:
        funde.append("kein Paket in " + wurzel)
    with tempfile.TemporaryDirectory() as tmp:
        for k in kennungen:
            a, b = os.path.join(tmp, "a"), os.path.join(tmp, "b")
            os.makedirs(a, exist_ok=True)
            os.makedirs(b, exist_ok=True)
            try:
                pa, pb = ordner_bauen(os.path.join(wurzel, k), a), ordner_bauen(os.path.join(wurzel, k), b)
            except (ValueError, OSError, json.JSONDecodeError) as e:
                funde.append(str(e))
                continue
            with open(pa, "rb") as f:
                ba = f.read()
            with open(pb, "rb") as f:
                bb = f.read()
            if ba != bb:
                funde.append(k + ": zweimal gebaut, aber nicht bytegleich")
            with zipfile.ZipFile(io.BytesIO(ba)) as z:
                namen = z.namelist()
                if namen != ["paket.json"] + sorted(n for n in namen if n != "paket.json"):
                    funde.append(k + ": Reihenfolge im Archiv falsch")
                kopf = json.loads(z.read("paket.json"))
                for n, angabe in kopf["dateien"].items():
                    inhalt = z.read(n)
                    if hashlib.sha256(inhalt).hexdigest() != angabe["sha256"] or len(inhalt) != angabe["groesse"]:
                        funde.append(k + ": Pruefsumme von " + n + " falsch")
                if any(i.date_time != (1980, 1, 1, 0, 0, 0) for i in z.infolist()):
                    funde.append(k + ": Zeitstempel nicht fest")
            # liegt das gebaute Paket als echte Testdatei der App im Repo, muss es dieselben Bytes haben
            echt = os.path.join(os.path.dirname(os.path.abspath(wurzel)), "tests", "Lernplattform.Tests", "Testdaten", "pakete_echt", os.path.basename(pa))
            if os.path.exists(echt):
                with open(echt, "rb") as f:
                    if f.read() != ba:
                        funde.append(k + ": Testdatei " + os.path.basename(pa) + " weicht vom Bau ab (neu bauen)")
            # derselbe Bau ueber den alten Aufruf mit Feldern ergibt dieselben Bytes
            kopf_q, dateien_q = quelle_lesen(os.path.join(wurzel, k))
            alt = os.path.join(tmp, "alt.fipaket")
            bauen(alt, kopf_q, dateien_q)
            with open(alt, "rb") as f:
                if f.read() != ba:
                    funde.append(k + ": Aufruf mit Feldern baut andere Bytes")
            # Gegenprobe: ein Byte anders in der Stadt = andere Bytes und andere Pruefsumme
            anders = os.path.join(tmp, "anders", k)
            os.makedirs(anders, exist_ok=True)
            for n in os.listdir(os.path.join(wurzel, k)):
                with open(os.path.join(wurzel, k, n), "rb") as f:
                    daten = f.read()
                if n == "stadt.fistadt":
                    daten = daten + b" "
                with open(os.path.join(anders, n), "wb") as f:
                    f.write(daten)
            c = os.path.join(tmp, "c")
            os.makedirs(c, exist_ok=True)
            with open(ordner_bauen(anders, c), "rb") as f:
                if f.read() == ba:
                    funde.append(k + ": Gegenprobe (anderer Inhalt) baut dieselben Bytes")
        # Gegenproben der Quelle: falsches Feld, Unterordner, fremde Kennung
        probe = os.path.join(tmp, "probe_fehler")
        os.makedirs(os.path.join(probe, "unter"))
        with open(os.path.join(probe, "paket.json"), "w", encoding="utf-8") as f:
            json.dump({"art": "stadt", "braucht_app": "A 0.89.0", "format": 1, "kennung": "probe_fehler", "name": "x", "stand": 1, "text": ""}, f)
        with open(os.path.join(probe, "stadt.fistadt"), "wb") as f:
            f.write(b"{}")
        for fall, aendern in [("Unterordner", lambda: None),
                              ("Feld dateien", lambda: _kopf_setzen(probe, "dateien", {})),
                              ("Kennung", lambda: _kopf_setzen(probe, "kennung", "stadt1"))]:
            aendern()
            try:
                quelle_lesen(probe)
                funde.append("Gegenprobe " + fall + ": nicht erkannt")
            except ValueError:
                pass
            if fall == "Unterordner":
                os.rmdir(os.path.join(probe, "unter"))
    # Welle A 0.91.0: die drei Fachrichtungs-Pakete aus dem Repo (Wurzel = Ordner ueber pakete/)
    funde.extend(lernen_selbsttest(os.path.dirname(os.path.abspath(wurzel))))
    for f in funde:
        print("FEHLER:", f)
    print("paket_bauen selbsttest:", len(kennungen), "Pakete,", len(FACHRICHTUNGEN), "Fachrichtungs-Pakete,", "gruen" if not funde else "rot")
    return 1 if funde else 0


def _kopf_setzen(ordner, feld, wert):
    with open(os.path.join(ordner, "paket.json"), encoding="utf-8") as f:
        kopf = json.load(f)
    kopf.pop("dateien", None)
    if feld == "kennung":
        kopf["kennung"] = wert
    else:
        kopf[feld] = wert
    with open(os.path.join(ordner, "paket.json"), "w", encoding="utf-8") as f:
        json.dump(kopf, f)


def main():
    if len(sys.argv) >= 2 and sys.argv[1] == "ordner":
        if len(sys.argv) != 4:
            sys.exit("FEHLER: ordner <pakete/kennung> <zielordner>")
        try:
            print(ordner_bauen(sys.argv[2], sys.argv[3]))
        except ValueError as e:
            sys.exit("FEHLER: " + str(e))
        return
    if len(sys.argv) >= 2 and sys.argv[1] == "lernen":
        q = argparse.ArgumentParser(prog="paket_bauen.py lernen")
        q.add_argument("fachrichtung")
        q.add_argument("zielordner")
        q.add_argument("--stand", type=int, required=True)
        q.add_argument("--braucht-app", required=True)
        q.add_argument("--repo", default=".")
        b = q.parse_args(sys.argv[2:])
        try:
            print(lernen_bauen(b.repo, b.fachrichtung, b.zielordner, b.stand, b.braucht_app))
        except (ValueError, OSError, KeyError) as e:
            sys.exit("FEHLER: " + str(e))
        return
    if len(sys.argv) >= 2 and sys.argv[1] == "selbsttest":
        sys.exit(selbsttest(sys.argv[2] if len(sys.argv) > 2 else "pakete"))
    p = argparse.ArgumentParser()
    p.add_argument("ziel")
    p.add_argument("--art", required=True)
    p.add_argument("--kennung", required=True)
    p.add_argument("--name", required=True)
    p.add_argument("--stand", type=int, required=True)
    p.add_argument("--braucht-app", required=True)
    p.add_argument("--text", default="")
    p.add_argument("--format", type=int, default=1)
    p.add_argument("dateien", nargs="+")
    a = p.parse_args()
    dateien = []
    for d in a.dateien:
        if "=" not in d:
            sys.exit("FEHLER: Datei als <name>=<quelle> angeben: " + d)
        name, quelle = d.split("=", 1)
        dateien.append((name, quelle))
    kopf = {"format": a.format, "art": a.art, "kennung": a.kennung, "name": a.name, "stand": a.stand,
            "braucht_app": a.braucht_app, "text": a.text}
    bauen(a.ziel, kopf, dateien)
    print("Paket geschrieben:", a.ziel)


if __name__ == "__main__":
    main()
