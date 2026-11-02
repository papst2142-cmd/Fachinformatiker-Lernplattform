namespace Lernplattform.Core.Inhalte;

/// <summary>
/// Sammelliste 177 (Nico 02.11.2026 Antwort 177a, Weisung P11-4): eingerueckte Tabellen in Aufgaben und
/// Musterloesungen ("  Kriterium     Gewicht  IDE A") stehen nur in einer Schrift mit fester Zeichenbreite genau
/// untereinander. Ein Block ist eine Folge von Zeilen, die mit einem Leerzeichen beginnen; er gilt als Tabelle,
/// wenn eine seiner Zeilen nach dem ersten Zeichen noch zwei Leerzeichen hintereinander oder ein "|" enthaelt.
/// Eingerueckte Aufzaehlungen ("  - A (Verwaltung): 172.16.0.0/23") bleiben in der normalen Schrift.
/// Nur Darstellung: die Texte selbst bleiben unveraendert.
/// </summary>
public static class Tabellenzeilen
{
    /// <summary>Bereiche (Start, Laenge) der Tabellenzeilen im Text, ohne Zeilenumbruch, aufsteigend.</summary>
    public static IReadOnlyList<(int Start, int Laenge)> Bereiche(string? text) => [.. Bloecke(text).SelectMany(b => b)];

    /// <summary>Die Tabellen des Textes, je Tabelle ihre Zeilen (Start, Laenge) ohne Zeilenumbruch, aufsteigend.</summary>
    public static IReadOnlyList<IReadOnlyList<(int Start, int Laenge)>> Bloecke(string? text)
    {
        var ergebnis = new List<IReadOnlyList<(int Start, int Laenge)>>();
        if (string.IsNullOrEmpty(text))
        {
            return ergebnis;
        }
        var block = new List<(int Start, int Laenge)>();
        var tabelle = false;
        void Abschliessen()
        {
            if (tabelle)
            {
                ergebnis.Add([.. block]);
            }
            block.Clear();
            tabelle = false;
        }
        var start = 0;
        while (start <= text.Length)
        {
            var ende = text.IndexOf('\n', start);
            if (ende < 0)
            {
                ende = text.Length;
            }
            var laenge = ende - start;
            if (laenge > 0 && text[ende - 1] == '\r')
            {
                laenge--;
            }
            if (laenge > 0 && text[start] == ' ')
            {
                block.Add((start, laenge));
                tabelle |= IstTabellenzeile(text.AsSpan(start, laenge));
            }
            else
            {
                Abschliessen();
            }
            start = ende + 1;
        }
        Abschliessen();
        return ergebnis;
    }

    // nach dem ersten sichtbaren Zeichen: "  " vor einem weiteren sichtbaren Zeichen oder ein "|"
    private static bool IstTabellenzeile(ReadOnlySpan<char> zeile)
    {
        var inhalt = zeile.TrimStart(' ').TrimEnd();
        return inhalt.Contains('|') || inhalt.Contains("  ", StringComparison.Ordinal);
    }
}
