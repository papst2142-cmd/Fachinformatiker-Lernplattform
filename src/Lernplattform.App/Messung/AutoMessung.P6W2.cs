using Lernplattform.Core.Texte;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 6, W2 (VE5): Optionen „Updates“ mit „Verknüpfung anlegen“ (Stand und langes Ergebnis) und die
/// Rueckfrage beim Start mit ihrem Ergebnis. Auf allen Systemen gezeigt (sonst nur Linux im AppImage);
/// nichts wird angelegt oder gespeichert: feste Probetexte, die Fenster schliesst Dialog.AlleSchliessen.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Anzahl der W2-Ansichten (fuer den Bericht): zwei Mal der Bereich, Rueckfrage, Ergebnis.</summary>
    public const int P6W2AnsichtenAnzahl = 2 + 1 + 1;

    // lange Pfade wie bei einem Benutzer mit langem Namen (beide Laeufe und alle Systeme pruefen denselben Text)
    private const string ProbeMenue = "/home/auszubildender-fachinformatiker/.local/share/applications/fachinformatiker-lernplattform.desktop";
    private const string ProbeDesktop = "/home/auszubildender-fachinformatiker/Schreibtisch/fachinformatiker-lernplattform.desktop";

    private static string ProbeErgebnis(string vorlage, bool desktop) => VerknuepfungDienst.Brechbar(
        vorlage.Replace("%s", ProbeMenue, StringComparison.Ordinal)
        + (desktop ? "\n" + vorlage.Replace("%s", ProbeDesktop, StringComparison.Ordinal) + VerknuepfungTexte.Unsicher
            : "\n" + VerknuepfungTexte.KeinDesktop));

    private static async IAsyncEnumerable<string> P6W2Ansichten(MainView ansicht)
    {
        await ansicht.ZeigeAsync(Seiten.Optionen);
        var optionen = (OptionenSeite)ansicht.GebauteSeiten[Seiten.Optionen];
        optionen.OeffneBereich(Optionen.Updates, false);
        var reihe = optionen.VerknuepfungReihe;
        var zeile = optionen.VerknuepfungZeile;
        if (reihe is null || zeile is null)
        {
            Messwerte.Notiere("  FEHLER: Optionen \"Updates\" ohne \"Verknüpfung anlegen\"");
            _w3Fehlt++;
        }
        else
        {
            reihe.IsVisible = true;
            zeile.Text = VerknuepfungTexte.Keine;
        }
        await ansicht.BildFertigAsync();
        yield return "optionen/verknuepfung";
        if (zeile is not null)
        {
            zeile.Text = ProbeErgebnis(VerknuepfungTexte.Angelegt, true);
        }
        await ansicht.BildFertigAsync();
        yield return "optionen/verknuepfung-ergebnis";
        if (reihe is not null && zeile is not null)
        {
            reihe.IsVisible = VerknuepfungDienst.Unterstuetzt;
            zeile.Text = VerknuepfungTexte.Keine;
        }
        _ = Dialog.Zeigen(VerknuepfungTexte.FrageTitel, VerknuepfungTexte.FrageText,
            VerknuepfungTexte.Ja, VerknuepfungTexte.Nein, VerknuepfungTexte.NieFragen);
        await ansicht.BildFertigAsync();
        yield return "verknuepfung/frage";
        Dialog.AlleSchliessen();
        await ansicht.BildFertigAsync();
        _ = Dialog.Zeigen(VerknuepfungTexte.FrageTitel, ProbeErgebnis(VerknuepfungTexte.Erneuert, false), Dialog.Ok);
        await ansicht.BildFertigAsync();
        yield return "verknuepfung/ergebnis";
        Dialog.AlleSchliessen();
        await ansicht.BildFertigAsync();
    }
}
