using Lernplattform.Core.Daten;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Suche;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Formulare mit Rechenweg wie <c>fisi_game.py</c> (Python 0.63.2, Z. 1834-2212:
/// ip_plan, offer_values, leasing_values, form_fields, printer_values, parse_number,
/// parse_prefix, parse_ip, field_ok, form_problems). IP-Adressen ueber <see cref="PythonIp"/>
/// (Nachbau von ipaddress), RAID wie <c>fisi_core.raid_values</c> mit int/float wie Python.
/// </summary>
public static partial class Bewertung
{
    /// <summary>FormError: ungueltige Formular-Aufgabe.</summary>
    public sealed class FormFehler(string text) : Exception(text);

    /// <summary>Ein Formularfeld mit Sollwert; Art ip, praefix, zahl, geld, prozent, wahl.</summary>
    public sealed record FormFeld(string Id, object? Gruppe, string Label, string Einheit, string Art, object? Soll, string Anzeige, List<object?>? Optionen);

    private static readonly Dictionary<string, string> IpFelder = new()
    {
        ["netz"] = "Netzadresse", ["praefix"] = "Präfix", ["maske"] = "Subnetzmaske", ["erste"] = "Erste Host-Adresse",
        ["letzte"] = "Letzte Host-Adresse", ["broadcast"] = "Broadcast", ["hosts"] = "Nutzbare Hosts",
    };

    private static readonly Dictionary<string, string> IpArt = new()
    {
        ["netz"] = "ip", ["erste"] = "ip", ["letzte"] = "ip", ["broadcast"] = "ip", ["praefix"] = "praefix", ["maske"] = "praefix", ["hosts"] = "zahl",
    };

    private static IpNetz V4Netz(string text, string fehler)
    {
        var netz = PythonIp.Netz(text) ?? throw new FormFehler(fehler);
        return netz.Version == 4 ? netz : throw new FormFehler("nur IPv4");
    }

    private static string V4(BigInteger wert) => PythonIp.V4Text(wert);

    /// <summary>fisi_core.ipv4_values ohne wildcard/adressen: Adressen als Text, hosts und praefix als Zahl.</summary>
    private static Dictionary<string, object> Ipv4Werte(IpNetz netz)
    {
        var p = netz.Praefix;
        var hosts = p < 31 ? (long)(netz.Anzahl - 2) : (p == 31 ? 2 : 1);
        var erste = p < 31 ? netz.Netzadresse.Wert + 1 : netz.Netzadresse.Wert;
        var letzte = p < 31 ? netz.Letzte - 1 : netz.Letzte;
        return new()
        {
            ["netz"] = V4(netz.Netzadresse.Wert), ["maske"] = V4(netz.Maske), ["broadcast"] = V4(netz.Letzte),
            ["erste"] = V4(erste), ["letzte"] = V4(letzte), ["hosts"] = hosts, ["praefix"] = (long)p,
        };
    }

    private static int PraefixFuerHosts(object? hosts)
    {
        var p = 32;
        while (Py.Kleiner((BigInteger.One << (32 - p)) - 2 is var n ? (double)n : 0, hosts))
        {
            p--;
        }
        return p;
    }

    /// <summary>ip_plan: Teilnetze in der Reihenfolge der Aufgabe (ohne VLSM gleich gross, mit VLSM groesstes zuerst).</summary>
    public static List<(object? Name, IpNetz Netz)> IpPlan(PyObjekt auftrag)
    {
        var daten = Objekt(auftrag["daten"]);
        var basis = V4Netz(Py.Str(daten["netz"]), "ungueltiges Netz: " + Py.Str(daten["netz"]));
        var netze = Liste(daten["teilnetze"]).Select(Objekt).ToList();
        if (Py.Wahr(Get(daten, "vlsm")))
        {
            var ordnung = Enumerable.Range(0, netze.Count).OrderBy(i => -Py.F(netze[i]["hosts"])).ThenBy(i => i).ToList();
            var ergebnis = new (object?, IpNetz)[netze.Count];
            var start = basis.Netzadresse.Wert;
            foreach (var i in ordnung)
            {
                var p = PraefixFuerHosts(netze[i]["hosts"]);
                if (p < basis.Praefix)
                {
                    throw new FormFehler(Prozent("Teilnetz %s passt nicht ins Netz", netze[i]["name"]));
                }
                var netz = PythonIp.Netz(V4(start) + "/" + p) ?? throw new InvalidDataException("ValueError: Hostanteil gesetzt");
                if (netz.Netzadresse.Wert < basis.Netzadresse.Wert || netz.Letzte > basis.Letzte)
                {
                    throw new FormFehler("Netz reicht nicht fuer alle Teilnetze");
                }
                ergebnis[i] = (netze[i]["name"], netz);
                start += netz.Anzahl;
            }
            return [.. ergebnis];
        }
        var bits = Math.Max(0, (int)Math.Ceiling(Math.Log(netze.Count) / Math.Log(2) - 1e-9));
        var neu = basis.Praefix + bits;
        if (neu > 32)
        {
            throw new InvalidDataException("ValueError: prefix length diff too large");
        }
        var plan = new List<(object?, IpNetz)>();
        var schritt = BigInteger.One << (32 - neu);
        for (var i = 0; i < netze.Count && i < (1 << bits); i++)
        {
            plan.Add((netze[i]["name"], new IpNetz(new IpAdresse(4, basis.Netzadresse.Wert + (schritt * i)), neu)));
        }
        foreach (var (netz, (_, teil)) in netze.Zip(plan))
        {
            if (Py.Kleiner(Ipv4Werte(teil)["hosts"], Get(netz, "hosts", 0L)))
            {
                throw new FormFehler(Prozent("Teilnetz %s hat zu wenige Hosts", netz["name"]));
            }
        }
        return plan;
    }

    /// <summary>_money: round(wert + 1e-9, 2).</summary>
    private static double Geld(object wert) => PythonRundenStellen.Runde(Py.F(wert) + 1e-9, 2);

    private static object Summe(IEnumerable<object> werte) => Py.Summe(werte);

    /// <summary>offer_values: Zuschlagskalkulation (schluessel, name, wert).</summary>
    public static List<(string Id, string Name, object Wert)> AngebotWerte(PyObjekt daten)
    {
        var einkauf = Py.Wahr(Get(daten, "positionen"))
            ? Geld(Summe(Liste(daten["positionen"]).Select(Objekt).Select(p => Py.Mal(p["menge"], p["preis"]))))
            : Geld(Py.Mal(daten["menge"], daten["einkaufspreis"]));
        var kosten = Geld(einkauf * Py.F(daten["handlungskosten"]) / 100.0);
        var selbst = Geld(einkauf + kosten);
        var gewinn = Geld(selbst * Py.F(daten["gewinn"]) / 100.0);
        var netto = Geld(selbst + gewinn);
        var steuer = Geld(netto * Py.F(daten["ust"]) / 100.0);
        var name = Get(daten, "einkauf_name");
        return
        [
            ("einkauf", Py.Wahr(name) ? Py.Str(name) : "Einkaufspreis gesamt", einkauf),
            ("handlungskosten", Prozent("Handlungskosten (%s %%)", Num(daten["handlungskosten"])), kosten),
            ("selbstkosten", "Selbstkosten", selbst),
            ("gewinn", Prozent("Gewinn (%s %%)", Num(daten["gewinn"])), gewinn),
            ("netto", "Nettoverkaufspreis", netto),
            ("ust", Prozent("Umsatzsteuer (%s %%)", Num(daten["ust"])), steuer),
            ("brutto", "Bruttoverkaufspreis", Geld(netto + steuer)),
        ];
    }

    /// <summary>leasing_values: Kauf gegen Leasing.</summary>
    public static List<(string Id, string Name, object Wert)> LeasingWerte(PyObjekt daten)
    {
        var leasing = Geld(Py.Plus(Py.Plus(Get(daten, "sonderzahlung", 0L), Py.Mal(daten["rate"], daten["laufzeit"])), Get(daten, "restwert", 0L)));
        var kauf = Geld(daten["kaufpreis"]!);
        return
        [
            ("leasing", "Gesamtkosten Leasing", leasing),
            ("kauf", "Gesamtkosten Kauf", kauf),
            ("differenz", "Unterschied", Geld(Math.Abs(leasing - kauf))),
            ("guenstiger", "Günstiger ist", kauf <= leasing ? "Kauf" : "Leasing"),
        ];
    }

    /// <summary>fisi_core.raid_values mit int/float wie Python, oder null.</summary>
    private static Dictionary<string, object>? RaidWerte(object? level, object? platten, object? groesse)
    {
        var n = platten!;
        var s = groesse!;
        var (minimum, netto, toleranz) = Py.Str(level) switch
        {
            "RAID 0" => (1L, (Func<object>)(() => Py.Mal(n, s)), (Func<object>)(() => 0L)),
            "RAID 1" => (2L, () => s, () => Py.Minus(n, 1L)),
            "RAID 5" => (3L, () => Py.Mal(Py.Minus(n, 1L), s), () => 1L),
            "RAID 6" => (4L, () => Py.Mal(Py.Minus(n, 2L), s), () => 2L),
            "RAID 10" => (4L, () => Py.Mal(Py.F(n) / 2, s), () => 1L),
            _ => throw new KeyNotFoundException("RAID_RULES: " + Py.Str(level)),
        };
        if (Py.Kleiner(n, minimum) || (Py.Str(level) == "RAID 10" && Py.Int(n) % 2 != 0))
        {
            return null;
        }
        var nt = netto();
        var brutto = Py.Mal(n, s);
        return new()
        {
            ["brutto"] = brutto, ["netto"] = nt, ["verlust"] = Py.Minus(brutto, nt),
            ["effizienz"] = Py.Wahr(brutto) ? Py.F(nt) / Py.F(brutto) * 100 : 0L, ["toleranz"] = toleranz(),
        };
    }

    /// <summary>printer_values: erste freie Adresse im Druckerbereich, Maske, Gateway (erste Host-Adresse).</summary>
    private static Dictionary<string, object> DruckerWerte(PyObjekt daten)
    {
        var netz = V4Netz(Py.Str(daten["netz"]), "ValueError: " + Py.Str(daten["netz"]));
        var bereich = Liste(daten["bereich"]);
        var (von, bis) = (Py.Int(bereich[0]), Py.Int(bereich[1]));
        var belegt = new PySet(ListeOderLeer(daten, "belegt"));
        var frei = new List<long>();
        for (var z = von; z <= bis; z++)
        {
            if (!belegt.Contains(z))
            {
                frei.Add(z);
            }
        }
        if (frei.Count == 0)
        {
            throw new FormFehler("im Druckerbereich ist keine Adresse frei");
        }
        var adresse = netz.Netzadresse.Wert + frei[0];
        if (adresse < 0 || adresse > uint.MaxValue)
        {
            throw new InvalidDataException("AddressValueError");
        }
        if (adresse < netz.Netzadresse.Wert || adresse > netz.Letzte || adresse == netz.Letzte || frei[0] < 1)
        {
            throw new FormFehler("Druckerbereich liegt nicht im Netz");
        }
        var tor = netz.Praefix < 31 ? netz.Netzadresse.Wert + 1 : netz.Netzadresse.Wert;
        if (adresse == tor)
        {
            throw new FormFehler("Druckerbereich ueberschneidet das Gateway");
        }
        if (!Py.Enthaelt(daten["treiber"], daten["treiber_richtig"]))
        {
            throw new FormFehler("richtiger Treiber steht nicht in der Auswahl");
        }
        if (Py.Wahr(Get(daten, "freigaben")) && !Py.Enthaelt(daten["freigaben"], Get(daten, "freigabe_richtig")))
        {
            throw new FormFehler("richtige Freigabe steht nicht in der Auswahl");
        }
        return new() { ["ip"] = V4(adresse), ["praefix"] = (long)netz.Praefix, ["maske"] = V4(netz.Maske), ["gateway"] = V4(tor) };
    }

    /// <summary>form_fields: die Felder eines Formulars mit Sollwert.</summary>
    public static List<FormFeld> FormFelder(PyObjekt auftrag)
    {
        var art = Get(auftrag, "art");
        var daten = Py.Wahr(Get(auftrag, "daten")) ? Objekt(auftrag["daten"]) : [];
        var felder = new List<FormFeld>();
        void Neu(string id, string label, string feldArt, object? soll, object? gruppe = null, string einheit = "", string? anzeige = null, List<object?>? optionen = null) =>
            felder.Add(new FormFeld(id, gruppe, label, einheit, feldArt, soll, string.IsNullOrEmpty(anzeige) ? Py.Str(soll) : anzeige, optionen));

        switch (art)
        {
            case "ip_plan":
                var plan = IpPlan(auftrag);
                var erstes = Ipv4Werte(plan[0].Netz);
                foreach (var k in ListeOderLeer(auftrag, "felder_global").Select(Py.Str))
                {
                    if (!IpFelder.ContainsKey(k))
                    {
                        throw new FormFehler("unbekanntes Feld '" + k + "'");
                    }
                    if (IpArt[k] == "ip")
                    {
                        // Python laesst hier das IPv4Address-Objekt stehen; das kommt im Spiel nicht vor
                        throw new NotSupportedException("felder_global mit IP-Feld '" + k + "' nicht nachgebaut");
                    }
                    var soll = erstes[k];
                    var anzeige = k == "praefix" ? Prozent("/%d", soll) : Py.Str(soll);
                    Neu(k, IpFelder[k] + " je Teilnetz", IpArt[k], IpArt[k] == "praefix" ? erstes["praefix"] : soll, anzeige: anzeige);
                }
                for (var nummer = 0; nummer < plan.Count; nummer++)
                {
                    var werte = Ipv4Werte(plan[nummer].Netz);
                    foreach (var k in ListeOderLeer(auftrag, "felder").Select(Py.Str))
                    {
                        if (!IpFelder.ContainsKey(k))
                        {
                            throw new FormFehler("unbekanntes Feld '" + k + "'");
                        }
                        var soll = werte[k];
                        var anzeige = k == "praefix" ? Prozent("/%d", soll) : Py.Str(soll);
                        if (k == "maske")
                        {
                            (soll, anzeige) = (werte["praefix"], Py.Str(werte["maske"]));
                        }
                        Neu(nummer.ToString(CultureInfo.InvariantCulture) + "." + k, IpFelder[k], IpArt[k], IpArt[k] != "ip" ? soll : Py.Str(soll), plan[nummer].Name, anzeige: anzeige);
                    }
                }
                break;
            case "raid":
                var raid = RaidWerte(daten["level"], daten["platten"], daten["groesse"]) ?? throw new FormFehler("RAID-Level passt nicht zur Plattenzahl");
                var einheit = Py.Str(Get(daten, "einheit", "TB"));
                Neu("brutto", "Bruttokapazität", "zahl", raid["brutto"], einheit: einheit, anzeige: Prozent("%s %s", Num(raid["brutto"]), einheit));
                Neu("netto", "Nutzkapazität", "zahl", raid["netto"], einheit: einheit, anzeige: Prozent("%s %s", Num(raid["netto"]), einheit));
                Neu("verlust", "Verlust durch Redundanz", "zahl", raid["verlust"], einheit: einheit, anzeige: Prozent("%s %s", Num(raid["verlust"]), einheit));
                Neu("toleranz", "Ausfalltoleranz", "zahl", raid["toleranz"], einheit: "Platten",
                    anzeige: Prozent("%d Platte%s", raid["toleranz"], Py.Gleich(raid["toleranz"], 1L) ? "" : "n"));
                if (Py.Enthaelt(ListeOderLeer(auftrag, "felder"), "effizienz"))
                {
                    object eff = raid["effizienz"] is double e ? PythonRundenStellen.Runde(e, 1) : raid["effizienz"];
                    Neu("effizienz", "Speichereffizienz", "prozent", eff, einheit: "%", anzeige: Prozent("%s %%", Num(eff)));
                }
                break;
            case "angebot" or "leasing":
                var zeilen = Py.Gleich(art, "angebot") ? AngebotWerte(daten) : LeasingWerte(daten);
                var gewuenscht = Py.Wahr(Get(auftrag, "felder")) ? Liste(auftrag["felder"]) : zeilen.Select(z => (object?)z.Id).ToList();
                foreach (var (id, label, wert) in zeilen)
                {
                    if (!Py.Enthaelt(gewuenscht, id))
                    {
                        continue;
                    }
                    if (wert is string)
                    {
                        Neu(id, label, "wahl", wert, optionen: ["Kauf", "Leasing"]);
                    }
                    else
                    {
                        Neu(id, label, "geld", wert, einheit: "€", anzeige: Euro(wert));
                    }
                }
                break;
            case "drucker":
                var drucker = DruckerWerte(daten);
                Neu("ip", "IP-Adresse des Druckers", "ip", drucker["ip"]);
                Neu("maske", "Subnetzmaske", "praefix", drucker["praefix"], anzeige: (string)drucker["maske"]);
                Neu("gateway", "Standardgateway", "ip", drucker["gateway"]);
                if (Py.Gleich(Get(daten, "dns"), "gateway"))
                {
                    Neu("dns", "DNS-Server", "ip", drucker["gateway"]);
                }
                Neu("treiber", "Treiber", "wahl", daten["treiber_richtig"], optionen: [.. Liste(daten["treiber"])]);
                if (Py.Wahr(Get(daten, "freigaben")))
                {
                    Neu("freigabe", "Freigabename", "wahl", daten["freigabe_richtig"], optionen: [.. Liste(daten["freigaben"])]);
                }
                break;
            default:
                throw new FormFehler("unbekannte Formular-Art '" + Py.Str(art) + "'");
        }
        return felder;
    }

    private static string Text(object? wert) => wert switch
    {
        null => "",
        string s => s,
        _ when !Py.Wahr(wert) => "",
        _ => throw new NotSupportedException("Formular-Eingabe ist kein Text: " + wert.GetType().Name),
    };

    // re.I bei Python: "s" passt auch auf das lange s "ſ" (U+017F), sonst nur ASCII gross/klein (mit python3.12 -I fuer alle Buchstaben des Musters ermittelt)
    private static readonly Regex Endung = new(@"(€|EUR|TB|GB|Platten?|%|Ho[sſ]t[sſ]?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Tausenderpunkt = new(@"\A-?[1-9]\d{0,2}(\.\d{3})+\z", RegexOptions.CultureInvariant);

    /// <summary>parse_number: "1.234,50 €", "16 TB", "66,7 %", "12000"; null, wenn es keine Zahl ist.</summary>
    public static double? ZahlLesen(object? eingabe)
    {
        var text = PythonZeichen.Strip(Text(eingabe)).Replace(" ", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        text = PythonZeichen.Strip(Endung.Replace(text, "", 1));
        if (text.Length == 0)
        {
            return null;
        }
        if (text.Contains(',', StringComparison.Ordinal) && text.Contains('.', StringComparison.Ordinal))
        {
            text = text.Replace(".", "", StringComparison.Ordinal).Replace(",", ".", StringComparison.Ordinal);
        }
        else if (text.Contains(',', StringComparison.Ordinal))
        {
            text = text.Replace(",", ".", StringComparison.Ordinal);
        }
        else if (Tausenderpunkt.IsMatch(text))
        {
            // Tausenderpunkt nur ohne fuehrende Null - "0.125" ist 0,125
            text = text.Replace(".", "", StringComparison.Ordinal);
        }
        return Float(text);
    }

    /// <summary>parse_prefix: "/26", "26" oder "255.255.255.192"; null bei Unsinn.</summary>
    public static long? PraefixLesen(object? eingabe)
    {
        var text = PythonZeichen.Strip(Text(eingabe)).Replace(" ", "", StringComparison.Ordinal);
        if (text.Contains('.', StringComparison.Ordinal))
        {
            if (PythonIp.Adresse(text) is not { Version: 4 } ip || text.Contains('%', StringComparison.Ordinal))
            {
                return null;
            }
            var bits = Convert.ToString((long)ip.Wert, 2).PadLeft(32, '0');
            return bits.Contains("01", StringComparison.Ordinal) ? null : bits.Count(c => c == '1');
        }
        text = text.TrimStart('/');
        if (text.Length == 0 || !text.All(c => char.GetUnicodeCategory(c) == UnicodeCategory.DecimalDigitNumber))
        {
            return null;
        }
        var wert = BigInteger.Parse(string.Concat(text.Select(c => (char)('0' + (int)char.GetNumericValue(c)))), CultureInfo.InvariantCulture);
        return wert <= 32 ? (long)wert : null;
    }

    /// <summary>parse_ip: IPv4-Adresse als Text (ohne "/..."), sonst null.</summary>
    public static string? IpLesen(object? eingabe)
    {
        var text = PythonZeichen.Strip(Text(eingabe)).Replace(" ", "", StringComparison.Ordinal).Split('/')[0];
        return PythonIp.Adresse(text) is { Version: 4 } ip && !text.Contains('%', StringComparison.Ordinal) ? V4(ip.Wert) : null;
    }

    /// <summary>field_ok: stimmt die Eingabe fuer ein Feld?</summary>
    public static bool FeldRichtig(FormFeld feld, object? eingabe)
    {
        switch (feld.Art)
        {
            case "ip":
                return IpLesen(eingabe) is string ip && Py.Gleich(ip, feld.Soll);
            case "praefix":
                return PraefixLesen(eingabe) is long p && Py.Gleich(p, feld.Soll);
            case "wahl":
                return PythonKlein.Klein(PythonZeichen.Strip(Text(eingabe))) == PythonKlein.Klein(Py.Str(feld.Soll));
        }
        if (ZahlLesen(eingabe) is not double wert)
        {
            return false;
        }
        var toleranz = feld.Art switch { "geld" => 0.011, "prozent" => 0.1, _ => 0.01 };
        return Math.Abs(wert - Py.F(feld.Soll)) <= toleranz;
    }

    /// <summary>form_problems: falsche Felder als Saetze mit dem richtigen Wert.</summary>
    public static List<string> FormProbleme(PyObjekt auftrag, PyObjekt? antwort)
    {
        antwort ??= [];
        var probleme = new List<string>();
        foreach (var feld in FormFelder(auftrag))
        {
            var eingabe = Get(antwort, feld.Id);
            if (FeldRichtig(feld, eingabe))
            {
                continue;
            }
            var label = !Py.Wahr(feld.Gruppe) ? feld.Label : Prozent("%s – %s", feld.Gruppe, feld.Label);
            var gegeben = PythonZeichen.Strip(Text(eingabe));
            probleme.Add(gegeben.Length > 0
                ? Prozent("%s: „%s“ stimmt nicht, richtig ist %s.", label, gegeben, feld.Anzeige)
                : Prozent("%s fehlt, richtig ist %s.", label, feld.Anzeige));
        }
        return probleme;
    }

    /// <summary>find_solution fuer Formulare.</summary>
    private static PyObjekt FormLoesung(PyObjekt auftrag)
    {
        var ergebnis = new PyObjekt();
        foreach (var feld in FormFelder(auftrag))
        {
            Py.Setzen(ergebnis, feld.Id, feld.Art switch
            {
                "praefix" => Prozent("/%d", feld.Soll),
                "ip" or "wahl" => feld.Soll,
                _ => Num(feld.Soll),
            });
        }
        return ergebnis;
    }
}
