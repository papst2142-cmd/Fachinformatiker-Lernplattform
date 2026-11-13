using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// S 0.50.0 (Weisung P12-234 mit P12-231 Antwort 1b): Moebel im Innenraum in 3D setzen, schieben, drehen und loeschen, ohne
/// Oberflaeche. Die 3D-Koordinaten des Innenraums sind die des Grundrisses (x bleibt, y des Grundrisses ist z, siehe
/// <see cref="Innenraum"/>), darum laufen alle Schritte ueber dieselben Funktionen wie der Grundriss (<see cref="Bearbeitung"/>):
/// gleiche Schritte, gleicher Verlauf, gleiche Pruefung. Treffer mit einem Strahl aus der Kamera: zuerst der Kasten eines
/// Stuecks (Hoehe wie gezeichnet), sonst der Punkt am Boden mit demselben Treffertest wie der Grundriss. Der Schatten am Boden
/// ist gruen, wenn der Schritt keinen neuen roten Fund bringt (<see cref="Bauen3D.NeuRot"/>), sonst rot mit dem Fund.
/// </summary>
public static class InnenBauen3D
{
    /// <summary>Hoehe eines Stuecks, das (noch) nicht gezeichnet ist (z. B. eben gesetzt, Innenraum baut noch).</summary>
    public const float HoeheOhneBild = 0.6f;

    /// <summary>Kasten eines Stuecks in 3D (x, Hoehe, z).</summary>
    public sealed record Kasten(Stelle Stelle, Vector3 Min, Vector3 Max);

    /// <summary>
    /// Alle Stuecke des Grundrisses als Kasten; die Hoehe kommt aus dem gezeichneten Innenraum (<see cref="Innenraum.DekoUmrisse"/>,
    /// gleicher Typ an gleicher Stelle), sonst <see cref="HoeheOhneBild"/>.
    /// </summary>
    public static List<Kasten> Kaesten(PyObjekt gebaeude, Innenraum? innen)
    {
        var hoehen = new Dictionary<(string, double, double), float>();
        foreach (var u in innen?.DekoUmrisse ?? [])
        {
            if (u.Teile.Count > 0 && u.Deko["typ"] is string typ)
            {
                var schluessel = (typ, Bearbeitung.Runden(Py.F(u.Deko["x"])), Bearbeitung.Runden(Py.F(u.Deko["y"])));
                hoehen[schluessel] = Math.Max(hoehen.GetValueOrDefault(schluessel), u.Flaeche.Oben);
            }
        }
        var ergebnis = new List<Kasten>();
        foreach (var (stelle, d) in Bearbeitung.Alle(gebaeude))
        {
            double x = Py.F(d["x"]), y = Py.F(d["y"]), w = Py.F(d["w"]), h = Py.F(d["h"]);
            var oben = hoehen.TryGetValue((Py.Str(d["typ"]), Bearbeitung.Runden(x), Bearbeitung.Runden(y)), out var o) && o > 0.05f ? o : HoeheOhneBild;
            ergebnis.Add(new Kasten(stelle, new Vector3((float)x, 0, (float)y), new Vector3((float)(x + w), oben, (float)(y + h))));
        }
        return ergebnis;
    }

    /// <summary>
    /// Stueck unter dem Strahl: der vorderste getroffene Kasten, sonst das oberste Stueck am Bodenpunkt (wie der Grundriss,
    /// <see cref="Bearbeitung.StueckBei"/>), sonst null.
    /// </summary>
    public static Stelle? Treffer(PyObjekt gebaeude, IReadOnlyList<Kasten> kaesten, Vector3 start, Vector3 richtung)
    {
        Stelle? beste = null;
        var bestT = float.MaxValue;
        foreach (var k in kaesten)
        {
            if (Bauen3D.Schnitt(start, richtung, k.Min, k.Max) is { } t && t < bestT)
            {
                (beste, bestT) = (k.Stelle, t);
            }
        }
        if (beste is not null)
        {
            return beste;
        }
        return Bauen3D.Boden(start, richtung) is { } b ? Bearbeitung.StueckBei(gebaeude, b.X, b.Y) : null;
    }

