using Lernplattform.Core.Spiel;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 7, W7b (Weisung P7-13): Bank und Amt fuer die Layoutpruefung. In der Stadt je Ort hinein
/// (vor dem Oeffnen warten), die Figur an den Schalter stellen (Text der Person mit Knopf), danach
/// die Seiten am Schalter in der Tafel (Bank: Kredite, Amt: Firma und Umsatzsteuer; mit der Firma
/// aus den W6-Ansichten). Geschrieben wird nichts.
/// </summary>
public static partial class AutoMessung
{
    private static async IAsyncEnumerable<string> P7W7bAnsichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Tafel is not { } tafel)
        {
            P7W1AnsichtFehler.Add("spiel/schalter: Spielseite nicht gebaut");
            yield break;
        }
        foreach (var ort in Schalter.Orte)
        {
            var spiel = Frisch(seite);
            tafel.AllesSchliessen();
            spiel.GeheZu(ort);
            spiel.SofortAnkommen();
            if (!spiel.KannBetreten && spiel.KannWarten)
            {
                spiel.Warten();
            }
            if (!spiel.KannBetreten || !spiel.Betreten() || spiel.FlaecheId != "schalter:" + ort)
            {
                P7W1AnsichtFehler.Add("spiel/innen-" + ort + ": nicht betretbar (" + spiel.Zeile.Replace('\n', ' ') + ")");
                continue;
            }
            // nur BildFertigAsync wie P7W7Ansichten (ohne GL zeichnet die 3D-Flaeche nie ein Bild)
            await ansicht.BildFertigAsync();
            P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/innen-" + ort));
            yield return "spiel/innen-" + ort;
            if (spiel.Innen!.Personen.FirstOrDefault() is { } person)
            {
                spiel.GeheZu(person.X, person.Z);
                spiel.SofortAnkommen();
                await ansicht.BildFertigAsync();
                P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/innen-" + ort + "-schalter"));
                yield return "spiel/innen-" + ort + "-schalter";
            }
            else
            {
                P7W1AnsichtFehler.Add("spiel/innen-" + ort + ": keine Person am Schalter");
            }
            spiel.Verlassen();
        }
        foreach (var ort in Schalter.Orte)
        {
            if (!tafel.ProbeSchalter(ort))
            {
                P7W1AnsichtFehler.Add("spiel/seite-" + ort + ": Seite am Schalter nicht offen");
                continue;
            }
            seite.TafelZeigen();
            await ansicht.BildFertigAsync();
            yield return "spiel/seite-" + ort;
        }
        tafel.AllesSchliessen();
        seite.SpielSetzen(Frisch(seite));
        await ansicht.BildFertigAsync();
    }
}
