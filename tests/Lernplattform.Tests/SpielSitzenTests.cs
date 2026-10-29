using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 9 W1 (Weisung P9-3, Nicos Spielwuensche): Personen am Schreibtisch sitzen auf dem Stuhl (Stuhl, Tisch und
/// sitzende Figur durchdringen sich nicht), Baenke und Schreibtische stehen an der Wand, Tueren und Wege bleiben frei.
/// </summary>
public class SpielSitzenTests
{
    private static PyObjekt Innenraeume => SpielInnenraumTests.Innenraeume.Value;

    /// <summary>Alle Innenraeume mit Personen: neuer Spielstand und gespielter Stand (Firma mit Filiale und Mitarbeitern).</summary>
    internal static IEnumerable<(string Stand, Innenraum Innen)> AlleRaeume()
    {
        var neu = new Spielablauf(SpielInhaltTests.Inhalt).Stand;
        var gespielt = SpielFingerabdruckTests.Gespielt.Value.Stand;
        var spaet = new Spielablauf(SpielInhaltTests.Inhalt).Stand;
        spaet.TageFertig = 200;
        var ansichten = new List<string> { "buero", "filiale", "serverfarm", "schalter:bank", "schalter:amt", "geschaeft:moebelhaus", "geschaeft:elektromarkt", "geschaeft:kaufhaus" };
        ansichten.AddRange(SpielDaten.Kundenorte(SpielInhaltTests.Inhalt).Select(k => "kunde:" + Py.Str(k["id"])));
        foreach (var (name, stand) in new[] { ("neu", neu), ("tag200", spaet), ("gespielt", gespielt) })
        {
            foreach (var ansicht in ansichten)
            {
                if (Innenraum.Bauen(ansicht, stand, Innenraeume, SpielGrafikTests.Bibliothek.Value) is { } innen)
                {
                    yield return (name, innen);
                }
            }
        }
    }

    /// <summary>Wer sitzt (Platz bis 0,45 von einer Stuhlmitte), feste Tabelle; wer steht, steht mindestens 1,0 von jeder Stuhlmitte weg (nie "im" Stuhl).</summary>
    [Fact]
    public void SitzendePersonenWieTabelle()
    {
        var sitzen = new HashSet<string>();
        foreach (var (stand, innen) in AlleRaeume())
        {
            foreach (var p in innen.Personen)
            {
                var abstand = innen.Stuehle.Count == 0 ? float.MaxValue : innen.Stuehle.Min(st => Vector2.Distance(st.Stuhl, new Vector2(p.X, p.Z)));
                if (innen.Sitz(p) is { } sitz)
                {
                    Assert.True(abstand <= Innenraum.SitzAbstand, p.Id);
                    sitzen.Add(stand + " " + innen.Ansicht + " " + p.Id);
                }
                else
                {
                    Assert.True(abstand >= 1.0f, stand + " " + innen.Ansicht + " " + p.Id + " steht am Stuhl: " + abstand);
                }
            }
        }
        string[] soll =
        [
            "neu buero sabine", "neu buero lea", "neu buero frank", "neu buero jonas", "neu buero ulla", "neu buero rainer", "neu serverfarm arendt",
            "tag200 buero sabine", "tag200 buero lea", "tag200 buero jonas", "tag200 buero ulla", "tag200 buero rainer", "tag200 buero tim",
            "tag200 buero lina", "tag200 buero paul", "tag200 serverfarm arendt", "tag200 kunde:betriebswerkstatt marta", "tag200 kunde:lindenau wendt",
            "gespielt buero bw0-0", "gespielt buero bw0-1", "gespielt buero bw0-2", "gespielt buero bw1-0", "gespielt buero bw1-1", "gespielt buero bw1-2",
            "gespielt serverfarm arendt", "gespielt kunde:talheim petra", "gespielt kunde:betriebswerkstatt marta", "gespielt kunde:lindenau wendt",
        ];
        Assert.Equal(soll.Order(StringComparer.Ordinal), sitzen.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Wunsch 4: kein Stuhl ragt durch die sitzende Figur. Fuer jede sitzende Person: keine Ecke der Figur liegt in Stuhl,
    /// Tisch oder Bildschirm in ihrer Naehe und keine Ecke dieser Moebel in der Figur (Grenze 0 Ecken; "in" = Strahltest in
    /// drei Richtungen, mindestens zwei ungerade). Dieselbe Pruefung haelt auch 0,02 vor und hinter der Stelle (Spielraum).
    /// </summary>
    [Fact]
    public void StuhlTischUndFigurDurchdringenSichNicht()
    {
        var grafik = SpielGrafikTests.Bibliothek.Value;
        var geprueft = 0;
        var gesehen = new HashSet<string>();
        foreach (var (_, innen) in AlleRaeume())
        {
            foreach (var p in innen.Personen)
            {
                if (innen.Sitz(p) is not { } sitz || !gesehen.Add(innen.Ansicht + "|" + p.Id + "|" + sitz.X + "|" + sitz.Z))
                {
                    continue;
                }
                var netz = Figur.Netz(grafik, p.Aussehen, 1f, null, Figur.Sitzen, 0);
                var richtung = new Vector3(MathF.Sin(sitz.Blick), 0, MathF.Cos(sitz.Blick));
                foreach (var schub in new[] { 0f, -0.02f, 0.02f })
                {
                    var ort = new Vector3(sitz.X, 0, sitz.Z) + (richtung * schub);
                    var figur = Dreiecke(netz, Matrix4x4.CreateScale(Innenraum.FigurHoehe) * Matrix4x4.CreateRotationY(sitz.Blick) * Matrix4x4.CreateTranslation(ort));
                    var (min, max) = Huelle(figur);
                    foreach (var stueck in innen.Stuecke.Where(st => st.Name is "chairDesk" or "desk" or "computerScreen"))
                    {
                        var moebel = Dreiecke(grafik.Modell(stueck.Name)!, stueck.Lage);
                        var (mmin, mmax) = Huelle(moebel);
                        if (Vector3.Max(min, mmin) is var u && Vector3.Min(max, mmax) is var o && (u.X > o.X || u.Y > o.Y || u.Z > o.Z))
                        {
                            continue;
                        }
                        var figurInMoebel = figur.SelectMany(d => new[] { d.A, d.B, d.C }).Count(e => Innen(e, moebel));
                        var moebelInFigur = moebel.SelectMany(d => new[] { d.A, d.B, d.C }).Count(e => Innen(e, figur));
                        Assert.True(figurInMoebel == 0 && moebelInFigur == 0,
                            $"{innen.Ansicht} {p.Id} ({schub}): {figurInMoebel} Ecken der Figur in {stueck.Name}, {moebelInFigur} Ecken von {stueck.Name} in der Figur");
                        geprueft++;
                    }
                }
            }
        }
        // je sitzende Person drei Stellen, je Stuhl, Tisch und Bildschirm in der Naehe (Stand A 0.23.1: 115 Pruefungen)
        Assert.True(geprueft >= 100, "geprueft: " + geprueft);
    }

    /// <summary>
    /// Wunsch 1 bis 3: Baenke und Schreibtische stehen an der Wand, nicht vor einer Tuer (Tuerbreite plus 0,3 je Seite, 1,2 tief
    /// in den Raum, am Eingang ebenso; alle anderen Stuecke nicht in der Oeffnung selbst) und auf keinem anderen Moebel; jede Tuer und jede Person ist vom Eingang
    /// erreichbar; in der Verwaltung der Bitweiche ist zwischen den Schreibtischen ein Gang frei.
    /// </summary>
    [Theory]
    [InlineData("schalter:bank", "schalterhalle")]
    [InlineData("schalter:amt", "buergerbuero")]
    [InlineData("kunde:talheim", "bahnsteig")]
    [InlineData("buero", "verwaltung")]
    public void MoebelAnDerWandTuerenUndWegeFrei(string ansicht, string raumId)
    {
        var neu = new Spielablauf(SpielInhaltTests.Inhalt).Stand;
        var spaet = new Spielablauf(SpielInhaltTests.Inhalt).Stand;
        spaet.TageFertig = 200;
        foreach (var stand in new[] { neu, spaet })
        {
            var innen = Innenraum.Bauen(ansicht, stand, Innenraeume, SpielGrafikTests.Bibliothek.Value)!;
            var g = innen.Gebaeude;
            var raum = SpielDaten.Liste(g["raeume"]).Select(SpielDaten.Objekt).Single(r => Py.Gleich(r["id"], raumId));
            var zonen = Tuerzonen(g, 0.3).ToList();
            var oeffnungen = Tuerzonen(g, 0).ToList();
            var deko = SpielDaten.ListeOderLeer(raum, "deko").Select(SpielDaten.Objekt)
                .Where(d => !Wege.Begehbar.Contains(Py.Str(d["typ"])) && Py.Str(d["typ"]) is not ("platznummer" or "anzeigetafel" or "gemaelde" or "wappen")).ToList();
            foreach (var d in deko)
            {
                var r = Rechteck(d);
                // die umgestellten Stuecke (Baenke, Schreibtische) mit Abstand 0,3 neben der Tuer, alles andere nicht in der Oeffnung
                var umgestellt = Py.Str(d["typ"]) is "bank" or "schreibtisch";
                foreach (var z in umgestellt ? zonen : oeffnungen)
                {
                    Assert.False(Ueberlappt(r, z), $"{ansicht}: {Py.Str(d["typ"])} bei ({r.X}, {r.Y}) steht vor einer Tuer");
                }
                if (umgestellt)
                {
                    foreach (var e in deko.Where(e => !ReferenceEquals(e, d)))
                    {
                        Assert.False(Ueberlappt(r, Rechteck(e)), $"{ansicht}: {Py.Str(d["typ"])} und {Py.Str(e["typ"])} uebereinander");
                    }
                }
            }
            // jede Tuer (Mitte, 0,6 im Raum) und jede Person erreichbar
            foreach (var r in SpielDaten.Liste(g["raeume"]).Select(SpielDaten.Objekt))
            {
                var (tx, ty) = TuerInnen(r, g);
                Assert.True(innen.Weg(innen.StartX, innen.StartZ, tx, ty).Count > 0 || Wege.Zelle(tx, ty) == Wege.Zelle(innen.StartX, innen.StartZ),
                    $"{ansicht}: Tuer von {Py.Str(r["id"])} nicht erreichbar");
            }
            foreach (var p in innen.Personen)
            {
                var nah = Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(innen.StartX, innen.StartZ)) <= Wege.Reichweite;
                Assert.True(nah || innen.Weg(innen.StartX, innen.StartZ, p.X, p.Z, Wege.Reichweite).Count > 0, $"{ansicht}: {p.Id} nicht erreichbar");
            }
            var baenke = deko.Where(d => Py.Str(d["typ"]) == "bank").Select(Rechteck).ToList();
            switch (raumId)
            {
                case "schalterhalle" or "buergerbuero":
                    // genau eine Bank, mit der Rueckseite an der Wand zum Flur (hoechstens 0,15 Abstand)
                    var bank = Assert.Single(baenke);
                    var wand = Py.F(raum["y"]) + Py.F(raum["h"]);
                    Assert.InRange(wand - (bank.Y + bank.H), 0, 0.15);
                    break;
                case "bahnsteig":
                    // die zwoelf Baenke der Wartehalle, keine mehr mitten im Raum (Python-Grundriss: y 10,3)
                    Assert.Equal(12, baenke.Count);
                    Assert.All(baenke, b => Assert.True(b.Y >= 12.3, "Bank bei y " + b.Y));
                    break;
                case "verwaltung":
                    // beide Schreibtische an einer Wand (0,2 Abstand), dazwischen ein Gang: jede Zelle bei y 3,0 von der Tuerseite bis vor Karin frei
                    var tische = deko.Where(d => Py.Str(d["typ"]) == "schreibtisch").Select(Rechteck).OrderBy(t => t.Y).ToList();
                    Assert.Equal(2, tische.Count);
                    Assert.Equal(0.2, tische[0].Y, 6);
                    Assert.Equal(Py.F(raum["y"]) + Py.F(raum["h"]) - 0.2, tische[1].Y + tische[1].H, 6);
                    for (var x = 16.65; x <= 21.6; x += 0.25)
                    {
                        Assert.True(Wege.KannStehen(x, 3.0, innen.Lauf), "Gang zu bei x " + x);
                    }
                    Assert.True(innen.Weg(innen.StartX, innen.StartZ, 21.5, 3.0).Count > 0, "Gang nicht erreichbar");
                    break;
            }
        }
    }

    private readonly record struct Feld(double X, double Y, double W, double H);

    private static Feld Rechteck(PyObjekt d) => new(Py.F(d["x"]), Py.F(d["y"]), Py.F(d["w"]), Py.F(d["h"]));

    private static bool Ueberlappt(Feld a, Feld b) => a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H;

    /// <summary>Feld vor jeder Tuer (in den Raum hinein, Tuerbreite plus <paramref name="rand"/> je Seite, 1,2 tief) und am Eingang.</summary>
    private static IEnumerable<Feld> Tuerzonen(PyObjekt g, double rand)
    {
        foreach (var r in SpielDaten.Liste(g["raeume"]).Select(SpielDaten.Objekt))
        {
            var tuer = SpielDaten.Objekt(r["tuer"]);
            if (Py.Wahr(SpielDaten.Get(tuer, "offen")))
            {
                // offene Seite (Bahnsteig Talheim: die ganze Wand zur Halle), keine Tuer
                continue;
            }
            double von = Py.F(tuer["von"]) - rand, bis = Py.F(tuer["bis"]) + rand;
            double x = Py.F(r["x"]), y = Py.F(r["y"]), w = Py.F(r["w"]), h = Py.F(r["h"]);
            yield return Wege.TuerSeite(r, g) switch
            {
                "s" => new Feld(von, y + h - 1.2, bis - von, 1.2),
                "n" => new Feld(von, y, bis - von, 1.2),
                "w" => new Feld(x, von, 1.2, bis - von),
                _ => new Feld(x + w - 1.2, von, 1.2, bis - von),
            };
        }
        var eingang = SpielDaten.Objekt(SpielDaten.Objekt(g["flur"])["eingang"]);
        yield return new Feld(0, Py.F(eingang["von"]) - rand, 1.2, Py.F(eingang["bis"]) - Py.F(eingang["von"]) + (2 * rand));
    }

    private static (double X, double Y) TuerInnen(PyObjekt r, PyObjekt g)
    {
        var tuer = SpielDaten.Objekt(r["tuer"]);
        var mitte = (Py.F(tuer["von"]) + Py.F(tuer["bis"])) / 2;
        double x = Py.F(r["x"]), y = Py.F(r["y"]), w = Py.F(r["w"]), h = Py.F(r["h"]);
        return Wege.TuerSeite(r, g) switch
        {
            "s" => (mitte, y + h - 0.6),
            "n" => (mitte, y + 0.6),
            "w" => (x + 0.6, mitte),
            _ => (x + w - 0.6, mitte),
        };
    }

    private readonly record struct Dreieck(Vector3 A, Vector3 B, Vector3 C);

    private static List<Dreieck> Dreiecke(Netz netz, Matrix4x4 lage)
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

    private static List<Dreieck> Dreiecke(GrafikModell modell, Matrix4x4 lage)
    {
        var e = modell.Ecken;
        Vector3 Ecke(int i) => Vector3.Transform(new Vector3(e[i], e[i + 1], e[i + 2]), lage);
        return Enumerable.Range(0, modell.Dreiecke).Select(d => new Dreieck(Ecke(d * 9), Ecke((d * 9) + 3), Ecke((d * 9) + 6))).ToList();
    }

    private static (Vector3 Min, Vector3 Max) Huelle(List<Dreieck> dreiecke)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var d in dreiecke)
        {
            min = Vector3.Min(min, Vector3.Min(d.A, Vector3.Min(d.B, d.C)));
            max = Vector3.Max(max, Vector3.Max(d.A, Vector3.Max(d.B, d.C)));
        }
        return (min, max);
    }

    private static readonly Vector3[] Strahlen =
        [Vector3.Normalize(new(0.13f, 1, 0.07f)), Vector3.Normalize(new(0.07f, -1, 0.11f)), Vector3.Normalize(new(1, 0.05f, 0.09f))];

    /// <summary>Punkt im Netz: Strahl in drei Richtungen, ungerade Zahl der Treffer in mindestens zwei (Moeller-Trumbore).</summary>
    private static bool Innen(Vector3 punkt, List<Dreieck> netz)
    {
        var stimmen = 0;
        foreach (var r in Strahlen)
        {
            var treffer = 0;
            foreach (var d in netz)
            {
                Vector3 e1 = d.B - d.A, e2 = d.C - d.A;
                var q = Vector3.Cross(r, e2);
                var det = Vector3.Dot(e1, q);
                if (MathF.Abs(det) < 1e-9f)
                {
                    continue;
                }
                var t0 = punkt - d.A;
                var u = Vector3.Dot(t0, q) / det;
                if (u < 0 || u > 1)
                {
                    continue;
                }
                var k = Vector3.Cross(t0, e1);
                var v = Vector3.Dot(r, k) / det;
                if (v < 0 || u + v > 1)
                {
                    continue;
                }
                if (Vector3.Dot(e2, k) / det > 1e-6f)
                {
                    treffer++;
                }
            }
            stimmen += treffer % 2;
        }
        return stimmen >= 2;
    }
}
