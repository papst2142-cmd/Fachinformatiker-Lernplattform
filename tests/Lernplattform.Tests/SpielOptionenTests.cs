using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 9 W4 (Weisung P9-10 Teil B, Sammelliste 178): Karte "Spiel" der Optionen wie Python 0.63.2.
/// Texte wortgleich mit fisi_game.py (RENT_CHOICES, RENT_HELP, difficulty_options_text, slot_options_text),
/// die Zeile zeigt Platz und Schwierigkeitsgrad des aktiven Spielstands, der Schalter "Miete" liegt unter
/// spiel_miete in einstellungen.json und wirkt auf den naechsten Umzug (Ereignis umzug mit bzw. ohne miete).
/// </summary>
public class SpielOptionenTests
{
    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static PyObjekt Aussehen() => new()
    {
        { "haut", "hell" }, { "haare", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "hoodie" }, { "farbe", "blau" },
        { "hose", "jeans" }, { "brille", false }, { "bart", false }, { "extra", "headset" }, { "kreis", "gelb" },
    };

    [Fact]
    public void TexteWiePython()
    {
        // fisi_game.py Z. 13887-13899 (RENT_SETTING, RENT_CHOICES, HOME_SUBTITLE, RENT_HELP)
        Assert.Equal("spiel_miete", Wohnung.MieteSchluessel);
        Assert.Equal([("einmal", "Einmalzahlung"), ("miete", "Miete")], Wohnung.MieteWahl);
        Assert.Equal("Größer wohnen kostet einmalig", Wohnung.WohnungUntertitel(false));
        Assert.Equal("Größer wohnen: Kaution und Miete je Arbeitstag", Wohnung.WohnungUntertitel(true));
        Assert.Equal("Gilt für den nächsten Umzug im Spiel. Einmalzahlung: Die Wohnung wird gekauft. Miete: nur eine "
            + "Kaution (15 % des Kaufpreises), dafür geht jeden Arbeitstag die Miete vom Spielgeld ab. Ein laufender "
            + "Mietvertrag gilt bis zum nächsten Umzug. Die Einstellung gilt nur für dieses Gerät.",
            Wohnung.MieteHilfe(SpielDaten.Objekt(Inhalt["balancing"])));
        // app_gui.py "WOHNUNGEN", mobile ui.label("Wohnungen") zeigt Grossbuchstaben
        Assert.Equal("WOHNUNGEN", Core.Texte.Beschriftungen.Gross(Wohnung.MieteTitel));
    }

    [Fact]
    public void ZeileZeigtPlatzUndStufeDesAktivenSpielstands()
    {
        using var ordner = new TempOrdner();
        var schreiber = new LernstandSchreiber(new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName)));
        var spiel = new Spielverwaltung(schreiber, "PC", Inhalt, sperre: Vorgaenge.Sperren);
        // ohne Spielstand wie Python: kein Platz, Stufe wird beim Spielstart gewaehlt
        Assert.Equal("Schwierigkeitsgrad: wird beim Spielstart gewählt", spiel.OptionenZeile());
        spiel.NeuerLauf(2);
        Assert.Equal("Platz 2 · Schwierigkeitsgrad: wird beim Spielstart gewählt", spiel.OptionenZeile());
        spiel.ZumSchreiben().Profil("Nico", Aussehen(), Ereignisse.Einfach);
        Assert.Equal("Platz 2 · Schwierigkeitsgrad: Einfach · ändern nur durch „Spielstand zurücksetzen“", spiel.OptionenZeile());
        Assert.True(spiel.PlatzUmbenennen(2, "Büro"));
        Assert.Equal("Platz 2 · Büro · Schwierigkeitsgrad: Einfach · ändern nur durch „Spielstand zurücksetzen“", spiel.OptionenZeile());
        // ein zweites Geraet (frische Verwaltung wie die Optionen) liest denselben aktiven Spielstand aus der Datenbank
        spiel.NeuerLauf(1);
        spiel.ZumSchreiben().Profil("Anna", Aussehen(), Ereignisse.Normal);
        var optionen = new Spielverwaltung(schreiber, "PC", Inhalt, sperre: Vorgaenge.Sperren);
        Assert.Equal("Platz 1 · Schwierigkeitsgrad: Normal · ändern nur durch „Spielstand zurücksetzen“", optionen.OptionenZeile());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SchalterWirktAufDenNaechstenUmzug(bool miete)
    {
        using var ordner = new TempOrdner();
        var einstellungen = new Einstellungen(ordner.Pfad);
        Assert.False(Wohnung.MieteGewaehlt(einstellungen.Laden()));   // Vorgabe Einmalzahlung
        Assert.True(einstellungen.Aendern(w => Wohnung.MieteSetzen(w, miete)));
        var gespeichert = JsonNode.Parse(File.ReadAllText(einstellungen.Pfad))!.AsObject();
        Assert.Equal(miete, gespeichert["spiel_miete"]!.GetValue<bool>());   // set_rent_mode: bool(flag)

        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var spiel = new Spielverwaltung(new LernstandSchreiber(lernstand), "PC", Inhalt, sperre: Vorgaenge.Sperren);
        spiel.NeuerLauf(1);
        var ablauf = spiel.ZumSchreiben();
        ablauf.MieteSchalter = () => Wohnung.MieteGewaehlt(einstellungen.Laden());
        ablauf.Profil("Nico", Aussehen(), Ereignisse.Normal);
        for (var tag = 0; tag < 80 && ablauf.Stand.Geld < 2500; tag++)
        {
            ablauf.SchlafenGehen();     // offene Tickets verschieben, dann Feierabend mit Gehalt
        }
        Assert.True(ablauf.Stand.Geld >= 2500, "Geld " + ablauf.Stand.Geld);
        var geldVorher = ablauf.Stand.Geld;
        ablauf.Umziehen("zweizimmer");     // ohne Angabe: der Schalter der Optionen (move_home(rent=None))
        // die Umzug-Zeile (beim Kauf folgt ihr noch das Abzeichen fuer die eigene Wohnung)
        var zeile = lernstand.Abfragen("SELECT typ, daten FROM spiel_ereignisse WHERE typ = 'umzug'").Single();
        var daten = JsonNode.Parse((string)zeile[1]!)!.AsObject();
        Assert.Equal("zweizimmer", daten["wohnung"]!.GetValue<string>());
        Assert.Equal(miete, daten.ContainsKey("miete"));
        Assert.Equal(miete, daten.ContainsKey("kaution"));
        Assert.Equal(miete ? 60 : 0, ablauf.Stand.Miete);
        Assert.Equal(miete ? 375 : 0, ablauf.Stand.Kaution);
        Assert.Equal(geldVorher - (miete ? 375 : 2500), ablauf.Stand.Geld);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("\"\"", false)]
    [InlineData("\"ja\"", true)]
    [InlineData("null", false)]
    [InlineData("[]", false)]
    [InlineData("{}", false)]
    public void SchalterLiestWiePythonBool(string json, bool soll)
    {
        var werte = JsonNode.Parse("{\"spiel_miete\": " + json + "}")!.AsObject();
        Assert.Equal(soll, Wohnung.MieteGewaehlt(werte));
        Assert.False(Wohnung.MieteGewaehlt([]));
    }
}
