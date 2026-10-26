#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt src/Lernplattform.Core/Texte/RechnerTexte.Python.cs (Phase 3, Welle 1,
Pakete R1-R3): alle Texte und festen Werte des Abschnitts "PRAXIS-RECHNER" aus
fisi_core.py (v0.63.2, Zeilen 2568-5735) zeichengleich, mit den Python-Namen.
Uebernommen werden Namen, deren Wert ein Text, eine Zahl oder eine Liste,
ein Tupel oder ein Woerterbuch nur aus Texten ist (auch geschachtelt).
Nicht hier (in C# von Hand, Test gegen G25/G26): RAID_RULES, RAID_INPUT_MIN,
IPV6_TYPES, UPS_LIMITS, die Zahlen in UPS_TASKS, die regulaeren Ausdruecke.
Von UPS_TASKS kommen Titel, Frage und Annahmen als UPS_TASK_TEXTS hierher.

Die CI erzeugt die Datei neu und vergleicht sie Byte fuer Byte (wie
PythonKlein.Tabelle.cs). Die Python-Linie ist eingefroren.

Aufruf:
  python3.12 -I werkzeuge/tabellen/erzeuge_rechner_texte.py <pfad_fisi_lernplattform> src/Lernplattform.Core/Texte/RechnerTexte.Python.cs
"""
import ast
import atexit
import os
import shutil
import sys
import tempfile
import unicodedata

VON, BIS = 2568, 5735          # Abschnitt PRAXIS-RECHNER in fisi_core.py (fbc657d)
HAND = {"RAID_RULES", "RAID_INPUT_MIN", "IPV6_TYPES", "UPS_LIMITS", "UPS_TASKS"}


def text(wert):
    teile = []
    for z in wert:
        c = ord(z)
        if z == "\\":
            teile.append("\\\\")
        elif z == '"':
            teile.append('\\"')
        elif z == "\n":
            teile.append("\\n")
        elif 0x20 <= c < 0x7F:
            teile.append(z)
        elif c > 0xFFFF:
            raise ValueError("Zeichen ausserhalb BMP: U+%X" % c)
        elif unicodedata.category(z)[0] in "LPSN" and unicodedata.category(z) != "Zs":
            teile.append(z)
        else:
            teile.append("\\u%04x" % c)
    return '"' + "".join(teile) + '"'


def nur_texte(wert):
    if isinstance(wert, str):
        return True
    if isinstance(wert, (list, tuple)):
        return len(wert) > 0 and all(nur_texte(w) for w in wert)
    if isinstance(wert, dict):
        return len(wert) > 0 and all(nur_texte(k) and nur_texte(v) for k, v in wert.items())
    return False


def typ(wert, oben=False):
    if isinstance(wert, str):
        return "string"
    if isinstance(wert, tuple) and not oben:
        return "(" + ", ".join(typ(w) for w in wert) + ")"
    if isinstance(wert, (list, tuple)):
        typen = {typ(w) for w in wert}
        if len(typen) != 1:
            raise ValueError("gemischte Liste: %r" % (wert,))
        return "IReadOnlyList<%s>" % typen.pop()
    schluessel = {typ(k) for k in wert}
    werte = {typ(v) for v in wert.values()}
    if len(schluessel) != 1 or len(werte) != 1:
        raise ValueError("gemischtes Woerterbuch: %r" % (wert,))
    return "IReadOnlyDictionary<%s, %s>" % (schluessel.pop(), werte.pop())


def ausdruck(wert, oben=False):
    if isinstance(wert, str):
        return text(wert)
    if isinstance(wert, tuple) and not oben:
        return "(" + ", ".join(ausdruck(w) for w in wert) + ")"
    if isinstance(wert, (list, tuple)):
        return "[" + ", ".join(ausdruck(w) for w in wert) + "]"
    arten = typ(wert).removeprefix("IReadOnlyDictionary<").removesuffix(">")
    vergleich = "(StringComparer.Ordinal)" if arten.startswith("string,") else ""
    inhalt = ", ".join("[%s] = %s" % (ausdruck(a), ausdruck(b)) for a, b in wert.items())
    return "new Dictionary<%s>%s { %s }" % (arten, vergleich, inhalt)


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    if sys.version_info[:2] != (3, 12):
        print("Bitte mit Python 3.12 erzeugen (wie die CI)")
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    atexit.register(shutil.rmtree, tmp, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as core

    with open(os.path.join(repo, "fisi_core.py"), encoding="utf-8") as handle:
        baum = ast.parse(handle.read())
    zeilen = []
    anzahl = 0
    for knoten in baum.body:
        if not isinstance(knoten, ast.Assign) or not VON <= knoten.lineno <= BIS:
            continue
        for ziel_name in knoten.targets:
            if not isinstance(ziel_name, ast.Name) or ziel_name.id in HAND:
                continue
            name = ziel_name.id
            wert = getattr(core, name)
            zeile = "    // fisi_core.py %d" % knoten.lineno
            if isinstance(wert, bool):
                continue
            if isinstance(wert, int):
                zeilen += [zeile, "    public const int %s = %d;" % (name, wert)]
            elif isinstance(wert, float):
                zeilen += [zeile, "    public const double %s = %r;" % (name, wert)]
            elif isinstance(wert, str):
                zeilen += [zeile, "    public const string %s = %s;" % (name, text(wert))]
            elif nur_texte(wert):
                zeilen += [zeile, "    public static readonly %s %s = %s;"
                           % (typ(wert, True), name, ausdruck(wert, True))]
            else:
                continue
            anzahl += 1
    aufgaben = [(a["titel"], a["frage"], a["annahmen"]) for a in core.UPS_TASKS]
    zeilen += ["    // fisi_core.py UPS_TASKS: Titel, Frage, Annahmen (Zahlen \"werte\" in Usv.cs)",
               "    public static readonly %s UPS_TASK_TEXTS = %s;" % (typ(aufgaben, True), ausdruck(aufgaben, True))]
    anzahl += 1

    kopf = [
        "// <auto-generated>",
        "// Erzeugt von werkzeuge/tabellen/erzeuge_rechner_texte.py mit Python 3.12 aus",
        "// fisi_core.py v0.63.2 (Abschnitt PRAXIS-RECHNER, Zeilen %d-%d), zeichengleich." % (VON, BIS),
        "// Nicht von Hand aendern; die CI erzeugt die Datei neu und vergleicht sie.",
        "// </auto-generated>",
        "#pragma warning disable CA1707, IDE1006 // Namen wie in Python (Nachvollziehbarkeit)",
        "namespace Lernplattform.Core.Texte;",
        "",
        "public static partial class RechnerTexte",
        "{",
    ]
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(kopf + zeilen + ["}"]) + "\n")
    print("%d Texte und Werte -> %s" % (anzahl, ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
