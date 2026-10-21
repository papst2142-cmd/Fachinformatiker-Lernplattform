using System.Globalization;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Dashboard.DashboardTexte;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 2, L10: Texte des Abgleichs PC und Handy. T5 bis T7 nach dem Wortlaut P2-11
/// (anlagen/P2-11_Wortlaut_W4.md), alle übrigen nach der Textliste P2-9 mit den Entscheidungen
/// aus anlagen/P2-12_Wortlaut_Abgleich.md (Weisung P2-12). Mit (Py) markierte Sätze sind
/// zeichengleich aus Python v0.63.2 (app_gui, mobile/src/main.py, fisi_sync, fisi_optionen).
/// </summary>
public static class AbgleichTexte
{
    // E1 Bereich „Abgleich PC und Handy“ (Titel und Untertitel in Texte.Optionen)
    public const string Repository = "Repository (Benutzer/Name)";                  // (Py) E1.2
    public const string Token = "Zugangsschlüssel (Token)";                         // (Py) E1.3
    public const string Knopf = "Speichern und abgleichen";                         // (Py) E1.4
    /// <summary>E1.5 (Chat P2-12): ein Satz für PC und Handy.</summary>
    public const string Automatisch = "Automatisch abgleichen (beim Start, nach dem Lernen und beim Verlassen der App)";
    public const string Hinweis = "Auf PC und Handy dasselbe Repository und denselben Zugangsschlüssel eintragen.";   // (Py) E1.6

    /// <summary>T6: letzter Satz des Abgleich-Hinweises (ersetzt den Python-Satz zur Hilfe, E1.7).</summary>
    public const string Zugangsschluessel = "Der Zugangsschlüssel wird nur in der Einstellungsdatei dieses Geräts gespeichert, ohne Verschlüsselung, und nicht in Sicherungen oder in die Fehlerdatei übernommen.";

    // E4, E5 Einrichten
    public const string BitteEintragen = "Bitte Repository und Zugangsschlüssel eintragen.";   // (Py) E4
    /// <summary>T7: Beispiel im Repository-Hinweis (E5).</summary>
    public const string RepositoryBeispiel = "Das Repository bitte als „Benutzer/Name“ angeben, z. B. deinname/Fi-Lernstand.";
    public const string NichtEingerichtetFehler = "Der Abgleich ist noch nicht eingerichtet.";   // (Py) E5.1

    // A3 Lernstand nur lesbar
    public const string Ausgesetzt = "Der Abgleich ist ausgesetzt. %s";              // (Py) A3.1

    // Status unter dem Knopf
    public const string GleicheAb = "Gleiche ab …";                                  // (Py) S1
    public const string NichtEingerichtet = "Noch nicht eingerichtet.";              // (Py) S2
    public const string Zuletzt = "Zuletzt abgeglichen: %s";                         // (Py) S3
    public const string NochNie = "Noch nicht abgeglichen.";                         // (Py) S4
    public const string MischFertig = "Abgleich: volle Schutzwirkung aktiv.";         // (Py) S5
    public const string MischAktiv = "Abgleich: volle Schutzwirkung ab %s (ein Gerät mit älterer Version hat zuletzt am %s abgeglichen).";   // (Py) S6

    // A8 Ergebnis und Hinweise
    public const string AllesAktuell = "Alles auf dem neuesten Stand.";              // (Py) R1
    public const string Uebernommen = "%s von anderen Geräten übernommen";           // (Py) R2
    public const string Hochgeladen = "%s hochgeladen";                              // (Py) R3
    public const string Entfernt = "%s wegen einer Löschung entfernt";               // (Py) R4
    public const string UngueltigeMarke = "Die Abgleich-Datei enthält eine ungültige Löschmarke. Sie wurde nicht angewendet.";   // (Py) M1
    public const string MarkeGekappt = "Eine Löschmarke lag in der Zukunft (falsche Uhr) und wurde auf die Uhrzeit von GitHub gesetzt.";   // (Py) M2

    // A9 Fehler
    public const string TokenUngueltig = "Der Zugangsschlüssel (Token) ist ungültig oder abgelaufen.";   // (Py) F1
    public const string KeinSchreibrecht = "Der Zugangsschlüssel darf das Repository %s nicht beschreiben. Er braucht die Berechtigung „Contents: Read and write“.";   // (Py) F2
    public const string NichtGefunden = "Das Repository %s wurde nicht gefunden oder der Zugangsschlüssel hat keinen Zugriff darauf.";   // (Py) F3
    public const string Abgelehnt = "GitHub hat die Anfrage abgelehnt (HTTP %d).";   // (Py) F4
    public const string KeineVerbindung = "Keine Verbindung zu GitHub. Bitte die Internetverbindung prüfen.";   // (Py) F5
    public const string Unvollstaendig = "Die Antwort von GitHub war unvollständig. Bitte später noch einmal.";   // (Py) F6
    public const string DateiKaputt = "Die Datei lernstand.json.gz im Repository ist beschädigt.";   // (Py) F7
    public const string ZuNeu = "Der Lernstand im Repository stammt von einer neueren Programmversion. Bitte zuerst aktualisieren.";   // (Py) F8
    public const string HochladenAbgelehnt = "GitHub hat das Hochladen des Lernstands abgelehnt (HTTP 422). Der Lernstand auf diesem Gerät bleibt erhalten.";   // (Py) F9
    public const string Gleichzeitig = "Ein anderes Gerät hat gleichzeitig abgeglichen. Bitte gleich noch einmal versuchen.";   // (Py) F10
    /// <summary>F11 (Chat P2-12): an beiden Geräten, ohne Text der Ausnahme.</summary>
    public const string Unerwartet = "Unerwarteter Fehler beim Abgleich.";

