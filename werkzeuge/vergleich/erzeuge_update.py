#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G41 (Phase 5, Paket BE9 "Update"), gerechnet vom echten Python-Code
der Python-Linie (fisi_update.py, v0.63.2). Kein Netz: _open wird durch feste Antworten ersetzt,
die Uhr (datetime.datetime.now) durch einen festen Zeitpunkt. Nur synthetische Testdaten.

Aufbau:
  texte      - die Texte von fisi_update (Konstanten und die Saetze aus den Funktionen)
  notizen    - [Markdown, plain_notes]
  suche      - je Fall: Antwort (JSON-Text, {"http": Code} oder {"netz": true}), installierte Version,
               Ergebnis von check_for_update(kind="android"): null, {"fehler": Text} oder die Felder
               von UpdateInfo (version, notes, page_url, asset_name, asset_url, asset_size, asset_digest)
  bausteine  - lange Inhalte: "lang" = 3000 Zeilen "Zeile %05d mit Text äöü", "emoji" = lang mit zwei Emojis
               in Zeile 1500, "x70k" = 70 KiB "x", "crlf" = 10000 mal "a\r\nb\rc\n"
  protokoll  - je Fall: Inhalt von update.log vorher (null = fehlt, "{baustein}" = langer Inhalt),
               geschriebene Zeilen, Inhalt danach (write_update_log; Zeitpunkt fest ZEIT, Kuerzen ueber
               64 KB); ist er laenger als 2000 Zeichen, steht statt seiner {"sha256", "zeichen", "ende"}
  lesen      - [Inhalt (wie oben), Grenze, read_update_log (wie oben)]
  download   - je Fall: Groesse und digest der Angabe, Inhalt der Datei, Fehlertext oder null, Zeilen in
               update.log (download mit fester Antwort)
  merker     - Einstellungen nach remember_pending_update("0.11.1") zum Zeitpunkt ZEIT
  abschluss  - je Fall: Merker (oder null), installierte Version, Inhalt von update_installer.log (oder
               null), Ergebnis von finish_pending_update ("{PFAD}" = Pfad von update.log), Zeilen in update.log

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_update.py <pfad_fisi_lernplattform> tests/Vergleich/update.json
"""
import atexit
import datetime
import hashlib
import io
import json
import os
import shutil
import sys
import tempfile
import urllib.error

ZEIT = datetime.datetime(2026, 10, 23, 12, 34, 56)

NOTIZEN = [
    "",
    "Nur ein Satz.",
    "## A 0.11.1\n\n- **Neu:** Update im Programm\n- Fix: `Abgleich`\n  zweite Zeile des Punkts\n\nAbsatz\nweiter im Absatz\n\n### Herunterladen\n- Datei.exe sha256 ...",
    "* Stern-Punkt\r\n* __fett__\r\n\r\nText",
    "   - eingerueckt  \n\t\n-ohne Leerzeichen\n- \n### Herunterladen",
    "Vorher\n### Herunterladen\nnachher",
    "Zeile mit Leerraum \n\n\n- Ä Ö Ü ß €",
]

APK = {"name": "Fachinformatiker-Lernplattform.apk", "browser_download_url": "https://example.invalid/app.apk",
       "size": 1234, "digest": "sha256:" + "ab" * 32}
EXE = {"name": "Fachinformatiker-Lernplattform-win-Setup.exe", "browser_download_url": "https://example.invalid/s.exe", "size": 9}


def release(**felder):
    werte = {"tag_name": "v0.64.0", "prerelease": False, "draft": False, "body": "  - Neu  \n",
             "html_url": "https://example.invalid/release", "assets": [EXE, APK]}
    werte.update(felder)
    return werte


SUCHE = [
    (release(), "0.63.2"),
    (release(), "0.64.0"),
    (release(), "0.65.0"),
    (release(tag_name="v0.63.10"), "0.63.9"),
    (release(prerelease=True), "0.63.2"),
    (release(draft="ja"), "0.63.2"),
    (release(prerelease=0), "0.63.2"),
    (release(assets=[EXE]), "0.63.2"),
    (release(assets=[]), "0.63.2"),
    (release(assets=None), "0.63.2"),
    (release(tag_name=None), "0.63.2"),
    (release(tag_name=5), "0.63.2"),
    (release(tag_name="kaputt"), "0.63.2"),
    (release(body=None, html_url=None), "0.63.2"),
    (release(body=7, html_url=""), "0.63.2"),
    (release(assets=["text", 5, None, {"name": 5}, {"name": "x.APK"}, {"name": "b.apk"}]), "0.63.2"),
    (release(assets=[{"name": "c.apk", "size": "groß", "digest": 5}]), "0.63.2"),
    (release(assets=[{"name": "d.apk", "size": 1.5}]), "0.63.2"),
    ({k: v for k, v in release().items() if k != "assets"}, "0.63.2"),
    ({k: v for k, v in release().items() if k != "tag_name"}, "0.63.2"),
    ([release()], "0.63.2"),
    ("text", "0.63.2"),
    (None, "0.63.2"),
]

DOWNLOAD = [
    ("ok mit Pruefsumme", b"Paketinhalt", None, True),
    ("ok ohne Pruefsumme", b"Paketinhalt", None, False),
    ("Pruefsumme falsch", b"Paketinhalt", "sha256:" + "00" * 32, True),
    ("Pruefsumme ungueltig (nur Groesse)", b"Paketinhalt", "md5:1234", True),
    ("Groesse falsch", b"Paketinhalt", None, True),
    ("Groesse 0 (nicht geprueft)", b"Paketinhalt", None, True),
    ("Pruefsumme gross geschrieben", b"Paketinhalt", "SHA256:" + hashlib.sha256(b"Paketinhalt").hexdigest().upper(), True),
]


class Uhr(datetime.datetime):
    @classmethod
    def now(cls, tz=None):
        return ZEIT


class Antwort(io.BytesIO):
    def __init__(self, daten, laenge=None):
        super().__init__(daten)
        self.headers = {"Content-Length": str(laenge if laenge is not None else len(daten))}

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    atexit.register(shutil.rmtree, tmp, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
    tempfile.tempdir = tmp   # Sammelliste 91: auch der Download-Ordner von fisi_update (FISI-Lernplattform-Update)
    daten = os.path.join(tmp, "daten")
    os.makedirs(daten)
    os.environ["HOME"] = os.path.join(tmp, "home")
    os.environ["FISI_DB_PATH"] = os.path.join(daten, "lernplattform.db")
    for name in ("APPDATA", "LOCALAPPDATA"):
        os.environ.pop(name, None)
    sys.path.insert(0, repo)
    import fisi_update as fu
    fu.datetime.datetime = Uhr
    log = fu.update_log_path()

    def log_lesen():
        if not os.path.exists(log):
            return None
        with open(log, encoding="utf-8", newline="") as handle:
            return handle.read()

    def log_leeren():
        if os.path.exists(log):
            os.remove(log)

    texte = {name: getattr(fu, name) for name in (
        "UNEXPECTED_ANSWER", "NO_ASSET_TEXT", "DOWNLOAD_HTTP", "DOWNLOAD_TIMEOUT", "DOWNLOAD_OFFLINE",
        "DOWNLOAD_INCOMPLETE", "DOWNLOAD_SAVE", "DOWNLOAD_CHECKSUM", "FAILED_TITLE", "FAILED_TEXT",
        "FAILED_TIP_WINDOWS", "FAILED_TIP_APPIMAGE", "REASON_ROLLBACK", "REASON_UNKNOWN", "REASON_APPIMAGE",
        "NOTES_END", "PENDING_KEY", "UPDATE_LOG_NAME")}
    texte["UPDATE_LOG_MAX"] = fu.UPDATE_LOG_MAX

    # -- Suche ---------------------------------------------------------------
    def frage(antwort, aktuell):
        def oeffnen(url, timeout):
            if isinstance(antwort, dict) and "http" in antwort and len(antwort) == 1:
                raise urllib.error.HTTPError(url, antwort["http"], "abgelehnt", {}, None)
            if isinstance(antwort, dict) and antwort.get("netz") is True and len(antwort) == 1:
                raise urllib.error.URLError("keine Verbindung")
            return Antwort(antwort.encode("utf-8"))
        fu._open = oeffnen
        try:
            info = fu.check_for_update(aktuell, kind="android")
        except fu.UpdateError as fehler:
            return {"fehler": str(fehler)}
        if info is None:
            return None
        return {"version": info.version, "notes": info.notes, "page_url": info.page_url, "asset_name": info.asset_name,
                "asset_url": info.asset_url, "asset_size": info.asset_size, "asset_digest": info.asset_digest}

    original_open = fu._open
    suche = []
    for antwort, aktuell in SUCHE:
        text = json.dumps(antwort, ensure_ascii=False)
        suche.append({"antwort": text, "aktuell": aktuell, "ergebnis": frage(text, aktuell)})
    for antwort in ({"http": 403}, {"http": 404}, {"http": 500}, {"netz": True}):
        suche.append({"antwort": antwort, "aktuell": "0.63.2", "ergebnis": frage(antwort, "0.63.2")})
    suche.append({"antwort": "{kaputt", "aktuell": "0.63.2", "ergebnis": frage("{kaputt", "0.63.2")})
    fu._open = original_open

    # -- update.log ----------------------------------------------------------
    protokoll = []
    lang = "".join("Zeile %05d mit Text äöü\n" % i for i in range(3000))
    bausteine = {"lang": lang, "emoji": lang.replace("Zeile 01500", "Zeile \U0001F600\U0001F600"),
                 "x70k": "x" * (70 * 1024), "crlf": "a\r\nb\rc\n" * 10000}

    def kurz(text):
        if text is None or len(text) <= 2000:
            return text
        return {"sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(), "zeichen": len(text), "ende": text[-200:]}

    def inhalt_von(angabe):
        return bausteine[angabe[1:-1]] if angabe and angabe.startswith("{") else angabe
    for name, vorher, zeilen in (
            ("neu", None, ["Download startet: Version 0.11.1, Paket.nupkg"]),
            ("anhaengen", "2026-10-22 08:00:00 | Programm | alt\n", ["zwei", "drei mit | Strich"]),
            ("kuerzen", "{lang}", ["nach dem Kuerzen"]),
            ("kuerzen mit Emoji", "{emoji}", ["danach"]),
            ("kuerzen ohne Zeilenende", "{x70k}", ["eine Zeile"]),
            ("CRLF und CR", "{crlf}", ["neu"])):
        log_leeren()
        if vorher is not None:
            with open(log, "w", encoding="utf-8", newline="") as handle:
                handle.write(inhalt_von(vorher))
        for zeile in zeilen:
            fu.write_update_log(zeile)
        protokoll.append({"name": name, "vorher": vorher, "zeilen": zeilen, "nachher": kurz(log_lesen())})

    lesen = []
    for inhalt, grenze in (("", 16384), ("eine\nzwei\n", 16384), ("a\nbb\nccc\n", 5), ("ohne Zeilenende", 4),
                           ("  Rand  \n\n", 16384), ("{lang}", 16 * 1024), ("x\nä\nü\n", 3), ("{crlf}", 100)):
        log_leeren()
        with open(log, "w", encoding="utf-8", newline="") as handle:
            handle.write(inhalt_von(inhalt))
        lesen.append([inhalt, grenze, kurz(fu.read_update_log(grenze))])
    log_leeren()
    lesen.append([None, 16384, fu.read_update_log()])

    # -- Download ------------------------------------------------------------
    download = []
    for name, inhalt, digest, mit_groesse in DOWNLOAD:
        log_leeren()
        groesse = len(inhalt) if mit_groesse else 0
        if name == "Groesse falsch":
            groesse = len(inhalt) + 3
        if name.startswith("Groesse 0"):
            groesse = 0
        if name == "ok mit Pruefsumme":
            digest = "sha256:" + hashlib.sha256(inhalt).hexdigest()
        info = fu.UpdateInfo("0.11.1", "", "https://example.invalid", asset_name="Paket.nupkg",
                             asset_url="https://example.invalid/Paket.nupkg", asset_size=groesse, asset_digest=digest)
        fu._open = lambda url, timeout, inhalt=inhalt: Antwort(inhalt)
        fehler = None
        try:
            ziel_datei = fu.download(info)
            os.remove(ziel_datei)
        except fu.UpdateError as e:
            fehler = str(e)
        fu._open = original_open
        download.append({"name": name, "groesse": groesse, "digest": digest, "inhalt": inhalt.decode("utf-8"),
                         "fehler": fehler, "log": log_lesen()})

    # -- Merker und Pruefung beim Start --------------------------------------
    einstellungen = os.path.join(daten, "einstellungen.json")
    installer = fu.installer_log_path()

    def einstellungen_schreiben(werte):
        with open(einstellungen, "w", encoding="utf-8") as handle:
            json.dump(werte, handle)

    einstellungen_schreiben({"auto_check": True, "tagesziel": 20})
    fu.remember_pending_update("0.11.1")
    with open(einstellungen, encoding="utf-8") as handle:
        merker = json.load(handle)

    abschluss = []
    for name, eintrag, aktuell, inst in (
            ("kein Merker", None, "0.11.0", None),
            ("angekommen", {"version": "0.11.1", "zeit": "2026-10-23T12:00:00"}, "0.11.1", None),
            ("neuer als erwartet", {"version": "0.11.1"}, "0.12.0", None),
            ("nicht angekommen", {"version": "0.11.1", "zeit": "2026-10-23T12:00:00"}, "0.11.0", None),
            ("nicht angekommen, Installer zurueckgerollt", {"version": "0.11.1"}, "0.11.0", "x\nRolling back changes\n"),
            ("Ziel leer", {"version": ""}, "0.11.0", None),
            ("Ziel fehlt", {"zeit": "x"}, "0.11.0", None),
            ("Ziel Zahl", {"version": 5}, "0.11.0", None),
            ("Merker kein Objekt", "0.11.1", "0.11.0", None),
            ("AppImage", {"version": "0.11.1", "alt": "/opt/fehlt/Programm.AppImage.alt"}, "0.11.0", None)):
        log_leeren()
        if os.path.exists(installer):
            os.remove(installer)
        if inst is not None:
            with open(installer, "w", encoding="utf-8") as handle:
                handle.write(inst)
        werte = {"auto_check": True}
        if eintrag is not None:
            werte[fu.PENDING_KEY] = eintrag
        einstellungen_schreiben(werte)
        ergebnis = fu.finish_pending_update(aktuell)
        with open(einstellungen, encoding="utf-8") as handle:
            danach = json.load(handle)
        if isinstance(ergebnis, tuple):
            ergebnis = [ergebnis[0], ergebnis[1].replace(log, "{PFAD}")]
        abschluss.append({"name": name, "merker": eintrag, "aktuell": aktuell, "installer": inst,
                          "ergebnis": ergebnis, "einstellungen": danach, "log": log_lesen()})

    ergebnis = {"quelle": "fisi_update.py (v0.63.2)", "python": "3.12", "zeit": ZEIT.isoformat(sep=" "),
                "texte": texte, "notizen": [[n, fu.plain_notes(n)] for n in NOTIZEN], "suche": suche,
                "protokoll": protokoll, "lesen": lesen, "download": download, "merker": merker,
                "abschluss": abschluss}
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(ergebnis, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    print("G41: %d Notizen, %d Suchen, %d Protokolle, %d Downloads, %d Abschluesse -> %s"
          % (len(NOTIZEN), len(suche), len(protokoll), len(download), len(abschluss), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
