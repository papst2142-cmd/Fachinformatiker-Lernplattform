using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Hilfe;
using Lernplattform.Core.Texte;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 4, W4 (Plan P4-10 Abschnitt 6): Ansichten, Messpunkte und Tastaturpruefung von Hilfe,
/// Rundgang, Ausbildungsfrage, Warnhinweis und den Optionen "Rundgang und Hilfe" und "Lerninhalte".
/// Nichts wird gespeichert: der Rundgang laeuft als Probe, Frage und Hinweis werden nur gezeigt
/// und mit Dialog.AlleSchliessen geschlossen (ohne Auswertung der Antwort).
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Ab dieser Laufzeit pruefen die W4-Ansichten nur noch 2 Groessen (Plan Phase 4, Zeitbudget).</summary>
    public static readonly TimeSpan W4Zeitgrenze = TimeSpan.FromMinutes(7.5);

    /// <summary>Laengster Text, den der Warnhinweis zeigt (DPA, Weisung P4-11).</summary>
    public const string WarnungFachrichtung = "dpa";

    /// <summary>Anzahl der W4-Ansichten (fuer den Bericht).</summary>
    public static int W4AnsichtenAnzahl => 2 + RundgangAblauf.Anzahl + 1 + 2 + 2;

    // Marken der Hilfe-Abschnitte (HELP_ACCENTS) auf card; accent, purple, green, orange, red stehen schon oben
    private static readonly (string Text, string Flaeche)[] W4Paare =
    [
        ("accent2", "card"),
        // Weisung P8-5b: Gruppenfarben der Optionen, Titel auf der Karte und Ueberschrift auf dem Hintergrund
        ("blue", "card"), ("pink", "card"), ("purple", "card"), ("orange", "card"), ("cyan", "card"), ("red", "card"),
        ("blue", "bg"), ("cyan", "bg"), ("pink", "bg"), ("purple", "bg"), ("orange", "bg"),
        // Weisung P8-10: Griff der Scrollleisten (Programmfarbe) gegen Spur und Hintergruende
        ("accent", "scrollbar"), ("accent", "sidebar"), ("accent", "bg"), ("accent", "card"),
    ];

    private static HilfeAbschnitt LaengsterAbschnitt => HilfeTexte.Abschnitte.MaxBy(a => a.Text.Length)!;

    /// <summary>
    /// Die neuen Ansichten der W4 nacheinander (je nach dem Zeigen pruefen): Hilfe eingeklappt und mit dem
    /// laengsten Abschnitt offen, Rundgang Seiten 1-5 und Seite 5 mit Fehlerzeile, Ausbildungsfrage,
    /// Warnhinweis DPA, Optionen "Rundgang und Hilfe" und "Lerninhalte" aufgeklappt.
    /// </summary>
    private static async IAsyncEnumerable<string> W4Ansichten(MainView ansicht)
    {
        if (ansicht.GebauteSeiten.TryGetValue(Seiten.Hilfe, out var h) && h is HilfeSeite hilfe)
        {
            await ansicht.ZeigeAsync(Seiten.Hilfe);
            hilfe.Gezeigt();
            await ansicht.BildFertigAsync();
            yield return "hilfe/eingeklappt";
            hilfe.Oeffnen(LaengsterAbschnitt.Id);
            await ansicht.BildFertigAsync();
            await ansicht.BildFertigAsync();
            yield return "hilfe/abschnitt-" + LaengsterAbschnitt.Id;
            hilfe.Gezeigt();
        }
        if (RundgangEbene.Probe() is { } rundgang)
        {
            for (var i = 0; i < RundgangAblauf.Anzahl; i++)
            {
                rundgang.Springen(i);
                await ansicht.BildFertigAsync();
                yield return "rundgang/seite-" + (i + 1).ToString(CultureInfo.InvariantCulture);
            }
            if (rundgang.Elemente[RundgangElement.TerminAp1] is TextBox ap1)
            {
                ap1.Text = "31.02.2027";
            }
            rundgang.Fertig();
            await ansicht.BildFertigAsync();
            yield return "rundgang/seite-5-fehler";
            rundgang.Ueberspringen();
            await ansicht.BildFertigAsync();
        }
        var (frage, _) = AusbildungTeil.FrageZeigen();
        await ansicht.BildFertigAsync();
        yield return "ausbildung/frage";
        Dialog.AlleSchliessen();
        await frage;
        var warnung = AusbildungTeil.WarnungZeigen(WarnungFachrichtung);
        await ansicht.BildFertigAsync();
        yield return "ausbildung/warnhinweis-dpa";
        Dialog.AlleSchliessen();
        await warnung;
        if (ansicht.GebauteSeiten.TryGetValue(Seiten.Optionen, out var o) && o is OptionenSeite optionen)
        {
            await ansicht.ZeigeAsync(Seiten.Optionen);
            foreach (var (id, name) in new[] { (Optionen.Rundgang, "rundgang-und-hilfe"), (Optionen.Lerninhalte, "lerninhalte") })
            {
                optionen.OeffneBereich(id, false);
                await ansicht.BildFertigAsync();
                await ansicht.BildFertigAsync();
                yield return "optionen/" + name;
            }
        }
    }

    /// <summary>
    /// Messpunkte W4 (Grenze 400 ms, Fund ab 1 000 ms), je dreimal bis zum Bild: Hilfe wieder oeffnen,
    /// jeden Abschnitt aufklappen, Rundgang zeigen und je Schritt, Seite 5 bauen, Optionen-Bereiche
    /// aufklappen, Ausbildungsfrage und Warnhinweis zeigen. Dazu die Tastaturpruefung (nur PC).
    /// Messlogik nach Weisung P4-12 B6 (eingeordnet mit Weisung P4-16): einmalig und streng sind
    /// "Hilfe erstes Oeffnen" (Erstes Oeffnen der Seite) und das erste "Rundgang zeigen" (erster Bau
    /// der Rundgang-Ebene); alles andere ist wiederholbar mit dem Zustand davor. Rote Werte stehen
    /// danach in <paramref name="reihenRot"/>.
    /// </summary>
    private static async Task<int> W4MesspunkteAsync(MainView ansicht, List<string> reihenRot)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        var einstellungen = Zustand.Einstellungen.Pfad;
        var vorher = File.Exists(einstellungen) ? File.ReadAllBytes(einstellungen) : null;
        if (Messwerte.ErstesOeffnen.TryGetValue(Seiten.Hilfe, out var erstes))
        {
            Messwerte.Notiere(string.Create(inv, $"Hilfe erstes Oeffnen: {erstes:F0} ms"));
        }
        if (!ansicht.GebauteSeiten.TryGetValue(Seiten.Hilfe, out var h) || h is not HilfeSeite hilfe)
        {
            Messwerte.Notiere("Hilfe: FEHLER, Seite nicht gebaut");
            return 1;
        }
        var klappen = hilfe.GetLogicalDescendants().OfType<Expander>().ToList();
        for (var runde = 0; runde < 3; runde++)
        {
            await ansicht.ZeigeAsync(Seiten.Dashboard);
            await W4WertAsync("Hilfe wieder oeffnen", async () =>
            {
                var uhr = Stopwatch.StartNew();
                await ansicht.ZeigeAsync(Seiten.Hilfe);
                hilfe.Gezeigt();
                await ansicht.BildFertigAsync();
                Messwerte.Reihe("Hilfe wieder oeffnen", uhr.Elapsed.TotalMilliseconds);
            }, () => ansicht.ZeigeAsync(Seiten.Dashboard), reihenRot);
            foreach (var abschnitt in HilfeTexte.Abschnitte)
            {
                var klappe = klappen.FirstOrDefault(k => Avalonia.Automation.AutomationProperties.GetName(k) == abschnitt.Titel);
                if (klappe is null)
                {
                    // ohne Klappe kein Zustand davor: der Punkt waere still einmalig (Gegenpruefung L1)
                    Messwerte.Notiere("Hilfe: FEHLER, keine Klappe fuer Abschnitt " + abschnitt.Id);
                    funde++;
                }
                // misst "Hilfe: Abschnitt aufklappen" bis zum Bild; davor: Abschnitt zu
                await W4WertAsync("Hilfe: Abschnitt aufklappen", async () =>
                {
                    hilfe.Oeffnen(abschnitt.Id);
                    await ansicht.BildFertigAsync();
                    await ansicht.BildFertigAsync();
                }, klappe is null ? null : async () =>
                {
                    klappe.IsExpanded = false;
                    await ansicht.BildFertigAsync();
                }, reihenRot);
            }
            hilfe.Gezeigt();
            await ansicht.BildFertigAsync();
        }
        for (var runde = 0; runde < 3; runde++)
        {
            RundgangEbene? rundgang = null;
            // misst "Rundgang zeigen"; das erste Mal einmalig (erster Bau), danach davor: Rundgang zu
            await W4WertAsync("Rundgang zeigen", async () =>
            {
                rundgang = RundgangEbene.Probe();
                await ansicht.BildFertigAsync();
            }, runde == 0 ? null : async () =>
            {
                rundgang?.Ueberspringen();
                await ansicht.BildFertigAsync();
            }, reihenRot);
            if (rundgang is not { } offen)
            {
                Messwerte.Notiere("Rundgang: FEHLER, Probe nicht moeglich");
                return funde + 1;
            }
            while (!offen.Ablauf.Letzte)
            {
                var index = offen.Ablauf.Index;
                var name = index + 1 == RundgangAblauf.Anzahl - 1 ? "Rundgang: Seite 5 bauen" : "Rundgang: Schritt";
                // "Weiter"; davor: dieselbe Seite wieder zeigen
                await W4WertAsync(name, async () =>
                {
                    offen.Fertig();
                    await ansicht.BildFertigAsync();
                }, async () =>
                {
                    offen.Springen(index);
                    await ansicht.BildFertigAsync();
                }, reihenRot);
            }
            offen.Ueberspringen();
            await ansicht.BildFertigAsync();
        }
        if (ansicht.GebauteSeiten.TryGetValue(Seiten.Optionen, out var o) && o is OptionenSeite optionen)
        {
            await ansicht.ZeigeAsync(Seiten.Optionen);
            foreach (var (id, name) in new[] { (Optionen.Rundgang, "Rundgang und Hilfe"), (Optionen.Lerninhalte, "Lerninhalte") })
            {
                if (optionen.Klappe(id) is not { } klappe)
                {
                    Messwerte.Notiere("Optionen " + name + ": FEHLER, kein Klappbereich");
                    funde++;
                    continue;
                }
                async Task Zu()
                {
                    klappe.IsExpanded = false;
                    await ansicht.BildFertigAsync();
                }
                for (var runde = 0; runde < 3; runde++)
                {
                    await Zu();
                    await W4WertAsync("Optionen: " + name + " aufklappen", async () =>
                    {
                        var uhr = Stopwatch.StartNew();
                        klappe.IsExpanded = true;
                        await ansicht.BildFertigAsync();
                        Messwerte.Reihe("Optionen: " + name + " aufklappen", uhr.Elapsed.TotalMilliseconds);
                    }, Zu, reihenRot);
                }
            }
        }
        for (var runde = 0; runde < 3; runde++)
        {
            Task<int>? frage = null;
            Task<int>? warnung = null;
            // misst "Ausbildungsfrage zeigen" und "Warnhinweis zeigen"; davor: Fenster zu
            await W4WertAsync("Ausbildungsfrage zeigen", async () =>
            {
                (frage, _) = AusbildungTeil.FrageZeigen();
                await ansicht.BildFertigAsync();
            }, async () =>
            {
                Dialog.AlleSchliessen();
                await frage!;
            }, reihenRot);
            Dialog.AlleSchliessen();
            await frage!;
            await W4WertAsync("Warnhinweis zeigen", async () =>
            {
                warnung = AusbildungTeil.WarnungZeigen(WarnungFachrichtung);
                await ansicht.BildFertigAsync();
            }, async () =>
            {
                Dialog.AlleSchliessen();
                await warnung!;
            }, reihenRot);
            Dialog.AlleSchliessen();
            await warnung!;
        }
        if (TopLevel.GetTopLevel(ansicht) is Window)
        {
            funde += await TastaturRundgangAsync(ansicht);
            funde += await TastaturHilfeAsync(ansicht, hilfe);
        }
        else
        {
            Messwerte.Notiere("Tastaturpruefung W4: nur am PC (Handy ohne Tastatur)");
        }
        var nachher = File.Exists(einstellungen) ? File.ReadAllBytes(einstellungen) : null;
        var gleich = vorher is null ? nachher is null : nachher is not null && vorher.AsSpan().SequenceEqual(nachher);
        Messwerte.Notiere(gleich ? "W4: einstellungen.json unveraendert (nichts gespeichert)" : "W4: FEHLER, einstellungen.json wurde geaendert");
        return funde + (gleich ? 0 : 1);
    }

    private static void Taste(TopLevel top, Key taste, KeyModifiers zusatz = KeyModifiers.None)
    {
        var ziel = top.FocusManager?.GetFocusedElement() as Interactive ?? top;
        ziel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = taste, KeyModifiers = zusatz, Source = ziel });
        ziel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = taste, KeyModifiers = zusatz, Source = ziel });
    }

    /// <summary>
    /// Weisung P4-16: ein W4-Messpunkt nach der Messlogik von Weisung P4-12 B6. Den Wert schreibt das
    /// Programm (oder <paramref name="schritt"/>) in die Messreihe <paramref name="name"/>. Liegt er
    /// ueber 1 000 ms, wird nach <paramref name="zurueck"/> (Zustand davor, nicht gemessen) und
    /// 200 ms Pause bis zu 3 Mal gemessen; ohne <paramref name="zurueck"/> bleibt der Punkt einmalig
    /// und streng. Danach behaelt die Reihe <paramref name="name"/> nur den kleinsten Wert dieses
    /// Punkts; Werte, die <paramref name="zurueck"/> schreibt, fallen heraus. Werte anderer Messpunkte,
    /// die der Schritt selbst schreibt (z. B. "Seitenwechsel (gebaute Seite)" aus MainView, streng),
    /// bleiben alle stehen. Rot nur, wenn alle Messungen ueber der Grenze liegen (dann mit allen
    /// Einzelmessungen in <paramref name="rot"/>).
    /// </summary>
    private static async Task W4WertAsync(string name, Func<Task> schritt, Func<Task>? zurueck, List<string> rot)
    {
        var anfang = Messwerte.Stand().GetValueOrDefault(name);
        async Task<double?> Einmal()
        {
            var stand = Messwerte.Stand();
            await schritt();
            return Messwerte.NeuSeit(stand, name);
        }
        async Task Zurueck()
        {
            var stand = Messwerte.Stand();
            await zurueck!();
            Messwerte.ZurueckAuf(stand);    // nur die Werte aus zurueck heraus (nicht gemessen)
        }
        var erste = await Einmal();
        var reihe = zurueck is null
            ? Messwiederholung.Einmal(erste, Abbruch)
            : await Messwiederholung.MessenAsync(erste, Abbruch, Einmal, Zurueck, Task.Delay);
        Messwerte.ReiheAuf(name, anfang);
        Messwerte.Reihe(name, reihe.Bester ?? double.PositiveInfinity);
        if (reihe.Wiederholt)
        {
            Messwerte.Notiere("Wiederholt " + name + ": Einzelmessungen " + reihe.Einzelwerte() + (reihe.Rot ? " (rot)" : " (gruen)"));
        }
        if (reihe.Rot)
        {
            rot.Add(reihe.Fehlertext(name));
        }
    }

    /// <summary>
    /// Hilfe "tastatur" am Rundgang: Tab/Umschalt+Tab ringfoermig nach der Reihenfolge des Kerns, Eingabe
    /// und Leertaste druecken Knoepfe und schalten Schalter um, Esc ueberspringt (Probe: ohne Speichern).
    /// </summary>
    private static async Task<int> TastaturRundgangAsync(MainView ansicht)
    {
        var top = TopLevel.GetTopLevel(ansicht)!;
        var fehler = new List<string>();
        if (RundgangEbene.Probe() is not { } rundgang)
        {
            Messwerte.Notiere("Tastatur Rundgang: FEHLER, Probe nicht moeglich");
            return 1;
        }
        await ansicht.BildFertigAsync();
        foreach (var seite in new[] { 0, 1, RundgangAblauf.Anzahl - 1 })
        {
            rundgang.Springen(seite);
            await ansicht.BildFertigAsync();
            var reihe = rundgang.Ablauf.Reihenfolge;
            if (rundgang.FokusElement != reihe[0])
            {
                fehler.Add($"Seite {seite + 1}: Fokus am Anfang {rundgang.FokusElement}, erwartet {reihe[0]}");
            }
            foreach (var rueckwaerts in new[] { false, true })
            {
                var erwartet = reihe[0];
                for (var i = 0; i < reihe.Count; i++)
                {
                    Taste(top, Key.Tab, rueckwaerts ? KeyModifiers.Shift : KeyModifiers.None);
                    await ansicht.BildFertigAsync();
                    erwartet = rundgang.Ablauf.Naechstes(erwartet, rueckwaerts);
                    if (rundgang.FokusElement != erwartet)
                    {
                        fehler.Add($"Seite {seite + 1}: {(rueckwaerts ? "Umschalt+Tab" : "Tab")} Schritt {i + 1} Fokus {rundgang.FokusElement}, erwartet {erwartet}");
                        break;
                    }
                }
                if (rundgang.FokusElement != reihe[0])
                {
                    fehler.Add($"Seite {seite + 1}: {(rueckwaerts ? "Umschalt+Tab" : "Tab")} nicht ringfoermig");
                }
            }
        }
        // Schalter mit Leertaste und Eingabe (Seite 5)
        foreach (var taste in new[] { Key.Space, Key.Enter })
        {
            var schalter = (ToggleSwitch)rundgang.Elemente[RundgangElement.AbschnittB];
            schalter.Focus(NavigationMethod.Tab);
            await ansicht.BildFertigAsync();
            var alt = schalter.IsChecked == true;
            Taste(top, taste);
            await ansicht.BildFertigAsync();
            if ((schalter.IsChecked == true) == alt)
            {
                fehler.Add(taste + " schaltet den Schalter nicht um");
            }
        }
        // Knoepfe mit Eingabe und Leertaste: "Weiter" auf Seite 1 und 2, "Zurück" auf Seite 3
        rundgang.Springen(0);
        await ansicht.BildFertigAsync();
        foreach (var (taste, element, index) in new[] { (Key.Enter, RundgangElement.Weiter, 1), (Key.Space, RundgangElement.Weiter, 2), (Key.Enter, RundgangElement.Zurueck, 1) })
        {
            rundgang.Elemente[element].Focus(NavigationMethod.Tab);
            await ansicht.BildFertigAsync();
            Taste(top, taste);
            await ansicht.BildFertigAsync();
            if (rundgang.Ablauf.Index != index)
            {
                fehler.Add($"{taste} auf {element}: Seite {rundgang.Ablauf.Index + 1}, erwartet {index + 1}");
            }
        }
        Taste(top, Key.Escape);
        await ansicht.BildFertigAsync();
        if (!rundgang.Geschlossen)
        {
            fehler.Add("Esc schliesst den Rundgang nicht");
            rundgang.Ueberspringen();
        }
        Messwerte.Notiere(fehler.Count == 0
            ? "Tastatur Rundgang (Seiten 1, 2, 5): Tab und Umschalt+Tab ringfoermig wie der Kern, Leertaste/Eingabe schalten Schalter um und druecken Knoepfe, Esc ueberspringt: in Ordnung"
            : "Tastatur Rundgang: FEHLER " + string.Join("; ", fehler));
        return fehler.Count;
    }

    /// <summary>
    /// Hilfe "tastatur" an der Hilfe: Tab und Umschalt+Tab wechseln zwischen den Abschnitten, das gewaehlte
    /// Element traegt :focus-visible (Rahmen), Eingabe und Leertaste klappen auf und zu.
    /// </summary>
    private static async Task<int> TastaturHilfeAsync(MainView ansicht, HilfeSeite hilfe)
    {
        var top = TopLevel.GetTopLevel(ansicht)!;
        var fehler = new List<string>();
        await ansicht.ZeigeAsync(Seiten.Hilfe);
        hilfe.Gezeigt();
        await ansicht.BildFertigAsync();
        var kopf = hilfe.Klappen.Values.Select(k => k.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault()).ToList();
        if (kopf.Any(k => k is null))
        {
            Messwerte.Notiere("Tastatur Hilfe: FEHLER, Kopf eines Abschnitts nicht gefunden");
            return 1;
        }
        kopf[0]!.Focus(NavigationMethod.Tab);
        await ansicht.BildFertigAsync();
        for (var i = 1; i < kopf.Count; i++)
        {
            Taste(top, Key.Tab);
            await ansicht.BildFertigAsync();
            if (!ReferenceEquals(top.FocusManager?.GetFocusedElement(), kopf[i]))
            {
                fehler.Add($"Tab {i}: Fokus nicht auf Abschnitt {i + 1}");
                kopf[i]!.Focus(NavigationMethod.Tab);
            }
            if (!kopf[i]!.Classes.Contains(":focus-visible"))
            {
                fehler.Add($"Abschnitt {i + 1}: kein :focus-visible");
            }
        }
        Taste(top, Key.Tab, KeyModifiers.Shift);
        await ansicht.BildFertigAsync();
        if (!ReferenceEquals(top.FocusManager?.GetFocusedElement(), kopf[^2]))
        {
            fehler.Add("Umschalt+Tab: Fokus nicht auf dem vorigen Abschnitt");
        }
        var klappe = hilfe.Klappen.Values.First();
        kopf[0]!.Focus(NavigationMethod.Tab);
        await ansicht.BildFertigAsync();
        foreach (var (taste, offen) in new[] { (Key.Enter, true), (Key.Enter, false), (Key.Space, true), (Key.Space, false) })
        {
            Taste(top, taste);
            await ansicht.BildFertigAsync();
            if (klappe.IsExpanded != offen)
            {
                fehler.Add($"{taste}: Abschnitt {(offen ? "nicht aufgeklappt" : "nicht zugeklappt")}");
                klappe.IsExpanded = offen;
            }
        }
        hilfe.Gezeigt();
        await ansicht.BildFertigAsync();
        Messwerte.Notiere(fehler.Count == 0
            ? "Tastatur Hilfe (16 Abschnitte): Tab und Umschalt+Tab wechseln, :focus-visible am gewaehlten Abschnitt, Eingabe und Leertaste klappen auf und zu: in Ordnung"
            : "Tastatur Hilfe: FEHLER " + string.Join("; ", fehler));
        return fehler.Count;
    }
}
