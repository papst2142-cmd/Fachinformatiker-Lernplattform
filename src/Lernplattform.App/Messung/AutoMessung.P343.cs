using Avalonia.Controls;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Patch A 0.75.1 (Sammelliste 343, Weisung P12-258): ist die Ortsliste offen und die Figur betritt einen Laden (oder einen
/// anderen Innenraum), schliesst sie sich; nie eine Ortsliste ohne Knopf zum Schliessen. Schmal (Handy) und am PC, ueber die
/// echte Bedienung: Ortsliste ueber den Knopf „Orte“ oeffnen, zum Ort gehen, ankommen (drinnen), Bild abwarten. Danach
/// wieder draussen: der Knopf „Orte“ ist da und oeffnet die Liste. Gegenprobe: mit dem Verhalten bis A 0.75.0
/// (Schalter je Seite <c>OrtslisteAltFuerTests</c>) bleibt die Liste drinnen offen, ohne Knopf (wird erkannt).
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> OrtslisteInnenAsync(MainView ansicht)
    {
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Ortsliste innen " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Ortsliste innen " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel Ortsliste innen: Spielseite nicht gebaut");
            return 1;
        }
        if (!await GlAbwartenAsync(seite))
        {
            Messwerte.Notiere("Spiel: Ortsliste innen nicht geprueft (ohne 3D ist die Ortsliste immer offen, sie ist dort die Bedienung)");
            return 0;
        }
        var vorher = seite.Spiel;
        var fenster = TopLevel.GetTopLevel(ansicht) as Window;
        var (breiteVorher, hoeheVorher) = fenster is null ? (0d, 0d) : (fenster.Width, fenster.Height);
        try
        {
            // jeder Ort mit Innenraum (Laeden, Buero, Wohnung, ...): betretene Orte, Orte mit offener Liste oder Knopf drinnen,
            // Orte, nach denen die Liste draussen nicht ueber den Knopf aufging
            async Task<(List<string> Orte, List<string> Schlecht, List<string> NichtWieder)> Durchgang()
            {
                var (orte, schlecht, nichtWieder) = (new List<string>(), new List<string>(), new List<string>());
                var spiel = Frisch(seite);
                if (spiel.ImInnenraum)
                {
                    spiel.Verlassen();
                }
                await ansicht.BildFertigAsync();
                foreach (var ort in spiel.Szene.Orte.Select(o => o.Id))
                {
                    seite.OrtslisteZeigen(true);
                    await ansicht.BildFertigAsync();
                    if (spiel.GeheZu(ort) != Core.Spiel.StadtGrund.Ok)
                    {
                        continue;
                    }
                    spiel.SofortAnkommen();
                    await ansicht.BildFertigAsync();
                    if (!spiel.ImInnenraum && spiel.KannBetreten)
                    {
                        spiel.Betreten();
                    }
                    await ansicht.BildFertigAsync();
                    await ansicht.BildFertigAsync();
                    if (!spiel.ImInnenraum)
                    {
                        continue;
                    }
                    orte.Add(ort);
                    if (seite.OrtslisteSichtbar || seite.OrteKnopfSichtbar)
                    {
                        schlecht.Add(ort);
                    }
                    spiel.Verlassen();
                    await ansicht.BildFertigAsync();
                    var knopf = seite.OrteKnopfSichtbar;
                    seite.OrtslisteZeigen(!seite.OrtslisteSichtbar);
                    await ansicht.BildFertigAsync();
                    if (!knopf || !seite.OrtslisteSichtbar)
                    {
                        nichtWieder.Add(ort);
                    }
                }
                seite.OrtslisteZeigen(false);
                return (orte, schlecht, nichtWieder);
            }
            foreach (var (groesse, breite, hoehe) in fenster is null ? [("wie gestartet", 0d, 0d)] : new[] { ("schmal", SchmalBreite, SchmalHoehe), ("pc", PcBreite, PcHoehe) })
            {
                if (fenster is not null)
                {
                    var (hoechstB, hoechstH) = Hoechstgroesse(fenster);
                    fenster.Width = Math.Min(breite, hoechstB);
                    fenster.Height = Math.Min(hoehe, hoechstH);
                    await ansicht.BildFertigAsync();
                }
                var (orte, schlecht, nichtWieder) = await Durchgang();
                Pruefe(groesse + " schliesst beim Betreten", orte.Count >= 3 && schlecht.Count == 0,
                    orte.Count + " Orte betreten (" + string.Join(", ", orte) + "), offen geblieben: " + (schlecht.Count == 0 ? "keiner" : string.Join(", ", schlecht)));
                Pruefe(groesse + " draussen wieder zu oeffnen", nichtWieder.Count == 0, nichtWieder.Count == 0 ? "Knopf Orte da, Liste geht auf" : "Knopf fehlt oder Liste geht nicht auf: " + string.Join(", ", nichtWieder));
                seite.OrtslisteAltFuerTests = true;
                try
                {
                    var alt = await Durchgang();
                    Pruefe(groesse + " Gegenprobe bis A 0.75.0", alt.Orte.Count > 0 && alt.Schlecht.Count == alt.Orte.Count,
                        alt.Schlecht.Count + " von " + alt.Orte.Count + " Orten mit offener Liste ohne Knopf" + (alt.Schlecht.Count == alt.Orte.Count && alt.Orte.Count > 0 ? " (erkannt)" : " (NICHT erkannt)"));
                }
                finally
                {
                    seite.OrtslisteAltFuerTests = false;
                }
            }
        }
        finally
        {
            if (fenster is not null)
            {
                fenster.Width = breiteVorher;
                fenster.Height = hoeheVorher;
            }
            seite.OrtslisteZeigen(false);
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }
}
