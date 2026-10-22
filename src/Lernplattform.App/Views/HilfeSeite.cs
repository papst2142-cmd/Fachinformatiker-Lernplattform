using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Hilfe;
using Lernplattform.Design;
using Lernplattform.Messung;

namespace Lernplattform.Views;

/// <summary>
/// Phase 4, W4: Hilfe wie app_gui.HelpView / main.HelpScreen 0.63.2 (ein Aufbau fuer PC und Handy):
/// Kopfkarte (Titel, Untertitel, Einleitung, "Rundgang starten"), darunter alle 16 Abschnitte als
/// Klappkarten mit der Farbe aus HELP_ACCENTS. Beim Oeffnen ist alles eingeklappt (E11); ein
/// Suchtreffer klappt seinen Abschnitt auf und holt ihn in den sichtbaren Bereich.
/// </summary>
public sealed class HilfeSeite : UserControl, IAufklappbar
{
    /// <summary>HELP_ACCENTS (app_gui 8388), sonst "accent".</summary>
    public static string Farbe(string id) => id switch
    {
        "lernen" or "update" => "Pinsel.accent",
        "pruefung" or "fortschritt" => "Pinsel.purple",
        "rechner" => "Pinsel.green",
        "spiel" or "sicherung" => "Pinsel.accent2",
        "problem" => "Pinsel.orange",
        "schutzprogramm" => "Pinsel.red",
        _ => "Pinsel.accent",
    };

    private readonly Dictionary<string, Expander> _klappen = new(StringComparer.Ordinal);
    private readonly ScrollViewer _rollen;

    public HilfeSeite(Action rundgangStarten)
    {
        var liste = new StackPanel { Spacing = 14, Margin = new Thickness(16) };
        var starten = Schrift.Knopf(RundgangTexte.RundgangStarten);
        starten.HorizontalAlignment = HorizontalAlignment.Left;
        starten.Click += (_, _) => rundgangStarten();
        var kopf = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Schrift.Text(RundgangTexte.HilfeTitel, "Gross", FontWeight.Bold),
                Schrift.Text(RundgangTexte.HilfeUntertitel, "Klein", FontWeight.Normal, "Pinsel.muted"),
                new LernText(HilfeTexte.Einleitung, "Normal", FontWeight.Normal, "Pinsel.text_dim"),
                starten,
            },
        };
        liste.Children.Add(Schrift.Karte(MitMarke(kopf, "Pinsel.green")));
        foreach (var abschnitt in HilfeTexte.Abschnitte)
        {
            var klappe = new Expander
            {
                Header = MitMarke(new LernText(abschnitt.Titel, "Mittel", FontWeight.Bold, "Pinsel.text"), Farbe(abschnitt.Id)),
                Content = new LernText(abschnitt.Text, "Normal", FontWeight.Normal, "Pinsel.text_soft"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            klappe.Farbe(Expander.BackgroundProperty, "Pinsel.card").Farbe(Expander.BorderBrushProperty, "Pinsel.border")
                .Farbe(Expander.ForegroundProperty, "Pinsel.text");
            Avalonia.Automation.AutomationProperties.SetName(klappe, abschnitt.Titel);
            var uhr = new System.Diagnostics.Stopwatch();
            klappe.Expanding += (_, _) => uhr.Restart();
            klappe.Expanded += (_, _) => TopLevel.GetTopLevel(this)?.RequestAnimationFrame(_ =>
            {
                if (uhr.IsRunning)
                {
                    Messwerte.Reihe("Hilfe: Abschnitt aufklappen", uhr.Elapsed.TotalMilliseconds);
                    uhr.Reset();
                }
            });
            _klappen[abschnitt.Id] = klappe;
            var karte = Schrift.Karte(klappe);
            karte.Padding = new Thickness(0);
            liste.Children.Add(karte);
        }
        _rollen = new ScrollViewer { Content = liste };
        Content = _rollen;
    }

    private static Control MitMarke(Control inhalt, string farbe)
    {
        var marke = new Border { Width = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 2, 12, 2) }
            .Farbe(Border.BackgroundProperty, farbe);
        DockPanel.SetDock(marke, Dock.Left);
        return new DockPanel { Children = { marke, inhalt } };
    }

    /// <summary>Abschnitte (fuer den Start ohne Bedienung).</summary>
    public IReadOnlyDictionary<string, Expander> Klappen => _klappen;

    /// <summary>reset_folds: beim Oeffnen der Hilfe alles einklappen und nach oben.</summary>
    public void Gezeigt()
    {
        foreach (var klappe in _klappen.Values)
        {
            klappe.IsExpanded = false;
        }
        _rollen.Offset = default;
    }

    public void AllesAufklappen()
    {
        foreach (var klappe in _klappen.Values)
        {
            klappe.IsExpanded = true;
        }
    }

    /// <summary>open_section (Suche): Abschnitt aufklappen und oben (10 Punkte Abstand) zeigen.</summary>
    public void Oeffnen(string id)
    {
        if (!_klappen.TryGetValue(id, out var klappe))
        {
            return;
        }
        klappe.IsExpanded = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (klappe.Parent is Control karte && karte.TranslatePoint(new Point(0, 0), _rollen.Content as Visual ?? _rollen) is { } punkt)
            {
                _rollen.Offset = new Vector(_rollen.Offset.X, Math.Max(0, punkt.Y - 10));
            }
        }, DispatcherPriority.Loaded);
    }
}
