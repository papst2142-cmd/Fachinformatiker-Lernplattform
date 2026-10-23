#!/usr/bin/env bash
# Phase 6 W1 (VE1): Tag-Pruefungen fuer release.yml, nur lesend (git ls-remote auf origin).
#   pruefe_tag.sh fehlt <tag>               den Tag gibt es noch nicht (sonst FEHLER)
#   pruefe_tag.sh zeigt-auf <tag> <commit>  der Tag zeigt auf genau diesen Commit
# Ein Netz- oder Rechtefehler ist immer FEHLER, nie "Tag fehlt".
set -euo pipefail
ART="$1"; TAG="$2"
rc=0
ausgabe="$(git ls-remote --exit-code --tags origin "refs/tags/$TAG" "refs/tags/$TAG^{}")" || rc=$?
case "$ART:$rc" in
  fehlt:2) echo "Tag $TAG gibt es noch nicht" ;;
  fehlt:0) echo "FEHLER: Tag $TAG gibt es schon (ein Tag wird nie ueberschrieben)"; exit 1 ;;
  zeigt-auf:0)
    # bei einem annotierten Tag zaehlt der Commit hinter ^{}
    ziel="$(printf '%s\n' "$ausgabe" | awk -v t="refs/tags/$TAG^{}" '$2 == t {print $1}')"
    [ -n "$ziel" ] || ziel="$(printf '%s\n' "$ausgabe" | awk -v t="refs/tags/$TAG" '$2 == t {print $1}')"
    if [ "$ziel" != "$3" ]; then
      echo "FEHLER: Tag $TAG zeigt auf '$ziel' statt $3 (Nico und Chat melden, nichts loeschen)"; exit 1
    fi
    echo "Tag $TAG zeigt auf $3"
    ;;
  zeigt-auf:2) echo "FEHLER: Tag $TAG fehlt nach dem Veroeffentlichen"; exit 1 ;;
  *) echo "FEHLER: git ls-remote endete mit $rc (Netz oder Rechte), kein Urteil moeglich"; exit 1 ;;
esac
