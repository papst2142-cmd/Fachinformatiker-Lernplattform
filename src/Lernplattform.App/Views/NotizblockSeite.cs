using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 4, PB4: Notizblock wie app_gui.NotebookView (v0.63.2, 5142-5489), PC-Wortlaut auf PC und
/// Handy (Wortliste W1, H4-H9): Karte "Lernstand je Bereich", Filter (Suche 180 ms nach dem
/// letzten Tastendruck, Fachbereich, Bereich, Thema, Status, Musterantworten), Uebungsknoepfe,
/// Liste mit 12 Eintraegen je Seite, Musterantwort zum Aufklappen (bis 1 200 Zeichen) und
/// "Jetzt üben". Eintraege, Uebersicht und Suche rechnet Core.Lernen.Notizblock; nur lesend.
/// </summary>
public sealed class NotizblockSeite : UserControl
{
    /// <summary>Ueben: Quelle, Schluessel der Liste, erster Schluessel ("Jetzt üben") oder null.</summary>
    private readonly Action<string, List<object>, object?> _ueben;
    private readonly StackPanel _seite = new() { Spacing = 14, Margin = new Thickness(16) };
    private readonly HashSet<(string, object)> _offen = [];
    private readonly List<Action> _wartend = [];
    private readonly System.Diagnostics.Stopwatch _uhr = System.Diagnostics.Stopwatch.StartNew();
    private Lerninhalte? _inhalte;
    private Notizblock _notiz = null!;
    private StatusBuch? _buch;
    private List<NotizEintrag> _alle = [];
    private List<NotizEintrag> _eintraege = [];
    private string _anfrage = "";
    private int _seitenNr;
    private DispatcherTimer? _sucheSpaeter;

    private Spaltenraster _kacheln = null!;
    private TextBox _suche = null!;
    private Pillen _fachbereich = null!;
    private Pillen _bereich = null!;
    private ComboBox _thema = null!;
    private readonly List<string?> _themenIds = [];
    private bool _themenSetzen;
    private Pillen _status = null!;
    private Pillen _antworten = null!;
    private WrapPanel _uebenLeiste = null!;
    private TextBlock _listeTitel = null!;
    private TextBlock _listeUnter = null!;
    private StackPanel _zeilen = null!;
    private TextBlock _seiteText = null!;
    private Button _zurueck = null!;
    private Button _weiter = null!;

    public NotizblockSeite(Action<string, List<object>, object?> ueben)
    {
        _ueben = ueben;
        _seite.Children.Add(Schrift.Text(FarbenTexte.Laden, "Normal", FontWeight.Normal, "Pinsel.text_dim"));
        Content = new ScrollViewer { Content = _seite };
        Geladen = LadenAsync();
    }

    /// <summary>Fertig aufgebaut (Lernstand gelesen, Liste gezeigt).</summary>
    public Task Geladen { get; }

    /// <summary>Zeit vom Bauen der Seite bis die Liste steht (Messwert).</summary>
    public double? InhaltMs { get; private set; }

    /// <summary>Eintraege nach Filtern und Suche (fuer Tests und Messung).</summary>
    internal IReadOnlyList<NotizEintrag> Eintraege => _eintraege;

