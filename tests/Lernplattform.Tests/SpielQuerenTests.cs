using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Patch A 0.75.1 (Sammelliste 342, Weisung P12-258, Nicos Abnahmesatz): die Spielfigur betritt Fahrbahn und Gleis nie, weder
/// beim Gehen von Ort zu Ort noch nach einem Tipp auf die Stadt; sie quert nur auf einem Zebrastreifen, an einer Ampel oder auf
/// einem Bahnuebergang. Geprueft wird die Lage der Figur, wie sie im Bild laeuft (alle 0,02 auf jedem Stueck des Wegs), gegen
/// die Strassen und Gleise der Szene mit einer eigenen Rechnung (nicht dem Laufraster): auf der Fahrbahn ist ein Punkt naeher
/// als die halbe Strassenbreite an der Strassenmitte (Spielraum 0,02), ausser er liegt auf einem Uebergang (laengs 0,02 Spielraum);
/// auf dem Gleis ist ein Punkt naeher als die halbe Gleisbreite, ausser auf einer Strasse (Bahnuebergang). Grenze 0.
/// Gegenprobe: mit den Wegen bis A 0.75.0 (<see cref="StadtSzene.QuerenAltFuerTests"/>) gibt es Treffer.
/// </summary>
[Collection("Haenger")]
public sealed class SpielQuerenTests
{
    private const float Spielraum = 0.02f;

    /// <summary>Was an der Stelle verboten ist (null = erlaubt).</summary>
    internal static string? Verboten(StadtSzene sz, Vector2 p)
    {
        var aufUebergang = sz.Zebras.Concat(sz.Ampeln.SelectMany(a => a.Furten)).Any(z => z.Drauf(p, Spielraum, 0.3f));
        if (!aufUebergang && sz.Strassen.Any(s => s.Count >= 2 && Kurve.Abstand(p, s) < (StadtSzene.StrassenBreite / 2) - Spielraum))
        {
            return "Fahrbahn";
        }
        if (sz.Gleise.Any(g => g.Count >= 2 && Kurve.Abstand(p, g) < (StadtSzene.GleisBreite / 2) - Spielraum)
            && !sz.Strassen.Any(s => s.Count >= 2 && Kurve.Abstand(p, s) < (StadtSzene.StrassenBreite / 2) + 0.4f))
        {
            return "Gleis";
        }
        return null;
    }

    /// <summary>Die Figur geht ihren Weg ganz ab; jede Lage (und alle 0,02 dazwischen) wird geprueft. Rueckgabe: Treffer.</summary>
    private static List<string> Abgehen(StadtSpiel spiel, string was)
    {
        var treffer = new List<string>();
        var vorher = new Vector2(spiel.FigurX, spiel.FigurZ);
        for (var i = 0; i < 200_000 && spiel.Geht; i++)
        {
            spiel.Fortschreiten(0.01);
            var jetzt = new Vector2(spiel.FigurX, spiel.FigurZ);
            var n = Math.Max(1, (int)(Vector2.Distance(vorher, jetzt) / 0.02f));
            for (var k = 1; k <= n; k++)
            {
                var p = Vector2.Lerp(vorher, jetzt, k / (float)n);
                if (Verboten(spiel.Szene, p) is { } v && treffer.Count < 5)
                {
                    treffer.Add(string.Create(CultureInfo.InvariantCulture, $"{was}: {v} bei {p.X:F2}/{p.Y:F2}"));
                }
            }
            vorher = jetzt;
        }
        Assert.False(spiel.Geht, was + ": Weg endet nicht");
        return treffer;
    }

    /// <summary>Von jedem Ort zu jedem anderen Ort gehen (die Figur kommt aus dem Haus); Treffer und Zahl der Wege.</summary>
    private static (List<string> Treffer, int Wege) OrtZuOrt(Func<StadtSpiel> neu)
    {
        var treffer = new List<string>();
        var wege = 0;
        var orte = neu().Szene.Orte.Select(o => o.Id).ToList();
        foreach (var a in orte)
        {
            var spiel = neu();
            if (spiel.GeheZu(a) != StadtGrund.Ok)
            {
                continue;
            }
            spiel.SofortAnkommen();
            foreach (var b in orte.Where(b => b != a))
            {
                var von = spiel.Jetzt.Ort;
                if (spiel.GeheZu(b) != StadtGrund.Ok)
                {
                    continue;
                }
                wege++;
                treffer.AddRange(Abgehen(spiel, von + " -> " + b));
            }
        }
        return (treffer, wege);
    }

