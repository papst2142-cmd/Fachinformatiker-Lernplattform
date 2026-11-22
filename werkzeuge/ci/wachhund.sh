#!/usr/bin/env bash
# Sammelliste 226 (W4b, Weisung P12-228): Wachhund fuer CI-Schritte ohne eigenen Wachhund (heute "Pakete bauen und
# Update pruefen" in pakete). Haengt der Schritt, greift sonst nur die Schrittzeit von GitHub, und es gibt keine
# Angabe, wo er stand. Aufruf: wachhund.sh <minuten> <befehl ...>. Nach <minuten> ohne Ende: Zeile "FEHLER: HAENGER",
# Prozessbaum mit CPU/Speicher/Laufzeit, unter macOS die Faden-Stapel des am laengsten laufenden Nachkommen ("sample"),
# dann den ganzen Prozessbaum beenden, Rueckgabe 4. Ohne Haenger: Rueckgabe des Befehls. Nur strenger: ohne Wachhund
# waere der Schritt genauso rot (Zeitlimit), nur ohne Angaben. Selbsttest: wachhund.sh selbsttest.
# Welle Fehlerpruefung Teil 2 (Weisung P12-345, Sammelliste 424): vor dem Beenden werden zusaetzlich die Faden-Stapel jedes
# Nachkommen gesichert, dessen Befehl "crossgen2" enthaelt (Lauf 1162: dotnet publish hing 29 min in crossgen2 bei 0 % CPU).
# Datei je Prozess in $WACHHUND_STAPEL (Vorgabe wachhund_stapel/), Anfang im Log; die CI laedt den Ordner bei Rot hoch.
# Nur Diagnose: keine Zeit und keine Grenze geaendert.
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

stapel_sichern() {
  local wurzel=$1 ordner=${WACHHUND_STAPEL:-wachhund_stapel} p befehl datei t
  for p in $(nachkommen "$wurzel"); do
    befehl=$(ps -o command= -p "$p" 2>/dev/null)
    case "$(echo "$befehl" | tr '[:upper:]' '[:lower:]')" in *crossgen2*) ;; *) continue ;; esac
    mkdir -p "$ordner"
    datei="$ordner/crossgen2_$p.txt"
    {
      echo "Wachhund: crossgen2 pid $p, Befehl: $befehl"
      ps -o pid=,ppid=,pcpu=,rss=,etime=,state= -p "$p" 2>/dev/null || true
      if [ "$(uname)" = "Darwin" ] && command -v sample >/dev/null 2>&1; then
        sample "$p" 5 -mayDie 2>&1 || true
      elif [ -d "/proc/$p/task" ]; then
        for t in /proc/"$p"/task/*; do
          echo "--- Faden ${t##*/} ($(cat "$t/comm" 2>/dev/null)), Zustand $(awk '{print $3}' "$t/stat" 2>/dev/null), wartet in $(cat "$t/wchan" 2>/dev/null)"
          cat "$t/stack" 2>/dev/null || true
        done
        if command -v gdb >/dev/null 2>&1; then
          timeout 60 gdb -p "$p" -batch -ex "thread apply all bt" 2>&1 | head -n 2000 || true
        fi
      fi
    } > "$datei" 2>&1
    echo "Wachhund: Stapel von crossgen2 (pid $p) gesichert in $datei ($(wc -l < "$datei" | tr -d ' ') Zeilen), Anfang:"
    head -n 80 "$datei"
  done
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
    stapel_sichern "$wurzel"
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
  # Sammelliste 424: haengt ein Prozess namens crossgen2, liegt danach je Prozess eine Stapel-Datei vor und er ist beendet;
  # Gegenprobe: ein haengender Prozess mit anderem Namen bekommt keine Datei
  ordner=$(mktemp -d)
  kindpid=$(mktemp)
  ausgabe=$(WACHHUND_STAPEL="$ordner/s" WACHHUND_SEKUNDEN_FUER_TESTS=2 laufen 1 bash -c "(exec -a crossgen2 sleep 300) & echo \$! > $kindpid; sleep 300 & wait")
  rc=$?
  [ $rc -eq 4 ] || { echo "Selbsttest rot: Haenger crossgen2 gibt $rc statt 4"; exit 1; }
  cg=$(cat "$kindpid")
  [ -s "$ordner/s/crossgen2_$cg.txt" ] || { echo "Selbsttest rot: keine Stapel-Datei fuer crossgen2 (pid $cg)"; echo "$ausgabe"; exit 1; }
  grep -q "^Wachhund: crossgen2 pid $cg" "$ordner/s/crossgen2_$cg.txt" || { echo "Selbsttest rot: Stapel-Datei ohne Kopf"; exit 1; }
  echo "$ausgabe" | grep -q "Stapel von crossgen2 (pid $cg) gesichert" || { echo "Selbsttest rot: keine Zeile im Log"; exit 1; }
  [ "$(ls "$ordner/s" | wc -l | tr -d ' ')" = 1 ] || { echo "Selbsttest rot: Stapel-Datei auch fuer andere Prozesse: $(ls "$ordner/s")"; exit 1; }
  if kill -0 "$cg" 2>/dev/null; then echo "Selbsttest rot: crossgen2 $cg lebt noch"; exit 1; fi
  rm -rf "$ordner" "$kindpid"
  echo "Selbsttest gruen: Wachhund"
  exit 0
fi

minuten=${1:?Minuten fehlen}
shift
laufen "$minuten" "$@"
