using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.56.2 (Fund Nico 373, Weisungen P12-295 und P12-295b): Nicos Weg ueber die Oberflaeche mit seiner echten Stadt 1
/// (Karte 64 × 64). „Plätze“ an, Breite, Tiefe und Platz der heutigen Stadt wie ein Mensch waehlen (Auswahl aufklappen und
/// Eintrag mit Maus oder Finger antippen, Pfeiltasten, Tippen auf die Karte, wie es der Hinweis sagt), die Vorschau pruefen
/// (Karte blau auf dem Platz, Zeile rechts), „Übernehmen und speichern“ druecken. Danach liegt die Stadt auf dem Platz: in den
/// Daten, in der Draufsicht, in 3D und nach Speichern und Neuoeffnen. Fall B (Karte schon gross) und Grund rot ebenso.
/// Gegenprobe: derselbe Weg mit dem Stand S 0.56.1 (<see cref="Hauptansicht.PlaetzeAltFuerTests"/>) laesst die Stadt auf
/// Platz 1 (Nicos Fund).
/// </summary>
public static partial class StadtbauMessung
{
    /// <summary>Nicos Stadt 1 (Fassung 2, Werkzeug S 0.53.1) im Repo.</summary>
    private const string NicoStadt1 = "tests/Stadtbau.Tests/Testdaten/nico/stadt1_S0.53.1.fistadt";

