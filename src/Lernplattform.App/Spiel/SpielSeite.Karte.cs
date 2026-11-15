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
/// Welle A 0.80.0 (Weisung P12-268, Nico Antwort 5 „Ort antippen = Figur laeuft sofort los“, P12-258 „Das Orts Menue wandert
/// zur Karte im Handy“): Reiter „Karte“ im Spiel-Handy. Oben die gezeichnete Karte (<see cref="StadtKarteBild"/>), darunter eine
/// Legende und die Liste aller Orte mit derselben Nummer wie auf der Karte; ein Knopf je Ort geht sofort hin, „Mit dem Bus“ steht
/// dabei, wenn der Bus schneller ist (wie bisher in der Ortsliste). Seit A 0.81.0 (Sammelliste 350, Weisung P12-274) gibt es die
/// Karte nur noch im Handy (kein Knopf „Karte“ in der Leiste); ohne 3D bleibt die Ortsliste mit dem Knopf „Orte“ die Bedienung.
/// </summary>
public sealed partial class SpielSeite
{
    /// <summary>Welcher Reiter des Handys gerade gezeigt wird (Start-Test, Fotos).</summary>
    public HandyReiter HandyReiterGerade { get; private set; }

    /// <summary>Ist das Spiel-Handy gerade offen?</summary>
    public bool HandyOffen { get; private set; }

    /// <summary>Die gerade gezeigte Karte im Handy oder null (Start-Test, Fotos).</summary>
    public StadtKarteBild? KarteGezeigt { get; private set; }

    /// <summary>Knoepfe der Liste unter der Karte je Ort (Start-Test).</summary>
    public IReadOnlyDictionary<string, Button> KarteHingehen { get; private set; } = new Dictionary<string, Button>();

    /// <summary>Knoepfe „Mit dem Bus“ unter der Karte je Ort, nur wo der Bus schneller ist (Start-Test).</summary>
    public IReadOnlyDictionary<string, Button> KarteMitBus { get; private set; } = new Dictionary<string, Button>();

    /// <summary>
    /// Start-Test (Sammelliste 350): Beschriftungen aller sichtbaren Knoepfe der unteren Leiste (Knoepfe des Orts und „Spiel
    /// verlassen“; ohne Handy-Knopf und Klapp-Pfeil, die keinen Text tragen).
    /// </summary>
    public IReadOnlyList<string> LeisteKnoepfe =>
        [.. _knoepfe.GetLogicalDescendants().OfType<Button>().Append(VerlassenKnopf).Where(k => k.IsEffectivelyVisible).Select(KnopfText).Where(t => t.Length > 0)];

