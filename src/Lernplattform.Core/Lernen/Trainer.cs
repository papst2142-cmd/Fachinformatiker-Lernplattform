using System.Globalization;

namespace Lernplattform.Core.Lernen;

/// <summary>Rechnungen des Pruefungstrainers (Ergebnis einer Session) wie Python v0.63.2.</summary>
public static class Trainer
{
    /// <summary>finish_quiz: (richtig / gesamt) * 100, ohne Fragen 0.</summary>
    public static double Prozent(int richtig, int gesamt) => gesamt != 0 ? (double)richtig / gesamt * 100 : 0.0;

    /// <summary>fisi_core.ihk_note: IHK-Notenschluessel.</summary>
    public static string Note(double prozent) => prozent switch
    {
        >= 92 => "1 (Sehr gut)",
        >= 81 => "2 (Gut)",
        >= 67 => "3 (Befriedigend)",
        >= 50 => "4 (Ausreichend)",
        >= 30 => "5 (Mangelhaft)",
        _ => "6 (Ungenügend)",
    };

    /// <summary>
    /// fisi_core.de_number fuer endliche Werte im Bereich von decimal: Decimal(repr(x)),
    /// kaufmaennisch gerundet (ROUND_HALF_UP), Tausenderpunkt und Komma.
    /// </summary>
    public static string DeZahl(double wert, int stellen = 2)
    {
        // repr(x) ist die kuerzeste eindeutige Ziffernfolge, wie "R" in .NET
        var genau = decimal.Parse(wert.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
        var gerundet = Math.Round(genau, stellen, MidpointRounding.AwayFromZero);
        var text = gerundet.ToString("#,0." + new string('0', stellen), CultureInfo.InvariantCulture);
        if (stellen == 0)
        {
            text = gerundet.ToString("#,0", CultureInfo.InvariantCulture);
        }
        if (gerundet == 0 && (wert < 0 || double.IsNegative(wert)))
        {
            text = "-" + text;      // Python behaelt das Vorzeichen von -0,0
        }
        return text.Replace(",", "#", StringComparison.Ordinal).Replace(".", ",", StringComparison.Ordinal)
            .Replace("#", ".", StringComparison.Ordinal);
    }
}
