using Lernplattform.Core.Texte;

namespace Lernplattform.Views;

/// <summary>Kennungen der Seiten wie in der Python-Linie (app_gui.NAV_ITEMS, main.NAV).</summary>
public static class Seiten
{
    public const string Dashboard = "dashboard";
    public const string Lernen = "learn";
    public const string Optionen = "settings";
    public const string Hilfe = "help";
    public const string Suche = "search";

    /// <summary>Titel einer Seite fuer Kopfleiste und Platzhalter (Beschriftungen aus G9, PC-Namen).</summary>
    public static string Titel(string id)
    {
        foreach (var punkt in Texte.NavigationPc.SelectMany(p => p.Unter.Prepend(p)))
        {
            if (punkt.Id == id)
            {
                return punkt.Titel;
            }
        }
        return id switch
        {
            Lernen => Texte.NavigationHandy.First(p => p.Id == Lernen).Titel,
            Suche => Navigation.SucheHandy,
            Hilfe => Navigation.Hilfe,
            _ => id,
        };
    }
}
