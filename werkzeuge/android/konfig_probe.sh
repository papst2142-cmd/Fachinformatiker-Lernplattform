#!/usr/bin/env bash
# Welle Fehlerpruefung Teil 2 (Weisung P12-345, Sammelliste 426): Probe der Konfigurationswechsel im Emulator. Laeuft nach der
# Touch-Probe im selben Emulator (emulator_proben.sh) mit der dort installierten Probe-APK n+2 (-p:UpdateProbe=true). Die App
# startet mit "--ez konfig_probe true" und schreibt Zeilen "KONFIGPROBE:" (KonfigProbe.cs): ONCREATE n, GEAENDERT, ANSICHT.
# Schritte: Dichte aendern (wm density), Groesse aendern (wm size), beides zuruecksetzen. Gruen nur, wenn
#   - die Activity in jedem Schritt NICHT neu angelegt wird (genau eine Zeile ONCREATE, gleicher Prozess),
#   - jeder Schritt bei der App ankommt (GEAENDERT mit den neuen Werten) und die Ansicht danach zeichnet (ANSICHT, aufgebaut ja,
#     Groesse passt zur neuen Konfiguration),
#   - es keinen Absturz gibt (kein "FATAL EXCEPTION" der App) und die App vorne bleibt.
# Gegenprobe im selben Lauf: eine Aenderung, die NICHT in ConfigurationChanges steht (Schriftgroesse, fontScale), legt die
# Activity neu an (ONCREATE 2): so ist belegt, dass die Probe ein Neuanlegen ueberhaupt sieht.
# Welle A 1.3.2 / S 0.61.1 (Weisung P12-350, Sammelliste 434/435): jeder Schritt einzeln auch mit der Skalierung: die Zeile
# ANSICHT nennt die Skalierung von Avalonia und die zur Dichte gehoerende (soll = Dichte / 160); rot, wenn sie in einem Schritt
# nicht passt (434: nach "Dichte 320" galt bis A 1.3.1 die alte Skalierung bis zum naechsten Groessenwechsel). Gegenprobe im
# selben Lauf: ein neuer Start mit "--ez dichte_gegenprobe true" verhaelt sich wie vor 434; dort MUSS der Schritt "Dichte"
# eine falsche Skalierung zeigen, sonst saehe die Probe den Fehler nicht. Dasselbe fuer das Werkzeug (435, zweites Argument).
# Aufruf: konfig_probe.sh <ergebnisdatei> [paket] (haengt an die Datei an; ohne Paket die Lernplattform). Rueckgabe 1 = rot.
set -u
ERGEBNIS=$1
PAKET=${2:-io.github.papst2142cmd.lernplattform}
LOG=$(mktemp)
trap 'rm -f "$LOG"; adb shell wm density reset >/dev/null 2>&1; adb shell wm size reset >/dev/null 2>&1; adb shell settings put system font_scale 1.0 >/dev/null 2>&1' EXIT
zeile() { echo "$*" | tee -a "$ERGEBNIS"; }
fehler=0
rot() { zeile "FEHLER: Konfig-Probe: $*"; fehler=1; }
lesen() { adb logcat -d -s KONFIGPROBE:I 2>/dev/null | tr -d '\r' | sed -n 's/^.*KONFIGPROBE: //p' > "$LOG"; }
warten() {   # $1 Muster (grep -E), $2 Mindestzahl Treffer, $3 Sekunden
  local t=0
  while [ "$t" -lt "$3" ]; do lesen; [ "$(grep -cE "$1" "$LOG")" -ge "$2" ] && return 0; sleep 2; t=$((t + 2)); done
  return 1
}
zeile "===== Konfig-Probe $PAKET (Dichte und Groesse aendern, Activity darf nicht neu entstehen, Skalierung passt sofort) ====="
KOMP=$(adb shell cmd package resolve-activity --brief "$PAKET" | tr -d '\r' | tail -n 1)
adb shell am force-stop "$PAKET"
adb shell wm density reset >/dev/null 2>&1; adb shell wm size reset >/dev/null 2>&1
adb logcat -c
adb shell am start -n "$KOMP" --ez konfig_probe true > /dev/null 2>&1
warten "^ONCREATE 1( |$)" 1 90 || { rot "App startet nicht (keine Zeile ONCREATE 1)"; cat "$LOG" >> "$ERGEBNIS"; exit 1; }
sleep 8
PID=$(adb shell pidof "$PAKET" | tr -d '\r')
DICHTE=$(adb shell wm density | tr -d '\r' | sed -n 's/^Physical density: //p')
GROESSE=$(adb shell wm size | tr -d '\r' | sed -n 's/^Physical size: //p')
zeile "Start: pid $PID, Dichte $DICHTE, Groesse $GROESSE"
# eine andere Dichte als beim Start (sonst aendert "wm density" nichts)
NEU=320; [ "$DICHTE" = 320 ] && NEU=280
n=0
LETZTE=()
# Skalierung einer Zeile ANSICHT passt zur Dichte? (Feld 7 = skalierung, Feld 9 = soll), Rueckgabe 0 = passt
passt() { echo "$1" | awk '{d = $7 - $9; if (d < 0) d = -d; exit (d <= 0.01 && $9 > 0) ? 0 : 1}'; }
schritt() {   # $1 Beschreibung, $2.. adb shell Befehl; ein Wechsel kann mehrere Rueckrufe bringen, gezaehlt wird ab dem Stand davor
  local text=$1; shift
  n=$((n + 1))
  lesen; local vorher; vorher=$(grep -c "^ANSICHT " "$LOG")
  adb shell "$@" > /dev/null 2>&1
  if warten "^ANSICHT " "$((vorher + 1))" 30; then
    sleep 3; lesen
    LETZTE[$n]=$(grep '^ANSICHT ' "$LOG" | tail -n 1)
    zeile "Schritt $n ($text): $(grep '^GEAENDERT ' "$LOG" | tail -n 1) | ${LETZTE[$n]}"
    passt "${LETZTE[$n]}" || rot "Schritt $n ($text): Skalierung passt nicht zur Dichte (${LETZTE[$n]})"
  else
    LETZTE[$n]=""
    rot "Schritt $n ($text) kam nicht an oder die Ansicht zeichnete nicht (Zeilen: $(tr '\n' ';' < "$LOG"))"
  fi
}
schritt "Dichte $NEU" wm density "$NEU"
schritt "Groesse 1000x1800" wm size 1000x1800
schritt "Dichte zurueck" wm density reset
schritt "Groesse zurueck" wm size reset
sleep 3
lesen
ONCREATE=$(grep -c "^ONCREATE " "$LOG")
PID2=$(adb shell pidof "$PAKET" | tr -d '\r')
[ "$ONCREATE" -eq 1 ] || rot "Activity wurde neu angelegt ($ONCREATE Zeilen ONCREATE)"
[ -n "$PID" ] && [ "$PID" = "$PID2" ] || rot "Prozess gewechselt ($PID -> $PID2)"
grep -q "^ANSICHT .* aufgebaut nein" "$LOG" && rot "Ansicht nach einem Wechsel nicht aufgebaut"
# die Ansicht passt zur neuen Groesse: Schritt 2 (1000x1800 bei Dichte 320) ist schmaler als Schritt 4 (zurueck)
B2=$(echo "${LETZTE[2]:-}" | awk '{print $3}'); B4=$(echo "${LETZTE[4]:-}" | awk '{print $3}')
if [ -n "$B2" ] && [ -n "$B4" ] && [ "$B2" = "$B4" ] && [ "$GROESSE" != "1000x1800" ]; then rot "Ansicht folgt der Groesse nicht (Breite $B2 und $B4)"; fi
FATAL=$(adb logcat -d 2>/dev/null | tr -d '\r' | grep -A3 "FATAL EXCEPTION" | grep -c "$PAKET" || true)
[ "$FATAL" -eq 0 ] || rot "Absturz im Protokoll ($FATAL Zeilen FATAL EXCEPTION mit $PAKET)"
adb shell dumpsys window 2>/dev/null | tr -d '\r' | grep -E "mCurrentFocus|mFocusedApp" | grep -q "$PAKET" || rot "App ist nicht mehr vorne"
# Gegenprobe: Schriftgroesse (nicht in ConfigurationChanges) legt die Activity neu an
adb shell settings put system font_scale 1.3 > /dev/null 2>&1
if warten "^ONCREATE 2( |$)" 1 40; then zeile "Gegenprobe: Schriftgroesse legt die Activity neu an (ONCREATE 2), die Probe sieht ein Neuanlegen"
else rot "Gegenprobe: Schriftgroesse legte die Activity nicht neu an (die Probe saehe ein Neuanlegen nicht)"; fi
adb shell settings put system font_scale 1.0 > /dev/null 2>&1
# Gegenprobe Dichte: neuer Start wie vor 434 (dichte_gegenprobe); der Schritt "Dichte" muss die falsche Skalierung zeigen
adb shell am force-stop "$PAKET"
adb shell wm density reset >/dev/null 2>&1; adb shell wm size reset >/dev/null 2>&1
adb logcat -c
adb shell am start -n "$KOMP" --ez konfig_probe true --ez dichte_gegenprobe true > /dev/null 2>&1
if warten "^ONCREATE 1 .*gegenprobe" 1 90; then
  sleep 8
  adb shell wm density "$NEU" > /dev/null 2>&1
  if warten "^ANSICHT " 1 30; then
    sleep 3; lesen
    G=$(grep '^ANSICHT ' "$LOG" | tail -n 1)
    if passt "$G"; then rot "Gegenprobe Dichte: ohne Behebung passt die Skalierung trotzdem ($G), die Probe saehe 434 nicht"
    else zeile "Gegenprobe Dichte: ohne Behebung falsche Skalierung ($G), die Probe sieht den Fehler"; fi
  else
    rot "Gegenprobe Dichte: Schritt kam nicht an (Zeilen: $(tr '\n' ';' < "$LOG"))"
  fi
else
  rot "Gegenprobe Dichte: App startet nicht (keine Zeile ONCREATE 1 ... gegenprobe)"
fi
adb shell wm density reset > /dev/null 2>&1
adb shell am force-stop "$PAKET"
if [ "$fehler" -eq 0 ]; then zeile "Konfig-Probe gruen: $ONCREATE x ONCREATE bei 4 Wechseln, Skalierung in jedem Schritt passend, pid $PID gleich, kein Absturz"; fi
exit $fehler
