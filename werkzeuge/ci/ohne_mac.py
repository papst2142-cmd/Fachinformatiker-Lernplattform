#!/usr/bin/env python3
"""Waechter der CI-Stufe "ohne-mac" (Welle CI schneller 2, Meldung P12-84 Punkt 4, Nico Frage 2 = a, Weisung P12-131).

Die Stufe ohne-mac ist der volle Lauf ohne die sieben Mac-Jobs (5 x tests auf macos-15/macos-15-intel, pakete
(osx-arm64), pakete (osx-x64)): 10 Jobs. Sie gilt nur, wenn der Kopf gegen origin/main KEINE Datei aendert, die die
Mac-Teile noetig macht. Vor jedem Release der Lernplattform verlangt release.yml weiter den vollen Lauf 17/17.

Mac noetig (Liste aus Meldung P12-84 Punkt 4, dazu strenger, selbst gefunden: Ton, Diagnose, Projektdateien,
alle Workflows, Lizenzliste, Unit-Tests zu Mac, Haenger, Update, Paketen, Release, Vollbild, Ton):
  src/Lernplattform.App/**, src/Lernplattform.Desktop/**, src/Lernplattform.Core/Spiel/Probe/**,
  src/Lernplattform.Engine/Spiel/Probe/** (eigene Engine seit Weisung P12-179, Zeichnen der Stadt),
  src/Lernplattform.Core/Spiel/Ton/**, src/Lernplattform.Core/Update/**, src/Lernplattform.Core/Diagnose/**,
  src/Lernplattform.*/*.csproj und *.props (Projektaufbau, auch Grundlagen.props), werkzeuge/starttest/**, werkzeuge/pakete/**, werkzeuge/release/**,
  werkzeuge/lizenzen/**, werkzeuge/ci/**, .github/**, Directory.*, global.json, nuget.config,
  tests/Lernplattform.Tests/** mit Mac, Haenger, Update, Paket, Release, Vollbild oder Ton im Dateinamen.
Jede solche Datei macht die Stufe sofort rot mit der Zeile "Mac-Teile noetig: <Datei>".

Aufruf:
  ohne_mac.py pruefen [basis]   Basis: Vorgabe origin/main; steht der Kopf genau auf origin/main (Lauf auf main),
                                gilt der Commit davor (main entsteht nur aus Squash-Merges).
  ohne_mac.py selbsttest        feste Faelle mit Gegenprobe und ein Lauf in einem frischen Git-Archiv.
Rueckgabe 0 = ohne Mac erlaubt, 1 = Mac-Teile noetig oder Fehler.
"""
import os
import re
import subprocess
import sys
import tempfile

MAC_NOETIG = (
    re.compile(r"^src/Lernplattform\.App/"),
    re.compile(r"^src/Lernplattform\.Desktop/"),
    re.compile(r"^src/Lernplattform\.Core/Spiel/Probe/"),
    re.compile(r"^src/Lernplattform\.Engine/Spiel/Probe/"),
    re.compile(r"^src/Lernplattform\.Core/Spiel/Ton/"),
    re.compile(r"^src/Lernplattform\.Core/Update/"),
    re.compile(r"^src/Lernplattform\.Core/Diagnose/"),
    re.compile(r"^src/Lernplattform\.[^/]+/[^/]+\.(csproj|props)$"),
    re.compile(r"^werkzeuge/(starttest|pakete|release|lizenzen|ci)/"),
    re.compile(r"^\.github/"),
    re.compile(r"^Directory\.[^/]+$"),
    re.compile(r"^global\.json$"),
    re.compile(r"^nuget\.config$", re.I),
    re.compile(r"^tests/Lernplattform\.Tests/(.*/)?[^/]*(Mac|Haenger|Update|Paket|Release|Vollbild|Ton)[^/]*$"),
)


def funde(dateien):
    """dateien = geaenderte Pfade (Repo-relativ, mit /). Gibt die Dateien zurueck, die die Mac-Teile noetig machen."""
    return [d for d in sorted(set(dateien)) if any(m.search(d) for m in MAC_NOETIG)]


def git(*argumente, ordner=None):
    return subprocess.run(["git", *argumente], cwd=ordner, check=True, capture_output=True, text=True).stdout


