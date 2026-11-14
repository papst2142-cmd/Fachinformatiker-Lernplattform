using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Spiel;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

using Point = Avalonia.Point;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// S 0.7.0 (Weisung P12-45, Plan P12-26b Abschnitt 9): Bauen in der 3D-Ansicht. Eine durchsichtige Ebene ueber der
/// Spiel-Flaeche nimmt Maus und Finger an, die Spiel-Flaeche selbst bleibt unveraendert (das Spiel bedient sie wie
/// bisher). Alle Schritte laufen ueber die Draufsicht (<see cref="KartenFlaeche"/>: dieselbe Auswahl, dieselbe Karte
/// rechts, derselbe Verlauf). Treffer mit einem Strahl aus der Kamera (<see cref="Bauen3D.Treffer"/>), der Schatten am
/// Boden wird hier gezeichnet (kein Neubau der Szene), gruen/rot nach derselben Pruefung wie die Liste „Prüfungen
/// draußen“ (im Hintergrund, der neueste Wunsch gewinnt).
/// Maus: links auf ein Stueck waehlt und zieht es, links auf freier Flaeche schiebt die Ansicht, Doppelklick auf ein Haus
/// oeffnet den Innenraum, rechts ziehen dreht (links/rechts) und kippt (hoch/runter), mittlere Taste oder Leertaste
/// schiebt, Mausrad zoomt. Finger: ein Finger wie die linke Maustaste, zwei Finger zoomen (auseinander), kippen
/// (gemeinsam hoch/runter) und drehen (Drehung der Finger). Mit „Figur laufen lassen“ laeuft die Figur dorthin.
/// </summary>
public sealed partial class DreiBedienung : Control
{
    private readonly StadtFlaeche _drei;
    private readonly KartenFlaeche _karte;
    private Fingergeste _geste = new();
    private readonly Dictionary<long, Point> _finger = [];
    private double _winkel;

    private enum Griff { Keiner, Bauen, Schieben, Drehen }

    private Griff _griff;
    private Point _letzt;
    private Vector2? _ziehStart;

    // Welle Z: Ziehen von Punkt zu Punkt (Anfang in dieser Geste, Rechtsklick ohne Bewegung bricht ab)
    private bool _anfangInDieserGeste;
    private Point? _rechtsStart;
    private bool _rechtsBewegt;

    // Schatten
    private Bauen3D.Vorschau? _vorschau;
    private bool _ziehen;
    private (double, double)? _schluessel;
    private bool? _passt;
    private string? _grund;
    private int _nr;
    private bool _laeuft;
    private int _standNr;
    private int _basisNr = -1;
    private List<StadtbauFund>? _basis;

    /// <summary>Gegenprobe im Start-Test: Fingerauswertung wie vor S 0.5.1 (jeder Druck beginnt neu).</summary>
    public bool GesteAltFuerTests { set => _geste = new Fingergeste(altFuerTests: value); }

    public DreiBedienung(StadtFlaeche drei, KartenFlaeche karte)
    {
        _drei = drei;
        _karte = karte;
        Focusable = true;
        ClipToBounds = true;
        // die Kamera bewegt sich: Auswahl und Schatten mitzeichnen (das Ereignis kommt aus dem Zeichnen)
        _drei.BildGezeichnet += () => Dispatcher.UIThread.Post(InvalidateVisual);
        _karte.AuswahlGeaendert += () =>
        {
            if (_karte.SetzenArt is null && !_ziehen)
            {
                SchattenWeg();
            }
            InvalidateVisual();
        };
        // Welle Z: ohne Anfang gibt es keinen Schatten (gesetzt, abgebrochen)
        _karte.ZiehenGeaendert += () =>
        {
            if (_karte.ZiehAnfang is null)
            {
                SchattenWeg();
            }
            InvalidateVisual();
        };
        // S 0.51.0: die Nachpruefung der Ausstattung ist fertig: Schatten mit dem geprueften Plan neu bauen (gleich dem Schritt)
        _karte.ZugGeprueft += () =>
        {
            if (Projekt is { } proj && _karte.ZiehArtAktiv is { } art && _karte.ZiehAnfang is { } a && _karte.ZiehZiel is { } z && _vorschau is not null)
            {
                _vorschau = Bauen3D.Ziehen(proj.AussenStand, art, a.X, a.Y, z.X, z.Y, _karte.Raster, _karte.ZugAktiv ? _karte.ZugVorlage : null, _karte.ZugVorschau);
                PruefungAnstossen();
                InvalidateVisual();
            }
        };
        // S 0.27.0: Massband (gleiche Messung wie in der Draufsicht)
        _karte.MassbandGeaendert += InvalidateVisual;
        // Welle E4 Nachtrag (P12-249): Pinsel an/aus oder anderer Pinsel, ein angefangener Zug faellt weg
        _karte.PinselGeaendert += () =>
        {
            _pinselZug.Clear();
            InvalidateVisual();
        };
        // S 0.28.0: Tuer setzen (Vorschau, Modus); die Tueren des Modells aus der schon gebauten 3D-Szene
        _karte.TuerModusGeaendert += () =>
        {
            if (_karte.TuerFuer is not null && _drei.Spiel?.Szene is { } sz)
            {
                _karte.TuerModellTuerenAus(sz);
            }
            InvalidateVisual();
        };
        // S 0.9.1 (Weisung P12-53 Teil A): Setzen abgewaehlt, dann verschwindet der Schatten sofort
        _karte.SetzenGeaendert += () =>
        {
            if (_karte.SetzenArt is null && !_ziehen)
            {
                SchattenWeg();
            }
            InvalidateVisual();
        };
    }

