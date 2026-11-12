using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.46.0 / A 0.70.0 (Weisung P12-220, Sammelliste 324): Bahnsteig mit Bahnhof verknuepfen. Nicos Teststadt: nach
/// „Tür dazu“ am Bahnhof steht der Bahnsteig als eigenes Stueck daneben, der Bahnhof Talheim meldete gelb „hat keinen
/// Bahnsteig mehr“. Je Regel rot und gruen mit Gegenprobe.
/// </summary>
public class BahnsteigVerknuepfungTests
{
    private static Spieldaten D => Daten.Spiel;

    private const string KeinBahnsteigTalheim = "„Bahnhof Talheim“ hat keinen Bahnsteig mehr.";

    /// <summary>Nicos Teststadt mit allen Spielorten (wie im Bild der Weisung: Bahnhof Talheim, Bahnsteig daneben).</summary>
    private static Projekt NicoMitOrten()
    {
        var p = TorEingangTests.Nico();
        foreach (var (modell, ort) in SpielortTests.Zuordnung)
        {
            Assert.Null(SpielortTests.Festlegen(p, modell, ort));
        }
        return p;
    }

    /// <summary>Das Bahnsteig-Stueck neben dem Bahnhof Talheim (abgetrennt von „Tür dazu“).</summary>
    private static AussenStelle BahnsteigStelle(Projekt p)
    {
        var land = Aussen.Landschaft(p.AussenStand);
        var i = land.FindIndex(t => t is PyObjekt g && Py.Gleich(g["typ"], "gebaeude") && BahnsteigVerknuepfung.BahnsteigBei(p.AussenStand, new AussenStelle(AussenArt.Gebaeude, land.IndexOf(t))) is not null);
        Assert.True(i >= 0, "kein Bahnsteig-Stueck in Nicos Teststadt");
        return new AussenStelle(AussenArt.Gebaeude, i);
    }

    private static PyObjekt Stueck(Projekt p, AussenStelle s) => (PyObjekt)Aussen.Landschaft(p.AussenStand)[s.Nummer]!;

    private static string? Verknuepfen(Projekt p, AussenStelle s, string ort)
    {
        string? grund = null;
        p.AussenAendern("verknuepfen", a => grund = BahnsteigVerknuepfung.Verknuepfen(a, s, ort, D.Fassung));
        return grund;
    }

    private static List<StadtbauFund> Funde(Projekt p) => p.PruefenAussen();

    private static T Alt<T>(Func<T> f)
    {
        StadtbauPruefung.BahnsteigVerknuepfungAltFuerTests = true;
        try
        {
            return f();
        }
        finally
        {
            StadtbauPruefung.BahnsteigVerknuepfungAltFuerTests = false;
        }
    }

    [Fact]
    public void Nico_BahnsteigDaneben_HinweisNenntKnopf_NachVerknuepfenWeg_Rueckgaengig()
    {
        var p = NicoMitOrten();
        var funde = Funde(p);
        var hinweis = Assert.Single(funde, f => f.Text == KeinBahnsteigTalheim);
        Assert.False(hinweis.Rot);
        Assert.Contains("Mit Bahnhof verknüpfen", hinweis.Tipp, StringComparison.Ordinal);
        // Gegenprobe: vorher nannte der Hinweis nur „Altes Aussehen zurück“
        Assert.DoesNotContain("verknüpfen", Alt(() => Funde(p)).Single(f => f.Text == KeinBahnsteigTalheim).Tipp, StringComparison.Ordinal);

        var rotVorher = funde.Where(f => f.Rot).Select(f => f.Text).ToList();
        var s = BahnsteigStelle(p);
        var schritte = p.SchritteZurueck;
        Assert.Null(Verknuepfen(p, s, "talheim"));
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        Assert.Equal("talheim", Bahnsteig.BahnhofVon(Stueck(p, s)));
        var nachher = Funde(p);
        Assert.DoesNotContain(nachher, f => f.Text.Contains("keinen Bahnsteig", StringComparison.Ordinal));
        Assert.Equal(rotVorher, nachher.Where(f => f.Rot).Select(f => f.Text).ToList());
        // Gegenprobe: ohne die neue Regel bliebe der Hinweis
        Assert.Contains(Alt(() => Funde(p)), f => f.Text == KeinBahnsteigTalheim);

        p.Rueckgaengig();
        Assert.Null(Bahnsteig.BahnhofVon(Stueck(p, s)));
        Assert.Contains(Funde(p), f => f.Text == KeinBahnsteigTalheim);
    }

