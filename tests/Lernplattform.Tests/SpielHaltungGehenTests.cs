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
        var s = SpielHaltungBedienungTests.ZuHause(SpielHaltungBedienungTests.NeuesSpiel());
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

    /// <summary>Funde: Knopf (Kreis) ausserhalb des Bilds, unter der Kopfzeile, auf einem Punkt, an dem ein Stick beginnt, oder auf dem anderen.</summary>
    private static List<string> LageFunde(string name, Sticks.Bereich b, ((double X, double Y) S, (double X, double Y) H) lage)
    {
        var funde = new List<string>();
        var (g, k) = HaltungKnopf.Groesse(b.Breite, b.Hoehe);
        foreach (var (wer, (x, y), d) in new[] { ("Springen", lage.S, g), ("Haltung", lage.H, k) })
        {
            var r = d / 2;
            if (x - r < 0 || x + r > b.Breite || y - r < b.Oben || y + r > b.Hoehe - b.Unten)
            {
                funde.Add($"{name} {wer}: ausserhalb des freien Bereichs");
            }
            for (var w = 0; w < 360; w += 10)
            {
                for (var a = 0.0; a <= r; a += r / 4)
                {
                    var px = x + (a * Math.Cos(w * Math.PI / 180));
                    var py = y + (a * Math.Sin(w * Math.PI / 180));
                    if (Sticks.SeiteFuer(px, py, b) is { } seite)
                    {
                        funde.Add(string.Create(CultureInfo.InvariantCulture, $"{name} {wer}: bei {px:F0}/{py:F0} beginnt der Stick {seite}"));
                        w = 360;
                        break;
                    }
                }
            }
        }
        var abstand = Math.Sqrt(Math.Pow(lage.S.X - lage.H.X, 2) + Math.Pow(lage.S.Y - lage.H.Y, 2));
        if (abstand < (g + k) / 2)
        {
            funde.Add($"{name}: Knoepfe ueberlappen");
        }
        return funde;
    }

    [Fact]
    public void Lage_RechtsUeberDemStick_KeinStickBereich_Gegenprobe()
    {
        var funde = new List<string>();
        foreach (var (name, w, h) in new[] { ("S24 quer", 832.0, 384.0), ("S24 hoch", 384.0, 832.0), ("Tab quer", 1280.0, 800.0), ("Tab hoch", 800.0, 1280.0), ("PC", 1360.0, 880.0) })
        {
            var b = new Sticks.Bereich(w, h, 64, 86, (w / 2) - 90, (w / 2) + 90);
            var lage = HaltungKnopf.Lage(b);
            Assert.True(lage is not null, name + ": keine Lage");
            var (s, hl) = lage!.Value;
            // rechts (Daumen rechts), Springen aussen, Haltung links darueber
            Assert.True(s.X > w * 0.8 && hl.X < s.X && hl.Y < s.Y, name);
            funde.AddRange(LageFunde(name, b, lage.Value));
            Assert.Equal(Math.Min(w, h) >= 600 ? (72.0, 52.0) : (64.0, 46.0), HaltungKnopf.Groesse(w, h));
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        // Gegenprobe: 40 Punkte tiefer laege Springen im Bereich des rechten Sticks
        var bq = new Sticks.Bereich(832, 384, 64, 86, 326, 506);
        var l = HaltungKnopf.Lage(bq)!.Value;
        Assert.Contains(LageFunde("Gegenprobe", bq, ((l.Springen.X, l.Springen.Y + 40), l.Haltung)), f => f.Contains("Stick", StringComparison.Ordinal));
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
