"""Phase 6 W1 (VE4, Sammelliste 12): Lizenzen aller ausgelieferten Bestandteile.

Drei Schritte:
  sammeln   je Plattform: ordnet jede ausgelieferte Datei einem Bestandteil zu (NuGet-Pakete aus
            Lernplattform.deps.json bzw. project.assets.json, der Rest aus bestandteile.json) und liest
            Lizenzangabe und Lizenzdateien aus dem Paket. Eine Datei ohne Bestandteil oder ein Bestandteil
            ohne Lizenztext ist ein FEHLER (Rueckgabe 1): "jede ausgelieferte Datei steht in der Liste".
  schreiben aus einer oder mehreren Sammlungen: Uebersicht (THIRD_PARTY_NOTICES.txt, im Programm unter
            "Ueber" gezeigt) und Volltexte (THIRD_PARTY_LICENSES.txt, neben dem Programm und im Release).
  pruefen   die Uebersicht im Repo muss gleich der neu geschriebenen sein (sonst Unterschied ausgeben).

Aufrufe (python3 -I):
  erzeuge_lizenzen.py sammeln desktop <rid> <publish-ordner> <aus.json> [paketdatei ...]
  erzeuge_lizenzen.py sammeln android <project.assets.json> <apk> <aus.json>
  erzeuge_lizenzen.py schreiben <uebersicht.txt> <volltexte.txt> <sammlung.json> ...
  erzeuge_lizenzen.py pruefen <THIRD_PARTY_NOTICES.txt im Repo> <sammlung.json> ...
"""
import hashlib
import io
import json
import os
import re
import shutil
import subprocess
import sys
import tarfile
import zipfile

HIER = os.path.dirname(os.path.abspath(__file__))
TEXTE = os.path.join(HIER, "texte")
NUGET = os.environ.get("NUGET_PACKAGES") or os.path.join(os.path.expanduser("~"), ".nuget", "packages")
LIZENZDATEI = re.compile(r"(?i)^(licen[cs]e|copying|third[-_]?party[-_]?notices|thirdpartynotices|notice)(\.(txt|md))?$")
# APK: Signatur (vom Signieren) und Bauergebnis (aus App und allen Paketen zusammengesetzt, Werkzeug des Workloads)
ANDROID_SIGNATUR = re.compile(r"^META-INF/([^/]+\.(SF|RSA|EC|DSA)|MANIFEST\.MF)$")
ANDROID_BAU = re.compile(r"^(AndroidManifest\.xml|resources\.arsc|classes[0-9]*\.dex|res/.+)$")
# eigene Dateien mit allgemeinem Namen zaehlen nur in den von vpk erzeugten Paketen (Pfad "paket:innen")
NUR_IM_PAKET = {"AppRun", ".DirIcon", "Info.plist", "PkgInfo", "sq.version", "setup.ico"}


def fehler(text):
    print("FEHLER: " + text)


def lade_bestandteile():
    with open(os.path.join(HIER, "bestandteile.json"), encoding="utf-8") as d:
        return json.load(d)


def nuspec(ordner):
    for name in sorted(os.listdir(ordner)):
        if name.endswith(".nuspec"):
            with open(os.path.join(ordner, name), encoding="utf-8-sig") as d:
                return d.read()
    return ""


def feld(text, muster):
    m = re.search(muster, text, re.S)
    return m.group(1).strip() if m else ""


