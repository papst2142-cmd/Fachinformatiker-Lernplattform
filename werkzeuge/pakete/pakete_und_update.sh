#!/usr/bin/env bash
# Phase 0, Spike D: baut die Pakete fuer ein System und prueft das Update.
#   A 0.49.0 (Paketversion 1.0.0-a.0.49.0) -> A 0.49.1 -> B 0.1.0, danach muss
#   ein Rueckschritt auf A 0.49.1 abgelehnt werden. Die Datenordner-Probe aus
#   der ersten Version muss nach jedem Update unveraendert da sein.
#   Phase 5 W3 (Weisung P5-8): jedes Update laeuft wie der Knopf "Jetzt aktualisieren"
#   (Laden mit Pruefsumme, Kopie sicherungen/vor_update_*.db, Merker); nach jedem
#   Update muss die Pruefung beim Start "ok" mit der neuen Version melden (genau
#   einmal) und die Zahl der Kopien um eins steigen. Sammelliste 46 (Velopack-Sperre
#   unter Windows) wird nur beobachtet und am Ende gezaehlt (Phase 6 T5).
#   Phase 6 T4: unter Windows zum Schluss deinstallieren; beide Datenordner bleiben gleich.
# Aufruf: werkzeuge/pakete/pakete_und_update.sh <rid> <ausgabeordner>
#   rid: win-x64 | linux-x64 | osx-arm64 | osx-x64 (osx-x64 = Intel-Mac mit eigenem Velopack-Kanal
#   osx-x64, Phase 11 FW10). Phase 11 Mac-Welle (Weisungen P11-28, P11-27): auch macOS mit Update-Probe;
#   vorher Startprobe der fertigen .pkg (Installation nach ~/Applications wie bei Nico, Start der installierten
#   App ohne Oberflaeche, codesign -dv und spctl -a -vv ins Log).
# Braucht: dotnet, vpk (dotnet tool), unter Linux mksquashfs.
set -euo pipefail
RID="$1"
AUS="$(mkdir -p "$2" && cd "$2" && pwd)"
WURZEL="$(cd "$(dirname "$0")/../.." && pwd)"
ID="Fachinformatiker-Lernplattform"
ASSETS="$WURZEL/src/Lernplattform.App/Assets"
case "$RID" in
  win-*) EXE="Lernplattform.exe"; ICON="$ASSETS/icon.ico" ;;
  linux-*) EXE="Lernplattform"; ICON="$ASSETS/icon.png" ;;
  osx-*)
    EXE="Lernplattform"
    # vpk braucht unter macOS ein .icns; aus icon.png mit Bordmitteln erzeugen
    SET="$AUS/icon.iconset"
    mkdir -p "$SET"
    for g in 16 32 128 256 512; do
      sips -z $g $g "$ASSETS/icon.png" --out "$SET/icon_${g}x${g}.png" >/dev/null
      sips -z $((g*2)) $((g*2)) "$ASSETS/icon.png" --out "$SET/icon_${g}x${g}@2x.png" >/dev/null
    done
    iconutil -c icns "$SET" -o "$AUS/icon.icns"
    ICON="$AUS/icon.icns"
    ;;
esac

# Weisung P8-53: alle Probe-Pakete tragen die Notizen der Version im Repo (Abschnitt aus AENDERUNGEN.md,
# Schnitt in release.py wie beim Release); die Update-Probe prueft sie im angebotenen Paket.
PY="${PYTHON:-python3}"
NOTIZEN="$AUS/notizen.md"
NOTIZEN_SHA256="$("$PY" -I "$WURZEL/werkzeuge/release/release.py" notizen "$NOTIZEN" | tr -d '\r' | grep '^NOTIZEN_SHA256=' | cut -d= -f2)"
test -n "$NOTIZEN_SHA256"
# Phase 11 FW10 (Weisung P11-18): Intel-Mac eigener Kanal, die anderen behalten die Vorgabe von vpk
KANAL_ARG=()
case "$RID" in win-*) KANAL=win ;; linux-*) KANAL=linux ;; osx-x64) KANAL=osx-x64; KANAL_ARG=(-c osx-x64) ;; osx-*) KANAL=osx ;; esac

