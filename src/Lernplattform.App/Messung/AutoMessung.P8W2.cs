using Avalonia.Controls;
using Lernplattform.Core.Texte;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W2 (Weisung P8-3, Sammelliste 74): Knopf "Lizenzen" in den Optionen "Über das Programm".
/// Messpunkte: erstes Zeigen der Liste (einmalig, streng) und Aufklappen des laengsten Bestandteils
/// (Velopack, fast 1 MB Text; wiederholbar, Grenze 400 ms). Fuer die Layoutpruefung die Liste zu und
/// mit dem ersten Bestandteil offen. Liest nur, speichert nichts.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Anzahl der Ansichten (fuer den Bericht): Liste zu, erster Bestandteil offen.</summary>
    public const int P8W2AnsichtenAnzahl = 2;

    private static async Task<OptionenSeite?> LizenzenSeiteAsync(MainView ansicht)
    {
        await ansicht.ZeigeAsync(Seiten.Optionen);
        var optionen = (OptionenSeite)ansicht.GebauteSeiten[Seiten.Optionen];
        optionen.OeffneBereich(Optionen.Ueber, false);
        await ansicht.BildFertigAsync();
        if (optionen.LizenzenOeffnen is null || optionen.LizenzenTeil is null)
        {
            Messwerte.Notiere("  FEHLER: Optionen \"Über das Programm\" ohne Knopf \"Lizenzen\"");
            return null;
        }
        return optionen;
    }

    private static IEnumerable<Expander> Klappen(OptionenSeite optionen) => optionen.LizenzenTeil!.Children.OfType<Expander>();

    private static async Task<int> LizenzenMesspunkteAsync(MainView ansicht)
    {
        if (await LizenzenSeiteAsync(ansicht) is not { } optionen)
        {
            return 1;
        }
        var funde = Ergebnis("Optionen: Lizenzen zeigen (erstes Bauen, bis zum Bild)",
            await BildMessenAsync(ansicht, () => optionen.LizenzenOeffnen!()));
        var klappen = Klappen(optionen).ToList();
        var teile = Lizenztexte.Zerlegen(EingebetteterText("THIRD_PARTY_LICENSES.txt")).Teile;
        if (klappen.Count == 0 || klappen.Count != teile.Count)
        {
            Messwerte.Notiere($"  FEHLER: Lizenzen zeigen {klappen.Count} Bestandteile statt {teile.Count}");
            funde++;
        }
        else
        {
            // der laengste Text (Velopack-Programme, fast 1 MB)
            var laengste = klappen[teile.Select((t, i) => (Laenge: t.Bloecke.Sum(b => b.Length), i)).MaxBy(x => x.Laenge).i];
            funde += Ergebnis("Optionen: Lizenzen längsten Text aufklappen (bis zum Bild)",
                await BildMessenAsync(ansicht, () => laengste.IsExpanded = true, () => laengste.IsExpanded = false, grenze: Geraetegrenze));
            laengste.IsExpanded = false;
        }
        optionen.LizenzenTeil!.IsVisible = false;
        await ansicht.BildFertigAsync();
        return funde;
    }

    private static async IAsyncEnumerable<string> P8W2Ansichten(MainView ansicht)
    {
        if (await LizenzenSeiteAsync(ansicht) is not { } optionen)
        {
            _w3Fehlt++;
            yield break;
        }
        optionen.LizenzenOeffnen!();
        await ansicht.BildFertigAsync();
        yield return "optionen/lizenzen";
        if (Klappen(optionen).FirstOrDefault() is { } erste)
        {
            erste.IsExpanded = true;
            await ansicht.BildFertigAsync();
            yield return "optionen/lizenzen-offen";
            erste.IsExpanded = false;
        }
        optionen.LizenzenTeil!.IsVisible = false;
        await ansicht.BildFertigAsync();
    }

    private static string EingebetteterText(string datei)
    {
        using var strom = typeof(OptionenSeite).Assembly.GetManifestResourceStream("texte/" + datei);
        return strom is null ? "" : new StreamReader(strom).ReadToEnd();
    }
}
