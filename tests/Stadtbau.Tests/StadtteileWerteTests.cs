using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;

namespace Stadtbau.Tests;

/// <summary>
/// Welle Nachladen S 0.54.0 (Weisung P12-280): die Werte des Knopfs „Stadtteile“ an der Grundlage des Werkzeugs:
/// jede Kachel genau einmal, Reihe fuer Reihe (A1, B1 …), Dreiecke und Netzdaten = Bau der Kachel wie im Spiel
/// (<c>StadtSzene.KachelVorbereiten</c>, Szene der Grenzen), keine Kachel leer.
/// </summary>
public class StadtteileWerteTests
{
    private static Spieldaten D => Daten.Spiel;

    [Fact]
    public void Grundlage_JedeKachelEinmal_WerteGleichDemBauImSpiel()
    {
        var p = Projekt.Neu("Stadtteile Werte", D, Daten.NeueAblage());
        var werte = StadtteilWert.Fuer(p.Stand(), D);
        var szene = Ansicht3D.Bauen(p.Stand(), D, Ansicht3D.Einstellung.Grenzen).Szene;
        foreach (var w in werte)
        {
            var n = szene.KachelVorbereiten(w.Kachel);
            Assert.Equal(n.Dreiecke, w.Dreiecke);
            Assert.True(w.Dreiecke > 0 && w.Bytes > 0 && w.Netze > 0, w.Name);
        }
        Assert.Equal(Stadtteile.Alle(szene.Breite, szene.Tiefe), werte.Select(w => w.Kachel));
        Assert.Equal("A1", werte[0].Name);
        Assert.True(werte.Count >= 4);
    }
}
