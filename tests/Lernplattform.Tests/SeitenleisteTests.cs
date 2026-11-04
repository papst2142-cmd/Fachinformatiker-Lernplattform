using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.38.0 (Sammelliste 207, Weisung P11-37, Antworten 1a 2a 3a): Gruppen der Seitenleiste.
/// Gegenproben (lokal, je Test rot): Oeffnen ohne Zuklappen der anderen Gruppe -> NurEineGruppeOffen rot;
/// Seite() ohne Rueckgriff auf den Klick -> KlickAufOffeneGruppeKlapptZu rot; Seite ohne Gruppe laesst offen ->
/// SeiteOhneGruppeKlapptAllesZu rot; Eltern nicht geoeffnet -> SprungOeffnetGruppe rot; gleiche Seite wirkt erneut ->
/// GleicheSeiteAendertNichts rot.
/// </summary>
public class SeitenleisteTests
{
    [Fact]
    public void AufbauWieNico()
    {
        var leiste = new Seitenleiste();
        Assert.Equal(["learn", "game", "cards"], Navigation.PcLeiste.Concat(Navigation.PcLernen).Where(leiste.IstGruppe));
        Assert.Equal(Navigation.PcLernen, leiste.Kinder("learn"));
        Assert.Equal(Fachbereiche.Alle, leiste.Kinder("cards"));
        Assert.Equal(["buero", "kunde", "zuhause", "firma", "reise"], leiste.Kinder("game"));
        Assert.Equal("learn", leiste.Eltern("notebook"));
        Assert.Equal("cards", leiste.Eltern(Fachbereiche.Netzwerk));
        Assert.Null(leiste.Eltern("calc"));
    }

    [Fact]
    public void BeimStartAllesZu()
    {
        var leiste = new Seitenleiste();
        leiste.Seite("dashboard");
        Assert.Empty(leiste.OffeneGruppen);
    }

    [Fact]
    public void KlickAufLernenKlapptAufUndZeigtUebersicht()
    {
        var leiste = new Seitenleiste();
        leiste.Seite("dashboard");
        leiste.Klick("learn");
        leiste.Seite("learn");
        Assert.Equal(["learn"], leiste.OffeneGruppen);
    }

    [Fact]
    public void KlickAufOffeneGruppeKlapptZu()
    {
        var leiste = new Seitenleiste();
        leiste.Seite("dashboard");
        leiste.Klick("learn");
        leiste.Seite("learn");
        leiste.Klick("quiz");
        leiste.Seite("quiz");
        // zurueck auf Lernen per Klick: Lernen war offen und klappt zu, obwohl die Seite wechselt
        leiste.Klick("learn");
        leiste.Seite("learn");
        Assert.Empty(leiste.OffeneGruppen);
        // nochmal: auf derselben Seite wieder auf
        leiste.Klick("learn");
        leiste.Seite("learn");
        Assert.Equal(["learn"], leiste.OffeneGruppen);
    }

    [Fact]
    public void NurEineGruppeOffen()
    {
        var leiste = new Seitenleiste();
        leiste.Seite("dashboard");
        leiste.Klick("learn");
        leiste.Seite("learn");
        leiste.Umschalten("game");
        Assert.Equal(["game"], leiste.OffeneGruppen);
        leiste.Umschalten("learn");
        Assert.Equal(["learn"], leiste.OffeneGruppen);
    }

    [Fact]
    public void KarteikartenKlappenInnerhalbVonLernen()
    {
        var leiste = new Seitenleiste();
        leiste.Seite("dashboard");
        leiste.Klick("learn");
        leiste.Seite("learn");
        leiste.Klick("cards");
        leiste.Seite("cards");
        Assert.Equal(["cards", "learn"], leiste.OffeneGruppen);
        leiste.Klick("cards");
        leiste.Seite("cards");
        Assert.Equal(["learn"], leiste.OffeneGruppen);
        // Lernen zu klappt auch Karteikarten zu; beim naechsten Oeffnen bleibt Karteikarten zu
        leiste.Klick("cards");
        leiste.Seite("cards");
        leiste.Umschalten("game");
        leiste.Umschalten("learn");
        Assert.Equal(["learn"], leiste.OffeneGruppen);
    }

    [Theory]
    [InlineData("dashboard")]
    [InlineData("calc")]
    [InlineData("progress")]
    [InlineData("settings")]
    [InlineData("help")]
    public void SeiteOhneGruppeKlapptAllesZu(string seite)
    {
        var leiste = new Seitenleiste();
        leiste.Seite("quiz");
        leiste.Klick("cards");
        leiste.Seite("cards");
        Assert.Equal(["cards", "learn"], leiste.OffeneGruppen);
        leiste.Klick(seite);
        leiste.Seite(seite);
        Assert.Empty(leiste.OffeneGruppen);
    }

    [Theory]
    [InlineData("notebook", new[] { "learn" })]
    [InlineData("cards", new[] { "learn" })]
    [InlineData(Fachbereiche.Netzwerk, new[] { "cards", "learn" })]
    [InlineData("buero", new[] { "game" })]
    [InlineData("game", new[] { "game" })]
    public void SprungOeffnetGruppe(string seite, string[] offen)
    {
        // Suche, Dashboard-Kachel oder "Weiter lernen" ohne Klick in der Leiste
        var leiste = new Seitenleiste();
        leiste.Seite("dashboard");
        leiste.Umschalten(seite == "game" || seite == "buero" ? "learn" : "game");
        leiste.Seite(seite);
        Assert.Equal(offen, leiste.OffeneGruppen);
    }

    [Fact]
    public void GleicheSeiteAendertNichts()
    {
        // z. B. Fensterbreite wechselt und markiert die Seite neu: eine zugeklappte Gruppe bleibt zu
        var leiste = new Seitenleiste();
        leiste.Seite("quiz");
        leiste.Umschalten("learn");
        leiste.Seite("quiz");
        Assert.Empty(leiste.OffeneGruppen);
    }
}