    private static string KnopfText(Button knopf) => knopf.Content switch
    {
        string text => text,
        TextBlock block => block.Text ?? "",
        _ => string.Join(" ", knopf.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t))),
    };

    /// <summary>Knopf „Orte“ (nur ohne 3D und draussen); die Karte gibt es seit A 0.81.0 nur im Spiel-Handy (Sammelliste 350).</summary>
    private void OrteKnoepfeSetzen(StadtSpiel spiel)
    {
        _orteKnopf.IsVisible = !spiel.ImInnenraum && _ersatz.IsVisible;
    }

    /// <summary>Inhalt des Reiters Karte in <paramref name="liste"/> bauen.</summary>
    private void KarteFuellen(StadtSpiel spiel, StackPanel liste, IReadOnlyList<Questlog.Eintrag> offen, Action<string> hin, Action<string> mitBus)
    {
        var aufgaben = offen.Where(e => !e.Erledigt && e.Ziel is not null).Select(e => e.Ziel!).ToHashSet(StringComparer.Ordinal);
        var karte = new StadtKarte(spiel.Szene, aufgaben);
        var bild = new StadtKarteBild { HorizontalAlignment = HorizontalAlignment.Stretch };
        bild.Setzen(karte, StadtKarte.Figur(spiel));
        bild.OrtGewaehlt += hin;
        KarteGezeigt = bild;
        liste.Children.Add(Schrift.Text(KarteTexte.Antippen, "Klein"));
        liste.Children.Add(bild);
        liste.Children.Add(Legende());
        var knoepfe = new Dictionary<string, Button>(StringComparer.Ordinal);
        var busKnoepfe = new Dictionary<string, Button>(StringComparer.Ordinal);
        var hier = spiel.ImFreien && !spiel.ImInnenraum ? null : spiel.Jetzt.Ort;
        foreach (var ort in karte.Orte)
        {
            var id = ort.Id;
            var teil = new StackPanel { Spacing = 4 };
            var knopf = Schrift.Knopf(string.Format(CultureInfo.InvariantCulture, KarteTexte.Eintrag, ort.Nr, ort.Name), betont: ort.Aufgabe);
            knopf.HorizontalAlignment = HorizontalAlignment.Left;
            AutomationName(knopf, string.Format(CultureInfo.InvariantCulture, ort.Aufgabe ? KarteTexte.EintragNameAufgabe : KarteTexte.EintragName, ort.Name));
            knopf.Click += (_, _) => hin(id);
            knoepfe[id] = knopf;
            teil.Children.Add(knopf);
            if (ort.Aufgabe)
            {
                teil.Children.Add(Schrift.Text(KarteTexte.AufgabeWartet, "Klein", farbe: "Pinsel.muted"));
            }
            if (id == hier)
            {
                teil.Children.Add(Schrift.Text(KarteTexte.HierBistDu, "Klein", farbe: "Pinsel.muted"));
            }
            // wie die Ortsliste seit A 0.67.0 (Nico 5a): Zeiten und „Mit dem Bus“ nur, wenn der Bus schneller ist
            if (spiel.Linie is not null && !spiel.Geht && spiel.BusLohnt(id) is { } l)
            {
                var zeit = Schrift.Text(string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.BusZeiten, l.ZuFuss, l.Bus), "Klein");
                zeit.TextWrapping = TextWrapping.Wrap;
                var bus = Schrift.Knopf(SpielProbeTexte.MitDemBus);
                bus.HorizontalAlignment = HorizontalAlignment.Left;
                AutomationName(bus, string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.MitDemBusName, ort.Name, l.Bus));
                bus.Click += (_, _) => mitBus(id);
                busKnoepfe[id] = bus;
                teil.Children.Add(zeit);
                teil.Children.Add(bus);
            }
            liste.Children.Add(teil);
        }
        KarteHingehen = knoepfe;
        KarteMitBus = busKnoepfe;
    }

    private static void AutomationName(Control c, string name) => Avalonia.Automation.AutomationProperties.SetName(c, name);

    /// <summary>Legende unter der Karte als echte Texte (Kontrast und Screenreader wie jeder Text).</summary>
    private static WrapPanel Legende()
    {
        Border Kreis(string farbe, string text, bool ring = false)
        {
            var b = new Border
            {
                Width = 18 * Schrift.Faktor,
                Height = 18 * Schrift.Faktor,
                CornerRadius = new CornerRadius(999),
                BorderThickness = new Thickness(ring ? 3 : 0),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Child = text.Length == 0 ? null : new TextBlock
                {
                    Text = text,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White,
                    FontSize = 11 * Schrift.Faktor,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            var c = Color.Parse(farbe);
            if (ring)
            {
                b.BorderBrush = new SolidColorBrush(c);
            }
            else
            {
                b.Background = new SolidColorBrush(c);
            }
            return b;
        }
        StackPanel Teil(Border k, string text) => new()
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 14, 4),
            Children = { k, Schrift.Text(text, "Klein") },
        };
        return new WrapPanel
        {
            Children =
            {
                Teil(Kreis("#14AAD7", "", ring: true), KarteTexte.Du),
                Teil(Kreis("#1E7842", "H"), KarteTexte.Haltestelle),
                Teil(Kreis("#8B3DF0", "!"), KarteTexte.Aufgabe),
            },
        };
    }
}
