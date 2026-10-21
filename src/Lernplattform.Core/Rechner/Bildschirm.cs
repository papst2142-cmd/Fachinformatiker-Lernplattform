using System.Globalization;
using System.Numerics;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3, R3: Bildschirm-Datenvolumen genau wie Python v0.63.2 (fisi_core 3920-4002,
/// screen_report). "23.976" ist nach der Leseregel 23976 und damit zu gross (Meldung statt
/// falscher Rechnung). Vergleichswerte G26.
/// </summary>
public static class Bildschirm
{
    private static string I(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>screen_report(width_text, height_text, depth, fps_text): Datenvolumen und Datenrate.</summary>
    public static string Bericht(string? breiteText, string? hoeheText, string tiefeText, string fpsText)
    {
        var breite = Zahlen.LesenGanz(breiteText, SCREEN_FIELD_WIDTH);
        var hoehe = Zahlen.LesenGanz(hoeheText, SCREEN_FIELD_HEIGHT);
        if (!PythonZeichen.Ganzzahl(tiefeText, out var tiefe))
        {
            throw new RechnerFehler("Bitte gültige Zahlen für Breite, Höhe und Bildrate eingeben.");
        }
        var mitFps = PythonZeichen.Strip(fpsText).Length > 0;
        var fps = mitFps ? Zahlen.Lesen(fpsText, "Bildrate (fps)") : 0.0;
        if (tiefe < 1 || tiefe > 64)
        {
            throw new RechnerFehler("Bitte eine Farbtiefe von 1 bis 64 Bit wählen.");
        }
        if (breite <= 0 || hoehe <= 0)
        {
            throw new RechnerFehler("Breite und Höhe müssen größer als 0 sein.");
        }
        if (breite > 100000)
        {
            throw new RechnerFehler("Die Breite ist zu groß (höchstens 100.000 Pixel).");
        }
        if (hoehe > 100000)
        {
            throw new RechnerFehler("Die Höhe ist zu groß (höchstens 100.000 Pixel).");
        }
        if (fps < 0)
        {
            throw new RechnerFehler("Die Bildrate darf nicht negativ sein.");
        }
        if (fps > SCREEN_FPS_MAX)
        {
            throw new RechnerFehler("Die Bildrate ist zu groß (höchstens " + Zahlen.DeGanz(new BigInteger(SCREEN_FPS_MAX))
                + " Bilder pro Sekunde). Nachkommastellen bitte mit Komma eingeben, zum Beispiel 23,976.");
        }

        var pixel = breite * hoehe;
        var bits = pixel * tiefe;
        // bits ist hoechstens 6,4 * 10^11 und damit als Gleitkommazahl exakt (wie Pythons int / int)
        var bytes = (double)bits / 8;
        var kib = bytes / 1024;
        var mib = kib / 1024;
        var zeilen = new List<string>
        {
            I($"Auflösung             : {breite} x {hoehe} Pixel"),
            "Pixel gesamt          : " + Zahlen.DeGanz(pixel),
            I($"Farbtiefe             : {tiefe} Bit/Pixel"),
            "Datenmenge pro Bild   : " + Zahlen.DeGanz(bits) + " Bit",
            "                      : " + Zahlen.DeGanz(bytes) + " Byte",
            "                      : " + Zahlen.DeZahl(kib) + " KiB",
            "                      : " + Zahlen.DeZahl(mib) + " MiB",
        };
        if (fps > 0)
        {
            var proSekunde = bytes * fps;
            var mbit = proSekunde * 8 / 1_000_000;
            var mibS = proSekunde / (1024 * 1024);
            var gibMin = proSekunde * 60 / (1024.0 * 1024 * 1024);
            zeilen.AddRange([
                "",
                "Bildrate (fps)        : " + Zahlen.DeKurz(fps, 3) + " Bilder/Sekunde",
                "Datenrate             : " + Zahlen.DeZahl(mibS) + " MiB/s",
                "                      : " + Zahlen.DeZahl(mbit) + " Mbit/s",
                "                      : " + Zahlen.DeZahl(gibMin) + " GiB/Minute",
            ]);
        }
        zeilen.AddRange(Zahlen.TausenderHinweis(breiteText, hoeheText, mitFps ? fpsText : ""));
        return string.Join("\n", zeilen);
    }
}
