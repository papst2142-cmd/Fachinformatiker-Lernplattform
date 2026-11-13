using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Tests;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.51.0 (Weisung P12-241, Kurzplan P12-131 Punkt 4): Gelaende in der Mehrfachauswahl. Rahmen und Umschalt+Klick
/// waehlen Huegel und gemalte Hoehen; Verschieben, Kopieren, Drehen und Loeschen nehmen die gemalten Hoehen mit, je Aktion
/// EIN Schritt (ein Rueckgaengig stellt alles her); bei 16 Huegeln setzt und kopiert das Werkzeug keinen weiteren. Jede Regel
/// mit Gegenprobe (Stand S 0.50.0 ueber <see cref="AussenBearbeitung.GelaendeAuswahlAltFuerTests"/>).
/// Grundlage: leere Stadt 32 × 24, ein Huegel um (8; 8), ein gemalter Fleck um (20; 12).
/// </summary>
public class GelaendeAuswahlTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Projekt Grundlage()
    {
        var p = Projekt.Neu("GelaendeAuswahl", D, Daten.NeueAblage(), leer: (32, 24));
        Assert.NotNull(p.AussenAendern("Huegel", a => AussenBearbeitung.Setzen(a, AussenArt.Huegel, 8, 8)));
        Assert.True(p.AussenAendern("Pinsel", a => AussenBearbeitung.Pinselzug(a, PinselArt.Heben, [new(20, 12), new(21, 12)], 1) > 0));
        return p;
    }

    private static string Text(PyObjekt a) => PythonJson.Standard(a);

    private static Dictionary<(int, int), int> Gemalt(PyObjekt a)
    {
        var d = new Dictionary<(int, int), int>();
        for (var n = 0; n < AussenBearbeitung.GemaltListe(a).Count; n++)
        {
            var q = AussenBearbeitung.HoehePunkt(a, n)!.Value;
            d[(q.I, q.J)] = q.Hundertstel;
        }
        return d;
    }

    private static List<AussenStelle> AllesImRahmen(PyObjekt a) => AussenBearbeitung.ImRahmen(a, 0, 0, 32, 24);

    [Fact]
    public void Rahmen_WaehltHuegelUndGemalteHoehen_Gegenprobe()
    {
        var p = Grundlage();
        var a = p.AussenStand;
        var n = AussenBearbeitung.GemaltListe(a).Count;
        Assert.True(n > 4, "zu wenig gemalt: " + n);
        var g = AllesImRahmen(a);
        Assert.Contains(new AussenStelle(AussenArt.Huegel, 0), g);
        Assert.Equal(n, g.Count(s => s.Art == AussenArt.Hoehe));
        // nur ein Teil des Flecks im Rahmen: nur diese Punkte
        var teil = AussenBearbeitung.ImRahmen(a, 19.9, 0, 32, 24).Where(s => s.Art == AussenArt.Hoehe).ToList();
        Assert.All(teil, s => Assert.True(AussenBearbeitung.HoehePunkt(a, s.Nummer)!.Value.Lage.X >= 19.9f));
        Assert.InRange(teil.Count, 1, n - 1);
        AussenBearbeitung.GelaendeAuswahlAltFuerTests = true;
        try
        {
            Assert.DoesNotContain(AllesImRahmen(a), s => s.Art == AussenArt.Hoehe);
        }
        finally
        {
            AussenBearbeitung.GelaendeAuswahlAltFuerTests = false;
        }
    }

    [Fact]
    public void UmschaltKlick_AufGemaltemBoden_NimmtDenGanzenBereich_Gegenprobe()
    {
        var p = Grundlage();
        // zweiter, getrennter Fleck
        Assert.True(p.AussenAendern("Pinsel", a => AussenBearbeitung.Pinselzug(a, PinselArt.Heben, [new(4, 20)], 0.5f) > 0));
        var a = p.AussenStand;
        var bereich = AussenBearbeitung.HoehenBereichBei(a, 20.5, 12);
        var alle = AussenBearbeitung.GemaltListe(a).Count;
        Assert.True(bereich.Count > 4);
        Assert.True(bereich.Count < alle, "der getrennte Fleck gehoert nicht dazu");
        Assert.All(bereich, s => Assert.True(Vector2.Distance(AussenBearbeitung.HoehePunkt(a, s.Nummer)!.Value.Lage, new Vector2(20.5f, 12)) < 3));
        Assert.Empty(AussenBearbeitung.HoehenBereichBei(a, 12, 20));
        // Umschalten: dazu und wieder heraus
        var gruppe = new List<AussenStelle>();
        foreach (var s in bereich)
        {
            gruppe = AussenBearbeitung.Umschalten(gruppe, s);
        }
        Assert.Equal(bereich.Count, gruppe.Count);
        AussenBearbeitung.GelaendeAuswahlAltFuerTests = true;
        try
        {
            Assert.Empty(AussenBearbeitung.HoehenBereichBei(a, 20.5, 12));
        }
        finally
        {
            AussenBearbeitung.GelaendeAuswahlAltFuerTests = false;
        }
    }

    [Fact]
    public void Verschieben_NimmtHuegelUndGemaltesMit_EinSchritt_EinRueckgaengig()
    {
        var p = Grundlage();
        var vorher = Text(p.AussenStand);
        var gemaltVorher = Gemalt(p.AussenStand);
        var g = AllesImRahmen(p.AussenStand);
        var e = p.AussenAendern("Gruppe", a => AussenBearbeitung.Verschieben(a, g, 2, -1));
        var a = p.AussenStand;
        Assert.Equal(10.0, Py.F(SpielDaten.Objekt(AussenBearbeitung.HuegelListe(a)[0])["x"]));
        Assert.Equal(gemaltVorher.ToDictionary(k => (k.Key.Item1 + 4, k.Key.Item2 - 2), k => k.Value), Gemalt(a));
        Assert.Equal(g.Count, e.Auswahl.Count);
        Assert.Equal(gemaltVorher.Count, e.Auswahl.Count(s => s.Art == AussenArt.Hoehe));
        Assert.True(p.Rueckgaengig());
        Assert.Equal(vorher, Text(p.AussenStand));
        // Gegenprobe: ohne gemalte Hoehen in der Auswahl bleibt der Fleck liegen
        AussenBearbeitung.GelaendeAuswahlAltFuerTests = true;
        try
        {
            var alt = AllesImRahmen(p.AussenStand);
            p.AussenAendern("Alt", a2 => AussenBearbeitung.Verschieben(a2, alt, 2, -1));
            Assert.Equal(gemaltVorher, Gemalt(p.AussenStand));
        }
        finally
        {
            AussenBearbeitung.GelaendeAuswahlAltFuerTests = false;
        }
    }

    [Fact]
    public void Verschieben_MitGemaltem_RuecktInSchrittenEinhalb_UndUeberDenRandEntfaellt()
    {
        var p = Grundlage();
        var gemaltVorher = Gemalt(p.AussenStand);
        var g = AllesImRahmen(p.AussenStand);
        // 0,3 ohne Raster: mit gemalten Hoehen auf 0,5 gerundet
        p.AussenAendern("Gruppe", a => AussenBearbeitung.Verschieben(a, g, 0.3, 0, raster: false));
        Assert.Equal(gemaltVorher.ToDictionary(k => (k.Key.Item1 + 1, k.Key.Item2), k => k.Value), Gemalt(p.AussenStand));
        Assert.Equal(8.5, Py.F(SpielDaten.Objekt(AussenBearbeitung.HuegelListe(p.AussenStand)[0])["x"]));
        // weit nach rechts: der Fleck faellt teilweise ueber den Rand und zaehlt als uebersprungen
        var g2 = AllesImRahmen(p.AussenStand).Where(s => s.Art == AussenArt.Hoehe).ToList();
        var e = p.AussenAendern("Rand", a => AussenBearbeitung.Verschieben(a, g2, 10, 0));
        Assert.True(e.Uebersprungen > 0);
        Assert.Equal(gemaltVorher.Count - e.Uebersprungen, Gemalt(p.AussenStand).Count);
        Assert.All(Gemalt(p.AussenStand).Keys, k => Assert.True(k.Item1 <= 63));
    }

    [Fact]
    public void Kopieren_NimmtGemaltesMit_EinSchritt_Gegenprobe()
    {
        var p = Grundlage();
        var vorher = Text(p.AussenStand);
        var gemaltVorher = Gemalt(p.AussenStand);
        var g = AllesImRahmen(p.AussenStand).Where(s => s.Art == AussenArt.Hoehe).ToList();
        var e = p.AussenAendern("Kopie", a => AussenBearbeitung.Kopieren(a, g));
        var nachher = Gemalt(p.AussenStand);
        Assert.Equal(2 * gemaltVorher.Count, nachher.Count);
        Assert.All(gemaltVorher, k => Assert.Equal(k.Value, nachher[k.Key]));
        Assert.Equal(gemaltVorher.Count, e.Auswahl.Count);
        Assert.All(e.Auswahl, s => Assert.Equal(AussenArt.Hoehe, s.Art));
        // die neue Auswahl sind genau die Kopien (rechts der alten)
        var rechts = gemaltVorher.Keys.Max(k => k.Item1);
        Assert.All(e.Auswahl, s => Assert.True(AussenBearbeitung.HoehePunkt(p.AussenStand, s.Nummer)!.Value.I > rechts));
        Assert.True(p.Rueckgaengig());
        Assert.Equal(vorher, Text(p.AussenStand));
    }

    [Fact]
    public void Drehen_UndLoeschen_MitGemaltem_JeEinSchritt()
    {
        var p = Grundlage();
        var vorher = Text(p.AussenStand);
        var n = AussenBearbeitung.GemaltListe(p.AussenStand).Count;
        var g = AllesImRahmen(p.AussenStand);
        p.AussenAendern("Drehen", a => AussenBearbeitung.Drehen(a, g));
        Assert.NotEqual(vorher, Text(p.AussenStand));
        Assert.Equal(n, AussenBearbeitung.GemaltListe(p.AussenStand).Count);
        Assert.True(p.Rueckgaengig());
        Assert.Equal(vorher, Text(p.AussenStand));
        var e = p.AussenAendern("Loeschen", a => AussenBearbeitung.Loeschen(a, AllesImRahmen(a)));
        Assert.Equal(n + 1, e.Erledigt);
        Assert.Empty(AussenBearbeitung.GemaltListe(p.AussenStand));
        Assert.Empty(AussenBearbeitung.HuegelListe(p.AussenStand));
        Assert.True(p.Rueckgaengig());
        Assert.Equal(vorher, Text(p.AussenStand));
    }

    [Fact]
    public void Zielpunkt_SchonGemalt_BekommtDieNeueHoehe()
    {
        var p = Grundlage();
        var a = p.AussenStand;
        var hoch = Gemalt(a).OrderByDescending(k => k.Value).First();
        var nachbar = Gemalt(a).First(k => k.Key == (hoch.Key.Item1 + 1, hoch.Key.Item2));
        var stelle = Enumerable.Range(0, AussenBearbeitung.GemaltListe(a).Count)
            .First(n => AussenBearbeitung.HoehePunkt(a, n)!.Value.I == hoch.Key.Item1 && AussenBearbeitung.HoehePunkt(a, n)!.Value.J == hoch.Key.Item2);
        p.AussenAendern("Einer", aa => AussenBearbeitung.Verschieben(aa, [new AussenStelle(AussenArt.Hoehe, stelle)], 0.5, 0));
        var nachher = Gemalt(p.AussenStand);
        Assert.Equal(hoch.Value, nachher[nachbar.Key]);
        Assert.False(nachher.ContainsKey(hoch.Key));
    }

    [Fact]
    public void SechzehnHuegel_KeinWeiterer_KeineKopie_Gegenprobe()
    {
        var p = Projekt.Neu("Huegel16", D, Daten.NeueAblage(), leer: (40, 30));
        for (var i = 0; i < Gelaende.HuegelHoechstens; i++)
        {
            Assert.NotNull(p.AussenAendern("Huegel", a => AussenBearbeitung.Setzen(a, AussenArt.Huegel, 3 + (i % 8) * 4.5, 4 + (i / 8) * 6)));
        }
        Assert.True(AussenBearbeitung.HuegelVoll(p.AussenStand));
        Assert.Null(p.AussenAendern("Huegel17", a => AussenBearbeitung.Setzen(a, AussenArt.Huegel, 20, 25)));
        Assert.Null(p.AussenAendern("Kopie", a => AussenBearbeitung.Kopieren(a, new AussenStelle(AussenArt.Huegel, 0))));
        var e = p.AussenAendern("Gruppe", a => AussenBearbeitung.Kopieren(a, [new AussenStelle(AussenArt.Huegel, 0), new AussenStelle(AussenArt.Huegel, 1)]));
        Assert.Equal(2, e.Uebersprungen);
        Assert.Equal(Gelaende.HuegelHoechstens, AussenBearbeitung.HuegelListe(p.AussenStand).Count);
        Assert.Contains("16", Texte.HuegelGenug(Gelaende.HuegelHoechstens), StringComparison.Ordinal);
        // einer weg: wieder frei
        p.AussenAendern("Weg", a => AussenBearbeitung.Loeschen(a, new AussenStelle(AussenArt.Huegel, 0)));
        Assert.False(AussenBearbeitung.HuegelVoll(p.AussenStand));
        // Gegenprobe Stand S 0.50.0: der 17. Huegel wurde gesetzt (und erst die Pruefung meldete rot)
        AussenBearbeitung.GelaendeAuswahlAltFuerTests = true;
        try
        {
            Assert.NotNull(p.AussenAendern("16", a => AussenBearbeitung.Setzen(a, AussenArt.Huegel, 20, 25)));
            Assert.NotNull(p.AussenAendern("17", a => AussenBearbeitung.Setzen(a, AussenArt.Huegel, 30, 25)));
            Assert.Equal(Gelaende.HuegelHoechstens + 1, AussenBearbeitung.HuegelListe(p.AussenStand).Count);
        }
        finally
        {
            AussenBearbeitung.GelaendeAuswahlAltFuerTests = false;
        }
    }

    [Fact]
    public void GemalterPunkt_HatBeschreibungUndUmriss()
    {
        var p = Grundlage();
        var s = new AussenStelle(AussenArt.Hoehe, 0);
        Assert.True(AussenBearbeitung.Gueltig(p.AussenStand, s));
        Assert.Contains(Texte.GemalteHoehen, AussenBearbeitung.Beschreibung(p.AussenStand, s, o => o), StringComparison.Ordinal);
        Assert.NotEmpty(Bauen3D.Umriss(p.AussenStand, s));
        Assert.False(AussenBearbeitung.Gueltig(p.AussenStand, new AussenStelle(AussenArt.Hoehe, 9999)));
    }
}
