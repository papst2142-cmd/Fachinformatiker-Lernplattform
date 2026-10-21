namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 3, W4a (Weisung P3-17): Texte der neuen Rechner "Datenmengen" und "Übertragung" und
/// der Trainer-Arten "einheiten"/"dauer", PC und Handy gleich. Wortlaut genau wie die von Chat
/// freigegebene Wortliste (Fi-Austausch anlagen/P3-16_Wortliste_W4a.md, Aenderungen P3-17
/// Punkt 3). Kein Python-Vorbild; RechnerTexte.Python.cs (erzeugt) bleibt unveraendert.
/// W4b (Weisung P3-21): Karte "Bild und Video", Uebernahme und Trainer-Art "video" genau nach der
/// freigegebenen Wortliste (anlagen/P3-20_Wortliste_W4b.md, Feld "Dauer (Minuten, optional)").
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
    // P3-19: zwei Zeilen statt "dezimal: 1 ..." - fit_lines liest "Text: Wert" als Beschriftung und
    // bricht eine zu lange Beschriftung am Handy nicht um (zur Lesung in Meldung P3-19)
    public const string TransferExplainDecimal = "   Datenraten sind immer dezimal.";
    public const string TransferExplainDecimalValue = "   1 %s = %s bit/s.";
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

    // -- Bild und Video (W4b) ----------------------------------------------------------------

    public const string VideoTitle = "Bild und Video";
    public const string VideoSubtitle = "Pixel, Farbtiefe, Datenrate, Video";
    public const string VideoFieldMinutes = "Dauer (Minuten, optional)";
    public const string VideoReadMinutes = "Dauer (Minuten)";
    public const string VideoFieldCompression = "Kompression (y-fach)";
    public const string VideoDefaultCompression = "1";
    public const string VideoMinutesTooSmall = "Die Dauer muss größer als 0 sein.";
    public const string VideoMinutesTooBig = "Die Dauer ist zu groß (höchstens 10.000 Minuten).";
    public const string VideoNeedsFps = "Für ein Video bitte eine Bildrate größer als 0 eingeben.";
    public const string VideoCompressionWrong = "Die Kompression muss mindestens 1 und höchstens 1.000 sein.";
    public const string VideoHeader = "VIDEO (NUR BILD, OHNE TON)";
    public const string VideoLineDuration = "Dauer";
    public const string VideoLinePerSecond = "Byte pro Sekunde";
    public const string VideoLineRaw = "Unkomprimiert";
    public const string VideoLineCompression = "Kompression";
    public const string VideoLineCompressed = "Komprimiert";
    public const string VideoLineRate = "Datenrate komprimiert";
    public const string Minute = "Minute";
    public const string Minuten = "Minuten";
    public const string Fach = "-fach";
    public const string VideoTakeOver = "In Übertragungsdauer übernehmen";
    public const string VideoTakenMiB = "Aus Bild und Video übernommen (auf 3 Nachkommastellen gerundet).";
    public const string VideoTakenByte = "Aus Bild und Video übernommen (in Byte, aufgerundet).";

    public const string VideoExplainStep5 = "SCHRITT 5: Video (mit den eingegebenen Zahlen)";
    public static readonly IReadOnlyList<string> VideoExplainWhat =
    [
        "   Ein Video ist eine Folge von Bildern. Hier zählt nur das",
        "   Bild, ohne Ton.",
    ];
    public const string VideoExplainPerSecond = "   Byte pro Sekunde = Byte je Bild x Bildrate";
    public const string VideoExplainAmount = "   Datenmenge = Byte pro Sekunde x Minuten x 60";
    public const string VideoExplainStep6 = "SCHRITT 6: Kompression";
    public const string VideoExplainCompression = "   Bei y-facher Kompression wird die Datenmenge durch y geteilt.";

    public const string ArtVideo = "Bild und Video";
    public const string AufgabeVideoLeicht = "Ein Bild hat %s x %s Pixel und %s Bit Farbtiefe. Wie viele Byte braucht es unkomprimiert?";
    public const string AufgabeVideoMittel = "Ein Video hat %s x %s Pixel, %s Bit Farbtiefe und %s Bilder pro Sekunde. Wie groß ist die Datenrate unkomprimiert? Runde auf 2 Nachkommastellen.";
    public const string AufgabeVideoSchwer = "Ein Video hat %s x %s Pixel, %s Bit Farbtiefe und %s Bilder pro Sekunde. Es dauert %s und wird %s-fach komprimiert. Wie groß ist die Datei? Runde auf 2 Nachkommastellen.";
}
