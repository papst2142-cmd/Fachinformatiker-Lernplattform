using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Rechner;

namespace Lernplattform.Core.Lernen;

/// <summary>Rahmenplan-Einstellungen je Geraet mit Vorgaben (fisi_rahmenplan.rp_settings).</summary>
/// <param name="TerminAp1">Pruefungstermin als JJJJ-MM-TT oder "" (ungueltig).</param>
public sealed record RahmenplanWerte(bool AbschnittB, bool AbschnittD, bool AbschnittE, bool Gewichtung,
    string Lernfeld, string TerminAp1, string TerminAp2)
{
    public const string TerminAp1Vorgabe = "2027-09-29";   // AP1_DATE_DEFAULT
    public const string TerminAp2Vorgabe = "2028-04-26";   // AP2_DATE_DEFAULT

    public static readonly RahmenplanWerte Vorgabe = new(false, false, false, true, "", TerminAp1Vorgabe, TerminAp2Vorgabe);

    /// <summary>rp_settings: Werte aus einstellungen.json, sonst Vorgabe; bool() und Datumspruefung wie Python.</summary>
    public static RahmenplanWerte Aus(JsonObject einstellungen, RahmenplanGewicht plan)
    {
        JsonNode? Wert(string schluessel, JsonNode vorgabe) => einstellungen.ContainsKey(schluessel) ? einstellungen[schluessel] : vorgabe;
        string Termin(string schluessel, string vorgabe)
        {
            // str(x or ""): nur Text kann ein Datum sein (str() einer Zahl oder Liste passt nie)
            var text = Wert(schluessel, vorgabe) is JsonValue wert && wert.TryGetValue<string>(out var t) ? t : "";
            return RahmenplanGewicht.Datum(text) is { } tag ? tag.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
        }
        var lernfeld = Wert("rp_lernfeld", "") is JsonValue v && v.TryGetValue<string>(out var feld) && plan.Lernfelder.ContainsKey(feld) ? feld : "";
        return new RahmenplanWerte(
            Einstellungen.PythonBool(Wert("rp_abschnitt_b", false)), Einstellungen.PythonBool(Wert("rp_abschnitt_d", false)),
            Einstellungen.PythonBool(Wert("rp_abschnitt_e", false)), Einstellungen.PythonBool(Wert("rp_gewichtung", true)),
            lernfeld, Termin("rp_termin_ap1", TerminAp1Vorgabe), Termin("rp_termin_ap2", TerminAp2Vorgabe));
    }
}

/// <summary>
/// Gewichtung NEUER Inhalte beim normalen Weiterlernen (fisi_rahmenplan, Teil 4):
/// Gewicht = 1 + 1,5 * Wurzel(Wochen je Punkt / 10) + 0,5 * (LF-Stunden / 120), in den
/// 6 Monaten vor AP1/AP2 mal 1,5; Reihenfolge = Tageszufall hoch 1/Gewicht, absteigend.
/// Der Zufall haengt nur von Datum und Inhalt ab (MD5), PC und Handy rechnen gleich.
/// </summary>
public sealed class RahmenplanGewicht
{
    public const double WochenMax = 10.0;     // WEEKS_MAX
    public const double StundenMax = 120.0;   // HOURS_MAX
    public const double PruefungsFaktor = 1.5; // EXAM_FACTOR
    public const int PruefungsMonate = 6;     // EXAM_MONTHS

    private static readonly IReadOnlyDictionary<string, string> AbschnittSchalter =
        new Dictionary<string, string> { ["B"] = "rp_abschnitt_b", ["D"] = "rp_abschnitt_d", ["E"] = "rp_abschnitt_e" };

    private sealed record Punkt(string Abschnitt, double Wochen, bool Ap1, bool Ap2);

    private readonly List<string> _abschnitte = [];
    private readonly Dictionary<string, Punkt> _punkte = new(StringComparer.Ordinal);
    private readonly List<(string Id, int Stunden, string Abschnitt)> _lernfelder = [];
    private readonly Dictionary<string, List<string>> _themaLernfelder = new(StringComparer.Ordinal);
    private readonly Dictionary<(string, string), List<string>> _inhaltPunkte = [];
    private readonly Dictionary<(string, string), string?> _thema = [];

