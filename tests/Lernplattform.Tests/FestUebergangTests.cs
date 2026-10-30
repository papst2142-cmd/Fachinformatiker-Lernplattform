using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Patch A 0.29.1 (Weisung P10-3, Nico 30.10.2026 "Nach und nach, unsichtbar"): laeuft die Uhr in der Stadt ueber die
/// Daemmerung, tauschen Autos und Festgestalten einzeln, ohne Hausbesuch. Kein Tausch eines Fahrzeugs im Sichtkegel
/// (ausser am Rundlauf am Kartenrand), nach der Hoechstzeit ist der neue Satz ganz da, in jedem Zwischenstand gelten
/// Zaehlung und Abstaende. Nur Darstellung: die Fingerabdruecke pruefen die bestehenden Tests.
/// </summary>
public sealed class FestUebergangTests
{
    private static GrafikBibliothek Grafik => SpielGrafikTests.Bibliothek.Value;

    private static StadtSzene Szene(string? fest, Jahreszeit j) =>
        StadtSzene.Bauen(new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value), null, Grafik, fest, FestDekoTests.Daten.Value, j);

    /// <summary>Je Fest: Jahreszeit, Satz vor und nach der Daemmerung (hin und zurueck wird je Richtung geprueft).</summary>
    public static TheoryData<string, Jahreszeit, bool> Faelle => new()
    {
        { Festzeit.Halloween, Jahreszeit.Herbst, true },
        { Festzeit.Halloween, Jahreszeit.Herbst, false },
        { Festzeit.Weihnachten, Jahreszeit.Winter, true },
        { Festzeit.Weihnachten, Jahreszeit.Winter, false },
        { Festzeit.Ostern, Jahreszeit.Fruehling, true },
        { Festzeit.Ostern, Jahreszeit.Fruehling, false },
    };

    /// <summary>Saetze kurz vor und kurz nach der Daemmerung (nach der Regel <see cref="FestVerkehr.Fuer"/>).</summary>
    private static (FestVerkehr.Satz Vorher, FestVerkehr.Satz Nachher) UmDieDaemmerung(string fest, Jahreszeit j)
    {
        var ab = Tageslicht.DaemmerungAb(j);
        return (FestVerkehr.Fuer(fest, ab - 1, j), FestVerkehr.Fuer(fest, ab, j));
    }

    private static FestUebergang Beginnen(StadtSzene szene, FestVerkehr.Satz alt, FestVerkehr.Satz neu, double vorlauf)
    {
        var v = FestVerkehr.VerkehrFuer(szene, alt);
        v.Probe(vorlauf, -100, -100);
        return new FestUebergang(alt, v, neu, FestVerkehr.VerkehrFuer(szene, neu));
    }

    /// <summary>Alle Mitten der gerade fahrenden Fahrzeuge und Gestalten (je Strecke mit Satz).</summary>
    private static List<(int Strecke, Vector3 Punkt)> Punkte(FestUebergang u) =>
        [.. u.Verkehr.Autos.SelectMany(a => FestUebergang.Punkte(a, u.AltVerkehr.Autos.Contains(a) ? u.Alt : u.Neu, a.Weg).Select(p => (a.Nummer, p)))];

    [Theory]
    [MemberData(nameof(Faelle))]
    public void UhrUeberDieDaemmerungInDerGesamtansicht(string fest, Jahreszeit j, bool hin)
    {
        // Gesamtansicht: alles liegt im Bild, getauscht wird nur am Rundlauf oder bei der Einfahrt am Kartenrand
        var szene = Szene(fest, j);
        var (vorher, nachher) = UmDieDaemmerung(fest, j);
        Assert.NotEqual(vorher, nachher);
        var (alt, neu) = hin ? (vorher, nachher) : (nachher, vorher);
        var u = Beginnen(szene, alt, neu, 7.3);
        var schritte = 0;
        var kleinste = float.MaxValue;
        while (!u.Fertig)
        {
            u.Schritt(1.0 / 60, -100, -100, (_, _) => true);
            schritte++;
            Assert.True(u.Verkehr.Autos.Count <= 16, "hoechstens 16 Fahrzeuge");
            // je Strecke hoechstens ein altes und ein neues Fahrzeug, Reihenfolge nach Strecken (Vorrang an Kreuzungen)
            Assert.All(u.Verkehr.Autos.GroupBy(a => a.Nummer), g => Assert.True(g.Count(a => u.AltVerkehr.Autos.Contains(a)) <= 1 && g.Count(a => u.NeuVerkehr.Autos.Contains(a)) <= 1));
            Assert.Equal(u.Verkehr.Autos.Select(a => a.Nummer).Order(), u.Verkehr.Autos.Select(a => a.Nummer));
            kleinste = MathF.Min(kleinste, KleinsterAbstand(Punkte(u)));
            Assert.True(schritte <= (FestUebergang.Hoechstzeit * 60) + 1, "Hoechstzeit ueberschritten");
        }
        // alte gehen nur am Ende ihrer Strecke hinaus, neue kommen nur am Anfang ihrer Strecke herein (Kartenrand)
        Assert.True(u.Tausche.All(t => t.Grund == (t.Neu ? FestUebergang.Grund.Einfahrt : FestUebergang.Grund.Rundlauf)), string.Join("; ", u.Tausche));
        // fertig: genau der neue Satz mit allen Plaetzen, der Verkehr des neuen Satzes
        Assert.Equal(u.NeuVerkehr.Autos, u.Verkehr.Autos);
        var soll = FestVerkehr.VerkehrFuer(szene, neu).Autos.Select(a => a.Nummer);
        Assert.Equal(soll, u.NeuVerkehr.Autos.Select(a => a.Nummer));
        if (neu != FestVerkehr.Satz.Autos)
        {
            Assert.Equal(FestVerkehr.Plaetze(neu).Count, FestVerkehr.Gestalten(Grafik, neu).Count);
            Assert.All(FestVerkehr.Gestalten(Grafik, neu), g => Assert.NotNull(FestVerkehr.Lage(g, u.NeuVerkehr)));
        }
        else
        {
            Assert.Equal(6, u.NeuVerkehr.Autos.Count);
        }
        Assert.True(kleinste > 0.05f, string.Create(CultureInfo.InvariantCulture, $"{fest} {alt}->{neu}: kleinster Abstand {kleinste:F3}"));
        TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture,
            $"{fest} {alt}->{neu}: fertig nach {schritte / 60.0:F1} s Fahrzeit, {u.Tausche.Count} Tausche, kleinster Abstand {kleinste:F3}"));
    }

    [Theory]
    [MemberData(nameof(Faelle))]
    public void KeinTauschImSichtkegel(string fest, Jahreszeit j, bool hin)
    {
        // Kamera nah an der Hauptstrasse (wie beim Spielen): ein Teil der Strecken liegt im Bild, ein Teil nicht
        var szene = Szene(fest, j);
        var (vorher, nachher) = UmDieDaemmerung(fest, j);
        var (alt, neu) = hin ? (vorher, nachher) : (nachher, vorher);
        var u = Beginnen(szene, alt, neu, 2.1);
        var kamera = new Kamera(12f, 19f, 9f);
        var sichtProj = kamera.Sicht * kamera.Projektion(1280, 800);
        bool ImBild(Vector3 p, float halb) => FestUebergang.ImSichtkegel(sichtProj, p, halb);
        var ausserhalb = 0;
        while (!u.Fertig)
        {
            var zahl = u.Tausche.Count;
            u.Schritt(1.0 / 60, -100, -100, ImBild);
            foreach (var t in u.Tausche.Skip(zahl))
            {
                var auto = (t.Neu ? u.NeuVerkehr : u.AltVerkehr).Autos.First(a => a.Nummer == t.Strecke);
                var satz = t.Neu ? u.Neu : u.Alt;
                if (t.Grund == FestUebergang.Grund.Ausserhalb)
                {
                    ausserhalb++;
                    // das Fahrzeug liegt beim Tausch ganz ausserhalb des Bildes (Mitte mit 0,5 Rand)
                    Assert.All(FestUebergang.Punkte(auto, satz, auto.Weg), p => Assert.False(ImBild(p, 0.5f), t.ToString()));
                }
                else if (t.Grund == FestUebergang.Grund.Rundlauf)
                {
                    // am Ende der Strecke (Kartenrand), dort faehrt es ohnehin hinaus
                    Assert.False(t.Neu);
                    Assert.True(auto.Weg >= auto.Laenge - 0.5f || auto.Weg < 0.1f, string.Create(CultureInfo.InvariantCulture, $"Rundlauf bei Weg {auto.Weg:F2}"));
                }
                else
                {
                    // Einfahrt am Anfang der Strecke (Kartenrand)
                    Assert.Equal(FestUebergang.Grund.Einfahrt, t.Grund);
                    Assert.True(t.Neu);
                    Assert.True(auto.Weg < 0.1f, string.Create(CultureInfo.InvariantCulture, $"Einfahrt bei Weg {auto.Weg:F2}"));
                }
            }
            Assert.True(u.Fahrzeit <= FestUebergang.Hoechstzeit + 0.02, "Hoechstzeit ueberschritten");
        }
        Assert.DoesNotContain(u.Tausche, t => t.Grund == FestUebergang.Grund.Hoechstzeit);
        Assert.True(ausserhalb > 0, "kein Tausch ausserhalb des Bildes");
        TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture,
            $"{fest} {alt}->{neu}: {ausserhalb} ausserhalb, {u.Tausche.Count} Tausche, fertig nach {u.Fahrzeit:F1} s"));
    }

    [Fact]
    public void NachDerHoechstzeitIstAllesGetauscht()
    {
        // die Figur steht vor dem langsamsten Huhn (Strecke 1 an ihrem Start): es haelt, bis die Hoechstzeit kommt
        var szene = Szene(Festzeit.Ostern, Jahreszeit.Fruehling);
        var u = Beginnen(szene, FestVerkehr.Satz.Ostern, FestVerkehr.Satz.Autos, 0);
        var huhn = u.Verkehr.Autos.First(a => a.Nummer == 1);
        var (vorn, r) = huhn.Lage(huhn.Weg + 0.5f);
        var schritte = 0;
        while (!u.Fertig)
        {
            u.Schritt(1.0 / 60, vorn.X, vorn.Y, (_, _) => true);
            schritte++;
            Assert.True(schritte <= (FestUebergang.Hoechstzeit * 60) + 1, "Hoechstzeit ueberschritten");
        }
        Assert.Contains(u.Tausche, t => t.Grund == FestUebergang.Grund.Hoechstzeit && t.Strecke == 1 && !t.Neu);
        Assert.Equal(u.NeuVerkehr.Autos, u.Verkehr.Autos);
        Assert.Equal(6, u.Verkehr.Autos.Count);
        _ = r;
    }

    [Fact]
    public void StehendeFahrzeugeTauschenNurAusserhalbDesBildes()
    {
        // stehen die Fahrzeuge (Nachlauf vorbei), tauscht nur, was nicht im Bild ist; die Hoechstzeit zaehlt nur Fahrzeit
        var szene = Szene(Festzeit.Halloween, Jahreszeit.Herbst);
        var u = Beginnen(szene, FestVerkehr.Satz.Autos, FestVerkehr.Satz.Halloween, 3);
        for (var i = 0; i < 600; i++)
        {
            u.Schritt(0, -100, -100, (_, _) => true);
        }
        Assert.Empty(u.Tausche);
        Assert.Equal(0, u.Fahrzeit);
        u.Schritt(0, -100, -100, (_, _) => false);
        Assert.True(u.Fertig);
        Assert.All(u.Tausche, t => Assert.Equal(FestUebergang.Grund.Ausserhalb, t.Grund));
    }

    [Fact]
    public void AbschliessenTauschtAllesSofort()
    {
        // Figur geht ins Haus (niemand sieht die Strassen): alle offenen Strecken sofort
        var szene = Szene(Festzeit.Weihnachten, Jahreszeit.Winter);
        var u = Beginnen(szene, FestVerkehr.Satz.Autos, FestVerkehr.Satz.Weihnachten, 4);
        u.Abschliessen();
        Assert.True(u.Fertig);
        Assert.Equal(u.NeuVerkehr.Autos, u.Verkehr.Autos);
        Assert.Equal([2], u.Verkehr.Autos.Select(a => a.Nummer));
        // der Weihnachtsmann kommt von seinem festen Startplatz (wie beim Hinausgehen aus einem Haus)
        Assert.Equal(FestVerkehr.VerkehrFuer(szene, FestVerkehr.Satz.Weihnachten).Autos[0].Weg, u.Verkehr.Autos[0].Weg, 3);
        Assert.All(u.Tausche, t => Assert.Equal(FestUebergang.Grund.Sofort, t.Grund));
    }

    [Fact]
    public void SichtkegelPasstZumBildpunkt()
    {
        var kamera = new Kamera(16f, 16f, 12f) { Drehung = 30f };
        var sichtProj = kamera.Sicht * kamera.Projektion(1280, 800);
        var innen = 0;
        for (var x = -10f; x <= 42f; x += 1.3f)
        {
            for (var z = -10f; z <= 42f; z += 1.3f)
            {
                var p = new Vector3(x, 0.2f, z);
                var bp = kamera.Bildpunkt(p, 1280, 800);
                var imBild = bp is { } b && b.X >= 0 && b.X <= 1280 && b.Y >= 0 && b.Y <= 800;
                if (imBild)
                {
                    innen++;
                }
                // bis auf Punkte am Bildrand (2 Bildpunkte) gleich wie die Bildpunkte der Kamera
                var amRand = bp is { } c && MathF.Abs(MathF.Min(MathF.Min(c.X, 1280 - c.X), MathF.Min(c.Y, 800 - c.Y))) < 2f;
                if (!amRand)
                {
                    Assert.Equal(imBild, FestUebergang.ImSichtkegel(sichtProj, p, 0f));
                }
                // mit Rand gilt jeder Punkt im Bild erst recht als im Bild
                if (imBild)
                {
                    Assert.True(FestUebergang.ImSichtkegel(sichtProj, p, FestUebergang.Rand));
                }
            }
        }
        Assert.InRange(innen, 50, 2000);
    }

    /// <summary>Kleinster Abstand zweier Mitten verschiedener Strecken.</summary>
    private static float KleinsterAbstand(List<(int Strecke, Vector3 Punkt)> punkte)
    {
        var kleinste = float.MaxValue;
        for (var i = 0; i < punkte.Count; i++)
        {
            for (var k = i + 1; k < punkte.Count; k++)
            {
                if (punkte[i].Strecke != punkte[k].Strecke)
                {
                    kleinste = MathF.Min(kleinste, Vector3.Distance(punkte[i].Punkt, punkte[k].Punkt));
                }
            }
        }
        return kleinste;
    }
}
