using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Lernplattform.Messung;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.52.0 (Weisung P12-243 Punkt 2, Sammelliste 337): der gesperrte Knopf „Hügel“ (16 Huegel) muss sich sichtbar vom
/// freien unterscheiden. Gemessen im gezeichneten Bild: mittlere Farbe der Knopf-Flaeche gesperrt gegen frei (derselbe
/// Knopf), PC und Tablet, hell und dunkel; Unterschied mindestens <see cref="GesperrtUnterschied"/>. Der Grund darunter hat
/// mindestens Kontrast <see cref="GrundKontrast"/>. Jeder gesperrte Knopf und Schalter in einer Katalog-Flaeche ist blasser.
/// Gegenprobe: gesperrt mit und ohne die Klasse „katalog“ (wie bis S 0.51.0) unterscheiden sich mindestens um
/// <see cref="GesperrtUnterschied"/> (die Messung sieht die Klasse).
/// </summary>
public static partial class StadtbauMessung
{
    /// <summary>Mittlerer Farbunterschied (0–255 je Kanal, gemittelt), ab dem gesperrt und frei sichtbar verschieden sind.</summary>
    private const double GesperrtUnterschied = 20;

    /// <summary>Mindestkontrast des Grunds unter dem Knopf (WCAG AA fuer Text).</summary>
    private const double GrundKontrast = 4.5;

