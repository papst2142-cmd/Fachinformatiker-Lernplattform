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

    /// <summary>
    /// Fund 305 (P12-199): in Nicos Teststadt Laterne Nr. 11 um (1; 0,5) schieben. Danach findet eine Laterne an ihrer Strasse
    /// keinen freien Platz. Die Pruefung meldet das rot, die 3D-Ansicht baut trotzdem (ohne diese Laterne). Gegenprobe: mit dem
    /// alten Weg (S 0.42.0) brach der ganze Bau ab, die Ansicht blieb beim alten Bild mit „Fehler“.
    /// </summary>
    [Fact]
    public void LaterneOhneFreienPlatz_PruefungRot_3DBautTrotzdem()
    {
        var datei = File.ReadAllBytes(Path.Combine(Daten.Wurzel, "tests", "Stadtbau.Tests", "Testdaten", "nico", "teststadt_S0.40.0.fistadt"));
        var p = Projekt.AusStadtDatei(datei, D, Daten.NeueAblage(), "Fund 305");
        p.AussenAendern("Schieben", a => AussenBearbeitung.Verschieben(a, new AussenStelle(AussenArt.Laterne, 10), 1, 0.5));
        var stand = p.Stand();
        Assert.Contains(Projekt.PruefenAussen(stand, D), f => f.Rot && f.Text.Contains("findet an ihrer Straße keinen freien Platz", StringComparison.Ordinal));
        var spiel = Ansicht3D.Bauen(stand, D, Ansicht3D.Einstellung.Standard);
        Assert.Equal(stand.Laternen.Count - 1, spiel.Szene.Laternen.Count);
        Ansicht3D.LaterneAltFuerTests = true;
        try
        {
            Assert.Throws<InvalidOperationException>(() => Ansicht3D.Bauen(stand, D, Ansicht3D.Einstellung.Standard).Szene.Laternen.Count);
        }
        finally
        {
            Ansicht3D.LaterneAltFuerTests = false;
        }
    }
}
