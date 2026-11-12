using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.21.0 / A 0.52.0 (Weisungen P12-87/P12-88, Kurzplan Meldung P12-56, Nico 1a-5a): das Aussehen eines Orts tauschen.
/// Talheim-Nord bekommt das Stellwerk (Nico loescht das Deko-Stellwerk), der Bahnsteig bleibt als eigenes Stueck stehen.
/// </summary>
public class OrtAussehenTests
{
    private static string Json(object? o) => PythonJson.Standard(o);

    private static Projekt Stadt1()
    {
        var p = Projekt.Neu("Aussehen", Daten.Spiel, Daten.NeueAblage());
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        return p;
    }

    private static int GebaeudeNummer(PyObjekt a, string modell) =>
        Aussen.Landschaft(a).FindIndex(t => t is PyObjekt g && Py.Gleich(g["typ"], "gebaeude") && Py.Gleich(g["modell"], modell));

    private static AussenStelle Gebaeude(PyObjekt a, string modell) => new(AussenArt.Gebaeude, GebaeudeNummer(a, modell));

    private static OrtAussehen.Vorlage Vorlage(Projekt p, string ort, string modell) =>
        OrtAussehen.Vorlagen(p.AussenStand, Aussen.Aus(p.Basis), ort, []).Single(v => v.Blueprint.Modell == modell);

    private static OrtAussehen.Ergebnis Tauschen(Projekt p, string ort, Blueprint b) =>
        p.AussenAendern("Aussehen tauschen", a => OrtAussehen.Tauschen(a, ort, b, Aussen.Aus(p.Basis)));

    private static StadtSzene Szene(Projekt p, string? fest = null) =>
        Ansicht3D.Bauen(p.Stand(), Daten.Spiel, new Ansicht3D.Einstellung(false, Jahreszeit.Winter, fest)).Szene;

    private static List<StadtbauFund> Rot(Projekt p) => [.. p.PruefenAussen().Where(f => f.Rot)];

    /// <summary>Export wie „Stadt-Datei speichern“, dann die Ladepruefung des Spiels: beides ohne Fund.</summary>
    private static StadtFassung Export(Projekt p)
    {
        var st = StadtFassung.Lesen(p.Exportieren(DateTimeOffset.Now));
        Assert.Equal([], StadtbauPruefung.Inhalt(st, Daten.Spiel.Inhalt));
        Assert.Equal([], SpielPruefung.Welt(st.MitKarte(Daten.Spiel.Inhalt)));
        return st;
    }

    private static (double X, double Y) Mitte(PyObjekt a, string ort)
    {
        var (x, y, w, h) = AussenBearbeitung.HausFlaeche(a, new AussenStelle(AussenArt.Haus, -1, 0, ort));
        return (x + (w / 2), y + (h / 2));
    }

    /// <summary>Test a: das Deko-Stellwerk loeschen; Pruefung gruen, Tuer, Weg und Fest-Deko gehen mit.</summary>
    [Fact]
    public void Stellwerk_Loeschen_PruefungGruen_TuerUndFestDekoGehenMit()
    {
        var p = Stadt1();
        var vorher = Szene(p, Festzeit.Weihnachten);
        Assert.Contains(vorher.Tueren, t => t.Ort == "stellwerk");
        Assert.Contains(vorher.Zierhaeuser, z => z.Name == "Stellwerk");
        Assert.Contains(vorher.FestGesetzt, g => g.Ort == "stellwerk");
        Assert.Equal([], Rot(p).Select(f => f.Text));

        Assert.True(p.AussenAendern("Stellwerk weg", a => AussenBearbeitung.Loeschen(a, Gebaeude(a, "stellwerk"))));
        Assert.Equal(-1, GebaeudeNummer(p.AussenStand, "stellwerk"));
        Assert.Null(Blueprint.Zierweg(p.AussenStand, "stellwerk"));
        var nachher = Szene(p, Festzeit.Weihnachten);
        Assert.DoesNotContain(nachher.Tueren, t => t.Ort == "stellwerk");
        Assert.DoesNotContain(nachher.Zierhaeuser, z => z.Name == "Stellwerk");
        Assert.DoesNotContain(nachher.FestGesetzt, g => g.Ort == "stellwerk");
        Assert.Equal([], Rot(p).Select(f => f.Text));
        Export(p);
    }

