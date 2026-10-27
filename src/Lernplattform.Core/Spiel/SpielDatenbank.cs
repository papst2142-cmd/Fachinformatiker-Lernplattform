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
/// <param name="schlange">Weisung P7-17: schreibt im Hintergrund (null: sofort im eigenen Faden wie vorher).
/// Dann gilt eine Aktion sofort, ihre Zeilen kommen in die <see cref="Spielschlange"/>; was das Spiel bis
/// dahin liest (Stempel, neue Zeilen, Bestenliste), kommt aus einem Zwischenstand im Speicher, alles andere
/// erst nach dem Leeren aus der Datenbank. Die Sperre der Vorgaenge bleibt, bis die Zeilen geschrieben sind.</param>
public sealed class SpielDatenbank(LernstandSchreiber schreiber, string geraet, string? lauf, Func<IDisposable?>? sperre = null,
    Spielschlange? schlange = null) : ISpielSpeicher
{
    private readonly Func<IDisposable?> holen = sperre ?? Vorgaenge.Versuchen;
    private readonly object zustand = schlange?.Sperre ?? new object();
    private Sitzungsmarke? offen;
    private IDisposable? gehalten;
    private Zwischenstand? zwischen;
    private bool gestoert;

    /// <summary>
    /// Weisung P7-17: der Stand des Protokolls mit den noch nicht geschriebenen Zeilen. Die Nummern der neuen
    /// Zeilen sind vorhergesagt wie SQLite sie vergibt (AUTOINCREMENT, nur dieses Spiel schreibt, solange es
    /// die Sperre haelt). Stimmt eine Vorhersage nicht, passt der Stempel danach nicht mehr, und die naechste
    /// Aktion rechnet voll neu (wie nach jeder fremden Aenderung des Protokolls).
    /// </summary>
    private sealed class Zwischenstand
    {
        public required SpielStempel Stempel;
        public required long Basis;
        public required long Naechste;
        public required object? Uhrabweichung;
        public required object? Loeschmarke;
        public required List<BestenZeile> Besten;
        public List<SpielZeile> Zeilen { get; } = [];
    }

    public Lernstand Lernstand => schreiber.Lernstand;

    public string Geraet { get; } = geraet;

    /// <summary>Der Durchgang (null, leer oder "alt": der alte Durchgang ohne lauf).</summary>
    public string? Lauf { get; } = lauf;

    /// <summary>Die Schlange, in die geschrieben wird (null: sofort).</summary>
    public Spielschlange? Schlange => schlange;

    public IDisposable? Sitzung()
    {
        for (var versuch = 0; ; versuch++)
        {
            lock (zustand)
            {
                if (offen is not null)
                {
                    throw new InvalidOperationException("Sitzung des Spiels ist schon offen");
                }
                // Weisung P7-17: haelt die Datenbank die Sperre noch (Zeilen der letzten Aktion offen), gilt sie weiter
                if (gehalten is not null)
                {
                    return offen = new Sitzungsmarke(this);
                }
            }
            // Die Sperre ausserhalb von zustand holen: ein wartendes Holen darf den Schreiber nie aufhalten
            var neu = holen();
            if (neu is not null)
            {
                lock (zustand)
                {
                    if (gehalten is null && offen is null)
                    {
                        gehalten = neu;
                        return offen = new Sitzungsmarke(this);
                    }
                }
                neu.Dispose();
                continue;
            }
            // Belegt: haelt sie eine andere Datenbank dieser Schlange (Verwaltung) nur bis zum Schreiben, einmal leeren
            if (versuch > 0 || schlange is null || schlange.Anzahl == 0)
            {
                return null;
            }
            schlange.Leeren();
        }
    }

    /// <summary>Ende einer Sitzung: die Sperre geht erst, wenn alle Zeilen geschrieben sind.</summary>
    private void SitzungZu()
    {
        lock (zustand)
        {
            offen = null;
            if (schlange is null || schlange.Offen(this) == 0)
            {
                Freigeben();
            }
        }
    }

    /// <summary>Vom Schreiber (unter der Sperre der Schlange): alle Zeilen dieser Datenbank sind geschrieben oder verworfen.</summary>
    internal void Geschrieben()
    {
        zwischen = null;
        if (offen is null)
        {
            Freigeben();
        }
    }

    /// <summary>
    /// Vom Schreiber (unter der Sperre der Schlange): eine Zeile ist nicht geschrieben. Bis der Stand aus der
    /// Datenbank neu gelesen ist (<see cref="Ereignisse"/>), schreibt keine Aktion mehr (sie baute auf der
    /// fehlenden Zeile auf): Schreiben gibt null wie ein Schreibfehler beim sofortigen Schreiben.
    /// </summary>
    internal void Gestoert()
    {
        gestoert = true;
        zwischen = null;   // Stempel und neue Zeilen wieder aus der Datenbank: die naechste Aktion rechnet voll neu
    }

    private void Freigeben()
    {
        zwischen = null;
        var frei = gehalten;
        gehalten = null;
        frei?.Dispose();
    }

    /// <summary>Zwischenstand anlegen (vor der ersten Zeile einer Aktion; die Schlange ist dann fuer diese Datenbank leer).</summary>
    private Zwischenstand ZwischenAnlegen()
    {
        var stempel = DatenbankStempel() ?? new SpielStempel(0, null, null);
        var naechste = Lernstand.Abfragen(
            "SELECT MAX(COALESCE((SELECT seq FROM sqlite_sequence WHERE name = 'spiel_ereignisse'), 0),"
            + " COALESCE((SELECT MAX(id) FROM spiel_ereignisse), 0))") is [var z, ..] && z[0] is long n ? n + 1 : 1;
        return new Zwischenstand
        {
            Stempel = stempel,
            Basis = stempel.Nummer ?? 0,
            Naechste = naechste,
            Uhrabweichung = UhrabweichungGemerkt(),
            Loeschmarke = schreiber.BestenMarkeLesen(),
            Besten = Bestenzeilen(Lernstand),
        };
    }

    private (long Wechsel, object? Wert)? uhrabweichung;
    private int uhrabweichungGelesen;

    /// <summary>
    /// Weisung P8-6 (Sammelliste 108): die Uhrabweichung aus sync_meta einmal je Sitzung des Spiels statt je
    /// Aktion. Sie schreibt nur der Abgleich, und nur er, Uebernahme, Sicherung einspielen, Loeschen und Update
    /// aendern sync_meta oder tauschen die Datenbank, alle mit <see cref="Vorgaenge.Sperren"/>. Hat seit dem
    /// Lesen einer von ihnen die Sperre gehabt (<see cref="Vorgaenge.Wechsel"/>), wird neu gelesen. Aufgerufen
    /// nur unter der Sperre der Vorgaenge (in einer Sitzung) und unter <c>zustand</c>.
    /// </summary>
    private object? UhrabweichungGemerkt()
    {
        var wechsel = Vorgaenge.Wechsel;
        if (uhrabweichung is { } gemerkt && gemerkt.Wechsel == wechsel)
        {
            return gemerkt.Wert;
        }
        var wert = schreiber.UhrabweichungLesen();
        uhrabweichungGelesen++;
        uhrabweichung = (wechsel, wert);
        return wert;
    }

    /// <summary>Wie oft die Uhrabweichung aus der Datenbank gelesen wurde (nur Tests, Weisung P8-6).</summary>
    internal int UhrabweichungGelesen => uhrabweichungGelesen;

    private static object? Spaeter(object? alt, string neu) =>
        alt is string a && string.CompareOrdinal(a, neu) >= 0 ? alt : neu;

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
        if (schlange is null)
        {
            return schreiber.SpielEreignisSpeichern(art, daten, Geraet, Lauf);
        }
        lock (zustand)
        {
            if (gestoert)
            {
                return null;
            }
            var z = zwischen ??= ZwischenAnlegen();
            var marke = schreiber.SpielMarkeJetzt(z.Uhrabweichung);
            var nummer = z.Naechste++;
            z.Zeilen.Add(new SpielZeile(nummer, marke.Zeit, art, daten));
            z.Stempel = new SpielStempel(z.Stempel.Anzahl + 1, nummer, Spaeter(z.Stempel.Zeit, marke.Zeit));
            schlange.Anhaengen(new Spielschlange.Eintrag(this, art, () => schreiber.SpielEreignisSpeichern(art, daten, Geraet, Lauf, marke) is not null));
            return marke.Zeit;
        }
    }

    /// <summary>Alle Zeilen dieses Spiels fertig schreiben (ohne Schlange nichts zu tun).</summary>
    public void Leeren() => schlange?.Leeren();

    public List<SpielEreignis> Ereignisse()
    {
        Leeren();
        try
        {
            using var verbindung = Lernstand.Verbindung();
            var ereignisse = Spielstaende.LaufEreignisse(verbindung, Lauf);
            Geraete = new SpielGeraete(Geraet, Spielstaende.StadtGeraete(verbindung, Lauf));
            lock (zustand)
            {
                gestoert = false;   // der Stand kommt jetzt wieder aus der Datenbank
            }
            return ereignisse;
        }
        catch (SqliteException)
        {
            return [];   // Python: _execute(default=[])
        }
    }

    /// <summary>Weisung P8-5 (L3): wer die Stadt-Zeilen geschrieben hat (Stand des letzten <see cref="Ereignisse"/>).</summary>
    public SpielGeraete? Geraete { get; private set; }

    private (string Wo, string? Wert) Bedingung() =>
        string.IsNullOrEmpty(Lauf) || Lauf == LernstandSchreiber.AlterLauf ? ("(lauf IS NULL OR lauf = '')", null) : ("lauf = $lauf", Lauf);

    /// <summary>game_event_stamp: COUNT, MAX(id), MAX(timestamp) des Durchgangs; null bei einem Lesefehler (Abfragen meldet ihn).</summary>
    public SpielStempel? Stempel()
    {
        lock (zustand)
        {
            if (zwischen is { } z)
            {
                return z.Stempel;
            }
        }
        return DatenbankStempel();
    }

    private SpielStempel? DatenbankStempel()
    {
        var (wo, wert) = Bedingung();
        const string Sql = "SELECT COUNT(*), MAX(id), MAX(timestamp) FROM spiel_ereignisse WHERE ";
        var zeilen = wert is null ? Lernstand.Abfragen(Sql + wo) : Lernstand.Abfragen(Sql + wo, ("$lauf", wert));
        return zeilen is [var z, ..] ? new SpielStempel((long)z[0]!, z[1] as long?, z[2]) : null;
    }

    /// <summary>Die Zeilen des Durchgangs nach der Nummer <paramref name="nummer"/> (leer bei einem Lesefehler).</summary>
    public List<SpielZeile> ZeilenNach(long nummer)
    {
        lock (zustand)
        {
            if (zwischen is { } z && nummer >= z.Basis)
            {
                return [.. z.Zeilen.Where(zeile => zeile.Nummer > nummer)];
            }
        }
        Leeren();
        var (wo, wert) = Bedingung();
        var sql = "SELECT id, timestamp, typ, daten FROM spiel_ereignisse WHERE " + wo + " AND id > $nr ORDER BY id";
        var zeilen = wert is null ? Lernstand.Abfragen(sql, ("$nr", nummer)) : Lernstand.Abfragen(sql, ("$lauf", wert), ("$nr", nummer));
        return [.. zeilen.Select(z => new SpielZeile((long)z[0]!, z[1], z[2] as string ?? "", z[3] as string))];
    }

    public bool BestenSchreiben(string lauf, string art, string schluessel, object? wert, string daten)
    {
        Pruefen();
        if (schlange is null)
        {
            return schreiber.BestenSpeichern(lauf, art, schluessel, wert, daten);
        }
        lock (zustand)
        {
            if (gestoert)
            {
                return false;
            }
            var z = zwischen ??= ZwischenAnlegen();
            var marke = schreiber.SpielMarkeJetzt(z.Uhrabweichung);
            // die Zeile gilt sofort in der Bestenliste des Spiels, genau wie sie geschrieben wird (ORDER BY timestamp, id)
            var zeile = Zeile(LernstandSchreiber.BestenZeit(marke, z.Loeschmarke).Zeit, lauf, art, schluessel, Gelesen(wert),
                string.IsNullOrEmpty(daten) ? "{}" : daten);
            var stelle = z.Besten.FindLastIndex(b => string.CompareOrdinal(b.Zeit, zeile.Zeit) <= 0) + 1;
            z.Besten.Insert(stelle, zeile);
            schlange.Anhaengen(new Spielschlange.Eintrag(this, "bestenliste", () => schreiber.BestenSpeichern(lauf, art, schluessel, wert, daten, marke)));
            return true;
        }
    }

    /// <summary>
    /// records: alle Zeilen der Bestenliste (ueber alle Durchgaenge), kaputte Daten = leeres dict. Weisung P7-17:
    /// solange Zeilen warten, die Bestenliste vom Beginn der Aktion mit den wartenden Zeilen.
    /// </summary>
    public List<BestenZeile> Bestenliste()
    {
        lock (zustand)
        {
            if (zwischen is { } z)
            {
                return [.. z.Besten];
            }
        }
        Leeren();
        return Bestenzeilen(Lernstand);
    }

    /// <summary>records() fuer eine Datenbank.</summary>
    public static List<BestenZeile> Bestenzeilen(Lernstand lernstand)
    {
        var zeilen = new List<BestenZeile>();
        foreach (var z in lernstand.Abfragen("SELECT timestamp, durchgang, art, schluessel, wert, daten FROM spiel_bestenliste ORDER BY timestamp, id"))
        {
            zeilen.Add(Zeile(z[0], z[1], z[2], z[3], z[4], z[5]));
        }
        return zeilen;
    }

    /// <summary>Eine Zeile der Bestenliste aus den Spalten (wie gelesen).</summary>
    private static BestenZeile Zeile(object? zeit, object? durchgang, object? art, object? schluessel, object? wert, object? datenText)
    {
        PyObjekt daten;
        try
        {
            daten = datenText is string { Length: > 0 } text ? Py.Lesen(text) as PyObjekt ?? [] : [];
        }
        catch (Exception fehler) when (fehler is FormatException or System.Text.Json.JsonException or InvalidOperationException)
        {
            daten = [];
        }
        return new BestenZeile(Convert.ToString(zeit, System.Globalization.CultureInfo.InvariantCulture) ?? "",
            durchgang as string ?? "", art as string ?? "", schluessel as string ?? "",
            wert switch { long n => n, double d => d, _ => null }, daten);
    }

    /// <summary>Der Wert so, wie SQLite ihn speichert und zurueckgibt (ganze Zahl, Kommazahl oder nichts).</summary>
    private static object? Gelesen(object? wert) => wert switch
    {
        long n => n,
        int n => (long)n,
        bool b => b ? 1L : 0L,
        double d => double.IsNaN(d) ? null : d,
        float f => float.IsNaN(f) ? null : (double)f,
        _ => null,
    };

    public bool BestenlisteLoeschen()
    {
        Pruefen();
        // Weisung P7-17: erst alles schreiben, dann sofort loeschen (die Marke gilt fuer alle spaeteren Zeilen)
        Leeren();
        return schreiber.BestenlisteLoeschen();
    }

    private sealed class Sitzungsmarke(SpielDatenbank speicher) : IDisposable
    {
        private int frei;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref frei, 1) == 0)
            {
                speicher.SitzungZu();
            }
        }
    }
}
