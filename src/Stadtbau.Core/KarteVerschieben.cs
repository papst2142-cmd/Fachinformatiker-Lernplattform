using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.56.0 (Sammelliste 362, Weisung P12-287, Nico 1a 2b): die ganze Stadt draussen um einen Versatz verschieben
/// (Karte vergroessern mit Wahl des Platzes, Stadtteil einsetzen). Jede Lage wird genau um (dx, dy) verschoben, nichts
/// rastet neu ein, nichts wird neu gelegt: Haeuser, Schilder, Tueren und Wege, Strassen (auch Kurven und Kreisverkehre),
/// Bahn, Fluss, Baeume, Parks, Gebaeude ohne Ort, Zierwege, Fusswege, Haltestellen, Parkplaetze, Ampeln, Zebrastreifen,
/// Teiche, Stege, Huegel, gemalte Hoehen (Raster 0,5), Katalog-Stuecke, Stadtteil-Namen, Questgeber draussen, Laternen
/// (auch die Merkliste der automatischen), Beete und Notizen. Modelle, Innenraeume, Quests (Arbeitsort liegt im Raum),
/// Oeffnungszeiten, Uhr und Farben haben keine Lage auf der Karte und bleiben.
/// <para>Ein Teil, den diese Liste nicht kennt, wird nie still mitgenommen: <see cref="Unbekannt"/> nennt ihn, und
/// <see cref="Alles"/> verschiebt dann nichts (der Aufrufer zeigt den Grund).</para>
/// </summary>
public static class KarteVerschieben
{
    /// <summary>Teile ohne Lage auf der Karte (bleiben, wie sie sind).</summary>
    public static readonly IReadOnlySet<string> OhneLage = new HashSet<string>(StringComparer.Ordinal)
    {
        "karte/modelle", "stadtdaten/neue_modelle", "stadtdaten/bauteile", "bauten", "stadtdaten/" + Questebene.AbschnittQuests,
        "stadtdaten/oeffnungszeiten", "stadtdaten/uhr", "stadtdaten/raster", "stadtdaten/farben", "karte/_hinweis", "stadtdaten/_hinweis",
        "stadtdaten/" + StadtPaket.Abschnitt,
    };

    /// <summary>Teile mit Lage (werden verschoben).</summary>
    public static readonly IReadOnlySet<string> MitLage = new HashSet<string>(StringComparer.Ordinal)
    {
        "karte/welt", "karte/orte", "stadtdaten/neue_orte", "stadtdaten/zugaenge", "stadtdaten/zugaenge_neu", "stadtdaten/darstellung",
        "stadtdaten/fusswege", "stadtdaten/haltestellen", "stadtdaten/parkplaetze", "stadtdaten/ampeln", "stadtdaten/zebrastreifen",
        "stadtdaten/katalog", "stadtdaten/wasser", "stadtdaten/" + Gelaende.Abschnitt, "stadtdaten/" + Stadtteile.AbschnittNamen,
        "stadtdaten/" + Questebene.AbschnittQuestgeber, "laternen", "beete", LaternenAutomatik.Teil, AussenBearbeitung.NotizTeil,
    };

    /// <summary>Teile der Stadt draussen, die weder <see cref="MitLage"/> noch <see cref="OhneLage"/> kennt.</summary>
    public static List<string> Unbekannt(PyObjekt aussen) =>
        [.. Aussen.Teile(aussen).Select(t => t.Pfad).Where(p => !MitLage.Contains(p) && !OhneLage.Contains(p)).Order(StringComparer.Ordinal)];

