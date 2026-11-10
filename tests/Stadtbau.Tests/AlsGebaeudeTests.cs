using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.31.0 (Weisung P12-130 Frage 2 = a, P12-140 Punkt 2): „Als Gebäude übernehmen“ an einem Katalog-Stueck. Das Stueck
/// wird ein Gebaeude ohne Ort mit Tuer, Weg zur Strasse und Innenraum, sieht in 3D genau so aus wie vorher (gleiche
/// Dreiecke, gleicher Kasten), kein roter Fund; danach gehen „Tür setzen“, Innenraum und „Als Blueprint speichern“, der
/// Blueprint setzt dasselbe Modell an anderer Stelle, Rueckgaengig stellt das Stueck wieder her. Gegenprobe: ein Effekt
/// wird kein Gebaeude.
/// </summary>
public class AlsGebaeudeTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Projekt Leer() => Projekt.Neu("Als Gebaeude", D, Daten.NeueAblage(), null, (40, 40));

    /// <summary>Ein Gebaeude aus dem Katalog (Gruppe mit Haeusern, mindestens 1,5 breit und tief).</summary>
    private static KatalogEintrag Haus => Katalog.Aktiv!.Eintraege.First(k => k.Art == KatalogArt.Modell && k.Paket == "industrial" && k.Name.StartsWith("building", StringComparison.Ordinal)
        && k.Aussen[0] >= 1.5 && k.Aussen[1] >= 1.5);

    private static KatalogEintrag Effekt => Katalog.Aktiv!.Eintraege.First(k => k.Art == KatalogArt.Effekt);

    private static void Strasse(Projekt p, double y) =>
        p.AussenAendern("Strasse", a => Aussen.Landschaft(a).Add(new PyObjekt
        {
            { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { 2.0, y }, new List<object?> { 38.0, y } } }, { "gehweg", 0.35 },
        }));

    private static AussenStelle Stueck(Projekt p, KatalogEintrag e, double x, double y, int dreh = 0) =>
        p.AussenAendern("Stueck", a =>
        {
            var s = AussenBearbeitung.Setzen(a, AussenArt.Katalog, x, y, raster: false, modell: e.Id)!;
            for (var i = 0; i < dreh; i++)
            {
                AussenBearbeitung.Drehen(a, s);
            }
            return s;
        });

    private static StadtSzene Szene(Projekt p) => Ansicht3D.Bauen(p.Stand(), D, Ansicht3D.Einstellung.Standard).Szene;

    /// <summary>Dreiecke aller Netze der Szene in einem Rechteck (Karte) ueber dem Boden, und ihr Kasten (min, max).</summary>
    private static (Vector3 Min, Vector3 Max, int Dreiecke) Inhalt(StadtSzene sz, Vector2 a, Vector2 b)
    {
        var (min, max, n) = (new Vector3(float.MaxValue), new Vector3(float.MinValue), 0);
        var netze = new List<Netz> { sz.NetzBauen(), sz.PaketNetzBauen(false) };
        netze.AddRange(sz.Haeuser.Select(sz.HausNetz));
        foreach (var netz in netze)
        {
            var e = netz.Ecken;
            var ix = netz.Indizes;
            Vector3 P(uint i) => new(e[(int)i * Netz.Zahlen], e[((int)i * Netz.Zahlen) + 1], e[((int)i * Netz.Zahlen) + 2]);
            for (var i = 0; i + 2 < ix.Count; i += 3)
            {
                var ecken = new[] { P(ix[i]), P(ix[i + 1]), P(ix[i + 2]) };
                if (ecken.All(q => q.X >= a.X && q.X <= b.X && q.Z >= a.Y && q.Z <= b.Y && q.Y > 0.06f))
                {
                    n++;
                    foreach (var q in ecken)
                    {
                        min = Vector3.Min(min, q);
                        max = Vector3.Max(max, q);
                    }
                }
            }
        }
        return (min, max, n);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Katalogstueck_WirdGebaeude_MitTuerUndInnenraum_GleichesAussehen_KeinRot(int dreh)
    {
        var p = Leer();
        Strasse(p, 25);
        var e = Haus;
        var s = Stueck(p, e, 20, 21, dreh);
        var (w, t) = AussenBearbeitung.KatalogGroesse(p.AussenStand, s);
        var (ka, kb) = (new Vector2((float)(20 - (w / 2) - 0.05), (float)(21 - (t / 2) - 0.05)), new Vector2((float)(20 + (w / 2) + 0.05), (float)(21 + (t / 2) + 0.05)));
        var vorher = Inhalt(Szene(p), ka, kb);
        Assert.True(vorher.Dreiecke > 50, "das Katalog-Stueck wird gezeichnet");
        string? grund = null, hinweis = null;
        var g = p.AussenAendern("Als Gebaeude", a => AussenBearbeitung.AlsGebaeude(a, s, out grund, out hinweis));
        Assert.Null(grund);
        Assert.Null(hinweis);
        Assert.NotNull(g);
        Assert.Equal(AussenArt.Gebaeude, g.Art);
        Assert.Empty(Aussen.KatalogStuecke(p.AussenStand));
        // Tuer, Weg zur Strasse, Innenraum
        Assert.True(GebaeudeTuer.HatTuer(p.AussenStand, g));
        var modell = Py.Str(((PyObjekt)Aussen.Landschaft(p.AussenStand)[g.Nummer]!)["modell"]);
        Assert.StartsWith(Bauteile.ModellPraefix, modell, StringComparison.Ordinal);
        Assert.Contains(Projekt.BauPraefix + modell, p.Innenorte);
        Assert.Equal(Projekt.BauPraefix + modell, p.InnenortVon(g));
        Assert.DoesNotContain(p.Pruefen(Projekt.BauPraefix + modell), f => f.Rot);
        // Tuer liegt auf der Wand zur Strasse (Sueden)
        var (x, y, bw, bh) = AussenBearbeitung.HausFlaeche(p.AussenStand, g);
        var z = Blueprint.ZierwegVon(p.AussenStand, g)!;
        var tuer = (List<object?>)z["tuer"]!;
        Assert.Equal(y + bh, Py.F(tuer[1]), 2);
        Assert.InRange(Py.F(tuer[0]), x, x + bw);
        // gleiche Grundflaeche, gleiches Aussehen
        Assert.Equal(20 - (w / 2), x, 3);
        Assert.Equal(21 - (t / 2), y, 3);
        var sz = Szene(p);
        Assert.Contains(sz.Tueren, d => Vector2.Distance(d.WegAnfang, new Vector2((float)Py.F(tuer[0]), (float)Py.F(tuer[1]))) <= 0.05f);
        var nachher = Inhalt(sz, ka, kb);
        Assert.True(nachher.Dreiecke >= vorher.Dreiecke, $"{nachher.Dreiecke} < {vorher.Dreiecke}");
        Assert.True(Vector3.Distance(vorher.Min, nachher.Min) <= 0.01f, $"{vorher.Min} {nachher.Min}");
        // die Tuer (Kasten 0,02 vor der Wand) ragt an der Strassenseite etwas heraus, sonst gleich
        Assert.True(Vector3.Distance(vorher.Max, nachher.Max) <= 0.03f, $"{vorher.Max} {nachher.Max}");
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot);
        // Tuer setzen geht, Rueckgaengig stellt das Stueck wieder her
        Assert.Null(TuerSetzen.Nicht(p.AussenStand, g));
        p.AussenAendern("Tuer", a => TuerSetzen.Setzen(a, g, new Vector2((float)(x + (bw / 2)), (float)y)));
        Assert.Equal(y, Py.F(((List<object?>)Blueprint.ZierwegVon(p.AussenStand, g)!["tuer"]!)[1]), 2);
        Assert.True(p.Rueckgaengig());
        Assert.True(p.Rueckgaengig());
        Assert.Single(Aussen.KatalogStuecke(p.AussenStand));
        Assert.Null(Get(Aussen.Bauten(p.AussenStand), modell));
    }

    [Fact]
    public void AlsBlueprint_SetztDasselbeModellAnAndererStelle()
    {
        var p = Leer();
        Strasse(p, 25);
        var s = Stueck(p, Haus, 20, 21);
        var g = p.AussenAendern("Als Gebaeude", a => AussenBearbeitung.AlsGebaeude(a, s, out _, out _))!;
        var b = Blueprint.Aus(p, g, "Halle aus dem Katalog")!;
        Assert.NotNull(b.Bauteile);
        Assert.NotNull(b.Innenraum);
        Assert.NotNull(b.Tuer);
        var q = Leer();
        Strasse(q, 15);
        string? grund = null;
        var neu = q.AussenAendern("Blueprint", a => b.Setzen(a, 10, 11, out grund));
        Assert.Null(grund);
        Assert.NotNull(neu);
        var gm1 = Ansicht3D.MitGebaeuden(D.Grafik, p.Stand())!.Modell(b.Modell)!;
        var gm2 = Ansicht3D.MitGebaeuden(D.Grafik, q.Stand())!.Modell(b.Modell)!;
        Assert.Equal(gm1.Ecken, gm2.Ecken);
        Assert.Contains(Projekt.BauPraefix + b.Modell, q.Innenorte);
        Assert.DoesNotContain(q.PruefenAussen(), f => f.Rot);
    }

    [Fact]
    public void Modell_GleichDemKatalogstueck_ZweitesStueck_EigenerName()
    {
        var p = Leer();
        Strasse(p, 25);
        var e = Haus;
        var s1 = Stueck(p, e, 10, 21);
        var g1 = p.AussenAendern("1", a => AussenBearbeitung.AlsGebaeude(a, s1, out _, out _))!;
        var s2 = Stueck(p, e, 25, 21);
        var g2 = p.AussenAendern("2", a => AussenBearbeitung.AlsGebaeude(a, s2, out _, out _))!;
        var m1 = Py.Str(((PyObjekt)Aussen.Landschaft(p.AussenStand)[g1.Nummer]!)["modell"]);
        var m2 = Py.Str(((PyObjekt)Aussen.Landschaft(p.AussenStand)[g2.Nummer]!)["modell"]);
        Assert.NotEqual(m1, m2);
        Assert.Equal(m1 + "_2", m2);
        // das zusammengesetzte Modell hat genau die Dreiecke des Katalog-Modells
        var gm = Ansicht3D.MitGebaeuden(D.Grafik, p.Stand())!.Modell(m1)!;
        Assert.Equal(Katalog.Aktiv!.Modelle(e.Paket)[e.Name].Dreiecke, gm.Dreiecke);
        Assert.Equal(2, p.Bauten.Count);
    }

    [Fact]
    public void Effekt_WirdKeinGebaeude_Gegenprobe()
    {
        var p = Leer();
        var s = Stueck(p, Effekt, 20, 20);
        Assert.Equal(Texte.AlsGebaeudeEffekt, AussenBearbeitung.AlsGebaeudeNicht(p.AussenStand, s));
        Assert.Null(AussenBearbeitung.AlsGebaeude(p.AussenStand, s, out var grund, out _));
        Assert.Equal(Texte.AlsGebaeudeEffekt, grund);
        Assert.Single(Aussen.KatalogStuecke(p.AussenStand));
        // und nur ein Katalog-Stueck: ein Baum wird kein Gebaeude
        Assert.Equal(Texte.TuerNurGebaeude, AussenBearbeitung.AlsGebaeudeNicht(p.AussenStand, new AussenStelle(AussenArt.Baum, 0)));
    }

    [Fact]
    public void GanzId_HinUndZurueck_FalscheKennung_Null()
    {
        var id = Bauteile.GanzId("industrial/building-a", 2.5, 1.75);
        Assert.Equal((2.5f, 1.75f, "industrial/building-a"), Bauteile.GanzLesen(id));
        Assert.Null(Bauteile.GanzLesen("ganz:x:1:industrial/a"));
        Assert.Null(Bauteile.GanzLesen("ganz:1:1:ohne-paket"));
        Assert.Null(Bauteile.GanzLesen("cc0:modular/building-block"));
        Assert.Same(Bauteile.GanzArt, new Bauteile.Teil(id, 0, 0, 0, 0).Art);
    }
}
