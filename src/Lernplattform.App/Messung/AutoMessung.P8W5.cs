using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Design;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W5 (Weisung P8-7 B1, Plan Phase 8 Abschnitt 11): Feierabend ueber das Bett. Messpunkt (streng, ohne
/// Wiederholung, Grenze 400 ms): "Spiel: Rueckfrage am Bett (bis zum Bild)", die Rueckfrage nach dem Spielstand des
/// Durchgangs aus W3 (<see cref="Wohnung.SchlafFrage"/>) bis zum Bild. Prueft: der Text passt zum Stand (ist der
/// Feierabend nicht moeglich, nennt er die offenen Aufgaben; ohne Firma fragt das Bett trotzdem, Weisung P8-8b).
/// Die Anordnung der drei Texte prueft die Layoutpruefung (Fenster "spiel/bett_feierabend", "spiel/bett_offen",
/// "spiel/bett_trotzdem").
/// Kaufhaus (Weisung P8-8c): Messpunkt "Spiel: Kaufhaus Sortiment (bis zum Bild)" (streng, Grenze 400 ms), die Seite
/// "Kleidung" des Kaufhauses bis zum Bild; die Abteilungen zeigt die Layoutpruefung (P7W7Ansichten,
/// "spiel/sortiment-kaufhaus-&lt;Slot&gt;").
/// </summary>
public static partial class AutoMessung
{
    /// <summary>
    /// Sammelliste 112 (Weisung P8-7 B4): die Spielseite einmal vor der 3D-Stichprobe in allen Schriftstufen in der
    /// schmalen Groesse pruefen (Spiel zuerst schmal geoeffnet). Funde wie die Layoutpruefung ("&lt;Groesse&gt; &lt;Stufe&gt;
    /// spiel vor der Stichprobe: …"); danach Schrift und Fenster wie vorher.
    /// </summary>
    private static async Task<int> P8W5SpielVorStichprobeAsync(MainView ansicht)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var fenster = Avalonia.Controls.TopLevel.GetTopLevel(ansicht) as Avalonia.Controls.Window;
        var (breiteVorher, hoeheVorher) = fenster is null ? (0d, 0d) : (fenster.Width, fenster.Height);
        var funde = new List<string>();
        foreach (var stufe in Einstellungen.Schriftgroessen)
        {
            Schrift.Anwenden(Zustand.SchriftFaktor(stufe));
            if (fenster is not null)
            {
                var (hoechstB, hoechstH) = Hoechstgroesse(fenster);
                fenster.Width = Math.Min(SchmalBreite, hoechstB);
                fenster.Height = Math.Min(SchmalHoehe, hoechstH);
            }
            await ansicht.BildFertigAsync();
            await ansicht.BildFertigAsync();
            if (await SpielSeiteAsync(ansicht) is null)
            {
                funde.Add("spiel vor der Stichprobe: Spielseite nicht gebaut");
                break;
            }
            await ansicht.BildFertigAsync();
            var ist = string.Create(inv, $"schmal {ansicht.Bounds.Width:0}x{ansicht.Bounds.Height:0}");
            funde.AddRange(Layoutpruefung.Pruefe(ansicht).Select(f => $"{ist} {stufe} spiel vor der Stichprobe: {f}"));
        }
        Zustand.DesignAnwenden();
        if (fenster is not null)
        {
            fenster.Width = breiteVorher;
            fenster.Height = hoeheVorher;
        }
        await ansicht.BildFertigAsync();
        Messwerte.Notiere(string.Create(inv, $"Spielseite vor der 3D-Stichprobe (Sammelliste 112), {Einstellungen.Schriftgroessen.Count} Schriftstufen schmal: {funde.Count} Funde"));
        foreach (var fund in funde.Distinct().Take(20))
        {
            Messwerte.Notiere("  " + fund);
        }
        return funde.Count;
    }

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

        if (tafel.Sitzung.Geschaefte is null)
        {
            Fehler("Kaufhaus Sortiment: Geschaefte nicht geladen");
            return funde;
        }
        funde += Ergebnis("Spiel: Kaufhaus Sortiment (bis zum Bild)",
            await BildMessenAsync(ansicht, () =>
            {
                tafel.ProbeGeschaeft(Kleidung.Laden);
                seite.TafelZeigen();
            }, tafel.AllesSchliessen, grenze: Geraetegrenze));
        tafel.AllesSchliessen();
        await ansicht.BildFertigAsync();
        return funde;
    }
}
