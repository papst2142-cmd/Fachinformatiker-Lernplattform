using Avalonia;
using Avalonia.Controls;
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
/// Welle S 0.64.0 (Weisung P12-381, Nico 2c, Bilder 03/04 P12-244 und 07 P12-245): Karte „Raum ändern“ in der Ansicht „Räume
/// des Spiels“. Schalter „Ändern“: danach laesst sich der Raum wie jeder Innenraum bearbeiten (Moebel im Grundriss
/// verschieben, drehen, setzen, loeschen) und dazu „Wand ziehen“ (Raum oder Flur), „Tür setzen“ und „Raumgröße“ (ganzes Haus).
/// Ein geaenderter Raum heisst in der Liste „geändert“; „Zurück auf den Stand des Spiels“ nimmt alles in einem Schritt zurueck.
/// Darunter die Fundliste des Raums (rote Funde sperren den Export, ein Tipp zeigt die Stelle mit rotem Ring).
/// </summary>
public sealed partial class Hauptansicht
{
    private readonly StackPanel _spielRaumAendern = new() { Spacing = 8 };
    private readonly StackPanel _spielRaumFunde = new() { Spacing = 6 };
    private readonly Dictionary<string, Border> _spielRaumMarken = [];
    private string _spielRaumModus = Texte.SpielRaumWandZiehen;
    private string? _spielRaumBereich;
    private long _spielRaumPruefStand;
    private TaskCompletionSource _spielRaumGeprueft = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Schalter „Ändern“ (Start-Test).</summary>
    internal ToggleSwitch? SpielRaumSchalter { get; private set; }

    /// <summary>Knopf „Zurück auf den Stand des Spiels“ (Start-Test; null = Raum unveraendert).</summary>
    internal Button? SpielRaumZurueckKnopf { get; private set; }

    /// <summary>Rote Funde des offenen Raums nach der letzten Pruefung (Start-Test).</summary>
    internal IReadOnlyList<StadtbauFund> SpielRaumRot { get; private set; } = [];

    /// <summary>Fertig, sobald die Fundliste des offenen Raums geprueft ist.</summary>
    internal Task SpielRaumGeprueft => _spielRaumGeprueft.Task;

    /// <summary>Karten rechts: „Raum ändern“ und die Fundliste.</summary>
    private Control[] SpielRaumAendernKarten() =>
    [
        Schrift.Karte(new StackPanel { Spacing = 8, Children = { Schrift.Text(Texte.SpielRaumAendernTitel, "Mittel", FontWeight.Bold), _spielRaumAendern } }),
        Schrift.Karte(_spielRaumFunde),
    ];

