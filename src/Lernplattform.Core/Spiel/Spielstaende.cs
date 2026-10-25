using Lernplattform.Core.Daten;
using Lernplattform.Core.Suche;
using Microsoft.Data.Sqlite;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>Eine Zeile der Tabelle spiel_plaetze (db.slot_rows()).</summary>
public sealed record PlatzZeile(string Zeit, string? Uid, long Platz, string Lauf, string Aktion, PyObjekt Daten);

/// <summary>slot_layout: {"plaetze": {platz: lauf}, "namen": {platz: name}, "extra": [lauf], "geloescht": {lauf}}.</summary>
public sealed record PlatzBelegung(List<(long Platz, string Lauf)> Plaetze, List<(long Platz, string Name)> Namen, List<string> Extra, HashSet<string> Geloescht)
{
    public string? LaufAuf(long platz) => Plaetze.Where(p => p.Platz == platz).Select(p => p.Lauf).FirstOrDefault();

    /// <summary>_known_runs: alle Durchgaenge auf Plaetzen und als Extra.</summary>
    public HashSet<string> Bekannt => [.. Plaetze.Select(p => p.Lauf), .. Extra];
}

/// <summary>
/// Phase 7, W2: Spielstand-Plaetze und Durchgaenge wie <c>fisi_game.py</c>/<c>fisi_core.py</c> (Python 0.63.2:
/// slot_layout, slot_summary, slot_line, Game.slots, _resolve_run; DBManager.game_events, slot_rows,
/// has_legacy_events, event_counts). Hier wird nur gelesen; Schreiben (Platz anlegen, umbenennen,
/// loeschen, Ereignisse) kommt mit W3 nach der Lesung durch Chat.
/// </summary>
public static class Spielstaende
{
    public const int Anzahl = 3;                     // SLOT_COUNT
    public const string Angelegt = "angelegt";       // SLOT_CREATED
    public const string Geloescht = "geloescht";     // SLOT_DELETED
    public const string Umbenannt = "umbenannt";     // SLOT_RENAMED
    public const int NameMax = 24;                   // SLOT_NAME_MAX
    public const string AlterLauf = "alt";           // LEGACY_RUN
    public const string AktiverLaufSchluessel = "spiel_aktiver_lauf";   // ACTIVE_RUN_KEY (sync_meta)
    public const string Titel = "Spielstände";       // SLOTS_TITLE
    public const string Leer = "Neuer Durchgang";    // SLOT_EMPTY
    public const string ExtraName = "Zusätzlicher Durchgang";

    public const string Hilfe = "Drei Plätze für getrennte Durchgänge. Der Lernfortschritt und die "
        + "Bestenliste gelten für alle Plätze gemeinsam. Abzeichen verdienst du "
        + "in jedem Durchgang neu, erreichte Stufen bleiben in der Bestenliste.";

    public const string ExtraHilfe = "Zusätzlicher Durchgang: PC und Handy haben gleichzeitig einen neuen "
        + "Durchgang angelegt. Er rückt auf den nächsten frei werdenden Platz.";

    /// <summary>slot_default_name</summary>
    public static string Standardname(long platz) => Prozent("Platz %d", platz);

    // -- Lesen aus der Datenbank -------------------------------------------

    private static (string Wo, string? Lauf) Bedingung(string? lauf) =>
        string.IsNullOrEmpty(lauf) || lauf == AlterLauf ? ("lauf IS NULL OR lauf = ''", null) : ("lauf = $lauf", lauf);

