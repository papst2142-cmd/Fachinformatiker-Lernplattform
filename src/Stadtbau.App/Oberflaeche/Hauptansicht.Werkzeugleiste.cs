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
/// Welle S 0.27.0 (Weisung P12-118 Teil B, Entwuerfe Meldung P12-79 Bilder 3–5): Werkzeugleiste oben links in der Ansicht
/// „Stadt von oben“ mit „Wählen“, „Maßband“, „Ebenen ▾“ und „Notiz“ (in 3D nur „Wählen“ und „Maßband“). Rechts zeigt die
/// Karte „Ausgewähltes Stück“ beim Massband die Erklaerung und beim Notiz-Zettel das Textfeld.
/// </summary>
public sealed partial class Hauptansicht
{
    private readonly WrapPanel _werkzeugLeiste = new() { Margin = new Thickness(Kamerakarte.Rand, Kamerakarte.Rand, Kamerakarte.Rand, 0), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
    private Button _waehlenChip = null!;
    private Button _massbandChip = null!;
    private Button _ebenenChip = null!;
    private Button _notizChip = null!;
    private Button _mehrereChip = null!;
    private readonly TextBlock _ebenenHinweis = Schrift.Text("", "Klein", farbe: "Pinsel.muted");
    private readonly Border _ebenenKarte = new()
    {
        Padding = new Thickness(10, 6),
        CornerRadius = new CornerRadius(12),
        BorderThickness = new Thickness(1),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        IsVisible = false,
        MinWidth = 240,
    };
    private readonly Dictionary<Ebene, CheckBox> _ebenenSchalter = [];

    /// <summary>Start-Test: Knoepfe der Leiste und die Liste.</summary>
    internal Button MassbandKnopf => _massbandChip;
    internal Button NotizKnopf => _notizChip;
    internal Button MehrereKnopf => _mehrereChip;
    internal Button EbenenKnopf => _ebenenChip;
    internal Border EbenenListe => _ebenenKarte;
    internal WrapPanel WerkzeugLeiste => _werkzeugLeiste;
    internal IReadOnlyDictionary<Ebene, CheckBox> EbenenSchalter => _ebenenSchalter;
    internal string EbenenHinweisText => _ebenenHinweis.IsVisible ? _ebenenHinweis.Text ?? "" : "";

    /// <summary>Die Leiste und die Liste der Ebenen ueber die Mitte legen (oben links); die Karte haelt oben ihren Platz frei.</summary>
    private void WerkzeugLeisteBauen(Grid mitte)
    {
        _waehlenChip = Chip(Texte.Waehlen, Texte.WaehlenTipp, () =>
        {
            Karte.MassbandAn = false;
            Karte.MehrereWaehlen = false;
            SetzenBeenden();
            _ebenenKarte.IsVisible = false;
            ChipsZeigen();
        });
        _mehrereChip = Chip(Texte.MehrereWaehlen, Texte.MehrereWaehlenTipp, MehrereUmschalten);
        _massbandChip = Chip(Texte.Massband, Texte.MassbandTipp, MassbandUmschalten);
        _ebenenChip = Chip(Texte.EbenenKnopf, Texte.EbenenTipp, () =>
        {
            _ebenenKarte.IsVisible = !_ebenenKarte.IsVisible;
            ChipsZeigen();
            if (_ebenenKarte.IsVisible)
            {
                Ansage.Sagen(Texte.EbenenTitel);
            }
        });
        _notizChip = Schrift.Knopf(Texte.Notiz).Vorlese(Texte.NotizSetzen).Mit(Texte.NotizTipp);
        _notizChip.Click += (_, _) => SetzenKnopfGedrueckt(_notizChip, true, () => AussenSetzenWaehlen(AussenArt.Notiz, null));
        _ebenenHinweis.VerticalAlignment = VerticalAlignment.Center;
        _ebenenHinweis.Margin = new Thickness(4, 0, 8, 8);
        foreach (var k in (Control[])[_waehlenChip, _mehrereChip, _massbandChip, _ebenenChip, _notizChip, _ebenenHinweis])
        {
            _werkzeugLeiste.Children.Add(k);
        }

        // Liste der Ebenen (Haken = zu sehen), darunter „Alle einblenden“
        var liste = new StackPanel { Spacing = 2 };
        liste.Children.Add(Schrift.Text(Texte.EbenenTitel, "Klein", FontWeight.Bold));
        foreach (var e in Ebenen.Alle)
        {
            var name = Ebenen.Name(e);
            var schalter = new CheckBox { Content = Schrift.Text(name), IsChecked = true, MinHeight = 36 }.Vorlese(name).Mit(Texte.EbeneTipp(name));
            schalter.IsCheckedChanged += (_, _) => EbenenUebernehmen();
            _ebenenSchalter[e] = schalter;
            liste.Children.Add(schalter);
        }
        var alle = Schrift.Knopf(Texte.EbenenAlleAn).Vorlese(Texte.EbenenAlleAn).Mit(Texte.EbenenAlleAnTipp);
        alle.Margin = new Thickness(0, 6, 0, 2);
        alle.Click += (_, _) => EbenenSetzen(new HashSet<Ebene>());
        liste.Children.Add(alle);
        _ebenenKarte.Child = liste;
        _ebenenKarte.Farbe(Border.BackgroundProperty, "Pinsel.card").Farbe(Border.BorderBrushProperty, "Pinsel.border");
        _ebenenKarte.BoxShadow = BoxShadows.Parse("0 6 18 0 #40000000");
        _ebenenKarte.Vorlese(Texte.EbenenTitel);

        mitte.Children.Add(_werkzeugLeiste);
        mitte.Children.Add(_ebenenKarte);
        // Weisung P12-137: Anzeigen der Draufsicht und der 3D-Ansicht liegen nie unter der Kamera-Karte, der Werkzeugleiste
        // oder der Liste der Ebenen
        Karte.Verdecker = v => Anzeigen.Rahmen(v, _kameraAussen, _werkzeugLeiste, _ebenenKarte);
        _werkzeugLeiste.SizeChanged += (_, _) =>
        {
            _ebenenKarte.Margin = new Thickness(Kamerakarte.Rand + 150, _werkzeugLeiste.Bounds.Bottom, 0, 0);
            Karte.ObenFrei = _werkzeugLeiste.Bounds.Bottom;
        };

        // gemerkte Ebenen
        EbenenSetzen(WerkzeugEinstellungen.EbenenAus(_ordner), merken: false);
        Karte.MassbandGeaendert += () =>
        {
            ChipsZeigen();
            AussenAuswahlZeigen();
        };
        Karte.SetzenGeaendert += ChipsZeigen;
        Karte.MehrereGeaendert += () =>
        {
            ChipsZeigen();
            if (Karte.MehrereWaehlen)
            {
                Melden(Texte.MehrereWaehlenTipp);
            }
        };
        ChipsZeigen();
    }

    private static Button Chip(string text, string tipp, Action aktion)
    {
        var k = Schrift.Knopf(text).Vorlese(text).Mit(tipp);
        k.Click += (_, _) => aktion();
        return k;
    }

    /// <summary>Massband an oder aus (Knopf und Taste L).</summary>
    internal void MassbandUmschalten()
    {
        if (_projekt is null || _innen || _geschichteOffen)
        {
            return;
        }
        Karte.MassbandAn = !Karte.MassbandAn;
        AussenAuswahlZeigen();
    }

    /// <summary>S 0.30.0: „Mehrere wählen“ an oder aus (Knopf und Taste U, Stadt von oben und 3D).</summary>
    internal void MehrereUmschalten()
    {
        if (_projekt is null || _innen || _geschichteOffen)
        {
            return;
        }
        Karte.MehrereWaehlen = !Karte.MehrereWaehlen;
        AussenAuswahlZeigen();
    }

    /// <summary>Notiz setzen (Knopf und Taste N, nur in „Stadt von oben“).</summary>
    internal void NotizSetzenWaehlen()
    {
        if (_drei)
        {
            Melden(Texte.NotizNurDraufsicht);
            return;
        }
        SetzenKnopfGedrueckt(_notizChip, true, () => AussenSetzenWaehlen(AussenArt.Notiz, null));
    }

    /// <summary>Markierung der Knoepfe: der aktive Zustand wie ein betonter Knopf.</summary>
    private void ChipsZeigen()
    {
        if (_waehlenChip is null)
        {
            return;
        }
        var massband = Karte.MassbandAn;
        ChipMarkieren(_waehlenChip, !massband && Karte.SetzenArt is null && !Karte.MehrereWaehlen);
        ChipMarkieren(_mehrereChip, Karte.MehrereWaehlen);
        Avalonia.Automation.AutomationProperties.SetName(_mehrereChip, Karte.MehrereWaehlen ? Texte.Gewaehlt(Texte.MehrereWaehlen) : Texte.MehrereWaehlen);
        ChipMarkieren(_massbandChip, massband);
        ChipMarkieren(_ebenenChip, _ebenenKarte.IsVisible);
        _ebenenChip.IsVisible = !_drei;
        _notizChip.IsVisible = !_drei;
        _ebenenKarte.IsVisible &= !_drei;
        var aus = Karte.EbenenAus.Count;
        _ebenenHinweis.Text = Texte.EbenenAusHinweis(aus);
        _ebenenHinweis.IsVisible = aus > 0 && !_drei;
    }

    private static void ChipMarkieren(Button k, bool an)
    {
        if (an)
        {
            k.Farbe(Button.BackgroundProperty, "VerlaufW.primary").Farbe(Button.ForegroundProperty, "Pinsel.on_accent");
            (k.Content as TextBlock)?.Farbe(TextBlock.ForegroundProperty, "Pinsel.on_accent");
        }
        else
        {
            k.Farbe(Button.BackgroundProperty, "Pinsel.card_alt").Farbe(Button.ForegroundProperty, "Pinsel.text");
            (k.Content as TextBlock)?.Farbe(TextBlock.ForegroundProperty, "Pinsel.text");
        }
    }

    private bool _ebenenSetzen;

    /// <summary>Ausgeblendete Ebenen setzen (Schalter, Karte, Hinweis) und merken.</summary>
    internal void EbenenSetzen(IReadOnlySet<Ebene> aus, bool merken = true)
    {
        _ebenenSetzen = true;
        foreach (var (e, schalter) in _ebenenSchalter)
        {
            schalter.IsChecked = !aus.Contains(e);
        }
        _ebenenSetzen = false;
        Karte.EbenenAus = aus;
        if (merken)
        {
            Merken(aus);
        }
        ChipsZeigen();
    }

    private void EbenenUebernehmen()
    {
        if (_ebenenSetzen)
        {
            return;
        }
        var aus = _ebenenSchalter.Where(p => p.Value.IsChecked != true).Select(p => p.Key).ToHashSet();
        Karte.EbenenAus = aus;
        Merken(aus);
        ChipsZeigen();
        Melden(Texte.EbenenAusHinweis(aus.Count));
    }

    private void Merken(IReadOnlySet<Ebene> aus)
    {
        try
        {
            WerkzeugEinstellungen.EbenenAusSetzen(_ordner, aus);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ordner.Fehler("Ebenen merken: " + e.GetType().Name + ": " + e.Message);
            Melden(Texte.EbenenSpeichernFehler);
        }
    }

    /// <summary>Wer ein Stueck einer ausgeblendeten Ebene setzt, sieht sie wieder (E1: sonst setzt man Unsichtbares).</summary>
    private void EbeneFuerSetzenAn(AussenArt art)
    {
        if (Ebenen.Von(art) is { } e && !Karte.Zeigt(e))
        {
            EbenenSetzen(Karte.EbenenAus.Where(x => x != e).ToHashSet());
            Melden(Texte.EbeneWiederAn(Ebenen.Name(e)));
        }
    }

    // ---- rechts: Massband und Notiz ----------------------------------------------------------------

    /// <summary>Karte „Ausgewähltes Stück“ beim Massband: Erklaerung, gemessene Laenge, „Maßband wegnehmen“.</summary>
    private bool MassbandTeil()
    {
        if (!Karte.MassbandAn)
        {
            return false;
        }
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.Massband, "Mittel", FontWeight.Bold));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.MassbandErklaerung, "Klein"));
        var laenge = Schrift.Text(Karte.MassbandText ?? "–", "Mittel", FontWeight.Bold).Vorlese(Texte.MassbandName);
        _aussenAuswahl.Children.Add(laenge);
        var weg = Schrift.Knopf(Texte.MassbandWeg).Vorlese(Texte.MassbandWeg).Mit(Texte.MassbandWegTipp);
        weg.Click += (_, _) => MassbandUmschalten();
        _aussenAuswahl.Children.Add(weg);
        return true;
    }

    /// <summary>Feld „Text der Notiz“ mit „Text übernehmen“ und dem Hinweis, wo die Notiz steht.</summary>
    private void NotizTeil(AussenStelle s)
    {
        if (_projekt is null || s.Art != AussenArt.Notiz)
        {
            return;
        }
        var feld = new TextBox
        {
            Text = AussenBearbeitung.NotizText(_projekt.AussenStand, s),
            MaxLength = AussenBearbeitung.NotizHoechstens + 10,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 64,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = !_projekt.NurLesen,
        };
        var fehler = Schrift.Text("", "Klein", farbe: "Pinsel.red");
        fehler.IsVisible = false;
        var knopf = Schrift.Knopf(Texte.NotizUebernehmen).Vorlese(Texte.NotizUebernehmen).Mit(Texte.NotizUebernehmenTipp);
        knopf.IsEnabled = !_projekt.NurLesen;
        knopf.Click += (_, _) =>
        {
            if (NotizTextAendern(s, feld.Text ?? "") is { } grund)
            {
                fehler.Text = grund;
                fehler.IsVisible = true;
                Melden(grund);
            }
        };
        NotizFeld = feld;
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.NotizFeld, "Klein").Fuer(feld));
        _aussenAuswahl.Children.Add(feld);
        _aussenAuswahl.Children.Add(knopf);
        _aussenAuswahl.Children.Add(fehler);
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.NotizNurImProjekt, "Klein", farbe: "Pinsel.muted"));
    }

    /// <summary>Start-Test: Feld „Text der Notiz“ der gewaehlten Notiz.</summary>
    internal TextBox? NotizFeld { get; private set; }

    /// <summary>Text einer Notiz aendern (auch fuer den Start-Test): null = geaendert (ein Schritt), sonst der Grund.</summary>
    public string? NotizTextAendern(AussenStelle s, string text)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return Texte.NotizLeer;
        }
        if (string.Equals(AussenBearbeitung.NotizText(_projekt.AussenStand, s), text.Trim(), StringComparison.Ordinal))
        {
            return null;
        }
        // ein abgelehnter Text aendert nichts, also entsteht auch kein Schritt im Verlauf
        if (AussenBearbeitung.NotizTextNicht(text) is { } grund)
        {
            return grund;
        }
        _projekt.AussenAendern(Texte.NotizGeaendert, a => AussenBearbeitung.NotizTextSetzen(a, s, text));
        Melden(Texte.NotizGeaendert);
        return null;
    }
}
