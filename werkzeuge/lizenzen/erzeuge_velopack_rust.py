"""Phase 6 W1 (VE4, Sammelliste 12): Lizenzen der Rust-Bibliotheken in den Velopack-Programmen.

Velopack legt in jedes Paket eigene Programme (Windows Update.exe, Setup.exe und das Start-Programm,
Linux UpdateNix im AppImage, macOS UpdateMac). Sie sind in Rust geschrieben und enthalten weitere
Bibliotheken (Crates). Dieses Skript liest den Quellstand von Velopack zur Version aus
Directory.Packages.props (Git-Tag gleichen Namens) mit `cargo metadata` und schreibt fuer jedes
Crate, das in diese Programme eingeht (nur normale Abhaengigkeiten, keine Bau- und Testteile),
Name, Version, Lizenz und den vollen Text der Lizenzdateien aus dem Crate selbst.

Aufruf: python3 -I erzeuge_velopack_rust.py <velopack-quellordner> <ausgabe.txt>
Braucht cargo (Netz fuer crates.io beim ersten Lauf).
"""
import hashlib
import json
import os
import subprocess
import sys

ZIELE = {
    "Windows (Update.exe, Setup.exe, Start-Programm)": "x86_64-pc-windows-msvc",
    "Linux (UpdateNix im AppImage)": "x86_64-unknown-linux-gnu",
    "macOS (UpdateMac)": "aarch64-apple-darwin",
}
WURZEL = "velopack_bins"
TEXTDATEIEN = ("LICENSE", "LICENCE", "COPYING", "NOTICE", "COPYRIGHT", "UNLICENSE")


def metadaten(quelle, ziel):
    aus = subprocess.run(
        ["cargo", "metadata", "--format-version", "1", "--locked", "--features", "velopack_bins/windows", "--filter-platform", ziel],
        cwd=quelle, check=True, capture_output=True, text=True)
    return json.loads(aus.stdout)


def huelle(meta):
    pakete = {p["id"]: p for p in meta["packages"]}
    knoten = {k["id"]: k for k in meta["resolve"]["nodes"]}
    start = [i for i, p in pakete.items() if p["name"] == WURZEL]
    if len(start) != 1:
        raise SystemExit("FEHLER: " + WURZEL + " nicht eindeutig im Quellstand")
    gesehen, offen = set(), list(start)
    while offen:
        i = offen.pop()
        if i in gesehen:
            continue
        gesehen.add(i)
        for dep in knoten[i]["deps"]:
            if any(k["kind"] is None for k in dep["dep_kinds"]):
                offen.append(dep["pkg"])
    return [pakete[i] for i in gesehen]


def texte(paket):
    ordner = os.path.dirname(paket["manifest_path"])
    gefunden = []
    for name in sorted(os.listdir(ordner)):
        pfad = os.path.join(ordner, name)
        if os.path.isfile(pfad) and name.upper().startswith(TEXTDATEIEN):
            with open(pfad, encoding="utf-8", errors="replace") as d:
                gefunden.append((name, d.read().replace("\r\n", "\n").strip() + "\n"))
    if paket.get("license_file"):
        pfad = os.path.join(ordner, paket["license_file"])
        name = os.path.basename(pfad)
        if os.path.isfile(pfad) and name not in [n for n, _ in gefunden]:
            with open(pfad, encoding="utf-8", errors="replace") as d:
                gefunden.append((name, d.read().replace("\r\n", "\n").strip() + "\n"))
    return gefunden


def main():
    quelle, ausgabe = sys.argv[1], sys.argv[2]
    alle, wo = {}, {}
    for anzeige, ziel in ZIELE.items():
        for p in huelle(metadaten(quelle, ziel)):
            if p["source"] is None:          # eigener Velopack-Code im Arbeitsbereich: MIT, steht beim Paket Velopack
                continue
            if all("proc-macro" in z["kind"] for z in p["targets"]):    # laeuft nur beim Bauen, steckt nicht im Programm
                continue
            schluessel = (p["name"], p["version"])
            alle[schluessel] = p
            wo.setdefault(schluessel, []).append(anzeige)
    zeilen = ["Rust-Bibliotheken in den Velopack-Programmen (erzeugt von werkzeuge/lizenzen/erzeuge_velopack_rust.py,",
              "Quellstand velopack/velopack Tag " + os.environ.get("VELOPACK_TAG", "?") + ", cargo metadata, nur normale Abhaengigkeiten).",
              "Anzahl: " + str(len(alle)), ""]
    ohne = []
    bekannt = {}
    for schluessel in sorted(alle):
        p = alle[schluessel]
        lizenz = p.get("license") or "(keine Angabe im Crate)"
        zeilen.append("- %s %s, Lizenz %s, Urheber %s, %s; in: %s" % (p["name"], p["version"], lizenz,
                                                                    ", ".join(p.get("authors") or ["(keine Angabe)"]),
                                                                    p.get("repository") or "ohne Quelladresse", "; ".join(wo[schluessel])))
    zeilen.append("")
    for schluessel in sorted(alle):
        p = alle[schluessel]
        liste = texte(p)
        if not liste:
            ohne.append("%s %s" % schluessel)
            continue
        for name, text in liste:
            summe = hashlib.sha256(text.encode("utf-8")).hexdigest()
            kopf = "--- %s %s: %s ---" % (p["name"], p["version"], name)
            if summe in bekannt:
                zeilen += [kopf, "(gleicher Text wie " + bekannt[summe] + ")", ""]
            else:
                bekannt[summe] = "%s %s %s" % (p["name"], p["version"], name)
                zeilen += [kopf, text]
    if ohne:
        zeilen += ["Crates ohne Lizenzdatei im Paket (Lizenz nur als Angabe, Text der genannten Lizenz siehe oben):"]
        zeilen += ["- " + o for o in ohne]
    with open(ausgabe, "w", encoding="utf-8", newline="\n") as d:
        d.write("\n".join(zeilen).rstrip("\n") + "\n")
    print("Crates:", len(alle), "ohne Lizenzdatei:", len(ohne))


if __name__ == "__main__":
    main()