    private async Task LadenAsync()
    {
        try
        {
            var inhalte = await Zustand.Inhalte;
            if (inhalte is null)
            {
                return;
            }
            var notiz = new Notizblock(inhalte);
            var buch = await Task.Run(() =>
            {
                var b = Zustand.BuchLesen();
                _ = notiz.Eintraege(b);
                return b;
            });
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _inhalte = inhalte;
                _notiz = notiz;
                _buch = buch;
                Aufbauen(inhalte);
                Neu();
                foreach (var schritt in _wartend)
                {
                    schritt();
                }
                _wartend.Clear();
                InhaltMs = _uhr.Elapsed.TotalMilliseconds;
            });
            // Suchtext je Frage im Hintergrund vorbereiten (die erste Suche baut sonst alle Musterantworten)
            var alle = _notiz.Eintraege(buch, FragenStatus.Alle);
            await Task.Run(() =>
            {
                foreach (var eintrag in alle)
                {
                    lock (_notiz)
                    {
                        _ = _notiz.Suchtext(eintrag);
                    }
                }
            });
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
    }

    /// <summary>on_show: Lernstand neu lesen (neue Antworten, Abgleich).</summary>
    public void Gezeigt()
    {
        if (_inhalte is not null)
        {
            _buch = null;
            Neu(seiteBehalten: true);
        }
    }

    private static TextBlock Ueberschrift(string text)
    {
        var label = LernTeile.Label(text);
        label.Margin = new Thickness(0, 6, 0, 2);
        return label;
    }

    private void Aufbauen(Lerninhalte inhalte)
    {
        _seite.Children.Clear();
        _kacheln = new Spaltenraster(150, 6);
        _seite.Children.Add(Schrift.Karte(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Schrift.Text(NotizblockTexte.UebersichtTitel, "Mittel", FontWeight.Bold),
                Schrift.Text(NotizblockTexte.UebersichtUnter, "Klein", FontWeight.Normal, "Pinsel.muted"),
                _kacheln,
            },
        }));

        _suche = new TextBox { PlaceholderText = NotizblockTexte.SucheHinweis, HorizontalAlignment = HorizontalAlignment.Stretch, MaxWidth = 520 }
            .Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt").Groesse();
        _suche.HorizontalAlignment = HorizontalAlignment.Left;
        _suche.MinWidth = 260;
        Avalonia.Automation.AutomationProperties.SetName(_suche, NotizblockTexte.SucheHinweis);
        _suche.TextChanged += (_, _) => SucheSpaeter();
        _fachbereich = new Pillen(Fachbereiche.Alle.Select(f => (f, Fachbereiche.Kurzname[f])).Prepend((FragenStatus.Alle, NotizblockTexte.Alle)),
            FragenStatus.Alle, _ => { ThemenZeigen(); Neu(); });
        _bereich = new Pillen(Quellen.Alle.Select(q => (q, NotizblockTexte.QuelleMehrzahl[q])).Prepend((FragenStatus.Alle, NotizblockTexte.Alle)),
            FragenStatus.Alle, _ => Neu());
        _thema = LernTeile.Themenliste();
        _thema.SelectionChanged += (_, _) =>
        {
            if (!_themenSetzen && _thema.SelectedIndex >= 0)
            {
                Neu();
            }
        };
        _status = new Pillen(NotizblockTexte.StatusPillen, FragenStatus.Ueben, _ => Neu());
        _antworten = new Pillen([(NotizblockTexte.Aus, NotizblockTexte.AusText), (NotizblockTexte.An, NotizblockTexte.AnText)], NotizblockTexte.Aus,
            wert => AlleUmschalten(wert));
        _seite.Children.Add(Schrift.Karte(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Ueberschrift(NotizblockTexte.Suche), _suche,
                Ueberschrift(NotizblockTexte.Fachbereich), _fachbereich,
                Ueberschrift(NotizblockTexte.Bereich), _bereich,
                Ueberschrift(NotizblockTexte.Thema), _thema,
                Ueberschrift(NotizblockTexte.Status), _status,
                Ueberschrift(NotizblockTexte.Musterantworten), _antworten,
            },
        }));
        ThemenZeigen();

        _uebenLeiste = new WrapPanel { IsVisible = false };
        _seite.Children.Add(_uebenLeiste);

        _listeTitel = Schrift.Text("", "Mittel", FontWeight.Bold, "Pinsel.red");
        _listeUnter = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.muted");
        _zeilen = new StackPanel { Spacing = 8 };
        _seiteText = Schrift.Text("", "Normal", FontWeight.Bold, "Pinsel.text_dim");
        _seiteText.HorizontalAlignment = HorizontalAlignment.Center;
        _seiteText.VerticalAlignment = VerticalAlignment.Center;
        _zurueck = Pfeil("←", SzenarienTexte.SeiteZurueck, -1);
        _weiter = Pfeil("→", SzenarienTexte.SeiteWeiter, 1);
        var blaettern = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 10, 0, 0) };
        Grid.SetColumn(_seiteText, 1);
        Grid.SetColumn(_weiter, 2);
        blaettern.Children.Add(_zurueck);
        blaettern.Children.Add(_seiteText);
        blaettern.Children.Add(_weiter);
        _seite.Children.Add(Schrift.Karte(new StackPanel { Spacing = 8, Children = { _listeTitel, _listeUnter, _zeilen, blaettern } }));
    }

    private Button Pfeil(string zeichen, string name, int schritt)
    {
        var knopf = LernTeile.Knopf(zeichen);
        knopf.Margin = new Thickness(0);
        ToolTip.SetTip(knopf, name);
        Avalonia.Automation.AutomationProperties.SetName(knopf, name);
        knopf.Click += (_, _) =>
        {
            _seitenNr += schritt;
            Zeichnen();
        };
        return knopf;
    }

    /// <summary>TopicMenu.set_category: Themen des Fachbereichs (die Auswahl geht auf "Alle").</summary>
    private void ThemenZeigen()
    {
        _themenSetzen = true;
        var themen = Lernregeln.ThemenFuer(_inhalte!, _fachbereich.Wert);
        _themenIds.Clear();
        _themenIds.Add(null);
        _themenIds.AddRange(themen.Select(t => (string?)t.Id));
        _thema.ItemsSource = themen.Select(t => t.Name).Prepend(NotizblockTexte.Alle).ToList();
        _thema.SelectedIndex = 0;
        _themenSetzen = false;
    }

    private string Thema => _thema.SelectedIndex > 0 ? _themenIds[_thema.SelectedIndex] ?? FragenStatus.Alle : FragenStatus.Alle;

    /// <summary>_search_later: Suche 180 ms nach dem letzten Tastendruck.</summary>
    private void SucheSpaeter()
    {
        _sucheSpaeter?.Stop();
        _sucheSpaeter = new DispatcherTimer(TimeSpan.FromMilliseconds(180), DispatcherPriority.Normal, (_, _) =>
        {
            _sucheSpaeter?.Stop();
            _sucheSpaeter = null;
            SucheAnwenden();
        });
        _sucheSpaeter.Start();
    }

    /// <summary>_apply_search: nur die Stichwortsuche neu (Eintraege nach den Filtern liegen schon vor).</summary>
    internal void SucheAnwenden()
    {
        var anfrage = _suche.Text ?? "";
        if (Notizblock.GleicheAnfrage(anfrage, _anfrage))
        {
            _anfrage = anfrage;
            return;
        }
        _anfrage = anfrage;
        lock (_notiz)
        {
            _eintraege = _notiz.Suchen(_alle, anfrage);
        }
        if (_antworten.Wert == NotizblockTexte.An)
        {
            AlleOeffnen();
        }
        _seitenNr = 0;
        UebenZeigen();
        Zeichnen();
    }

    /// <summary>refresh: Lernstand (falls neu), Filter, Suche, Uebersicht, Uebungsknoepfe, Liste.</summary>
    private void Neu(bool seiteBehalten = false)
    {
        if (_inhalte is null)
        {
            return;
        }
        _buch ??= Zustand.BuchLesen();
        var fach = _fachbereich.Wert;
        var thema = Thema;
        var quelle = _bereich.Wert;
        _alle = _notiz.Eintraege(_buch, _status.Wert, fach, thema, quelle == FragenStatus.Alle ? null : [quelle]);
        lock (_notiz)
        {
            _eintraege = _notiz.Suchen(_alle, _anfrage);
        }
        if (_antworten.Wert == NotizblockTexte.An)
        {
            AlleOeffnen();
        }
        if (!seiteBehalten)
        {
            _seitenNr = 0;
        }
        UebersichtZeigen(fach, thema);
        UebenZeigen();
        Zeichnen();
    }

    private void AlleOeffnen()
    {
        _offen.Clear();
        foreach (var e in _eintraege)
        {
            _offen.Add((e.Quelle, e.Schluessel));
        }
    }

    /// <summary>_toggle_all: Musterantworten aller Eintraege ein- oder ausblenden.</summary>
    private void AlleUmschalten(string wert)
    {
        if (wert == NotizblockTexte.An)
        {
            AlleOeffnen();
        }
        else
        {
            _offen.Clear();
        }
        Zeichnen();
    }

    private void UebersichtZeigen(string fach, string thema)
    {
        _kacheln.Children.Clear();
        var uebersicht = _notiz.Uebersicht(_buch!, fach, thema);
        foreach (var quelle in Quellen.Alle)
        {
            var werte = uebersicht[quelle];
            var gesamt = werte.Values.Sum();
            var teiler = gesamt == 0 ? 1 : gesamt;
            _kacheln.Children.Add(new Border
            {
                Padding = new Thickness(12, 10),
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        Schrift.Text(NotizblockTexte.QuelleMehrzahl[quelle], "Klein", FontWeight.Bold, "Pinsel." + NotizblockTexte.QuelleFarbe[quelle]),
                        Schrift.Text(NotizblockTexte.ZuUeben(werte[FragenStatus.Ueben]), "Gross", FontWeight.Bold,
                            werte[FragenStatus.Ueben] > 0 ? "Pinsel.red" : "Pinsel.text_dim"),
                        Schrift.Text(NotizblockTexte.Abgeschlossen(werte[FragenStatus.Fertig], teiler), "Klein", FontWeight.Normal, "Pinsel.text_dim"),
                        new ProgressBar { Minimum = 0, Maximum = 1, Value = werte[FragenStatus.Fertig] / (double)teiler, Height = 6, MinWidth = 40 }
                            .Farbe(ProgressBar.ForegroundProperty, "Pinsel.green").Farbe(ProgressBar.BackgroundProperty, "Pinsel.card_hi"),
                        Schrift.Text(NotizblockTexte.OffenText(werte[FragenStatus.Offen]), "Klein", FontWeight.Normal, "Pinsel.muted"),
                    },
                },
            }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border"));
        }
    }

    private List<object> Schluessel(string quelle) => [.. _eintraege.Where(e => e.Quelle == quelle).Select(e => e.Schluessel)];

    /// <summary>_paint_practice: Knoepfe nur mit Fragen, ohne beide keine Leiste.</summary>
    private void UebenZeigen()
    {
        _uebenLeiste.Children.Clear();
        var karten = Schluessel(Quellen.Karte);
        var fragen = Schluessel(Quellen.Quiz);
        _uebenLeiste.IsVisible = karten.Count > 0 || fragen.Count > 0;
        if (karten.Count > 0)
        {
            var knopf = LernTeile.Knopf(NotizblockTexte.KartenKnopf(karten.Count, _status.Wert), true);
            knopf.Click += (_, _) => Ueben(Quellen.Karte, karten, null);
            _uebenLeiste.Children.Add(knopf);
        }
        if (fragen.Count > 0)
        {
            var knopf = LernTeile.Knopf(NotizblockTexte.FragenKnopf(fragen.Count, _status.Wert), true);
            knopf.Click += (_, _) => Ueben(Quellen.Quiz, fragen, null);
            _uebenLeiste.Children.Add(knopf);
        }
        var hinweis = Schrift.Text(NotizblockTexte.UebenHinweis, "Normal", FontWeight.Normal, "Pinsel.muted");
        hinweis.VerticalAlignment = VerticalAlignment.Center;
        hinweis.Margin = new Thickness(6, 0, 0, 8);
        _uebenLeiste.Children.Add(hinweis);
    }

    /// <summary>practice: ohne Fragen Hinweis; "Jetzt üben" stellt die Frage nach vorn.</summary>
    private void Ueben(string quelle, List<object> schluessel, object? erste)
    {
        if (schluessel.Count == 0)
        {
            _ = Dialog.Zeigen(NotizblockTexte.HinweisTitel, NotizblockTexte.KeineFragenDazu, Dialog.Ok);
            return;
        }
        if (erste is not null)
        {
            schluessel = [erste, .. schluessel.Where(k => !Equals(k, erste))];
        }
        _ueben(quelle, schluessel, erste);
    }

    private void Zeichnen()
    {
        var status = _status.Wert;
        _listeTitel.Text = NotizblockTexte.ListeTitel(status);
        _listeTitel.Farbe(TextBlock.ForegroundProperty, LernTeile.Pinsel(status == FragenStatus.Ueben ? FragenStatus.Rot : status));
        var (sichtbar, seite, seiten) = Listen.Seite(_eintraege, _seitenNr, NotizblockTexte.Seitengroesse);
        _seitenNr = seite;
        _listeUnter.Text = NotizblockTexte.ListeUnter(_eintraege.Count);
        _zeilen.Children.Clear();
        if (sichtbar.Count == 0)
        {
            _zeilen.Children.Add(Schrift.Text(NotizblockTexte.Leer(status, _anfrage, _alle.Count > 0), "Normal", FontWeight.Normal, "Pinsel.text_soft"));
        }
        foreach (var eintrag in sichtbar)
        {
            _zeilen.Children.Add(Zeile(eintrag));
        }
        _seiteText.Text = SzenarienTexte.SeiteText(seite, seiten);
        _zurueck.IsEnabled = seite > 0;
        _weiter.IsEnabled = seite < seiten - 1;
    }

    private Border Zeile(NotizEintrag eintrag)
    {
        var farbe = LernTeile.Pinsel(eintrag.Stufe.Length > 0 ? eintrag.Stufe : eintrag.Status);
        var (statusText, _) = KartenTexte.StatusZeile(_buch!, eintrag.Quelle, eintrag.Schluessel);
        var thema = eintrag.Eintrag.Thema is { } t && _inhalte!.ThemaNach.TryGetValue(t, out var bekannt) ? bekannt.Kurz ?? "" : "";
        var kopf = new WrapPanel
        {
            Children =
            {
                new LernText(NotizblockTexte.Kopf(eintrag.Quelle, eintrag.Eintrag, thema), "Klein", FontWeight.Normal, "Pinsel.muted") { Margin = new Thickness(0, 0, 12, 0) },
                new TextBlock { Text = statusText, TextWrapping = TextWrapping.Wrap }.Groesse("Klein").Farbe(TextBlock.ForegroundProperty, farbe),
            },
        };
        var inhalt = new StackPanel { Spacing = 4, Children = { kopf, new LernText(NotizblockTexte.EintragTitel(eintrag), "Normal", FontWeight.Bold, "Pinsel.text") } };
        var kennung = (eintrag.Quelle, eintrag.Schluessel);
        var offen = _offen.Contains(kennung);
        if (offen)
        {
            var antwort = new LernText(NotizblockTexte.Gekuerzt(Notizblock.Musterantwort(eintrag.Quelle, eintrag.Eintrag)), "Normal", FontWeight.Normal, "Pinsel.text_dim");
            antwort.Margin = new Thickness(0, 4, 0, 0);
            inhalt.Children.Add(antwort);
        }
        var umschalten = LernTeile.Knopf(offen ? NotizblockTexte.AntwortAusblenden : NotizblockTexte.AntwortZeigen);
        umschalten.Click += (_, _) =>
        {
            if (!_offen.Remove(kennung))
            {
                _offen.Add(kennung);
            }
            Zeichnen();
        };
        var jetzt = LernTeile.Knopf(NotizblockTexte.JetztUeben, true);
        jetzt.Click += (_, _) => Ueben(eintrag.Quelle, Schluessel(eintrag.Quelle), eintrag.Schluessel);
        inhalt.Children.Add(new WrapPanel { Margin = new Thickness(0, 8, 0, 0), Children = { umschalten, jetzt } });
        var streifen = new Border { Width = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 2, 10, 2) }.Farbe(Border.BackgroundProperty, farbe);
        var zeile = new DockPanel { Children = { streifen, inhalt } };
        DockPanel.SetDock(streifen, Dock.Left);
        return new Border
        {
            Child = zeile,
            Padding = new Thickness(10, 10),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
        }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }

    /// <summary>
    /// Nur fuer den Start ohne Bedienung: 0 = Status "Offen" (volle Liste) mit allen Musterantworten,
    /// 1 = Suche ohne Treffer, 2 = Suche "netz" sofort angewendet, -1 = zurueck auf den Anfang.
    /// </summary>
    internal void Probe(int art)
    {
        if (_inhalte is null)
        {
            return;
        }
        switch (art)
        {
            case 0:
                _status.Waehle(FragenStatus.Offen);
                _antworten.Waehle(NotizblockTexte.An);
                Neu();
                break;
            case 1:
                _suche.Text = "zz-nichts";
                SucheAnwenden();
                break;
            case 2:
                _suche.Text = "netz";
                SucheAnwenden();
                break;
            default:
                _sucheSpaeter?.Stop();
                _suche.Text = "";
                _status.Waehle(FragenStatus.Ueben);
                _antworten.Waehle(NotizblockTexte.Aus);
                _offen.Clear();
                _anfrage = "";
                Neu();
                break;
        }
    }
}
