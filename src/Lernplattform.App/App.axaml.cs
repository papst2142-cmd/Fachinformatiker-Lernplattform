using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Lernplattform.Core.Diagnose;
using Lernplattform.Messung;
using Lernplattform.ViewModels;
using Lernplattform.Views;

namespace Lernplattform;

public partial class App : Application
{
    public override void Initialize()
    {
        _ = Messwerte.SeitStart;
        // Phase 1, K4: unbehandelte Fehler nach fehler.log im Datenordner (PC und Handy)
        Fehlerprotokoll.Einrichten(Aktualisierung.Aktualisierung.Anzeige);
        // Wie Python (Tk report_callback_exception): Fehler einer Bedienung protokollieren, Programm laeuft weiter;
        // als behandelt markiert, damit derselbe Fehler nicht ein zweites Mal ueber AppDomain im Protokoll landet.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Fehlerprotokoll.Schreiben(e.Exception);
            e.Handled = true;
        };
        AvaloniaXamlLoader.Load(this);
        // Phase 1: Einstellungen lesen, Farbwelt und Schriftstufe anwenden, Inhalte im Hintergrund laden
        Programm.Zustand.Starten();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
            // Wie Python flush_inputs beim Schliessen: Aufgedecktes als angesehen speichern
            desktop.Exit += (_, _) =>
            {
                Programm.Zustand.Beenden();
                // L10: letzter Abgleich, wenn seither gelernt wurde (run_before_exit, hoechstens 12 s)
                Programm.AbgleichDienst.VorDemBeenden();
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = new MainViewModel() };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        // L10: Handy gleicht beim Verlassen der App ab (on_leave), wenn seither gelernt wurde
        if (OperatingSystem.IsAndroid() && this.TryGetFeature<IActivatableLifetime>() is { } leben)
        {
            leben.Deactivated += (_, e) =>
            {
                if (e.Kind == ActivationKind.Background)
                {
                    Programm.AbgleichDienst.Verlassen();
                }
            };
        }
        // L10: Abgleich beim Start (PC nach 1,5 s, Handy sofort), wenn eingerichtet und an
        Programm.AbgleichDienst.Starten();

        base.OnFrameworkInitializationCompleted();
    }
}