using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Spiel.Ton;
using Lernplattform.Core.Texte;
using static Avalonia.OpenGL.GlConsts;

namespace Lernplattform.Spiel;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="StadtFlaeche"/> mit Maus, Finger und Tasten.
/// Reiner Umzug aus StadtFlaeche.cs, nichts geaendert.
/// </summary>
public sealed partial class StadtFlaeche
{
    // ---- Bedienung ----

    /// <summary>Neue Groesse (Fenster, Drehen, Ortsliste): sofort neu zeichnen, sonst bleibt das alte Bild stehen.</summary>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Anfordern();
    }

    /// <summary>Phase 8 W8 (Sammelliste 132/135): Maus ueber den Kameragriff (rechte Taste dreht, nie Gehen).</summary>
    private readonly Kameragriff _griff = new(ZiehenAb);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _zeigerStempel = Stopwatch.GetTimestamp();
        Focus();
        var p = e.GetPosition(this);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            var art = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
            if (art is PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.RightButtonPressed)
            {
                _griff.Druecken(art == PointerUpdateKind.RightButtonPressed, p.X, p.Y);
                e.Pointer.Capture(this);
            }
            e.Handled = true;
            return;
        }
        FingerAuf(e.Pointer.Id, p);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            if (!_griff.Gedrueckt || AktuelleKamera is not { } maus)
            {
                return;
            }
            var mp = e.GetPosition(this);
            MausSchritt(maus, _griff.Bewegen(mp.X, mp.Y));
            return;
        }
        FingerBewegt(e.Pointer.Id, e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _zeigerStempel = Stopwatch.GetTimestamp();
        if (e.Pointer.Type == PointerType.Mouse)
        {
            var art = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
            if (art is PointerUpdateKind.LeftButtonReleased or PointerUpdateKind.RightButtonReleased)
            {
                var tippen = _griff.Loslassen(art == PointerUpdateKind.RightButtonReleased);
                if (!_griff.Gedrueckt)
                {
                    e.Pointer.Capture(null);
                }
                if (tippen)
                {
                    Tippen(e.GetPosition(this));
                }
            }
            e.Handled = true;
            return;
        }
        // erst auswerten, dann die Kapsel loesen: das Loesen meldet „Kapsel verloren“ (FingerWeg), das nie ein Tipp ist
        FingerAb(e.Pointer.Id, e.GetPosition(this));
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _griff.Abbrechen();
        }
        FingerWeg(e.Pointer.Id);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (AktuelleKamera is { } kamera)
        {
            Kamerabedienung.Mausrad(kamera, e.Delta.Y);
        }
        AnfordernBild();
        e.Handled = true;
    }

    /// <summary>Weite eines Pfeiltasten-Schritts in Bildpunkten (wie Ziehen).</summary>
    public const float TastenSchritt = Kamerabedienung.TastenSchritt;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Taste(e.Key))
        {
            e.Handled = true;
        }
    }

    /// <summary>Taste anwenden (auch fuer den Start-Test); true, wenn die Flaeche sie genommen hat.</summary>
    public bool Taste(Key taste)
    {
        if (AktuelleKamera is not { } kamera || _spiel is not { } spiel)
        {
            return false;
        }
        // Phase 8 W8 (Sammelliste 133): Tasten nach der Tastenbelegung (Standard wie bisher); Welle A 0.80.0: Regeln in
        // Kamerabedienung (draussen keine Pfeiltasten, R/F neigen)
        var aufgabe = Tastenbelegung.Aktuell.AufgabeFuer(taste.ToString());
        if (aufgabe == Tastenbelegung.Hinein)
        {
            return spiel.Betreten();
        }
        var fest = KameraFest;
        if (!Kamerabedienung.Taste(kamera, fest, aufgabe, Bounds.Height))
        {
            return false;
        }
        if (!fest)
        {
            _folgenAb = _uhr.Elapsed.TotalSeconds + 3;
        }
        AnfordernBild();
        return true;
    }

    /// <summary>Ein Schritt der Maus anwenden (auch fuer den Start-Test): Ziehen verschiebt, rechte Taste dreht.</summary>
    public void MausSchritt(Kamera kamera, Kameragriff.Schritt schritt)
    {
        var fest = KameraFest;
        // Welle A 1.6.0 (Sammelliste 441): Maus nach rechts = Blick nach rechts (Regel in Kamerabedienung.Maus)
        var bewegt = Kamerabedienung.Maus(kamera, fest, schritt, Bounds.Height);
        if (!bewegt)
        {
            return;
        }
        if (!fest)
        {
            _folgenAb = _uhr.Elapsed.TotalSeconds + 3;
        }
        AnfordernBild();
    }

    /// <summary>Start-Test: Maus mit rechter Taste von x1 nach x2 ziehen (wie echte Ereignisse ueber den Griff).</summary>
    public bool ProbeRechtsZiehen(double x1, double x2, double y) => ProbeRechtsZiehen(x1, y, x2, y);

    /// <summary>Welle A 0.80.0 (Start-Test): rechte Taste von (x1, y1) nach (x2, y2) ziehen: drehen und draussen neigen.</summary>
    public bool ProbeRechtsZiehen(double x1, double y1, double x2, double y2)
    {
        if (AktuelleKamera is not { } kamera)
        {
            return false;
        }
        var y = y2;
        _griff.Druecken(true, x1, y1);
        MausSchritt(kamera, _griff.Bewegen(x2, y2));
        var tippen = _griff.Loslassen(true);
        if (tippen)
        {
            Tippen(new Point(x2, y));
        }
        return !tippen;
    }

    private double Abstand()
    {
        var p = _zeiger.Values.Take(2).ToArray();
        return p.Length < 2 ? 0 : Math.Sqrt(Math.Pow(p[0].X - p[1].X, 2) + Math.Pow(p[0].Y - p[1].Y, 2));
    }

    /// <summary>Welle A 0.80.0: Hoehe der Mitte zwischen den zwei Fingern (gemeinsam hoch/runter = neigen).</summary>
    private double MitteY()
    {
        var p = _zeiger.Values.Take(2).ToArray();
        return p.Length < 2 ? 0 : (p[0].Y + p[1].Y) / 2;
    }

    /// <summary>Winkel der Linie zwischen den zwei Fingern in Grad.</summary>
    private double Winkel()
    {
        var p = _zeiger.Values.Take(2).ToArray();
        return p.Length < 2 ? 0 : Math.Atan2(p[1].Y - p[0].Y, p[1].X - p[0].X) * 180 / Math.PI;
    }

    /// <summary>Welle A 0.66.0: Hoehe der Figur draussen (auf Weg und Strasse wie bisher, auf einem Huegel auf dem Gelaende).</summary>
    internal static float FigurHoehe(StadtSpiel spiel) => MathF.Max(StadtSzene.Hoehe.Figur, spiel.Szene.BodenHoehe(spiel.FigurX, spiel.FigurZ));

    /// <summary>Welle A 0.66.0: Hoehe, auf die die Kamera schaut (Gelaende unter der Figur, ohne Gelaende 0).</summary>
    internal static float KameraZielHoehe(StadtSpiel spiel) => spiel.Szene.BodenHoehe(spiel.FigurX, spiel.FigurZ);

    /// <summary>Kamera sofort auf die Figur stellen (nach einem Sprung ohne Gehen, z. B. im Start-Test).</summary>
    public void KameraAufFigur()
    {
        if (AktuelleKamera is { } kamera && _spiel is { } spiel)
        {
            kamera.Setzen(spiel.FigurX, spiel.FigurZ);
            kamera.ZielY = spiel.ImInnenraum ? 0f : KameraZielHoehe(spiel);
            AnfordernBild();
        }
    }

    /// <summary>
    /// S 0.49.0 / A 0.72.0 (Weisung P12-231 Antwort 1a): nur im Stadtbau-Werkzeug (Innenraum in 3D ansehen) gesetzt, solange
    /// „Figur laufen lassen“ aus ist: ein Tipp laesst die Figur nicht gehen. Das Spiel setzt ihn nie.
    /// </summary>
    public bool OhneGehen { get; set; }

    /// <summary>Tippen an (x, y) in Punkten: Stadt -> Haus unter dem Finger, sonst zum Bodenpunkt; Buero -> zum Bodenpunkt.</summary>
    public void Tippen(Point p)
    {
        if (OhneGehen || AktuelleKamera is not { } kamera || _spiel is not { } spiel)
        {
            return;
        }
        if (FesteKamera && !Gehbedienung.AntippenAn)
        {
            // Welle A 0.95.0 (Weisung P12-306, Nico 1c): „Antippen zum Gehen“ ist aus, ein Tipp in die 3D-Ansicht laesst die Figur
            // nicht gehen (Ortsliste, Karte und Knoepfe bleiben)
            return;
        }
        _folgenAb = 0;
        if (spiel.ImInnenraum)
        {
            if (kamera.Boden((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height) is { } b)
            {
                spiel.GeheZu(b.X, b.Y);
            }
        }
        else if (kamera.Strahl((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height) is { } strahl
            && spiel.Szene.OrtBei(strahl.Start, strahl.Richtung) is { } ort)
        {
            spiel.GeheZu(ort.Id);
        }
        else if (kamera.Boden((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height, spiel.Szene.Gelaende is null ? null : spiel.Szene.BodenHoehe) is { } boden)
        {
            // kein Haus getroffen: frei dorthin gehen (P7-10 Nachtrag 1, Wunsch 3)
            spiel.GeheZu(boden.X, boden.Y);
        }
        AnfordernBild();
    }
}
