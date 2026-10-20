using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Messung;
using Lernplattform.Programm;
using Update = Lernplattform.Aktualisierung.Aktualisierung;

namespace Lernplattform.Views;

/// <summary>
/// Phase 1, K8: Optionen in der Reihenfolge von fisi_optionen.AREAS. Gebaut sind
/// Optische Anpassungen (Schriftgroesse, Darstellung, Vorlagen, eigene Farben),
/// Tagesziel, Diagnose (Fehlerprotokoll anzeigen und kopieren) und "Über das
/// Programm". Die uebrigen Bereiche zeigen den Platzhalter. Jede Aenderung wird
/// sofort gespeichert; geht das nicht, erscheint "Speichern fehlgeschlagen" und
/// die Anzeige springt auf den gespeicherten Wert zurueck.
/// </summary>
public sealed class OptionenSeite : UserControl, IAufklappbar
{
    private static readonly bool Pc = !OperatingSystem.IsAndroid();

    private readonly List<Expander> _klappen = [];
    private readonly Border _fehler;
    private readonly Func<Control>? _sicherung;
    private readonly List<RadioButton> _schrift = [];
    private readonly List<RadioButton> _darstellung = [];
    private EigeneFarbenTeil? _eigene;
    private bool _setzen;

    public OptionenSeite(Func<Control>? sicherung = null)
    {
        _sicherung = sicherung;
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
        var liste = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        liste.Children.Add(_fehler);
        foreach (var bereich in Optionen.BereicheFuer(Pc))
        {
            liste.Children.Add(Bereich(bereich, Inhalt(bereich)));
        }
        Content = new ScrollViewer { Content = liste };
    }

    public void AllesAufklappen()
    {
        foreach (var klappe in _klappen)
        {
            klappe.IsExpanded = true;
        }
    }

