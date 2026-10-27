using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Spiel.Ton;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W8 (Weisungen P8-17, P8-17b, P8-17c, P8-17d): Toene im Spiel. Regler und Wahl in einstellungen.json,
/// Mischer (Gesamt 0 = still), eigene Lieder (leerer Ordner = keine Musik, kaputte Datei = uebersprungen mit
/// einer Zeile in fehler.log), stille Ausgabe ohne Geraet, Tonplan (Naehe, Boden, Anlaesse aus Ereignissen),
/// Toene nur angehaengt und alle lesbar.
/// </summary>
public sealed class SpielTonTests
{
    private static string ToeneOrdner => Path.Combine(Pfade.Wurzel, "src", "Lernplattform.App", "Assets", "Toene");

    private static string NeuerOrdner()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "lp_ton_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ordner);
        return ordner;
    }

    // -- Einstellungen ----------------------------------------------------------------

    [Fact]
    public void VorgabenWieNico()
    {
        var werte = TonEinstellungen.Lesen(new JsonObject());
        Assert.Equal(new TonWerte(80, 50, 80, TonEinstellungen.Innen), werte);
        Assert.Equal(0.8f, werte.GesamtFaktor);
        Assert.Equal(0.5f, werte.MusikFaktor);
        Assert.False(VollbildWahl.Lesen(new JsonObject()));
    }

    [Fact]
    public void FalscheWerteGeltenAlsVorgabeOderGeklemmt()
    {
        var werte = TonEinstellungen.Lesen(new JsonObject
        {
            [TonEinstellungen.Gesamt] = 140,
            [TonEinstellungen.Musik] = "laut",
            [TonEinstellungen.Umgebung] = -3.4,
            [TonEinstellungen.MusikOrte] = 7,
        });
        Assert.Equal(new TonWerte(100, 50, 0, TonEinstellungen.Innen), werte);
        Assert.Equal(33, TonEinstellungen.Zahl(new JsonObject { ["x"] = 32.6 }, "x", 1));
        Assert.Equal(1, TonEinstellungen.Zahl(new JsonObject { ["x"] = true }, "x", 1));
        Assert.Equal(1, TonEinstellungen.Zahl(new JsonObject { ["x"] = double.NaN.ToString(CultureInfo.InvariantCulture) }, "x", 1));
        Assert.Equal(100, TonEinstellungen.Zahl(new JsonObject { ["x"] = long.MaxValue }, "x", 1));
        Assert.False(VollbildWahl.Lesen(new JsonObject { [VollbildWahl.Schluessel] = "ja" }));
    }

    /// <summary>P8-17b: alle drei Werte und ein kaputter Wert (zaehlt als innen), wo eigene Lieder laufen.</summary>
    [Theory]
    [InlineData("innen", "innen", true, false)]
    [InlineData("stadt", "stadt", false, true)]
    [InlineData("ueberall", "ueberall", true, true)]
    [InlineData("Stadt", "innen", true, false)]
    [InlineData("draussen", "innen", true, false)]
    [InlineData("", "innen", true, false)]
    public void LiedOrteAlleWerte(string gespeichert, string gilt, bool innen, bool stadt)
    {
        var werte = TonEinstellungen.Lesen(new JsonObject { [TonEinstellungen.MusikOrte] = gespeichert });
        Assert.Equal(gilt, werte.MusikOrte);
        Assert.Equal(innen, TonEinstellungen.MusikHier(werte.MusikOrte, innen: true));
        Assert.Equal(stadt, TonEinstellungen.MusikHier(werte.MusikOrte, innen: false));
        // Schreiben nimmt nur erlaubte Werte
        var neu = new JsonObject();
        TonEinstellungen.OrteWert(neu, gespeichert);
        Assert.Equal(gilt, (string?)neu[TonEinstellungen.MusikOrte]);
    }

    [Fact]
    public void SchreibenNurUeberAendern()
    {
        var ordner = NeuerOrdner();
        try
        {
            var einstellungen = new Einstellungen(ordner);
            Assert.True(TonEinstellungen.Setzen(einstellungen, TonEinstellungen.Musik, 35));
            Assert.True(TonEinstellungen.OrteSetzen(einstellungen, TonEinstellungen.Ueberall));
            Assert.True(VollbildWahl.Setzen(einstellungen, true));
            var gelesen = JsonNode.Parse(File.ReadAllText(Path.Combine(ordner, "einstellungen.json")))!.AsObject();
            Assert.Equal(new TonWerte(80, 35, 80, TonEinstellungen.Ueberall), TonEinstellungen.Lesen(gelesen));
            Assert.True(VollbildWahl.Lesen(gelesen));
        }
        finally
        {
            Directory.Delete(ordner, true);
        }
    }

    // -- Mischer ----------------------------------------------------------------------

    private static TonDaten Sinus(double sekunden = 0.5) => new(Tondekoder.Sinus(440, sekunden, Mischer.Rate, 0.5f), Mischer.Rate);

    private static float Spitze(Mischer mischer, int bilder = 4410)
    {
        var puffer = new float[bilder * 2];
        mischer.Fuellen(puffer, 2);
        return puffer.Max(Math.Abs);
    }

    [Fact]
    public void GesamtNullIstStill()
    {
        var mischer = new Mischer();
        mischer.Regler(0, 1, 1);
        mischer.Starten(Sinus(), Tongruppe.Umgebung, 1);
        Assert.Equal(0f, Spitze(mischer));
        Assert.Equal(0f, mischer.Faktor(Tongruppe.Musik));
        mischer.Regler(1, 1, 1);
        Assert.True(Spitze(mischer) > 0.3f);
    }

    [Fact]
    public void GruppenReglerWirkenGetrennt()
    {
        var mischer = new Mischer();
        mischer.Regler(1, 0, 1);
        mischer.Starten(Sinus(), Tongruppe.Musik, 1);
        Assert.Equal(0f, Spitze(mischer));
        var umgebung = new Mischer();
        umgebung.Regler(0.5f, 0, 1);
        umgebung.Starten(Sinus(), Tongruppe.Umgebung, 1);
        Assert.InRange(Spitze(umgebung), 0.2f, 0.26f);
        Assert.Equal(0.5f * 0.8f, new Mischer().Faktor(Tongruppe.Umgebung) - 0.24f, 3);
    }

    [Fact]
    public void KurzerTonEndetSchleifeNicht()
    {
        var mischer = new Mischer();
        mischer.Regler(1, 1, 1);
        var kurz = mischer.Starten(Sinus(0.05), Tongruppe.Umgebung, 1);
        var schleife = mischer.Starten(Sinus(0.05), Tongruppe.Umgebung, 1, schleife: true);
        Spitze(mischer, 44100);
        Assert.False(mischer.Laeuft(kurz));
        Assert.True(mischer.Beendet(kurz));
        Assert.False(mischer.Beendet(kurz));
        Assert.True(mischer.Laeuft(schleife));
        mischer.Beenden(schleife);
        Spitze(mischer, 44100);
        Assert.False(mischer.Laeuft(schleife));
        Assert.Equal(0, mischer.Stimmen);
    }

    [Fact]
    public void TonhoeheVerkuerztDenTon()
    {
        var mischer = new Mischer();
        mischer.Regler(1, 1, 1);
        // 0,1 s mit doppelter Tonhoehe ist nach 0,06 s (2646 Bilder) zu Ende, mit einfacher nicht
        var hoch = mischer.Starten(Sinus(0.1), Tongruppe.Umgebung, 1, tonhoehe: 2f);
        var normal = mischer.Starten(Sinus(0.1), Tongruppe.Umgebung, 1);
        Spitze(mischer, 2646);
        Assert.False(mischer.Laeuft(hoch));
        Assert.True(mischer.Laeuft(normal));
    }

    [Fact]
    public void AusgabeBleibtImBereich()
    {
        var mischer = new Mischer();
        mischer.Regler(1, 1, 1);
        for (var i = 0; i < 8; i++)
        {
            mischer.Starten(Sinus(), Tongruppe.Umgebung, 1);
        }
        Assert.Equal(1f, Spitze(mischer));
    }

    // -- Dekoder und eigene Lieder ----------------------------------------------------

    [Fact]
    public void WavHinUndZurueck()
    {
        var proben = Tondekoder.Sinus(220, 0.25, 22050, 0.4f);
        using var speicher = new MemoryStream();
        Tondekoder.WavSchreiben(speicher, proben, 22050);
        speicher.Position = 0;
        var gelesen = Tondekoder.Lesen(speicher, ".wav");
        Assert.Equal(22050, gelesen.Rate);
        Assert.Equal(proben.Length, gelesen.Proben.Length);
        Assert.All(proben.Zip(gelesen.Proben), p => Assert.InRange(p.Second - p.First, -0.0001f, 0.0001f));
    }

    [Fact]
    public void LeererOrdnerKeineMusik()
    {
        var ordner = NeuerOrdner();
        try
        {
            var lieder = new EigeneLieder(ordner);
            Assert.Empty(lieder.Liste());
            Assert.Null(lieder.Naechstes());
            Directory.CreateDirectory(lieder.Ordner);
            File.WriteAllText(Path.Combine(lieder.Ordner, "liesmich.txt"), "kein Lied");
            Assert.Empty(lieder.Liste());
            Assert.Null(lieder.Naechstes());
            Assert.False(File.Exists(Path.Combine(ordner, Fehlerprotokoll.DateiName)));
        }
        finally
        {
            Directory.Delete(ordner, true);
        }
    }

    [Fact]
    public void KaputteDateiWirdUebersprungen()
    {
        var ordner = NeuerOrdner();
        try
        {
            var lieder = new EigeneLieder(ordner);
            Directory.CreateDirectory(lieder.Ordner);
            File.WriteAllBytes(Path.Combine(lieder.Ordner, "a_kaputt.ogg"), Encoding.ASCII.GetBytes("kein Ton, nur Text"));
            using (var wav = File.Create(Path.Combine(lieder.Ordner, "b_sinus.wav")))
            {
                Tondekoder.WavSchreiben(wav, Tondekoder.Sinus(440, 0.2, 44100), 44100);
            }
            var erstes = lieder.Naechstes();
            Assert.NotNull(erstes);
            Assert.Equal("b_sinus.wav", erstes.Value.Name);
            var puffer = new float[1024];
            Assert.True(erstes.Value.Strom.Lesen(puffer, puffer.Length) > 0);
            erstes.Value.Strom.Dispose();
            var log = File.ReadAllText(Path.Combine(ordner, Fehlerprotokoll.DateiName));
            Assert.Single(log.Split('\n'), z => z.Contains("Ton: eigenes Lied übersprungen (a_kaputt.ogg)", StringComparison.Ordinal));
            // nur die kaputte Datei: keine Musik, kein Absturz, eine weitere Zeile
            File.Delete(Path.Combine(lieder.Ordner, "b_sinus.wav"));
            Assert.Null(lieder.Naechstes());
            log = File.ReadAllText(Path.Combine(ordner, Fehlerprotokoll.DateiName));
            Assert.Equal(2, log.Split('\n').Count(z => z.Contains("Ton: eigenes Lied übersprungen (a_kaputt.ogg)", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(ordner, true);
        }
    }

    [Fact]
    public void HinzufuegenUndEntfernen()
    {
        var ordner = NeuerOrdner();
        try
        {
            var lieder = new EigeneLieder(ordner);
            using var daten = new MemoryStream([1, 2, 3]);
            Assert.Equal("Lied.mp3", lieder.Hinzufuegen(daten, Path.Combine("irgendwo", "Lied.mp3")));
            daten.Position = 0;
            Assert.Equal("Lied (2).mp3", lieder.Hinzufuegen(daten, "Lied.mp3"));
            Assert.Throws<NotSupportedException>(() => lieder.Hinzufuegen(daten, "bild.png"));
            Assert.Equal(["Lied (2).mp3", "Lied.mp3"], lieder.Liste());
            Assert.Empty(Directory.GetFiles(lieder.Ordner, "*.teil"));
            Assert.True(lieder.Entfernen("Lied.mp3"));
            Assert.False(lieder.Entfernen("Lied.mp3"));
            // nur Namen im eigenen Ordner, nie ein Pfad nach draussen
            File.WriteAllText(Path.Combine(ordner, "draussen.mp3"), "x");
            Assert.False(lieder.Entfernen(Path.Combine("..", "draussen.mp3")));
            Assert.True(File.Exists(Path.Combine(ordner, "draussen.mp3")));
            Assert.Equal(["Lied (2).mp3"], lieder.Liste());
        }
        finally
        {
            Directory.Delete(ordner, true);
        }
    }

    [Fact]
    public void LiederDerReiheNach()
    {
        var ordner = NeuerOrdner();
        try
        {
            var lieder = new EigeneLieder(ordner);
            Directory.CreateDirectory(lieder.Ordner);
            foreach (var name in new[] { "c.wav", "A.wav", "b.wav" })
            {
                using var wav = File.Create(Path.Combine(lieder.Ordner, name));
                Tondekoder.WavSchreiben(wav, Tondekoder.Sinus(440, 0.05, 44100), 44100);
            }
            var folge = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                var lied = lieder.Naechstes()!.Value;
                lied.Strom.Dispose();
                folge.Add(lied.Name);
            }
            Assert.Equal(["A.wav", "b.wav", "c.wav", "A.wav"], folge);
        }
        finally
        {
            Directory.Delete(ordner, true);
        }
    }

    // -- Ausgabe ----------------------------------------------------------------------

    private sealed class KaputteAusgabe : ITonausgabe
    {
        public bool Geschlossen;

        public string Name => "kaputt";

        public void Starten(Mischer mischer) => throw new InvalidOperationException("kein Geraet");

        public void Anhalten()
        {
        }

        public void Dispose() => Geschlossen = true;
    }

    [Fact]
    public void OhneGeraetStillOhneFehlerprotokoll()
    {
        var mischer = new Mischer();
        var kaputt = new KaputteAusgabe();
        var ausgabe = Tonwahl.Oeffnen(mischer, () => kaputt, null);
        Assert.IsType<StilleAusgabe>(ausgabe);
        Assert.True(kaputt.Geschlossen);
        Assert.Equal("Ton: kein Audiogerät (InvalidOperationException)", Tonwahl.Zeile);
        Assert.Equal("still (kein Gerät)", ausgabe.Name);
        var gerufen = false;
        Assert.IsType<StilleAusgabe>(Tonwahl.Oeffnen(mischer, () =>
        {
            gerufen = true;
            return kaputt;
        }, " AUS "));
        Assert.False(gerufen);
        Assert.Equal("Ton: aus (LERNPLATTFORM_TON=aus)", Tonwahl.Zeile);
        Assert.False(Tonwahl.Aus("an"));
        Assert.False(Tonwahl.Aus(null));
    }

    // -- Wann der Ton laeuft (Weisung P8-17e) -----------------------------------------

    [Fact]
    public void NachDemHintergrundVonSelbstWiederAn()
    {
        var schalter = new Tonschalter();
        Assert.Null(schalter.Setzen(hintergrund: false));
        Assert.True(schalter.Setzen(imSpiel: true));
        Assert.True(schalter.Laeuft);
        // Pause -> Fortsetzen: aus, dann von selbst wieder an (ohne den Ton aus- und einzuschalten)
        Assert.False(schalter.Setzen(hintergrund: true));
        Assert.False(schalter.Laeuft);
        Assert.Null(schalter.Setzen(hintergrund: true));
        Assert.True(schalter.Setzen(hintergrund: false));
        Assert.Equal(1, schalter.Wiederanlaeufe);
        Assert.Equal(2, schalter.Starts);
        // zweimal hintereinander
        schalter.Setzen(hintergrund: true);
        Assert.True(schalter.Setzen(hintergrund: false));
        Assert.Equal(2, schalter.Wiederanlaeufe);
    }

    [Fact]
    public void OhneSpielOderBeiGesamtNullKeinWiederanlauf()
    {
        var schalter = new Tonschalter();
        // Hintergrund ausserhalb der Spielseite: nichts
        Assert.Null(schalter.Setzen(hintergrund: true));
        Assert.Null(schalter.Setzen(hintergrund: false));
        Assert.True(schalter.Setzen(imSpiel: true));
        // Gesamt 0: aus; zurueck aus dem Hintergrund bleibt es aus
        Assert.False(schalter.Setzen(gesamt: 0));
        Assert.Null(schalter.Setzen(hintergrund: true));
        Assert.Null(schalter.Setzen(hintergrund: false));
        Assert.False(schalter.Laeuft);
        Assert.True(schalter.Setzen(gesamt: 40));
        Assert.Equal(0, schalter.Wiederanlaeufe);
        // Spiel verlassen und wieder betreten ist kein Wiederanlauf
        Assert.False(schalter.Setzen(imSpiel: false));
        Assert.True(schalter.Setzen(imSpiel: true));
        Assert.Equal(0, schalter.Wiederanlaeufe);
        Assert.Equal(3, schalter.Starts);
        // im Hintergrund das Spiel verlassen: beim Zurueckkehren kein Ton
        schalter.Setzen(hintergrund: true);
        Assert.Null(schalter.Setzen(imSpiel: false));
        Assert.Null(schalter.Setzen(hintergrund: false));
        Assert.False(schalter.Laeuft);
        Assert.Equal(0, schalter.Wiederanlaeufe);
    }

    // -- Tonplan ----------------------------------------------------------------------

    [Fact]
    public void NaeheWieNico2a()
    {
        Assert.Equal(1f, Tonplan.Naehe(0));
        Assert.Equal(0.5f, Tonplan.Naehe(6));
        Assert.Equal(1f / (1f + (24.9f / 6f)), Tonplan.Naehe(24.9f));
        Assert.Equal(0f, Tonplan.Naehe(25));
        Assert.Equal(0f, Tonplan.Naehe(float.PositiveInfinity));
        Assert.Equal(1f, Tonplan.Naehe(-2));
    }

    [Fact]
    public void BodenJeOrt()
    {
        Assert.Equal(Untergrund.Stein, Tonplan.Boden(null, false));
        Assert.Equal(Untergrund.Gras, Tonplan.Boden(null, true));
        Assert.Equal(Untergrund.Teppich, Tonplan.Boden("zuhause", false));
        Assert.Equal(Untergrund.Teppich, Tonplan.Boden("buero", false));
        Assert.Equal(Untergrund.Holz, Tonplan.Boden("kaufhaus", false));
        Assert.Equal("schritt_concrete", Tonplan.Schluessel(new Tonanlass(Anlass.Schritt, Untergrund.Schnee)));
        Assert.Equal(Tongruppe.Musik, Tonplan.Gruppe(Anlass.Feierabend));
        Assert.Equal(Tongruppe.Umgebung, Tonplan.Gruppe(Anlass.Kasse));
    }

    [Fact]
    public void AnlaesseAusEreignissen()
    {
        Assert.Equal(Anlass.Kasse, Tonplan.AusEreignis(Ereignisse.MoebelGekauft, []));
        Assert.Equal(Anlass.Kasse, Tonplan.AusEreignis(Geschaefte.TeilGekauft, []));
        Assert.Equal(Anlass.Kasse, Tonplan.AusEreignis(Kleidung.Gekauft, []));
        Assert.Equal(Anlass.Kasse, Tonplan.AusEreignis(Ereignisse.Kredit, []));
        Assert.Equal(Anlass.Feierabend, Tonplan.AusEreignis(Ereignisse.Feierabend, []));
        Assert.Equal(Anlass.Erfolg, Tonplan.AusEreignis(Ereignisse.Erfolg, []));
        Assert.Equal(Anlass.Ticket, Tonplan.AusEreignis(Ereignisse.Erledigt, new PyObjekt { { "richtig", true } }));
        Assert.Null(Tonplan.AusEreignis(Ereignisse.Erledigt, new PyObjekt { { "richtig", false } }));
        Assert.Null(Tonplan.AusEreignis(Ereignisse.Erledigt, []));
        Assert.Null(Tonplan.AusEreignis(Kleidung.Getragen, []));
    }

    [Fact]
    public void VarianteNieZweimalHintereinander()
    {
        var zufall = new Random(7);
        var letzte = -1;
        var gesehen = new HashSet<int>();
        for (var i = 0; i < 500; i++)
        {
            var wahl = Tonplan.Variante(5, letzte, zufall);
            Assert.InRange(wahl, 0, 4);
            Assert.NotEqual(letzte, wahl);
            gesehen.Add(wahl);
            letzte = wahl;
        }
        Assert.Equal(5, gesehen.Count);
        Assert.Equal(0, Tonplan.Variante(1, 0, zufall));
        for (var i = 0; i < 200; i++)
        {
            Assert.InRange(Tonplan.NaechsterRuf(zufall), 8, 25);
        }
    }

    private static readonly PyObjekt Aussehen = new() { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } };

    private static StadtSpiel NeuesSpiel()
    {
        var stadt = new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
        var ablauf = new Spielablauf(stadt.Inhalt);
        ablauf.Profil("Probe", Aussehen, Ereignisse.Normal);
        return new StadtSpiel(stadt, ablauf, null, SpielInnenraumTests.Innenraeume.Value);
    }

    [Fact]
    public void StadtMitParkUndFluss()
    {
        var szene = NeuesSpiel().Szene;
        Assert.NotEmpty(szene.Parks);
        Assert.NotEmpty(szene.Fluesse);
        var park = szene.Parks[0];
        var (px, pz) = ((park.X1 + park.X2) / 2, (park.Z1 + park.Z2) / 2);
        Assert.True(Tonplan.ImPark(szene, px, pz));
        Assert.True(Tonplan.VogelNah(szene, px, pz));
        var fluss = szene.Fluesse[0][0];
        Assert.Equal(0f, Tonplan.FlussAbstand(szene, fluss.X, fluss.Y), 4);
        var lage = Tonplan.Stadt(szene, fluss.X, fluss.Y, null, faehrt: true);
        Assert.Equal(1f, lage.Fluss, 4);
        Assert.Empty(lage.Autos);
        Assert.Equal(0f, Tonplan.Stadt(szene, fluss.X + 500, fluss.Y + 500, null, false).Fluss);
    }

    [Fact]
    public void AutosNurWennSieFahren()
    {
        var spiel = NeuesSpiel();
        var verkehr = Verkehr.Fuer(spiel.Szene);
        Assert.NotEmpty(verkehr.Autos);
        var (ort, _) = verkehr.Autos[0].Lage();
        var lage = Tonplan.Stadt(spiel.Szene, ort.X, ort.Y, verkehr, faehrt: true);
        Assert.Equal(verkehr.Autos.Count, lage.Autos.Count);
        Assert.Equal(verkehr.Autos[0].Haelt ? Tonplan.Leerlauf : 1f, lage.Autos[0].Laut, 4);
        Assert.All(Tonplan.Stadt(spiel.Szene, ort.X, ort.Y, verkehr, faehrt: false).Autos, a => Assert.Equal(0f, a.Laut));
    }

    [Fact]
    public void SchritteUndTuerenAusDemSpiel()
    {
        var spiel = NeuesSpiel();
        var toene = new List<Tonanlass>();
        spiel.Ton += toene.Add;
        Assert.True(spiel.GeheZu(spiel.FigurX + 3, spiel.FigurZ));
        while (spiel.Geht)
        {
            spiel.Fortschreiten(0.02);
        }
        var schritte = toene.Count(t => t.Art == Anlass.Schritt);
        // je halber Doppelschritt ein Schritt (Weg / (Zyklus / 2), auf ganze Haelften)
        Assert.InRange(schritte, (int)Math.Floor(spiel.Schrittweg / (StadtSpiel.ZyklusStadt / 2)) - 1, (int)Math.Floor(spiel.Schrittweg / (StadtSpiel.ZyklusStadt / 2)) + 1);
        Assert.True(schritte >= 2);
        toene.Clear();
        Assert.Contains(spiel.GeheZu("zuhause"), new[] { StadtGrund.Ok, StadtGrund.SchonDa });
        spiel.SofortAnkommen();
        toene.Clear();
        Assert.True(spiel.Betreten());
        Assert.Equal([new Tonanlass(Anlass.TuerAuf)], toene);
        Assert.Equal(Untergrund.Teppich, spiel.Boden);
        toene.Clear();
        spiel.Verlassen();
        Assert.Equal([new Tonanlass(Anlass.TuerZu)], toene);
    }

    [Fact]
    public void KasseNachDemGeschriebenenKredit()
    {
        var speicher = new SpielSpeicher(() => "2026-10-03 00:00:00");
        foreach (var e in SpielFingerabdruckTests.Gespielt.Value.Stand.Verlauf)
        {
            speicher.Schreiben(e.Art, PythonJson.Standard(e.Daten));
        }
        var ablauf = new Spielablauf(SpielInhaltTests.Inhalt, speicher);
        var gemeldet = new List<(string Art, int Zeilen)>();
        ablauf.NachEreignis = (art, _) => gemeldet.Add((art, ablauf.Stand.Verlauf.Count));
        var stadt = new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
        Assert.Equal(StadtGrund.Ok, stadt.Gehen(ablauf, Schalter.Bank).Grund);
        if (stadt.Betreten(ablauf.Stand) == StadtGrund.Geschlossen)
        {
            Assert.Equal(StadtGrund.Ok, stadt.Warten(ablauf).Grund);
        }
        gemeldet.Clear();
        var paket = Wirtschaft.KreditPakete(ablauf.Stand, SpielInhaltTests.Inhalt).First(a => !Py.Wahr(a["problem"]));
        var vorher = ablauf.Stand.Verlauf.Count;
        ablauf.BankKreditAufnehmen(stadt, 0L, 0L, paket["paket"]);
        Assert.Equal(Ereignisse.Kredit, gemeldet[0].Art);
        // gemeldet erst, wenn die Zeile im Stand steht
        Assert.True(gemeldet[0].Zeilen > vorher);
        Assert.Equal(Anlass.Kasse, Tonplan.AusEreignis(gemeldet[0].Art, []));
    }

    /// <summary>
    /// Weisung P8-19: wirft der Haken der Darstellung, bleibt die Spiel-Logik gleich: Ereignis angewandt,
    /// Erfolge geprueft (dieselben Zeilen wie ohne Haken), Fingerabdruck gleich, Eintrag in fehler.log.
    /// </summary>
    [Fact]
    public void WerfenderHakenBeruehrtDieSpielLogikNicht()
    {
        (List<string> Zeilen, string Hash, int Erfolge) Spielen(bool werfen, string? log)
        {
            var speicher = new SpielSpeicher(() => "2026-10-03 00:00:00");
            // ohne die Erfolge des Szenarios: die erste Aktion schaltet sie wieder frei (check_achievements)
            foreach (var e in SpielFingerabdruckTests.Gespielt.Value.Stand.Verlauf.Where(e => e.Art != Ereignisse.Erfolg))
            {
                speicher.Schreiben(e.Art, PythonJson.Standard(e.Daten));
            }
            var ablauf = new Spielablauf(SpielInhaltTests.Inhalt, speicher) { NachEreignisFehlerPfad = log };
            if (werfen)
            {
                ablauf.NachEreignis = (art, _) => throw new InvalidOperationException("Probe " + art);
            }
            var vorher = ablauf.Stand.Verlauf.Count;
            var stadt = new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
            Assert.Equal(StadtGrund.Ok, stadt.Gehen(ablauf, Schalter.Bank).Grund);
            if (stadt.Betreten(ablauf.Stand) == StadtGrund.Geschlossen)
            {
                Assert.Equal(StadtGrund.Ok, stadt.Warten(ablauf).Grund);
            }
            var paket = Wirtschaft.KreditPakete(ablauf.Stand, SpielInhaltTests.Inhalt).First(a => !Py.Wahr(a["problem"]));
            ablauf.BankKreditAufnehmen(stadt, 0L, 0L, paket["paket"]);
            ablauf.SchlafenGehen();
            // Erfolge pruefen ohne Haken nachgeholt: nichts darf mehr fehlen
            Assert.Empty(ablauf.ErfolgePruefen());
            var neu = ablauf.Stand.Verlauf.Skip(vorher).ToList();
            return ([.. neu.Select(e => e.Art + " " + PythonJson.Standard(e.Daten))], SpielFingerabdruckTests.Hash(ablauf.Stand),
                neu.Count(e => e.Art == Ereignisse.Erfolg));
        }

        var ordner = Path.Combine(Path.GetTempPath(), "lp_haken_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ordner);
        try
        {
            var log = Path.Combine(ordner, Fehlerprotokoll.DateiName);
            var ohne = Spielen(false, log);
            Assert.False(File.Exists(log));
            var mit = Spielen(true, log);
            Assert.Contains(ohne.Zeilen, z => z.StartsWith(Ereignisse.Kredit + " ", StringComparison.Ordinal));
            Assert.Contains(ohne.Zeilen, z => z.StartsWith(Ereignisse.Feierabend + " ", StringComparison.Ordinal));
            Assert.True(ohne.Erfolge > 0, "der Ablauf soll mindestens einen Erfolg freischalten");
            Assert.Equal(ohne.Zeilen, mit.Zeilen);
            Assert.Equal(ohne.Erfolge, mit.Erfolge);
            Assert.Equal(ohne.Hash, mit.Hash);
            var text = File.ReadAllText(log);
            Assert.Contains("Spiel: Darstellung nach Ereignis " + Ereignisse.Kredit + " fehlgeschlagen: InvalidOperationException: Probe " + Ereignisse.Kredit, text, StringComparison.Ordinal);
            Assert.Contains("Darstellung nach Ereignis " + Ereignisse.Feierabend + " fehlgeschlagen", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(ordner, true);
        }
    }

    // -- Toene im Programm ------------------------------------------------------------

    /// <summary>Pruefsumme ueber die 40 Toene aus W8 (Datei und SHA-256 in toene.json): neue Toene nur angehaengt.</summary>
    private const string SollToeneW8 = "4bab89e27fa317e51ec700e63a49d98b672d39a44192fc94ffc5c9a03856958f";

    private static List<(string Datei, string Anlass, string Sha)> Eintraege()
    {
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(ToeneOrdner, "toene.json")));
        return [.. index.RootElement.GetProperty("dateien").EnumerateArray().Select(e => (
            e.GetProperty("datei").GetString()!, e.GetProperty("anlass").GetString()!, e.GetProperty("sha256").GetString()!))];
    }

    [Fact]
    public void ToeneNurAngehaengt()
    {
        var eintraege = Eintraege();
        Assert.True(eintraege.Count >= 40);
        var text = string.Concat(eintraege.Take(40).Select(e => e.Datei + ":" + e.Sha + "\n"));
        Assert.Equal(SollToeneW8, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
        foreach (var (datei, _, sha) in eintraege)
        {
            Assert.Equal(sha, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(ToeneOrdner, datei)))));
        }
        // jede Datei im Ordner steht in der Liste, keine doppelt
        var dateien = Directory.GetFiles(ToeneOrdner).Select(n => Path.GetFileName(n)).Where(n => n != "toene.json").Order(StringComparer.Ordinal).ToList();
        Assert.Equal(dateien, eintraege.Select(e => e.Datei).Order(StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void JederAnlassHatToeneUndAlleSindLesbar()
    {
        var eintraege = Eintraege();
        var anlaesse = eintraege.Select(e => e.Anlass).ToHashSet(StringComparer.Ordinal);
        foreach (var art in Enum.GetValues<Anlass>())
        {
            var boeden = art == Anlass.Schritt ? new[] { Untergrund.Stein, Untergrund.Gras, Untergrund.Holz, Untergrund.Teppich } : [Untergrund.Stein];
            foreach (var boden in boeden)
            {
                Assert.Contains(Tonplan.Schluessel(new Tonanlass(art, boden)), anlaesse);
            }
        }
        Assert.Contains(Tonplan.StadtGrundSchluessel, anlaesse);
        Assert.Contains(Tonplan.FlussSchluessel, anlaesse);
        foreach (var (modell, _, _) in Verkehr.Strecken)
        {
            Assert.Contains(Tonplan.AutoSchluessel(modell), anlaesse);
        }
        // Schritte je Boden fuenf Varianten (keine zweimal hintereinander)
        Assert.All(new[] { "schritt_concrete", "schritt_grass", "schritt_wood", "schritt_carpet" }, s => Assert.Equal(5, eintraege.Count(e => e.Anlass == s)));
        long groesse = 0;
        foreach (var (datei, anlass, _) in eintraege)
        {
            using var stream = File.OpenRead(Path.Combine(ToeneOrdner, datei));
            groesse += stream.Length;
            var ton = Tondekoder.Lesen(stream, Path.GetExtension(datei));
            Assert.True(ton.Proben.Length > 1000, datei);
            Assert.All(ton.Proben, p => Assert.InRange(p, -1.0001f, 1.0001f));
            if (anlass is Tonplan.StadtGrundSchluessel or Tonplan.FlussSchluessel)
            {
                Assert.InRange(ton.Sekunden, 60, 90);
            }
        }
        // Groesse der Toene (Meldung P8-17): unter 2 MB
        Assert.True(groesse < 2_000_000, "Toene " + groesse + " Bytes");
    }
}
