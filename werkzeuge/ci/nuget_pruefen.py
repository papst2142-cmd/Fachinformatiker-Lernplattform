#!/usr/bin/env python3
"""NuGet-Zwischenspeicher der CI pruefen (Welle CI schneller 2, Weisung P12-131, Nico Frage 3 = b).

Der Zwischenspeicher (actions/cache, Ordner ~/.nuget/packages) darf nie einen veralteten oder kaputten Stand gruen machen.
Darum zwei Regeln:
1. Schluessel (Pruefung von ci.yml): jeder NuGet-Zwischenspeicher hat einen Schluessel mit hashFiles ueber ALLE Dateien,
   die die Wiederherstellung bestimmen (global.json, Directory.Packages.props, Directory.Build.props,
   Directory.Build.targets, alle *.csproj, Lockdateien packages.lock.json, nuget.config), und KEINE restore-keys (nur
   genauer Treffer; aendert sich eine Paketversion, ist der Schluessel neu und NuGet laedt alles frisch).
   Lockdateien gibt es im Repo heute keine (zentrale Paketversionen in Directory.Packages.props); sobald eine dazukommt,
   zaehlt sie im Schluessel mit.
2. Inhalt (nach dem Bauen, nur bei einem Treffer): fuer jedes Paket in jeder obj/project.assets.json liegt im
   Zwischenspeicher genau diese Version, ihre .nupkg-Datei hat die Pruefsumme aus <name>.<version>.nupkg.sha512, und der
   Inhalts-Hash in .nupkg.metadata ist der aus project.assets.json. Sonst rot ("FEHLER: NuGet-Zwischenspeicher ...").

Aufruf:
  nuget_pruefen.py inhalt [ordner] [paketordner]   Vorgabe: ., ~/.nuget/packages
  nuget_pruefen.py schluessel [ci.yml]
  nuget_pruefen.py selbsttest                      feste Faelle mit Gegenproben (Rueckgabe 0 = alle richtig)
"""
import base64
import hashlib
import json
import os
import re
import sys
import tempfile

NOETIG = ("global.json", "Directory.Packages.props", "Directory.Build.props", "Directory.Build.targets",
          "**/*.csproj", "**/packages.lock.json", "**/nuget.config")


def sha512(pfad):
    h = hashlib.sha512()
    with open(pfad, "rb") as f:
        for teil in iter(lambda: f.read(1 << 20), b""):
            h.update(teil)
    return base64.b64encode(h.digest()).decode()


def assets_dateien(ordner):
    for wurzel, verzeichnisse, dateien in os.walk(ordner):
        verzeichnisse[:] = [v for v in verzeichnisse if v not in (".git", "node_modules", "python-linie")]
        if "project.assets.json" in dateien and os.path.basename(wurzel) == "obj":
            yield os.path.join(wurzel, "project.assets.json")


