using System.Globalization;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Welle A 0.87.0 (Weisung P12-294, Plan P12-175, Nico 1a 2a 3b 4a 5a): Texte der Inhaltspakete (Karte „Inhaltspakete“ in den
/// Optionen, Gruende der Pruefung). PC und Handy gleich.
/// </summary>
public static class PaketTexte
{
    public const string Titel = "Inhaltspakete";
    public const string Kurz = "Neue Städte zum Nachladen";
    public static readonly IReadOnlyList<string> Stichwoerter = ["Inhaltspakete", "Pakete", "DLC", "Städte", "nachladen"];

    public const string Satz = "Neue Städte zum Nachladen, ohne die App zu tauschen. Geladene Pakete gehen auch ohne Netz und bleiben bei "
        + "jedem App-Update erhalten. Nichts lädt von allein.";
    public const string Suchen = "Nach Paketen suchen";
    public const string Sucht = "Suche nach Paketen …";
    public const string KeineVerbindung = "Keine Verbindung. Geladene Pakete gehen weiter.";
    public const string Keine = "Keine Pakete geladen.";
    public const string KeineAngebote = "Gerade gibt es keine Pakete zum Laden.";
    public const string Gesucht = "Zuletzt gesucht: heute {0} Uhr";

    public const string Laden = "Laden";
    public const string Aktualisieren = "Aktualisieren";
    public const string NeuLaden = "Neu laden";
    public const string Entfernen = "Entfernen";
    public const string Abbrechen = "Abbrechen";

    public const string ZustandGeladen = "Geladen, Stand {0}";
    public const string ZustandNeu = "Geladen, Stand {0} · neuer Stand {1} da";
    public const string ZustandNicht = "Nicht geladen";
    public const string ZustandFehler = "Fehlerhaft: {0}";
    public const string LaedtMb = "{0} von {1} MB · danach Prüfung";
    public const string Prueft = "Wird geprüft …";
    public const string Abgebrochen = "Laden abgebrochen. Der bisherige Stand bleibt.";
    public const string Geladen = "Paket „{0}“ geladen.";
    public const string Entfernt = "Paket „{0}“ entfernt.";

    public const string MobilTitel = "Über mobile Daten laden?";
    public const string MobilFrage = "Du bist nicht im WLAN. Das Paket „{0}“ ist {1} MB groß.";
    public const string MobilJa = "Jetzt laden";
    public const string MobilNein = "Später im WLAN";
    public const string EntfernenTitel = "Paket entfernen?";
    public const string EntfernenFrage = "Das Paket „{0}“ wird vom Gerät entfernt. Du kannst es später wieder laden.";

    // Gruende der Pruefung (rot in der Liste)
    public const string KeinArchiv = "Die Datei ist kein Inhaltspaket (kein lesbares Archiv).";
    public const string KopfFehlt = "Die Datei ist kein Inhaltspaket (paket.json fehlt).";
    public const string KopfKaputt = "Der Kopf des Pakets (paket.json) ist nicht lesbar.";
    public const string FeldFehlt = "Im Kopf des Pakets fehlt oder stimmt das Feld „{0}“ nicht.";
    public const string FormatNeu = "Dieses Paket hat Format {0}, die App kennt nur Format {1}. Bitte die App aktualisieren.";
    public const string ArtUnbekannt = "Diese Art von Paket kennt die App noch nicht. Bitte die App aktualisieren.";
    public const string AppAlt = "Dieses Paket braucht {0}. Bitte die App aktualisieren.";
    public const string KennungReserviert = "Die Kennung „{0}“ gehört zur App und kann kein Paket sein.";
    public const string DateiFehlt = "Im Paket fehlt die Datei „{0}“.";
    public const string DateiFremd = "Im Paket liegt eine unerwartete Datei „{0}“.";
    public const string Pruefsumme = "Prüfsumme stimmt nicht: {0}. Bitte neu laden.";
    public const string ZuGross = "Das Paket ist größer als erlaubt.";
    public const string Anders = "Das geladene Paket passt nicht zum Angebot (erwartet {0}, Stand {1}).";
    public const string StadtFehler = "Die Stadt im Paket ist fehlerhaft: {0}";
    public const string Speichern = "Das Paket konnte nicht gespeichert werden: {0}";
    public const string Netz = "Laden fehlgeschlagen: {0}";
    public const string QuelleFehlt = "Die Testquelle der Pakete wurde nicht gefunden.";

    /// <summary>
    /// Groesse in MB mit Komma: ab 1 MB eine Nachkommastelle (12,1), darunter zwei (0,05), damit kleine Pakete nie „0,0 MB“
    /// zeigen (Welle A 0.88.0, Sammelliste 374, Nico a). Gegenprobe <see cref="MbAltFuerTests"/> (AsyncLocal): wie bis A 0.87.0.
    /// </summary>
    public static string Mb(long bytes)
    {
        var mb = bytes / (1024.0 * 1024.0);
        var muster = !MbAlt.Value && Math.Round(mb, 1, MidpointRounding.AwayFromZero) < 1.0 ? "0.00" : "0.0";
        return mb.ToString(muster, CultureInfo.GetCultureInfo("de-DE"));
    }

    /// <summary>Gegenprobe Sammelliste 374: Groesse immer mit einer Nachkommastelle (wie bis A 0.87.0).</summary>
    public static bool MbAltFuerTests
    {
        get => MbAlt.Value;
        set => MbAlt.Value = value;
    }

    private static readonly AsyncLocal<bool> MbAlt = new();

    /// <summary>
    /// Welle A 0.88.0 (Sammelliste 374, Nico a): kann ein neues Laden diesen Grund loesen? Nein bei Gruenden, die nur ein
    /// App-Update loest (unbekannte Art, Format zu neu, App zu alt) und bei einer Kennung der App; dort bietet die Liste kein
    /// „Neu laden“ an, der Grund nennt das App-Update. Gegenprobe <see cref="NeuLadenAltFuerTests"/> (AsyncLocal).
    /// </summary>
    public static bool NeuLadenHilft(string grund) =>
        NeuLadenAlt.Value
        || !(grund == ArtUnbekannt || Beginnt(grund, FormatNeu) || Beginnt(grund, AppAlt) || Beginnt(grund, KennungReserviert));

    /// <summary>Gegenprobe Sammelliste 374: „Neu laden“ bei jedem Grund (wie bis A 0.87.0).</summary>
    public static bool NeuLadenAltFuerTests
    {
        get => NeuLadenAlt.Value;
        set => NeuLadenAlt.Value = value;
    }

    private static readonly AsyncLocal<bool> NeuLadenAlt = new();

    private static bool Beginnt(string grund, string vorlage)
    {
        var fest = vorlage[..vorlage.IndexOf('{', StringComparison.Ordinal)];
        return grund.StartsWith(fest, StringComparison.Ordinal);
    }

    public static string Format(string vorlage, params object?[] werte) => string.Format(CultureInfo.InvariantCulture, vorlage, werte);
}
