"""Sammelliste 365 (Weisung P12-289, Nico 16.11.2026): welcher gruene CI-Lauf ein Release erlaubt (python3.12 -I).

Zuerst wie bisher ein gruener Lauf (Dispatch) auf genau dem Commit von main. Gibt es keinen, gilt ein gruener Lauf
auf einem anderen Commit nur, wenn dessen Git-Tree gleich dem Tree des main-Kopfs ist (Datei fuer Datei derselbe
Inhalt, z. B. der volle PR-Lauf vor dem Squash-Merge). Die Jobliste (18 bzw. 6), das Ergebnis jedes Jobs, der
letzte Versuch und der Filter fuer den Hinweis-Job bleiben gleich streng. Ein anderer Tree: kein Release, Meldung
"voller Lauf auf main noetig: Inhalt weicht ab" - nie still.

  lauf_waehlen.py waehlen             in GitHub Actions (release.yml, stadtbau_release.yml): Listen aus SOLL und
                                      SOLL_WERKZEUG (optional), Job-Filter aus JOBFILTER (jq), Commit GITHUB_SHA;
                                      schreibt lauf=<Text> nach GITHUB_OUTPUT, Rueckgabe 1 ohne passenden Lauf.
  lauf_waehlen.py text <release.md> <lauf>   setzt die Zeile "Geprueft mit ..." vor "Release-Seite:" in den Text.
  lauf_waehlen.py selbsttest          Faelle mit Gegenproben (gleicher Tree an, anderer Tree ab, rot ab, Job fehlt ab).
Nichts hier laedt hoch, legt einen Tag an oder loescht etwas.
"""
import json
import os
import subprocess
import sys

HILFSJOB = "urteil (GitHub oder echt)"
ABWEICHUNG = "voller Lauf auf main noetig: Inhalt weicht ab"


def waehlen(main_sha, main_tree, laeufe, listen):
    """laeufe: Liste von dict(id, nummer, sha, tree, fazit, jobs = Namen der gruenen Jobs, schon gefiltert),
    listen: {Name: [Jobs]}. Rueckgabe (lauf, listenname, zeilen); lauf None = kein Release."""
    zeilen = []
    soll = {name: sorted(jobs) for name, jobs in listen.items()}
    abweichend = []
    # Erst Laeufe auf genau dem Commit von main, dann die uebrigen (Reihenfolge sonst wie von GitHub, neueste zuerst)
    for lauf in sorted(laeufe, key=lambda l: l["sha"] != main_sha):
        kennung = "Lauf %s (Commit %s)" % (lauf["nummer"], lauf["sha"][:7])
        if lauf["fazit"] != "success":
            zeilen.append("%s ist nicht gruen (%s)" % (kennung, lauf["fazit"]))
            continue
        ist = sorted(j for j in lauf["jobs"] if j != HILFSJOB)
        treffer = next((name for name, jobs in soll.items() if ist == jobs), None)
        if treffer is None:
            fehlt = sorted(set().union(*soll.values()) - set(ist)) if soll else []
            zeilen.append("%s hat nicht alle Jobs gruen (%d gruen), fehlen z. B.: %s" % (kennung, len(ist), ", ".join(fehlt[:3])))
            continue
        if lauf["sha"] == main_sha:
            zeilen.append("%s: %s, %d/%d gruen, gleicher Commit wie main" % (kennung, treffer, len(ist), len(ist)))
            return lauf, treffer, zeilen
        if lauf["tree"] == main_tree:
            zeilen.append("%s: %s, %d/%d gruen, Tree gleich main: ja (%s)" % (kennung, treffer, len(ist), len(ist), main_tree[:12]))
            return lauf, treffer, zeilen
        zeilen.append("%s ist gruen, aber Tree %s statt %s (Tree gleich main: nein)" % (kennung, (lauf["tree"] or "?")[:12], main_tree[:12]))
        abweichend.append(lauf)
    if abweichend:
        zeilen.append("FEHLER: " + ABWEICHUNG + " (kein gruener Lauf auf %s, %d gruene Laeufe mit anderem Inhalt)" % (main_sha[:7], len(abweichend)))
    else:
        zeilen.append("FEHLER: kein gruener Lauf (%s) auf %s und keiner mit gleichem Tree" % (" oder ".join(soll), main_sha[:7]))
    return None, None, zeilen


def lauf_text(lauf, main_sha):
    gleich = "gleicher Commit" if lauf["sha"] == main_sha else "ja"
    return "CI-Lauf %s, Commit %s, Tree gleich main: %s" % (lauf["nummer"], lauf["sha"][:7], gleich)


def text_einsetzen(text, lauf):
    zeile = "Geprüft mit " + lauf + "."
    if "\nRelease-Seite: " not in text or "\nGeprüft mit " in text:
        raise ValueError("Release-Text ohne Zeile 'Release-Seite:' oder schon mit 'Geprueft mit'")
    return text.replace("\nRelease-Seite: ", "\n" + zeile + "\n\nRelease-Seite: ", 1)


def gh(pfad, jq):
    aus = subprocess.run(["gh", "api", pfad, "--jq", jq], check=True, capture_output=True, text=True).stdout
    return [z for z in aus.splitlines() if z.strip()]


