using Lernplattform.Core.Texte;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 5, W2 (Plan Phase 5, BE6 bis BE8): Bereich „Diagnose und Werkzeuge“ mit eingetragenem
/// Problembericht, die Fehlerfenster und der Kurzhinweis. Nichts wird gespeichert oder geschaltet:
/// die Fenster werden nur gezeigt und mit Dialog.AlleSchliessen geschlossen.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Anzahl der W2-Ansichten (fuer den Bericht): der Bereich, zwei Fenster, ein Kurzhinweis.</summary>
    public const int P5W2AnsichtenAnzahl = 1 + 2 + 1;

    /// <summary>Fehlender Diagnose-Bereich (zaehlt als Fund im Urteil).</summary>
    private static int _w2Fehlt;

    private static (string Name, Func<Task<int>> Zeigen)[] P5W2Fenster() =>
    [
        ("diagnose/ordner-fehler", () => Dialog.Zeigen(DiagnoseTexte.OrdnerOeffnen,
            Beschriftungen.Einsetzen(DiagnoseTexte.OrdnerFehler, DiagnoseW2Texte.GrundImProtokoll), Dialog.Ok)),
        ("diagnose/speichern-fehler", () => Dialog.Zeigen(RuecksicherungTexte.SpeichernTitel, ProjektTexte.DateiNichtGespeichert, Dialog.Ok)),
    ];

    private static async IAsyncEnumerable<string> P5W2Ansichten(MainView ansicht)
    {
        // erst zeigen (baut die Seite); fehlt der Bereich danach, ist das ein Fund statt eines stillen Uebergehens (Gegenpruefung W2 Fund 11)
        await ansicht.ZeigeAsync(Seiten.Optionen);
        var optionen = (OptionenSeite)ansicht.GebauteSeiten[Seiten.Optionen];
        optionen.OeffneBereich(Optionen.Diagnose, false);
        if (optionen.DiagnoseBereich is not { } diagnose)
        {
            Messwerte.Notiere("  FEHLER: Optionen ohne Bereich \"Diagnose und Werkzeuge\"");
            _w2Fehlt++;
        }
        else
        {
            await diagnose.Bericht;
        }
        await ansicht.BildFertigAsync();
        await ansicht.BildFertigAsync();
        yield return "optionen/diagnose";
        foreach (var (name, zeigen) in P5W2Fenster())
        {
            _ = zeigen();
            await ansicht.BildFertigAsync();
            yield return name;
            Dialog.AlleSchliessen();
        }
        Kurzhinweis.Zeigen(DiagnoseTexte.Kopiert);
        await ansicht.BildFertigAsync();
        yield return "diagnose/kurzhinweis-kopiert";
        Kurzhinweis.Schliessen();
        await ansicht.BildFertigAsync();
    }
}
