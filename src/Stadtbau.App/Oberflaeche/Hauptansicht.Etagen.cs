using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle W5 „Etagen“ (Weisung P12-73, Plan P12-23b Abschnitte 1 und 2): Etagenliste im Reiter Innenraeume (links ueber dem
/// Katalog) und Ziel/Ankunft eines gewaehlten Stuecks „Treppe“ oder „Fahrstuhl“ rechts unter „Auswahl“. Die Liste ist ein
/// eigener Baustein (<see cref="EtagenBaustein"/>), damit Welle Q sie ohne Umbau an eine andere Stelle setzen kann.
/// </summary>
public sealed partial class Hauptansicht
{
    private readonly StackPanel _etagenTafel = new() { Spacing = 6 };

    /// <summary>Knoepfe der Etagen (Erdgeschoss zuerst) beim letzten Zeigen (Start-Test).</summary>
    internal List<Button> EtagenKnoepfe { get; } = [];

    /// <summary>Knopf „Etage hinzufügen“ (Start-Test).</summary>
    internal Button? EtageHinzuKnopf { get; private set; }

    /// <summary>Auswahl „Ziel“ einer Treppe bzw. die Haken eines Fahrstuhls beim letzten Zeigen (Start-Test).</summary>
    internal ComboBox? ZielWahl { get; private set; }

    internal List<CheckBox> FahrstuhlHaken { get; } = [];

    /// <summary>Der Baustein „Etagen“: Titel mit Tipp und die Liste.</summary>
    private Control EtagenBaustein() => new StackPanel
    {
        Spacing = 6,
        Children = { Gruppe(Schrift.Text(Texte.Etagen, "Mittel", FontWeight.Bold), Texte.TippEtagen), _etagenTafel },
    };

    /// <summary>Etagenliste zum offenen Innenraum neu aufbauen.</summary>
    private void EtagenZeigen()
    {
        _etagenTafel.Children.Clear();
        EtagenKnoepfe.Clear();
        EtageHinzuKnopf = null;
        if (_projekt is null || _ort is null || !_projekt.GibtInnenort(_ort))
        {
            return;
        }
        var (basis, etage) = Etagen.Teilen(_ort);
        var eg = _projekt.Grundriss(basis);
        var liste = new WrapPanel();
        for (var k = 0; k <= Etagen.Anzahl(eg); k++)
        {
            if (!Etagen.Gibt(eg, k))
            {
                continue;
            }
            var name = Etagen.Name(eg, k);
            var schluessel = Etagen.Schluessel(basis, k);
            var knopf = Schrift.Knopf(name, betont: k == etage).Vorlese(k == etage ? name + ", " + Texte.SetzenGewaehlt : name);
            knopf.Margin = new Thickness(0, 0, 4, 4);
            knopf.Click += (_, _) =>
            {
                if (_ort != schluessel)
                {
                    OrtOeffnen(schluessel);
                    Melden(Texte.EtageGezeigt(name));
                }
            };
            EtagenKnoepfe.Add(knopf);
            liste.Children.Add(knopf);
        }
        _etagenTafel.Children.Add(liste);
        var hinzu = Schrift.Knopf(Texte.EtageHinzufuegen).Vorlese(Texte.EtageHinzufuegen).Mit(Texte.EtageHinzufuegenTipp);
        hinzu.Click += (_, _) => EtageHinzufuegen();
        EtageHinzuKnopf = hinzu;
        var reihe = new WrapPanel { Children = { hinzu } };
        if (etage > 0)
        {
            var weg = Schrift.Gefahrknopf(Texte.EtageLoeschen).Vorlese(Texte.EtageLoeschen).Mit(Texte.EtageLoeschenTipp);
            weg.Margin = new Thickness(4, 0, 0, 0);
            weg.Click += async (_, _) => await EtageLoeschenAsync();
            reihe.Children.Add(weg);
        }
        _etagenTafel.Children.Add(reihe);
        // S 0.19.0 (Nico 3a): passt der Innenraum nicht mehr zum Entwurf, gelber Hinweis mit „Vorschlag neu“
        VorschlagNeuKnopf = null;
        if (_projekt.HatEntwurf(basis) && _projekt.EtagenEntwurfFunde(basis) is { Count: > 0 } abweichung)
        {
            var hinweis = Schrift.Text(string.Join(" ", abweichung.Select(f => f.Fund.Text)), "Klein", farbe: abweichung.Any(f => f.Fund.Rot) ? "Pinsel.red" : "Pinsel.orange");
            hinweis.TextWrapping = TextWrapping.Wrap;
            var neu = Schrift.Knopf(Texte.EtagenVorschlagNeu).Vorlese(Texte.EtagenVorschlagNeu).Mit(Texte.EtagenVorschlagNeuKnopfTipp);
            neu.Click += async (_, _) => await EtagenVorschlagNeuAsync();
            VorschlagNeuKnopf = neu;
            _etagenTafel.Children.Add(hinweis);
            _etagenTafel.Children.Add(neu);
        }
        if (etage > 0)
        {
            var feld = new TextBox { Text = Etagen.Name(eg, etage), HorizontalAlignment = HorizontalAlignment.Stretch, MaxLength = 40 };
            var umbenennen = Schrift.Knopf(Texte.EtageUmbenennen).Vorlese(Texte.EtageUmbenennen).Mit(Texte.EtageUmbenennenTipp);
            umbenennen.Click += (_, _) =>
            {
                if (_projekt is null || _ort is null)
                {
                    return;
                }
                _projekt.EtagenAendern(_ort, Texte.EtageUmbenennen, (g, k) => EtagenBearbeitung.Umbenennen(g, k, feld.Text ?? ""));
                Melden(Texte.EtageUmbenennen + ": " + Etagen.Name(_projekt.Grundriss(basis), etage));
            };
            _etagenTafel.Children.Add(Zeilen((Texte.EtageName, feld)));
            _etagenTafel.Children.Add(umbenennen);
        }
        foreach (var k in _etagenTafel.GetLogicalDescendants().OfType<Control>().Where(c => c is TextBox || (c is Button b && !EtagenKnoepfe.Contains(b))))
        {
            k.IsEnabled = !_projekt.NurLesen;
        }
    }

    /// <summary>Knopf „Vorschlag neu“ beim letzten Zeigen (null = keiner, Start-Test).</summary>
    internal Button? VorschlagNeuKnopf { get; private set; }

    /// <summary>„Vorschlag neu“ (Nico 3a): mit Rueckfrage, dann Etagen und Treppenhaus neu aus dem Entwurf (ein Schritt).</summary>
    private async Task EtagenVorschlagNeuAsync(bool ohneFrage = false)
    {
        if (_projekt is null || _ort is null || _projekt.NurLesen)
        {
            return;
        }
        if (!ohneFrage && await Dialog.Zeigen(Texte.EtagenVorschlagNeuTitel, Texte.EtagenVorschlagNeuFrage, Texte.Abbrechen, Texte.EtagenVorschlagNeu) != 1)
        {
            return;
        }
        var basis = Etagen.Teilen(_ort).Ort;
        // erst ins Erdgeschoss (die gezeigte Etage gibt es danach vielleicht nicht mehr)
        OrtOeffnen(basis);
        var n = _projekt.EtagenVorschlagNeu(basis, Texte.EtagenVorschlagNeu);
        if (n >= 0)
        {
            OrtOeffnen(basis);
            Melden(Texte.EtagenVorschlagGemacht(n));
        }
    }

    /// <summary>Start-Test: „Vorschlag neu“ ohne Rueckfrage.</summary>
    internal Task EtagenVorschlagNeuFuerTests() => EtagenVorschlagNeuAsync(ohneFrage: true);

    /// <summary>„Etage hinzufügen“: neue Etage oben (hoechstens 4, sonst Hinweis in einfachen Worten, Nico 4a), dann zeigen.</summary>
    private void EtageHinzufuegen()
    {
        if (_projekt is null || _ort is null || _projekt.NurLesen)
        {
            return;
        }
        var basis = Etagen.Teilen(_ort).Ort;
        if (Etagen.Anzahl(_projekt.Grundriss(basis)) >= Etagen.Hoechstens)
        {
            Melden(Texte.EtageHoechstens);
            _ = Dialog.Zeigen(Texte.Etagen, Texte.EtageHoechstens, Texte.Ok);
            return;
        }
        var neu = _projekt.EtagenAendern(basis, Texte.EtageHinzufuegen, (g, _) => EtagenBearbeitung.Hinzufuegen(g));
        if (neu > 0)
        {
            var schluessel = Etagen.Schluessel(basis, neu);
            OrtOeffnen(schluessel);
            Melden(Texte.EtageAngelegt(Etagen.Name(_projekt.Grundriss(basis), neu)));
        }
    }

    /// <summary>„Etage löschen“: nur ohne Treppe/Fahrstuhl auf der Etage, mit Rueckfrage.</summary>
    private async Task EtageLoeschenAsync()
    {
        if (_projekt is null || _ort is null || _projekt.NurLesen)
        {
            return;
        }
        var (basis, etage) = Etagen.Teilen(_ort);
        var eg = _projekt.Grundriss(basis);
        if (etage == 0)
        {
            Melden(Texte.EgNichtLoeschen);
            return;
        }
        if (EtagenBearbeitung.HatUebergang(eg, etage))
        {
            Melden(Texte.EtageLoeschenNicht);
            await Dialog.Zeigen(Texte.EtageLoeschenTitel, Texte.EtageLoeschenNicht, Texte.Ok);
            return;
        }
        var name = Etagen.Name(eg, etage);
        if (await Dialog.Zeigen(Texte.EtageLoeschenTitel, Texte.EtageLoeschenFrage(name), Texte.Abbrechen, Texte.EtageLoeschen) != 1)
        {
            return;
        }
        // erst ins Erdgeschoss, dann loeschen (die gezeigte Etage gibt es danach nicht mehr)
        OrtOeffnen(basis);
        if (_projekt.EtagenAendern(basis, Texte.EtageLoeschen, (g, _) => EtagenBearbeitung.Loeschen(g, etage)))
        {
            Melden(Texte.EtageLoeschen + ": " + name);
        }
    }

    /// <summary>Rechts unter „Auswahl“: Ziel einer Treppe (Auswahl) oder Halte eines Fahrstuhls (Haken), Blick der Ankunft.</summary>
    private void EtagenAuswahl(PyObjekt d, string ort, Stelle stelle)
    {
        ZielWahl = null;
        FahrstuhlHaken.Clear();
        var typ = Py.Str(d["typ"]);
        if (_projekt is null || !Etagen.IstStueck(typ))
        {
            return;
        }
        var (basis, etage) = Etagen.Teilen(ort);
        var eg = _projekt.Grundriss(basis);
        var moeglich = EtagenBearbeitung.MoeglicheZiele(eg, etage, d);
        var jetzt = EtagenBearbeitung.Ziele(eg, etage, d);
        if (moeglich.Count == 0)
        {
            _auswahl.Children.Add(Schrift.Text(Texte.ZielTreppeNurNachbar, "Klein", farbe: "Pinsel.muted"));
            return;
        }
        if (typ == Etagen.Treppe)
        {
            var wahl = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch }.Mit(Texte.ZielTipp);
            wahl.Items.Add(new ComboBoxItem { Content = Texte.ZielKeins, Tag = -1 });
            foreach (var k in moeglich)
            {
                wahl.Items.Add(new ComboBoxItem { Content = Etagen.Name(eg, k), Tag = k });
            }
            wahl.SelectedItem = wahl.Items.OfType<ComboBoxItem>().FirstOrDefault(i => jetzt.Contains((int)i.Tag!)) ?? wahl.Items[0];
            wahl.SelectionChanged += (_, _) =>
            {
                if (wahl.SelectedItem is ComboBoxItem { Tag: int k } && k >= 0 && !jetzt.Contains(k))
                {
                    ZielSetzen(ort, stelle, k);
                }
            };
            ZielWahl = wahl;
            _auswahl.Children.Add(Zeilen((Texte.Ziel, wahl)));
        }
        else
        {
            _auswahl.Children.Add(Schrift.Text(Texte.FahrstuhlZiele, "Klein").Mit(Texte.FahrstuhlZieleTipp));
            var haken = new WrapPanel();
            foreach (var k in moeglich)
            {
                var name = Etagen.Name(eg, k);
                var h = new CheckBox { Content = Schrift.Text(name, "Klein"), IsChecked = jetzt.Contains(k), Margin = new Thickness(0, 0, 8, 0) }.Vorlese(Texte.FahrstuhlZiele + " " + name);
                h.IsCheckedChanged += (_, _) =>
                {
                    if ((h.IsChecked == true) != jetzt.Contains(k))
                    {
                        ZielSetzen(ort, stelle, k);
                    }
                };
                FahrstuhlHaken.Add(h);
                haken.Children.Add(h);
            }
            _auswahl.Children.Add(haken);
        }
        if (jetzt.Count > 0 && Get(d, Etagen.FeldLink) is string id)
        {
            var blick = Schrift.Knopf(Texte.BlickDrehen).Vorlese(Texte.BlickDrehen).Mit(Texte.BlickDrehenTipp);
            blick.Click += (_, _) =>
            {
                _projekt.EtagenAendern(ort, Texte.BlickDrehen, (g, k) => EtagenBearbeitung.BlickDrehen(g, id, k));
                Melden(Texte.BlickDrehen);
            };
            _auswahl.Children.Add(blick);
            _auswahl.Children.Add(Schrift.Text(Texte.AnkunftHinweis, "Klein", farbe: "Pinsel.muted"));
        }
    }

    /// <summary>Ziel setzen (ein Schritt), die Auswahl bleibt am Stueck.</summary>
    private void ZielSetzen(string ort, Stelle stelle, int ziel)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return;
        }
        if (_projekt.EtagenAendern(ort, Texte.Ziel, (g, k) => EtagenBearbeitung.ZielSetzen(g, k, stelle, ziel)))
        {
            Flaeche.Waehlen(stelle);
            Melden(Texte.ZielGesetzt + ": " + Etagen.Name(_projekt.Grundriss(Etagen.Teilen(ort).Ort), ziel));
        }
    }

    /// <summary>Start-Test: Ziel des gewaehlten Stuecks setzen wie mit der Auswahl rechts.</summary>
    internal bool ZielSetzenFuerTests(int ziel)
    {
        if (_ort is null || Flaeche.Auswahl is not { } a)
        {
            return false;
        }
        ZielSetzen(_ort, a, ziel);
        return true;
    }

    /// <summary>Start-Test: „Etage hinzufügen“ wie mit dem Knopf.</summary>
    internal void EtageHinzufuegenFuerTests() => EtageHinzufuegen();
}
