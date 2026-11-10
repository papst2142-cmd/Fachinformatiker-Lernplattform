using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Sammelliste 271 (Weisung P12-134 Frage 1 = a, A 0.56.0 / S 0.31.0): eine Laterne in einer Stadt ohne jede Strasse.
/// Die Pruefung ist rot („Es gibt Laternen, aber keine Straße.“), die 3D-Ansicht baut die Stadt trotzdem (ohne diese
/// Laterne) statt mit einer Ausnahme leer zu bleiben. Gegenprobe: der alte Weg bricht beim Bau ab.
/// </summary>
public class LaterneOhneStrasseTests
{
    private static Spieldaten D => Daten.Spiel;

    private static StadtFassung NurLaterne()
    {
        var p = Projekt.Neu("Nur Laterne", D, Daten.NeueAblage(), leer: (16, 16));
        p.AussenAendern("Laterne", a => AussenBearbeitung.Setzen(a, AussenArt.Laterne, 8, 8));
        return p.Stand();
    }

    [Fact]
    public void LaterneOhneStrasse_PruefungRot_3DBautOhneLaterne()
    {
        var stand = NurLaterne();
        Assert.Single(stand.Laternen);
        Assert.Contains(Projekt.PruefenAussen(stand, D), f => f.Rot && f.Text == "Es gibt Laternen, aber keine Straße.");
        var spiel = Ansicht3D.Bauen(stand, D, Ansicht3D.Einstellung.Standard);
        Assert.Empty(spiel.Szene.Laternen);
        Assert.True(spiel.Szene.Teile.Count > 0);
    }

    [Fact]
    public void Gegenprobe_AlterWeg_BrichtAb()
    {
        var stand = NurLaterne();
        Nachtlicht.OhneStrasseAltFuerTests = true;
        try
        {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => Ansicht3D.Bauen(stand, D, Ansicht3D.Einstellung.Standard).Szene.Laternen.Count);
        }
        finally
        {
            Nachtlicht.OhneStrasseAltFuerTests = false;
        }
    }

    [Fact]
    public void MitStrasse_LaternenUnveraendert()
    {
        // Fassung 0: jede Laterne hat ihre Strasse, die Plaetze bleiben dieselben (22, Test LaternenGenauDiesePlaetze im Spiel)
        var szene = Ansicht3D.Bauen(D.Fassung, D, Ansicht3D.Einstellung.Standard).Szene;
        Assert.Equal(22, szene.Laternen.Count);
    }
}