    /// <summary>Test b: solange das Stellwerk steht, Grund (nichts geaendert); nach dem Loeschen ohne Umweg waehlbar.</summary>
    [Fact]
    public void TalheimNord_WirdStellwerk_ErstNachDemLoeschen_MitteTuerseiteWegBleiben()
    {
        // Fund 262: Regel „Tuer buendig“ hier aus (Tueren an CC0-Modellen ueber Fenstern/Nischen), sie pruefen TuerBuendigTests
        using var ohne262 = TuerBuendig.AusFuerTests();
        var p = Stadt1();
        var basis = Aussen.Aus(p.Basis);
        var altMitte = Mitte(p.AussenStand, "talheim_nord");
        var altSeite = AussenBearbeitung.HausTuerSeite(p.AussenStand, new AussenStelle(AussenArt.Haus, -1, 0, "talheim_nord"));
        var schritte = p.SchritteZurueck;

        // Gegenprobe: das Stellwerk steht noch
        var r1 = Tauschen(p, "talheim_nord", Vorlage(p, "talheim_nord", "stellwerk").Blueprint);
        Assert.Equal(Texte.AussehenSchonDa("Stellwerk"), r1.Grund);
        Assert.Equal(schritte, p.SchritteZurueck);
        Assert.Equal("bahnhof_neu", Py.Str(AussenBearbeitung.Ort(p.AussenStand, "talheim_nord")!["modell"]));

        p.AussenAendern("Stellwerk weg", a => AussenBearbeitung.Loeschen(a, Gebaeude(a, "stellwerk")));
        var vorlage = Vorlage(p, "talheim_nord", "stellwerk");
        Assert.True(vorlage.AusDerStadt);
        Assert.False(vorlage.Zurueck);
        var r = Tauschen(p, "talheim_nord", vorlage.Blueprint);
        Assert.Null(r.Grund);
        Assert.Contains(Texte.AussehenBahnsteigBleibt, r.Hinweise);
        Assert.Equal(schritte + 2, p.SchritteZurueck);

        var a = p.AussenStand;
        var o = AussenBearbeitung.Ort(a, "talheim_nord")!;
        Assert.Equal("stellwerk", Py.Str(o["modell"]));
        Assert.Equal("Talheim-Nord", o["name"]);
        Assert.Equal("kunde:talheim_nord", o["ansicht"]);
        Assert.NotNull(Get(Objekt(Aussen.Karte(a)["modelle"]), "stellwerk"));
        var mitte = Mitte(a, "talheim_nord");
        Assert.InRange(Math.Abs(mitte.X - altMitte.X), 0, 0.5);
        Assert.InRange(Math.Abs(mitte.Y - altMitte.Y), 0, 0.6);
        Assert.Equal(altSeite, AussenBearbeitung.HausTuerSeite(a, new AussenStelle(AussenArt.Haus, -1, 0, "talheim_nord")));
        var weg = Liste(AussenBearbeitung.Zugang(a, "talheim_nord")!["weg"]);
        Assert.Equal("[29.8, 14.4]", Json(weg[^1]));
        Assert.Null(Get(Objekt(Objekt(Aussen.Stadtdaten(a)["darstellung"])["modell_tuer"]), "talheim_nord"));

        // Bahnsteig als eigenes Stueck an der alten Stelle
        var nr = GebaeudeNummer(a, Bahnsteig.ModellName("talheim_nord"));
        Assert.True(nr >= 0);
        var stueck = (PyObjekt)Aussen.Landschaft(a)[nr]!;
        Assert.Equal((28.0, 5.2), (Py.F(stueck["x"]), Py.F(stueck["y"])));
        Assert.True(Bahnsteig.Nur(Get(Objekt(Aussen.Karte(a)["modelle"]), Bahnsteig.ModellName("talheim_nord"))));

        // keine roten Funde; seit Weisung P12-220 Punkt 4 ist die Baustelle des alten Bahnhofs am Stellwerk kein fehlender Teil
        // (Gegenprobe: vorher gelb „wechselnde Teile“)
        var funde = p.PruefenAussen();
        Assert.Equal([], funde.Where(f => f.Rot).Select(f => f.Text));
        Assert.DoesNotContain(funde, f => f.Text.Contains("wechselnden Teile", StringComparison.Ordinal));
        Assert.DoesNotContain(funde, f => f.Text.Contains("keinen Bahnsteig", StringComparison.Ordinal));
        StadtbauPruefung.BahnsteigVerknuepfungAltFuerTests = true;
        try
        {
            Assert.Contains(p.PruefenAussen(), f => !f.Rot && f.Text.Contains("wechselnden Teile", StringComparison.Ordinal));
        }
        finally
        {
            StadtbauPruefung.BahnsteigVerknuepfungAltFuerTests = false;
        }

        // Spiel: Tuer am Stellwerk von Talheim-Nord, Fest-Deko dort, Innenraum und alles andere gleich
        var szene = Szene(p, Festzeit.Weihnachten);
        Assert.Contains(szene.Tueren, t => t.Ort == "talheim_nord");
        Assert.Contains(szene.FestGesetzt, g => g.Ort == "talheim_nord");
        var st = Export(p);
        Assert.Equal(Json(p.Basis.Innenraeume), Json(st.Innenraeume));
        var stadt = st.StadtFuer(st.MitKarte(Daten.Spiel.Inhalt));
        Assert.Equal("stellwerk", Py.Str(Liste(st.Karte["orte"]).OfType<PyObjekt>().Single(t => Py.Gleich(t["id"], "talheim_nord"))["modell"]));
        Assert.NotNull(stadt);
    }

