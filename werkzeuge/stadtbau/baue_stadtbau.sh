#!/usr/bin/env bash
# Phase 13 Welle 2 (Weisung P12-17): Pakete des Stadtbau-Werkzeugs (eigene Linie S, Tag vS<Version>).
# Aufruf: werkzeuge/stadtbau/baue_stadtbau.sh <win-x64|android> <ausgabeordner>
# Ergebnis: <aus>/stadtbau-<teil>/ mit
#   win-x64: Fachinformatiker-Stadtbau-win-Setup.exe, ...-full.nupkg, releases.stadtbau-win.json (Velopack-Kanal
#            stadtbau-win, die Lernplattform nutzt "win"), THIRD_PARTY_LICENSES.txt, THIRD_PARTY_NOTICES.txt, lizenzen-stadtbau-win-x64.json
#   android: Fachinformatiker-Stadtbau-S-<Version>-android-arm64.apk (Kennung io.github.papst2142cmd.stadtbau),
#            lizenzen-stadtbau-android.json
# Jede ausgelieferte Datei braucht einen Bestandteil (werkzeuge/lizenzen, LIZENZ_PROGRAMM=Stadtbau), sonst Abbruch.
# Prueft Name, Version und Kanal. Laedt nichts hoch und legt keinen Tag an (Release nur ueber stadtbau_release.yml, Chat).
# Android: mit LERNPLATTFORM_SCHLUESSEL_* (Geheimnisse) der Release-Schluessel, sonst der Testschluessel;
# STADTBAU_RELEASE_STRENG=1 verlangt den Release-Schluessel mit dem Fingerabdruck aus android_fingerabdruck.txt.
set -Eeuo pipefail
trap 'echo "FEHLER: Abbruch in Zeile $LINENO (Rueckgabe $?): $BASH_COMMAND"' ERR
TEIL="$1"
mkdir -p "$2"
AUS="$(cd "$2" && pwd)"
WURZEL="$(cd "$(dirname "$0")/../.." && pwd)"
PY="${PYTHON:-python3}"
ID="Fachinformatiker-Stadtbau"
KANAL="stadtbau-win"
ZIEL="$AUS/stadtbau-$TEIL"
ARBEIT="$AUS/arbeit-stadtbau-$TEIL"
if [ -e "$ZIEL" ] || [ -e "$ARBEIT" ]; then echo "FEHLER: $ZIEL oder $ARBEIT gibt es schon"; exit 1; fi
mkdir -p "$ZIEL" "$ARBEIT"
export LIZENZ_PROGRAMM=Stadtbau
LIZENZEN=("$PY" -I "$WURZEL/werkzeuge/lizenzen/erzeuge_lizenzen.py")
VERSION="$(sed -n 's:.*<StadtbauVersion>\([0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*\)</StadtbauVersion>.*:\1:p' "$WURZEL/Directory.Build.props")"
if [ -z "$VERSION" ]; then echo "FEHLER: StadtbauVersion in Directory.Build.props nicht gefunden"; exit 1; fi
TAG="vS$VERSION"
echo "Stadtbau-Bau $TEIL: S $VERSION, Tag $TAG"
# Notizen fuer das Paket: Abschnitt "## S <Version>" aus AENDERUNGEN_STADTBAU.md (ohne Abschnitt kein Bau)
awk -v kopf="## S $VERSION" '$0 == kopf { an = 1; next } an && /^## / { exit } an { print }' "$WURZEL/AENDERUNGEN_STADTBAU.md" > "$ARBEIT/notizen.md"
if ! grep -q '[^[:space:]]' "$ARBEIT/notizen.md"; then echo "FEHLER: kein Abschnitt \"## S $VERSION\" in AENDERUNGEN_STADTBAU.md"; exit 1; fi

windows() {
  local pub="$ARBEIT/pub" feed="$ARBEIT/feed"
  dotnet publish "$WURZEL/src/Stadtbau.Desktop" -c Release -r win-x64 --self-contained -p:DebugType=none -o "$pub"
  if [ ! -f "$pub/Stadtbau.exe" ]; then echo "FEHLER: Stadtbau.exe fehlt im Bau"; exit 1; fi
  if [ -e "$pub/Lernplattform.exe" ]; then echo "FEHLER: Lernplattform.exe im Werkzeug"; exit 1; fi
  cp "$WURZEL/LICENSE.txt" "$pub/LICENSE.txt"
  "${LIZENZEN[@]}" sammeln desktop win-x64 "$pub" "$ARBEIT/vorab.json"
  "${LIZENZEN[@]}" schreiben "$ARBEIT/uebersicht.txt" "$pub/THIRD_PARTY_LICENSES.txt" "$ARBEIT/vorab.json"
  # die Uebersicht im Programm (Lernplattform.App.dll) gilt der Lernplattform; die des Werkzeugs liegt daneben
  cp "$ARBEIT/uebersicht.txt" "$pub/THIRD_PARTY_NOTICES.txt"
  vpk pack -u "$ID" -v "$VERSION" -p "$pub" -e Stadtbau.exe -i "$WURZEL/src/Stadtbau.App/Assets/icon.ico" \
    --packTitle "Fachinformatiker Stadtbau" --releaseNotes "$ARBEIT/notizen.md" -c "$KANAL" -o "$feed"
  ls -la "$feed"
  local setup nupkg="$feed/$ID-$VERSION-$KANAL-full.nupkg" json="$feed/releases.$KANAL.json"
  setup="$(ls "$feed"/*Setup.exe | head -n 1)"
  for f in "$setup" "$nupkg" "$json"; do
    if [ ! -f "$f" ]; then echo "FEHLER: $f fehlt (Kanal $KANAL)"; ls -la "$feed"; exit 1; fi
  done
  if [ -e "$feed/releases.win.json" ]; then echo "FEHLER: Kanal win (der Lernplattform) im Werkzeug"; exit 1; fi
  "$PY" -I - "$json" "$VERSION" <<'PY'
import json, sys
d = json.load(open(sys.argv[1], encoding="utf-8-sig"))
eintraege = d.get("Assets") or d.get("assets") or []
versionen = sorted({e.get("Version") or e.get("version") for e in eintraege})
ids = sorted({e.get("PackageId") or e.get("packageId") for e in eintraege})
print("Feed: Pakete %s, Versionen %s" % (ids, versionen))
if ids != ["Fachinformatiker-Stadtbau"] or versionen != [sys.argv[2]]:
    print("FEHLER: Feed nennt nicht genau Fachinformatiker-Stadtbau %s" % sys.argv[2]); sys.exit(1)
PY
  cp "$setup" "$ZIEL/$ID-win-Setup.exe"
  cp "$nupkg" "$json" "$pub/THIRD_PARTY_LICENSES.txt" "$pub/THIRD_PARTY_NOTICES.txt" "$ZIEL/"
  "${LIZENZEN[@]}" sammeln desktop win-x64 "$pub" "$ZIEL/lizenzen-stadtbau-win-x64.json" "$ZIEL/$ID-win-Setup.exe" "$ZIEL/$(basename "$nupkg")"
}

