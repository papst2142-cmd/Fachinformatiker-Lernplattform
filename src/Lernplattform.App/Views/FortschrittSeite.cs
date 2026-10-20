using System.Numerics;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Fortschritt;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Zufall;
using Lernplattform.Design;
using Lernplattform.Messung;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 2, L7: Fortschritt wie app_gui.ProgressView und CoverageCard (PC-Wortlaut), nur
/// lesend. Werte aus Core.Fortschritt (Vergleichswerte G18): Kacheln, "Aufgaben pro Tag" und
/// "Richtig und falsch pro Tag" fuer 7 oder 30 Tage, Rahmenplan-Abdeckung zum Aufklappen,
/// Ergebnisse, Historie der Sessions, Pruefungen mit schwachen Themen und "Jetzt üben".
/// Gerechnet wird im Hintergrund; bei jedem erneuten Zeigen neu (wie das Dashboard).
/// </summary>
public sealed class FortschrittSeite : UserControl, IAufklappbar
{
    private readonly Func<List<(string Quelle, List<string> Schluessel)>, Task> _ueben;
    private readonly StackPanel _liste = new() { Spacing = 14, Margin = new Thickness(16) };
    private readonly Spaltenraster _kacheln = new(200);
    private readonly Pillen _tagePillen;
    private readonly LinienDiagramm _tage = new(200, "accent");
    private readonly TextBlock _tageText = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.text_dim");
    private readonly AnteilBalken _anteile = new(190);
    private readonly TextBlock _anteileText = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.text_dim");
    private readonly StackPanel _abdeckungPlatz = new();
    private readonly LinienDiagramm _ergebnisse = new(220, "accent2");
    private readonly TextBlock _ergebnisLeer = Schrift.Text(FortschrittTexte.ErgebnisLeer, "Klein", FontWeight.Normal, "Pinsel.text_dim");
    private readonly Tabelle _sitzungen = new([FortschrittTexte.SpalteDatum, FortschrittTexte.SpalteErgebnis,
        FortschrittTexte.SpalteQuote, FortschrittTexte.SpalteNote, FortschrittTexte.SpalteDauer], 9);
    private readonly Tabelle _pruefungen = new([FortschrittTexte.SpalteDatum, FortschrittTexte.SpaltePruefung,
        FortschrittTexte.SpaltePunkte, FortschrittTexte.SpalteNote, FortschrittTexte.SpalteDauer], 6);
    private readonly StackPanel _schwach = new();
    private readonly AbdeckungKarte _abdeckung;

    private readonly System.Diagnostics.Stopwatch _uhr = System.Diagnostics.Stopwatch.StartNew();
    private Auswertung? _auswertung;

    /// <param name="ueben">"Jetzt üben" mit den Teilen (Quelle, Fragen); leer = Hinweis "keine Übungsfragen".</param>
    public FortschrittSeite(Func<List<(string Quelle, List<string> Schluessel)>, Task> ueben)
    {
        _ueben = ueben;
        _abdeckung = new AbdeckungKarte(id => _ = PunktUebenAsync(id), name => _ = ThemaUebenAsync(name));
        _tagePillen = new Pillen(FortschrittTexte.Zeitraeume.Select(z => (Zahl(z.Tage), z.Text)),
            Zahl(FortschrittTexte.Zeitraeume[0].Tage), _ => TageZeichnen());
        foreach (var titel in (string[])[FortschrittTexte.Sessions, FortschrittTexte.Durchschnitt, FortschrittTexte.Bestes])
        {
            _kacheln.Children.Add(Kachel(titel, FortschrittTexte.OhneWert, ""));
        }
        _liste.Children.Add(_kacheln);
        _liste.Children.Add(Karte(FortschrittTexte.TageTitel, FortschrittTexte.TageUnter, new StackPanel
        {
            Spacing = 6,
            Children =
            {
                _tagePillen, _tage, _tageText,
                Abstand(Schrift.Text(FortschrittTexte.AnteilTitel.ToUpperInvariant(), "Klein", FontWeight.Bold, "Pinsel.muted"), 12),
                _anteile, _anteileText,
            },
        }));
        _liste.Children.Add(_abdeckungPlatz);
        _liste.Children.Add(Karte(FortschrittTexte.ErgebnisTitel, FortschrittTexte.ErgebnisUnter, new StackPanel { Children = { _ergebnisse, _ergebnisLeer } }));
        _liste.Children.Add(Karte(FortschrittTexte.SessionsTitel, null, _sitzungen));
        _liste.Children.Add(Karte(FortschrittTexte.PruefungenTitel, FortschrittTexte.PruefungenUnter, new StackPanel { Children = { _pruefungen, _schwach } }));
        var aktualisieren = Schrift.Knopf(FortschrittTexte.Aktualisieren);
        aktualisieren.HorizontalAlignment = HorizontalAlignment.Left;
        aktualisieren.Click += (_, _) => Neu();
        _liste.Children.Add(aktualisieren);
        _ergebnisse.IsVisible = false;
        _ergebnisLeer.IsVisible = false;
        Content = new ScrollViewer { Content = _liste };
        Geladen = LadenAsync();
    }

