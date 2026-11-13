using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.52.0 (Weisung P12-231 Punkt 5): „Alle beheben, die sicher gehen“ legt eine Tuer, die nicht sauber in der Wand
/// sitzt, auf die naechste glatte Stelle derselben Wand. Das Gebaeude selbst bleibt (kein Fenster wird zur Tuer).
/// Gegenprobe: ohne Behebung bleibt der rote Fund.
/// </summary>
public partial class TuerBuendigTests
{
    [Fact]
    public void AlleBeheben_TuerAufFenster_NaechsteGlatteStelle_GebaeudeBleibt()
    {
        var (p, s) = GesetztMit("Haus", Haus("building-edges-door", b: 5));
        var (hx, hy, _, hh) = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        // auf das Fenster ganz links vorne: rot
        TuerNach(p, s, new Vector2((float)(hx + 0.5), (float)(hy + hh)));
        var fund = Assert.Single(p.PruefenAussen(), f => f.Rot && f.Text.Contains("sitzt nicht sauber in der Wand", StringComparison.Ordinal));
        Assert.Equal(AlleBeheben.Art.Tuer, AlleBeheben.ArtVon(p.AussenStand, fund));
        var gebaeudeVorher = Lernplattform.Core.Daten.PythonJson.Standard(Bauteile.Gebaeude(Aussen.Stadtdaten(p.AussenStand)));
        var tuerVorher = TuerSetzen.Tuer(p.AussenStand, s)!.Value;

        var e = AlleBehebenTests.Suchen(p);
        var b = Assert.Single(e.Behebungen);
        Assert.Equal(AlleBeheben.Art.Tuer, b.Art);
        var schritte = p.SchritteZurueck;
        Assert.True(p.AussenAendern(Texte.AlleBehebenSchritt, a => AlleBeheben.Anwenden(a, e)));
        Assert.Equal(schritte + 1, p.SchritteZurueck);

        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot);
        var tuer = TuerSetzen.Tuer(p.AussenStand, s)!.Value;
        Assert.NotEqual(tuerVorher, tuer);
        // dieselbe Wand (vorne, gleiche Hoehe), das Gebaeude unveraendert (kein Fensterteil wurde zur Tuer)
        Assert.Equal(tuerVorher.Y, tuer.Y, 0.02f);
        Assert.Equal(gebaeudeVorher, Lernplattform.Core.Daten.PythonJson.Standard(Bauteile.Gebaeude(Aussen.Stadtdaten(p.AussenStand))));

        // Gegenprobe: zurueck = wieder rot
        Assert.True(p.Rueckgaengig());
        Assert.Contains(p.PruefenAussen(), f => f.Rot && f.Text.Contains("sitzt nicht sauber in der Wand", StringComparison.Ordinal));
    }
}
