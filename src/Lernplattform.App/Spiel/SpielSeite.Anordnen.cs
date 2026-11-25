using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Views;
using Knopf = Lernplattform.Core.Spiel.KnopfAnordnung.Knopf;
using Platz = Lernplattform.Core.Spiel.KnopfAnordnung.Platz;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 1.9.0 (Sammelliste 450, Weisungen P12-375 bis P12-377, Nico 1a 2b 3a 4a 5a+b, Frage 1a): „Knöpfe anordnen“. Am Handy
/// und Tablet lassen sich die sechs runden Knoepfe (Springen, Hinknien, Hinlegen, Handlung, Ich-Sicht, Spiel-Handy) verschieben und
/// je Knopf von 50 bis 150 % vergroessern oder verkleinern (100 % = Mitte = heute); X und Sticks bleiben fest. Hoch und quer haben
/// je eine eigene Anordnung. Der Bearbeiten-Modus liegt ueber dem angehaltenen Spiel (Sticks, Antippen und Tasten gesperrt): oben
/// die Leiste mit Name, Regler, Raster-Schalter, Pfeilen, „Standard“, „Abbrechen“, „Fertig“ (Abnahmebilder P12-240 und P12-241).
/// Ohne eigene Anordnung bleibt alles genau wie in A 1.8.0 (Abnahmesatz A1). Gespeichert wird nur ueber
/// <c>Einstellungen.Aendern</c> (<see cref="KnopfAnordnung.Schluessel"/>, <see cref="KnopfAnordnung.SchluesselRaster"/>).
/// </summary>
public sealed partial class SpielSeite
{
    private Anordnung? _anordnen;
    private (Sticks.Bereich B, (double L, double O, double R, double U) S, object G, Dictionary<Knopf, Platz>? A)? _eigeneCache;
    private bool _passtNichtGezeigt;
    private Border? _passtNichtHinweis;

    private Grid? _anordnenEbene;
    private AnordnenBild? _anordnenBild;
    private Canvas? _anordnenFlaeche;
    private readonly Dictionary<Knopf, DaumenKnopf> _anordnenKnoepfe = [];
    private Border? _anordnenLeiste;
    private TextBlock? _anordnenTitel;
    private TextBlock? _anordnenName;
    private Slider? _anordnenRegler;
    private TextBlock? _anordnenProzent;
    private ToggleSwitch? _anordnenRaster;
    private StackPanel? _anordnenPfeile;
    private Border? _anordnenHinweis;
    private TextBlock? _anordnenHinweisText;
    private bool _anordnenSetzen;
    private string _anordnenSchluessel = "";

    /// <summary>Laeuft der Bearbeiten-Modus „Knöpfe anordnen“?</summary>
    public bool AnordnenAktiv => _anordnen is not null;

    /// <summary>Sicherer Bereich des Geraets (Kerbe, Systemleisten) als links, oben, rechts, unten.</summary>
    private (double L, double O, double R, double U) SicherRand
    {
        get
        {
            var s = _oben?.InsetsManager?.SafeAreaPadding ?? default;
            return (s.Left, s.Top, s.Right, s.Bottom);
        }
    }

    private static (double X, double Y) Mitte(Platz p) => (p.X, p.Y);

    /// <summary>
    /// Wirksame eigene Anordnung fuer die aktuelle Groesse und Ausrichtung (null = Standard wie A 1.8.0). Passt die gemerkte
    /// Anordnung hier nicht, gilt der Standard und einmal je Sitzung erscheint ein Hinweis.
    /// </summary>
    private Dictionary<Knopf, Platz>? EigeneLagen
    {
        get
        {
            var g = KnopfAnordnung.Gespeichert;
            if (g.Count == 0)
            {
                return null;
            }
            var b = Flaeche.GrundBereich;
            if (b.Breite <= 1 || b.Hoehe <= 1)
            {
                return null;
            }
            var s = SicherRand;
            if (_eigeneCache is { } c && c.B == b && c.S == s && ReferenceEquals(c.G, g))
            {
                return c.A;
            }
            var a = KnopfAnordnung.Wirksam(b, s, out var passtNicht);
            _eigeneCache = (b, s, g, a);
            if (passtNicht && !_passtNichtGezeigt)
            {
                _passtNichtGezeigt = true;
                Dispatcher.UIThread.Post(PasstNichtZeigen);
            }
            return a;
        }
    }

    /// <summary>Start-Test: der Hinweis „passt hier nicht“ wurde in dieser Sitzung gezeigt.</summary>
    public bool AnordnenPasstNichtGezeigt => _passtNichtGezeigt;

    private void PasstNichtZeigen()
    {
        if (_passtNichtHinweis is null)
        {
            _passtNichtHinweis = Pille(out var text);
            text.Text = SpielProbeTexte.AnordnenPasstNicht;
            _passtNichtHinweis.HorizontalAlignment = HorizontalAlignment.Center;
            _passtNichtHinweis.VerticalAlignment = VerticalAlignment.Center;
            _passtNichtHinweis.IsHitTestVisible = false;
            Children.Add(_passtNichtHinweis);
        }
        _passtNichtHinweis.IsVisible = true;
        DispatcherTimer.RunOnce(() => _passtNichtHinweis.IsVisible = false, TimeSpan.FromSeconds(4));
    }

