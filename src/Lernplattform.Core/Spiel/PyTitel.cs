using System.Text;
using Lernplattform.Core.Suche;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 11 FW2 (Sammelliste 80): Pythons str.capitalize() (Python 3.12, Unicode 15.0). Das erste
/// Zeichen bekommt die Titelschreibung mit voller Zuordnung ("ß" wird "Ss", "ǆ" wird "ǅ"), der Rest
/// wird wie str.lower() klein (<see cref="PythonKlein"/>, mit Schluss-Sigma im ganzen Text).
/// Nicht ToUpperInvariant: .NET laesst "ß" stehen. Die Tabelle steht in PyTitel.Tabelle.cs (erzeugt).
/// </summary>
public static partial class PyTitel
{
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
        foreach (var (zeichen, titel) in Mehrfach)
        {
            zuordnung[zeichen] = titel;
        }
        return zuordnung;
    }

    /// <summary>Alle Zeichen, deren Titelschreibung anders ist, mit Ergebnis (fuer Tests).</summary>
    public static IReadOnlyDictionary<int, string> Tabelle => Zuordnung.Value;

    /// <summary>str.capitalize() wie Python 3.12.</summary>
    public static string Capitalize(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }
        // einzelnes Ersatzzeichen bleibt wie in Python stehen
        var laenge = Rune.DecodeFromUtf16(text, out var erstes, out var gelesen) == System.Buffers.OperationStatus.Done ? gelesen : 1;
        var kopf = text[..laenge];
        var titel = laenge == gelesen && Zuordnung.Value.TryGetValue(erstes.Value, out var t) ? t : kopf;
        // lower() ueber den ganzen Text, damit das Schluss-Sigma seinen Vorgaenger sieht; der Kopf allein
        // wird klein genauso lang wie im ganzen Text (nur das Sigma haengt am Umfeld, an Stelle 0 nie)
        return titel + PythonKlein.Klein(text)[PythonKlein.Klein(kopf).Length..];
    }
}
