using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.55.0 (Weisung P12-282 Punkt 2): Stadtteil-Namen ziehen und speichern mit Maus, Finger, Tastatur und
/// Screenreader (Abnahmesatz „Namen ziehen und speichern geht mit Maus, Finger, Tastatur“).
/// <list type="bullet">
/// <item>Knopf mit der Maus an (Name „gewählt“), Rechteck mit der Maus ziehen (Vorschau waehrend des Ziehens), Name mit der
/// Tastatur eintippen und Eingabetaste.</item>
/// <item>Zweites Rechteck mit dem Finger, Name ueber „Name übernehmen“ per Automation (wie ein Screenreader).</item>
/// <item>Pfeiltaste verschiebt, Entf loescht, Strg+Z holt zurueck; „Neuer Stadtteil“ per Automation; ein ueberdeckendes
/// Rechteck wird mit rotem Grund abgelehnt (Gegenprobe: nichts geaendert); Tippen waehlt; Taste B beendet.</item>
/// <item>Gespeichert: Projektdatei Format 5, Namen auf der Karte gezeichnet auch nach dem Beenden.</item>
/// </list>
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task NamenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Namen", null, (64, 64));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var karte = a.Karte;
        await BildAsync(a);
        if (karte.NamenGezeichnet.Namen != 0 || p.StadtteilEintraege().Count != 0)
        {
            Fund("Namen: Stadt ohne Namen zeigt Namen");
        }
        IReadOnlyList<string> Namen() => [.. p.StadtteilEintraege().Select(b => b?.Name ?? "?")];
        void Erwarte(string wie, params string[] soll)
        {
            var ist = Namen();
            Messwerte.Notiere("Namen (" + wie + "): " + string.Join(", ", ist));
            if (!ist.SequenceEqual(soll))
            {
                Fund("Namen (" + wie + "): " + string.Join(", ", ist) + " statt " + string.Join(", ", soll));
            }
        }

        // Maus: Knopf an
        var knopf = a.NamenKnopf;
        var zeigerMaus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var mitte = new Point(knopf.Bounds.Width / 2, knopf.Bounds.Height / 2);
        Maus(knopf, zeigerMaus, mitte, RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        Maus(knopf, zeigerMaus, mitte, RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased, los: true);
        await BildAsync(a);
        if (!karte.NamenAn || AutomationProperties.GetName(knopf) != Texte.Gewaehlt(Texte.StadtteilNamen))
        {
            Fund("Namen: Knopf mit der Maus nicht an");
        }

        // Maus zieht ein Rechteck (mit Vorschau), danach Name ueber die Tastatur
        var von = karte.BildPunkt(2.2, 2.1);
        var bis = karte.BildPunkt(17.9, 14.2);
        var maus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        Maus(karte, maus, von, RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        for (var i = 1; i <= 6; i++)
        {
            Maus(karte, maus, von + ((bis - von) * (i / 6.0)), RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other);
        }
        await BildAsync(a);
        if (!karte.NamenGezeichnet.Vorschau)
        {
            Fund("Namen: beim Ziehen mit der Maus keine Vorschau");
        }
        Maus(karte, maus, bis, RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased, los: true);
        await BildAsync(a);
        Erwarte("Maus gezogen", "Stadtteil 1");
        if (p.StadtteilEintraege().FirstOrDefault() is { } b1 && (b1.X1, b1.Z1, b1.X2, b1.Z2) != (2, 2, 18, 14))
        {
            Fund(string.Create(Inv, $"Namen: Rechteck {b1.X1}/{b1.Z1} bis {b1.X2}/{b1.Z2} statt 2/2 bis 18/14 (Raster 0,5)"));
        }
        if (a.NamenFeld is not { } feld)
        {
            Fund("Namen: nach dem Ziehen kein Namensfeld");
        }
        else
        {
            feld.Text = "Altstadt";
            feld.RaiseEvent(new KeyEventArgs { Key = Key.Enter, RoutedEvent = InputElement.KeyDownEvent, Source = feld });
            await BildAsync(a);
            Erwarte("Tastatur Eingabetaste", "Altstadt");
        }

        // Finger zieht ein zweites Rechteck, Name ueber den Screenreader („Name übernehmen“ aufrufen)
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var f1 = karte.BildPunkt(20, 2);
        var f2 = karte.BildPunkt(36, 14);
        Druck(karte, finger, f1);
        for (var i = 1; i <= 6; i++)
        {
            Bewegung(karte, finger, f1 + ((f2 - f1) * (i / 6.0)));
        }
        Los(karte, finger, f2);
        await BildAsync(a);
        // der erste freie Vorschlag ist wieder „Stadtteil 1“ (die Altstadt hat ihren Namen)
        Erwarte("Finger gezogen", "Altstadt", "Stadtteil 1");
        if (a.NamenFeld is { } feld2 && a.NamenKnopfMit(Texte.StadtteilNameUebernehmen) is { } uebernehmen
            && ControlAutomationPeer.CreatePeerForElement(uebernehmen).GetProvider<IInvokeProvider>() is { } aufruf)
        {
            feld2.Text = "Am Hafen";
            aufruf.Invoke();
            await BildAsync(a);
            Erwarte("Screenreader Name übernehmen", "Altstadt", "Am Hafen");
        }
        else
        {
            Fund("Namen: kein Feld oder kein Knopf „Name übernehmen“ für den Screenreader");
        }

        // Tastatur: Pfeil verschiebt, Entf loescht, Strg+Z holt zurueck (Fokus auf der Karte, im Feld bewegt der Pfeil die Schreibmarke)
        karte.Focus();
        a.TasteFuerTests(Key.Right);
        await BildAsync(a);
        if (p.StadtteilEintraege().ElementAtOrDefault(1) is not { X1: 21, X2: 37 })
        {
            Fund("Namen: Pfeiltaste rechts verschiebt den gewählten Stadtteil nicht um 1");
        }
        a.TasteFuerTests(Key.Delete);
        await BildAsync(a);
        Erwarte("Taste Entf", "Altstadt");
        a.Rueckgaengig();
        await BildAsync(a);
        Erwarte("Rückgängig", "Altstadt", "Am Hafen");

        // Screenreader: „Neuer Stadtteil“ in der Mitte der Ansicht
        if (a.NamenKnopfMit(Texte.StadtteilNeu) is { } neu && ControlAutomationPeer.CreatePeerForElement(neu).GetProvider<IInvokeProvider>() is { } neuAufruf)
        {
            karte.Hinfahren(48, 48);
            await BildAsync(a);
            neuAufruf.Invoke();
            await BildAsync(a);
            Erwarte("Screenreader Neuer Stadtteil", "Altstadt", "Am Hafen", "Stadtteil 1");
        }
        else
        {
            Fund("Namen: Knopf „Neuer Stadtteil“ fehlt");
        }
        if (a.NamenFehler is { } nf)
        {
            Fund("Namen: unerwartet abgelehnt: " + nf);
        }

        // rot: ein Rechteck ueber der Altstadt wird abgelehnt, nichts aendert sich
        var vorher = Namen();
        var schritte = p.Verlauf.Count;
        Druck(karte, finger, karte.BildPunkt(4, 4));
        Bewegung(karte, finger, karte.BildPunkt(8, 8));
        Bewegung(karte, finger, karte.BildPunkt(12, 10));
        Los(karte, finger, karte.BildPunkt(12, 10));
        await BildAsync(a);
        Messwerte.Notiere("Namen (rot): " + a.NamenFehler);
        if (a.NamenFehler?.Contains("überdecken sich", StringComparison.Ordinal) != true || !Namen().SequenceEqual(vorher) || p.Verlauf.Count != schritte)
        {
            Fund("Namen: überdeckendes Rechteck nicht mit Grund abgelehnt (\"" + a.NamenFehler + "\")");
        }

        // Tippen waehlt
        Druck(karte, finger, karte.BildPunkt(30, 8));
        Los(karte, finger, karte.BildPunkt(30, 8));
        await BildAsync(a);
        if (karte.NameGewaehlt != 1)
        {
            Fund("Namen: Tippen in „Am Hafen“ wählt ihn nicht (" + karte.NameGewaehlt + ")");
        }

        // Taste B beendet; die Namen bleiben auf der Karte, das Projekt hat Format 5
        a.TasteFuerTests(Key.B);
        await BildAsync(a);
        if (karte.NamenAn || AutomationProperties.GetName(knopf) != Texte.StadtteilNamen)
        {
            Fund("Namen: Taste B beendet nicht");
        }
        karte.Hinfahren(32, 32);
        await BildAsync(a);
        Messwerte.Notiere(string.Create(Inv, $"Namen gezeichnet: {karte.NamenGezeichnet.Namen}"));
        if (karte.NamenGezeichnet.Namen < 2)
        {
            Fund("Namen: nach dem Beenden nicht auf der Karte");
        }
        p.Speichern(vonHand: true);
        var text = await File.ReadAllTextAsync(p.Datei);
        if (!text.Contains("\"formatversion\": 5,", StringComparison.Ordinal) || !text.Contains("Am Hafen", StringComparison.Ordinal))
        {
            Fund("Namen: Projektdatei ohne Format 5 oder ohne Namen");
        }
        if (StadtteilNamen.Alle(p.AussenStand).Count != 3)
        {
            Fund("Namen: gespeichert nicht drei Stadtteile");
        }
        await AlleGroessenAsync(a, f, "Stadtteil-Namen");
    }
}
