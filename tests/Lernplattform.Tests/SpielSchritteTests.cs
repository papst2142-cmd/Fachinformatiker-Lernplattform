using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W7 (Weisung P8-14, Sammelliste 125, Nico 1a): Schritte der Figur. Je Bewegung 16 Bilder als eigene
/// Modelle mit denselben Dreiecken wie die stehende Figur; das Bild haengt nur am zurueckgelegten Weg (nie an der
/// Uhr); steht die Figur, zeigt sie die stehende Haltung. In der Stadt Rennen, innen Gehen.
/// </summary>
public sealed class SpielSchritteTests
{
    private static GrafikBibliothek Grafik => SpielGrafikTests.Bibliothek.Value;

    private static readonly PyObjekt Aussehen = new() { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } };

    private static StadtSpiel NeuesSpiel()
    {
        var stadt = new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
        var ablauf = new Spielablauf(stadt.Inhalt);
        ablauf.Profil("Probe", Aussehen, Ereignisse.Normal);
        return new StadtSpiel(stadt, ablauf, null, SpielInnenraumTests.Innenraeume.Value);
    }

    [Fact]
    public void SechzehnBilderJeBewegungWieDieFigur()
    {
        var steht = Grafik.Modell(Figur.Modell)!;
        foreach (var bewegung in new[] { Figur.Gehen, Figur.Rennen })
        {
            var formen = new HashSet<string>();
            for (var i = 0; i < Figur.Bilder; i++)
            {
                var m = Grafik.Modell(Figur.BildName(bewegung, i));
                Assert.NotNull(m);
                Assert.Equal(steht.Dreiecke, m.Dreiecke);
                // gleiche Dreiecke in gleicher Reihenfolge: dieselben Farben
                Assert.Equal(steht.Farben, m.Farben);
                formen.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(m.Ecken.AsSpan()))));
            }
            // jedes Bild ist eine andere Haltung, keines ist die stehende Figur
            Assert.Equal(Figur.Bilder, formen.Count);
            Assert.DoesNotContain(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(steht.Ecken.AsSpan()))), formen);
        }
        Assert.Null(Grafik.Modell(Figur.BildName(Figur.Gehen, Figur.Bilder)));
    }

    /// <summary>Farben (auch Kleidung) und Groesse kommen von der stehenden Figur, die Ecken vom Bild.</summary>
    [Fact]
    public void BildTraegtAussehenUndKleidung()
    {
        IReadOnlyList<Figur.Stueck> kleidung = [new("oberteil", "shirt", "#DC2626"), new("kopf", "kappe", "#2563EB")];
        var steht = Figur.Netz(Grafik, Aussehen, 1f, kleidung);
        Assert.True(steht.AnzahlEcken > 0);
        foreach (var bewegung in new[] { Figur.Gehen, Figur.Rennen })
        {
            var anders = 0;
            for (var i = 0; i < Figur.Bilder; i++)
            {
                var bild = Figur.Netz(Grafik, Aussehen, 1f, kleidung, bewegung, i);
                Assert.Equal(steht.AnzahlEcken, bild.AnzahlEcken);
                Assert.Equal(steht.Indizes.Count, bild.Indizes.Count);
                for (var e = 0; e < steht.Ecken.Count; e += Netz.Zahlen)
                {
                    // Farbe gleich (Stelle 6 bis 8 je Ecke)
                    Assert.Equal(steht.Ecken[e + 6], bild.Ecken[e + 6]);
                    Assert.Equal(steht.Ecken[e + 7], bild.Ecken[e + 7]);
                    Assert.Equal(steht.Ecken[e + 8], bild.Ecken[e + 8]);
                    Assert.InRange(bild.Ecken[e + 1], -0.1f, 1.1f);
                    if (Math.Abs(steht.Ecken[e] - bild.Ecken[e]) > 0.01f || Math.Abs(steht.Ecken[e + 1] - bild.Ecken[e + 1]) > 0.01f)
                    {
                        anders++;
                    }
                }
            }
            Assert.True(anders > 1000, bewegung + ": Haltung bewegt sich nicht (" + anders + ")");
        }
        // ohne Bewegung oder mit unbekanntem Bild: die stehende Figur
        Assert.Equal(steht.Ecken, Figur.Netz(Grafik, Aussehen, 1f, kleidung, null, 3).Ecken);
        Assert.Equal(steht.Ecken, Figur.Netz(Grafik, Aussehen, 1f, kleidung, Figur.Gehen, -1).Ecken);
        Assert.Equal(steht.Ecken, Figur.Netz(Grafik, Aussehen, 1f, kleidung, Figur.Gehen, 99).Ecken);
    }

    [Fact]
    public void BildHaengtNurAmWeg()
    {
        var bilder = new HashSet<int>();
        for (var i = 0; i < 160; i++)
        {
            var weg = i * StadtSpiel.ZyklusStadt / 160 * 10;
            var bild = StadtSpiel.BildNachWeg(weg, StadtSpiel.ZyklusStadt);
            Assert.InRange(bild, 0, Figur.Bilder - 1);
            // gleicher Weg = gleiches Bild, ein Doppelschritt weiter = dasselbe Bild
            Assert.Equal(bild, StadtSpiel.BildNachWeg(weg, StadtSpiel.ZyklusStadt));
            bilder.Add(bild);
        }
        Assert.Equal(Figur.Bilder, bilder.Count);
        Assert.Equal(0, StadtSpiel.BildNachWeg(0, 1.4));
        Assert.Equal(8, StadtSpiel.BildNachWeg(0.7, 1.4));
        Assert.Equal(4, StadtSpiel.BildNachWeg(1.4 * 7.3, 1.4));
        Assert.Equal(15, StadtSpiel.BildNachWeg(-0.01, 1.4));
    }

    /// <summary>Stehend -1, beim Gehen das Bild nach dem Weg; der Weg waechst um genau die gegangene Strecke.</summary>
    [Fact]
    public void FigurGehtUndSteht()
    {
        var spiel = NeuesSpiel();
        Assert.False(spiel.Geht);
        Assert.Equal(-1, spiel.SchrittBild);
        Assert.Equal(Figur.Rennen, spiel.Bewegung);
        Assert.Equal(StadtSpiel.ZyklusStadt, spiel.Zyklus);
        var (x0, z0) = (spiel.FigurX, spiel.FigurZ);
        Assert.True(spiel.GeheZu(x0 + 3, z0));
        Assert.True(spiel.Geht);
        var gesehen = new HashSet<int>();
        var weg = 0.0;
        while (spiel.Geht)
        {
            var (xa, za) = (spiel.FigurX, spiel.FigurZ);
            var vorher = spiel.Schrittweg;
            spiel.Fortschreiten(0.02);
            weg += Math.Sqrt(Math.Pow(spiel.FigurX - xa, 2) + Math.Pow(spiel.FigurZ - za, 2));
            Assert.True(spiel.Schrittweg >= vorher);
            if (spiel.Geht)
            {
                Assert.Equal(StadtSpiel.BildNachWeg(spiel.Schrittweg, StadtSpiel.ZyklusStadt), spiel.SchrittBild);
                gesehen.Add(spiel.SchrittBild);
            }
        }
        Assert.Equal(-1, spiel.SchrittBild);
        // der Weg ist die gegangene Strecke (die Ecken des Wegs zaehlen einzeln, darum hoechstens etwas laenger als gerade)
        Assert.InRange(spiel.Schrittweg, weg - 0.01, weg + 0.01);
        Assert.True(gesehen.Count >= 8, "Bilder beim Gehen: " + gesehen.Count);
    }

    [Fact]
    public void InnenGehen()
    {
        var spiel = NeuesSpiel();
        Assert.Contains(spiel.GeheZu("zuhause"), new[] { StadtGrund.Ok, StadtGrund.SchonDa });
        spiel.SofortAnkommen();
        Assert.True(spiel.Betreten());
        Assert.True(spiel.ImInnenraum);
        Assert.Equal(Figur.Gehen, spiel.Bewegung);
        Assert.Equal(1.5 * Innenraum.FigurHoehe, spiel.Zyklus, 6);
        Assert.Equal(-1, spiel.SchrittBild);
    }
}
