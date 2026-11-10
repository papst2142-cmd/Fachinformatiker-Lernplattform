using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle W-B S 0.30.0 (Weisung P12-136, Kurzplan P12-79 Wunsch 2): eine Reihe von Baeumen oder Katalog-Stuecken. Vorschau
/// (Plan) und Schritt sind gleich, die Reihe ist EIN Schritt, rote Stellen werden ausgelassen und gezaehlt, die Karte zeigt
/// „+n Dreiecke“. Gegenprobe gegen die volle Pruefung draussen: was die Reihe setzt, bringt keinen neuen roten Fund; was sie
/// auslaesst, waere einzeln gesetzt rot.
/// </summary>
public class ReiheTests
{
    private static Spieldaten D => Daten.Spiel;

    private const string Busch = "nature/plant_bush";
    private const string Zaun = "nature/fence_simple";

    private static Projekt Leer() => Projekt.Neu("Reihe", D, Daten.NeueAblage(), leer: (32, 24));

    private static List<(double X, double Y)> Katalog(Projekt p) =>
        [.. AussenBearbeitung.Alle(p.AussenStand).Where(z => z.Stelle.Art == AussenArt.Katalog).Select(z => (z.X, z.Y))];

    [Fact]
    public void Reihe_PlanGleichSchritt_EinSchritt_RueckgaengigNimmtAlleWeg()
    {
        var p = Leer();
        var wahl = new ReiheWahl(1.0);
        var plan = AussenBearbeitung.ReihePlanen(p.AussenStand, AussenArt.Katalog, Busch, 4, 4, 10, 4, wahl, 0, true);
        Assert.Equal(7, plan.Frei);
        Assert.Equal(0, plan.Ausgelassen);
        Assert.Equal(7 * 32, plan.Dreiecke);
        var schritte = p.SchritteZurueck;
        var (neu, aus) = p.AussenAendern("Reihe", a => AussenBearbeitung.ReiheSetzen(a, AussenArt.Katalog, Busch, 4, 4, 10, 4, wahl, 0, true));
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        Assert.Equal(7, neu.Count);
        Assert.Equal(0, aus);
        Assert.Equal(plan.Stuecke.Select(s => (s.X, s.Y)).ToList(), Katalog(p));
        Assert.True(p.Rueckgaengig());
        Assert.Empty(Katalog(p));
    }

    [Fact]
    public void Reihe_AbstandBeideSeitenUndHoechstens()
    {
        var p = Leer();
        var plan = AussenBearbeitung.ReihePlanen(p.AussenStand, AussenArt.Katalog, Busch, 4, 4, 10, 4, new ReiheWahl(2.0, BeideSeiten: true), 0, true);
        Assert.Equal(8, plan.Stuecke.Count);
        Assert.Equal([4.0 - AussenBearbeitung.ReiheSeite, 4.0 + AussenBearbeitung.ReiheSeite],
            plan.Stuecke.Select(s => Math.Round(s.Y, 2)).Distinct().Order().ToArray());
        Assert.Equal([4.0, 6.0, 8.0, 10.0], plan.Stuecke.Select(s => s.X).Distinct().Order().ToArray());
        // sehr dicht und lang: nie mehr als ReiheHoechstens
        var viel = AussenBearbeitung.ReihePlanen(p.AussenStand, AussenArt.Katalog, Busch, 1, 1, 31, 23, new ReiheWahl(0.1, BeideSeiten: true), 0, true);
        Assert.Equal(AussenBearbeitung.ReiheHoechstens, viel.Stuecke.Count);
        // Laternen gibt es nicht als Reihe (die stellt das Werkzeug selbst an die Strassen)
        Assert.Empty(AussenBearbeitung.ReihePlanen(p.AussenStand, AussenArt.Laterne, null, 4, 4, 10, 4, new ReiheWahl(1), 0, true).Stuecke);
    }

    [Fact]
    public void Reihe_ZaunLaengs_SteilDrehtEinViertel_ZufaelligGleichBeiGleicherLage()
    {
        var p = Leer();
        Assert.Equal(0.9, AussenBearbeitung.ReiheVorgabe(AussenArt.Katalog, Zaun));
        Assert.Equal(1.0, AussenBearbeitung.ReiheVorgabe(AussenArt.Baum, null));
        var waagrecht = AussenBearbeitung.ReihePlanen(p.AussenStand, AussenArt.Katalog, Zaun, 4, 4, 10, 4, new ReiheWahl(0.9), 0, true);
        Assert.All(waagrecht.Stuecke, s => Assert.Equal(0, s.Dreh));
        var senkrecht = AussenBearbeitung.ReihePlanen(p.AussenStand, AussenArt.Katalog, Zaun, 4, 4, 4, 10, new ReiheWahl(0.9), 0, true);
        Assert.All(senkrecht.Stuecke, s => Assert.Equal(1, s.Dreh));
        var z1 = AussenBearbeitung.ReihePlanen(p.AussenStand, AussenArt.Katalog, Busch, 2, 2, 30, 2, new ReiheWahl(1, Zufaellig: true), 0, true);
        var z2 = AussenBearbeitung.ReihePlanen(p.AussenStand, AussenArt.Katalog, Busch, 2, 2, 30, 2, new ReiheWahl(1, Zufaellig: true), 0, true);
        Assert.Equal(z1.Stuecke, z2.Stuecke);
        Assert.True(z1.Stuecke.Select(s => s.Dreh).Distinct().Count() > 1, "zufaellig gedreht");
        // Gegenprobe: ohne „zufällig“ alle gleich
        Assert.Single(AussenBearbeitung.ReihePlanen(p.AussenStand, AussenArt.Katalog, Busch, 2, 2, 30, 2, new ReiheWahl(1), 0, true).Stuecke.Select(s => s.Dreh).Distinct());
    }

