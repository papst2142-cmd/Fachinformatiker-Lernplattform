using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Dashboard;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// A 0.12.2 (Weisung P6-16, Frage 3 A): Kalender "Lerntage" wie CalendarPanel am Handy: Pfeile fuer
/// Vor- und Folgemonat (Kreis 34, card_alt, Symbol accent), Monatsname mittig (15 fett), Wochentage
/// (11, Sa/So accent2), Tage als Kreise 28 in Zellen 32 (Lerntag purple gefuellt mit sidebar-farbener
/// fetter Zahl, heute Ring 2 accent, Wochenende accent2, sonst text_dim). Blaettern liest nur
/// (<see cref="DashboardWerte.Monat"/>), schreibt nichts.
/// </summary>
public sealed class LerntageKalender : StackPanel
{
    private DashboardKalender _monat;
    private readonly TextBlock _titel = Schrift.Pt("", 15, FontWeight.Bold);
    private readonly Grid _tage = new() { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*,*") };

    public LerntageKalender(DashboardKalender monat)
    {
        _monat = monat;
        Spacing = 6;
        _titel.TextAlignment = TextAlignment.Center;
        _titel.VerticalAlignment = VerticalAlignment.Center;
        var kopf = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var zurueck = Pfeil("CHEVRON_LEFT", KalenderBlaettern.Zurueck, -1);
        var weiter = Pfeil("CHEVRON_RIGHT", KalenderBlaettern.Weiter, 1);
        Grid.SetColumn(_titel, 1);
        Grid.SetColumn(weiter, 2);
        kopf.Children.Add(zurueck);
        kopf.Children.Add(_titel);
        kopf.Children.Add(weiter);
        Children.Add(kopf);
        Children.Add(_tage);
        Zeigen();
    }

    /// <summary>Dauer des letzten Blaetterns bis die Tage gesetzt sind (Millisekunden).</summary>
    public double LetzteDauer { get; private set; }

    public DashboardKalender Monat => _monat;

    /// <summary>Blaettert um einen Monat vor oder zurueck (liest die Lerntage im Hintergrund).</summary>
    public async Task BlaetternAsync(int schritt)
    {
        var uhr = Stopwatch.StartNew();
        var (jahr, monat) = KalenderBlaettern.Verschieben(_monat.Jahr, _monat.Monat, schritt);
        var pfad = Path.Combine(Ablage.Datenordner(), Lernstand.DateiName);
        _monat = await Task.Run(() => DashboardWerte.Monat(pfad, jahr, monat));
        Zeigen();
        LetzteDauer = uhr.Elapsed.TotalMilliseconds;
    }

    private Button Pfeil(string symbol, string name, int schritt)
    {
        var knopf = new Button
        {
            Content = Symbole.Block(symbol, 18, "Pinsel.accent"),
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(999),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        }.Breite(34).Farbe(Button.BackgroundProperty, "Pinsel.card_alt");
        knopf.Bind(Layoutable.HeightProperty, knopf.GetResourceObservable("Schrift.Normal", v => v is double d ? 34 * d / Schrift.Grundgroessen["Normal"] : 34));
        ToolTip.SetTip(knopf, name);
        Avalonia.Automation.AutomationProperties.SetName(knopf, name);
        knopf.Click += async (_, _) => await BlaetternAsync(schritt);
        return knopf;
    }

    private void Zeigen()
    {
        _titel.Text = _monat.Titel;
        _tage.Children.Clear();
        _tage.RowDefinitions.Clear();
        _tage.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        const string Namen = "MDMDFSS";
        for (var i = 0; i < 7; i++)
        {
            var name = Schrift.Pt(Namen[i].ToString(), 11, FontWeight.Normal, i > 4 ? "Pinsel.accent2" : "Pinsel.muted");
            name.TextAlignment = TextAlignment.Center;
            Grid.SetColumn(name, i);
            _tage.Children.Add(name);
        }
        var heute = DateTime.Today;
        var zeile = 0;
        foreach (var woche in _monat.Wochen)
        {
            zeile++;
            _tage.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var i = 0; i < woche.Count; i++)
            {
                var tag = woche[i];
                var zelle = new Border().HoeheMitSchrift(32);
                Grid.SetColumn(zelle, i);
                Grid.SetRow(zelle, zeile);
                _tage.Children.Add(zelle);
                if (tag == 0)
                {
                    continue;
                }
                var gelernt = _monat.Lerntage.Contains(tag);
                var istHeute = _monat.Jahr == heute.Year && _monat.Monat == heute.Month && tag == heute.Day;
                var zahl = Schrift.Pt(tag.ToString(CultureInfo.InvariantCulture), 12, gelernt ? FontWeight.Bold : FontWeight.Normal,
                    gelernt ? "Pinsel.sidebar" : i > 4 ? "Pinsel.accent2" : "Pinsel.text_dim");
                zahl.TextAlignment = TextAlignment.Center;
                zahl.HorizontalAlignment = HorizontalAlignment.Center;
                zahl.VerticalAlignment = VerticalAlignment.Center;
                var kreis = new Border
                {
                    CornerRadius = new CornerRadius(999),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    BorderThickness = new Thickness(istHeute ? 2 : 0),
                    Child = zahl,
                }.Breite(28).HoeheMitSchrift(28);
                if (gelernt)
                {
                    kreis.Farbe(Border.BackgroundProperty, "Pinsel.purple");
                }
                if (istHeute)
                {
                    kreis.Farbe(Border.BorderBrushProperty, "Pinsel.accent");
                }
                Avalonia.Automation.AutomationProperties.SetName(kreis, tag.ToString(CultureInfo.InvariantCulture) + ". " + _monat.Titel);
                zelle.Child = kreis;
            }
        }
    }
}
