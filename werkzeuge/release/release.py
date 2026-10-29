"""Phase 6 W1 (Weisung P6-2): Release zusammenstellen und pruefen (python3.12 -I).

  release.py version                     Version, Stufe, Tag (T1), Paketversion aus Directory.Build.props als
                                         NAME=Wert-Zeilen; Rueckgabe 1, wenn der Tag nach der Regel nicht geht.
  release.py selbsttest                  Tag-Regel gegen werkzeuge/release/tag_faelle.json (dieselben Faelle
                                         wie der C#-Test ReleaseTests); prueft, dass probe_release.md zum
                                         heutigen Release-Text passt und das Update-Fenster daraus nur
                                         "Neu in dieser Version" zeigt.
  release.py probe                       schreibt werkzeuge/release/probe_release.md neu (Release-Text mit zwei
                                         festen Probedateien; der C#-Test ReleaseTests liest ihn mit UpdateNotizen).
  release.py notizen <datei>             Weisung P8-53: schreibt die Notizen fuer vpk pack --releaseNotes (Abschnitt
                                         aus AENDERUNGEN.md wie im Release-Text) und druckt NOTIZEN_SHA256 (UTF-8
                                         des Textes im Update-Fenster, so schreibt ihn die Update-Probe).
  release.py pruefe-feed <json> [<paket>]  Rueckgabe 1, wenn ein Paket (Vorgabe: Paketversion im Repo) in
                                         releases.<os>.json keine Notizen hat oder sie nicht den Abschnitt zeigen.
  release.py zusammen <eingang> <aus>    sammelt die Dateien der vier Bauteile (Unterordner von <eingang>),
                                         schreibt Lizenz-Uebersicht und Volltexte, prueft die Uebersicht im
                                         Repo, schreibt den Release-Text <aus>/../release.md und prueft alles (T2).
  release.py pruefe <aus> <release.md>   T2: Pflichtdateien, Namen, Velopack-Feeds, Pruefsummen im Text.
  release.py digest <aus> <assets.json>  nach dem Hochladen: Name, Groesse und digest (sha256) jeder Datei der
                                         GitHub-Antwort gleich den Dateien in <aus>, keine fehlt, keine zu viel.
  release.py digest-text <assets.json> <release.md>  vor dem Veroeffentlichen: Dateien des Entwurfs gleich
                                         der Pruefsummen-Tabelle in seinem Text.
Nichts hier laedt hoch, legt einen Tag an oder loescht etwas.
"""
import hashlib
import importlib.util
import json
import os
import re
import shutil
import sys
import xml.etree.ElementTree as ET

WURZEL = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ID = "Fachinformatiker-Lernplattform"
ZAHL = r"(?:0|[1-9][0-9]*)"
VERSION = re.compile(r"^" + ZAHL + r"\." + ZAHL + r"\." + ZAHL + r"\Z")
STUFENTEXT = {"A": " (Alphaversion „A“)", "B": " (Betaversion „B“)"}
SEITE = "https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases"
# Weisung P6-10: das Update-Fenster (Core/Update/UpdateNotizen.cs, wie Python plain_notes) zeigt den
# Release-Text bis zu dieser Zeile. Davor steht darum nur der Abschnitt aus AENDERUNGEN.md.
ENDE_NOTIZEN = "### Herunterladen"
PROBE = os.path.join(WURZEL, "werkzeuge", "release", "probe_release.md")
PROBE_DATEIEN = {"Probe-1.txt": b"Probe 1\n", "Probe-2.txt": b"Probe 2\n"}


def fehler(text):
    print("FEHLER: " + text)


def tag_fuer(stufe, version):
    """Gleiche Regel wie UpdateVersion.ReleaseTag (C#)."""
    if not isinstance(version, str) or not VERSION.match(version):
        return None
    if any(int(teil) > 2147483647 for teil in version.split(".")):
        return None  # wie C# int.TryParse
    if stufe in ("A", "B"):
        return "v" + stufe + version
    if stufe in ("", None) and int(version.split(".")[0]) >= 1:
        return "v" + version
    return None


def props():
    wurzel = ET.parse(os.path.join(WURZEL, "Directory.Build.props")).getroot()
    werte = {}
    for name in ("Version", "Stufe"):
        treffer = [e.text or "" for e in wurzel.iter(name)]
        if len(treffer) != 1:
            raise SystemExit("FEHLER: " + name + " nicht genau einmal in Directory.Build.props")
        werte[name] = treffer[0].strip()
    return werte["Version"], werte["Stufe"]


