using Avalonia;
using Avalonia.Media;
using Stadtbau.Core;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle D2 (Weisung P12-157, Kurzplan P12-97 Punkt b, Hinweis P12-156 Punkt 1): die Waermekarte faerbt die Draufsicht je
/// Feld 1 × 1 nach der Zahl der Dreiecke (<see cref="GrenzenLive.Felder"/>, nur Felder auf der Karte). Nur Anzeige.
/// </summary>
public sealed partial class KartenFlaeche
{
    private GrenzenLive? _waerme;
    private bool _waermeAn;

    /// <summary>Die zuletzt gerechneten Grenzen (Felder der Waermekarte).</summary>
    public GrenzenLive? Waerme
    {
        get => _waerme;
        set
        {
            _waerme = value;
            if (_waermeAn)
            {
                InvalidateVisual();
            }
        }
    }

    /// <summary>Waermekarte zeigen (Knopf „Wärmekarte“, Taste W).</summary>
    public bool WaermeAn
    {
        get => _waermeAn;
        set
        {
            _waermeAn = value;
            InvalidateVisual();
        }
    }

    /// <summary>Start-Test: so viele Felder hat das letzte Bild gefaerbt.</summary>
    public int WaermeFelderGezeichnet { get; private set; }

    private void WaermeZeichnen(DrawingContext ctx)
    {
        WaermeFelderGezeichnet = 0;
        if (!_waermeAn || _waerme is not { } w)
        {
            return;
        }
        var (b, h) = Welt();
        var pinsel = GrenzenLive.WaermeFarben.Select(f => (IBrush)new SolidColorBrush(Color.Parse(f), 0.6)).ToArray();
        for (var y = 0; y < Math.Min(w.Tiefe, (int)Math.Ceiling(h)); y++)
        {
            for (var x = 0; x < Math.Min(w.Breite, (int)Math.Ceiling(b)); x++)
            {
                var stufe = GrenzenLive.Stufe(w.Feld(x, y));
                if (stufe < 0)
                {
                    continue;
                }
                ctx.FillRectangle(pinsel[stufe], new Rect(P(x, y), P(Math.Min(x + 1, b), Math.Min(y + 1, h))));
                WaermeFelderGezeichnet++;
            }
        }
    }
}
