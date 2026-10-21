#!/usr/bin/env bash
# Phase 0, Spike D: baut die Pakete fuer ein System und prueft das Update.
#   A 0.7.0 (Paketversion 1.0.0-a.0.7.0) -> A 0.7.1 -> B 0.1.0, danach muss
#   ein Rueckschritt auf A 0.7.1 abgelehnt werden. Die Datenordner-Probe aus
#   der ersten Version muss nach jedem Update unveraendert da sein.
# Aufruf: werkzeuge/pakete/pakete_und_update.sh <rid> <ausgabeordner>
#   rid: win-x64 | linux-x64 | osx-arm64 (macOS: nur Pakete, keine Update-Probe)
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

baue() { # Stufe Version
  local stufe="$1" version="$2"
  local paket="1.0.0-$(echo "$stufe" | tr 'A-Z' 'a-z').$version"
  local pub="$AUS/pub-$stufe-$version" feed="$AUS/feed-$stufe-$version"
  dotnet publish "$WURZEL/src/Lernplattform.Desktop" -c Release -r "$RID" --self-contained \
    -p:Version="$version" -p:Stufe="$stufe" -p:DebugType=none -o "$pub"
  vpk pack -u "$ID" -v "$paket" -p "$pub" -e "$EXE" \
    -i "$ICON" --packTitle "Fachinformatiker Lernplattform" \
    -o "$feed"
  rm -rf "$pub"
  echo "gebaut: $stufe $version = $paket"
}

baue A 0.7.0
baue A 0.7.1
baue B 0.1.0
ls -la "$AUS"/feed-*

# Linux: Ein ZIP (GitHub-Artefakt, Browser-Download) verliert das Ausfuehrungsrecht.
# tar behaelt es; beim Entpacken mit der Dateiverwaltung startet das AppImage per Doppelklick.
case "$RID" in
  linux-*)
    mkdir -p "$AUS/appimage"
    tar -C "$AUS/feed-A-0.7.0" -czf "$AUS/appimage/Fachinformatiker-Lernplattform-A-0.7.0-AppImage.tar.gz" "$ID.AppImage"
    tar -tvzf "$AUS/appimage/Fachinformatiker-Lernplattform-A-0.7.0-AppImage.tar.gz"
    ;;
esac

case "$RID" in
  osx-*) echo "macOS: nur Pakete (Plan: kein Geraet)"; exit 0 ;;
esac

# --- Update-Probe ---
export LERNPLATTFORM_DATEN="$AUS/daten"
export LERNPLATTFORM_UPDATE_BERICHT="$AUS/update_bericht.txt"
rm -rf "$LERNPLATTFORM_DATEN" "$LERNPLATTFORM_UPDATE_BERICHT"
case "$RID" in
  win-*)
    "$AUS/feed-A-0.7.0/$ID-win-Setup.exe" --silent
    APP="$(cygpath -u "$LOCALAPPDATA")/$ID/current/$EXE"
    ;;
  linux-*)
    # Velopack legt geladene Pakete hier ab und spielt ein liegengebliebenes
    # neueres Paket beim naechsten Start selbst ein; fuer eine saubere Probe leeren.
    rm -rf /var/tmp/velopack/Fachinformatiker-Lernplattform
    mkdir -p "$AUS/installiert"
    cp "$AUS/feed-A-0.7.0/$ID.AppImage" "$AUS/installiert/"
    APP="$AUS/installiert/$ID.AppImage"
    ;;
esac

starte() { # Modus Ordner
  # Linux: AppImage ohne FUSE entpackt sich nach $TMPDIR und nimmt einen schon
  # entpackten Stand wieder; darum je Start ein eigener, frischer TMPDIR.
  local tmp="$AUS/tmp/$(date +%s%N)"
  mkdir -p "$tmp"
  TMPDIR="$tmp" LERNPLATTFORM_UPDATE_AUTO="$1" LERNPLATTFORM_UPDATE_ORDNER="$2" "$APP" || echo "Rueckgabe $?"
}

warte_auf() { # Text, der im Bericht stehen muss (Update laeuft nach dem Beenden weiter)
  for _ in $(seq 1 30); do
    starte bericht "$AUS"
    if tail -n 1 "$LERNPLATTFORM_UPDATE_BERICHT" | grep -q "Version $1;"; then return 0; fi
    sleep 4
  done
  echo "FEHLER: Version $1 nicht erreicht"; cat "$LERNPLATTFORM_UPDATE_BERICHT"; return 1
}

starte bericht "$AUS"
grep -q "bericht: Version 1.0.0-a.0.7.0;" "$LERNPLATTFORM_UPDATE_BERICHT"
starte suchen "$AUS/feed-A-0.7.1"
warte_auf 1.0.0-a.0.7.1
starte suchen "$AUS/feed-B-0.1.0"
warte_auf 1.0.0-b.0.1.0
starte suchen "$AUS/feed-A-0.7.1"
grep -q "suchen: kein neueres Paket" "$LERNPLATTFORM_UPDATE_BERICHT"
# Probe stammt immer noch aus der ersten Version
test "$(grep -c "Probe: angelegt von 1.0.0-a.0.7.0" "$LERNPLATTFORM_UPDATE_BERICHT")" -eq "$(grep -c "Probe:" "$LERNPLATTFORM_UPDATE_BERICHT")"
echo "UPDATE-PROBE GRUEN"
cat "$LERNPLATTFORM_UPDATE_BERICHT"