    public RahmenplanGewicht(Lerninhalte inhalte)
    {
        var plan = inhalte.Rahmenplan;
        var zuordnung = inhalte.RahmenplanZuordnung;
        foreach (var abschnitt in plan["abschnitte"]!.AsArray())
        {
            _abschnitte.Add((string)abschnitt!["id"]!);
        }
        foreach (var punkt in plan["punkte"]!.AsArray())
        {
            _punkte[(string)punkt!["id"]!] = new Punkt((string)punkt["abschnitt"]!, (double)punkt["wochen_punkt"]!,
                Einstellungen.PythonBool(punkt["ap1"]), Einstellungen.PythonBool(punkt["ap2"]));
        }
        foreach (var feld in plan["lernfelder"]!.AsArray())
        {
            _lernfelder.Add(((string)feld!["id"]!, (int)feld["stunden"]!, (string?)feld["abschnitt"] ?? ""));
        }
        Lernfelder = _lernfelder.ToDictionary(f => f.Id, f => f.Stunden, StringComparer.Ordinal);
        foreach (var (thema, felder) in zuordnung["themen_lernfelder"]!.AsObject())
        {
            _themaLernfelder[thema] = felder!.AsArray().Select(f => (string)f!).ToList();
        }
        var standard = zuordnung["standard_punkt"]!.AsObject().ToDictionary(p => p.Key, p => (string)p.Value!, StringComparer.Ordinal);
        var regeln = zuordnung["punkte"]!.AsArray().Select(r =>
        {
            var themen = r!["themen"] is JsonArray liste && liste.Count > 0
                ? liste.Select(t => (string)t!).ToHashSet(StringComparer.Ordinal)
                : null;
            // Python re: Muster auf Kleinschreibung; Gleichheit belegt G16 fuer alle Inhalte
            return ((string)r["id"]!, themen, new Regex((string)r["stichworte"]!, RegexOptions.CultureInvariant | RegexOptions.Compiled));
        }).ToList();

        // _compute_item_points: gleicher Fragetext spaeter ueberschreibt frueher (dict wie Python)
        foreach (var (quelle, liste, felder) in new[]
                 {
                     (Quellen.Karte, inhalte.Karteikarten, new[] { "q", "a", "a_full" }),
                     (Quellen.Quiz, inhalte.Quizfragen, new[] { "q", "a", "exp" }),
                 })
        {
            foreach (var eintrag in liste)
            {
                var schluessel = (quelle, eintrag.Frage!);
                _thema[schluessel] = eintrag.Thema;
                if (eintrag.Daten["rahmenplan"] is JsonArray eigene && eigene.Count > 0)
                {
                    _inhaltPunkte[schluessel] = eigene.Select(p => p is JsonValue w && w.TryGetValue<string>(out var id) ? id : "").ToList();
                    continue;
                }
                var thema = eintrag.Thema;
                var text = Klein(string.Join(" ", felder.Select(eintrag.Text).OfType<string>()));
                var treffer = regeln.Where(r => (r.themen is null || (thema is not null && r.themen.Contains(thema))) && r.Item3.IsMatch(text))
                    .Select(r => r.Item1).ToList();
                if (treffer.Count == 0 && thema is not null && standard.TryGetValue(thema, out var vorgabe))
                {
                    treffer = [vorgabe];
                }
                _inhaltPunkte[schluessel] = treffer;
            }
        }
    }

    /// <summary>Lernfelder (Kennung -> Stunden).</summary>
    public IReadOnlyDictionary<string, int> Lernfelder { get; }

    /// <summary>item_points: Punkte je Inhalt (Quelle, Fragetext).</summary>
    public IReadOnlyList<string> Punkte(string quelle, string schluessel) =>
        _inhaltPunkte.TryGetValue((quelle, schluessel), out var punkte) ? punkte : [];

