using Android.Graphics;
using Android.Views;
using Avalonia.Android;

namespace Lernplattform.Android;

/// <summary>
/// Welle A 1.3.2 (Weisung P12-350, Sammelliste 434 und 435): Dichte am Handy sofort uebernehmen. Seit 426 behandelt die
/// Activity eine Aenderung der Anzeigedichte selbst (<c>ConfigChanges.Density</c>), damit Android sie nicht neu baut. Avalonia
/// 12.1 liest die Skalierung aber nur, wenn sich die Bildflaeche in Bildpunkten aendert (<c>SurfaceChanged</c> der eigenen
/// SurfaceView); bei einer reinen Dichte-Aenderung bleibt sie gleich, Schrift und Knoepfe behielten bis zum naechsten
/// Groessenwechsel die alte Groesse. Darum meldet <see cref="Nachziehen"/> der Bildflaeche von Avalonia dieselbe Aenderung
/// noch einmal: sie liest dabei die neue Dichte und gibt die neue Groesse und Skalierung an die Oberflaeche weiter.
/// Eine Datei fuer beide Programme (das Werkzeug bindet sie ein wie <c>HandyInstaller.cs</c>).
/// </summary>
internal static class Dichte
{
#if UPDATEPROBE
    /// <summary>Nur Probe-APK: Gegenprobe der Konfig-Probe, verhaelt sich wie vor 434 (Dichte erst beim naechsten Groessenwechsel).</summary>
    public static bool AusFuerGegenprobe { get; set; }
#endif

    /// <summary>
    /// Aus <c>OnConfigurationChanged</c> der Activity: hat sich die Dichte gegenueber <paramref name="vorher"/> geaendert, wird die
    /// Bildflaeche von Avalonia nach dem laufenden Rueckruf neu vermessen. Gibt die neue Dichte zurueck (fuer den naechsten Vergleich).
    /// </summary>
    public static int Nachziehen(global::Android.App.Activity activity, global::Android.Content.Res.Configuration neu, int vorher)
    {
        var dichte = neu.DensityDpi;
        if (dichte == vorher)
        {
            return dichte;
        }
#if UPDATEPROBE
        if (AusFuerGegenprobe)
        {
            return dichte;
        }
#endif
        // nach dem laufenden Rueckruf: dann hat jede Ansicht ihre neuen Ressourcen (DisplayMetrics.Density)
        activity.Window?.DecorView?.Post(() => Flaechen(activity.Window?.DecorView));
        return dichte;
    }

    /// <summary>Jede Bildflaeche (SurfaceView) unter einer Ansicht von Avalonia meldet ihre Groesse neu.</summary>
    private static void Flaechen(View? ansicht)
    {
        if (ansicht is AvaloniaView avalonia)
        {
            Melden(avalonia);
            return;
        }
        if (ansicht is ViewGroup gruppe)
        {
            for (var i = 0; i < gruppe.ChildCount; i++)
            {
                Flaechen(gruppe.GetChildAt(i));
            }
        }
    }

    private static void Melden(View ansicht)
    {
        if (ansicht is SurfaceView flaeche && ansicht is ISurfaceHolderCallback rueckruf)
        {
            var halter = flaeche.Holder;
            // ohne gueltige Flaeche (App im Hintergrund) nichts tun: beim Zurueckkommen liest Avalonia die Dichte ohnehin neu
            if (halter?.Surface is { IsValid: true } && halter.SurfaceFrame is { } rahmen)
            {
                rueckruf.SurfaceChanged(halter, Format.Transparent, rahmen.Width(), rahmen.Height());
            }
        }
        if (ansicht is ViewGroup gruppe)
        {
            for (var i = 0; i < gruppe.ChildCount; i++)
            {
                if (gruppe.GetChildAt(i) is { } kind)
                {
                    Melden(kind);
                }
            }
        }
    }
}
