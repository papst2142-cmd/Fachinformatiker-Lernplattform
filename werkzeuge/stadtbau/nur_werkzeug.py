#!/usr/bin/env python3
"""Waechter der CI-Stufe "werkzeug" (Weisung P12-20, Nico 06.11.2026 08:13 MEZ).

Die Stufe werkzeug ist ein kleiner Lauf ohne Mac-Jobs und ohne Pakete der Lernplattform. Sie gilt nur, wenn ein Stand
ausschliesslich das Stadtbau-Werkzeug aendert. Dieser Waechter vergleicht den Kopf mit origin/main und laesst nur zu:
  src/Stadtbau.*/**, tests/Stadtbau.Tests/**, werkzeuge/stadtbau/**, inhalte_stadtbau/** (Katalog, Weisung P12-25
  Frage 1 = a; nur genau dieser Ordner, inhalte/ und inhalte_csharp/ bleiben rot), AENDERUNGEN_STADTBAU.md,
  in Directory.Build.props nur Zeilen mit <StadtbauVersion>, und .md-Dateien.
Jede andere Datei (besonders src/Lernplattform.* mit der eigenen Engine src/Lernplattform.Engine (Spiel/Probe, Stadtbau)
und den Grundlagen src/Lernplattform.Grundlagen, Weisung P12-179, inhalte*/, tests/Lernplattform.Tests,
Directory.Packages.props, die Workflows) macht die Stufe sofort rot mit der Zeile "voller Lauf noetig: <Datei>".
Strenger als die Liste der Weisung (selbst gefunden): AENDERUNGEN.md ist der Release-Text der Lernplattform (ReleaseTests,
release.py) und braucht den vollen Lauf; .md-Dateien unter verbotenen Ordnern (inhalte*/, src/Lernplattform.*, ...) auch.

Aufruf:
  nur_werkzeug.py pruefen [basis]   Basis: Vorgabe origin/main; steht der Kopf genau auf origin/main (Lauf auf main),
                                    gilt der Commit davor (main entsteht nur aus Squash-Merges, ein Commit = ein PR).
  nur_werkzeug.py selbsttest        feste Faelle mit Gegenprobe (Datei der Lernplattform rot, nur Werkzeug gruen) und ein
                                    Lauf in einem frischen Git-Archiv.
Rueckgabe 0 = nur Werkzeug, 1 = voller Lauf noetig oder Fehler.
"""
import os
import re
import subprocess
import sys
import tempfile

# zuerst verboten (gilt auch fuer .md-Dateien darin), dann erlaubt
VERBOTEN = (
    re.compile(r"^src/Lernplattform\.[^/]+/"),
    re.compile(r"^inhalte(?!_stadtbau/)[^/]*/"),
    re.compile(r"^tests/(?!Stadtbau\.Tests/)"),
    re.compile(r"^\.github/"),
    re.compile(r"^AENDERUNGEN\.md$"),
)
ERLAUBT = (
    re.compile(r"^src/Stadtbau\.[^/]+/"),
    re.compile(r"^tests/Stadtbau\.Tests/"),
    re.compile(r"^werkzeuge/stadtbau/"),
    re.compile(r"^inhalte_stadtbau/"),
    re.compile(r"^AENDERUNGEN_STADTBAU\.md$"),
    re.compile(r"\.md$"),
)
PROPS = "Directory.Build.props"


def props_nur_stadtbauversion(zeilen):
    """zeilen = geaenderte Zeilen (+/-) der Datei Directory.Build.props; erlaubt nur die Zeile StadtbauVersion."""
    return len(zeilen) > 0 and all(re.fullmatch(r"\s*<StadtbauVersion>[0-9]+\.[0-9]+\.[0-9]+</StadtbauVersion>\s*", z)
                                   for z in zeilen)


def funde(dateien, props_zeilen):
    """dateien = geaenderte Pfade (Repo-relativ, mit /); props_zeilen = geaenderte Zeilen von Directory.Build.props.
    Gibt die Dateien zurueck, die einen vollen Lauf brauchen (sortiert)."""
    rot = []
    for d in sorted(set(dateien)):
        if d == PROPS:
            if not props_nur_stadtbauversion(props_zeilen):
                rot.append(d)
            continue
        if any(m.search(d) for m in VERBOTEN) or not any(m.search(d) for m in ERLAUBT):
            rot.append(d)
    return rot


def git(*argumente, ordner=None):
    return subprocess.run(["git", *argumente], cwd=ordner, check=True, capture_output=True, text=True).stdout


