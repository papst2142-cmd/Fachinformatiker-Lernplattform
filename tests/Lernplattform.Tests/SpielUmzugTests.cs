using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 9 W5 (Weisung P9-11 Teil B, Sammelliste 179): Umziehen beim Einrichten der Wohnung wie Python 0.63.2
/// (Karte "Wohnung" in fisi_game_gui.py _build_below und mobile/src/spiel.py _home_editor, Ablauf _move).
/// Die Oberflaeche liest den Schalter aus Optionen &gt; Spiel einmal, fragt mit move_texts und zieht ueber
/// <see cref="StadtSpiel.Umziehen"/> (Spielablauf.Umziehen, Ereignis umzug) um. Die Sollwerte der Texte sind
/// mit fisi_game.py (v0.63.2) erzeugt.
/// </summary>
public class SpielUmzugTests
{
    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    /// <summary>Spielstand mit mindestens <paramref name="geld"/> Euro, die Figur steht in der Wohnung beim Einrichten.</summary>
    private static StadtSpiel ZuhauseBeimEinrichten(long geld)
    {
        var ablauf = new Spielablauf(Inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        for (var tag = 0; tag < 120 && ablauf.Stand.Geld < geld; tag++)
        {
            ablauf.SchlafenGehen();     // offene Tickets verschieben, dann Feierabend mit Gehalt
        }
        Assert.True(ablauf.Stand.Geld >= geld, "Geld " + ablauf.Stand.Geld);
        var spiel = new StadtSpiel(new Stadt(Inhalt, SpielStadtTests.Daten.Value), ablauf, null, SpielInnenraumTests.Innenraeume.Value);
        Assert.True(spiel.Betreten());
        Assert.Equal(SpielDaten.OrtZuhause, spiel.Ort);
        spiel.EinrichtenUmschalten();
        Assert.True(spiel.Einrichten);
        return spiel;
    }

    [Fact]
    public void TexteWiePython()
    {
        // fisi_game.py: Karte "Wohnung" (Titel, "Du wohnst in: %s", Knopf "Umziehen", letzte Wohnung)
        Assert.Equal("Wohnung", Wohnung.WohnungTitel);
        Assert.Equal("Umziehen", Wohnung.UmziehenKnopf);
        Assert.Equal("Du wohnst schon in der größten Wohnung.", Wohnung.GroessteWohnung);
        var stand = new Spielablauf(Inhalt).Stand;
        Assert.Equal("Du wohnst in: Einzimmer-Apartment", Wohnung.WohntIn(Wohnung.Bleibe(stand.Wohnung, Inhalt)!));
        Assert.Equal(["zweizimmer", "altbau", "loft"], Wohnung.Umzuege(stand).Select(w => Py.Str(w["id"])));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UmzugBeimEinrichten_MitKaufUndMitMiete(bool miete)
    {
        var spiel = ZuhauseBeimEinrichten(2500);
        var stand = spiel.Ablauf.Stand;
        var ziel = Wohnung.Bleibe("zweizimmer", Inhalt)!;
        // move_texts (Preiszeile, Rueckfrage) wortgleich mit fisi_game.py
        var (preis, frage) = Wohnung.UmzugTexte(stand, ziel, miete);
        Assert.Equal(miete ? "Kaution 375 € · Miete 60 €/Tag" : "2.500 €", preis);
        Assert.Equal(miete
            ? "In die Wohnung „Zweizimmer-Wohnung“ ziehen? Kaution 375 €, danach 60 € Miete pro Arbeitstag. Alle Möbel kommen dabei in Umzugskartons."
            : "Für 2.500 € in die Wohnung „Zweizimmer-Wohnung“ umziehen? Alle Möbel kommen dabei in Umzugskartons.", frage);
        Assert.Equal(0L, Py.Int(Wohnung.UmzugAngebot(stand, ziel, miete)["fehlt"]));

        var geld = stand.Geld;
        var ereignisse = stand.Verlauf.Count;
        var startX = spiel.Innen!.StartX;
        Assert.True(spiel.Umziehen("zweizimmer", miete));
        stand = spiel.Ablauf.Stand;
        var umzug = stand.Verlauf.Skip(ereignisse).First(e => e.Art == Ereignisse.Umzug);
        Assert.Equal("zweizimmer", Py.Str(umzug.Daten["wohnung"]));
        Assert.Equal(miete, Py.Hat(umzug.Daten, "miete"));
        Assert.Equal("zweizimmer", Py.Str(stand.Wohnung));
        Assert.Equal(miete ? 60 : 0, stand.Miete);
        Assert.Equal(miete ? 375 : 0, stand.Kaution);
        Assert.Equal(geld - (miete ? 375 : 2500), stand.Geld);
        Assert.Equal(miete ? "Zur Miete: 60 € pro Arbeitstag, Kaution 375 € hinterlegt" : "", Wohnung.MietText(stand));
        // alle Moebel im Karton, nichts gewaehlt, Figur an der Tuer der neuen Wohnung, Einrichten bleibt an
        Assert.Contains(spiel.ImKarton(), k => k.Stueck == "start-matratze");
        Assert.Empty(Wohnung.AufgestellteMoebel(stand));
        Assert.Null(spiel.Ausgewaehlt);
        Assert.True(spiel.Einrichten);
        Assert.Equal(spiel.Innen!.StartX, spiel.FigurX);
        Assert.Equal(spiel.Innen.StartZ, spiel.FigurZ);
        Assert.Equal("Zweizimmer-Wohnung", spiel.Zeile);
        Assert.NotEqual(0, startX);
        // beim naechsten Umzug kommt die Kaution zurueck (Satz in der Rueckfrage wie Python)
        var altbau = Wohnung.Bleibe("altbau", Inhalt)!;
        Assert.Equal(miete
            ? "In die Wohnung „Dreizimmer-Altbau“ ziehen? Kaution 900 €, danach 140 € Miete pro Arbeitstag. Die Kaution von 375 € für deine jetzige Wohnung bekommst du zurück. Alle Möbel kommen dabei in Umzugskartons."
            : "Für 6.000 € in die Wohnung „Dreizimmer-Altbau“ umziehen? Alle Möbel kommen dabei in Umzugskartons.",
            Wohnung.UmzugTexte(stand, altbau, miete).Frage);
        Assert.Equal(["altbau", "loft"], Wohnung.Umzuege(stand).Select(w => Py.Str(w["id"])));
        // draussen zeigt die Stadt die neue Wohnung (Szene im Hintergrund gebaut, beim Hinausgehen genommen)
        spiel.EinrichtenUmschalten();
        spiel.Verlassen();
        Assert.False(spiel.ImInnenraum);
        Assert.Equal(StadtSzene.SignaturFuer(spiel.Stadt, stand, spiel.Szene.Fest), spiel.Szene.Signatur);
    }

    [Fact]
    public void ZuWenigGeld_NurHinweisInDerZeile()
    {
        var spiel = ZuhauseBeimEinrichten(0);
        var ereignisse = spiel.Ablauf.Stand.Verlauf.Count;
        var loft = Wohnung.Bleibe("loft", Inhalt)!;
        Assert.True(Py.Int(Wohnung.UmzugAngebot(spiel.Ablauf.Stand, loft, false)["fehlt"]) > 0);
        Assert.False(spiel.Umziehen("loft", false));
        Assert.StartsWith("Dafür reicht dein Geld noch nicht (", spiel.Zeile);
        Assert.Equal(ereignisse, spiel.Ablauf.Stand.Verlauf.Count);
        Assert.Equal("apartment", Py.Str(spiel.Ablauf.Stand.Wohnung));
        // ausserhalb von "Einrichten" zieht niemand um
        spiel.EinrichtenUmschalten();
        Assert.False(spiel.Umziehen("zweizimmer", true));
        Assert.Equal(ereignisse, spiel.Ablauf.Stand.Verlauf.Count);
    }
}
