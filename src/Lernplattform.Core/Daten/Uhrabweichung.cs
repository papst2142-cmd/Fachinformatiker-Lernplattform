using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Lernplattform.Core.Mathe;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Phase 2, L10: gemessene Abweichung der Geraeteuhr von der Uhrzeit von GitHub, wie die
/// Python-Linie v0.63.2 (fisi_core CLOCK_KEY, clock_measurement, clock_deviation,
/// clock_jump_unknown, _clock_jump). Nur der Abgleich misst und speichert sie (sync_meta
/// "uhr_abweichung"); LernstandSchreiber rechnet sie beim Stempeln ein (_stamp).
/// Zur Messung gehoeren eine Laufzeit-Uhr seit Systemstart (nicht stellbar, laeuft im
/// Ruhezustand weiter), die Kennung des Systemstarts (Linux, Android) und die des Prozesses:
/// Daran zeigt sich, ob die Uhr seit der Messung gestellt wurde. Tests geben alle drei vor.
/// </summary>
public sealed class Uhrabweichung(Func<double> laufzeit, string? systemstart, string prozess)
{
    public const string Schluessel = "uhr_abweichung";     // CLOCK_KEY
    public const int Toleranz = 300;                       // CLOCK_TOLERANCE
    public const int Gueltig = 24 * 3600;                  // CLOCK_VALID

    private static readonly string EigenerProzess = Guid.NewGuid().ToString("N");     // PROCESS_TOKEN
    private static readonly Lazy<string?> EigenerStart = new(StartLesen);

    /// <summary>Das laufende Programm: echte Laufzeit-Uhr, Systemstart, Prozess.</summary>
    public static Uhrabweichung System { get; } = new(Laufzeit, EigenerStart.Value, EigenerProzess);

    public Func<double> Laufzeituhr { get; } = laufzeit;

    public string? Systemstart { get; } = systemstart;

    public string Prozess { get; } = prozess;