def kennzahlen():
    version, stufe = props()
    tag = tag_fuer(stufe, version)
    paket = version if stufe == "" else "1.0.0-" + stufe.lower() + "." + version
    anzeige = (stufe + " " + version).strip()
    name = (stufe + "-" + version) if stufe else version
    return {"VERSION": version, "STUFE": stufe, "TAG": tag, "PAKET": paket, "ANZEIGE": anzeige, "NAME": name}


def pflichtdateien(k):
    """Release-Dateien je Bauteil (Plan Phase 6: VE5 Linux mit tar.gz, D2 ohne Portable-ZIP, D7 macOS ohne Feed)."""
    return {
        "win-x64": [ID + "-win-Setup.exe", ID + "-" + k["PAKET"] + "-full.nupkg", "RELEASES", "releases.win.json"],
        "linux-x64": [ID + ".AppImage", ID + "-" + k["PAKET"] + "-linux-full.nupkg", "RELEASES-linux",
                      "releases.linux.json", ID + "-" + k["NAME"] + "-AppImage.tar.gz"],
        "osx-arm64": [ID + "-" + k["NAME"] + "-osx-arm64.pkg"],
        "android": [ID + "-" + k["NAME"] + "-android-arm64.apk"],
        "gemeinsam": ["LICENSE.txt", "THIRD_PARTY_NOTICES.txt", "THIRD_PARTY_LICENSES.txt"],
    }


def sha256(pfad):
    h = hashlib.sha256()
    with open(pfad, "rb") as d:
        for stueck in iter(lambda: d.read(1 << 20), b""):
            h.update(stueck)
    return h.hexdigest()


def lizenzmodul():
    pfad = os.path.join(WURZEL, "werkzeuge", "lizenzen", "erzeuge_lizenzen.py")
    spec = importlib.util.spec_from_file_location("erzeuge_lizenzen", pfad)
    modul = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(modul)
    return modul


def neuerungen(anzeige):
    """Abschnitt "## <Anzeige> ..." aus AENDERUNGEN.md ohne die Ueberschrift."""
    with open(os.path.join(WURZEL, "AENDERUNGEN.md"), encoding="utf-8") as d:
        zeilen = d.read().splitlines()
    start = [i for i, z in enumerate(zeilen) if z.startswith("## " + anzeige + " ") or z == "## " + anzeige]
    if len(start) != 1:
        raise SystemExit("FEHLER: Abschnitt '## " + anzeige + "' nicht genau einmal in AENDERUNGEN.md")
    ende = next((i for i in range(start[0] + 1, len(zeilen)) if zeilen[i].startswith("## ")), len(zeilen))
    return "\n".join(zeilen[start[0] + 1:ende]).strip()


def lesbar(markdown):
    """Wie UpdateNotizen.Lesbar (C#) und plain_notes (fisi_update.py): Text fuer das Update-Fenster."""
    punkte = []
    for zeile in (markdown or "").replace("\r\n", "\n").split("\n"):
        ohne = zeile.strip()
        if ohne == ENDE_NOTIZEN:
            break
        if not ohne:
            punkte.append("")
        elif ohne.startswith("- ") or ohne.startswith("* "):
            punkte.append("• " + ohne[2:])
        elif punkte and punkte[-1]:
            punkte[-1] += " " + ohne
        else:
            punkte.append(ohne)
    text = "\n".join(p for p in punkte if p)
    for zeichen in ("**", "__", "`"):
        text = text.replace(zeichen, "")
    return text


def pruefe_notizen(k, text):
    """Rueckgabe 1, wenn das Update-Fenster aus dem Release-Text mehr oder anderes zeigte als den Abschnitt."""
    neu = neuerungen(k["ANZEIGE"])
    rot = 0
    if not neu or not text.startswith(neu + "\n\n" + ENDE_NOTIZEN + "\n"):
        fehler("Release-Text beginnt nicht mit dem Abschnitt '## %s' aus AENDERUNGEN.md und '%s'" % (k["ANZEIGE"], ENDE_NOTIZEN))
        rot = 1
    if text.count(ENDE_NOTIZEN + "\n") != 1 or lesbar(text) != lesbar(neu) or not lesbar(neu):
        fehler("Update-Fenster zeigte aus dem Release-Text nicht genau 'Neu in dieser Version'")
        rot = 1
    return rot


