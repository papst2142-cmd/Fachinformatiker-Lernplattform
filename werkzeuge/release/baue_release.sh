#!/usr/bin/env bash
# Phase 6 W1 (Weisung P6-2, VE1/VE3/VE4): baut die Release-Dateien eines Bauteils aus dem Stand im Repo
# (Version und Stufe nur aus Directory.Build.props, nichts wird ueberschrieben).
# Aufruf: werkzeuge/release/baue_release.sh <win-x64|linux-x64|osx-arm64|android> <ausgabeordner>
# Ergebnis: <aus>/release-<teil>/ mit den Release-Dateien dieses Bauteils und lizenzen-<teil>.json
#   (jede ausgelieferte Datei einem Bestandteil zugeordnet, sonst Abbruch). release.py zusammen fuegt
#   die vier Bauteile zu einem Release zusammen. Dieses Skript laedt nichts hoch und legt keinen Tag an.
# Android (VE3): Signieren mit dem Release-Schluessel nur ueber Umgebungsvariablen aus Geheimnissen:
#   LERNPLATTFORM_SCHLUESSEL_DATEI (Pfad zur .keystore-Datei), LERNPLATTFORM_SCHLUESSEL_ALIAS,
#   LERNPLATTFORM_SCHLUESSEL_PASSWORT, LERNPLATTFORM_SCHLUESSEL_KEY_PASSWORT (Passwoerter nur als env:,
#   nie auf der Befehlszeile). Ohne sie signiert .NET mit einem Testschluessel (jeder Lauf ein anderer).
#   LERNPLATTFORM_RELEASE_STRENG=1 (release.yml): ohne Release-Schluessel oder mit anderem Fingerabdruck
#   als werkzeuge/release/android_fingerabdruck.txt ist das ein FEHLER.
# Braucht: dotnet, vpk 1.2.161 (Desktop), unter Linux squashfs-tools, fuer Android den Workload.
set -Eeuo pipefail
# kein stummer Abbruch: Zeile und Befehl nennen (Befehle enthalten nie ein Passwort, nur env:-Verweise)
trap 'echo "FEHLER: Abbruch in Zeile $LINENO (Rueckgabe $?): $BASH_COMMAND"' ERR
TEIL="$1"
mkdir -p "$2"
AUS="$(cd "$2" && pwd)"
WURZEL="$(cd "$(dirname "$0")/../.." && pwd)"
PY="${PYTHON:-python3}"
ID="Fachinformatiker-Lernplattform"
ZIEL="$AUS/release-$TEIL"
ARBEIT="$AUS/arbeit-$TEIL"
if [ -e "$ZIEL" ] || [ -e "$ARBEIT" ]; then echo "FEHLER: $ZIEL oder $ARBEIT gibt es schon"; exit 1; fi
mkdir -p "$ZIEL" "$ARBEIT"
LIZENZEN=("$PY" -I "$WURZEL/werkzeuge/lizenzen/erzeuge_lizenzen.py")

# Kennzahlen (T1): ohne gueltigen Tag kein Release-Bau
"$PY" -I "$WURZEL/werkzeuge/release/release.py" version | tr -d '\r' > "$ARBEIT/kennzahlen.txt"
wert() { grep -m1 "^$1=" "$ARBEIT/kennzahlen.txt" | cut -d= -f2-; }
PAKET="$(wert PAKET)"; NAME="$(wert NAME)"; TAG="$(wert TAG)"
echo "Release-Bau $TEIL: $(wert ANZEIGE), Tag $TAG, Paket $PAKET"