    /// <summary>
    /// _boot_clock: Sekunden seit Systemstart. Linux/Android CLOCK_BOOTTIME, macOS
    /// CLOCK_MONOTONIC (laeuft dort im Ruhezustand weiter), sonst (Windows) GetTickCount64.
    /// </summary>
    public static double Laufzeit()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid() || OperatingSystem.IsMacOS())
        {
            try
            {
                var uhr = OperatingSystem.IsMacOS() ? 6 : 7;     // CLOCK_MONOTONIC (Darwin), CLOCK_BOOTTIME (Linux)
                if (clock_gettime(uhr, out var zeit) == 0)
                {
                    return zeit.Sekunden + zeit.Nanosekunden / 1e9;
                }
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
            }
        }
        return Environment.TickCount64 / 1000.0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Zeitwert
    {
        public long Sekunden;
        public long Nanosekunden;
    }

    [DllImport("libc", EntryPoint = "clock_gettime")]
    private static extern int clock_gettime(int uhr, out Zeitwert zeit);

    /// <summary>_boot_id: Kennung des Systemstarts (Linux, Android), sonst null.</summary>
    private static string? StartLesen()
    {
        try
        {
            var text = File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim();
            return text.Length > 0 ? text : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// clock_measurement: json.dumps({"s": round(s, 1), "gemessen": device_now().isoformat(),
    /// "lauf": round(_boot_clock(), 3), "start": _boot_id(), "prozess": PROCESS_TOKEN})
    /// mit Pythons Standard-Trennzeichen (", " und ": ").
    /// </summary>
    public string Messung(double sekunden, Uhr uhr)
    {
        var start = Systemstart is null ? "null" : JsonSerializer.Serialize(Systemstart);
        return "{\"s\": " + PythonJson.Zahl(Runde(sekunden, 1)) + ", \"gemessen\": \"" + Iso(uhr.Jetzt())
            + "\", \"lauf\": " + PythonJson.Zahl(Runde(Laufzeituhr(), 3)) + ", \"start\": " + start
            + ", \"prozess\": \"" + Prozess + "\"}";
    }

    /// <summary>round(x, n) wie Python: exakter Wert halb gerade gerundet, wieder als Gleitkommazahl.</summary>
    internal static double Runde(double x, int stellen)
    {
        if (!double.IsFinite(x))
        {
            return x;
        }
        var ganz = PythonRunden.Gerundet(x, stellen);
        var text = global::System.Numerics.BigInteger.Abs(ganz).ToString(CultureInfo.InvariantCulture).PadLeft(stellen + 1, '0');
        var zahl = double.Parse(text[..^stellen] + "." + text[^stellen..], CultureInfo.InvariantCulture);
        return ganz.Sign < 0 || (ganz.IsZero && double.IsNegative(x)) ? -zahl : zahl;
    }

    /// <summary>datetime.isoformat() eines Zeitpunkts mit Zone (Mikrosekunden nur, wenn nicht 0).</summary>
    public static string Iso(DateTimeOffset moment)
    {
        var mikro = (moment.Ticks % TimeSpan.TicksPerSecond) / 10;
        var zeit = moment.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
            + (mikro != 0 ? "." + mikro.ToString("000000", CultureInfo.InvariantCulture) : "");
        var abstand = moment.Offset;
        var zeichen = abstand < TimeSpan.Zero ? "-" : "+";
        abstand = abstand.Duration();
        return zeit + zeichen + abstand.Hours.ToString("00", CultureInfo.InvariantCulture) + ":"
            + abstand.Minutes.ToString("00", CultureInfo.InvariantCulture)
            + (abstand.Seconds != 0 ? ":" + abstand.Seconds.ToString("00", CultureInfo.InvariantCulture) : "");
    }

    private sealed record Gelesen(double Sekunden, DateTimeOffset Gemessen, JsonElement Daten);

    /// <summary>json.loads, float(data["s"]), fromisoformat(data["gemessen"]) mit Zone; sonst null.</summary>
    private static Gelesen? Lesen(object? wert)
    {
        if (wert is not string text)
        {
            return null;
        }
        try
        {
            using var dokument = JsonDocument.Parse(text);
            var daten = dokument.RootElement.Clone();
            if (daten.ValueKind != JsonValueKind.Object || !daten.TryGetProperty("s", out var s)
                || !daten.TryGetProperty("gemessen", out var g) || g.ValueKind != JsonValueKind.String)
            {
                return null;
            }
            if (Kommazahl(s) is not { } sekunden || Loeschmarken.IsoUtc(g.GetString()!) is not { } gemessen)
            {
                return null;
            }
            return new Gelesen(sekunden, gemessen, daten);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>float(x) fuer einen JSON-Wert (Zahl, Text mit Zahl, true/false); sonst null.</summary>
    private static double? Kommazahl(JsonElement wert) => wert.ValueKind switch
    {
        JsonValueKind.Number => double.TryParse(wert.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null,
        JsonValueKind.True => 1,
        JsonValueKind.False => 0,
        JsonValueKind.String => PythonFloat(wert.GetString()!),
        _ => null,
    };

    /// <summary>float("…") wie Python (Leerraum, inf, nan, Unterstriche zwischen Ziffern).</summary>
    private static double? PythonFloat(string text)
    {
        var t = text.Trim().Replace("_", "", StringComparison.Ordinal);
        if (t.Length == 0 || text.Contains("__", StringComparison.Ordinal) || text.Trim().StartsWith('_') || text.Trim().EndsWith('_'))
        {
            return null;
        }
        var klein = t.ToLowerInvariant().TrimStart('+', '-');
        var negativ = t.StartsWith('-');
        if (klein is "inf" or "infinity")
        {
            return negativ ? double.NegativeInfinity : double.PositiveInfinity;
        }
        if (klein == "nan")
        {
            return double.NaN;
        }
        if (!global::System.Text.RegularExpressions.Regex.IsMatch(t, @"^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$"))
        {
            return null;
        }
        return double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>_clock_jump: Sprung der Geraeteuhr seit der Messung gegenueber der Laufzeit-Uhr, oder null.</summary>
    private double? Sprung(JsonElement daten, double alter)
    {
        if (!daten.TryGetProperty("lauf", out var lauf) || Kommazahl(lauf) is not { } damals)
        {
            return null;
        }
        string? start = daten.TryGetProperty("start", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        var startIstNull = !daten.TryGetProperty("start", out var s2) || s2.ValueKind == JsonValueKind.Null;
        var prozessGleich = daten.TryGetProperty("prozess", out var p) && p.ValueKind == JsonValueKind.String && p.GetString() == Prozess;
        // Python: start is not None and start == _boot_id() (Werte anderer Art sind nie gleich)
        if (!(prozessGleich || (!startIstNull && start is not null && start == Systemstart)))
        {
            return null;
        }
        var vergangen = Laufzeituhr() - damals;
        if (vergangen < 0)
        {
            return null;
        }
        return alter - vergangen;
    }

    /// <summary>
    /// clock_deviation: gemessene Abweichung in Sekunden, oder null (fehlt, unlesbar, aelter als
    /// 24 h oder in der Zukunft). Wurde die Uhr seit der Messung gestellt und geht danach richtig,
    /// die kleine Abweichung danach.
    /// </summary>
    public double? Abweichung(object? wert, DateTimeOffset jetzt)
    {
        if (Lesen(wert) is not { } g)
        {
            return null;
        }
        var alter = (jetzt - g.Gemessen).TotalSeconds;
        if (alter < 0 || alter > Gueltig)
        {
            return null;
        }
        if (Math.Abs(g.Sekunden) > Toleranz)
        {
            var sprung = Sprung(g.Daten, alter);
            if (sprung is { } j && Math.Abs(j) > Toleranz && Math.Abs(g.Sekunden + j) <= Toleranz)
            {
                return g.Sekunden + j;
            }
        }
        return g.Sekunden;
    }

    /// <summary>clock_jump_unknown: Messung ueber der Toleranz, ein Sprung seither aber nicht feststellbar.</summary>
    public bool SprungUnbekannt(object? wert, DateTimeOffset jetzt)
    {
        if (Lesen(wert) is not { } g)
        {
            return false;
        }
        var alter = (jetzt - g.Gemessen).TotalSeconds;
        if (alter < 0 || alter > Gueltig)
        {
            return false;
        }
        return Math.Abs(g.Sekunden) > Toleranz && Sprung(g.Daten, alter) is null;
    }

    /// <summary>
    /// _last_measurement: (Abweichung, Geraeteuhr beim Messen in UTC ohne Zone) ohne
    /// Altersgrenze, oder null.
    /// </summary>
    public static (double Sekunden, DateTime Gemessen)? LetzteMessung(object? wert) =>
        Lesen(wert) is { } g ? (g.Sekunden, g.Gemessen.UtcDateTime) : null;

    /// <summary>_clock_jump fuer _correct_own_entries: Sprung seit der Messung, oder null.</summary>
    public double? SprungSeit(object? wert, double alter) => Lesen(wert) is { } g ? Sprung(g.Daten, alter) : null;
}
