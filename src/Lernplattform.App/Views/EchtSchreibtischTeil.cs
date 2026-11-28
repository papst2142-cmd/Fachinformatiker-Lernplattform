using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// A 1.20.0 (W2 Schreibtischtest, Weisung P12-446, Entwurf P12-267 Bilder 01, 02 und 04, Nico 1a): Raster wie auf Papier.
/// Spalte "Zeile" (Zeile im Code) bleibt beim seitlichen Wischen links stehen, je Variable eine Spalte, dazu "Ausgabe".
/// Vorgegebenes steht grau, offene Zellen sind Kaestchen; Eingabetaste oder Tab springt zur naechsten offenen Zelle, die Zeile
/// mit dem Fokus ist gelb markiert (und ueber <see cref="ZeileGewaehlt"/> im Code). Nach dem Abgeben (<c>bewertet</c>) ist jedes
/// Kaestchen gruen oder rot, bei Rot steht der richtige Wert darunter.
/// </summary>
public sealed class EchtSchreibtischTeil : StackPanel
{
    private readonly EchtAntwort _a;
    private readonly Grid _raster = new();
    private readonly ScrollViewer _rollen;
    private readonly TextBlock _wischen;
    private readonly List<Control> _festLinks = [];
    private readonly List<(Border Flaeche, Border Links, TextBlock Nummer)> _zeilen = [];
    private readonly List<TextBox> _felder = [];
    private int _gewaehlt = -1;

