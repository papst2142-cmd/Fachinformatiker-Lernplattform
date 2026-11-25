using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 9 W4 (Weisung P9-10 Teil B, Sammelliste 178): Karte "Spiel" wie _build_spiel (app_gui.py) und der Bereich
/// "spiel" der Handy-App (mobile/src/main.py) in Python 0.63.2: oben die Zeile zum Platz und Schwierigkeitsgrad
/// (slot_options_text nach game.reload(), fett), darunter WOHNUNGEN mit "Einmalzahlung" / "Miete" (einstellungen.json
/// dieses Geraets, Schluessel spiel_miete, geschrieben nur ueber Einstellungen.Aendern) und der Erklaertext.
/// Die Zeile wird bei jedem Aufklappen der Karte im Hintergrund gelesen (wie _show_difficulty beim Zeigen der Optionen).
/// </summary>
public sealed partial class OptionenSeite
{
    private readonly List<RadioButton> _miete = [];
    private ToggleSwitch? _antippenSchalter;
    private ToggleSwitch? _umsehenLr;
    private ToggleSwitch? _umsehenOb;

    /// <summary>Start-Test: Schalter „Umsehen links/rechts umkehren“ und „oben/unten“ (null ohne aufgeklappte Karte).</summary>
    public (ToggleSwitch? LinksRechts, ToggleSwitch? ObenUnten) UmsehenSchalter => (_umsehenLr, _umsehenOb);

    /// <summary>Start-Test: Schalter „Antippen zum Gehen“ (null ohne aufgeklappte Karte).</summary>
    public ToggleSwitch? AntippenSchalter => _antippenSchalter;
    private TextBlock? _spielZeile;
    private int _spielZeileLauf;

    /// <summary>Start-Test: die angezeigte Zeile zum Platz und Schwierigkeitsgrad (null ohne Karte).</summary>
    public string? SpielZeile => _spielZeile?.Text;

    /// <summary>Start-Test: wird fertig, wenn die Zeile nach dem letzten Anstoss gelesen ist.</summary>
    public Task SpielZeileGelesen { get; private set; } = Task.CompletedTask;

    private Control SpielKarte()
    {
        var panel = new StackPanel { Spacing = 6 };
        _spielZeile = new LernText("", "Normal", FontWeight.Bold, "Pinsel.text_soft");
        panel.Children.Add(_spielZeile);
        var titel = Abschnitt(Wohnung.MieteTitel);
        titel.Margin = new Avalonia.Thickness(0, 4, 0, 0);
        panel.Children.Add(titel);
        var aktuell = Wohnung.MieteGewaehlt(Zustand.Werte) ? "miete" : "einmal";
        var reihe = new WrapPanel();
        foreach (var (id, text) in Wohnung.MieteWahl)
        {
            var wahl = Auswahl(text, "spiel_miete", id == aktuell);
            wahl.Tag = id;
            wahl.IsCheckedChanged += (_, _) =>
            {
                if (_setzen || wahl.IsChecked != true)
                {
                    return;
                }
                if (!Ergebnis(Zustand.Aendere(werte => Wohnung.MieteSetzen(werte, id == "miete"))))
                {
                    Zuruecksetzen(_miete, Wohnung.MieteGewaehlt(Zustand.Werte) ? "miete" : "einmal");
                }
            };
            _miete.Add(wahl);
            reihe.Children.Add(wahl);
        }
        panel.Children.Add(reihe);
        panel.Children.Add(new LernText(Wohnung.MieteHilfe(Lernplattform.Spiel.SpielStart.Balancing()), "Winzig", FontWeight.Normal, "Pinsel.muted"));
        return panel;     // die Zeile oben liest erst das Aufklappen (alle Karten starten zugeklappt)
    }

