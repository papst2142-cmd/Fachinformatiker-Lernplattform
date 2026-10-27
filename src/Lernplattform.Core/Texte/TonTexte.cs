namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 8 W8: Texte der Karten "Musik und Geräusche", "Vollbild" und "Tastenbelegung". Die Saetze aus
/// Meldung P8-15 hat Chat angenommen (P8-16 E2 Punkt 5); die Texte der eigenen Lieder und der Wahl
/// "Eigene Lieder spielen" sind Vorschlag in Meldung P8-17 (Chat nimmt an).
/// </summary>
public static class TonTexte
{
    public const string Gesamt = "Gesamt";
    public const string Musik = "Musik";
    public const string Umgebung = "Umgebung";
    public const string Prozent = "{0} %";
    public const string ReglerSatz = "Gesamt regelt alles zusammen, Musik die Musik, Umgebung die Geräusche (Schritte, Autos, Türen, Kasse). Bei 0 % ist es still. Töne spielen nur im Spiel.";

    // Eigene Lieder (P8-17, P8-17b; Vorschlag)
    public const string Lieder = "Eigene Lieder";
    public const string LiederSatz = "Deine eigenen Lieder (MP3, WAV, FLAC oder OGG) laufen im Spiel als Musik. Ohne eigene Lieder gibt es keine Musik.";
    public const string KeineLieder = "Noch keine eigenen Lieder.";
    public const string LiedHinzufuegen = "Lied hinzufügen";
    public const string Entfernen = "Entfernen";
    public const string EntfernenTitel = "Lied entfernen";
    public const string EntfernenFrage = "„{0}“ entfernen? Gelöscht wird nur die Kopie im Programm, deine eigene Datei bleibt.";
    public const string NichtHinzugefuegt = "Das Lied ließ sich nicht hinzufügen. Bitte eine Datei im Format MP3, WAV, FLAC oder OGG wählen.";
    public const string LiedWaehlen = "Lied auswählen";
    public const string Dateiart = "Lieder (MP3, WAV, FLAC, OGG)";
    public const string OrteTitel = "Eigene Lieder spielen";
    public const string OrtInnen = "nur in den Räumen";
    public const string OrtStadt = "nur in der Stadt";
    public const string OrtUeberall = "überall";

    public static string Ort(string wert) => wert switch
    {
        Spiel.Ton.TonEinstellungen.Stadt => OrtStadt,
        Spiel.Ton.TonEinstellungen.Ueberall => OrtUeberall,
        _ => OrtInnen,
    };

    // Vollbild
    public const string Vollbild = "Vollbild";
    public const string VollbildSatz = "Das Programm füllt den ganzen Bildschirm, ohne Fensterrahmen und Leisten. Die Wahl gilt auch beim nächsten Start. Am PC schaltet auch die Taste F11 um.";

    // Tastenbelegung
    public const string TasteDruecken = "Taste drücken …";
    public const string TastenSatz = "Auf einen Knopf klicken, dann die neue Taste drücken. Esc bricht ab. Jede Taste kann nur einmal belegt sein.";
    public const string Standard = "Standard wiederherstellen";
    public const string Gesperrt = "„{0}“ bleibt dem Programm vorbehalten. Bitte eine andere Taste wählen.";
}
