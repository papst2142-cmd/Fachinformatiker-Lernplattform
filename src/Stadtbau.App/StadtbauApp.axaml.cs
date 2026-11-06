using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Lernplattform.Core.Design;
using Lernplattform.Design;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau;

/// <summary>
/// Phase 13 Welle 2: Anwendung des Stadtbau-Werkzeugs (PC-Fenster 1440 × 810 oder Tablet-Ansicht). Farben, Schrift und
/// Screenreader-Bausteine wie in der Lernplattform; Fehler gehen nach fehler.log im Ordner des Werkzeugs.
/// </summary>
public partial class StadtbauApp : Application
{
    /// <summary>Ordner des Werkzeugs (Projekte, Sicherungen, Exporte); Vorgabe aus <see cref="Werkzeugordner.Standard"/>.</summary>
    public static Werkzeugordner Ordner { get; set; } = Werkzeugordner.Standard();

    /// <summary>
    /// Patch S 0.5.1 (Weisung P12-35): Ende der App unter Android (setzt MainActivity: Aufgabe schliessen, dann Prozessende,
    /// wie "Programm beenden" der Lernplattform).
    /// </summary>
    public static Action? GeraetBeenden { get; set; }

    /// <summary>Wird nach dem Aufbau gerufen (Start-Test).</summary>
    public static Action<Hauptansicht, Window?>? NachStart { get; set; }

    /// <summary>Patch S 0.5.1: gemerktes Vollbild beim Start anwenden (der Start-Test ruft es fuer den Neustart).</summary>
    public static void VollbildBeimStart() => WerkzeugVollbild.Setzen(WerkzeugEinstellungen.Vollbild(Ordner));

    public override void Initialize()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Ordner.Fehler(e.Exception.ToString());
            e.Handled = true;
        };
        AvaloniaXamlLoader.Load(this);
        Lernplattform.Views.Vorlesen.Einrichten();
        Lernplattform.Views.Fokusrahmen.Einrichten(this);
        FarbenAnwenden.Anwenden(Farbwelt.Berechne(Farbwelt.Hell, Farbwelt.StandardGrundfarbe, Farbwelt.StandardHintergrund));
        Schrift.Anwenden(1.0);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var ansicht = new Hauptansicht(Ordner);
            var fenster = new Window
            {
                Title = Werkzeug.Kennzeile,
                Width = 1440,
                Height = 810,
                MinWidth = 900,
                MinHeight = 600,
                Content = ansicht,
            };
            desktop.MainWindow = fenster;
            // Patch S 0.5.1: Knopf "Werkzeug beenden" und Fensterkreuz laufen ueber dieselbe Pruefung (Rueckfrage nur bei
            // einer nicht gespeicherten Aenderung); beim Herunterfahren des Systems oder der App wird nicht gefragt
            var frei = false;
            fenster.Closing += async (_, e) =>
            {
                if (frei || e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown)
                {
                    return;
                }
                e.Cancel = true;
                if (await ansicht.BeendenPruefenAsync())
                {
                    frei = true;
                    fenster.Close();
                }
            };
            ansicht.Schliessen = fenster.Close;
            // Patch S 0.5.1 (Weisung P12-37 Punkt 2): Vollbild vor dem ersten Zeigen, wenn es beim letzten Mal an war
            WerkzeugVollbild.Fenster = an => fenster.WindowState = an ? WindowState.FullScreen : WindowState.Normal;
            VollbildBeimStart();
            NachStart?.Invoke(ansicht, fenster);
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime aktivitaet)
        {
            aktivitaet.MainViewFactory = () =>
            {
                var ansicht = new Hauptansicht(Ordner);
                ansicht.Schliessen = () => _ = ansicht.BeendenAsync(GeraetBeenden);
                VollbildBeimStart();
                NachStart?.Invoke(ansicht, null);
                return ansicht;
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime einzeln)
        {
            var ansicht = new Hauptansicht(Ordner);
            einzeln.MainView = ansicht;
            NachStart?.Invoke(ansicht, null);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
