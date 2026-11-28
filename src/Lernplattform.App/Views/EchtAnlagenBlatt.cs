using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// A 1.20.0 (W2, Weisung P12-446, Entwurf P12-267 Bilder 01 und 05 bis 11): Blaetter der neuen Anlagen. Ein dunkler Kasten mit
/// Zeilennummern am Rand (<see cref="ZeilenKasten"/>) fuer die Code-Anlage eines Schreibtischtests (mit gelb markierter Zeile),
/// die Ausgabe (Eingabezeilen gruen und fett) und den Datenauszug (CSV "Wie im Beleg" oder "Als Tabelle", JSON). Nie umgebrochen,
/// seitlich wischen; "Groß öffnen" (<see cref="AnlageGross"/>) fuer Ausgabe und Datenauszug (Nico 3a).
/// </summary>
public static class EchtAnlagenBlatt
{
    /// <summary>Code-Anlage eines Schreibtischtests: Zeilennummern am Rand, Markierung der Zeile des gewaehlten Schritts.</summary>
    public static CodeBlatt Code(EchtAnlage anlage) => new(anlage);

    /// <summary>Ausgabe-Anlage mit Hinweis und den Knoepfen "Groß öffnen" und "Als Text vorlesen".</summary>
    public static StackPanel Ausgabe(EchtAnlage anlage, Action<EchtAnlage>? gross, bool vorlesenOffen = false)
    {
        var teil = new StackPanel { Spacing = 8 };
        teil.Children.Add(AusgabeKasten(anlage));
        teil.Children.Add(Schrift.Text(anlage.Eingaben.Count > 0 ? EchtTexte.AusgabeHinweis : EchtTexte.AusgabeHinweisOhne, "Klein",
            FontWeight.Normal, "Pinsel.muted"));
        var vorlesen = new StackPanel { Spacing = 2, IsVisible = vorlesenOffen };
        foreach (var zeile in Vorlesetext(anlage))
        {
            vorlesen.Children.Add(new LernText(zeile, "Klein", FontWeight.Normal, "Pinsel.text_soft"));
        }
        var knopfVorlesen = LernTeile.Knopf(vorlesenOffen ? EchtTexte.VorlesenAus : EchtTexte.Vorlesen);
        Avalonia.Automation.AutomationProperties.SetName(knopfVorlesen, vorlesenOffen ? EchtTexte.VorlesenAus : EchtTexte.Vorlesen);
        knopfVorlesen.Click += (_, _) =>
        {
            vorlesen.IsVisible = !vorlesen.IsVisible;
            var name = vorlesen.IsVisible ? EchtTexte.VorlesenAus : EchtTexte.Vorlesen;
            ((TextBlock)knopfVorlesen.Content!).Text = name;
            Avalonia.Automation.AutomationProperties.SetName(knopfVorlesen, name);
            if (vorlesen.IsVisible)
            {
                Ansage.Sagen(string.Join("\n", Vorlesetext(anlage)));
            }
        };
        var knoepfe = new WrapPanel();
        if (gross is not null)
        {
            knoepfe.Children.Add(GrossKnopf(() => gross(anlage)));
        }
        knoepfe.Children.Add(knopfVorlesen);
        teil.Children.Add(knoepfe);
        teil.Children.Add(vorlesen);
        return teil;
    }

    /// <summary>Kasten der Ausgabe (auch im grossen Blatt).</summary>
    public static ZeilenKasten AusgabeKasten(EchtAnlage anlage) =>
        new(anlage.Zeilen.Select((z, i) => (Nummer(i + 1), z, anlage.Eingaben.Contains(i + 1) ? Stil.Eingabe : Stil.Normal)).ToList(),
            Beschriftungen.Einsetzen(EchtTexte.Anlage, anlage.Nr, anlage.Titel));

