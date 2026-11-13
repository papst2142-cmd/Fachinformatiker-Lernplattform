using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Spiel.Probe;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Weisung P12-240 (Nicos Abnahmesatz 4 „steht neben der Haltestelle“): die Figur steht an jeder Haltestelle frei, beim Warten
/// (auch wenn der Bus haelt und „Einsteigen“ unten steht) und nach dem Aussteigen. Geprueft wird die gezeichnete Figur gegen
/// die gezeichneten Modelle der Haltestelle (Mast, Schild, Haeuschen mit Bank): keine Ecke der einen in der anderen und keine
/// Kante, die durch eine Flaeche der anderen geht (Grenze 0), auch 0,02 versetzt in vier Richtungen (Spielraum wie
/// <c>SpielSitzenTests</c>).
/// </summary>
public sealed class SpielMitfahrtFreiTests
{
    internal readonly record struct Dreieck(Vector3 A, Vector3 B, Vector3 C)
    {
        public Vector3 Min => Vector3.Min(A, Vector3.Min(B, C));

        public Vector3 Max => Vector3.Max(A, Vector3.Max(B, C));
    }

    private static readonly Lazy<Netz> FigurStehend = new(() => Lernplattform.Core.Spiel.Probe.Figur.Netz(SpielGrafikTests.Bibliothek.Value, null, 1f));

    internal static List<Dreieck> Dreiecke(Netz netz, Matrix4x4 lage)
    {
        var e = netz.Ecken;
        Vector3 Ecke(uint i) => Vector3.Transform(new Vector3(e[(int)i * Netz.Zahlen], e[((int)i * Netz.Zahlen) + 1], e[((int)i * Netz.Zahlen) + 2]), lage);
        var liste = new List<Dreieck>();
        for (var i = 0; i + 2 < netz.Indizes.Count; i += 3)
        {
            liste.Add(new Dreieck(Ecke(netz.Indizes[i]), Ecke(netz.Indizes[i + 1]), Ecke(netz.Indizes[i + 2])));
        }
        return liste;
    }

    /// <summary>Die Figur wie in der Stadt gezeichnet (Groesse, Blick, Hoehe) an der Stelle (x, z).</summary>
    internal static List<Dreieck> Figur(float x, float z, float richtung) =>
        Dreiecke(FigurStehend.Value, Matrix4x4.CreateScale(Lernplattform.Core.Spiel.Probe.Figur.StadtHoehe) * Matrix4x4.CreateRotationY(richtung * MathF.PI / 180f)
            * Matrix4x4.CreateTranslation(x, StadtSzene.Hoehe.Figur, z));

    /// <summary>Mast, Schild und Haeuschen der Haltestelle, so wie die Stadt sie zeichnet.</summary>
    internal static List<Dreieck> Haltestelle(Haltestelle h)
    {
        var netz = new Netz();
        Haltestellen.Hinzu(netz, [h]);
        return Dreiecke(netz, Matrix4x4.Identity);
    }

    /// <summary>Zahl der Durchdringungen: Ecken in der anderen Huelle plus Kanten durch Flaechen der anderen, beide Richtungen.</summary>
    internal static int Durchdringungen(List<Dreieck> a, List<Dreieck> b)
    {
        var (amin, amax) = Huelle(a);
        var (bmin, bmax) = Huelle(b);
        if (Getrennt(amin, amax, bmin, bmax))
        {
            return 0;
        }
        return a.SelectMany(d => new[] { d.A, d.B, d.C }).Distinct().Count(p => Innen(p, b))
            + b.SelectMany(d => new[] { d.A, d.B, d.C }).Distinct().Count(p => Innen(p, a))
            + Kreuzungen(a, b) + Kreuzungen(b, a);
    }

    private static bool Getrennt(Vector3 amin, Vector3 amax, Vector3 bmin, Vector3 bmax) =>
        amin.X > bmax.X || bmin.X > amax.X || amin.Y > bmax.Y || bmin.Y > amax.Y || amin.Z > bmax.Z || bmin.Z > amax.Z;

