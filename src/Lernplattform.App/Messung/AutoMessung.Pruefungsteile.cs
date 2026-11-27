using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Lernplattform.Core.Texte;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 1.15.0 (Weisungen P12-406/P12-407, Variante B): die neuen Seiten der Pruefungsteile.
/// <list type="bullet">
/// <item>Klickwege wie mit Maus oder Finger: jede bisherige Seite in hoechstens zwei Klicks (A 1.17.0: am Handy die Seiten unter
/// AP2 in drei, Prüfung → AP2 → Seite), am Handy ueber die untere Leiste,
/// am PC ueber die Seitenleiste (Knoepfe nach ihrem Namen fuer den Bildschirmleser gesucht, dadurch prueft der Weg auch die Namen).</item>
/// <item>Ansichten fuer die Layoutpruefung: Reiter Ueben und Pruefen je Teil, Satz waehlen, Aufgabe waehlen, Einzelaufgabe mit
/// und ohne Musterloesung, Teil 2 und die Seite „Mehr“.</item>
/// </list>
/// Speichert nichts (Einzelaufgaben nur lesen; Probeklausur und echtes Format werden nicht gestartet).
/// </summary>
public static partial class AutoMessung
{
    private static Button? KnopfNamens(Control wurzel, string name) =>
        wurzel.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => AutomationProperties.GetName(b) == name && b.IsEffectivelyVisible);

    /// <summary>
    /// Klickweg je bisheriger Seite: Eintrag der Leiste, dann Knoepfe der gezeigten Seiten (nach Namen). A 1.17.0 (Weisung
    /// P12-430, Reiter B): am Handy fuehrt „Prüfung“ zu AP1 (zuerst), AP2 ist ein Klick auf die Segment-Leiste; die Wege ueber
    /// AP2 haben darum drei Klicks. Seit Weisung P12-432 (Nico 2b) stehen Testprojekte und Mein Projekt auch unter „Mehr“ (zwei
    /// Klicks, Wege aus <see cref="Pruefungsteile.MehrEintraege"/>); jede bisherige Seite hat so einen Weg mit hoechstens zwei.
    /// </summary>
    private static IEnumerable<(string Leiste, string[] Knoepfe, string Ziel, string? Reiter)> Klickwege(bool handy)
    {
        var ueben = Pruefungsteile.Ueben + " ";
        var fr = Core.Pruefung.Fachrichtung.Nach(Programm.Zustand.Sicht);
        var ap2 = Pruefungsteile.Teil2Titel + " " + Beschriftungen.Einsetzen(Pruefungsteile.SegmentGewicht,
            Core.Pruefung.Fachrichtung.Teil2.Sum(r => fr.Bereich(r).Gewicht));
        var leiste1 = handy ? Pruefungsteile.PruefungTitel : Pruefungsteile.Teil1Titel;
        var leiste2 = handy ? Pruefungsteile.PruefungTitel : Pruefungsteile.Teil2Titel;
        string[] Zu2(string knopf) => handy ? [ap2, knopf] : [knopf];
        yield return (leiste1, [ueben + Pruefungsteile.Pruefungsfragen], Seiten.Trainer, null);
        yield return (leiste1, [ueben + Pruefungsteile.Ap1Szenarien], Seiten.Ap1Szenarien, null);
        yield return (leiste1, [Pruefungsteile.Pruefen], Pruefungsteile.Teil1, Pruefungsteile.Pruefen);
        yield return (leiste2, Zu2(Pruefungsteile.Testprojekte), Seiten.Testprojekt, null);
        yield return (leiste2, Zu2(Pruefungsteile.MeinProjekt), Seiten.Abschluss, null);
        foreach (var rolle in (string[])[Pruefungsteile.B1, Pruefungsteile.B2, Pruefungsteile.Wiso])
        {
            yield return (leiste2, Zu2(ueben + Pruefungsteile.BereichKurz(rolle)), rolle, Pruefungsteile.Ueben);
            yield return (leiste2, Zu2(Pruefungsteile.Pruefen + " " + Pruefungsteile.BereichKurz(rolle)), rolle, Pruefungsteile.Pruefen);
        }
        if (handy)
        {
            yield return (leiste1, [ap2], Pruefungsteile.Teil2, null);
            foreach (var e in Navigation.Lernen)
            {
                yield return (Pruefungsteile.LernenTitel, [e.Titel], e.Id, null);
            }
            foreach (var e in Pruefungsteile.MehrEintraege)
            {
                yield return (Pruefungsteile.MehrTitel, [e.Titel], e.Id, null);
            }
            yield return ("Start", [Pruefungsteile.StartOeffnen + " " + Pruefungsteile.Teil1Titel], Pruefungsteile.Teil1, null);
            yield return ("Start", [Pruefungsteile.StartOeffnen + " " + Pruefungsteile.Teil2Titel], Pruefungsteile.Teil2, null);
        }
        else
        {
            // am PC steht jeder Bereich in der Seitenleiste: dort die AP2-Szenarien des Bereichs
            yield return (Pruefungsteile.Seitenleiste(fr).Single(z => z.Id == Pruefungsteile.B1).Titel,
                [ueben + Pruefungsteile.Ap2Szenarien], Seiten.Ap2Szenarien, null);
            // „Prüfung“ und „Lernen“ als Hauptpunkte: Prüfung zeigt AP1 (zuerst), Lernen die Sammelseite
            yield return (Pruefungsteile.PruefungTitel, [], Pruefungsteile.Teil1, null);
            yield return (Pruefungsteile.LernenTitel, [], Seiten.Lernen, null);
        }
    }

    private static async Task<int> PruefungsteilKlicksAsync(MainView ansicht)
    {
        var funde = 0;
        var fenster = TopLevel.GetTopLevel(ansicht) as Window;
        var vorher = fenster is null ? (0.0, 0.0) : (fenster.Width, fenster.Height);
        foreach (var handy in new[] { true, false })
        {
            if (fenster is not null)
            {
                fenster.Width = handy ? SchmalBreite : PcBreite;
                fenster.Height = handy ? SchmalHoehe : PcHoehe;
                await ansicht.BildFertigAsync();
                await ansicht.BildFertigAsync();
            }
            else if (handy != ansicht.Bounds.Width < MainView.BreitGrenze)
            {
                continue;   // Handy ohne Fenster: nur der Aufbau, der gerade gilt
            }
            var geraet = handy ? "Handy" : "PC";
            var wege = 0;
            var hoechstens = 0;
            foreach (var (leiste, knoepfe, ziel, reiter) in Klickwege(handy))
            {
                // wie nach dem Start: zuletzt gezeigte Haelfte AP1
                await ansicht.ZeigeAsync(Pruefungsteile.Teil1);
                await ansicht.ZeigeAsync(Seiten.Dashboard);
                if (ansicht.GebauteSeiten[Seiten.Dashboard] is DashboardSeite start)
                {
                    await start.Geladen;
                }
                await ansicht.BildFertigAsync();
                // „Start“: die Startseite ist schon offen, der erste Klick ist der Knopf auf ihr
                var eins = leiste == "Start" ? null : KnopfNamens(handy ? ansicht.Leisten.UntereLeiste : ansicht.Leisten.Seitenleiste, leiste);
                if (leiste != "Start" && eins is null)
                {
                    Messwerte.Notiere($"  FEHLER: Klickweg {geraet}: Leiste ohne Knopf „{leiste}“");
                    funde++;
                    continue;
                }
                eins?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await ansicht.BildFertigAsync();
                var weg = true;
                foreach (var knopf in knoepfe)
                {
                    if (ansicht.GebauteSeiten.GetValueOrDefault(ansicht.AktuelleSeite) is PruefungsteilSeite teil)
                    {
                        await teil.Geladen;
                        await ansicht.BildFertigAsync();
                    }
                    var seite = ansicht.GebauteSeiten.GetValueOrDefault(ansicht.AktuelleSeite);
                    if (seite is null || KnopfNamens(seite, knopf) is not { } zwei)
                    {
                        Messwerte.Notiere($"  FEHLER: Klickweg {geraet}: {leiste} → kein Knopf „{knopf}“ auf {ansicht.AktuelleSeite}");
                        funde++;
                        weg = false;
                        break;
                    }
                    zwei.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await ansicht.BildFertigAsync();
                }
                if (!weg)
                {
                    continue;
                }
                var ist = ansicht.AktuelleSeite;
                var istReiter = ansicht.GebauteSeiten.GetValueOrDefault(ist) is PruefungsteilSeite p ? p.Reiter : null;
                if (ist != ziel || (reiter is not null && istReiter != reiter))
                {
                    Messwerte.Notiere($"  FEHLER: Klickweg {geraet}: {leiste} → {string.Join(" → ", knoepfe)} zeigt {ist}/{istReiter} statt {ziel}/{reiter}");
                    funde++;
                }
                wege++;
                hoechstens = Math.Max(hoechstens, knoepfe.Length + (leiste == "Start" ? 0 : 1));
            }
            Messwerte.Notiere($"Klickwege {geraet} ({Programm.Zustand.Sicht}): {wege} Wege geprueft, hoechstens {hoechstens} Klicks");
            if (handy)
            {
                // „Prüfung“ zeigt die zuletzt gewaehlte Haelfte: nach AP2 wieder AP2
                await ansicht.ZeigeAsync(Pruefungsteile.Teil2);
                await ansicht.ZeigeAsync(Seiten.Dashboard);
                KnopfNamens(ansicht.Leisten.UntereLeiste, Pruefungsteile.PruefungTitel)?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await ansicht.BildFertigAsync();
                if (ansicht.AktuelleSeite != Pruefungsteile.Teil2)
                {
                    Messwerte.Notiere($"  FEHLER: Prüfung nach AP2 zeigt {ansicht.AktuelleSeite} statt der zuletzt gewaehlten Haelfte AP2");
                    funde++;
                }
                await ansicht.ZeigeAsync(Pruefungsteile.Teil1);
            }
        }
        if (fenster is not null)
        {
            fenster.Width = vorher.Item1;
            fenster.Height = vorher.Item2;
            await ansicht.BildFertigAsync();
        }
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        return funde;
    }

    /// <summary>Ansichten der Pruefungsteile fuer die Layoutpruefung (in jeder Groesse und Schriftstufe).</summary>
    private static async IAsyncEnumerable<string> PruefungsteilAnsichten(MainView ansicht)
    {
        async Task<PruefungsteilSeite> Teil(string id, string? reiter)
        {
            await ansicht.ZeigeTeilAsync(id, reiter);
            var s = (PruefungsteilSeite)ansicht.GebauteSeiten[id];
            await s.Geladen;
            if (reiter is not null)
            {
                s.ReiterFuerTests(reiter);
            }
            await ansicht.BildFertigAsync();
            return s;
        }
        foreach (var id in Pruefungsteile.ReiterSeiten)
        {
            await Teil(id, Pruefungsteile.Ueben);
            yield return id + "/ueben";
            await Teil(id, Pruefungsteile.Pruefen);
            yield return id + "/pruefen";
        }
        var b1 = await Teil(Pruefungsteile.B1, Pruefungsteile.Pruefen);
        if (b1.HatSaetze)
        {
            b1.ListeFuerTests(false);
            await ansicht.BildFertigAsync();
            yield return "b1/satz-waehlen";
            b1 = await Teil(Pruefungsteile.B1, Pruefungsteile.Ueben);
            b1.ListeFuerTests(true);
            await ansicht.BildFertigAsync();
            yield return "b1/aufgabe-waehlen";
            b1.AufgabeFuerTests(1, false);
            await ansicht.BildFertigAsync();
            yield return "b1/einzelaufgabe";
            b1.AufgabeFuerTests(1, true);
            await ansicht.BildFertigAsync();
            yield return "b1/musterloesung";
            await Teil(Pruefungsteile.B1, Pruefungsteile.Ueben);
        }
    }
}