    /// <summary>Zeilen zum Vorlesen ("Zeile 1, Eingabe: …").</summary>
    public static IReadOnlyList<string> Vorlesetext(EchtAnlage anlage) => anlage.Zeilen
        .Select((z, i) => Beschriftungen.Einsetzen(anlage.Eingaben.Contains(i + 1) ? EchtTexte.VorlesenEingabe : EchtTexte.VorlesenZeile, i + 1,
            z.Trim().Length == 0 ? EchtTexte.VorlesenLeer : z.Trim()))
        .ToList();

    /// <summary>
    /// Datenauszug: CSV mit Umschalter "Wie im Beleg" / "Als Tabelle" (<paramref name="tabelle"/>, gemerkt ueber
    /// <paramref name="umgeschaltet"/>), JSON mit Zeilennummern; dazu "Groß öffnen".
    /// </summary>
    public static StackPanel Daten(EchtAnlage anlage, bool tabelle, Action<bool> umgeschaltet, Action<EchtAnlage>? gross)
    {
        var teil = new StackPanel { Spacing = 8 };
        var inhalt = new ContentControl();
        if (anlage.DatenFormat == EchtDaten.Csv)
        {
            var umschalter = Umschalter(tabelle, an =>
            {
                inhalt.Content = DatenInhalt(anlage, an);
                umgeschaltet(an);
            });
            teil.Children.Add(umschalter);
        }
        inhalt.Content = DatenInhalt(anlage, tabelle);
        teil.Children.Add(inhalt);
        teil.Children.Add(Schrift.Text(DatenAngabe(anlage), "Klein", FontWeight.Normal, "Pinsel.muted"));
        if (gross is not null)
        {
            teil.Children.Add(GrossKnopf(() => gross(anlage)));
        }
        return teil;
    }

    /// <summary>Unterzeile eines Datenauszugs ("6 Zeilen · 6 Spalten · Zeile 1 = Kopf").</summary>
    public static string DatenAngabe(EchtAnlage anlage) => anlage.DatenFormat == EchtDaten.Csv
        ? Beschriftungen.Einsetzen(EchtTexte.CsvAngabe, anlage.Felder.Count, anlage.Felder[0].Count)
        : anlage.Fehlerhaft ? EchtTexte.JsonFehlerhaft : EchtTexte.JsonHinweis;

    /// <summary>Inhalt eines Datenauszugs: Rohtext mit Zeilennummern oder (CSV) die Tabelle.</summary>
    public static Control DatenInhalt(EchtAnlage anlage, bool tabelle) => anlage.DatenFormat == EchtDaten.Csv && tabelle
        ? new CsvTabelle(anlage)
        : new ZeilenKasten(anlage.Zeilen.Select((z, i) => (Nummer(i + 1), z, Stil.Normal)).ToList(),
            Beschriftungen.Einsetzen(EchtTexte.Anlage, anlage.Nr, anlage.Titel));

    /// <summary>Zwei Knoepfe "Wie im Beleg" / "Als Tabelle" (gewaehlt mit Rand in Cyan wie im Entwurf).</summary>
    public static WrapPanel Umschalter(bool tabelle, Action<bool> gewechselt)
    {
        var reihe = new WrapPanel();
        var beleg = Pille(EchtTexte.WieImBeleg);
        var tab = Pille(EchtTexte.AlsTabelle);
        void Zeigen(bool an)
        {
            PilleSetzen(beleg, !an);
            PilleSetzen(tab, an);
        }
        Zeigen(tabelle);
        beleg.Click += (_, _) =>
        {
            Zeigen(false);
            gewechselt(false);
        };
        tab.Click += (_, _) =>
        {
            Zeigen(true);
            gewechselt(true);
        };
        reihe.Children.Add(beleg);
        reihe.Children.Add(tab);
        return reihe;
    }

