namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 5 BE9/BE10: Texte des Updates wie fisi_update.py, app_gui.py (UpdateDialog, Bereich "Updates")
/// und mobile/src/main.py (show_update_dialog) in Python 0.63.2. Abweichungen stehen in der Wortliste W3
/// (Anlage P5-8_Wortliste_W3.md) und sind hier mit "W3-n" bezeichnet.
/// </summary>
public static class UpdateTexte
{
    // -- Suche (check_for_update) ---------------------------------------------
    public const string Abgelehnt = "GitHub hat die Anfrage abgelehnt (HTTP %d). Bitte später erneut versuchen.";
    public const string KeineVerbindung = "Keine Verbindung zu GitHub. Bitte die Internetverbindung prüfen.";
    public const string Unerwartet = "Die Antwort von GitHub ist unerwartet. Bitte später erneut versuchen.";
    public const string KeineDatei = "Für dein System liegt diesmal keine Datei bei. Auf der Download-Seite stehen alle Dateien dieses Releases.";

    /// <summary>W3-1: "Unerwarteter Fehler: %s" nennt die Fehlerart statt des Systemtexts.</summary>
    public const string UnerwarteterFehler = "Unerwarteter Fehler: %s";

    // -- Download (download) -------------------------------------------------
    public const string KeinInstaller = "Für dieses System gibt es keinen passenden Installer.";
    public const string DownloadHttp = "Der Download ist fehlgeschlagen (HTTP %d). Bitte später erneut versuchen.";
    public const string DownloadZeit = "Der Download hat zu lange gedauert. Bitte die Internetverbindung prüfen und erneut versuchen.";
    public const string DownloadOffline = "Keine Verbindung zum Download. Bitte die Internetverbindung prüfen.";
    public const string DownloadUnvollstaendig = "Der Download ist unvollständig. Bitte erneut versuchen.";
    public const string DownloadSpeichern = "Die Datei konnte nicht gespeichert werden. Bitte freien Speicherplatz prüfen und erneut versuchen.";
    public const string DownloadPruefsumme = "Die heruntergeladene Datei ist beschädigt (Prüfsumme stimmt nicht). Bitte erneut versuchen.";

    // -- Installation (install, Velopack) --------------------------------------
    /// <summary>W3-2: ein Satz fuer alle Systeme (Velopack ersetzt Inno-Installer und AppImage-Tausch).</summary>
    public const string Installiert = "Das Update wird installiert. Das Programm startet danach neu.";

    /// <summary>W3-3: Fehlertext von install ohne Systemtext.</summary>
    public const string StartFehler = "Die Installation konnte nicht gestartet werden. Einzelheiten stehen in update.log.";
    public const string NurInstalliert = "Automatische Updates sind nur in der installierten Anwendung möglich.";

    /// <summary>W3-4 (neu, Nico Frage 3): die Kopie des Lernstands vor dem Update liess sich nicht anlegen.</summary>
    public const string KopieFehler = "Vor dem Update ließ sich keine Kopie des Lernstands anlegen. Das Update wurde nicht gestartet, dein Lernstand ist unverändert.";

    // -- Pruefung beim naechsten Start (finish_pending_update) ----------------
    public const string FehlschlagTitel = "Update nicht abgeschlossen";
    public const string FehlschlagText = "Das Update auf Version %(neu)s wurde nicht installiert. Installiert ist weiterhin Version %(alt)s. "
        + "Dein Lernstand ist davon nicht betroffen.\n\n%(grund)s\n\nDas Protokoll liegt hier:\n%(pfad)s\n"
        + "Es steht auch unter Optionen › Diagnose und Werkzeuge › Problem melden.\n\n%(tipp)s";
    public const string TippWindows = "Tipp: PC neu starten und das Update erneut versuchen. Klappt es dann nicht, den Installer von der Download-Seite von Hand starten.";
    public const string TippAppImage = "Tipp: Das Update später erneut versuchen. Klappt es dann nicht, die neue AppImage-Datei von der Download-Seite laden, "
        + "ausführbar machen (z. B. chmod +x) und von Hand starten. Die bisherige Datei erst löschen, wenn die neue läuft.";
    public const string GrundUnbekannt = "Der Installer hat die Installation nicht abgeschlossen.";

    // -- Bereich "Updates" (PC und Handy gleich) -------------------------------
    public const string Suche = "Suche nach Updates …";
    public const string Neueste = "Du hast die neueste Version (%s).";
    public const string Verfuegbar = "Version %s ist verfügbar.";

    // -- Update-Fenster (UpdateDialog, Handy show_update_dialog) ---------------
    public const string FensterTitel = "Update verfügbar";
    public const string Installiertes = "installiert: %s";
    public const string Ueberschrift = "Version %s ist verfügbar";
    public const string Neuerungen = "Neuerungen";
    public const string KeineBeschreibung = "Keine Beschreibung vorhanden.";
    public const string Jetzt = "Jetzt aktualisieren";
    public const string ZurSeite = "Zur Download-Seite";
    public const string Spaeter = "Später";
    public const string Erneut = "Erneut versuchen";
    public const string Schliessen = "Schließen";
    public const string NichtInstalliert = "Automatisches Aktualisieren ist nur in der installierten Anwendung möglich. Die neue Version gibt es auf der Download-Seite.";
    public const string Laedt = "Update wird heruntergeladen …";
    public const string LaedtMb = "Update wird heruntergeladen … %s von %s MB";
    public const string Installiere = "Update wird installiert …";

    /// <summary>Handy: Knopf und Hinweise (show_update_dialog).</summary>
    public const string Herunterladen = "Herunterladen";
    public const string HandyHinweis = "Die neue Version wird im Browser heruntergeladen. Danach die Datei öffnen und „Installieren“ tippen - dein Lernstand bleibt erhalten.";
    public const string HandyOhneDatei = "Für Android ist in diesem Release keine App-Datei dabei.";
}
