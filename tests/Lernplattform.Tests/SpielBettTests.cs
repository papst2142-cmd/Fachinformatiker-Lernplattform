using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W5 (Plan Phase 8 Abschnitt 11, Sammelliste 111): Feierabend ueber das Bett. Die Rueckfrage kommt
/// nur, wenn der Feierabend moeglich ist (dann derselbe Feierabend wie der Knopf); offene Aufgaben zaehlen wie
/// FeierabendMoeglich (ohne Firma offene Tickets).
/// </summary>
public class SpielBettTests
{
    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static Spielablauf NeuesSpiel()
    {
        var zaehler = 0;
        var spiel = new Spielablauf(Inhalt, new SpielSpeicher(() =>
        {
            zaehler += 7;
            return new DateTime(2026, 10, 3).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }));
        spiel.Profil("Nico", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" } });
        return spiel;
    }

    [Fact]
    public void RueckfrageNurWennFeierabendMoeglich()
    {
        var spiel = NeuesSpiel();
        var offen = spiel.Stand.OffeneTickets().Count;
        Assert.True(offen > 1);
        var (moeglich, text) = Wohnung.SchlafFrage(spiel.Stand);
        Assert.False(moeglich);
        Assert.Equal($"Für heute sind noch {offen} Aufgaben offen. Erledige sie zuerst, dann kannst du schlafen gehen und Feierabend machen.", text);
        Assert.False(spiel.Stand.FeierabendMoeglich());

        while (spiel.Stand.OffeneTickets().Count > 1)
        {
            spiel.Verschieben(spiel.Stand.OffeneTickets()[0]["id"]);
        }
        Assert.Equal((false, "Für heute ist noch 1 Aufgabe offen. Erledige sie zuerst, dann kannst du schlafen gehen und Feierabend machen."),
            Wohnung.SchlafFrage(spiel.Stand));

        spiel.Verschieben(spiel.Stand.OffeneTickets()[0]["id"]);
        Assert.Equal((true, "Du hast alle Aufgaben für heute erledigt. Willst du schlafen gehen und Feierabend machen?"), Wohnung.SchlafFrage(spiel.Stand));
        Assert.True(spiel.Stand.FeierabendMoeglich());

        // Schlafen gehen ist derselbe Feierabend (dasselbe Ereignis tag_beendet, neuer Tag)
        var tag = spiel.Stand.Tag;
        spiel.Feierabend();
        Assert.Equal(Ereignisse.Feierabend, spiel.Stand.Verlauf[^1].Art);
        Assert.Equal(tag + 1, spiel.Stand.Tag);
    }

    [Fact]
    public void BettHatDenKnopfSchlafen()
    {
        // Die Wohnung zeigt am Bett weiter den Knopf "Schlafen" (Python home_message), die Rueckfrage kommt danach
        foreach (var id in new[] { "matratze", "bett_einzel", "bett_doppel" })
        {
            Assert.Equal("schlafen", Wohnung.MoebelStueck(id, Inhalt)!["aktion"]);
        }
        Assert.Equal(("Feierabend", "Schlafen gehen", "Noch nicht"), (Wohnung.SchlafenTitel, Wohnung.SchlafenJa, Wohnung.SchlafenNein));
    }
}
