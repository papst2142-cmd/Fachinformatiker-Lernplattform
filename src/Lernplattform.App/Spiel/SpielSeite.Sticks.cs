using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 0.96.0 (Sammelliste 385 Welle 2, Weisung P12-309): die zwei Sticks als Ebene ueber der 3D-Flaeche (nie antippbar,
/// die Finger nimmt die Flaeche selbst). Je Stick ein Ring (Rand in der Textfarbe der Farbwelt, Flaeche halb durchsichtig) und
/// ein Knopf unter dem Daumen (Akzentfarbe). Sie erscheinen nur, solange ein Daumen sie haelt; liegt das Spiel-Handy, die
/// Spielansicht oder die Ortsliste (ohne 3D) ueber der Stadt, gibt es keine (<see cref="StadtFlaeche.SticksGesperrt"/>).
/// </summary>
public sealed partial class SpielSeite
{
    private readonly Canvas _stickEbene = new() { IsHitTestVisible = false, ClipToBounds = true };
    private readonly Dictionary<Sticks.Seite, (Ellipse Ring, Ellipse Knopf)> _stickBilder = [];

    /// <summary>Start-Test, Fotos: Ring eines sichtbaren Sticks in Punkten der Seite (null ohne Stick).</summary>
    public Rect? StickRing(Sticks.Seite seite) =>
        _stickBilder.TryGetValue(seite, out var b) && b.Ring.IsVisible ? new Rect(Canvas.GetLeft(b.Ring), Canvas.GetTop(b.Ring), b.Ring.Width, b.Ring.Height) : null;

    /// <summary>Start-Test: oberer Rand der unteren Leiste und unterer Rand der Kopfzeile in Punkten der Seite.</summary>
    public (double KopfUnten, double LeisteOben) StickGrenzen =>
        (_kopf.TranslatePoint(new Point(0, _kopf.Bounds.Height), this)?.Y ?? 0,
         _unten.IsVisible ? _unten.TranslatePoint(default, this)?.Y ?? Bounds.Height : Bounds.Height);

    private void SticksAnlegen()
    {
        foreach (var (seite, name) in new[] { (Sticks.Seite.Links, SpielProbeTexte.StickGehen), (Sticks.Seite.Rechts, SpielProbeTexte.StickDrehen) })
        {
            var ring = new Ellipse { StrokeThickness = 3, Opacity = 0.75, IsVisible = false };
            ring.Bind(Shape.StrokeProperty, ring.GetResourceObservable("Pinsel.text"));
            ring.Bind(Shape.FillProperty, ring.GetResourceObservable("Pinsel.card"));
            var knopf = new Ellipse { StrokeThickness = 2, IsVisible = false };
            knopf.Bind(Shape.StrokeProperty, knopf.GetResourceObservable("Pinsel.text"));
            knopf.Bind(Shape.FillProperty, knopf.GetResourceObservable("Pinsel.accent"));
            Avalonia.Automation.AutomationProperties.SetName(ring, name);
            Avalonia.Automation.AutomationProperties.SetName(knopf, name);
            _stickEbene.Children.Add(ring);
            _stickEbene.Children.Add(knopf);
            _stickBilder[seite] = (ring, knopf);
        }
        Children.Add(_stickEbene);
        // Welle A 1.0.0 (Sammelliste 405): die runden Knoepfe stehen unten rechts in der Ecke; dort setzt kein Stick auf
        Flaeche.KnopfSperre = b => DaumenKnoepfeAn && (_springenRund?.IsVisible ?? false) ? HaltungKnopf.Sperre(b) : default;
        Flaeche.SticksGesperrt = () => HandyOffen || (Tafel?.IsVisible ?? false) || _ersatz.IsVisible || !Ebene.IsVisible;
        Flaeche.SticksGeaendert += () =>
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                SticksZeichnen();
            }
            else
            {
                Dispatcher.UIThread.Post(SticksZeichnen);
            }
        };
    }

    /// <summary>Ring und Knopf an die Stellen der Sticks setzen (sofort mit Arrange, Sammelliste 162).</summary>
    private void SticksZeichnen()
    {
        var zustand = Flaeche.StickZustand;
        foreach (var (seite, (ring, knopf)) in _stickBilder)
        {
            var stick = seite == Sticks.Seite.Links ? zustand.Links : zustand.Rechts;
            if (stick is null || Flaeche.TranslatePoint(default, this) is not { } null0)
            {
                ring.IsVisible = false;
                knopf.IsVisible = false;
                continue;
            }
            var r = stick.Groesse;
            ring.Width = ring.Height = 2 * r;
            Canvas.SetLeft(ring, null0.X + stick.MitteX - r);
            Canvas.SetTop(ring, null0.Y + stick.MitteY - r);
            var k = r * 0.45;
            var (kx, ky) = stick.Knopf;
            knopf.Width = knopf.Height = 2 * k;
            Canvas.SetLeft(knopf, null0.X + kx - k);
            Canvas.SetTop(knopf, null0.Y + ky - k);
            ring.IsVisible = true;
            knopf.IsVisible = true;
            ring.Arrange(new Rect(Canvas.GetLeft(ring), Canvas.GetTop(ring), ring.Width, ring.Height));
            knopf.Arrange(new Rect(Canvas.GetLeft(knopf), Canvas.GetTop(knopf), knopf.Width, knopf.Height));
        }
    }
}
