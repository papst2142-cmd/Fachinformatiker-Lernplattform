using System.Diagnostics;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle Fehlerpruefung Teil 2 (Weisung P12-345, Sammelliste 430): ein Fehler beim Bau einer Kachel landet einheitlich in
/// <c>fehler.log</c> (hier: <see cref="Nachlader.Protokoll"/>), genau einmal je Kachel, die Kachel bleibt fern gezeichnet
/// (<see cref="Nachlader.Kaputt"/>, kein Loch, kein neuer Versuch), und <see cref="Nachlader.StartBauen"/> laesst ihn nicht
/// durchschlagen. Gegenprobe: bis A 1.3.0 warf <c>StartBauen</c> den Fehler (AggregateException) und der Hintergrundbau
/// verwarf ihn ohne Eintrag und versuchte dieselbe Kachel in jedem Takt neu.
/// </summary>
public sealed class NachladerFehlerTests
{
    private static (Nachlader Lader, List<string> Log) Lader(bool hintergrund)
    {
        var log = new List<string>();
        var lader = new Nachlader(SpielTestkarteTests.Testkarte.Value.Szene, mitGruen: true, hintergrund) { Protokoll = t => { lock (log) { log.Add(t); } } };
        return (lader, log);
    }

    [Fact]
    public void StartBauen_FehlerEinerKachel_SchlaegtNichtDurch_EinEintrag_KachelFern()
    {
        var (lader, log) = Lader(hintergrund: false);
        lader.Takt(40, 40);
        var noetig = lader.Noetig.ToList();
        Assert.True(noetig.Count > 2);
        var kaputt = noetig[1];
        Nachlader.FehlerFuerTests.Value = k => k == kaputt;
        try
        {
            var start = Record.Exception(() => lader.StartBauen(noetig));
            Assert.Null(start);
            Assert.Single(log);
            Assert.Contains("Nachladen: Kachel " + kaputt.Spalte + "," + kaputt.Reihe + " nicht gebaut, wird fern gezeichnet", log[0], StringComparison.Ordinal);
            Assert.Contains("eingeschleuster Fehler", log[0], StringComparison.Ordinal);
            Assert.Contains(kaputt, lader.Kaputt);
            Assert.DoesNotContain(kaputt, lader.Fehlend());
            Assert.Null(lader.JetztBauen(kaputt));
            // alle anderen noetigen Kacheln sind gebaut
            foreach (var k in noetig.Where(k => k != kaputt))
            {
                var p = lader.JetztBauen(k);
                Assert.NotNull(p);
                lader.Hochgeladen(p);
            }
            // weitere Takte und Sofort-Bauten melden dieselbe Kachel nicht noch einmal und bauen sie nicht neu
            for (var i = 0; i < 20; i++)
            {
                lader.Takt(40 + (i * 0.5f), 40);
                Assert.Null(lader.JetztBauen(kaputt));
            }
            Assert.Single(log);
        }
        finally
        {
            Nachlader.FehlerFuerTests.Value = null;
        }
    }

    [Fact]
    public void JetztBauen_Fehler_Null_StattAusnahme_EinEintrag()
    {
        var (lader, log) = Lader(hintergrund: false);
        var k = Ladeplan.Noetig(100, 100, lader.Szene.Breite, lader.Szene.Tiefe)[0];
        Nachlader.FehlerFuerTests.Value = x => x == k;
        try
        {
            lader.Takt(100, 100);
            Assert.Equal(k, lader.Noetig[0]);
            Assert.Null(lader.JetztBauen(k));
            Assert.Null(lader.JetztBauen(k));
            Assert.Single(log);
            Assert.True(lader.Sofort <= 1);
        }
        finally
        {
            Nachlader.FehlerFuerTests.Value = null;
        }
    }

    [Fact]
    public void Hintergrundbau_Fehler_EinEintrag_KeinNeuerVersuch()
    {
        var (lader, log) = Lader(hintergrund: true);
        lader.Takt(120, 120);
        lader.StartBauen(lader.Noetig);
        var gebautVorher = lader.BauKacheln.ToHashSet();
        // die naechste Kachel des Vorrats (Ring um das Noetige) scheitert im Hintergrund
        Nachlader.FehlerFuerTests.Value = k => !gebautVorher.Contains(k);
        try
        {
            var uhr = Stopwatch.StartNew();
            while (lader.Kaputt.Count == 0 && uhr.Elapsed < TimeSpan.FromSeconds(60))
            {
                lader.Takt(120, 120);
                Thread.Sleep(5);
            }
            Assert.NotEmpty(lader.Kaputt);
            // weitere Takte: jede gescheiterte Kachel steht genau einmal im Protokoll, keine wird noch einmal versucht
            uhr.Restart();
            while (uhr.Elapsed < TimeSpan.FromSeconds(2))
            {
                lader.Takt(120, 120);
                Thread.Sleep(5);
            }
            lock (log)
            {
                Assert.Equal(lader.Kaputt.Count, log.Count);
                Assert.Equal(log.Count, log.Distinct(StringComparer.Ordinal).Count());
            }
            Assert.All(lader.Kaputt, k => Assert.False(lader.Bereit.ContainsKey(k)));
        }
        finally
        {
            Nachlader.FehlerFuerTests.Value = null;
        }
    }
}
