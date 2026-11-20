"""Reserve der Pruefschritte bis zu ihrer Zeitgrenze (Weisung P12-324, Nico 1a 2a 3a 4c).

Laeuft im Hinweis-Job "urteil (GitHub oder echt)" und ist nie rot (Rueckgabe immer 0, aendert kein Urteil):

  python -I werkzeuge/ci/reserve.py github      Tabelle dieses Laufs nach reserve.txt (Artefakt reserve-<Versuch>)
  python -I werkzeuge/ci/reserve.py selbsttest  feste Faelle mit Gegenproben

Inhalt von reserve.txt:
  T1  je Job und Pruefschritt aus werkzeuge/ci/zeitlimits.json: Dauer, Grenze, Reserve in Prozent; unter 25 % eine
      Zeile "WARNUNG Reserve" (Grenze = Wachhund = Schrittzeit minus 1 Minute, unter Windows ohne Wachhund die
      Schrittzeit selbst).
  T2  Vorschlaege fuer hoehere Zeitlimits aus den letzten 5 gruenen vollen Laeufen (Regel in zeitlimits.py).
  T3  fuer jeden Schritt unter 25 %: die laengsten Abschnitte (ZEITPLAN-Zeilen) und ein Verschiebe-Vorschlag in den
      Start-Test-Schritt desselben Jobs mit der meisten Reserve.
  T4c fuer einmalige Messpunkte, die hoechstens 10 % ueber ihrer Grenze rot sind: "INFO main: <Messpunkt> <Wert> ms,
      main <Wert> ms" mit dem Wert desselben Jobs im juengsten gruenen vollen Lauf auf dem Stand von main. Nur Info.
"""
import datetime
import json
import math
import os
import re
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
sys.path.insert(0, os.path.join(HIER, "..", "starttest"))
import zeitlimits  # noqa: E402
import urteil_github  # noqa: E402

VOLL_JOBS = 18          # voller Lauf: 18 Jobs ohne den Hinweis-Job
INFO_SPANNE = 1.10      # T4c: hoechstens 10 % ueber der Grenze
ZEITPLAN = re.compile(r"^ZEITPLAN (\d+(?:\.\d+)?) s: (.+)$")
EINMALIG = re.compile(r"^FEHLER: (.+?): (\d+(?:\.\d+)?) ms ueber Grenze (\d+) ms;.*\(einmaliger Messpunkt, nicht wiederholt\)")
START_TEST = ("Start ohne Bedienung (1. Lauf", "Spielansichten ohne Bedienung (1. Lauf", "Testkarte Nachladen ohne Bedienung (1. Lauf")


def dauer(schritt):
    a, e = urteil_github.zeit(schritt.get("started_at")), urteil_github.zeit(schritt.get("completed_at"))
    return (e - a).total_seconds() if a and e else None


def tabelle(jobs, daten):
    """[(job, schritt, dauer, grenze, reserve_prozent)] fuer jeden gelaufenen Pruefschritt aus zeitlimits.json."""
    eintraege = {e["schritt"]: e for e in daten["schritte"]}
    raus = []
    for j in sorted(jobs, key=lambda j: j.get("name", "")):
        name = j.get("name", "")
        for s in j.get("steps") or []:
            e = eintraege.get(s.get("name"))
            d = dauer(s)
            if e is None or d is None or s.get("conclusion") in (None, "skipped"):
                continue
            g = zeitlimits.grenze_sekunden(e, name, "windows" in name)
            raus.append((name, s["name"], d, g, math.floor((g - d) * 100 / g)))
    return raus


def tabelle_zeilen(zeilen):
    text = ["RESERVE: Job | Schritt | Dauer s | Grenze s | Reserve %"]
    for job, schritt, d, g, r in zeilen:
        text.append("RESERVE: %s | %s | %.0f | %d | %d" % (job, schritt, d, g, r))
    for job, schritt, d, g, r in zeilen:
        if r < zeitlimits.WARNGRENZE:
            text.append("WARNUNG Reserve: %s, %s: %.0f s von %d s, nur %d %% (unter %d %%)" % (
                job, schritt, d, g, r, zeitlimits.WARNGRENZE))
    return text


