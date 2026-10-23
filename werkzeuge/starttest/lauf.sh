#!/usr/bin/env bash
# Start-Test (Start ohne Bedienung) mit Neustart-Regel, Weisung P4-13 (Nico 22.10.2026).
# Ein Lauf = ein neuer Prozess des fertig gebauten Desktop-Programms (Release, --no-build),
# unter Linux in Xvfb. Das Urteil faellt werkzeuge/starttest/urteil.py.
#
# Aufruf (aus dem Repo-Wurzelordner):
#   bash werkzeuge/starttest/lauf.sh 1 <messdatei_lauf1>
#   bash werkzeuge/starttest/lauf.sh 2 <messdatei_lauf2> <messdatei_lauf1>
# Der 2. Lauf braucht einen frischen Datenordner in LERNPLATTFORM_DATEN (setzt ci.yml).
# PYTHON waehlt den Python-Befehl (Vorgabe python).
set -u
nr=$1
datei=$2
export LERNPLATTFORM_MESSUNG_AUTO=1
export LERNPLATTFORM_MESSUNG_DATEI="$datei"
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
befehl=(bash -c '"$@"; r=$?; echo $r > "$0"; exit $r' "$rcdatei" dotnet run --no-build -c Release --project src/Lernplattform.Desktop)
if [ "${RUNNER_OS:-Linux}" = "Linux" ]; then
  befehl=(xvfb-run -a -s "-screen 0 1280x800x24" "${befehl[@]}")
fi
"${befehl[@]}"
rc=$?
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
