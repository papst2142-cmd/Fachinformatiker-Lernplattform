using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 0, Spike A: sammelt Messwerte (Start, erstes Oeffnen einer Seite,
/// Farbwechsel, Arbeitsspeicher) und liefert sie als Text zum Kopieren.
/// Nur fuer Phase 0; wird mit dem Phase-0-Bericht wieder entfernt.
/// </summary>
public static class Messwerte
{
    public static readonly Stopwatch SeitStart = Stopwatch.StartNew();
    private static readonly List<string> Zeilen = [];
    private static readonly Dictionary<string, List<double>> Reihen = new(StringComparer.Ordinal);

    public static double? ErstesBild { get; set; }
    public static Dictionary<string, double> ErstesOeffnen { get; } = new(StringComparer.Ordinal);

    public static void Notiere(string text) => Zeilen.Add(text);

    public static void Reihe(string name, double ms)
    {
        if (!Reihen.TryGetValue(name, out var liste))
        {
            Reihen[name] = liste = [];
        }
        liste.Add(ms);
    }

    public static string Bericht(string geraet = "")
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        using var p = Process.GetCurrentProcess();
        p.Refresh();
        sb.AppendLine("MESSUNG SPIKE A (Phase 0) - bitte komplett kopieren und senden");
        sb.AppendLine(string.Create(inv, $"Programm: A {typeof(Messwerte).Assembly.GetName().Version?.ToString(3)}"));
        sb.AppendLine("System: " + RuntimeInformation.OSDescription + " / " + RuntimeInformation.OSArchitecture
            + " / .NET " + Environment.Version + " / Kerne " + Environment.ProcessorCount);
        if (geraet.Length > 0)
        {
            sb.AppendLine("Anzeige: " + geraet);
        }
        sb.AppendLine(string.Create(inv, $"Zeitpunkt (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"));
        sb.AppendLine(string.Create(inv, $"Start bis erstes Bild: {(ErstesBild is double e ? e.ToString("F0", inv) + " ms" : "nicht gemessen")}"));
        foreach (var (seite, ms) in ErstesOeffnen)
        {
            sb.AppendLine(string.Create(inv, $"Erstes Oeffnen '{seite}': {ms:F0} ms"));
        }
        foreach (var (name, werte) in Reihen)
        {
            var sortiert = werte.Order().ToList();
            double mittel = werte.Average(), median = sortiert[sortiert.Count / 2], max = sortiert[^1];
            sb.AppendLine(string.Create(inv,
                $"{name}: {werte.Count}x, Mittel {mittel:F0} ms, Median {median:F0} ms, Hoechstwert {max:F0} ms"));
        }
        // macOS liefert keinen Hoechstwert (0), dann "nicht lesbar" statt 0 MB.
        var spitze = p.PeakWorkingSet64 > 0 ? string.Create(inv, $"{p.PeakWorkingSet64 / 1048576.0:F0} MB") : "nicht lesbar";
        sb.AppendLine(string.Create(inv,
            $"Arbeitsspeicher: {p.WorkingSet64 / 1048576.0:F0} MB (Hoechstwert {spitze}), verwalteter Speicher {GC.GetTotalMemory(false) / 1048576.0:F0} MB"));
        foreach (var z in Zeilen)
        {
            sb.AppendLine(z);
        }
        sb.AppendLine("Grenze: 400 ms (Abbruch ab 1000 ms)");
        return sb.ToString();
    }
}
