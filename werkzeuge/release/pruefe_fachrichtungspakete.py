#!/usr/bin/env python3
"""Welle A 1.5.0 (Weisung P12-358, Sammelliste 386, Nico Zusatz a): vor jedem Release der Lernplattform muessen die drei
Fachrichtungs-Pakete lernen_ae, lernen_dpa, lernen_dv als normale Releases (kein Entwurf, kein Vorab) veroeffentlicht sein,
und ihre Datei muss Byte fuer Byte die echte Testdatei dieses Commits sein (digest von GitHub = SHA-256 der Datei in
tests/Lernplattform.Tests/Testdaten/pakete_echt/). Seit A 1.5.0 bringt die App FIAE, FIDP und FIDV nicht mehr selbst mit: ohne
diese Pruefung gaebe es eine Zeit mit leerer Fachrichtung. Nichts hier laedt hoch oder aendert etwas (python3.12 -I).

  pruefe_fachrichtungspakete.py pruefen
      fragt GitHub (gh api, Umgebung GITHUB_REPOSITORY und GH_TOKEN) nach vPlernen_<fr>.<stand>; rot mit FEHLER-Zeile.
  pruefe_fachrichtungspakete.py selbsttest
      dieselbe Pruefung gegen gebaute Antworten, mit Gegenproben (fehlt, Entwurf, Vorab, falsche Pruefsumme, Datei fehlt).
"""
import hashlib
import importlib.util
import json
import os
import subprocess
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
WURZEL = os.path.dirname(os.path.dirname(HIER))
_spec = importlib.util.spec_from_file_location("paket_release", os.path.join(WURZEL, "werkzeuge", "pakete", "paket_release.py"))
paket_release = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(paket_release)
KENNUNGEN = ("lernen_ae", "lernen_dpa", "lernen_dv")


def _sha(pfad):
    with open(pfad, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def funde(holen):
    """holen(tag) -> Release als dict wie die GitHub-API oder None (gibt es nicht)."""
    ergebnis = []
    for k in KENNUNGEN:
        w = paket_release.lernen_kopf(k)
        echt = paket_release.lernen_echt(k)
        if not os.path.exists(echt):
            ergebnis.append("%s: echte Testdatei fehlt im Commit" % w["DATEI"])
            continue
        release = holen(w["TAG"])
        if release is None:
            ergebnis.append("%s ist nicht veroeffentlicht (zuerst die drei Fachrichtungs-Pakete, dann die App)" % w["TAG"])
            continue
        if release.get("draft") or release.get("prerelease"):
            ergebnis.append("%s ist noch Entwurf oder Vorab (die App saehe es nicht)" % w["TAG"])
            continue
        dateien = [a for a in release.get("assets", []) if a.get("name") == w["DATEI"]]
        if len(dateien) != 1:
            ergebnis.append("%s: Datei %s fehlt im Release" % (w["TAG"], w["DATEI"]))
            continue
        if dateien[0].get("digest") != "sha256:" + _sha(echt):
            ergebnis.append("%s: Pruefsumme passt nicht zur Testdatei des Commits" % w["TAG"])
    return ergebnis


def github(tag):
    repo = os.environ.get("GITHUB_REPOSITORY", "")
    if not repo:
        raise ValueError("GITHUB_REPOSITORY fehlt")
    lauf = subprocess.run(["gh", "api", "repos/%s/releases/tags/%s" % (repo, tag)], capture_output=True, text=True)
    if lauf.returncode != 0:
        if "Not Found" in lauf.stdout + lauf.stderr or "404" in lauf.stderr:
            return None
        raise ValueError("gh api %s: %s" % (tag, (lauf.stderr or lauf.stdout).strip()[:300]))
    return json.loads(lauf.stdout)


def selbsttest():
    gut = {}
    for k in KENNUNGEN:
        w = paket_release.lernen_kopf(k)
        gut[w["TAG"]] = {"tag_name": w["TAG"], "draft": False, "prerelease": False,
                         "assets": [{"name": w["DATEI"], "digest": "sha256:" + _sha(paket_release.lernen_echt(k))}]}
    rot = []
    if funde(gut.get):
        rot.append("gute Antworten sind rot: " + "; ".join(funde(gut.get)))
    erster = sorted(gut)[0]

    def mit(aendern):
        neu = json.loads(json.dumps(gut))
        aendern(neu)
        return neu.get

    gegen = {
        "fehlt": mit(lambda r: r.pop(erster)),
        "Entwurf": mit(lambda r: r[erster].update(draft=True)),
        "Vorab": mit(lambda r: r[erster].update(prerelease=True)),
        "Pruefsumme": mit(lambda r: r[erster]["assets"][0].update(digest="sha256:" + "0" * 64)),
        "Datei fehlt": mit(lambda r: r[erster].update(assets=[])),
    }
    for name, holen in gegen.items():
        if not funde(holen):
            rot.append("Gegenprobe %s nicht rot" % name)
    for f in rot:
        print("FEHLER: Selbsttest Fachrichtungs-Pakete: " + f)
    if not rot:
        print("Selbsttest Fachrichtungs-Pakete: %d Pakete, %d Gegenproben rot" % (len(KENNUNGEN), len(gegen)))
    return 1 if rot else 0


def main():
    a = sys.argv[1:]
    try:
        if a == ["selbsttest"]:
            return selbsttest()
        if a == ["pruefen"]:
            liste = funde(github)
            for f in liste:
                print("FEHLER: " + f)
            if not liste:
                print("Fachrichtungs-Pakete veroeffentlicht und gleich den Testdateien: " + ", ".join(KENNUNGEN))
            return 1 if liste else 0
    except (ValueError, OSError) as e:
        print("FEHLER: " + str(e))
        return 1
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main())
