using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Lernplattform.Core.Suche;
using Lernplattform.Core.Texte;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Core.Rechner;

/// <summary>Ergebnis von subnet_parse: Netz (Hostanteil geloescht), Praefix, eingegebene Adresse, Hinweis.</summary>
public sealed record GelesenesNetz(int Version, int Breite, BigInteger Netz, int Praefix, BigInteger Adresse, string? Hinweis);

/// <summary>Ergebnis von split_network ("Netz aufteilen"); die Subnetze selbst rechnet Eintrag() ohne Liste.</summary>
public sealed record Aufteilung(GelesenesNetz Basis, BigInteger Gewuenscht, int K, int Neu, BigInteger Anzahl,
    BigInteger Groesse, BigInteger Seiten, BigInteger? Maske, BigInteger? Hosts, int? Nibble, BigInteger? NibbleAnzahl,
    IReadOnlyList<string> Hinweise)
{
    public int Version => Basis.Version;

    public BigInteger Netz => Basis.Netz;

    public int Praefix => Basis.Praefix;
}

/// <summary>Ein Subnetz aus split_entry.</summary>
public sealed record Subnetz(BigInteger Nr, BigInteger Netz, BigInteger Ende, int Praefix, string Status,
    BigInteger Erste, BigInteger Letzte, BigInteger? Broadcast);

/// <summary>Eine Seite aus split_page.</summary>
public sealed record Seite(BigInteger Nummer, BigInteger Seiten, string Text, IReadOnlyList<Subnetz> Eintraege);

/// <summary>Ein Subnetz bei "Nach Hostbedarf" (subnet_by_hosts).</summary>
public sealed class Bedarfsnetz
{
    public required string Name { get; init; }

    public required BigInteger Bedarf { get; init; }

    public required int H { get; init; }

    public required int Praefix { get; init; }

    public required BigInteger Groesse { get; init; }

    public required BigInteger Maske { get; init; }

    public BigInteger Netz { get; internal set; }

    public BigInteger Erste { get; internal set; }

    public BigInteger Letzte { get; internal set; }

    public BigInteger Broadcast { get; internal set; }
}

/// <summary>Ergebnis von subnet_by_hosts ("Nach Hostbedarf", VLSM, nur IPv4).</summary>
public sealed record Hostbedarf(GelesenesNetz Basis, IReadOnlyList<Bedarfsnetz> Subnetze, IReadOnlyList<int> Reihenfolge,
    BigInteger Kapazitaet, BigInteger Summe, bool Passt, BigInteger Fehlt, int? Noetig, string? Meldung,
    BigInteger? Rest, BigInteger? RestVon, BigInteger? RestBis, IReadOnlyList<(BigInteger Netz, int Praefix)> Bloecke,
    IReadOnlyList<string> Hinweise)
{
    public BigInteger Netz => Basis.Netz;

    public int Praefix => Basis.Praefix;
}

