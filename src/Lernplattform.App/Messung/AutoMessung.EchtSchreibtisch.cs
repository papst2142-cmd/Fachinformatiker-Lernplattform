using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// A 1.20.0 "Pruefung echt W2" (Weisung P12-446, Entwurf P12-267, Nico 1a bis 5a): Schreibtischtest im Raster, Ausgabe- und
/// Datenauszug-Anlage, "Groß öffnen". Der Start-Test misst die Ansichten ab <see cref="EchtFormatTeil.SchreibtischAb"/> (erstes
/// Oeffnen streng 400 ms; CSV umschalten und "Groß öffnen" wiederholbar), prueft: jedes Kaestchen hat einen Namen fuer den
/// Screenreader ("Schritt n, Zeile z, Spalte", leer mit ", leer"); die Spalte "Zeile" bleibt beim seitlichen Wischen stehen (Nico
/// 1a) und der Hinweis nennt die verdeckten Spalten; der gewaehlte Schritt markiert seine Zeile im Code; Eingabetaste springt zur
/// naechsten offenen Zelle; eine Eingabe landet unter <c>&lt;aufgabe&gt;.&lt;nr&gt;/z{schritt}s{spalte}</c>; die Ausgabe zeigt
/// Eingabezeilen und liest als Text vor; der CSV-Umschalter zeigt die Tabelle; "Groß öffnen" nur bei Ausgabe und Datenauszug
/// (Nico 3a), am PC als Fenster, schmal als ganzer Bildschirm, Escape schliesst; die Bewertung zeigt gruen und rot.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> EchtSchreibtischAsync(MainView ansicht, KlausurTeil k, EchtFormatTeil e,
        Func<string, Action, Action?, bool, Task> messpunkt)
    {
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("  FEHLER: Pruefung im echten Format (Schreibtischtest): " + text);
            funde++;
        }
        var ab = KlausurTeil.EchtAb + 1 + EchtFormatTeil.SchreibtischAb;
        await messpunkt("Pruefung im echten Format Schreibtischtest im Lauf (erstes Oeffnen, bis zum Bild)", () => k.Probe(ab), null, false);
        await messpunkt("Pruefung im echten Format Bewertung Schreibtischtest (bis zum Bild)", () => k.Probe(ab + 2), null, true);
        await messpunkt("Pruefung im echten Format Ausgabe-Anlage (erstes Oeffnen, bis zum Bild)", () => k.Probe(ab + 3), null, false);
        await messpunkt("Pruefung im echten Format Datenauszug CSV (erstes Oeffnen, bis zum Bild)", () => k.Probe(ab + 4), null, false);

        // Raster im Lauf: Namen, Markierung im Code, feste Spalte, Eingabetaste, Speichern
        k.Probe(ab);
        await ansicht.BildFertigAsync();
        if (e.ProbeSchreibtisch is not { } raster)
        {
            Fund("kein Raster im Lauf");
            return funde;
        }
        var felder = raster.Felder;
        var ohneName = felder.Count(f => string.IsNullOrEmpty(Avalonia.Automation.AutomationProperties.GetName(f)));
        var leerBenannt = felder.Where(f => (f.Text ?? "").Trim().Length == 0)
            .All(f => (Avalonia.Automation.AutomationProperties.GetName(f) ?? "").EndsWith(EchtTexte.SchreibtischLeer, StringComparison.Ordinal));
        if (felder.Count != 6 || ohneName > 0 || !leerBenannt)
        {
            Fund("Kaestchen: " + felder.Count + " (erwartet 6), ohne Namen " + ohneName + ", leere ohne \"" + EchtTexte.SchreibtischLeer + "\"");
        }
        // Code steht daneben (breit) oder unter "Anlagen" (schmal): dann dorthin wechseln, die Markierung muss bleiben
        var gewaehlt = raster.ProbeStand.Schritt;
        var markiert = e.ProbeCodeBlatt?.MarkiertText ?? "";
        if (e.ProbeCodeBlatt is null)
        {
            k.Probe(ab + 1);
            await ansicht.BildFertigAsync();
            markiert = e.ProbeCodeBlatt?.MarkiertText ?? "";
            k.Probe(ab);
            await ansicht.BildFertigAsync();
            raster = e.ProbeSchreibtisch!;
        }
        if (gewaehlt < 0 || !markiert.StartsWith("Markiert: Zeile ", StringComparison.Ordinal)
            || !markiert.Contains("Schritt " + (gewaehlt + 1), StringComparison.Ordinal))
        {
            Fund("gewaehlter Schritt " + (gewaehlt + 1) + " markiert keine Zeile im Code (\"" + markiert + "\")");
        }
        felder = raster.Felder;
        Messwerte.Notiere("Pruefung im echten Format Schreibtischtest: " + felder.Count + " Kaestchen, " + markiert + ", " + raster.ProbeStand.Wischen);

        // Tastatur: Eingabetaste im ersten Kaestchen springt ins zweite; Fokus waehlt dessen Schritt und markiert den Code
        await TasteAsync(ansicht, felder[0], Key.Enter);
        var fokus = TopLevel.GetTopLevel(ansicht)?.FocusManager?.GetFocusedElement();
        if (!ReferenceEquals(fokus, felder[1]))
        {
            Fund("Tastatur: Eingabetaste springt nicht zur naechsten offenen Zelle");
        }
        var zweiterSchritt = raster.ProbeStand.Schritt;
        if (zweiterSchritt <= 0 || (e.ProbeCodeBlatt is { } blatt && !blatt.MarkiertText.Contains("Schritt " + (zweiterSchritt + 1), StringComparison.Ordinal)))
        {
            Fund("Tastatur: Fokus im naechsten Kaestchen markiert dessen Zeile nicht (" + e.ProbeCodeBlatt?.MarkiertText + ")");
        }

        // Eingabe landet unter dem Schluessel des Kaestchens (Festhalten wie beim Wechsel der Aufgabe)
        var leer = felder[EchtFormatTeil.ProbeLeer];
        leer.Text = "6";
        e.ProbeFesthalten();
        var teil = e.ProbeSatz!.Aufgaben[3].Teile.First(t => t.Antwort.Form == EchtSaetze.FormSchreibtischtest);
        var schluessel = Enumerable.Range(0, teil.Antwort.Schritte.Count)
            .SelectMany(z => Enumerable.Range(0, teil.Antwort.Spalten.Count).Select(s => (z, s)))
            .Where(p => teil.Antwort.Schritte[p.z].Werte[p.s].Offen).ElementAt(EchtFormatTeil.ProbeLeer);
        var gespeichert = EchtBewertung.Antwort(e.ProbeZustandJetzt!, EchtBewertung.Feld(4, teil.Nr, EchtBewertung.Zelle(schluessel.z, schluessel.s)));
        var nameJetzt = Avalonia.Automation.AutomationProperties.GetName(leer) ?? "";
        if (gespeichert != "6" || nameJetzt != EchtSchreibtisch.ZellName(teil.Antwort, schluessel.z, schluessel.s))
        {
            Fund("Eingabe \"6\" nicht unter z" + schluessel.z + "s" + schluessel.s + " gespeichert (\"" + gespeichert + "\") oder Name nicht neu (\"" + nameJetzt + "\")");
        }

        // Nico 1a: schmal (Handy) wischt das Raster seitlich, die Spalte "Zeile" bleibt stehen, der Hinweis nennt die Spalten
        var fenster = TopLevel.GetTopLevel(ansicht) as Window;
        // Groesse vor dem Wechsel aus der Flaeche (Width/Height sind beim Start-Test oft nicht gesetzt = NaN)
        var (breite, hoehe) = (fenster?.ClientSize.Width ?? 0, fenster?.ClientSize.Height ?? 0);
        Messwerte.Notiere("Pruefung im echten Format Schreibtischtest: Flaeche " + breite.ToString("F0", CultureInfo.InvariantCulture) + " x "
            + hoehe.ToString("F0", CultureInfo.InvariantCulture) + ", Schrift " + Schrift.Faktor.ToString(CultureInfo.InvariantCulture));
        // das Fenster aendert die Groesse nicht sofort (X11): warten, bis die Flaeche die neue Breite hat (hoechstens 3 s);
        // nie groesser als der Bildschirm (Windows-CI 1024 x 768)
        async Task GroesseAsync(double b, double h)
        {
            if (fenster is null)
            {
                return;
            }
            var (hoechstB, hoechstH) = Hoechstgroesse(fenster);
            fenster.Width = Math.Min(b, hoechstB);
            fenster.Height = Math.Min(h, hoechstH);
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                await ansicht.BildFertigAsync();
            }
            while (Math.Abs(ansicht.Bounds.Width - fenster.Width) > 1 && uhr.ElapsedMilliseconds < 3000);
        }
        if (fenster is not null)
        {
            await GroesseAsync(384, 832);
            k.Probe(ab);
            await ansicht.BildFertigAsync();
            if (e.ProbeSchreibtisch is { } schmal)
            {
                var rollen = schmal.Rollen;
                if (rollen.Extent.Width <= rollen.Viewport.Width + 1 || schmal.ProbeStand.Wischen.Length == 0)
                {
                    Fund("Handy: Raster passt ganz oder kein Hinweis \"→ wischen\" (" + rollen.Extent.Width + " / " + rollen.Viewport.Width + ")");
                }
                rollen.Offset = new Vector(rollen.Extent.Width - rollen.Viewport.Width, 0);
                await ansicht.BildFertigAsync();
                await ansicht.BildFertigAsync();
                var fest = schmal.GetVisualDescendants().OfType<Border>().Where(b => b.ZIndex == 2).ToList();
                var mit = fest.Count > 0 && fest.All(b => b.RenderTransform is TranslateTransform t && Math.Abs(t.X - rollen.Offset.X) < 0.5);
                if (!mit)
                {
                    Fund("Handy: Spalte \"Zeile\" bleibt beim Wischen nicht stehen");
                }
                Messwerte.Notiere("Pruefung im echten Format Schreibtischtest Handy: " + schmal.ProbeStand.Wischen + ", feste Spalte " + (mit ? "steht" : "wandert"));
            }
            // "Groß öffnen" schmal = ganzer Bildschirm
            k.Probe(ab + 7);
            await ansicht.BildFertigAsync();
            if (AnlageGross.Offen is null || !AnlageGross.GanzerBildschirm)
            {
                Fund("Handy: \"Groß öffnen\" nicht als ganzer Bildschirm");
            }
            AnlageGross.Schliessen();
            await GroesseAsync(breite, hoehe);
        }

        // Ausgabe: Eingabezeilen, Vorlesen per Maus, Finger und Screenreader
        foreach (var art in new[] { PointerType.Mouse, PointerType.Touch })
        {
            k.Probe(ab + 3);
            await ansicht.BildFertigAsync();
            var name = art == PointerType.Mouse ? "Maus" : "Finger";
            if (!await TippenAsync(ansicht, e.ProbeInhalt, EchtTexte.Vorlesen, art) || Benannt(e.ProbeInhalt!, EchtTexte.VorlesenAus) is null)
            {
                Fund(name + ": \"" + EchtTexte.Vorlesen + "\" zeigt den Text nicht" + TrefferZuletzt);
            }
        }
        var texte = e.ProbeInhalt?.GetVisualDescendants().OfType<TextBlock>().Where(x => x.IsEffectivelyVisible).Select(x => x.Text ?? "").ToList() ?? [];
        if (!texte.Contains("Zeile 1, Eingabe: C:\\> ipconfig") || !texte.Contains("Zeile 2: Ethernet-Adapter Ethernet:"))
        {
            Fund("Ausgabe: Vorlesetext fehlt (Zeile 1 mit \"Eingabe\")");
        }
        var fett = e.ProbeInhalt?.GetVisualDescendants().OfType<TextBlock>().Count(x => x.Text == "C:\\> ipconfig" && x.FontWeight == FontWeight.Bold) ?? 0;
        if (fett == 0)
        {
            Fund("Ausgabe: Eingabezeile nicht fett");
        }

        // CSV: Umschalter "Als Tabelle" per Screenreader (Aufruf), Hinweis und Tabelle
        k.Probe(ab + 4);
        await ansicht.BildFertigAsync();
        if (Benannt(e.ProbeInhalt!, EchtTexte.AlsTabelle) is not { } tabelle
            || ControlAutomationPeer.CreatePeerForElement(tabelle).GetProvider<IInvokeProvider>() is not { } aufruf)
        {
            Fund("CSV: Knopf \"" + EchtTexte.AlsTabelle + "\" fehlt oder nicht aufrufbar");
        }
        else
        {
            await messpunkt("Pruefung im echten Format Datenauszug als Tabelle umschalten (bis zum Bild)", () => aufruf.Invoke(), () =>
            {
                if (Benannt(e.ProbeInhalt!, EchtTexte.WieImBeleg) is { } beleg)
                {
                    ControlAutomationPeer.CreatePeerForElement(beleg).GetProvider<IInvokeProvider>()?.Invoke();
                }
            }, true);
            aufruf.Invoke();
            await ansicht.BildFertigAsync();
            if (e.ProbeInhalt?.GetVisualDescendants().OfType<CsvTabelle>().FirstOrDefault(x => x.IsEffectivelyVisible) is null)
            {
                Fund("CSV: \"" + EchtTexte.AlsTabelle + "\" zeigt keine Tabelle");
            }
        }
        // Nico 3a: "Groß öffnen" nur bei Ausgabe und Datenauszug, nie bei Code
        k.Probe(ab + 1);
        await ansicht.BildFertigAsync();
        if (Benannt(e.ProbeInhalt!, EchtTexte.GrossOeffnenName) is not null)
        {
            Fund("Code-Anlage hat \"" + EchtTexte.GrossOeffnenName + "\" (nur Ausgabe und Datenauszug)");
        }
        // PC: Fenster, Escape schliesst (in PC-Groesse, danach wieder wie vorher)
        await GroesseAsync(PcBreite, PcHoehe);
        k.Probe(ab + 4);
        await ansicht.BildFertigAsync();
        if (Benannt(e.ProbeInhalt!, EchtTexte.GrossOeffnenName) is not { } gross)
        {
            Fund("CSV: Knopf \"" + EchtTexte.GrossOeffnenName + "\" fehlt");
        }
        else
        {
            await messpunkt("Pruefung im echten Format Groß öffnen (bis zum Bild)", () => ControlAutomationPeer.CreatePeerForElement(gross)
                .GetProvider<IInvokeProvider>()?.Invoke(), AnlageGross.Schliessen, true);
            gross.BringIntoView();
            await ansicht.BildFertigAsync();
            var getippt = await TippenAsync(ansicht, gross, PointerType.Mouse);
            // Fenster am PC; schmaler als 720 x Schriftfaktor (z. B. grosse Schrift) ganzer Bildschirm wie am Handy
            var ganzSoll = ansicht.Bounds.Width < 720 * Schrift.Faktor;
            if (AnlageGross.Offen is not { } offen || AnlageGross.GanzerBildschirm != ganzSoll)
            {
                Fund("PC: \"Groß öffnen\" oeffnet kein " + (ganzSoll ? "Blatt ueber den ganzen Bildschirm" : "Fenster") + " (getippt " + getippt
                    + ", offen " + (AnlageGross.Offen is not null) + ", ganzer Bildschirm " + AnlageGross.GanzerBildschirm + ", Flaeche "
                    + ansicht.Bounds.Width.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + ", Schrift "
                    + Schrift.Faktor.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")" + TrefferZuletzt);
            }
            else
            {
                offen.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = offen });
                await ansicht.BildFertigAsync();
                if (AnlageGross.Offen is not null)
                {
                    Fund("PC: Escape schliesst das grosse Blatt nicht");
                }
            }
        }
        AnlageGross.Schliessen();
        await GroesseAsync(breite, hoehe);

        // Bewertung: gruen und rot, richtiger Wert darunter, Legende
        k.Probe(ab + 2);
        await ansicht.BildFertigAsync();
        var bewertet = e.ProbeInhalt?.GetVisualDescendants().OfType<TextBlock>().Where(x => x.IsEffectivelyVisible).Select(x => x.Text ?? "").ToList() ?? [];
        var richtig = bewertet.Count(t => t.StartsWith("richtig: ", StringComparison.Ordinal));
        if (richtig < 2 || !bewertet.Any(t => t.StartsWith("grün = richtig", StringComparison.Ordinal)) || !bewertet.Contains(EchtTexte.Musterloesung))
        {
            Fund("Bewertung: " + richtig + " rote Kaestchen mit richtigem Wert (erwartet 2), Legende oder Musterlösung fehlt");
        }
        Messwerte.Notiere("Pruefung im echten Format Bewertung Schreibtischtest: " + richtig + " rot mit richtigem Wert");

        // Weisung P12-448 Funde A und B (PC 1280 wie in der Weisung, nicht PcBreite): PC 1280 und Handy 384 x 832 bei Schrift 100 und 200 %, dazu die Gegenprobe wie bis
        // Lauf 1289 (ohne Rollen zum Kaestchen, ohne ganze Spalten) = rot
        var faktorVorher = Schrift.Faktor;
        var gegen = (A: 0, B: 0);
        try
        {
            foreach (var (geraet, gb, gh) in new[] { ("PC", 1280d, (double)PcHoehe), ("Handy", 384d, 832d) })
            {
                foreach (var f in new[] { 1.0, 2.0 })
                {
                    Schrift.Anwenden(f);
                    await GroesseAsync(gb, gh);
                    var wo = geraet + " " + (f * 100).ToString("F0", CultureInfo.InvariantCulture) + " % ("
                        + ansicht.Bounds.Width.ToString("F0", CultureInfo.InvariantCulture) + " breit)";
                    var (a, b) = await SchreibtischSichtbarAsync(ansicht, k, e, ab, wo, Fund);
                    Messwerte.Notiere("Pruefung im echten Format Schreibtischtest sichtbar " + wo + ": " + a + " Kaestchen geprueft, " + b
                        + " Texte der Bewertung geprueft");
                }
            }
            // Gegenprobe: dieselbe Pruefung wie bis Lauf 1289 muss Funde liefern
            EchtSchreibtischTeil.WieBisherFuerTests.Value = true;
            foreach (var (gb, gh) in new[] { (1280d, (double)PcHoehe), (384d, 832d) })
            {
                foreach (var f in new[] { 1.0, 2.0 })
                {
                    Schrift.Anwenden(f);
                    await GroesseAsync(gb, gh);
                    var zaehlA = 0;
                    var zaehlB = 0;
                    await SchreibtischSichtbarAsync(ansicht, k, e, ab, "Gegenprobe", t =>
                    {
                        if (t.StartsWith("Fund A", StringComparison.Ordinal))
                        {
                            zaehlA++;
                        }
                        else if (t.StartsWith("Fund B", StringComparison.Ordinal))
                        {
                            zaehlB++;
                        }
                    });
                    gegen = (gegen.A + zaehlA, gegen.B + zaehlB);
                }
            }
        }
        finally
        {
            EchtSchreibtischTeil.WieBisherFuerTests.Value = false;
            Schrift.Anwenden(faktorVorher);
            await GroesseAsync(breite, hoehe);
        }
        Messwerte.Notiere("Pruefung im echten Format Schreibtischtest Gegenprobe wie bis Lauf 1289: Fund A " + gegen.A + ", Fund B " + gegen.B);
        if (gegen.A == 0 || gegen.B == 0)
        {
            Fund("Gegenprobe ohne Rollen und ohne ganze Spalten findet nichts (Fund A " + gegen.A + ", Fund B " + gegen.B + "): Pruefung zu schwach");
        }
        return funde;
    }

    /// <summary>
    /// Weisung P12-448. Fund A: jedes Kaestchen mit Fokus (Oeffnen mit gewaehltem Schritt, Eingabetaste, Tab, Antippen) liegt ganz
    /// im sichtbaren Bereich des Rasters und nicht unter der festen Spalte „Zeile“, mit tuerkisem Rahmen. Fund B: in der Bewertung
    /// steht kein Text halb am Rand oder halb unter der festen Spalte (am Anfang, an jedem Spaltenanfang und nach freiem Wischen),
    /// jedes „richtig: …“ ist an einer Stelle ganz zu sehen. Meldet Funde mit „Fund A“/„Fund B“ vorn.
    /// </summary>
    private static async Task<(int A, int B)> SchreibtischSichtbarAsync(MainView ansicht, KlausurTeil k, EchtFormatTeil e, int ab, string wo,
        Action<string> fund)
    {
        static (double Fest, double Sicht) Rand(EchtSchreibtischTeil r) =>
            (r.Rollen.Content is Grid g && g.ColumnDefinitions.Count > 0 ? g.ColumnDefinitions[0].ActualWidth : 0, r.Rollen.Viewport.Width);
        var geprueftA = 0;
        k.Probe(ab);
        await ansicht.BildFertigAsync();
        await ansicht.BildFertigAsync();
        if (e.ProbeSchreibtisch is not { } raster)
        {
            fund("Fund A " + wo + ": kein Raster im Lauf");
            return (0, 0);
        }
        var felder = raster.Felder;
        async Task PruefeA(TextBox box, string wie)
        {
            await ansicht.BildFertigAsync();
            await ansicht.BildFertigAsync();
            geprueftA++;
            var fokus = TopLevel.GetTopLevel(ansicht)?.FocusManager?.GetFocusedElement();
            var ring = (box.Parent as Panel)?.Children.OfType<Border>().FirstOrDefault();
            var ziel = ring is { IsVisible: true } ? (Control)ring : box;
            var (fest, sicht) = Rand(raster);
            var p = ziel.TranslatePoint(default, raster.Rollen) ?? new Point(-1, -1);
            var (l, r) = (p.X, p.X + ziel.Bounds.Width);
            var nr = felder.ToList().IndexOf(box) + 1;
            if (!ReferenceEquals(fokus, box))
            {
                fund("Fund A " + wo + ": " + wie + " gibt Kaestchen " + nr + " keinen Fokus");
            }
            else if (l < fest - 0.5 || r > sicht + 0.5)
            {
                fund("Fund A " + wo + ": " + wie + ", Kaestchen " + nr + " nicht ganz sichtbar (links " + l.ToString("F1", CultureInfo.InvariantCulture)
                    + ", rechts " + r.ToString("F1", CultureInfo.InvariantCulture) + ", feste Spalte bis " + fest.ToString("F1", CultureInfo.InvariantCulture)
                    + ", sichtbar bis " + sicht.ToString("F1", CultureInfo.InvariantCulture) + ")");
            }
            else if (ring is not { IsVisible: true } || ring.BorderBrush is not ISolidColorBrush rahmen
                || Application.Current?.TryGetResource("Pinsel.cyan", null, out var tuerkis) != true || tuerkis is not ISolidColorBrush soll
                || rahmen.Color != soll.Color)
            {
                fund("Fund A " + wo + ": Kaestchen " + nr + " mit Fokus ohne tuerkisen Rahmen");
            }
        }
        // Oeffnen: der Schritt mit der leeren Zelle ist gewaehlt und hat den Fokus (Bild 01 des Entwurfs)
        await PruefeA(felder[EchtFormatTeil.ProbeLeer], "Oeffnen mit gewaehltem Schritt");
        // Eingabetaste vom ersten bis zum letzten Kaestchen
        felder[0].Focus(NavigationMethod.Pointer);
        await PruefeA(felder[0], "Antippen");
        for (var i = 1; i < felder.Count; i++)
        {
            felder[i - 1].RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = felder[i - 1] });
            await PruefeA(felder[i], "Eingabetaste");
        }
        // Tab (wie an der Tastatur) und Antippen rueckwaerts
        for (var i = felder.Count - 1; i >= 0; i--)
        {
            felder[i].Focus(i % 2 == 0 ? NavigationMethod.Tab : NavigationMethod.Pointer);
            await PruefeA(felder[i], i % 2 == 0 ? "Tab" : "Antippen");
        }

        // Fund B: Bewertung
        k.Probe(ab + 2);
        await ansicht.BildFertigAsync();
        await ansicht.BildFertigAsync();
        var bewertet = e.ProbeInhalt?.GetVisualDescendants().OfType<EchtSchreibtischTeil>().FirstOrDefault(x => x.IsEffectivelyVisible);
        if (bewertet is null)
        {
            fund("Fund B " + wo + ": kein Raster in der Bewertung");
            return (geprueftA, 0);
        }
        var texte = bewertet.Rollen.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && !t.GetVisualAncestors().OfType<Border>().Any(b => b.ZIndex == 2)).ToList();
        var richtigAlle = texte.Where(t => (t.Text ?? "").StartsWith("richtig: ", StringComparison.Ordinal)).ToList();
        var ganzGesehen = new HashSet<TextBlock>();
        var geprueftB = 0;
        void PruefeB(string stelle)
        {
            var (fest, sicht) = Rand(bewertet);
            foreach (var t in texte)
            {
                geprueftB++;
                var p = t.TranslatePoint(default, bewertet.Rollen) ?? new Point(-1, -1);
                var (l, r) = (p.X, p.X + t.Bounds.Width);
                var halb = (l < sicht - 0.5 && r > sicht + 0.5) || (l < fest - 0.5 && r > fest + 0.5) || t.DesiredSize.Width > t.Bounds.Width + 0.5;
                if (halb)
                {
                    fund("Fund B " + wo + ": \"" + t.Text + "\" " + stelle + " abgeschnitten (links " + l.ToString("F1", CultureInfo.InvariantCulture)
                        + ", rechts " + r.ToString("F1", CultureInfo.InvariantCulture) + ", feste Spalte bis " + fest.ToString("F1", CultureInfo.InvariantCulture)
                        + ", sichtbar bis " + sicht.ToString("F1", CultureInfo.InvariantCulture) + ")");
                }
                else if (l >= fest - 0.5 && r <= sicht + 0.5)
                {
                    ganzGesehen.Add(t);
                }
            }
        }
        PruefeB("am Anfang");
        // an jedem Spaltenanfang (wie nach dem Einrasten) und nach freiem Wischen auf die Mitte einer Spalte
        var g = (Grid)bewertet.Rollen.Content!;
        var x = 0.0;
        for (var c = 0; c < g.ColumnDefinitions.Count; c++)
        {
            var spaltenBreite = g.ColumnDefinitions[c].ActualWidth;
            if (c >= 1)
            {
                var festB = g.ColumnDefinitions[0].ActualWidth;
                foreach (var (versatz, stelle) in new[] { (x - festB, "an Spalte " + c), (x - festB + spaltenBreite / 2, "nach Wischen in Spalte " + c) })
                {
                    if (versatz < 0 || versatz > bewertet.Rollen.Extent.Width - bewertet.Rollen.Viewport.Width + 0.5)
                    {
                        continue;
                    }
                    bewertet.Rollen.Offset = new Vector(versatz, 0);
                    // Einrasten nach dem Wischen abwarten (200 ms Ruhe)
                    await Task.Delay(400);
                    await ansicht.BildFertigAsync();
                    await ansicht.BildFertigAsync();
                    PruefeB(stelle);
                }
            }
            x += spaltenBreite;
        }
        var nie = richtigAlle.Where(t => !ganzGesehen.Contains(t)).Select(t => t.Text).ToList();
        if (nie.Count > 0)
        {
            fund("Fund B " + wo + ": nie ganz zu sehen: " + string.Join(", ", nie));
        }
        if (richtigAlle.Count < 2)
        {
            fund("Fund B " + wo + ": nur " + richtigAlle.Count + " \"richtig: …\" in der Bewertung (erwartet 2)");
        }
        return (geprueftA, geprueftB);
    }
}
