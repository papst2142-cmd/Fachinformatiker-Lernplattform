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
/// Zeile beim gewaehlten Ort mit Aufgaben, kein Fenster. Die Ansicht „Zusammenhänge“ folgt nach Nicos OK zum Entwurf.
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

    /// <summary>Spielgeschichte zum aktuellen Stand (null ohne Projekt).</summary>
    internal Spielgeschichte? Geschichte => _geschichte;

    /// <summary>Ist der Reiter „Spielgeschichte“ offen?</summary>
    internal bool GeschichteOffen => _geschichteOffen;

    /// <summary>Zeilen der Liste (Start-Test): Kennung, hervorgehoben.</summary>
    internal IReadOnlyList<(string Id, bool Hervor)> GeschichteListe { get; private set; } = [];

    /// <summary>Ort der hervorgehobenen Zeilen (Start-Test).</summary>
    internal string? GeschichteHervor => _geschichteHervor;

    /// <summary>Karte rechts „Spielgeschichte an diesem Ort“ (Start-Test: sichtbar, Inhalt).</summary>
    internal Border? GeschichteOrtKarte => _geschichteOrtKarte;

    /// <summary>Texte der Karte rechts (Start-Test).</summary>
    internal IEnumerable<string> GeschichteOrtTexte => _geschichteOrt.Children.OfType<TextBlock>().Select(t => t.Text ?? "")
        .Concat(_geschichteOrt.Children.OfType<Grid>().SelectMany(g => g.Children.OfType<TextBlock>()).Select(t => t.Text ?? ""));

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
            _aussenAuswahl.Children.Add(Schrift.Text(Texte.GeschichteGelb(zahl), "Klein", FontWeight.Bold, "Pinsel.orange"));
        }
        _geschichteOrt.Children.Add(Schrift.Text(Texte.GeschichteAnOrt, "Mittel", FontWeight.Bold));
        _geschichteOrt.Children.Add(Schrift.Text(Texte.GeschichteUnterzeile(_projekt.OrtName(ort), gs.FreiAbTag(ort)), "Klein", farbe: "Pinsel.muted"));
        void Abschnitt(string titel, IReadOnlyList<Spielgeschichte.Eintrag> liste)
        {
            if (liste.Count == 0)
            {
                return;
            }
            _geschichteOrt.Children.Add(Schrift.Text(titel, gewicht: FontWeight.Bold));
            foreach (var e in liste)
            {
                var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
                var tag = Schrift.Text(Texte.GeschichteTag(e.AbTag), "Klein", FontWeight.Bold, "Pinsel.accent_text");
                tag.MinWidth = 52;
                var titelText = Schrift.Text(e.Titel, "Klein");
                titelText.TextWrapping = TextWrapping.Wrap;
                Grid.SetColumn(titelText, 1);
                zeile.Children.Add(tag);
                zeile.Children.Add(titelText);
                _geschichteOrt.Children.Add(zeile);
            }
        }
        Abschnitt(Texte.GeschichteAufgaben(zahl), gs.AnOrt(ort, Spielgeschichte.Art.Aufgabe));
        Abschnitt(Texte.GeschichteAuftraege(gs.AnOrt(ort, Spielgeschichte.Art.Auftrag).Count), gs.AnOrt(ort, Spielgeschichte.Art.Auftrag));
        Abschnitt(Texte.GeschichteZwischenfaelle(gs.AnOrt(ort, Spielgeschichte.Art.Zwischenfall).Count), gs.AnOrt(ort, Spielgeschichte.Art.Zwischenfall));
        var alle = Schrift.Knopf(Texte.GeschichteAlle).Mit(Texte.GeschichteAlleTipp);
        alle.Click += (_, _) => GeschichteZeigen(ort);
        _geschichteOrt.Children.Add(alle);
    }

    // ---- Reiter „Spielgeschichte“ ------------------------------------------------------------------

    /// <summary>
    /// Reiter „Spielgeschichte“ zeigen; mit <paramref name="ort"/> (Knopf „Alle in der Spielgeschichte“) ab dem Tag, an dem
    /// der Ort ins Spiel kommt, und seine Aufgaben hervorgehoben.
    /// </summary>
    public void GeschichteZeigen(string? ort = null)
    {
        if (_projekt is null)
        {
            return;
        }
        _geschichteOffen = true;
        _innen = false;
        _drei = false;
        _geschichteAnsicht ??= GeschichteBauen();
        _geschichte ??= _daten is null ? null : Spielgeschichte.Aus(_daten.Inhalt, _projekt.AussenStand);
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

    private Control GeschichteBauen()
    {
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
        var inhalt = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Schrift.Text(Texte.GeschichteListe, "Gross", FontWeight.Bold),
                _geschichteZeile,
                filter,
                _geschichteTreffer,
                kopf,
                _geschichteZeilen,
            },
        };
        var karte = Schrift.Karte(inhalt);
        karte.Margin = new Thickness(12);
        return new ScrollViewer { Content = new Border { Child = karte, Padding = new Thickness(0, 0, Dialog.RandFuerLeiste, 0) } };
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
