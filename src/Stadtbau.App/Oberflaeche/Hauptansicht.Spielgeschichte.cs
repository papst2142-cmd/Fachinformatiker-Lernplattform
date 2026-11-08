using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.22.0 „Spielgeschichte ansehen“ (Weisung P12-91, Kurzplan Meldung P12-59, Nico 1a 2a 3a 4a 5a), nur lesen:
/// Bild 1 Zahl am Haus (nur Aufgaben der Spielgeschichte, 3a) und rechts die Karte „Spielgeschichte an diesem Ort“;
/// Bild 2 Reiter „Spielgeschichte“ mit der Liste „Aufgabe → Ort“ (Filter Ort, Tag, Person, Suche); Bild 4 (4a) gelbe
/// Zeile beim gewaehlten Ort mit Aufgaben, kein Fenster. Ansicht „Zusammenhänge“ (Weisung P12-92, Entwurf P12-60) als
/// erste Seite des Reiters; ein angetippter Kasten zeigt rechts dieselbe Karte wie Bild 1.
/// </summary>
public sealed partial class Hauptansicht
{
    private Spielgeschichte? _geschichte;
    private bool _geschichteOffen;
    private Control? _geschichteAnsicht;
    private readonly StackPanel _geschichteOrt = new() { Spacing = 6 };
    private Border? _geschichteOrtKarte;
    private readonly ComboBox _geschichteFilterOrt = new() { MinWidth = 180 };
    private readonly ComboBox _geschichteFilterTag = new() { MinWidth = 140 };
    private readonly ComboBox _geschichteFilterPerson = new() { MinWidth = 180 };
    private readonly TextBox _geschichteSuche = new() { MinWidth = 220 };
    private readonly TextBlock _geschichteZeile = Schrift.Text("", "Klein", farbe: "Pinsel.muted");
    private readonly TextBlock _geschichteTreffer = Schrift.Text("", "Klein", farbe: "Pinsel.muted");
    private readonly StackPanel _geschichteZeilen = new();
    private string? _geschichteHervor;
    private bool _geschichteFilterSetzen;
    private readonly ZusammenhangAnsicht _zusammenhang = new();
    private readonly ContentControl _geschichteMitte = new();
    private readonly StackPanel _geschichteSeiteOrt = new() { Spacing = 6 };
    private Border? _geschichteSeiteRechts;
    private Control? _geschichteListeSeite;
    private bool _geschichteListe;
    private string? _geschichteSeiteOrtName;

    /// <summary>Ansicht „Zusammenhänge“ (Start-Test).</summary>
    internal ZusammenhangAnsicht Zusammenhang => _zusammenhang;

    /// <summary>Zeigt der Reiter die Liste „Aufgabe → Ort“ (sonst die Zusammenhänge)?</summary>
    internal bool GeschichteListeOffen => _geschichteOffen && _geschichteListe;

    /// <summary>Karte rechts im Reiter (Kasten angetippt): sichtbar, Ort.</summary>
    internal (bool Sichtbar, string? Ort) GeschichteSeiteRechts => (_geschichteSeiteRechts?.IsVisible == true, _geschichteSeiteOrtName);

    /// <summary>Texte der Karte rechts im Reiter (Start-Test).</summary>
    internal IEnumerable<string> GeschichteSeiteTexte => Texte_(_geschichteSeiteOrt);

    /// <summary>Spielgeschichte zum aktuellen Stand (null ohne Projekt).</summary>
    internal Spielgeschichte? Geschichte => _geschichte;

    /// <summary>Ist der Reiter „Spielgeschichte“ offen?</summary>
    internal bool GeschichteOffen => _geschichteOffen;

    /// <summary>Zeilen der Liste (Start-Test): Kennung, hervorgehoben.</summary>
    internal IReadOnlyList<(string Id, bool Hervor)> GeschichteListe { get; private set; } = [];

    /// <summary>Ort der hervorgehobenen Zeilen (Start-Test).</summary>
    internal string? GeschichteHervor => _geschichteHervor;

    /// <summary>Gelbe Zeile bei der Auswahl (Bild 4), null = keine (Start-Test).</summary>
    internal string? GeschichteGelbText { get; private set; }

    /// <summary>Karte rechts „Spielgeschichte an diesem Ort“ (Start-Test: sichtbar, Inhalt).</summary>
    internal Border? GeschichteOrtKarte => _geschichteOrtKarte;

