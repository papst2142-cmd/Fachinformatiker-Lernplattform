using System.Text.RegularExpressions;

namespace Lernplattform.Core.Diagnose;

/// <summary>
/// Entfernt Zugangsschluessel, Passwoerter und Benutzernamen in Pfaden aus
/// Texten (Fehlerprotokoll, spaeter Problembericht). Gleiche Regeln und
/// gleiche Ergebnisse wie Python fisi_diagnose.scrub/secrets_of und
/// fisi_core.anonymize_paths (Vergleichswerte G10).
/// </summary>
public static class Datenschutz
{
    public const string Entfernt = "[entfernt]";
    public const string Auslassung = "…";

    /// <summary>Einstellungs-Schluessel, deren Werte nie in einem Bericht stehen duerfen.</summary>
    private static readonly Regex GeheimerSchluessel =
        new(@"token|passw|kennwort|secret|geheim|api[_-]?key", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Muster fuer Zugangsdaten im freien Text, Reihenfolge wie Python _PATTERNS
    private static readonly (Regex Muster, string Ersatz)[] Muster =
    [
        // GitHub-Zugangsschluessel (klassisch und fein eingestellt)
        (new Regex(@"\b(?:gh[pousr]_[A-Za-z0-9]{16,}|github_pat_[A-Za-z0-9_]{16,})"), Entfernt),
        // Kopfzeile "Authorization: Bearer ..."
        (new Regex(@"(?i)(authorization['""]?\s*[:=]\s*['""]?)(?:bearer\s+|token\s+|basic\s+)?[^\s'"",}]+"), "${1}" + Entfernt),
        (new Regex(@"(?i)\b(bearer\s+)[A-Za-z0-9._~+/=-]{8,}"), "${1}" + Entfernt),
        // Schluessel = Wert bzw. "schluessel": "wert" (Schluessel endet auf token,
        // kennwort, secret, api-key oder enthaelt "passw" im letzten Teil ohne "-")
        (new Regex(@"(?i)([""']?(?<![\w-])"
                   + @"(?:[\w-]+(?:(?<=token)|(?<=kennwort)|(?<=secret)|(?<=apikey)|(?<=api_key)"
                   + @"|(?<=api-key))|(?:[\w-]*-)?(?:(?!passw)\w)*passw\w*)"
                   + @"[""']?\s*[:=]\s*)(""[^""]*""|'[^']*'|[^\s,;}]+)"), "${1}" + Entfernt),
        // Zugangsdaten in Adressen: https://name:passwort@host oder https://token@host
        (new Regex(@"(?i)\b(https?://)[^\s/@]+@"), "${1}" + Entfernt + "@"),
    ];

    private static readonly Regex WindowsBenutzerpfad = new(
        @"(?i)((?:\b[a-z]:)?\\(?:users|benutzer|documents and settings)\\|"
        + @"\b[a-z]:/(?:users|benutzer|documents and settings)/)([^\\/\r\n""'<>|:;,]+)");

    private static readonly Regex UnixBenutzerpfad = new(@"(/home/|/Users/)([^/\s""':;,]+)");

    /// <summary>Werte geheimer Einstellungen (mindestens 6 Zeichen), laengste zuerst.</summary>
    public static IReadOnlyList<string> Geheimnisse(IEnumerable<KeyValuePair<string, object?>> einstellungen)
    {
        var werte = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (schluessel, wert) in einstellungen)
        {
            if (GeheimerSchluessel.IsMatch(schluessel) && wert is string text && PythonStrip(text).Length >= 6)
            {
                werte.Add(PythonStrip(text));
            }
        }
        return werte.OrderByDescending(w => w.Length).ThenBy(w => w, StringComparer.Ordinal).ToList();
    }

    /// <summary>Entfernt zuerst die bekannten Geheimnisse, dann alles, was nach Zugangsdaten aussieht.</summary>
    public static string Schwaerzen(string text, IEnumerable<string>? geheimnisse = null)
    {
        foreach (var geheim in geheimnisse ?? [])
        {
            if (geheim.Length > 0)
            {
                text = text.Replace(geheim, Entfernt, StringComparison.Ordinal);
            }
        }
        foreach (var (muster, ersatz) in Muster)
        {
            text = muster.Replace(text, ersatz);
        }
        return text;
    }

    /// <summary>
    /// Ersetzt Benutzerpfade: bekannte Ordner durch Platzhalter (%APPDATA%,
    /// %LOCALAPPDATA%, ~ fuer das Benutzerverzeichnis), alle uebrigen
    /// Benutzernamen in Pfaden durch "…". umgebung/heim nur fuer Tests.
    /// </summary>
    public static string PfadeAnonymisieren(string text, IReadOnlyDictionary<string, string>? umgebung = null, string? heim = null)
    {
        string? Umgebung(string name) => umgebung is null
            ? Environment.GetEnvironmentVariable(name)
            : umgebung.TryGetValue(name, out var wert) ? wert : null;
        heim ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (heim.Length == 0)
        {
            heim = "~";
        }

        var bekannt = new List<(string Pfad, string Platzhalter)>();
        foreach (var name in new[] { "LOCALAPPDATA", "APPDATA" })
        {
            if (Umgebung(name) is { Length: > 0 } wert)
            {
                bekannt.AddRange(Varianten(wert).Select(v => (v, "%" + name + "%")));
            }
        }
        if (heim is not ("~" or "/" or "\\"))
        {
            bekannt.AddRange(Varianten(heim).Select(v => (v, "~")));
        }
        // stabil sortieren wie Python list.sort(key=len, reverse=True)
        bekannt = bekannt.Select((e, i) => (e, i)).OrderByDescending(x => x.e.Pfad.Length).ThenBy(x => x.i).Select(x => x.e).ToList();
        foreach (var (pfad, platzhalter) in bekannt)
        {
            text = Regex.Replace(text, Regex.Escape(pfad) + @"(?=[\\/\s""':;,)]|$)",
                _ => platzhalter, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        foreach (var muster in new[] { WindowsBenutzerpfad, UnixBenutzerpfad })
        {
            text = muster.Replace(text, m => m.Groups[1].Value + Auslassung);
        }
        return text;
    }

    private static List<string> Varianten(string pfad)
    {
        pfad = pfad.TrimEnd('\\', '/');
        var varianten = new List<string>();
        foreach (var v in new[] { pfad, pfad.Replace('\\', '/'), pfad.Replace('/', '\\') })
        {
            if (v.Length > 3 && !varianten.Contains(v))
            {
                varianten.Add(v);
            }
        }
        return varianten;
    }

    /// <summary>Wie Pythons str.strip(): Leerraum an beiden Enden entfernen.</summary>
    internal static string PythonStrip(string text) => text.Trim();
}
