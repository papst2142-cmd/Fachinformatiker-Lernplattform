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

    /// <summary>
    /// S 0.33.0 (Weisung P12-148, Sammelliste 272): die gezeichnete Fassade des Hauses (Waende, glatte Stellen, Tueren des
    /// Modells, Fensterteile), im Hintergrund gerechnet; null = noch nicht da (dann rechnet das Setzen sie selbst).
    /// </summary>
    public TuerFassade? Fassade
    {
        get
        {
            // Haus inzwischen verschoben, gedreht, umgebaut oder Rueckgaengig: alte Fassade gilt nicht mehr, neu rechnen
            if (_fassade is not null && TuerFuer is { } s && _projekt is { } p && FassadeSchluessel(p.AussenStand, s) != _fassadeSchluessel)
            {
                _fassade = null;
                FassadeRechnen(p, s);
            }
            return _fassade;
        }
        private set => _fassade = value;
    }

    private TuerFassade? _fassade;

    /// <summary>Stand des Hauses, fuer den <see cref="Fassade"/> gerechnet ist.</summary>
    private string? _fassadeSchluessel;

    /// <summary>
    /// Alles, wovon die Fassade abhaengt: Flaeche des Hauses, sein Eintrag (Ort bzw. Gebaeude ohne Ort, mit Modell und Drehung),
    /// Drehung des Orts, Modelle und Bauteile eigener Gebaeude. Die Tuer selbst zaehlt nicht.
    /// </summary>
    private static string FassadeSchluessel(Lernplattform.Core.Daten.PyObjekt stand, AussenStelle s)
    {
        var flaeche = AussenBearbeitung.HausFlaeche(stand, s);
        var daten = Aussen.Stadtdaten(stand);
        object? eintrag = s.Art == AussenArt.Haus ? AussenBearbeitung.Ort(stand, s.Ort!)
            : s.Nummer >= 0 && s.Nummer < Aussen.Landschaft(stand).Count ? Aussen.Landschaft(stand)[s.Nummer] : null;
        var dreh = Lernplattform.Core.Spiel.Py.Get(Lernplattform.Core.Spiel.Py.Get(Lernplattform.Core.Spiel.SpielDaten.Get(daten, "darstellung"), "hausdreh"), s.Ort ?? "");
        // S 0.49.0: auch die feine Drehung eines Orts (hausgrad)
        var grad = Lernplattform.Core.Spiel.Py.Get(Lernplattform.Core.Spiel.Py.Get(Lernplattform.Core.Spiel.SpielDaten.Get(daten, "darstellung"), "hausgrad"), s.Ort ?? "");
        return string.Join("|", flaeche.ToString(), Lernplattform.Core.Daten.PythonJson.Text(eintrag), Lernplattform.Core.Daten.PythonJson.Text(dreh),
            Lernplattform.Core.Daten.PythonJson.Text(grad),
            Lernplattform.Core.Daten.PythonJson.Text(Lernplattform.Core.Spiel.SpielDaten.Get(daten, "bauteile")));
    }

    /// <summary>Platz der Vorschau (gleich dem Schritt beim Loslassen).</summary>
    public TuerFassade.Platz? TuerPlatz { get; private set; }

    /// <summary>Wand des letzten Zugschritts (Frage 3 = a: hinter dem Wandende bleibt die Tuer an ihr).</summary>
    private int? _tuerWand;

    /// <summary>Nummer der Rechnung (eine spaetere ersetzt eine fruehere).</summary>
    private int _fassadeNr;

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
        TuerPlatz = null;
        TuerModellTueren = null;
        _tuerWand = null;
        _tuerWartend = null;
        Fassade = null;
        FassadeRechnen(p, s);
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
        TuerPlatz = null;
        Fassade = null;
        _tuerWand = null;
        _tuerWartend = null;
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

    /// <summary>
    /// Sammelliste 277 (Weisung P12-164): Fassaden-Rechnung im Hintergrund fuer das Haus <paramref name="s"/> zum Stand
    /// <paramref name="schluessel"/>; laeuft schon, sobald ein Haus gewaehlt ist (<see cref="FassadeVorrechnen"/>), das Tuer-Setzen
    /// nimmt sie dann nur noch ab. Nie auf dem Bedienfaden.
    /// </summary>
    private (Projekt P, AussenStelle S, string Schluessel, Task<TuerFassade?> Rechnung)? _fassadeVorrat;

    /// <summary>Tipp, der kam, bevor die Fassade fertig war: wird gesetzt, sobald sie da ist (ein Schritt).</summary>
    private (double X, double Y, double? Weite)? _tuerWartend;

    /// <summary>Die Rechnung fuer das aktuelle Tuer-Setzen ist fertig (auch mit null = kein Haus in der Szene).</summary>
    private bool _fassadeGerechnet;

    private TaskCompletionSource _fassadeBereit = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Start-Test: fertig, sobald die Fassade des laufenden Tuer-Setzens da ist (und ein wartender Tipp gesetzt).</summary>
    public Task FassadeBereit => _fassadeBereit.Task;

    /// <summary>Wie oft die Fassade auf dem Bedienfaden gerechnet wurde (nur der alte Weg; Start-Test: muss 0 bleiben).</summary>
    public int FassadeAufBedienfaden { get; private set; }

    /// <summary>Gegenprobe Sammelliste 277: Tipp vor fertiger Fassade rechnet sie auf dem Bedienfaden (wie bis S 0.34.0).</summary>
    public static bool TippSofortAltFuerTests { get; set; }

    /// <summary>Sammelliste 277: beim Waehlen eines Hauses die Fassade schon im Hintergrund rechnen.</summary>
    private void FassadeVorrechnen(AussenStelle? s)
    {
        if (s is not { } st || st.Art is not (AussenArt.Haus or AussenArt.Gebaeude) || _projekt is not { NurLesen: false } p
            || TuerFassade.AltFuerTests || !AussenBearbeitung.Gueltig(p.AussenStand, st) || TuerSetzen.Nicht(p.AussenStand, st) is not null)
        {
            return;
        }
        _ = FassadeRechnung(p, st);
    }

    /// <summary>Rechnung aus dem Vorrat (gleiches Haus, gleicher Stand), sonst neu im Hintergrund.</summary>
    private Task<TuerFassade?> FassadeRechnung(Projekt p, AussenStelle s)
    {
        var stand = (Lernplattform.Core.Daten.PyObjekt)Lernplattform.Core.Spiel.Py.Kopie(p.AussenStand)!;
        var schluessel = FassadeSchluessel(stand, s);
        if (_fassadeVorrat is { } v && ReferenceEquals(v.P, p) && v.S == s && v.Schluessel == schluessel && !v.Rechnung.IsFaulted)
        {
            return v.Rechnung;
        }
        var rechnung = Task.Run(() => TuerFassade.Rechnen(p, stand, s));
        _fassadeVorrat = (p, s, schluessel, rechnung);
        return rechnung;
    }

    /// <summary>Fassade im Hintergrund rechnen (nach jedem Setzen neu: ein getauschtes Fensterteil aendert die Wand).</summary>
    private void FassadeRechnen(Projekt p, AussenStelle s)
    {
        if (TuerFassade.AltFuerTests)
        {
            return;
        }
        var nr = ++_fassadeNr;
        _fassadeGerechnet = false;
        if (_fassadeBereit.Task.IsCompleted)
        {
            _fassadeBereit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        var schluessel = FassadeSchluessel(p.AussenStand, s);
        var rechnung = FassadeRechnung(p, s);
        if (rechnung.IsCompletedSuccessfully && !TippSofortAltFuerTests)
        {
            // schon fertig (beim Waehlen vorgerechnet): sofort uebernehmen, nichts rechnen
            _fassadeSchluessel = schluessel;
            Fassade = rechnung.Result;
            _fassadeGerechnet = true;
            _fassadeBereit.TrySetResult();
            return;
        }
        _ = rechnung.ContinueWith(t =>
        {
            var f = t.IsCompletedSuccessfully ? t.Result : null;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (TuerFuer == s && nr == _fassadeNr)
                {
                    _fassadeSchluessel = schluessel;
                    Fassade = f;
                    _fassadeGerechnet = true;
                    if (_tuerWartend is { } w)
                    {
                        _tuerWartend = null;
                        TuerSetzenAn(w.X, w.Y, w.Weite);
                    }
                    TuerModusGeaendert?.Invoke();
                    InvalidateVisual();
                    _fassadeBereit.TrySetResult();
                }
            });
        }, TaskScheduler.Default);
    }

    /// <summary>Alter Weg (nur Gegenprobe 277): Fassade jetzt auf dem Bedienfaden rechnen.</summary>
    private TuerFassade? FassadeJetzt()
    {
        if (Fassade is null && !TuerFassade.AltFuerTests && TuerFuer is { } s && _projekt is { } p)
        {
            _fassadeNr++;
            _fassadeSchluessel = FassadeSchluessel(p.AussenStand, s);
            FassadeAufBedienfaden++;
            Fassade = TuerFassade.Rechnen(p, p.AussenStand, s);
            _fassadeGerechnet = true;
        }
        return Fassade;
    }

    /// <summary>Bisherige Wand: die des letzten Zugschritts, sonst die der gespeicherten Tuer.</summary>
    private int? BisherWand(TuerFassade f) =>
        _tuerWand ?? (TuerFuer is { } s && _projekt is { } p ? f.WandVon(TuerSetzen.Tuer(p.AussenStand, s)) : null);

    private void ModellTuerenRechnen(Projekt p, AussenStelle s)
    {
        if (!TuerFassade.AltFuerTests)
        {
            return;
        }
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
        s.Art == AussenArt.Haus ? OrtName(s.Ort) : ModellNamen.Beschriftung(p.AussenStand, s);

    /// <summary>Liegt die Weltstelle nah genug am Haus, dass ein Tipp seine Wand meint? (bis 1,5 oder 30 Bildpunkte)</summary>
    public bool AmHaus(double x, double y, double? weite = null)
    {
        if (TuerFuer is not { } s || _projekt is not { } p)
        {
            return false;
        }
        var (hx, hy, hw, hh) = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        var grenze = weite ?? Math.Max(1.5, 30 / _zoom);
        static double Abstand(double x, double y, double x1, double y1, double x2, double y2)
        {
            var dx = Math.Max(Math.Max(x1 - x, 0), x - x2);
            var dy = Math.Max(Math.Max(y1 - y, 0), y - y2);
            return Math.Sqrt((dx * dx) + (dy * dy));
        }
        // S 0.33.0: auch an den gezeichneten Bloecken (Vorbau, Bahnsteig, Vordach ragen ueber das Rechteck der Daten)
        return Abstand(x, y, hx, hy, hx + hw, hy + hh) <= grenze
            || (Fassade is { } fa && fa.Fein.Zurueck(new Vector2((float)x, (float)y)) is var q && fa.Bloecke.Any(b => Abstand(q.X, q.Y, b.X1, b.Z1, b.X2, b.Z2) <= grenze));
    }

    /// <summary>Vorschau der Tuer an die Weltstelle (x, y): gleiche Rechnung wie der Schritt.</summary>
    public void TuerVorschauAn(double x, double y)
    {
        if (TuerFuer is { } s && _projekt is { } p)
        {
            if (Fassade is { } f)
            {
                var platz = f.Finden(new Vector2((float)x, (float)y), BisherWand(f), Raster);
                _tuerWand = platz.Wand;
                TuerPlatz = platz;
                TuerVorschau = (platz.Gezeichnet, platz.Aussen);
                TuerModusGeaendert?.Invoke();
                InvalidateVisual();
                return;
            }
            TuerVorschau = TuerSetzen.AufWand(AussenBearbeitung.HausFlaeche(p.AussenStand, s), new Vector2((float)x, (float)y), Raster ? TuerModellTueren : null);
            TuerModusGeaendert?.Invoke();
            InvalidateVisual();
        }
    }

    /// <summary>Vorschau weg (zweiter Finger, abgebrochen).</summary>
    public void TuerVorschauWeg()
    {
        TuerVorschau = null;
        TuerPlatz = null;
        _tuerWand = null;
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
        else if (!TippSofortAltFuerTests && !TuerFassade.AltFuerTests && Fassade is null && !_fassadeGerechnet)
        {
            // Sammelliste 277: die Fassade ist noch nicht fertig. Nicht hier rechnen (das hielt die Oberflaeche am Tablet bis
            // 6,6 s an), sondern den Tipp merken; er wird gesetzt, sobald die Rechnung im Hintergrund fertig ist
            _tuerWartend = (x, y, weite);
            Gemeldet?.Invoke(Texte.TuerFassadeRechnet);
            InvalidateVisual();
            return Texte.TuerFassadeRechnet;
        }
        else
        {
            string? hinweis = null;
            var punkt = new Vector2((float)x, (float)y);
            if ((TippSofortAltFuerTests ? FassadeJetzt() : Fassade) is { } f)
            {
                var platz = f.Finden(punkt, BisherWand(f), Raster);
                p.AussenAendern(Texte.TuerSetzenSchritt, a => hinweis = TuerSetzen.SetzenAn(a, s, platz));
                _tuerWand = platz.Wand;
                if (platz.Fensterteil is not null)
                {
                    // das getauschte Teil aendert die Fassade: neu rechnen
                    Fassade = null;
                    FassadeRechnen(p, s);
                }
            }
            else
            {
                var tueren = Raster ? TuerModellTueren : null;
                p.AussenAendern(Texte.TuerSetzenSchritt, a => hinweis = TuerSetzen.Setzen(a, s, punkt, tueren));
            }
            TuerPlatz = null;
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
        var stift = new Pen(TuerFarbe, 3, new DashStyle([2, 2], 0));
        if (Fassade is { } f)
        {
            // S 0.33.0: die gezeichneten Waende (Bloecke des Hauses), an die die Tuer kommt
            foreach (var b in f.Bloecke)
            {
                if (!f.Fein.Aktiv)
                {
                    ctx.DrawRectangle(null, stift, new Rect(P(b.X1, b.Z1), P(b.X2, b.Z2)));
                    continue;
                }
                // S 0.49.0: schraeg gedrehtes Haus, die Bloecke gedreht wie im Spiel
                var e = f.Fein.Ecken(b.X1, b.Z1, b.X2, b.Z2);
                for (var i = 0; i < e.Length; i++)
                {
                    var (a, c) = (e[i], e[(i + 1) % e.Length]);
                    ctx.DrawLine(stift, P(a.X, a.Y), P(c.X, c.Y));
                }
            }
        }
        else
        {
            var e = AussenBearbeitung.HausEcken(p.AussenStand, s);
            for (var i = 0; i < e.Length; i++)
            {
                var (a, c) = (e[i], e[(i + 1) % e.Length]);
                ctx.DrawLine(stift, P(a.X, a.Y), P(c.X, c.Y));
            }
        }
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
