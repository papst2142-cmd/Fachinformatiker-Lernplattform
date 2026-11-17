using System.Globalization;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.56.0 (Sammelliste 362, Weisung P12-287): Texte zu „Karte vergrößern“ und „Stadtteil einsetzen“. Einfache Worte.
/// </summary>
public static partial class Texte
{
    private static readonly CultureInfo DePlaetze = CultureInfo.GetCultureInfo("de-DE");

    public const string KarteVergroessern = "Karte vergrößern";
    public const string KarteVergroessernTipp = "Macht die Karte größer (in Stücken von 64 Feldern, bis 256 × 256). Du wählst, auf welchem Platz die heutige Stadt liegen soll; alles rückt genau dorthin.";
    public const string StadtteilEinsetzen = "Stadtteil einsetzen";
    public const string StadtteilEinsetzenTipp = "Setzt ein anderes Projekt (höchstens 64 × 64) ganz auf einen freien Platz der Karte, in einem Schritt.";
    public const string PlatzWaehlen = "Platz wählen";
    public const string PlatzErklaerung = "Die Karte ist in Plätze von 64 × 64 Feldern geteilt, Platz 1 oben links, dann zeilenweise nach rechts.";
    public const string VergroessernGroesse = "Breite und Tiefe gehen nur in Stücken von 64: 64, 128, 192 oder 256.";
    public const string VergroessernKleiner = "Die Karte kann nur größer werden, nie kleiner.";
    public const string VergroessernGleich = "Die Karte hat schon diese Größe. Wähle eine größere Breite oder Tiefe.";
    public const string EinsetzenSichSelbst = "Ein Projekt kann sich nicht selbst einsetzen. Wähle ein anderes Projekt.";
    public const string EinsetzenHausFehlt = "Ein Haus des Projekts lässt sich nicht mitnehmen. Es wurde nichts eingesetzt.";
    public const string EinsetzenPaketplatz = "Beide Städte haben eine Wohnung. Es gilt der Paketplatz dieser Karte; der des eingesetzten Stadtteils kommt nicht mit.";
    public const string EinsetzenKleingruen = "Der eingesetzte Stadtteil hat beim Kleingrün eine andere Einstellung. Es gilt die Einstellung dieser Karte.";
    public const string EinsetzenLinie = "eine Straße, Bahn, ein Fluss oder Weg";
    public const string EinsetzenVorschauFrei = "Platz frei: hier lässt sich der Stadtteil einsetzen.";
    public const string RandOffen = "Rand offen";

    public const string Plaetze = "Plätze";
    public const string PlaetzeTipp = "Plätze zeigen (P): die Karte vergrößern oder einen anderen Stadtteil auf einen freien Platz setzen.";
    public const string PlaetzeNurDraufsicht = "„Plätze“ geht nur in „Stadt von oben“.";
    public const string PlaetzeErklaerung = "Tippe auf einen Platz, um ihn zu wählen. Mit der Tastatur: Pfeiltasten wählen den Platz, rechts die Auswahl und die Knöpfe.";
    public const string PlatzDaneben = "Dort ist kein Platz. Tippe auf ein Feld mit einer Nummer.";
    public const string PlatzKeiner = "Kein Platz gewählt";
    public const string NeueBreite = "Neue Breite";
    public const string NeueTiefe = "Neue Tiefe";
    public const string HeutigeStadtAuf = "Heutige Stadt kommt auf";
    public const string KarteVergroessernKnopf = "Vergrößern";
    public const string EinsetzenProjekt = "Welches Projekt?";
    public const string EinsetzenKeinProjekt = "Kein Projekt gewählt";
    public const string Einsetzen = "Einsetzen";
    public const string EinsetzenWahlFehlt = "Wähle zuerst ein Projekt und einen Platz.";
    public const string PlaetzeFertig = "Plätze fertig";
    public const string PlaetzeFertigTipp = "Beendet „Plätze“ (Esc).";

    public static string PlatzNummer(int platz) => string.Create(DePlaetze, $"Platz {platz}");

    public static string PlatzGewaehlt(int platz) => string.Create(DePlaetze, $"Platz {platz} gewählt.");

    public static string Felder(int felder) => string.Create(DePlaetze, $"{felder} Felder");

    public static string KarteGroesseJetzt(double breite, double hoehe) => string.Create(DePlaetze, $"Heute {breite:0.##} × {hoehe:0.##} Felder.");

    public static string EinsetzenNichtLesbar(string name) => $"„{name}“ lässt sich nicht lesen.";

