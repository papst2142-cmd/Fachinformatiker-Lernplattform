using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Suche;
using Lernplattform.Core.Texte;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.96.0 (Sammelliste 385 Welle 2, Weisung P12-309, Nico Antworten 2a und 4): die zwei Sticks am Handy und Tablet
/// (Engine-Baustein <see cref="Sticks"/>) und der Schalter „Antippen zum Gehen“ in der Karte „Steuerung“ (Weisung P12-308).
/// Jede Regel mit Gegenprobe.
/// </summary>
public sealed class SpielSticksTests
{
    /// <summary>Bildgroessen wie die Layoutpruefung und die Geraete (Handy hoch/quer, Tablet hoch/quer, PC), je mit Kopf und Leiste.</summary>
    public static readonly (string Name, double Breite, double Hoehe)[] Groessen =
    [
        ("Handy hoch", 384, 832), ("Handy quer", 832, 384), ("Tablet hoch", 800, 1280), ("Tablet quer", 1280, 800),
        ("Grenze", 720, 880), ("PC", 1360, 880), ("klein", 320, 568), ("klein quer", 568, 320),
    ];

    private static IEnumerable<Sticks.Bereich> Bereiche()
    {
        foreach (var (_, b, h) in Groessen)
        {
            foreach (var (oben, unten) in new[] { (0.0, 0.0), (60.0, h / 3), (90.0, h / 2), (40.0, h * 0.6), (h / 4, h / 4) })
            {
                yield return new Sticks.Bereich(b, h, oben, unten);
            }
        }
    }

    [Fact]
    public void Stick_Rechnung_Totzone_VollAbDreiViertel_RichtungDesDaumens()
    {
        var sticks = new Sticks();
        var b = new Sticks.Bereich(384, 832, 80, 300);
        Assert.True(sticks.Druecken(1, 100, 500, b));
        var stick = sticks.Bewegen(1, 100, 500 - (0.14 * Sticks.Radius))!;    // ab dem kleinen Weg ein Stick, aber in der Totzone
        Assert.NotNull(stick);
        Assert.Equal((0.0, 0.0), stick.Eingabe);
        sticks.Bewegen(1, 100, 500 - Sticks.Radius);                          // ganz nach oben: volles Tempo vor
        var (vor, seit) = stick.Eingabe;
        Assert.Equal(1.0, vor, 9);
        Assert.Equal(0.0, seit, 9);
        sticks.Bewegen(1, 100 + (0.45 * Sticks.Radius), 500);               // halb zwischen Totzone und drei Vierteln: halbes Tempo rechts
        (vor, seit) = stick.Eingabe;
        Assert.Equal(0.5, seit, 9);
        Assert.Equal(0.0, vor, 9);
        sticks.Bewegen(1, 100 + (5 * Sticks.Radius), 500);                  // weit draussen: Knopf bleibt am Rand, Tempo voll
        Assert.Equal(1.0, stick.Eingabe.Seit, 9);
        Assert.Equal(stick.MitteX + stick.Groesse, stick.Knopf.X, 9);
        Assert.Equal(stick.MitteY, stick.Knopf.Y, 9);
        // die Mitte ist hier nach oben gerueckt (Daumen nah an der Leiste); zurueck an der Stelle des Aufsetzens: nichts
        Assert.True(stick.MitteY < 500);
        sticks.Bewegen(1, 100, 500);
        Assert.Equal((0.0, 0.0), stick.Eingabe);
        Assert.Equal((stick.MitteX, stick.MitteY), stick.Knopf);
        // Gegenprobe: ab der gerueckten Mitte gerechnet ginge die Figur schon ohne Ziehen rueckwaerts
        Assert.NotEqual((0.0, 0.0), Steuerung.AusStick(100 - stick.MitteX, 500 - stick.MitteY, stick.Groesse));
    }

