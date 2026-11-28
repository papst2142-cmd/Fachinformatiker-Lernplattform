using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Patch A 1.20.1 (Weisung P12-449 Punkt 2): nach einem Halt beim freien Gehen kommt nur ein Weg in den Verlauf; die Zahl der
/// offenen Auftraege am Handy-Knopf wird dann nicht neu gerechnet (<see cref="Questlog.NurWegeSeit"/>). Belegt an jedem Stand eines
/// ganz gespielten Spiels: ein Weg aendert die offenen Auftraege nie; Gegenprobe: andere Zeilen aendern sie und gelten nie als Weg.
/// </summary>
public sealed class SpielOffenWegeTests
{
    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static List<object?> Offen(Spielstand stand) => [.. stand.OffeneTickets().Select(a => a["id"])];

    private static SpielEreignis Weg(string art, Spielstand stand) =>
        new("2099-01-01 00:00:00", art, new PyObjekt { { "tag", stand.Tag }, { "ort", "bitweiche" }, { "schritte", 3L }, { "minuten", 1L } });

    [Fact]
    public void Weg_AendertDieOffenenAuftraegeNie_GegenprobeAndereZeilen()
    {
        var ereignisse = SpielFingerabdruckTests.Gespielt.Value.Speicher.Ereignisse();
        Assert.True(ereignisse.Count >= 100, ereignisse.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var geprueft = 0;
        var geaendert = 0;
        var erkannt = 0;
        // schrittweise wie im Spiel (Spielablauf.Anwenden: Kopie, Zeile anwenden)
        var stand = new Spielstand(ereignisse.Take(1), Inhalt);
        var einmal = new PySet();
        for (var k = 1; k < ereignisse.Count; k++)
        {
            var vorher = Offen(stand);
            foreach (var art in new[] { Stadt.Gelaufen, Stadt.Gegangen })
            {
                // wie im Spiel: Kopie, Zeile anwenden (Spielablauf.Anwenden), dann ist nur ein Weg dazugekommen
                var kopie = stand.Kopie();
                var tagVorher = kopie.Tag;
                var weg = Weg(art, kopie);
                if (kopie.Anwenden(weg, new PySet(einmal)))
                {
                    kopie.BearbeitetNotieren(weg.Art, weg.Daten, tagVorher);
                }
                // haengt die erste oder letzte Zeile auch an einem Feld (z. B. Einrichtung), ist sie in der Kopie neu: dann wird
                // vorsichtshalber neu gerechnet (nein); die offenen Auftraege bleiben in jedem Fall gleich
                if (Questlog.NurWegeSeit(kopie.Verlauf, stand.Verlauf.Count, stand.Verlauf[0], stand.Verlauf[^1]))
                {
                    erkannt++;
                }
                Assert.Equal(stand.Tag, kopie.Tag);
                Assert.Equal(vorher, Offen(kopie));
                geprueft++;
            }
            // Gegenprobe: die naechste echte Zeile (kein Weg) gilt nie als „nur Wege“; manche aendern die offenen Auftraege
            var naechste = ereignisse[k];
            var danach = stand.Kopie();
            var tag = danach.Tag;
            if (danach.Anwenden(naechste, einmal))
            {
                danach.BearbeitetNotieren(naechste.Art, naechste.Daten, tag);
            }
            if (naechste.Art is not (Stadt.Gelaufen or Stadt.Gegangen))
            {
                Assert.False(Questlog.NurWegeSeit(danach.Verlauf, stand.Verlauf.Count, stand.Verlauf[0], stand.Verlauf[^1]));
                if (!Offen(danach).SequenceEqual(vorher))
                {
                    geaendert++;
                }
            }
            stand = danach;
        }
        // schrittweise gleich dem Neuaufbau (sonst pruefte der Test einen anderen Stand als das Spiel)
        Assert.Empty(stand.Unterschiede(new Spielstand(ereignisse, Inhalt)));
        Assert.True(geprueft >= 200, $"{geprueft}");
        Assert.True(erkannt * 2 >= geprueft, $"nur {erkannt} von {geprueft} Wegen als „nur Wege“ erkannt");
        Assert.True(geaendert >= 5, $"Gegenprobe: nur {geaendert} Zeilen aendern die offenen Auftraege");
    }

    [Fact]
    public void NurWegeSeit_NeuaufbauOderAndereZeilen_Nein()
    {
        var stand = new Spielstand(SpielFingerabdruckTests.Gespielt.Value.Speicher.Ereignisse().Take(30), Inhalt);
        var (erste, letzte, zahl) = (stand.Verlauf[0], stand.Verlauf[^1], stand.Verlauf.Count);
        Assert.True(Questlog.NurWegeSeit(stand.Verlauf, zahl, erste, letzte));
        // ein Neuaufbau hat neue Zeilen-Objekte: nie „nur Wege“
        var neu = new Spielstand(SpielFingerabdruckTests.Gespielt.Value.Speicher.Ereignisse().Take(30), Inhalt);
        Assert.False(Questlog.NurWegeSeit(neu.Verlauf, zahl, erste, letzte));
        Assert.False(Questlog.NurWegeSeit(stand.Verlauf, 0, null, null));
        Assert.False(Questlog.NurWegeSeit(stand.Verlauf, zahl + 1, erste, letzte));
        stand.Verlauf.Add(Weg(Stadt.Gelaufen, stand));
        Assert.True(Questlog.NurWegeSeit(stand.Verlauf, zahl, erste, letzte));
        stand.Verlauf.Add(Weg(Stadt.Gewartet, stand));
        Assert.False(Questlog.NurWegeSeit(stand.Verlauf, zahl, erste, letzte));
    }
}
