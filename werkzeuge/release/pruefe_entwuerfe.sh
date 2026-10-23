#!/usr/bin/env bash
# Phase 6 W1 (Weisung P6-2 Punkt 2): Regeln fuer Entwuerfe und Releases vor jedem Hochladen in release.yml.
# Laeuft nur in den Jobs mit contents: write, weil GitHub Entwuerfe nur mit Schreibrecht auflistet.
# Aufruf: pruefe_entwuerfe.sh <entwurf|veroeffentlichen> <tag> <commit>
# Schreibt die id des Entwurfs (oder leer) nach entwurf_id.txt. Loescht und aendert nichts.
set -euo pipefail
ART="$1"; TAG="$2"; SHA="$3"
gh api "repos/$GITHUB_REPOSITORY/releases?per_page=100" --paginate --slurp > releases_seiten.json
python -I - "$ART" "$TAG" "$SHA" <<'PY'
import json, sys
art, tag, sha = sys.argv[1:4]
alle = [r for seite in json.load(open("releases_seiten.json", encoding="utf-8")) for r in seite]
for r in alle:
    print("Release: %s, Entwurf %s, Ziel %s" % (r["tag_name"], r["draft"], r["target_commitish"]))
def stopp(text):
    print("FEHLER: " + text)
    sys.exit(1)
if any(r["tag_name"] == tag and not r["draft"] for r in alle):
    stopp("ein veroeffentlichtes Release %s gibt es schon" % tag)
entwuerfe = [r for r in alle if r["draft"]]
if len(entwuerfe) > 1:
    stopp("mehr als ein Entwurf (hoechstens einer; Loeschen macht nur Nico)")
if entwuerfe and entwuerfe[0]["tag_name"] != tag:
    stopp("es gibt schon einen Entwurf mit anderem Tag (%s)" % entwuerfe[0]["tag_name"])
if art == "veroeffentlichen":
    if not entwuerfe:
        stopp("kein Entwurf %s zum Veroeffentlichen" % tag)
    if entwuerfe[0]["target_commitish"] != sha:
        stopp("der Entwurf gehoert zu %s, nicht zu diesem Commit %s" % (entwuerfe[0]["target_commitish"], sha))
open("entwurf_id.txt", "w").write(str(entwuerfe[0]["id"]) if entwuerfe else "")
print("Entwuerfe ok: %s" % ("Entwurf %s ersetzen" % tag if entwuerfe else "neuer Entwurf"))
PY
