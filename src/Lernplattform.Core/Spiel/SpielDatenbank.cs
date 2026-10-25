using Lernplattform.Core.Daten;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W8 (Weisung P7-5, 2c; R3): ein Durchgang in <c>lernplattform.db</c> wie <c>Game.db</c> in
/// Python 0.63.2. Gelesen wird wie DBManager.game_events/records, geschrieben nur ueber
/// <see cref="LernstandSchreiber"/> (angehaengte Zeilen) und nur innerhalb einer <see cref="Sitzung"/>:
/// sie haelt die Sperre aus <see cref="Vorgaenge"/> ohne zu warten, so schreibt das Spiel nie neben
/// Abgleich, Uebernahme, Update oder Sicherung einspielen. Geraet und Durchgang (lauf) stehen in
/// jeder Zeile wie in Python, damit der Spiel-Abgleich (Phase 8) darauf aufbauen kann.
/// </summary>
/// <param name="sperre">Nur fuer Tests: wie die Sperre geholt wird (Vorgabe <see cref="Vorgaenge.Versuchen"/>,
/// ohne Warten). Tests, die parallel zu Abgleich- und Uebernahme-Tests laufen, warten stattdessen
/// mit <see cref="Vorgaenge.Sperren"/> auf dieselbe Sperre.</param>
public sealed class SpielDatenbank(LernstandSchreiber schreiber, string geraet, string? lauf, Func<IDisposable?>? sperre = null) : ISpielSpeicher
{
    private readonly Func<IDisposable?> holen = sperre ?? Vorgaenge.Versuchen;
    private Sitzungsmarke? offen;

    public Lernstand Lernstand => schreiber.Lernstand;

    public string Geraet { get; } = geraet;

    /// <summary>Der Durchgang (null, leer oder "alt": der alte Durchgang ohne lauf).</summary>
    public string? Lauf { get; } = lauf;

    public IDisposable? Sitzung()
    {
        if (offen is not null)
        {
            throw new InvalidOperationException("Sitzung des Spiels ist schon offen");
        }
        if (holen() is not { } gehalten)
        {
            return null;
        }
        return offen = new Sitzungsmarke(this, gehalten);
    }

    private void Pruefen()
    {
        if (offen is null)
        {
            throw new InvalidOperationException("Das Spiel schreibt nur in einer Sitzung (Sperre der Vorgaenge)");
        }
    }

    public string? Schreiben(string art, string daten)
    {
        Pruefen();
        return schreiber.SpielEreignisSpeichern(art, daten, Geraet, Lauf);
    }

    public List<SpielEreignis> Ereignisse()
    {
        try
        {
            using var verbindung = Lernstand.Verbindung();
            return Spielstaende.LaufEreignisse(verbindung, Lauf);
        }
        catch (SqliteException)
        {
            return [];   // Python: _execute(default=[])
        }
    }

    private (string Wo, string? Wert) Bedingung() =>
        string.IsNullOrEmpty(Lauf) || Lauf == LernstandSchreiber.AlterLauf ? ("(lauf IS NULL OR lauf = '')", null) : ("lauf = $lauf", Lauf);

    /// <summary>game_event_stamp: COUNT, MAX(id), MAX(timestamp) des Durchgangs; null bei einem Lesefehler (Abfragen meldet ihn).</summary>
    public SpielStempel? Stempel()
    {
        var (wo, wert) = Bedingung();
        const string Sql = "SELECT COUNT(*), MAX(id), MAX(timestamp) FROM spiel_ereignisse WHERE ";
        var zeilen = wert is null ? Lernstand.Abfragen(Sql + wo) : Lernstand.Abfragen(Sql + wo, ("$lauf", wert));
        return zeilen is [var z, ..] ? new SpielStempel((long)z[0]!, z[1] as long?, z[2]) : null;
    }

    /// <summary>Die Zeilen des Durchgangs nach der Nummer <paramref name="nummer"/> (leer bei einem Lesefehler).</summary>
    public List<SpielZeile> ZeilenNach(long nummer)
    {
        var (wo, wert) = Bedingung();
        var sql = "SELECT id, timestamp, typ, daten FROM spiel_ereignisse WHERE " + wo + " AND id > $nr ORDER BY id";
        var zeilen = wert is null ? Lernstand.Abfragen(sql, ("$nr", nummer)) : Lernstand.Abfragen(sql, ("$lauf", wert), ("$nr", nummer));
        return [.. zeilen.Select(z => new SpielZeile((long)z[0]!, z[1], z[2] as string ?? "", z[3] as string))];
    }

    public bool BestenSchreiben(string lauf, string art, string schluessel, object? wert, string daten)
    {
        Pruefen();
        return schreiber.BestenSpeichern(lauf, art, schluessel, wert, daten);
    }

    /// <summary>records: alle Zeilen der Bestenliste (ueber alle Durchgaenge), kaputte Daten = leeres dict.</summary>
    public List<BestenZeile> Bestenliste() => Bestenzeilen(Lernstand);

    /// <summary>records() fuer eine Datenbank.</summary>
    public static List<BestenZeile> Bestenzeilen(Lernstand lernstand)
    {
        var zeilen = new List<BestenZeile>();
        foreach (var z in lernstand.Abfragen("SELECT timestamp, durchgang, art, schluessel, wert, daten FROM spiel_bestenliste ORDER BY timestamp, id"))
        {
            PyObjekt daten;
            try
            {
                daten = z[5] is string { Length: > 0 } text ? Py.Lesen(text) as PyObjekt ?? [] : [];
            }
            catch (Exception fehler) when (fehler is FormatException or System.Text.Json.JsonException or InvalidOperationException)
            {
                daten = [];
            }
            zeilen.Add(new BestenZeile(Convert.ToString(z[0], System.Globalization.CultureInfo.InvariantCulture) ?? "",
                z[1] as string ?? "", z[2] as string ?? "", z[3] as string ?? "",
                z[4] switch { long n => n, double d => d, _ => null }, daten));
        }
        return zeilen;
    }

    public bool BestenlisteLoeschen()
    {
        Pruefen();
        return schreiber.BestenlisteLoeschen();
    }

    private sealed class Sitzungsmarke(SpielDatenbank speicher, IDisposable sperre) : IDisposable
    {
        private int frei;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref frei, 1) == 0)
            {
                speicher.offen = null;
                sperre.Dispose();
            }
        }
    }
}
