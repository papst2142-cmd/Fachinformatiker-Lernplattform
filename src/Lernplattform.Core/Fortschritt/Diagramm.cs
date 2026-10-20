using System.Globalization;

namespace Lernplattform.Core.Fortschritt;

/// <summary>
/// Phase 2, L7: Rechenteil der Diagramme im Fortschritt wie Python v0.63.2
/// (fisi_widgets._nice_step/_axis_text, fisi_theme.label_stride/shown_labels/curve_controls).
/// Gezeichnet wird in der Oberflaeche; hier nur Achse, Beschriftungsabstand und Kurve.
/// </summary>
public static class Diagramm
{
    /// <summary>LABEL_STRIDES (fisi_theme 1189).</summary>
    public static readonly IReadOnlyList<int> Schritte = [1, 2, 3, 5, 7, 10, 14, 15, 30];

    /// <summary>LABEL_SPACING (fisi_theme 1188).</summary>
    public const double Abstand = 1.8;

    /// <summary>_nice_step: glatte Schrittweite fuer eine Achse, mindestens 1.</summary>
    public static int GlatterSchritt(double roh)
    {
        if (roh <= 1)
        {
            return 1;
        }
        // 10 ** math.floor(...) rechnet Python ganzzahlig, daher hier ohne Math.Pow
        var potenz = 1.0;
        for (var i = (int)Math.Floor(Math.Log10(roh)); i > 0; i--)
        {
            potenz *= 10;
        }
        foreach (var faktor in (double[])[1, 1.2, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10])
        {
            var schritt = faktor * potenz;
            if (roh <= schritt && schritt == Math.Floor(schritt))
            {
                return (int)schritt;
            }
        }
        return (int)(10 * potenz);
    }

    /// <summary>_axis_text: Achsenbeschriftung mit Tausenderpunkt (12.500 statt 12500).</summary>
    public static string AchsenText(double wert)
    {
        if (wert == Math.Floor(wert))
        {
            return ((long)wert).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
        }
        return wert.ToString("G6", CultureInfo.InvariantCulture).Replace('.', ',');
    }

    /// <summary>
    /// Obergrenze und Schritt der y-Achse (LineChart._draw): fest (z. B. 100 bei Prozent)
    /// oder aus den Werten und dem Tagesziel, mindestens 5.
    /// </summary>
    public static (double Spitze, double Schritt) Achse(IEnumerable<double> werte, double? fest, double? ziel)
    {
        if (fest is { } f && f != 0)
        {
            return (f, f / 4.0);
        }
        var spitze = werte.DefaultIfEmpty(0).Max();
        if (ziel is { } z && z != 0)
        {
            spitze = Math.Max(spitze, z * 1.1);
        }
        spitze = Math.Max(5, spitze);
        var schritt = GlatterSchritt(spitze / 4.0);
        return (schritt * 4, schritt);
    }

    /// <summary>label_stride: Schrittweite fuer anzahl Beschriftungen im Abstand platz.</summary>
    public static int Schrittweite(int anzahl, double platz, double breite)
    {
        if (anzahl <= 1 || platz <= 0)
        {
            return 1;
        }
        foreach (var schritt in Schritte)
        {
            if (platz * schritt >= breite * Abstand)
            {
                return schritt;
            }
        }
        return Math.Max(1, anzahl);
    }

    /// <summary>shown_labels: Stellen der beschrifteten Punkte, vom letzten Punkt aus gezaehlt.</summary>
    public static List<int> Gezeigt(int anzahl, int schritt) =>
        Enumerable.Range(0, anzahl).Where(i => (anzahl - 1 - i) % schritt == 0).ToList();

    /// <summary>
    /// curve_controls: Kontrollpunkte einer monotonen Kurve (Fritsch und Carlson) durch alle
    /// Punkte, je Abschnitt (c1, c2) fuer ein kubisches Bezier-Stueck.
    /// </summary>
    public static List<((double X, double Y) C1, (double X, double Y) C2)> Kontrollpunkte(IReadOnlyList<(double X, double Y)> punkte)
    {
        var ergebnis = new List<((double, double), (double, double))>();
        var anzahl = punkte.Count;
        if (anzahl < 2)
        {
            return ergebnis;
        }
        var steigungen = new List<double>();
        for (var i = 0; i < anzahl - 1; i++)
        {
            var (x1, y1) = punkte[i];
            var (x2, y2) = punkte[i + 1];
            steigungen.Add(x2 != x1 ? (y2 - y1) / (x2 - x1) : 0.0);
        }
        var tangenten = new List<double> { steigungen[0] };
        for (var i = 0; i < steigungen.Count - 1; i++)
        {
            var (vorher, nachher) = (steigungen[i], steigungen[i + 1]);
            tangenten.Add(vorher * nachher <= 0 ? 0.0 : 2.0 / (1.0 / vorher + 1.0 / nachher));
        }
        tangenten.Add(steigungen[^1]);
        for (var i = 0; i < anzahl - 1; i++)
        {
            var (x1, y1) = punkte[i];
            var (x2, y2) = punkte[i + 1];
            var drittel = (x2 - x1) / 3.0;
            ergebnis.Add(((x1 + drittel, y1 + tangenten[i] * drittel), (x2 - drittel, y2 - tangenten[i + 1] * drittel)));
        }
        return ergebnis;
    }
}
