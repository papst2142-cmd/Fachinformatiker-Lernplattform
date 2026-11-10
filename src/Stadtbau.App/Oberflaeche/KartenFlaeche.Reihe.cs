using Avalonia;
using Avalonia.Media;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle W-B S 0.30.0 (Weisung P12-136, Kurzplan P12-79 Wunsch 2): ein Baum oder Katalog-Stueck als Reihe setzen. Bedienung
/// wie „Fußweg ziehen“: Tipp Anfang, Tipp Ende (oder ein Finger zieht in einem Zug); die Vorschau zeigt jedes Stueck gruen
/// oder rot (rot = Grund, wird ausgelassen) und „+n Dreiecke“. Setzen ist EIN Schritt im Verlauf
/// (<see cref="AussenBearbeitung.ReiheSetzen"/>, gleiche Rechnung wie die Vorschau <see cref="AussenBearbeitung.ReihePlanen"/>).
/// </summary>
public sealed partial class KartenFlaeche
{
    private bool _reiheAn;
    private ReiheWahl _reiheWahl = new(1.0);

    /// <summary>Schalter „Als Reihe setzen“ (gilt fuer Baum und Katalog-Stueck, bleibt bis zum Ausschalten).</summary>
    public bool ReiheAn
    {
        get => _reiheAn;
        set
        {
            if (_reiheAn == value)
            {
                return;
            }
            _reiheAn = value;
            ReiheZuruecksetzen();
            ReiheGeaendert?.Invoke();
        }
    }

    /// <summary>Wird gerade eine Reihe gesetzt (Schalter an und ein Baum oder Katalog-Stueck gewaehlt)?</summary>
    public bool ReiheAktiv => _reiheAn && AussenBearbeitung.ReiheMoeglich(SetzenArt);

    /// <summary>Abstand, beide Seiten, zufaellig drehen.</summary>
    public ReiheWahl ReiheWahl
    {
        get => _reiheWahl;
        set
        {
            _reiheWahl = value with { Abstand = Math.Clamp(Bearbeitung.Runden(value.Abstand), AussenBearbeitung.ReiheAbstandMin, AussenBearbeitung.ReiheAbstandMax) };
            if (ReiheZiel is { } z)
            {
                ReiheZielSetzen(z.X, z.Y);
            }
            ReiheGeaendert?.Invoke();
        }
    }

    /// <summary>Anfang der Reihe (Weltpunkt) nach dem ersten Tipp, sonst null.</summary>
    public Point? ReiheAnfang { get; private set; }

    /// <summary>Wo das Ende gerade laege (Zeiger, Finger).</summary>
    public Point? ReiheZiel { get; private set; }

    /// <summary>Plan der Vorschau (gleich dem Schritt), null = keine.</summary>
    public ReihenPlan? ReiheVorschau { get; private set; }

    /// <summary>Anfang, Vorschau oder Wahl geaendert (Infozeile rechts, 3D-Schatten).</summary>
    public event Action? ReiheGeaendert;

    private bool _reiheAnfangInDieserGeste;

