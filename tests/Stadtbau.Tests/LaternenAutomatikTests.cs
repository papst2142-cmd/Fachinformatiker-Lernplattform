using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.26.0 (Weisung P12-115, Meldung P12-75 Abschnitt 2, Sammelliste 253): automatische Laternen im Werkzeug. Nach jeder
/// Aenderung einer Strasse stehen an ihr Laternen alle <see cref="LaternenAutomatik.Abstand"/> abwechselnd links und rechts;
/// selbst gesetzte bleiben, geloeschte automatische bleiben weg, Knopf „Laternen neu verteilen“. Grundlage: leere Stadt
/// 32 × 24, gerade Strasse (2; 8)–(30; 8) mit Gehweg.
/// </summary>
public class LaternenAutomatikTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Projekt Leer() => Projekt.Neu("Laternen", D, Daten.NeueAblage(), leer: (32, 24));

    private static PyObjekt Strasse(double x1, double y1, double x2, double y2) => new()
    {
        { "typ", "strasse" },
        { "punkte", new List<object?> { new List<object?> { x1, y1 }, new List<object?> { x2, y2 } } },
        { "gehweg", 0.35 },
    };

    private static Projekt MitStrasse()
    {
        var p = Leer();
        p.AussenAendern("Straße", a => Aussen.Landschaft(a).Add(Strasse(2, 8, 30, 8)));
        return p;
    }

    private static List<Vector2> Laternen(Projekt p) => [.. Aussen.LaternenWerte(p.AussenStand).Select(l => new Vector2(l.X, l.Z))];

    private static List<StadtbauFund> Rot(Projekt p) => [.. Projekt.PruefenAussen(p.Stand(), D, Aussen.Bauten(p.AussenStand)).Where(f => f.Rot)];

    [Fact]
    public void NeueStrasse_LaternenAlle3Komma5_AbwechselndLinksUndRechts_OhneRotenFund()
    {
        var p = MitStrasse();
        var l = Laternen(p).OrderBy(q => q.X).ToList();
        // 28 lang: Stellen 1,75 + k · 3,5 bis 26,25 = 8 Laternen
        Assert.Equal(8, l.Count);
        for (var i = 0; i < l.Count; i++)
        {
            Assert.Equal(2 + 1.75f + (i * LaternenAutomatik.Abstand), l[i].X, 2);
            Assert.Equal(8 + ((i % 2 == 0 ? 1 : -1) * Nachtlicht.Rand), l[i].Y, 2);
            Assert.True(LaternenAutomatik.IstAutomatisch(p.AussenStand, Aussen.LaternenWerte(p.AussenStand).FindIndex(w => w.X == l[i].X && w.Z == l[i].Y)));
        }
        Assert.Equal(8, LaternenAutomatik.Gesetzt(p.AussenStand).Count);
        Assert.Empty(Rot(p));
        // ein Schritt: Strasse und Laternen zusammen zurueck
        Assert.Single(p.Verlauf);
        p.Rueckgaengig();
        Assert.Empty(Laternen(p));
        // danach (Teil leer) wieder eine Strasse: wieder 8 (Fund Start-Test: leerer Teil ohne Listen)
        p.AussenAendern("Straße", a => Aussen.Landschaft(a).Add(Strasse(2, 8, 30, 8)));
        Assert.Equal(8, Laternen(p).Count);
        p.AussenAendern("Löschen", a => AussenBearbeitung.Loeschen(a, new AussenStelle(AussenArt.Laterne, 0)));
        Assert.Single(LaternenAutomatik.Weg(p.AussenStand));
    }

    [Fact]
    public void Gegenprobe_OhneAutomatik_KeineLaternen()
    {
        LaternenAutomatik.AusFuerTests = true;
        try
        {
            Assert.Empty(Laternen(MitStrasse()));
        }
        finally
        {
            LaternenAutomatik.AusFuerTests = false;
        }
    }

    [Fact]
    public void GeloeschteLaterne_BleibtWeg_AuchNachStrassenAenderungUndNeuVerteilen()
    {
        var p = MitStrasse();
        var nr = Aussen.LaternenWerte(p.AussenStand).FindIndex(w => Math.Abs(w.X - 10.75f) < 0.01f);
        Assert.True(nr >= 0);
        p.AussenAendern("Löschen", a => AussenBearbeitung.Loeschen(a, new AussenStelle(AussenArt.Laterne, nr)));
        Assert.Equal(7, Laternen(p).Count);
        Assert.Single(LaternenAutomatik.Weg(p.AussenStand));
        // die Strasse wird laenger: an der Stelle kommt keine wieder
        p.AussenAendern("Länger", a => Py.Setzen(Objekt(Aussen.Landschaft(a)[^1]), "punkte",
            new List<object?> { new List<object?> { 2.0, 8.0 }, new List<object?> { 31.0, 8.0 } }));
        Assert.DoesNotContain(Laternen(p), q => Math.Abs(q.X - 10.75f) < LaternenAutomatik.Abstand / 2);
        // Knopf „Laternen neu verteilen“: alle automatischen neu, die geloeschte bleibt weg
        var (dazu, weg) = p.LaternenVerteilen();
        Assert.Equal(weg, dazu);
        Assert.DoesNotContain(Laternen(p), q => Math.Abs(q.X - 10.75f) < LaternenAutomatik.Abstand / 2);
        Assert.Equal(7, Laternen(p).Count);
        // Gegenprobe: in einem neuen Projekt ohne Loeschen steht dort eine
        Assert.Contains(Laternen(MitStrasse()), q => Math.Abs(q.X - 10.75f) < 0.01f);
    }

    [Fact]
    public void SelbstGesetzteLaterne_Bleibt_AutomatischeHaltenAbstand()
    {
        var p = Leer();
        p.AussenAendern("Straße", a => Aussen.Landschaft(a).Add(Strasse(2, 8, 30, 8)));
        // eine Laterne von Hand dazu, dann neu verteilen
        p.AussenAendern("Laterne", a => Aussen.Laternen(a).Add(new List<object?> { 12.0, 8.5, false }));
        var (_, _) = p.LaternenVerteilen();
        var l = Laternen(p);
        Assert.Contains(l, q => q == new Vector2(12, 8.5f));
        Assert.All(l.Where(q => q != new Vector2(12, 8.5f)), q => Assert.True(Vector2.Distance(q, new Vector2(12, 8.5f)) >= LaternenAutomatik.ZuHand - 0.001f, $"{q}"));
        Assert.False(LaternenAutomatik.IstAutomatisch(p.AussenStand, Aussen.LaternenWerte(p.AussenStand).FindIndex(w => w.X == 12 && w.Z == 8.5f)));
        // verschoben ist eine automatische danach eine selbst gesetzte: sie bleibt beim Neuverteilen stehen
        var nr = Aussen.LaternenWerte(p.AussenStand).FindIndex(w => Math.Abs(w.X - 3.75f) < 0.01f);
        p.AussenAendern("Schieben", a => AussenBearbeitung.Verschieben(a, new AussenStelle(AussenArt.Laterne, nr), 0.5, 0, false));
        p.LaternenVerteilen();
        Assert.Contains(Laternen(p), q => Math.Abs(q.X - 4.25f) < 0.01f);
        Assert.Empty(Rot(p));
    }

    [Fact]
    public void Kreuzung_KeineLaterneNaeherAls0Komma8()
    {
        var p = MitStrasse();
        p.AussenAendern("Quer", a => Aussen.Landschaft(a).Add(Strasse(16, 2, 16, 20)));
        Assert.All(Laternen(p), q => Assert.True(Vector2.Distance(q, new Vector2(16, 8)) >= LaternenAutomatik.ZuKreuzung, $"{q}"));
        Assert.Contains(Laternen(p), q => Math.Abs(q.X - 16) < 0.6f);
        Assert.Empty(Rot(p));
    }

    [Fact]
    public void Zebrastreifen_LaterneWeichtAus_OhneRotenFund()
    {
        var p = MitStrasse();
        // die Laterne bei x 10,75 steht dort, wo der Zebrastreifen hinkommt
        Assert.Contains(Laternen(p), q => Math.Abs(q.X - 10.75f) < 0.01f);
        p.AussenAendern("Zebra", a => AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 10.75, 8));
        Assert.DoesNotContain(Laternen(p), q => Math.Abs(q.X - 10.75f) <= (Zebrastreifen.Breite / 2) + StadtbauPruefung.ZebraFreiLaengs);
        Assert.Equal(8, Laternen(p).Count);
        Assert.Empty(Rot(p));
        // Gegenprobe: ohne Automatik bleibt sie stehen und die Pruefung ist rot
        var g = MitStrasse();
        LaternenAutomatik.AusFuerTests = true;
        try
        {
            g.AussenAendern("Zebra", a => AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 10.75, 8));
        }
        finally
        {
            LaternenAutomatik.AusFuerTests = false;
        }
        Assert.Contains(Rot(g), f => f.Text.Contains("steht Laterne", StringComparison.Ordinal));
    }

    [Fact]
    public void StrasseGeloescht_IhreLaternenFallenWeg_AndereBleiben()
    {
        var p = MitStrasse();
        p.AussenAendern("Zweite", a => Aussen.Landschaft(a).Add(Strasse(2, 18, 30, 18)));
        Assert.Equal(16, Laternen(p).Count);
        var nr = Aussen.Landschaft(p.AussenStand).Count - 1;
        p.AussenAendern("Löschen", a => AussenBearbeitung.Loeschen(a, new AussenStelle(AussenArt.Strasse, nr)));
        Assert.Equal(8, Laternen(p).Count);
        Assert.All(Laternen(p), q => Assert.True(Math.Abs(q.Y - 8) < 0.6f));
        Assert.Empty(LaternenAutomatik.Weg(p.AussenStand));
    }

    [Fact]
    public void Projektdatei_MerktAutomatischeUndGeloeschte()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Merken", D, ablage, leer: (32, 24));
        p.AussenAendern("Straße", a => Aussen.Landschaft(a).Add(Strasse(2, 8, 30, 8)));
        p.AussenAendern("Löschen", a => AussenBearbeitung.Loeschen(a, new AussenStelle(AussenArt.Laterne, 0)));
        var wieder = Projekt.Oeffnen(p.Datei, D, ablage);
        Assert.Equal(LaternenAutomatik.Gesetzt(p.AussenStand), LaternenAutomatik.Gesetzt(wieder.AussenStand));
        Assert.Equal(LaternenAutomatik.Weg(p.AussenStand), LaternenAutomatik.Weg(wieder.AussenStand));
        Assert.Single(LaternenAutomatik.Weg(wieder.AussenStand));
        // nie in der Stadt-Datei (das Spiel kennt nur die Laternen)
        Assert.DoesNotContain(LaternenAutomatik.Teil, PythonJson.Standard(p.Stand().Stadtdaten), StringComparison.Ordinal);
    }

    [Fact]
    public void Stadt1_OhneAenderung_KeineAutomatischen_Fassung0Unveraendert()
    {
        var p = Projekt.Neu("Stadt 1", D, Daten.NeueAblage());
        Assert.Empty(LaternenAutomatik.Gesetzt(p.AussenStand));
        // ein Baum gesetzt: die 22 Laternen der Fassung 0 bleiben genau so
        var vorher = PythonJson.Standard(Aussen.Laternen(p.AussenStand));
        p.AussenAendern("Baum", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, 22, 2));
        Assert.Equal(vorher, PythonJson.Standard(Aussen.Laternen(p.AussenStand)));
    }

    [Fact]
    public void Kreisverkehr_RingLaternenBleibenSelbstGesetzt_ZufahrtenOhneRotenFund()
    {
        var p = Leer();
        var ring = p.AussenAendern("Kreis", a => AussenBearbeitung.Setzen(a, AussenArt.Kreisverkehr, 16, 12, true, AussenBearbeitung.Kennung(2)));
        Assert.NotNull(ring);
        var gesetzt = LaternenAutomatik.Gesetzt(p.AussenStand);
        // die vier Laternen am Ring setzt der Kreisverkehr selbst (nicht automatisch); an den Zufahrten haelt die Automatik
        // Abstand zum Ring
        Assert.Equal(4 + gesetzt.Count, Laternen(p).Count);
        Assert.All(gesetzt, g => Assert.True(Vector2.Distance(g, new Vector2(16, 12)) >= 2 + (StadtSzene.StrassenBreite / 2) + LaternenAutomatik.ZuKreuzung, $"{g}"));
        Assert.Empty(Rot(p));
    }

    [Fact]
    public void Beschreibung_NenntAutomatisch()
    {
        var p = MitStrasse();
        Assert.Contains(Texte.LaterneAutomatischZusatz, AussenBearbeitung.Beschreibung(p.AussenStand, new AussenStelle(AussenArt.Laterne, 0), x => x), StringComparison.Ordinal);
        p.AussenAendern("Laterne", a => Aussen.Laternen(a).Add(new List<object?> { 12.0, 14.0, false }));
        Assert.DoesNotContain(Texte.LaterneAutomatischZusatz, AussenBearbeitung.Beschreibung(p.AussenStand, new AussenStelle(AussenArt.Laterne, 8), x => x), StringComparison.Ordinal);
    }

    [Fact]
    public void Greifen_AufDerFahrbahnDieStrasse_DanebenDieLaterne()
    {
        var p = MitStrasse();
        // Laterne bei (3,75; 8,5); weit greifen wie in 3D aus der Ferne (0,6)
        var a = p.AussenStand;
        Assert.Equal(AussenArt.Strasse, AussenBearbeitung.StueckBei(a, 3.75, 8.1, 0.6)?.Art);
        Assert.Equal(AussenArt.Laterne, AussenBearbeitung.StueckBei(a, 3.75, 8.55, 0.6)?.Art);
        // Gegenprobe: ganz nah an der Laterne (bis 0,2) greift sie auch vom Rand der Fahrbahn aus
        Assert.Equal(AussenArt.Laterne, AussenBearbeitung.StueckBei(a, 3.75, 8.34, 0.6)?.Art);
    }
}
