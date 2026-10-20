using System.Text;

namespace Lernplattform.Core.Suche;

/// <summary>
/// Phase 2, L8: Pythons str.lower() nachgebaut (Python 3.12, Unicode 15.0), damit die
/// Suche genau dieselben Treffer findet wie die Python-Linie. Nicht ToLowerInvariant:
/// .NET weicht bei einzelnen Zeichen ab (z. B. "İ" wird bei Python zu "i" mit Punkt
/// oben, das grosse Sigma am Wortende zu "ς"). Die Tabelle steht in
/// PythonKlein.Tabelle.cs (erzeugt, geprueft gegen G19).
/// </summary>
public static partial class PythonKlein
{
    private const int Sigma = 0x03A3;
    private const int SigmaKlein = 0x03C3;
    private const int SigmaSchluss = 0x03C2;

    private static readonly Lazy<Dictionary<int, string>> Zuordnung = new(Aufbauen);

    private static Dictionary<int, string> Aufbauen()
    {
        var zuordnung = new Dictionary<int, string>();
        for (var i = 0; i < Laeufe.Length; i += 4)
        {
            int start = Laeufe[i], anzahl = Laeufe[i + 1], schritt = Laeufe[i + 2], abstand = Laeufe[i + 3];
            for (var n = 0; n < anzahl; n++)
            {
                var zeichen = start + n * schritt;
                zuordnung[zeichen] = char.ConvertFromUtf32(zeichen + abstand);
            }
        }
        foreach (var (zeichen, klein) in Mehrfach)
        {
            zuordnung[zeichen] = klein;
        }
        return zuordnung;
    }

    /// <summary>Alle Zeichen, die lower() aendert, mit Ergebnis (fuer den Vergleich mit G19).</summary>
    public static IReadOnlyDictionary<int, string> Tabelle => Zuordnung.Value;

    /// <summary>Bereiche (von, bis) "gross/klein, nicht uebergehbar" und "uebergehbar" (fuer den Vergleich mit G19).</summary>
    internal static (IReadOnlyList<int> GrossKlein, IReadOnlyList<int> Uebergehbar) SigmaBereiche => (GrossKlein, Uebergehbar);

    /// <summary>str.lower() wie Python, auch das Schluss-Sigma (Final_Sigma).</summary>
    public static string Klein(string text)
    {
        // Schnellweg: reiner ASCII-Text ohne Grossbuchstaben bleibt gleich
        var i = 0;
        while (i < text.Length && text[i] < 0x80 && text[i] is not (>= 'A' and <= 'Z'))
        {
            i++;
        }
        if (i == text.Length)
        {
            return text;
        }
        var zuordnung = Zuordnung.Value;
        var ergebnis = new StringBuilder(text.Length);
        ergebnis.Append(text, 0, i);
        while (i < text.Length)
        {
            if (Rune.DecodeFromUtf16(text.AsSpan(i), out var zeichen, out var laenge) != System.Buffers.OperationStatus.Done)
            {
                ergebnis.Append(text[i]);       // einzelnes Ersatzzeichen bleibt wie in Python
                i++;
                continue;
            }
            var wert = zeichen.Value;
            if (wert == Sigma)
            {
                ergebnis.Append((char)(Schluss(text, i) ? SigmaSchluss : SigmaKlein));
            }
            else if (zuordnung.TryGetValue(wert, out var klein))
            {
                ergebnis.Append(klein);
            }
            else
            {
                ergebnis.Append(text, i, laenge);
            }
            i += laenge;
        }
        return ergebnis.ToString();
    }

    /// <summary>
    /// handle_capital_sigma (CPython unicodeobject.c): vor dem Sigma steht (ueber
    /// uebergehbare Zeichen hinweg) ein Zeichen mit Gross-/Kleinschreibung, danach nicht.
    /// </summary>
    private static bool Schluss(string text, int stelle)
    {
        var j = stelle;
        int davor = -1;
        while (j > 0)
        {
            var gelesen = Rune.DecodeLastFromUtf16(text.AsSpan(0, j), out var zeichen, out var laenge);
            j -= Math.Max(1, laenge);
            var wert = gelesen == System.Buffers.OperationStatus.Done ? zeichen.Value : 0xD800;
            if (!In(Uebergehbar, wert))
            {
                davor = wert;
                break;
            }
        }
        if (davor < 0 || !In(GrossKlein, davor))
        {
            return false;
        }
        j = stelle + 1;
        while (j < text.Length)
        {
            var gelesen = Rune.DecodeFromUtf16(text.AsSpan(j), out var zeichen, out var laenge);
            var wert = gelesen == System.Buffers.OperationStatus.Done ? zeichen.Value : 0xD800;
            if (!In(Uebergehbar, wert))
            {
                return !In(GrossKlein, wert);
            }
            j += Math.Max(1, laenge);
        }
        return true;
    }

    /// <summary>Liegt der Wert in einem der Bereiche (von, bis), aufsteigend sortiert?</summary>
    private static bool In(int[] bereiche, int wert)
    {
        int unten = 0, oben = bereiche.Length / 2 - 1;
        while (unten <= oben)
        {
            var mitte = (unten + oben) / 2;
            if (wert < bereiche[2 * mitte])
            {
                oben = mitte - 1;
            }
            else if (wert > bereiche[2 * mitte + 1])
            {
                unten = mitte + 1;
            }
            else
            {
                return true;
            }
        }
        return false;
    }
}
