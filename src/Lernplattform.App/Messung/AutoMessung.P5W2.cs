using Lernplattform.Core.Diagnose;
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
    public const int P5W2AnsichtenAnzahl = 1 + 2 + 4 + 1;

    /// <summary>Fehlender Diagnose-Bereich (zaehlt als Fund im Urteil).</summary>
    private static int _w2Fehlt;

    private static bool _sendenGeprueft;

    private static (string Name, Func<Task<int>> Zeigen)[] P5W2Fenster() =>
    [
        ("diagnose/ordner-fehler", () => Dialog.Zeigen(DiagnoseTexte.OrdnerOeffnen,
            Beschriftungen.Einsetzen(DiagnoseTexte.OrdnerFehler, DiagnoseW2Texte.GrundImProtokoll), Dialog.Ok)),
        ("diagnose/speichern-fehler", () => Dialog.Zeigen(RuecksicherungTexte.SpeichernTitel, ProjektTexte.DateiNichtGespeichert, Dialog.Ok)),
        // Phase 11 (Weisung P11-29, Sammelliste 202): die Fenster von "Bericht senden"; gesendet wird hier nichts
        ("diagnose/senden-rueckfrage", () => Dialog.Zeigen(BerichtSendenTexte.RueckfrageTitel, BerichtSenden.Rueckfrage().Inhalt,
            BerichtSendenTexte.Senden, BerichtSendenTexte.Abbrechen)),
        ("diagnose/senden-gesendet", () => Zeige(new VersandErgebnis(VersandArt.Gesendet, "berichte/2026-06-15_101500_PC_A0.36.0.md", 201))),
        ("diagnose/senden-fehler", () => Zeige(new VersandErgebnis(VersandArt.Abgelehnt, null, 401))),
        ("diagnose/senden-nicht-verfuegbar", () => Zeige(new VersandErgebnis(VersandArt.NichtVerfuegbar, null, 0))),
    ];

    private static Task<int> Zeige(VersandErgebnis ergebnis)
    {
        var (titel, text) = BerichtVersand.Rueckmeldung(ergebnis);
        return Dialog.Zeigen(titel, text, Dialog.Ok);
    }

    /// <summary>
    /// Gegenprobe ohne Schluessel (der Start-Test baut nie mit Schluessel): der Knopf "Bericht senden" ist da und zeigt
    /// nur "nicht verfuegbar", es wird nichts gesendet.
    /// </summary>
    private static async Task P11SendenOhneSchluesselAsync(MainView ansicht, DiagnoseTeil diagnose)
    {
        // einmal je Lauf genuegt (die Fenster selbst prueft die Layoutpruefung in jeder Groesse und Stufe)
        if (_sendenGeprueft)
        {
            return;
        }
        _sendenGeprueft = true;
        if (diagnose.SendenKnopf is not { } knopf)
        {
            Messwerte.Notiere("  FEHLER: Problem melden ohne Knopf \"" + BerichtSendenTexte.Knopf + "\"");
            _w2Fehlt++;
            return;
        }
        if (BerichtVersand.Verfuegbar)
        {
            Messwerte.Notiere("  FEHLER: Start-Test mit eingebautem Schluessel gebaut (Bericht senden)");
            _w2Fehlt++;
            return;
        }
        knopf.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        await ansicht.BildFertigAsync();
        var text = Dialog.Offen is { } offen ? Vorlesen.Text(offen) : "";
        if (!text.Contains(BerichtSendenTexte.NichtVerfuegbar, StringComparison.Ordinal))
        {
            Messwerte.Notiere("  FEHLER: \"" + BerichtSendenTexte.Knopf + "\" ohne Schluessel zeigt nicht \"nicht verfuegbar\"");
            _w2Fehlt++;
        }
        else
        {
            Messwerte.Notiere("Bericht senden ohne Schluessel: Hinweis \"nicht verfuegbar\", nichts gesendet (gruen)");
        }
        Dialog.AlleSchliessen();
        await ansicht.BildFertigAsync();
    }

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
            await P11SendenOhneSchluesselAsync(ansicht, diagnose);
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
