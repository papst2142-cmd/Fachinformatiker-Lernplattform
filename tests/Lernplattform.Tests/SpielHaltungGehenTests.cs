using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Design;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.99.0 (Sammelliste 400, Weisungen P12-319/P12-320, Nico 1a 2a 3b): „Springen und Haltung sind mit dem rechten Daumen ohne
/// Menue erreichbar, das Symbol zeigt die Haltung, und man kann geduckt und kriechend gehen.“ Hier: frei gehen in der Haltung
/// (langsamer, Haltung bleibt), nie in Wand, Einrichtung oder Person (Durchdringung 0 nach <see cref="Innenraum.KoerperFrei"/>, in
/// allen Innenraeumen), Knopf „Haltung“ tippen/lang, Lage der Knoepfe ausserhalb der Stick-Bereiche und Kontrast des Symbols
/// auf der Grundfarbe in jeder Farbwelt. Gegenproben je Punkt.
/// </summary>
public sealed class SpielHaltungGehenTests
{
    private const double Dt = 1.0 / 30;

    /// <summary>Strecke, die die Figur in <paramref name="bilder"/> Bildern frei nach rechts (+x) geht.</summary>
    private static double Strecke(StadtSpiel s, int bilder)
    {
        var von = new Vector2(s.FigurX, s.FigurZ);
        for (var i = 0; i < bilder; i++)
        {
            s.FreiGehen(1, 0, Dt);
        }
        return Vector2.Distance(von, new Vector2(s.FigurX, s.FigurZ));
    }

    [Fact]
    public void FreiGehen_InHaltung_Langsamer_HaltungBleibt_GegenprobeAlt()
    {
        var s = SpielHaltungBedienungTests.ZuHause(SpielHaltungBedienungTests.NeuesSpiel());
        var innen = s.Innen!;
        var rechts = MathF.PI / 2;
        const int bilder = 6;
        var weite = (float)(bilder * Dt * StadtSpiel.Tempo) + 0.3f;
        var stelle = Enumerable.Range(0, 900).Select(i => new Vector2(0.3f + (i % 30 * 0.3f), 0.3f + (i / 30 * 0.3f)))
            .First(p => p.X + weite < innen.Breite && p.Y < innen.Tiefe
                && Enumerable.Range(0, (int)(weite / 0.05f) + 1).All(i => innen.KoerperFrei(p + new Vector2(i * 0.05f, 0), rechts, Koerperhaltung.Kriechen)
                    && StadtSpiel.FreiDrinnen(innen, p.X + (i * 0.05f), p.Y, p.X, p.Y)));
        double Gehen(Koerperhaltung haltung)
        {
            s.HinstellenFuerTests(stelle.X, stelle.Y);
            s.FreiHalt();
            if (haltung != Koerperhaltung.Stehen)
            {
                Assert.True(haltung == Koerperhaltung.Ducken ? s.Ducken(90f) : s.Kriechen(90f), s.HaltungHinweis);
            }
            var weg = Strecke(s, bilder);
            Assert.Equal(StadtSpiel.HaltungGehenAltFuerTests ? Koerperhaltung.Stehen : haltung, s.Koerper);
            return weg;
        }
        var stehen = Gehen(Koerperhaltung.Stehen);
        var ducken = Gehen(Koerperhaltung.Ducken);
        var kriechen = Gehen(Koerperhaltung.Kriechen);
        Assert.True(stehen > 0.5, "stehend kaum gegangen: " + stehen);
        Assert.InRange(ducken / stehen, StadtSpiel.DuckenTempo - 0.02, StadtSpiel.DuckenTempo + 0.02);
        Assert.InRange(kriechen / stehen, StadtSpiel.KriechenTempo - 0.02, StadtSpiel.KriechenTempo + 0.02);
        Assert.Equal(0.6f, StadtSpiel.DuckenTempo);
        Assert.Equal(0.35f, StadtSpiel.KriechenTempo);
        // Gegenprobe: Stand bis A 0.98.0, mit dem ersten Schritt steht die Figur auf und geht voll schnell
        StadtSpiel.HaltungGehenAltFuerTests = true;
        try
        {
            var alt = Gehen(Koerperhaltung.Kriechen);
            Assert.InRange(alt / stehen, 0.98, 1.02);
        }
        finally
        {
            StadtSpiel.HaltungGehenAltFuerTests = false;
        }
    }

