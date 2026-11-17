using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;

namespace Stadtbau.Tests;

/// <summary>
/// Sammelliste 370 (Weisung P12-292 Teil B): auf einer Karte 256 × 256 mit drei eingesetzten Kopien meldete die Fundliste
/// je Kopie eine rote Laterne bei der Betriebswerkstatt (Nr. 42, 64, 86). Befund: kein Fehler beim Einsetzen. Die Kopien
/// stammten aus der Grundlage des Werkzeugs (Fassung 0, „Neu“), in der Laterne 20 schon im Original rot ist („sobald alle
/// Orte zu sehen sind“, bekannt seit Weisung P12-30; Nico versetzt sie im Werkzeug). In einer Kopie ist die
/// Betriebswerkstatt ein Gebaeude ohne Spielort, also immer zu sehen: darum dieselbe Verschiebung 0,4 schon „jetzt“.
/// Nicos Stadt 1 (Fassung 2) hat die Laterne frei und bleibt auch in drei Kopien ohne Laternen-Fund.
/// Grenze 0,2 und <c>Nachtlicht.Regeln</c> unveraendert.
/// </summary>
public class LaterneKopie370Tests
{
    private const string Fassung2 = "inhalte_csharp/spiel/stadtbau/stadt1_fassung2.fistadt";

    private static List<StadtbauFund> Laternen(Projekt p) =>
        [.. p.PruefenAussen().Where(f => f.Text.StartsWith("Laterne", StringComparison.Ordinal))];

    private static Projekt KarteMitKopien(Func<Werkzeugordner, string, Projekt> neu, out Projekt quelle)
    {
        var ablage = Daten.NeueAblage();
        quelle = neu(ablage, "Teil");
        var ziel = neu(ablage, "Ziel");
        Assert.Null(ziel.KarteVergroessern(256, 256, 1));
        foreach (var platz in new[] { 2, 5, 6 })
        {
            var e = ziel.StadtteilEinsetzen(quelle, platz);
            Assert.True(e.Geht, e.Grund);
        }
        return ziel;
    }

    [Fact]
    public void NicosStadt1_Fassung2_AlleinUndInDreiKopien_OhneLaternenFund()
    {
        Projekt Neu(Werkzeugordner a, string n) => Projekt.AusStadtDatei(Daten.Lesen(Fassung2)!, Daten.Spiel, a, n);
        var allein = Neu(Daten.NeueAblage(), "Allein");
        Assert.Empty(Laternen(allein));
        var ziel = KarteMitKopien(Neu, out _);
        var funde = Laternen(ziel);
        Assert.True(funde.Count == 0, string.Join("\n", funde.Select(f => f.Text)));
    }

    [Fact]
    public void Grundlage_Fassung0_KopieMeldetDieselbeLaterneWieDasOriginal_UmDenPlatzVersetzt()
    {
        Projekt Neu(Werkzeugordner a, string n) => Projekt.Neu(n, Daten.Spiel, a);
        // Original allein: Laterne 20 rot, sobald alle Orte zu sehen sind (Betriebswerkstatt kommt spaeter dazu)
        var original = Assert.Single(Laternen(Neu(Daten.NeueAblage(), "Allein")));
        Assert.True(original.Rot);
        Assert.StartsWith("Laterne Nr. 20 muss um 0,4 verschoben werden, sobald alle Orte zu sehen sind", original.Text);

        var ziel = KarteMitKopien(Neu, out _);
        var funde = Laternen(ziel);
        Assert.Equal(4, funde.Count);
        Assert.Contains(funde, f => f.Text == original.Text && f.X == original.X && f.Y == original.Y);
        // je Kopie genau die Laterne 20 ihres Platzes (Nummer + 22 je Stadt), gleiche Verschiebung, Lage um den Platz versetzt
        foreach (var (nr, dx, dy) in new[] { (42, 64, 0), (64, 0, 64), (86, 64, 64) })
        {
            var f = Assert.Single(funde, f => f.Text.StartsWith($"Laterne Nr. {nr} ", StringComparison.Ordinal));
            Assert.True(f.Rot);
            Assert.Equal($"Laterne Nr. {nr} muss um 0,4 verschoben werden, damit sie frei steht (erlaubt 0,2).", f.Text);
            Assert.Equal(original.X!.Value + dx, f.X!.Value, 3);
            Assert.Equal(original.Y!.Value + dy, f.Y!.Value, 3);
        }
    }

    [Fact]
    public void Grundlage_Fassung0_LaterneImOriginalFrei_KopienGruen()
    {
        // so wie Nico es im Werkzeug tut (Weisung P12-30): ist Laterne 20 im Teil frei, sind es auch alle Kopien
        Projekt Neu(Werkzeugordner a, string n)
        {
            var p = Projekt.Neu(n, Daten.Spiel, a);
            p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
            return p;
        }
        var ziel = KarteMitKopien(Neu, out var quelle);
        Assert.Empty(Laternen(quelle));
        var funde = Laternen(ziel);
        Assert.True(funde.Count == 0, string.Join("\n", funde.Select(f => f.Text)));
    }
}
