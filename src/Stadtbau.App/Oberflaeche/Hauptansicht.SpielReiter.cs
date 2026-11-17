using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Lernplattform.Design;
using Lernplattform.Views;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Sammelliste 378 (Weisung P12-298, Nico): Spielgeschichte, Quests und Questgeber liegen unter EINEM ausklappbaren Reiter
/// „Spiel ▾“ in der oberen Leiste. Ein Tipp darauf oeffnet die zuletzt gewaehlte Seite (zuerst die Spielgeschichte) und klappt
/// links eine Seitenleiste mit den drei Seiten aus; jede wird dort einzeln gewaehlt. Die Seitenleiste liegt ueber der Seite
/// (sie nimmt ihr keine Breite, die Verkettungen bleiben lesbar, Weisung P12-187) und klappt nach der Wahl einer Seite, bei
/// einem Tipp daneben und beim zweiten Tipp auf „Spiel ▴“ wieder ein (die Seite bleibt offen). Die drei Knoepfe sind dieselben wie bisher (Namen fuer den Screenreader, Tipps, Tasten);
/// „Gebäude entwerfen“ steht in der Leiste direkt nach „3D-Ansicht“.
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>Die Seiten unter „Spiel ▾“ in der Reihenfolge der Seitenleiste.</summary>
    public static readonly string[] SpielSeiten = [Texte.Spielgeschichte, Texte.Quests, Texte.Questgeber];

    /// <summary>Gegenprobe (nur Tests, AsyncLocal nicht noetig: nur beim Bau gelesen): Leiste wie bis S 0.57.0.</summary>
    internal static bool SpielReiterAltFuerTests { get; set; }

    private readonly StackPanel _spielSeitenleiste = new() { Spacing = 4, Margin = new Thickness(10), Width = 190, IsVisible = false };
    private readonly ContentControl _spielSeite = new();
    private Grid? _spielHuelle;
    private Border? _spielRahmen;

    /// <summary>Seitenleiste „Spiel“ einklappen (nach der Wahl einer Seite oder einem Druck daneben).</summary>
    private void SpielSeitenleisteZu()
    {
        if (_spielSeitenleiste.IsVisible)
        {
            _spielSeitenleiste.IsVisible = false;
            SpielMenueBeschriften();
        }
    }
    private string? _spielZuletzt;

    /// <summary>Start-Test: die Seitenleiste mit den drei Seiten (sichtbar = ausgeklappt).</summary>
    internal StackPanel SpielSeitenleiste => _spielSeitenleiste;

    /// <summary>Ist die Seitenleiste „Spiel“ ausgeklappt und zu sehen?</summary>
    internal bool SpielAusgeklappt => _geschichteOffen && _spielSeitenleiste.IsVisible;

    /// <summary>Start-Test: Breite der Seite unter „Spiel“ (gleich mit und ohne Seitenleiste).</summary>
    internal double SpielSeiteBreite => _spielSeite.Bounds.Width;

    /// <summary>Eine Seite unter „Spiel“ in die Ansicht setzen (mit der Seitenleiste links).</summary>
    private void SpielSeiteSetzen(Control seite)
    {
        _spielZuletzt = _seite ?? Texte.Spielgeschichte;
        if (SpielReiterAltFuerTests)
        {
            _inhalt.Content = seite;
            return;
        }
        if (_spielHuelle is null)
        {
            var rahmen = new Border
            {
                Child = new ScrollViewer { Content = _spielSeitenleiste },
                BorderThickness = new Thickness(0, 0, 1, 1),
                CornerRadius = new CornerRadius(0, 0, 10, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                IsVisible = _spielSeitenleiste.IsVisible,
            }.Farbe(Border.BorderBrushProperty, "Pinsel.border").Farbe(Border.BackgroundProperty, "Pinsel.card");
            AutomationProperties.SetName(rahmen, Texte.SpielMenueName);
            _spielRahmen = rahmen;
            // ueber der Seite (Reihenfolge: Seite, dann Seitenleiste); ein Druck auf die Seite klappt sie ein
            _spielHuelle = new Grid();
            _spielHuelle.Children.Add(_spielSeite);
            _spielHuelle.Children.Add(rahmen);
            _spielSeite.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (_, _) => SpielSeitenleisteZu(), Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
            _spielSeitenleiste.PropertyChanged += (_, e) =>
            {
                if (e.Property == Visual.IsVisibleProperty)
                {
                    rahmen.IsVisible = _spielSeitenleiste.IsVisible;
                }
            };
        }
        _spielSeite.Content = seite;
        _inhalt.Content = _spielHuelle;
    }

    /// <summary>Knopf „Spiel ▾“: Seite oeffnen und Seitenleiste ausklappen, offen: Seitenleiste ein-/ausklappen.</summary>
    private void SpielMenueGedrueckt()
    {
        if (_geschichteOffen && _entwurf is null)
        {
            _spielSeitenleiste.IsVisible = !_spielSeitenleiste.IsVisible;
            Antippbar(_spielRahmen ?? (Control)_spielSeitenleiste);
            SpielMenueBeschriften();
            Ansage.Sagen(_spielSeitenleiste.IsVisible ? Texte.SpielMenueAuf : Texte.SpielMenueZu);
            return;
        }
        _spielSeitenleiste.IsVisible = true;
        ReiterGedrueckt(_spielZuletzt ?? Texte.Spielgeschichte);
        Antippbar(_spielRahmen ?? (Control)_spielSeitenleiste);
        SpielMenueBeschriften();
    }

    /// <summary>Beschriftung „Spiel ▾“ (zu) bzw. „Spiel ▴“ (ausgeklappt).</summary>
    private void SpielMenueBeschriften()
    {
        if (!_kopf.TryGetValue(Texte.SpielMenue, out var k))
        {
            return;
        }
        var auf = SpielAusgeklappt;
        if (k.Content is TextBlock t)
        {
            t.Text = Texte.SpielMenue + (auf ? " ▴" : " ▾");
        }
        else
        {
            k.Content = Texte.SpielMenue + (auf ? " ▴" : " ▾");
        }
    }

    /// <summary>Die drei Knoepfe der Seiten in die Seitenleiste legen (Breite gleich, mindestens 44 hoch).</summary>
    private void SpielSeitenleisteFuellen()
    {
        _spielSeitenleiste.Children.Add(Schrift.Text(Texte.SpielMenue, "Klein", Avalonia.Media.FontWeight.Bold, "Pinsel.muted"));
        foreach (var name in SpielSeiten)
        {
            var k = _kopf[name];
            k.HorizontalAlignment = HorizontalAlignment.Stretch;
            k.HorizontalContentAlignment = HorizontalAlignment.Left;
            k.CornerRadius = new CornerRadius(10);
            _spielSeitenleiste.Children.Add(k);
            // nach der Wahl einer Seite klappt die Seitenleiste ein (nach dem Oeffnen der Seite)
            k.AddHandler(Button.ClickEvent, (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(SpielSeitenleisteZu), Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        }
    }
}