    /// <summary>Zufallslauf in der Haltung durch einen Raum: Funde (Haltung an einer Stelle ohne ihren Platz) und Bilder in der Haltung.</summary>
    private static (List<string> Funde, int InHaltung) Lauf(StadtSpiel s, Innenraum innen, Koerperhaltung haltung, int bilder, int saat)
    {
        var funde = new List<string>();
        var drin = 0;
        var zufall = new Random(saat);
        s.Ansehen(innen, innen.StartX, innen.StartZ, 0f);
        var (rx, rz) = (0.0, 0.0);
        for (var i = 0; i < bilder && funde.Count < 3; i++)
        {
            if (s.Koerper == Koerperhaltung.Stehen)
            {
                // (wieder) einnehmen, wo Platz ist
                _ = haltung == Koerperhaltung.Ducken ? s.Ducken() : s.Kriechen();
            }
            if (i % 15 == 0)
            {
                var w = zufall.NextDouble() * Math.PI * 2;
                (rx, rz) = (Math.Sin(w), Math.Cos(w));
            }
            s.FreiGehen(rx, rz, Dt);
            if (s.Koerper is var k and not Koerperhaltung.Stehen)
            {
                drin++;
                if (!innen.KoerperFrei(new Vector2(s.FigurX, s.FigurZ), s.FigurRichtung * MathF.PI / 180f, k))
                {
                    funde.Add(string.Create(CultureInfo.InvariantCulture, $"{innen.Ansicht}: {k} beruehrt etwas bei {s.FigurX:F2}/{s.FigurZ:F2} Blick {s.FigurRichtung:F0}"));
                }
            }
        }
        return (funde, drin);
    }

    [Fact]
    public void JederInnenraum_GeducktUndKriechendGehen_DurchdringungNull_Gegenprobe()
    {
        var s = SpielHaltungBedienungTests.ZuHause(SpielHaltungBedienungTests.NeuesSpiel());
        var raeume = SpielWandTests.AlleRaeume(SpielWandTests.Fassungen().First().Innen)
            .DistinctBy(r => r.Ansicht + "|" + r.Fest + "|" + r.Personen.Count).ToList();
        var funde = new List<string>();
        var drin = 0;
        foreach (var (innen, i) in raeume.Select((r, i) => (r, i)))
        {
            foreach (var haltung in new[] { Koerperhaltung.Ducken, Koerperhaltung.Kriechen })
            {
                var (f, d) = Lauf(s, innen, haltung, 240, 400 + i);
                funde.AddRange(f);
                drin += d;
            }
        }
        Assert.True(raeume.Count > 20, "zu wenige Raeume: " + raeume.Count);
        Assert.True(drin > raeume.Count * 200, "zu wenige Bilder in der Haltung: " + drin);
        Assert.True(funde.Count == 0, string.Join("\n", funde.Take(20)));
        // Gegenprobe: ohne Platzpruefung der Haltung kriecht die Figur in Waende und Einrichtung
        StadtSpiel.KoerperFreiAltFuerTests(true);
        try
        {
            var alt = raeume.Take(12).SelectMany((r, i) => Lauf(s, r, Koerperhaltung.Kriechen, 240, 400 + i).Funde).ToList();
            Assert.True(alt.Count > 0, "Gegenprobe: ohne Pruefung haette die Haltung etwas beruehren muessen");
        }
        finally
        {
            StadtSpiel.KoerperFreiAltFuerTests(false);
        }
    }

    /// <summary>
    /// Seit A 1.7.0 liegt die Figur beim Kriechen flach (laenger als vorher): gleich hinter der Wohnungstuer ist dafuer kein Platz.
    /// Die Figur geht darum zuerst an eine Stelle, an der Ducken und Kriechen in jeder Richtung Platz haben (wie der Start-Test).
    /// </summary>
    internal static StadtSpiel FreieStelle(StadtSpiel s)
    {
        var innen = s.Innen!;
        foreach (var p in Enumerable.Range(0, 900).Select(i => new System.Numerics.Vector2(0.6f + (i % 30 * 0.3f), 0.6f + (i / 30 * 0.3f)))
            .Where(p => p.X < innen.Breite && p.Y < innen.Tiefe))
        {
            if (!s.GeheZu(p.X, p.Y))
            {
                continue;
            }
            SpielHaltungBedienungTests.Ankommen(s);
            var blick = s.FigurRichtung * MathF.PI / 180f;
            var hier = new System.Numerics.Vector2(s.FigurX, s.FigurZ);
            if (innen.KoerperFrei(hier, blick, Koerperhaltung.Kriechen) && innen.KoerperFrei(hier, blick, Koerperhaltung.Ducken))
            {
                return s;
            }
        }
        Assert.Fail("keine Stelle mit Platz zum Kriechen");
        return s;
    }