    /// <summary>
    /// <paramref name="wert"/>: Eingabe einer Zelle (Schritt, Spalte); <paramref name="feld"/>: meldet jedes Eingabefeld mit
    /// Schritt und Spalte (zum Speichern); null = bewertet (nur lesen).
    /// </summary>
    public EchtSchreibtischTeil(EchtAntwort a, Func<int, int, string> wert, Action<TextBox, int, int>? feld, IReadOnlyList<bool>? richtig = null,
        string flaeche = "Pinsel.card")
    {
        _a = a;
        Spacing = 2;
        var bewertet = feld is null;
        _raster.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        foreach (var _ in a.Spalten)
        {
            _raster.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)) { MinWidth = 0 });
        }
        _raster.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        // Kopf: Flaeche ueber alle Spalten, "Zeile" fest links
        var kopfFlaeche = new Border { BorderThickness = new Thickness(0, 0, 0, 1) }
            .Farbe(Border.BackgroundProperty, "Pinsel.card_hi").Farbe(Border.BorderBrushProperty, "Pinsel.border_hi");
        Grid.SetColumnSpan(kopfFlaeche, a.Spalten.Count + 1);
        _raster.Children.Add(kopfFlaeche);
        _raster.Children.Add(Fest(Kopf(EchtTexte.SchreibtischZeile), 0, 0, "Pinsel.card_hi"));
        for (var s = 0; s < a.Spalten.Count; s++)
        {
            Setzen(Kopf(a.Spalten[s]), 0, s + 1);
        }
        var k = 0;
        for (var z = 0; z < a.Schritte.Count; z++)
        {
            var schritt = a.Schritte[z];
            var reihe = z + 1;
            _raster.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            // Trennlinie und Markierung der gewaehlten Zeile (gelb, hinter den Zellen)
            var linie = new Border { BorderThickness = new Thickness(0, 0, 0, 1) }.Farbe(Border.BorderBrushProperty, "Pinsel.border");
            Grid.SetRow(linie, reihe);
            Grid.SetColumnSpan(linie, a.Spalten.Count + 1);
            _raster.Children.Add(linie);
            var markierung = new Border { Opacity = 0.16, IsVisible = false }.Farbe(Border.BackgroundProperty, "Pinsel.yellow");
            Grid.SetRow(markierung, reihe);
            Grid.SetColumnSpan(markierung, a.Spalten.Count + 1);
            _raster.Children.Add(markierung);
            var nummer = Schrift.Text(schritt.Zeile.ToString(CultureInfo.InvariantCulture), "Normal", FontWeight.Bold, "Pinsel.accent");
            nummer.TextAlignment = TextAlignment.Center;
            nummer.HorizontalAlignment = HorizontalAlignment.Center;
            nummer.VerticalAlignment = VerticalAlignment.Center;
            var links = new Border { Opacity = 0.16, IsVisible = false }.Farbe(Border.BackgroundProperty, "Pinsel.yellow");
            _zeilen.Add((markierung, links, nummer));
            _raster.Children.Add(Fest(new Panel { Children = { links, new Border { Padding = Festrand, Child = nummer } } }, reihe, 0, flaeche));
            for (var s = 0; s < schritt.Werte.Count; s++)
            {
                var zelle = schritt.Werte[s];
                Control inhalt;
                if (!zelle.Offen)
                {
                    var text = Schrift.Text(zelle.Fest!, "Normal", FontWeight.Normal, "Pinsel.text_soft");
                    text.TextAlignment = TextAlignment.Center;
                    text.HorizontalAlignment = HorizontalAlignment.Center;
                    text.VerticalAlignment = VerticalAlignment.Center;
                    inhalt = text;
                }
                else if (bewertet)
                {
                    inhalt = Bewertet(wert(z, s), richtig is not null && k < richtig.Count && richtig[k], zelle.Loesung[0]);
                    k++;
                }
                else
                {
                    var (zz, ss) = (z, s);
                    var box = new TextBox
                    {
                        Text = wert(z, s),
                        TextWrapping = TextWrapping.NoWrap,
                        TextAlignment = TextAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(6, 4),
                        CornerRadius = new CornerRadius(8),
                    }.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt").Groesse().Breite(68);
                    var name = EchtSchreibtisch.ZellName(a, z, s);
                    void Benennen() => Avalonia.Automation.AutomationProperties.SetName(box,
                        name + ((box.Text ?? "").Trim().Length == 0 ? EchtTexte.SchreibtischLeer : ""));
                    Benennen();
                    // auch beim Setzen von aussen (TextChanged kommt dann erst spaeter): sofort neu benennen
                    box.PropertyChanged += (_, e) =>
                    {
                        if (e.Property == TextBox.TextProperty)
                        {
                            Benennen();
                        }
                    };
                    box.GotFocus += (_, _) => Waehlen(zz);
                    box.KeyDown += (_, e) =>
                    {
                        if (e.Key == Key.Enter)
                        {
                            e.Handled = true;
                            Weiter(box);
                        }
                    };
                    _felder.Add(box);
                    feld!(box, z, s);
                    inhalt = box;
                }
                Setzen(inhalt, reihe, s + 1);
            }
        }
        _rollen = new ScrollViewer
        {
            Content = _raster,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        _wischen = new TextBlock { TextAlignment = TextAlignment.Right, HorizontalAlignment = HorizontalAlignment.Stretch, IsVisible = false, TextWrapping = TextWrapping.Wrap }
            .Groesse("Klein").Farbe(TextBlock.ForegroundProperty, "Pinsel.muted");
        Children.Add(_rollen);
        Children.Add(_wischen);
        _rollen.ScrollChanged += (_, _) => Anpassen();
        _rollen.LayoutUpdated += (_, _) => Anpassen();
    }

    /// <summary>Gewaehlter Schritt (Fokus in einer seiner Zellen): Schritt ab 0 und Zeile im Code.</summary>
    public event Action<int, int>? ZeileGewaehlt;

    /// <summary>Start-Test: die Eingabefelder in der Reihenfolge der Bedienung (Schritt fuer Schritt).</summary>
    public IReadOnlyList<TextBox> Felder => _felder;

    /// <summary>Start-Test: gewaehlter Schritt (-1 = keiner) und Text des Hinweises „→ wischen …“ (leer, wenn alles passt).</summary>
    public (int Schritt, string Wischen) ProbeStand => (_gewaehlt, _wischen.IsVisible ? _wischen.Text ?? "" : "");

    /// <summary>Start-Test: der seitlich verschiebbare Bereich.</summary>
    public ScrollViewer Rollen => _rollen;

    /// <summary>Schritt waehlen wie ein Fokus in einer seiner Zellen (Probe und Bedienung).</summary>
    public void Waehlen(int schritt)
    {
        if (_gewaehlt == schritt)
        {
            return;
        }
        if (_gewaehlt >= 0)
        {
            _zeilen[_gewaehlt].Flaeche.IsVisible = false;
            _zeilen[_gewaehlt].Links.IsVisible = false;
            _zeilen[_gewaehlt].Nummer.Farbe(TextBlock.ForegroundProperty, "Pinsel.accent");
        }
        _gewaehlt = schritt;
        if (schritt >= 0)
        {
            _zeilen[schritt].Flaeche.IsVisible = true;
            _zeilen[schritt].Links.IsVisible = true;
            _zeilen[schritt].Nummer.Farbe(TextBlock.ForegroundProperty, "Pinsel.yellow");
            ZeileGewaehlt?.Invoke(schritt, _a.Schritte[schritt].Zeile);
        }
    }

    /// <summary>Eingabetaste: naechste offene Zelle (nach der letzten bleibt der Fokus).</summary>
    public void Weiter(TextBox von)
    {
        var i = _felder.IndexOf(von);
        if (i >= 0 && i + 1 < _felder.Count)
        {
            _felder[i + 1].Focus(NavigationMethod.Tab);
        }
    }

    private static TextBlock Kopf(string text)
    {
        var kopf = Schrift.Text(text, "Normal", FontWeight.Bold, "Pinsel.text");
        kopf.TextWrapping = TextWrapping.NoWrap;
        kopf.TextAlignment = TextAlignment.Center;
        kopf.HorizontalAlignment = HorizontalAlignment.Center;
        kopf.VerticalAlignment = VerticalAlignment.Center;
        return kopf;
    }

    private void Setzen(Control inhalt, int reihe, int spalte)
    {
        // Rand ueber einen Rahmen (Margin am TextBlock zaehlt in der Layoutpruefung als abgeschnitten)
        var rand = new Border { Padding = new Thickness(10, 7), Child = inhalt };
        Grid.SetRow(rand, reihe);
        Grid.SetColumn(rand, spalte);
        _raster.Children.Add(rand);
    }

    private static readonly Thickness Festrand = new(16, 7);

    /// <summary>Zelle der festen Spalte "Zeile": deckende Flaeche, bleibt beim Wischen links stehen.</summary>
    private Border Fest(Control inhalt, int reihe, int spalte, string flaeche)
    {
        var zelle = new Border { Child = inhalt, ZIndex = 2, BorderThickness = new Thickness(0, 0, 0, 1), Padding = inhalt is Panel ? default : Festrand }
            .Farbe(Border.BackgroundProperty, flaeche).Farbe(Border.BorderBrushProperty, reihe == 0 ? "Pinsel.border_hi" : "Pinsel.border");
        Grid.SetRow(zelle, reihe);
        Grid.SetColumn(zelle, spalte);
        _festLinks.Add(zelle);
        return zelle;
    }

    private static StackPanel Bewertet(string eingabe, bool ok, string erwartet)
    {
        var text = Schrift.Text(eingabe.Trim().Length > 0 ? eingabe : EchtTexte.SchreibtischLeerZelle, "Normal", FontWeight.Normal, ok ? "Pinsel.green" : "Pinsel.text");
        text.TextAlignment = TextAlignment.Center;
        var kasten = new Border
        {
            Child = text,
            Padding = new Thickness(8, 3),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
        }.Breite(68).Farbe(Border.BorderBrushProperty, ok ? "Pinsel.green" : "Pinsel.red");
        // Flaeche leicht in der Farbe (wie im Entwurf), Text bleibt gut lesbar
        var tint = new Border { CornerRadius = new CornerRadius(8), Opacity = 0.14, IsHitTestVisible = false }
            .Farbe(Border.BackgroundProperty, ok ? "Pinsel.green" : "Pinsel.red");
        var zelle = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var panel = new Panel { Children = { tint, kasten }, HorizontalAlignment = HorizontalAlignment.Center };
        Avalonia.Automation.AutomationProperties.SetName(kasten, (ok ? EchtTexte.Richtig : EchtTexte.Falsch) + ": " + eingabe);
        zelle.Children.Add(panel);
        if (!ok)
        {
            var richtig = Schrift.Text(Beschriftungen.Einsetzen(EchtTexte.SchreibtischRichtigWert, erwartet), "Klein", FontWeight.Normal, "Pinsel.muted");
            richtig.TextAlignment = TextAlignment.Center;
            richtig.TextWrapping = TextWrapping.NoWrap;
            zelle.Children.Add(richtig);
        }
        return zelle;
    }

    private (double Versatz, double Breite, double Platz) _angepasst = (-1, -1, -1);

    /// <summary>Feste Spalte mitschieben und den Hinweis „→ wischen (Spalten …)“ setzen.</summary>
    private void Anpassen()
    {
        var versatz = _rollen.Offset.X;
        var jetzt = (versatz, _raster.Bounds.Width, _rollen.Viewport.Width);
        if (jetzt == _angepasst || _rollen.Viewport.Width <= 0)
        {
            return;
        }
        _angepasst = jetzt;
        foreach (var zelle in _festLinks)
        {
            zelle.RenderTransform = versatz > 0 ? new TranslateTransform(versatz, 0) : null;
        }
        var rechts = versatz + _rollen.Viewport.Width;
        var links = versatz + (_raster.ColumnDefinitions.Count > 0 ? _raster.ColumnDefinitions[0].ActualWidth : 0);
        var verdeckt = new List<string>();
        var x = 0.0;
        for (var c = 0; c < _raster.ColumnDefinitions.Count; c++)
        {
            var breite = _raster.ColumnDefinitions[c].ActualWidth;
            if (c > 0 && (x + breite > rechts + 0.5 || x < links - 0.5))
            {
                verdeckt.Add(_a.Spalten[c - 1]);
            }
            x += breite;
        }
        var wischen = verdeckt.Count > 0;
        _wischen.Text = wischen ? Beschriftungen.Einsetzen(EchtTexte.SchreibtischWischen, string.Join(", ", verdeckt)) : "";
        if (_wischen.IsVisible != wischen)
        {
            _wischen.IsVisible = wischen;
            // Platz unter der letzten Zeile fuer die waagrechte Scrollleiste (Layoutpruefung 9)
            _raster.Margin = wischen ? new Thickness(0, 0, 0, Dialog.RandFuerLeiste) : default;
        }
    }
}
