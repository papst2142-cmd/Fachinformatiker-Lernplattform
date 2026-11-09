using System.Numerics;
using Avalonia;
using Avalonia.Media;
using Lernplattform.Core.Spiel.Probe;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.28.0 (Weisung P12-121, Nico 2a 3a, Sammelliste 260): Tuer selbst setzen in der Draufsicht (die 3D-Ansicht
/// nutzt dieselben Schritte, <see cref="DreiBedienung"/>). Knopf „Tür setzen“ am gewaehlten Haus, dann eine Wand antippen
/// oder die Tuer an der Wand entlang ziehen (auch um die Ecke); jedes Loslassen ist ein Schritt. Ohne den Knopf greift ein
/// Druck auf die Tuer des gewaehlten Hauses sie direkt zum Ziehen.
/// </summary>
public sealed partial class KartenFlaeche
{
    /// <summary>Haus, dessen Tuer gerade gesetzt wird (null = aus).</summary>
    public AussenStelle? TuerFuer { get; private set; }

    /// <summary>Tuer-Setzen beginnt oder endet (Knopf markieren, 3D neu zeichnen).</summary>
    public event Action? TuerModusGeaendert;

    /// <summary>Vorschau der Tuer waehrend des Ziehens (Mitte auf der Wand, Richtung hinaus).</summary>
    public (Vector2 Punkt, Vector2 Aussen)? TuerVorschau { get; private set; }

    /// <summary>Tueren des CC0-Modells am Haus (zum Einrasten), im Hintergrund aus der Szene gerechnet; null = noch nicht da.</summary>
    public IReadOnlyList<Vector2>? TuerModellTueren { get; private set; }

    /// <summary>Nur fuer diesen einen Zug (Tuer direkt gegriffen): danach endet das Tuer-Setzen wieder.</summary>
    private bool _tuerNurZiehen;

    /// <summary>So nah (Bildpunkte) muss ein Druck an der Tuer des gewaehlten Hauses sein, um sie direkt zu greifen.</summary>
    public const double TuerGreifen = 16;

    /// <summary>Tuer-Setzen fuer das Haus <paramref name="s"/> beginnen (beendet Setzen, Ziehen und Massband).</summary>
    public void TuerSetzenStarten(AussenStelle s, bool still = false)
    {
        if (_projekt is not { NurLesen: false } p || TuerSetzen.Nicht(p.AussenStand, s) is not null)
        {
            return;
        }
        SetzenBeenden();
        MassbandAn = false;
        if (Auswahl != s)
        {
            Waehlen(s);
        }
        TuerFuer = s;
        TuerVorschau = null;
        TuerModellTueren = null;
        ModellTuerenRechnen(p, s);
        TuerModusGeaendert?.Invoke();
        if (!still)
        {
            Gemeldet?.Invoke(Texte.TuerSetzenHinweis(TuerName(p, s)));
        }
        InvalidateVisual();
    }

    /// <summary>Tuer-Setzen beenden (kein Schritt).</summary>
    public void TuerSetzenBeenden()
    {
        if (TuerFuer is null)
        {
            return;
        }
        TuerFuer = null;
        TuerVorschau = null;
        _tuerNurZiehen = false;
        TuerModusGeaendert?.Invoke();
        InvalidateVisual();
    }

    /// <summary>3D-Ansicht: ihre Szene ist schon gebaut, daraus die Tueren des Modells nehmen (schneller als der eigene Bau).</summary>
    public void TuerModellTuerenAus(StadtSzene szene)
    {
        if (TuerFuer is { } s && _projekt is { } p && TuerModellTueren is null)
        {
            TuerModellTueren = TuerSetzen.ModellTuerMitten(szene, p.Daten.Grafik, AussenBearbeitung.HausFlaeche(p.AussenStand, s));
        }
    }

    private void ModellTuerenRechnen(Projekt p, AussenStelle s)
    {
        var stand = (Lernplattform.Core.Daten.PyObjekt)Lernplattform.Core.Spiel.Py.Kopie(p.AussenStand)!;
        var flaeche = AussenBearbeitung.HausFlaeche(stand, s);
        _ = Task.Run(() =>
        {
            var szene = p.SzeneOhneLaternen(stand, mitGebaeuden: true);
            var tueren = szene is null ? [] : TuerSetzen.ModellTuerMitten(szene, p.Daten.Grafik, flaeche);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (TuerFuer == s && TuerModellTueren is null)
                {
                    TuerModellTueren = tueren;
                }
            });
        });
    }

    private string TuerName(Projekt p, AussenStelle s) =>
        s.Art == AussenArt.Haus ? OrtName(s.Ort) : ModellNamen.Anzeige(p.AussenStand, Lernplattform.Core.Spiel.Py.Str(Lernplattform.Core.Spiel.SpielDaten.Objekt(Aussen.Landschaft(p.AussenStand)[s.Nummer])["modell"]));

    /// <summary>Liegt die Weltstelle nah genug am Haus, dass ein Tipp seine Wand meint? (bis 1,5 oder 30 Bildpunkte)</summary>
    public bool AmHaus(double x, double y, double? weite = null)
    {
        if (TuerFuer is not { } s || _projekt is not { } p)
        {
            return false;
        }
        var (hx, hy, hw, hh) = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        var dx = Math.Max(Math.Max(hx - x, 0), x - (hx + hw));
        var dy = Math.Max(Math.Max(hy - y, 0), y - (hy + hh));
        return Math.Sqrt((dx * dx) + (dy * dy)) <= (weite ?? Math.Max(1.5, 30 / _zoom));
    }

    /// <summary>Vorschau der Tuer an die Weltstelle (x, y): gleiche Rechnung wie der Schritt.</summary>
    public void TuerVorschauAn(double x, double y)
    {
        if (TuerFuer is { } s && _projekt is { } p)
        {
            TuerVorschau = TuerSetzen.AufWand(AussenBearbeitung.HausFlaeche(p.AussenStand, s), new Vector2((float)x, (float)y), Raster ? TuerModellTueren : null);
            TuerModusGeaendert?.Invoke();
            InvalidateVisual();
        }
    }

    /// <summary>Vorschau weg (zweiter Finger, abgebrochen).</summary>
    public void TuerVorschauWeg()
    {
        TuerVorschau = null;
        if (_tuerNurZiehen)
        {
            TuerSetzenBeenden();
        }
        TuerModusGeaendert?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Die Tuer an die Wand bei (x, y) setzen (ein Schritt). Rueckgabe: Meldung (auch fuer den Start-Test).</summary>
    public string? TuerSetzenAn(double x, double y, double? weite = null)
    {
        if (TuerFuer is not { } s || _projekt is not { NurLesen: false } p)
        {
            return null;
        }
        TuerVorschau = null;
        var name = TuerName(p, s);
        string meldung;
        if (!AmHaus(x, y, weite))
        {
            meldung = Texte.TuerSetzenDaneben(name);
        }
        else
        {
            string? hinweis = null;
            var tueren = Raster ? TuerModellTueren : null;
            p.AussenAendern(Texte.TuerSetzenSchritt, a => hinweis = TuerSetzen.Setzen(a, s, new Vector2((float)x, (float)y), tueren));
            meldung = hinweis ?? Texte.TuerGesetzt(name);
            Waehlen(AussenBearbeitung.Gueltig(p.AussenStand, s) ? s : null);
        }
        if (_tuerNurZiehen)
        {
            TuerSetzenBeenden();
        }
        else
        {
            TuerModusGeaendert?.Invoke();
        }
        Gemeldet?.Invoke(meldung);
        InvalidateVisual();
        return meldung;
    }

    /// <summary>Druck nah an der Tuer des gewaehlten Hauses? Dann wird sie direkt gezogen (nur dieser Zug).</summary>
    private bool TuerDirektGreifen(Point bild)
    {
        if (TuerFuer is not null || Auswahl is not { } s || _projekt is not { NurLesen: false } p || s.Art is not (AussenArt.Haus or AussenArt.Gebaeude)
            || TuerSetzen.Nicht(p.AussenStand, s) is not null || TuerSetzen.Tuer(p.AussenStand, s) is not { } t)
        {
            return false;
        }
        var b = P(t.X, t.Y);
        if (Math.Abs(b.X - bild.X) > TuerGreifen || Math.Abs(b.Y - bild.Y) > TuerGreifen)
        {
            return false;
        }
        TuerSetzenStarten(s, still: true);
        _tuerNurZiehen = true;
        return true;
    }

    /// <summary>Fingerschritt beim Tuer-Setzen (true = verarbeitet; Ansicht schieben und zoomen gehen weiter).</summary>
    private bool TuerAnwenden(Fingergeste.Schritt s)
    {
        var w = WeltPunkt(new Point(s.X, s.Y));
        switch (s.Art)
        {
            case Fingergeste.Art.Tippen when _nurSchieben:
                if (_rechtsDruck)
                {
                    // Rechtsklick ohne Bewegung beendet das Tuer-Setzen wie Escape
                    TuerSetzenBeenden();
                }
                return true;
            case Fingergeste.Art.Tippen:
            case Fingergeste.Art.StueckFertig:
                TuerSetzenAn(w.X, w.Y);
                return true;
            case Fingergeste.Art.StueckZiehen:
                TuerVorschauAn(w.X, w.Y);
                return true;
            case Fingergeste.Art.StueckAbbrechen:
                TuerVorschauWeg();
                return true;
            default:
                return false;
        }
    }

    private static readonly IBrush TuerFarbe = new SolidColorBrush(Color.Parse("#E08A00"));

    /// <summary>Die Tuer des Hauses beim Tuer-Setzen: Waende hervorgehoben, Vorschau als Rechteck mit Pfeil hinaus.</summary>
    private void TuerZeichnen(DrawingContext ctx)
    {
        if (TuerFuer is not { } s || _projekt is not { } p || !AussenBearbeitung.Gueltig(p.AussenStand, s))
        {
            return;
        }
        var (x, y, w, h) = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        ctx.DrawRectangle(null, new Pen(TuerFarbe, 3, new DashStyle([2, 2], 0)), new Rect(P(x, y), P(x + w, y + h)));
        if (TuerVorschau is { } v)
        {
            TuerMarke(ctx, v.Punkt, v.Aussen);
        }
        else if (TuerSetzen.Tuer(p.AussenStand, s) is { } t)
        {
            TuerMarke(ctx, t, AussenBearbeitung.HausTuerSeite(p.AussenStand, s));
        }
    }

    private void TuerMarke(DrawingContext ctx, Vector2 t, Vector2 n)
    {
        var quer = new Vector2(-n.Y, n.X) * (StadtSzene.TuerBreite / 2);
        var a = P(t.X - quer.X, t.Y - quer.Y);
        var b = P(t.X + quer.X, t.Y + quer.Y);
        ctx.DrawLine(new Pen(Brushes.White, 9, lineCap: PenLineCap.Round), a, b);
        ctx.DrawLine(new Pen(TuerFarbe, 6, lineCap: PenLineCap.Round), a, b);
        var m = P(t.X, t.Y);
        ctx.DrawLine(new Pen(TuerFarbe, 3, lineCap: PenLineCap.Round), m, new Point(m.X + (n.X * 20), m.Y + (n.Y * 20)));
    }
}
