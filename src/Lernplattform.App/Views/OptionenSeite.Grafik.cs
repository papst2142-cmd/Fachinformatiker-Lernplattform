using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Welle Grafik G1 (Weisung P12-182, Nico 1a 2c 3a 4b): Karte „Grafik“ in der Gruppe Aussehen. Oben die Stufe (Niedrig,
/// Mittel, Hoch, Eigene, je mit einem Satz; bei Hoch „für starke Geräte“), bei „Eigene“ die Einzelwerte; darunter die
/// Belastung des Geraets aus dem letzten Aufenthalt in der Stadt (<see cref="Belastung.Letzte"/>, „nicht verfügbar“, wo
/// das System nichts meldet) und der Schalter der Leistungsanzeige im Spiel (Merker <c>leistungsanzeige</c>, Vorgabe aus,
/// derselbe wie seit D2; unter Diagnose steht jetzt nur ein Verweis hierher). Merker nur ueber <c>Einstellungen.Aendern</c>.
/// </summary>
public sealed partial class OptionenSeite
{
    private readonly List<RadioButton> _grafikStufen = [];
    private StackPanel? _grafikEigene;
    private Grid? _belastungTabelle;
    private TextBlock? _belastungLeer;
    private ToggleSwitch? _grafikAnzeige;
    private readonly List<(ComboBox Feld, Action Setzen)> _grafikEinzeln = [];

    /// <summary>Start-Test: Wahlknoepfe der Stufen (Reihenfolge Niedrig, Mittel, Hoch, Eigene).</summary>
    internal IReadOnlyList<RadioButton> GrafikStufenKnoepfe => _grafikStufen;

    /// <summary>Start-Test: Schalter der Leistungsanzeige in der Karte.</summary>
    internal ToggleSwitch? GrafikAnzeigeSchalter => _grafikAnzeige;

    /// <summary>Start-Test: Bereich der Einzelwerte von „Eigene“.</summary>
    internal StackPanel? GrafikEigene => _grafikEigene;

    private Control GrafikKarte()
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new LernText(GrafikTexte.Satz, "Klein", FontWeight.Normal, "Pinsel.text_dim"));
        panel.Children.Add(Abschnitt(GrafikTexte.StufeTitel));
        var gewaehlt = Grafikstufen.Lesen(Zustand.Werte) ?? Grafikstufe.Mittel;
        foreach (var stufe in (Grafikstufe[])[Grafikstufe.Niedrig, Grafikstufe.Mittel, Grafikstufe.Hoch, Grafikstufe.Eigene])
        {
            var name = GrafikTexte.Name(stufe);
            var wahl = new RadioButton
            {
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        Schrift.Text(name, "Normal", FontWeight.SemiBold),
                        new LernText(GrafikTexte.Erklaerung(stufe), "Klein", FontWeight.Normal, "Pinsel.muted"),
                    },
                },
                GroupName = "grafik_stufe",
                IsChecked = stufe == gewaehlt,
                Tag = Grafikstufen.Name(stufe),
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            wahl.Farbe(RadioButton.ForegroundProperty, "Pinsel.text");
            Avalonia.Automation.AutomationProperties.SetName(wahl, name + ". " + GrafikTexte.Erklaerung(stufe));
            wahl.IsCheckedChanged += (_, _) =>
            {
                if (_setzen || wahl.IsChecked != true)
                {
                    return;
                }
                var mitEigenen = stufe == Grafikstufe.Eigene && Zustand.Werte[Grafikstufen.SchluesselEigene] is null;
                var vorher = Grafikstufen.Aktuell(Zustand.Werte);
                if (!Ergebnis(Zustand.Aendere(w =>
                {
                    Grafikstufen.Wert(w, stufe);
                    if (mitEigenen)
                    {
                        // „Eigene“ beginnt mit den Werten der bisherigen Stufe
                        Grafikstufen.EigeneWert(w, vorher);
                    }
                })))
                {
                    Zuruecksetzen(_grafikStufen, Grafikstufen.Name(Grafikstufen.Lesen(Zustand.Werte) ?? Grafikstufe.Mittel));
                }
                GrafikNeu();
            };
            _grafikStufen.Add(wahl);
            panel.Children.Add(wahl);
        }
        _grafikEigene = new StackPanel { Spacing = 6, Margin = new Thickness(24, 0, 0, 4) };
        EinzelWahl(GrafikTexte.FernAbTitel, Grafikstufen.FernAbWahl, GrafikTexte.FernAbName, w => w.FernAb, (w, v) => w with { FernAb = v });
        EinzelWahl(GrafikTexte.GruenTitel, Grafikstufen.GruenBisWahl, GrafikTexte.GruenName, w => w.GruenBis, (w, v) => w with { GruenBis = v });
        EinzelWahl(GrafikTexte.AutosTitel, [true, false], h => h ? GrafikTexte.AutosHalb : GrafikTexte.AutosAlle, w => w.AutosHalb, (w, v) => w with { AutosHalb = v });
        EinzelWahl(GrafikTexte.AufloesungTitel, Grafikstufen.AufloesungWahl, GrafikTexte.AufloesungName, w => w.Aufloesung, (w, v) => w with { Aufloesung = v });
        EinzelWahl(GrafikTexte.BilderTitel, Grafikstufen.BilderWahl, GrafikTexte.BilderName, w => w.BilderHoechstens, (w, v) => w with { BilderHoechstens = v });
        panel.Children.Add(_grafikEigene);

        var belastungTitel = Abschnitt(GrafikTexte.BelastungTitel);
        belastungTitel.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(belastungTitel);
        panel.Children.Add(new LernText(GrafikTexte.BelastungSatz, "Klein", FontWeight.Normal, "Pinsel.muted"));
        _belastungLeer = new LernText(GrafikTexte.BelastungNochNicht, "Normal", FontWeight.Normal, "Pinsel.text_dim");
        panel.Children.Add(_belastungLeer);
        _belastungTabelle = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, RowSpacing = 2 };
        panel.Children.Add(_belastungTabelle);

        var hilfe = new LernText(GrafikTexte.AnzeigeHilfe, "Klein", FontWeight.Normal, "Pinsel.muted") { Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(hilfe);
        var schalter = new ToggleSwitch
        {
            Content = Schrift.Text(Leistungsanzeige.Schalter),
            IsChecked = Leistungsanzeige.Lesen(Zustand.Werte),
            OnContent = null,
            OffContent = null,
        };
        Avalonia.Automation.AutomationProperties.SetName(schalter, Leistungsanzeige.Schalter);
        schalter.IsCheckedChanged += (_, _) =>
        {
            if (_setzen)
            {
                return;
            }
            var an = schalter.IsChecked == true;
            if (!Ergebnis(Zustand.Aendere(w => Leistungsanzeige.Wert(w, an))))
            {
                GrafikNeu();
            }
        };
        _grafikAnzeige = schalter;
        panel.Children.Add(schalter);

        // die Belastung aendert sich nur, waehrend das Spiel laeuft; solange die Karte zu sehen ist, jede Sekunde neu lesen
        var takt = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => BelastungNeu());
        panel.AttachedToVisualTree += (_, _) =>
        {
            GrafikNeu();
            takt.Start();
        };
        panel.DetachedFromVisualTree += (_, _) => takt.Stop();
        GrafikNeu();
        return panel;
    }

    /// <summary>Eine Auswahl der Stufe „Eigene“ (Titel darueber, Werte als Liste).</summary>
    private void EinzelWahl<T>(string titel, IReadOnlyList<T> werte, Func<T, string> name, Func<Grafikwerte, T> lesen, Func<Grafikwerte, T, Grafikwerte> mit)
    {
        var feld = new ComboBox { MinWidth = 200, HorizontalAlignment = HorizontalAlignment.Left, ItemsSource = werte.Select(name).ToList() }
            .Farbe(ComboBox.BackgroundProperty, "Pinsel.card_alt").Farbe(ComboBox.ForegroundProperty, "Pinsel.text");
        Avalonia.Automation.AutomationProperties.SetName(feld, titel);
        feld.SelectionChanged += (_, _) =>
        {
            if (_setzen || feld.SelectedIndex < 0)
            {
                return;
            }
            var neu = mit(Grafikstufen.EigeneLesen(Zustand.Werte), werte[feld.SelectedIndex]);
            Ergebnis(Zustand.Aendere(w => Grafikstufen.EigeneWert(w, neu)));
            GrafikNeu();
        };
        void Setzen()
        {
            var aktuell = lesen(Grafikstufen.EigeneLesen(Zustand.Werte));
            var stelle = -1;
            for (var i = 0; i < werte.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(werte[i], aktuell))
                {
                    stelle = i;
                }
            }
            feld.SelectedIndex = stelle;
        }
        _grafikEinzeln.Add((feld, Setzen));
        _grafikEigene!.Children.Add(new StackPanel { Spacing = 2, Children = { LernTeile.Label(Beschriftungen.Gross(titel)), feld } });
    }

    /// <summary>Anzeige der Karte nach dem Merker (Stufe, Einzelwerte, Schalter) und die Belastung neu.</summary>
    private void GrafikNeu()
    {
        if (_grafikEigene is null)
        {
            return;
        }
        _setzen = true;
        var stufe = Grafikstufen.Lesen(Zustand.Werte) ?? Grafikstufe.Mittel;
        foreach (var w in _grafikStufen)
        {
            w.IsChecked = (string?)w.Tag == Grafikstufen.Name(stufe);
        }
        _grafikEigene.IsVisible = stufe == Grafikstufe.Eigene;
        foreach (var (_, setzen) in _grafikEinzeln)
        {
            setzen();
        }
        if (_grafikAnzeige is not null)
        {
            _grafikAnzeige.IsChecked = Leistungsanzeige.Lesen(Zustand.Werte);
        }
        _setzen = false;
        BelastungNeu();
    }

    private Belastung.Werte? _belastungGezeigt;

    /// <summary>Tabelle der Belastung (Werte des letzten Aufenthalts in der Stadt) neu fuellen, wenn sie sich geaendert hat.</summary>
    private void BelastungNeu()
    {
        if (_belastungTabelle is null || _belastungLeer is null)
        {
            return;
        }
        var werte = Belastung.Letzte;
        _belastungLeer.IsVisible = werte is null;
        _belastungTabelle.IsVisible = werte is not null;
        if (werte is null || ReferenceEquals(werte, _belastungGezeigt))
        {
            return;
        }
        _belastungGezeigt = werte;
        _belastungTabelle.Children.Clear();
        _belastungTabelle.RowDefinitions.Clear();
        var zeile = 0;
        foreach (var (name, wert) in GrafikTexte.Zeilen(werte))
        {
            _belastungTabelle.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var links = new LernText(name, "Klein", FontWeight.Normal, "Pinsel.text_dim");
            var rechts = Schrift.Text(wert, "Klein", FontWeight.SemiBold, wert == GrafikTexte.NichtVerfuegbar ? "Pinsel.muted" : "Pinsel.text");
            Grid.SetRow(links, zeile);
            Grid.SetRow(rechts, zeile);
            Grid.SetColumn(rechts, 1);
            _belastungTabelle.Children.Add(links);
            _belastungTabelle.Children.Add(rechts);
            zeile++;
        }
    }
}
