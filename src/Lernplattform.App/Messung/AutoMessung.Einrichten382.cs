using Avalonia.Controls;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Sammelliste 382 (Weisung P12-298, Nico): im Einrichtungsmenue steht nach dem Aufstellen eines Moebels „Übernehmen“. Mit
/// Maus, Finger und Screenreader (Aufruf ueber die Automation): die Auswahl ist danach aufgehoben, das Menue bleibt offen
/// (Knopf „Fertig“), kein neues Ereignis, der Platz bleibt; erst „Fertig“ verlaesst das Einrichten. Gegenprobe wie bis
/// A 0.89.0: kein Knopf „Übernehmen“.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> Einrichten382Async(MainView ansicht)
    {
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Einrichten 382 " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Einrichten 382 " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel Einrichten 382: Spielseite nicht gebaut");
            return 1;
        }
        try
        {
            foreach (var art in new[] { "Maus", "Finger", "Screenreader", "Gegenprobe" })
            {
                var spiel = Frisch(seite);
                await seite.Flaeche.NaechstesBildAsync();
                if (!spiel.KannBetreten || !spiel.Betreten())
                {
                    Pruefe(art + " Wohnung", false, spiel.Zeile.Replace('\n', ' '));
                    continue;
                }
                spiel.EinrichtenUmschalten();
                spiel.GeheZu(1.0, 1.0);
                spiel.InDenKarton();
                spiel.GeheZu(5.0, 3.0);
                if (spiel.Vorschau is not { } v)
                {
                    Pruefe(art + " Vorschau", false, spiel.Zeile.Replace('\n', ' '));
                    continue;
                }
                spiel.GeheZu(Py.F(v["x"]) + 0.1, Py.F(v["y"]) + 0.1);
                await ansicht.BildFertigAsync();
                var ereignisse = spiel.Ablauf.Stand.Verlauf.Count;
                var platz = Wohnung.AufgestellteMoebel(spiel.Ablauf.Stand).SingleOrDefault(m => m.Stueck == "start-matratze");
                if (art == "Gegenprobe")
                {
                    StadtSpiel.UebernehmenAltFuerTests.Value = true;
                    try
                    {
                        // Knoepfe wie bis A 0.89.0: ohne „Übernehmen“ bleibt die Auswahl
                        spiel.Waehlen("start-matratze");
                        await ansicht.BildFertigAsync();
                        var da = seite.EinrichtenKnoepfe.ContainsKey(SpielProbeTexte.Uebernehmen) && spiel.Uebernehmen();
                        Pruefe("Gegenprobe ohne Übernehmen", !da && spiel.Ausgewaehlt is not null,
                            da ? "KEIN Fund (Pruefung zu schwach)" : "Auswahl bleibt: " + spiel.Ausgewaehlt);
                    }
                    finally
                    {
                        StadtSpiel.UebernehmenAltFuerTests.Value = false;
                    }
                    spiel.EinrichtenUmschalten();
                    continue;
                }
                if (!seite.EinrichtenKnoepfe.TryGetValue(SpielProbeTexte.Uebernehmen, out var knopf))
                {
                    Pruefe(art + " Knopf da", false, "kein „Übernehmen“ nach dem Aufstellen (gewaehlt " + spiel.Ausgewaehlt + ")");
                    spiel.EinrichtenUmschalten();
                    continue;
                }
                var getippt = art switch
                {
                    "Maus" => await TippenAsync(ansicht, knopf, PointerType.Mouse),
                    "Finger" => await TippenAsync(ansicht, knopf, PointerType.Touch),
                    _ => (ControlAutomationPeer.CreatePeerForElement(knopf) as IInvokeProvider) is { } p && Aufrufen(p),
                };
                await ansicht.BildFertigAsync();
                var ok = getippt && spiel.Ausgewaehlt is null && spiel.Einrichten && (seite.EinrichtenKnopf.Content is TextBlock et ? et.Text : seite.EinrichtenKnopf.Content?.ToString()) == SpielProbeTexte.Fertig
                    && spiel.Ablauf.Stand.Verlauf.Count == ereignisse
                    && Wohnung.AufgestellteMoebel(spiel.Ablauf.Stand).SingleOrDefault(m => m.Stueck == "start-matratze") == platz
                    && !seite.EinrichtenKnoepfe.ContainsKey(SpielProbeTexte.Uebernehmen);
                Pruefe(art + " Übernehmen", ok, ok ? "Auswahl aufgehoben, Menue offen, Platz gleich, kein Ereignis"
                    : $"getippt {getippt}, gewaehlt {spiel.Ausgewaehlt}, Einrichten {spiel.Einrichten}, Ereignisse {ereignisse} -> {spiel.Ablauf.Stand.Verlauf.Count}");
                spiel.EinrichtenUmschalten();
                await ansicht.BildFertigAsync();
                Pruefe(art + " Fertig verlaesst", !spiel.Einrichten, spiel.Einrichten ? "noch im Einrichten" : "verlassen");
            }
        }
        finally
        {
            seite.SpielSetzen(Frisch(seite));
            await ansicht.BildFertigAsync();
        }
        return funde;

        static bool Aufrufen(IInvokeProvider p)
        {
            p.Invoke();
            return true;
        }
    }
}
