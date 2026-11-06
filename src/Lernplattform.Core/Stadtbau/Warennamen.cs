using Lernplattform.Core.Spiel;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Phase 13 Welle 3 (Sammelliste 219 Punkt 2): Name einer Ware so, wie das Spiel sie zeigt, fuer Pruefungen und Auswahl
/// im Stadtbau-Werkzeug. Kennungen wie in innenraeume.json "ware": "teile:&lt;Typ&gt;" (Name des Teile-Typs),
/// "kleidung:&lt;Stelle&gt;" (Abteilung im Kaufhaus), sonst die Kennung eines Moebels (Name im Sortiment). Unbekannt: die Kennung.
/// </summary>
public static class Warennamen
{
    public static string Name(Geschaefte geschaefte, string ort, string kennung)
    {
        if (kennung.StartsWith("teile:", StringComparison.Ordinal))
        {
            return Geschaefte.TypName(kennung["teile:".Length..], geschaefte.Inhalt);
        }
        if (kennung.StartsWith(Kleidung.Art + ":", StringComparison.Ordinal))
        {
            return Kleidung.Abteilung(geschaefte.Daten, kennung[(Kleidung.Art.Length + 1)..]);
        }
        return geschaefte.Finden(ort, Geschaefte.ArtMoebel, kennung)?.Name
            ?? Geschaefte.Laeden.Select(l => geschaefte.Finden(l, Geschaefte.ArtMoebel, kennung)?.Name).FirstOrDefault(n => n is not null)
            ?? kennung;
    }

    /// <summary>Zusatz fuer den Tipp: die Kennung klein dahinter (nur wenn sie vom Namen abweicht).</summary>
    public static string Kennung(string name, string kennung) => name == kennung ? "" : " (Kennung: " + kennung + ")";
}