def abschnitte(log, schritt):
    """[(abschnitt, sekunden)] aus den ZEITPLAN-Zeilen eines Schritts; der letzte Abschnitt laeuft bis zum Schrittende."""
    marken = []
    for z in urteil_github.schrittzeilen(log, schritt):
        m = ZEITPLAN.match(z)
        if m:
            marken.append((float(m.group(1)), m.group(2)))
    raus = []
    for i, (t, name) in enumerate(marken):
        if i + 1 < len(marken):
            raus.append((name, marken[i + 1][0] - t))
        else:
            start = urteil_github.zeit(schritt.get("started_at"))
            rest = (dauer(schritt) or 0) - t
            raus.append((name, max(rest, 0.0) if start else 0.0))
    return raus


def verschieben(zeilen, job, quelle, teile):
    """T3: groesste Abschnitte zuerst aus dem Schritt quelle in den Start-Test-Schritt desselben Jobs mit der meisten
    Reserve, bis die Quelle 25 % Reserve hat, solange das Ziel ueber 25 % bleibt. [Zeilen]."""
    werte = {s: (d, g) for j, s, d, g, r in zeilen if j == job}
    if quelle not in werte:
        return []
    ziele = [s for s in werte if s != quelle and s.startswith(START_TEST)]
    if not ziele:
        return ["VORSCHLAG Verschieben: %s, %s: kein Start-Test-Schritt als Ziel im Job" % (job, quelle)]
    ziel = max(ziele, key=lambda s: (werte[s][1] - werte[s][0]) / werte[s][1])
    qd, qg = werte[quelle]
    zd, zg = werte[ziel]
    raus = []
    for name, sek in sorted(teile, key=lambda t: -t[1]):
        if (qg - qd) * 100 / qg >= zeitlimits.WARNGRENZE:
            break
        if (zg - zd - sek) * 100 / zg < zeitlimits.WARNGRENZE:
            continue
        qd, zd = qd - sek, zd + sek
        raus.append("VORSCHLAG Verschieben: %s (%.0f s) aus %s nach %s, %s; Reserve danach %d %% und %d %%" % (
            name, sek, quelle, ziel, job, math.floor((qg - qd) * 100 / qg), math.floor((zg - zd) * 100 / zg)))
    return raus or ["VORSCHLAG Verschieben: %s, %s: kein Abschnitt passt in %s" % (job, quelle, ziel)]


def einmalig_knapp(log):
    """T4c: {Messpunkt: Wert} der einmaligen Messpunkte, die hoechstens 10 % ueber ihrer Grenze rot sind."""
    raus = {}
    for roh in log.splitlines():
        z = urteil_github.FARBE.sub("", urteil_github.ZEIT.sub("", roh)).strip()
        m = EINMALIG.match(z)
        if m and float(m.group(2)) <= INFO_SPANNE * int(m.group(3)):
            raus[m.group(1)] = float(m.group(2))
    return raus


def wert_in(log, punkt):
    """Wert eines Messpunkts in einem Protokoll: gruene Zeile "<punkt>: <n> ms" oder rote FEHLER-Zeile; sonst None."""
    gruen = re.compile(r"(?:^|\s)" + re.escape(punkt) + r": (\d+(?:\.\d+)?) ms(?:\s|$)")
    rot = re.compile(r"^FEHLER: " + re.escape(punkt) + r": (\d+(?:\.\d+)?) ms ueber Grenze")
    for roh in log.splitlines():
        z = urteil_github.FARBE.sub("", urteil_github.ZEIT.sub("", roh)).strip()
        m = rot.match(z) or gruen.search(z)
        if m:
            return float(m.group(1))
    return None


def info_main(punkte, log_main, job):
    return ["INFO main: %s %g ms, main %s ms (%s, nur Info, aendert kein Urteil)" % (
        p, w, "?" if (v := wert_in(log_main, p) if log_main else None) is None else "%g" % v, job) for p, w in sorted(punkte.items())]


def dauern_aus(laeufe):
    """T2: {(schritt, schluessel): {job: [sekunden, aelteste zuerst]}} aus [(lauf, jobs)] (neueste zuerst)."""
    raus = {}
    for _, jobs in reversed(laeufe):
        for j in jobs:
            for s in j.get("steps") or []:
                d = dauer(s)
                if d is not None and s.get("conclusion") == "success":
                    raus.setdefault((s["name"], zeitlimits.schluessel(j["name"])), {}).setdefault(j["name"], []).append(d)
    return raus