    /// <summary>Von jedem Ort aus auf Punkte der Stadt tippen (alle 3 Einheiten, auch auf Strassen); Treffer und Zahl der Wege.</summary>
    private static (List<string> Treffer, int Wege) Antippen(Func<StadtSpiel> neu)
    {
        var treffer = new List<string>();
        var wege = 0;
        var probe = neu();
        var orte = probe.Szene.Orte.Select(o => o.Id).ToList();
        foreach (var a in orte.Take(4))
        {
            var spiel = neu();
            if (spiel.GeheZu(a) != StadtGrund.Ok)
            {
                continue;
            }
            spiel.SofortAnkommen();
            spiel.Verlassen();
            for (var x = 1.5; x < probe.Szene.Breite; x += 3)
            {
                for (var z = 1.5; z < probe.Szene.Tiefe; z += 3)
                {
                    var start = (spiel.FigurX, spiel.FigurZ);
                    if (!spiel.GeheZu(x, z))
                    {
                        continue;
                    }
                    wege++;
                    treffer.AddRange(Abgehen(spiel, string.Create(CultureInfo.InvariantCulture, $"{start.FigurX:F1}/{start.FigurZ:F1} -> Tipp {x:F1}/{z:F1}")));
                }
            }
        }
        return (treffer, wege);
    }

    [Fact]
    public void Stadt1_VonOrtZuOrt_NieAufFahrbahnOderGleis_Gegenprobe()
    {
        var sz = SpielMitfahrtTests.Stadt1().Szene;
        Assert.True(sz.Zebras.Count > 0 && sz.Gleise.Count > 0, "Stadt 1 hat Zebrastreifen und Gleise");
        Assert.Empty(sz.FusswegeUeberGleis);
        var (treffer, wege) = OrtZuOrt(() => SpielMitfahrtTests.Stadt1());
        Assert.True(wege >= sz.Orte.Count * (sz.Orte.Count - 1) / 2, "zu wenige Wege: " + wege);
        Assert.True(treffer.Count == 0, string.Join("\n", treffer.Take(20)));
        StadtSzene.QuerenAltFuerTests = true;
        try
        {
            var (alt, _) = OrtZuOrt(() => SpielMitfahrtTests.Stadt1());
            Assert.True(alt.Count > 0, "Gegenprobe: mit den Wegen bis A 0.75.0 laeuft die Figur ueber die Fahrbahn");
        }
        finally
        {
            StadtSzene.QuerenAltFuerTests = false;
        }
    }

    [Fact]
    public void Stadt1_NachTippen_NieAufFahrbahnOderGleis_Gegenprobe()
    {
        var (treffer, wege) = Antippen(() => SpielMitfahrtTests.Stadt1());
        Assert.True(wege > 100, "zu wenige Wege: " + wege);
        Assert.True(treffer.Count == 0, string.Join("\n", treffer.Take(20)));
        StadtSzene.QuerenAltFuerTests = true;
        try
        {
            var (alt, _) = Antippen(() => SpielMitfahrtTests.Stadt1());
            Assert.True(alt.Count > 0, "Gegenprobe: mit den Wegen bis A 0.75.0 tritt die Figur auf die Fahrbahn");
        }
        finally
        {
            StadtSzene.QuerenAltFuerTests = false;
        }
    }

    /// <summary>Keine Insel: jeder Ort ist von jedem anderen zu Fuss ueber Gehwege und Uebergaenge erreichbar (sonst Fund).</summary>
    [Fact]
    public void Stadt1_JederOrtZuFussErreichbar_KeineInsel()
    {
        var sz = SpielMitfahrtTests.Stadt1().Szene;
        var funde = new List<string>();
        foreach (var a in sz.Orte)
        {
            foreach (var b in sz.Orte.Where(b => b.Id != a.Id))
            {
                if (sz.LaufWeg((a.StehX, a.StehZ), (b.StehX, b.StehZ)).Count == 0)
                {
                    funde.Add(a.Id + " -> " + b.Id);
                }
            }
        }
        Assert.True(funde.Count == 0, "ohne Weg zu Fuss: " + string.Join(", ", funde));
    }

    /// <summary>Die Querung geht ueber die Mitte des Zebrastreifens, quer zur Strasse, von Bordstein zu Bordstein.</summary>
    [Fact]
    public void Stadt1_QuerungUeberDieMitteDesZebrastreifens()
    {
        var sz = SpielMitfahrtTests.Stadt1().Szene;
        var geprueft = 0;
        foreach (var q in sz.Zebras)
        {
            var halb = (StadtSzene.StrassenBreite / 2) + 0.3f;
            var a = q.Mitte + (q.Quer * halb);
            var b = q.Mitte - (q.Quer * halb);
            var weg = sz.LaufWeg((a.X, a.Y), (b.X, b.Y));
            var punkte = new List<Vector2> { a };
            punkte.AddRange(weg.Select(p => new Vector2((float)p.X, (float)p.Y)));
            for (var i = 0; i + 1 < punkte.Count; i++)
            {
                for (var k = 0; k <= 50; k++)
                {
                    var p = Vector2.Lerp(punkte[i], punkte[i + 1], k / 50f);
                    if (MathF.Abs(Vector2.Dot(p - q.Mitte, q.Quer)) < StadtSzene.StrassenBreite / 2)
                    {
                        Assert.True(MathF.Abs(Vector2.Dot(p - q.Mitte, q.Richtung)) < 0.01f,
                            string.Create(CultureInfo.InvariantCulture, $"Zebrastreifen {q.Nr}: auf der Fahrbahn {Vector2.Dot(p - q.Mitte, q.Richtung):F3} neben der Mitte"));
                        geprueft++;
                    }
                }
            }
        }
        Assert.True(geprueft > 0);
    }

