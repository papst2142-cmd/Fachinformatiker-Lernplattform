using Lernplattform.Core.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W5 (Weisung P8-7 B1, Plan Phase 8 Abschnitt 11): Feierabend ueber das Bett. Messpunkt (wiederholbar,
/// Grenze 400 ms): "Spiel: Rueckfrage am Bett (bis zum Bild)", die Rueckfrage nach dem Spielstand des
/// Durchgangs aus W3 (<see cref="Wohnung.SchlafFrage"/>) bis zum Bild. Prueft: der Text passt zum Stand (ist der
/// Feierabend nicht moeglich, nennt er die offenen Aufgaben). Die Anordnung beider Texte prueft die Layoutpruefung
/// (Fenster "spiel/bett_feierabend" und "spiel/bett_offen").
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> P8W5MesspunkteAsync(MainView ansicht)
    {
        var funde = 0;
        void Fehler(string text)
        {
            Messwerte.Notiere("  FEHLER: " + text);
            funde++;
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Tafel is not { } tafel)
        {
            Fehler("Rueckfrage am Bett: Spielansichten nicht gebaut");
            return funde;
        }
        var stand = tafel.Sitzung.Verwaltung.Stand;
        var (moeglich, text) = Wohnung.SchlafFrage(stand);
        Messwerte.Notiere("Rueckfrage am Bett: Feierabend " + (moeglich ? "moeglich" : "nicht moeglich") + ", Text: " + text);
        if (moeglich != stand.FeierabendMoeglich() || (!moeglich && !text.Contains("offen", StringComparison.Ordinal)))
        {
            Fehler("Rueckfrage am Bett: Text passt nicht zum Spielstand");
        }
        funde += Ergebnis("Spiel: Rueckfrage am Bett (bis zum Bild)",
            await BildMessenAsync(ansicht, () =>
            {
                var (ja, frage) = Wohnung.SchlafFrage(tafel.Sitzung.Verwaltung.Stand);
                _ = ja
                    ? Dialog.Zeigen(Wohnung.SchlafenTitel, frage, Wohnung.SchlafenNein, Wohnung.SchlafenJa)
                    : Dialog.Zeigen(Wohnung.SchlafenTitel, frage, Dialog.Ok);
            }, Dialog.AlleSchliessen, grenze: Geraetegrenze));
        Dialog.AlleSchliessen();
        await ansicht.BildFertigAsync();
        return funde;
    }
}