    /// <summary>T5: Sicherheitskopie vor dem Abgleich gescheitert (AbgleichFehler.SicherungGescheitert).</summary>
    public const string SicherungGescheitert = "Der Abgleich wurde nicht durchgeführt, weil vorher keine Sicherheitskopie erstellt werden konnte. Dein Lernstand ist unverändert. Prüfe den freien Speicherplatz und versuche es später noch einmal.";

    /// <summary>P2-12 A2 (Chat): Übernahme oder Sicherung, während ein anderer Vorgang läuft.</summary>
    public const string Belegt = "Es läuft gerade ein Abgleich/eine Übernahme. Bitte später noch einmal.";

    private static string Setze(string vorlage, string wert) => vorlage.Replace("%s", wert, StringComparison.Ordinal);

    /// <summary>SyncResult.message: Teile mit „, “, Punkt am Ende, Hinweise mit Leerzeichen.</summary>
    public static string Ergebnis(AbgleichErgebnis e)
    {
        string text;
        if (e.Erhalten == 0 && e.Gesendet == 0 && e.Entfernt == 0)
        {
            text = AllesAktuell;
        }
        else
        {
            var teile = new List<string>();
            if (e.Erhalten != 0)
            {
                teile.Add(Setze(Uebernommen, Plural(e.Erhalten, "Eintrag", "Einträge")));
            }
            if (e.Gesendet != 0)
            {
                teile.Add(Setze(Hochgeladen, Plural(e.Gesendet, "Eintrag", "Einträge")));
            }
            if (e.Entfernt != 0)
            {
                teile.Add(Setze(Entfernt, Plural(e.Entfernt, "Eintrag", "Einträge")));
            }
            text = string.Join(", ", teile) + ".";
        }
        var hinweise = new List<string> { text };
        if (e.UngueltigeMarke)
        {
            hinweise.Add(UngueltigeMarke);
        }
        if (e.MarkeGekappt)
        {
            hinweise.Add(MarkeGekappt);
        }
        return string.Join(" ", hinweise);
    }

    /// <summary>Anzeigetext zu einem Fehler; repo wie eingetragen (ohne Leerzeichen und Schrägstriche außen).</summary>
    public static string Fehler(AbgleichAusnahme e, string repo) => e.Fehler switch
    {
        AbgleichFehler.NichtEingerichtet => NichtEingerichtetFehler,
        AbgleichFehler.RepositoryFormat => RepositoryBeispiel,
        AbgleichFehler.Ausgesetzt => Setze(Ausgesetzt, e.Grund is { } g && Lernstand.NurLesenZeilen.TryGetValue(g, out var zeile) ? zeile : "").Trim(),
        AbgleichFehler.TokenUngueltig => TokenUngueltig,
        AbgleichFehler.KeinSchreibrecht => Setze(KeinSchreibrecht, repo),
        AbgleichFehler.NichtGefunden => Setze(NichtGefunden, repo),
        AbgleichFehler.Abgelehnt => Abgelehnt.Replace("%d", e.Code.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal),
        AbgleichFehler.KeineVerbindung => KeineVerbindung,
        AbgleichFehler.Unvollstaendig => Unvollstaendig,
        AbgleichFehler.DateiKaputt => DateiKaputt,
        AbgleichFehler.ZuNeu => ZuNeu,
        AbgleichFehler.HochladenAbgelehnt => HochladenAbgelehnt,
        AbgleichFehler.Gleichzeitig => Gleichzeitig,
        AbgleichFehler.SicherungGescheitert => SicherungGescheitert,
        _ => Unerwartet,
    };

    /// <summary>_german_time: „2026-09-27 21:40:05“ → „27.09.2026 21:40“.</summary>
    public static string DeutscheZeit(string zeit) => zeit.Length >= 16
        ? zeit[8..10] + "." + zeit[5..7] + "." + zeit[..4] + " " + zeit[11..16]
        : zeit;

    /// <summary>Statuszeile ohne frisches Ergebnis (show_sync_status mit result None, error None).</summary>
    public static string Status(bool eingerichtet, string zuletzt) =>
        !eingerichtet ? NichtEingerichtet : zuletzt.Length > 0 ? Setze(Zuletzt, DeutscheZeit(zuletzt)) : NochNie;

    /// <summary>
    /// sync_mixed_text: "" wenn nicht eingerichtet oder noch nie abgeglichen; sonst S5 oder S6.
    /// mischbetrieb = Wert aus sync_meta, jetzt = Geräteuhr; Datumsangaben in der Ortszeit der Uhr.
    /// </summary>
    public static string Mischbetrieb(bool eingerichtet, string zuletzt, object? mischbetrieb, Uhr uhr)
    {
        if (!eingerichtet || zuletzt.Length == 0)
        {
            return "";
        }
        var gesehen = Loeschmarken.IsoUtc(mischbetrieb);
        if (gesehen is not { } g || (uhr.Jetzt().ToUniversalTime() - g).TotalSeconds > Loeschmarken.MischbetriebSekunden)
        {
            return MischFertig;
        }
        var bis = uhr.Ortszeit(g.AddSeconds(Loeschmarken.MischbetriebSekunden));
        var am = uhr.Ortszeit(g);
        var i = MischAktiv.IndexOf("%s", StringComparison.Ordinal);
        var j = MischAktiv.IndexOf("%s", i + 2, StringComparison.Ordinal);
        return MischAktiv[..i] + bis.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) + MischAktiv[(i + 2)..j]
            + am.ToString("dd.MM.", CultureInfo.InvariantCulture) + MischAktiv[(j + 2)..];
    }
}
