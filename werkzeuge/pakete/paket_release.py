#!/usr/bin/env python3
"""Welle A 0.89.0 (Weisung P12-297 W3-3/W3-4, Plan P12-175 Abschnitt 4): Hilfen fuer paket_release.yml und die Proben
des echten Ladens (python3.12 -I). Nichts hier laedt hoch, legt einen Tag an oder loescht etwas.

  paket_release.py kopf <pakete/kennung>
      prueft die Quelle (wie paket_bauen.py) und gibt KENNUNG=, STAND=, TAG= (vP<kennung>.<stand>), DATEI=, NAME=, ART= aus.
  paket_release.py text <pakete/kennung> <paket.fipaket> <release.md>
      schreibt den Release-Text: Zeile "Art: <art>" (die App liest daraus die Art), Zeile mit dem Text des Pakets (die App
      zeigt sie), Beschreibung, Pruefsummen-Tabelle (wie release.py digest-text sie liest) und "Release-Seite:".
  paket_release.py github-probe <pakete> <ziel.json>
      schreibt eine Release-Liste wie die GitHub-API sie liefert (Adresse http://PAKETPROBE/<datei>, der Probe-Server
      setzt seine eigene ein): jedes Paket aus pakete/ mit echtem digest, dazu ein Programm-Release (vA), ein Paket mit
      falscher Pruefsumme (probe_kaputt), ein Entwurf und ein Vorab-Release (beide darf die App nie zeigen).
  paket_release.py selbsttest [pakete]
      Kopf, Text und Tag je Paket, die Probe-Liste im Repo gleich dem Erzeuger, Gegenproben.

Welle A 1.5.0 (Weisung P12-358, Sammelliste 386): die drei Fachrichtungs-Pakete lernen_ae, lernen_dpa, lernen_dv haben
keinen Ordner unter pakete/: sie werden mit paket_bauen.py lernen aus inhalte_csharp/lernen/ geschnitten (eine Quelle).
Stand und braucht_app des Releases stehen an genau einer Stelle (paket_bauen.LERNEN_TESTDATEIEN je Fachrichtung, seit A 1.8.0;
AE Stand 2), und die gebaute Datei muss
Byte fuer Byte gleich der echten Testdatei tests/Lernplattform.Tests/Testdaten/pakete_echt/lernen_<fr>_<stand>.fipaket sein.
  paket_release.py lernen-kopf <lernen_fr>
      gibt KENNUNG=, STAND=, TAG=, DATEI=, NAME=, ART=lernen, FR= aus.
  paket_release.py lernen-bauen <lernen_fr> <zielordner>
      baut das Paket (paket_bauen.py lernen) und prueft es gegen die echte Testdatei; gibt den Pfad aus.
  paket_release.py lernen-text <lernen_fr> <paket.fipaket> <release.md>
      Release-Text wie bei den anderen Paketen (Art, Text, Beschreibung, Pruefsummen-Tabelle, Release-Seite).
"""
import hashlib
import importlib.util
import json
import os
import re
import sys
import tempfile
import zipfile

HIER = os.path.dirname(os.path.abspath(__file__))
WURZEL = os.path.dirname(os.path.dirname(HIER))
SEITE = "https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases/tag/"
PROBE_HOST = "http://PAKETPROBE"
PROBE_DATEI = os.path.join(WURZEL, "tests", "Lernplattform.Tests", "Testdaten", "pakete_echt", "github_releases.json")
TAG = re.compile(r"^vP([a-z][a-z0-9_]{1,31})\.([1-9][0-9]{0,8})\Z")

_spec = importlib.util.spec_from_file_location("paket_bauen", os.path.join(HIER, "paket_bauen.py"))
paket_bauen = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(paket_bauen)


def kopf(ordner):
    k, _ = paket_bauen.quelle_lesen(ordner)
    werte = {"KENNUNG": k["kennung"], "STAND": str(k["stand"]), "TAG": "vP%s.%d" % (k["kennung"], k["stand"]),
             "DATEI": "%s_%d.fipaket" % (k["kennung"], k["stand"]), "NAME": k["name"], "ART": k["art"]}
    if not TAG.match(werte["TAG"]):
        raise ValueError(werte["TAG"] + " ist kein Paket-Tag vP<kennung>.<stand>")
    return werte