    /// <summary>Ein Punkt des Wegs (alle 0,02) liegt auf dem Gleis ausserhalb einer Strasse.</summary>
    private static bool AufGleis(StadtSzene sz, IReadOnlyList<IReadOnlyList<Vector2>> ausnahmen, (double X, double Y) start, IReadOnlyList<(double X, double Y)> weg, float zugabe)
    {
        var vorher = new Vector2((float)start.X, (float)start.Y);
        foreach (var w in weg)
        {
            var jetzt = new Vector2((float)w.X, (float)w.Y);
            var n = Math.Max(1, (int)(Vector2.Distance(vorher, jetzt) / 0.02f));
            for (var k = 0; k <= n; k++)
            {
                var p = Vector2.Lerp(vorher, jetzt, k / (float)n);
                // Fassung 0 hat keine Gehwege: Bahnuebergang = nur die Strasse selbst (strenger als Verboten mit Gehweg-Zugabe)
                if (sz.Gleise.Any(g => g.Count >= 2 && Kurve.Abstand(p, g) < (StadtSzene.GleisBreite / 2) + zugabe)
                    && !sz.Strassen.Any(st => st.Count >= 2 && Kurve.Abstand(p, st) < (StadtSzene.StrassenBreite / 2) - zugabe)
                    && !ausnahmen.Any(z => Kurve.Abstand(p, z) <= (StadtSzene.WegBreite / 2) - zugabe))
                {
                    return true;
                }
            }
            vorher = jetzt;
        }
        return false;
    }

    /// <summary>
    /// Fassung 0 (ohne Gehwege und Zebrastreifen, die Strassen sind dort zum Gehen frei): Wege genau wie bis A 0.75.0, ausser ein
    /// alter Weg lief ueber das Gleis oder bis auf die Breite eines Feldes der feinen Sperre (0,05) heran; der neue laeuft nie
    /// darueber (Grenze 0, Spielraum 0,02; Gegenprobe: solche alten Wege gibt es). Feste
    /// Ausnahme (Fund, Fassung 0 wird nie geaendert): die Zugaenge zu Birkenhain, Talheim und Talheim-Nord queren das Gleis als
    /// gezeichneter Fussweg (<see cref="StadtSzene.FusswegeUeberGleis"/>); ohne sie waeren diese Orte Inseln.
    /// </summary>
    [Fact]
    public void Fassung0_WegeUnveraendert_AusserUeberDasGleis()
    {
        var sz = new StadtSpiel(new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value), new Spielablauf(SpielInhaltTests.Inhalt), null,
            SpielInnenraumTests.Innenraeume.Value).Szene;
        Assert.Empty(sz.Querungen);
        // Fund: genau diese drei Zugaenge queren das Gleis abseits einer Strasse (sonst waeren Birkenhain, Talheim und
        // Talheim-Nord Inseln); feste Ausnahme, weil Fassung 0 nie geaendert wird
        var ausnahmen = sz.FusswegeUeberGleis.ToList();
        Assert.Equal(["12.6/8.8", "19.9/9.3", "30.2/5.55"], ausnahmen.Select(w => string.Create(CultureInfo.InvariantCulture, $"{w[0].X:0.##}/{w[0].Y:0.##}")).Order());
        var (gleich, korrigiert) = (0, 0);
        foreach (var a in sz.Orte)
        {
            for (var x = 1.0; x < sz.Breite; x += 2.5)
            {
                for (var z = 1.0; z < sz.Tiefe; z += 2.5)
                {
                    var start = (a.StehX, a.StehZ);
                    var neu = sz.LaufWeg(start, (x, z));
                    StadtSzene.QuerenAltFuerTests = true;
                    var alt = sz.LaufWeg(start, (x, z));
                    StadtSzene.QuerenAltFuerTests = false;
                    Assert.False(AufGleis(sz, ausnahmen, start, neu, -Spielraum), string.Create(CultureInfo.InvariantCulture, $"{a.Id} -> {x:F1}/{z:F1}: neuer Weg auf dem Gleis"));
                    if (AufGleis(sz, ausnahmen, start, alt, StadtSzene.FeinRaster))
                    {
                        korrigiert++;
                        continue;
                    }
                    Assert.Equal(alt, neu);
                    gleich++;
                }
            }
        }
        Assert.True(gleich > 100, "verglichen: " + gleich);
        Assert.True(korrigiert > 0, "Gegenprobe: kein alter Weg lief ueber das Gleis");
    }
}
