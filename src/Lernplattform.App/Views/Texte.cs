using Lernplattform.Core.Texte;

namespace Lernplattform.Views;

/// <summary>Beschriftungen fuer die Oberflaeche aus dem Core (Vergleichswerte G9).</summary>
public static class Texte
{
    public const string Programmname = UeberTexte.Programmname;

    public static IReadOnlyList<NavPunkt> NavigationPc { get; } = Navigation.Pc.Select(Punkt).ToList();

    public static IReadOnlyList<NavPunkt> NavigationHandy { get; } = Navigation.Handy.Select(Punkt).ToList();

    private static NavPunkt Punkt(NavEintrag e) => new(e.Id, e.Titel, e.Unterpunkte.Select(Punkt).ToList(), e.Symbol, e.SymbolAktiv ?? e.Symbol);

    /// <summary>Welcher Eintrag der unteren Leiste zu einer Seite gehoert (Lernbereiche: "Lernen").</summary>
    public static string HandyZiel(string id)
    {
        if (NavigationHandy.Any(p => p.Id == id))
        {
            return id;
        }
        if (Navigation.Lernen.Any(e => e.Id == id))
        {
            return Seiten.Lernen;
        }
        var pc = NavigationPc.FirstOrDefault(p => p.Unter.Any(u => u.Id == id));
        return pc is not null ? HandyZiel(pc.Id) : "";
    }
}
