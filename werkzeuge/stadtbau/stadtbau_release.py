"""Phase 13 Welle 2 (Weisung P12-17): Release des Stadtbau-Werkzeugs zusammenstellen und pruefen (python3.12 -I).

Eigene Linie S (Tag vS<Version>, Version <StadtbauVersion> in Directory.Build.props, Abschnitt "## S <Version>" in
AENDERUNGEN_STADTBAU.md). Ein Werkzeug-Release wird nie "latest" (stadtbau_release.yml); die Update-Suche der
Lernplattform nimmt nur vA/vB/v1 (Core/Update/ReleaseLinie.cs), das Werkzeug nur vS.

  stadtbau_release.py version                    VERSION, TAG, ANZEIGE als NAME=Wert-Zeilen
  stadtbau_release.py zusammen <eingang> <aus>   Dateien aus <eingang>/stadtbau-win-x64 und <eingang>/stadtbau-android,
                                                 LICENSE.txt, Lizenz-Uebersicht und Volltexte beider Teile; schreibt den
                                                 Release-Text <aus>/../release.md und prueft alles
  stadtbau_release.py pruefe <aus> <release.md>  Pflichtdateien, Feed (Kanal stadtbau-win), Pruefsummen im Text
  stadtbau_release.py selbsttest                 Tag-Regel und Gegenproben (kein Tag der Lernplattform, kein Kanal win)
Fuer digest und digest-text nach dem Hochladen gilt werkzeuge/release/release.py (gleiche Regeln).
Nichts hier laedt hoch, legt einen Tag an oder loescht etwas.
"""
import importlib.util
import json
import os
import re
import shutil
import sys
import tempfile

WURZEL = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ID = "Fachinformatiker-Stadtbau"
KANAL = "stadtbau-win"
ZAHL = r"(?:0|[1-9][0-9]*)"
VERSION = re.compile(r"^" + ZAHL + r"\." + ZAHL + r"\." + ZAHL + r"\Z")
SEITE = "https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases"
ENDE_NOTIZEN = "### Herunterladen"
# Windows 3 (Setup, nupkg, Feed), Android 1, gemeinsam 3
DATEIEN_JE_RELEASE = 7


def fehler(text):
    print("FEHLER: " + text)


def modul(name, pfad):
    spec = importlib.util.spec_from_file_location(name, pfad)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def lizenzmodul():
    # Titel der Uebersicht und Namen der Programmdateien des Werkzeugs (erzeuge_lizenzen.py liest das beim Laden)
    os.environ["LIZENZ_PROGRAMM"] = "Stadtbau"
    return modul("erzeuge_lizenzen", os.path.join(WURZEL, "werkzeuge", "lizenzen", "erzeuge_lizenzen.py"))


def lernplattform_release():
    return modul("release", os.path.join(WURZEL, "werkzeuge", "release", "release.py"))


def tag_fuer(version):
    if not isinstance(version, str) or not VERSION.match(version):
        return None
    if any(int(teil) > 2147483647 for teil in version.split(".")):
        return None
    return "vS" + version


def kennzahlen():
    with open(os.path.join(WURZEL, "Directory.Build.props"), encoding="utf-8") as d:
        treffer = re.findall(r"<StadtbauVersion>([^<]*)</StadtbauVersion>", d.read())
    if len(treffer) != 1:
        raise SystemExit("FEHLER: StadtbauVersion nicht genau einmal in Directory.Build.props")
    version = treffer[0].strip()
    return {"VERSION": version, "TAG": tag_fuer(version), "ANZEIGE": "S " + version}


def pflichtdateien(k):
    return {
        "win-x64": [ID + "-win-Setup.exe", ID + "-" + k["VERSION"] + "-" + KANAL + "-full.nupkg", "releases." + KANAL + ".json"],
        "android": [ID + "-S-" + k["VERSION"] + "-android-arm64.apk"],
        "gemeinsam": ["LICENSE.txt", "THIRD_PARTY_NOTICES.txt", "THIRD_PARTY_LICENSES.txt"],
    }


def neuerungen(anzeige):
    with open(os.path.join(WURZEL, "AENDERUNGEN_STADTBAU.md"), encoding="utf-8") as d:
        zeilen = d.read().splitlines()
    start = [i for i, z in enumerate(zeilen) if z == "## " + anzeige]
    if len(start) != 1:
        raise SystemExit("FEHLER: Abschnitt '## " + anzeige + "' nicht genau einmal in AENDERUNGEN_STADTBAU.md")
    ende = next((i for i in range(start[0] + 1, len(zeilen)) if zeilen[i].startswith("## ")), len(zeilen))
    return "\n".join(zeilen[start[0] + 1:ende]).strip()


