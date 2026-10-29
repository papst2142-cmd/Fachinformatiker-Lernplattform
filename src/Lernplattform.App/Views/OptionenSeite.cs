using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Hilfe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Messung;
using Lernplattform.Programm;
using Update = Lernplattform.Aktualisierung.Aktualisierung;

namespace Lernplattform.Views;

/// <summary>
/// Phase 1, K8: Optionen in der Reihenfolge von fisi_optionen.AREAS. Gebaut sind
/// Optische Anpassungen (Schriftgroesse, Darstellung, Vorlagen, eigene Farben),
/// Tagesziel, Abgleich PC und Handy (Phase 2, L10), Diagnose (Phase 5 W2, <see cref="DiagnoseTeil"/>) und "Über das
/// Programm". Die uebrigen Bereiche zeigen den Platzhalter. Jede Aenderung wird
/// sofort gespeichert; geht das nicht, erscheint "Speichern fehlgeschlagen" und
/// die Anzeige springt auf den gespeicherten Wert zurueck.
/// </summary>
public sealed partial class OptionenSeite : UserControl, IAufklappbar
{
    private static readonly bool Pc = !OperatingSystem.IsAndroid();

    private readonly List<Expander> _klappen = [];
    private readonly Dictionary<string, (Control Karte, Expander? Klappe)> _bereiche = new(StringComparer.Ordinal);
    private Expander? _vorlagen;
    private readonly Border _fehler;
    private readonly Func<Control>? _sicherung;
    private readonly List<RadioButton> _schrift = [];
    private readonly List<RadioButton> _darstellung = [];
    private readonly List<RadioButton> _fachrichtung = [];
    private EigeneFarbenTeil? _eigene;
    private bool _setzen;
    private readonly Action? _rundgang;
    private readonly Action? _hilfe;
    private TextBox? _name;
    private readonly Dictionary<string, ToggleSwitch> _rpSchalter = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBox> _rpTermine = new(StringComparer.Ordinal);
    private ComboBox? _rpLernfeld;
    private List<string> _lernfelder = [];
    private TextBlock? _rpFehler;
    private Action? _tageszielNeu;
    private DiagnoseTeil? _diagnose;

