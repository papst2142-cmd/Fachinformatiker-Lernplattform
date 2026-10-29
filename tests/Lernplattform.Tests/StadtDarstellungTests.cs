using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W11 (Weisung P8-45): Darstellung der Stadt ohne Einfluss auf den Spielstand. Genau 21 Laternen nach
/// Nicos Skizze (16 einfache mit dem Arm zur Strasse, 5 Doppel-Laternen mit den Armen laengs der Strasse) in jeder
/// Jahres- und Festzeit an denselben Plaetzen, jede nach den Regeln aus P8-41 frei; die Regeln lehnen die verbotenen
/// Stellen wirklich ab. Die Modelle eines Hauses ueberlappen sich nicht (Bahnhof ohne Turm, Altbau mit Anbau an der
/// Ostwand), die Bahnhofsuhr c2 sitzt an der Wand, Strassenenden sind buendig, das Stellwerk hat hinten keine Tuer.
/// </summary>
public sealed class StadtDarstellungTests
{
    private static GrafikBibliothek Grafik => SpielGrafikTests.Bibliothek.Value;

    private static StadtSzene Szene(string? fest, Jahreszeit j, Spielstand? stand = null) =>
        StadtSzene.Bauen(new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value), stand, Grafik, fest, FestDekoTests.Daten.Value, j);

    private static readonly Lazy<StadtSzene> Sommer = new(() => Szene(null, Jahreszeit.Sommer));

    public static TheoryData<string?, Jahreszeit> Zeiten => new()
    {
        { null, Jahreszeit.Fruehling },
        { null, Jahreszeit.Sommer },
        { null, Jahreszeit.Herbst },
        { null, Jahreszeit.Winter },
        { Festzeit.Ostern, Jahreszeit.Fruehling },
        { Festzeit.Halloween, Jahreszeit.Herbst },
        { Festzeit.Weihnachten, Jahreszeit.Winter },
        { Festzeit.Silvester, Jahreszeit.Winter },
    };

    /// <summary>Genau 21 Laternen (16 einfach, 5 doppelt) an denselben Plaetzen in jeder Jahres- und Festzeit; jede frei nach den Regeln.</summary>
    [Theory]
    [MemberData(nameof(Zeiten))]
    public void LaternenJeJahresUndFestzeit(string? fest, Jahreszeit j)
    {
        var szene = Szene(fest, j);
        var laternen = szene.Laternen;
        Assert.Equal(Nachtlicht.Anzahl, laternen.Count);
        Assert.Equal(21, laternen.Count);
        Assert.Equal(16, laternen.Count(l => !l.Doppelt));
        Assert.Equal(5, laternen.Count(l => l.Doppelt));
        Assert.Equal(Nachtlicht.Skizze.Length, laternen.Count);
        // dieselben Plaetze wie im Sommer ohne Fest
        var bezug = Sommer.Value.Laternen;
        for (var i = 0; i < laternen.Count; i++)
        {
            Assert.True(Vector2.Distance(laternen[i].Ort, bezug[i].Ort) < 1e-4f && laternen[i].Doppelt == bezug[i].Doppelt
                && Vector2.Distance(laternen[i].Richtung, bezug[i].Richtung) < 1e-4f,
                string.Create(CultureInfo.InvariantCulture, $"{fest} {j}: Laterne {laternen[i].Nr} steht anders als im Sommer"));
        }
        var regeln = new Nachtlicht.Regeln(szene);
        foreach (var l in laternen)
        {
            var (frei, grund, abstand) = regeln.Pruefen(l.Ort);
            Assert.True(frei, string.Create(CultureInfo.InvariantCulture, $"{fest} {j}: Laterne {l.Nr} bei {l.Ort.X:F2};{l.Ort.Y:F2} verletzt {grund} ({abstand:F2})"));
        }
        for (var a = 0; a < laternen.Count; a++)
        {
            for (var b = a + 1; b < laternen.Count; b++)
            {
                Assert.True(Vector2.Distance(laternen[a].Ort, laternen[b].Ort) >= Nachtlicht.Zwischen - 1e-4f,
                    $"Laternen {laternen[a].Nr} und {laternen[b].Nr} zu nah");
            }
        }
    }

    /// <summary>Einfache Laternen zeigen mit dem Arm zur Strasse, Doppel-Laternen mit den Armen laengs; alle am Strassenrand.</summary>
    [Fact]
    public void LaternenArmeUndStrassenrand()
    {
        var szene = Sommer.Value;
        foreach (var l in szene.Laternen)
        {
            var (abstand, laengs) = NaechsteStrasse(szene, l.Ort);
            Assert.True(MathF.Abs(abstand - Nachtlicht.Rand) < 0.02f,
                string.Create(CultureInfo.InvariantCulture, $"Laterne {l.Nr}: {abstand:F2} von der Strassenmitte statt {Nachtlicht.Rand:F2}"));
            Assert.True(MathF.Abs(l.Richtung.Length() - 1) < 1e-3f, $"Laterne {l.Nr}: Richtung nicht Laenge 1");
            var quer = MathF.Abs(Vector2.Dot(l.Richtung, laengs));
            if (l.Doppelt)
            {
                Assert.True(quer > 0.99f, $"Doppel-Laterne {l.Nr}: Arme nicht laengs der Strasse");
            }
            else
            {
                Assert.True(quer < 0.01f, $"Laterne {l.Nr}: Arm nicht quer zur Strasse");
                var (naeher, _) = NaechsteStrasse(szene, l.Ort + (l.Richtung * 0.2f));
                Assert.True(naeher < abstand - 0.15f, $"Laterne {l.Nr}: Arm zeigt nicht zur Strasse");
            }
        }
        // die Skizze ist uebertragen (P8-47): nur laengs der Strasse verschoben, auf derselben Seite derselben Strasse,
        // hoechstens so weit wie noetig (groesste Verschiebung 0,20 bei Nr. 11 und 15)
        foreach (var l in szene.Laternen)
        {
            var (strasse, laenge, seite) = Nachtlicht.StrasseVon(szene.Strassen, l.Ziel);
            var (strasseOrt, laengeOrt, seiteOrt) = Nachtlicht.StrasseVon(szene.Strassen, l.Ort);
            Assert.True(ReferenceEquals(strasse, strasseOrt) && seite == seiteOrt, $"Laterne {l.Nr}: andere Strasse oder Seite als der Kreis");
            Assert.True(MathF.Abs(MathF.Abs(laengeOrt - laenge) - l.Verschoben) < 0.02f, $"Laterne {l.Nr}: nicht nur laengs verschoben");
            Assert.True(l.Verschoben <= 0.21f, string.Create(CultureInfo.InvariantCulture, $"Laterne {l.Nr} um {l.Verschoben:F2} verschoben"));
        }
    }

    /// <summary>
    /// Genau diese 21 Plaetze und Arten (Weisung P8-47, Kamera aus Nicos zwei Skizzen nachgebaut): Ort nach den Regeln,
    /// D = Doppel-Laterne. Verschoben sind nur 6 (Brueckenpfosten), 7 (Baum), 11 und 12 (Brueckenpfosten), 15 (Tuer Stellwerk).
    /// </summary>
    [Fact]
    public void LaternenGenauDiesePlaetze()
    {
        (float X, float Z, bool D)[] soll =
        [
            (7.00f, 0.46f, false), (8.00f, 2.55f, false), (7.00f, 5.27f, false), (7.00f, 7.72f, false), (7.00f, 12.61f, true),
            (8.00f, 14.53f, false), (7.00f, 16.82f, false), (3.52f, 20.30f, true), (0.41f, 21.30f, false), (7.60f, 21.30f, true),
            (9.96f, 21.30f, false), (11.94f, 20.30f, false), (18.50f, 21.30f, true), (18.90f, 12.66f, true), (17.90f, 15.39f, false),
            (18.90f, 17.21f, false), (22.83f, 20.30f, false), (26.70f, 21.30f, false), (31.69f, 21.30f, false), (30.30f, 14.76f, false),
            (29.30f, 17.85f, false),
        ];
        var laternen = Sommer.Value.Laternen;
        Assert.Equal(soll.Length, laternen.Count);
        Assert.Equal(16, laternen.Count(l => !l.Doppelt));
        for (var i = 0; i < soll.Length; i++)
        {
            var l = laternen[i];
            Assert.Equal(i + 1, l.Nr);
            Assert.Equal(soll[i].D, l.Doppelt);
            Assert.True(Vector2.Distance(l.Ort, new Vector2(soll[i].X, soll[i].Z)) < 0.01f,
                string.Create(CultureInfo.InvariantCulture, $"Laterne {l.Nr} bei {l.Ort.X:F2};{l.Ort.Y:F2} statt {soll[i].X:F2};{soll[i].Z:F2}"));
            // P8-50: keine Laterne im Lichtraum eines Gleises
            Assert.True(Sommer.Value.Gleise.All(g => Kurve.Abstand(l.Ort, g) >= Bahnuebergang.LichtraumHalb + Nachtlicht.AbstandModell), $"Laterne {l.Nr} im Lichtraum");
        }
    }

    private static List<Vector2> Ecken(Bahnuebergang.Kreuz k) => Bahnuebergang.Grundpunkte(k);

    private static List<string> ImLichtraum(StadtSzene szene, IEnumerable<Bahnuebergang.Kreuz> kreuze) =>
        [.. kreuze.SelectMany(k => Ecken(k).Where(p => szene.Gleise.Any(g => Kurve.Abstand(p, g) < Bahnuebergang.LichtraumHalb))
            .Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.X:F2};{p.Y:F2}")))];

    /// <summary>
    /// Bahnuebergang (Weisung P8-49, Variante c): kein Teil der Ausstattung (Sockel, Mast, Kreuz, Lichtkasten, Antrieb,
    /// Schranke offen und spaeter geschlossen) im Lichtraum eines Gleises (<see cref="Bahnuebergang.LichtraumHalb"/> von
    /// der Gleismitte, breitester Zugwagen plus 0,15). Gegenprobe: an der alten Stelle aus P8-44 ist der Test rot.
    /// </summary>
    [Fact]
    public void BahnuebergangAusserhalbDesGleises()
    {
        var szene = Sommer.Value;
        Assert.Equal(2 * szene.Uebergaenge.Count(u => u.Strasse), szene.Andreaskreuze.Count);
        Assert.NotEmpty(szene.Andreaskreuze);
        Assert.Empty(ImLichtraum(szene, szene.Andreaskreuze));
        // der Lichtraum deckt den breitesten Wagen des Zugmodells (alle drei Wagen gleich skaliert wie im Bild)
        var wagen = new[] { "train-electric-city-a", "train-electric-city-b", "train-electric-city-c" }.Select(n => Grafik.Modell(n)!).ToList();
        var skala = StadtSzene.ZugBreite / (wagen[0].Max.X - wagen[0].Min.X);
        Assert.All(wagen, w => Assert.True((w.Max.X - w.Min.X) * skala <= Bahnuebergang.BreitesterWagen, $"{w.Name} breiter als BreitesterWagen"));
        Assert.Contains(wagen, w => (w.Max.X - w.Min.X) * skala > Bahnuebergang.BreitesterWagen - 0.002f);
        Assert.Equal((Bahnuebergang.BreitesterWagen / 2) + 0.15f, Bahnuebergang.LichtraumHalb);
        // Gegenprobe: dieselbe Ausstattung an der Stelle der alten Kreuze (P8-44: halbe Gleisbreite + 0,12 + 0,2 vor der Mitte)
        var alt = new List<Bahnuebergang.Kreuz>();
        foreach (var g in szene.Gleise)
        {
            foreach (var strasse in szene.Strassen)
            {
                foreach (var (mitte, richtung, sinus) in StadtSzene.Kreuzungen(g, strasse))
                {
                    var quer = new Vector2(-richtung.Y, richtung.X);
                    var halb = ((StadtSzene.GleisBreite / 2) + 0.12f) / sinus;
                    foreach (var vor in (float[])[-1f, 1f])
                    {
                        alt.Add(new Bahnuebergang.Kreuz(mitte + (richtung * vor * (halb + 0.2f)) + (quer * -vor * ((StadtSzene.StrassenBreite / 2) + 0.14f)), richtung * vor, quer * vor));
                    }
                }
            }
        }
        Assert.NotEmpty(alt);
        Assert.NotEmpty(ImLichtraum(szene, alt));
    }

    /// <summary>
    /// Fehler der Lage nach Nicos Regel (P8-50, Rechtsverkehr): je Uebergang und Fahrtrichtung genau eine Ausstattung vor dem
    /// Gleis auf der rechten Seite des ankommenden Autos; Drehpunkt und Ende der geschlossenen Schranke auf dessen rechter
    /// Fahrspur bzw. am Rand (nie auf der Gegenspur).
    /// </summary>
    private static List<string> NichtRechts(StadtSzene szene, IReadOnlyList<Bahnuebergang.Kreuz> kreuze)
    {
        var fehler = new List<string>();
        foreach (var g in szene.Gleise)
        {
            foreach (var strasse in szene.Strassen)
            {
                foreach (var (mitte, richtung, _) in StadtSzene.Kreuzungen(g, strasse))
                {
                    foreach (var vor in (float[])[-1f, 1f])
                    {
                        var fahrt = -richtung * vor;
                        var rechts = Bahnuebergang.Rechts(fahrt);
                        var hier = kreuze.Where(k => Vector2.Distance(k.Fuss, mitte) < 3f && Vector2.Dot(k.Fuss - mitte, richtung * vor) > 0).ToList();
                        if (hier.Count != 1)
                        {
                            fehler.Add($"{hier.Count} Ausstattungen vor dem Gleis");
                            continue;
                        }
                        var k = hier[0];
                        var ende = k.Drehpunkt + (k.ZurStrasse * Bahnuebergang.SchrankeLaenge);
                        if (Vector2.Dot(k.Fuss - mitte, rechts) <= StadtSzene.StrassenBreite / 2)
                        {
                            fehler.Add(string.Create(CultureInfo.InvariantCulture, $"Ausstattung bei {k.Fuss.X:F2};{k.Fuss.Y:F2} nicht rechts der Fahrtrichtung"));
                        }
                        if (Vector2.Dot(k.Drehpunkt - mitte, rechts) <= 0 || Vector2.Dot(ende - mitte, rechts) < -0.01f || Vector2.Dot(ende - mitte, rechts) > StadtSzene.StrassenBreite / 2)
                        {
                            fehler.Add(string.Create(CultureInfo.InvariantCulture, $"Schranke bei {k.Fuss.X:F2};{k.Fuss.Y:F2} sperrt nicht die rechte Spur"));
                        }
                    }
                }
            }
        }
        return fehler;
    }

    /// <summary>
    /// Bahnuebergang (Weisung P8-50): je Fahrtrichtung rechts vor dem Gleis, die Schranke sperrt geschlossen nur die rechte
    /// Spur; rechts heisst dieselbe Seite wie die Spur der Autos. Gegenprobe: mit der Lage aus P8-45 (beide auf einer Seite) rot.
    /// </summary>
    [Fact]
    public void BahnuebergangRechtsInFahrtrichtung()
    {
        // dieselbe Seite wie die rechte Spur der Autos (Verkehr: Kurve.Versetzt um +Spur)
        var spur = Kurve.Versetzt([new Vector2(0, 0), new Vector2(0, 1)], Verkehr.Spur);
        Assert.True(Vector2.Distance(spur[0], Bahnuebergang.Rechts(new Vector2(0, 1)) * Verkehr.Spur) < 1e-5f);
        var szene = Sommer.Value;
        Assert.NotEmpty(szene.Andreaskreuze);
        Assert.Empty(NichtRechts(szene, szene.Andreaskreuze));
        // Gegenprobe P8-45: die noerdliche Ausstattung stand auf derselben Strassenseite wie die suedliche (gespiegelt)
        var alt = szene.Andreaskreuze.Select((k, i) =>
        {
            if (i % 2 == 0)
            {
                return k;
            }
            var m = szene.Uebergaenge.Where(u => u.Strasse).MinBy(u => Vector2.Distance(u.Mitte, k.Fuss)).Mitte;
            var r = -k.ZurStrasse;
            return new Bahnuebergang.Kreuz(k.Fuss - (r * (2 * Vector2.Dot(k.Fuss - m, r))), k.Blick, -k.ZurStrasse);
        }).ToList();
        Assert.NotEmpty(NichtRechts(szene, alt));
    }

    /// <summary>Die Ausstattung steht nicht auf Fahrbahn oder Weg und nicht in Laterne, Baum, Gruen oder Deko einer Festzeit.</summary>
    [Fact]
    public void BahnuebergangFrei()
    {
        foreach (var szene in Festzeit.Alle.Select(f => Szene(f, Jahreszeit.Herbst)).Prepend(Sommer.Value))
        {
            foreach (var k in szene.Andreaskreuze)
            {
                var ecken = Ecken(k).SkipLast(1).ToList();
                foreach (var strasse in szene.Strassen)
                {
                    Assert.All(ecken, p => Assert.True(Kurve.Abstand(p, strasse) >= Nachtlicht.Rand - 0.01f,
                        string.Create(CultureInfo.InvariantCulture, $"Ausstattung bei {p.X:F2};{p.Y:F2} auf Fahrbahn oder Rand")));
                }
                Assert.All(szene.Laternen, l => Assert.True(ecken.Min(p => Vector2.Distance(p, l.Ort)) >= Nachtlicht.AbstandModell, $"Laterne {l.Nr} an der Ausstattung"));
                Assert.All(szene.Baeume, b => Assert.True(Vector2.Distance(b.Mitte, k.Fuss) >= b.R + Bahnuebergang.Grundriss, "Baum an der Ausstattung"));
                Assert.All(szene.GruenKlein!.Plaetze, g => Assert.True(Vector2.Distance(new Vector2(g.X, g.Z), k.Fuss) >= g.R + Bahnuebergang.Grundriss, $"{g.Art} an der Ausstattung"));
                Assert.All(szene.FestGesetzt, g => Assert.True(Vector2.Distance(new Vector2(g.X, g.Z), k.Fuss) >= g.R + Bahnuebergang.Grundriss, $"{g.Art} an der Ausstattung"));
                // nur die zwei Lichter tragen die Nachtmarke, tagsueber dunkelrot
                var (netz, von, bis) = Bahnuebergang.Bauen();
                Assert.Equal(48, bis - von);
                Assert.All(Enumerable.Range(von, bis - von), e => Assert.Equal(Bahnuebergang.Licht, netz.FarbeAn(e)));
            }
            // nachts leuchten am Uebergang genau die Lichter (je Ausstattung 48 Ecken), nichts sonst
            var alle = new Netz();
            Bahnuebergang.Hinzu(alle, szene.Andreaskreuze);
            Assert.Equal(48 * szene.Andreaskreuze.Count, Enumerable.Range(0, alle.AnzahlEcken).Count(e => Nachtlicht.Leuchtet(alle.FarbeAn(e))));
        }
    }

    /// <summary>
    /// Fest-Deko an Haustueren (Weisung P8-48): jedes Stueck einer Tuer steht vor der Wand der gezeichneten Tuer (auch bei
    /// verlegtem Zugang wie Bahnhof und Gewerbehof), hoechstens <see cref="DekoAnDerTuer"/> von ihrer Mitte, und kein stehendes
    /// Stueck liegt auf dem Weg zur Tuer. In allen vier Festzeiten.
    /// </summary>
    [Theory]
    [InlineData(Festzeit.Ostern)]
    [InlineData(Festzeit.Halloween)]
    [InlineData(Festzeit.Weihnachten)]
    [InlineData(Festzeit.Silvester)]
    public void FestDekoAnDerTuer(string fest)
    {
        var szene = Szene(fest, Jahreszeit.Herbst);
        var anTueren = szene.FestGesetzt.Where(g => g.Ort is not null).ToList();
        Assert.NotEmpty(anTueren);
        foreach (var g in anTueren)
        {
            var tuer = szene.Tueren.Single(t => t.Ort == g.Ort);
            var p = new Vector2(g.X, g.Z);
            var weite = Vector2.Distance(p, tuer.Mitte);
            Assert.True(weite <= DekoAnDerTuer, string.Create(CultureInfo.InvariantCulture, $"{fest} {g.Ort}: {g.Art} {weite:F2} von der Tuer"));
            Assert.True(Vector2.Dot(p - tuer.Mitte, tuer.Aussen) >= 0, $"{fest} {g.Ort}: {g.Art} hinter der Wand der Tuer");
            if (g.IstHindernis)
            {
                // nicht auf Weg, Gehweg, Strasse, Gleis oder Wasser (dieselben Linien wie fuer die Baeume)
                foreach (var (linie, halb) in szene.BaumSperren)
                {
                    var weg = Kurve.Abstand(p, linie);
                    Assert.True(weg >= halb + g.R, string.Create(CultureInfo.InvariantCulture, $"{fest} {g.Ort}: {g.Art} auf einem Weg ({weg:F2})"));
                }
            }
        }
        // der Bahnhof (Tuer an der Suedwand, P8-48) hat seine Deko am Eingang
        Assert.Contains(anTueren, g => g.Ort == "talheim");
    }

    /// <summary>Groesster Abstand eines Deko-Stuecks von der Mitte seiner Tuer (P8-48).</summary>
    private const float DekoAnDerTuer = 1.6f;

    /// <summary>Die Regeln lehnen die verbotenen Stellen ab: Tuer, Weg zur Tuer, Haus, Baum, Gruen, Fest-Deko, Bruecke, Fahrbahn, Fluss.</summary>
    [Fact]
    public void LaternenRegelnLehnenAb()
    {
        var szene = Szene(Festzeit.Halloween, Jahreszeit.Herbst);
        var regeln = new Nachtlicht.Regeln(szene);
        void Verboten(Vector2 q, string was)
        {
            var (frei, grund, _) = regeln.Pruefen(q);
            Assert.False(frei, string.Create(CultureInfo.InvariantCulture, $"{was} bei {q.X:F2};{q.Y:F2} erlaubt ({grund})"));
        }
        var tuer = szene.Tueren.First(t => t.Ort == "kaufhaus");
        Verboten(tuer.Mitte + (tuer.Aussen * 0.6f), "vor der Tuer");
        Verboten(Vector2.Lerp(tuer.Mitte, tuer.WegAnfang, 0.5f), "Weg zur Tuer");
        var haus = szene.Haeuser.First(h => h.Id == "talheim");
        Verboten(new Vector2((haus.Min.X + haus.Max.X) / 2, (haus.Min.Z + haus.Max.Z) / 2), "im Haus");
        Verboten(szene.Baeume[0].Mitte, "im Baum");
        Verboten(szene.Bruecken[0], "auf der Bruecke");
        // an der Bruecke: neben dem Endpfosten und neben der Bruestung (Grundriss der Bruecke, P8-47)
        foreach (var (mitte, richtung, halbLaenge, _) in szene.BrueckenKaesten)
        {
            var quer = new Vector2(-richtung.Y, richtung.X) * Nachtlicht.Rand;
            Verboten(mitte + (richtung * (halbLaenge + 0.1f)) + quer, "am Endpfosten der Bruecke");
            Verboten(mitte + (richtung * (halbLaenge * 0.5f)) - quer, "an der Bruestung");
        }
        Verboten(szene.Strassen[0][0] + (Vector2.Normalize(szene.Strassen[0][1] - szene.Strassen[0][0]) * 2), "auf der Fahrbahn");
        Verboten(szene.FestPlaetzeAlle[0].P, "auf der Fest-Deko");
        var gruen = szene.GruenKlein!.Plaetze.First(p => p.Art != "gras");
        Verboten(new Vector2(gruen.X, gruen.Z), "im Beet oder Busch");
        Assert.Contains(szene.Laternen, l => regeln.Pruefen(l.Ort).Frei);
    }

    /// <summary>Kein Modell eines Hauses steckt in einem anderen (Wand an Wand ist erlaubt), auch mit dem Altbau.</summary>
    [Fact]
    public void HaeuserUeberlappenNicht()
    {
        var altbau = new Spielstand([], SpielInhaltTests.Inhalt) { Wohnung = "altbau" };
        var gebaeude = Grafik.Bloecke.Values.ToHashSet();
        var geprueft = 0;
        foreach (var szene in new[] { Sommer.Value, Szene(null, Jahreszeit.Sommer, altbau) })
        {
            // nur die Gebaeude-Modelle (Baeume am Haus sind Landschaft), in einem Haus und zwischen den Haeusern
            var kaesten = szene.Haeuser.SelectMany(haus => Enumerable.Range(haus.StueckeVon, haus.StueckeBis - haus.StueckeVon)
                .Where(i => gebaeude.Contains(szene.Stuecke[i].Name))
                .Select(i => (Haus: haus.Id, szene.Stuecke[i].Name, Kasten: Kasten(Grafik.Modell(szene.Stuecke[i].Name)!, szene.Stuecke[i].Lage)))).ToList();
            for (var a = 0; a < kaesten.Count; a++)
            {
                for (var b = a + 1; b < kaesten.Count; b++)
                {
                    geprueft++;
                    var tief = Ueberlappung(kaesten[a].Kasten, kaesten[b].Kasten);
                    Assert.True(tief <= 0.05f, string.Create(CultureInfo.InvariantCulture,
                        $"{kaesten[a].Haus}: {kaesten[a].Name} steckt {tief:F2} tief in {kaesten[b].Haus}: {kaesten[b].Name}"));
                }
            }
        }
        Assert.True(geprueft >= 5, "zu wenige Paare von Gebaeude-Modellen geprueft: " + geprueft);
        // Gegenprobe: die Bloecke aus orte.json (wie Python) stecken ineinander, die Darstellung trennt sie
        var modelle = StadtModelle.Modelle(SpielInhaltTests.Inhalt, null);
        Assert.True(BloeckeUeberlappen(modelle, "bahnhof", null, out _), "Gegenprobe Bahnhof: Turm steckt nicht im Hauptgebaeude");
        Assert.True(BloeckeUeberlappen(modelle, "wohnung", altbau, out _), "Gegenprobe Altbau: Anbau steckt nicht im Hauptgebaeude");
        Assert.Null(StadtSzene.Darstellung("bahnhof", 3));
        Assert.Equal(StadtSzene.AltbauAnbau, StadtSzene.Darstellung("wohnung", 5));
        Assert.False(BloeckeUeberlappen(modelle, "wohnung", altbau, out var tiefe, dargestellt: true), $"Altbau-Anbau steckt {tiefe:F2} tief im Hauptgebaeude");
    }

    /// <summary>Uhr c2: Wandplatte auf der Suedwand des Bahnhofs, Uhr unter dem Dach, Zifferblatt leuchtet nachts, kein gelbes Schild.</summary>
    [Fact]
    public void BahnhofsuhrAnDerWand()
    {
        var szene = Sommer.Value;
        var haus = szene.Haeuser.First(h => h.Id == "talheim");
        var teile = szene.Teile.Skip(haus.TeileVon).Take(haus.TeileBis - haus.TeileVon).ToList();
        var blattNr = Enumerable.Range(haus.TeileVon, haus.TeileBis - haus.TeileVon).Where(i => szene.Teile[i].Farbe == StadtSzene.UhrBlatt).ToList();
        Assert.All(blattNr, i => Assert.Contains(i, szene.LichtTeile));
        // nachts leuchten nur die beiden Seiten des Zifferblatts (nach Osten und Westen), nie die Kanten der Streifen
        var hausNetz = szene.HausNetz(haus);
        var leuchtend = Enumerable.Range(0, hausNetz.AnzahlEcken).Where(e => Nachtlicht.Leuchtet(hausNetz.FarbeAn(e))
            && MathF.Abs(hausNetz.Ort(e).Y - StadtSzene.UhrY) < StadtSzene.UhrR + 0.01f).ToList();
        Assert.NotEmpty(leuchtend);
        var blatt = blattNr.Select(i => szene.Teile[i]).Where(t => MathF.Min(t.Y1, t.Y2) > StadtSzene.UhrY - StadtSzene.UhrR - 0.01f
            && MathF.Max(t.Y1, t.Y2) < StadtSzene.UhrY + StadtSzene.UhrR + 0.01f).ToList();
        Assert.True(blatt.Count >= 5, "Zifferblatt fehlt oder leuchtet nicht");
        var kx = (blatt.Min(t => MathF.Min(t.X1, t.X2)) + blatt.Max(t => MathF.Max(t.X1, t.X2))) / 2;
        // Wandplatte: dunkler flacher Quader direkt an der Wand unter dem Arm
        var platte = Assert.Single(teile, t => MathF.Abs(MathF.Min(t.X1, t.X2) - (kx - 0.07f)) < 1e-3f && MathF.Abs(MathF.Max(t.X1, t.X2) - (kx + 0.07f)) < 1e-3f
            && MathF.Abs(MathF.Max(t.Z1, t.Z2) - MathF.Min(t.Z1, t.Z2) - 0.04f) < 1e-3f);
        var wand = MathF.Min(platte.Z1, platte.Z2) + 0.01f;
        var modelle = Enumerable.Range(haus.StueckeVon, haus.StueckeBis - haus.StueckeVon)
            .Select(i => Kasten(Grafik.Modell(szene.Stuecke[i].Name)!, szene.Stuecke[i].Lage)).ToList();
        Assert.Contains(modelle, k => MathF.Abs(k.Max.Z - wand) < 0.05f && k.Min.X < kx - 0.07f && k.Max.X > kx + 0.07f
            && k.Max.Y > StadtSzene.UhrY + StadtSzene.UhrR);
        // die Uhr steht quer vor der Wand, nicht in einem Modell
        var uhrMin = blatt.Min(t => MathF.Min(t.Z1, t.Z2));
        Assert.True(uhrMin > wand, "Zifferblatt steckt in der Wand");
        // kein gelbes Schild mehr am Bahnhof
        Assert.DoesNotContain(teile, t => t.Farbe.X > 0.9f && t.Farbe.X < 1.1f && t.Farbe.Y > 0.7f && t.Farbe.Y < 0.9f && t.Farbe.Z < 0.3f
            && MathF.Abs(t.Y1 - t.Y2) > 0.3f);
    }

    /// <summary>Sackgassen enden buendig an der Gehwegkante (halbe Gehwegbreite), alle anderen Enden mit halber Strassenbreite.</summary>
    [Fact]
    public void StrassenendenBuendig()
    {
        var szene = Sommer.Value;
        var sack = szene.StrassenEnden.Where(e => MathF.Abs(e.Verlaengert - (StadtSzene.WegBreite / 2)) < 1e-4f).ToList();
        Assert.Equal(2, sack.Count);
        Assert.Contains(sack, e => Vector2.Distance(e.Punkt, new Vector2(18.4f, 11.2f)) < 0.05f);
        Assert.Contains(sack, e => Vector2.Distance(e.Punkt, new Vector2(29.8f, 14.4f)) < 0.05f);
        foreach (var e in szene.StrassenEnden)
        {
            var amRand = e.Punkt.X <= 0.01f || e.Punkt.Y <= 0.01f || e.Punkt.X >= szene.Breite - 0.01f || e.Punkt.Y >= szene.Tiefe - 0.01f;
            var anStrasse = szene.Strassen.Count(s => Kurve.Abstand(e.Punkt, s) <= 0.05f) > 1;
            Assert.Equal(amRand || anStrasse ? StadtSzene.StrassenBreite / 2 : StadtSzene.WegBreite / 2, e.Verlaengert);
            // das Band der Fahrbahn endet genau dort
            Assert.Contains(szene.Teile, t => t.Art == TeilArt.Band
                && (Vector2.Distance(new Vector2(t.X1, t.Z1), e.Punkt + (e.Richtung * e.Verlaengert)) < 0.02f
                    || Vector2.Distance(new Vector2(t.X2, t.Z2), e.Punkt + (e.Richtung * e.Verlaengert)) < 0.02f));
        }
    }

    /// <summary>Das Stellwerk hat hinten keine sichtbare Tuer des Modells mehr (Gegenprobe: das rohe Modell hat eine).</summary>
    [Fact]
    public void StellwerkOhneRueckseitentuer()
    {
        var szene = Sommer.Value;
        var tuer = szene.Tueren.Single(t => t.Ort == "stellwerk");
        var haus = szene.Haeuser.First(h => h.Id == "stellwerk" || h.Id.StartsWith("stellwerk#", StringComparison.Ordinal));
        var c = new Vector2((haus.Min.X + haus.Max.X) / 2, (haus.Min.Z + haus.Max.Z) / 2);
        var tief = MathF.Abs(tuer.Aussen.X) > 0.5f ? (haus.Max.X - haus.Min.X) / 2 : (haus.Max.Z - haus.Min.Z) / 2;
        var lang = MathF.Abs(tuer.Aussen.X) > 0.5f ? (haus.Max.Z - haus.Min.Z) / 2 : (haus.Max.X - haus.Min.X) / 2;
        var rm = c - (tuer.Aussen * tief);
        var roh = StadtTueren.ModellTueren(szene.Stuecke, haus.StueckeVon, haus.StueckeBis, i => Grafik.Modell(szene.Stuecke[i].Name), rm, -tuer.Aussen, weite: lang, tief: 2 * tief);
        Assert.NotEmpty(roh);
        var jetzt = StadtTueren.ModellTueren(szene.Stuecke, haus.StueckeVon, haus.StueckeBis, szene.ModellAn, rm, -tuer.Aussen, weite: lang, tief: 2 * tief);
        Assert.DoesNotContain(jetzt, m => StadtTuerenTests.Sichtbar(szene, m, rm, -tuer.Aussen));
    }

    /// <summary>Tannen des Pakets stehen nie auf Wegen; zu Halloween Kuerbisse aus dem Paket, keine alten Kuerbisse in der Stadt.</summary>
    [Fact]
    public void TannenUndKuerbisseAusDemPaket()
    {
        var szene = Szene(Festzeit.Halloween, Jahreszeit.Herbst);
        var tannen = szene.Stuecke.Where(s => s.Name.StartsWith("gy-pine", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(tannen);
        // im Herbst die Herbstform (beim Netzbau gewaehlt), die es im Paket gibt
        Assert.All(tannen, s => Assert.NotNull(Grafik.Modell(StadtSzene.TanneIn(s.Name, Jahreszeit.Herbst))));
        Assert.All(tannen, s => Assert.StartsWith("gy-pine-fall", StadtSzene.TanneIn(s.Name, Jahreszeit.Herbst)));
        Assert.All(tannen, s => Assert.Equal(s.Name, StadtSzene.TanneIn(s.Name, Jahreszeit.Sommer)));
        foreach (var s in tannen)
        {
            var p = new Vector2(s.Lage.M41, s.Lage.M43);
            foreach (var (linie, halb) in szene.BaumSperren)
            {
                Assert.True(Kurve.Abstand(p, linie) >= halb, string.Create(CultureInfo.InvariantCulture, $"Tanne auf einem Weg bei {p.X:F2};{p.Y:F2}"));
            }
        }
        Assert.Contains(szene.Stuecke, s => s.Name.StartsWith("gy-pumpkin", StringComparison.Ordinal) || s.Name.StartsWith("gy-", StringComparison.Ordinal) && s.Name.Contains("pumpkin", StringComparison.Ordinal));
        Assert.DoesNotContain(szene.Stuecke, s => s.Name.StartsWith("hw-pumpkin", StringComparison.Ordinal));
    }

    private static (float Abstand, Vector2 Laengs) NaechsteStrasse(StadtSzene szene, Vector2 q)
    {
        var best = (Abstand: float.MaxValue, Laengs: Vector2.Zero);
        foreach (var s in szene.Strassen)
        {
            for (var i = 0; i + 1 < s.Count; i++)
            {
                var d = Kurve.Abstand(q, (IReadOnlyList<Vector2>)[s[i], s[i + 1]]);
                if (d < best.Abstand)
                {
                    best = (d, Vector2.Normalize(s[i + 1] - s[i]));
                }
            }
        }
        return best;
    }

    private static (Vector3 Min, Vector3 Max) Kasten(GrafikModell m, Matrix4x4 lage)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var x in (float[])[m.Min.X, m.Max.X])
        {
            foreach (var y in (float[])[m.Min.Y, m.Max.Y])
            {
                foreach (var z in (float[])[m.Min.Z, m.Max.Z])
                {
                    var w = Vector3.Transform(new Vector3(x, y, z), lage);
                    min = Vector3.Min(min, w);
                    max = Vector3.Max(max, w);
                }
            }
        }
        return (min, max);
    }

    /// <summary>Wie tief zwei Kaesten ineinander stecken (kleinste Ueberdeckung der drei Achsen, 0 = beruehren sich hoechstens).</summary>
    private static float Ueberlappung((Vector3 Min, Vector3 Max) a, (Vector3 Min, Vector3 Max) b)
    {
        var x = MathF.Min(a.Max.X, b.Max.X) - MathF.Max(a.Min.X, b.Min.X);
        var y = MathF.Min(a.Max.Y, b.Max.Y) - MathF.Max(a.Min.Y, b.Min.Y);
        var z = MathF.Min(a.Max.Z, b.Max.Z) - MathF.Max(a.Min.Z, b.Min.Z);
        return MathF.Max(0, MathF.Min(x, MathF.Min(y, z)));
    }

    /// <summary>Stecken zwei sichtbare Bloecke eines Modells aus orte.json ineinander (Grundriss, roh oder wie dargestellt)?</summary>
    private static bool BloeckeUeberlappen(PyObjekt modelle, string modell, Spielstand? stand, out float tiefe, bool dargestellt = false)
    {
        var k = StadtModelle.Kontext(stand, SpielInhaltTests.Inhalt);
        var bloecke = new List<(float X1, float Z1, float X2, float Z2)>();
        foreach (var (nr, teil) in StadtModelle.Teile(modelle, modell, k).Where(t => Py.Gleich(t.Teil["art"], "block")))
        {
            var dx = dargestellt ? StadtSzene.Darstellung(modell, nr) : 0f;
            if (dx is null)
            {
                continue;
            }
            float x = (float)Py.F(teil["x"]) + dx.Value, z = (float)Py.F(teil["y"]);
            bloecke.Add((x, z, x + (float)Py.F(teil["w"]), z + (float)Py.F(teil["t"])));
        }
        tiefe = 0;
        for (var a = 0; a < bloecke.Count; a++)
        {
            for (var b = a + 1; b < bloecke.Count; b++)
            {
                var x = MathF.Min(bloecke[a].X2, bloecke[b].X2) - MathF.Max(bloecke[a].X1, bloecke[b].X1);
                var z = MathF.Min(bloecke[a].Z2, bloecke[b].Z2) - MathF.Max(bloecke[a].Z1, bloecke[b].Z1);
                tiefe = MathF.Max(tiefe, MathF.Min(x, z));
            }
        }
        return tiefe > 0.05f;
    }
}