    /// <summary>Erstes Mal mit Werten gezeigt (fuer die Messung im Start ohne Bedienung).</summary>
    public TaskCompletionSource Fertig { get; } = new();

    /// <summary>Millisekunden vom Bauen der Seite, bis Werte und Abdeckung zum ersten Mal stehen.</summary>
    public double? FertigMs { get; private set; }

    /// <summary>Die zuletzt gestartete Rechnung (Auswertung und Abdeckung).</summary>
    public Task Geladen { get; private set; }

    /// <summary>Bei jedem erneuten Zeigen neu rechnen (on_show/refresh); die alten Werte bleiben stehen, bis die neuen fertig sind.</summary>
    public void Neu()
    {
        if (Geladen.IsCompleted)
        {
            Geladen = LadenAsync();
        }
        else
        {
            // laeuft noch eine Rechnung, danach gleich noch einmal (neue Antworten seit ihrem Start)
            _nochmal = true;
        }
    }

    private bool _nochmal;

    public void AllesAufklappen() => _abdeckung.ErsteAufklappen();

    private static string Zahl(int wert) => wert.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Pfad => System.IO.Path.Combine(Ablage.Datenordner(), Lernstand.DateiName);

    private async Task LadenAsync()
    {
        Auswertung? auswertung = null;
        try
        {
            auswertung = await Task.Run(() => Auswertung.Lesen(Pfad));
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                if (auswertung is not null)
                {
                    _auswertung = auswertung;
                    Zeigen(auswertung);
                }
            }
            catch (Exception fehler)
            {
                Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
                Fertig.TrySetException(fehler);
            }
        });
        // Abdeckung erst danach (wie _fill_coverage nach dem ersten Zeichnen)
        Abdeckung? abdeckung = null;
        try
        {
            var parameter = await Zustand.DashboardInhalte;
            var gewicht = await Zustand.Gewicht;
            if (parameter is not null && gewicht is not null)
            {
                var werte = Zustand.Werte;
                abdeckung = await Task.Run(() => new Abdeckung(parameter, gewicht, Auswertung.BuchLesen(Pfad), RahmenplanWerte.Aus(werte, gewicht)));
            }
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                if (abdeckung is not null)
                {
                    if (_abdeckungPlatz.Children.Count == 0)
                    {
                        _abdeckungPlatz.Children.Add(_abdeckung.Karte);
                    }
                    _abdeckung.Setzen(abdeckung);
                }
                FertigMs ??= _uhr.Elapsed.TotalMilliseconds;
                Fertig.TrySetResult();
                if (_nochmal)
                {
                    _nochmal = false;
                    Dispatcher.UIThread.Post(() => Geladen = LadenAsync());
                }
            }
            catch (Exception fehler)
            {
                Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
                Fertig.TrySetException(fehler);
            }
        });
    }

    private void Zeigen(Auswertung a)
    {
        _kacheln.Children.Clear();
        foreach (var k in a.Kacheln)
        {
            _kacheln.Children.Add(Kachel(k.Titel, k.Wert, k.Unter));
        }
        TageZeichnen();
        var mitWerten = a.ErgebnisBeschriftungen.Count > 0;
        _ergebnisse.IsVisible = mitWerten;
        _ergebnisLeer.IsVisible = !mitWerten;
        if (mitWerten)
        {
            _ergebnisse.Daten(a.ErgebnisBeschriftungen, FortschrittTexte.ErgebnisReihe, a.ErgebnisWerte, fest: 100);
        }
        _sitzungen.Daten(a.Sitzungen.Select(s => (IReadOnlyList<string>)[s.Datum, s.Ergebnis, s.Quote, s.Note, s.Dauer]).ToList());
        _pruefungen.Daten(a.Pruefungen.Select(p => (IReadOnlyList<string>)[p.Datum, p.Name, p.Punkte, p.Note, p.Dauer]).ToList());
        _schwach.Children.Clear();
        if (a.Schwaeche is { Themen.Count: > 0 } schwach)
        {
            _schwach.Children.Add(Abstand(Schrift.Text(schwach.Titel, "Klein", FontWeight.Bold, "Pinsel.red"), 12));
            foreach (var name in schwach.Themen)
            {
                _schwach.Children.Add(UebenZeile(name, null, "Pinsel.red", "Pinsel.card_alt", () => _ = ThemaUebenAsync(name)));
            }
        }
    }

    private void TageZeichnen()
    {
        if (_auswertung is null)
        {
            return;
        }
        var ziel = Einstellungen.Ziel(Zustand.Werte);
        var tage = int.Parse(_tagePillen.Wert, System.Globalization.CultureInfo.InvariantCulture);
        var reihe = _auswertung.Reihe(tage, DateOnly.FromDateTime(DateTime.Now), ziel.ZielAn ? ziel.ZielAnzahl : null);
        _tage.Daten(reihe.Beschriftungen, FortschrittTexte.TageReihe, reihe.Werte.Select(w => (double)w).ToList(),
            ziel: reihe.Ziel is { } z ? (z, reihe.ZielText!) : null);
        _tageText.Text = reihe.Zusammenfassung;
        _anteile.Daten(reihe.Anteile);
        _anteileText.Text = reihe.AnteileText;
    }

    // -- "Jetzt üben" (practice_topic, practice_point) --

    /// <summary>Neuer Zufall wie random.Random() in Python (Startwert aus dem System).</summary>
    private static PythonZufall NeuerZufall() => new(new BigInteger(RandomNumberGenerator.GetBytes(32), isUnsigned: true));

    private async Task ThemaUebenAsync(string name)
    {
        if (await Zustand.Inhalte is not { } inhalte)
        {
            return;
        }
        await _ueben(JetztUeben.ThemenTeile(inhalte, Auswertung.BuchLesen(Pfad), JetztUeben.Themen(inhalte, name), NeuerZufall()));
    }

    private async Task PunktUebenAsync(string id)
    {
        if (_abdeckung.Aktuell is not { } abdeckung)
        {
            return;
        }
        var (quiz, karte) = abdeckung.PunktInhalte(id);
        await _ueben(JetztUeben.Teile(Auswertung.BuchLesen(Pfad), quiz, karte, NeuerZufall()));
    }

    // -- Bausteine --

    private static Control Abstand(Control element, double oben)
    {
        element.Margin = new Thickness(0, oben, 0, 0);
        return element;
    }

    private static Border Kachel(string titel, string wert, string unter) =>
        Schrift.Karte(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Schrift.Text(titel, "Normal", FontWeight.Bold, "Pinsel.text_dim"),
                Schrift.Text(wert, "Titel", FontWeight.Bold),
                Schrift.Text(unter, "Klein", FontWeight.Normal, "Pinsel.muted"),
            },
        });

    internal static Border Karte(string titel, string? unter, Control inhalt)
    {
        var kopf = new StackPanel { Spacing = 2, Children = { Schrift.Text(titel, "Mittel", FontWeight.Bold) } };
        if (unter is not null)
        {
            kopf.Children.Add(Schrift.Text(unter, "Klein", FontWeight.Normal, "Pinsel.muted"));
        }
        return Schrift.Karte(new StackPanel { Spacing = 10, Children = { kopf, inhalt } });
    }

    /// <summary>Zeile mit farbiger Markierung links und "Jetzt üben" (weak_topic_rows, CoverageCard._leaf).</summary>
    internal static Border UebenZeile(string titel, Control? unten, string markierung, string flaeche, Action? ueben, string? wert = null)
    {
        var kopf = new TitelUndWert(220);
        kopf.Children.Add(new WortText(titel, "Klein", ueben is not null && unten is null ? FontWeight.Bold : FontWeight.Normal,
            unten is null ? "Pinsel.text" : "Pinsel.text_soft"));
        if (wert is not null)
        {
            kopf.Children.Add(Schrift.Text(wert, "Klein", FontWeight.Bold, markierung));
        }
        var inhalt = new StackPanel { Spacing = 4, Children = { kopf } };
        if (unten is not null)
        {
            inhalt.Children.Add(unten);
        }
        if (ueben is not null)
        {
            var knopf = Schrift.Knopf(FortschrittTexte.Ueben);
            knopf.Margin = new Thickness(0, 2, 0, 0);
            knopf.HorizontalAlignment = HorizontalAlignment.Left;
            knopf.Click += (_, _) => ueben();
            inhalt.Children.Add(knopf);
        }
        var marke = new Border { Width = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 2, 10, 2) }
            .Farbe(Border.BackgroundProperty, markierung);
        var zeile = new DockPanel { Children = { marke, inhalt } };
        DockPanel.SetDock(marke, Dock.Left);
        return new Border
        {
            Child = zeile,
            Padding = new Thickness(10, 8),
            Margin = new Thickness(0, 6, 0, 0),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
        }.Farbe(Border.BackgroundProperty, flaeche).Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }
}

