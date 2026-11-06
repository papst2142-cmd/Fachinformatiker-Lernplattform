#!/usr/bin/env bash
# Start-Test (Start ohne Bedienung) mit Neustart-Regel, Weisung P4-13 (Nico 22.10.2026).
# Ein Lauf = ein neuer Prozess des fertig gebauten Desktop-Programms (Release, --no-build),
# unter Linux in Xvfb. Das Urteil faellt werkzeuge/starttest/urteil.py.
#
# Aufruf (aus dem Repo-Wurzelordner):
#   bash werkzeuge/starttest/lauf.sh 1 <messdatei_lauf1>
#   bash werkzeuge/starttest/lauf.sh 2 <messdatei_lauf2> <messdatei_lauf1>
# Der 2. Lauf braucht einen frischen Datenordner in LERNPLATTFORM_DATEN (setzt ci.yml).
# Ob ein 2. Lauf folgt, sagt urteil.py lauf1 ueber die Ausgabe zweiter_lauf=ja|nein (Weisung P11-38).
# PYTHON waehlt den Python-Befehl (Vorgabe python).
set -u
nr=$1
datei=$2
export LERNPLATTFORM_MESSUNG_AUTO=1
export LERNPLATTFORM_MESSUNG_DATEI="$datei"
# Phase 8 W6: festes Datum ohne Festzeit (Teilplan P8-11 Abschnitt 4), die CI haengt nie vom echten Tag ab;
# die Festzeiten setzt der Start-Test selbst (Festzeit.Probe). Ein ungueltiger Wert bricht den Start-Test ab.
export LERNPLATTFORM_DATUM=2026-06-15
# Phase 8 W8: Pruefstand stumm (stiller Tondienst, zaehlt nur die Anlaesse)
export LERNPLATTFORM_TON=aus
# Beleg frischer Datenordner (das Programm liest LERNPLATTFORM_DATEN in Core/Daten/Ablage.cs)
if [ -n "${LERNPLATTFORM_DATEN:-}" ]; then
  if [ -d "$LERNPLATTFORM_DATEN" ]; then
    echo "Datenordner vor dem Start: $LERNPLATTFORM_DATEN, Eintraege: $(find "$LERNPLATTFORM_DATEN" -mindepth 1 | wc -l | tr -d ' ')"
  else
    echo "Datenordner vor dem Start: $LERNPLATTFORM_DATEN, nicht vorhanden (leer)"
  fi
fi
# Die Rueckgabe des Programms selbst zaehlt (eine Datei aus der inneren Huelle), nicht die von
# xvfb-run: das meldet z. B. 5, wenn nur sein eigenes Aufraeumen misslingt
rcdatei=$(mktemp)
# Phase 13 Welle 2 (Weisung P12-17): Teil "stadtbau" startet das Stadtbau-Werkzeug statt der Lernplattform
projekt=src/Lernplattform.Desktop
programm_name=Lernplattform
if [ "${LERNPLATTFORM_MESSUNG_TEIL:-}" = "stadtbau" ]; then
  projekt=src/Stadtbau.Desktop
  programm_name=Stadtbau
fi
befehl=(bash -c '"$@"; r=$?; echo $r > "$0"; exit $r' "$rcdatei" dotnet run --no-build -c Release --project "$projekt")
if [ "${RUNNER_OS:-Linux}" = "Linux" ]; then
  # Phase 7 W1: Xvfb hat nur den Mesa-Softwaretreiber llvmpipe, den Avalonia fuer OpenGL sperrt;
  # der Start-Test erlaubt ihn, damit der Pflicht-Messpunkt "GL-Zusammenhang erzeugt" messbar ist
  export LERNPLATTFORM_GL_LLVMPIPE=1
  # Phase 11 W3 (Sammelliste 196): der Bildschirm ist groesser als die PC-Groesse 1360 x 880. Bei 1280 x 800
  # kappte das Programm das Fenster still auf 1280 x 800, und "pc" pruefte nie die echte PC-Groesse (der Wort-Umbruch
  # "Netzwerkdesig|n" blieb unter Linux gruen). LERNPLATTFORM_VOLLE_PC_GROESSE macht ein Kappen zum Layoutfund.
  export LERNPLATTFORM_VOLLE_PC_GROESSE=1
  befehl=(xvfb-run -a -s "-screen 0 1600x1000x24" "${befehl[@]}")
