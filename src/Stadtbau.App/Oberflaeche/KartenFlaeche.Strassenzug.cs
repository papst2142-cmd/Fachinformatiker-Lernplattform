using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.51.0 (Weisung P12-241, Kurzplan P12-131 Punkt 3): „Straße ziehen“ mit Ausstattung. Ist eine Vorlage gewaehlt,
/// zeigt die Vorschau jedes Stueck der Ausstattung gruen (kommt dazu) oder rot (wird ausgelassen); im Hintergrund laeuft die
/// Nachpruefung mit der ganzen Pruefung, ihr Ergebnis ersetzt die Vorschau. Der Schritt nimmt genau diesen Plan
/// (<see cref="Projekt.StrassenzugZiehen"/>), so ist die Vorschau gleich dem Ergebnis.
/// </summary>
public sealed partial class KartenFlaeche
{
    private Strassenzug? _zugVorlage;
    private CancellationTokenSource? _zugAbbruch;
    private (double Ax, double Ay, double Bx, double By, bool Raster, Strassenzug Vorlage)? _zugSchluessel;

    /// <summary>Gewaehlte Ausstattung fuer „Straße ziehen“ (null = nur die Straße, wie bisher).</summary>
    public Strassenzug? ZugVorlage
    {
        get => _zugVorlage;
        set
        {
            _zugVorlage = value;
            ZugPlanNeu();
            ZiehenGeaendert?.Invoke();
            InvalidateVisual();
        }
    }

    /// <summary>Plan der Vorschau (erst schnell, nach der Nachpruefung mit <see cref="StrassenzugPlan.Nachgeprueft"/>), null = keiner.</summary>
    public StrassenzugPlan? ZugVorschau { get; private set; }

    /// <summary>Die Nachpruefung im Hintergrund ist fertig, <see cref="ZugVorschau"/> ist jetzt der Plan des Schritts.</summary>
    public event Action? ZugGeprueft;

    /// <summary>Wird gerade eine Straße mit Ausstattung gezogen?</summary>
    public bool ZugAktiv => ZiehArtAktiv == ZiehArt.Strasse && _zugVorlage is not null;

    /// <summary>Vorschau neu planen (nach jeder Bewegung) und die Nachpruefung im Hintergrund anstossen.</summary>
    private void ZugPlanNeu()
    {
        _zugAbbruch?.Cancel();
        _zugAbbruch = null;
        if (!ZugAktiv || ZiehAnfang is not { } a || ZiehZiel is not { } z || Stand is not { } stand || _projekt is not { } projekt)
        {
            ZugVorschau = null;
            _zugSchluessel = null;
            return;
        }
        var vorlage = _zugVorlage!;
        var raster = Raster;
        var schluessel = (a.X, a.Y, z.X, z.Y, raster, vorlage);
        if (_zugSchluessel == schluessel && ZugVorschau is not null)
        {
            return;
        }
        _zugSchluessel = schluessel;
        ZugVorschau = AussenBearbeitung.StrassenzugPlanen(stand, a.X, a.Y, z.X, z.Y, raster, vorlage);
        if (!ZugVorschau.Gefunden || ZugVorschau.Stuecke.Count == 0)
        {
            return;
        }
        // Nachpruefung auf einer Kopie im Hintergrund (der Stand des Projekts bleibt unberuehrt)
        var kopie = (PyObjekt)Py.Kopie(stand)!;
        var pruefen = projekt.ZugPruefen();
        var abbruch = new CancellationTokenSource();
        _zugAbbruch = abbruch;
        _ = Task.Run(() =>
        {
            try
            {
                var fertig = AussenBearbeitung.StrassenzugPlanen(kopie, a.X, a.Y, z.X, z.Y, raster, vorlage, pruefen, abbruch.Token);
                Dispatcher.UIThread.Post(() =>
                {
                    if (abbruch.IsCancellationRequested || !ReferenceEquals(_zugAbbruch, abbruch))
                    {
                        return;
                    }
                    ZugVorschau = fertig;
                    ZiehenGeaendert?.Invoke();
                    ZugGeprueft?.Invoke();
                    InvalidateVisual();
                });
            }
            catch (OperationCanceledException)
            {
                // neue Bewegung: diese Nachpruefung ist ueberholt
            }
        });
    }

