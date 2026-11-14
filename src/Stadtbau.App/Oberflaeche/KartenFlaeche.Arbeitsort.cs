using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>Welle E4: Arbeitsort der gewaehlten Quest in der Draufsicht (Weltpunkte; <paramref name="Von"/> = Platz des Questgebers).</summary>
internal sealed record ArbeitsortMarke(string Quest, Point Ziel, Point? Von, string Name);

/// <summary>
/// Welle E4 „Aufgaben an Gebäuden“ (S 0.53.0, Weisung P12-247, Antwort 4b): in der Draufsicht zeigt eine gestrichelte Linie
/// vom Questgeber zum Arbeitsort der gewaehlten Quest, am Arbeitsort steht die Marke „A“. Die Marke laesst sich mit einem
/// Finger oder der Maus auf ein anderes Haus ziehen: Loslassen setzt den Arbeitsort dort (ein Schritt, die Hauptansicht
/// prueft das Haus und meldet ein ungueltiges sichtbar), Escape bricht ab. Die Zeile „Arbeitsort: …“ waehrend des Ziehens
/// laeuft ueber <see cref="Anzeigen"/>.
/// </summary>
public sealed partial class KartenFlaeche
{
    /// <summary>Arbeitsort der gewaehlten Quest (setzt die Hauptansicht), null = keiner.</summary>
    internal ArbeitsortMarke? Arbeitsort { get; set; }

    /// <summary>Marke losgelassen ueber dem Haus mit diesem Ort der Karte (null = kein Haus mit Ort): die Hauptansicht setzt oder meldet.</summary>
    internal Action<string, string?>? ArbeitsortGezogen { get; set; }

    private bool _arbeitZiehen;
    private Point _arbeitPunkt;
    private int _arbeitZeiger;

    /// <summary>Zieht der Finger gerade die Marke „A“?</summary>
    internal bool ArbeitsortZiehtGerade => _arbeitZiehen;

    /// <summary>Radius der Marke „A“ im Bild (am Tablet gross genug fuer den Finger).</summary>
    internal double ArbeitsortRadius => Math.Max(12, 0.35 * _zoom);

    /// <summary>Bildpunkt der Marke (null = keine).</summary>
    internal Point? ArbeitsortBildpunkt => Arbeitsort is { } m ? P(m.Ziel.X, m.Ziel.Y) : null;

    /// <summary>Druck auf die Marke: Ziehen beginnt (true = verarbeitet).</summary>
    private bool ArbeitsortDruecken(PointerPressedEventArgs e, Point p)
    {
        if (Arbeitsort is null || _projekt?.NurLesen != false || ArbeitsortBildpunkt is not { } m || _geste.Aktiv)
        {
            return false;
        }
        var d = p - m;
        if (Math.Sqrt((d.X * d.X) + (d.Y * d.Y)) > ArbeitsortRadius + 4)
        {
            return false;
        }
        _arbeitZiehen = true;
        _arbeitPunkt = p;
        _arbeitZeiger = e.Pointer.Id;
        InvalidateVisual();
        return true;
    }

    private bool ArbeitsortBewegen(PointerEventArgs e, Point p)
    {
        if (!_arbeitZiehen)
        {
            return false;
        }
        if (e.Pointer.Id == _arbeitZeiger)
        {
            _arbeitPunkt = p;
            InvalidateVisual();
        }
        return true;
    }

    private bool ArbeitsortLoslassen(PointerReleasedEventArgs e, Point p)
    {
        if (!_arbeitZiehen)
        {
            return false;
        }
        if (e.Pointer.Id == _arbeitZeiger)
        {
            ArbeitsortAblegen(p);
            e.Pointer.Capture(null);
        }
        return true;
    }

    /// <summary>Marke an diesem Bildpunkt ablegen (Loslassen; Start-Test ueber <see cref="ArbeitsortZiehenFuerTests"/>).</summary>
    private void ArbeitsortAblegen(Point p)
    {
        _arbeitZiehen = false;
        InvalidateVisual();
        if (Arbeitsort is not { } m)
        {
            return;
        }
        var w = WeltPunkt(p);
        ArbeitsortGezogen?.Invoke(m.Quest, OrtBei(w.X, w.Y));
    }

