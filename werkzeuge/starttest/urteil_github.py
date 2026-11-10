#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Urteil "GitHub oder echt" fuer einen roten CI-Lauf (Welle CI schneller 2, Meldung P12-84 Punkt 2, Nico Frage 1 = a,
Weisung P12-131). Aufgerufen ueber `urteil.py github` (Job "urteil (GitHub oder echt)" am Ende von ci.yml) und
`urteil.py selbsttest`.

Ein roter Job zaehlt nur dann als GitHub-Ursache, wenn ein fester Beleg im Protokoll seines roten Schritts steht:
  - kein Rechner: kein Schritt ist gelaufen (Job ohne Schritte oder alle Schritte ohne Ergebnis);
  - weniger Jobs als die Liste der Stufe (GitHub hat nicht alle angelegt);
  - Abfragebremse: "rate limit exceeded" / "API rate limit" in einer Fehlerzeile;
  - Serverfehler 5xx von GitHub (500-504 mit "status code", "HTTP", "Gateway", "Service Unavailable" ...) in einer
    Fehlerzeile; WIEDERHOLUNG-Zeilen (Wiederholung, die danach gelang) zaehlen dabei NIE als Beleg;
  - Netzfehler beim Hoch-/Herunterladen (ENOTFOUND, ECONNRESET, ETIMEDOUT, EAI_AGAIN, socket hang up);
  - Rechner verloren ("lost communication with the server", "runner has received a shutdown signal").
Jeder rote Schritt eines Jobs braucht so einen Beleg. Echt rot bleibt der Job, sobald im roten Schritt
  - eine FEHLER-Zeile ohne Beleg steht, die nicht in der festen Liste der Folgezeilen steht
    (FOLGE: "Feed ... nennt nicht", "... laut GitHub-API", "pakete ... nicht gruen" = Job, von dem android abhaengt),
  - ein Testfehler ([FAIL], Test Run Failed, error CS/MSB/NU/NETSDK, Unhandled exception, URTEIL: rot, Absturz,
    Wachhund) oder eine ##[error]-Zeile ohne Beleg (ausser "Process completed with exit code n") steht,
  - kein Protokoll lesbar ist oder der Job ohne roten Schritt rot ist (z. B. Zeitgrenze).
