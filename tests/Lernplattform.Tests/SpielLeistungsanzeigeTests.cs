using System.Text.Json.Nodes;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>Welle D2 (Weisung P12-157 Frage 5 = a): Leistungsanzeige im Spiel, Vorgabe aus, Zeile mit drei Werten.</summary>
public sealed class SpielLeistungsanzeigeTests
{
    [Fact]
    public void Vorgabe_Aus()
    {
        Assert.False(Leistungsanzeige.Lesen(new JsonObject()));
        Assert.False(Leistungsanzeige.Lesen(new JsonObject { [Leistungsanzeige.Schluessel] = "true" }));
        Assert.False(Leistungsanzeige.Lesen(new JsonObject { [Leistungsanzeige.Schluessel] = 1 }));
        var werte = new JsonObject();
        Leistungsanzeige.Wert(werte, true);
        Assert.True(Leistungsanzeige.Lesen(werte));
        Leistungsanzeige.Wert(werte, false);
        Assert.False(Leistungsanzeige.Lesen(werte));
    }

    [Fact]
    public void Zeile_BilderDreieckeAufrufe()
    {
        Assert.Equal("60 Bilder/s · 52.957 Dreiecke · 17 Aufrufe", Leistungsanzeige.Zeile(60, 52957, 17));
        Assert.Equal("0 Bilder/s · 0 Dreiecke · 0 Aufrufe", Leistungsanzeige.Zeile(0, 0, 0));
        Assert.Equal("30 Bilder/s · 1.140.581 Dreiecke · 28 Aufrufe", Leistungsanzeige.Zeile(30, 1140581, 28));
    }
}