    private static Color[] BildPixel(Control wurzel, out int breite)
    {
        var groesse = new PixelSize(Math.Max(1, (int)wurzel.Bounds.Width), Math.Max(1, (int)wurzel.Bounds.Height));
        using var bild = new RenderTargetBitmap(groesse, new Vector(96, 96));
        bild.Render(wurzel);
        breite = groesse.Width;
        var zeile = breite * 4;
        var roh = new byte[zeile * groesse.Height];
        var speicher = Marshal.AllocHGlobal(roh.Length);
        try
        {
            bild.CopyPixels(new PixelRect(0, 0, breite, groesse.Height), speicher, roh.Length, zeile);
            Marshal.Copy(speicher, roh, 0, roh.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(speicher);
        }
        var bgra = bild.Format is null || bild.Format == PixelFormat.Bgra8888;
        var farben = new Color[breite * groesse.Height];
        for (var i = 0; i < farben.Length; i++)
        {
            var o = i * 4;
            farben[i] = bgra ? Color.FromArgb(roh[o + 3], roh[o + 2], roh[o + 1], roh[o]) : Color.FromArgb(roh[o + 3], roh[o], roh[o + 1], roh[o + 2]);
        }
        return farben;
    }

    /// <summary>Pixel im Rechteck des Controls <paramref name="c"/> (im Bild der Hauptansicht).</summary>
    private static List<Color> PixelVon(Hauptansicht a, Control c)
    {
        var bild = BildPixel(a, out var breite);
        var hoehe = bild.Length / breite;
        var p = c.TranslatePoint(new Point(0, 0), a) ?? new Point(0, 0);
        var liste = new List<Color>();
        for (var y = Math.Max(0, (int)p.Y + 1); y < Math.Min(hoehe, (int)(p.Y + c.Bounds.Height) - 1); y++)
        {
            for (var x = Math.Max(0, (int)p.X + 1); x < Math.Min(breite, (int)(p.X + c.Bounds.Width) - 1); x++)
            {
                liste.Add(bild[(y * breite) + x]);
            }
        }
        return liste;
    }

    private static (double R, double G, double B) Mittel(List<Color> px) =>
        px.Count == 0 ? (0, 0, 0) : (px.Average(c => c.R), px.Average(c => c.G), px.Average(c => c.B));

    private static double Unterschied((double R, double G, double B) x, (double R, double G, double B) y) =>
        (Math.Abs(x.R - y.R) + Math.Abs(x.G - y.G) + Math.Abs(x.B - y.B)) / 3;

    private static double Leuchte(Color c)
    {
        static double K(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return (0.2126 * K(c.R)) + (0.7152 * K(c.G)) + (0.0722 * K(c.B));
    }

    /// <summary>Unterschied gesperrt/frei desselben Knopfs im Bild (der Knopf wird kurz frei geschaltet und wieder gesperrt).</summary>
    private static async Task<double> GesperrtFreiAsync(Hauptansicht a, Button k)
    {
        k.IsEnabled = false;
        await BildAsync(a);
        var gesperrt = Mittel(PixelVon(a, k));
        k.IsEnabled = true;
        await BildAsync(a);
        var frei = Mittel(PixelVon(a, k));
        k.IsEnabled = false;
        await BildAsync(a);
        return Unterschied(gesperrt, frei);
    }

    private static async Task KatalogGesperrtAsync(Hauptansicht a, Window? f, Button hk)
    {
        var werte = new List<string>();
        foreach (var dunkel in new[] { false, true })
        {
            if (a.Dunkel != dunkel)
            {
                a.FarbeWechseln();
            }
            foreach (var (name, b, h) in Groessen)
            {
                await GroesseAsync(a, f, b, h);
                a.GruppeOeffnenFuer(hk);
                hk.BringIntoView();
                await BildAsync(a);
                var wo = name + " " + (dunkel ? "dunkel" : "hell");
                var u = await GesperrtFreiAsync(a, hk);
                werte.Add(wo + " " + u.ToString("F1", Inv));
                if (u < GesperrtUnterschied)
                {
                    Fund(string.Create(Inv, $"Katalog gesperrt {wo}: Knopf „Hügel“ gesperrt und frei nur {u:F1} verschieden (mindestens {GesperrtUnterschied:F0})"));
                }
                if (a.HuegelGrundZeile is { IsEffectivelyVisible: true } zeile)
                {
                    var px = PixelVon(a, zeile);
                    if (px.Count > 0)
                    {
                        var l = px.Select(Leuchte).ToList();
                        var kontrast = (l.Max() + 0.05) / (l.Min() + 0.05);
                        werte.Add(string.Create(Inv, $"Grund {wo} Kontrast {kontrast:F1}"));
                        if (kontrast < GrundKontrast)
                        {
                            Fund(string.Create(Inv, $"Katalog gesperrt {wo}: Grund unter „Hügel“ hat Kontrast {kontrast:F1} (mindestens {GrundKontrast:F1})"));
                        }
                    }
                }
                // jeder gesperrte Knopf und Schalter in einer Katalog-Flaeche ist blasser (auch kuenftige)
                foreach (var c in a.GetVisualDescendants().OfType<Control>().Where(c => c is Button or CheckBox && !c.IsEnabled && c.IsEffectivelyVisible
                    && c.GetVisualAncestors().OfType<Control>().Any(v => v.Classes.Contains(Hauptansicht.KatalogKlasse))))
                {
                    if (c.Opacity > 0.5)
                    {
                        Fund($"Katalog gesperrt {wo}: gesperrter Knopf „{Avalonia.Automation.AutomationProperties.GetName(c)}“ sieht aus wie frei");
                    }
                }
            }
        }
        if (a.Dunkel)
        {
            a.FarbeWechseln();
        }
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // Gegenprobe: dieselbe Messung sieht, ob die Klasse wirkt. Gesperrt mit Klasse gegen gesperrt ohne Klasse (wie bis
        // S 0.51.0) muss sich mindestens um GesperrtUnterschied unterscheiden; gesperrt gegen frei ohne Klasse steht nur als
        // Angabe (je nach Zustand davor, etwa Zeiger ueber dem Knopf, zeigt Fluent einen kleinen Unterschied).
        a.GruppeOeffnenFuer(hk);
        hk.BringIntoView();
        hk.IsEnabled = false;
        await BildAsync(a);
        var mitKlasse = Mittel(PixelVon(a, hk));
        a.KatalogGesperrtAltFuerTests(true);
        try
        {
            await BildAsync(a);
            var ohneKlasse = Mittel(PixelVon(a, hk));
            var wirkt = Unterschied(mitKlasse, ohneKlasse);
            var alt = await GesperrtFreiAsync(a, hk);
            werte.Add(string.Create(Inv, $"Gegenprobe gesperrt mit/ohne Klasse {wirkt:F1}, alt gesperrt/frei {alt:F1} (Angabe, Zeiger ueber dem Knopf {hk.IsPointerOver})"));
            if (wirkt < GesperrtUnterschied)
            {
                Fund(string.Create(Inv, $"Katalog gesperrt Gegenprobe: mit und ohne Klasse nur {wirkt:F1} verschieden (mindestens {GesperrtUnterschied:F0}), die Messung prueft nichts"));
            }
        }
        finally
        {
            a.KatalogGesperrtAltFuerTests(false);
        }
        await BildAsync(a);
        Messwerte.Notiere("Katalog gesperrt (Huegel bei 16): " + string.Join("; ", werte));
    }
}
