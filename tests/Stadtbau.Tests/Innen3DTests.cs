using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.49.0 (Weisung P12-231 Antwort 1a): Innenraum in 3D ansehen. Gebaut wird genau der Innenraum, den das Spiel beim Betreten
/// baut (gleiche Inhalts-Signatur), mit dem Stand des Grundrisses im Werkzeug; nichts wird in den Probe-Spielstand geschrieben;
/// eine Aenderung nur innen baut das Spiel nicht neu; Etagen stehen an der Ankunft; Innenraeume neuer Gebaeude nicht.
/// </summary>
public class Innen3DTests
{
    private static Spieldaten D => Daten.Spiel;

    private static string Signatur(Innen3D.Auftrag a, Innen3D.Ergebnis e, string ansicht) =>
        Innenraum.InhaltSignatur(ansicht, e.Spiel.Ablauf.Stand, a.Innenraeume, e.Spiel.GeschaefteDaten, null, Jahreszeit.Sommer)!;

    [Fact]
    public void Elektromarkt_WieDasSpielEsBetritt_MitDemGeaendertenGrundriss_OhneEreignis()
    {
        var p = Projekt.Neu("Innen 3D", D, Daten.NeueAblage(), "elektromarkt");
        var av = Innen3D.Vorbereiten(p, "elektromarkt");
        var vorher = Innen3D.Bauen(av, null);
        p.Aendern("elektromarkt", "Boden", g => Bearbeitung.Boden(g, "flur", "teppich"));
        var a = Innen3D.Vorbereiten(p, "elektromarkt");
        var e = Innen3D.Bauen(a, null);
        Assert.Equal("geschaeft:elektromarkt", e.Innen.Ansicht);
        // derselbe Innenraum wie beim Betreten im Spiel mit dieser Fassung (gleiche Signatur), und der Teppich ist drin
        var spielInnen = Innenraum.Bauen("geschaeft:elektromarkt", e.Spiel.Ablauf.Stand, p.Stand().Innenraeume, e.Spiel.Grafik, e.Spiel.GeschaefteDaten);
        Assert.NotNull(spielInnen);
        Assert.Equal(Innenraum.InhaltSignatur("geschaeft:elektromarkt", e.Spiel.Ablauf.Stand, p.Stand().Innenraeume, e.Spiel.GeschaefteDaten, null, Jahreszeit.Sommer),
            Signatur(a, e, "geschaeft:elektromarkt"));
        Assert.NotEqual(Signatur(av, vorher, "geschaeft:elektromarkt"), Signatur(a, e, "geschaeft:elektromarkt"));
        Assert.DoesNotContain("teppich", Signatur(av, vorher, "geschaeft:elektromarkt"), StringComparison.Ordinal);
        Assert.Contains("teppich", Signatur(a, e, "geschaeft:elektromarkt"), StringComparison.Ordinal);
        Assert.Equal(spielInnen.Teile.Count, e.Innen.Teile.Count);

        // ansehen: Figur an der Tuer, kein Ereignis im Probe-Spielstand
        var zeilen = e.Spiel.Ablauf.Stand.Verlauf.Count;
        Assert.NotEqual("elektromarkt", e.Spiel.Jetzt.Ort);   // im Spiel muesste die Figur erst hingehen
        e.Spiel.Ansehen(e.Innen, e.Figur.X, e.Figur.Y, e.Figur.Z);
        Assert.True(e.Spiel.ImInnenraum);
        Assert.Same(e.Innen, e.Spiel.Innen);
        Assert.Equal(e.Innen.StartX, e.Spiel.FigurX);
        Assert.Equal(e.Innen.StartZ, e.Spiel.FigurZ);
        Assert.Equal(zeilen, e.Spiel.Ablauf.Stand.Verlauf.Count);
    }

    [Fact]
    public void NurInnenGeaendert_SpielBleibt_DraussenGeaendert_SpielNeu()
    {
        var p = Projekt.Neu("Innen 3D Spiel", D, Daten.NeueAblage(), "bank");
        var erst = Innen3D.Bauen(Innen3D.Vorbereiten(p, "bank"), null);
        Assert.True(erst.SpielNeu);
        p.Aendern("bank", "Boden", g => Bearbeitung.Boden(g, "flur", "beton"));
        var innen = Innen3D.Bauen(Innen3D.Vorbereiten(p, "bank"), erst);
        Assert.False(innen.SpielNeu);
        Assert.Same(erst.Spiel, innen.Spiel);
        Assert.NotSame(erst.Innen, innen.Innen);
        var s = new AussenStelle(AussenArt.Haus, -1, 0, "bank");
        p.AussenAendern("Drehen", a => AussenBearbeitung.Drehen(a, s));
        var draussen = Innen3D.Bauen(Innen3D.Vorbereiten(p, "bank"), innen);
        Assert.True(draussen.SpielNeu);
        Assert.NotSame(innen.Spiel, draussen.Spiel);
    }

    [Fact]
    public void Etage_FigurStehtAnDerAnkunft()
    {
        var p = Projekt.Neu("Innen 3D Etage", D, Daten.NeueAblage(), "moebelhaus");
        p.Aendern("moebelhaus", "Etage", g =>
        {
            EtagenBearbeitung.Hinzufuegen(g);
            var s = Bearbeitung.Setzen(g, Etagen.Treppe, 9, 13)!;
            var d = Bearbeitung.Deko(g, s);
            Py.Setzen(d, "x", 8.0);
            Py.Setzen(d, "y", 12.2);
            Py.Setzen(d, "w", 3.0);
            Py.Setzen(d, "h", 1.6);
            Py.Setzen(d, "dreh", 3L);
            Assert.True(EtagenBearbeitung.ZielSetzen(g, 0, s, 1));
            EtagenBearbeitung.Aufraeumen(g);
        });
        var a = Innen3D.Vorbereiten(p, "moebelhaus@1");
        var e = Innen3D.Bauen(a, null);
        Assert.Equal("geschaeft:moebelhaus@1", e.Innen.Ansicht);
        var halt = Etagen.Uebergaenge(p.Grundriss("moebelhaus"), "moebelhaus").Select(u => u.Auf(1)).First(h => h is not null)!;
        Assert.Equal((float)halt.X, e.Figur.X);
        Assert.Equal((float)halt.Y, e.Figur.Y);
        Assert.Equal(halt.Blick, (long)e.Figur.Z);
        e.Spiel.Ansehen(e.Innen, e.Figur.X, e.Figur.Y, e.Figur.Z);
        Assert.Equal(1, e.Spiel.Etage);
    }

    [Fact]
    public void NeuesGebaeude_NochNicht_Grund()
    {
        Assert.Equal(Texte.Innen3DBauNicht, Innen3D.Nicht(Projekt.BauPraefix + "probe"));
        Assert.Null(Innen3D.Nicht("elektromarkt@1"));
        var p = Projekt.Neu("Innen 3D Bau", D, Daten.NeueAblage(), "elektromarkt");
        var a = Innen3D.Vorbereiten(p, "elektromarkt") with { Schluessel = Projekt.BauPraefix + "probe" };
        Assert.Equal(Texte.Innen3DBauNicht, Assert.Throws<InvalidDataException>(() => Innen3D.Bauen(a, null)).Message);
    }
}