    [Fact]
    public void Loesen_AmBahnsteigUndAmBahnhof_EinSchritt_KopieOhneVerknuepfung()
    {
        var p = NicoMitOrten();
        var s = BahnsteigStelle(p);
        Assert.Null(Verknuepfen(p, s, "talheim"));
        Assert.Equal([s.Nummer], BahnsteigVerknuepfung.BahnsteigeVon(p.AussenStand, "talheim"));

        // Kopie gehoert zu keinem Bahnhof (hoechstens ein Bahnhof je Bahnsteig, die Kopie ist ein neues Stueck)
        p.AussenAendern("kopieren", a => AussenBearbeitung.Kopieren(a, s));
        Assert.Equal([s.Nummer], BahnsteigVerknuepfung.BahnsteigeVon(p.AussenStand, "talheim"));
        p.Rueckgaengig();

        var schritte = p.SchritteZurueck;
        p.AussenAendern("loesen", a => BahnsteigVerknuepfung.Loesen(a, new AussenStelle(AussenArt.Haus, -1, 0, "talheim")));
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        Assert.Empty(BahnsteigVerknuepfung.BahnsteigeVon(p.AussenStand, "talheim"));
        p.Rueckgaengig();
        p.AussenAendern("loesen", a => BahnsteigVerknuepfung.Loesen(a, s));
        Assert.Null(Bahnsteig.BahnhofVon(Stueck(p, s)));
    }

    [Fact]
    public void Gruende_KeinBahnsteig_KeinBahnhof_NichtsGeaendert()
    {
        var p = NicoMitOrten();
        var s = BahnsteigStelle(p);
        var schritte = p.SchritteZurueck;
        Assert.Equal(Texte.BahnsteigKeinBahnhof("Zuhause"), Verknuepfen(p, s, "zuhause"));
        var haus = TorEingangTests.Gebaeude(p, "lokschuppen");
        Assert.True(haus.Nummer < 0 || BahnsteigVerknuepfung.BahnsteigBei(p.AussenStand, haus) is null);
        var land = Aussen.Landschaft(p.AussenStand);
        var keinBahnsteig = new AussenStelle(AussenArt.Gebaeude, land.FindIndex(t => t is PyObjekt g && Py.Gleich(g["typ"], "gebaeude")
            && BahnsteigVerknuepfung.BahnsteigBei(p.AussenStand, new AussenStelle(AussenArt.Gebaeude, land.IndexOf(t))) is null));
        Assert.Equal(Texte.BahnsteigNurBahnsteig, Verknuepfen(p, keinBahnsteig, "talheim"));
        // Gegenprobe: ein Bahnhof geht
        Assert.True(BahnsteigVerknuepfung.IstBahnhof(p.AussenStand, "talheim", D.Fassung));
        Assert.True(BahnsteigVerknuepfung.IstBahnhof(p.AussenStand, "birkenhain", D.Fassung));
        Assert.False(BahnsteigVerknuepfung.IstBahnhof(p.AussenStand, "zuhause", D.Fassung));
        Assert.Equal(schritte, p.SchritteZurueck);
        Assert.All(BahnsteigVerknuepfung.Bahnhoefe(p.AussenStand, D.Fassung), b => Assert.NotEqual("talheim_nord", b.Id));
    }