def paket_info(paket_id, version, bestand, ordner=None):
    """Lizenz, Quelle und Lizenztexte eines NuGet-Pakets aus dem lokalen Paketordner (oder einem Workload-Paket)."""
    if ordner is None:
        # NuGet-Ordner, sonst Workload-/Laufzeitpakete unter packs/ (z. B. Microsoft.NETCore.App.Host.<rid>)
        kandidaten = [os.path.join(NUGET, paket_id.lower(), version.lower())]
        for wurzel in paketwurzeln()[1:]:
            kandidaten += [os.path.join(wurzel, e, version) for e in os.listdir(wurzel) if e.lower() == paket_id.lower()]
        ordner = next((k for k in kandidaten if os.path.isdir(k)), kandidaten[0])
    if not os.path.isdir(ordner):
        return None
    spec = nuspec(ordner)
    lizenz = feld(spec, r'<license type="expression">([^<]+)<')
    if not lizenz and feld(spec, r'<license type="file">([^<]+)<'):
        lizenz = "Datei " + feld(spec, r'<license type="file">([^<]+)<') + " im Paket"
    lizenz = lizenz or feld(spec, r"<licenseUrl>([^<]+)<")
    quelle = feld(spec, r'<repository [^>]*url="([^"]+)"') or feld(spec, r"<projectUrl>([^<]+)<")
    if not spec:
        for anfang, angabe in sorted(bestand.get("ohne_nuspec", {}).items()):
            if paket_id.startswith(anfang + "."):
                lizenz, quelle = angabe["lizenz"], angabe["quelle"]
                break
    texte = []
    for name in sorted(os.listdir(ordner)):
        if LIZENZDATEI.match(name) and os.path.isfile(os.path.join(ordner, name)):
            texte.append(("Paket " + paket_id + " " + version + ": " + name, lies(os.path.join(ordner, name))))
    if not any(not t[0].lower().endswith(("third-party-notices.txt", "thirdpartynotices.txt")) for t in texte):
        for anfang, datei in sorted(bestand["texte_fuer_pakete"].items()):
            if paket_id == anfang or paket_id.startswith(anfang + "."):
                texte.insert(0, (datei, lies(os.path.join(TEXTE, datei))))
                break
    return {"id": paket_id, "version": version, "lizenz": lizenz, "quelle": quelle,
            "copyright": feld(spec, r"<copyright>([^<]+)<"), "texte": texte}


def lies(pfad):
    with open(pfad, encoding="utf-8-sig", errors="replace") as d:
        return d.read().replace("\r\n", "\n").strip() + "\n"


def zusaetze(bestand, rid):
    for z in bestand["zusaetze"]:
        if rid in z.get("plattformen", [rid]):
            yield z


def ergebnis(art, rid, dateien, pakete, bestand, ohne):
    eintraege = {}
    for p in pakete.values():
        eintraege[p["id"]] = p
    for z in zusaetze(bestand, rid):
        eintraege[z["id"]] = {"id": z["id"], "version": z["version"], "lizenz": z["lizenz"], "quelle": z["quelle"],
                              "copyright": "", "beschreibung": z["beschreibung"],
                              "texte": [(t, lies(os.path.join(TEXTE, t))) for t in z["texte"]]}
    leer = [e["id"] for e in eintraege.values() if not e["texte"]]
    for i in leer:
        fehler("kein Lizenztext fuer " + i)
    for d in ohne:
        fehler("Datei ohne Bestandteil: " + d)
    return {"art": art, "rid": rid, "dateien": dateien, "bestandteile": eintraege}, bool(leer or ohne)


def zuordnung_deps(ordner, rid):
    """Datei (Name) -> (Paket, Version) aus Lernplattform.deps.json."""
    with open(os.path.join(ordner, "Lernplattform.deps.json"), encoding="utf-8") as d:
        deps = json.load(d)
    ziel = deps["targets"][deps["runtimeTarget"]["name"]]
    karte, laufzeit = {}, None
    for schluessel, eintrag in ziel.items():
        name, version = schluessel.split("/")
        art = deps["libraries"][schluessel]["type"]
        if art == "project":
            continue
        if art == "runtimepack":
            name = name.replace("runtimepack.", "")
            laufzeit = version
        for gruppe in ("runtime", "native", "resources"):
            for pfad in eintrag.get(gruppe, {}):
                karte[os.path.basename(pfad)] = (name, version)
    if laufzeit is None:
        raise SystemExit("FEHLER: keine Laufzeit in deps.json (nicht self-contained?)")
    host = ("Microsoft.NETCore.App.Host." + rid, laufzeit)
    karte["Lernplattform"] = host
    karte["Lernplattform.exe"] = host
    # native Symboldateien (z. B. libSkiaSharp.pdb) gehoeren zum Paket der gleichnamigen Bibliothek
    for name, wert in list(karte.items()):
        stamm, endung = os.path.splitext(name)
        if endung in (".dll", ".so", ".dylib"):
            karte.setdefault(stamm + ".pdb", wert)
    return karte


