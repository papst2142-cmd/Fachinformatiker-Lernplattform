using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.81.0 (Sammelliste 351, Weisung P12-276, Nico 15.11.2026: „An der Bushaltestelle schwebt man zu hoch und zu weit
/// vorne“, „bitte die Haende dann auf den Beinen ablegen“). Ohne Tisch (jeder Sitzplatz ausser dem PC-Stuhl und die Bank im
/// Haeuschen) gilt fuer die Spielfigur, wie sie gezeichnet wird:
/// <list type="bullet">
/// <item>Hoehe: das Gesaess liegt auf der Sitzflaeche (Abstand 0 bis 0,02 der Figurhoehe).</item>
/// <item>Tiefe: 0,05 der Figurhoehe weiter hinten stiesse die Figur an (an die Lehne, mit den Unterschenkeln an die Vorderkante
/// oder nach den Regeln des Spiels an ein anderes Stueck, <see cref="Innenraum.Beruehrt"/>, oder ueber die Wandmitte); auf der Bank im Haeuschen steht der
/// Kopf (jedes Haar, jede Kopfbedeckung) hoechstens 0,02 vor der Glasrueckwand, und der Ruecken ist mindestens 0,04 naeher an
/// ihr als bis P12-160.</item>
/// <item>Haende: keine Ecke ueber der Brust (0,44) liegt weiter vorn als die Knie: die Arme sind nicht nach vorn gestreckt.</item>
/// </list>
/// Gegenproben: der Stand P12-160 (<see cref="Figur.SitzenAltFuerTests"/>) faellt mit Hoehe (Bank) und Haenden auf, die neue
/// Haltung an der Stelle bis P12-160 (ohne <see cref="Innenraum.SitzTiefe"/>) mit der Tiefe.
/// </summary>
public class SpielSitzenOhneTischTests
{
    private const float Brust = 0.44f;

    /// <summary>Abstand vom Punkt p in Richtung r bis zum ersten Dreieck (Moeller-Trumbore), sonst null.</summary>
    internal static float? Strahl(Vector3 p, Vector3 r, List<SpielSitzenTests.Dreieck> netz)
    {
        float? best = null;
        foreach (var d in netz)
        {
            Vector3 e1 = d.B - d.A, e2 = d.C - d.A;
            var q = Vector3.Cross(r, e2);
            var det = Vector3.Dot(e1, q);
            if (MathF.Abs(det) < 1e-9f)
            {
                continue;
            }
            var s = p - d.A;
            var u = Vector3.Dot(s, q) / det;
            if (u < 0 || u > 1)
            {
                continue;
            }
            var w = Vector3.Cross(s, e1);
            var v = Vector3.Dot(r, w) / det;
            if (v < 0 || u + v > 1)
            {
                continue;
            }
            var t = Vector3.Dot(e2, w) / det;
            if (t >= 0 && (best is null || t < best))
            {
                best = t;
            }
        }
        return best;
    }

    /// <summary>Ein Sitzplatz ohne Tisch: Stelle, Blick, Figurhoehe, Hoehe der Sitzflaeche, Netz des Sitzmoebels, Zeichenlage und die Regel des Spiels gegen andere Stuecke (Verschiebung -> stoesst an).</summary>
    private sealed record Fall(string Name, Vector3 Ort, float Blick, float Hoehe, float SitzY, List<SpielSitzenTests.Dreieck> Moebel, Matrix4x4 Lage, Func<Vector3, bool> Anderes, float Zurueck)
    {
        public Vector3 Vor => new(MathF.Sin(Blick), 0, MathF.Cos(Blick));

        /// <summary>Derselbe Platz um <paramref name="d"/> verschoben.</summary>
        public Fall Um(Vector3 d) => this with { Ort = Ort + d, Lage = Lage * Matrix4x4.CreateTranslation(d), Anderes = x => Anderes(x + d) };
    }

    private static List<Vector3> Figur(Fall f, IReadOnlyList<Figur.Stueck>? kleidung, PyObjekt? aussehen) =>
        SpielSitzenTests.Dreiecke(Core.Spiel.Probe.Figur.Netz(SpielGrafikTests.Bibliothek.Value, aussehen, 1f, kleidung, Core.Spiel.Probe.Figur.SitzBild(SitzArt.Sitzen), 0), f.Lage)
            .SelectMany(d => new[] { d.A, d.B, d.C }).Distinct().ToList();

    /// <summary>Kleinster Abstand von Ecken der Figur (Hoehe ueber der Sitzflaeche von bis) nach hinten bis zum Moebel, Anteil der Figurhoehe.</summary>
    private static float Hinten(Fall f, List<Vector3> figur, float von, float bis) =>
        figur.Where(e => e.Y > f.SitzY + (von * f.Hoehe) && e.Y < f.SitzY + (bis * f.Hoehe)).Select(e => Strahl(e, -f.Vor, f.Moebel))
            .Where(t => t is not null).Select(t => t!.Value).DefaultIfEmpty(float.PositiveInfinity).Min() / f.Hoehe;

    /// <summary>Funde zu Hoehe und Haenden (eine Kleidung).</summary>
    private static IEnumerable<string> HoeheUndHaende(Fall f, List<Vector3> figur)
    {
        var gesaess = (figur.Where(e => Vector3.Dot(e - f.Ort, f.Vor) < 0.02f * f.Hoehe).Min(e => e.Y) - f.SitzY) / f.Hoehe;
        if (gesaess is < -0.002f or > 0.02f)
        {
            yield return $"{f.Name}: Gesaess {gesaess:0.000} ueber der Sitzflaeche (erlaubt 0 bis 0,02)";
        }
        float Vorn(IEnumerable<Vector3> e) => e.Select(p => Vector3.Dot(p - f.Ort, f.Vor)).DefaultIfEmpty(float.NegativeInfinity).Max() / f.Hoehe;
        var knie = Vorn(figur.Where(e => e.Y < f.SitzY + (0.12f * f.Hoehe)));
        var arme = Vorn(figur.Where(e => e.Y > f.Ort.Y + (Brust * f.Hoehe)));
        if (arme > knie)
        {
            yield return $"{f.Name}: Arme {arme:0.000} weiter vorn als die Knie {knie:0.000} (nach vorn gestreckt)";
        }
    }

    /// <summary>Die Regel des Spiels (<see cref="Innenraum.Beruehrt"/> wie beim Aufbau): die Figur auf dem um d verschobenen Platz beruehrt ein anderes Stueck.</summary>
    private static Func<Vector3, bool> Regel(Innenraum innen, Sitzgelegenheit s)
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        (Vector3, Vector3) Huelle(Teil t) =>
            (new Vector3(MathF.Min(t.X1, t.X2), MathF.Min(t.Y1, t.Y2), MathF.Min(t.Z1, t.Z2)), new Vector3(MathF.Max(t.X1, t.X2), MathF.Max(t.Y1, t.Y2), MathF.Max(t.Z1, t.Z2)));
        IEnumerable<(Vector3, Vector3)> Modelle(IEnumerable<ModellStueck> m) => m.Select(x => (x, gm: g.Modell(x.Name))).Where(x => x.gm is not null).Select(x =>
        {
            var e = Enumerable.Range(0, 8).Select(i => Vector3.Transform(new Vector3((i & 1) == 0 ? x.gm!.Min.X : x.gm!.Max.X, (i & 2) == 0 ? x.gm.Min.Y : x.gm.Max.Y, (i & 4) == 0 ? x.gm.Min.Z : x.gm.Max.Z), x.x.Lage)).ToList();
            return (e.Aggregate(Vector3.Min), e.Aggregate(Vector3.Max));
        });
        var kaesten = innen.DekoUmrisse.Where(u => !ReferenceEquals(u.Deko, s.Deko)).SelectMany(u => u.Kaesten.Where(k => k.Art == TeilArt.Quader).Select(Huelle).Concat(Modelle(u.Modelle)))
            .Concat(innen.FestKaesten.Where(k => k.Art == TeilArt.Quader).Select(Huelle)).Concat(Modelle(innen.FestModelle)).Where(k => k.Item2.Y > 0.05f).ToList();
        // dazu die Wand (Regel 347): die Grundflaeche der Figur reicht nie ueber ihre Mitte
        return d => innen.UeberDieWand(s with { Ort = s.Ort + d }) || kaesten.Any(k => Innenraum.Beruehrt(s with { Ort = s.Ort + d }, Innenraum.FigurHoehe, k.Item1, k.Item2));
    }

    /// <summary>Alle Sitzplaetze ohne Tisch aller Innenraeume (beide Fassungen) und der Wohnung (Sofa, Sessel, Liegestuhl).</summary>
    private static List<Fall> Innen()
    {
        var faelle = new List<Fall>();
        var gesehen = new HashSet<string>();
        void Raum(string name, Innenraum innen)
        {
            List<(string Name, List<SpielSitzenTests.Dreieck> D)>? h = null;
            foreach (var s in innen.Sitzgelegenheiten.Where(s => s.Art == SitzArt.Sitzen && Innenraum.SitzTiefe.ContainsKey(s.Modell)))
            {
                h ??= SpielHaltungTests.Hindernisse(innen);
                var moebel = h.Where(x => x.Name == s.Modell).OrderBy(x =>
                {
                    var (lo, hi) = SpielSitzenTests.Huelle(x.D);
                    return Vector2.Distance(new Vector2((lo.X + hi.X) / 2, (lo.Z + hi.Z) / 2), s.Mitte);
                }).First().D;
                var (lo2, hi2) = SpielSitzenTests.Huelle(moebel);
                if (!gesehen.Add($"{s.Modell}|{(hi2 - lo2).X:0.00}|{(hi2 - lo2).Z:0.00}|{name}|{s.Ort}"))
                {
                    continue;
                }
                var sitzY = lo2.Y + ((hi2.Y - lo2.Y) * Innenraum.SitzModelle[s.Modell].Sitz);
                faelle.Add(new Fall($"{name} {s.Modell}", s.Ort, s.Blick, Innenraum.FigurHoehe, sitzY, moebel,
                    StadtSpiel.HaltungLage((s.Ort, s.Blick, false), Innenraum.FigurHoehe, 0f), Regel(innen, s), Innenraum.Zurueck(s.Modell, (MathF.Abs(MathF.Sin(s.Blick)) * (hi2 - lo2).X) + (MathF.Abs(MathF.Cos(s.Blick)) * (hi2 - lo2).Z), Innenraum.FigurHoehe)));
            }
        }
        foreach (var (name, innenraeume) in SpielWandTests.Fassungen())
        {
            foreach (var innen in SpielWandTests.AlleRaeume(innenraeume).Where(i => i.Fest is null))
            {
                Raum($"{name} {innen.Ansicht}", innen);
            }
        }
        var inhalt = SpielInhaltTests.Inhalt;
        var bleibe = SpielDaten.Liste(SpielDaten.Objekt(inhalt["wohnungen"])["wohnungen"]).Select(SpielDaten.Objekt).First();
        var raum = SpielDaten.Liste(SpielDaten.Objekt(bleibe["gebaeude"])["raeume"]).Select(SpielDaten.Objekt).First();
        foreach (var moebel in Wohnung.Laden(inhalt).Where(m => Py.Str(m["typ"]) is "sofa" or "sessel" or "liegestuhl"))
        {
            var stand = new Spielablauf(inhalt).Stand;
            stand.Wohnung = bleibe["id"];
            stand.Moebel["probe"] = moebel["id"];
            var (x, y) = Wohnung.Andocken(stand, moebel["id"], Py.F(raum["x"]) + (Py.F(raum["w"]) / 2), Py.F(raum["y"]), 0L);
            stand.Einrichtungen[stand.Wohnung] = new PyObjekt { { "moebel", new PyObjekt { { "probe", new List<object?> { x, y, 0L } } } }, { "boeden", new PyObjekt() } };
            Raum("Wohnung " + Py.Str(moebel["id"]), Innenraum.Bauen("zuhause", stand, SpielInnenraumTests.Innenraeume.Value, SpielGrafikTests.Bibliothek.Value)!);
        }
        return faelle;
    }

    private static readonly Haltestelle Halt = new(1, new Vector2(3, 4), Vector2.Normalize(new Vector2(1, 0.3f)), Vector2.Normalize(new Vector2(-0.3f, 1)), 0.3f);

    private static List<SpielSitzenTests.Dreieck> Haeuschen() =>
        SpielSitzenTests.Dreiecke(Haltestellen.HaeuschenNetz, Haltestellen.Lage(Halt.Haeuschen, Halt.Fahrt, Halt.Aussen));

    /// <summary>Jedes Haar und jede Kopfbedeckung der Pruefungen (der Kopf reicht verschieden weit nach hinten).</summary>
    private static IEnumerable<(string Name, PyObjekt? Aussehen, IReadOnlyList<Figur.Stueck>? Kleidung)> Koepfe() =>
        from frisur in new[] { "kurz", "lang", "zopf", "locken", "glatze" }
        from kleid in Enumerable.Range(0, SpielHaltungTests.Kleider.Count)
        select ($"{frisur} Kleid {kleid}", new PyObjekt { { "frisur", frisur } }, SpielHaltungTests.Kleider[kleid]);

    /// <summary>Die Bank im Haeuschen fuer einen Kopf, gezeichnet wie <c>StadtFlaeche</c> (Boden der Strasse unter der stehenden Figur).</summary>
    private static Fall Bank(PyObjekt? aussehen, IReadOnlyList<Figur.Stueck>? kleidung, List<SpielSitzenTests.Dreieck> haeuschen)
    {
        var (ort, blick) = Haltestellen.BankSitz(Halt, Core.Spiel.Probe.Figur.KopfHinten(SpielGrafikTests.Bibliothek.Value, aussehen, kleidung));
        return new Fall("Bank im Haeuschen", ort, blick, Core.Spiel.Probe.Figur.StadtHoehe, 0.12f, haeuschen,
            StadtSpiel.HaltungLage((ort, blick, false), Core.Spiel.Probe.Figur.StadtHoehe, StadtSzene.Hoehe.Figur), _ => false, 0f);
    }

    /// <summary>Funde je Platz: Hoehe, Haende und Tiefe (0,05 weiter hinten stiesse die Figur an).</summary>
    private static List<string> Funde(IEnumerable<Fall> faelle)
    {
        var funde = new List<string>();
        foreach (var f in faelle)
        {
            var figur = Figur(f, null, null);
            funde.AddRange(HoeheUndHaende(f, figur));
            var weiter = f.Um(-f.Vor * (0.05f * f.Hoehe));
            if (!weiter.Anderes(Vector3.Zero) && SpielHaltungTests.Durchdringung(SpielSitzenTests.Dreiecke(Core.Spiel.Probe.Figur.Netz(SpielGrafikTests.Bibliothek.Value,
                null, 1f, null, Core.Spiel.Probe.Figur.SitzBild(SitzArt.Sitzen), 0), weiter.Lage), f.Moebel) == 0)
            {
                funde.Add($"{f.Name}: 0,05 weiter hinten waere noch frei (Ruecken {Hinten(f, figur, 0.1f, 0.4f):0.000} bis zur Lehne)");
            }
        }
        return funde;
    }

    [Fact]
    public void Innen_AufDerSitzflaeche_AnDerLehne_HaendeNichtNachVorn()
    {
        var faelle = Innen();
        Assert.True(faelle.Count >= 20, "Plaetze: " + faelle.Count);
        // jedes Modell der Tabelle kommt vor (kkArmchair = Wartesessel, nicht in jeder Stadt)
        Assert.Equal(string.Join(",", Innenraum.SitzTiefe.Keys.Where(k => k != "kkArmchair").Order()), string.Join(",", faelle.Select(f => f.Name.Split(" ")[^1]).Distinct().Where(m => m != "kkArmchair").Order()));
        var funde = Funde(faelle);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    [Fact]
    public void Bank_AufDerBank_KopfAnDerGlasrueckwand_HaendeNichtNachVorn()
    {
        var haeuschen = Haeuschen();
        var funde = new List<string>();
        Core.Spiel.Probe.Figur.SitzenAltFuerTests(true);
        float rueckenAlt;
        try
        {
            var alt = Bank(null, null, haeuschen);
            rueckenAlt = Hinten(alt, Figur(alt, null, null), 0.1f, 0.4f);
        }
        finally
        {
            Core.Spiel.Probe.Figur.SitzenAltFuerTests(false);
        }
        foreach (var (name, aussehen, kleidung) in Koepfe())
        {
            var f = Bank(aussehen, kleidung, haeuschen);
            var figur = Figur(f, kleidung, aussehen);
            funde.AddRange(HoeheUndHaende(f, figur).Select(x => name + ": " + x));
            var kopf = Hinten(f, figur, 0.1f, 2f);
            if (kopf > 0.02f)
            {
                funde.Add($"{name}: Kopf {kopf:0.000} vor der Glasrueckwand (hoechstens 0,02)");
            }
            if (SpielHaltungTests.Durchdringung(SpielSitzenTests.Dreiecke(Core.Spiel.Probe.Figur.Netz(SpielGrafikTests.Bibliothek.Value, aussehen, 1f, kleidung,
                Core.Spiel.Probe.Figur.SitzBild(SitzArt.Sitzen), 0), f.Lage), haeuschen) is > 0 and var n)
            {
                funde.Add($"{name}: {n} Ecken im Haeuschen");
            }
        }
        var ohne = Bank(null, null, haeuschen);
        var ruecken = Hinten(ohne, Figur(ohne, null, null), 0.1f, 0.4f);
        if (!(ruecken <= rueckenAlt - 0.04f))
        {
            funde.Add($"Ruecken {ruecken:0.000} bis zur Glasrueckwand, bis P12-160 {rueckenAlt:0.000} (mindestens 0,04 naeher verlangt)");
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    /// <summary>Gegenprobe Stand P12-160: Haltung mit nach vorn gestreckten Armen und Figur ueber der Bank werden gefunden.</summary>
    [Fact]
    public void Gegenprobe_HaltungBisP12160_WirdGefunden()
    {
        Core.Spiel.Probe.Figur.SitzenAltFuerTests(true);
        try
        {
            var funde = Funde(Innen());
            Assert.Contains(funde, x => x.Contains("nach vorn gestreckt", StringComparison.Ordinal));
            var bank = Bank(null, null, Haeuschen());
            var hoehe = HoeheUndHaende(bank, Figur(bank, null, null)).ToList();
            Assert.Contains(hoehe, x => x.Contains("Gesaess", StringComparison.Ordinal));
            Assert.Contains(hoehe, x => x.Contains("nach vorn gestreckt", StringComparison.Ordinal));
            Assert.True(Hinten(bank, Figur(bank, null, null), 0.1f, 2f) > 0.02f);
        }
        finally
        {
            Core.Spiel.Probe.Figur.SitzenAltFuerTests(false);
        }
    }

    /// <summary>Gegenprobe Tiefe: die neue Haltung an der Stelle bis P12-160 (an der Vorderkante, ohne SitzTiefe) wird bei jedem Modell mit Tiefe gefunden.</summary>
    [Fact]
    public void Gegenprobe_StelleAnDerVorderkante_WirdGefunden()
    {
        var faelle = Innen().Where(f => f.Zurueck > 0).ToList();
        var vorn = faelle.Select(f => f.Um(f.Vor * f.Zurueck)).ToList();
        var funde = Funde(vorn);
        foreach (var modell in faelle.Select(f => f.Name.Split(" ")[^1]).Distinct())
        {
            Assert.Contains(funde, x => x.Contains(modell + ": 0,05 weiter hinten", StringComparison.Ordinal));
        }
    }
}
