using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W2b (Weisung P8-4, Sammelliste 114-116): Gruppen der Seitenleiste und der Optionen,
/// Kurzzeilen der Optionen-Karten und der Kasten LERNSTATUS.
/// Kontrollfehler: ein Eintrag fehlt in Navigation.PcGruppen -> JederEintragGenauEinmal rot;
/// Lernstatus zaehlt nur Karten -> LernstatusWieDashboard rot.
/// </summary>
public class SeitenleisteOptionenTests
{
    private static readonly DashboardInhalte Inhalte = new(Lerninhalte.Laden(Pfade.Inhalte));

    [Fact]
    public void JederEintragGenauEinmal()
    {
        var ids = Navigation.PcGruppen.SelectMany(g => g.Ids).Concat(Navigation.PcUnten).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(Navigation.Pc.Select(p => p.Id).Order(StringComparer.Ordinal), ids.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void GruppenWieNico()
    {
        Assert.Equal([null, "LERNEN", "SPIEL", "WERKZEUGE"], Navigation.PcGruppen.Select(g => g.Titel));
        Assert.Equal(["cards", "quiz", "ap1scenarios", "scenarios", "testproject", "abschluss", "progress"], Navigation.PcGruppen[1].Ids);
        Assert.Equal(["help", "settings"], Navigation.PcUnten);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OptionenJederBereichGenauEinmal(bool pc)
    {
        var gruppen = Optionen.Gruppieren(Geraetetexte.OptionenBereiche(pc));
        Assert.Equal(["LERNEN", "AUSSEHEN", "SPIEL", "DATEN", "PROGRAMM"], gruppen.Select(g => g.Titel));
        var ids = gruppen.SelectMany(g => g.Bereiche).Select(b => b.Id).ToList();
        Assert.Equal(Geraetetexte.OptionenBereiche(pc).Select(b => b.Id).Order(StringComparer.Ordinal), ids.Order(StringComparer.Ordinal));
        Assert.Equal(pc, ids.Contains(Optionen.Datenbank));
        Assert.All(gruppen, g => Assert.NotEmpty(g.Bereiche));
    }

    [Fact]
    public void JedeKarteHatKurzzeile()
    {
        foreach (var bereich in Optionen.AlleFuer(true))
        {
            Assert.False(string.IsNullOrWhiteSpace(Optionen.Kurzzeile(bereich)), bereich.Id);
            if (bereich.UntertitelPc is { } untertitel)
            {
                Assert.Equal(untertitel, Optionen.Kurzzeile(bereich));
            }
        }
    }

    /// <summary>
    /// Weisung P8-5b (Sammelliste 121, Nico 26.10.2026 19:43): jede Gruppe eine Farbe, jede Karte die Farbe
    /// ihrer Gruppe, "Loeschen und zuruecksetzen" rot. Die Farben sind Schluessel der Farbwelt (keine Hexwerte).
    /// </summary>
    [Fact]
    public void GruppenfarbenWieNico()
    {
        Assert.Equal(
            [("LERNEN", "blue"), ("AUSSEHEN", "cyan"), ("SPIEL", "pink"), ("DATEN", "purple"), ("PROGRAMM", "orange")],
            Optionen.Gruppen.Select(g => (g.Titel, Optionen.GruppenFarben[g.Titel])));
        foreach (var (titel, ids) in Optionen.Gruppen)
        {
            foreach (var id in ids)
            {
                Assert.Equal(id == Optionen.Loeschen ? "red" : Optionen.GruppenFarben[titel], Optionen.Farbe(id));
            }
        }
        Assert.Equal("red", Optionen.Farbe(Optionen.Loeschen));
        Assert.Contains(Optionen.Loeschen, Optionen.Gruppen.Single(g => g.Titel == Optionen.GruppeDaten).Ids);
        var palette = Farbwelt.Berechne("", "", "");   // unbekannt: Standard wie Python
        foreach (var farbe in Optionen.GruppenFarben.Values.Append("red"))
        {
            Assert.True(palette.Farben.ContainsKey(farbe), farbe);
        }
    }

    [Fact]
    public void LernstatusWieDashboard()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, "lernstand.db");
        File.Copy(Pfade.Vergleich("testdaten.db"), pfad);
        var werte = DashboardWerte.Lesen(pfad, new DateOnly(2026, 10, 20), Inhalte, new Einstellungen.Tagesziel(true, 20, true, false, "18:00"));
        var (bearbeitet, gesamt) = Lernstatus.Lesen(pfad, Inhalte);
        Assert.True(bearbeitet > 0);
        Assert.Equal(werte.Bearbeitet, bearbeitet);
        Assert.Equal(werte.InhalteGesamt, gesamt);
    }

    [Fact]
    public void LernstatusOhneOderMitKaputterDatei()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, "lernstand.db");
        Assert.Equal((0L, Inhalte.Summe), Lernstatus.Lesen(pfad, Inhalte));
        File.WriteAllText(pfad, "keine Datenbank");
        Assert.Equal((0L, Inhalte.Summe), Lernstatus.Lesen(pfad, Inhalte));
    }
}
