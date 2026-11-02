using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Lernplattform.Core.Inhalte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Phase 4, W1: Text der Lernseiten mit sehr langen Woertern (Titel wie
/// "Wirtschaftlichkeitsbetrachtung", Aufgaben, Musterloesungen). Regel wie <see cref="WortText"/>:
/// passt das laengste Wort in der eingestellten Schriftstufe nicht in die Zeile, wird nur dieser
/// Text so weit verkleinert, dass das Wort ganz bleibt, nie unter die Stufe Normal. Woerter
/// trennt jeder Leerraum (auch Zeilenumbruch); die Breite des laengsten Wortes wird je Text und
/// Schriftgroesse einmal gemessen (lange Aufgaben, viele Zeilen).
/// </summary>
public sealed class LernText : TextBlock
{
    private static readonly char[] Leerraum = [' ', ' ', '\n', '\r', '\t'];
    private double _soll;
    private (string Text, double Groesse, double Breite)? _gemessen;

    public LernText(string text, string groesse, FontWeight gewicht, string farbe)
    {
        Text = text;
        FontWeight = gewicht;
        TextWrapping = TextWrapping.Wrap;
        _soll = Schrift.Grundgroessen[groesse];
        Untergrenze = _soll;
        this.Farbe(ForegroundProperty, farbe);
        this.GetResourceObservable("Schrift." + groesse).Subscribe(new Beobachter(v =>
        {
            if (v is double d)
            {
                _soll = d;
                FontSize = d;
                InvalidateMeasure();
            }
        }));
    }

    /// <summary>Kleinste Groesse (Stufe Normal dieser Schrift).</summary>
    public double Untergrenze { get; }

    /// <summary>
    /// Sammelliste 177: Tabellenzeilen im Text (<see cref="Tabellenzeilen"/>) in der Schrift mit fester
    /// Zeichenbreite (<see cref="Festbreite.Schrift"/>) zeigen. Mit Tabelle steht der Text in <see cref="Rohtext"/>
    /// und als Folge von Abschnitten in <see cref="TextBlock.Inlines"/> (zusammen genau dieser Text);
    /// <see cref="TextBlock.Text"/> ist dann leer, sonst zeichnet Avalonia beides. Passt die laengste Zeile einer
    /// Tabelle nicht in die Breite, wird nur diese Tabelle kleiner gesetzt, hoechstens bis zur Stufe Klein (im
    /// Verhaeltnis zur Stufe dieses Textes); reicht auch das nicht (Handy, lange Zeilen), bleibt sie in der
    /// normalen Schrift wie bisher, damit die Spalten nicht umbrechen.
    /// </summary>
    public bool Tabellen
    {
        get => _tabellen;
        init
        {
            _tabellen = value;
            Abschnitte();
        }
    }

    /// <summary>Der gezeigte Text, auch wenn er als Abschnitte in <see cref="TextBlock.Inlines"/> steht.</summary>
    public string Rohtext => _bereiche.Count > 0 ? _roh : Text ?? "";

    private bool _tabellen;
    private bool _baut;
    private string _roh = "";
    private IReadOnlyList<(int Start, int Laenge)> _bereiche = [];
    private readonly List<(List<Run> Zeilen, int Zeichen)> _bloecke = [];

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty && _tabellen && !_baut)
        {
            Abschnitte();
        }
    }

    // Text in Abschnitte teilen: Tabellenzeilen in Festbreite, der Rest in der Schrift des Elements
    private void Abschnitte()
    {
        var text = Text ?? "";
        var bloecke = _tabellen ? Tabellenzeilen.Bloecke(text) : [];
        var bereiche = bloecke.SelectMany(b => b).ToList();
        _gemessen = null;
        _bloecke.Clear();
        _baut = true;
        try
        {
            Inlines?.Clear();
            _roh = text;
            _bereiche = bereiche;
            if (_bereiche.Count > 0)
            {
                Text = null;
                Inlines ??= [];
                var pos = 0;
                foreach (var block in bloecke)
                {
                    var zeilen = new List<Run>();
                    foreach (var (start, laenge) in block)
                    {
                        if (start > pos)
                        {
                            Inlines.Add(new Run(text[pos..start]));
                        }
                        var zeile = new Run(text.Substring(start, laenge)) { FontFamily = Festbreite.Schrift };
                        zeilen.Add(zeile);
                        Inlines.Add(zeile);
                        pos = start + laenge;
                    }
                    _bloecke.Add((zeilen, zeilen.Max(z => z.Text!.TrimEnd().Length)));
                }
                if (pos < text.Length)
                {
                    Inlines.Add(new Run(text[pos..]));
                }
            }
        }
        finally
        {
            _baut = false;
        }
        InvalidateMeasure();
    }

    // Je Tabelle: Festbreite in der Groesse des Textes, kleiner bis zur Stufe Klein, sonst normale Schrift
    private void TabellenAnpassen(double breite)
    {
        if (_bloecke.Count == 0)
        {
            return;
        }
        var jeZeichen = Festbreite.Zeichenbreite(100) / 100;
        var untergrenze = _soll * Schrift.Grundgroessen["Klein"] / Schrift.Grundgroessen["Normal"];
        foreach (var (zeilen, zeichen) in _bloecke)
        {
            var groesse = FontSize;
            if (!double.IsInfinity(breite) && breite > 0 && zeichen > 0)
            {
                groesse = Math.Min(groesse, Math.Floor(breite / (zeichen * jeZeichen) * 10) / 10 - 0.1);
            }
            var fest = groesse >= untergrenze;
            foreach (var zeile in zeilen)
            {
                if (fest)
                {
                    if (!Equals(zeile.FontFamily, Festbreite.Schrift))
                    {
                        zeile.FontFamily = Festbreite.Schrift;
                    }
                    if (groesse >= FontSize)
                    {
                        if (zeile.IsSet(TextElement.FontSizeProperty))
                        {
                            zeile.ClearValue(TextElement.FontSizeProperty);
                        }
                    }
                    else if (zeile.FontSize != groesse)
                    {
                        zeile.FontSize = groesse;
                    }
                }
                else
                {
                    if (zeile.IsSet(TextElement.FontFamilyProperty))
                    {
                        zeile.ClearValue(TextElement.FontFamilyProperty);
                    }
                    if (zeile.IsSet(TextElement.FontSizeProperty))
                    {
                        zeile.ClearValue(TextElement.FontSizeProperty);
                    }
                }
            }
        }
    }

    /// <summary>Breite des laengsten Wortes; Woerter der Tabellenzeilen in der Festbreitenschrift gemessen.</summary>
    private double LaengstesWortHier(string text, double groesse)
    {
        var schrift = new Typeface(FontFamily, FontStyle, FontWeight);
        if (_bereiche.Count == 0)
        {
            return LaengstesWort(text, schrift, groesse);
        }
        var fest = new Typeface(Festbreite.Schrift, FontStyle, FontWeight);
        var normal = new System.Text.StringBuilder();
        var tabelle = new System.Text.StringBuilder();
        var pos = 0;
        foreach (var (start, laenge) in _bereiche)
        {
            normal.Append(text, pos, start - pos).Append('\n');
            tabelle.Append(text, start, laenge).Append('\n');
            pos = start + laenge;
        }
        normal.Append(text, pos, text.Length - pos);
        return Math.Max(LaengstesWort(normal.ToString(), schrift, groesse), LaengstesWort(tabelle.ToString(), fest, groesse));
    }

    /// <summary>Breite des laengsten Wortes eines Textes in Schrift und Groesse eines Elements.</summary>
    public static double LaengstesWort(string text, Typeface schrift, double groesse) =>
        text.Split(Leerraum, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal)
            .Select(w => new FormattedText(w, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, schrift, groesse, null)
                .WidthIncludingTrailingWhitespace)
            .DefaultIfEmpty(0).Max();

    protected override Size MeasureOverride(Size verfuegbar)
    {
        FontSize = Passend(verfuegbar.Width);
        TabellenAnpassen(verfuegbar.Width);
        var groesse = base.MeasureOverride(verfuegbar);
        // Der Zeilenumbruch trennt ein Wort manchmal schon, wenn es nur zusammen mit dem Wort davor
        // nicht passt ("12. Netzwerkkomponenten"): dann in kleinen Schritten weiter verkleinern.
        while (!double.IsInfinity(verfuegbar.Width) && FontSize > Untergrenze && Wortbruch())
        {
            FontSize = Math.Max(Untergrenze, FontSize - 0.5);
            groesse = base.MeasureOverride(verfuegbar);
        }
        return groesse;
    }

    /// <summary>
    /// Die endgueltige Breite kann schmaler sein als die beim Messen angebotene (Bildlaufleiste,
    /// Spalten): dann neu messen lassen.
    /// </summary>
    protected override Size ArrangeOverride(Size endgueltig)
    {
        var ergebnis = base.ArrangeOverride(endgueltig);
        if (FontSize > Untergrenze && Wortbruch())
        {
            FontSize = Math.Max(Untergrenze, Math.Min(Passend(endgueltig.Width), FontSize - 0.5));
            InvalidateMeasure();
        }
        return ergebnis;
    }

    /// <summary>Endet eine Zeile mitten in einem Wort (Regel der Layoutpruefung)?</summary>
    private bool Wortbruch()
    {
        var text = Rohtext;
        var zeilen = TextLayout.TextLines;
        for (var i = 0; i < zeilen.Count - 1; i++)
        {
            var ende = zeilen[i].FirstTextSourceIndex + zeilen[i].Length;
            if (ende > 0 && ende < text.Length && char.IsLetterOrDigit(text[ende - 1]) && char.IsLetterOrDigit(text[ende]))
            {
                return true;
            }
        }
        return false;
    }

    private double Passend(double breite)
    {
        var groesse = _soll;
        if (!double.IsInfinity(breite) && breite > 0 && Rohtext is { Length: > 0 } text)
        {
            if (_gemessen is not { } g || g.Text != text || g.Groesse != _soll)
            {
                _gemessen = g = (text, _soll, LaengstesWortHier(text, _soll));
            }
            if (g.Breite > breite)
            {
                groesse = Math.Max(Untergrenze, Math.Floor(_soll * breite / g.Breite * 10) / 10 - 0.1);
            }
        }
        return groesse;
    }

    private sealed class Beobachter(Action<object?> weiter) : IObserver<object?>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(object? value) => weiter(value);
    }
}
