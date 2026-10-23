using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Suche;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 2, L8: Suche wie app_gui.SearchView (PC-Wortlaut): Suchfeld ("Suchen …  (Strg+F)"),
/// Karte "Suchergebnisse" mit "%d Treffer für „%s“", darunter hoechstens 60 Zeilen, die
/// ersten 12 sofort, dann je 12 alle 15 ms; eine neue Suche bricht die alte ab. Gesucht wird
/// im Hintergrund im einmal vorbereiteten Suchtext (Zustand.Suchtext). Ein Klick auf einen
/// Treffer oeffnet sein Ziel (open_search_hit).
/// </summary>
public sealed class SucheSeite : UserControl
{
    /// <summary>SEARCH_FIRST_ROWS, SEARCH_STEP_ROWS, SEARCH_STEP_MS (app_gui).</summary>
    public const int ErsteZeilen = 12;
    public const int SchrittZeilen = 12;
    public const int SchrittMs = 15;

    private static readonly bool Pc = !OperatingSystem.IsAndroid();

    private static readonly IReadOnlyDictionary<string, string> Farben = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Fachbereiche.Netzwerk] = "Pinsel.cyan",
        [Fachbereiche.Sicherheit] = "Pinsel.pink",
        [Fachbereiche.Systeme] = "Pinsel.purple",
        [Fachbereiche.Wirtschaft] = "Pinsel.green",
        [Fachbereiche.Datenbanken] = "Pinsel.orange",
    };

    private readonly Action<SuchTreffer, string> _oeffnen;
    private readonly TextBox _feld;
    private readonly TextBlock _info = Schrift.Text("", "Normal", FontWeight.Normal, "Pinsel.text_dim");
    private readonly StackPanel _treffer = new() { Spacing = 8 };
    private CancellationTokenSource? _abbruch;
    private DispatcherTimer? _schritt;

    /// <param name="oeffnen">Treffer oeffnen (Treffer, Suchbegriff fuer "Vorlagen").</param>
    public SucheSeite(Action<SuchTreffer, string> oeffnen)
    {
        _oeffnen = oeffnen;
        _feld = new TextBox { PlaceholderText = SucheTexte.Feld, HorizontalAlignment = HorizontalAlignment.Stretch }
            .Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card");
        _feld.Groesse();
        Avalonia.Automation.AutomationProperties.SetName(_feld, SucheTexte.Feld);
        _feld.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (GlobaleSuche.Anfrage(_feld.Text) is { } anfrage)
                {
                    _ = SuchenAsync(anfrage);
                }
            }
        };
        var kopf = Schrift.Karte(new StackPanel
        {
            Spacing = 6,
            Children = { Schrift.Text(SucheTexte.Titel, "Mittel", FontWeight.Bold), _info },
        });
        Content = new ScrollViewer
        {
            Content = new StackPanel { Spacing = 14, Margin = new Thickness(16), Children = { _feld, kopf, _treffer } },
        };
    }

    /// <summary>Strg+F und das Suchsymbol: Feld fokussieren, Text markieren.</summary>
    public void Fokus()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _feld.Focus();
            _feld.SelectAll();
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// do_search/search: alte Suche und geplante Zeilen verwerfen, im Hintergrund suchen, die
    /// ersten Zeilen zeigen. Ergebnis: Millisekunden bis die ersten Zeilen stehen (null bei Abbruch).
    /// </summary>
    public async Task<double?> SuchenAsync(string anfrage)
    {
        var uhr = Stopwatch.StartNew();
        _abbruch?.Cancel();
        _schritt?.Stop();
        _schritt = null;
        var abbruch = new CancellationTokenSource();
        _abbruch = abbruch;
        _feld.Text = anfrage;
        SuchErgebnis? ergebnis = null;
        try
        {
            if (await Zustand.Suchtext is { } text)
            {
                ergebnis = await Task.Run(() => GlobaleSuche.Suchen(text, anfrage, Pc, abbruch.Token), abbruch.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
        if (abbruch.IsCancellationRequested)
        {
            return null;
        }
        ergebnis ??= new SuchErgebnis(anfrage, GlobaleSuche.SchnelleTreffer(anfrage, Pc));
        _treffer.Children.Clear();
        _info.Text = ergebnis.Info;
        if (ergebnis.Treffer.Count == 0)
        {
            _treffer.Children.Add(Schrift.Text(SucheTexte.Keine, "Normal", FontWeight.Normal, "Pinsel.muted"));
            return uhr.Elapsed.TotalMilliseconds;
        }
        var gezeigt = ergebnis.Gezeigt;
        foreach (var treffer in gezeigt.Take(ErsteZeilen))
        {
            _treffer.Children.Add(Zeile(treffer, anfrage));
        }
        var rest = new Queue<SuchTreffer>(gezeigt.Skip(ErsteZeilen));
        var weitere = ergebnis.Weitere;
        if (rest.Count == 0)
        {
            Abschluss(weitere);
        }
        else
        {
            var schritt = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SchrittMs) };
            schritt.Tick += (_, _) =>
            {
                if (abbruch.IsCancellationRequested)
                {
                    schritt.Stop();
                    return;
                }
                for (var i = 0; i < SchrittZeilen && rest.Count > 0; i++)
                {
                    _treffer.Children.Add(Zeile(rest.Dequeue(), anfrage));
                }
                if (rest.Count == 0)
                {
                    schritt.Stop();
                    Abschluss(weitere);
                }
            };
            _schritt = schritt;
            schritt.Start();
        }
        return uhr.Elapsed.TotalMilliseconds;
    }

    /// <summary>Alle geplanten Zeilen stehen (fuer die Layoutpruefung).</summary>
    public bool Fertig => _schritt is not { IsEnabled: true };

    private void Abschluss(string? weitere)
    {
        if (weitere is not null)
        {
            var text = Schrift.Text(weitere, "Klein", FontWeight.Normal, "Pinsel.muted");
            text.Margin = new Thickness(0, 8);
            _treffer.Children.Add(text);
        }
    }

    private Button Zeile(SuchTreffer treffer, string anfrage)
    {
        var farbe = treffer.Fachbereich is { } f && Farben.TryGetValue(f, out var k) ? k : "Pinsel.purple";
        var inhalt = new StackPanel
        {
            Spacing = 3,
            Children =
            {
                Schrift.Text(treffer.Kopf, "Klein", FontWeight.Normal, "Pinsel.muted"),
                Schrift.Text(treffer.Titel, "Normal", FontWeight.Bold),
                Schrift.Text(treffer.Ausschnitt, "Klein", FontWeight.Normal, "Pinsel.text_dim"),
            },
        };
        var marke = new Border { Width = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 2, 10, 2) }.Farbe(Border.BackgroundProperty, farbe);
        var zeile = new DockPanel { Children = { marke, inhalt } };
        DockPanel.SetDock(marke, Dock.Left);
        var knopf = new Button
        {
            Content = zeile,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
        }.Farbe(Button.BackgroundProperty, "Pinsel.card").Farbe(Button.BorderBrushProperty, "Pinsel.border")
            .Farbe(Button.ForegroundProperty, "Pinsel.text");
        Avalonia.Automation.AutomationProperties.SetName(knopf, treffer.Kopf + ": " + treffer.Titel);
        knopf.Click += (_, _) => _oeffnen(treffer, anfrage);
        return knopf;
    }
}