Folgefehler "pakete ... nicht gruen" zaehlen nur, wenn jeder rote pakete-Job selbst GitHub ist.
Schriftzeilen des Schritts (zwischen ##[group]Run und ##[endgroup]: Skripttext, Umgebung) zaehlen nicht.

Ergebnis: "nur GitHub" (dann nur die roten Jobs neu starten, gleicher Lauf, gleicher Kopf; fehlen Jobs: ganzer
Neustart, zaehlt nicht als Versuch), "echt rot" (ganzer Neustart nach Regel, hoechstens 3 Versuche) oder "gruen".
Das Merge-Tor bleibt der volle Lauf 17/17 (bzw. die Stufe) auf demselben Kopf. Dieses Urteil aendert kein Ergebnis
eines Jobs; der Job "urteil (GitHub oder echt)" ist immer gruen und nur ein Hinweis.
"""
import datetime
import json
import os
import re
import subprocess

HILFSJOB = "urteil (GitHub oder echt)"
ZEIT = re.compile(r"^\ufeff?(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d)(?:\.\d+)?Z ?")
FARBE = re.compile(r"\x1b\[[0-9;]*m")
BELEG = re.compile(
    r"rate limit exceeded|API rate limit|"
    r"\b50[0-4]\b.*(status|HTTP|Gateway|Service Unavailable|Internal Server Error)|"
    r"(status code|HTTP)[^0-9]{0,40}\b50[0-4]\b|Bad Gateway|Gateway Time-?out|Service Unavailable|"
    r"ENOTFOUND|ECONNRESET|ETIMEDOUT|EAI_AGAIN|socket hang up|"
    r"lost communication with the server|runner has received a shutdown signal", re.I)
FOLGE = re.compile(r"FEHLER: (Feed \(Kanal [^)]*\) nennt nicht |.*laut GitHub-API)")
FOLGE_PAKETE = re.compile(r"FEHLER: (pakete( \(stadtbau win-x64\))?) nicht gruen")
TESTFEHLER = re.compile(r"\[FAIL\]|Test Run Failed|\berror (CS|MSB|NU|NETSDK)\d+|Unhandled exception|URTEIL: rot|"
                        r"Absturz|Wachhund|failed!", re.I)
FEHLERZEILE = re.compile(r"FEHLER|##\[error\]")
EXIT = re.compile(r"^##\[error\]Process completed with exit code \d+\.?$")


def zeit(text):
    return datetime.datetime.strptime(text[:19], "%Y-%m-%dT%H:%M:%S") if text else None


def schrittzeilen(log, schritt):
    """Zeilen des Protokolls im Zeitfenster des Schritts (Sekunden, +1 s), ohne Skripttext und Umgebung, ohne Zeitstempel
    und Farbcodes. Ohne Zeiten des Schritts: keine Zeilen."""
    anfang, ende = zeit(schritt.get("started_at")), zeit(schritt.get("completed_at"))
    if not anfang or not ende:
        return []
    ende += datetime.timedelta(seconds=1)
    zeilen, gruppe = [], False
    for roh in log.splitlines():
        m = ZEIT.match(roh)
        if not m:
            continue
        t = datetime.datetime.strptime(m.group(1), "%Y-%m-%dT%H:%M:%S")
        text = FARBE.sub("", roh[m.end():]).strip()
        if text.startswith("##[group]Run "):
            gruppe = True
            continue
        if gruppe:
            if text.startswith("##[endgroup]"):
                gruppe = False
            continue
        if anfang <= t <= ende:
            zeilen.append(text)
    return zeilen


def schritt_urteil(zeilen):
    """('github', Beleg) | ('echt', Grund) | ('folge', Name des pakete-Jobs) fuer die Zeilen eines roten Schritts."""
    beleg, folge = None, None
    for z in zeilen:
        if z.startswith("WIEDERHOLUNG"):
            continue
        if TESTFEHLER.search(z):
            return "echt", "Testfehler: " + z[:160]
        if BELEG.search(z) and (FEHLERZEILE.search(z) or "Exception" in z or "Failed" in z or "error" in z.lower()):
            beleg = beleg or z[:160]
            continue
        if EXIT.match(z):
            continue
        f = FOLGE_PAKETE.search(z)
        if f:
            folge = f.group(1)
            continue
        if FEHLERZEILE.search(z):
            if FOLGE.search(z):
                continue
            return "echt", "Fehler ohne GitHub-Beleg: " + z[:160]
    if beleg:
        return "github", beleg
    if folge:
        return "folge", folge
    return "echt", "kein GitHub-Beleg im roten Schritt"


def job_urteil(job, log):
    """('github'|'echt'|'folge:<pakete>', Grund) fuer einen roten Job (API-Daten, Protokolltext oder None)."""
    schritte = job.get("steps") or []
    gelaufen = [s for s in schritte if s.get("conclusion") not in (None, "skipped")]
    if not gelaufen:
        return "github", "kein Rechner (kein Schritt gelaufen)"
    rot = [s for s in schritte if s.get("conclusion") in ("failure", "cancelled", "timed_out")]
    if not rot:
        return "echt", "rot ohne roten Schritt (Zeitgrenze oder Abbruch des Jobs)"
    if log is None:
        return "echt", "Protokoll nicht lesbar"
    folgen = []
    for s in rot:
        art, grund = schritt_urteil(schrittzeilen(log, s))
        if art == "echt":
            return "echt", "%s: %s" % (s.get("name"), grund)
        if art == "folge":
            folgen.append(grund)
            continue
        letzter = "%s: %s" % (s.get("name"), grund)
    if folgen:
        return "folge:" + ",".join(sorted(set(folgen))), "Folgefehler von " + ", ".join(sorted(set(folgen)))
    return "github", letzter


def urteilen(jobs, logs, erwartet):
    """jobs = Liste der API-Jobs (neuester Versuch je Job), logs = {job_id: text|None}, erwartet = Namen der Stufe.
    Gibt (Ergebnis, Zeilen) zurueck; Ergebnis 'gruen', 'nur GitHub' oder 'echt rot'."""
    jobs = [j for j in jobs if j.get("name") != HILFSJOB]
    zeilen = []
    namen = set(j.get("name") for j in jobs)
    fehlt = sorted(set(erwartet) - namen)
    urteile = {}
    for j in jobs:
        if j.get("conclusion") in ("success", "skipped"):
            continue
        urteile[j.get("name")] = job_urteil(j, logs.get(j.get("id")))
    # Folgefehler gelten nur, wenn jeder rote pakete-Job selbst GitHub ist
    for name, (art, grund) in list(urteile.items()):
        if art.startswith("folge:"):
            quellen = art[6:].split(",")
            rote = [n for n in urteile if any(n == q or (q == "pakete" and re.fullmatch(r"pakete \([a-z0-9-]+\)", n)) for q in quellen)]
            if rote and all(urteile[n][0] == "github" for n in rote):
                urteile[name] = ("github", grund + " (alle GitHub)")
            else:
                urteile[name] = ("echt", grund + " (Quelle echt rot oder nicht gefunden)")
    for name in sorted(urteile):
        art, grund = urteile[name]
        zeilen.append("JOB %s: %s (%s)" % (name, "GitHub" if art == "github" else "echt", grund))
    for name in fehlt:
        zeilen.append("JOB %s: GitHub (nicht angelegt, %d von %d Jobs da)" % (name, len(namen & set(erwartet)), len(erwartet)))
    if not urteile and not fehlt:
        ergebnis = "gruen"
        zeilen.append("EMPFEHLUNG: nichts")
    elif all(a == "github" for a, _ in urteile.values()):
        ergebnis = "nur GitHub"
        zeilen.append("EMPFEHLUNG: " + ("ganzer Neustart (Jobs fehlen), zaehlt nicht als Versuch" if fehlt
                                        else "nur die roten Jobs neu starten (gleicher Lauf, gleicher Kopf), zaehlt nicht als Versuch"))
    else:
        ergebnis = "echt rot"
        zeilen.append("EMPFEHLUNG: ganzer Neustart nach Regel (hoechstens 3 Versuche), Ursache je rotem Job melden")
    return ergebnis, ["URTEIL GITHUB: " + ergebnis] + zeilen


def erwartete_jobs(stufe, soll_liste, release=".github/workflows/release.yml",
                   werkzeug_release=".github/workflows/stadtbau_release.yml"):
    """Namen der Jobs je Stufe aus den Listen der Release-Workflows (eine Quelle mit dem Merge-Tor)."""
    voll = soll_liste(release) or []
    if stufe == "voll":
        return voll
    if stufe == "ohne-mac":
        return [n for n in voll if "macos" not in n and "osx" not in n]
    if stufe == "werkzeug":
        return soll_liste(werkzeug_release, "soll_werkzeug") or []
    return ["sollwerte (Pruefsummen)", "tests (ubuntu-latest)"]


def gh(pfad):
    return subprocess.run(["gh", "api", pfad], check=True, capture_output=True, text=True, encoding="utf-8",
                          errors="replace").stdout


def protokoll(repo, job_id, hinweise):
    """Protokoll eines Jobs. Erst gh api; scheitert das (Lauf 759: beide roten Jobs "nicht lesbar"), direkt ueber die API
    mit dem Token und der Weiterleitung zum Speicher OHNE Anmeldung (dorthin gehoert kein Token). None = nicht lesbar,
    der Grund steht in hinweise."""
    pfad = "repos/%s/actions/jobs/%s/logs" % (repo, job_id)
    try:
        return gh(pfad)
    except subprocess.CalledProcessError as e:
        hinweise.append("HINWEIS Protokoll %s: gh api Rueckgabe %d %s" % (job_id, e.returncode, (e.stderr or "").strip()[:200]))
    import urllib.error
    import urllib.request

    class OhneAnmeldung(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, req, fp, code, msg, headers, newurl):
            neu = super().redirect_request(req, fp, code, msg, headers, newurl)
            if neu is not None:
                neu.remove_header("Authorization")
            return neu

    anfrage = urllib.request.Request("https://api.github.com/" + pfad, headers={
        "Accept": "application/vnd.github+json", "User-Agent": "urteil-github",
        "Authorization": "Bearer " + os.environ.get("GH_TOKEN", "")})
    try:
        with urllib.request.build_opener(OhneAnmeldung).open(anfrage, timeout=60) as antwort:
            return antwort.read().decode("utf-8", errors="replace")
    except (urllib.error.URLError, OSError) as e:
        hinweise.append("HINWEIS Protokoll %s: direkt %s" % (job_id, e))
        return None


def aus_github(soll_liste):
    """Liest Jobs und Protokolle des laufenden Laufs ueber gh (GH_TOKEN), schreibt das Urteil in urteil_github.txt und
    in die Zusammenfassung. Nie rot: jeder Fehler ergibt "unklar" (= wie echt rot behandeln)."""
    repo, lauf, stufe = os.environ.get("GITHUB_REPOSITORY"), os.environ.get("GITHUB_RUN_ID"), os.environ.get("CI_STUFE", "schnell")
    try:
        jobs = json.loads(gh("repos/%s/actions/runs/%s/jobs?filter=latest&per_page=100" % (repo, lauf)))["jobs"]
        logs, hinweise = {}, []
        for j in jobs:
            if j.get("name") != HILFSJOB and j.get("conclusion") not in ("success", "skipped"):
                logs[j["id"]] = protokoll(repo, j["id"], hinweise)
        ergebnis, zeilen = urteilen(jobs, logs, erwartete_jobs(stufe, soll_liste))
        zeilen += hinweise
    except Exception as e:  # nie rot, aber sichtbar
        zeilen = ["URTEIL GITHUB: unklar (%s: %s), wie echt rot behandeln" % (type(e).__name__, e)]
    zeilen.insert(1 if len(zeilen) > 1 else len(zeilen), "STUFE: %s, Lauf %s, Versuch %s" % (stufe, lauf, os.environ.get("GITHUB_RUN_ATTEMPT")))
    text = "\n".join(zeilen) + "\n"
    print(text, end="")
    with open("urteil_github.txt", "w", encoding="utf-8") as f:
        f.write(text)
    zusammenfassung = os.environ.get("GITHUB_STEP_SUMMARY")
    if zusammenfassung:
        with open(zusammenfassung, "a", encoding="utf-8") as f:
            f.write("```\n" + text + "```\n")
    return 0


# ---- Selbsttest: feste Faelle mit Zeilen aus echten Laeufen (751, 748, 753) und Gegenproben ----

def _job(jid, name, schritte, schluss="failure"):
    return {"id": jid, "name": name, "conclusion": schluss,
            "steps": [{"name": n, "conclusion": c, "started_at": a, "completed_at": e} for n, c, a, e in schritte]}


def _log(*zeilen):
    return "\n".join("%s.1234567Z %s" % (t, z) for t, z in zeilen) + "\n"


T0, T1, T2, T3 = "2026-11-09T20:07:00", "2026-11-09T20:07:30", "2026-11-09T20:10:00", "2026-11-09T20:10:06"
OK = ("Werkzeuge", "success", T0, T1)
SKRIPT = [(T1, "##[group]Run bash werkzeuge/pakete/pakete_und_update.sh osx-arm64 pakete"),
          (T1, 'echo "FEHLER: das ist nur Skripttext"'), (T1, "  FEHLERBERICHT_TOKEN: ***"), (T1, "##[endgroup]")]
RATE = SKRIPT + [(T3, "FEHLER: Feed (Kanal osx) nennt nicht 1.0.0-a.0.54.0 (vA0.54.0 laut GitHub-API)"),
                 (T3, "feed: FEHLER HttpRequestException: Response status code does not indicate success: 403 (rate limit exceeded)."),
                 (T3, "##[error]Process completed with exit code 1.")]
S504 = SKRIPT + [(T2, "WIEDERHOLUNG 1/4 nach 15 s: 504 Gateway Timeout"),
                 (T3, "FEHLER: Feed (Kanal osx) nennt nicht 1.0.0-a.0.54.0 (vA0.54.0 laut GitHub-API)"),
                 (T3, "feed: FEHLER HttpRequestException: Response status code does not indicate success: 504 (Gateway Timeout)."),
                 (T3, "##[error]Process completed with exit code 1.")]
PAKET = ("Pakete bauen und Update pruefen", "failure", T1, T3)
UPLOAD = [(T3, "##[group]Run actions/upload-artifact@v7"), (T3, "##[endgroup]"),
          (T3, "##[error]Failed to CreateArtifact: Unable to make request: ENOTFOUND")]
EMU = [(T3, "FEHLER: Werkzeug n+1 meldet sich nicht (120 s)"), (T3, "##[error]Process completed with exit code 1.")]
FOLGE_LOG = [(T3, "FEHLER: pakete nicht gruen (failure), kein Release-Entwurf"), (T3, "##[error]Process completed with exit code 1.")]
ANDROID = "android (APK, nur arm64-v8a, Testsignatur)"
ZWEI = ["pakete (osx-arm64)", "tests (ubuntu-latest)"]

FAELLE = [
    # (Name, Jobs, Logs, erwartet, Ergebnis)
    ("kein Rechner (Lauf 738)", [_job(1, "tests (macos-15-intel, Metal)", [("Set up job", None, None, None)])], {},
     ["tests (macos-15-intel, Metal)"], "nur GitHub"),
    ("Abfragebremse 403 (Lauf 751)", [_job(2, "pakete (osx-arm64)", [OK, PAKET])], {2: _log(*RATE)}, ["pakete (osx-arm64)"], "nur GitHub"),
    ("504 nach Wiederholungen (Lauf 709)", [_job(3, "pakete (osx-arm64)", [OK, PAKET])], {3: _log(*S504)}, ["pakete (osx-arm64)"], "nur GitHub"),
    ("Hochladen ENOTFOUND (Lauf 748)", [_job(4, "tests (macos-15-intel, Einzeltests und Spiel)",
                                            [OK, ("Run actions/upload-artifact@v7", "failure", T3, T3)])],
     {4: _log(*UPLOAD)}, ["tests (macos-15-intel, Einzeltests und Spiel)"], "nur GitHub"),
    ("weniger Jobs als die Liste (Lauf 744)", [_job(5, "tests (ubuntu-latest)", [OK], "success")], {}, ZWEI, "nur GitHub"),
    ("Folgefehler android von pakete GitHub (Lauf 751)", [_job(6, "pakete (osx-arm64)", [OK, PAKET]),
                                                          _job(7, ANDROID, [OK, ("Release zusammenstellen und pruefen (T2, Lizenzliste)", "failure", T3, T3)])],
     {6: _log(*RATE), 7: _log(*FOLGE_LOG)}, ["pakete (osx-arm64)", ANDROID], "nur GitHub"),
    ("alles gruen", [_job(8, "tests (ubuntu-latest)", [OK], "success"), _job(9, HILFSJOB, [], None)], {}, ["tests (ubuntu-latest)"], "gruen"),
    ("Gegenprobe Emulator meldet sich nicht (Lauf 753)", [_job(10, ANDROID, [OK, ("Emulator-Probe Update", "failure", T1, T3)])],
     {10: _log(*EMU)}, [ANDROID], "echt rot"),
    ("Gegenprobe Testfehler", [_job(11, "tests (ubuntu-latest)", [OK, ("Tests", "failure", T1, T3)])],
     {11: _log((T3, "[FAIL] Lernplattform.Tests.SpielBettTests.Bett"), (T3, "Test Run Failed."))}, ["tests (ubuntu-latest)"], "echt rot"),
    ("Gegenprobe 504 nur in Wiederholung, echter Fehler danach (Lauf 714)", [_job(12, "pakete (win-x64)", [OK, PAKET])],
     {12: _log((T2, "WIEDERHOLUNG 1/4 nach 15 s: 504 Gateway Timeout"), (T3, "FEHLER: Setup-Datei heisst nicht Fachinformatiker-Lernplattform-win-Setup.exe"),
               (T3, "##[error]Process completed with exit code 1."))}, ["pakete (win-x64)"], "echt rot"),
    ("Gegenprobe 504 in anderem Schritt, Testfehler im roten Schritt",
     [_job(13, "tests (windows-latest, Einzeltests)", [("Werkzeuge", "success", T0, T1), ("Tests", "failure", T2, T3)])],
     {13: _log((T1, "FEHLER HttpRequestException: 504 (Gateway Timeout)"), (T3, "[FAIL] Lernplattform.Tests.X"),
               (T3, "##[error]Process completed with exit code 1."))}, ["tests (windows-latest, Einzeltests)"], "echt rot"),
    ("Gegenprobe Beleg und Testfehler im selben Schritt", [_job(14, "tests (ubuntu-latest)", [OK, ("Tests", "failure", T1, T3)])],
     {14: _log((T3, "FEHLER: rate limit exceeded"), (T3, "[FAIL] Lernplattform.Tests.Y"))}, ["tests (ubuntu-latest)"], "echt rot"),
    ("Gegenprobe Beleg und fremde FEHLER-Zeile", [_job(15, "pakete (osx-arm64)", [OK, PAKET])],
     {15: _log(*(RATE + [(T3, "FEHLER: Paket ohne Signatur")]))}, ["pakete (osx-arm64)"], "echt rot"),
    ("Gegenprobe Folgefehler von echt rotem pakete", [_job(16, "pakete (win-x64)", [OK, PAKET]),
                                                      _job(17, ANDROID, [OK, ("Release zusammenstellen und pruefen (T2, Lizenzliste)", "failure", T3, T3)])],
     {16: _log((T3, "FEHLER: Setup-Datei falsch"), (T3, "##[error]Process completed with exit code 1.")), 17: _log(*FOLGE_LOG)},
     ["pakete (win-x64)", ANDROID], "echt rot"),
    ("Gegenprobe Folgefehler ohne roten pakete-Job", [_job(18, ANDROID, [OK, ("Release zusammenstellen und pruefen (T2, Lizenzliste)", "failure", T3, T3)])],
     {18: _log(*FOLGE_LOG)}, [ANDROID], "echt rot"),
    ("Gegenprobe Protokoll nicht lesbar", [_job(19, "pakete (osx-arm64)", [OK, PAKET])], {19: None}, ["pakete (osx-arm64)"], "echt rot"),
    ("Gegenprobe Wachhund", [_job(20, "tests (macos-15, Einzeltests und Lernteil)", [OK, ("Start ohne Bedienung (1. Lauf)", "failure", T1, T3)])],
     {20: _log((T3, "Wachhund: Start-Test nach 14 Minuten nicht fertig, Rueckgabe 4"), (T3, "##[error]Process completed with exit code 4."))},
     ["tests (macos-15, Einzeltests und Lernteil)"], "echt rot"),
    ("Gegenprobe rot ohne roten Schritt", [_job(21, "tests (ubuntu-latest)", [OK])], {21: ""}, ["tests (ubuntu-latest)"], "echt rot"),
    ("Gegenprobe ein Job GitHub, ein Job echt", [_job(22, "pakete (osx-arm64)", [OK, PAKET]),
                                                 _job(23, "tests (ubuntu-latest)", [OK, ("Tests", "failure", T1, T3)])],
     {22: _log(*RATE), 23: _log((T3, "[FAIL] Z"))}, ZWEI, "echt rot"),
    ("Gegenprobe nur Skripttext mit Beleg", [_job(24, "pakete (osx-arm64)", [OK, PAKET])],
     {24: _log((T1, "##[group]Run x"), (T1, "echo FEHLER rate limit exceeded"), (T1, "##[endgroup]"),
               (T3, "FEHLER: Paket kaputt"), (T3, "##[error]Process completed with exit code 1."))}, ["pakete (osx-arm64)"], "echt rot"),
]


def selbsttest():
    rot = 0
    for name, jobs, logs, erwartet, soll in FAELLE:
        ist, zeilen = urteilen(jobs, logs, erwartet)
        if ist != soll:
            print("\n".join(zeilen))
            print("FEHLER: Selbsttest GitHub-Urteil Fall '%s': %s statt %s" % (name, ist, soll))
            rot = 1
    print("Selbsttest GitHub-Urteil: %d Faelle (davon %d Gegenproben echt rot), %s"
          % (len(FAELLE), sum(1 for f in FAELLE if f[4] == "echt rot"), "rot" if rot else "gruen"))
    return rot
