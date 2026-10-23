#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Beleg fuer Phase 5, Welle 1: "Alles ersetzen" der C#-Linie gegen die Bedeutung von
Python v0.63.2 (fisi_sicherung.replace_all). Unabhaengig von der C#-Umsetzung geschrieben.
Nur synthetische Daten (A3).

Je Fall (Unterordner des Beleg-Ordners, mindestens a_gefuellt, a_leer, b_gefuellt,
c_gefuellt, d_aufhebung, e_gefuellt) liegen: vorher.db, einstellungen_vorher.json,
sicherung.fisisicherung, nachher.db, kopie.fisisicherung, einstellungen_nachher.json.

  W1  Zeilen: jede Tabelle aus SYNC_TABLES enthaelt in nachher.db genau die Zeilen der
      Sicherung (_records, gleiche uid zaehlt einmal wie INSERT OR IGNORE), verglichen ueber
      uid und Inhaltsspalten (ohne id). Eine Tabelle, die die Sicherung nicht traegt, ist leer
      (replace_all leert jede Tabelle aus SYNC_TABLES). Andere Tabellen bleiben unveraendert.
  W2  Neue Nummern: jede id in nachher.db liegt ueber max(id, sqlite_sequence) von vorher.db
      (Python garantiert das ueber AUTOINCREMENT; geprueft wird es nur fuer Tabellen, die in
      vorher.db AUTOINCREMENT haben).
  W3  Loeschmarken (fisi_sync.MARKERS) in nachher.db = Marken der Sicherung (nur gesetzte).
  W4  Aufhebungen ("aufgehoben:"+Marke) = replace_lifts(alte Marken, alte Aufhebungen,
      neue Marken) als Text (json.dumps); ohne neue Aufhebung bleibt der alte Wert.
      Alle uebrigen sync_meta-Schluessel bleiben unveraendert.
  W5  kopie.fisisicherung besteht read_backup, ihre Daten = export_local(vorher.db),
      Einstellungen = Tagesziel-Werte von vorher, Zusammenfassung passt.
  W6  einstellungen_nachher.json: Tagesziel-Schluessel = goal_settings(Sicherung), alle
      anderen Schluessel wie einstellungen_vorher.json; Dateirechte 0600 (ausser Windows).
  W7  Gegenprobe: replace_all von Python auf einer Kopie von vorher.db ergibt dieselben
      Zeilen (mit SQLite-Typ), dieselbe sync_meta und dieselben Einstellungen.

Aufruf:
  python3 -I werkzeuge/vergleich/pruefe_wiederherstellung.py <pfad_fisi_lernplattform> <beleg_ordner>
