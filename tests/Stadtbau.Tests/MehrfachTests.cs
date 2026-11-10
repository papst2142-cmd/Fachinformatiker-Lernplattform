using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle W-B S 0.30.0 (Weisung P12-136, Kurzplan P12-79 Wunsch 1): mehrere Stuecke zusammen schieben, drehen, kopieren und
/// loeschen. Jede Aktion ist EIN Schritt im Verlauf (ein Rueckgaengig stellt alles wieder her); Gegenproben zeigen, dass
/// einzelne Schritte je Stueck anders waeren und dass mitgenommene Teile (Zufahrten, Laternen eines Kreisverkehrs) nicht
/// doppelt bewegt werden.
/// </summary>
public class MehrfachTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Projekt Leer() => Projekt.Neu("Mehrfach", D, Daten.NeueAblage(), leer: (32, 24));

    private static AussenStelle Setzen(Projekt p, AussenArt art, double x, double y, string? modell = null) =>
        p.AussenAendern("Setzen", a => AussenBearbeitung.Setzen(a, art, x, y, modell: modell))!;

    private static long KatDreh(Projekt p, AussenStelle s) => Py.Int(Py.Get(Aussen.KatalogStuecke(p.AussenStand)[s.Nummer], "dreh") ?? 0L);

    private static (double X, double Y) Lage(Projekt p, AussenStelle s) => AussenBearbeitung.Lage(p.AussenStand, s);

    [Fact]
    public void Verschieben_DreiStuecke_EinSchritt_EinRueckgaengigStelltAllesHer()
    {
        var p = Leer();
        var b1 = Setzen(p, AussenArt.Baum, 4, 4);
        var b2 = Setzen(p, AussenArt.Baum, 6, 4);
        var auto = Setzen(p, AussenArt.Katalog, 8, 8, "car_kit/sedan");
        var vorher = (Lage(p, b1), Lage(p, b2), Lage(p, auto));
        var schritte = p.SchritteZurueck;

        var e = p.AussenAendern("Gruppe", a => AussenBearbeitung.Verschieben(a, [b1, b2, auto], 2, 1));
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        Assert.Equal(3, e.Erledigt);
        Assert.Equal(3, e.Auswahl.Count);
        Assert.Equal((6.0, 5.0), Lage(p, b1));
        Assert.Equal((8.0, 5.0), Lage(p, b2));
        Assert.Equal((vorher.Item3.X + 2, vorher.Item3.Y + 1), Lage(p, auto));

        Assert.True(p.Rueckgaengig());
        Assert.Equal(vorher, (Lage(p, b1), Lage(p, b2), Lage(p, auto)));

        // Gegenprobe: einzeln geschoben waeren es drei Schritte, ein Rueckgaengig nimmt nur den letzten zurueck
        foreach (var s in new[] { b1, b2, auto })
        {
            p.AussenAendern("Einzeln", a => { AussenBearbeitung.Verschieben(a, s, 2, 1); return 0; });
        }
        Assert.Equal(schritte + 3, p.SchritteZurueck);
        Assert.True(p.Rueckgaengig());
        Assert.Equal((6.0, 5.0), Lage(p, b1));
        Assert.Equal(vorher.Item3, Lage(p, auto));
    }

    [Fact]
    public void Verschieben_OhneEinrasten_AufDasRaster()
    {
        var p = Leer();
        var b1 = Setzen(p, AussenArt.Baum, 4, 4);
        var b2 = Setzen(p, AussenArt.Baum, 6, 4);
        // 0,7 wird zu 0,5 (Raster an), 0,24 zu 0 (kein Schritt)
        p.AussenAendern("Gruppe", a => AussenBearbeitung.Verschieben(a, [b1, b2], 0.7, 0.24));
        Assert.Equal((4.5, 4.0), Lage(p, b1));
        var schritte = p.SchritteZurueck;
        var e = p.AussenAendern("Gruppe", a => AussenBearbeitung.Verschieben(a, [b1, b2], 0.2, 0.2));
        Assert.Equal(0, e.Erledigt);
        Assert.Equal(schritte, p.SchritteZurueck);
        // fein (Raster aus): 0,1
        p.AussenAendern("Gruppe", a => AussenBearbeitung.Verschieben(a, [b1, b2], 0.13, 0, raster: false));
        Assert.Equal((4.6, 4.0), Lage(p, b1));
        Assert.Equal((6.6, 4.0), Lage(p, b2));
    }

    [Fact]
    public void Drehen_UmGemeinsameMitte_EinSchritt_KatalogStueckDrehtMit()
    {
        var p = Leer();
        var b1 = Setzen(p, AussenArt.Baum, 4, 4);
        var b2 = Setzen(p, AussenArt.Baum, 8, 4);
        var schritte = p.SchritteZurueck;
        p.AussenAendern("Drehen", a => AussenBearbeitung.Drehen(a, [b1, b2]));
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        // Mitte (6, 4); im Uhrzeigersinn von oben: links wird oben, rechts wird unten
        Assert.Equal((6.0, 2.0), Lage(p, b1));
        Assert.Equal((6.0, 6.0), Lage(p, b2));
        p.AussenAendern("Zurueck", a => AussenBearbeitung.Drehen(a, [b1, b2], rueckwaerts: true));
        Assert.Equal((4.0, 4.0), Lage(p, b1));
        Assert.Equal((8.0, 4.0), Lage(p, b2));

        var auto = Setzen(p, AussenArt.Katalog, 12, 12, "car_kit/sedan");
        var b3 = Setzen(p, AussenArt.Baum, 16, 12);
        var drehVorher = KatDreh(p, auto);
        p.AussenAendern("Drehen", a => AussenBearbeitung.Drehen(a, [auto, b3]));
        var drehNachher = KatDreh(p, auto);
        Assert.Equal((drehVorher + 1) % 4, drehNachher % 4);
        Assert.True(p.Rueckgaengig());
        Assert.Equal(drehVorher, KatDreh(p, auto));
    }

    [Fact]
    public void Kopieren_AlleUmDenselbenVersatz_KopienSindDieNeueAuswahl()
    {
        var p = Leer();
        var b1 = Setzen(p, AussenArt.Baum, 4, 4);
        var b2 = Setzen(p, AussenArt.Baum, 6, 5);
        var schritte = p.SchritteZurueck;
        var e = p.AussenAendern("Kopie", a => AussenBearbeitung.Kopieren(a, [b1, b2]));
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        Assert.Equal(2, e.Auswahl.Count);
        Assert.DoesNotContain(b1, e.Auswahl);
        var (k1, k2) = (Lage(p, e.Auswahl[0]), Lage(p, e.Auswahl[1]));
        var versatz = k1.X - 4;
        Assert.True(versatz > 2, "die Kopie liegt rechts neben der ganzen Gruppe");
        Assert.Equal(0, versatz % Bearbeitung.Raster, 6);
        Assert.Equal((4 + versatz, 4.0), k1);
        Assert.Equal((6 + versatz, 5.0), k2);
        // die Originale bleiben
        Assert.Equal((4.0, 4.0), Lage(p, b1));
        Assert.True(p.Rueckgaengig());
        Assert.Equal(2, AussenBearbeitung.Alle(p.AussenStand).Count(z => z.Stelle.Art == AussenArt.Baum));
    }

    [Fact]
    public void Loeschen_EinSchritt_HausMitOrtBleibtUndZaehltAlsUebersprungen()
    {
        var p = Projekt.Neu("Mehrfach Stadt 1", D, Daten.NeueAblage());
        var haus = AussenBearbeitung.Alle(p.AussenStand).First(z => z.Stelle.Art == AussenArt.Haus).Stelle;
        var baeume = AussenBearbeitung.Alle(p.AussenStand).Where(z => z.Stelle.Art == AussenArt.Baum).Take(3).Select(z => z.Stelle).ToList();
        Assert.Equal(3, baeume.Count);
        var zahl = AussenBearbeitung.Alle(p.AussenStand).Count(z => z.Stelle.Art == AussenArt.Baum);
        var schritte = p.SchritteZurueck;
        // drei Baeume aus derselben Liste: nach dem ersten verschieben sich die Nummern, trotzdem gehen genau diese drei
        var lagen = baeume.Select(b => Lage(p, b)).ToList();
        var e = p.AussenAendern("Löschen", a => AussenBearbeitung.Loeschen(a, [.. baeume, haus]));
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        Assert.Equal(3, e.Erledigt);
        Assert.Equal(1, e.Uebersprungen);
        Assert.True(AussenBearbeitung.Gueltig(p.AussenStand, haus));
        var rest = AussenBearbeitung.Alle(p.AussenStand).Where(z => z.Stelle.Art == AussenArt.Baum).Select(z => (z.X, z.Y)).ToList();
        Assert.Equal(zahl - 3, rest.Count);
        Assert.All(lagen, l => Assert.DoesNotContain(l, rest));
        Assert.True(p.Rueckgaengig());
        Assert.Equal(zahl, AussenBearbeitung.Alle(p.AussenStand).Count(z => z.Stelle.Art == AussenArt.Baum));
    }

    [Fact]
    public void Kreisverkehr_ZufahrtenUndLaternenGehenMit_NieDoppelt()
    {
        var p = Leer();
        var ring = Setzen(p, AussenArt.Kreisverkehr, 16, 12, AussenBearbeitung.Kennung(3));
        var a0 = p.AussenStand;
        var strassen = AussenBearbeitung.Alle(a0).Where(z => z.Stelle.Art == AussenArt.Strasse && z.Stelle != ring).ToList();
        var laternen = AussenBearbeitung.Alle(a0).Where(z => z.Stelle.Art == AussenArt.Laterne && !LaternenAutomatik.IstAutomatisch(a0, z.Stelle.Nummer)).ToList();
        Assert.Equal(4, strassen.Count);
        Assert.Equal(4, laternen.Count);
        var zufahrt = strassen[0];
        // Ring UND eine seiner Zufahrten gewaehlt: die Zufahrt geht nur einmal mit (sonst 4 statt 2 weiter)
        p.AussenAendern("Gruppe", a => AussenBearbeitung.Verschieben(a, [ring, zufahrt.Stelle], 2, 0));
        var nachher = AussenBearbeitung.Alle(p.AussenStand).Where(z => z.Stelle.Art == AussenArt.Strasse && z.Stelle != ring).ToList();
        Assert.All(strassen.Zip(nachher), t => Assert.Equal(t.First.X + 2, t.Second.X, 6));
        var lnachher = AussenBearbeitung.Alle(p.AussenStand).Where(z => z.Stelle.Art == AussenArt.Laterne && !LaternenAutomatik.IstAutomatisch(p.AussenStand, z.Stelle.Nummer)).ToList();
        Assert.All(laternen.Zip(lnachher), t => Assert.Equal(t.First.X + 2, t.Second.X, 6));
        Assert.Single(AussenBearbeitung.OhneMitgenommene(p.AussenStand, [ring, zufahrt.Stelle]));

        // Gegenprobe: ohne „OhneMitgenommene“ (jedes Stueck einzeln genau verschoben) liefe die Zufahrt doppelt
        var q = Leer();
        var ring2 = Setzen(q, AussenArt.Kreisverkehr, 16, 12, AussenBearbeitung.Kennung(3));
        var z2 = AussenBearbeitung.Alle(q.AussenStand).First(z => z.Stelle.Art == AussenArt.Strasse && z.Stelle != ring2);
        q.AussenAendern("Doppelt", a =>
        {
            AussenBearbeitung.Verschieben(a, [ring2], 2, 0);
            AussenBearbeitung.Verschieben(a, [z2.Stelle], 2, 0);
            return 0;
        });
        var doppelt = AussenBearbeitung.Alle(q.AussenStand).First(z => z.Stelle == z2.Stelle);
        Assert.Equal(z2.X + 4, doppelt.X, 6);
    }

    [Fact]
    public void Rahmen_WaehltNurWasDrinLiegt_OhneAusgeblendeteArten()
    {
        var p = Leer();
        var b1 = Setzen(p, AussenArt.Baum, 4, 4);
        var b2 = Setzen(p, AussenArt.Baum, 6, 5);
        Setzen(p, AussenArt.Baum, 20, 20);
        var auto = Setzen(p, AussenArt.Katalog, 5, 6, "car_kit/sedan");
        var drin = AussenBearbeitung.ImRahmen(p.AussenStand, 3, 3, 7, 7);
        Assert.Equal(new HashSet<AussenStelle> { b1, b2, auto }, drin.ToHashSet());
        // eine ausgeblendete Art faengt nicht (Ebenen, S 0.27.0)
        var ohneKatalog = AussenBearbeitung.ImRahmen(p.AussenStand, 7, 7, 3, 3, new HashSet<AussenArt> { AussenArt.Katalog });
        Assert.Equal(new HashSet<AussenStelle> { b1, b2 }, ohneKatalog.ToHashSet());
        // 3D: Rahmen im Bild (hier Bild = Karte mal 10)
        var bild = AussenBearbeitung.ImBildRahmen(p.AussenStand, (x, y) => (x * 10, y * 10), 30, 30, 70, 55);
        Assert.Equal(new HashSet<AussenStelle> { b1, b2 }, bild.ToHashSet());
        // Umschalten: dazu und wieder heraus
        var g = AussenBearbeitung.Umschalten([b1], b2);
        Assert.Equal([b1, b2], g);
        Assert.Equal([b1], AussenBearbeitung.Umschalten(g, b2));
    }

    [Fact]
    public void GruppeMitHaus_DrehtNurInVierteln_TuerUndWegBleibenAmHaus()
    {
        var p = Projekt.Neu("Mehrfach Haus", D, Daten.NeueAblage());
        var haus = AussenBearbeitung.Alle(p.AussenStand).First(z => z.Stelle.Art == AussenArt.Haus).Stelle;
        var baum = AussenBearbeitung.Alle(p.AussenStand).First(z => z.Stelle.Art == AussenArt.Baum).Stelle;
        var e = p.AussenAendern("Drehen", a => AussenBearbeitung.Drehen(a, [haus, baum]));
        Assert.Equal(2, e.Erledigt);
        // Vierteldrehung des Hauses (Spielregel: nur Viertel)
        Assert.Equal(1, AussenBearbeitung.HausDreh(p.AussenStand, haus));
        // der Weg des Hauses beginnt weiter an seiner Tuer (gleiches Ergebnis wie das einzelne Drehen eines Hauses)
        Assert.NotNull(AussenBearbeitung.Zugang(p.AussenStand, haus.Ort));
        Assert.True(p.Rueckgaengig());
        Assert.Equal(0, AussenBearbeitung.HausDreh(p.AussenStand, haus));
    }
}
