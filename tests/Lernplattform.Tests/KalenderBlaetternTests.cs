using System.Diagnostics;
using System.Security.Cryptography;
using Lernplattform.Core.Dashboard;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.12.2 (Weisung P6-16, Frage 3 A): Blaettern im Kalender "Lerntage" wie CalendarPanel._shift am
/// Handy: Monate ueber Jahresgrenzen, nur lesend (Datei bytegleich), je Monat unter 400 ms.
/// </summary>
public class KalenderBlaetternTests
{
    [Theory]
    [InlineData(2026, 10, 1, 2026, 11)]
    [InlineData(2026, 10, -1, 2026, 9)]
    [InlineData(2026, 12, 1, 2027, 1)]
    [InlineData(2026, 1, -1, 2025, 12)]
    [InlineData(2026, 10, 0, 2026, 10)]
    [InlineData(2026, 10, -14, 2025, 8)]
    [InlineData(2026, 10, 27, 2029, 1)]
    [InlineData(2026, 1, -13, 2024, 12)]
    public void VerschiebenWiePython(int jahr, int monat, int schritt, int sollJahr, int sollMonat)
    {
        Assert.Equal((sollJahr, sollMonat), KalenderBlaettern.Verschieben(jahr, monat, schritt));
    }

    [Fact]
    public void BlaetternLiestNurUndIstSchnell()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, "lernplattform.db");
        File.Copy(Pfade.Vergleich("testdaten.db"), pfad);
        var vorher = SHA256.HashData(File.ReadAllBytes(pfad));
        var (jahr, monat) = (2026, 10);
        var hin = new List<DashboardKalender>();
        // 13 Monate zurueck und wieder vor: gleiche Lerntage, je Schritt unter 400 ms (Grenze fuer Ansichtswechsel)
        for (var i = 0; i < 13; i++)
        {
            (jahr, monat) = KalenderBlaettern.Verschieben(jahr, monat, -1);
            var uhr = Stopwatch.StartNew();
            var kalender = DashboardWerte.Monat(pfad, jahr, monat);
            Assert.True(uhr.Elapsed.TotalMilliseconds < 400, $"{jahr}-{monat}: {uhr.Elapsed.TotalMilliseconds:F0} ms");
            Assert.Equal((jahr, monat), (kalender.Jahr, kalender.Monat));
            hin.Add(kalender);
        }
        for (var i = hin.Count - 2; i >= 0; i--)
        {
            (jahr, monat) = KalenderBlaettern.Verschieben(jahr, monat, 1);
            var kalender = DashboardWerte.Monat(pfad, jahr, monat);
            Assert.Equal(hin[i].Titel, kalender.Titel);
            Assert.Equal(hin[i].Lerntage, kalender.Lerntage);
        }
        Assert.Equal(vorher, SHA256.HashData(File.ReadAllBytes(pfad)));
        Assert.False(File.Exists(pfad + "-wal"));
    }
}
