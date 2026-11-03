namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 11 (Weisungen P11-29, P11-30, Sammelliste 202): "Bericht senden" unter "Problem melden", nur in C#
/// (Python kennt den Knopf nicht; die Liste <see cref="DiagnoseTexte.Knoepfe"/> und der Hilfetext bleiben wie Python).
/// Wortlaut von Chat freigegeben (Meldung P11-22, Frage 1a; Feld laut Weisung P11-30). PC und Handy gleich.
/// BEFRISTET bis 1.0 (Weisung P11-29): Senden mit eingebautem Schluessel, vor 1.0 Umbau ohne Konto und ohne Schluessel.
/// </summary>
public static class BerichtSendenTexte
{
    public const string Knopf = "Bericht senden";
    public const string Hinweis = "„Bericht senden“ schickt den Bericht oben direkt an den Herausgeber der Lernplattform. "
        + "Es wird nur gesendet, wenn du den Knopf drückst.";

    public const string RueckfrageTitel = "Bericht senden";
    public const string Rueckfrage = "Der Bericht oben wird an den Herausgeber der Lernplattform gesendet und dort in einem "
        + "nicht öffentlichen Speicher abgelegt. Er enthält keine Passwörter, keine Zugangsschlüssel, nicht deinen "
        + "Benutzernamen in Pfaden und nicht deinen Lernstand. Jetzt senden?";
    public const string Senden = "Senden";
    public const string Abbrechen = "Abbrechen";

    /// <summary>Freies Feld in der Rueckfrage (Weisung P11-30, Frage 2a).</summary>
    public const string Feld = "Was ist passiert? (freiwillig)";
    public const string FeldHinweis = "Schreibe keine Passwörter oder Namen hinein.";
    /// <summary>Hoechstlaenge des freien Felds in Zeichen.</summary>
    public const int FeldHoechst = 2000;

    public const string Sendet = "Bericht wird gesendet …";

    public const string GesendetTitel = "Bericht gesendet";
    /// <summary>%s = Dateiname.</summary>
    public const string Gesendet = "Danke! Der Bericht ist angekommen. Dateiname: %s";

    public const string FehlerTitel = "Bericht nicht gesendet";
    /// <summary>%s = einer der Gruende unten (endet mit Punkt).</summary>
    public const string Fehler = "Der Bericht ließ sich nicht senden: %s Du kannst ihn stattdessen als Datei speichern "
        + "oder kopieren und weitergeben.";
    public const string GrundVerbindung = "Keine Verbindung zum Internet oder Zeit überschritten.";
    public const string GrundAbgelehnt = "Der Speicher hat den Bericht abgelehnt (Zugang ungültig oder abgelaufen).";
    /// <summary>%s = HTTP-Code.</summary>
    public const string GrundCode = "Der Speicher meldet einen Fehler (HTTP %s).";

    /// <summary>Ohne eingebauten Schluessel (lokale Bauten, PR-Laeufe).</summary>
    public const string NichtVerfuegbar = "Senden ist in dieser Ausgabe des Programms nicht verfügbar. Speichere den "
        + "Bericht als Datei oder kopiere ihn und gib ihn weiter.";

    /// <summary>Ueberschriften der gesendeten Datei, wenn eine Beschreibung dabei ist.</summary>
    public const string KopfBeschreibung = "## Beschreibung";
    public const string KopfBericht = "## Bericht";
}
