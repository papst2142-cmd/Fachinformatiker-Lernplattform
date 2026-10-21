using System.Globalization;
using Lernplattform.Core.Texte;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3 W4a, D3/D5 (Weisung P3-17): Ergebnistext und Rechenweg der Karten "Datenmengen
/// umrechnen" und "Übertragungsdauer" nach der freigegebenen Wortliste (Fi-Austausch
/// anlagen/P3-16_Wortliste_W4a.md, Aenderungen P3-17 Punkt 3). Gelesen wird mit der vorhandenen
/// Leseregel (<see cref="Zahlen.Lesen"/>, Fehlertexte mit der Feldbeschriftung), danach die
/// Bereichspruefung. Der Rechenweg nutzt die eingegebenen Zahlen (F1a). Winzige Werte stehen als
/// "&lt; 0,01" bzw. "&lt; 0,001", im Rechenweg als "weniger als ..." (F2a, P3-17 3b/3c). Den
/// Umbruch uebernimmt der Ergebniskasten (fit_lines, PC 75 / Handy 28 Zeichen). In den
/// Rechenweg-Zeilen steht im Zeilenkern nur Einfachleerzeichen: fit_lines haelt eine Zeile mit
/// Doppelleerzeichen fuer eine Tabellenspalte und bricht sie nicht um (Weisung P3-19).
/// </summary>
public static class DatenBerichte
{
    /// <summary>Zeilenbreite des PC-Kastens; zwei Spalten nur, wenn sie hineinpassen.</summary>
    public const int PcBreite = 75;

    /// <summary>Beginn der rechten Spalte (Wortliste: "Dezimal (Faktor 1000)" + Abstand).</summary>
    private const int Spalte2 = 31;

