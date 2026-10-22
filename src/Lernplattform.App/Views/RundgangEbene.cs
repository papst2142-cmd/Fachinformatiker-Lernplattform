using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lernplattform.Core.Hilfe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Messung;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 4, W4: Rundgang wie app_gui.TourOverlay / main.TourDialog 0.63.2 als Ebene ueber dem ganzen
/// Fenster (Klicks gehen nicht durch). Ablauf, Entwurf und Tab-Reihenfolge kommen aus
/// <see cref="RundgangAblauf"/> (Core). Tastatur: Tab/Umschalt+Tab ringfoermig, Eingabe/Leertaste
/// loest aus bzw. schaltet um, Eingabe im Textfeld speichert, Esc und die Zurueck-Geste ueberspringen.
/// Die Tastenzeile steht nur am PC (B13). Speichern schreibt Name, Rahmenplan und Merker in einem
/// Schreibvorgang; geht das nicht, bleibt der Rundgang offen und nennt N9.
/// </summary>
public sealed class RundgangEbene
{
    private static readonly bool Pc = !OperatingSystem.IsAndroid();

    private static Panel? _ebene;

    /// <summary>Gerade offener Rundgang (ein Rundgang zur Zeit).</summary>
    public static RundgangEbene? Offen { get; private set; }

    /// <summary>Wird von MainView gesetzt: die Ebene ueber allen Seiten, unter den Hinweisfenstern.</summary>
    internal static void Ebene(Panel ebene) => _ebene = ebene;

    private readonly RundgangAblauf _ablauf = new(Core.Hilfe.Einrichten.Startwerte(Zustand.Werte));
    private readonly TaskCompletionSource<bool> _fertig = new();
    private readonly Border _fenster;
    private readonly Dictionary<RundgangElement, Control> _elemente = [];
    private TextBox? _name;
    private TextBox? _ap1;
    private TextBox? _ap2;
    private TextBlock? _fehler;
    private TopLevel? _top;
    private bool _probe;

