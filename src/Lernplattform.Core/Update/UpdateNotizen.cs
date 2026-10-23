using Lernplattform.Core.Rechner;

namespace Lernplattform.Core.Update;

/// <summary>
/// Phase 5 BE9: plain_notes (fisi_update.py): Release-Text (Markdown aus AENDERUNGEN.md) lesbar fuer
/// ein einfaches Textfeld. Aufzaehlungen mit "• ", keine Sternchen, fliessender Text statt harter
/// Zeilenumbrueche; der Text endet vor "### Herunterladen".
/// </summary>
public static class UpdateNotizen
{
    public const string Ende = "### Herunterladen";

    public static string Lesbar(string? markdown)
    {
        var punkte = new List<string>();
        foreach (var zeile in (markdown ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var ohne = PythonZeichen.Strip(zeile);
            if (ohne == Ende)
            {
                break;
            }
            if (ohne.Length == 0)
            {
                punkte.Add("");
            }
            else if (ohne.StartsWith("- ", StringComparison.Ordinal) || ohne.StartsWith("* ", StringComparison.Ordinal))
            {
                punkte.Add("• " + ohne[2..]);
            }
            else if (punkte.Count > 0 && punkte[^1].Length > 0)
            {
                punkte[^1] += " " + ohne;
            }
            else
            {
                punkte.Add(ohne);
            }
        }
        var text = string.Join("\n", punkte.Where(p => p.Length > 0));
        foreach (var zeichen in new[] { "**", "__", "`" })
        {
            text = text.Replace(zeichen, "", StringComparison.Ordinal);
        }
        return text;
    }
}
