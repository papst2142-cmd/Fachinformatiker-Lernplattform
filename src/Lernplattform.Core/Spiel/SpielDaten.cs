using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Nachschlagen in den Spielinhalten wie <c>fisi_game.py</c> (Python 0.63.2):
/// Auftraege (task_by_id, wrong_delivery_task, is_incident, task_site), Personen (colleague,
/// all_people, active_person, person_present) und Hardware (part, rack_device, stock_combos).
/// <paramref name="inhalt"/> ist immer das Ergebnis von <see cref="SpielInhalt.Laden(Func{string, byte[]?}, object?)"/>.
/// </summary>
public static class SpielDaten
{
    public const string Falschlieferung = "falschlieferung:";   // WRONG_DELIVERY
    public const string Grundbestand = "grundbestand";          // STOCK_BASE
    public const string OrtBuero = "buero";                      // SITE_OFFICE
    public const string OrtZuhause = "zuhause";                  // SITE_HOME
    public const string OrtFiliale = "filiale";                  // SITE_BRANCH
    public const string Kombi = "*";                             // COMBO_MARK

    /// <summary>POOLED_NEEDS: Bedarfe, die sich aus mehreren Teilen zusammensetzen.</summary>
    public static readonly IReadOnlyDictionary<string, string> Zusammen = new Dictionary<string, string> { ["ram"] = "groesse" };

    public static List<object?> Liste(object? wert) => wert as List<object?> ?? throw new InvalidDataException("Liste erwartet");

    public static PyObjekt Objekt(object? wert) => wert as PyObjekt ?? throw new InvalidDataException("dict erwartet");

    /// <summary>d.get(k, standard) fuer ein dict aus JSON.</summary>
    public static object? Get(PyObjekt d, string k, object? standard = null) => Py.Hat(d, k) ? d[k] : standard;

    /// <summary>d.get(k) or [] als Liste.</summary>
    public static List<object?> ListeOderLeer(PyObjekt d, string k) => Py.Wahr(d[k]) ? Liste(d[k]) : [];

    /// <summary>task_by_id</summary>
    public static PyObjekt? Auftrag(object? kennung, PyObjekt inhalt)
    {
        if (kennung is string s && s.Length > 0 && s.StartsWith(Falschlieferung, StringComparison.Ordinal))
        {
            return FalschlieferungAuftrag(s[Falschlieferung.Length..], inhalt);
        }
        foreach (var a in Liste(inhalt["aufgaben"]).Concat(Py.Hat(inhalt, "zwischenfaelle") ? Liste(inhalt["zwischenfaelle"]) : []))
        {
            var auftrag = Objekt(a);
            if (Py.Gleich(auftrag["id"], kennung))
            {
                return auftrag;
            }
        }
        return null;
    }

    /// <summary>wrong_delivery_task: Zwischenfall "Falsche Ware" nach einer falschen Bestellung.</summary>
    public static PyObjekt? FalschlieferungAuftrag(string bestellung, PyObjekt inhalt, string ware = "")
    {
        var auftrag = Liste(inhalt["aufgaben"]).Select(Objekt).FirstOrDefault(a => Py.Gleich(a["id"], bestellung));
        var hardware = Objekt(inhalt["hardware"]);
        if (auftrag is null || hardware["falschlieferung"] is not PyObjekt vorlage)
        {
            return null;
        }
        var namen = new PyObjekt { { "bestellung", auftrag["titel"] }, { "ware", ware.Length > 0 ? ware : "die bestellte Ware" } };
        var neu = new PyObjekt();
        neu.AddRange(vorlage.Where(p => !p.Key.StartsWith('_')));
        Py.Setzen(neu, "id", Falschlieferung + bestellung);
        Py.Setzen(neu, "typ", "auswahl");
        Py.Setzen(neu, "zwischenfall", true);
        Py.Setzen(neu, "falschlieferung", bestellung);
        Py.Setzen(neu, "titel", Py.Format((string)vorlage["titel"]!, namen));
        Py.Setzen(neu, "ticket", Py.Format((string)SpielInhalt.Zeilen(vorlage["ticket"])!, namen));
        Py.Setzen(neu, "hilfe", SpielInhalt.Zeilen(vorlage["hilfe"]));
        Py.Setzen(neu, "erklaerung", SpielInhalt.Zeilen(vorlage["erklaerung"]));
        return neu;
    }

