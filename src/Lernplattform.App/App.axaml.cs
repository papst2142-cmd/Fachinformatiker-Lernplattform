using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Lernplattform.Core.Diagnose;
using Lernplattform.Design;
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
        FarbenAnwenden.Anwenden(FarbenAnwenden.Aktuell);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
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

        base.OnFrameworkInitializationCompleted();
    }
}