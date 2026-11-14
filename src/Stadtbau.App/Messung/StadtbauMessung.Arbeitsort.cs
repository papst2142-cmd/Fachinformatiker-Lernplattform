using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle E4 „Aufgaben an Gebäuden“ (S 0.53.0, Weisung P12-247): Arbeitsort einer neuen Quest. Messpunkte (400 ms, wiederholbar):
/// „Arbeitsort wählen (bis zum Bild)“ (Ort in der Quest-Karte waehlen) und „Arbeitsort ziehen (bis zum Bild)“ (Marke „A“ mit dem
/// Finger in der Draufsicht auf ein anderes Haus ziehen). Geprueft: nach dem Waehlen zeigt die Karte Raum und Stelle und die
/// Draufsicht die Marke; vorhandene Quest bietet keinen Arbeitsort an (1a); Ziehen ist ein Schritt, Raum und Stelle stehen danach
/// auf der ersten freien Stelle im ersten Raum und die Karte zeigt sie; Loslassen neben einem Haus = kein Schritt und sichtbarer
/// Hinweis (Gegenprobe); Escape bricht das Ziehen ab (kein Schritt). Layout je Groesse hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task ArbeitsortAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Arbeitsort", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        a.QuestSeiteZeigen(Texte.Quests);
        await QuestPruefungAbwartenAsync(a);

        // vorhandene Quest: kein Arbeitsort (nur der Hinweis, 1a)
        var bestand = p.Quests().First(q => q.Bestand);
        a.QuestOeffnen(bestand.Id);
        await BildAsync(a);
        if (OrtWahl(a) is not null || !a.QuestEditor.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == Texte.QuestArbeitsortBestand))
        {
            Fund("Arbeitsort: vorhandene Quest bietet einen Arbeitsort an oder zeigt den Hinweis nicht");
        }

        // neue Quest, an einen vorhandenen Questgeber gehaengt
        a.QuestNeuAnlegen("auswahl");
        var neu = a.QuestAuswahl.Quest!;
        p.QuestUmhaengen(neu, p.Questgeber().First(q => q.Bestand).Id);
        a.QuestOeffnen(neu);
        await BildAsync(a);
        var wahlen = p.Arbeitsorte();
        var erster = wahlen.First(w => w.Art == Questebene.ArbeitsortArt.Buero).Ort;
        await MessenAsync("Arbeitsort wählen (bis zum Bild)", a, () =>
        {
            if (OrtWahl(a) is { } c && c.Items.OfType<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, erster)) is { } item)
            {
                c.SelectedItem = item;
            }
            else
            {
                Fund("Arbeitsort: Auswahl „" + Texte.QuestArbeitsortOrt + "“ fehlt oder ohne „" + erster + "“");
            }
            return Task.CompletedTask;
        }, async () =>
        {
            a.Rueckgaengig();
            a.QuestOeffnen(neu);
            await BildAsync(a);
        });
        await BildAsync(a);
        if (QuestDaten.NeueQuest(p.AussenStand, neu) is not { } qn || Questebene.ArbeitsortLesen(qn) is not { } gewaehlt || gewaehlt.Ort != erster
            || gewaehlt.Raum.Length == 0)
        {
            Fund("Arbeitsort: Waehlen in der Karte setzt keinen Arbeitsort mit Raum");
            return;
        }
        var raumWahl = a.QuestEditor.GetLogicalDescendants().OfType<ComboBox>().Any(c => AutomationProperties.GetName(c) == Texte.QuestArbeitsortRaum);
        var stelleZeile = a.QuestEditor.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains(Texte.QuestArbeitsortStelleWert(gewaehlt.X, gewaehlt.Y), StringComparison.Ordinal) == true);
        if (!raumWahl || !stelleZeile)
        {
            Fund("Arbeitsort: Karte zeigt Raum oder Stelle nicht");
        }
        await AlleGroessenAsync(a, f, "Quest Arbeitsort");
        FelderFrei(a.QuestEditor, "Quest Arbeitsort");

        // Draufsicht: Marke „A“ und Linie
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        if (a.Karte.Arbeitsort is not { } marke || marke.Quest != neu || marke.Von is null)
        {
            Fund("Arbeitsort: Draufsicht zeigt keine Marke „A“ mit Linie vom Questgeber");
            return;
        }
        await AlleGroessenAsync(a, f, "Arbeitsort Draufsicht");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.Karte.Einpassen();
        await BildAsync(a);

        // Ziel: Mitte eines anderen Hauses mit Arbeitsort (Ort der Karte unter dem Punkt muss stimmen)
        if (ArbeitsortZiel(a, p, erster) is not { } ziel)
        {
            Fund("Arbeitsort: kein zweites Haus mit Arbeitsort fuer das Ziehen");
            return;
        }
        await MessenAsync("Arbeitsort ziehen (bis zum Bild)", a, () =>
        {
            MarkeZiehen(a, a.Karte.Bildpunkt(ziel.X, ziel.Y), loslassen: true);
            return Task.CompletedTask;
        }, async () =>
        {
            a.Rueckgaengig();
            await BildAsync(a);
        });
        await BildAsync(a);
        var schritte = p.SchritteZurueck;
        if (Questebene.ArbeitsortLesen(QuestDaten.NeueQuest(p.AussenStand, neu)!) is not { } gezogen || gezogen.Ort != ziel.Ort)
        {
            Fund("Arbeitsort: Ziehen auf ein Haus setzt den Arbeitsort nicht");
            return;
        }
        var g = p.ArbeitsortGrundriss(gezogen.Ort, gezogen.Etage);
        var raum = g is null ? null : Questebene.Raeume(g).Select(r => r.Id).FirstOrDefault(r => Questebene.ErsteFreieStelle(g, r) is not null);
        if (gezogen.Raum != (raum ?? "") || !a.StatusText.Contains(a.ArbeitsortBeschreibung(neu), StringComparison.Ordinal)
            || a.Karte.ArbeitsortZiehtGerade)
        {
            Fund("Arbeitsort: nach dem Ziehen nicht im ersten Raum mit freier Stelle oder ohne Meldung („" + a.StatusText + "“)");
        }
        a.QuestOeffnen(neu);
        await BildAsync(a);
        if (!a.QuestEditor.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains(Texte.QuestArbeitsortStelleWert(gezogen.X, gezogen.Y), StringComparison.Ordinal) == true))
        {
            Fund("Arbeitsort: Karte zeigt die neue Stelle nach dem Ziehen nicht");
        }
        a.ReiterZeigen(innen: false);
        await BildAsync(a);

        // Gegenprobe: Loslassen neben einem Haus (draussen am Weg) = kein Schritt, sichtbarer Hinweis
        var draussen = QuestDraussenPlatz(p);
        MarkeZiehen(a, a.Karte.Bildpunkt(draussen.X, draussen.Y), loslassen: true);
        await BildAsync(a);
        if (p.SchritteZurueck != schritte || a.StatusText != Texte.QuestArbeitsortZiehenUngueltig)
        {
            Fund("Arbeitsort: Loslassen neben einem Haus macht einen Schritt oder meldet nichts („" + a.StatusText + "“)");
        }

        // Escape bricht das Ziehen ab
        MarkeZiehen(a, a.Karte.Bildpunkt(ziel.X, ziel.Y), loslassen: false);
        await BildAsync(a);
        if (!a.Karte.ArbeitsortZiehtGerade)
        {
            Fund("Arbeitsort: Druck auf die Marke beginnt kein Ziehen");
        }
        await AlleGroessenAsync(a, f, "Arbeitsort beim Ziehen");
        a.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = a });
        await BildAsync(a);
        if (a.Karte.ArbeitsortZiehtGerade || p.SchritteZurueck != schritte || a.StatusText != Texte.QuestArbeitsortZiehenAbgebrochen)
        {
            Fund("Arbeitsort: Escape bricht das Ziehen nicht ab");
        }

        // Bild 2: gewaehltes Haus zeigt rechts „Quests mit Arbeitsort hier (1)“; Gegenprobe: das Haus des ersten Orts nicht mehr
        if (HausVon(p, ziel.Ort) is not { } haus)
        {
            Fund("Arbeitsort: Haus des Ziels nicht gefunden");
            return;
        }
        a.Karte.Waehlen(haus);
        await BildAsync(a);
        if (!a.AuswahlDraussenTexte.Contains(Texte.QuestArbeitsortHier(1)))
        {
            Fund("Arbeitsort: gewaehltes Haus zeigt „" + Texte.QuestArbeitsortHier(1) + "“ nicht");
        }
        await AlleGroessenAsync(a, f, "Arbeitsort Haus gewaehlt");
        if (HausVon(p, erster) is { } alt)
        {
            a.Karte.Waehlen(alt);
            await BildAsync(a);
            if (a.AuswahlDraussenTexte.Any(t => t.StartsWith(Texte.QuestArbeitsortHier(0)[..20], StringComparison.Ordinal)))
            {
                Fund("Arbeitsort: altes Haus zeigt noch „Quests mit Arbeitsort hier“ (Gegenprobe)");
            }
        }
        a.Karte.Waehlen(null);

        // Bild 3: Arbeitsort im Moebelhaus, im Grundriss die Marke und rechts „Arbeitsort hier: 1 Quest“
        if (p.QuestArbeitsort(neu, "moebelhaus") && Questebene.ArbeitsortLesen(QuestDaten.NeueQuest(p.AussenStand, neu)!) is { } laden)
        {
            a.ReiterZeigen(innen: true);
            a.OrtOeffnen(Etagen.Schluessel("moebelhaus", 0));
            await BildAsync(a);
            if (!a.Flaeche.ArbeitsortMarken.Any(m => Math.Abs(m.X - laden.X) < 0.01 && Math.Abs(m.Y - laden.Y) < 0.01)
                || !a.ArbeitsortHierTexte.Contains(Texte.QuestArbeitsortInnenHier(1)))
            {
                Fund("Arbeitsort: Grundriss zeigt Marke oder „" + Texte.QuestArbeitsortInnenHier(1) + "“ nicht");
            }
            await AlleGroessenAsync(a, f, "Arbeitsort im Grundriss");
            p.QuestArbeitsortWartet(neu, false);
            await BildAsync(a);
            if (!a.Flaeche.ArbeitsortMarken.Any(m => m.Name == Texte.QuestArbeitsortMarkeAuftragName))
            {
                Fund("Arbeitsort: bei „wartet nein“ zeigt die Marke nicht „" + Texte.QuestArbeitsortMarkeAuftragName + "“");
            }
            a.Rueckgaengig();
            a.Rueckgaengig();
            await BildAsync(a);
            if (a.Flaeche.ArbeitsortMarken.Count != 0 || a.ArbeitsortHierTexte.Any())
            {
                Fund("Arbeitsort: nach Rueckgaengig noch Marke im Moebelhaus (Gegenprobe)");
            }
            a.ReiterZeigen(innen: false);
            await BildAsync(a);
        }
        else
        {
            Fund("Arbeitsort: Moebelhaus laesst sich nicht als Arbeitsort setzen");
        }
        Messwerte.Notiere("Arbeitsort: " + wahlen.Count.ToString(Inv) + " Orte waehlbar, gezogen nach " + ziel.Ort);
    }

    private static AussenStelle? HausVon(Projekt p, string spielOrt) =>
        AussenBearbeitung.Alle(p.AussenStand).Select(t => t.Stelle).FirstOrDefault(s => s.Art == AussenArt.Haus && s.Ort is not null
            && AussenBearbeitung.Ort(p.AussenStand, s.Ort) is { } o && Innenraum.OrtFuer(SpielDaten.Get(o, "ansicht") as string) == spielOrt);

    private static ComboBox? OrtWahl(Hauptansicht a) =>
        a.QuestEditor.GetLogicalDescendants().OfType<ComboBox>().FirstOrDefault(c => AutomationProperties.GetName(c) == Texte.QuestArbeitsortOrt);

    /// <summary>Mitte eines Hauses, dessen Spiel-Ort Arbeitsort sein kann und nicht <paramref name="ausser"/> ist.</summary>
    private static (double X, double Y, string Ort)? ArbeitsortZiel(Hauptansicht a, Projekt p, string ausser)
    {
        var orte = p.Arbeitsorte().Select(w => w.Ort).ToHashSet();
        var st = p.AussenStand;
        foreach (var (s, _, _) in AussenBearbeitung.Alle(st).Where(t => t.Stelle.Art == AussenArt.Haus && t.Stelle.Ort is not null).ToList())
        {
            if (AussenBearbeitung.Ort(st, s.Ort!) is not { } o || Innenraum.OrtFuer(SpielDaten.Get(o, "ansicht") as string) is not { } spiel
                || spiel == ausser || !orte.Contains(spiel))
            {
                continue;
            }
            var (x, y, w, h) = AussenBearbeitung.HausFlaeche(st, s);
            var (mx, my) = (x + (w / 2), y + (h / 2));
            if (a.Karte.OrtBei(mx, my) == s.Ort)
            {
                return (mx, my, spiel);
            }
        }
        return null;
    }

    /// <summary>Finger drueckt auf die Marke „A“ und zieht in Schritten bis <paramref name="ziel"/> (Bildpunkt).</summary>
    private static void MarkeZiehen(Hauptansicht a, Point ziel, bool loslassen)
    {
        if (a.Karte.ArbeitsortBildpunkt is not { } start)
        {
            Fund("Arbeitsort: keine Marke zum Ziehen");
            return;
        }
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        Druck(a.Karte, finger, start);
        for (var i = 1; i <= 6; i++)
        {
            Bewegung(a.Karte, finger, start + ((ziel - start) * (i / 6.0)));
        }
        if (loslassen)
        {
            Los(a.Karte, finger, ziel);
        }
    }
}