    /// <summary>Alle Inhalte mit ihren Punkten (fuer den Vergleich mit Python).</summary>
    public IReadOnlyDictionary<(string Quelle, string Schluessel), List<string>> AllePunkte => _inhaltPunkte;

    /// <summary>str.lower() wie Python fuer die Texte der Inhalte (U+0130 wird "i" + Punkt oben).</summary>
    internal static string Klein(string text) => text.Replace("İ", "i̇", StringComparison.Ordinal).ToLowerInvariant();

    /// <summary>enabled_sections: A, C, F immer, B/D/E nach Einstellung.</summary>
    public List<string> Abschnitte(RahmenplanWerte werte) => _abschnitte.Where(a => !AbschnittSchalter.ContainsKey(a) || a switch
    {
        "B" => werte.AbschnittB,
        "D" => werte.AbschnittD,
        _ => werte.AbschnittE,
    }).ToList();

    /// <summary>visible_fields: Lernfelder ohne Abschnitt oder mit eingeschaltetem Abschnitt.</summary>
    public List<string> SichtbareLernfelder(RahmenplanWerte werte)
    {
        var abschnitte = Abschnitte(werte);
        return _lernfelder.Where(f => f.Abschnitt.Length == 0 || abschnitte.Contains(f.Abschnitt)).Select(f => f.Id).ToList();
    }

    /// <summary>
    /// parse_date: "TT.MM.JJJJ" oder "JJJJ-MM-TT" (strptime), sonst null. Wie Python: str.strip()
    /// (auch U+001C bis U+001F), und wo strptime \d nimmt, gilt jede Dezimalziffer (str.isdecimal,
    /// z. B. Vollbreite); die uebrigen Ziffern sind ASCII (Spezifikation G35, Teil datum).
    /// </summary>
    public static DateOnly? Datum(string? text)
    {
        text = PythonZeichen.Strip(text ?? "");
        const string Z = @"(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|\p{Nd})";   // \d von strptime, Wert unten
        const string T = "(3[0-1]|[1-2]" + Z + "|0[1-9]|[1-9]| [1-9])";
        const string M = "(1[0-2]|0[1-9]|[1-9])";
        const string J = "(" + Z + Z + Z + Z + ")";
        var m = Regex.Match(text, "^" + T + @"\." + M + @"\." + J + @"\z");
        if (m.Success && Tag(m.Groups[3].Value, m.Groups[2].Value, m.Groups[1].Value) is { } a)
        {
            return a;
        }
        m = Regex.Match(text, "^" + J + "-" + M + "-" + T + @"\z");
        return m.Success ? Tag(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value) : null;

        static DateOnly? Tag(string jahr, string monat, string tag)
        {
            if (Zahl(jahr) is not { } j || Zahl(monat) is not { } mo || Zahl(tag.TrimStart(' ')) is not { } t)
            {
                return null;
            }
            return j >= 1 && t <= DateTime.DaysInMonth(j, mo) ? new DateOnly(j, mo, t) : null;
        }

        // int() der Ziffern; null, wenn ein Zeichen fuer Python keine Dezimalziffer ist
        static int? Zahl(string ziffern)
        {
            var wert = 0;
            for (var i = 0; i < ziffern.Length; i += char.IsSurrogatePair(ziffern, i) ? 2 : 1)
            {
                var ziffer = PythonZeichen.Dezimalwert(char.ConvertToUtf32(ziffern, i));
                if (ziffer < 0)
                {
                    return null;
                }
                wert = wert * 10 + ziffer;
            }
            return wert;
        }
    }

    /// <summary>months_before: gleicher Tag vor n Monaten (am Monatsende gekappt).</summary>
    public static DateOnly MonateVorher(DateOnly tag, int monate)
    {
        var monat = tag.Month - monate;
        var jahr = tag.Year;
        while (monat < 1)
        {
            monat += 12;
            jahr -= 1;
        }
        return new DateOnly(jahr, monat, Math.Min(tag.Day, DateTime.DaysInMonth(jahr, monat)));
    }

