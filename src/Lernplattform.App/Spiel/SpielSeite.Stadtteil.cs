using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 0.83.0 (Weisung P12-282 Punkt 3): kurzes Schild mit dem Namen des Stadtteils, sobald die Figur draussen einen
/// anderen benannten Stadtteil betritt (<see cref="StadtteilSchild"/>, Core). Oben in der Mitte des freien Bereichs, steht
/// <see cref="StadtteilSchild.Sekunden"/> Sekunden und blendet dann aus; der Screenreader sagt den Namen an. Nur
/// Darstellung; ohne Namen (Stadt 1, Fassung 0) und ohne 3D erscheint es nie.
/// </summary>
public sealed partial class SpielSeite
{
    private Border? _stadtteilSchild;
    private TextBlock? _stadtteilName;
    private StadtteilSchild? _stadtteilLogik;
    private StadtSpiel? _stadtteilSpiel;
    private DispatcherTimer? _stadtteilUhr;

    /// <summary>Das Schild (Start-Test, Fotos); sichtbar nur waehrend es steht.</summary>
    public Border? StadtteilSchildFeld => _stadtteilSchild;

    /// <summary>Name auf dem Schild beim letzten Zeigen (Start-Test), null = noch nie gezeigt.</summary>
    public string? StadtteilSchildText { get; private set; }

    /// <summary>Wie oft das Schild gezeigt wurde (Start-Test, Gegenprobe ohne Namen = 0).</summary>
    public int StadtteilSchildMal { get; private set; }

    private void StadtteilSchildBauen()
    {
        _stadtteilName = Schrift.Text("", "Gross", FontWeight.Bold);
        _stadtteilName.HorizontalAlignment = HorizontalAlignment.Center;
        _stadtteilName.TextAlignment = TextAlignment.Center;
        _stadtteilName.TextWrapping = TextWrapping.Wrap;
        var kopf = Schrift.Text(KarteTexte.SchildKopf, "Klein", FontWeight.SemiBold, "Pinsel.muted");
        kopf.HorizontalAlignment = HorizontalAlignment.Center;
        var schild = Schrift.Karte(new StackPanel { Spacing = 2, Children = { kopf, _stadtteilName } });
        schild.Padding = new Thickness(22, 10);
        schild.HorizontalAlignment = HorizontalAlignment.Center;
        schild.VerticalAlignment = VerticalAlignment.Top;
        schild.MaxWidth = 420;
        schild.IsHitTestVisible = false;
        schild.IsVisible = false;
        schild.Opacity = 0;
        schild.Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(350) }];
        _stadtteilSchild = schild;
        Children.Add(schild);
    }

    /// <summary>Je Bild: hat die Figur einen neuen Stadtteil betreten, das Schild zeigen.</summary>
    private void StadtteilSchildTakt()
    {
        if (_stadtteilSchild is null || Spiel is not { } spiel)
        {
            return;
        }
        if (!ReferenceEquals(spiel, _stadtteilSpiel))
        {
            // neues Spiel (oder neu geladen): von vorn, das erste Bild zeigt den Stadtteil, in dem die Figur steht
            _stadtteilSpiel = spiel;
            _stadtteilLogik = new StadtteilSchild();
        }
        if (!Flaeche.GlBereit || _ersatz.IsVisible || _stadtteilLogik!.Takt(spiel) is not { } name)
        {
            return;
        }
        StadtteilSchildZeigen(name);
    }

    private void StadtteilSchildZeigen(string name)
    {
        if (_stadtteilSchild is not { } schild || _stadtteilName is null)
        {
            return;
        }
        _stadtteilName.Text = name;
        StadtteilSchildText = name;
        StadtteilSchildMal++;
        var oben = Math.Max(0, Flaeche.Frei.Oben) + 12;
        schild.Margin = new Thickness(Rand, oben, Rand, 0);
        AutomationProperties.SetName(schild, string.Format(CultureInfo.CurrentCulture, KarteTexte.SchildVorlesen, name));
        schild.IsVisible = true;
        schild.Opacity = 1;
        Views.Ansage.Sagen(string.Format(CultureInfo.CurrentCulture, KarteTexte.SchildVorlesen, name));
        _stadtteilUhr?.Stop();
        _stadtteilUhr = new DispatcherTimer(TimeSpan.FromSeconds(StadtteilSchild.Sekunden), DispatcherPriority.Background, (_, _) =>
        {
            _stadtteilUhr?.Stop();
            schild.Opacity = 0;
            DispatcherTimer.RunOnce(() => schild.IsVisible = schild.Opacity > 0, TimeSpan.FromMilliseconds(400));
        });
        _stadtteilUhr.Start();
    }
}