def paketinhalt(pfad):
    """Dateinamen in einer Paketdatei (nupkg/zip, AppImage) oder die Datei selbst."""
    if pfad.endswith((".nupkg", ".zip")):
        with zipfile.ZipFile(pfad) as z:
            return [n for n in z.namelist() if not n.endswith("/") and not n.startswith(("_rels/", "package/"))
                    and n not in ("[Content_Types].xml",) and not n.endswith(".nuspec")]
    if pfad.endswith(".tar.gz"):
        with tarfile.open(pfad) as t:
            return [m.name for m in t.getmembers() if m.isfile()] + [os.path.basename(pfad)]
    if pfad.endswith(".AppImage"):
        versatz = subprocess.run([pfad, "--appimage-offset"], check=True, capture_output=True, text=True).stdout.strip()
        liste = subprocess.run(["unsquashfs", "-o", versatz, "-lc", pfad], check=True, capture_output=True, text=True).stdout
        return [z.split("squashfs-root/", 1)[1] for z in liste.splitlines() if z.startswith("squashfs-root/")] + [os.path.basename(pfad)]
    return [os.path.basename(pfad)]


def sammeln_desktop(rid, ordner, aus, paketdateien):
    bestand = lade_bestandteile()
    karte = zuordnung_deps(ordner, rid)
    eigen = set(bestand["eigen"]["dateien"])
    fremd = {d: z["id"] for z in zusaetze(bestand, rid) for d in z["dateien"]}
    namen = []
    for wurzel, _, dateien in os.walk(ordner):
        for name in dateien:
            namen.append(os.path.relpath(os.path.join(wurzel, name), ordner).replace(os.sep, "/"))
    for paketdatei in paketdateien:
        namen += [os.path.basename(paketdatei) + ":" + n for n in paketinhalt(paketdatei)]
    dateien, pakete, ohne = {}, {}, []
    for pfad in sorted(set(namen)):
        name = pfad.rsplit("/", 1)[-1].rsplit(":", 1)[-1]
        if ":" in pfad and name.endswith(".__symlink"):
            # vpk (macOS) legt Verknuepfungen im Paket als <Name>.__symlink ab; sie zaehlt wie die Datei selbst
            name = name[:-len(".__symlink")]
        im_paket = ":" in pfad
        if (name in eigen and (im_paket or ("/" not in pfad and name not in NUR_IM_PAKET))) or (
                "/" not in pfad and name.endswith("-AppImage.tar.gz")):
            # das tar.gz ist nur eine Huelle um das AppImage (dessen Inhalt wird mitgezaehlt)
            dateien[pfad] = "eigen"
        elif name.endswith(".pkg") and rid.startswith("osx-"):
            # Installer-Huelle von vpk; ihr Inhalt ist der des Pakets (.nupkg), das mit uebergeben wird
            dateien[pfad] = "Velopack-Programme"
        elif name in fremd:
            dateien[pfad] = fremd[name]
        elif name in karte:
            paket_id, version = karte[name]
            if paket_id not in pakete:
                info = paket_info(paket_id, version, bestand)
                if info is None:
                    fehler("Paket nicht im NuGet-Ordner: %s %s" % (paket_id, version))
                    ohne.append(pfad)
                    continue
                pakete[paket_id] = info
            dateien[pfad] = paket_id
        else:
            ohne.append(pfad)
    daten, rot = ergebnis("desktop", rid, dateien, pakete, bestand, ohne)
    schreibe_json(aus, daten)
    print("%s: %d Dateien, %d Bestandteile, %d ohne Zuordnung" % (rid, len(namen), len(daten["bestandteile"]), len(ohne)))
    return 1 if rot else 0


