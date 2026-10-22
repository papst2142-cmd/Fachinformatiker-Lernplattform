using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Lernplattform.Core.Daten;
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
    /// </summary>
    private static async Task<int> W4MesspunkteAsync(MainView ansicht)
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
        for (var runde = 0; runde < 3; runde++)
        {
            await ansicht.ZeigeAsync(Seiten.Dashboard);
            var uhr = Stopwatch.StartNew();
            await ansicht.ZeigeAsync(Seiten.Hilfe);
            hilfe.Gezeigt();
            await ansicht.BildFertigAsync();
            Messwerte.Reihe("Hilfe wieder oeffnen", uhr.Elapsed.TotalMilliseconds);
            foreach (var abschnitt in HilfeTexte.Abschnitte)
            {
                hilfe.Oeffnen(abschnitt.Id);         // misst "Hilfe: Abschnitt aufklappen" bis zum Bild
                await ansicht.BildFertigAsync();
                await ansicht.BildFertigAsync();
            }
            hilfe.Gezeigt();
            await ansicht.BildFertigAsync();
        }
        for (var runde = 0; runde < 3; runde++)
        {
            if (RundgangEbene.Probe() is not { } rundgang)
            {
                Messwerte.Notiere("Rundgang: FEHLER, Probe nicht moeglich");
                return funde + 1;
            }
            await ansicht.BildFertigAsync();
            while (!rundgang.Ablauf.Letzte)
            {
                rundgang.Fertig();                   // "Weiter": misst "Rundgang: Schritt" bzw. "Seite 5 bauen"
                await ansicht.BildFertigAsync();
            }
            rundgang.Ueberspringen();
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
                for (var runde = 0; runde < 3; runde++)
                {
                    klappe.IsExpanded = false;
                    await ansicht.BildFertigAsync();
                    var uhr = Stopwatch.StartNew();
                    klappe.IsExpanded = true;
                    await ansicht.BildFertigAsync();
                    Messwerte.Reihe("Optionen: " + name + " aufklappen", uhr.Elapsed.TotalMilliseconds);
                }
            }
        }
        for (var runde = 0; runde < 3; runde++)
        {
            var (frage, _) = AusbildungTeil.FrageZeigen();
            await ansicht.BildFertigAsync();
            Dialog.AlleSchliessen();
            await frage;
            var warnung = AusbildungTeil.WarnungZeigen(WarnungFachrichtung);
            await ansicht.BildFertigAsync();
            Dialog.AlleSchliessen();
            await warnung;
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
