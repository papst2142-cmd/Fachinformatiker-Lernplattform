using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.55.0 (Weisung P12-282 Punkt 2): Knopf „Stadtteil-Namen“ (Taste B, nur „Stadt von oben“). An: ein Finger oder die
/// Maus zieht ein Rechteck (neuer Stadtteil mit Vorschlag „Stadtteil n“, das Namensfeld rechts bekommt den Fokus), Tippen
/// waehlt einen. Rechts die Liste aller Stadtteile, fuer den gewaehlten Name, Verschieben und Groesse je 1 Feld und Loeschen;
/// „Neuer Stadtteil“ legt 16 × 16 in der Mitte der Ansicht an. So geht alles auch mit Tastatur und Screenreader. Jede Aenderung
/// ist ein Schritt (Rueckgaengig), ein ungueltiger Stand wird mit Grund abgelehnt (rote Zeile und Melden, nie still).
/// </summary>
public sealed partial class Hauptansicht
{
    private Button _namenChip = null!;
    private TextBox? _namenFeld;
    private string? _namenFehler;

    internal Button NamenKnopf => _namenChip;

    /// <summary>Start-Test: Namensfeld des gewaehlten Stadtteils (null = keiner gewaehlt) und die rote Zeile.</summary>
    internal TextBox? NamenFeld => _namenFeld;

    internal string? NamenFehler => _namenFehler;

    /// <summary>Start-Test: Knopf rechts mit diesem Namen fuer den Screenreader (auch in Reihen), sonst null.</summary>
    internal Button? NamenKnopfMit(string name) =>
        _aussenAuswahl.Children.SelectMany(c => c is Panel reihe ? reihe.Children : [c]).OfType<Button>()
            .FirstOrDefault(k => AutomationProperties.GetName(k) == name);

    private void NamenKnopfBauen()
    {
        _namenChip = Chip(Texte.StadtteilNamen, Texte.StadtteilNamenTipp, NamenUmschalten);
        _werkzeugLeiste.Children.Insert(_werkzeugLeiste.Children.IndexOf(_ebenenHinweis), _namenChip);
        Karte.NamenGeaendert += () =>
        {
            ChipsZeigen();
            NamenChipZeigen();
            AussenAuswahlZeigen();
        };
        Karte.NameGezogen += (x1, z1, x2, z2) => NameAnlegen(x1, z1, x2, z2);
        Karte.NameGetippt += nummer =>
        {
            NameWaehlen(nummer);
        };
        NamenChipZeigen();
    }

    private void NamenChipZeigen()
    {
        ChipMarkieren(_namenChip, Karte.NamenAn);
        AutomationProperties.SetName(_namenChip, Karte.NamenAn ? Texte.Gewaehlt(Texte.StadtteilNamen) : Texte.StadtteilNamen);
        _namenChip.IsVisible = !_drei;
    }

    /// <summary>Stadtteil-Namen an oder aus (Knopf und Taste B).</summary>
    internal void NamenUmschalten()
    {
        if (_projekt is null || _innen || _geschichteOffen)
        {
            return;
        }
        if (_drei)
        {
            Melden(Texte.StadtteilNamenNurDraufsicht);
            return;
        }
        _namenFehler = null;
        Karte.NameGewaehlt = null;
        Karte.NamenAn = !Karte.NamenAn;
        if (Karte.NamenAn)
        {
            Melden(Texte.StadtteilNamenErklaerung);
        }
    }

