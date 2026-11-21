using System.Globalization;

namespace Stadtbau.Core;

/// <summary>Welle S 0.60.0 (Weisung P12-337): Texte der Ansicht „Räume des Spiels“.</summary>
public static partial class Texte
{
    public const string SpielRaeumeKnopf = "Räume des Spiels ansehen";
    public const string SpielRaeumeKnopfTipp = "Wohnungen, Büro, Serverfarm, Zweigstelle und Kunden innen ansehen, genau so wie im Spiel.";
    public const string SpielRaeumeTitel = "Räume des Spiels";
    public const string SpielRaeumeErklaerung = "Diese Räume baut das Spiel aus seinen eigenen Daten, nicht aus deiner Stadt-Datei. "
        + "Du siehst sie hier genau so wie im Spiel: Räume, Gänge, Türen und Möbelplätze. Ändern kannst du sie hier nicht.";
    public const string SpielRaumWahl = "Raum";
    public const string SpielRaumWahlTipp = "Raum des Spiels wählen";
    public const string SpielRaeumeZurueck = "Zurück zu den Innenräumen";
    public const string SpielRaeumeZurueckTipp = "Zurück zu den Innenräumen deiner Stadt, die du bearbeiten kannst.";
    public const string SpielRaumNurAnsehen = "Nur ansehen";
    public const string SpielRaumMasse = "Maße wie im Spiel";
    public const string SpielRaumServerfarm = "Serverfarm";
    public const string SpielRaumFehlt = "Diesen Raum gibt es in den Spieldaten nicht.";
    public const string SpielRaumGrundriss = "Grundriss des Spiel-Raums";

    public static string SpielRaumFirma(int stufe, string name) =>
        string.Format(CultureInfo.InvariantCulture, "Eigene Firma, Stufe {0}{1}", stufe, name.Length > 0 ? ": " + name : "");

    public static string SpielRaumZweigstelle(int stufe, string name) =>
        string.Format(CultureInfo.InvariantCulture, "Zweigstelle, Stufe {0}{1}", stufe, name.Length > 0 ? ": " + name : "");

    public static string SpielRaumGroesse(string breite, string tiefe) => "Größe: " + breite + " × " + tiefe;

    public static string SpielRaumFlur(string breite) => "Flur oder Diele: " + breite + " breit";

    public static string SpielRaumTuer(string breite) => "Schmalste Innentür: " + breite;

    public static string SpielRaumZahlen(int raeume, int moebel) =>
        string.Format(CultureInfo.InvariantCulture, "{0} Räume, {1} Möbel und Dekostücke", raeume, moebel);

    public static string SpielRaumGeoeffnet(string name) => name + " geöffnet (nur ansehen)";
}