    private static PyObjekt? AlsDict(string? daten)
    {
        if (string.IsNullOrEmpty(daten))
        {
            return [];
        }
        try
        {
            return Py.Lesen(daten) as PyObjekt;
        }
        catch (Exception fehler) when (fehler is FormatException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>game_events(run): die Ereignisse eines Durchgangs chronologisch (alter Durchgang: lauf leer).</summary>
    public static List<SpielEreignis> LaufEreignisse(SqliteConnection verbindung, string? lauf)
    {
        var (wo, wert) = Bedingung(lauf);
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText = "SELECT timestamp, typ, daten FROM spiel_ereignisse WHERE " + wo + " ORDER BY timestamp, id";
        if (wert is not null)
        {
            befehl.Parameters.AddWithValue("$lauf", wert);
        }
        var ergebnis = new List<SpielEreignis>();
        using var leser = befehl.ExecuteReader();
        while (leser.Read())
        {
            if (AlsDict(leser.IsDBNull(2) ? null : leser.GetString(2)) is { } daten)
            {
                ergebnis.Add(new SpielEreignis(leser.GetString(0), leser.GetString(1), daten));
            }
        }
        return ergebnis;
    }

    /// <summary>slot_rows: alle Zeilen der Platz-Tabelle chronologisch (kaputte Daten = leeres dict).</summary>
    public static List<PlatzZeile> Zeilen(SqliteConnection verbindung)
    {
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText = "SELECT timestamp, uid, platz, lauf, aktion, daten FROM spiel_plaetze ORDER BY timestamp, uid";
        var ergebnis = new List<PlatzZeile>();
        using var leser = befehl.ExecuteReader();
        while (leser.Read())
        {
            ergebnis.Add(new PlatzZeile(leser.GetString(0), leser.IsDBNull(1) ? null : leser.GetString(1), leser.GetInt64(2),
                leser.GetString(3), leser.GetString(4), AlsDict(leser.IsDBNull(5) ? null : leser.GetString(5)) ?? []));
        }
        return ergebnis;
    }

    /// <summary>has_legacy_events: gibt es Spielereignisse von vor 0.48 (ohne Durchgang)?</summary>
    public static bool HatAlteEreignisse(SqliteConnection verbindung)
    {
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText = "SELECT 1 FROM spiel_ereignisse WHERE lauf IS NULL OR lauf = '' LIMIT 1";
        return befehl.ExecuteScalar() is not null;
    }

    /// <summary>event_counts: Anzahl Ereignisse je Durchgang (alter Durchgang = "alt").</summary>
    public static Dictionary<string, long> AnzahlJeLauf(SqliteConnection verbindung)
    {
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText = "SELECT COALESCE(NULLIF(lauf, ''), $alt), COUNT(*) FROM spiel_ereignisse GROUP BY 1";
        befehl.Parameters.AddWithValue("$alt", AlterLauf);
        var ergebnis = new Dictionary<string, long>();
        using var leser = befehl.ExecuteReader();
        while (leser.Read())
        {
            ergebnis[leser.GetString(0)] = leser.GetInt64(1);
        }
        return ergebnis;
    }

    /// <summary>get_meta(ACTIVE_RUN_KEY): der zuletzt auf diesem Geraet gewaehlte Durchgang ("" = keiner).</summary>
    public static string? GespeicherterLauf(SqliteConnection verbindung)
    {
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText = "SELECT value FROM sync_meta WHERE key = $k";
        befehl.Parameters.AddWithValue("$k", AktiverLaufSchluessel);
        return befehl.ExecuteScalar() as string;
    }

    // -- Belegung und Kurzinfo ---------------------------------------------

    /// <summary>
    /// slot_layout: Legen zwei Geraete offline denselben Platz an, bekommt ihn der zuerst angelegte
    /// Durchgang, der andere rueckt auf einen freien Platz (oder wartet als "extra").
    /// </summary>
    public static PlatzBelegung Belegung(IReadOnlyList<PlatzZeile> zeilen, bool alte)
    {
        var geloescht = zeilen.Where(z => z.Aktion == Geloescht).Select(z => z.Lauf).ToHashSet(StringComparer.Ordinal);
        var angelegt = new List<(string Zeit, string Uid, long Platz, string Lauf)>();
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        if (alte)
        {
            angelegt.Add(("", "", 1, AlterLauf));
            gesehen.Add(AlterLauf);
        }
        var namen = new List<(long Platz, string Name)>();
        foreach (var z in zeilen)
        {
            if (z.Aktion == Angelegt && z.Lauf.Length > 0 && gesehen.Add(z.Lauf))
            {
                angelegt.Add((z.Zeit, z.Uid ?? "", z.Platz, z.Lauf));
            }
            else if (z.Aktion == Umbenannt && 1 <= z.Platz && z.Platz <= Anzahl)
            {
                var roh = Get(z.Daten, "name");
                var name = Kurz(Rechner.PythonZeichen.Strip(Py.Wahr(roh) ? Py.Str(roh) : ""), NameMax);
                var stelle = namen.FindIndex(n => n.Platz == z.Platz);
                if (stelle >= 0)
                {
                    namen[stelle] = (z.Platz, name);
                }
                else
                {
                    namen.Add((z.Platz, name));
                }
            }
        }
        // sort ist stabil wie in Python
        angelegt = [.. angelegt.OrderBy(a => a.Zeit, StringComparer.Ordinal).ThenBy(a => a.Uid, StringComparer.Ordinal)];
        var plaetze = new List<(long, string)>();
        var extra = new List<string>();
        foreach (var (_, _, platz, lauf) in angelegt)
        {
            if (geloescht.Contains(lauf))
            {
                continue;
            }
            if (1 <= platz && platz <= Anzahl && plaetze.All(p => p.Item1 != platz))
            {
                plaetze.Add((platz, lauf));
                continue;
            }
            var frei = Enumerable.Range(1, Anzahl).Select(n => (long)n).Where(n => plaetze.All(p => p.Item1 != n)).ToList();
            if (frei.Count > 0)
            {
                plaetze.Add((frei[0], lauf));
            }
            else
            {
                extra.Add(lauf);
            }
        }
        return new PlatzBelegung(plaetze, [.. namen.Where(n => n.Name.Length > 0)], extra, geloescht);
    }

    private static string Kurz(string text, int max) => string.Concat(text.EnumerateRunes().Take(max).Select(r => r.ToString()));

    /// <summary>slot_name</summary>
    public static string PlatzName(long platz, PlatzBelegung belegung) =>
        belegung.Namen.Where(n => n.Platz == platz).Select(n => n.Name).FirstOrDefault() is { Length: > 0 } name ? name : Standardname(platz);

    /// <summary>difficulty_name</summary>
    public static string SchwierigkeitName(object? stufe, PyObjekt inhalt)
    {
        var regeln = Objekt(inhalt["balancing"])["schwierigkeit"] is PyObjekt r && Py.Wahr(r) ? r : [];
        var stufen = Py.Wahr(Get(regeln, "stufen")) ? Liste(regeln["stufen"]).Select(Objekt).ToList()
            : [new PyObjekt { { "id", Ereignisse.Einfach }, { "name", "Einfach" } }, new PyObjekt { { "id", Ereignisse.Normal }, { "name", "Normal" } }];
        var kennung = Py.Wahr(stufe) ? Py.Str(stufe) : Ereignisse.Normal;
        // dict: der letzte Eintrag mit derselben Kennung gilt
        var treffer = stufen.LastOrDefault(s => Py.Gleich(s["id"], kennung));
        if (treffer is not null)
        {
            return Py.Str(treffer["name"]);
        }
        return kennung.Length == 0 ? "" : Kurz(kennung, 1).ToUpperInvariant() + PythonKlein.Klein(kennung[Kurz(kennung, 1).Length..]);
    }

    /// <summary>slot_summary: Kurzinfo eines Durchgangs fuer den Auswahlbildschirm.</summary>
    public static PyObjekt Kurzinfo(Spielstand? stand)
    {
        if (stand?.Profil is null)
        {
            return new PyObjekt { { "leer", true } };
        }
        var aussehen = Get(stand.Profil, "aussehen");
        return new PyObjekt
        {
            { "leer", false }, { "spieler", Get(stand.Profil, "name", "") },
            { "aussehen", Py.Wahr(aussehen) ? aussehen : Aussehen.Voreinstellung() },
            { "schwierigkeit", SchwierigkeitName(stand.Stufe, stand.Inhalt) }, { "tag", stand.Tag },
            { "firma", stand.Firma is PyObjekt f && Py.Wahr(Get(f, "name")) ? f["name"] : "" },
        };
    }

    /// <summary>slot_line: eine Zeile Text zu einem Platz (z.B. fuer die Loeschabfrage).</summary>
    public static string Zeile(PyObjekt platz)
    {
        if (Py.Wahr(Get(platz, "leer", true)))
        {
            return Prozent("%s (%s)", platz["name"], Leer);
        }
        var teile = new List<string> { Py.Str(platz["spieler"]), Py.Str(platz["schwierigkeit"]), Prozent("Tag %d", platz["tag"]) };
        teile.Add(Py.Wahr(platz["firma"]) ? "Firma " + Py.Str(platz["firma"]) : "noch angestellt");
        return Prozent("%s (%s)", platz["name"], string.Join(", ", teile));
    }

    /// <summary>_resolve_run ohne Schreiben: der gespeicherte Durchgang, wenn es ihn noch gibt, sonst der auf dem kleinsten Platz.</summary>
    public static string? AktiverLauf(string? gespeichert, PlatzBelegung belegung)
    {
        if (!string.IsNullOrEmpty(gespeichert) && belegung.Bekannt.Contains(gespeichert))
        {
            return gespeichert;
        }
        return belegung.Plaetze.Count > 0 ? belegung.Plaetze.MinBy(p => p.Platz).Lauf : null;
    }

    /// <summary>Game.slots: die drei Plaetze fuer den Auswahlbildschirm, dazu seltene Extras.</summary>
    public static List<PyObjekt> Uebersicht(SqliteConnection verbindung, string? aktiv, PyObjekt inhalt)
    {
        var belegung = Belegung(Zeilen(verbindung), HatAlteEreignisse(verbindung));
        var anzahl = AnzahlJeLauf(verbindung);
        PyObjekt Info(string lauf) => anzahl.GetValueOrDefault(lauf) > 0
            ? Kurzinfo(new Spielstand(LaufEreignisse(verbindung, lauf), inhalt))
            : Kurzinfo(null);
        var ergebnis = new List<PyObjekt>();
        for (var platz = 1L; platz <= Anzahl; platz++)
        {
            var lauf = belegung.LaufAuf(platz);
            var eintrag = new PyObjekt
            {
                { "platz", platz }, { "name", PlatzName(platz, belegung) }, { "lauf", lauf },
                { "aktiv", lauf is not null && lauf == aktiv }, { "extra", false },
            };
            foreach (var (k, v) in lauf is not null ? Info(lauf) : new PyObjekt { { "leer", true } })
            {
                Py.Setzen(eintrag, k, v);
            }
            ergebnis.Add(eintrag);
        }
        foreach (var lauf in belegung.Extra)
        {
            var eintrag = new PyObjekt { { "platz", 0L }, { "name", ExtraName }, { "lauf", lauf }, { "aktiv", lauf == aktiv }, { "extra", true } };
            foreach (var (k, v) in Info(lauf))
            {
                Py.Setzen(eintrag, k, v);
            }
            ergebnis.Add(eintrag);
        }
        return ergebnis;
    }
}
