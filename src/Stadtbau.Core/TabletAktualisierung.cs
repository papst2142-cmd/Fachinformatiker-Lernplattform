using Lernplattform.Core.Update;

namespace Stadtbau.Core;

/// <summary>
/// S 0.6.0 (Weisung P12-42 Punkt 2): Update des Werkzeugs am Android-Tablet ohne Oberflaeche (die Fenster macht
/// Stadtbau.Oberflaeche.TabletUpdate). Gemeinsam mit der Lernplattform: <see cref="HandyQuelle"/> mit Linie Stadtbau
/// (Suche ueber die Liste der Releases, nur vS, nie ein Release der Lernplattform) und <see cref="HandyPruefung"/>
/// (SHA-256 aus zwei Quellen, Paketname, genau ein Zertifikat gleich android_fingerabdruck.txt, hoeherer Versionscode,
/// Platz). Projekte sind nach jeder Aenderung gespeichert, deshalb keine Kopie und kein Merker wie in der Lernplattform.
/// Protokoll update.log im Werkzeugordner.
/// </summary>
public static class TabletAktualisierung
{
    /// <summary>
    /// Quelle des Werkzeugs (MainActivity und Tests bauen sie gleich): Linie Stadtbau, Version <see cref="Werkzeug.Anzeige"/>
    /// (<paramref name="aktuell"/> nur fuer Tests, damit sie nicht von der laufenden Version abhaengen).
    /// </summary>
    public static HandyQuelle Quelle(HttpClient http, IHandyGeraet geraet, string protokollordner, string url = GithubSuche.ListeUrl,
        string fingerabdruck = HandyPruefung.Fingerabdruck, string? aktuell = null) =>
        new(http, geraet, aktuell ?? Werkzeug.Anzeige, url, fingerabdruck, protokollordner, ReleaseLinie.Linie.Stadtbau);

    /// <summary>
    /// Laden, Pruefen (Groesse, beide Pruefsummen, Paket, Zertifikat, Versionscode, Platz) und an Android uebergeben.
    /// null = uebergeben (das Ergebnis kommt spaeter ueber <see cref="Rueckmeldung"/>), sonst der Text fuer das Fenster.
    /// </summary>
    public static async Task<string?> UebergebenAsync(HandyQuelle quelle, UpdateAngebot angebot, string protokollordner, Action<string> fehler)
    {
        OrdnerAnlegen(protokollordner);
        try
        {
            await UpdateAblauf.LadenAsync(quelle, angebot, null, protokollordner).ConfigureAwait(false);
            Updateprotokoll.Schreiben("Installer wird gestartet: " + quelle.Name + ", Version " + angebot.Version + ", " + angebot.DateiName, protokollordner);
            quelle.Installieren(angebot);
            Updateprotokoll.Schreiben("Installer läuft. Android ersetzt das Werkzeug und schließt es dafür.", protokollordner);
            return null;
        }
        catch (UpdateFehler x)
        {
            return Texte.TabletText(x.Message);
        }
        catch (Exception x)
        {
            fehler("Update: " + x.GetType().Name + ": " + x.Message);
            Updateprotokoll.Schreiben("Installer ließ sich nicht starten: " + x.GetType().Name, protokollordner);
            return UpdateAblauf.Unerwartet(x);
        }
    }

    /// <summary>Rueckmeldung von Android: Zeile in update.log, bei einem Fehler Datei weg und der Text fuer das Fenster.</summary>
    public static string? Rueckmeldung(int status, string? meldung, long groesse, string ladeordner, string protokollordner) =>
        OrdnerAnlegen(protokollordner) is var _ && HandyQuelle.Rueckmeldung(status, meldung, groesse, null, ladeordner, protokollordner) is { } text ? Texte.TabletText(text) : null;

    /// <summary>
    /// Selbst gefunden (Emulator-Probe, Lauf 587): am frischen Tablet gibt es den Werkzeugordner erst mit dem ersten
    /// Projekt; update.log (Updateprotokoll schreibt still nichts, wenn der Ordner fehlt) ging dann verloren. Deshalb
    /// legt das Update den Ordner vor der ersten Zeile an. Scheitert das, schreibt das Protokoll wie bisher nichts.
    /// </summary>
    private static bool OrdnerAnlegen(string ordner)
    {
        try
        {
            Directory.CreateDirectory(ordner);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
