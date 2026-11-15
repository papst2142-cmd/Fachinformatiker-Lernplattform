using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// A 0.78.0 "Zeichen-Baukasten" (Weisung P12-266): Messpunkte der Baukasten-Ansichten und Bedienproben auf allen Wegen:
/// Maus und Finger (Baustein setzen, auf ein freies Feld tippen = verschieben, Baustein antippen = waehlen, "Verbinden durch
/// Antippen" und Ziel antippen), Tastatur (Eingabetaste auf "Ende setzen", "Nach rechts", Pfeiltaste in "Verbinden mit",
/// "Verbinden") und Screenreader (Knopf der Liste "Deine Zeichnung" ueber seine Automation aufrufen, Namen mit
/// "verbunden mit"/"weiter zu"). In der Bewertung zaehlt das Programm (gruene und rote Zeilen).
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> EchtBaukastenAsync(MainView ansicht, KlausurTeil k, EchtFormatTeil e,
        Func<string, Action, Action?, bool, Task> messpunkt)
    {
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("  FEHLER: Pruefung im echten Format (Baukasten): " + text);
            funde++;
        }
        var ab = KlausurTeil.EchtAb + 1 + EchtFormatTeil.BaukastenAb;
        await messpunkt("Pruefung im echten Format Baukasten ER im Lauf (bis zum Bild)", () => k.Probe(ab), null, true);
        await messpunkt("Pruefung im echten Format Baukasten UML (bis zum Bild)", () => k.Probe(ab + 1), null, true);
        await messpunkt("Pruefung im echten Format Bewertung mit Zaehlung (bis zum Bild)", () => k.Probe(ab + 2), null, true);

        // Lauf: der Baukasten ist offen (Zeichnung vorhanden), die Vorgabe steht fest darin
        k.Probe(ab);
        await ansicht.BildFertigAsync();
        if (e.ProbeBaukasten is not { } er || !er.IsEffectivelyVisible || !er.ProbeBau.IstFest("pk") || er.ProbeBau.IstFest(er.ProbeGewaehlt ?? "pk"))
        {
            Fund("ER im Lauf: Baukasten nicht offen, Vorgabe nicht fest oder nichts Eigenes gewaehlt");
        }

        // Maus und Finger
        foreach (var art in new[] { PointerType.Mouse, PointerType.Touch })
        {
            var name = art == PointerType.Mouse ? "Maus" : "Finger";
            k.Probe(ab + 1);
            await ansicht.BildFertigAsync();
            if (e.ProbeUmlBaukasten is not { } b)
            {
                Fund(name + ": kein Baukasten");
                continue;
            }
            var bau = b.ProbeBau;
            var vorher = bau.Diagramm.Elemente.Count;
            await TippenAsync(ansicht, b.ProbeSetzen("aktion"), art);
            var neu = b.ProbeGewaehlt;
            var schritte = new List<string>();
            if (bau.Diagramm.Elemente.Count != vorher + 1 || bau.Element(neu) is not { Typ: "aktion" } aktion)
            {
                Fund(name + ": \"Aktion setzen\" setzt keine Aktion");
                continue;
            }
            schritte.Add("gesetzt " + EchtBaukasten.StelleText(aktion));
            // auf ein freies Feld tippen: der gewaehlte Baustein zieht dorthin
            var frei = Enumerable.Range(0, 4).SelectMany(y => Enumerable.Range(0, 4).Select(x => (X: x, Y: aktion.Y + y)))
                .First(f => f.Y < EchtZeichnung.RasterHoehe && bau.An(f.X, f.Y) is null && b.ProbeBild.FeldMitte(f.X, f.Y) is not null);
            await FeldTippenAsync(ansicht, b, frei.X, frei.Y, art);
            if (bau.Element(neu) is not { } gezogen || (gezogen.X, gezogen.Y) != frei)
            {
                Fund(name + ": Antippen eines freien Felds verschiebt den Baustein nicht nach " + frei);
                continue;
            }
            schritte.Add("verschoben " + EchtBaukasten.StelleText(gezogen));
            // Baustein der Vorgabe antippen: gewaehlt
            var a1 = bau.Element("a1")!;
            await FeldTippenAsync(ansicht, b, a1.X, a1.Y, art);
            if (b.ProbeGewaehlt != "a1")
            {
                Fund(name + ": Antippen eines Bausteins waehlt ihn nicht (" + b.ProbeGewaehlt + ")");
                continue;
            }
            schritte.Add("gewaehlt a1");
            // Verbinden durch Antippen
            var verbindungen = bau.Diagramm.Verbindungen.Count;
            await TippenAsync(ansicht, b.ProbeKarte, EchtTexte.VerbindenTippen, art);
            await FeldTippenAsync(ansicht, b, gezogen.X, gezogen.Y, art);
            if (bau.Diagramm.Verbindungen.Count != verbindungen + 1 || !bau.Diagramm.Verbindungen.Any(v => v.Von == "a1" && v.Nach == neu))
            {
                Fund(name + ": \"Verbinden durch Antippen\" verbindet nicht");
                continue;
            }
            schritte.Add("verbunden");
            Messwerte.Notiere("Pruefung im echten Format Bedienprobe Baukasten " + name + ": " + string.Join(", ", schritte));
        }

        // Tastatur und Screenreader
        k.Probe(ab + 1);
        await ansicht.BildFertigAsync();
        if (e.ProbeUmlBaukasten is { } t)
        {
            var bau = t.ProbeBau;
            var schritte = new List<string>();
            var liste = t.ProbeListe.Children.OfType<Button>().ToList();
            var namen = liste.Select(x => Avalonia.Automation.AutomationProperties.GetName(x) ?? "").ToList();
            if (liste.Count != bau.Diagramm.Elemente.Count || namen.Any(n => !n.Contains("Spalte", StringComparison.Ordinal))
                || !namen.Any(n => n.Contains(EchtTexte.WeiterZu, StringComparison.Ordinal)) || !namen.Any(n => n.Contains(EchtTexte.KommtVon, StringComparison.Ordinal)))
            {
                Fund("Screenreader: Liste \"Deine Zeichnung\" nennt nicht jeden Baustein mit Stelle und Verbindungen");
            }
            // Screenreader: Knopf der Liste ueber seine Automation aufrufen (wie NVDA "Ausfuehren")
            var ticket = bau.Diagramm.Elemente.First(x => x.Text == "Ticket anlegen").Id;
            var knopf = liste.FirstOrDefault(x => (Avalonia.Automation.AutomationProperties.GetName(x) ?? "").Contains("Ticket anlegen", StringComparison.Ordinal)
                && (Avalonia.Automation.AutomationProperties.GetName(x) ?? "").StartsWith("Aktion", StringComparison.Ordinal));
            if (knopf is null || ControlAutomationPeer.CreatePeerForElement(knopf).GetProvider<IInvokeProvider>() is not { } aufruf)
            {
                Fund("Screenreader: Listenknopf nicht aufrufbar");
            }
            else
            {
                aufruf.Invoke();
                await ansicht.BildFertigAsync();
                if (t.ProbeGewaehlt != ticket)
                {
                    Fund("Screenreader: Aufruf des Listenknopfs waehlt den Baustein nicht");
                }
                schritte.Add("Liste gewaehlt");
            }
            // Tastatur: "Ende setzen" mit der Eingabetaste
            var vorher = bau.Diagramm.Elemente.Count;
            await TasteAsync(ansicht, t.ProbeSetzen("ende"), Key.Enter);
            var ende = t.ProbeGewaehlt;
            if (bau.Diagramm.Elemente.Count != vorher + 1 || bau.Element(ende)?.Typ != "ende")
            {
                Fund("Tastatur: Eingabetaste auf \"Ende setzen\" setzt kein Ende");
            }
            else
            {
                schritte.Add("Ende gesetzt " + EchtBaukasten.StelleText(bau.Element(ende)!));
                // "Nach rechts" mit der Eingabetaste
                var x0 = bau.Element(ende)!.X;
                var rechts = Benannt(t.ProbeKarte, EchtTexte.NachRechts);
                if (rechts is null || !rechts.IsEnabled)
                {
                    Fund("Tastatur: \"Nach rechts\" fehlt oder ist gesperrt");
                }
                else
                {
                    await TasteAsync(ansicht, rechts, Key.Enter);
                    if (bau.Element(ende)!.X != x0 + 1)
                    {
                        Fund("Tastatur: \"Nach rechts\" verschiebt nicht");
                    }
                    schritte.Add("nach rechts " + EchtBaukasten.StelleText(bau.Element(ende)!));
                }
                // Ticket waehlen, in "Verbinden mit" mit der Pfeiltaste das Ende suchen, "Verbinden" mit der Eingabetaste
                t.ProbeWaehlen(ticket);
                await ansicht.BildFertigAsync();
                var auswahl = t.ProbeKarte.Children.OfType<ComboBox>()
                    .FirstOrDefault(c => Avalonia.Automation.AutomationProperties.GetName(c) == EchtTexte.VerbindenMit);
                var gefunden = false;
                for (var i = 0; auswahl is not null && i < 12 && !gefunden; i++)
                {
                    gefunden = (auswahl.SelectedItem as string ?? "").StartsWith("Ende", StringComparison.Ordinal);
                    if (!gefunden)
                    {
                        auswahl.Focus();
                        auswahl.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down, Source = auswahl });
                        await ansicht.BildFertigAsync();
                    }
                }
                var verbindungen = bau.Diagramm.Verbindungen.Count;
                if (!gefunden || Benannt(t.ProbeKarte, EchtTexte.Verbinden) is not { } verbinden)
                {
                    Fund("Tastatur: in \"Verbinden mit\" laesst sich das Ende nicht mit der Pfeiltaste waehlen");
                }
                else
                {
                    await TasteAsync(ansicht, verbinden, Key.Enter);
                    if (bau.Diagramm.Verbindungen.Count != verbindungen + 1 || !bau.Diagramm.Verbindungen.Any(v => v.Von == ticket && v.Nach == ende))
                    {
                        Fund("Tastatur: \"Verbinden\" verbindet nicht");
                    }
                    schritte.Add("verbunden mit Ende");
                }
            }
            Messwerte.Notiere("Pruefung im echten Format Bedienprobe Baukasten Tastatur und Screenreader: " + string.Join(", ", schritte));
        }
        else
        {
            Fund("Tastatur: kein Baukasten");
        }

        // Bewertung: eigene Zeichnung als Bild und die Zaehlung des Programms (gruen und rot)
        k.Probe(ab + 2);
        await ansicht.BildFertigAsync();
        var zeilen = e.ProbeInhalt?.GetVisualDescendants().OfType<LernText>().Select(x => x.Text ?? "").ToList() ?? [];
        var gruen = zeilen.Count(z => z.StartsWith("✓ ", StringComparison.Ordinal));
        var rot = zeilen.Count(z => z.StartsWith("✗ ", StringComparison.Ordinal));
        if (gruen == 0 || rot == 0 || !zeilen.Contains(EchtTexte.ZaehlHinweis))
        {
            Fund("Bewertung: Zaehlung fehlt (gruen " + gruen + ", rot " + rot + ")");
        }
        Messwerte.Notiere("Pruefung im echten Format Bewertung mit Zaehlung: " + gruen + " gruen, " + rot + " rot");
        return funde;
    }

    private static Button? Benannt(Control bereich, string name) => bereich.GetVisualDescendants().OfType<Button>()
        .FirstOrDefault(b => Avalonia.Automation.AutomationProperties.GetName(b) == name);

    /// <summary>Feld der Zeichenflaeche antippen: erst sichtbar rollen (waagrecht im Bildkasten, senkrecht auf der Seite).</summary>
    private static async Task FeldTippenAsync(MainView ansicht, EchtBaukastenTeil b, int x, int y, PointerType art)
    {
        var bild = b.ProbeBild;
        if (bild.FeldMitte(x, y) is not { } mitte)
        {
            return;
        }
        var rollen = b.ProbeKasten.Rollen;
        if (rollen.Extent.Width > rollen.Viewport.Width)
        {
            rollen.Offset = new Vector(Math.Clamp(mitte.X - rollen.Viewport.Width / 2, 0, rollen.Extent.Width - rollen.Viewport.Width), rollen.Offset.Y);
        }
        bild.BringIntoView(new Rect(mitte.X - 20, mitte.Y - 20, 40, 40));
        await ansicht.BildFertigAsync();
        if (TopLevel.GetTopLevel(bild) is not { } wurzel)
        {
            return;
        }
        var punkt = bild.TranslatePoint(mitte, wurzel) ?? mitte;
        var zeiger = new Pointer(Pointer.GetNextFreeId(), art, true);
        var druck = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        var los = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
        bild.RaiseEvent(new PointerPressedEventArgs(bild, zeiger, wurzel, punkt, 0, druck, KeyModifiers.None));
        bild.RaiseEvent(new PointerReleasedEventArgs(bild, zeiger, wurzel, punkt, 0, los, KeyModifiers.None, MouseButton.Left));
        await ansicht.BildFertigAsync();
    }

    /// <summary>Fokus auf das Element und eine Taste druecken und loslassen (wie an der Tastatur).</summary>
    private static async Task TasteAsync(MainView ansicht, Control ziel, Key taste)
    {
        ziel.BringIntoView();
        ziel.Focus(NavigationMethod.Tab);
        await ansicht.BildFertigAsync();
        ziel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = taste, Source = ziel });
        ziel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = taste, Source = ziel });
        await ansicht.BildFertigAsync();
    }
}
