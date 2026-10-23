using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 5 BE1/BE3: Texte von „Sicherung erstellen“ und „Sicherung einspielen“ (Zusammenführen,
/// Alles ersetzen). Mit (Py) markierte Teile zeichengleich aus Python v0.63.2 (fisi_sicherung,
/// app_gui; Vergleichswerte G37). Mit (W n) markierte Sätze weichen ab und stehen in der
/// Wortliste W1 (Plan Phase 5 D4, Fi-Austausch anlagen/P5-2_Wortliste_W1.md).
/// PC und Handy gleich.
/// </summary>
public static class SicherungTexte
{
    public const string Hilfe = "Speichert den gesamten Lernstand in einer Datei: Lernfortschritt, Prüfungen, "
        + "Abschlussprojekt, Spielstände, Bestenliste und das Tagesziel. Mit „Sicherung "
        + "einspielen“ holst du ihn zurück, auch auf einem anderen Gerät. "
        + "Zugangsschlüssel und Farben kommen nicht mit in die Datei.";                       // (Py) HELP

    /// <summary>(W1) Frage 2: dieselbe Datei liest auch die alte App.</summary>
    public const string HilfeAlteApp = "Auch die alte Python-Version der Lernplattform (0.63.2) kann diese Datei mit „Sicherung einspielen“ einlesen.";

    public const string KnopfEinspielen = "Sicherung einspielen";                             // (Py) BTN_RESTORE
    public const string Zusammenfuehren = "Zusammenführen";                                   // (Py) BTN_MERGE
    public const string Ersetzen = "Alles ersetzen";                                          // (Py) BTN_REPLACE
    public const string Abbrechen = "Abbrechen";                                              // (Py) BTN_CANCEL
    public const string FilterAlle = "Alle Dateien";                                          // (Py) app_gui 8073

    public const string VorschauHinweis = "Zusammenführen übernimmt, was auf diesem Gerät noch fehlt – doppelt "
        + "wird nichts. "
        + "Gelöschtes kommt dabei in der Regel nicht zurück: Was auf diesem Gerät "
        + "gelöscht wurde (z. B. mit „Alle Lerndaten löschen“), bleibt gelöscht – "
        + "außer die Sicherung wurde auf einem Gerät erstellt, nachdem es nach dieser "
        + "Löschung mit „Alles ersetzen“ einen älteren Stand eingespielt hatte; dann "
        + "kommt dieser Stand zurück. "
        + "Alles ersetzen stellt genau den Stand der Sicherung her.";                        // (Py) PREVIEW_HINT

    public const string ErsetzenTitel = "Alles ersetzen";                                     // (Py) REPLACE_TITLE
    public const string RueckfrageTitel = "Wirklich ersetzen?";                               // (Py) REPLACE_CONFIRM_TITLE
    public const string Rueckfrage = "Letzte Rückfrage: Den Lernstand dieses Geräts jetzt durch die "
        + "Sicherung ersetzen?";                                                              // (Py) REPLACE_CONFIRM
    public const string Titel = "Sicherung einspielen";                                       // (Py) ERROR_TITLE

    public const string KeineSicherung = "Diese Datei ist keine Sicherung der Lernplattform.";  // (Py) MSG_NOT_BACKUP
    public const string NurLesen = "Die Sicherung lässt sich nicht einspielen. %s";             // (Py) MSG_READ_ONLY
    public const string WartenErsetzen = "Bitte warten – die Daten werden ersetzt.";       // (Py) WAIT_REPLACE_TEXT

    /// <summary>(W5) statt „…: &lt;Systemtext&gt;“: Grund wie bei „Sicherung erstellen“ (T4), dazu „nichts geändert“.</summary>
    public const string KopieGescheitert = "Die Sicherheitskopie ließ sich nicht speichern: %s. Es wurde nichts geändert.";

    /// <summary>(W6) MSG_RESTORE_FAILED ohne „Fehler: &lt;Systemtext&gt;“; alles wurde zurückgenommen.</summary>
    public const string EinspielenGescheitert = "Die Sicherung ließ sich nicht einspielen. Die Datenbank ist "
        + "gerade gesperrt (z. B. von einem anderen Programm), schreibgeschützt "
        + "oder die Platte ist voll. Es wurde nichts geändert.";

    // (Py) replace_question; (W2) einzige Aenderung: Ort der Sicherheitskopie (D3)
    private const string FrageAnfang = "Der komplette Lernstand dieses Geräts – Lernfortschritt, Prüfungen, "
        + "Abschlussprojekt, Spielstände und Bestenliste – wird durch die Sicherung "
        + "vom %s ersetzt. Das Tagesziel kommt ebenfalls aus der Sicherung. ";

    /// <summary>Der Satz von Python (Sicherheitskopie „neben der Datenbank“).</summary>
    public const string FrageKopiePython = "Vorher wird der jetzige Stand als Sicherheitskopie neben der Datenbank gespeichert.";

    /// <summary>(W2) Die Kopie liegt im Ordner „sicherungen“ (D3).</summary>
    public const string FrageKopie = "Vorher wird der jetzige Stand als Sicherheitskopie im Ordner „sicherungen“ gespeichert.";

    private const string FrageRest = "\n\n"
        + "Ist der Abgleich eingerichtet, kommen beim nächsten Abgleich die Daten aus "
        + "dem Repository wieder dazu. Löschungen, die dieses Gerät schon kennt (zum "
        + "Beispiel „Alle Lerndaten löschen“), löschen das Eingespielte nicht noch "
        + "einmal; das gilt auch für deine anderen Geräte, wenn alle deine Geräte schon "
        + "beim Ersetzen mindestens Version 0.63.2 hatten. Solange ein Gerät noch eine "
        + "ältere Version hat, kann das Eingespielte auf deinen anderen Geräten "
        + "zeitweise wieder fehlen, und ein Gerät mit einer älteren Version, das die "
        + "Löschung kennt, löscht es bei sich weiterhin; aktualisiere deine anderen "
        + "Geräte deshalb bald. Was ein anderes Gerät ohne Verbindung gelernt hat, "
        + "bleibt in der Regel erhalten, wenn es sich erst nach dem Ersetzen wieder "
        + "abgleicht und alle deine Geräte schon beim Ersetzen mindestens Version 0.63.2 "
        + "hatten. Löschungen eines anderen Geräts, die dieses Gerät noch nicht kennt, "
        + "gelten weiter – außer sie kamen vor der letzten Löschung derselben Art, die "
        + "dieses Gerät kennt (beide „Alle Lerndaten löschen“, beide „Historie löschen“ "
        + "oder beide „Bestenliste löschen“); solche werden aufgehoben. Bei Spielständen "
        + "und Projekten ist das anders: Ein zurückgesetzter oder gelöschter Spielstand "
        + "und ein gelöschtes Projekt bleiben gelöscht, wenn die Löschung von einem "
        + "anderen Gerät kam oder schon abgeglichen war. Bewahre die Sicherungsdatei "
        + "deshalb auf.";

    private const string Ersetzt = "Sicherung eingespielt: Der Lernstand entspricht jetzt der Sicherung vom %s. "
        + "Sicherheitskopie des vorherigen Stands: %s";                                       // (Py) replace_message

    private const string BleibtGeloeschtEins = "1 Eintrag der Sicherung bleibt gelöscht, weil er auf diesem Gerät "
        + "gelöscht wurde (z. B. mit „Alle Lerndaten löschen“). Zurück holt ihn "
        + "nur „Alles ersetzen“.";                                                            // (Py) kept_deleted_text
    private const string BleibtGeloeschtMehr = "%s der Sicherung bleiben gelöscht, weil sie auf diesem Gerät gelöscht "
        + "wurden (z. B. mit „Alle Lerndaten löschen“). Zurück holt sie nur „Alles "
        + "ersetzen“.";                                                                       // (Py) kept_deleted_text

    /// <summary>preview_text: Zeilen der Vorschau, Leerzeile, PREVIEW_HINT.</summary>
    public static string Vorschau(Sicherungsinhalt sicherung) => string.Join("\n", sicherung.Zeilen) + "\n\n" + VorschauHinweis;

    /// <summary>replace_question mit dem Ort der Kopie (W2).</summary>
    public static string ErsetzenFrage(Sicherungsinhalt sicherung) =>
        Beschriftungen.Einsetzen(FrageAnfang, sicherung.ErstelltText) + FrageKopie + FrageRest;

    /// <summary>replace_question genau wie Python (nur fuer den Vergleich mit G37).</summary>
    public static string ErsetzenFragePython(Sicherungsinhalt sicherung) =>
        Beschriftungen.Einsetzen(FrageAnfang, sicherung.ErstelltText) + FrageKopiePython + FrageRest;

    /// <summary>replace_message (Dateiname am Handy mit Umbruchstellen wie bei der Übernahme).</summary>
    public static string ErsetztMeldung(Sicherungsinhalt sicherung, string kopieName) =>
        Beschriftungen.Einsetzen(Ersetzt, sicherung.ErstelltText, kopieName);

    /// <summary>kept_deleted_text.</summary>
    public static string BleibtGeloescht(int anzahl) => anzahl == 1 ? BleibtGeloeschtEins
        : Beschriftungen.Einsetzen(BleibtGeloeschtMehr, UebernahmeTexte.Eintraege(anzahl));

    /// <summary>
    /// merge_message nach dem Zusammenführen einer Sicherung: Kern, kept_deleted_text, removed_text
    /// (Py), dann (W3) der Satz zur Kopie im Ordner „sicherungen“ und (W4) die Hinweise zu
    /// Löschmarken der Datei wie bei „Lernstand übernehmen“ (UebernahmeTexte.Hinweise).
    /// </summary>
    public static string Zusammengefuehrt(UebernahmeBericht bericht, string? kopieName)
    {
        var teile = new List<string>
        {
            UebernahmeTexte.EinleitungSicherung,
            bericht.Neu > 0 ? Beschriftungen.Einsetzen(UebernahmeTexte.KernUebernommen, UebernahmeTexte.Eintraege(bericht.Neu))
                : bericht.NichtUebernommen > 0 ? UebernahmeTexte.KernNichts : UebernahmeTexte.KernAllesDa,
        };
        if (bericht.NichtUebernommen > 0)
        {
            teile.Add(BleibtGeloescht(bericht.NichtUebernommen));
        }
        if (bericht.EntferntDurchLoeschmarke > 0)
        {
            teile.Add(Beschriftungen.Einsetzen(UebernahmeTexte.Entfernt, UebernahmeTexte.Eintraege(bericht.EntferntDurchLoeschmarke)));
        }
        if (kopieName is not null)
        {
            teile.Add(Beschriftungen.Einsetzen(UebernahmeTexte.KopieLiegt, kopieName));
        }
        teile.AddRange(UebernahmeTexte.Hinweise(UebernahmeQuelle.Sicherung, bericht.UngueltigInDatei, bericht.ZukunftsMarken));
        return string.Join(" ", teile);
    }

    /// <summary>Text zu einem Fehler beim Lesen der Datei (read_backup); (W7) Datei nicht lesbar ohne Systemtext.</summary>
    public static string LesenFehler(UebernahmeAusnahme fehler) => fehler.Fehler switch
    {
        UebernahmeFehler.KeineSicherung => KeineSicherung,
        UebernahmeFehler.ZuNeu => Beschriftungen.Einsetzen(UebernahmeTexte.ZuNeu, fehler.Version ?? "?"),
        UebernahmeFehler.PruefsummeFalsch => UebernahmeTexte.PruefsummeFalsch,
        UebernahmeFehler.InhaltKaputt => UebernahmeTexte.InhaltKaputt,
        _ => UebernahmeTexte.DateiNichtLesbar,
    };

    /// <summary>MSG_READ_ONLY mit der Zeile zum Grund (DB_READ_ONLY_LINES, Python strip()).</summary>
    public static string NurLesenText(string? grund) =>
        Beschriftungen.Einsetzen(NurLesen, grund is not null && Lernstand.NurLesenZeilen.TryGetValue(grund, out var zeile) ? zeile : "").Trim();

    /// <summary>Fehler beim Zusammenführen: nur lesen (Py), Kopie gescheitert (W10), Schreiben gescheitert (W6).</summary>
    public static string ZusammenfuehrenFehler(UebernahmeFehler fehler, string? nurLesenGrund) => fehler switch
    {
        UebernahmeFehler.NurLesen => NurLesenText(nurLesenGrund),
        UebernahmeFehler.SicherungGescheitert => UebernahmeTexte.SicherungGescheitert,
        _ => EinspielenGescheitert,
    };

    /// <summary>Fehler bei „Alles ersetzen“: nur lesen (Py), Kopie (W5), Einspielen (W6).</summary>
    public static string ErsetzenFehler(WiederherstellungAusnahme fehler, string? nurLesenGrund) => fehler.Fehler switch
    {
        WiederherstellungFehler.NurLesen => NurLesenText(nurLesenGrund),
        WiederherstellungFehler.KopieGescheitert => Beschriftungen.Einsetzen(KopieGescheitert, RuecksicherungTexte.Grund(fehler.Grund)),
        _ => EinspielenGescheitert,
    };
}
