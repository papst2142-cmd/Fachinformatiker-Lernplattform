using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Sammelliste 238 (Weisungen P12-81, P12-83): die 3D-Ansicht des Werkzeugs zeigt jeden Ort, den die Draufsicht zeigt. Bis
/// S 0.18.0 schaltete der Probe-Spielstand die Serverfarm nicht frei; das Rechenzentrum Lokschuppen fehlte in 3D und die
/// Pruefung draussen pruefte es nicht.
/// </summary>
public class OrteIn3DTests
{
    private static Spieldaten D => Daten.Spiel;

    /// <summary>Haeuser der 3D-Ansicht mit mindestens einem Dreieck (Kennung des Orts bzw. Modell#n eines Gebaeudes ohne Ort).</summary>
    private static List<string> Gezeichnet()
    {
        var spiel = Ansicht3D.Bauen(D.Fassung, D, Ansicht3D.Einstellung.Standard);
        return [.. spiel.Szene.Vorbereiten().Haeuser.Where(h => h.Value.Indizes.Count >= 3).Select(h => h.Key.Id)];
    }

    /// <summary>Was die Draufsicht zeigt: jeder Ort der Karte und jedes Gebaeude ohne Ort.</summary>
    private static (List<string> Orte, List<string> Gebaeude) Karte()
    {
        var karte = D.Fassung.Karte;
        var orte = Liste(karte["orte"]).OfType<PyObjekt>().Select(o => Py.Str(o["id"])).ToList();
        var gebaeude = Liste(Objekt(karte["welt"])["landschaft"]).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "gebaeude")).Select(t => Py.Str(t["modell"])).ToList();
        return (orte, gebaeude);
    }

    private static List<string> Fehlend(List<string> gezeichnet)
    {
        var (orte, gebaeude) = Karte();
        return [.. orte.Where(o => !gezeichnet.Contains(o)), .. gebaeude.Where(m => !gezeichnet.Any(g => g.StartsWith(m + "#", StringComparison.Ordinal)))];
    }

    [Fact]
    public void Stadt1_JederOrtUndJedesGebaeude_HatDreieckeIn3D()
    {
        var (orte, gebaeude) = Karte();
        Assert.Contains("lokschuppen", orte);
        Assert.NotEmpty(gebaeude);
        Assert.Equal([], Fehlend(Gezeichnet()));
    }

    [Fact]
    public void Gegenprobe_ProbestandOhneServerfarm_LokschuppenFehlt()
    {
        StadtbauSpiel.ProbestandOhneFarmFuerTests = true;
        try
        {
            Assert.Equal(["lokschuppen"], Fehlend(Gezeichnet()));
        }
        finally
        {
            StadtbauSpiel.ProbestandOhneFarmFuerTests = false;
        }
    }

    [Fact]
    public void Probestand_SchaltetServerfarmFrei_SpielstandSonstGleich()
    {
        var mit = StadtbauSpiel.Probestand(D.Inhalt).Stand;
        Assert.NotNull(mit.FarmFrei);
        StadtbauSpiel.ProbestandOhneFarmFuerTests = true;
        try
        {
            Assert.Null(StadtbauSpiel.Probestand(D.Inhalt).Stand.FarmFrei);
        }
        finally
        {
            StadtbauSpiel.ProbestandOhneFarmFuerTests = false;
        }
        Assert.Equal(200, mit.TageFertig);
    }
}
