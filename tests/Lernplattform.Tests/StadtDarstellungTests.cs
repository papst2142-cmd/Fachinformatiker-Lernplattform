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
        // die Skizze ist uebertragen: hoechstens so weit verschoben wie noetig (groesste Verschiebung 2,58 bei Nr. 18)
        Assert.All(szene.Laternen, l => Assert.True(l.Verschoben < 2.6f, $"Laterne {l.Nr} um {l.Verschoben:F2} verschoben"));
        Assert.True(szene.Laternen.Count(l => l.Verschoben < 0.3f) >= 15, "zu viele Laternen weit von der Skizze");
    }

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
