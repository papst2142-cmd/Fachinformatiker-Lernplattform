using System.Globalization;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 8 W6 (Weisung P8-12, Teilplan P8-11, Nico 1a): Festzeiten nach dem Kalenderdatum des Geraets.
/// Nur Darstellung (Deko in Stadt und Innenraeumen, Festkleidung im Kaufhaus), keine Spielregel und kein
/// Ereignis. Zeitraeume (je einschliesslich): Halloween 24.10.-01.11., Weihnachten 01.12.-26.12., Silvester
/// 27.12.-01.01., Ostern Palmsonntag (Ostersonntag - 7) bis Ostermontag (Ostersonntag + 1); Ostersonntag nach
/// der Anonymen Gregorianischen Formel (Meeus/Jones/Butcher). Die Zeitraeume stehen zum Nachlesen auch in
/// <c>inhalte_csharp/spiel/festzeiten.json</c> ("zeitraeume"); die Ladepruefung verlangt, dass sie gleich
/// <see cref="Zeitraeume"/> sind.
/// <para>
/// <see cref="Heute"/> ist im Spiel die einzige Stelle, die das Datum des Geraets liest (fester Test
/// durchsucht den Quelltext). Alle Aufbau-Funktionen bekommen die Festzeit als Wert.
/// </para>
/// </summary>
public static class Festzeit
{
    public const string Halloween = "halloween";
    public const string Weihnachten = "weihnachten";
    public const string Silvester = "silvester";
    public const string Ostern = "ostern";

    /// <summary>Alle Festzeiten in der Reihenfolge des Jahres ab Ostern.</summary>
    public static readonly IReadOnlyList<string> Alle = [Ostern, Halloween, Weihnachten, Silvester];

    /// <summary>Schalter fuer den Pruefstand: festes Datum JJJJ-MM-TT statt des Geraetedatums (ungueltig = Fehler).</summary>
    public const string Schalter = "LERNPLATTFORM_DATUM";

    /// <summary>Ein fester Zeitraum (Monat, Tag) bis (Monat, Tag), einschliesslich; ueber den Jahreswechsel, wenn bis vor von liegt.</summary>
    public sealed record Zeitraum(string Fest, int VonMonat, int VonTag, int BisMonat, int BisTag);

    /// <summary>Die festen Zeitraeume (Ostern haengt am Ostersonntag: <see cref="OsternVon"/> bis <see cref="OsternBis"/>).</summary>
    public static readonly IReadOnlyList<Zeitraum> Zeitraeume =
    [
        new(Halloween, 10, 24, 11, 1),
        new(Weihnachten, 12, 1, 12, 26),
        new(Silvester, 12, 27, 1, 1),
    ];

    /// <summary>Ostern beginnt am Palmsonntag (Tage relativ zum Ostersonntag).</summary>
    public const int OsternVon = -7;

    /// <summary>Ostern endet am Ostermontag (Tage relativ zum Ostersonntag).</summary>
    public const int OsternBis = 1;

    /// <summary>Ostersonntag im gregorianischen Kalender (Anonyme Gregorianische Formel, ganze Zahlen).</summary>
    public static DateOnly Ostersonntag(int jahr)
    {
        int a = jahr % 19, b = jahr / 100, c = jahr % 100, d = b / 4, e = b % 4;
        int f = (b + 8) / 25, g = (b - f + 1) / 3, h = ((19 * a) + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4, l = (32 + (2 * e) + (2 * i) - h - k) % 7, m = (a + (11 * h) + (22 * l)) / 451;
        int monat = (h + l - (7 * m) + 114) / 31, tag = ((h + l - (7 * m) + 114) % 31) + 1;
        return new DateOnly(jahr, monat, tag);
    }

    /// <summary>Die Festzeit an einem Tag, null = keine.</summary>
    public static string? Fuer(DateOnly tag)
    {
        var ostern = Ostersonntag(tag.Year);
        if (tag >= ostern.AddDays(OsternVon) && tag <= ostern.AddDays(OsternBis))
        {
            return Ostern;
        }
        var heute = (tag.Month * 100) + tag.Day;
        foreach (var z in Zeitraeume)
        {
            int von = (z.VonMonat * 100) + z.VonTag, bis = (z.BisMonat * 100) + z.BisTag;
            if (von <= bis ? heute >= von && heute <= bis : heute >= von || heute <= bis)
            {
                return z.Fest;
            }
        }
        return null;
    }

    private static bool _probeAktiv;
    private static string? _probe;

    /// <summary>
    /// Start-Test: die Festzeit fest setzen (null = keine), unabhaengig von Datum und Schalter, bis
    /// <see cref="ProbeEnde"/>. Nur der Pruefstand ruft das.
    /// </summary>
    public static void Probe(string? fest)
    {
        if (fest is not null && !Alle.Contains(fest))
        {
            throw new ArgumentException("unbekannte Festzeit: " + fest, nameof(fest));
        }
        _probe = fest;
        _probeAktiv = true;
    }

    /// <summary>Start-Test: wieder nach Schalter oder Datum.</summary>
    public static void ProbeEnde()
    {
        _probe = null;
        _probeAktiv = false;
    }

    /// <summary>
    /// Das Datum aus dem Schalter <see cref="Schalter"/>; null = nicht gesetzt. Ein gesetzter, aber
    /// ungueltiger Wert ist ein Fehler (kein stilles Geraetedatum).
    /// </summary>
    public static DateOnly? SchalterDatum()
    {
        var wert = Environment.GetEnvironmentVariable(Schalter);
        if (string.IsNullOrEmpty(wert))
        {
            return null;
        }
        return DateOnly.TryParseExact(wert, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tag)
            ? tag
            : throw new InvalidOperationException(Schalter + " ungueltig (erwartet JJJJ-MM-TT): " + wert);
    }

    /// <summary>
    /// Das Datum jetzt: Schalter (Pruefstand), sonst das lokale Datum des Geraets. Die einzige Stelle im Spiel,
    /// die das Geraetedatum liest (auch fuer die Jahreszeit, Weisung P8-24).
    /// </summary>
    public static DateOnly Datum() => SchalterDatum() ?? DateOnly.FromDateTime(DateTime.Now);

    /// <summary>Festzeit jetzt: Probe (Start-Test), sonst nach <see cref="Datum"/>.</summary>
    public static string? Heute() =>
        _probeAktiv ? _probe : Fuer(Datum());
}