def voll(jobs):
    return len([j for j in jobs if j.get("name") != urteil_github.HILFSJOB]) >= VOLL_JOBS


def aus_github():
    repo, lauf = os.environ.get("GITHUB_REPOSITORY"), os.environ.get("GITHUB_RUN_ID")
    zeilen = ["STUFE: %s, Lauf %s, Versuch %s" % (os.environ.get("CI_STUFE", "?"), lauf, os.environ.get("GITHUB_RUN_ATTEMPT"))]
    try:
        daten = zeitlimits.laden()
        hole = lambda pfad: json.loads(urteil_github.gh(pfad))  # noqa: E731
        jobs = hole("repos/%s/actions/runs/%s/jobs?filter=latest&per_page=100" % (repo, lauf))["jobs"]
        tab = tabelle(jobs, daten)
        zeilen += tabelle_zeilen(tab)
        hinweise, logs = [], {}

        def log(jid):
            if jid not in logs:
                logs[jid] = urteil_github.protokoll(repo, jid, hinweise) or ""
            return logs[jid]

        # T3: Abschnitte und Verschiebe-Vorschlag fuer jeden Start-Test-Schritt unter 25 %
        for j in jobs:
            for s in j.get("steps") or []:
                knapp = [r for jn, sn, d, g, r in tab if jn == j["name"] and sn == s.get("name") and r < zeitlimits.WARNGRENZE]
                if knapp and s["name"].startswith(START_TEST):
                    teile = abschnitte(log(j["id"]), s)
                    for name, sek in sorted(teile, key=lambda t: -t[1])[:5]:
                        zeilen.append("ABSCHNITT: %s, %s: %s %.0f s" % (j["name"], s["name"], name, sek))
                    zeilen += verschieben(tab, j["name"], s["name"], teile)
        # T2 und T4c brauchen fruehere Laeufe: juengste gruene Laeufe von ci.yml
        laeufe = []
        for r in hole("repos/%s/actions/workflows/ci.yml/runs?status=success&per_page=30" % repo)["workflow_runs"]:
            if str(r["id"]) == str(lauf):
                continue
            rjobs = hole("repos/%s/actions/runs/%s/jobs?filter=latest&per_page=100" % (repo, r["id"]))["jobs"]
            if voll(rjobs):
                laeufe.append((r, rjobs))
            if len(laeufe) >= zeitlimits.ANZAHL_LAEUFE:
                break
        vorschlaege = zeitlimits.vorschlag(daten, dauern_aus(laeufe))
        zeilen.append("ZEITLIMITS: Grundlage %d gruene volle Laeufe (%s)" % (len(laeufe), ", ".join(str(r["run_number"]) for r, _ in laeufe)))
        zeilen += [zeitlimits.vorschlag_zeile(*v) for v in vorschlaege] or ["ZEITLIMITS: kein Vorschlag (alle Limits reichen nach der Regel 1,5 x Median)"]
        # T4c: knappe einmalige Messpunkte gegen main
        knapp = {}
        for j in jobs:
            if j.get("conclusion") == "failure" and j["name"].startswith("tests"):
                for p, w in einmalig_knapp(log(j["id"])).items():
                    knapp.setdefault(j["name"], {})[p] = w
        if knapp:
            baum_main = hole("repos/%s/commits/main" % repo)["commit"]["tree"]["sha"]
            bezug = None
            for r, rjobs in laeufe:
                if hole("repos/%s/commits/%s" % (repo, r["head_sha"]))["commit"]["tree"]["sha"] == baum_main:
                    bezug = (r, rjobs)
                    break
            for jname, punkte in sorted(knapp.items()):
                mjob = next((j for j in bezug[1] if j["name"] == jname), None) if bezug else None
                zeilen += info_main(punkte, log(mjob["id"]) if mjob else "", jname)
            zeilen.append("INFO main: Bezug %s" % ("Lauf %s (Kopf %s)" % (bezug[0]["run_number"], bezug[0]["head_sha"][:7]) if bezug
                                                   else "kein gruener voller Lauf auf dem Stand von main unter den letzten"))
        zeilen += hinweise
    except Exception as e:  # nie rot, aber sichtbar
        zeilen.append("RESERVE: unklar (%s: %s)" % (type(e).__name__, e))
    text = "\n".join(zeilen) + "\n"
    print(text, end="")
    with open("reserve.txt", "w", encoding="utf-8") as f:
        f.write(text)
    zusammenfassung = os.environ.get("GITHUB_STEP_SUMMARY")
    if zusammenfassung:
        with open(zusammenfassung, "a", encoding="utf-8") as f:
            f.write("```\n" + text + "```\n")
    return 0