/// <summary>
/// Phase 3, R2: Subnetting genau wie Python v0.63.2 (fisi_core 2714-3434): Netzdaten
/// (subnet_report, ipv6_details), Netz aufteilen (seitenweise, nie alle Subnetze aufzaehlen,
/// IPv6 bis 2^64 Subnetze sofort) und Nach Hostbedarf. Adressen liest PythonIp (drei
/// Regelsaetze je Einstieg), geschrieben wird mit Pythons str() (Netzdaten) bzw. RFC 5952
/// (Aufteilen, Hostbedarf). Vergleichswerte G25.
/// </summary>
public static partial class Subnetting
{
    private static string I(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static string D(BigInteger wert) => Zahlen.DeGanz(wert);

    private static int L(string text) => PythonZeichen.Laenge(text);

    /// <summary>Kennwerte eines IPv4-Netzes (ipv4_values, auch fuer das Spiel).</summary>
    public sealed record V4Werte(BigInteger Netz, BigInteger Maske, BigInteger Wildcard, BigInteger Broadcast,
        BigInteger Erste, BigInteger Letzte, BigInteger Hosts, BigInteger Adressen, int Praefix);

    /// <summary>ipv4_values(network).</summary>
    public static V4Werte V4Kennwerte(IpNetz netz)
    {
        var anzahl = netz.Anzahl;
        var hosts = netz.Praefix < 31 ? anzahl - 2 : (netz.Praefix == 31 ? 2 : 1);
        var netzadresse = netz.Netzadresse.Wert;
        var broadcast = netz.Letzte;
        BigInteger erste, letzte;
        if (netz.Praefix < 31)
        {
            erste = netzadresse + 1;
            letzte = broadcast - 1;
        }
        else
        {
            // /31: beide Adressen; /32: die eine Adresse
            erste = netzadresse;
            letzte = broadcast;
        }
        return new V4Werte(netzadresse, netz.Maske, netz.Maske ^ 0xFFFFFFFF, broadcast, erste, letzte, hosts, anzahl, netz.Praefix);
    }

    /// <summary>subnet_report(value): Netzdaten als Text (Adresse mit Praefix, Maske oder ohne).</summary>
    public static string Bericht(string wert)
    {
        var netz = PythonIp.Netz(PythonZeichen.Strip(wert), streng: false) ?? throw new RechnerFehler(
            "Bitte eine gültige Adresse angeben.\n\nBeispiele:\n  192.168.1.50/24\n  10.0.0.0/255.255.255.0\n  2001:db8::1/64");
        List<string> zeilen;
        if (netz.Version == 4)
        {
            var v = V4Kennwerte(netz);
            var broadcast = v.Praefix switch
            {
                31 => "keine (/31, RFC 3021)",
                32 => "keine (/32, einzelne Adresse)",
                _ => PythonIp.V4Text(v.Broadcast),
            };
            zeilen =
            [
                "Netzadresse           : " + PythonIp.V4Text(v.Netz),
                "Subnetzmaske          : " + PythonIp.V4Text(v.Maske),
                "Wildcard-Maske        : " + PythonIp.V4Text(v.Wildcard),
                "Broadcast-Adresse     : " + broadcast,
                "Erste Host-Adresse    : " + PythonIp.V4Text(v.Erste),
                "Letzte Host-Adresse   : " + PythonIp.V4Text(v.Letzte),
                "Nutzbare Hosts        : " + D(v.Hosts),
                "Adressen gesamt       : " + D(v.Adressen),
                I($"Präfixlänge           : /{v.Praefix}"),
            ];
        }
        else
        {
            zeilen =
            [
                "Netzadresse           : " + PythonIp.Text(netz.Netzadresse),
                I($"Präfixlänge           : /{netz.Praefix}"),
                "Erste Adresse         : " + PythonIp.Text(netz.Netzadresse),
                "Letzte Adresse        : " + PythonIp.Text(new IpAdresse(6, netz.Letzte)),
                I($"Adressen gesamt       : 2^{128 - netz.Praefix} = ") + D(netz.Anzahl),
            ];
        }
        return string.Join("\n", zeilen);
    }

    private static bool Im(BigInteger wert, int praefix, BigInteger basis, int laenge) =>
        praefix >= laenge && (wert >> (128 - laenge)) == (basis >> (128 - laenge));

    /// <summary>IPV6_TYPES (fisi_core 2802): Praefix als Zahl, Laenge, Text.</summary>
    internal static readonly IReadOnlyList<(BigInteger Basis, int Laenge, string Text)> V6Typen =
    [
        (0, 128, "Unspezifiziert (::/128)"),
        (1, 128, "Loopback (::1/128)"),
        (new BigInteger(0xff) << 120, 8, "Multicast (ff00::/8)"),
        (new BigInteger(0xfe80) << 112, 10, "Link-Local (fe80::/10)"),
        (new BigInteger(0xfec0) << 112, 10, "Site-Local (veraltet, RFC 3879)"),
        (new BigInteger(0xfc) << 120, 7, _IPV6_ULA),
        (new BigInteger(0xfd) << 120, 8, _IPV6_ULA + ", lokal vergeben, fd00::/8"),
        (new BigInteger(0xfc) << 120, 8, _IPV6_ULA + ", L-Bit 0, bisher nicht vergeben"),
        (new BigInteger(0x20010db8) << 96, 32, "Dokumentation (2001:db8::/32, RFC 3849)"),
        (new BigInteger(0x3fff) << 112, 20, "Dokumentation (3fff::/20, RFC 9637)"),
        (new BigInteger(0x2000) << 112, 3, "Global Unicast (2000::/3)"),
    ];

    /// <summary>ipv6_type(value, prefix): genauester Bereich, der das ganze Netz enthaelt.</summary>
    public static string V6Typ(BigInteger wert, int praefix)
    {
        (int Laenge, string Text)? bester = null;
        foreach (var (basis, laenge, text) in V6Typen)
        {
            if (Im(wert, praefix, basis, laenge) && (bester is null || laenge > bester.Value.Laenge))
            {
                bester = (laenge, text);
            }
        }
        if (bester is not null)
        {
            return bester.Value.Text;
        }
        foreach (var (basis, laenge, _) in V6Typen)
        {
            if (Im(basis, laenge, wert, praefix))
            {
                return IPV6_TYPE_MIXED;
            }
        }
        return IPV6_TYPE_REST;
    }

    /// <summary>ipv6_long: 8 Hextette zu je 4 Stellen mit Praefix.</summary>
    public static string V6Lang(BigInteger wert, int praefix) =>
        string.Join(":", PythonIp.Hextette(wert).Select(g => g.ToString("x4", CultureInfo.InvariantCulture))) + I($"/{praefix}");

    /// <summary>ipv6_details(value): Ausgeschrieben und Adresstyp.</summary>
    public static IReadOnlyList<(string Beschriftung, string Wert)> V6Details(string wert)
    {
        var netz = PythonIp.Netz(PythonZeichen.Strip(wert), streng: false);
        if (netz is null || netz.Version != 6)
        {
            throw new RechnerFehler(IPV6_DETAILS_ERROR);
        }
        var adresse = netz.Netzadresse.Wert;
        return [(IPV6_LABEL_AUSGESCHRIEBEN, V6Lang(adresse, netz.Praefix)), (IPV6_LABEL_ADRESSTYP, V6Typ(adresse, netz.Praefix))];
    }

    /// <summary>
    /// Netzdaten wie die Rechner-Seite in Python (app_gui SubnetCalc.calc, main.py calc_subnet):
    /// subnet_report; ein Netz der anderen Version meldet subnet_parse; im Modus IPv6 kommen
    /// Ausgeschrieben und Adresstyp dazu. modus "ipv4" oder "ipv6".
    /// </summary>
    public static string Netzdaten(string? wert, string modus)
    {
        var eingabe = wert ?? "";
        var version = modus == "ipv4" ? 4 : 6;
        var text = Bericht(eingabe);
        var gefunden = PythonIp.Netz(PythonZeichen.Strip(eingabe), streng: false)!;
        if (gefunden.Version != version)
        {
            Lesen(PythonIp.Text(gefunden), version);
        }
        if (modus == "ipv6")
        {
            text += string.Concat(V6Details(eingabe).Select(p => "\n" + PythonZeichen.Links(p.Beschriftung, 22) + ": " + p.Wert));
        }
        return text;
    }

    /// <summary>subnet_addr_text.</summary>
    public static string AdressText(int version, BigInteger wert) => version == 4 ? PythonIp.V4Text(wert) : PythonIp.V6Text(wert);

    /// <summary>subnet_net_text.</summary>
    public static string NetzText(int version, BigInteger wert, int praefix) => AdressText(version, wert) + I($"/{praefix}");

    /// <summary>subnet_mask(prefix): IPv4-Maske als Zahl.</summary>
    public static BigInteger V4Maske(int praefix) => ((BigInteger.One << 32) - 1) ^ ((BigInteger.One << (32 - praefix)) - 1);

    /// <summary>subnet_parse(text, version): Netz mit Praefix ("/" Pflicht, keine Zone).</summary>
    public static GelesenesNetz Lesen(string? text, int? version = null)
    {
        var roh = PythonZeichen.Strip(text ?? "");
        if (!roh.Contains('/', StringComparison.Ordinal) || roh.Contains('%', StringComparison.Ordinal))
        {
            throw new RechnerFehler(SUBNET_NET_ERROR);
        }
        var schnittstelle = PythonIp.Schnittstelle(roh) ?? throw new RechnerFehler(SUBNET_NET_ERROR);
        var gefunden = schnittstelle.Ip.Version;
        if (version is int v && gefunden != v)
        {
            var beispiel = v == 4 ? "192.168.10.0/24" : "2001:db8:abcd::/48";
            throw new RechnerFehler(I($"Im Modus IPv{v} bitte ein IPv{v}-Netz angeben (z. B. {beispiel})."));
        }
        var breite = gefunden == 4 ? 32 : 128;
        var praefix = schnittstelle.Netz.Praefix;
        var adresse = schnittstelle.Ip.Wert;
        var basis = adresse - (adresse & ((BigInteger.One << (breite - praefix)) - 1));
        string? hinweis = null;
        if (adresse != basis)
        {
            hinweis = Beschriftungen.Einsetzen(SUBNET_HOST_HINT, NetzText(gefunden, adresse, praefix), NetzText(gefunden, basis, praefix));
        }
        return new GelesenesNetz(gefunden, breite, basis, praefix, adresse, hinweis);
    }

    /// <summary>_subnet_whole: ganze Zahl genau aus dem Text (ueber 2^53).</summary>
    private static BigInteger Ganz(string text)
    {
        var roh = PythonZeichen.Strip(text);
        var negativ = roh.StartsWith('-');
        roh = roh.TrimStart('-');
        if (Regex.IsMatch(roh, "^[1-9][0-9]{0,2}([ \u00a0\u202f][0-9]{3})+(,[0-9]+)?\\z", RegexOptions.CultureInvariant))
        {
            roh = Regex.Replace(roh, "[ \u00a0\u202f]", ".", RegexOptions.CultureInvariant);
        }
        var komma = roh.IndexOf(',', StringComparison.Ordinal);
        var ganz = komma < 0 ? roh : roh[..komma];
        var rest = komma < 0 ? "" : roh[(komma + 1)..];
        if (komma < 0 && ganz.Contains('.', StringComparison.Ordinal) && !Zahlen.PasstGruppen(ganz))
        {
            var punkt = ganz.IndexOf('.', StringComparison.Ordinal);
            rest = ganz[(punkt + 1)..];
            ganz = ganz[..punkt];
        }
        if (rest.Trim('0').Length > 0)
        {
            throw new RechnerFehler("Bitte eine ganze Zahl eingeben: " + SUBNET_FIELD_COUNT + ".");
        }
        var ziffern = ganz.Replace(".", "", StringComparison.Ordinal);
        var wert = BigInteger.Parse(ziffern.Length > 0 ? ziffern : "0", NumberStyles.None, CultureInfo.InvariantCulture);
        return negativ ? -wert : wert;
    }

    /// <summary>subnet_count(text): Anzahl Subnetze (Leseregel, ueber 2^53 genau aus dem Text).</summary>
    public static BigInteger Anzahl(string? text)
    {
        var zahl = Zahlen.LesenGanz(text, SUBNET_FIELD_COUNT);
        if (BigInteger.Abs(zahl) >= BigInteger.One << 53)
        {
            zahl = Ganz(text!);
        }
        return Anzahl(zahl);
    }

    /// <summary>subnet_count(int).</summary>
    public static BigInteger Anzahl(BigInteger zahl) => zahl < 1 ? throw new RechnerFehler(SUBNET_COUNT_ERROR) : zahl;

    /// <summary>int.bit_length() fuer nicht negative Zahlen.</summary>
    internal static int Bits(BigInteger wert) => wert.Sign == 0 ? 0 : (int)wert.GetBitLength();

    /// <summary>split_network(net_text, count, version).</summary>
    public static Aufteilung Aufteilen(string? netzText, string? anzahl, int? version = null) =>
        Aufteilen(Lesen(netzText, version), Anzahl(anzahl));

    /// <summary>split_network mit schon gelesener Anzahl.</summary>
    public static Aufteilung Aufteilen(string? netzText, BigInteger anzahl, int? version = null) =>
        Aufteilen(Lesen(netzText, version), Anzahl(anzahl));

    private static Aufteilung Aufteilen(GelesenesNetz basis, BigInteger gewuenscht)
    {
        var praefix = basis.Praefix;
        var breite = basis.Breite;
        var bits = Bits(gewuenscht - 1);
        var neu = praefix + bits;
        if (neu > breite)
        {
            var raum = breite - praefix;
            var hoechstens = Beschriftungen.Einsetzen(SUBNET_TOO_MANY_MAX,
                raum <= 32 ? D(BigInteger.One << raum) : I($"2^{raum}"), raum == 0 ? "Subnetz" : "Subnetze");
            throw new RechnerFehler(Beschriftungen.Einsetzen(SUBNET_TOO_MANY, praefix, bits, neu, breite) + " " + hoechstens);
        }
        var gesamt = BigInteger.One << bits;
        var groesse = BigInteger.One << (breite - neu);
        var hinweise = new List<string>();
        if (basis.Hinweis is not null)
        {
            hinweise.Add(basis.Hinweis);
        }
        if (bits == 0)
        {
            hinweise.Add(SUBNET_NO_SPLIT);
        }
        BigInteger? maske = null;
        BigInteger? hosts = null;
        if (basis.Version == 4)
        {
            maske = V4Maske(neu);
            hosts = neu < 31 ? groesse - 2 : groesse;
            if (neu == 31)
            {
                hinweise.Add(SUBNET_HINT_31);
            }
            else if (neu == 32)
            {
                hinweise.Add(SUBNET_HINT_32);
            }
        }
        int? nibble = null;
        BigInteger? nibbleAnzahl = null;
        if (basis.Version == 6 && neu % 4 != 0)
        {
            nibble = (neu + 3) / 4 * 4;
            nibbleAnzahl = BigInteger.One << (nibble.Value - praefix);
        }
        return new Aufteilung(basis, gewuenscht, bits, neu, gesamt, groesse,
            (gesamt + SUBNET_PAGE_SIZE - 1) / SUBNET_PAGE_SIZE, maske, hosts, nibble, nibbleAnzahl, hinweise);
    }

    /// <summary>split_entry(result, index): Subnetz Nr. index + 1 ohne Liste.</summary>
    public static Subnetz Eintrag(Aufteilung a, BigInteger index)
    {
        if (index < 0 || index >= a.Anzahl)
        {
            throw new ArgumentOutOfRangeException(nameof(index), I($"Subnetz {index + 1} gibt es nicht"));
        }
        var netz = a.Netz + index * a.Groesse;
        var ende = netz + a.Groesse - 1;
        var status = index < a.Gewuenscht ? SUBNET_STATE_USED : SUBNET_STATE_FREE;
        return a.Version == 4 && a.Neu < 31
            ? new Subnetz(index + 1, netz, ende, a.Neu, status, netz + 1, ende - 1, ende)
            : new Subnetz(index + 1, netz, ende, a.Neu, status, netz, ende, null);
    }

    /// <summary>split_page(result, page): Seite auf 1 bis letzte begrenzt.</summary>
    public static Seite SeiteVon(Aufteilung a, BigInteger seite)
    {
        var seiten = a.Seiten;
        seite = BigInteger.Min(BigInteger.Max(seite, 1), seiten);
        var start = (seite - 1) * SUBNET_PAGE_SIZE;
        var stopp = BigInteger.Min(start + SUBNET_PAGE_SIZE, a.Anzahl);
        var eintraege = new List<Subnetz>();
        for (var i = start; i < stopp; i++)
        {
            eintraege.Add(Eintrag(a, i));
        }
        return new Seite(seite, seiten, Beschriftungen.Einsetzen(SUBNET_PAGE_OF, D(seite), D(seiten)), eintraege);
    }

    private static string HostText(Aufteilung a) => a.Neu switch
    {
        31 => SUBNET_HINT_31,
        32 => SUBNET_HINT_32,
        _ => D(a.Hosts!.Value),
    };

    /// <summary>split_report(result, page, schmal): Ergebnis "Netz aufteilen" mit einer Seite.</summary>
    public static string AufteilenBericht(Aufteilung a, BigInteger seite, bool schmal = false)
    {
        var version = a.Version;
        var daten = SeiteVon(a, seite);
        var basis = NetzText(version, a.Netz, a.Praefix);
        var benutzt = a.Gewuenscht;
        var frei = a.Anzahl - benutzt;
        var anzahlText = D(a.Anzahl);
        if (frei != 0)
        {
            anzahlText += " (" + D(benutzt) + " verwendet, " + D(frei) + " frei, Reserve)";
        }
        var reihen = new List<(string, string)>
        {
            ("Ausgangsnetz", basis),
            ("Gesucht", benutzt == 1 ? "1 Subnetz" : D(benutzt) + " Subnetze"),
            ("Benötigte Bits", I($"k = {a.K}")),
            ("Neue Präfixlänge", I($"/{a.Neu}")),
        };
        if (version == 4)
        {
            reihen.Add(("Subnetzmaske", PythonIp.V4Text(a.Maske!.Value)));
        }
        reihen.Add(("Adressen je Subnetz", D(a.Groesse)));
        if (version == 4)
        {
            reihen.Add(("Nutzbare Hosts", HostText(a)));
        }
        reihen.Add(("Subnetze", anzahlText));
        var zeilen = new List<string>(a.Hinweise);
        if (a.Nibble is int nibble)
        {
            zeilen.Add(I($"Hinweis: An der nächsten 4-Bit-Grenze (Nibble): /{nibble} = ") + D(a.NibbleAnzahl!.Value) + " Subnetze.");
        }
        if (zeilen.Count > 0)
        {
            zeilen.Add("");
        }
        if (schmal)
        {
            foreach (var (beschriftung, wert) in reihen)
            {
                zeilen.Add(beschriftung + ":");
                zeilen.Add(" " + wert);
            }
            zeilen.Add("");
            foreach (var e in daten.Eintraege)
            {
                zeilen.Add("Subnetz " + D(e.Nr) + ": " + e.Status);
                zeilen.AddRange(Zeilen.HandyAdresse(" Netz: ", NetzText(version, e.Netz, e.Praefix)));
                if (version == 4)
                {
                    zeilen.Add(" Erste: " + PythonIp.V4Text(e.Erste));
                    zeilen.Add(" Letzte: " + PythonIp.V4Text(e.Letzte));
                    zeilen.Add(" Broadcast: " + (e.Broadcast is { } b ? PythonIp.V4Text(b) : "keine"));
                }
                else
                {
                    zeilen.AddRange(Zeilen.HandyAdresse(" bis: ", PythonIp.V6Text(e.Ende)));
                }
            }
            zeilen.Add(daten.Text);
            return Zeilen.Text(Zeilen.HandyEinpassen(zeilen));
        }
        foreach (var (beschriftung, wert) in reihen)
        {
            zeilen.Add(PythonZeichen.Links(beschriftung, 22) + ": " + wert);
        }
        zeilen.Add("");
        var nummern = daten.Eintraege.Select(e => D(e.Nr) + (e.Status == SUBNET_STATE_USED ? "" : "*")).ToList();
        var breite = Math.Max(2, nummern.Max(L));
        var tabelle = new List<string>();
        if (version == 4)
        {
            var zellen = daten.Eintraege.Select(e => new[]
            {
                NetzText(4, e.Netz, e.Praefix), PythonIp.V4Text(e.Erste), PythonIp.V4Text(e.Letzte),
                e.Broadcast is { } b ? PythonIp.V4Text(b) : "keine",
            }).ToList();
            string[] koepfe = ["Netzadresse", "Erster Host", "Letzter Host", "Broadcast"];
            var groessen = Enumerable.Range(0, 4).Select(c => Math.Max(L(koepfe[c]), zellen.Max(r => L(r[c])))).ToArray();
            tabelle.Add(PythonZeichen.RStrip(string.Join("  ", new[] { PythonZeichen.Links("Nr", breite) }
                .Concat(Enumerable.Range(0, 4).Select(c => PythonZeichen.Links(koepfe[c], groessen[c]))))));
            for (var n = 0; n < nummern.Count; n++)
            {
                var reihe = zellen[n];
                tabelle.Add(PythonZeichen.RStrip(string.Join("  ", new[] { PythonZeichen.Links(nummern[n], breite) }
                    .Concat(Enumerable.Range(0, 4).Select(c => PythonZeichen.Links(reihe[c], groessen[c]))))));
            }
        }
        else
        {
            zeilen.Add(PythonZeichen.Links("Nr", breite) + " Subnetz und letzte Adresse");
            for (var n = 0; n < nummern.Count; n++)
            {
                var e = daten.Eintraege[n];
                zeilen.Add(PythonZeichen.Links(nummern[n], breite) + " " + NetzText(6, e.Netz, e.Praefix));
                zeilen.Add(new string(' ', breite) + " bis " + PythonIp.V6Text(e.Ende));
            }
        }
        var schluss = new List<string>();
        if (nummern.Any(n => n.EndsWith('*')))
        {
            schluss.Add("* frei, Reserve");
        }
        schluss.Add(daten.Text);
        return Zeilen.Text(Zeilen.Einpassen(zeilen, Zeilen.PcBreite).Concat(tabelle).Concat(Zeilen.Einpassen(schluss, Zeilen.PcBreite)));
    }

    // ---------------------------------------------------------------- Nach Hostbedarf

    [GeneratedRegex("^-?[0-9.]+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ZahlWort();

    /// <summary>subnet_hosts_text: "50 Hosts", bei 1 "1 Host".</summary>
    public static string HostsText(BigInteger bedarf) => D(bedarf) + (bedarf == 1 ? " Host" : " Hosts");

    private static List<string> OhneHostWort(List<string> woerter)
    {
        if (woerter.Count > 1 && PythonKlein.Klein(woerter[^1]) is "hosts" or "host")
        {
            woerter.RemoveAt(woerter.Count - 1);
        }
        return woerter;
    }

    /// <summary>read_host_lines(text): Bedarfszeilen "Name Hosts", "Name: Hosts" oder nur "Hosts".</summary>
    public static List<(string Name, BigInteger Bedarf)> BedarfLesen(string? text)
    {
        var zeilen = PythonZeichen.SplitLines(text ?? "");
        var gefuellt = zeilen.Count(z => PythonZeichen.Strip(z).Length > 0);
        if (gefuellt == 0)
        {
            throw new RechnerFehler(SUBNET_NEEDS_EMPTY);
        }
        if (gefuellt > SUBNET_HOSTS_MAX)
        {
            throw new RechnerFehler(Beschriftungen.Einsetzen(SUBNET_NEEDS_TOO_MANY, SUBNET_HOSTS_MAX, gefuellt));
        }
        var wuensche = new List<(string, BigInteger)>();
        var gesehen = new Dictionary<string, (int Zeile, bool Auto)>(StringComparer.Ordinal);
        for (var nummer = 1; nummer <= zeilen.Count; nummer++)
        {
            var zeile = PythonZeichen.Strip(zeilen[nummer - 1].Replace("\t", " ", StringComparison.Ordinal).Replace("\u2212", "-", StringComparison.Ordinal));
            if (zeile.Length == 0)
            {
                continue;
            }
            if (zeile.IndexOfAny([',', ';', '/']) >= 0)
            {
                throw new RechnerFehler(Beschriftungen.Einsetzen(SUBNET_NEED_COMMA, nummer));
            }
            if (zeile.Count(c => c == ':') > 1)
            {
                throw new RechnerFehler(Beschriftungen.Einsetzen(SUBNET_NEED_NUMBERS, nummer));
            }
            string name, bedarfText;
            var doppelpunkt = zeile.IndexOf(':', StringComparison.Ordinal);
            if (doppelpunkt >= 0)
            {
                var woerter = OhneHostWort(PythonZeichen.Split(zeile[(doppelpunkt + 1)..]));
                name = string.Join(" ", PythonZeichen.Split(zeile[..doppelpunkt]));
                bedarfText = string.Join(" ", woerter);
            }
            else
            {
                var woerter = OhneHostWort(PythonZeichen.Split(zeile));
                if (Zahlen.IstZahl(string.Join(" ", woerter)))
                {
                    name = "";
                    bedarfText = string.Join(" ", woerter);
                }
                else
                {
                    if (woerter.Take(woerter.Count - 1).Any(w => ZahlWort().IsMatch(w)))
                    {
                        throw new RechnerFehler(Beschriftungen.Einsetzen(SUBNET_NEED_NUMBERS, nummer));
                    }
                    name = string.Join(" ", woerter.Take(woerter.Count - 1));
                    bedarfText = woerter[^1];
                }
            }
            name = PythonZeichen.Strip(name, " -\u2013\u2014", links: false);
            BigInteger bedarf;
            try
            {
                bedarf = Zahlen.LesenGanz(bedarfText, SUBNET_FIELD_NEEDS);
            }
            catch (RechnerFehler)
            {
                throw new RechnerFehler(Beschriftungen.Einsetzen(SUBNET_NEED_NUMBER, nummer));
            }
            if (bedarf < 1)
            {
                throw new RechnerFehler(Beschriftungen.Einsetzen(SUBNET_NEED_MIN, nummer));
            }
            if (bedarf > (BigInteger.One << 32) - 2)
            {
                throw new RechnerFehler(Beschriftungen.Einsetzen(SUBNET_NEED_MAX, nummer, D((BigInteger.One << 32) - 2)));
            }
            var auto = name.Length == 0;
            if (auto)
            {
                name = I($"Netz {wuensche.Count + 1}");
            }
            if (L(name) > SUBNET_NAME_MAX)
            {
                throw new RechnerFehler(Beschriftungen.Einsetzen(SUBNET_NAME_LONG, nummer, SUBNET_NAME_MAX));
            }
            var schluessel = PythonZeichen.Falten(name);
            if (gesehen.TryGetValue(schluessel, out var frueher))
            {
                if (auto)
                {
                    throw new RechnerFehler(Beschriftungen.Einsetzen(SUBNET_NAME_AUTO_CLASH, nummer, name, frueher.Zeile));
                }
                if (frueher.Auto)
                {
                    throw new RechnerFehler(Beschriftungen.Einsetzen(SUBNET_NAME_AUTO_TAKEN, nummer, name, frueher.Zeile));
                }
                throw new RechnerFehler(Beschriftungen.Einsetzen(SUBNET_NAME_TWICE, nummer, name, frueher.Zeile));
            }
            gesehen[schluessel] = (nummer, auto);
            wuensche.Add((name, bedarf));
        }
        return wuensche;
    }

    /// <summary>subnet_host_bits: kleinstes h mit 2^h - 2 &gt;= Bedarf, mindestens 2.</summary>
    public static int HostBits(BigInteger bedarf) => Math.Max(2, Bits(bedarf + 1));

    /// <summary>subnet_blocks(start, end): Rest als ausgerichtete Bloecke.</summary>
    public static List<(BigInteger Netz, int Praefix)> Bloecke(BigInteger start, BigInteger ende)
    {
        var bloecke = new List<(BigInteger, int)>();
        while (start < ende)
        {
            var groesse = start.IsZero ? BigInteger.One << 32 : start & -start;
            while (start + groesse > ende)
            {
                groesse /= 2;
            }
            bloecke.Add((start, 32 - (Bits(groesse) - 1)));
            start += groesse;
        }
        return bloecke;
    }

    /// <summary>subnet_by_hosts(net_text, needs, version) mit Bedarfszeilen als Text.</summary>
    public static Hostbedarf NachBedarf(string? netzText, string? bedarf, int? version = null)
    {
        var basis = Lesen(netzText, version);
        if (basis.Version != 4)
        {
            throw new RechnerFehler(SUBNET_HOSTS_V6_ERROR);
        }
        return NachBedarf(basis, BedarfLesen(bedarf));
    }

    /// <summary>subnet_by_hosts mit einer Liste (Name, Bedarf).</summary>
    public static Hostbedarf NachBedarf(string? netzText, IReadOnlyList<(string Name, BigInteger Bedarf)> wuensche, int? version = null)
    {
        var basis = Lesen(netzText, version);
        if (basis.Version != 4)
        {
            throw new RechnerFehler(SUBNET_HOSTS_V6_ERROR);
        }
        return NachBedarf(basis, wuensche);
    }

    private static Hostbedarf NachBedarf(GelesenesNetz basis, IReadOnlyList<(string Name, BigInteger Bedarf)> wuensche)
    {
        var reihenfolge = Enumerable.Range(0, wuensche.Count).OrderByDescending(i => wuensche[i].Bedarf).ThenBy(i => i).ToList();
        var kapazitaet = BigInteger.One << (32 - basis.Praefix);
        var netze = wuensche.Select(w =>
        {
            var h = HostBits(w.Bedarf);
            return new Bedarfsnetz { Name = w.Name, Bedarf = w.Bedarf, H = h, Praefix = 32 - h, Groesse = BigInteger.One << h, Maske = V4Maske(32 - h) };
        }).ToList();
        var summe = netze.Aggregate(BigInteger.Zero, (s, n) => s + n.Groesse);
        var passt = summe <= kapazitaet;
        var hinweise = new List<string>();
        if (basis.Hinweis is not null)
        {
            hinweise.Add(basis.Hinweis);
        }
        BigInteger? rest = null, restVon = null, restBis = null;
        int? noetig = null;
        string? meldung = null;
        var bloecke = new List<(BigInteger, int)>();
        if (passt)
        {
            var zeiger = basis.Netz;
            foreach (var index in reihenfolge)
            {
                var n = netze[index];
                n.Netz = zeiger;
                n.Erste = zeiger + 1;
                n.Letzte = zeiger + n.Groesse - 2;
                n.Broadcast = zeiger + n.Groesse - 1;
                zeiger += n.Groesse;
            }
            var ende = basis.Netz + kapazitaet;
            rest = ende - zeiger;
            restVon = zeiger;
            restBis = ende - 1;
            bloecke = Bloecke(zeiger, ende);
            if (wuensche.Any(w => w.Bedarf <= 2))
            {
                hinweise.Add(SUBNET_HINT_P2P);
            }
        }
        else
        {
            var noetigesPraefix = 32 - Bits(summe - 1);
            if (noetigesPraefix < 0)
            {
                meldung = Beschriftungen.Einsetzen(SUBNET_MISSING_ALL, D(summe - kapazitaet));
            }
            else
            {
                noetig = noetigesPraefix;
                meldung = Beschriftungen.Einsetzen(SUBNET_MISSING, D(summe - kapazitaet), noetigesPraefix, D(BigInteger.One << (32 - noetigesPraefix)));
            }
        }
        return new Hostbedarf(basis, netze, reihenfolge, kapazitaet, summe, passt, BigInteger.Max(0, summe - kapazitaet),
            noetig, meldung, rest, restVon, restBis, bloecke, hinweise);
    }

    /// <summary>hosts_report(result, schmal): Ergebnis "Nach Hostbedarf" in der Reihenfolge der Eingabe.</summary>
    public static string BedarfBericht(Hostbedarf r, bool schmal = false)
    {
        var breite = schmal ? Zeilen.HandyBreite : Zeilen.PcBreite;
        var zeilen = r.Hinweise.Where(h => h != SUBNET_HINT_P2P).ToList();
        if (zeilen.Count > 0)
        {
            zeilen.Add("");
        }
        var basis = NetzText(4, r.Netz, r.Praefix);
        if (!r.Passt)
        {
            zeilen.AddRange(["Ausgangsnetz: " + basis, "Benötigt: " + D(r.Summe) + " Adressen, vorhanden: " + D(r.Kapazitaet), "", r.Meldung!]);
            return Zeilen.Text(Zeilen.Einpassen(zeilen, breite));
        }
        var rest = r.Rest is { } x && x != 0
            ? PythonIp.V4Text(r.RestVon!.Value) + " bis " + PythonIp.V4Text(r.RestBis!.Value) + " = " + D(x) + " Adressen"
            : "keiner, das Netz ist genau voll";
        var folge = string.Join(", ", r.Reihenfolge.Select(i => r.Subnetze[i].Name));
        if (schmal)
        {
            zeilen.AddRange(["Ausgangsnetz:", " " + basis, "Summe: " + D(r.Summe) + " von " + D(r.Kapazitaet) + " Adressen",
                "Freier Rest:", " " + rest, "Vergeben in der Reihenfolge:", " " + folge, ""]);
            foreach (var n in r.Subnetze)
            {
                zeilen.AddRange([n.Name + " (" + HostsText(n.Bedarf) + ")", " Netz: " + PythonIp.V4Text(n.Netz) + I($"/{n.Praefix}"),
                    " Maske: " + PythonIp.V4Text(n.Maske), " Erste: " + PythonIp.V4Text(n.Erste),
                    " Letzte: " + PythonIp.V4Text(n.Letzte), " Broadcast: " + PythonIp.V4Text(n.Broadcast)]);
            }
        }
        else
        {
            zeilen.AddRange([PythonZeichen.Links("Ausgangsnetz", 22) + ": " + basis,
                PythonZeichen.Links("Summe der Subnetze", 22) + ": " + D(r.Summe) + " von " + D(r.Kapazitaet) + " Adressen",
                PythonZeichen.Links("Freier Rest", 22) + ": " + rest,
                PythonZeichen.Links("Vergabe-Reihenfolge", 22) + ": " + folge, ""]);
            foreach (var n in r.Subnetze)
            {
                zeilen.Add(n.Name + " (" + HostsText(n.Bedarf) + "): " + PythonIp.V4Text(n.Netz) + I($"/{n.Praefix}") + ", Maske " + PythonIp.V4Text(n.Maske));
                zeilen.Add("   Hosts " + PythonIp.V4Text(n.Erste) + " bis " + PythonIp.V4Text(n.Letzte) + ", Broadcast " + PythonIp.V4Text(n.Broadcast));
            }
        }
        if (r.Hinweise.Contains(SUBNET_HINT_P2P))
        {
            zeilen.AddRange(["", SUBNET_HINT_P2P]);
        }
        return Zeilen.Text(Zeilen.Einpassen(zeilen, breite));
    }
}
