using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.81.0 (Weisung P12-272, Sammelliste 349, Nico: „hinsetzen“, „ins Bett legen“): fuer jeden Platz zum Sitzen und
/// Liegen in jedem Innenraum (beide Fassungen, drei Spielstaende), fuer jedes Sitz- und Liegemoebel der Wohnung an jeder Wand
/// und fuer die Bank jeder Haltestelle: die Spielfigur (sitzend bzw. liegend, auch mit Muetze und Spitzhut) und jedes Moebel,
/// jede Person und das Haeuschen durchdringen sich nicht (Grenze 0 Ecken, Strahltest wie <see cref="SpielSitzenTests"/>), auch
/// 0,02 vor, hinter und neben der Stelle; kein Teil der Figur reicht ueber die Mitte einer Wand (Regel 347).
/// </summary>
public class SpielHaltungTests
{
    private const float Schub = 0.02f;

    internal static readonly IReadOnlyList<IReadOnlyList<Figur.Stueck>?> Kleider =
        [null, [new Figur.Stueck("kopf", "muetze", "#DC2626")], [new Figur.Stueck("kopf", "spitzhut", "#111827")]];

    private static readonly Dictionary<(string, int), List<SpielSitzenTests.Dreieck>> FigurVorrat = [];

    /// <summary>
    /// Die Figur (Hoehe 1) sitzend (Sitzbild wie gezeichnet, <see cref="Figur.SitzBild(SitzArt, string?)"/>, auf Sofa, Sessel
    /// und Liegestuhl seit A 1.3.1 mit den Beinen auf dem Polster) oder liegend, je Kleidung.
    /// </summary>
    internal static List<SpielSitzenTests.Dreieck> FigurDreiecke(SitzArt art, int kleid, string? modell = null)
    {
        lock (FigurVorrat)
        {
            var key = (art == SitzArt.Liegen ? "liegen" : Figur.SitzBild(art, modell), kleid);
            if (!FigurVorrat.TryGetValue(key, out var d))
            {
                var g = SpielGrafikTests.Bibliothek.Value;
                var netz = art == SitzArt.Liegen ? Figur.Netz(g, null, 1f, Kleider[kleid]) : Figur.Netz(g, null, 1f, Kleider[kleid], Figur.SitzBild(art, modell), 0);
                d = SpielSitzenTests.Dreiecke(netz, art == SitzArt.Liegen ? Matrix4x4.CreateRotationX(-MathF.PI / 2) : Matrix4x4.Identity);
                FigurVorrat[key] = d;
            }
            return d;
        }
    }

    /// <summary>Lage der Figur (Hoehe <paramref name="hoehe"/>) auf dem Platz, verschoben um <paramref name="schub"/> (laengs, quer).</summary>
    internal static Matrix4x4 Lage(Vector3 ort, float blick, float hoehe, Vector2 schub)
    {
        var vor = new Vector3(MathF.Sin(blick), 0, MathF.Cos(blick));
        var quer = new Vector3(vor.Z, 0, -vor.X);
        return Matrix4x4.CreateScale(hoehe) * Matrix4x4.CreateRotationY(blick) * Matrix4x4.CreateTranslation(ort + (vor * schub.X) + (quer * schub.Y));
    }

    private static readonly Vector2[] Schuebe = [Vector2.Zero, new(Schub, 0), new(-Schub, 0), new(0, Schub), new(0, -Schub)];

    /// <summary>Ecken von a in b und von b in a (Huellen-Vorpruefung); 0 = frei.</summary>
    internal static int Durchdringung(List<SpielSitzenTests.Dreieck> a, List<SpielSitzenTests.Dreieck> b)
    {
        var (amin, amax) = SpielSitzenTests.Huelle(a);
        var (bmin, bmax) = SpielSitzenTests.Huelle(b);
        var u = Vector3.Max(amin, bmin);
        var o = Vector3.Min(amax, bmax);
        if (u.X > o.X || u.Y > o.Y || u.Z > o.Z)
        {
            return 0;
        }
        bool Drin(Vector3 e) => e.X >= u.X && e.Y >= u.Y && e.Z >= u.Z && e.X <= o.X && e.Y <= o.Y && e.Z <= o.Z;
        // Sammelliste 453: jedes Netz einmal vorbereitet, und nur, wenn eine Ecke in der Ueberschneidung liegt (gleiches Ergebnis)
        var inA = a.SelectMany(d => new[] { d.A, d.B, d.C }).Where(Drin).Distinct().ToList();
        var inB = b.SelectMany(d => new[] { d.A, d.B, d.C }).Where(Drin).Distinct().ToList();
        var vorratB = inA.Count > 0 ? new SpielSitzenTests.Netzvorrat(b) : null;
        var vorratA = inB.Count > 0 ? new SpielSitzenTests.Netzvorrat(a) : null;
        return inA.Count(e => SpielSitzenTests.Innen(e, vorratB!)) + inB.Count(e => SpielSitzenTests.Innen(e, vorratA!));
    }

