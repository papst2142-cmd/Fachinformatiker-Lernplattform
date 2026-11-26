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
/// A 1.12.0 "Pruefung echt W2" (Weisung P12-390, Plan P12-250): Struktogramm im Baukasten der AE-Saetze. Der Start-Test misst
/// die drei Ansichten ab <see cref="EchtFormatTeil.W2Ab"/> (erstes Oeffnen streng 400 ms), prueft ihre Bilder (gezeichnet, mit
/// Namen), dass beim gewaehlten Block alle Bedienelemente da sind (Textfeld, "Neuer Block kommt …", Nach oben, Nach unten,
/// Heraus, Entfernen; Auflage B), dass der Blocktext unveraendert gezeigt wird (Auflage C) und bedient ihn auf allen Wegen:
/// Maus und Finger (Knopf "Anweisung", Antippen eines Zweigs im Bild, "Heraus"), Tastatur (Pfeil runter in der Liste waehlt
/// die naechste Zeile, Eingabetaste oeffnet das Textfeld) und Screenreader (Listenknopf per Automation aufrufen). In der
/// Bewertung stehen eigene Zeichnung und Musterbild (Auflage F) und die Zaehlung gruen und rot.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> EchtW2Async(MainView ansicht, KlausurTeil k, EchtFormatTeil e,
        Func<string, Action, Action?, bool, Task> messpunkt)
    {
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("  FEHLER: Pruefung im echten Format (Struktogramm): " + text);
            funde++;
        }
        var ab = KlausurTeil.EchtAb + 1 + EchtFormatTeil.W2Ab;
        await messpunkt("Pruefung im echten Format Struktogramm im Lauf (erstes Oeffnen, bis zum Bild)", () => k.Probe(ab), null, false);
        await messpunkt("Pruefung im echten Format Struktogramm alle Bausteine (bis zum Bild)", () => k.Probe(ab + 1), null, true);
        await messpunkt("Pruefung im echten Format Bewertung Struktogramm (bis zum Bild)", () => k.Probe(ab + 2), null, true);

        // Bilder: gezeichnet, mit Namen; im Lauf ist der Baukasten offen, etwas gewaehlt und die Karte vollstaendig
        for (var art = ab; art < ab + 3; art++)
        {
            k.Probe(art);
            await ansicht.BildFertigAsync();
            var kaesten = e.ProbeInhalt?.GetVisualDescendants().OfType<Bildkasten>().Where(x => x.IsEffectivelyVisible).ToList() ?? [];
            if (!kaesten.Any(x => x.Bild is StruktogrammBild))
            {
                Fund(KlausurTeil.ProbeNamen[art] + ": kein Struktogramm gezeichnet");
            }
            foreach (var kasten in kaesten)
            {
                var (gezeichnet, flaeche) = BildPixel(kasten.Bild);
                if (gezeichnet * 100 < flaeche || string.IsNullOrEmpty(Avalonia.Automation.AutomationProperties.GetName(kasten.Bild)))
                {
                    Fund(KlausurTeil.ProbeNamen[art] + ": Bild leer (" + gezeichnet + " von " + flaeche + ") oder ohne Namen");
                }
            }
            if (art < ab + 2 && (e.ProbeStruktogrammTeil is not { } s || !s.IsEffectivelyVisible || s.ProbeGewaehlt is null))
            {
                Fund(KlausurTeil.ProbeNamen[art] + ": Struktogramm-Baukasten nicht offen oder nichts gewaehlt");
            }
            Messwerte.Notiere("Pruefung im echten Format " + KlausurTeil.ProbeNamen[art] + ": " + kaesten.Count + " Bild(er)");
        }

        // Auflage B und C: beim gewaehlten Block alle Bedienelemente sichtbar, Text unveraendert
        k.Probe(ab);
        await ansicht.BildFertigAsync();
        if (e.ProbeStruktogrammTeil is { } t && t.ProbeBau.Block(t.ProbeGewaehlt) is { } block)
        {
            var feld = t.ProbeKarte.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(x => x.IsEffectivelyVisible);
            var auswahl = t.ProbeKarte.GetVisualDescendants().OfType<ComboBox>()
                .FirstOrDefault(x => x.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetName(x) == EchtTexte.NeuerBlockKommt);
            var fehlt = new[] { EchtTexte.NachOben, EchtTexte.NachUnten, EchtTexte.HerausName, EchtTexte.Entfernen }
                .Where(n => Benannt(t.ProbeKarte, n) is not { IsEffectivelyVisible: true }).ToList();
            if (feld is null || auswahl is null || fehlt.Count > 0)
            {
                Fund("Gewaehlter Block: Textfeld, \"" + EchtTexte.NeuerBlockKommt + "\" oder Knoepfe fehlen (" + string.Join(", ", fehlt) + ")");
            }
            if (feld?.Text != block.Text || block.Text != EchtFormatTeil.ProbeBedingung)
            {
                Fund("Blocktext nicht unveraendert: Feld \"" + feld?.Text + "\", Block \"" + block.Text + "\"");
            }
            Messwerte.Notiere("Pruefung im echten Format Struktogramm Karte: " + EchtStruktogramm.Kurz(block) + ", Ziel \"" + auswahl?.SelectedItem + "\"");
        }
        else
        {
            Fund("Lauf: keine Verzweigung gewaehlt");
        }

        // Maus und Finger: Knopf "Anweisung" (kommt ins gewaehlte Ziel), Zweig im Bild antippen, "Heraus"
        foreach (var zeiger in new[] { PointerType.Mouse, PointerType.Touch })
        {
            var name = zeiger == PointerType.Mouse ? "Maus" : "Finger";
            var schritte = new List<string>();
            k.Probe(ab);
            await ansicht.BildFertigAsync();
            if (e.ProbeStruktogrammTeil is not { } s || s.ProbeGewaehlt is not { } wenn)
            {
                Fund(name + ": kein Struktogramm-Baukasten");
                continue;
            }
            var bau = s.ProbeBau;
            var nein = wenn + ".1";
            var vorher = bau.Zweig(nein)!.Bloecke.Count;
            // der Nein-Zweig ist kuerzer als der Ja-Zweig: Antippen im Fuellraum waehlt den Zweig
            await StelleTippenAsync(ansicht, s, nein, zeiger);
            if (s.ProbeGewaehlt != nein)
            {
                Fund(name + ": Antippen des Nein-Zweigs waehlt ihn nicht (" + s.ProbeGewaehlt + ")");
                continue;
            }
            schritte.Add("Nein-Zweig gewaehlt");
            await TippenAsync(ansicht, s.ProbeSetzen(EchtStruktogramm.Anweisung), zeiger);
            if (bau.Zweig(nein)!.Bloecke.Count != vorher + 1 || s.ProbeGewaehlt != nein + "." + vorher.ToString(System.Globalization.CultureInfo.InvariantCulture))
            {
                Fund(name + ": \"Anweisung\" kommt nicht ans Ende des Nein-Zweigs");
                continue;
            }
            schritte.Add("Anweisung im Nein-Zweig");
            var neu = nein + "." + vorher.ToString(System.Globalization.CultureInfo.InvariantCulture);
            s.ProbeWaehlen(neu);
            await ansicht.BildFertigAsync();
            var zahlOben = bau.Bloecke.Count;
            if (!await TippenAsync(ansicht, s.ProbeKarte, EchtTexte.HerausName, zeiger) || !bau.IstBlock(s.ProbeGewaehlt)
                || s.ProbeGewaehlt!.Split('.').Length != neu.Split('.').Length - 2)
            {
                Fund(name + ": \"Heraus\" bringt den Block nicht eine Ebene nach aussen (" + s.ProbeGewaehlt + ")");
            }
            else
            {
                schritte.Add("Heraus nach " + s.ProbeGewaehlt + " (oben " + zahlOben + ")");
            }
            Messwerte.Notiere("Pruefung im echten Format Bedienprobe Struktogramm " + name + ": " + string.Join(", ", schritte));
        }

        // Tastatur und Screenreader: Pfeil runter in der Liste, Eingabetaste oeffnet das Textfeld, Aufruf per Automation
        k.Probe(ab);
        await ansicht.BildFertigAsync();
        if (e.ProbeStruktogrammTeil is { } l)
        {
            var liste = l.ProbeListe.Children.OfType<Button>().ToList();
            var zeilen = l.ProbeBau.Zeilen();
            if (liste.Count != zeilen.Count || liste.Count == 0)
            {
                Fund("Liste \"Deine Zeichnung\": " + liste.Count + " Knoepfe, erwartet " + zeilen.Count);
            }
            else
            {
                await TasteAsync(ansicht, liste[0], Key.Down);
                if (l.ProbeGewaehlt != zeilen[1].Pfad)
                {
                    Fund("Tastatur: Pfeil runter waehlt nicht die naechste Zeile (" + l.ProbeGewaehlt + ")");
                }
                var zweite = l.ProbeListe.Children.OfType<Button>().ElementAt(1);
                await TasteAsync(ansicht, zweite, Key.Enter);
                await ansicht.BildFertigAsync();
                if (TopLevel.GetTopLevel(ansicht)?.FocusManager?.GetFocusedElement() is not TextBox)
                {
                    Fund("Tastatur: Eingabetaste oeffnet das Textfeld nicht");
                }
                var namen = l.ProbeListe.Children.OfType<Button>().Select(x => Avalonia.Automation.AutomationProperties.GetName(x) ?? "").ToList();
                if (!namen.Any(n => n.Contains("im Rumpf von Zählschleife", StringComparison.Ordinal)) || !namen.Any(n => n.Contains("Nein-Zweig", StringComparison.Ordinal)))
                {
                    Fund("Screenreader: Liste nennt Ort und Zweige nicht");
                }
                var knopf = l.ProbeListe.Children.OfType<Button>().First();
                if (ControlAutomationPeer.CreatePeerForElement(knopf).GetProvider<IInvokeProvider>() is not { } aufruf)
                {
                    Fund("Screenreader: Listenknopf nicht aufrufbar");
                }
                else
                {
                    aufruf.Invoke();
                    await ansicht.BildFertigAsync();
                    if (l.ProbeGewaehlt != zeilen[0].Pfad)
                    {
                        Fund("Screenreader: Aufruf des Listenknopfs waehlt den Block nicht");
                    }
                }
                Messwerte.Notiere("Pruefung im echten Format Bedienprobe Struktogramm Tastatur und Screenreader: " + namen.Count + " Listenknoepfe");
            }
        }

        // Bewertung (Auflage F): eigene Zeichnung und Musterbild, Zaehlung gruen und rot
        k.Probe(ab + 2);
        await ansicht.BildFertigAsync();
        var texte = e.ProbeInhalt?.GetVisualDescendants().OfType<TextBlock>().Where(x => x.IsEffectivelyVisible).Select(x => x.Text ?? "").ToList() ?? [];
        var bilder = e.ProbeInhalt?.GetVisualDescendants().OfType<StruktogrammBild>().Count(x => x.IsEffectivelyVisible) ?? 0;
        var gruen = texte.Count(z => z.StartsWith("✓ ", StringComparison.Ordinal));
        var rot = texte.Count(z => z.StartsWith("✗ ", StringComparison.Ordinal));
        if (bilder < 2 || !texte.Contains(EchtTexte.EigeneZeichnungTitel) || !texte.Contains(EchtTexte.Musterbild) || gruen == 0 || rot == 0)
        {
            Fund("Bewertung: " + bilder + " Struktogramme, gruen " + gruen + ", rot " + rot + " (erwartet eigene Zeichnung und Musterbild, beides gruen und rot)");
        }
        Messwerte.Notiere("Pruefung im echten Format Bewertung Struktogramm: " + bilder + " Bilder, " + gruen + " gruen, " + rot + " rot");
        return funde;
    }

    /// <summary>Stelle im Struktogramm antippen: erst sichtbar rollen (waagrecht im Bildkasten, senkrecht auf der Seite).</summary>
    private static async Task StelleTippenAsync(MainView ansicht, EchtStruktogrammTeil s, string pfad, PointerType art)
    {
        var bild = s.ProbeBild;
        if (bild.Mitte(pfad) is not { } mitte)
        {
            return;
        }
        var rollen = s.ProbeKasten.Rollen;
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
}
