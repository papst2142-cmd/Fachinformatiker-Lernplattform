using Avalonia.Controls;
using Avalonia.Interactivity;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.25.1 (Weisung P12-108 Punkt 2, Sammelliste 251): im Fenster „Projekt öffnen“ steht der Knopf „Stadt-Datei als neues
/// Projekt öffnen“ (mit Tipp, im Layout aller Groessen). Er legt aus einer Stadt-Datei ein neues Projekt an, zeigt es, und die
/// Datei dieses Projekts ergibt wieder dieselbe Stadt; das Projekt, aus dem die Datei stammt, bleibt bytegleich. Eine zweite
/// Datei mit gleichem Namen bekommt einen freien Namen (nichts ueberschrieben). Gegenprobe: eine kaputte Datei legt nichts an.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task StadtDateiAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Stadt-Datei", null, (24, 16));
        a.Projekt!.AussenAendern("Straße", s => AussenBearbeitung.Setzen(s, AussenArt.Strasse, 10, 8));
        var quelle = a.Projekt.Datei;
        var datei = a.Projekt.Stand("2026-06-15T10:00:00+02:00").Schreiben();
        var exportName = a.Projekt.ExportName(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.FromHours(2)));
        var quelleVorher = File.ReadAllBytes(quelle);
        var vorher = Projekt.Liste(a.Ablage).Count;

        var fenster = a.OeffnenAsync();
        await BildAsync(a);
        if (a.StadtDateiKnopf is not { } knopf || !knopf.IsEffectivelyVisible)
        {
            Fund("Stadt-Datei: Knopf \"" + Texte.StadtDateiOeffnen + "\" fehlt im Fenster \"" + Texte.OeffnenTitel + "\"");
            OffenWaehlen(Texte.Abbrechen);
            await fenster;
            return;
        }
        if (Tipp.Von(knopf) != Texte.StadtDateiOeffnenTipp)
        {
            Fund("Stadt-Datei: Tipp am Knopf fehlt");
        }
        await AlleGroessenAsync(a, f, "Projekt oeffnen mit Stadt-Datei");

        // Gegenprobe: kaputte Datei legt nichts an und meldet einen Grund
        Hauptansicht.StadtDateiWahlFuerTests = () => ("kaputt.fistadt", "kein JSON {"u8.ToArray());
        knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!await BisAsync(a, () => KnoepfeSind(Texte.Ok)))
        {
            Fund("Stadt-Datei: kaputte Datei ohne Hinweis (\"" + string.Join(" | ", OffeneKnoepfe()) + "\")");
        }
        OffenWaehlen(Texte.Ok);
        await fenster;
        if (Projekt.Liste(a.Ablage).Count != vorher)
        {
            Fund("Stadt-Datei: kaputte Datei hat ein Projekt angelegt");
        }

        // echte Datei, zweimal: zweites Mal freier Name, nie ueberschrieben
        Hauptansicht.StadtDateiWahlFuerTests = () => (exportName, datei);
        try
        {
            for (var mal = 1; mal <= 2; mal++)
            {
                fenster = a.OeffnenAsync();
                await BildAsync(a);
                a.StadtDateiKnopf?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await fenster;
                await BildAsync(a);
                var soll = mal == 1 ? "Starttest Stadt-Datei 2" : "Starttest Stadt-Datei 3";
                if (a.Projekt is not { } p || p.Name != soll || p.Leer != (24, 16))
                {
                    Fund("Stadt-Datei: offenes Projekt nach dem " + mal.ToString(Inv) + ". Oeffnen ist \"" + a.Projekt?.Name + "\" statt \"" + soll + "\"");
                    continue;
                }
                var wieder = Aussen.Landschaft(p.AussenStand).Count;
                if (wieder != 1)
                {
                    Fund("Stadt-Datei: neues Projekt hat " + wieder.ToString(Inv) + " Stuecke draussen statt 1");
                }
            }
        }
        finally
        {
            Hauptansicht.StadtDateiWahlFuerTests = null;
        }
        if (Projekt.Liste(a.Ablage).Count != vorher + 2 || !File.ReadAllBytes(quelle).AsSpan().SequenceEqual(quelleVorher))
        {
            Fund("Stadt-Datei: " + Projekt.Liste(a.Ablage).Count.ToString(Inv) + " Projekte statt " + (vorher + 2).ToString(Inv)
                + " oder das Quell-Projekt hat sich geaendert");
        }
        Messwerte.Notiere("Stadt-Datei als neues Projekt: Knopf mit Tipp, kaputte Datei mit Hinweis, zweimal geoeffnet mit freiem Namen, nichts ueberschrieben");
    }
}
