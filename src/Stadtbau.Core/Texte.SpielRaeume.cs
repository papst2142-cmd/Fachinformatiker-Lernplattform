using System.Globalization;

namespace Stadtbau.Core;

/// <summary>Welle S 0.60.0 (Weisung P12-337): Texte der Ansicht „Räume des Spiels“.</summary>
public static partial class Texte
{
    public const string SpielRaeumeKnopf = "Räume des Spiels ansehen";
    public const string SpielRaeumeKnopfTipp = "Wohnungen, Büro, Serverfarm, Zweigstelle und Kunden innen ansehen, genau so wie im Spiel.";
    public const string SpielRaeumeTitel = "Räume des Spiels";
    public const string SpielRaeumeErklaerung = "Diese Räume baut das Spiel aus seinen eigenen Daten. Mit „Ändern“ (rechts) "
        + "bearbeitest du sie; der Export bringt deine Änderung ins Spiel.";
    public const string SpielRaum3DKnopf = "In 3D ansehen";
    public const string SpielRaum3DTipp = "Den Raum in 3D ansehen, wie das Spiel ihn zeigt. Ziehen dreht, das Mausrad oder zwei Finger zoomen.";
    public const string SpielRaeumeZurueck = "Zurück zu den Innenräumen";
    public const string SpielRaeumeZurueckTipp = "Zurück zu den Innenräumen deiner Stadt, die du bearbeiten kannst.";
    public const string SpielRaumAendernTitel = "Raum ändern";
    public const string SpielRaumAendernSchalter = "Ändern";
    public const string SpielRaumAendernTipp = "Den Raum bearbeiten: Möbel verschieben, Wände ziehen, Türen setzen, Raumgröße ändern.";
    public const string SpielRaumAendernErklaerung = "Das Spiel nimmt deine Änderung aus der Export-Datei. Ohne Änderung bleibt der Raum genau wie heute.";
    public const string SpielRaumAendernAus = "Schalte „Ändern“ ein, um den Raum zu bearbeiten. Bis dahin siehst du ihn genau wie im Spiel.";
    public const string SpielRaumAendernNurStadt1 = "Räume des Spiels lassen sich nur in einem Projekt auf Stadt 1 ändern (nicht in einer neuen oder einer Paket-Stadt).";
    public const string SpielRaumWandZiehen = "Wand ziehen";
    public const string SpielRaumWandZiehenTipp = "Wände eines Raums oder des Flurs verschieben: Lage und Größe eintragen.";
    public const string SpielRaumTuerSetzen = "Tür setzen";
    public const string SpielRaumTuerSetzenTipp = "Die Tür eines Raums zum Flur verschieben oder breiter machen (mindestens 0,90 m).";
    public const string SpielRaumGroesseKnopf = "Raumgröße";
    public const string SpielRaumGroesseTipp = "Breite und Tiefe des ganzen Hauses ändern.";
    public const string SpielRaumFlurOben = "Flur: obere Wand";
    public const string SpielRaumFlurBreite = "Flur: Breite";
    public const string SpielRaumHausBreite = "Haus: Breite";
    public const string SpielRaumHausTiefe = "Haus: Tiefe";
    public const string SpielRaumUebernehmen = "Übernehmen";
    public const string SpielRaumZurueck = "Zurück auf den Stand des Spiels";
    public const string SpielRaumZurueckTipp = "Alle Änderungen an diesem Raum zurücknehmen (ein Schritt, „Rückgängig“ holt sie wieder).";
    public const string SpielRaumGeaendert = "geändert";
    public const string SpielRaumUnveraendert = "Unverändert: der Raum ist genau wie im Spiel.";
    public const string SpielRaumFundlisteKeine = "Fundliste: nichts rot";
    public const string SpielRaumFundlistePrueft = "Fundliste: wird geprüft …";
    public const string SpielRaumFundlisteErklaerung = "Das Spiel bräuchte diese Stellen frei. Tippe einen Fund an: der Grundriss zeigt die Stelle mit rotem Ring.";
    public const string SpielRaumExportFrei = "Kein roter Fund: der Export ist für diesen Raum frei.";
    public const string SpielRaumNurZahlen = "Bitte nur Zahlen eintragen (z. B. 2,5).";
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

    public static string SpielRaumGeoeffnet(string name, bool aendern) => name + " geöffnet (Ändern " + (aendern ? "an)" : "aus)");

    public static string SpielRaumFundliste(int rot) => string.Format(CultureInfo.InvariantCulture, "Fundliste: {0} rot", rot);

    public static string SpielRaumExportGesperrt(int rot) =>
        string.Format(CultureInfo.InvariantCulture, "Export gesperrt ({0} {1})", rot, rot == 1 ? "roter Fund" : "rote Funde");

    public static string SpielRaumGeaendertMeldung(string was) => was + " (Raum des Spiels geändert)";
}
