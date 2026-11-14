using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle 327 (Weisung P12-244, Kurzplan P12-142 Weg a): das nahe Paket-Netz (Laternen, Bahnuebergaenge, Masten von Ampeln und
/// Haltestellen, Tannen, Kuerbisse, Questgeber) ist in Zellen geteilt, gezeichnet und gezaehlt wird nur, was im Bild ist.
/// Geprueft wird Nicos Ziel: Stadt 1 bleibt in „Niedrig“ unter 125 000 Dreiecken und 32 Aufrufen (ohne Ausnahme), es fehlt
/// nichts, was im Bild ist, und das Netz enthaelt genau die Dreiecke wie vorher. Gegenprobe: ohne Zellen (Stand A 0.73.0)
/// liegt Stadt 1 in Niedrig ueber der Grenze.
/// </summary>
public sealed class SpielPaketZellenTests
{
    private static readonly Lazy<StadtSpiel> Stadt1 = new(() =>
    {
        var f = AktiveFassung.LadenNummer(p => File.Exists(Path.Combine(Pfade.Wurzel, p)) ? File.ReadAllBytes(Path.Combine(Pfade.Wurzel, p)) : null, 1);
        var inhalt = f.MitKarte(SpielInhaltTests.Inhalt);
        return new StadtSpiel(f.StadtFuer(inhalt), SpielQuestTests.AllesErledigt(inhalt), SpielGrafikTests.Bibliothek.Value, SpielInnenraumTests.Innenraeume.Value);
    });

    private static readonly float[] Abstaende = [4f, 6f, 10f, 16f];

    private static Matrix4x4 SichtProj(float x, float z, float abstand, float drehung)
    {
        var k = new Kamera(x, z, abstand) { Drehung = drehung };
        return k.Sicht * k.Projektion(1600, 900);
    }

    /// <summary>Die Dreiecke als Texte (je Dreieck die neun Zahlen seiner drei Ecken), sortiert: gleiche Menge = gleiches Netz.</summary>
    private static List<string> Dreiecke(Netz n)
    {
        var e = n.Ecken;
        var idx = n.Indizes;
        var liste = new List<string>();
        for (var i = 0; i + 2 < idx.Count; i += 3)
        {
            liste.Add(string.Join(";", Enumerable.Range(0, 3).SelectMany(k => Enumerable.Range(0, Netz.Zahlen).Select(z => e[((int)idx[i + k] * Netz.Zahlen) + z].ToString("R", CultureInfo.InvariantCulture)))));
        }
        liste.Sort(StringComparer.Ordinal);
        return liste;
    }

    [Fact]
    public void Geordnet_GleicheDreieckeUndEcken_ZellenDeckenAlles()
    {
        var szene = Stadt1.Value.Szene;
        var nah = szene.PaketNetzBauen(false);
        var fern = szene.PaketNetzBauen(true);
        Assert.NotNull(nah.Zellen);
        // die Fernform bleibt ungeteilt (klein, meist ganz im Bild)
        Assert.Null(fern.Zellen);
        // dieselben Ecken in derselben Reihenfolge wie das ungeteilte Netz (Stand A 0.73.0), nur die Indizes sind umgestellt
        var roh = szene.PaketNetzBauen(false, zellen: false);
        Assert.Null(roh.Zellen);
        Assert.True(nah.EckenFeld.SequenceEqual(roh.EckenFeld));
        Assert.Equal(roh.Indizes.Count, nah.Indizes.Count);
        Assert.False(nah.IndizesFeld.SequenceEqual(roh.IndizesFeld));
        Assert.True(fern.EckenFeld.SequenceEqual(szene.PaketNetzBauen(true, zellen: false).EckenFeld));
        var zellen = nah.Zellen!;
        Assert.True(zellen.Count > 1, zellen.Count + " Zellen");
        Assert.Equal(0, zellen[0].Start);
        for (var i = 1; i < zellen.Count; i++)
        {
            Assert.Equal(zellen[i - 1].Start + zellen[i - 1].Anzahl, zellen[i].Start);
        }
        Assert.Equal(nah.Indizes.Count, zellen[^1].Start + zellen[^1].Anzahl);
        // ohne Sichtkegel (alle) ein einziger Lauf mit allem: in der Gesamtansicht von „Hoch“ ein Aufruf wie vorher
        var laeufe = new List<(int, int)>();
        nah.Laeufe(Matrix4x4.Identity, true, laeufe);
        Assert.Equal([(0, nah.Indizes.Count)], laeufe);
        // Gegenprobe: ein ungeteiltes Netz hat dieselben Dreiecke
        Netz.OhneZellenFuerTests = true;
        try
        {
            nah.Laeufe(SichtProj(10, 10, 4, 0), false, laeufe);
            Assert.Equal([(0, nah.Indizes.Count)], laeufe);
        }
        finally
        {
            Netz.OhneZellenFuerTests = false;
        }
    }

    [Fact]
    public void Ordnen_BehaeltDieDreiecke()
    {
        var szene = Stadt1.Value.Szene;
        var roh = szene.PaketNetzBauen(false, zellen: false);
        var nah = szene.PaketNetzBauen(false);
        Assert.Equal(Dreiecke(roh), Dreiecke(nah));
        // jedes Dreieck mit seinen eigenen drei Ecken (gleiche Indizes-Tripel, nur in anderer Reihenfolge)
        var tripel = (Netz n) => Enumerable.Range(0, n.Indizes.Count / 3).Select(d => (n.Indizes[3 * d], n.Indizes[(3 * d) + 1], n.Indizes[(3 * d) + 2])).Order().ToList();
        Assert.Equal(tripel(roh), tripel(nah));
        // innerhalb einer Zelle bleibt die Reihenfolge wie vorher (stabil)
        foreach (var z in nah.Zellen!)
        {
            var erste = Enumerable.Range(z.Start / 3, z.Anzahl / 3).Select(d => nah.Indizes[3 * d]).ToList();
            Assert.Equal(erste.Order().ToList(), erste);
        }
    }

    /// <summary>Nichts fehlt im Bild: jede Ecke im Sichtkegel liegt in einer gezeichneten Zelle (an vielen Kamerastellen).</summary>
    [Fact]
    public void NichtsImBildFehlt()
    {
        var szene = Stadt1.Value.Szene;
        var nah = szene.PaketNetzBauen(false);
        var laeufe = new List<(int Start, int Anzahl)>();
        var weg = 0;
        for (var x = 2f; x < szene.Breite; x += 6)
        {
            for (var z = 2f; z < szene.Tiefe; z += 6)
            {
                foreach (var ab in Abstaende)
                {
                    foreach (var dreh in new[] { 0f, 90f, 180f, 270f })
                    {
                        var sp = SichtProj(x, z, ab, dreh);
                        nah.Laeufe(sp, false, laeufe);
                        var gezeichnet = new bool[nah.Indizes.Count];
                        foreach (var (s, a) in laeufe)
                        {
                            Array.Fill(gezeichnet, true, s, a);
                        }
                        for (var i = 0; i < gezeichnet.Length; i++)
                        {
                            if (gezeichnet[i])
                            {
                                continue;
                            }
                            weg++;
                            var c = Vector4.Transform(new Vector4(nah.Ort((int)nah.Indizes[i]), 1f), sp);
                            var imBild = c.W > 0 && MathF.Abs(c.X) <= c.W && MathF.Abs(c.Y) <= c.W && c.Z >= -c.W && c.Z <= c.W;
                            Assert.False(imBild, string.Create(CultureInfo.InvariantCulture, $"Ecke {i} fehlt im Bild bei ({x};{z}) Abstand {ab} Drehung {dreh}"));
                        }
                    }
                }
            }
        }
        // Gegenprobe: es wurde wirklich etwas weggelassen
        Assert.True(weg > 0);
    }

    /// <summary>Stadt 1 in „Niedrig“: teuerstes Nahbild (fest und bewegt) unter 125 000 / 32; Gegenprobe ohne Zellen darueber.</summary>
    [Fact]
    public void Stadt1_Niedrig_UnterDerGrenze_OhneZellenDarueber()
    {
        var spiel = Stadt1.Value;
        var szene = spiel.Szene;
        var zaehlung = new StadtbauGrenzen.Draussen(szene, szene.Vorbereiten());
        var werte = Grafikstufen.Niedrig;
        var (grenzeDreiecke, grenzeAufrufe) = Grafikstufen.Grenze(Grafikstufe.Niedrig);
        (long Dreiecke, int Aufrufe, string Wo) Teuerstes()
        {
            (long, int, string) t = (0, 0, "");
            var aufrufe = 0;
            foreach (var ab in Abstaende)
            {
                var b = StadtbauSpiel.Bewegt(spiel, werte.Fern(ab), halb: werte.AutosHalb);
                foreach (var dreh in new[] { 0f, 180f })
                {
                    for (var x = 1f; x < szene.Breite; x += 4)
                    {
                        for (var z = 1f; z < szene.Tiefe; z += 4)
                        {
                            var f = zaehlung.Bild(SichtProj(x, z, ab, dreh), ab, werte: werte);
                            aufrufe = Math.Max(aufrufe, f.Aufrufe + b.Aufrufe);
                            if (f.Dreiecke + b.Dreiecke > t.Item1)
                            {
                                t = (f.Dreiecke + b.Dreiecke, 0, string.Create(CultureInfo.InvariantCulture, $"({x};{z}) Abstand {ab} Drehung {dreh}"));
                            }
                        }
                    }
                }
            }
            return (t.Item1, aufrufe, t.Item3);
        }
        var mit = Teuerstes();
        Assert.True(mit.Dreiecke <= grenzeDreiecke && mit.Aufrufe <= grenzeAufrufe, $"Niedrig {mit.Dreiecke}/{mit.Aufrufe} {mit.Wo}");
        Netz.OhneZellenFuerTests = true;
        try
        {
            var ohne = Teuerstes();
            Assert.True(ohne.Dreiecke > grenzeDreiecke, $"Gegenprobe ohne Zellen {ohne.Dreiecke} {ohne.Wo}");
        }
        finally
        {
            Netz.OhneZellenFuerTests = false;
        }
    }
}
