using System.Globalization;
using System.Numerics;

namespace Lernplattform.Core.Rechner;

/// <summary>Eine Adresse wie Pythons IPv4Address/IPv6Address: Version, Wert, Zone (nur IPv6, "%eth0").</summary>
public sealed record IpAdresse(int Version, BigInteger Wert, string? Zone = null);

/// <summary>Ein Netz wie Pythons IPv4Network/IPv6Network: Netzadresse (mit Zone, wenn erhalten) und Praefix.</summary>
public sealed record IpNetz(IpAdresse Netzadresse, int Praefix)
{
    public int Version => Netzadresse.Version;

    public int Breite => Version == 4 ? 32 : 128;

    /// <summary>Anzahl Adressen (num_addresses).</summary>
    public BigInteger Anzahl => BigInteger.One << (Breite - Praefix);

    /// <summary>Netzmaske als ganze Zahl.</summary>
    public BigInteger Maske => ((BigInteger.One << Breite) - 1) ^ ((BigInteger.One << (Breite - Praefix)) - 1);

    /// <summary>Letzte Adresse (broadcast_address bzw. network[-1], ohne Zone).</summary>
    public BigInteger Letzte => Netzadresse.Wert | ((BigInteger.One << (Breite - Praefix)) - 1);
}

/// <summary>
/// Phase 3, R2: eigener Adressleser mit den Regeln von Pythons ipaddress (Python 3.12, wie die
/// CI, die die Vergleichswerte erzeugt). Nie System.Net.IPAddress.Parse: das nimmt "1" als
/// 0.0.0.1, fuehrende Nullen und anderes an. Regeln: IPv4 genau vier Oktette aus ASCII-Ziffern,
/// hoechstens 3 Zeichen, keine fuehrende Null, bis 255; Maske als Praefix (ASCII-Ziffern, auch
/// "/024"), Netzmaske oder Hostmaske; IPv6 mit "::", IPv4-Schluss, Zone hinter "%",
/// hoechstens 45 Zeichen, nur Praefix. Schreibweise wie str() in Python 3.12 (IPv4-gemappte
/// Adressen als "::ffff:1.2.3.4", Zone wird angehaengt). Vergleichswerte G25.
/// </summary>
public static class PythonIp
{
    private static readonly BigInteger Alle6 = (BigInteger.One << 128) - 1;
    private const uint Alle4 = uint.MaxValue;

    private static bool NurAsciiZiffern(string text) => text.Length > 0 && text.All(c => c is >= '0' and <= '9');

    /// <summary>_BaseV4._ip_int_from_string.</summary>
    private static bool V4Wert(string text, out uint wert)
    {
        wert = 0;
        if (text.Length == 0)
        {
            return false;
        }
        var oktette = text.Split('.');
        if (oktette.Length != 4)
        {
            return false;
        }
        foreach (var oktett in oktette)
        {
            if (!NurAsciiZiffern(oktett) || oktett.Length > 3 || (oktett != "0" && oktett[0] == '0'))
            {
                return false;
            }
            var zahl = uint.Parse(oktett, NumberStyles.None, CultureInfo.InvariantCulture);
            if (zahl > 255)
            {
                return false;
            }
            wert = (wert << 8) | zahl;
        }
        return true;
    }

    /// <summary>IPv4Address(text).</summary>
    private static IpAdresse? V4Adresse(string text) =>
        !text.Contains('/', StringComparison.Ordinal) && V4Wert(text, out var wert) ? new IpAdresse(4, wert) : null;

    /// <summary>_BaseV6._ip_int_from_string (ohne Zone).</summary>
    private static bool V6Wert(string text, out BigInteger wert)
    {
        wert = BigInteger.Zero;
        if (text.Length == 0 || PythonZeichen.Laenge(text) > 45)
        {
            return false;
        }
        var teile = TeileMax(text, ':', 9);
        if (teile.Count < 3)
        {
            return false;
        }
        if (teile[^1].Contains('.', StringComparison.Ordinal))
        {
            if (!(V4Adresse(teile[^1]) is { } v4))
            {
                return false;
            }
            teile.RemoveAt(teile.Count - 1);
            var v4Wert = (uint)v4.Wert;
            teile.Add((v4Wert >> 16).ToString("x", CultureInfo.InvariantCulture));
            teile.Add((v4Wert & 0xFFFF).ToString("x", CultureInfo.InvariantCulture));
        }
        if (teile.Count > 9)
        {
            return false;
        }
        int? luecke = null;
        for (var i = 1; i < teile.Count - 1; i++)
        {
            if (teile[i].Length == 0)
            {
                if (luecke is not null)
                {
                    return false;
                }
                luecke = i;
            }
        }
        int hoch, tief, ausgelassen;
        if (luecke is int l)
        {
            hoch = l;
            tief = teile.Count - l - 1;
            if (teile[0].Length == 0)
            {
                hoch--;
                if (hoch != 0)
                {
                    return false;
                }
            }
            if (teile[^1].Length == 0)
            {
                tief--;
                if (tief != 0)
                {
                    return false;
                }
            }
            ausgelassen = 8 - (hoch + tief);
            if (ausgelassen < 1)
            {
                return false;
            }
        }
        else
        {
            if (teile.Count != 8 || teile[0].Length == 0 || teile[^1].Length == 0)
            {
                return false;
            }
            hoch = teile.Count;
            tief = 0;
            ausgelassen = 0;
        }
        for (var i = 0; i < hoch; i++)
        {
            if (!Hextett(teile[i], out var h))
            {
                return false;
            }
            wert = (wert << 16) | h;
        }
        wert <<= 16 * ausgelassen;
        for (var i = teile.Count - tief; i < teile.Count; i++)
        {
            if (!Hextett(teile[i], out var h))
            {
                return false;
            }
            wert = (wert << 16) | h;
        }
        return true;
    }

