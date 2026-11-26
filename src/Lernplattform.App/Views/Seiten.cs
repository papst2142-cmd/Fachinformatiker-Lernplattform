using Lernplattform.Core.Texte;

namespace Lernplattform.Views;

/// <summary>Kennungen der Seiten wie in der Python-Linie (app_gui.NAV_ITEMS, main.NAV).</summary>
public static class Seiten
{
    public const string Dashboard = "dashboard";
    public const string Lernen = "learn";
    public const string Karteikarten = "cards";
    public const string Trainer = "quiz";
    public const string Optionen = "settings";
    public const string Hilfe = "help";
    public const string Suche = "search";
    public const string Fortschritt = "progress";
    public const string Ap1Szenarien = "ap1scenarios";
    public const string Ap2Szenarien = "scenarios";
    public const string Testprojekt = "testproject";
    public const string Notizblock = "notebook";
    public const string Rechner = "calc";
    public const string Abschluss = "abschluss";
    public const string Spiel = "game";

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
            Pruefungsteile.Teil1 => Pruefungsteile.Teil1Titel,
            Pruefungsteile.Teil2 => Pruefungsteile.Teil2Titel,
            Pruefungsteile.B1 or Pruefungsteile.B2 or Pruefungsteile.Wiso => Pruefungsteile.BereichKurz(id),
            Pruefungsteile.Mehr => Pruefungsteile.MehrTitel,
            Suche => Navigation.SucheHandy,
            Hilfe => Navigation.Hilfe,
            _ => id,
        };
    }
}
