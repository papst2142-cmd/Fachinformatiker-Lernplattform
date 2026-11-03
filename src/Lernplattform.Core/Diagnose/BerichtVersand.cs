using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Diagnose;

/// <summary>Ausgang von "Bericht senden".</summary>
public enum VersandArt
{
    Gesendet,
    /// <summary>Ohne eingebauten Schluessel gebaut: nichts gesendet.</summary>
    NichtVerfuegbar,
    /// <summary>Keine Verbindung, Zeit ueberschritten oder Antwort abgebrochen.</summary>
    KeineVerbindung,
    /// <summary>HTTP 401, 403 oder 404: Schluessel ungueltig, abgelaufen oder ohne Recht auf das Repo.</summary>
    Abgelehnt,
    /// <summary>Anderer HTTP-Code.</summary>
    Fehler,
}

/// <summary>Ergebnis: Art, Pfad der Datei im Repo (nur bei Gesendet) und HTTP-Code (0 ohne Antwort).</summary>
public sealed record VersandErgebnis(VersandArt Art, string? Datei, int Code);

/// <summary>
/// Phase 11 (Weisungen P11-29, P11-30, Sammelliste 202): "Bericht senden" legt den Problembericht als Datei im privaten
/// Repo papst2142-cmd/fi-fehlerberichte ab (Zweig main, Ordner berichte), ein Commit je Bericht ueber die Contents-API
/// (PUT). Nur auf Knopfdruck, nie von allein, kein Wiederholen ohne Druck. Netz ueber <see cref="GitHubZugang"/>
/// (gleiche Kopfzeilen und Wartezeiten wie der Abgleich, keine Weiterleitung bei PUT, der Schluessel steht nur im Kopf
/// der Anfrage). Gibt es den Namen schon (gleiche Sekunde), haengt der Versand "_2" bis "_9" an.
/// BEFRISTET bis 1.0 (Weisung P11-29): siehe <see cref="BerichtSchluessel"/>.
/// </summary>
public static class BerichtVersand
{
    public const string Repo = "papst2142-cmd/fi-fehlerberichte";
    public const string Zweig = "main";
    public const string Ordner = "berichte";
    private const int HoechstNummer = 9;

    /// <summary>Wurde das Programm mit Schluessel gebaut?</summary>
    public static bool Verfuegbar => BerichtSchluessel.Wert() is not null;

    /// <summary>Nur fuer Tests: Attrappe statt GitHub, eigener Schluessel und eigenes fehler.log.</summary>
    internal sealed record Zugriff(GitHubZugang Zugang, string? Schluessel, string? Log);

    /// <summary>
    /// Dateiname berichte/&lt;JJJJ-MM-TT&gt;_&lt;HHMMSS&gt;_&lt;PC|Handy&gt;_&lt;Version&gt;.md (Ortszeit des Geraets, Version wie
    /// angezeigt ohne Leerzeichen, z. B. A0.36.0); ab nummer 2 mit "_nummer" vor ".md".
    /// </summary>
    public static string DateiName(DateTime zeit, string geraet, string version, int nummer = 1) =>
        Ordner + "/" + zeit.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + "_" + geraet + "_"
        + Sicher(version) + (nummer > 1 ? "_" + nummer.ToString(CultureInfo.InvariantCulture) : "") + ".md";

    /// <summary>Probe der CI: berichte/_probe_&lt;Lauf&gt;.md.</summary>
    public static string ProbeName(string lauf, int nummer = 1) =>
        Ordner + "/_probe_" + Sicher(lauf) + (nummer > 1 ? "_" + nummer.ToString(CultureInfo.InvariantCulture) : "") + ".md";

