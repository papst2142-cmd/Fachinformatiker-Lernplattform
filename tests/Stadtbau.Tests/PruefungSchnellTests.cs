using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle A 0.83.0 / S 0.55.0 (Weisung P12-282: Pruefung der Testkarte deutlich unter den rund 45 s von S 0.54.0). Die
/// schnelleren Rechnungen muessen GENAU dasselbe ergeben wie die alten (Gegenproben je Ablauf):
/// begehbare Zellen der Spiel-Stadt (<see cref="Stadt.BegehbarAltFuerTests"/>), Funde der Pruefung draussen mit dem
/// Vorfilter nach Rahmen (<see cref="StadtbauPruefung.RahmenAusFuerTests"/>) und die Karte „Laufzeiten“ mit gemerkten
/// Fusswegen (<see cref="Laufzeiten.OhneMerkerFuerTests"/>), an Stadt 1 (aktive Fassung, Fassung 0) und an der Testkarte.
/// </summary>
public class PruefungSchnellTests
{
    private static byte[]? Lesen(string pfad)
    {
        var d = Path.Combine(Daten.Wurzel, pfad);
        return File.Exists(d) ? File.ReadAllBytes(d) : null;
    }

    private static readonly Lazy<StadtFassung> Testkarte = new(() =>
        StadtFassung.Lesen(File.ReadAllBytes(Path.Combine(Daten.Wurzel, "tests/Lernplattform.Tests/Testdaten/testkarte_256.fistadt"))));

    public static TheoryData<string> Staedte => ["aktiv", "fassung0", "testkarte"];

    private static StadtFassung Fassung(string welche) => welche switch
    {
        "aktiv" => AktiveFassung.Laden(Lesen),
        "fassung0" => AktiveFassung.LadenNummer(Lesen, 0),
        _ => Testkarte.Value,
    };

    private static T Alt<T>(Func<T> rechnen, Action<bool> schalter)
    {
        schalter(true);
        try
        {
            return rechnen();
        }
        finally
        {
            schalter(false);
        }
    }

    [Theory]
    [MemberData(nameof(Staedte))]
    public void BegehbareZellen_GleichWieVorher(string welche)
    {
        var f = Fassung(welche);
        Stadt Bauen() => (Stadt)SpielKern.Regeln.Stadt(f, f.MitKarte(Daten.Spiel.Inhalt), nachsichtig: true);
        var neu = Bauen().Begehbar.ToList();
        var alt = Alt(() => Bauen().Begehbar.ToList(), v => Stadt.BegehbarAltFuerTests = v);
        Assert.NotEmpty(neu);
        Assert.Equal(alt, neu);
    }

    [Theory]
    [MemberData(nameof(Staedte))]
    public void PruefungDraussen_GleicheFundeWieVorher(string welche)
    {
        var f = Fassung(welche);
        var neu = Projekt.PruefenAussen(f, Daten.Spiel, wege: []);
        var alt = Alt(() => Projekt.PruefenAussen(f, Daten.Spiel, wege: []), v => StadtbauPruefung.RahmenAusFuerTests = v);
        Assert.Equal(alt, neu);
    }

    [Theory]
    [MemberData(nameof(Staedte))]
    public void Laufzeiten_GleichWieVorher(string welche)
    {
        var f = Fassung(welche);
        var neu = Laufzeiten.Rechnen(f, Daten.Spiel.Inhalt, Daten.Spiel.Grafik);
        var alt = Alt(() => Laufzeiten.Rechnen(f, Daten.Spiel.Inhalt, Daten.Spiel.Grafik), v => Laufzeiten.OhneMerkerFuerTests = v);
        Assert.NotEmpty(neu);
        Assert.Equal(alt, neu);
    }

    /// <summary>Gegenprobe des Vorfilters: zwei Strassen aufeinander bleiben rot, zwei weit getrennte werden uebersprungen.</summary>
    [Fact]
    public void Rahmen_FernNurWennWeiterAlsGrenze()
    {
        var a = StadtbauPruefung.Rahmen.Von([new(0, 0), new(10, 0)]);
        var b = StadtbauPruefung.Rahmen.Von([new(0, 1), new(10, 1)]);
        var c = StadtbauPruefung.Rahmen.Von([new(0, 5), new(10, 5)]);
        Assert.False(a.Fern(b, 1.2f));
        Assert.True(a.Fern(c, 1.2f));
        StadtbauPruefung.RahmenAusFuerTests = true;
        try
        {
            Assert.False(a.Fern(c, 1.2f));
        }
        finally
        {
            StadtbauPruefung.RahmenAusFuerTests = false;
        }
    }
}
