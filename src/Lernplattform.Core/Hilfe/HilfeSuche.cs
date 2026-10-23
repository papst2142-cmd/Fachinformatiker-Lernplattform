using Lernplattform.Core.Lernen;
using Lernplattform.Core.Suche;

namespace Lernplattform.Core.Hilfe;

public static partial class HilfeTexte
{
    /// <summary>Abschnitt zur Kennung oder null.</summary>
    public static HilfeAbschnitt? Abschnitt(string id) => Abschnitte.FirstOrDefault(a => a.Id == id);

    /// <summary>
    /// search_help: Abschnitte, deren Titel oder Text (mit " " statt "\n") die Anfrage enthalten,
    /// in der Reihenfolge der Hilfe. Nadel = klein(Strip(anfrage)), leer = keine Treffer.
    /// Treffer (SEARCH_KIND, ohne Fachbereich, Titel, Text). Gesucht wird in allen 16 Abschnitten.
    /// </summary>
    public static List<SuchTreffer> Treffer(string anfrage, Func<string, string> klein)
    {
        var treffer = new List<SuchTreffer>();
        var nadel = klein(Lernregeln.Strip(anfrage));
        if (nadel.Length == 0)
        {
            return treffer;
        }
        foreach (var abschnitt in Abschnitte)
        {
            var text = abschnitt.Text.Replace("\n", " ", StringComparison.Ordinal);
            if (klein(abschnitt.Titel).Contains(nadel, StringComparison.Ordinal) || klein(text).Contains(nadel, StringComparison.Ordinal))
            {
                treffer.Add(new SuchTreffer(RundgangTexte.SucheArt, null, abschnitt.Titel, text));
            }
        }
        return treffer;
    }
}