    /// <summary>Neuer Stadtteil aus einem gezogenen Rechteck (auch Start-Test); null = angelegt, sonst der Grund.</summary>
    internal string? NameAnlegen(double x1, double z1, double x2, double z2)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return null;
        }
        var name = StadtteilNamen.Vorschlag(_projekt.AussenStand);
        var grund = _projekt.StadtteilNeu(name, x1, z1, x2, z2);
        if (grund is not null)
        {
            NamenFehlerZeigen(grund);
            return grund;
        }
        _namenFehler = null;
        Karte.NameGewaehlt = _projekt.StadtteilEintraege().Count - 1;
        AussenAuswahlZeigen();
        Melden(Texte.StadtteilAngelegt(name));
        if (_namenFeld is { } feld)
        {
            Dispatcher.UIThread.Post(() =>
            {
                feld.Focus();
                feld.SelectAll();
            });
        }
        return null;
    }

    /// <summary>Neuer Stadtteil 16 × 16 in der Mitte der Ansicht (Tastatur).</summary>
    internal string? NameInDerMitte()
    {
        var m = Karte.AnsichtMitte;
        var halb = Stadtteile.Groesse / 2;
        var (b, h) = Aussen.Groesse(_projekt!.AussenStand);
        var x1 = Math.Clamp(StadtteilNamen.Rund(m.X - halb), 0, Math.Max(0, b - Stadtteile.Groesse));
        var z1 = Math.Clamp(StadtteilNamen.Rund(m.Y - halb), 0, Math.Max(0, h - Stadtteile.Groesse));
        return NameAnlegen(x1, z1, Math.Min(b, x1 + Stadtteile.Groesse), Math.Min(h, z1 + Stadtteile.Groesse));
    }

    internal void NameWaehlen(int? nummer)
    {
        _namenFehler = null;
        Karte.NameGewaehlt = nummer;
        AussenAuswahlZeigen();
        if (nummer is { } n && _projekt?.StadtteilEintraege().ElementAtOrDefault(n) is { } b)
        {
            Melden(Texte.StadtteilGewaehlt(Zeile(b)));
        }
    }

    private static string Zeile(Stadtteile.Bereich b) => Texte.StadtteilZeile(b.Name, b.X1, b.Z1, b.X2, b.Z2);

    private void NamenFehlerZeigen(string grund)
    {
        _namenFehler = grund;
        AussenAuswahlZeigen();
        Melden(grund);
    }

    /// <summary>Gewaehlten Stadtteil umbenennen (auch Start-Test); null = gelungen oder unveraendert.</summary>
    internal string? NameUmbenennen(string name)
    {
        if (_projekt is null || Karte.NameGewaehlt is not { } n || _projekt.StadtteilEintraege().ElementAtOrDefault(n) is not { } b)
        {
            return Texte.StadtteilFehlt;
        }
        if (b.Name == name)
        {
            return null;
        }
        if (_projekt.StadtteilUmbenennen(n, name) is { } grund)
        {
            NamenFehlerZeigen(grund);
            return grund;
        }
        _namenFehler = null;
        AussenAuswahlZeigen();
        Melden(Texte.StadtteilUmbenannt + ": „" + b.Name + "“ → „" + name + "“");
        return null;
    }

    /// <summary>Gewaehlten Stadtteil verschieben (dx, dz) oder in der Groesse aendern (db, dt), je in Feldern.</summary>
    internal string? NameAendern(double dx, double dz, double db, double dt)
    {
        if (_projekt is null || Karte.NameGewaehlt is not { } n || _projekt.StadtteilEintraege().ElementAtOrDefault(n) is not { } b)
        {
            return Texte.StadtteilFehlt;
        }
        var verschoben = db == 0 && dt == 0;
        if (_projekt.StadtteilBereich(n, b.X1 + dx, b.Z1 + dz, b.X2 + dx + db, b.Z2 + dz + dt, verschoben) is { } grund)
        {
            NamenFehlerZeigen(grund);
            return grund;
        }
        _namenFehler = null;
        AussenAuswahlZeigen();
        if (_projekt.StadtteilEintraege().ElementAtOrDefault(n) is { } neu)
        {
            Melden((verschoben ? Texte.StadtteilVerschoben : Texte.StadtteilGroesse) + ": " + Zeile(neu));
        }
        return null;
    }

    internal void NameLoeschen()
    {
        if (_projekt is null || Karte.NameGewaehlt is not { } n)
        {
            return;
        }
        var name = _projekt.StadtteilEintraege().ElementAtOrDefault(n)?.Name ?? Texte.StadtteilKaputt;
        if (_projekt.StadtteilLoeschen(n) is { } grund)
        {
            NamenFehlerZeigen(grund);
            return;
        }
        _namenFehler = null;
        Karte.NameGewaehlt = null;
        AussenAuswahlZeigen();
        Melden(Texte.StadtteilGeloescht + ": „" + name + "“");
    }

    /// <summary>Tasten bei „Stadtteil-Namen“: Pfeile verschieben den gewaehlten, Entf loescht ihn, Esc beendet.</summary>
    private bool NamenTaste(Key taste)
    {
        if (!Karte.NamenAn)
        {
            return false;
        }
        switch (taste)
        {
            case Key.Escape:
                Karte.NamenAn = false;
                return true;
            case Key.Left or Key.Right or Key.Up or Key.Down when Karte.NameGewaehlt is not null && _projekt?.NurLesen == false:
                NameAendern(taste == Key.Left ? -1 : taste == Key.Right ? 1 : 0, taste == Key.Up ? -1 : taste == Key.Down ? 1 : 0, 0, 0);
                return true;
            case Key.Delete or Key.Back when Karte.NameGewaehlt is not null && _projekt?.NurLesen == false:
                NameLoeschen();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Karte rechts bei „Stadtteil-Namen“: Erklaerung, Liste, gewaehlter Stadtteil, rote Zeile.</summary>
    private bool NamenTeil()
    {
        if (!Karte.NamenAn || _projekt is null)
        {
            _namenFeld = null;
            return false;
        }
        var lesen = _projekt.NurLesen;
        var liste = _projekt.StadtteilEintraege();
        if (Karte.NameGewaehlt is { } g && g >= liste.Count)
        {
            Karte.NameGewaehlt = null;
        }
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.StadtteilNamen, "Mittel", FontWeight.Bold));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.StadtteilNamenErklaerung, "Klein"));
        // die rote Zeile gleich oben, damit sie ohne Bildlauf zu sehen ist
        if (_namenFehler is { } f)
        {
            _aussenAuswahl.Children.Add(Schrift.Text(f, "Klein", FontWeight.Bold, "Pinsel.red"));
        }
        var neu = Knopf(Texte.StadtteilNeu, Texte.StadtteilNeuTipp, () => NameInDerMitte(), betont: true).Vorlese(Texte.StadtteilNeu);
        neu.IsEnabled = !lesen;
        _aussenAuswahl.Children.Add(neu);
        _aussenAuswahl.Children.Add(Schrift.Text(liste.Count == 0 ? Texte.StadtteilNamenKeine : Texte.StadtteilAnzahl(liste.Count), "Klein", FontWeight.Bold));
        for (var i = 0; i < liste.Count; i++)
        {
            var nr = i;
            var text = liste[i] is { } b ? Zeile(b) : Texte.StadtteilKaputt;
            var k = Knopf(text, text, () => NameWaehlen(nr)).Vorlese(Karte.NameGewaehlt == i ? Texte.Gewaehlt(text) : text);
            k.HorizontalAlignment = HorizontalAlignment.Stretch;
            ChipMarkieren(k, Karte.NameGewaehlt == i);
            _aussenAuswahl.Children.Add(k);
        }
        _namenFeld = null;
        if (Karte.NameGewaehlt is { } n)
        {
            var b = liste[n];
            if (b is not null)
            {
                var feld = new TextBox
                {
                    Text = b.Name,
                    MaxLength = Stadtteile.NameHoechstens + 10,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    IsEnabled = !lesen,
                };
                feld.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        NameUmbenennen(feld.Text ?? "");
                        e.Handled = true;
                    }
                };
                _namenFeld = feld;
                var uebernehmen = Knopf(Texte.StadtteilNameUebernehmen, Texte.StadtteilNameTipp, () => NameUmbenennen(feld.Text ?? "")).Vorlese(Texte.StadtteilNameUebernehmen);
                uebernehmen.IsEnabled = !lesen;
                _aussenAuswahl.Children.Add(Schrift.Text(Texte.StadtteilNameFeld, "Klein").Fuer(feld));
                _aussenAuswahl.Children.Add(feld);
                _aussenAuswahl.Children.Add(uebernehmen);
                Button K(string t, Action a)
                {
                    var k = Knopf(t, t, a).Vorlese(t);
                    k.IsEnabled = !lesen;
                    return k;
                }
                _aussenAuswahl.Children.Add(Schrift.Text(Texte.StadtteilSchieben, "Klein"));
                _aussenAuswahl.Children.Add(new WrapPanel
                {
                    Children =
                    {
                        K(Texte.StadtteilLinks, () => NameAendern(-1, 0, 0, 0)), K(Texte.StadtteilRechts, () => NameAendern(1, 0, 0, 0)),
                        K(Texte.StadtteilOben, () => NameAendern(0, -1, 0, 0)), K(Texte.StadtteilUnten, () => NameAendern(0, 1, 0, 0)),
                    },
                });
                _aussenAuswahl.Children.Add(Schrift.Text(Texte.StadtteilGroesseText, "Klein"));
                _aussenAuswahl.Children.Add(new WrapPanel
                {
                    Children =
                    {
                        K(Texte.StadtteilBreiter, () => NameAendern(0, 0, 1, 0)), K(Texte.StadtteilSchmaler, () => NameAendern(0, 0, -1, 0)),
                        K(Texte.StadtteilHoeher, () => NameAendern(0, 0, 0, 1)), K(Texte.StadtteilFlacher, () => NameAendern(0, 0, 0, -1)),
                    },
                });
            }
            var loeschen = Knopf(Texte.StadtteilLoeschen, Texte.StadtteilLoeschen, NameLoeschen).Vorlese(Texte.StadtteilLoeschen);
            loeschen.IsEnabled = !lesen;
            _aussenAuswahl.Children.Add(loeschen);
        }
        _aussenAuswahl.Children.Add(Knopf(Texte.StadtteilFertig, Texte.StadtteilFertigTipp, () => Karte.NamenAn = false).Vorlese(Texte.StadtteilFertig));
        return true;
    }
}
