#!/usr/bin/env bash
# A 0.39.0 (Weisung P11-40 Frage 5a): Emulator-Probe des Updates im Programm am Handy, nur in der CI (Job android).
# Drei Probe-APKs (-p:UpdateProbe=true, x86_64, Testschluessel dieses Laufs) mit den Versionen n, n+1, n+2.
# Ein kleiner Testserver auf dem Runner spielt die GitHub-Antwort "neuestes Release" nach (Version, Datei, Groesse,
# digest von GitHub und die Pruefsummen-Tabelle im Release-Text). Die App sucht beim Start selbst, laedt, prueft und
# uebergibt die APK an Android (echter Ablauf UpdateDienst.AktualisierenAsync, siehe UpdateProbe.cs).
#   Schritt 1: n per adb installiert (Installer "adb"), Update auf n+1.
#   Schritt 2: n+1 hat sich selbst installiert (die App ist jetzt der Installer), Update auf n+2.
#   Schritt 3: Start von n+2 liest update.log und die Sicherheitskopien.
# Fragt Android zurueck ("Aktualisieren?", Status -1), tippt die Probe auf den Knopf und haelt das fest
# (Rueckfrage ja/nein je Schritt, Nico 1a: das Programm laedt selbst, Android darf nachfragen).
# Rot (Rueckgabe 1) wenn: eine Version kommt nicht an, update.log nennt das Update nicht "abgeschlossen" oder es gibt
# keine Kopie vor_update_*. Aufruf: update_probe.sh <apk_n> <apk_n1> <apk_n2> <ergebnisdatei>
set -u
APK0=$1; APK1=$2; APK2=$3; ERGEBNIS=$4
PORT=8765
SERVER=$(mktemp -d)
LOG=$(mktemp)
: > "$ERGEBNIS"
zeile() { echo "$*" | tee -a "$ERGEBNIS"; }

BT=$(ls -d "$ANDROID_HOME"/build-tools/* | sort -V | tail -n 1)
badging() { "$BT/aapt2" dump badging "$1" > "$SERVER/badging.txt"; head -n 1 "$SERVER/badging.txt"; }
PAKET=$(badging "$APK0" | sed -E "s/.*name='([^']+)'.*/\1/")
code() { badging "$1" | sed -E "s/.*versionCode='([0-9]+)'.*/\1/"; }
version() { badging "$1" | sed -E "s/.*versionName='([^']+)'.*/\1/"; }
C0=$(code "$APK0"); C1=$(code "$APK1"); C2=$(code "$APK2")
zeile "Paket $PAKET, Versionscodes $C0 -> $C1 -> $C2"

# Testserver: latest.json wie die GitHub-API, APK unter ihrem Namen
python3 -I -m http.server "$PORT" --bind 0.0.0.0 --directory "$SERVER" > "$SERVER/../server_$PORT.log" 2>&1 &
SERVER_PID=$!
trap 'kill $SERVER_PID 2>/dev/null; rm -rf "$SERVER" "$LOG"' EXIT

anbieten() {   # $1 = APK, die die App beim naechsten Start findet
  local apk=$1 ver name
  ver=$(version "$apk")            # Anzeige wie "A 0.39.1"
  ver=${ver// /}                    # Tag-Form "A0.39.1"
  name="Fachinformatiker-Lernplattform-${ver:0:1}-${ver:1}-android-arm64.apk"
  rm -f "$SERVER"/*.apk
  cp "$apk" "$SERVER/$name"
  python3 -I - "$SERVER" "$name" "$ver" "$PORT" <<'PY'
import hashlib, json, os, sys
ordner, name, ver, port = sys.argv[1:5]
pfad = os.path.join(ordner, name)
daten = open(pfad, "rb").read()
sha = hashlib.sha256(daten).hexdigest()
text = ("Emulator-Probe\n\n## Prüfsummen (SHA-256)\n\n| Datei | Größe | SHA-256 |\n|---|---|---|\n"
        f"| `{name}` | {len(daten)} | `{sha}` |\n")
antwort = {"tag_name": "v" + ver, "draft": False, "prerelease": False, "html_url": f"http://10.0.2.2:{port}/",
           "body": text, "assets": [{"name": name, "size": len(daten), "digest": "sha256:" + sha,
                                     "browser_download_url": f"http://10.0.2.2:{port}/{name}"}]}
with open(os.path.join(ordner, "latest.json"), "w", encoding="utf-8") as f:
    json.dump(antwort, f, ensure_ascii=False)
PY
  zeile "Angebot: Version $ver ($name)"
}

installiert() { adb shell dumpsys package "$PAKET" | sed -nE 's/.*versionCode=([0-9]+).*/\1/p' | head -n 1; }

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

schritt() {   # $1 Nummer, $2 Ziel-Versionscode
  local nr=$1 ziel=$2 rueckfrage=nein getippt=nein t=0 punkt
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
    if [ "$(installiert)" = "$ziel" ]; then break; fi
  done
  sed 's/^.*UPDATEPROBE: /  /' "$LOG" | tee -a "$ERGEBNIS"
  local jetzt; jetzt=$(installiert)
  zeile "Schritt $nr: installiert $jetzt (Ziel $ziel), Rueckfrage von Android: $rueckfrage, nach ${t} s"
  [ "$jetzt" = "$ziel" ]
}

rc=0
adb uninstall "$PAKET" > /dev/null 2>&1 || true
adb install -r "$APK0" > /dev/null || { zeile "FEHLER: Installation von n per adb"; exit 1; }
# Erlaubnis "Unbekannte Apps installieren" wie nach dem Antippen in der Einstellung (Nico 1a, Fenster ErlaubnisTitel)
adb shell appops set "$PAKET" REQUEST_INSTALL_PACKAGES allow
anbieten "$APK1"
schritt 1 "$C1" || { zeile "FEHLER: Update n -> n+1 nicht angekommen"; rc=1; }
anbieten "$APK2"
schritt 2 "$C2" || { zeile "FEHLER: Update n+1 -> n+2 nicht angekommen"; rc=1; }
# Schritt 3: n+2 starten, update.log und Kopien lesen (kein neueres Angebot mehr)
starten
t=0
while [ $t -lt 120 ]; do
  sleep 5; t=$((t + 5))
  adb logcat -d -s UPDATEPROBE:I > "$LOG" 2>/dev/null || true
  grep -q "UPDATEPROBE: \(KEIN ANGEBOT\|FEHLER\|ANGEBOT\)" "$LOG" && break
done
sed 's/^.*UPDATEPROBE: /  /' "$LOG" | tee -a "$ERGEBNIS"
n_ab=$(grep -c "UPDATEPROBE: LOG .*Update auf .* abgeschlossen" "$LOG" || true)
grep -q "UPDATEPROBE: LOG .*NICHT abgeschlossen" "$LOG" && { zeile "FEHLER: update.log meldet ein nicht abgeschlossenes Update"; rc=1; }
[ "$n_ab" -ge 2 ] || { zeile "FEHLER: update.log nennt $n_ab statt 2 abgeschlossene Updates"; rc=1; }
grep -q "UPDATEPROBE: KOPIEN .*vor_update_" "$LOG" || { zeile "FEHLER: keine Sicherheitskopie vor_update_*"; rc=1; }
if [ $rc -ne 0 ]; then
  echo "===== Absturz-Puffer (logcat -b crash) ====="
  adb logcat -d -b crash | tail -n 80 || true
fi
if [ $rc -eq 0 ]; then zeile "ERGEBNIS: gruen (n -> n+1 -> n+2, update.log und Kopien da)"; else zeile "ERGEBNIS: rot"; fi
exit $rc
