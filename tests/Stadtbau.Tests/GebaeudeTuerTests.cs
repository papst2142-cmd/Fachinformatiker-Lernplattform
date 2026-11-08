using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.24.0 (Weisung P12-100, Nico 1b 2a 3a 4a, Sammelliste 245): „Tür dazu“ an Gebaeuden ohne Innenraum. Geprueft:
/// das Spiel zeichnet die Tuer jedes vorgegebenen Modells in jeder Drehung auf der Wand; Weg zur Strasse; zweites Haus
/// gleichen Modells bekommt eine Kopie (Gegenprobe ohne Kopie: Tuer am falschen Haus); Entfernen; Loeschen raeumt die
/// Kopie weg; Pruefregeln (Tuer in ein Haus, ins Wasser, Bahnhof fern vom Gleis, Weg ohne Strasse) je rot/gelb und gruen;
/// Liste mit Bahnhoefen und deutschen Namen (Gegenprobe alte Liste). Fassung 0 bleibt unberuehrt (nur leere Staedte).
/// </summary>
public class GebaeudeTuerTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Projekt Leer(double groesse = 40) => Projekt.Neu("Tuer", D, Daten.NeueAblage(), null, (groesse, groesse));

    private static AussenStelle Haus(Projekt p, string modell, double x, double y, int dreh = 0) =>
        p.AussenAendern("Haus", a =>
        {
            var s = AussenBearbeitung.Setzen(a, AussenArt.Gebaeude, x, y, raster: true, modell: modell)!;
            for (var i = 0; i < dreh; i++)
            {
                AussenBearbeitung.Drehen(a, s);
            }
            return s;
        });

    /// <summary>Waagrechte Strasse bei y von x0 bis x1 (mit Gehweg).</summary>
    private static void Strasse(Projekt p, double y, double x0 = 2, double x1 = 38) =>
        p.AussenAendern("Strasse", a => Aussen.Landschaft(a).Add(new PyObjekt
        {
            { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { x0, y }, new List<object?> { x1, y } } }, { "gehweg", 0.35 },
        }));

    private static Vector2 TuerVon(Projekt p, AussenStelle s)
    {
        var z = Blueprint.ZierwegVon(p.AussenStand, s)!;
        var t = (List<object?>)z["tuer"]!;
        return new Vector2((float)Py.F(t[0]), (float)Py.F(t[1]));
    }

    private static IReadOnlyList<StadtTuer> SpielTueren(Projekt p) =>
        Ansicht3D.Bauen(p.Stand(), D, Ansicht3D.Einstellung.Standard).Szene.Tueren;

    public static TheoryData<string, int> ModelleUndDrehungen()
    {
        var d = new TheoryData<string, int>();
        foreach (var m in ModellNamen.Namen.Keys)
        {
            for (var r = 0; r < 4; r++)
            {
                d.Add(m, r);
            }
        }
        return d;
    }

    [Theory]
    [MemberData(nameof(ModelleUndDrehungen))]
    public void JedesModell_JedeDrehung_SpielZeichnetDieTuerAufDerWand(string modell, int dreh)
    {
        var p = Leer();
        var s = Haus(p, modell, 20, 20, dreh);
        Assert.Null(p.AussenAendern("Tuer", a => GebaeudeTuer.Dazu(a, s, einrasten: false) is var h && h == Texte.TuerWegOhneStrasse ? null : h));
        Assert.True(GebaeudeTuer.HatTuer(p.AussenStand, s));
        var tuer = TuerVon(p, s);
        // Tuer auf der Vorderseite (dreht mit dem Haus)
        var (x, y, w, h) = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        var n = AussenBearbeitung.HausFront(p.AussenStand, s);
        var mitte = new Vector2((float)(x + (w / 2)), (float)(y + (h / 2)));
        Assert.True(Vector2.Dot(tuer - mitte, n) > 0, $"Tuer {tuer} liegt nicht auf der Vorderseite {n}");
        var t = SpielTueren(p).Where(t => Vector2.Distance(t.WegAnfang, tuer) <= 0.05f).ToList();
        Assert.True(t.Count == 1, $"{modell} dreh {dreh}: das Spiel zeichnet {t.Count} Tueren bei {tuer}");
        Assert.True(Vector2.Distance(t[0].Mitte, tuer) <= 0.6f, $"{modell} dreh {dreh}: Tuer {t[0].Mitte} nicht an der Wand bei {tuer}");
    }

    [Fact]
    public void MitEinrasten_HausAnDenGehweg_WegErreichtDieStrasse_OhneFund()
    {
        var p = Leer();
        var s = Haus(p, "kaufhaus", 20, 15);
        var vorher = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        // Strasse so, dass die Front 0,5 vor der Gehwegkante steht (Fangweite wie bei den Laeden 0,95)
        var strasseY = Math.Round(vorher.Y + vorher.H + (StadtSzene.StrassenBreite / 2) + 0.35 + 0.5, 2);
        Strasse(p, strasseY);
        Assert.Null(p.AussenAendern("Tuer", a => GebaeudeTuer.Dazu(a, s, einrasten: true)));
        var nachher = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        Assert.True(nachher.Y > vorher.Y, "Haus ist nicht an den Gehweg gerueckt");
        var z = Blueprint.ZierwegVon(p.AussenStand, s)!;
        var ende = ((List<object?>)z["weg"]!).OfType<List<object?>>().Last();
        Assert.Equal(strasseY, Py.F(ende[1]), 2);
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Text.Contains("Tür", StringComparison.Ordinal) || f.Text.Contains("Fußweg", StringComparison.Ordinal));
        Assert.Equal("Kaufhaus", Py.Str(z["name"]));
    }

    [Fact]
    public void OhneStrasse_TuerTrotzdem_GelberHinweis()
    {
        var p = Leer();
        var s = Haus(p, "amt", 20, 20);
        Assert.Equal(Texte.TuerWegOhneStrasse, p.AussenAendern("Tuer", a => GebaeudeTuer.Dazu(a, s, einrasten: true)));
        Assert.True(GebaeudeTuer.HatTuer(p.AussenStand, s));
        Assert.Contains(p.PruefenAussen(), f => !f.Rot && f.Text.StartsWith(Texte.TuerWegOhneStrasse, StringComparison.Ordinal));
        // Nico 1b: nur gelb, kein roter Fund dazu (Gegenprobe: der rote Fund des gemeinsamen Teils bliebe)
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot);
        GebaeudeTuer.RotBleibtFuerTests.Value = true;
        try
        {
            Assert.Contains(p.PruefenAussen(), f => f.Rot && f.Text == "Der Fußweg von „Amt“ erreicht keine Straße.");
        }
        finally
        {
            GebaeudeTuer.RotBleibtFuerTests.Value = false;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void StrasseHinterOderNebenDemHaus_WegFuehrtUmDasHaus(int dreh)
    {
        // Fund Teststadt Nico: die Haeuser stehen mit Rueckseite/Seite zur Strasse; der Weg geht um das Haus herum
        var p = Leer();
        var s = Haus(p, "kaufhaus", 20, 20, dreh);
        var f = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        Strasse(p, Math.Round(f.Y - 1.5, 2));
        Assert.Null(p.AussenAendern("Tuer", a => GebaeudeTuer.Dazu(a, s, einrasten: true)));
        var f2 = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        var weg = ((List<object?>)Blueprint.ZierwegVon(p.AussenStand, s)!["weg"]!).OfType<List<object?>>().Select(q => new Vector2((float)Py.F(q[0]), (float)Py.F(q[1]))).ToList();
        for (var i = 1; i + 1 < weg.Count; i++)
        {
            Assert.False(GebaeudeTuer.SchneidetHaus(weg[i], weg[i + 1], new Vector2((float)f2.X, (float)f2.Y), new Vector2((float)(f2.X + f2.W), (float)(f2.Y + f2.H))), "Weg schneidet das Haus");
        }
        Assert.Equal(Math.Round(f.Y - 1.5, 2), weg[^1].Y, 2);
        Assert.DoesNotContain(p.PruefenAussen(), x => x.Rot || x.Text.StartsWith(Texte.TuerWegOhneStrasse, StringComparison.Ordinal));
    }

    [Fact]
    public void WegEndeMeidetLaternen_Gegenprobe()
    {
        Vector2 Ende(bool egal)
        {
            var p = Leer();
            var s = Haus(p, "kaufhaus", 20, 20);
            var f = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
            var y = Math.Round(f.Y - 1.5, 2);
            Strasse(p, y);
            var mitteX = Math.Round(f.X + (f.W / 2), 2);
            // Laterne genau dort, wo der kuerzeste Weg ankaeme (ueber der Hausmitte, Weg um die Ecke links oder rechts)
            p.AussenAendern("Laternen", a =>
            {
                Aussen.Laternen(a).Add(new List<object?> { Math.Round(f.X - 0.5, 2), y + 0.5, false });
                Aussen.Laternen(a).Add(new List<object?> { Math.Round(f.X + f.W + 0.5, 2), y + 0.5, false });
            });
            GebaeudeTuer.LaternenEgalFuerTests.Value = egal;
            try
            {
                Assert.Null(p.AussenAendern("Tuer", a => GebaeudeTuer.Dazu(a, s, einrasten: true)));
            }
            finally
            {
                GebaeudeTuer.LaternenEgalFuerTests.Value = false;
            }
            var w = (List<object?>)Blueprint.ZierwegVon(p.AussenStand, s)!["weg"]!;
            var e = (List<object?>)w[^1]!;
            var ende = new Vector2((float)Py.F(e[0]), (float)Py.F(e[1]));
            var lat = Aussen.Laternen(p.AussenStand).OfType<List<object?>>().Select(l => new Vector2((float)Py.F(l[0]), (float)Py.F(l[1])));
            Assert.Equal(egal, lat.Any(l => Vector2.Distance(l, ende) <= GebaeudeTuer.LaterneFrei));
            return ende;
        }
        Ende(egal: false);
        Ende(egal: true); // Gegenprobe: ohne die Regel endet der Weg an einer Laterne
    }

    [Fact]
    public void StrasseHinterDemHaus_Gegenprobe_NurGeradeaus_ErreichtSieNicht()
    {
        var p = Leer();
        var s = Haus(p, "kaufhaus", 20, 20);
        var f = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        Strasse(p, Math.Round(f.Y - 1.5, 2));
        GebaeudeTuer.NurGeradeausFuerTests.Value = true;
        try
        {
            Assert.Equal(Texte.TuerWegOhneStrasse, p.AussenAendern("Tuer", a => GebaeudeTuer.Dazu(a, s, einrasten: true)));
        }
        finally
        {
            GebaeudeTuer.NurGeradeausFuerTests.Value = false;
        }
    }

    [Fact]
    public void EinSchritt_RueckgaengigNimmtTuerUndWegWeg()
    {
        var p = Leer();
        Strasse(p, 20);
        var s = Haus(p, "bank", 20, 15);
        p.AussenAendern("Tuer", a => GebaeudeTuer.Dazu(a, s, einrasten: true));
        Assert.True(GebaeudeTuer.HatTuer(p.AussenStand, s));
        Assert.True(p.Rueckgaengig());
        Assert.False(GebaeudeTuer.HatTuer(p.AussenStand, s));
    }

    [Fact]
    public void ZweitesHausGleichenModells_BekommtKopie_TuerAmRichtigenHaus()
    {
        foreach (var alt in new[] { false, true })
        {
            GebaeudeTuer.OhneKopieFuerTests.Value = alt;
            try
            {
                var p = Leer();
                Haus(p, "wohnung", 8, 8);
                var zweites = Haus(p, "wohnung", 25, 25);
                p.AussenAendern("Tuer", a => GebaeudeTuer.Dazu(a, zweites, einrasten: false));
                var (x, y, w, h) = AussenBearbeitung.HausFlaeche(p.AussenStand, zweites);
                var amZweiten = SpielTueren(p).Any(t => t.Mitte.X >= x - 0.6 && t.Mitte.X <= x + w + 0.6 && t.Mitte.Y >= y - 0.6 && t.Mitte.Y <= y + h + 0.6);
                if (alt)
                {
                    // Gegenprobe: ohne Kopie zeichnet das Spiel die Tuer am ersten Haus dieses Modells
                    Assert.False(amZweiten);
                    continue;
                }
                Assert.True(amZweiten);
                var modell = Py.Str(Objekt(Aussen.Landschaft(p.AussenStand)[zweites.Nummer])["modell"]);
                Assert.Equal("wohnung_2", modell);
                Assert.True(GebaeudeTuer.IstKopie(p.AussenStand, modell));
                Assert.Equal("Wohnung 2", ModellNamen.Anzeige(p.AussenStand, modell));
                Assert.DoesNotContain("wohnung_2", AussenBearbeitung.HausModelle(p.AussenStand));
                // Entfernen: Tuer weg, Kopie bleibt am Haus
                Assert.True(p.AussenAendern("weg", a => GebaeudeTuer.Entfernen(a, zweites)));
                Assert.False(GebaeudeTuer.HatTuer(p.AussenStand, zweites));
                Assert.NotNull(Get(AussenBearbeitung.Modelle(p.AussenStand), "wohnung_2"));
                // Loeschen: die Kopie nutzt niemand mehr und faellt weg
                p.AussenAendern("loeschen", a => AussenBearbeitung.Loeschen(a, zweites));
                Assert.Null(Get(AussenBearbeitung.Modelle(p.AussenStand), "wohnung_2"));
                Assert.NotNull(Get(AussenBearbeitung.Modelle(p.AussenStand), "wohnung"));
            }
            finally
            {
                GebaeudeTuer.OhneKopieFuerTests.Value = false;
            }
        }
    }

    [Fact]
    public void Pruefung_TuerInEinAnderesHaus_Rot_SonstGruen()
    {
        var p = Leer();
        var s = Haus(p, "amt", 20, 18);
        p.AussenAendern("Tuer", a => GebaeudeTuer.Dazu(a, s, einrasten: false));
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot && f.Text.Contains("anderes Gebäude", StringComparison.Ordinal));
        var tuer = TuerVon(p, s);
        Haus(p, "bank", tuer.X, tuer.Y + 1.2);
        Assert.Contains(p.PruefenAussen(), f => f.Rot && f.Text == Texte.TuerAnHaus("Amt"));
    }

    [Fact]
    public void Pruefung_TuerInsWasser_Rot_SonstGruen()
    {
        var p = Leer();
        var s = Haus(p, "amt", 20, 18);
        p.AussenAendern("Tuer", a => GebaeudeTuer.Dazu(a, s, einrasten: false));
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Text == Texte.TuerImWasser("Amt"));
        var tuer = TuerVon(p, s);
        p.AussenAendern("Teich", a => ((List<object?>)Aussen.WasserAnlegen(a)["teiche"]!).Add(new PyObjekt
        {
            { "punkte", new List<object?> { new List<object?> { (double)tuer.X - 1.5, (double)tuer.Y + 0.2 }, new List<object?> { (double)tuer.X + 1.5, (double)tuer.Y + 0.2 }, new List<object?> { (double)tuer.X + 1.5, (double)tuer.Y + 3 }, new List<object?> { (double)tuer.X - 1.5, (double)tuer.Y + 3 } } },
        }));
        Assert.Contains(p.PruefenAussen(), f => f.Rot && f.Text == Texte.TuerImWasser("Amt"));
    }

    [Fact]
    public void Knopf_NurAnGebaeudenOhneInnenraum_NichtAmBahnsteigStueck()
    {
        var p = Leer();
        var s = Haus(p, "bahnhof", 20, 20);
        Assert.True(GebaeudeTuer.KnopfZeigen(p.AussenStand, s));
        p.AussenAendern("Tuer", a => GebaeudeTuer.Dazu(a, s, einrasten: false));
        Assert.True(GebaeudeTuer.KnopfZeigen(p.AussenStand, s)); // jetzt „Tür entfernen“
        var land = Aussen.Landschaft(p.AussenStand);
        var nr = land.FindIndex(t => t is PyObjekt g && Py.Str(g["modell"]).StartsWith(Bahnsteig.ModellPraefix, StringComparison.Ordinal));
        Assert.True(nr >= 0, "Bahnsteig steht nicht als eigenes Stueck");
        var steig = new AussenStelle(AussenArt.Gebaeude, nr);
        Assert.False(GebaeudeTuer.KnopfZeigen(p.AussenStand, steig));
        Assert.Equal(Texte.TuerNurGebaeude, GebaeudeTuer.DazuNicht(p.AussenStand, steig));
        Assert.False(GebaeudeTuer.KnopfZeigen(p.AussenStand, new AussenStelle(AussenArt.Baum, 0)));
    }

    [Fact]
    public void Pruefung_BahnhofFernVomGleis_Gelb_AmGleisNicht()
    {
        var p = Leer();
        p.AussenAendern("Bahn", a => Aussen.Landschaft(a).Add(new PyObjekt
        {
            { "typ", "bahn" }, { "punkte", new List<object?> { new List<object?> { 0.0, 30.0 }, new List<object?> { 40.0, 30.0 } } }, { "radien", new List<object?> { 3.0, 3.0 } },
        }));
        var nah = Haus(p, "bahnhof", 20, 27.5);
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Text == Texte.BahnhofNichtAmGleis("Bahnhof Talheim"));
        Haus(p, "bahnhof_neu", 20, 10);
        var fund = Assert.Single(p.PruefenAussen(), f => f.Text == Texte.BahnhofNichtAmGleis("Talheim-Nord"));
        Assert.False(fund.Rot);
        Assert.NotNull(nah);
    }

    [Fact]
    public void Liste_MitBahnhoefen_DeutscheNamen_Gegenprobe()
    {
        var p = Leer();
        var liste = AussenBearbeitung.HausModelle(p.AussenStand);
        Assert.Contains("bahnhof", liste);
        Assert.Contains("bahnhof_neu", liste);
        Assert.All(liste, m => Assert.True(ModellNamen.Namen.ContainsKey(m), m + " ohne deutschen Namen"));
        Assert.Equal(liste.Select(m => ModellNamen.Anzeige(p.AussenStand, m)).Order(StringComparer.Ordinal), liste.Select(m => ModellNamen.Anzeige(p.AussenStand, m)));
        AussenBearbeitung.BahnhoefeAltFuerTests.Value = true;
        try
        {
            Assert.DoesNotContain("bahnhof", AussenBearbeitung.HausModelle(p.AussenStand));
        }
        finally
        {
            AussenBearbeitung.BahnhoefeAltFuerTests.Value = false;
        }
    }

    [Fact]
    public void ZweimalDazu_Geht_Nicht_EntfernenOhneTuer_False()
    {
        var p = Leer();
        var s = Haus(p, "filiale", 20, 20);
        Assert.False(p.AussenAendern("weg", a => GebaeudeTuer.Entfernen(a, s)));
        p.AussenAendern("Tuer", a => GebaeudeTuer.Dazu(a, s, einrasten: false));
        Assert.Equal(Texte.TuerSchonDa, GebaeudeTuer.DazuNicht(p.AussenStand, s));
    }
}
