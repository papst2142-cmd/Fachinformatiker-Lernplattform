using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle Nachladen A 0.82.0 (Weisung P12-280, Nico 1c und 4a): jede Karte ist in Kacheln von 16 × 16 geteilt. Geprueft wird
/// Nicos Ziel „die Summe aller Kacheln ergibt die Netze des ganzen Baus“: Boden plus alle Kacheln haben genau die Flaeche
/// des ganzen Stadtnetzes, des Kleingruens und des Paket-Netzes, jedes Dreieck, das ganz in einer Kachel liegt, kommt
/// unveraendert in genau eine Kachel, jedes Haus in genau eine. Dazu Namen, Ladeplan und Formatnummer.
/// </summary>
public sealed class SpielKachelnTests
{
    private static StadtSpiel Spiel(int fassung)
    {
        var f = AktiveFassung.LadenNummer(p => File.Exists(Path.Combine(Pfade.Wurzel, p)) ? File.ReadAllBytes(Path.Combine(Pfade.Wurzel, p)) : null, fassung);
        var inhalt = f.MitKarte(SpielInhaltTests.Inhalt);
        return new StadtSpiel(f.StadtFuer(inhalt), SpielQuestTests.AllesErledigt(inhalt), SpielGrafikTests.Bibliothek.Value, SpielInnenraumTests.Innenraeume.Value);
    }

    private static string Text(Netz n, int i)
    {
        var e = n.Ecken;
        return string.Join(";", Enumerable.Range(0, 3).SelectMany(k => Enumerable.Range(0, Netz.Zahlen).Select(z => e[((int)n.Indizes[i + k] * Netz.Zahlen) + z].ToString("R", CultureInfo.InvariantCulture))));
    }

    private static bool KreuztRaster(Netz n, int i)
    {
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (var k = 0; k < 3; k++)
        {
            var p = (int)n.Indizes[i + k] * Netz.Zahlen;
            (minX, maxX, minZ, maxZ) = (MathF.Min(minX, n.Ecken[p]), MathF.Max(maxX, n.Ecken[p]), MathF.Min(minZ, n.Ecken[p + 2]), MathF.Max(maxZ, n.Ecken[p + 2]));
        }
        bool Kreuzt(float a, float b)
        {
            for (var l = (MathF.Floor(a / Stadtteile.Groesse) + 1) * Stadtteile.Groesse; l < b; l += Stadtteile.Groesse)
            {
                if (l > a)
                {
                    return true;
                }
            }
            return false;
        }
        return Kreuzt(minX, maxX) || Kreuzt(minZ, maxZ);
    }