/// <summary>
/// CoverageCard: "Rahmenplan-Abdeckung" mit Ansicht (Rahmenplan, Lernfeld), Filter (nur in der
/// Ansicht Rahmenplan) und Zeilen zum Aufklappen; Unterzeilen werden erst beim Aufklappen gebaut,
/// aufgeklappte Zeilen bleiben beim Neurechnen offen.
/// </summary>
internal sealed class AbdeckungKarte
{
    private readonly Action<string> _punktUeben;
    private readonly Action<string> _themaUeben;
    private readonly Pillen _ansicht;
    private readonly Pillen _filter;
    private readonly TextBlock _hinweis = Schrift.Text(FortschrittTexte.KeineAntworten, "Klein", FontWeight.Normal, "Pinsel.text_dim");
    private readonly StackPanel _zeilen = new();
    private readonly HashSet<string> _offen = new(StringComparer.Ordinal);
    private readonly List<Expander> _klappen = [];

    public AbdeckungKarte(Action<string> punktUeben, Action<string> themaUeben)
    {
        _punktUeben = punktUeben;
        _themaUeben = themaUeben;
        _ansicht = new Pillen(FortschrittTexte.Ansichten, FortschrittTexte.AnsichtPlan, _ => Zeichnen());
        _filter = new Pillen(FortschrittTexte.Filter, FortschrittTexte.FilterAlle, _ => Zeichnen());
        _hinweis.IsVisible = false;
        Karte = FortschrittSeite.Karte(FortschrittTexte.AbdeckungTitel, FortschrittTexte.AbdeckungUnter,
            new StackPanel { Spacing = 6, Children = { _ansicht, _filter, _hinweis, _zeilen } });
    }

