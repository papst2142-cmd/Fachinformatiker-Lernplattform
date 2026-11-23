"""Welle „Ablauf stabil“ Teil C (Weisungen P12-338, P12-340, Nico a): ein voller Lauf statt zwei (python3.12 -I).

Vor dem Squash-Merge eines PRs beweist dieses Werkzeug, dass main danach KEINEN zweiten vollen Lauf braucht:
  1. Baum gleich: `git merge-tree --write-tree <main> <kopf>` (das, was der Squash-Merge auf main schreibt) hat denselben
     Git-Baum (tree-sha) wie der Kopf selbst. Das gilt genau dann, wenn origin/main schon in den Zweig gemergt ist und
     danach auf main nichts Neues kam. Sonst: „voller Lauf nach Merge von origin/main noetig“.
  2. Lauf gruen: auf dem Kopf liegt ein gruener voller Dispatch-Lauf mit genau den Jobs der Liste `soll` aus
     release.yml (18, Hinweis-Job zaehlt nicht). Stufe werkzeug (6) und ohne-mac (10) reichen fuer release.yml nicht.
Ist beides gruen, nimmt release.yml diesen Lauf ueber `werkzeuge/release/lauf_waehlen.py` (gleicher Tree, Sammelliste 365).
Sammelliste 440 (Weisung P12-355, Frage 1 a): ein reiner Werkzeug-Stand (der Squash aendert gegen main nur Dateien, die
`werkzeuge/stadtbau/nur_werkzeug.py` dem Werkzeug zuordnet) braucht keinen vollen Lauf: dort reicht auch ein gruener
Lauf der Stufe werkzeug mit genau den Jobs der Liste `soll_werkzeug` aus stadtbau_release.yml (6), denn
stadtbau_release.yml nimmt ihn (lauf_waehlen.py). Jeder andere Stand braucht weiter 18/18; ohne-mac (10) reicht nie.
Lehre S 0.60.0 (P12-340): ein Commit nach dem Tor-Lauf (auch nur Doku) aendert den Baum, dann ist Punkt 1 rot.

  tor_gleich.py pruefen <kopf> [<main-ref>]   prueft 1 und 2 (GitHub ueber die oeffentliche API, GITHUB_TOKEN wenn gesetzt)
  tor_gleich.py selbsttest                     Faelle mit Gegenproben (eigenes Test-Repo in einem Temp-Ordner, ohne Netz)
Liest nur; legt nichts an, mergt nichts, startet keinen Lauf.
"""
import importlib.util
import json
import os
import re
import subprocess
import sys
import tempfile
import urllib.request

HILFSJOB = "urteil (GitHub oder echt)"
REPO = "papst2142-cmd/Fachinformatiker-Lernplattform"


def git(*args, cwd=None):
    return subprocess.run(["git", *args], cwd=cwd, check=True, capture_output=True, text=True).stdout.strip()


def baum_gleich(kopf, main, cwd=None):
    """(gleich, Zeile): Baum des Squash-Ergebnisses gegen den Baum des Kopfs."""
    r = subprocess.run(["git", "merge-tree", "--write-tree", main, kopf], cwd=cwd, capture_output=True, text=True)
    kopf_baum = git("rev-parse", kopf + "^{tree}", cwd=cwd)
    if r.returncode != 0:
        return False, "FEHLER: Squash von %s auf %s hat Konflikte: voller Lauf nach Merge von origin/main noetig" % (kopf[:7], main)
    squash = r.stdout.split("\n", 1)[0].strip()
    if squash != kopf_baum:
        return False, ("FEHLER: Baum nach dem Squash %s statt %s (Zweig enthaelt main nicht oder main ist weiter): "
                       "voller Lauf nach Merge von origin/main noetig" % (squash[:12], kopf_baum[:12]))
    return True, "Baum gleich: Squash auf %s ergibt den Baum des Kopfs %s (%s)" % (main, kopf[:7], kopf_baum[:12])


