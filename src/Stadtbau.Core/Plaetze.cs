using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.56.0 (Sammelliste 362, Weisung P12-287, Nico 1a): festes Raster aus Plaetzen von 64 × 64 Feldern. Eine Karte
/// 256 × 256 hat 16 Plaetze (4 × 4), eine 128 × 128 hat 4. Nummer 1 ist oben links, gezaehlt Reihe fuer Reihe von links
/// nach rechts (Platz 1 bis 4 die oberste Reihe einer Karte 256 breit). Ein Platz besteht aus 4 × 4 Kacheln des Nachladens.
/// </summary>
public static class Plaetze
{
    /// <summary>Kantenlaenge eines Platzes (Felder).</summary>
    public const int Seite = 64;

    /// <summary>Plaetze nebeneinander (ganze Plaetze; ein Rest unter 64 zaehlt nicht).</summary>
    public static int Spalten(double breite) => (int)Math.Floor(breite / Seite + 1e-9);

    /// <summary>Plaetze untereinander.</summary>
    public static int Reihen(double hoehe) => (int)Math.Floor(hoehe / Seite + 1e-9);

    /// <summary>Anzahl der Plaetze einer Karte.</summary>
    public static int Anzahl(double breite, double hoehe) => Spalten(breite) * Reihen(hoehe);

    /// <summary>Gibt es den Platz <paramref name="platz"/> (ab 1) auf einer Karte dieser Groesse?</summary>
    public static bool Gibt(int platz, double breite, double hoehe) => platz >= 1 && platz <= Anzahl(breite, hoehe);

    /// <summary>Linke obere Ecke (x, y) des Platzes (ab 1) auf einer Karte der Breite <paramref name="breite"/>.</summary>
    public static (int X, int Y) Lage(int platz, double breite)
    {
        var spalten = Math.Max(1, Spalten(breite));
        return ((platz - 1) % spalten * Seite, (platz - 1) / spalten * Seite);
    }

    /// <summary>Platz (ab 1) an der Stelle (x, y), 0 ausserhalb der Plaetze.</summary>
    public static int Bei(double x, double y, double breite, double hoehe)
    {
        if (x < 0 || y < 0)
        {
            return 0;
        }
        var (s, r) = ((int)Math.Floor(x / Seite), (int)Math.Floor(y / Seite));
        return s < Spalten(breite) && r < Reihen(hoehe) ? (r * Spalten(breite)) + s + 1 : 0;
    }

    /// <summary>Erlaubte Seitenlaengen beim Vergroessern: ganze Plaetze bis 256.</summary>
    public static readonly IReadOnlyList<int> Seiten = [64, 128, 192, 256];

    /// <summary>Abstand, ab dem ein Ende nicht mehr am Rand bzw. am Gegenstueck liegt.</summary>
    public const double RandGenau = 0.05;

    /// <summary>
    /// Nico 3a (Weisung P12-287): gelbe Hinweise fuer Strassen, Bahn, Fluss und Fusswege, die genau an der Grenze zweier Plaetze
    /// enden, ohne dass drueben eine Linie derselben Art weitergeht (Ende, Punkt oder Strecke bis <see cref="RandGenau"/>).
    /// Der aeussere Rand der Karte zaehlt nicht. Nur Hinweis, nie rot; verbinden macht Nico selbst.
    /// </summary>
    public static List<StadtbauFund> RandHinweise(PyObjekt aussen)
    {
        var (breite, hoehe) = Aussen.Groesse(aussen);
        var funde = new List<StadtbauFund>();
        if (Anzahl(breite, hoehe) < 2)
        {
            return funde;
        }
        var linien = new List<(string Art, List<Vector2> Punkte)>();
        foreach (var l in Aussen.Landschaft(aussen).OfType<PyObjekt>())
        {
            var art = Get(l, "typ") as string;
            if (art is "strasse" or "bahn" or "fluss")
            {
                linien.Add((art, AussenBearbeitung.Linie(Get(l, "punkte"))));
            }
        }
        linien.AddRange(ListeOderLeer(Aussen.Stadtdaten(aussen), "fusswege").OfType<PyObjekt>().Select(f => ("fussweg", AussenBearbeitung.Linie(Get(f, "punkte")))));
        bool AmRand(Vector2 p)
        {
            bool Grenze(double w, double grenze) => Math.Abs(w - (Math.Round(w / Seite) * Seite)) < RandGenau && w > RandGenau && w < grenze - RandGenau;
            return Grenze(p.X, Spalten(breite) * Seite) || Grenze(p.Y, Reihen(hoehe) * Seite);
        }
        for (var i = 0; i < linien.Count; i++)
        {
            var (art, punkte) = linien[i];
            if (punkte.Count < 2)
            {
                continue;
            }
            foreach (var ende in new[] { punkte[0], punkte[^1] })
            {
                if (!AmRand(ende))
                {
                    continue;
                }
                var drueben = linien.Where((l, j) => j != i && l.Art == art).Any(l => Abstand(ende, l.Punkte) < RandGenau);
                if (!drueben)
                {
                    funde.Add(new StadtbauFund(Texte.RandHinweis(ArtName(art), ende.X, ende.Y), Texte.RandOffen, false, ende.X, ende.Y));
                }
            }
        }
        return funde;
    }

    private static string ArtName(string art) => art switch
    {
        "strasse" => Texte.ArtName(AussenArt.Strasse),
        "bahn" => Texte.ArtName(AussenArt.Bahn),
        "fluss" => Texte.ArtName(AussenArt.Fluss),
        _ => Texte.ArtName(AussenArt.Fussweg),
    };

    /// <summary>Kleinster Abstand des Punkts zur Linie (Strecken zwischen den Punkten).</summary>
    private static double Abstand(Vector2 p, List<Vector2> linie)
    {
        if (linie.Count == 1)
        {
            return Vector2.Distance(p, linie[0]);
        }
        var best = double.MaxValue;
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            var (a, b) = (linie[i], linie[i + 1]);
            var d = b - a;
            var t = d.LengthSquared() < 1e-12 ? 0 : Math.Clamp(Vector2.Dot(p - a, d) / d.LengthSquared(), 0, 1);
            best = Math.Min(best, Vector2.Distance(p, a + (d * (float)t)));
        }
        return best;
    }
}
