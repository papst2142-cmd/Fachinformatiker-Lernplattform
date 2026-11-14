using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Tests;

/// <summary>
/// Weisung P12-197: beim Ansprechen einer Person im Innenraum (Tippen auf die Person, die Figur geht bis auf
/// <see cref="Wege.Reichweite"/> heran) durchdringen sich Spielfigur und Person nicht. Zwei stehende Figuren reichen
/// waagrecht hoechstens <see cref="StadtSpiel.FigurHalbmesser"/> von ihrer Mitte; Mitte zu Mitte muessen also
/// mindestens zwei Halbmesser plus 0,02 Spielraum (wie <see cref="SpielSitzenTests"/>) frei bleiben.
/// </summary>
public class SpielAnsprechenTests
{
    /// <summary>Groesster waagrechter Abstand einer Ecke der stehenden Figur von ihrer Mitte (Hoehe FigurHoehe).</summary>
    private static float Halbmesser(Netz netz)
    {
        var e = netz.Ecken;
        var r = 0f;
        for (var i = 0; i < e.Count / Netz.Zahlen; i++)
        {
            r = MathF.Max(r, new Vector2(e[i * Netz.Zahlen], e[(i * Netz.Zahlen) + 2]).Length());
        }
        return r * Innenraum.FigurHoehe;
    }

    private static double Grenze => (2 * StadtSpiel.FigurHalbmesser) + 0.02;

    [Fact]
    public void Halbmesser_JederFigur_HoechstensDerFesteWert()
    {
        var grafik = SpielGrafikTests.Bibliothek.Value;
        foreach (var (_, innen) in SpielSitzenTests.AlleRaeume())
        {
            foreach (var p in innen.Personen)
            {
                Assert.True(Halbmesser(Figur.Netz(grafik, p.Aussehen, 1f)) <= StadtSpiel.FigurHalbmesser, innen.Ansicht + " " + p.Id);
            }
        }
        Assert.True(StadtSpiel.AnsprechAbstand >= Grenze);
    }

    /// <summary>Alle stehenden Personen aller Innenraeume: der Weg vom Eingang endet mindestens <see cref="Grenze"/> vor der Person.</summary>
    [Fact]
    public void JedePerson_VomEingangAngesprochen_OhneDurchdringen()
    {
        var geprueft = 0;
        foreach (var (stand, innen) in SpielSitzenTests.AlleRaeume())
        {
            foreach (var p in innen.Personen.Where(p => innen.Sitz(p) is null))
            {
                var weg = innen.Weg(innen.StartX, innen.StartZ, p.X, p.Z, Wege.Reichweite);
                var (x, z) = weg.Count > 0 ? weg[^1] : (innen.StartX, innen.StartZ);
                var a = Vector2.Distance(new Vector2((float)x, (float)z), new Vector2(p.X, p.Z));
                Assert.True(a >= Grenze, $"{stand} {innen.Ansicht} {p.Id}: {a}");
                geprueft++;
            }
        }
        Assert.True(geprueft >= 25, "geprueft: " + geprueft);
    }

