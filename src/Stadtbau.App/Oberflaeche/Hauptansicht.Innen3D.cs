using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Messung;
using Lernplattform.Spiel;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// S 0.49.0 (Weisung P12-231 Antwort 1a): der offene Innenraum in 3D, wie das Spiel ihn zeichnet (Knopf „In 3D ansehen“ in der
/// Kamera-Karte des Grundrisses). Die Mitte wechselt zwischen Grundriss und 3D; links und rechts bleiben. Tag oder Nacht,
/// Etage, „Figur laufen lassen“ in der Karte unten rechts; Kamera wie im Spiel (ziehen, rechte Maustaste, Mausrad, zwei
/// Finger). Gebaut wird im Hintergrund (<see cref="Innen3D.Bauen"/>), nach jeder Aenderung neu, der neueste Stand gewinnt.
/// Hier wird nur angesehen: waehlt man links ein Stueck zum Setzen, kommt der Grundriss zurueck.
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>Die 3D-Flaeche des Innenraums (eigene Flaeche, die 3D-Ansicht der Stadt bleibt unberuehrt).</summary>
    internal StadtFlaeche Innen3DFlaeche { get; } = new() { OhneGehen = true };

    private readonly ContentControl _innenMitte = new() { Margin = new Thickness(12) };
    private Control? _grundrissMitte;
    private Control? _innen3DMitte;
    private readonly TextBlock _innen3DStatus = Schrift.Text("", "Klein", farbe: "Pinsel.muted");
    private readonly CheckBox _innen3DNacht = new() { Content = Texte.Nacht };
    private readonly CheckBox _innen3DFigur = new() { Content = Texte.FigurLaufen };
    private readonly ComboBox _innen3DEtage = new() { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 160 };
    private StackPanel? _innen3DEtageZeile;
    private readonly StadtSzene.Vorrat _innen3DVorrat = new();
    private bool _innen3DAn;
    private bool _innen3DBaut;
    private bool _innen3DWartetAufBild;
    private bool _innen3DEtageFuellt;
    private int _innen3DWunsch;
    private int _innen3DGebaut = -1;
    private string? _innen3DGebautOrt;
    private bool _innen3DGebautNacht;
    private Innen3D.Ergebnis? _innen3DLetzt;
    private long _innen3DAenderung;
    private TaskCompletionSource _innen3DFertig = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Knopf „In 3D ansehen“ in der Kamera-Karte des Grundrisses (Start-Test).</summary>
    internal Button? Innen3DKnopf { get; private set; }

    /// <summary>Knopf „Zurück zum Grundriss“ (Start-Test).</summary>
    internal Button? Innen3DZurueckKnopf { get; private set; }

    /// <summary>Zeigt die Mitte gerade den Innenraum in 3D?</summary>
    internal bool ZeigtInnen3D => _innen && _innen3DAn;

    /// <summary>Fertig, sobald der neueste Stand gebaut und gezeichnet ist (oder ein Grund/Fehler dasteht).</summary>
    internal Task Innen3DFertig => _innen3DFertig.Task;

    /// <summary>Zeit von der letzten Aenderung bis zum Bild in ms (Messpunkt).</summary>
    internal double Innen3DMs { get; private set; }

    /// <summary>Text unter der 3D-Ansicht (leer = alles gut).</summary>
    internal string Innen3DStatusText => _innen3DStatus.Text ?? "";

    /// <summary>Etagen-Wahl der 3D-Karte (Start-Test).</summary>
    internal ComboBox Innen3DEtage => _innen3DEtage;

    /// <summary>Tag oder Nacht (Start-Test, Fotos).</summary>
    internal bool Innen3DNacht
    {
        get => _innen3DNacht.IsChecked == true;
        set => _innen3DNacht.IsChecked = value;
    }

    /// <summary>Schalter „Figur laufen lassen“ (Start-Test).</summary>
    internal bool Innen3DFigurLaufen
    {
        get => _innen3DFigur.IsChecked == true;
        set => _innen3DFigur.IsChecked = value;
    }

    private bool Innen3DAktuell => _innen3DGebaut == _innen3DWunsch && _innen3DGebautOrt == _ort && _innen3DGebautNacht == Innen3DNacht;

    /// <summary>Knopf fuer die Kamera-Karte des Grundrisses und die Mitte (in <see cref="InnenBauen"/>).</summary>
    private Control Innen3DMitteBauen(Control grundriss, WrapPanel zoom)
    {
        _grundrissMitte = grundriss;
        _innenMitte.Content = grundriss;
        var knopf = Schrift.Knopf(Texte.Innen3DKnopf).Vorlese(Texte.Innen3DKnopf).Mit(Texte.Innen3DTipp);
        knopf.Click += (_, _) => Innen3DZeigen(true);
        Innen3DKnopf = knopf;
        zoom.Children.Add(knopf);
        return _innenMitte;
    }

    private Control Innen3DFlaecheBauen()
    {
        Innen3DFlaeche.Vorlese(Texte.Innen3DKnopf);
        Innen3DFlaeche.BildGezeichnet += Innen3DBildGezeichnet;
        var zurueck = Schrift.Knopf(Texte.Innen3DZurueck).Vorlese(Texte.Innen3DZurueck).Mit(Texte.Innen3DZurueckTipp);
        zurueck.Click += (_, _) => Innen3DZeigen(false);
        Innen3DZurueckKnopf = zurueck;
        _innen3DNacht.Vorlese(Texte.Nacht).Mit(Texte.Innen3DNachtTipp);
        _innen3DNacht.IsCheckedChanged += (_, _) =>
        {
            _innen3DAenderung = Stopwatch.GetTimestamp();
            Innen3DAktualisieren();
        };
        _innen3DFigur.Vorlese(Texte.FigurLaufen).Mit(Texte.Innen3DFigurTipp);
        _innen3DFigur.IsCheckedChanged += (_, _) => Innen3DFlaeche.OhneGehen = _innen3DFigur.IsChecked != true;
        _innen3DEtage.Vorlese(Texte.Innen3DEtage).Mit(Texte.Innen3DEtageTipp);
        _innen3DEtage.SelectionChanged += (_, _) =>
        {
            if (!_innen3DEtageFuellt && _innen3DEtage.SelectedItem is ComboBoxItem { Tag: string ziel } && ziel != _ort)
            {
                OrtOeffnen(ziel);
            }
        };
        _innen3DEtageZeile = new StackPanel { Spacing = 4, Children = { Schrift.Text(Texte.Innen3DEtage, "Klein", FontWeight.Bold).Fuer(_innen3DEtage), _innen3DEtage } };
        Button Klein(string text, string name, Action a)
        {
            var k = Schrift.Knopf(text).Vorlese(name).Mit(Texte.ZoomTipp);
            k.Click += (_, _) => a();
            return k;
        }
        var zoom = new WrapPanel
        {
            Children =
            {
                Klein("+", Texte.Vergroessern, () => { Innen3DFlaeche.AktuelleKamera?.Zoomen(1.25f); Innen3DFlaeche.AnfordernBild(); }),
                Klein("−", Texte.Verkleinern, () => { Innen3DFlaeche.AktuelleKamera?.Zoomen(0.8f); Innen3DFlaeche.AnfordernBild(); }),
                zurueck,
            },
        };
        var karte = Kamerakarte.Bauen(zoom, new WrapPanel { Children = { _innen3DNacht, _innen3DFigur } }, _innen3DEtageZeile,
            Schrift.Text(Texte.Innen3DHinweis, "Klein", farbe: "Pinsel.muted"), _innen3DStatus);
        karte.MaxWidth = 440;
        var rahmen = new Border { Child = Innen3DFlaeche, CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderThickness = new Thickness(1) }
            .Farbe(Border.BorderBrushProperty, "Pinsel.border");
        return Kamerakarte.Ueber(rahmen, karte);
    }

    /// <summary>Mitte umschalten: Innenraum in 3D (true) oder Grundriss.</summary>
    internal void Innen3DZeigen(bool an)
    {
        if (_grundrissMitte is null)
        {
            return;
        }
        _innen3DAn = an;
        if (an)
        {
            Flaeche.SetzenTyp = null;
            _innen3DMitte ??= Innen3DFlaecheBauen();
            _innenMitte.Content = _innen3DMitte;
            _innen3DAenderung = Stopwatch.GetTimestamp();
            Innen3DEtagenFuellen();
            Innen3DAktualisieren();
        }
        else
        {
            _innenMitte.Content = _grundrissMitte;
        }
        Melden(an ? Texte.Innen3DKnopf : Texte.Innen3DZurueck);
    }

    /// <summary>Ein anderer Innenraum oder eine andere Etage ist offen (aus <see cref="OrtOeffnen"/>).</summary>
    private void Innen3DOrtGeaendert()
    {
        if (!ZeigtInnen3D)
        {
            return;
        }
        _innen3DAenderung = Stopwatch.GetTimestamp();
        Innen3DEtagenFuellen();
        Innen3DAktualisieren();
    }

    /// <summary>Etwas am Projekt hat sich geaendert (aus <see cref="ProjektGeaendert"/>).</summary>
    private void Innen3DProjektGeaendert()
    {
        _innen3DWunsch++;
        if (ZeigtInnen3D)
        {
            _innen3DAenderung = Stopwatch.GetTimestamp();
            Innen3DEtagenFuellen();
            Innen3DAktualisieren();
        }
    }

    private void Innen3DEtagenFuellen()
    {
        if (_projekt is null || _ort is not { } ort || _innen3DEtageZeile is null)
        {
            return;
        }
        var basis = Etagen.Teilen(ort).Ort;
        var etagen = _projekt.EtagenVon(basis);
        _innen3DEtageFuellt = true;
        _innen3DEtage.Items.Clear();
        foreach (var e in etagen)
        {
            var item = new ComboBoxItem { Content = _projekt.OrtName(e), Tag = e };
            _innen3DEtage.Items.Add(item);
            if (e == ort)
            {
                _innen3DEtage.SelectedItem = item;
            }
        }
        _innen3DEtageFuellt = false;
        _innen3DEtageZeile.IsVisible = etagen.Count > 1;
    }

    private void Innen3DAktualisieren()
    {
        if (!ZeigtInnen3D || _projekt is null || _daten is null || _ort is not { } ort)
        {
            return;
        }
        Tageslicht.FesteUhr = Innen3DNacht ? 22 * 60 : 12 * 60;
        if (Innen3DAktuell)
        {
            Innen3DFlaeche.AnfordernBild();
            return;
        }
        if (_innen3DFertig.Task.IsCompleted)
        {
            // auch waehrend ein Bau laeuft: wer jetzt wartet, wartet auf den neuesten Stand
            _innen3DFertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        if (_innen3DBaut)
        {
            return;
        }
        var wunsch = _innen3DWunsch;
        var nacht = Innen3DNacht;
        if (Innen3D.Nicht(ort) is { } grund)
        {
            _innen3DStatus.Text = grund;
            Innen3DFlaeche.IsVisible = false;
            (_innen3DGebaut, _innen3DGebautOrt, _innen3DGebautNacht) = (wunsch, ort, nacht);
            _innen3DFertig.TrySetResult();
            return;
        }
        var auftrag = Innen3D.Vorbereiten(_projekt, ort);
        var vorher = _innen3DLetzt;
        _innen3DBaut = true;
        _innen3DStatus.Text = Texte.Innen3DBaut;
        _ = Hintergrund(Task.Run(() => Innen3D.Bauen(auftrag, vorher, _innen3DVorrat))).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            _innen3DBaut = false;
            (_innen3DGebaut, _innen3DGebautOrt, _innen3DGebautNacht) = (wunsch, ort, nacht);
            if (t.IsFaulted)
            {
                var fehler = t.Exception?.InnerException;
                if (fehler is not InvalidDataException)
                {
                    _ordner.FehlerEinmal("Innenraum 3D", t.Exception);
                }
                _innen3DStatus.Text = fehler is InvalidDataException ? fehler.Message : Texte.Innen3DFehler;
                Innen3DFlaeche.IsVisible = false;
                if (Innen3DAktuell)
                {
                    _innen3DFertig.TrySetResult();
                }
            }
            else
            {
                var e = t.Result;
                _innen3DLetzt = e;
                Innen3DFlaeche.IsVisible = true;
                Tageslicht.FesteUhr = Innen3DNacht ? 22 * 60 : 12 * 60;
                if (!ReferenceEquals(Innen3DFlaeche.Spiel, e.Spiel))
                {
                    Innen3DFlaeche.Spiel = e.Spiel;
                }
                e.Spiel.Ansehen(e.Innen, e.Figur.X, e.Figur.Y, e.Figur.Z);
                Innen3DFlaeche.KameraAufFigur();
                _innen3DStatus.Text = "";
                _innen3DWartetAufBild = true;
                DispatcherTimer.RunOnce(Innen3DOhneGrafikPruefen, TimeSpan.FromSeconds(1));
                Innen3DFlaeche.AnfordernBild();
            }
            if (!Innen3DAktuell)
            {
                Innen3DAktualisieren();
            }
        }), TaskScheduler.Default);
    }

    private void Innen3DOhneGrafikPruefen()
    {
        if (ZeigtInnen3D && _innen3DWartetAufBild && !Innen3DFlaeche.GlBereit)
        {
            _innen3DWartetAufBild = false;
            _innen3DStatus.Text = Texte.Ansicht3DOhneGrafik;
            _innen3DFertig.TrySetResult();
        }
    }

    private void Innen3DBildGezeichnet()
    {
        if (!_innen3DWartetAufBild)
        {
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (!_innen3DWartetAufBild || !Innen3DAktuell || _innen3DBaut)
            {
                return;
            }
            _innen3DWartetAufBild = false;
            Innen3DMs = Stopwatch.GetElapsedTime(_innen3DAenderung).TotalMilliseconds;
            Messwerte.Reihe("Innenraum 3D bis zum Bild (Angabe)", Innen3DMs);
            _innen3DFertig.TrySetResult();
        });
    }
}
