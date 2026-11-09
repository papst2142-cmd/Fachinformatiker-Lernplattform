using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.27.0 (Weisung P12-118 Teil B, Kurzplan Meldung P12-79 Wunsch 3): Massband. Erster Tipp = Anfang, zweiter Tipp =
/// Ende (Maus und Finger, in der Draufsicht und in 3D am Boden), ein weiterer Tipp beginnt neu. Jeder Punkt rastet an Ecken
/// und Mitten der Stuecke ein (<see cref="Fangen"/>), sonst mit „Einrasten“ am Raster 0,5. Die Laenge steht in Feldern mit
/// einer Nachkommastelle (<see cref="Text"/>). Nur Anzeige: kein Schritt im Verlauf, nichts in einer Datei.
/// </summary>
public sealed class Massband
{
    /// <summary>So weit (in Bildpunkten) greift ein Fangpunkt; die Ansicht rechnet ihn in Karten-Einheiten um.</summary>
    public const double FangBildpunkte = 14;

    public Vector2? Anfang { get; private set; }

    public Vector2? Ende { get; private set; }

    /// <summary>Ende waehrend die Maus schwebt (Vorschau, nur solange kein Ende gesetzt ist).</summary>
    public Vector2? Schwebe { get; private set; }

    /// <summary>Laenge zwischen Anfang und Ende (oder der Vorschau), null ohne beide Punkte.</summary>
    public double? Laenge => Anfang is { } a && (Ende ?? Schwebe) is { } e ? Vector2.Distance(a, e) : null;

    /// <summary>Ein Tipp an (x, y) mit Fangen; true = die Messung ist fertig (Ende gesetzt).</summary>
    public bool Tippen(PyObjekt aussen, double x, double y, double fang, bool raster, IReadOnlySet<AussenArt>? aus = null)
    {
        var p = Fangen(aussen, x, y, fang, raster, aus);
        Schwebe = null;
        if (Anfang is null || Ende is not null)
        {
            Anfang = p;
            Ende = null;
            return false;
        }
        Ende = p;
        return true;
    }

    /// <summary>Vorschau des Endes (Maus schwebt); ohne Anfang oder mit Ende nichts.</summary>
    public void Schweben(PyObjekt aussen, double x, double y, double fang, bool raster, IReadOnlySet<AussenArt>? aus = null) =>
        Schwebe = Anfang is not null && Ende is null ? Fangen(aussen, x, y, fang, raster, aus) : null;

    /// <summary>Alles vergessen (Massband aus oder Escape).</summary>
    public void Leeren()
    {
        Anfang = null;
        Ende = null;
        Schwebe = null;
    }

    /// <summary>Laenge in Feldern mit einer Nachkommastelle (deutsches Komma), z. B. „3,5 Felder“.</summary>
    public static string Text(double laenge) => Texte.MassbandLaenge(Math.Round(laenge, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.GetCultureInfo("de-DE")));

    /// <summary>
    /// Punkt fuer das Massband: der naechste Fangpunkt (Ecke oder Mitte eines Stuecks, Ende oder Mitte einer Strasse) bis
    /// <paramref name="fang"/>; sonst mit <paramref name="raster"/> das Raster 0,5, ohne das feine Raster 0,1.
    /// Ausgeblendete Stuecke (<paramref name="aus"/>) fangen nicht.
    /// </summary>
    public static Vector2 Fangen(PyObjekt aussen, double x, double y, double fang, bool raster, IReadOnlySet<AussenArt>? aus = null)
    {
        var p = new Vector2((float)x, (float)y);
        Vector2? bester = null;
        var abstand = (float)fang;
        foreach (var q in Fangpunkte(aussen, aus))
        {
            var d = Vector2.Distance(p, q);
            if (d <= abstand)
            {
                (bester, abstand) = (q, d);
            }
        }
        if (bester is { } b)
        {
            return b;
        }
        var r = raster ? Bearbeitung.Raster : Bearbeitung.RasterFein;
        return new Vector2((float)Bearbeitung.AufRaster(x, r), (float)Bearbeitung.AufRaster(y, r));
    }

    /// <summary>Alle Ecken und Mitten der Stuecke draussen (Rechtecke: 4 Ecken, 4 Kantenmitten, Mitte).</summary>
    public static IEnumerable<Vector2> Fangpunkte(PyObjekt aussen, IReadOnlySet<AussenArt>? aus = null)
    {
        bool An(AussenArt a) => aus is null || !aus.Contains(a);
        var land = Aussen.Landschaft(aussen);
        foreach (var (s, x, y) in AussenBearbeitung.Alle(aussen).ToList())
        {
            if (!An(s.Art))
            {
                continue;
            }
            switch (s.Art)
            {
                case AussenArt.Haus or AussenArt.Gebaeude:
                    foreach (var q in Rechteck(AussenBearbeitung.HausFlaeche(aussen, s)))
                    {
                        yield return q;
                    }
                    break;
                case AussenArt.Katalog:
                    foreach (var q in Rechteck(AussenBearbeitung.KatalogFlaeche(aussen, s)))
                    {
                        yield return q;
                    }
                    break;
                case AussenArt.Park:
                {
                    var l = Objekt(land[s.Nummer]);
                    foreach (var q in Rechteck((Py.F(l["x"]), Py.F(l["y"]), Py.F(l["w"]), Py.F(l["h"]))))
                    {
                        yield return q;
                    }
                    break;
                }
                case AussenArt.Steg:
                    if (Wasser.Steg(Aussen.Stege(aussen)[s.Nummer]) is { } steg)
                    {
                        var (x1, z1, x2, z2) = steg.Rechteck;
                        foreach (var q in Rechteck((x1, z1, x2 - x1, z2 - z1)))
                        {
                            yield return q;
                        }
                    }
                    break;
                case AussenArt.Strasse:
                {
                    // Enden, Mitte jeder geraden Strasse; Kurve und Kreisverkehr: ihre Punkte
                    var linie = AussenBearbeitung.Linie(Objekt(land[s.Nummer])["punkte"]);
                    foreach (var q in linie)
                    {
                        yield return q;
                    }
                    if (linie.Count == 2)
                    {
                        yield return (linie[0] + linie[1]) / 2;
                    }
                    break;
                }
                case AussenArt.Teich or AussenArt.Fluss or AussenArt.Bahn:
                    foreach (var q in AussenBearbeitung.EckpunkteWerte(aussen, AussenBearbeitung.Ganz(s)))
                    {
                        yield return q;
                    }
                    break;
                case AussenArt.Fussweg:
                    foreach (var q in AussenBearbeitung.Linie(Objekt(Aussen.Fusswege(aussen)[s.Nummer])["punkte"]))
                    {
                        yield return q;
                    }
                    break;
                default:
                    // Baum, Beet, Laterne, Paketplatz, Zebrastreifen, Notiz: ihre Mitte
                    yield return new Vector2((float)x, (float)y);
                    break;
            }
        }
    }

    private static IEnumerable<Vector2> Rechteck((double X, double Y, double W, double H) r)
    {
        var (x, y, w, h) = r;
        foreach (var fx in (double[])[0, 0.5, 1])
        {
            foreach (var fy in (double[])[0, 0.5, 1])
            {
                yield return new Vector2((float)(x + (w * fx)), (float)(y + (h * fy)));
            }
        }
    }
}