# ---- Selbsttest ----

def _schritt(name, a, e, schluss="success"):
    return {"name": name, "conclusion": schluss, "started_at": "2026-11-20T10:%s:00Z" % a, "completed_at": "2026-11-20T10:%s:00Z" % e}


def selbsttest():
    fehler = []

    def soll(bedingung, text):
        if not bedingung:
            fehler.append(text)

    daten = zeitlimits.laden()
    sp1 = "Spielansichten ohne Bedienung (1. Lauf, frischer Datenordner)"
    tk1 = "Testkarte Nachladen ohne Bedienung (1. Lauf, frischer Datenordner)"
    ub = {"id": 1, "name": "tests (ubuntu-latest)", "conclusion": "success", "steps": [
        _schritt(sp1, "00", "08"),             # 480 s von 540 s: 11 % -> Warnung
        _schritt(tk1, "10", "20"),             # 600 s von 840 s: 28 %
        _schritt("Tests", "20", "21", "skipped"),
        _schritt("Fremder Schritt", "21", "59")]}
    win = {"id": 2, "name": "tests (windows-latest, Lernteil und Spiel)", "conclusion": "success", "steps": [
        _schritt(sp1, "00", "08")]}            # Windows ohne Wachhund: 480 von 600 s = 20 %
    intel = {"id": 3, "name": "tests (macos-15-intel, Einzeltests und Spiel)", "conclusion": "success", "steps": [
        _schritt("Tests", "00", "50")]}        # 3000 s von 3600 s = 16 %
    tab = tabelle([ub, win, intel], daten)
    soll([(j, s, d, g, r) for j, s, d, g, r in tab] == [
        ("tests (macos-15-intel, Einzeltests und Spiel)", "Tests", 3000.0, 3600, 16),
        ("tests (ubuntu-latest)", sp1, 480.0, 540, 11),
        ("tests (ubuntu-latest)", tk1, 600.0, 840, 28),
        ("tests (windows-latest, Lernteil und Spiel)", sp1, 480.0, 600, 20)], "Tabelle %s" % tab)
    warn = [z for z in tabelle_zeilen(tab) if z.startswith("WARNUNG")]
    soll(len(warn) == 3 and not any(tk1 in z for z in warn), "Warnungen %s" % warn)
    # Gegenprobe: 25 % genau ist keine Warnung, 24 % ist eine
    soll(not [z for z in tabelle_zeilen([("j", "s", 75.0, 100, 25)]) if z.startswith("WARNUNG")], "25 % warnt")
    soll([z for z in tabelle_zeilen([("j", "s", 76.0, 100, 24)]) if z.startswith("WARNUNG")], "24 % warnt nicht")
    # T3: Abschnitte aus ZEITPLAN-Zeilen im Zeitfenster des Schritts, der letzte bis zum Schrittende
    log = "\n".join([
        "2026-11-20T09:59:00.1Z ZEITPLAN 0.0 s: Vorher",
        "2026-11-20T10:00:10.1Z ZEITPLAN 0.0 s: SpielMessen",
        "2026-11-20T10:01:00.1Z ZEITPLAN 50.0 s: Mitfahrt",
        "2026-11-20T10:03:00.1Z ZEITPLAN 170.0 s: LayoutAsync",
        "2026-11-20T10:07:00.1Z ZEITPLAN 410.0 s: Ende"])
    teile = abschnitte(log, ub["steps"][0])
    soll(teile == [("SpielMessen", 50.0), ("Mitfahrt", 120.0), ("LayoutAsync", 240.0), ("Ende", 70.0)], "Abschnitte %s" % teile)
    vor = verschieben(tab, "tests (ubuntu-latest)", sp1, teile)
    # Ziel 600/840: kein Abschnitt passt, ohne dass das Ziel unter 25 % faellt
    soll(vor and all("LayoutAsync" not in z for z in vor), "Verschieben nimmt zu Grosses %s" % vor)
    soll(all("Reserve danach" not in z or int(z.rsplit(" und ", 1)[1].split(" ")[0]) >= 25 for z in vor), "Ziel unter 25 %% %s" % vor)
    vor2 = verschieben([("j", sp1, 480.0, 540, 11), ("j", tk1, 300.0, 840, 64)], "j", sp1, teile)
    soll(len(vor2) == 1 and "LayoutAsync" in vor2[0] and "danach 55 % und 35 %" in vor2[0], "Verschieben gross %s" % vor2)
    # Gegenprobe: ohne Start-Test-Ziel kein Vorschlag mit Reserve
    soll("kein Start-Test-Schritt" in verschieben([("j", sp1, 480.0, 540, 11), ("j", "Tests", 1.0, 1800, 99)], "j", sp1, teile)[0],
         "Ziel Tests")
    # T4c: nur einmalige Messpunkte bis 10 % ueber der Grenze; Wert in main aus gruener oder roter Zeile
    zweig = "\n".join([
        "2026-11-20T10:00:00.1Z FEHLER: Spiel: Kaufhaus Sortiment (erstes Oeffnen): 430 ms ueber Grenze 400 ms; "
        "Einzelmessungen 430 ms (einmaliger Messpunkt, nicht wiederholt)",
        "FEHLER: Inhalte laden: 441 ms ueber Grenze 400 ms; Einzelmessungen 441 ms (einmaliger Messpunkt, nicht wiederholt)",
        "FEHLER: Lernen: 500 ms ueber Grenze 400 ms; Einzelmessungen 500, 480, 470 ms"])
    knapp = einmalig_knapp(zweig)
    soll(knapp == {"Spiel: Kaufhaus Sortiment (erstes Oeffnen)": 430}, "knapp %s" % knapp)
    main_log = "2026-11-19T10:00:00.1Z   Spiel: Kaufhaus Sortiment (erstes Oeffnen): 395 ms"
    soll(info_main(knapp, main_log, "tests (ubuntu-latest)") ==
         ["INFO main: Spiel: Kaufhaus Sortiment (erstes Oeffnen) 430 ms, main 395 ms (tests (ubuntu-latest), nur Info, aendert kein Urteil)"],
         "INFO main %s" % info_main(knapp, main_log, "x"))
    soll("main ? ms" in info_main(knapp, "", "x")[0], "ohne main")
    # T2: Dauern je Schritt und Systemschluessel, aelteste zuerst; nur gruene Schritte
    l1 = ({"id": 9}, [{"name": "tests (ubuntu-latest)", "steps": [_schritt(sp1, "00", "08"), _schritt(tk1, "10", "11", "failure")]}])
    l2 = ({"id": 8}, [{"name": "tests (ubuntu-latest)", "steps": [_schritt(sp1, "00", "07")]}])
    d = dauern_aus([l1, l2])
    soll(d == {(sp1, "standard"): {"tests (ubuntu-latest)": [420.0, 480.0]}}, "dauern %s" % d)
    soll(voll([{"name": str(i)} for i in range(18)]) and not voll([{"name": str(i)} for i in range(10)] + [{"name": urteil_github.HILFSJOB}]),
         "voll")
    # ci.yml: der Hinweis-Job ruft reserve.py auf und laedt reserve.txt als Artefakt reserve-<Versuch> hoch
    with open(zeitlimits.CI, encoding="utf-8") as f:
        ci = f.read()
    block = ci[ci.find("name: " + urteil_github.HILFSJOB):]
    soll("python -I werkzeuge/ci/reserve.py github" in block and "name: reserve-${{ github.run_attempt }}" in block
         and "path: reserve.txt" in block, "ci.yml: Hinweis-Job ohne reserve.py oder Artefakt reserve-<Versuch>")
    for f in fehler:
        print("FEHLER: Selbsttest Reserve: " + f)
    print("Selbsttest Reserve: %s" % ("rot" if fehler else "gruen"))
    return 1 if fehler else 0


def main():
    sys.stdout.reconfigure(newline="\n")
    if len(sys.argv) == 2 and sys.argv[1] == "github":
        return aus_github()
    if len(sys.argv) == 2 and sys.argv[1] == "selbsttest":
        return selbsttest()
    print(__doc__)
    return 2


if __name__ == "__main__":
    os.chdir(os.path.join(HIER, "..", ".."))
    sys.exit(main())