def release_text(k, aus):
    rel = lernplattform_release()
    z = [neuerungen(k["ANZEIGE"]), "", ENDE_NOTIZEN, "",
         "**Fachinformatiker Stadtbau " + k["ANZEIGE"] + "**: Werkzeug zum Bauen der Häuser und Innenräume der Spielstadt "
         "der Fachinformatiker Lernplattform, nur für Windows-PC und Android-Tablet. Eigenes Programm neben der "
         "Lernplattform; die Lernplattform selbst bekommt dieses Release nie als Update. Copyright Nico H, alle Rechte "
         "vorbehalten (siehe `LICENSE.txt`).", "",
         "## Welche Datei für welches Gerät?", "",
         "- **Windows-PC:** `" + ID + "-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital "
         "signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann "
         "„Trotzdem ausführen“ klicken.",
         "- **Android-Tablet** (64-Bit, arm64-v8a): `" + ID + "-S-" + k["VERSION"] + "-android-arm64.apk` am Tablet "
         "herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.",
         "- Die übrigen Dateien (`.nupkg`, `releases." + KANAL + ".json`) braucht das Update im Werkzeug. Lizenzen der "
         "Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).", "",
         "## Prüfsummen (SHA-256)", "",
         "Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile "
         "<Datei> SHA256`).", "",
         "| Datei | Größe (Bytes) | SHA-256 |", "|---|---:|---|"]
    for name in sorted(os.listdir(aus)):
        pfad = os.path.join(aus, name)
        z.append("| `%s` | %d | `%s` |" % (name, os.path.getsize(pfad), rel.sha256(pfad)))
    z += ["", "Release-Seite: " + SEITE]
    return "\n".join(z) + "\n"


def pruefe(aus, text_pfad):
    k = kennzahlen()
    rel = lernplattform_release()
    rot = 0
    if k["TAG"] is None:
        fehler("StadtbauVersion %s ergibt keinen gueltigen Tag" % k["VERSION"])
        rot = 1
    soll = sorted(n for liste in pflichtdateien(k).values() for n in liste)
    ist = sorted(os.listdir(aus))
    if len(soll) != DATEIEN_JE_RELEASE:
        fehler("Pflichtliste hat %d statt %d Dateien" % (len(soll), DATEIEN_JE_RELEASE))
        rot = 1
    for name in sorted(set(soll) - set(ist)):
        fehler("Pflichtdatei fehlt: " + name)
        rot = 1
    for name in sorted(set(ist) - set(soll)):
        fehler("Datei gehoert nicht ins Werkzeug-Release: " + name)
        rot = 1
    # nie ein Feed oder Paket der Lernplattform (sonst faende deren Update das Werkzeug)
    for name in ist:
        if name.startswith("Fachinformatiker-Lernplattform") or re.fullmatch(r"releases\.(win|linux|osx|osx-x64)\.json|RELEASES.*", name):
            fehler("Datei der Lernplattform im Werkzeug-Release: " + name)
            rot = 1
    feed = os.path.join(aus, "releases." + KANAL + ".json")
    if os.path.isfile(feed):
        with open(feed, encoding="utf-8-sig") as d:
            eintraege = json.load(d)["Assets"]
        if len(eintraege) != 1:
            fehler("releases.%s.json: erwartet genau ein Paket, gefunden %d" % (KANAL, len(eintraege)))
            rot = 1
        for e in eintraege:
            pfad = os.path.join(aus, e["FileName"])
            if e["Version"] != k["VERSION"] or e["PackageId"] != ID:
                fehler("releases.%s.json: %s %s statt %s %s" % (KANAL, e["PackageId"], e["Version"], ID, k["VERSION"]))
                rot = 1
            if not os.path.isfile(pfad):
                fehler("releases.%s.json nennt fehlende Datei %s" % (KANAL, e["FileName"]))
                rot = 1
            elif os.path.getsize(pfad) != e["Size"] or rel.sha256(pfad) != e["SHA256"].lower():
                fehler("releases.%s.json: Groesse oder SHA-256 von %s stimmt nicht" % (KANAL, e["FileName"]))
                rot = 1
    with open(text_pfad, encoding="utf-8") as d:
        text = d.read()
    zeilen = re.findall(r"^\| `([^`]+)` \| (\d+) \| `([0-9a-f]{64})` \|$", text, re.M)
    if sorted(n for n, _, _ in zeilen) != ist:
        fehler("Pruefsummen-Tabelle im Release-Text nennt nicht genau die Dateien des Releases")
        rot = 1
    for name, groesse, summe in zeilen:
        pfad = os.path.join(aus, name)
        if os.path.isfile(pfad) and (int(groesse) != os.path.getsize(pfad) or summe != rel.sha256(pfad)):
            fehler("Pruefsumme oder Groesse im Release-Text falsch: " + name)
            rot = 1
    if ("\n**Fachinformatiker Stadtbau " + k["ANZEIGE"] + "**: ") not in text:
        fehler("Kopfzeile des Release-Texts passt nicht zur Version")
        rot = 1
    neu = neuerungen(k["ANZEIGE"])
    if not neu or not text.startswith(neu + "\n\n" + ENDE_NOTIZEN + "\n") or rel.lesbar(text) != rel.lesbar(neu):
        fehler("Release-Text beginnt nicht genau mit dem Abschnitt '## %s' aus AENDERUNGEN_STADTBAU.md" % k["ANZEIGE"])
        rot = 1
    print("Werkzeug-Release: %d Dateien geprueft, %s" % (len(ist), "rot" if rot else "gruen"))
    return rot


