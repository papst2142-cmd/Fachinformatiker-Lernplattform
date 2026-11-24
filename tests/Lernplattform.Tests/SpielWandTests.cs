using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Sammelliste 347 (Weisung P12-269, Nicos Fund 15.11.2026: "Die Stuehle bei dem Tisch ragen durch die Wand ... Alle
/// Gegenstaende duerfen nicht durch die Wand glitchen"): in keinem Innenraum ragt ein Gegenstand durch eine Wand. Geprueft
/// werden alle Innenraeume (jede Etage, neuer, spaeter und gespielter Stand, mit und ohne Fest) der Fassung 0 und der aktiven
/// Stadt 1, jedes Einrichtungsstueck mit seinen echten Ecken (Modelle) und Quadern, die Fest-Deko und die Personen. Eine Wand
/// ist jeder gezeichnete Quader eines Wandstuecks (Glas der Fenster eingeschlossen). "Durch die Wand" heisst: eine Kante eines
/// Dreiecks oder ein Quader reicht ueber die Mitte der Wand (die Linie des Grundrisses) mehr als 0,02 auf die andere Seite
/// (Grenze 0, Spielraum 0,02 wie <see cref="SpielSitzenTests"/>); siehe <see cref="Funde"/>. Dazu jedes Moebel der Wohnung an
/// jeder Wand jeder Wohnung in jeder Drehung.
/// Gegenprobe: die Stuehle am Esstisch wie bis A 0.79.0 (<see cref="Innenraum.WandAltFuerTests"/>) werden rot.
/// </summary>
public sealed class SpielWandTests
{
    private const float Spiel = 0.02f;

    private static readonly Lazy<PyObjekt> Fassung2Innen = new(() => StadtbauFassung2Tests.Fassung2.Value.Innenraeume);

    /// <summary>Was an der Wand haengt (Bild, Tafel ...) darf sie beruehren, nie durch sie hindurch: heute keine Ausnahme.</summary>
    private static readonly HashSet<string> Ausnahmen = new(StringComparer.Ordinal);

    internal static IEnumerable<(string Name, PyObjekt Innen)> Fassungen()
    {
        yield return ("Fassung 0", SpielInnenraumTests.Innenraeume.Value);
        yield return ("Stadt 1 aktiv", Fassung2Innen.Value);
    }

    /// <summary>Alle Innenraeume einer Fassung: jede Ansicht, jede Etage, drei Spielstaende; im neuen Stand dazu mit jedem Fest.</summary>
    internal static IEnumerable<Innenraum> AlleRaeume(PyObjekt innenraeume)
    {
        var neu = new Spielablauf(SpielInhaltTests.Inhalt).Stand;
        var spaet = new Spielablauf(SpielInhaltTests.Inhalt).Stand;
        spaet.TageFertig = 200;
        var gespielt = SpielFingerabdruckTests.Gespielt.Value.Stand;
        var ansichten = new List<string> { "zuhause", "buero", "filiale", "serverfarm", "schalter:bank", "schalter:amt", "geschaeft:moebelhaus", "geschaeft:elektromarkt", "geschaeft:kaufhaus" };
        ansichten.AddRange(SpielDaten.Kundenorte(SpielInhaltTests.Inhalt).Select(k => "kunde:" + Py.Str(k["id"])));
        var grafik = SpielGrafikTests.Bibliothek.Value;
        var geschaefte = SpielGeschaefteTests.Daten.Value;
        var feste = FestDekoTests.Daten.Value;
        var alleFeste = SpielDaten.Objekt(feste["zeitraeume"]).Select(p => p.Key).ToList();
        foreach (var stand in new[] { neu, spaet, gespielt })
        {
            foreach (var basis in ansichten)
            {
                for (var etage = 0; etage < 10; etage++)
                {
                    var ansicht = Lernplattform.Core.Stadtbau.Etagen.Schluessel(basis, etage);
                    if (Innenraum.Bauen(ansicht, stand, innenraeume, grafik, geschaefte) is not { } innen)
                    {
                        break;
                    }
                    yield return innen;
                    foreach (var fest in ReferenceEquals(stand, neu) ? alleFeste : [])
                    {
                        if (Innenraum.Bauen(ansicht, stand, innenraeume, grafik, geschaefte, fest, feste) is { } mitFest)
                        {
                            yield return mitFest;
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void KeinGegenstandRagtDurchEineWand_AlleInnenraeume()
    {
        var funde = new List<string>();
        var raeume = 0;
        var stuecke = 0;
        foreach (var (name, innenraeume) in Fassungen())
        {
            foreach (var innen in AlleRaeume(innenraeume))
            {
                raeume++;
                stuecke += innen.DekoUmrisse.Count;
                funde.AddRange(Funde(innen).Select(f => $"{name} {innen.Ansicht}{(innen.Fest is null ? "" : " (Fest " + innen.Fest + ")")}: {f}"));
            }
        }
        Assert.True(funde.Count == 0, funde.Count + " Funde:\n" + string.Join("\n", funde.Distinct().Take(80)));
        // jede Fassung, jede Ansicht und Etage (Stand A 0.80.0: rund 200 Raeume, ueber 4000 Stuecke)
        Assert.True(raeume >= 100 && stuecke >= 2000, $"geprueft: {raeume} Raeume, {stuecke} Stuecke");
    }

    /// <summary>
    /// Nicos Ziel auch fuer die eigene Wohnung: jedes Moebel des Ladens, in jeder Wohnung an jeder Wand jedes Raums (Mitte und
    /// beide Ecken) und in jeder Drehung, wo das Spiel das Aufstellen erlaubt, ragt nicht durch eine Wand.
    /// </summary>
    [Fact]
    public void JedesMoebelDerWohnung_AnJederWand_NieDurchDieWand()
    {
        var inhalt = SpielInhaltTests.Inhalt;
        var grafik = SpielGrafikTests.Bibliothek.Value;
        var funde = new List<string>();
        var geprueft = 0;
        var wohnungen = SpielDaten.Liste(SpielDaten.Objekt(inhalt["wohnungen"])["wohnungen"]).Select(SpielDaten.Objekt).ToList();
        foreach (var bleibe in wohnungen)
        {
            var stand = new Spielablauf(inhalt).Stand;
            stand.Wohnung = bleibe["id"];
            var gebaeude = SpielDaten.Objekt(bleibe["gebaeude"]);
            foreach (var moebel in Wohnung.Laden(inhalt))
            {
                stand.Moebel.Clear();
                stand.Moebel["probe"] = moebel["id"];
                foreach (var raum in SpielDaten.Liste(gebaeude["raeume"]).Select(SpielDaten.Objekt))
                {
                    double rx = Py.F(raum["x"]), ry = Py.F(raum["y"]), rw = Py.F(raum["w"]), rh = Py.F(raum["h"]);
                    var stellen = new HashSet<(object X, object Y, long Dreh)>();
                    for (var dreh = 0L; dreh < 4; dreh++)
                    {
                        foreach (var (cx, cy) in new[]
                        {
                            (rx + (rw / 2), ry), (rx + (rw / 2), ry + rh), (rx, ry + (rh / 2)), (rx + rw, ry + (rh / 2)),
                            (rx + 0.01, ry + 0.01), (rx + rw - 0.01, ry + 0.01), (rx + 0.01, ry + rh - 0.01), (rx + rw - 0.01, ry + rh - 0.01),
                        })
                        {
                            var (x, y) = Wohnung.Andocken(stand, moebel["id"], cx, cy, dreh);
                            var leer = new PyObjekt { { "moebel", new PyObjekt() }, { "boeden", new PyObjekt() } };
                            if (Wohnung.PlatzProblem(stand, "probe", moebel["id"], x, y, dreh, leer) == "")
                            {
                                stellen.Add((x, y, dreh));
                            }
                        }
                    }
                    foreach (var (x, y, dreh) in stellen)
                    {
                        stand.Einrichtungen[stand.Wohnung] = new PyObjekt
                        {
                            { "moebel", new PyObjekt { { "probe", new List<object?> { x, y, dreh } } } }, { "boeden", new PyObjekt() },
                        };
                        var innen = Innenraum.Bauen("zuhause", stand, SpielInnenraumTests.Innenraeume.Value, grafik)!;
                        geprueft++;
                        var umriss = innen.DekoUmrisse.Where(u => Py.Gleich(SpielDaten.Get(u.Deko, "moebel"), moebel["id"])).ToList();
                        Assert.NotEmpty(umriss);
                        funde.AddRange(Funde(innen, umriss).Select(f => $"{Py.Str(bleibe["id"])} {Py.Str(raum["id"])} {Py.Str(moebel["id"])} ({Py.Str(x)}, {Py.Str(y)}, dreh {dreh}): {f}"));
                    }
                }
            }
        }
        Assert.True(funde.Count == 0, funde.Count + " Funde:\n" + string.Join("\n", funde.Take(80)));
        Assert.True(geprueft >= 500, "geprueft: " + geprueft);
    }

    /// <summary>Gegenprobe: mit den Stuehlen wie bis A 0.79.0 (immer vor und hinter dem Tisch) findet die Pruefung Nicos Fund.</summary>
    [Fact]
    public void Gegenprobe_StuehleWieBisher_RagenDurchDieWand()
    {
        Innenraum.WandAltFuerTests.Value = true;
        try
        {
            var funde = Fassungen().SelectMany(f => AlleRaeume(f.Innen).Where(i => i.Ansicht.Contains("moebelhaus", StringComparison.Ordinal))
                .SelectMany(i => Funde(i).Select(x => f.Name + " " + i.Ansicht + ": " + x))).ToList();
            Assert.Contains(funde, f => f.Contains("esstisch", StringComparison.Ordinal) && f.Contains("chair", StringComparison.Ordinal));
        }
        finally
        {
            Innenraum.WandAltFuerTests.Value = false;
        }
    }

    /// <summary>Gegenprobe der Rechnung: eine Kante quer durch eine Wand und ein Quader ueber die Mitte hinaus sind Funde, bis zur Mitte (+0,02) nicht.</summary>
    [Fact]
    public void Gegenprobe_Rechnung()
    {
        // Innenwand x 4,92..5,08 (Mitte 5), Stueck links davon (Anker x 3)
        var wand = new WandTeil(new Teil(TeilArt.Quader, 4.92f, 0, 0, 5.08f, 3, 4, Vector3.Zero), Vector2.Zero);
        var anker = new Vector2(3, 2);
        Assert.True(KastenDurch(new Teil(TeilArt.Quader, 4, 0, 1, 5.05f, 1, 2, Vector3.Zero), wand, anker));
        Assert.False(KastenDurch(new Teil(TeilArt.Quader, 4, 0, 1, 5.0f + Spiel, 1, 2, Vector3.Zero), wand, anker));
        Assert.True(KanteDurch(new Vector3(4, 0.5f, 1), new Vector3(6, 0.5f, 1), wand, anker));
        Assert.False(KanteDurch(new Vector3(4, 0.5f, 1), new Vector3(4.99f, 0.5f, 1), wand, anker));
        // von der anderen Seite gilt dasselbe gespiegelt; neben der Wand (Tueroeffnung) ist nichts ein Fund
        Assert.True(KanteDurch(new Vector3(6, 0.5f, 1), new Vector3(4.9f, 0.5f, 1), wand, new Vector2(7, 2)));
        Assert.False(KanteDurch(new Vector3(4, 0.5f, 5), new Vector3(6, 0.5f, 5), wand, anker));
        // Aussenwand (nach draussen -z): ueber die Mitte nach draussen ist ein Fund, egal wo der Anker liegt
        var aussen = new WandTeil(new Teil(TeilArt.Quader, 0, 0, -0.15f, 10, 3, 0.15f, Vector3.Zero), new Vector2(0, -1));
        Assert.True(KastenDurch(new Teil(TeilArt.Quader, 1, 0, -0.05f, 2, 1, 0.5f, Vector3.Zero), aussen, new Vector2(5, -1)));
        Assert.False(KastenDurch(new Teil(TeilArt.Quader, 1, 0, 0, 2, 1, 0.5f, Vector3.Zero), aussen, new Vector2(5, 5)));
    }

    /// <summary>
    /// Ein gezeichneter Quader eines Wandstuecks; <see cref="Aussen"/> zeigt bei einer Aussenwand nach draussen, sonst 0.
    /// <see cref="Quer"/>: die Wand laeuft laengs z (Dicke in x); null = aus der Form des Quaders. Welle A 1.6.0 (Sammelliste 443):
    /// aus dem ganzen Wandstueck, weil ein Pfosten der Zarge (0,06 laengs, 0,16 dick) sonst als Wand quer zur echten Wand galt.
    /// </summary>
    internal readonly record struct WandTeil(Teil Q, Vector2 Aussen, bool? Quer = null);

    internal static IEnumerable<WandTeil> WandQuader(Innenraum innen) =>
        innen.Waende.SelectMany(w => innen.Teile.Skip(w.Von).Take(w.Bis - w.Von).Where(t => t.Art == TeilArt.Quader)
            .Select(t => new WandTeil(t, w.Aussen, w.Max.X - w.Min.X <= w.Max.Z - w.Min.Z)));

    /// <summary>
    /// Alle Funde eines Raums (oder nur der genannten Stuecke). "Durch die Wand" heisst: ein Teil reicht ueber die Mitte der
    /// Wand (= die Linie des Grundrisses) mehr als 0,02 auf die andere Seite, bei einer Aussenwand nach draussen, bei einer
    /// Innenwand auf die Seite, auf der das Stueck nicht steht (Anker = Mitte seines Rechtecks im Grundriss). Die Wand steht
    /// mittig auf dieser Linie; was nur bis zur Mitte in die Wand reicht (Rueckseite an der Wand), sieht man von keiner Seite.
    /// </summary>
    internal static List<string> Funde(Innenraum innen, IEnumerable<DekoUmriss>? nur = null)
    {
        var grafik = SpielGrafikTests.Bibliothek.Value;
        var waende = WandQuader(innen).ToList();
        var funde = new List<string>();
        string Wand(WandTeil w) => $"Wand ({w.Q.X1:0.00}..{w.Q.X2:0.00}, {w.Q.Z1:0.00}..{w.Q.Z2:0.00})";
        void Pruefen(string was, Vector2? anker, IEnumerable<Teil> kaesten, IEnumerable<ModellStueck> modelle)
        {
            foreach (var k in kaesten)
            {
                var a = anker ?? new Vector2((k.X1 + k.X2) / 2, (k.Z1 + k.Z2) / 2);
                foreach (var w in waende.Where(w => KastenDurch(k, w, a)).Take(1))
                {
                    funde.Add($"{was}: Quader ({k.X1:0.00}..{k.X2:0.00}, {k.Z1:0.00}..{k.Z2:0.00}) durch die {Wand(w)}");
                }
            }
            foreach (var m in modelle)
            {
                if (grafik.Modell(m.Name) is not { } gm)
                {
                    continue;
                }
                var dreiecke = SpielSitzenTests.Dreiecke(gm, m.Lage);
                var (min, max) = SpielSitzenTests.Huelle(dreiecke);
                var a = anker ?? new Vector2((min.X + max.X) / 2, (min.Z + max.Z) / 2);
                foreach (var w in waende.Where(w => Halbe(w, a) is var (lo, hi) && min.X < hi.X && max.X > lo.X && min.Y < hi.Y && max.Y > lo.Y && min.Z < hi.Z && max.Z > lo.Z))
                {
                    if (dreiecke.Any(d => KanteDurch(d.A, d.B, w, a) || KanteDurch(d.B, d.C, w, a) || KanteDurch(d.C, d.A, w, a)))
                    {
                        funde.Add($"{was}: {m.Name} ({min.X:0.00}..{max.X:0.00}, {min.Z:0.00}..{max.Z:0.00}) durch die {Wand(w)}");
                        break;
                    }
                }
            }
        }
        foreach (var u in nur ?? innen.DekoUmrisse)
        {
            var typ = Py.Str(u.Deko["typ"]);
            if (Ausnahmen.Contains(typ))
            {
                continue;
            }
            double x = Py.F(u.Deko["x"]), y = Py.F(u.Deko["y"]), w = Py.F(u.Deko["w"]), h = Py.F(u.Deko["h"]);
            Pruefen($"{typ}@{x:0.##},{y:0.##}", new Vector2((float)(x + (w / 2)), (float)(y + (h / 2))), u.Kaesten, u.Modelle);
        }
        if (nur is null)
        {
            // Fest-Deko: jedes Teil mit seiner eigenen Mitte als Anker
            Pruefen("Fest-Deko", null, innen.FestKaesten, innen.FestModelle);
            foreach (var p in innen.Personen)
            {
                // die Person als stehende Figur in voller Hoehe an ihrer Stelle (jede Blickrichtung: 0,3 um die Mitte)
                const float r = 0.3f;
                Pruefen("Person " + p.Id, new Vector2(p.X, p.Z), [new Teil(TeilArt.Quader, p.X - r, 0, p.Z - r, p.X + r, Innenraum.FigurHoehe, p.Z + r, Vector3.Zero)], []);
            }
        }
        return funde;
    }

    /// <summary>
    /// Die Haelfte der Wand jenseits ihrer Mitte (+0,02), von <paramref name="anker"/> aus gesehen (Aussenwand: die aeussere
    /// Haelfte), seitlich und in der Hoehe um 0,02 verkleinert.
    /// </summary>
    private static (Vector3 Lo, Vector3 Hi) Halbe(WandTeil w, Vector2 anker)
    {
        var q = w.Q;
        var lo = new Vector3(MathF.Min(q.X1, q.X2), MathF.Min(q.Y1, q.Y2), MathF.Min(q.Z1, q.Z2));
        var hi = new Vector3(MathF.Max(q.X1, q.X2), MathF.Max(q.Y1, q.Y2), MathF.Max(q.Z1, q.Z2));
        var quer = w.Quer ?? hi.X - lo.X <= hi.Z - lo.Z;   // Wand laeuft laengs z: Dicke in x
        lo.Y += Spiel;
        hi.Y -= Spiel;
        if (quer)
        {
            var mitte = (lo.X + hi.X) / 2;
            var nachPlus = w.Aussen.X != 0 ? w.Aussen.X > 0 : anker.X < mitte;
            (lo.X, hi.X) = nachPlus ? (mitte + Spiel, hi.X + 1) : (lo.X - 1, mitte - Spiel);
            lo.Z += Spiel;
            hi.Z -= Spiel;
        }
        else
        {
            var mitte = (lo.Z + hi.Z) / 2;
            var nachPlus = w.Aussen.Y != 0 ? w.Aussen.Y > 0 : anker.Y < mitte;
            (lo.Z, hi.Z) = nachPlus ? (mitte + Spiel, hi.Z + 1) : (lo.Z - 1, mitte - Spiel);
            lo.X += Spiel;
            hi.X -= Spiel;
        }
        return (lo, hi);
    }

    internal static bool KastenDurch(Teil k, WandTeil w, Vector2 anker)
    {
        var (lo, hi) = Halbe(w, anker);
        return MathF.Min(k.X1, k.X2) < hi.X && MathF.Max(k.X1, k.X2) > lo.X && MathF.Min(k.Y1, k.Y2) < hi.Y && MathF.Max(k.Y1, k.Y2) > lo.Y
            && MathF.Min(k.Z1, k.Z2) < hi.Z && MathF.Max(k.Z1, k.Z2) > lo.Z;
    }

    /// <summary>Strecke a-b schneidet die jenseitige Haelfte der Wand (Plattenverfahren).</summary>
    internal static bool KanteDurch(Vector3 a, Vector3 b, WandTeil w, Vector2 anker)
    {
        var (lo, hi) = Halbe(w, anker);
        if (lo.X >= hi.X || lo.Y >= hi.Y || lo.Z >= hi.Z)
        {
            return false;
        }
        float t0 = 0, t1 = 1;
        var d = b - a;
        for (var i = 0; i < 3; i++)
        {
            var (s, dd, l, h) = i switch { 0 => (a.X, d.X, lo.X, hi.X), 1 => (a.Y, d.Y, lo.Y, hi.Y), _ => (a.Z, d.Z, lo.Z, hi.Z) };
            if (MathF.Abs(dd) < 1e-9f)
            {
                if (s <= l || s >= h)
                {
                    return false;
                }
                continue;
            }
            var u = (l - s) / dd;
            var v = (h - s) / dd;
            if (u > v)
            {
                (u, v) = (v, u);
            }
            t0 = MathF.Max(t0, u);
            t1 = MathF.Min(t1, v);
            if (t0 >= t1)
            {
                return false;
            }
        }
        return true;
    }
}