    /// <summary>Texte der Karte rechts (Start-Test).</summary>
    internal IEnumerable<string> GeschichteOrtTexte => Texte_(_geschichteOrt);

    private static IEnumerable<string> Texte_(StackPanel p) => p.Children.OfType<TextBlock>().Select(t => t.Text ?? "")
        .Concat(p.Children.OfType<Grid>().SelectMany(g => g.Children.OfType<TextBlock>()).Select(t => t.Text ?? ""));

    /// <summary>Filter der Liste (Start-Test).</summary>
    internal (ComboBox Ort, ComboBox Tag, ComboBox Person, TextBox Suche) GeschichteFilter =>
        (_geschichteFilterOrt, _geschichteFilterTag, _geschichteFilterPerson, _geschichteSuche);

    /// <summary>Nach jedem neuen Stand: Zuordnung neu, Zahlen am Haus, offene Liste neu.</summary>
    private void GeschichteNeu()
    {
        _geschichte = _projekt is null || _daten is null ? null : Spielgeschichte.Aus(_daten.Inhalt, _projekt.AussenStand);
        Karte.GeschichteZahlen = _geschichte?.Zahlen();
        if (_geschichteOffen)
        {
            GeschichteFilterFuellen();
            GeschichteListeZeigen();
            GeschichteMitteZeigen();
            GeschichteSeiteOrtZeigen(_geschichteSeiteOrtName);
        }
    }

    private Border GeschichteOrtKarteBauen()
    {
        _geschichteOrtKarte = Schrift.Karte(_geschichteOrt);
        _geschichteOrtKarte.IsVisible = false;
        return _geschichteOrtKarte;
    }

    /// <summary>Teil der Auswahl draussen: gelbe Zeile (4a) und die Karte rechts (Bild 1) fuer einen Ort mit Spielgeschichte.</summary>
    private void GeschichteTeil(AussenStelle? s)
    {
        _geschichteOrt.Children.Clear();
        GeschichteGelbText = null;
        var ort = s is { Art: AussenArt.Haus } h && _geschichte is { } g && g.HatEtwas(h.Ort) ? h.Ort : null;
        if (_geschichteOrtKarte is { } karte)
        {
            karte.IsVisible = ort is not null;
        }
        if (ort is null || _geschichte is not { } gs || _projekt is null)
        {
            return;
        }
        var zahl = gs.Zahl(ort);
        if (zahl > 0)
        {
            GeschichteGelbText = Texte.GeschichteGelb(zahl);
            var gelb = Schrift.Text(GeschichteGelbText, "Klein", FontWeight.Bold, "Pinsel.orange");
            gelb.TextWrapping = TextWrapping.Wrap;
            _aussenAuswahl.Children.Add(gelb);
        }
        GeschichteOrtFuellen(_geschichteOrt, ort, null);
    }

    /// <summary>Inhalt der Karte „Spielgeschichte an diesem Ort“ (Bild 1), draussen und im Reiter gleich.</summary>
    private void GeschichteOrtFuellen(StackPanel ziel, string ort, Action? schliessen)
    {
        ziel.Children.Clear();
        if (_geschichte is not { } gs || _projekt is null)
        {
            return;
        }
        var zahl = gs.Zahl(ort);
        var titel = Schrift.Text(Texte.GeschichteAnOrt, "Mittel", FontWeight.Bold);
        if (schliessen is not null)
        {
            var zu = Schrift.Knopf(Texte.GeschichteRechtsZu).Vorlese(Texte.GeschichteRechtsZu);
            zu.Click += (_, _) => schliessen();
            var kopf = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
            titel.TextWrapping = TextWrapping.Wrap;
            kopf.Children.Add(titel);
            Grid.SetColumn(zu, 1);
            kopf.Children.Add(zu);
            ziel.Children.Add(kopf);
        }
        else
        {
            ziel.Children.Add(titel);
        }
        ziel.Children.Add(Schrift.Text(Texte.GeschichteUnterzeile(_projekt.OrtName(ort), gs.FreiAbTag(ort)), "Klein", farbe: "Pinsel.muted"));
        void Abschnitt(string text, IReadOnlyList<Spielgeschichte.Eintrag> liste)
        {
            if (liste.Count == 0)
            {
                return;
            }
            ziel.Children.Add(Schrift.Text(text, gewicht: FontWeight.Bold));
            foreach (var e in liste)
            {
                var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
                var tag = Schrift.Text(Texte.GeschichteTag(e.AbTag), "Klein", FontWeight.Bold, "Pinsel.accent");
                tag.MinWidth = 52;
                var titelText = Schrift.Text(e.Titel, "Klein");
                titelText.TextWrapping = TextWrapping.Wrap;
                Grid.SetColumn(titelText, 1);
                zeile.Children.Add(tag);
                zeile.Children.Add(titelText);
                ziel.Children.Add(zeile);
            }
        }
        Abschnitt(Texte.GeschichteAufgaben(zahl), gs.AnOrt(ort, Spielgeschichte.Art.Aufgabe));
        Abschnitt(Texte.GeschichteAuftraege(gs.AnOrt(ort, Spielgeschichte.Art.Auftrag).Count), gs.AnOrt(ort, Spielgeschichte.Art.Auftrag));
        Abschnitt(Texte.GeschichteZwischenfaelle(gs.AnOrt(ort, Spielgeschichte.Art.Zwischenfall).Count), gs.AnOrt(ort, Spielgeschichte.Art.Zwischenfall));
        var alle = Schrift.Knopf(Texte.GeschichteAlle).Mit(Texte.GeschichteAlleTipp);
        alle.Click += (_, _) => GeschichteZeigen(ort);
        ziel.Children.Add(alle);
    }

