using Avalonia;
using Avalonia.Controls;
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
        // Phase 5 W2 (BE7): Leistungsmessung nach dem gespeicherten Schalter (aus = keine Datei)
        Programm.Diagnosedienst.Starten(Programm.Zustand.Werte);
        // Phase 8 W8: Tastenbelegung, Toene und Vollbild aus den Einstellungen
        Core.Spiel.Probe.Tastenbelegung.Aktuell = Core.Spiel.Probe.Tastenbelegung.Lesen(Programm.Zustand.Werte);
        Spiel.TonDienst.Regler(Core.Spiel.Ton.TonEinstellungen.Lesen(Programm.Zustand.Werte));
        Spiel.TonDienst.Lieder = new Core.Spiel.Ton.EigeneLieder(Core.Daten.Ablage.Datenordner());
        Spiel.Vollbild.DauerhaftSetzen(Core.Spiel.Ton.VollbildWahl.Lesen(Programm.Zustand.Werte));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var fenster = new MainWindow
            {
                DataContext = new MainViewModel()
            };
            // Phase 8 W8 (Sammelliste 131): Vollbild vor dem ersten Zeigen (kein Aufblitzen), Toene aus, solange minimiert
            if (Spiel.Vollbild.Dauerhaft)
            {
                fenster.WindowState = WindowState.FullScreen;
            }
            Spiel.Vollbild.Fenster = an => fenster.WindowState = an ? WindowState.FullScreen : WindowState.Normal;
            fenster.PropertyChanged += (_, e) =>
            {
                if (e.Property == Window.WindowStateProperty)
                {
                    Spiel.TonDienst.Hintergrund(fenster.WindowState == WindowState.Minimized);
                }
            };
            desktop.MainWindow = fenster;
            // Wie Python flush_inputs beim Schliessen: Aufgedecktes als angesehen speichern
            desktop.Exit += (_, _) =>
            {
                // Weisung P7-17: zuerst alle Spielschritte fertig schreiben
                Core.Spiel.Spielschlange.AlleLeeren();
                // Phase 5 W2 (BE8): ab jetzt "endet" fuer einen zweiten Start, Messdatei schreiben
                Programm.Diagnosedienst.Beenden();
                Programm.Zustand.Beenden();
                // L10: letzter Abgleich, wenn seither gelernt wurde (run_before_exit, hoechstens 12 s);
                // Phase 5 BE10: nicht, wenn das Programm fuer ein Update schliesst (on_close(final_sync=False))
                if (!Programm.UpdateDienst.FuerUpdateBeendet)
                {
                    Programm.AbgleichDienst.VorDemBeenden();
                }
                Programm.Diagnosedienst.NachDemAbgleich();
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
            // Phase 5 BE10: beim Zurueckholen nach Updates suchen (hoechstens alle 30 Minuten, _lifecycle main.py 5979)
            leben.Activated += (_, e) =>
            {
                if (e.Kind == ActivationKind.Background)
                {
                    Programm.UpdateDienst.Zurueck();
                }
            };
            leben.Deactivated += (_, e) =>
            {
                if (e.Kind == ActivationKind.Background)
                {
                    // Weisung P7-17: alle Spielschritte fertig schreiben
                    Core.Spiel.Spielschlange.AlleLeeren();
                    // W3: offene Eingaben im Abschlussprojekt zuerst speichern (_lifecycle main.py 5975)
                    Programm.Zustand.EingabenSpeichern();
                    Programm.AbgleichDienst.Verlassen();
                }
            };
        }
        // L10: Abgleich beim Start (PC nach 1,5 s, Handy sofort), wenn eingerichtet und an
        Programm.AbgleichDienst.Starten();

        base.OnFrameworkInitializationCompleted();
    }
}