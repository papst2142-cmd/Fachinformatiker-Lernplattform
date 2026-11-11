using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle Q1 (S 0.40.0, Weisung P12-186, Kurzplan Meldung P12-111 Abschnitte 2 bis 5, Nico 1a 2a 3a 4a 5a): Reiter „Quests“ und
/// „Questgeber“. Links die Liste mit Suche und Filtern, in der Mitte der Editor, rechts die Pruefung (antippen springt zur Quest
/// bzw. Person). Vorhandene Quests sind nur lesbar, lassen sich umhaengen und als neue Quest kopieren (1a); neue Quests sind
/// Auswahl oder Zuordnung (2a). Questgeber stehen drinnen oder draussen, hoechstens 12 draussen (4a). Ein gelber Hinweis sagt,
/// dass beides erst mit dem Einbau ins Spiel kommt (5a). Jede Aenderung ist ein Schritt im Verlauf (Rueckgaengig wie ueberall).
/// Textfelder uebernehmen beim Verlassen oder mit Enter; der Editor wird danach nicht neu gebaut (der Fokus bleibt).
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>Seite im Reiter-Bereich der Spielgeschichte: null = Spielgeschichte, sonst <see cref="Texte.Quests"/> bzw. <see cref="Texte.Questgeber"/>.</summary>
    private string? _seite;
    private Control? _questAnsicht;
    private Control? _qgAnsicht;
    private string? _questGewaehlt;
    private string? _qgGewaehlt;
    private bool _ausEditor;
    private bool _questStill;
    private int _questListeGrenze = ListeSeite;

    /// <summary>So viele Zeilen zeigt die Liste auf einmal (darunter „Weitere zeigen“), damit jede Aenderung schnell bleibt.</summary>
    public const int ListeSeite = 40;
    private string? _qgSetzen;
    private long _qgBlick;
    private int _questPruefNr;
    private List<QuestPruefung.Fund> _questFundeStand = [];
    private readonly StackPanel _questListe = new() { Spacing = 4 };
    private readonly StackPanel _questEditor = new() { Spacing = 10 };
    private readonly StackPanel _questFundeListe = new() { Spacing = 6 };
    private readonly TextBlock _questFundeTitel = Schrift.Text(Texte.QuestFundeLaeuft, "Mittel", FontWeight.Bold);
    private readonly TextBlock _questTreffer = Schrift.Text("", "Klein", farbe: "Pinsel.muted");
    private readonly TextBox _questSuche = new();
    private readonly ComboBox _questFilterTyp = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _questFilterQg = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _questFilterOrt = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private bool _questFilterSetzen;
    private readonly StackPanel _qgListe = new() { Spacing = 4 };
    private readonly StackPanel _qgEditor = new() { Spacing = 10 };
    private readonly StackPanel _qgFundeListe = new() { Spacing = 6 };
    private readonly TextBlock _qgFundeTitel = Schrift.Text(Texte.QuestFundeLaeuft, "Mittel", FontWeight.Bold);
    private readonly TextBlock _qgDraussen = Schrift.Text("", "Klein", FontWeight.Bold);

    /// <summary>Offene Seite (Start-Test): null = Spielgeschichte oder anderer Reiter.</summary>
    internal string? QuestSeite => _geschichteOffen ? _seite : null;

    /// <summary>Gewaehlte Quest bzw. Person (Start-Test).</summary>
    internal (string? Quest, string? Questgeber) QuestAuswahl => (_questGewaehlt, _qgGewaehlt);

    /// <summary>Laeuft die Pruefung der Quests (Start-Test wartet darauf)?</summary>
    internal bool QuestPruefungLaeuft { get; private set; }

    /// <summary>Funde der letzten Pruefung (Start-Test).</summary>
    internal IReadOnlyList<QuestPruefung.Fund> QuestFundeStand => _questFundeStand;

    /// <summary>Kennungen der Zeilen der Quest-Liste (Start-Test).</summary>
    internal IReadOnlyList<string> QuestListeIds { get; private set; } = [];

    /// <summary>Editor der Quest (Start-Test: Felder finden).</summary>
    internal StackPanel QuestEditor => _questEditor;

    /// <summary>Editor des Questgebers (Start-Test).</summary>
    internal StackPanel QgEditor => _qgEditor;

    /// <summary>Wird gerade ein Platz fuer eine Person gewaehlt (Kennung)?</summary>
    internal string? QgSetzenFuer => _qgSetzen;

    // ---- Seiten zeigen -----------------------------------------------------------------------------------

    /// <summary>Reiter „Quests“ bzw. „Questgeber“ zeigen.</summary>
    public void QuestSeiteZeigen(string seite)
    {
        if (_projekt is null)
        {
            return;
        }
        QgSetzenBeenden();
        Karte.SetzenBeenden();
        _geschichteOffen = true;
        _innen = false;
        _drei = false;
        _seite = seite;
        _geschichte ??= _daten is null ? null : Spielgeschichte.Aus(_daten.Inhalt, _projekt.AussenStand);
        if (seite == Texte.Quests)
        {
            _questAnsicht ??= QuestBauen();
            _inhalt.Content = _questAnsicht;
            QuestFilterFuellen();
            QuestListeZeigen();
            QuestEditorZeigen();
        }
        else
        {
            _qgAnsicht ??= QgBauen();
            _inhalt.Content = _qgAnsicht;
            QgListeZeigen();
            QgEditorZeigen();
        }
        QuestPruefenStarten();
        KnoepfeFreigeben();
    }

    /// <summary>Eine Quest im Reiter „Quests“ oeffnen.</summary>
    internal void QuestOeffnen(string? id)
    {
        _questGewaehlt = id;
        if (QuestSeite == Texte.Quests)
        {
            QuestListeZeigen();
            QuestEditorZeigen();
        }
        else
        {
            QuestSeiteZeigen(Texte.Quests);
        }
    }

    /// <summary>Eine Person im Reiter „Questgeber“ oeffnen.</summary>
    internal void QgOeffnen(string? id)
    {
        _qgGewaehlt = id;
        QuestgeberMarkenNeu();
        if (QuestSeite == Texte.Questgeber)
        {
            QgListeZeigen();
            QgEditorZeigen();
        }
        else
        {
            QuestSeiteZeigen(Texte.Questgeber);
        }
    }

    /// <summary>Nach jedem neuen Stand (aus <see cref="GeschichteNeu"/>): Marken, offene Seite, Pruefung.</summary>
    private void QuestSeitenNeu()
    {
        QuestgeberMarkenNeu();
        if (!_geschichteOffen || _seite is null || _projekt is null || _questStill)
        {
            return;
        }
        if (_seite == Texte.Quests)
        {
            if (_questGewaehlt is { } q && !_projekt.Quests().Any(x => x.Id == q))
            {
                _questGewaehlt = null;
            }
            QuestFilterFuellen();
            QuestListeZeigen();
            if (!_ausEditor)
            {
                QuestEditorZeigen();
            }
        }
        else
        {
            if (_qgGewaehlt is { } g && !_projekt.Questgeber().Any(x => x.Id == g))
            {
                _qgGewaehlt = null;
            }
            QgListeZeigen();
            if (!_ausEditor)
            {
                QgEditorZeigen();
            }
        }
        QuestPruefenStarten();
    }

    private void AusEditor(Action a)
    {
        _ausEditor = true;
        try
        {
            a();
        }
        finally
        {
            _ausEditor = false;
        }
    }

    // ---- Bausteine ---------------------------------------------------------------------------------------

    private static Control Beschriftet(string name, Control feld)
    {
        var titel = Schrift.Text(name, "Klein", FontWeight.Bold);
        titel.TextWrapping = TextWrapping.Wrap;
        var stapel = new StackPanel { Spacing = 4, Children = { titel } };
        if (feld is TextBox { AcceptsReturn: true } && !FeldTippAltFuerTests)
        {
            // Nachbesserung Q1 (Weisung P12-187): Hinweis unter der Beschriftung, nie als Schild ueber dem Feld
            stapel.Children.Add(Hinweis(Texte.QuestZeilenTipp));
        }
        stapel.Children.Add(feld);
        return stapel;
    }

    /// <summary>Kleiner grauer Hinweis (Zeile ueber den Feldern bzw. unter einer Beschriftung).</summary>
    private static TextBlock Hinweis(string text)
    {
        var t = Schrift.Text(text, "Klein", farbe: "Pinsel.muted");
        t.TextWrapping = TextWrapping.Wrap;
        return t;
    }

    /// <summary>
    /// Gegenprobe des Start-Tests (Weisung P12-187 Punkt 3): Hinweise wie vorher als Tipp am Eingabefeld (das Schild
    /// erschien mitten im Feld „Frage“).
    /// </summary>
    internal static bool FeldTippAltFuerTests { get; set; }

    /// <summary>Textfeld, das beim Verlassen (und einzeilig mit Enter) uebernimmt; <paramref name="setzen"/> laeuft nur bei einer Aenderung.</summary>
    private TextBox Feld(string name, string wert, Action<string> setzen, bool mehrzeilig = false, string? tipp = null)
    {
        var t = new TextBox
        {
            Text = wert,
            AcceptsReturn = mehrzeilig,
            TextWrapping = mehrzeilig ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = mehrzeilig ? 72 : 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        }.Vorlese(name);
        if (FeldTippAltFuerTests)
        {
            t.Mit(tipp ?? (mehrzeilig ? Texte.QuestZeilenTipp : Texte.QuestFeldTipp));
        }
        var letzter = wert;
        void Uebernehmen()
        {
            var w = t.Text ?? "";
            if (w == letzter || _projekt is null)
            {
                return;
            }
            letzter = w;
            AusEditor(() => setzen(w));
        }
        t.LostFocus += (_, _) => Uebernehmen();
        if (!mehrzeilig)
        {
            t.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Uebernehmen();
                    e.Handled = true;
                }
            };
        }
        return t;
    }

    /// <summary>Zahlfeld (ganze Zahl); falsche Eingabe meldet und setzt den alten Wert zurueck.</summary>
    private TextBox ZahlFeld(string name, long wert, Action<long> setzen)
    {
        TextBox? t = null;
        t = Feld(name, wert.ToString(CultureInfo.InvariantCulture), w =>
        {
            if (long.TryParse(w.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
            {
                setzen(z);
            }
            else
            {
                Melden(Texte.QuestZahlFalsch);
                Dispatcher.UIThread.Post(() => t!.Text = wert.ToString(CultureInfo.InvariantCulture));
            }
        });
        t.Width = 120;
        t.HorizontalAlignment = HorizontalAlignment.Left;
        return t;
    }

    private static ComboBox Wahl(string name, string tipp, IEnumerable<(string Text, object? Tag)> eintraege, object? gewaehlt, Action<object?> gewechselt)
    {
        var c = new ComboBox { MinWidth = 200, HorizontalAlignment = HorizontalAlignment.Stretch }.Vorlese(name).Mit(tipp);
        var liste = eintraege.ToList();
        // lange Listen (z. B. „Erst nach“ mit allen Quests) erst beim Aufklappen fuellen: der Editor baut schneller
        var spaeter = liste.Count > 30;
        void Fuellen(IEnumerable<(string Text, object? Tag)> was)
        {
            c.Items.Clear();
            foreach (var (text, tag) in was)
            {
                c.Items.Add(new ComboBoxItem { Content = text, Tag = tag });
            }
            c.SelectedItem = c.Items.OfType<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, gewaehlt)) ?? (c.Items.Count > 0 ? c.Items[0] : null);
        }
        Fuellen(spaeter ? liste.Where(e => Equals(e.Tag, gewaehlt)).DefaultIfEmpty(liste[0]) : liste);
        if (spaeter)
        {
            c.DropDownOpened += (_, _) =>
            {
                if (spaeter)
                {
                    spaeter = false;
                    Fuellen(liste);
                }
            };
        }
        var vorher = (c.SelectedItem as ComboBoxItem)?.Tag;
        c.SelectionChanged += (_, _) =>
        {
            if (c.SelectedItem is ComboBoxItem i && !Equals(i.Tag, vorher))
            {
                vorher = i.Tag;
                gewechselt(i.Tag);
            }
        };
        return c;
    }

    private Button Knopf(string text, string tipp, Action a, bool betont = false)
    {
        var k = Schrift.Knopf(text, betont).Mit(tipp);
        k.Click += (_, _) => a();
        return k;
    }

    private static Border GelbHinweis()
    {
        var t = Schrift.Text(Texte.QuestEinbauHinweis, "Klein", FontWeight.Bold, "Pinsel.orange");
        t.TextWrapping = TextWrapping.Wrap;
        ToolTip.SetTip(t, Texte.QuestEinbauTipp);
        return new Border
        {
            Child = t,
            Padding = new Thickness(10, 8),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
        }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.yellow");
    }

    /// <summary>Drei Spalten: links (Liste), Mitte (Editor), rechts (Pruefung), je mit Bildlauf.</summary>
    private static Grid DreiSpalten(Control links, Control mitte, Control rechts)
    {
        ScrollViewer Rollen(Control c, Thickness rand)
        {
            var karte = Schrift.Karte(c);
            karte.Margin = rand;
            return new ScrollViewer { Content = new Border { Child = karte, Padding = new Thickness(0, 0, Dialog.RandFuerLeiste, 0) } };
        }
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("320,*,330") };
        g.Children.Add(Rollen(links, new Thickness(12, 12, 6, 12)));
        var m = Rollen(mitte, new Thickness(6, 12, 6, 12));
        Grid.SetColumn(m, 1);
        g.Children.Add(m);
        var r = Rollen(rechts, new Thickness(6, 12, 12, 12));
        Grid.SetColumn(r, 2);
        g.Children.Add(r);
        return g;
    }

    /// <summary>Zeile einer Liste (Titel fett, Unterzeile), gewaehlt farbig hinterlegt.</summary>
    private static Button ListenZeile(string titel, string unter, bool gewaehlt, Action a, string tipp)
    {
        var t = Schrift.Text(titel, "Klein", FontWeight.Bold);
        t.TextWrapping = TextWrapping.Wrap;
        var u = Schrift.Text(unter, "Klein", farbe: "Pinsel.muted");
        u.TextWrapping = TextWrapping.Wrap;
        var k = new Button
        {
            Content = new StackPanel { Spacing = 2, Children = { t, u } },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 6),
        }.Vorlese(titel + ". " + unter + (gewaehlt ? ", gewählt" : "")).Mit(tipp);
        k.Farbe(Button.BackgroundProperty, gewaehlt ? "Pinsel.card_hi" : "Pinsel.card");
        k.Click += (_, _) => a();
        return k;
    }

    // ---- Reiter „Quests“ -------------------------------------------------------------------------------

    private Control QuestBauen()
    {
        _questSuche.PlaceholderText = Texte.QuestSuche;
        _questSuche.Vorlese(Texte.QuestSuche).Mit(Texte.QuestSuche);
        _questSuche.TextChanged += (_, _) =>
        {
            if (!_questFilterSetzen)
            {
                _questListeGrenze = ListeSeite;
                QuestListeZeigen();
            }
        };
        _questFilterTyp.Vorlese(Texte.QuestFilterTyp).Mit(Texte.QuestFilterTyp);
        _questFilterQg.Vorlese(Texte.QuestFilterQuestgeber).Mit(Texte.QuestFilterQuestgeber);
        _questFilterOrt.Vorlese(Texte.QuestFilterOrt).Mit(Texte.QuestFilterOrt);
        foreach (var c in new[] { _questFilterTyp, _questFilterQg, _questFilterOrt })
        {
            c.SelectionChanged += (_, _) =>
            {
                if (!_questFilterSetzen)
                {
                    _questListeGrenze = ListeSeite;
                    QuestListeZeigen();
                }
            };
        }
        var neu = new WrapPanel
        {
            ItemSpacing = 6,
            LineSpacing = 6,
            Children =
            {
                Knopf(Texte.QuestNeuAuswahl, Texte.QuestNeuAuswahlTipp, () => QuestNeuAnlegen("auswahl"), betont: true),
                Knopf(Texte.QuestNeuZuordnung, Texte.QuestNeuZuordnungTipp, () => QuestNeuAnlegen("zuordnung")),
            },
        };
        var links = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Schrift.Text(Texte.QuestListeTitel, "Mittel", FontWeight.Bold),
                neu,
                _questSuche,
                Beschriftet(Texte.QuestFilterTyp, _questFilterTyp),
                Beschriftet(Texte.QuestFilterQuestgeber, _questFilterQg),
                Beschriftet(Texte.QuestFilterOrt, _questFilterOrt),
                _questTreffer,
                _questListe,
            },
        };
        var rechts = new StackPanel { Spacing = 8, Children = { GelbHinweis(), _questFundeTitel, _questFundeListe } };
        return DreiSpalten(links, _questEditor, rechts);
    }

    internal void QuestNeuAnlegen(string typ)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return;
        }
        // neue Quest gleich bei der Person des Filters, sonst ohne Questgeber (rot, bis eine gewaehlt ist)
        var qg = (_questFilterQg.SelectedItem as ComboBoxItem)?.Tag as string;
        // still anlegen (die Seite wird danach genau einmal gebaut)
        _questStill = true;
        string id;
        try
        {
            id = _projekt.QuestNeu(typ, string.IsNullOrEmpty(qg) ? null : qg);
        }
        finally
        {
            _questStill = false;
        }
        _questFilterSetzen = true;
        _questSuche.Text = "";
        _questFilterSetzen = false;
        QuestFilterFuellen();
        _questGewaehlt = id;
        QuestListeZeigen();
        QuestEditorZeigen();
        QuestPruefenStarten();
    }

    /// <summary>Ort (Schluessel) je Questgeber, null = draussen oder ohne Platz.</summary>
    private Dictionary<string, string?> QgOrte() =>
        _projekt is null ? [] : _projekt.Questgeber().ToDictionary(q => q.Id, QuestgeberDaten.Stelle, StringComparer.Ordinal);

    private string QgName(string? id, Dictionary<string, QuestgeberDaten.Questgeber> qg) =>
        id is null ? Texte.QuestOhne : qg.TryGetValue(id, out var q) ? (q.Name.Trim().Length > 0 ? q.Name : q.Id) : id;

    private void QuestFilterFuellen()
    {
        if (_projekt is null)
        {
            return;
        }
        _questFilterSetzen = true;
        static void Fuellen(ComboBox c, IEnumerable<(string Text, object? Tag)> eintraege)
        {
            var vorher = (c.SelectedItem as ComboBoxItem)?.Tag;
            c.Items.Clear();
            c.Items.Add(new ComboBoxItem { Content = Texte.QuestFilterAlle, Tag = null });
            foreach (var (text, tag) in eintraege)
            {
                c.Items.Add(new ComboBoxItem { Content = text, Tag = tag });
            }
            c.SelectedItem = c.Items.OfType<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, vorher)) ?? c.Items[0];
        }
        var quests = _projekt.Quests();
        var qg = _projekt.Questgeber();
        Fuellen(_questFilterTyp, quests.Select(q => q.Typ).Distinct().Order(StringComparer.Ordinal).Select(t => (Texte.QuestTypName(t), (object?)t)));
        Fuellen(_questFilterQg, [(Texte.QuestOhne, ""), .. qg.Select(q => (q.Name.Trim().Length > 0 ? q.Name : q.Id, (object?)q.Id))]);
        var orte = QgOrte().Values.Select(o => o ?? "").Distinct().Order(StringComparer.Ordinal);
        Fuellen(_questFilterOrt, orte.Select(o => (o.Length == 0 ? Texte.QgPlatzDraussen : _projekt.OrtName(o), (object?)o)));
        _questFilterSetzen = false;
    }

    private void QuestListeZeigen()
    {
        _questListe.Children.Clear();
        if (_projekt is null)
        {
            QuestListeIds = [];
            return;
        }
        var alle = _projekt.Quests();
        var qg = _projekt.Questgeber().ToDictionary(q => q.Id, StringComparer.Ordinal);
        var orte = QgOrte();
        var typ = (_questFilterTyp.SelectedItem as ComboBoxItem)?.Tag as string;
        var person = (_questFilterQg.SelectedItem as ComboBoxItem)?.Tag as string;
        var ort = (_questFilterOrt.SelectedItem as ComboBoxItem)?.Tag as string;
        var suche = Katalog.Normal(_questSuche.Text ?? "").Trim();
        var ids = new List<string>();
        var treffer = new List<QuestDaten.Quest>();
        foreach (var q in alle)
        {
            var qOrt = q.Questgeber is { } g && orte.TryGetValue(g, out var o) ? o ?? "" : null;
            if ((typ is not null && q.Typ != typ) || (person is not null && (q.Questgeber ?? "") != person)
                || (ort is not null && qOrt != ort) || (suche.Length > 0 && !Katalog.Normal(q.Titel).Contains(suche, StringComparison.Ordinal)))
            {
                continue;
            }
            treffer.Add(q);
        }
        // neue Quests zuerst (an ihnen wird gearbeitet), dann die vorhandenen in der Reihenfolge des Spiels;
        // die gewaehlte Quest immer sichtbar: die Grenze waechst bis zu ihr
        treffer = [.. treffer.Where(q => !q.Bestand), .. treffer.Where(q => q.Bestand)];
        var stelle = treffer.FindIndex(q => q.Id == _questGewaehlt);
        if (stelle >= _questListeGrenze)
        {
            _questListeGrenze = ((stelle / ListeSeite) + 1) * ListeSeite;
        }
        foreach (var q in treffer.Take(_questListeGrenze))
        {
            var marke = q.Bestand ? (q.Umgehaengt ? Texte.QuestUmgehaengtMarke : Texte.QuestBestandMarke) : Texte.QuestNeuMarke;
            var unter = string.Join(" · ", new[]
            {
                Texte.QuestTypName(q.Typ), QgName(q.Questgeber, qg), Texte.GeschichteTag(q.AbTag), marke,
            });
            var id = q.Id;
            _questListe.Children.Add(ListenZeile(q.Titel, unter, id == _questGewaehlt, () => QuestOeffnen(id), Texte.TippQuests));
            ids.Add(id);
        }
        if (treffer.Count > _questListeGrenze)
        {
            _questListe.Children.Add(Knopf(Texte.ListeWeitere(treffer.Count - _questListeGrenze), Texte.ListeWeitereTipp, () =>
            {
                _questListeGrenze += ListeSeite;
                QuestListeZeigen();
            }));
        }
        QuestListeIds = [.. treffer.Select(q => q.Id)];
        _questTreffer.Text = treffer.Count == 0 ? Texte.QuestKeine : Texte.QuestTreffer(treffer.Count, alle.Count);
    }

    private void QuestEditorZeigen()
    {
        _questEditor.Children.Clear();
        if (_projekt is null || _projekt.Quests().FirstOrDefault(q => q.Id == _questGewaehlt) is not { } q)
        {
            var leer = Schrift.Text(Texte.QuestNichtsGewaehlt, "Normal", farbe: "Pinsel.muted");
            leer.TextWrapping = TextWrapping.Wrap;
            _questEditor.Children.Add(leer);
            return;
        }
        var schreiben = !_projekt.NurLesen;
        var titel = Schrift.Text(q.Titel, "Gross", FontWeight.Bold);
        titel.TextWrapping = TextWrapping.Wrap;
        _questEditor.Children.Add(titel);
        var qg = _projekt.Questgeber();
        var umhaengen = Wahl(Texte.QuestFeldQuestgeber, Texte.QuestUmhaengenTipp,
            [(Texte.QuestOhne, ""), .. qg.Select(p => ((p.Name.Trim().Length > 0 ? p.Name : p.Id) + (p.Rolle.Length > 0 ? " (" + p.Rolle + ")" : ""), (object?)p.Id))],
            q.Questgeber ?? "", t => _projekt.QuestUmhaengen(q.Id, t as string is { Length: > 0 } s ? s : null));
        umhaengen.IsEnabled = schreiben;
        if (q.Bestand)
        {
            var hinweis = Schrift.Text(Texte.QuestBestandHinweis, "Klein", farbe: "Pinsel.muted");
            hinweis.TextWrapping = TextWrapping.Wrap;
            _questEditor.Children.Add(hinweis);
            _questEditor.Children.Add(Beschriftet(Texte.QuestFeldQuestgeber, umhaengen));
            void Lesen(string name, string? text)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }
                var t = Schrift.Text(text, "Klein");
                t.TextWrapping = TextWrapping.Wrap;
                _questEditor.Children.Add(Beschriftet(name, t));
            }
            Lesen(Texte.QuestFeldTyp, Texte.QuestTypName(q.Typ));
            Lesen(Texte.QuestFeldTicket, QuestDaten.Zeilen(SpielDaten.Get(q.Daten, "ticket")));
            Lesen(Texte.QuestFeldFrage, QuestDaten.Zeilen(SpielDaten.Get(q.Daten, "frage")));
            Lesen(Texte.QuestFeldAbTag, Texte.GeschichteTag(q.AbTag));
            Lesen(Texte.QuestFeldNach, q.Nach is { } n ? _projekt.Quests().FirstOrDefault(x => x.Id == n)?.Titel ?? n : null);
            var kopieren = Knopf(Texte.QuestKopieren, Texte.QuestKopierenTipp, () =>
            {
                if (_projekt.QuestKopieren(q.Id) is { } neu)
                {
                    QuestOeffnen(neu);
                }
            }, betont: true);
            kopieren.IsEnabled = schreiben && QuestDaten.Typen.Contains(q.Typ);
            _questEditor.Children.Add(kopieren);
            return;
        }

        // neue Quest: alle Felder
        var id = q.Id;
        PyObjekt? Live() => QuestDaten.NeueQuest(_projekt.AussenStand, id);
        var felder = new StackPanel { Spacing = 10, IsEnabled = schreiben };
        _questEditor.Children.Add(felder);
        if (!FeldTippAltFuerTests)
        {
            felder.Children.Add(Hinweis(Texte.QuestFeldTipp));
        }
        felder.Children.Add(Beschriftet(Texte.QuestFeldTyp, Wahl(Texte.QuestFeldTyp, Texte.QuestTypTipp,
            QuestDaten.Typen.Select(t => (Texte.QuestTypName(t), (object?)t)), q.Typ, t => _projekt.QuestFeld(id, "typ", t))));
        felder.Children.Add(Beschriftet(Texte.QuestFeldQuestgeber, umhaengen));
        felder.Children.Add(Beschriftet(Texte.QuestFeldTitel, Feld(Texte.QuestFeldTitel, q.Titel, w => _projekt.QuestFeld(id, "titel", w))));
        felder.Children.Add(Beschriftet(Texte.QuestFeldTicket, Feld(Texte.QuestFeldTicket, QuestDaten.Zeilen(SpielDaten.Get(q.Daten, "ticket")),
            w => _projekt.QuestFeld(id, "ticket", QuestDaten.AusZeilen(w)), mehrzeilig: true)));
        felder.Children.Add(Beschriftet(Texte.QuestFeldFrage, Feld(Texte.QuestFeldFrage, Py.Str(SpielDaten.Get(q.Daten, "frage", "")),
            w => _projekt.QuestFeld(id, "frage", w), mehrzeilig: true)));
        if (q.Typ == "auswahl")
        {
            felder.Children.Add(OptionenBauen(id, Live));
        }
        else
        {
            felder.Children.Add(PaareBauen(id, Live));
        }
        felder.Children.Add(Beschriftet(Texte.QuestFeldHilfe, Feld(Texte.QuestFeldHilfe, QuestDaten.Zeilen(SpielDaten.Get(q.Daten, "hilfe")),
            w => _projekt.QuestFeld(id, "hilfe", QuestDaten.AusZeilen(w)), mehrzeilig: true)));
        felder.Children.Add(Beschriftet(Texte.QuestFeldErklaerung, Feld(Texte.QuestFeldErklaerung, QuestDaten.Zeilen(SpielDaten.Get(q.Daten, "erklaerung")),
            w => _projekt.QuestFeld(id, "erklaerung", QuestDaten.AusZeilen(w)), mehrzeilig: true)));
        felder.Children.Add(Beschriftet(Texte.QuestFeldThema, Wahl(Texte.QuestFeldThema, Texte.QuestFeldThema,
            QuestDaten.Themen.Select(t => (Texte.ThemenNamen.TryGetValue(t, out var n) ? n : t, (object?)t)),
            Py.Str(SpielDaten.Get(q.Daten, "cat", "")), t => _projekt.QuestFeld(id, "cat", t))));
        var belohnung = SpielDaten.Get(q.Daten, "belohnung") as PyObjekt;
        long Zahl(string feld) => belohnung is not null && SpielDaten.Get(belohnung, feld) is long l ? l : 0;
        void Belohnung(string feld, long wert) => _projekt.QuestAendern(id, feld == "geld" ? Texte.QuestFeldGeld : Texte.QuestFeldRuf, e =>
        {
            var b = SpielDaten.Get(e, "belohnung") as PyObjekt ?? new PyObjekt();
            var neu = new PyObjekt { { "geld", SpielDaten.Get(b, "geld", 0L) }, { "reputation", SpielDaten.Get(b, "reputation", 0L) } };
            Py.Setzen(neu, feld, wert);
            Py.Setzen(e, "belohnung", neu);
        });
        felder.Children.Add(new WrapPanel
        {
            ItemSpacing = 16,
            LineSpacing = 8,
            Children =
            {
                Beschriftet(Texte.QuestFeldGeld, ZahlFeld(Texte.QuestFeldGeld, Zahl("geld"), w => Belohnung("geld", w))),
                Beschriftet(Texte.QuestFeldRuf, ZahlFeld(Texte.QuestFeldRuf, Zahl("reputation"), w => Belohnung("reputation", w))),
                Beschriftet(Texte.QuestFeldAbTag, ZahlFeld(Texte.QuestFeldAbTag, q.AbTag, w => _projekt.QuestFeld(id, "ab_tag", w))),
            },
        });
        felder.Children.Add(Beschriftet(Texte.QuestFeldNach, Wahl(Texte.QuestFeldNach, Texte.QuestNachTipp,
            [(Texte.QuestNachKeine, ""), .. _projekt.Quests().Where(x => x.Id != id).Select(x => (x.Titel, (object?)x.Id))],
            q.Nach ?? "", t => _projekt.QuestNach(id, t as string is { Length: > 0 } s ? s : null))));
        var loeschen = Knopf(Texte.QuestLoeschen, Texte.QuestLoeschenFrage, async () =>
        {
            if (await Dialog.Rueckfrage(Texte.QuestLoeschen, Texte.QuestLoeschenFrage, 1, Texte.QuestLoeschen, Texte.Abbrechen) == 0)
            {
                _questGewaehlt = null;
                _projekt.QuestLoeschen(id);
            }
        });
        loeschen.IsEnabled = schreiben;
        _questEditor.Children.Add(loeschen);
    }

    private Control OptionenBauen(string id, Func<PyObjekt?> live)
    {
        var p = new StackPanel { Spacing = 6 };
        var q = live();
        var optionen = q is null ? [] : (SpielDaten.Get(q, "optionen") as List<object?> ?? []).Select(Py.Str).ToList();
        var antwort = q is null ? "" : Py.Str(SpielDaten.Get(q, "antwort", ""));
        p.Children.Add(Schrift.Text(Texte.QuestFeldOption, "Klein", FontWeight.Bold));
        var gruppe = "richtig-" + id;
        for (var i = 0; i < optionen.Count; i++)
        {
            var nr = i;
            var richtig = new RadioButton
            {
                GroupName = gruppe,
                IsChecked = optionen[i].Length > 0 && optionen[i] == antwort,
                Content = Schrift.Text(Texte.QuestRichtig, "Klein"),
                VerticalAlignment = VerticalAlignment.Center,
            }.Vorlese(Texte.QuestRichtigName(i + 1)).Mit(Texte.QuestRichtigTipp);
            richtig.IsCheckedChanged += (_, _) =>
            {
                if (richtig.IsChecked == true && live() is { } e && SpielDaten.Get(e, "optionen") is List<object?> l && nr < l.Count)
                {
                    _projekt!.QuestFeld(id, "antwort", Py.Str(l[nr]));
                }
            };
            var text = Feld(Texte.QuestOptionName(i + 1), optionen[i], w => _projekt!.QuestAendern(id, Texte.QuestFeldOption, e =>
            {
                var l = (SpielDaten.Get(e, "optionen") as List<object?> ?? []).ToList();
                if (nr >= l.Count)
                {
                    return;
                }
                var alt = Py.Str(l[nr]);
                l[nr] = w;
                Py.Setzen(e, "optionen", l);
                if (Py.Str(SpielDaten.Get(e, "antwort", "")) == alt && alt.Length > 0)
                {
                    Py.Setzen(e, "antwort", w);
                }
            }));
            var weg = Knopf(Texte.QuestOptionWeg, Texte.QuestOptionWegTipp, () => _projekt!.QuestAendern(id, Texte.QuestOptionWeg, e =>
            {
                var l = (SpielDaten.Get(e, "optionen") as List<object?> ?? []).ToList();
                if (nr >= l.Count || l.Count <= 2)
                {
                    return;
                }
                var alt = Py.Str(l[nr]);
                l.RemoveAt(nr);
                Py.Setzen(e, "optionen", l);
                if (Py.Str(SpielDaten.Get(e, "antwort", "")) == alt)
                {
                    Py.Setzen(e, "antwort", "");
                }
            }));
            weg.IsEnabled = optionen.Count > 2;
            var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
            zeile.Children.Add(richtig);
            Grid.SetColumn(text, 1);
            zeile.Children.Add(text);
            Grid.SetColumn(weg, 2);
            zeile.Children.Add(weg);
            p.Children.Add(zeile);
        }
        var dazu = Knopf(Texte.QuestOptionDazu, Texte.QuestOptionDazu, () => _projekt!.QuestAendern(id, Texte.QuestOptionDazu, e =>
        {
            var l = (SpielDaten.Get(e, "optionen") as List<object?> ?? []).ToList();
            if (l.Count < QuestDaten.OptionenHoechstens)
            {
                l.Add("");
                Py.Setzen(e, "optionen", l);
            }
        }));
        dazu.IsEnabled = optionen.Count < QuestDaten.OptionenHoechstens;
        p.Children.Add(dazu);
        return p;
    }

    private Control PaareBauen(string id, Func<PyObjekt?> live)
    {
        var p = new StackPanel { Spacing = 6 };
        var q = live();
        var paare = q is null ? [] : (SpielDaten.Get(q, "paare") as List<object?> ?? []).OfType<List<object?>>().ToList();
        p.Children.Add(Schrift.Text(Texte.QuestFeldPaar, "Klein", FontWeight.Bold));
        void Setzen(int nr, int seite, string w) => _projekt!.QuestAendern(id, Texte.QuestFeldPaar, e =>
        {
            var l = (SpielDaten.Get(e, "paare") as List<object?> ?? []).Select(x => x is List<object?> pl ? pl.ToList() : (object?)x).ToList();
            if (nr < l.Count && l[nr] is List<object?> paar && paar.Count == 2)
            {
                paar[seite] = w;
                Py.Setzen(e, "paare", l);
            }
        });
        for (var i = 0; i < paare.Count; i++)
        {
            var nr = i;
            var links = Feld(Texte.QuestPaarName(i + 1, false), paare[i].Count > 0 ? Py.Str(paare[i][0]) : "", w => Setzen(nr, 0, w));
            var rechts = Feld(Texte.QuestPaarName(i + 1, true), paare[i].Count > 1 ? Py.Str(paare[i][1]) : "", w => Setzen(nr, 1, w));
            var weg = Knopf(Texte.QuestPaarWeg, Texte.QuestPaarWegTipp, () => _projekt!.QuestAendern(id, Texte.QuestPaarWeg, e =>
            {
                var l = (SpielDaten.Get(e, "paare") as List<object?> ?? []).ToList();
                if (nr < l.Count && l.Count > 2)
                {
                    l.RemoveAt(nr);
                    Py.Setzen(e, "paare", l);
                }
            }));
            weg.IsEnabled = paare.Count > 2;
            var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*,Auto"), ColumnSpacing = 8 };
            zeile.Children.Add(links);
            var pfeil = Schrift.Text("→", "Normal", farbe: "Pinsel.muted");
            pfeil.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(pfeil, 1);
            zeile.Children.Add(pfeil);
            Grid.SetColumn(rechts, 2);
            zeile.Children.Add(rechts);
            Grid.SetColumn(weg, 3);
            zeile.Children.Add(weg);
            p.Children.Add(zeile);
        }
        var dazu = Knopf(Texte.QuestPaarDazu, Texte.QuestPaarDazu, () => _projekt!.QuestAendern(id, Texte.QuestPaarDazu, e =>
        {
            var l = (SpielDaten.Get(e, "paare") as List<object?> ?? []).ToList();
            if (l.Count < QuestDaten.PaareHoechstens)
            {
                l.Add(new List<object?> { "", "" });
                Py.Setzen(e, "paare", l);
            }
        }));
        dazu.IsEnabled = paare.Count < QuestDaten.PaareHoechstens;
        p.Children.Add(dazu);
        return p;
    }

    // ---- Pruefung ------------------------------------------------------------------------------------

    /// <summary>Pruefung der Quests und Questgeber im Hintergrund; der neueste Auftrag gewinnt.</summary>
    private void QuestPruefenStarten()
    {
        if (_projekt is null || !_geschichteOffen || _seite is null)
        {
            return;
        }
        var nr = ++_questPruefNr;
        var umgebung = _projekt.QuestUmgebung();
        QuestPruefungLaeuft = true;
        _questFundeTitel.Text = Texte.QuestFundeLaeuft;
        _qgFundeTitel.Text = Texte.QuestFundeLaeuft;
        _ = Task.Run(() => QuestPruefung.Pruefen(umgebung)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            if (nr != _questPruefNr)
            {
                return;
            }
            QuestPruefungLaeuft = false;
            if (t.IsFaulted)
            {
                var text = Texte.QuestPruefFehler + (t.Exception?.InnerException?.Message ?? "");
                _questFundeTitel.Text = text;
                _qgFundeTitel.Text = text;
                return;
            }
            _questFundeStand = t.Result;
            QuestFundeZeigen();
        }), TaskScheduler.Default);
    }

    private void QuestFundeZeigen()
    {
        void Fuellen(StackPanel ziel, TextBlock titel, IEnumerable<QuestPruefung.Fund> funde)
        {
            var liste = funde.OrderByDescending(f => f.Inhalt.Rot).ToList();
            titel.Text = Texte.QuestFundeTitel(liste.Count(f => f.Inhalt.Rot));
            titel.TextWrapping = TextWrapping.Wrap;
            ziel.Children.Clear();
            foreach (var f in liste)
            {
                Action? springen = f.Quest is { } q ? () => QuestOeffnen(q) : f.Questgeber is { } g ? () => QgOeffnen(g) : null;
                ziel.Children.Add(FundListe.Eintrag(f.Inhalt, springen));
            }
        }
        Fuellen(_questFundeListe, _questFundeTitel, _questFundeStand.Where(f => f.Questgeber is null || f.Quest is not null));
        Fuellen(_qgFundeListe, _qgFundeTitel, _questFundeStand.Where(f => f.Questgeber is not null && f.Quest is null));
    }

    // ---- Reiter „Questgeber“ -------------------------------------------------------------------------

    private Control QgBauen()
    {
        var neu = Knopf(Texte.QgNeu, Texte.QgNeuTipp, () =>
        {
            if (_projekt is { NurLesen: false })
            {
                QgOeffnen(_projekt.QuestgeberNeu());
            }
        }, betont: true);
        var links = new StackPanel
        {
            Spacing = 8,
            Children = { Schrift.Text(Texte.QgListeTitel, "Mittel", FontWeight.Bold), neu, _qgDraussen, _qgListe },
        };
        var rechts = new StackPanel { Spacing = 8, Children = { GelbHinweis(), _qgFundeTitel, _qgFundeListe } };
        return DreiSpalten(links, _qgEditor, rechts);
    }

    private string QgPlatzText(QuestgeberDaten.Questgeber q) => q.Platz switch
    {
        { Draussen: true } p => Texte.QgPlatzDraussenText(p.X, p.Y),
        { } p => Texte.QgPlatzText(_projekt?.OrtName(p.InnenSchluessel) ?? p.Ort),
        null when q.Bestand => Texte.QgAmAltenPlatz + (q.Stelle is { } s ? " (" + StelleName(s) + ")" : ""),
        _ => Texte.QgOhnePlatzKurz,
    };

    /// <summary>
    /// Name der Stelle im Spiel (buero = Bueroort der Karte, Kunden-Ort) ueber den Ort der Karte, nie die Kennung
    /// (Regel 274/282: Name statt Kennung). Gegenprobe <see cref="StelleAltFuerTests"/>: wie vorher die Kennung.
    /// </summary>
    internal string StelleName(string stelle)
    {
        if (_projekt is null || StelleAltFuerTests)
        {
            return _projekt?.OrtName(stelle) ?? stelle;
        }
        if (_daten is not null)
        {
            _geschichte ??= Spielgeschichte.Aus(_daten.Inhalt, _projekt.AussenStand);
        }
        return _geschichte is not null && _geschichte.OrtJeStelle.TryGetValue(stelle, out var ort) ? _projekt.OrtName(ort) : _projekt.OrtName(stelle);
    }

    /// <summary>Gegenprobe des Start-Tests: Stelle mit ihrer Kennung wie in der ersten Fassung von Q1.</summary>
    internal static bool StelleAltFuerTests { get; set; }

    private void QgListeZeigen()
    {
        _qgListe.Children.Clear();
        if (_projekt is null)
        {
            return;
        }
        var alle = _projekt.Questgeber();
        _qgDraussen.Text = Texte.QgDraussenZahlText(alle.Count(q => q.Platz is { Draussen: true }));
        foreach (var q in alle)
        {
            var id = q.Id;
            var unter = (q.Rolle.Length > 0 ? q.Rolle + " · " : "") + QgPlatzText(q);
            _qgListe.Children.Add(ListenZeile(q.Name.Trim().Length > 0 ? q.Name : q.Id, unter, id == _qgGewaehlt, () => QgOeffnen(id), Texte.TippQuestgeber));
        }
    }

    /// <summary>Innenraeume fuer den Platz drinnen: jeder Ort des Werkzeugs und seine Etagen.</summary>
    private List<string> QgInnenorte()
    {
        if (_projekt is null)
        {
            return [];
        }
        var liste = new List<string>();
        foreach (var o in _projekt.Innenorte)
        {
            liste.Add(o);
            for (var k = 1; k <= 4; k++)
            {
                var s = Etagen.Schluessel(o, k);
                if (_projekt.GibtInnenort(s))
                {
                    liste.Add(s);
                }
            }
        }
        return liste;
    }

    private void QgEditorZeigen()
    {
        _qgEditor.Children.Clear();
        if (_projekt is null || _projekt.Questgeber().FirstOrDefault(q => q.Id == _qgGewaehlt) is not { } q)
        {
            var leer = Schrift.Text(Texte.QgNichtsGewaehlt, "Normal", farbe: "Pinsel.muted");
            leer.TextWrapping = TextWrapping.Wrap;
            _qgEditor.Children.Add(leer);
            return;
        }
        var id = q.Id;
        var schreiben = !_projekt.NurLesen;
        var titel = Schrift.Text(q.Name.Trim().Length > 0 ? q.Name : q.Id, "Gross", FontWeight.Bold);
        titel.TextWrapping = TextWrapping.Wrap;
        _qgEditor.Children.Add(titel);
        var felder = new StackPanel { Spacing = 10, IsEnabled = schreiben };
        _qgEditor.Children.Add(felder);
        if (q.Bestand)
        {
            var hinweis = Schrift.Text(Texte.QgBestandHinweis, "Klein", farbe: "Pinsel.muted");
            hinweis.TextWrapping = TextWrapping.Wrap;
            felder.Children.Add(hinweis);
            if (q.Rolle.Length > 0)
            {
                felder.Children.Add(Beschriftet(Texte.QgRolle, Schrift.Text(q.Rolle, "Klein")));
            }
        }
        else
        {
            if (!FeldTippAltFuerTests)
            {
                felder.Children.Add(Hinweis(Texte.QuestFeldTipp));
            }
            felder.Children.Add(Beschriftet(Texte.QgName, Feld(Texte.QgName, q.Name, w => _projekt.QuestgeberFeld(id, "name", w))));
            felder.Children.Add(Beschriftet(Texte.QgRolle, Feld(Texte.QgRolle, q.Rolle, w => _projekt.QuestgeberFeld(id, "rolle", w))));
            var aussehen = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
            foreach (var (teil, wahl) in Aussehen.Teile.Where(t => QuestgeberDaten.AussehenTeile.Contains(t.Teil)))
            {
                var t = teil;
                var c = Wahl(Texte.QgAussehen + ": " + Texte.AussehenTeilName(t), Texte.QgAussehenWahlTipp,
                    wahl.Select(w => (w.Name, (object?)w.Kennung)), Py.Str(SpielDaten.Get(q.Aussehen, t, "")),
                    k => _projekt.QuestgeberAussehen(id, t, (string)k!));
                c.MinWidth = 160;
                aussehen.Children.Add(Beschriftet(Texte.AussehenTeilName(t), c));
            }
            felder.Children.Add(Beschriftet(Texte.QgAussehen, aussehen));
        }

        // Platz: drinnen (Ort, Etage) oder draussen, Blick, „Platz wählen“
        var platz = Schrift.Text(QgPlatzText(q), "Klein");
        platz.TextWrapping = TextWrapping.Wrap;
        _qgBlick = q.Platz?.Blick ?? _qgBlick;
        var innen = QgInnenorte();
        var ortWahl = Wahl(Texte.QgOrt, Texte.QgOrtWahlTipp,
            [(Texte.QgPlatzDraussen, ""), .. innen.Select(o => (_projekt.OrtName(o), (object?)o))],
            q.Platz switch { { Draussen: true } => "", { } p => p.InnenSchluessel, _ => q.Stelle is { } s && innen.Contains(s) ? s : "" },
            _ => { });
        var blick = Wahl(Texte.QgBlickName, Texte.QgBlickTipp2, QuestgeberDaten.Blicke.Select(b => (Texte.BlickNamen[b], (object?)b)), _qgBlick, b =>
        {
            _qgBlick = (long)b!;
            if (_projekt.Questgeber().FirstOrDefault(x => x.Id == id)?.Platz is { } p)
            {
                _projekt.QuestgeberPlatz(id, p with { Blick = _qgBlick });
            }
        });
        var waehlen = Knopf(Texte.QgPlatzWaehlen, Texte.QgPlatzWaehlenTipp,
            () => QgPlatzWaehlen(id, (ortWahl.SelectedItem as ComboBoxItem)?.Tag as string is { Length: > 0 } o ? o : null), betont: true);
        var platzKnoepfe = new WrapPanel { ItemSpacing = 8, LineSpacing = 8, Children = { waehlen } };
        if (q.Bestand && q.Platz is not null)
        {
            platzKnoepfe.Children.Add(Knopf(Texte.QgPlatzZurueck, Texte.QgPlatzZurueckTipp, () => _projekt.QuestgeberPlatzZurueck(id)));
        }
        felder.Children.Add(Beschriftet(Texte.QgPlatz, new StackPanel
        {
            Spacing = 8,
            Children = { platz, Beschriftet(Texte.QgOrt, ortWahl), Beschriftet(Texte.QgBlickName, blick), platzKnoepfe },
        }));

        // Quests dieser Person (antippen oeffnet sie)
        var quests = _projekt.Quests().Where(x => x.Questgeber == id).ToList();
        var qp = new StackPanel { Spacing = 4 };
        if (quests.Count == 0)
        {
            qp.Children.Add(Schrift.Text(Texte.QgKeineQuests, "Klein", farbe: "Pinsel.muted"));
        }
        foreach (var x in quests)
        {
            var qid = x.Id;
            qp.Children.Add(ListenZeile(x.Titel, Texte.QuestTypName(x.Typ) + " · " + Texte.GeschichteTag(x.AbTag), false, () => QuestOeffnen(qid),
                Texte.QgQuestOeffnenTipp));
        }
        _qgEditor.Children.Add(Beschriftet(Texte.QgQuests, qp));
        if (!q.Bestand)
        {
            var loeschen = Knopf(Texte.QgLoeschen, Texte.QgLoeschenFrage, async () =>
            {
                if (await Dialog.Rueckfrage(Texte.QgLoeschen, Texte.QgLoeschenFrage, 1, Texte.QgLoeschen, Texte.Abbrechen) == 0)
                {
                    _qgGewaehlt = null;
                    _projekt.QuestgeberLoeschen(id);
                }
            });
            loeschen.IsEnabled = schreiben;
            _qgEditor.Children.Add(loeschen);
        }
    }

    // ---- Platz waehlen ---------------------------------------------------------------------------------

    /// <summary>„Platz wählen“: Draufsicht (draussen) bzw. Grundriss des Innenraums oeffnen; der naechste Tipp stellt die Person hin.</summary>
    internal void QgPlatzWaehlen(string id, string? innenraum)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return;
        }
        var alle = _projekt.Questgeber();
        if (innenraum is null && alle.Count(q => q.Platz is { Draussen: true } && q.Id != id) >= QuestgeberDaten.DraussenHoechstens)
        {
            Melden(Texte.QgZuVieleDraussen);
            return;
        }
        var blick = _qgBlick;
        if (innenraum is null)
        {
            ReiterZeigen(innen: false);
            _qgSetzen = id;
            Karte.QuestgeberSetzen = (x, y) => QgGesetzt(id, new QuestgeberDaten.Platz(true, "", 0, x, y, blick));
        }
        else
        {
            ReiterZeigen(innen: true);
            OrtOeffnen(innenraum);
            _qgSetzen = id;
            var (ort, etage) = Etagen.Teilen(innenraum);
            Flaeche.QuestgeberSetzen = (x, y) => QgGesetzt(id, new QuestgeberDaten.Platz(false, ort, etage, x, y, blick));
        }
        Melden(Texte.QgPlatzWaehlenHinweis);
    }

    private void QgGesetzt(string id, QuestgeberDaten.Platz platz)
    {
        if (_projekt is null)
        {
            return;
        }
        QgSetzenBeenden();
        _projekt.QuestgeberPlatz(id, platz);
        var name = _projekt.Questgeber().FirstOrDefault(q => q.Id == id)?.Name ?? id;
        QgOeffnen(id);
        Melden(Texte.QgGesetzt(name));
    }

    /// <summary>Platz waehlen beenden (Escape, anderer Reiter, Projekt zu).</summary>
    private void QgSetzenBeenden()
    {
        _qgSetzen = null;
        Karte.QuestgeberSetzen = null;
        Flaeche.QuestgeberSetzen = null;
    }

    /// <summary>Escape beim Platz waehlen: abbrechen und zurueck zur Person.</summary>
    private bool QgSetzenAbbrechen()
    {
        if (_qgSetzen is not { } id)
        {
            return false;
        }
        QgSetzenBeenden();
        QgOeffnen(id);
        return true;
    }

    /// <summary>Marken der Personen in Draufsicht (draussen) und Grundriss (im offenen Innenraum).</summary>
    private void QuestgeberMarkenNeu()
    {
        if (_projekt is null)
        {
            Karte.QuestgeberMarken = [];
            Flaeche.QuestgeberMarken = [];
            return;
        }
        var alle = _projekt.Questgeber();
        QuestgeberMarke Marke(QuestgeberDaten.Questgeber q) =>
            new(q.Id, q.Platz!.X, q.Platz.Y, q.Platz.Blick, q.Name.Trim().Length > 0 ? q.Name : q.Id, q.Id == _qgGewaehlt);
        Karte.QuestgeberMarken = [.. alle.Where(q => q.Platz is { Draussen: true }).Select(Marke)];
        Flaeche.QuestgeberMarken = [.. alle.Where(q => q.Platz is { Draussen: false } p && p.InnenSchluessel == _ort).Select(Marke)];
        Karte.InvalidateVisual();
        Flaeche.InvalidateVisual();
    }

    // ---- Verkettungen ----------------------------------------------------------------------------------

    /// <summary>Pfeil in den Zusammenhaengen gezogen: <paramref name="auf"/> kommt erst nach <paramref name="von"/> (ein Schritt).</summary>
    private void PfeilGezogen(string von, string auf)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return;
        }
        var quests = _projekt.Quests();
        if (quests.FirstOrDefault(q => q.Id == von) is not { } vor || quests.FirstOrDefault(q => q.Id == auf) is not { } nach)
        {
            Melden(Texte.VerkettungNurQuests);
            return;
        }
        if (nach.Bestand)
        {
            Melden(Texte.VerkettungNurNeu);
            return;
        }
        _projekt.QuestNach(auf, von);
        Melden(Texte.VerkettungGesetzt(nach.Titel, vor.Titel));
    }
}
