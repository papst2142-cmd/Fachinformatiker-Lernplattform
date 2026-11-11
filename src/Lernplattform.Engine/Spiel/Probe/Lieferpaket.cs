using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 12 Welle 2 (Weisung P12-5 Abschnitt 2, Chat-Fund Bild 06; Nico 4a und 5b): sichtbare Kartons vor der Haustuer der
/// Wohnung, ein Stapel bei mehreren Paketen. Nur Darstellung: der Stapel ist kein Hindernis, steht neben Stufe und Fussweg
/// und aendert weder Laufraster noch Gruen noch die Fest-Deko (Fingerabdruecke bleiben). Der Platz steht an einer Stelle,
/// <c>darstellung.paket_platz</c> in stadt.json (vor = Abstand von der Wand nach aussen, seitlich = entlang der Wand);
/// kommen spaeter Gehwege neben der Strasse, aendert sich nur dieses Feld (Nico 5b). Modell: der Umzugskarton
/// „cardboardBoxClosed“ aus dem Kenney Furniture Kit (CC0, schon seit Phase 7 in modelle.bin, nichts neu angehaengt).
/// </summary>
public static class Lieferpaket
{
    public const string Modell = "cardboardBoxClosed";

    /// <summary>Schluessel in stadt.json → darstellung.</summary>
    public const string Abschnitt = "paket_platz";

    /// <summary>Kantenlaenge eines Kartons (Stadt-Einheiten, 1 m = 0,2) und Hoehe.</summary>
    public const float Breite = 0.13f;

    public const float Hoehe = 0.1f;

    /// <summary>Hoechstens so viele Kartons sind zu sehen (mehr Pakete: derselbe Stapel).</summary>
    public const int HoechstensSichtbar = 6;

    public static readonly Vector3 Ersatzfarbe = Probe.Netz.Farbe("#C79A62");

    /// <summary>Werte aus <c>darstellung.paket_platz</c> (fehlt er: vor 0,5, seitlich 0,42).</summary>
    public static (float Vor, float Seitlich) Platz(IStadtSicht stadt)
    {
        var p = Get(StadtTueren.Darstellung(stadt), Abschnitt) as PyObjekt ?? [];
        return ((float)Py.F(Get(p, "vor", 0.5)), (float)Py.F(Get(p, "seitlich", 0.42)));
    }

    /// <summary>Id des Orts der Wohnung in der Szene (Ansicht „zuhause“), null ohne Wohnung.</summary>
    public static string? WohnOrt(StadtSzene szene) => szene.Orte.FirstOrDefault(o => o.Ansicht == SpielDaten.OrtZuhause)?.Id;

    /// <summary>Mitte des Stapels am Boden und die Richtung nach aussen (null, wenn die Wohnung keine Tuer hat).</summary>
    public static (Vector2 Mitte, Vector2 Aussen)? Lage(StadtSzene szene, IStadtSicht stadt)
    {
        if (WohnOrt(szene) is not { } ort || szene.Tueren.FirstOrDefault(t => t.Ort == ort) is not { } tuer)
        {
            return null;
        }
        var (vor, seitlich) = Platz(stadt);
        var n = Vector2.Normalize(tuer.Aussen);
        var quer = new Vector2(-n.Y, n.X);
        return (tuer.Mitte + (n * vor) + (quer * seitlich), n);
    }

    /// <summary>Plaetze der Kartons im Stapel (unten zwei nebeneinander, darauf versetzt), relativ zur Mitte, je (quer, aussen, unten).</summary>
    public static IReadOnlyList<Vector3> Stapel(int anzahl)
    {
        var plaetze = new List<Vector3>();
        Vector3[] muster =
        [
            new(0, 0, 0), new(-Breite * 1.05f, 0.01f, 0), new(-Breite * 0.5f, 0, Hoehe),
            new(Breite * 0.02f, -0.02f, Hoehe * 2), new(-Breite * 1.05f, -0.01f, Hoehe * 2), new(-Breite * 0.5f, 0.01f, Hoehe * 3),
        ];
        for (var i = 0; i < Math.Min(anzahl, HoechstensSichtbar); i++)
        {
            plaetze.Add(muster[i]);
        }
        return plaetze;
    }

    /// <summary>
    /// Netz des Stapels mit <paramref name="anzahl"/> Kartons, Ursprung in der Mitte am Boden, „aussen“ = +Z.
    /// Ohne Grafik (oder ohne das Modell) einfache Kaesten in Kartonfarbe.
    /// </summary>
    public static Netz Bauen(GrafikBibliothek? grafik, int anzahl)
    {
        var netz = Probe.Netz.Aus([]);
        var modell = grafik?.Modell(Modell);
        var i = 0;
        foreach (var p in Stapel(anzahl))
        {
            // jeder Karton etwas anders gedreht, damit der Stapel nicht wie ein Block aussieht
            var winkel = (i++ % 3 - 1) * 0.12f;
            var drehung = Matrix4x4.CreateRotationY(winkel);
            var ort = Matrix4x4.CreateTranslation(p.X, p.Z, p.Y);
            if (modell is null)
            {
                var h = Breite / 2;
                netz.Hinzu(new Teil(TeilArt.Quader, p.X - h, p.Z, p.Y - h, p.X + h, p.Z + Hoehe, p.Y + h, Ersatzfarbe));
                continue;
            }
            var mass = new Vector3(Breite / MathF.Max(modell.Max.X - modell.Min.X, 1e-4f), Hoehe / MathF.Max(modell.Max.Y - modell.Min.Y, 1e-4f),
                Breite / MathF.Max(modell.Max.Z - modell.Min.Z, 1e-4f));
            var anker = new Vector3((modell.Min.X + modell.Max.X) / 2, modell.Min.Y, (modell.Min.Z + modell.Max.Z) / 2);
            netz.Hinzu(modell, Matrix4x4.CreateTranslation(-anker) * Matrix4x4.CreateScale(mass) * drehung * ort);
        }
        return netz;
    }

    /// <summary>Lage des Stapels in der Welt: gedreht zur Tuer (+Z nach aussen), auf dem Boden.</summary>
    public static Matrix4x4 Welt(Vector2 mitte, Vector2 aussen) =>
        StadtSzene.Drehung(aussen) * Matrix4x4.CreateTranslation(mitte.X, 0.002f, mitte.Y);
}

/// <summary>Phase 12 Welle 2 (Nico 2b): Kennung und Modell des begehbaren Kleiderschranks (Darstellung im Innenraum).</summary>
public static class Begehbar
{
    /// <summary>Id des Moebels in geschaefte.json moebel_neu.</summary>
    public const string Id = "kleiderschrank_begehbar";

    /// <summary>Garderobenstaender aus dem Kenney Furniture Kit (CC0), angehaengt in modelle.bin (Phase 12 Welle 2).</summary>
    public const string Modell = "coatRackStanding";

    /// <summary>Farben der Kleidung im Schrank.</summary>
    public static readonly IReadOnlyList<Vector3> Farben =
    [
        Netz.Farbe("#2563EB"), Netz.Farbe("#DC2626"), Netz.Farbe("#F5F5F4"), Netz.Farbe("#16A34A"), Netz.Farbe("#1F2937"),
        Netz.Farbe("#F59E0B"), Netz.Farbe("#7C3AED"), Netz.Farbe("#0EA5E9"),
    ];
}
