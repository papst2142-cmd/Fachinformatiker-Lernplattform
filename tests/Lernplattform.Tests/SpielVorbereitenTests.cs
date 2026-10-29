using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Weisung P7-16 (Haenger beim Betreten): der Innenraum wird beim Hingehen im Hintergrund aus einer Kopie
/// des Spielstands vorbereitet. Was man sieht, bleibt gleich: Teile, Modelle, Personen und Netze sind
/// dieselben wie beim sofortigen Bauen. Genommen wurde die Vorbereitung bis W11 nur, wenn der Spielstand noch
/// derselbe war; seit W12 (Weisung P8-55) wird sie an ihrem Inhalt erkannt (Inhalts-Signatur), dazu das Vorladen
/// aller Innenraeume beim Oeffnen und die Szene fuer ein neues Fest im Haus.
/// </summary>
public sealed class SpielVorbereitenTests
{
    private static List<string> Ansichten()
    {
        var ansichten = new List<string> { "buero", "zuhause", "serverfarm" };
        ansichten.AddRange(SpielDaten.Kundenorte(SpielInhaltTests.Inhalt).Select(k => "kunde:" + Py.Str(k["id"])));
        ansichten.AddRange(["geschaeft:moebelhaus", "geschaeft:elektromarkt", "schalter:bank", "schalter:amt"]);
        return ansichten;
    }

    private static void GleichesBild(Innenraum soll, Innenraum ist)
    {
        Assert.Equal(soll.Ansicht, ist.Ansicht);
        Assert.Equal((soll.Breite, soll.Tiefe, soll.StartX, soll.StartZ), (ist.Breite, ist.Tiefe, ist.StartX, ist.StartZ));
        Assert.Equal(soll.Teile, ist.Teile);
        Assert.Equal(soll.Stuecke, ist.Stuecke);
        Assert.Equal(soll.Signatur, ist.Signatur);
        Assert.Equal(soll.Waende, ist.Waende);
        Assert.Equal(soll.Personen.Select(p => (p.Id, p.Name, p.X, p.Z, p.Auftrag)), ist.Personen.Select(p => (p.Id, p.Name, p.X, p.Z, p.Auftrag)));
        var netz = soll.NetzBauen(ohneWaende: true);
        Assert.True(netz.EckenFeld.SequenceEqual(ist.NetzOhneWaende.EckenFeld), soll.Ansicht + ": Ecken");
        Assert.True(netz.IndizesFeld.SequenceEqual(ist.NetzOhneWaende.IndizesFeld), soll.Ansicht + ": Indizes");
        foreach (var wand in soll.Waende)
        {
            Assert.True(soll.WandNetz(wand).EckenFeld.SequenceEqual(ist.WandNetzFertig(wand).EckenFeld), soll.Ansicht + ": Wand");
        }
        Assert.Equal(soll.Lauf.Frei.ToList(), ist.Lauf.Frei.ToList());
    }

