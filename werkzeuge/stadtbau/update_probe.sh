#!/usr/bin/env bash
# S 0.6.0 (Weisung P12-42 Punkt 2): Emulator-Probe des Updates im Stadtbau-Werkzeug am Tablet, nur in der CI (Job android,
# im selben Emulator nach der Probe der Lernplattform, siehe werkzeuge/android/emulator_proben.sh). Zwei Probe-APKs
# (-p:UpdateProbe=true, x86_64, Testschluessel dieses Laufs) mit den Versionen n und n+1. Ein kleiner Testserver auf dem
# Runner spielt die GitHub-Antwort "Liste der Releases" nach: zuerst ein NEUERES Release der Lernplattform (vA, mit APK),
# dann das Werkzeug-Release vS n+1 (Datei, Groesse, digest von GitHub und die Pruefsummen-Tabelle im Release-Text).
# Das Werkzeug sucht beim Start selbst, laedt, prueft und uebergibt die APK an Android (echter Ablauf
# TabletUpdate.UebergebenAsync, siehe src/Stadtbau.Android/UpdateProbe.cs).
#   Schritt 1: n per adb installiert, Update auf n+1 (das vA-Release darf nie gewaehlt werden).
#   Schritt 2: Start von n+1 liest update.log; die Liste ist gleich, ein Angebot gibt es nicht mehr ("KEIN ANGEBOT"):
#              Gegenprobe im echten Werkzeug, dass das neuere Release der Lernplattform nie angeboten wird.
# Fragt Android zurueck ("Aktualisieren?", Status -1), tippt die Probe auf den Knopf und haelt das fest.
# Rot (Rueckgabe 1) wenn: n+1 kommt nicht an, ein Angebot nennt die Lernplattform, update.log nennt keinen fertigen Download
# und keinen gestarteten Installer, oder n+1 bietet noch etwas an. Aufruf: update_probe.sh <apk_n> <apk_n1> <ergebnisdatei>
set -u
APK0=$1; APK1=$2; ERGEBNIS=$3
PORT=8766
SERVER=$(mktemp -d)
LOG=$(mktemp)
: > "$ERGEBNIS"
zeile() { echo "$*" | tee -a "$ERGEBNIS"; }

BT=$(ls -d "$ANDROID_HOME"/build-tools/* | sort -V | tail -n 1)
badging() { "$BT/aapt2" dump badging "$1" > "$SERVER/badging.txt"; head -n 1 "$SERVER/badging.txt"; }
PAKET=$(badging "$APK0" | sed -nE "s/^package: name='([^']+)'.*/\1/p")
code() { badging "$1" | sed -nE "s/.* versionCode='([0-9]+)'.*/\1/p"; }
version() { badging "$1" | sed -nE "s/.* versionName='([^']+)'.*/\1/p"; }
C0=$(code "$APK0"); C1=$(code "$APK1")
zeile "Paket $PAKET, Versionscodes $C0 -> $C1"
case "$PAKET" in io.github.papst2142cmd.stadtbau) ;; *) zeile "FEHLER: Paketname nicht gelesen ($PAKET)"; exit 1;; esac
[ -n "$C0" ] && [ -n "$C1" ] && [ "$C0" -lt "$C1" ] || { zeile "FEHLER: Versionscodes nicht gelesen"; exit 1; }

python3 -I -m http.server "$PORT" --bind 0.0.0.0 --directory "$SERVER" > "$SERVER/../server_$PORT.log" 2>&1 &
SERVER_PID=$!
trap 'kill $SERVER_PID 2>/dev/null; rm -rf "$SERVER" "$LOG"' EXIT

