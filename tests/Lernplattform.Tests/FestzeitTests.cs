using System.Globalization;
using System.Text.RegularExpressions;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W6 (Weisung P8-12, Teilplan P8-11 Abschnitte 1 und 4, Nico 1a): Festzeiten nach dem Kalenderdatum.
/// Alle Faelle mit festen Daten ueber <see cref="Festzeit.Fuer"/>; kein Test liest die Uhr oder den Schalter.
/// </summary>
public class FestzeitTests
{
    [Theory]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2028, 4, 16)]
    [InlineData(2029, 4, 1)]
    [InlineData(2030, 4, 21)]
    [InlineData(2038, 4, 25)]
    [InlineData(2285, 3, 22)]
    [InlineData(2008, 3, 23)]
    public void OstersonntagWieTabelle(int jahr, int monat, int tag)
    {
        var ostern = new DateOnly(jahr, monat, tag);
        Assert.Equal(ostern, Festzeit.Ostersonntag(jahr));
        // Palmsonntag bis Ostermontag, die Tage davor und danach nicht
        Assert.Null(Festzeit.Fuer(ostern.AddDays(-8)));
        Assert.Equal(Festzeit.Ostern, Festzeit.Fuer(ostern.AddDays(-7)));
        Assert.Equal(Festzeit.Ostern, Festzeit.Fuer(ostern));
        Assert.Equal(Festzeit.Ostern, Festzeit.Fuer(ostern.AddDays(1)));
        Assert.Null(Festzeit.Fuer(ostern.AddDays(2)));
    }

    /// <summary>Je Festzeit und Jahr 2024 bis 2030 die vier Grenztage: Tag davor, erster, letzter, Tag danach.</summary>
    [Fact]
    public void GrenztageJeFestzeit()
    {
        for (var jahr = 2024; jahr <= 2030; jahr++)
        {
            Assert.Null(Festzeit.Fuer(new DateOnly(jahr, 10, 23)));
            Assert.Equal(Festzeit.Halloween, Festzeit.Fuer(new DateOnly(jahr, 10, 24)));
            Assert.Equal(Festzeit.Halloween, Festzeit.Fuer(new DateOnly(jahr, 10, 31)));
            Assert.Equal(Festzeit.Halloween, Festzeit.Fuer(new DateOnly(jahr, 11, 1)));
            Assert.Null(Festzeit.Fuer(new DateOnly(jahr, 11, 2)));

            Assert.Null(Festzeit.Fuer(new DateOnly(jahr, 11, 30)));
            Assert.Equal(Festzeit.Weihnachten, Festzeit.Fuer(new DateOnly(jahr, 12, 1)));
            Assert.Equal(Festzeit.Weihnachten, Festzeit.Fuer(new DateOnly(jahr, 12, 26)));

            Assert.Equal(Festzeit.Silvester, Festzeit.Fuer(new DateOnly(jahr, 12, 27)));
            Assert.Equal(Festzeit.Silvester, Festzeit.Fuer(new DateOnly(jahr, 12, 31)));
            Assert.Equal(Festzeit.Silvester, Festzeit.Fuer(new DateOnly(jahr, 1, 1)));
            Assert.Null(Festzeit.Fuer(new DateOnly(jahr, 1, 2)));

            Assert.Null(Festzeit.Fuer(new DateOnly(jahr, 6, 15)));   // Vorgabe des Pruefstands (lauf.sh)
        }
        // 27.10.2026 (Tag des Baus) ist Halloween
        Assert.Equal(Festzeit.Halloween, Festzeit.Fuer(new DateOnly(2026, 10, 27)));
    }

    [Fact]
    public void SchaltjahrOhneFestzeit()
    {
        foreach (var jahr in new[] { 2024, 2028, 2032, 2096, 2400 })
        {
            Assert.Null(Festzeit.Fuer(new DateOnly(jahr, 2, 29)));
            Assert.Null(Festzeit.Fuer(new DateOnly(jahr, 3, 1)));
        }
    }

    /// <summary>
    /// Eigenschaft fuer 1583 bis 4099: Ostersonntag ist ein Sonntag zwischen 22.03. und 25.04., Ostern ueberschneidet
    /// sich nie mit einer anderen Festzeit, und jeder Tag hat hoechstens eine Festzeit (9 + 9 + 26 + 6 Tage im Jahr).
    /// </summary>
    [Fact]
    public void EigenschaftenUeberAlleJahre()
    {
        for (var jahr = 1583; jahr <= 4099; jahr++)
        {
            var ostern = Festzeit.Ostersonntag(jahr);
            Assert.Equal(DayOfWeek.Sunday, ostern.DayOfWeek);
            Assert.InRange(ostern, new DateOnly(jahr, 3, 22), new DateOnly(jahr, 4, 25));
        }
        var tage = new Dictionary<string, int>();
        for (var tag = new DateOnly(2027, 1, 1); tag.Year == 2027; tag = tag.AddDays(1))
        {
            if (Festzeit.Fuer(tag) is { } fest)
            {
                tage[fest] = tage.GetValueOrDefault(fest) + 1;
            }
        }
        // 2027: Silvester 01.01. und 27.-31.12. (6 Tage in einem Kalenderjahr)
        Assert.Equal(new Dictionary<string, int> { [Festzeit.Ostern] = 9, [Festzeit.Halloween] = 9, [Festzeit.Weihnachten] = 26, [Festzeit.Silvester] = 6 }, tage);
    }

    /// <summary>
    /// Teilplan Abschnitt 4: im Spiel (Core und App) liest nur Festzeit.Heute das Datum des Geraets; dazu die bekannte
    /// bisherige Stelle (Zeitstempel der Spielzeilen in Spielablauf). Jede neue Stelle macht diesen Test rot.
    /// </summary>
    [Fact]
    public void NurEineStelleLiestDasGeraetedatum()
    {
        var erlaubt = new Dictionary<string, int>
        {
            ["src/Lernplattform.Core/Spiel/Festzeit.cs"] = 1,
            ["src/Lernplattform.Core/Spiel/Spielablauf.cs"] = 1,
        };
        var muster = new Regex(@"\b(DateTime|DateTimeOffset|DateOnly)\s*\.\s*(Now|Today|UtcNow)\b|TimeProvider\.System", RegexOptions.CultureInvariant);
        var gefunden = new Dictionary<string, int>();
        foreach (var ordner in new[] { "src/Lernplattform.Core/Spiel", "src/Lernplattform.App/Spiel" })
        {
            foreach (var datei in Directory.EnumerateFiles(Path.Combine(Pfade.Wurzel, ordner), "*.cs", SearchOption.AllDirectories))
            {
                var anzahl = muster.Matches(File.ReadAllText(datei)).Count;
                if (anzahl > 0)
                {
                    gefunden[Path.GetRelativePath(Pfade.Wurzel, datei).Replace('\\', '/')] = anzahl;
                }
            }
        }
        Assert.Equal(erlaubt.OrderBy(p => p.Key, StringComparer.Ordinal), gefunden.OrderBy(p => p.Key, StringComparer.Ordinal));
        // die Stelle in Festzeit.cs ist Datum(); Heute() (Festzeit) und Jahreszeiten.Heute() lesen nur sie (Weisung P8-24)
        var text = File.ReadAllText(Path.Combine(Pfade.Wurzel, "src/Lernplattform.Core/Spiel/Festzeit.cs"));
        Assert.Matches(@"public static DateOnly Datum\(\) => SchalterDatum\(\) \?\? DateOnly\.FromDateTime\(DateTime\.Now\);", text);
        Assert.Matches(@"public static string\? Heute\(\) =>\s+_probeAktiv \? _probe : Fuer\(Datum\(\)\);", text);
        var jahreszeit = File.ReadAllText(Path.Combine(Pfade.Wurzel, "src/Lernplattform.Core/Spiel/Jahreszeit.cs"));
        Assert.Matches(@"public static Jahreszeit Heute\(\) =>\s+_probe \?\? Fuer\(Festzeit\.Datum\(\)\);", jahreszeit);
    }

    /// <summary>Weisung P8-24: Jahreszeit meteorologisch je Grenztag, auch 29. Februar.</summary>
    [Theory]
    [InlineData("2028-02-28", Jahreszeit.Winter)]
    [InlineData("2028-02-29", Jahreszeit.Winter)]
    [InlineData("2028-03-01", Jahreszeit.Fruehling)]
    [InlineData("2027-02-28", Jahreszeit.Winter)]
    [InlineData("2027-03-01", Jahreszeit.Fruehling)]
    [InlineData("2026-05-31", Jahreszeit.Fruehling)]
    [InlineData("2026-06-01", Jahreszeit.Sommer)]
    [InlineData("2026-06-15", Jahreszeit.Sommer)]
    [InlineData("2026-08-31", Jahreszeit.Sommer)]
    [InlineData("2026-09-01", Jahreszeit.Herbst)]
    [InlineData("2026-11-30", Jahreszeit.Herbst)]
    [InlineData("2026-12-01", Jahreszeit.Winter)]
    [InlineData("2026-12-31", Jahreszeit.Winter)]
    [InlineData("2027-01-01", Jahreszeit.Winter)]
    public void JahreszeitJeGrenztag(string tag, Jahreszeit soll)
    {
        Assert.Equal(soll, Jahreszeiten.Fuer(DateOnly.ParseExact(tag, "yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void JahreszeitProbeUndDatum()
    {
        try
        {
            foreach (var zeit in Enum.GetValues<Jahreszeit>())
            {
                Jahreszeiten.Probe(zeit);
                Assert.Equal(zeit, Jahreszeiten.Heute());
            }
        }
        finally
        {
            Jahreszeiten.ProbeEnde();
        }
        Assert.Equal(Jahreszeiten.Fuer(Festzeit.Datum()), Jahreszeiten.Heute());
    }

    [Fact]
    public void UnbekannteProbeAbgelehnt()
    {
        Assert.Throws<ArgumentException>(() => Festzeit.Probe("fasching"));
    }
}