def basis_bestimmen(gewuenscht, ordner=None):
    if gewuenscht:
        return gewuenscht
    kopf = git("rev-parse", "HEAD", ordner=ordner).strip()
    main = git("rev-parse", "origin/main", ordner=ordner).strip()
    return "HEAD^1" if kopf == main else "origin/main"


def pruefen(basis=None, ordner=None):
    try:
        b = basis_bestimmen(basis, ordner)
        roh = git("diff", "--name-status", "--no-renames", b, "HEAD", ordner=ordner)
    except subprocess.CalledProcessError as e:
        print("FEHLER: Waechter ohne_mac: git %s (Rueckgabe %d) %s" % (" ".join(e.cmd[1:]), e.returncode, (e.stderr or "").strip()))
        return 1
    dateien = [z.split("\t", 1)[1] for z in roh.splitlines() if "\t" in z]
    rot = funde(dateien)
    for d in rot:
        print("Mac-Teile noetig: " + d)
    if rot:
        print("FEHLER: Stufe ohne-mac nicht erlaubt, %d von %d Dateien brauchen die Mac-Teile (Basis %s); voller Lauf noetig"
              % (len(rot), len(dateien), b))
        return 1
    print("Waechter ohne_mac: %d geaenderte Dateien gegen %s, keine braucht die Mac-Teile" % (len(dateien), b))
    return 0


FAELLE = [
    # (Name, Dateien, erwartet rot)
    ("Lerninhalte und Kern ohne Mac", ["inhalte_csharp/lernen/quizfragen.json", "src/Lernplattform.Core/Lernen/Karte.cs",
                                        "src/Lernplattform.Engine/Stadtbau/StadtbauPruefung.cs",
                                        "tests/Lernplattform.Tests/LerninhalteTests.cs", "tests/Vergleich/lerninhalte_pruefung.json",
                                        "AENDERUNGEN.md", "CLAUDE.md"], False),
    ("nur Werkzeug", ["src/Stadtbau.App/Oberflaeche/Hauptansicht.cs", "tests/Stadtbau.Tests/X.cs",
                      "werkzeuge/stadtbau/baue_stadtbau.sh", "inhalte_stadtbau/grafik/katalog.json"], False),
    ("Spiel-Kern ohne Darstellung", ["src/Lernplattform.Core/Spiel/Betrieb.Farm.cs",
                                     "tests/Lernplattform.Tests/SpielTests.cs"], False),
    ("Gegenprobe App", ["src/Lernplattform.App/Views/Dialog.cs"], True),
    ("Gegenprobe Desktop", ["src/Lernplattform.Desktop/Program.cs"], True),
    ("Gegenprobe Spiel/Probe", ["src/Lernplattform.Core/Spiel/Probe/StadtSpiel.cs"], True),
    ("Gegenprobe Engine Spiel/Probe", ["src/Lernplattform.Engine/Spiel/Probe/StadtSzene.cs"], True),
    ("Gegenprobe Projektdatei Engine", ["src/Lernplattform.Engine/Lernplattform.Engine.csproj"], True),
    ("Gegenprobe Projektdatei Grundlagen", ["src/Lernplattform.Grundlagen/Lernplattform.Grundlagen.csproj"], True),
    ("Gegenprobe Liste der Grundlagen", ["src/Lernplattform.Core/Grundlagen.props"], True),
    ("Gegenprobe Ton", ["src/Lernplattform.Core/Spiel/Ton/VollbildBildschirm.cs"], True),
    ("Gegenprobe Update", ["src/Lernplattform.Core/Update/MacBundle.cs"], True),
    ("Gegenprobe Diagnose", ["src/Lernplattform.Core/Diagnose/Haenger.cs"], True),
    ("Gegenprobe Projektdatei Kern", ["src/Lernplattform.Core/Lernplattform.Core.csproj"], True),
    ("Gegenprobe Start-Test", ["werkzeuge/starttest/lauf.sh"], True),
    ("Gegenprobe Pakete", ["werkzeuge/pakete/pkg_nur_benutzer.sh"], True),
    ("Gegenprobe Release-Werkzeug", ["werkzeuge/release/release.py"], True),
    ("Gegenprobe Lizenzliste", ["werkzeuge/lizenzen/bestandteile.json"], True),
    ("Gegenprobe CI-Werkzeug", ["werkzeuge/ci/ohne_mac.py"], True),
    ("Gegenprobe ci.yml", [".github/workflows/ci.yml"], True),
    ("Gegenprobe release.yml", [".github/workflows/release.yml"], True),
    ("Gegenprobe Paketversionen", ["Directory.Packages.props"], True),
    ("Gegenprobe Version", ["Directory.Build.props"], True),
    ("Gegenprobe Targets", ["Directory.Build.targets"], True),
    ("Gegenprobe global.json", ["global.json"], True),
    ("Gegenprobe NuGet.Config", ["NuGet.Config"], True),
    ("Gegenprobe Test Mac", ["tests/Lernplattform.Tests/MacBundleTests.cs"], True),
    ("Gegenprobe Test Haenger", ["tests/Lernplattform.Tests/HaengerTests.cs"], True),
    ("Gegenprobe Test Update", ["tests/Lernplattform.Tests/UpdateStoerungTests.cs"], True),
    ("Gegenprobe Test Release", ["tests/Lernplattform.Tests/ReleaseTests.cs"], True),
    ("Gegenprobe Test Ton", ["tests/Lernplattform.Tests/SpielTonTests.cs"], True),
    ("Gegenprobe Test Vollbild", ["tests/Lernplattform.Tests/VollbildBildschirmTests.cs"], True),
    ("Gegenprobe gemischt", ["inhalte/quizfragen.json", "src/Lernplattform.App/Views/Dialog.cs"], True),
]


