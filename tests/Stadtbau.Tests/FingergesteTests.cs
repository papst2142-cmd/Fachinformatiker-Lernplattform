using Stadtbau.Core;
using A = Stadtbau.Core.Fingergeste.Art;

namespace Stadtbau.Tests;

/// <summary>
/// Patch S 0.5.1 (Weisung P12-37 Punkt 3, Nico: Zoomen mit zwei Fingern klappte nur manchmal): nachgestellte
/// Finger-Ereignisse. Jeder Fall hat eine Gegenprobe mit der Auswertung vor dem Fix (altFuerTests), die rot ist.
/// </summary>
public class FingergesteTests
{
    /// <summary>Zwei Finger auseinander: (100,100)/(200,100) nach (50,100)/(250,100), also Abstand 100 nach 200.</summary>
    private static List<Fingergeste.Schritt> Spreizen(Fingergeste g, bool zweiterSpaeter, bool aufStueck)
    {
        var schritte = new List<Fingergeste.Schritt> { g.Druecken(1, 100, 100, aufStueck) };
        if (zweiterSpaeter)
        {
            // erster Finger bewegt sich erst ein Stueck allein (ueber die Schwelle), dann kommt der zweite
            schritte.Add(g.Bewegen(1, 90, 100));
            schritte.Add(g.Bewegen(1, 100, 100));
        }
        schritte.Add(g.Druecken(2, 200, 100, false));
        for (var i = 1; i <= 5; i++)
        {
            schritte.Add(g.Bewegen(1, 100 - (10 * i), 100));
            schritte.Add(g.Bewegen(2, 200 + (10 * i), 100));
        }
        schritte.Add(g.Loslassen(2));
        schritte.Add(g.Loslassen(1));
        return schritte;
    }

    private static double Zoom(IEnumerable<Fingergeste.Schritt> s) =>
        s.Where(x => x.Art == A.Zoomen).Aggregate(1.0, (f, x) => f * x.Faktor);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ZweiFinger_ZoomenImmer(bool zweiterSpaeter, bool aufStueck)
    {
        var s = Spreizen(new Fingergeste(), zweiterSpaeter, aufStueck);
        Assert.Equal(2.0, Zoom(s), 6);
        // nach zwei Fingern weder Tippen noch ein verschobenes Stueck
        Assert.DoesNotContain(s, x => x.Art is A.Tippen or A.StueckFertig);
        if (zweiterSpaeter && aufStueck)
        {
            Assert.Contains(s, x => x.Art == A.StueckAbbrechen);
        }

        // Gegenprobe: Auswertung vor S 0.5.1 zoomt nicht (jeder Druck begann neu)
        var alt = Spreizen(new Fingergeste(altFuerTests: true), zweiterSpaeter, aufStueck);
        Assert.Equal(1.0, Zoom(alt), 6);
    }

    [Fact]
    public void ZweiFinger_MitteSchiebtDieAnsicht()
    {
        var g = new Fingergeste();
        g.Druecken(1, 100, 100, false);
        g.Druecken(2, 200, 100, false);
        // beide Finger um (30, 40): Abstand am Ende gleich, die Mitte wandert mit
        var a = g.Bewegen(1, 130, 140);
        var s = g.Bewegen(2, 230, 140);
        Assert.Equal((A.Zoomen, A.Zoomen), (a.Art, s.Art));
        Assert.Equal(1.0, a.Faktor * s.Faktor, 6);
        Assert.Equal((30.0, 40.0), (a.Dx + s.Dx, a.Dy + s.Dy));
        Assert.Equal((180.0, 140.0), (s.X, s.Y));
        Assert.True(g.ZweiFinger);
    }

    [Fact]
    public void EinFinger_FreieFlaecheSchiebt_StueckZieht_TippenOhneBewegung()
    {
        var g = new Fingergeste();
        g.Druecken(1, 10, 10, false);
        var s = g.Bewegen(1, 30, 10);
        Assert.Equal((A.AnsichtSchieben, 20.0), (s.Art, s.Dx));
        Assert.Equal(A.Nichts, g.Loslassen(1).Art);

        g.Druecken(1, 10, 10, true);
        Assert.Equal(A.StueckZiehen, g.Bewegen(1, 30, 15).Art);
        var f = g.Loslassen(1);
        Assert.Equal((A.StueckFertig, 20.0, 5.0), (f.Art, f.Dx, f.Dy));

        g.Druecken(1, 10, 10, true);
        Assert.Equal(A.Nichts, g.Bewegen(1, 12, 11).Art);
        Assert.Equal(A.Tippen, g.Loslassen(1).Art);
    }

    [Fact]
    public void ZweiterFinger_AendertDenErstenNicht_NeueGesteErstWennAlleOben()
    {
        var g = new Fingergeste();
        g.Druecken(1, 100, 100, true);
        g.Druecken(2, 200, 100, false);
        Assert.Equal(A.Nichts, g.Loslassen(2).Art);
        // der erste Finger allein schiebt jetzt kein Stueck und tippt nicht
        Assert.Equal(A.Nichts, g.Bewegen(1, 150, 100).Art);
        Assert.Equal(A.Nichts, g.Loslassen(1).Art);
        Assert.False(g.Aktiv);
        Assert.False(g.ZweiFinger);
        // neue Geste
        g.Druecken(3, 10, 10, false);
        Assert.Equal(A.Tippen, g.Loslassen(3).Art);
    }

    [Fact]
    public void ZeigerVerloren_BrichtZiehenAb()
    {
        var g = new Fingergeste();
        g.Druecken(1, 10, 10, true);
        g.Bewegen(1, 40, 10);
        Assert.Equal(A.StueckAbbrechen, g.Verloren(1).Art);
        Assert.False(g.Aktiv);
    }
}
