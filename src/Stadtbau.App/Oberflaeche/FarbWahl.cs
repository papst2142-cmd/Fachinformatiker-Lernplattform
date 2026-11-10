using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Design;
using Lernplattform.Views;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// S 0.31.0 (Sammelliste 264, Weisung P12-130 Frage 3 = c): Farbwahl eines Gebaeudes fuer „Ausgewähltes Stück“ und den
/// Gebaeude-Editor. Oben die Teile (Fassade, Dach, Rahmen, Tuer), darunter fuer einen Farbteil die 12 festen Farben,
/// „Wie vorher“ und die eigene Farbe mit drei Reglern (Farbton, Saettigung, Helligkeit) und Vorschau; fuer die Tuer die
/// vier Tuer-Farben (Nico 10.11.2026). Jede Wahl ruft <c>setzen</c> (ein Schritt beim Aufrufer). Alle Flaechen sind
/// mindestens 44 px gross (Tablet).
/// </summary>
public sealed class FarbWahl
{
    /// <summary>Kantenlaenge eines Farbfelds (Tablet).</summary>
    public const double Feld = 44;

    private readonly Func<string, string?> _lesen;
    private readonly Action<string, string?> _setzen;
    private readonly StackPanel _inhalt = new() { Spacing = 6 };
    private readonly Slider _ton = Regler(0, 360);
    private readonly Slider _saettigung = Regler(0, 100);
    private readonly Slider _hell = Regler(0, 100);
    private readonly Border _vorschau = new() { Width = Feld, Height = Feld, CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1) };
    private readonly TextBlock _wert = Schrift.Text("", "Klein");
    private readonly StackPanel _eigeneZeile = new() { Orientation = Orientation.Horizontal, Spacing = 8 };

    /// <summary>Zuletzt gewaehlter Teil (bleibt fuer das naechste Gebaeude).</summary>
    public static string Teil { get; set; } = Gebaeudefarben.Teile[0];

    public FarbWahl(Func<string, string?> lesen, Action<string, string?> setzen)
    {
        _lesen = lesen;
        _setzen = setzen;
        _vorschau.Farbe(Border.BorderBrushProperty, "Pinsel.field_border");
        foreach (var r in new[] { _ton, _saettigung, _hell })
        {
            r.ValueChanged += (_, _) => VorschauZeigen();
        }
        Ansicht = new StackPanel { Spacing = 6, Children = { _inhalt } };
        Bauen();
    }

    /// <summary>Die ganze Wahl (in die Karte einhaengen).</summary>
    public StackPanel Ansicht { get; }

    /// <summary>Start-Test: Farbfelder des gezeigten Teils (Name, Knopf).</summary>
    internal List<(string Name, Button Knopf)> Felder { get; } = [];

    /// <summary>Start-Test: Knopf „Übernehmen“ der eigenen Farbe (null bei der Tuer).</summary>
    internal Button? UebernehmenKnopf { get; private set; }

    /// <summary>Start-Test: Regler Farbton, Saettigung, Helligkeit.</summary>
    internal (Slider Ton, Slider Saettigung, Slider Hell) Regler3 => (_ton, _saettigung, _hell);

    private static Slider Regler(double min, double max) => new()
    {
        Minimum = min,
        Maximum = max,
        SmallChange = 1,
        LargeChange = 10,
        MinHeight = Feld,
        MinWidth = 160,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Teil wechseln (auch fuer den Start-Test).</summary>
    public void TeilWaehlen(string teil)
    {
        Teil = teil;
        Bauen();
    }

    private void Bauen()
    {
        _inhalt.Children.Clear();
        Felder.Clear();
        UebernehmenKnopf = null;
        var reiter = new WrapPanel();
        foreach (var t in Gebaeudefarben.AlleSchluessel)
        {
            var name = Gebaeudefarben.TeilName(t);
            var k = Schrift.Knopf(name, betont: t == Teil).Vorlese(t == Teil ? name + ", gewählt" : name).Mit(name);
            k.MinHeight = Feld;
            k.Click += (_, _) => TeilWaehlen(t);
            reiter.Children.Add(k);
        }
        _inhalt.Children.Add(reiter);
        var jetzt = _lesen(Teil);
        var tuer = Teil == Gebaeudefarben.TuerSchluessel;
        var name0 = tuer
            ? Gebaeudefarben.TuerVarianten.FirstOrDefault(v => v.Id == jetzt).Name
            : jetzt is null ? null : Gebaeudefarben.Feste.FirstOrDefault(f => string.Equals(f.Wert, jetzt, StringComparison.OrdinalIgnoreCase)).Name ?? jetzt;
        _inhalt.Children.Add(Schrift.Text(Texte.FarbeJetzt(Gebaeudefarben.TeilName(Teil), name0), "Klein"));
        var felder = new WrapPanel();
        IEnumerable<(string Name, string Wert, string Id)> liste = tuer
            ? Gebaeudefarben.TuerVarianten.Select(v => (v.Name, v.Wert, v.Id))
            : Gebaeudefarben.Feste.Select(f => (f.Name, f.Wert, f.Wert));
        foreach (var (name, wert, id) in liste)
        {
            var gewaehlt = string.Equals(jetzt, id, StringComparison.OrdinalIgnoreCase);
            var flaeche = new Border
            {
                Width = Feld - 8,
                Height = Feld - 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.Parse(wert)),
                BorderThickness = new Thickness(gewaehlt ? 3 : 1),
            }.Farbe(Border.BorderBrushProperty, gewaehlt ? "Pinsel.text" : "Pinsel.field_border");
            var k = new Button { Content = flaeche, MinWidth = Feld, MinHeight = Feld, Padding = new Thickness(2), Margin = new Thickness(0, 0, 4, 4) }
                .Vorlese(gewaehlt ? name + ", gewählt" : name).Mit(name);
            k.Click += (_, _) => Waehlen(id);
            felder.Children.Add(k);
            Felder.Add((name, k));
        }
        _inhalt.Children.Add(felder);
        var vorher = Schrift.Knopf(Texte.FarbeWieVorher).Vorlese(Texte.FarbeWieVorher).Mit(Texte.FarbeWieVorherTipp);
        vorher.MinHeight = Feld;
        vorher.IsEnabled = jetzt is not null;
        vorher.Click += (_, _) => Waehlen(null);
        _inhalt.Children.Add(vorher);
        if (tuer)
        {
            _inhalt.Children.Add(Schrift.Text(Texte.FarbeTuerHinweis, "Klein", farbe: "Pinsel.muted"));
            return;
        }
        // eigene Farbe: Regler stehen auf der jetzigen Farbe (sonst auf einem mittleren Grau)
        var (h, s, v) = jetzt is not null && Gebaeudefarben.Gueltig(jetzt) ? Gebaeudefarben.Hsv(Netz.Farbe(jetzt)) : (30f, 0.2f, 0.7f);
        (_ton.Value, _saettigung.Value, _hell.Value) = (h, s * 100, v * 100);
        _inhalt.Children.Add(Schrift.Text(Texte.FarbeEigene, "Normal", FontWeight.Bold).Mit(Texte.FarbeEigeneTipp));
        foreach (var (titel, r) in new[] { (Texte.FarbeTon, _ton), (Texte.FarbeSaettigung, _saettigung), (Texte.FarbeHell, _hell) })
        {
            r.Vorlese(titel);
            _inhalt.Children.Add(Schrift.Text(titel, "Klein", farbe: "Pinsel.muted"));
            _inhalt.Children.Add(r);
        }
        var uebernehmen = Schrift.Knopf(Texte.FarbeUebernehmen, betont: true).Vorlese(Texte.FarbeUebernehmen).Mit(Texte.FarbeEigeneTipp);
        uebernehmen.MinHeight = Feld;
        uebernehmen.Click += (_, _) => Waehlen(EigeneWert);
        UebernehmenKnopf = uebernehmen;
        // Vorschau und Wert sind feste Elemente: die Zeile wird nur neu befuellt (ein Element hat hoechstens einen Elternteil;
        // eine neue Zeile je Aufbau warf beim zweiten Aufbau, Start-Test S 0.31.0)
        _wert.VerticalAlignment = VerticalAlignment.Center;
        _eigeneZeile.Children.Clear();
        _eigeneZeile.Children.Add(_vorschau);
        _eigeneZeile.Children.Add(_wert);
        _eigeneZeile.Children.Add(uebernehmen);
        _inhalt.Children.Add(_eigeneZeile);
        _inhalt.Children.Add(Schrift.Text(Texte.FarbeFamilieHinweis, "Klein", farbe: "Pinsel.muted"));
        VorschauZeigen();
    }

    /// <summary>Wert der eigenen Farbe aus den Reglern.</summary>
    internal string EigeneWert => Gebaeudefarben.Wert(Gebaeudefarben.AusHsv((float)_ton.Value, (float)(_saettigung.Value / 100), (float)(_hell.Value / 100)));

    private void VorschauZeigen()
    {
        _vorschau.Background = new SolidColorBrush(Color.Parse(EigeneWert));
        _wert.Text = EigeneWert;
    }

    private void Waehlen(string? wert)
    {
        _setzen(Teil, wert);
        Bauen();
    }
}
