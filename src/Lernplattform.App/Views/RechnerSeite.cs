using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Messung;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Views;

/// <summary>
/// Phase 3, W2 (Paket R6): Rechner-Seite wie app_gui.CalcView / main.py CalcScreen (Python
/// 0.63.2): Umschalter "Rechner / Trainer", Karten Subnetting (IPv4/IPv6; Netzdaten, Netz
/// aufteilen mit Blaettern und Sprung, Nach Hostbedarf), RAID, Bildschirm-Datenvolumen und USV
/// (Modi, W/VA, Bild, Uebungsaufgaben), je mit aufklappbarem Rechenweg. Gerechnet wird im Core
/// (Vergleichswerte G24-G26), hier wird nur angezeigt. Ein Aufbau fuer PC und Handy: unter
/// <see cref="MainView.BreitGrenze"/> gilt die schmale Fassung (Handy-Rechenwege, schmale
/// Tabellen, fit_lines einheitlich). Fehleingabe auf beiden Geraeten als roter Hinweis mit dem
/// Meldungstext, Ergebniskasten leer, kein Fenster (Weisung P3-4 Frage 2). Die Rechner speichern
/// nichts; der Reiter "Trainer" (W3, R7, <see cref="RechenTrainerTeil"/>) speichert je Pruefen.
/// W4a (Weisung P3-17): Auswahl-Pillen ueber den Karten, immer genau eine Karte sichtbar; neu
/// "Datenmengen" und "Übertragung" (<see cref="DatenKarten"/>). W4b (Weisung P3-21): Karte
/// "Bild und Video" (<see cref="VideoKarte"/>) mit Uebernahme in die Uebertragungsdauer.
/// W4c (Weisung P3-23): Karte "WiSo-Rechner" (<see cref="WisoKarte"/>) als letzte Pille.
/// </summary>
public sealed class RechnerSeite : UserControl, IAufklappbar
{
    private readonly ScrollViewer _rollen;
    private readonly Pillen _tabs;
    private readonly Pillen _auswahl;
    private readonly Dictionary<string, Control> _karten = new(StringComparer.Ordinal);
    private readonly DatenKarten _datenKarten;
    private readonly VideoKarte _video;
    private readonly WisoKarte _wiso;
    private readonly StackPanel _rechner = new() { Spacing = 16 };
    private readonly RechenTrainerTeil _trainer;
    private readonly SubnetKarte _subnet;
    private readonly List<Rechenwegteil> _rechenwege = [];
    private bool _schmal;

    // RAID
    private readonly Pillen _raidLevel;
    private readonly TextBox _platten;
    private readonly TextBox _groesse;
    private readonly Ergebniskasten _raidKasten = new(NochKeineBerechnung);
    private readonly Fehlerhinweis _raidFehler = new();

    // USV
    private readonly Dictionary<string, TextBox> _usvFelder = new(StringComparer.Ordinal);
    private readonly Pillen _usvEinheit;
    private readonly UsvBildAnzeige _usvBild = new();
    private readonly Ergebniskasten _usvKasten = new();
    private readonly TextBlock _usvHinweis = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.text_dim");
    private readonly Fehlerhinweis _usvFehler = new();
    private readonly Ergebniskasten _aufgabeKasten = new();
    private readonly Button _loesungKnopf = LernTeile.Knopf(UPS_SOLUTION_SHOW);
    private int _aufgabe;
    private bool _loesung;