    /// <summary>str.split(trenner, maxsplit).</summary>
    private static List<string> TeileMax(string text, char trenner, int hoechstens)
    {
        var teile = new List<string>();
        var start = 0;
        while (teile.Count < hoechstens)
        {
            var i = text.IndexOf(trenner, start);
            if (i < 0)
            {
                break;
            }
            teile.Add(text[start..i]);
            start = i + 1;
        }
        teile.Add(text[start..]);
        return teile;
    }

    /// <summary>_parse_hextet: nur ASCII-Hexziffern, 1 bis 4 Zeichen.</summary>
    private static bool Hextett(string text, out int wert)
    {
        wert = 0;
        if (text.Length is 0 or > 4 || !text.All(char.IsAsciiHexDigit))
        {
            return false;
        }
        wert = int.Parse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>IPv6Address(text): Zone hinter "%" (nicht leer, kein zweites "%").</summary>
    private static IpAdresse? V6Adresse(string text)
    {
        if (text.Contains('/', StringComparison.Ordinal))
        {
            return null;
        }
        string? zone = null;
        var prozent = text.IndexOf('%', StringComparison.Ordinal);
        if (prozent >= 0)
        {
            zone = text[(prozent + 1)..];
            text = text[..prozent];
            if (zone.Length == 0 || zone.Contains('%', StringComparison.Ordinal))
            {
                return null;
            }
        }
        return V6Wert(text, out var wert) ? new IpAdresse(6, wert, zone) : null;
    }

    /// <summary>ip_address(text): erst IPv4, dann IPv6; ungueltig = null (Python ValueError).</summary>
    public static IpAdresse? Adresse(string text) => V4Adresse(text) ?? V6Adresse(text);

    /// <summary>_prefix_from_prefix_string: ASCII-Ziffern (fuehrende Nullen erlaubt), 0 bis Breite.</summary>
    private static int? PraefixText(string text, int breite)
    {
        if (!NurAsciiZiffern(text))
        {
            return null;
        }
        var wert = BigInteger.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
        return wert <= breite ? (int)wert : null;
    }

    /// <summary>_prefix_from_ip_int: Maske 1...10...0, sonst null.</summary>
    private static int? PraefixAusMaske(uint maske)
    {
        var nullen = maske == 0 ? 32 : BitOperations.TrailingZeroCount(maske);
        var praefix = 32 - nullen;
        var einsen = nullen == 32 ? 0u : maske >> nullen;
        var soll = praefix == 32 ? Alle4 : (1u << praefix) - 1;
        return einsen == soll ? praefix : null;
    }

    /// <summary>_BaseV4._make_netmask: Praefix, sonst Netzmaske, sonst Hostmaske.</summary>
    private static int? V4Maske(string text)
    {
        if (PraefixText(text, 32) is int p)
        {
            return p;
        }
        if (!V4Wert(text, out var wert))
        {
            return null;
        }
        return PraefixAusMaske(wert) ?? PraefixAusMaske(wert ^ Alle4);
    }

    private static (string Adresse, string? Maske)? Zerlegen(string text)
    {
        var teile = text.Split('/');
        if (teile.Length > 2)
        {
            return null;
        }
        return (teile[0], teile.Length == 2 ? teile[1] : null);
    }

    private static IpNetz? V4Netz(string adresse, string? maske, bool streng)
    {
        if (V4Adresse(adresse) is not { } ip)
        {
            return null;
        }
        if ((maske is null ? 32 : V4Maske(maske)) is not int praefix)
        {
            return null;
        }
        return Netzbau(ip, praefix, 32, streng);
    }

    private static IpNetz? V6Netz(string adresse, string? maske, bool streng)
    {
        if (V6Adresse(adresse) is not { } ip)
        {
            return null;
        }
        if ((maske is null ? 128 : PraefixText(maske, 128)) is not int praefix)
        {
            return null;
        }
        return Netzbau(ip, praefix, 128, streng);
    }

    /// <summary>Hostanteil gesetzt: streng = Fehler, sonst Netzadresse ohne Zone (neues Objekt aus der Zahl).</summary>
    private static IpNetz? Netzbau(IpAdresse ip, int praefix, int breite, bool streng)
    {
        var maske = ((BigInteger.One << breite) - 1) ^ ((BigInteger.One << (breite - praefix)) - 1);
        if ((ip.Wert & maske) != ip.Wert)
        {
            return streng ? null : new IpNetz(new IpAdresse(ip.Version, ip.Wert & maske), praefix);
        }
        return new IpNetz(ip, praefix);
    }

    /// <summary>ip_network(text, strict): erst IPv4, dann IPv6; ungueltig = null.</summary>
    public static IpNetz? Netz(string text, bool streng = true)
    {
        if (Zerlegen(text) is not var (adresse, maske))
        {
            return null;
        }
        return V4Netz(adresse, maske, streng) ?? V6Netz(adresse, maske, streng);
    }

    /// <summary>ip_interface(text): Adresse und Netz (Hostanteil geloescht); ungueltig = null.</summary>
    public static (IpAdresse Ip, IpNetz Netz)? Schnittstelle(string text)
    {
        if (Zerlegen(text) is not var (adresse, maske))
        {
            return null;
        }
        if (V4Adresse(adresse) is { } ip4 && V4Netz(adresse, maske, false) is { } netz4)
        {
            return (ip4, netz4);
        }
        if (V6Adresse(adresse) is { } ip6 && V6Netz(adresse, maske, false) is { } netz6)
        {
            return (ip6, netz6);
        }
        return null;
    }

    /// <summary>ipv4_text: 192.168.10.0.</summary>
    public static string V4Text(BigInteger wert) =>
        string.Join(".", new[] { 24, 16, 8, 0 }.Select(s => ((int)((wert >> s) & 255)).ToString(CultureInfo.InvariantCulture)));

    /// <summary>ipv6_groups: die acht Hextette.</summary>
    public static int[] Hextette(BigInteger wert) => [.. Enumerable.Range(0, 8).Select(i => (int)((wert >> (16 * (7 - i))) & 0xFFFF))];

    /// <summary>ipv6_text (RFC 5952): laengste Nullfolge ab 2 Hextetten wird "::", bei Gleichstand die erste.</summary>
    public static string V6Text(BigInteger wert) => Kurz(Hextette(wert));

    private static string Kurz(int[] gruppen)
    {
        int besterStart = 0, besteLaenge = 0;
        int? start = null;
        for (var i = 0; i < gruppen.Length + 1; i++)
        {
            if (i < gruppen.Length && gruppen[i] == 0)
            {
                start ??= i;
            }
            else if (start is int s)
            {
                if (i - s > besteLaenge)
                {
                    besterStart = s;
                    besteLaenge = i - s;
                }
                start = null;
            }
        }
        var teile = gruppen.Select(g => g.ToString("x", CultureInfo.InvariantCulture)).ToArray();
        if (besteLaenge < 2)
        {
            return string.Join(":", teile);
        }
        return string.Join(":", teile[..besterStart]) + "::" + string.Join(":", teile[(besterStart + besteLaenge)..]);
    }

    /// <summary>str(adresse) wie Python 3.12: IPv4-gemappt als "::ffff:1.2.3.4", Zone angehaengt.</summary>
    public static string Text(IpAdresse adresse)
    {
        if (adresse.Version == 4)
        {
            return V4Text(adresse.Wert);
        }
        string text;
        if (adresse.Wert >> 32 == 0xFFFF)
        {
            // _string_from_ip_int(ip >> 32) hat 8 Hextette ueber nur 16 Bit Wert: "::ffff"
            text = Kurz(Hextette(adresse.Wert >> 32)) + ":" + V4Text(adresse.Wert & 0xFFFFFFFF);
        }
        else
        {
            text = V6Text(adresse.Wert);
        }
        return string.IsNullOrEmpty(adresse.Zone) ? text : text + "%" + adresse.Zone;
    }

    /// <summary>str(netz): Netzadresse/Praefix (with_prefixlen).</summary>
    public static string Text(IpNetz netz) => Text(netz.Netzadresse) + "/" + netz.Praefix.ToString(CultureInfo.InvariantCulture);

    /// <summary>adresse.exploded wie Python 3.12 (gemappt: 30 Zeichen + IPv4); mit Zone wirft Python einen Fehler.</summary>
    public static string Ausgeschrieben(IpAdresse adresse)
    {
        if (adresse.Version == 4)
        {
            return V4Text(adresse.Wert);
        }
        if (!string.IsNullOrEmpty(adresse.Zone))
        {
            throw new FormatException("Python: exploded mit Zone ist ein AddressValueError");
        }
        var lang = string.Join(":", Hextette(adresse.Wert).Select(g => g.ToString("x4", CultureInfo.InvariantCulture)));
        return adresse.Wert >> 32 == 0xFFFF ? lang[..30] + V4Text(adresse.Wert & 0xFFFFFFFF) : lang;
    }

    /// <summary>adresse.compressed = str(adresse).</summary>
    public static string Kurzform(IpAdresse adresse) => Text(adresse);

    /// <summary>"%x" % wert (Kleinbuchstaben, ohne fuehrende Nullen).</summary>
    internal static string Hex(BigInteger wert) => wert.ToString("x", CultureInfo.InvariantCulture).TrimStart('0') is { Length: > 0 } t ? t : "0";
}