    /// <summary>Karte rechts im Reiter fuer einen angetippten Kasten (null = zu).</summary>
    private void GeschichteSeiteOrtZeigen(string? ort)
    {
        if (ort is not null && _geschichte?.HatEtwas(ort) != true)
        {
            ort = null;
        }
        _geschichteSeiteOrtName = ort;
        if (ort is null)
        {
            _geschichteSeiteOrt.Children.Clear();
        }
        else
        {
            GeschichteOrtFuellen(_geschichteSeiteOrt, ort, () => GeschichteSeiteOrtZeigen(null));
        }
        if (_geschichteSeiteRechts is { } r)
        {
            r.IsVisible = ort is not null;
        }
    }

    // ---- Reiter „Spielgeschichte“ ------------------------------------------------------------------

    /// <summary>
    /// Reiter „Spielgeschichte“ zeigen: ohne <paramref name="ort"/> die Zusammenhänge; mit Ort (Knopf „Alle in der
    /// Spielgeschichte“) die Liste ab dem Tag, an dem der Ort ins Spiel kommt, und seine Aufgaben hervorgehoben.
    /// </summary>
    public void GeschichteZeigen(string? ort = null)
    {
        if (_projekt is null)
        {
            return;
        }
        Karte.SetzenBeenden();
        _geschichteOffen = true;
        _innen = false;
        _drei = false;
        _geschichteListe = ort is not null;
        _geschichteAnsicht ??= GeschichteBauen();
        _geschichte ??= _daten is null ? null : Spielgeschichte.Aus(_daten.Inhalt, _projekt.AussenStand);
        GeschichteSeiteOrtZeigen(null);
        GeschichteMitteZeigen();
        GeschichteFilterFuellen();
        _geschichteFilterSetzen = true;
        _geschichteHervor = ort;
        _geschichteFilterOrt.SelectedIndex = 0;
        _geschichteFilterPerson.SelectedIndex = 0;
        _geschichteSuche.Text = "";
        var ab = ort is not null && _geschichte?.FreiAbTag(ort) is { } t && t > 1 ? t : (long?)null;
        _geschichteFilterTag.SelectedItem = _geschichteFilterTag.Items.OfType<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, ab)) ?? _geschichteFilterTag.Items[0];
        _geschichteFilterSetzen = false;
        GeschichteListeZeigen();
        _inhalt.Content = _geschichteAnsicht;
        KnoepfeFreigeben();
    }

    /// <summary>Zwischen Zusammenhängen und Liste wechseln (Knoepfe „Aufgabe → Ort“ und „Zusammenhänge“).</summary>
    internal void GeschichteListeUmschalten(bool liste)
    {
        _geschichteListe = liste;
        _geschichteHervor = null;
        GeschichteSeiteOrtZeigen(null);
        GeschichteListeZeigen();
        GeschichteMitteZeigen();
    }

    private void GeschichteMitteZeigen()
    {
        if (_geschichteListe)
        {
            _geschichteMitte.Content = _geschichteListeSeite;
        }
        else
        {
            _geschichteMitte.Content = _zusammenhang;
            if (_geschichte is { } g && _projekt is not null)
            {
                _zusammenhang.Zeigen(g, _projekt.OrtName);
            }
        }
    }

    private Control GeschichteBauen()
    {
        _zusammenhang.OrtGewaehlt += GeschichteSeiteOrtZeigen;
        _zusammenhang.ZurListe += () => GeschichteListeUmschalten(true);
        var zuZusammenhaengen = Schrift.Knopf(Texte.GeschichteZuZusammenhaengen).Mit(Texte.GeschichteZuZusammenhaengenTipp);
        zuZusammenhaengen.Click += (_, _) => GeschichteListeUmschalten(false);
        _geschichteFilterOrt.Vorlese(Texte.GeschichteFilterOrt).Mit(Texte.GeschichteFilterTipp);
        _geschichteFilterTag.Vorlese(Texte.GeschichteFilterTag).Mit(Texte.GeschichteFilterTipp);
        _geschichteFilterPerson.Vorlese(Texte.GeschichteFilterPerson).Mit(Texte.GeschichteFilterTipp);
        _geschichteSuche.PlaceholderText = Texte.GeschichteSuchenPlatzhalter;
        _geschichteSuche.Vorlese(Texte.GeschichteSuchen).Mit(Texte.GeschichteSuchenTipp);
        foreach (var c in new[] { _geschichteFilterOrt, _geschichteFilterTag, _geschichteFilterPerson })
        {
            c.SelectionChanged += (_, _) => GeschichteFilterGeaendert();
        }
        _geschichteSuche.TextChanged += (_, _) => GeschichteFilterGeaendert();
        var filter = new WrapPanel
        {
            Children = { _geschichteFilterOrt, _geschichteFilterTag, _geschichteFilterPerson, _geschichteSuche },
            ItemSpacing = 8,
            LineSpacing = 8,
        };
        var kopf = GeschichteZeile(Texte.GeschichteSpalteAb, Texte.GeschichteSpalteAufgabe, Texte.GeschichteSpalteOrt, Texte.GeschichteSpalteAuftraggeber,
            Texte.GeschichteSpalteNach, true, false);
        var titelZeile = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        titelZeile.Children.Add(Schrift.Text(Texte.GeschichteListe, "Gross", FontWeight.Bold));
        Grid.SetColumn(zuZusammenhaengen, 1);
        titelZeile.Children.Add(zuZusammenhaengen);
        var inhalt = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                titelZeile,
                _geschichteZeile,
                filter,
                _geschichteTreffer,
                kopf,
                _geschichteZeilen,
            },
        };
        var karte = Schrift.Karte(inhalt);
        karte.Margin = new Thickness(12);
        _geschichteListeSeite = new ScrollViewer { Content = new Border { Child = karte, Padding = new Thickness(0, 0, Dialog.RandFuerLeiste, 0) } };

        // rechts: Spielgeschichte des angetippten Kastens (wie Bild 1)
        var rechtsKarte = Schrift.Karte(_geschichteSeiteOrt);
        rechtsKarte.Margin = new Thickness(0, 12, 12, 12);
        _geschichteSeiteRechts = new Border
        {
            Width = 340,
            IsVisible = false,
            Child = new ScrollViewer { Content = new Border { Child = rechtsKarte, Padding = new Thickness(0, 0, Dialog.RandFuerLeiste, 0) } },
        };
        var seite = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        seite.Children.Add(_geschichteMitte);
        Grid.SetColumn(_geschichteSeiteRechts, 1);
        seite.Children.Add(_geschichteSeiteRechts);
        return seite;
    }

    /// <summary>Spalten der Liste: Ab, Aufgabe, Ort, Auftraggeber, Erst nach.</summary>
    private static Border GeschichteZeile(string ab, string titel, string ort, string person, string nach, bool kopf, bool hervor)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("72,3*,2*,1.4*,2.4*"), ColumnSpacing = 12 };
        var spalten = new[] { ab, titel, ort, person, nach };
        for (var i = 0; i < spalten.Length; i++)
        {
            var t = Schrift.Text(spalten[i], "Klein", kopf ? FontWeight.Bold : FontWeight.Normal);
            t.TextWrapping = TextWrapping.Wrap;
            t.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(t, i);
            g.Children.Add(t);
        }
        var rahmen = new Border
        {
            Child = g,
            Padding = new Thickness(hervor ? 4 : 8, 6, 8, 6),
            BorderThickness = new Thickness(hervor ? 4 : 0, 0, 0, 1),
        };
        rahmen.Farbe(Border.BorderBrushProperty, "Pinsel.border");
        if (kopf)
        {
            rahmen.Farbe(Border.BackgroundProperty, "Pinsel.card_alt");
        }
        else if (hervor)
        {
            // gelb hervorgehoben: Streifen links in Gelb, Flaeche aus der Farbwelt (Kontrast wie jede Karte)
            rahmen.Farbe(Border.BackgroundProperty, "Pinsel.card_hi");
            rahmen.BorderBrush = null;
            rahmen.Farbe(Border.BorderBrushProperty, "Pinsel.yellow");
        }
        return rahmen;
    }

    private void GeschichteFilterFuellen()
    {
        if (_geschichte is not { } g || _projekt is null)
        {
            return;
        }
        var setzen = _geschichteFilterSetzen;
        _geschichteFilterSetzen = true;
        var ort = (_geschichteFilterOrt.SelectedItem as ComboBoxItem)?.Tag;
        var tag = (_geschichteFilterTag.SelectedItem as ComboBoxItem)?.Tag;
        var person = (_geschichteFilterPerson.SelectedItem as ComboBoxItem)?.Tag;
        static void Fuellen(ComboBox c, string alle, IEnumerable<(string Text, object Tag)> eintraege, object? vorher)
        {
            c.Items.Clear();
            c.Items.Add(new ComboBoxItem { Content = alle, Tag = null });
            foreach (var (text, tag) in eintraege)
            {
                c.Items.Add(new ComboBoxItem { Content = text, Tag = tag });
            }
            c.SelectedItem = c.Items.OfType<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, vorher)) ?? c.Items[0];
        }
        Fuellen(_geschichteFilterOrt, Texte.GeschichteAlleOrte,
            g.Zahlen().Keys.Select(o => (_projekt.OrtName(o), (object)o)).OrderBy(x => x.Item1, StringComparer.Ordinal), ort);
        Fuellen(_geschichteFilterTag, Texte.GeschichteAlleTage,
            g.Zahlen().Keys.Select(g.FreiAbTag).OfType<long>().Where(t => t > 1).Distinct().Order().Select(t => (Texte.GeschichteAbTag(t), (object)t)), tag);
        Fuellen(_geschichteFilterPerson, Texte.GeschichteAllePersonen, g.Personen().Select(p => (p, (object)p)), person);
        _geschichteZeile.Text = Texte.GeschichteListeZeile(g.Aufgaben.Count, g.Alle.Count(e => e.Sorte == Spielgeschichte.Art.Auftrag),
            g.Alle.Count(e => e.Sorte == Spielgeschichte.Art.Zwischenfall));
        _geschichteFilterSetzen = setzen;
    }

    private void GeschichteFilterGeaendert()
    {
        if (!_geschichteFilterSetzen)
        {
            GeschichteListeZeigen();
        }
    }

    private void GeschichteListeZeigen()
    {
        _geschichteZeilen.Children.Clear();
        if (_geschichte is not { } g || _projekt is null)
        {
            GeschichteListe = [];
            return;
        }
        var liste = g.Liste((_geschichteFilterOrt.SelectedItem as ComboBoxItem)?.Tag as string,
            (_geschichteFilterTag.SelectedItem as ComboBoxItem)?.Tag as long?,
            (_geschichteFilterPerson.SelectedItem as ComboBoxItem)?.Tag as string,
            _geschichteSuche.Text, _projekt.OrtName);
        var zeilen = new List<(string, bool)>();
        foreach (var e in liste)
        {
            var hervor = _geschichteHervor is not null && e.Ort == _geschichteHervor;
            _geschichteZeilen.Children.Add(GeschichteZeile(Texte.GeschichteTag(e.AbTag), e.Titel,
                e.Ort is null ? Texte.GeschichteOhneOrt : _projekt.OrtName(e.Ort), e.Auftraggeber, e.NachTitel ?? Texte.GeschichteOhneOrt, false, hervor));
            zeilen.Add((e.Id, hervor));
        }
        GeschichteListe = zeilen;
        _geschichteTreffer.Text = g.Zahlen().Count == 0 ? Texte.GeschichteKeineStadt
            : liste.Count == 0 ? Texte.GeschichteKeineTreffer : Texte.GeschichteTreffer(liste.Count);
    }
}