def soll_liste(release=".github/workflows/release.yml"):
    with open(release, encoding="utf-8") as f:
        block = re.search(r"(?<![A-Za-z_])soll='(.*?)'", f.read(), re.S)
    return sorted(z.strip() for z in block.group(1).splitlines() if z.strip()) if block else None


def soll_werkzeug_liste(release=".github/workflows/stadtbau_release.yml"):
    """Liste soll_werkzeug (Stufe werkzeug, 6 Jobs) aus stadtbau_release.yml, sortiert; None, wenn sie fehlt."""
    with open(release, encoding="utf-8") as f:
        block = re.search(r"(?<![A-Za-z_])soll_werkzeug='(.*?)'", f.read(), re.S)
    return sorted(z.strip() for z in block.group(1).splitlines() if z.strip()) if block else None


def lauf_urteil(laeufe, soll, soll_werkzeug=None):
    """laeufe: [{nummer, fazit, jobs: {Name: Ergebnis}}] (letzter Versuch). (Lauf oder None, Zeilen).
    soll_werkzeug: nur bei einem reinen Werkzeug-Stand (Sammelliste 440) die Liste der Stufe werkzeug, sonst None."""
    zeilen = []
    for lauf in laeufe:
        gruen = sorted(n for n, e in lauf["jobs"].items() if e == "success" and n != HILFSJOB)
        if lauf["fazit"] != "success":
            zeilen.append("Lauf %s ist nicht gruen (%s)" % (lauf["nummer"], lauf["fazit"]))
        elif gruen == soll:
            zeilen.append("Lauf %s: %d/%d gruen (voll)" % (lauf["nummer"], len(gruen), len(soll)))
            return lauf, zeilen
        elif soll_werkzeug is not None and gruen == soll_werkzeug:
            zeilen.append("Lauf %s: %d/%d gruen (Stufe werkzeug, reiner Werkzeug-Stand)" % (lauf["nummer"], len(gruen), len(soll_werkzeug)))
            return lauf, zeilen
        else:
            fehlt = sorted(set(soll) - set(gruen))
            zeilen.append("Lauf %s: %d von %d Jobs (fehlt z. B. %s) - %s"
                          % (lauf["nummer"], len(gruen), len(soll), ", ".join(fehlt[:2]) or "-",
                             "ohne-mac reicht nicht, werkzeug nur mit genau seinen Jobs" if soll_werkzeug is not None
                             else "Stufe werkzeug oder ohne-mac reicht nicht"))
    if soll_werkzeug is not None:
        zeilen.append("FEHLER: kein gruener Lauf %d/%d (voll) oder %d/%d (werkzeug) auf dem Kopf"
                      % (len(soll), len(soll), len(soll_werkzeug), len(soll_werkzeug)))
    else:
        zeilen.append("FEHLER: kein gruener voller Lauf %d/%d auf dem Kopf" % (len(soll), len(soll)))
    return None, zeilen


def _nur_werkzeug_modul():
    spec = importlib.util.spec_from_file_location("nur_werkzeug", os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "stadtbau", "nur_werkzeug.py"))
    modul = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(modul)
    return modul


def reiner_werkzeug_stand(kopf, main, cwd=None):
    """(ja/nein, Zeile): aendert der Squash gegen main nur Werkzeug-Dateien (Regeln von nur_werkzeug.py, Baum gegen Baum)?"""
    nw = _nur_werkzeug_modul()
    roh = git("diff", "--name-status", "--no-renames", main, kopf, cwd=cwd)
    dateien = [z.split("\t", 1)[1] for z in roh.splitlines() if "\t" in z]
    props = []
    if nw.PROPS in dateien:
        diff = git("diff", "--unified=0", main, kopf, "--", nw.PROPS, cwd=cwd)
        props = [z[1:] for z in diff.splitlines() if z[:1] in "+-" and not z.startswith("+++") and not z.startswith("---")]
    rot = nw.funde(dateien, props)
    if not dateien:
        return False, "Werkzeug-Stand: nein (keine geaenderte Datei gegen %s)" % main
    if rot:
        return False, "Werkzeug-Stand: nein (%d von %d Dateien gehoeren nicht nur zum Werkzeug, z. B. %s): nur voller Lauf 18/18" % (len(rot), len(dateien), rot[0])
    return True, "Werkzeug-Stand: ja (%d Dateien gegen %s, alle nur Werkzeug laut nur_werkzeug.py): voll 18/18 oder werkzeug 6/6" % (len(dateien), main)