def release_text(k, aus):
    dateien = sorted(os.listdir(aus))
    z = [neuerungen(k["ANZEIGE"]), "", ENDE_NOTIZEN, "",
         "**Fachinformatiker Lernplattform " + k["ANZEIGE"] + "**: privates Lernprogramm für die Ausbildung Fachinformatiker/in"
         + STUFENTEXT.get(k["STUFE"], "") + ". Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).", "",
         "## Welche Datei für welches Gerät?", "",
         "- **Windows:** `" + ID + "-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital "
         "signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann "
         "„Trotzdem ausführen“ klicken.",
         "- **Linux:** `" + ID + "-" + k["NAME"] + "-AppImage.tar.gz` herunterladen und entpacken, dann `" + ID +
         ".AppImage` doppelklicken. Die einzelne Datei `" + ID + ".AppImage` braucht das Update im Programm; lädst "
         "du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.",
         "- **Android** (64-Bit, arm64-v8a): `" + ID + "-" + k["NAME"] + "-android-arm64.apk` am Handy herunterladen "
         "und öffnen. Android fragt, ob der Browser Apps installieren darf.",
         "- **macOS** (nur Apple-Chips): `" + ID + "-" + k["NAME"] + "-osx-arm64.pkg`. Nur gebaut, nicht auf einem "
         "Mac geprüft, nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & "
         "Sicherheit“ „Dennoch öffnen“ wählen. Das Update im Programm gibt es auf macOS noch nicht.",
         "- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. "
         "Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` "
         "(volle Texte).", "",
         "## Prüfsummen (SHA-256)", "",
         "Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile "
         "<Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).", "",
         "| Datei | Größe (Bytes) | SHA-256 |", "|---|---:|---|"]
    for name in dateien:
        pfad = os.path.join(aus, name)
        z.append("| `%s` | %d | `%s` |" % (name, os.path.getsize(pfad), sha256(pfad)))
    z += ["", "Release-Seite: " + SEITE]
    return "\n".join(z) + "\n"


def pruefe(aus, text_pfad):
    k = kennzahlen()
    rot = 0
    if k["TAG"] is None:
        fehler("Version %s mit Stufe '%s' ergibt keinen gueltigen Tag (Sammelliste 73)" % (k["VERSION"], k["STUFE"]))
        rot = 1
    soll = sorted(n for liste in pflichtdateien(k).values() for n in liste)
    ist = sorted(os.listdir(aus))
    for name in sorted(set(soll) - set(ist)):
        fehler("Pflichtdatei fehlt: " + name)
        rot = 1
    for name in sorted(set(ist) - set(soll)):
        fehler("Datei gehoert nicht ins Release: " + name)
        rot = 1
    for kanal in ("win", "linux"):
        feed = os.path.join(aus, "releases." + kanal + ".json")
        if not os.path.isfile(feed):
            continue
        with open(feed, encoding="utf-8-sig") as d:
            eintraege = json.load(d)["Assets"]
        if len(eintraege) != 1:
            fehler("releases.%s.json: erwartet genau ein Paket (erstes Release ohne Delta), gefunden %d" % (kanal, len(eintraege)))
            rot = 1
        for e in eintraege:
            pfad = os.path.join(aus, e["FileName"])
            if e["Version"] != k["PAKET"] or e["PackageId"] != ID:
                fehler("releases.%s.json: %s %s statt %s %s" % (kanal, e["PackageId"], e["Version"], ID, k["PAKET"]))
                rot = 1
            if not os.path.isfile(pfad):
                fehler("releases.%s.json nennt fehlende Datei %s" % (kanal, e["FileName"]))
                rot = 1
            elif os.path.getsize(pfad) != e["Size"] or sha256(pfad) != e["SHA256"].lower():
                fehler("releases.%s.json: Groesse oder SHA-256 von %s stimmt nicht" % (kanal, e["FileName"]))
                rot = 1
    with open(text_pfad, encoding="utf-8") as d:
        text = d.read()
    zeilen = re.findall(r"^\| `([^`]+)` \| (\d+) \| `([0-9a-f]{64})` \|$", text, re.M)
    if sorted(n for n, _, _ in zeilen) != ist:
        fehler("Pruefsummen-Tabelle im Release-Text nennt nicht genau die Dateien des Releases")
        rot = 1
    for name, groesse, summe in zeilen:
        pfad = os.path.join(aus, name)
        if os.path.isfile(pfad) and (int(groesse) != os.path.getsize(pfad) or summe != sha256(pfad)):
            fehler("Pruefsumme oder Groesse im Release-Text falsch: " + name)
            rot = 1
    if ("\n**Fachinformatiker Lernplattform " + k["ANZEIGE"] + "**: ") not in text:
        fehler("Kopfzeile des Release-Texts passt nicht zur Version")
        rot = 1
    rot |= pruefe_notizen(k, text)
    print("T2: %d Dateien geprueft, %s" % (len(ist), "rot" if rot else "gruen"))
    return rot


