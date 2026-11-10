using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle W-B Nachbesserung (Weisung P12-137, Fund Chat: die Infozeile der Reihe lag in 3D unter der Kamera-Karte):
/// Anzeigen, die eine Flaeche selbst zeichnet (Infozeile der Reihe, Schatten-Text, Schild „n Stücke gewählt“,
/// Laenge des Massbands, Gradzahl am Drehgriff), liegen nie unter einem Element, das ueber der Flaeche liegt
/// (Kamera-Karte, Werkzeugleiste, Liste der Ebenen). Liegt eine darunter, rueckt sie darueber, sonst darunter.
/// Jede Flaeche merkt sich, wohin sie ihre Anzeigen gezeichnet hat; der Start-Test prueft das.
/// </summary>
internal static class Anzeigen
{
    /// <summary>Nur fuer die Gegenprobe im Start-Test: Anzeigen bleiben an der alten Stelle (vor P12-137).</summary>
    internal static bool AltFuerTests { get; set; }

    /// <summary>Abstand einer verschobenen Anzeige zum Element, dem sie ausweicht.</summary>
    public const double Abstand = 6;

    /// <summary>Rahmen der sichtbaren Elemente, bezogen auf <paramref name="bezug"/>.</summary>
    public static IReadOnlyList<Rect> Rahmen(Visual bezug, params Control?[] elemente)
    {
        var liste = new List<Rect>();
        foreach (var e in elemente)
        {
            if (e is { IsEffectivelyVisible: true } && e.Bounds.Width > 0 && e.Bounds.Height > 0
                && e.TranslatePoint(new Point(0, 0), bezug) is { } p)
            {
                liste.Add(new Rect(p, e.Bounds.Size));
            }
        }
        return liste;
    }

    /// <summary>
    /// Die Anzeige <paramref name="r"/> so legen, dass sie keines der verdeckenden Elemente schneidet: erst darueber,
    /// sonst darunter, immer ganz in der Flaeche (<paramref name="flaeche"/>, Breite und Hoehe).
    /// </summary>
    public static Rect FreiLegen(Rect r, IReadOnlyList<Rect> verdeckt, Size flaeche)
    {
        if (AltFuerTests || verdeckt.Count == 0)
        {
            return r;
        }
        for (var runde = 0; runde < 6; runde++)
        {
            var b = verdeckt.FirstOrDefault(v => v.Intersects(r));
            if (b == default)
            {
                return r;
            }
            var oben = r.WithY(b.Top - r.Height - Abstand);
            var unten = r.WithY(b.Bottom + Abstand);
            var obenFrei = oben.Top >= 0 && !verdeckt.Any(v => v.Intersects(oben));
            var untenFrei = unten.Bottom <= flaeche.Height && !verdeckt.Any(v => v.Intersects(unten));
            if (obenFrei)
            {
                return oben;
            }
            if (untenFrei)
            {
                return unten;
            }
            r = oben.Top >= 0 ? oben : unten;
        }
        return r;
    }

    /// <summary>
    /// Eine Anzeige zeichnen: Text <paramref name="ft"/> mit Innenabstand <paramref name="innen"/> (links/oben = rechts/unten)
    /// an der Wunschstelle <paramref name="ort"/> (linke obere Ecke des Kastens), frei gelegt, gemerkt in <paramref name="merken"/>.
    /// </summary>
    public static Rect Zeichnen(DrawingContext ctx, FormattedText ft, string text, Point ort, Size innen, IBrush? grund, IPen? rand, double ecke,
        IReadOnlyList<Rect> verdeckt, Size flaeche, List<(string Text, Rect Rahmen)> merken)
    {
        var r = FreiLegen(new Rect(ort.X, ort.Y, ft.Width + (2 * innen.Width), ft.Height + (2 * innen.Height)), verdeckt, flaeche);
        if (grund is not null || rand is not null)
        {
            ctx.DrawRectangle(grund, rand, r, ecke, ecke);
        }
        ctx.DrawText(ft, new Point(r.X + innen.Width, r.Y + innen.Height));
        merken.Add((text, r));
        return r;
    }

    /// <summary>Anzeigen, die ein verdeckendes Element schneiden (Text, Rahmen der Anzeige, Rahmen des Elements).</summary>
    public static List<string> Verdeckt(IEnumerable<(string Text, Rect Rahmen)> anzeigen, IReadOnlyList<Rect> verdeckt)
    {
        var funde = new List<string>();
        foreach (var (text, r) in anzeigen)
        {
            foreach (var v in verdeckt.Where(v => v.Intersects(r)))
            {
                funde.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"Anzeige \"{text}\" ({r.X:F0},{r.Y:F0} {r.Width:F0}x{r.Height:F0}) liegt unter einem Element ({v.X:F0},{v.Y:F0} {v.Width:F0}x{v.Height:F0})"));
            }
        }
        return funde;
    }
}