    public RechnerSeite()
    {
        _subnet = new SubnetKarte(this);
        _tabs = new Pillen(CALC_TABS, "rechner", TabWechseln);
        _trainer = new RechenTrainerTeil(() => _schmal);
        _trainer.IsVisible = false;

        // --- RAID ---
        _raidLevel = new Pillen(RAID_LEVELS.Select(l => (l, l)), RAID_LEVELS[2], _ => { });
        _platten = RechnerTeile.Feld("4", RAID_FIELD_DISKS, zahl: true, breite: 110);
        _groesse = RechnerTeile.Feld("1000", RAID_FIELD_SIZE, zahl: true, breite: 110);
        var raid = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                RechnerTeile.Beschriftung(RAID_FIELD_LEVEL), _raidLevel,
                Felder(RechnerTeile.Zeile(RAID_FIELD_DISKS, _platten), RechnerTeile.Zeile(RAID_FIELD_SIZE, _groesse)),
                Knopf(Berechnen, RaidRechnen), _raidFehler, _raidKasten,
            },
        };
        Rechenweg(raid, CALC_EXPLAIN_RAID, CALC_EXPLAIN_RAID_HANDY);
        EnterRechnet(RaidRechnen, _platten, _groesse);

        // --- USV ---
        _usvEinheit = new Pillen(UPS_UNITS, UPS_UNITS[0].Item1, _ => { });
        var gruppen = new Spaltenraster(300, 24);
        foreach (var (titel, schluessel) in new[]
        {
            (UPS_GROUP_LOAD, new[] { "last", "pf", "eta", "nenn_va", "nenn_w" }),
            (UPS_GROUP_BATTERY, new[] { "block_v", "block_ah", "reihe", "parallel", "minuten", "alterung" }),
        })
        {
            var spalte = new StackPanel { Spacing = 8, Children = { LernTeile.Label(titel) } };
            foreach (var s in schluessel)
            {
                var feld = RechnerTeile.Feld(UPS_FIELD_DEFAULTS[s], UPS_FIELD_CAPTIONS[s], zahl: true, breite: 110);
                _usvFelder[s] = feld;
                var zeile = RechnerTeile.Zeile(UPS_FIELD_CAPTIONS[s], feld).HoechstBreite(420);
                zeile.HorizontalAlignment = HorizontalAlignment.Left;
                spalte.Children.Add(zeile);
                if (s == "last")
                {
                    spalte.Children.Add(_usvEinheit);
                }
            }
            gruppen.Children.Add(spalte);
        }
        EnterRechnet(() => UsvRechnen("empfehlung"), [.. _usvFelder.Values]);
        var modi = new WrapPanel();
        foreach (var (modus, text) in UPS_MODES)
        {
            modi.Children.Add(Knopf(text, () => UsvRechnen(modus)));
        }
        var naechste = LernTeile.Knopf(UPS_TASK_NEXT);
        naechste.Click += (_, _) => NaechsteAufgabe();
        _loesungKnopf.Click += (_, _) => LoesungUmschalten();
        var usv = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                gruppen, modi, _usvFehler,
                RechnerTeile.Beschriftung(UPS_RULE_TEXT),
                _usvBild, _usvKasten, _usvHinweis,
            },
        };
        Rechenweg(usv, CALC_EXPLAIN_UPS, CALC_EXPLAIN_UPS_HANDY);
        usv.Children.Add(Abstand(LernTeile.Label(Beschriftungen.Gross(UPS_TASKS_TITLE)), 8));
        usv.Children.Add(new WrapPanel { Children = { naechste, _loesungKnopf } });
        usv.Children.Add(_aufgabeKasten);

        // --- Datenmengen, Uebertragung (W4a) ---
        _datenKarten = new DatenKarten(() => _schmal);
        _rechenwege.Add(_datenKarten.DatenWeg);
        _rechenwege.Add(_datenKarten.DauerWeg);
        // --- Bild und Video (W4b) ---
        _video = new VideoKarte(() => _schmal, (text, einheit, hinweis) =>
        {
            Waehle("uebertragung");
            _datenKarten.Uebernehmen(text, einheit, hinweis);
        });
        _rechenwege.Add(_video.Weg);
        // --- WiSo-Rechner (W4c) ---
        _wiso = new WisoKarte(() => _schmal);
        _rechenwege.AddRange(_wiso.Rechenwege);

        _karten["subnetting"] = _subnet.Karte;
        _karten["datenmengen"] = _datenKarten.DatenKarte;
        _karten["uebertragung"] = _datenKarten.DauerKarte;
        _karten["bild"] = _video.Karte;
        _karten["raid"] = RechnerTeile.Karte(RAID_TITLE, "Pinsel.purple", RechnerTeile.Unterschrift(RAID_SUBTITLE), raid);
        _karten["usv"] = RechnerTeile.Karte(UPS_TITLE, "Pinsel.orange", RechnerTeile.Unterschrift(UPS_SUBTITLE), usv);
        _karten["wiso"] = _wiso.Karte;
        _auswahl = new Pillen(RechnerAuswahl, RechnerAuswahl[0].Wert, AuswahlWechseln);
        _rechner.Children.Add(_auswahl);
        foreach (var (wert, _) in RechnerAuswahl)
        {
            _karten[wert].IsVisible = wert == RechnerAuswahl[0].Wert;
            _rechner.Children.Add(_karten[wert]);
        }

        _rollen = new ScrollViewer
        {
            Content = new StackPanel { Margin = new Thickness(16), Spacing = 14, Children = { _tabs, _rechner, _trainer } },
        };
        Content = _rollen;
        AufgabeZeigen();
        UsvRechnen("empfehlung", leise: true);
        SizeChanged += (_, _) => AufbauPruefen();
        Schrift.Geaendert += AufbauPruefen;
        DetachedFromVisualTree += (_, _) => Schrift.Geaendert -= AufbauPruefen;
        AttachedToVisualTree += (_, _) =>
        {
            Schrift.Geaendert -= AufbauPruefen;
            Schrift.Geaendert += AufbauPruefen;
            AufbauPruefen();
        };
    }

    /// <summary>Schmaler Aufbau (unter MainView.BreitGrenze): Handy-Fassung der Rechenwege und Tabellen.</summary>
    public bool Schmal => _schmal;

    /// <summary>Die Subnetting-Karte (fuer den Start ohne Bedienung).</summary>
    internal SubnetKarte Subnet => _subnet;

    internal Ergebniskasten RaidKasten => _raidKasten;

    /// <summary>Karten Datenmengen und Uebertragung (fuer den Start ohne Bedienung).</summary>
    internal DatenKarten Daten => _datenKarten;

    /// <summary>Karte Bild und Video (fuer den Start ohne Bedienung).</summary>
    internal VideoKarte Video => _video;

    /// <summary>Karte WiSo-Rechner (fuer den Start ohne Bedienung).</summary>
    internal WisoKarte Wiso => _wiso;

    /// <summary>Gewaehlte Karte der Auswahl-Pillen.</summary>
    internal string Gewaehlt => _auswahl.Wert;

    internal Ergebniskasten UsvKasten => _usvKasten;

    internal UsvBildAnzeige UsvBild => _usvBild;

    internal string RaidFehler => _raidFehler.Text ?? "";

    internal IReadOnlyList<Rechenwegteil> Rechenwege => _rechenwege;

    /// <summary>Reiter "Trainer" (fuer den Start ohne Bedienung).</summary>
    internal RechenTrainerTeil Trainer => _trainer;

    // -- Aufbau --------------------------------------------------------------------------------

    private void AufbauPruefen()
    {
        var haupt = this.FindAncestorOfType<MainView>();
        if (haupt is null || haupt.Bounds.Width <= 0)
        {
            return;
        }
        var schmal = haupt.Bounds.Width < MainView.BreitGrenze;
        if (schmal == _schmal && _rechenwege.All(r => r.Kasten.Einheitlich == schmal))
        {
            return;
        }
        _schmal = schmal;
        foreach (var kasten in this.GetVisualDescendants().OfType<Ergebniskasten>().Concat(_rechenwege.Select(r => r.Kasten)))
        {
            kasten.Einheitlich = schmal;
        }
        foreach (var weg in _rechenwege)
        {
            weg.Fassung(schmal);
        }
        _subnet.AufbauGeaendert();
        _datenKarten.AufbauGeaendert();
        _wiso.AufbauGeaendert();
    }

    /// <summary>Auswahl-Pille: genau eine Karte sichtbar, oben anfangen.</summary>
    private void AuswahlWechseln(string wert)
    {
        foreach (var (schluessel, karte) in _karten)
        {
            karte.IsVisible = schluessel == wert;
        }
        _rollen.Offset = default;
    }

    /// <summary>Karte waehlen wie mit der Pille (fuer den Start ohne Bedienung).</summary>
    internal void Waehle(string wert)
    {
        _auswahl.Waehle(wert);
        AuswahlWechseln(wert);
    }

    private void TabWechseln(string wert)
    {
        _trainer.IsVisible = wert == "trainer";
        _rechner.IsVisible = !_trainer.IsVisible;
        if (_trainer.IsVisible)
        {
            _trainer.Gezeigt();
        }
        _rollen.Offset = default;     // to_top wie Python
    }

    /// <summary>Umschalter "Rechner / Trainer" (fuer den Start ohne Bedienung).</summary>
    internal void Tab(string wert)
    {
        _tabs.Waehle(wert);
        TabWechseln(wert);
    }

    // -- RAID -----------------------------------------------------------------------------------

    internal void RaidRechnen() =>
        Ausfuehren(_raidKasten, _raidFehler, () => Raid.Bericht(_raidLevel.Wert, _platten.Text, _groesse.Text));

    /// <summary>Feld der RAID-Karte setzen (fuer den Start ohne Bedienung).</summary>
    internal void RaidEingabe(string platten, string groesse)
    {
        _platten.Text = platten;
        _groesse.Text = groesse;
    }

    private static void Ausfuehren(Ergebniskasten kasten, Fehlerhinweis fehler, Func<string> rechnung)
    {
        try
        {
            kasten.Setzen(rechnung());
            fehler.Zeigen(null);
        }
        catch (RechnerFehler f)
        {
            // B-D-04 (D-4): Kasten leer, dann die Meldung (roter Hinweis, Weisung P3-4)
            kasten.Setzen("");
            fehler.Zeigen(f.Message);
        }
    }

    // -- USV -----------------------------------------------------------------------------------

    internal void UsvRechnen(string modus, bool leise = false)
    {
        var felder = _usvFelder.ToDictionary(p => p.Key, p => (string?)(p.Value.Text ?? ""), StringComparer.Ordinal);
        UsvErgebnis ergebnis;
        try
        {
            ergebnis = Usv.Berechnen(modus, felder, _usvEinheit.Wert);
        }
        catch (RechnerFehler f)
        {
            _usvKasten.Setzen("");
            _usvHinweis.Text = "";
            _usvHinweis.IsVisible = false;
            _usvBild.Setzen(null);
            _usvFehler.Zeigen(leise ? null : f.Message);
            return;
        }
        _usvFehler.Zeigen(null);
        // Der Hinweis "Vereinfacht: ..." steht unter der Tabelle als Text, der umbrechen darf (main.py calc_ups)
        var trenner = "\n\n" + UPS_SIMPLE_TEXT;
        var stelle = ergebnis.Text.IndexOf(trenner, StringComparison.Ordinal);
        var nurHinweis = stelle >= 0 && stelle + trenner.Length == ergebnis.Text.Length;
        _usvKasten.Setzen(nurHinweis ? ergebnis.Text[..stelle] : ergebnis.Text);
        _usvHinweis.Text = nurHinweis ? UPS_SIMPLE_TEXT : "";
        _usvHinweis.IsVisible = nurHinweis;
        _usvBild.Setzen(ergebnis.Bild);
    }

    /// <summary>Feld der USV-Karte setzen (fuer den Start ohne Bedienung).</summary>
    internal void UsvFeld(string schluessel, string wert) => _usvFelder[schluessel].Text = wert;

    private void AufgabeZeigen()
    {
        _aufgabeKasten.Setzen(Usv.AufgabeText(_aufgabe, _loesung));
        ((TextBlock)_loesungKnopf.Content!).Text = _loesung ? UPS_SOLUTION_HIDE : UPS_SOLUTION_SHOW;
    }

    internal void NaechsteAufgabe()
    {
        _aufgabe = (_aufgabe + 1) % UPS_TASK_TEXTS.Count;
        _loesung = false;
        AufgabeZeigen();
    }

    internal void LoesungUmschalten()
    {
        _loesung = !_loesung;
        AufgabeZeigen();
    }

    // -- Aufklappen (Layoutpruefung) -------------------------------------------------------------

    public void AllesAufklappen()
    {
        foreach (var weg in _rechenwege)
        {
            weg.Zeigen(true);
        }
        _subnet.RechenwegZeigen(true);
    }

    // -- Hilfen --------------------------------------------------------------------------------

    private Rechenwegteil Rechenweg(StackPanel karte, string pc, string handy)
    {
        var weg = new Rechenwegteil(() => _schmal ? handy : pc);
        karte.Children.Add(weg.Knopf);
        karte.Children.Add(weg.Kasten);
        _rechenwege.Add(weg);
        return weg;
    }

    internal static Button Knopf(string text, Action klick)
    {
        var knopf = LernTeile.Knopf(text, betont: true);
        knopf.HorizontalAlignment = HorizontalAlignment.Left;
        knopf.Click += (_, _) => klick();
        return knopf;
    }

    internal static Control Felder(params Control[] zeilen)
    {
        var panel = new StackPanel { Spacing = 8 };
        foreach (var z in zeilen)
        {
            z.HorizontalAlignment = HorizontalAlignment.Left;
            panel.Children.Add(z.HoechstBreite(420));
        }
        return panel;
    }

    private static T Abstand<T>(T element, double oben) where T : Control
    {
        element.Margin = new Thickness(0, oben, 0, 0);
        return element;
    }

    internal static void EnterRechnet(Action rechnen, params TextBox[] felder)
    {
        foreach (var feld in felder)
        {
            feld.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    rechnen();
                }
            };
        }
    }
}