    /// <summary>Der fertig nachgepruefte Plan genau dieser Punkte (sonst null, dann prueft der Schritt selbst).</summary>
    private StrassenzugPlan? ZugPlanFuer(double ax, double ay, double bx, double by, bool raster) =>
        ZugVorschau is { Nachgeprueft: true } p && _zugSchluessel == (ax, ay, bx, by, raster, _zugVorlage!) ? p : null;

    /// <summary>Straße mit Ausstattung ziehen (aus <see cref="ZiehenBis"/>): ein Schritt, Meldung mit Zahl der Stuecke.</summary>
    private AussenStelle? ZugZiehenBis(Point a, double x, double y)
    {
        if (_projekt is null)
        {
            return null;
        }
        var raster = Raster;
        var vorlage = _zugVorlage!;
        var (strasse, neu, plan) = _projekt.StrassenzugZiehen(a.X, a.Y, x, y, raster, vorlage, ZugPlanFuer(a.X, a.Y, x, y, raster));
        if (!plan.Gefunden)
        {
            ZiehGrund = plan.Plan.Grund;
            ZiehenGeaendert?.Invoke();
            Gemeldet?.Invoke(plan.Plan.Grund!);
            InvalidateVisual();
            return null;
        }
        _zugAbbruch?.Cancel();
        _zugAbbruch = null;
        ZugVorschau = null;
        _zugSchluessel = null;
        ZiehAnfang = null;
        ZiehZiel = null;
        ZiehGrund = null;
        ZiehenGeaendert?.Invoke();
        Gemeldet?.Invoke(Texte.ZugGesetzt(Verbindung.Stuecke(plan.Plan).Count, neu.Count, plan.Ausgelassen));
        Waehlen(strasse);
        return strasse;
    }

    /// <summary>Vorschau der Ausstattung: Baum als Kreis, Zebrastreifen als Streifen, Haltestelle als Quadrat; gruen oder rot.</summary>
    private void ZugZeichnen(DrawingContext ctx)
    {
        if (!ZugAktiv || ZiehAnfang is null || ZiehZiel is null || ZugVorschau is not { Gefunden: true } plan)
        {
            return;
        }
        var rand = new Pen(Pinsel("#111827"), 1);
        foreach (var st in plan.Stuecke)
        {
            var fuellung = Pinsel(st.Frei ? "#34D399" : "#F87171", 0.75);
            var m = P(st.Lage.X, st.Lage.Y);
            switch (st.Teil)
            {
                case ZugTeil.Baum:
                {
                    var r = Math.Max(4, AussenBearbeitung.ReiheBaumRadius * _zoom);
                    ctx.DrawEllipse(fuellung, rand, m, r, r);
                    break;
                }
                case ZugTeil.Zebrastreifen:
                {
                    var r = Math.Max(5, 0.4 * _zoom);
                    ctx.DrawRectangle(fuellung, rand, new Rect(m.X - r, m.Y - r, 2 * r, 2 * r));
                    ctx.DrawLine(new Pen(Brushes.White, Math.Max(1.5, 0.08 * _zoom)), new Point(m.X - r + 2, m.Y), new Point(m.X + r - 2, m.Y));
                    break;
                }
                default:
                {
                    var r = Math.Max(6, 0.5 * _zoom);
                    ctx.DrawRectangle(fuellung, new Pen(Pinsel("#111827"), 2), new Rect(m.X - r, m.Y - r, 2 * r, 2 * r));
                    break;
                }
            }
        }
    }
}