    /// <summary>Kleine Pille mit Rand (Umschalter und A−/A+ im grossen Blatt).</summary>
    public static Button Pille(string text, string? name = null)
    {
        var knopf = new Button
        {
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }.Groesse().Farbe(TextBlock.ForegroundProperty, "Pinsel.text"),
            Padding = new Thickness(14, 4),
            Margin = new Thickness(0, 0, 8, 8),
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
        }.Farbe(Button.BackgroundProperty, "Pinsel.card_alt").Farbe(Button.BorderBrushProperty, "Pinsel.border_hi");
        Avalonia.Automation.AutomationProperties.SetName(knopf, name ?? text);
        return knopf;
    }

    private static void PilleSetzen(Button knopf, bool gewaehlt)
    {
        knopf.Farbe(Button.BorderBrushProperty, gewaehlt ? "Pinsel.cyan" : "Pinsel.border_hi");
        knopf.BorderThickness = new Thickness(gewaehlt ? 2 : 1);
        knopf.Padding = gewaehlt ? new Thickness(13, 3) : new Thickness(14, 4);
        // Screenreader: gewaehlt ansagen
        var text = ((TextBlock)knopf.Content!).Text ?? "";
        Avalonia.Automation.AutomationProperties.SetName(knopf, gewaehlt ? text + ", gewählt" : text);
    }

    /// <summary>Knopf "Groß öffnen".</summary>
    public static Button GrossKnopf(Action oeffnen)
    {
        var knopf = LernTeile.Knopf(EchtTexte.GrossOeffnen);
        Avalonia.Automation.AutomationProperties.SetName(knopf, EchtTexte.GrossOeffnenName);
        knopf.HorizontalAlignment = HorizontalAlignment.Left;
        knopf.Click += (_, _) => oeffnen();
        return knopf;
    }

    internal static string Nummer(int n) => n.ToString(CultureInfo.InvariantCulture);

    public enum Stil
    {
        Normal,
        Eingabe,
    }
}

/// <summary>
/// Dunkler Kasten mit Zeilennummern am Rand (grau, rechtsbuendig) und Text in fester Zeichenbreite, nie umgebrochen. Ist er zu
/// breit, laesst er sich seitlich wischen ("→ wischen" darunter, Platz fuer die Leiste unter der letzten Zeile).
/// </summary>
public sealed class ZeilenKasten : StackPanel
{
    private readonly Grid _raster = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
    private readonly ScrollViewer _rollen;
    private readonly TextBlock _hinweis;
    private readonly List<(Border Flaeche, Border Strich, TextBlock Nummer)> _zeilen = [];
    private int _markiert = -1;

