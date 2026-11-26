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
/// <item>Klickwege wie mit Maus oder Finger: jede bisherige Seite in hoechstens zwei Klicks, am Handy ueber die untere Leiste,
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

    /// <summary>Zwei Klicks je bisheriger Seite (Start in der Leiste, dann ein Knopf der gezeigten Seite).</summary>
    private static IEnumerable<(string Leiste, string? Knopf, string Ziel, string? Reiter)> Klickwege(bool handy)
    {
        var ueben = Pruefungsteile.Ueben + " ";
        var leiste1 = handy ? "Teil 1" : Pruefungsteile.Teil1Titel;
        var leiste2 = handy ? "Teil 2" : Pruefungsteile.Teil2Titel;
        yield return (leiste1, ueben + Pruefungsteile.Pruefungsfragen, Seiten.Trainer, null);
        yield return (leiste1, ueben + Pruefungsteile.Ap1Szenarien, Seiten.Ap1Szenarien, null);
        yield return (leiste1, Pruefungsteile.Pruefen, Pruefungsteile.Teil1, Pruefungsteile.Pruefen);
        yield return (leiste2, Pruefungsteile.Testprojekte, Seiten.Testprojekt, null);
        yield return (leiste2, Pruefungsteile.MeinProjekt, Seiten.Abschluss, null);
        foreach (var rolle in (string[])[Pruefungsteile.B1, Pruefungsteile.B2, Pruefungsteile.Wiso])
        {
            yield return (leiste2, ueben + Pruefungsteile.BereichKurz(rolle), rolle, Pruefungsteile.Ueben);
            yield return (leiste2, Pruefungsteile.Pruefen + " " + Pruefungsteile.BereichKurz(rolle), rolle, Pruefungsteile.Pruefen);
        }
        if (handy)
        {
            foreach (var e in Navigation.Lernen)
            {
                yield return ("Lernen", e.Titel, e.Id, null);
            }
            foreach (var e in Pruefungsteile.MehrEintraege)
            {
                yield return ("Mehr", e.Titel, e.Id, null);
            }
            yield return ("Start", Pruefungsteile.StartOeffnen + " " + Pruefungsteile.Teil1Titel, Pruefungsteile.Teil1, null);
            yield return ("Start", Pruefungsteile.StartOeffnen + " " + Pruefungsteile.Teil2Titel, Pruefungsteile.Teil2, null);
        }
        else
        {
            // am PC steht jeder Bereich in der Seitenleiste: dort die AP2-Szenarien des Bereichs
            yield return (Pruefungsteile.Seitenleiste(Core.Pruefung.Fachrichtung.Nach(Programm.Zustand.Sicht)).Single(z => z.Id == Pruefungsteile.B1).Titel,
                ueben + Pruefungsteile.Ap2Szenarien, Seiten.Ap2Szenarien, null);
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
            foreach (var (leiste, knopf, ziel, reiter) in Klickwege(handy))
            {
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
                if (ansicht.GebauteSeiten.GetValueOrDefault(ansicht.AktuelleSeite) is PruefungsteilSeite teil)
                {
                    await teil.Geladen;
                    await ansicht.BildFertigAsync();
                }
                if (knopf is not null)
                {
                    var seite = ansicht.GebauteSeiten.GetValueOrDefault(ansicht.AktuelleSeite);
                    if (seite is null || KnopfNamens(seite, knopf) is not { } zwei)
                    {
                        Messwerte.Notiere($"  FEHLER: Klickweg {geraet}: {leiste} → kein Knopf „{knopf}“ auf {ansicht.AktuelleSeite}");
                        funde++;
                        continue;
                    }
                    zwei.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await ansicht.BildFertigAsync();
                }
                var ist = ansicht.AktuelleSeite;
                var istReiter = ansicht.GebauteSeiten.GetValueOrDefault(ist) is PruefungsteilSeite p ? p.Reiter : null;
                if (ist != ziel || (reiter is not null && istReiter != reiter))
                {
                    Messwerte.Notiere($"  FEHLER: Klickweg {geraet}: {leiste} → {knopf} zeigt {ist}/{istReiter} statt {ziel}/{reiter}");
                    funde++;
                }
                wege++;
            }
            Messwerte.Notiere($"Klickwege {geraet} ({Programm.Zustand.Sicht}): {wege} Wege mit hoechstens zwei Klicks geprueft");
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