    public Border Karte { get; }

    public Abdeckung? Aktuell { get; private set; }

    public void Setzen(Abdeckung abdeckung)
    {
        Aktuell = abdeckung;
        Zeichnen();
    }

    /// <summary>Fuer die Layoutpruefung: je Ebene die erste Zeile aufklappen.</summary>
    public void ErsteAufklappen()
    {
        for (var i = 0; i < 4; i++)
        {
            var ersterZu = _klappen.FirstOrDefault(k => !k.IsExpanded && k.IsVisible);
            if (ersterZu is null)
            {
                break;
            }
            ersterZu.IsExpanded = true;
        }
    }

    private void Zeichnen()
    {
        _zeilen.Children.Clear();
        _klappen.Clear();
        if (Aktuell is not { } a)
        {
            return;
        }
        var plan = _ansicht.Wert == FortschrittTexte.AnsichtPlan;
        _filter.IsVisible = plan;
        _hinweis.IsVisible = !a.IrgendeineAntwort;
        if (plan)
        {
            var baum = a.Abschnitte(_filter.Wert);
            if (baum.Count == 0)
            {
                _zeilen.Children.Add(Schrift.Text(FortschrittTexte.FilterLeer, "Klein", FontWeight.Normal, "Pinsel.muted"));
            }
            foreach (var abschnitt in baum)
            {
                _zeilen.Children.Add(Ast("s:" + abschnitt.Id, abschnitt, 0, kasten =>
                {
                    foreach (var position in abschnitt.Aeste)
                    {
                        kasten.Children.Add(Ast("p:" + position.Id, position, 1, innen =>
                        {
                            foreach (var punkt in position.Blaetter)
                            {
                                innen.Children.Add(Blatt(punkt, () => _punktUeben(punkt.Id)));
                            }
                        }));
                    }
                }));
            }
        }
        else
        {
            foreach (var jahr in a.Lernfelder())
            {
                _zeilen.Children.Add(Ast("j:" + jahr.Id, jahr, 0, kasten =>
                {
                    foreach (var feld in jahr.Aeste)
                    {
                        kasten.Children.Add(Ast("f:" + feld.Id, feld, 1, innen =>
                        {
                            if (feld.Blaetter.Count == 0)
                            {
                                var leer = Schrift.Text(FortschrittTexte.KeinInhalt, "Klein", FontWeight.Normal, "Pinsel.muted");
                                leer.Margin = new Thickness(Einzug(2), 4, 0, 0);
                                innen.Children.Add(leer);
                            }
                            foreach (var thema in feld.Blaetter)
                            {
                                innen.Children.Add(Blatt(thema, () => _themaUeben(thema.Id)));
                            }
                        }));
                    }
                }));
            }
        }
    }

