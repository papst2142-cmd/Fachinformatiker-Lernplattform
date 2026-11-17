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
                _stadtVorschau = null;
                if (_plaetzeRaster is not null)
                {
                    _plaetzeRaster = null;
                    if (_eingepasst)
                    {
                        Einpassen();
                    }
                }
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

    private (int B, int T)? _plaetzeRaster;
    private ((int X1, int Y1, int X2, int Y2) Bereich, bool Geht)? _stadtVorschau;

    /// <summary>
    /// Welle S 0.56.2 (Fund Nico 373): Groesse des Rasters der Plaetze, wie sie nach „Übernehmen“ waere (null = heutige Karte).
    /// Gezeichnet wird das Raster dieser Groesse, getippt wird auf ihm; die neue Flaeche ist hell gestrichelt.
    /// </summary>
    public (int B, int T)? PlaetzeRaster
    {
        get => _plaetzeRaster;
        set
        {
            if (_plaetzeRaster == value)
            {
                return;
            }
            _plaetzeRaster = value;
            if (_eingepasst)
            {
                Einpassen();
            }
            InvalidateVisual();
        }
    }

    /// <summary>Welle S 0.56.2 (Fund Nico 373): Vorschau, wo die heutige Stadt landet (blau = geht, rot = geht nicht), null = keine.</summary>
    public void StadtVorschauZeigen((int X1, int Y1, int X2, int Y2)? bereich, bool geht)
    {
        _stadtVorschau = bereich is { } b ? (b, geht) : null;
        InvalidateVisual();
    }

    /// <summary>Start-Test: die gezeichnete Vorschau der Stadt (Bereich in Feldern und Farbe), null = keine.</summary>
    public ((int X1, int Y1, int X2, int Y2) Bereich, string Farbe)? StadtVorschauGezeichnet { get; private set; }

    /// <summary>Groesse, auf der getippt und gezeichnet wird (Raster der Vorschau oder heutige Karte).</summary>
    private (double B, double H) RasterGroesse() => _plaetzeAn && _plaetzeRaster is { } r ? (r.B, r.T) : Welt();

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
        var (breite, tiefe) = RasterGroesse();
        PlatzGetippt?.Invoke(Plaetze.Bei(w.X, w.Y, breite, tiefe));
        return true;
    }

    /// <summary>Start-Test: Tippen wie ein Finger an einer Stelle (Bildpunkte).</summary>
    internal void PlatzTippenFuerTests(Point p) => PlaetzeAnwenden(new Fingergeste.Schritt(Fingergeste.Art.Tippen, p.X, p.Y, 0, 0, 1));

    private void PlaetzeZeichnen(DrawingContext ctx)
    {
        PlaetzeGezeichnet = default;
        StadtVorschauGezeichnet = null;
        if (!_plaetzeAn || Stand is null)
        {
            return;
        }
        var (breite, tiefe) = RasterGroesse();
        var (heuteB, heuteT) = Welt();
        if (breite > heuteB + 1e-9 || tiefe > heuteT + 1e-9)
        {
            // die neue Flaeche nach „Übernehmen“: hell, damit man sieht, wie gross die Karte wird
            var neu = new Pen(Pinsel("#33691E", 0.6), 1.5) { DashStyle = new DashStyle([4, 4], 0) };
            if (breite > heuteB + 1e-9)
            {
                ctx.FillRectangle(Pinsel("#B9D29A", 0.45), new Rect(P(heuteB, 0), P(breite, tiefe)));
            }
            if (tiefe > heuteT + 1e-9)
            {
                ctx.FillRectangle(Pinsel("#B9D29A", 0.45), new Rect(P(0, heuteT), P(Math.Min(breite, heuteB), tiefe)));
            }
            ctx.DrawRectangle(null, neu, new Rect(P(0, 0), P(breite, tiefe)));
        }
        if (_stadtVorschau is { } sv)
        {
            // Welle S 0.56.2 (Fund Nico 373): hier landet die heutige Stadt
            var r = new Rect(P(sv.Bereich.X1, sv.Bereich.Y1), P(sv.Bereich.X2, sv.Bereich.Y2));
            var farbe = sv.Geht ? "#1565C0" : "#C62828";
            ctx.FillRectangle(Pinsel(farbe, 0.18), r);
            ctx.DrawRectangle(null, new Pen(Pinsel(farbe), 5), r);
            var marke = new FormattedText(Texte.StadtVorschauName, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold), 16, Pinsel("#FFFFFF"));
            var mk = new Rect(r.X + 8, r.Y + 8, marke.Width + 12, marke.Height + 6);
            ctx.FillRectangle(Pinsel(farbe, 0.95), mk, 5);
            ctx.DrawText(marke, new Point(mk.X + 6, mk.Y + 3));
            StadtVorschauGezeichnet = (sv.Bereich, sv.Geht ? "blau" : "rot");
        }
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
