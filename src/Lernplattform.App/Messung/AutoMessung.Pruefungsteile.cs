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
    /// P12-430, Reiter B): am Handy fuehrt „Prüfung“ zu AP1 (zuerst), AP2 ist ein Klick auf die Segment-Leiste.
    /// A 1.19.0 (Weisung P12-440, Nico 1a–4a): Pruefungstrainer und Szenarien erscheinen eingebettet in AP1 bzw. AP2
    /// (<c>Haelfte</c> = erwartete Haelfte des Rahmens); die Bereiche von AP2 stehen als Karten auf AP2 (am PC oeffnet ihr
    /// Unterpunkt AP2 › Üben); Testprojekte und Mein Projekt nur unter „Mehr“ (am Handy unten, am PC Hauptpunkt mit Unterpunkten).
    /// </summary>
    private static IEnumerable<(string Leiste, string[] Knoepfe, string Ziel, string? Reiter, string? Haelfte)> Klickwege(bool handy)
    {
        var ueben = Pruefungsteile.Ueben + " ";
        var fr = Core.Pruefung.Fachrichtung.Nach(Programm.Zustand.Sicht);
        var ap2 = Pruefungsteile.Teil2Titel + " " + Beschriftungen.Einsetzen(Pruefungsteile.SegmentGewicht,
            Core.Pruefung.Fachrichtung.Teil2.Sum(r => fr.Bereich(r).Gewicht));
        var leiste1 = handy ? Pruefungsteile.PruefungTitel : Pruefungsteile.Teil1Titel;
        var leiste2 = handy ? Pruefungsteile.PruefungTitel : Pruefungsteile.Teil2Titel;
        string[] Zu2(params string[] knoepfe) => handy ? [ap2, .. knoepfe] : knoepfe;
        const string t1 = Pruefungsteile.Teil1, t2 = Pruefungsteile.Teil2;
        yield return (leiste1, [ueben + Pruefungsteile.Pruefungsfragen + " " + Pruefungsteile.Teil1Kurz], Seiten.Trainer, null, t1);
        yield return (leiste1, [ueben + Pruefungsteile.Szenarien + " " + Pruefungsteile.Teil1Kurz], Seiten.Ap1Szenarien, null, t1);
        yield return (leiste1, [Pruefungsteile.Pruefen], t1, Pruefungsteile.Pruefen, null);
        yield return (leiste2, Zu2(Pruefungsteile.Pruefen), t2, Pruefungsteile.Pruefen, null);
        foreach (var rolle in PruefungsteilSeite.Bereiche)
        {
            var name = Pruefungsteile.BereichKurz(rolle);
            yield return (leiste2, Zu2(ueben + Pruefungsteile.Pruefungsfragen + " " + name), Seiten.Trainer, null, t2);
            // Weisung P12-442 (Nico 2b): WiSo zeigt die AP1-Fallaufgaben zur WiSo (Seite der AP1-Szenarien, eingebettet in AP2)
            yield return (leiste2, Zu2(ueben + Pruefungsteile.Szenarien + " " + name), rolle == Pruefungsteile.Wiso ? Seiten.Ap1Szenarien : Seiten.Ap2Szenarien, null, t2);
        }
        if (handy)
        {
            yield return (leiste1, [ap2], t2, Pruefungsteile.Ueben, null);
            foreach (var e in Navigation.Lernen)
            {
                yield return (Pruefungsteile.LernenTitel, [e.Titel], e.Id, null, null);
            }
            foreach (var e in Pruefungsteile.MehrEintraege)
            {
                yield return (Pruefungsteile.MehrTitel, [e.Titel], e.Id, null, null);
            }
            yield return ("Start", [Pruefungsteile.StartOeffnen + " " + Pruefungsteile.Teil1Titel], t1, null, null);
            yield return ("Start", [Pruefungsteile.StartOeffnen + " " + Pruefungsteile.Teil2Titel], t2, null, null);
        }
        else
        {
            // Nico 4a: der Unterpunkt eines Bereichs oeffnet AP2 › Üben
            foreach (var rolle in PruefungsteilSeite.Bereiche)
            {
                yield return (Pruefungsteile.Seitenleiste(fr).Single(z => z.Id == rolle).Titel, [], t2, Pruefungsteile.Ueben, null);
            }
            // „Prüfung“, „Lernen“ und „Mehr“ als Hauptpunkte: Prüfung zeigt AP1 (zuerst), Lernen und Mehr die Sammelseiten
            yield return (Pruefungsteile.PruefungTitel, [], t1, null, null);
            yield return (Pruefungsteile.LernenTitel, [], Seiten.Lernen, null, null);
            yield return (Pruefungsteile.MehrTitel, [], Pruefungsteile.Mehr, null, null);
            // Nico 3a: Testprojekte und Mein Projekt als Unterpunkte von „Mehr“
            yield return (Pruefungsteile.Testprojekte, [], Seiten.Testprojekt, null, null);
            yield return (Pruefungsteile.MeinProjekt, [], Seiten.Abschluss, null, null);
        }
    }

    /// <summary>
    /// A 1.19.0 (Weisung P12-440, Tests): Seiten, die nur eingebettet in AP1/AP2 erscheinen duerfen, und die Seiten mit eigener
    /// Leiste AP1 | AP2. Kein Klickweg darf in eine dieser Seiten ohne sichtbare Leiste AP1 | AP2 fuehren.
    /// </summary>
    internal static readonly IReadOnlySet<string> NurMitLeiste = new HashSet<string>(StringComparer.Ordinal)
    {
        Seiten.Trainer, Seiten.Ap1Szenarien, Seiten.Ap2Szenarien, Pruefungsteile.Teil1, Pruefungsteile.Teil2,
    };

    /// <summary>Ist auf der gezeigten Seite die Leiste AP1 | AP2 sichtbar (eigene oder im Rahmen)? Leer = ja, sonst der Fund.</summary>
    internal static string LeisteFund(MainView ansicht, string? haelfte)
    {
        var ist = ansicht.AktuelleSeite;
        if (!NurMitLeiste.Contains(ist))
        {
            return "";
        }
        if (ansicht.GebauteSeiten.GetValueOrDefault(ist) is PruefungsteilSeite seite)
        {
            return seite.Segment is { IsEffectivelyVisible: true } && seite.ReiterPillen is { IsEffectivelyVisible: true } ? "" : ist + " ohne Leiste AP1 | AP2";
        }
        if (ansicht.RahmenKopf is not { IsEffectivelyVisible: true } || ansicht.RahmenKopf.GetVisualDescendants().OfType<SegmentLeiste>().FirstOrDefault() is null)
        {
            return ist + " ohne Rahmen mit Leiste AP1 | AP2 (alte Seite)";
        }
        return haelfte is not null && ansicht.RahmenHaelfte != haelfte ? $"{ist} eingebettet in {ansicht.RahmenHaelfte} statt {haelfte}" : "";
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
            foreach (var (leiste, knoepfe, ziel, reiter, haelfte) in Klickwege(handy))
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
                else if (LeisteFund(ansicht, haelfte) is { Length: > 0 } leisteFund)
                {
                    Messwerte.Notiere($"  FEHLER: Klickweg {geraet}: {leiste} → {string.Join(" → ", knoepfe)}: {leisteFund}");
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

    /// <summary>Wege in Pruefungsfragen und Szenarien ausserhalb der Leisten (Weisung P12-440, Nico 5a).</summary>
    internal static readonly IReadOnlyList<string> AndereWege =
        ["suche-frage", "suche-ap1", "suche-ap2", "suche-ap1-wiso", "spiel-frage", "notizblock-frage", "notizblock-ap1", "notizblock-ap2",
            "notizblock-ap1-wiso", "fortschritt-jetzt-ueben"];

    /// <summary>
    /// A 1.19.0 (Weisung P12-440, Tests): Suche, Spiel-Tafel, Notizblock und Fortschritt „Jetzt üben“ oeffnen Pruefungsfragen und
    /// Szenarien eingebettet mit Leiste AP1 | AP2; „Zurück“ fuehrt dorthin zurueck, woher man kam (ins Spiel nur ohne Klick,
    /// das baut die 3D-Stadt).
    /// </summary>
    private static async Task<int> AndereWegeAsync(MainView ansicht)
    {
        var funde = 0;
        foreach (var weg in AndereWege)
        {
            await ansicht.ZeigeAsync(Seiten.Dashboard);
            var herkunft = await ansicht.AndererWegFuerTestsAsync(weg);
            await ansicht.BildFertigAsync();
            // AP1-Szenarien in AP1, AP2-Szenarien und die AP1-Fallaufgaben zur WiSo in AP2 (Weisung P12-442, Nico 2b)
            var haelfte = weg.EndsWith("-wiso", StringComparison.Ordinal) || weg.EndsWith("-ap2", StringComparison.Ordinal) ? Pruefungsteile.Teil2
                : weg.EndsWith("-ap1", StringComparison.Ordinal) ? Pruefungsteile.Teil1 : null;
            var fund = NurMitLeiste.Contains(ansicht.AktuelleSeite) ? LeisteFund(ansicht, haelfte) : ansicht.AktuelleSeite + " ist keine Seite mit Leiste AP1 | AP2";
            if (fund.Length > 0)
            {
                Messwerte.Notiere($"  FEHLER: Weg {weg}: {fund}");
                funde++;
                continue;
            }
            if (herkunft == Seiten.Spiel)
            {
                continue;
            }
            if (ansicht.RahmenKopf is not { } kopf || KnopfNamens(kopf, Pruefungsteile.Zurueck) is not { } zurueck)
            {
                Messwerte.Notiere($"  FEHLER: Weg {weg}: keine Zeile „← Zurück“");
                funde++;
                continue;
            }
            zurueck.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await ansicht.BildFertigAsync();
            if (ansicht.AktuelleSeite != herkunft)
            {
                Messwerte.Notiere($"  FEHLER: Weg {weg}: „Zurück“ zeigt {ansicht.AktuelleSeite} statt {herkunft}");
                funde++;
            }
        }
        Messwerte.Notiere($"Andere Wege ({Programm.Zustand.Sicht}): {AndereWege.Count} Wege geprueft, {funde} Funde");
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
        foreach (var id in (string[])[Pruefungsteile.Teil1, Pruefungsteile.Teil2])
        {
            await Teil(id, Pruefungsteile.Ueben);
            yield return id + "/ueben";
            await Teil(id, Pruefungsteile.Pruefen);
            yield return id + "/pruefen";
        }
        // A 1.19.0: AP2 ganz unten (Karte Projekt als Hinweis)
        var ap2 = await Teil(Pruefungsteile.Teil2, Pruefungsteile.Ueben);
        ap2.NachUnten();
        await ansicht.BildFertigAsync();
        yield return "teil2/unten";
        foreach (var id in (string[])[Pruefungsteile.Teil1, Pruefungsteile.Teil2])
        {
            var s = await Teil(id, Pruefungsteile.Pruefen);
            var name = id == Pruefungsteile.Teil1 ? "teil1" : "teil2-b1";
            if (!s.HatSaetze)
            {
                continue;
            }
            s.ListeFuerTests(false);
            await ansicht.BildFertigAsync();
            yield return name + "/satz-waehlen";
            s = await Teil(id, Pruefungsteile.Ueben);
            s.ListeFuerTests(true);
            await ansicht.BildFertigAsync();
            yield return name + "/aufgabe-waehlen";
            s.AufgabeFuerTests(1, false);
            await ansicht.BildFertigAsync();
            yield return name + "/einzelaufgabe";
            s.AufgabeFuerTests(1, true);
            await ansicht.BildFertigAsync();
            yield return name + "/musterloesung";
        }
        // A 1.19.0: eingebettete alte Seiten (Pruefungstrainer, Szenarien AP1 und AP2) unter der Leiste AP1 | AP2
        foreach (var (id, knopf, name) in new[]
        {
            (Pruefungsteile.Teil1, Pruefungsteile.Ueben + " " + Pruefungsteile.Pruefungsfragen + " " + Pruefungsteile.Teil1Kurz, "teil1/fragen-eingebettet"),
            (Pruefungsteile.Teil1, Pruefungsteile.Ueben + " " + Pruefungsteile.Szenarien + " " + Pruefungsteile.Teil1Kurz, "teil1/szenarien-eingebettet"),
            (Pruefungsteile.Teil2, Pruefungsteile.Ueben + " " + Pruefungsteile.Pruefungsfragen + " " + Pruefungsteile.BereichKurz(Pruefungsteile.B1), "teil2/fragen-eingebettet"),
            (Pruefungsteile.Teil2, Pruefungsteile.Ueben + " " + Pruefungsteile.Szenarien + " " + Pruefungsteile.BereichKurz(Pruefungsteile.B1), "teil2/szenarien-eingebettet"),
            // Weisung P12-442 (Nico 2b): die AP1-Fallaufgaben zur WiSo unter AP2 › Üben › WiSo
            (Pruefungsteile.Teil2, Pruefungsteile.Ueben + " " + Pruefungsteile.Szenarien + " " + Pruefungsteile.BereichKurz(Pruefungsteile.Wiso), "teil2/wiso-uebungsaufgaben-eingebettet"),
        })
        {
            var s = await Teil(id, Pruefungsteile.Ueben);
            if (KnopfNamens(s, knopf) is not { IsEnabled: true } k)
            {
                continue;
            }
            k.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await ansicht.BildFertigAsync();
            if (ansicht.GebauteSeiten.GetValueOrDefault(ansicht.AktuelleSeite) is TrainerSeite trainer)
            {
                await trainer.Geladen;
            }
            else if (ansicht.GebauteSeiten.GetValueOrDefault(ansicht.AktuelleSeite) is SzenarioSeite szenarien)
            {
                await szenarien.Geladen;
            }
            await ansicht.BildFertigAsync();
            yield return name;
            // die laufende Runde beenden, ohne etwas zu speichern (wie „Zurück“): nur die Seite wechseln
            await ansicht.ZeigeAsync(id);
        }
        await Teil(Pruefungsteile.Teil1, Pruefungsteile.Ueben);
    }
}
