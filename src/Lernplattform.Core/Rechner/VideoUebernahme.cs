namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3 W4b, V2 (Weisung P3-21, Auflage a): Zustand der Karte "Bild und Video" ohne
/// Oberflaeche. Der Knopf "In Übertragungsdauer übernehmen" ist nur aktiv, wenn die letzte
/// Berechnung ein Video-Ergebnis hatte; nach einer Feldaenderung, nach einem Fehler und ohne
/// Dauer ist er gesperrt, bis wieder berechnet wurde. Der Rechenweg nutzt die Zahlen der letzten
/// gueltigen Berechnung (wie W4a, F1a); ohne Dauer zeigt er nur die festen Schritte.
/// Eine Feldaenderung zaehlt nur, wenn die Felder danach anders sind als bei der Berechnung
/// (die Oberflaeche meldet Textaenderungen auch verspaetet, nach dem Rechnen).
/// </summary>
public sealed class VideoKartenZustand
{
    private (string, string, string, string, string, string)? _gerechnet;

    /// <summary>Video-Ergebnis fuer die Uebernahme; null = Knopf gesperrt.</summary>
    public VideoEingabe? Video { get; private set; }

    /// <summary>Zahlen der letzten gueltigen Berechnung fuer den Rechenweg (null = ohne Dauer).</summary>
    public VideoEingabe? Weg { get; private set; }

    public bool KnopfAktiv => Video is not null;

    /// <summary>Rechnen wie <see cref="VideoBerichte.Bericht"/>; bei RechnerFehler ist der Knopf danach gesperrt.</summary>
    public string Rechnen(string? breiteText, string? hoeheText, string tiefeText, string fpsText, string? minutenText, string? kompressionText)
    {
        Video = null;
        _gerechnet = null;
        var (text, video) = VideoBerichte.Bericht(breiteText, hoeheText, tiefeText, fpsText, minutenText, kompressionText);
        Video = video;
        Weg = video;
        _gerechnet = Felder(breiteText, hoeheText, tiefeText, fpsText, minutenText, kompressionText);
        return text;
    }

    /// <summary>
    /// Ein Feld oder die Farbtiefe wurde geaendert: stehen die Felder jetzt anders als bei der
    /// letzten Berechnung, ist der Knopf gesperrt bis zur naechsten Berechnung.
    /// </summary>
    public void Geaendert(string? breiteText, string? hoeheText, string tiefeText, string fpsText, string? minutenText, string? kompressionText)
    {
        if (_gerechnet != Felder(breiteText, hoeheText, tiefeText, fpsText, minutenText, kompressionText))
        {
            Video = null;
        }
    }

    private static (string, string, string, string, string, string) Felder(string? b, string? h, string t, string f, string? m, string? y) =>
        (b ?? "", h ?? "", t, f, m ?? "", y ?? "");
}

/// <summary>
/// Phase 3 W4b, V2 (Weisung P3-21, Auflagen a und b): eine Berechnung der Karte
/// "Übertragungsdauer". Kommt sie aus der Uebernahme, steht die Hinweiszeile ueber dem Ergebnis;
/// hat die Karte einen Eingabefehler, steht nur ihr roter Hinweis und keine Hinweiszeile. Eine
/// normale Berechnung (ohne Uebernahme) zeigt keine Hinweiszeile.
/// </summary>
public static class UebertragungKarte
{
    public static UebertragungAnzeige Rechnen(string? groesse, string einheit, string? rate, string rateEinheit, string? prozent,
        bool schmal, string? hinweis = null)
    {
        try
        {
            var eingabe = DatenBerichte.UebertragungLesen(groesse, einheit, rate, rateEinheit, prozent);
            return new UebertragungAnzeige(eingabe, DatenBerichte.UebertragungBericht(eingabe, schmal), "", hinweis ?? "");
        }
        catch (RechnerFehler f)
        {
            return new UebertragungAnzeige(null, "", f.Message, "");
        }
    }
}

/// <summary>Ergebnis einer Uebertragungs-Berechnung: Eingabe (null bei Fehler), Kasten, roter Hinweis, Hinweiszeile.</summary>
public sealed record UebertragungAnzeige(UebertragungEingabe? Eingabe, string Text, string Fehler, string Hinweis);
