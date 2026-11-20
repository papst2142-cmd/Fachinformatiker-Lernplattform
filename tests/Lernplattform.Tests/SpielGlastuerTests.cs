using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using GlastuerLage = Lernplattform.Core.Spiel.Probe.Innenraum.GlastuerLage;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.99.0 (Weisung P12-317 F2, Nico: „Erst zu und durch Oeffnen wird sie so geoeffnet wie auf dem Bild.“): die Glastuer
/// jedes Serverschranks ist zu (in der Front, im Rechteck des Schranks), bis die Figur sie oeffnet; dann steht sie 90 Grad offen
/// und beruehrt weder die Figur noch ein Geraet (Durchdringung 0, ±0,02). Nur Darstellung: kein Ereignis. Gegenproben: die
/// Tuer zur falschen Seite beruehrt die Figur; die alte Geometrie hat keine Glastuer.
/// </summary>
public class SpielGlastuerTests
{
    private static Innenraum Serverfarm() =>
        Innenraum.Bauen("serverfarm", new Spielablauf(SpielInhaltTests.Inhalt).Stand, SpielInnenraumTests.Innenraeume.Value, SpielGrafikTests.Bibliothek.Value)!;

    private static (Vector3 Lo, Vector3 Hi) Kasten(Teil t) =>
        (new Vector3(MathF.Min(t.X1, t.X2), MathF.Min(t.Y1, t.Y2), MathF.Min(t.Z1, t.Z2)), new Vector3(MathF.Max(t.X1, t.X2), MathF.Max(t.Y1, t.Y2), MathF.Max(t.Z1, t.Z2)));

    private static List<Teil> Tuer(Innenraum innen, int nr, GlastuerLage lage) => [.. innen.GlastuerTeile(nr, lage, false), .. innen.GlastuerTeile(nr, lage, true)];

    [Fact]
    public void Zu_InDerFront_ImRechteck_EineScheibe()
    {
        var innen = Serverfarm();
        Assert.Equal(8, innen.GlastuerZahl);
        var racks = innen.DekoUmrisse.Where(u => Py.Str(u.Deko["typ"]) == "rack").ToList();
        for (var i = 0; i < innen.GlastuerZahl; i++)
        {
            var d = racks[i].Deko;
            float x = (float)Py.F(d["x"]), z = (float)Py.F(d["y"]), w = (float)Py.F(d["w"]), h = (float)Py.F(d["h"]);
            foreach (var t in Tuer(innen, i, GlastuerLage.Zu))
            {
                var (lo, hi) = Kasten(t);
                Assert.True(lo.X >= x - 0.02f && hi.X <= x + w + 0.02f && lo.Z >= z - 0.02f && hi.Z <= z + h + 0.02f, $"Tuer {i} zu ausserhalb");
                // in der Front zum Gang (Sueden, groesstes z), hoechstens 0,07 tief
                Assert.True(lo.Z >= z + h - 0.07f, $"Tuer {i} nicht vorn: {lo.Z} bei Schrank bis {z + h}");
            }
            Assert.Single(innen.GlastuerTeile(i, GlastuerLage.Zu, true));
            // die Scheibe deckt fast die ganze Front
            var (glo, ghi) = Kasten(innen.GlastuerTeile(i, GlastuerLage.Zu, true)[0]);
            Assert.True(ghi.X - glo.X > w * 0.8f && ghi.Y - glo.Y > Innenraum.Meter * 1.6f);
        }
    }

    [Fact]
    public void Offen_90Grad_NachVorn_ZumScharnier()
    {
        var innen = Serverfarm();
        for (var i = 0; i < innen.GlastuerZahl; i++)
        {
            foreach (var lage in new[] { GlastuerLage.OffenLinks, GlastuerLage.OffenRechts })
            {
                var (lo, hi) = Kasten(innen.GlastuerTeile(i, lage, true)[0]);
                var (zlo, zhi) = Kasten(innen.GlastuerTeile(i, GlastuerLage.Zu, true)[0]);
                // gedreht: die Scheibe steht quer zur Front (lang nach vorn, schmal in x) und beginnt vor dem Schrank
                Assert.True(hi.Z - lo.Z > (zhi.X - zlo.X) * 0.99f && hi.X - lo.X < 0.02f, $"Tuer {i} {lage} nicht 90 Grad");
                Assert.True(lo.Z >= zlo.Z - 0.01f, $"Tuer {i} {lage} nicht nach vorn");
                Assert.True(lage == GlastuerLage.OffenLinks ? lo.X < zlo.X : hi.X > zhi.X, $"Tuer {i} {lage} falsches Scharnier");
            }
        }
    }

