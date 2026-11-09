#!/usr/bin/env bash
# S 0.25.1 (Sammelliste 249/250, Weisung P12-107 Punkte 2 und 3): Probe des Werkzeug-Updates am PC gegen das ECHTE
# GitHub. Nur Windows in der CI (Job "pakete (stadtbau win-x64)", aufgerufen aus baue_stadtbau.sh win-x64 im Workflow CI).
# Aufruf: update_probe_pc.sh <pub-Ordner des Baus> <Arbeitsordner>
# Ablauf:
#   1 lesend: neueste Version im Feed der Linie vS (Kanal stadtbau-win) und der Lernplattform (vA, Kanal win), je gleich
#     der hoechsten veroeffentlichten Version laut GitHub-API (die Lernplattform muss also auch von vA0.52.1 aus die
#     neueste finden).
#   2 der eben gebaute Stand als aeltere Version 0.0.1 gepackt und mit Setup.exe --silent installiert (eine echte alte
#     Version vS0.24.0/vS0.25.0 kann sich wegen Fund 249 nicht selbst aktualisieren, sie hat genau den Fehler).
#   3 alter Datenordner: eine Projekt-Datei in %LOCALAPPDATA%\Fachinformatiker-Stadtbau\projekte (= Installationsordner,
#     so lag es bis S 0.25.0).
#   4 das installierte Werkzeug sucht wie "Nach Updates suchen" ueber LinienQuelle, laedt und spielt ein; Erwartung:
#     Angebot = neueste vS, danach current\Stadtbau.exe mit dieser Version, Projekt in %APPDATA% kopiert, im alten Ordner
#     unveraendert, update.log in %APPDATA% mit Umzug, Velopack-Zeilen und Probe-Zeile.
#   5 Deinstallation: die Daten in %APPDATA% bleiben (Gegenprobe zu Fund 250).
# Rot (Rueckgabe 1) mit Zeilen FEHLER: und dem ganzen Bericht und update.log.
set -Eeuo pipefail
trap 'echo "FEHLER: Abbruch in Zeile $LINENO (Rueckgabe $?): $BASH_COMMAND"' ERR
PUB="$1"
ARB="$2"
ID="Fachinformatiker-Stadtbau"
KANAL="stadtbau-win"
PY="${PYTHON:-python}"
if [ "${CI:-}" != "true" ]; then echo "FEHLER: die Probe installiert das Werkzeug und laeuft nur in der CI (CI=true)"; exit 1; fi
mkdir -p "$ARB"
ALT="$(cygpath -u "$LOCALAPPDATA")/$ID"
NEU="$(cygpath -u "$APPDATA")/$ID"
if [ -e "$ALT" ] || [ -e "$NEU" ]; then echo "FEHLER: $ALT oder $NEU gibt es schon (keine frische Probe)"; exit 1; fi
BERICHT="$ARB/bericht.txt"
export STADTBAU_UPDATE_PROBE_BERICHT="$(cygpath -w "$BERICHT")"

zeigen() {
  echo "--- Bericht ---"; cat "$BERICHT" 2>/dev/null || true
  echo "--- update.log (neuer Ordner) ---"; cat "$NEU/update.log" 2>/dev/null || true
  echo "--- fehler.log (neuer Ordner) ---"; cat "$NEU/fehler.log" 2>/dev/null || true
}
fehler() { echo "FEHLER: $1"; zeigen; exit 1; }

# hoechste veroeffentlichte Version je Linie laut GitHub-API (ohne Entwurf und Vorab)
"$PY" -I - "$ARB/soll.txt" <<'PY'
import json, re, sys, time, urllib.error, urllib.request
r = urllib.request.Request("https://api.github.com/repos/papst2142-cmd/Fachinformatiker-Lernplattform/releases?per_page=100",
                           headers={"Accept": "application/vnd.github+json", "User-Agent": "stadtbau-update-probe"})
# Stoerung bei GitHub (5xx wie 504, keine Antwort): bis zu 4 Versuche wie Updatequelle.Wiederholen, 4xx sofort rot
for versuch, pause in enumerate([15, 30, 60, None], 1):
    try:
        daten = json.load(urllib.request.urlopen(r, timeout=60))
        break
    except urllib.error.HTTPError as x:
        if x.code < 500 or pause is None:
            raise
        grund = "HTTP %d" % x.code
    except (urllib.error.URLError, TimeoutError) as x:
        if pause is None:
            raise
        grund = str(x)
    print("WIEDERHOLUNG GitHub-API Versuch %d nach %d s (%s)" % (versuch + 1, pause, grund), flush=True)
    time.sleep(pause)
def hoechste(praefix):
    v = [tuple(int(x) for x in m.groups()) for d in daten if not d.get("draft") and not d.get("prerelease")
         for m in [re.fullmatch(praefix + r"(\d+)\.(\d+)\.(\d+)", d.get("tag_name", ""))] if m]
    return ".".join(map(str, max(v))) if v else ""
with open(sys.argv[1], "w") as f:
    f.write("werkzeug %s\nlernplattform %s\n" % (hoechste("vS"), hoechste("vA")))
PY
SOLL_S="$(sed -n 's/^werkzeug //p' "$ARB/soll.txt")"
SOLL_A="$(sed -n 's/^lernplattform //p' "$ARB/soll.txt")"
echo "GitHub: neueste vS $SOLL_S, neueste vA $SOLL_A"
if [ -z "$SOLL_S" ] || [ -z "$SOLL_A" ]; then fehler "keine veroeffentlichte Version vS oder vA gefunden"; fi

