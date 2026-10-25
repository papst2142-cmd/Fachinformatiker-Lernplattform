using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Nachtrag 1 zu P7-13 (Wunsch 1, Foto 01): kein Baum steht auf Strasse, Fussweg, Gleis, Wasser oder
/// Bruecke (Bruecken liegen ueber dem Wasser und auf Strasse oder Fussweg). Geprueft werden alle Baeume
/// der Landschaft und der Modelle, mit und ohne CC0-Grafik und mit einem Spielstand.
/// </summary>
public sealed class SpielBaumTests
{
    private static Stadt NeueStadt() => new(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);

    /// <summary>Kleinster Abstand der Krone zum Rand der gesperrten Flaeche (negativ = steht darauf).</summary>
    private static float Frei(Vector2 p, float r, IReadOnlyList<Vector2> linie, float halb)
    {
        var abstand = float.MaxValue;
        for (var i = 1; i < linie.Count; i++)
        {
            var (a, b) = (linie[i - 1], linie[i]);
            var ab = b - a;
            var t = ab.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f) : 0f;
            abstand = Math.Min(abstand, Vector2.Distance(p, a + (ab * t)));
        }
        return abstand - halb - r;
    }

    private static List<string> Funde(StadtSzene szene) =>
        [.. from b in szene.Baeume
            from s in szene.BaumSperren
            let frei = Frei(b.Mitte, b.R, s.Linie, s.Halb)
            where frei < 0
            select $"Baum ({b.Mitte.X:0.00}, {b.Mitte.Y:0.00}) r {b.R:0.00}: {-frei:0.000} auf der Linie ab ({s.Linie[0].X}, {s.Linie[0].Y})"];

    [Fact]
    public void KeinBaumAufStrasseWegGleisOderWasser()
    {
        var stadt = NeueStadt();
        var stand = SpielFingerabdruckTests.Gespielt.Value.Stand;
        foreach (var szene in new[] { StadtSzene.Bauen(stadt, null), StadtSzene.Bauen(stadt, null, SpielGrafikTests.Bibliothek.Value), StadtSzene.Bauen(stadt, stand) })
        {
            Assert.True(szene.Baeume.Count > 20, "alle Baeume erfasst");
            Assert.True(szene.BaumSperren.Count > 10, "Strassen, Wege, Gleise und Wasser erfasst");
            Assert.Empty(Funde(szene));
        }
    }

    [Fact]
    public void PruefungFindetDenBaumAufDerKreuzung()
    {
        // Kontrollfehler: der Baum der Wohnung stand bei (29,9 | 20,0) auf der Strasse x = 29,8 (Foto 01);
        // die Pruefung muss ihn dort finden, und in der Szene steht keiner mehr auf diesem Fleck
        var szene = StadtSzene.Bauen(NeueStadt(), null);
        var alt = new Vector2(29.9f, 20.0f);
        Assert.Contains(szene.BaumSperren, s => Frei(alt, 0.4f, s.Linie, s.Halb) < 0);
        Assert.DoesNotContain(szene.Baeume, b => Vector2.Distance(b.Mitte, alt) < 0.05f);
        Assert.Contains(szene.Baeume, b => Vector2.Distance(b.Mitte, alt) < 1.2f);
    }
}