baue() { # Stufe Version
  local stufe="$1" version="$2"
  local paket="1.0.0-$(echo "$stufe" | tr 'A-Z' 'a-z').$version"
  local pub="$AUS/pub-$stufe-$version" feed="$AUS/feed-$stufe-$version"
  dotnet publish "$WURZEL/src/Lernplattform.Desktop" -c Release -r "$RID" --self-contained \
    -p:Version="$version" -p:Stufe="$stufe" -p:DebugType=none -o "$pub"
  vpk pack -u "$ID" -v "$paket" -p "$pub" -e "$EXE" \
    -i "$ICON" --packTitle "Fachinformatiker Lernplattform" \
    --releaseNotes "$NOTIZEN" ${KANAL_ARG[@]+"${KANAL_ARG[@]}"} -o "$feed"
  rm -rf "$pub"
  case "$RID" in
    osx-*)
      # Patch A 0.38.0 (Sammelliste 203): .pkg nur "Nur fuer mich"; das unbearbeitete Paket der ersten Version
      # bleibt fuer die Gegenprobe (a) liegen
      local pkgs=("$feed"/*.pkg)
      if [ "${#pkgs[@]}" -ne 1 ] || [ ! -f "${pkgs[0]}" ]; then echo "FEHLER: nicht genau ein .pkg in $feed"; exit 1; fi
      cp "${pkgs[0]}" "$AUS/pkg-original-$stufe-$version.pkg"
      bash "$WURZEL/werkzeuge/pakete/pkg_nur_benutzer.sh" "${pkgs[0]}"
      ;;
  esac
  "$PY" -I "$WURZEL/werkzeuge/release/release.py" pruefe-feed "$feed/releases.$KANAL.json" "$paket"
  echo "gebaut: $stufe $version = $paket"
}

baue A 0.49.0
baue A 0.49.1
baue B 0.1.0
ls -la "$AUS"/feed-*

# Linux: Ein ZIP (GitHub-Artefakt, Browser-Download) verliert das Ausfuehrungsrecht.
# tar behaelt es; beim Entpacken mit der Dateiverwaltung startet das AppImage per Doppelklick.
case "$RID" in
  linux-*)
    mkdir -p "$AUS/appimage"
    tar -C "$AUS/feed-A-0.49.0" -czf "$AUS/appimage/Fachinformatiker-Lernplattform-A-0.49.0-AppImage.tar.gz" "$ID.AppImage"
    tar -tvzf "$AUS/appimage/Fachinformatiker-Lernplattform-A-0.49.0-AppImage.tar.gz"
    ;;
esac


# --- Update-Probe ---
export LERNPLATTFORM_DATEN="$AUS/daten"
export LERNPLATTFORM_UPDATE_BERICHT="$AUS/update_bericht.txt"
rm -rf "$LERNPLATTFORM_DATEN" "$LERNPLATTFORM_UPDATE_BERICHT"
case "$RID" in
  win-*)
    "$AUS/feed-A-0.49.0/$ID-win-Setup.exe" --silent
    APP="$(cygpath -u "$LOCALAPPDATA")/$ID/current/$EXE"
    ;;
  linux-*)
    # Velopack legt geladene Pakete hier ab und spielt ein liegengebliebenes
    # neueres Paket beim naechsten Start selbst ein; fuer eine saubere Probe leeren.
    rm -rf /var/tmp/velopack/Fachinformatiker-Lernplattform
    mkdir -p "$AUS/installiert"
    cp "$AUS/feed-A-0.49.0/$ID.AppImage" "$AUS/installiert/"
    APP="$AUS/installiert/$ID.AppImage"
    ;;
  osx-*)
    # Startprobe (Weisung P11-27): die fertige .pkg ohne Administrator in den Benutzerordner installieren,
    # wie es das Paket erlaubt (enable_currentUserHome) und wie es bei Nico landet (Sammelliste 201)
    if [ "${CI:-}" != "true" ]; then echo "FEHLER: die Mac-Probe installiert nach ~/Applications und laeuft nur in der CI (CI=true)"; exit 1; fi
    PKGS=("$AUS/feed-A-0.49.0"/*.pkg)
    if [ "${#PKGS[@]}" -ne 1 ] || [ ! -f "${PKGS[0]}" ]; then echo "FEHLER: nicht genau ein .pkg in feed-A-0.49.0"; exit 1; fi
    BUNDLE="$HOME/Applications/Fachinformatiker Lernplattform.app"
    if [ -e "$BUNDLE" ]; then echo "FEHLER: $BUNDLE besteht schon (keine frische Probe)"; exit 1; fi
    if ! installer -pkg "${PKGS[0]}" -target CurrentUserHomeDirectory; then
      # Ersatzweg nur fuer die CI (kein Administrator-Dialog moeglich): Paket entpacken und die App an dieselbe
      # Stelle legen; das Update danach laeuft genauso ueber Velopack. Steht als INFO im Log.
      echo "INFO Mac-Probe: installer ohne Administrator abgelehnt, Ersatzweg pkgutil --expand-full nach ~/Applications"
      pkgutil --expand-full "${PKGS[0]}" "$AUS/pkg-entpackt"
      mkdir -p "$HOME/Applications"
      cp -R "$(find "$AUS/pkg-entpackt" -maxdepth 3 -name "Fachinformatiker Lernplattform.app" -type d | head -n 1)" "$BUNDLE"
    fi
    # das Paket startet die App nach der Installation selbst (postinstall von Velopack, mit Oberflaeche);
    # fuer die Probe ohne Oberflaeche wieder beenden
    sleep 10
    pkill -f "$BUNDLE/Contents/MacOS/" || true
    sleep 3
    test -d "$BUNDLE" || { echo "FEHLER: .pkg hat nicht nach $BUNDLE installiert"; ls -la "$HOME/Applications" /Applications || true; exit 1; }
    APP="$BUNDLE/Contents/MacOS/$EXE"
    echo "Mac-Startprobe $RID ($(uname -m)): installiert nach $BUNDLE"
    echo "--- codesign -dv (Bundle) ---"; codesign -dv "$BUNDLE" 2>&1 || true
    echo "--- codesign -dv (Programmdatei) ---"; codesign -dv "$APP" 2>&1 || true
    echo "--- spctl -a -vv ---"; spctl -a -vv "$BUNDLE" 2>&1 || true
    ;;
esac

starte() { # Modus Ordner
  # Linux: AppImage ohne FUSE entpackt sich nach $TMPDIR und nimmt einen schon
  # entpackten Stand wieder; darum je Start ein eigener, frischer TMPDIR.
  local tmp="$AUS/tmp/$(date +%s%N)"
  mkdir -p "$tmp"
  TMPDIR="$tmp" LERNPLATTFORM_UPDATE_AUTO="$1" LERNPLATTFORM_UPDATE_ORDNER="$2" "$APP" || echo "Rueckgabe $?"
}

warte_auf() { # Version, die erreicht sein muss (Update laeuft nach dem Beenden weiter)
  # Modus "version" laesst den Merker stehen; erst "bericht" prueft wie der Programmstart
  for _ in $(seq 1 30); do
    starte version "$AUS"
    if tail -n 1 "$LERNPLATTFORM_UPDATE_BERICHT" | grep -q "Version $1;"; then return 0; fi
    sleep 4
  done
  echo "FEHLER: Version $1 nicht erreicht"; cat "$LERNPLATTFORM_UPDATE_BERICHT"; return 1
}

letzte() { # letzte Zeile des Berichts ohne \r (Windows schreibt CRLF; grep -x vergleicht die ganze Zeile)
  tail -n 1 "$LERNPLATTFORM_UPDATE_BERICHT" | tr -d '\r'
}

pruefe_abschluss() { # Anzeige, Zahl der Kopien
  starte bericht "$AUS"
  if ! letzte | grep -qx "bericht: Abschluss ok $1; Kopien vor dem Update: $2"; then
    echo "FEHLER: erwartet Abschluss ok $1 mit $2 Kopien"; cat "$LERNPLATTFORM_UPDATE_BERICHT"; return 1
  fi
  # zweiter Start: keine zweite Meldung
  starte bericht "$AUS"
  if ! letzte | grep -qx "bericht: Abschluss keiner; Kopien vor dem Update: $2"; then
    echo "FEHLER: zweite Meldung nach dem Neustart"; cat "$LERNPLATTFORM_UPDATE_BERICHT"; return 1
  fi
}

pruefe_notizen() { # Weisung P8-53: das angebotene Paket hat Notizen, das Update-Fenster zeigte genau den Abschnitt
  if ! tr -d '\r' < "$LERNPLATTFORM_UPDATE_BERICHT" | grep "^suchen: Notizen " | tail -n 1 | grep -q "; sha256 $NOTIZEN_SHA256\$"; then
    echo "FEHLER: angebotenes Paket ohne die erwarteten Notizen (sha256 $NOTIZEN_SHA256)"; cat "$LERNPLATTFORM_UPDATE_BERICHT"; return 1
  fi
}

starte bericht "$AUS"
grep -q "bericht: Version 1.0.0-a.0.49.0;" "$LERNPLATTFORM_UPDATE_BERICHT"
case "$RID" in osx-*) echo "Mac-Startprobe $RID: installierte App gestartet, Version 1.0.0-a.0.49.0, Datenordner-Probe angelegt" ;; esac
letzte | grep -qx "bericht: Abschluss keiner; Kopien vor dem Update: 0"
# Patch A 0.39.0 (Sammelliste 203, Probe (b)): Bundle, das der Benutzer nicht aendern darf (wie eine Installation
# "fuer alle Benutzer" in /Applications, Dateien gehoeren root). Gegenprobe: der alte stille Weg muss scheitern
# ("Not allowed to ask for elevated permissions"); mit dem Fix startet der Updater ohne --silent und will nach dem
# Passwort fragen ("will attempt to elevate"). Die Passwort-Eingabe kann die CI nicht vornehmen: Dialog beenden,
# Besitz zurueck, dann die normale Update-Kette. Jeder Versuch legt eine Kopie vor_update_* an (ZUSATZ).
ZUSATZ=0
case "$RID" in
  osx-*)
    VLOG="$HOME/Library/Logs/velopack_$ID.log"
    zeilen() { if [ -f "$VLOG" ]; then wc -l < "$VLOG" | tr -d ' '; else echo 0; fi; }
    warte_log() { # Text, ab Zeile
      for _ in $(seq 1 45); do
        if [ -f "$VLOG" ] && tail -n +"$(($2 + 1))" "$VLOG" | grep -q "$1"; then return 0; fi
        sleep 2
      done
      echo "FEHLER: im Velopack-Log fehlt nach Zeile $2: $1"; tail -n +"$(($2 + 1))" "$VLOG" 2>/dev/null | tail -n 40 || true; return 1
    }
    aufraeumen() { # Updater, Passwort-Dialog und eine vom Updater neu geoeffnete alte App beenden
      sleep 5
      pkill -f "$BUNDLE/Contents/MacOS/UpdateMac" || true
      pkill -x osascript || true
      pkill -f "$BUNDLE/Contents/MacOS/$EXE" || true
      sleep 3
    }
    sudo chown -R root:wheel "$BUNDLE"
    sudo chmod -R go-w "$BUNDLE"
    if touch "$BUNDLE/Contents/MacOS/.schreibtest" 2>/dev/null; then echo "FEHLER: Probe (b) Bundle ist noch beschreibbar"; exit 1; fi
    echo "--- Probe (b) Gegenprobe: alter stiller Weg bei nicht beschreibbarem Bundle ---"
    ab="$(zeilen)"
    export LERNPLATTFORM_UPDATE_GEGENPROBE_STILL=1
    starte suchen "$AUS/feed-A-0.49.1"
    unset LERNPLATTFORM_UPDATE_GEGENPROBE_STILL
    warte_log "Not allowed to ask for elevated permissions" "$ab"
    aufraeumen
    starte version "$AUS"
    tail -n 1 "$LERNPLATTFORM_UPDATE_BERICHT" | grep -q "Version 1.0.0-a.0.49.0;" || { echo "FEHLER: Gegenprobe (b) hat doch aktualisiert"; exit 1; }
    echo "Gegenprobe (b): still gescheitert wie bei Nico (Version bleibt 1.0.0-a.0.49.0)"
    echo "--- Probe (b): mit Fix fragt der Updater nach dem Passwort ---"
    ab="$(zeilen)"
    starte suchen "$AUS/feed-A-0.49.1"
    warte_log "will attempt to elevate permissions" "$ab"
    # diese Zeile schreibt der Updater auch auf dem stillen Weg; entscheidend ist, dass danach kein "Not allowed" folgt
    sleep 8
    echo "Probe (b): laeuft der Updater noch (wartet auf das Passwort)? $(pgrep -f "$BUNDLE/Contents/MacOS/UpdateMac" >/dev/null && echo ja || echo nein)"
    echo "--- Velopack-Log ab Probe (b) ---"; tail -n +"$((ab + 1))" "$VLOG" | grep -E "\[update:" | tail -n 15 || true
    if tail -n +"$((ab + 1))" "$VLOG" | grep -q "Not allowed to ask for elevated permissions"; then
      echo "FEHLER: Probe (b) Updater lief trotz nicht beschreibbarem Bundle still"; tail -n +"$((ab + 1))" "$VLOG" | tail -n 40; exit 1
    fi
    grep -q "Mac: Programmordner nicht beschreibbar, Update mit Passwortabfrage." "$LERNPLATTFORM_DATEN/update.log" \
      || { echo "FEHLER: Probe (b) Zeile in update.log fehlt"; cat "$LERNPLATTFORM_DATEN/update.log"; exit 1; }
    aufraeumen
    sudo chown -R "$(id -un)":staff "$BUNDLE"
    chmod -R u+w "$BUNDLE"
    echo "Probe (b): Updater ohne --silent gestartet (Passwortabfrage), Zeile in update.log"
    # Merker der beiden abgebrochenen Versuche wie beim naechsten Programmstart abschliessen (Meldung "nicht abgeschlossen")
    starte bericht "$AUS"
    letzte | grep -q "Abschluss FEHLER" || { echo "FEHLER: Probe (b) Merker nicht als gescheitert gemeldet"; cat "$LERNPLATTFORM_UPDATE_BERICHT"; exit 1; }
    ZUSATZ=2
    ;;
esac
starte suchen "$AUS/feed-A-0.49.1"
pruefe_notizen
warte_auf 1.0.0-a.0.49.1
pruefe_abschluss "A 0.49.1" $((1 + ZUSATZ))
starte suchen "$AUS/feed-B-0.1.0"
pruefe_notizen
warte_auf 1.0.0-b.0.1.0
pruefe_abschluss "B 0.1.0" $((2 + ZUSATZ))
starte suchen "$AUS/feed-A-0.49.1"
grep -q "suchen: kein neueres Paket" "$LERNPLATTFORM_UPDATE_BERICHT"
# Probe stammt immer noch aus der ersten Version
test "$(grep -c "Probe: angelegt von 1.0.0-a.0.49.0" "$LERNPLATTFORM_UPDATE_BERICHT")" -eq "$(grep -c "Probe:" "$LERNPLATTFORM_UPDATE_BERICHT")"
# update.log im Datenordner: beide Kopien und beide Starts des Updaters stehen drin
test "$(grep -c "| Programm | Installer wird gestartet: Velopack" "$LERNPLATTFORM_DATEN/update.log")" -eq $((2 + ZUSATZ))
# T5 (Phase 6, Sammelliste 46): nur zaehlen, keine Wiederholung (sie wuerde eine Sperre verdecken,
# die auch Nutzer traefe); "Version nicht erreicht" bleibt oben rot
echo "T5 Velopack-Sperre (Sammelliste 46): $(grep -c "AcquireLockFailed" "$LERNPLATTFORM_UPDATE_BERICHT" || true) mal im Bericht"

# T4 (Phase 6, Sammelliste 11): Deinstallation unter Windows laesst Datenordner und Lernstand stehen,
# auch den echten Datenordner %APPDATA%\Fachinformatiker-Lernplattform (ohne LERNPLATTFORM_DATEN)
case "$RID" in
  win-*)
    ECHT="$(cygpath -u "$APPDATA")/$ID"
    # nie auf einem echten PC: die Probe deinstalliert und darf einen echten Lernstand nie beruehren
    if [ "${CI:-}" != "true" ]; then echo "FEHLER: T4 laeuft nur in der CI (CI=true)"; exit 1; fi
    if [ -e "$ECHT" ]; then echo "FEHLER: T4 echter Datenordner besteht schon, wird nicht angefasst: $ECHT"; exit 1; fi
    tmp="$AUS/tmp/echt"
    mkdir -p "$tmp"
    TMPDIR="$tmp" LERNPLATTFORM_UPDATE_AUTO=bericht LERNPLATTFORM_UPDATE_ORDNER="$AUS" env -u LERNPLATTFORM_DATEN "$APP" || echo "Rueckgabe $?"
    test -f "$ECHT/lernplattform.db" && test -f "$ECHT/update_probe.txt" || { echo "FEHLER: T4 echter Datenordner nicht angelegt: $ECHT"; exit 1; }
    vorher="$( (cd "$ECHT" && find . -type f | sort | xargs sha256sum); (cd "$LERNPLATTFORM_DATEN" && find . -type f | sort | xargs sha256sum) )"
    WURZEL_APP="$(cygpath -u "$LOCALAPPDATA")/$ID"
    env -u LERNPLATTFORM_DATEN "$WURZEL_APP/Update.exe" --silent uninstall || echo "Update.exe Rueckgabe $?"
    for _ in $(seq 1 30); do [ -e "$APP" ] || break; sleep 2; done
    if [ -e "$APP" ]; then echo "FEHLER: T4 Programm nach der Deinstallation noch da: $APP"; exit 1; fi
    nachher="$( (cd "$ECHT" && find . -type f | sort | xargs sha256sum); (cd "$LERNPLATTFORM_DATEN" && find . -type f | sort | xargs sha256sum) )"
    if [ "$vorher" != "$nachher" ]; then
      echo "FEHLER: T4 Datenordner nach der Deinstallation veraendert"; diff <(echo "$vorher") <(echo "$nachher") || true; exit 1
    fi
    echo "T4 Deinstallation: Programm entfernt, $(echo "$nachher" | wc -l) Dateien in beiden Datenordnern unveraendert"
    ;;
  osx-*)
    # Patch A 0.37.0 (Sammelliste 203, Probe (a)): das Paket darf "fuer alle Benutzer" (/Applications, dorthin legte
    # der grafische Installer A 0.35.0 bei Nico) nicht mehr anbieten. Geprueft wird, welche Orte der Installer selbst
    # fuer das Paket erlaubt (installer -dominfo). Ein Einbau mit "sudo installer -target /" taugt nicht als Probe:
    # als root uebergeht der Befehl die Auswahl der Distribution (voller Lauf 479: auch das bearbeitete Paket landete
    # in /Applications); der grafische Installer zeigt nur die erlaubten Orte. Gegenprobe: unbearbeitetes Paket.
    echo "--- Probe (a): erlaubte Installationsorte (installer -dominfo) ---"
    orig="$(installer -dominfo -pkg "$AUS/pkg-original-A-0.49.0.pkg" 2>&1 || true)"
    bearb="$(installer -dominfo -pkg "${PKGS[0]}" 2>&1 || true)"
    echo "unbearbeitetes Paket: $(echo $orig)"
    echo "bearbeitetes Paket:   $(echo $bearb)"
    if ! grep -q "LocalSystem" <<<"$orig"; then
      echo "FEHLER: Gegenprobe (a) unbearbeitetes Paket bietet LocalSystem nicht an, die Probe waere nicht empfindlich"; exit 1
    fi
    echo "Gegenprobe (a): unbearbeitetes Paket bietet \"fuer alle Benutzer\" (LocalSystem) an, wie bei Nico"
    if ! grep -q "CurrentUserHomeDirectory" <<<"$bearb"; then
      echo "FEHLER: Probe (a) bearbeitetes Paket bietet \"Nur fuer mich\" (CurrentUserHomeDirectory) nicht an"; exit 1
    fi
    if grep -q "LocalSystem" <<<"$bearb"; then
      echo "FEHLER: Probe (a) bearbeitetes Paket bietet weiter \"fuer alle Benutzer\" (LocalSystem) an"; exit 1
    fi
    echo "Probe (a): bearbeitetes Paket bietet nur noch \"Nur fuer mich\" (~/Applications) an"
    ;;
esac
echo "UPDATE-PROBE GRUEN"
cat "$LERNPLATTFORM_UPDATE_BERICHT"
echo "--- update.log ---"
cat "$LERNPLATTFORM_DATEN/update.log"
