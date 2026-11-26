using System.Globalization;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Welle A 0.87.0 (Weisung P12-294, Plan P12-175, Nico 1a 2a 3b 4a 5a): Texte der Inhaltspakete (Karte „Inhaltspakete“ in den
/// Optionen, Gruende der Pruefung). PC und Handy gleich.
/// </summary>
public static class PaketTexte
{
    public const string Titel = "Inhaltspakete";
    public const string Kurz = "Neue Städte und Fachrichtungs-Pakete";
    public static readonly IReadOnlyList<string> Stichwoerter = ["Inhaltspakete", "Pakete", "DLC", "Städte", "nachladen"];

    public const string Satz = "Neue Städte und neue Lerninhalte je Fachrichtung, ohne die App zu tauschen. Geladene Pakete gehen auch ohne "
        + "Netz und bleiben bei jedem App-Update erhalten. Nichts lädt von allein.";
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

    /// <summary>Welle A 0.89.0 (Weisung P12-297 W3-2): die Stadt traegt die Kennung eines anderen Pakets.</summary>
    public const string StadtAnderesPaket = "Die Stadt im Paket gehört zum Paket „{0}“.";
    // Welle A 0.91.0 (Weisung P12-300, Nico 2c): Fachrichtungs-Pakete (Art „lernen“), Name „Fachrichtungs-Paket FIAE“ (seit A 1.14.0 sichtbar FIAE, der Kopf im Paket bleibt bis zum naechsten Paketstand)
    public const string FrName = "Fachrichtungs-Paket {0}";
    public const string FrText = "Lerninhalte der Fachrichtung {0}: Karteikarten, Quiz, AP2-Szenarien, Testprojekte, Fachgespräch und Prüfungssätze.";
    public const string ZustandEingebaut = "In der App enthalten";
    public const string ZustandEingebautNeu = "In der App enthalten · neuer Stand {0} da";
    public const string KennungArt = "Die Kennung „{0}“ passt nicht zur Art des Pakets.";
    public const string LernenAufbau = "Der Inhalt des Pakets ist fehlerhaft ({0}: {1}).";
    public const string StelleDoppelt = "Stelle {0} doppelt oder nicht der Reihe nach";
    public const string StelleFremd = "Eintrag an Stelle {0} gehört nicht zu dieser Fachrichtung";
    public const string StelleBelegt = "Das Paket verschiebt Inhalte: {0}, Stelle {1} gehört in der App zu einem anderen Eintrag.";
    public const string EintragFehlt = "Das Paket ist älter als die App: in {0} fehlt Stelle {1}.";
    public const string EintragAnders = "Das Paket ändert einen Inhalt der App: {0}, Stelle {1}.";

    /// <summary>Hinweis auf einen neuen Stand (Nico 3a): in der Karte und einmal oben in der Lernansicht.</summary>
    public const string FrHinweisNeu = "Neue Inhalte für {0}: Fachrichtungs-Paket {0}, Stand {1}, {2} MB.";
    public const string Spaeter = "Später";

    /// <summary>Rueckfrage, wenn eine Fachrichtung keine Inhalte hat (Weisung P12-300 Punkt 4, wortgleich).</summary>
    public const string FrFehltTitel = "Fachrichtungs-Paket fehlt";
    public const string FrFehltFrage = "Für {0} fehlt das Fachrichtungs-Paket ({1} MB). Jetzt laden?";
    public const string FrFehltOhneNetz = "Für {0} fehlt das Fachrichtungs-Paket. Ohne Verbindung kann es gerade nicht geladen werden. Dein Lernstand bleibt.";
    public const string FrFehltSicht = "Für {0} fehlt das Fachrichtungs-Paket. Du siehst die gemeinsamen Inhalte. Dein Lernstand bleibt.";

    /// <summary>Entfernen gesperrt, solange ein Pruefungssatz dieser Fachrichtung offen ist.</summary>
    public const string FrEntfernenGesperrt = "Du schreibst gerade einen {0}-Prüfungssatz. Erst beenden oder abbrechen.";
    public const string FrEntfernenFrage = "Das Fachrichtungs-Paket {0} wird vom Gerät entfernt. Danach gilt wieder der Stand, den die App mitbringt. Dein Lernstand bleibt.";
    public const string FrEntfernenFrageOhne = "Das Fachrichtungs-Paket {0} wird vom Gerät entfernt. Danach fehlen die Inhalte für {0}, bis du es wieder lädst. Dein Lernstand bleibt.";

    /// <summary>Der offene Pruefungssatz fehlt (z. B. nach einem Abgleich); sein gespeicherter Stand bleibt.</summary>
    public const string FrSatzFehlt = "Dieser Prüfungssatz gehört zum Fachrichtungs-Paket {0}. Bitte laden, um weiterzumachen. Dein Stand bleibt gespeichert.";

    /// <summary>Hinweis nach einem Abgleich: der Lernstand kennt Inhalte, die auf diesem Geraet fehlen.</summary>
    public const string FrNachAbgleich = "Auf diesem Gerät fehlen neuere Inhalte für {0}. Bitte das Fachrichtungs-Paket {0} laden (Optionen › Inhaltspakete). Nichts geht verloren.";

    /// <summary>Anzeige der Fachrichtung im Text (FIAE, FIDP, FIDV; seit A 1.14.0 wie bei der IHK, Weisung P12-400).</summary>
    public static string Fr(string fachrichtung) => FachrichtungTexte.KuerzelFuer(fachrichtung);

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