    private static string I(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>Beschriftung bis Spalte 22, dann ": " (wie Bildschirm); schmal nur die eigene Laenge.</summary>
    private static string Zeile(string beschriftung, string wert, bool schmal) =>
        (schmal ? beschriftung + " " : beschriftung.PadRight(22)) + ": " + wert;

    /// <summary>Basis und Exponent einer Einheit ("1000^3", "1024^2").</summary>
    public static string Potenz(string einheit)
    {
        var dez = Datenmenge.Dezimal.ToList().IndexOf(einheit);
        if (dez >= 0)
        {
            return I($"1000^{dez + 1}");
        }
        var bin = Datenmenge.Binaer.ToList().IndexOf(einheit);
        if (bin >= 0)
        {
            return I($"1024^{bin + 1}");
        }
        throw new ArgumentException("keine Potenz fuer " + einheit, nameof(einheit));
    }

    // -- Datenmengen ---------------------------------------------------------------------------

    /// <summary>Liest die Eingabe; RechnerFehler mit dem Wortlaut der Wortliste.</summary>
    public static DatenmengeEingabe DatenmengeLesen(string? text, string einheit)
    {
        var wert = Zahlen.Lesen(text, DataField);
        if (wert <= 0)
        {
            throw new RechnerFehler(DataTooSmall);
        }
        var ergebnis = Datenmenge.Rechnen(wert, einheit) ?? throw new RechnerFehler(DataTooBig);
        return new DatenmengeEingabe(wert, einheit, ergebnis, Zahlen.TausenderHinweis(text));
    }

    /// <summary>Ergebnis: Eingabe, Byte, Bit, dann dezimal und binaer (zwei Spalten nur am PC, wenn sie passen).</summary>
    public static string DatenmengeBericht(DatenmengeEingabe d, bool schmal)
    {
        var e = d.Ergebnis;
        var zeilen = new List<string>
        {
            Zeile(DataInput, Datenmenge.GanzOderKurz(d.Wert) + " " + d.Einheit, schmal),
            Zeile("Byte", e.Anzeige("Byte") + " Byte", schmal),
            Zeile("Bit", e.Anzeige("Bit") + " Bit", schmal),
            "",
        };
        var links = new List<string> { DataDecimal };
        links.AddRange(Datenmenge.Dezimal.Select(z => z.PadRight(4) + ": " + e.Anzeige(z)));
        var rechts = new List<string> { DataBinary };
        rechts.AddRange(Datenmenge.Binaer.Select(z => z.PadRight(4) + ": " + e.Anzeige(z)));
        var spalte = Math.Max(Spalte2, links.Max(z => z.Length) + 2);
        if (!schmal && spalte + rechts.Max(z => z.Length) <= PcBreite)
        {
            zeilen.AddRange(links.Select((z, i) => z.PadRight(spalte) + rechts[i]));
        }
        else
        {
            zeilen.AddRange(links);
            zeilen.Add("");
            zeilen.AddRange(rechts);
        }
        zeilen.AddRange(d.Hinweis);
        return string.Join("\n", zeilen);
    }

    /// <summary>Rechenweg mit den eingegebenen Zahlen (Wortliste Abschnitt 2, F1a).</summary>
    public static string DatenmengeRechenweg(DatenmengeEingabe d)
    {
        var e = d.Ergebnis;
        var byteText = Weniger(e.Anzeige("Byte"));
        var zeilen = new List<string> { DataExplainTitle, DataExplainStep1 };
        zeilen.AddRange(DataExplainUnits);
        zeilen.Add(d.Einheit switch
        {
            "Bit" => "   Byte = " + Datenmenge.GanzOderKurz(d.Wert) + " / 8 = " + byteText + " Byte",
            "Byte" => "   Byte = " + DataInput,
            _ => "   Byte = " + Datenmenge.GanzOderKurz(d.Wert) + " x " + Potenz(d.Einheit) + " = " + byteText + " Byte",
        });
        zeilen.Add(DataExplainStep2);
        zeilen.Add(DataExplainBit);
        zeilen.Add(d.Einheit == "Bit"
            ? "   Bit = " + DataInput
            : "   Bit = " + byteText + " x 8 = " + Weniger(e.Anzeige("Bit")) + " Bit");
        zeilen.Add(DataExplainStep3);
        zeilen.Add(DataExplainDivide);
        foreach (var ziel in Datenmenge.Dezimal.Concat(Datenmenge.Binaer))
        {
            zeilen.Add("   " + ziel + " = " + byteText + " / " + Potenz(ziel) + " = " + Weniger(e.Anzeige(ziel)) + " " + ziel);
        }
        zeilen.AddRange(DataExplainMerke);
        return string.Join("\n", zeilen);
    }

    /// <summary>Im Rechenweg steht nach "=" nie "&lt;": "weniger als 0,01" (P3-17 3b), analog "weniger als 0,001".</summary>
    private static string Weniger(string text) => text switch
    {
        KleinerAls2 => WenigerAls2,
        KleinerAls3 => WenigerAls3,
        _ => text,
    };

    // -- Uebertragungsdauer --------------------------------------------------------------------

    /// <summary>Liest die drei Felder; RechnerFehler mit dem Wortlaut der Wortliste (erst Lesen, dann Bereich).</summary>
    public static UebertragungEingabe UebertragungLesen(string? groesseText, string einheit, string? rateText, string rateEinheit, string? prozentText)
    {
        var groesse = Zahlen.Lesen(groesseText, TransferSize);
        var rate = Zahlen.Lesen(rateText, TransferRate);
        var prozent = Zahlen.Lesen(prozentText, TransferPercent);
        var e = Uebertragung.Rechnen(groesse, einheit, rate, rateEinheit, prozent);
        switch (e.Fehler)
        {
            case UebertragungFehler.Groesse:
                throw new RechnerFehler(groesse <= 0 ? TransferSizeTooSmall : TransferSizeTooBig);
            case UebertragungFehler.Rate:
                throw new RechnerFehler(rate <= 0 ? TransferRateTooSmall : TransferRateTooBig);
            case UebertragungFehler.Prozent:
                throw new RechnerFehler(TransferPercentWrong);
        }
        return new UebertragungEingabe(groesse, einheit, rate, rateEinheit, prozent, e,
            Zahlen.TausenderHinweis(groesseText, rateText, prozentText));
    }

    /// <summary>Bit, bit/s: ganze Zahl oder bis 3 Nachkommastellen; winzig "&lt; 0,001".</summary>
    private static string Kurz(double wert) => Datenmenge.Klein3(wert) ? KleinerAls3 : Datenmenge.GanzOderKurz(wert);

    /// <summary>Sekunden mit 2 Nachkommastellen; winzig "&lt; 0,01" (P3-17 3c).</summary>
    private static string SekText(double t) => Datenmenge.Klein(t) ? KleinerAls2 : Zahlen.DeZahl(t);

    /// <summary>"0 Std. 39 Min. 10,00 Sek." (Stunden mit Tausenderpunkt, exakt als Ganzzahl).</summary>
    private static string AufteilungText(UebertragungErgebnis e, bool weniger)
    {
        var (std, min, sek) = e.Aufgeteilt;
        var sekunden = Datenmenge.Klein(e.Sekunden) ? (weniger ? WenigerAls2 : KleinerAls2) : sek;
        return Zahlen.DeGanz(std) + " Std. " + min.ToString(CultureInfo.InvariantCulture) + " Min. " + sekunden + " Sek.";
    }

    private static string ByteText(UebertragungEingabe u) =>
        Kurz(Datenmenge.NachByte(u.Groesse, u.Einheit));

    /// <summary>Ergebnis der Uebertragungsdauer (Wortliste Abschnitt 3, zweite Dauerzeile "Aufgeteilt").</summary>
    public static string UebertragungBericht(UebertragungEingabe u, bool schmal)
    {
        var e = u.Ergebnis;
        var groesse = Datenmenge.GanzOderKurz(u.Groesse) + " " + u.Einheit;
        if (u.Einheit != "Byte")
        {
            groesse += " = " + ByteText(u) + " Byte";
        }
        var zeilen = new List<string>
        {
            Zeile(TransferLineSize, groesse, schmal),
            Zeile(TransferLineBits, Kurz(e.Bit) + " Bit", schmal),
            Zeile(TransferLineRate, Datenmenge.GanzOderKurz(u.Datenrate) + " " + u.RateEinheit, schmal),
            Zeile(TransferLinePercent, Datenmenge.GanzOderKurz(u.Prozent) + " %", schmal),
            Zeile(TransferLineEffective, Kurz(e.Rate) + " bit/s", schmal),
            Zeile(TransferLineDuration, SekText(e.Sekunden) + " " + Sekunden, schmal),
            Zeile(TransferLineSplit, AufteilungText(e, false), schmal),
        };
        zeilen.AddRange(u.Hinweis);
        return string.Join("\n", zeilen);
    }

    /// <summary>Rechenweg der Uebertragungsdauer mit den eingegebenen Zahlen (Wortliste Abschnitt 3).</summary>
    public static string UebertragungRechenweg(UebertragungEingabe u)
    {
        var e = u.Ergebnis;
        var bitText = Weniger(Kurz(e.Bit));
        var rateText = Weniger(Kurz(e.Rate));
        var sekText = Weniger(SekText(e.Sekunden));
        var zeilen = new List<string> { TransferExplainTitle, TransferExplainStep1 };
        zeilen.AddRange(TransferExplainWhy);
        if (u.Einheit == "Byte")
        {
            zeilen.Add("   Bit = " + Datenmenge.GanzOderKurz(u.Groesse) + " x 8 = " + bitText + " Bit");
        }
        else
        {
            var byteText = Weniger(ByteText(u));
            zeilen.Add("   Byte = " + Datenmenge.GanzOderKurz(u.Groesse) + " x " + Potenz(u.Einheit) + " = " + byteText + " Byte");
            zeilen.Add("   Bit = " + byteText + " x 8 = " + bitText + " Bit");
        }
        var faktor = Zahlen.DeZahl(Uebertragung.RatenFaktor(u.RateEinheit), 0);
        zeilen.Add(TransferExplainStep2);
        zeilen.Add(TransferExplainDecimal);
        zeilen.Add(Beschriftungen.Einsetzen(TransferExplainDecimalValue, u.RateEinheit, faktor));
        zeilen.Add(TransferExplainFormula);
        zeilen.Add("   " + Datenmenge.GanzOderKurz(u.Datenrate) + " x " + faktor + " x " + Datenmenge.GanzOderKurz(u.Prozent)
            + " / 100 = " + rateText + " bit/s");
        zeilen.Add(TransferExplainStep3);
        zeilen.Add(TransferExplainDuration);
        zeilen.Add("   " + bitText + " / " + rateText + " = " + sekText + " " + Sekunden);
        zeilen.Add(TransferExplainStep4);
        zeilen.Add(TransferExplainUnits);
        zeilen.Add("   " + sekText + " Sek. = " + AufteilungText(e, true));
        return string.Join("\n", zeilen);
    }
}

/// <summary>Gelesene Eingabe der Datenmengen-Karte mit Ergebnis und Tausender-Hinweis.</summary>
public sealed record DatenmengeEingabe(double Wert, string Einheit, DatenmengeErgebnis Ergebnis, IReadOnlyList<string> Hinweis);

/// <summary>Gelesene Eingabe der Uebertragungs-Karte mit Ergebnis und Tausender-Hinweis.</summary>
public sealed record UebertragungEingabe(double Groesse, string Einheit, double Datenrate, string RateEinheit, double Prozent,
    UebertragungErgebnis Ergebnis, IReadOnlyList<string> Hinweis);
