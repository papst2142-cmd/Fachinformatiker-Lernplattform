using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.10.0 (Weisung P12-381, Nico 3c mit Hinweis 1a; P12-382 Punkt 4): nach einem Umbau der Wohnung im Werkzeug zaehlt
/// ein gekauftes Moebel, dessen Platz nicht mehr passt, als im Karton; der Spielstand bleibt unveraendert. Der Hinweis hat
/// Nicos Wortlaut (Bild 08 P12-245), mehrere Moebel stehen in einem Hinweis, er kommt einmal (Merker in einstellungen.json).
/// Zurueckverkaufen aus dem Karton bringt den halben Preis wie jedes Moebel (Startmatratze Preis 0 = 0 EUR, kein Verlust).
/// </summary>
public class SpielUmbauKartonTests
{
    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    /// <summary>Inhalte mit einem Regal mitten auf dem Platz der Startmatratze (wie eine im Werkzeug gezogene Wand).</summary>
    private static PyObjekt Umgebaut()
    {
        var inhalt = (PyObjekt)Py.Kopie(Inhalt)!;
        var bleibe = Wohnung.Bleibe("apartment", inhalt)!;
        var g = (PyObjekt)bleibe["gebaeude"]!;
        var raum = ((List<object?>)g["raeume"]!).OfType<PyObjekt>().First(r => Py.Str(r["id"]) == "wohnraum");
        if (SpielDaten.Get(raum, "deko") is not List<object?>)
        {
            Py.Setzen(raum, "deko", new List<object?>());
        }
        ((List<object?>)raum["deko"]!).Add(new PyObjekt { { "typ", "regal" }, { "x", 0.6 }, { "y", 0.6 }, { "w", 1.0 }, { "h", 1.0 } });
        return inhalt;
    }

    [Fact]
    public void Umbau_MatratzeImKarton_HinweisWortlaut_SpielstandBleibt_MitGegenprobe()
    {
        // Gegenprobe ohne Umbau: nichts im Karton, kein Hinweis
        var normal = new Spielstand([], Inhalt);
        Assert.Empty(Wohnung.Umgebaut(normal));
        Assert.Null(Wohnung.UmbauText(normal));
        Assert.Null(Wohnung.UmbauKennung(normal));

        var stand = new Spielstand([], Umgebaut());
        Assert.Equal([("start-matratze", (object?)"matratze")], Wohnung.Umgebaut(stand));
        Assert.Empty(Wohnung.AufgestellteMoebel(stand));
        Assert.Contains(Wohnung.ImKarton(stand), k => Py.Str(k.Stueck) == "start-matratze");
        Assert.Equal("Die Matratze passte nicht mehr an ihren Platz. Sie steht jetzt im Karton: tippe „Aufstellen“ und stell sie neu auf.",
            Wohnung.UmbauText(stand));
        // der Kartonstapel steht im Grundriss
        var grundriss = SpielOrte.WohnungsGrundriss(stand);
        Assert.Contains(((List<object?>)grundriss["raeume"]!).OfType<PyObjekt>().SelectMany(r => ((List<object?>?)SpielDaten.Get(r, "deko")) ?? [])
            .Concat((List<object?>?)SpielDaten.Get((PyObjekt)grundriss["flur"]!, "deko") ?? []).OfType<PyObjekt>(), d => Py.Str(d["typ"]) == "kartons");
        // der Spielstand selbst ist unveraendert (keine Einrichtung geschrieben)
        Assert.Empty(stand.Einrichtungen);

        // Gegenprobe: mit der alten Regel bliebe die Matratze im Regal stehen
        Wohnung.UmbauAltFuerTests.Value = true;
        try
        {
            Assert.Empty(Wohnung.Umgebaut(stand));
            Assert.Single(Wohnung.AufgestellteMoebel(stand));
        }
        finally
        {
            Wohnung.UmbauAltFuerTests.Value = false;
        }
    }

    [Fact]
    public void Umbau_HinweisEinmal_MehrereMoebelEinHinweis()
    {
        var stand = new Spielstand([], Umgebaut());
        var werte = new JsonObject();
        Assert.True(Wohnung.UmbauHinweisOffen(stand, werte));
        Wohnung.UmbauGesehen(werte, Wohnung.UmbauKennung(stand)!);
        Assert.False(Wohnung.UmbauHinweisOffen(stand, werte));
        Assert.Equal(Wohnung.UmbauKennung(stand), (string?)werte[Wohnung.Merker]);

        // zwei Moebel: ein Hinweis mit beiden (Mehrzahl); Satz mit dem Artikel jedes Moebels
        var inhalt = Umgebaut();
        var bleibe = Wohnung.Bleibe("apartment", inhalt)!;
        var moebel = (PyObjekt)((PyObjekt)bleibe["einrichtung"]!)["moebel"]!;
        moebel.Add("k-schreibtisch", new List<object?> { 0.7, 2.2, 0L });
        var zwei = new Spielstand([], inhalt);
        zwei.Moebel["k-schreibtisch"] = "schreibtisch";
        var regal = ((List<object?>)((List<object?>)((PyObjekt)bleibe["gebaeude"]!)["raeume"]!).OfType<PyObjekt>().First(r => Py.Str(r["id"]) == "wohnraum")["deko"]!)
            .OfType<PyObjekt>().Last();
        Py.Setzen(regal, "h", 2.6);
        Assert.Equal(2, Wohnung.Umgebaut(zwei).Count);
        Assert.Equal("Der Schreibtisch mit PC und die Matratze passten nicht mehr an ihren Platz. Sie stehen jetzt im Karton: tippe „Aufstellen“ und stell sie neu auf.",
            Wohnung.UmbauText(zwei));
        // ein neuer Umbau (andere Stuecke) zeigt den Hinweis wieder
        Assert.True(Wohnung.UmbauHinweisOffen(zwei, werte));
    }

    /// <summary>P12-382 Punkt 4: Zurueckverkaufen aus dem Umbau-Karton = halber Preis wie jedes Moebel (Startmatratze 0 EUR).</summary>
    [Fact]
    public void Umbau_Zurueckverkaufen_HalberPreis_KeinVerlust()
    {
        var stand = new Spielstand([], Umgebaut());
        var verkauf = SpielAktionen.MoebelVerkaufen(stand, "start-matratze");
        Assert.Equal(0L, verkauf.Last().Daten["geld"]);
        Assert.Equal(0L, Wohnung.Rueckkauf(Wohnung.MoebelStueck("matratze", stand.Inhalt)!, stand.Inhalt));
        Assert.Equal(240L, Wohnung.Rueckkauf(Wohnung.MoebelStueck("schreibtisch", stand.Inhalt)!, stand.Inhalt));
        // aus dem Karton schreibt der Verkauf keine Einrichtung (das Stueck stand ja nicht mehr), nur den Verkauf
        Assert.Single(verkauf);
    }
}
