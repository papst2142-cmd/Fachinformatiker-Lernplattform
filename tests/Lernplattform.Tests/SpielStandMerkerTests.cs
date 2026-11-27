using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Patch A 1.19.1 (Sammelliste 473, Ruckeln Galaxy Tab A8): der Stand der Stadt wird nur neu durch den Verlauf gerechnet, wenn
/// sich am Verlauf etwas geaendert hat. Ergebnis immer gleich der Rechnung ohne Merker; der Indexer von PyObjekt bleibt gleich.
/// </summary>
public sealed class SpielStandMerkerTests
{
    private static StadtStand OhneMerker(Stadt stadt, Spielstand stand)
    {
        Stadt.StandOhneMerkerFuerTests = true;
        try
        {
            return stadt.Stand(stand);
        }
        finally
        {
            Stadt.StandOhneMerkerFuerTests = false;
        }
    }

    [Fact]
    public void Stand_MitMerker_GleichOhne_UndNeuNurNachAenderung()
    {
        var spiel = SpielBusfahrtTests.Spiel();
        var orte = spiel.Szene.Orte.Select(o => o.Id).ToList();
        for (var i = 0; i < 12; i++)
        {
            var stand = spiel.Ablauf.Stand;
            var erst = spiel.Stadt.Stand(stand);
            var gerechnet = spiel.Stadt.StandGerechnet;
            // zweite Abfrage ohne Aenderung: aus dem Merker, gleich der Rechnung ohne Merker
            Assert.Equal(erst, spiel.Stadt.Stand(stand));
            Assert.Equal(gerechnet, spiel.Stadt.StandGerechnet);
            Assert.Equal(erst, OhneMerker(spiel.Stadt, stand));
            var zahl = stand.Verlauf.Count;
            if (spiel.GeheZu(orte[i % orte.Count]) == StadtGrund.Ok)
            {
                spiel.SofortAnkommen();
            }
            if (spiel.Ablauf.Stand.Verlauf.Count != zahl)
            {
                // neue Zeile: neu gerechnet, gleiches Ergebnis wie ohne Merker
                var neu = spiel.Stadt.Stand(spiel.Ablauf.Stand);
                Assert.True(spiel.Stadt.StandGerechnet > 0);
                Assert.Equal(OhneMerker(spiel.Stadt, spiel.Ablauf.Stand), neu);
            }
        }
        Assert.True(spiel.Ablauf.Stand.Verlauf.Count >= 5);
        // Neuaufbau aus dem Protokoll: neuer Stand, wieder gleich
        var vorher = spiel.Stadt.Stand(spiel.Ablauf.Stand);
        spiel.Ablauf.Neuaufbau();
        Assert.Equal(vorher, spiel.Stadt.Stand(spiel.Ablauf.Stand));
        Assert.Equal(OhneMerker(spiel.Stadt, spiel.Ablauf.Stand), spiel.Stadt.Stand(spiel.Ablauf.Stand));
    }

    [Fact]
    public void PyObjekt_Indexer_ErsterTreffer_FehltNull()
    {
        var o = new PyObjekt { { "a", 1L }, { "b", null }, { "c", "x" } };
        Assert.Equal(1L, o["a"]);
        Assert.Null(o["b"]);
        Assert.Equal("x", o["c"]);
        Assert.Null(o["fehlt"]);
        Assert.Null(new PyObjekt()["a"]);
    }
}