    /// <summary>Schalter „Figur laufen lassen“: Tippen laesst die Figur laufen, gebaut wird nichts.</summary>
    public bool FigurLaufen { get; set; }

    /// <summary>Zeit des letzten Schattens bis zum fertigen Umriss in ms (Start-Test „3D Schatten folgt“).</summary>
    public double SchattenMs { get; private set; }

    /// <summary>Gruen (true), rot (false) oder noch in Pruefung (null); null auch ohne Schatten.</summary>
    public bool? Passt => _vorschau is null ? null : _passt;

    /// <summary>Gibt es gerade einen Schatten am Boden?</summary>
    public bool SchattenDa => _vorschau is { Umriss.Count: > 0 };

    public string? SchattenText => _vorschau is null ? null : Texte.Schatten(_passt, _grund);

    /// <summary>Letzter Abbruch der Schatten-Pruefung (Start-Test meldet ihn).</summary>
    public string? Abbruch { get; private set; }

    /// <summary>Pruefung des Schattens ist fertig (Start-Test).</summary>
    public event Action? SchattenGeprueft;

    private Projekt? Projekt => _karte.Projekt;

    private Kamera? Kamera => _drei.AktuelleKamera;

    /// <summary>Der Stand draussen hat sich geaendert (Hauptansicht): Funde von vorher neu rechnen, Schatten neu.</summary>
    public void StandGeaendert()
    {
        _standNr++;
        _basis = null;
        if (_karte.SetzenArt is not null && _schluessel is not null && _letztBoden is { } b)
        {
            _schluessel = null;
            SchattenSetzen(b.X, b.Y);
        }
        else if (!_ziehen)
        {
            SchattenWeg();
        }
        InvalidateVisual();
    }

    private Vector2? _letztBoden;

    // ---- Kamera -----------------------------------------------------------------------------------

    public void Kippen(float grad)
    {
        if (Kamera is { } k)
        {
            k.Neigung = Bauen3D.Kippen(k.Neigung, grad);
            _drei.AnfordernBild();
        }
    }

    public void GanzeStadt()
    {
        if (Kamera is { } k && Projekt is { } p)
        {
            var (x, z, abstand) = Bauen3D.GanzeStadt(p.AussenStand);
            k.Setzen(x, z);
            k.Zoomen(k.Abstand / abstand);
            k.Neigung = Bauen3D.NeigungMax - 15;
            _drei.AnfordernBild();
        }
    }

    /// <summary>Welle S 0.33.0 (Sammelliste 273): Kamera faehrt zur Weltstelle (x, y der Karte); Neigung, Drehung und Abstand bleiben.</summary>
    public void Hinfahren(double x, double y)
    {
        if (Kamera is { } k)
        {
            k.Setzen((float)x, (float)y);
            _drei.AnfordernBild();
        }
    }

    public void Zoomen(float faktor)
    {
        if (Kamera is { } k)
        {
            k.Zoomen(faktor);
            _drei.AnfordernBild();
        }
    }

