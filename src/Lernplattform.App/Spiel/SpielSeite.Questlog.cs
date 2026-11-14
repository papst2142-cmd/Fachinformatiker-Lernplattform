using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Views;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 0.67.0 (Weisung P12-204, Sammelliste 301, Kurzplan P12-118 Abschnitt 3): Handy-Knopf unten links (ersetzt den
/// Klapp-Pfeil, der an den rechten Rand der unteren Karte rueckt) mit der Zahl der offenen Auftraege; er oeffnet das Questlog
/// als Fenster im Aussehen eines Handys (PC und Handy gleich, am PC auch mit der Taste der Tastenbelegung, Vorgabe L). Nur
/// Anzeige (<see cref="Questlog"/>); „Hingehen“ fuehrt die Figur zum Ort des Auftrags wie die Ortsliste (ohne 3D: sofort da).
/// </summary>
public sealed partial class SpielSeite
{
    private Button? _questKnopf;

    private TextBlock? _questZahl;

    private Border? _questZahlRand;

    /// <summary>Gerade offenes Questlog (Start-Test): Reiter Erledigt gewaehlt.</summary>
    public bool QuestlogErledigt { get; private set; }

    /// <summary>Gerade gezeigte Eintraege des Questlogs (Start-Test).</summary>
    public IReadOnlyList<Questlog.Eintrag> QuestlogEintraege { get; private set; } = [];

    /// <summary>Hingehen-Knoepfe des offenen Questlogs (Start-Test).</summary>
    public IReadOnlyList<Button> QuestlogHingehen { get; private set; } = [];

    /// <summary>Der Handy-Knopf (Start-Test, Fotos).</summary>
    public Button? QuestKnopf => _questKnopf;

    private Button QuestKnopfBauen()
    {
        var symbol = Symbole.Block("smartphone_rounded", 26, "Pinsel.text");
        _questZahl = new TextBlock { FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center }.Groesse("Klein");
        _questZahl.Farbe(TextBlock.ForegroundProperty, "Pinsel.text");
        // gruene Zahl: gruener Rand um die Zahl (Kontrast der Schrift bleibt wie jeder Text)
        _questZahlRand = new Border
        {
            Child = _questZahl,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(6, 0),
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _questZahlRand.Farbe(Border.BorderBrushProperty, "Pinsel.green");
        var knopf = Schrift.Knopf("");
        knopf.Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { symbol, _questZahlRand } };
        knopf.Padding = new Thickness(10, 4);
        knopf.VerticalAlignment = VerticalAlignment.Top;
        knopf.Click += (_, _) => _ = QuestlogZeigenAsync();
        _questKnopf = knopf;
        return knopf;
    }

    /// <summary>Zahl am Knopf und Name fuer den Screenreader (bei jedem Beschriften).</summary>
    private void QuestKnopfSetzen(StadtSpiel spiel)
    {
        if (_questKnopf is null || _questZahl is null || _questZahlRand is null)
        {
            return;
        }
        var zahl = Questlog.ZahlOffen(spiel);
        _questZahl.Text = zahl.ToString(CultureInfo.InvariantCulture);
        _questZahlRand.IsVisible = zahl > 0;
        Avalonia.Automation.AutomationProperties.SetName(_questKnopf, string.Format(CultureInfo.InvariantCulture, VorleseTexte.Auftraege, zahl));
        _questKnopf.IsVisible = !spiel.Einrichten;
    }

    /// <summary>Das Questlog oeffnen (Knopf, Taste); <paramref name="erledigt"/> = gleich den Reiter „Erledigt“ zeigen.</summary>
    public async Task QuestlogZeigenAsync(bool erledigt = false)
    {
        if (Spiel is not { } spiel || Dialog.Offen is not null)
        {
            return;
        }
        var offen = Questlog.Offen(spiel);
        var fertig = Questlog.Erledigte(spiel);
        var liste = new StackPanel { Spacing = 10 };
        var reiterOffen = Schrift.Knopf(string.Format(CultureInfo.InvariantCulture, QuestlogTexte.Offen, offen.Count));
        var reiterErledigt = Schrift.Knopf(string.Format(CultureInfo.InvariantCulture, QuestlogTexte.Erledigt, fertig.Count));
        string? ziel = null;
        void Fuellen(bool zeigeErledigt)
        {
            QuestlogErledigt = zeigeErledigt;
            liste.Children.Clear();
            var eintraege = zeigeErledigt ? fertig : offen;
            QuestlogEintraege = eintraege;
            var knoepfe = new List<Button>();
            ReiterSetzen(reiterOffen, !zeigeErledigt);
            ReiterSetzen(reiterErledigt, zeigeErledigt);
            if (eintraege.Count == 0)
            {
                liste.Children.Add(Schrift.Text(Questlog.LeerText(spiel, zeigeErledigt)));
            }
            foreach (var e in eintraege)
            {
                var teil = new StackPanel { Spacing = 4 };
                var kopf = new WrapPanel();
                kopf.Children.Add(new TextBlock { Text = (e.Erledigt ? "✓ " : "") + e.Titel, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 8, 0) }
                    .Groesse("Normal").Farbe(TextBlock.ForegroundProperty, "Pinsel.text"));
                if (e.NeuHeute)
                {
                    var neu = new Border { Child = Schrift.Text(QuestlogTexte.NeuHeute, "Klein", FontWeight.Bold), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(999), Padding = new Thickness(8, 0) };
                    neu.Farbe(Border.BorderBrushProperty, "Pinsel.green");
                    kopf.Children.Add(neu);
                }
                teil.Children.Add(kopf);
                teil.Children.Add(Schrift.Text(string.Format(CultureInfo.InvariantCulture, QuestlogTexte.Von, e.Questgeber), "Klein"));
                teil.Children.Add(Schrift.Text(string.Format(CultureInfo.InvariantCulture, QuestlogTexte.Wo, e.OrtText), "Klein"));
                if (e.Wartet.Length > 0)
                {
                    teil.Children.Add(Schrift.Text(e.Wartet, "Klein"));
                }
                if (e.Ticket.Length > 0)
                {
                    teil.Children.Add(Schrift.Text(e.Ticket, "Klein", farbe: "Pinsel.muted"));
                }
                if (Questlog.KetteText(e) is { Length: > 0 } kette)
                {
                    teil.Children.Add(Schrift.Text(kette, "Klein"));
                }
                if (!e.Erledigt && e.Ziel is { } ort)
                {
                    var hin = Schrift.Knopf(QuestlogTexte.Hingehen, betont: true);
                    hin.HorizontalAlignment = HorizontalAlignment.Left;
                    Avalonia.Automation.AutomationProperties.SetName(hin,
                        string.Format(CultureInfo.InvariantCulture, VorleseTexte.AuftragHingehen, e.Titel, spiel.Name(ort)));
                    hin.Click += (_, _) =>
                    {
                        ziel = ort;
                        Dialog.AlleSchliessen();
                    };
                    knoepfe.Add(hin);
                    teil.Children.Add(hin);
                }
                var karte = Schrift.Karte(teil, "Pinsel.card");
                karte.Padding = new Thickness(12);
                liste.Children.Add(karte);
            }
            QuestlogHingehen = knoepfe;
        }
        reiterOffen.Click += (_, _) => Fuellen(false);
        reiterErledigt.Click += (_, _) => Fuellen(true);
        Fuellen(erledigt);
        // Aussehen eines Handys: dicker runder Rahmen, oben ein Lautsprecher-Schlitz
        var schlitz = new Border { Width = 56, Height = 6, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
        schlitz.Farbe(Border.BackgroundProperty, "Pinsel.border_hi");
        var handy = new Border
        {
            CornerRadius = new CornerRadius(28),
            BorderThickness = new Thickness(6),
            Padding = new Thickness(12),
            MaxWidth = 460,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new StackPanel
            {
                Spacing = 8,
                Children = { schlitz, new WrapPanel { Children = { reiterOffen, reiterErledigt } }, liste },
            },
        };
        handy.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border_hi");
        await Dialog.Zeigen(QuestlogTexte.Titel, handy, QuestlogTexte.Schliessen);
        QuestlogEintraege = [];
        QuestlogHingehen = [];
        if (ziel is { } hinZu && Spiel is { } jetzt)
        {
            jetzt.GeheZu(hinZu);
            if (_ersatz.IsVisible)
            {
                jetzt.SofortAnkommen();
            }
            Flaeche.AnfordernBild();
        }
    }

    private Grid? _klappZeile;

    /// <summary>Start-Test: stehen die Knoepfe des Orts unter dem Handy-Knopf (schmale Karte, grosse Schrift)?</summary>
    public bool KnoepfeUnterHandy => _knoepfeRolle is { } r && Grid.GetRow(r) == 1;

    /// <summary>
    /// Zwischen Handy-Knopf und Klapp-Pfeil bleibt weniger Platz als vorher neben dem Pfeil allein. Passt dort das laengste
    /// Wort eines Knopfs nicht (schmales Handy, grosse Schrift), stehen die Knoepfe in einer eigenen Zeile darunter ueber die
    /// ganze Breite (sonst wuerden Woerter getrennt, Layoutpruefung); der Kurztext bleibt in der Mitte.
    /// </summary>
    private void QuestZeileSetzen()
    {
        if (_klappZeile is not { } zeile || _questKnopf is null || zeile.Bounds.Width <= 0)
        {
            return;
        }
        var frei = zeile.Bounds.Width - (_questKnopf.IsVisible ? _questKnopf.DesiredSize.Width + _questKnopf.Margin.Left + _questKnopf.Margin.Right : 0)
            - (_klapp.IsVisible ? _klapp.DesiredSize.Width + _klapp.Margin.Left + _klapp.Margin.Right : 0);
        var unten = LaengsterKnopf() > frei;
        Grid.SetRow(_knoepfeRolle, unten ? 1 : 0);
        Grid.SetColumn(_knoepfeRolle, unten ? 0 : 1);
        Grid.SetColumnSpan(_knoepfeRolle, unten ? 3 : 1);
        _knoepfeRolle.Margin = new Thickness(0, unten ? 6 : 0, 0, 0);
        // der Kurztext (zugeklappt) ebenso, wenn sein laengstes Wort nicht dazwischen passt
        var kurzUnten = _kurz.IsVisible && LaengstesWort(_kurz) + _kurz.Margin.Left + _kurz.Margin.Right > frei;
        Grid.SetRow(_kurz, kurzUnten ? 1 : 0);
        Grid.SetColumn(_kurz, kurzUnten ? 0 : 1);
        Grid.SetColumnSpan(_kurz, kurzUnten ? 3 : 1);
    }

    private double LaengstesWort(TextBlock t)
    {
        var skala = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var breit = 0.0;
        foreach (var wort in (t.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var mass = new TextBlock { Text = wort, FontFamily = t.FontFamily, FontSize = t.FontSize, FontWeight = t.FontWeight, FontStyle = t.FontStyle };
            mass.Measure(Size.Infinity);
            breit = Math.Max(breit, (Math.Ceiling(mass.DesiredSize.Width * skala) / skala) + 1);
        }
        return breit;
    }

    /// <summary>Breite des breitesten Knopfs, wenn nur sein laengstes Wort in einer Zeile steht (mit Rand, auf Bildpunkte gerundet).</summary>
    private double LaengsterKnopf()
    {
        var skala = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var breit = 0.0;
        foreach (var knopf in _knoepfe.GetLogicalDescendants().OfType<Button>().Where(b => b.IsVisible))
        {
            foreach (var t in knopf.GetLogicalDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text)))
            {
                foreach (var wort in t.Text!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var mass = new TextBlock { Text = wort, FontFamily = t.FontFamily, FontSize = t.FontSize, FontWeight = t.FontWeight, FontStyle = t.FontStyle };
                    mass.Measure(Size.Infinity);
                    var w = Math.Ceiling(mass.DesiredSize.Width * skala) / skala + knopf.Padding.Left + knopf.Padding.Right
                        + knopf.BorderThickness.Left + knopf.BorderThickness.Right + knopf.Margin.Left + knopf.Margin.Right + t.Margin.Left + t.Margin.Right + 1;
                    breit = Math.Max(breit, w);
                }
            }
        }
        return breit;
    }

    private static void ReiterSetzen(Button knopf, bool gewaehlt)
    {
        knopf.Farbe(Button.BackgroundProperty, gewaehlt ? "VerlaufW.primary" : "Pinsel.card_alt");
        knopf.BorderThickness = new Thickness(gewaehlt ? 0 : 1);
        if (knopf.Content is TextBlock t)
        {
            t.Farbe(TextBlock.ForegroundProperty, gewaehlt ? "Pinsel.on_accent" : "Pinsel.text");
            Avalonia.Automation.AutomationProperties.SetName(knopf, t.Text + (gewaehlt ? ", gewählt" : ""));
        }
    }
}