def inhalt(ordner=".", pakete=None):
    pakete = pakete or os.path.join(os.path.expanduser("~"), ".nuget", "packages")
    funde, geprueft, gesehen = [], 0, {}
    dateien = sorted(assets_dateien(ordner))
    for a in dateien:
        with open(a, encoding="utf-8-sig") as f:
            daten = json.load(f)
        for name, lib in daten.get("libraries", {}).items():
            if lib.get("type") != "package":
                continue
            if name in gesehen:
                if gesehen[name] != lib.get("sha512"):
                    funde.append("%s: zwei Inhalts-Hashes in verschiedenen Projekten" % name)
                continue
            gesehen[name] = lib.get("sha512")
            pfad = os.path.join(pakete, lib.get("path", ""))
            paket, version = name.split("/", 1)
            datei = os.path.join(pfad, "%s.%s.nupkg" % (paket.lower(), version.lower()))
            meta = os.path.join(pfad, ".nupkg.metadata")
            if not (os.path.isfile(datei) and os.path.isfile(datei + ".sha512") and os.path.isfile(meta)):
                funde.append("%s: fehlt im Zwischenspeicher (%s)" % (name, lib.get("path")))
                continue
            with open(datei + ".sha512", encoding="utf-8") as f:
                soll = f.read().strip()
            if sha512(datei) != soll:
                funde.append("%s: .nupkg passt nicht zur Pruefsumme" % name)
            with open(meta, encoding="utf-8") as f:
                if json.load(f).get("contentHash") != lib.get("sha512"):
                    funde.append("%s: Inhalts-Hash weicht von project.assets.json ab" % name)
            geprueft += 1
    for z in funde:
        print("FEHLER: NuGet-Zwischenspeicher " + z)
    if not dateien:
        print("FEHLER: NuGet-Zwischenspeicher: keine obj/project.assets.json unter " + os.path.abspath(ordner))
        return 1
    print("NuGet-Zwischenspeicher: %d Pakete aus %d Projekten geprueft, %s" % (geprueft, len(dateien), "rot" if funde else "gruen"))
    return 1 if funde else 0


def cache_schritte(text):
    """Bloecke 'uses: actions/cache@...' mit path ~/.nuget/packages: Liste (Schluessel, ganzer Block)."""
    bloecke = re.findall(r"^( *)- uses: actions/cache@[^\n]*\n((?:\1  [^\n]*\n)+)", text, re.M)
    return [(re.search(r"key: (.*)", b).group(1) if re.search(r"key: (.*)", b) else "", b) for _, b in bloecke
            if "~/.nuget/packages" in b]


def schluessel(ci=".github/workflows/ci.yml", still=False):
    with open(ci, encoding="utf-8") as f:
        schritte = cache_schritte(f.read())
    funde = []
    if not schritte:
        funde.append("kein NuGet-Zwischenspeicher in " + ci)
    for key, block in schritte:
        fehlend = [n for n in NOETIG if "'%s'" % n not in key]
        if "hashFiles(" not in key or fehlend:
            funde.append("Schluessel ohne %s: %s" % (", ".join(fehlend) or "hashFiles", key))
        if "restore-keys" in block:
            funde.append("restore-keys erlaubt einen ungenauen Treffer: " + key)
        if "runner.os" not in key or "runner.arch" not in key:
            funde.append("Schluessel ohne runner.os/runner.arch: " + key)
    if not still:
        for z in funde:
            print("FEHLER: NuGet-Schluessel " + z)
        if not funde:
            print("NuGet-Schluessel: %d Zwischenspeicher in %s, jeder mit allen Dateien und ohne restore-keys"
                  % (len(schritte), os.path.basename(ci)))
    return 1 if funde else 0


