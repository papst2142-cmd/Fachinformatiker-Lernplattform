using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.51.0 (Weisung P12-241, Kurzplan P12-131 Punkt 4): Gelaende in der Mehrfachauswahl.
/// <list type="bullet">
/// <item>Rahmen und Umschalt+Klick waehlen auch Huegel (seit S 0.42.0) und gemalte Hoehen: jeder gemalte Rasterpunkt im Rahmen
/// ist ein Stueck der Art <see cref="AussenArt.Hoehe"/>; Umschalt+Klick auf gemalten Boden nimmt den ganzen zusammenhaengenden
/// Bereich dazu oder heraus (<see cref="HoehenBereichBei"/>).</item>
/// <item>Verschieben, Drehen, Kopieren und Loeschen der Gruppe nehmen die gemalten Hoehen mit, je Aktion ein Schritt
/// (<see cref="HoehenAbbilden"/>). Die Punkte bleiben auf dem Raster 0,5: eine Gruppe mit gemalten Hoehen rueckt immer in
/// ganzen Schritten 0,5. Was ueber den Rand der Karte faellt, entfaellt und zaehlt als uebersprungen; ein Zielpunkt, der
/// schon gemalt ist, bekommt die neue Hoehe.</item>
/// <item>Bei <see cref="Gelaende.HuegelHoechstens"/> Huegeln setzt und kopiert das Werkzeug keinen weiteren (<see cref="HuegelVoll"/>);
/// die Oberflaeche macht den Knopf „Hügel“ grau und nennt den Grund, statt dass erst danach ein roter Fund kommt.</item>
/// </list>
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>Gegenprobe: gemalte Hoehen nicht waehlbar und 17 Huegel moeglich (Stand S 0.50.0, nur Tests, je Ablauf).</summary>
    public static bool GelaendeAuswahlAltFuerTests { get => GelaendeAuswahlAlt.Value; set => GelaendeAuswahlAlt.Value = value; }

    private static readonly AsyncLocal<bool> GelaendeAuswahlAlt = new();

    /// <summary>Ein gemalter Punkt: Raster [i, j], Stelle in der Stadt, Hoehe in Hundertsteln.</summary>
    public readonly record struct GemalterPunkt(int I, int J, Vector2 Lage, int Hundertstel);

    /// <summary>Der gemalte Punkt Nummer <paramref name="nummer"/> (null = gibt es nicht oder falsch gespeichert).</summary>
    public static GemalterPunkt? HoehePunkt(PyObjekt aussen, int nummer)
    {
        var liste = GemaltListe(aussen);
        if (nummer < 0 || nummer >= liste.Count || liste[nummer] is not List<object?> { Count: 3 } q || q[0] is not long i || q[1] is not long j || q[2] is not long h)
        {
            return null;
        }
        return new GemalterPunkt((int)i, (int)j, new Vector2(i * Gelaende.Raster, j * Gelaende.Raster), (int)h);
    }

    /// <summary>Alle gemalten Punkte als Stuecke mit ihrer Stelle (leer in der Gegenprobe).</summary>
    public static IEnumerable<(AussenStelle Stelle, double X, double Y)> AlleHoehen(PyObjekt aussen)
    {
        if (GelaendeAuswahlAlt.Value)
        {
            yield break;
        }
        var liste = GemaltListe(aussen);
        for (var n = 0; n < liste.Count; n++)
        {
            if (HoehePunkt(aussen, n) is { } p)
            {
                yield return (new AussenStelle(AussenArt.Hoehe, n), p.Lage.X, p.Lage.Y);
            }
        }
    }

    /// <summary>
    /// Der zusammenhaengende gemalte Bereich unter (x, y): ausgehend vom naechsten gemalten Punkt (hoechstens ein Rasterschritt
    /// entfernt) alle Punkte, die ueber Nachbarn (auch schraeg) daran haengen. Leer = dort ist nichts gemalt.
    /// </summary>
    public static List<AussenStelle> HoehenBereichBei(PyObjekt aussen, double x, double y)
    {
        var alle = AlleHoehen(aussen).ToList();
        var p = new Vector2((float)x, (float)y);
        var start = alle.Where(a => Vector2.Distance(new Vector2((float)a.X, (float)a.Y), p) <= Gelaende.Raster + 1e-3f)
            .OrderBy(a => Vector2.Distance(new Vector2((float)a.X, (float)a.Y), p)).Select(a => (AussenStelle?)a.Stelle).FirstOrDefault();
        if (start is null)
        {
            return [];
        }
        var nachRaster = new Dictionary<(int, int), AussenStelle>();
        foreach (var (s, _, _) in alle)
        {
            var q = HoehePunkt(aussen, s.Nummer)!.Value;
            nachRaster[(q.I, q.J)] = s;
        }
        var erster = HoehePunkt(aussen, start.Nummer)!.Value;
        var gefunden = new HashSet<(int, int)> { (erster.I, erster.J) };
        var offen = new Queue<(int I, int J)>();
        offen.Enqueue((erster.I, erster.J));
        while (offen.Count > 0)
        {
            var (i, j) = offen.Dequeue();
            for (var a = -1; a <= 1; a++)
            {
                for (var b = -1; b <= 1; b++)
                {
                    var n = (i + a, j + b);
                    if (nachRaster.ContainsKey(n) && gefunden.Add(n))
                    {
                        offen.Enqueue(n);
                    }
                }
            }
        }
        return [.. gefunden.Select(g => nachRaster[g]).OrderBy(s => s.Nummer)];
    }

    /// <summary>
    /// Die gemalten Punkte <paramref name="nummern"/> abbilden (ein Teil einer Aktion der Gruppe): <paramref name="abbilden"/> = neue
    /// Stelle (auf das Raster 0,5 gerundet), null = loeschen; <paramref name="kopie"/> = die alten bleiben. Rueckgabe: die Stellen
    /// der neuen Punkte (Nummern nach dem Neuschreiben der Liste) und wie viele ueber den Rand fielen.
    /// </summary>
    public static (List<AussenStelle> Neu, int Uebersprungen) HoehenAbbilden(PyObjekt aussen, IEnumerable<int> nummern, Func<Vector2, Vector2>? abbilden, bool kopie)
    {
        var punkte = nummern.Distinct().Select(n => HoehePunkt(aussen, n)).OfType<GemalterPunkt>().ToList();
        if (punkte.Count == 0)
        {
            return ([], 0);
        }
        var gemalt = new Dictionary<(int I, int J), int>();
        foreach (var e in GemaltListe(aussen))
        {
            if (e is List<object?> { Count: 3 } q && q[0] is long i && q[1] is long j && q[2] is long h)
            {
                gemalt[((int)i, (int)j)] = (int)h;
            }
        }
        if (!kopie)
        {
            foreach (var p in punkte)
            {
                gemalt.Remove((p.I, p.J));
            }
        }
        var (breite, hoehe) = Aussen.Groesse(aussen);
        var imax = (int)Math.Floor(breite / Gelaende.Raster) - 1;
        var jmax = (int)Math.Floor(hoehe / Gelaende.Raster) - 1;
        var ziele = new HashSet<(int, int)>();
        var uebersprungen = 0;
        if (abbilden is not null)
        {
            foreach (var p in punkte)
            {
                var z = abbilden(p.Lage);
                var (i, j) = ((int)MathF.Round(z.X / Gelaende.Raster), (int)MathF.Round(z.Y / Gelaende.Raster));
                if (i < 1 || j < 1 || i > imax || j > jmax)
                {
                    uebersprungen++;
                    continue;
                }
                gemalt[(i, j)] = p.Hundertstel;
                ziele.Add((i, j));
            }
        }
        var sortiert = gemalt.OrderBy(e => e.Key.I).ThenBy(e => e.Key.J).ToList();
        var g = GelaendeAnlegen(aussen);
        if (sortiert.Count == 0)
        {
            Weg(g, "gemalt");
            GelaendeAufraeumen(aussen);
        }
        else
        {
            Py.Setzen(g, "gemalt", sortiert.Select(e => (object?)new List<object?> { (long)e.Key.I, (long)e.Key.J, (long)e.Value }).ToList());
        }
        var neu = new List<AussenStelle>();
        for (var n = 0; n < sortiert.Count; n++)
        {
            if (ziele.Contains(sortiert[n].Key))
            {
                neu.Add(new AussenStelle(AussenArt.Hoehe, n));
            }
        }
        return (neu, uebersprungen);
    }

    /// <summary>Hat die Stadt schon so viele Huegel, wie erlaubt sind (dann kein neuer und keine Kopie)?</summary>
    public static bool HuegelVoll(PyObjekt aussen) => !GelaendeAuswahlAlt.Value && HuegelListe(aussen).Count >= Gelaende.HuegelHoechstens;
}