/// <summary>Roter Hinweis mit dem Meldungstext einer Fehleingabe (Weisung P3-4 Frage 2); leer = unsichtbar.</summary>
public sealed class Fehlerhinweis : TextBlock
{
    public Fehlerhinweis()
    {
        TextWrapping = TextWrapping.Wrap;
        FontWeight = FontWeight.SemiBold;
        IsVisible = false;
        this.Groesse("Normal").Farbe(ForegroundProperty, "Pinsel.red");
    }

    public void Zeigen(string? meldung)
    {
        Text = meldung ?? "";
        IsVisible = !string.IsNullOrEmpty(meldung);
    }
}

/// <summary>"Rechenweg anzeigen" mit Kasten (zu Beginn zu); der Text kommt beim Aufklappen.</summary>
public sealed class Rechenwegteil
{
    private readonly Func<string> _text;

    public Rechenwegteil(Func<string> text)
    {
        _text = text;
        Knopf = LernTeile.Knopf(RechnerTexte.RechenwegAnzeigen);
        Knopf.HorizontalAlignment = HorizontalAlignment.Left;
        Knopf.Click += (_, _) => Zeigen(!Offen);
        Kasten.IsVisible = false;
    }

    public Button Knopf { get; }

    public Ergebniskasten Kasten { get; } = new();

    public bool Offen { get; private set; }

    public void Zeigen(bool offen)
    {
        Offen = offen;
        if (offen)
        {
            Kasten.Setzen(_text());
        }
        Kasten.IsVisible = offen;
        ((TextBlock)Knopf.Content!).Text = offen ? RechnerTexte.RechenwegAusblenden : RechnerTexte.RechenwegAnzeigen;
    }

    /// <summary>Aufbau gewechselt: offenen Rechenweg in der passenden Fassung zeigen.</summary>
    public void Fassung(bool schmal)
    {
        if (Offen)
        {
            Kasten.Setzen(_text());
        }
    }
}