    public ZeilenKasten(IReadOnlyList<(string Nummer, string Text, EchtAnlagenBlatt.Stil Stil)> zeilen, string name)
    {
        Spacing = 2;
        for (var i = 0; i < zeilen.Count; i++)
        {
            var (nr, text, stil) = zeilen[i];
            _raster.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var flaeche = new Border { Opacity = 0.18, IsVisible = false, Margin = new Thickness(-14, 0, -14, 0) }.Farbe(Border.BackgroundProperty, "Pinsel.yellow");
            var strich = new Border { Width = 4, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(-14, 0, 0, 0) }
                .Farbe(Border.BackgroundProperty, "Pinsel.yellow");
            Grid.SetRow(flaeche, i);
            Grid.SetColumnSpan(flaeche, 2);
            Grid.SetRow(strich, i);
            _raster.Children.Add(flaeche);
            _raster.Children.Add(strich);
            var nummer = Zeile(nr, "Pinsel.muted", FontWeight.Normal);
            nummer.TextAlignment = TextAlignment.Right;
            // Rand ueber einen Rahmen (Margin am TextBlock zaehlt in der Layoutpruefung als abgeschnitten)
            var nummerRand = new Border { Padding = new Thickness(0, 1, 14, 1), Child = nummer };
            Grid.SetRow(nummerRand, i);
            _raster.Children.Add(nummerRand);
            var eingabe = stil == EchtAnlagenBlatt.Stil.Eingabe;
            var inhalt = Zeile(text, eingabe ? "Pinsel.green" : "Pinsel.text", eingabe ? FontWeight.Bold : FontWeight.Normal);
            var inhaltRand = new Border { Padding = new Thickness(0, 1), Child = inhalt };
            Grid.SetRow(inhaltRand, i);
            Grid.SetColumn(inhaltRand, 1);
            _raster.Children.Add(inhaltRand);
            _zeilen.Add((flaeche, strich, nummer));
        }
        _rollen = new ScrollViewer
        {
            Content = _raster,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        var kasten = new Border
        {
            Child = _rollen,
            Padding = new Thickness(14, 10),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
        }.Farbe(Border.BackgroundProperty, "Pinsel.bg").Farbe(Border.BorderBrushProperty, "Pinsel.border");
        Avalonia.Automation.AutomationProperties.SetName(kasten, name);
        _hinweis = new TextBlock { Text = RechnerTexte.Wischen, TextAlignment = TextAlignment.Right, HorizontalAlignment = HorizontalAlignment.Stretch, IsVisible = false }
            .Groesse("Klein").Farbe(TextBlock.ForegroundProperty, "Pinsel.muted");
        Children.Add(kasten);
        Children.Add(_hinweis);
        _rollen.LayoutUpdated += (_, _) => Pruefen();
    }

    private static TextBlock Zeile(string text, string farbe, FontWeight gewicht) =>
        new TextBlock { Text = text, FontFamily = Festbreite.Schrift, FontWeight = gewicht, TextWrapping = TextWrapping.NoWrap }
            .Groesse().Farbe(TextBlock.ForegroundProperty, farbe);

    /// <summary>Der seitlich verschiebbare Bereich.</summary>
    public ScrollViewer Rollen => _rollen;

    /// <summary>Steht "→ wischen" darunter?</summary>
    public bool WischHinweis => _hinweis.IsVisible;

    /// <summary>Markierte Zeile (Index ab 0, -1 = keine).</summary>
    public int Markiert => _markiert;

    /// <summary>Zeile (Index ab 0) gelb markieren, -1 = keine.</summary>
    public void Markieren(int index)
    {
        if (_markiert >= 0)
        {
            var (f, s, n) = _zeilen[_markiert];
            f.IsVisible = s.IsVisible = false;
            n.Farbe(TextBlock.ForegroundProperty, "Pinsel.muted");
            n.FontWeight = FontWeight.Normal;
        }
        _markiert = index >= 0 && index < _zeilen.Count ? index : -1;
        if (_markiert >= 0)
        {
            var (f, s, n) = _zeilen[_markiert];
            f.IsVisible = s.IsVisible = true;
            n.Farbe(TextBlock.ForegroundProperty, "Pinsel.yellow");
            n.FontWeight = FontWeight.Bold;
        }
    }

    private void Pruefen()
    {
        var wischen = _rollen.Extent.Width > _rollen.Viewport.Width + 0.5;
        if (_hinweis.IsVisible != wischen)
        {
            _hinweis.IsVisible = wischen;
            _raster.Margin = wischen ? new Thickness(0, 0, 0, Dialog.RandFuerLeiste) : default;
        }
    }
}

/// <summary>Code-Anlage eines Schreibtischtests: Kasten mit Zeilennummern und darunter "Markiert: Zeile 4 (gehört zu deinem Schritt 9)".</summary>
public sealed class CodeBlatt : StackPanel
{
    private readonly ZeilenKasten _kasten;
    private readonly TextBlock _markiert;
    private readonly IReadOnlyDictionary<int, int> _nummern;