def geaendert(basis, ordner=None):
    """Pfade und geaenderte Zeilen von Directory.Build.props zwischen basis und HEAD (Baum gegen Baum, auch
    umbenannte Dateien zaehlen mit altem und neuem Namen)."""
    roh = git("diff", "--name-status", "--no-renames", basis, "HEAD", ordner=ordner)
    dateien = [z.split("\t", 1)[1] for z in roh.splitlines() if "\t" in z]
    props = []
    if PROPS in dateien:
        diff = git("diff", "--unified=0", basis, "HEAD", "--", PROPS, ordner=ordner)
        props = [z[1:] for z in diff.splitlines()
                 if z[:1] in "+-" and not z.startswith("+++") and not z.startswith("---")]
    return dateien, props


def basis_bestimmen(gewuenscht, ordner=None):
    if gewuenscht:
        return gewuenscht
    kopf = git("rev-parse", "HEAD", ordner=ordner).strip()
    main = git("rev-parse", "origin/main", ordner=ordner).strip()
    return "HEAD^1" if kopf == main else "origin/main"


def pruefen(basis=None, ordner=None):
    try:
        b = basis_bestimmen(basis, ordner)
        dateien, props = geaendert(b, ordner)
    except subprocess.CalledProcessError as e:
        print("FEHLER: Waechter nur_werkzeug: git %s (Rueckgabe %d) %s" % (" ".join(e.cmd[1:]), e.returncode, (e.stderr or "").strip()))
        return 1
    rot = funde(dateien, props)
    for d in rot:
        print("voller Lauf noetig: " + d)
    if rot:
        print("FEHLER: Stufe werkzeug nicht erlaubt, %d von %d Dateien gehoeren nicht nur zum Werkzeug (Basis %s)" % (len(rot), len(dateien), b))
        return 1
    print("Waechter nur_werkzeug: %d geaenderte Dateien gegen %s, alle nur Werkzeug" % (len(dateien), b))
    return 0


FAELLE = [
    # (Name, Dateien, Zeilen Directory.Build.props, erwartet rot)
    ("nur Werkzeug", ["src/Stadtbau.App/Oberflaeche/Hauptansicht.cs", "src/Stadtbau.Core/Texte.cs",
                      "tests/Stadtbau.Tests/PruefungTests.cs", "werkzeuge/stadtbau/baue_stadtbau.sh",
                      "AENDERUNGEN_STADTBAU.md", "CLAUDE.md", "START_Chat.md"], [], False),
    ("StadtbauVersion", [PROPS, "AENDERUNGEN_STADTBAU.md"],
     ["    <StadtbauVersion>0.2.0</StadtbauVersion>", "    <StadtbauVersion>0.3.0</StadtbauVersion>"], False),
    ("Symbol des Werkzeugs (Weisung P12-51)", ["src/Stadtbau.App/Assets/icon.png", "src/Stadtbau.App/Assets/icon.ico",
                                               "src/Stadtbau.Android/Icon.png", "src/Stadtbau.Desktop/Stadtbau.Desktop.csproj",
                                               "werkzeuge/stadtbau/erzeuge_symbol_ico.py"], [], False),
    ("Gegenprobe Symbol der Lernplattform", ["src/Lernplattform.App/Assets/icon.png"], [], True),
    ("Gegenprobe Lernplattform-Kern", ["src/Stadtbau.App/A.cs", "src/Lernplattform.Core/Stadtbau/StadtbauPruefung.cs"], [], True),
    ("Gegenprobe Spiel/Probe", ["src/Lernplattform.Core/Spiel/Probe/StadtSzene.cs"], [], True),
    ("Gegenprobe Engine Stadtbau (Weisung P12-179)", ["src/Stadtbau.App/A.cs", "src/Lernplattform.Engine/Stadtbau/StadtbauPruefung.cs"], [], True),
    ("Gegenprobe Engine Spiel/Probe", ["src/Lernplattform.Engine/Spiel/Probe/StadtSzene.cs"], [], True),
    ("Gegenprobe Grundlagen", ["src/Lernplattform.Grundlagen/Lernplattform.Grundlagen.csproj"], [], True),
    ("Gegenprobe App", ["src/Lernplattform.App/Views/Dialog.cs"], [], True),
    ("Gegenprobe Inhalte", ["inhalte/quizfragen.json"], [], True),
    ("Gegenprobe Inhalte C#", ["inhalte_csharp/spiel/stadt.json"], [], True),
    ("Katalog inhalte_stadtbau", ["inhalte_stadtbau/grafik/x.bin", "inhalte_stadtbau/grafik/katalog.json"], [], False),
    ("Gegenprobe Inhalte C# Spiel", ["inhalte_csharp/spiel/x.json"], [], True),
    ("Gegenprobe Inhalte x.json", ["inhalte/x.json"], [], True),
    ("Gegenprobe aehnlicher Ordner", ["inhalte_stadtbauX/a"], [], True),
    ("Gegenprobe inhalte_stadtbau ohne Schraegstrich", ["inhalte_stadtbau"], [], True),
    ("Gegenprobe Inhalte .md", ["inhalte_csharp/LIESMICH.md"], [], True),
    ("Gegenprobe Tests Lernplattform", ["tests/Lernplattform.Tests/SpielTests.cs"], [], True),
    ("Gegenprobe Sollwerte", ["tests/Vergleich/SOLLWERTE.sha256"], [], True),
    ("Gegenprobe Pakete", ["Directory.Packages.props"], [], True),
    ("Gegenprobe ci.yml", [".github/workflows/ci.yml"], [], True),
    ("Gegenprobe Release-Text Lernplattform", ["AENDERUNGEN.md"], [], True),
    ("Gegenprobe Lizenzliste", ["werkzeuge/lizenzen/bestandteile.json"], [], True),
    ("Gegenprobe Version Lernplattform", [PROPS],
     ["    <Version>0.42.2</Version>", "    <Version>0.42.3</Version>"], True),
    ("Gegenprobe Version beide", [PROPS],
     ["    <StadtbauVersion>0.2.0</StadtbauVersion>", "    <StadtbauVersion>0.3.0</StadtbauVersion>",
      "    <Version>0.42.2</Version>", "    <Version>0.42.3</Version>"], True),
    ("Gegenprobe Props andere Zeile", [PROPS], ["    <IstStadtbau>false</IstStadtbau>"], True),
]


