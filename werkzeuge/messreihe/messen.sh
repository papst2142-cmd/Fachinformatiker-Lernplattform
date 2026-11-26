#!/usr/bin/env bash
# Messreihe 438 (Engine festigen 3, A 1.16.0, Weisung P12-399 Antwort 1a): misst den kalten Bau der Stadt in vier Fassungen
# (Engine vorab uebersetzt ja/nein x TieredPGO Vorgabe/aus), je Fassung N frische Prozesse, abwechselnd der Reihe nach.
# Nur Messung: aendert keine Einstellung des Programms, keine Grenze, kein Urteil eines anderen Laufs.
#
# Aufruf (aus dem Repo-Wurzelordner, nach "dotnet build src/Lernplattform.Desktop -c Release"):
#   bash werkzeuge/messreihe/messen.sh <rid> <ziel_ordner> <starts_spiel> <starts_werkzeug>
# rid: win-x64, linux-x64, osx-arm64, osx-x64. starts_werkzeug 0 = Werkzeug nicht messen (am Mac immer 0: kein Werkzeug-Paket).
# Ergebnis: <ziel_ordner>/messung_<art>_<fassung>_<nr>.txt und die Tabelle <ziel_ordner>/ergebnis.md (auswerten.py).
set -u
rid=$1
ziel=$2
n_spiel=$3
n_werkzeug=$4
py=${PYTHON:-python}
mkdir -p "$ziel"
ziel=$(cd "$ziel" && pwd)
exe=""
case "$rid" in win-*) exe=".exe" ;; esac

bauen() {
  # $1 Projekt, $2 Ausgabe
  dotnet publish "$1" -c Release -r "$rid" --self-contained -p:DebugType=none -o "$2" > "$ziel/publish_$(basename "$2").log" 2>&1 \
    || { echo "FEHLER: publish $1"; tail -20 "$ziel/publish_$(basename "$2").log"; exit 1; }
}

echo "Messreihe 438: $rid, Spiel $n_spiel Starts je Fassung, Werkzeug $n_werkzeug Starts je Fassung"
# Engine einmal als reine IL-DLL (Bau) und einmal vorab uebersetzt (publish der Bibliothek mit ReadyToRun)
dotnet build src/Lernplattform.Engine -c Release > /dev/null 2>&1 || { echo "FEHLER: build Engine"; exit 1; }
il=src/Lernplattform.Engine/bin/Release/net10.0/Lernplattform.Engine.dll
dotnet publish src/Lernplattform.Engine -c Release -r "$rid" -p:PublishReadyToRun=true -o "$ziel/engine_r2r" > "$ziel/publish_engine.log" 2>&1 \
  || { echo "FEHLER: publish Engine"; tail -20 "$ziel/publish_engine.log"; exit 1; }
r2r="$ziel/engine_r2r/Lernplattform.Engine.dll"
"$py" -I werkzeuge/messreihe/r2r.py selbsttest "$il" "$r2r" || exit 1

fassung_bauen() {
  # $1 Projekt, $2 Name (spiel/werkzeug): zwei Ordner, die sich nur in der Engine-DLL unterscheiden
  bauen "$1" "$ziel/$2_vorab_ja"
  rm -rf "$ziel/$2_vorab_nein"
  cp -R "$ziel/$2_vorab_ja" "$ziel/$2_vorab_nein"
  cp "$r2r" "$ziel/$2_vorab_ja/Lernplattform.Engine.dll"
  cp "$il" "$ziel/$2_vorab_nein/Lernplattform.Engine.dll"
  "$py" -I werkzeuge/messreihe/r2r.py pruefen "$ziel/$2_vorab_ja/Lernplattform.Engine.dll" ja || exit 1
  "$py" -I werkzeuge/messreihe/r2r.py pruefen "$ziel/$2_vorab_nein/Lernplattform.Engine.dll" nein || exit 1
}

fassung_bauen src/Lernplattform.Desktop spiel
if [ "$n_werkzeug" -gt 0 ]; then
  fassung_bauen src/Stadtbau.Desktop werkzeug
fi

export LERNPLATTFORM_TESTKARTE="$PWD/tests/Lernplattform.Tests/Testdaten/testkarte_256.fistadt"
lauf() {
  # $1 art (spiel/werkzeug), $2 vorab (ja/nein), $3 pgo (vorgabe/aus), $4 nr
  local datei="$ziel/messung_$1_vorab-$2_pgo-$3_$4.txt"
  local daten
  daten=$(mktemp -d)
  local teil=kaltbau programm_var=LERNPLATTFORM_STARTTEST_PROGRAMM programm="$ziel/spiel_vorab_$2/Lernplattform$exe"
  if [ "$1" = werkzeug ]; then
    teil=stadtbau programm_var=LERNPLATTFORM_STARTTEST_WERKZEUG programm="$ziel/werkzeug_vorab_$2/Stadtbau$exe"
  fi
  local pgo_wert=""
  [ "$3" = aus ] && pgo_wert=0
  local start=$SECONDS
  if [ -n "$pgo_wert" ]; then
    env DOTNET_TieredPGO=0 LERNPLATTFORM_MESSUNG_TEIL=$teil LERNPLATTFORM_DATEN="$daten" "$programm_var=$programm" PYTHON="$py" \
      bash werkzeuge/starttest/lauf.sh 1 "$datei" > "$datei.log" 2>&1
  else
    env LERNPLATTFORM_MESSUNG_TEIL=$teil LERNPLATTFORM_DATEN="$daten" "$programm_var=$programm" PYTHON="$py" \
      bash werkzeuge/starttest/lauf.sh 1 "$datei" > "$datei.log" 2>&1
  fi
  echo "  $1 vorab $2, TieredPGO $3, Start $4: $((SECONDS - start)) s"
  rm -rf "$daten"
}

# abwechselnd: jede Runde jede Fassung einmal (gleiche Lage des Rechners fuer alle Fassungen)
for nr in $(seq 1 "$n_spiel"); do
  for vorab in ja nein; do for pgo in vorgabe aus; do lauf spiel $vorab $pgo $nr; done; done
done
for nr in $(seq 1 "$n_werkzeug"); do
  for vorab in ja nein; do for pgo in vorgabe aus; do lauf werkzeug $vorab $pgo $nr; done; done
done
"$py" -I werkzeuge/messreihe/auswerten.py "$ziel" "$rid" | tee "$ziel/ergebnis.md"
