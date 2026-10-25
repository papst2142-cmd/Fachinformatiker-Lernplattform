using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Design;
using Lernplattform.Design;
using Lernplattform.Views;

namespace Lernplattform.Spiel.Tafel;

/// <summary>
/// Phase 7, W6: Bausteine der Spielansichten wie mobile/src/ui.py (text, label, Card, GradientButton,
/// GradientBar, PillGroup, entry, as_button). Farben kommen als Name der Farbwelt ("accent"), als
/// fester Wert ("#22D3EE") oder als fertiger Schluessel ("Pinsel.text"); Mischfarben rechnet
/// <see cref="Mix"/> wie fisi_theme.mix aus der aktuellen Farbwelt. Die Ansichten bauen sich bei
/// jeder Aenderung neu (wie render() in Python).
/// </summary>
public static class Bau
{
    /// <summary>C[name]: Farbe der aktuellen Farbwelt.</summary>
    public static string C(string name) => FarbenAnwenden.Aktuell.Farben.TryGetValue(name, out var hex) ? hex : "#808080";

    /// <summary>fisi_theme.mix</summary>
    public static string Mix(string a, string b, double t) => Farbwelt.Mischen(Hex(a), Hex(b), t);

    /// <summary>lighten</summary>
    public static string Hell(string farbe, double anteil = 0.15) => Mix(farbe, "#FFFFFF", anteil);

    /// <summary>Hexwert zu einem Farbnamen oder Hexwert.</summary>
    public static string Hex(string farbe) => farbe.StartsWith('#') ? farbe
        : C(farbe.StartsWith("Pinsel.", StringComparison.Ordinal) ? farbe["Pinsel.".Length..] : farbe);

    /// <summary>Ressourcen-Schluessel zu einer Farbe (Name der Farbwelt, Hexwert oder Schluessel).</summary>
    public static string Ton(string farbe)
    {
        if (farbe.StartsWith('#'))
        {
            var schluessel = "Hex." + farbe.ToUpperInvariant();
            if (Application.Current is { } app && !app.Resources.ContainsKey(schluessel))
            {
                app.Resources[schluessel] = new SolidColorBrush(Color.Parse(farbe));
            }
            return schluessel;
        }
        return farbe.Contains('.', StringComparison.Ordinal) ? farbe : "Pinsel." + farbe;
    }

    public static IBrush Pinsel(string farbe) => new SolidColorBrush(Color.Parse(Hex(farbe)));

    /// <summary>
    /// ui.text: Groesse in Punkten wie am Handy (waechst mit der Schriftstufe). Passt das laengste Wort
    /// bei grosser Schrift nicht in die Zeile (Handy schmal), wird nur dieser Text kleiner, nie unter
    /// die Stufe Normal (Regel wie <see cref="WortText"/>), damit kein Wort getrennt wird.
    /// </summary>
    public static TextBlock T(string text, double groesse = 14, string farbe = "text", bool fett = false) =>
        new TafelText(text, groesse, fett ? FontWeight.Bold : FontWeight.Normal, Ton(farbe));

    /// <summary>ui.label: kleine Ueberschrift in Grossbuchstaben (11 fett, muted).</summary>
    public static TextBlock Label(string text, string farbe = "muted") =>
        T(Core.Texte.Beschriftungen.Gross(text), 11, farbe, true);

    /// <summary>ui.Card mit Titel, Inhalt untereinander (spacing 10), Akzent und Unterschrift.</summary>
    public static Border Karte(string titel, IEnumerable<Control?> inhalt, string akzent = "accent", string? unter = null, Control? aktion = null)
    {
        var stapel = Stapel(10, inhalt);
        return Schrift.KopfKarte(titel, string.IsNullOrEmpty(unter) ? null : unter, stapel, Ton(akzent), aktion);
    }

    public static StackPanel Stapel(double abstand, IEnumerable<Control?> inhalt)
    {
        var stapel = new StackPanel { Spacing = abstand };
        foreach (var teil in inhalt)
        {
            if (teil is not null)
            {
                stapel.Children.Add(teil);
            }
        }
        return stapel;
    }

    public static StackPanel Stapel(params Control?[] inhalt) => Stapel(6, inhalt);

    /// <summary>ft.Row(wrap=True): Teile nebeneinander, bei Platzmangel in die naechste Zeile.</summary>
    public static WrapPanel Reihe(IEnumerable<Control?> teile)
    {
        var reihe = new WrapPanel();
        foreach (var teil in teile)
        {
            if (teil is not null)
            {
                reihe.Children.Add(teil);
            }
        }
        return reihe;
    }

