using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Loeschmarken und Aufhebungen wie fisi_sync (Python v0.63.2): parse_marker, _later,
/// _order, _merged_markers, Aufhebungen nach "Alles ersetzen" (_lift_pair, _union_lift,
/// _merged_lifts, _lifted). Werte sind Texte oder andere Zellwerte (dann ungueltig).
/// Ziffern zaehlen wie in Python auch als Unicode-Ziffern, wo Python sie annimmt.
/// </summary>
public static class Loeschmarken
{
    public static readonly IReadOnlyList<string> Schluessel = ["reset_at", "history_cleared_at", "spiel_reset_at", "bestenliste_reset_at"];
    public const string AufhebungPraefix = "aufgehoben:";     // LIFT_PREFIX
    public const string AufhebungFeld = "aufgehoben";         // LIFT_KEY
    public const string MischbetriebSchluessel = "mischbetrieb"; // MIXED_KEY
    public const int MischbetriebSekunden = 7 * 86400;        // MIXED_HOLD
    public const int UhrToleranz = 300;                       // CLOCK_TOLERANCE

    private static readonly Regex Muster = new(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})(\.\d{1,6})?(?:([+-])(\d{2}):(\d{2}))?\z");

    // _strptime "%Y-%m-%d %H:%M:%S": Muster wie Pythons _strptime (Leerzeichen = \s+)
    private static readonly Regex Zeitmuster = new(
        @"^(\d\d\d\d)-(1[0-2]|0[1-9]|[1-9])-(3[01]|[12]\d|0[1-9]|[1-9]| [1-9])\s+(2[0-3]|[0-1]\d|\d):([0-5]\d|\d):(6[0-1]|[0-5]\d|\d)\z",
        RegexOptions.IgnoreCase);

    /// <summary>Wahrheitswert wie Pythons bool(x) fuer Zellwerte.</summary>
    public static bool Wahr(object? wert) => wert switch
    {
        null => false,
        string s => s.Length > 0,
        long n => n != 0,
        int n => n != 0,
        double d => d != 0,
        bool b => b,
        byte[] b => b.Length > 0,
        JsonArray a => a.Count > 0,
        JsonObject o => o.Count > 0,
        _ => true,
    };

    /// <summary>int() eines Textes aus Ziffern (auch Unicode-Ziffern wie Python).</summary>
    internal static int Ziffern(string text)
    {
        var wert = 0;
        foreach (var zeichen in text.EnumerateRunes())
        {
            wert = checked(wert * 10 + (int)Rune.GetNumericValue(zeichen));
        }
        return wert;
    }

    /// <summary>datetime.strptime(text, "%Y-%m-%d %H:%M:%S") oder null (ValueError).</summary>
    public static DateTime? Ortszeit(string text)
    {
        var m = Zeitmuster.Match(text);
        if (!m.Success)
        {
            return null;
        }
        int jahr = Ziffern(m.Groups[1].Value), monat = Ziffern(m.Groups[2].Value), tag = Ziffern(m.Groups[3].Value.Trim()),
            stunde = Ziffern(m.Groups[4].Value), minute = Ziffern(m.Groups[5].Value), sekunde = Ziffern(m.Groups[6].Value);
        if (jahr < 1 || tag > DateTime.DaysInMonth(jahr, monat) || sekunde > 59)
        {
            return null;
        }
        return new DateTime(jahr, monat, tag, stunde, minute, sekunde, DateTimeKind.Unspecified);
    }

    /// <summary>parse_marker: (Ortszeit-Text ohne Zone, Zeitpunkt in UTC oder null ohne Zone); null = ungueltig.</summary>
    public static (string Text, DateTime? Utc)? Lesen(object? wert)
    {
        if (wert is not string text)
        {
            return null;
        }
        var m = Muster.Match(text);
        if (!m.Success || Ortszeit(m.Groups[1].Value) is not { } ort)
        {
            return null;
        }
        var ohneZone = m.Groups[1].Value + m.Groups[2].Value;
        if (m.Groups[2].Success)
        {
            ort = ort.AddTicks(Ziffern(m.Groups[2].Value[1..].PadRight(6, '0')) * 10L);
        }
        if (!m.Groups[3].Success)
        {
            return (ohneZone, null);
        }
        int stunden = Ziffern(m.Groups[4].Value), minuten = Ziffern(m.Groups[5].Value);
        if (stunden > 23 || minuten > 59)
        {
            return null;
        }
        var abstand = new TimeSpan(stunden, minuten, 0);
        try
        {
            return (ohneZone, m.Groups[3].Value == "+" ? ort - abstand : ort + abstand);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;    // Python: OverflowError am Rand des Kalenders
        }
    }

    /// <summary>Vergleich zweier Texte wie Python (Unicode-Codepunkte).</summary>
    public static int Vergleich(string a, string b)
    {
        var ea = a.EnumerateRunes();
        var eb = b.EnumerateRunes();
        while (true)
        {
            bool ha = ea.MoveNext(), hb = eb.MoveNext();
            if (!ha || !hb)
            {
                return ha.CompareTo(hb);
            }
            var d = ea.Current.Value.CompareTo(eb.Current.Value);
            if (d != 0)
            {
                return d;
            }
        }
    }

    private static string Max(string a, string b) => Vergleich(b, a) > 0 ? b : a;

    private static string Min(string a, string b) => Vergleich(b, a) < 0 ? b : a;

    private static bool Gilt(object? wert, bool zone) => Wahr(wert) && (zone ? Lesen(wert) is not null : wert is string);

    /// <summary>_later: die spaetere von zwei Marken (mit Zone in UTC, sonst als Text); ungueltige zaehlen nicht.</summary>
    public static string? Spaeter(object? erste, object? zweite, bool zone = true)
    {
        var werte = new[] { erste, zweite }.Where(w => Gilt(w, zone)).Cast<string>().ToList();
        if (werte.Count == 0)
        {
            return null;
        }
        if (zone && werte.Count == 2)
        {
            var a = Lesen(werte[0])!.Value.Utc;
            var b = Lesen(werte[1])!.Value.Utc;
            if (a is not null && b is not null)
            {
                return a >= b ? werte[0] : werte[1];
            }
        }
        return werte.Count == 2 ? Max(werte[0], werte[1]) : werte[0];
    }

    /// <summary>_order: -1, 0 oder 1 (mit Zone in UTC, sonst Text).</summary>
    public static int Reihenfolge(string erste, string zweite, bool zone = true)
    {
        if (zone && Lesen(erste) is { Utc: { } a } && Lesen(zweite) is { Utc: { } b })
        {
            return a.CompareTo(b);
        }
        return Math.Sign(Vergleich(erste, zweite));
    }

    /// <summary>_invalid_markers: Schluessel mit einem Wert, der keine gueltige Marke ist.</summary>
    public static List<string> Ungueltige(IReadOnlyDictionary<string, object?> marken, bool zone) =>
        Schluessel.Where(k => marken.TryGetValue(k, out var w) && Wahr(w) && !(zone ? Lesen(w) is not null : w is string)).ToList();

    /// <summary>_merged_markers: je Schluessel die spaetere Marke; bei Gleichstand die erste Seite.</summary>
    public static Dictionary<string, string> Zusammen(IReadOnlyDictionary<string, object?> lokal, IReadOnlyDictionary<string, object?> fremd,
        bool zone, bool fremdZuerst)
    {
        var ergebnis = new Dictionary<string, string>();
        foreach (var k in Schluessel)
        {
            var (erste, zweite) = fremdZuerst ? (fremd.GetValueOrDefault(k), lokal.GetValueOrDefault(k)) : (lokal.GetValueOrDefault(k), fremd.GetValueOrDefault(k));
            if (Spaeter(erste, zweite, zone) is { Length: > 0 } wert)
            {
                ergebnis[k] = wert;
            }
        }
        return ergebnis;
    }

    /// <summary>_lift_pair: [alt, neu] aus JSON-Text oder Liste; alt gueltig, neu gueltig oder "". Sonst null.</summary>
    public static string[]? Paar(object? wert)
    {
        if (wert is string text)
        {
            try
            {
                wert = JsonNode.Parse(text);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }
        if (wert is string[] { Length: 2 } fertig)
        {
            wert = new JsonArray(fertig[0], fertig[1]);
        }
        if (wert is JsonArray liste && liste.Count == 2
            && liste.All(e => e is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.String))
        {
            string alt = (string)liste[0]!, neu = (string)liste[1]!;
            if (Lesen(alt) is not null && (neu.Length == 0 || Lesen(neu) is not null))
            {
                return [alt, neu];
            }
        }
        return null;
    }

    /// <summary>_union_lift: spaetere alte und fruehere neue Marke ("" ist die frueheste).</summary>
    public static string[]? Vereinigen(string[]? erste, string[]? zweite, bool zone = true)
    {
        if (erste is null || zweite is null)
        {
            return erste ?? zweite;
        }
        var alt = Reihenfolge(erste[0], zweite[0], zone) >= 0 ? erste[0] : zweite[0];
        if (Reihenfolge(erste[0], zweite[0], zone) == 0)
        {
            alt = Max(erste[0], zweite[0]);
        }
        string neu;
        if (erste[1].Length == 0 || zweite[1].Length == 0)
        {
            neu = "";
        }
        else if (Reihenfolge(erste[1], zweite[1], zone) == 0)
        {
            neu = Min(erste[1], zweite[1]);
        }
        else
        {
            neu = Reihenfolge(erste[1], zweite[1], zone) < 0 ? erste[1] : zweite[1];
        }
        return [alt, neu];
    }

    /// <summary>
    /// _merged_lifts: Aufhebungen beider Seiten. jetzt (Geraeteuhr bei Sicherungen): eine alte
    /// Marke mit Zone mehr als 300 s danach gilt als jetzt.
    /// </summary>
    public static Dictionary<string, string[]> AufhebungenZusammen(IReadOnlyDictionary<string, object?>? lokal,
        IReadOnlyDictionary<string, object?>? fremd, bool zone, DateTimeOffset? jetzt)
    {
        var ergebnis = new Dictionary<string, string[]>();
        foreach (var k in Schluessel)
        {
            var paar = Vereinigen(Paar(lokal?.GetValueOrDefault(k)), Paar(fremd?.GetValueOrDefault(k)), zone);
            if (paar is not null && jetzt is { } j)
            {
                var alt = Lesen(paar[0])!.Value.Utc;
                var grenze = j.UtcDateTime.AddSeconds(UhrToleranz);
                if (alt is not null && alt > DateTime.SpecifyKind(grenze, DateTimeKind.Unspecified))
                {
                    paar = [Uhr.MitZone(j), paar[1]];
                }
            }
            if (paar is not null)
            {
                ergebnis[k] = paar;
            }
        }
        return ergebnis;
    }

    /// <summary>_lifted: die Marke nach der Aufhebung (null = keine Marke mehr).</summary>
    public static object? Aufgehoben(object? wert, string[]? paar, bool zone = true)
    {
        if (!Wahr(wert) || paar is null)
        {
            return wert;
        }
        var text = (string)wert!;
        var (alt, neu) = (paar[0], paar[1]);
        if (Reihenfolge(text, alt, zone) > 0 || (neu.Length > 0 && Reihenfolge(text, neu, zone) <= 0))
        {
            return text;
        }
        return neu.Length > 0 ? neu : null;
    }

    /// <summary>_apply_lifts: Aufhebungen auf die Marken einer Seite anwenden.</summary>
    public static void AufhebungenAnwenden(Dictionary<string, object?> marken, IReadOnlyDictionary<string, string[]> aufhebungen, bool zone)
    {
        foreach (var (k, paar) in aufhebungen)
        {
            if (marken.TryGetValue(k, out var wert) && Wahr(wert))
            {
                var neu = Aufgehoben(wert, paar, zone);
                if (Wahr(neu))
                {
                    marken[k] = neu;
                }
                else
                {
                    marken.Remove(k);
                }
            }
        }
    }

    /// <summary>json.dumps(["alt", "neu"]) wie Python (ensure_ascii, Trenner ", ").</summary>
    public static string PaarText(string[] paar)
    {
        var sb = new StringBuilder("[");
        for (var i = 0; i < paar.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            sb.Append('"');
            foreach (var c in paar[i])
            {
                sb.Append(c switch
                {
                    '"' => "\\\"",
                    '\\' => "\\\\",
                    '\n' => "\\n",
                    '\r' => "\\r",
                    '\t' => "\\t",
                    '\b' => "\\b",
                    '\f' => "\\f",
                    _ when c < 0x20 || c > 0x7E => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
                    _ => c.ToString(),
                });
            }
            sb.Append('"');
        }
        return sb.Append(']').ToString();
    }

    /// <summary>
    /// _parse_utc: ISO-Zeitpunkt mit Zone als UTC, sonst null. Angenommen werden die
    /// Schreibweisen, die Python mit isoformat() schreibt (T oder Leerzeichen, Bruchteile, Zone oder Z).
    /// </summary>
    public static DateTimeOffset? IsoUtc(object? wert)
    {
        if (wert is not string text)
        {
            return null;
        }
        var m = Regex.Match(text, @"^(\d{4}-\d{2}-\d{2})[T ](\d{2}:\d{2}(?::\d{2}(?:\.\d{1,6})?)?)(Z|[+-]\d{2}:\d{2}(?::\d{2})?)\z");
        if (!m.Success)
        {
            return null;
        }
        var zone = m.Groups[3].Value == "Z" ? "+00:00" : m.Groups[3].Value[..6];
        return DateTimeOffset.TryParse(m.Groups[1].Value + "T" + m.Groups[2].Value + zone, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var zeit) ? zeit.ToUniversalTime() : null;
    }
}
