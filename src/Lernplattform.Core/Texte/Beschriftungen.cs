using System.Globalization;
using System.Text;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Gemeinsame Hilfen fuer Beschriftungen (Vergleichswerte G9): Grossschreibung
/// wie Python str.upper, Zahlwoerter (fisi_core.count_word), Tausenderpunkte
/// (fisi_core.de_int) und das Einsetzen in Python-Muster mit %s/%d.
/// </summary>
public static class Beschriftungen
{
    /// <summary>Text fuer Teile, die erst spaeter gebaut werden (Vorgabe Nico, nicht aus Python).</summary>
    public const string Platzhalter = "Kommt in einer späteren Ausbaustufe";

    /// <summary>
    /// Grossbuchstaben wie Python str.upper (Zwischenueberschriften am PC .upper(),
    /// am Handy ui.label): aus "ß" wird "SS" ("Schriftgröße" -> "SCHRIFTGRÖSSE").
    /// ToUpperInvariant allein laesst "ß" stehen.
    /// </summary>
    public static string Gross(string text) => text.ToUpperInvariant().Replace("ß", "SS", StringComparison.Ordinal);

    private static readonly string[] Zahlwoerter =
        ["null", "ein", "zwei", "drei", "vier", "fünf", "sechs", "sieben", "acht", "neun", "zehn", "elf", "zwölf"];

    /// <summary>Zahl als Wort fuer Fliesstexte, ab 13 (und unter 0) als Ziffern (count_word).</summary>
    public static string Zahlwort(int zahl) =>
        zahl >= 0 && zahl < Zahlwoerter.Length ? Zahlwoerter[zahl] : zahl.ToString(CultureInfo.InvariantCulture);

    /// <summary>Ganze Zahl mit Tausenderpunkten (de_int): 1234567 -> "1.234.567".</summary>
    public static string MitPunkten(long zahl) =>
        zahl.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');

    /// <summary>
    /// Setzt Werte in ein Python-Muster ein ("%s", "%d", "%%"), der Reihe nach
    /// wie der Python-Operator %. Zahlen ohne Tausenderpunkte (wie %d).
    /// </summary>
    public static string Einsetzen(string muster, params object[] werte)
    {
        var text = new StringBuilder();
        var naechster = 0;
        for (var i = 0; i < muster.Length; i++)
        {
            if (muster[i] != '%' || i + 1 >= muster.Length)
            {
                text.Append(muster[i]);
                continue;
            }
            var art = muster[++i];
            if (art == '%')
            {
                text.Append('%');
                continue;
            }
            if (art != 's' && art != 'd')
            {
                throw new FormatException("Unbekannter Platzhalter %" + art + " in: " + muster);
            }
            if (naechster >= werte.Length)
            {
                throw new FormatException("Zu wenige Werte fuer: " + muster);
            }
            text.Append(Convert.ToString(werte[naechster++], CultureInfo.InvariantCulture));
        }
        if (naechster != werte.Length)
        {
            throw new FormatException("Zu viele Werte fuer: " + muster);
        }
        return text.ToString();
    }
}

/// <summary>Woerter und Beschriftung der Klappbereiche (fisi_theme FOLD_*, PC und Handy gleich).</summary>
public static class KlappTexte
{
    public const string Aufklappen = "aufklappen";
    public const string Zuklappen = "zuklappen";
    public const string ZustandZu = "zugeklappt";
    public const string ZustandAuf = "aufgeklappt";

    /// <summary>"Farben, zugeklappt" bzw. "Farben, aufgeklappt" (Tastatur, Bildschirmleser; fold_label).</summary>
    public static string Name(string titel, bool offen) => titel + ", " + (offen ? ZustandAuf : ZustandZu);
}
