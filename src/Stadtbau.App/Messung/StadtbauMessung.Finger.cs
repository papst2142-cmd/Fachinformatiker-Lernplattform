using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Messung;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Patch S 0.5.1 (Weisung P12-37 Punkt 3, Nico: Zoomen mit zwei Fingern klappte nur manchmal): nachgestellte
/// Finger (PointerType.Touch) auf der echten Draufsicht, dem Grundriss und der 3D-Ansicht. Je Flaeche vier Faelle:
/// zweiter Finger spaeter oder gleich, Start auf einem Stueck oder auf freier Flaeche. Zwei Finger auseinander muessen
/// zoomen und duerfen nichts verschieben. Gegenprobe: dieselben Finger mit der Auswertung vor S 0.5.1 zoomen nicht.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task FingerAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Finger", "moebelhaus");
        await a.AussenGeprueft;
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        a.ReiterZeigen(innen: true);
        a.OrtOeffnen("moebelhaus");
        await BildAsync(a);
        var p = a.Projekt!;
        await FlaecheAsync(a, "Grundriss", a.Flaeche, () => a.Flaeche.Zoom, a.Flaeche.StueckAnFuerTests,
            () => PythonJson.Text(p.Grundriss("moebelhaus")), alt => a.Flaeche.GesteAltFuerTests = alt);

        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        await FlaecheAsync(a, "Draufsicht", a.Karte, () => a.Karte.Zoom, a.Karte.StueckAnFuerTests,
            () => PythonJson.Text(p.AussenStand), alt => a.Karte.GesteAltFuerTests = alt);

        // 3D: die Spiel-Flaeche verfolgt die Finger selbst (schon vor S 0.5.1); geprueft wird, dass beide Faelle zoomen
        a.DreiZeigen();
        await a.DreiFertig;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Finger 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
        }
        else
        {
            foreach (var spaeter in new[] { false, true })
            {
                // vorher herauszoomen (Mausrad), damit der Zoom nicht an der kleinsten Entfernung haengt
                await EinpassenAsync(a.Drei, () => 1 / a.Drei.AktuelleKamera!.Abstand, 1 / 10.0);
                var vorher = a.Drei.AktuelleKamera!.Abstand;
                await SpreizenAsync(a.Drei, MitteVon(a.Drei), spaeter);
                var nachher = a.Drei.AktuelleKamera!.Abstand;
                Messwerte.Notiere(string.Create(Inv, $"Finger 3D (zweiter Finger {(spaeter ? "spaeter" : "gleich")}): Abstand {vorher:F2} -> {nachher:F2}"));
                if (Math.Abs((nachher / vorher) - 1) < 0.2)
                {
                    Fund(string.Create(Inv, $"Finger 3D: zwei Finger zoomen nicht (zweiter Finger {(spaeter ? "spaeter" : "gleich")}, Abstand {vorher:F2} -> {nachher:F2})"));
                }
            }
            // Gegenprobe: ein Finger allein zoomt nicht (die Probe unterscheidet Zoomen vom Schieben)
            var ein = a.Drei.AktuelleKamera!.Abstand;
            await EinFingerAsync(a.Drei, MitteVon(a.Drei));
            if (Math.Abs(a.Drei.AktuelleKamera!.Abstand - ein) > 0.01)
            {
                Fund("Finger 3D Gegenprobe: ein Finger aendert den Zoom (Probe taugt nicht)");
            }
        }
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }

    private static async Task FlaecheAsync(Hauptansicht a, string name, Control c, Func<double> zoom, Func<Point, bool> stueck,
        Func<string> zustand, Action<bool> alt)
    {
        var frei = Suchen(c, x => !stueck(x));
        var aufStueck = Suchen(c, stueck);
        if (frei is null || aufStueck is null)
        {
            Fund(name + " Finger: keine " + (frei is null ? "freie Stelle" : "Stelle mit Stueck") + " fuer die Probe gefunden");
            return;
        }
        var altFaelle = 0;
        foreach (var start in new[] { frei.Value, aufStueck.Value })
        {
            foreach (var spaeter in new[] { false, true })
            {
                var fall = name + " Finger (" + (start == frei.Value ? "freie Flaeche" : "auf Stueck") + ", zweiter Finger " + (spaeter ? "spaeter" : "gleich") + ")";
                var stand = zustand();
                var vorher = zoom();
                await SpreizenAsync(c, start, spaeter);
                var faktor = zoom() / vorher;
                Messwerte.Notiere(string.Create(Inv, $"{fall}: Zoom x {faktor:F2}"));
                if (faktor < 1.5)
                {
                    Fund(string.Create(Inv, $"{fall}: zwei Finger zoomen nicht (x {faktor:F2} statt etwa x 2)"));
                }
                if (zustand() != stand)
                {
                    Fund(fall + ": zwei Finger haben etwas veraendert");
                }
                await EinpassenAsync(c, zoom, vorher);

                // Gegenprobe mit der Auswertung vor S 0.5.1
                alt(true);
                var altVorher = zoom();
                await SpreizenAsync(c, start, spaeter);
                if (zoom() / altVorher < 1.05)
                {
                    altFaelle++;
                }
                alt(false);
                for (var n = 0; n < 3 && zustand() != stand; n++)
                {
                    // die alte Auswertung schob ein Stueck mit dem zweiten Finger: zuruecknehmen, nur Gegenprobe
                    a.Rueckgaengig();
                }
                if (zustand() != stand)
                {
                    Fund(fall + ": Gegenprobe liess sich nicht zuruecknehmen");
                }
                await EinpassenAsync(c, zoom, vorher);
            }
        }
        Messwerte.Notiere(name + " Finger Gegenprobe (Auswertung vor S 0.5.1): " + altFaelle.ToString(Inv) + " von 4 Faellen ohne Zoom");
        if (altFaelle != 4)
        {
            Fund(name + " Finger Gegenprobe: die alte Auswertung zoomte in " + (4 - altFaelle).ToString(Inv) + " Faellen (Probe taugt nicht)");
        }
    }

    /// <summary>Zoom zurueck auf den Wert vor dem Fall (Mausrad-Schritte in der Mitte).</summary>
    private static async Task EinpassenAsync(Control c, Func<double> zoom, double ziel)
    {
        for (var i = 0; i < 40 && Math.Abs(zoom() / ziel - 1) > 0.08; i++)
        {
            var maus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            var p = MitteVon(c);
            c.RaiseEvent(new PointerWheelEventArgs(c, maus, Wurzel(c), c.TranslatePoint(p, Wurzel(c)) ?? p, 0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None,
                new Vector(0, zoom() > ziel ? -1 : 1)));
        }
        await BildAsync(c);
    }

    private static Point MitteVon(Control c) => new(c.Bounds.Width / 2, c.Bounds.Height / 2);

    /// <summary>Stelle im Bild, an der die Bedingung gilt und zwei Finger rechts daneben Platz haben.</summary>
    private static Point? Suchen(Control c, Func<Point, bool> bedingung)
    {
        for (var y = 60.0; y < c.Bounds.Height - 60; y += 12)
        {
            for (var x = 80.0; x < c.Bounds.Width - 180; x += 12)
            {
                var p = new Point(x, y);
                if (bedingung(p))
                {
                    return p;
                }
            }
        }
        return null;
    }

    private static Visual Wurzel(Control c) => (Visual?)TopLevel.GetTopLevel(c) ?? c;

    private static void Druck(Control c, Pointer finger, Point p) =>
        c.RaiseEvent(new PointerPressedEventArgs(c, finger, Wurzel(c), c.TranslatePoint(p, Wurzel(c)) ?? p, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));

    private static void Bewegung(Control c, Pointer finger, Point p) =>
        c.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, c, finger, Wurzel(c), c.TranslatePoint(p, Wurzel(c)) ?? p, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None));

    private static void Los(Control c, Pointer finger, Point p) =>
        c.RaiseEvent(new PointerReleasedEventArgs(c, finger, Wurzel(c), c.TranslatePoint(p, Wurzel(c)) ?? p, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.None));

    /// <summary>Zwei Finger 100 Bildpunkte auseinander, dann auf 200 spreizen; mit "spaeter" bewegt sich der erste Finger
    /// zuerst allein (wie ein Finger, der etwas frueher aufliegt).</summary>
    private static async Task SpreizenAsync(Control c, Point start, bool spaeter)
    {
        var eins = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var zwei = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, false);
        var b = start + new Point(100, 0);
        Druck(c, eins, start);
        if (spaeter)
        {
            Bewegung(c, eins, start + new Point(-10, 0));
            await BildAsync(c);
            Bewegung(c, eins, start);
        }
        Druck(c, zwei, b);
        for (var i = 1; i <= 5; i++)
        {
            Bewegung(c, eins, start + new Point(-10 * i, 0));
            Bewegung(c, zwei, b + new Point(10 * i, 0));
        }
        Los(c, zwei, b + new Point(50, 0));
        Los(c, eins, start + new Point(-50, 0));
        await BildAsync(c);
    }

    private static async Task EinFingerAsync(Control c, Point start)
    {
        var eins = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        Druck(c, eins, start);
        for (var i = 1; i <= 5; i++)
        {
            Bewegung(c, eins, start + new Point(10 * i, 0));
        }
        Los(c, eins, start + new Point(50, 0));
        await BildAsync(c);
    }
}
