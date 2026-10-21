using System.Globalization;

namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3 W4b, V1: Bild und Video (Plan P3-15 Abschnitt 4.3, Weisung P3-20). Erweitert die
/// Bildschirm-Rechnung um Dauer und Kompression; <see cref="Bildschirm"/> bleibt unveraendert
/// (G26). Kein Python-Vorbild, Vergleichswerte G29 aus dem unabhaengigen Skript. Feste
/// Reihenfolge: byte_s = byte_bild * fps, unkomp = byte_s * minuten * 60, komp = unkomp / y,
/// rate_komp = byte_s / y. Nur Rechnung und Zahltext, keine Saetze.
/// </summary>
public static class Video
{
    /// <summary>Hoechste Videodauer in Minuten (Vorschlag Wortliste W4b).</summary>
    public const double HoechstMinuten = 10000;

    /// <summary>Hoechste Kompression (y-fach), Plan 4.3.</summary>
    public const double HoechstKompression = 1000;

    /// <summary>Unter dieser Menge in MiB wird in Byte uebernommen (aufgerundet), nie 0.</summary>
    public const double UebernahmeGrenzeMiB = 0.0005;

    private const double MiB = 1024 * 1024;
    private const double GiB = 1024.0 * 1024 * 1024;

    /// <summary>
    /// Rechnet das Video oder liefert das erste ungueltige Feld (Minuten, Bildrate, Kompression).
    /// Breite, Hoehe, Farbtiefe und Bildrate sind schon nach den Bildschirm-Regeln geprueft.
    /// </summary>
    public static VideoErgebnis Rechnen(long breite, long hoehe, int tiefe, double fps, double minuten, double kompression)
    {
        if (!(minuten > 0 && minuten <= HoechstMinuten))
        {
            return VideoErgebnis.MitFehler(VideoFehler.Minuten);
        }
        if (fps == 0)
        {
            return VideoErgebnis.MitFehler(VideoFehler.Bildrate);
        }
        if (!(kompression >= 1 && kompression <= HoechstKompression))
        {
            return VideoErgebnis.MitFehler(VideoFehler.Kompression);
        }
        // hoechstens 100.000 * 100.000 * 64 = 6,4 * 10^11 und damit als Gleitkommazahl exakt
        var byteBild = (double)(breite * hoehe * tiefe) / 8;
        var byteS = byteBild * fps;
        var unkomp = byteS * minuten * 60;
        var rateKomp = byteS / kompression;
        return new VideoErgebnis(VideoFehler.Keiner, byteBild, byteS, minuten * 60, unkomp, unkomp / kompression,
            byteS * 8 / 1_000_000, rateKomp * 8 / 1_000_000, rateKomp / MiB);
    }

    /// <summary>
    /// Uebernahme in die Uebertragungsdauer: MiB auf 3 Nachkommastellen (kaufmaennisch, ohne
    /// Tausenderpunkte, ohne Nullen am Ende), unter 0,0005 MiB ganze Byte aufgerundet.
    /// </summary>
    public static (string Text, string Einheit) Uebernahme(double kompByte)
    {
        var mib = kompByte / MiB;
        if (mib < UebernahmeGrenzeMiB)
        {
            return (Math.Ceiling(kompByte).ToString("0", CultureInfo.InvariantCulture), "Byte");
        }
        return (Zahlen.DeKurz(mib, 3).Replace(".", "", StringComparison.Ordinal), "MiB");
    }

    /// <summary>Anzeigegroessen: Byte in MiB bzw. GiB.</summary>
    public static double InMiB(double bytes) => bytes / MiB;

    /// <inheritdoc cref="InMiB"/>
    public static double InGiB(double bytes) => bytes / GiB;
}

/// <summary>Erstes ungueltiges Feld von Bild und Video.</summary>
public enum VideoFehler
{
    Keiner,
    Minuten,
    Bildrate,
    Kompression,
}

/// <summary>Ergebnis: Byte je Bild, Byte/s, Sekunden, Datenmengen und Datenraten (Mbit/s, MiB/s).</summary>
public sealed record VideoErgebnis(VideoFehler Fehler, double ByteBild, double ByteS, double Sekunden, double Unkomprimiert,
    double Komprimiert, double MbitUnkomprimiert, double MbitKomprimiert, double MiBSKomprimiert)
{
    internal static VideoErgebnis MitFehler(VideoFehler fehler) => new(fehler, 0, 0, 0, 0, 0, 0, 0, 0);
}