    public static WrapPanel Reihe(params Control?[] teile) => Reihe((IEnumerable<Control?>)teile);

    /// <summary>Links ein Teil, das den Platz nimmt, rechts ein schmales (ft.Row mit expand).</summary>
    public static Grid Zeile(Control links, Control? rechts, double abstand = 10, Control? vorne = null)
    {
        var raster = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = abstand };
        if (vorne is not null)
        {
            vorne.VerticalAlignment = VerticalAlignment.Center;
            raster.Children.Add(vorne);
        }
        links.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(links, 1);
        raster.Children.Add(links);
        if (rechts is not null)
        {
            rechts.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(rechts, 2);
            raster.Children.Add(rechts);
        }
        return raster;
    }

    /// <summary>
    /// ui.GradientButton: art "primary", "accent", "success", "danger" (Verlauf) oder "ghost" (dezent).
    /// hoehe wie am Handy (46, 42, 38, 30).
    /// </summary>
    public static Button Knopf(string text, Action? klick, string art = "primary", double hoehe = 46, bool frei = true)
    {
        var knopf = Schrift.Knopf(text, art != "ghost");
        if (art is not ("ghost" or "primary"))
        {
            knopf.Farbe(Button.BackgroundProperty, "VerlaufW." + art);
        }
        knopf.Bind(Layoutable.MinHeightProperty, knopf.GetResourceObservable("Schrift.Normal",
            v => v is double d ? hoehe * d / Schrift.Grundgroessen["Normal"] : hoehe));
        if (hoehe < Schrift.KnopfHoehe)
        {
            knopf.Padding = new Thickness(16, 4);
        }
        knopf.IsEnabled = frei;
        knopf.Opacity = frei ? 1.0 : 0.4;
        if (klick is not null)
        {
            knopf.Click += (_, _) => klick();
        }
        return knopf;
    }

    /// <summary>ui.GradientBar: Name links (13 text_dim), Wert rechts (12 muted), Balken 8 mit Verlauf.</summary>
    public static Control Balken(string name, string a, string b, double prozent, string wert = "")
    {
        var anteil = (int)Math.Round(Math.Max(0, Math.Min(100, prozent)) * 10, MidpointRounding.ToEven);
        var kopf = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        var wertText = T(wert, 12, "muted");
        Grid.SetColumn(wertText, 1);
        kopf.Children.Add(T(name, 13, "text_dim"));
        kopf.Children.Add(wertText);
        var spur = new Grid { Height = 8, ColumnDefinitions = new ColumnDefinitions($"{anteil}*,{1000 - anteil}*") };
        var hinten = new Border { CornerRadius = new CornerRadius(4) }.Farbe(Border.BackgroundProperty, "Pinsel.ring_bg");
        Grid.SetColumnSpan(hinten, 2);
        spur.Children.Add(hinten);
        if (anteil > 0)
        {
            spur.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Verlauf(a, b) });
        }
        var feld = new StackPanel { Spacing = 6, Children = { kopf, spur } };
        Avalonia.Automation.AutomationProperties.SetName(feld, name + " " + wert);
        return feld;
    }

    /// <summary>gradient((a, b)): Verlauf von links nach rechts.</summary>
    public static LinearGradientBrush Verlauf(string a, string b) => new()
    {
        StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.Parse(Hex(a)), 0), new GradientStop(Color.Parse(Hex(b)), 1) },
    };

    /// <summary>ui.PillGroup: genau eine Wahl, Wechsel ruft geaendert.</summary>
    public static Pillen Pillen(IEnumerable<(string Wert, string Text)> optionen, string gewaehlt, Action<string> geaendert) =>
        new(optionen, gewaehlt, geaendert);

    /// <summary>ui.entry: einzeiliges Eingabefeld mit Hinweis.</summary>
    public static TextBox Eingabe(string wert, string? hinweis = null, Action<string>? geaendert = null, int laenge = 0)
    {
        var feld = new TextBox { Text = wert, PlaceholderText = hinweis, TextWrapping = TextWrapping.Wrap }.Groesse();
        if (laenge > 0)
        {
            feld.MaxLength = laenge;
        }
        if (geaendert is not null)
        {
            feld.TextChanged += (_, _) => geaendert(feld.Text ?? "");
        }
        return feld;
    }

    /// <summary>
    /// Flaeche wie ft.Container(bgcolor, border, border_radius, padding) und mit klick antippbar
    /// (ui.as_button: meldet sich als Schaltflaeche).
    /// </summary>
    public static Border Flaeche(Control inhalt, string hinten = "card_alt", string rand = "border", Action? klick = null,
        double ecken = 12, Thickness? innen = null, double randBreite = 1)
    {
        var flaeche = new Border
        {
            Child = inhalt,
            CornerRadius = new CornerRadius(ecken),
            Padding = innen ?? new Thickness(12, 10),
            BorderThickness = new Thickness(randBreite),
        }.Farbe(Border.BackgroundProperty, Ton(hinten)).Farbe(Border.BorderBrushProperty, Ton(rand));
        if (klick is not null)
        {
            flaeche.Cursor = new Cursor(StandardCursorType.Hand);
            flaeche.Focusable = true;
            flaeche.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Left)
                {
                    e.Handled = true;
                    klick();
                }
            };
            flaeche.KeyDown += (_, e) =>
            {
                if (e.Key is Key.Enter or Key.Space)
                {
                    e.Handled = true;
                    klick();
                }
            };
            Avalonia.Automation.AutomationProperties.SetControlTypeOverride(flaeche, Avalonia.Automation.Peers.AutomationControlType.Button);
        }
        return flaeche;
    }

    /// <summary>Hinweis-Kasten wie im Spiel (mix(card, farbe, 0.1), Rand mix(farbe, card, 0.45)).</summary>
    public static Border Hinweis(IEnumerable<Control?> inhalt, string farbe = "purple") =>
        Flaeche(Stapel(4, inhalt), Mix("card", farbe, 0.1), Mix(farbe, "card", 0.45), innen: new Thickness(12));

    /// <summary>Senkrechter Farbstrich (Prioritaet eines Tickets).</summary>
    public static Border Strich(string farbe, double breite = 4, double hoehe = 40) =>
        new Border { Width = breite, Height = hoehe, CornerRadius = new CornerRadius(2) }.Farbe(Border.BackgroundProperty, Ton(farbe));

    /// <summary>euro(): ganze Euro mit Punkt als Tausender und geschuetztem Leerzeichen vor "€".</summary>
    public static string Euro(object? wert) => Core.Spiel.PyFormat.GanzPunkt(wert) + "\u00a0€";

    /// <summary>Hinweis unten (app.toast) in der Farbe des Spiels.</summary>
    public static void Hinweisen(string text, string farbe = "yellow") => Kurzhinweis.Zeigen(text, Ton(farbe));
}