def selbsttest():
    rot = 0
    for name, dateien, props, soll_rot in FAELLE:
        ist_rot = bool(funde(dateien, props))
        if ist_rot != soll_rot:
            print("FEHLER: Selbsttest nur_werkzeug Fall '%s': %s statt %s" % (name, "rot" if ist_rot else "gruen", "rot" if soll_rot else "gruen"))
            rot = 1
    # echter Lauf in einem frischen Git-Archiv: main, Zweig nur Werkzeug (gruen), dann eine Datei der Lernplattform (rot)
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
            schreiben(PROPS, "<Project>\n  <Version>0.42.2</Version>\n  <StadtbauVersion>0.2.0</StadtbauVersion>\n</Project>\n")
            schreiben("src/Lernplattform.Core/A.cs", "a\n")
            schreiben("src/Stadtbau.App/B.cs", "b\n")
            commit("main")
            git("update-ref", "refs/remotes/origin/main", "HEAD", ordner=ordner)
            git("checkout", "-q", "-b", "zweig", ordner=ordner)
            schreiben("src/Stadtbau.App/B.cs", "b2\n")
            schreiben(PROPS, "<Project>\n  <Version>0.42.2</Version>\n  <StadtbauVersion>0.3.0</StadtbauVersion>\n</Project>\n")
            commit("nur Werkzeug")
            stumm = open(os.devnull, "w")
            alt, sys.stdout = sys.stdout, stumm
            try:
                gruen = pruefen(ordner=ordner)
                schreiben("src/Lernplattform.Core/A.cs", "a2\n")
                commit("Lernplattform")
                gegen = pruefen(ordner=ordner)
                # Lauf auf main selbst: Basis ist der Commit davor
                git("update-ref", "refs/remotes/origin/main", "HEAD", ordner=ordner)
                main_rot = pruefen(ordner=ordner)
            finally:
                sys.stdout = alt
                stumm.close()
        except (subprocess.CalledProcessError, OSError) as e:
            print("FEHLER: Selbsttest nur_werkzeug: Git-Archiv nicht anlegbar: %s" % e)
            return 1
        if gruen != 0:
            print("FEHLER: Selbsttest nur_werkzeug: Zweig nur mit Werkzeug-Dateien nicht gruen")
            rot = 1
        if gegen != 1:
            print("FEHLER: Selbsttest nur_werkzeug: Gegenprobe mit Datei der Lernplattform nicht rot")
            rot = 1
        if main_rot != 1:
            print("FEHLER: Selbsttest nur_werkzeug: Lauf auf main prueft den letzten Commit nicht (Gegenprobe nicht rot)")
            rot = 1
    print("Selbsttest nur_werkzeug: %d feste Faelle und Git-Probe (gruen, Gegenprobe rot, main rot), %s"
          % (len(FAELLE), "rot" if rot else "gruen"))
    return rot


def main():
    sys.stdout.reconfigure(newline="\n")
    if len(sys.argv) in (2, 3) and sys.argv[1] == "pruefen":
        return pruefen(sys.argv[2] if len(sys.argv) == 3 else None)
    if len(sys.argv) == 2 and sys.argv[1] == "selbsttest":
        return selbsttest()
    print("Aufruf: nur_werkzeug.py pruefen [basis] | selbsttest")
    return 1


if __name__ == "__main__":
    sys.exit(main())
