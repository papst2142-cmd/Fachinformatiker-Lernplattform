using System;
using Avalonia;
using Lernplattform.Messung;
using Velopack;

namespace Lernplattform.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack zuerst: erledigt Installations- und Update-Schritte und beendet dann selbst.
        VelopackApp.Build().Run();
        if (Environment.GetEnvironmentVariable("LERNPLATTFORM_UPDATE_AUTO") is { Length: > 0 } modus)
        {
            return Updater.Auto(modus);
        }
        Updater.Verbinden();
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        // Phase 0, Spike A: Ausweichschalter, falls Tippen haengt oder das Zeichnen
        // Fehler macht. LERNPLATTFORM_IME=0 schaltet die Eingabemethode (IBus) unter
        // Linux ab, LERNPLATTFORM_SOFTWARE=1 zeichnet ohne Grafikkarte.
        var imeAus = Environment.GetEnvironmentVariable("LERNPLATTFORM_IME") == "0";
        var software = Environment.GetEnvironmentVariable("LERNPLATTFORM_SOFTWARE") == "1";
        var x11 = new X11PlatformOptions { EnableIme = !imeAus };
        var win32 = new Win32PlatformOptions();
        if (software)
        {
            x11.RenderingMode = [X11RenderingMode.Software];
            win32.RenderingMode = [Win32RenderingMode.Software];
        }
        Messwerte.Notiere("Schalter: Eingabemethode " + (imeAus ? "aus" : "an") + ", Zeichnen " + (software ? "Software" : "Standard"));
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(x11)
            .With(win32)
            .WithInterFont()
            .LogToTrace();
    }
}
