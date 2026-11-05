using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Tests;

public class ErkundungTests(ITestOutputHelper aus)
{
    [Fact]
    public void Fassung0Funde()
    {
        var d = Daten.Spiel;
        foreach (var ort in d.Orte)
        {
            var g = InnenGrundriss.Grundriss(d.Fassung.Innenraeume, ort);
            var funde = StadtbauPruefung.Innen(ort, g, InnenGrundriss.Personen(d.Fassung.Innenraeume, ort), d.Grafik, d.Geschaefte, d.Fassung.Innenraeume);
            var innen = Lernplattform.Core.Spiel.Probe.Innenraum.AusGrundrissMitPersonen(StadtbauPruefung.Ansicht(ort), g, InnenGrundriss.Personen(d.Fassung.Innenraeume, ort), d.Grafik);
            aus.WriteLine(ort + " " + StadtbauGrenzen.Innen(innen));
            foreach (var f in funde)
            {
                aus.WriteLine("  " + (f.Rot ? "ROT " : "gelb ") + f.Text);
            }
        }
    }
}
