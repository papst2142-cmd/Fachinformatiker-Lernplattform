using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.28.0 (Weisung P12-121, Nico 2a 3a, Sammelliste 260): Tuer selbst setzen. Geprueft: Tuer auf der gewaehlten
/// Wand (jede Seite, auch gedreht), mindestens 0,3 von der Ecke, Weg erreicht die Strasse, Ziehen um die Ecke ist ein
/// Schritt (Rueckgaengig), „Tür dazu“ beginnt an der Wand zur Strasse (Gegenprobe alte Regel: Vorderseite), Einrasten auf
/// eine Tuer des CC0-Modells, ein Ort mit Innenraum (Bitweiche) und das Zuhause laden im Spiel mit der neuen Tuer und die
/// Figur kommt von der Strasse hinein (Gegenprobe: alte Stelle).
/// </summary>
public class TuerSetzenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Projekt Leer() => Projekt.Neu("Tuer", D, Daten.NeueAblage(), null, (40, 40));

    private static Projekt Stadt1()
    {
        var p = Projekt.Neu("Tuer setzen", D, Daten.NeueAblage());
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        return p;
    }

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

    private static void Strasse(Projekt p, double x0, double y0, double x1, double y1) =>
        p.AussenAendern("Strasse", a => Aussen.Landschaft(a).Add(new PyObjekt
        {
            { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { x0, y0 }, new List<object?> { x1, y1 } } }, { "gehweg", 0.35 },
        }));

    private static List<Vector2> Weg(PyObjekt z) => [.. ((List<object?>)z["weg"]!).OfType<List<object?>>().Select(q => new Vector2((float)Py.F(q[0]), (float)Py.F(q[1])))];

    private static PyObjekt Zugang(Projekt p, AussenStelle s) =>
        s.Art == AussenArt.Haus ? AussenBearbeitung.Zugang(p.AussenStand, s.Ort)! : Blueprint.ZierwegVon(p.AussenStand, s)!;

    private static StadtSzene Szene(Projekt p) => Ansicht3D.Bauen(p.Stand(), D, Ansicht3D.Einstellung.Standard).Szene;

    private static bool AmEndeStrasse(Projekt p, AussenStelle s) =>
        Weg(Zugang(p, s))[^1] is var e && AussenBearbeitung.Strassen(p.AussenStand).Any(l => l.Count >= 2 && Kurve.Abstand(e, l) <= 0.02f);

    /// <summary>Seite der Tuer: Abstand zu den vier Waenden des Hauses.</summary>
    private static Vector2 Seite((double X, double Y, double W, double H) r, Vector2 t)
    {
        var seiten = new (float D, Vector2 N)[]
        {
            (MathF.Abs(t.Y - (float)(r.Y + r.H)), new(0, 1)), (MathF.Abs(t.Y - (float)r.Y), new(0, -1)),
            (MathF.Abs(t.X - (float)(r.X + r.W)), new(1, 0)), (MathF.Abs(t.X - (float)r.X), new(-1, 0)),
        };
        Assert.True(seiten.Min(k => k.D) <= 0.011f, $"Tuer {t} liegt auf keiner Wand von {r}");
        return seiten.MinBy(k => k.D).N;
    }

    public static TheoryData<int, int> SeitenUndDrehungen()
    {
        var d = new TheoryData<int, int>();
        for (var seite = 0; seite < 4; seite++)
        {
            for (var dreh = 0; dreh < 4; dreh++)
            {
                d.Add(seite, dreh);
            }
        }
        return d;
    }

    [Theory]
    [MemberData(nameof(SeitenUndDrehungen))]
    public void Setzen_TuerAufDerGewaehltenWand_WegZurStrasse_SpielZeichnetSie(int seite, int dreh)
    {
        // Fund 262: Regel „Tuer buendig“ hier aus (Tueren an CC0-Modellen ueber Fenstern/Nischen), sie pruefen TuerBuendigTests
        using var ohne262 = TuerBuendig.AusFuerTests();
        var p = Leer();
        Strasse(p, 2, 24, 38, 24);
        var s = Haus(p, "wohnung", 18, 18, dreh);
        var r = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        var mitte = new Vector2((float)(r.X + (r.W / 2)), (float)(r.Y + (r.H / 2)));
        var n = new[] { new Vector2(0, 1), new Vector2(1, 0), new Vector2(0, -1), new Vector2(-1, 0) }[seite];
        var tippen = mitte + (n * (float)((n.X != 0 ? r.W : r.H) / 2 + 0.3)) + (new Vector2(-n.Y, n.X) * 0.4f);
        var vorher = p.SchritteZurueck;
        var rotVorher = p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text).ToList();
        var hinweis = p.AussenAendern("Tuer setzen", a => TuerSetzen.Setzen(a, s, tippen));
        Assert.Null(hinweis);
        Assert.Equal(vorher + 1, p.SchritteZurueck);
        var tuer = TuerSetzen.Tuer(p.AussenStand, s)!.Value;
        Assert.Equal(n, Seite(r, tuer));
        Assert.Equal(n, AussenBearbeitung.HausTuerSeite(p.AussenStand, s));
        // laengs der Wand an der getippten Stelle (0,4 neben der Mitte)
        Assert.True(Vector2.Distance(tuer, mitte + (n * (float)((n.X != 0 ? r.W : r.H) / 2)) + (new Vector2(-n.Y, n.X) * 0.4f)) <= 0.02f, $"Tuer {tuer}");
        Assert.True(AmEndeStrasse(p, s), "Weg erreicht die Strasse nicht");
        // kein neuer roter Fund durch die Tuer; einzige bekannte Ausnahme (Sammelliste 265): der Baum des Modells „wohnung“
        // weicht im Spiel dem neuen Fussweg aus und landet im Haus (BaumPlaner), die Pruefung zeigt das rot
        var neuRot = p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text).Except(rotVorher).ToList();
        Assert.True(neuRot.Count == 0 || (seite, dreh) == (2, 3) && neuRot.SequenceEqual(["Ein Baum steht im Haus „wohnung“."]), string.Join("; ", neuRot));
        var t = Szene(p).Tueren.Where(t => Vector2.Distance(t.WegAnfang, tuer) <= 0.05f).ToList();
        Assert.True(t.Count == 1, $"Spiel zeichnet {t.Count} Tueren bei {tuer}");
        Assert.True(Vector2.Dot(t[0].Aussen, n) > 0.99f, $"Spiel-Tuer zeigt nach {t[0].Aussen}");
    }

    [Fact]
    public void Ecke_TuerMindestensEckAbstand()
    {
        var p = Leer();
        Strasse(p, 2, 24, 38, 24);
        var s = Haus(p, "wohnung", 18, 18);
        var r = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        // knapp neben der Ecke unten rechts, etwas mehr unterhalb als rechts = Suedwand
        var ecke = new Vector2((float)(r.X + r.W), (float)(r.Y + r.H));
        p.AussenAendern("Tuer setzen", a => TuerSetzen.Setzen(a, s, ecke + new Vector2(0.1f, 0.2f)));
        var tuer = TuerSetzen.Tuer(p.AussenStand, s)!.Value;
        Assert.Equal(new Vector2(0, 1), Seite(r, tuer));
        Assert.True(MathF.Abs(tuer.X - ecke.X - -TuerSetzen.EckAbstand) <= 0.011f, $"Tuer {tuer}, Ecke {ecke}");
    }

    [Fact]
    public void Ziehen_UmDieEcke_JeEinSchritt_Rueckgaengig()
    {
        var p = Leer();
        Strasse(p, 2, 24, 38, 24);
        var s = Haus(p, "wohnung", 18, 18);
        var r = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        p.AussenAendern("Tuer dazu", a => GebaeudeTuer.Dazu(a, s, einrasten: false));
        var sued = TuerSetzen.Tuer(p.AussenStand, s)!.Value;
        Assert.Equal(new Vector2(0, 1), Seite(r, sued));
        // am Tablet: Finger auf der Tuer, an der Suedwand entlang nach rechts und um die Ecke hinauf
        var ziel = new Vector2((float)(r.X + r.W + 0.5), (float)(r.Y + 0.8));
        var vorher = p.SchritteZurueck;
        p.AussenAendern("Tuer setzen", a => TuerSetzen.Setzen(a, s, ziel));
        Assert.Equal(vorher + 1, p.SchritteZurueck);
        var ost = TuerSetzen.Tuer(p.AussenStand, s)!.Value;
        Assert.Equal(new Vector2(1, 0), Seite(r, ost));
        Assert.True(AmEndeStrasse(p, s));
        Assert.True(p.Rueckgaengig());
        Assert.Equal(sued, TuerSetzen.Tuer(p.AussenStand, s)!.Value);
    }

    [Fact]
    public void Dazu_BeginntAnDerWandZurStrasse_GegenprobeVorderseite()
    {
        var p = Leer();
        // Strasse noerdlich des Hauses (kleineres y), die Vorderseite (Sued) zeigt weg von ihr
        Strasse(p, 2, 12, 38, 12);
        var s = Haus(p, "wohnung", 18, 18);
        var r = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        p.AussenAendern("Tuer dazu", a => GebaeudeTuer.Dazu(a, s, einrasten: false));
        Assert.Equal(new Vector2(0, -1), Seite(r, TuerSetzen.Tuer(p.AussenStand, s)!.Value));
        // Gegenprobe: alte Regel (immer Vorderseite) setzte sie nach Sueden, weg von der Strasse
        var q = Leer();
        Strasse(q, 2, 12, 38, 12);
        var s2 = Haus(q, "wohnung", 18, 18);
        GebaeudeTuer.SuedAltFuerTests.Value = true;
        try
        {
            q.AussenAendern("Tuer dazu", a => GebaeudeTuer.Dazu(a, s2, einrasten: false));
        }
        finally
        {
            GebaeudeTuer.SuedAltFuerTests.Value = false;
        }
        Assert.Equal(new Vector2(0, 1), Seite(r, TuerSetzen.Tuer(q.AussenStand, s2)!.Value));
    }

    [Fact]
    public void AufWand_RastetAufModellTuerEin_NurInDerFangweite()
    {
        var r = (10.0, 10.0, 4.0, 2.0);
        var modellTuer = new Vector2(12.8f, 12.0f);
        // 0,5 daneben auf derselben Wand: rastet ein
        Assert.Equal(modellTuer, TuerSetzen.AufWand(r, new Vector2(12.3f, 12.3f), [modellTuer]).Punkt);
        // 0,8 daneben: bleibt
        Assert.Equal(new Vector2(12.0f, 12.0f), TuerSetzen.AufWand(r, new Vector2(12.0f, 12.3f), [modellTuer]).Punkt);
        // andere Wand: rastet nie auf eine Tuer der Suedwand
        Assert.Equal(new Vector2(12.8f, 10.0f), TuerSetzen.AufWand(r, new Vector2(12.8f, 9.7f), [modellTuer]).Punkt);
        // Gegenprobe ohne Modell-Tueren
        Assert.Equal(new Vector2(12.3f, 12.0f), TuerSetzen.AufWand(r, new Vector2(12.3f, 12.3f)).Punkt);
    }

    [Fact]
    public void ModellTueren_Moebelhaus_TuerDesModellsImNorden()
    {
        var p = Stadt1();
        var s = new AussenStelle(AussenArt.Haus, -1, 0, "moebelhaus");
        var r = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        var tueren = TuerSetzen.ModellTuerMitten(p.SzeneOhneLaternen(p.AussenStand)!, D.Grafik, r);
        Assert.Contains(tueren, t => MathF.Abs(t.Y - (float)r.Y) <= 0.4f && t.X > r.X && t.X < r.X + r.W);
    }

    public static TheoryData<string> Orte() => ["bitweiche", "zuhause", "moebelhaus"];

    /// <summary>
    /// Nico 3a: Ort mit Innenraum (und das Zuhause): die neue Tuer steht als zugaenge_neu, die Stadt laedt im Spiel ohne
    /// roten Fund, das Spiel setzt die Tuer an die neue Wand, und die Figur kommt von der Strasse bis vor die neue Tuer
    /// (Gegenprobe: die alte Tuer gibt es nicht mehr).
    /// </summary>
    [Theory]
    [MemberData(nameof(Orte))]
    public void Ort_MitInnenraum_SpielLaedtNeueTuer_FigurKommtHinein(string ort)
    {
        var p = Stadt1();
        var s = new AussenStelle(AussenArt.Haus, -1, 0, ort);
        var alt = TuerSetzen.Tuer(p.AussenStand, s)!.Value;
        var r = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        // die Westwand (Fassung 0: keine dieser Tueren liegt im Westen), etwas unter der Mitte
        var tippen = new Vector2((float)r.X - 0.3f, (float)(r.Y + (r.H / 2)));
        p.AussenAendern("Tuer setzen", a => TuerSetzen.Setzen(a, s, tippen));
        var neu = TuerSetzen.Tuer(p.AussenStand, s)!.Value;
        Assert.Equal(new Vector2(-1, 0), Seite(r, neu));
        Assert.True(Vector2.Distance(alt, neu) > 1f);
        Assert.True(Py.Hat((PyObjekt)Get(Aussen.Stadtdaten(p.AussenStand), "zugaenge_neu")!, ort));
        Assert.True(AmEndeStrasse(p, s), "Weg erreicht die Strasse nicht");
        Assert.Empty(p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text));
        var szene = Szene(p);
        var tuer = Assert.Single(szene.Tueren, t => t.Ort == ort);
        Assert.True(Vector2.Distance(tuer.Mitte, neu) <= 0.6f, $"Spiel-Tuer {tuer.Mitte}, gesetzt {neu}");
        Assert.True(Vector2.Dot(tuer.Aussen, new Vector2(-1, 0)) > 0.99f);
        Assert.DoesNotContain(szene.Tueren, t => t.Ort == ort && Vector2.Distance(t.Mitte, alt) <= 0.6f);
        var o = szene.Ort(ort)!;
        Assert.True(Vector2.Distance(new Vector2(o.TuerX, o.TuerZ), tuer.WegAnfang) <= 0.6f, $"Ort betritt man nicht durch die neue Tuer: {o.TuerX} {o.TuerZ}, Tuer {tuer.Mitte} {tuer.WegAnfang}");
        // die Figur kommt vom Ende des Wegs (an der Strasse) bis vor die neue Tuer
        var weg = Weg(Zugang(p, s));
        var raster = szene.Laufraster;
        var vor = tuer.WegAnfang + (tuer.Aussen * 0.1f);
        Assert.True(raster.KannStehen(vor.X, vor.Y), "vor der Tuer kann die Figur nicht stehen");
        var pfad = raster.Weg((weg[^1].X, weg[^1].Y), (vor.X, vor.Y));
        Assert.NotEmpty(pfad);
        Assert.True(Vector2.Distance(new Vector2((float)pfad[^1].X, (float)pfad[^1].Y), vor) <= 0.3f, "Weg endet nicht vor der Tuer");
        // der Tuerversatz des CC0-Modells faellt weg (sonst saesse die Modell-Tuer auf dem alten Weg)
        Assert.Null(Get(Objekt(Get(Objekt(Get(Aussen.Stadtdaten(p.AussenStand), "darstellung"))!, "modell_tuer") ?? new PyObjekt()), ort));
    }
}
