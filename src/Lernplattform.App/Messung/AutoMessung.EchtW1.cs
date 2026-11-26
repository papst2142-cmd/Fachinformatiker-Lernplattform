using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// A 1.8.0 "Pruefung echt W1" (Weisung P12-371): Klassen- und Use-Case-Diagramm im Baukasten der FIAE-Saetze. Der Start-Test misst
/// die vier Ansichten ab <see cref="EchtFormatTeil.W1Ab"/> (Grenze 400 ms), prueft ihre Bilder (gezeichnet, mit Namen) und bedient
/// sie auf allen Wegen: Maus und Finger ("Attribut hinzufügen" an der gewaehlten Klasse, "Klasse setzen", "Verbinden …" und
/// Ziel antippen = Assoziation; Use-Case: neuer Anwendungsfall innerhalb, neuer Akteur ausserhalb der Systemgrenze), Tastatur
/// ("Verbinden mit" und "Art der Beziehung" mit der Pfeiltaste, "Verbinden" mit der Eingabetaste = Aggregation in der gewaehlten
/// Richtung) und Screenreader (Liste "Deine Zeichnung" nennt Beziehungen mit Art und Multiplizitaet, Knopf per Automation
/// aufrufen). In der Bewertung zaehlt das Programm (gruene und rote Zeilen) und der Kopf nennt den FIAE-Bereich.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> EchtW1Async(MainView ansicht, KlausurTeil k, EchtFormatTeil e,
        Func<string, Action, Action?, bool, Task> messpunkt)
    {
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("  FEHLER: Pruefung im echten Format (Klasse/Use-Case): " + text);
            funde++;
        }
        var ab = KlausurTeil.EchtAb + 1 + EchtFormatTeil.W1Ab;
        await messpunkt("Pruefung im echten Format Klassendiagramm im Lauf (bis zum Bild)", () => k.Probe(ab), null, true);
        await messpunkt("Pruefung im echten Format Use-Case-Diagramm im Lauf (bis zum Bild)", () => k.Probe(ab + 1), null, true);
        await messpunkt("Pruefung im echten Format Bewertung Klassendiagramm (bis zum Bild)", () => k.Probe(ab + 2), null, true);
        await messpunkt("Pruefung im echten Format Bewertung Use-Case-Diagramm (bis zum Bild)", () => k.Probe(ab + 3), null, true);

        // Bilder: gezeichnet, mit Namen; im Lauf ist der Baukasten offen und der gewaehlte Baustein ein eigener
        for (var art = ab; art < ab + 4; art++)
        {
            k.Probe(art);
            await ansicht.BildFertigAsync();
            var kaesten = e.ProbeInhalt?.GetVisualDescendants().OfType<Bildkasten>().Where(x => x.IsEffectivelyVisible).ToList() ?? [];
            if (kaesten.Count == 0)
            {
                Fund(KlausurTeil.ProbeNamen[art] + ": kein gezeichnetes Bild");
            }
            foreach (var kasten in kaesten)
            {
                var (gezeichnet, flaeche) = BildPixel(kasten.Bild);
                if (gezeichnet * 100 < flaeche || string.IsNullOrEmpty(Avalonia.Automation.AutomationProperties.GetName(kasten.Bild)))
                {
                    Fund(KlausurTeil.ProbeNamen[art] + ": Bild leer (" + gezeichnet + " von " + flaeche + ") oder ohne Namen");
                }
            }
            if (art < ab + 2 && (e.ProbeBaukasten is not { } b || !b.IsEffectivelyVisible || b.ProbeGewaehlt is not { } g || b.ProbeBau.IstFest(g)))
            {
                Fund(KlausurTeil.ProbeNamen[art] + ": Baukasten nicht offen oder kein eigener Baustein gewaehlt");
            }
            Messwerte.Notiere("Pruefung im echten Format " + KlausurTeil.ProbeNamen[art] + ": " + kaesten.Count + " Bild(er)");
        }

        // Maus und Finger
        foreach (var zeiger in new[] { PointerType.Mouse, PointerType.Touch })
        {
            var name = zeiger == PointerType.Mouse ? "Maus" : "Finger";
            var schritte = new List<string>();
            k.Probe(ab);
            await ansicht.BildFertigAsync();
            if (e.ProbeBaukasten is not { } kl || kl.ProbeBau.Element(kl.ProbeGewaehlt) is not { Typ: "klasse" } erinnerung)
            {
                Fund(name + ": Klasse nicht gewaehlt");
                continue;
            }
            var bau = kl.ProbeBau;
            var vorher = erinnerung.AttributZeilen.Count;
            if (!await TippenAsync(ansicht, kl, EchtTexte.AttributNeu, zeiger) || bau.Element(erinnerung.Id)!.AttributZeilen.Count != vorher + 1)
            {
                Fund(name + ": \"" + EchtTexte.AttributNeu + "\" fuegt keine Zeile an");
            }
            schritte.Add("Attribut " + vorher + " -> " + bau.Element(erinnerung.Id)!.AttributZeilen.Count);
            var zahl = bau.Diagramm.Elemente.Count;
            await TippenAsync(ansicht, kl.ProbeSetzen("klasse"), zeiger);
            var neu = kl.ProbeGewaehlt;
            if (bau.Diagramm.Elemente.Count != zahl + 1 || bau.Element(neu) is not { Typ: "klasse" })
            {
                Fund(name + ": \"Klasse setzen\" setzt keine Klasse");
                continue;
            }
            schritte.Add("Klasse gesetzt");
            var tier = bau.Element("tier")!;
            var verbindungen = bau.Diagramm.Verbindungen.Count;
            if (!await TippenAsync(ansicht, kl, EchtTexte.VerbindenLeiste, zeiger))
            {
                Fund(name + ": \"" + EchtTexte.VerbindenLeiste + "\" fehlt");
                continue;
            }
            await FeldTippenAsync(ansicht, kl, tier.X, tier.Y, zeiger);
            if (bau.Diagramm.Verbindungen.Count != verbindungen + 1
                || !bau.Diagramm.Verbindungen.Any(v => v.Von == neu && v.Nach == "tier" && v.Typ == "assoziation"))
            {
                Fund(name + ": \"Verbinden …\" und Ziel antippen verbindet nicht als Assoziation");
                continue;
            }
            schritte.Add("Assoziation zu Tier");

            k.Probe(ab + 1);
            await ansicht.BildFertigAsync();
            if (e.ProbeBaukasten is not { } uc)
            {
                Fund(name + ": kein Use-Case-Baukasten");
                continue;
            }
            var grenze = uc.ProbeBau.Diagramm.Grenze!;
            foreach (var (typ, innen) in new[] { ("anwendungsfall", true), ("akteur", false) })
            {
                await TippenAsync(ansicht, uc.ProbeSetzen(typ), zeiger);
                if (uc.ProbeBau.Element(uc.ProbeGewaehlt) is not { } gesetzt || gesetzt.Typ != typ || grenze.Enthaelt(gesetzt.X, gesetzt.Y) != innen)
                {
                    Fund(name + ": \"" + EchtZeichnung.Baustein(typ) + " setzen\" setzt ihn nicht " + (innen ? "innerhalb" : "ausserhalb") + " der Systemgrenze");
                }
                else
                {
                    schritte.Add(EchtZeichnung.Baustein(typ) + " " + EchtBaukasten.StelleText(gesetzt));
                }
            }
            Messwerte.Notiere("Pruefung im echten Format Bedienprobe Klasse/Use-Case " + name + ": " + string.Join(", ", schritte));
        }

        // Tastatur: Erinnerung gewaehlt, "Verbinden mit" Tier, "Art der Beziehung" Aggregation, "Verbinden" mit der Eingabetaste
        k.Probe(ab);
        await ansicht.BildFertigAsync();
        if (e.ProbeBaukasten is { } t && t.ProbeGewaehlt is { } von)
        {
            var bau = t.ProbeBau;
            async Task<bool> Waehlen(string name, Func<string, bool> passt)
            {
                var auswahl = t.ProbeKarte.Children.OfType<ComboBox>().FirstOrDefault(c => Avalonia.Automation.AutomationProperties.GetName(c) == name);
                for (var i = 0; auswahl is not null && i < 12; i++)
                {
                    if (passt(auswahl.SelectedItem as string ?? ""))
                    {
                        return true;
                    }
                    auswahl.Focus();
                    auswahl.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down, Source = auswahl });
                    await ansicht.BildFertigAsync();
                }
                return false;
            }
            var ziel = await Waehlen(EchtTexte.VerbindenMit, s => s.StartsWith("Klasse „Tier“", StringComparison.Ordinal));
            var art = await Waehlen(EchtTexte.ArtDerBeziehung, s => s == EchtZeichnung.Beziehung("aggregation"));
            if (!ziel || !art || Benannt(t.ProbeKarte, EchtTexte.Verbinden) is not { } verbinden)
            {
                Fund("Tastatur: \"Verbinden mit\" oder \"Art der Beziehung\" laesst sich nicht mit der Pfeiltaste waehlen");
            }
            else
            {
                await TasteAsync(ansicht, verbinden, Key.Enter);
                if (!bau.Diagramm.Verbindungen.Any(v => v.Von == von && v.Nach == "tier" && v.Typ == "aggregation"))
                {
                    Fund("Tastatur: \"Verbinden\" legt keine Aggregation von der gewaehlten Klasse zu Tier an");
                }
            }
            // Screenreader: die Liste nennt Beziehungen mit Art und Multiplizitaet; Aufruf eines Knopfs waehlt den Baustein
            var liste = t.ProbeListe.Children.OfType<Button>().ToList();
            var namen = liste.Select(x => Avalonia.Automation.AutomationProperties.GetName(x) ?? "").ToList();
            if (liste.Count != bau.Diagramm.Elemente.Count || !namen.Any(n => n.Contains(EchtTexte.Attribute + ":", StringComparison.Ordinal))
                || !namen.Any(n => n.Contains("0..*", StringComparison.Ordinal)))
            {
                Fund("Screenreader: Liste \"Deine Zeichnung\" nennt nicht jede Klasse mit Attributen und Multiplizitaeten");
            }
            var knopf = liste.FirstOrDefault(x => (Avalonia.Automation.AutomationProperties.GetName(x) ?? "").StartsWith("Klasse „Termin“", StringComparison.Ordinal));
            if (knopf is null || ControlAutomationPeer.CreatePeerForElement(knopf).GetProvider<IInvokeProvider>() is not { } aufruf)
            {
                Fund("Screenreader: Listenknopf der Klasse Termin nicht aufrufbar");
            }
            else
            {
                aufruf.Invoke();
                await ansicht.BildFertigAsync();
                if (t.ProbeGewaehlt != "termin")
                {
                    Fund("Screenreader: Aufruf des Listenknopfs waehlt die Klasse nicht");
                }
            }
            Messwerte.Notiere("Pruefung im echten Format Bedienprobe Klasse Tastatur und Screenreader: " + namen.Count + " Listenknoepfe");
        }
        else
        {
            Fund("Tastatur: kein Klassen-Baukasten");
        }
        k.Probe(ab + 1);
        await ansicht.BildFertigAsync();
        if (e.ProbeBaukasten is { } u)
        {
            var namen = u.ProbeListe.Children.OfType<Button>().Select(x => Avalonia.Automation.AutomationProperties.GetName(x) ?? "").ToList();
            var grenze = u.ProbeListe.Children.OfType<LernText>().Select(x => x.Text ?? "").ToList();
            if (!namen.Any(n => n.Contains("«include»", StringComparison.Ordinal)) || !namen.Any(n => n.Contains("«extend»", StringComparison.Ordinal))
                || grenze.Count == 0)
            {
                Fund("Screenreader: Use-Case-Liste nennt «include», «extend» oder die Systemgrenze nicht");
            }
        }

        // Bewertung: Zaehlung gruen und rot, Kopf mit dem FIAE-Bereich
        var aeName = EchtArten.Namen(EchtArten.Art(Fachrichtung.Alle.Single(f => f.Id == "ae"), EchtSaetze.B1))!.Value.Name;
        foreach (var art in new[] { ab + 2, ab + 3 })
        {
            k.Probe(art);
            await ansicht.BildFertigAsync();
            var zeilen = e.ProbeInhalt?.GetVisualDescendants().OfType<LernText>().Select(x => x.Text ?? "").ToList() ?? [];
            var gruen = zeilen.Count(z => z.StartsWith("✓ ", StringComparison.Ordinal));
            var rot = zeilen.Count(z => z.StartsWith("✗ ", StringComparison.Ordinal));
            var kopf = ansicht.GetVisualDescendants().OfType<TextBlock>().Any(x => x.IsEffectivelyVisible && x.Text == aeName);
            if (gruen == 0 || rot == 0 || !kopf)
            {
                Fund(KlausurTeil.ProbeNamen[art] + ": Zaehlung (gruen " + gruen + ", rot " + rot + ") oder Kopf mit \"" + aeName + "\" fehlt");
            }
            Messwerte.Notiere("Pruefung im echten Format " + KlausurTeil.ProbeNamen[art] + ": " + gruen + " gruen, " + rot + " rot, Kopf " + (kopf ? "AE" : "fehlt"));
        }
        return funde;
    }
}