    /// <summary>Karte „Raum ändern“ und Fundliste fuer den offenen Raum neu aufbauen, dazu die Marken „geändert“ links.</summary>
    private void SpielRaumAendernZeigen()
    {
        _spielRaumAendern.Children.Clear();
        SpielRaumZurueckKnopf = null;
        SpielRaumMarkenZeigen();
        if (_projekt is null || _spielRaumOffen is not { } schluessel)
        {
            return;
        }
        var an = _projekt.SpielRaumInBearbeitung(schluessel);
        var darf = _projekt.SpielRaeumeAenderbar && !_projekt.NurLesen;
        var schalter = new ToggleSwitch
        {
            Content = Schrift.Text(Texte.SpielRaumAendernSchalter + (an ? " (an)" : " (aus)"), "Klein"),
            IsChecked = an, IsEnabled = darf && !(an && _projekt.GeaenderteSpielRaeume.Contains(schluessel)),
            OnContent = null, OffContent = null,
        }.Vorlese(Texte.SpielRaumAendernSchalter).Mit(Texte.SpielRaumAendernTipp);
        schalter.IsCheckedChanged += (_, _) => SpielRaumSchalten(schalter.IsChecked == true);
        SpielRaumSchalter = schalter;
        _spielRaumAendern.Children.Add(schalter);
        if (!darf)
        {
            _spielRaumAendern.Children.Add(Hinweis(_projekt.NurLesen ? Texte.NurLesen : Texte.SpielRaumAendernNurStadt1));
        }
        if (!an)
        {
            _spielRaumAendern.Children.Add(Hinweis(Texte.SpielRaumAendernAus));
            SpielRaumFundeZeigen(null);
            return;
        }
        _spielRaumAendern.Children.Add(Hinweis(Texte.SpielRaumAendernErklaerung));
        var modi = new WrapPanel();
        foreach (var (text, tipp) in new[] { (Texte.SpielRaumWandZiehen, Texte.SpielRaumWandZiehenTipp), (Texte.SpielRaumTuerSetzen, Texte.SpielRaumTuerSetzenTipp), (Texte.SpielRaumGroesseKnopf, Texte.SpielRaumGroesseTipp) })
        {
            var k = Schrift.Knopf(text, betont: text == _spielRaumModus).Vorlese(text).Mit(tipp);
            k.Margin = new Thickness(0, 0, 6, 6);
            var modus = text;
            k.Click += (_, _) =>
            {
                _spielRaumModus = modus;
                SpielRaumAendernZeigen();
                Melden(modus);
            };
            modi.Children.Add(k);
        }
        _spielRaumAendern.Children.Add(modi);
        _spielRaumAendern.Children.Add(SpielRaumFelder(schluessel));
        if (_projekt.GeaenderteSpielRaeume.Contains(schluessel))
        {
            var zurueck = Schrift.Knopf(Texte.SpielRaumZurueck).Vorlese(Texte.SpielRaumZurueck).Mit(Texte.SpielRaumZurueckTipp);
            zurueck.HorizontalAlignment = HorizontalAlignment.Stretch;
            zurueck.Click += (_, _) =>
            {
                if (_projekt.SpielRaumZuruecksetzen(schluessel, Texte.SpielRaumZurueck))
                {
                    Melden(Texte.SpielRaumZurueck);
                }
            };
            SpielRaumZurueckKnopf = zurueck;
            _spielRaumAendern.Children.Add(zurueck);
            var marke = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 4), BorderThickness = new Thickness(2), Child = Schrift.Text(Texte.SpielRaumGeaendert, "Klein", FontWeight.Bold) }
                .Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.orange");
            _spielRaumAendern.Children.Add(marke);
        }
        else
        {
            _spielRaumAendern.Children.Add(Hinweis(Texte.SpielRaumUnveraendert));
        }
        SpielRaumPruefenStarten();
    }

    private void SpielRaumSchalten(bool an)
    {
        if (_projekt is null || _spielRaumOffen is not { } schluessel)
        {
            return;
        }
        if (an && !_projekt.SpielRaumBearbeiten(schluessel))
        {
            Melden(Texte.SpielRaumAendernNurStadt1);
        }
        else if (!an && !_projekt.GeaenderteSpielRaeume.Contains(schluessel))
        {
            // aus: nur, solange nichts geaendert ist (sonst erst „Zurück auf den Stand des Spiels“)
            _projekt.SpielRaumBeenden(schluessel);
        }
        SpielRaumFlaeche.Waehlen(null);
        SpielRaumFlaeche.Zeigen(_projekt, schluessel);
        SpielRaumFlaeche.InvalidateVisual();
        SpielRaumAendernZeigen();
        Melden(Texte.SpielRaumGeoeffnet(_projekt.SpielRaumName(schluessel), _projekt.SpielRaumInBearbeitung(schluessel)));
    }

    /// <summary>Felder des gewaehlten Modus (Wand ziehen: Raum oder Flur; Tür setzen: Raum; Raumgröße: ganzes Haus).</summary>
    private Control SpielRaumFelder(string schluessel)
    {
        var p = _projekt!;
        var g = p.Grundriss(schluessel);
        var felder = new StackPanel { Spacing = 6 };
        if (_spielRaumModus == Texte.SpielRaumGroesseKnopf)
        {
            var b = Feld(Py.F(g["breite"]));
            var t = Feld(Py.F(g["hoehe"]));
            felder.Children.Add(Zeilen((Texte.SpielRaumHausBreite, b), (Texte.SpielRaumHausTiefe, t)));
            felder.Children.Add(SpielRaumUebernehmen(() => Zahl(b, out var nb) & Zahl(t, out var nt)
                ? () => p.Aendern(schluessel, Texte.SpielRaumGroesseKnopf, gg => SpielRaumBearbeitung.Haus(gg, nb, nt)) : null));
            return felder;
        }
        var wahl = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch }.Vorlese(Texte.Raum);
        if (_spielRaumModus == Texte.SpielRaumWandZiehen)
        {
            wahl.Items.Add(new ComboBoxItem { Content = SpielDaten.Get(SpielDaten.Objekt(g["flur"]), "name") as string ?? "Flur", Tag = "flur" });
        }
        foreach (var r in SpielDaten.Liste(g["raeume"]).OfType<PyObjekt>())
        {
            wahl.Items.Add(new ComboBoxItem { Content = SpielDaten.Get(r, "name") as string ?? Py.Str(r["id"]), Tag = Py.Str(r["id"]) });
        }
        var werte = new StackPanel { Spacing = 6 };
        void Zeigen(string bereich)
        {
            werte.Children.Clear();
            var gg = p.Grundriss(schluessel);
            if (bereich == "flur")
            {
                var flur = SpielDaten.Objekt(gg["flur"]);
                var y = Feld(Py.F(flur["y"]));
                var h = Feld(Py.F(flur["h"]));
                werte.Children.Add(Zeilen((Texte.SpielRaumFlurOben, y), (Texte.SpielRaumFlurBreite, h)));
                werte.Children.Add(SpielRaumUebernehmen(() => Zahl(y, out var ny) & Zahl(h, out var nh)
                    ? () => p.Aendern(schluessel, Texte.SpielRaumWandZiehen, g2 => SpielRaumBearbeitung.Flur(g2, ny, nh)) : null));
                return;
            }
            if (_spielRaumModus == Texte.SpielRaumTuerSetzen)
            {
                var t = SpielDaten.Get(Bearbeitung.Bereich(gg, bereich), "tuer") as PyObjekt;
                var von = Feld(t is null || !Py.Hat(t, "von") ? 0 : Py.F(t["von"]));
                var bis = Feld(t is null || !Py.Hat(t, "bis") ? 0 : Py.F(t["bis"]));
                werte.Children.Add(Zeilen((Texte.Tuer + " (von)", von), (Texte.Tuer + " (bis)", bis)));
                werte.Children.Add(SpielRaumUebernehmen(() => Zahl(von, out var nv) & Zahl(bis, out var nb)
                    ? () => p.Aendern(schluessel, Texte.SpielRaumTuerSetzen, g2 => Bearbeitung.Tuer(g2, bereich, nv, nb)) : null));
                return;
            }
            var (rx, ry, rw, rh) = Bearbeitung.Flaeche(gg, bereich);
            var fx = Feld(rx);
            var fy = Feld(ry);
            var fw = Feld(rw);
            var fh = Feld(rh);
            werte.Children.Add(Zeilen((Texte.X, fx), (Texte.Y, fy), (Texte.Breite, fw), (Texte.Tiefe, fh)));
            werte.Children.Add(SpielRaumUebernehmen(() => Zahl(fx, out var nx) & Zahl(fy, out var ny) & Zahl(fw, out var nw) & Zahl(fh, out var nh)
                ? () => p.Aendern(schluessel, Texte.SpielRaumWandZiehen, g2 => Bearbeitung.Raum(g2, bereich, nx, ny, nw, nh)) : null));
        }
        wahl.SelectionChanged += (_, _) =>
        {
            if (wahl.SelectedItem is ComboBoxItem { Tag: string b })
            {
                _spielRaumBereich = b;
                Zeigen(b);
            }
        };
        felder.Children.Add(Zeilen((Texte.Raum, wahl)));
        felder.Children.Add(werte);
        // die Wahl bleibt nach „Übernehmen“ stehen (die Karte baut sich nach jeder Aenderung neu)
        var vorher = wahl.Items.OfType<ComboBoxItem>().ToList().FindIndex(i => (string)i.Tag! == _spielRaumBereich);
        wahl.SelectedIndex = Math.Max(0, vorher);
        return felder;
    }

    /// <summary>Knopf „Übernehmen“: <paramref name="lesen"/> liefert die Aenderung (null = keine Zahl).</summary>
    private Button SpielRaumUebernehmen(Func<Action?> lesen)
    {
        var k = Schrift.Knopf(Texte.SpielRaumUebernehmen, betont: true).Vorlese(Texte.SpielRaumUebernehmen);
        k.Click += (_, _) =>
        {
            if (lesen() is { } aendern)
            {
                aendern();
                Melden(Texte.SpielRaumGeaendertMeldung(_spielRaumModus));
            }
            else
            {
                Melden(Texte.SpielRaumNurZahlen);
            }
        };
        return k;
    }

    /// <summary>Marke „geändert“ neben jedem geaenderten Raum in der Liste links.</summary>
    private void SpielRaumMarkenZeigen()
    {
        if (_projekt is null)
        {
            return;
        }
        var geaendert = _projekt.GeaenderteSpielRaeume;
        foreach (var (s, k) in _spielRaumKnoepfe)
        {
            if (k.Content is not TextBlock name)
            {
                continue;
            }
            var text = _projekt.SpielRaumName(s) + (geaendert.Contains(s) ? "  · " + Texte.SpielRaumGeaendert : "");
            if (name.Text != text)
            {
                name.Text = text;
            }
        }
    }

    /// <summary>Fundliste des offenen Raums im Hintergrund pruefen (gleicher Weg wie die Export-Sperre).</summary>
    private void SpielRaumPruefenStarten()
    {
        if (_projekt is null || _spielRaumOffen is not { } schluessel)
        {
            return;
        }
        var stand = ++_spielRaumPruefStand;
        if (_spielRaumGeprueft.Task.IsCompleted)
        {
            _spielRaumGeprueft = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        if (!_projekt.GeaenderteSpielRaeume.Contains(schluessel))
        {
            SpielRaumFundeZeigen([]);
            return;
        }
        var auftrag = InnenFunde.Vorbereiten(_projekt, [schluessel]);
        SpielRaumFundeZeigen(null, pruefen: true);
        _ = Hintergrund(Task.Run(() => InnenFunde.Rechnen(auftrag)[0].Funde)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            if (stand != _spielRaumPruefStand || schluessel != _spielRaumOffen)
            {
                return;
            }
            if (t.IsFaulted)
            {
                _ordner.FehlerEinmal("Räume des Spiels prüfen", t.Exception);
                SpielRaumFundeZeigen([new StadtbauFund(Texte.SpielRaumFehlt, "", true)]);
                return;
            }
            SpielRaumFundeZeigen(t.Result);
        }), TaskScheduler.Default);
    }

    /// <summary>Fundliste zeigen (null = Raum nicht in Bearbeitung).</summary>
    private void SpielRaumFundeZeigen(List<StadtbauFund>? funde, bool pruefen = false)
    {
        _spielRaumFunde.Children.Clear();
        var rot = funde?.Where(f => f.Rot).ToList() ?? [];
        SpielRaumFlaeche.Funde = rot;
        SpielRaumFlaeche.InvalidateVisual();
        if (pruefen)
        {
            _spielRaumFunde.Children.Add(Schrift.Text(Texte.SpielRaumFundlistePrueft, "Mittel", FontWeight.Bold));
            return;
        }
        SpielRaumRot = rot;
        _spielRaumFunde.Children.Add(Schrift.Text(rot.Count > 0 ? Texte.SpielRaumFundliste(rot.Count) : Texte.SpielRaumFundlisteKeine, "Mittel", FontWeight.Bold));
        foreach (var f in rot)
        {
            var punkt = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Margin = new Thickness(0, 4, 6, 0), VerticalAlignment = VerticalAlignment.Top }
                .Farbe(Border.BackgroundProperty, "Pinsel.red");
            var text = Schrift.Text(f.Text, "Klein");
            text.TextWrapping = TextWrapping.Wrap;
            var zeile = new DockPanel { Children = { punkt, text } };
            DockPanel.SetDock(punkt, Dock.Left);
            var flaeche = new Klickflaeche { Child = zeile, Background = Brushes.Transparent }.Vorlese(f.Text);
            if (f.X is { } x && f.Y is { } y)
            {
                flaeche.Klick = () => SpielRaumFlaeche.Hinfahren(x, y);
            }
            _spielRaumFunde.Children.Add(flaeche);
        }
        if (rot.Count > 0)
        {
            _spielRaumFunde.Children.Add(Hinweis(Texte.SpielRaumFundlisteErklaerung));
            var sperre = Schrift.Knopf(Texte.SpielRaumExportGesperrt(rot.Count)).Vorlese(Texte.SpielRaumExportGesperrt(rot.Count));
            sperre.IsEnabled = false;
            sperre.HorizontalAlignment = HorizontalAlignment.Stretch;
            _spielRaumFunde.Children.Add(sperre);
        }
        else if (funde is not null && _projekt?.GeaenderteSpielRaeume.Contains(_spielRaumOffen ?? "") == true)
        {
            _spielRaumFunde.Children.Add(Hinweis(Texte.SpielRaumExportFrei));
        }
        _spielRaumGeprueft.TrySetResult();
    }

    /// <summary>Nach jeder Aenderung am Projekt: offene Ansicht „Räume des Spiels“ neu zeichnen und pruefen.</summary>
    private void SpielRaumProjektGeaendert()
    {
        if (!ZeigtSpielRaeume || _projekt is null || _spielRaumOffen is not { } schluessel)
        {
            return;
        }
        if (_projekt.LetzterSchrittSpielRaum is { } letzter && letzter != schluessel && SpielRaeume.Ist(letzter))
        {
            SpielRaumOeffnen(letzter);
            return;
        }
        SpielRaumFlaeche.InvalidateVisual();
        SpielRaumMasseZeigen(schluessel);
        SpielRaumAendernZeigen();
        _spielRaum3DGebaut = null;
        SpielRaum3DAktualisieren();
    }
}
