using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Input;
using Lernplattform.Core.Spiel;
using Lernplattform.Spiel.Tafel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.81.0 (Weisung P12-273, Nico 5a): „Zurück“ in der Spiel-Tafel bleibt beim Rollen fest oben. Je Tafel
/// (Kaufhaus-Sortiment, Möbelhaus, PC zu Hause, Firma): Knopf da, nicht im Bildlauf, im oberen Drittel auch ganz nach unten
/// gerollt (<see cref="SpielTafel.ZurueckFund"/>, dieselbe Pruefung wie in der Layoutpruefung jeder Schriftstufe und Groesse);
/// ganz unten gerollt „Zurück“ mit Maus, Finger und Screenreader (Aufruf ueber die Automation) schliesst die Seite wie bisher.
/// Gegenprobe: Kopfzeile wie bis A 0.80.0 im Bildlauf liefert den Fund.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> TafelZurueckAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Tafel Zurueck " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Tafel Zurueck " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Tafel is not { } tafel)
        {
            Messwerte.Notiere("  FEHLER: Spiel Tafel Zurueck: Spielseite nicht gebaut");
            return 1;
        }
        tafel.OhneFenster = true;
        try
        {
            try
            {
                tafel.ProbeFirmaAnlegen();
            }
            catch (Exception e) when (e is SpielFehler or SpielBelegtAusnahme or InvalidOperationException)
            {
                Messwerte.Notiere("Spiel: Tafel Zurueck: Firma schon da oder nicht angelegt (" + e.Message + ")");
            }
            tafel.ProbePcAnlegen();
            var kaufhaus = tafel.Sitzung.Geschaefte is { } g ? Kleidung.Stellen(g.Daten).FirstOrDefault()?.Id : null;
            var firma = Betrieb.Reiter(tafel.Sitzung.Stand).Select(r => r.Item1).FirstOrDefault();
            var tafeln = new (string Name, Func<bool> Oeffnen)[]
            {
                ("Kaufhaus-Sortiment", () => kaufhaus is not null && tafel.ProbeGeschaeft(Kleidung.Laden, kaufhaus)),
                ("Moebelhaus", () => tafel.ProbeGeschaeft("moebelhaus")),
                ("PC zu Hause", () => { tafel.PcOeffnen(SpielTafel.PcShop); return true; }),
                ("Firma", () => { if (firma is null) { return false; } tafel.ProbeFirma(firma); return true; }),
            };
            async Task<int> Oeffnen(Func<bool> oeffnen)
            {
                tafel.AllesSchliessen();
                var ok = oeffnen();
                seite.TafelZeigen();
                await ansicht.BildFertigAsync();
                return ok ? tafel.Tiefe : 0;
            }
            foreach (var (name, oeffnen) in tafeln)
            {
                foreach (var art in new[] { "Maus", "Finger", "Screenreader" })
                {
                    var tiefe = await Oeffnen(oeffnen);
                    if (tiefe == 0)
                    {
                        Pruefe(name + " " + art, false, "Tafel nicht offen");
                        continue;
                    }
                    var fund = tafel.ZurueckFund();
                    Pruefe($"{name} {art} fest oben", fund is null, fund ?? "ok");
                    var rolle = tafel.Rolle;
                    rolle.Offset = new Vector(0, Math.Max(0, rolle.Extent.Height - rolle.Viewport.Height));
                    await ansicht.BildFertigAsync();
                    var gerollt = rolle.Offset.Y;
                    switch (art)
                    {
                        case "Maus":
                            await TippenAsync(ansicht, tafel.ZurueckKnopf, PointerType.Mouse);
                            break;
                        case "Finger":
                            await TippenAsync(ansicht, tafel.ZurueckKnopf, PointerType.Touch);
                            break;
                        default:
                            (ControlAutomationPeer.CreatePeerForElement(tafel.ZurueckKnopf) as IInvokeProvider)?.Invoke();
                            break;
                    }
                    await ansicht.BildFertigAsync();
                    Pruefe($"{name} {art} Zurueck ganz unten", tafel.Tiefe < tiefe,
                        string.Create(inv, $"Seiten {tiefe} -> {tafel.Tiefe}, gerollt {gerollt:F0} von {rolle.Extent.Height:F0}"));
                }
            }
            // Gegenprobe: Kopfzeile wie bis A 0.80.0 im Bildlauf
            await Oeffnen(tafeln[0].Oeffnen);
            tafel.KopfImBildlaufFuerTests(true);
            try
            {
                await ansicht.BildFertigAsync();
                var gegen = tafel.ZurueckFund();
                Pruefe("Gegenprobe Kopf im Bildlauf", gegen is not null, gegen ?? "KEIN Fund (Pruefung zu schwach)");
            }
            finally
            {
                tafel.KopfImBildlaufFuerTests(false);
            }
        }
        finally
        {
            tafel.AllesSchliessen();
            seite.SpielSetzen(Frisch(seite));
            await ansicht.BildFertigAsync();
        }
        return funde;
    }
}