    public CodeBlatt(EchtAnlage anlage)
    {
        Spacing = 8;
        _nummern = EchtSchreibtisch.Zeilennummern(anlage);
        var zeilen = anlage.Zeilen.Select((z, i) =>
        {
            var nr = _nummern.FirstOrDefault(p => p.Value == i);
            if (_nummern.Values.Contains(i))
            {
                // Nummer aus dem Text an den Rand (der Text der Zeile bleibt sonst genau gleich)
                var rest = z.TrimStart();
                rest = rest[EchtAnlagenBlatt.Nummer(nr.Key).Length..];
                rest = rest.StartsWith("  ", StringComparison.Ordinal) ? rest[2..] : rest.TrimStart(' ');
                return (EchtAnlagenBlatt.Nummer(nr.Key), rest, EchtAnlagenBlatt.Stil.Normal);
            }
            return ("", z, EchtAnlagenBlatt.Stil.Normal);
        }).ToList();
        _kasten = new ZeilenKasten(zeilen, Beschriftungen.Einsetzen(EchtTexte.Anlage, anlage.Nr, anlage.Titel));
        _markiert = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.muted");
        _markiert.IsVisible = false;
        Children.Add(_kasten);
        Children.Add(_markiert);
    }

    public ZeilenKasten Kasten => _kasten;

    public string MarkiertText => _markiert.IsVisible ? _markiert.Text ?? "" : "";

    /// <summary>Zeile <paramref name="zeile"/> des Codes markieren (Schritt ab 0); unbekannte Zeile = keine Markierung.</summary>
    public void Markieren(int zeile, int schritt)
    {
        if (!_nummern.TryGetValue(zeile, out var index))
        {
            _kasten.Markieren(-1);
            _markiert.IsVisible = false;
            return;
        }
        _kasten.Markieren(index);
        _markiert.Text = Beschriftungen.Einsetzen(EchtTexte.CodeMarkiert, zeile, schritt + 1);
        _markiert.IsVisible = true;
    }
}

/// <summary>CSV als Tabelle: Spalte "Zeile" (Nummer im Beleg), Kopf fett, leere Felder als "(leer)"; zu breit = wischen mit Zahl der verdeckten Spalten.</summary>
public sealed class CsvTabelle : StackPanel
{
    private readonly Grid _raster = new();
    private readonly ScrollViewer _rollen;
    private readonly TextBlock _hinweis;

