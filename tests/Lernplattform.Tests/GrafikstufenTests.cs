using System.Text.Json.Nodes;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle Grafik G1 (Weisung P12-182): Stufen, Werte je Stufe, Merker, feste Tabelle des ersten Starts (je Zeile ein Fall
/// mit Gegenprobe), Belastung und Texte. Mittel muss die Werte von heute haben (bitgleich).
/// </summary>
public sealed class GrafikstufenTests
{
    private const long Gb = 1024L * 1024 * 1024;

    [Fact]
    public void Mittel_GleichDenWertenVonHeute()
    {
        var m = Grafikstufen.Mittel;
        Assert.Equal(StadtSzene.FernAb, m.FernAb);
        Assert.Equal(StadtSzene.GruenBis, m.GruenBis);
        Assert.False(m.AutosHalb);
        Assert.Equal(100, m.Aufloesung);
        Assert.Equal(0, m.BilderHoechstens);
        // gleiche Entscheidung wie bis A 0.63.0: fern ab FernAb (echt groesser), Gruen bis GruenBis
        Assert.False(m.Fern(StadtSzene.FernAb));
        Assert.True(m.Fern(StadtSzene.FernAb + 0.01f));
        Assert.True(m.Gruen(StadtSzene.GruenBis));
        Assert.False(m.Gruen(StadtSzene.GruenBis + 0.01f));
    }

    [Fact]
    public void Niedrig_UndHoch_Werte()
    {
        var n = Grafikstufen.Niedrig;
        Assert.True(n.AutosHalb);
        Assert.Equal(75, n.Aufloesung);
        Assert.Equal(30, n.BilderHoechstens);
        Assert.False(n.Gruen(0f));
        Assert.True(n.Fern(Grafikstufen.FernAbNiedrig + 0.01f));
        var h = Grafikstufen.Hoch;
        Assert.False(h.Fern(Kamera.AbstandMax));
        Assert.True(h.Gruen(StadtSzene.GruenBis + 1));
        Assert.False(h.AutosHalb);
    }

    [Fact]
    public void Grenzen_JeStufe()
    {
        Assert.Equal((125_000L, 32), Grafikstufen.Grenze(Grafikstufe.Niedrig));
        Assert.Equal((Lernplattform.Core.Stadtbau.StadtbauGrenzen.Dreiecke, Lernplattform.Core.Stadtbau.StadtbauGrenzen.Aufrufe), Grafikstufen.Grenze(Grafikstufe.Mittel));
        Assert.Equal((200_000L, 48), Grafikstufen.Grenze(Grafikstufe.Hoch));
        Assert.Equal(Grafikstufen.Grenze(Grafikstufe.Hoch), Grafikstufen.Grenze(Grafikstufe.Eigene));
    }

    [Fact]
    public void Merker_LesenUndSchreiben()
    {
        var w = new JsonObject();
        Assert.Null(Grafikstufen.Lesen(w));
        Assert.Equal(Grafikstufen.Mittel, Grafikstufen.Aktuell(w));
        foreach (var s in Enum.GetValues<Grafikstufe>())
        {
            Grafikstufen.Wert(w, s);
            Assert.Equal(s, Grafikstufen.Lesen(w));
        }
        // ungueltige Werte gelten als fehlend (dann Mittel), nie als Ausnahme
        Assert.Null(Grafikstufen.Lesen(new JsonObject { [Grafikstufen.Schluessel] = "ultra" }));
        Assert.Null(Grafikstufen.Lesen(new JsonObject { [Grafikstufen.Schluessel] = 2 }));
        Assert.Equal(Grafikstufen.Mittel, Grafikstufen.Aktuell(new JsonObject { [Grafikstufen.Schluessel] = "ultra" }));
    }

    [Fact]
    public void Eigene_Werte_WerdenGemerkt()
    {
        var w = new JsonObject();
        Grafikstufen.Wert(w, Grafikstufe.Eigene);
        // ohne eigene Werte: wie Mittel
        Assert.Equal(Grafikstufen.Mittel, Grafikstufen.Aktuell(w));
        var eigen = Grafikstufen.Niedrig with { BilderHoechstens = 60, Aufloesung = 100 };
        Grafikstufen.EigeneWert(w, eigen);
        Assert.Equal(eigen, Grafikstufen.Aktuell(w));
        Assert.Equal(eigen, Grafikstufen.EigeneLesen(w));
        // nach Speichern und Lesen (JsonElement statt int/double) gleich
        var gelesen = (JsonObject)JsonNode.Parse(w.ToJsonString())!;
        Assert.Equal(eigen, Grafikstufen.Aktuell(gelesen));
        // ungueltiger Einzelwert (nicht in der Wahlliste) = Wert von Mittel, die anderen bleiben
        ((JsonObject)w[Grafikstufen.SchluesselEigene]!)["bilder"] = 45;
        Assert.Equal(eigen with { BilderHoechstens = Grafikstufen.Mittel.BilderHoechstens }, Grafikstufen.Aktuell(w));
        // die eigenen Werte wirken nur in der Stufe Eigene
        Grafikstufen.Wert(w, Grafikstufe.Hoch);
        Assert.Equal(Grafikstufen.Hoch, Grafikstufen.Aktuell(w));
    }

    public static TheoryData<string, bool, long?, int, string?, Grafikstufe, string> Faelle => new()
    {
        // Name, PC, RAM, Kerne, Renderer, Stufe, Regel (Anfang)
        { "CI Linux llvmpipe", true, 16 * Gb, 4, "llvmpipe (LLVM 15.0.7, 256 bits)", Grafikstufe.Niedrig, "1" },
        { "CI Windows Basic Render", true, 16 * Gb, 4, "ANGLE (Microsoft, Microsoft Basic Render Driver Direct3D11 vs_5_0 ps_5_0, D3D11)", Grafikstufe.Niedrig, "1" },
        { "SwiftShader", false, 8 * Gb, 8, "Google SwiftShader", Grafikstufe.Niedrig, "1" },
        { "wenig RAM", true, 4 * Gb, 8, "NVIDIA GeForce GTX 1050/PCIe/SSE2", Grafikstufe.Niedrig, "2" },
        { "wenig Kerne", true, 16 * Gb, 2, "NVIDIA GeForce GTX 1050/PCIe/SSE2", Grafikstufe.Niedrig, "2" },
        { "S24 Ultra", false, 12 * Gb, 8, "Adreno (TM) 750", Grafikstufe.Mittel, "3" },
        { "Tab A9+", false, 8 * Gb, 8, "Adreno (TM) 619", Grafikstufe.Niedrig, "3" },
        { "Mali G710", false, 8 * Gb, 8, "Mali-G710", Grafikstufe.Mittel, "3" },
        { "Mali G57", false, 6 * Gb, 8, "Mali-G57", Grafikstufe.Niedrig, "3" },
        { "Handy mit RTX-Namen nie Hoch", false, 16 * Gb, 8, "Adreno (TM) 830", Grafikstufe.Mittel, "3" },
        { "Nico RTX 5090 (ANGLE)", true, 64 * Gb, 32, "ANGLE (NVIDIA, NVIDIA GeForce RTX 5090 (0x00002B85) Direct3D11 vs_5_0 ps_5_0, D3D11)", Grafikstufe.Hoch, "4" },
        { "Radeon RX", true, 32 * Gb, 12, "AMD Radeon RX 7800 XT", Grafikstufe.Hoch, "4" },
        { "RTX mit 8 GB", true, 8 * Gb, 8, "NVIDIA GeForce RTX 3060/PCIe/SSE2", Grafikstufe.Mittel, "6" },
        { "Apple M2 Pro", true, 16 * Gb, 10, "Apple M2 Pro", Grafikstufe.Hoch, "5" },
        { "Apple M1", true, 16 * Gb, 8, "Apple M1", Grafikstufe.Mittel, "6" },
        { "CI Mac paravirtuell", true, 7 * Gb, 3, "Apple Paravirtual device", Grafikstufe.Niedrig, "2" },
        { "Intel eingebaut", true, 16 * Gb, 8, "Mesa Intel(R) UHD Graphics 620 (KBL GT2)", Grafikstufe.Mittel, "6" },
        { "Renderer unbekannt", true, null, 8, null, Grafikstufe.Mittel, "6" },
    };

    [Theory]
    [MemberData(nameof(Faelle))]
    public void Automatisch_FesteTabelle(string fall, bool pc, long? ram, int kerne, string? renderer, Grafikstufe stufe, string regel)
    {
        var wahl = Grafikstufen.Automatisch(new Grafikstufen.Geraet(pc, ram, kerne, renderer));
        Assert.True(wahl.Stufe == stufe, fall + ": " + wahl.Stufe + " (" + wahl.Regel + ")");
        Assert.StartsWith(regel + " ", wahl.Regel, StringComparison.Ordinal);
    }

    [Fact]
    public void Automatisch_Gegenproben()
    {
        // dieselbe Grafikkarte: ohne Software-Zeichnung Hoch, mit „llvmpipe“ im Namen Niedrig
        var stark = new Grafikstufen.Geraet(true, 32 * Gb, 16, "NVIDIA GeForce RTX 4080/PCIe/SSE2");
        Assert.Equal(Grafikstufe.Hoch, Grafikstufen.Automatisch(stark).Stufe);
        Assert.Equal(Grafikstufe.Niedrig, Grafikstufen.Automatisch(stark with { Renderer = "llvmpipe" }).Stufe);
        // dieselbe Karte am Handy (kein PC) nie Hoch
        Assert.NotEqual(Grafikstufe.Hoch, Grafikstufen.Automatisch(stark with { Pc = false }).Stufe);
        // Adreno 699 knapp unter der Grenze, 700 genau auf ihr
        var handy = new Grafikstufen.Geraet(false, 8 * Gb, 8, "Adreno (TM) 699");
        Assert.Equal(Grafikstufe.Niedrig, Grafikstufen.Automatisch(handy).Stufe);
        Assert.Equal(Grafikstufe.Mittel, Grafikstufen.Automatisch(handy with { Renderer = "Adreno (TM) 700" }).Stufe);
        // 16 GB genau reicht, knapp darunter nicht
        Assert.Equal(Grafikstufe.Hoch, Grafikstufen.Automatisch(stark with { RamBytes = 16 * Gb }).Stufe);
        Assert.Equal(Grafikstufe.Mittel, Grafikstufen.Automatisch(stark with { RamBytes = (16 * Gb) - 1 }).Stufe);
    }

    [Fact]
    public void Belastung_MitteltUeberFuenfSekunden_UndNichtsGeschaetzt()
    {
        var b = new Belastung();
        var t = 100.0;
        b.Bild(t, 5);
        Assert.Null(b.Aktuell);
        for (var i = 0; i < 60 * 7; i++)
        {
            t += 1.0 / 60;
            b.Bild(t, 4);
        }
        Assert.NotNull(b.Aktuell);
        var w = b.Aktuell!;
        Assert.InRange(w.BilderJeSekunde, 55, 65);
        Assert.Equal(4, w.ZeitJeBildMs, 3);
        Assert.Null(w.GpuMs);   // keine GPU-Zeit gemeldet: nicht verfuegbar, nicht 0
        Assert.True(w.RamBytes > 0);
        Assert.Same(w, Belastung.Letzte);
        // Pause ueber 3 s (im Haus): neu anfangen
        b.Bild(t + 10, 4);
        b.GpuZeit(2.5);
        for (var i = 0; i < 70; i++)
        {
            b.Bild(t + 10 + ((i + 1) / 60.0), 4);
        }
        Assert.Equal(2.5, b.Aktuell!.GpuMs!.Value, 3);
    }

    [Fact]
    public void Belastung_Mittel_WertNurWennJedeSekundeIhnHatte()
    {
        var a = new Belastung.Werte(60, 4, 100, 10, 2, null);
        var b = new Belastung.Werte(30, 6, 200, null, 4, null);
        var m = Belastung.Mittel([a, b]);
        Assert.Equal(45, m.BilderJeSekunde);
        Assert.Equal(200, m.RamBytes);
        Assert.Null(m.CpuProzent);
        Assert.Equal(3, m.GpuMs);
        Assert.Null(m.GpuProzent);
    }

    [Fact]
    public void Texte_NichtVerfuegbar_StattSchaetzung()
    {
        var w = new Belastung.Werte(59.6, 4.25, 512L * 1024 * 1024, null, null, null);
        var zeilen = GrafikTexte.Zeilen(w);
        Assert.Equal(6, zeilen.Count);
        Assert.Equal("60", zeilen[0].Wert);
        Assert.Equal("512 MB", zeilen[2].Wert);
        Assert.Equal(GrafikTexte.NichtVerfuegbar, zeilen[3].Wert);
        Assert.Equal(GrafikTexte.NichtVerfuegbar, zeilen[4].Wert);
        Assert.Equal(GrafikTexte.NichtVerfuegbar, zeilen[5].Wert);
        Assert.Equal("RAM 512 MB · CPU nicht verfügbar · GPU nicht verfügbar", GrafikTexte.Kurzzeile(w));
        Assert.Equal("RAM 512 MB · CPU 12 % · GPU 3,1 ms · GPU 40 %", GrafikTexte.Kurzzeile(w with { CpuProzent = 12, GpuMs = 3.1, GpuProzent = 40 }));
        Assert.Equal("Niedrig 81 % · Hoch 70 % (geprüft wird gegen Mittel)", GrafikTexte.WerkzeugInfo(0.81, 0.7));
        Assert.Contains("starke Geräte", GrafikTexte.Erklaerung(Grafikstufe.Hoch), StringComparison.Ordinal);
    }

    [Fact]
    public void Gezeigt_Halb_JedesZweiteAuto_BusImmer()
    {
        var gestalten = Enumerable.Range(0, 5).Select(i => (FestVerkehr.Gestalt)default!).ToList();
        Assert.Equal(5, FestVerkehr.Gezeigt(gestalten, false).Count());
        Assert.Equal(3, FestVerkehr.Gezeigt(gestalten, true).Count());
    }
}