    private Vector2? BodenAn(Point p) =>
        Kamera?.Boden((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height);

    /// <summary>Stueck unter dem Bildpunkt (Fassade zuerst, sonst Boden wie in der Draufsicht).</summary>
    public AussenStelle? TrefferAn(Point p)
    {
        if (Kamera is not { } k || Projekt is not { } proj || k.Strahl((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height) is not { } strahl)
        {
            return null;
        }
        var szene = _drei.Spiel?.Szene.Haeuser.Select(h => (h.Min, h.Max)).ToList();
        var haeuser = Bauen3D.Haeuser(proj.AussenStand, szene is null ? null : f => Bauen3D.HoeheAus(szene, f));
        return Bauen3D.Treffer(proj.AussenStand, strahl.Start, strahl.Richtung, haeuser, Bauen3D.Greifen(k.Abstand));
    }

    // ---- Eingabe ----------------------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        e.Handled = true;
        var p = e.GetPosition(this);
        e.Pointer.Capture(this);
        if (_drehZeiger is not null)
        {
            return;
        }
        if (!_geste.Aktiv && !FigurLaufen && GriffMitte is { } gm && Drehgriff.Getroffen(gm.X, gm.Y, p.X, p.Y)
            && !(e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed))
        {
            // Welle E: Drehgriff wie in der Draufsicht (ein Finger dreht in Rasten, zwei Finger bleiben bei der Kamera)
            _drehZeiger = e.Pointer.Id;
            _drehPunkt = p;
            InvalidateVisual();
            return;
        }
        if (e.Pointer.Type == PointerType.Mouse && !_geste.Aktiv)
        {
            var k = e.GetCurrentPoint(this).Properties;
            if (k.IsRightButtonPressed)
            {
                _griff = Griff.Drehen;
                _letzt = p;
                _rechtsStart = p;
                _rechtsBewegt = false;
                return;
            }
            if (k.IsMiddleButtonPressed || KartenFlaeche.LeertasteGehalten)
            {
                _griff = Griff.Schieben;
                _letzt = p;
                return;
            }
        }
        _griff = Griff.Bauen;
        if (e.Pointer.Type != PointerType.Mouse)
        {
            _finger[e.Pointer.Id] = p;
            if (_finger.Count == 2)
            {
                _winkel = Winkel();
            }
        }
        var erster = !_geste.Aktiv;
        if (erster)
        {
            _anfangInDieserGeste = false;
            _reiheAnfangHier = false;
            _mehr = null;
        }
        var bauen = !FigurLaufen && Projekt is { NurLesen: false };
        if (Malt)
        {
            // Welle E4 Nachtrag (P12-249): der Pinsel malt auch in 3D; ein Finger oder die linke Maustaste zieht den Zug
            if (erster && bauen)
            {
                PinselDruecken(p);
            }
            Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, bauen), p);
            return;
        }
        if (_karte.TuerFuer is not null && !FigurLaufen)
        {
            // S 0.28.0: ein Finger setzt und zieht die Tuer, zwei Finger bewegen die Kamera
            Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, bauen), p);
            if (erster && bauen && TuerPunktAn(p) is { } tp)
            {
                _karte.TuerVorschauAn(tp.X, tp.Y);
            }
            return;
        }
        AussenStelle? s = null;
        if (!FigurLaufen && _karte.SetzenArt is null && !_karte.MassbandAn && erster)
        {
            s = TrefferAn(p);
            if (MehrDrucken(e, s, p))
            {
                // S 0.30.0: Umschalt, „Mehrere wählen“ oder ein Stueck der Gruppe: ein Finger zieht Gruppe oder Rahmen
                Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, true), p);
                return;
            }
        }
        // beim Setzen zieht der Finger den Schatten mit (am Tablet gibt es kein Schweben)
        Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, bauen && (s is not null || _karte.SetzenArt is not null)), p);
        if (!erster || FigurLaufen)
        {
            return;
        }
        if (_karte.SetzenArt is not null)
        {
            if (bauen && !_karte.ReiheAktiv)
            {
                SchattenAn(p);
            }
            return;
        }
        if (e.ClickCount == 2 && s is not null && _karte.InnenOeffnen(s))
        {
            return;
        }
        if (Projekt is not null && !_karte.MassbandAn)
        {
            _karte.WaehlenMitMeldung(s);
        }
        _ziehStart = BodenAn(p);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (_drehZeiger is { } dz)
        {
            if (dz == e.Pointer.Id)
            {
                _drehPunkt = p;
                InvalidateVisual();
            }
            return;
        }
        switch (_griff)
        {
            case Griff.Drehen when Kamera is { } k:
                if (_rechtsStart is { } rs && Math.Abs(p.X - rs.X) + Math.Abs(p.Y - rs.Y) > 4)
                {
                    _rechtsBewegt = true;
                }
                k.Drehen((float)((p.X - _letzt.X) * 0.4));
                k.Neigung = Bauen3D.Kippen(k.Neigung, (float)((p.Y - _letzt.Y) * 0.3));
                _letzt = p;
                _drei.AnfordernBild();
                return;
            case Griff.Schieben when Kamera is { } k:
                k.Verschieben((float)(p.X - _letzt.X), (float)(p.Y - _letzt.Y), (float)Bounds.Height);
                _letzt = p;
                _drei.AnfordernBild();
                return;
            case Griff.Keiner:
                // Maus schwebt: beim Setzen folgt der Schatten
                if (e.Pointer.Type == PointerType.Mouse && _karte.SetzenArt is not null && !FigurLaufen && Projekt is { NurLesen: false })
                {
                    if (_karte.ReiheAktiv)
                    {
                        // S 0.30.0: Reihe, die Vorschau folgt der Maus ab dem Anfang
                        if (_karte.ReiheAnfang is not null && BodenAn(p) is { } rb)
                        {
                            _karte.ReiheZielSetzen(rb.X, rb.Y);
                        }
                    }
                    else
                    {
                        SchattenAn(p);
                    }
                }
                // S 0.27.0: Massband, das Ende folgt der Maus
                if (e.Pointer.Type == PointerType.Mouse && _karte.MassbandAn && !FigurLaufen && Kamera is { } mk && BodenAn(p) is { } mb)
                {
                    _karte.MassbandSchweben3D(mb.X, mb.Y, Bauen3D.Greifen(mk.Abstand));
                    InvalidateVisual();
                }
                return;
        }
        if (_finger.ContainsKey(e.Pointer.Id))
        {
            _finger[e.Pointer.Id] = p;
        }
        Anwenden(_geste.Bewegen(e.Pointer.Id, p.X, p.Y), p);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var p = e.GetPosition(this);
        if (_drehZeiger is { } dz)
        {
            if (dz == e.Pointer.Id)
            {
                GriffLoslassen(p);
                e.Pointer.Capture(null);
            }
            return;
        }
        if (_griff is Griff.Drehen or Griff.Schieben)
        {
            // Welle Z: Rechtsklick ohne Bewegung bricht das Ziehen ab (erst den Anfang, dann das Ziehen selbst)
            if (_griff == Griff.Drehen && _rechtsStart is not null && !_rechtsBewegt && _karte.ZiehArtAktiv is not null && !_karte.ZiehAbbrechen())
            {
                _karte.SetzenBeenden();
            }
            else if (_griff == Griff.Drehen && _rechtsStart is not null && !_rechtsBewegt && _karte.ReiheAktiv && !_karte.ReiheAbbrechen())
            {
                _karte.SetzenBeenden();
            }
            else if (_griff == Griff.Drehen && _rechtsStart is not null && !_rechtsBewegt && _karte.GelaendePinsel is not null)
            {
                // wie in der Draufsicht: Rechtsklick ohne Bewegung beendet den Pinsel
                _karte.GelaendePinsel = null;
            }
            _rechtsStart = null;
            _griff = Griff.Keiner;
            e.Pointer.Capture(null);
            return;
        }
        // zuerst auswerten, dann freigeben (wie die Draufsicht, Lehre S 0.5.1)
        Anwenden(_geste.Loslassen(e.Pointer.Id), p);
        _finger.Remove(e.Pointer.Id);
        e.Pointer.Capture(null);
        if (!_geste.Aktiv)
        {
            _griff = Griff.Keiner;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_drehZeiger == e.Pointer.Id)
        {
            _drehZeiger = null;
            InvalidateVisual();
            return;
        }
        Anwenden(_geste.Verloren(e.Pointer.Id), default);
        _finger.Remove(e.Pointer.Id);
        if (!_geste.Aktiv)
        {
            _griff = Griff.Keiner;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Zoomen(MathF.Pow(1.12f, (float)e.Delta.Y));
        e.Handled = true;
    }

    private double Winkel()
    {
        var f = _finger.Values.Take(2).ToArray();
        return f.Length < 2 ? 0 : Math.Atan2(f[1].Y - f[0].Y, f[1].X - f[0].X) * 180 / Math.PI;
    }

    private void Anwenden(Fingergeste.Schritt s, Point p)
    {
        if (Malt && PinselAnwenden(s))
        {
            return;
        }
        if (_karte.TuerFuer is not null && !FigurLaufen && TuerAnwenden(s))
        {
            return;
        }
        if (Reiht && ReiheAnwenden(s))
        {
            return;
        }
        if (_mehr is not null && MehrAnwenden(s))
        {
            return;
        }
        switch (s.Art)
        {
            case Fingergeste.Art.Tippen when FigurLaufen:
                _drei.Tippen(new Point(s.X, s.Y));
                break;
            case Fingergeste.Art.Tippen when _karte.MassbandAn && !FigurLaufen:
                MassbandTippen(new Point(s.X, s.Y));
                break;
            case Fingergeste.Art.Tippen when Zieht && BodenAn(new Point(s.X, s.Y)) is { } b:
                ZiehenTippen(b);
                break;
            case Fingergeste.Art.StueckZiehen when Zieht:
                if (_karte.ZiehAnfang is null && BodenAn(new Point(s.X - s.Dx, s.Y - s.Dy)) is { } start)
                {
                    _karte.ZiehAnfangSetzen(start.X, start.Y);
                    _anfangInDieserGeste = true;
                }
                SchattenAn(new Point(s.X, s.Y));
                break;
            case Fingergeste.Art.StueckFertig when Zieht && BodenAn(new Point(s.X, s.Y)) is { } b:
                SchattenWeg();
                _karte.ZiehenBis(b.X, b.Y);
                break;
            case Fingergeste.Art.StueckAbbrechen when Zieht:
                // ein zweiter Finger: die Kamera bewegt sich, ein eben erst begonnener Anfang faellt weg
                if (_anfangInDieserGeste)
                {
                    _karte.ZiehAnfangVerwerfen();
                }
                SchattenWeg();
                break;
            case Fingergeste.Art.Tippen when _karte.SetzenArt is not null:
            case Fingergeste.Art.StueckFertig when _karte.SetzenArt is not null:
                SetzenAn(new Point(s.X, s.Y));
                break;
            case Fingergeste.Art.StueckZiehen when _karte.SetzenArt is not null:
                SchattenAn(new Point(s.X, s.Y));
                break;
            case Fingergeste.Art.StueckZiehen when _karte.Auswahl is not null && _ziehStart is { } a && BodenAn(new Point(s.X, s.Y)) is { } b:
                ZiehenVorschau(b.X - a.X, b.Y - a.Y);
                break;
            case Fingergeste.Art.StueckFertig when _ziehen:
            {
                _ziehen = false;
                var (dx, dy) = _ziehDelta;
                SchattenWeg();
                _karte.Verschieben(dx, dy);
                break;
            }
            case Fingergeste.Art.StueckAbbrechen:
                if (_ziehen)
                {
                    _ziehen = false;
                    SchattenWeg();
                }
                break;
            case Fingergeste.Art.AnsichtSchieben when Kamera is { } k:
                k.Verschieben((float)s.Dx, (float)s.Dy, (float)Bounds.Height);
                _drei.AnfordernBild();
                break;
            case Fingergeste.Art.Zoomen when Kamera is { } k:
            {
                k.Zoomen((float)s.Faktor);
                // zwei Finger gemeinsam hoch = steiler (mehr von oben), Drehung der Finger dreht die Kamera
                k.Neigung = Bauen3D.Kippen(k.Neigung, (float)(-s.Dy * 0.3));
                if (_finger.Count >= 2)
                {
                    var w = Winkel();
                    var d = w - _winkel;
                    d = d > 180 ? d - 360 : d < -180 ? d + 360 : d;
                    k.Drehen((float)d);
                    _winkel = w;
                }
                _drei.AnfordernBild();
                break;
            }
        }
    }

    /// <summary>S 0.27.0: ein Tipp mit dem Massband am Boden (auch fuer den Start-Test).</summary>
    public void MassbandTippen(Point p)
    {
        if (Kamera is { } k && BodenAn(p) is { } b)
        {
            _karte.MassbandTippen(b.X, b.Y, Bauen3D.Greifen(k.Abstand), dreiD: true);
        }
    }

    /// <summary>Bildlage der Laengen-Beschriftung in 3D beim letzten Zeichnen (Start-Test).</summary>
    public Rect MassbandSchild { get; private set; }

    private void MassbandZeichnen(DrawingContext ctx, Kamera k)
    {
        MassbandSchild = default;
        var m = _karte.Massband;
        if (!_karte.MassbandAn || m.Anfang is not { } a
            || k.Bildpunkt(new Vector3(a.X, Bauen3D.SchattenHoehe, a.Y), (float)Bounds.Width, (float)Bounds.Height) is not { } pa)
        {
            return;
        }
        Point? ende = (m.Ende ?? m.Schwebe) is { } e && k.Bildpunkt(new Vector3(e.X, Bauen3D.SchattenHoehe, e.Y), (float)Bounds.Width, (float)Bounds.Height) is { } pe
            ? new Point(pe.X, pe.Y) : null;
        MassbandSchild = KartenFlaeche.MassbandLinie(ctx, new Point(pa.X, pa.Y), ende, m.Laenge, Bounds, Verdeckt, _anzeigen);
    }

    private bool Zieht => _karte.ZiehArtAktiv is not null && !FigurLaufen && Projekt is { NurLesen: false };

    /// <summary>Welle Z: erster Tipp = Anfang, zweiter Tipp = bis hierher ziehen (ein Schritt ueber die Draufsicht).</summary>
    private void ZiehenTippen(Vector2 b)
    {
        if (_karte.ZiehAnfang is null)
        {
            _karte.ZiehAnfangSetzen(b.X, b.Y);
        }
        else
        {
            SchattenWeg();
            _karte.ZiehenBis(b.X, b.Y);
        }
    }

    /// <summary>Start-Test: Tippen beim Ziehen an einem Bildpunkt (Anfang oder Ende).</summary>
    internal void ZiehenTippenFuerTests(Point p)
    {
        if (BodenAn(p) is { } b)
        {
            ZiehenTippen(b);
        }
    }

    /// <summary>Setzen an einem Bildpunkt (Tippen oder Start-Test): ein Schritt im Verlauf ueber die Draufsicht.</summary>
    public AussenStelle? SetzenAn(Point p)
    {
        if (BodenAn(p) is not { } b)
        {
            return null;
        }
        SchattenWeg();
        return _karte.SetzenAn(b.X, b.Y);
    }

    // ---- Schatten ---------------------------------------------------------------------------------

    private (double, double) _ziehDelta;

    /// <summary>Schatten fuer das naechste gesetzte Stueck unter dem Bildpunkt (Schweben, Finger beim Setzen).</summary>
    public void SchattenAn(Point p)
    {
        if (BodenAn(p) is { } b)
        {
            SchattenSetzen(b.X, b.Y);
        }
    }

    private double Schritt => _karte.Raster ? Bearbeitung.Raster : Bearbeitung.RasterFein;

    private (double, double) Runden(double x, double y) => (Math.Round(x / Schritt) * Schritt, Math.Round(y / Schritt) * Schritt);

    private void SchattenSetzen(double x, double y)
    {
        if (Projekt is not { } proj || _karte.SetzenArt is not { } art)
        {
            return;
        }
        _letztBoden = new Vector2((float)x, (float)y);
        var schluessel = Runden(x, y);
        if (_schluessel == schluessel && _vorschau is not null)
        {
            return;
        }
        if (_karte.ZiehArtAktiv is { } ziehArt)
        {
            // Welle Z: Vorschau vom Anfang bis hier, gleich dem echten Schritt (Bauen3D.Ziehen auf einer Kopie)
            if (_karte.ZiehAnfang is not { } a)
            {
                SchattenWeg();
                return;
            }
            _schluessel = schluessel;
            var zuhr = Stopwatch.StartNew();
            _karte.ZiehZielSetzen(x, y);
            _vorschau = Bauen3D.Ziehen(proj.AussenStand, ziehArt, a.X, a.Y, x, y, _karte.Raster, _karte.ZugAktiv ? _karte.ZugVorlage : null, _karte.ZugVorschau);
            SchattenMs = zuhr.Elapsed.TotalMilliseconds;
            PruefungAnstossen();
            return;
        }
        _schluessel = schluessel;
        var uhr = Stopwatch.StartNew();
        var dreh = KartenFlaeche.DrehbarBeimSetzen(art) ? _karte.SetzenDreh : 0;
        _vorschau = Bauen3D.Setzen(proj.AussenStand, art, x, y, _karte.Raster, _karte.SetzenModell, dreh, _karte.SetzenBlueprint);
        SchattenMs = uhr.Elapsed.TotalMilliseconds;
        PruefungAnstossen();
    }

    private void ZiehenVorschau(double dx, double dy)
    {
        if (Projekt is not { } proj || _karte.Auswahl is not { } s)
        {
            return;
        }
        _ziehen = true;
        _ziehDelta = (dx, dy);
        var schluessel = Runden(dx, dy);
        if (_schluessel == schluessel && _vorschau is not null)
        {
            return;
        }
        _schluessel = schluessel;
        var uhr = Stopwatch.StartNew();
        _vorschau = Bauen3D.Verschieben(proj.AussenStand, s, dx, dy, _karte.Raster);
        SchattenMs = uhr.Elapsed.TotalMilliseconds;
        PruefungAnstossen();
    }

    /// <summary>Schatten weg (gesetzt, abgebrochen, anderes Werkzeug).</summary>
    public void SchattenWeg()
    {
        _vorschau = null;
        _schluessel = null;
        _passt = null;
        _grund = null;
        _nr++;
        InvalidateVisual();
    }

    private void PruefungAnstossen()
    {
        _nr++;
        if (_vorschau is { Stelle: null } v)
        {
            _passt = false;
            _grund = v.Grund;
            SchattenGeprueft?.Invoke();
        }
        else
        {
            _passt = null;
            _grund = null;
            PruefungStarten();
        }
        InvalidateVisual();
    }

    private void PruefungStarten()
    {
        if (_laeuft || _vorschau is not { Stelle: not null } v || Projekt is not { } proj)
        {
            return;
        }
        _laeuft = true;
        var nr = _nr;
        var standNr = _standNr;
        var basis = _basisNr == standNr ? _basis : null;
        var vorherStand = basis is null ? (PyObjekt)Py.Kopie(proj.AussenStand)! : null;
        var fassung = proj.Basis;
        var innen = proj.Innenraeume();
        var daten = proj.Daten;
        var nachher = v.Stand;
        // S 0.26.0: mit den automatischen Laternen des echten Schritts (gleiche Funde wie danach in der Liste)
        var laternen = proj.LaternenFuerVorschau();
        _ = Task.Run(() =>
        {
            var vorher = basis ?? Bauen3D.Pruefen(vorherStand!, fassung, innen, daten);
            return (vorher, neu: Bauen3D.NeuRot(vorher, Bauen3D.Pruefen(laternen(nachher), fassung, innen, daten)));
        }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            _laeuft = false;
            if (t.IsFaulted)
            {
                // die Liste „Prüfungen draußen“ meldet denselben Abbruch; der Schatten bleibt grau
                Abbruch = t.Exception?.InnerException?.Message;
                return;
            }
            {
                if (standNr == _standNr)
                {
                    _basis = t.Result.vorher;
                    _basisNr = standNr;
                }
                if (nr == _nr && standNr == _standNr)
                {
                    _passt = t.Result.neu is null;
                    _grund = t.Result.neu?.Text;
                    SchattenGeprueft?.Invoke();
                    InvalidateVisual();
                    return;
                }
            }
            // inzwischen ein neuerer Wunsch: den neuesten pruefen
            PruefungStarten();
        }), TaskScheduler.Default);
    }

    // ---- Drehgriff (Welle E) -----------------------------------------------------------------------

    private long? _drehZeiger;
    private Point _drehPunkt;

    /// <summary>Mitte des gewaehlten Stuecks im Bild, wenn es einen Drehgriff hat.</summary>
    public Point? GriffMitte
    {
        get
        {
            if (Kamera is not { } k || Projekt is not { NurLesen: false } proj || _karte.SetzenArt is not null || _ziehen || _karte.Auswahl is not { } s
                || !AussenBearbeitung.Gueltig(proj.AussenStand, s) || AussenBearbeitung.DrehenNicht(proj.AussenStand, s) is not null
                || (AussenBearbeitung.MitEckpunkten(s.Art) && s.Punkt >= 0))
            {
                return null;
            }
            var m = AussenBearbeitung.Alle(proj.AussenStand).FirstOrDefault(z => z.Stelle == s);
            return k.Bildpunkt(new Vector3((float)m.X, Bauen3D.SchattenHoehe, (float)m.Y), (float)Bounds.Width, (float)Bounds.Height) is { } b ? new Point(b.X, b.Y) : null;
        }
    }

    /// <summary>Lage des Drehgriffs im Bild (Start-Test).</summary>
    public Point? Griff3D => GriffMitte is { } m ? new Point(Drehgriff.Lage(m.X, m.Y).X, Drehgriff.Lage(m.X, m.Y).Y) : null;

    private void GriffLoslassen(Point p)
    {
        _drehZeiger = null;
        if (GriffMitte is { } m && _karte.Auswahl is { } s)
        {
            _karte.DrehenUm(Drehgriff.Grad(m.X, m.Y, p.X, p.Y, AussenBearbeitung.DrehSchritt(s.Art)));
        }
        InvalidateVisual();
    }

    /// <summary>Start-Test: Drehgriff von seiner Lage bis (x, y) ziehen.</summary>
    internal void GriffZiehenFuerTests(double x, double y)
    {
        if (Griff3D is { } g)
        {
            _drehZeiger = -1;
            _drehPunkt = g;
            GriffLoslassen(new Point(x, y));
        }
    }

    private void GriffZeichnen(DrawingContext ctx)
    {
        if (GriffMitte is not { } m)
        {
            return;
        }
        var (gx, gy) = Drehgriff.Lage(m.X, m.Y);
        var akzent = new SolidColorBrush(Color.Parse(Lernplattform.Design.FarbenAnwenden.Aktuell.Farben["accent"]));
        var stift = new Pen(akzent, 2);
        ctx.DrawLine(new Pen(akzent, 1.5, new DashStyle([3, 2], 0)), m, new Point(gx, gy));
        ctx.DrawEllipse(Brushes.White, stift, new Point(gx, gy), Drehgriff.Halbmesser, Drehgriff.Halbmesser);
        if (_drehZeiger is not null && _karte.Auswahl is { } s)
        {
            var grad = Drehgriff.Grad(m.X, m.Y, _drehPunkt.X, _drehPunkt.Y, AussenBearbeitung.DrehSchritt(s.Art));
            var w = (-90 + grad) * Math.PI / 180;
            ctx.DrawLine(new Pen(akzent, 3), m, new Point(m.X + (Drehgriff.Abstand * Math.Cos(w)), m.Y + (Drehgriff.Abstand * Math.Sin(w))));
            var gt = (grad > 0 ? "+" : "") + grad.ToString(CultureInfo.InvariantCulture) + "°";
            var ft = new FormattedText(gt, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface(FontFamily.Default), 13, Brushes.White);
            Anzeigen.Zeichnen(ctx, ft, gt, new Point(gx + 14, gy - 10), new Size(4, 2), new SolidColorBrush(Color.Parse("#111827"), 0.8), null, 4, Verdeckt, Bounds.Size, _anzeigen);
        }
    }

    // ---- Zeichnen ---------------------------------------------------------------------------------

    private static readonly IBrush Gruen = new SolidColorBrush(Color.Parse("#34D399"), 0.5);
    private static readonly IBrush Rot = new SolidColorBrush(Color.Parse("#F87171"), 0.55);
    private static readonly IBrush Grau = new SolidColorBrush(Color.Parse("#9CA3AF"), 0.45);
    private static readonly IPen RandHell = new Pen(Brushes.White, 3);
    private static readonly IPen RandDunkel = new Pen(new SolidColorBrush(Color.Parse("#111827")), 1.5);

    /// <summary>Umriss beim letzten Zeichnen in Bildpunkten (Start-Test: der Schatten liegt unter dem Zeiger).</summary>
    public Rect SchattenRahmen { get; private set; }

    /// <summary>Weisung P12-137: wohin die Ebene ihre Anzeigen zuletzt gezeichnet hat (Text, Rahmen).</summary>
    public IReadOnlyList<(string Text, Rect Rahmen)> AnzeigenGezeichnet => _anzeigen;

    private readonly List<(string Text, Rect Rahmen)> _anzeigen = [];

    private IReadOnlyList<Rect> Verdeckt => _karte.VerdecktFuer(this);

    public override void Render(DrawingContext ctx)
    {
        // durchsichtig, aber trifft (sonst gehen Maus und Finger an die Spiel-Flaeche)
        ctx.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        _anzeigen.Clear();
        if (Kamera is not { } k || Projekt is not { } proj)
        {
            return;
        }
        MehrZeichnen(ctx, k, proj.AussenStand);
        ReiheZeichnen(ctx, k);
        if (_karte.Auswahl is { } s && !_ziehen)
        {
            foreach (var vieleck in Bauen3D.Umriss(proj.AussenStand, s))
            {
                if (Geometrie(k, vieleck) is { } g)
                {
                    ctx.DrawGeometry(null, RandHell, g);
                    ctx.DrawGeometry(null, RandDunkel, g);
                }
            }
        }
        SchattenRahmen = default;
        if (_vorschau is { } v)
        {
            var pinsel = _passt switch { true => Gruen, false => Rot, null => Grau };
            var rahmen = default(Rect?);
            foreach (var vieleck in v.Umriss)
            {
                if (Geometrie(k, vieleck) is { } g)
                {
                    ctx.DrawGeometry(pinsel, RandDunkel, g);
                    rahmen = rahmen is { } r ? r.Union(g.Bounds) : g.Bounds;
                }
            }
            SchattenRahmen = rahmen ?? default;
            if (SchattenText is { } text)
            {
                var ft = new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface(FontFamily.Default), 15, Brushes.White)
                {
                    MaxTextWidth = Math.Max(40, Bounds.Width - 40),
                };
                Anzeigen.Zeichnen(ctx, ft, text, new Point(8, Bounds.Height - ft.Height - 27), new Size(8, 5),
                    _passt == false ? new SolidColorBrush(Color.Parse("#7F1D1D"), 0.9) : new SolidColorBrush(Color.Parse("#111827"), 0.8), null, 6, Verdeckt, Bounds.Size, _anzeigen);
            }
        }
        if (_karte.ZiehAnfang is { } za
            && k.Bildpunkt(new Vector3((float)za.X, Bauen3D.SchattenHoehe, (float)za.Y), (float)Bounds.Width, (float)Bounds.Height) is { } zb)
        {
            // Welle Z: der Anfang als Punkt am Boden
            ctx.DrawEllipse(Brushes.White, RandDunkel, new Point(zb.X, zb.Y), 7, 7);
        }
        MassbandZeichnen(ctx, k);
        PinselZeichnen(ctx, k);
        TuerZeichnen(ctx, k);
        GriffZeichnen(ctx);
    }

    private StreamGeometry? Geometrie(Kamera k, Vector2[] vieleck)
    {
        var punkte = new List<Point>(vieleck.Length);
        foreach (var e in vieleck)
        {
            if (k.Bildpunkt(new Vector3(e.X, Bauen3D.SchattenHoehe, e.Y), (float)Bounds.Width, (float)Bounds.Height) is not { } b)
            {
                return null;
            }
            punkte.Add(new Point(b.X, b.Y));
        }
        if (punkte.Count < 3)
        {
            return null;
        }
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(punkte[0], true);
            foreach (var q in punkte.Skip(1))
            {
                c.LineTo(q);
            }
            c.EndFigure(true);
        }
        return g;
    }
}
