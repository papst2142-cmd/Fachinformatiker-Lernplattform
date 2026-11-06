namespace Stadtbau.Core;

/// <summary>
/// Phase 13 Welle 2: alle sichtbaren Texte des Stadtbau-Werkzeugs an einer Stelle (Deutsch, einfache Worte fuer Nico).
/// Die Oberflaeche nimmt Texte nur von hier, die Tests pruefen Wortlaut und Namen fuer den Screenreader.
/// </summary>
public static class Texte
{
    // ---- Kopfleiste ----
    public const string Neu = "Neu";
    public const string Oeffnen = "Öffnen";
    public const string Speichern = "Speichern";
    public const string Rueckgaengig = "Rückgängig";
    public const string Wiederholen = "Wiederholen";
    public const string DateiSpeichern = "Datei speichern";
    public const string AnCcSenden = "An CC senden";
    public const string Hell = "Hell";
    public const string Dunkel = "Dunkel";
    public const string Draufsicht = "Draufsicht";
    public const string Innenraeume = "Innenräume";

    // ---- Start ----
    public const string Willkommen = "Willkommen im Stadtbau-Werkzeug";
    public const string WillkommenText =
        "Hier baust du Innenräume der Stadt um. Lege ein neues Projekt an oder öffne ein vorhandenes. Jede Änderung wird sofort gespeichert, die letzten 20 Stände liegen zusätzlich als Sicherung bereit.";
    public const string LetzteProjekte = "Zuletzt bearbeitet";
    public const string KeineProjekte = "Noch kein Projekt vorhanden.";
    public const string NurHandy = "Das Werkzeug ist für Tablet und PC gedacht";
    public const string NurHandyText = "Auf einem Handy ist der Bildschirm zu klein zum Bauen. Bitte öffne das Werkzeug auf dem Tablet oder am PC.";

    // ---- Neu ----
    public const string NeuTitel = "Neues Projekt";
    public const string NeuName = "Name des Projekts";
    public const string NeuNameHinweis = "Buchstaben, Ziffern, Leerzeichen, - und _ (höchstens 40 Zeichen).";
    public const string NeuArt = "Was möchtest du bauen?";
    public const string NeuStadt = "Stadt 1 überarbeiten";
    public const string NeuInnenraum = "Innenraum überarbeiten";
    public const string NeuLeer = "Leere Stadt (kommt mit der nächsten Version)";
    public const string NeuOrt = "Welcher Innenraum?";
    public const string Anlegen = "Anlegen";
    public const string Abbrechen = "Abbrechen";
    public const string NameUngueltig = "Der Name passt nicht. Erlaubt sind Buchstaben, Ziffern, Leerzeichen, - und _ (1 bis 40 Zeichen).";

    // ---- Oeffnen ----
    public const string OeffnenTitel = "Projekt öffnen";
    public const string OeffnenDatei = "Datei von woanders holen";
    public const string NurLesen = "Diese Datei kommt von einer neueren Version des Werkzeugs. Du kannst sie ansehen, aber nicht ändern. Bitte aktualisiere das Werkzeug.";

    // ---- Innenraeume ----
    public const string Ort = "Innenraum";
    public const string Katalog = "Katalog";
    public const string KatalogHinweis = "Tippe ein Stück an und dann auf die Stelle im Grundriss.";
    public const string SetzenAktiv = "Tippe auf die Stelle im Grundriss, an die das Stück soll.";
    public const string Auswahl = "Ausgewähltes Stück";
    public const string NichtsGewaehlt = "Tippe ein Stück im Grundriss an, um es zu bearbeiten.";
    public const string Drehen = "Drehen";
    public const string Kopie = "Kopie";
    public const string Loeschen = "Löschen";
    public const string Ware = "Ware";
    public const string KeineWare = "keine Ware";
    public const string Breite = "Breite";
    public const string Tiefe = "Tiefe";
    public const string X = "Links (x)";
    public const string Y = "Oben (y)";
    public const string Uebernehmen = "Übernehmen";
    public const string RasterAus = "Raster aus (feiner schieben)";
    public const string Raum = "Raum";
    public const string RaumAendern = "Raum ändern";
    public const string Tuer = "Tür von / bis";
    public const string Boden = "Boden";
    public const string Grenzen = "Grenzen";
    public const string Pruefungen = "Prüfungen";
    public const string AllesGruen = "Alles in Ordnung.";
    public const string Vergroessern = "Vergrößern";
    public const string Verkleinern = "Verkleinern";
    public const string Einpassen = "Ganz zeigen";
    public const string Grundriss = "Grundriss";
    public const string DraufsichtHinweis = "Tippe auf ein Haus mit Innenraum, um ihn zu bearbeiten. Die Stadt außen kommt mit der nächsten Version.";

    // ---- Export und Senden ----
    public const string ExportGesperrt = "Datei kann noch nicht gespeichert werden";
    public const string ExportOk = "Datei gespeichert";
    public const string SendenFrage = "Die Datei geht an CC, der sie ins Spiel einbaut und prüft. Möchtest du eine Nachricht dazuschreiben? (freiwillig)";
    public const string SendenTitel = "An CC senden";
    public const string Senden = "Senden";
    public const string Gesendet = "Gesendet";
    public const string SendenFehler = "Senden hat nicht geklappt";
    public const string SendenNichtVerfuegbar = "Senden ist in dieser Fassung nicht verfügbar. Nimm „Datei speichern“ und gib die Datei an CC.";
    public const string Ok = "OK";
    public const string Gespeichert = "Gespeichert";

    public static string Grenzzeile(long dreiecke, int aufrufe, long grenzeDreiecke, int grenzeAufrufe) =>
        string.Create(System.Globalization.CultureInfo.GetCultureInfo("de-DE"),
            $"{dreiecke:N0} von {grenzeDreiecke:N0} Dreiecken, {aufrufe} von {grenzeAufrufe} Zeichenaufrufen");
}