    [Fact]
    public void Reihe_UeberDieStrasse_RoteStellenAusgelassen_KeinNeuerRoterFund()
    {
        var p = Leer();
        p.AussenAendern("Straße", a => AussenBearbeitung.Setzen(a, AussenArt.Strasse, 16, 12));
        var vorher = p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text).ToList();
        var wahl = new ReiheWahl(0.5);
        var plan = AussenBearbeitung.ReihePlanen(p.AussenStand, AussenArt.Katalog, Busch, 16, 8, 16, 16, wahl, 0, true);
        Assert.True(plan.Ausgelassen > 0, "auf der Fahrbahn ist rot");
        Assert.True(plan.Frei > 0);
        Assert.Contains(plan.Stuecke, s => s.Grund == Texte.ReiheAufFahrbahn);
        var (neu, aus) = p.AussenAendern("Reihe", a => AussenBearbeitung.ReiheSetzen(a, AussenArt.Katalog, Busch, 16, 8, 16, 16, wahl, 0, true));
        Assert.Equal(plan.Frei, neu.Count);
        Assert.Equal(plan.Ausgelassen, aus);
        var nachher = p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text).ToList();
        Assert.Equal(vorher.Order(), nachher.Order());

        // Gegenprobe: eine ausgelassene Stelle einzeln gesetzt ist in der vollen Pruefung rot
        var rot = plan.Stuecke.First(s => s.Grund == Texte.ReiheAufFahrbahn);
        p.AussenAendern("Einzeln", a => AussenBearbeitung.SetzenUndDrehen(a, AussenArt.Katalog, rot.X, rot.Y, false, Busch, 0));
        Assert.True(p.PruefenAussen().Count(f => f.Rot) > nachher.Count);
    }

    [Fact]
    public void Reihe_Baeume_HausWasserGleis_UndDoppelt()
    {
        var p = Projekt.Neu("Reihe Stadt 1", D, Daten.NeueAblage());
        var a = p.AussenStand;
        var (b, h) = Aussen.Groesse(a);
        // quer ueber die ganze Stadt: viele Gruende kommen vor
        var plan = AussenBearbeitung.ReihePlanen(a, AussenArt.Baum, null, 0.5, h / 2.0, b - 0.5, h / 2.0, new ReiheWahl(0.5), 0, true, D.Grafik);
        var gruende = plan.Stuecke.Select(s => s.Grund).OfType<string>().ToHashSet();
        Assert.Contains(Texte.ReiheImHaus, gruende);
        Assert.True(plan.Frei > 0);
        Assert.True(plan.Dreiecke > 0, "die Karte zeigt +n Dreiecke");
        // Gegenprobe: dieselbe Reihe auf einer leeren Stadt hat keinen Grund
        var leer = Leer();
        Assert.Equal(0, AussenBearbeitung.ReihePlanen(leer.AussenStand, AussenArt.Baum, null, 0.5, 6, 31.5, 6, new ReiheWahl(0.5), 0, true, D.Grafik).Ausgelassen);
        // gleiche Stelle zweimal (Abstand 0,1 auf dem Raster 0,1 beide Seiten auf einer Linie der Laenge 0): doppelt
        var punkt = AussenBearbeitung.ReihePlanen(leer.AussenStand, AussenArt.Baum, null, 6, 6, 6, 6, new ReiheWahl(0.1), 0, true);
        Assert.Single(punkt.Stuecke);
        // neben einem schon stehenden Baum: rot
        leer.AussenAendern("Baum", st => AussenBearbeitung.Setzen(st, AussenArt.Baum, 8, 6));
        var neben = AussenBearbeitung.ReihePlanen(leer.AussenStand, AussenArt.Katalog, Busch, 6, 6, 10, 6, new ReiheWahl(1), 0, true);
        Assert.Contains(neben.Stuecke, s => s.Grund == Texte.ReiheAufBaum);
    }
}
