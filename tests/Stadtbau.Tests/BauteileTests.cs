using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.9.0 (Weisung P12-48): die Teile des Gebaeude-Editors (Kenney Modular Buildings). Je Teil ein Test der Ausrichtung
/// (die Tabelle sagt, wo die Fassade bei Drehung 0 liegt; das Modell muss das zeigen, auch nach jeder Drehung).
/// </summary>
public class BauteileTests
{
    private static GrafikModell M(string name) => Katalog.Bauteil(Bauteile.Praefix + name) ?? throw new InvalidOperationException("Modell fehlt: " + name);

    public static TheoryData<string> AlleTeile() => [.. Bauteile.Tabelle.Keys.Order(StringComparer.Ordinal)];

    [Fact]
    public void JedesModellDesPakets_InDerTabelleOderBegruendetAusgenommen()
    {
        var modelle = Katalog.Aktiv!.Modelle(Bauteile.Paket).Keys.ToHashSet(StringComparer.Ordinal);
        Assert.Equal(modelle.Order(StringComparer.Ordinal), Bauteile.Tabelle.Keys.Concat(Bauteile.NichtImEditor.Keys).Order(StringComparer.Ordinal));
        Assert.Empty(Bauteile.Tabelle.Keys.Intersect(Bauteile.NichtImEditor.Keys));
        Assert.All(Bauteile.NichtImEditor.Values, grund => Assert.False(string.IsNullOrWhiteSpace(grund)));
        Assert.All(Bauteile.Tabelle.Values, a => Assert.False(string.IsNullOrWhiteSpace(a.Anzeige)));
        // deutsche Namen sind eindeutig (Liste links)
        Assert.Equal(Bauteile.Tabelle.Count, Bauteile.Tabelle.Values.Select(a => a.Anzeige).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Je Seite: Zahl der Dreiecke nahe der Feldkante (bis 0,15 innen oder davor), die NICHT glatt auf der Kante liegen:
    /// Fensterrahmen, Glas in der Vertiefung, Tuer, Sims, Balkon. Eine glatte Rueckwand hat dort keine.
    /// </summary>
    private static Dictionary<Bauteile.Seiten, int> Feinheiten(GrafikModell m, Matrix4x4 lage, int x, int y)
    {
        var f = Bauteile.Alle.ToDictionary(s => s, _ => 0);
        for (var i = 0; i + 8 < m.Ecken.Length; i += 9)
        {
            var a = Vector3.Transform(new Vector3(m.Ecken[i], m.Ecken[i + 1], m.Ecken[i + 2]), lage);
            var b = Vector3.Transform(new Vector3(m.Ecken[i + 3], m.Ecken[i + 4], m.Ecken[i + 5]), lage);
            var c = Vector3.Transform(new Vector3(m.Ecken[i + 6], m.Ecken[i + 7], m.Ecken[i + 8]), lage);
            var mitte = (a + b + c) / 3;
            foreach (var s in Bauteile.Alle)
            {
                // Abstand nach innen von der Kante (negativ = davor)
                float Innen(Vector3 p) => s switch
                {
                    Bauteile.Seiten.Ost => (x + 1) - p.X,
                    Bauteile.Seiten.West => p.X - x,
                    Bauteile.Seiten.Sued => (y + 1) - p.Z,
                    _ => p.Z - y,
                };
                var glatt = MathF.Abs(Innen(a)) < 0.003f && MathF.Abs(Innen(b)) < 0.003f && MathF.Abs(Innen(c)) < 0.003f;
                if (!glatt && Innen(mitte) < 0.15f)
                {
                    f[s]++;
                }
            }
        }
        return f;
    }

    /// <summary>Flaeche der Dreiecke, die glatt auf der Feldkante liegen und nach aussen zeigen, je Seite.</summary>
    private static Dictionary<Bauteile.Seiten, float> Aussenflaeche(GrafikModell m, Matrix4x4 lage, int x, int y)
    {
        var f = Bauteile.Alle.ToDictionary(s => s, _ => 0f);
        for (var i = 0; i + 8 < m.Ecken.Length; i += 9)
        {
            var a = Vector3.Transform(new Vector3(m.Ecken[i], m.Ecken[i + 1], m.Ecken[i + 2]), lage);
            var b = Vector3.Transform(new Vector3(m.Ecken[i + 3], m.Ecken[i + 4], m.Ecken[i + 5]), lage);
            var c = Vector3.Transform(new Vector3(m.Ecken[i + 6], m.Ecken[i + 7], m.Ecken[i + 8]), lage);
            var flaeche = Vector3.Cross(b - a, c - a).Length() / 2;
            foreach (var s in Bauteile.Alle)
            {
                float Innen(Vector3 p) => s switch
                {
                    Bauteile.Seiten.Ost => (x + 1) - p.X,
                    Bauteile.Seiten.West => p.X - x,
                    Bauteile.Seiten.Sued => (y + 1) - p.Z,
                    _ => p.Z - y,
                };
                if (MathF.Abs(Innen(a)) < 0.003f && MathF.Abs(Innen(b)) < 0.003f && MathF.Abs(Innen(c)) < 0.003f)
                {
                    f[s] += flaeche;
                }
            }
        }
        return f;
    }

    /// <summary>
    /// Wert je Seite: Feinheiten plus fehlende glatte Flaeche (in Hundertsteln; eine abgeschraegte Ecke ohne Fenster hat
    /// auf ihren zwei Seiten nur die halbe Wand).
    /// </summary>
    private static Dictionary<Bauteile.Seiten, int> Wert(GrafikModell m, Matrix4x4 lage, int x, int y)
    {
        var fein = Feinheiten(m, lage, x, y);
        var flaeche = Aussenflaeche(m, lage, x, y);
        var voll = flaeche.Values.Max();
        return Bauteile.Alle.ToDictionary(s => s, s => fein[s] + (int)MathF.Round(MathF.Max(0, voll - flaeche[s]) * 100));
    }

    /// <summary>
    /// Die Fassade(n): die <paramref name="anzahl"/> Seiten mit den meisten Feinheiten, jede mit sichtbar mehr als jede
    /// andere Seite (sonst Keine).
    /// </summary>
    private static Bauteile.Seiten Fassaden(Dictionary<Bauteile.Seiten, int> f, int anzahl)
    {
        var sortiert = f.OrderByDescending(p => p.Value).ThenBy(p => p.Key).ToList();
        var rest = sortiert.Skip(anzahl).Select(p => p.Value).DefaultIfEmpty(0).Max();
        var s = Bauteile.Seiten.Keine;
        foreach (var (seite, wert) in sortiert.Take(anzahl))
        {
            if (wert > rest)
            {
                s |= seite;
            }
        }
        return s;
    }

    private static int Anzahl(Bauteile.Seiten s) => System.Numerics.BitOperations.PopCount((uint)s);

    [Theory]
    [MemberData(nameof(AlleTeile))]
    public void Ausrichtung_JeTeil_WieInDerTabelle_AuchNachJederDrehung(string name)
    {
        var a = Bauteile.Tabelle[name];
        var m = M(name);
        const int x = 3, y = 5, stock = 1;
        for (var dreh = 0; dreh < 4; dreh++)
        {
            var t = new Bauteile.Teil(Bauteile.Praefix + name, x, y, stock, dreh);
            Assert.Equal(Bauteile.Drehen(a.Fassade, dreh), t.Fassade);
            var lage = Bauteile.Lage(t, m);
            var (min, max) = Kasten(m, lage);
            var boden = stock * Bauteile.Stockwerk;
            switch (a.Art)
            {
                case Bauteile.Art.Wand or Bauteile.Art.Ecke or Bauteile.Art.Tuer or Bauteile.Art.Block:
                    // steht genau im Feld und auf der Ebene, Fassade(n) dort, wo die Tabelle sagt
                    // (Simse, Markisen und Balkone duerfen bis 0,16 vor die Fassade ragen, nie nach hinten; Weisung P12-52:
                    // vorher durften sie auf allen Seiten 0,1 ueberstehen, darum fiel der Versatz um den halben Ueberstand
                    // nicht auf; streng prueft BauteileBuendigTests)
                    float Vor(Bauteile.Seiten s) => t.Fassade.HasFlag(s) ? 0.16f : 0.01f;
                    Assert.InRange(min.X, x - Vor(Bauteile.Seiten.West), x + 0.01f);
                    Assert.InRange(max.X, x + 0.99f, x + 1 + Vor(Bauteile.Seiten.Ost));
                    Assert.InRange(min.Z, y - Vor(Bauteile.Seiten.Nord), y + 0.01f);
                    Assert.InRange(max.Z, y + 0.99f, y + 1 + Vor(Bauteile.Seiten.Sued));
                    Assert.InRange(min.Y, boden - 0.01f, boden + 0.01f);
                    var flaechen = Wert(m, lage, x, y);
                    if (t.Fassade == Bauteile.Seiten.Keine)
                    {
                        Assert.True(flaechen.Values.Max() - flaechen.Values.Min() <= 2, name + ": alle Seiten gleich erwartet");
                    }
                    else
                    {
                        Assert.Equal(t.Fassade, Fassaden(flaechen, Anzahl(t.Fassade)));
                    }
                    break;
                case Bauteile.Art.Detail:
                {
                    // liegt vor der Fassade (aussen an der Feldkante), quer zur Fassade in der Mitte des Felds
                    var mitte = (min + max) / 2;
                    switch (t.Fassade)
                    {
                        case Bauteile.Seiten.Sued:
                            Assert.InRange(min.Z, y + 0.99f, y + 1.01f);
                            Assert.InRange(mitte.X, x + 0.49f, x + 0.51f);
                            break;
                        case Bauteile.Seiten.Nord:
                            Assert.InRange(max.Z, y - 0.01f, y + 0.01f);
                            Assert.InRange(mitte.X, x + 0.49f, x + 0.51f);
                            break;
                        case Bauteile.Seiten.Ost:
                            Assert.InRange(min.X, x + 0.99f, x + 1.01f);
                            Assert.InRange(mitte.Z, y + 0.49f, y + 0.51f);
                            break;
                        default:
                            Assert.InRange(max.X, x - 0.01f, x + 0.01f);
                            Assert.InRange(mitte.Z, y + 0.49f, y + 0.51f);
                            break;
                    }
                    Assert.InRange(min.Y, boden - 0.01f, boden + Bauteile.Stockwerk);
                    Assert.True(max.Y <= boden + Bauteile.Stockwerk + 0.01f, name + " ragt ueber die Ebene");
                    break;
                }
                case Bauteile.Art.DachDetail:
                {
                    var mitte = (min + max) / 2;
                    Assert.InRange(mitte.X, x + 0.49f, x + 0.51f);
                    Assert.InRange(mitte.Z, y + 0.49f, y + 0.51f);
                    Assert.InRange(min.Y, boden + Bauteile.DachDetailHoehe - 0.01f, boden + Bauteile.DachDetailHoehe + 0.01f);
                    break;
                }
                default:
                {
                    // Dach: auf der Ebene (die Traufe darf bis 0,1 darunter reichen), Mitte im Feld (Ueberstand ueber die Kante)
                    var mitte = (min + max) / 2;
                    Assert.InRange(min.Y, boden - 0.1f, boden + 0.01f);
                    Assert.InRange(mitte.X, x + 0.2f, x + 0.8f);
                    Assert.InRange(mitte.Z, y + 0.2f, y + 0.8f);
                    Assert.True(max.X - min.X < 1.6f && max.Z - min.Z < 1.6f, name + " zu breit");
                    break;
                }
            }
        }
    }

    [Fact]
    public void Ausrichtung_Gegenprobe_FalscheFassadeFaellt()
    {
        // Wand mit Fenster: die Fassade liegt im Sueden; als Nord-Fassade gelesen waere sie falsch
        var m = M("building-window");
        var t = new Bauteile.Teil(Bauteile.Praefix + "building-window", 0, 0, 0, 0);
        Assert.Equal(Bauteile.Seiten.Sued, Fassaden(Wert(m, Bauteile.Lage(t, m), 0, 0), 1));
        // eine Drehung, die nicht zur Tabelle passt (gegen den Uhrzeigersinn), zeigt eine andere Seite
        var falsch = Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation(0.5f, 0, 0.5f);
        Assert.NotEqual(Bauteile.Drehen(Bauteile.Seiten.Sued, 1), Fassaden(Wert(m, falsch, 0, 0), 1));
    }

    private static (Vector3 Min, Vector3 Max) Kasten(GrafikModell m, Matrix4x4 lage)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var i = 0; i + 2 < m.Ecken.Length; i += 3)
        {
            var p = Vector3.Transform(new Vector3(m.Ecken[i], m.Ecken[i + 1], m.Ecken[i + 2]), lage);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return (min, max);
    }

    [Fact]
    public void Drehen_ImUhrzeigersinn_VonObenGesehen()
    {
        Assert.Equal(Bauteile.Seiten.West, Bauteile.Drehen(Bauteile.Seiten.Sued, 1));
        Assert.Equal(Bauteile.Seiten.Nord, Bauteile.Drehen(Bauteile.Seiten.Sued, 2));
        Assert.Equal(Bauteile.Seiten.Ost, Bauteile.Drehen(Bauteile.Seiten.Sued, 3));
        Assert.Equal(Bauteile.Seiten.West | Bauteile.Seiten.Sued, Bauteile.Drehen(Bauteile.Seiten.Sued | Bauteile.Seiten.Ost, 1));
        Assert.Equal(Bauteile.Seiten.Keine, Bauteile.Drehen(Bauteile.Seiten.Keine, 3));
    }

    [Fact]
    public void Zusammensetzen_DreieckeUndFarbenAllerTeile_BlockMitMassstabEins()
    {
        List<Bauteile.Teil> teile =
        [
            new(Bauteile.Praefix + "building-door", 0, 0, 0, 0),
            new(Bauteile.Praefix + "building-window", 1, 0, 0, 0),
            new(Bauteile.Praefix + "roof-flat-center", 0, 0, 1, 0),
        ];
        var gm = Bauteile.Zusammensetzen("bau:test", teile, Katalog.Bauteil);
        Assert.Equal(teile.Sum(t => M(t.Name).Dreiecke), gm.Dreiecke);
        Assert.Equal(Bauteile.Dreiecke(teile, Katalog.Bauteil), gm.Dreiecke);
        // S 0.9.1 (Weisung P12-50): Farbe je Ecke wie das Teil, in der Reihenfolge der Teile (der Farbverlauf bleibt)
        Assert.Equal(teile.SelectMany(t => M(t.Name).EckFarben!).ToArray(), gm.EckFarben);
        Assert.Contains(Enumerable.Range(0, gm.Dreiecke), d => gm.EckFarbe(d, 0) != gm.Farbe(d));
        var block = Assert.IsType<PyObjekt>(Assert.Single(Bauteile.Block(gm)));
        // der Block umschliesst das Modell genau: das Spiel zeichnet es dann ohne Streckung (Massstab 1)
        Assert.Equal((double)gm.Min.X, Lernplattform.Core.Spiel.Py.F(block["x"]), 4);
        Assert.Equal((double)(gm.Max.X - gm.Min.X), Lernplattform.Core.Spiel.Py.F(block["w"]), 4);
        Assert.Equal((double)(gm.Max.Z - gm.Min.Z), Lernplattform.Core.Spiel.Py.F(block["t"]), 4);
        Assert.Equal((double)(gm.Max.Y - gm.Min.Y), Lernplattform.Core.Spiel.Py.F(block["h"]), 4);
        // Gegenprobe: ein unbekanntes Teil faellt nie still weg
        Assert.Throws<InvalidDataException>(() => Bauteile.Zusammensetzen("bau:x", [new("cc0:modular/gibt-es-nicht", 0, 0, 0, 0)], Katalog.Bauteil));
    }

    [Fact]
    public void Daten_HinUndZurueck_GleicheTeileUndEingaenge()
    {
        List<Bauteile.Teil> teile = [new(Bauteile.Praefix + "building-door", 2, 3, 0, 1), new(Bauteile.Praefix + "roof-flat-center", 2, 3, 1, 2)];
        List<Bauteile.Eingang> ein = [new(2, 3, Bauteile.Seiten.West)];
        Assert.Equal(teile, Bauteile.TeileLesen(Bauteile.TeileDaten(teile)));
        Assert.Equal(ein, Bauteile.EingaengeLesen(Bauteile.EingaengeDaten(ein)));
        // Gegenprobe: falsche Daten werfen (nie still weg)
        Assert.Throws<InvalidDataException>(() => Bauteile.TeileLesen(new List<object?> { new PyObjekt { { "teil", "x" } } }));
        Assert.Throws<InvalidDataException>(() => Bauteile.EingaengeLesen(new List<object?> { new PyObjekt { { "x", 1L }, { "y", 1L }, { "seite", "oben" } } }));
    }

    [Fact]
    public void MitGebaeuden_SpielGrafikKenntDasEigeneGebaeude()
    {
        var d = Daten.Spiel;
        var stadtdaten = new PyObjekt
        {
            {
                "bauteile", new PyObjekt
                {
                    { "bau:probe", new PyObjekt { { "teile", Bauteile.TeileDaten([new(Bauteile.Praefix + "building-door", 0, 0, 0, 0)]) }, { "eingang", Bauteile.EingaengeDaten([new(0, 0, Bauteile.Seiten.Sued)]) } } },
                }
            },
        };
        var g = Bauteile.MitGebaeuden(d.Grafik, stadtdaten, Katalog.Bauteil)!;
        Assert.NotNull(g.Modell("bau:probe"));
        Assert.Equal("bau:probe", g.Bloecke["bau:probe:0"]);
        // ohne eigene Gebaeude bleibt die Spielgrafik dieselbe
        Assert.Same(d.Grafik, Bauteile.MitGebaeuden(d.Grafik, new PyObjekt(), Katalog.Bauteil));
        Assert.Empty(Bauteile.Gebaeude(new PyObjekt()));
        _ = Get(stadtdaten, "bauteile");
    }
}
