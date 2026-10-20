#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G10 (Phase 1, K4 Fehlerprotokoll): Beispieltexte,
gerechnet vom echten Python-Code der Python-Linie:

  1. scrub(text) und scrub(text, secrets) aus fisi_diagnose (Zugangsschluessel
     und Passwoerter entfernen),
  2. secrets_of(settings) aus fisi_diagnose,
  3. anonymize_paths(text, env, home) aus fisi_core (Benutzerpfade ersetzen),
     einmal mit Windows-Werten, einmal mit Linux-Werten,
  4. die Kombination wie in write_error_log: anonymize_paths(scrub(text.rstrip())).

Die Texte sind ausgedacht (keine echten Daten, keine echten Schluessel).

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_fehlerprotokoll.py <pfad_fisi_lernplattform> tests/Vergleich/fehlerprotokoll.json
"""
import json
import os
import sys
import tempfile

TEXTE = [
    "",
    "Alles in Ordnung, kein Geheimnis hier.",
    "Fehler beim Abgleich: token=ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789",
    "Zugang github_pat_11ABCDEFG0123456789_abcdefghijklmnopqrstuvwxyz gesperrt",
    "Authorization: Bearer abcdef1234567890",
    "headers={'Authorization': 'token geheim12345', 'Accept': 'json'}",
    "authorization=Basic dXNlcjpwYXNz",
    "Anfrage mit bearer AbC.dEf_123-456~789+/= gescheitert",
    "sync_token = 'abc123456' und api_key: \"xyz987654\"",
    "{\"sync_token\": \"geheimerWert1\", \"sync_repo\": \"nico/fisi-lernstand\"}",
    "passwort=hallo123; kennwort: Welt456, secret=\"s e c r e t\"",
    "Mein-Passwort: abc und passwd=xyz und PASSWORD=Gross1",
    "api-key=k1 apikey=k2 API_KEY=k3 x-api-key: k4",
    "ein tokenizer=harmlos, mytoken=weg, token_typ=bleibt",
    "https://nico:geheim@github.com/papst/repo.git und http://abc123@host/x",
    "https://github.com/papst/repo ohne Zugang",
    "C:\\Users\\Nico\\AppData\\Roaming\\FISI-Lernplattform\\fehler.log",
    "c:/users/nico/Documents/datei.txt und C:\\Benutzer\\Anna Maria\\x",
    "D:\\Documents and Settings\\Max\\Desktop\\a.txt",
    "\\Users\\Nico\\Dokumente",
    "C:\\Users\\Nico\\AppData\\Local\\Temp\\x.tmp; C:\\Users\\Nico\\Bilder",
    "/home/nico/.local/share/fisi-lernplattform/fehler.log",
    "/Users/nico/Library/Application Support/x und /home/anna/datei",
    "Datei \"/home/nico\" fehlt",
    "Pfad /home/nico: nicht gefunden, /home/nicolas/x bleibt anders",
    "C:\\Users\\Nico, C:\\Users\\Nico\\ und C:\\Users\\Nico)",
    "Traceback (most recent call last):\n  File \"C:\\Users\\Nico\\app.py\", line 3\nValueError: token=abcdef123\n\n",
    "Umlaute äöü ß und Zeichen € bleiben; Größe=12 kennwort=Über123",
    "/home/nico/x\n/home/nico",
    # Grenzfaelle der Schluessel-Laenge (mindestens 16 Zeichen nach dem Praefix)
    "Schluessel ghp_ABCDEFGHIJKLMNOP und ghp_ABCDEFGHIJKLMNO bleibt",
    "github_pat_0123456789abcdef und github_pat_0123456789abcde bleibt",
]

GEHEIMNISSE = [
    {"sync_token": "ghp_meinGeheimerToken123", "sync_repo": "nico/x", "ziel_an": True},
    {"passwort": "kurz", "kennwort": "  langgenug  ", "api-key": "abcdefgh", "zahl": 12345678},
    {"geheim": "abcdef", "secret": "abcdefghij", "name": "abcdefghijk"},
    {},
]

UMGEBUNGEN = {
    "windows": {"env": {"APPDATA": "C:\\Users\\Nico\\AppData\\Roaming",
                        "LOCALAPPDATA": "C:\\Users\\Nico\\AppData\\Local"},
                "home": "C:\\Users\\Nico"},
    "linux": {"env": {}, "home": "/home/nico"},
    "ohne": {"env": {}, "home": "~"},
}


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_diagnose as diag

    faelle = []
    for text in TEXTE:
        eintrag = {"text": text, "scrub": diag.scrub(text), "anonym": {}, "protokoll": {}}
        for name, umg in UMGEBUNGEN.items():
            eintrag["anonym"][name] = core.anonymize_paths(text, env=umg["env"], home=umg["home"])
            eintrag["protokoll"][name] = core.anonymize_paths(
                diag.scrub(text.rstrip()), env=umg["env"], home=umg["home"])
        faelle.append(eintrag)
    geheim = []
    for settings in GEHEIMNISSE:
        werte = diag.secrets_of(settings)
        geheim.append({"einstellungen": settings, "geheimnisse": werte,
                       "scrub": [diag.scrub(text, werte) for text in TEXTE[:12]]})
    daten = {
        "quelle": "fisi_core.anonymize_paths, fisi_diagnose.scrub/secrets_of (Python v0.63.2)",
        "redacted": diag.REDACTED,
        "log_max": core.ERROR_LOG_MAX,
        "umgebungen": UMGEBUNGEN,
        "faelle": faelle,
        "geheimnisse": geheim,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    print("G10: %d Texte, %d Einstellungsfaelle -> %s" % (len(faelle), len(geheim), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
