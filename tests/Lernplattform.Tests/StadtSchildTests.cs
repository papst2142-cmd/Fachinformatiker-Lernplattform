using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.55.0 (Weisung P12-121, Nico 1b „alle Schilder weg“, Sammelliste 261): das Teil „schild“ der Hausmodelle (eine
/// Platte ohne Schrift auf der Suedwand, an Geschaeften, Schaltern und Gebaeuden ohne Ort grau) wird nicht mehr gezeichnet.
/// Daten bleiben gleich. Geprueft: genau die Schild-Platten fehlen (Gegenprobe mit dem alten Schild), Tueren, Orte, Laternen,
/// Laufraster und Haeuser sind gleich, und kein Teil ausser der Darstellung braucht die Rollenfarbe des Schilds.
/// </summary>
public sealed class StadtSchildTests
{
    private static StadtSzene Bauen(Spielstand? stand = null) =>
        StadtSzene.Bauen(new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value), stand, SpielGrafikTests.Bibliothek.Value, null, FestDekoTests.Daten.Value);

    /// <summary>Modelle mit Schild (Meldung P12-80): Bitweiche, Gewerbehof, Filiale, Moebelhaus, Elektromarkt, Amt, Bank, Kaufhaus.</summary>
    public static readonly string[] ModelleMitSchild = ["amt", "bank", "bitweiche", "elektromarkt", "filiale", "gewerbehof", "kaufhaus", "moebelhaus"];

    [Fact]
    public void AchtModelleHabenEinSchild_DatenBleiben()
    {
        // Modelle der Karte (orte.json) und die angehaengten (stadt.json neue_modelle), roh gelesen
        static IEnumerable<System.Text.Json.JsonProperty> Modelle(string datei, string feld) =>
            System.Text.Json.JsonDocument.Parse(File.ReadAllText(datei)).RootElement.GetProperty(feld).EnumerateObject();
        var mit = Modelle(Path.Combine(Pfade.Inhalte, "spiel", "orte.json"), "modelle")
            .Concat(Modelle(Path.Combine(Pfade.Wurzel, "inhalte_csharp", "spiel", "stadt.json"), "neue_modelle"))
            .Where(m => m.Value.EnumerateArray().Any(t => t.TryGetProperty("art", out var art) && art.GetString() == "schild"))
            .Select(m => m.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(ModelleMitSchild, mit);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SchildWeg_NurDiePlatten_SonstAllesGleich(bool gespielt)
    {
        var stand = gespielt ? SpielFingerabdruckTests.Gespielt.Value.Stand : null;
        var neu = Bauen(stand);
        var alt = StadtbauFassungTests.MitSchild(() => Bauen(stand));
        // je Schild genau ein Quader weniger (12 Dreiecke), alles andere gleich
        var weg = alt.Teile.Count - neu.Teile.Count;
        Assert.True(weg == (gespielt ? SchilderGespielt : SchilderStart), "Schild-Platten weg: " + weg);
        var neuListe = neu.Teile.ToHashSet();
        var fehlend = alt.Teile.Where(t => !neuListe.Contains(t)).ToList();
        Assert.Equal(weg, fehlend.Count);
        // die fehlenden Teile sind duenne Platten (0,05 tief) auf einer Hauswand
        Assert.All(fehlend, t => Assert.True(MathF.Abs(t.Z2 - t.Z1 - 0.05f) < 1e-4f, $"kein Schild: {t}"));
        Assert.Equal(alt.Tueren, neu.Tueren);
        Assert.Equal(alt.Orte, neu.Orte);
        Assert.Equal(alt.Laternen.Select(l => (l.Nr, l.Ort)), neu.Laternen.Select(l => (l.Nr, l.Ort)));
        Assert.Equal(alt.Haeuser.Select(h => (h.Id, h.Min, h.Max)), neu.Haeuser.Select(h => (h.Id, h.Min, h.Max)));
        for (var s = 0; s < 200; s++)
        {
            for (var z = 0; z < 200; z++)
            {
                Assert.Equal(alt.Laufraster.IstFrei((s, z)), neu.Laufraster.IstFrei((s, z)));
            }
        }
    }

    /// <summary>Platten ohne Spielstand (Bitweiche, Moebelhaus, Elektromarkt, Amt, Bank, Kaufhaus; Gewerbehof erst nach der Gruendung).</summary>
    private const int SchilderStart = 6;

    /// <summary>Platten im gespielten Stand (dazu Gewerbehof und Filiale).</summary>
    private const int SchilderGespielt = 8;
}
