using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lernplattform.Design;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W2 Nachtrag (Weisung P8-3b, Sammelliste 113): Farbverlauf der Fortschrittsringe pruefen.
/// Je Anteil 15, 50 und 90 % einen Ring (120 px, Dicke 11, Farben accent und purple der aktuellen
/// Farbwelt) zeichnen und auf der Mittellinie des Bogens Grad fuer Grad die Farbe lesen: Sie muss dem
/// Verlauf von a (12 Uhr) nach b (Bogenende) folgen, in der Kappe am Anfang a und in der Kappe am Ende
/// b zeigen, hinter dem Bogen ring_bg. Vor der Korrektur lag die Mitte des Verlaufs neben der Ringmitte
/// (eine Farbe und ein Dreieck in der anderen), das ergab hier Abweichungen ueber 100.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Hoechste erlaubte Abweichung je Farbkanal (0-255) zwischen gelesenem und erwartetem Pixel.</summary>
    public const int RingToleranz = 24;

    private static int RingVerlaufPruefen()
    {
        const int groesse = 120;
        const double dicke = 11;
        var a = Color.Parse(FarbenAnwenden.Hex("accent"));
        var b = Color.Parse(FarbenAnwenden.Hex("purple"));
        var hinten = Color.Parse(FarbenAnwenden.Hex("ring_bg"));
        var funde = 0;
        foreach (var anteil in new[] { 0.15, 0.5, 0.9 })
        {
            var ring = new VerlaufRing(anteil, "accent", "purple", dicke) { Width = groesse, Height = groesse };
            ring.Measure(new Size(groesse, groesse));
            ring.Arrange(new Rect(0, 0, groesse, groesse));
            using var bild = new RenderTargetBitmap(new PixelSize(groesse, groesse), new Vector(96, 96));
            bild.Render(ring);
            var pixel = Pixel(bild);
            var radius = (groesse - dicke) / 2;
            var winkel = anteil * 360;
            var schlimmste = (Abweichung: 0, Grad: 0.0, Soll: "");
            void Pruefe(double grad, Color soll, string art)
            {
                var rad = (grad - 90) * Math.PI / 180;
                var x = (int)Math.Round(groesse / 2.0 + radius * Math.Cos(rad) - 0.5);
                var y = (int)Math.Round(groesse / 2.0 + radius * Math.Sin(rad) - 0.5);
                var ist = pixel[(y * groesse) + x];
                var abweichung = Math.Max(Math.Abs(ist.R - soll.R), Math.Max(Math.Abs(ist.G - soll.G), Math.Abs(ist.B - soll.B)));
                if (abweichung > schlimmste.Abweichung)
                {
                    schlimmste = (abweichung, grad, art + " " + soll + " ist " + ist);
                }
            }
            // im Bogen (ohne die ersten und letzten 3 Grad, dort beginnen die Kappen)
            for (var grad = 3.0; grad <= winkel - 3; grad += 1)
            {
                Pruefe(grad, Mische(a, b, grad / winkel), "Verlauf");
            }
            Pruefe(-3, a, "Kappe am Anfang");
            Pruefe(winkel + 3, b, "Kappe am Ende");
            Pruefe(winkel + (360 - winkel) / 2, hinten, "hinter dem Bogen");
            var titel = string.Create(CultureInfo.InvariantCulture, $"Ring {anteil * 100:F0} %: Farbverlauf");
            Messwerte.Notiere(string.Create(CultureInfo.InvariantCulture,
                $"{titel}: groesste Abweichung {schlimmste.Abweichung} (Grenze {RingToleranz}) bei {schlimmste.Grad:F0} Grad"));
            if (schlimmste.Abweichung > RingToleranz)
            {
                Messwerte.Notiere(string.Create(CultureInfo.InvariantCulture,
                    $"  FEHLER: {titel}: Abweichung {schlimmste.Abweichung} UEBER {RingToleranz} bei {schlimmste.Grad:F0} Grad ({schlimmste.Soll})"));
                funde++;
            }
        }
        return funde;
    }

    private static Color Mische(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + ((b.R - a.R) * t)), (byte)Math.Round(a.G + ((b.G - a.G) * t)), (byte)Math.Round(a.B + ((b.B - a.B) * t)));

    /// <summary>Pixel des Bildes (Bgra8888 vormultipliziert; der Ring ist an den gelesenen Stellen deckend).</summary>
    private static Color[] Pixel(RenderTargetBitmap bild)
    {
        var breite = bild.PixelSize.Width;
        var hoehe = bild.PixelSize.Height;
        var zeile = breite * 4;
        var roh = new byte[zeile * hoehe];
        var speicher = Marshal.AllocHGlobal(roh.Length);
        try
        {
            bild.CopyPixels(new PixelRect(0, 0, breite, hoehe), speicher, roh.Length, zeile);
            Marshal.Copy(speicher, roh, 0, roh.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(speicher);
        }
        var farben = new Color[breite * hoehe];
        var bgra = bild.Format is null || bild.Format == PixelFormat.Bgra8888;
        for (var i = 0; i < farben.Length; i++)
        {
            var o = i * 4;
            farben[i] = bgra ? Color.FromArgb(roh[o + 3], roh[o + 2], roh[o + 1], roh[o]) : Color.FromArgb(roh[o + 3], roh[o], roh[o + 1], roh[o + 2]);
        }
        return farben;
    }
}