    /// <summary>P12-88 Punkt 3: Bahnsteig-Stueck loeschen = gelb, Export geht; Gegenprobe Stueck auf dem Gleis = rot.</summary>
    [Fact]
    public void Bahnsteig_Geloescht_Gelb_ExportGeht_Gegenprobe_AufDemGleis_Rot()
    {
        // Fund 262: Regel „Tuer buendig“ hier aus (Tueren an CC0-Modellen ueber Fenstern/Nischen), sie pruefen TuerBuendigTests
        using var ohne262 = TuerBuendig.AusFuerTests();
        var p = Stadt1();
        p.AussenAendern("Stellwerk weg", a => AussenBearbeitung.Loeschen(a, Gebaeude(a, "stellwerk")));
        Tauschen(p, "talheim_nord", Vorlage(p, "talheim_nord", "stellwerk").Blueprint);
        var name = Bahnsteig.ModellName("talheim_nord");

        // Gegenprobe: zum Gleis geschoben (0,40 → unter 0,21 von der Gleismitte)
        p.AussenAendern("Bahnsteig zum Gleis", a => AussenBearbeitung.Verschieben(a, Gebaeude(a, name), 0, 0.5, raster: false));
        var stueck = (PyObjekt)Aussen.Landschaft(p.AussenStand)[GebaeudeNummer(p.AussenStand, name)]!;
        Assert.Equal(5.7, Py.F(stueck["y"]), 3);
        Assert.Contains(Rot(p), f => f.Text.Contains("liegt auf dem Gleis", StringComparison.Ordinal));
        Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
        p.Rueckgaengig();
        Assert.DoesNotContain(Rot(p), f => f.Text.Contains("liegt auf dem Gleis", StringComparison.Ordinal));

        // geloescht: gelb, kein Rot, Export geht
        p.AussenAendern("Bahnsteig weg", a => AussenBearbeitung.Loeschen(a, Gebaeude(a, name)));
        Assert.Equal(-1, GebaeudeNummer(p.AussenStand, name));
        var funde = p.PruefenAussen();
        Assert.Equal([], funde.Where(f => f.Rot).Select(f => f.Text));
        // Weisung P12-220 Punkt 4: Talheim-Nord ist das Stellwerk ohne Bahnsteig, kein Hinweis (Gegenprobe: vorher gelb)
        Assert.DoesNotContain(funde, f => f.Text.Contains("keinen Bahnsteig", StringComparison.Ordinal));
        StadtbauPruefung.BahnsteigVerknuepfungAltFuerTests = true;
        try
        {
            Assert.Contains(p.PruefenAussen(), f => !f.Rot && f.Text == "„Talheim-Nord“ hat keinen Bahnsteig mehr.");
        }
        finally
        {
            StadtbauPruefung.BahnsteigVerknuepfungAltFuerTests = false;
        }
        Export(p);
    }

