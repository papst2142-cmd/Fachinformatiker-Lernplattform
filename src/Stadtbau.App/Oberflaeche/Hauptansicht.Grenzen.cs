using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Messung;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle D2 (Weisung P12-157, Kurzplan P12-97 Punkte b und c, Hinweise P12-156): Leiste „Grenzen live“ unten links in
/// „Stadt von oben“ und in der 3D-Ansicht (neben der Kamera-Karte, bei wenig Platz darueber), nach jeder Aenderung im
/// Hintergrund neu gerechnet (<see cref="GrenzenLive"/>, mit eigenem Vorrat: nur Geaendertes wird neu gebaut). Ueber der
/// Grenze roter Fund „Stadt zu teuer“ (Nico 3a). Knopf „Wärmekarte“ (Taste W) in der Werkzeugleiste, Legende in der Leiste.
/// </summary>
public sealed partial class Hauptansicht
{
    private const double LeisteBreite = 330;

    private readonly Border _grenzLeiste = new()
    {
        Padding = new Thickness(10, 8),
        CornerRadius = new CornerRadius(12),
        BorderThickness = new Thickness(1),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Bottom,
        Width = LeisteBreite,
        Margin = new Thickness(Kamerakarte.Rand),
    };
    private readonly TextBlock _grenzKopf = Schrift.Text(Texte.GrenzenLiveRechnet, "Klein", FontWeight.Bold);
    private readonly ProgressBar _grenzLiveBalken = new() { Minimum = 0, Maximum = 100, Height = 10, MinHeight = 10 };
    private readonly TextBlock _grenzGesamt = Schrift.Text("", "Klein");
    private readonly TextBlock _grenzNah = Schrift.Text("", "Klein");
    private readonly WrapPanel _waermeLegende = new() { IsVisible = false };
    private Button _waermeChip = null!;
    private readonly StadtSzene.Vorrat _grenzVorrat = new();
    private int _grenzStand;
    private StadtbauFund? _grenzFund;
    private List<StadtbauFund> _aussenPruefFunde = [];
    private TaskCompletionSource _grenzenFertig = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Grid? _grenzMitte;

    /// <summary>Die zuletzt gerechneten Grenzen (null bis zum ersten Ergebnis).</summary>
    public GrenzenLive? GrenzenLiveStand { get; private set; }

    /// <summary>Start-Test: fertig, sobald die Leiste das Ergebnis der letzten Aenderung zeigt.</summary>
    internal Task GrenzenLiveFertig => _grenzenFertig.Task;

    internal Border GrenzLeiste => _grenzLeiste;
    internal string GrenzLeisteKopf => _grenzKopf.Text ?? "";
    internal Button WaermeKnopf => _waermeChip;
    internal WrapPanel WaermeLegende => _waermeLegende;

