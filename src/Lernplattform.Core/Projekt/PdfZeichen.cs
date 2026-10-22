using System.Text;

namespace Lernplattform.Core.Projekt;

/// <summary>
/// Phase 4, W3: Zeichen fuer den PDF-Export wie fisi_pdf._clean und _encode v0.63.2. NFC ueber den
/// ganzen Text macht .NET, das Ergebnis je Zeichen kommt aus der Tabelle (PdfZeichen.Tabelle.cs,
/// erzeugt mit Python 3.12 aus fisi_pdf, weil unicodedata an der Unicode-Fassung haengt). Danach
/// WinAnsi (cp1252 wie Python, ohne die fuenf dort nicht belegten Codes; errors="replace" ergibt "?").
/// </summary>
public static partial class PdfZeichen
{
    // Eigene Klasse: die Tabellen (andere Datei der partiellen Klasse) sind dann sicher schon da
    private static class Mengen
    {
        public static readonly Dictionary<int, string> Einzeln = PdfZeichen.Einzeln.ToDictionary(e => e.Zeichen, e => e.Text);
    }

    /// <summary>Unicode 0x80-0x9F von cp1252 (Python: 0x81, 0x8D, 0x8F, 0x90, 0x9D nicht belegt).</summary>
    private static readonly Dictionary<char, byte> WinAnsi = new()
    {
        ['€'] = 0x80, ['‚'] = 0x82, ['ƒ'] = 0x83, ['„'] = 0x84, ['…'] = 0x85, ['†'] = 0x86,
        ['‡'] = 0x87, ['ˆ'] = 0x88, ['‰'] = 0x89, ['Š'] = 0x8A, ['‹'] = 0x8B, ['Œ'] = 0x8C,
        ['Ž'] = 0x8E, ['‘'] = 0x91, ['’'] = 0x92, ['“'] = 0x93, ['”'] = 0x94, ['•'] = 0x95,
        ['–'] = 0x96, ['—'] = 0x97, ['˜'] = 0x98, ['™'] = 0x99, ['š'] = 0x9A, ['›'] = 0x9B,
        ['œ'] = 0x9C, ['ž'] = 0x9E, ['Ÿ'] = 0x9F,
    };

    private static bool InBereichen(int[] bereiche, int zeichen)
    {
        int unten = 0, oben = bereiche.Length / 2 - 1;
        while (unten <= oben)
        {
            var mitte = (unten + oben) / 2;
            if (zeichen < bereiche[2 * mitte])
            {
                oben = mitte - 1;
            }
            else if (zeichen > bereiche[2 * mitte + 1])
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

    /// <summary>_clean(chr(c)) fuer ein einzelnes Zeichen.</summary>
    public static string Zeichen(int zeichen)
    {
        if (Mengen.Einzeln.TryGetValue(zeichen, out var text))
        {
            return text;
        }
        if (InBereichen(Leer, zeichen))
        {
            return "";
        }
        return InBereichen(Frage, zeichen) ? "?" : char.ConvertFromUtf32(zeichen);
    }

    /// <summary>_clean: NFC, dann je Zeichen Ersatz, Grundform oder Wegfall.</summary>
    public static string Bereinigen(string text)
    {
        var nfc = text.IsNormalized(NormalizationForm.FormC) ? text : text.Normalize(NormalizationForm.FormC);
        var ergebnis = new StringBuilder(nfc.Length);
        foreach (var rune in nfc.EnumerateRunes())
        {
            if (rune.Value < 0x80)
            {
                ergebnis.Append((char)rune.Value);
            }
            else
            {
                ergebnis.Append(Zeichen(rune.Value));
            }
        }
        return ergebnis.ToString();
    }

    /// <summary>_encode: bereinigt und als cp1252 (nicht darstellbar wird "?").</summary>
    public static byte[] Kodieren(string text)
    {
        var sauber = Bereinigen(text);
        var bytes = new byte[sauber.Length];
        var n = 0;
        foreach (var rune in sauber.EnumerateRunes())
        {
            var c = rune.Value;
            bytes[n++] = c < 0x80 || (c >= 0xA0 && c <= 0xFF) ? (byte)c
                : c <= 0xFFFF && WinAnsi.TryGetValue((char)c, out var b) ? b : (byte)'?';
        }
        return n == bytes.Length ? bytes : bytes[..n];
    }
}
