using Lernplattform.Core.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W5 (Weisung P8-7 B1, Plan Phase 8 Abschnitt 11): Feierabend ueber das Bett. Messpunkt (wiederholbar,
/// Grenze 400 ms): "Spiel: Rueckfrage am Bett (bis zum Bild)", die Rueckfrage nach dem Spielstand des
/// Durchgangs aus W3 (<see cref="Wohnung.SchlafFrage"/>) bis zum Bild. Prueft: der Text passt zum Stand (ist der
/// Feierabend nicht moeglich, nennt er die offenen Aufgaben; ohne Firma fragt das Bett trotzdem, Weisung P8-8b).
/// Die Anordnung der drei Texte prueft die Layoutpruefung (Fenster "spiel/bett_feierabend", "spiel/bett_offen",
/// "spiel/bett_trotzdem").
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
        var (art, text) = Wohnung.SchlafFrage(stand);
        var moeglich = art == Wohnung.Schlafart.Fertig;
        Messwerte.Notiere("Rueckfrage am Bett: " + art + ", Text: " + text);
        if (moeglich != stand.FeierabendMoeglich() || (!moeglich && !text.Contains("offen", StringComparison.Ordinal))
            || (art == Wohnung.Schlafart.Hinweis) != (!moeglich && Py.Wahr(stand.Firma)))
        {
            Fehler("Rueckfrage am Bett: Text passt nicht zum Spielstand");
        }
        funde += Ergebnis("Spiel: Rueckfrage am Bett (bis zum Bild)",
            await BildMessenAsync(ansicht, () =>
            {
                var (frageArt, frage) = Wohnung.SchlafFrage(tafel.Sitzung.Verwaltung.Stand);
                _ = frageArt == Wohnung.Schlafart.Hinweis
                    ? Dialog.Zeigen(Wohnung.SchlafenTitel, frage, Dialog.Ok)
                    : Dialog.Zeigen(Wohnung.SchlafenTitel, frage, Wohnung.SchlafenNein, Wohnung.SchlafenJa);
            }, Dialog.AlleSchliessen, grenze: Geraetegrenze));
        Dialog.AlleSchliessen();
        await ansicht.BildFertigAsync();
        return funde;
    }
}