    /// <summary>
    /// Welle A 0.95.0 (Weisung P12-306, Nico 1c): Schalter „Antippen zum Gehen“ (Standard an); seit A 0.96.0 (Weisung P12-308,
    /// Nico „Der Schalter gehört aber zur Steuerung“) nur noch oben in der Karte „Steuerung“, gleicher Wert und Standard.
    /// </summary>
    private void AntippenSchalterAnhaengen(Panel panel)
    {
        var schalter = new ToggleSwitch
        {
            Content = Schrift.Text(SpielProbeTexte.AntippenGehen),
            IsChecked = Gehbedienung.Lesen(Zustand.Werte),
            OnContent = null,
            OffContent = null,
        };
        Avalonia.Automation.AutomationProperties.SetName(schalter, SpielProbeTexte.AntippenGehen);
        schalter.IsCheckedChanged += (_, _) =>
        {
            if (_setzen)
            {
                return;
            }
            var an = schalter.IsChecked == true;
            if (Ergebnis(Zustand.Aendere(werte => Gehbedienung.Wert(werte, an))))
            {
                Gehbedienung.AntippenAn = an;
            }
            else
            {
                // nicht gespeichert: der Schalter zeigt wieder, was gilt (nie still verloren)
                _setzen = true;
                schalter.IsChecked = Gehbedienung.Lesen(Zustand.Werte);
                _setzen = false;
            }
        };
        _antippenSchalter = schalter;
        panel.Children.Add(schalter);
        panel.Children.Add(new LernText(SpielProbeTexte.AntippenGehenSatz, "Klein", FontWeight.Normal, "Pinsel.muted"));
        panel.Children.Add(new LernText(SpielProbeTexte.SticksSatz, "Klein", FontWeight.Normal, "Pinsel.muted"));
        // Welle A 0.97.0 (Weisung P12-314): zwei Schalter „Umsehen umkehren“, Standard aus
        _umsehenLr = UmsehenSchalterBauen(panel, SpielProbeTexte.UmsehenLinksRechts, Umsehbedienung.SchluesselLinksRechts, an => Umsehbedienung.LinksRechts = an);
        // Welle A 0.98.0 (Weisung P12-315, Sammelliste 397): Abstand zur Gruppe darueber wie zwischen den Gruppen der Optionen
        _umsehenLr.Margin = new Avalonia.Thickness(0, GruppenAbstand, 0, 0);
        _umsehenOb = UmsehenSchalterBauen(panel, SpielProbeTexte.UmsehenObenUnten, Umsehbedienung.SchluesselObenUnten, an => Umsehbedienung.ObenUnten = an);
        panel.Children.Add(new LernText(SpielProbeTexte.UmsehenSatz, "Klein", FontWeight.Normal, "Pinsel.muted"));
        // Welle A 1.1.0 (Weisung P12-328 Punkt 2): Regler „Blickfeld Ich-Sicht“ 60 bis 110 Grad, Vorgabe 90
        BlickfeldReglerAnhaengen(panel);
        // Welle A 1.6.0 (Weisung P12-360 Punkte 6 und 7, Sammelliste 445/446): Regler „Empfindlichkeit“ Maus, linker und rechter Stick
        _empfindlich.Clear();
        EmpfindlichkeitAnhaengen(panel, Empfindlichkeit.Maus, SpielProbeTexte.EmpfindlichkeitMaus);
        EmpfindlichkeitAnhaengen(panel, Empfindlichkeit.StickLinks, SpielProbeTexte.EmpfindlichkeitStickLinks);
        EmpfindlichkeitAnhaengen(panel, Empfindlichkeit.StickRechts, SpielProbeTexte.EmpfindlichkeitStickRechts);
        panel.Children.Add(new LernText(SpielProbeTexte.EmpfindlichkeitSatz, "Klein", FontWeight.Normal, "Pinsel.muted"));
        // Welle A 1.9.0 (Sammelliste 450, Nico 1a): „Knöpfe anordnen“ nur am Handy und Tablet (am PC gibt es die runden Knoepfe nicht)
        if (KnoepfeAnordnenFuerTests ?? !Pc)
        {
            KnoepfeAnordnenAnhaengen(panel);
        }
    }

