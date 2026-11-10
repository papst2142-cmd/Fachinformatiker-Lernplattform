using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.31.0 (Weisung P12-130 Frage 1 = a, P12-140 Punkt 1): Industrie im Gebaeude-Editor. Factory-Teile im Massstab 1/3 auf
/// dem feineren Raster (1/3 Feld, Ebene 1,0), ein Gebaeude ist Wohnbau oder Halle, Schornstein/Tank/Solar als Dachaufsaetze
/// fuer beide, Grenze 8 000 Dreiecke bleibt. Die Tuer der Halle sitzt buendig im Tuerteil und ist die eigene Tuer des
/// Gebaeudes (Tuerblatt mit Klinke, keine Kastentuer davor).
/// </summary>
public partial class TuerBuendigTests
{
    [Fact]
    public void Halle_JedesFactoryTeil_HatEinenPlatzOderGrund()
    {
        var k = Katalog.Aktiv!;
        var alle = k.Modelle(Bauteile.PaketHalle);
        Assert.Equal(143, alle.Count);
        foreach (var name in alle.Keys)
        {
            var drin = Bauteile.TabelleHalle.ContainsKey(name);
            var grund = Bauteile.NichtInHalle(name);
            Assert.True(drin ^ (grund is not null), name);
        }
        Assert.All(Bauteile.TabelleHalle.Keys, n => Assert.True(alle.ContainsKey(n), n));
        Assert.NotNull(Katalog.Bauteil(Bauteile.HallenTuer));
        Assert.All(Bauteile.TabelleAufsatz.Keys, n => Assert.NotNull(Katalog.Bauteil(Bauteile.PraefixAufsatz + n)));
        // die Art eines Teils haengt am Paket: ein Name des Factory Kit ist im Wohnbau unbekannt
        Assert.Null(new Bauteile.Teil(Bauteile.Praefix + "structure-wall", 0, 0, 0, 0).Art);
        Assert.NotNull(new Bauteile.Teil(Bauteile.PraefixHalle + "structure-wall", 0, 0, 0, 0).Art);
    }

    /// <summary>Wand der Halle je Drehung: im Drittel-Raster, Ebene 1,0, sie ragt nur zur Seite der Fassade aus dem Feld.</summary>
    [Fact]
    public void Halle_Wand_ImDrittelRaster_NurZurFassadeHinaus()
    {
        const float f = Bauteile.HalleMassstab;
        foreach (var name in new[] { "structure-wall", "structure-window", "structure-doorway", "structure-corner-inner" })
        {
            for (var dreh = 0; dreh < 4; dreh++)
            {
                var t = new Bauteile.Teil(Bauteile.PraefixHalle + name, 5, 7, 1, dreh);
                var gm = Bauteile.Zusammensetzen("x", [t], Katalog.Bauteil);
                var (min, max) = (gm.Min, gm.Max);
                Assert.InRange(min.Y, 1f - 0.001f, 1f + 0.001f);
                Assert.InRange(max.Y, 2f - 0.001f, 2f + 0.001f);
                var fassade = t.Fassade;
                // aus dem Feld [5/3, 6/3] x [7/3, 8/3] hinaus nur zur Fassade (um eine Feldbreite, die Mauerkrone)
                Assert.InRange(min.X, (5 * f) - ((fassade & Bauteile.Seiten.West) != 0 ? f : 0) - 0.001f, (5 * f) + 0.001f + 1);
                Assert.True(min.X >= (5 * f) - ((fassade & Bauteile.Seiten.West) != 0 ? f : 0) - 0.001f, $"{name} {dreh} West");
                Assert.True(max.X <= (6 * f) + ((fassade & Bauteile.Seiten.Ost) != 0 ? f : 0) + 0.001f, $"{name} {dreh} Ost");
                Assert.True(min.Z >= (7 * f) - ((fassade & Bauteile.Seiten.Nord) != 0 ? f : 0) - 0.001f, $"{name} {dreh} Nord");
                Assert.True(max.Z <= (8 * f) + ((fassade & Bauteile.Seiten.Sued) != 0 ? f : 0) + 0.001f, $"{name} {dreh} Sued");
                // und tut es wirklich (sonst stimmt die Tabelle nicht)
                if ((fassade & Bauteile.Seiten.Sued) != 0)
                {
                    Assert.True(max.Z > (8 * f) + 0.01f, $"{name} {dreh}: Fassade Sued, aber nichts dort");
                }
                if ((fassade & Bauteile.Seiten.West) != 0)
                {
                    Assert.True(min.X < (5 * f) - 0.01f, $"{name} {dreh}: Fassade West, aber nichts dort");
                }
            }
        }
    }