fi
# Sammelliste 199 (Weisung P11-22): Wachhund VOR dem Schrittzeitlimit der CI. Lauf 458 (Intel-Mac, 2. Lauf) stand
# 25 Minuten ohne Ausgabe, bis GitHub den Schritt abbrach; der Prozess lebte danach weiter (bis Jobende) und es
# gab keine Angabe, wo er stand. STARTTEST_WACHHUND_MINUTEN (setzt ci.yml: Schrittzeit minus 3 Minuten) beendet
# den Lauf vorher selbst: Zeile "FEHLER: HAENGER", Abruf ueber USR1 (Haenger.cs schreibt Seite, Vorgang und
# Wartepunkte nach haenger.log), unter macOS zusaetzlich die Faden-Stapel ueber "sample", Ausgabe von haenger.log
# und fehler.log, dann den ganzen Prozessbaum beenden. Rueckgabe 4 = Haenger (urteil.py: sofort rot, kein
# Neustart). Nur strenger: ohne Wachhund waere der Schritt genauso rot (Zeitlimit), nur ohne Angaben.
nachkommen() {
  # alle Nachkommen eines Prozesses (ps gibt es unter Linux und macOS gleich)
  local kinder k
  kinder=$(ps -A -o pid= -o ppid= | awk -v p="$1" '$2 == p { print $1 }')
  for k in $kinder; do
    echo "$k"
    nachkommen "$k"
  done
}
haenger_angaben() {
  local wurzel=$1 pid name ordner
  pid=""
  for k in $(nachkommen "$wurzel"); do
    name=$(ps -o comm= -p "$k" 2>/dev/null)
    if [ "${name##*/}" = "$programm_name" ]; then
      pid=$k
    fi
  done
  echo "Wachhund: Programm-Prozess ${pid:-nicht gefunden}, Prozessbaum:"
  ps -o pid=,ppid=,pcpu=,rss=,etime=,comm= -p "$(nachkommen "$wurzel" | tr '\n' ',' | sed 's/,$//')" 2>/dev/null || true
  if [ -n "$pid" ]; then
    # Abruf: Linux USR1 = 10, macOS USR1 = 30; kill -USR1 waehlt die richtige Nummer selbst
    kill -USR1 "$pid" 2>/dev/null || true
    sleep 5
    if [ "$(uname)" = "Darwin" ] && command -v sample >/dev/null 2>&1; then
      echo "Wachhund: Faden-Stapel (sample, 5 s):"
      sample "$pid" 5 -mayDie 2>/dev/null | head -n 400 || true
    fi
  fi
  for ordner in "${LERNPLATTFORM_DATEN:-}" "${LERNPLATTFORM_DATEN:-/nicht}/stadtbau" "$HOME/Library/Application Support/Fachinformatiker-Lernplattform" \
    "$HOME/.local/share/fachinformatiker-lernplattform"; do
    [ -n "$ordner" ] || continue
    for log in haenger.log fehler.log messpunkte.log; do
      if [ -s "$ordner/$log" ]; then
        echo "Wachhund: $ordner/$log (letzte 80 Zeilen):"
        tail -n 80 "$ordner/$log"
      fi
    done
  done
}
baum_beenden() {
  local liste
  liste=$(nachkommen "$1")
  [ -n "$liste" ] && kill -TERM $liste 2>/dev/null
  sleep 5
  liste=$(nachkommen "$1")
  [ -n "$liste" ] && kill -KILL $liste 2>/dev/null
  kill -KILL "$1" 2>/dev/null
  return 0
}
wachhund=${STARTTEST_WACHHUND_MINUTEN:-0}
if [ "$wachhund" -gt 0 ] 2>/dev/null && [ "${RUNNER_OS:-Linux}" != "Windows" ]; then
  "${befehl[@]}" &
  wurzel=$!
  frist=$(( $(date +%s) + wachhund * 60 ))
  while kill -0 "$wurzel" 2>/dev/null && [ "$(date +%s)" -lt "$frist" ]; do
    sleep 5
  done
  if kill -0 "$wurzel" 2>/dev/null; then
    echo "FEHLER: HAENGER: Start-Test nach $wachhund Minuten nicht beendet (Wachhund vor dem Schrittzeitlimit, Sammelliste 199)"
    haenger_angaben "$wurzel"
    baum_beenden "$wurzel"
    wait "$wurzel" 2>/dev/null
    : > "$rcdatei"
    rc=4
  else
    wait "$wurzel"
    rc=$?
  fi
else
  "${befehl[@]}"
  rc=$?
fi
if [ -s "$rcdatei" ]; then
  programm=$(cat "$rcdatei")
  if [ "$programm" != "$rc" ]; then
    echo "Hinweis: Huelle meldet $rc, das Programm $programm (es zaehlt das Programm)"
  fi
  rc=$programm
fi
echo "Rueckgabe ${nr}. Lauf: $rc"
if [ "$nr" = "1" ]; then
  # genau eine Quelle fuer die Ausgabe rc, aus bash ohne \r (Weisung P4-14 b)
  if [ -n "${GITHUB_OUTPUT:-}" ]; then
    printf 'rc=%s\n' "$rc" >> "$GITHUB_OUTPUT"
  fi
  exec "${PYTHON:-python}" -I werkzeuge/starttest/urteil.py lauf1 "$rc" "$datei"
fi
exec "${PYTHON:-python}" -I werkzeuge/starttest/urteil.py lauf2 "$rc" "$3" "$datei"