    /// <summary>Escape: Ziehen der Marke abbrechen (true = es lief eines, nichts geaendert).</summary>
    internal bool ArbeitsortAbbrechen()
    {
        if (!_arbeitZiehen)
        {
            return false;
        }
        _arbeitZiehen = false;
        InvalidateVisual();
        Gemeldet?.Invoke(Texte.QuestArbeitsortZiehenAbgebrochen);
        return true;
    }

    private bool ArbeitsortVerloren(PointerCaptureLostEventArgs e)
    {
        if (!_arbeitZiehen || e.Pointer.Id != _arbeitZeiger)
        {
            return false;
        }
        _arbeitZiehen = false;
        InvalidateVisual();
        return true;
    }

    /// <summary>Start-Test: die Marke mit dem Finger bis zum Weltpunkt (x, y) ziehen (wie Druecken, Bewegen, Loslassen).</summary>
    internal void ArbeitsortZiehenFuerTests(double x, double y, bool abbrechen = false)
    {
        if (ArbeitsortBildpunkt is not { } m)
        {
            return;
        }
        _arbeitZiehen = true;
        _arbeitPunkt = m;
        _arbeitPunkt = P(x, y);
        if (abbrechen)
        {
            ArbeitsortAbbrechen();
            return;
        }
        ArbeitsortAblegen(P(x, y));
    }

    /// <summary>Ort der Karte des Hauses am Weltpunkt (auch fein gedrehte Haeuser), null = dort steht kein Haus mit Ort.</summary>
    internal string? OrtBei(double x, double y)
    {
        if (Stand is not { } a)
        {
            return null;
        }
        foreach (var (s, _, _) in AussenBearbeitung.Alle(a).Where(t => t.Stelle.Art == AussenArt.Haus).ToList())
        {
            if (Innen(AussenBearbeitung.HausEcken(a, s), x, y))
            {
                return s.Ort;
            }
        }
        return null;
    }

    private static bool Innen(System.Numerics.Vector2[] ecken, double x, double y)
    {
        var drin = false;
        for (int i = 0, j = ecken.Length - 1; i < ecken.Length; j = i++)
        {
            if ((ecken[i].Y > y) != (ecken[j].Y > y)
                && x < ((ecken[j].X - ecken[i].X) * (y - ecken[i].Y) / (ecken[j].Y - ecken[i].Y)) + ecken[i].X)
            {
                drin = !drin;
            }
        }
        return drin;
    }

    /// <summary>Linie, Marke „A“ und beim Ziehen die Zeile „Arbeitsort: …“ (ueber <see cref="Anzeigen"/>).</summary>
    private void ArbeitsortZeichnen(DrawingContext ctx, Typeface schrift)
    {
        if (Arbeitsort is not { } m)
        {
            return;
        }
        var ziel = _arbeitZiehen ? _arbeitPunkt : P(m.Ziel.X, m.Ziel.Y);
        var lila = new SolidColorBrush(Color.Parse(Lila));
        if (m.Von is { } von)
        {
            ctx.DrawLine(new Pen(lila, 2.5, new DashStyle([3, 2], 0)), P(von.X, von.Y), ziel);
        }
        var r = ArbeitsortRadius;
        ctx.DrawEllipse(lila, new Pen(Brushes.White, 2.5), ziel, r, r);
        var ft = new FormattedText("A", CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight,
            new Typeface(schrift.FontFamily, FontStyle.Normal, FontWeight.Bold), Math.Max(11, r * 1.1), Brushes.White);
        ctx.DrawText(ft, ziel - new Vector(ft.Width / 2, ft.Height / 2));
        if (_arbeitZiehen)
        {
            var w = WeltPunkt(_arbeitPunkt);
            var ort = OrtBei(w.X, w.Y);
            var text = ort is null ? Texte.QuestArbeitsortZiehenKeinHaus : string.Format(CultureInfo.InvariantCulture, Texte.QuestArbeitsortZiehenUeber, OrtName(ort));
            var zeile = new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, schrift, 13, Farbe("text"));
            Anzeigen.Zeichnen(ctx, zeile, text, new Point(ziel.X + r + 8, ziel.Y - r - 8), new Size(6, 3), Farbe("card"), new Pen(lila, 1.5), 4,
                VerdecktFuer(this), Bounds.Size, _anzeigen);
        }
    }

    /// <summary>Farbe der Marke und der Linie (lila, unterscheidet sich von Questgeber gruen und Zahl am Haus).</summary>
    public const string Lila = "#7B3FA0";
}
