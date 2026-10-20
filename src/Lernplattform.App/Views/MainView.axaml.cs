using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Security.Cryptography;
using Avalonia.Platform.Storage;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;
using Lernplattform.Design;
using Lernplattform.Messung;
using Lernplattform.ViewModels;

namespace Lernplattform.Views;

/// <summary>
/// Phase 0: Startseite, Testseite und Messseite (Spike A). Die Seiten werden
/// beim ersten Oeffnen gebaut und danach wiederverwendet; das erste Oeffnen
/// und jeder Wechsel werden bis zum fertig gezeichneten Bild gemessen.
/// </summary>
public partial class MainView : UserControl
{
    private readonly ContentControl _inhalt = new();
    private readonly Dictionary<string, Control> _seiten = new(StringComparer.Ordinal);
    private readonly TextBox _ergebnis = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 160 };
    private readonly TextBlock _zustand = new() { TextWrapping = TextWrapping.Wrap };
    private bool _laeuft;

    public static bool AutoModus => Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSUNG_AUTO") == "1";

    public MainView()
    {
        InitializeComponent();
        var navigation = new StackPanel { Spacing = 6, Margin = new Thickness(10) };
        foreach (var name in (string[])["Start", "Testseite", "Messung"])
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
                "Messung" => BaueMessseite(),
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
        panel.Children.Add(Text("Spike A: links \"Messung\" waehlen und \"Messung starten\" tippen."));
        return panel;
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

    private Control BaueMessseite()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(16) };
        panel.Children.Add(Text("Messung Spike A", 20, FontWeight.SemiBold));
        panel.Children.Add(Text("\"Messung starten\" wechselt auf der Testseite 10x zwischen Hell und Dunkel, dann durch alle 72 Farbwelten und 10x zwischen den Seiten. Danach \"Ergebnis kopieren\" und den Text an Claude schicken."));

        var darstellung = new ComboBox { ItemsSource = new[] { "Dunkel", "Hell" }, SelectedIndex = FarbenAnwenden.Aktuell.Hell ? 1 : 0, MinWidth = 140 };
        var grundfarbe = new ComboBox { ItemsSource = Farbwelt.Grundfarben.Select(g => g.Name).ToList(), SelectedIndex = 0, MinWidth = 160 };
        var hintergrund = new ComboBox { ItemsSource = Farbwelt.Hintergruende.Select(h => h.NameDunkel + " / " + h.NameHell).ToList(), SelectedIndex = 0, MinWidth = 200 };
        void Waehle(object? s, SelectionChangedEventArgs e)
        {
            if (darstellung.SelectedIndex < 0 || grundfarbe.SelectedIndex < 0 || hintergrund.SelectedIndex < 0)
            {
                return;
            }
            FarbenAnwenden.Anwenden(Farbwelt.Berechne(darstellung.SelectedIndex == 1 ? Farbwelt.Hell : Farbwelt.Dunkel,
                Farbwelt.Grundfarben[grundfarbe.SelectedIndex].Id, Farbwelt.Hintergruende[hintergrund.SelectedIndex].Id));
        }
        darstellung.SelectionChanged += Waehle;
        grundfarbe.SelectionChanged += Waehle;
        hintergrund.SelectionChanged += Waehle;
        panel.Children.Add(new WrapPanel { Children = { darstellung, grundfarbe, hintergrund } });

        var starten = new Button { Content = "Messung starten" };
        starten.Farbe(Button.BackgroundProperty, "Verlauf.primary").Farbe(Button.ForegroundProperty, "Pinsel.on_accent");
        starten.Click += async (_, _) => await MessenAsync();
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
        panel.Children.Add(new WrapPanel { Children = { starten, kopieren, sicherung } });
        panel.Children.Add(Text("\"Alte Sicherung pruefen\" liest eine Sicherung der alten App (lernstand.json.gz) oder die alte Datenbank (.db) nur lesend. Die Datei bleibt auf diesem Geraet; im Ergebnis stehen nur Zaehlwerte und Pruefsummen, keine Inhalte."));
        panel.Children.Add(_zustand);
        _ergebnis.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card");
        panel.Children.Add(_ergebnis);
        _ergebnis.Text = Messwerte.Bericht(Geraet());
        return new ScrollViewer { Content = panel };
    }

    private string Geraet()
    {
        var top = TopLevel.GetTopLevel(this);
        return top is null ? "" : string.Create(CultureInfo.InvariantCulture,
            $"{top.Bounds.Width:F0}x{top.Bounds.Height:F0} Punkte, Skalierung {top.RenderScaling:F2}");
    }

    private async Task WechselAsync(string reihe, Palette palette)
    {
        var uhr = Stopwatch.StartNew();
        FarbenAnwenden.Anwenden(palette);
        await BildFertigAsync();
        Messwerte.Reihe(reihe, uhr.Elapsed.TotalMilliseconds);
    }

    private async Task MessenAsync()
    {
        if (_laeuft)
        {
            return;
        }
        _laeuft = true;
        try
        {
            var ausgang = FarbenAnwenden.Aktuell;
            await ZeigeAsync("Testseite");
            var dunkel = Farbwelt.Berechne(Farbwelt.Dunkel, Farbwelt.StandardGrundfarbe, Farbwelt.StandardHintergrund);
            var hell = Farbwelt.Berechne(Farbwelt.Hell, Farbwelt.StandardGrundfarbe, Farbwelt.StandardHintergrund);
            for (var i = 0; i < 10; i++)
            {
                await WechselAsync("Wechsel Hell/Dunkel", i % 2 == 0 ? hell : dunkel);
            }
            foreach (var d in (string[])[Farbwelt.Dunkel, Farbwelt.Hell])
            {
                foreach (var g in Farbwelt.Grundfarben)
                {
                    foreach (var h in Farbwelt.Hintergruende)
                    {
                        var uhr = Stopwatch.StartNew();
                        var palette = Farbwelt.Berechne(d, g.Id, h.Id);
                        Messwerte.Reihe("Farbwelt berechnen (ohne Zeichnen)", uhr.Elapsed.TotalMilliseconds);
                        await WechselAsync("Wechsel durch alle 72 Farbwelten", palette);
                    }
                }
            }
            await WechselAsync("Wechsel Hell/Dunkel", ausgang);
            for (var i = 0; i < 10; i++)
            {
                await ZeigeAsync(i % 2 == 0 ? "Start" : "Testseite");
            }
            await ZeigeAsync("Messung");
            _ergebnis.Text = Messwerte.Bericht(Geraet());
            _zustand.Text = "Messung fertig. Jetzt \"Ergebnis kopieren\".";
        }
        finally
        {
            _laeuft = false;
        }
    }

    /// <summary>Spike C mit echtem Lernstand (Auflage A3): nur lesen, nur Zaehlwerte und Pruefsummen.</summary>
    private async Task SicherungPruefenAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }
        var auswahl = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Alte Sicherung (lernstand.json.gz) oder alte Datenbank (.db) waehlen",
            AllowMultiple = false,
        });
        if (auswahl.Count == 0)
        {
            return;
        }
        var datei = auswahl[0];
        var inv = CultureInfo.InvariantCulture;
        try
        {
            var uhr = Stopwatch.StartNew();
            using var puffer = new MemoryStream();
            await using (var lesen = await datei.OpenReadAsync())
            {
                await lesen.CopyToAsync(puffer);
            }
            var hash = Convert.ToHexStringLower(SHA256.HashData(puffer.ToArray()));
            Messwerte.Notiere(string.Create(inv, $"Alte Datei: {puffer.Length} Bytes, SHA-256 {hash}"));
            if (datei.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            {
                var pfad = datei.TryGetLocalPath() ?? throw new InvalidOperationException("Datenbank ist keine lokale Datei");
                foreach (var t in AlterLernstand.LeseDatenbank(pfad))
                {
                    Messwerte.Notiere(string.Create(inv, $"  Datenbank Tabelle {t.Name}: {t.Zeilen.Count} Zeilen"));
                }
            }
            else
            {
                puffer.Position = 0;
                using var doc = AlterLernstand.LeseSicherung(puffer);
                var w = doc.RootElement;
                Messwerte.Notiere("  Sicherung Formatversion " + w.GetProperty("formatversion").GetInt32()
                    + ", App " + (w.TryGetProperty("app_version", out var v) ? v.GetString() : "?")
                    + ", Pruefsumme " + (AlterLernstand.PruefsummeStimmt(doc) ? "stimmt" : "STIMMT NICHT"));
                foreach (var (name, anzahl) in AlterLernstand.Zaehlwerte(doc))
                {
                    Messwerte.Notiere(string.Create(inv, $"  {name}: {anzahl}"));
                }
            }
            Messwerte.Notiere(string.Create(inv, $"  Lesezeit: {uhr.Elapsed.TotalMilliseconds:F0} ms"));
            _zustand.Text = "Alte Datei gelesen. Jetzt \"Ergebnis kopieren\".";
        }
        catch (Exception fehler)
        {
            // Nur die Fehlerart, kein Text: der koennte Pfad oder Inhalt enthalten (A3).
            Messwerte.Notiere("Alte Datei: FEHLER " + fehler.GetType().Name);
            _zustand.Text = "Fehler beim Lesen, steht im Ergebnis.";
        }
        _ergebnis.Text = Messwerte.Bericht(Geraet());
    }

    /// <summary>Ablauf ohne Bedienung (Umgebungsvariable LERNPLATTFORM_MESSUNG_AUTO=1), fuer Xvfb und CI.</summary>
    private async Task AutoLaufAsync()
    {
        await ZeigeAsync("Messung");
        await MessenAsync();
        var datei = Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSUNG_DATEI") is { Length: > 0 } d ? d : "messung_auto.txt";
        await File.WriteAllTextAsync(datei, _ergebnis.Text);
        Console.WriteLine(_ergebnis.Text);
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
