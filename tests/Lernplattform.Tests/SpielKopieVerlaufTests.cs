using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Patch A 1.20.1 (Sammelliste 473, Weisung P12-449 Punkt 2): die Kopie des Spielstands vor jeder Aktion teilt die Zeilen des
/// Verlaufs, statt sie tief zu kopieren. Ergebnis Feld fuer Feld gleich der tiefen Kopie; ein Halt beim freien Gehen legt mit
/// langem Spielstand nur noch wenig Speicher an (Gegenprobe mit der tiefen Kopie: darueber). Die Ereignisse bleiben gleich.
/// </summary>
public sealed class SpielKopieVerlaufTests
{
    /// <summary>Grenze: Speicher je Halt beim freien Gehen mit langem Spielstand (bis A 1.20.0 am PC rund 540 KB).</summary>
    public const double HaltKbGrenze = 200;

    /// <summary>Grenze: Zuwachs des Speichers je Halt je 100 weitere Zeilen im Verlauf.</summary>
    public const double WachsenKbGrenze = 5;

    private static StadtSpiel LangesSpiel(int wege)
    {
        var spiel = SpielBusfahrtTests.Spiel();
        var zufall = new Random(473);
        for (var w = 0; w < wege; w++)
        {
            spiel.GeheZu(spiel.Szene.Orte[zufall.Next(spiel.Szene.Orte.Count)].Id);
            spiel.SofortAnkommen();
        }
        return spiel;
    }

    private static Spielstand Tief(Spielstand stand)
    {
        Spielstand.KopieVerlaufAltFuerTests = true;
        try
        {
            return stand.Kopie();
        }
        finally
        {
            Spielstand.KopieVerlaufAltFuerTests = false;
        }
    }

    [Fact]
    public void Kopie_GleichDerTiefenKopie_UndTeiltDieZeilen()
    {
        var spiel = LangesSpiel(60);
        var stand = spiel.Ablauf.Stand;
        Assert.True(stand.Verlauf.Count >= 60);
        var neu = stand.Kopie();
        var tief = Tief(stand);
        Assert.Empty(neu.Unterschiede(tief));
        Assert.Empty(neu.Unterschiede(stand));
        Assert.NotSame(stand.Verlauf, neu.Verlauf);
        Assert.Equal(stand.Verlauf.Count, neu.Verlauf.Count);
        // die Zeilen der Wege haengen an keinem anderen Feld: geteilt; die tiefe Kopie hat eigene
        Assert.Contains(neu.Verlauf, e => e.Art == Stadt.Gegangen && stand.Verlauf.Any(a => ReferenceEquals(a, e)));
        Assert.DoesNotContain(tief.Verlauf, e => stand.Verlauf.Any(a => ReferenceEquals(a, e)));
        // eine Aenderung an der Kopie (neue Zeile) laesst das Original unveraendert
        var zahl = stand.Verlauf.Count;
        neu.Verlauf.Add(new SpielEreignis("2026-10-02 08:00:00", Stadt.Gewartet, []));
        Assert.Equal(zahl, stand.Verlauf.Count);
    }

    [Fact]
    public void Kopie_ZeileAnEinemFeld_WirdTiefKopiert()
    {
        // ein Teil einer Zeile, der auch an einem anderen Feld haengt (wie Einrichtungen[w] = daten): die Kopie haelt beide als
        // dasselbe neue Objekt (wie deepcopy mit memo), das Original bleibt unberuehrt
        var spiel = LangesSpiel(5);
        var stand = spiel.Ablauf.Stand;
        var daten = new PyObjekt { { "wohnung", "w1" }, { "stuecke", new List<object?> { "a" } } };
        stand.Verlauf.Add(new SpielEreignis("2026-10-02 08:00:00", "moebel_probe", daten));
        stand.Einrichtungen["w1"] = daten;
        var neu = stand.Kopie();
        var zeile = neu.Verlauf[^1];
        Assert.NotSame(daten, zeile.Daten);
        Assert.Same(zeile.Daten, neu.Einrichtungen["w1"]);
        Assert.Empty(neu.Unterschiede(Tief(stand)));
    }

