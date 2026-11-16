using System.Globalization;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.55.0 (Weisung P12-282 Punkt 2): Texte der Stadtteil-Namen. Einfache Worte, ein Satz je Tipp.
/// </summary>
public static partial class Texte
{
    private static readonly CultureInfo DeNamen = CultureInfo.GetCultureInfo("de-DE");

    public const string StadtteilNamen = "Stadtteil-Namen";
    public const string StadtteilNamenTipp = "Bereiche der Stadt benennen (B): auf der Karte ein Rechteck ziehen und einen Namen geben. Das Spiel zeigt die Namen auf der Karte im Handy und als kurzes Schild beim Betreten. Nur Anzeige, keine Spielregel.";
    public const string StadtteilNamenErklaerung = "Ziehe mit dem Finger oder der Maus ein Rechteck auf der Karte. Tippe in einen Stadtteil, um ihn zu wählen. Mit der Tastatur: „Neuer Stadtteil“ und die Knöpfe unten.";
    public const string StadtteilNamenKeine = "Diese Stadt hat noch keine Stadtteil-Namen.";
    public const string StadtteilNamenNurDraufsicht = "Stadtteil-Namen gehen nur in „Stadt von oben“.";
    public const string StadtteilNeu = "Neuer Stadtteil";
    public const string StadtteilNeuTipp = "Legt einen Stadtteil von 16 × 16 Feldern in der Mitte der Ansicht an.";
    public const string StadtteilNeuSchritt = "Stadtteil angelegt";
    public const string StadtteilUmbenannt = "Stadtteil umbenannt";
    public const string StadtteilVerschoben = "Stadtteil verschoben";
    public const string StadtteilGroesse = "Größe des Stadtteils geändert";
    public const string StadtteilGeloescht = "Stadtteil gelöscht";
    public const string StadtteilFehlt = "Diesen Stadtteil gibt es nicht mehr.";
    public const string StadtteilKaputt = "(falsch gespeichert)";
    public const string StadtteilNameFeld = "Name des Stadtteils";
    public const string StadtteilNameUebernehmen = "Name übernehmen";
    public const string StadtteilNameTipp = "Höchstens 24 Zeichen, jeder Name nur einmal.";
    public const string StadtteilLoeschen = "Stadtteil löschen";
    public const string StadtteilFertig = "Fertig";
    public const string StadtteilFertigTipp = "Stadtteil-Namen beenden (Esc).";
    public const string StadtteilLinks = "Nach links";
    public const string StadtteilRechts = "Nach rechts";
    public const string StadtteilOben = "Nach oben";
    public const string StadtteilUnten = "Nach unten";
    public const string StadtteilBreiter = "Breiter";
    public const string StadtteilSchmaler = "Schmaler";
    public const string StadtteilHoeher = "Tiefer (nach unten größer)";
    public const string StadtteilFlacher = "Weniger tief";
    public const string StadtteilSchieben = "Verschieben (je 1 Feld, Pfeiltasten)";
    public const string StadtteilGroesseText = "Größe (je 1 Feld)";

    /// <summary>Vorschlag fuer einen neuen Namen.</summary>
    public static string StadtteilVorschlag(int nummer) => string.Create(DeNamen, $"Stadtteil {nummer}");

    /// <summary>Masse beim Ziehen (in der Vorschau).</summary>
    public static string StadtteilMasse(double breite, double tiefe) => string.Create(DeNamen, $"{breite:0.#} × {tiefe:0.#}");

    /// <summary>Eine Zeile der Liste (auch Vorlesen): Name, Lage und Groesse.</summary>
    public static string StadtteilZeile(string name, double x1, double z1, double x2, double z2) =>
        string.Create(DeNamen, $"{name}: von {x1:0.#}/{z1:0.#} bis {x2:0.#}/{z2:0.#} ({x2 - x1:0.#} × {z2 - z1:0.#})");

    /// <summary>Melden nach dem Anlegen.</summary>
    public static string StadtteilAngelegt(string name) => "Stadtteil „" + name + "“ angelegt. Gib ihm rechts einen Namen.";

    /// <summary>Melden beim Waehlen.</summary>
    public static string StadtteilGewaehlt(string zeile) => "Gewählt: " + zeile;

    /// <summary>Anzahl in der Ueberschrift der Liste.</summary>
    public static string StadtteilAnzahl(int anzahl) => anzahl == 1 ? "1 Stadtteil" : string.Create(DeNamen, $"{anzahl} Stadtteile");
}
