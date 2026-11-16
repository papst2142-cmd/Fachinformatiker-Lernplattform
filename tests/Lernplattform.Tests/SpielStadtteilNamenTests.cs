using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.83.0 (Weisung P12-282 Punkt 2 und 3): Stadtteil-Namen sind gezogene Bereiche nur zur Anzeige. Geprueft wird
/// Nicos Ziel: das Spiel zeigt die Namen beim Betreten (Schild) und auf der Karte; ohne Namen (Stadt 1, Fassung 0) kommt
/// nie ein Schild; jede Regel der Namen ist rot mit gruener Gegenprobe.
/// </summary>
[Collection("Haenger")]
public sealed class SpielStadtteilNamenTests
{
    private static PyObjekt Daten(string json) => (PyObjekt)Py.Lesen(json)!;

    [Fact]
    public void Testkarte_HatSechzehnNamen_JedeKopieEinStadtteil()
    {
        var namen = SpielTestkarteTests.Testkarte.Value.Szene.StadtteilNamen;
        Assert.Equal(16, namen.Count);
        Assert.Equal(16, namen.Select(b => b.Name).Distinct().Count());
        Assert.Equal("Altstadt", Stadtteile.An(namen, 10, 10)?.Name);
        Assert.Equal("Bergfeld", Stadtteile.An(namen, 250, 250)?.Name);
        Assert.Equal("Ostend", Stadtteile.An(namen, 250, 10)?.Name);
        Assert.Equal("Südhang", Stadtteile.An(namen, 10, 250)?.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void StadtOhneNamen_NieEinSchild(int fassung)
    {
        var f = AktiveFassung.LadenNummer(p => File.Exists(Path.Combine(Pfade.Wurzel, p)) ? File.ReadAllBytes(Path.Combine(Pfade.Wurzel, p)) : null, fassung);
        var inhalt = f.MitKarte(SpielInhaltTests.Inhalt);
        var spiel = new StadtSpiel(f.StadtFuer(inhalt), SpielQuestTests.AllesErledigt(inhalt), SpielGrafikTests.Bibliothek.Value, SpielInnenraumTests.Innenraeume.Value);
        Assert.Empty(spiel.Szene.StadtteilNamen);
        Assert.False(Stadtteile.HatNamen(f.Stadtdaten));
        var schild = new StadtteilSchild();
        for (var x = 1f; x < spiel.Szene.Breite; x += 3)
        {
            spiel.HinstellenFuerTests(x, x);
            Assert.Null(schild.Takt(spiel));
        }
    }

    [Fact]
    public void Schild_BeimBetretenEinesAnderenStadtteils_SonstNicht()
    {
        var spiel = SpielTestkarteTests.Testkarte.Value;
        var schild = new StadtteilSchild();
        spiel.HinstellenFuerTests(10, 10);
        Assert.Equal("Altstadt", schild.Takt(spiel));
        // gleicher Stadtteil: kein neues Schild
        spiel.HinstellenFuerTests(40, 30);
        Assert.Null(schild.Takt(spiel));
        // ueber die Grenze nach rechts: Nordviertel (Spalte 2, Reihe 1)
        spiel.HinstellenFuerTests(70, 30);
        Assert.Equal("Nordviertel", schild.Takt(spiel));
        Assert.Null(schild.Takt(spiel));
        // zurueck: wieder ein Schild
        spiel.HinstellenFuerTests(60, 30);
        Assert.Equal("Altstadt", schild.Takt(spiel));
        // eine Reihe tiefer: Lindenhof
        spiel.HinstellenFuerTests(60, 70);
        Assert.Equal("Lindenhof", schild.Takt(spiel));
        Assert.Equal("Lindenhof", schild.Zuletzt);
    }

    [Fact]
    public void Schild_InsUnbenannte_KeinSchild_ZurueckWiederEins()
    {
        var daten = Daten("{\"stadtteile\":[{\"name\":\"Mitte\",\"x1\":10,\"x2\":20,\"z1\":10,\"z2\":20}]}");
        var bereiche = Stadtteile.Bereiche(daten);
        Assert.Single(bereiche);
        Assert.Null(Stadtteile.An(bereiche, 5, 5));
        Assert.Equal("Mitte", Stadtteile.An(bereiche, 10, 10)?.Name);
        // rechts und unten nicht eingeschlossen (wie eine Kachel)
        Assert.Null(Stadtteile.An(bereiche, 20, 15));
        Assert.Null(Stadtteile.An(bereiche, 15, 20));
    }

    public static TheoryData<string, string> Rote => new()
    {
        { "{\"stadtteile\":{}}", "keine Liste" },
        { "{\"stadtteile\":[{\"name\":\"A\",\"x1\":0,\"x2\":8,\"z1\":0}]}", "falsch gespeichert" },
        { "{\"stadtteile\":[{\"name\":\"A\",\"x1\":0,\"x2\":8,\"z1\":0,\"z2\":8,\"farbe\":1}]}", "falsch gespeichert" },
        { "{\"stadtteile\":[{\"name\":\"  \",\"x1\":0,\"x2\":8,\"z1\":0,\"z2\":8}]}", "keinen Namen" },
        { "{\"stadtteile\":[{\"name\":\" Mitte\",\"x1\":0,\"x2\":8,\"z1\":0,\"z2\":8}]}", "Leerzeichen" },
        { "{\"stadtteile\":[{\"name\":\"ABCDEFGHIJKLMNOPQRSTUVWXY\",\"x1\":0,\"x2\":8,\"z1\":0,\"z2\":8}]}", "länger als 24" },
        { "{\"stadtteile\":[{\"name\":\"Mitte\",\"x1\":0,\"x2\":8,\"z1\":0,\"z2\":8},{\"name\":\"mitte\",\"x1\":10,\"x2\":18,\"z1\":0,\"z2\":8}]}", "tragen Stadtteil Nr. 1 und Nr. 2" },
        { "{\"stadtteile\":[{\"name\":\"A\",\"x1\":0.25,\"x2\":8,\"z1\":0,\"z2\":8}]}", "Raster 0,5" },
        { "{\"stadtteile\":[{\"name\":\"A\",\"x1\":60,\"x2\":68,\"z1\":0,\"z2\":8}]}", "über den Rand" },
        { "{\"stadtteile\":[{\"name\":\"A\",\"x1\":0,\"x2\":3.5,\"z1\":0,\"z2\":8}]}", "kleiner als 4" },
        { "{\"stadtteile\":[{\"name\":\"A\",\"x1\":0,\"x2\":8,\"z1\":0,\"z2\":8},{\"name\":\"B\",\"x1\":7.5,\"x2\":16,\"z1\":0,\"z2\":8}]}", "überdecken sich" },
    };

    [Theory]
    [MemberData(nameof(Rote))]
    public void Regeln_Rot(string json, string teil)
    {
        var fehler = Stadtteile.Fehler(Daten(json), 64, 64);
        Assert.Contains(fehler, f => f.Text.Contains(teil, StringComparison.Ordinal));
    }

    [Fact]
    public void Regeln_Gruen_GrenzfaelleUndTestkarte()
    {
        // genau 24 Zeichen, genau 4 x 4, Kante an Kante, bis zum Rand: alles erlaubt
        var gut = Daten("{\"stadtteile\":[{\"name\":\"ABCDEFGHIJKLMNOPQRSTUVWX\",\"x1\":0,\"x2\":4,\"z1\":0,\"z2\":4},"
            + "{\"name\":\"Am Rand\",\"x1\":4,\"x2\":64,\"z1\":0,\"z2\":64.0},{\"name\":\"Süd\",\"x1\":0,\"x2\":4,\"z1\":4,\"z2\":63.5}]}");
        Assert.Empty(Stadtteile.Fehler(gut, 64, 64));
        Assert.Equal(3, Stadtteile.Bereiche(gut).Count);
        Assert.Empty(Stadtteile.Fehler(Daten("{\"stadtteile\":[]}"), 64, 64));
        Assert.Empty(Stadtteile.Fehler(Daten("{}"), 64, 64));
        var f = StadtFassung.Lesen(File.ReadAllBytes(Path.Combine(Pfade.Wurzel, "tests/Lernplattform.Tests/Testdaten/testkarte_256.fistadt")));
        Assert.Empty(Stadtteile.Fehler(f.Stadtdaten, 256, 256));
    }

    [Fact]
    public void Daten_HinUndZurueck_GleicherEintrag()
    {
        var b = new Stadtteile.Bereich("Neustadt", 1.5, 2, 9, 12.5);
        var liste = new List<object?> { b.AlsDaten() };
        var daten = new PyObjekt { { Stadtteile.AbschnittNamen, liste } };
        Assert.Equal([b], Stadtteile.Bereiche(daten));
        Assert.Empty(Stadtteile.Fehler(daten, 64, 64));
    }
}
