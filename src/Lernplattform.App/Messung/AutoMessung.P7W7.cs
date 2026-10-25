using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 7, W7 (Weisung P7-12): Moebelhaus und Elektromarkt fuer die Layoutpruefung. In der Stadt
/// je Geschaeft hinein (vor dem Oeffnen warten), die Figur an den Verkauf und an eine Ware stellen
/// (Text unter der Ansicht mit "Kaufen"/"Ansehen"), danach das Sortiment in der Tafel: Moebelhaus,
/// Elektromarkt mit dem ersten Teile-Typ und dem Typ mit dem laengsten Namen. Gekauft wird nichts.
/// </summary>
public static partial class AutoMessung
{
    private static async IAsyncEnumerable<string> P7W7Ansichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Tafel is not { } tafel)
        {
            P7W1AnsichtFehler.Add("spiel/geschaefte: Spielseite nicht gebaut");
            yield break;
        }
        foreach (var laden in Geschaefte.Laeden)
        {
            var spiel = Frisch(seite);
            tafel.AllesSchliessen();
            spiel.GeheZu(laden);
            spiel.SofortAnkommen();
            if (!spiel.KannBetreten && spiel.KannWarten)
            {
                spiel.Warten();
            }
            if (!spiel.KannBetreten || !spiel.Betreten() || spiel.FlaecheId != "geschaeft:" + laden)
            {
                P7W1AnsichtFehler.Add("spiel/innen-" + laden + ": nicht betretbar (" + spiel.Zeile.Replace('\n', ' ') + ")");
                continue;
            }
            await seite.Flaeche.NaechstesBildAsync();
            await ansicht.BildFertigAsync();
            yield return "spiel/innen-" + laden;
            var innen = spiel.Innen!;
            if (innen.Personen.FirstOrDefault() is { } verkauf)
            {
                spiel.GeheZu(verkauf.X, verkauf.Z);
                spiel.SofortAnkommen();
                await ansicht.BildFertigAsync();
                yield return "spiel/innen-" + laden + "-verkauf";
            }
            // die Ware mit dem laengsten Namen (Text unter der Ansicht)
            var waren = Geschaefte.Auslage(new() { { "gebaeude", innen.Gebaeude } }).ToList();
            var ware = waren.OrderByDescending(d => Py.Str(d["ware"]).Length).FirstOrDefault();
            if (ware is null)
            {
                P7W1AnsichtFehler.Add("spiel/innen-" + laden + ": keine Auslage");
                continue;
            }
            spiel.GeheZu(Py.F(ware["x"]) + (Py.F(ware["w"]) / 2), Py.F(ware["y"]) + (Py.F(ware["h"]) / 2));
            spiel.SofortAnkommen();
            await ansicht.BildFertigAsync();
            yield return "spiel/innen-" + laden + "-ware";
            spiel.Verlassen();
        }
        if (!tafel.ProbeGeschaeft("moebelhaus"))
        {
            P7W1AnsichtFehler.Add("spiel/sortiment: Geschaefte nicht geladen");
            seite.SpielSetzen(Frisch(seite));
            yield break;
        }
        seite.TafelZeigen();
        await ansicht.BildFertigAsync();
        yield return "spiel/sortiment-moebelhaus";
        var typen = tafel.ProbeTeileTypen();
        var laengster = typen.OrderByDescending(t => Geschaefte.TypName(t, tafel.Sitzung.Stand.Inhalt).Length).FirstOrDefault();
        foreach (var typ in typen.Take(1).Concat(laengster is null ? [] : [laengster]).Distinct())
        {
            tafel.ProbeGeschaeft("elektromarkt", typ);
            await ansicht.BildFertigAsync();
            yield return "spiel/sortiment-elektromarkt-" + typ;
        }
        tafel.AllesSchliessen();
        seite.SpielSetzen(Frisch(seite));
        await ansicht.BildFertigAsync();
    }
}
