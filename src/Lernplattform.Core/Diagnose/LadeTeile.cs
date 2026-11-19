using System.Diagnostics;
using System.Globalization;

namespace Lernplattform.Core.Diagnose;

/// <summary>
/// A 0.94.0 (Weisung P12-304 Punkt 3, Vorschlag aus Meldung P12-185): Teilzeiten des Messpunkts "Inhalte laden", nur
/// Ausgabe (Grenze 400 ms und Wertung bleiben unveraendert). Gezaehlt wird die Arbeitszeit aller Threads: "Datei lesen"
/// (Bytes der Inhaltsdateien holen) und "JSON lesen" (Bytes in einen JSON-Baum wandeln) laufen zum Teil im Hintergrund
/// gleichzeitig mit dem Rest; ihre Summe kann darum groesser sein als die Gesamtzeit. "Sicht aufbauen" misst der Aufrufer.
/// </summary>
public static class LadeTeile
{
    private static long _datei;
    private static long _json;

    /// <summary>Zaehler auf 0 (am Anfang von "Inhalte laden").</summary>
    public static void Zuruecksetzen()
    {
        Interlocked.Exchange(ref _datei, 0);
        Interlocked.Exchange(ref _json, 0);
    }

    /// <summary>Datei lesen und die Zeit zu "Datei lesen" zaehlen.</summary>
    public static T Datei<T>(Func<T> lesen)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            return lesen();
        }
        finally
        {
            Interlocked.Add(ref _datei, Stopwatch.GetTimestamp() - start);
        }
    }

    /// <summary>JSON lesen und die Zeit zu "JSON lesen" zaehlen.</summary>
    public static T Json<T>(Func<T> lesen)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            return lesen();
        }
        finally
        {
            Interlocked.Add(ref _json, Stopwatch.GetTimestamp() - start);
        }
    }

    public static double DateiMs => Stopwatch.GetElapsedTime(0, Interlocked.Read(ref _datei)).TotalMilliseconds;

    public static double JsonMs => Stopwatch.GetElapsedTime(0, Interlocked.Read(ref _json)).TotalMilliseconds;

    /// <summary>Zusatz fuer die Messzeile, z. B. "Teile: Datei lesen 12 ms, JSON lesen 140 ms, Sicht aufbauen 30 ms".</summary>
    public static string Text(double dateiMs, double jsonMs, double sichtMs) => string.Create(CultureInfo.InvariantCulture,
        $"Teile: Datei lesen {dateiMs:F0} ms, JSON lesen {jsonMs:F0} ms, Sicht aufbauen {sichtMs:F0} ms (Datei und JSON: Arbeitszeit aller Threads)");
}
