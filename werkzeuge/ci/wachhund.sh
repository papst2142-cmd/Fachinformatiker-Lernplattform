#!/usr/bin/env bash
# Sammelliste 226 (W4b, Weisung P12-228): Wachhund fuer CI-Schritte ohne eigenen Wachhund (heute "Pakete bauen und
# Update pruefen" in pakete). Haengt der Schritt, greift sonst nur die Schrittzeit von GitHub, und es gibt keine
# Angabe, wo er stand. Aufruf: wachhund.sh <minuten> <befehl ...>. Nach <minuten> ohne Ende: Zeile "FEHLER: HAENGER",
# Prozessbaum mit CPU/Speicher/Laufzeit, unter macOS die Faden-Stapel des am laengsten laufenden Nachkommen ("sample"),
# dann den ganzen Prozessbaum beenden, Rueckgabe 4. Ohne Haenger: Rueckgabe des Befehls. Nur strenger: ohne Wachhund
# waere der Schritt genauso rot (Zeitlimit), nur ohne Angaben. Selbsttest: wachhund.sh selbsttest.
set -u

nachkommen() {
  local kinder k
  kinder=$(ps -A -o pid= -o ppid= | awk -v p="$1" '$2 == p { print $1 }')
  for k in $kinder; do
    echo "$k"
    nachkommen "$k"
  done
}

angaben() {
  local wurzel=$1 liste letzter
  liste=$(nachkommen "$wurzel")
  echo "Wachhund: Prozessbaum (pid, ppid, cpu, rss, Laufzeit, Befehl):"
  if [ -n "$liste" ]; then
    ps -o pid=,ppid=,pcpu=,rss=,etime=,command= -p "$(echo "$liste" | tr '\n' ',' | sed 's/,$//')" 2>/dev/null | cut -c1-300 || true
  fi
  letzter=$(echo "$liste" | tail -n 1)
  if [ -n "$letzter" ] && [ "$(uname)" = "Darwin" ] && command -v sample >/dev/null 2>&1; then
    echo "Wachhund: Faden-Stapel von $letzter (sample, 5 s):"
    sample "$letzter" 5 -mayDie 2>/dev/null | head -n 300 || true
  fi
}

beenden() {
  local liste
  liste=$(nachkommen "$1")
  [ -n "$liste" ] && kill -TERM $liste 2>/dev/null
  kill -TERM "$1" 2>/dev/null
  sleep 5
  liste=$(nachkommen "$1")
  [ -n "$liste" ] && kill -KILL $liste 2>/dev/null
  kill -KILL "$1" 2>/dev/null
  return 0
}

laufen() {
  local minuten=$1 sekunden wurzel frist
  shift
  sekunden=$(( minuten * 60 ))
  [ -n "${WACHHUND_SEKUNDEN_FUER_TESTS:-}" ] && sekunden=$WACHHUND_SEKUNDEN_FUER_TESTS
  "$@" &
  wurzel=$!
  frist=$(( $(date +%s) + sekunden ))
  while kill -0 "$wurzel" 2>/dev/null && [ "$(date +%s)" -lt "$frist" ]; do
    sleep 1
  done
  if kill -0 "$wurzel" 2>/dev/null; then
    echo "FEHLER: HAENGER: Schritt nach $minuten Minuten nicht beendet (Wachhund vor dem Schrittzeitlimit, Sammelliste 226): $*"
    angaben "$wurzel"
    beenden "$wurzel"
    wait "$wurzel" 2>/dev/null
    return 4
  fi
  wait "$wurzel"
}

if [ "${1:-}" = "selbsttest" ]; then
  # Gegenproben: Befehl endet rechtzeitig (Rueckgabe des Befehls), Befehl haengt (Rueckgabe 4, Zeile FEHLER, Kind beendet)
  WACHHUND_SEKUNDEN_FUER_TESTS=3 laufen 1 bash -c 'exit 7'
  [ $? -eq 7 ] || { echo "Selbsttest rot: Rueckgabe des Befehls fehlt"; exit 1; }
  kindpid=$(mktemp)
  ausgabe=$(WACHHUND_SEKUNDEN_FUER_TESTS=2 laufen 1 bash -c "sleep 300 & echo \$! > $kindpid; wait")
  rc=$?
  [ $rc -eq 4 ] || { echo "Selbsttest rot: Haenger gibt $rc statt 4"; exit 1; }
  echo "$ausgabe" | grep -q "^FEHLER: HAENGER" || { echo "Selbsttest rot: keine Zeile FEHLER: HAENGER"; exit 1; }
  if kill -0 "$(cat "$kindpid")" 2>/dev/null; then
    echo "Selbsttest rot: Kind $(cat "$kindpid") lebt noch"; exit 1
  fi
  rm -f "$kindpid"
  echo "Selbsttest gruen: Wachhund"
  exit 0
fi

minuten=${1:?Minuten fehlen}
shift
laufen "$minuten" "$@"