def api(pfad):
    kopf = {"Accept": "application/vnd.github+json"}
    if os.environ.get("GITHUB_TOKEN"):
        kopf["Authorization"] = "Bearer " + os.environ["GITHUB_TOKEN"]
    with urllib.request.urlopen(urllib.request.Request("https://api.github.com/repos/" + REPO + pfad, headers=kopf), timeout=60) as r:
        return json.load(r)


def laeufe_holen(kopf):
    laeufe = []
    for r in api("/actions/workflows/ci.yml/runs?event=workflow_dispatch&head_sha=%s&per_page=20" % kopf)["workflow_runs"]:
        jobs = api("/actions/runs/%s/jobs?per_page=100" % r["id"])["jobs"]
        laeufe.append({"nummer": r["run_number"], "fazit": r["conclusion"], "jobs": {j["name"]: j["conclusion"] for j in jobs}})
    return laeufe


def pruefen(kopf, main="origin/main"):
    try:
        kopf = git("rev-parse", "--verify", kopf + "^{commit}")
        ok, zeile = baum_gleich(kopf, main)
    except subprocess.CalledProcessError as e:
        print("FEHLER: Commit %s oder %s nicht im Klon (erst git fetch origin <zweig> main): %s" % (kopf, main, (e.stderr or "").strip()[:200]))
        return 1
    print(zeile)
    try:
        werkzeug, zeile = reiner_werkzeug_stand(kopf, main)
    except subprocess.CalledProcessError as e:
        werkzeug, zeile = False, "Werkzeug-Stand: nein (git diff gescheitert: %s)" % (e.stderr or "").strip()[:200]
    print(zeile)
    try:
        lauf, zeilen = lauf_urteil(laeufe_holen(kopf), soll_liste(), soll_werkzeug_liste() if werkzeug else None)
    except (OSError, ValueError, KeyError) as e:
        lauf, zeilen = None, ["FEHLER: GitHub-Abfrage gescheitert (%s: %s)" % (type(e).__name__, e)]
    print("\n".join(zeilen))
    if ok and lauf:
        print("ERGEBNIS: ein Lauf reicht: nach dem Squash-Merge braucht main keinen zweiten vollen Lauf (%s nimmt Lauf %s)"
              % ("stadtbau_release.yml" if werkzeug else "release.yml", lauf["nummer"]))
        return 0
    print("ERGEBNIS: zweiter voller Lauf noetig")
    return 1


