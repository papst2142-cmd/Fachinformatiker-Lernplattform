using Lernplattform.Core.Texte;
using Lernplattform.Core.Update;
using Lernplattform.Views;

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

    // laengste Notizen wie ein echtes Release (Abschnitt "### Herunterladen" faellt weg)
    private static UpdateAngebot ProbeAngebot(bool mitDatei) => new("A 0.11.1",
        "## A 0.11.1\n\n- **Neu:** Update im Programm mit Kopie des Lernstands vor jedem Update\n- Fix: Abgleich zwischen PC und Handy nach langer Pause\n- Fix: Sehr lange Themennamen im Prüfungstrainer brechen sauber um\n\nAlle Änderungen stehen in AENDERUNGEN.md.\n\n### Herunterladen\n- Datei sha256 …",
        GithubSuche.ReleaseSeite, mitDatei ? "Fachinformatiker-Lernplattform-1.0.0-a.0.11.1-full.nupkg" : null,
        mitDatei ? "Fachinformatiker-Lernplattform-1.0.0-a.0.11.1-full.nupkg" : null, 89_128_960);

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
        _ = Dialog.Zeigen(UpdateTexte.FehlschlagTitel, UpdateMerker.Fehlschlag("A 0.11.1", "A 0.11.0", "windows"), Dialog.Ok);
        await ansicht.BildFertigAsync();
        yield return "update/nicht-abgeschlossen";
        Dialog.AlleSchliessen();
        await ansicht.BildFertigAsync();
    }
}
