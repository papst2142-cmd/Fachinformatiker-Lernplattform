using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Programm;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Sammelliste 438, Weisung P12-399 Antwort 1a): kurzer Teil <see cref="TeilKaltbau"/> nur fuer die
/// Messreihe (<c>.github/workflows/messreihe.yml</c>, nie im Merge-Tor). Ein frischer Prozess misst genau einmal: das erste
/// Oeffnen der Spielseite (der erste, kalte Bau der Stadt), das Laden der grossen Testkarte und den Messpunkt „Stadt bauen
/// Testkarte (geladen)“ wie im Teil <see cref="TeilNachladen"/> (<see cref="TestkarteBauenMessen"/>, dieselbe Grenze). Dazu
/// schreibt er, wie die Laufzeit eingestellt ist (TieredPGO, vorab uebersetzte Engine), damit jede Fassung der Messreihe belegt ist.
/// Aendert nichts am Programm und an keinem anderen Teil.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Wert von LERNPLATTFORM_MESSUNG_TEIL fuer diesen Teil.</summary>
    public const string TeilKaltbau = "kaltbau";

    private static async Task<int> KaltbauTeilAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Fehler(string text)
        {
            Messwerte.Notiere("  FEHLER: Kaltbau: " + text);
            funde++;
        }
        var engine = typeof(StadtSzene).Assembly;
        Messwerte.Notiere("Kaltbau: Laufzeit " + Environment.Version + ", TieredPGO "
            + (Environment.GetEnvironmentVariable("DOTNET_TieredPGO") is { Length: > 0 } pgo ? pgo : "Vorgabe")
            + ", TieredCompilation " + (Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") is { Length: > 0 } tc ? tc : "Vorgabe")
            + ", Engine " + Path.GetFileName(engine.Location));
        var uhr = Stopwatch.StartNew();
        var seite = await SpielSeiteAsync(ansicht);
        var oeffnen = uhr.Elapsed.TotalMilliseconds;
        if (seite?.Spiel is not { } vorher)
        {
            Fehler("Spielseite nicht gebaut");
            return funde;
        }
        Messwerte.Notiere(string.Create(inv, $"Kaltbau: Spielseite oeffnen (erster Bau der Stadt, einmalig): {oeffnen:F0} ms"));
        if (Nachladeprobe.Laden() is not { } datei)
        {
            Fehler("Testkarte nicht gefunden (" + Nachladeprobe.Umgebung + ")");
            return funde;
        }
        var stufeVorher = Grafikstufen.Lesen(Zustand.Werte);
        try
        {
            Zustand.Aendere(w => Grafikstufen.Wert(w, Grafikstufe.Mittel));
            seite.GrafikAnwenden();
            uhr.Restart();
            var spiel = Nachladeprobe.Spiel(datei, vorher);
            Messwerte.Notiere(string.Create(inv, $"Kaltbau: Testkarte geladen in {uhr.ElapsedMilliseconds} ms"));
            funde += TestkarteBauenMessen(spiel);
        }
        finally
        {
            if (stufeVorher is { } s)
            {
                Zustand.Aendere(w => Grafikstufen.Wert(w, s));
            }
        }
        return funde;
    }
}