def selbsttest():
    rot = 0

    def melde(text):
        nonlocal rot
        print("FEHLER: Selbsttest tor_gleich: " + text)
        rot = 1

    with tempfile.TemporaryDirectory() as tmp:
        def g(*a):
            return git(*a, cwd=tmp)
        g("init", "-q", "-b", "main")
        g("config", "user.email", "t@t")
        g("config", "user.name", "t")

        def datei(name, inhalt, text):
            with open(os.path.join(tmp, name), "w", encoding="utf-8") as f:
                f.write(inhalt)
            g("add", name)
            g("commit", "-qm", text)
            return g("rev-parse", "HEAD")
        datei("a.txt", "1", "main 1")
        g("checkout", "-qb", "welle")
        kopf = datei("b.txt", "welle", "welle")
        # (1) Zweig enthaelt main: gleicher Baum
        ok, _ = baum_gleich(kopf, "main", cwd=tmp)
        if not ok:
            melde("Zweig auf aktuellem main nicht als gleich erkannt")
        # (2) Gegenprobe: main ist weiter (anderer Commit auf main) -> Baum anders
        g("checkout", "-q", "main")
        datei("c.txt", "main 2", "main 2")
        ok, zeile = baum_gleich(kopf, "main", cwd=tmp)
        if ok or "voller Lauf nach Merge von origin/main noetig" not in zeile:
            melde("main weiter, trotzdem gleich (%s)" % zeile)
        # (3) nach Merge von main in den Zweig wieder gleich
        g("checkout", "-q", "welle")
        g("merge", "-q", "--no-edit", "main")
        kopf2 = g("rev-parse", "HEAD")
        ok, _ = baum_gleich(kopf2, "main", cwd=tmp)
        if not ok:
            melde("nach Merge von main nicht gleich")
        # (4) Gegenprobe Lehre S 0.60.0: Doku-Commit nach dem Tor-Lauf -> der Lauf lag auf kopf2, der neue Kopf hat einen
        # anderen Baum als kopf2 (der Lauf passt nicht mehr)
        kopf3 = datei("doku.md", "nachtrag", "doku")
        if g("rev-parse", kopf3 + "^{tree}") == g("rev-parse", kopf2 + "^{tree}"):
            melde("Doku-Commit aendert den Baum nicht (Gegenprobe sieht nichts)")
        # (5) Konflikt -> rot
        g("checkout", "-q", "main")
        datei("b.txt", "anders", "main 3")
        ok, zeile = baum_gleich(kopf3, "main", cwd=tmp)
        if ok:
            melde("Konflikt nicht erkannt (%s)" % zeile)
        # Sammelliste 440: reiner Werkzeug-Stand (6) und Gegenproben (Engine, Version der Lernplattform, Datei des Werkzeugs
        # plus eine der Lernplattform)
        g("checkout", "-qb", "w440", "main")
        os.makedirs(os.path.join(tmp, "src", "Stadtbau.Core"), exist_ok=True)
        os.makedirs(os.path.join(tmp, "src", "Lernplattform.Engine"), exist_ok=True)
        wk = datei(os.path.join("src", "Stadtbau.Core", "A.cs"), "x", "werkzeug")
        ok, zeile = reiner_werkzeug_stand(wk, "main", cwd=tmp)
        if not ok:
            melde("reiner Werkzeug-Stand nicht erkannt (%s)" % zeile)
        ek = datei(os.path.join("src", "Lernplattform.Engine", "B.cs"), "y", "engine")
        ok, zeile = reiner_werkzeug_stand(ek, "main", cwd=tmp)
        if ok or "nur voller Lauf" not in zeile:
            melde("Gegenprobe Engine-Datei als Werkzeug-Stand erkannt (%s)" % zeile)
        g("checkout", "-qb", "w440b", "main")
        datei("Directory.Build.props", "<Version>1.4.1</Version>\n", "version")
        ok, zeile = reiner_werkzeug_stand(g("rev-parse", "HEAD"), "main", cwd=tmp)
        if ok:
            melde("Gegenprobe Version der Lernplattform als Werkzeug-Stand erkannt (%s)" % zeile)
        ok, zeile = reiner_werkzeug_stand(g("rev-parse", "main"), "main", cwd=tmp)
        if ok:
            melde("Gegenprobe ohne Aenderung als Werkzeug-Stand erkannt (%s)" % zeile)

    soll = sorted("job%02d" % i for i in range(18))
    mac = {"job%02d" % i for i in range(10, 18)}

    def lauf(nr, fazit="success", fehlt=(), rot_jobs=()):
        jobs = {n: ("failure" if n in rot_jobs else "success") for n in soll if n not in fehlt}
        jobs[HILFSJOB] = "success"
        return {"nummer": nr, "fazit": fazit, "jobs": jobs}
    faelle = [
        ("voll 18/18", [lauf(1)], 1),
        ("ohne-mac (10 Jobs) reicht nicht", [lauf(2, fehlt=mac)], None),
        ("werkzeug (6 Jobs) reicht nicht", [lauf(3, fehlt=set(soll[6:]))], None),
        ("roter Lauf", [lauf(4, fazit="failure", rot_jobs={"job03"})], None),
        ("ein Job fehlt", [lauf(5, fehlt={"job07"})], None),
        ("erst rot, dann 18/18", [lauf(6, fazit="failure"), lauf(7)], 7),
        ("Hinweis-Job allein zaehlt nicht", [{"nummer": 8, "fazit": "success", "jobs": {HILFSJOB: "success"}}], None),
    ]
    for name, laeufe, erwartet in faelle:
        gew, zeilen = lauf_urteil(laeufe, soll)
        ist = gew["nummer"] if gew else None
        if ist != erwartet:
            melde("%s: Lauf %s statt %s" % (name, ist, erwartet))
        if erwartet is None and not zeilen[-1].startswith("FEHLER: "):
            melde("%s ohne FEHLER-Zeile" % name)
    # Sammelliste 440: Faelle mit der Liste werkzeug (reiner Werkzeug-Stand); Gegenproben: ohne Werkzeug-Stand, ohne-mac,
    # ein Job zu wenig, ein Job zu viel, rot
    werkzeug_soll = sorted(soll[:6])

    def wlauf(nr, jobs, fazit="success"):
        d = {n: "success" for n in jobs}
        d[HILFSJOB] = "success"
        return {"nummer": nr, "fazit": fazit, "jobs": d}
    faelle_w = [
        ("werkzeug 6/6 bei Werkzeug-Stand", [wlauf(20, werkzeug_soll)], werkzeug_soll, 20),
        ("voll 18/18 bei Werkzeug-Stand", [lauf(21)], werkzeug_soll, 21),
        ("Gegenprobe werkzeug 6/6 ohne Werkzeug-Stand", [wlauf(22, werkzeug_soll)], None, None),
        ("Gegenprobe ohne-mac bei Werkzeug-Stand", [lauf(23, fehlt=mac)], werkzeug_soll, None),
        ("Gegenprobe werkzeug ein Job fehlt", [wlauf(24, werkzeug_soll[:5])], werkzeug_soll, None),
        ("Gegenprobe werkzeug ein Job zu viel", [wlauf(25, werkzeug_soll + [soll[7]])], werkzeug_soll, None),
        ("Gegenprobe werkzeug rot", [wlauf(26, werkzeug_soll, fazit="failure")], werkzeug_soll, None),
    ]
    for name, laeufe, wsoll, erwartet in faelle_w:
        gew, zeilen = lauf_urteil(laeufe, soll, wsoll)
        ist = gew["nummer"] if gew else None
        if ist != erwartet:
            melde("%s: Lauf %s statt %s" % (name, ist, erwartet))
        if erwartet is None and not zeilen[-1].startswith("FEHLER: "):
            melde("%s ohne FEHLER-Zeile" % name)
    faelle += faelle_w
    if os.path.exists(".github/workflows/release.yml"):
        liste = soll_liste()
        if not liste or len(liste) != 18:
            melde("Liste soll in release.yml nicht 18 Jobs (%s)" % (len(liste) if liste else "keine"))
    if os.path.exists(".github/workflows/stadtbau_release.yml"):
        wliste = soll_werkzeug_liste()
        if not wliste or len(wliste) != 6 or not set(wliste) < set(soll_liste() or []):
            melde("Liste soll_werkzeug in stadtbau_release.yml nicht 6 Jobs aus der vollen Liste (%s)" % (len(wliste) if wliste else "keine"))
    print("Selbsttest tor_gleich (ein Lauf statt zwei): %d Lauf-Faelle, 5 Baum-Faelle, 4 Werkzeug-Faelle, %s" % (len(faelle), "rot" if rot else "gruen"))
    return rot


def main():
    sys.stdout.reconfigure(newline="\n")
    if sys.argv[1:] == ["selbsttest"]:
        return selbsttest()
    if len(sys.argv) in (3, 4) and sys.argv[1] == "pruefen":
        return pruefen(*sys.argv[2:])
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main())
