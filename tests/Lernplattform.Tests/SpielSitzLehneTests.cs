using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.3.1 (Weisung P12-348, Sammelliste 431, Nico 22.11.2026: „Die Person sitzt zu weit vorne, selbst in der zurück
/// gelehnten Position“, „da sitzt der Charakter auf der Fußstütze“; Nico 1a „Beine auf dem Polster“). Abnahme: auf jedem
/// Sitzmoebel aller Innenraeume (beide Fassungen) und der Wohnung sitzt die Figur, wie sie gezeichnet wird, gerade und
/// zurueckgelehnt
/// <list type="bullet">
/// <item>mit der Huefte auf der Sitzflaeche: unter dem Gesaess liegt das Polster hoechstens <see cref="HuefteHoechstens"/> tiefer
/// (Anteil der Figurhoehe), die Huefte steckt hoechstens <see cref="HuefteEinsinken"/> darin, und die Huefte liegt innerhalb der
/// Grundflaeche des Moebels;</item>
/// <item>mit dem Ruecken an der Lehne: von jeder Stelle des Rueckens nach hinten bis zum Moebel hoechstens
/// <see cref="RueckenHoechstens"/> (der kleinste Abstand zaehlt). Dazu kommt nur der Weg, um den die Wandregel 347 den Platz
/// nach vorn schiebt (hoechstens 0,07, damit der Spitzhut nicht ueber die Wandmitte reicht; Sofa und Sessel direkt an der
/// Wand der Wohnung). Bank und Hocker haben keine Lehne (hinter dem Ruecken ist nichts vom Moebel, <see cref="OhneLehne"/>).
/// Feste Ausnahme <see cref="Ausnahmen"/>: der Wartestuhl im Amt (dahinter haengt die Anzeigetafel, schon 0,03 weiter hinten
/// stiesse der Kopf an und der Platz fiele weg);</item>
/// <item>damit nie auf einer Fussstuetze oder Armlehne (die Lehne ist hinter dem Ruecken, das Polster unter der Huefte).</item>
/// </list>
/// Gegenprobe: der Stand bis A 1.3.0 (<see cref="Figur.SitzenLangAltFuerTests"/>) ist auf Sofa, Sessel und Liegestuhl rot.
/// </summary>
public class SpielSitzLehneTests
{
    /// <summary>Feste Grenze: so weit (Anteil der Figurhoehe) darf der Ruecken hoechstens vor der Lehne sein.</summary>
    private const float RueckenHoechstens = 0.06f;

    /// <summary>Feste Grenze: so weit (Anteil der Figurhoehe) darf das Gesaess hoechstens ueber dem Polster sein.</summary>
    private const float HuefteHoechstens = 0.02f;

    /// <summary>Feste Grenze: so weit (Anteil der Figurhoehe) darf das Gesaess hoechstens im Polster stecken.</summary>
    private const float HuefteEinsinken = 0.01f;

    private sealed record Fall(string Name, Sitzgelegenheit Platz, List<SpielSitzenTests.Dreieck> Moebel, float Schub);

    /// <summary>Bank und Hocker (Sitzsack) ohne Lehne: hinter dem Ruecken liegt nichts vom Moebel.</summary>
    private static readonly IReadOnlySet<string> OhneLehne = new HashSet<string>(StringComparer.Ordinal) { "bench", "loungeSofaOttoman" };

    /// <summary>Feste Ausnahme mit Grund (Modell, Raum, gemessener Hoechstwert): der Wartestuhl im Amt vor der Anzeigetafel.</summary>
    private static readonly IReadOnlyList<(string Modell, string Raum, float Ruecken)> Ausnahmen = [("kkChairA", "schalter:amt", 0.085f)];

    /// <summary>Alle Plaetze zum Sitzen und Zuruecklehnen (ohne PC-Stuhl) aller Innenraeume und der Wohnung (Sofa, Sessel, Liegestuhl).</summary>
    private static List<Fall> Faelle()
    {
        var faelle = new List<Fall>();
        var gesehen = new HashSet<string>();
        void Raum(string name, Innenraum innen)
        {
            List<(string Name, List<SpielSitzenTests.Dreieck> D)>? h = null;
            foreach (var s in innen.Sitzgelegenheiten.Where(s => s.Art is SitzArt.Sitzen or SitzArt.Zurueck))
            {
                h ??= SpielHaltungTests.Hindernisse(innen);
                var moebel = h.Where(x => x.Name == s.Modell).OrderBy(x =>
                {
                    var (lo, hi) = SpielSitzenTests.Huelle(x.D);
                    return Vector2.Distance(new Vector2((lo.X + hi.X) / 2, (lo.Z + hi.Z) / 2), s.Mitte);
                }).First().D;
                var (lo2, hi2) = SpielSitzenTests.Huelle(moebel);
                if (gesehen.Add(string.Create(CultureInfo.InvariantCulture, $"{s.Modell}|{s.Art}|{(hi2 - lo2).X:0.00}|{(hi2 - lo2).Y:0.00}|{(hi2 - lo2).Z:0.00}|{name}")))
                {
                    // Weg nach vorn durch die Wandregel 347: Platz ohne Wand (Platz) gegen den Platz des Raums
                    var vor = new Vector3(MathF.Sin(s.Blick), 0, MathF.Cos(s.Blick));
                    var g = SpielGrafikTests.Bibliothek.Value;
                    var ohneWand = innen.Stuecke.Where(st => st.Name == s.Modell).Select(st => g.Modell(st.Name) is { } gm
                        ? Innenraum.Platz(st.Name, gm.Min, gm.Max, st.Lage, Innenraum.FigurHoehe, null, s.Art == SitzArt.Zurueck) : null)
                        .First(p => p is not null && p.Rechteck == s.Rechteck)!;
                    faelle.Add(new Fall(string.Create(CultureInfo.InvariantCulture, $"{name} {s.Modell} {s.Art} ({s.Ort.X:0.00}, {s.Ort.Z:0.00})"), s, moebel,
                        Vector3.Dot(s.Ort - ohneWand.Ort, vor) / Innenraum.FigurHoehe));
                }
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
        foreach (var moebel in Wohnung.Laden(inhalt).Where(m => Py.Str(m["typ"]) is "sofa" or "sessel" or "liegestuhl" or "sitzsack"))
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

    /// <summary>Messung je Platz: Huefte ueber dem Polster, Huefte in der Grundflaeche, Ruecken bis zur Lehne (Anteile der Figurhoehe).</summary>
    private static (float Huefte, bool InFlaeche, float Ruecken) Messen(Fall f)
    {
        var s = f.Platz;
        var hoehe = Innenraum.FigurHoehe;
        var vor = new Vector3(MathF.Sin(s.Blick), 0, MathF.Cos(s.Blick));
        var quer = new Vector3(vor.Z, 0, -vor.X);
        var figur = SpielSitzenTests.Dreiecke(Figur.Netz(SpielGrafikTests.Bibliothek.Value, null, 1f, null, Figur.SitzBild(s.Art, s.Modell), 0),
            StadtSpiel.HaltungLage((s.Ort, s.Blick, false), hoehe, 0f)).SelectMany(d => new[] { d.A, d.B, d.C }).Distinct().ToList();
        // Gesaess: der tiefste Punkt der Figur nahe der Stelle (laengs und quer hoechstens 0,06 der Figurhoehe)
        var huefte = figur.Where(e => MathF.Abs(Vector3.Dot(e - s.Ort, vor)) < 0.06f * hoehe && MathF.Abs(Vector3.Dot(e - s.Ort, quer)) < 0.06f * hoehe).MinBy(e => e.Y);
        var oben = huefte + new Vector3(0, 0.1f * hoehe, 0);
        var polster = SpielSitzenOhneTischTests.Strahl(oben, -Vector3.UnitY, f.Moebel) is { } t ? oben.Y - t : float.NegativeInfinity;
        var inFlaeche = s.Abstand(huefte.X, huefte.Z) == 0;
        // Ruecken: jede Ecke hinter der Stelle oberhalb der Oberschenkel, nach hinten bis zum Moebel; der kleinste Abstand zaehlt
        var ruecken = figur.Where(e => Vector3.Dot(e - s.Ort, vor) < 0 && e.Y > huefte.Y + (0.12f * hoehe))
            .Select(e => SpielSitzenOhneTischTests.Strahl(e, -vor, f.Moebel)).Where(x => x is not null).Select(x => x!.Value)
            .DefaultIfEmpty(float.PositiveInfinity).Min();
        return ((huefte.Y - polster) / hoehe, inFlaeche, ruecken / hoehe);
    }

    private static List<string> Funde(IEnumerable<Fall> faelle, List<string>? werte = null)
    {
        var funde = new List<string>();
        foreach (var f in faelle)
        {
            var (huefte, inFlaeche, ruecken) = Messen(f);
            werte?.Add(string.Create(CultureInfo.InvariantCulture, $"{f.Name}: Huefte {huefte:0.000}, Ruecken {ruecken:0.000}, Wandschub {f.Schub:0.000}"));
            if (huefte > HuefteHoechstens || huefte < -HuefteEinsinken)
            {
                funde.Add(string.Create(CultureInfo.InvariantCulture, $"{f.Name}: Huefte {huefte:0.000} ueber dem Polster (erlaubt -{HuefteEinsinken} bis {HuefteHoechstens})"));
            }
            if (!inFlaeche)
            {
                funde.Add($"{f.Name}: Huefte ausserhalb der Grundflaeche");
            }
            var raum = Ausnahmen.FirstOrDefault(a => a.Modell == f.Platz.Modell && f.Name.Contains(" " + a.Raum + " ", StringComparison.Ordinal));
            var grenze = raum.Modell is null ? RueckenHoechstens + f.Schub : raum.Ruecken;
            if (OhneLehne.Contains(f.Platz.Modell) ? !float.IsPositiveInfinity(ruecken) : ruecken > grenze)
            {
                funde.Add(string.Create(CultureInfo.InvariantCulture, $"{f.Name}: Ruecken {ruecken:0.000} vor der Lehne (hoechstens {grenze:0.000})"));
            }
            if (f.Schub is < -0.001f or > 0.071f)
            {
                funde.Add(string.Create(CultureInfo.InvariantCulture, $"{f.Name}: Wandregel schiebt {f.Schub:0.000} (erlaubt 0 bis 0,07)"));
            }
        }
        return funde;
    }

    [Fact]
    public void JedesSitzmoebel_HuefteAufDemPolster_RueckenAnDerLehne()
    {
        var faelle = Faelle();
        var werte = new List<string>();
        var funde = Funde(faelle, werte);
        Assert.True(funde.Count == 0, string.Join("\n", funde) + "\n--- alle Werte:\n" + string.Join("\n", werte));
        // jedes Sitzmodell, das heute in einem Raum oder der Wohnung steht, gerade und (wo es das gibt) zurueckgelehnt
        Assert.Equal(["bench", "kkChairA", "loungeChair", "loungeChairRelax", "loungeSofa", "loungeSofaOttoman"],
            faelle.Select(f => f.Platz.Modell).Distinct().Where(m => m != "kkArmchair").Order(StringComparer.Ordinal));
        foreach (var m in Innenraum.ZurueckModelle)
        {
            Assert.Contains(faelle, f => f.Platz.Modell == m && f.Platz.Art == SitzArt.Zurueck);
        }
    }

    /// <summary>Gegenprobe: der Stand bis A 1.3.0 (zu weit vorn, auf der Fussstuetze) faellt auf Sofa, Sessel und Liegestuhl auf.</summary>
    [Fact]
    public void Gegenprobe_StandBisA130_WirdGefunden()
    {
        Figur.SitzenLangAltFuerTests(true);
        try
        {
            var funde = Funde(Faelle());
            foreach (var m in Innenraum.LangModelle.Keys)
            {
                Assert.Contains(funde, x => x.Contains($" {m} Sitzen ", StringComparison.Ordinal) && x.Contains("Ruecken", StringComparison.Ordinal));
                Assert.Contains(funde, x => x.Contains($" {m} Zurueck ", StringComparison.Ordinal) && x.Contains("Ruecken", StringComparison.Ordinal));
            }
        }
        finally
        {
            Figur.SitzenLangAltFuerTests(false);
        }
    }
}