Rueckgabe 0 = alles belegt, 1 = Abweichung (Zeilen "FEHLER: ..."), 2 = falscher Aufruf.
"""
import datetime
import json
import os
import shutil
import sqlite3
import stat
import sys
import tempfile
import time
import types

FAELLE = ("a_gefuellt", "a_leer", "b_gefuellt", "c_gefuellt", "d_aufhebung", "e_gefuellt")
DATEIEN = ("vorher.db", "einstellungen_vorher.json", "sicherung.fisisicherung", "nachher.db",
           "kopie.fisisicherung", "einstellungen_nachher.json")


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ordner = sys.argv[1], sys.argv[2]
    if not os.path.isdir(repo) or not os.path.isdir(ordner):
        print(__doc__)
        return 2
    os.environ["TZ"] = "Europe/Berlin"
    time.tzset()
    tmp = tempfile.mkdtemp(prefix="beleg_wh_")
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as core
    import fisi_lernen as lernen
    import fisi_sicherung as sich
    import fisi_sync as sync

    fehler = []

    def fehlt(fall, text):
        fehler.append("%s: %s" % (fall, text))
        print("FEHLER: %s: %s" % (fall, text))

    # -- Faelle einsammeln ---------------------------------------------------
    faelle = sorted(d for d in os.listdir(ordner) if os.path.isdir(os.path.join(ordner, d)))
    for name in FAELLE:
        if name not in faelle:
            fehlt(name, "Fall fehlt im Beleg-Ordner")
    for name in faelle:
        for datei in DATEIEN:
            if not os.path.isfile(os.path.join(ordner, name, datei)):
                fehlt(name, "Datei %s fehlt" % datei)
    if fehler:
        print("NICHT BELEGT: %d Punkt(e)" % len(fehler))
        return 1

    def nur_lesen(pfad):
        return sqlite3.connect("file:%s?mode=ro" % pfad, uri=True)

    def tabellen(conn):
        return [r[0] for r in conn.execute(
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")]

    def inhalt(conn, tabelle, mit_typ=False):
        """Zeilen (uid, Spalten...) einer SYNC-Tabelle ohne id, sortiert."""
        spalten = ["uid"] + list(core.SYNC_TABLES[tabelle])
        if mit_typ:
            teile = []
            for s in spalten:
                teile += [s, "typeof(%s)" % s]
            auswahl = ", ".join(teile)
        else:
            auswahl = ", ".join(spalten)
        zeilen = conn.execute("SELECT %s FROM %s" % (auswahl, tabelle)).fetchall()
        return sorted(zeilen, key=repr)

    def roh_tabelle(conn, tabelle):
        return sorted(conn.execute("SELECT * FROM %s" % tabelle).fetchall(), key=repr)

    def meta(conn):
        return dict(conn.execute("SELECT key, value FROM sync_meta"))

    def autoincrement(conn, tabelle):
        sql = conn.execute("SELECT sql FROM sqlite_master WHERE type='table' AND name=?",
                           (tabelle,)).fetchone()
        return bool(sql and sql[0] and "AUTOINCREMENT" in sql[0].upper())

    def json_laden(pfad):
        with open(pfad, encoding="utf-8") as handle:
            return json.load(handle)

    def gleich(a, b):
        # strenger Vergleich mit Typ (True ist nicht 1, 1 ist nicht "1")
        return json.dumps(a, sort_keys=True) == json.dumps(b, sort_keys=True)

    def kurz(wert):
        text = repr(wert)
        return text if len(text) <= 300 else text[:300] + "..."

    for fall in faelle:
        pfad = os.path.join(ordner, fall)
        datei = lambda n: os.path.join(pfad, n)
        vorher_fehler = len(fehler)

        # Sicherung lesen (Python-Pruefung)
        with open(datei("sicherung.fisisicherung"), "rb") as handle:
            roh = handle.read()
        try:
            backup = sich.read_backup(roh)
        except sich.BackupError as error:
            fehlt(fall, "read_backup lehnt sicherung.fisisicherung ab: %s" % error)
            continue
        daten = backup.get("daten") or {}

        vorher = nur_lesen(datei("vorher.db"))
        nachher = nur_lesen(datei("nachher.db"))
        try:
            # -- W1 Zeilen ----------------------------------------------------
            zeilen_gesamt = 0
            for tabelle, spalten in core.SYNC_TABLES.items():
                erwartet, gesehen = [], set()
                for record in sich._records(daten, tabelle):
                    if record["uid"] in gesehen:      # INSERT OR IGNORE, uid eindeutig
                        continue
                    gesehen.add(record["uid"])
                    erwartet.append(tuple([record["uid"]] + [record[c] for c in spalten]))
                erwartet.sort(key=repr)
                ist = inhalt(nachher, tabelle)
                zeilen_gesamt += len(ist)
                if [tuple(z) for z in ist] != erwartet:
                    e_uid = {z[0]: z for z in erwartet}
                    i_uid = {z[0]: tuple(z) for z in ist}
                    zuviel = sorted(set(i_uid) - set(e_uid), key=repr)
                    fehlen = sorted(set(e_uid) - set(i_uid), key=repr)
                    anders = sorted((u for u in set(e_uid) & set(i_uid) if e_uid[u] != i_uid[u]),
                                    key=repr)
                    fehlt(fall, "W1 %s: zu viel %s, fehlt %s, anders %s" % (
                        tabelle, kurz(zuviel), kurz(fehlen),
                        kurz([(e_uid[u], i_uid[u]) for u in anders])))
            andere = [t for t in tabellen(vorher) if t not in core.SYNC_TABLES and t != "sync_meta"]
            for tabelle in andere:
                if tabelle not in tabellen(nachher) or \
                        roh_tabelle(vorher, tabelle) != roh_tabelle(nachher, tabelle):
                    fehlt(fall, "W1 Tabelle %s (nicht abgeglichen) hat sich geaendert" % tabelle)
            neu_tabellen = set(tabellen(nachher)) - set(tabellen(vorher))
            if neu_tabellen and tabellen(vorher):
                fehlt(fall, "W1 neue Tabellen in nachher.db: %s" % sorted(neu_tabellen))

            # -- W2 neue Nummern ----------------------------------------------
            sequenz = dict(vorher.execute("SELECT name, seq FROM sqlite_sequence")) \
                if vorher.execute("SELECT 1 FROM sqlite_master WHERE name='sqlite_sequence'"
                                  ).fetchone() else {}
            for tabelle in core.SYNC_TABLES:
                if tabelle not in tabellen(vorher):
                    continue
                if not autoincrement(vorher, tabelle):
                    # Python garantiert neue Nummern nur ueber AUTOINCREMENT
                    continue
                alt_max = vorher.execute("SELECT max(id) FROM %s" % tabelle).fetchone()[0] or 0
                grenze = max(alt_max, int(sequenz.get(tabelle) or 0))
                if grenze == 0:
                    continue
                klein = nachher.execute("SELECT min(id) FROM %s" % tabelle).fetchone()[0]
                if klein is not None and klein <= grenze:
                    fehlt(fall, "W2 %s: kleinste neue id %d nicht ueber %d" % (tabelle, klein, grenze))

            # -- W3 Loeschmarken ----------------------------------------------
            m_vorher, m_nachher = meta(vorher), meta(nachher)
            neue_marken = {k: v for k, v in (daten.get("markers") or {}).items()
                           if k in sync.MARKERS and v}
            ist_marken = {k: v for k, v in m_nachher.items() if k in sync.MARKERS}
            if ist_marken != neue_marken:
                fehlt(fall, "W3 Marken: erwartet %s, ist %s" % (kurz(neue_marken), kurz(ist_marken)))

            # -- W4 Aufhebungen -----------------------------------------------
            lifts = sync.replace_lifts(
                {k: m_vorher.get(k) for k in sync.MARKERS},
                {k: m_vorher.get(sync.LIFT_PREFIX + k) for k in sync.MARKERS}, neue_marken)
            erwartet_meta = {k: v for k, v in m_vorher.items() if k not in sync.MARKERS}
            erwartet_meta.update(neue_marken)
            for k, paar in lifts.items():
                erwartet_meta[sync.LIFT_PREFIX + k] = json.dumps(paar)
            for k in sync.MARKERS:
                schluessel = sync.LIFT_PREFIX + k
                if erwartet_meta.get(schluessel) != m_nachher.get(schluessel):
                    fehlt(fall, "W4 %s: erwartet %r, ist %r" % (
                        schluessel, erwartet_meta.get(schluessel), m_nachher.get(schluessel)))
            rest_e = {k: v for k, v in erwartet_meta.items()
                      if k not in sync.MARKERS and not k.startswith(sync.LIFT_PREFIX)}
            rest_i = {k: v for k, v in m_nachher.items()
                      if k not in sync.MARKERS and not k.startswith(sync.LIFT_PREFIX)}
            # Aufhebungen zu Schluesseln ausserhalb von MARKERS bleiben ebenfalls stehen
            for k, v in m_vorher.items():
                if k.startswith(sync.LIFT_PREFIX) and k[len(sync.LIFT_PREFIX):] not in sync.MARKERS:
                    rest_e[k] = v
            for k, v in m_nachher.items():
                if k.startswith(sync.LIFT_PREFIX) and k[len(sync.LIFT_PREFIX):] not in sync.MARKERS:
                    rest_i[k] = v
            if rest_e != rest_i:
                fehlt(fall, "W4 uebrige sync_meta: erwartet %s, ist %s" % (kurz(rest_e), kurz(rest_i)))

            # -- W5 Sicherheitskopie ------------------------------------------
            with open(datei("kopie.fisisicherung"), "rb") as handle:
                kopie_roh = handle.read()
            try:
                kopie = sich.read_backup(kopie_roh)
            except sich.BackupError as error:
                kopie = None
                fehlt(fall, "W5 read_backup lehnt kopie.fisisicherung ab: %s" % error)
            if kopie is not None:
                arbeits = os.path.join(tmp, fall + "_export.db")
                shutil.copyfile(datei("vorher.db"), arbeits)
                erwartet_export = sync.export_local(types.SimpleNamespace(db_path=arbeits))
                k_daten = dict(kopie["daten"])
                e_daten = dict(erwartet_export)
                # "schreiber" wertet Python nur auf vorhanden aus (siehe pruefe_ruecksicherung A3)
                if (k_daten.pop(sync.WRITER_KEY, None) is None) != \
                        (e_daten.pop(sync.WRITER_KEY, None) is None):
                    fehlt(fall, "W5 Feld schreiber vorhanden/fehlt anders als export_local")
                for schluessel in sorted(set(k_daten) | set(e_daten)):
                    if not gleich(k_daten.get(schluessel), e_daten.get(schluessel)):
                        fehlt(fall, "W5 Kopie daten.%s: erwartet %s, ist %s" % (
                            schluessel, kurz(e_daten.get(schluessel)), kurz(k_daten.get(schluessel))))
                e_einst = sich._goal_values(dict({"auto_check": True},
                                                 **json_laden(datei("einstellungen_vorher.json"))))
                if not gleich(kopie.get("einstellungen"), e_einst):
                    fehlt(fall, "W5 Kopie einstellungen: erwartet %s, ist %s" % (
                        kurz(e_einst), kurz(kopie.get("einstellungen"))))
                if not gleich(kopie.get("zusammenfassung"), sich._summary_counts(kopie["daten"])):
                    fehlt(fall, "W5 Kopie zusammenfassung passt nicht zu den Daten")

            # -- W6 Einstellungen ---------------------------------------------
            e_vorher = json_laden(datei("einstellungen_vorher.json"))
            e_nachher = json_laden(datei("einstellungen_nachher.json"))
            ziel = lernen.goal_settings(backup.get("einstellungen") or {})
            for k in lernen.GOAL_SETTINGS:
                if not gleich(e_nachher.get(k), ziel[k]):
                    fehlt(fall, "W6 %s: erwartet %r, ist %r" % (k, ziel[k], e_nachher.get(k)))
            for k in sorted((set(e_vorher) | set(e_nachher)) - set(lernen.GOAL_SETTINGS)):
                if k == "auto_check" and k not in e_vorher and e_nachher.get(k) is True:
                    # Python (load_settings) fuegt den Vorgabewert auto_check=True hinzu
                    continue
                if k not in e_vorher or k not in e_nachher or not gleich(e_vorher[k], e_nachher[k]):
                    fehlt(fall, "W6 Schluessel %s: vorher %s, nachher %s" % (
                        k, "da" if k in e_vorher else "fehlt", "da" if k in e_nachher else "fehlt"))
            if os.name != "nt":
                modus = stat.S_IMODE(os.stat(datei("einstellungen_nachher.json")).st_mode)
                if modus != 0o600:
                    fehlt(fall, "W6 Dateirechte %o statt 600" % modus)

            # -- W7 Gegenprobe mit replace_all --------------------------------
            probe = os.path.join(tmp, fall + "_probe")
            os.makedirs(probe)
            probe_db = os.path.join(probe, "lernplattform.db")
            shutil.copyfile(datei("vorher.db"), probe_db)
            shutil.copyfile(datei("einstellungen_vorher.json"), os.path.join(probe, "einstellungen.json"))
            os.environ["FISI_DB_PATH"] = probe_db
            sich.replace_all(types.SimpleNamespace(db_path=probe_db, read_only_reason=None),
                             sich.read_backup(roh), os.path.join(probe, "sicherungen"))
            gegen = nur_lesen(probe_db)
            try:
                for tabelle in core.SYNC_TABLES:
                    if inhalt(gegen, tabelle, True) != inhalt(nachher, tabelle, True):
                        fehlt(fall, "W7 %s: Zeilen oder SQLite-Typen anders als replace_all" % tabelle)
                if meta(gegen) != m_nachher:
                    fehlt(fall, "W7 sync_meta: replace_all %s, C# %s" % (kurz(meta(gegen)), kurz(m_nachher)))
            finally:
                gegen.close()
            e_gegen = json_laden(os.path.join(probe, "einstellungen.json"))
            if "auto_check" not in e_vorher:
                e_gegen.pop("auto_check", None)
                e_nachher = {k: v for k, v in e_nachher.items() if k != "auto_check"}
            if not gleich(e_gegen, e_nachher):
                # nur Schluesselnamen ausgeben (Werte koennen einen Zugangsschluessel enthalten)
                anders = sorted(k for k in set(e_gegen) | set(e_nachher)
                                if k not in e_gegen or k not in e_nachher
                                or not gleich(e_gegen[k], e_nachher[k]))
                fehlt(fall, "W7 Einstellungen anders als replace_all bei: %s" % ", ".join(anders))
            os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")

            if len(fehler) == vorher_fehler:
                print("OK %s: %d Zeilen in %d Tabellen, %d Marken, %d neue Aufhebung(en), "
                      "Kopie %d Zeilen, %d Einstellungen" % (
                          fall, zeilen_gesamt, len(core.SYNC_TABLES), len(neue_marken), len(lifts),
                          sum(len(r) for r in (kopie["daten"]["tables"].values() if kopie else [])),
                          len(e_nachher)))
        finally:
            vorher.close()
            nachher.close()

    shutil.rmtree(tmp, ignore_errors=True)
    if fehler:
        print("NICHT BELEGT: %d Punkt(e)" % len(fehler))
        return 1
    print("Alles belegt: %d Faelle (Python %s)." % (len(faelle), sys.version.split()[0]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