android() {
  local projekt="$WURZEL/src/Stadtbau.Android" signatur=() bt apk
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
    echo "Android: Testschluessel (nur Pruefung, nie Release)"
  fi
  dotnet publish "$projekt" -c Release -o "$ARBEIT/apk" "${signatur[@]}"
  apk=("$ARBEIT"/apk/*-Signed.apk)
  if [ "${#apk[@]}" -ne 1 ] || [ ! -f "${apk[0]}" ]; then echo "FEHLER: nicht genau eine signierte APK"; exit 1; fi
  bt="$(ls -d "$ANDROID_HOME"/build-tools/* | sort -V | tail -n 1)"
  "$bt/aapt2" dump badging "${apk[0]}" | tr -d '\r' > "$ARBEIT/badging.txt"
  head -n 1 "$ARBEIT/badging.txt"
  local code
  code=$(( 1000000 + 10#$(echo "$VERSION" | cut -d. -f1) * 10000 + 10#$(echo "$VERSION" | cut -d. -f2) * 100 + 10#$(echo "$VERSION" | cut -d. -f3) ))
  grep -q "package: name='io.github.papst2142cmd.stadtbau' versionCode='$code' versionName='S $VERSION'" "$ARBEIT/badging.txt" \
    || { echo "FEHLER: Kennung, Versionscode $code oder Anzeige S $VERSION falsch"; exit 1; }
  grep -q "application-label:'Fachinformatiker Stadtbau'" "$ARBEIT/badging.txt" || { echo "FEHLER: App-Name falsch"; exit 1; }
  local abis
  abis="$(unzip -l "${apk[0]}" | awk '{print $4}' | sed -n 's:^lib/\([^/]*\)/.*:\1:p' | sort -u | tr '\n' ' ')"
  echo "Android: Bibliotheken fuer $abis"
  if [ "$abis" != "arm64-v8a " ]; then echo "FEHLER: APK nicht nur arm64-v8a ($abis)"; exit 1; fi
  # gleicher Schluessel wie die Lernplattform (android_fingerabdruck.txt); STADTBAU_RELEASE_STRENG=1 (stadtbau_release.yml):
  # ohne Release-Schluessel oder mit anderem Fingerabdruck FEHLER; ein Testbau nie mit dem Release-Fingerabdruck
  local fp soll
  "$bt/apksigner" verify --print-certs "${apk[0]}" | tr -d '\r' > "$ARBEIT/zertifikat.txt"
  fp="$(sed -n 's/.*certificate SHA-256 digest: *//p' "$ARBEIT/zertifikat.txt" | tr 'A-F' 'a-f' | sort -u)"
  if [ -z "$fp" ] || [ "$(printf '%s\n' "$fp" | wc -l)" -ne 1 ]; then echo "FEHLER: nicht genau ein Zertifikat-Fingerabdruck: $fp"; exit 1; fi
  soll="$(sed -e '/^#/d' "$WURZEL/werkzeuge/release/android_fingerabdruck.txt" | tr -d ' \r\n:' | tr 'A-F' 'a-f')"
  echo "Android: Fingerabdruck des Zertifikats $fp"
  if [ "${STADTBAU_RELEASE_STRENG:-0}" = "1" ]; then
    if [ -z "${LERNPLATTFORM_SCHLUESSEL_DATEI:-}" ]; then echo "FEHLER: Release ohne Release-Schluessel"; exit 1; fi
    if [ -z "$soll" ] || [ "$fp" != "$soll" ]; then echo "FEHLER: Fingerabdruck $fp ungleich android_fingerabdruck.txt"; exit 1; fi
  elif [ -n "$soll" ] && [ "$fp" = "$soll" ] && [ -z "${LERNPLATTFORM_SCHLUESSEL_DATEI:-}" ]; then
    echo "FEHLER: Testbau mit dem Release-Fingerabdruck"; exit 1
  fi
  cp "${apk[0]}" "$ZIEL/$ID-S-$VERSION-android-arm64.apk"
  "${LIZENZEN[@]}" sammeln android "$projekt/obj/project.assets.json" "$ZIEL/$ID-S-$VERSION-android-arm64.apk" "$ZIEL/lizenzen-stadtbau-android.json"
}

case "$TEIL" in
  win-x64) windows ;;
  android) android ;;
  *) echo "FEHLER: unbekanntes Bauteil $TEIL"; exit 1 ;;
esac
ls -la "$ZIEL"
