using System.Diagnostics;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// A 0.12.2 (Weisung P6-16, Frage 3 A): Blaettern im Kalender "Lerntage" der Startseite, je Schritt bis
/// zum fertigen Bild. Grenze 400 ms (Ansichtswechsel); ueber der Grenze wird derselbe Schritt nach dem
/// Zurueckblaettern bis zu 3 Mal gemessen. Liest nur, speichert nichts.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> KalenderMesspunkteAsync(MainView ansicht)
    {
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        if (ansicht.GebauteSeiten[Seiten.Dashboard] is not DashboardSeite { Kalender: { } kalender })
        {
            Messwerte.Notiere("  FEHLER: Startseite ohne Kalender \"Lerntage\"");
            return 1;
        }
        var funde = 0;
        foreach (var (titel, schritt) in new[] { ("Kalender Lerntage: Vormonat", -1), ("Kalender Lerntage: Folgemonat", 1) })
        {
            async Task<double?> Einmal()
            {
                var uhr = Stopwatch.StartNew();
                await kalender.BlaetternAsync(schritt);
                await ansicht.BildFertigAsync();
                return uhr.Elapsed.TotalMilliseconds;
            }
            var erste = await Einmal();
            var reihe = await Core.Diagnose.Messwiederholung.MessenAsync(erste, Geraetegrenze, Einmal,
                async () => { await kalender.BlaetternAsync(-schritt); await ansicht.BildFertigAsync(); }, Task.Delay);
            funde += Ergebnis(titel, reihe);
        }
        return funde;
    }
}
