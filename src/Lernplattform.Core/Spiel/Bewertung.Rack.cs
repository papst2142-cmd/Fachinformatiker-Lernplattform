using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Serverschrank bestuecken wie <c>fisi_game.py</c> (Python 0.63.2, Z. 1478-1740:
/// rack_placed, _position_problems, _load_problems, rack_problems, rack_solution).
/// Antwort: {platz: unterste_HE}, platz ist die Nummer des Geraets in "geraete".
/// </summary>
public static partial class Bewertung
{
    /// <summary>Ein eingebautes Geraet: (platz, geraet, unterste_HE).</summary>
    public sealed record RackPlatz(long Index, PyObjekt Geraet, long Unten);

    private static string RackTypName(object? art, PyObjekt inhalt) =>
        Py.Str(Get(Get(Hardware(inhalt), "rack_typen") as PyObjekt ?? [], Py.Str(art), art));

    private static string HeText(long unten, long hoehe)
    {
        var oben = unten + hoehe - 1;
        return oben == unten ? Prozent("HE %d", unten) : Prozent("HE %d–%d", unten, oben);
    }

    private static long? IntOderNichts(object? wert)
    {
        try
        {
            return Py.Int(wert);
        }
        catch (Exception e) when (e is FormatException or InvalidCastException)
        {
            return null;   // TypeError, ValueError
        }
    }

    /// <summary>rack_placed: eingebaute Geraete von unten nach oben; unbekannte Plaetze zaehlen nicht.</summary>
    public static List<RackPlatz> RackBelegt(PyObjekt auftrag, PyObjekt? antwort, PyObjekt inhalt)
    {
        var geraete = ListeOderLeer(auftrag, "geraete");
        var belegt = new List<RackPlatz>();
        foreach (var (k, wert) in antwort ?? [])
        {
            if (IntOderNichts(k) is not long index || IntOderNichts(wert) is not long unten)
            {
                continue;
            }
            if (index >= 0 && index < geraete.Count && unten >= 1 && RackGeraet(geraete[(int)index], inhalt) is { } g)
            {
                belegt.Add(new RackPlatz(index, g, unten));
            }
        }
        return [.. belegt.OrderBy(r => r.Unten).ThenBy(r => r.Index)];
    }

    private static long He(PyObjekt g) => Py.Int(g["he"]);

    private static string Typ(PyObjekt g) => Py.Str(g["typ"]);

    /// <summary>ups_capacity: nutzbare USV-Leistung mit Reserve oder null.</summary>
    private static long? UsvLeistung(PyObjekt auftrag, List<RackPlatz> belegt, PyObjekt inhalt)
    {
        var regeln = Objekt(Hardware(inhalt)["rack_regeln"]);
        var summe = Py.Plus(Py.Summe(belegt.Where(r => Typ(r.Geraet) == "usv").Select(r => Get(r.Geraet, "leistung", 0L))),
            Get(Objekt(auftrag["schrank"]), "usv_watt", 0L));
        if (!Py.Wahr(summe))
        {
            return null;
        }
        return Py.Int(Py.Mal(summe, Py.Minus(1L, regeln["strom_reserve"])));
    }

    private static List<string> LageProbleme(PyObjekt auftrag, List<RackPlatz> belegt, PyObjekt inhalt)
    {
        var regeln = Objekt(Hardware(inhalt)["rack_regeln"]);
        var groesse = Py.Int(Objekt(auftrag["schrank"])["he"]);
        var probleme = new List<string>();
        var benutzt = new Dictionary<long, PyObjekt>();
        foreach (var r in belegt)
        {
            var oben = r.Unten + He(r.Geraet) - 1;
            if (oben > groesse)
            {
                probleme.Add(Prozent("%s (%d HE) passt ab HE %d nicht mehr in den Schrank, er hat nur %d HE.", r.Geraet["name"], r.Geraet["he"], r.Unten, groesse));
                continue;
            }
            for (var he = r.Unten; he <= oben; he++)
            {
                if (benutzt.TryGetValue(he, out var anderes))
                {
                    probleme.Add(Prozent("%s und %s überlappen in HE %d.", anderes["name"], r.Geraet["name"], he));
                    break;
                }
            }
            for (var he = r.Unten; he <= oben; he++)
            {
                benutzt.TryAdd(he, r.Geraet);
            }
        }
        var grenze = Math.Max(1, PyDiv(groesse, 3));
        foreach (var r in belegt)
        {
            if (!Py.Kleiner(r.Geraet["gewicht"], regeln["schwer_ab_kg"]) && r.Unten > grenze)
            {
                probleme.Add(Prozent("%s wiegt %d kg und gehört ins untere Drittel (Beginn bis HE %d), steckt aber in %s.",
                    r.Geraet["name"], r.Geraet["gewicht"], grenze, HeText(r.Unten, He(r.Geraet))));
            }
        }
        var usv = belegt.Where(r => Typ(r.Geraet) == "usv").ToList();
        var andere = belegt.Where(r => Typ(r.Geraet) != "usv").ToList();
        if (usv.Count > 0 && andere.Count > 0 && usv.Max(r => r.Unten) > andere.Min(r => r.Unten))
        {
            probleme.Add("Die USV gehört ganz nach unten, unter alle anderen Geräte.");
        }
        bool Nachbar(RackPlatz r, string art)
        {
            var oben = r.Unten + He(r.Geraet) - 1;
            return belegt.Any(o => Typ(o.Geraet) == art && (o.Unten == oben + 1 || o.Unten + He(o.Geraet) - 1 == r.Unten - 1));
        }
        foreach (var r in belegt)
        {
            if (Typ(r.Geraet) == "patchpanel" && !Nachbar(r, "switch"))
            {
                probleme.Add(Prozent("%s in %s hat keinen Switch direkt darüber oder darunter (kurze Patchkabel).", r.Geraet["name"], HeText(r.Unten, He(r.Geraet))));
            }
            else if (Typ(r.Geraet) == "switch" && !Nachbar(r, "patchpanel"))
            {
                probleme.Add(Prozent("%s in %s hat kein Patchpanel direkt darüber oder darunter.", r.Geraet["name"], HeText(r.Unten, He(r.Geraet))));
            }
        }
        return probleme;
    }

    /// <summary>a // b fuer ganze Zahlen (rundet nach unten).</summary>
    private static long PyDiv(long a, long b)
    {
        var q = a / b;
        return (a % b != 0 && ((a < 0) ^ (b < 0))) ? q - 1 : q;
    }

    private static List<string> LastProbleme(PyObjekt auftrag, List<RackPlatz> belegt, PyObjekt inhalt)
    {
        var schrank = Objekt(auftrag["schrank"]);
        var probleme = new List<string>();
        var gewicht = Py.Summe(belegt.Select(r => r.Geraet["gewicht"]));
        var strom = Py.Summe(belegt.Where(r => Typ(r.Geraet) != "usv").Select(r => r.Geraet["watt"]));
        if (Py.Kleiner(schrank["traglast"], gewicht))
        {
            probleme.Add(Prozent("Zu schwer: %d kg bei %d kg Traglast des Schranks.", gewicht, schrank["traglast"]));
        }
        var leistung = UsvLeistung(auftrag, belegt, inhalt);
        var reserve = Skaliert(Objekt(Hardware(inhalt)["rack_regeln"])["strom_reserve"], 100L);
        if (leistung is long l && Py.Kleiner(l, strom))
        {
            probleme.Add(Prozent("Die USV schafft mit %d %% Reserve nur %d W, die Geräte brauchen %d W.", reserve, l, strom));
        }
        if (Py.Wahr(Get(schrank, "kuehlung")) && Py.Kleiner(schrank["kuehlung"], strom))
        {
            probleme.Add(Prozent("Die Kühlung führt höchstens %d W Abwärme ab, die Geräte erzeugen %d W.", schrank["kuehlung"], strom));
        }
        var regeln = Py.Wahr(Get(auftrag, "vorgaben")) ? Objekt(auftrag["vorgaben"]) : [];
        var mindestens = Py.Wahr(Get(regeln, "mindestens")) ? Objekt(regeln["mindestens"]) : [];
        var ordnung = (Get(Hardware(inhalt), "rack_typen") as PyObjekt ?? []).Select(p => p.Key).ToList();
        foreach (var (art, anzahl) in mindestens.OrderBy(p => ordnung.IndexOf(p.Key) is var i and >= 0 ? i : ordnung.Count))
        {
            long da = belegt.Count(r => Typ(r.Geraet) == art);
            if (Py.Kleiner(da, anzahl))
            {
                probleme.Add(Prozent("Der Auftrag verlangt %d × %s, eingebaut %s %d.", anzahl, RackTypName(art, inhalt), da == 1 ? "ist" : "sind", da));
            }
        }
        if (Py.Wahr(Get(regeln, "ports_min")))
        {
            foreach (var art in new[] { "switch", "patchpanel" })
            {
                var ports = Py.Summe(belegt.Where(r => Typ(r.Geraet) == art).Select(r => Get(r.Geraet, "ports", 0L)));
                if (Py.Kleiner(ports, regeln["ports_min"]))
                {
                    probleme.Add(Prozent("Zu wenige Ports (%s): %d, gebraucht werden %d.", RackTypName(art, inhalt), ports, regeln["ports_min"]));
                }
            }
        }
        if (Py.Wahr(Get(regeln, "frei_min")))
        {
            object frei = schrank["he"]!;
            foreach (var r in belegt)
            {
                frei = Py.Minus(frei, r.Geraet["he"]);
            }
            if (Py.Kleiner(frei, regeln["frei_min"]))
            {
                probleme.Add(Prozent("Es sollen mindestens %d HE für später frei bleiben, frei sind %d.", regeln["frei_min"], Py.Max(0L, frei)));
            }
        }
        return probleme;
    }

    /// <summary>rack_problems: alle Probleme einer Schrankbelegung.</summary>
    public static List<string> RackProbleme(PyObjekt auftrag, PyObjekt? antwort, PyObjekt inhalt)
    {
        var belegt = RackBelegt(auftrag, antwort, inhalt);
        if (belegt.Count == 0)
        {
            return ["Der Schrank ist noch leer."];
        }
        return [.. LageProbleme(auftrag, belegt, inhalt), .. LastProbleme(auftrag, belegt, inhalt)];
    }

    /// <summary>rack_solution: erst welche Geraete (Gewicht, Strom, Vorgaben), dann alle Reihenfolgen lueckenlos von unten.</summary>
    public static PyObjekt? RackLoesung(PyObjekt auftrag, PyObjekt inhalt)
    {
        var geraete = ListeOderLeer(auftrag, "geraete");
        for (var groesse = 1; groesse <= geraete.Count; groesse++)
        {
            foreach (var gewaehlt in Kombinationen(geraete.Count, groesse))
            {
                var zeilen = gewaehlt.Select(i => new RackPlatz(i, RackGeraet(geraete[i], inhalt)!, 1)).ToList();
                if (LastProbleme(auftrag, zeilen, inhalt).Count > 0)
                {
                    continue;
                }
                var gesehen = new HashSet<string>();
                foreach (var folge in Permutationen(gewaehlt))
                {
                    if (!gesehen.Add(string.Join("\u0001", folge.Select(i => Py.Str(geraete[i])))))
                    {
                        continue;
                    }
                    var antwort = new PyObjekt();
                    long unten = 1;
                    foreach (var i in folge)
                    {
                        antwort.Add(i.ToString(System.Globalization.CultureInfo.InvariantCulture), unten);
                        unten += He(RackGeraet(geraete[i], inhalt)!);
                    }
                    if (RackProbleme(auftrag, antwort, inhalt).Count == 0)
                    {
                        return antwort;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>itertools.combinations(range(n), k) in Python-Reihenfolge.</summary>
    internal static IEnumerable<int[]> Kombinationen(int n, int k)
    {
        var index = Enumerable.Range(0, k).ToArray();
        if (k > n)
        {
            yield break;
        }
        while (true)
        {
            yield return (int[])index.Clone();
            var i = k - 1;
            while (i >= 0 && index[i] == i + n - k)
            {
                i--;
            }
            if (i < 0)
            {
                yield break;
            }
            index[i]++;
            for (var j = i + 1; j < k; j++)
            {
                index[j] = index[j - 1] + 1;
            }
        }
    }

    /// <summary>itertools.permutations(werte) in Python-Reihenfolge (lexikografisch nach Stelle).</summary>
    internal static IEnumerable<int[]> Permutationen(int[] werte)
    {
        if (werte.Length <= 1)
        {
            yield return (int[])werte.Clone();
            yield break;
        }
        for (var i = 0; i < werte.Length; i++)
        {
            var rest = werte.Take(i).Concat(werte.Skip(i + 1)).ToArray();
            foreach (var p in Permutationen(rest))
            {
                yield return [werte[i], .. p];
            }
        }
    }
}
