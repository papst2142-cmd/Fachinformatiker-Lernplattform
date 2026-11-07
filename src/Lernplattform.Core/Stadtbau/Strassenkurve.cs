using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle K1 (A 0.44.0 / S 0.10.0, Weisung P12-55, Plan P12-36 Abschnitte 3 bis 5): Kurvenstuecke fuer Strassen und die
/// erlaubten Radien fuer Fluss und Bahn. Eine Kurve ist eine Strasse in <c>karte.welt.landschaft</c> mit mehr Punkten
/// (alle <see cref="SchrittGrad"/> Grad einer, auf der Mittellinie) und dem Zusatzfeld <c>kurve</c>
/// (<c>{"form": "bogen" | "s", "winkel": 90 | 45, "radius": …}</c>), an dem das Werkzeug sie wiedererkennt. Keine neue
/// Art und keine neue Formatnummer: Ladepruefung und Spiel-Kern kennen Linienzuege schon. Fluss und Bahn bekommen das
/// Feld <c>radien</c> (je Punkt ein Wunschradius, ohne Feld gilt <see cref="Spiel.Probe.Kurve.Radius"/> wie bisher).
/// </summary>
public static class Strassenkurve
{
    /// <summary>Ein Punkt der Mittellinie alle so viel Grad.</summary>
    public const double SchrittGrad = 7.5;

    /// <summary>Feld der Strasse, an dem das Werkzeug eine Kurve erkennt.</summary>
    public const string Feld = "kurve";

    /// <summary>Feld mit dem Wunschradius je Punkt (Fluss, Bahn).</summary>
    public const string RadienFeld = "radien";

    public const string Bogen = "bogen";

    public const string S = "s";

    /// <summary>Erlaubte Kurven: Form, Winkel je Bogen, Radius (Mitte der Fahrbahn).</summary>
    public static readonly IReadOnlyList<(string Form, int Winkel, double Radius)> Erlaubt =
    [
        (Bogen, 90, 1), (Bogen, 90, 2), (Bogen, 90, 3), (Bogen, 45, 2), (S, 45, 2),
    ];

    /// <summary>Erlaubte Radien je Eckpunkt eines Flusses (3 = wie bisher).</summary>
    public static readonly IReadOnlyList<double> FlussRadien = [2, 3, 5];

    /// <summary>Erlaubte Radien je Eckpunkt der Bahn (K2; 3 = wie bisher, kleiner ragen die Wagen ueber die Schienen).</summary>
    public static readonly IReadOnlyList<double> BahnRadien = [3, 4.5];

    /// <summary>Kleinster Radius der Bahn.</summary>
    public const double BahnMindestens = 3;

    /// <summary>Ist die Strasse eine Kurve aus dem Werkzeug (Feld <see cref="Feld"/>)?</summary>
    public static bool IstKurve(PyObjekt strasse) => Py.Gleich(strasse["typ"], "strasse") && Py.Hat(strasse, Feld);

    /// <summary>
    /// Welle Z (A 0.48.0, Weisung P12-71, Nico P12-67 Frage 3 = a): Boegen aus dem Ziehen von Punkt zu Punkt haben jeden
    /// Winkel in Schritten von <see cref="SchrittGrad"/> Grad von <see cref="ZugMindestens"/> bis <see cref="ZugHoechstens"/>
    /// Grad mit einem der heutigen Radien (<see cref="ZugRadien"/>). Mindestens zwei Schritte: ein Bogen aus nur einem
    /// Schritt haette zwei Punkte und waere fuer das Spiel eine gerade Strasse (kein Band, kein Anschluss, Sehne schief).
    /// </summary>
    public const double ZugMindestens = 2 * SchrittGrad;

    public const double ZugHoechstens = 90;

    /// <summary>Radien der Boegen aus dem Ziehen (wie die Kurve 90°).</summary>
    public static readonly IReadOnlyList<double> ZugRadien = [1, 2, 3];