    [Fact]
    public void Regel_UnbekannterBahnhof_Rot_Gegenprobe_Bekannt()
    {
        var p = NicoMitOrten();
        var s = BahnsteigStelle(p);
        p.AussenAendern("falsch", a => Py.Setzen((PyObjekt)Aussen.Landschaft(a)[s.Nummer]!, Bahnsteig.BahnhofFeld, "gibt_es_nicht"));
        Assert.Contains(Funde(p), f => f.Rot && f.Text.Contains("unbekannten Bahnhof", StringComparison.Ordinal));
        p.Rueckgaengig();
        Assert.Null(Verknuepfen(p, s, "talheim"));
        Assert.DoesNotContain(Funde(p), f => f.Text.Contains("unbekannten Bahnhof", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel_NichtAmGleis_Rot_Gegenprobe_AmGleis()
    {
        var p = NicoMitOrten();
        var s = BahnsteigStelle(p);
        Assert.Null(Verknuepfen(p, s, "talheim"));
        Assert.DoesNotContain(Funde(p), f => f.Text.Contains("liegt nicht am Gleis", StringComparison.Ordinal));
        // vom Gleis weg (quer zum Gleis), 2 Felder
        var g = Stueck(p, s);
        var mitte = Bahnsteig.MitteVon(g, Get(AussenBearbeitung.Modelle(p.AussenStand), Py.Str(g["modell"])))!.Value;
        var szene = p.SzeneOhneLaternen(p.AussenStand, true)!;
        var gs = StadtSzene.NaechstesGleisStueck(szene.Gleise, mitte)!.Value;
        var weg = Vector2.Normalize(mitte - gs.Punkt) * 2;
        p.AussenAendern("weg vom Gleis", a => AussenBearbeitung.Verschieben(a, s, weg.X, weg.Y, raster: false));
        Assert.Contains(Funde(p), f => f.Rot && f.Text == "Der Bahnsteig von „Bahnhof Talheim“ liegt nicht am Gleis.");
        // Gegenprobe: ohne Verknuepfung kein solcher Fund
        p.AussenAendern("loesen", a => BahnsteigVerknuepfung.Loesen(a, s));
        Assert.DoesNotContain(Funde(p), f => f.Text.Contains("liegt nicht am Gleis", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel_NichtErreichbar_Rot_Gegenprobe_OhneGleisRing()
    {
        var p = NicoMitOrten();
        var s = BahnsteigStelle(p);
        Assert.Null(Verknuepfen(p, s, "talheim"));
        // ein geschlossener Gleisring ohne Uebergang auf freier Wiese; der Bahnsteig innen an seinem unteren Gleis
        var ring = new PyObjekt
        {
            { "typ", "bahn" },
            { "punkte", new List<object?> { L(48, 23), L(57, 23), L(57, 32), L(48, 32), L(48, 23) } },
            { "radien", new List<object?> { 3L, 3L, 3L, 3L, 3L } },
        };
        var g = Stueck(p, s);
        var mitte = Bahnsteig.MitteVon(g, Get(AussenBearbeitung.Modelle(p.AussenStand), Py.Str(g["modell"])))!.Value;
        var r = Bahnsteig.Rechtecke(Get(AussenBearbeitung.Modelle(p.AussenStand), Py.Str(g["modell"])), Py.F(g["x"]), Py.F(g["y"]), 0);
        var halbHoehe = (r.Max(q => q.Max.Y) - r.Min(q => q.Min.Y)) / 2;
        var ziel = new Vector2(52.5f, 23f + 0.3f + halbHoehe);
        static bool Unerreicht(StadtbauFund f) => f.Rot && f.Text == "Der Bahnsteig von „Bahnhof Talheim“ ist vom Bahnhof zu Fuß nicht erreichbar.";
        p.AussenAendern("Gleisring", a => Aussen.Landschaft(a).Add(Py.Kopie(ring)));
        // Gegenprobe: am alten Platz (neben dem Bahnhof) ist der Bahnsteig erreichbar, der Ring allein macht keinen Fund
        Assert.DoesNotContain(Funde(p), Unerreicht);
        p.AussenAendern("in den Ring", a => AussenBearbeitung.Verschieben(a, s, ziel.X - mitte.X, ziel.Y - mitte.Y, raster: false));
        var funde = Funde(p);
        Assert.Contains(funde, Unerreicht);
        Assert.DoesNotContain(funde, f => f.Text.Contains("liegt nicht am Gleis", StringComparison.Ordinal));
        // ohne Verknuepfung kein solcher Fund
        p.AussenAendern("loesen", a => BahnsteigVerknuepfung.Loesen(a, s));
        Assert.DoesNotContain(Funde(p), Unerreicht);
    }

    private static List<object?> L(double x, double y) => [x, y];

    [Fact]
    public void Regel_KeinBahnhofRolle_Rot_Gegenprobe_Bahnhof()
    {
        var p = NicoMitOrten();
        var s = BahnsteigStelle(p);
        // Talheim-Nord fehlt in Nicos Teststadt; die Regel gilt fuer jeden Ort aus Bahnsteig.OhneBahnsteig: hier an einem Ort mit dieser Kennung
        p.AussenAendern("talheim_nord", a =>
        {
            var o = AussenBearbeitung.Ort(a, "birkenhain")!;
            Py.Setzen((PyObjekt)Aussen.Landschaft(a)[s.Nummer]!, Bahnsteig.BahnhofFeld, "birkenhain");
            Py.Setzen(o, "id", "talheim_nord");
            Py.Setzen((PyObjekt)Aussen.Landschaft(a)[s.Nummer]!, Bahnsteig.BahnhofFeld, "talheim_nord");
        });
        Assert.Contains(Funde(p), f => f.Rot && f.Text.Contains("ist kein Bahnhof und hat keinen Bahnsteig", StringComparison.Ordinal));
        p.Rueckgaengig();
        Assert.Null(Verknuepfen(p, s, "birkenhain"));
        Assert.DoesNotContain(Funde(p), f => f.Text.Contains("ist kein Bahnhof", StringComparison.Ordinal));
    }

    [Fact]
    public void OhneBahnsteig_GleichDenOrtenMitNeuerRolle()
    {
        Assert.Equal(Spielort.Neu.Keys.Order(StringComparer.Ordinal), Bahnsteig.OhneBahnsteig.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Spiel_ZugHaeltAmVerknuepftenBahnsteig_Gegenprobe_LaengstesStueck()
    {
        var p = NicoMitOrten();
        var s = BahnsteigStelle(p);
        Assert.Null(Verknuepfen(p, s, "talheim"));
        var g = Stueck(p, s);
        var mitte = Bahnsteig.MitteVon(g, Get(AussenBearbeitung.Modelle(p.AussenStand), Py.Str(g["modell"])))!.Value;
        Vector2 ZugMitte()
        {
            var sz = Ansicht3D.Bauen(p.Stand(), D, new Ansicht3D.Einstellung(false, Jahreszeit.Sommer, null)).Szene;
            var wagen = sz.Stuecke.Where(w => w.Name.StartsWith("train-electric-city-", StringComparison.Ordinal)).Select(w => w.Lage.Translation).ToList();
            Assert.Equal(3, wagen.Count);
            var m = wagen.Aggregate(Vector3.Zero, (a, b) => a + b) / wagen.Count;
            return new Vector2(m.X, m.Z);
        }
        var halt = ZugMitte();
        Assert.True(Vector2.Distance(halt, mitte) < 1.5f, $"Zug {halt}, Bahnsteig {mitte}");
        StadtSzene.ZugHaltAltFuerTests = true;
        try
        {
            Assert.True(Vector2.Distance(ZugMitte(), halt) > 0.5f, "Gegenprobe: ohne Verknuepfung steht der Zug woanders");
        }
        finally
        {
            StadtSzene.ZugHaltAltFuerTests = false;
        }
        // Stand der Stadt (wie die Stadt-Datei; Nicos Teststadt hat eigene rote Funde, darum ohne Export): Ladepruefung ohne Fund
        var st = p.Stand();
        Assert.Equal([], StadtbauPruefung.Verbaut(st, null).Where(f => f.Contains("Bahnsteig", StringComparison.Ordinal)));
        Assert.Contains(ListeOderLeer(Objekt(st.Karte["welt"]), "landschaft").OfType<PyObjekt>(), t => Bahnsteig.BahnhofVon(t) == "talheim");
    }

    [Fact]
    public void Ladepruefung_UnbekannterBahnhof_Fund_Gegenprobe_Fassung0()
    {
        Assert.DoesNotContain(StadtbauPruefung.Verbaut(D.Fassung, null), f => f.Contains("Bahnsteig", StringComparison.Ordinal));
        var p = NicoMitOrten();
        var s = BahnsteigStelle(p);
        Assert.Null(Verknuepfen(p, s, "talheim"));
        var st = p.Stand();
        Assert.DoesNotContain(StadtbauPruefung.Verbaut(st, null), f => f.Contains("Bahnsteig", StringComparison.Ordinal));
        var stueck = ListeOderLeer(Objekt(st.Karte["welt"]), "landschaft").OfType<PyObjekt>().Single(t => Bahnsteig.BahnhofVon(t) == "talheim");
        Py.Setzen(stueck, Bahnsteig.BahnhofFeld, 3L);
        Assert.Contains(StadtbauPruefung.Verbaut(st, null), f => f.Contains("unbekannten Bahnhof", StringComparison.Ordinal));
    }

    [Fact]
    public void RcKandidat_Stadt1_VerknuepfenNimmtDenHinweis_OhneNeuesRot()
    {
        // Nicos RC-Kandidat fuer Stadt 1 (12.11.2026, S 0.45.0): Bahnhof Talheim mit dem abgetrennten Bahnsteig daneben
        var p = LaufzeitenTests.Rc();
        var vorher = Funde(p);
        var hinweis = Assert.Single(vorher, f => f.Text == KeinBahnsteigTalheim);
        Assert.Contains("Mit Bahnhof verknüpfen", hinweis.Tipp, StringComparison.Ordinal);
        // Stellwerk Talheim-Nord: kein Hinweis „keinen Bahnsteig“, keine „wechselnden Teile“ nur wegen des Geruests
        Assert.DoesNotContain(vorher, f => f.Text.Contains("Stellwerk Talheim-Nord", StringComparison.Ordinal));
        Assert.Null(Verknuepfen(p, BahnsteigStelle(p), "talheim"));
        var nachher = Funde(p);
        Assert.DoesNotContain(nachher, f => f.Text == KeinBahnsteigTalheim);
        Assert.Equal(vorher.Where(f => f.Rot).Select(f => f.Text).Order(StringComparer.Ordinal), nachher.Where(f => f.Rot).Select(f => f.Text).Order(StringComparer.Ordinal));
        // Gegenprobe: mit der alten Regel bleibt der Hinweis
        Assert.Contains(Alt(() => Funde(p)), f => f.Text == KeinBahnsteigTalheim);
    }
}