    /// <summary>
    /// An jeder Stelle vor einem Schrank (Laufraster, in Reichweite, vor der Front) und jedem Blick (8 Richtungen): oeffnet die Tuer,
    /// beruehrt sie weder die Figur (Koerper der Haltung) noch ein anderes Stueck; sie oeffnet an mindestens 90 % der Stellen mit
    /// Blick zum Schrank. Gegenprobe: zur anderen Seite geoeffnet beruehrt sie an Stellen nahe dem Scharnier die Figur.
    /// </summary>
    [Fact]
    public void Offen_BeruehrtWederFigurNochGeraete_Gegenprobe()
    {
        var innen = Serverfarm();
        var andere = innen.DekoUmrisse.SelectMany(u => u.Teile).Where(t => t.Art == TeilArt.Quader).Select(Kasten).ToList();
        var funde = new List<string>();
        var (stellen, offen, gegenprobe) = (0, 0, 0);
        for (var x = 0.2f; x < innen.Breite; x += 0.1f)
        {
            for (var z = 0.2f; z < innen.Tiefe; z += 0.1f)
            {
                if (!Wege.KannStehen(x, z, innen.Lauf) || innen.GlastuerNahe(x, z) is not { } nr)
                {
                    continue;
                }
                var ort = new Vector2(x, z);
                foreach (var grad in new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
                {
                    var blick = grad * MathF.PI / 180f;
                    var lage = innen.GlastuerWahl(nr, ort, blick, Koerperhaltung.Stehen);
                    if (grad == 180f)
                    {
                        stellen++;
                        offen += lage == GlastuerLage.Zu ? 0 : 1;
                    }
                    if (lage == GlastuerLage.Zu)
                    {
                        continue;
                    }
                    foreach (var t in Tuer(innen, nr, lage))
                    {
                        var (lo, hi) = Kasten(t);
                        if (Innenraum.QuaderBeruehrt(new Vector3(x, 0, z), blick, Innenraum.KoerperQuader(Koerperhaltung.Stehen), Innenraum.FigurHoehe, lo, hi))
                        {
                            funde.Add($"Tuer {nr} {lage} beruehrt die Figur bei {x:0.0}/{z:0.0} Blick {grad}");
                        }
                        if (andere.Any(k => Innenraum.Durchdringt(lo, hi, k.Lo, k.Hi)))
                        {
                            funde.Add($"Tuer {nr} {lage} beruehrt ein Geraet bei {x:0.0}/{z:0.0}");
                        }
                    }
                    var falsch = lage == GlastuerLage.OffenLinks ? GlastuerLage.OffenRechts : GlastuerLage.OffenLinks;
                    gegenprobe += innen.GlastuerFrei(nr, falsch, ort, blick, Koerperhaltung.Stehen) ? 0 : 1;
                }
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde.Distinct().Take(20)));
        Assert.True(stellen > 50, "Stellen vor den Schraenken: " + stellen);
        Assert.True(offen >= stellen * 0.9, $"Tuer oeffnet nur an {offen} von {stellen} Stellen (Blick zum Schrank)");
        Assert.True(gegenprobe > 0, "Gegenprobe: die falsche Seite beruehrt nie etwas");
    }

    [Fact]
    public void AlteGeometrie_OhneGlastuer()
    {
        Innenraum.WerkstattAltFuerTests.Value = true;
        try
        {
            Assert.Equal(0, Serverfarm().GlastuerZahl);
        }
        finally
        {
            Innenraum.WerkstattAltFuerTests.Value = false;
        }
    }

    /// <summary>
    /// Im Spiel: vor dem Schrank „Glastür öffnen“, danach offen und „Glastür schließen“; gehen schliesst sie (E1); kein Ereignis,
    /// der Spielstand bleibt gleich. Ohne 3D kein Knopf (Gegenprobe).
    /// </summary>
    [Fact]
    public void ImSpiel_Oeffnen_Schliessen_GehenSchliesst_KeinEreignis()
    {
        var spiel = SpielHaltungBedienungTests.NeuesSpiel();
        var s = SpielHaltungBedienungTests.ZuHause(spiel);
        // die Serverfarm ist im neuen Spiel gesperrt: ansehen wie das Werkzeug und der Start-Test (nur Darstellung)
        var innen = Serverfarm();
        s.Ansehen(innen, innen.StartX, innen.StartZ, 0f);
        var zeilen = spiel.Speicher.Ereignisse().Count;
        // eine Stelle mittig vor dem ersten Schrank
        var rack = innen.DekoUmrisse.First(u => Py.Str(u.Deko["typ"]) == "rack").Deko;
        var vor = new Vector2((float)(Py.F(rack["x"]) + (Py.F(rack["w"]) / 2)), (float)(Py.F(rack["y"]) + Py.F(rack["h"]) + 0.45));
        Assert.True(s.GeheZu(vor.X, vor.Y));
        SpielHaltungBedienungTests.Ankommen(s);
        Assert.Equal(0, innen.GlastuerNahe(s.FigurX, s.FigurZ));
        Assert.Contains(s.Knoepfe, k => k.Aktion == StadtSpiel.AktionGlastuer && k.Name == SpielProbeTexte.KnopfGlastuerOeffnen);
        Assert.All(s.GlastuerLagen(), l => Assert.Equal(GlastuerLage.Zu, l));
        Assert.True(s.Knopf(StadtSpiel.AktionGlastuer));
        Assert.True(s.GlastuerLagen()[0] != GlastuerLage.Zu, $"zu: Figur {s.FigurX}/{s.FigurZ} Richtung {s.FigurRichtung}, Zeile {s.Zeile}");
        Assert.Contains(s.Knoepfe, k => k.Aktion == StadtSpiel.AktionGlastuer && k.Name == SpielProbeTexte.KnopfGlastuerSchliessen);
        Assert.True(s.Knopf(StadtSpiel.AktionGlastuer));
        Assert.All(s.GlastuerLagen(), l => Assert.Equal(GlastuerLage.Zu, l));
        // wieder oeffnen, dann gehen: zu
        Assert.True(s.Knopf(StadtSpiel.AktionGlastuer));
        Assert.NotNull(s.GlastuerOffen);
        Assert.True(s.GeheZu(vor.X + 1.0, vor.Y + 0.3));
        SpielHaltungBedienungTests.Ankommen(s);
        Assert.Null(s.GlastuerOffen);
        Assert.Equal(zeilen, spiel.Speicher.Ereignisse().Count);
        // Gegenprobe: ohne 3D kein Knopf
        Assert.True(s.GeheZu(vor.X, vor.Y));
        SpielHaltungBedienungTests.Ankommen(s);
        s.OhneDreiDSetzen(true);
        Assert.DoesNotContain(s.Knoepfe, k => k.Aktion == StadtSpiel.AktionGlastuer);
    }
}
