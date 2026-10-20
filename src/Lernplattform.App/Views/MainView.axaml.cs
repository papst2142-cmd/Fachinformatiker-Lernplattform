using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Lernplattform.Core.Daten;
using Lernplattform.Design;
using Update = Lernplattform.Aktualisierung.Aktualisierung;
using Lernplattform.Messung;
using Lernplattform.ViewModels;

namespace Lernplattform.Views;

/// <summary>
/// Phase 0: Startseite (mit "Alte Sicherung pruefen") und Testseite. Die Seiten werden
/// beim ersten Oeffnen gebaut und danach wiederverwendet.
/// </summary>
public partial class MainView : UserControl
{
    private readonly ContentControl _inhalt = new();
    private readonly Dictionary<string, Control> _seiten = new(StringComparer.Ordinal);
    private readonly TextBox _ergebnis = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 160 };
    private readonly TextBlock _zustand = new() { TextWrapping = TextWrapping.Wrap };

    public static bool AutoModus => Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSUNG_AUTO") == "1";

    public MainView()
    {
        InitializeComponent();
        var navigation = new StackPanel { Spacing = 6, Margin = new Thickness(10) };
        foreach (var name in (string[])["Start", "Testseite"])
        {
            var knopf = new Button { Content = name, HorizontalAlignment = HorizontalAlignment.Stretch };
            knopf.Farbe(Button.ForegroundProperty, "Pinsel.text");
            knopf.Farbe(Button.BackgroundProperty, "Pinsel.card_alt");
            knopf.Click += async (_, _) => await ZeigeAsync(name);
            navigation.Children.Add(knopf);
        }
        var leiste = new Border { Child = navigation, Width = 150 }.Farbe(Border.BackgroundProperty, "Pinsel.sidebar");
        var raster = new DockPanel();
        DockPanel.SetDock(leiste, Dock.Left);
        raster.Children.Add(leiste);
        raster.Children.Add(_inhalt);
        Content = new Border { Child = raster }.Farbe(Border.BackgroundProperty, "Pinsel.bg");
        _inhalt.Farbe(ContentControl.ForegroundProperty, "Pinsel.text");
        _inhalt.Content = Seite("Start");

        AttachedToVisualTree += (_, _) => TopLevel.GetTopLevel(this)?.RequestAnimationFrame(_ =>
        {
            Messwerte.ErstesBild ??= Messwerte.SeitStart.Elapsed.TotalMilliseconds;
            if (AutoModus)
            {
                Dispatcher.UIThread.Post(async () => await AutoLaufAsync(), DispatcherPriority.Background);
            }
        });
    }

    /// <summary>Wartet, bis das naechste Bild fertig gezeichnet ist (zwei Bildaufrufe).</summary>
    private Task BildFertigAsync()
    {
        var fertig = new TaskCompletionSource();
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            fertig.SetResult();
            return fertig.Task;
        }
        top.RequestAnimationFrame(_ => top.RequestAnimationFrame(_ => fertig.TrySetResult()));
        return fertig.Task;
    }

    private Control Seite(string name)
    {
        if (!_seiten.TryGetValue(name, out var seite))
        {
            seite = name switch
            {
                "Testseite" => BaueTestseite(),
                _ => BaueStart(),
            };
            _seiten[name] = seite;
        }
        return seite;
    }

    private async Task ZeigeAsync(string name)
    {
        var erstes = !_seiten.ContainsKey(name);
        var uhr = Stopwatch.StartNew();
        _inhalt.Content = Seite(name);
        await BildFertigAsync();
        var ms = uhr.Elapsed.TotalMilliseconds;
        if (erstes)
        {
            Messwerte.ErstesOeffnen[name] = ms;
        }
        else
        {
            Messwerte.Reihe("Seitenwechsel (gebaute Seite)", ms);
        }
    }

    private static TextBlock Text(string text, double groesse = 14, FontWeight gewicht = FontWeight.Normal) =>
        new() { Text = text, FontSize = groesse, FontWeight = gewicht, TextWrapping = TextWrapping.Wrap };

    private Control BaueStart()
    {
        var vm = new MainViewModel();
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(24), VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(Text(vm.Titel, 24, FontWeight.SemiBold));
        panel.Children.Add(Text(vm.Version));
        panel.Children.Add(Text(vm.Hinweis));

        var kopieren = new Button { Content = "Ergebnis kopieren" };
        kopieren.Farbe(Button.BackgroundProperty, "Pinsel.card_alt").Farbe(Button.ForegroundProperty, "Pinsel.text");
        kopieren.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } ablage)
            {
                await ablage.SetTextAsync(_ergebnis.Text ?? "");
                _zustand.Text = "Ergebnis kopiert.";
            }
        };
        var sicherung = new Button { Content = "Alte Sicherung pruefen" };
        sicherung.Farbe(Button.BackgroundProperty, "Pinsel.card_alt").Farbe(Button.ForegroundProperty, "Pinsel.text");
        sicherung.Click += async (_, _) => await SicherungPruefenAsync();
        panel.Children.Add(new WrapPanel { Children = { sicherung, kopieren } });
        panel.Children.Add(Text("\"Alte Sicherung pruefen\" liest eine Kopie der Sicherung der alten App (lernstand.json.gz) nur lesend. Die Datei bleibt auf diesem Geraet; im Ergebnis stehen nur Zaehlwerte und Pruefsummen, keine Inhalte, kein Dateiname."));
        panel.Children.Add(Text("Linux: Zeigt das Fenster Bildfehler (z. B. ueber eine VNC-Konsole), das Programm mit --software starten (oder LERNPLATTFORM_SOFTWARE=1). Haengt das Tippen, mit --ohne-eingabemethode starten (oder LERNPLATTFORM_IME=0)."));
        panel.Children.Add(_zustand);
        _ergebnis.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card");
        panel.Children.Add(_ergebnis);
        _ergebnis.Text = BerichtText();
        return new ScrollViewer { Content = panel };
    }


    private static Control BaueTestseite()
    {
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        var banner = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Child = Text("Testseite: 50 Karten, 30 Knoepfe, Eingabefeld, Liste", 18, FontWeight.SemiBold) };
        banner.Farbe(Border.BackgroundProperty, "Verlauf.hero");
        ((TextBlock)banner.Child!).Farbe(TextBlock.ForegroundProperty, "Pinsel.on_accent");
        panel.Children.Add(banner);

        var eingabe = new TextBox { PlaceholderText = "Umlaut-Test: hier ä ö ü ß Ä Ö Ü tippen", MinHeight = 36 };
        eingabe.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card")
            .Farbe(TextBox.BorderBrushProperty, "Pinsel.field_border");
        panel.Children.Add(eingabe);

        var karten = new WrapPanel();
        for (var i = 1; i <= 50; i++)
        {
            var titel = Text($"Karte {i}", 15, FontWeight.SemiBold).Farbe(TextBlock.ForegroundProperty, "Pinsel.text");
            var unter = Text("Lernfeld " + ((i % 12) + 1), 12).Farbe(TextBlock.ForegroundProperty, "Pinsel.text_dim");
            var punkt = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5) }
                .Farbe(Border.BackgroundProperty, (i % 3) switch { 0 => "Pinsel.accent", 1 => "Pinsel.accent2", _ => "Pinsel.green" });
            var karte = new Border
            {
                Width = 160, Margin = new Thickness(4), Padding = new Thickness(10), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1),
                Child = new StackPanel { Spacing = 4, Children = { punkt, titel, unter } },
            };
            karte.Farbe(Border.BackgroundProperty, i % 2 == 0 ? "Pinsel.card" : "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border");
            karten.Children.Add(karte);
        }
        panel.Children.Add(karten);

        var knoepfe = new WrapPanel();
        for (var i = 1; i <= 30; i++)
        {
            var knopf = new Button { Content = $"Knopf {i}", Margin = new Thickness(4), MinWidth = 100 };
            knopf.Farbe(Button.BackgroundProperty, i % 2 == 0 ? "Verlauf.primary" : "Verlauf.accent").Farbe(Button.ForegroundProperty, "Pinsel.on_accent");
            knoepfe.Children.Add(knopf);
        }
        panel.Children.Add(knoepfe);

        var liste = new ListBox { Height = 240, ItemsSource = Enumerable.Range(1, 100).Select(i => $"Frage {i}: Beispielzeile in der Liste").ToList() };
        liste.Farbe(ListBox.BackgroundProperty, "Pinsel.card").Farbe(ListBox.ForegroundProperty, "Pinsel.text");
        panel.Children.Add(liste);
        return new ScrollViewer { Content = panel };
    }


    private string Geraet()
    {
        var top = TopLevel.GetTopLevel(this);
        return top is null ? "" : string.Create(CultureInfo.InvariantCulture,
            $"{top.Bounds.Width:F0}x{top.Bounds.Height:F0} Punkte, Skalierung {top.RenderScaling:F2}");
    }


    private string BerichtText()
    {
        string probe;
        try
        {
            probe = Ablage.UpdateProbe(Update.PaketVersion);
        }
        catch (Exception fehler)
        {
            probe = "nicht lesbar (" + fehler.GetType().Name + ")";
        }
        return Messwerte.Bericht(Geraet())
            + "Version: " + Update.Anzeige + " (Paketversion " + Update.PaketVersion + ")" + Environment.NewLine
            + "Updater: " + (Update.Zustand?.Invoke() ?? "keiner (Android: Update ueber neue APK)") + Environment.NewLine
            + "Datenordner-Probe: " + probe + Environment.NewLine;
    }


    /// <summary>
    /// Spike C mit echtem Lernstand (Auflage A3, Weisung CC P0-3): die Datei wird nur
    /// in den Speicher gelesen; ausgegeben wird nur, was SicherungsPruefung liefert.
    /// Kein Dateiname, kein Pfad, kein Fehlertext.
    /// </summary>
    private async Task SicherungPruefenAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }
        var auswahl = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Kopie der alten Sicherung (lernstand.json.gz) waehlen",
            AllowMultiple = false,
        });
        if (auswahl.Count == 0)
        {
            return;
        }
        IReadOnlyList<string> zeilen;
        try
        {
            using var puffer = new MemoryStream();
            await using (var lesen = await auswahl[0].OpenReadAsync())
            {
                await lesen.CopyToAsync(puffer);
            }
            zeilen = SicherungsPruefung.Pruefe(puffer.ToArray());
        }
        catch (Exception)
        {
            zeilen = ["FEHLER C0 (Datei nicht lesbar)"];
        }
        Messwerte.Notiere("ALTE SICHERUNG (nur Zaehlwerte):");
        foreach (var z in zeilen)
        {
            Messwerte.Notiere("  " + z);
        }
        _zustand.Text = "Alte Sicherung geprueft. Jetzt \"Ergebnis kopieren\".";
        _ergebnis.Text = BerichtText();
    }

    /// <summary>Start ohne Bedienung (Umgebungsvariable LERNPLATTFORM_MESSUNG_AUTO=1), fuer Xvfb und CI.</summary>
    private async Task AutoLaufAsync()
    {
        await ZeigeAsync("Testseite");
        await ZeigeAsync("Start");
        _ergebnis.Text = BerichtText();
        var datei = Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSUNG_DATEI") is { Length: > 0 } d ? d : "messung_auto.txt";
        await File.WriteAllTextAsync(datei, _ergebnis.Text);
        Console.WriteLine(_ergebnis.Text);
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
