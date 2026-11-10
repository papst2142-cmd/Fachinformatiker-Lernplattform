using Lernplattform.Core.Texte;
using Lernplattform.Core.Update;
using Lernplattform.Programm;
using Lernplattform.Views;
using Update = Lernplattform.Aktualisierung.Aktualisierung;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 5, W3 (Plan Phase 5, BE9 und BE10): Bereich „Updates“ mit Fehlerzeile, das Fenster „Neue
/// Version“ (installiert, Ordner-Programm, Handy, waehrend des Ladens, nach einem Fehler) und das Fenster
/// „Update nicht abgeschlossen“. Kein Netz, nichts wird geladen, gespeichert oder geschaltet: die Fenster
/// werden nur gezeigt und mit Dialog.AlleSchliessen geschlossen.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Anzahl der W3-Ansichten (fuer den Bericht): der Bereich, fuenf Mal "Update verfügbar", ein Fehlschlag.</summary>
    public const int P5W3AnsichtenAnzahl = 1 + 5 + 1;

    /// <summary>Fehlender Update-Bereich (zaehlt als Fund im Urteil).</summary>
    private static int _w3Fehlt;

    /// <summary>
    /// Phase 11 FW1 (Sammelliste 78): die Probe nennt die laufende Version als installiert und den naechsten Fix
    /// als angeboten (vorher fest "A 0.11.0" und "A 0.11.1"); die Texte wachsen mit der Version nicht mehr auseinander.
    /// </summary>
    private static UpdateVersion ProbeInstalliert => UpdateVersion.Lesen(Update.Anzeige) ?? new UpdateVersion(UpdateVersion.StufeA, 0, 0, 0);

    private static UpdateVersion ProbeNeu => ProbeInstalliert with { Fix = ProbeInstalliert.Fix + 1 };

    // laengste Notizen wie ein echtes Release (Abschnitt "### Herunterladen" faellt weg)
    private static UpdateAngebot ProbeAngebot(bool mitDatei) => new(ProbeNeu.Anzeige,
        "## " + ProbeNeu.Anzeige + "\n\n- **Neu:** Update im Programm mit Kopie des Lernstands vor jedem Update\n- Fix: Abgleich zwischen PC und Handy nach langer Pause\n- Fix: Sehr lange Themennamen im Prüfungstrainer brechen sauber um\n\nAlle Änderungen stehen in AENDERUNGEN.md.\n\n### Herunterladen\n- Datei sha256 …",
        GithubSuche.ReleaseSeite, mitDatei ? "Fachinformatiker-Lernplattform-" + ProbeNeu.Paket + "-full.nupkg" : null,
        mitDatei ? "Fachinformatiker-Lernplattform-" + ProbeNeu.Paket + "-full.nupkg" : null, 89_128_960);

    private static async IAsyncEnumerable<string> P5W3Ansichten(MainView ansicht)
    {
        await ansicht.ZeigeAsync(Seiten.Optionen);
        var optionen = (OptionenSeite)ansicht.GebauteSeiten[Seiten.Optionen];
        optionen.OeffneBereich(Optionen.Updates, false);
        var zeile = optionen.UpdateErgebnis;
        if (zeile is null)
        {
            Messwerte.Notiere("  FEHLER: Optionen ohne Bereich \"Updates\"");
            _w3Fehlt++;
        }
        else
        {
            OptionenSeite.UpdateZeile(zeile, UpdateTexte.KeineVerbindung, "Pinsel.red");
        }
        await ansicht.BildFertigAsync();
        yield return "optionen/updates";
        if (zeile is not null)
        {
            zeile.IsVisible = false;
        }
        foreach (var (name, fenster, zustand) in new (string, UpdateFenster, Action<UpdateFenster>?)[]
        {
            ("update/neue-version", new UpdateFenster(ProbeAngebot(true), true, pc: true), null),
            ("update/ordner-programm", new UpdateFenster(ProbeAngebot(true), false, pc: true), null),
            ("update/handy", new UpdateFenster(ProbeAngebot(true), false, pc: false), null),
            ("update/laedt", new UpdateFenster(ProbeAngebot(true), true, pc: true),
                f => f.Vorschau(Beschriftungen.Einsetzen(UpdateTexte.LaedtMb, "37,4", "85,0"), "Pinsel.text_dim", 0.44)),
            ("update/fehler", new UpdateFenster(ProbeAngebot(true), true, pc: true), f => f.Vorschau(UpdateTexte.DownloadPruefsumme, "Pinsel.red", null)),
        })
        {
            _ = fenster.ZeigenAsync();
            await ansicht.BildFertigAsync();
            zustand?.Invoke(fenster);
            await ansicht.BildFertigAsync();
            yield return name;
            Dialog.AlleSchliessen();
            await ansicht.BildFertigAsync();
        }
        // A 0.61.0 (Sammelliste 284): Layoutpruefung (12) kein Nur-Lese-Feld ausser der festen Liste; Gegenprobe: ein
        // Fenster mit einem Nur-Lese-Feld (wie das Update-Fenster bis A 0.60.0) muss genau diesen Fund geben
        var probe = new Avalonia.Controls.TextBox { Text = "Neuerungen wie bis A 0.60.0", IsReadOnly = true, Width = 300 };
        Avalonia.Automation.AutomationProperties.SetName(probe, UpdateTexte.Neuerungen);
        _ = Dialog.Zeigen(UpdateTexte.Neuerungen, probe, Dialog.Ok);
        await ansicht.BildFertigAsync();
        var nurLesen = Layoutpruefung.Pruefe(ansicht).Count(f => f.StartsWith(Layoutpruefung.NurLesenKennung, StringComparison.Ordinal));
        Messwerte.Notiere($"  Nur-Lese-Felder Gegenprobe (Update-Fenster wie A 0.60.0): {nurLesen} Fund(e)");
        if (nurLesen != 1)
        {
            Messwerte.Notiere("  FEHLER: Gegenprobe Nur-Lese-Feld nicht gefunden (Layoutpruefung (12))");
            _w3Fehlt++;
        }
        Dialog.AlleSchliessen();
        await ansicht.BildFertigAsync();
        // fester langer Windows-Pfad statt des Datenordners: beide Laeufe und alle Systeme pruefen denselben Text
        const string ordner = @"C:\Users\Auszubildender\AppData\Roaming\Fachinformatiker-Lernplattform";
        _ = Dialog.Zeigen(UpdateTexte.FehlschlagTitel,
            UpdateDienst.FehlschlagAnzeige(UpdateMerker.Fehlschlag(ProbeNeu.Anzeige, ProbeInstalliert.Anzeige, "windows", ordner), ordner), Dialog.Ok);
        await ansicht.BildFertigAsync();
        yield return "update/nicht-abgeschlossen";
        Dialog.AlleSchliessen();
        await ansicht.BildFertigAsync();
    }
}