def zusammen(eingang, aus):
    k = kennzahlen()
    if k["TAG"] is None:
        fehler("kein gueltiger Tag fuer S %s" % k["VERSION"])
        return 1
    os.makedirs(aus, exist_ok=False)
    sammlungen = []
    rot = 0
    for teil, namen in pflichtdateien(k).items():
        if teil == "gemeinsam":
            continue
        quelle = os.path.join(eingang, "stadtbau-" + teil)
        for name in namen:
            if os.path.isfile(os.path.join(quelle, name)):
                shutil.copy2(os.path.join(quelle, name), os.path.join(aus, name))
        json_pfad = os.path.join(quelle, "lizenzen-stadtbau-" + teil + ".json")
        if os.path.isfile(json_pfad):
            sammlungen.append(json_pfad)
        else:
            fehler("Lizenz-Sammlung fehlt: " + json_pfad)
            rot = 1
    shutil.copy2(os.path.join(WURZEL, "LICENSE.txt"), os.path.join(aus, "LICENSE.txt"))
    lizenzen = lizenzmodul()
    alle, wo = lizenzen.vereinigen(sammlungen)
    with open(os.path.join(aus, "THIRD_PARTY_NOTICES.txt"), "w", encoding="utf-8", newline="\n") as d:
        d.write(lizenzen.uebersicht(alle, wo))
    with open(os.path.join(aus, "THIRD_PARTY_LICENSES.txt"), "w", encoding="utf-8", newline="\n") as d:
        d.write(lizenzen.volltexte(alle))
    text_pfad = os.path.join(os.path.dirname(os.path.abspath(aus)), "release.md")
    with open(text_pfad, "w", encoding="utf-8", newline="\n") as d:
        d.write(release_text(k, aus))
    rot |= pruefe(aus, text_pfad)
    return rot


def selbsttest():
    rot = 0
    faelle = [("0.1.0", "vS0.1.0"), ("1.2.3", "vS1.2.3"), ("01.0.0", None), ("0.1", None), ("0.1.0 ", None),
              ("2147483648.0.0", None)]
    for version, soll in faelle:
        if tag_fuer(version) != soll:
            fehler("Selbsttest Tag: %r ergibt %r statt %r" % (version, tag_fuer(version), soll))
            rot = 1
    # Gegenprobe: der Tag des Werkzeugs ist nie ein Tag der Lernplattform (deren Regel kennt nur vA/vB/v1)
    rel = lernplattform_release()
    for stufe in ("A", "B", ""):
        t = rel.tag_fuer(stufe, "1.0.0")
        if t is None or t.startswith("vS"):
            fehler("Selbsttest Gegenprobe: Lernplattform-Tag %r" % t)
            rot = 1
    if rel.tag_fuer("S", "0.1.0") is not None:
        fehler("Selbsttest Gegenprobe: die Tag-Regel der Lernplattform nimmt die Stufe S an")
        rot = 1
    # Gegenprobe der Pruefung: ein Feed der Lernplattform im Werkzeug-Release ist rot
    k = kennzahlen()
    with tempfile.TemporaryDirectory() as ordner:
        aus = os.path.join(ordner, "aus")
        os.makedirs(aus)
        for liste in pflichtdateien(k).values():
            for name in liste:
                with open(os.path.join(aus, name), "wb") as d:
                    d.write(b"probe\n")
        with open(os.path.join(aus, "releases.win.json"), "w", encoding="utf-8") as d:
            d.write('{"Assets": []}')
        os.remove(os.path.join(aus, "releases." + KANAL + ".json"))
        text = os.path.join(ordner, "release.md")
        with open(text, "w", encoding="utf-8") as d:
            d.write(release_text(k, aus))
        import contextlib
        import io
        with contextlib.redirect_stdout(io.StringIO()) as puffer:
            ergebnis = pruefe(aus, text)
        if ergebnis != 1 or "releases.win.json" not in puffer.getvalue():
            fehler("Selbsttest Gegenprobe: Feed der Lernplattform im Werkzeug-Release nicht rot")
            rot = 1
    print("Selbsttest Werkzeug-Release: %s" % ("rot" if rot else "gruen"))
    return rot


def main():
    befehl = sys.argv[1]
    if befehl == "version":
        k = kennzahlen()
        for name in ("VERSION", "TAG", "ANZEIGE"):
            print("%s=%s" % (name, k[name] or ""))
        return 0 if k["TAG"] else 1
    if befehl == "zusammen":
        return zusammen(sys.argv[2], sys.argv[3])
    if befehl == "pruefe":
        return pruefe(sys.argv[2], sys.argv[3])
    if befehl == "selbsttest":
        return selbsttest()
    raise SystemExit("FEHLER: unbekannter Befehl " + befehl)


if __name__ == "__main__":
    sys.exit(main())