    [Fact]
    public void Tippen_BleibtTippen_ZiehenWirdStick_Gegenprobe()
    {
        var b = new Sticks.Bereich(384, 832, 80, 300);
        var sticks = new Sticks();
        Assert.True(sticks.Druecken(7, 120, 480, b));
        Assert.Null(sticks.Bewegen(7, 120 + (Sticks.AnfangAb / 2), 480));   // kleiner als der Weg: noch ein Tipp
        Assert.False(sticks.Aktiv);
        Assert.False(sticks.Loslassen(7));                                  // Loslassen: kein Stick, die Flaeche tippt
        // Gegenprobe: derselbe Finger ein Stueck weiter wird zum Stick, Loslassen ist dann nie ein Tipp
        Assert.True(sticks.Druecken(7, 120, 480, b));
        Assert.NotNull(sticks.Bewegen(7, 120 + Sticks.AnfangAb + 1, 480));
        Assert.True(sticks.Aktiv);
        Assert.True(sticks.Loslassen(7));
        Assert.False(sticks.Aktiv);
        // zwei Finger zoomen: die Kandidaten werden vergessen, kein Stick
        Assert.True(sticks.Druecken(8, 120, 480, b));
        sticks.KandidatenVergessen();
        Assert.Null(sticks.Bewegen(8, 200, 480));
        // ein Kandidat wird nie zum Stick, waehrend ein zweiter Finger zoomt (allein = false)
        Assert.True(sticks.Druecken(9, 120, 480, b));
        Assert.Null(sticks.Bewegen(9, 200, 480, allein: false));
    }

