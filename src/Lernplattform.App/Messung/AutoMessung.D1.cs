using System.Globalization;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;

namespace Lernplattform.Messung;

/// <summary>
/// Welle D1 „Dreiecke sparen“ (Weisung P12-143, Nico P12-118 Frage 3 = a, Sammelliste 259): neben der Gesamtansicht misst
/// der Start-Test jetzt auch die <b>Nahansicht</b> mit derselben Grenze (160 000 Dreiecke, 40 Zeichenaufrufe). Nahansicht =
/// normale Spielkamera: Abstand 4, 6, 10 und 16 (Neigung der Stadt), Blick nach Norden und nach Sueden, je auf 9 Stellen der
/// Stadt (Viertel, Mitte, Dreiviertel in beiden Richtungen). Gewertet wird das teuerste Bild; alle Einzelwerte stehen in der
/// Ausgabe. Gegenprobe: ohne Sparen (<see cref="StadtFlaeche.OhneSparenFuerTests"/>: jedes Haus gezeichnet, keine
/// Fernformen) ist dasselbe teuerste Bild teurer, und die Gesamtansicht ohne Fernformen ebenso.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Kameraabstaende der Nahansicht (normale Spielkamera, Plan P12-79 Teil D).</summary>
    public static readonly float[] NahAbstaende = [4f, 6f, 10f, 16f];

    private static async Task<int> NahansichtAsync(SpielSeite seite)
    {
        var inv = CultureInfo.InvariantCulture;
        var flaeche = seite.Flaeche;
        if (flaeche.AktuelleKamera is not { } kamera || seite.Spiel is not { } spiel)
        {
            Messwerte.Notiere("  FEHLER: Spiel Nahansicht: keine Kamera");
            return 1;
        }
        var funde = 0;
        var (abstand, drehung, zx, zz) = (kamera.Abstand, kamera.Drehung, kamera.ZielX, kamera.ZielZ);
        var szene = spiel.Szene;
        async Task<(long Dreiecke, int Aufrufe)> BildAsync(float ab, float dreh, float x, float z)
        {
            kamera.Drehung = dreh;
            kamera.Zoomen(kamera.Abstand / ab);
            kamera.Setzen(x, z);
            await flaeche.NaechstesBildAsync();
            await flaeche.NaechstesBildAsync();
            return (flaeche.LetzteDreiecke, flaeche.LetzteZeichenaufrufe);
        }
        var werte = new List<string>();
        (long Dreiecke, int Aufrufe, float Ab, float Dreh, float X, float Z) teuerstes = (-1, 0, 0, 0, 0, 0);
        var aufrufeMax = 0;
        try
        {
            foreach (var ab in NahAbstaende)
            {
                foreach (var dreh in new[] { 0f, 180f })
                {
                    var zeile = new List<string>();
                    for (var i = 1; i <= 3; i++)
                    {
                        for (var j = 1; j <= 3; j++)
                        {
                            var (x, z) = (szene.Breite * i / 4f, szene.Tiefe * j / 4f);
                            var (d, a) = await BildAsync(ab, dreh, x, z);
                            zeile.Add(string.Create(inv, $"{d}/{a}"));
                            aufrufeMax = Math.Max(aufrufeMax, a);
                            if (d > teuerstes.Dreiecke)
                            {
                                teuerstes = (d, a, ab, dreh, x, z);
                            }
                        }
                    }
                    werte.Add(string.Create(inv, $"Abstand {ab:F0}, Drehung {dreh:F0}: {string.Join(" ", zeile)}"));
                }
            }
            foreach (var w in werte)
            {
                Messwerte.Notiere("Spiel: Nahansicht Einzelwerte (Dreiecke/Aufrufe): " + w);
            }
            Messwerte.Notiere(string.Create(inv,
                $"Spiel: Nahansicht teuerstes Bild (Abstand {teuerstes.Ab:F0}, Drehung {teuerstes.Dreh:F0}, bei {teuerstes.X:F1}/{teuerstes.Z:F1}): {teuerstes.Dreiecke} Dreiecke, hoechstens {aufrufeMax} Zeichenaufrufe (Grenzen {GesamtDreieckeGrenze} Dreiecke, {GesamtAufrufeGrenze} Aufrufe)"));
            if (teuerstes.Dreiecke > GesamtDreieckeGrenze || aufrufeMax > GesamtAufrufeGrenze || teuerstes.Dreiecke <= 0)
            {
                Messwerte.Notiere(string.Create(inv,
                    $"  FEHLER: Spiel Nahansicht: {teuerstes.Dreiecke} Dreiecke, {aufrufeMax} Zeichenaufrufe (Grenzen {GesamtDreieckeGrenze}, {GesamtAufrufeGrenze}; Abstand {teuerstes.Ab:F0}, Drehung {teuerstes.Dreh:F0}, bei {teuerstes.X:F1}/{teuerstes.Z:F1})"));
                funde++;
            }
            // Gegenprobe: dasselbe Bild ohne Sparen (alle Haeuser, keine Fernform) und die Gesamtansicht ohne Fernformen
            var (nahMit, _) = await BildAsync(teuerstes.Ab, teuerstes.Dreh, teuerstes.X, teuerstes.Z);
            var (fernMit, _) = await BildAsync(Kamera.AbstandMax, 0f, szene.Breite / 2, szene.Tiefe / 2);
            StadtFlaeche.OhneSparenFuerTests = true;
            var (nahOhne, _) = await BildAsync(teuerstes.Ab, teuerstes.Dreh, teuerstes.X, teuerstes.Z);
            var (fernOhne, _) = await BildAsync(Kamera.AbstandMax, 0f, szene.Breite / 2, szene.Tiefe / 2);
            StadtFlaeche.OhneSparenFuerTests = false;
            var text = string.Create(inv, $"Nahansicht {nahMit} mit Sparen, {nahOhne} ohne; Gesamtansicht {fernMit} mit Sparen, {fernOhne} ohne");
            Messwerte.Notiere("Spiel: Nahansicht Gegenprobe ohne Sichtkegel und Fernformen: " + text);
            if (!(nahMit < nahOhne && fernMit < fernOhne))
            {
                Messwerte.Notiere("  FEHLER: Spiel Nahansicht Gegenprobe: Sparen wirkt nicht: " + text);
                funde++;
            }
        }
        finally
        {
            StadtFlaeche.OhneSparenFuerTests = false;
            kamera.Drehung = drehung;
            kamera.Zoomen(kamera.Abstand / abstand);
            kamera.Setzen(zx, zz);
        }
        return funde;
    }
}