# 1 lesend (eigener Datenordner, nichts installiert)
STADTBAU_DATEN="$(cygpath -w "$ARB/daten_feed")" STADTBAU_UPDATE_PROBE=feed "$PUB/Stadtbau.exe" || fehler "Feed-Probe Rueckgabe $?"
tr -d '\r' < "$BERICHT" | grep "^WIEDERHOLUNG" || true
grep -qx "FEED werkzeug $SOLL_S" <(tr -d '\r' < "$BERICHT") || fehler "Feed der Linie vS nennt nicht $SOLL_S"
grep -qx "FEED lernplattform 1.0.0-a.$SOLL_A" <(tr -d '\r' < "$BERICHT") || fehler "Feed der Lernplattform nennt nicht 1.0.0-a.$SOLL_A (Paketversion von vA$SOLL_A)"
echo "Probe 1: Feed vS $SOLL_S und vA $SOLL_A gefunden"

# 2 aeltere Version 0.0.1 aus dem eben gebauten Stand
vpk pack -u "$ID" -v 0.0.1 -p "$PUB" -e Stadtbau.exe --packTitle "Fachinformatiker Stadtbau" -c "$KANAL" -o "$ARB/feed"
"$ARB/feed/$ID-$KANAL-Setup.exe" --silent || fehler "Setup.exe Rueckgabe $?"
sleep 5
# Setup startet das Werkzeug nach der Installation; fuer die Probe ohne Fenster beenden
taskkill //IM Stadtbau.exe //F >/dev/null 2>&1 || true
sleep 2
test -f "$ALT/current/Stadtbau.exe" || fehler "Setup hat nicht nach $ALT installiert"
echo "Probe 2: 0.0.1 installiert nach $ALT"

# 3 Daten im alten Ordner wie bis S 0.25.0
mkdir -p "$ALT/projekte" "$ALT/blueprints"
printf 'probe-projekt\n' > "$ALT/projekte/probe_alt.fisbau"
printf 'probe-blueprint\n' > "$ALT/blueprints/probe_alt.fisblueprint"

# 4 Update gegen das echte GitHub
: > "$BERICHT"
env -u STADTBAU_DATEN STADTBAU_UPDATE_PROBE=update "$ALT/current/Stadtbau.exe" || fehler "Update-Probe Rueckgabe $?"
tr -d '\r' < "$BERICHT"
grep -qx "INSTALLIERT 0.0.1" <(tr -d '\r' < "$BERICHT") || fehler "installierte Version nicht 0.0.1"
grep -qx "ANGEBOT $SOLL_S" <(tr -d '\r' < "$BERICHT") || fehler "kein Angebot $SOLL_S"
grep -q "^EINSPIELEN" <(tr -d '\r' < "$BERICHT") || fehler "Einspielen nicht gestartet"
ist=""
for _ in $(seq 1 45); do
  ist="$(powershell -NoProfile -Command "(Get-Item '$(cygpath -w "$ALT/current/Stadtbau.exe")').VersionInfo.ProductVersion" 2>/dev/null | tr -d '\r' | sed 's/+.*//')" || true
  if [ "$ist" = "$SOLL_S" ]; then break; fi
  sleep 4
done
[ "$ist" = "$SOLL_S" ] || fehler "nach dem Update ist current\\Stadtbau.exe Version '$ist' statt $SOLL_S"
echo "Probe 4: Update auf $SOLL_S eingespielt (current\\Stadtbau.exe $ist)"
taskkill //IM Stadtbau.exe //F >/dev/null 2>&1 || true
cmp -s "$ALT/projekte/probe_alt.fisbau" <(printf 'probe-projekt\n') || fehler "Projekt im alten Ordner veraendert oder weg"
cmp -s "$NEU/projekte/probe_alt.fisbau" <(printf 'probe-projekt\n') || fehler "Projekt nicht in den neuen Datenordner kopiert"
cmp -s "$NEU/blueprints/probe_alt.fisblueprint" <(printf 'probe-blueprint\n') || fehler "Blueprint nicht in den neuen Datenordner kopiert"
grep -q "Datenordner neu" "$NEU/update.log" || fehler "keine Zeile zum Umzug in update.log"
grep -q "Velopack" "$NEU/update.log" || fehler "keine Velopack-Zeilen in update.log"
grep -q "Probe: Version $SOLL_S geladen" "$NEU/update.log" || fehler "keine Probe-Zeile in update.log"
echo "Probe 3: Daten kopiert, alter Ordner unveraendert, update.log im neuen Ordner"

# 5 Deinstallation: Daten im neuen Ordner bleiben
"$ALT/Update.exe" --silent uninstall || echo "Update.exe Rueckgabe $?"
for _ in $(seq 1 30); do [ -e "$ALT/current/Stadtbau.exe" ] || break; sleep 2; done
[ ! -e "$ALT/current/Stadtbau.exe" ] || fehler "Deinstallation hat $ALT\\current nicht entfernt"
cmp -s "$NEU/projekte/probe_alt.fisbau" <(printf 'probe-projekt\n') || fehler "Projekt nach der Deinstallation weg"
echo "Probe 5: nach der Deinstallation liegen die Daten noch in %APPDATA%"
zeigen
echo "Update-Probe PC: alles gut (vS $SOLL_S, vA $SOLL_A)"