    public OptionenSeite(Func<Control>? sicherung = null, Action? rundgang = null, Action? hilfe = null)
    {
        _sicherung = sicherung;
        _rundgang = rundgang;
        _hilfe = hilfe;
        _fehler = Schrift.Karte(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Schrift.Text(Optionen.SpeichernFehlgeschlagenTitel, "Mittel", FontWeight.Bold, "Pinsel.red"),
                Schrift.Text(Optionen.SpeichernFehlgeschlagen),
            },
        });
        _fehler.IsVisible = false;
        var liste = new StackPanel { Spacing = 14, Margin = new Thickness(16, 4, 16, 24) };   // screen_list
        liste.Children.Add(_fehler);
        // Phase 8 W2b (Sammelliste 116): Bereiche in Gruppen LERNEN, AUSSEHEN, SPIEL, DATEN, PROGRAMM,
        // jede Karte zugeklappt mit Titel und Kurzzeile; Reihenfolge und Inhalte der Bereiche aus Geraetetexte
        var bereiche = Geraetetexte.OptionenBereiche(Pc);
        Angezeigt = bereiche;
        foreach (var (titel, gruppe) in Optionen.Gruppieren(bereiche))
        {
            var karten = new StackPanel { Spacing = 10 };
            foreach (var bereich in gruppe)
            {
                karten.Children.Add(Bereich(bereich, Inhalt(bereich)));
            }
            liste.Children.Add(new StackPanel { Spacing = 8, Children = { Gruppe(titel), karten } });
        }
        if (bereiche.FirstOrDefault(b => !_bereiche.ContainsKey(b.Id)) is { } fehlt)
        {
            throw new InvalidOperationException("Optionen: Bereich ohne Gruppe: " + fehlt.Id);
        }
        Content = new ScrollViewer { Content = liste };
        UpdateDienst.Gefunden += version => Dispatcher.UIThread.Post(() => UpdateVerfuegbar(version));
        if (UpdateDienst.Verfuegbar is { } verfuegbar)
        {
            UpdateVerfuegbar(verfuegbar);
        }
    }

    /// <summary>Klappbereich eines Bereichs (Start ohne Bedienung), null bei offenen Bereichen.</summary>
    public Expander? Klappe(string id) => _bereiche.TryGetValue(id, out var bereich) ? bereich.Klappe : null;

    public void AllesAufklappen()
    {
        foreach (var klappe in _klappen)
        {
            klappe.IsExpanded = true;
        }
    }

    /// <summary>
    /// open_area (Suche, Phase 2 L8): Bereich (und ggf. "Vorlagen") aufklappen und in den
    /// sichtbaren Bereich holen. Nichts wird danach wieder zugeklappt.
    /// </summary>
    public void OeffneBereich(string id, bool vorlagen)
    {
        if (!_bereiche.TryGetValue(id, out var bereich))
        {
            return;
        }
        Control ziel = bereich.Karte;
        if (bereich.Klappe is { } klappe)
        {
            klappe.IsExpanded = true;
        }
        if (vorlagen && id == Optionen.Optik && _vorlagen is { } v)
        {
            v.IsExpanded = true;
            ziel = v;
        }
        // nach dem Aufklappen messen, dann oben (mit 10 Punkten Abstand) zeigen
        Dispatcher.UIThread.Post(() =>
        {
            if (Content is ScrollViewer rollen && ziel.TranslatePoint(new Point(0, 0), rollen.Content as Visual ?? rollen) is { } punkt)
            {
                rollen.Offset = new Vector(rollen.Offset.X, Math.Max(0, punkt.Y - 10));
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Farbe je Bereich: die seiner Gruppe (Weisung P8-5b), "Loeschen und zuruecksetzen" rot.</summary>
    private static string Akzent(string id) => "Pinsel." + Optionen.Farbe(id);

    /// <summary>Gruppen-Ueberschrift wie in der Seitenleiste (11 fett, Grossbuchstaben), seit P8-5b in der Farbe der Gruppe.</summary>
    private static TextBlock Gruppe(string titel)
    {
        var text = Schrift.Pt(titel, 11, FontWeight.Bold, "Pinsel." + Optionen.GruppenFarben[titel]);
        text.Margin = new Thickness(4, 6, 0, 0);
        return text;
    }

    private TextBlock? _updateKurz;

    /// <summary>Die angezeigten Bereiche (Start-Test: jeder hat eine Klappkarte).</summary>
    public IReadOnlyList<OptionenBereich> Angezeigt { get; }

    /// <summary>Eine neue Version ist gefunden: ohne Aufklappen in der Kurzzeile der Karte "Updates" sichtbar.</summary>
    private void UpdateVerfuegbar(string version)
    {
        if (_updateKurz is null)
        {
            return;
        }
        _updateKurz.Text = Beschriftungen.Einsetzen(UpdateTexte.Verfuegbar, version);
        _updateKurz.Farbe(TextBlock.ForegroundProperty, "Pinsel.accent");
        _updateKurz.FontWeight = FontWeight.Bold;
    }

    /// <summary>Start-Test: Kurzzeile der Karte "Updates" mit gefundener Version zeigen und zurueckgeben; null stellt die Zeile zurueck.</summary>
    public string UpdateKurzProbe(string? version)
    {
        if (_updateKurz is null)
        {
            return "";
        }
        if (version is not null)
        {
            UpdateVerfuegbar(version);
            return _updateKurz.Text ?? "";
        }
        _updateKurz.Text = Beschriftungen.Einsetzen(Optionen.Kurzzeile(Optionen.Bereich(Optionen.Updates)), Update.Anzeige.Replace(' ', '\u00A0'));
        _updateKurz.Farbe(TextBlock.ForegroundProperty, "Pinsel.muted");
        _updateKurz.FontWeight = FontWeight.Normal;
        return _updateKurz.Text;
    }

    /// <summary>A 0.12.2: Klappkarte wie FoldCard(marker=True) am Handy; seit W2b jede Karte zugeklappt mit Kurzzeile.</summary>
    private Control Bereich(OptionenBereich bereich, Control inhalt)
    {
        var kurz = Optionen.Kurzzeile(bereich);
        if (bereich.Id == Optionen.Updates)
        {
            kurz = Beschriftungen.Einsetzen(kurz, Update.Anzeige.Replace(' ', '\u00A0'));
        }
        var kurzText = Schrift.Text(kurz, "Winzig", FontWeight.Normal, "Pinsel.muted");
        if (bereich.Id == Optionen.Updates)
        {
            _updateKurz = kurzText;
        }
        var klappe = Klappkarte.Bauen(bereich.Titel, kurzText, inhalt, Akzent(bereich.Id));
        _klappen.Add(klappe);
        _bereiche[bereich.Id] = (klappe, klappe);
        return klappe;
    }

    private Control Inhalt(OptionenBereich bereich) => bereich.Id switch
    {
        Optionen.Optik => Optik(bereich),
        Optionen.Tagesziel => Tagesziel(),
        Optionen.Diagnose => Diagnose(bereich),
        Optionen.Ueber => Ueber(),
        Optionen.Sicherung when _sicherung is not null => _sicherung(),
        Optionen.Abgleich => new AbgleichTeil().Teil,
        Optionen.Ausbildung => Ausbildung(),
        Optionen.Rundgang => RundgangUndHilfe(),
        Optionen.Lerninhalte => Lerninhalte(bereich),
        Optionen.Loeschen => Loeschen(),
        Optionen.Datenbank => Datenbank(),
        Optionen.Updates => Updates(),
        Optionen.Spiel when Lernplattform.Spiel.TonProbe.Geraet is { } probe => TonProbeTeil(probe),
        Optionen.Ton => Ton(),
        Optionen.Vollbild => VollbildKarte(),
        Optionen.Tasten => Tasten(),
        _ => Platzhalterzeile(),
    };

    /// <summary>
    /// Weisung P8-16 a: Ton-Probe nur in der Probe-APK (sonst ist <see cref="Lernplattform.Spiel.TonProbe.Geraet"/> null):
    /// Regler 0 bis 100 %, Knopf Starten/Anhalten, Zeile mit dem Ergebnis. Texte nur für die Probe.
    /// </summary>
    private static Control TonProbeTeil(Lernplattform.Spiel.ITonProbe probe)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new LernText("Ton-Probe (nur in der Probe-App): spielt zwei kurze Töne als Schleife. Der Regler ändert die Lautstärke sofort.", "Klein", FontWeight.Normal, "Pinsel.muted"));
        var zahl = Schrift.Text("50 %", "Normal", FontWeight.Bold, "Pinsel.text");
        var regler = new Slider { Minimum = 0, Maximum = 100, Value = 50, SmallChange = 5, LargeChange = 10 };
        Avalonia.Automation.AutomationProperties.SetName(regler, "Lautstärke");
        regler.ValueChanged += (_, e) =>
        {
            zahl.Text = $"{(int)e.NewValue} %";
            probe.Lautstaerke((float)(e.NewValue / 100));
        };
        var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        zahl.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(zahl, 1);
        zeile.Children.Add(regler);
        zeile.Children.Add(zahl);
        panel.Children.Add(zeile);
        var ergebnis = new LernText("", "Klein", FontWeight.Normal, "Pinsel.text_dim");
        var laeuft = false;
        var knopf = Schrift.Knopf("Ton starten", true);
        knopf.HorizontalAlignment = HorizontalAlignment.Left;
        knopf.Click += (_, _) =>
        {
            if (laeuft)
            {
                probe.Anhalten();
                laeuft = false;
                ((TextBlock)knopf.Content!).Text = "Ton starten";
                ergebnis.Text = "Angehalten.";
                return;
            }
            var zeileText = probe.Starten((float)(regler.Value / 100));
            laeuft = zeileText.StartsWith("Ton läuft", StringComparison.Ordinal);
            ((TextBlock)knopf.Content!).Text = laeuft ? "Ton anhalten" : "Ton starten";
            ergebnis.Text = zeileText;
        };
        panel.Children.Add(knopf);
        panel.Children.Add(ergebnis);
        return panel;
    }

    private static TextBlock Platzhalterzeile() => Schrift.Text(Beschriftungen.Platzhalter, "Normal", FontWeight.Normal, "Pinsel.text_dim");

    private static TextBlock Abschnitt(string titel) =>
        Schrift.Text(Beschriftungen.Gross(titel), "Klein", FontWeight.Bold, "Pinsel.muted");

    /// <summary>Speichern fehlgeschlagen anzeigen bzw. ausblenden.</summary>
    private bool Ergebnis(bool gespeichert)
    {
        _fehler.IsVisible = !gespeichert;
        return gespeichert;
    }

    // -- Ausbildung (Phase 4, W2; Wortliste W2 Abschnitt 7) ---------------------

    /// <summary>
    /// Fachrichtung waehlen: gespeichert im Lernstand (Ausbildung.Waehlen), mit dem Abgleich auf
    /// die anderen Geraete. Die Anzeige zeigt den neuen Wert erst nach Erfolg; sonst
    /// "Speichern fehlgeschlagen" und der alte Wert.
    /// </summary>
    private Control Ausbildung()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Schrift.Text(PruefungTexte.AusbildungTitel, "Normal", FontWeight.Bold),
                Schrift.Text(PruefungTexte.AusbildungUntertitel, "Klein", FontWeight.Normal, "Pinsel.muted"),
            },
        });
        var aktuell = Core.Pruefung.Ausbildung.Aktuell(Zustand.Schreiber);
        var reihe = new WrapPanel();
        foreach (var (wert, text) in PruefungTexte.Fachrichtungen)
        {
            var wahl = Auswahl(text, "fachrichtung", wert == aktuell);
            wahl.Content = new LernText(text, "Normal", FontWeight.Normal, "Pinsel.text");   // lange Namen nie im Wort trennen
            wahl.Tag = wert;
            wahl.IsCheckedChanged += (_, _) =>
            {
                if (_setzen || wahl.IsChecked != true)
                {
                    return;
                }
                if (Ergebnis(Core.Pruefung.Ausbildung.Waehlen(Zustand.Schreiber, wert)))
                {
                    AbgleichDienst.NachDemLernen();     // wie nach jedem Speichern im Lernstand
                    Zustand.AusbildungGespeichert();    // Phase 9 W2: Inhalte der neuen Fachrichtung
                    _ = WarnungAsync(wert);             // W4: Warnhinweis Rahmenplan (Plan 2.5)
                }
                else
                {
                    Zuruecksetzen(_fachrichtung, Core.Pruefung.Ausbildung.Aktuell(Zustand.Schreiber));
                }
            };
            _fachrichtung.Add(wahl);
            reihe.Children.Add(wahl);
        }
        panel.Children.Add(reihe);
        panel.Children.Add(Schrift.Text(PruefungTexte.AusbildungHinweis, "Klein", FontWeight.Normal, "Pinsel.muted"));
        return panel;
    }

    /// <summary>on_show: die Fachrichtung kann per Abgleich gekommen sein; W4: Name und Rahmenplan (refresh_plan).</summary>
    public void Gezeigt()
    {
        _ = ZaehlteilAsync();
        if (_fachrichtung.Count > 0)
        {
            Zuruecksetzen(_fachrichtung, Core.Pruefung.Ausbildung.Aktuell(Zustand.Schreiber));
        }
        RahmenplanNeu();
        _tageszielNeu?.Invoke();
        _diagnose?.Neu();
    }

    private async Task WarnungAsync(string wert)
    {
        await AusbildungTeil.GewechseltAsync(wert);
        RahmenplanNeu();
    }

    // -- Rundgang und Hilfe (Phase 4, W4; app_gui 7266-7287) ------------------

    private Control RundgangUndHilfe()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new LernText(RundgangTexte.OptionenText, "Klein", FontWeight.Normal, "Pinsel.text_dim"));
        var starten = Schrift.Knopf(RundgangTexte.RundgangStarten, betont: true);
        starten.Click += (_, _) => _rundgang?.Invoke();
        var hilfe = Schrift.Knopf(RundgangTexte.HilfeOeffnen);
        hilfe.Click += (_, _) => _hilfe?.Invoke();
        panel.Children.Add(new WrapPanel { Margin = new Thickness(0, 4, 0, 0), Children = { starten, hilfe } });
        var titel = Abschnitt(RundgangTexte.Name);
        titel.Margin = new Thickness(0, 10, 0, 0);
        panel.Children.Add(titel);
        _name = new TextBox
        {
            Text = Core.Hilfe.Einrichten.NameLesen(Zustand.Werte),
            PlaceholderText = RundgangTexte.NameZusatz,
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 520,
        }.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Breite(380);
        Avalonia.Automation.AutomationProperties.SetName(_name, RundgangTexte.Name);
        _name.LostFocus += (_, _) => NameSpeichern();
        _name.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                NameSpeichern();
            }
        };
        panel.Children.Add(_name);
        panel.Children.Add(new LernText(Core.Hilfe.HilfeTexte.NameHinweis, "Klein", FontWeight.Normal, "Pinsel.muted"));
        return panel;
    }

    /// <summary>_save_name: bereinigt speichern (nur einstellungen.json), nur wenn er sich aendert; danach rechnet das Dashboard neu.</summary>
    private void NameSpeichern()
    {
        if (_name is null)
        {
            return;
        }
        var text = _name.Text ?? "";
        if (Core.Hilfe.Einrichten.NameSetzen((JsonObject)Zustand.Werte.DeepClone(), text)
            && !Ergebnis(Zustand.Aendere(w => Core.Hilfe.Einrichten.NameSetzen(w, text))))
        {
            _name.Text = Core.Hilfe.Einrichten.NameLesen(Zustand.Werte);
            return;
        }
        var name = Core.Hilfe.Einrichten.NameBereinigen(text);
        if (_name.Text != name)
        {
            _name.Text = name;
        }
    }

    // -- Lerninhalte > Rahmenplan (Phase 4, W4; app_gui 7397-7437, 7741-7761) ---

    private TextBlock? _zaehlteil;

    /// <summary>Zahlen der Lerninhalte der aktuellen Sicht eintragen (Sammelliste 54).</summary>
    private async Task ZaehlteilAsync()
    {
        if (_zaehlteil is not { } feld || await Zustand.Inhalte is not { } inhalte)
        {
            return;
        }
        var text = Core.Texte.FachrichtungTexte.Zaehlteil(inhalte, inhalte.Sicht ?? Core.Inhalte.Fachrichtungen.Vorgabe);
        await Dispatcher.UIThread.InvokeAsync(() => feld.Text = text);
    }

    private Control Lerninhalte(OptionenBereich bereich)
    {
        var panel = new StackPanel { Spacing = 8 };
        // Phase 9 W2 (Sammelliste 54): Zaehlteil der gewaehlten Fachrichtung, nach dem Laden und bei jedem Zeigen
        _zaehlteil = Schrift.Text(FarbenTexte.Laden, "Normal", FontWeight.Normal, "Pinsel.text_dim");
        panel.Children.Add(_zaehlteil);
        _ = ZaehlteilAsync();
        foreach (var abschnitt in Geraetetexte.OptionenAbschnitte(bereich, Pc))
        {
            var titel = Abschnitt(abschnitt.Titel);
            titel.Margin = new Thickness(0, 10, 0, 0);
            panel.Children.Add(titel);
            if (abschnitt.Id == Optionen.AbschnittRahmenplan)
            {
                panel.Children.Add(Rahmenplan());
            }
        }
        return panel;
    }

    private Control Rahmenplan()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Schrift.Text(RahmenplanTexte.NurDiesesGeraet, "Klein", FontWeight.Normal, "Pinsel.muted"));
        panel.Children.Add(new LernText(RahmenplanTexte.Untertitel, "Klein", FontWeight.Normal, "Pinsel.text_dim"));
        var schalter = Core.Hilfe.Einrichten.Abschnitte
            .Select(a => (a.Schluessel, RahmenplanTexte.AbschnittText(a.Abschnitt)))
            .Append((Core.Hilfe.Einrichten.GewichtungSchluessel, RahmenplanTexte.Gewichtung));
        foreach (var (schluessel, text) in schalter)
        {
            var knopf = new ToggleSwitch
            {
                Content = new LernText(text, "Klein", FontWeight.Normal, "Pinsel.text_dim"),
                OnContent = null,
                OffContent = null,
            };
            Avalonia.Automation.AutomationProperties.SetName(knopf, text);
            knopf.IsCheckedChanged += (_, _) =>
            {
                if (_setzen)
                {
                    return;
                }
                var an = knopf.IsChecked == true;
                if (!Ergebnis(Zustand.Aendere(w => Core.Hilfe.Einrichten.RahmenplanSetzen(w, schluessel, an))))
                {
                    RahmenplanNeu();
                }
            };
            _rpSchalter[schluessel] = knopf;
            panel.Children.Add(knopf);
        }
        panel.Children.Add(new LernText(RahmenplanTexte.GewichtungHinweis, "Klein", FontWeight.Normal, "Pinsel.muted"));
        var reihe = new WrapPanel();
        _rpLernfeld = new ComboBox { MinWidth = 110, ItemsSource = new List<string> { RahmenplanTexte.LernfeldAus }, SelectedIndex = 0 };
        Avalonia.Automation.AutomationProperties.SetName(_rpLernfeld, RahmenplanTexte.Lernfeld);
        _rpLernfeld.SelectionChanged += (_, _) =>
        {
            if (_setzen || _rpLernfeld.SelectedIndex < 0)
            {
                return;
            }
            var wert = _rpLernfeld.SelectedIndex == 0 ? "" : _lernfelder[_rpLernfeld.SelectedIndex - 1];
            if (!Ergebnis(Zustand.Aendere(w => Core.Hilfe.Einrichten.RahmenplanSetzen(w, Core.Hilfe.Einrichten.LernfeldSchluessel, wert))))
            {
                RahmenplanNeu();
            }
        };
        reihe.Children.Add(Paar(RahmenplanTexte.Lernfeld, _rpLernfeld));
        foreach (var (schluessel, text) in new[]
        {
            (Core.Hilfe.Einrichten.TerminAp1Schluessel, RahmenplanTexte.TerminAp1),
            (Core.Hilfe.Einrichten.TerminAp2Schluessel, RahmenplanTexte.TerminAp2),
        })
        {
            var feld = new TextBox { MinWidth = 130 }.Farbe(TextBox.ForegroundProperty, "Pinsel.text");   // kein Platzhalter (N12)
            Avalonia.Automation.AutomationProperties.SetName(feld, text);
            feld.LostFocus += (_, _) => TerminSpeichern(schluessel, false);
            feld.KeyDown += (_, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Enter)
                {
                    TerminSpeichern(schluessel, false);
                }
            };
            if (!Pc) // Termin beim Tippen
            {
                // am Handy speichern beim Tippen, sobald das Datum gueltig ist (main.py 4484-4490)
                feld.TextChanged += (_, _) =>
                {
                    if (!_setzen)
                    {
                        TerminSpeichern(schluessel, true);
                    }
                };
            }
            _rpTermine[schluessel] = feld;
            reihe.Children.Add(Paar(text, feld));
        }
        panel.Children.Add(reihe);
        _rpFehler = Schrift.Text(RahmenplanTexte.DatumUngueltig, "Klein", FontWeight.Normal, "Pinsel.red");
        _rpFehler.IsVisible = false;
        panel.Children.Add(_rpFehler);
        RahmenplanNeu();
        _ = LernfelderAsync();
        return panel;
    }

    private static Control Paar(string text, Control feld)
    {
        var beschriftung = Schrift.Text(text, "Klein", FontWeight.Normal, "Pinsel.text_dim");
        beschriftung.VerticalAlignment = VerticalAlignment.Center;
        beschriftung.Margin = new Thickness(0, 0, 10, 0);
        return new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 28, 8), Children = { beschriftung, feld } };
    }

    /// <summary>field_options: "Aus" und alle Lernfelder (Reihenfolge der Lerninhalte), sobald sie geladen sind.</summary>
    private async Task LernfelderAsync()
    {
        if (await Zustand.Inhalte is not { } inhalte || _rpLernfeld is null)
        {
            return;
        }
        _lernfelder = [.. inhalte.Rahmenplan["lernfelder"]!.AsArray().Select(f => (string)f!["id"]!)];
        _setzen = true;
        _rpLernfeld.ItemsSource = new List<string> { RahmenplanTexte.LernfeldAus }.Concat(_lernfelder).ToList();
        _setzen = false;
        RahmenplanNeu();
    }

    /// <summary>
    /// _save_rp fuer einen Termin: leer = kein Termin, ungueltig = Fehlerzeile (beim Tippen am Handy still),
    /// sonst JJJJ-MM-TT speichern und als TT.MM.JJJJ zeigen.
    /// </summary>
    private void TerminSpeichern(string schluessel, bool beimTippen)
    {
        var feld = _rpTermine[schluessel];
        var (iso, fehler) = Core.Hilfe.Einrichten.PruefeDatum(feld.Text);
        if (fehler is not null)
        {
            if (!beimTippen)
            {
                _rpFehler!.IsVisible = true;
            }
            return;
        }
        _rpFehler!.IsVisible = false;
        var alt = Zustand.Werte[schluessel] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var t) ? t : null;
        if (alt != iso && !Ergebnis(Zustand.Aendere(w => Core.Hilfe.Einrichten.RahmenplanSetzen(w, schluessel, iso))))
        {
            RahmenplanNeu();
            return;
        }
        if (!beimTippen)
        {
            _setzen = true;
            feld.Text = Core.Hilfe.Einrichten.DatumText(iso);
            _setzen = false;
        }
    }

    /// <summary>refresh_plan: Name und Rahmenplan-Werte aus den Einstellungen zeigen (nach Einrichten, Abgleich, Fehler).</summary>
    private void RahmenplanNeu()
    {
        _setzen = true;
        if (_name is not null && !_name.IsFocused)
        {
            _name.Text = Core.Hilfe.Einrichten.NameLesen(Zustand.Werte);
        }
        var werte = Core.Hilfe.Einrichten.Startwerte(Zustand.Werte);
        foreach (var (abschnitt, schluessel) in Core.Hilfe.Einrichten.Abschnitte)
        {
            if (_rpSchalter.TryGetValue(schluessel, out var knopf))
            {
                knopf.IsChecked = werte.Abschnitt(abschnitt);
            }
        }
        if (_rpSchalter.TryGetValue(Core.Hilfe.Einrichten.GewichtungSchluessel, out var gewichtung))
        {
            gewichtung.IsChecked = Core.Hilfe.Einrichten.Gewichtung(Zustand.Werte);
        }
        if (_rpTermine.Count > 0)
        {
            _rpTermine[Core.Hilfe.Einrichten.TerminAp1Schluessel].Text = werte.TerminAp1;
            _rpTermine[Core.Hilfe.Einrichten.TerminAp2Schluessel].Text = werte.TerminAp2;
            _rpFehler!.IsVisible = false;
        }
        if (_rpLernfeld is not null)
        {
            var feld = Zustand.Werte[Core.Hilfe.Einrichten.LernfeldSchluessel] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var t) ? t : "";
            var index = _lernfelder.IndexOf(feld);
            _rpLernfeld.SelectedIndex = index + 1;
        }
        _setzen = false;
    }

    // -- Optische Anpassungen ------------------------------------------------

    private Control Optik(OptionenBereich bereich)
    {
        var panel = new StackPanel { Spacing = 10 };
        foreach (var abschnitt in Geraetetexte.OptionenAbschnitte(bereich, Pc))
        {
            panel.Children.Add(Abschnitt(abschnitt.Titel));
            if (abschnitt.Id == Optionen.AbschnittSchrift)
            {
                panel.Children.Add(SchriftWahl());
                panel.Children.Add(Schrift.Text(SchriftTexte.HinweisKurz, "Klein", FontWeight.Normal, "Pinsel.muted"));
            }
            else if (abschnitt.Id == Optionen.AbschnittFarben)
            {
                panel.Children.Add(Farben());
            }
        }
        return panel;
    }

    private Control SchriftWahl()
    {
        var reihe = new WrapPanel();
        var aktuell = Einstellungen.Schriftgroesse(Zustand.Werte);
        foreach (var stufe in SchriftTexte.Stufen)
        {
            var wahl = Auswahl(stufe.Name, "schrift", stufe.Id == aktuell);
            wahl.Tag = stufe.Id;
            wahl.IsCheckedChanged += (_, _) =>
            {
                if (_setzen || wahl.IsChecked != true)
                {
                    return;
                }
                var vorher = Einstellungen.Schriftgroesse(Zustand.Werte);
                if (Ergebnis(Zustand.Setze(Einstellungen.SchriftSchluessel, stufe.Id)))
                {
                    // Phase 5 W2: Vorgang "schrift" fuer die Haenger-Diagnose, Zeile "darstellung" in der Messung
                    Diagnosedienst.Darstellung(vorher, stufe.Id, () => Schrift.Anwenden(stufe.Faktor), "schrift");
                }
                else
                {
                    Zuruecksetzen(_schrift, Einstellungen.Schriftgroesse(Zustand.Werte));
                }
            };
            _schrift.Add(wahl);
            reihe.Children.Add(wahl);
        }
        return reihe;
    }

    private static RadioButton Auswahl(string text, string gruppe, bool gewaehlt)
    {
        var wahl = new RadioButton
        {
            Content = Schrift.Text(text),
            GroupName = gruppe,
            IsChecked = gewaehlt,
            Margin = new Thickness(0, 0, 16, 6),
        };
        wahl.Farbe(RadioButton.ForegroundProperty, "Pinsel.text");
        Avalonia.Automation.AutomationProperties.SetName(wahl, text);
        return wahl;
    }

    private void Zuruecksetzen(List<RadioButton> wahlen, string id)
    {
        _setzen = true;
        foreach (var w in wahlen)
        {
            w.IsChecked = (string?)w.Tag == id;
        }
        _setzen = false;
    }

    private Control Farben()
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(Schrift.Text(DarstellungTexte.Beschriftung, "Normal", FontWeight.SemiBold, "Pinsel.text_dim"));
        var reihe = new WrapPanel();
        var aktuell = Einstellungen.Darstellung(Zustand.Werte);
        foreach (var (id, name) in DarstellungTexte.Modi)
        {
            var wahl = Auswahl(name, "darstellung", id == aktuell);
            wahl.Tag = id;
            wahl.IsCheckedChanged += (_, _) =>
            {
                if (_setzen || wahl.IsChecked != true)
                {
                    return;
                }
                var vorher = Einstellungen.Darstellung(Zustand.Werte);
                if (Ergebnis(Zustand.Setze(Einstellungen.DarstellungSchluessel, id)))
                {
                    Diagnosedienst.Darstellung(vorher, id, () =>
                    {
                        Zustand.DesignAnwenden();
                        _eigene?.Neu();
                    });
                }
                else
                {
                    Zuruecksetzen(_darstellung, Einstellungen.Darstellung(Zustand.Werte));
                }
            };
            _darstellung.Add(wahl);
            reihe.Children.Add(wahl);
        }
        panel.Children.Add(reihe);

        _eigene = new EigeneFarbenTeil(Ergebnis);
        var vorlagen = new StackPanel { Spacing = 8 };
        vorlagen.Children.Add(Schrift.Text(FarbenTexte.VorlagenHinweis, "Klein", FontWeight.Normal, "Pinsel.text_dim"));
        vorlagen.Children.Add(Abschnitt(FarbenTexte.Grundfarbe));
        var grund = new WrapPanel();
        foreach (var g in Farbwelt.Grundfarben)
        {
            grund.Children.Add(Kachel(g.Name, g.Primaer.Item1, g.Primaer.Item2, () => _eigene.Kachel(grundfarbe: g.Id)));
        }
        vorlagen.Children.Add(grund);
        vorlagen.Children.Add(Abschnitt(FarbenTexte.Hintergrund));
        var hinter = new WrapPanel();
        foreach (var h in Farbwelt.Hintergruende)
        {
            var knopf = Kachel(FarbenTexte.HintergrundName(h, Einstellungen.Darstellung(Zustand.Werte)), h.Dunkel[0], h.Hell[0],
                () => _eigene.Kachel(hintergrund: h.Id));
            // Name je Darstellung (im Hellen z. B. "Weiß" statt "Schwarz"), wird beim Wechsel nachgefuehrt
            _eigene.Hintergrundnamen.Add((h, (TextBlock)((StackPanel)knopf.Content!).Children[1]));
            hinter.Children.Add(knopf);
        }
        vorlagen.Children.Add(hinter);
        vorlagen.Children.Add(Schrift.Text(FarbenTexte.VorlagenText, "Klein", FontWeight.Normal, "Pinsel.muted"));
        var klappe = new Expander
        {
            Header = Schrift.Text(FarbenTexte.VorlagenTitel, "Normal", FontWeight.SemiBold),
            Content = vorlagen,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        klappe.Farbe(Expander.BackgroundProperty, "Pinsel.card_alt").Farbe(Expander.BorderBrushProperty, "Pinsel.border");
        _klappen.Add(klappe);
        _vorlagen = klappe;
        panel.Children.Add(klappe);
        panel.Children.Add(_eigene.Bauen());
        return panel;
    }

    private static Button Kachel(string name, string von, string bis, Action klick)
    {
        var feld = new Border
        {
            Width = 64,
            Height = 36,
            CornerRadius = new CornerRadius(8),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse(von), 0), new GradientStop(Color.Parse(bis), 1) },
            },
        };
        var text = Schrift.Text(name, "Klein");
        text.TextAlignment = TextAlignment.Center;
        var knopf = new Button
        {
            Content = new StackPanel { Spacing = 4, Children = { feld, text } },
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 8, 8),
            MinWidth = 96,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        knopf.Farbe(Button.BackgroundProperty, "Pinsel.card");
        Avalonia.Automation.AutomationProperties.SetName(knopf, name);
        knopf.Click += (_, _) => klick();
        return knopf;
    }

    // -- Tagesziel -----------------------------------------------------------

    private Control Tagesziel()
    {
        var panel = new StackPanel { Spacing = 10 };
        var ziel = Einstellungen.Ziel(Zustand.Werte);
        var schalterListe = new List<(string Schluessel, ToggleSwitch Schalter)>();
        foreach (var (schluessel, text) in TageszielTexte.Schalter)
        {
            if (schluessel == "erinnerung_an")
            {
                // Die Erinnerung kommt mit dem Lernen (Phase 2); bis dahin kein Schalter ohne Wirkung
                continue;
            }
            var schalter = new ToggleSwitch
            {
                Content = Schrift.Text(text),
                IsChecked = schluessel == "ziel_an" ? ziel.ZielAn : ziel.SerieAn,
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
                if (!Ergebnis(Zustand.Setze(schluessel, schalter.IsChecked == true)))
                {
                    _setzen = true;
                    var jetzt = Einstellungen.Ziel(Zustand.Werte);
                    schalter.IsChecked = schluessel == "ziel_an" ? jetzt.ZielAn : jetzt.SerieAn;
                    _setzen = false;
                }
            };
            panel.Children.Add(schalter);
            schalterListe.Add((schluessel, schalter));
        }
        var anzahl = new NumericUpDown
        {
            Minimum = Einstellungen.ZielMin,
            Maximum = Einstellungen.ZielMax,
            Increment = TageszielTexte.Schritt,
            Value = ziel.ZielAnzahl,
            FormatString = "0",
            MinWidth = 140,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Avalonia.Automation.AutomationProperties.SetName(anzahl, TageszielTexte.AufgabenProTag);
        anzahl.ValueChanged += (_, e) =>
        {
            if (_setzen || e.NewValue is not decimal wert)
            {
                return;
            }
            if (!Ergebnis(Zustand.Setze("ziel_anzahl", (int)wert)))
            {
                _setzen = true;
                anzahl.Value = Einstellungen.Ziel(Zustand.Werte).ZielAnzahl;
                _setzen = false;
            }
        };
        panel.Children.Add(new WrapPanel
        {
            Children =
            {
                new Border { Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center,
                    Child = Schrift.Text(TageszielTexte.AufgabenProTag, "Normal", FontWeight.Normal, "Pinsel.text_dim") },
                anzahl,
            },
        });
        panel.Children.Add(Schrift.Text(TageszielTexte.Schalter[2].Text + ": " + Beschriftungen.Platzhalter, "Klein", FontWeight.Normal, "Pinsel.muted"));
        panel.Children.Add(Schrift.Text(TageszielTexte.HinweisGezaehlt, "Klein", FontWeight.Normal, "Pinsel.muted"));
        // Phase 5 BE3 (_after_restore): nach „Alles ersetzen“ die Werte aus der Sicherung zeigen
        _tageszielNeu = () =>
        {
            var jetzt = Einstellungen.Ziel(Zustand.Werte);
            _setzen = true;
            foreach (var (schluessel, schalter) in schalterListe)
            {
                schalter.IsChecked = schluessel == "ziel_an" ? jetzt.ZielAn : jetzt.SerieAn;
            }
            anzahl.Value = jetzt.ZielAnzahl;
            _setzen = false;
        };
        return panel;
    }

    // -- Löschen und zurücksetzen (Phase 5 BE4, _build_loeschen) -------------

    private Control Loeschen()
    {
        var panel = new StackPanel { Spacing = 6 };
        void Teil(string titel, string text, Control knopf, bool erster)
        {
            var kopf = Abschnitt(titel);
            if (!erster)
            {
                kopf.Margin = new Thickness(0, 14, 0, 0);
            }
            panel.Children.Add(kopf);
            panel.Children.Add(Schrift.Text(text, "Klein", FontWeight.Normal, "Pinsel.text_dim"));
            knopf.Margin = new Thickness(0, 6, 8, 0);
            knopf.HorizontalAlignment = HorizontalAlignment.Left;
            panel.Children.Add(knopf);
        }
        var alles = Schrift.Gefahrknopf(LoeschenTexte.LerndatenKnopf);
        alles.Click += async (_, _) => await LoeschenAsync(LoeschenTexte.LerndatenFrageTitel, LoeschenTexte.LerndatenFrage,
            s => s.AllesLoeschen(), LoeschenTexte.LerndatenErfolgTitel, LoeschenTexte.LerndatenErfolg);
        Teil(LoeschenTexte.Lerndaten, LoeschenTexte.LerndatenText, alles, true);
        var historie = Schrift.Gefahrknopf(LoeschenTexte.HistorieKnopf);
        historie.Click += async (_, _) => await LoeschenAsync(LoeschenTexte.HistorieFrageTitel, LoeschenTexte.HistorieFrage,
            s => s.HistorieLoeschen(), LoeschenTexte.HistorieErfolgTitel, LoeschenTexte.HistorieErfolg);
        Teil(LoeschenTexte.Historie, LoeschenTexte.HistorieText, historie, false);
        // Spielstand und Bestenliste kommen mit dem Spiel (Phase 7)
        Teil(LoeschenTexte.Spielstand, Beschriftungen.Platzhalter, new Border(), false);
        Teil(LoeschenTexte.Bestenliste, Beschriftungen.Platzhalter, new Border(), false);
        return panel;
    }

    /// <summary>
    /// clear_history / reset_all mit _delete_checked: Rueckfrage, loeschen; ein Datenbankfehler
    /// waehrenddessen wird gesammelt und als genau ein Fenster gezeigt („Es wurde nichts gelöscht.“),
    /// sonst Bestaetigung mit clock_fix_text und Abgleich wie nach dem Lernen (notify_progress).
    /// </summary>
    private static async Task LoeschenAsync(string titel, string frage, Func<LernstandSchreiber, bool> loeschen, string erfolgTitel, string erfolg)
    {
        if (await Dialog.Zeigen(titel, frage, TrainerTexte.Ja, TrainerTexte.Nein) != 0)
        {
            return;
        }
        Core.Spiel.Spielschlange.AlleLeeren();   // Weisung P7-17: Spielschritte zuerst fertig schreiben
        var schreiber = Zustand.Schreiber;
        bool geloescht;
        IReadOnlyList<string> gesammelt;
        try
        {
            (geloescht, gesammelt) = Loeschablauf.Ausfuehren(() => loeschen(schreiber), () => Zustand.Fehlermeldung, e => Zustand.Fehlermeldung = e);
        }
        catch (Exception fehler)
        {
            // Nie einen Systemtext zeigen; Einzelheiten ins Fehlerprotokoll (die Marke entsteht vor dem Schreiben)
            Fehlerprotokoll.Schreiben(fehler);
            (geloescht, gesammelt) = (false, []);
        }
        if (!geloescht)
        {
            await Dialog.Zeigen(titel, LoeschenTexte.NichtsGeloeschtMit(gesammelt), Dialog.Ok);
            return;
        }
        AbgleichDienst.NachDemLernen();
        await Dialog.Zeigen(erfolgTitel, LoeschenTexte.MitUhrHinweis(erfolg, schreiber.LetzteUhrkorrektur), Dialog.Ok);
    }

    // -- Datenbank (Phase 5 BE4, nur PC, _build_datenbank) --------------------

    private static Control Datenbank()
    {
        var pfad = new TextBox
        {
            Text = Zustand.Lernstand.Pfad,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("monospace"),
        }.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt");
        Avalonia.Automation.AutomationProperties.SetName(pfad, LoeschenTexte.DatenbankTitel);
        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Schrift.Text(LoeschenTexte.DatenbankTitel, "Klein", FontWeight.Normal, "Pinsel.text_dim"),
                pfad,
                Schrift.Text(LoeschenTexte.DatenbankHinweis, "Klein", FontWeight.Normal, "Pinsel.muted"),
            },
        };
    }

    // -- Diagnose (Phase 5 W2: Problem melden, Leistungsmessung, Hänger-Diagnose) ----

    // -- Updates (Phase 5 BE10, app_gui 7182; Handy main.py 4549) ---------------

    /// <summary>
    /// "Nach Updates suchen" mit Zeile fuer das Ergebnis und der Schalter "Beim Start automatisch nach
    /// Updates suchen" (auto_check, nur dieses Geraet). Gibt es ein Update, oeffnet sich das Fenster.
    /// </summary>
    private Control Updates()
    {
        var zeile = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.text_dim");
        zeile.VerticalAlignment = VerticalAlignment.Center;
        zeile.IsVisible = false;
        var knopf = Schrift.Knopf(Optionen.UpdatesSuchen, betont: true);
        knopf.Margin = new Thickness(0, 0, 16, 6);
        knopf.Click += async (_, _) =>
        {
            knopf.IsEnabled = false;
            UpdateZeile(zeile, UpdateTexte.Suche, "Pinsel.text_dim");
            var ergebnis = await UpdateDienst.VonHandAsync();
            knopf.IsEnabled = true;
            switch (ergebnis)
            {
                case null:
                    zeile.IsVisible = false;    // eine Suche laeuft schon (automatisch beim Start)
                    break;
                case { Fehler: { } fehler }:
                    UpdateZeile(zeile, fehler, "Pinsel.red");
                    break;
                case { Angebot: null }:
                    UpdateZeile(zeile, Beschriftungen.Einsetzen(UpdateTexte.Neueste, Update.Anzeige), "Pinsel.green");
                    break;
                case { Angebot: { } angebot }:
                    UpdateZeile(zeile, Beschriftungen.Einsetzen(UpdateTexte.Verfuegbar, angebot.Version), "Pinsel.accent");
                    break;
            }
        };
        UpdateKnopf = knopf;
        UpdateErgebnis = zeile;
        var schalter = new ToggleSwitch
        {
            IsChecked = Zustand.Werte[Einstellungen.AutoCheckSchluessel]?.GetValueKind() is System.Text.Json.JsonValueKind.True,
            OnContent = null,
            OffContent = null,
        };
        Avalonia.Automation.AutomationProperties.SetName(schalter, Optionen.UpdatesAutomatisch);
        schalter.IsCheckedChanged += (_, _) =>
        {
            if (_setzen)
            {
                return;
            }
            if (!Ergebnis(Zustand.Setze(Einstellungen.AutoCheckSchluessel, schalter.IsChecked == true)))
            {
                _setzen = true;
                schalter.IsChecked = !schalter.IsChecked;
                _setzen = false;
            }
        };
        // Phase 6 W2 (VE5, app_gui 7206): "Verknüpfung anlegen" mit Zeile, nur unter Linux im installierten AppImage
        var verknuepfung = Schrift.Knopf(VerknuepfungTexte.Anlegen);
        verknuepfung.Margin = new Thickness(0, 0, 16, 6);
        var stand = Schrift.Text(VerknuepfungDienst.Unterstuetzt ? VerknuepfungDienst.StatusText() : VerknuepfungTexte.Keine,
            "Klein", FontWeight.Normal, "Pinsel.text_dim");
        stand.VerticalAlignment = VerticalAlignment.Center;
        verknuepfung.Click += async (_, _) =>
        {
            // Sammelliste 77: im Hintergrund anlegen; solange ist der Knopf gesperrt (kein zweites Anlegen)
            verknuepfung.IsEnabled = false;
            try
            {
                stand.Text = await VerknuepfungDienst.AnlegenUndMeldenAsync();
            }
            finally
            {
                verknuepfung.IsEnabled = true;
            }
        };
        if (VerknuepfungDienst.Unterstuetzt)
        {
            // nach "Ja" in der Rueckfrage beim Start und beim erneuten Zeigen der Seite den Stand neu lesen
            void Neu() => stand.Text = VerknuepfungDienst.StatusText();
            VerknuepfungDienst.Geaendert += Neu;
            DetachedFromVisualTree += (_, _) => VerknuepfungDienst.Geaendert -= Neu;
            AttachedToVisualTree += (_, _) =>
            {
                VerknuepfungDienst.Geaendert -= Neu;
                VerknuepfungDienst.Geaendert += Neu;
                Neu();
            };
        }
        var reihe = new WrapPanel { IsVisible = VerknuepfungDienst.Unterstuetzt, Children = { verknuepfung, stand } };
        VerknuepfungReihe = reihe;
        VerknuepfungZeile = stand;
        // A 0.12.2 wie _switch am Handy: Schalter links, Text (13 text_dim) rechts daneben
        var schalterText = Schrift.Pt(Optionen.UpdatesAutomatisch, 13, FontWeight.Normal, "Pinsel.text_dim");
        schalterText.VerticalAlignment = VerticalAlignment.Center;
        schalter.VerticalAlignment = VerticalAlignment.Center;
        var schalterZeile = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10, Children = { schalter, schalterText } };
        Grid.SetColumn(schalterText, 1);
        knopf.Margin = new Thickness(0, 0, 16, 0);
        return new StackPanel { Spacing = 10, Children = { new WrapPanel { Children = { knopf, zeile } }, schalterZeile, reihe } };
    }

    /// <summary>Reihe "Verknüpfung anlegen" (Linux, AppImage); der Start ohne Bedienung zeigt sie auf allen Systemen.</summary>
    internal WrapPanel? VerknuepfungReihe { get; private set; }

    /// <summary>Zeile neben "Verknüpfung anlegen" (Start ohne Bedienung setzt dort ein Probeergebnis).</summary>
    internal TextBlock? VerknuepfungZeile { get; private set; }

    /// <summary>Knopf "Nach Updates suchen", sobald gebaut (Start ohne Bedienung).</summary>
    public Button? UpdateKnopf { get; private set; }

    /// <summary>Zeile fuer das Ergebnis der Suche (Start ohne Bedienung zeigt dort einen Fehler).</summary>
    internal TextBlock? UpdateErgebnis { get; private set; }

    internal static void UpdateZeile(TextBlock zeile, string text, string farbe)
    {
        zeile.Text = text;
        zeile.IsVisible = true;
        zeile.Farbe(TextBlock.ForegroundProperty, farbe);
    }

    /// <summary>Bereich "Diagnose und Werkzeuge", sobald gebaut (Start ohne Bedienung).</summary>
    public DiagnoseTeil? DiagnoseBereich => _diagnose;

    private Control Diagnose(OptionenBereich bereich)
    {
        _diagnose = new DiagnoseTeil(bereich, Ergebnis);
        return _diagnose.Teil;
    }

    // -- Über das Programm ---------------------------------------------------

    private Control Ueber()
    {
        // Wie fisi_rechtliches.about_text; der Absatz zur Umsetzung (Python-Linie: "Umgesetzt mit
        // Python und ...") fehlt, bis Nico den Text fuer die C#-Linie festlegt.
        var text = UeberTexte.Programmname + " Version " + Update.Anzeige + "\n" + UeberTexte.Copyright + "\n\n"
            + UeberTexte.Beschreibung + "\n\n" + UeberTexte.Hinweis + "\n\n" + UeberTexte.ClaudeHinweis;
        var panel = new StackPanel { Spacing = 10, Children = { Schrift.Pt(text, 13, FontWeight.Normal, "Pinsel.text_dim") } };
        var anzeige = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            MaxHeight = 360,
            IsVisible = false,
        }.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt");
        // Phase 8 W2 (Sammelliste 74, Nico 5a): volle Lizenztexte, erst beim ersten Klick aufgebaut
        var lizenzen = new StackPanel { Spacing = 10, IsVisible = false };
        // A 0.12.2: Knoepfe volle Breite als Rahmenknoepfe (GradientButton kind="ghost", expand)
        var reihe = new StackPanel { Spacing = 10 };
        foreach (var (knopfText, datei) in (ReadOnlySpan<(string, string)>)[(UeberTexte.KnopfLizenz, "LICENSE.txt"), (UeberTexte.KnopfFremd, "THIRD_PARTY_NOTICES.txt")])
        {
            var knopf = Schrift.Knopf(knopfText);
            knopf.Margin = new Thickness(0);
            knopf.HorizontalAlignment = HorizontalAlignment.Stretch;
            knopf.Click += (_, _) =>
            {
                anzeige.Text = Eingebettet(datei);
                anzeige.IsVisible = true;
                lizenzen.IsVisible = false;
            };
            reihe.Children.Add(knopf);
        }
        var lizenzKnopf = Schrift.Knopf(UeberTexte.KnopfLizenzen);
        lizenzKnopf.Margin = new Thickness(0);
        lizenzKnopf.HorizontalAlignment = HorizontalAlignment.Stretch;
        LizenzenOeffnen = () => LizenzenZeigen(lizenzen, anzeige);
        lizenzKnopf.Click += (_, _) => LizenzenOeffnen();
        reihe.Children.Add(lizenzKnopf);
        LizenzenTeil = lizenzen;
        panel.Children.Add(reihe);
        panel.Children.Add(anzeige);
        panel.Children.Add(lizenzen);
        return panel;
    }

    /// <summary>Wie ein Klick auf "Lizenzen" und die Anzeige dazu (fuer den Start ohne Bedienung).</summary>
    internal Action? LizenzenOeffnen { get; private set; }

    internal StackPanel? LizenzenTeil { get; private set; }

    /// <summary>
    /// "Lizenzen": je Bestandteil eine Klappkarte wie in der Hilfe (Titel aus THIRD_PARTY_LICENSES.txt);
    /// der Text entsteht erst beim Aufklappen und zeichnet nur die sichtbaren Bloecke (die Datei ist ueber 1 MB).
    /// </summary>
    private static void LizenzenZeigen(StackPanel lizenzen, TextBox anzeige)
    {
        anzeige.IsVisible = false;
        lizenzen.IsVisible = true;
        if (lizenzen.Children.Count > 0)
        {
            return;
        }
        var (vorspann, teile) = Lizenztexte.Zerlegen(Eingebettet("THIRD_PARTY_LICENSES.txt"));
        lizenzen.Children.Add(new LernText(vorspann.Trim(), "Klein", FontWeight.Normal, "Pinsel.text_dim"));
        foreach (var teil in teile)
        {
            var klappe = new Expander
            {
                Header = Schrift.Text(Lizenztexte.Brechbar(teil.Titel), "Normal", FontWeight.Bold),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            klappe.Farbe(Expander.BackgroundProperty, "Pinsel.card_alt").Farbe(Expander.BorderBrushProperty, "Pinsel.border")
                .Farbe(Expander.ForegroundProperty, "Pinsel.text");
            Avalonia.Automation.AutomationProperties.SetName(klappe, teil.Titel);
            klappe.Expanding += (_, _) => klappe.Content ??= new ItemsControl
            {
                ItemsSource = teil.Bloecke,
                ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(() => new VirtualizingStackPanel()),
                ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((block, _) =>
                    new LernText(block, "Klein", FontWeight.Normal, "Pinsel.text_soft")),
            };
            lizenzen.Children.Add(klappe);
        }
    }

    private static string Eingebettet(string datei)
    {
        using var strom = typeof(OptionenSeite).Assembly.GetManifestResourceStream("texte/" + datei);
        return strom is null ? "" : new StreamReader(strom).ReadToEnd();
    }
}