/// <summary>
/// Text der Spielansichten (<see cref="Bau.T"/>): Groesse wie <see cref="Schrift.Pt"/>; ist das
/// laengste Wort breiter als die Zeile, wird der Text so weit verkleinert, dass es ganz bleibt, nie
/// unter die Groesse der Stufe Normal.
/// </summary>
public sealed class TafelText : TextBlock
{
    private readonly double _normal;
    private double _soll;

    public TafelText(string text, double groesse, FontWeight gewicht, string farbe)
    {
        Text = text;
        FontWeight = gewicht;
        TextWrapping = TextWrapping.Wrap;
        _normal = groesse;
        _soll = groesse;
        FontSize = groesse;
        this.Farbe(ForegroundProperty, farbe);
        this.GetResourceObservable("Schrift.Normal").Subscribe(new Beobachter(v =>
        {
            _soll = v is double d ? groesse * d / Schrift.Grundgroessen["Normal"] : groesse;
            FontSize = _soll;
            InvalidateMeasure();
        }));
    }

    protected override Size MeasureOverride(Size verfuegbar)
    {
        var groesse = _soll;
        if (_soll > _normal && !double.IsInfinity(verfuegbar.Width) && verfuegbar.Width > 0 && Text is { Length: > 0 } text)
        {
            var laengstes = text.Split([' ', '\n', '-', '/', '\u200B']).Max(w => Breite(w, _soll));
            if (laengstes > verfuegbar.Width)
            {
                groesse = Math.Max(_normal, Math.Floor(_soll * verfuegbar.Width / laengstes * 10) / 10 - 0.1);
            }
        }
        if (FontSize != groesse)
        {
            FontSize = groesse;
        }
        return base.MeasureOverride(verfuegbar);
    }

    private double Breite(string wort, double groesse) =>
        new FormattedText(wort, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle, FontWeight), groesse, null).WidthIncludingTrailingWhitespace;

    private sealed class Beobachter(Action<object?> weiter) : IObserver<object?>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(object? value) => weiter(value);
    }
}
