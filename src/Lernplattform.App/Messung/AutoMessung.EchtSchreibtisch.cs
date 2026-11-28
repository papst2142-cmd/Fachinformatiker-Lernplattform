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
        return funde;
    }
}
