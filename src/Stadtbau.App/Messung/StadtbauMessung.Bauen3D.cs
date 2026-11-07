using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

using Point = Avalonia.Point;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.7.0 (Weisung P12-45, Plan P12-26b Abschnitt 9): Bauen in der 3D-Ansicht mit nachgestellter Maus und Fingern auf
/// der echten Ebene ueber dem Bild. Schatten folgt ohne Neubau (Messpunkt „3D Schatten folgt“, je Bewegung), gruen auf
/// freier Flaeche, rot auf der Strasse; Setzen per Klick (Messpunkt „3D Stück setzen (bis zum Bild)“), Waehlen und Ziehen
/// mit dem Finger (ein Schritt im Verlauf), Ziehen auf freier Flaeche bewegt nur die Kamera, Kippen bleibt nach einem Neubau,
/// mit „Figur laufen lassen“ wird nichts gebaut. Layout mit sichtbarem Schatten in allen Groessen.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task Bauen3DAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Bauen 3D", null, (24, 16));
        await a.AussenGeprueft;
        a.Karte.SetzenArt = AussenArt.Strasse;
        a.Karte.SetzenAn(12, 8);
        a.Karte.SetzenArt = null;
        await a.AussenGeprueft;
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.DreiZeigen();
        await a.DreiFertig;
        var b = a.Bedienung3D;
        if (!b.IsEffectivelyVisible)
        {
            Fund("Bauen 3D: die Ebene zum Bauen ist nicht zu sehen");
        }
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Bauen 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
            a.ReiterZeigen(innen: false);
            return;
        }
        b.GanzeStadt();
        await BildAsync(b);
        var spiel = a.Drei.Spiel;
        var p = a.Projekt!;

        // Schatten folgt der Maus (gruen auf freier Flaeche), ohne die Szene neu zu bauen
        a.AussenSetzenWaehlenFuerTests(AussenArt.Baum);
        var maus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var hoechst = new List<double>();
        for (var versuch = 0; versuch < 3; versuch++)
        {
            var werte = new List<double>();
            for (var i = 0; i < 6; i++)
            {
                var bild = Bild(a, 3 + i + (versuch * 0.25), 3);
                var uhr = Stopwatch.StartNew();
                Bewegung(b, maus, bild);
                b.InvalidateVisual();
                await BildAsync(b);
                werte.Add(uhr.Elapsed.TotalMilliseconds);
                if (!b.SchattenDa)
                {
                    Fund($"Bauen 3D: kein Schatten unter der Maus bei {bild}");
                }
                else if (!b.SchattenRahmen.Inflate(16).Contains(bild))
                {
                    Fund($"Bauen 3D: Schatten {b.SchattenRahmen} liegt nicht unter der Maus {bild}");
                }
            }
            hoechst.Add(werte.Max());
            Messwerte.Reihe("3D Schatten folgt", hoechst[^1]);
            if (hoechst[^1] <= Grenze)
            {
                break;
            }
            await Task.Delay(200);
        }
        if (hoechst.All(w => w > Grenze))
        {
            Fund(string.Create(Inv, $"3D Schatten folgt: {hoechst.Min():F0} ms ueber Grenze {Grenze:F0} ms; Einzelmessungen {string.Join(", ", hoechst.Select(w => w.ToString("F0", Inv)))} ms"));
        }
        if (!ReferenceEquals(spiel, a.Drei.Spiel))
        {
            Fund("Bauen 3D: der Schatten hat die Szene neu gebaut");
        }
        if (await GeprueftAsync(b) != true)
        {
            Fund("Bauen 3D: Schatten auf freier Flaeche ist nicht gruen (" + b.SchattenText + ")");
        }

        // rot: eine Strasse fast auf der vorhandenen (dieselbe Regel wie die Liste rechts)
        a.AussenSetzenWaehlenFuerTests(AussenArt.Strasse);
        Bewegung(b, maus, Bild(a, 12, 8.4));
        if (await GeprueftAsync(b) != false)
        {
            Fund("Bauen 3D: Schatten einer Strasse auf der Strasse ist nicht rot (" + b.SchattenText + ")");
        }
        else
        {
            Messwerte.Notiere("Bauen 3D: rot: " + b.SchattenText);
        }
        a.Karte.Taste(Key.Escape, KeyModifiers.None);

        // Setzen per Klick bis zum neuen Bild (wiederholbar)
        a.AussenSetzenWaehlenFuerTests(AussenArt.Baum);
        await BildAsync(b);
        var nr = 0;
        await MessenAsync("3D Stück setzen (bis zum Bild)", b, async () =>
        {
            var vorher = Baeume(p);
            Klick(b, Bild(a, 4 + (2 * nr), 12));
            nr++;
            await a.DreiFertig;
            if (Baeume(p) != vorher + 1)
            {
                Fund("Bauen 3D: Klick hat keinen Baum gesetzt");
            }
        }, async () =>
        {
            a.AussenSetzenWaehlenFuerTests(AussenArt.Baum);
            await BildAsync(b);
        });
        a.AussenSetzenWaehlenFuerTests(AussenArt.Baum);
        Klick(b, Bild(a, 18, 12));
        await a.DreiFertig;
        var baum = a.Karte.Auswahl;
        if (baum is not { Art: AussenArt.Baum })
        {
            Fund("Bauen 3D: der gesetzte Baum ist nicht ausgewaehlt");
            a.ReiterZeigen(innen: false);
            return;
        }

        // Waehlen und Ziehen mit dem Finger: ein Schritt im Verlauf
        a.Karte.Waehlen(null);
        var (bx, by) = AussenBearbeitung.Lage(p.AussenStand, baum);
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var start = Bild(a, bx, by);
        Druck(b, finger, start);
        if (a.Karte.Auswahl != baum)
        {
            Fund("Bauen 3D: Antippen waehlt den Baum nicht (" + a.Karte.Auswahl + ")");
        }
        var ziel = Bild(a, bx - 2, by);
        for (var i = 1; i <= 6; i++)
        {
            Bewegung(b, finger, start + ((ziel - start) * (i / 6.0)));
        }
        Los(b, finger, ziel);
        await a.DreiFertig;
        var (nx, ny) = AussenBearbeitung.Lage(p.AussenStand, baum);
        if (Math.Abs(nx - (bx - 2)) > 0.3 || Math.Abs(ny - by) > 0.3)
        {
            Fund(string.Create(Inv, $"Bauen 3D: Ziehen mit dem Finger: Baum bei {nx:F2}/{ny:F2} statt {bx - 2:F2}/{by:F2}"));
        }
        a.Rueckgaengig();
        await a.DreiFertig;
        var (rx, ry) = AussenBearbeitung.Lage(p.AussenStand, baum);
        if (Math.Abs(rx - bx) > 1e-9 || Math.Abs(ry - by) > 1e-9)
        {
            Fund("Bauen 3D: Rueckgaengig nach dem Ziehen bringt den Baum nicht zurueck (kein einzelner Schritt)");
        }

        // Gegenprobe: Ziehen auf freier Flaeche bewegt die Kamera, nicht die Stadt
        var stand = PythonJson.Text(p.AussenStand);
        var k = a.Drei.AktuelleKamera!;
        var kz = (k.ZielX, k.ZielZ);
        var frei = Bild(a, 20, 3);
        if (b.TrefferAn(frei) is not null)
        {
            Fund("Bauen 3D: freie Stelle 20/3 ist nicht frei (Probe taugt nicht)");
        }
        Druck(b, finger, frei);
        for (var i = 1; i <= 5; i++)
        {
            Bewegung(b, finger, frei + new Point(-12 * i, 0));
        }
        Los(b, finger, frei + new Point(-60, 0));
        await BildAsync(b);
        if (PythonJson.Text(p.AussenStand) != stand)
        {
            Fund("Bauen 3D: Ziehen auf freier Flaeche hat die Stadt geaendert");
        }
        if ((k.ZielX, k.ZielZ) == kz)
        {
            Fund("Bauen 3D: Ziehen auf freier Flaeche bewegt die Kamera nicht");
        }

        // Kippen: Knopf und rechte Maustaste; die Neigung bleibt nach dem Neubau
        var n0 = k.Neigung;
        b.Kippen(8);
        if (Math.Abs(k.Neigung - Bauen3D.Kippen(n0, 8)) > 0.01)
        {
            Fund(string.Create(Inv, $"Bauen 3D: Steiler kippt nicht ({n0:F1} -> {k.Neigung:F1})"));
        }
        var rechts = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var mitte = MitteVon(b);
        var n1 = k.Neigung;
        b.RaiseEvent(new PointerPressedEventArgs(b, rechts, Wurzel(b), b.TranslatePoint(mitte, Wurzel(b)) ?? mitte, 0,
            new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed), KeyModifiers.None));
        Bewegung(b, rechts, mitte + new Point(0, -40));
        b.RaiseEvent(new PointerReleasedEventArgs(b, rechts, Wurzel(b), b.TranslatePoint(mitte, Wurzel(b)) ?? mitte, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.RightButtonReleased), KeyModifiers.None, MouseButton.Right));
        if (Math.Abs(k.Neigung - n1) < 1)
        {
            Fund(string.Create(Inv, $"Bauen 3D: rechte Maustaste hoch kippt nicht ({n1:F1} -> {k.Neigung:F1})"));
        }
        var gekippt = k.Neigung;
        a.Karte.Waehlen(baum);
        a.Karte.Verschieben(0.5, 0);
        await a.DreiFertig;
        if (a.Drei.AktuelleKamera is not { } k2 || Math.Abs(k2.Neigung - gekippt) > 0.01)
        {
            Fund("Bauen 3D: nach dem Neubau ist die Kamera nicht mehr gekippt");
        }

        // Gegenprobe: mit „Figur laufen lassen“ setzt ein Tipp nichts
        b.FigurLaufen = true;
        a.AussenSetzenWaehlenFuerTests(AussenArt.Baum);
        var zahl = Baeume(p);
        Klick(b, Bild(a, 6, 14));
        await BildAsync(b);
        if (Baeume(p) != zahl)
        {
            Fund("Bauen 3D: mit „Figur laufen lassen“ wurde trotzdem gebaut");
        }
        b.FigurLaufen = false;

        // Layout mit sichtbarem Schatten
        Bewegung(b, maus, Bild(a, 6, 14));
        await GeprueftAsync(b);
        await AlleGroessenAsync(a, f, "3D bauen");
        a.Karte.Taste(Key.Escape, KeyModifiers.None);
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }

    private static int Baeume(Projekt p) => AussenBearbeitung.Alle(p.AussenStand).Count(z => z.Stelle.Art == AussenArt.Baum);

    /// <summary>Bildpunkt eines Punkts am Boden (Karte x, y) in der Ebene ueber der 3D-Ansicht.</summary>
    private static Point Bild(Hauptansicht a, double x, double y)
    {
        var b = a.Bedienung3D;
        var k = a.Drei.AktuelleKamera!;
        var q = k.Bildpunkt(new Vector3((float)x, 0, (float)y), (float)b.Bounds.Width, (float)b.Bounds.Height);
        return q is { } r ? new Point(r.X, r.Y) : new Point(-1, -1);
    }

    private static void Klick(Control c, Point p)
    {
        var maus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        Druck(c, maus, p);
        Los(c, maus, p);
    }

    /// <summary>Auf das Ergebnis der Schatten-Pruefung warten (hoechstens 10 s).</summary>
    private static async Task<bool?> GeprueftAsync(DreiBedienung b)
    {
        for (var i = 0; i < 200 && b.SchattenDa && b.Passt is null; i++)
        {
            await Task.Delay(50);
        }
        await BildAsync(b);
        return b.Passt;
    }
}