    /// <summary>Speicher eines Halts beim freien Gehen (KB), zweiter Halt (der erste zahlt das Einrichten).</summary>
    private static double HaltKb(StadtSpiel spiel)
    {
        double Einmal()
        {
            spiel.StelleFuerTests(spiel.Szene.Orte[0].StehX, spiel.Szene.Orte[0].StehZ);
            var gegangen = 0.0;
            foreach (var (rx, rz) in new[] { (1.0, 0.0), (-1.0, 0.0), (0.0, 1.0), (0.0, -1.0) })
            {
                for (var i = 0; i < 45 && gegangen < 1.5; i++)
                {
                    var (x0, z0) = (spiel.FigurX, spiel.FigurZ);
                    spiel.FreiGehen(rx, rz, 1.0 / 30);
                    gegangen += Math.Abs(spiel.FigurX - x0) + Math.Abs(spiel.FigurZ - z0);
                }
            }
            Assert.True(spiel.FreiOffen);
            var zahl = spiel.Ablauf.Stand.Verlauf.Count;
            var b0 = GC.GetAllocatedBytesForCurrentThread();
            spiel.FreiHalt();
            var kb = (GC.GetAllocatedBytesForCurrentThread() - b0) / 1024.0;
            Assert.Equal(zahl + 1, spiel.Ablauf.Stand.Verlauf.Count);
            return kb;
        }
        Einmal();
        return Einmal();
    }

    private static double HaltKbTief(StadtSpiel spiel)
    {
        Spielstand.KopieVerlaufAltFuerTests = true;
        try
        {
            return HaltKb(spiel);
        }
        finally
        {
            Spielstand.KopieVerlaufAltFuerTests = false;
        }
    }

    [Fact]
    public void Halt_SpeicherWaechstNichtMitDemVerlauf_GegenprobeTief()
    {
        // Nicos Ziel: ein Halt legt nicht mehr Megabytes an, auch mit sehr langem Spielstand. Gemessen mit 100 und 500 Wegen (mindestens 300 Zeilen mehr):
        // jetzt fast gleich (unter der Grenze), mit der tiefen Kopie (bis A 1.20.0) waechst er mit jeder Zeile des Verlaufs
        var kurz = LangesSpiel(100);
        var lang = LangesSpiel(500);
        Assert.True(lang.Ablauf.Stand.Verlauf.Count >= kurz.Ablauf.Stand.Verlauf.Count + 300, $"{kurz.Ablauf.Stand.Verlauf.Count} / {lang.Ablauf.Stand.Verlauf.Count}");
        var (k, l) = (HaltKb(kurz), HaltKb(lang));
        var (kt, lt) = (HaltKbTief(kurz), HaltKbTief(lang));
        var hundert = (lang.Ablauf.Stand.Verlauf.Count - kurz.Ablauf.Stand.Verlauf.Count) / 100.0;
        var je100 = (l - k) / hundert;
        var je100Tief = (lt - kt) / hundert;
        Assert.True(l <= HaltKbGrenze, $"Halt {l:F0} KB bei {lang.Ablauf.Stand.Verlauf.Count} Zeilen (Grenze {HaltKbGrenze} KB)");
        Assert.True(je100 <= WachsenKbGrenze, $"Halt waechst um {je100:F1} KB je 100 Zeilen (Grenze {WachsenKbGrenze})");
        Assert.True(je100Tief > WachsenKbGrenze * 5, $"Gegenprobe tief: {je100Tief:F1} KB je 100 Zeilen");
        Assert.True(lt > HaltKbGrenze * 2, $"Gegenprobe tief: {lt:F0} KB");
    }
}