    public CsvTabelle(EchtAnlage anlage)
    {
        Spacing = 2;
        var spalten = anlage.Felder[0].Count;
        for (var s = 0; s <= spalten; s++)
        {
            _raster.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }
        _raster.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        for (var z = 0; z < anlage.Felder.Count; z++)
        {
            _raster.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var flaeche = new Border { BorderThickness = new Thickness(0, 0, 0, 1) }
                .Farbe(Border.BorderBrushProperty, "Pinsel.border");
            if (z == 0)
            {
                flaeche.Farbe(Border.BackgroundProperty, "Pinsel.card_hi");
            }
            Grid.SetRow(flaeche, z);
            Grid.SetColumnSpan(flaeche, spalten + 2);
            _raster.Children.Add(flaeche);
            var nummer = Schrift.Text(z == 0 ? EchtTexte.SchreibtischZeile : EchtAnlagenBlatt.Nummer(z + 1), "Normal",
                z == 0 ? FontWeight.Bold : FontWeight.Normal, z == 0 ? "Pinsel.text" : "Pinsel.muted");
            nummer.TextWrapping = TextWrapping.NoWrap;
            nummer.TextAlignment = TextAlignment.Right;
            nummer.HorizontalAlignment = HorizontalAlignment.Right;
            Setzen(nummer, z, 0);
            for (var s = 0; s < spalten; s++)
            {
                var wert = anlage.Felder[z][s];
                var leer = wert.Trim().Length == 0;
                var zelle = Schrift.Text(leer ? EchtTexte.DatenLeer : wert, "Normal", z == 0 ? FontWeight.Bold : FontWeight.Normal,
                    z == 0 ? "Pinsel.text" : leer ? "Pinsel.muted" : "Pinsel.text");
                zelle.TextWrapping = TextWrapping.NoWrap;
                Setzen(zelle, z, s + 1);
            }
        }
        _rollen = new ScrollViewer
        {
            Content = _raster,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Avalonia.Automation.AutomationProperties.SetName(_rollen, Beschriftungen.Einsetzen(EchtTexte.Anlage, anlage.Nr, anlage.Titel));
        _hinweis = new TextBlock { TextAlignment = TextAlignment.Right, HorizontalAlignment = HorizontalAlignment.Stretch, IsVisible = false }
            .Groesse("Klein").Farbe(TextBlock.ForegroundProperty, "Pinsel.muted");
        Children.Add(_rollen);
        Children.Add(_hinweis);
        _rollen.ScrollChanged += (_, _) => Pruefen();
        _rollen.LayoutUpdated += (_, _) => Pruefen();
    }

    public ScrollViewer Rollen => _rollen;

    public string Hinweis => _hinweis.IsVisible ? _hinweis.Text ?? "" : "";

    private void Setzen(Control inhalt, int z, int s)
    {
        // Rand ueber einen Rahmen (Margin am TextBlock zaehlt in der Layoutpruefung als abgeschnitten)
        var rand = new Border { Padding = new Thickness(12, 6), Child = inhalt };
        Grid.SetRow(rand, z);
        Grid.SetColumn(rand, s);
        _raster.Children.Add(rand);
    }

    private void Pruefen()
    {
        var rechts = _rollen.Offset.X + _rollen.Viewport.Width;
        var x = 0.0;
        var verdeckt = 0;
        for (var c = 0; c < _raster.ColumnDefinitions.Count - 1; c++)
        {
            var breite = _raster.ColumnDefinitions[c].ActualWidth;
            if (c > 0 && x + breite > rechts + 0.5)
            {
                verdeckt++;
            }
            x += breite;
        }
        var wischen = _rollen.Extent.Width > _rollen.Viewport.Width + 0.5;
        var text = !wischen ? "" : verdeckt == 1 ? EchtTexte.WischenEine : verdeckt > 1
            ? Beschriftungen.Einsetzen(EchtTexte.WischenWeitere, verdeckt) : RechnerTexte.Wischen;
        if (_hinweis.Text != text)
        {
            _hinweis.Text = text;
        }
        if (_hinweis.IsVisible != wischen)
        {
            _hinweis.IsVisible = wischen;
            _raster.Margin = wischen ? new Thickness(0, 0, 0, Dialog.RandFuerLeiste) : default;
        }
    }
}

/// <summary>
/// "Groß öffnen" (Kurzplan P12-267 D, Nico 3a, Entwurf Bild 11): am PC ein eigenes Fenster ueber der Seite, das sich am Kopf
/// verschieben und an der Ecke groesser ziehen laesst; die Aufgabe daneben bleibt bedienbar. Am Handy (schmal) der ganze
/// Bildschirm mit "Schließen". Die Uhr laeuft weiter. A−/A+ aendern die Groesse des Inhalts.
/// </summary>
public static class AnlageGross
{
    private static Panel? _ebene;
    private static Border? _offen;

    /// <summary>Von MainView gesetzt: Ebene ueber den Seiten, unter den Rueckfragen.</summary>
    internal static void Ebene(Panel ebene) => _ebene = ebene;

    /// <summary>Ist ein grosses Blatt offen? (Start-Test)</summary>
    public static Border? Offen => _offen;

    /// <summary>Am Handy (schmal) ganzer Bildschirm?</summary>
    public static bool GanzerBildschirm { get; private set; }

    public static void Schliessen()
    {
        if (_ebene is { } ebene && _offen is { } offen)
        {
            ebene.Children.Clear();
            ebene.IsVisible = false;
            _offen = null;
            _ = offen;
        }
    }

