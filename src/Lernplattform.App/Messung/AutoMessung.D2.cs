using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Programm;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle D2 (Weisung P12-157 Frage 5 = a): Leistungsanzeige im Spiel. Vorgabe aus (keine Zeile), mit dem Schalter in den
/// Optionen eine Zeile oben links mit Bildern/s, Dreiecken und Aufrufen des letzten Bilds; Layout je Groesse.
/// </summary>
public static partial class AutoMessung
{
    private static async IAsyncEnumerable<string> LeistungsanzeigeAnsichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            P7W1AnsichtFehler.Add("spiel/leistungsanzeige: Spielseite nicht gebaut");
            yield break;
        }
        var war = Leistungsanzeige.Lesen(Zustand.Werte);
        if (war)
        {
            P7W1AnsichtFehler.Add("spiel/leistungsanzeige: Vorgabe ist nicht aus");
        }
        await ansicht.BildFertigAsync();
        await LeistungZeileAbwartenAsync(seite, false);
        if (seite.LeistungZeile.IsEffectivelyVisible)
        {
            P7W1AnsichtFehler.Add("spiel/leistungsanzeige: Zeile sichtbar, obwohl der Schalter aus ist");
        }
        Zustand.Aendere(w => Leistungsanzeige.Wert(w, true));
        try
        {
            await LeistungZeileAbwartenAsync(seite, true);
            await ansicht.BildFertigAsync();
            var text = seite.LeistungZeile.Text ?? "";
            if (!seite.Flaeche.GlBereit)
            {
                Messwerte.Notiere("Spiel: Leistungsanzeige ohne OpenGL nicht gezeigt (wie vorgesehen)");
            }
            else if (!seite.LeistungZeile.IsEffectivelyVisible || !text.Contains("Bilder/s", StringComparison.Ordinal)
                || !text.Contains("Dreiecke", StringComparison.Ordinal) || !text.Contains("Aufrufe", StringComparison.Ordinal))
            {
                P7W1AnsichtFehler.Add("spiel/leistungsanzeige: Zeile fehlt oder unvollstaendig: \"" + text + "\"");
            }
            else
            {
                Messwerte.Notiere("Spiel: Leistungsanzeige: " + text);
            }
            yield return "spiel/leistungsanzeige";
        }
        finally
        {
            Zustand.Aendere(w => Leistungsanzeige.Wert(w, war));
        }
        await LeistungZeileAbwartenAsync(seite, false);
    }

    /// <summary>Die Zeile setzt der Takt der Seite (alle 500 ms); bis zu 3 s auf den erwarteten Zustand warten.</summary>
    private static async Task LeistungZeileAbwartenAsync(SpielSeite seite, bool an)
    {
        for (var i = 0; i < 60 && seite.LeistungZeile.IsVisible != (an && seite.Flaeche.GlBereit); i++)
        {
            await Task.Delay(50);
        }
    }
}