    /// <summary>Jedes Dreieck des ganzen Netzes, das keine Rasterlinie kreuzt, steht unveraendert in den Teilen (gleiche Anzahl).</summary>
    private static void UnveraendertDrin(Netz ganz, IEnumerable<Netz> teile, string was)
    {
        var soll = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i + 2 < ganz.Indizes.Count; i += 3)
        {
            if (!KreuztRaster(ganz, i))
            {
                var t = Text(ganz, i);
                soll[t] = soll.GetValueOrDefault(t) + 1;
            }
        }
        var ist = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var n in teile)
        {
            for (var i = 0; i + 2 < n.Indizes.Count; i += 3)
            {
                var t = Text(n, i);
                if (soll.ContainsKey(t))
                {
                    ist[t] = ist.GetValueOrDefault(t) + 1;
                }
            }
        }
        var fehlt = soll.Where(p => ist.GetValueOrDefault(p.Key) != p.Value).Take(3).Select(p => $"{p.Value} gegen {ist.GetValueOrDefault(p.Key)}: {p.Key}").ToList();
        Assert.True(fehlt.Count == 0, was + ": " + string.Join(" | ", fehlt));
    }

    private static void FlaecheGleich(double ganz, double teile, string was) =>
        Assert.True(Math.Abs(ganz - teile) <= 1e-4 * Math.Max(1, ganz), $"{was}: ganz {ganz:F4}, Kacheln {teile:F4}");

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void AlleKachelnZusammen_GleichDemGanzenBau(int fassung, bool winter)
    {
        var spiel = Spiel(fassung);
        var szene = winter
            ? StadtSzene.Bauen(spiel.Stadt, spiel.Ablauf.Stand, SpielGrafikTests.Bibliothek.Value, Festzeit.Weihnachten, FestDekoTests.Daten.Value, Jahreszeit.Winter)
            : spiel.Szene;
        var ganz = szene.Vorbereiten();
        var grund = szene.GrundlageVorbereiten();
        var kacheln = Stadtteile.Alle(szene.Breite, szene.Tiefe).Select(szene.KachelVorbereiten).ToList();
        var (s, r) = Stadtteile.Raster(szene.Breite, szene.Tiefe);
        Assert.Equal(s * r, kacheln.Count);

        FlaecheGleich(ganz.Stadt.FlaecheGesamt(), grund.Boden.FlaecheGesamt() + kacheln.Sum(k => k.Stadt.FlaecheGesamt()), "Stadtnetz");
        FlaecheGleich(ganz.Gruen?.FlaecheGesamt() ?? 0, kacheln.Sum(k => k.Gruen?.FlaecheGesamt() ?? 0), "Kleingruen");
        FlaecheGleich(ganz.PaketNah?.FlaecheGesamt() ?? 0, kacheln.Sum(k => k.Paket?.FlaecheGesamt() ?? 0), "Paket-Netz");
        UnveraendertDrin(ganz.Stadt, kacheln.Select(k => k.Stadt).Append(grund.Boden), "Stadtnetz");
        if (ganz.Gruen is { } g)
        {
            UnveraendertDrin(g, kacheln.Select(k => k.Gruen).OfType<Netz>(), "Kleingruen");
        }
        if (ganz.PaketNah is { } p)
        {
            UnveraendertDrin(p, kacheln.Select(k => k.Paket).OfType<Netz>(), "Paket-Netz");
        }

        // jedes Haus in genau einer Kachel, mit demselben Netz wie im ganzen Bau
        var haeuser = kacheln.SelectMany(k => k.Haeuser.Select(h => (k.Kachel, h.Haus, h.Netz))).ToList();
        Assert.Equal(szene.Haeuser.Count, haeuser.Count);
        Assert.Equal(szene.Haeuser.Count, haeuser.Select(h => h.Haus).Distinct().Count());
        foreach (var (k, haus, netz) in haeuser)
        {
            Assert.Equal(k, szene.KachelVon(haus));
            Assert.True(ganz.Haeuser[haus].EckenFeld.SequenceEqual(netz.EckenFeld), "Haus " + haus);
        }
        // die Fernform hat je Haus einen Kasten (12 Dreiecke)
        Assert.Equal(szene.Haeuser.Count * 12, grund.FernKaesten.Indizes.Count / 3);
    }

    [Fact]
    public void Kachelnamen_UndRaster()
    {
        Assert.Equal("A1", new Stadtteile.Kachel(0, 0).Name);
        Assert.Equal("B3", new Stadtteile.Kachel(1, 2).Name);
        Assert.Equal("P16", new Stadtteile.Kachel(15, 15).Name);
        Assert.Equal("AA1", new Stadtteile.Kachel(26, 0).Name);
        Assert.Equal((16, 16), Stadtteile.Raster(256, 256));
        Assert.Equal((4, 4), Stadtteile.Raster(64, 64));
        Assert.Equal((3, 2), Stadtteile.Raster(40, 17));
        // links und oben gehoeren dazu, rechts und unten nicht; ausserhalb die naechste Kachel am Rand
        Assert.Equal(new Stadtteile.Kachel(1, 0), Stadtteile.Von(16f, 0f, 64, 64));
        Assert.Equal(new Stadtteile.Kachel(0, 0), Stadtteile.Von(15.999f, -3f, 64, 64));
        Assert.Equal(new Stadtteile.Kachel(3, 3), Stadtteile.Von(99f, 64f, 64, 64));
        Assert.True(new Stadtteile.Kachel(1, 1).Drin(16f, 16f));
        Assert.False(new Stadtteile.Kachel(1, 1).Drin(32f, 20f));
    }

    [Fact]
    public void Ladeplan_NahImVorrat_UndFreigabeErstWeitWeg()
    {
        var rnd = new Random(280);
        for (var n = 0; n < 200; n++)
        {
            float x = (float)(rnd.NextDouble() * 256), z = (float)(rnd.NextDouble() * 256);
            var nah = Ladeplan.Noetig(x, z, 256, 256);
            var vor = Ladeplan.Vorbereiten(x, z, 256, 256);
            Assert.Contains(Stadtteile.Von(x, z, 256, 256), nah);
            Assert.All(nah, k => Assert.Contains(k, vor));
            // nah zuerst: die Liste ist nach Abstand geordnet
            Assert.Equal(vor.Take(nah.Count), nah);
            // eine Kachel, die nah oder vorbereitet ist, wird nie freigegeben (Grafikkarte bzw. Vorrat)
            Assert.All(nah, k => Assert.False(Ladeplan.FreigebenNah(k, x, z)));
            Assert.All(vor, k => Assert.False(Ladeplan.FreigebenVor(k, x, z)));
        }
        // die feste Kamera sieht den Boden bis rund 19 vor der Figur: jede Kachel in diesem Kreis ist nah
        var alle = Ladeplan.ImKreis(128, 128, 19f, 256, 256);
        Assert.All(alle, k => Assert.Contains(k, Ladeplan.Noetig(128, 128, 256, 256)));
        Assert.True(Ladeplan.VorWeg > Ladeplan.Vor && Ladeplan.Vor > Ladeplan.NahWeg && Ladeplan.NahWeg > Ladeplan.Nah);
    }

    /// <summary>
    /// Die schnelle Laternen-Pruefung (<see cref="Nachtlicht.Regeln.Frei"/>, Raster nach Wirkkasten) ergibt an jedem Punkt
    /// genau dasselbe wie die volle <see cref="Nachtlicht.Regeln.Pruefen"/>: Zufallspunkte ueber die ganze Karte und dichte
    /// Punkte im Laternenabstand neben jeder Strasse (dort liegen die Grenzfaelle), Fassung 0, 1, 2 und die Testkarte.
    /// </summary>
    [Fact]
    public void Laternenregeln_SchnellGleichVoll()
    {
        foreach (var szene in new[] { Spiel(0).Szene, Spiel(1).Szene, Spiel(2).Szene, SpielTestkarteTests.Testkarte.Value.Szene })
        {
            var regeln = new Nachtlicht.Regeln(szene);
            var rnd = new Random(281);
            var punkte = Enumerable.Range(0, 20000).Select(_ => new Vector2((float)rnd.NextDouble() * szene.Breite, (float)rnd.NextDouble() * szene.Tiefe)).ToList();
            foreach (var strasse in szene.Strassen.Take(40))
            {
                for (var i = 0; i + 1 < strasse.Count; i++)
                {
                    var d = strasse[i + 1] - strasse[i];
                    var laenge = d.Length();
                    if (laenge < 1e-4f)
                    {
                        continue;
                    }
                    var quer = new Vector2(-d.Y, d.X) / laenge;
                    for (var t = 0f; t <= laenge; t += 0.05f)
                    {
                        var p = strasse[i] + (d * (t / laenge));
                        punkte.Add(p + (quer * Nachtlicht.Rand));
                        punkte.Add(p - (quer * Nachtlicht.Rand));
                    }
                }
            }
            // Sammelliste 457 (Weisung P12-380, W7): dieselben Punkte, jeder genau einmal, nebeneinander gerechnet (Regeln lesen
            // nur; das schnelle Raster wird vorher einmal gebaut); Pruefung danach in derselben Reihenfolge wie vorher.
            _ = regeln.Frei(punkte[0]);
            var vollJe = new bool[punkte.Count];
            var schnellJe = new bool[punkte.Count];
            Nebeneinander.Fuer(punkte.Count, i =>
            {
                vollJe[i] = regeln.Pruefen(punkte[i]).Frei;
                schnellJe[i] = regeln.Frei(punkte[i]);
            });
            var frei = 0;
            for (var i = 0; i < punkte.Count; i++)
            {
                var q = punkte[i];
                var voll = vollJe[i];
                Assert.True(voll == schnellJe[i], $"{szene.Breite}: Punkt {q.X:R} {q.Y:R}, voll {voll}");
                frei += voll ? 1 : 0;
            }
            // beide Antworten kommen vor
            Assert.InRange(frei, 1, punkte.Count - 1);
        }
    }

    [Fact]
    public void Formatnummer_Drei_NurFuerGrosseKarten()
    {
        foreach (var n in new[] { 0, 1, 2 })
        {
            var f = AktiveFassung.LadenNummer(p => File.Exists(Path.Combine(Pfade.Wurzel, p)) ? File.ReadAllBytes(Path.Combine(Pfade.Wurzel, p)) : null, n);
            Assert.True(StadtFassung.FormatFuer(f.Stadtdaten, f.Karte) < 3, "Fassung " + n);
        }
        var gross = Py.Lesen("{\"welt\":{\"breite\":65,\"hoehe\":64}}") as PyObjekt;
        Assert.Equal(3, StadtFassung.FormatFuer(null, gross));
        var genau = Py.Lesen("{\"welt\":{\"breite\":64,\"hoehe\":64}}") as PyObjekt;
        Assert.Equal(1, StadtFassung.FormatFuer(null, genau));
        var namen = Py.Lesen("{\"stadtteile\":[]}") as PyObjekt;
        Assert.Equal(3, StadtFassung.FormatFuer(namen, genau));
    }
}

