namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 3, W2 (Paket R6, Weisung P3-4 A3): Oberflaechentexte der Rechner-Seite als EINE Liste
/// fuer PC und Handy. In Python stehen sie doppelt in app_gui.py (TrainerPanel bis CalcView,
/// Z. 5490-6567) und mobile/src/main.py (TrainerPanel bis CalcScreen, Z. 2950-3581); hier stehen
/// die 25 Texte, die auf beiden gleich sind (Vergleichswerte G26 "oberflaeche", Test
/// RechnerTests.OberflaecheWiePython). Die Texte des Rechen-Trainers braucht erst Welle 3.
/// Nicht uebernommen: der Fenstertitel "Ungültige Eingabe" (nur PC; Fehleingabe auf beiden
/// Geraeten als roter Hinweis, Weisung P3-4 Frage 2) und die Kopfzeile "WERKZEUGE > RECHNER"
/// (kommt wie bei allen Seiten aus Navigation).
/// </summary>
public static partial class RechnerTexte
{
    // -- Rechner (app_gui 5891-6527, main.py 3079-3438) --------------------------------------

    public const string TabRechner = "Rechner";
    public const string TabTrainer = "Trainer";

    /// <summary>CALC_TABS: Umschalter "Rechner / Trainer".</summary>
    public static readonly IReadOnlyList<(string Wert, string Text)> CALC_TABS = [("rechner", TabRechner), ("trainer", TabTrainer)];

    public const string Berechnen = "Berechnen";
    public const string RechenwegAnzeigen = "Rechenweg anzeigen";
    public const string RechenwegAusblenden = "Rechenweg ausblenden";
    public const string NochKeineBerechnung = "Noch keine Berechnung durchgeführt.";

    /// <summary>Zeile der IPv6-Netzdaten "%-22s: %s" (rechnet Subnetting.Netzdaten).</summary>
    public const string DetailZeile = "\n%-22s: %s";

    // -- Rechen-Trainer (app_gui 5504-5755, main.py 2963-3072), erst Welle 3 ------------------

    public const string TrainerUnter = "Zufallsaufgaben mit Selbstkontrolle";
    public const string TrainerArt = "AUFGABENART";
    public const string TrainerStufe = "SCHWIERIGKEIT";
    public const string RundeStarten = "Runde starten";
    public const string Aufgabe = "Aufgabe";
    public const string Aufgaben = "Aufgaben";
    public const string TrainerStart = "Wähle Aufgabenart und Schwierigkeit und starte eine Runde mit %d Aufgaben.";
    public const string Pruefen = "Prüfen";
    public const string Bisher = "bisher %s, %d richtig";
    public const string ArtUndStufe = "%s · %s";
    public const string AufgabeVon = "Aufgabe %d / %d";
    public const string RichtigWaere = "richtig wäre: %s";
    public const string AllesRichtig = "Alles richtig.";
    public const string NochNichtGanz = "Noch nicht ganz - hier der Rechenweg:";
    public const string Auswertung = "Auswertung";
    public const string NaechsteAufgabe = "Nächste Aufgabe";
    public const string RundeBeendet = "Runde beendet: %s.";
    public const string NeueRunde = "Starte eine neue Runde oder wähle eine andere Aufgabenart.";

    /// <summary>Alle 25 Oberflaechentexte, die PC und Handy gleich zeigen (Pruefung gegen G26).</summary>
    public static readonly IReadOnlyList<string> Oberflaeche =
    [
        TabRechner, TabTrainer, Berechnen, RechenwegAnzeigen, RechenwegAusblenden, NochKeineBerechnung, DetailZeile,
        TrainerUnter, TrainerArt, TrainerStufe, RundeStarten, Aufgabe, Aufgaben, TrainerStart, Pruefen, Bisher,
        ArtUndStufe, AufgabeVon, RichtigWaere, AllesRichtig, NochNichtGanz, Auswertung, NaechsteAufgabe,
        RundeBeendet, NeueRunde,
    ];

    // -- USV-Bild (fisi_widgets UpsDiagram 2054-2078, mobile/src/ui.py UpsPicture 1391-1425) ---

    public const string BildLast = "LAST";
    public const string BildUsv = "USV";
    public const string BildAkku = "AKKU";

    /// <summary>"Empfehlung: USV %s" (urteil "passend" gruen, sonst rot).</summary>
    public const string BildEmpfehlung = "Empfehlung: USV %s";
}