    /// <summary>
    /// Die Huelle der Probehalle ist geschlossen: von der Mitte jedes Felds aus trifft ein waagrechter Strahl (Hoehe 0,5) in
    /// jede Richtung eine Wand, spaetestens in der Mauerkrone (eine Feldbreite hinter der Kante der Grundflaeche; die Wand
    /// steht 0,1 Feld hinter der Kante, das Tuerblatt im Vorbau) (Fotos P12-93: mit dem falschen Eckteil
    /// „corner-outer“ standen die Ecken offen und die Waende ragten schraeg hinaus; Gegenprobe mit diesem Teil).
    /// </summary>
    [Fact]
    public void Halle_Probehalle_HuelleGeschlossen_GegenprobeFalscheEcke()
    {
        var o = OffeneStellen(Entwurf.Probehalle());
        Assert.True(o.Count == 0, string.Join("; ", o));
        var falsch = new Entwurf { HalleWunsch = true };
        foreach (var t in Entwurf.Probehalle().Teile)
        {
            falsch.Setzen(t.Id.Replace("structure-corner-inner", "structure-corner-outer", StringComparison.Ordinal), t.X, t.Y, t.Stockwerk, t.Dreh);
        }
        Assert.NotEmpty(OffeneStellen(falsch));
    }

    private static List<string> OffeneStellen(Entwurf e)
    {
        var gm = Bauteile.Zusammensetzen("x", e.Teile, Katalog.Bauteil);
        const float f = Bauteile.HalleMassstab;
        var g = e.Teile.Where(t => t.Stockwerk == 0).ToList();
        var (x0, x1, y0, y1) = (g.Min(t => t.X), g.Max(t => t.X), g.Min(t => t.Y), g.Max(t => t.Y));
        var offen = new List<string>();
        for (var x = x0; x <= x1; x++)
        {
            for (var y = y0; y <= y1; y++)
            {
                var start = new Vector3((x + 0.5f) * f, 0.5f, (y + 0.5f) * f);
                foreach (var (r, bis) in new (Vector3, float)[]
                {
                    (Vector3.UnitX, ((x1 + 1) * f) - start.X), (-Vector3.UnitX, start.X - (x0 * f)),
                    (Vector3.UnitZ, ((y1 + 1) * f) - start.Z), (-Vector3.UnitZ, start.Z - (y0 * f)),
                })
                {
                    if (Treffer(gm, start, r) is not { } d || d > bis + f)
                    {
                        offen.Add($"{x},{y} {r}");
                    }
                }
            }
        }
        return offen;
    }

    private static float? Treffer(GrafikModell m, Vector3 o, Vector3 r)
    {
        float? best = null;
        for (var i = 0; i + 8 < m.Ecken.Length; i += 9)
        {
            var a = new Vector3(m.Ecken[i], m.Ecken[i + 1], m.Ecken[i + 2]);
            var b = new Vector3(m.Ecken[i + 3], m.Ecken[i + 4], m.Ecken[i + 5]);
            var c = new Vector3(m.Ecken[i + 6], m.Ecken[i + 7], m.Ecken[i + 8]);
            var e1 = b - a;
            var e2 = c - a;
            var p = Vector3.Cross(r, e2);
            var det = Vector3.Dot(e1, p);
            if (MathF.Abs(det) < 1e-9f)
            {
                continue;
            }
            var tv = o - a;
            var u = Vector3.Dot(tv, p) / det;
            var q = Vector3.Cross(tv, e1);
            var v = Vector3.Dot(r, q) / det;
            var d = Vector3.Dot(e2, q) / det;
            if (u >= 0 && v >= 0 && u + v <= 1 && d > 0.001f && (best is null || d < best))
            {
                best = d;
            }
        }
        return best;
    }

