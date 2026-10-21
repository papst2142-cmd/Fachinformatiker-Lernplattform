using System.Text.Json;
using Lernplattform.Core.Design;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L12 (Sammelliste 14, Weisung P2-14): Reglerspur und Knopf exakt wie Python
/// (G23, erzeugt von werkzeuge/vergleich/erzeuge_reglerspur.py). Kontrollfehler F11:
/// (a) Spur rechnet mit falschem Kanal, (b) Knopffarben vertauscht, (c) Farbton ohne Kreis.
/// </summary>
public class ReglerspurTests
{
    private static readonly JsonElement G23 = Lade();

    private static JsonElement Lade()
    {
        using var f = File.OpenRead(Pfade.Vergleich("reglerspur.json"));
        return JsonDocument.Parse(f).RootElement.Clone();
    }

    private static double?[] Werte(JsonElement liste) =>
        [.. liste.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.Null ? (double?)null : v.GetDouble())];

    private static string[] Texte(JsonElement liste) => [.. liste.EnumerateArray().Select(v => v.GetString()!)];

    [Fact]
    public void KonstantenWiePython()
    {
        // Python ist die Vorgabe (G23); die Konstanten in C# muessen gleich sein
        Assert.True(G23.GetProperty("knopf_hell").GetString() == Reglerspur.KnopfHell, "KNOB_LIGHT");
        Assert.True(G23.GetProperty("knopf_dunkel").GetString() == Reglerspur.KnopfDunkel, "KNOB_DARK");
        Assert.True(G23.GetProperty("knopf_mindestkontrast").GetDouble() == Reglerspur.KnopfMindestkontrast, "KNOB_MIN_CONTRAST");
        var kanaele = G23.GetProperty("kanaele").EnumerateArray().ToList();
        Assert.Equal(3, kanaele.Count);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(kanaele[i][0].GetString(), EigeneFarben.Kanaele[i].Id);
            Assert.Equal(kanaele[i][2].GetInt32(), EigeneFarben.Kanaele[i].Min);
            Assert.Equal(kanaele[i][3].GetInt32(), EigeneFarben.Kanaele[i].Max);
        }
    }

    [Fact]
    public void SpurenWiePython()
    {
        var anzahl = 0;
        foreach (var spur in G23.GetProperty("spuren").EnumerateArray())
        {
            var werte = Werte(spur.GetProperty("werte"));
            foreach (var kanal in spur.GetProperty("kanaele").EnumerateObject())
            {
                var k = kanal.Value;
                Assert.Equal(Texte(k.GetProperty("stuetzen")), Reglerspur.Stuetzfarben(werte, kanal.Name));
                Assert.Equal(Texte(k.GetProperty("elf")), Reglerspur.Farben(werte, kanal.Name, 11));
                Assert.Equal(k.GetProperty("stelle").GetDouble(), Reglerspur.Stelle(werte, kanal.Name));
                var knopf = Texte(k.GetProperty("knopf"));
                Assert.Equal((knopf[0], knopf[1]), Reglerspur.Knopf(werte, kanal.Name));
                Assert.Equal(k.GetProperty("kontrast").GetDouble(), Reglerspur.KnopfKontrast(werte, kanal.Name));
                anzahl++;
            }
        }
        Assert.Equal(32 * 3, anzahl);
    }

    [Fact]
    public void KnopfRasterWiePython()
    {
        var kleinster = 99.0;
        foreach (var zeile in G23.GetProperty("raster").EnumerateArray())
        {
            var kanal = zeile.GetProperty("kanal").GetString()!;
            var h = zeile.GetProperty("farbton").GetInt32();
            var s = zeile.GetProperty("saettigung").GetInt32();
            var erwartet = zeile.GetProperty("knopf").GetString()!;
            var ist = string.Concat(Enumerable.Range(0, 21).Select(i =>
            {
                double?[] werte = [h, s, i * 5];
                kleinster = Math.Min(kleinster, Reglerspur.KnopfKontrast(werte, kanal));
                return Reglerspur.Knopf(werte, kanal).Fuellung == Reglerspur.KnopfHell ? "H" : "D";
            }));
            Assert.Equal(erwartet, ist);
        }
        Assert.Equal(G23.GetProperty("raster_kleinster_kontrast").GetDouble(), kleinster);
    }

    [Fact]
    public void FarbweltenStartwerteWiePython()
    {
        var welten = G23.GetProperty("welten").EnumerateArray().ToList();
        Assert.Equal(72, welten.Count);
        foreach (var welt in welten)
        {
            var start = EigeneFarben.Start(welt.GetProperty("darstellung").GetString()!, welt.GetProperty("grundfarbe").GetString()!,
                welt.GetProperty("hintergrund").GetString()!);
            foreach (var teil in welt.GetProperty("teile").EnumerateObject())
            {
                var regler = teil.Name switch
                {
                    EigeneFarben.TeilAkzent1 => start.Akzent1,
                    EigeneFarben.TeilAkzent2 => start.Akzent2,
                    _ => start.Hintergrund,
                };
                Assert.Equal(Werte(welt.GetProperty("start").GetProperty(teil.Name)), Reglerspur.Werte(regler));
                foreach (var kanal in teil.Value.EnumerateObject())
                {
                    var knopf = Texte(kanal.Value.GetProperty("knopf"));
                    Assert.Equal((knopf[0], knopf[1]), Reglerspur.Knopf(Reglerspur.Werte(regler), kanal.Name));
                    Assert.Equal(kanal.Value.GetProperty("kontrast").GetDouble(), Reglerspur.KnopfKontrast(Reglerspur.Werte(regler), kanal.Name));
                }
            }
        }
    }

    [Fact]
    public void GrenzfaelleWiePython()
    {
        foreach (var fall in G23.GetProperty("grenzfaelle").EnumerateArray())
        {
            var werte = Werte(fall.GetProperty("werte"));
            Assert.Equal(Werte(fall.GetProperty("sicher")).Select(v => v!.Value), Reglerspur.Sicher(werte));
            foreach (var kanal in fall.GetProperty("mitte").EnumerateObject())
            {
                Assert.Equal(kanal.Value.GetString(), Reglerspur.Farbe(werte, kanal.Name, 0.5));
            }
            foreach (var kanal in fall.GetProperty("knopf").EnumerateObject())
            {
                Assert.Equal(kanal.Value.GetString(), Reglerspur.Knopf(werte, kanal.Name).Fuellung);
            }
        }
    }

    [Theory]
    [InlineData("h", 359, 1, 0)]
    [InlineData("h", 0, -1, 359)]
    [InlineData("h", 355, 10, 5)]
    [InlineData("h", 3, -10, 353)]
    [InlineData("s", 100, 1, 100)]
    [InlineData("s", 0, -10, 0)]
    [InlineData("l", 95, 10, 100)]
    [InlineData("l", 50, -1, 49)]
    public void SchrittWiePython(string kanal, int wert, int schritt, int erwartet)
    {
        Assert.Equal(erwartet, Reglerspur.Schritt(EigeneFarben.Kanaele.Single(k => k.Id == kanal), wert, schritt));
    }

    [Fact]
    public void FarbtonImKreisSpeichernUndNeuLaden()
    {
        // Weisung P2-14 A3: gespeichert werden weiter nur Farbton 0 bis 359; Einstellungen.cs unveraendert
        using var ordner = new TempOrdner();
        var einstellungen = new Lernplattform.Core.Daten.Einstellungen(ordner.Pfad);
        var h = EigeneFarben.Farbton;
        var werte = new EigeneWerte(new Regler(Reglerspur.Schritt(h, 359, 1), 50, 50), new Regler(Reglerspur.Schritt(h, 0, -1), 60, 40),
            new Regler(Reglerspur.Schritt(h, 355, 10), 20, 10));
        Assert.Equal(new EigeneWerte(new Regler(0, 50, 50), new Regler(359, 60, 40), new Regler(5, 20, 10)), werte);
        var alles = einstellungen.Laden();
        alles[EigeneFarben.Schluessel] = EigeneFarben.SpeicherWert(alles[EigeneFarben.Schluessel]?.DeepClone(), Farbwelt.Dunkel, werte);
        Assert.True(einstellungen.Speichern(alles));
        var neu = new Lernplattform.Core.Daten.Einstellungen(ordner.Pfad).Laden();
        Assert.Equal(werte, EigeneFarben.Gespeichert(neu[EigeneFarben.Schluessel])[Farbwelt.Dunkel]);
    }

    // -- Kontrollfehler F11 ---------------------------------------------------

    [Fact]
    public void Kontrollfehler_FalscherKanal()
    {
        var spur = G23.GetProperty("spuren")[11];   // (210, 45, 50)
        var werte = Werte(spur.GetProperty("werte"));
        var saettigung = Texte(spur.GetProperty("kanaele").GetProperty("s").GetProperty("stuetzen"));
        Assert.Equal(saettigung, Reglerspur.Stuetzfarben(werte, "s"));
        // Wer die Saettigungs-Spur mit dem Helligkeits-Kanal rechnet, liegt daneben
        Assert.NotEqual(saettigung, Reglerspur.Stuetzfarben(werte, "l"));
    }

    [Fact]
    public void Kontrollfehler_KnopfVertauscht()
    {
        double?[] werte = [60, 100, 50];
        var (fuellung, rand) = Reglerspur.Knopf(werte, "l");
        var spur = G23.GetProperty("spuren").EnumerateArray().Single(s => Werte(s.GetProperty("werte")).SequenceEqual(werte));
        var knopf = Texte(spur.GetProperty("kanaele").GetProperty("l").GetProperty("knopf"));
        Assert.Equal((knopf[0], knopf[1]), (fuellung, rand));
        Assert.NotEqual((knopf[1], knopf[0]), (fuellung, rand));
        Assert.NotEqual(fuellung, rand);
    }

    [Fact]
    public void Kontrollfehler_FarbtonOhneKreis()
    {
        // Eine Klemme ohne Kreis gaebe 359 statt 0
        var farbton = EigeneFarben.Farbton;
        Assert.NotEqual(Math.Max(farbton.Min, Math.Min(farbton.Max, 359 + 1)), Reglerspur.Schritt(farbton, 359, 1));
        Assert.Equal(0, Reglerspur.Schritt(farbton, 359, 1));
    }
}