def selbsttest():
    rot = 0
    for name, dateien, soll_rot in FAELLE:
        ist_rot = bool(funde(dateien))
        if ist_rot != soll_rot:
            print("FEHLER: Selbsttest ohne_mac Fall '%s': %s statt %s" % (name, "rot" if ist_rot else "gruen", "rot" if soll_rot else "gruen"))
            rot = 1
    with tempfile.TemporaryDirectory() as ordner:
        def schreiben(pfad, text):
            ziel = os.path.join(ordner, pfad)
            os.makedirs(os.path.dirname(ziel), exist_ok=True)
            with open(ziel, "w", encoding="utf-8") as f:
                f.write(text)

        def commit(nachricht):
            git("add", "-A", ordner=ordner)
            git("-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", nachricht, ordner=ordner)

        try:
            git("init", "-q", "-b", "main", ordner=ordner)
            schreiben("inhalte/a.json", "1\n")
            schreiben("src/Lernplattform.App/B.cs", "b\n")
            commit("main")
            git("update-ref", "refs/remotes/origin/main", "HEAD", ordner=ordner)
            git("checkout", "-q", "-b", "zweig", ordner=ordner)
            schreiben("inhalte/a.json", "2\n")
            commit("nur Inhalte")
            stumm = open(os.devnull, "w")
            alt, sys.stdout = sys.stdout, stumm
            try:
                gruen = pruefen(ordner=ordner)
                schreiben("src/Lernplattform.App/B.cs", "b2\n")
                commit("App")
                gegen = pruefen(ordner=ordner)
                git("update-ref", "refs/remotes/origin/main", "HEAD", ordner=ordner)
                main_rot = pruefen(ordner=ordner)
            finally:
                sys.stdout = alt
                stumm.close()
        except (subprocess.CalledProcessError, OSError) as e:
            print("FEHLER: Selbsttest ohne_mac: Git-Archiv nicht anlegbar: %s" % e)
            return 1
        if gruen != 0:
            print("FEHLER: Selbsttest ohne_mac: Zweig nur mit Inhalten nicht gruen")
            rot = 1
        if gegen != 1:
            print("FEHLER: Selbsttest ohne_mac: Gegenprobe mit Datei der App nicht rot")
            rot = 1
        if main_rot != 1:
            print("FEHLER: Selbsttest ohne_mac: Lauf auf main prueft den letzten Commit nicht (Gegenprobe nicht rot)")
            rot = 1
    print("Selbsttest ohne_mac: %d feste Faelle und Git-Probe (gruen, Gegenprobe rot, main rot), %s"
          % (len(FAELLE), "rot" if rot else "gruen"))
    return rot


def main():
    sys.stdout.reconfigure(newline="\n")
    if len(sys.argv) in (2, 3) and sys.argv[1] == "pruefen":
        return pruefen(sys.argv[2] if len(sys.argv) == 3 else None)
    if len(sys.argv) == 2 and sys.argv[1] == "selbsttest":
        return selbsttest()
    print("Aufruf: ohne_mac.py pruefen [basis] | selbsttest")
    return 1


if __name__ == "__main__":
    sys.exit(main())
