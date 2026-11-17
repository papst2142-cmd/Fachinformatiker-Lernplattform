using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Sammelliste 380 (Weisung P12-298, Nico: „die Figur liegt quer im Bett“): fuer jeden Liegeplatz in jedem Innenraum (beide
/// Fassungen, drei Spielstaende) und fuer jedes Bett und jede Matratze der Wohnung an jeder Wand und in jeder Drehung liegt die
/// Figur laengs: ihre Koerperachse ist parallel zur Laengsachse des Bettes (vom Kopfteil zum Fussende des Modells), der Kopf
/// liegt in der Haelfte am Kopfteil, und das Bett ist laenger als breit gezeichnet (Kopfteil an einer kurzen Seite).
/// Die Gegenprobe (Stand bis A 0.89.0) findet die quer liegende Figur im Moebelhaus.
/// </summary>
public class SpielBettLaengsTests
{
    /// <summary>Funde fuer alle Liegeplaetze eines Raums.</summary>
    internal static List<string> Funde(Innenraum innen, string wo)
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        var funde = new List<string>();
        foreach (var s in innen.Sitzgelegenheiten.Where(s => s.Art == SitzArt.Liegen))
        {
            // das Modell des Platzes: gleicher Name, gleiche Grundflaeche
            var stueck = innen.Stuecke.Where(st => st.Name == s.Modell).Select(st =>
            {
                var gm = g.Modell(st.Name)!;
                var gr = gm.Max - gm.Min;
                Vector3 L(float fx, float fz) => Vector3.Transform(gm.Min + (gr * new Vector3(fx, 0, fz)), st.Lage);
                var ecken = new[] { L(0, 0), L(1, 0), L(0, 1), L(1, 1) };
                var rechteck = (ecken.Min(e => e.X), ecken.Min(e => e.Z), ecken.Max(e => e.X), ecken.Max(e => e.Z));
                return (Rechteck: rechteck, Kopfteil: L(0.5f, 0), Fussende: L(0.5f, 1), Breite: Vector3.Distance(L(0, 0.5f), L(1, 0.5f)));
            }).FirstOrDefault(st => MathF.Abs(st.Rechteck.Item1 - s.Rechteck.X1) < 1e-3f && MathF.Abs(st.Rechteck.Item2 - s.Rechteck.Z1) < 1e-3f
                && MathF.Abs(st.Rechteck.Item3 - s.Rechteck.X2) < 1e-3f && MathF.Abs(st.Rechteck.Item4 - s.Rechteck.Z2) < 1e-3f);
            if (stueck.Kopfteil == default && stueck.Fussende == default)
            {
                funde.Add($"{wo} {s.Modell}: kein Modell zum Platz");
                continue;
            }
            var achse = new Vector2(stueck.Fussende.X - stueck.Kopfteil.X, stueck.Fussende.Z - stueck.Kopfteil.Z);
            var laenge = achse.Length();
            achse /= laenge;
            // die Figur liegt mit den Fuessen an Ort, der Kopf liegt gegen den Blick
            var blick = new Vector2(MathF.Sin(s.Blick), MathF.Cos(s.Blick));
            var kopf = new Vector2(s.Ort.X, s.Ort.Z) - (blick * Innenraum.FigurHoehe);
            var kopfAnteil = Vector2.Dot(kopf - new Vector2(stueck.Kopfteil.X, stueck.Kopfteil.Z), achse) / laenge;
            if (Vector2.Dot(achse, blick) < 0.999f)
            {
                funde.Add($"{wo} {s.Modell}: Figur nicht laengs (Winkel {MathF.Acos(Math.Clamp(Vector2.Dot(achse, blick), -1, 1)) * 180 / MathF.PI:0} Grad zur Laengsachse)");
            }
            else if (kopfAnteil is < 0 or > 0.5f)
            {
                funde.Add($"{wo} {s.Modell}: Kopf nicht am Kopfteil (Anteil {kopfAnteil:0.00} der Laenge)");
            }
            if (laenge < stueck.Breite)
            {
                funde.Add($"{wo} {s.Modell}: Bett breiter ({stueck.Breite:0.00}) als lang ({laenge:0.00}), Kopfteil an der langen Seite");
            }
        }
        return funde;
    }

    private static (int Plaetze, List<string> Funde) AlleInnenraeume()
    {
        var funde = new List<string>();
        var plaetze = 0;
        foreach (var (name, innenraeume) in SpielWandTests.Fassungen())
        {
            foreach (var innen in SpielWandTests.AlleRaeume(innenraeume).Where(i => i.Fest is null))
            {
                plaetze += innen.Sitzgelegenheiten.Count(s => s.Art == SitzArt.Liegen);
                funde.AddRange(Funde(innen, $"{name} {innen.Ansicht}"));
            }
        }
        return (plaetze, funde.Distinct().ToList());
    }

    [Fact]
    public void JedesBettAllerInnenraeume_FigurLiegtLaengsKopfAmKopfteil()
    {
        var (plaetze, funde) = AlleInnenraeume();
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        Assert.True(plaetze >= 6, "Liegeplaetze: " + plaetze);
    }

    [Fact]
    public void JedesBettDerWohnung_AnJederWandUndDrehung_FigurLiegtLaengs()
    {
        var inhalt = SpielInhaltTests.Inhalt;
        var grafik = SpielGrafikTests.Bibliothek.Value;
        var funde = new List<string>();
        var geprueft = 0;
        var gesehen = new HashSet<string>();
        var alle = new HashSet<string>();
        var mitPlatz = new HashSet<string>();
        foreach (var bleibe in SpielDaten.Liste(SpielDaten.Objekt(inhalt["wohnungen"])["wohnungen"]).Select(SpielDaten.Objekt))
        {
            var stand = new Spielablauf(inhalt).Stand;
            stand.Wohnung = bleibe["id"];
            var gebaeude = SpielDaten.Objekt(bleibe["gebaeude"]);
            foreach (var moebel in Wohnung.Laden(inhalt).Where(m => Py.Str(m["typ"]) is "bett" or "matratze"))
            {
                stand.Moebel.Clear();
                stand.Moebel["probe"] = moebel["id"];
                alle.Add(Py.Str(moebel["id"]));
                foreach (var raum in SpielDaten.Liste(gebaeude["raeume"]).Select(SpielDaten.Objekt))
                {
                    double rx = Py.F(raum["x"]), ry = Py.F(raum["y"]), rw = Py.F(raum["w"]), rh = Py.F(raum["h"]);
                    for (var dreh = 0L; dreh < 4; dreh++)
                    {
                        foreach (var (cx, cy) in new[] { (rx + (rw / 2), ry), (rx + (rw / 2), ry + rh), (rx, ry + (rh / 2)), (rx + rw, ry + (rh / 2)), (rx + (rw / 2), ry + (rh / 2)) })
                        {
                            var (x, y) = Wohnung.Andocken(stand, moebel["id"], cx, cy, dreh);
                            var leer = new PyObjekt { { "moebel", new PyObjekt() }, { "boeden", new PyObjekt() } };
                            if (Wohnung.PlatzProblem(stand, "probe", moebel["id"], x, y, dreh, leer) != ""
                                || !gesehen.Add($"{Py.Str(bleibe["id"])}|{Py.Str(moebel["id"])}|{Py.Str(x)}|{Py.Str(y)}|{dreh}"))
                            {
                                continue;
                            }
                            stand.Einrichtungen[stand.Wohnung] = new PyObjekt
                            {
                                { "moebel", new PyObjekt { { "probe", new List<object?> { x, y, dreh } } } }, { "boeden", new PyObjekt() },
                            };
                            var innen = Innenraum.Bauen("zuhause", stand, SpielInnenraumTests.Innenraeume.Value, grafik)!;
                            var wo = $"{Py.Str(bleibe["id"])} {Py.Str(raum["id"])} {Py.Str(moebel["id"])} ({Py.Str(x)}, {Py.Str(y)}, dreh {dreh})";
                            var eigene = innen.Sitzgelegenheiten.Count(s => s.Art == SitzArt.Liegen && s.Deko is { } d && Py.Gleich(SpielDaten.Get(d, "moebel"), moebel["id"]));
                            if (eigene > 0)
                            {
                                mitPlatz.Add(Py.Str(moebel["id"]));
                            }
                            geprueft += eigene;
                            funde.AddRange(Funde(innen, wo));
                        }
                    }
                }
            }
        }
        Assert.True(funde.Count == 0, funde.Count + " Funde:\n" + string.Join("\n", funde.Take(30)));
        Assert.True(geprueft >= 40, "geprueft: " + geprueft);
        Assert.Equal(alle.Order(), mitPlatz.Order());
    }

    /// <summary>Gegenprobe: mit dem Stand bis A 0.89.0 findet dieselbe Pruefung die quer liegende Figur (Moebelhaus).</summary>
    [Fact]
    public void Gegenprobe_StandBisA0890_FindetQuer()
    {
        Innenraum.BettQuerAltFuerTests.Value = true;
        try
        {
            var (_, funde) = AlleInnenraeume();
            Assert.Contains(funde, f => f.Contains("moebelhaus") && f.Contains("nicht laengs"));
            Assert.Contains(funde, f => f.Contains("moebelhaus") && f.Contains("breiter"));
        }
        finally
        {
            Innenraum.BettQuerAltFuerTests.Value = false;
        }
    }
}
