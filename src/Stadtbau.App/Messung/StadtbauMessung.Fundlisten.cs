using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.49.0 (Weisung P12-231 Antwort 2c, Weisung P12-229, Sammelliste 328): in JEDER Ansicht (Draufsicht, 3D, Grundriss,
/// Editor) stehen rechts zwei Listen „Stadt – n rot“ und „Innenräume – m rot“. Probe: der Elektromarkt (Fund 216, 8 rote
/// Funde) wird geaendert, damit er das Senden sperrt; draussen steht die Liste der Innenraeume mit seinen roten Funden, und
/// ein Antippen oeffnet den Grundriss an der Stelle. Gegenprobe mit der alten Regel (bis S 0.48.0): draussen kein Fund des
/// Innenraums. Layout jeder Ansicht je Groesse hell und dunkel; Zahl gleich <see cref="Projekt.RoteFunde"/>.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task FundlistenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Fundlisten", "elektromarkt");
        a.OrtOeffnen("elektromarkt");
        await UeberschriftUnveraendertAsync(a, f);
        a.Projekt!.Aendern("elektromarkt", "Boden", g => Bearbeitung.Boden(g, "flur", "teppich"));
        await a.Geprueft;
        await a.InnenAlleGeprueft;
        a.ReiterZeigen(innen: false);
        await a.AussenGeprueft;
        await a.InnenAlleGeprueft;
        await BildAsync(a);

        var sperren = a.Projekt.RoteFunde().Count(r => r.Ort != Projekt.StadtOrt);
        Messwerte.Notiere("Fundlisten: Innenraeume " + a.InnenRot.ToString(Inv) + " rot, Speichern/Senden sperren " + sperren.ToString(Inv));
        if (a.InnenRot != sperren || sperren != 8)
        {
            Fund("Fundlisten: Innenraeume " + a.InnenRot + " rot, RoteFunde nennt " + sperren + " (erwartet 8, gleich)");
        }
        ListenPruefen(a, "Draufsicht", a.InnenRot);
        var knopf = FundKnoepfe(a.Fundlisten.Innen).FirstOrDefault();
        if (knopf is null)
        {
            Fund("Fundlisten Draufsicht: kein antippbarer Fund des Elektromarkts in „Innenräume“");
        }
        await AlleGroessenAsync(a, f, "Fundlisten Draufsicht");

        // Gegenprobe: alte Regel, draussen steht kein Fund eines Innenraums
        Hauptansicht.FundlistenAltFuerTests = true;
        try
        {
            a.NeuZeichnenFuerTests();
            await BildAsync(a);
            if (a.Fundlisten.Innen is { IsEffectivelyVisible: true } alt && FundKnoepfe(alt).Count > 0)
            {
                Fund("Fundlisten Gegenprobe: mit der alten Regel stehen draussen Funde der Innenraeume (Probe taugt nicht)");
            }
            if (a.PruefenDraussen?.Titel != (a.StadtRot > 0 ? Texte.PruefenRot(a.StadtRot) : Texte.PruefenGruen))
            {
                Fund("Fundlisten Gegenprobe: Kopf draussen nicht wie bis S 0.48.0: " + a.PruefenDraussen?.Titel);
            }
        }
        finally
        {
            Hauptansicht.FundlistenAltFuerTests = false;
            a.NeuZeichnenFuerTests();
        }
        await BildAsync(a);

        // 3D
        a.DreiZeigen();
        await BildAsync(a);
        ListenPruefen(a, "3D", a.InnenRot);
        await AlleGroessenAsync(a, f, "Fundlisten 3D");

        // Antippen von draussen: Grundriss des Elektromarkts an der Stelle
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        if (FundKnoepfe(a.Fundlisten.Innen).FirstOrDefault() is { } tipp)
        {
            a.OrtOeffnen("bank");
            a.ReiterZeigen(innen: false);
            await BildAsync(a);
            tipp = FundKnoepfe(a.Fundlisten.Innen).First();
            tipp.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await a.Geprueft;
            await BildAsync(a);
            if (a.OffenerOrt != "elektromarkt" || !a.ZeigtGrundriss)
            {
                Fund("Fundlisten: Antippen eines Innenraum-Funds oeffnet \"" + a.OffenerOrt + "\" statt den Grundriss des Elektromarkts");
            }
        }

        // Grundriss eines anderen Raums: „Stadt“ oben, „Innenräume“ mit dem Elektromarkt unter dem offenen Raum
        a.ReiterZeigen(innen: true);
        a.OrtOeffnen("bank");
        await a.Geprueft;
        await a.InnenAlleGeprueft;
        await BildAsync(a);
        ListenPruefen(a, "Grundriss", a.InnenRot);
        if (FundKnoepfe(a.InnenAndereListe).Count == 0)
        {
            Fund("Fundlisten Grundriss: im Raum „Bank“ fehlen die roten Funde des Elektromarkts");
        }
        await AlleGroessenAsync(a, f, "Fundlisten Grundriss");

        // Editor
        var e = a.EntwurfOeffnen(null);
        await BildAsync(a);
        if (e is null)
        {
            Fund("Fundlisten Editor: Editor laesst sich nicht oeffnen");
        }
        else
        {
            ListenPruefen(a, "Editor", a.InnenRot);
            await AlleGroessenAsync(a, f, "Fundlisten Editor");
            a.EntwurfSchliessen();
            await BildAsync(a);
        }
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }

    /// <summary>
    /// Patch S 0.49.1 (Sammelliste 332): Grundriss des unveraenderten Elektromarkts (8 rote Funde, Fund 216, sperren nicht):
    /// der Kopf „Innenräume“ nennt sie („8 rot (unverändert, sperren nicht)“) statt „alles in Ordnung“. Gegenprobe: alte Regel.
    /// </summary>
    private static async Task UeberschriftUnveraendertAsync(Hauptansicht a, Window? f)
    {
        a.ReiterZeigen(innen: true);
        await a.Geprueft;
        await a.InnenAlleGeprueft;
        await BildAsync(a);
        var innen = a.Fundlisten.Innen;
        var soll = Texte.FundeInnenTitel(0, 8);
        Messwerte.Notiere("Fundlisten 332: Grundriss Elektromarkt unveraendert, Kopf \"" + innen?.Titel + "\", rot unveraendert " + a.UnveraendertRot.ToString(Inv));
        if (a.InnenRot != 0 || a.UnveraendertRot != 8 || innen?.Titel != soll)
        {
            Fund("Fundlisten 332: Kopf \"" + innen?.Titel + "\" statt \"" + soll + "\" (rot " + a.InnenRot + ", unveraendert " + a.UnveraendertRot + ")");
        }
        await AlleGroessenAsync(a, f, "Fundlisten Grundriss unveraendert");
        Hauptansicht.UeberschriftAltFuerTests = true;
        try
        {
            a.NeuZeichnenFuerTests();
            await BildAsync(a);
            if (a.Fundlisten.Innen?.Titel != Texte.FundeInnenTitel(0))
            {
                Fund("Fundlisten 332 Gegenprobe: mit der alten Regel steht nicht \"" + Texte.FundeInnenTitel(0) + "\" (Probe taugt nicht): " + a.Fundlisten.Innen?.Titel);
            }
        }
        finally
        {
            Hauptansicht.UeberschriftAltFuerTests = false;
            a.NeuZeichnenFuerTests();
        }
        await BildAsync(a);
    }

    /// <summary>Beide Listen sichtbar, Titel mit der Zahl, Innenraum-Liste nennt den Elektromarkt.</summary>
    private static void ListenPruefen(Hauptansicht a, string wo, int innenRot)
    {
        var (stadt, innen) = a.Fundlisten;
        if (stadt is not { IsEffectivelyVisible: true } || innen is not { IsEffectivelyVisible: true })
        {
            Fund("Fundlisten " + wo + ": Liste fehlt (Stadt " + (stadt?.IsEffectivelyVisible == true) + ", Innenraeume " + (innen?.IsEffectivelyVisible == true) + ")");
            return;
        }
        if (stadt.Titel != Texte.FundeStadtTitel(a.StadtRot))
        {
            Fund("Fundlisten " + wo + ": Kopf \"" + stadt.Titel + "\" statt \"" + Texte.FundeStadtTitel(a.StadtRot) + "\"");
        }
        if (innen.Titel != Texte.FundeInnenTitel(innenRot))
        {
            Fund("Fundlisten " + wo + ": Kopf \"" + innen.Titel + "\" statt \"" + Texte.FundeInnenTitel(innenRot) + "\"");
        }
        var name = a.Projekt!.OrtName("elektromarkt");
        if (!innen.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.StartsWith(name, StringComparison.Ordinal) == true))
        {
            Fund("Fundlisten " + wo + ": „Innenräume“ nennt den Elektromarkt nicht");
        }
    }

    private static List<Button> FundKnoepfe(Control? wo) => wo is null ? []
        : [.. wo.GetLogicalDescendants().OfType<Button>().Where(b => b.Tag is StadtbauFund { X: not null, Y: not null } && b.IsEffectivelyVisible)];
}
