using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W8b (Weisung P8-20, Teilplan P8-18 Abschnitt 8): Ansichten fuer die Layoutpruefung vor den neuen Tueren
/// (Bahnhof Talheim mit dem neuen Fussweg, Moebelhaus mit verschobenem Modell), am Stellwerk (Gehweg, Tuer und
/// Schild, nicht betretbar) und im Bahnhof innen (Wartehalle). Prueft: das Stellwerk hat ein Schild und ist kein Ort.
/// </summary>
public static partial class AutoMessung
{
    public const int P8W8bAnsichtenAnzahl = 4;

    private static async IAsyncEnumerable<string> P8W8bAnsichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite is null)
        {
            P7W1AnsichtFehler.Add("spiel/tuer: Spielseite nicht gebaut");
            yield break;
        }
        foreach (var ort in (string[])["talheim", "moebelhaus", "talheim_nord"])
        {
            var spiel = Frisch(seite);
            spiel.GeheZu(ort);
            spiel.SofortAnkommen();
            seite.Flaeche.KameraAufFigur();
            await ansicht.BildFertigAsync();
            P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/tuer/" + ort));
            yield return "spiel/tuer/" + ort;
        }
        {
            // W4b (Weisung P12-228): das Stellwerk als Haus ohne Innenraum gibt es in der alten Stadt (Fassung 0); in Nicos
            // Stadt 1 ist Talheim-Nord ein Spielort mit Tuer (eigene Ansicht "spiel/tuer/talheim_nord")
            var neueStadt = seite.Spiel!;
            var spiel = Fassung0(seite);
            var stellwerk = spiel.Szene.Zierhaeuser.FirstOrDefault(z => z.Name == "Stellwerk");
            if (stellwerk is null || spiel.Szene.Ort("stellwerk") is not null || !seite.HatSchild("Stellwerk"))
            {
                P7W1AnsichtFehler.Add("spiel/tuer/stellwerk: Stellwerk ohne Gehweg und Schild oder betretbar");
            }
            else
            {
                spiel.GeheZu(stellwerk.GehX, stellwerk.GehZ);
                spiel.SofortAnkommen();
                seite.Flaeche.KameraAufFigur();
                await ansicht.BildFertigAsync();
                P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/tuer/stellwerk"));
                yield return "spiel/tuer/stellwerk";
            }
            seite.SpielSetzen(neueStadt);
        }
        {
            var spiel = Frisch(seite);
            spiel.GeheZu("talheim");
            spiel.SofortAnkommen();
            if (!spiel.KannBetreten && spiel.KannWarten)
            {
                spiel.Warten();
            }
            if (!spiel.KannBetreten || !spiel.Betreten() || spiel.FlaecheId != "kunde:talheim")
            {
                P7W1AnsichtFehler.Add("spiel/innen/talheim: nicht betretbar (" + spiel.Zeile.Replace('\n', ' ') + ")");
                yield break;
            }
            // nur BildFertigAsync wie P7W7Ansichten (ohne GL zeichnet die 3D-Flaeche nie ein Bild)
            await ansicht.BildFertigAsync();
            P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/innen/talheim"));
            yield return "spiel/innen/talheim";
            spiel.Verlassen();
        }
    }
}
