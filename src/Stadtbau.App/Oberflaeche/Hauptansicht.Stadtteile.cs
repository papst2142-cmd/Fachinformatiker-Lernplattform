using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Stadtbau.Core;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle Nachladen S 0.54.0 (Weisung P12-280, Nico 1c): Knopf „Stadtteile“ (Taste T) neben der Waermekarte. In der
/// Draufsicht zeigt er das Raster der Kacheln mit Namen und, im Hintergrund gerechnet, Dreiecke und Netzdaten je Kachel
/// (<see cref="StadtteilWert"/>, nach jeder Aenderung neu, der neueste Stand gewinnt). In 3D nennt er den Stadtteil in der
/// Mitte der Ansicht. Screenreader: Name des Knopfs mit „gewählt“, Melden des Stadtteils.
/// </summary>
public sealed partial class Hauptansicht
{
    private Button _stadtteileChip = null!;
    private int _stadtteileStand;
    private TaskCompletionSource _stadtteileFertig = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Button StadtteileKnopf => _stadtteileChip;

    /// <summary>Start-Test: fertig, sobald die Werte des letzten Stands in der Karte stehen.</summary>
    internal Task StadtteileFertig => _stadtteileFertig.Task;

    private void StadtteileKnopfBauen()
    {
        _stadtteileChip = Chip(Texte.Stadtteile, Texte.StadtteileTipp, StadtteileUmschalten);
        _werkzeugLeiste.Children.Insert(_werkzeugLeiste.Children.IndexOf(_ebenenHinweis), _stadtteileChip);
    }

    /// <summary>Stadtteile an/aus (Knopf und Taste T); in 3D nur Melden des Stadtteils in der Mitte der Ansicht.</summary>
    internal void StadtteileUmschalten()
    {
        if (_projekt is null || _innen || _geschichteOffen)
        {
            return;
        }
        if (_drei)
        {
            var name = Drei.AktuelleKamera is { } k ? Karte.StadtteilAn(k.ZielX, k.ZielZ).Name : "?";
            Melden(Texte.StadtteileIn3D(name));
            return;
        }
        Karte.StadtteileAn = !Karte.StadtteileAn;
        ChipMarkieren(_stadtteileChip, Karte.StadtteileAn);
        AutomationProperties.SetName(_stadtteileChip, Karte.StadtteileAn ? Texte.Gewaehlt(Texte.Stadtteile) : Texte.Stadtteile);
        if (Karte.StadtteileAn)
        {
            Melden(Texte.StadtteileRechnet);
            StadtteileRechnen();
        }
    }

    /// <summary>Werte je Kachel im Hintergrund (nur wenn der Knopf an ist); nach jeder Aenderung draussen erneut.</summary>
    private void StadtteileRechnen()
    {
        if (_projekt is not { } p || !Karte.StadtteileAn)
        {
            return;
        }
        var stand = ++_stadtteileStand;
        if (_stadtteileFertig.Task.IsCompleted)
        {
            _stadtteileFertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        var (fassung, daten) = (p.Stand(), p.Daten);
        _ = Hintergrund(Task.Run(() => StadtteilWert.Fuer(fassung, daten))).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            if (stand != _stadtteileStand)
            {
                return;
            }
            if (t.IsFaulted)
            {
                _ordner.FehlerEinmal("Stadtteile", t.Exception);
                Karte.Stadtteile = null;
            }
            else
            {
                Karte.Stadtteile = t.Result;
                if (StatusText == Texte.StadtteileRechnet)
                {
                    Melden(Texte.StadtteileFertig(t.Result.Count));
                }
            }
            _stadtteileFertig.TrySetResult();
        }), TaskScheduler.Default);
    }
}
