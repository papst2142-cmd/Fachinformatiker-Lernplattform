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

    /// <summary>
    /// Sammelliste 177 Teil 2 (Nico 02.11.2026 Antwort b, Weisung P11-12): der Text als Folge von Teilen, je Teil
    /// Fliesstext oder genau eine Tabelle (ihre Zeilen ohne Zeilenumbruch am Ende). Der Zeilenumbruch, der einen Teil
    /// vom naechsten trennt, gehoert zu keinem Teil; Leerzeilen davor oder danach bleiben im Fliesstext.
    /// Zusammengesetzt mit je einem "\n" zwischen den Teilen ergibt sich wieder genau der Text (ohne "\r" vor dem
    /// Umbruch einer Tabellenzeile). Ein Text ohne Tabelle ist ein einziger Teil.
    /// </summary>
    public static IReadOnlyList<(string Text, bool Tabelle)> Teile(string? text)
    {
        var teile = new List<(string Text, bool Tabelle)>();
        text ??= "";
        var pos = 0;
        foreach (var block in Bloecke(text))
        {
            var start = block[0].Start;
            if (start > pos)
            {
                // ohne den Umbruch direkt vor der Tabelle (gehoert zur Trennung)
                var davor = text[pos..(start - 1)];
                teile.Add((davor.EndsWith('\r') ? davor[..^1] : davor, false));
            }
            teile.Add((string.Join("\n", block.Select(z => text.Substring(z.Start, z.Laenge))), true));
            var ende = block[^1].Start + block[^1].Laenge;
            if (ende < text.Length && text[ende] == '\r')
            {
                ende++;
            }
            // der Umbruch nach der Tabelle trennt sie vom naechsten Teil
            pos = ende + 1;
        }
        if (pos <= text.Length)
        {
            teile.Add((text[pos..], false));
        }
        return teile;
    }

    /// <summary>
    /// Sammelliste 177 Teil 2: Schriftgroesse einer Tabelle mit <paramref name="zeichen"/> Zeichen in der laengsten
    /// Zeile. Passt sie in der Groesse des Textes (<paramref name="soll"/>) nicht in <paramref name="breite"/>, wird sie
    /// kleiner gesetzt, hoechstens bis <paramref name="untergrenze"/> (Stufe Klein im Verhaeltnis zum Text). Reicht
    /// auch das nicht, bleibt sie in der Untergrenze ohne Umbruch und laesst sich seitlich wischen
    /// (<c>Wischen</c> = true, Hinweis wie bei den Rechner-Ergebnissen). <paramref name="jeZeichen"/> ist die Breite
    /// eines Zeichens der Festbreitenschrift je Punkt Schriftgroesse.
    /// </summary>
    public static (double Groesse, bool Wischen) Schriftgroesse(double soll, double untergrenze, int zeichen, double jeZeichen, double breite)
    {
        if (double.IsInfinity(breite) || breite <= 0 || zeichen <= 0 || jeZeichen <= 0)
        {
            return (soll, false);
        }
        var passt = Math.Floor(breite / (zeichen * jeZeichen) * 10) / 10 - 0.1;
        if (passt >= soll)
        {
            return (soll, false);
        }
        return passt >= untergrenze ? (passt, false) : (Math.Min(soll, untergrenze), true);
    }

    // nach dem ersten sichtbaren Zeichen: "  " vor einem weiteren sichtbaren Zeichen oder ein "|"
    private static bool IstTabellenzeile(ReadOnlySpan<char> zeile)
    {
        var inhalt = zeile.TrimStart(' ').TrimEnd();
        return inhalt.Contains('|') || inhalt.Contains("  ", StringComparison.Ordinal);
    }
}
