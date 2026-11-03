using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lernplattform.Core.Dashboard;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Phase 11 FW10 (Weisung P11-19, Antwort 2a): Themenname in der Themenreihe des Dashboards. Wie
/// <see cref="Schrift.Pt"/> (Groesse folgt der Schriftstufe), aber passt das laengste Wort nicht in die Spalte,
/// wird nur dieser Name verkleinert, hoechstens auf 85 % (<see cref="Themenname.Groesse"/>).
/// </summary>
public sealed class ThemenNameText : TextBlock
{
    private double _soll;

    public ThemenNameText(string text, double groesse, string farbe)
    {
        Text = text;
        TextWrapping = TextWrapping.Wrap;
        _soll = groesse;
        FontSize = groesse;
        this.Farbe(ForegroundProperty, farbe);
        this.GetResourceObservable("Schrift.Normal").Subscribe(new Beobachter(v =>
        {
            if (v is double d)
            {
                _soll = groesse * d / Schrift.Grundgroessen["Normal"];
                FontSize = _soll;
                InvalidateMeasure();
            }
        }));
    }

    /// <summary>Eingestellte Groesse (ohne Verkleinerung).</summary>
    public double Soll => _soll;

    protected override Size MeasureOverride(Size verfuegbar)
    {
        var groesse = _soll;
        if (!double.IsInfinity(verfuegbar.Width) && verfuegbar.Width > 0 && Text is { Length: > 0 } text)
        {
            var laengstes = text.Split(' ', ' ').Max(w => Breite(w, _soll));
            groesse = Themenname.Groesse(_soll, laengstes, verfuegbar.Width);
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