    [Fact]
    public void ZweiDaumen_LinksGehen_RechtsDrehen_Zugleich_Gegenprobe()
    {
        var b = new Sticks.Bereich(384, 832, 80, 300);
        var sticks = new Sticks();
        Assert.True(sticks.Druecken(1, 80, 450, b));
        Assert.True(sticks.Druecken(2, 300, 450, b));
        // Gegenprobe: ein dritter Daumen auf einer Seite, die schon einen hat, wird kein Stick
        Assert.False(sticks.Druecken(3, 90, 460, b));
        sticks.Bewegen(1, 80, 450 - Sticks.Radius);
        sticks.Bewegen(2, 300 + Sticks.Radius, 450);
        Assert.Equal(Sticks.Seite.Links, sticks.Links!.Art);
        Assert.Equal(Sticks.Seite.Rechts, sticks.Rechts!.Art);
        Assert.Equal(1.0, sticks.Links.Eingabe.Vor, 9);
        // rechter Stick nach rechts: 300 Punkte je Sekunde, seit A 0.97.0 (Weisung P12-313) wie die Maus nach LINKS gezogen,
        // damit der Blick nach rechts dreht (Richtung prueft SpielUmsehenTests)
        var (dx, dy) = sticks.Drehen(0.5);
        Assert.Equal(-150.0, dx, 9);
        Assert.Equal(0.0, dy, 9);
        sticks.Bewegen(2, 300, 450 - Sticks.Radius);
        Assert.True(sticks.Drehen(1).Dy > 0);                                // nach oben = wie die Maus nach unten
        var ich = SpielKamera.Draussen(5, 5, 20, 20);
        ich.IchZiel = 1f;
        ich.IchAnteil = 1f;
        var blick0 = ich.IchNeigung;
        Kamerabedienung.RechteMaus(ich, true, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: sticks.Drehen(0.1).Dy));
        Assert.True(ich.IchNeigung < blick0, $"Blick nicht hoch ({blick0} -> {ich.IchNeigung})");   // in der Ich-Sicht: Blick hoch
        // Gegenprobe: der linke Stick dreht nie, der rechte geht nie
        sticks.Loslassen(2);
        Assert.Equal((0.0, 0.0), sticks.Drehen(1));
        Assert.NotNull(sticks.Links);
        // Kamera: der rechte Stick dreht wie die rechte Maus (gleiche Rechnung wie die Spielflaeche)
        var k = SpielKamera.Draussen(5, 5, 20, 20);
        var vorher = k.Drehung;
        Kamerabedienung.RechteMaus(k, true, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Grad: (float)dx * Kameragriff.GradJePunkt));
        Assert.Equal(300f, ((k.Drehung - vorher) + 360f) % 360f, 3);          // 0,5 s voll = 60 Grad (nach rechts, also -60)
    }

    [Fact]
    public void Sticks_NurUnten_NieUeberKopfOderLeiste_AlleGroessen_Gegenprobe()
    {
        var gepruefte = 0;
        var ohneKlemmeRaus = 0;
        foreach (var b in Bereiche())
        {
            for (var x = 0.0; x <= b.Breite; x += b.Breite / 24)
            {
                for (var y = 0.0; y <= b.Hoehe; y += b.Hoehe / 40)
                {
                    var seite = Sticks.SeiteFuer(x, y, b);
                    var unten = y >= Sticks.ZoneOben(b) && y <= b.Hoehe - b.Unten;
                    // der Bereich ist das untere Drittel, mindestens 2 Radien, hoechstens die untere Haelfte (die Mitte bleibt frei)
                    Assert.True(Sticks.ZoneOben(b) >= b.Oben + (b.FreiHoehe / 2) - 1e-9);
                    if (y < b.Oben + (b.FreiHoehe / 2))
                    {
                        Assert.Null(seite);
                    }
                    if (!unten)
                    {
                        Assert.Null(seite);   // oben, in der Kopfzeile oder in der Leiste nie ein Stick
                        continue;
                    }
                    if (seite is not { } s)
                    {
                        Assert.True(b.FreiHoehe < Sticks.Radius || b.Breite < 2 * Sticks.Radius, $"unten ohne Stick bei {b}");
                        continue;
                    }
                    Assert.Equal(x < b.Breite / 2 ? Sticks.Seite.Links : Sticks.Seite.Rechts, s);
                    var (mx, my, r) = Sticks.Lage(x, y, s, b);
                    Assert.True(r >= Sticks.Radius / 2 && r <= Sticks.Radius, $"Radius {r} bei {b}");
                    Assert.True(my - r >= b.Oben - 1e-9, $"ueber der Kopfzeile: {my - r} < {b.Oben} ({b})");
                    // A 1.0.0 (Sammelliste 405): neben einer mittigen Leiste bis in die Ecke, nie auf der Leiste, nie unter dem Bild
                    var leiste = b.LeisteBis > b.LeisteVon ? (b.LeisteVon, b.Hoehe - b.Unten, b.LeisteBis, b.Hoehe) : (0, b.Hoehe - b.Unten, b.Breite, b.Hoehe);
                    Assert.False(Sticks.KreisInRechteck(mx, my, r, leiste), $"auf der Leiste: ({mx}, {my}, {r}) ({b})");
                    Assert.True(my + r <= b.Hoehe - Sticks.EckRand + 1e-9 || my + r <= b.Hoehe - b.Unten + 1e-9, $"unter dem Bild: {my + r} ({b})");
                    Assert.True(mx - r >= (s == Sticks.Seite.Links ? 0 : b.Breite / 2) - 1e-9 && mx + r <= (s == Sticks.Seite.Links ? b.Breite / 2 : b.Breite) + 1e-9,
                        $"nicht in seiner Haelfte ({mx}, {r}, {b})");
                    gepruefte++;
                    // Gegenprobe: ein Stick mit der Mitte genau unter dem Daumen (ohne Klemme) laege oft ueber der Leiste
                    if (y + Sticks.Radius > b.Hoehe - b.Unten || x - Sticks.Radius < 0 || x + Sticks.Radius > b.Breite)
                    {
                        ohneKlemmeRaus++;
                    }
                }
            }
        }
        Assert.True(gepruefte > 2000, $"nur {gepruefte} Stellen");
        Assert.True(ohneKlemmeRaus > 100, $"Gegenprobe greift nicht ({ohneKlemmeRaus})");
    }

    [Fact]
    public void Steuerung_Karte_HatDenSchalter_Suche_TitelUndKurzzeile()
    {
        Assert.Equal("Steuerung", Optionen.TastenBereich.Titel);
        Assert.Equal("Gehen, Tasten und Sticks im Spiel", Optionen.Kurzzeile(Optionen.TastenBereich));
        // die alten Woerter finden die Karte weiter, die neuen auch
        foreach (var wort in new[] { "Tastenbelegung", "Tastatur", "Steuerung", "Antippen", "Stick" })
        {
            Assert.Equal((Optionen.Tasten, false), GlobaleSuche.Ziel(Optionen.TastenBereich.Titel, wort));
            Assert.Contains(wort, Optionen.TastenBereich.Stichwoerter);
        }
        Assert.Contains("unten in dieser Karte", SpielProbeTexte.AntippenGehenSatz, StringComparison.Ordinal);
        Assert.DoesNotContain("„Tastenbelegung“", SpielProbeTexte.AntippenGehenSatz, StringComparison.Ordinal);
    }

    [Fact]
    public void Schalter_WertUndStandardBleiben_NachDemUmzug()
    {
        // gleicher Schluessel, gleicher Standard (an = kein Eintrag), aus bleibt aus: nichts geht beim Umzug verloren
        Assert.Equal("antippen_gehen", Gehbedienung.Schluessel);
        var werte = new System.Text.Json.Nodes.JsonObject();
        Assert.True(Gehbedienung.Lesen(werte));
        Gehbedienung.Wert(werte, false);
        Assert.False(Gehbedienung.Lesen(werte));
        Assert.Equal("{\"antippen_gehen\":false}", werte.ToJsonString());
        Gehbedienung.Wert(werte, true);
        Assert.True(Gehbedienung.Lesen(werte));
        Assert.Equal("{}", werte.ToJsonString());
    }

    /// <summary>
    /// Welle A 0.99.0 (Sammelliste 399, Nico: „Dadurch druecke ich immer das Handy zu erst“): die Leiste steht mittig; ein Daumen
    /// links oder rechts unten neben ihr wird zum Stick (dort lag frueher das Spiel-Handy), auf der Leiste nie. Gegenprobe: die
    /// Leiste ueber die ganze Breite (bis A 0.98.0) laesst unten keinen Stick zu.
    /// </summary>
    [Fact]
    public void Leiste_Mittig_DaumenUntenDaneben_WirdStick_Gegenprobe()
    {
        var ecken = 0;
        foreach (var (name, breite, hoehe) in Groessen.Where(g => g.Name.StartsWith("Handy", StringComparison.Ordinal) || g.Name.StartsWith("Tablet", StringComparison.Ordinal)))
        {
            const double leisteBreite = 180, leisteHoehe = 60;
            var (von, bis) = ((breite - leisteBreite) / 2, (breite + leisteBreite) / 2);
            var b = new Sticks.Bereich(breite, hoehe, 64, leisteHoehe, von, bis);
            var unten = hoehe - 20;
            Assert.Equal(Sticks.Seite.Links, Sticks.SeiteFuer(40, unten, b));
            Assert.Equal(Sticks.Seite.Rechts, Sticks.SeiteFuer(breite - 40, unten, b));
            Assert.Null(Sticks.SeiteFuer(breite / 2, unten, b));
            Assert.Null(Sticks.SeiteFuer(von + 5, unten, b));
            // A 1.0.0 (Sammelliste 405, Nico: „Sticks weiter nach unten in die Ecken“): der Stick liegt unten in der Ecke neben der
            // Leiste (Abstand EckRand zum Rand), beruehrt die Leiste nicht; Gegenprobe Stand A 0.99.0: er endete ueber der Leiste
            // Ist neben der Leiste fuer den ganzen Kreis kein Platz (Handy hoch: 102 Punkte neben der Leiste, Kreis 120), bleibt
            // er ueber der Leiste: nie Ueberlappung (Nico: „Nichts darf ueberlappen“)
            var (mx, my, r) = Sticks.Lage(40, unten, Sticks.Seite.Links, b);
            var platz = von - Sticks.EckRand >= 2 * r;
            if (platz)
            {
                ecken++;
                Assert.Equal(hoehe - Sticks.EckRand, my + r, 6);
                Assert.True(mx + r <= von - Sticks.EckRand + 1e-9, name);
            }
            else
            {
                Assert.True(my + r <= hoehe - leisteHoehe + 1e-9, name + ": ohne Platz neben der Leiste ueber ihr");
            }
            var (mxr, myr, rr) = Sticks.Lage(breite - 40, unten, Sticks.Seite.Rechts, b);
            if (platz)
            {
                Assert.Equal(hoehe - Sticks.EckRand, myr + rr, 6);
                Assert.True(mxr - rr >= bis + Sticks.EckRand - 1e-9, name);
            }
            else
            {
                Assert.True(myr + rr <= hoehe - leisteHoehe + 1e-9, name + ": ohne Platz neben der Leiste ueber ihr");
            }
            Sticks.AltFuerTests = true;
            try
            {
                var (_, myAlt, rAlt) = Sticks.Lage(40, unten, Sticks.Seite.Links, b);
                Assert.True(myAlt + rAlt <= hoehe - leisteHoehe + 1e-9, name);
                Assert.True(!platz || myAlt + rAlt < my + r - 30, name + ": Gegenprobe greift nicht");
            }
            finally
            {
                Sticks.AltFuerTests = false;
            }
            // Gegenprobe: Leiste ueber die ganze Breite wie bis A 0.98.0
            var alt = new Sticks.Bereich(breite, hoehe, 64, leisteHoehe);
            Assert.Null(Sticks.SeiteFuer(40, unten, alt));
            Assert.Null(Sticks.SeiteFuer(breite - 40, unten, alt));
        }
        // Handy quer, Tablet hoch und quer: in der Ecke
        Assert.True(ecken >= 3, "zu wenige Groessen mit Stick in der Ecke: " + ecken);
    }

    /// <summary>
    /// Welle A 1.0.0 (Sammelliste 405): die runden Knoepfe stehen unten rechts in der Ecke. Auf ihnen beginnt kein Stick, und der
    /// rechte Stick ragt nie in ihre Flaeche (er rueckt nach links oder oben), an jeder Stelle der rechten Zone und in jeder Groesse.
    /// Gegenprobe: ohne Sperre (Stand A 0.99.0) laegen Sticks auf den Knoepfen.
    /// </summary>
    [Fact]
    public void Sticks_Knoepfe_InDerEcke_NieUeberlappt_Gegenprobe()
    {
        var gepruefte = 0;
        var ohneSperre = 0;
        foreach (var (name, breite, hoehe) in Groessen.Where(g => g.Name.StartsWith("Handy", StringComparison.Ordinal) || g.Name.StartsWith("Tablet", StringComparison.Ordinal)))
        {
            foreach (var leisteBreite in new[] { 180.0, 420.0 })
            {
                var leer = new Sticks.Bereich(breite, hoehe, 64, 60, (breite - leisteBreite) / 2, (breite + leisteBreite) / 2);
                var b = leer with { Sperre = HaltungKnopf.Sperre(leer) };
                if (b.Sperre.X2 <= b.Sperre.X1)
                {
                    continue;
                }
                for (var x = breite / 2; x <= breite; x += 8)
                {
                    for (var y = 0.0; y <= hoehe; y += 8)
                    {
                        if (b.Gesperrt(x, y))
                        {
                            Assert.Null(Sticks.SeiteFuer(x, y, b));
                        }
                        if (Sticks.SeiteFuer(x, y, b) is not { } s)
                        {
                            continue;
                        }
                        var (mx, my, r) = Sticks.Lage(x, y, s, b);
                        Assert.False(Sticks.KreisInRechteck(mx, my, r, b.Sperre), $"{name} {leisteBreite}: Stick ({mx:F0}, {my:F0}, {r:F0}) auf den Knoepfen");
                        gepruefte++;
                        Sticks.AltFuerTests = true;
                        try
                        {
                            if (Sticks.SeiteFuer(x, y, leer) is { } sa && Sticks.Lage(x, y, sa, leer) is var (ax, ay, ar) && Sticks.KreisInRechteck(ax, ay, ar, b.Sperre))
                            {
                                ohneSperre++;
                            }
                        }
                        finally
                        {
                            Sticks.AltFuerTests = false;
                        }
                    }
                }
            }
        }
        Assert.True(gepruefte > 200, $"nur {gepruefte} Stellen");
        Assert.True(ohneSperre > 0, "Gegenprobe greift nicht");
        // Gegenprobe deutlich: ohne Sperre beginnt auf dem Knopf Springen ein Stick
        var g0 = new Sticks.Bereich(915, 412, 64, 60, 367, 547);
        var l0 = HaltungKnopf.Lage(g0)!.Value;
        Assert.NotNull(Sticks.SeiteFuer(l0.Springen.X, l0.Springen.Y, g0));
        Assert.Null(Sticks.SeiteFuer(l0.Springen.X, l0.Springen.Y, g0 with { Sperre = HaltungKnopf.Sperre(g0) }));
    }
}
