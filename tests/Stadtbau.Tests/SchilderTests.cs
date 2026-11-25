using Stadtbau.Core;
using K = Stadtbau.Core.Schilder.Kasten;
using S = Stadtbau.Core.Schilder.Schild;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.64.0 (Weisung P12-380 Punkt 415, P12-381 Antwort 5a und Zusatz): Schilder im Grundriss ganz sichtbar und ohne
/// Ueberdeckung. Jeder Fall mit Gegenprobe (alte Zeichnung ist rot). Der Start-Test prueft dasselbe in allen Raeumen des
/// Spiels mit echten Schriftbreiten.
/// </summary>
public sealed class SchilderTests
{
    private static readonly K Sicht = new(0, 0, 400, 300);

    private static void AlleFreiOhneUeberdeckung(List<Schilder.Gesetzt> g, IReadOnlyList<K> sperren)
    {
        for (var i = 0; i < g.Count; i++)
        {
            Assert.True(g[i].Frei, g[i].Text + " nicht frei");
            Assert.True(g[i].Lage.InnerhalbVon(Sicht), g[i].Text + " nicht ganz sichtbar");
            Assert.All(sperren, s => Assert.Equal(0, s.Ueberdeckung(g[i].Lage)));
            for (var j = i + 1; j < g.Count; j++)
            {
                Assert.True(g[i].Lage.Ueberdeckung(g[j].Lage) == 0, g[i].Text + " ueber " + g[j].Text);
            }
        }
    }

    private static List<Schilder.Gesetzt> Alt(IReadOnlyList<S> s, IReadOnlyList<K> sperren)
    {
        Schilder.AltFuerTests.Value = true;
        try
        {
            return Schilder.Setzen(s, sperren, Sicht);
        }
        finally
        {
            Schilder.AltFuerTests.Value = false;
        }
    }

    [Fact]
    public void ZweiPersonenNebeneinander_NamenGetrennt()
    {
        // wie Bitweiche: zwei Punkte dicht uebereinander, lange Namen
        var p1 = new K(100, 100, 12, 12);
        var p2 = new K(100, 108, 12, 12);
        var schilder = new List<S> { new("Jonas Pieper", p1, 70, 15, false), new("Tim Becker", p2, 62, 15, false) };
        var sperren = new List<K> { p1, p2 };
        AlleFreiOhneUeberdeckung(Schilder.Setzen(schilder, sperren, Sicht), sperren);
        var alt = Alt(schilder, sperren);
        Assert.True(alt[0].Lage.Ueberdeckung(alt[1].Lage) > 0, "Gegenprobe: alte Zeichnung muesste ueberdecken");
    }

    [Fact]
    public void NameAmRechtenRand_RueckenNachLinks_Ganz()
    {
        // wie Serverfarm am Tablet: Punkt nahe am rechten Rand, rechts passt der Name nicht ganz hinein
        var p = new K(360, 150, 12, 12);
        var schilder = new List<S> { new("Dr. Selma Arendt", p, 95, 15, false) };
        var g = Schilder.Setzen(schilder, [p], Sicht);
        AlleFreiOhneUeberdeckung(g, [p]);
        Assert.True(g[0].Lage.Rechts <= p.X, "Name steht links vom Punkt");
        Assert.False(Alt(schilder, [p])[0].Frei, "Gegenprobe: alte Zeichnung ragt ueber den Rand");
    }

    [Fact]
    public void BeschriftungZuLangFuersStueck_ganzDaneben()
    {
        var tisch = new K(50, 50, 50, 30);
        var schreibtisch = new K(60, 55, 80, 20);
        var schilder = new List<S> { new("Schreibtisch", schreibtisch, 64, 14, true), new("Teeküche-Tisch", tisch, 80, 14, true) };
        var g = Schilder.Setzen(schilder, [], Sicht);
        AlleFreiOhneUeberdeckung(g, []);
        Assert.True(g[0].ImAnker, "Schreibtisch passt ins eigene Stueck");
        Assert.False(g[1].ImAnker, "zu lang fuer den Tisch: Schild daneben");
        Assert.Equal(80, g[1].Lage.Breite);
        var alt = Alt(schilder, []);
        Assert.True(alt.Any(a => !a.Frei), "Gegenprobe: alte Zeichnung kuerzt oder ueberdeckt");
    }

    [Fact]
    public void RaumnameGesperrt_NameWeichtAus()
    {
        var p = new K(100, 100, 12, 12);
        var raumname = new K(115, 90, 120, 30);   // liegt rechts am Punkt
        var g = Schilder.Setzen([new("Karin Albers", p, 70, 15, false)], [p, raumname], Sicht);
        AlleFreiOhneUeberdeckung(g, [p, raumname]);
        Assert.True(g[0].Lage.Rechts <= p.X + 0.5, "weicht nach links aus");
    }

    [Fact]
    public void VieleNamenAufEngemRaum_alleFrei()
    {
        var schilder = new List<S>();
        var sperren = new List<K>();
        for (var i = 0; i < 8; i++)
        {
            var p = new K(150 + (i % 2 * 20), 100 + (i * 10), 10, 10);
            sperren.Add(p);
            schilder.Add(new("Person Nummer " + i, p, 90, 15, false));
        }
        AlleFreiOhneUeberdeckung(Schilder.Setzen(schilder, sperren, Sicht), sperren);
        Assert.Contains(Alt(schilder, sperren), a => !a.Frei);
    }
}
