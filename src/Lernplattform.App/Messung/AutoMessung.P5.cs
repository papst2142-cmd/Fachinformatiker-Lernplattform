using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 5, W1 (Plan Phase 5, BE1 bis BE4): Ansichten von „Sicherung erstellen“, „Sicherung
/// einspielen“ (Vorschau, Rueckfragen, Rueckmeldungen, Fehler), „Löschen und zurücksetzen“ und
/// „Datenbank“. Nichts wird gespeichert, eingespielt oder geloescht: die Fenster werden nur gezeigt
/// und mit Dialog.AlleSchliessen geschlossen (ohne Auswertung der Antwort). Die Sicherung der
/// Vorschau entsteht nur im Speicher aus dem (leeren) Lernstand des Start-Tests.
/// </summary>
public static partial class AutoMessung
{
    private const string LangerGeraetename = "Testgeraet mit einem recht langen Namen";

    private static Sicherungsinhalt? _probeInhalt;

    private static Sicherungsinhalt ProbeInhalt() => _probeInhalt ??= Sicherungsinhalt.Lesen(
        Ruecksicherung.Erstellen(Zustand.Lernstand, Zustand.Werte, Aktualisierung.Aktualisierung.Anzeige, LangerGeraetename, new DateTime(2026, 10, 23, 12, 0, 0)));

    /// <summary>
    /// Fenster der W1 (Name, Zeigen); Eintraege mit grossen Zahlen und dem laengsten echten Dateinamen der
    /// Kopie (zweite Kopie in derselben Minute). Im schmalen Aufbau mit den Umbruchstellen des Handys (wie W2).
    /// </summary>
    private static (string Name, Func<Task<int>> Zeigen)[] P5Fenster(MainView ansicht) =>
    [
        ("sicherung/vorschau", () => EinspielenTeil.VorschauZeigen(ProbeInhalt())),
        ("sicherung/frage-ersetzen", () => EinspielenTeil.FrageZeigen(ProbeInhalt())),
        ("sicherung/rueckfrage-ersetzen", () => Dialog.Rueckfrage(SicherungTexte.RueckfrageTitel, SicherungTexte.Rueckfrage, 1, TrainerTexte.Ja, TrainerTexte.Nein)),
        ("sicherung/ersetzt", () => EinspielenTeil.MeldungMitDatei(SicherungTexte.ErsetztMeldung(ProbeInhalt(),
            UebernahmeTexte.KopieName(Wiederherstellung.Praefix + "2026-10-23_1200_2" + Ruecksicherung.Endung, !ansicht.Breit), false))),   // P5-4 A1: laengste Form mit dem Satz W13
        ("sicherung/zusammengefuehrt", () => EinspielenTeil.MeldungMitDatei(SicherungTexte.Zusammengefuehrt(ProbeBericht(),
            UebernahmeTexte.KopieName("vor_uebernahme_2026-10-23_120000_2.db", !ansicht.Breit)))),
        ("sicherung/kopie-gescheitert", () => Dialog.Zeigen(SicherungTexte.Titel,
            SicherungTexte.ErsetzenFehler(new WiederherstellungAusnahme(WiederherstellungFehler.KopieGescheitert, RuecksicherungFehler.KeineBerechtigung), null), Dialog.Ok)),
        ("sicherung/einspielen-gescheitert", () => Dialog.Zeigen(SicherungTexte.Titel, SicherungTexte.EinspielenGescheitert, Dialog.Ok)),
        ("loeschen/frage-historie", () => Dialog.Zeigen(LoeschenTexte.HistorieFrageTitel, LoeschenTexte.HistorieFrage, TrainerTexte.Ja, TrainerTexte.Nein)),
        ("loeschen/nichts-geloescht", () => Dialog.Zeigen(LoeschenTexte.LerndatenFrageTitel,
            LoeschenTexte.NichtsGeloeschtMit([Lernstand.NurLesenZeilen.Values.MaxBy(z => z.Length)!]), Dialog.Ok)),
        ("loeschen/erfolg-uhr", () => Dialog.Zeigen(LoeschenTexte.LerndatenErfolgTitel,
            LoeschenTexte.MitUhrHinweis(LoeschenTexte.LerndatenErfolg, -86400 * 400), Dialog.Ok)),
    ];

    /// <summary>Anzahl der W1-Ansichten (fuer den Bericht): 3 Bereiche der Optionen und die Fenster.</summary>
    public const int P5AnsichtenAnzahl = 3 + 10 + 1;

    /// <summary>
    /// Die neuen Ansichten der W1 nacheinander (je nach dem Zeigen pruefen): Optionen „Sicherung“,
    /// „Löschen und zurücksetzen“ und „Datenbank“ aufgeklappt, dann die Fenster und „Bitte warten“ beim Ersetzen.
    /// </summary>
    private static async IAsyncEnumerable<string> P5Ansichten(MainView ansicht)
    {
        if (ansicht.GebauteSeiten.TryGetValue(Seiten.Optionen, out var o) && o is OptionenSeite optionen)
        {
            await ansicht.ZeigeAsync(Seiten.Optionen);
            foreach (var (id, name) in new[] { (Optionen.Sicherung, "sicherung"), (Optionen.Loeschen, "loeschen"), (Optionen.Datenbank, "datenbank") })
            {
                optionen.OeffneBereich(id, false);
                await ansicht.BildFertigAsync();
                await ansicht.BildFertigAsync();
                yield return "optionen/" + name;
            }
        }
        foreach (var (name, zeigen) in P5Fenster(ansicht))
        {
            _ = zeigen();
            await ansicht.BildFertigAsync();
            yield return name;
            Dialog.AlleSchliessen();
        }
        using (Dialog.Warten(SicherungTexte.WartenErsetzen))
        {
            await ansicht.BildFertigAsync();
            yield return "sicherung/bitte-warten";
        }
        await ansicht.BildFertigAsync();
    }
}