    /// <summary>
    /// Ein Questgeber gleich neben dem Eingang des Moebelhauses: die Figur steht beim Betreten zu nah und tritt beim
    /// Ansprechen zurueck (Gegenprobe Stand A 0.65.0: sie bleibt stehen und durchdringt die Person).
    /// </summary>
    [Fact]
    public void QuestgeberAmEingang_FigurTrittZurueck_Gegenprobe()
    {
        var fassung = StadtbauFassungTests.Fassung0.Value;
        var leer = Innenraum.Bauen("geschaeft:moebelhaus", new Spielablauf(SpielInhaltTests.Inhalt).Stand, SpielInnenraumTests.Innenraeume.Value, null)!;
        var (qx, qz) = new[] { (0.0, 0.6), (0.6, 0.0), (-0.6, 0.0), (0.0, -0.6) }
            .Select(d => (leer.StartX + d.Item1, leer.StartZ + d.Item2)).First(p => Wege.KannStehen(p.Item1, p.Item2, leer.Lauf));
        var abschnitte = SpielQuestTests.Abschnitte();
        var ulla = SpielDaten.Objekt(SpielDaten.Liste(abschnitte[Questebene.AbschnittQuestgeber])[0]);
        Py.Setzen(ulla, "platz", new PyObjekt { { "ort", "moebelhaus" }, { "etage", 0L }, { "x", qx }, { "y", qz }, { "blick", 0L } });
        var stadtdaten = SpielQuestTests.Stadtdaten(abschnitte);
        var inhalt = Questebene.Anwenden(fassung.MitKarte(SpielInhaltTests.Inhalt), stadtdaten, fassung.Innenraeume);
        var stadt = new Stadt(inhalt, stadtdaten) { Fassung = fassung };

        double Ansprechen()
        {
            var spiel = new StadtSpiel(stadt, SpielQuestTests.AllesErledigt(inhalt), SpielGrafikTests.Bibliothek.Value, SpielInnenraumTests.Innenraeume.Value,
                SpielGeschaefteTests.Daten.Value, null, () => null, () => Jahreszeit.Sommer);
            Assert.Equal(StadtGrund.Ok, spiel.GeheZu("moebelhaus"));
            spiel.SofortAnkommen();
            if (!spiel.KannBetreten)
            {
                Assert.Equal(StadtGrund.Ok, spiel.Warten());
            }
            Assert.True(spiel.Betreten());
            var p = spiel.Innen!.Personen.Single(x => x.Id == "ulla");
            spiel.GeheZu(p.X, p.Z);
            spiel.SofortAnkommen();
            Assert.Contains("Ulla", spiel.Zeile);
            return Math.Sqrt(Math.Pow(spiel.FigurX - p.X, 2) + Math.Pow(spiel.FigurZ - p.Z, 2));
        }

        Assert.True(Ansprechen() >= Grenze);
        try
        {
            StadtSpiel.AnsprechenAltFuerTests(true);
            Assert.True(Ansprechen() < Grenze);
        }
        finally
        {
            StadtSpiel.AnsprechenAltFuerTests(false);
        }
    }

    /// <summary>
    /// Weisung P12-253 (Bild 11): ein Tipp auf den Boden dicht neben einer stehenden Person (0,8 in vier Richtungen, Wege sperrt
    /// nur 0,5 um die Person) laesst die Figur mindestens <see cref="Grenze"/> vor der Person stehen. Gegenprobe Stand A 0.75.0:
    /// ohne Zuruecktreten bleibt sie wenigstens einmal naeher stehen (dort durchdringen sich beide, 0,76: 226 Ecken).
    /// </summary>
    [Fact]
    public void BodenNebenPerson_FigurBleibtAufAbstand_Gegenprobe()
    {
        var inhalt = SpielInhaltTests.Inhalt;
        var stadt = new Stadt(inhalt, SpielStadtTests.Daten.Value);

        List<double> Abstaende()
        {
            var liste = new List<double>();
            foreach (var ((dx, dz), ort) in new[] { (0.0, 0.8), (0.8, 0.0), (-0.8, 0.0), (0.0, -0.8) }.SelectMany(d => new[] { "moebelhaus", "elektromarkt", "bitweiche" }.Select(o => (d, o))))
            {
                var spiel = new StadtSpiel(stadt, SpielQuestTests.AllesErledigt(inhalt), SpielGrafikTests.Bibliothek.Value, SpielInnenraumTests.Innenraeume.Value,
                    SpielGeschaefteTests.Daten.Value, null, () => null, () => Jahreszeit.Sommer);
                Assert.Equal(StadtGrund.Ok, spiel.GeheZu(ort));
                spiel.SofortAnkommen();
                if (!spiel.KannBetreten)
                {
                    Assert.Equal(StadtGrund.Ok, spiel.Warten());
                }
                Assert.True(spiel.Betreten(), ort);
                foreach (var p in spiel.Innen!.Personen.Where(p => spiel.Innen.Sitz(p) is null))
                {
                    spiel.GeheZu(p.X + dx, p.Z + dz);
                    spiel.SofortAnkommen();
                    liste.Add(spiel.Innen.Personen.Min(q => Math.Sqrt(Math.Pow(spiel.FigurX - q.X, 2) + Math.Pow(spiel.FigurZ - q.Z, 2))));
                }
            }
            return liste;
        }

        var neu = Abstaende();
        Assert.True(neu.Count >= 4, "geprueft: " + neu.Count);
        Assert.All(neu, a => Assert.True(a >= Grenze, $"nur {a:0.00} vor einer Person"));
        try
        {
            StadtSpiel.BodenAltFuerTests(true);
            Assert.Contains(Abstaende(), a => a < Grenze);
        }
        finally
        {
            StadtSpiel.BodenAltFuerTests(false);
        }
    }
}