    private Control Bereich(OptionenBereich bereich, Control inhalt)
    {
        var titel = Schrift.Text(bereich.Titel, "Mittel", FontWeight.Bold);
        var kopf = new StackPanel { Spacing = 2, Children = { titel } };
        if (bereich.Untertitel(Pc) is { } untertitel)
        {
            var text = bereich.Id == Optionen.Updates ? Beschriftungen.Einsetzen(untertitel, Update.Anzeige) : untertitel;
            kopf.Children.Add(Schrift.Text(text, "Klein", FontWeight.Normal, "Pinsel.muted"));
        }
        if (bereich.Offen)
        {
            return Schrift.Karte(new StackPanel { Spacing = 12, Children = { kopf, inhalt } });
        }
        var klappe = new Expander
        {
            Header = kopf,
            Content = inhalt,
            IsExpanded = bereich.Aufgeklappt,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        klappe.Farbe(Expander.BackgroundProperty, "Pinsel.card").Farbe(Expander.BorderBrushProperty, "Pinsel.border")
            .Farbe(Expander.ForegroundProperty, "Pinsel.text");
        Avalonia.Automation.AutomationProperties.SetName(klappe, bereich.Titel);
        _klappen.Add(klappe);
        var karte = Schrift.Karte(klappe);
        karte.Padding = new Thickness(0);
        return karte;
    }

    private Control Inhalt(OptionenBereich bereich) => bereich.Id switch
    {
        Optionen.Optik => Optik(bereich),
        Optionen.Tagesziel => Tagesziel(),
        Optionen.Diagnose => Diagnose(bereich),
        Optionen.Ueber => Ueber(),
        Optionen.Sicherung when _sicherung is not null => _sicherung(),
        _ => Platzhalterzeile(),
    };

    private static TextBlock Platzhalterzeile() => Schrift.Text(Beschriftungen.Platzhalter, "Normal", FontWeight.Normal, "Pinsel.text_dim");

    private static TextBlock Abschnitt(string titel) =>
        Schrift.Text(Beschriftungen.Gross(titel), "Klein", FontWeight.Bold, "Pinsel.muted");

    /// <summary>Speichern fehlgeschlagen anzeigen bzw. ausblenden.</summary>
    private bool Ergebnis(bool gespeichert)
    {
        _fehler.IsVisible = !gespeichert;
        return gespeichert;
    }

    // -- Optische Anpassungen ------------------------------------------------

    private Control Optik(OptionenBereich bereich)
    {
        var panel = new StackPanel { Spacing = 10 };
        foreach (var abschnitt in bereich.AbschnitteFuer(Pc))
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
                if (Ergebnis(Zustand.Setze(Einstellungen.SchriftSchluessel, stufe.Id)))
                {
                    Schrift.Anwenden(stufe.Faktor);
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
                if (Ergebnis(Zustand.Setze(Einstellungen.DarstellungSchluessel, id)))
                {
                    Zustand.DesignAnwenden();
                    _eigene?.Neu();
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
        return panel;
    }

    // -- Diagnose ------------------------------------------------------------

    private Control Diagnose(OptionenBereich bereich)
    {
        var panel = new StackPanel { Spacing = 10 };
        foreach (var abschnitt in bereich.AbschnitteFuer(Pc))
        {
            panel.Children.Add(Abschnitt(abschnitt.Titel));
            if (abschnitt.Id != Optionen.AbschnittProblem)
            {
                panel.Children.Add(Platzhalterzeile());
                continue;
            }
            // Phase 1 (K4): nur das Fehlerprotokoll anzeigen und kopieren; "Problem melden" kommt in Phase 6
            var protokoll = new TextBox
            {
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                MaxHeight = 320,
                FontFamily = new FontFamily("monospace"),
            }.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt");
            void Neu()
            {
                var text = Fehlerprotokoll.Anzeigen();
                protokoll.Text = text.Length == 0 ? Fehlerprotokoll.KeinProtokoll : text;
            }
            Neu();
            var kopieren = Schrift.Knopf(DiagnoseTexte.Kopieren);
            kopieren.Click += async (_, _) =>
            {
                Neu();
                if (TopLevel.GetTopLevel(this)?.Clipboard is { } ablage)
                {
                    await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(ablage, protokoll.Text ?? "");
                }
            };
            panel.Children.Add(Schrift.Text(Fehlerprotokoll.DateiName, "Klein", FontWeight.SemiBold, "Pinsel.text_dim"));
            panel.Children.Add(protokoll);
            panel.Children.Add(new WrapPanel { Children = { kopieren } });
        }
        return panel;
    }

    // -- Über das Programm ---------------------------------------------------

    private Control Ueber()
    {
        // Wie fisi_rechtliches.about_text; der Absatz zur Umsetzung (Python-Linie: "Umgesetzt mit
        // Python und ...") fehlt, bis Nico den Text fuer die C#-Linie festlegt.
        var text = UeberTexte.Programmname + " Version " + Update.Anzeige + "\n" + UeberTexte.Copyright + "\n\n"
            + UeberTexte.Beschreibung + "\n\n" + UeberTexte.Hinweis + "\n\n" + UeberTexte.ClaudeHinweis;
        var panel = new StackPanel { Spacing = 10, Children = { Schrift.Text(text) } };
        var anzeige = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            MaxHeight = 360,
            IsVisible = false,
        }.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt");
        var reihe = new WrapPanel();
        foreach (var (knopfText, datei) in (ReadOnlySpan<(string, string)>)[(UeberTexte.KnopfLizenz, "LICENSE.txt"), (UeberTexte.KnopfFremd, "THIRD_PARTY_NOTICES.txt")])
        {
            var knopf = Schrift.Knopf(knopfText);
            knopf.Click += (_, _) =>
            {
                anzeige.Text = Eingebettet(datei);
                anzeige.IsVisible = true;
            };
            reihe.Children.Add(knopf);
        }
        panel.Children.Add(reihe);
        panel.Children.Add(anzeige);
        return panel;
    }

    private static string Eingebettet(string datei)
    {
        using var strom = typeof(OptionenSeite).Assembly.GetManifestResourceStream("texte/" + datei);
        return strom is null ? "" : new StreamReader(strom).ReadToEnd();
    }
}