    private readonly Action? _knoepfeAnordnen;

    /// <summary>Start-Test: den Knopf „Knöpfe anordnen“ auch am PC zeigen (null = nur Handy und Tablet).</summary>
    internal static bool? KnoepfeAnordnenFuerTests { get; set; }

    /// <summary>Start-Test: der Knopf „Knöpfe anordnen“ (null ohne aufgeklappte Karte oder am PC).</summary>
    public Button? KnoepfeAnordnenKnopf { get; private set; }

    private void KnoepfeAnordnenAnhaengen(Panel panel)
    {
        var titel = new LernText(SpielProbeTexte.KnoepfeAnordnen, "Normal", FontWeight.Normal, "Pinsel.text") { Margin = new Avalonia.Thickness(0, GruppenAbstand, 0, 0) };
        var knopf = Schrift.Knopf(SpielProbeTexte.KnoepfeAnordnen);
        knopf.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        knopf.Click += (_, _) => _knoepfeAnordnen?.Invoke();
        KnoepfeAnordnenKnopf = knopf;
        panel.Children.Add(titel);
        panel.Children.Add(knopf);
        panel.Children.Add(new LernText(SpielProbeTexte.KnoepfeAnordnenSatz, "Klein", FontWeight.Normal, "Pinsel.muted"));
    }

    private readonly List<(Empfindlichkeit Art, Slider Regler, TextBlock Zahl)> _empfindlich = [];
    private DispatcherTimer? _empfindlichSpeichern;

    /// <summary>Start-Test: die drei Regler „Empfindlichkeit“ (Maus, linker Stick, rechter Stick) mit Anzeige (leer ohne aufgeklappte Karte).</summary>
    public IReadOnlyList<(Empfindlichkeit Art, Slider Regler, TextBlock Zahl)> EmpfindlichkeitRegler => _empfindlich;

    private void EmpfindlichkeitAnhaengen(Panel panel, Empfindlichkeit art, string name)
    {
        var titel = new LernText(name, "Normal", FontWeight.Normal, "Pinsel.text") { Margin = new Avalonia.Thickness(0, GruppenAbstand, 0, 0) };
        var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        var wert = art.Lesen(Zustand.Werte);
        var regler = new Slider
        {
            Minimum = Empfindlichkeit.Min, Maximum = Empfindlichkeit.Max, Value = wert,
            SmallChange = Empfindlichkeit.Schritt, LargeChange = 4 * Empfindlichkeit.Schritt,
            TickFrequency = Empfindlichkeit.Schritt, IsSnapToTickEnabled = true,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        Avalonia.Automation.AutomationProperties.SetName(regler, name);
        if (Avalonia.Application.Current!.TryFindResource(Akzent(Optionen.Tasten), out var farbe))
        {
            foreach (var n in (string[])["SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed",
                         "SliderThumbBackground", "SliderThumbBackgroundPointerOver", "SliderThumbBackgroundPressed"])
            {
                regler.Resources[n] = farbe;
            }
        }
        var zahl = Schrift.Text(string.Format(SpielProbeTexte.EmpfindlichkeitProzent, wert), "Normal", FontWeight.Bold, "Pinsel.text");
        zahl.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        zahl.MinWidth = 64 * Schrift.Faktor;
        zahl.TextAlignment = TextAlignment.Right;
        regler.ValueChanged += (_, e) =>
        {
            var p = Empfindlichkeit.Begrenzen((int)Math.Round(e.NewValue));
            zahl.Text = string.Format(SpielProbeTexte.EmpfindlichkeitProzent, p);
            if (_setzen)
            {
                return;
            }
            // das Spiel folgt sofort, geschrieben wird erst, wenn der Regler ruht (nicht jeder Zwischenwert)
            art.Prozent = p;
            _empfindlichSpeichern ??= new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => EmpfindlichkeitSpeichern());
            _empfindlichSpeichern.Stop();
            _empfindlichSpeichern.Start();
        };
        _empfindlich.Add((art, regler, zahl));
        Grid.SetColumn(zahl, 1);
        zeile.Children.Add(regler);
        zeile.Children.Add(zahl);
        panel.Children.Add(titel);
        panel.Children.Add(zeile);
    }

