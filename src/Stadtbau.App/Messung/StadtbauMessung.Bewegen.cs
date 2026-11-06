using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Patch S 0.5.1 Nachbesserung (Weisung P12-40 Punkte 2 und 4): Ansicht frei bewegen und Drehen. Draufsicht und
/// Grundriss: rechte Maustaste, mittlere Maustaste und Leertaste schieben die Ansicht auch ueber einem Stueck, ohne es
/// zu bewegen (Gegenprobe: linke Maustaste auf dem Stueck schiebt das Stueck); "Ganze Stadt zeigen" / "Ganzen Raum
/// zeigen" holt die Ansicht zurueck. Drehen: beim Setzen einer Strasse sichtbar ("Drehen", Taste R), die Strasse liegt
/// danach senkrecht (ein Schritt im Verlauf); ein Haus hat einen grauen Knopf "Drehen" mit Grund.
/// </summary>
public static partial class StadtbauMessung
{
    private static void Maus(Control c, Pointer maus, Point p, RawInputModifiers taste, PointerUpdateKind art, bool los = false)
    {
        var w = Wurzel(c);
        var punkt = c.TranslatePoint(p, w) ?? p;
        var k = new PointerPointProperties(taste, art);
        if (los)
        {
            c.RaiseEvent(new PointerReleasedEventArgs(c, maus, w, punkt, 0, k, KeyModifiers.None,
                art == PointerUpdateKind.RightButtonReleased ? MouseButton.Right : art == PointerUpdateKind.MiddleButtonReleased ? MouseButton.Middle : MouseButton.Left));
        }
        else if (art is PointerUpdateKind.Other)
        {
            c.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, c, maus, w, punkt, 0, k, KeyModifiers.None));
        }
        else
        {
            c.RaiseEvent(new PointerPressedEventArgs(c, maus, w, punkt, 0, k, KeyModifiers.None));
        }
    }

    /// <summary>Mit der Maus ziehen (Taste: links, rechts, mitte) um (dx, dy) Bildpunkte.</summary>
    private static async Task MausZiehenAsync(Control c, Point start, double dx, double dy, string taste)
    {
        var maus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var (mod, druck, los) = taste switch
        {
            "rechts" => (RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed, PointerUpdateKind.RightButtonReleased),
            "mitte" => (RawInputModifiers.MiddleMouseButton, PointerUpdateKind.MiddleButtonPressed, PointerUpdateKind.MiddleButtonReleased),
            _ => (RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed, PointerUpdateKind.LeftButtonReleased),
        };
        Maus(c, maus, start, mod, druck);
        for (var i = 1; i <= 6; i++)
        {
            Maus(c, maus, start + new Point(dx * i / 6, dy * i / 6), mod, PointerUpdateKind.Other);
        }
        Maus(c, maus, start + new Point(dx, dy), RawInputModifiers.None, los, los: true);
        await BildAsync(c);
    }

    private static async Task BewegenAsync(Hauptansicht a, Window? f)
    {
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.NeuAnlegen("Starttest Bewegen", "moebelhaus");
        await a.AussenGeprueft;
        var p = a.Projekt!;

        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        await SchiebenPruefenAsync(a, "Draufsicht", a.Karte, () => a.Karte.Versatz, a.Karte.StueckAnFuerTests, () => PythonJson.Text(p.AussenStand), Texte.GanzeStadtZeigen);

        a.ReiterZeigen(innen: true);
        a.OrtOeffnen("moebelhaus");
        await BildAsync(a);
        await SchiebenPruefenAsync(a, "Grundriss", a.Flaeche, () => a.Flaeche.Versatz, a.Flaeche.StueckAnFuerTests, () => PythonJson.Text(p.Grundriss("moebelhaus")), Texte.GanzenRaumZeigen);

        // Drehen beim Setzen (Weisung P12-40 Punkt 4): Knopf sichtbar, Taste R dreht, die Strasse liegt senkrecht
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        a.AussenSetzenWaehlenFuerTests(AussenArt.Strasse);
        if (!a.AussenAuswahlKnoepfeAn.Any(k => k.Text == Texte.Drehen && k.An))
        {
            Fund("Drehen: beim Setzen einer Strasse fehlt der Knopf \"" + Texte.Drehen + "\"");
        }
        var dreh = a.Karte.SetzenDreh;
        a.Karte.Taste(Key.R, KeyModifiers.None);
        if (a.Karte.SetzenDreh != (dreh + 1) % 4)
        {
            Fund("Drehen: Taste R dreht beim Setzen nicht");
        }
        a.Karte.SetzenDreh = 1;
        var vorher = p.SchritteZurueck;
        var strasse = a.Karte.SetzenAn(4, 4);
        if (strasse is null)
        {
            Fund("Drehen: senkrechte Strasse nicht gesetzt");
        }
        else
        {
            var punkte = SpielDaten.Liste(Lernplattform.Core.Spiel.SpielDaten.Objekt(Aussen.Landschaft(p.AussenStand)[strasse.Nummer])["punkte"]);
            var senkrecht = punkte.OfType<List<object?>>().Select(q => Py.F(q[0])).Distinct().Count() == 1;
            Messwerte.Notiere("Drehen: Strasse gesetzt mit " + string.Join(" ", punkte.OfType<List<object?>>().Select(q => Py.F(q[0]).ToString(Inv) + "/" + Py.F(q[1]).ToString(Inv))));
            if (!senkrecht || p.SchritteZurueck != vorher + 1)
            {
                Fund("Drehen: Strasse nach \"Drehen\" nicht senkrecht oder nicht ein einziger Schritt");
            }
            a.Rueckgaengig();
        }
        a.Karte.SetzenDreh = 0;
        // ein Haus dreht das Spiel nicht: der Knopf ist grau, der Grund steht dabei
        a.Karte.Waehlen(new AussenStelle(AussenArt.Haus, -1, 0, "moebelhaus"));
        if (!a.AussenAuswahlKnoepfeAn.Any(k => k.Text == Texte.Drehen && !k.An) || !a.AussenAuswahlTexte.Contains(Texte.HausDrehenNicht))
        {
            Fund("Drehen: am Haus fehlt der graue Knopf \"" + Texte.Drehen + "\" mit Grund");
        }
        // eine Strasse dreht sich: Knopf an
        var erste = AussenBearbeitung.Alle(p.AussenStand).First(x => x.Stelle.Art == AussenArt.Strasse).Stelle;
        a.Karte.Waehlen(erste);
        if (!a.AussenAuswahlKnoepfeAn.Any(k => k.Text == Texte.Drehen && k.An))
        {
            Fund("Drehen: an einer Strasse ist der Knopf \"" + Texte.Drehen + "\" nicht an");
        }
        a.Karte.Waehlen(null);
        await AlleGroessenAsync(a, f, "Draufsicht Drehen beim Setzen");
    }

    private static async Task SchiebenPruefenAsync(Hauptansicht a, string name, Control c, Func<Point> versatz, Func<Point, bool> stueck,
        Func<string> zustand, string ganz)
    {
        var auf = Suchen(c, stueck);
        if (auf is not { } punkt)
        {
            Fund(name + " schieben: keine Stelle mit Stueck gefunden");
            return;
        }
        foreach (var taste in new[] { "rechts", "mitte", "leertaste" })
        {
            var v = versatz();
            var z = zustand();
            KartenFlaeche.LeertasteGehalten = taste == "leertaste";
            try
            {
                await MausZiehenAsync(c, punkt, 60, 40, taste == "leertaste" ? "links" : taste);
            }
            finally
            {
                KartenFlaeche.LeertasteGehalten = false;
            }
            var n = versatz();
            Messwerte.Notiere(string.Create(Inv, $"{name} schieben ({taste}): Ansicht {v.X:F0}/{v.Y:F0} -> {n.X:F0}/{n.Y:F0}"));
            if (Math.Abs(n.X - v.X - 60) > 1 || Math.Abs(n.Y - v.Y - 40) > 1)
            {
                Fund(string.Create(Inv, $"{name}: Ziehen mit {taste} ueber einem Stueck schiebt die Ansicht nicht ({v.X:F0}/{v.Y:F0} -> {n.X:F0}/{n.Y:F0})"));
            }
            if (zustand() != z)
            {
                Fund(name + ": Ziehen mit " + taste + " hat ein Stueck verschoben");
                a.Rueckgaengig();
            }
            punkt += new Point(60, 40);
        }
        // Gegenprobe: links auf dem Stueck schiebt das Stueck, nicht die Ansicht
        var v2 = versatz();
        var z2 = zustand();
        await MausZiehenAsync(c, punkt, 30, 0, "links");
        if (zustand() == z2 || versatz() != v2)
        {
            Fund(name + " schieben Gegenprobe: linke Maustaste auf einem Stueck verschiebt das Stueck nicht (Probe taugt nicht)");
        }
        else
        {
            a.Rueckgaengig();
        }
        // "Ganze ... zeigen" holt die Ansicht zurueck
        var knopf = a.KnoepfeMit(ganz).FirstOrDefault();
        if (knopf is null)
        {
            Fund(name + ": Knopf \"" + ganz + "\" fehlt");
            return;
        }
        knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(c);
        var zurueck = versatz();
        await MausZiehenAsync(c, MitteVon(c), 80, 0, "rechts");
        knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(c);
        if (Math.Abs(versatz().X - zurueck.X) > 1 || Math.Abs(versatz().Y - zurueck.Y) > 1)
        {
            Fund(name + ": \"" + ganz + "\" holt die Ansicht nicht zurueck");
        }
    }
}