    [Fact]
    public void KnopfHaltung_Tippen_Lang_Aufstehen()
    {
        Assert.Equal(Tastenbelegung.Ducken, HaltungKnopf.Aufgabe(Koerperhaltung.Stehen, lang: false));
        Assert.Equal(Tastenbelegung.Kriechen, HaltungKnopf.Aufgabe(Koerperhaltung.Stehen, lang: true));
        Assert.Equal(Tastenbelegung.Ducken, HaltungKnopf.Aufgabe(Koerperhaltung.Ducken, lang: false));
        Assert.Equal(Tastenbelegung.Kriechen, HaltungKnopf.Aufgabe(Koerperhaltung.Ducken, lang: true));
        Assert.Equal(Tastenbelegung.Kriechen, HaltungKnopf.Aufgabe(Koerperhaltung.Kriechen, lang: false));
        Assert.Null(HaltungKnopf.Aufgabe(Koerperhaltung.Kriechen, lang: true));
        // im Spiel: tippen = ducken, noch einmal tippen = aufstehen, lang = kriechen, tippen = aufstehen
        var s = FreieStelle(SpielHaltungBedienungTests.ZuHause(SpielHaltungBedienungTests.NeuesSpiel()));
        void Druecken(bool lang)
        {
            if (HaltungKnopf.Aufgabe(s.Koerper, lang) is { } a)
            {
                Assert.True(a == Tastenbelegung.Ducken ? s.Ducken() : s.Kriechen(), s.HaltungHinweis);
            }
        }
        Druecken(false);
        Assert.Equal(Koerperhaltung.Ducken, s.Koerper);
        Druecken(false);
        Assert.Equal(Koerperhaltung.Stehen, s.Koerper);
        Druecken(true);
        Assert.Equal(Koerperhaltung.Kriechen, s.Koerper);
        Druecken(true);
        Assert.Equal(Koerperhaltung.Kriechen, s.Koerper);
        Druecken(false);
        Assert.Equal(Koerperhaltung.Stehen, s.Koerper);
        Assert.Equal(SpielProbeTexte.DuStehst, s.HaltungHinweis);
    }

    /// <summary>Funde: Knopf (Kreis) ausserhalb des Bilds, unter der Kopfzeile, auf einem Punkt, an dem ein Stick beginnt, oder auf einem anderen.</summary>
    private static List<string> LageFunde(string name, Sticks.Bereich b, HaltungKnopf.Lagen lage)
    {
        var funde = new List<string>();
        var (g, k) = HaltungKnopf.Groessen(b.Breite, b.Hoehe);
        var kreise = new List<(string Wer, (double X, double Y) M, double D)> { ("Springen", lage.Springen, g), ("Hinknien", lage.Knien, k) };
        if (lage.Liegen is { } liegen)
        {
            kreise.Add(("Hinlegen", liegen, k));
        }
        foreach (var (wer, (x, y), d) in kreise)
        {
            var r = d / 2;
            // A 1.0.0 (Sammelliste 405): bis in die Ecke neben der Leiste, nie auf der Leiste, Rand zum Bild
            var leiste = b.LeisteBis > b.LeisteVon ? (b.LeisteVon, b.Hoehe - b.Unten, b.LeisteBis, b.Hoehe) : (0, b.Hoehe - b.Unten, b.Breite, b.Hoehe);
            if (x - r < 0 || x + r > b.Breite || y - r < b.Oben || y + r > b.Hoehe - HaltungKnopf.Rand + 1e-9 || Sticks.KreisInRechteck(x, y, r, leiste))
            {
                funde.Add($"{name} {wer}: ausserhalb des freien Bereichs");
            }
            for (var w = 0; w < 360; w += 10)
            {
                for (var a = 0.0; a <= r; a += r / 4)
                {
                    var px = x + (a * Math.Cos(w * Math.PI / 180));
                    var py = y + (a * Math.Sin(w * Math.PI / 180));
                    if (Sticks.SeiteFuer(px, py, b with { Sperre = HaltungKnopf.Sperre(b) }) is { } seite)
                    {
                        funde.Add(string.Create(CultureInfo.InvariantCulture, $"{name} {wer}: bei {px:F0}/{py:F0} beginnt der Stick {seite}"));
                        w = 360;
                        break;
                    }
                }
            }
        }
        for (var i = 0; i < kreise.Count; i++)
        {
            for (var j = i + 1; j < kreise.Count; j++)
            {
                var abstand = Math.Sqrt(Math.Pow(kreise[i].M.X - kreise[j].M.X, 2) + Math.Pow(kreise[i].M.Y - kreise[j].M.Y, 2));
                if (abstand < ((kreise[i].D + kreise[j].D) / 2) + HaltungKnopf.Rand - 1e-6)
                {
                    funde.Add($"{name}: {kreise[i].Wer} und {kreise[j].Wer} naeher als der Rand");
                }
            }
        }
        return funde;
    }

