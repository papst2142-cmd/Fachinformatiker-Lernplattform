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
    /// <summary>Welle Grafik G1: teuerstes Nahbild je gemessener Stufe (Dreiecke, Aufrufe) fuer den Vergleich der Stufen.</summary>
    private static readonly Dictionary<Grafikstufe, (long Dreiecke, int Aufrufe)> NahTeuerstes = [];

    /// <summary>Kameraabstaende der Nahansicht (normale Spielkamera, Plan P12-79 Teil D).</summary>
    public static readonly float[] NahAbstaende = [4f, 6f, 10f, 16f];

    /// <remarks>
    /// Welle Grafik G1 (Weisung P12-182): <paramref name="stufe"/> = gemessene Grafikstufe mit ihrer Grenze (Niedrig
    /// 125 000 / 32, Mittel 160 000 / 40, Hoch 200 000 / 48); die Gegenprobe ohne Sparen nur bei Mittel.
    /// </remarks>
    /// <summary>
    /// Befristete Ausnahme bis Sammelliste 327 (Nico 13.11.2026, Entscheidungskarte zu W4b): nur Stufe Niedrig, nur Stadt 1
    /// in Fassung 1 (Nicos Stadt). Faellt mit der Welle 327 weg; Gegenprobe: Mittel, Hoch und Fassung 0 sind nie betroffen.
    /// </summary>
    internal static bool Ausnahme327(Grafikstufe stufe, StadtSpiel spiel) =>
        stufe == Grafikstufe.Niedrig && spiel.Stadt.Fassung is { Nummer: 1 } f && f.StadtId == Core.Stadtbau.AktiveFassung.Stadt1;

    private static async Task<int> NahansichtAsync(SpielSeite seite, Grafikstufe stufe = Grafikstufe.Mittel)
    {
        var inv = CultureInfo.InvariantCulture;
        var werteStufe = Grafikstufen.Werte(stufe);
        var (grenzeDreiecke, grenzeAufrufe) = Grafikstufen.Grenze(stufe);
        var name = stufe == Grafikstufe.Mittel ? "Spiel: Nahansicht" : "Spiel: Nahansicht " + GrafikTexte.Name(stufe);
        var flaeche = seite.Flaeche;
        if (flaeche.AktuelleKamera is not { } kamera || seite.Spiel is not { } spiel)
        {
            Messwerte.Notiere("  FEHLER: Spiel Nahansicht: keine Kamera");
            return 1;
        }
        var funde = 0;
        var (abstand, drehung, zx, zz) = (kamera.Abstand, kamera.Drehung, kamera.ZielX, kamera.ZielZ);
        var szene = spiel.Szene;
        // Welle D2 (Weisung P12-157): die Zaehlung ohne Grafikkarte muss jedes gezeichnete Bild genau treffen
        var zaehlung = new Core.Stadtbau.StadtbauGrenzen.Draussen(szene, szene.Vorbereiten());
        var abweichungen = new List<string>();
        var verglichen = 0;
        async Task<(long Dreiecke, int Aufrufe)> BildAsync(float ab, float dreh, float x, float z)
        {
            kamera.Drehung = dreh;
            kamera.Zoomen(kamera.Abstand / ab);
            kamera.Setzen(x, z);
            await flaeche.NaechstesBildAsync();
            await flaeche.NaechstesBildAsync();
            if (flaeche.InstancingDa && ReferenceEquals(spiel.Szene, szene))
            {
                var fest = zaehlung.Bild(flaeche.LetzteSichtProj, flaeche.LetzterAbstand, StadtFlaeche.OhneSparenFuerTests, werteStufe);
                var bewegt = Core.Stadtbau.StadtbauSpiel.Bewegt(spiel, werteStufe.Fern(flaeche.LetzterAbstand), StadtFlaeche.AutosHoechstens, halb: werteStufe.AutosHalb);
                verglichen++;
                if (fest != (flaeche.LetzteStadtDreiecke, flaeche.LetzteStadtAufrufe)
                    || (fest.Dreiecke + bewegt.Dreiecke, fest.Aufrufe + bewegt.Aufrufe) != (flaeche.LetzteDreiecke, flaeche.LetzteZeichenaufrufe))
                {
                    abweichungen.Add(string.Create(inv, $"Abstand {ab:F0}, Drehung {dreh:F0}, bei {x:F1}/{z:F1}: gezeichnet {flaeche.LetzteStadtDreiecke}/{flaeche.LetzteStadtAufrufe} (ganz {flaeche.LetzteDreiecke}/{flaeche.LetzteZeichenaufrufe}), gezaehlt {fest.Dreiecke}/{fest.Aufrufe} (ganz {fest.Dreiecke + bewegt.Dreiecke}/{fest.Aufrufe + bewegt.Aufrufe})"));
                }
            }
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
                Messwerte.Notiere(name + " Einzelwerte (Dreiecke/Aufrufe): " + w);
            }
            Messwerte.Notiere(string.Create(inv,
                $"{name} teuerstes Bild (Abstand {teuerstes.Ab:F0}, Drehung {teuerstes.Dreh:F0}, bei {teuerstes.X:F1}/{teuerstes.Z:F1}): {teuerstes.Dreiecke} Dreiecke, hoechstens {aufrufeMax} Zeichenaufrufe (Grenzen {grenzeDreiecke} Dreiecke, {grenzeAufrufe} Aufrufe)"));
            if (teuerstes.Dreiecke > grenzeDreiecke && aufrufeMax <= grenzeAufrufe && Ausnahme327(stufe, spiel))
            {
                // Befristete Ausnahme (Nico 13.11.2026, Entscheidungskarte zu W4b): Stufe Niedrig in Nicos Stadt 1
                // (Fassung 1) liegt bis zur Welle Sammelliste 327 ueber ihrer Grenze; nur Angabe, die Grenze selbst und
                // alle anderen Stufen (auch Niedrig in Fassung 0) bleiben streng, Aufrufe ebenso
                Messwerte.Notiere(string.Create(inv,
                    $"Spiel: {name}: INFO befristet bis Sammelliste 327 (Nico 13.11.2026): {teuerstes.Dreiecke} Dreiecke ueber der Grenze {grenzeDreiecke} ({100.0 * teuerstes.Dreiecke / grenzeDreiecke:F1} %), nur Angabe"));
            }
            else if (teuerstes.Dreiecke > grenzeDreiecke || aufrufeMax > grenzeAufrufe || teuerstes.Dreiecke <= 0)
            {
                Messwerte.Notiere(string.Create(inv,
                    $"  FEHLER: {name}: {teuerstes.Dreiecke} Dreiecke, {aufrufeMax} Zeichenaufrufe (Grenzen {grenzeDreiecke}, {grenzeAufrufe}; Abstand {teuerstes.Ab:F0}, Drehung {teuerstes.Dreh:F0}, bei {teuerstes.X:F1}/{teuerstes.Z:F1})"));
                funde++;
            }
            NahTeuerstes[stufe] = (teuerstes.Dreiecke, aufrufeMax);
            if (stufe != Grafikstufe.Mittel)
            {
                Messwerte.Notiere(string.Create(inv, $"{name}: Zaehlung ohne Grafikkarte: {verglichen} Bilder verglichen, {abweichungen.Count} Abweichungen"));
                foreach (var a in abweichungen.Take(10))
                {
                    Messwerte.Notiere("  FEHLER: " + name + " Zaehlung ohne Grafikkarte weicht ab: " + a);
                }
                return funde + (abweichungen.Count > 0 ? 1 : 0);
            }
            // Gegenprobe: dasselbe Bild ohne Sparen (alle Haeuser, keine Fernform) und die Gesamtansicht ohne Fernformen
            var (nahMit, _) = await BildAsync(teuerstes.Ab, teuerstes.Dreh, teuerstes.X, teuerstes.Z);
            var (fernMit, _) = await BildAsync(Kamera.AbstandMax, 0f, szene.Breite / 2, szene.Tiefe / 2);
            StadtFlaeche.OhneSparenFuerTests = true;
            var (nahOhne, _) = await BildAsync(teuerstes.Ab, teuerstes.Dreh, teuerstes.X, teuerstes.Z);
            var (fernOhne, _) = await BildAsync(Kamera.AbstandMax, 0f, szene.Breite / 2, szene.Tiefe / 2);
            StadtFlaeche.OhneSparenFuerTests = false;
            var text = string.Create(inv, $"Nahansicht {nahMit} mit Sparen, {nahOhne} ohne; Gesamtansicht {fernMit} mit Sparen, {fernOhne} ohne");
            if (!flaeche.InstancingDa)
            {
                Messwerte.Notiere("Spiel: Zaehlung ohne Grafikkarte nicht verglichen: Treiber ohne Instancing (" + flaeche.GlKurz + ")");
            }
            else
            {
                // Gegenprobe der Zaehlung: ohne Sichtkegel gezaehlt trifft sie das teuerste Nahbild nicht
                var (nahWieder, _) = await BildAsync(teuerstes.Ab, teuerstes.Dreh, teuerstes.X, teuerstes.Z);
                var gegen = zaehlung.Bild(flaeche.LetzteSichtProj, flaeche.LetzterAbstand, ohneSparen: true);
                var gezaehlt = zaehlung.Bild(flaeche.LetzteSichtProj, flaeche.LetzterAbstand);
                Messwerte.Notiere(string.Create(inv,
                    $"Spiel: Zaehlung ohne Grafikkarte: {verglichen} Bilder verglichen, {abweichungen.Count} Abweichungen; Gegenprobe ohne Sichtkegel gezaehlt {gegen.Dreiecke} statt {gezaehlt.Dreiecke} (gezeichnet {flaeche.LetzteStadtDreiecke})"));
                foreach (var a in abweichungen.Take(10))
                {
                    Messwerte.Notiere("  FEHLER: Spiel Zaehlung ohne Grafikkarte weicht ab: " + a);
                }
                funde += abweichungen.Count > 0 ? 1 : 0;
                if (gegen.Dreiecke == flaeche.LetzteStadtDreiecke || nahWieder <= 0)
                {
                    Messwerte.Notiere("  FEHLER: Spiel Zaehlung ohne Grafikkarte: Gegenprobe ohne Sichtkegel trifft das Bild auch");
                    funde++;
                }
            }
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