    /// <summary>Die Regler „Empfindlichkeit“ speichern (auch sofort fuer den Start-Test); false = nicht gespeichert.</summary>
    public bool EmpfindlichkeitSpeichern()
    {
        _empfindlichSpeichern?.Stop();
        if (_empfindlich.Count == 0)
        {
            return true;
        }
        var werte = _empfindlich.Select(e => (e.Art, P: Empfindlichkeit.Begrenzen((int)Math.Round(e.Regler.Value)))).ToList();
        if (Ergebnis(Zustand.Aendere(w =>
            {
                foreach (var (art, p) in werte)
                {
                    art.Wert(w, p);
                }
            })))
        {
            foreach (var (art, p) in werte)
            {
                art.Prozent = p;
            }
            return true;
        }
        // nicht gespeichert: die Regler zeigen wieder, was gilt (nie still verloren)
        _setzen = true;
        foreach (var (art, regler, _) in _empfindlich)
        {
            var gilt = art.Lesen(Zustand.Werte);
            art.Prozent = gilt;
            regler.Value = gilt;
        }
        _setzen = false;
        return false;
    }

    private Slider? _blickfeld;
    private TextBlock? _blickfeldZahl;
    private DispatcherTimer? _blickfeldSpeichern;

    /// <summary>Start-Test: Regler „Blickfeld Ich-Sicht“ und seine Anzeige (null ohne aufgeklappte Karte).</summary>
    public (Slider? Regler, TextBlock? Zahl) BlickfeldRegler => (_blickfeld, _blickfeldZahl);

    private void BlickfeldReglerAnhaengen(Panel panel)
    {
        // Titel ueber dem Regler (eigene Zeile), damit er bei grosser Schrift nie neben dem Regler zu eng wird
        var titel = new LernText(SpielProbeTexte.Blickfeld, "Normal", FontWeight.Normal, "Pinsel.text") { Margin = new Avalonia.Thickness(0, GruppenAbstand, 0, 0) };
        var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        var wert = Blickfeldbedienung.Lesen(Zustand.Werte);
        var regler = new Slider
        {
            Minimum = Blickfeldbedienung.Min, Maximum = Blickfeldbedienung.Max, Value = wert,
            SmallChange = Blickfeldbedienung.Schritt, LargeChange = 2 * Blickfeldbedienung.Schritt,
            TickFrequency = Blickfeldbedienung.Schritt, IsSnapToTickEnabled = true,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        Avalonia.Automation.AutomationProperties.SetName(regler, SpielProbeTexte.Blickfeld);
        if (Avalonia.Application.Current!.TryFindResource(Akzent(Optionen.Tasten), out var farbe))
        {
            foreach (var n in (string[])["SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed",
                         "SliderThumbBackground", "SliderThumbBackgroundPointerOver", "SliderThumbBackgroundPressed"])
            {
                regler.Resources[n] = farbe;
            }
        }
        var zahl = Schrift.Text(string.Format(SpielProbeTexte.BlickfeldGrad, wert), "Normal", FontWeight.Bold, "Pinsel.text");
        zahl.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        zahl.MinWidth = 48 * Schrift.Faktor;
        zahl.TextAlignment = TextAlignment.Right;
        regler.ValueChanged += (_, e) =>
        {
            var grad = Blickfeldbedienung.Begrenzen((int)Math.Round(e.NewValue));
            zahl.Text = string.Format(SpielProbeTexte.BlickfeldGrad, grad);
            if (_setzen)
            {
                return;
            }
            // das Bild folgt sofort, geschrieben wird erst, wenn der Regler ruht (nicht jeder Zwischenwert)
            Blickfeldbedienung.Grad = grad;
            _blickfeldSpeichern ??= new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => BlickfeldSpeichern());
            _blickfeldSpeichern.Stop();
            _blickfeldSpeichern.Start();
        };
        _blickfeld = regler;
        _blickfeldZahl = zahl;
        Grid.SetColumn(zahl, 1);
        zeile.Children.Add(regler);
        zeile.Children.Add(zahl);
        panel.Children.Add(titel);
        panel.Children.Add(zeile);
        panel.Children.Add(new LernText(SpielProbeTexte.BlickfeldSatz, "Klein", FontWeight.Normal, "Pinsel.muted"));
    }