    private static readonly (string Name, double W, double H)[] Geraete =
        [("S24 quer", 832.0, 384.0), ("S24 hoch", 384.0, 832.0), ("Tab quer", 1280.0, 800.0), ("Tab hoch", 800.0, 1280.0), ("PC", 1360.0, 880.0), ("Emulator", 640.0, 320.0)];

    /// <summary>
    /// Welle A 1.7.0 (Sammelliste 447, Weisung P12-367, Nico 1a: Anordnung B). Abnahmesatz: Springen ist der groesste Knopf und
    /// sitzt ganz unten rechts in der Ecke (Daumenruhe, Rand 16; nur ueber einer Leiste darunter hoeher), Hinknien und Hinlegen
    /// sind kleiner und stehen im Bogen darueber (Hinknien links daneben, Hinlegen fast senkrecht darueber, Abstand 16 zwischen den
    /// Kreisen); kein Knopf liegt auf der Leiste, unter der Kopfzeile oder auf einer Stelle, an der ein Stick beginnt; der rechte
    /// Stick beginnt weiter im unteren rechten Bereich und sein Kreis beruehrt keinen Knopf. Groessen Handy 64/48, Tablet 76/56.
    /// Gegenprobe Stand A 1.2.0 bis A 1.6.0 (zwei Knoepfe ueber dem Stick): Springen steht nicht in der Ecke.
    /// </summary>
    [Fact]
    public void Lage_AnordnungB_SpringenInDerEcke_BogenDarueber_StickFrei_Gegenprobe()
    {
        var funde = new List<string>();
        var gegen = 0;
        var faelle = 0;
        foreach (var (name, w, h) in Geraete)
        {
            foreach (var leiste in new[] { 180.0, 420.0, 0.0 })
            {
                var b = leiste > 0 ? new Sticks.Bereich(w, h, 64, 86, (w - leiste) / 2, (w + leiste) / 2) : new Sticks.Bereich(w, h, 64, 86);
                var wo = $"{name} Leiste {leiste}";
                faelle++;
                if (HaltungKnopf.Lage(b) is not { Liegen: { } liegen } lage)
                {
                    funde.Add(wo + ": keine Lage mit drei Knoepfen");
                    continue;
                }
                var (g, k) = HaltungKnopf.Groessen(w, h);
                Assert.Equal(Math.Min(w, h) >= 600 ? (76.0, 56.0) : (64.0, 48.0), (g, k));
                funde.AddRange(LageFunde(wo, b, lage));
                var s = lage.Springen;
                var rechts = Math.Abs(s.X + (g / 2) - (w - HaltungKnopf.Rand)) < 1e-6;
                var unten = Math.Abs(s.Y + (g / 2) - (h - HaltungKnopf.Rand)) < 1e-6 || Math.Abs(s.Y + (g / 2) - (h - b.Unten - HaltungKnopf.Rand)) < 1e-6;
                if (!rechts || !unten)
                {
                    funde.Add(wo + string.Create(CultureInfo.InvariantCulture, $": Springen nicht in der Ecke ({s.X:F0}/{s.Y:F0})"));
                }
                var r = (g / 2) + HaltungKnopf.Rand + (k / 2);
                foreach (var (wer, m, grad) in new[] { ("Hinknien", lage.Knien, HaltungKnopf.KnienWinkel), ("Hinlegen", liegen, HaltungKnopf.LiegenWinkel) })
                {
                    var (dx, dy) = (m.X - s.X, s.Y - m.Y);
                    var winkel = Math.Atan2(dy, dx) * 180 / Math.PI;
                    if (Math.Abs(Math.Sqrt((dx * dx) + (dy * dy)) - r) > 1e-6 || Math.Abs(winkel - grad) > 1e-6)
                    {
                        funde.Add(wo + string.Create(CultureInfo.InvariantCulture, $": {wer} nicht im Bogen ({winkel:F1} Grad)"));
                    }
                }
                if (!(lage.Knien.X < s.X && lage.Knien.Y < s.Y && liegen.Y < lage.Knien.Y))
                {
                    funde.Add(wo + ": Reihenfolge im Bogen falsch");
                }
                // der rechte Stick: jeder Daumen, der rechts unten einen Stick beginnt, bekommt einen Kreis ohne Knopf darin
                var mit = b with { Sperre = HaltungKnopf.Sperre(b) };
                var sticks = 0;
                for (var px = (w / 2) + 4; px < w; px += 8)
                {
                    for (var py = Sticks.ZoneOben(b); py < h; py += 8)
                    {
                        if (Sticks.SeiteFuer(px, py, mit) != Sticks.Seite.Rechts)
                        {
                            continue;
                        }
                        sticks++;
                        var (mx, my, mr) = Sticks.Lage(px, py, Sticks.Seite.Rechts, mit);
                        foreach (var ((x, y), d) in new[] { (s, g), (lage.Knien, k), (liegen, k) })
                        {
                            if (Math.Sqrt(((x - mx) * (x - mx)) + ((y - my) * (y - my))) < mr + (d / 2) - 1e-6)
                            {
                                funde.Add(wo + string.Create(CultureInfo.InvariantCulture, $": Stick bei {px:F0}/{py:F0} beruehrt einen Knopf"));
                                px = w;
                                py = h;
                                break;
                            }
                        }
                    }
                }
                if (sticks == 0)
                {
                    funde.Add(wo + ": rechts beginnt nirgends ein Stick");
                }
                // Gegenprobe Stand A 1.2.0 bis A 1.6.0: zwei Knoepfe, Springen ueber dem Stick (nicht in der Ecke)
                HaltungKnopf.ZweiKnoepfeFuerTests = true;
                try
                {
                    if (HaltungKnopf.Lage(b) is { Liegen: null } alt && Math.Abs(alt.Springen.Y - s.Y) > 1)
                    {
                        gegen++;
                    }
                }
                finally
                {
                    HaltungKnopf.ZweiKnoepfeFuerTests = false;
                }
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        Assert.True(gegen >= faelle / 2, $"Gegenprobe greift nicht: {gegen} von {faelle}");
    }

    /// <summary>
    /// Welle A 1.7.0 (Nico 4a): am PC drei kleine Symbole unten rechts (von rechts Springen, Hinknien, Hinlegen), je mit Platz fuer
    /// die Taste darunter, ganz im Bild, nicht auf der Leiste und nicht uebereinander.
    /// </summary>
    [Fact]
    public void LageTasten_DreiSymboleUntenRechts_PlatzFuerDieTaste()
    {
        foreach (var (w, h) in new[] { (1280.0, 800.0), (1360.0, 880.0), (1920.0, 1080.0), (1024.0, 700.0) })
        {
            foreach (var leiste in new[] { 160.0, 0.0 })
            {
                var b = leiste > 0 ? new Sticks.Bereich(w, h, 64, 86, (w - leiste) / 2, (w + leiste) / 2) : new Sticks.Bereich(w, h, 64, 86);
                var l = HaltungKnopf.LageTasten(b);
                Assert.True(l is { Liegen: not null }, $"{w}x{h} Leiste {leiste}: keine Lage");
                var (g, k) = HaltungKnopf.GroessenTasten;
                var s = l!.Value.Springen;
                var kn = l.Value.Knien;
                var li = l.Value.Liegen!.Value;
                Assert.Equal(w - HaltungKnopf.Rand, s.X + (g / 2), 6);
                Assert.True(s.Y + (g / 2) + HaltungKnopf.TasteHoehe <= h - HaltungKnopf.Rand + 1e-6, "Taste unter Springen ragt aus dem Bild");
                Assert.True(li.X + (k / 2) + HaltungKnopf.AbstandTasten <= kn.X - (k / 2) + 1e-6 && kn.X + (k / 2) + HaltungKnopf.AbstandTasten <= s.X - (g / 2) + 1e-6, "Symbole zu nah");
                Assert.Equal(s.Y + (g / 2), kn.Y + (k / 2), 6);
                Assert.Equal(s.Y + (g / 2), li.Y + (k / 2), 6);
                var leisteOben = h - b.Unten;
                var aufLeiste = leiste > 0 && li.X - (k / 2) < b.LeisteBis && s.Y + (g / 2) + HaltungKnopf.TasteHoehe > leisteOben;
                Assert.False(aufLeiste, $"{w}x{h}: Symbole auf der Leiste");
            }
        }
    }

    /// <summary>
    /// Welle A 1.7.0 (Konzept Punkt 3 und 4, Nico 2a): Hinknien tippen = hinknien bzw. aufstehen (liegend: hinknien), halten oder
    /// nach unten wischen = hinlegen; Hinlegen tippen = hinlegen bzw. aufstehen; das Symbol zeigt die naechste Haltung, die aktuelle
    /// leuchtet. Im Spiel durchgespielt (Wohnung).
    /// </summary>
    [Fact]
    public void DreiKnoepfe_Aufgaben_SymbolZeigtNaechsteHaltung()
    {
        Assert.Equal(Tastenbelegung.Springen, HaltungKnopf.Aufgabe(HaltungKnopf.Knopf.Springen, Koerperhaltung.Ducken, lang: false));
        Assert.Equal(Tastenbelegung.Ducken, HaltungKnopf.Aufgabe(HaltungKnopf.Knopf.Knien, Koerperhaltung.Stehen, lang: false));
        Assert.Equal(Tastenbelegung.Kriechen, HaltungKnopf.Aufgabe(HaltungKnopf.Knopf.Knien, Koerperhaltung.Stehen, lang: true));
        Assert.Null(HaltungKnopf.Aufgabe(HaltungKnopf.Knopf.Knien, Koerperhaltung.Kriechen, lang: true));
        Assert.Equal(Tastenbelegung.Kriechen, HaltungKnopf.Aufgabe(HaltungKnopf.Knopf.Liegen, Koerperhaltung.Ducken, lang: false));
        Assert.Equal((false, false), HaltungKnopf.Zustand(HaltungKnopf.Knopf.Knien, Koerperhaltung.Kriechen));
        Assert.Equal((true, true), HaltungKnopf.Zustand(HaltungKnopf.Knopf.Liegen, Koerperhaltung.Kriechen));
        Assert.Equal((true, true), HaltungKnopf.Zustand(HaltungKnopf.Knopf.Knien, Koerperhaltung.Ducken));
        var s = FreieStelle(SpielHaltungBedienungTests.ZuHause(SpielHaltungBedienungTests.NeuesSpiel()));
        void Druecken(HaltungKnopf.Knopf knopf, bool lang)
        {
            if (HaltungKnopf.Aufgabe(knopf, s.Koerper, lang) is { } a)
            {
                Assert.True(a == Tastenbelegung.Ducken ? s.Ducken() : a == Tastenbelegung.Kriechen ? s.Kriechen() : s.Springen(), s.HaltungHinweis);
            }
        }
        var folge = new (HaltungKnopf.Knopf Knopf, bool Lang, Koerperhaltung Danach)[]
        {
            (HaltungKnopf.Knopf.Knien, false, Koerperhaltung.Ducken),
            (HaltungKnopf.Knopf.Knien, false, Koerperhaltung.Stehen),
            (HaltungKnopf.Knopf.Knien, true, Koerperhaltung.Kriechen),
            (HaltungKnopf.Knopf.Knien, true, Koerperhaltung.Kriechen),
            (HaltungKnopf.Knopf.Knien, false, Koerperhaltung.Ducken),
            (HaltungKnopf.Knopf.Liegen, false, Koerperhaltung.Kriechen),
            (HaltungKnopf.Knopf.Liegen, false, Koerperhaltung.Stehen),
            (HaltungKnopf.Knopf.Liegen, true, Koerperhaltung.Kriechen),
            (HaltungKnopf.Knopf.Liegen, true, Koerperhaltung.Kriechen),
            (HaltungKnopf.Knopf.Liegen, false, Koerperhaltung.Stehen),
        };
        foreach (var (knopf, lang, danach) in folge)
        {
            Druecken(knopf, lang);
            Assert.Equal(danach, s.Koerper);
        }
    }

    /// <summary>
    /// Welle A 1.2.0 (Sammelliste 412, Weisung P12-332, Nico: „Der Joystick sollte da sitzen und die Knoepfe darueber“): der rechte
    /// Stick liegt unten rechts in der Ecke (wie seit A 1.0.0), Springen steht ueber seinem Kreis am rechten Rand, Haltung links
    /// daneben etwas hoeher. Abnahmesatz: der Daumen erreicht den Stick in der Ecke voll, kein Knopf (mit Rand) liegt in seinem
    /// Kreis, und ein Daumen in der Ecke setzt dort den Stick. Gegenprobe Stand A 1.0.0 bis A 1.1.0 (Knoepfe in der Ecke): der
    /// Stick kommt nicht in die Ecke, die Knoepfe liegen in seinem Kreis.
    /// </summary>
    [Fact]
    public void Lage_UeberDemRechtenStick_EckeFrei_Gegenprobe()
    {
        // seit A 1.7.0 nur noch der Stand der Gegenprobe ZweiKnoepfeFuerTests (zwei Knoepfe ueber dem Stick)
        HaltungKnopf.ZweiKnoepfeFuerTests = true;
        try
        {
            ZweiKnoepfeUeberDemStick();
        }
        finally
        {
            HaltungKnopf.ZweiKnoepfeFuerTests = false;
        }
    }

    private static void ZweiKnoepfeUeberDemStick()
    {
        var funde = new List<string>();
        var gegen = 0;
        var groessen = new[] { ("S24 quer", 832.0, 384.0), ("S24 hoch", 384.0, 832.0), ("Tab quer", 1280.0, 800.0), ("Tab hoch", 800.0, 1280.0), ("PC", 1360.0, 880.0), ("Emulator", 640.0, 320.0) };
        foreach (var (name, w, h) in groessen)
        {
            foreach (var leiste in new[] { 180.0, 420.0, 0.0 })
            {
                var b = leiste > 0 ? new Sticks.Bereich(w, h, 64, 86, (w - leiste) / 2, (w + leiste) / 2) : new Sticks.Bereich(w, h, 64, 86);
                var wo = $"{name} Leiste {leiste}";
                if (HaltungKnopf.Lage(b) is not { } lage)
                {
                    funde.Add(wo + ": keine Lage");
                    continue;
                }
                var (s, hl) = (lage.Springen, lage.Knien);
                var (g, k) = HaltungKnopf.Groesse(w, h);
                Assert.True(hl.X < s.X && hl.Y <= s.Y, wo + ": Haltung links neben/ueber Springen");
                funde.AddRange(LageFunde(wo, b, lage));
                // der rechte Stick in der Ecke: ein Daumen ganz unten rechts setzt ihn dort, kein Knopf liegt in seinem Kreis
                var mit = b with { Sperre = HaltungKnopf.Sperre(b) };
                var (ex, ey, er) = Sticks.Lage(w, h, Sticks.Seite.Rechts, b);
                // Springen am rechten Rand ueber dem Stick, nur im sehr flachen Bild (Emulator) links neben seinem Kreis
                if (Math.Abs(w - HaltungKnopf.Rand - (s.X + (g / 2))) > 1e-6 && (name != "Emulator" || s.X + (g / 2) > ex - er - HaltungKnopf.Rand + 1e-6))
                {
                    funde.Add(wo + $": Springen nicht am rechten Rand ueber dem Stick ({s.X:F0}/{s.Y:F0})");
                }
                var daumen = (X: ex + (er / 2), Y: ey + (er / 2));
                if (Sticks.SeiteFuer(daumen.X, daumen.Y, mit) != Sticks.Seite.Rechts)
                {
                    funde.Add(wo + ": in der Ecke beginnt kein Stick");
                }
                var (mx, my, mr) = Sticks.Lage(daumen.X, daumen.Y, Sticks.Seite.Rechts, mit);
                if (Math.Abs(mx - ex) > 1e-6 || Math.Abs(my - ey) > 1e-6 || Math.Abs(mr - er) > 1e-6)
                {
                    funde.Add(wo + $": Stick nicht in der Ecke ({mx:F0}/{my:F0} statt {ex:F0}/{ey:F0})");
                }
                foreach (var ((x, y), d) in new[] { (s, g), (hl, k) })
                {
                    if (Math.Sqrt(((x - ex) * (x - ex)) + ((y - ey) * (y - ey))) < er + (d / 2) + HaltungKnopf.Rand - 1e-6)
                    {
                        funde.Add(wo + ": Knopf im Kreis des Sticks");
                    }
                }
                // Gegenprobe Stand A 1.0.0 bis A 1.1.0: Knoepfe in der Ecke, dort hat der Stick keinen Platz
                HaltungKnopf.EckeFuerTests = true;
                try
                {
                    if (HaltungKnopf.Lage(b) is { } alt
                        && new[] { (alt.Springen, g), (alt.Knien, k) }.Any(p => Math.Sqrt(((p.Item1.X - ex) * (p.Item1.X - ex)) + ((p.Item1.Y - ey) * (p.Item1.Y - ey))) < er + (p.Item2 / 2)))
                    {
                        gegen++;
                    }
                }
                finally
                {
                    HaltungKnopf.EckeFuerTests = false;
                }
            }
            Assert.Equal(Math.Min(w, h) >= 600 ? (64.0, 46.0) : (56.0, 40.0), HaltungKnopf.Groesse(w, h));
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        Assert.True(gegen >= 6, "Gegenprobe greift nicht: " + gegen);
    }

    [Fact]
    public void Lage_FlachesBild_EmulatorPasst_Gegenprobe()
    {
        // Lauf 1118: Emulator der Touch-Probe 640 x 320 Punkte, frei 170 (Kopfzeile 64, Leiste 86); dort lag Haltung schraeg
        // ueber der Kopfzeile, die Knoepfe fehlten. Jetzt steigt Haltung nur so weit, wie Platz ist
        var b = new Sticks.Bereich(640, 320, 64, 86, 230, 410);
        var lage = HaltungKnopf.Lage(b);
        Assert.True(lage is not null, "Emulator: keine Lage");
        var (s, h) = (lage!.Value.Springen, lage.Value.Knien);
        Assert.True(h.X < s.X && h.Y <= s.Y, "Hinknien links neben/ueber Springen");
        Assert.True(lage.Value.Liegen is not null, "Emulator: Hinlegen fehlt");
        var funde = LageFunde("Emulator", b, lage.Value);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        // Gegenprobe: Stand A 0.99.0 nur schraeg (vor Lauf 1118) gibt es im Emulator keine Lage
        HaltungKnopf.AltFuerTests = true;
        HaltungKnopf.NurSchraegFuerTests = true;
        try
        {
            Assert.Null(HaltungKnopf.Lage(b));
        }
        finally
        {
            HaltungKnopf.NurSchraegFuerTests = false;
            HaltungKnopf.AltFuerTests = false;
        }
    }

    [Fact]
    public void Leuchten_SymbolAufGrundfarbe_KontrastInJederFarbwelt_Gegenprobe()
    {
        var funde = new List<string>();
        var nurWeiss = 0;
        var welten = 0;
        foreach (var g in Farbwelt.Grundfarben)
        {
            foreach (var h in Farbwelt.Hintergruende)
            {
                foreach (var d in new[] { Farbwelt.Hell, Farbwelt.Dunkel })
                {
                    welten++;
                    var akzent = Farbwelt.Berechne(d, g.Id, h.Id).Farben["accent"];
                    var symbol = Symbolfarbe.Auf(akzent);
                    var k = Farbwelt.Kontrast(symbol, akzent);
                    if (k < Farbwelt.MindestKontrast)
                    {
                        funde.Add(string.Create(CultureInfo.InvariantCulture, $"{g.Id}/{h.Id}/{d}: {symbol} auf {akzent} nur {k:F2}"));
                    }
                    if (Farbwelt.Kontrast(Symbolfarbe.Hell, akzent) < Farbwelt.MindestKontrast)
                    {
                        nurWeiss++;
                    }
                }
            }
        }
        Assert.True(welten >= 72, "Farbwelten: " + welten);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        // Gegenprobe: immer weiss waere in vielen Farbwelten zu schwach
        Assert.True(nurWeiss > 0, "Gegenprobe: weiss reicht ueberall");
    }
}
