using System.Globalization;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.51.0 (Weisung P12-241): Texte der Strassenzug-Vorlagen und der Gelaende-Mehrfachauswahl. Gleiche Regeln wie der
/// Rest von <see cref="Texte"/>: einfache Worte, ein Satz je Tipp.
/// </summary>
public static partial class Texte
{
    private static readonly CultureInfo DeZug = CultureInfo.GetCultureInfo("de-DE");

    // ---- Vorlage ----
    public const string ZugGehweg = "Gehweg";
    public const string ZugOhneGehweg = "ohne Gehweg";
    public static string ZugBaeumeAlle(double abstand) => string.Create(DeZug, $"Bäume alle {abstand:0.0#}");

    // ---- Datei ----
    public const string ZugKaputt = "Die Datei ist beschädigt und lässt sich nicht lesen.";
    public const string ZugKeiner = "Das ist keine Straßenzug-Vorlage des Stadtbau-Werkzeugs.";
    public const string ZugNeuer = "Die Datei stammt aus einer neueren Version des Werkzeugs. Bitte das Werkzeug aktualisieren.";
    public const string ZugPruefsumme = "Die Datei wurde verändert oder ist beschädigt (Prüfsumme passt nicht).";
    public const string ZugNichtLesbar = "Die Datei lässt sich gerade nicht öffnen.";
    public const string ZugGibtEs = "Eine Vorlage mit diesem Namen gibt es schon.";

    // ---- Gruende (rot, wird ausgelassen) ----
    public const string ZugZuKurz = "Das gerade Stück ist dafür zu kurz.";
    public const string ZugHaltZuKurz = "Kein gerades Stück ist lang genug für eine Haltestelle (mindestens 6).";
    public const string ZugOhneGehwegKeinZebra = "Ohne Gehweg gibt es keinen Zebrastreifen.";
    public const string ZugOhneGehwegKeineHalt = "Ohne Gehweg gibt es keine Haltestelle.";
    public const string ZugZebraNichtGerade = "Liegt nicht quer auf einem geraden Stück.";
    public const string ZugZuNahKreuzung = "Zu nah an einer Kreuzung oder Einmündung.";
    public const string ZugZuNahZebra = "Zu nah an einem Zebrastreifen oder einer Ampel.";
    public const string ZugZuNahAmpel = "Zu nah an einer Ampel.";
    public const string ZugZuNahGleis = "Zu nah am Gleis.";
    public const string ZugZuNahKreisverkehr = "Zu nah am Kreisverkehr.";
    public const string ZugZuNahHalt = "Zu nah an einer anderen Haltestelle.";
    public const string ZugHaltGenug = "Die Stadt hat schon so viele Haltestellen, wie erlaubt sind.";
    public const string ZugHaltNichtGerade = "Neben diesem Stück ist kein Platz für eine Haltestelle.";
    public const string ZugAufGehweg = "Liegt auf einer Straße oder ihrem Gehweg.";
    public const string ZugAnHaltestelle = "Steht am Häuschen oder Schild der Haltestelle.";
    public static string ZugNachPruefung(string fund) => "Die Prüfung meldet sonst: " + fund;
    public const string ZugNachPruefungZuViel = "Die Prüfung meldet an zu vielen Stellen neue Fehler.";

    // ---- Bedienung ----
    public const string ZugAusstattung = "Ausstattung der Straße";
    public const string ZugAusstattungTipp = "Was mit der Straße in einem Schritt dazukommt. Rote Stellen werden ausgelassen und gezählt.";
    public const string ZugOhne = "Nur die Straße";
    public const string ZugOhneBeschreibung = "wie bisher, ohne Ausstattung";
    public const string ZugLaternenHinweis = "Laternen stellt das Werkzeug an jede Straße selbst.";
    public const string ZugFest = "fest";
    public const string ZugEigene = "Eigene Vorlage zusammenstellen";
    public const string ZugEigeneTipp = "Gehweg, Bäume, Zebrastreifen und Haltestelle selbst wählen und unter einem Namen speichern.";
    public const string ZugTitel = "Eigene Straßenzug-Vorlage";
    public const string ZugName = "Name der Vorlage";
    public const string ZugMitGehweg = "Mit Gehweg";
    public const string ZugMitBaeumen = "Bäume auf beiden Seiten";
    public const string ZugBaumAbstand = "Abstand der Bäume";
    public const string ZugBaumEnger = "Bäume enger";
    public const string ZugBaumWeiter = "Bäume weiter auseinander";
    public static string ZugAbstandText(double abstand) => string.Create(DeZug, $"{abstand:0.0}");
    public const string ZugMitZebra = "Zebrastreifen an jeder Einmündung";
    public const string ZugMitHalt = "Haltestelle in der Mitte, wenn die Straße lang genug ist";
    public const string ZugNurMitGehweg = "Zebrastreifen und Haltestelle gibt es nur an einer Straße mit Gehweg.";
    public const string ZugSpeichern = "Vorlage speichern";
    public static string ZugGespeichert(string name) => $"Vorlage „{name}“ gespeichert";
    public const string ZugNameFest = "So heißt schon eine feste Vorlage.";
    public const string ZugLoeschen = "Vorlage löschen";
    public const string ZugLoeschenTitel = "Vorlage löschen?";
    public static string ZugLoeschenFrage(string name) => $"Die Vorlage „{name}“ wird gelöscht. Eine Kopie bleibt in den Sicherungen des Werkzeugs.";
    public static string ZugGeloescht(string name) => $"Vorlage „{name}“ gelöscht";
    public static string ZugDateiFehler(string name, string fehler) => $"„{name}“ lässt sich nicht verwenden: {fehler}";
    public static string ZugGewaehlt(string name) => $"Ausstattung: {name}";

    public static string ZugInfo(int frei, int rot, bool geprueft) =>
        (frei == 1 ? "1 Stück Ausstattung" : frei + " Stücke Ausstattung")
        + (rot > 0 ? " · " + rot + " rot, werden ausgelassen" : "")
        + (geprueft ? " · geprüft" : "");

    public static string ZugGesetzt(int stuecke, int neu, int ausgelassen) =>
        ZiehenGesetzt(Strasse, stuecke) + ", " + (neu == 1 ? "1 Stück Ausstattung" : neu + " Stücke Ausstattung")
        + (ausgelassen > 0 ? ", " + ausgelassen + " rote Stellen ausgelassen" : "");

    // ---- Gelaende in der Mehrfachauswahl ----
    public const string GemalteHoehen = "Gemalte Höhen";
    public static string GemaltePunkte(int n) => n == 1 ? "1 gemalter Höhenpunkt" : n + " gemalte Höhenpunkte";
    public static string HuegelGenug(int hoechstens) => $"Die Stadt hat schon {hoechstens} Hügel, mehr sind nicht erlaubt. Erst einen Hügel löschen.";
    public const string HuegelGenugKurz = "Höchstzahl erreicht";
}