    /// <summary>Regler „Blickfeld Ich-Sicht“ speichern (auch sofort fuer den Start-Test); false = nicht gespeichert.</summary>
    public bool BlickfeldSpeichern()
    {
        _blickfeldSpeichern?.Stop();
        if (_blickfeld is null)
        {
            return true;
        }
        var grad = Blickfeldbedienung.Begrenzen((int)Math.Round(_blickfeld.Value));
        if (Ergebnis(Zustand.Aendere(werte => Blickfeldbedienung.Wert(werte, grad))))
        {
            Blickfeldbedienung.Grad = grad;
            return true;
        }
        // nicht gespeichert: der Regler zeigt wieder, was gilt (nie still verloren)
        var gilt = Blickfeldbedienung.Lesen(Zustand.Werte);
        Blickfeldbedienung.Grad = gilt;
        _setzen = true;
        _blickfeld.Value = gilt;
        _setzen = false;
        return false;
    }

    /// <summary>Welle A 0.98.0 (Sammelliste 397): Abstand ueber einer neuen Gruppe in einer Karte (wie die Ueberschriften in <c>OptionenSeite</c>).</summary>
    internal const double GruppenAbstand = 10;

    private ToggleSwitch UmsehenSchalterBauen(Panel panel, string text, string schluessel, Action<bool> gilt)
    {
        var schalter = new ToggleSwitch
        {
            Content = Schrift.Text(text),
            IsChecked = Umsehbedienung.Lesen(Zustand.Werte, schluessel),
            OnContent = null,
            OffContent = null,
        };
        Avalonia.Automation.AutomationProperties.SetName(schalter, text);
        schalter.IsCheckedChanged += (_, _) =>
        {
            if (_setzen)
            {
                return;
            }
            var an = schalter.IsChecked == true;
            if (Ergebnis(Zustand.Aendere(werte => Umsehbedienung.Wert(werte, schluessel, an))))
            {
                gilt(an);
            }
            else
            {
                // nicht gespeichert: der Schalter zeigt wieder, was gilt (nie still verloren)
                _setzen = true;
                schalter.IsChecked = Umsehbedienung.Lesen(Zustand.Werte, schluessel);
                _setzen = false;
            }
        };
        panel.Children.Add(schalter);
        return schalter;
    }

    /// <summary>Zeile zum Platz und Schwierigkeitsgrad neu lesen (Hintergrund); nur das Ergebnis des letzten Anstosses wird gezeigt.</summary>
    private void SpielZeileNeu()
    {
        if (_spielZeile is null)
        {
            return;
        }
        var lauf = ++_spielZeileLauf;
        SpielZeileGelesen = LesenAsync();

        async Task LesenAsync()
        {
            var zeile = await Task.Run(Lernplattform.Spiel.SpielStart.OptionenZeile);
            if (lauf == _spielZeileLauf && _spielZeile is not null)
            {
                _spielZeile.Text = zeile;      // await kehrt auf den Oberflaechen-Faden zurueck
            }
        }
    }
}
