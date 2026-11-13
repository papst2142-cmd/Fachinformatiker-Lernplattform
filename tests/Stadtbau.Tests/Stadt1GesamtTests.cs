using Stadtbau.Core;
using Xunit;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.52.0 (Weisung P12-242 Punkt 2, Gesamtpruefung): Nicos Stadt 1 (Fassung 1) im Werkzeug geoeffnet zeigt in
/// beiden Fundlisten „Stadt – 0 rot“ und „Innenräume – 0 rot“ (derselbe Weg wie die Listen rechts). Gegenprobe: dieselbe
/// Rechnung findet einen roten Fund, sobald die Stadt einen hat (Laterne 20 auf die Fahrbahn).
/// </summary>
public class Stadt1GesamtTests
{
    [Fact]
    public void Stadt1_BeideFundlisten_NullRot_GegenprobeRot()
    {
        var p = HuegelFund336Tests.Stadt1("Gesamt Stadt1");
        var innen = InnenFunde.Rechnen(InnenFunde.Vorbereiten(p));
        Assert.NotEmpty(innen);
        var innenRot = innen.SelectMany(o => o.Funde.Where(f => f.Rot).Select(f => o.Schluessel + ": " + f.Text)).ToList();
        Assert.True(innenRot.Count == 0, "Innenräume rot: " + string.Join(" | ", innenRot));
        var aussenRot = p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text).ToList();
        Assert.True(aussenRot.Count == 0, "Stadt rot: " + string.Join(" | ", aussenRot));

        // Gegenprobe: eine Laterne mitten auf die Fahrbahn = rot in derselben Rechnung
        p.AussenAendern("Laterne", a =>
        {
            var l = (List<object?>)Aussen.Laternen(a)[0]!;
            l[0] = 14.5;
            l[1] = 30.0;
            return true;
        });
        Assert.Contains(p.PruefenAussen(), f => f.Rot);
    }

    /// <summary>
    /// Rundgang Stadt 1 (Fassung 1): jeder Weg zwischen zwei Orten geht zu Fuss (Minuten gesetzt) und mindestens ein Weg
    /// mit dem Bus (Haltestellen seit Fassung 1). Gegenprobe Fassung 0: kein Bus.
    /// </summary>
    [Fact]
    public void Stadt1_Rundgang_JederOrtZuFuss_BusFaehrt_GegenprobeFassung0OhneBus()
    {
        var p = HuegelFund336Tests.Stadt1("Rundgang Stadt1");
        var wege = Laufzeiten.Rechnen(p.Stand(), Daten.Spiel.Inhalt, Daten.Spiel.Grafik);
        Assert.NotEmpty(wege);
        var ohne = wege.Where(w => w.Minuten is null).Select(w => w.VonName + " – " + w.NachName).ToList();
        Assert.True(ohne.Count == 0, "zu Fuß kein Weg: " + string.Join(" | ", ohne));
        Assert.Contains(wege, w => w.BusHin is not null || w.BusZurueck is not null);
        TestContext.Current.SendDiagnosticMessage($"Stadt 1: {wege.Count} Wege zu Fuß, {wege.Count(w => w.BusHin is not null || w.BusZurueck is not null)} auch mit dem Bus");

        var alt = Projekt.Neu("Rundgang Fassung0", Daten.Spiel, Daten.NeueAblage());
        Assert.All(Laufzeiten.Rechnen(alt.Stand(), Daten.Spiel.Inhalt, Daten.Spiel.Grafik), w => Assert.Null(w.BusHin));
    }
}
