using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Views;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle Q (Weisung P12-76, Kurzplan Meldung P12-42 Punkte 6 und 7): klappbare Gruppe in den Spalten des Werkzeugs. Eine Karte in
/// den Farben der Farbwelt; der Kopf ist ein Knopf (Titel links, Pfeil rechts), darunter der Inhalt. Der Screenreader hoert
/// „Titel, aufgeklappt/zugeklappt“ wie bei den Klappkarten der Lernplattform (<see cref="KlappTexte.Name"/>). Der Titel laesst
/// sich aendern (Kopf „Prüfen – 2 rot“ in Rot).
/// </summary>
internal sealed class Klappgruppe : Border
{
    private readonly TextBlock _titel;
    private readonly Path _pfeil;
    private readonly Border _koerper;
    private bool _offen;

    /// <summary>Kennung der Gruppe (zum Merken, welche offen ist).</summary>
    public string Kennung { get; }

    /// <summary>Der Knopf im Kopf (Start-Test: Name fuer den Screenreader, Klick).</summary>
    public Button Kopf { get; }

    /// <summary>Der Koerper unter dem Kopf (zugeklappt unsichtbar).</summary>
    public Control Koerper => _koerper;

    /// <summary>Der Inhalt unter dem Kopf.</summary>
    public StackPanel Inhalt { get; } = new() { Spacing = 8 };

    /// <summary>Aufgeklappt (true) oder zugeklappt (false); ein Klick auf den Kopf schaltet um.</summary>
    public event Action<Klappgruppe>? Umgeschaltet;

    public Klappgruppe(string kennung, string titel, string? tipp, bool offen, params Control[] inhalt)
    {
        Kennung = kennung;
        _titel = Schrift.Text(titel, "Mittel", FontWeight.Bold);
        _titel.VerticalAlignment = VerticalAlignment.Center;
        _pfeil = new Path
        {
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform,
            StrokeThickness = 2.2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _pfeil.Farbe(Shape.StrokeProperty, "Pinsel.text");
        var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        zeile.Children.Add(_titel);
        Grid.SetColumn(_pfeil, 1);
        zeile.Children.Add(_pfeil);
        Kopf = new Button
        {
            Content = zeile,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(16, 12),
            CornerRadius = new CornerRadius(15),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        if (tipp is not null)
        {
            // Tipp vor dem eigenen Klick (langer Druck am Tablet zeigt nur den Tipp, siehe Tipp.Mit)
            Kopf.Mit(tipp);
        }
        Kopf.Click += (_, _) =>
        {
            Offen = !Offen;
            Umgeschaltet?.Invoke(this);
        };
        foreach (var c in inhalt)
        {
            Inhalt.Children.Add(c);
        }
        _koerper = new Border { Child = Inhalt, Padding = new Thickness(16, 4, 16, 16) };
        Child = new StackPanel { Children = { Kopf, _koerper } };
        CornerRadius = new CornerRadius(16);
        BorderThickness = new Thickness(1);
        this.Farbe(BackgroundProperty, "Pinsel.card").Farbe(BorderBrushProperty, "Pinsel.border");
        _offen = !offen;
        Offen = offen;
    }

    public bool Offen
    {
        get => _offen;
        set
        {
            if (_offen == value)
            {
                return;
            }
            _offen = value;
            _koerper.IsVisible = value;
            // Pfeil nach unten (zu) bzw. nach oben (offen) wie im Entwurf P12-42
            _pfeil.Data = Geometry.Parse(value ? "M 1,9 L 7,3 L 13,9" : "M 1,4 L 7,10 L 13,4");
            AutomationProperties.SetName(Kopf, KlappTexte.Name(Titel, value));
        }
    }

    public string Titel => _titel.Text ?? "";

    /// <summary>Titel und Farbe des Titels aendern (Name fuer den Screenreader wandert mit).</summary>
    public void TitelSetzen(string text, string farbe = "Pinsel.text")
    {
        _titel.Text = text;
        _titel.Farbe(TextBlock.ForegroundProperty, farbe);
        AutomationProperties.SetName(Kopf, KlappTexte.Name(text, _offen));
    }
}