    private RundgangEbene()
    {
        _fenster = new Border { Background = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)) };
        // wie grab_set: Klicks und Ziehen gehen nicht an die Ansicht darunter
        _fenster.PointerPressed += (_, e) => e.Handled = true;
        _fenster.AddHandler(InputElement.KeyDownEvent, Taste, RoutingStrategies.Tunnel);
        // Leertaste: Taste loest beim Druecken aus; das Loslassen darf Knopf oder Schalter nicht ein zweites Mal ausloesen
        _fenster.AddHandler(InputElement.KeyUpEvent, (_, e) =>
        {
            if (e.Key == Key.Space && Dialog.Offen is null && Fokus() is not null)
            {
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
    }

    /// <summary>Ablauf (fuer den Start ohne Bedienung).</summary>
    public RundgangAblauf Ablauf => _ablauf;

    /// <summary>Bedienelemente der gezeigten Seite.</summary>
    public IReadOnlyDictionary<RundgangElement, Control> Elemente => _elemente;

    public Control Fenster => _fenster;

    /// <summary>
    /// Rundgang zeigen; Ergebnis true nach "Speichern und loslegen", false nach Ueberspringen.
    /// Ist schon einer offen, kommt dessen Ergebnis (kein zweiter Rundgang).
    /// </summary>
    public static Task<bool> Starten()
    {
        if (Offen is { } offen)
        {
            return offen._fertig.Task;
        }
        return Oeffnen(false)?._fertig.Task ?? Task.FromResult(false);
    }

    /// <summary>
    /// Start ohne Bedienung und Bilder: derselbe Rundgang, der nichts speichert ("Fertig" mit gueltigen
    /// Angaben, "Ueberspringen", Esc schliessen ohne Schreibvorgang). null, wenn schon einer offen ist.
    /// </summary>
    public static RundgangEbene? Probe() => Offen is null ? Oeffnen(true) : null;

    private static RundgangEbene? Oeffnen(bool probe)
    {
        if (_ebene is not { } ebene)
        {
            return null;
        }
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var rundgang = new RundgangEbene { _probe = probe };
        Offen = rundgang;
        ebene.Children.Add(rundgang._fenster);
        ebene.IsVisible = true;
        rundgang._top = TopLevel.GetTopLevel(ebene);
        if (rundgang._top is { } top)
        {
            top.BackRequested += rundgang.Zurueckgeste;
        }
        rundgang.Zeigen();
        rundgang._top?.RequestAnimationFrame(_ => Messwerte.Reihe("Rundgang zeigen", uhr.Elapsed.TotalMilliseconds));
        return rundgang;
    }

    /// <summary>Geschlossen (Start ohne Bedienung).</summary>
    public bool Geschlossen => _fertig.Task.IsCompleted;

    /// <summary>Element mit dem Fokus (Start ohne Bedienung, Tastaturpruefung).</summary>
    public RundgangElement? FokusElement => Fokus();

    /// <summary>Fehlerzeile der Seite 5, wenn sichtbar.</summary>
    public string? FehlerZeile => _fehler is { IsVisible: true } f ? f.Text : null;

    private void Zurueckgeste(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Ueberspringen();
    }

    /// <summary>Seite direkt zeigen (Start ohne Bedienung, Bilder).</summary>
    public void Springen(int index)
    {
        Entwurf();
        _ablauf.Springen(index);
        Zeigen();
    }

    private void Zeigen()
    {
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        _elemente.Clear();
        _name = _ap1 = _ap2 = null;
        _fehler = null;
        var seite = _ablauf.Seite;
        var punkte = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < RundgangAblauf.Anzahl; i++)
        {
            punkte.Children.Add(new Ellipse { Width = 8, Height = 8 }.Farbe(Shape.FillProperty, i == _ablauf.Index ? "Pinsel.accent" : "Pinsel.border_hi"));
        }
        var kopf = new DockPanel();
        DockPanel.SetDock(punkte, Dock.Right);
        kopf.Children.Add(punkte);
        kopf.Children.Add(Schrift.Text(_ablauf.Kopf, "Klein", FontWeight.Bold, "Pinsel.accent"));
        var koerper = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                kopf,
                new LernText(seite.Titel, "Gross", FontWeight.Bold, "Pinsel.text"),
                new LernText(seite.Text, "Normal", FontWeight.Normal, "Pinsel.text_soft"),
            },
        };
        if (_ablauf.Letzte)
        {
            koerper.Children.Add(Einrichten());
        }
        koerper.Children.Add(Knoepfe());
        if (Geraetetexte.RundgangTasten(Pc) is { } tasten)
        {
            koerper.Children.Add(Schrift.Text(tasten, "Klein", FontWeight.Normal, "Pinsel.muted"));
        }
        var karte = Schrift.Karte(new ScrollViewer { Content = koerper });
        karte.BorderThickness = new Thickness(2);
        karte.Farbe(Border.BorderBrushProperty, "Pinsel.accent");
        karte.CornerRadius = new CornerRadius(22);
        karte.MaxWidth = 640;
        karte.Margin = new Thickness(14, 24);
        karte.HorizontalAlignment = HorizontalAlignment.Stretch;
        karte.VerticalAlignment = VerticalAlignment.Center;
        Avalonia.Automation.AutomationProperties.SetName(karte, RundgangTexte.Titel);
        _fenster.Child = karte;
        // Fokus: erstes Eingabefeld der Einrichtung, sonst "Weiter"
        Dispatcher.UIThread.Post(() => _elemente[_ablauf.Reihenfolge[0]].Focus(NavigationMethod.Tab));
        _top?.RequestAnimationFrame(_ => Messwerte.Reihe(_ablauf.Letzte ? "Rundgang: Seite 5 bauen" : "Rundgang: Schritt", uhr.Elapsed.TotalMilliseconds));
    }

    private static TextBlock Beschriftung(string text) => Schrift.Text(Beschriftungen.Gross(text), "Klein", FontWeight.Bold, "Pinsel.muted");

    private static TextBox Feld(string wert, double breite) => new TextBox
    {
        Text = wert,
        HorizontalAlignment = HorizontalAlignment.Left,
        MinWidth = breite,
    }.Farbe(TextBox.ForegroundProperty, "Pinsel.text");

    private Control Einrichten()
    {
        var form = new StackPanel { Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };
        form.Children.Add(Beschriftung(RundgangTexte.Name));
        _name = Feld(_ablauf.Name, 260);
        _name.PlaceholderText = RundgangTexte.NameZusatz;
        Avalonia.Automation.AutomationProperties.SetName(_name, RundgangTexte.Name);
        _elemente[RundgangElement.Name] = _name;
        form.Children.Add(_name);
        var plan = Beschriftung(RundgangTexte.Plan);
        plan.Margin = new Thickness(0, 8, 0, 0);
        form.Children.Add(plan);
        foreach (var (abschnitt, text) in RahmenplanTexte.AbschnittTexte)
        {
            var schalter = new ToggleSwitch
            {
                Content = new LernText(text, "Klein", FontWeight.Normal, "Pinsel.text_dim"),
                IsChecked = _ablauf.Abschnitte[abschnitt],
                OnContent = null,
                OffContent = null,
            };
            Avalonia.Automation.AutomationProperties.SetName(schalter, text);
            schalter.IsCheckedChanged += (_, _) => _ablauf.Abschnitte[abschnitt] = schalter.IsChecked == true;
            _elemente[abschnitt switch { "B" => RundgangElement.AbschnittB, "D" => RundgangElement.AbschnittD, _ => RundgangElement.AbschnittE }] = schalter;
            form.Children.Add(schalter);
        }
        form.Children.Add(new LernText(RundgangTexte.PlanHinweis, "Klein", FontWeight.Normal, "Pinsel.muted"));
        var termine = Beschriftung(RundgangTexte.Termine);
        termine.Margin = new Thickness(0, 8, 0, 0);
        form.Children.Add(termine);
        var reihe = new WrapPanel();
        foreach (var (element, text, wert) in new[]
        {
            (RundgangElement.TerminAp1, RahmenplanTexte.TerminAp1, _ablauf.TerminAp1),
            (RundgangElement.TerminAp2, RahmenplanTexte.TerminAp2, _ablauf.TerminAp2),
        })
        {
            var feld = Feld(wert, 130);      // kein Platzhalter "TT.MM.JJJJ" (N12)
            Avalonia.Automation.AutomationProperties.SetName(feld, text);
            _elemente[element] = feld;
            if (element == RundgangElement.TerminAp1)
            {
                _ap1 = feld;
            }
            else
            {
                _ap2 = feld;
            }
            var beschriftung = Schrift.Text(text, "Klein", FontWeight.Normal, "Pinsel.text_dim");
            beschriftung.VerticalAlignment = VerticalAlignment.Center;
            beschriftung.Margin = new Thickness(0, 0, 10, 0);
            reihe.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 28, 6),
                Children = { beschriftung, feld },
            });
        }
        form.Children.Add(reihe);
        form.Children.Add(new LernText(HilfeTexte.TermineHinweis, "Klein", FontWeight.Normal, "Pinsel.muted"));
        _fehler = Schrift.Text(_ablauf.Fehler ?? "", "Klein", FontWeight.Normal, "Pinsel.red");
        _fehler.IsVisible = _ablauf.Fehler is not null;
        form.Children.Add(_fehler);
        return form;
    }

    private Control Knoepfe()
    {
        var weiter = Schrift.Knopf(_ablauf.Letzte ? RundgangTexte.Fertig : RundgangTexte.Weiter, betont: true);
        weiter.Click += (_, _) => Fertig();
        _elemente[_ablauf.Letzte ? RundgangElement.Fertig : RundgangElement.Weiter] = weiter;
        var ueberspringen = Schrift.Knopf(RundgangTexte.Ueberspringen);
        ueberspringen.Click += (_, _) => Ueberspringen();
        _elemente[RundgangElement.Ueberspringen] = ueberspringen;
        Button? zurueck = null;
        if (_ablauf.Index > 0)
        {
            zurueck = Schrift.Knopf(RundgangTexte.Zurueck);
            zurueck.Click += (_, _) =>
            {
                Entwurf();
                if (_ablauf.Zurueck())
                {
                    Zeigen();
                }
            };
            _elemente[RundgangElement.Zurueck] = zurueck;
        }
        foreach (var (element, knopf) in _elemente)
        {
            if (knopf is Button b)
            {
                Avalonia.Automation.AutomationProperties.SetName(b, ((TextBlock)b.Content!).Text);
            }
        }
        // PC eine Zeile (links Ueberspringen, rechts Zurueck und Weiter/Speichern), wenn sie passt; sonst wie
        // am Handy oben der Hauptknopf, darunter Ueberspringen und Zurueck (main.py 5059-5066)
        var reihe = new KnopfReihe { EineZeile = Pc, Margin = new Thickness(0, 10, 0, 0) };
        foreach (var knopf in new[] { weiter, ueberspringen, zurueck })
        {
            if (knopf is not null)
            {
                knopf.HorizontalAlignment = HorizontalAlignment.Stretch;
                reihe.Children.Add(knopf);
            }
        }
        return reihe;
    }

    /// <summary>_keep_draft: Eingaben der Seite 5 behalten.</summary>
    private void Entwurf()
    {
        if (_name is not null)
        {
            _ablauf.Name = _name.Text ?? "";
        }
        if (_ap1 is not null)
        {
            _ablauf.TerminAp1 = _ap1.Text ?? "";
        }
        if (_ap2 is not null)
        {
            _ablauf.TerminAp2 = _ap2.Text ?? "";
        }
    }

    /// <summary>"Weiter" bzw. "Speichern und loslegen" (finish).</summary>
    public void Fertig()
    {
        Entwurf();
        var aenderung = _ablauf.Fertig(DateOnly.FromDateTime(DateTime.Now));
        if (!_ablauf.Letzte || (aenderung is null && _ablauf.Fehler is null))
        {
            Zeigen();
            return;
        }
        if (_probe && aenderung is not null)
        {
            Schliessen(false);
            return;
        }
        if (aenderung is null || !Zustand.Aendere(aenderung))
        {
            if (aenderung is not null)
            {
                _ablauf.SpeichernFehlgeschlagen();
            }
            FehlerZeigen();
            return;
        }
        Schliessen(true);
    }

    private void FehlerZeigen()
    {
        if (_fehler is not null)
        {
            _fehler.Text = _ablauf.Fehler ?? "";
            _fehler.IsVisible = _ablauf.Fehler is not null;
        }
        if (_ablauf.FehlerFeld is { } feld && _elemente.TryGetValue(feld, out var control) && control is TextBox box)
        {
            box.Farbe(TextBox.BorderBrushProperty, "Pinsel.red");
        }
    }

    /// <summary>"Ueberspringen", Esc, Zurueck-Geste: nur der Merker (geht er nicht, schliesst der Rundgang trotzdem).</summary>
    public void Ueberspringen()
    {
        if (!_probe)
        {
            Zustand.Aendere(RundgangAblauf.Ueberspringen(DateOnly.FromDateTime(DateTime.Now)));
        }
        Schliessen(false);
    }

    private void Schliessen(bool gespeichert)
    {
        if (_top is { } top)
        {
            top.BackRequested -= Zurueckgeste;
        }
        if (_ebene is { } ebene)
        {
            ebene.Children.Remove(_fenster);
            ebene.IsVisible = ebene.Children.Count > 0;
        }
        Offen = null;
        _fertig.TrySetResult(gespeichert);
    }

    private RundgangElement? Fokus()
    {
        var fokus = _top?.FocusManager?.GetFocusedElement() as Visual;
        foreach (var (element, control) in _elemente)
        {
            for (var v = fokus; v is not null; v = v.GetVisualParent())
            {
                if (ReferenceEquals(v, control))
                {
                    return element;
                }
            }
        }
        return null;
    }

    private void Taste(object? sender, KeyEventArgs e)
    {
        if (Dialog.Offen is not null)
        {
            return;
        }
        var aktuell = Fokus();
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Ueberspringen();
                break;
            case Key.Tab:
                e.Handled = true;
                var naechstes = _ablauf.Naechstes(aktuell ?? _ablauf.Reihenfolge[^1], e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                _elemente[naechstes].Focus(NavigationMethod.Tab);
                break;
            case Key.Enter or Key.Space when aktuell is { } element && _elemente[element] is ToggleSwitch schalter:
                e.Handled = true;
                schalter.IsChecked = schalter.IsChecked != true;
                break;
            case Key.Enter when aktuell is { } element && _elemente[element] is TextBox:
                e.Handled = true;
                if (!_ablauf.Letzte)
                {
                    break;
                }
                Fertig();
                break;
            case Key.Enter or Key.Space when aktuell is { } element && _elemente[element] is Button knopf:
                e.Handled = true;
                knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                break;
        }
    }
}

