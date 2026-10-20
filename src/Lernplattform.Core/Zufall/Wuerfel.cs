using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Lernplattform.Core.Zufall;

/// <summary>
/// Fester "Wuerfel" des Spiels zwischen 0 und 1 aus Text, auf allen Geraeten
/// gleich. Nachbau von <c>_dice(seed, day, salt)</c> aus fisi_game.py:
/// SHA-256 ueber "seed|tag|salz", die ersten 12 Hexzeichen geteilt durch 16^12.
/// </summary>
public static class Wuerfel
{
    private const double Teiler = 281474976710656.0; // 16^12

    public static double Werfen(string startwert, long tag, string salz)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"{startwert}|{tag}|{salz}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        // 12 Hexzeichen = 6 Byte
        ulong wert = 0;
        for (int i = 0; i < 6; i++)
        {
            wert = (wert << 8) | hash[i];
        }
        return wert / Teiler;
    }
}
