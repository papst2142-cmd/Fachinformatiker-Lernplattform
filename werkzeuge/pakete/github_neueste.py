"""A 0.53.2 (Sammelliste 256): hoechste veroeffentlichte Version einer Linie laut GitHub-API (ohne Entwurf und Vorab).

Aufruf: python3 -I werkzeuge/pakete/github_neueste.py <praefix>   (z. B. vA, vS); Ausgabe "0.53.1".
Nur lesend. Stoerung bei GitHub (5xx wie 504, keine Antwort): bis zu 4 Versuche mit Pausen 15/30/60 s wie
Stoerung.WiederholenAsync im Programm, je Wiederholung eine Zeile auf stderr; 4xx sofort Fehler. Gleiche Regel wie
der Abschnitt in werkzeuge/stadtbau/update_probe_pc.sh.
"""
import json
import re
import sys
import time
import urllib.error
import urllib.request

URL = "https://api.github.com/repos/papst2142-cmd/Fachinformatiker-Lernplattform/releases?per_page=100"


def laden():
    anfrage = urllib.request.Request(URL, headers={"Accept": "application/vnd.github+json",
                                                   "User-Agent": "lernplattform-update-probe"})
    for versuch, pause in enumerate([15, 30, 60, None], 1):
        try:
            return json.load(urllib.request.urlopen(anfrage, timeout=60))
        except urllib.error.HTTPError as x:
            if x.code < 500 or pause is None:
                raise
            grund = "HTTP %d" % x.code
        except (urllib.error.URLError, TimeoutError) as x:
            if pause is None:
                raise
            grund = str(x)
        print("WIEDERHOLUNG GitHub-API Versuch %d nach %d s (%s)" % (versuch + 1, pause, grund), file=sys.stderr, flush=True)
        time.sleep(pause)
    return []


def hoechste(daten, praefix):
    v = [tuple(int(x) for x in m.groups()) for d in daten if not d.get("draft") and not d.get("prerelease")
         for m in [re.fullmatch(re.escape(praefix) + r"(\d+)\.(\d+)\.(\d+)", d.get("tag_name", ""))] if m]
    return ".".join(map(str, max(v))) if v else ""


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit("Aufruf: github_neueste.py <praefix>")
    ergebnis = hoechste(laden(), sys.argv[1])
    if not ergebnis:
        sys.exit("keine veroeffentlichte Version %s gefunden" % sys.argv[1])
    print(ergebnis)