def zusammen(eingang, aus):
    k = kennzahlen()
    if k["TAG"] is None:
        fehler("kein gueltiger Tag fuer %s/%s" % (k["STUFE"], k["VERSION"]))
        return 1
    os.makedirs(aus, exist_ok=False)
    sammlungen = []
    for teil, namen in pflichtdateien(k).items():
        if teil == "gemeinsam":
            continue
        quelle = os.path.join(eingang, "release-" + teil)
        for name in namen:
            if os.path.isfile(os.path.join(quelle, name)):
                shutil.copy2(os.path.join(quelle, name), os.path.join(aus, name))
        json_pfad = os.path.join(quelle, "lizenzen-" + teil + ".json")
        if os.path.isfile(json_pfad):
            sammlungen.append(json_pfad)
        else:
            fehler("Lizenz-Sammlung fehlt: " + json_pfad)
    shutil.copy2(os.path.join(WURZEL, "LICENSE.txt"), os.path.join(aus, "LICENSE.txt"))
    lizenzen = lizenzmodul()
    alle, wo = lizenzen.vereinigen(sammlungen)
    with open(os.path.join(aus, "THIRD_PARTY_NOTICES.txt"), "w", encoding="utf-8", newline="\n") as d:
        d.write(lizenzen.uebersicht(alle, wo))
    with open(os.path.join(aus, "THIRD_PARTY_LICENSES.txt"), "w", encoding="utf-8", newline="\n") as d:
        d.write(lizenzen.volltexte(alle))
    rot = 1 if len(sammlungen) != 4 else 0
    rot |= lizenzen.main_pruefen(os.path.join(WURZEL, "THIRD_PARTY_NOTICES.txt"), sammlungen,
                                 os.path.join(WURZEL, "THIRD_PARTY_LICENSES.txt"))
    text_pfad = os.path.join(os.path.dirname(os.path.abspath(aus)), "release.md")
    with open(text_pfad, "w", encoding="utf-8", newline="\n") as d:
        d.write(release_text(k, aus))
    rot |= pruefe(aus, text_pfad)
    return rot


def vergleiche_assets(assets_pfad, soll, wo):
    """GitHub-Assets (Name, Groesse, digest "sha256:...") gegen soll = {Name: (Groesse, sha256)}."""
    with open(assets_pfad, encoding="utf-8") as d:
        assets = json.load(d)
    rot = 0
    online = {a["name"]: a for a in assets}
    if sorted(online) != sorted(soll):
        fehler("Dateien im Release weichen ab (Vergleich mit: %s): fehlen %s, zu viel %s" % (
            wo, sorted(set(soll) - set(online)), sorted(set(online) - set(soll))))
        rot = 1
    for name, (groesse, summe) in sorted(soll.items()):
        a = online.get(name)
        if a is not None and (a.get("size") != groesse or a.get("digest") != "sha256:" + summe):
            fehler("hochgeladene Datei weicht ab (Vergleich mit: %s): %s (digest %s)" % (wo, name, a.get("digest")))
            rot = 1
    print("Digest (Vergleich mit: %s): %d Dateien, %s" % (wo, len(soll), "rot" if rot else "gruen"))
    return rot


def digest(aus, assets_pfad):
    soll = {n: (os.path.getsize(os.path.join(aus, n)), sha256(os.path.join(aus, n))) for n in os.listdir(aus)}
    return vergleiche_assets(assets_pfad, soll, "gebaute Dateien")


def digest_text(assets_pfad, text_pfad):
    """Vor dem Veroeffentlichen: die Dateien im Entwurf muessen genau der Pruefsummen-Tabelle im Text entsprechen."""
    with open(text_pfad, encoding="utf-8") as d:
        zeilen = re.findall(r"^\| `([^`]+)` \| (\d+) \| `([0-9a-f]{64})` \|$", d.read(), re.M)
    if not zeilen:
        fehler("keine Pruefsummen-Tabelle im Release-Text")
        return 1
    return vergleiche_assets(assets_pfad, {n: (int(g), h) for n, g, h in zeilen}, "Tabelle im Release-Text")


