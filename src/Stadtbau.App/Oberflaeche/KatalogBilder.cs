using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Stadtbau.Core;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Phase 13 Welle 3b (Weisung P12-19 Abschnitt 2, Antwort P12-23 2a): Vorschaubilder des vollen Katalogs. Je Paket ein
/// Bild mit allen Feldern (beim Wandeln erzeugt); gelesen wird ein Paket erst, wenn eines seiner Stuecke gezeigt wird.
/// </summary>
internal static class KatalogBilder
{
    private static readonly Dictionary<string, Bitmap?> Pakete = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, IImage> Felder = new(StringComparer.Ordinal);

    /// <summary>Bild eines Pakets (einmal gelesen, danach aus dem Speicher; ohne Katalog oder bei Fehler null).</summary>
    public static Bitmap? Paket(string paket)
    {
        if (Pakete.TryGetValue(paket, out var b))
        {
            return b;
        }
        try
        {
            var daten = Katalog.Aktiv?.Vorschaubild(paket);
            b = daten is null ? null : new Bitmap(new MemoryStream(daten));
        }
        catch (Exception fehler) when (fehler is InvalidDataException or IOException or ArgumentException or KeyNotFoundException or InvalidOperationException)
        {
            b = null;
        }
        Pakete[paket] = b;
        return b;
    }

    /// <summary>Ausschnitt eines Stuecks im Bild seines Pakets.</summary>
    public static (Bitmap Bild, Rect Quelle)? Feld(string typ)
    {
        if (Katalog.Aktiv is not { } k || k.Eintrag(typ) is not { } e || Paket(e.Paket) is not { } bild)
        {
            return null;
        }
        var g = k.FeldGroesse;
        return (bild, new Rect(e.Feld % k.Spalten * g, e.Feld / k.Spalten * g, g, g));
    }

    /// <summary>Ein Stueck als eigenes Bild (fuer Kacheln im Katalog).</summary>
    public static IImage? Bild(KatalogEintrag e)
    {
        if (Felder.TryGetValue(e.Id, out var fertig))
        {
            return fertig;
        }
        if (Katalog.Aktiv is not { } k || Paket(e.Paket) is not { } bild)
        {
            return null;
        }
        var g = k.FeldGroesse;
        var b = new CroppedBitmap(bild, new PixelRect(e.Feld % k.Spalten * g, e.Feld / k.Spalten * g, g, g));
        Felder[e.Id] = b;
        return b;
    }
}