def sammeln_android(assets, apk, aus):
    """APK: .NET-Bibliotheken stecken gebuendelt in lib/arm64-v8a; jedes Paket aus project.assets.json zaehlt."""
    bestand = lade_bestandteile()
    with open(assets, encoding="utf-8") as d:
        daten = json.load(d)
    ziel = next(v for k, v in daten["targets"].items() if "android" in k and "/" in k)
    karte, pakete, fehlend, pfade = {}, {}, [], {}
    for schluessel, eintrag in sorted(ziel.items()):
        if eintrag.get("type") != "package":
            continue
        name, version = schluessel.split("/")
        info = paket_info(name, version, bestand)
        if info is None:
            fehler("Paket nicht im NuGet-Ordner: %s %s" % (name, version))
            fehlend.append(name)
            continue
        pakete[name] = info
        for gruppe in ("runtime", "native", "runtimeTargets"):
            for pfad in eintrag.get(gruppe, {}):
                karte[os.path.basename(pfad)] = name
        # Java-/Kotlin-Teile (.aar/.jar im Paket, auch ein classes.jar darin) landen mit ihrem Pfad in der APK
        # Bei gleichem Pfad in mehreren Paketen (Kotlin-Metadaten, R.txt) gilt das erste nach Namen.
        for innen in archivinhalt(os.path.join(NUGET, name.lower(), version.lower())):
            pfade.setdefault(innen, name)
            # native Bibliothek eines .aar (jni/<abi>/lib.so) liegt in der APK unter lib/<abi>/
            if innen.startswith("jni/") and innen.endswith(".so"):
                karte.setdefault(os.path.basename(innen), name)
    # Laufzeit und Android-Teile von .NET (Workload-Pakete, nicht in project.assets.json)
    laufzeit = []
    for name, version, ordner in laufzeit_android():
        info = paket_info(name, version, bestand, ordner)
        if info is None:
            continue
        pakete[name] = info
        laufzeit.append(name)
        for wurzel, _, namen in os.walk(ordner):
            for datei in namen:
                if datei.endswith((".so", ".dll", ".jar", ".dex")):
                    karte.setdefault(datei, name)
    sdk = next((n for n in laufzeit if n.lower() == "microsoft.android.sdk.linux"), None)
    dateien, ohne = {}, ["Paket fehlt: " + n for n in fehlend]
    with zipfile.ZipFile(apk) as z:
        namen = [n for n in z.namelist() if not n.endswith("/")]
    for pfad in sorted(namen):
        name = pfad.rsplit("/", 1)[-1]
        if ANDROID_SIGNATUR.match(pfad):
            dateien[pfad] = "eigen"
        elif sdk and ANDROID_BAU.match(pfad):
            dateien[pfad] = sdk
        elif kotlin_paket(pfad, pakete):
            dateien[pfad] = kotlin_paket(pfad, pakete)
        elif pfad in pfade:
            dateien[pfad] = pfade[pfad]
        elif pfad.startswith("lib/arm64-v8a/libaot-") and name.endswith(".so"):
            # vorab uebersetzte .NET-Bibliothek (AOT): gehoert zum Bestandteil der Bibliothek
            bibliothek = name[len("libaot-"):-len(".so")]
            if bibliothek.startswith("Lernplattform."):
                dateien[pfad] = "eigen"
            elif bibliothek in karte:
                dateien[pfad] = karte[bibliothek]
            elif sdk and bibliothek == "_Microsoft.Android.Resource.Designer.dll":
                dateien[pfad] = sdk
            else:
                ohne.append(pfad)
        elif name in karte:
            dateien[pfad] = karte[name]
        elif sdk and pfad.startswith("lib/arm64-v8a/") and (
                name.startswith(("libassembl", "libxa", "libarchive-dso-stub")) or name in ("libmonodroid.so", "libarc.bin.so")):
            # beim Bau erzeugt (gebuendelte .NET-Bibliotheken, App-Tabelle): Werkzeug des Android-Workloads
            dateien[pfad] = sdk
        else:
            ohne.append(pfad)
    # ein Workload-Paket zaehlt nur, wenn eine Datei der APK daraus stammt
    for name in laufzeit:
        if name not in dateien.values():
            pakete.pop(name)
    daten2, rot = ergebnis("android", "android-arm64", dateien, pakete, bestand, ohne)
    schreibe_json(aus, daten2)
    print("android: %d Dateien, %d Bestandteile, %d ohne Zuordnung" % (len(namen), len(daten2["bestandteile"]), len(ohne)))
    return 1 if rot else 0


def paketwurzeln():
    """Wo Workload-Pakete liegen: NuGet-Ordner und packs/ neben dotnet (DOTNET_ROOT, ~/.dotnet)."""
    wurzeln = [NUGET]
    for basis in (os.environ.get("DOTNET_ROOT"), os.path.join(os.path.expanduser("~"), ".dotnet"),
                  os.path.dirname(os.path.realpath(shutil.which("dotnet") or "/"))):
        if basis and os.path.isdir(os.path.join(basis, "packs")):
            wurzeln.append(os.path.join(basis, "packs"))
    return wurzeln


def versionsschluessel(v):
    return [(0, int(x), "") if x.isdigit() else (1, 0, x) for x in re.split(r"[.-]", v)]


