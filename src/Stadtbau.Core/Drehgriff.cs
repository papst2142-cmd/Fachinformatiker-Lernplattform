namespace Stadtbau.Core;

/// <summary>
/// Welle E (Weisung P12-67 Frage 2 = a): Drehgriff am gewaehlten Stueck (Draufsicht und 3D-Ansicht), vor allem fuer das
/// Tablet. Ein Kreis ueber dem Stueck; ein Finger (oder die Maus) zieht ihn um die Mitte, das Stueck dreht in Rasten von
/// <see cref="AussenBearbeitung.DrehSchritt"/> Grad (Linien 15, sonst 90). Zwei Finger bleiben bei der Kamera bzw. Ansicht.
/// Rechnet nur in Bildpunkten (ohne Oberflaeche), damit beide Ansichten und die Tests denselben Weg nehmen.
/// </summary>
public static class Drehgriff
{
    /// <summary>Abstand des Griffs ueber der Mitte des Stuecks (Bildpunkte, ohne Schriftfaktor).</summary>
    public const double Abstand = 56;

    /// <summary>Halbmesser des gezeichneten Griffs.</summary>
    public const double Halbmesser = 13;

    /// <summary>So nah muss ein Finger am Griff sein (44 Bildpunkte Tippflaeche).</summary>
    public const double Greifen = 22;

    /// <summary>Lage des Griffs zur Mitte (mx, my) des Stuecks im Bild.</summary>
    public static (double X, double Y) Lage(double mx, double my) => (mx, my - Abstand);

    /// <summary>Liegt (x, y) auf dem Griff?</summary>
    public static bool Getroffen(double mx, double my, double x, double y)
    {
        var (gx, gy) = Lage(mx, my);
        return ((x - gx) * (x - gx)) + ((y - gy) * (y - gy)) <= Greifen * Greifen;
    }

    /// <summary>
    /// Gedrehte Grad (Uhrzeigersinn positiv, im Bild mit y nach unten) vom Griff bis zum Finger (x, y), auf ganze
    /// <paramref name="schritt"/> gerundet und auf -180 bis 180 begrenzt.
    /// </summary>
    public static int Grad(double mx, double my, double x, double y, int schritt)
    {
        var (gx, gy) = Lage(mx, my);
        var start = Math.Atan2(gy - my, gx - mx);
        var jetzt = Math.Atan2(y - my, x - mx);
        var grad = (jetzt - start) * 180 / Math.PI;
        grad = ((grad % 360) + 540) % 360 - 180;
        return (int)(Math.Round(grad / schritt, MidpointRounding.AwayFromZero) * schritt);
    }
}
