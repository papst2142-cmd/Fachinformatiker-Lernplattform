using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.81.0 (Weisung P12-272, Nico 1b 2c 3a 4b): Bedienung von Hinsetzen und Hinlegen im Spiel-Kern. PC-Stuhl antippen:
/// hingehen, setzen, erst dann die PC-Knoepfe; vor einem Sitzmoebel „Hinsetzen“; Bett antippen: hinlegen, erst dann „Schlafen“;
/// „Aufstehen“ oder woanders hin tippen. Nur Darstellung: kein neues Ereignis, keine Spielzeit. Gegenprobe: wie bis A 0.80.0
/// (PC-Knoepfe im Stehen) und ohne 3D.
/// </summary>
public class SpielHaltungBedienungTests
{
    private static PyObjekt Inhalt => SpielCharakterTests.InhaltMitNeuen.Value;

    private static Spielablauf NeuesSpiel()
    {
        var zaehler = 0;
        var spiel = new Spielablauf(Inhalt, new SpielSpeicher(() =>
        {
            zaehler += 7;
            return new DateTime(2026, 11, 6).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }));
        spiel.Profil("Nico", new PyObjekt { { "haut", "mittel" }, { "frisur", "locken" }, { "haarfarbe", "braun" }, { "oberteil", "gruen" }, { "kreis", "cyan" } });
        spiel.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 1L }, { "richtig", false }, { "geld", 8000L } });
        return spiel;
    }

    private static string KaufenUndAufstellen(Spielablauf spiel, string moebel)
    {
        var stueck = "m" + Guid.NewGuid().ToString("N")[..10];
        spiel.Ausfuehren(SpielAktionen.MoebelKaufen(spiel.Stand, moebel, stueck));
        for (var y = 1.0; y < 12; y += 0.5)
        {
            for (var x = 1.0; x < 16; x += 0.5)
            {
                try
                {
                    spiel.Ausfuehren(SpielAktionen.AufstellenBei(spiel.Stand, stueck, x, y, 0L));
                    return stueck;
                }
                catch (SpielFehler)
                {
                    // naechste Stelle
                }
            }
        }
        Assert.Fail("kein Platz fuer " + moebel);
        return stueck;
    }

    private static StadtSpiel ZuHause(Spielablauf spiel)
    {
        var stadt = new Stadt(Inhalt, SpielStadtTests.Daten.Value);
        var s = new StadtSpiel(stadt, spiel, SpielGrafikTests.Bibliothek.Value, SpielInnenraumTests.Innenraeume.Value, SpielGeschaefteTests.Daten.Value, null, () => null, () => Jahreszeit.Sommer);
        Assert.True(s.Betreten());
        Assert.Equal("zuhause", s.FlaecheId);
        return s;
    }

    private static void Ankommen(StadtSpiel s)
    {
        for (var i = 0; i < 2000 && s.Fortschreiten(0.05); i++)
        {
        }
    }

    private static Sitzgelegenheit PlatzVon(StadtSpiel s, string moebel) =>
        s.Innen!.Sitzgelegenheiten.Single(p => p.Deko is { } d && Py.Gleich(SpielDaten.Get(d, "moebel"), moebel));

    private static bool Hat(StadtSpiel s, string aktion) => s.Knoepfe.Any(k => k.Aktion == aktion);

    [Fact]
    public void PcStuhlAntippen_ErstImSitzenDiePcKnoepfe_AufstehenWoandersHin()
    {
        var spiel = NeuesSpiel();
        KaufenUndAufstellen(spiel, Spielwelt.PcTyp);
        var s = ZuHause(spiel);
        var zeilen = spiel.Speicher.Ereignisse().Count;
        var platz = PlatzVon(s, Spielwelt.PcTyp);
        Assert.Equal(SitzArt.PcStuhl, platz.Art);
        // vor den Tisch (neben den Stuhl) gehen: stehend keine PC-Knoepfe, dafuer „Hinsetzen“ und der Hinweis
        var neben = (X: platz.Rechteck.X2 + 0.5, Z: platz.Mitte.Y);
        s.GeheZu(neben.X, neben.Z);
        Ankommen(s);
        Assert.Null(s.MeinPlatz);
        Assert.False(Hat(s, Spielwelt.AktionComputer) || Hat(s, "lernen"), string.Join(",", s.Knoepfe));
        Assert.True(Hat(s, StadtSpiel.AktionHinsetzen));
        Assert.Contains(Core.Texte.SpielProbeTexte.ErstHinsetzen, s.Zeile);
        // Stuhl antippen: hingehen und setzen, jetzt „Lernen“, „PC benutzen“ und „Aufstehen“
        s.GeheZu(platz.Mitte.X, platz.Mitte.Y);
        Ankommen(s);
        Assert.Same(platz.Deko, s.MeinPlatz?.Deko);
        Assert.True(Hat(s, Spielwelt.AktionComputer) && Hat(s, "lernen") && Hat(s, StadtSpiel.AktionAufstehen), string.Join(",", s.Knoepfe));
        Assert.Equal((platz.Ort, platz.Blick, false), s.Haltung);
        // „PC benutzen“ geht an die Oberflaeche, die Figur bleibt sitzen
        string? gemeldet = null;
        s.Aktion += a => gemeldet = a;
        Assert.True(s.Knopf(Spielwelt.AktionComputer));
        Assert.Equal(Spielwelt.AktionComputer, gemeldet);
        Assert.NotNull(s.MeinPlatz);
        // „Aufstehen“: wieder stehend, PC-Knoepfe weg
        Assert.True(s.Knopf(StadtSpiel.AktionAufstehen));
        Assert.Null(s.MeinPlatz);
        Assert.Null(s.Haltung);
        Assert.False(Hat(s, Spielwelt.AktionComputer));
        // „Hinsetzen“ (Tastatur, Screenreader) setzt auf denselben Stuhl
        Assert.True(s.Knopf(StadtSpiel.AktionHinsetzen));
        Assert.Equal(SitzArt.PcStuhl, s.MeinPlatz?.Art);
        Assert.True(Hat(s, Spielwelt.AktionComputer));
        // woanders hin getippt: aufstehen und gehen
        Assert.True(s.GeheZu(neben.X + 2, neben.Z + 2));
        Assert.Null(s.MeinPlatz);
        Ankommen(s);
        // nur Darstellung: keine neue Zeile im Spielstand
        Assert.Equal(zeilen, spiel.Speicher.Ereignisse().Count);
    }

    [Fact]
    public void BettAntippen_Hinlegen_ErstLiegendSchlafen()
    {
        var spiel = NeuesSpiel();
        KaufenUndAufstellen(spiel, "bett_einzel");
        var s = ZuHause(spiel);
        var zeilen = spiel.Speicher.Ereignisse().Count;
        var platz = PlatzVon(s, "bett_einzel");
        Assert.Equal(SitzArt.Liegen, platz.Art);
        s.GeheZu(platz.Mitte.X, platz.Mitte.Y);
        Ankommen(s);
        Assert.Equal(SitzArt.Liegen, s.MeinPlatz?.Art);
        Assert.True(Hat(s, "schlafen") && Hat(s, StadtSpiel.AktionAufstehen), string.Join(",", s.Knoepfe));
        Assert.True(s.Haltung?.Liegt);
        Assert.True(s.Knopf(StadtSpiel.AktionAufstehen));
        // stehend am Bett: kein „Schlafen“, dafuer „Hinlegen“
        Assert.False(Hat(s, "schlafen"));
        Assert.True(Hat(s, StadtSpiel.AktionHinlegen), string.Join(",", s.Knoepfe));
        Assert.True(s.Knopf(StadtSpiel.AktionHinlegen));
        Assert.True(Hat(s, "schlafen"));
        Assert.Equal(zeilen, spiel.Speicher.Ereignisse().Count);
    }

    [Fact]
    public void SofaHinsetzen_OhnePcKnoepfe_UndVerlassenStehtAuf()
    {
        var spiel = NeuesSpiel();
        KaufenUndAufstellen(spiel, "sofa");
        var s = ZuHause(spiel);
        var platz = PlatzVon(s, "sofa");
        Assert.Equal(SitzArt.Sitzen, platz.Art);
        var vor = platz.Ort + (new System.Numerics.Vector3(MathF.Sin(platz.Blick), 0, MathF.Cos(platz.Blick)) * 0.8f);
        s.GeheZu(vor.X, vor.Z);
        Ankommen(s);
        Assert.True(Hat(s, StadtSpiel.AktionHinsetzen), string.Join(",", s.Knoepfe) + " | " + s.Zeile + $" | ziel {vor.X:0.00},{vor.Z:0.00} figur {s.FigurX:0.00},{s.FigurZ:0.00} abstand {platz.Abstand(s.FigurX, s.FigurZ):0.00} ort {platz.Ort} blick {platz.Blick:0.00} rect {platz.Rechteck}");
        Assert.True(s.Knopf(StadtSpiel.AktionHinsetzen));
        Assert.Same(platz.Deko, s.MeinPlatz?.Deko);
        Assert.False(Hat(s, "lernen") || Hat(s, "schlafen"));
        Assert.Contains(Core.Texte.SpielProbeTexte.DuSitzt, s.Zeile);
        s.Verlassen();
        Assert.Null(s.MeinPlatz);
        Assert.Null(s.Haltung);
    }

    /// <summary>Gegenproben: wie bis A 0.80.0 (Schalter) und ohne 3D stehen die PC-Knoepfe am Tisch im Stehen, ohne „Hinsetzen“.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Gegenprobe_AltOderOhne3D_PcKnoepfeImStehen(bool alt)
    {
        StadtSpiel.HaltungAltFuerTests(alt);
        try
        {
            var spiel = NeuesSpiel();
            KaufenUndAufstellen(spiel, Spielwelt.PcTyp);
            var s = ZuHause(spiel);
            if (!alt)
            {
                s.OhneDreiDSetzen(true);
            }
            var platz = PlatzVon(s, Spielwelt.PcTyp);
            s.GeheZu(platz.Mitte.X, platz.Mitte.Y);
            Ankommen(s);
            Assert.Null(s.MeinPlatz);
            Assert.True(Hat(s, Spielwelt.AktionComputer), string.Join(",", s.Knoepfe));
            Assert.False(Hat(s, StadtSpiel.AktionHinsetzen));
        }
        finally
        {
            StadtSpiel.HaltungAltFuerTests(false);
        }
    }

    /// <summary>
    /// 4b: an der Haltestelle setzt sich die Figur nicht von allein; „Hinsetzen“ setzt sie auf die Bank (Stelle wie vorher, nur
    /// Darstellung), „Einsteigen“ laesst sie vorher aufstehen. Ohne „Hinsetzen“ bleibt alles wie bis A 0.80.0.
    /// </summary>
    [Fact]
    public void Haltestelle_NurMitHinsetzen_EinsteigenStehtAuf()
    {
        var vorlage = SpielMitfahrtTests.Stadt1();
        var s = new StadtSpiel(vorlage.Stadt, vorlage.Ablauf, SpielGrafikTests.Bibliothek.Value, SpielInnenraumTests.Innenraeume.Value);
        var linie = s.Linie!;
        var h = s.Szene.Haltestellen.First(h => linie.Halte.Any(l => l.Haltestelle == h.Nr));
        var v = Verkehr.FuerSpieltag(s.Szene, 1);
        SpielMitfahrtTests.Hingehen(s, v, h);
        Assert.Equal(h.Nr, s.WartetAn);
        // nicht von allein
        Assert.Null(s.AufDerBank);
        Assert.Null(s.Haltung);
        Assert.True(Hat(s, StadtSpiel.AktionHinsetzen), string.Join(",", s.Knoepfe));
        var stand = (s.FigurX, s.FigurZ);
        var zeilen = s.Ablauf.Stand.Verlauf.Count;
        Assert.True(s.Knopf(StadtSpiel.AktionHinsetzen));
        Assert.Equal(h.Nr, s.AufDerBank);
        // Sammelliste 351: so weit nach hinten, wie der Kopf (Aussehen und Kleidung der Figur) vor der Glasrueckwand Platz hat
        var aussehen = s.Ablauf.Stand.Profil is { } profil ? SpielDaten.Get(profil, "aussehen") as PyObjekt : null;
        var bank = Haltestellen.BankSitz(h, Figur.KopfHinten(s.Grafik, aussehen, s.Kleidung));
        Assert.Equal((bank.Ort, bank.Blick, false), s.Haltung);
        Assert.NotEqual(Haltestellen.BankSitz(h).Ort, bank.Ort);
        Assert.True(Hat(s, StadtSpiel.AktionAufstehen) && !Hat(s, StadtSpiel.AktionHinsetzen), string.Join(",", s.Knoepfe));
        Assert.Equal(stand, (s.FigurX, s.FigurZ));
        // der Bus haelt auch fuer die sitzende Figur; „Einsteigen“ steht vorher auf
        Assert.True(SpielMitfahrtTests.Bis(s, v, () => s.BusZumEinsteigen is not null, SpielMitfahrtTests.Runde(v)), "kein Bus");
        Assert.True(Hat(s, StadtSpiel.AktionEinsteigen) && Hat(s, StadtSpiel.AktionAufstehen), string.Join(",", s.Knoepfe));
        Assert.True(s.Knopf(StadtSpiel.AktionEinsteigen));
        Assert.Null(s.AufDerBank);
        Assert.False(Hat(s, StadtSpiel.AktionAufstehen) || Hat(s, StadtSpiel.AktionHinsetzen));
        Assert.Equal(zeilen, s.Ablauf.Stand.Verlauf.Count);
    }
}