    [Fact]
    public void Halle_Probehalle_OhneRot_UnterDerGrenze_GrundrissImDrittelMass()
    {
        var e = Entwurf.Probehalle();
        Assert.True(e.Halle);
        Assert.Equal(48, e.RasterFelder);
        Assert.DoesNotContain(e.Pruefen(Katalog.Bauteil), x => x.Rot);
        var dreiecke = Bauteile.Dreiecke(e.Teile, Katalog.Bauteil);
        Assert.InRange(dreiecke, 1000, Bauteile.GrenzeDreiecke);
        var gm = e.Modell3D("bau:probehalle", Katalog.Bauteil);
        Assert.Equal(dreiecke, gm.Dreiecke);
        // 3 x 2 Felder, die Mauerkrone ragt ringsum ein Drittel hinaus, Dach auf 1,0, Schornstein darueber
        Assert.InRange(gm.Max.X - gm.Min.X, 3.66f, 3.68f);
        Assert.InRange(gm.Max.Z - gm.Min.Z, 2.66f, 2.68f);
        Assert.True(gm.Max.Y > 1.5f);
        var g = e.GrundrissVorschlag()!;
        // Eingang vorne (Sued): Fassade 9 Felder breit = Hoehe des Plans, Tiefe 6 Felder = Breite, je Feld 4/3
        Assert.Equal(6 * Bauteile.InnenJeFeld / 3, Py.F(g["breite"]), 6);
        Assert.Equal(9 * Bauteile.InnenJeFeld / 3, Py.F(g["hoehe"]), 6);
        // Raster der Halle: 48 Felder je Seite
        Assert.True(e.Setzen(Bauteile.PraefixHalle + "floor", 47, 47, 0, 0) >= 0);
        Assert.Equal(-1, e.Setzen(Bauteile.PraefixHalle + "floor", 48, 0, 0, 0));
    }

    [Fact]
    public void Halle_Gemischt_IstRot_AufsatzPasstZuBeiden()
    {
        var e = Entwurf.Probehalle();
        Assert.True(e.Setzen(Bauteile.Praefix + "building-block", 12, 12, 0, 0) >= 0);
        Assert.Contains(e.Pruefen(Katalog.Bauteil), x => x.Rot && x.Text.EndsWith(Texte.EntwurfGemischt, StringComparison.Ordinal));
        // Aufsatz auf dem Flachdach eines Wohnhauses: kein Fund, steht auf dem Dach
        var w = Entwurf.Probehaus(1);
        Assert.True(w.Setzen(Bauteile.PraefixAufsatz + "solar-panel-flat", 1, 0, 1, 0) >= 0);
        Assert.False(w.Halle);
        Assert.DoesNotContain(w.Pruefen(Katalog.Bauteil), x => x.Rot);
        var t = w.Teile[^1];
        var m = Katalog.Bauteil(t.Id)!;
        var lage = Bauteile.Lage(t, m, false);
        Assert.InRange(Vector3.Transform(m.Min, lage).Y, Bauteile.Stockwerk + Bauteile.DachDetailHoehe - 0.001f, Bauteile.Stockwerk + Bauteile.DachDetailHoehe + 0.001f);
        // in der Halle steht derselbe Aufsatz auf dem Hallendach (Hoehe 1,0)
        var h = Entwurf.Probehalle();
        var s = h.Teile.Single(x => Bauteile.IstAufsatz(x.Id));
        var ms = Katalog.Bauteil(s.Id)!;
        Assert.InRange(Vector3.Transform(ms.Min, Bauteile.Lage(s, ms, true)).Y, 0.999f, 1.001f);
        // Aufsatz ohne Dach darunter: in der Luft (rot)
        Assert.True(h.Setzen(Bauteile.PraefixAufsatz + "detail-tank", 30, 30, 1, 0) >= 0);
        Assert.Contains(h.Pruefen(Katalog.Bauteil), x => x.Rot && x.Text.EndsWith(Texte.EntwurfInDerLuft, StringComparison.Ordinal));
    }

