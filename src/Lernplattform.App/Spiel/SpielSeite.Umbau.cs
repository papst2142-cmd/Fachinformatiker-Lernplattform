using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Design;
using Lernplattform.Views;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 1.10.0 (Weisung P12-381, Nico 3c mit Hinweis 1a, Bild 08 P12-245): nach einem Umbau der Wohnung im Werkzeug stehen
/// Moebel, deren Platz nicht mehr passt, im Karton (<see cref="Wohnung.Umgebaut"/>). Beim ersten Betreten der Wohnung danach
/// steht einmal ein Hinweis mit orangem Rand ueber dem Kartonstapel (mehrere Moebel in einem Hinweis); ein Tipp schliesst ihn,
/// beim Verlassen der Wohnung verschwindet er. Gemerkt wird er nur in einstellungen.json (<see cref="Wohnung.Merker"/>).
/// </summary>
public sealed partial class SpielSeite
{
    private Klickflaeche? _umbauBlase;
    private bool _umbauGeprueft;

    /// <summary>Steht der Umbau-Hinweis gerade da (Start-Test)?</summary>
    internal bool UmbauHinweisSichtbar => _umbauBlase is { IsVisible: true };

    /// <summary>Text des Umbau-Hinweises (Start-Test; leer = keiner).</summary>
    internal string UmbauHinweisText { get; private set; } = "";

    /// <summary>Je Zustand: in der Wohnung einmal den Hinweis zeigen; draussen weg und beim naechsten Betreten neu pruefen.</summary>
    private void UmbauHinweisZeigen(StadtSpiel spiel)
    {
        var zuhause = spiel.ImInnenraum && spiel.Innen?.Ansicht == SpielDaten.OrtZuhause;
        if (!zuhause)
        {
            _umbauGeprueft = false;
            if (_umbauBlase is { IsVisible: true })
            {
                _umbauBlase.IsVisible = false;
            }
            return;
        }
        if (_umbauGeprueft)
        {
            return;
        }
        _umbauGeprueft = true;
        var stand = spiel.Ablauf.Stand;
        if (!Wohnung.UmbauHinweisOffen(stand, Programm.Zustand.Werte) || Wohnung.UmbauKennung(stand) is not { } kennung || Wohnung.UmbauText(stand) is not { } text)
        {
            return;
        }
        Programm.Zustand.Aendere(w => Wohnung.UmbauGesehen(w, kennung));
        UmbauHinweisText = Wohnung.UmbauTitel + " " + text;
        var inhalt = new StackPanel { Spacing = 2, MaxWidth = 280 };
        inhalt.Children.Add(Schrift.Text(Wohnung.UmbauTitel, "Klein", FontWeight.Bold));
        var satz = Schrift.Text(text, "Klein");
        satz.TextWrapping = TextWrapping.Wrap;
        inhalt.Children.Add(satz);
        if (_umbauBlase is null)
        {
            _umbauBlase = new Klickflaeche { CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), BorderThickness = new Thickness(2) };
            _umbauBlase.Farbe(Border.BackgroundProperty, "Pinsel.card").Farbe(Border.BorderBrushProperty, "Pinsel.orange");
            _umbauBlase.Klick = () => _umbauBlase.IsVisible = false;
            _schilder.Children.Add(_umbauBlase);
        }
        _umbauBlase.Child = inhalt;
        _umbauBlase.Vorlese(UmbauHinweisText);
        _umbauBlase.IsVisible = true;
        UmbauBlaseSetzen();
    }

    /// <summary>Hinweis ueber den Kartonstapel setzen (je Bild); ist der Stapel nicht im Bild: oben in der Mitte.</summary>
    private void UmbauBlaseSetzen()
    {
        if (_umbauBlase is not { IsVisible: true } blase || Spiel is not { } spiel)
        {
            return;
        }
        if (!blase.IsMeasureValid)
        {
            blase.Measure(Size.Infinity);
        }
        var (w, h) = (blase.DesiredSize.Width, blase.DesiredSize.Height);
        var breite = Flaeche.Bounds.Width;
        var hoehe = Flaeche.Bounds.Height;
        var links = (breite - w) / 2;
        var oben = Flaeche.Frei.Oben + 8;
        if (Wohnung.Bleibe(spiel.Ablauf.Stand.Wohnung, spiel.Ablauf.Stand.Inhalt) is { } bleibe && SpielDaten.Get(bleibe, "kartons") is List<object?> { Count: 4 } k
            && Flaeche.AktuelleKamera is { } kamera && Flaeche.GlBereit
            && kamera.Bildpunkt(new Vector3((float)(Py.F(k[0]) + (Py.F(k[2]) / 2)), 1.6f, (float)(Py.F(k[1]) + (Py.F(k[3]) / 2))), (float)breite, (float)hoehe) is { } p)
        {
            links = p.X - (w / 2);
            oben = p.Y - h - 8;
        }
        links = Math.Clamp(links, 4, Math.Max(4, breite - w - 4));
        oben = Math.Clamp(oben, Flaeche.Frei.Oben + 4, Math.Max(Flaeche.Frei.Oben + 4, hoehe - Flaeche.Frei.Unten - h - 4));
        Canvas.SetLeft(blase, links);
        Canvas.SetTop(blase, oben);
        blase.Arrange(new Rect(links, oben, w, h));
    }
}
