using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.98.0 (Weisung P12-315, Sammelliste 391): Touch-Probe im Emulator (nur die Emulator-Probe der CI ruft sie, aus der
/// Probe-APK mit <c>-p:UpdateProbe=true</c>). Das Programm oeffnet ein frisches Spiel in 3D, stellt die Figur draussen an eine
/// Stelle mit freiem Weg in Blickrichtung der Kamera und meldet in Bildpunkten des Bildschirms, wo die Daumen der Sticks und ein
/// Tipp-Ziel liegen (Zeile „BEREIT“). Die echten Finger setzt <c>werkzeuge/android/touch_probe.sh</c> mit <c>adb shell input</c>;
/// das Programm meldet danach jeden ruhigen Stand (Zeile „STAND“: Stelle der Figur, waagrechter Blick der Kamera, geht sie) und
/// ob sie losgeht („GEHT“). Das Urteil faellt <c>werkzeuge/android/touch_urteil.py</c> (mit Selbsttest und Gegenprobe).
/// <paramref name="umkehren"/> schaltet „Umsehen links/rechts umkehren“ ein (zweiter Start der Probe).
/// </summary>
public static partial class AutoMessung
{
    public static async Task TouchProbeAsync(MainView ansicht, Action<string> melden, bool umkehren)
    {
        var inv = CultureInfo.InvariantCulture;
        try
        {
            melden("START umkehren " + (umkehren ? "an" : "aus"));
            Umsehbedienung.LinksRechts = umkehren;
            Umsehbedienung.ObenUnten = false;
            var seite = await SpielSeiteAsync(ansicht);
            if (seite?.Spiel is null)
            {
                melden("FEHLER kein Spiel");
                return;
            }
            var gl = await GlAbwartenAsync(seite);
            melden("3D " + (gl ? "ja" : "nein"));
            if (!gl)
            {
                melden("FEHLER kein 3D");
                return;
            }
            var spiel = Frisch(seite);
            if (!seite.HinweisZu)
            {
                seite.HinweisUmschalten();
            }
            var fl = seite.Flaeche;
            // Querformat und Vollbild abwarten (die Seite dreht den Bildschirm), dann die Lage festlegen
            for (var i = 0; i < 40; i++)
            {
                await fl.NaechstesBildAsync();
                await Task.Delay(100);
            }
            var sz = spiel.Szene;
            bool Frei(double x, double z) => sz.Laufraster.KannStehen(x, z) && !sz.Laufraster.FeinGesperrt(x, z);
            // eine Stelle nahe dem Start mit freiem Kreis (Radius 2): dort ist jede Richtung frei
            var (sx, sz0) = ((double)spiel.FigurX, (double)spiel.FigurZ);
            (double X, double Z)? stelle = null;
            for (var r = 0.0; r < 12 && stelle is null; r += 0.5)
            {
                for (var w = 0; w < 360 && stelle is null; w += 15)
                {
                    var (x, z) = (sx + (r * Math.Sin(w * Math.PI / 180)), sz0 + (r * Math.Cos(w * Math.PI / 180)));
                    if (Enumerable.Range(0, 24).All(j => Enumerable.Range(0, 5).All(t => Frei(x + (t * 0.5 * Math.Sin(j * Math.PI / 12)), z + (t * 0.5 * Math.Cos(j * Math.PI / 12))))))
                    {
                        stelle = (x, z);
                    }
                }
            }
            if (stelle is not { } st || !spiel.GeheZu(st.X, st.Z))
            {
                melden("FEHLER keine freie Stelle");
                return;
            }
            spiel.SofortAnkommen();
            fl.KameraAufFigur();
            for (var i = 0; i < 10; i++)
            {
                await fl.NaechstesBildAsync();
            }
            var k = fl.AktuelleKamera!;
            var b = fl.StickBereich;
            var oben = TopLevel.GetTopLevel(fl);
            var skala = oben?.RenderScaling ?? 1;
            (int X, int Y) Pixel(Point p)
            {
                var q = fl.TranslatePoint(p, oben!) ?? p;
                return ((int)Math.Round(q.X * skala), (int)Math.Round(q.Y * skala));
            }
            var yUnten = b.Hoehe - b.Unten - (b.FreiHoehe / 4);
            var links = Pixel(new Point(b.Breite / 4, yUnten));
            var rechts = Pixel(new Point(b.Breite * 3 / 4, yUnten));
            var radius = (int)Math.Round(Sticks.Radius * skala);
            // Tipp-Ziel: ein Haus in Blickrichtung, das unten im Bild steht (wie der Start-Test), sonst der Punkt des linken Daumens
            var (vx, vz) = Steuerung.Richtung(1, 0, k.Drehung);
            var tipp = links;
            foreach (var d in new[] { 1.5, 2.0, 2.5 })
            {
                var ziel = new Vector3((float)(st.X + (vx * d)), StadtSzene.Hoehe.Gehweg, (float)(st.Z + (vz * d)));
                if (Frei(ziel.X, ziel.Z) && k.Bildpunkt(ziel, (float)b.Breite, (float)b.Hoehe) is { } bp && Sticks.SeiteFuer(bp.X, bp.Y, b) is not null)
                {
                    tipp = Pixel(new Point(bp.X, bp.Y));
                    break;
                }
            }
            melden(string.Create(inv, $"LAGE flaeche {fl.Bounds.Width:F0}x{fl.Bounds.Height:F0} skala {skala:0.###} frei {b.FreiHoehe:F0} moeglich {fl.SticksMoeglich} fest {fl.FesteKamera}"));
            melden(string.Create(inv, $"BEREIT links {links.X} {links.Y} rechts {rechts.X} {rechts.Y} radius {radius} tipp {tipp.X} {tipp.Y}"));
            fl.SticksGeaendert += () =>
            {
                var z = fl.StickZustand;
                melden("STICK aktiv " + (z.Aktiv ? "ja" : "nein"));
            };
            // ruhige Staende melden (Stelle, waagrechter Blick der Kamera, geht sie)
            string Stand()
            {
                var kam = fl.AktuelleKamera!;
                var v = SpielKamera.ImIch(kam) ? kam.IchBlick : kam.Ziel - kam.Auge;
                var blick = Vector2.Normalize(new Vector2(v.X, v.Z));
                return string.Create(inv, $"{spiel.FigurX:0.000} {spiel.FigurZ:0.000} {blick.X:0.0000} {blick.Y:0.0000}");
            }
            var nummer = 0;
            var gemeldet = "";
            var zuletzt = Stand();
            var ruhig = Stopwatch.StartNew();
            var ging = false;
            var uhr = Stopwatch.StartNew();
            while (uhr.Elapsed.TotalSeconds < 300)
            {
                await Task.Delay(100);
                var geht = spiel.Geht || spiel.FreiUnterwegs;
                if (geht && !ging)
                {
                    melden("GEHT");
                }
                ging = geht;
                var jetzt = Stand();
                if (jetzt != zuletzt || geht || fl.StickZustand.Aktiv)
                {
                    zuletzt = jetzt;
                    ruhig.Restart();
                    continue;
                }
                if (ruhig.Elapsed.TotalSeconds >= 0.8 && jetzt != gemeldet)
                {
                    gemeldet = jetzt;
                    melden(string.Create(inv, $"STAND {++nummer} {jetzt}"));
                }
            }
            melden("ENDE");
        }
        catch (Exception e)
        {
            melden("FEHLER " + e.GetType().Name + ": " + e.Message);
        }
    }
}