    /// <summary>Leiste und Knopf bauen und ueber die Mitte legen (unten links).</summary>
    private void GrenzLeisteBauen(Grid mitte)
    {
        _grenzMitte = mitte;
        _grenzLiveBalken.Farbe(ProgressBar.ForegroundProperty, "Pinsel.green");
        _waermeLegende.Children.Add(Schrift.Text(Texte.WaermekarteLegende + ":", "Klein", farbe: "Pinsel.muted"));
        for (var i = 0; i < GrenzenLive.WaermeStufen.Length; i++)
        {
            var bis = i + 1 < GrenzenLive.WaermeStufen.Length ? GrenzenLive.WaermeStufen[i + 1] - 1 : (long?)null;
            _waermeLegende.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Margin = new Thickness(0, 2, 8, 0),
                Children =
                {
                    new Border { Width = 12, Height = 12, Background = new SolidColorBrush(Color.Parse(GrenzenLive.WaermeFarben[i]), 0.85), VerticalAlignment = VerticalAlignment.Center },
                    Schrift.Text(Texte.WaermekarteStufe(GrenzenLive.WaermeStufen[i], bis), "Klein"),
                },
            });
        }
        _grenzLeiste.Child = new StackPanel { Spacing = 4, Children = { _grenzKopf, _grenzLiveBalken, _grenzGesamt, _grenzNah, _waermeLegende } };
        _grenzLeiste.Farbe(Border.BackgroundProperty, "Pinsel.card").Farbe(Border.BorderBrushProperty, "Pinsel.border");
        _grenzLeiste.Vorlese(Texte.GrenzenLive).Mit(Texte.GrenzenLiveTipp);
        mitte.Children.Add(_grenzLeiste);
        mitte.SizeChanged += (_, _) => GrenzLeisteLegen();
        _kameraAussen.SizeChanged += (_, _) => GrenzLeisteLegen();
        _grenzLeiste.SizeChanged += (_, _) => GrenzLeisteLegen();

        _waermeChip = Chip(Texte.Waermekarte, Texte.WaermekarteTipp, WaermeUmschalten);
        _werkzeugLeiste.Children.Insert(_werkzeugLeiste.Children.IndexOf(_ebenenHinweis), _waermeChip);
    }

    /// <summary>Neben die Kamera-Karte, wenn beide nebeneinander passen, sonst darueber; die Karte haelt unten Platz frei.</summary>
    private void GrenzLeisteLegen()
    {
        if (_grenzMitte is null)
        {
            return;
        }
        var kamera = Kamerakarte.Hoehe(_kameraAussen);
        var daneben = _grenzMitte.Bounds.Width - _kameraAussen.Bounds.Width - (3 * Kamerakarte.Rand) >= LeisteBreite;
        var unten = daneben ? Kamerakarte.Rand : kamera;
        var rand = new Thickness(Kamerakarte.Rand, 0, 0, unten);
        if (_grenzLeiste.Margin != rand)
        {
            _grenzLeiste.Margin = rand;
        }
        var leiste = _grenzLeiste.IsVisible ? _grenzLeiste.Bounds.Height + Kamerakarte.Rand + unten : 0;
        Karte.UntenFrei = Math.Max(kamera, leiste);
    }

    /// <summary>Waermekarte an/aus (Knopf und Taste W, nur „Stadt von oben“).</summary>
    internal void WaermeUmschalten()
    {
        if (_projekt is null || _innen || _geschichteOffen)
        {
            return;
        }
        if (_drei)
        {
            Melden(Texte.WaermekarteTipp);
            return;
        }
        Karte.WaermeAn = !Karte.WaermeAn;
        _waermeLegende.IsVisible = Karte.WaermeAn;
        ChipMarkieren(_waermeChip, Karte.WaermeAn);
        AutomationProperties.SetName(_waermeChip, Karte.WaermeAn ? Texte.Gewaehlt(Texte.Waermekarte) : Texte.Waermekarte);
    }

    /// <summary>Nach jeder Aenderung draussen: im Hintergrund rechnen, der neueste Stand gewinnt.</summary>
    private void GrenzenLiveStarten(StadtFassung fassung, Spieldaten daten)
    {
        var stand = ++_grenzStand;
        if (_grenzenFertig.Task.IsCompleted)
        {
            _grenzenFertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        _grenzKopf.Text = Texte.GrenzenLiveRechnet;
        var uhr = Stopwatch.StartNew();
        _ = Hintergrund(Task.Run(() => GrenzenLive.Fuer(fassung, daten, _grenzVorrat))).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            if (stand != _grenzStand)
            {
                return;
            }
            if (t.IsFaulted)
            {
                _ordner.Fehler("Grenzen live: " + t.Exception?.InnerException);
                GrenzenLiveStand = null;
                _grenzFund = new StadtbauFund("Die Rechnung der Grenzen ist abgebrochen: " + t.Exception?.InnerException?.Message, "Bitte CC Bescheid geben.", true);
            }
            else
            {
                GrenzenLiveStand = t.Result;
                _grenzFund = t.Result.Fund();
            }
            GrenzLeisteZeigen();
            Karte.Waerme = GrenzenLiveStand;
            Messwerte.Reihe("Grenzen live im Hintergrund (Angabe)", uhr.Elapsed.TotalMilliseconds);
            AussenFundeZusammen();
            if (_aussenGeprueft.Task.IsCompleted)
            {
                AussenPruefungZeigen(fertig: true);
            }
            _grenzenFertig.TrySetResult();
        }), TaskScheduler.Default);
    }

    /// <summary>Funde der Pruefung plus „Stadt zu teuer“.</summary>
    private void AussenFundeZusammen() =>
        AussenFunde = _grenzFund is { } f ? [.. _aussenPruefFunde, f] : _aussenPruefFunde;

    private void GrenzLeisteZeigen()
    {
        if (GrenzenLiveStand is not { } g)
        {
            _grenzKopf.Text = Texte.GrenzenLive;
            return;
        }
        var (d, a) = g.Teuerstes;
        var anteil = g.Anteil;
        var kopf = Texte.GrenzenLiveKopf(d, a, StadtbauGrenzen.Dreiecke, StadtbauGrenzen.Aufrufe);
        _grenzKopf.Text = Texte.GrenzenLive + ": " + kopf;
        _grenzLiveBalken.Value = Math.Min(100, anteil * 100);
        _grenzLiveBalken.Farbe(ProgressBar.ForegroundProperty, anteil > 1 ? "Pinsel.red" : anteil >= GrenzenLive.GelbAb ? "Pinsel.yellow" : "Pinsel.green");
        _grenzGesamt.Text = Texte.GrenzenLiveGesamt(g.Gesamt.Dreiecke, g.Gesamt.Aufrufe);
        _grenzNah.Text = Texte.GrenzenLiveNah(g.Nah.Dreiecke, g.Nah.Aufrufe);
        AutomationProperties.SetName(_grenzLeiste, Texte.GrenzenLiveName(kopf));
        AutomationProperties.SetName(_grenzLiveBalken, Texte.GrenzenLive + ": " + kopf);
    }
    /// <summary>
    /// Start-Test (Weisung P12-157): die Zaehlung ohne Grafikkarte gegen das gezeichnete Bild der 3D-Ansicht in der Szene der
    /// Grenzen, Gesamtansicht und Nahansichten an der teuersten Stelle (alle Abstaende, beide Richtungen). Zurueck: Zahl der
    /// verglichenen Bilder und die Abweichungen (leer = alles gleich); null ohne OpenGL oder ohne Instancing.
    /// </summary>
    internal async Task<(int Bilder, List<string> Abweichungen)?> ZaehlungVergleichenAsync()
    {
        if (_projekt is null || !_drei)
        {
            return null;
        }
        var vorher = _einstellung;
        EinstellungSetzen(Ansicht3D.Einstellung.Grenzen);
        await DreiFertig;
        if (!Drei.GlBereit || !Drei.InstancingDa || Drei.AktuelleKamera is not { } kamera || Drei.Spiel is not { } spiel)
        {
            EinstellungSetzen(vorher);
            return null;
        }
        var szene = spiel.Szene;
        var zaehlung = new StadtbauGrenzen.Draussen(szene, szene.Vorbereiten());
        var uhr = Ansicht3D.Einstellung.Grenzen.Uhr;
        var (ax, az, ab, an, ad) = (kamera.ZielX, kamera.ZielZ, kamera.Abstand, kamera.Neigung, kamera.Drehung);
        var abweichungen = new List<string>();
        var bilder = 0;
        var nah = GrenzenLiveStand is { } g ? (g.NahX, g.NahZ) : (szene.Breite / 2, szene.Tiefe / 2);
        var stellen = new List<(float X, float Z, float Ab, float Dreh)> { (szene.Breite / 2, szene.Tiefe / 2, Kamera.AbstandMax, 0) };
        foreach (var abstand in GrenzenLive.NahAbstaende)
        {
            stellen.Add((nah.Item1, nah.Item2, abstand, 0));
            stellen.Add((nah.Item1, nah.Item2, abstand, 180));
        }
        try
        {
            kamera.Neigung = Bauen3D.NeigungSpiel;
            foreach (var (x, z, abstand, dreh) in stellen)
            {
                kamera.Drehung = dreh;
                kamera.Zoomen(kamera.Abstand / abstand);
                kamera.Setzen(x, z);
                await Drei.NaechstesBildAsync();
                await Drei.NaechstesBildAsync();
                var fest = zaehlung.Bild(Drei.LetzteSichtProj, Drei.LetzterAbstand);
                var bewegt = StadtbauGrenzen.Bewegt(spiel, Drei.LetzterAbstand > StadtSzene.FernAb, Lernplattform.Spiel.StadtFlaeche.AutosHoechstens, uhr);
                bilder++;
                if (fest != (Drei.LetzteStadtDreiecke, Drei.LetzteStadtAufrufe)
                    || (fest.Dreiecke + bewegt.Dreiecke, fest.Aufrufe + bewegt.Aufrufe) != (Drei.LetzteDreiecke, Drei.LetzteZeichenaufrufe))
                {
                    abweichungen.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"Abstand {abstand:F0}, Drehung {dreh:F0}, bei {x:F1}/{z:F1}: gezeichnet {Drei.LetzteStadtDreiecke}/{Drei.LetzteStadtAufrufe} (ganz {Drei.LetzteDreiecke}/{Drei.LetzteZeichenaufrufe}), gezaehlt {fest.Dreiecke}/{fest.Aufrufe} (ganz {fest.Dreiecke + bewegt.Dreiecke}/{fest.Aufrufe + bewegt.Aufrufe})"));
                }
            }
        }
        finally
        {
            kamera.Drehung = ad;
            kamera.Setzen(ax, az);
            kamera.Zoomen(kamera.Abstand / ab);
            kamera.Neigung = an;
            EinstellungSetzen(vorher);
        }
        return (bilder, abweichungen);
    }

    /// <summary>Start-Test: eine Taste wie vom Fenster (ohne Fokus in einem Textfeld).</summary>
    internal void TasteFuerTests(Avalonia.Input.Key taste) =>
        TasteGedrueckt(this, new Avalonia.Input.KeyEventArgs { Key = taste, KeyModifiers = Avalonia.Input.KeyModifiers.None, RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent });
}
