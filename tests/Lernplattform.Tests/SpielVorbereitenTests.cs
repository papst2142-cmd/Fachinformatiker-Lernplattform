using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Weisung P7-16 (Haenger beim Betreten): der Innenraum wird beim Hingehen im Hintergrund aus einer Kopie
/// des Spielstands vorbereitet. Was man sieht, bleibt gleich: Teile, Modelle, Personen und Netze sind
/// dieselben wie beim sofortigen Bauen. Genommen wird die Vorbereitung nur, wenn der Spielstand noch
/// derselbe ist.
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

    private static StadtSpiel NeuesSpiel()
    {
        var stadt = new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
        var ablauf = new Spielablauf(stadt.Inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        return new StadtSpiel(stadt, ablauf, null, SpielInnenraumTests.Innenraeume.Value);
    }

    [Fact]
    public void BetretenNimmtDieVorbereitungNurBeiGleichemStand()
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
        // ein Ereignis dazwischen (neuer Stand): nicht die Vorbereitung, sondern sofort gebaut
        spiel.Vorbereiten("bitweiche");
        Assert.True(spiel.Stadt.Laufen(spiel.Ablauf, 1).Ok);
        Assert.True(spiel.Betreten());
        Assert.Equal(2, spiel.VorbereitetGenommen);
        GleichesBild(Innenraum.Bauen("buero", spiel.Ablauf.Stand, SpielInnenraumTests.Innenraeume.Value, null)!, spiel.Innen!);
    }
}
