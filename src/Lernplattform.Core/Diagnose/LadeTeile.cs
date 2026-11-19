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
    private static long _dateiHaupt;
    private static long _jsonHaupt;
    private static int _hauptfaden = -1;

    /// <summary>
    /// Zaehler auf 0 (am Anfang von "Inhalte laden"). Welle A 0.97.0 (Weisung P12-312 Punkt 6, E1): der aufrufende Faden ist
    /// der Hauptfaden des Ladens; seine Zeit steht zusaetzlich einzeln in der Ausgabe (Hintergrund-Aufgaben zaehlen dort nicht).
    /// </summary>
    public static void Zuruecksetzen()
    {
        Interlocked.Exchange(ref _datei, 0);
        Interlocked.Exchange(ref _json, 0);
        Interlocked.Exchange(ref _dateiHaupt, 0);
        Interlocked.Exchange(ref _jsonHaupt, 0);
        Volatile.Write(ref _hauptfaden, Environment.CurrentManagedThreadId);
    }

    private static void Zaehlen(ref long alle, ref long haupt, long dauer)
    {
        Interlocked.Add(ref alle, dauer);
        if (Environment.CurrentManagedThreadId == Volatile.Read(ref _hauptfaden))
        {
            Interlocked.Add(ref haupt, dauer);
        }
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
            Zaehlen(ref _datei, ref _dateiHaupt, Stopwatch.GetTimestamp() - start);
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
            Zaehlen(ref _json, ref _jsonHaupt, Stopwatch.GetTimestamp() - start);
        }
    }

    public static double DateiMs => Stopwatch.GetElapsedTime(0, Interlocked.Read(ref _datei)).TotalMilliseconds;

    public static double JsonMs => Stopwatch.GetElapsedTime(0, Interlocked.Read(ref _json)).TotalMilliseconds;

    /// <summary>Welle A 0.97.0 (E1): "Datei lesen" nur im Hauptfaden des Ladens.</summary>
    public static double DateiHauptMs => Stopwatch.GetElapsedTime(0, Interlocked.Read(ref _dateiHaupt)).TotalMilliseconds;

    /// <summary>Welle A 0.97.0 (E1): "JSON lesen" nur im Hauptfaden des Ladens (ohne Warten auf einen freien Kern im Hintergrund).</summary>
    public static double JsonHauptMs => Stopwatch.GetElapsedTime(0, Interlocked.Read(ref _jsonHaupt)).TotalMilliseconds;

    /// <summary>
    /// Welle A 0.97.0 (Weisung P12-312 Punkt 6, E1): wie <see cref="Text(double, double, double)"/>, dazu die Zeit im Hauptfaden,
    /// z. B. "Teile: Datei lesen 12 ms, JSON lesen 140 ms, Sicht aufbauen 30 ms (Datei und JSON: Arbeitszeit aller Threads;
    /// davon im Hauptfaden: Datei lesen 3 ms, JSON lesen 60 ms)". Nur Ausgabe.
    /// </summary>
    public static string Text(double dateiMs, double jsonMs, double sichtMs, double dateiHauptMs, double jsonHauptMs) =>
        Text(dateiMs, jsonMs, sichtMs)[..^1] + string.Create(CultureInfo.InvariantCulture,
            $"; davon im Hauptfaden: Datei lesen {dateiHauptMs:F0} ms, JSON lesen {jsonHauptMs:F0} ms)");

    /// <summary>Zusatz fuer die Messzeile, z. B. "Teile: Datei lesen 12 ms, JSON lesen 140 ms, Sicht aufbauen 30 ms".</summary>
    public static string Text(double dateiMs, double jsonMs, double sichtMs) => string.Create(CultureInfo.InvariantCulture,
        $"Teile: Datei lesen {dateiMs:F0} ms, JSON lesen {jsonMs:F0} ms, Sicht aufbauen {sichtMs:F0} ms (Datei und JSON: Arbeitszeit aller Threads)");
}
