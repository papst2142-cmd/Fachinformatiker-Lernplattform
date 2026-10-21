using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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

    /// <summary>Breite des laengsten Wortes eines Textes in Schrift und Groesse eines Elements.</summary>
    public static double LaengstesWort(string text, Typeface schrift, double groesse) =>
        text.Split(Leerraum, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal)
            .Select(w => new FormattedText(w, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, schrift, groesse, null)
                .WidthIncludingTrailingWhitespace)
            .DefaultIfEmpty(0).Max();

    protected override Size MeasureOverride(Size verfuegbar)
    {
        FontSize = Passend(verfuegbar.Width);
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
        var text = Text ?? "";
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
        if (!double.IsInfinity(breite) && breite > 0 && Text is { Length: > 0 } text)
        {
            if (_gemessen is not { } g || g.Text != text || g.Groesse != _soll)
            {
                _gemessen = g = (text, _soll, LaengstesWort(text, new Typeface(FontFamily, FontStyle, FontWeight), _soll));
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