    private static List<SpielSitzenTests.Dreieck> Kasten(Teil t)
    {
        Vector3 lo = new(MathF.Min(t.X1, t.X2), MathF.Min(t.Y1, t.Y2), MathF.Min(t.Z1, t.Z2)), hi = new(MathF.Max(t.X1, t.X2), MathF.Max(t.Y1, t.Y2), MathF.Max(t.Z1, t.Z2));
        Vector3 P(int i) => new((i & 1) == 0 ? lo.X : hi.X, (i & 2) == 0 ? lo.Y : hi.Y, (i & 4) == 0 ? lo.Z : hi.Z);
        int[][] seiten = [[0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3]];
        return seiten.SelectMany(s => new[] { new SpielSitzenTests.Dreieck(P(s[0]), P(s[1]), P(s[2])), new SpielSitzenTests.Dreieck(P(s[0]), P(s[2]), P(s[3])) }).ToList();
    }

    /// <summary>Alle Hindernisse eines Raums: Modelle, eigene Quader der Einrichtung, Fest-Deko und Personen (sitzend oder stehend).</summary>
    internal static List<(string Name, List<SpielSitzenTests.Dreieck> D)> Hindernisse(Innenraum innen)
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        var liste = new List<(string, List<SpielSitzenTests.Dreieck>)>();
        foreach (var s in innen.Stuecke)
        {
            if (g.Modell(s.Name) is { } gm)
            {
                liste.Add((s.Name, SpielSitzenTests.Dreiecke(gm, s.Lage)));
            }
        }
        foreach (var u in innen.DekoUmrisse)
        {
            liste.AddRange(u.Kaesten.Where(k => k.Art == TeilArt.Quader).Select(k => (Py.Str(u.Deko["typ"]) + " (Quader)", Kasten(k))));
        }
        liste.AddRange(innen.FestKaesten.Where(k => k.Art == TeilArt.Quader).Select(k => ("Fest-Deko", Kasten(k))));
        foreach (var p in innen.Personen)
        {
            var sitz = innen.Sitz(p);
            var netz = Figur.Netz(g, p.Aussehen, 1f, null, sitz is null ? null : Figur.Sitzen, 0);
            var lage = Matrix4x4.CreateScale(Innenraum.FigurHoehe) * Matrix4x4.CreateRotationY(sitz?.Blick ?? 0) * Matrix4x4.CreateTranslation(sitz?.X ?? p.X, 0, sitz?.Z ?? p.Z);
            if (sitz is null)
            {
                // stehende Person: jede Blickrichtung (Zylinder um die Mitte)
                liste.Add(("Person " + p.Id, Kasten(new Teil(TeilArt.Quader, p.X - 0.3f, 0, p.Z - 0.3f, p.X + 0.3f, Innenraum.FigurHoehe, p.Z + 0.3f, Vector3.Zero))));
            }
            else
            {
                liste.Add(("Person " + p.Id, SpielSitzenTests.Dreiecke(netz, lage)));
            }
        }
        return liste;
    }

    /// <summary>Funde fuer einen Platz: Durchdringung mit jedem Hindernis und Teile der Figur ueber der Mitte einer Wand.</summary>
    private static List<string> Funde(Innenraum innen, Sitzgelegenheit s, List<(string Name, List<SpielSitzenTests.Dreieck> D)> hindernisse)
    {
        // Sammelliste 453: gleiche Faelle, gleiche Pruefung, nur schneller: Lage und Wandhaelften einmal statt je Ecke, Huellen-Vorpruefung
        // (ohne Ueberschneidung der Huellen meldet Durchdringung 0 und KanteDurch nichts, Muster wie in SpielHaltungenTests), die Faelle
        // (Kleid x Schub) nebeneinander; die Funde stehen danach in derselben Reihenfolge wie nacheinander gerechnet
        var waende = SpielWandTests.WandQuader(innen).Select(w => (W: w, H: SpielWandTests.Halbe(w, s.Mitte))).ToList();
        var huellen = hindernisse.Select(h => SpielSitzenTests.Huelle(h.D)).ToList();
        // liegend ohne Kopfbedeckung (die Figur legt sie im Bett ab, StadtFlaeche zeichnet sie dann nicht)
        var faelle = (from kleid in Enumerable.Range(0, s.Art == SitzArt.Liegen ? 1 : Kleider.Count) from schub in Schuebe select (Kleid: kleid, Schub: schub)).ToList();
        var vorlagen = faelle.Select(f => f.Kleid).Distinct().ToDictionary(k => k, k => FigurDreiecke(s.Art, k, s.Modell));
        var je = new List<string>[faelle.Count];
        Nebeneinander.Fuer(faelle.Count, i =>
        {
            var (kleid, schub) = faelle[i];
            var funde = new List<string>();
            var lage = Lage(s.Ort, s.Blick, Innenraum.FigurHoehe, schub);
            var figur = vorlagen[kleid].Select(d => new SpielSitzenTests.Dreieck(Vector3.Transform(d.A, lage), Vector3.Transform(d.B, lage), Vector3.Transform(d.C, lage))).ToList();
            var (fmin, fmax) = SpielSitzenTests.Huelle(figur);
            for (var j = 0; j < hindernisse.Count; j++)
            {
                var (name, d) = hindernisse[j];
                var (dmin, dmax) = huellen[j];
                if (fmin.X > dmax.X || fmin.Y > dmax.Y || fmin.Z > dmax.Z || dmin.X > fmax.X || dmin.Y > fmax.Y || dmin.Z > fmax.Z)
                {
                    continue;
                }
                if (Durchdringung(figur, d) is > 0 and var n)
                {
                    funde.Add($"{s.Modell} {s.Art} mit {name} | ({s.Ort.X:0.00}, {s.Ort.Z:0.00}) Kleid {kleid} Schub {schub}: {n} Ecken");
                }
            }
            foreach (var (w, (lo, hi)) in waende)
            {
                if (!(fmin.X < hi.X && fmax.X > lo.X && fmin.Y < hi.Y && fmax.Y > lo.Y && fmin.Z < hi.Z && fmax.Z > lo.Z))
                {
                    continue;
                }
                if (figur.Any(d => SpielWandTests.KanteDurch(d.A, d.B, lo, hi) || SpielWandTests.KanteDurch(d.B, d.C, lo, hi) || SpielWandTests.KanteDurch(d.C, d.A, lo, hi)))
                {
                    funde.Add($"{s.Modell} {s.Art} durch die Wand | ({s.Ort.X:0.00}, {s.Ort.Z:0.00}) Kleid {kleid} Schub {schub}: Wand ({w.Q.X1:0.00}..{w.Q.X2:0.00}, {w.Q.Z1:0.00}..{w.Q.Z2:0.00})");
                    break;
                }
            }
            je[i] = funde;
        });
        return [.. je.SelectMany(f => f)];
    }

    /// <summary>Funde gruppiert (Modell, Art, Hindernis): Zahl und das erste Beispiel.</summary>
    private static string Liste(List<string> funde) =>
        funde.Count + " Funde:\n" + string.Join("\n", funde.GroupBy(f => System.Text.RegularExpressions.Regex.Replace(f.Split(" | ")[0], @"^\S+ \S+ [^:]*: ", ""))
            .Select(gr => $"{gr.Count()} x {gr.Key}, z. B. {gr.First()}"));

    [Fact]
    public void JederPlatzAllerInnenraeume_FigurUndMoebelDurchdringenSichNicht()
    {
        var funde = new List<string>();
        var gesehen = new HashSet<string>();
        var plaetze = 0;
        var arten = new HashSet<string>();
        foreach (var (name, innenraeume) in SpielWandTests.Fassungen())
        {
            foreach (var innen in SpielWandTests.AlleRaeume(innenraeume).Where(i => i.Fest is null))
            {
                List<(string, List<SpielSitzenTests.Dreieck>)>? hindernisse = null;
                foreach (var s in innen.Sitzgelegenheiten.Where(innen.Frei))
                {
                    var schluessel = string.Join("|", innen.Ansicht, s.Modell, s.Ort.X, s.Ort.Y, s.Ort.Z, s.Blick, string.Join(",", innen.Personen.Select(p => p.Id + p.X + p.Z)));
                    if (!gesehen.Add(schluessel))
                    {
                        continue;
                    }
                    hindernisse ??= Hindernisse(innen);
                    plaetze++;
                    arten.Add(s.Modell);
                    funde.AddRange(Funde(innen, s, hindernisse).Select(f => $"{name} {innen.Ansicht}: {f}"));
                }
            }
        }
        Assert.True(funde.Count == 0, Liste(funde));
        Assert.True(plaetze >= 60, "geprueft: " + plaetze);
        // alle Sitzmodelle, die heute in einem Raum stehen (Wartesessel stehen nur im Katalog des Werkzeugs, siehe innenraeume.json „ohne“)
        Assert.True(arten.IsSupersetOf(["chairDesk", "bench", "loungeSofa", "loungeChair", "loungeSofaOttoman", "loungeChairRelax", "kkChairA", "bedSingle", "bedDouble"]), string.Join(", ", arten.Order()));
    }

    [Fact]
    public void JedesSitzUndLiegemoebelDerWohnung_AnJederWand_FreiUndNieDurchDieWand()
    {
        var inhalt = SpielInhaltTests.Inhalt;
        var grafik = SpielGrafikTests.Bibliothek.Value;
        var funde = new List<string>();
        var geprueft = 0;
        var gesehen = new HashSet<string>();
        var mitPlatz = new HashSet<string>();
        var alle = new HashSet<string>();
        foreach (var bleibe in SpielDaten.Liste(SpielDaten.Objekt(inhalt["wohnungen"])["wohnungen"]).Select(SpielDaten.Objekt))
        {
            var stand = new Spielablauf(inhalt).Stand;
            stand.Wohnung = bleibe["id"];
            var gebaeude = SpielDaten.Objekt(bleibe["gebaeude"]);
            foreach (var moebel in Wohnung.Laden(inhalt).Where(m => Py.Str(m["typ"]) is "sofa" or "sessel" or "sitzsack" or "esstisch" or "liegestuhl" or "schreibtisch" or "bett" or "matratze"))
            {
                stand.Moebel.Clear();
                stand.Moebel["probe"] = moebel["id"];
                alle.Add(Py.Str(moebel["id"]));
                foreach (var raum in SpielDaten.Liste(gebaeude["raeume"]).Select(SpielDaten.Objekt))
                {
                    double rx = Py.F(raum["x"]), ry = Py.F(raum["y"]), rw = Py.F(raum["w"]), rh = Py.F(raum["h"]);
                    for (var dreh = 0L; dreh < 4; dreh++)
                    {
                        foreach (var (cx, cy) in new[] { (rx + (rw / 2), ry), (rx + (rw / 2), ry + rh), (rx, ry + (rh / 2)), (rx + rw, ry + (rh / 2)), (rx + 0.01, ry + 0.01), (rx + rw - 0.01, ry + rh - 0.01) })
                        {
                            var (x, y) = Wohnung.Andocken(stand, moebel["id"], cx, cy, dreh);
                            var leer = new PyObjekt { { "moebel", new PyObjekt() }, { "boeden", new PyObjekt() } };
                            if (Wohnung.PlatzProblem(stand, "probe", moebel["id"], x, y, dreh, leer) != "" || !gesehen.Add($"{Py.Str(bleibe["id"])}|{Py.Str(moebel["id"])}|{Py.Str(x)}|{Py.Str(y)}|{dreh}"))
                            {
                                continue;
                            }
                            stand.Einrichtungen[stand.Wohnung] = new PyObjekt
                            {
                                { "moebel", new PyObjekt { { "probe", new List<object?> { x, y, dreh } } } }, { "boeden", new PyObjekt() },
                            };
                            var innen = Innenraum.Bauen("zuhause", stand, SpielInnenraumTests.Innenraeume.Value, grafik)!;
                            var eigene = innen.Sitzgelegenheiten.Where(s => s.Deko is { } d && Py.Gleich(SpielDaten.Get(d, "moebel"), moebel["id"])).ToList();
                            // zeigt die Vorderseite zur Wand, gibt es dort keinen Platz (die Figur ragte sonst durch die Wand)
                            if (eigene.Count > 0)
                            {
                                mitPlatz.Add(Py.Str(moebel["id"]));
                            }
                            var hindernisse = Hindernisse(innen);
                            foreach (var s in eigene)
                            {
                                geprueft++;
                                funde.AddRange(Funde(innen, s, hindernisse).Select(f => $"{Py.Str(bleibe["id"])} {Py.Str(raum["id"])} {Py.Str(moebel["id"])} ({Py.Str(x)}, {Py.Str(y)}, dreh {dreh}): {f}"));
                            }
                        }
                    }
                }
            }
        }
        Assert.True(funde.Count == 0, Liste(funde));
        Assert.True(geprueft >= 100, "geprueft: " + geprueft);
        // jedes Sitz- und Liegemoebel bietet irgendwo einen Platz (Esstisch: seine Stuehle sind keine Plaetze, siehe SitzModelle)
        Assert.Equal(alle.Where(m => m != "esstisch").Order(), mitPlatz.Order());
    }

    [Fact]
    public void BankDerHaltestelle_FigurHaeuschenUndWartendeFrei()
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        var h = new Haltestelle(1, new Vector2(3, 4), Vector2.Normalize(new Vector2(1, 0.3f)), Vector2.Normalize(new Vector2(-0.3f, 1)), 0.3f);
        var lage = Haltestellen.Lage(h.Haeuschen, h.Fahrt, h.Aussen);
        var haeuschen = SpielSitzenTests.Dreiecke(Haltestellen.HaeuschenNetz, lage);
        var wartend = SpielSitzenTests.Dreiecke(Figur.Netz(g, null, 1f), Lage(new Vector3(h.Wartend.X, 0, h.Wartend.Y), MathF.Atan2(-h.Aussen.X, -h.Aussen.Y), Figur.StadtHoehe, Vector2.Zero));
        var funde = new List<string>();
        for (var kleid = 0; kleid < Kleider.Count; kleid++)
        {
            // Sammelliste 351: die Stelle haengt vom Kopf ab (bis an die Glasrueckwand)
            var (ort, blick) = Haltestellen.BankSitz(h, Figur.KopfHinten(g, null, Kleider[kleid]));
            foreach (var schub in Schuebe.Select(s => s * (Figur.StadtHoehe / Innenraum.FigurHoehe)))
            {
                var figur = FigurDreiecke(SitzArt.Sitzen, kleid).Select(d => new SpielSitzenTests.Dreieck(
                    Vector3.Transform(d.A, Lage(ort, blick, Figur.StadtHoehe, schub)), Vector3.Transform(d.B, Lage(ort, blick, Figur.StadtHoehe, schub)),
                    Vector3.Transform(d.C, Lage(ort, blick, Figur.StadtHoehe, schub)))).ToList();
                foreach (var (name, d) in new[] { ("Haeuschen", haeuschen), ("Wartende Person", wartend) })
                {
                    if (Durchdringung(figur, d) is > 0 and var n)
                    {
                        funde.Add($"Kleid {kleid} Schub {schub}: {n} Ecken mit {name}");
                    }
                }
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    /// <summary>Die Quader der Figur (Innenraum.FigurQuader) umfassen jede Ecke der sitzenden (mit jeder Kleidung) und liegenden Figur.</summary>
    [Fact]
    public void FigurQuader_UmfassenDieFigur()
    {
        // Welle A 1.3.1 (Sammelliste 431): dazu die Haltungen mit den Beinen auf dem Polster (Sofa, Sessel, Liegestuhl)
        foreach (var (art, modell) in new (SitzArt, string?)[] { (SitzArt.Sitzen, null), (SitzArt.PcStuhl, null), (SitzArt.Liegen, null), (SitzArt.Sitzen, "loungeSofa"), (SitzArt.Zurueck, "loungeSofa") })
        {
            for (var kleid = 0; kleid < (art == SitzArt.Liegen ? 1 : Kleider.Count); kleid++)
            {
                var quader = Innenraum.FigurQuader(art, modell);
                var draussen = FigurDreiecke(art, kleid, modell).SelectMany(d => new[] { d.A, d.B, d.C })
                    .Where(e => !quader.Any(q => MathF.Abs(e.X) <= q.Halb && e.Y >= q.Y1 && e.Y <= q.Y2 && e.Z >= q.Z1 && e.Z <= q.Z2)).Distinct().ToList();
                Assert.True(draussen.Count == 0, $"{art} {modell} Kleid {kleid}: {draussen.Count} Ecken ausserhalb, z. B. {string.Join(" ", draussen.Take(5))}");
                // die Grundflaeche fuer die Wandregel (Regel 347) reicht nach hinten, vorn und zur Seite ueber jede Ecke (sitzend)
                if (art != SitzArt.Liegen)
                {
                    var (hinten, vorn, halb) = Innenraum.Grundflaeche(art, modell);
                    var ecken = FigurDreiecke(art, kleid, modell).SelectMany(d => new[] { d.A, d.B, d.C }).ToList();
                    Assert.True(-ecken.Min(e => e.Z) <= hinten && ecken.Max(e => e.Z) <= vorn && ecken.Max(e => MathF.Abs(e.X)) <= halb,
                        $"{art} {modell} Kleid {kleid}: Grundflaeche ({hinten}, {vorn}, {halb}) zu klein fuer z {ecken.Min(e => e.Z):0.000} bis {ecken.Max(e => e.Z):0.000}, x {ecken.Max(e => MathF.Abs(e.X)):0.000}");
                }
            }
        }
        Assert.Equal(Figur.SitzenLang, Figur.SitzBild(SitzArt.Sitzen, "loungeSofa"));
        Assert.Equal(Figur.SitzenLangZurueck, Figur.SitzBild(SitzArt.Zurueck, "loungeChairRelax"));
    }

    /// <summary>Gegenprobe: dieselbe Pruefung findet eine Figur, die mitten im Sofa sitzt (Stelle 0,3 tiefer).</summary>
    [Fact]
    public void Gegenprobe_FigurImSofa_WirdGefunden()
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        var gm = g.Modell("loungeSofa")!;
        var lage = Matrix4x4.CreateTranslation(-gm.Min) * Matrix4x4.CreateScale(3.6f / (gm.Max.X - gm.Min.X), 0.8f * Innenraum.Meter / (gm.Max.Y - gm.Min.Y), 1.4f / (gm.Max.Z - gm.Min.Z));
        var s = Innenraum.Platz("loungeSofa", gm.Min, gm.Max, lage, Innenraum.FigurHoehe, null)!;
        var sofa = SpielSitzenTests.Dreiecke(gm, lage);
        var figur = FigurDreiecke(SitzArt.Sitzen, 0, "loungeSofa");
        List<SpielSitzenTests.Dreieck> An(Vector3 ort) => figur.Select(d => new SpielSitzenTests.Dreieck(
            Vector3.Transform(d.A, Lage(ort, s.Blick, Innenraum.FigurHoehe, Vector2.Zero)), Vector3.Transform(d.B, Lage(ort, s.Blick, Innenraum.FigurHoehe, Vector2.Zero)),
            Vector3.Transform(d.C, Lage(ort, s.Blick, Innenraum.FigurHoehe, Vector2.Zero)))).ToList();
        Assert.Equal(0, Durchdringung(An(s.Ort), sofa));
        Assert.True(Durchdringung(An(s.Ort - new Vector3(0, 0.3f, 0)), sofa) > 0);
    }

    /// <summary>
    /// Sammelliste 453: Innen mit vorbereitetem Netz (Huellen-Vorpruefung je Dreieck) ergibt genau dasselbe wie ohne, fuer jede Ecke
    /// und ein dichtes Gitter um Figur (alle Haltungen) und Moebel; Gegenprobe: es gibt Punkte innen und aussen.
    /// </summary>
    [Fact]
    public void Innen_MitVorrat_GleichOhne()
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        var netze = new List<List<SpielSitzenTests.Dreieck>>
        {
            FigurDreiecke(SitzArt.Liegen, 0), FigurDreiecke(SitzArt.Sitzen, 2, "loungeSofa"), FigurDreiecke(SitzArt.Sitzen, 1),
        };
        foreach (var name in new[] { "loungeSofa", "bedDouble", "chairDesk" })
        {
            netze.Add(SpielSitzenTests.Dreiecke(g.Modell(name)!, Matrix4x4.Identity));
        }
        int innen = 0, aussen = 0;
        foreach (var netz in netze)
        {
            var vorrat = new SpielSitzenTests.Netzvorrat(netz);
            var (min, max) = SpielSitzenTests.Huelle(netz);
            var punkte = netz.SelectMany(d => new[] { d.A, d.B, d.C, (d.A + d.B + d.C) / 3 }).ToList();
            for (var i = 0; i <= 16; i++)
            {
                for (var j = 0; j <= 16; j++)
                {
                    for (var k = 0; k <= 16; k++)
                    {
                        punkte.Add(min - new Vector3(0.05f) + ((max - min + new Vector3(0.1f)) * new Vector3(i, j, k) / 16f));
                    }
                }
            }
            foreach (var p in punkte)
            {
                var ohne = SpielSitzenTests.Innen(p, netz);
                Assert.Equal(ohne, SpielSitzenTests.Innen(p, vorrat));
                innen += ohne ? 1 : 0;
                aussen += ohne ? 0 : 1;
            }
        }
        Assert.True(innen > 1000 && aussen > 1000, $"innen {innen}, aussen {aussen}");
    }
}
