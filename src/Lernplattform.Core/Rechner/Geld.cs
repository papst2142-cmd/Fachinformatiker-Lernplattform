using System.Globalization;
using System.Numerics;

namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3 W4c, W1 (Weisungen P3-16 Punkt 4 und P3-22, R3: Chat liest die ganze Datei): Geld fuer
/// die WiSo-Rechner. Kein Python-Vorbild. Geld rechnet immer mit decimal, nie mit double; jeder
/// Rechenschritt wird kaufmaennisch auf Cent gerundet (<see cref="Runden"/>, halbe Cent weg von 0,
/// nie Math.Round mit der Standardregel "zur geraden Zahl"). Das ist eine bewusste Ausnahme von
/// der Dauerregel "Rundung nur PythonRunden/PythonSumme" und gilt nur fuer Geld (Entscheidung
/// F5a, P3-16). Gelesen wird mit der vorhandenen Leseregel (<see cref="Zahlen.Bereinigt"/>: Komma,
/// Tausenderpunkt, hoechstens 3 Nachkommastellen), der bereinigte Text geht direkt in
/// decimal.Parse, nie ueber double (Auflage 4a). Die Schreibweise ist dieselbe wie
/// <see cref="Zahlen.DeZahl"/> (Tausenderpunkt, Komma), aber ein eigener Weg fuer decimal.
/// </summary>
public static class Geld
{
    /// <summary>Hoechster Geldbetrag (Plan P3-15 Abschnitt 4.4).</summary>
    public const decimal HoechstBetrag = 1_000_000_000m;

    /// <summary>Kaufmaennisch auf 2 Nachkommastellen (Cent), halbe Cent weg von 0.</summary>
    public static decimal Runden(decimal wert) => Math.Round(wert, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Liest einen Wert nach der Leseregel als decimal. Leer oder keine Zahl: dieselben Fehler
    /// wie <see cref="Zahlen.Lesen"/> ("Bitte einen Wert eingeben: …", "Bitte eine Zahl eingeben: …").
    /// </summary>
    public static decimal Lesen(string? text, string beschriftung)
    {
        if (Wert(text, out var wert))
        {
            return wert;
        }
        Zahlen.Lesen(text, beschriftung);   // wirft genau den Fehler der Leseregel
        throw new InvalidOperationException("Leseregel uneinig fuer " + beschriftung);
    }

    /// <summary>
    /// Wert nach der Leseregel ohne Fehlertext (fuer die Pruefer des Trainers). Die Leseregel
    /// laesst hoechstens 20 Zeichen zu, also hoechstens 20 Ziffern: das passt immer in decimal.
    /// </summary>
    public static bool Wert(string? text, out decimal wert)
    {
        wert = 0m;
        if (!Zahlen.Bereinigt(text, out var negativ, out var betrag, out _))
        {
            return false;
        }
        var zahl = decimal.Parse(betrag, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        wert = zahl == 0m ? 0m : (negativ ? -zahl : zahl);
        return true;
    }

    /// <summary>True, wenn der Wert mehr als 2 Nachkommastellen hat (nach dem Wert: "19,990" hat 2).</summary>
    public static bool MehrAlsCent(decimal wert) => wert != Math.Round(wert, 2, MidpointRounding.AwayFromZero);

    /// <summary>True, wenn der Wert ganz ist (nach dem Wert: "12,0" ist ganz).</summary>
    public static bool IstGanz(decimal wert) => wert == decimal.Truncate(wert);

    /// <summary>
    /// Deutsche Schreibweise: kaufmaennisch auf <paramref name="stellen"/> Nachkommastellen,
    /// Tausenderpunkt, Komma ("1.024,60", "-4.000,00", "0,00"). Ohne Waehrungszeichen.
    /// </summary>
    public static string Text(decimal wert, int stellen = 2)
    {
        var r = Math.Round(wert, stellen, MidpointRounding.AwayFromZero);
        var betrag = Math.Abs(r);
        var ganz = decimal.Truncate(betrag);
        var text = Zahlen.DeGanz(new BigInteger(ganz));
        if (stellen > 0)
        {
            var bruch = (betrag - ganz).ToString("F" + stellen.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            text += "," + bruch[2..];
        }
        return (r < 0 ? "-" : "") + text;
    }

    /// <summary>Wie <see cref="Text"/> mit 3 Stellen, ohne Nullen am Ende ("2,5", "99,999", "15").</summary>
    public static string Kurz(decimal wert)
    {
        var text = Text(wert, 3);
        return text.TrimEnd('0').TrimEnd(',');
    }

    /// <summary>
    /// Kleinste ganze Zahl &gt;= zaehler / nenner, exakt ueber den Rest (zaehler &gt;= 0, nenner &gt; 0).
    /// Fuer die Gewinnschwelle: aufrunden, nie abrunden.
    /// </summary>
    public static long Aufrunden(decimal zaehler, decimal nenner)
    {
        if (zaehler < 0 || nenner <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nenner), "nur zaehler >= 0 und nenner > 0");
        }
        var rest = zaehler % nenner;
        var ganz = (long)((zaehler - rest) / nenner);
        return rest == 0m ? ganz : ganz + 1;
    }
}