    public static string EinsetzenHinweise(int anzahl) =>
        anzahl == 1 ? "1 Hinweis steht rechts." : string.Create(DePlaetze, $"{anzahl} Hinweise stehen rechts.");

    public static string PlatzFehlt(int platz, int anzahl) =>
        string.Create(DePlaetze, $"Platz {platz} gibt es auf dieser Karte nicht. Sie hat die Plätze 1 bis {anzahl}.");

    public static string VergroessernPasstNicht(int platz) =>
        string.Create(DePlaetze, $"Auf Platz {platz} ragt die heutige Karte über den Rand der neuen Karte. Wähle einen Platz weiter oben oder links.");

    public static string TeilUnbekannt(string teil) =>
        $"Das Werkzeug kennt den Teil „{teil}“ der Stadt nicht und kann ihn nicht sicher verschieben. Es wurde nichts geändert.";

    public static string KarteVergroessernSchritt(int breite, int hoehe, int platz) =>
        string.Create(DePlaetze, $"Karte vergrößert auf {breite} × {hoehe} (heutige Stadt auf Platz {platz})");

    public static string EinsetzenSchritt(string name, int platz) =>
        string.Create(DePlaetze, $"Stadtteil „{name}“ auf Platz {platz} eingesetzt");

    public static string EinsetzenZuGross(string name, double breite, double hoehe) =>
        string.Create(DePlaetze, $"„{name}“ ist {breite:0.##} × {hoehe:0.##} groß. Einsetzen geht nur mit Projekten bis 64 × 64.");

    public static string PlatzBelegt(int platz, string art) =>
        string.Create(DePlaetze, $"Auf Platz {platz} liegt schon etwas ({art}). Wähle einen freien Platz.");

    public static string EinsetzenHuegel(int anzahl) =>
        string.Create(DePlaetze, $"Zusammen wären es {anzahl} Hügel; erlaubt sind höchstens 16. Es wurde nichts eingesetzt.");

    public static string EinsetzenInnenraumStadt1(string ort) =>
        $"„{ort}“ wird hier Spielort; sein Innenraum ist der der Stadt 1, nicht der geänderte aus dem eingesetzten Projekt.";

    public static string EinsetzenKennungNeu(string alt, string neu) =>
        $"Die Kennung „{alt}“ gab es schon; sie heißt jetzt „{neu}“.";

    public static string EinsetzenQuestNicht(string titel) =>
        $"Die Quest „{titel}“ kommt nicht mit: ihr Spielort ist auf dieser Karte schon da (das Haus kommt als Gebäude ohne Ort).";

    public static string EinsetzenQuestDoppelt(string id) =>
        $"Die Quest „{id}“ ist in beiden Projekten verschieden geändert. Es wurde nichts eingesetzt.";

    public static string RandHinweis(string art, double x, double y) =>
        string.Create(DePlaetze, $"{art} endet bei x {x:0.##}, y {y:0.##} am Rand eines Platzes, drüben geht nichts weiter. Verbinde selbst oder ändere das Ende.");

    /// <summary>Name einer Art fuer Meldungen (gleiche Worte wie die Knoepfe).</summary>
    public static string ArtName(AussenArt art) => art switch
    {
        AussenArt.Strasse => Strasse,
        AussenArt.Kurve => Kurve90,
        AussenArt.Kreisverkehr => KreisverkehrKlein,
        AussenArt.Haus => Haus,
        AussenArt.Gebaeude => HausOhneOrt,
        AussenArt.Baum => Baum,
        AussenArt.Park => Park,
        AussenArt.Beet => Beet,
        AussenArt.Laterne => Laterne,
        AussenArt.Katalog => VollerKatalog,
        AussenArt.Blueprint => BlueprintSetzen,
        AussenArt.Fluss => Fluss,
        AussenArt.Bahn => Bahn,
        AussenArt.Teich => Teich,
        AussenArt.Steg => Steg,
        AussenArt.Zebrastreifen => Zebrastreifen,
        AussenArt.Notiz => Notiz,
        AussenArt.Ampel => Ampel,
        AussenArt.Haltestelle => Haltestelle,
        AussenArt.Parkplatz => Parkplatz,
        AussenArt.Huegel => Huegel,
        AussenArt.Hoehe => GemalteHoehen,
        AussenArt.Fussweg => Fussweg,
        AussenArt.Ziehen => StrasseZiehen,
        AussenArt.Paketplatz => Paketplatz,
        _ => art.ToString(),
    };
}
