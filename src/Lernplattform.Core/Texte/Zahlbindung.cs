using System.Text.RegularExpressions;
using Lernplattform.Core.Rechner;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 11 FW7 (Sammelliste 40): in Fliesstexten der Rechner (Aufgabentext des Rechen-Trainers)
/// bleibt eine Einheit bei ihrer Zahl ("50 %", "994 MB", "16 Mbit/s"). Das Leerzeichen
/// dazwischen wird in der ANZEIGE ein geschuetztes Leerzeichen (U+00A0), an dem die Oberflaeche
/// nicht umbricht. Der Wortlaut bleibt gleich; gespeichert und verglichen wird weiter der Text
/// mit normalem Leerzeichen (wie Python).
/// </summary>
public static class Zahlbindung
{
    /// <summary>Geschuetztes Leerzeichen.</summary>
    public const char Geschuetzt = ' ';

    /// <summary>Einheiten, die bei der Zahl davor bleiben.</summary>
    public static readonly IReadOnlyList<string> Einheiten =
        [.. new[] { "%", "€", "bit/s" }.Concat(Datenmenge.Einheiten).Concat(Uebertragung.Einheiten).Concat(Uebertragung.Raten)
            .Distinct(StringComparer.Ordinal).OrderByDescending(e => e.Length).ThenBy(e => e, StringComparer.Ordinal)];

    private static readonly Regex Muster = new(
        "(?<=[0-9]) (?=(?:" + string.Join("|", Einheiten.Select(Regex.Escape)) + ")(?![\\p{L}\\p{N}]))",
        RegexOptions.CultureInvariant);

    /// <summary>Leerzeichen zwischen Zahl und Einheit schuetzen.</summary>
    public static string Binden(string text) => string.IsNullOrEmpty(text) ? text : Muster.Replace(text, Geschuetzt.ToString());
}
