using System.Text.Json.Nodes;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.1.0 (Sammelliste 409 „Gaenge zu eng“, Weisung P12-328): Blickfeld der Ich-Sicht (Regler 60 bis 110 Grad, Vorgabe 90,
/// normale Kamera unveraendert) und Massstab innen 1 Einheit = 0,75 m. Nicos Ziel: Diele der Wohnung mindestens 1,10 m und
/// Bueroflur mindestens 1,40 m breit; die Gegenprobe mit dem alten Massstab (0,6 m) muss scheitern.
/// </summary>
public class SpielBlickfeldTests
{
    private static JsonNode Lesen(string datei) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Pfade.Wurzel, "inhalte", "spiel", datei)))!;

    /// <summary>Breite (Grundriss-Einheiten) aller Dielen der Wohnungen.</summary>
    private static List<(string Wo, double Breite)> Dielen() =>
        Lesen("wohnungen.json")["wohnungen"]!.AsArray()
            .Select(w => ((string)w!["id"]!, (double)w["gebaeude"]!["flur"]!["h"]!)).ToList();

    /// <summary>Breite (Grundriss-Einheiten) der Bueroflure: Bitweiche und jede Stufe der eigenen Firma und Filiale.</summary>
    private static List<(string Wo, double Breite)> Bueroflure()
    {
        var liste = new List<(string, double)> { ("bitweiche", (double)Lesen("gebaeude.json")["flur"]!["h"]!) };
        var firma = Lesen("firma.json");
        foreach (var teil in new[] { "gebaeude", "filiale" })
        {
            var stufen = firma[teil]!["stufen"]!.AsArray();
            for (var i = 0; i < stufen.Count; i++)
            {
                liste.Add(($"{teil} Stufe {i}", (double)stufen[i]!["gebaeude"]!["flur"]!["h"]!));
            }
        }
        return liste;
    }

    private static List<string> ZuSchmal(List<(string Wo, double Breite)> gaenge, float meter, double mindestens) =>
        gaenge.Where(g => g.Breite / meter < mindestens - 1e-9).Select(g => $"{g.Wo} {g.Breite / meter:0.###} m").ToList();

    [Fact]
    public void Massstab_DieleUndBueroflur_BreitGenug_GegenprobeAlterMassstabRot()
    {
        Assert.Equal(1f / 0.75f, Innenraum.Meter, 5);
        var dielen = Dielen();
        var flure = Bueroflure();
        Assert.Equal(4, dielen.Count);           // alle vier Wohnungsarten
        Assert.True(flure.Count >= 3);
        // Nicos Ziel mit dem neuen Massstab
        Assert.Empty(ZuSchmal(dielen, Innenraum.Meter, 1.10));
        Assert.Empty(ZuSchmal(flure, Innenraum.Meter, 1.40));
        // Gegenprobe: mit 0,6 m je Einheit (bis A 1.0.0) waeren beide zu schmal (Diele 0,90 m, Bitweiche 1,20 m)
        Assert.Equal(dielen.Count, ZuSchmal(dielen, Innenraum.MeterBisA100, 1.10).Count);
        Assert.Contains(ZuSchmal(flure, Innenraum.MeterBisA100, 1.40), s => s.StartsWith("bitweiche", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(60f)]
    [InlineData(90f)]
    [InlineData(110f)]
    public void IchSicht_WaagrechtesBlickfeld_WieEingestellt_NormaleKameraBleibt(float grad)
    {
        var k = new Kamera(0, 0, 8) { IchBlickfeld = grad };
        // normale Kamera: 40 Grad senkrecht, unabhaengig vom Regler
        var normal = k.Projektion(1600, 1000);
        Assert.Equal(1f / MathF.Tan(20f * MathF.PI / 180f), normal.M22, 3);
        // ganz in der Ich-Sicht: waagrecht genau das eingestellte Blickfeld (quer, PC 16:10 und Handy 20:9)
        k.IchAnteil = 1f;
        foreach (var (b, h) in new[] { (1600f, 1000f), (2400f, 1080f) })
        {
            var p = k.Projektion(b, h);
            Assert.Equal(1f / MathF.Tan(grad * MathF.PI / 360f), p.M11, 3);
        }
        // Hochformat: das Blickfeld gilt fuer die lange (senkrechte) Seite, nie weiter
        var hoch = k.Projektion(1080, 2400);
        Assert.Equal(1f / MathF.Tan(grad * MathF.PI / 360f), hoch.M22, 3);
        // auf halbem Weg liegt es dazwischen
        k.IchAnteil = 0.5f;
        var halb = k.SichtfeldJetzt(1.6f);
        Assert.Equal(40f + ((Kamera.IchSenkrecht(grad, 1.6f) - 40f) * 0.5f), halb, 3);
    }

    [Fact]
    public void IchSicht_Blickfeld_Bereich_UndUnterschiedlich()
    {
        var k = new Kamera(0, 0, 8) { IchAnteil = 1f };
        Assert.Equal(90f, k.IchBlickfeld);
        k.IchBlickfeld = 20f;
        Assert.Equal(60f, k.IchBlickfeld);
        k.IchBlickfeld = 170f;
        Assert.Equal(110f, k.IchBlickfeld);
        // 60, 90 und 110 ergeben je ein anderes Bild
        var m = new[] { 60f, 90f, 110f }.Select(g => { k.IchBlickfeld = g; return k.Projektion(1600, 1000).M11; }).ToList();
        Assert.Equal(3, m.Distinct().Count());
        Assert.True(m[0] > m[1] && m[1] > m[2]);
    }

    [Fact]
    public void Regler_Speichern_Lesen_Vorgabe_UndKaputteWerte()
    {
        Assert.Equal("ich_blickfeld", Blickfeldbedienung.Schluessel);
        Assert.Equal((60, 90, 110, 5), (Blickfeldbedienung.Min, Blickfeldbedienung.Vorgabe, Blickfeldbedienung.Max, Blickfeldbedienung.Schritt));
        var werte = new JsonObject();
        Assert.Equal(90, Blickfeldbedienung.Lesen(werte));
        Blickfeldbedienung.Wert(werte, 105);
        Assert.Equal(105, (int)werte["ich_blickfeld"]!);
        Assert.Equal(105, Blickfeldbedienung.Lesen(JsonNode.Parse(werte.ToJsonString())!.AsObject()));
        Blickfeldbedienung.Wert(werte, 90);           // Vorgabe = kein Eintrag
        Assert.False(werte.ContainsKey("ich_blickfeld"));
        Blickfeldbedienung.Wert(werte, 200);          // ausserhalb: auf den Rand
        Assert.Equal(110, (int)werte["ich_blickfeld"]!);
        foreach (var kaputt in new[] { """{"ich_blickfeld":"weit"}""", """{"ich_blickfeld":30}""", """{"ich_blickfeld":500}""", """{"ich_blickfeld":true}""", """{"ich_blickfeld":null}""" })
        {
            Assert.Equal(90, Blickfeldbedienung.Lesen(JsonNode.Parse(kaputt)!.AsObject()));
        }
        Assert.Equal(75, Blickfeldbedienung.Lesen(JsonNode.Parse("""{"ich_blickfeld":73}""")!.AsObject()));
        using (Blickfeldbedienung.FuerTests(65))
        {
            Assert.Equal(65, Blickfeldbedienung.Grad);
        }
    }
}