    private static string Farbe(AbdeckungWert wert) => wert.Stufe switch
    {
        "rot" => "Pinsel.red",
        "orange" => "Pinsel.orange",
        "gruen" => "Pinsel.green",
        _ => "Pinsel.muted",
    };

    /// <summary>_value_widgets: ohne Wert "kein Lerninhalt vorhanden".</summary>
    private static string WertText(AbdeckungWert wert) => wert.Wert is null ? FortschrittTexte.KeinInhalt : wert.Text;

    // Einzug je Ebene wie Python (depth * 22), am Handy (schmal) kleiner, damit lange Woerter passen
    private double Einzug(int ebene) => ebene * (Karte.Bounds.Width is > 0 and < 480 ? 8 : 22);

    private static ProgressBar Balken(AbdeckungWert wert, string farbe) =>
        new ProgressBar { Minimum = 0, Maximum = 100, Value = wert.Wert ?? 0, Height = 6, MinWidth = 40, IsVisible = wert.Wert is not null }
            .Farbe(ProgressBar.ForegroundProperty, farbe).Farbe(ProgressBar.BackgroundProperty, "Pinsel.card_hi");

    private Control Ast(string schluessel, AbdeckungAst ast, int ebene, Action<StackPanel> fuellen)
    {
        var farbe = Farbe(ast.Wert);
        var kopf = new TitelUndWert(220);
        kopf.Children.Add(new WortText(ast.Titel, "Klein", FontWeight.Bold, "Pinsel.text"));
        kopf.Children.Add(Schrift.Text(WertText(ast.Wert), "Klein", FontWeight.Bold, farbe));
        var oben = new StackPanel { Spacing = 4, Children = { kopf } };
        if (ast.Hinweis is { } hinweis)
        {
            oben.Children.Add(Schrift.Text(hinweis, "Klein", FontWeight.Normal, "Pinsel.accent"));
        }
        oben.Children.Add(Balken(ast.Wert, farbe));
        var marke = new Border { Width = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 2, 10, 2) }.Farbe(Border.BackgroundProperty, farbe);
        var titel = new DockPanel { Children = { marke, oben } };
        DockPanel.SetDock(marke, Dock.Left);
        var kinder = new StackPanel();
        var klappe = new Expander
        {
            Header = titel,
            Content = kinder,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(Einzug(ebene), 6, 0, 0),
            Padding = new Thickness(0),
        };
        klappe.Farbe(Expander.BackgroundProperty, "Pinsel.card_alt").Farbe(Expander.BorderBrushProperty, "Pinsel.border")
            .Farbe(Expander.ForegroundProperty, "Pinsel.text");
        Avalonia.Automation.AutomationProperties.SetName(klappe, ast.Titel);
        klappe.Expanding += (_, _) =>
        {
            _offen.Add(schluessel);
            if (kinder.Children.Count == 0)
            {
                fuellen(kinder);
            }
        };
        klappe.Collapsed += (_, _) =>
        {
            _offen.Remove(schluessel);
            kinder.Children.Clear();
        };
        _klappen.Add(klappe);
        if (_offen.Contains(schluessel))
        {
            fuellen(kinder);
            klappe.IsExpanded = true;
        }
        return klappe;
    }

    private Control Blatt(AbdeckungBlatt blatt, Action ueben)
    {
        var farbe = Farbe(blatt.Wert);
        Control? unten = null;
        if (blatt.Wert.Wert is not null)
        {
            unten = new StackPanel
            {
                Spacing = 4,
                Children = { Balken(blatt.Wert, farbe), Schrift.Text(blatt.AnzahlText, "Klein", FontWeight.Normal, "Pinsel.muted") },
            };
        }
        var zeile = FortschrittSeite.UebenZeile(blatt.Titel, unten ?? new Panel(), farbe, "Pinsel.card",
            blatt.Wert.Wert is not null ? ueben : null, WertText(blatt.Wert));
        zeile.Margin = new Thickness(Einzug(2), 6, 0, 0);
        return zeile;
    }
}