def sha256(pfad):
    with open(pfad, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def text(ordner, paket):
    q, _ = paket_bauen.quelle_lesen(ordner)
    w = kopf(ordner)
    if os.path.basename(paket) != w["DATEI"]:
        raise ValueError("Paketdatei %s statt %s" % (os.path.basename(paket), w["DATEI"]))
    zeilen = ["Art: " + q["art"], q["text"] or q["name"], "",
              "Inhaltspaket „%s“ (Kennung %s, Stand %s). Es braucht die Lernplattform ab %s und wird in der App unter "
              "Optionen, Inhaltspakete geladen; ein Programm-Update nimmt es nie." % (q["name"], w["KENNUNG"], w["STAND"], q["braucht_app"]),
              "", "| Datei | Größe (Bytes) | SHA-256 |", "|---|---|---|",
              "| `%s` | %d | `%s` |" % (w["DATEI"], os.path.getsize(paket), sha256(paket)),
              "", "Release-Seite: " + SEITE + w["TAG"]]
    return "\n".join(zeilen) + "\n"


def lernen_kopf(kennung):
    fr = kennung[len("lernen_"):] if kennung.startswith("lernen_") else ""
    if fr not in paket_bauen.FACHRICHTUNGEN:
        raise ValueError(kennung + " ist kein Fachrichtungs-Paket (lernen_ae, lernen_dpa, lernen_dv)")
    stand, _ = paket_bauen.lernen_testdatei(fr)
    werte = {"KENNUNG": kennung, "STAND": str(stand), "TAG": "vP%s.%d" % (kennung, stand), "DATEI": "%s_%d.fipaket" % (kennung, stand),
             "NAME": "Fachrichtungs-Paket " + fr.upper(), "ART": "lernen", "FR": fr}
    if not TAG.match(werte["TAG"]):
        raise ValueError(werte["TAG"] + " ist kein Paket-Tag vP<kennung>.<stand>")
    return werte


def lernen_echt(kennung):
    w = lernen_kopf(kennung)
    return os.path.join(WURZEL, "tests", "Lernplattform.Tests", "Testdaten", "pakete_echt", w["DATEI"])


def lernen_bauen(kennung, ziel, repo=WURZEL):
    w = lernen_kopf(kennung)
    stand, braucht = paket_bauen.lernen_testdatei(w["FR"])
    pfad = paket_bauen.lernen_bauen(repo, w["FR"], ziel, stand, braucht)
    echt = lernen_echt(kennung)
    if not os.path.exists(echt):
        raise ValueError("echte Testdatei fehlt: " + os.path.relpath(echt, WURZEL))
    if sha256(pfad) != sha256(echt):
        raise ValueError(w["DATEI"] + ": Bau weicht von der echten Testdatei ab (paket_bauen.py lernen neu bauen)")
    return pfad


def lernen_text(kennung, paket):
    w = lernen_kopf(kennung)
    if os.path.basename(paket) != w["DATEI"]:
        raise ValueError("Paketdatei %s statt %s" % (os.path.basename(paket), w["DATEI"]))
    with zipfile.ZipFile(paket) as z:
        q = json.loads(z.read("paket.json"))
    if q["kennung"] != kennung or str(q["stand"]) != w["STAND"] or q["art"] != "lernen":
        raise ValueError(w["DATEI"] + ": Kopf passt nicht zu " + kennung)
    zeilen = ["Art: lernen", q["text"], "",
              "„%s“ (Kennung %s, Stand %s). Es braucht die Lernplattform ab %s und wird in der App auf der "
              "Startseite oder unter Optionen, Inhaltspakete geladen; ein Programm-Update nimmt es nie." % (q["name"], kennung, w["STAND"], q["braucht_app"]),
              "", "| Datei | Größe (Bytes) | SHA-256 |", "|---|---|---|",
              "| `%s` | %d | `%s` |" % (w["DATEI"], os.path.getsize(paket), sha256(paket)),
              "", "Release-Seite: " + SEITE + w["TAG"]]
    return "\n".join(zeilen) + "\n"


def lernen_selbsttest():
    funde = []
    with tempfile.TemporaryDirectory() as tmp:
        for fr in paket_bauen.FACHRICHTUNGEN:
            k = "lernen_" + fr
            try:
                pfad = lernen_bauen(k, tmp)
                t = lernen_text(k, pfad)
            except (ValueError, OSError, KeyError) as e:
                funde.append(str(e))
                continue
            stand, braucht = paket_bauen.lernen_testdatei(fr)
            if not t.startswith("Art: lernen\n") or braucht not in t or "\nRelease-Seite: " + SEITE + "vP%s.%d" % (k, stand) not in t:
                funde.append(k + ": Release-Text falsch")
        # Gegenproben: fremde Kennung, falsche Datei
        for falsch in ("lernen_si", "stadt1", "lernen_"):
            try:
                lernen_kopf(falsch)
                funde.append("Gegenprobe Kennung " + falsch + " nicht erkannt")
            except ValueError:
                pass
        try:
            lernen_text("lernen_ae", os.path.join(tmp, "lernen_dpa_1.fipaket"))
            funde.append("Gegenprobe andere Datei (lernen) nicht erkannt")
        except (ValueError, OSError):
            pass
    return funde


def _datei(name, pfad, digest=None):
    return {"name": name, "size": os.path.getsize(pfad), "digest": "sha256:" + (digest or sha256(pfad)),
            "browser_download_url": PROBE_HOST + "/" + os.path.basename(pfad)}


def github_probe(wurzel):
    """Release-Liste wie GitHub (neueste zuerst); Pakete gebaut wie paket_bauen.py ordner."""
    liste = [{"tag_name": "vA0.89.0", "name": "Fachinformatiker Lernplattform A 0.89.0", "draft": False, "prerelease": False,
              "body": "Programm-Release (die Paketliste nimmt es nie).", "assets": []}]
    with tempfile.TemporaryDirectory() as tmp:
        for k in sorted(os.listdir(wurzel)):
            ordner = os.path.join(wurzel, k)
            if not os.path.isdir(ordner):
                continue
            pfad = paket_bauen.ordner_bauen(ordner, tmp)
            w = kopf(ordner)
            liste.append({"tag_name": w["TAG"], "name": w["NAME"], "draft": False, "prerelease": False,
                          "body": text(ordner, pfad), "assets": [_datei(w["DATEI"], pfad)]})
            if k == "probe_quests":
                # dieselbe Datei unter anderer Kennung mit falscher Pruefsumme: die App muss sie ablehnen
                liste.append({"tag_name": "vPprobe_kaputt.1", "name": "Probe: falsche Prüfsumme", "draft": False, "prerelease": False,
                              "body": "Art: stadt\nProbe: die Prüfsumme passt nicht zur Datei.\n",
                              "assets": [_datei("probe_kaputt_1.fipaket", pfad, "0" * 64)]})
                for tag, art in (("vPprobe_entwurf.1", "draft"), ("vPprobe_vorab.1", "prerelease")):
                    eintrag = {"tag_name": tag, "name": "Probe: " + art, "draft": art == "draft", "prerelease": art == "prerelease",
                               "body": "Art: stadt\nDarf die App nie zeigen.\n",
                               "assets": [_datei(tag[2:].replace(".", "_") + ".fipaket", pfad)]}
                    liste.append(eintrag)
    return json.dumps(liste, ensure_ascii=False, indent=1) + "\n"


def selbsttest(wurzel):
    funde = []
    with tempfile.TemporaryDirectory() as tmp:
        for k in sorted(os.listdir(wurzel)):
            ordner = os.path.join(wurzel, k)
            if not os.path.isdir(ordner):
                continue
            try:
                w = kopf(ordner)
                pfad = paket_bauen.ordner_bauen(ordner, tmp)
                t = text(ordner, pfad)
            except (ValueError, OSError) as e:
                funde.append(str(e))
                continue
            if w["TAG"] != "vP%s.%s" % (k, w["STAND"]) or not TAG.match(w["TAG"]):
                funde.append(k + ": Tag " + w["TAG"])
            zeilen = t.split("\n")
            if zeilen[0] != "Art: " + w["ART"] or not zeilen[1].strip() or zeilen[1].startswith("Art: "):
                funde.append(k + ": Release-Text beginnt nicht mit Art und Text")
            if not re.search(r"^\| `%s` \| %d \| `%s` \|$" % (re.escape(w["DATEI"]), os.path.getsize(pfad), sha256(pfad)), t, re.M):
                funde.append(k + ": Pruefsummen-Tabelle fehlt oder falsch")
            if "\nRelease-Seite: " not in t:
                funde.append(k + ": Zeile Release-Seite fehlt (lauf_waehlen.py text setzt davor ein)")
            # Gegenprobe: eine andere Datei passt nicht zum Paket
            try:
                text(ordner, os.path.join(tmp, "andere_1.fipaket"))
                funde.append(k + ": Gegenprobe andere Datei nicht erkannt")
            except ValueError:
                pass
        probe = github_probe(wurzel)
        if not os.path.exists(PROBE_DATEI):
            funde.append("Probe-Liste fehlt: " + PROBE_DATEI)
        else:
            with open(PROBE_DATEI, encoding="utf-8") as f:
                if f.read() != probe:
                    funde.append("Probe-Liste weicht vom Erzeuger ab (paket_release.py github-probe pakete " + os.path.relpath(PROBE_DATEI, WURZEL) + ")")
        liste = json.loads(probe)
        sichtbar = sorted(r["tag_name"] for r in liste if not r["draft"] and not r["prerelease"] and TAG.match(r["tag_name"]))
        if "vPprobe_kaputt.1" not in sichtbar or "vPprobe_entwurf.1" in sichtbar or "vPprobe_vorab.1" in sichtbar:
            funde.append("Probe-Liste: falsche Auswahl sichtbarer Pakete " + str(sichtbar))
        # Gegenprobe Tag: Grossbuchstaben, Stand 0 und Programm-Tag sind kein Paket-Tag
        for falsch in ("vPProbe.1", "vPprobe.0", "vA0.89.0", "vS0.57.0", "vPprobe.1.2"):
            if TAG.match(falsch):
                funde.append("Gegenprobe Tag " + falsch + " nicht erkannt")
    funde.extend(lernen_selbsttest())
    for f in funde:
        print("FEHLER:", f)
    print("paket_release selbsttest:", "gruen" if not funde else "rot")
    return 1 if funde else 0


def main():
    a = sys.argv[1:]
    try:
        if len(a) == 2 and a[0] == "kopf":
            for schluessel, wert in kopf(a[1]).items():
                print("%s=%s" % (schluessel, wert))
            return 0
        if len(a) == 4 and a[0] == "text":
            with open(a[3], "w", encoding="utf-8", newline="\n") as f:
                f.write(text(a[1], a[2]))
            return 0
        if len(a) == 3 and a[0] == "github-probe":
            with open(a[2], "w", encoding="utf-8", newline="\n") as f:
                f.write(github_probe(a[1]))
            return 0
        if len(a) == 2 and a[0] == "lernen-kopf":
            for schluessel, wert in lernen_kopf(a[1]).items():
                print("%s=%s" % (schluessel, wert))
            return 0
        if len(a) == 3 and a[0] == "lernen-bauen":
            print(lernen_bauen(a[1], a[2]))
            return 0
        if len(a) == 4 and a[0] == "lernen-text":
            with open(a[3], "w", encoding="utf-8", newline="\n") as f:
                f.write(lernen_text(a[1], a[2]))
            return 0
        if len(a) in (1, 2) and a[0] == "selbsttest":
            return selbsttest(a[1] if len(a) == 2 else os.path.join(WURZEL, "pakete"))
    except (ValueError, OSError) as e:
        print("FEHLER: " + str(e))
        return 1
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main())