    private static (Vector3 Min, Vector3 Max) Huelle(List<Dreieck> d)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var x in d)
        {
            min = Vector3.Min(min, x.Min);
            max = Vector3.Max(max, x.Max);
        }
        return (min, max);
    }

    /// <summary>Kanten von <paramref name="kanten"/>, die eine Flaeche von <paramref name="flaechen"/> schneiden.</summary>
    private static int Kreuzungen(List<Dreieck> kanten, List<Dreieck> flaechen)
    {
        var zahl = 0;
        foreach (var d in kanten)
        {
            foreach (var (p, q) in new[] { (d.A, d.B), (d.B, d.C), (d.C, d.A) })
            {
                var (kmin, kmax) = (Vector3.Min(p, q), Vector3.Max(p, q));
                foreach (var f in flaechen)
                {
                    if (!Getrennt(kmin, kmax, f.Min, f.Max) && Schneidet(p, q - p, f, 1f))
                    {
                        zahl++;
                    }
                }
            }
        }
        return zahl;
    }

    /// <summary>Strahl/Strecke ab <paramref name="o"/> in Richtung <paramref name="r"/> trifft das Dreieck bei 0 &lt; t &lt; bis (Moeller-Trumbore).</summary>
    private static bool Schneidet(Vector3 o, Vector3 r, Dreieck d, float bis)
    {
        Vector3 e1 = d.B - d.A, e2 = d.C - d.A;
        var q = Vector3.Cross(r, e2);
        var det = Vector3.Dot(e1, q);
        if (MathF.Abs(det) < 1e-12f)
        {
            return false;
        }
        var t0 = o - d.A;
        var u = Vector3.Dot(t0, q) / det;
        if (u < 0 || u > 1)
        {
            return false;
        }
        var k = Vector3.Cross(t0, e1);
        var v = Vector3.Dot(r, k) / det;
        if (v < 0 || u + v > 1)
        {
            return false;
        }
        var t = Vector3.Dot(e2, k) / det;
        return t > 1e-5f && t < bis - 1e-5f;
    }

    private static readonly Vector3[] Strahlen =
        [Vector3.Normalize(new(0.13f, 1, 0.07f)), Vector3.Normalize(new(0.07f, -1, 0.11f)), Vector3.Normalize(new(1, 0.05f, 0.09f))];

    /// <summary>Punkt im Netz: Strahl in drei Richtungen, ungerade Zahl der Treffer in mindestens zwei.</summary>
    private static bool Innen(Vector3 punkt, List<Dreieck> netz) =>
        Strahlen.Count(r => netz.Count(d => Schneidet(punkt, r, d, float.MaxValue)) % 2 == 1) >= 2;

    /// <summary>
    /// So weit muss der Mast mindestens von der Mitte der Figur weg sein: Kreis um die Figur (Radius plus Breite des Rings)
    /// plus 0,02 Spielraum. Sonst steht der Mast im Kreis und sieht im Bild aus, als stuende er in der Figur (Fund P12-240).
    /// </summary>
    internal const float MastFrei = RingRadius + 0.06f + 0.02f;

    /// <summary>Radius des Kreises um die Figur, wie <c>StadtFlaeche.RingRadius</c> (der Test unten haelt beide gleich).</summary>
    internal const float RingRadius = 0.22f;

    /// <summary>Der Kreis um die Figur in der Oberflaeche ist so gross wie hier angenommen.</summary>
    [Fact]
    public void RingRadius_GleichDerOberflaeche() =>
        Assert.Contains("public const float RingRadius = 0.22f;", File.ReadAllText(Path.Combine(Pfade.Wurzel, "src", "Lernplattform.App", "Spiel", "StadtFlaeche.cs")), StringComparison.Ordinal);

    /// <summary>Die Stelle des Wartplatzes bis Weisung P12-240 (0,1 vor der Mitte der Haltestelle), nur fuer die Gegenprobe.</summary>
    internal static Vector2 WartplatzAlt(Haltestelle h) =>
        h.Mitte + (h.Fahrt * 0.1f) + (h.Aussen * ((StadtSzene.StrassenBreite / 2 + h.Halb) / 2));

    /// <summary>Halbe Breite der Figur (waagrecht, gemessen in <c>Figur_Breite_Diagnose</c>: 0,064) aufgerundet.</summary>
    internal const float FigurHalb = 0.07f;

    /// <summary>
    /// Steht die Figur auf dem Gehweg (ganz ausserhalb der Strasse) und, wenn ein Bus haelt, laengs neben ihm?
    /// Liefert den Grund, wenn nicht, sonst null.
    /// </summary>
    internal static string? GehwegNebenBus(Haltestelle h, Vector2 figur, Verkehr.Auto? bus)
    {
        var quer = Vector2.Dot(figur - h.Mitte, h.Aussen);
        if (quer < (StadtSzene.StrassenBreite / 2) + FigurHalb)
        {
            return string.Create(CultureInfo.InvariantCulture, $"auf der Strasse (quer {quer:0.000})");
        }
        if (bus is not null)
        {
            var (ort, richtung) = bus.Lage();
            var laengs = Vector2.Dot(figur - ort, richtung);
            if (MathF.Abs(laengs) > bus.Halb)
            {
                return string.Create(CultureInfo.InvariantCulture, $"nicht neben dem Bus (laengs {laengs:0.000}, Bus halb {bus.Halb:0.000})");
            }
        }
        return null;
    }

    private static readonly Vector2[] Versatz = [Vector2.Zero, new(0.02f, 0), new(-0.02f, 0), new(0, 0.02f), new(0, -0.02f)];

    /// <summary>Durchdringungen der Figur an (x, z) mit der Haltestelle, an der Stelle und 0,02 versetzt (die groesste Zahl).</summary>
    internal static int AnStelle(Haltestelle h, float x, float z, float richtung)
    {
        var modelle = Haltestelle(h);
        return Versatz.Max(s => Durchdringungen(Figur(x + s.X, z + s.Y, richtung), modelle));
    }

    /// <summary>
    /// Nicos Ziel an allen Haltestellen der Linie, an den Spieltagen 1 bis 3: hingehen und warten (Bild 1), der Bus haelt und
    /// „Einsteigen“ steht unten (Bild 2), einsteigen, „Aussteigen“, ausgestiegen an der naechsten (Bild 5): jedes Mal frei.
    /// </summary>
    [Theory]
    [InlineData(1L)]
    [InlineData(2L)]
    [InlineData(3L)]
    public void Figur_StehtFrei_BeimWarten_BeimEinsteigen_NachDemAussteigen(long tag)
    {
        var spiel = SpielMitfahrtTests.Stadt1();
        var linie = spiel.Linie!;
        var funde = new List<string>();
        var geprueft = 0;
        void Pruefen(string wann, Haltestelle h, Verkehr.Auto? bus)
        {
            var n = AnStelle(h, spiel.FigurX, spiel.FigurZ, spiel.FigurRichtung);
            var f = new Vector2(spiel.FigurX, spiel.FigurZ);
            var mast = Vector2.Distance(f, h.Schild);
            var gehweg = GehwegNebenBus(h, f, bus);
            geprueft++;
            if (n > 0 || mast < MastFrei || gehweg is not null)
            {
                funde.Add(string.Create(CultureInfo.InvariantCulture,
                    $"Tag {tag}, Haltestelle {h.Nr}, {wann}: {n} Durchdringungen, Mast {mast:0.000} weg (mindestens {MastFrei:0.00}), {gehweg ?? "Gehweg ok"} bei ({spiel.FigurX:0.00},{spiel.FigurZ:0.00})"));
            }
        }
        foreach (var h in spiel.Szene.Haltestellen.Where(h => linie.Halte.Any(l => l.Haltestelle == h.Nr)))
        {
            var v = Verkehr.FuerSpieltag(spiel.Szene, tag);
            var runde = SpielMitfahrtTests.Runde(v);
            SpielMitfahrtTests.Hingehen(spiel, v, h);
            Assert.Equal(h.Nr, spiel.WartetAn);
            Pruefen("wartet", h, null);
            Assert.True(SpielMitfahrtTests.Bis(spiel, v, () => spiel.BusZumEinsteigen is not null, runde), $"Haltestelle {h.Nr}: kein Bus");
            Pruefen("Bus haelt, Einsteigen", h, v.Autos.Single(a => a.Nummer == spiel.BusZumEinsteigen));
            Assert.True(spiel.Knopf(StadtSpiel.AktionEinsteigen));
            Assert.True(SpielMitfahrtTests.Bis(spiel, v, () => spiel.SitztImBus, 30), $"Haltestelle {h.Nr}: steigt nicht ein");
            Assert.True(spiel.Knopf(StadtSpiel.AktionAussteigen));
            Assert.True(SpielMitfahrtTests.Bis(spiel, v, () => spiel.Mitfahrt == StadtSpiel.MitfahrtStufe.Keine, runde), $"Haltestelle {h.Nr}: steigt nicht aus");
            var aus = spiel.Szene.Haltestellen.Single(x => x.Nr == spiel.LetzteMitfahrt!.Value.Aus);
            var f = new Vector2(spiel.FigurX, spiel.FigurZ);
            Pruefen("ausgestiegen", aus, v.Autos.Where(a => a.Nummer >= Buslinie.ErsteNummer).MinBy(a => Vector2.Distance(a.Lage().Ort, f)));
        }
        Assert.True(geprueft >= 18, "geprueft: " + geprueft);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    /// <summary>
    /// Gegenprobe: die Pruefung findet eine Figur am Fuss des Masts, unter dem Schild (Mast 0,05 neben der Mitte der Figur) und
    /// mitten im Haeuschen, und die Stelle bis P12-240 hat den Mast im Kreis um die Figur; dazu die Abstaende der Stellen (Wartplatz, Tuer) zum Mast in der Diagnose.
    /// </summary>
    [Fact]
    public void Gegenprobe_FigurImMastOderHaeuschen_WirdGefunden()
    {
        var spiel = SpielMitfahrtTests.Stadt1();
        foreach (var h in spiel.Szene.Haltestellen)
        {
            var blick = MathF.Atan2(-h.Aussen.X, -h.Aussen.Y) * 180f / MathF.PI;
            var mast = Durchdringungen(Figur(h.Schild.X, h.Schild.Y, blick), Haltestelle(h));
            var daneben = h.Schild + (h.Fahrt * 0.05f);
            var nah = Durchdringungen(Figur(daneben.X, daneben.Y, blick), Haltestelle(h));
            var haeuschen = Durchdringungen(Figur(h.Haeuschen.X, h.Haeuschen.Y, blick), Haltestelle(h));
            Assert.True(mast > 0 && nah > 0 && haeuschen > 0, $"Haltestelle {h.Nr}: Mast {mast}, 0,05 daneben {nah}, Haeuschen {haeuschen}");
            // die Stelle bis P12-240: keine Durchdringung, aber der Mast steht im Kreis um die Figur (rot)
            var alt = WartplatzAlt(h);
            Assert.True(Vector2.Distance(alt, h.Schild) < MastFrei, $"Haltestelle {h.Nr}: alte Stelle {Vector2.Distance(alt, h.Schild):0.000} vom Mast");
            Assert.True(Vector2.Distance(StadtSpiel.Wartplatz(h), h.Schild) >= MastFrei, $"Haltestelle {h.Nr}: neue Stelle zu nah am Mast");
            // Gehweg: die Tuer (Bordsteinkante) und die Strassenmitte zaehlen als Strasse, der Wartplatz nicht
            Assert.NotNull(GehwegNebenBus(h, StadtSpiel.Tuer(h), null));
            Assert.NotNull(GehwegNebenBus(h, h.Mitte, null));
            Assert.Null(GehwegNebenBus(h, StadtSpiel.Wartplatz(h), null));
            var w = StadtSpiel.Wartplatz(h);
            TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture,
                $"Haltestelle {h.Nr}: Mast {mast}, 0,05 daneben {nah}, Haeuschen {haeuschen}; Wartplatz zum Mast {Vector2.Distance(w, h.Schild):0.000}, Tuer zum Mast {Vector2.Distance(StadtSpiel.Tuer(h), h.Schild):0.000}"));
        }
    }

    /// <summary>Wie breit ist die Figur? (Diagnose: groesster Abstand einer Ecke von der Mitte, waagrecht.)</summary>
    [Fact]
    public void Figur_Breite_Diagnose()
    {
        var f = Figur(0, 0, 0);
        var r = f.SelectMany(d => new[] { d.A, d.B, d.C }).Max(p => MathF.Sqrt((p.X * p.X) + (p.Z * p.Z)));
        Assert.InRange(r, 0.05f, 0.3f);
        TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture, $"Figur waagrecht bis {r:0.000} von der Mitte, {f.Count} Dreiecke"));
    }
}
