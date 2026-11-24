#!/usr/bin/env bash
# Welle A 0.98.0 (Weisung P12-315, Sammelliste 391): Touch-Probe der Sticks im Emulator mit echten Touch-Eingaben (adb shell
# input). Laeuft nach der Update-Probe im selben Emulator (emulator_proben.sh) mit der dort installierten Probe-APK n+2
# (-p:UpdateProbe=true, x86_64). Die App startet mit "--ez touch_probe true", oeffnet ein frisches Spiel in 3D, stellt die Figur
# draussen auf eine freie Stelle und meldet im Protokoll (TOUCHPROBE:) die Bildpunkte der Daumen (BEREIT) und jeden ruhigen
# Stand (STAND: Stelle, waagrechter Blick der Kamera). Schritte:
#   links_vor        linken Stick nach vorn ziehen und halten (input swipe)
#   rechts_rechts    rechten Stick nach rechts ziehen und halten
#   tippen           kurzer Tipp ohne Ziehen (input tap) auf ein Ziel im Stick-Bereich
#   haltung_*        Welle A 0.99.0 (Sammelliste 400): runder Haltung-Knopf am rechten Daumen (Zeile HALTUNG = Mitte):
#                    tippen (Ducken), tippen (Stehen), lang druecken (Kriechen), tippen (Stehen); je Zeile KOERPER der App;
#                    seit A 1.7.0 (Sammelliste 447) ist das „Hinknien“, Aufstehen tippt „Hinlegen“ (Zeile LIEGEN), dazu nach unten
#                    wischen auf Hinknien (Kriechen) und Hinlegen tippen (Stehen)
#   rechts_umgekehrt zweiter Start mit "--ez touch_umkehren true" (Schalter Umsehen links/rechts umkehren), rechter Stick nach rechts
# Zwei ganze Durchgaenge nacheinander (stabil = zwei von zwei). Das Urteil faellt werkzeuge/android/touch_urteil.py (zuerst dessen Selbsttest mit Gegenproben). Rueckgabe 1 = rot.
# Aufruf: touch_probe.sh <ergebnisdatei> (haengt an die Datei an)
set -u
ERGEBNIS=$1
PAKET=io.github.papst2142cmd.lernplattform
LOG=$(mktemp)
SCHRITTE=$(mktemp)
trap 'rm -f "$LOG" "$SCHRITTE"' EXIT
zeile() { echo "$*" | tee -a "$ERGEBNIS"; }
zeile "===== Touch-Probe (Sticks mit echten Touch-Eingaben) ====="
python3 -I werkzeuge/android/touch_urteil.py selbsttest | tee -a "$ERGEBNIS" || { zeile "FEHLER: Selbsttest des Urteils rot"; exit 1; }
KOMP=$(adb shell cmd package resolve-activity --brief "$PAKET" | tr -d '\r' | tail -n 1)
lesen() { adb logcat -d -s TOUCHPROBE:I 2>/dev/null | tr -d '\r' | sed -n 's/^.*TOUCHPROBE: //p' > "$LOG"; }
warten() {   # $1 Muster (grep), $2 Sekunden
  local t=0
  while [ "$t" -lt "$2" ]; do lesen; grep -q "$1" "$LOG" && return 0; sleep 2; t=$((t + 2)); done
  return 1
}
stand() { lesen; grep "^STAND " "$LOG" | tail -n 1; }
nummer() { echo "$1" | awk '{print $2}'; }
zaehlen() { lesen; grep -c "$1" "$LOG" || true; }
neuer_stand() {   # $1 Nummer des Stands vorher; wartet bis 40 s auf einen spaeteren ruhigen Stand
  local t=0 s n
  while [ "$t" -lt 40 ]; do
    sleep 2; t=$((t + 2)); s=$(stand); n=$(nummer "$s")
    if [ -n "$n" ] && [ "$n" -gt "$1" ]; then echo "$s"; return 0; fi
  done
  return 1
}
starten() {   # $1 = true/false fuer "Umsehen links/rechts umkehren"
  adb logcat -c
  adb shell am force-stop "$PAKET" > /dev/null 2>&1 || true
  # Befund Lauf 1110 (alle vier Schritte ohne jede Wirkung, beide Durchgaenge): Bildschirm wach halten, Sperre weg und den
  # Android-Hinweis zum Vollbild als gesehen markieren, damit kein Fenster die Finger abfaengt; Zustand ins Protokoll
  adb shell svc power stayon true > /dev/null 2>&1 || true
  adb shell input keyevent KEYCODE_WAKEUP > /dev/null 2>&1 || true
  adb shell wm dismiss-keyguard > /dev/null 2>&1 || true
  adb shell settings put secure immersive_mode_confirmations confirmed > /dev/null 2>&1 || true
  adb shell am start -n "$KOMP" --ez touch_probe true --ez touch_umkehren "$1" > /dev/null 2>&1
  warten "^BEREIT \|^FEHLER" 300 || { lesen; sed 's/^/  /' "$LOG" | tee -a "$ERGEBNIS"; zeile "FEHLER: keine Zeile BEREIT nach 300 s"; return 1; }
  grep -E "^(START|3D|LAGE|UEBERLAGERUNG|TREFFER|HALTUNG|LIEGEN|BEREIT|FEHLER)" "$LOG" | sed 's/^/  /' | tee -a "$ERGEBNIS"
  zeile "  Geraet: $(adb shell wm size | tr -d '\r' | tail -n 1), $(adb shell dumpsys power | tr -d '\r' | grep -m 1 -o 'mWakefulness=[A-Za-z]*'), $(adb shell dumpsys window | tr -d '\r' | grep -m 1 -o 'mCurrentFocus=.*')"
  if grep -q "^FEHLER" "$LOG"; then return 1; fi
  # BEREIT links LX LY rechts RX RY radius R tipp TX TY
  read -r _ _ LX LY _ RX RY _ R _ TX TY <<< "$(grep "^BEREIT " "$LOG" | tail -n 1)"
  # HALTUNG HX HY durchmesser D haltung H (Mitte des runden Haltung-Knopfs)
  read -r _ HX HY _ <<< "$(grep "^HALTUNG " "$LOG" | tail -n 1)"
  # LIEGEN GX GY durchmesser D wisch W (Welle A 1.7.0, Sammelliste 447: Mitte des Knopfs Hinlegen; W = Bildpunkte, die das
  # Wischen nach unten auf Hinknien sicher ueber die Schwelle bringen)
  read -r _ GX GY _ _ _ WISCH <<< "$(grep "^LIEGEN " "$LOG" | tail -n 1)"
  warten "^STAND 1 " 40 || { zeile "FEHLER: kein ruhiger Stand nach dem Start"; return 1; }
  return 0
}
schritt() {   # $1 Name, Rest: Befehl fuer adb shell
  local name=$1; shift
  local vor nach geht0 stick0 geht stick
  vor=$(stand); geht0=$(zaehlen "^GEHT"); stick0=$(zaehlen "^STICK aktiv ja"); zeiger0=$(zaehlen "^ZEIGER")
  adb shell "$*"
  if ! nach=$(neuer_stand "$(nummer "$vor")"); then
    nach=$vor
    zeile "  $name: kein neuer ruhiger Stand nach 40 s"
  fi
  zeile "  $name: Finger beim Programm angekommen: $(zaehlen "^ZEIGER ab") ab, $(zaehlen "^ZEIGER auf") auf (Summe seit dem Start)"
  lesen; grep "^ZEIGER" "$LOG" | tail -n "+$((zeiger0 + 1))" | sed 's/^/    /' | tee -a "$ERGEBNIS"
  geht=nein; stick=nein
  [ "$(zaehlen "^GEHT")" -gt "$geht0" ] && geht=ja
  [ "$(zaehlen "^STICK aktiv ja")" -gt "$stick0" ] && stick=ja
  echo "SCHRITT $name VOR $(echo "$vor" | cut -d' ' -f3-6) NACH $(echo "$nach" | cut -d' ' -f3-6) GEHT $geht STICK $stick" | tee -a "$SCHRITTE" "$ERGEBNIS"
}
haltung_schritt() {   # $1 Name, $2 erwartete Haltung, Rest: Befehl fuer adb shell; wartet bis 10 s auf eine neue Zeile KOERPER
  local name=$1 soll=$2; shift 2
  local vor t=0 ist=-
  vor=$(zaehlen "^KOERPER ")
  adb shell "$*"
  while [ "$t" -lt 10 ]; do
    sleep 1; t=$((t + 1))
    if [ "$(zaehlen "^KOERPER ")" -gt "$vor" ]; then lesen; ist=$(grep "^KOERPER " "$LOG" | tail -n 1 | awk '{print $2}'); break; fi
  done
  sleep 1
  # eine zweite Aenderung in derselben Geste (z. B. Ducken und gleich wieder Stehen) waere ein Fehler: dann zaehlt die letzte
  [ "$ist" != "-" ] && { lesen; ist=$(grep "^KOERPER " "$LOG" | tail -n 1 | awk '{print $2}'); }
  echo "HALTUNG $name ERWARTET $soll IST $ist AENDERUNGEN $(( $(zaehlen "^KOERPER ") - vor ))" | tee -a "$SCHRITTE" "$ERGEBNIS"
}
rc=0
# zwei ganze Durchgaenge im selben Lauf (Weisung P12-315: stabil = zwei von zwei); jeder Durchgang hat sein eigenes Urteil
for DURCHGANG in 1 2; do
  zeile "----- Durchgang $DURCHGANG von 2 -----"
  : > "$SCHRITTE"
  if starten false; then
    zeile "  Befehle: links $LX,$LY rechts $RX,$RY Radius $R Tipp $TX,$TY"
    schritt links_vor "input swipe $LX $LY $LX $((LY - R)) 2500"
    schritt rechts_rechts "input swipe $RX $RY $((RX + R)) $RY 1500"
    zeile "  Hinknien: $HX,$HY  Hinlegen: $GX,$GY"
    haltung_schritt haltung_tippen Ducken "input tap $HX $HY"
    haltung_schritt haltung_tippen2 Stehen "input tap $HX $HY"
    haltung_schritt haltung_lang Kriechen "input swipe $HX $HY $HX $HY 1200"
    haltung_schritt haltung_auf Stehen "input tap $GX $GY"
    # Welle A 1.7.0 (Nico 2a): nach unten wischen auf Hinknien = gleich hinlegen; Hinlegen antippen = aufstehen
    haltung_schritt haltung_wisch Kriechen "input swipe $HX $HY $HX $((HY + WISCH)) 300"
    haltung_schritt haltung_auf2 Stehen "input tap $GX $GY"
    schritt tippen "input tap $TX $TY"
  else
    rc=1
  fi
  if starten true; then
    schritt rechts_umgekehrt "input swipe $RX $RY $((RX + R)) $RY 1500"
  else
    rc=1
  fi
  adb shell am force-stop "$PAKET" > /dev/null 2>&1 || true
  python3 -I werkzeuge/android/touch_urteil.py "$SCHRITTE" | tee -a "$ERGEBNIS"
  [ "${PIPESTATUS[0]}" -eq 0 ] || rc=1
  zeile "Durchgang $DURCHGANG: $([ $rc -eq 0 ] && echo gruen || echo rot)"
done
if [ $rc -ne 0 ]; then
  zeile "  Protokoll der App (letzter Start):"
  sed 's/^/    /' "$LOG" | tail -n 60 | tee -a "$ERGEBNIS"
  echo "===== Absturz-Puffer (logcat -b crash) ====="
  adb logcat -d -b crash | tail -n 60 || true
fi
exit $rc
