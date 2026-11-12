using Lernplattform.Core.Daten;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle D2 (Weisung P12-157, Nico 3a, Hinweise P12-156): Grenzen live ohne Grafikkarte, Waermekarte nur auf der Karte,
/// roter Fund „Stadt zu teuer“ mit Stelle ueber der Grenze, Kopf der Leiste nennt den Wert, der den Balken fuellt.
/// </summary>
public class GrenzenLiveTests
{
    private static Spieldaten D => Daten.Spiel;

    private static readonly Lazy<GrenzenLive> Stadt1 = new(() => GrenzenLive.Fuer(D.Fassung, D));

    [Fact]
    public void Stadt1_UnterDerGrenze_OhneFund()
    {
        var g = Stadt1.Value;
        Assert.False(g.Ueber);
        Assert.Null(g.Fund());
        Assert.True(g.Gesamt.Dreiecke > 0 && g.Nah.Dreiecke > 0);
        Assert.True(g.Anteil is > 0 and <= 1);
        // je Abstand, Drehung und Rasterstelle ein Bild, dazu die Gesamtansicht
        Assert.True(g.Bilder > 1 + (GrenzenLive.NahAbstaende.Length * 2));
    }

    [Fact]
    public void Gegenprobe_OhneSichtkegel_NahansichtTeurer()
    {
        GrenzenLive.OhneSparenFuerTests = true;
        GrenzenLive ohne;
        try
        {
            ohne = GrenzenLive.Fuer(D.Fassung, D);
        }
        finally
        {
            GrenzenLive.OhneSparenFuerTests = false;
        }
        Assert.True(ohne.Nah.Dreiecke > Stadt1.Value.Nah.Dreiecke, ohne + " gegen " + Stadt1.Value);
        Assert.True(ohne.Gesamt.Dreiecke > Stadt1.Value.Gesamt.Dreiecke, ohne + " gegen " + Stadt1.Value);
    }

    /// <summary>
    /// Sammelliste 307 (Weisung P12-202, Laeufe 855 und 856 unter Windows): die Gegenprobe oben schaltete den Sichtkegel fuer den
    /// ganzen Prozess ab; ein Test einer anderen Klasse, der gerade nebenher Grenzen live rechnete (QuestTests, OrtVerlegenTests),
    /// bekam „Stadt zu teuer“. Hier setzt ein Ablauf die Schalter, ein zweiter rechnet zur selben Zeit: er muss wie ohne Schalter
    /// rechnen. Gegenprobe: mit den Schaltern von S 0.42.1 (statisch fuer den Prozess) ist dieser Test rot.
    /// </summary>
    [Fact]
    public async Task Schalter_GeltenNurImEigenenAblauf_NebenherRechnetNormal()
    {
        using var gesetzt = new ManualResetEventSlim();
        using var fertig = new ManualResetEventSlim();
        var setzer = Task.Run(() =>
        {
            GrenzenLive.OhneSparenFuerTests = true;
            Blueprint.ModellnamenAltFuerTests = true;
            try
            {
                gesetzt.Set();
                Assert.True(fertig.Wait(TimeSpan.FromMinutes(2)), "zweiter Ablauf nicht fertig");
            }
            finally
            {
                GrenzenLive.OhneSparenFuerTests = false;
                Blueprint.ModellnamenAltFuerTests = false;
            }
        }, TestContext.Current.CancellationToken);
        var rechner = Task.Run(() =>
        {
            try
            {
                Assert.True(gesetzt.Wait(TimeSpan.FromMinutes(2)), "erster Ablauf hat nicht gesetzt");
                Assert.False(GrenzenLive.OhneSparenFuerTests);
                Assert.False(Blueprint.ModellnamenAltFuerTests);
                return GrenzenLive.Fuer(D.Fassung, D);
            }
            finally
            {
                fertig.Set();
            }
        }, TestContext.Current.CancellationToken);
        var nebenher = await rechner;
        await setzer;
        Assert.Equal(Stadt1.Value.Nah, nebenher.Nah);
        Assert.Equal(Stadt1.Value.Gesamt, nebenher.Gesamt);
        Assert.Null(nebenher.Fund());
    }

