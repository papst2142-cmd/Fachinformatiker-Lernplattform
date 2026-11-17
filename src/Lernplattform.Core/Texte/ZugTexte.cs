using System.Globalization;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Welle A 0.88.0 (Inhaltspakete W2, Weisung P12-296, Plan P12-175 Abschnitte 3 und 5): Texte zu Bahnhof, Fahrplan, Zugfahrt
/// und Paket-Staedten. Die Saetze in Anfuehrung stehen woertlich im Plan (Nicos Antworten 1a und 5a); die anderen sind neu
/// (in Meldung P12-177 genannt).
/// </summary>
public static class ZugTexte
{
    /// <summary>Name von Stadt 1 im Fahrplan (Nico nennt sie so).</summary>
    public const string Stadt1 = "Stadt 1";

    public const string Fahrplan = "Fahrplan: {0}";

    public const string FahrplanTrenner = " · ";

    /// <summary>Knopf am Bahnhof: mit dem Zug in eine Stadt.</summary>
    public const string ZugNach = "Zug nach {0}";

    /// <summary>
    /// Plan Abschnitt 3 (woertlich „Paket fehlt (&lt;Groesse&gt;) – Laden“), davor der Name der Stadt: am Bahnhof koennen mehrere
    /// Pakete fehlen, jeder Knopf muss sagen, welche Stadt er laedt (auch fuer den Screenreader). {0} = Name, {1} = Groesse in MB.
    /// </summary>
    public const string PaketFehlt = "{0}: Paket fehlt ({1} MB) – Laden";

    /// <summary>Plan Abschnitt 3: ein neuer Stand wird nie von allein geladen.</summary>
    public const string NeuerStand = "{0}: neuer Stand da – Aktualisieren";

    /// <summary>Fahrplan ohne Angebot (noch nie gesucht): Suche nur auf Knopfdruck.</summary>
    public const string Suchen = "Weitere Städte suchen";

    public const string Fehlerhaft = "{0}: Paket fehlerhaft (siehe Optionen › Inhaltspakete)";

    public const string Laedt = "{0} wird geladen …";

    public const string Geladen = "{0} ist geladen. Du kannst jetzt mit dem Zug fahren.";

    public const string Angekommen = "Mit dem Zug in {0} angekommen.";

    public const string NichtAmBahnhof = "Der Zug fährt nur vom Bahnhof.";

    public const string AndereStadt = "Du bist gerade in einer anderen Stadt.";

    /// <summary>Plan Abschnitt 5 (Nico 5a, woertlich mit dem Namen der Stadt): Entfernen gesperrt.</summary>
    public const string EntfernenGesperrt = "Du bist gerade in {0}. Fahre erst mit dem Zug zurück.";

    /// <summary>Plan Abschnitt 5 (woertlich mit dem Namen der Stadt): zweites Geraet ohne das Paket.</summary>
    public const string PaketFehltHier = "Dein Spielstand ist gerade in {0}. Paket laden?";

    public const string PaketLaden = "Paket laden";

    /// <summary>Kein Spielschritt, solange das Paket der Stadt fehlt, in der die Figur ist.</summary>
    public const string KeinSchritt = "Erst das Paket laden: Dein Spielstand ist gerade in {0}.";

    /// <summary>Welle A 0.89.0 (Weisung P12-297 B): eine Quest einer Paket-Stadt loest man nur in dieser Stadt.</summary>
    public const string QuestAndereStadt = "Diesen Auftrag gibt es nur in seiner Stadt. Fahre erst mit dem Zug dorthin.";

    public static string Format(string text, params object?[] werte) => string.Format(CultureInfo.InvariantCulture, text, werte);
}
