#!/usr/bin/env bash
# S 0.6.0: beide Emulator-Proben im selben Emulator nacheinander (Job android): zuerst das Update der Lernplattform
# (werkzeuge/android/update_probe.sh, n -> n+1 -> n+2, nur wenn ihre Probe-APKs gebaut wurden, also nicht in der Stufe
# werkzeug), dann das Update des Stadtbau-Werkzeugs (werkzeuge/stadtbau/update_probe.sh, n -> n+1). Rot, wenn eine rot ist.
# Welle A 0.98.0 (Weisung P12-315, Sammelliste 391): nach der Update-Probe der Lernplattform die Touch-Probe der Sticks mit
# der dort installierten Probe-APK n+2 (werkzeuge/android/touch_probe.sh); ihr Ergebnis steht am Ende von emulator_probe_update.txt.
# Aufruf: emulator_proben.sh <ergebnis lernplattform-bau> <ergebnis werkzeug-bau> (je "success" oder anderes)
set -u
rc=0
if [ "${1:-}" = success ]; then
  bash werkzeuge/android/update_probe.sh updateprobe-0/*-Signed.apk updateprobe-1/*-Signed.apk updateprobe-2/*-Signed.apk emulator_probe_update.txt || rc=1
  bash werkzeuge/android/touch_probe.sh emulator_probe_update.txt || rc=1
fi
if [ "${2:-}" = success ]; then
  adb uninstall io.github.papst2142cmd.lernplattform > /dev/null 2>&1 || true
  bash werkzeuge/stadtbau/update_probe.sh stadtbau-probe-0/*-Signed.apk stadtbau-probe-1/*-Signed.apk emulator_probe_stadtbau.txt || rc=1
fi
exit $rc
