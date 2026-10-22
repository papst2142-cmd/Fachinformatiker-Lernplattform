using System.Globalization;

namespace Lernplattform.Core.Diagnose;

/// <summary>
/// Weisung P4-12 B6 (Nico 22.10.2026): Ueberschreitet eine Messung im Start ohne Bedienung ihre
/// Grenze, wird dieselbe Handlung bis zu <see cref="Hoechstens"/> Mal gemessen (vor jeder
/// Wiederholung der Zustand davor und eine kurze Pause, kein Vorwaermen, keine zwischengespeicherte
/// Ansicht). Rot nur, wenn ALLE Messungen ueber der Grenze liegen. Die Grenzen selbst bleiben
/// unveraendert (150 ms Farbregler Schritt 1, 100 ms ab Schritt 2, 400 ms Ansicht, Abbruch ab
/// 1 000 ms). Nur fuer Messpunkte, die sich ohne Zustandsaenderung wiederholen lassen; einmalige
/// Messpunkte (erstes Oeffnen, erstes Bauen) bleiben streng (<see cref="Einmal"/>).
/// </summary>
public static class Messwiederholung
{
    /// <summary>Hoechstens so viele Messungen derselben Handlung (die erste mitgezaehlt).</summary>
    public const int Hoechstens = 3;

    /// <summary>Kurze Pause vor jeder Wiederholung.</summary>
    public static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(200);

    /// <summary>Ueber der Grenze? Eine nicht fertige Messung (null) zaehlt als ueber der Grenze.</summary>
    public static bool Ueber(double? ms, double grenze) => ms is not double wert || double.IsNaN(wert) || wert > grenze;

    /// <summary>Einmaliger Messpunkt: wird nie wiederholt, rot sobald die eine Messung ueber der Grenze liegt.</summary>
    public static Messreihe Einmal(double? ms, double grenze) => new([ms], grenze, false);

    /// <summary>
    /// Wiederholbarer Messpunkt: <paramref name="erste"/> ist die schon gemessene erste Messung.
    /// Solange die letzte Messung ueber der Grenze liegt und weniger als <see cref="Hoechstens"/>
    /// Messungen vorliegen: <paramref name="zurueck"/> (Zustand vor der Handlung, nicht gemessen),
    /// Pause, dieselbe Handlung neu messen (<paramref name="nochmal"/>).
    /// </summary>
    public static async Task<Messreihe> MessenAsync(double? erste, double grenze, Func<Task<double?>> nochmal,
        Func<Task>? zurueck, Func<TimeSpan, Task> warten)
    {
        var werte = new List<double?> { erste };
        while (werte.Count < Hoechstens && Ueber(werte[^1], grenze))
        {
            if (zurueck is not null)
            {
                await zurueck();
            }
            await warten(Pause);
            werte.Add(await nochmal());
        }
        return new Messreihe(werte, grenze, true);
    }
}

/// <summary>Alle Einzelmessungen eines Messpunkts mit seiner Grenze.</summary>
public sealed record Messreihe(IReadOnlyList<double?> Werte, double Grenze, bool Wiederholbar)
{
    /// <summary>Rot nur, wenn alle Messungen ueber der Grenze liegen.</summary>
    public bool Rot => Werte.All(w => Messwiederholung.Ueber(w, Grenze));

    /// <summary>Kleinster fertiger Wert (gilt als Messwert des Punkts); null, wenn keine Messung fertig wurde.</summary>
    public double? Bester => Werte.Min();

    /// <summary>Wurde wiederholt (mehr als eine Messung)?</summary>
    public bool Wiederholt => Werte.Count > 1;

    /// <summary>Einzelmessungen, z. B. "1119 ms / 412 ms".</summary>
    public string Einzelwerte(string format = "F0") => string.Join(" / ", Werte.Select(w => w is double wert
        ? wert.ToString(format, CultureInfo.InvariantCulture) + " ms"
        : "nicht fertig"));

    /// <summary>Fehlerzeile fuer die Messdatei: Messpunkt, Wert, Grenze und alle Einzelmessungen (Weisung P4-12 B5).</summary>
    public string Fehlertext(string messpunkt, string format = "F0")
    {
        var inv = CultureInfo.InvariantCulture;
        var wert = Bester is double b ? b.ToString(format, inv) + " ms" : "nicht fertig";
        var art = Wiederholbar
            ? string.Create(inv, $"{Werte.Count} Messungen")
            : "einmaliger Messpunkt, nicht wiederholt";
        return string.Create(inv, $"{messpunkt}: {wert} ueber Grenze {Grenze:F0} ms; Einzelmessungen {Einzelwerte(format)} ({art})");
    }
}
