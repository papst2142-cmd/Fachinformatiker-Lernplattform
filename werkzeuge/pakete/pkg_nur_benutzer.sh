#!/usr/bin/env bash
# Patch A 0.37.0 (Sammelliste 203, Weisung P11-34 Antwort 1c): das .pkg von vpk erlaubt fest die Installation
# "fuer alle Benutzer" nach /Applications (Dateien gehoeren dann root, das Update im Programm scheitert still).
# Dieses Werkzeug schaltet in der Distribution genau diese Wahl ab (enable_localSystem="false"); der Installer
# bietet danach nur noch "Nur fuer mich" an (~/Applications). Das Paket ist nicht signiert, es geht keine
# Signatur kaputt. Bricht ab, wenn die erwartete Zeile fehlt oder sich mehr als genau diese Stelle aendert.
# Aufruf (nur macOS): werkzeuge/pakete/pkg_nur_benutzer.sh <paket.pkg>   (ersetzt das Paket an seiner Stelle)
set -euo pipefail
PKG="$1"
test -f "$PKG" || { echo "FEHLER: $PKG fehlt"; exit 1; }
ARBEIT="$(mktemp -d)"
trap 'rm -rf "$ARBEIT"' EXIT
pkgutil --expand "$PKG" "$ARBEIT/x"
DIST="$ARBEIT/x/Distribution"
test -f "$DIST" || { echo "FEHLER: keine Distribution im Paket"; exit 1; }
ALT='enable_localSystem="true"'
NEU='enable_localSystem="false"'
test "$(grep -c "$ALT" "$DIST")" -eq 1 || { echo "FEHLER: $ALT nicht genau einmal in der Distribution"; grep -n "domains" "$DIST" || true; exit 1; }
grep -q 'enable_currentUserHome="true"' "$DIST" || { echo "FEHLER: enable_currentUserHome=\"true\" fehlt"; exit 1; }
cp "$DIST" "$ARBEIT/Distribution.vorher"
sed -i '' "s/$ALT/$NEU/" "$DIST"
# genau eine geaenderte Zeile, und sie unterscheidet sich nur in dieser Stelle
ANZ="$(diff "$ARBEIT/Distribution.vorher" "$DIST" | grep -c '^[<>]' || true)"
test "$ANZ" -eq 2 || { echo "FEHLER: $ANZ geaenderte Zeilen statt genau einer"; diff "$ARBEIT/Distribution.vorher" "$DIST" || true; exit 1; }
test "$(diff "$ARBEIT/Distribution.vorher" "$DIST" | grep '^<' | sed "s/$ALT/$NEU/")" = "$(diff "$ARBEIT/Distribution.vorher" "$DIST" | grep '^>' | sed 's/^>/</')" \
  || { echo "FEHLER: die Zeile aendert sich an mehr als einer Stelle"; exit 1; }
rm -f "$PKG"
pkgutil --flatten "$ARBEIT/x" "$PKG"
# Nachkontrolle am fertigen Paket
pkgutil --expand "$PKG" "$ARBEIT/kontrolle"
grep -q "$NEU" "$ARBEIT/kontrolle/Distribution" || { echo "FEHLER: fertiges Paket ohne $NEU"; exit 1; }
echo "pkg nur fuer den Benutzer: $(basename "$PKG") ($(grep -o '<domains[^>]*>' "$ARBEIT/kontrolle/Distribution"))"