    /// <summary>Grundflaeche eines Stuecks als Viereck (fuer den Schatten).</summary>
    public static Vector2[] Umriss(PyObjekt gebaeude, Stelle s)
    {
        var d = Bearbeitung.Deko(gebaeude, s);
        float x = (float)Py.F(d["x"]), y = (float)Py.F(d["y"]), w = (float)Py.F(d["w"]), h = (float)Py.F(d["h"]);
        return [new(x, y), new(x + w, y), new(x + w, y + h), new(x, y + h)];
    }

    /// <summary>Stand nach dem Schritt (Kopie des Grundrisses), das neue oder bewegte Stueck (null = geht nicht) und sein Umriss.</summary>
    public sealed record Vorschau(PyObjekt Gebaeude, Stelle? Stelle, Vector2[] Umriss, string? Grund = null);

    /// <summary>Setzen wie <c>GrundrissFlaeche.SetzenAn</c> (gleiches Raster), nur auf einer Kopie.</summary>
    public static Vorschau Setzen(PyObjekt gebaeude, string typ, double x, double y, bool raster)
    {
        var kopie = (PyObjekt)Py.Kopie(gebaeude)!;
        var s = Bearbeitung.Setzen(kopie, typ, x, y, raster);
        return new Vorschau(kopie, s, s is null ? [] : Umriss(kopie, s), s is null ? Texte.InnenBauen3DAusserhalb : null);
    }

    /// <summary>Verschieben wie <c>GrundrissFlaeche</c> nach dem Ziehen (ganze Rasterschritte), nur auf einer Kopie.</summary>
    public static Vorschau Verschieben(PyObjekt gebaeude, Stelle s, double dx, double dy, bool raster)
    {
        var kopie = (PyObjekt)Py.Kopie(gebaeude)!;
        var neu = Bearbeitung.Verschieben(kopie, s, dx, dy, raster);
        return new Vorschau(kopie, neu, Umriss(kopie, neu));
    }

    /// <summary>Was die Pruefung eines Innenraums braucht (Kopien vom Oberflaechen-Faden), siehe <see cref="Pruefen"/>.</summary>
    public sealed record PruefAuftrag(string Basis, int Etage, IReadOnlyList<PyObjekt> Personen, Spieldaten Daten, PyObjekt? Erdgeschoss,
        IReadOnlyList<StadtbauFund> EntwurfFunde);

    /// <summary>Auf dem Oberflaechen-Faden: Kopien fuer die Pruefung des offenen Innenraums (oder der Etage „ort@k“).</summary>
    public static PruefAuftrag Vorbereiten(Projekt p, string ort)
    {
        var (basis, etage) = Etagen.Teilen(ort);
        var eg = etage > 0 && Etagen.Hat(p.Grundriss(basis)) ? (PyObjekt)Py.Kopie(p.Grundriss(basis))! : null;
        var entwurf = p.EtagenEntwurfFunde(basis).Where(f => f.Etage == etage).Select(f => f.Fund).ToList();
        return new PruefAuftrag(basis, etage, p.Personen(ort), p.Daten, eg, entwurf);
    }

    /// <summary>
    /// Funde und Grenzen eines Grundrisses <paramref name="g"/> mit derselben Rechnung wie die Pruefung rechts im Grundriss:
    /// Regeln des gemeinsamen Teils plus Katalog-Stuecke (<see cref="KatalogPruefung.Innen"/>), dazu die Regeln zwischen den
    /// Etagen fuer diese Etage (im Erdgeschoss mit <paramref name="g"/> selbst) und die Funde zum Entwurf.
    /// </summary>
    public static (List<StadtbauFund> Funde, (long Dreiecke, int Aufrufe) Grenzen) Pruefen(PruefAuftrag a, PyObjekt g)
    {
        var (funde, grenzen) = KatalogPruefung.Innen(Katalog.Aktiv, a.Basis, g, a.Personen, a.Daten.Grafik, a.Daten.Geschaefte, a.Daten.Fassung.Innenraeume, a.Etage);
        var eg = a.Etage == 0 ? (Etagen.Hat(g) ? g : null) : a.Erdgeschoss;
        if (eg is not null)
        {
            funde.AddRange(StadtbauPruefung.Etagen(a.Basis, eg, a.Daten.Grafik, (_, _) => []).Where(f => f.Etage == a.Etage).Select(f => f.Fund));
        }
        funde.AddRange(a.EntwurfFunde);
        return (funde, grenzen);
    }
}