def archivinhalt(ordner):
    """Pfade in den .aar/.jar/.zip eines Paketordners, auch in einem darin liegenden .jar oder .zip (classes.jar)."""
    gefunden = []
    if not os.path.isdir(ordner):
        return gefunden
    for wurzel, _, namen in os.walk(ordner):
        for datei in sorted(namen):
            if not datei.endswith((".aar", ".jar", ".zip")):
                continue
            try:
                with zipfile.ZipFile(os.path.join(wurzel, datei)) as z:
                    for innen in z.namelist():
                        if innen.endswith("/"):
                            continue
                        gefunden.append(innen)
                        if innen.endswith((".jar", ".zip")):
                            with zipfile.ZipFile(io.BytesIO(z.read(innen))) as j:
                                gefunden += [n for n in j.namelist() if not n.endswith("/")]
            except zipfile.BadZipFile:
                fehler("kein Archiv: " + os.path.join(wurzel, datei))
    return gefunden


def flach(text):
    return re.sub(r"[^a-z0-9]", "", text.lower())


def kotlin_paket(pfad, pakete):
    """Java-/Kotlin-Beigaben in der APK dem Paket zuordnen, aus dem sie stammen (sonst None = ohne Zuordnung).
    META-INF/<gruppe>_<artefakt>.version und <name>.properties -> Paket mit gleichem Namen (ohne "Xamarin.");
    kotlin/..., *.kotlin_builtins, META-INF/*.kotlin_module -> Kotlin-Standardbibliothek;
    DebugProbesKt.bin -> KotlinX Coroutines."""
    name = pfad.rsplit("/", 1)[-1]
    ids = sorted(pakete)
    def erstes(anfang):
        return next((i for i in ids if i.lower().startswith(anfang)), None)
    if pfad.startswith("kotlin/") or name.endswith((".kotlin_builtins", ".kotlin_module")):
        return erstes("xamarin.kotlin.stdlib")
    if name == "DebugProbesKt.bin":
        return erstes("xamarin.kotlinx.coroutines.core")
    if name.endswith((".version", ".properties")):
        stamm = name.rsplit(".", 1)[0].split("_", 1)[-1] if name.endswith(".version") else name.rsplit(".", 1)[0]
        gruppe = name.split("_", 1)[0] if name.endswith(".version") and "_" in name else ""
        for i in ids:
            ohne_x = flach(i[len("xamarin."):] if i.lower().startswith("xamarin.") else i)
            if ohne_x in (flach(gruppe.split(".")[0] + stamm), flach(stamm), flach(gruppe + stamm)):
                return i
    return None


def laufzeit_android():
    """Pakete der .NET-Android-Laufzeit (Workload oder NuGet), je Name die neueste Version zu .NET 10 bzw. API 36."""
    gefunden = {}
    for wurzel in paketwurzeln():
        for eintrag in sorted(os.listdir(wurzel)):
            klein = eintrag.lower()
            # CoreCLR und NativeAOT sind andere Laufzeiten, die dieser Bau nicht nutzt (Mono, libmonosgen)
            if not (klein.startswith(("microsoft.netcore.app.runtime.", "microsoft.android.runtime."))
                    and klein.endswith("android-arm64") and ".cross." not in klein
                    and ".coreclr." not in klein and ".nativeaot." not in klein) and klein != "microsoft.android.sdk.linux":
                continue
            for version in os.listdir(os.path.join(wurzel, eintrag)):
                if not version.startswith(("10.", "36.")):
                    continue
                alt = gefunden.get(klein)
                if alt is None or versionsschluessel(version) > versionsschluessel(alt[1]):
                    gefunden[klein] = (eintrag, version, os.path.join(wurzel, eintrag, version))
    for name, version, ordner in sorted(gefunden.values()):
        print("Android-Laufzeit: %s %s (%s)" % (name, version, ordner))
    return sorted(gefunden.values())


def schreibe_json(aus, daten):
    with open(aus, "w", encoding="utf-8", newline="\n") as d:
        json.dump(daten, d, ensure_ascii=False, indent=1, sort_keys=True)


def vereinigen(sammlungen):
    alle, wo = {}, {}
    for pfad in sammlungen:
        with open(pfad, encoding="utf-8") as d:
            s = json.load(d)
        for i, e in s["bestandteile"].items():
            alle[i] = e
            wo.setdefault(i, set()).add(s["rid"])
    return alle, wo