    private void ReiheZuruecksetzen()
    {
        if (ReiheAnfang is null && ReiheZiel is null && ReiheVorschau is null)
        {
            return;
        }
        ReiheAnfang = null;
        ReiheZiel = null;
        ReiheVorschau = null;
        ReiheGeaendert?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Den Anfang setzen (erster Tipp, auch aus der 3D-Ansicht).</summary>
    public void ReiheAnfangSetzen(double x, double y)
    {
        if (!ReiheAktiv || _projekt is null || _projekt.NurLesen)
        {
            return;
        }
        ReiheAnfang = new Point(x, y);
        ReiheZiel = null;
        ReiheVorschau = null;
        Gemeldet?.Invoke(Texte.ReiheEnde);
        ReiheGeaendert?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Vorschau bis (x, y): Plan wie der Schritt.</summary>
    public void ReiheZielSetzen(double x, double y)
    {
        ReiheZiel = new Point(x, y);
        ReiheVorschau = ReiheAnfang is { } a && Stand is { } st && SetzenArt is { } art
            ? AussenBearbeitung.ReihePlanen(st, art, SetzenModell, a.X, a.Y, x, y, _reiheWahl, SetzenDreh, Raster, _projekt?.Daten.Grafik)
            : null;
        ReiheGeaendert?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Abbrechen (Escape, Rechtsklick): true = es gab einen Anfang, er ist verworfen; die Reihe bleibt gewaehlt.</summary>
    public bool ReiheAbbrechen()
    {
        if (ReiheAnfang is null)
        {
            return false;
        }
        ReiheZuruecksetzen();
        Gemeldet?.Invoke(Texte.ReiheAnfang);
        return true;
    }

    /// <summary>Vom Anfang bis (x, y) die Reihe setzen: ein Schritt im Verlauf; danach sind die neuen Stuecke gewaehlt.</summary>
    public IReadOnlyList<AussenStelle> ReiheBis(double x, double y)
    {
        if (_projekt is null || _projekt.NurLesen || !ReiheAktiv || ReiheAnfang is not { } a || SetzenArt is not { } art)
        {
            return [];
        }
        var (modell, wahl, dreh, raster) = (SetzenModell, _reiheWahl, SetzenDreh, Raster);
        var plan = AussenBearbeitung.ReihePlanen(_projekt.AussenStand, art, modell, a.X, a.Y, x, y, wahl, dreh, raster);
        ReiheAnfang = null;
        ReiheZiel = null;
        ReiheVorschau = null;
        if (plan.Frei == 0)
        {
            Gemeldet?.Invoke(Texte.ReiheNichtsFrei);
            ReiheGeaendert?.Invoke();
            InvalidateVisual();
            return [];
        }
        var (neu, ausgelassen) = _projekt.AussenAendern(Texte.ReiheSchritt, st => AussenBearbeitung.ReiheSetzen(st, art, modell, a.X, a.Y, x, y, wahl, dreh, raster));
        Gemeldet?.Invoke(Texte.ReiheGesetzt(neu.Count, ausgelassen));
        ReiheGeaendert?.Invoke();
        // die neuen Stuecke sind gewaehlt (gleich zusammen verschieben); die Reihe bleibt fuer die naechste Linie gewaehlt
        GruppeSetzen(neu);
        InvalidateVisual();
        return neu;
    }

    /// <summary>Tippen, Ziehen und Loslassen beim Setzen einer Reihe (true = verarbeitet).</summary>
    private bool ReiheAnwenden(Fingergeste.Schritt s)
    {
        var w = WeltPunkt(new Point(s.X, s.Y));
        switch (s.Art)
        {
            case Fingergeste.Art.Tippen when _nurSchieben:
                if (_rechtsDruck && !ReiheAbbrechen())
                {
                    SetzenBeenden();
                }
                return true;
            case Fingergeste.Art.Tippen:
                if (ReiheAnfang is null)
                {
                    ReiheAnfangSetzen(w.X, w.Y);
                }
                else
                {
                    ReiheBis(w.X, w.Y);
                }
                return true;
            case Fingergeste.Art.StueckZiehen:
                if (ReiheAnfang is null)
                {
                    var start = WeltPunkt(new Point(s.X - s.Dx, s.Y - s.Dy));
                    ReiheAnfangSetzen(start.X, start.Y);
                    _reiheAnfangInDieserGeste = true;
                }
                ReiheZielSetzen(w.X, w.Y);
                return true;
            case Fingergeste.Art.StueckFertig:
                ReiheBis(w.X, w.Y);
                return true;
            case Fingergeste.Art.StueckAbbrechen:
                if (_reiheAnfangInDieserGeste)
                {
                    ReiheZuruecksetzen();
                }
                else
                {
                    ReiheZiel = null;
                    ReiheVorschau = null;
                    InvalidateVisual();
                }
                return true;
            default:
                return false;
        }
    }

    /// <summary>Vorschau der Reihe: Linie, jedes Stueck gruen (wird gesetzt) oder rot (wird ausgelassen).</summary>
    private void ReiheZeichnen(DrawingContext ctx)
    {
        if (!ReiheAktiv)
        {
            return;
        }
        var akzent = Farbe("accent");
        if (ReiheAnfang is { } anfang)
        {
            var pa = P(anfang.X, anfang.Y);
            if (ReiheZiel is { } ziel)
            {
                ctx.DrawLine(new Pen(Pinsel("#111827"), 1.5, new DashStyle([4, 3], 0)), pa, P(ziel.X, ziel.Y));
            }
            ctx.DrawEllipse(Brushes.White, new Pen(akzent, 2), pa, GriffRadius, GriffRadius);
        }
        if (ReiheVorschau is not { } plan)
        {
            return;
        }
        var rand = new Pen(Pinsel("#111827"), 1);
        foreach (var st in plan.Stuecke)
        {
            var fuellung = Pinsel(st.Frei ? "#34D399" : "#F87171", 0.7);
            if (SetzenArt == AussenArt.Baum)
            {
                var r = Math.Max(4, AussenBearbeitung.ReiheBaumRadius * _zoom);
                ctx.DrawEllipse(fuellung, rand, P(st.X, st.Y), r, r);
            }
            else
            {
                var r = new Rect(P(st.X - (st.W / 2), st.Y - (st.T / 2)), P(st.X + (st.W / 2), st.Y + (st.T / 2)));
                if (r.Width < 6 || r.Height < 6)
                {
                    r = r.Inflate(new Thickness(Math.Max(0, (6 - r.Width) / 2), Math.Max(0, (6 - r.Height) / 2)));
                }
                ctx.DrawRectangle(fuellung, rand, r);
            }
        }
    }
}