    [Fact]
    public void Waermekarte_NurFelderAufDerKarte()
    {
        var g = Stadt1.Value;
        Assert.Equal(g.Breite * g.Tiefe, g.Felder.Length);
        Assert.Contains(g.Felder, f => f > 0);
        Assert.Equal(0, g.Feld(-1, 0));
        Assert.Equal(0, g.Feld(0, g.Tiefe));
        Assert.Equal(0, g.Feld(g.Breite, 0));
        Assert.Equal(-1, GrenzenLive.Stufe(0));
        Assert.Equal(0, GrenzenLive.Stufe(1));
        Assert.Equal(1, GrenzenLive.Stufe(101));
        Assert.Equal(GrenzenLive.WaermeStufen.Length - 1, GrenzenLive.Stufe(long.MaxValue));
        Assert.Equal(GrenzenLive.WaermeStufen.Length, GrenzenLive.WaermeFarben.Length);
    }

    [Fact]
    public void UeberDerGrenze_RoterFundMitStelle()
    {
        var g = Stadt1.Value with { Nah = (StadtbauGrenzen.Dreiecke + 1, 10), NahX = 7, NahZ = 9 };
        Assert.True(g.Ueber);
        var fund = g.Fund();
        Assert.NotNull(fund);
        Assert.True(fund!.Rot);
        Assert.StartsWith("Stadt zu teuer", fund.Text, StringComparison.Ordinal);
        Assert.Equal((7f, 9f), ((float)fund.X!, (float)fund.Y!));
        // Gesamtansicht zu teuer: Stelle = Mitte der Karte
        var gesamt = Stadt1.Value with { Gesamt = (10, StadtbauGrenzen.Aufrufe + 1) };
        var f2 = gesamt.Fund()!;
        Assert.Equal((gesamt.Breite / 2f, gesamt.Tiefe / 2f), ((float)f2.X!, (float)f2.Y!));
        // Gegenprobe: genau auf der Grenze ist kein Fund
        Assert.Null((Stadt1.Value with { Nah = (StadtbauGrenzen.Dreiecke, StadtbauGrenzen.Aufrufe) }).Fund());
    }

    [Fact]
    public void Kopf_NenntDenWertDerDenBalkenFuellt()
    {
        Assert.Equal("Aufrufe 55 % (Dreiecke 41 %)", Texte.GrenzenLiveKopf(65600, 22, 160000, 40));
        Assert.Equal("Dreiecke 75 % (Aufrufe 50 %)", Texte.GrenzenLiveKopf(120000, 20, 160000, 40));
    }

    /// <summary>
    /// Welle Q2: die Questgeber draussen zeichnet die 3D-Szene selbst (Paket-Netz wie im Spiel), Grenzen live zaehlen sie
    /// darum genau einmal (in dieser Klasse, weil <see cref="GrenzenLive.OhneSparenFuerTests"/> fuer den ganzen Prozess gilt). Gegenprobe: die Zugabe aus Q1 dazu zaehlt sie doppelt.
    /// </summary>
    [Fact]
    public void Grenzen_QuestgeberDraussen_InDerSzeneGezaehlt_NieDoppelt()
    {
        var p = QuestTests.Stadt1();
        var ohne = GrenzenLive.Fuer(p.Stand(), p.Daten);
        var qg = p.QuestgeberNeu();
        p.QuestgeberPlatz(qg, QuestTests.DraussenPlatz(p));
        var je = GrenzenLive.QuestgeberDreieckeDraussen(p.Stand(), p.Daten);
        var mit = GrenzenLive.Fuer(p.Stand(), p.Daten);
        Assert.Equal(0, mit.QuestgeberDreiecke);
        Assert.True(mit.Gesamt.Dreiecke > ohne.Gesamt.Dreiecke, "Questgeber draussen fehlt in der Szene");
        Assert.True(mit.Gesamt.Dreiecke - ohne.Gesamt.Dreiecke <= je, "mehr als eine Figur gezaehlt");
        GrenzenLive.QuestgeberZugabeAltFuerTests = true;
        try
        {
            var alt = GrenzenLive.Fuer(p.Stand(), p.Daten);
            Assert.True(alt.Gesamt.Dreiecke - ohne.Gesamt.Dreiecke > je, "Gegenprobe: Q1-Zugabe zaehlt doppelt");
        }
        finally
        {
            GrenzenLive.QuestgeberZugabeAltFuerTests = false;
        }
    }
}
