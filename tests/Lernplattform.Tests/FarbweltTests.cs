using System.Text.Json;
using Lernplattform.Core.Design;

namespace Lernplattform.Tests;

/// <summary>
/// Auflage A2: alle 72 Farbwelten exakt gleich wie Python (Hex), Vergleichswerte G6.
/// Jede Abweichung, auch um 1 je Farbkanal, ist ein Fehler.
/// </summary>
public class FarbweltTests
{
    private static JsonElement Welten()
    {
        using var f = File.OpenRead(Pfade.Vergleich("farbwelten.json"));
        return JsonDocument.Parse(f).RootElement.GetProperty("welten").Clone();
    }

    public static TheoryData<int> Faelle()
    {
        var daten = new TheoryData<int>();
        for (int i = 0; i < Welten().GetArrayLength(); i++)
        {
            daten.Add(i);
        }
        return daten;
    }

    [Fact]
    public void Genau72Farbwelten()
    {
        Assert.Equal(72, Welten().GetArrayLength());
        Assert.Equal(6, Farbwelt.Grundfarben.Count);
        Assert.Equal(6, Farbwelt.Hintergruende.Count);
    }

    [Theory]
    [MemberData(nameof(Faelle))]
    public void FarbweltGleichWiePython(int index)
    {
        var soll = Welten()[index];
        var ist = Farbwelt.Berechne(soll.GetProperty("darstellung").GetString()!,
            soll.GetProperty("grundfarbe").GetString()!, soll.GetProperty("hintergrund").GetString()!);
        Assert.Equal(soll.GetProperty("hell").GetBoolean(), ist.Hell);
        var farben = soll.GetProperty("farben").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
        Assert.Equal(farben.Keys.Order(StringComparer.Ordinal), ist.Farben.Keys.Order(StringComparer.Ordinal));
        foreach (var (name, wert) in farben)
        {
            Assert.True(wert == ist.Farben[name], $"{name}: Python {wert}, C# {ist.Farben[name]}");
        }
        foreach (var v in soll.GetProperty("verlaeufe").EnumerateObject())
        {
            Assert.Equal((v.Value[0].GetString()!, v.Value[1].GetString()!), ist.Verlaeufe[v.Name]);
        }
    }
}
