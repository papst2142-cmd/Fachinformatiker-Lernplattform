using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Views;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 1.13.0 (Sammelliste 464, Weisung P12-394, Nico 1a 2a 3a 4a 5a; Nicos Fund: „Die Texte sind sehr oft im weg beim
/// Steuern“): mit der Handlung (3D, nicht beim Einrichten) steht der Hinweis nicht mehr unten auf dem Weg der Figur, sondern als
/// schmale Zeile oben neben der Uhr (am schmalen Handy darunter). Ablauf in <see cref="HinweisOben"/>: 6 s ganz, danach nur der
/// Titel blass, beim Weitergehen weg; Tippen oder Klick (am PC auch die Taste <see cref="Tastenbelegung.Hinweis"/>, Standard H)
/// zeigt den ganzen Text oben, noch einmal Tippen oder Weitergehen schliesst ihn. Jeder neue Text wird vorgelesen. Unten bleiben
/// nur die Steuer-Knoepfe. Gegenprobe <see cref="HinweisfeldAltFuerTests"/>: der Text steht wie bis A 1.12.0 in der Karte unten.
/// </summary>
public sealed partial class SpielSeite
{
    /// <summary>Gegenprobe (Start-Test 464, je Seite): der Hinweis steht wie bis A 1.12.0 offen in der Karte unten.</summary>
    internal bool HinweisfeldAltFuerTests { get; set; }

    /// <summary>Gegenprobe zur Nachbesserung A 1.13.0 (Weisung P12-398, nur Start-Test): Leiste oben immer bis vor das X wie a0f9e08.</summary>
    internal bool HinweisObenBreitAltFuerTests
    {
        get => _obenBreitAlt;
        set
        {
            _obenBreitAlt = value;
            HinweisObenLegen();
        }
    }

    private bool _obenBreitAlt;

    /// <summary>Start-Test 464: feste Zeit des Ablaufs statt der Uhr (null = Uhr), um die 6 s nicht abwarten zu muessen.</summary>
    internal TimeSpan? HinweisZeitFuerTests { get; set; }

    /// <summary>Breite der Zeile neben der Uhr, ab der sie daneben steht (sonst darunter ueber die ganze Breite).</summary>
    private const double ObenDanebenAb = 240;

    private readonly HinweisOben _hinweisLogik = new();
    private readonly long _hinweisStart = Stopwatch.GetTimestamp();
    private Klickflaeche? _obenFeld;
    private readonly TextBlock _obenZeile = new() { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _obenTitel = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.Bold };
    private readonly TextBlock _obenGanz = new() { TextWrapping = TextWrapping.Wrap };
    private ScrollViewer? _obenRolle;
    private TextBlock? _obenSymbol;
    private string _obenSymbolName = "";
    private (HinweisOben.Stufe Stufe, string Text, bool Daneben, bool An)? _obenZuletzt;
    private bool _obenDaneben = true;

    /// <summary>Hinweis oben statt in der Karte unten (Handlung, ohne Gegenprobe).</summary>
    private bool HinweisObenAn => HandlungModus && !HinweisfeldAltFuerTests;

    /// <summary>Start-Test, Fotos: die Zeile oben (Klickflaeche).</summary>
    public Klickflaeche? HinweisObenFeld => _obenFeld;

    /// <summary>Start-Test: was oben zu sehen ist (Aus, wenn die Zeile nicht zu sehen ist).</summary>
    public HinweisOben.Stufe HinweisObenStufe => _obenFeld is { IsVisible: true } ? _hinweisLogik.Jetzt : HinweisOben.Stufe.Aus;

    /// <summary>Start-Test: der Text der Zeile oben, wie er zu sehen ist (eine Zeile, Titel oder ganzer Text).</summary>
    public string HinweisObenText => HinweisObenStufe switch
    {
        HinweisOben.Stufe.Offen => ((_obenTitel.IsVisible ? _obenTitel.Text + "\n" : "") + _obenGanz.Text).Trim(),
        HinweisOben.Stufe.Aus => "",
        _ => string.Concat(_obenZeile.Inlines?.OfType<Run>().Select(r => r.Text) ?? []),
    };

    /// <summary>Start-Test: steht die Zeile neben der Uhr (true) oder darunter?</summary>
    public bool HinweisObenDaneben => _obenDaneben;

    /// <summary>Start-Test: Rechteck der Zeile oben in Punkten der Seite (null = nicht zu sehen).</summary>
    public Rect? HinweisObenRechteck() =>
        _obenFeld is { IsEffectivelyVisible: true } f && f.TranslatePoint(default, this) is { } p ? new Rect(p, f.Bounds.Size) : null;

    /// <summary>
    /// Start-Test (Weisung P12-398): Platz in der Leiste oben rechts neben ihrem Inhalt in Punkten (0 = die Leiste endet am Text;
    /// gross = sie geht ueber die ganze Breite wie bis a0f9e08); null = nicht zu sehen.
    /// </summary>
    public double? HinweisObenRest() => _obenFeld is { IsEffectivelyVisible: true } f ? f.Bounds.Width - f.DesiredSize.Width + f.Margin.Left + f.Margin.Right : null;

    /// <summary>Start-Test 464: gilt Antippen (Text der Tafelzeile wie in der Zeile oben)?</summary>
    public bool AntippenGiltJetzt => AntippenGilt;

    /// <summary>Start-Test 464: steht der Hinweistext (wie bis A 1.12.0) in der Karte unten?</summary>
    public bool HinweisUntenSichtbar => _zeileRolle.IsEffectivelyVisible;

    /// <summary>Start-Test 464: Zustand der Karte unten (Fehlersuche): Text, Karte, Karte wirklich zu sehen, Handlung.</summary>
    public string UntenZustand => $"Text {_zeileRolle.IsVisible}, Karte {_unten.IsVisible}/{_unten.IsEffectivelyVisible}, Handlung {HandlungModus}, oben {HinweisObenAn}";

    /// <summary>Start-Test 464: ist die Karte unten zu sehen?
    public bool UntenSichtbar => _unten.IsEffectivelyVisible;

    private void HinweisObenAnlegen(Grid ebene)
    {
        _obenZeile.Groesse("Normal");
        _obenZeile.Farbe(TextBlock.ForegroundProperty, "Pinsel.text");
        _obenTitel.Groesse("Normal");
        _obenTitel.Farbe(TextBlock.ForegroundProperty, "Pinsel.text");
        _obenGanz.Groesse("Normal");
        _obenGanz.Farbe(TextBlock.ForegroundProperty, "Pinsel.text");
        _obenRolle = new ScrollViewer
        {
            Content = new StackPanel { Spacing = 2, Children = { _obenTitel, _obenGanz }, Margin = new Thickness(0, 0, Dialog.RandFuerLeiste, 0) },
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            IsVisible = false,
        };
        _obenSymbol = Symbole.Block("chevron_right", 18, "Pinsel.muted");
        _obenSymbolName = "chevron_right";
        // Rand am Symbol nur ueber einen Rahmen (Margin am TextBlock zaehlt in der Layoutpruefung als abgeschnitten)
        var symbolRahmen = new Border { Child = _obenSymbol, Padding = new Thickness(8, 2, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        var inhalt = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var text = new Panel { Children = { _obenZeile, _obenRolle } };
        inhalt.Children.Add(text);
        Grid.SetColumn(symbolRahmen, 1);
        inhalt.Children.Add(symbolRahmen);
        var feld = new Klickflaeche
        {
            Child = inhalt,
            Padding = new Thickness(14, 6, 10, 6),
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            // Nachbesserung A 1.13.0 (Weisung P12-398, Nico 26.11.2026): die Leiste ist nur so breit wie ihr Text (kurzer Text =
            // kurze Leiste), hoechstens bis vor das X; darueber „…“ bzw. Umbruch wie bisher
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
            IsVisible = false,
            Klick = HinweisObenTippen,
        };
        feld.Classes.Add("karte");
        feld.Farbe(Border.BackgroundProperty, "Pinsel.card");
        feld.Farbe(Border.BorderBrushProperty, "Pinsel.border");
        feld.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left && new Rect(feld.Bounds.Size).Contains(e.GetPosition(feld)))
            {
                e.Handled = true;
                HinweisObenTippen();
            }
        };
        feld.PointerPressed += (_, e) => e.Handled = true;
        feld.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                e.Handled = true;
                HinweisObenTippen();
            }
        };
        ebene.Children.Add(feld);
        _obenFeld = feld;
        _kopf.SizeChanged += (_, _) => HinweisObenLegen();
        ebene.SizeChanged += (_, _) => HinweisObenLegen();
    }

    private TimeSpan HinweisJetzt => HinweisZeitFuerTests ?? Stopwatch.GetElapsedTime(_hinweisStart);

    /// <summary>Tippen, Klick, Eingabe, Taste H: ganzen Text zeigen oder schliessen.</summary>
    internal void HinweisObenTippen()
    {
        if (!HinweisObenAn || Spiel is not { } spiel || !_hinweisLogik.Tippen(spiel.FigurX, spiel.FigurZ))
        {
            return;
        }
        HinweisObenZeigen();
    }

    /// <summary>Beim Beschriften: neuen Text uebernehmen (und vorlesen).</summary>
    private void HinweisObenTakt(StadtSpiel spiel)
    {
        var text = SpielProbeTexte.Tafelzeile(spiel.Zeile, AntippenGilt);
        if (_hinweisLogik.Takt(text, HinweisJetzt, spiel.FigurX, spiel.FigurZ) && HinweisObenAn)
        {
            // B-4: jeder neue Text wird vorgelesen (wie er oben steht, ganz)
            Ansage.Sagen(_hinweisLogik.Text);
        }
        HinweisObenZeigen();
    }

    /// <summary>Je Bild: Zeit (6 s) und Weitergehen pruefen; die Oberflaeche nur bei einer Aenderung anfassen.</summary>
    private void HinweisObenBild()
    {
        if (Spiel is not { } spiel || _obenFeld is null)
        {
            return;
        }
        var vorher = _hinweisLogik.Jetzt;
        if (vorher == HinweisOben.Stufe.Offen)
        {
            OffenHoeheSetzen();
        }
        _hinweisLogik.Takt(_hinweisLogik.Text.Length > 0 ? _hinweisLogik.Text : SpielProbeTexte.Tafelzeile(spiel.Zeile, AntippenGilt), HinweisJetzt, spiel.FigurX, spiel.FigurZ);
        if (_hinweisLogik.Jetzt != vorher)
        {
            HinweisObenZeigen();
        }
    }

    /// <summary>Inhalt, Sichtbarkeit und Deckkraft der Zeile nach der Stufe (nur bei einer Aenderung, Lehre A 1.6.0).</summary>
    private void HinweisObenZeigen()
    {
        if (_obenFeld is not { } feld || _obenRolle is null || _obenSymbol is null)
        {
            return;
        }
        var an = HinweisObenAn && _hinweisLogik.Jetzt != HinweisOben.Stufe.Aus;
        var stand = (_hinweisLogik.Jetzt, _hinweisLogik.Text, _obenDaneben, an);
        if (_obenZuletzt == stand)
        {
            return;
        }
        _obenZuletzt = stand;
        feld.IsVisible = an;
        if (!an)
        {
            FreiSetzen();
            return;
        }
        var text = _hinweisLogik.Text;
        var (titel, _) = HinweisOben.Teile(text);
        var offen = _hinweisLogik.Jetzt == HinweisOben.Stufe.Offen;
        _obenZeile.IsVisible = !offen;
        _obenRolle.IsVisible = offen;
        _obenZeile.Inlines ??= [];
        _obenZeile.Inlines.Clear();
        switch (_hinweisLogik.Jetzt)
        {
            case HinweisOben.Stufe.Voll:
                if (titel.Length > 0)
                {
                    _obenZeile.Inlines.Add(new Run(titel) { FontWeight = FontWeight.Bold });
                    _obenZeile.Inlines.Add(new Run(" " + HinweisOben.RestInEinerZeile(text)));
                }
                else
                {
                    _obenZeile.Inlines.Add(new Run(HinweisOben.RestInEinerZeile(text)));
                }
                break;
            case HinweisOben.Stufe.Titel:
                _obenZeile.Inlines.Add(new Run(HinweisOben.Kurz(text)) { FontWeight = FontWeight.Bold });
                break;
            default:
                _obenTitel.Text = titel;
                _obenTitel.IsVisible = titel.Length > 0;
                _obenGanz.Text = HinweisOben.Teile(text).Inhalt;
                break;
        }
        // am schmalen Handy (Zeile unter der Uhr) zwei Zeilen, daneben eine
        _obenZeile.TextWrapping = _obenDaneben ? TextWrapping.NoWrap : TextWrapping.Wrap;
        _obenZeile.MaxLines = _obenDaneben ? 1 : 2;
        OffenHoeheSetzen();
        feld.Opacity = _hinweisLogik.Jetzt == HinweisOben.Stufe.Titel ? HinweisOben.TitelDeckkraft : 1;
        var symbol = offen ? "close_rounded" : "chevron_right";
        if (symbol != _obenSymbolName)
        {
            _obenSymbolName = symbol;
            _obenSymbol.Text = Symbole.Text(symbol);
        }
        var name = string.Format(CultureInfo.CurrentCulture, offen ? SpielProbeTexte.HinweisGanz : SpielProbeTexte.HinweisZeile, text.Replace('\n', ' '));
        Avalonia.Automation.AutomationProperties.SetName(feld, name);
        FreiSetzen();
    }

    /// <summary>Lage der Zeile: neben der Uhr, wenn dort genug Platz ist, sonst darunter ueber die ganze Breite.</summary>
    private void HinweisObenLegen()
    {
        if (_obenFeld is not { } feld || Ebene.Bounds.Width <= 0 || _kopf.Bounds.Width <= 0)
        {
            return;
        }
        var xBreite = _xKnopf is { IsVisible: true } x ? x.Width + x.Margin.Left : 0;
        var platz = Ebene.Bounds.Width - _kopf.Bounds.Width - 8 - xBreite - 8;
        var daneben = platz >= ObenDanebenAb;
        var rand = daneben ? new Thickness(_kopf.Bounds.Width + 8, 0, 8, 0) : new Thickness(0, _kopf.Bounds.Height + 8, 0, 0);
        if (feld.Margin != rand)
        {
            feld.Margin = rand;
        }
        var ausrichtung = HinweisObenBreitAltFuerTests ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        if (feld.HorizontalAlignment != ausrichtung)
        {
            feld.HorizontalAlignment = ausrichtung;
        }
        var spalten = daneben ? 1 : 2;
        if (Grid.GetColumnSpan(feld) != spalten)
        {
            Grid.SetColumnSpan(feld, spalten);
        }
        if (_obenDaneben != daneben)
        {
            _obenDaneben = daneben;
            HinweisObenZeigen();
        }
    }

    /// <summary>
    /// B-1 auch aufgeklappt: der ganze Text endet ueber der Figur (rollbar), hoechstens 40 % der Hoehe, mindestens eine Zeile.
    /// Nur bei einer Aenderung um mehr als einen Punkt (je Bild gerufen).
    /// </summary>
    private void OffenHoeheSetzen()
    {
        if (_obenRolle is not { } rolle || _obenFeld is not { } feld)
        {
            return;
        }
        var hoechstens = Math.Floor(Ebene.Bounds.Height * 0.4);
        var zeile = Math.Ceiling(_obenGanz.FontSize * 1.4);
        if (Flaeche.FigurAufBild() is { } figur && feld.TranslatePoint(default, Flaeche) is { } oben)
        {
            // Rand der Karte (Rahmen und Innenabstand) und 8 Punkte Luft zur Figur
            var rand = feld.Padding.Top + feld.Padding.Bottom + feld.BorderThickness.Top + feld.BorderThickness.Bottom + 8;
            hoechstens = Math.Min(hoechstens, Math.Floor(figur.Top - oben.Y - rand));
        }
        var ziel = Math.Max(zeile, hoechstens);
        if (Math.Abs(rolle.MaxHeight - ziel) > 1)
        {
            rolle.MaxHeight = ziel;
        }
    }

    /// <summary>
    /// Unterkante der Zeile oben in Punkten der Flaeche (null = nicht zu sehen). Aufgeklappt zaehlt die eine Zeile (die Kamera
    /// springt nicht, wenn man den ganzen Text oeffnet; die Hoehe des offenen Texts richtet sich nach der Figur).
    /// </summary>
    private double? HinweisObenUnterkante()
    {
        if (_obenFeld is not { IsVisible: true } f || f.TranslatePoint(default, Flaeche) is not { } p)
        {
            return null;
        }
        var hoehe = _hinweisLogik.Jetzt == HinweisOben.Stufe.Offen
            ? Math.Min(f.Bounds.Height, f.Padding.Top + f.Padding.Bottom + f.BorderThickness.Top + f.BorderThickness.Bottom + Math.Ceiling(_obenZeile.FontSize * 1.4 * (_obenDaneben ? 1 : 2)))
            : f.Bounds.Height;
        return p.Y + hoehe;
    }
}