    /// <summary>exam_phase: "ap1", "ap2" oder null (6 Monate vor einem Termin; vor AP1 gilt AP1).</summary>
    public static string? Pruefungsphase(RahmenplanWerte werte, DateOnly heute)
    {
        var ap1 = Datum(werte.TerminAp1);
        var ap2 = Datum(werte.TerminAp2);
        if (ap1 is { } a1 && heute < a1)
        {
            return heute >= MonateVorher(a1, PruefungsMonate) ? "ap1" : null;
        }
        if (ap2 is { } a2 && heute < a2 && heute >= MonateVorher(a2, PruefungsMonate))
        {
            return "ap2";
        }
        return null;
    }

    private int LernfeldStunden(string? thema, RahmenplanWerte werte)
    {
        var sichtbar = SichtbareLernfelder(werte);
        var stunden = (thema is not null && _themaLernfelder.TryGetValue(thema, out var felder) ? felder : [])
            .Where(sichtbar.Contains).Select(f => Lernfelder[f]).ToList();
        return stunden.Count > 0 ? stunden.Max() : 0;
    }

    /// <summary>item_weight: Gewicht eines Inhalts (etwa 1 bis 3, mit Pruefungsnaehe bis 4,5).</summary>
    public double Gewicht(string quelle, string schluessel, RahmenplanWerte werte, DateOnly heute)
    {
        var abschnitte = Abschnitte(werte);
        var punkte = Punkte(quelle, schluessel).Where(p => _punkte.TryGetValue(p, out var punkt) && abschnitte.Contains(punkt.Abschnitt))
            .Select(p => _punkte[p]).ToList();
        var wochen = punkte.Count > 0 ? punkte.Max(p => p.Wochen) : 0;
        var gewicht = 1.0 + 1.5 * Math.Sqrt(Math.Min(wochen, WochenMax) / WochenMax)
            + 0.5 * (LernfeldStunden(_thema.GetValueOrDefault((quelle, schluessel)), werte) / StundenMax);
        var phase = Pruefungsphase(werte, heute);
        if (phase == "ap1" && punkte.Any(p => p.Ap1))
        {
            gewicht *= PruefungsFaktor;
        }
        else if (phase == "ap2" && punkte.Any(p => p.Ap2))
        {
            gewicht *= PruefungsFaktor;
        }
        return gewicht;
    }

    /// <summary>_day_random: (0, 1], aus MD5 von "Datum|Quelle|Schluessel" (nur Reihenfolge, keine Sicherheit).</summary>
#pragma warning disable CA5351 // MD5 nur als Tageszufall wie in Python, keine Sicherheitsfunktion
    public static double Tageszufall(string quelle, string schluessel, DateOnly heute)
    {
        var text = heute.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + quelle + "|" + schluessel;
        var hex = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(text)));
        return (long.Parse(hex[..13], NumberStyles.HexNumber, CultureInfo.InvariantCulture) + 1) / (double)(4503599627370496L + 1);
    }
#pragma warning restore CA5351

    /// <summary>weighted_order: Schluessel in gewichteter Zufallsreihenfolge (Efraimidis-Spirakis), absteigend, stabil.</summary>
    public List<string> GewichteteReihenfolge(string quelle, IEnumerable<string> schluessel, RahmenplanWerte? werte, DateOnly heute)
    {
        werte ??= RahmenplanWerte.Vorgabe;
        return schluessel.Select(s => (Wert: PythonPow.Pow(Tageszufall(quelle, s, heute), 1.0 / Gewicht(quelle, s, werte, heute)), Schluessel: s))
            .OrderByDescending(x => x.Wert).Select(x => x.Schluessel).ToList();
    }

    /// <summary>fresh_order: Reihenfolge der neuen Inhalte fuer BevorzugteReihenfolge, null wenn die Gewichtung aus ist.</summary>
    public Func<List<string>, List<string>>? NeuReihenfolge(string quelle, RahmenplanWerte werte, DateOnly heute) =>
        werte.Gewichtung ? schluessel => GewichteteReihenfolge(quelle, schluessel, werte, heute) : null;
}
