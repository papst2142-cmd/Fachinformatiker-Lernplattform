using Lernplattform.Core.Dashboard;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 11 FW10 (Weisung P11-19, Antwort 2a, Sammelliste 195): Themenname der Dashboard-Themenreihe wird nur
/// verkleinert, wenn sein laengstes Wort nicht in die Spalte passt, hoechstens auf 85 %.
/// </summary>
public class ThemennameTests
{
    [Fact]
    public void PasstDasWort_BleibtDieSchrift()
    {
        Assert.Equal(13.2, Themenname.Groesse(13.2, 80, 80));
        Assert.Equal(13.2, Themenname.Groesse(13.2, 79.9, 80));
        Assert.Equal(13.2, Themenname.Groesse(13.2, 200, double.PositiveInfinity));
        Assert.Equal(13.2, Themenname.Groesse(13.2, 200, 0));
    }

    [Fact]
    public void ZuBreitesWort_WirdNurSoWeitWieNoetigKleiner()
    {
        // Lauf 450 (Intel-Mac): Wort minimal breiter als die Spalte -> knapp kleiner, Wort passt
        var g = Themenname.Groesse(13.2, 84.0, 82.0);
        Assert.True(g < 13.2);
        Assert.True(84.0 * g / 13.2 <= 82.0, "Wort passt nicht: " + g);
        Assert.True(g >= 13.2 * Themenname.Untergrenze);
    }

    [Fact]
    public void NieUnter85Prozent()
    {
        Assert.Equal(0.85, Themenname.Untergrenze);
        Assert.Equal(13.2 * 0.85, Themenname.Groesse(13.2, 200, 80), 6);
    }
}
