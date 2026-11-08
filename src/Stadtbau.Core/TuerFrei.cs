using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.25.0 (Sammelliste 246, Weisung P12-104, Kurzplan Meldung P12-69): ein Katalog-Stueck direkt vor einer Tuer
/// (auf der Tuer oder auf den ersten <see cref="WegStrecke"/> des Fusswegs, in der ganzen Wegbreite) ist rot, sonst kommt die Figur im Spiel nicht
/// hinein. Geprueft werden die Tueren der Orte (Zugang) und der Gebaeude ohne Ort (Zierweg). Effekte (Stern) versperren
/// nichts und bleiben erlaubt. Nur Werkzeug (Frage 2 = a): das Spiel prueft beim Laden keine Stuecke vor Tueren.
/// </summary>
public static class TuerFrei
{
    /// <summary>Laenge des Wegs ab der Tuer, die frei bleiben muss.</summary>
    public const double WegStrecke = 0.6;

    /// <summary>Schrittweite der Probepunkte auf dem Weg.</summary>
    private const double Schritt = 0.05;

    /// <summary>Halbe Breite des Fusswegs (wie im Spiel gezeichnet).</summary>
    private const double Halb = Lernplattform.Core.Spiel.Probe.StadtSzene.WegBreite / 2;

    /// <summary>Abstand eines Punkts zu einem Rechteck (innen 0).</summary>
    private static double Abstand(Vector2 p, double x, double y, double w, double h)
    {
        var dx = Math.Max(Math.Max(x - p.X, 0), p.X - (x + w));
        var dy = Math.Max(Math.Max(y - p.Y, 0), p.Y - (y + h));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>Gegenprobe: Verhalten bis S 0.24.0 (keine Pruefung), nur in Tests, je Ablauf.</summary>
    internal static readonly AsyncLocal<bool> AusFuerTests = new();

    /// <summary>Rote Funde aller Katalog-Stuecke vor einer Tuer (je Stueck hoechstens ein Fund).</summary>
    public static List<StadtbauFund> Pruefen(PyObjekt aussen)
    {
        var funde = new List<StadtbauFund>();
        if (AusFuerTests.Value)
        {
            return funde;
        }
        var tueren = Tueren(aussen);
        if (tueren.Count == 0)
        {
            return funde;
        }
        foreach (var (s, _, _) in AussenBearbeitung.Alle(aussen).Where(a => a.Stelle.Art == AussenArt.Katalog).ToList())
        {
            var eintrag = Katalog.Aktiv?.Eintrag(Katalog.Praefix + AussenBearbeitung.KatalogId(aussen, s));
            if (eintrag is { Art: KatalogArt.Effekt })
            {
                continue;
            }
            var (kx, ky, kw, kh) = AussenBearbeitung.KatalogFlaeche(aussen, s);
            foreach (var (name, punkte) in tueren)
            {
                // das Stueck beruehrt den Weg in seiner Breite (Figur geht ueber die ganze Wegbreite)
                if (punkte.Any(p => Abstand(p, kx, ky, kw, kh) <= Halb))
                {
                    var stueck = eintrag?.Anzeige ?? AussenBearbeitung.KatalogId(aussen, s);
                    funde.Add(new StadtbauFund(Texte.TuerVerstellt(stueck, name), Texte.TuerVerstelltTipp, true, kx + (kw / 2), ky + (kh / 2)));
                    break;
                }
            }
        }
        return funde;
    }

    /// <summary>Je Tuer: Name des Hauses und Probepunkte von der Tuer bis <see cref="WegStrecke"/> den Weg entlang.</summary>
    private static List<(string Name, List<Vector2> Punkte)> Tueren(PyObjekt aussen)
    {
        var tueren = new List<(string, List<Vector2>)>();
        foreach (var (s, _, _) in AussenBearbeitung.Alle(aussen).Where(a => a.Stelle.Art == AussenArt.Haus).ToList())
        {
            if (AussenBearbeitung.Zugang(aussen, s.Ort) is { } z && Punkte(z) is { } p)
            {
                var name = AussenBearbeitung.Ort(aussen, s.Ort)?["name"] as string;
                tueren.Add((string.IsNullOrEmpty(name) ? s.Ort : name, p));
            }
        }
        var darstellung = Aussen.Stadtdaten(aussen)["darstellung"] as PyObjekt;
        foreach (var z in (darstellung?["zierwege"] as List<object?> ?? []).OfType<PyObjekt>())
        {
            if (Punkte(z) is { } p)
            {
                var name = z["name"] as string;
                tueren.Add((string.IsNullOrEmpty(name) ? ModellNamen.Anzeige(aussen, Py.Str(z["modell"])) : name, p));
            }
        }
        return tueren;
    }

    /// <summary>Probepunkte der Tuer und des Wegs bis <see cref="WegStrecke"/> (ohne Tuer: null).</summary>
    private static List<Vector2>? Punkte(PyObjekt zugang)
    {
        if (zugang["tuer"] is not List<object?> { Count: >= 2 } t)
        {
            return null;
        }
        var tuer = new Vector2((float)Py.F(t[0]), (float)Py.F(t[1]));
        var linie = new List<Vector2> { tuer };
        foreach (var p in (zugang["weg"] as List<object?> ?? []).OfType<List<object?>>().Where(p => p.Count >= 2))
        {
            linie.Add(new Vector2((float)Py.F(p[0]), (float)Py.F(p[1])));
        }
        var punkte = new List<Vector2> { tuer };
        var rest = WegStrecke;
        for (var i = 1; i < linie.Count && rest > 0; i++)
        {
            var a = linie[i - 1];
            var d = linie[i] - a;
            var l = d.Length();
            if (l <= 0)
            {
                continue;
            }
            var bis = Math.Min(l, rest);
            for (var k = Schritt; k <= bis + 1e-9; k += Schritt)
            {
                punkte.Add(a + (d * (float)(k / l)));
            }
            rest -= bis;
        }
        return punkte;
    }
}
