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
        // Phase 11 (Weisung P11-29): Probe der CI fuer "Bericht senden" mit dem eingebauten Schluessel, ohne Fenster
        if (Environment.GetEnvironmentVariable("LERNPLATTFORM_BERICHT_PROBE") is { Length: > 0 } lauf)
        {
            return BerichtProbe(lauf);
        }
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

    /// <summary>
    /// Sendet berichte/_probe_&lt;Lauf&gt;.md ueber denselben Weg wie der Knopf. Rueckgabe 0 gesendet, 2 ohne Schluessel
    /// gebaut (nichts gesendet), 1 Fehler. Die Ausgabe nennt nie den Schluessel.
    /// </summary>
    private static int BerichtProbe(string lauf)
    {
        Fehlerprotokoll.Einrichten(Lernplattform.Aktualisierung.Aktualisierung.Anzeige);
        var ergebnis = Lernplattform.Core.Diagnose.BerichtVersand.Probe(lauf, Lernplattform.Aktualisierung.Aktualisierung.Anzeige, DateTime.Now);
        switch (ergebnis.Art)
        {
            case Lernplattform.Core.Diagnose.VersandArt.Gesendet:
                Console.WriteLine("Bericht-Probe: gesendet " + ergebnis.Datei);
                return 0;
            case Lernplattform.Core.Diagnose.VersandArt.NichtVerfuegbar:
                Console.WriteLine("Bericht-Probe: nicht verfuegbar (ohne Schluessel gebaut), nichts gesendet");
                return 2;
            default:
                Console.WriteLine("Bericht-Probe: FEHLER " + ergebnis.Art + (ergebnis.Code > 0 ? " HTTP " + ergebnis.Code : ""));
                return 1;
        }
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
        // Phase 7 W1: Avalonia sperrt unter Linux den Mesa-Softwaretreiber llvmpipe fuer OpenGL (Liste
        // GlxRendererBlacklist). In Xvfb der CI gibt es nur ihn; nur fuer den Start-Test erlaubt
        // LERNPLATTFORM_GL_LLVMPIPE=1 ihn, damit die 3D-Probe dort ueberhaupt OpenGL bekommt.
        var llvmpipe = Environment.GetEnvironmentVariable("LERNPLATTFORM_GL_LLVMPIPE") == "1" && !software;
        if (llvmpipe)
        {
            x11.GlxRendererBlacklist = [];
        }
        // Phase 7 W1 (Plan Abschnitt 3, Weg 1): macOS zeichnet in Avalonia 12.1.4 standardmaessig mit Metal,
        // dort gibt es kein OpenGL fuer die 3D-Flaeche. Mac-Welle (Weisung P11-28, Nico 03.11.2026 16:57 MEZ, Test
        // auf dem Intel-MacBook 30 bis 61 Bilder je Sekunde): am Mac zeichnet die App jetzt von allein mit OpenGL,
        // damit die Stadt in 3D erscheint. Kommt OpenGL nicht zustande, zeichnet Avalonia mit Metal (dann Software),
        // und das Spiel zeigt wie bisher Hinweis und Ortsliste. Notschalter LERNPLATTFORM_MAC_METAL=1: der alte Weg
        // (nur Metal, Ortsliste). LERNPLATTFORM_MAC_OPENGL=1 aus alten Anleitungen bleibt erlaubt (gleich der Vorgabe).
        var macMetal = OperatingSystem.IsMacOS() && Environment.GetEnvironmentVariable("LERNPLATTFORM_MAC_METAL") == "1";
        var macOpenGl = OperatingSystem.IsMacOS() && !macMetal;
        var native = new AvaloniaNativePlatformOptions();
        if (macOpenGl)
        {
            native.RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Metal, AvaloniaNativeRenderingMode.Software];
        }
        else if (macMetal)
        {
            native.RenderingMode = [AvaloniaNativeRenderingMode.Metal, AvaloniaNativeRenderingMode.Software];
        }
        Messwerte.Notiere("Schalter: Eingabemethode " + (imeAus ? "aus" : "an") + ", Zeichnen " + (software ? "Software" : "Standard")
            + (llvmpipe ? ", llvmpipe erlaubt" : "") + (macOpenGl ? ", macOS mit OpenGL (Vorgabe, Rueckfall Metal)" : "")
            + (macMetal ? ", macOS nur Metal (Notschalter LERNPLATTFORM_MAC_METAL)" : ""));
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(x11)
            .With(win32);
        if (macOpenGl || macMetal)
        {
            builder = builder.With(native);
        }
        return builder
            .WithInterFont()
            .LogToTrace();
    }
}
