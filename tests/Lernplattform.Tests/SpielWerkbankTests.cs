using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.0.0 (Sammelliste 357, Werkstatt Teil 2): was Werkbank- und Regal-Ansicht anzeigen. Nicos Ziel „RAM frei
/// beachten“: im Entwurf P12-194 stand „RAM frei: 3“ bei leerer Bank; frei ist immer Baenke minus eingesetzte Module, ohne
/// RAM also alle Baenke, ohne Mainboard gibt es keine Zahl. Die Bedienung (Teil antippen, Ziel, Ablegen, Regal, Liste)
/// prueft der Start-Test (<c>AutoMessung.P7W6</c>, Ansichten spiel/ticket-bauteile-*).
/// </summary>
public sealed class SpielWerkbankTests
{
    private static PyObjekt I => SpielInhaltTests.Inhalt;

    private static PyObjekt T(string id) => SpielDaten.Teil(id, I)!;

    [Fact]
    public void RamFrei_BaenkeMinusModule_LeereBankZaehltMit_Gegenprobe()
    {
        // ohne Mainboard keine Baenke
        Assert.Null(Werkbank.Ram(null, T("ram_ddr5_2x8")));
        Assert.Equal("RAM-Bänke: erst ein Mainboard einsetzen", Werkbank.RamText(null, null));
        // ATX mit 4 Baenken: ohne RAM alle 4 frei (Entwurf zeigte faelschlich 3)
        Assert.Equal((4L, 0L, 4L, false), Werkbank.Ram(T("mb_b650_atx"), null));
        Assert.Equal("RAM frei: 4 von 4", Werkbank.RamText(T("mb_b650_atx"), null));
        Assert.NotEqual("RAM frei: 3 von 4", Werkbank.RamText(T("mb_b650_atx"), null));
        // 2 Module: 2 frei; 4 Module: 0 frei
        Assert.Equal("RAM frei: 2 von 4", Werkbank.RamText(T("mb_b650_atx"), T("ram_ddr5_2x8")));
        Assert.Equal("RAM frei: 0 von 4", Werkbank.RamText(T("mb_b760_matx"), T("ram_ddr4_4x8")));
        // mehr Module als Baenke: klar gesagt, nie eine negative Zahl
        Assert.Equal((2L, 2L, 0L, true), Werkbank.Ram(T("mb_a620_matx"), T("ram_ddr4_4x8")));
        Assert.Equal("RAM passt nicht: 4 Module, nur 2 Bänke", Werkbank.RamText(T("mb_a620_matx"), T("ram_ddr4_4x8")));
        // Kombi aus zwei Kits zaehlt alle Module
        var kombi = SpielDaten.Teil("ram_ddr5_2x8" + SpielDaten.Kombi + "2", I);
        Assert.NotNull(kombi);
        Assert.Equal("RAM frei: 0 von 4", Werkbank.RamText(T("mb_b650_atx"), kombi));
    }

    [Fact]
    public void PlatzUndFehlt_NurNachTyp_OptionalNieFehlend()
    {
        string[] plaetze = ["mainboard", "cpu", "ram", "ssd", "gpu", "netzteil", "gehaeuse"];
        Assert.Equal("ram", Werkbank.PlatzFuer("ram_ddr5_2x8", plaetze, I));
        Assert.Equal("mainboard", Werkbank.PlatzFuer("mb_b650_atx", plaetze, I));
        Assert.Null(Werkbank.PlatzFuer("mb_b650_atx", ["cpu", "ram"], I));
        Assert.Null(Werkbank.PlatzFuer("gibt_es_nicht", plaetze, I));
        var antwort = new Dictionary<string, object?> { ["mainboard"] = "mb_b650_atx", ["ram"] = "ram_ddr5_2x8" };
        Assert.Equal(["cpu", "ssd", "netzteil", "gehaeuse"], Werkbank.Fehlt(plaetze, antwort, ["gpu"]));
    }

    [Fact]
    public void Regal_FaecherNachTyp_InReihenfolgeDerHardware_UnbekanntesWeg()
    {
        var lager = new PyDict
        {
            ["ram_ddr4_1x8"] = 3L,
            ["mb_b650_atx"] = 1L,
            ["ram_ddr5_2x16"] = 2L,
            ["gibt_es_nicht"] = 5L,
        };
        var regal = Werkbank.Regal(lager, I);
        Assert.Equal(["mainboard", "ram"], regal.Select(f => f.Typ));
        Assert.Equal("Arbeitsspeicher", regal[1].Name);
        Assert.Equal([("ram_ddr4_1x8", 3L), ("ram_ddr5_2x16", 2L)], regal[1].Teile.Select(t => (Py.Str(t.Kennung), t.Anzahl)));
        Assert.Empty(Werkbank.Regal(new PyDict(), I));
    }
}