def uebersicht(alle, wo):
    z = ["Fachinformatiker Lernplattform - Hinweise zu Fremdbestandteilen",
         "================================================================",
         "",
         "Diese Liste entsteht beim Bau (werkzeuge/lizenzen/erzeuge_lizenzen.py) aus den Dateien, die mit dem",
         "Programm ausgeliefert werden: Windows, Linux, macOS und Android. Jede ausgelieferte Datei gehoert zu",
         "genau einem der Bestandteile unten. Lizenz und Quelle stammen aus der Paketbeschreibung (.nuspec) bzw.",
         "aus werkzeuge/lizenzen/bestandteile.json. Die vollstaendigen Lizenztexte stehen in der Datei",
         "THIRD_PARTY_LICENSES.txt neben dem Programm und auf der Release-Seite",
         "(github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases).",
         "",
         "Bestandteil (Version) | Lizenz | Quelle | Plattformen", ""]
    for i in sorted(alle, key=str.lower):
        e = alle[i]
        z.append("- %s (%s) | %s | %s | %s" % (e["id"], e["version"], e["lizenz"] or "(keine Angabe)",
                                               e["quelle"] or "-", ", ".join(sorted(wo[i]))))
        if e.get("beschreibung"):
            z.append("  " + e["beschreibung"])
    z += ["", "Nicht ausgeliefert (nur Tests und Bau): xunit.v3, Microsoft.Testing.Platform, vpk, Python-Werkzeuge."]
    return "\n".join(z) + "\n"


def volltexte(alle):
    z = ["Fachinformatiker Lernplattform - Lizenztexte der Fremdbestandteile", "=" * 66, "",
         "Uebersicht: THIRD_PARTY_NOTICES.txt. Gleiche Texte stehen nur einmal, spaeter mit Verweis.", ""]
    bekannt = {}
    for i in sorted(alle, key=str.lower):
        e = alle[i]
        z += ["#" * 78, "%s (%s), Lizenz %s" % (e["id"], e["version"], e["lizenz"] or "(keine Angabe)")]
        if e.get("copyright"):
            z.append("Copyright laut Paket: " + e["copyright"])
        z.append("")
        for name, text in e["texte"]:
            summe = hashlib.sha256(text.encode("utf-8")).hexdigest()
            if summe in bekannt:
                z += ["[%s: gleicher Text wie bei %s]" % (name, bekannt[summe]), ""]
            else:
                bekannt[summe] = e["id"] + ", " + name
                z += ["--- " + name + " ---", text]
    return "\n".join(z).rstrip("\n") + "\n"


def main_pruefen(repo_datei, sammlungen):
    """Die Uebersicht im Repo (im Programm eingebettet) muss gleich der aus den Sammlungen erzeugten sein."""
    alle, wo = vereinigen(sammlungen)
    neu = uebersicht(alle, wo)
    with open(repo_datei, encoding="utf-8") as d:
        alt = d.read().replace("\r\n", "\n")
    if alt != neu:
        import difflib
        print("FEHLER: THIRD_PARTY_NOTICES.txt im Repo weicht von der neu erzeugten Liste ab:")
        sys.stdout.writelines(difflib.unified_diff(alt.splitlines(True), neu.splitlines(True), "Repo", "neu"))
        # ganze neue Liste zum Uebernehmen (nach Pruefung von Hand ins Repo)
        print("===== ANFANG neue THIRD_PARTY_NOTICES.txt =====")
        sys.stdout.write(neu)
        print("===== ENDE neue THIRD_PARTY_NOTICES.txt =====")
        return 1
    print("THIRD_PARTY_NOTICES.txt gleich der neu erzeugten Liste (%d Bestandteile)" % len(alle))
    return 0


def main():
    befehl = sys.argv[1]
    if befehl == "sammeln" and sys.argv[2] == "desktop":
        return sammeln_desktop(sys.argv[3], sys.argv[4], sys.argv[5], sys.argv[6:])
    if befehl == "sammeln" and sys.argv[2] == "android":
        return sammeln_android(sys.argv[3], sys.argv[4], sys.argv[5])
    if befehl == "schreiben":
        alle, wo = vereinigen(sys.argv[4:])
        for pfad, text in ((sys.argv[2], uebersicht(alle, wo)), (sys.argv[3], volltexte(alle))):
            with open(pfad, "w", encoding="utf-8", newline="\n") as d:
                d.write(text)
        print("geschrieben: %d Bestandteile" % len(alle))
        return 0
    if befehl == "pruefen":
        return main_pruefen(sys.argv[2], sys.argv[3:])
    raise SystemExit("unbekannter Aufruf")


if __name__ == "__main__":
    sys.exit(main())
