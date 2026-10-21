namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 3, W4a (Weisung P3-17): Texte der neuen Rechner "Datenmengen" und "Übertragung" und
/// der Trainer-Arten "einheiten"/"dauer", PC und Handy gleich. Wortlaut genau wie die von Chat
/// freigegebene Wortliste (Fi-Austausch anlagen/P3-16_Wortliste_W4a.md, Aenderungen P3-17
/// Punkt 3). Kein Python-Vorbild; RechnerTexte.Python.cs (erzeugt) bleibt unveraendert.
/// </summary>
public static partial class RechnerTexte
{
    // -- Auswahl der Rechner (Pillen) --------------------------------------------------------

    /// <summary>Auswahl im Reiter "Rechner": (Wert, Pillentext) in fester Reihenfolge (P3-16 Punkt 3).</summary>
    public static readonly IReadOnlyList<(string Wert, string Text)> RechnerAuswahl =
    [
        ("subnetting", "Subnetting"), ("datenmengen", "Datenmengen"), ("uebertragung", "Übertragung"),
        ("bild", "Bild und Video"), ("raid", "RAID"), ("usv", "USV"),
    ];

    // -- Trainer-Bereich ---------------------------------------------------------------------

    public const string TrainerBereich = "BEREICH";
    public const string BereichNetzwerk = "Netzwerk";
    public const string BereichDaten = "Daten";

    // -- Datenmengen umrechnen ---------------------------------------------------------------

    public const string DataTitle = "Datenmengen umrechnen";
    public const string DataSubtitle = "Bit, Byte, dezimal und binär";
    public const string DataField = "Datenmenge";
    public const string DataUnit = "Einheit";
    public const string DataDefault = "4,7";
    public const string DataDefaultUnit = "GB";
    public const string DataTooSmall = "Die Datenmenge muss größer als 0 sein.";
    public const string DataTooBig = "Die Datenmenge ist zu groß (höchstens 1.000.000.000.000).";
    public const string DataInput = "Eingabe";
    public const string DataDecimal = "Dezimal (Faktor 1000)";
    public const string DataBinary = "Binär (Faktor 1024)";
    public const string KleinerAls2 = "< 0,01";
    public const string KleinerAls3 = "< 0,001";
    public const string WenigerAls2 = "weniger als 0,01";
    public const string WenigerAls3 = "weniger als 0,001";

    public const string DataExplainTitle = "RECHENWEG DATENMENGEN";
    public const string DataExplainStep1 = "SCHRITT 1: In Byte umrechnen";
    public static readonly IReadOnlyList<string> DataExplainUnits =
    [
        "   Dezimal: 1 kB = 1000 Byte, 1 MB = 1000 kB, 1 GB = 1000 MB,",
        "   1 TB = 1000 GB. Binär: 1 KiB = 1024 Byte, 1 MiB = 1024 KiB,",
        "   1 GiB = 1024 MiB, 1 TiB = 1024 GiB.",
    ];
    public const string DataExplainStep2 = "SCHRITT 2: In Bit umrechnen";
    public const string DataExplainBit = "   1 Byte = 8 Bit";
    public const string DataExplainStep3 = "SCHRITT 3: In die anderen Einheiten umrechnen";
    public const string DataExplainDivide = "   Dezimal wird durch 1000 je Stufe geteilt, binär durch 1024.";
    public static readonly IReadOnlyList<string> DataExplainMerke =
    [
        "   Merke: 1 KiB (1024 Byte) ist größer als 1 kB (1000 Byte),",
        "   deshalb ist die Zahl in KiB kleiner als in kB.",
    ];

    // -- Übertragungsdauer -------------------------------------------------------------------

    public const string TransferTitle = "Übertragungsdauer";
    public const string TransferSubtitle = "Dateigröße, Datenrate, nutzbare Leistung";
    public const string TransferSize = "Dateigröße";
    public const string TransferRate = "Datenrate";
    public const string TransferPercent = "Nutzbare Leistung (%)";
    public const string TransferDefaultSize = "4,7";
    public const string TransferDefaultRate = "16";
    public const string TransferDefaultPercent = "100";
    public const string TransferSizeTooSmall = "Die Dateigröße muss größer als 0 sein.";
    public const string TransferSizeTooBig = "Die Dateigröße ist zu groß (höchstens 1.000.000.000.000).";
    public const string TransferRateTooSmall = "Die Datenrate muss größer als 0 sein.";
    public const string TransferRateTooBig = "Die Datenrate ist zu groß (höchstens 1.000.000).";
    public const string TransferPercentWrong = "Die nutzbare Leistung muss größer als 0 und höchstens 100 % sein.";
    public const string TransferLineSize = "Dateigröße";
    public const string TransferLineBits = "Datenmenge";
    public const string TransferLineRate = "Datenrate";
    public const string TransferLinePercent = "Nutzbare Leistung";
    public const string TransferLineEffective = "Wirksame Datenrate";
    public const string TransferLineDuration = "Dauer";
    public const string TransferLineSplit = "Aufgeteilt";
    public const string Sekunden = "Sekunden";

    public const string TransferExplainTitle = "RECHENWEG ÜBERTRAGUNGSDAUER";
    public const string TransferExplainStep1 = "SCHRITT 1: Dateigröße in Bit umrechnen";
    public static readonly IReadOnlyList<string> TransferExplainWhy =
    [
        "   Datenraten zählen Bit pro Sekunde, deshalb wird die",
        "   Dateigröße zuerst in Bit umgerechnet (1 Byte = 8 Bit).",
    ];
    public const string TransferExplainStep2 = "SCHRITT 2: Wirksame Datenrate ermitteln";
    public const string TransferExplainDecimal = "   Datenraten sind immer dezimal: 1 %s = %s bit/s.";
    public const string TransferExplainFormula = "   Wirksame Datenrate = Datenrate x nutzbare Leistung / 100";
    public const string TransferExplainStep3 = "SCHRITT 3: Dauer berechnen";
    public const string TransferExplainDuration = "   Dauer = Bit / wirksame Datenrate";
    public const string TransferExplainStep4 = "SCHRITT 4: In Stunden, Minuten und Sekunden aufteilen";
    public const string TransferExplainUnits = "   1 Std. = 3600 Sek., 1 Min. = 60 Sek.";

    // -- Trainer-Arten -----------------------------------------------------------------------

    public const string ArtEinheiten = "Datenmengen";
    public const string ArtDauer = "Übertragungsdauer";
    public const string FeldErgebnis = "Ergebnis (%s)";
    public const string FeldStunden = "Stunden";
    public const string FeldMinuten = "Minuten";
    public const string FeldSekunden = "Sekunden";
    public const string AufgabeUmrechnen = "Rechne %s %s in %s um.";
    public const string AufgabeRunden2 = " Runde auf 2 Nachkommastellen.";
    public const string AufgabeBitAusByte = "Wie viele Bit sind %s Byte?";
    public const string AufgabeByteAusBit = "Wie viele Byte sind %s Bit?";
    public const string AufgabeDauerLeicht = "Eine Datei mit %s MB wird mit %s Mbit/s übertragen. Die nutzbare Leistung ist 100 %%. Wie viele Sekunden dauert das?";
    public const string AufgabeDauerMittel = "Eine Datei mit %s GB wird mit %s Mbit/s übertragen. Die nutzbare Leistung ist 100 %%. Wie lange dauert das?";
    public const string AufgabeDauerSchwer = "Eine Datei mit %s %s wird mit %s Mbit/s übertragen. Die nutzbare Leistung ist %s %%. Wie lange dauert das? Runde die Sekunden auf 2 Nachkommastellen.";
}
