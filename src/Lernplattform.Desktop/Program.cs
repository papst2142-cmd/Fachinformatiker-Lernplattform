using System;
using System.Linq;
using Avalonia;
using Lernplattform.Core.Diagnose;
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
        // Phase 5 W3: ein geladenes Paket nie von allein beim Start einspielen; ein Update laeuft nur
        // ueber UpdateAblauf.Installieren (vorher Kopie des Lernstands und Merker, Gegenpruefung W3 Fund 1).
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        if (Environment.GetEnvironmentVariable("LERNPLATTFORM_UPDATE_AUTO") is { Length: > 0 } modus)
        {
            return Updater.Auto(modus);
        }
        Updater.Verbinden();
        // Phase 5 W2 (BE8): Sperre gegen Mehrfachstart (Linux, Windows). Laeuft das Programm schon,
        // kommt dessen Fenster nach vorn und dieser Start endet still. Der Start ohne Bedienung ist ausgenommen.
        var version = Lernplattform.Aktualisierung.Aktualisierung.Anzeige;
        Fehlerprotokoll.Einrichten(version);
        var auto = Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSUNG_AUTO") == "1";
        if (!auto && Einzelstart.Unterstuetzt() && !Einzelstart.Beanspruchen(version: version))
        {
            return 0;
        }
        // Haenger-Diagnose (haenger.log, Waechter, kill -USR1); still aus, wenn der Datenordner nicht beschreibbar ist
        var (imeAus, software) = Schalter();
        Haenger.Einrichten(version, zusatz: "Eingabemethode " + (imeAus ? "aus" : "an") + ", Zeichnen " + (software ? "Software" : "Standard"));
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Ausweichschalter (Umgebung oder Startschalter): Eingabemethode aus, Zeichnen ohne Grafikkarte.</summary>
    private static (bool ImeAus, bool Software) Schalter()
    {
        var args = Environment.GetCommandLineArgs();
        return (Environment.GetEnvironmentVariable("LERNPLATTFORM_IME") == "0" || args.Contains("--ohne-eingabemethode"),
            Environment.GetEnvironmentVariable("LERNPLATTFORM_SOFTWARE") == "1" || args.Contains("--software"));
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        // Phase 0, Spike A: Ausweichschalter, falls Tippen haengt oder das Zeichnen
        // Fehler macht. LERNPLATTFORM_IME=0 schaltet die Eingabemethode (IBus) unter
        // Linux ab, LERNPLATTFORM_SOFTWARE=1 zeichnet ohne Grafikkarte.
        // Gleiches geht per Startschalter --ohne-eingabemethode und --software (fuer Verknuepfungen).
        var (imeAus, software) = Schalter();
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
