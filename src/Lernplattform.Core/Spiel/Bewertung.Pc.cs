using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: PC zusammenbauen und Bestellungen wie <c>fisi_game.py</c> (Python 0.63.2,
/// Z. 1209-1450: psu_needed, compatibility_problems, build_problems, _need_mismatch, offer_need,
/// pooled_cover, order_problems, cart_total). Die Saetze sind die Rueckmeldungen im Spiel.
/// </summary>
public static partial class Bewertung
{
    private static PyObjekt Hardware(PyObjekt inhalt) => Objekt(inhalt["hardware"]);

    /// <summary>slot_name</summary>
    public static string PlatzName(object? platz, PyObjekt inhalt) =>
        Py.Str(Get(Objekt(Hardware(inhalt)["typen"]), Py.Str(platz), platz));

    private static string Form(object? wert, PyObjekt inhalt) =>
        Py.Str(Get(Objekt(Hardware(inhalt)["formfaktoren"]), Py.Str(wert), wert));

    /// <summary>psu_needed: (CPU + Grafikkarte + Grundlast) mal Reserve, aufgerundet.</summary>
    public static long NetzteilNoetig(PyObjekt? cpu, PyObjekt? gpu, PyObjekt inhalt)
    {
        var regeln = Objekt(Hardware(inhalt)["regeln"]);
        var last = Py.Plus(Py.Plus(cpu is null ? 0L : cpu["tdp"], gpu is null ? 0L : gpu["watt"]), regeln["grundlast_watt"]);
        return checked((long)Math.Ceiling(Py.F(Py.Mal(last, regeln["netzteil_reserve"])) - 1e-9));
    }

    /// <summary>compatibility_problems: prueft nur, was zusammen vorkommt.</summary>
    public static List<string> Vertraeglichkeit(IReadOnlyDictionary<string, PyObjekt> teile, PyObjekt inhalt)
    {
        PyObjekt? T(string k) => teile.TryGetValue(k, out var t) ? t : null;
        var brett = T("mainboard");
        var cpu = T("cpu");
        var ram = T("ram");
        var ssd = T("ssd");
        var gehaeuse = T("gehaeuse");
        var netzteil = T("netzteil");
        var probleme = new List<string>();
        if (brett is not null && cpu is not null && !Py.Gleich(cpu["sockel"], brett["sockel"]))
        {
            probleme.Add(Prozent("Die CPU (Sockel %s) passt nicht auf das Mainboard (Sockel %s).", cpu["sockel"], brett["sockel"]));
        }
        if (brett is not null && ram is not null)
        {
            if (!Py.Gleich(ram["ram_typ"], brett["ram_typ"]))
            {
                probleme.Add(Prozent("Der Arbeitsspeicher ist %s, das Mainboard braucht %s.", ram["ram_typ"], brett["ram_typ"]));
            }
            else if (Py.Kleiner(brett["ram_slots"], ram["module"]))
            {
                probleme.Add(Prozent("%d RAM-Module, aber das Mainboard hat nur %d Steckplätze.", ram["module"], brett["ram_slots"]));
            }
        }
        if (brett is not null && gehaeuse is not null && !Py.Enthaelt(gehaeuse["formfaktoren"], brett["formfaktor"]))
        {
            probleme.Add(Prozent("Das Mainboard (%s) passt nicht ins Gehäuse (%s).", Form(brett["formfaktor"], inhalt),
                string.Join(", ", Liste(gehaeuse["formfaktoren"]).Select(f => Form(f, inhalt)))));
        }
        if (brett is not null && ssd is not null && Py.Gleich(ssd["anschluss"], "m2") && Py.Kleiner(brett["m2"], 1L))
        {
            probleme.Add("Die SSD braucht einen M.2-Steckplatz, das Mainboard hat keinen (nur SATA).");
        }
        if (netzteil is not null && cpu is not null)
        {
            var noetig = NetzteilNoetig(cpu, T("gpu"), inhalt);
            if (Py.Kleiner(netzteil["watt"], noetig))
            {
                probleme.Add(Prozent("Das Netzteil ist zu schwach: %d W, gebraucht werden mindestens %d W.", netzteil["watt"], noetig));
            }
        }
        return probleme;
    }

    /// <summary>build_problems: alle Probleme eines zusammengebauten PCs; antwort = {steckplatz: bauteil}.</summary>
    public static List<string> BauProbleme(PyObjekt auftrag, PyObjekt? antwort, IReadOnlyList<object?>? verfuegbar, PyObjekt inhalt)
    {
        antwort ??= [];
        var erlaubt = verfuegbar ?? ListeOderLeerGet(auftrag, "teile");
        var optional = ListeOderLeer(auftrag, "optional");
        var gewaehlt = new Dictionary<string, PyObjekt>();
        var reihenfolge = new List<string>();
        var probleme = new List<string>();
        foreach (var platz in Liste(auftrag["slots"]))
        {
            var kennung = Get(antwort, Py.Str(platz));
            var teil = Py.Wahr(kennung) ? Teil(kennung, inhalt) : null;
            if (teil is null || !Py.Gleich(teil["typ"], platz) || !Py.Enthaelt(erlaubt.ToList(), kennung))
            {
                if (!Py.Enthaelt(optional, platz))
                {
                    probleme.Add(PlatzName(platz, inhalt) + " fehlt noch.");
                }
                continue;
            }
            var k = Py.Str(platz);
            if (!gewaehlt.ContainsKey(k))
            {
                reihenfolge.Add(k);
            }
            gewaehlt[k] = teil;
        }
        probleme.AddRange(Vertraeglichkeit(gewaehlt, inhalt));
        var cpu = gewaehlt.GetValueOrDefault("cpu");
        if (cpu is not null && !Py.Wahr(cpu["igpu"]) && !gewaehlt.ContainsKey("gpu"))
        {
            probleme.Add(Prozent("Kein Bild: %s hat keine eingebaute Grafik und es steckt keine Grafikkarte im PC.", cpu["name"]));
        }
        var regeln = Py.Wahr(Get(auftrag, "vorgaben")) ? Objekt(auftrag["vorgaben"]) : [];
        var ram = gewaehlt.GetValueOrDefault("ram");
        if (ram is not null && Py.Kleiner(ram["groesse"], Get(regeln, "ram_min", 0L)))
        {
            probleme.Add(Prozent("Zu wenig Arbeitsspeicher: %d GB, verlangt sind mindestens %d GB.", ram["groesse"], regeln["ram_min"]));
        }
        var ssd = gewaehlt.GetValueOrDefault("ssd");
        if (ssd is not null && Py.Kleiner(ssd["groesse"], Get(regeln, "speicher_min", 0L)))
        {
            probleme.Add(Prozent("Zu wenig Speicherplatz: %d GB, verlangt sind mindestens %d GB.", ssd["groesse"], regeln["speicher_min"]));
        }
        if (Py.Wahr(Get(regeln, "grafikkarte")) && !gewaehlt.ContainsKey("gpu"))
        {
            probleme.Add("Der Auftrag verlangt eine Grafikkarte.");
        }
        var preis = Py.Summe(reihenfolge.Select(k => gewaehlt[k]["preis"]));
        if (Py.Wahr(Get(regeln, "budget")) && Py.Kleiner(regeln["budget"], preis))
        {
            probleme.Add(Prozent("Zu teuer: %d € bei %d € Budget.", preis, regeln["budget"]));
        }
        return probleme;
    }

    private static List<object?> ListeOderLeerGet(PyObjekt d, string k) => Py.Hat(d, k) ? Liste(d[k]) : [];

    /// <summary>spare_features: [(feld, Name, Einheit)].</summary>
    public static List<(object? Feld, object? Name, object? Einheit)> ErsatzMerkmale(object? art, PyObjekt inhalt)
    {
        var merkmale = Get(Hardware(inhalt), "ersatz_merkmale") is PyObjekt m ? m : [];
        return (Get(merkmale, Py.Str(art)) is List<object?> l ? l : [])
            .Select(e => Liste(e)).Select(e => (e[0], e[1], e[2])).ToList();
    }

    /// <summary>labels.get(feld, (feld, "")) aus {feld: (Name, Einheit)}: der letzte Eintrag gilt.</summary>
    private static (object? Name, object? Einheit) Marke(object? art, string feld, PyObjekt inhalt)
    {
        foreach (var m in Enumerable.Reverse(ErsatzMerkmale(art, inhalt)))
        {
            if (Py.Gleich(m.Feld, feld))
            {
                return (m.Name, m.Einheit);
            }
        }
        return (feld, "");
    }

    /// <summary>_feature_value</summary>
    public static string MerkmalWert(object? wert, object? einheit) =>
        Rechner.PythonZeichen.Strip(Prozent("%s %s", wert is double ? Num(wert) : wert, einheit));

    /// <summary>_need_mismatch: warum passt ein Bauteil nicht zu einem Bedarf? "" = passt.</summary>
    public static string BedarfPasstNicht(PyObjekt teil, PyObjekt bedarf, PyObjekt inhalt)
    {
        if (!Py.Gleich(teil["typ"], bedarf["typ"]))
        {
            return "wird nicht gebraucht";
        }
        foreach (var andereKennung in ListeOderLeer(bedarf, "fuer"))
        {
            var andere = Teil(andereKennung, inhalt)!;
            var paar = new Dictionary<string, PyObjekt> { [Py.Str(andere["typ"])] = andere };
            paar[Py.Str(teil["typ"])] = teil;
            var probleme = Vertraeglichkeit(paar, inhalt);
            if (probleme.Count > 0)
            {
                return probleme[0];
            }
        }
        if (Py.Wahr(Get(bedarf, "passt")))
        {
            foreach (var (k, wert) in Objekt(bedarf["passt"]))
            {
                if (!Py.Gleich(Get(teil, k), wert))
                {
                    var (name, einheit) = Marke(teil["typ"], k, inhalt);
                    return Prozent("passt nicht: %s %s statt %s", name, MerkmalWert(Get(teil, k), einheit), MerkmalWert(wert, einheit));
                }
            }
        }
        if (Py.Wahr(Get(bedarf, "min")))
        {
            foreach (var (k, wert) in Objekt(bedarf["min"]))
            {
                if (Zusammen.TryGetValue(Py.Str(bedarf["typ"]), out var zusammen) && zusammen == k)
                {
                    continue;   // zaehlt erst zusammen mit den anderen Teilen (order_problems)
                }
                if (Py.Kleiner(Get(teil, k, 0L), wert))
                {
                    var einheit = Einheiten.GetValueOrDefault(k, "");
                    return Prozent("zu klein: %s %s, gebraucht werden mindestens %s %s", Get(teil, k, 0L), einheit, wert, einheit);
                }
            }
        }
        return "";
    }

    /// <summary>UNITS</summary>
    private static readonly Dictionary<string, string> Einheiten = new() { ["groesse"] = "GB", ["watt"] = "W", ["leistung"] = "W", ["ports"] = "Ports" };

    /// <summary>offer_need: Index des Bedarfs, den ein Angebot deckt, oder null.</summary>
    public static int? AngebotBedarf(PyObjekt angebot, PyObjekt auftrag, PyObjekt inhalt)
    {
        var teil = Teil(angebot["teil"], inhalt)!;
        var bedarfe = Liste(auftrag["bedarf"]);
        for (var i = 0; i < bedarfe.Count; i++)
        {
            if (BedarfPasstNicht(teil, Objekt(bedarfe[i]), inhalt).Length == 0)
            {
                return i;
            }
        }
        return null;
    }

    /// <summary>_need_slots: Steckplaetze im kleinsten Mainboard des Bedarfs.</summary>
    private static long BedarfPlaetze(PyObjekt bedarf, PyObjekt inhalt)
    {
        var plaetze = ListeOderLeer(bedarf, "fuer").Select(k => Teil(k, inhalt))
            .Where(b => b is not null && Py.Gleich(b["typ"], "mainboard")).Select(b => Py.Int(b!["ram_slots"])).ToList();
        return plaetze.Count > 0 ? plaetze.Min() : MaxRamPlaetze(inhalt);
    }

    /// <summary>pooled_cover: wie viele Einheiten eines Bedarfs lassen sich aus den Teilen bilden?</summary>
    public static long ZusammenGedeckt(PyObjekt bedarf, IReadOnlyList<(PyObjekt Teil, long Anzahl)> zeilen, PyObjekt inhalt)
    {
        var k = Zusammen[Py.Str(bedarf["typ"])];
        var ziel = Objekt(bedarf["min"])[k];
        var plaetze = BedarfPlaetze(bedarf, inhalt);
        var menge = Py.Int(bedarf["menge"]);
        var gruppen = new List<long[]>();
        foreach (var zahlen in Produkt(zeilen.Select(z => z.Anzahl + 1).ToArray()))
        {
            var groesse = Py.Summe(zeilen.Select((z, i) => (object?)Py.Mal(z.Teil[k], zahlen[i])));
            var module = Py.Summe(zeilen.Select((z, i) => (object?)Py.Mal(Get(z.Teil, "module", 1L), zahlen[i])));
            if (Py.Kleiner(groesse, ziel) || Py.Kleiner(plaetze, module))
            {
                continue;
            }
            // Nur kleinste Gruppen: ohne irgendein Teil reicht es nicht mehr
            var zuViel = false;
            for (var i = 0; i < zeilen.Count; i++)
            {
                if (zahlen[i] != 0 && !Py.Kleiner(Py.Minus(groesse, zeilen[i].Teil[k]), ziel))
                {
                    zuViel = true;
                    break;
                }
            }
            if (!zuViel)
            {
                gruppen.Add(zahlen);
            }
        }
        var gemerkt = new Dictionary<string, long>();
        long Beste(long[] uebrig)
        {
            var schluessel = string.Join(",", uebrig);
            if (!gemerkt.TryGetValue(schluessel, out var ergebnis))
            {
                ergebnis = 0;
                foreach (var gruppe in gruppen)
                {
                    if (gruppe.Zip(uebrig).All(p => p.First <= p.Second))
                    {
                        var rest = gruppe.Zip(uebrig).Select(p => p.Second - p.First).ToArray();
                        ergebnis = Math.Max(ergebnis, 1 + Beste(rest));
                        if (ergebnis >= menge)
                        {
                            break;
                        }
                    }
                }
                gemerkt[schluessel] = ergebnis;
            }
            return ergebnis;
        }
        return Beste(zeilen.Select(z => z.Anzahl).ToArray());
    }

    /// <summary>itertools.product(range(g0), range(g1), ...): die letzte Stelle laeuft am schnellsten.</summary>
    internal static IEnumerable<long[]> Produkt(long[] grenzen)
    {
        if (grenzen.Any(g => g <= 0))
        {
            yield break;
        }
        var zahlen = new long[grenzen.Length];
        while (true)
        {
            yield return (long[])zahlen.Clone();
            var i = grenzen.Length - 1;
            while (i >= 0 && ++zahlen[i] == grenzen[i])
            {
                zahlen[i] = 0;
                i--;
            }
            if (i < 0)
            {
                yield break;
            }
        }
    }

    private static List<string> ZusammenProbleme(PyObjekt bedarf, List<(PyObjekt Angebot, long Anzahl)> angebote, PyObjekt inhalt)
    {
        var zaehler = new PyDict();
        foreach (var (angebot, anzahl) in angebote)
        {
            zaehler[angebot["teil"]] = Py.Plus(zaehler.Get(angebot["teil"], 0L), anzahl);
        }
        var zeilen = zaehler.Select(e => (Teil: Teil(e.Key, inhalt)!, Anzahl: Py.Int(e.Value))).ToList();
        var menge = Py.Int(bedarf["menge"]);
        var gedeckt = ZusammenGedeckt(bedarf, zeilen, inhalt);
        if (gedeckt < menge)
        {
            return [Prozent("Bedarf nicht gedeckt: %s (%d von %d).", bedarf["text"], gedeckt, menge)];
        }
        for (var i = 0; i < zeilen.Count; i++)
        {
            var weniger = zeilen.Take(i).Concat(zeilen[i].Anzahl > 1 ? [(zeilen[i].Teil, zeilen[i].Anzahl - 1)] : []).Concat(zeilen.Skip(i + 1)).ToList();
            if (ZusammenGedeckt(bedarf, weniger, inhalt) >= menge)
            {
                return [Prozent("Zu viel bestellt: %s. Einmal „%s“ weniger reicht auch.", bedarf["text"], zeilen[i].Teil["name"])];
            }
        }
        return [];
    }

    /// <summary>int(cart.get(id, 0) or 0)</summary>
    private static long Menge(PyObjekt? korb, object? kennung)
    {
        var wert = korb is null ? null : Get(korb, Py.Str(kennung), 0L);
        return Py.Wahr(wert) ? Py.Int(wert) : 0;
    }

    /// <summary>cart_total: Gesamtpreis und laengste Lieferzeit.</summary>
    public static (object Summe, object Laengste) KorbSumme(PyObjekt auftrag, PyObjekt? korb)
    {
        object summe = 0L;
        object laengste = 0L;
        foreach (var angebot in Liste(auftrag["angebote"]).Select(Objekt))
        {
            var anzahl = Menge(korb, angebot["id"]);
            if (anzahl > 0)
            {
                summe = Py.Plus(summe, Py.Mal(angebot["preis"], anzahl));
                laengste = Py.Max(laengste, angebot["lieferzeit"])!;
            }
        }
        return (summe, laengste);
    }

    /// <summary>order_problems: alle Probleme eines Warenkorbs {angebot: menge}.</summary>
    public static List<string> BestellProbleme(PyObjekt auftrag, PyObjekt? korb, PyObjekt inhalt)
    {
        var sauber = new PyObjekt();
        foreach (var (k, v) in korb ?? [])
        {
            if (Py.Wahr(v) && Py.Int(v) > 0)
            {
                Py.Setzen(sauber, k, Py.Int(v));
            }
        }
        if (sauber.Count == 0)
        {
            return ["Der Warenkorb ist leer."];
        }
        var probleme = new List<string>();
        var bedarfe = Liste(auftrag["bedarf"]).Select(Objekt).ToList();
        var gedeckt = new long[bedarfe.Count];
        var zusammen = new SortedDictionary<int, List<(PyObjekt, long)>>();
        var zusammenReihe = new List<int>();
        foreach (var angebot in Liste(auftrag["angebote"]).Select(Objekt))
        {
            var anzahl = Py.Int(Get(sauber, Py.Str(angebot["id"]), 0L));
            if (anzahl == 0)
            {
                continue;
            }
            var index = AngebotBedarf(angebot, auftrag, inhalt);
            if (index is null)
            {
                var teil = Teil(angebot["teil"], inhalt)!;
                var gruende = bedarfe.Where(b => Py.Gleich(b["typ"], teil["typ"])).Select(b => BedarfPasstNicht(teil, b, inhalt)).ToList();
                probleme.Add(Prozent("%s passt nicht: %s", teil["name"], gruende.Count > 0 ? gruende[0] : "wird nicht gebraucht"));
            }
            else
            {
                var i = index.Value;
                gedeckt[i] += anzahl;
                if (Zusammen.TryGetValue(Py.Str(bedarfe[i]["typ"]), out var k)
                    && Py.Wahr(Get(bedarfe[i], "min")) && Py.Hat(Objekt(bedarfe[i]["min"]), k))
                {
                    if (!zusammen.ContainsKey(i))
                    {
                        zusammen[i] = [];
                    }
                    zusammen[i].Add((angebot, anzahl));
                }
            }
        }
        for (var i = 0; i < bedarfe.Count; i++)
        {
            if (zusammen.TryGetValue(i, out var angebote))
            {
                probleme.AddRange(ZusammenProbleme(bedarfe[i], angebote, inhalt));
                gedeckt[i] = Py.Int(bedarfe[i]["menge"]);
            }
        }
        for (var i = 0; i < bedarfe.Count; i++)
        {
            var menge = Py.Int(bedarfe[i]["menge"]);
            if (gedeckt[i] < menge)
            {
                probleme.Add(Prozent("Bedarf nicht gedeckt: %s (%d von %d).", bedarfe[i]["text"], gedeckt[i], menge));
            }
            else if (gedeckt[i] > menge)
            {
                probleme.Add(Prozent("Zu viel bestellt: %s (%d statt %d).", bedarfe[i]["text"], gedeckt[i], menge));
            }
        }
        var (summe, laengste) = KorbSumme(auftrag, sauber);
        if (Py.Kleiner(auftrag["budget"], summe))
        {
            probleme.Add(Prozent("Budget überschritten: %d € bei %d € Budget.", summe, auftrag["budget"]));
        }
        if (Py.Kleiner(auftrag["frist"], laengste))
        {
            probleme.Add(Prozent("Zu spät: Die Ware braucht %s, die Frist ist %d.", Plural(laengste, "Arbeitstag", "Arbeitstage"), auftrag["frist"]));
        }
        return probleme;
    }
}
