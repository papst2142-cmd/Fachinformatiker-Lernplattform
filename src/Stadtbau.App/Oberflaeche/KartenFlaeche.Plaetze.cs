using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Stadtbau.Core;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.56.0 (Sammelliste 362, Weisung P12-287): „Plätze“ in der Draufsicht. An: das Raster der Plaetze (64 × 64) mit
/// ihren Nummern auf der Karte; Tippen (Finger oder Maus) waehlt einen Platz (<see cref="PlatzGetippt"/>). Der gewaehlte
/// Platz hat einen Rahmen: gruen, wenn sich der gewaehlte Stadtteil dort einsetzen laesst, rot, wenn nicht (gleiche Rechnung
/// wie das Einsetzen, <see cref="Projekt.EinsetzenPruefen"/>). Zwei Finger, rechte Maus und Leertaste bleiben bei der Ansicht.
/// Tastatur und Screenreader: Auswahl rechts (Hauptansicht).
/// </summary>
public sealed partial class KartenFlaeche
{
    private bool _plaetzeAn;
    private int? _platzGewaehlt;
    private bool? _platzFrei;

    /// <summary>Plaetze zeigen und waehlen.</summary>
    public bool PlaetzeAn
    {
        get => _plaetzeAn;
        set
        {
            if (_plaetzeAn == value)
            {
                return;
            }
            _plaetzeAn = value;
            if (value)
            {
                GelaendePinsel = null;
                SetzenBeenden();
                MassbandAn = false;
                MehrereWaehlen = false;
                NamenAn = false;
                Waehlen(null);
            }
            else
            {
                _platzGewaehlt = null;
                _platzFrei = null;
            }
            InvalidateVisual();
            PlaetzeGeaendert?.Invoke();
        }
    }

    /// <summary>Plaetze an oder aus (auch von selbst, wenn ein anderes Werkzeug beginnt).</summary>
    public event Action? PlaetzeGeaendert;

    /// <summary>Auf einen Platz getippt (Nummer ab 1; 0 = ausserhalb der Plaetze).</summary>
    public event Action<int>? PlatzGetippt;

    /// <summary>Gewaehlter Platz und ob der Stadtteil dort hineinpasst (null = kein Stadtteil gewaehlt, nur Rahmen).</summary>
    public void PlatzZeigen(int? platz, bool? frei)
    {
        _platzGewaehlt = platz;
        _platzFrei = frei;
        InvalidateVisual();
    }

    /// <summary>Gewaehlter Platz (Start-Test).</summary>
    public int? PlatzGewaehlt => _platzGewaehlt;

    /// <summary>Start-Test: gezeichnete Plaetze und die Farbe des Rahmens (gruen, rot, keine).</summary>
    public (int Plaetze, string? Rahmen) PlaetzeGezeichnet { get; private set; }

    /// <summary>Tippen bei „Plätze“ (true = verarbeitet).</summary>
    private bool PlaetzeAnwenden(Fingergeste.Schritt s)
    {
        if (s.Art != Fingergeste.Art.Tippen || _nurSchieben)
        {
            // Ziehen schiebt die Ansicht wie sonst
            return false;
        }
        var w = WeltPunkt(new Point(s.X, s.Y));
        var (breite, tiefe) = Welt();
        PlatzGetippt?.Invoke(Plaetze.Bei(w.X, w.Y, breite, tiefe));
        return true;
    }

    /// <summary>Start-Test: Tippen wie ein Finger an einer Stelle (Bildpunkte).</summary>
    internal void PlatzTippenFuerTests(Point p) => PlaetzeAnwenden(new Fingergeste.Schritt(Fingergeste.Art.Tippen, p.X, p.Y, 0, 0, 1));

    private void PlaetzeZeichnen(DrawingContext ctx)
    {
        PlaetzeGezeichnet = default;
        if (!_plaetzeAn || Stand is null)
        {
            return;
        }
        var (breite, tiefe) = Welt();
        var anzahl = Plaetze.Anzahl(breite, tiefe);
        var linie = new Pen(Pinsel("#0D47A1", 0.85), 2) { DashStyle = new DashStyle([8, 4], 0) };
        var schrift = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
        var dunkel = Pinsel("#0D47A1");
        for (var p = 1; p <= anzahl; p++)
        {
            var (x, y) = Plaetze.Lage(p, breite);
            var r = new Rect(P(x, y), P(x + Plaetze.Seite, y + Plaetze.Seite));
            ctx.DrawRectangle(null, linie, r);
            var text = new FormattedText(Texte.PlatzNummer(p), CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, schrift, 15, dunkel);
            var kasten = new Rect(r.Center.X - (text.Width / 2) - 6, r.Center.Y - (text.Height / 2) - 3, text.Width + 12, text.Height + 6);
            ctx.FillRectangle(Pinsel("#FFFFFF", 0.9), kasten, 5);
            ctx.DrawText(text, new Point(kasten.X + 6, kasten.Y + 3));
        }
        string? rahmen = null;
        if (_platzGewaehlt is { } g && Plaetze.Gibt(g, breite, tiefe))
        {
            var (x, y) = Plaetze.Lage(g, breite);
            var r = new Rect(P(x, y), P(x + Plaetze.Seite, y + Plaetze.Seite));
            var farbe = _platzFrei switch { true => "#2E7D32", false => "#C62828", null => "#0D47A1" };
            ctx.FillRectangle(Pinsel(farbe, 0.14), r);
            ctx.DrawRectangle(null, new Pen(Pinsel(farbe), 4), r);
            rahmen = _platzFrei switch { true => "gruen", false => "rot", null => "blau" };
        }
        PlaetzeGezeichnet = (anzahl, rahmen);
    }
}
