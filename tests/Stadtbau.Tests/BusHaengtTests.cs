using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;

namespace Stadtbau.Tests;

/// <summary>
/// Patch A 0.72.1 / S 0.49.1 (Sammelliste 329, Weisung P12-233): in Nicos Stadt 1 (Fassung 1) blieb der Bus an Haltestelle
/// Nr. 3 fuer immer stehen: nach dem Halt lag die Front durch Rundung 0,00003 vor dem Schild, und dieselbe Haltestelle wurde
/// endlos neu erkannt. Jetzt zaehlt die eben bediente Haltestelle erst nach dem Weiterfahren. Die Pruefung „Bus steckt“ faehrt
/// mehrere Tage je eine ganze Runde und 20 Sekunden (bis A 0.72.0: 90 Sekunden an einem Tag, sie sah den Fehler nicht).
/// Gegenproben: alte Halte-Regel (der Bus steht), alte Pruefung (kein Fund).
/// </summary>
public class BusHaengtTests
{
    private static StadtFassung Stadt1()
    {
        byte[]? Lesen(string pfad)
        {
            var d = Path.Combine(Daten.Wurzel, pfad);
            return File.Exists(d) ? File.ReadAllBytes(d) : null;
        }
        return AktiveFassung.LadenNummer(Lesen, 1);
    }

    private static readonly Lazy<StadtSzene> Szene = new(() => Ansicht3D.Bauen(Stadt1(), Daten.Spiel, Ansicht3D.Einstellung.Standard).Szene);

    /// <summary>Laengste Standzeit jedes Busses an den Tagen 1 bis 3 (eine ganze Runde und 40 Sekunden, Figur weit weg).</summary>
    private static double LaengsterHalt()
    {
        var max = 0.0;
        foreach (var tag in new long[] { 1, 2, 3 })
        {
            var v = Verkehr.FuerSpieltag(Szene.Value, tag);
            var busse = v.Autos.Where(a => a.Nummer >= Buslinie.ErsteNummer).ToList();
            Assert.NotEmpty(busse);
            var steht = new double[busse.Count];
            const double s = 1.0 / 20;
            var runde = StadtbauPruefung.BusRunde(v);
            for (var t = 0; t < runde / s; t++)
            {
                v.Weiter(s, -1000, -1000);
                for (var i = 0; i < busse.Count; i++)
                {
                    steht[i] = busse[i].Haelt ? steht[i] + s : 0;
                    max = Math.Max(max, steht[i]);
                }
            }
        }
        return max;
    }

    [Fact]
    public void Stadt1_BusHaeltUndFaehrtWeiter_GegenprobeAlteRegelHaengt()
    {
        Assert.Contains(Szene.Value.Haltestellen, h => h.Nr == 3);
        var neu = LaengsterHalt();
        Assert.True(neu < 20, $"Bus steht {neu:F1} s");
        Assert.True(neu >= Buslinie.HaltSekunden - 0.1, $"Bus haelt nie ({neu:F1} s)");

        Verkehr.HaltAltFuerTests = true;
        double alt;
        try
        {
            alt = LaengsterHalt();
        }
        finally
        {
            Verkehr.HaltAltFuerTests = false;
        }
        Assert.True(alt > 60, $"Gegenprobe: mit der alten Regel steht der Bus nur {alt:F1} s");
    }

    [Fact]
    public void Pruefung_Stadt1_OhneFund_GegenprobeAlteRegelRot_AltePruefungSahEsNicht()
    {
        static List<StadtbauFund> Bus() => [.. StadtbauPruefung.Aussen(Stadt1(), Daten.Spiel.Inhalt, Daten.Spiel.Grafik)
            .Where(f => f.Text.Contains("Bus bleibt stecken", StringComparison.Ordinal) || f.Text.StartsWith("Hier bleibt der Bus", StringComparison.Ordinal))];
        Assert.Empty(Bus());

        Verkehr.HaltAltFuerTests = true;
        try
        {
            var rot = Bus();
            Assert.NotEmpty(rot);
            Assert.All(rot, f => Assert.True(f.Rot));
            // die Pruefung bis A 0.72.0 (90 Sekunden, nur Tag 1) sah den haengenden Bus nicht
            StadtbauPruefung.BusTageAltFuerTests = true;
            Assert.Empty(Bus());
        }
        finally
        {
            Verkehr.HaltAltFuerTests = false;
            StadtbauPruefung.BusTageAltFuerTests = false;
        }
    }

    [Fact]
    public void BusRunde_MindestensEineRundeJedesBusses()
    {
        var v = Verkehr.FuerSpieltag(Szene.Value, 1);
        foreach (var bus in v.Autos.Where(a => a.Nummer >= Buslinie.ErsteNummer))
        {
            Assert.True(StadtbauPruefung.BusRunde(v) >= (bus.Laenge / bus.Tempo) + (bus.Halte.Count * Buslinie.HaltSekunden) + 20);
        }
        Assert.True(StadtbauPruefung.BusSteckenTage.Length >= 3);
    }
}