    /// <summary>is_incident</summary>
    public static bool IstZwischenfall(object? kennung, PyObjekt inhalt) =>
        Auftrag(kennung, inhalt) is { } a && Py.Wahr(a["zwischenfall"]);

    /// <summary>customer_places</summary>
    public static List<PyObjekt> Kundenorte(PyObjekt inhalt) =>
        inhalt["kunden"] is PyObjekt k && Py.Hat(k, "orte") ? Liste(k["orte"]).Select(Objekt).ToList() : [];

    /// <summary>customer_people: Personen beim Kunden mit "ort".</summary>
    public static List<PyObjekt> KundenPersonen(PyObjekt inhalt)
    {
        var ergebnis = new List<PyObjekt>();
        foreach (var ort in Kundenorte(inhalt))
        {
            foreach (var p in Py.Hat(ort, "personen") ? Liste(ort["personen"]) : [])
            {
                var neu = Py.Flach(Objekt(p));
                Py.Setzen(neu, "ort", ort["id"]);
                ergebnis.Add(neu);
            }
        }
        return ergebnis;
    }

    /// <summary>all_people</summary>
    public static List<PyObjekt> AllePersonen(PyObjekt inhalt) =>
        Liste(inhalt["kollegen"]).Select(Objekt).Concat(KundenPersonen(inhalt)).ToList();

    /// <summary>colleague</summary>
    public static PyObjekt? Person(object? kennung, PyObjekt inhalt) =>
        AllePersonen(inhalt).FirstOrDefault(p => Py.Gleich(p["id"], kennung));

    /// <summary>active_person: die Person selbst oder nach ihrem letzten Arbeitstag ihre Nachfolge.</summary>
    public static object? AktivePerson(object? kennung, long tag, PyObjekt inhalt)
    {
        var gesehen = new PySet();
        while (!gesehen.Contains(kennung))
        {
            gesehen.Add(kennung);
            var person = Person(kennung, inhalt);
            if (person is null || person["bis_tag"] is null || Py.F(tag) <= Py.F(person["bis_tag"]) || !Py.Wahr(person["nachfolger"]))
            {
                return kennung;
            }
            kennung = person["nachfolger"];
        }
        return kennung;
    }

    /// <summary>person_present</summary>
    public static bool PersonDa(PyObjekt person, long tag) =>
        Py.F(Get(person, "ab_tag", 1L)) <= tag && (person["bis_tag"] is null || tag <= Py.F(person["bis_tag"]));

    /// <summary>task_site: "ort" des Auftrags oder der Ort der Person, die ihn stellt.</summary>
    public static object? AuftragOrt(PyObjekt auftrag, PyObjekt inhalt)
    {
        if (Py.Wahr(auftrag["ort"]))
        {
            return auftrag["ort"];
        }
        var person = Person(auftrag["auftraggeber"], inhalt);
        return person is not null && Py.Hat(person, "ort") ? person["ort"] : OrtBuero;
    }

    private static IEnumerable<PyObjekt> TeileUndErsatz(PyObjekt inhalt)
    {
        var hardware = Objekt(inhalt["hardware"]);
        return Liste(hardware["teile"]).Concat(Py.Hat(hardware, "ersatzteile") ? Liste(hardware["ersatzteile"]) : []).Select(Objekt);
    }

    /// <summary>combo_id</summary>
    public static string KombiKennung(string teil, long anzahl) => anzahl == 1 ? teil : teil + Kombi + anzahl;