    /// <summary>
    /// Alles um (<paramref name="dx"/>, <paramref name="dy"/>) verschieben (ganze Felder; die Karte selbst waechst nicht mit,
    /// das macht der Aufrufer). Gibt es einen unbekannten Teil, bleibt alles unveraendert und die Rueckgabe nennt ihn.
    /// </summary>
    public static List<string> Alles(PyObjekt aussen, double dx, double dy)
    {
        var unbekannt = Unbekannt(aussen);
        if (unbekannt.Count > 0 || (dx == 0 && dy == 0))
        {
            return unbekannt;
        }
        var v = new Versatz(dx, dy);
        var karte = Aussen.Karte(aussen);
        var sd = Aussen.Stadtdaten(aussen);
        foreach (var l in Aussen.Landschaft(aussen).OfType<PyObjekt>())
        {
            v.Punkte(Get(l, "punkte"));
            v.Lage(l);
        }
        foreach (var o in ListeOderLeer(karte, "orte").Concat(ListeOderLeer(sd, "neue_orte")).OfType<PyObjekt>())
        {
            v.Lage(o);
            v.Punkt(Get(o, "schild"));
        }
        foreach (var abschnitt in new[] { "zugaenge", "zugaenge_neu" })
        {
            if (Get(sd, abschnitt) is PyObjekt z)
            {
                foreach (var (_, wert) in z)
                {
                    v.Zugang(wert as PyObjekt);
                }
            }
        }
        if (Get(sd, "darstellung") is PyObjekt d)
        {
            foreach (var z in ListeOderLeer(d, "zierwege").OfType<PyObjekt>())
            {
                v.Zugang(z);
            }
        }
        foreach (var f in ListeOderLeer(sd, "fusswege").OfType<PyObjekt>())
        {
            v.Punkte(Get(f, "punkte"));
        }
        foreach (var abschnitt in new[] { "haltestellen", "parkplaetze", "ampeln", "zebrastreifen", "katalog" })
        {
            foreach (var o in ListeOderLeer(sd, abschnitt).OfType<PyObjekt>())
            {
                v.Lage(o);
            }
        }
        if (Get(sd, "wasser") is PyObjekt w)
        {
            foreach (var t in ListeOderLeer(w, "teiche").OfType<PyObjekt>())
            {
                v.Punkte(Get(t, "punkte"));
            }
            foreach (var s in ListeOderLeer(w, "stege").OfType<PyObjekt>())
            {
                v.Lage(s);
            }
        }
        if (Get(sd, Gelaende.Abschnitt) is PyObjekt g)
        {
            foreach (var h in ListeOderLeer(g, "huegel").OfType<PyObjekt>())
            {
                v.Lage(h);
            }
            foreach (var e in ListeOderLeer(g, "gemalt").OfType<List<object?>>())
            {
                v.Raster(e);
            }
        }
        foreach (var n in ListeOderLeer(sd, Stadtteile.AbschnittNamen).OfType<PyObjekt>())
        {
            v.Feld(n, "x1", dx);
            v.Feld(n, "x2", dx);
            v.Feld(n, "z1", dy);
            v.Feld(n, "z2", dy);
        }
        foreach (var q in ListeOderLeer(sd, Questebene.AbschnittQuestgeber).OfType<PyObjekt>())
        {
            if (Get(q, "platz") is PyObjekt platz && Py.Wahr(Get(platz, "draussen")))
            {
                v.Lage(platz);
            }
        }
        v.Punkte(aussen["laternen"]);
        v.Punkte(aussen["beete"]);
        if (Get(aussen, LaternenAutomatik.Teil) is PyObjekt auto)
        {
            foreach (var (_, liste) in auto)
            {
                v.Punkte(liste);
            }
        }
        foreach (var n in (Get(aussen, AussenBearbeitung.NotizTeil) as List<object?> ?? []).OfType<PyObjekt>())
        {
            v.Lage(n);
        }
        return [];
    }

    /// <summary>Ein Versatz; jede Zahl behaelt ihre Nachkommastellen (keine neue Rundung, ganze Zahl bleibt ganze Zahl).</summary>
    private sealed class Versatz(double dx, double dy)
    {
        public void Lage(PyObjekt o)
        {
            if (Get(o, "x") is long or double && Get(o, "y") is long or double)
            {
                Feld(o, "x", dx);
                Feld(o, "y", dy);
            }
        }

        public void Feld(PyObjekt o, string feld, double um)
        {
            if (Get(o, feld) is long or double)
            {
                Py.Setzen(o, feld, Plus(o[feld], um));
            }
        }

        public void Punkt(object? p)
        {
            if (p is List<object?> { Count: >= 2 } l && l[0] is long or double && l[1] is long or double)
            {
                l[0] = Plus(l[0], dx);
                l[1] = Plus(l[1], dy);
            }
        }

        public void Punkte(object? liste)
        {
            foreach (var p in liste as List<object?> ?? [])
            {
                Punkt(p);
            }
        }

        public void Zugang(PyObjekt? z)
        {
            if (z is null)
            {
                return;
            }
            Punkt(Get(z, "tuer"));
            Punkte(Get(z, "weg"));
        }

        /// <summary>Gemalte Hoehe [i, j, h]: Lage i · 0,5, j · 0,5.</summary>
        public void Raster(List<object?> e)
        {
            if (e is { Count: 3 } && e[0] is long i && e[1] is long j)
            {
                e[0] = i + (long)Math.Round(dx / Gelaende.Raster);
                e[1] = j + (long)Math.Round(dy / Gelaende.Raster);
            }
        }
    }

    /// <summary>Wert plus Versatz mit denselben Nachkommastellen wie vorher (Text wie json.dumps).</summary>
    internal static object Plus(object? wert, double um)
    {
        if (wert is long l && um == Math.Floor(um))
        {
            return l + (long)um;
        }
        var w = Py.F(wert);
        var text = w.ToString("R", CultureInfo.InvariantCulture);
        var stellen = text.Contains('E', StringComparison.Ordinal) ? 10 : text.Contains('.', StringComparison.Ordinal) ? text.Length - text.IndexOf('.') - 1 : 0;
        return Math.Round(w + um, Math.Min(stellen, 10), MidpointRounding.AwayFromZero);
    }
}
