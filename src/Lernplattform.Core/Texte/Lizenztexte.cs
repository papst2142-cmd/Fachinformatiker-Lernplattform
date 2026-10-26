using System.Text.RegularExpressions;

namespace Lernplattform.Core.Texte;

/// <summary>Ein Bestandteil aus THIRD_PARTY_LICENSES.txt: Titel ("Name (Version), Lizenz X") und sein Text in Bloecken.</summary>
public sealed record LizenzTeil(string Titel, IReadOnlyList<string> Bloecke);

/// <summary>
/// Phase 8 W2 (Weisung P8-3, Sammelliste 74, Nico 5a): THIRD_PARTY_LICENSES.txt (von
/// werkzeuge/lizenzen/erzeuge_lizenzen.py volltexte geschrieben, im Programm eingebettet) fuer den Knopf
/// "Lizenzen" zerlegen. Die Datei ist ueber 1 MB gross (allein Velopack fast 1 MB); damit die Anzeige
/// nur sichtbare Teile zeichnen muss, steht der Text je Bestandteil in Bloecken von hoechstens
/// <see cref="BlockGroesse"/> Zeichen (Schnitt nur an Zeilenenden, bevorzugt an Leerzeilen).
/// Verlustfrei: Vorspann und je Teil Trennzeile, Titel und Bloecke mit "\n" verbunden ergeben die Datei.
/// </summary>
public static partial class Lizenztexte
{
    /// <summary>Trennzeile vor jedem Bestandteil (volltexte: "#" * 78).</summary>
    public static readonly string Trenner = new('#', 78);

    /// <summary>Hoechstens so viele Zeichen je Block (eine einzelne laengere Zeile bleibt ganz).</summary>
    public const int BlockGroesse = 2000;

    [GeneratedRegex(@"^.+ \(.+\), Lizenz .+$")]
    private static partial Regex TitelMuster();

    /// <summary>Vorspann (Text vor dem ersten Bestandteil) und alle Bestandteile in der Reihenfolge der Datei.</summary>
    public static (string Vorspann, IReadOnlyList<LizenzTeil> Teile) Zerlegen(string text)
    {
        var zeilen = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var starts = new List<int>();
        for (var i = 0; i + 1 < zeilen.Length; i++)
        {
            // ein Lizenztext kann selbst eine Zeile aus "#" enthalten; ein Bestandteil beginnt nur mit Trenner + Titel
            if (zeilen[i] == Trenner && TitelMuster().IsMatch(zeilen[i + 1]))
            {
                starts.Add(i);
            }
        }
        var ende = starts.Count > 0 ? starts[0] : zeilen.Length;
        var vorspann = string.Join('\n', zeilen[..ende]);
        var teile = new List<LizenzTeil>(starts.Count);
        for (var n = 0; n < starts.Count; n++)
        {
            var von = starts[n] + 2;
            var bis = n + 1 < starts.Count ? starts[n + 1] : zeilen.Length;
            teile.Add(new LizenzTeil(zeilen[starts[n] + 1], Bloecke(zeilen[von..bis])));
        }
        return (vorspann, teile);
    }

    /// <summary>
    /// Titel zur Anzeige: Paketnamen wie "Xamarin.AndroidX.Lifecycle.ViewModelSavedState" duerfen nach ".",
    /// "-", "/", "_" und vor einem Grossbuchstaben nach einem Kleinbuchstaben umbrechen (U+200B ohne Breite),
    /// sonst trennt die schmale Anzeige mitten im Wort.
    /// </summary>
    public static string Brechbar(string titel)
    {
        var aus = new System.Text.StringBuilder(titel.Length + 16);
        for (var i = 0; i < titel.Length; i++)
        {
            // auch zwischen Klein- und Grossbuchstabe ("ViewModel|Saved|State"), sonst passt ein Teil in der
            // groessten Schrift nicht in die schmale Anzeige
            if (i > 0 && char.IsUpper(titel[i]) && char.IsLower(titel[i - 1]))
            {
                aus.Append('\u200B');
            }
            aus.Append(titel[i]);
            if (titel[i] is '.' or '-' or '/' or '_')
            {
                aus.Append('\u200B');
            }
        }
        return aus.ToString();
    }

    /// <summary>Zeilen zu Bloecken: neuer Block an einer Leerzeile ab der halben Groesse, sonst vor der Grenze.</summary>
    private static List<string> Bloecke(string[] zeilen)
    {
        var bloecke = new List<string>();
        var block = new List<string>();
        var laenge = 0;
        foreach (var zeile in zeilen)
        {
            if (block.Count > 0 && laenge + zeile.Length + 1 > BlockGroesse)
            {
                bloecke.Add(string.Join('\n', block));
                block.Clear();
                laenge = 0;
            }
            block.Add(zeile);
            laenge += zeile.Length + 1;
            if (zeile.Length == 0 && laenge >= BlockGroesse / 2)
            {
                bloecke.Add(string.Join('\n', block));
                block.Clear();
                laenge = 0;
            }
        }
        if (block.Count > 0)
        {
            bloecke.Add(string.Join('\n', block));
        }
        return bloecke;
    }
}