def selbsttest():
    rot = 0
    stumm = open(os.devnull, "w")
    with tempfile.TemporaryDirectory() as t:
        pakete, repo = os.path.join(t, "pakete"), os.path.join(t, "repo")

        def paket(name, version, inhalt_bytes=b"inhalt", content="H1"):
            ordner = os.path.join(pakete, name.lower(), version)
            os.makedirs(ordner, exist_ok=True)
            datei = os.path.join(ordner, "%s.%s.nupkg" % (name.lower(), version))
            with open(datei, "wb") as f:
                f.write(inhalt_bytes)
            with open(datei + ".sha512", "w") as f:
                f.write(base64.b64encode(hashlib.sha512(inhalt_bytes).digest()).decode())
            with open(os.path.join(ordner, ".nupkg.metadata"), "w") as f:
                json.dump({"version": 2, "contentHash": content}, f)
            return datei

        def assets(libs):
            os.makedirs(os.path.join(repo, "src", "A", "obj"), exist_ok=True)
            with open(os.path.join(repo, "src", "A", "obj", "project.assets.json"), "w") as f:
                json.dump({"libraries": {"%s/%s" % (n, v): {"type": "package", "sha512": h, "path": "%s/%s" % (n.lower(), v)}
                                         for n, v, h in libs}}, f)

        def lauf(*a):
            alt, sys.stdout = sys.stdout, stumm
            try:
                return inhalt(*a)
            finally:
                sys.stdout = alt

        datei = paket("Avalonia", "12.1.4")
        paket("Xunit", "3.0.0", b"x", "H2")
        assets([("Avalonia", "12.1.4", "H1"), ("Xunit", "3.0.0", "H2")])
        faelle = [("gruen", lambda: None, 0)]

        def kaputt():
            with open(datei, "ab") as f:
                f.write(b"!")

        def andere_version():
            assets([("Avalonia", "12.1.5", "H1"), ("Xunit", "3.0.0", "H2")])

        def anderer_hash():
            assets([("Avalonia", "12.1.4", "H9"), ("Xunit", "3.0.0", "H2")])
        for name, aenderung, soll in [("gruen", lambda: None, 0), ("Gegenprobe veraltete Version (nicht im Speicher)", andere_version, 1),
                                      ("Gegenprobe anderer Inhalts-Hash", anderer_hash, 1), ("Gegenprobe kaputte .nupkg", kaputt, 1)]:
            if name.startswith("Gegenprobe kaputte"):
                assets([("Avalonia", "12.1.4", "H1"), ("Xunit", "3.0.0", "H2")])
            aenderung()
            ist = lauf(repo, pakete)
            if ist != soll:
                print("FEHLER: Selbsttest NuGet Inhalt '%s': Rueckgabe %d statt %d" % (name, ist, soll))
                rot = 1
        if lauf(os.path.join(t, "leer"), pakete) != 1:
            print("FEHLER: Selbsttest NuGet Inhalt: Ordner ohne project.assets.json nicht rot")
            rot = 1
        # Schluessel: echte ci.yml gruen; Gegenproben ohne Directory.Packages.props, mit restore-keys, ohne runner.arch
        ci = ".github/workflows/ci.yml"
        if os.path.exists(ci):
            with open(ci, encoding="utf-8") as f:
                text = f.read()
            if schluessel(ci, still=True) != 0:
                schluessel(ci)
                print("FEHLER: Selbsttest NuGet-Schluessel: ci.yml nicht gruen")
                rot = 1
            gegen = [("ohne Directory.Packages.props", text.replace("'Directory.Packages.props', ", "", 1)),
                     ("mit restore-keys", re.sub(r"(\n( *)key: nuget-[^\n]*\n)", r"\1\2restore-keys: nuget-\n", text, count=1)),
                     ("ohne runner.arch", text.replace("-${{ runner.arch }}", "", 1)),
                     ("ohne Zwischenspeicher", text.replace("~/.nuget/packages", "~/anders"))]
            for name, geaendert in gegen:
                pfad = os.path.join(t, "ci.yml")
                with open(pfad, "w", encoding="utf-8") as f:
                    f.write(geaendert)
                if geaendert == text or schluessel(pfad, still=True) != 1:
                    print("FEHLER: Selbsttest NuGet-Schluessel Gegenprobe '%s' nicht rot" % name)
                    rot = 1
    stumm.close()
    print("Selbsttest NuGet: Inhalt (gruen, 3 Gegenproben, leerer Ordner) und Schluessel (ci.yml, 4 Gegenproben), %s"
          % ("rot" if rot else "gruen"))
    return rot


def main():
    sys.stdout.reconfigure(newline="\n")
    if len(sys.argv) >= 2 and sys.argv[1] == "inhalt" and len(sys.argv) <= 4:
        return inhalt(*sys.argv[2:])
    if len(sys.argv) in (2, 3) and sys.argv[1] == "schluessel":
        return schluessel(*sys.argv[2:])
    if len(sys.argv) == 2 and sys.argv[1] == "selbsttest":
        return selbsttest()
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main())
