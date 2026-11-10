using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Sammelliste 277 (Weisung P12-164, Kurzplan P12-99 Punkt 1, Nico: „App reagiert nicht“ jedes Mal direkt nach „Tür setzen“).
/// Stadt 1, Kaufhaus waehlen, „Tür setzen“ und SOFORT auf die Wand tippen (die Fassade ist dann noch nicht fertig). Ein
/// Herzschlag alle 10 ms auf dem Bedienfaden misst den laengsten Stillstand vom Tipp bis die Tuer gesetzt ist: hoechstens
/// 400 ms, die Fassade wird nie auf dem Bedienfaden gerechnet, der Tipp setzt genau einen Schritt. Gegenprobe alter Weg
/// (<see cref="KartenFlaeche.TippSofortAltFuerTests"/>): die Fassade wird auf dem Bedienfaden gerechnet (rot).
/// </summary>
public static partial class StadtbauMessung
{
    /// <summary>Grenze fuer den Stillstand des Bedienfadens (wie die Messpunkte).</summary>
    private const double HerzschlagGrenze = 400;

    private static async Task TuerHerzschlagAsync(Hauptansicht a, Window? f)
    {
        await GroesseAsync(a, f, Groessen[1].B, Groessen[1].H);
        // Sammelliste 281: eine Auswahl im Grundriss (hier aus „Fund antippen“ im Innenraum) faellt beim Projektwechsel weg.
        // Gegenprobe alter Weg: sie bleibt stehen
        if (a.Flaeche.Auswahl is not null)
        {
            Hauptansicht.GrundrissAuswahlAltFuerTests = true;
            a.NeuAnlegen("Starttest Herzschlag Gegenprobe", null);
            Hauptansicht.GrundrissAuswahlAltFuerTests = false;
            if (a.Flaeche.Auswahl is null)
            {
                Fund("Projektwechsel Gegenprobe: alte Grundriss-Auswahl blieb nicht stehen");
            }
            Messwerte.Notiere("Projektwechsel: Auswahl im Grundriss vorher gesetzt, Gegenprobe bleibt stehen");
        }
        else
        {
            Fund("Projektwechsel: keine Grundriss-Auswahl aus dem Teil davor (Gegenprobe nicht moeglich)");
        }
        a.NeuAnlegen("Starttest Herzschlag", null);
        if (a.Flaeche.Auswahl is not null)
        {
            Fund("Projektwechsel: Auswahl im Grundriss blieb stehen (Sammelliste 281)");
        }
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        var p = a.Projekt!;
        var k = a.Karte;
        var kaufhaus = new AussenStelle(AussenArt.Haus, -1, 0, "kaufhaus");
        var (hx, hy, hw, hh) = AussenBearbeitung.HausFlaeche(p.AussenStand, kaufhaus);
        // Tipp vor die Nordwand (Mitte), mit Einrasten
        var (tx, ty) = (hx + (hw / 2), hy - 0.1);
        k.Raster = true;

        async Task<(double Stillstand, int AufBedienfaden, int Schritte)> LaufAsync()
        {
            var aufVorher = k.FassadeAufBedienfaden;
            var schritte = p.SchritteZurueck;
            var uhr = Stopwatch.StartNew();
            var letzter = 0.0;
            var groesster = 0.0;
            var takt = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Send, (_, _) =>
            {
                var jetzt = uhr.Elapsed.TotalMilliseconds;
                groesster = Math.Max(groesster, jetzt - letzter);
                letzter = jetzt;
            });
            takt.Start();
            try
            {
                k.Waehlen(kaufhaus);
                k.TuerSetzenStarten(kaufhaus, still: true);
                var beginn = uhr.Elapsed.TotalMilliseconds;
                FingerTipp(k, k.Bildpunkt(tx, ty));
                // der Tipp selbst zaehlt mit (alter Weg: er rechnet die Fassade hier)
                groesster = Math.Max(groesster, uhr.Elapsed.TotalMilliseconds - beginn);
                await Task.WhenAny(k.FassadeBereit, Task.Delay(60000));
                await BildAsync(a);
                groesster = Math.Max(groesster, uhr.Elapsed.TotalMilliseconds - letzter);
            }
            finally
            {
                takt.Stop();
            }
            return (groesster, k.FassadeAufBedienfaden - aufVorher, p.SchritteZurueck - schritte);
        }

        var (still, auf, schritt) = await LaufAsync();
        Messwerte.Notiere(string.Create(Inv, $"Tuer setzen Herzschlag: laengster Stillstand {still:F0} ms (Grenze {HerzschlagGrenze:F0}), Fassade auf dem Bedienfaden {auf}x"));
        if (still > HerzschlagGrenze)
        {
            Fund(string.Create(Inv, $"Tuer setzen sofort: Bedienfaden stand {still:F0} ms still (Grenze {HerzschlagGrenze:F0})"));
        }
        if (auf != 0)
        {
            Fund("Tuer setzen sofort: Fassade auf dem Bedienfaden gerechnet");
        }
        if (schritt != 1)
        {
            Fund(string.Create(Inv, $"Tuer setzen sofort: {schritt} Schritte statt 1 (wartender Tipp)"));
        }
        k.TuerSetzenBeenden();
        a.Rueckgaengig();
        await a.AussenGeprueft;

        // Gegenprobe: alter Weg rechnet die Fassade beim Tipp auf dem Bedienfaden
        KartenFlaeche.TippSofortAltFuerTests = true;
        try
        {
            k.Waehlen(null);
            var (stillAlt, aufAlt, _) = await LaufAsync();
            Messwerte.Notiere(string.Create(Inv, $"Tuer setzen Herzschlag Gegenprobe alter Weg: laengster Stillstand {stillAlt:F0} ms, Fassade auf dem Bedienfaden {aufAlt}x"));
            if (aufAlt == 0)
            {
                Fund("Tuer setzen sofort: Gegenprobe (alter Weg) rechnet die Fassade nicht auf dem Bedienfaden");
            }
        }
        finally
        {
            KartenFlaeche.TippSofortAltFuerTests = false;
        }
        k.TuerSetzenBeenden();
        a.Rueckgaengig();
        await a.AussenGeprueft;
        k.Waehlen(null);
    }
}
