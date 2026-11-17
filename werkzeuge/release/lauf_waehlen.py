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
  lauf_waehlen.py selbsttest          Faelle mit Gegenproben (gleicher Tree an, anderer Tree ab, rot ab, Job fehlt ab,
                                      Jobs nur fuer Kandidaten, Wiederholung und FEHLER-Zeile bei GitHub-Abfragen).
Sammelliste 368 (Weisung P12-291): Jobs werden nur fuer Kandidaten geholt (gleicher Commit oder gleicher Tree; der
Tree kommt zuerst aus der Laufliste). Jeder `gh api`-Aufruf wird bei einem Fehler bis 3 Mal mit kurzer Pause wiederholt,
danach steht genau eine Zeile "FEHLER: GitHub-Abfrage ..." (Rueckgabe, Text) da, nie ein Traceback.
Nichts hier laedt hoch, legt einen Tag an oder loescht etwas.
"""
import json
import os
import subprocess
import sys
import time

HILFSJOB = "urteil (GitHub oder echt)"
ABWEICHUNG = "voller Lauf auf main noetig: Inhalt weicht ab"
WIEDERHOLUNGEN = 3
PAUSEN = (5, 10, 20)


class GhFehler(Exception):
    """Eine GitHub-Abfrage scheiterte auch nach allen Wiederholungen; str() ist die fertige FEHLER-Zeile."""


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
        if lauf["sha"] != main_sha and lauf["tree"] != main_tree:
            # Sammelliste 368: kein Kandidat (anderer Inhalt), seine Jobs werden gar nicht erst geholt
            zeilen.append("%s ist gruen, aber Tree %s statt %s (Tree gleich main: nein)" % (kennung, (lauf["tree"] or "?")[:12], main_tree[:12]))
            abweichend.append(lauf)
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
        zeilen.append("%s: %s, %d/%d gruen, Tree gleich main: ja (%s)" % (kennung, treffer, len(ist), len(ist), main_tree[:12]))
        return lauf, treffer, zeilen
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


def gh(pfad, jq, ausfuehren=subprocess.run, schlafen=time.sleep):
    """Ein `gh api`-Aufruf; bei Fehler bis WIEDERHOLUNGEN Mal mit Pause wiederholt, dann GhFehler (fertige Zeile)."""
    letzter = None
    for versuch in range(WIEDERHOLUNGEN + 1):
        if versuch:
            schlafen(PAUSEN[min(versuch - 1, len(PAUSEN) - 1)])
        try:
            r = ausfuehren(["gh", "api", pfad, "--jq", jq], capture_output=True, text=True)
        except OSError as e:
            letzter = ("-", str(e))
            continue
        if r.returncode == 0:
            return [z for z in r.stdout.splitlines() if z.strip()]
        letzter = (r.returncode, " ".join((r.stderr or r.stdout or "").split())[:300])
        print("WIEDERHOLUNG: GitHub-Abfrage %s, Versuch %d von %d: Rueckgabe %s" % (pfad, versuch + 1, WIEDERHOLUNGEN + 1, letzter[0]))
    raise GhFehler("FEHLER: GitHub-Abfrage %s nach %d Versuchen gescheitert (Rueckgabe %s, %s)"
                   % (pfad, WIEDERHOLUNGEN + 1, letzter[0], letzter[1] or "ohne Text"))


def laeufe_holen(repo, main_sha, main_tree, jobfilter, abfrage=gh):
    basis = "repos/%s/actions/workflows/ci.yml/runs?event=workflow_dispatch&status=success&per_page=50" % repo
    form = ".workflow_runs[] | {id, run_number, head_sha, conclusion, tree: .head_commit.tree_id}"
    # Laeufe auf dem main-Kopf (wie bisher) und die 50 neuesten gruenen Laeufe (fuer den Vergleich des Trees)
    roh = [json.loads(z) for z in abfrage(basis + "&head_sha=" + main_sha, form)]
    gesehen = {r["id"] for r in roh}
    roh += [r for r in (json.loads(z) for z in abfrage(basis, form)) if r["id"] not in gesehen]
    laeufe, trees = [], {}
    for r in roh:
        sha = r["head_sha"]
        if sha not in trees:
            trees[sha] = r.get("tree") or abfrage("repos/%s/git/commits/%s" % (repo, sha), ".tree.sha")[0]
        # Sammelliste 368: Jobs nur fuer Kandidaten (gleicher Commit oder gleicher Tree); andere Laeufe brauchen keine Abfrage
        kandidat = sha == main_sha or trees[sha] == main_tree
        jobs = abfrage("repos/%s/actions/runs/%s/jobs?per_page=100" % (repo, r["id"]), jobfilter) if kandidat else None
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
    try:
        laeufe = laeufe_holen(os.environ["GITHUB_REPOSITORY"], main_sha, main_tree, os.environ["JOBFILTER"])
    except GhFehler as e:
        print(str(e))
        return 1
    except (ValueError, KeyError, IndexError) as e:
        print("FEHLER: GitHub-Abfrage lieferte unerwartete Daten (%s: %s)" % (type(e).__name__, e))
        return 1
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
    rot |= selbsttest_368(m, t)
    print("Selbsttest Lauf waehlen (Sammelliste 365, 368): %d Faelle, %s" % (len(faelle), "rot" if rot else "gruen"))
    return rot


def selbsttest_368(m, t):
    """Sammelliste 368: Jobs nur fuer Kandidaten, Wiederholung voruebergehender Fehler, klare FEHLER-Zeile."""
    import contextlib
    import io
    rot = 0

    def melde(text):
        nonlocal rot
        print("FEHLER: Selbsttest Lauf waehlen (368): " + text)
        rot = 1

    liste_laeufe = [{"id": 1, "run_number": 11, "head_sha": "a" * 40, "conclusion": "success", "tree": "x" * 40},
                    {"id": 2, "run_number": 12, "head_sha": "b" * 40, "conclusion": "success", "tree": t},
                    {"id": 3, "run_number": 13, "head_sha": m, "conclusion": "success", "tree": None}]

    def abfrage_von(gegenprobe):
        gefragt = []

        def abfrage(pfad, jq):
            gefragt.append(pfad)
            if pfad.endswith("&head_sha=" + m):
                return [json.dumps(liste_laeufe[2])]
            if "/runs?" in pfad:
                return [json.dumps(r) for r in liste_laeufe]
            if "/git/commits/" in pfad:
                return [t]
            if pfad.endswith("/runs/1/jobs?per_page=100"):
                raise GhFehler("FEHLER: GitHub-Abfrage %s (Rueckgabe 1, alter Lauf)" % pfad)
            return ["a", "b", "c"]
        return abfrage, gefragt

    # (1) Kandidatenfilter: Lauf 1 (anderer Tree) wird nie nach Jobs gefragt, Lauf 2 (gleicher Tree) und 3 (main) schon
    abfrage, gefragt = abfrage_von(False)
    try:
        laeufe = laeufe_holen("o/r", m, t, ".jobs", abfrage)
    except GhFehler as e:
        melde("Jobs eines Laufs mit anderem Tree abgefragt: " + str(e))
        laeufe = []
    jobs_gefragt = sorted(p.split("/runs/")[1].split("/")[0] for p in gefragt if "/jobs?" in p)
    if laeufe and jobs_gefragt != ["2", "3"]:
        melde("Jobs abgefragt fuer Laeufe %s statt 2 und 3" % jobs_gefragt)
    if laeufe and not any("/git/commits/" + m in p for p in gefragt):
        melde("fehlender Tree in der Laufliste nicht ueber den Commit geholt")
    if laeufe:
        gewaehlt, _, _ = waehlen(m, t, laeufe, {"voll": ["a", "b", "c"]})
        if not gewaehlt or gewaehlt["nummer"] != 13:
            melde("mit Kandidatenfilter nicht Lauf 13 gewaehlt")
    # Gegenprobe: ohne Filter (alle Laeufe wie bis S 0.56.0) bricht der alte Lauf 1 die Wahl ab
    abfrage, _ = abfrage_von(True)
    try:
        for r in liste_laeufe:
            abfrage("repos/o/r/actions/runs/%s/jobs?per_page=100" % r["id"], ".jobs")
        melde("Gegenprobe ohne Kandidatenfilter scheiterte nicht")
    except GhFehler:
        pass

    # (2) Wiederholung: einmal Rueckgabe 1, dann 200 -> Ergebnis da, eine Pause
    class Antwort:
        def __init__(self, rc, aus="", err=""):
            self.returncode, self.stdout, self.stderr = rc, aus, err

    def ablauf(antworten):
        pausen = []
        folge = iter(antworten)
        return (lambda befehl, **_: next(folge)), pausen.append, pausen

    ausf, schlaf, pausen = ablauf([Antwort(1, err="HTTP 502: Bad Gateway"), Antwort(0, "x\n")])
    with contextlib.redirect_stdout(io.StringIO()):
        try:
            ergebnis = gh("repos/o/r/actions/runs/1/jobs", ".jobs", ausf, schlaf)
        except GhFehler as e:
            ergebnis = str(e)
    if ergebnis != ["x"] or pausen != [PAUSEN[0]]:
        melde("voruebergehender Fehler nicht wiederholt (Ergebnis %r, Pausen %r)" % (ergebnis, pausen))
    # (3) dauerhaft rot: 4 Versuche, dann genau eine FEHLER-Zeile mit Rueckgabe und Text, kein Traceback
    ausf, schlaf, pausen = ablauf([Antwort(1, err="HTTP 404: Not Found")] * (WIEDERHOLUNGEN + 1))
    with contextlib.redirect_stdout(io.StringIO()):
        try:
            gh("repos/o/r/actions/runs/9/jobs", ".jobs", ausf, schlaf)
            zeile = None
        except GhFehler as e:
            zeile = str(e)
    if not zeile or not zeile.startswith("FEHLER: GitHub-Abfrage repos/o/r/actions/runs/9/jobs") \
            or "Rueckgabe 1" not in zeile or "HTTP 404" not in zeile or len(pausen) != WIEDERHOLUNGEN:
        melde("dauerhafter Fehler ohne klare Zeile (%r, %d Pausen)" % (zeile, len(pausen)))
    # Gegenprobe: der alte Aufruf (check=True) wirft einen Python-Fehler statt der Zeile
    try:
        subprocess.run([sys.executable, "-c", "import sys; sys.exit(1)"], check=True, capture_output=True)
        melde("Gegenprobe check=True warf keinen Python-Fehler")
    except subprocess.CalledProcessError:
        pass
    # (4) waehlen_in_actions macht aus GhFehler eine Zeile und Rueckgabe 1
    alt_holen = globals()["laeufe_holen"]
    alt_env = {k: os.environ.get(k) for k in ("GITHUB_SHA", "SOLL", "JOBFILTER", "GITHUB_REPOSITORY")}

    def holen_rot(*_):
        raise GhFehler("FEHLER: GitHub-Abfrage repos/o/r/x nach 4 Versuchen gescheitert (Rueckgabe 1, kaputt)")
    try:
        kopf = subprocess.run(["git", "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip()
        if kopf:
            os.environ.update(GITHUB_SHA=kopf, SOLL="a", JOBFILTER=".", GITHUB_REPOSITORY="o/r")
            globals()["laeufe_holen"] = holen_rot
            puffer = io.StringIO()
            with contextlib.redirect_stdout(puffer):
                rc = waehlen_in_actions()
            if rc != 1 or puffer.getvalue().strip() != "FEHLER: GitHub-Abfrage repos/o/r/x nach 4 Versuchen gescheitert (Rueckgabe 1, kaputt)":
                melde("waehlen ohne klare FEHLER-Zeile (rc %s, Ausgabe %r)" % (rc, puffer.getvalue()))
    finally:
        globals()["laeufe_holen"] = alt_holen
        for k, v in alt_env.items():
            if v is None:
                os.environ.pop(k, None)
            else:
                os.environ[k] = v
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
