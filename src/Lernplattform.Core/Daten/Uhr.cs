using System.Globalization;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Uhr und Zeitzone des Geraets (fisi_core.device_now). Tests geben einen festen
/// Zeitpunkt und eine feste Zone vor; das Programm nutzt Systemuhr und Ortszeit.
/// </summary>
public sealed class Uhr(Func<DateTimeOffset> utcJetzt, TimeZoneInfo zone)
{
    /// <summary>Systemuhr in der Zeitzone des Geraets.</summary>
    public static Uhr System => new(() => DateTimeOffset.UtcNow, TimeZoneInfo.Local);

    public TimeZoneInfo Zone { get; } = zone;

    /// <summary>device_now: jetzt als Ortszeit mit Abstand zu UTC (ueber UTC ermittelt, auch in der doppelten Stunde richtig).</summary>
    public DateTimeOffset Jetzt() => Ortszeit(utcJetzt());

    /// <summary>astimezone(): ein Zeitpunkt als Ortszeit dieses Geraets.</summary>
    public DateTimeOffset Ortszeit(DateTimeOffset moment) => TimeZoneInfo.ConvertTime(moment, Zone);

    /// <summary>offset_minutes: Abstand zu UTC in ganzen Minuten (abgerundet wie Python //).</summary>
    public static int AbstandMinuten(DateTimeOffset moment) => (int)Math.Floor(moment.Offset.TotalSeconds / 60);

    /// <summary>strftime("%Y-%m-%d %H:%M:%S") der Ortszeit (Sekundenbruchteile fallen weg).</summary>
    public static string Text(DateTimeOffset moment) => moment.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>format_with_zone: z. B. 2026-10-25 02:30:00+02:00 (immer +HH:MM oder -HH:MM, nie "Z").</summary>
    public static string MitZone(DateTimeOffset moment)
    {
        var minuten = AbstandMinuten(moment);
        var betrag = Math.Abs(minuten);
        return Text(moment) + (minuten < 0 ? "-" : "+")
            + (betrag / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (betrag % 60).ToString("00", CultureInfo.InvariantCulture);
    }
}