    /// <summary>Ein Bogen aus dem Ziehen: Winkel in 7,5-Grad-Schritten von 15 bis 90 Grad, Radius 1, 2 oder 3.</summary>
    public static bool ZugBogen(string form, double winkel, double radius)
    {
        var schritte = winkel / SchrittGrad;
        return form == Bogen && Math.Abs(schritte - Math.Round(schritte)) < 1e-9 && winkel >= ZugMindestens - 1e-9 && winkel <= ZugHoechstens + 1e-9
            && ZugRadien.Any(z => Math.Abs(z - radius) < 1e-9);
    }

    /// <summary>Gueltige Angabe <c>kurve</c> (Form, Winkel und Radius aus <see cref="Erlaubt"/> oder ein Bogen aus dem Ziehen)?</summary>
    public static bool Gueltig(object? kurve) =>
        kurve is PyObjekt k && k["form"] is string form && Zahl(k["winkel"]) is { } w && Zahl(k["radius"]) is { } r
        && (Erlaubt.Any(e => e.Form == form && e.Winkel == w && Math.Abs(e.Radius - r) < 1e-9) || ZugBogen(form, w, r));

    private static double? Zahl(object? wert) => wert is long or double ? Py.F(wert) : null;

    /// <summary>Kennung fuer das Setzen im Werkzeug: „bogen:90:2“, „s:45:2“; mit „:m“ gespiegelt.</summary>
    public static string Kennung(string form, int winkel, double radius, bool gespiegelt) =>
        string.Create(CultureInfo.InvariantCulture, $"{form}:{winkel}:{radius}") + (gespiegelt ? ":m" : "");

    /// <summary>Kennung lesen (null = keine gueltige Kurve).</summary>
    public static (string Form, int Winkel, double Radius, bool Gespiegelt)? Lesen(string? kennung)
    {
        var teile = (kennung ?? "").Split(':');
        if (teile.Length is < 3 or > 4 || !int.TryParse(teile[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)
            || !double.TryParse(teile[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var r) || (teile.Length == 4 && teile[3] != "m"))
        {
            return null;
        }
        var form = teile[0];
        return Erlaubt.Any(e => e.Form == form && e.Winkel == w && Math.Abs(e.Radius - r) < 1e-9) ? (form, w, r, teile.Length == 4) : null;
    }

    /// <summary>
    /// Mittellinie der Kurve, Anfang bei (0, 0) in Richtung +x, Bogen nach +y (auf der Karte rechts herum; gespiegelt nach
    /// −y). S-Kurve: erst ein Bogen nach +y, dann einer zurueck, das Ende zeigt wieder nach +x.
    /// </summary>
    public static List<Vector2> Mittellinie(string form, double winkel, double radius, bool gespiegelt = false)
    {
        var seite = gespiegelt ? -1.0 : 1.0;
        var punkte = new List<Vector2> { Vector2.Zero };
        var schritte = (int)Math.Round(winkel / SchrittGrad);
        // erster Bogen: Mittelpunkt (0, r) bzw. (0, -r)
        for (var k = 1; k <= schritte; k++)
        {
            var t = k * SchrittGrad * Math.PI / 180;
            punkte.Add(new Vector2((float)(radius * Math.Sin(t)), (float)(seite * radius * (1 - Math.Cos(t)))));
        }
        if (form == S)
        {
            // zweiter Bogen in die Gegenrichtung, gleicher Radius: Punktspiegelung des ersten an seinem Ende
            var ende = punkte[^1];
            for (var k = schritte - 1; k >= 0; k--)
            {
                punkte.Add((2 * ende) - punkte[k]);
            }
        }
        return punkte;
    }

    /// <summary>Text fuer Anzeige und Screenreader, z. B. „Kurve 90°, Radius 2“.</summary>
    public static string Name(string form, double winkel, double radius) =>
        (form == S ? "S-Kurve" : string.Create(CultureInfo.GetCultureInfo("de-DE"), $"Kurve {winkel:0.#}°"))
        + string.Create(CultureInfo.GetCultureInfo("de-DE"), $", Radius {radius:0.##}");
}
