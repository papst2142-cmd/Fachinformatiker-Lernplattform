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
      baut jedes Paket zweimal (gleiche Bytes), prueft Kopf, Pruefsummen und Reihenfolge, dazu Gegenproben.
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
    for f in funde:
        print("FEHLER:", f)
    print("paket_bauen selbsttest:", len(kennungen), "Pakete,", "gruen" if not funde else "rot")
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