anbieten() {   # $1 = APK des Werkzeugs, die das Werkzeug beim naechsten Start findet
  local apk=$1 ver name
  ver=$(version "$apk")            # Anzeige wie "S 0.6.1"
  ver=${ver// /}                    # Tag-Form "S0.6.1"
  name="Fachinformatiker-Stadtbau-${ver:0:1}-${ver:1}-android-arm64.apk"
  rm -f "$SERVER"/*.apk
  cp "$apk" "$SERVER/$name"
  # Lockvogel: ein neueres Release der Lernplattform mit einer APK (dieselbe Datei, also auch Signatur und Paket des
  # Werkzeugs): nur die Linie entscheidet, dass es nie gewaehlt wird
  cp "$apk" "$SERVER/Fachinformatiker-Lernplattform-A-9.9.9-android-arm64.apk"
  python3 -I - "$SERVER" "$name" "$ver" "$PORT" <<'PY'
import hashlib, json, os, sys
ordner, name, ver, port = sys.argv[1:5]
def release(tag, datei):
    daten = open(os.path.join(ordner, datei), "rb").read()
    sha = hashlib.sha256(daten).hexdigest()
    text = ("Emulator-Probe\n\n## Prüfsummen (SHA-256)\n\n| Datei | Größe | SHA-256 |\n|---|---|---|\n"
            f"| `{datei}` | {len(daten)} | `{sha}` |\n")
    return {"tag_name": tag, "draft": False, "prerelease": False, "html_url": f"http://10.0.2.2:{port}/",
            "body": text, "assets": [{"name": datei, "size": len(daten), "digest": "sha256:" + sha,
                                      "browser_download_url": f"http://10.0.2.2:{port}/{datei}"}]}
liste = [release("vA9.9.9", "Fachinformatiker-Lernplattform-A-9.9.9-android-arm64.apk"), release("v" + ver, name)]
with open(os.path.join(ordner, "releases.json"), "w", encoding="utf-8") as f:
    json.dump(liste, f, ensure_ascii=False)
PY
  zeile "Angebot: Version $ver ($name), davor vA9.9.9 der Lernplattform"
}

installiert() { adb shell dumpsys package "$PAKET" | grep -m 1 -oE "versionCode=[0-9]+" | cut -d= -f2; }

starten() {
  adb logcat -c
  adb shell am force-stop "$PAKET" > /dev/null 2>&1 || true
  adb shell monkey -p "$PAKET" -c android.intent.category.LAUNCHER 1 > /dev/null 2>&1
}

tippen() {   # Knopf im Fenster von Android ("Update", "Aktualisieren", "Installieren")
  adb shell uiautomator dump /sdcard/ui.xml > /dev/null 2>&1 || return 1
  adb shell cat /sdcard/ui.xml > "$SERVER/ui.xml" 2>/dev/null || return 1
  python3 -I - "$SERVER/ui.xml" <<'PY'
import re, sys
xml = open(sys.argv[1], encoding="utf-8", errors="replace").read()
for knoten in re.findall(r"<node [^>]*>", xml):
    text = re.search(r' text="([^"]*)"', knoten)
    if text and text.group(1).strip().lower() in ("update", "aktualisieren", "install", "installieren"):
        b = re.search(r'bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"', knoten)
        if b:
            x1, y1, x2, y2 = map(int, b.groups())
            print((x1 + x2) // 2, (y1 + y2) // 2)
            break
PY
}

rc=0
adb uninstall "$PAKET" > /dev/null 2>&1 || true
adb install -r "$APK0" > /dev/null || { zeile "FEHLER: Installation von n per adb"; exit 1; }
# Erlaubnis "Unbekannte Apps installieren" wie nach dem Antippen in der Einstellung (Fenster TabletErlaubnisTitel)
adb shell appops set "$PAKET" REQUEST_INSTALL_PACKAGES allow
anbieten "$APK1"
# Schritt 1: n -> n+1
rueckfrage=nein; getippt=nein; t=0
starten
while [ $t -lt 300 ]; do
  sleep 5; t=$((t + 5))
  adb logcat -d -s UPDATEPROBE:I > "$LOG" 2>/dev/null || true
  if grep -q "UPDATEPROBE: STATUS -1" "$LOG"; then
    rueckfrage=ja
    if [ "$getippt" = nein ] && punkt=$(tippen) && [ -n "$punkt" ]; then
      adb shell input tap $punkt; getippt=ja
    fi
  fi
  if [ "$(installiert)" = "$C1" ]; then break; fi
  grep -q "UPDATEPROBE: \(FEHLER\|KEIN ANGEBOT\)" "$LOG" && break
done
sed 's/^.*UPDATEPROBE: /  /' "$LOG" | tee -a "$ERGEBNIS"
jetzt=$(installiert)
zeile "Schritt 1: installiert $jetzt (Ziel $C1), Rueckfrage von Android: $rueckfrage, nach ${t} s"
[ "$jetzt" = "$C1" ] || { zeile "FEHLER: Update n -> n+1 nicht angekommen"; rc=1; }
grep -q "UPDATEPROBE: ANGEBOT S " "$LOG" || { zeile "FEHLER: kein Angebot der Linie S"; rc=1; }
grep -q "UPDATEPROBE: ANGEBOT .*Lernplattform" "$LOG" && { zeile "FEHLER: das Werkzeug bot ein Release der Lernplattform an"; rc=1; }
# Schritt 2: n+1 starten, update.log lesen; dieselbe Liste darf nichts mehr anbieten (vA9.9.9 bleibt aussen vor)
starten
t=0
while [ $t -lt 120 ]; do
  sleep 5; t=$((t + 5))
  adb logcat -d -s UPDATEPROBE:I > "$LOG" 2>/dev/null || true
  grep -q "UPDATEPROBE: \(KEIN ANGEBOT\|FEHLER\|ANGEBOT\)" "$LOG" && break
done
sed 's/^.*UPDATEPROBE: /  /' "$LOG" | tee -a "$ERGEBNIS"
grep -q "UPDATEPROBE: START Version $(version "$APK1")," "$LOG" || { zeile "FEHLER: n+1 meldet nicht seine Version"; rc=1; }
grep -q "UPDATEPROBE: KEIN ANGEBOT" "$LOG" || { zeile "FEHLER: n+1 bekam noch ein Angebot (oder einen Fehler) statt keinem"; rc=1; }
grep -q "UPDATEPROBE: LOG .*Download fertig: Fachinformatiker-Stadtbau-" "$LOG" || { zeile "FEHLER: update.log nennt keinen fertigen Download"; rc=1; }
grep -q "UPDATEPROBE: LOG .*Installer läuft" "$LOG" || { zeile "FEHLER: update.log nennt keinen gestarteten Installer"; rc=1; }
if [ $rc -ne 0 ]; then
  echo "===== Absturz-Puffer (logcat -b crash) ====="
  adb logcat -d -b crash | tail -n 80 || true
  # Welle CI schneller 2 (Meldung P12-84 Punkt 1, Sammelliste 270/696): Zustand der App ins Artefakt, damit die Ursache
  # von "meldet sich nicht" belegbar wird (laufende Prozesse, Activity im Vordergrund, installierte Version, Protokoll)
  {
    echo "===== Zustand bei Rot (Sammelliste 270) ====="
    echo "installiert: $(installiert)"
    echo "--- Prozesse der App"
    adb shell ps -A 2>/dev/null | grep -i "stadtbau\|PID" || echo "(kein Prozess der App)"
    echo "--- Activity im Vordergrund"
    adb shell dumpsys activity activities 2>/dev/null | grep -E "mResumedActivity|topResumedActivity|mFocusedApp" | head -n 5 || true
    echo "--- Paketinstaller (Sitzungen)"
    adb shell dumpsys package installer 2>/dev/null | grep -iE "stadtbau|sessionId" | head -n 10 || true
    echo "--- logcat der letzten 400 Zeilen (alle Puffer ausser crash)"
    adb logcat -d -t 400 2>/dev/null || true
  } >> "$ERGEBNIS" 2>&1
fi
if [ $rc -eq 0 ]; then zeile "ERGEBNIS: gruen (Werkzeug n -> n+1, Lernplattform-Release nie angeboten, update.log da)"; else zeile "ERGEBNIS: rot"; fi
exit $rc