desktop() {
  local rid="$1" exe icon assets="$WURZEL/src/Lernplattform.App/Assets" pub="$ARBEIT/pub" feed="$ARBEIT/feed"
  case "$rid" in
    win-*) exe="Lernplattform.exe"; icon="$assets/icon.ico" ;;
    linux-*) exe="Lernplattform"; icon="$assets/icon.png" ;;
    osx-*)
      exe="Lernplattform"
      local set="$ARBEIT/icon.iconset" g
      mkdir -p "$set"
      for g in 16 32 128 256 512; do
        sips -z $g $g "$assets/icon.png" --out "$set/icon_${g}x${g}.png" >/dev/null
        sips -z $((g*2)) $((g*2)) "$assets/icon.png" --out "$set/icon_${g}x${g}@2x.png" >/dev/null
      done
      iconutil -c icns "$set" -o "$ARBEIT/icon.icns"
      icon="$ARBEIT/icon.icns"
      ;;
  esac
  dotnet publish "$WURZEL/src/Lernplattform.Desktop" -c Release -r "$rid" --self-contained -p:DebugType=none -o "$pub"
  # VE4: Lizenz und volle Lizenztexte liegen neben dem Programm (die Uebersicht ist im Programm eingebettet)
  cp "$WURZEL/LICENSE.txt" "$pub/LICENSE.txt"
  "${LIZENZEN[@]}" sammeln desktop "$rid" "$pub" "$ARBEIT/vorab.json"
  "${LIZENZEN[@]}" schreiben "$ARBEIT/uebersicht.txt" "$pub/THIRD_PARTY_LICENSES.txt" "$ARBEIT/vorab.json"
  vpk pack -u "$ID" -v "$PAKET" -p "$pub" -e "$exe" -i "$icon" --packTitle "Fachinformatiker Lernplattform" -o "$feed"
  ls -la "$feed"
  local inhalt=()
  case "$rid" in
    win-*)
      cp "$feed/$ID-win-Setup.exe" "$feed/$ID-$PAKET-full.nupkg" "$feed/RELEASES" "$feed/releases.win.json" "$ZIEL/"
      inhalt=("$ZIEL/$ID-win-Setup.exe" "$ZIEL/$ID-$PAKET-full.nupkg")
      ;;
    linux-*)
      cp "$feed/$ID.AppImage" "$feed/$ID-$PAKET-linux-full.nupkg" "$feed/RELEASES-linux" "$feed/releases.linux.json" "$ZIEL/"
      # VE5/Sammelliste 9: tar behaelt das Ausfuehrungsrecht (ein Browser-Download des AppImage nicht)
      tar -C "$feed" -czf "$ZIEL/$ID-$NAME-AppImage.tar.gz" "$ID.AppImage"
      tar -tvzf "$ZIEL/$ID-$NAME-AppImage.tar.gz"
      inhalt=("$ZIEL/$ID.AppImage" "$ZIEL/$ID-$PAKET-linux-full.nupkg" "$ZIEL/$ID-$NAME-AppImage.tar.gz")
      ;;
    osx-*)
      # D7: macOS nur das .pkg, ohne Update-Feed; der Inhalt wird ueber das .nupkg gezaehlt
      local pkgs=("$feed"/*.pkg)
      if [ "${#pkgs[@]}" -ne 1 ] || [ ! -f "${pkgs[0]}" ]; then echo "FEHLER: nicht genau ein .pkg in $feed"; exit 1; fi
      cp "${pkgs[0]}" "$ZIEL/$ID-$NAME-osx-arm64.pkg"
      inhalt=("$ZIEL/$ID-$NAME-osx-arm64.pkg" "$feed/$ID-$PAKET-osx-full.nupkg")
      ;;
  esac
  "${LIZENZEN[@]}" sammeln desktop "$rid" "$pub" "$ZIEL/lizenzen-$rid.json" "${inhalt[@]}"
}

android() {
  local projekt="$WURZEL/src/Lernplattform.Android" signatur=() bt apk
  if [ -n "${LERNPLATTFORM_SCHLUESSEL_DATEI:-}" ]; then
    for v in LERNPLATTFORM_SCHLUESSEL_ALIAS LERNPLATTFORM_SCHLUESSEL_PASSWORT LERNPLATTFORM_SCHLUESSEL_KEY_PASSWORT; do
      if [ -z "${!v:-}" ]; then echo "FEHLER: $v fehlt (Geheimnis)"; exit 1; fi
    done
    signatur=(-p:AndroidKeyStore=true "-p:AndroidSigningKeyStore=$LERNPLATTFORM_SCHLUESSEL_DATEI"
              "-p:AndroidSigningKeyAlias=$LERNPLATTFORM_SCHLUESSEL_ALIAS"
              -p:AndroidSigningStorePass=env:LERNPLATTFORM_SCHLUESSEL_PASSWORT
              -p:AndroidSigningKeyPass=env:LERNPLATTFORM_SCHLUESSEL_KEY_PASSWORT)
    echo "Android: Release-Schluessel aus Geheimnissen"
  else
    echo "Android: kein Release-Schluessel, Testschluessel (nur Pruefung, nie Release)"
  fi
  dotnet publish "$projekt" -c Release -o "$ARBEIT/apk" "${signatur[@]}"
  apk=("$ARBEIT"/apk/*-Signed.apk)
  if [ "${#apk[@]}" -ne 1 ] || [ ! -f "${apk[0]}" ]; then echo "FEHLER: nicht genau eine signierte APK"; exit 1; fi
  bt="$(ls -d "$ANDROID_HOME"/build-tools/* | sort -V | tail -n 1)"
  "$bt/apksigner" verify --print-certs "${apk[0]}" | tr -d '\r' > "$ARBEIT/zertifikat.txt"
  cat "$ARBEIT/zertifikat.txt"
  local fp soll
  # apksigner schreibt je Signaturschema eine Zeile ("Signer #1 certificate ..." oder "V3.0 Signer: certificate ...");
  # alle muessen denselben Fingerabdruck nennen
  fp="$(sed -n 's/.*certificate SHA-256 digest: *//p' "$ARBEIT/zertifikat.txt" | tr 'A-F' 'a-f' | sort -u)"
  if [ -z "$fp" ] || [ "$(printf '%s\n' "$fp" | wc -l)" -ne 1 ]; then
    echo "FEHLER: nicht genau ein Zertifikat-Fingerabdruck in der APK: $fp"; exit 1
  fi
  # sed statt grep: eine Datei nur aus Kommentaren ist kein Fehler (grep gaebe 1, set -e braeche stumm ab)
  soll="$(sed -e '/^#/d' "$WURZEL/werkzeuge/release/android_fingerabdruck.txt" | tr -d ' \r\n:' | tr 'A-F' 'a-f')"
  echo "Android: Fingerabdruck des Zertifikats $fp"
  # T3: im Release nur der bekannte Schluessel
  if [ "${LERNPLATTFORM_RELEASE_STRENG:-0}" = "1" ]; then
    if [ -z "${LERNPLATTFORM_SCHLUESSEL_DATEI:-}" ]; then echo "FEHLER: Release ohne Release-Schluessel"; exit 1; fi
    if [ -z "$soll" ]; then echo "FEHLER: android_fingerabdruck.txt nennt noch keinen Fingerabdruck"; exit 1; fi
    if [ "$fp" != "$soll" ]; then echo "FEHLER: Fingerabdruck $fp ungleich android_fingerabdruck.txt"; exit 1; fi
  elif [ -n "$soll" ] && [ "$fp" = "$soll" ] && [ -z "${LERNPLATTFORM_SCHLUESSEL_DATEI:-}" ]; then
    echo "FEHLER: Testbau mit dem Release-Fingerabdruck"; exit 1
  fi
  "$bt/aapt2" dump badging "${apk[0]}" | head -n 1
  cp "${apk[0]}" "$ZIEL/$ID-$NAME-android-arm64.apk"
  "${LIZENZEN[@]}" sammeln android "$projekt/obj/project.assets.json" "$ZIEL/$ID-$NAME-android-arm64.apk" "$ZIEL/lizenzen-android.json"
}

case "$TEIL" in
  win-x64|linux-x64|osx-arm64) desktop "$TEIL" ;;
  android) android ;;
  *) echo "FEHLER: unbekanntes Bauteil $TEIL"; exit 1 ;;
esac
ls -la "$ZIEL"
echo "RELEASE-BAU $TEIL GRUEN"
