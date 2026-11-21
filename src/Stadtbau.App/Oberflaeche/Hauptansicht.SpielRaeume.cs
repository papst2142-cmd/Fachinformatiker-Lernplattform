using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Design;
using Lernplattform.Messung;
using Lernplattform.Spiel;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.60.0 (Weisung P12-337, Nico Antwort 1a aus P12-207): Ansicht „Räume des Spiels“. Links die Wahl des Raums (jede
/// Wohnung, Buero der Bitweiche, eigene Firma auf jeder Stufe, Serverfarm, Zweigstelle, Kunden), in der Mitte der Grundriss
/// (2D) oder der Raum in 3D, rechts die Masse. Alles kommt aus denselben Daten und demselben Bau wie im Spiel
/// (<see cref="SpielRaeume"/>, breite Grundrisse); nur ansehen. Der Knopf „Räume des Spiels ansehen“ steht links in den
/// Innenraeumen unter der Wahl des Orts.
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>Grundriss des offenen Spiel-Raums (eigene Flaeche, gesperrt).</summary>
    internal GrundrissFlaeche SpielRaumFlaeche { get; } = new();

    /// <summary>3D-Flaeche des Spiels-Raums (eigene Flaeche und eigenes Spiel, die Innenraeume der Stadt bleiben unberuehrt).</summary>
    internal StadtFlaeche SpielRaum3DFlaeche { get; } = new() { OhneGehen = true, VerkehrNurNachBedienen = true };

    private Control? _spielRaumAnsicht;
    /// <summary>Liste der Raeume (Gruppen-Ueberschrift, dann je Raum ein Knopf mit Umbruch; eine Auswahlliste schnitt lange Namen ab).</summary>
    private readonly StackPanel _spielRaumWahl = new() { Spacing = 4 };
    private readonly Dictionary<string, RadioButton> _spielRaumKnoepfe = [];
    private readonly ContentControl _spielRaumMitte = new() { Margin = new Thickness(12) };
    private readonly StackPanel _spielRaumMasse = new() { Spacing = 4 };
    private readonly TextBlock _spielRaum3DStatus = Schrift.Text("", "Klein", farbe: "Pinsel.muted");
    private readonly StadtSzene.Vorrat _spielRaumVorrat = new();
    private Control? _spielRaum2DMitte;
    private Control? _spielRaum3DMitte;
    private bool _spielRaumWahlFuellt;
    private bool _spielRaum3DAn;
    private bool _spielRaum3DBaut;
    private bool _spielRaum3DWartetAufBild;
    private string? _spielRaumOffen;
    private string? _spielRaum3DGebaut;
    private Innen3D.Ergebnis? _spielRaum3DLetzt;
    private long _spielRaum3DStart;
    private TaskCompletionSource _spielRaum3DFertig = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Knopf „Räume des Spiels ansehen“ in den Innenraeumen (Start-Test).</summary>
    internal Button? SpielRaeumeKnopf { get; private set; }

    /// <summary>Knoepfe „In 3D ansehen“ und „Zurück zum Grundriss“ der Ansicht (Start-Test).</summary>
    internal Button? SpielRaum3DKnopf { get; private set; }

    internal Button? SpielRaum2DKnopf { get; private set; }

    /// <summary>Zeigt die Mitte gerade die Ansicht „Räume des Spiels“?</summary>
    internal bool ZeigtSpielRaeume => _spielRaumAnsicht is not null && ReferenceEquals(_inhalt.Content, _spielRaumAnsicht);

    /// <summary>Schluessel des offenen Spiel-Raums (null = keiner).</summary>
    internal string? SpielRaumOffen => _spielRaumOffen;

    /// <summary>Zeigt die Ansicht den Raum in 3D?</summary>
    internal bool ZeigtSpielRaum3D => ZeigtSpielRaeume && _spielRaum3DAn;

    /// <summary>Fertig, sobald der offene Raum in 3D gebaut und gezeichnet ist (oder ein Grund dasteht).</summary>
    internal Task SpielRaum3DFertig => _spielRaum3DFertig.Task;

    /// <summary>Text unter der 3D-Ansicht (leer = alles gut).</summary>
    internal string SpielRaum3DStatusText => _spielRaum3DStatus.Text ?? "";

    /// <summary>Der zuletzt in 3D gebaute Raum (Start-Test: Vergleich mit dem Spiel).</summary>
    internal Innenraum? SpielRaum3DInnen => _spielRaum3DLetzt?.Innen;

    /// <summary>Knopf fuer die linke Spalte der Innenraeume.</summary>
    private Button SpielRaeumeKnopfBauen()
    {
        var k = Schrift.Knopf(Texte.SpielRaeumeKnopf).Vorlese(Texte.SpielRaeumeKnopf).Mit(Texte.SpielRaeumeKnopfTipp);
        k.HorizontalAlignment = HorizontalAlignment.Stretch;
        k.Click += (_, _) => SpielRaeumeZeigen(null);
        SpielRaeumeKnopf = k;
        return k;
    }

    /// <summary>Ansicht „Räume des Spiels“ zeigen und den Raum <paramref name="schluessel"/> (null = der zuletzt offene, sonst der erste) oeffnen.</summary>
    internal void SpielRaeumeZeigen(string? schluessel)
    {
        if (_projekt is null || _daten is null)
        {
            return;
        }
        var liste = _projekt.SpielRaeumeListe();
        if (liste.Count == 0)
        {
            return;
        }
        _spielRaumAnsicht ??= SpielRaeumeBauen();
        _spielRaumWahlFuellt = true;
        _spielRaumWahl.Children.Clear();
        _spielRaumKnoepfe.Clear();
        string? gruppe = null;
        foreach (var r in liste)
        {
            if (r.Gruppe != gruppe)
            {
                gruppe = r.Gruppe;
                var kopf = Schrift.Text(gruppe, "Klein", FontWeight.Bold);
                kopf.Margin = new Thickness(0, 6, 0, 0);
                _spielRaumWahl.Children.Add(kopf);
            }
            var name = Schrift.Text(r.Name, "Klein");
            name.TextWrapping = TextWrapping.Wrap;
            var knopf = new RadioButton { Content = name, GroupName = "spielraeume", Tag = r.Schluessel }.Vorlese(r.Gruppe + ": " + r.Name);
            var raum = r.Schluessel;
            knopf.IsCheckedChanged += (_, _) =>
            {
                if (!_spielRaumWahlFuellt && knopf.IsChecked == true && raum != _spielRaumOffen)
                {
                    SpielRaumOeffnen(raum);
                }
            };
            _spielRaumKnoepfe[raum] = knopf;
            _spielRaumWahl.Children.Add(knopf);
        }
        _spielRaumWahlFuellt = false;
        _innen = true;
        _drei = false;
        _geschichteOffen = false;
        _inhalt.Content = _spielRaumAnsicht;
        var ziel = schluessel ?? _spielRaumOffen;
        SpielRaumOeffnen(ziel is not null && liste.Any(r => r.Schluessel == ziel) ? ziel : liste[0].Schluessel);
        KnoepfeFreigeben();
    }

    /// <summary>Einen Raum des Spiels oeffnen (Grundriss und, wenn an, 3D).</summary>
    internal void SpielRaumOeffnen(string schluessel)
    {
        if (_projekt is null || !SpielRaeume.Ist(schluessel))
        {
            return;
        }
        _spielRaumOffen = schluessel;
        _spielRaumWahlFuellt = true;
        foreach (var (s, k) in _spielRaumKnoepfe)
        {
            k.IsChecked = s == schluessel;
        }
        _spielRaumWahlFuellt = false;
        SpielRaumFlaeche.Zeigen(_projekt, schluessel);
        _spielRaumMasse.Children.Clear();
        if (_projekt.SpielRaum(schluessel) is { } innen)
        {
            foreach (var zeile in SpielRaeume.Masse(innen.Gebaeude))
            {
                var t = Schrift.Text(zeile, "Klein");
                t.TextWrapping = TextWrapping.Wrap;
                _spielRaumMasse.Children.Add(t);
            }
        }
        else
        {
            _spielRaumMasse.Children.Add(Hinweis(Texte.SpielRaumFehlt));
        }
        SpielRaum3DAktualisieren();
        Melden(Texte.SpielRaumGeoeffnet(_projekt.SpielRaumName(schluessel)));
    }

    private Control SpielRaeumeBauen()
    {
        var zurueck = Schrift.Knopf(Texte.SpielRaeumeZurueck).Vorlese(Texte.SpielRaeumeZurueck).Mit(Texte.SpielRaeumeZurueckTipp);
        zurueck.HorizontalAlignment = HorizontalAlignment.Stretch;
        zurueck.Click += (_, _) => ReiterZeigen(true);
        var links = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Schrift.Text(Texte.SpielRaeumeTitel, "Mittel", FontWeight.Bold),
                Hinweis(Texte.SpielRaeumeErklaerung),
                zurueck,
                _spielRaumWahl,
            },
        };
        var linksRollen = new ScrollViewer { Content = Schrift.Karte(links), Margin = new Thickness(12, 12, 0, 12) };

        // Mitte 2D: der gesperrte Grundriss mit Kamera-Karte (Zoom, ganzer Raum, In 3D ansehen)
        SpielRaumFlaeche.Vorlese(Texte.SpielRaumGrundriss);
        Button Klein(string text, string name, string tipp, Action a)
        {
            var k = Schrift.Knopf(text).Vorlese(name).Mit(tipp);
            k.Click += (_, _) => a();
            return k;
        }
        var drei = Klein(Texte.SpielRaum3DKnopf, Texte.SpielRaum3DKnopf, Texte.SpielRaum3DTipp, () => SpielRaum3DZeigen(true));
        SpielRaum3DKnopf = drei;
        var karte2D = Kamerakarte.Bauen(new WrapPanel
        {
            Children =
            {
                Klein("+", Texte.Vergroessern, Texte.ZoomTipp, SpielRaumFlaeche.Vergroessern),
                Klein("−", Texte.Verkleinern, Texte.ZoomTipp, SpielRaumFlaeche.Verkleinern),
                Klein(Texte.GanzenRaumZeigen, Texte.GanzenRaumZeigen, Texte.GanzenRaumZeigenTipp, SpielRaumFlaeche.Einpassen),
                drei,
            },
        });
        karte2D.MaxWidth = 440;
        var rahmen2D = new Border { Child = SpielRaumFlaeche, CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderThickness = new Thickness(1) }
            .Farbe(Border.BorderBrushProperty, "Pinsel.border");
        _spielRaum2DMitte = Kamerakarte.Ueber(rahmen2D, karte2D, h => SpielRaumFlaeche.UntenFrei = h);

        // Mitte 3D: eigene Flaeche, Kamera wie im Spiel (ziehen, Mausrad, zwei Finger), immer hell
        SpielRaum3DFlaeche.Vorlese(Texte.SpielRaum3DKnopf);
        SpielRaum3DFlaeche.BildGezeichnet += SpielRaum3DBildGezeichnet;
        var zwei = Klein(Texte.Innen3DZurueck, Texte.Innen3DZurueck, Texte.Innen3DZurueckTipp, () => SpielRaum3DZeigen(false));
        SpielRaum2DKnopf = zwei;
        var karte3D = Kamerakarte.Bauen(new WrapPanel
        {
            Children =
            {
                Klein("+", Texte.Vergroessern, Texte.ZoomTipp, () => { SpielRaum3DFlaeche.AktuelleKamera?.Zoomen(1.25f); SpielRaum3DFlaeche.AnfordernBild(); }),
                Klein("−", Texte.Verkleinern, Texte.ZoomTipp, () => { SpielRaum3DFlaeche.AktuelleKamera?.Zoomen(0.8f); SpielRaum3DFlaeche.AnfordernBild(); }),
                zwei,
            },
        }, Schrift.Text(Texte.Innen3DImmerHell, "Klein", farbe: "Pinsel.muted"), _spielRaum3DStatus);
        karte3D.MaxWidth = 440;
        var rahmen3D = new Border { Child = SpielRaum3DFlaeche, CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderThickness = new Thickness(1) }
            .Farbe(Border.BorderBrushProperty, "Pinsel.border");
        _spielRaum3DMitte = Kamerakarte.Ueber(rahmen3D, karte3D);
        _spielRaumMitte.Content = _spielRaum2DMitte;

        var rechts = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.SpielRaumNurAnsehen, "Mittel", FontWeight.Bold), Hinweis(Texte.SpielRaeumeErklaerung) } }),
                Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.SpielRaumMasse, "Mittel", FontWeight.Bold), _spielRaumMasse } }),
            },
        };
        var rechtsRollen = new ScrollViewer { Content = new Border { Child = rechts, Padding = new Thickness(0, 0, Dialog.RandFuerLeiste, 0) }, Margin = new Thickness(0, 12, 12, 12) };

        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*,340") };
        g.Children.Add(linksRollen);
        Grid.SetColumn(_spielRaumMitte, 1);
        g.Children.Add(_spielRaumMitte);
        Grid.SetColumn(rechtsRollen, 2);
        g.Children.Add(rechtsRollen);
        return g;
    }

    /// <summary>Mitte umschalten: Raum in 3D (true) oder Grundriss.</summary>
    internal void SpielRaum3DZeigen(bool an)
    {
        if (_spielRaum2DMitte is null || _spielRaum3DMitte is null)
        {
            return;
        }
        _spielRaum3DAn = an;
        _spielRaumMitte.Content = an ? _spielRaum3DMitte : _spielRaum2DMitte;
        SpielRaum3DAktualisieren();
        Melden(an ? Texte.SpielRaum3DKnopf : Texte.Innen3DZurueck);
    }

    private void SpielRaum3DAktualisieren()
    {
        if (!_spielRaum3DAn || _projekt is null || _spielRaumOffen is not { } schluessel)
        {
            return;
        }
        Tageslicht.FesteUhr = 12 * 60;
        if (_spielRaum3DGebaut == schluessel && !_spielRaum3DBaut)
        {
            SpielRaum3DFlaeche.AnfordernBild();
            return;
        }
        if (_spielRaum3DFertig.Task.IsCompleted)
        {
            _spielRaum3DFertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        if (_spielRaum3DBaut)
        {
            return;
        }
        var auftrag = Innen3D.Vorbereiten(_projekt, schluessel);
        var vorher = _spielRaum3DLetzt;
        _spielRaum3DBaut = true;
        _spielRaum3DStart = Stopwatch.GetTimestamp();
        _spielRaum3DStatus.Text = Texte.Innen3DBaut;
        _ = Hintergrund(Task.Run(() => Innen3D.Bauen(auftrag, vorher, _spielRaumVorrat))).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            _spielRaum3DBaut = false;
            _spielRaum3DGebaut = schluessel;
            if (t.IsFaulted)
            {
                var fehler = t.Exception?.InnerException;
                if (fehler is not InvalidDataException)
                {
                    _ordner.FehlerEinmal("Räume des Spiels 3D", t.Exception);
                }
                _spielRaum3DStatus.Text = fehler is InvalidDataException ? fehler.Message : Texte.Innen3DFehler;
                SpielRaum3DFlaeche.IsVisible = false;
                _spielRaum3DFertig.TrySetResult();
            }
            else
            {
                var e = t.Result;
                _spielRaum3DLetzt = e;
                SpielRaum3DFlaeche.IsVisible = true;
                Tageslicht.FesteUhr = 12 * 60;
                if (!ReferenceEquals(SpielRaum3DFlaeche.Spiel, e.Spiel))
                {
                    SpielRaum3DFlaeche.Spiel = e.Spiel;
                }
                e.Spiel.Ansehen(e.Innen, e.Figur.X, e.Figur.Y, e.Figur.Z);
                SpielRaum3DFlaeche.KameraAufFigur();
                _spielRaum3DStatus.Text = "";
                _spielRaum3DWartetAufBild = true;
                DispatcherTimer.RunOnce(SpielRaum3DOhneGrafikPruefen, TimeSpan.FromSeconds(1));
                SpielRaum3DFlaeche.AnfordernBild();
            }
            if (_spielRaumOffen != schluessel)
            {
                // inzwischen ein anderer Raum gewaehlt: der neueste Wunsch gewinnt
                SpielRaum3DAktualisieren();
            }
        }), TaskScheduler.Default);
    }

    private void SpielRaum3DOhneGrafikPruefen()
    {
        if (ZeigtSpielRaum3D && _spielRaum3DWartetAufBild && !SpielRaum3DFlaeche.GlBereit)
        {
            _spielRaum3DWartetAufBild = false;
            _spielRaum3DStatus.Text = Texte.Ansicht3DOhneGrafik;
            _spielRaum3DFertig.TrySetResult();
        }
    }

    private void SpielRaum3DBildGezeichnet()
    {
        if (!_spielRaum3DWartetAufBild)
        {
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (!_spielRaum3DWartetAufBild || _spielRaum3DBaut || _spielRaum3DGebaut != _spielRaumOffen)
            {
                return;
            }
            _spielRaum3DWartetAufBild = false;
            Messwerte.Reihe("Raum des Spiels 3D bis zum Bild (Angabe)", Stopwatch.GetElapsedTime(_spielRaum3DStart).TotalMilliseconds);
            _spielRaum3DFertig.TrySetResult();
        });
    }
}
