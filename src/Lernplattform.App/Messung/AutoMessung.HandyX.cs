using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Lernplattform.Design;
using Lernplattform.Core.Texte;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.81.0 (Weisung P12-271, Sammelliste 348, Nico: „Handy in Game schließen über ein X im Handy-Menü oben rechts,
/// dass man nicht erst nach unten scrollen muss“). Je Reiter (Aufträge, Erledigt, Karte): X da, mit Namen, oben rechts in
/// der Karte und nicht im Bildlauf (auch ganz nach unten gerollt zu sehen); Schliessen mit Maus, Finger, Screenreader
/// (Aufruf über die Automation) und Escape; der Knopf „Schließen“ unten bleibt und schliesst weiter. Gegenprobe: ein
/// Fenster ohne X (gleicher Inhalt, <c>Dialog.Zeigen</c>) liefert den Fund „kein X“. Jede Schriftstufe und Groesse prueft
/// zusaetzlich die Layoutpruefung (Ansichten <c>spiel/questlog-offen</c>, <c>-erledigt</c>, <c>karte-bus</c>).
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> HandyKreuzAsync(MainView ansicht)
    {
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Handy X " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Handy X " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel Handy X: Spielseite nicht gebaut");
            return 1;
        }
        var vorher = seite.Spiel;
        try
        {
            Frisch(seite);
            await ansicht.BildFertigAsync();
            foreach (var reiter in new[] { SpielSeite.HandyReiter.Offen, SpielSeite.HandyReiter.Erledigt, SpielSeite.HandyReiter.Karte })
            {
                foreach (var art in new[] { "Maus", "Finger", "Screenreader", "Escape", "Knopf unten" })
                {
                    var offen = seite.HandyZeigenAsync(reiter);
                    await ansicht.BildFertigAsync();
                    var fenster = Dialog.Offen;
                    var fund = Dialog.KreuzFund(fenster, KarteTexte.HandySchliessen);
                    Pruefe($"{reiter} {art} oben rechts", fenster is not null && fund is null, fenster is null ? "Handy nicht offen" : fund ?? "ok");
                    var kreuz = Dialog.KreuzVon(fenster);
                    switch (art)
                    {
                        case "Maus":
                            if (kreuz is not null) { await TippenAsync(ansicht, kreuz, PointerType.Mouse); }
                            break;
                        case "Finger":
                            if (kreuz is not null) { await TippenAsync(ansicht, kreuz, PointerType.Touch); }
                            break;
                        case "Screenreader":
                            (kreuz is null ? null : ControlAutomationPeer.CreatePeerForElement(kreuz).GetProvider<IInvokeProvider>())?.Invoke();
                            break;
                        case "Escape":
                            fenster?.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = fenster });
                            break;
                        default:
                            var unten = fenster?.GetLogicalDescendants().OfType<Button>()
                                .LastOrDefault(b => b.Content is TextBlock { Text: QuestlogTexte.Schliessen });
                            unten?.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                            break;
                    }
                    await Task.WhenAny(offen, Task.Delay(3000));
                    await ansicht.BildFertigAsync();
                    var zu = Dialog.Offen is null && offen.IsCompleted && !seite.HandyOffen;
                    Pruefe($"{reiter} {art} schliesst", zu, zu ? "Handy zu" : "Handy OFFEN");
                    Dialog.AlleSchliessen();
                    await ansicht.BildFertigAsync();
                }
            }
            // Gegenprobe: gleicher Inhalt im Fenster ohne X -> die Pruefung findet „kein X“
            var ohne = Dialog.Zeigen(KarteTexte.HandyTitel, Schrift.Text(KarteTexte.Antippen), QuestlogTexte.Schliessen);
            await ansicht.BildFertigAsync();
            var gegen = Dialog.KreuzFund(Dialog.Offen, KarteTexte.HandySchliessen);
            Pruefe("Gegenprobe Fenster ohne X", gegen is not null, gegen ?? "KEIN Fund (Pruefung zu schwach)");
            Dialog.AlleSchliessen();
            await ohne;
        }
        finally
        {
            Dialog.AlleSchliessen();
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }
}
