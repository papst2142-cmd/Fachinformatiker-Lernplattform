using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Sammelliste 295 (Weisung P12-196, Nico 2a): ein Haus mit Bahnsteig (Bahnhof) rastet mit „Einrasten“ beim Setzen und
/// Schieben ans gerade Gleis: zwei Bahnsteige nebeneinander = Gleis genau mittig dazwischen, ein Bahnsteig = Kante
/// <see cref="BahnsteigKante"/> vor der Gleismitte. Der Bahnhof dreht dazu (seit Welle A 0.70.0 in Schritten von 15 Grad),
/// bis die Bahnsteige parallel zum Gleis liegen. Nur wenn ein gerades Gleisstueck neben dem Bahnsteig liegt und die Verschiebung hoechstens
/// <see cref="BahnhofFang"/> ist. Nur Werkzeug.
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>Abstand der Bahnsteigkante von der Gleismitte bei einem Bahnsteig (Fassung 0: Birkenhain 0,295, Talheim-Nord 0,40).</summary>
    public const float BahnsteigKante = 0.30f;

    /// <summary>So weit (quer zum Gleis) rastet ein Bahnhof hoechstens ein.</summary>
    public const float BahnhofFang = 1.0f;

    /// <summary>Gegenprobe: Bahnhof rastet nicht ans Gleis (wie bis S 0.41.0, nur Tests, je Ablauf).</summary>
    public static bool BahnhofAltFuerTests { get => BahnhofAlt.Value; set => BahnhofAlt.Value = value; }
    private static readonly AsyncLocal<bool> BahnhofAlt = new();

    /// <summary>
    /// Gegenprobe Sammelliste 314: nur achsparallele Gleisstuecke und Drehung in Vierteln (wie bis S 0.45.0, nur Tests, je Ablauf).
    /// </summary>
    public static bool BahnhofSchraegAltFuerTests { get => BahnhofSchraegAlt.Value; set => BahnhofSchraegAlt.Value = value; }
    private static readonly AsyncLocal<bool> BahnhofSchraegAlt = new();

    /// <summary>So weit (Grad) darf ein Gleisstueck von einer 15-Grad-Richtung abweichen, damit ein Bahnhof daran einrastet.</summary>
    public const float GleisRichtungFang = 1f;

    /// <summary>
    /// Gerade Gleisstuecke (Anfang, Ende) aller Bahnen mit der Feindrehung (0 bis 75 Grad), die ein Haus braucht, damit seine
    /// Bahnsteige parallel liegen. Seit Welle A 0.70.0 (Sammelliste 314) auch schraeg in einer Richtung von 15-Grad-Schritten
    /// (bis <see cref="GleisRichtungFang"/>); vorher nur achsparallel.
    /// </summary>
    private static List<(Vector2 A, Vector2 B, int Fein)> GeradeGleise(PyObjekt aussen)
    {
        var liste = new List<(Vector2, Vector2, int)>();
        foreach (var t in Aussen.Landschaft(aussen).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "bahn")))
        {
            var l = Linie(t["punkte"]);
            for (var i = 0; i + 1 < l.Count; i++)
            {
                var d = l[i + 1] - l[i];
                if (d.Length() <= 0.5f)
                {
                    continue;
                }
                if (BahnhofSchraegAlt.Value || HausViertelAlt.Value)
                {
                    if (MathF.Abs(d.X) < 1e-3f || MathF.Abs(d.Y) < 1e-3f)
                    {
                        liste.Add((l[i], l[i + 1], 0));
                    }
                    continue;
                }
                var grad = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
                var stufe = Math.Round(grad / Hausdrehung.GradSchritt, MidpointRounding.AwayFromZero) * Hausdrehung.GradSchritt;
                if (Math.Abs(grad - stufe) <= GleisRichtungFang)
                {
                    liste.Add((l[i], l[i + 1], (int)((((long)stufe % 90) + 90) % 90)));
                }
            }
        }
        return liste;
    }

    /// <summary>
    /// Bahnhof ans Gleis legen (Drehung so, dass die Bahnsteige parallel zum Gleis liegen, dann quer verschieben); true = gedreht
    /// oder verschoben. Seit Welle A 0.70.0 auch an einem schraegen geraden Gleis: das Haus dreht dann in Schritten von 15 Grad
    /// (Feld „grad“ bzw. „hausgrad“), gerechnet wird im Rahmen des Hauses.
    /// </summary>
    public static bool BahnhofAnGleis(PyObjekt aussen, AussenStelle s)
    {
        if (BahnhofAlt.Value || s.Art is not (AussenArt.Haus or AussenArt.Gebaeude))
        {
            return false;
        }
        var o = s.Art == AussenArt.Haus ? Ort(aussen, s.Ort) : Aussen.Landschaft(aussen).ElementAtOrDefault(s.Nummer) as PyObjekt;
        if (o is null)
        {
            return false;
        }
        var teile = Get(Modelle(aussen), Py.Str(o["modell"]));
        if (!Bahnsteig.Hat(teile))
        {
            return false;
        }
        var gleise = GeradeGleise(aussen);
        if (gleise.Count == 0)
        {
            return false;
        }
        var (x, y) = (Py.F(o["x"]), Py.F(o["y"]));
        var mitte = new Vector2((float)x, (float)y) + Hausdrehung.Mitte(teile);
        var jetzt = Hausdrehung.Winkel(HausDreh(aussen, s), HausGrad(aussen, s));
        var linien = Aussen.Landschaft(aussen).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "bahn")).Select(t => Linie(t["punkte"])).ToList();
        // Ziel-Winkel: je Feindrehung der Gleise die vier Lagen; zuerst ohne Drehen, dann die kleinste Drehung (rechts vor links),
        // zuletzt halb herum
        static int Abstand(int von, int nach)
        {
            var d = ((((nach - von) % 360) + 540) % 360) - 180;
            return (Math.Abs(d) * 2) + (d < 0 ? 1 : 0);
        }
        var ziele = gleise.Select(g => g.Fein).Distinct()
            .SelectMany(f => Enumerable.Range(0, 4).Select(k => f + (90 * k)))
            .OrderBy(w => Abstand(jetzt, w)).ToList();
        (int Winkel, Vector2 Schub)? bester = null;
        foreach (var ziel in ziele)
        {
            var (dreh, fein) = Hausdrehung.Zerlegen(ziel);
            var lage = new Hausdrehung.Feinlage(mitte, fein);
            // Bahnsteige im Rahmen des Hauses (nur Viertel), Gleise dorthin zurueckgedreht: dann liegen sie achsparallel
            var r = Bahnsteig.Rechtecke(teile, x, y, dreh);
            var waagrecht = r.Sum(q => q.Max.X - q.Min.X) >= r.Sum(q => q.Max.Y - q.Min.Y);
            (float Delta, float Betrag)? hier = null;
            foreach (var (ga, gb, gf) in gleise)
            {
                if (gf != fein)
                {
                    continue;
                }
                var (a, b) = (lage.Zurueck(ga), lage.Zurueck(gb));
                if ((MathF.Abs(a.Y - b.Y) < MathF.Abs(a.X - b.X)) != waagrecht)
                {
                    continue;
                }
                // laengs: Bahnsteige muessen neben dem Gleisstueck liegen; quer: Lage der Gleismitte
                float L(Vector2 p) => waagrecht ? p.X : p.Y;
                float Q(Vector2 p) => waagrecht ? p.Y : p.X;
                var (g1, g2) = (MathF.Min(L(a), L(b)), MathF.Max(L(a), L(b)));
                var neben = r.Where(q => L(q.Max) > g1 + 0.01f && L(q.Min) < g2 - 0.01f).OrderBy(q => Q(q.Min)).ToList();
                if (neben.Count == 0)
                {
                    continue;
                }
                // Gleismitte quer an der Mitte der Bahnsteige (ein Gleis in 15-Grad-Richtung liegt bis 1 Grad schief)
                var lm = neben.Average(q => (L(q.Min) + L(q.Max)) / 2);
                var c = MathF.Abs(L(b) - L(a)) < 1e-6f ? Q(a) : Q(a) + ((Q(b) - Q(a)) * ((lm - L(a)) / (L(b) - L(a))));
                var kandidaten = new List<float>();
                for (var i = 0; i + 1 < neben.Count; i++)
                {
                    if (Q(neben[i + 1].Min) > Q(neben[i].Max) + 0.05f)
                    {
                        kandidaten.Add(c - ((Q(neben[i].Max) + Q(neben[i + 1].Min)) / 2));
                    }
                }
                if (kandidaten.Count == 0 || kandidaten.Min(MathF.Abs) > BahnhofFang)
                {
                    // ein Bahnsteig (bzw. alle auf einer Seite): naechste Kante BahnsteigKante vor der Gleismitte
                    var nah = neben.MinBy(q => MathF.Min(MathF.Abs(Q(q.Min) - c), MathF.Abs(Q(q.Max) - c)));
                    var seite = (Q(nah.Min) + Q(nah.Max)) / 2 >= c ? 1 : -1;
                    kandidaten.Add(seite > 0 ? c + BahnsteigKante - Q(nah.Min) : c - BahnsteigKante - Q(nah.Max));
                }
                var delta = kandidaten.MinBy(MathF.Abs);
                var (lx, ly) = waagrecht ? (0f, delta) : (delta, 0f);
                if (MathF.Abs(delta) <= BahnhofFang + 1e-4f && (hier is null || MathF.Abs(delta) < hier.Value.Betrag)
                    && KoerperFrei(teile, x + lx, y + ly, dreh, [.. linien.Select(l => l.Select(lage.Zurueck).ToList())]))
                {
                    hier = (delta, MathF.Abs(delta));
                }
            }
            if (hier is { } h)
            {
                var schub = lage.Richtung(waagrecht ? new Vector2(0, h.Delta) : new Vector2(h.Delta, 0));
                bester = (ziel, schub);
                break;
            }
        }
        if (bester is not { } wahl)
        {
            return false;
        }
        var dreht = Abstand(jetzt, wahl.Winkel) != 0;
        if (dreht)
        {
            var um = ((((wahl.Winkel - jetzt) % 360) + 360) % 360);
            if (um % 90 == 0)
            {
                HausDrehen(aussen, s, um / 90);
            }
            else
            {
                HausFeinDrehen(aussen, s, um);
            }
        }
        // Lagen stehen auf 0,01 (wie VerschiebenGenau): eine kleinere Abweichung bleibt
        float Runden(float v) => MathF.Abs(v) <= 0.0055f ? 0 : MathF.Round(v, 2);
        var (dx, dy) = (Runden(wahl.Schub.X), Runden(wahl.Schub.Y));
        if (dx != 0 || dy != 0)
        {
            VerschiebenGenau(aussen, s, dx, dy);
        }
        return dreht || dx != 0 || dy != 0;
    }

    /// <summary>Gegenprobe Sammelliste 313: Tiefe eines Blocks aus der Hoehe „h“ (wie bis S 0.44.0, nur Tests, je Ablauf).</summary>
    public static bool KoerperHoeheAltFuerTests { get => KoerperHoeheAlt.Value; set => KoerperHoeheAlt.Value = value; }
    private static readonly AsyncLocal<bool> KoerperHoeheAlt = new();

    private static string KoerperTiefe => KoerperHoeheAlt.Value ? "h" : "t";

    /// <summary>Liegt kein Block des Hauses (ausser den Bahnsteigen) naeher als das halbe Gleisbett an einer Bahn?</summary>
    private static bool KoerperFrei(object? teile, double x, double y, int dreh, List<List<Vector2>> linien)
    {
        var mitte = new Vector2((float)x, (float)y) + Hausdrehung.Mitte(teile);
        // Sammelliste 313 (selbst gefunden): Tiefe des Blocks ist „t“ (vorher die Hoehe „h“: ein flacher, tiefer Block galt als frei)
        foreach (var t in (teile as List<object?> ?? []).OfType<PyObjekt>().Where(t => !Bahnsteig.IstFlaeche(t) && Get(t, "w") is not null && Get(t, KoerperTiefe) is not null))
        {
            var a = Hausdrehung.Drehen(new Vector2((float)(x + Py.F(t["x"])), (float)(y + Py.F(t["y"]))), mitte, dreh);
            var b = Hausdrehung.Drehen(new Vector2((float)(x + Py.F(t["x"]) + Py.F(t["w"])), (float)(y + Py.F(t["y"]) + Py.F(t[KoerperTiefe]))), mitte, dreh);
            var (min, max) = (Vector2.Min(a, b) - new Vector2(StadtbauPruefung.BahnsteigAbstand), Vector2.Max(a, b) + new Vector2(StadtbauPruefung.BahnsteigAbstand));
            foreach (var l in linien)
            {
                for (var i = 0; i + 1 < l.Count; i++)
                {
                    var n = (int)MathF.Ceiling(Vector2.Distance(l[i], l[i + 1]) / 0.05f);
                    for (var k = 0; k <= n; k++)
                    {
                        var p = Vector2.Lerp(l[i], l[i + 1], n == 0 ? 0 : k / (float)n);
                        if (p.X > min.X && p.X < max.X && p.Y > min.Y && p.Y < max.Y)
                        {
                            return false;
                        }
                    }
                }
            }
        }
        return true;
    }
}