def selbsttest():
    with open(os.path.join(WURZEL, "werkzeuge", "release", "tag_faelle.json"), encoding="utf-8") as d:
        faelle = json.load(d)["faelle"]
    rot = 0
    for f in faelle:
        if tag_fuer(f["stufe"], f["version"]) != f["tag"]:
            fehler("Tag-Regel: %r %r ergibt %r statt %r" % (f["stufe"], f["version"], tag_fuer(f["stufe"], f["version"]), f["tag"]))
            rot = 1
    k = kennzahlen()
    if k["TAG"] is None:
        fehler("Version im Repo ergibt keinen Tag")
        rot = 1
    print("Selbsttest Tag-Regel: %d Faelle, Repo %s -> %s, %s" % (len(faelle), k["ANZEIGE"], k["TAG"], "rot" if rot else "gruen"))
    probe = probe_text(k)
    rot_probe = pruefe_notizen(k, probe)
    with open(PROBE, encoding="utf-8") as d:
        if d.read() != probe:
            fehler("werkzeuge/release/probe_release.md passt nicht zum heutigen Release-Text "
                   "(python3 -I werkzeuge/release/release.py probe)")
            rot_probe = 1
    print("Selbsttest Update-Fenster: %s" % ("rot" if rot_probe else "gruen"))
    return rot | rot_probe


def notizen(datei):
    """Weisung P8-53: Notizen fuer vpk pack --releaseNotes, derselbe Schnitt wie der Release-Text (Abschnitt
    aus AENDERUNGEN.md bis vor ENDE_NOTIZEN). Druckt die Pruefsumme des Textes im Update-Fenster."""
    text = neuerungen(kennzahlen()["ANZEIGE"])
    if not lesbar(text):
        fehler("Abschnitt in AENDERUNGEN.md ergibt keinen Text fuer das Update-Fenster")
        return 1
    with open(datei, "w", encoding="utf-8", newline="\n") as d:
        d.write(text + "\n")
    print("NOTIZEN_SHA256=" + hashlib.sha256(lesbar(text).encode("utf-8")).hexdigest())
    return 0


def pruefe_feed(json_pfad, paket=None):
    """Weisung P8-53: Rueckgabe 1, wenn ein Paket im Feed (releases.<os>.json) keine Notizen hat oder das
    Update-Fenster daraus nicht genau den Abschnitt 'Neu in dieser Version' zeigte."""
    k = kennzahlen()
    paket = paket or k["PAKET"]
    soll = lesbar(neuerungen(k["ANZEIGE"]))
    with open(json_pfad, encoding="utf-8-sig") as d:
        pakete = [a for a in json.load(d).get("Assets", []) if a.get("Version") == paket]
    if not pakete:
        fehler("kein Paket %s in %s" % (paket, json_pfad))
        return 1
    rot = 0
    for a in pakete:
        text = a.get("NotesMarkdown") or ""
        if not text.strip():
            fehler("Paket %s in %s ohne Notizen (vpk pack ohne --releaseNotes?)" % (a.get("FileName"), json_pfad))
            rot = 1
        elif not soll or lesbar(text) != soll:
            fehler("Notizen von %s zeigen im Update-Fenster nicht den Abschnitt '## %s'" % (a.get("FileName"), k["ANZEIGE"]))
            rot = 1
    print("Notizen im Feed %s: %d Paket(e) %s, %s" % (os.path.basename(json_pfad), len(pakete), paket, "rot" if rot else "gruen"))
    return rot


def probe_text(k):
    """Release-Text mit zwei festen Probedateien (gleich bei jedem Lauf)."""
    import tempfile
    with tempfile.TemporaryDirectory() as ordner:
        for name, inhalt in PROBE_DATEIEN.items():
            with open(os.path.join(ordner, name), "wb") as d:
                d.write(inhalt)
        return release_text(k, ordner)


def main():
    befehl = sys.argv[1]
    if befehl == "version":
        k = kennzahlen()
        for name, wert in k.items():
            print("%s=%s" % (name, wert if wert is not None else ""))
        if k["TAG"] is None:
            fehler("kein gueltiger Tag (Sammelliste 73)")
            return 1
        return 0
    if befehl == "selbsttest":
        return selbsttest()
    if befehl == "probe":
        with open(PROBE, "w", encoding="utf-8", newline="\n") as d:
            d.write(probe_text(kennzahlen()))
        return 0
    if befehl == "notizen":
        return notizen(sys.argv[2])
    if befehl == "pruefe-feed":
        return pruefe_feed(sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else None)
    if befehl == "zusammen":
        return zusammen(sys.argv[2], sys.argv[3])
    if befehl == "pruefe":
        return pruefe(sys.argv[2], sys.argv[3])
    if befehl == "digest":
        return digest(sys.argv[2], sys.argv[3])
    if befehl == "digest-text":
        return digest_text(sys.argv[2], sys.argv[3])
    raise SystemExit("unbekannter Aufruf")


if __name__ == "__main__":
    sys.exit(main())