def laeufe_holen(repo, main_sha, jobfilter):
    basis = "repos/%s/actions/workflows/ci.yml/runs?event=workflow_dispatch&status=success&per_page=50" % repo
    form = ".workflow_runs[] | {id, run_number, head_sha, conclusion}"
    # Laeufe auf dem main-Kopf (wie bisher) und die 50 neuesten gruenen Laeufe (fuer den Vergleich des Trees)
    roh = [json.loads(z) for z in gh(basis + "&head_sha=" + main_sha, form)]
    gesehen = {r["id"] for r in roh}
    roh += [r for r in (json.loads(z) for z in gh(basis, form)) if r["id"] not in gesehen]
    laeufe, trees = [], {}
    for r in roh:
        sha = r["head_sha"]
        if sha not in trees:
            trees[sha] = gh("repos/%s/git/commits/%s" % (repo, sha), ".tree.sha")[0]
        jobs = gh("repos/%s/actions/runs/%s/jobs?per_page=100" % (repo, r["id"]), jobfilter)
        laeufe.append({"id": r["id"], "nummer": r["run_number"], "sha": sha, "tree": trees[sha],
                       "fazit": r["conclusion"], "jobs": jobs})
    return laeufe


def liste(text):
    return sorted(z.strip() for z in text.splitlines() if z.strip())


def waehlen_in_actions():
    main_sha = os.environ["GITHUB_SHA"]
    kopf = subprocess.run(["git", "rev-parse", "HEAD"], check=True, capture_output=True, text=True).stdout.strip()
    if kopf != main_sha:
        print("FEHLER: ausgecheckt %s statt %s" % (kopf, main_sha))
        return 1
    main_tree = subprocess.run(["git", "rev-parse", "HEAD^{tree}"], check=True, capture_output=True, text=True).stdout.strip()
    listen = {"voll": liste(os.environ["SOLL"])}
    if os.environ.get("SOLL_WERKZEUG", "").strip():
        listen["werkzeug"] = liste(os.environ["SOLL_WERKZEUG"])
    laeufe = laeufe_holen(os.environ["GITHUB_REPOSITORY"], main_sha, os.environ["JOBFILTER"])
    lauf, _, zeilen = waehlen(main_sha, main_tree, laeufe, listen)
    print("\n".join(zeilen))
    if lauf is None:
        return 1
    text = lauf_text(lauf, main_sha)
    print("Genommen: " + text)
    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as d:
        d.write("lauf=" + text + "\n")
    return 0


def selbsttest():
    soll = {"voll": ["a", "b", "c"], "werkzeug": ["a", "w"]}
    m, t = "1" * 40, "t" * 40

    def lauf(nr, sha, tree, fazit="success", jobs=("a", "b", "c", HILFSJOB)):
        return {"id": nr, "nummer": nr, "sha": sha, "tree": tree, "fazit": fazit, "jobs": list(jobs)}

    faelle = [
        ("gruener Lauf auf main", [lauf(1, m, t)], 1),
        ("Tree gleich: angenommen", [lauf(2, "2" * 40, t)], 2),
        ("Tree verschieden: abgelehnt", [lauf(3, "3" * 40, "x" * 40)], None),
        ("roter Lauf mit gleichem Tree: abgelehnt", [lauf(4, "4" * 40, t, fazit="failure")], None),
        ("Job fehlt, gleicher Tree: abgelehnt", [lauf(5, "5" * 40, t, jobs=("a", "b", HILFSJOB))], None),
        ("Job fehlt auf main: abgelehnt", [lauf(6, m, t, jobs=("a", "c"))], None),
        ("nur Hinweis-Job zusaetzlich zaehlt nicht", [lauf(7, "7" * 40, t, jobs=("a", "b", "c"))], 7),
        ("Lauf auf main vor neuerem gleichem Tree", [lauf(8, "8" * 40, t), lauf(9, m, t)], 9),
        ("Lauf werkzeug 2/2 mit gleichem Tree", [lauf(10, "a" * 40, t, jobs=("a", "w", HILFSJOB))], 10),
        ("zusaetzlicher fremder Job: abgelehnt", [lauf(11, "b" * 40, t, jobs=("a", "b", "c", "d"))], None),
    ]
    rot = 0
    for name, laeufe, erwartet in faelle:
        gewaehlt, _, zeilen = waehlen(m, t, laeufe, soll)
        ist = gewaehlt["nummer"] if gewaehlt else None
        if ist != erwartet:
            print("FEHLER: Selbsttest Lauf waehlen: %s: Lauf %s statt %s" % (name, ist, erwartet))
            rot = 1
        if erwartet is None and not zeilen[-1].startswith("FEHLER: "):
            print("FEHLER: Selbsttest Lauf waehlen: %s ohne Fehlerzeile (still)" % name)
            rot = 1
    _, _, zeilen = waehlen(m, t, [lauf(3, "3" * 40, "x" * 40)], soll)
    if ABWEICHUNG not in zeilen[-1]:
        print("FEHLER: Selbsttest Lauf waehlen: anderer Tree ohne Meldung '%s'" % ABWEICHUNG)
        rot = 1
    text = text_einsetzen("Kopf\n\nRelease-Seite: x\n", lauf_text(lauf(2, "2" * 40, t), m))
    if "\nGeprüft mit CI-Lauf 2, Commit 2222222, Tree gleich main: ja.\n\nRelease-Seite: x\n" not in text:
        print("FEHLER: Selbsttest Lauf waehlen: Zeile im Release-Text fehlt")
        rot = 1
    print("Selbsttest Lauf waehlen (Sammelliste 365): %d Faelle, %s" % (len(faelle), "rot" if rot else "gruen"))
    return rot


def main():
    sys.stdout.reconfigure(newline="\n")
    if sys.argv[1:] == ["selbsttest"]:
        return selbsttest()
    if sys.argv[1:] == ["waehlen"]:
        return waehlen_in_actions()
    if len(sys.argv) == 4 and sys.argv[1] == "text":
        with open(sys.argv[2], encoding="utf-8") as d:
            text = text_einsetzen(d.read(), sys.argv[3])
        with open(sys.argv[2], "w", encoding="utf-8", newline="\n") as d:
            d.write(text)
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main())