    /// <summary>Nur Buchstaben, Ziffern, Punkt, Minus und Unterstrich im Dateinamen.</summary>
    private static string Sicher(string text)
    {
        var aus = new StringBuilder();
        foreach (var z in text)
        {
            if (z is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_')
            {
                aus.Append(z);
            }
        }
        return aus.Length == 0 ? "unbekannt" : aus.ToString();
    }

    /// <summary>
    /// Inhalt der Datei: der Bericht unveraendert in einem Textblock (GitHub zeigt ihn so Zeile fuer Zeile); mit
    /// Beschreibung (hoechstens 2000 Zeichen) steht sie unter eigener Ueberschrift darueber. Leer = nur der Bericht.
    /// </summary>
    public static string Inhalt(string bericht, string? beschreibung)
    {
        var zaun = "```";
        while (bericht.Contains(zaun, StringComparison.Ordinal))
        {
            zaun += "`";
        }
        var block = zaun + "text\n" + bericht.TrimEnd('\n') + "\n" + zaun + "\n";
        var text = (beschreibung ?? "").Trim();
        if (text.Length == 0)
        {
            return block;
        }
        if (text.Length > BerichtSendenTexte.FeldHoechst)
        {
            text = text[..BerichtSendenTexte.FeldHoechst];
        }
        return BerichtSendenTexte.KopfBeschreibung + "\n\n" + text + "\n\n" + BerichtSendenTexte.KopfBericht + "\n\n" + block;
    }

    /// <summary>Bericht senden (blockiert bis zur Antwort, im Hintergrund aufrufen).</summary>
    public static VersandErgebnis Senden(string inhalt, DateTime zeit, string geraet, string version) =>
        Senden(inhalt, zeit, geraet, version, null);

    internal static VersandErgebnis Senden(string inhalt, DateTime zeit, string geraet, string version, Zugriff? test) =>
        Hochladen(inhalt, n => DateiName(zeit, geraet, version, n), "Problembericht " + geraet + " " + version, test);

    /// <summary>Probe der CI mit derselben Strecke (Datei berichte/_probe_&lt;Lauf&gt;.md).</summary>
    public static VersandErgebnis Probe(string lauf, string version, DateTime zeit) =>
        Hochladen("Probe der CI\n\nLauf: " + lauf + "\nVersion: " + version + "\nZeit: "
            + zeit.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n",
            n => ProbeName(lauf, n), "Probe " + lauf, null);

    private static VersandErgebnis Hochladen(string inhalt, Func<int, string> name, string nachricht, Zugriff? test)
    {
        GitHubZugang zugang;
        string? schluessel;
        var log = test?.Log;
        if (test is not null)
        {
            (zugang, schluessel) = (test.Zugang, test.Schluessel);
        }
        else
        {
            schluessel = BerichtSchluessel.Wert();
            if (schluessel is null)
            {
                return new VersandErgebnis(VersandArt.NichtVerfuegbar, null, 0);
            }
            zugang = new GitHubZugang();
        }
        if (schluessel is null)
        {
            return new VersandErgebnis(VersandArt.NichtVerfuegbar, null, 0);
        }
        using (zugang)
        {
            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(inhalt));
            for (var n = 1; ; n++)
            {
                var datei = name(n);
                var koerper = new JsonObject { ["message"] = nachricht, ["content"] = b64, ["branch"] = Zweig };
                try
                {
                    zugang.Anfrage(HttpMethod.Put, "/repos/" + Repo + "/contents/" + datei, schluessel,
                        Encoding.UTF8.GetBytes(koerper.ToJsonString()));
                    return new VersandErgebnis(VersandArt.Gesendet, datei, 200);
                }
                catch (GitHubAbgelehnt e) when (e.Code == 422 && n < HoechstNummer)
                {
                    // Name schon vergeben (gleiche Sekunde): naechste Nummer
                }
                catch (GitHubAbgelehnt e)
                {
                    return Melden(new VersandErgebnis(e.Code is 401 or 403 or 404 ? VersandArt.Abgelehnt : VersandArt.Fehler, null, e.Code), log);
                }
                catch (AbgleichAusnahme)
                {
                    return Melden(new VersandErgebnis(VersandArt.KeineVerbindung, null, 0), log);
                }
            }
        }
    }

    /// <summary>Zeile im fehler.log: nur Art und HTTP-Code, nie der Schluessel.</summary>
    private static VersandErgebnis Melden(VersandErgebnis ergebnis, string? log)
    {
        Fehlerprotokoll.Schreiben("Bericht senden fehlgeschlagen: " + ergebnis.Art
            + (ergebnis.Code > 0 ? " (HTTP " + ergebnis.Code.ToString(CultureInfo.InvariantCulture) + ")" : ""), log);
        return ergebnis;
    }

    /// <summary>Text fuer das Fenster nach dem Senden (Titel, Text).</summary>
    public static (string Titel, string Text) Rueckmeldung(VersandErgebnis ergebnis) => ergebnis.Art switch
    {
        VersandArt.Gesendet => (BerichtSendenTexte.GesendetTitel,
            // Umbruchstellen nach "_" (wie UebernahmeTexte): sonst trennt eine schmale Anzeige mit grosser Schrift mitten im Namen
            Beschriftungen.Einsetzen(BerichtSendenTexte.Gesendet, Path.GetFileName(ergebnis.Datei ?? "").Replace("_", "_\u200B", StringComparison.Ordinal))),
        VersandArt.NichtVerfuegbar => (BerichtSendenTexte.RueckfrageTitel, BerichtSendenTexte.NichtVerfuegbar),
        VersandArt.KeineVerbindung => (BerichtSendenTexte.FehlerTitel,
            Beschriftungen.Einsetzen(BerichtSendenTexte.Fehler, BerichtSendenTexte.GrundVerbindung)),
        VersandArt.Abgelehnt => (BerichtSendenTexte.FehlerTitel,
            Beschriftungen.Einsetzen(BerichtSendenTexte.Fehler, BerichtSendenTexte.GrundAbgelehnt)),
        _ => (BerichtSendenTexte.FehlerTitel, Beschriftungen.Einsetzen(BerichtSendenTexte.Fehler,
            Beschriftungen.Einsetzen(BerichtSendenTexte.GrundCode, ergebnis.Code.ToString(CultureInfo.InvariantCulture)))),
    };
}
