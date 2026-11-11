using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Phase 13 Welle 3 (Sammelliste 219 Punkt 2): Name einer Ware so, wie das Spiel sie zeigt, fuer Pruefungen und Auswahl
/// im Stadtbau-Werkzeug. Kennungen wie in innenraeume.json "ware": "teile:&lt;Typ&gt;" (Name des Teile-Typs),
/// "kleidung:&lt;Stelle&gt;" (Abteilung im Kaufhaus), sonst die Kennung eines Moebels (Name im Sortiment). Unbekannt: die Kennung.
/// </summary>
public static class Warennamen
{
    public static string Name(ISortiment geschaefte, string ort, string kennung)
    {
        if (kennung.StartsWith("teile:", StringComparison.Ordinal))
        {
            return SpielKern.Regeln.TeilName(kennung["teile:".Length..], geschaefte.Inhalt);
        }
        if (kennung.StartsWith(SpielKern.KleidungArt + ":", StringComparison.Ordinal))
        {
            return SpielKern.Regeln.Abteilung(geschaefte.Daten, kennung[(SpielKern.KleidungArt.Length + 1)..]);
        }
        return geschaefte.MoebelName(ort, kennung)
            ?? SpielKern.Laeden.Select(l => geschaefte.MoebelName(l, kennung)).FirstOrDefault(n => n is not null)
            ?? kennung;
    }

    /// <summary>Zusatz fuer den Tipp: die Kennung klein dahinter (nur wenn sie vom Namen abweicht).</summary>
    public static string Kennung(string name, string kennung) => name == kennung ? "" : " (Kennung: " + kennung + ")";
}