/// <summary>
/// Welle Nachladen A 0.82.0 (Weisung P12-280, Nico 2b und 4a): die Testkarte 256 × 256 (16 Kopien von Stadt 1, erzeugt mit
/// <c>werkzeuge/stadtbau/testkarte.py</c>). Ein Gang ueber die ganze Karte (Reihe fuer Reihe, hin und zurueck) mit dem
/// Nachlader wie im Spiel, nur ohne Hintergrund: an jeder Stelle sind alle noetigen Kacheln geladen (nach dem Anfang muss nie
/// eine sofort gebaut werden), und das gleichzeitig Geladene bleibt zu jedem Zeitpunkt unter 48 MB Netzdaten.
/// </summary>
[Collection("Haenger")]
public sealed class SpielTestkarteTests
{
    internal static readonly Lazy<StadtSpiel> Testkarte = new(() =>
    {
        var f = StadtFassung.Lesen(File.ReadAllBytes(Path.Combine(Pfade.Wurzel, "tests/Lernplattform.Tests/Testdaten/testkarte_256.fistadt")));
        var inhalt = f.MitKarte(SpielInhaltTests.Inhalt);
        return new StadtSpiel(f.StadtFuer(inhalt), SpielQuestTests.AllesErledigt(inhalt), SpielGrafikTests.Bibliothek.Value, SpielInnenraumTests.Innenraeume.Value);
    });

