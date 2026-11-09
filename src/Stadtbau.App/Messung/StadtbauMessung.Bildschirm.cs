using Avalonia.Controls;
using Avalonia.Interactivity;
using Lernplattform.Core.Spiel.Ton;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.25.1 (Weisung P12-109, Sammelliste 252): Menue „Einstellungen“ > „Bildschirm für Vollbild“. Mit einem Bildschirm (Rechner
/// der CI) steht der Eintrag nicht im Menue; mit zwei nachgestellten Bildschirmen steht er dort, das Fenster zeigt drei
/// Eintraege (Layout je Groesse), die Wahl landet in einstellungen.json, ist beim naechsten Oeffnen markiert, und „Bildschirm
/// des Fensters“ nimmt sie wieder weg. Gegenprobe: ein Bildschirm = kein Eintrag.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task BildschirmWahlAsync(Hauptansicht a, Window? f)
    {
        if (a.BildschirmEintrag is not { } eintrag)
        {
            Fund("Bildschirm fuer Vollbild: Eintrag im Menue \"" + Texte.MenueEinstellungen + "\" fehlt");
            return;
        }
        var echte = WerkzeugVollbild.Liste().Count;
        Messwerte.Notiere("Bildschirm fuer Vollbild: " + echte.ToString(Inv) + " echte Bildschirme, Eintrag sichtbar " + WerkzeugVollbild.WahlZeigen);
        if (echte < 2 && WerkzeugVollbild.WahlZeigen)
        {
            Fund("Bildschirm fuer Vollbild: Eintrag steht mit nur einem Bildschirm im Menue");
        }
        Bildschirm[] zwei = [new(0, 0, 1920, 1080, true, "Starttest links"), new(1920, 0, 2560, 1440, false, "Starttest rechts")];
        WerkzeugVollbild.BildschirmeFuerTests = zwei;
        try
        {
            a.EinstellungenMenueFuerFotos();
            await BildAsync(a);
            if (!eintrag.IsEffectivelyVisible)
            {
                Fund("Bildschirm fuer Vollbild: Eintrag fehlt im Menue trotz zwei Bildschirmen");
            }
            if (Tipp.Von(eintrag) != Texte.KopfTipps[Texte.VollbildBildschirm])
            {
                Fund("Bildschirm fuer Vollbild: Tipp am Eintrag fehlt");
            }
            Layout(a, "Menue Einstellungen mit Bildschirm fuer Vollbild");
            eintrag.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!await BisAsync(a, () => a.BildschirmKnoepfe.Count == 3))
            {
                Fund("Bildschirm fuer Vollbild: Fenster zeigt " + a.BildschirmKnoepfe.Count.ToString(Inv) + " Eintraege statt 3");
                OffenWaehlen(Texte.Abbrechen);
                return;
            }
            await AlleGroessenAsync(a, f, "Bildschirm fuer Vollbild");
            a.BildschirmKnoepfe[2].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await BisAsync(a, () => a.BildschirmKnoepfe.Count == 0);
            if (WerkzeugEinstellungen.VollbildBildschirm(a.Ablage) is not { X: 1920, Breite: 2560, Name: "Starttest rechts" })
            {
                Fund("Bildschirm fuer Vollbild: Wahl \"Bildschirm 2\" nicht in einstellungen.json");
            }
            var zweites = a.BildschirmWaehlenAsync();
            await BisAsync(a, () => a.BildschirmKnoepfe.Count == 3);
            if (Avalonia.Automation.AutomationProperties.GetName(a.BildschirmKnoepfe[2]) is not { } n || !n.EndsWith(", gewählt", StringComparison.Ordinal))
            {
                Fund("Bildschirm fuer Vollbild: gewaehlter Bildschirm beim naechsten Oeffnen nicht markiert");
            }
            a.BildschirmKnoepfe[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await zweites;
            if (WerkzeugEinstellungen.VollbildBildschirm(a.Ablage) is not null)
            {
                Fund("Bildschirm fuer Vollbild: \"Bildschirm des Fensters\" nimmt die Wahl nicht weg");
            }
        }
        finally
        {
            WerkzeugVollbild.BildschirmeFuerTests = null;
            a.MenueZuFuerFotos();
        }
        // Gegenprobe: mit einem Bildschirm kein Eintrag
        WerkzeugVollbild.BildschirmeFuerTests = [new Bildschirm(0, 0, 1920, 1080, true, "einer")];
        a.EinstellungenMenueFuerFotos();
        await BildAsync(a);
        if (eintrag.IsEffectivelyVisible)
        {
            Fund("Bildschirm fuer Vollbild: Gegenprobe ein Bildschirm zeigt den Eintrag");
        }
        WerkzeugVollbild.BildschirmeFuerTests = null;
        a.MenueZuFuerFotos();
        await BildAsync(a);
        Messwerte.Notiere("Bildschirm fuer Vollbild: nur ab zwei Bildschirmen, Wahl gemerkt und markiert, zuruecksetzbar");
    }
}
