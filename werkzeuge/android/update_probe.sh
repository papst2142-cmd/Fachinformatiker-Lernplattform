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
# keine Kopie vor_update_*, oder (Schritt 4, A 0.41.1) das Vollbild nach Neustart/Rueckkehr fehlt. Aufruf: update_probe.sh <apk_n> <apk_n1> <apk_n2> <ergebnisdatei>
set -u
APK0=$1; APK1=$2; APK2=$3; ERGEBNIS=$4
PORT=8765
SERVER=$(mktemp -d)
LOG=$(mktemp)
: > "$ERGEBNIS"
zeile() { echo "$*" | tee -a "$ERGEBNIS"; }

BT=$(ls -d "$ANDROID_HOME"/build-tools/* | sort -V | tail -n 1)
badging() { "$BT/aapt2" dump badging "$1" > "$SERVER/badging.txt"; head -n 1 "$SERVER/badging.txt"; }
# Lauf 502: ".*name=" traf gierig "compileSdkVersionCodename='16'"; deshalb mit Anfang bzw. Leerzeichen davor
PAKET=$(badging "$APK0" | sed -nE "s/^package: name='([^']+)'.*/\1/p")
code() { badging "$1" | sed -nE "s/.* versionCode='([0-9]+)'.*/\1/p"; }
version() { badging "$1" | sed -nE "s/.* versionName='([^']+)'.*/\1/p"; }
C0=$(code "$APK0"); C1=$(code "$APK1"); C2=$(code "$APK2")
zeile "Paket $PAKET, Versionscodes $C0 -> $C1 -> $C2"
case "$PAKET" in io.github.papst2142cmd.lernplattform) ;; *) zeile "FEHLER: Paketname nicht gelesen ($PAKET)"; exit 1;; esac
[ -n "$C0" ] && [ -n "$C1" ] && [ -n "$C2" ] && [ "$C0" -lt "$C1" ] && [ "$C1" -lt "$C2" ] || { zeile "FEHLER: Versionscodes nicht gelesen"; exit 1; }

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

installiert() { adb shell dumpsys package "$PAKET" | grep -m 1 -oE "versionCode=[0-9]+" | cut -d= -f2; }

starten() {
  # Sammelliste 270 (Weisung P12-144): direkt nach einem Update kann der Start ueber monkey ins Leere gehen (Paket noch
  # nicht fertig eingerichtet), ohne dass etwas im Protokoll steht. Darum: Start pruefen (Prozess da?), bis zu 6 Versuche
  # im Abstand von 5 s, jeden Versuch mit der Antwort von monkey festhalten. Die Wartezeiten danach bleiben gleich.
  adb logcat -c
  adb shell am force-stop "$PAKET" > /dev/null 2>&1 || true
  local v aus pid
  for v in 1 2 3 4 5 6; do
    aus=$(adb shell monkey -p "$PAKET" -c android.intent.category.LAUNCHER 1 2>&1 | tr -d '\r')
    sleep 2
    pid=$(adb shell pidof "$PAKET" 2>/dev/null | tr -d '\r')
    if [ -n "$pid" ]; then
      zeile "Start: Versuch $v, Prozess $pid"
      return 0
    fi
    zeile "Start: Versuch $v ohne Prozess (monkey: $(echo "$aus" | grep -iE 'abort|error|no activities|injected|exception' | head -n 3 | tr '\n' ' '))"
    sleep 3
  done
  zeile "FEHLER: App startet nicht (6 Versuche)"
  return 1
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
# Lauf 846 Versuch 2: Android (ab 12) laesst dieselbe App von demselben Installer nur einmal in 30 s ohne Rueckfrage
# aktualisieren (Drossel fuer stille Updates). Schritt 2 begann dort 27 s nach Schritt 1, Android fragte nach (Status -1).
# Echte Updates liegen Tage auseinander; die Probe wartet darum 35 s, damit Schritt 2 dieselbe Lage prueft wie am Geraet.
zeile "Pause 35 s (Drossel stiller Updates von Android)"
sleep 35
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
# Schritt 4 (Patch A 0.41.1, Sammelliste 218, Fund Nico): Vollbild bleibt nach dem Neustart und nach der Rueckkehr aus
# dem Hintergrund an. n+2 hat in Schritt 3 den Merker "vollbild" gespeichert (wie der Schalter in den Optionen); jeder
# Start meldet 5 s nach OnResume, ob Android die Statusleiste zeigt (UpdateProbe.LeistenMelden). Gegenprobe: Start mit
# "--ez vollbild_gegenprobe true" verhaelt sich wie vor dem Fix und muss die Leiste zeigen, sonst beweist die Probe nichts.
t=0
until grep -q "UPDATEPROBE: VOLLBILD MERKER" "$LOG" || [ $t -ge 30 ]; do
  sleep 3; t=$((t + 3)); adb logcat -d -s UPDATEPROBE:I > "$LOG" 2>/dev/null || true
done
grep "UPDATEPROBE: VOLLBILD MERKER" "$LOG" | sed 's/^.*UPDATEPROBE: /  /' | tee -a "$ERGEBNIS"
grep -q "UPDATEPROBE: VOLLBILD MERKER an" "$LOG" || { zeile "FEHLER: Merker vollbild nicht gespeichert"; rc=1; }
KOMP=$(adb shell cmd package resolve-activity --brief "$PAKET" | tr -d '\r' | tail -n 1)
leisten() {   # $1 Beschreibung, $2 erwartet "ja"/"nein" (Statusleiste sichtbar)
  local t=0 z=""
  while [ $t -lt 60 ]; do
    sleep 3; t=$((t + 3))
    adb logcat -d -s UPDATEPROBE:I > "$LOG" 2>/dev/null || true
    z=$(grep "UPDATEPROBE: LEISTEN" "$LOG" | tail -n 1)
    [ -n "$z" ] && break
  done
  zeile "Vollbild $1: ${z#*UPDATEPROBE: } (erwartet sichtbar $2)"
  case "$z" in *"sichtbar $2, vollbild an"*) return 0;; esac
  return 1
}
adb logcat -c
adb shell am force-stop "$PAKET" > /dev/null 2>&1 || true
adb shell am start -n "$KOMP" --ez vollbild_gegenprobe true > /dev/null 2>&1
leisten "Gegenprobe ohne Fix, Neustart" ja || { zeile "FEHLER: Gegenprobe zeigt den Fund nicht (Probe beweist nichts)"; rc=1; }
starten
leisten "nach Neustart" nein || { zeile "FEHLER: Vollbild nach Neustart nicht aktiv"; rc=1; }
adb logcat -c
adb shell input keyevent KEYCODE_HOME
sleep 3
adb shell monkey -p "$PAKET" -c android.intent.category.LAUNCHER 1 > /dev/null 2>&1
leisten "nach Rueckkehr aus dem Hintergrund" nein || { zeile "FEHLER: Vollbild nach Rueckkehr aus dem Hintergrund nicht aktiv"; rc=1; }
if [ $rc -ne 0 ]; then
  echo "===== Absturz-Puffer (logcat -b crash) ====="
  adb logcat -d -b crash | tail -n 80 || true
fi
if [ $rc -eq 0 ]; then zeile "ERGEBNIS: gruen (n -> n+1 -> n+2, update.log und Kopien da, Vollbild nach Neustart und Rueckkehr)"; else zeile "ERGEBNIS: rot"; fi
exit $rc
