#!/usr/bin/env python3
"""Baut ein Inhaltspaket (.fipaket) immer gleich (gleiche Bytes bei gleichem Inhalt).

Welle A 0.87.0 (Weisung P12-294, Plan P12-175 Abschnitt 1): ein Paket ist ein ZIP-Archiv mit dem Kopf paket.json
(format, art, kennung, name, stand, braucht_app, text, dateien mit sha256 und groesse) und den Dateien des Inhalts.
Feste Zeitstempel (1980-01-01), feste Reihenfolge (paket.json zuerst, dann nach Namen), Deflate.

Aufruf:
  python3 -I werkzeuge/pakete/paket_bauen.py <ziel.fipaket> --art stadt --kennung <k> --name <n> --stand <s> \
      --braucht-app "A 0.87.0" [--text <t>] [--format 1] <datei>=<quelle> ...
Beispiel: ... stadt.fistadt=tests/Lernplattform.Tests/Testdaten/testkarte_256.fistadt
"""
import argparse
import hashlib
import json
import sys
import zipfile


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


def main():
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