    [Fact]
    public async Task AusKopieVorbereitetGleichDemSofortGebauten()
    {
        var ablauf = new Spielablauf(SpielInhaltTests.Inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" } }, Ereignisse.Normal);
        ablauf.Stand.TageFertig = 200;
        var grafik = SpielGrafikTests.Bibliothek.Value;
        foreach (var ansicht in Ansichten())
        {
            var soll = Innenraum.Bauen(ansicht, ablauf.Stand, SpielInnenraumTests.Innenraeume.Value, grafik)!;
            var kopie = ablauf.Stand.Kopie();
            var ist = await Task.Run(() =>
            {
                var innen = Innenraum.Bauen(ansicht, kopie, SpielInnenraumTests.Innenraeume.Value, grafik)!;
                innen.Vorbereiten();
                return innen;
            }, TestContext.Current.CancellationToken);
            GleichesBild(soll, ist);
        }
        // auch ein gespielter Stand (Auftraege, Kollegen) mit seinen Personen
        var gespielt = SpielFingerabdruckTests.Gespielt.Value.Stand;
        foreach (var ansicht in Ansichten())
        {
            if (Innenraum.Bauen(ansicht, gespielt, SpielInnenraumTests.Innenraeume.Value, grafik) is not { } soll)
            {
                continue;
            }
            var ist = Innenraum.Bauen(ansicht, gespielt.Kopie(), SpielInnenraumTests.Innenraeume.Value, grafik)!;
            ist.Vorbereiten();
            GleichesBild(soll, ist);
        }
    }

    private static StadtSpiel NeuesSpiel(Func<string?>? fest = null, Func<Jahreszeit>? jahreszeit = null)
    {
        var stadt = new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
        var ablauf = new Spielablauf(stadt.Inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        return new StadtSpiel(stadt, ablauf, null, SpielInnenraumTests.Innenraeume.Value, null, fest is null ? null : FestDekoTests.Daten.Value, fest, jahreszeit);
    }

    /// <summary>
    /// Bis W11 hiess der Test BetretenNimmtDieVorbereitungNurBeiGleichemStand: nach einem Ereignis (neuer Stand) wurde
    /// sofort gebaut. Seit W12 (Weisung P8-55) zaehlt der Inhalt: ein Ereignis, das am Buero nichts aendert (eine Minute
    /// laufen), nimmt die Vorbereitung; ein anderer Inhalt (hier eine Festzeit) nicht (<see cref="AndererInhaltWirdNeuGebaut"/>).
    /// </summary>
    [Fact]
    public void BetretenNimmtDieVorbereitungBeiGleichemInhalt()
    {
        var spiel = NeuesSpiel();
        // zu Hause: beim Oeffnen vorbereitet
        Assert.True(spiel.Betreten());
        Assert.Equal(1, spiel.VorbereitetGenommen);
        GleichesBild(Innenraum.Bauen("zuhause", spiel.Ablauf.Stand, SpielInnenraumTests.Innenraeume.Value, null)!, spiel.Innen!);
        spiel.Verlassen();
        // auf dem Weg ins Buero vorbereitet
        Assert.Equal(StadtGrund.Ok, spiel.GeheZu("bitweiche"));
        spiel.SofortAnkommen();
        Assert.True(spiel.Betreten());
        Assert.Equal(2, spiel.VorbereitetGenommen);
        Assert.Equal("buero", spiel.FlaecheId);
        GleichesBild(Innenraum.Bauen("buero", spiel.Ablauf.Stand, SpielInnenraumTests.Innenraeume.Value, null)!, spiel.Innen!);
        spiel.Verlassen();
        // ein Ereignis dazwischen (neuer Stand, gleicher Inhalt des Bueros): die Vorbereitung gilt weiter
        spiel.Vorbereiten("bitweiche");
        var stand = spiel.Ablauf.Stand;
        Assert.True(spiel.Stadt.Laufen(spiel.Ablauf, 1).Ok);
        Assert.NotSame(stand, spiel.Ablauf.Stand);
        Assert.True(spiel.Betreten());
        Assert.Equal(3, spiel.VorbereitetGenommen);
        GleichesBild(Innenraum.Bauen("buero", spiel.Ablauf.Stand, SpielInnenraumTests.Innenraeume.Value, null)!, spiel.Innen!);
    }

    [Fact]
    public void AndererInhaltWirdNeuGebaut()
    {
        string? fest = null;
        var spiel = NeuesSpiel(() => fest);
        // vor der Wohnung vorbereitet ohne Festzeit; beim Betreten ist Weihnachten: anderer Inhalt, sofort gebaut
        fest = Festzeit.Weihnachten;
        Assert.True(spiel.Betreten());
        Assert.Equal(0, spiel.VorbereitetGenommen);
        Assert.Equal(Festzeit.Weihnachten, spiel.Innen!.Fest);
        GleichesBild(Innenraum.Bauen("zuhause", spiel.Ablauf.Stand, SpielInnenraumTests.Innenraeume.Value, null, null, fest, FestDekoTests.Daten.Value)!, spiel.Innen!);
        // der eben gebaute liegt jetzt im Vorrat: noch einmal hinein ohne Aenderung nimmt ihn
        var innen = spiel.Innen;
        spiel.Verlassen();
        Assert.True(spiel.Betreten());
        Assert.Equal(1, spiel.VorbereitetGenommen);
        Assert.Same(innen, spiel.Innen);
    }

    [Fact]
    public void InhaltSignaturGleichFuerKopieUndAndersMitFestUndJahreszeit()
    {
        var stand = SpielFingerabdruckTests.Gespielt.Value.Stand;
        var raeume = SpielInnenraumTests.Innenraeume.Value;
        var gesehen = 0;
        foreach (var ansicht in Ansichten())
        {
            var signatur = Innenraum.InhaltSignatur(ansicht, stand, raeume, null, null, Jahreszeit.Sommer);
            Assert.Equal(Innenraum.Bauen(ansicht, stand, raeume, null) is null, signatur is null);
            if (signatur is null)
            {
                continue;
            }
            gesehen++;
            Assert.Equal(signatur, Innenraum.InhaltSignatur(ansicht, stand.Kopie(), raeume, null, null, Jahreszeit.Sommer));
            Assert.NotEqual(signatur, Innenraum.InhaltSignatur(ansicht, stand, raeume, null, Festzeit.Weihnachten, Jahreszeit.Sommer));
            Assert.NotEqual(signatur, Innenraum.InhaltSignatur(ansicht, stand, raeume, null, null, Jahreszeit.Winter));
        }
        Assert.True(gesehen >= 8, "nur " + gesehen + " Innenraeume");
        // ein anderer Auftrag im Buero (Person mit "!") aendert die Signatur des Bueros
        var buero = Innenraum.Bauen("buero", stand, raeume, null)!;
        Assert.Equal(buero.Personen.Any(p => p.Auftrag), Innenraum.InhaltSignatur("buero", stand, raeume, null, null, Jahreszeit.Sommer)!.Contains("true]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AlleVorbereitenBautJedenInnenraumWieSofortUndAendertNichts()
    {
        var spiel = NeuesSpiel();
        var stand = spiel.Ablauf.Stand;
        var ereignisse = stand.Verlauf.Count;
        await spiel.AlleVorbereiten();
        // kein Ereignis, kein neuer Stand (Fingerabdruecke bleiben, Darstellung nur)
        Assert.Same(stand, spiel.Ablauf.Stand);
        Assert.Equal(ereignisse, spiel.Ablauf.Stand.Verlauf.Count);
        var ansichten = spiel.Szene.Orte.Select(o => o.Ansicht).Where(a => Innenraum.OrtFuer(a) is not null && Innenraum.Bauen(a, stand, SpielInnenraumTests.Innenraeume.Value, null) is not null)
            .Distinct().ToList();
        Assert.True(ansichten.Count >= 6, "nur " + ansichten.Count);
        Assert.Equal(ansichten.Count, spiel.Vorrat.Count);
        foreach (var ansicht in ansichten)
        {
            var soll = Innenraum.Bauen(ansicht, stand, SpielInnenraumTests.Innenraeume.Value, null)!;
            GleichesBild(soll, spiel.Vorrat.Single(i => i.Ansicht == ansicht));
        }
        // ein zweites Vorladen baut nichts neu (gleiche Gegenstaende)
        var vorher = spiel.Vorrat.ToList();
        await spiel.AlleVorbereiten();
        Assert.Equal(vorher.Count, spiel.Vorrat.Count);
        Assert.All(vorher, i => Assert.Contains(spiel.Vorrat, j => ReferenceEquals(i, j)));
        // Betreten nimmt den vorgeladenen Innenraum
        Assert.True(spiel.Betreten());
        Assert.Equal(1, spiel.VorbereitetGenommen);
        Assert.Contains(vorher, i => ReferenceEquals(i, spiel.Innen));
    }

    [Fact]
    public async Task SzeneFuerNeueJahreszeitImHausVorbereitet()
    {
        var jahreszeit = Jahreszeit.Sommer;
        string? fest = null;
        var spiel = NeuesSpiel(() => fest, () => jahreszeit);
        // draussen geschieht nichts
        jahreszeit = Jahreszeit.Winter;
        spiel.SzeneVorbereiten();
        Assert.Same(spiel.Szene, spiel.SzeneDraussen);
        jahreszeit = Jahreszeit.Sommer;
        Assert.True(spiel.Betreten());
        // im Haus ohne Wechsel auch nicht
        spiel.SzeneVorbereiten();
        Assert.Same(spiel.Szene, spiel.SzeneDraussen);
        jahreszeit = Jahreszeit.Winter;
        fest = Festzeit.Weihnachten;
        spiel.SzeneVorbereiten();
        var bis = DateTime.UtcNow.AddSeconds(60);
        while (ReferenceEquals(spiel.Szene, spiel.SzeneDraussen) && DateTime.UtcNow < bis)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        var vorbereitet = spiel.SzeneDraussen;
        Assert.NotSame(spiel.Szene, vorbereitet);
        spiel.Verlassen();
        Assert.Equal(1, spiel.SzeneVorbereitetGenommen);
        Assert.Same(vorbereitet, spiel.Szene);
        var soll = StadtSzene.Bauen(spiel.Stadt, spiel.Ablauf.Stand, null, Festzeit.Weihnachten, FestDekoTests.Daten.Value, Jahreszeit.Winter);
        Assert.Equal((soll.Signatur, soll.Fest, soll.Jahreszeit), (spiel.Szene.Signatur, spiel.Szene.Fest, spiel.Szene.Jahreszeit));
        Assert.True(soll.NetzBauen().EckenFeld.SequenceEqual(spiel.Szene.Vorbereiten().Stadt.EckenFeld));
        // ein Ereignis, das die sichtbaren Orte nicht aendert, laesst die Vorbereitung gelten; ohne Wechsel bleibt die Szene
        Assert.True(spiel.Betreten());
        spiel.SzeneVorbereiten();
        Assert.Same(spiel.Szene, spiel.SzeneDraussen);
        spiel.Verlassen();
        Assert.Equal(1, spiel.SzeneVorbereitetGenommen);
    }

    [Fact]
    public void SzeneNetzeFertigWieGebaut()
    {
        var spiel = NeuesSpiel(() => Festzeit.Weihnachten);
        var szene = StadtSzene.Bauen(spiel.Stadt, spiel.Ablauf.Stand, SpielGrafikTests.Bibliothek.Value, Festzeit.Weihnachten, FestDekoTests.Daten.Value, Jahreszeit.Winter);
        var fertig = szene.Vorbereiten();
        Assert.Same(fertig, szene.Vorbereiten());
        Assert.True(szene.NetzBauen().EckenFeld.SequenceEqual(fertig.Stadt.EckenFeld));
        Assert.True(szene.NetzBauen().IndizesFeld.SequenceEqual(fertig.Stadt.IndizesFeld));
        Assert.Equal(szene.Haeuser.Count, fertig.Haeuser.Count);
        foreach (var haus in szene.Haeuser)
        {
            Assert.True(szene.HausNetz(haus).EckenFeld.SequenceEqual(fertig.Haeuser[haus].EckenFeld));
        }
        Assert.True(szene.GruenNetzBauen()!.EckenFeld.SequenceEqual(fertig.Gruen!.EckenFeld));
        Assert.True(szene.PaketNetzBauen(false).EckenFeld.SequenceEqual(fertig.PaketNah!.EckenFeld));
        Assert.True(szene.PaketNetzBauen(true).EckenFeld.SequenceEqual(fertig.PaketFern!.EckenFeld));
    }

    [Fact]
    public void LadeanzeigeErstAbDreiZehntelSekunden()
    {
        Assert.Equal(0.3, Ladeanzeige.ZeigenAbSekunden);
        Assert.False(Ladeanzeige.Zeigen(true, 0));
        Assert.False(Ladeanzeige.Zeigen(true, 0.299));
        Assert.True(Ladeanzeige.Zeigen(true, 0.3));
        Assert.True(Ladeanzeige.Zeigen(true, 5));
        Assert.False(Ladeanzeige.Zeigen(false, 5));
        Assert.Equal(0, Ladeanzeige.Anteil(0, 0));
        Assert.Equal(0.5, Ladeanzeige.Anteil(5, 10));
        Assert.Equal(1, Ladeanzeige.Anteil(12, 10));
    }
}
