using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.25.0 (Weisung P12-104, Kurzplan Meldung P12-69): 246 Katalog-Stueck vor einer Tuer ist rot (Tuer und erste
/// 0,6 des Wegs, Effekt ausgenommen), je rot und gruen mit Gegenprobe ohne Regel; 247 Beschriftung der Draufsicht deutsch
/// wie die Liste „Modell“ (Gegenprobe alte Beschriftung = Kennung).
/// </summary>
public class TuerFreiTests
{
    private static Projekt Stadt1() => Projekt.Neu("Tuer frei", Daten.Spiel, Daten.NeueAblage());

    private static KatalogEintrag Kiste => Katalog.Aktiv!.Eintraege.First(k => k.Art == KatalogArt.Modell && k.Aussen[0] is >= 0.3 and <= 0.5 && k.Aussen[1] is >= 0.3 and <= 0.5);

    private static KatalogEintrag Effekt => Katalog.Aktiv!.Eintraege.First(k => k.Art == KatalogArt.Effekt);

    private static void Setzen(Projekt p, KatalogEintrag e, double x, double y) =>
        Assert.NotNull(p.AussenAendern("Stueck", a => AussenBearbeitung.Setzen(a, AussenArt.Katalog, x, y, raster: false, modell: e.Id)));

    private static List<StadtbauFund> Verstellt(Projekt p) =>
        [.. p.PruefenAussen().Where(f => f.Text.EndsWith(": die Figur kommt nicht hinein.", StringComparison.Ordinal))];

    /// <summary>Zierweg des Stellwerks in Stadt 1: Tuer (17,25 | 14,0), Weg erst 0,45 nach unten, dann nach rechts.</summary>
    private static (double X, double Y) Stellwerk => (17.25, 14.0);

    [Fact]
    public void Stadt1_OhneStueck_KeinFund()
    {
        Assert.Empty(TuerFrei.Pruefen(Stadt1().AussenStand));
    }

    [Fact]
    public void StueckAufDerTuer_Rot_MitNamen_GegenprobeOhneRegel()
    {
        var p = Stadt1();
        Setzen(p, Kiste, Stellwerk.X, Stellwerk.Y + 0.1);
        var f = Assert.Single(Verstellt(p));
        Assert.True(f.Rot);
        Assert.Equal(Texte.TuerVerstellt(Kiste.Anzeige, "Stellwerk"), f.Text);
        Assert.Contains(p.RoteFunde(), r => r.Fund.Text == f.Text);
        TuerFrei.AusFuerTests.Value = true;
        try
        {
            Assert.Empty(Verstellt(p));
        }
        finally
        {
            TuerFrei.AusFuerTests.Value = false;
        }
    }

    [Fact]
    public void StueckAufDemWeg_BisNull6_Rot_Dahinter_Gruen()
    {
        // 0,45 nach unten, dann 0,15 nach rechts = 0,6: bis (17,40 | 14,45) zaehlt der Weg, dahinter nicht mehr
        // (Setzen ohne Einrasten legt auf 0,1; die Kiste ist 0,3 bis 0,5 breit und tief; der Weg zaehlt in seiner Breite)
        var a = Stadt1();
        Setzen(a, Kiste, 17.4, 14.5);
        Assert.Single(Verstellt(a));
        var b = Stadt1();
        Setzen(b, Kiste, 18.4, 14.5);
        Assert.Empty(Verstellt(b));
    }

    [Fact]
    public void StueckNebenDemWeg_Gruen()
    {
        var p = Stadt1();
        Setzen(p, Kiste, 16.5, 14.2);
        Assert.Empty(Verstellt(p));
    }

    [Fact]
    public void EffektVorDerTuer_Gruen()
    {
        var p = Stadt1();
        Setzen(p, Effekt, Stellwerk.X, Stellwerk.Y + 0.1);
        Assert.Empty(Verstellt(p));
    }

    [Fact]
    public void TuerEinesOrts_Rot_MitOrtsnamen()
    {
        var p = Stadt1();
        var z = AussenBearbeitung.Zugang(p.AussenStand, "bank")!;
        var t = (List<object?>)z["tuer"]!;
        Setzen(p, Kiste, Py.F(t[0]), Py.F(t[1]));
        var f = Assert.Single(Verstellt(p));
        Assert.Contains("„" + AussenBearbeitung.Ort(p.AussenStand, "bank")!["name"] + "“", f.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Beschriftung_Deutsch_GegenprobeKennung()
    {
        var p = Stadt1();
        var land = Aussen.Landschaft(p.AussenStand);
        var i = land.FindIndex(l => l is PyObjekt g && Py.Gleich(g["typ"], "gebaeude") && Py.Gleich(g["modell"], "stellwerk"));
        var s = new AussenStelle(AussenArt.Gebaeude, i);
        Assert.Equal("Stellwerk", ModellNamen.Beschriftung(p.AussenStand, s));
        ModellNamen.BeschriftungAltFuerTests.Value = true;
        try
        {
            Assert.Equal("stellwerk", ModellNamen.Beschriftung(p.AussenStand, s));
        }
        finally
        {
            ModellNamen.BeschriftungAltFuerTests.Value = false;
        }
    }

    [Fact]
    public void Beschriftung_Bahnsteig_UndKopie()
    {
        var p = Stadt1();
        p.AussenAendern("Bahnsteig", a => Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "gebaeude" }, { "modell", Bahnsteig.ModellPraefix + "bahnhof" }, { "x", 3.0 }, { "y", 3.0 } }));
        var land = Aussen.Landschaft(p.AussenStand);
        Assert.Equal(Texte.Bahnsteig, ModellNamen.Beschriftung(p.AussenStand, new AussenStelle(AussenArt.Gebaeude, land.Count - 1)));
        // jedes Gebaeude ohne Ort der Stadt 1 traegt einen Namen, keine Kennung
        foreach (var (st, _, _) in AussenBearbeitung.Alle(p.AussenStand).Where(x => x.Stelle.Art == AussenArt.Gebaeude))
        {
            var modell = Py.Str(Objekt(land[st.Nummer])["modell"]);
            Assert.NotEqual(modell, ModellNamen.Beschriftung(p.AussenStand, st));
        }
    }
}