    /// <summary>split_combo: (Grund-Id, Anzahl).</summary>
    public static (object? Grund, long Anzahl) KombiTeilen(object? kennung)
    {
        var text = Py.Str(kennung);
        var stelle = text.LastIndexOf(Kombi, StringComparison.Ordinal);
        if (stelle > 0)
        {
            var zahl = text[(stelle + 1)..];
            if (zahl.Length > 0 && zahl.All(char.IsAsciiDigit) && long.TryParse(zahl, out var n) && n >= 2)
            {
                return (text[..stelle], n);
            }
        }
        return (kennung, 1);
    }

    /// <summary>part: Bauteil (PC) oder Rack-Geraet; mehrere gleiche RAM-Teile gelten als ein Teil.</summary>
    public static PyObjekt? Teil(object? kennung, PyObjekt inhalt)
    {
        var (grund, anzahl) = KombiTeilen(kennung);
        foreach (var t in TeileUndErsatz(inhalt))
        {
            if (Py.Gleich(t["id"], grund))
            {
                if (anzahl == 1)
                {
                    return t;
                }
                return Zusammen.ContainsKey((string)t["typ"]!) ? KombiTeil(t, anzahl) : null;
            }
        }
        return anzahl == 1 ? RackGeraet(kennung, inhalt) : null;
    }

    /// <summary>_combo_part</summary>
    private static PyObjekt KombiTeil(PyObjekt t, long anzahl)
    {
        var groesse = Py.Int(t["groesse"]);
        var module = Py.Int(t["module"]);
        var jeModul = Math.DivRem(groesse, module).Quotient;
        if ((groesse % module != 0) && ((groesse < 0) ^ (module < 0)))
        {
            jeModul--;   // // rundet in Python nach unten
        }
        var text = module == 1
            ? anzahl + " × " + jeModul + " GB, Einzelmodule"
            : anzahl + " Kits mit je " + module + " × " + jeModul + " GB";
        var neu = Py.Flach(t);
        Py.Setzen(neu, "id", KombiKennung((string)t["id"]!, anzahl));
        Py.Setzen(neu, "preis", Py.Mal(t["preis"], anzahl));
        Py.Setzen(neu, "groesse", groesse * anzahl);
        Py.Setzen(neu, "module", module * anzahl);
        Py.Setzen(neu, "name", Py.Str(t["ram_typ"]) + " " + (groesse * anzahl) + " GB (" + text + ")");
        return neu;
    }

    /// <summary>rack_device</summary>
    public static PyObjekt? RackGeraet(object? kennung, PyObjekt inhalt)
    {
        var hardware = Objekt(inhalt["hardware"]);
        return Py.Hat(hardware, "rack_geraete")
            ? Liste(hardware["rack_geraete"]).Select(Objekt).FirstOrDefault(g => Py.Gleich(g["id"], kennung))
            : null;
    }

    /// <summary>max_ram_slots</summary>
    public static long MaxRamPlaetze(PyObjekt inhalt)
    {
        var bretter = Liste(Objekt(inhalt["hardware"])["teile"]).Select(Objekt).Where(t => Py.Gleich(t["typ"], "mainboard")).Select(t => Py.Int(t["ram_slots"])).ToList();
        return bretter.Count > 0 ? bretter.Max() : 4;
    }

    /// <summary>stock_combos: zusaetzliche Ids fuer mehrere gleiche RAM-Teile aus dem Lager.</summary>
    public static List<string> LagerKombis(PyDict lager, IReadOnlyCollection<object?> erlaubt, PyObjekt inhalt)
    {
        var ergebnis = new List<string>();
        foreach (var (kennung, anzahl) in lager)
        {
            var t = Teil(kennung, inhalt);
            if (t is null || !erlaubt.Any(e => Py.Gleich(e, t["typ"])) || !Zusammen.ContainsKey((string)t["typ"]!))
            {
                continue;
            }
            for (long n = 2; n <= Py.Int(anzahl); n++)
            {
                if (n * Py.Int(t["module"]) > MaxRamPlaetze(inhalt))
                {
                    break;
                }
                ergebnis.Add(KombiKennung((string)kennung!, n));
            }
        }
        return ergebnis;
    }
}
