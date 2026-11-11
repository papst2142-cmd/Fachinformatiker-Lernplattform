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
/// <see cref="BahnsteigKante"/> vor der Gleismitte. Der Bahnhof dreht dazu in Vierteln, bis die Bahnsteige parallel zum Gleis
/// liegen. Nur wenn ein gerades, achsparalleles Gleisstueck neben dem Bahnsteig liegt und die Verschiebung hoechstens
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

    /// <summary>Gerade, achsparallele Gleisstuecke (Anfang, Ende) aller Bahnen.</summary>
    private static List<(Vector2 A, Vector2 B)> GeradeGleise(PyObjekt aussen)
    {
        var liste = new List<(Vector2, Vector2)>();
        foreach (var t in Aussen.Landschaft(aussen).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "bahn")))
        {
            var l = Linie(t["punkte"]);
            for (var i = 0; i + 1 < l.Count; i++)
            {
                var d = l[i + 1] - l[i];
                if (d.Length() > 0.5f && (MathF.Abs(d.X) < 1e-3f || MathF.Abs(d.Y) < 1e-3f))
                {
                    liste.Add((l[i], l[i + 1]));
                }
            }
        }
        return liste;
    }

    /// <summary>Bahnhof ans Gleis legen (Drehung in Vierteln, dann quer verschieben); true = gedreht oder verschoben.</summary>
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
        var jetzt = HausDreh(aussen, s);
        (int Mal, float Delta, bool Waagrecht)? bester = null;
        foreach (var mal in new[] { 0, 1 })
        {
            var r = Bahnsteig.Rechtecke(teile, x, y, (jetzt + mal) % 4);
            var waagrecht = r.Sum(q => q.Max.X - q.Min.X) >= r.Sum(q => q.Max.Y - q.Min.Y);
            foreach (var (a, b) in gleise)
            {
                if ((MathF.Abs(a.Y - b.Y) < 1e-3f) != waagrecht)
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
                var c = Q(a);
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
                if (MathF.Abs(delta) <= BahnhofFang + 1e-4f && (bester is null || (mal, MathF.Abs(delta)).CompareTo((bester.Value.Mal, MathF.Abs(bester.Value.Delta))) < 0))
                {
                    bester = (mal, delta, waagrecht);
                }
            }
            if (bester is not null)
            {
                break;
            }
        }
        if (bester is not { } wahl)
        {
            return false;
        }
        if (wahl.Mal > 0)
        {
            HausDrehen(aussen, s, wahl.Mal);
        }
        var d = MathF.Round(wahl.Delta, 3);
        if (MathF.Abs(d) >= 0.0005f)
        {
            VerschiebenGenau(aussen, s, wahl.Waagrecht ? 0 : d, wahl.Waagrecht ? d : 0);
        }
        return wahl.Mal > 0 || MathF.Abs(d) >= 0.0005f;
    }
}