    /// <summary>Dunkle Pille mit weissem Text (Hinweis im Bearbeiten-Modus und im Spiel).</summary>
    private static Border Pille(out TextBlock text)
    {
        text = new TextBlock { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center }.Groesse("Klein");
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 20, 28, 36)),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14, 6),
            MaxWidth = 560,
            Child = text,
        };
    }

    // ---------------------------------------------------------------- Sitzung

    /// <summary>Zustand einer Sitzung im Bearbeiten-Modus.</summary>
    private sealed class Anordnung
    {
        public Action? Danach { get; init; }

        public bool TafelWar { get; init; }

        /// <summary>Entwurf je Ausrichtung (null = Standard); nur geaenderte Ausrichtungen werden gespeichert.</summary>
        public Dictionary<string, Dictionary<Knopf, KnopfAnordnung.Gemerkt>?> Entwurf { get; } = [];

        public HashSet<string> Geaendert { get; } = [];

        public string Ausrichtung { get; set; } = "";

        public Sticks.Bereich Bereich { get; set; }

        public (double L, double O, double R, double U) Sicher { get; set; }

        public Dictionary<Knopf, Platz> Lagen { get; set; } = [];

        public Knopf? Gewaehlt { get; set; }

        public long? Finger { get; set; }

        public Point Versatz { get; set; }

        public Platz LetzteGute { get; set; }

        public KnopfAnordnung.Fehler Fehler { get; set; }

        /// <summary>Knopf, der gerade rot ist (Ziehen an eine ungueltige Stelle oder zu gross).</summary>
        public Knopf? Rot { get; set; }

        /// <summary>Grund, warum <see cref="Rot"/> rot ist (Groesse oder Pfeil ohne Ziehen).</summary>
        public KnopfAnordnung.Fehler RotFehler { get; set; }

        public bool Gezogen { get; set; }

        public bool SpeichernFehlgeschlagen { get; set; }
    }

    /// <summary>Start-Test: aktuelle Lagen im Bearbeiten-Modus (Punkte der Spielflaeche), gewaehlter Knopf und Fehler.</summary>
    public (IReadOnlyDictionary<Knopf, Platz> Lagen, Knopf? Gewaehlt, KnopfAnordnung.Fehler Fehler)? AnordnenStand =>
        _anordnen is { } s ? (s.Lagen, s.Gewaehlt, s.Fehler) : null;

    /// <summary>Start-Test: Knoepfe, Regler, Raster-Schalter und Leiste des Bearbeiten-Modus.</summary>
    internal (IReadOnlyDictionary<Knopf, DaumenKnopf> Knoepfe, Slider? Regler, ToggleSwitch? Raster, Border? Leiste, Border? Hinweis) AnordnenTeile =>
        (_anordnenKnoepfe, _anordnenRegler, _anordnenRaster, _anordnenLeiste, _anordnenHinweis);

    /// <summary>
    /// Bearbeiten-Modus oeffnen (Optionen > Steuerung > „Knöpfe anordnen“). <paramref name="danach"/> laeuft nach „Fertig“ und
    /// „Abbrechen“ (zurueck zu den Optionen).
    /// </summary>
    public void AnordnenStarten(Action? danach = null)
    {
        if (_anordnen is not null)
        {
            return;
        }
        AnordnenAnlegen();
        Flaeche.GehTastenLos();
        var s = new Anordnung { Danach = danach, TafelWar = Tafel?.IsVisible ?? false };
        foreach (var (ausrichtung, gemerkt) in KnopfAnordnung.Gespeichert)
        {
            s.Entwurf[ausrichtung] = new Dictionary<Knopf, KnopfAnordnung.Gemerkt>(gemerkt);
        }
        _anordnen = s;
        if (Tafel is { IsVisible: true } tafel)
        {
            tafel.IsVisible = false;
        }
        if (_auswahl is { IsVisible: true })
        {
            AuswahlZu();
        }
        _anordnenSchluessel = "";
        _anordnenEbene!.IsVisible = true;
        DaumenSetzen();
        AnordnenLegen();
        _anordnenRegler?.Focus();
    }

    /// <summary>„Fertig“ (speichern = true) oder „Abbrechen“; false = Speichern fehlgeschlagen, der Modus bleibt offen.</summary>
    public bool AnordnenEnde(bool speichern)
    {
        if (_anordnen is not { } s)
        {
            return true;
        }
        if (speichern)
        {
            s.Entwurf[s.Ausrichtung] = Merken(s);
            s.Geaendert.Add(s.Ausrichtung);
            if (!Programm.Zustand.Aendere(werte =>
                {
                    foreach (var ausrichtung in s.Geaendert)
                    {
                        KnopfAnordnung.Wert(werte, ausrichtung, s.Entwurf.GetValueOrDefault(ausrichtung));
                    }
                }))
            {
                // nicht gespeichert (Datei gesperrt, nur Anzeige): nichts geht still verloren, der Modus bleibt offen
                s.SpeichernFehlgeschlagen = true;
                _anordnenSchluessel = "";
                AnordnenLegen();
                return false;
            }
            KnopfAnordnung.Uebernehmen(Programm.Zustand.Werte);
        }
        _anordnen = null;
        _eigeneCache = null;
        _passtNichtGezeigt = false;
        _anordnenEbene!.IsVisible = false;
        if (s.TafelWar && Tafel is { } tafel)
        {
            tafel.Zeichnen();
        }
        DaumenSetzen();
        Flaeche.AnfordernBild();
        s.Danach?.Invoke();
        return true;
    }

    /// <summary>Lagen der aktuellen Ausrichtung zum Speichern (null = Standard oder nicht alle sechs Knoepfe da).</summary>
    private static Dictionary<Knopf, KnopfAnordnung.Gemerkt>? Merken(Anordnung s)
    {
        if (!KnopfAnordnung.Alle.All(s.Lagen.ContainsKey) || KnopfAnordnung.IstStandard(s.Lagen, s.Bereich))
        {
            return null;
        }
        return KnopfAnordnung.Alle.ToDictionary(k => k, k => KnopfAnordnung.Merken(s.Lagen[k], s.Bereich, s.Sicher));
    }

    /// <summary>„Standard“: nach einer Rueckfrage alle Knoepfe dieser Ausrichtung zurueck (Lage und Groesse).</summary>
    public async Task AnordnenStandardAsync()
    {
        if (_anordnen is not { } s)
        {
            return;
        }
        var quer = s.Ausrichtung == "quer";
        var wahl = await Dialog.Zeigen(SpielProbeTexte.AnordnenStandardTitel,
            string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.AnordnenStandardFrage,
                quer ? SpielProbeTexte.Querformat : SpielProbeTexte.Hochformat, quer ? SpielProbeTexte.Hochformat : SpielProbeTexte.Querformat),
            SpielProbeTexte.AnordnenZuruecksetzen, SpielProbeTexte.AnordnenAbbrechen);
        if (wahl == 0)
        {
            AnordnenStandardSetzen();
        }
    }

    /// <summary>Standard der aktuellen Ausrichtung setzen (ohne Rueckfrage; Start-Test).</summary>
    public void AnordnenStandardSetzen()
    {
        if (_anordnen is not { } s)
        {
            return;
        }
        s.Lagen = KnopfAnordnung.Standard(s.Bereich);
        s.Entwurf[s.Ausrichtung] = null;
        s.Geaendert.Add(s.Ausrichtung);
        s.Gewaehlt = null;
        s.Rot = null;
        s.Fehler = KnopfAnordnung.Fehler.Keiner;
        AnordnenLegen();
    }

    // ---------------------------------------------------------------- Bedienung

    /// <summary>Knopf waehlen (antippen oder Screenreader).</summary>
    public void AnordnenWaehlen(Knopf? k)
    {
        if (_anordnen is not { } s)
        {
            return;
        }
        s.Gewaehlt = k is { } w && s.Lagen.ContainsKey(w) ? w : null;
        s.Rot = null;
        AnordnenLegen();
    }

    /// <summary>Ziehen beginnen: Finger an (x, y) in Punkten der Spielflaeche; true = ein Knopf liegt dort.</summary>
    public bool AnordnenDruecken(long finger, Point p)
    {
        if (_anordnen is not { } s)
        {
            return false;
        }
        Knopf? treffer = null;
        var bester = double.MaxValue;
        foreach (var (k, platz) in s.Lagen)
        {
            var t = KnopfAnordnung.Treffer(KnopfAnordnung.Durchmesser(k, s.Bereich, platz.Prozent)) / 2;
            var weg = Math.Sqrt(((p.X - platz.X) * (p.X - platz.X)) + ((p.Y - platz.Y) * (p.Y - platz.Y)));
            if (weg <= t && weg < bester)
            {
                bester = weg;
                treffer = k;
            }
        }
        s.Rot = null;
        s.SpeichernFehlgeschlagen = false;
        if (treffer is not { } knopf)
        {
            s.Gewaehlt = null;
            AnordnenLegen();
            return false;
        }
        s.Gewaehlt = knopf;
        s.Finger = finger;
        var mitte = s.Lagen[knopf];
        s.Versatz = new Point(p.X - mitte.X, p.Y - mitte.Y);
        s.LetzteGute = mitte;
        s.Gezogen = false;
        s.Fehler = KnopfAnordnung.Fehler.Keiner;
        AnordnenLegen();
        return true;
    }

    /// <summary>Finger bewegt (Punkte der Spielflaeche): der Knopf folgt, rastet ein (Raster an) und wird rot an ungueltiger Stelle.</summary>
    public void AnordnenZiehen(long finger, Point p)
    {
        if (_anordnen is not { Finger: { } f, Gewaehlt: { } k } s || f != finger)
        {
            return;
        }
        var wunsch = new Platz(p.X - s.Versatz.X, p.Y - s.Versatz.Y, s.Lagen[k].Prozent);
        if (KnopfAnordnung.RasterAn)
        {
            wunsch = KnopfAnordnung.Einrasten(wunsch);
        }
        s.Fehler = KnopfAnordnung.PruefenEiner(s.Lagen, k, wunsch, s.Bereich, s.Sicher);
        s.Lagen[k] = wunsch;
        s.Rot = s.Fehler == KnopfAnordnung.Fehler.Keiner ? null : k;
        if (s.Fehler == KnopfAnordnung.Fehler.Keiner)
        {
            s.LetzteGute = wunsch;
        }
        s.Gezogen = true;
        AnordnenLegen();
    }

    /// <summary>Finger los: an ungueltiger Stelle springt der Knopf an die letzte erlaubte zurueck.</summary>
    public void AnordnenLoslassen(long finger)
    {
        if (_anordnen is not { Finger: { } f } s || f != finger)
        {
            return;
        }
        if (s.Gewaehlt is { } k && s.Fehler != KnopfAnordnung.Fehler.Keiner)
        {
            s.Lagen[k] = s.LetzteGute;
        }
        if (s.Gezogen)
        {
            s.Geaendert.Add(s.Ausrichtung);
        }
        s.Finger = null;
        s.Fehler = KnopfAnordnung.Fehler.Keiner;
        s.Rot = null;
        AnordnenLegen();
    }

    /// <summary>
    /// Groesse des gewaehlten Knopfs setzen (50 bis 150 %). Ragt er dann ueber den Rand, rueckt er nach innen; wuerde er einen
    /// anderen Knopf beruehren, wird er rot und bleibt bei der letzten passenden Groesse. true = gesetzt.
    /// </summary>
    public bool AnordnenGroesse(int prozent)
    {
        if (_anordnen is not { Gewaehlt: { } k } s)
        {
            return false;
        }
        var neu = s.Lagen[k] with { Prozent = KnopfAnordnung.GroesseBegrenzen(prozent) };
        var fehler = KnopfAnordnung.PruefenEiner(s.Lagen, k, neu, s.Bereich, s.Sicher);
        if (fehler == KnopfAnordnung.Fehler.Aussen)
        {
            var t = KnopfAnordnung.Treffer(KnopfAnordnung.Durchmesser(k, s.Bereich, neu.Prozent)) / 2;
            var (x1, y1, x2, y2) = KnopfAnordnung.Erlaubt(s.Bereich, s.Sicher);
            if (x2 - x1 >= 2 * t && y2 - y1 >= 2 * t)
            {
                neu = neu with { X = Math.Clamp(neu.X, x1 + t, x2 - t), Y = Math.Clamp(neu.Y, y1 + t, y2 - t) };
                fehler = KnopfAnordnung.PruefenEiner(s.Lagen, k, neu, s.Bereich, s.Sicher);
            }
        }
        if (fehler != KnopfAnordnung.Fehler.Keiner)
        {
            s.Rot = k;
            s.RotFehler = fehler;
            AnordnenLegen();
            return false;
        }
        s.Lagen[k] = neu;
        s.Rot = null;
        s.Geaendert.Add(s.Ausrichtung);
        AnordnenLegen();
        return true;
    }

    /// <summary>Pfeil-Knopf: den gewaehlten Knopf um einen Rasterschritt verschieben (nur an eine erlaubte Stelle). true = verschoben.</summary>
    public bool AnordnenSchritt(int dx, int dy)
    {
        if (_anordnen is not { Gewaehlt: { } k } s)
        {
            return false;
        }
        var p = s.Lagen[k];
        var neu = p with { X = p.X + (dx * KnopfAnordnung.Raster), Y = p.Y + (dy * KnopfAnordnung.Raster) };
        if (KnopfAnordnung.RasterAn)
        {
            neu = KnopfAnordnung.Einrasten(neu);
        }
        var fehler = KnopfAnordnung.PruefenEiner(s.Lagen, k, neu, s.Bereich, s.Sicher);
        if (fehler != KnopfAnordnung.Fehler.Keiner)
        {
            s.Rot = k;
            s.RotFehler = fehler;
            AnordnenLegen();
            return false;
        }
        s.Lagen[k] = neu;
        s.Rot = null;
        s.Geaendert.Add(s.Ausrichtung);
        AnordnenLegen();
        return true;
    }

    /// <summary>Raster-Schalter: wirkt sofort und wird gleich gemerkt; false = nicht gespeichert (Schalter zeigt wieder, was gilt).</summary>
    public bool AnordnenRasterSetzen(bool an)
    {
        var ok = Programm.Zustand.Aendere(werte => KnopfAnordnung.RasterWert(werte, an));
        if (ok)
        {
            KnopfAnordnung.RasterAn = an;
        }
        if (_anordnenRaster is { } schalter && schalter.IsChecked != KnopfAnordnung.RasterAn)
        {
            _anordnenSetzen = true;
            schalter.IsChecked = KnopfAnordnung.RasterAn;
            _anordnenSetzen = false;
        }
        if (_anordnen is { } s)
        {
            s.SpeichernFehlgeschlagen = !ok;
        }
        AnordnenLegen();
        return ok;
    }

    // ---------------------------------------------------------------- Aufbau

    private void AnordnenAnlegen()
    {
        if (_anordnenEbene is not null)
        {
            return;
        }
        _anordnenBild = new AnordnenBild { IsHitTestVisible = false };
        _anordnenFlaeche = new Canvas();
        var ebene = new Grid { Background = Brushes.Transparent, IsVisible = false, ClipToBounds = true };
        ebene.Children.Add(_anordnenBild);
        ebene.Children.Add(_anordnenFlaeche);
        var symbole = new Dictionary<Knopf, DaumenSymbol>
        {
            [Knopf.Springen] = DaumenSymbol.Springen,
            [Knopf.Knien] = DaumenSymbol.Knien,
            [Knopf.Liegen] = DaumenSymbol.Liegen,
            [Knopf.Handlung] = DaumenSymbol.Hand,
            [Knopf.Ich] = DaumenSymbol.Auge,
            [Knopf.Telefon] = DaumenSymbol.Telefon,
        };
        foreach (var k in KnopfAnordnung.Alle)
        {
            // nur Anzeige: gezogen wird auf der Ebene; der Screenreader waehlt den Knopf (danach die Pfeile oben)
            var knopf = new DaumenKnopf(symbole[k]) { IsHitTestVisible = false };
            var kk = k;
            knopf.Getippt += () => AnordnenWaehlen(kk);
            _anordnenKnoepfe[k] = knopf;
            _anordnenFlaeche.Children.Add(knopf);
        }
        _anordnenLeiste = LeisteBauen();
        _anordnenFlaeche.Children.Add(_anordnenLeiste);
        _anordnenHinweis = Pille(out var hinweis);
        _anordnenHinweis.IsHitTestVisible = false;
        _anordnenHinweisText = hinweis;
        _anordnenFlaeche.Children.Add(_anordnenHinweis);
        ebene.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (LiegtAufLeiste(e))
            {
                return;
            }
            e.Handled = true;
            if (AnordnenDruecken(e.Pointer.Id, e.GetPosition(Flaeche)))
            {
                e.Pointer.Capture(ebene);
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ebene.AddHandler(PointerMovedEvent, (_, e) =>
        {
            if (_anordnen is { Finger: { } f } && f == e.Pointer.Id)
            {
                e.Handled = true;
                AnordnenZiehen(e.Pointer.Id, e.GetPosition(Flaeche));
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ebene.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (_anordnen is { Finger: { } f } && f == e.Pointer.Id)
            {
                e.Handled = true;
                e.Pointer.Capture(null);
                AnordnenLoslassen(e.Pointer.Id);
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ebene.PointerCaptureLost += (_, e) => AnordnenLoslassen(e.Pointer.Id);
        _anordnenEbene = ebene;
        Children.Add(ebene);
    }

    private bool LiegtAufLeiste(PointerEventArgs e) =>
        _anordnenLeiste is { IsVisible: true } leiste && e.GetPosition(leiste) is var p && p.X >= 0 && p.Y >= 0
        && p.X <= leiste.Bounds.Width && p.Y <= leiste.Bounds.Height;

    private Border LeisteBauen()
    {
        _anordnenTitel = Schrift.Text(SpielProbeTexte.KnoepfeAnordnen, "Normal", FontWeight.Bold);
        _anordnenTitel.VerticalAlignment = VerticalAlignment.Center;
        _anordnenName = Schrift.Text(SpielProbeTexte.AnordnenTippen, "Normal", FontWeight.Bold, "Pinsel.muted");
        _anordnenName.VerticalAlignment = VerticalAlignment.Center;
        _anordnenRegler = new Slider
        {
            Minimum = KnopfAnordnung.GroesseMin,
            Maximum = KnopfAnordnung.GroesseMax,
            SmallChange = KnopfAnordnung.GroesseSchritt,
            LargeChange = 2 * KnopfAnordnung.GroesseSchritt,
            TickFrequency = KnopfAnordnung.GroesseSchritt,
            IsSnapToTickEnabled = true,
            Value = KnopfAnordnung.GroesseVorgabe,
            Width = 160,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = false,
        };
        Avalonia.Automation.AutomationProperties.SetName(_anordnenRegler, string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.AnordnenGroesse, ""));
        _anordnenRegler.ValueChanged += (_, e) =>
        {
            if (_anordnenSetzen)
            {
                return;
            }
            // zu gross fuer diese Stelle: AnordnenLegen setzt den Regler auf die letzte passende Groesse zurueck
            AnordnenGroesse((int)Math.Round(e.NewValue));
        };
        _anordnenProzent = Schrift.Text("–", "Normal", FontWeight.Normal, "Pinsel.text");
        _anordnenProzent.VerticalAlignment = VerticalAlignment.Center;
        _anordnenProzent.MinWidth = 48 * Schrift.Faktor;
        _anordnenRaster = new ToggleSwitch
        {
            Content = Schrift.Text(SpielProbeTexte.AnordnenRaster),
            IsChecked = KnopfAnordnung.RasterAn,
            OnContent = null,
            OffContent = null,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Avalonia.Automation.AutomationProperties.SetName(_anordnenRaster, SpielProbeTexte.AnordnenRaster);
        _anordnenRaster.IsCheckedChanged += (_, _) =>
        {
            if (!_anordnenSetzen)
            {
                AnordnenRasterSetzen(_anordnenRaster.IsChecked == true);
            }
        };
        _anordnenPfeile = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
        foreach (var (zeichen, dx, dy) in new[] { ("←", -1, 0), ("→", 1, 0), ("↑", 0, -1), ("↓", 0, 1) })
        {
            var pfeil = KleinerKnopf(zeichen, false);
            pfeil.Click += (_, _) => AnordnenSchritt(dx, dy);
            _anordnenPfeile.Children.Add(pfeil);
        }
        var standard = KleinerKnopf(SpielProbeTexte.AnordnenStandard, false);
        standard.Click += (_, _) => _ = AnordnenStandardAsync();
        var abbrechen = KleinerKnopf(SpielProbeTexte.AnordnenAbbrechen, false);
        abbrechen.Click += (_, _) => AnordnenEnde(false);
        var fertig = KleinerKnopf(SpielProbeTexte.AnordnenFertig, true);
        fertig.Click += (_, _) => AnordnenEnde(true);
        var links = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 12, LineSpacing = 4, VerticalAlignment = VerticalAlignment.Center };
        links.Children.Add(_anordnenTitel);
        links.Children.Add(_anordnenName);
        links.Children.Add(_anordnenRegler);
        links.Children.Add(_anordnenProzent);
        links.Children.Add(_anordnenRaster);
        links.Children.Add(_anordnenPfeile);
        var rechts = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 4, VerticalAlignment = VerticalAlignment.Center };
        rechts.Children.Add(standard);
        rechts.Children.Add(abbrechen);
        rechts.Children.Add(fertig);
        var zeile = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(rechts, Dock.Right);
        zeile.Children.Add(rechts);
        zeile.Children.Add(links);
        var leiste = new Border
        {
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12, 6),
            Child = zeile,
        };
        return leiste.Farbe(Border.BackgroundProperty, "Pinsel.card");
    }

    /// <summary>Kleiner Knopf der Leiste (Pille wie <see cref="Schrift.Knopf"/>, aber flacher, damit die Leiste in die Kopfzeile passt).</summary>
    private static Button KleinerKnopf(string text, bool betont)
    {
        var knopf = Schrift.Knopf(text, betont);
        knopf.Margin = default;
        knopf.Padding = new Thickness(12, 2);
        knopf.Bind(MinHeightProperty, knopf.GetResourceObservable("Schrift.Normal", v => v is double d ? 32 * d / 14 : 32));
        Avalonia.Automation.AutomationProperties.SetName(knopf, text);
        return knopf;
    }

    // ---------------------------------------------------------------- Legen und Zeichnen

    /// <summary>Alles im Bearbeiten-Modus an seine Stelle (nur bei einer Aenderung; die Seite ruft es nach jedem Layout).</summary>
    private void AnordnenLegen()
    {
        if (_anordnen is not { } s || _anordnenFlaeche is null || _anordnenBild is null || _anordnenLeiste is null)
        {
            return;
        }
        var b = Flaeche.GrundBereich;
        if (b.Breite <= 1 || b.Hoehe <= 1 || Flaeche.TranslatePoint(default, this) is not { } f0)
        {
            return;
        }
        var sicher = SicherRand;
        var ausrichtung = KnopfAnordnung.Ausrichtung(b);
        if (ausrichtung != s.Ausrichtung)
        {
            if (s.Ausrichtung.Length > 0)
            {
                // gedreht: diese Ausrichtung merken, die andere zeigen (je Ausrichtung eigene Anordnung, Nico 3a)
                s.Entwurf[s.Ausrichtung] = Merken(s);
            }
            s.Ausrichtung = ausrichtung;
            s.Lagen = (s.Entwurf.GetValueOrDefault(ausrichtung) is { } g ? KnopfAnordnung.Einpassen(g, b, sicher, out _) : null)
                ?? KnopfAnordnung.Standard(b);
            s.Gewaehlt = null;
            s.Finger = null;
            s.Rot = null;
        }
        else if (b != s.Bereich || sicher != s.Sicher)
        {
            // andere Groesse in derselben Ausrichtung (Leiste auf- oder zugeklappt, Schrift): einpassen
            var g = Merken(s);
            s.Lagen = (g is not null ? KnopfAnordnung.Einpassen(g, b, sicher, out _) : null) ?? KnopfAnordnung.Standard(b);
        }
        s.Bereich = b;
        s.Sicher = sicher;
        var schluessel = string.Join(";", s.Lagen.Select(e => $"{e.Key}:{e.Value.X:0.##},{e.Value.Y:0.##},{e.Value.Prozent}"))
            + $"|{b}|{sicher}|{s.Gewaehlt}|{s.Fehler}|{s.Rot}|{s.RotFehler}|{KnopfAnordnung.RasterAn}|{s.SpeichernFehlgeschlagen}|{Bounds.Width}x{Bounds.Height}|{Schrift.Faktor}|{f0}";
        if (schluessel == _anordnenSchluessel)
        {
            return;
        }
        _anordnenSchluessel = schluessel;
        var namen = new Dictionary<Knopf, string>
        {
            [Knopf.Springen] = SpielProbeTexte.KnopfSpringen,
            [Knopf.Knien] = SpielProbeTexte.KnopfHinknien,
            [Knopf.Liegen] = SpielProbeTexte.KnopfHinlegen,
            [Knopf.Handlung] = SpielProbeTexte.KnopfHandlung,
            [Knopf.Ich] = SpielProbeTexte.IchSichtKnopf,
            [Knopf.Telefon] = SpielProbeTexte.KnopfTelefon,
        };
        // Knoepfe
        foreach (var (k, knopf) in _anordnenKnoepfe)
        {
            if (!s.Lagen.TryGetValue(k, out var p))
            {
                knopf.IsVisible = false;
                continue;
            }
            knopf.IsVisible = true;
            var d = KnopfAnordnung.Durchmesser(k, b, p.Prozent);
            var scheibe = k is Knopf.Springen or Knopf.Knien or Knopf.Liegen;
            knopf.Setzen(d, knopf.Symbol, false, string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.AnordnenVerschieben, namen[k]),
                scheibe, KnopfAnordnung.Treffer(d));
            var t = knopf.Width;
            Canvas.SetLeft(knopf, f0.X + p.X - (t / 2));
            Canvas.SetTop(knopf, f0.Y + p.Y - (t / 2));
            knopf.Arrange(new Rect(Canvas.GetLeft(knopf), Canvas.GetTop(knopf), t, t));
        }
        // Leiste oben (statt der Kopfzeile)
        _anordnenSetzen = true;
        try
        {
            var gewaehlt = s.Gewaehlt is { } gw ? namen[gw] : null;
            _anordnenTitel!.IsVisible = b.Breite >= 1000;
            _anordnenName!.Text = gewaehlt is null ? SpielProbeTexte.AnordnenTippen : gewaehlt + ":";
            _anordnenName.Farbe(TextBlock.ForegroundProperty, gewaehlt is null ? "Pinsel.muted" : "Pinsel.accent");
            _anordnenRegler!.Width = b.Breite >= 1000 ? 200 : 120;
            _anordnenRegler.IsEnabled = gewaehlt is not null;
            _anordnenRegler.Value = s.Gewaehlt is { } gk ? s.Lagen[gk].Prozent : KnopfAnordnung.GroesseVorgabe;
            Avalonia.Automation.AutomationProperties.SetName(_anordnenRegler,
                string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.AnordnenGroesse, gewaehlt ?? "").TrimEnd());
            _anordnenProzent!.Text = s.Gewaehlt is { } gp ? KnopfAnordnung.ProzentText(s.Lagen[gp].Prozent) : "–";
            _anordnenRaster!.IsChecked = KnopfAnordnung.RasterAn;
            _anordnenPfeile!.IsVisible = gewaehlt is not null;
            if (gewaehlt is not null)
            {
                var namenPfeile = new[] { SpielProbeTexte.AnordnenLinks, SpielProbeTexte.AnordnenRechts, SpielProbeTexte.AnordnenOben, SpielProbeTexte.AnordnenUnten };
                for (var i = 0; i < _anordnenPfeile.Children.Count; i++)
                {
                    Avalonia.Automation.AutomationProperties.SetName(_anordnenPfeile.Children[i], string.Format(CultureInfo.InvariantCulture, namenPfeile[i], gewaehlt));
                }
            }
        }
        finally
        {
            _anordnenSetzen = false;
        }
        var breite = Math.Max(0, Bounds.Width - sicher.L - sicher.R - 16);
        _anordnenLeiste.Width = breite;
        _anordnenLeiste.Measure(new Size(breite, double.PositiveInfinity));
        var leisteHoehe = _anordnenLeiste.DesiredSize.Height;
        Canvas.SetLeft(_anordnenLeiste, sicher.L + 8);
        Canvas.SetTop(_anordnenLeiste, sicher.O + 8);
        _anordnenLeiste.Arrange(new Rect(sicher.L + 8, sicher.O + 8, breite, leisteHoehe));
        // Hinweis
        _anordnenHinweisText!.Text = s.SpeichernFehlgeschlagen ? ProjektTexte.SpeichernFehlgeschlagen
            : s.Fehler != KnopfAnordnung.Fehler.Keiner ? FehlerText(s.Fehler) + (s.Finger is not null ? SpielProbeTexte.AnordnenZurueck : "")
            : s.Rot is not null && s.RotFehler != KnopfAnordnung.Fehler.Keiner ? FehlerText(s.RotFehler)
            : KnopfAnordnung.RasterAn ? SpielProbeTexte.AnordnenHinweisRaster : SpielProbeTexte.AnordnenHinweis;
        // das Canvas misst seine Kinder ohne Grenze: die Breite darum ueber MaxWidth begrenzen (sonst ragt ein langer Hinweis
        // am Handy hoch ueber den Rand; nur bei Aenderung setzen, damit das Layout zur Ruhe kommt)
        var hinweisMax = Math.Max(0, Math.Min(560, Bounds.Width - 32));
        if (Math.Abs(_anordnenHinweis!.MaxWidth - hinweisMax) > 1e-9)
        {
            _anordnenHinweis.MaxWidth = hinweisMax;
        }
        _anordnenHinweis.Measure(new Size(hinweisMax, double.PositiveInfinity));
        var hs = _anordnenHinweis.DesiredSize;
        var hy = Math.Max(sicher.O + 8 + leisteHoehe + 8, (Bounds.Height * (s.Fehler != KnopfAnordnung.Fehler.Keiner ? 0.42 : 0.36)) - (hs.Height / 2));
        var hx = (Bounds.Width - hs.Width) / 2;
        Canvas.SetLeft(_anordnenHinweis, hx);
        Canvas.SetTop(_anordnenHinweis, hy);
        _anordnenHinweis.Arrange(new Rect(hx, hy, hs.Width, hs.Height));
        // Bild darunter: Abdunkeln, Raster, gesperrte Flaechen, Platz fuer die Sticks, Ringe, Hilfslinien
        var sticks = new List<(double X, double Y, double R)>();
        foreach (var seite in new[] { Sticks.Seite.Links, Sticks.Seite.Rechts })
        {
            if (KnopfAnordnung.StickStelle(s.Lagen, b, seite) is { } m)
            {
                sticks.Add(m);
            }
        }
        var (hx0, hy0) = s.Finger is not null && s.Gewaehlt is { } hk && KnopfAnordnung.RasterAn ? KnopfAnordnung.Hilfslinien(s.Lagen, hk) : (null, null);
        _anordnenBild.Setzen(new AnordnenBild.Stand(f0, b, KnopfAnordnung.RasterAn,
            [.. s.Lagen.Select(e => (e.Value.X, e.Value.Y, KnopfAnordnung.Durchmesser(e.Key, b, e.Value.Prozent),
                e.Key == s.Rot ? AnordnenBild.Ring.Rot : e.Key == s.Gewaehlt ? AnordnenBild.Ring.Gewaehlt : AnordnenBild.Ring.Normal))],
            sticks, hx0, hy0));
    }

    private static string FehlerText(KnopfAnordnung.Fehler f) => f switch
    {
        KnopfAnordnung.Fehler.Aussen => SpielProbeTexte.AnordnenAussen,
        KnopfAnordnung.Fehler.Leiste => SpielProbeTexte.AnordnenLeiste,
        KnopfAnordnung.Fehler.Ueberlappt => SpielProbeTexte.AnordnenUeberlappt,
        KnopfAnordnung.Fehler.StickPlatz => SpielProbeTexte.AnordnenStickPlatz,
        _ => "",
    };

    /// <summary>Gezeichnete Teile des Bearbeiten-Modus (nur Anzeige, nicht antippbar).</summary>
    private sealed class AnordnenBild : Control
    {
        public enum Ring { Normal, Gewaehlt, Rot }

        public sealed record Stand(Point F0, Sticks.Bereich B, bool Raster, List<(double X, double Y, double D, Ring Ring)> Knoepfe,
            List<(double X, double Y, double R)> Sticks, double? LinieX, double? LinieY);

        private Stand? _stand;

        public void Setzen(Stand stand)
        {
            _stand = stand;
            InvalidateVisual();
        }

        public override void Render(DrawingContext dc)
        {
            base.Render(dc);
            if (_stand is not { } s)
            {
                return;
            }
            var w = Bounds.Width;
            var h = Bounds.Height;
            dc.FillRectangle(new SolidColorBrush(Color.FromArgb(95, 0, 0, 0)), new Rect(0, 0, w, h));
            using (dc.PushTransform(Matrix.CreateTranslation(s.F0.X, s.F0.Y)))
            {
                var b = s.B;
                if (s.Raster)
                {
                    // alle 16 Punkte ein Punkt (eingerastet wird auf 8)
                    var punkt = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
                    for (var x = 16.0; x < b.Breite; x += 16)
                    {
                        for (var y = (Math.Floor(b.Oben / 16) * 16) + 16; y < b.Hoehe; y += 16)
                        {
                            dc.FillRectangle(punkt, new Rect(x - 0.75, y - 0.75, 1.5, 1.5));
                        }
                    }
                }
                Schraffur(dc, new Rect(0, 0, b.Breite, b.Oben + KnopfAnordnung.RandBild));
                var leiste = KnopfAnordnung.Leiste(b);
                if (leiste.X2 > leiste.X1)
                {
                    Schraffur(dc, new Rect(leiste.X1, leiste.Y1, leiste.X2 - leiste.X1, leiste.Y2 - leiste.Y1));
                }
                var hell = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255));
                var gestrichelt = new Pen(hell, 2, new DashStyle([4, 4], 0));
                foreach (var (x, y, r) in s.Sticks)
                {
                    dc.DrawEllipse(null, gestrichelt, new Point(x, y), r, r);
                    var text = new FormattedText(SpielProbeTexte.PlatzFuerStick, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        Typeface.Default, 11, new SolidColorBrush(Color.FromArgb(220, 255, 255, 255))) { TextAlignment = TextAlignment.Center };
                    dc.DrawText(text, new Point(x - (text.Width / 2) + (text.WidthIncludingTrailingWhitespace / 2), y - (text.Height / 2)));
                }
                var linie = new Pen(new SolidColorBrush(Color.FromRgb(120, 230, 245)), 3, new DashStyle([2.5, 2.5], 0));
                if (s.LinieX is { } lx)
                {
                    dc.DrawLine(linie, new Point(lx, b.Oben), new Point(lx, b.Hoehe));
                }
                if (s.LinieY is { } ly)
                {
                    dc.DrawLine(linie, new Point(0, ly), new Point(b.Breite, ly));
                }
                var app = Application.Current;
                var akzent = app?.TryFindResource("Pinsel.accent", out var a) == true && a is ISolidColorBrush ab ? ab.Color : Colors.DeepSkyBlue;
                var rot = app?.TryFindResource("Pinsel.red", out var rr) == true && rr is ISolidColorBrush rb ? rb.Color : Colors.Red;
                foreach (var (x, y, d, ring) in s.Knoepfe)
                {
                    var r = d / 2;
                    var m = new Point(x, y);
                    switch (ring)
                    {
                        case Ring.Rot:
                            dc.DrawEllipse(null, new Pen(new SolidColorBrush(rot), 4), m, r + 5, r + 5);
                            break;
                        case Ring.Gewaehlt:
                            dc.DrawEllipse(null, new Pen(new SolidColorBrush(akzent), 4), m, r + 5, r + 5);
                            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)), 2), m, r + 9, r + 9);
                            if (d < KnopfAnordnung.TrefferMin)
                            {
                                // Trefferflaeche bleibt mindestens 40 Punkte (Frage 1a, Abnahmebild N1)
                                var gelb = new Pen(new SolidColorBrush(Color.FromRgb(255, 220, 120)), 2, new DashStyle([3, 3], 0));
                                dc.DrawEllipse(null, gelb, m, (KnopfAnordnung.TrefferMin / 2) + 10, (KnopfAnordnung.TrefferMin / 2) + 10);
                            }
                            break;
                        default:
                            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)), 2, new DashStyle([3, 3], 0)), m, r + 5, r + 5);
                            break;
                    }
                }
            }
        }

        private static void Schraffur(DrawingContext dc, Rect r)
        {
            if (r.Width <= 0 || r.Height <= 0)
            {
                return;
            }
            dc.FillRectangle(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), r);
            var stift = new Pen(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), 2);
            using (dc.PushClip(r))
            {
                for (var x = r.X - r.Height; x < r.Right; x += 14)
                {
                    dc.DrawLine(stift, new Point(x, r.Bottom), new Point(x + r.Height, r.Y));
                }
            }
        }
    }
}