    [Fact]
    public void GangUeberDieGanzeKarte_ImmerAllesNoetigeGeladen_UnterGrenzeNetzdaten()
    {
        var szene = Testkarte.Value.Szene;
        Assert.Equal((256f, 256f), (szene.Breite, szene.Tiefe));
        var lader = new Nachlader(szene, mitGruen: true, hintergrund: false);
        var schritte = 0;
        void An(float x, float z)
        {
            lader.Takt(x, z);
            while (lader.Naechste() is { } p)
            {
                lader.Hochgeladen(p);
            }
            foreach (var k in lader.Fehlend().ToList())
            {
                lader.Hochgeladen(lader.JetztBauen(k) ?? throw new InvalidOperationException(Nachlader.FehlerText(k, null)));
            }
            Assert.All(lader.Noetig, k => Assert.True(lader.Geladen.ContainsKey(k), k.Name));
            schritte++;
        }
        An(8, 8);
        var sofortAmAnfang = lader.Sofort;
        for (var reihe = 0; reihe < 8; reihe++)
        {
            var z = 8f + (reihe * 32f);
            for (var i = 0; i <= 480; i++)
            {
                var x = reihe % 2 == 0 ? 8f + (i * 0.5f) : 248f - (i * 0.5f);
                An(x, z);
            }
            if (reihe < 7)
            {
                for (var j = 1; j <= 64; j++)
                {
                    An(reihe % 2 == 0 ? 248f : 8f, z + (j * 0.5f));
                }
            }
        }
        // im Gang selbst ist jede noetige Kachel rechtzeitig vorbereitet (zu Fuss 0,5 je Takt)
        Assert.Equal(sofortAmAnfang, lader.Sofort);
        Assert.True(lader.HoechstBytes <= StadtLast.NetzdatenGrenzeMb * 1024 * 1024, $"hoechstens {lader.HoechstBytes / 1048576.0:F1} MB");
        Assert.True(schritte > 4000);
    }
}