    /// <summary>
    /// Grosses Blatt oeffnen: <paramref name="inhalt"/> baut den Inhalt (neu bei A−/A+ nicht noetig, er wird skaliert),
    /// <paramref name="werkzeuge"/> steht vor A−/A+ (z. B. der CSV-Umschalter), <paramref name="aufgabe"/> = Nummer der Aufgabe.
    /// </summary>
    public static void Oeffnen(string titel, Control inhalt, Control? werkzeuge, int aufgabe)
    {
        if (_ebene is not { } ebene)
        {
            return;
        }
        Schliessen();
        // Groesse vom sichtbaren Elternteil: die eigene Ebene war unsichtbar und behaelt alte Masse (z. B. nach Drehen)
        var flaecheGesamt = (ebene.Parent as Visual)?.Bounds ?? ebene.Bounds;
        var breite = flaecheGesamt.Width;
        GanzerBildschirm = breite < 720 * Schrift.Faktor;
        var zoom = 1.0;
        var skaliert = new LayoutTransformControl { Child = inhalt, LayoutTransform = new ScaleTransform(1, 1) };
        void Zoomen(double neu)
        {
            zoom = Math.Clamp(neu, 0.7, 2.0);
            skaliert.LayoutTransform = new ScaleTransform(zoom, zoom);
        }
        var kleiner = EchtAnlagenBlatt.Pille(EchtTexte.Kleiner, EchtTexte.KleinerName);
        kleiner.Click += (_, _) => Zoomen(zoom - 0.15);
        var groesser = EchtAnlagenBlatt.Pille(EchtTexte.Groesser, EchtTexte.GroesserName);
        groesser.Click += (_, _) => Zoomen(zoom + 0.15);
        var leiste = new WrapPanel();
        if (werkzeuge is WrapPanel reihe)
        {
            foreach (var k in reihe.Children.ToList())
            {
                reihe.Children.Remove(k);
                leiste.Children.Add(k);
            }
        }
        else if (werkzeuge is not null)
        {
            leiste.Children.Add(werkzeuge);
        }
        leiste.Children.Add(kleiner);
        leiste.Children.Add(groesser);
        var titelText = Schrift.Text(GanzerBildschirm ? titel : titel + " " + EchtTexte.GrossFenster, "Mittel", FontWeight.Bold, "Pinsel.text");
        titelText.VerticalAlignment = VerticalAlignment.Center;
        var kreuz = new Button
        {
            Content = new TextBlock { Text = GanzerBildschirm ? "× " + EchtTexte.GrossSchliessen : "×", FontWeight = FontWeight.Bold }
                .Groesse("Mittel").Farbe(TextBlock.ForegroundProperty, "Pinsel.text_dim"),
            Background = Brushes.Transparent,
            Padding = new Thickness(10, 4),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Avalonia.Automation.AutomationProperties.SetName(kreuz, EchtTexte.GrossSchliessen);
        ToolTip.SetTip(kreuz, EchtTexte.GrossSchliessen);
        kreuz.Click += (_, _) => Schliessen();
        // ab 160 % untereinander: der Knopf oben rechts, der Titel darunter mit der ganzen Breite (sonst trennt er Woerter)
        var untereinander = Schrift.Faktor >= Schrift.UntereinanderAb;
        var kopf = untereinander ? new Grid { RowDefinitions = new RowDefinitions("Auto,Auto") } : new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        kopf.Children.Add(titelText);
        if (untereinander)
        {
            kreuz.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetRow(titelText, 1);
        }
        else
        {
            Grid.SetColumn(kreuz, 1);
        }
        kopf.Children.Add(kreuz);
        var kopfRahmen = new Border { Child = kopf, Padding = new Thickness(16, 8, 8, 8), BorderThickness = new Thickness(0, 0, 0, 1), Background = Brushes.Transparent }
            .Farbe(Border.BorderBrushProperty, "Pinsel.border");
        var hinweis = Schrift.Text(GanzerBildschirm ? Beschriftungen.Einsetzen(EchtTexte.GrossHinweisGanz, aufgabe) : EchtTexte.GrossHinweisFenster,
            "Normal", FontWeight.Normal, "Pinsel.muted");
        var rollen = new ScrollViewer
        {
            Content = new StackPanel { Spacing = 12, Margin = new Thickness(16, 12, 16 + Dialog.RandFuerLeiste, 16), Children = { leiste, skaliert, hinweis } },
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var innen = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        innen.Children.Add(kopfRahmen);
        Grid.SetRow(rollen, 1);
        innen.Children.Add(rollen);
        var fenster = new Border
        {
            Child = innen,
            BorderThickness = new Thickness(GanzerBildschirm ? 0 : 1),
            CornerRadius = new CornerRadius(GanzerBildschirm ? 0 : 14),
            ClipToBounds = true,
        }.Farbe(Border.BackgroundProperty, "Pinsel.bg").Farbe(Border.BorderBrushProperty, "Pinsel.border_hi");
        Avalonia.Automation.AutomationProperties.SetName(fenster, titel);
        ebene.Children.Clear();
        if (GanzerBildschirm)
        {
            ebene.Children.Add(fenster);
        }
        else
        {
            // Seite leicht abgedunkelt wie im Entwurf, bleibt aber bedienbar (kein Klickfang)
            ebene.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)), IsHitTestVisible = false });
            var flaeche = new Canvas();
            var w = Math.Min(Math.Max(breite * 0.55, 420), breite - 32);
            var h = Math.Min(Math.Max(flaecheGesamt.Height * 0.55, 320), flaecheGesamt.Height - 32);
            fenster.Width = w;
            fenster.Height = h;
            Canvas.SetLeft(fenster, Math.Max(16, breite - w - 64));
            Canvas.SetTop(fenster, Math.Min(110, Math.Max(16, flaecheGesamt.Height - h - 16)));
            flaeche.Children.Add(fenster);
            Ziehen(kopfRahmen, fenster, (dx, dy) =>
            {
                Canvas.SetLeft(fenster, Math.Clamp(Canvas.GetLeft(fenster) + dx, 0, Math.Max(0, ebene.Bounds.Width - 80)));
                Canvas.SetTop(fenster, Math.Clamp(Canvas.GetTop(fenster) + dy, 0, Math.Max(0, ebene.Bounds.Height - 60)));
            });
            var ecke = new Border { Width = 22, Height = 22, Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.BottomRightCorner),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
            ecke.Child = new TextBlock { Text = "◢", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom }
                .Farbe(TextBlock.ForegroundProperty, "Pinsel.muted");
            Avalonia.Automation.AutomationProperties.SetName(ecke, EchtTexte.GrossZiehen);
            Grid.SetRowSpan(ecke, 2);
            innen.Children.Add(ecke);
            Ziehen(ecke, fenster, (dx, dy) =>
            {
                fenster.Width = Math.Clamp(fenster.Width + dx, 300, Math.Max(300, ebene.Bounds.Width - Canvas.GetLeft(fenster)));
                fenster.Height = Math.Clamp(fenster.Height + dy, 220, Math.Max(220, ebene.Bounds.Height - Canvas.GetTop(fenster)));
            });
            ebene.Children.Add(flaeche);
        }
        fenster.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Schliessen();
            }
        };
        ebene.IsVisible = true;
        _offen = fenster;
        Ansage.Sagen(titel);
        kreuz.Focus();
    }

    private static void Ziehen(Control griff, Control fenster, Action<double, double> bewegt)
    {
        Point? start = null;
        griff.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(griff).Properties.IsLeftButtonPressed && e.Source is not Button && (e.Source as Visual)?.FindAncestorOfType<Button>() is null)
            {
                start = e.GetPosition(fenster.Parent as Visual);
                e.Pointer.Capture(griff);
                e.Handled = true;
            }
        };
        griff.PointerMoved += (_, e) =>
        {
            if (start is { } s)
            {
                var jetzt = e.GetPosition(fenster.Parent as Visual);
                bewegt(jetzt.X - s.X, jetzt.Y - s.Y);
                start = jetzt;
            }
        };
        griff.PointerReleased += (_, e) =>
        {
            start = null;
            e.Pointer.Capture(null);
        };
    }
}