    /// <summary>Wie ein Mensch: Auswahl aufklappen, Eintrag antippen (Maus, Finger) oder mit Pfeiltasten waehlen.</summary>
    private static async Task WieMenschWaehlenAsync(Hauptansicht a, string name, object? tag, string art)
    {
        if (a.PlaetzeElement(name) is not ComboBox c)
        {
            Fund("373: Auswahl „" + name + "“ fehlt");
            return;
        }
        if (art == "tastatur")
        {
            c.Focus();
            var ziel = c.Items.OfType<ComboBoxItem>().ToList().FindIndex(i => Equals(i.Tag, tag));
            for (var n = 0; n < 40 && c.SelectedIndex != ziel; n++)
            {
                c.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = c.SelectedIndex < ziel ? Key.Down : Key.Up, Source = c });
                await BildAsync(a);
                await BildAsync(a);
                if (a.PlaetzeElement(name) is ComboBox neu && !ReferenceEquals(neu, c))
                {
                    c = neu;
                    c.Focus();
                }
            }
            return;
        }
        c.IsDropDownOpen = true;
        await BildAsync(a);
        await BildAsync(a);
        if (c.Items.OfType<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, tag)) is not { } item)
        {
            Fund("373: Auswahl „" + name + "“ ohne Eintrag " + tag);
            c.IsDropDownOpen = false;
            return;
        }
        item.BringIntoView();
        await BildAsync(a);
        var mitte = new Point(item.Bounds.Width / 2, item.Bounds.Height / 2);
        var zeiger = new Pointer(Pointer.GetNextFreeId(), art == "finger" ? PointerType.Touch : PointerType.Mouse, true);
        Maus(item, zeiger, mitte, art == "finger" ? RawInputModifiers.None : RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        Maus(item, zeiger, mitte, RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased, los: true);
        await BildAsync(a);
        await BildAsync(a);
        if (c.IsDropDownOpen)
        {
            c.IsDropDownOpen = false;
            await BildAsync(a);
        }
    }

    /// <summary>Knopf rechts druecken wie ein Mensch (Maus oder Finger); trifft der Klick nicht, wie ein Screenreader.</summary>
    private static async Task<string> DrueckenAsync(Hauptansicht a, Projekt p, string name, string art)
    {
        if (a.PlaetzeElement(name) is not Button { IsEnabled: true } k)
        {
            Fund("373: Knopf „" + name + "“ fehlt oder gesperrt");
            return "fehlt";
        }
        k.BringIntoView();
        for (var n = 0; n < 4; n++)
        {
            await BildAsync(a);
        }
        if (a.PlaetzeElement(name) is Button { IsEnabled: true } frisch)
        {
            k = frisch;
        }
        var vorher = p.SchritteZurueck;
        var fehlerVorher = a.PlaetzeFehler;
        if (art == "tastatur")
        {
            // Tastatur: Fokus auf den Knopf, Eingabetaste
            k.Focus(NavigationMethod.Tab);
            await BildAsync(a);
            k.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = k });
            k.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Enter, Source = k });
            await BildAsync(a);
            if (p.SchritteZurueck != vorher || a.PlaetzeFehler != fehlerVorher || a.StadtBestaetigt is not null)
            {
                return art;
            }
            Fund("373: „" + name + "“ reagiert nicht auf die Eingabetaste");
            return "keiner";
        }
        var m = new Point(k.Bounds.Width / 2, k.Bounds.Height / 2);
        for (var versuch = 0; versuch < 4; versuch++)
        {
            if (versuch > 0)
            {
                // ein Mensch klickt nicht im selben Bild, in dem der Knopf entsteht
                await Task.Delay(300);
                await BildAsync(a);
                if (a.PlaetzeElement(name) is Button { IsEnabled: true } neu)
                {
                    k = neu;
                }
            }
            if (TopLevel.GetTopLevel(k) is { } tl && k.TranslatePoint(m, tl) is { } tp && tl.InputHitTest(tp) is Visual treffer
                && !ReferenceEquals(treffer, k) && !Avalonia.VisualTree.VisualExtensions.IsVisualAncestorOf(k, treffer))
            {
                Messwerte.Notiere("373 Hinweis: Versuch " + (versuch + 1) + ", an der Mitte von „" + name + "“ liegt im Bild " + treffer.GetType().Name + " " + treffer.Bounds + " (Knopf " + k.Bounds + ")");
                continue;
            }
            var z = new Pointer(Pointer.GetNextFreeId(), art == "finger" ? PointerType.Touch : PointerType.Mouse, true);
            Maus(k, z, m, art == "finger" ? RawInputModifiers.None : RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
            Maus(k, z, m, RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased, los: true);
            await BildAsync(a);
            if (p.SchritteZurueck != vorher || a.PlaetzeFehler != fehlerVorher || a.StadtBestaetigt is not null)
            {
                return art + (versuch > 0 ? " (Versuch " + (versuch + 1) + ")" : "");
            }
        }
        // Xvfb: die Trefferpruefung des Klicks findet einen eben neu gebauten Knopf nicht immer; dann wie ein Screenreader
        if (a.PlaetzeElement(name) is Button { IsEnabled: true } k2 && ControlAutomationPeer.CreatePeerForElement(k2).GetProvider<IInvokeProvider>() is { } aufruf)
        {
            aufruf.Invoke();
            await BildAsync(a);
            return "screenreader";
        }
        return "keiner";
    }

    private static IEnumerable<string> Kette(Visual v)
    {
        for (Visual? x = v; x is not null; x = Avalonia.VisualTree.VisualExtensions.GetVisualParent(x))
        {
            yield return x.GetType().Name + (x is Control c && !string.IsNullOrEmpty(c.Name) ? "#" + c.Name : "") + "@" + x.Bounds.Position;
        }
    }

    /// <summary>Ecke der Stadt (kleinste Lage aller Stuecke draussen).</summary>
    private static (double X, double Y) StadtMarke(Projekt p)
    {
        var alle = AussenBearbeitung.Alle(p.AussenStand).ToList();
        return (alle.Min(t => t.X), alle.Min(t => t.Y));
    }

    private static async Task<Projekt> Nico373OeffnenAsync(Hauptansicht a, Window? f, string name)
    {
        var p = Projekt.AusStadtDatei(File.ReadAllBytes(NicoStadt1), a.Daten!, a.Ablage, name);
        a.ProjektSetzen(p, null, innen: false);
        a.ReiterZeigen(innen: false);
        await a.AussenGeprueft;
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var knopf = a.PlaetzeKnopf;
        var zeiger = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var m = new Point(knopf.Bounds.Width / 2, knopf.Bounds.Height / 2);
        Maus(knopf, zeiger, m, RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        Maus(knopf, zeiger, m, RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased, los: true);
        await BildAsync(a);
        return p;
    }

    /// <summary>Auf die Karte tippen, wo Platz <paramref name="platz"/> des Rasters (Breite) liegt.</summary>
    private static async Task PlatzAufKarteAsync(Hauptansicht a, int platz, int breite, string art)
    {
        var (x, y) = Plaetze.Lage(platz, breite);
        var punkt = a.Karte.BildPunkt(x + 32, y + 32);
        var zeiger = new Pointer(Pointer.GetNextFreeId(), art == "finger" ? PointerType.Touch : PointerType.Mouse, true);
        if (art == "finger")
        {
            Druck(a.Karte, zeiger, punkt);
            Los(a.Karte, zeiger, punkt);
        }
        else
        {
            Maus(a.Karte, zeiger, punkt, RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
            Maus(a.Karte, zeiger, punkt, RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased, los: true);
        }
        await BildAsync(a);
        await BildAsync(a);
    }

    /// <summary>Stadt liegt auf dem Platz: Daten, Bestaetigung, Draufsicht, Speichern und Neuoeffnen (und auf Wunsch 3D).</summary>
    private static async Task StadtAufPlatzPruefenAsync(Hauptansicht a, Projekt p, string fall, (double X, double Y) marke0, int platz, int breite, int tiefe, bool drei)
    {
        var (ox, oy) = Plaetze.Lage(platz, breite);
        var marke = StadtMarke(p);
        var groesse = Aussen.Groesse(p.AussenStand);
        Messwerte.Notiere(string.Create(Inv, $"373 {fall}: Groesse {groesse}, Ecke {marke0} -> {marke} (soll + {ox}/{oy}), Bestätigung: {a.StadtBestaetigt}"));
        if (groesse != (breite, tiefe) || Math.Abs(marke.X - marke0.X - ox) > 1e-3 || Math.Abs(marke.Y - marke0.Y - oy) > 1e-3)
        {
            Fund(string.Create(Inv, $"373 {fall}: Stadt nicht auf Platz {platz} (Größe {groesse}, Versatz {marke.X - marke0.X:0.##}/{marke.Y - marke0.Y:0.##})"));
        }
        if (a.StadtBestaetigt != Texte.StadtUebernommen(platz, breite, tiefe))
        {
            Fund("373 " + fall + ": keine grüne Bestätigung (" + a.StadtBestaetigt + ")");
        }
        // Draufsicht: die ganze neue Karte ist zu sehen, die Stadt-Vorschau steht auf ihrem Platz (dort, wo sie jetzt liegt)
        var welt = a.Karte.WeltFlaeche;
        if (welt.Width < 10 || Math.Abs((welt.Width / welt.Height) - (breite / (double)tiefe)) > 0.02
            || a.Karte.StadtVorschauGezeichnet is not { } sv || sv.Bereich.X1 != ox || sv.Bereich.Y1 != oy)
        {
            Fund("373 " + fall + ": Draufsicht zeigt die Stadt nicht auf Platz " + platz + " (" + a.Karte.StadtVorschauGezeichnet + ", " + welt + ")");
        }
        if (drei)
        {
            a.DreiZeigen();
            await a.DreiFertig;
            await BildAsync(a);
            var o = a.Drei.Spiel?.Szene.Orte.FirstOrDefault();
            Messwerte.Notiere(string.Create(Inv, $"373 {fall}: 3D erster Ort {o?.Id} bei {o?.StehX:0.##}/{o?.StehZ:0.##}"));
            if (o is null || o.StehX < ox || o.StehX > ox + Plaetze.Seite || o.StehZ < oy || o.StehZ > oy + Plaetze.Seite)
            {
                Fund("373 " + fall + ": 3D zeigt die Stadt nicht auf Platz " + platz);
            }
            a.ReiterZeigen(innen: false);
            await BildAsync(a);
            // „Plätze“ geht nur in der Draufsicht: wieder an (Platz der Stadt ist dann der, auf dem sie liegt)
            a.PlaetzeUmschalten();
            await BildAsync(a);
            if (a.StadtPlan?.Platz != platz || a.Karte.StadtVorschauGezeichnet is not { } sv2 || sv2.Bereich.X1 != ox || sv2.Bereich.Y1 != oy)
            {
                Fund("373 " + fall + ": nach 3D zeigt „Plätze“ die Stadt nicht auf Platz " + platz + " (" + a.StadtPlan + ")");
            }
        }
        var neu = Projekt.Oeffnen(p.Datei, a.Daten!, a.Ablage);
        var nm = StadtMarke(neu);
        if (Aussen.Groesse(neu.AussenStand) != (breite, tiefe) || Math.Abs(nm.X - marke.X) > 1e-3 || Math.Abs(nm.Y - marke.Y) > 1e-3)
        {
            Fund(string.Create(Inv, $"373 {fall}: nach Speichern und Öffnen nicht auf Platz {platz} ({nm})"));
        }
    }

    private static async Task Platz373Async(Hauptansicht a, Window? f)
    {
        if (!File.Exists(NicoStadt1) || a.Daten is null)
        {
            Fund("373: Nicos Stadt 1 fehlt (" + Path.GetFullPath(NicoStadt1) + ")");
            return;
        }

        // Gegenprobe: Nicos Weg mit dem Stand S 0.56.1 (Breite und Tiefe 128, Platz 4 auf der Karte getippt, wie es der Hinweis
        // sagt, dann „Vergrößern“): die Karte wird groesser, die Stadt bleibt auf Platz 1
        Hauptansicht.PlaetzeAltFuerTests = true;
        try
        {
            var p = await Nico373OeffnenAsync(a, f, "Starttest 373 Gegenprobe");
            var marke0 = StadtMarke(p);
            await WieMenschWaehlenAsync(a, Texte.NeueBreite, 128, "maus");
            await WieMenschWaehlenAsync(a, Texte.NeueTiefe, 128, "maus");
            await PlatzAufKarteAsync(a, 4, 128, "maus");
            await DrueckenAsync(a, p, Texte.KarteVergroessernKnopf, "maus");
            var marke = StadtMarke(p);
            Messwerte.Notiere(string.Create(Inv, $"373 Gegenprobe S 0.56.1: Größe {Aussen.Groesse(p.AussenStand)}, Versatz {marke.X - marke0.X:0.##}/{marke.Y - marke0.Y:0.##} (Platz 4 wäre 64/64)"));
            if (Aussen.Groesse(p.AussenStand) != (128, 128) || Math.Abs(marke.X - marke0.X) > 1e-3 || Math.Abs(marke.Y - marke0.Y) > 1e-3)
            {
                Fund("373: Gegenprobe zeigt Nicos Fund nicht (mit dem alten Weg bleibt die Stadt auf Platz 1)");
            }
            a.Karte.PlaetzeAn = false;
            await BildAsync(a);
        }
        finally
        {
            Hauptansicht.PlaetzeAltFuerTests = false;
        }

        // Fall A, Nicos Weg mit dem neuen Stand: Breite, Tiefe, Platz auf der Karte getippt, Vorschau, „Übernehmen und speichern“
        {
            var p = await Nico373OeffnenAsync(a, f, "Starttest 373 Karte");
            var marke0 = StadtMarke(p);
            if (a.StadtPlan is not { Platz: 1 } || a.Karte.StadtVorschauGezeichnet is not { Bereich: (0, 0, 64, 64) })
            {
                Fund("373: beim Einschalten zeigt die Vorschau die Stadt nicht auf Platz 1 (" + a.StadtPlan + ")");
            }
            await WieMenschWaehlenAsync(a, Texte.NeueBreite, 128, "maus");
            await WieMenschWaehlenAsync(a, Texte.NeueTiefe, 128, "maus");
            await PlatzAufKarteAsync(a, 4, 128, "finger");
            var vorschau = a.Karte.StadtVorschauGezeichnet;
            Messwerte.Notiere("373 Fall A: Vorschau " + vorschau + ", Plan " + a.StadtPlan + ", Schritte " + p.SchritteZurueck);
            if (vorschau is not { Bereich: (64, 64, 128, 128), Farbe: "blau" } || a.StadtPlan is not { Platz: 4, Geht: true }
                || p.SchritteZurueck != 0 || a.Karte.PlaetzeGezeichnet.Plaetze != 4)
            {
                Fund("373 Fall A: Vorschau nach dem Tippen auf Platz 4 fehlt oder es wurde schon etwas geändert (" + vorschau + ")");
            }
            var weg = await DrueckenAsync(a, p, Texte.StadtUebernehmenKnopf, "maus");
            Messwerte.Notiere("373 Fall A: „Übernehmen und speichern“ gedrückt über " + weg);
            await StadtAufPlatzPruefenAsync(a, p, "Fall A Karte getippt", marke0, 4, 128, 128, drei: true);
            await AlleGroessenAsync(a, f, "373 Plätze nach Übernehmen");

            // Fall B: Karte schon 128 × 128, Stadt mit der Pfeiltaste nach oben auf Platz 2
            a.Karte.Focus();
            a.TasteFuerTests(Key.Up);
            await BildAsync(a);
            if (a.StadtPlan is not { Platz: 2, Geht: true } || a.Karte.StadtVorschauGezeichnet is not { Bereich: (64, 0, 128, 64) })
            {
                Fund("373 Fall B: Pfeil nach oben wählt nicht Platz 2 mit Vorschau (" + a.StadtPlan + ")");
            }
            var weg2 = await DrueckenAsync(a, p, Texte.StadtUebernehmenKnopf, "finger");
            Messwerte.Notiere("373 Fall B: „Übernehmen und speichern“ gedrückt über " + weg2);
            await StadtAufPlatzPruefenAsync(a, p, "Fall B Pfeiltaste", marke0, 2, 128, 128, drei: false);

            // Grund rot: dieselbe Wahl noch einmal (liegt schon dort), nichts geaendert
            var schritte = p.SchritteZurueck;
            await DrueckenAsync(a, p, Texte.StadtUebernehmenKnopf, "maus");
            if (a.PlaetzeFehler != Texte.StadtSchonDort(2) || p.SchritteZurueck != schritte || a.Karte.StadtVorschauGezeichnet is not { Farbe: "rot" })
            {
                Fund("373: „liegt schon dort“ nicht rot oder trotzdem geändert (" + a.PlaetzeFehler + ")");
            }
            // Rueckgaengig zweimal: zurueck auf 64 × 64, Stadt auf Platz 1
            a.Rueckgaengig();
            a.Rueckgaengig();
            await BildAsync(a);
            var zurueck = StadtMarke(p);
            if (Aussen.Groesse(p.AussenStand) != (64, 64) || Math.Abs(zurueck.X - marke0.X) > 1e-9 || Math.Abs(zurueck.Y - marke0.Y) > 1e-9)
            {
                Fund("373: Rückgängig bringt die Stadt nicht auf 64 × 64 Platz 1 zurück");
            }
            a.Karte.PlaetzeAn = false;
            await BildAsync(a);
        }

        // Reihenfolgen der drei Wahlen rechts (B = Breite, T = Tiefe, P = Platz) mit Maus, Finger und Tastatur
        var faelle = new (string Folge, int Breite, int Tiefe, int Platz, string Art)[]
        {
            ("BTP", 128, 128, 4, "maus"),
            ("TBP", 128, 128, 2, "finger"),
            ("BPT", 128, 128, 2, "tastatur"),
            ("TPB", 64, 128, 2, "maus"),
            ("BTP", 192, 64, 3, "finger"),
        };
        foreach (var fall in faelle)
        {
            var p = await Nico373OeffnenAsync(a, f, "Starttest 373 " + fall.Folge + " " + fall.Platz + " " + fall.Art);
            var marke0 = StadtMarke(p);
            foreach (var schritt in fall.Folge)
            {
                var (name, wert) = schritt switch
                {
                    'B' => (Texte.NeueBreite, fall.Breite),
                    'T' => (Texte.NeueTiefe, fall.Tiefe),
                    _ => (Texte.HeutigeStadtAuf, fall.Platz),
                };
                await WieMenschWaehlenAsync(a, name, wert, fall.Art);
            }
            if (a.VergroessernWahl != (fall.Breite, fall.Tiefe, fall.Platz))
            {
                Fund("373 " + fall.Folge + "/" + fall.Art + ": Wahl rechts ist " + a.VergroessernWahl);
            }
            var weg = await DrueckenAsync(a, p, Texte.StadtUebernehmenKnopf, fall.Art);
            Messwerte.Notiere("373 " + fall.Folge + "/" + fall.Art + ": „Übernehmen und speichern“ gedrückt über " + weg);
            await StadtAufPlatzPruefenAsync(a, p, fall.Folge + "/" + fall.Art, marke0, fall.Platz, fall.Breite, fall.Tiefe, drei: false);
            a.Karte.PlaetzeAn = false;
            await BildAsync(a);
        }
    }
}
