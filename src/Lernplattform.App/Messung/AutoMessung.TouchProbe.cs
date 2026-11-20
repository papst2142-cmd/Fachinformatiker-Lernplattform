using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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
            var tippPunkt = new Point(b.Breite / 4, yUnten);
            foreach (var d in new[] { 1.5, 2.0, 2.5 })
            {
                var ziel = new Vector3((float)(st.X + (vx * d)), StadtSzene.Hoehe.Gehweg, (float)(st.Z + (vz * d)));
                if (Frei(ziel.X, ziel.Z) && k.Bildpunkt(ziel, (float)b.Breite, (float)b.Hoehe) is { } bp && Sticks.SeiteFuer(bp.X, bp.Y, b) is not null)
                {
                    tipp = Pixel(new Point(bp.X, bp.Y));
                    tippPunkt = new Point(bp.X, bp.Y);
                    break;
                }
            }
            melden(string.Create(inv, $"LAGE flaeche {fl.Bounds.Width:F0}x{fl.Bounds.Height:F0} skala {skala:0.###} frei {b.FreiHoehe:F0} moeglich {fl.SticksMoeglich} fest {fl.FesteKamera}"));
            // Welle A 0.99.0 (Weisung P12-317, Sammelliste 398): in Lauf 1116 trafen die Finger „LernText“ und einen Bildlauf ueber
            // der Flaeche, also ein Fenster der Ebene ueber allen Seiten (Frage nach der Ausbildung, Rundgang oder Hinweis beim
            // ersten Start nach der Update-Probe). Solche Fenster schliessen wie ein Nutzer (letzter Knopf bzw. Ueberspringen),
            // bis 3 s lang keins mehr kommt; danach muss die Trefferpruefung an allen drei Fingerstellen die Flaeche treffen
            var punkte = new[] { ("links", new Point(b.Breite / 4, yUnten)), ("rechts", new Point(b.Breite * 3 / 4, yUnten)), ("tipp", tippPunkt) };
            if (!await FreiraeumenAsync(fl, oben!, punkte, melden))
            {
                return;
            }
            // Welle A 0.99.0 (Weisung P12-319/320, Sammelliste 400): der runde Haltung-Knopf am rechten Daumen. Seine Mitte in
            // Bildpunkten (Zeile „HALTUNG“) muss den Knopf selbst treffen; die Probe tippt und drueckt ihn dort lang
            await fl.NaechstesBildAsync();
            var hk = seite.DaumenKnoepfe.Haltung;
            if (seite.DaumenRechteck(hk) is not { } hr)
            {
                melden("FEHLER runder Haltung-Knopf nicht sichtbar");
                return;
            }
            var hMitte = fl.TranslatePoint(hr.Center, oben!) ?? hr.Center;
            var hTreffer = oben!.InputHitTest(hMitte) as Visual;
            if (hTreffer is null || !(ReferenceEquals(hTreffer, hk) || hk.IsVisualAncestorOf(hTreffer)))
            {
                melden("FEHLER Mitte des Haltung-Knopfs trifft " + (hTreffer?.GetType().Name ?? "nichts"));
                return;
            }
            var haltungPx = Pixel(hr.Center);
            melden(string.Create(inv, $"HALTUNG {haltungPx.X} {haltungPx.Y} durchmesser {hr.Width:F0} haltung {spiel.Koerper}"));
            melden(string.Create(inv, $"BEREIT links {links.X} {links.Y} rechts {rechts.X} {rechts.Y} radius {radius} tipp {tipp.X} {tipp.Y}"));
            // Befund Lauf 1110: kein Stick und kein Gehen. Jeder echte Finger, der beim Fenster ankommt, steht darum im Protokoll
            // (ZEIGER ab/auf mit Bildpunkt), auch wenn ihn ein anderes Element schon behandelt hat
            // Lauf 1113: die Finger kommen an (ZEIGER), aber kein Stick. Darum je Finger auch, welches Element ihn trifft, ob es in
            // der Flaeche liegt, wie viele Bewegungen ankamen und den Zustand der Sticks gleich danach
            var bewegt = 0;
            void Zeiger(string art, PointerEventArgs e)
            {
                var p = e.GetPosition(oben);
                var ziel = oben?.InputHitTest(p) as Visual;
                var inFlaeche = ziel is not null && (ReferenceEquals(ziel, fl) || fl.IsVisualAncestorOf(ziel));
                var zielName = ziel is null ? "-" : ziel.GetType().Name + (ziel is Control c && !string.IsNullOrEmpty(c.Name) ? "#" + c.Name : "");
                var q = e.GetPosition(fl);
                melden(string.Create(inv, $"ZEIGER {art} {p.X * skala:F0} {p.Y * skala:F0} {e.Pointer.Type} id {e.Pointer.Id} ziel {zielName} flaeche {(inFlaeche ? "ja" : "nein")} inFlaeche {q.X:F0} {q.Y:F0} bewegt {bewegt} moeglich {fl.SticksMoeglich} kandidaten {fl.StickZustand.Kandidaten} aktiv {fl.StickZustand.Aktiv} seite {Sticks.SeiteFuer(q.X, q.Y, fl.StickBereich)?.ToString() ?? "-"}"));
                bewegt = 0;
                Avalonia.Threading.Dispatcher.UIThread.Post(() => melden(string.Create(inv, $"ZEIGER danach {art} kandidaten {fl.StickZustand.Kandidaten} aktiv {fl.StickZustand.Aktiv}")));
            }
            oben?.AddHandler(InputElement.PointerPressedEvent, (_, e) => Zeiger("ab", e), RoutingStrategies.Tunnel, handledEventsToo: true);
            oben?.AddHandler(InputElement.PointerMovedEvent, (_, _) => bewegt++, RoutingStrategies.Tunnel, handledEventsToo: true);
            oben?.AddHandler(InputElement.PointerReleasedEvent, (_, e) => Zeiger("auf", e), RoutingStrategies.Tunnel, handledEventsToo: true);
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
            var koerper = spiel.Koerper;
            while (uhr.Elapsed.TotalSeconds < 300)
            {
                await Task.Delay(100);
                // Sammelliste 400: jede neue Haltung (Ducken, Kriechen, Stehen) steht im Protokoll
                if (spiel.Koerper != koerper)
                {
                    koerper = spiel.Koerper;
                    melden("KOERPER " + koerper);
                }
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

    /// <summary>
    /// Welle A 0.99.0 (Sammelliste 398): schliesst offene Fenster und den Rundgang ueber der Spielseite (Zeile „UEBERLAGERUNG“ je
    /// Fund), bis 3 s lang keins mehr offen ist (hoechstens 60 s), und prueft dann mit der Trefferpruefung von Avalonia, dass
    /// jede Fingerstelle die 3D-Flaeche trifft (Zeile „TREFFER“). Trifft eine Stelle etwas anderes, steht es mit seinen
    /// Vorfahren in einer Zeile „FEHLER“, und die Probe endet rot (kein Finger wird gesetzt).
    /// </summary>
    private static async Task<bool> FreiraeumenAsync(StadtFlaeche fl, TopLevel oben, (string Name, Point Stelle)[] punkte, Action<string> melden)
    {
        var inv = CultureInfo.InvariantCulture;
        var ruhig = Stopwatch.StartNew();
        var uhr = Stopwatch.StartNew();
        var gefunden = 0;
        while (uhr.Elapsed.TotalSeconds < 60 && ruhig.Elapsed.TotalSeconds < 3)
        {
            var fenster = Dialog.Offen;
            var rundgang = RundgangEbene.Offen;
            if (fenster is not null || rundgang is not null)
            {
                gefunden++;
                melden(string.Create(inv, $"UEBERLAGERUNG fenster {(fenster is null ? "-" : fenster.GetType().Name + " " + ProbeKurz(Vorlesen.Text(fenster)))} rundgang {(rundgang is null ? "nein" : "ja")}"));
                Dialog.AlleSchliessen();
                rundgang?.Ueberspringen();
                ruhig.Restart();
            }
            await fl.NaechstesBildAsync();
            await Task.Delay(200);
        }
        var alle = true;
        var zeile = "TREFFER";
        foreach (var (name, stelle) in punkte)
        {
            var p = fl.TranslatePoint(stelle, oben) ?? stelle;
            var ziel = oben.InputHitTest(p) as Visual;
            var trifft = ziel is not null && (ReferenceEquals(ziel, fl) || fl.IsVisualAncestorOf(ziel));
            zeile += " " + name + " " + (trifft ? "Flaeche" : "nein");
            if (!trifft)
            {
                alle = false;
                var kette = new List<string>();
                for (var v = ziel; v is not null && kette.Count < 12; v = v.GetVisualParent())
                {
                    kette.Add(v.GetType().Name + (v is Control c && !string.IsNullOrEmpty(c.Name) ? "#" + c.Name : ""));
                }
                melden(string.Create(inv, $"FEHLER Treffer {name} bei {p.X:F0} {p.Y:F0}: {string.Join(" < ", kette)}"));
            }
        }
        melden(string.Create(inv, $"{zeile} (Fenster geschlossen: {gefunden})"));
        return alle;
    }

    private static string ProbeKurz(string text)
    {
        var einzeilig = text.Replace('\n', ' ').Replace('\r', ' ');
        return einzeilig.Length > 60 ? einzeilig[..60] : einzeilig;
    }
}