    /// <summary>„Altes Aussehen zurueck“: die Stadt ist wieder genau die Ausgangsstadt (ohne das geloeschte Stellwerk), das Bahnsteig-Stueck geht weg.</summary>
    [Fact]
    public void AltesAussehenZurueck_GleichDerAusgangsstadt_BahnsteigStueckWeg()
    {
        var p = Stadt1();
        p.AussenAendern("Stellwerk weg", a => AussenBearbeitung.Loeschen(a, Gebaeude(a, "stellwerk")));
        var ohneStellwerk = Json(p.AussenStand);
        Tauschen(p, "talheim_nord", Vorlage(p, "talheim_nord", "stellwerk").Blueprint);
        Assert.NotEqual(ohneStellwerk, Json(p.AussenStand));

        var zurueck = OrtAussehen.Vorlagen(p.AussenStand, Aussen.Aus(p.Basis), "talheim_nord", []).Single(v => v.Zurueck);
        Assert.Equal(Texte.AussehenAltZurueck, zurueck.Name);
        var r = Tauschen(p, "talheim_nord", zurueck.Blueprint);
        Assert.Null(r.Grund);
        var a = p.AussenStand;
        Assert.Equal(-1, GebaeudeNummer(a, Bahnsteig.ModellName("talheim_nord")));
        Assert.Null(Get(Objekt(Aussen.Karte(a)["modelle"]), Bahnsteig.ModellName("talheim_nord")));
        var o = AussenBearbeitung.Ort(a, "talheim_nord")!;
        var b = AussenBearbeitung.Ort(Aussen.Aus(p.Basis), "talheim_nord")!;
        Assert.Equal(Json(b), Json(o));
        Assert.Equal(Json(AussenBearbeitung.Zugang(Aussen.Aus(p.Basis), "talheim_nord")), Json(AussenBearbeitung.Zugang(a, "talheim_nord")));
        Assert.Equal("-0.463", Json(Get(Objekt(Objekt(Aussen.Stadtdaten(a)["darstellung"])["modell_tuer"]), "talheim_nord")));
        Assert.Equal([], Rot(p).Select(f => f.Text));
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Text.Contains("Talheim-Nord", StringComparison.Ordinal) && !f.Text.Contains("Spielminuten", StringComparison.Ordinal));
        Export(p);
        // Liste ohne „zurueck“, wenn das Haus wieder das alte ist
        Assert.DoesNotContain(OrtAussehen.Vorlagen(a, Aussen.Aus(p.Basis), "talheim_nord", []), v => v.Zurueck);
    }

    /// <summary>Andere Orte mit dem Haus eines anderen Orts (Blueprint): Export laedt im Spiel, Innenraeume gleich.</summary>
    [Theory]
    [InlineData("lindenau", "birkenhain")]
    [InlineData("lokschuppen", "betriebswerkstatt")]
    [InlineData("bank", "amt")]
    [InlineData("gewerbehof", "moebelhaus")]
    public void AndereOrte_Tauschen_ExportLaedt_InnenraeumeGleich(string ort, string vorlageVon)
    {
        // Fund 262: Regel „Tuer buendig“ hier aus (Tueren an CC0-Modellen ueber Fenstern/Nischen), sie pruefen TuerBuendigTests
        using var ohne262 = TuerBuendig.AusFuerTests();
        var p = Stadt1();
        var bp = Blueprint.Aus(p, new AussenStelle(AussenArt.Haus, -1, 0, vorlageVon), "Vorlage")!;
        var r = Tauschen(p, ort, bp);
        Assert.Null(r.Grund);
        Assert.Equal(bp.Modell, Py.Str(AussenBearbeitung.Ort(p.AussenStand, ort)!["modell"]));
        Assert.Equal([], Rot(p).Select(f => f.Text));
        var st = Export(p);
        Assert.Equal(Json(p.Basis.Innenraeume), Json(st.Innenraeume));
        Assert.Contains(Szene(p).Tueren, t => t.Ort == ort);
    }

    /// <summary>Gegenprobe: ein zu grosses Haus (Verwaltung an Birkenhain) ragt aufs Gleis; die Pruefung zeigt es rot, der Export ist gesperrt.</summary>
    [Fact]
    public void ZuGrossesHaus_AufDemGleis_Rot()
    {
        var p = Stadt1();
        var bp = Blueprint.Aus(p, new AussenStelle(AussenArt.Haus, -1, 0, "lindenau"), "Vorlage")!;
        Assert.Null(Tauschen(p, "birkenhain", bp).Grund);
        Assert.Contains(Rot(p), f => f.Text == "„Birkenhain“ steht auf dem Gleis.");
        Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
    }

    /// <summary>Zuhause nicht tauschbar (Frage 3 = a): Grund, kein Schritt.</summary>
    [Fact]
    public void Zuhause_NichtTauschbar_NichtsGeaendert()
    {
        var p = Stadt1();
        Assert.Equal(Texte.AussehenZuhauseNicht, OrtAussehen.TauschenNicht(p.AussenStand, "zuhause"));
        Assert.Null(OrtAussehen.TauschenNicht(p.AussenStand, "talheim_nord"));
        var vorher = Json(p.AussenStand);
        var bp = Blueprint.Aus(p, new AussenStelle(AussenArt.Haus, -1, 0, "lindenau"), "Vorlage")!;
        Assert.Equal(Texte.AussehenZuhauseNicht, Tauschen(p, "zuhause", bp).Grund);
        Assert.Equal(vorher, Json(p.AussenStand));
        Assert.Equal(Texte.AussehenKeinOrt, OrtAussehen.TauschenNicht(p.AussenStand, "gibt_es_nicht"));
    }

    /// <summary>Die Liste zeigt nur Vorlagen mit Tuer und nie das jetzige Modell.</summary>
    [Fact]
    public void Vorlagen_NurMitTuer_OhneDasJetzigeModell()
    {
        var p = Stadt1();
        var mitTuer = Blueprint.Aus(p, new AussenStelle(AussenArt.Haus, -1, 0, "lindenau"), "Mit Tür")!;
        var ohneTuer = new Blueprint { Name = "Ohne Tür", Herkunft = mitTuer.Herkunft, Modell = mitTuer.Modell, Teile = mitTuer.Teile, Tuer = null };
        var gleich = Blueprint.Aus(p, new AussenStelle(AussenArt.Haus, -1, 0, "birkenhain"), "Gleich")!;
        var eigene = new[]
        {
            new Blueprint.Eintrag("a", mitTuer.Name, mitTuer, null, false),
            new Blueprint.Eintrag("b", ohneTuer.Name, ohneTuer, null, false),
            new Blueprint.Eintrag("c", gleich.Name, gleich, null, false),
            new Blueprint.Eintrag("d", "Kaputt", null, "kaputt", false),
        };
        var liste = OrtAussehen.Vorlagen(p.AussenStand, Aussen.Aus(p.Basis), "birkenhain", eigene);
        Assert.Equal(["Mit Tür", "Stellwerk"], liste.Select(v => v.Name));
        Assert.Equal([false, true], liste.Select(v => v.AusDerStadt));
        Assert.Equal(Texte.AussehenOhneTuer, Tauschen(p, "birkenhain", ohneTuer).Grund);
    }

    /// <summary>Regel rot: ein Ort, dessen Haus keinen Block hat (nur Flaechen); Gegenprobe mit Block.</summary>
    [Fact]
    public void Regel_HausOhneBlock_Rot_Gegenprobe_MitBlock()
    {
        var p = Stadt1();
        const string text = "Das Haus von „Birkenhain“ hat kein Gebäude (keinen Block), nur Flächen.";
        Assert.DoesNotContain(Rot(p), f => f.Text == text);
        p.AussenAendern("Ohne Block", a =>
        {
            var teile = (List<object?>)Objekt(Aussen.Karte(a)["modelle"])["haltepunkt"]!;
            teile.RemoveAll(t => t is PyObjekt b && Py.Gleich(b["art"], "block"));
        });
        Assert.Contains(Rot(p), f => f.Text == text);
        Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
    }

    /// <summary>Regel rot: Schild ausserhalb der Karte; Gegenprobe innen.</summary>
    [Fact]
    public void Regel_SchildAusserhalb_Rot_Gegenprobe_Innen()
    {
        var p = Stadt1();
        const string text = "Das Schild von „Birkenhain“ liegt außerhalb der Karte.";
        p.AussenAendern("Schild innen", a => Py.Setzen(AussenBearbeitung.Ort(a, "birkenhain")!, "schild", new List<object?> { 5.0, 5.0 }));
        Assert.DoesNotContain(Rot(p), f => f.Text == text);
        p.AussenAendern("Schild aussen", a => Py.Setzen(AussenBearbeitung.Ort(a, "birkenhain")!, "schild", new List<object?> { -1.0, 5.0 }));
        Assert.Contains(Rot(p), f => f.Text == text);
    }

    /// <summary>Gegenprobe: liegt das neue Modell eines Orts der Karte nicht in karte.modelle, sperrt die Pruefung den Export.</summary>
    [Fact]
    public void Gegenprobe_ModellFehltInDerKarte_ExportGesperrt()
    {
        // Fund 262: Regel „Tuer buendig“ hier aus (Tueren an CC0-Modellen ueber Fenstern/Nischen), sie pruefen TuerBuendigTests
        using var ohne262 = TuerBuendig.AusFuerTests();
        var p = Stadt1();
        p.AussenAendern("Stellwerk weg", a => AussenBearbeitung.Loeschen(a, Gebaeude(a, "stellwerk")));
        Tauschen(p, "talheim_nord", Vorlage(p, "talheim_nord", "stellwerk").Blueprint);
        Export(p);
        p.AussenAendern("Modell verlegt", a =>
        {
            var m = Objekt(Aussen.Karte(a)["modelle"]);
            var teile = m["stellwerk"];
            m.RemoveAll(e => e.Key == "stellwerk");
            var sd = Aussen.Stadtdaten(a);
            if (Get(sd, "neue_modelle") is not PyObjekt neu)
            {
                neu = new PyObjekt();
                Py.Setzen(sd, "neue_modelle", neu);
            }
            Py.Setzen(neu, "stellwerk", teile);
        });
        Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
    }

    /// <summary>Fassung 0: die neuen Regeln finden nichts (alte Modelle, keine Bahnsteig-Stuecke).</summary>
    [Fact]
    public void Fassung0_OhneFundDerNeuenRegeln()
    {
        var p = Stadt1();
        var funde = p.PruefenAussen();
        Assert.DoesNotContain(funde, f => f.Text.Contains("Bahnsteig", StringComparison.Ordinal) || f.Text.Contains("keinen Block", StringComparison.Ordinal)
            || f.Text.Contains("Schild von", StringComparison.Ordinal) || f.Text.Contains("wechselnden Teile", StringComparison.Ordinal));
        Assert.Equal(0.21f, StadtbauPruefung.BahnsteigAbstand, 3);
    }

    /// <summary>Lage der Bahnsteige der Stadt 1: alle naeher als der Lichtraum (0,493), alle weiter als 0,21 (darum diese Grenze, E1).</summary>
    [Fact]
    public void BahnsteigeDerStadt1_ZwischenGleisbettUndLichtraum()
    {
        var p = Stadt1();
        var f0 = p.Basis;
        var szene = StadtSzene.Bauen(f0.StadtFuer(f0.MitKarte(Daten.Spiel.Inhalt)), null);
        var a0 = Aussen.Aus(f0);
        foreach (var id in new[] { "talheim", "talheim_nord", "birkenhain" })
        {
            var o = AussenBearbeitung.Ort(a0, id)!;
            var teile = Get(AussenBearbeitung.Modelle(a0), Py.Str(o["modell"]));
            var rechtecke = Bahnsteig.Rechtecke(teile, Py.F(o["x"]), Py.F(o["y"]), 0);
            Assert.NotEmpty(rechtecke);
            foreach (var (min, max) in rechtecke)
            {
                var d = 99f;
                for (var i = 0; i <= 40; i++)
                {
                    for (var j = 0; j <= 10; j++)
                    {
                        var q = new Vector2(min.X + ((max.X - min.X) * i / 40), min.Y + ((max.Y - min.Y) * j / 10));
                        foreach (var g in szene.Gleise)
                        {
                            d = Math.Min(d, Kurve.Abstand(q, g));
                        }
                    }
                }
                Assert.InRange(d, StadtbauPruefung.BahnsteigAbstand + 0.02f, Bahnuebergang.LichtraumHalb);
            }
        }
    }
}