    /// <summary>
    /// Die Halle in der Stadt: die Tuer im Tuerteil ist die eigene Tuer des Gebaeudes (keine Kastentuer), sie liegt auf der
    /// Aussenseite des Blocks, traegt das Tuerblatt in der gewaehlten Tuer-Farbe und die Klinke; kein roter Fund.
    /// </summary>
    [Fact]
    public void Halle_InDerStadt_EigeneTuerMitTuerblatt_KeineKastentuer_KeinRot()
    {
        var p = Gesetzt("Halle", Entwurf.Probehalle());
        var sz = Szene(p);
        var t = Assert.Single(sz.Tueren);
        Assert.True(t.Modell);
        Assert.Equal(0, Kastenteile(sz, t));
        Assert.Single(Klinken(sz, t));
        Assert.Empty(TuerFunde(p));
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot);
        // Tuer-Farbe schwarz: das Tuerblatt folgt
        var kennung = Assert.Single(Bauteile.Gebaeude(Aussen.Stadtdaten(p.AussenStand))).Key;
        p.AussenAendern("Farbe", a => AussenBearbeitung.FarbeSetzen(a, kennung, Gebaeudefarben.TuerSchluessel, "schwarz"));
        var sz2 = Szene(p);
        var t2 = Assert.Single(sz2.Tueren);
        var haus = Assert.Single(sz2.Haeuser, h => h.Id.StartsWith(Bauteile.ModellPraefix, StringComparison.Ordinal));
        // vorderste Flaeche des Tuerblatts (die Rueckseite liegt 0,1 dahinter und zeigt nach innen)
        var eigene = StadtTueren.ModellTueren(sz2.Stuecke, haus.StueckeVon, haus.StueckeBis, sz2.ModellAn, t2.Mitte, t2.Aussen, weite: 0.1f, glas: true)
            .Where(g => g.A1 <= 0.001f && g.A2 >= -0.001f && StadtTueren.IstTuerblatt(Farbe(sz2, g.Stueck, g.Dreiecke[0]))).MinBy(g => g.Tiefe)!;
        // das ganze Tuerblatt (Rahmen, Fuellung, Rueckseite) in der gewaehlten Farbe, kein Rest im Holzton
        var gmH = sz2.ModellAn(eigene.Stueck)!;
        var tuerDreiecke = Katalog.Bauteil(Bauteile.HallenTuer)!.Dreiecke;
        Assert.Equal(tuerDreiecke, Enumerable.Range(0, gmH.Dreiecke).Count(d => Farbe(sz2, eigene.Stueck, d) == Netz.Farbe(Gebaeudefarben.TuerVarianten.Single(v => v.Id == "schwarz").Wert!)));
        Assert.DoesNotContain(Enumerable.Range(0, gmH.Dreiecke), d => Farbe(sz2, eigene.Stueck, d) == StadtTueren.Tuerblatt);
        var schwarz = Gebaeudefarben.TuerVarianten.Single(v => v.Id == "schwarz").Wert;
        Assert.All(eigene.Dreiecke, d => Assert.Equal(Netz.Farbe(schwarz), Farbe(sz2, eigene.Stueck, d)));
    }

    private static Vector3 Farbe(StadtSzene sz, int stueck, int d)
    {
        var gm = sz.ModellAn(stueck)!;
        return new Vector3(gm.Farben[d * 3], gm.Farben[(d * 3) + 1], gm.Farben[(d * 3) + 2]) / 255f;
    }
}
