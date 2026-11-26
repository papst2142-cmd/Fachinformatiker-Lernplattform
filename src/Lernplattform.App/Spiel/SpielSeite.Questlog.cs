using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Views;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 0.67.0 (Weisung P12-204, Sammelliste 301, Kurzplan P12-118 Abschnitt 3): Handy-Knopf unten links (ersetzt den
/// Klapp-Pfeil, der an den rechten Rand der unteren Karte rueckt) mit der Zahl der offenen Auftraege; er oeffnet das Questlog
/// als Fenster im Aussehen eines Handys (PC und Handy gleich, am PC auch mit der Taste der Tastenbelegung, Vorgabe L). Nur
/// Anzeige (<see cref="Questlog"/>); „Hingehen“ fuehrt die Figur zum Ort des Auftrags wie die Ortsliste (ohne 3D: sofort da).
/// </summary>
public sealed partial class SpielSeite
{
    private Button? _questKnopf;

    private TextBlock? _questZahl;

    private Border? _questZahlRand;

    /// <summary>Gerade offenes Questlog (Start-Test): Reiter Erledigt gewaehlt.</summary>
    public bool QuestlogErledigt { get; private set; }

    /// <summary>Gerade gezeigte Eintraege des Questlogs (Start-Test).</summary>
    public IReadOnlyList<Questlog.Eintrag> QuestlogEintraege { get; private set; } = [];

    /// <summary>Hingehen-Knoepfe des offenen Questlogs (Start-Test).</summary>
    public IReadOnlyList<Button> QuestlogHingehen { get; private set; } = [];

    /// <summary>Der Handy-Knopf (Start-Test, Fotos).</summary>
    public Button? QuestKnopf => _questKnopf;

    private Button QuestKnopfBauen()
    {
        var symbol = Symbole.Block("smartphone_rounded", 26, "Pinsel.text");
        _questZahl = new TextBlock { FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center }.Groesse("Klein");
        _questZahl.Farbe(TextBlock.ForegroundProperty, "Pinsel.text");
        // gruene Zahl: gruener Rand um die Zahl (Kontrast der Schrift bleibt wie jeder Text)
        _questZahlRand = new Border
        {
            Child = _questZahl,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(6, 0),
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _questZahlRand.Farbe(Border.BorderBrushProperty, "Pinsel.green");
        var knopf = Schrift.Knopf("");
        knopf.Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { symbol, _questZahlRand } };
        knopf.Padding = new Thickness(10, 4);
        knopf.VerticalAlignment = VerticalAlignment.Top;
        knopf.Click += (_, _) => _ = QuestlogZeigenAsync();
        _questKnopf = knopf;
        return knopf;
    }

    /// <summary>Zahl am Knopf und Name fuer den Screenreader (bei jedem Beschriften).</summary>
    private void QuestKnopfSetzen(StadtSpiel spiel)
    {
        if (_questKnopf is null || _questZahl is null || _questZahlRand is null)
        {
            return;
        }
        var zahl = Questlog.ZahlOffen(spiel);
        _questZahl.Text = zahl.ToString(CultureInfo.InvariantCulture);
        _questZahlRand.IsVisible = zahl > 0;
        Avalonia.Automation.AutomationProperties.SetName(_questKnopf, string.Format(CultureInfo.InvariantCulture, VorleseTexte.Auftraege, zahl));
        _questKnopf.IsVisible = !spiel.Einrichten;
    }

    /// <summary>Das Questlog oeffnen (Knopf, Taste); <paramref name="erledigt"/> = gleich den Reiter „Erledigt“ zeigen.</summary>
    public Task QuestlogZeigenAsync(bool erledigt = false) => HandyZeigenAsync(erledigt ? HandyReiter.Erledigt : HandyReiter.Offen);

    /// <summary>Welle A 0.80.0: Reiter des Spiel-Handys (Auftraege offen, erledigt, Karte); A 0.81.0 (Sammelliste 350): Uebersicht.</summary>
    public enum HandyReiter { Offen, Erledigt, Karte, Uebersicht }

    /// <summary>Gibt es den Reiter „Übersicht“ (nur mit Spielstand und Figur, nicht in der reinen Stadt-Probe)?</summary>
    public bool UebersichtReiterMoeglich => _tafelAn && Tafel is { HatFigur: true };

    /// <summary>Inhalt des Reiters „Übersicht“, solange er gezeigt wird (Start-Test, Fotos); sonst leer.</summary>
    public IReadOnlyList<Control> UebersichtGezeigt { get; private set; } = [];

    /// <summary>
    /// Das Spiel-Handy oeffnen. Welle A 0.80.0 (Weisung P12-268, Nico Antwort 5, P12-258): dritter Reiter „Karte“ mit der Karte
    /// der Stadt und der Liste der Orte (das fruehere Ortsmenue). Antippen eines Orts schliesst das Handy, die Figur geht sofort los
    /// (ohne 3D: kommt sofort an); „Mit dem Bus“ steht beim Ort, wenn der Bus schneller ist.
    /// </summary>
    public async Task HandyZeigenAsync(HandyReiter start)
    {
        if (Spiel is not { } spiel || Dialog.Offen is not null)
        {
            return;
        }
        var offen = Questlog.Offen(spiel);
        var fertig = Questlog.Erledigte(spiel);
        var liste = new StackPanel { Spacing = 10 };
        var reiterOffen = Schrift.Knopf(string.Format(CultureInfo.InvariantCulture, QuestlogTexte.Offen, offen.Count));
        var reiterErledigt = Schrift.Knopf(string.Format(CultureInfo.InvariantCulture, QuestlogTexte.Erledigt, fertig.Count));
        var reiterKarte = Schrift.Knopf(KarteTexte.Karte);
        // Welle A 0.81.0 (Sammelliste 350, Weisung P12-274): dieselbe Uebersicht wie bisher die Seite der Spiel-Tafel
        var reiterUebersicht = Schrift.Knopf(StadtTexte.Uebersicht);
        reiterUebersicht.IsVisible = UebersichtReiterMoeglich;
        string? ziel = null;
        string? busZiel = null;
        Action? danach = null;
        void Fuellen(HandyReiter reiter)
        {
            HandyReiterGerade = reiter;
            ReiterSetzen(reiterOffen, reiter == HandyReiter.Offen);
            ReiterSetzen(reiterErledigt, reiter == HandyReiter.Erledigt);
            ReiterSetzen(reiterKarte, reiter == HandyReiter.Karte);
            ReiterSetzen(reiterUebersicht, reiter == HandyReiter.Uebersicht);
            liste.Children.Clear();
            UebersichtGezeigt = [];
            if (reiter == HandyReiter.Uebersicht && Tafel is { } tafel)
            {
                QuestlogEintraege = [];
                QuestlogHingehen = [];
                QuestlogErledigt = false;
                KarteGezeigt = null;
                // Knoepfe, die eine Seite der Tafel oeffnen (Ticket, Platz wechseln), schliessen zuerst das Handy
                var teile = tafel.UebersichtAlsReiter(() => Fuellen(HandyReiter.Uebersicht), aktion => { danach = aktion; Dialog.AlleSchliessen(); });
                foreach (var teil in teile)
                {
                    liste.Children.Add(teil);
                }
                UebersichtGezeigt = teile;
                return;
            }
            if (reiter == HandyReiter.Karte)
            {
                QuestlogEintraege = [];
                QuestlogHingehen = [];
                QuestlogErledigt = false;
                KarteFuellen(spiel, liste, offen, id => { ziel = id; Dialog.AlleSchliessen(); }, id => { busZiel = id; Dialog.AlleSchliessen(); });
                return;
            }
            KarteGezeigt = null;
            var zeigeErledigt = reiter == HandyReiter.Erledigt;
            QuestlogErledigt = zeigeErledigt;
            var eintraege = zeigeErledigt ? fertig : offen;
            QuestlogEintraege = eintraege;
            var knoepfe = new List<Button>();
            if (eintraege.Count == 0)
            {
                liste.Children.Add(Schrift.Text(Questlog.LeerText(spiel, zeigeErledigt)));
            }
            foreach (var e in eintraege)
            {
                var teil = new StackPanel { Spacing = 4 };
                var kopf = new WrapPanel();
                kopf.Children.Add(new TextBlock { Text = (e.Erledigt ? "✓ " : "") + e.Titel, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 8, 0) }
                    .Groesse("Normal").Farbe(TextBlock.ForegroundProperty, "Pinsel.text"));
                if (e.NeuHeute)
                {
                    var neu = new Border { Child = Schrift.Text(QuestlogTexte.NeuHeute, "Klein", FontWeight.Bold), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(999), Padding = new Thickness(8, 0) };
                    neu.Farbe(Border.BorderBrushProperty, "Pinsel.green");
                    kopf.Children.Add(neu);
                }
                teil.Children.Add(kopf);
                teil.Children.Add(Schrift.Text(string.Format(CultureInfo.InvariantCulture, QuestlogTexte.Von, e.Questgeber), "Klein"));
                teil.Children.Add(Schrift.Text(string.Format(CultureInfo.InvariantCulture, QuestlogTexte.Wo, e.OrtText), "Klein"));
                if (e.Wartet.Length > 0)
                {
                    teil.Children.Add(Schrift.Text(e.Wartet, "Klein"));
                }
                if (e.Ticket.Length > 0)
                {
                    teil.Children.Add(Schrift.Text(e.Ticket, "Klein", farbe: "Pinsel.muted"));
                }
                if (Questlog.KetteText(e) is { Length: > 0 } kette)
                {
                    teil.Children.Add(Schrift.Text(kette, "Klein"));
                }
                if (!e.Erledigt && e.Ziel is { } ort)
                {
                    var hin = Schrift.Knopf(QuestlogTexte.Hingehen, betont: true);
                    hin.HorizontalAlignment = HorizontalAlignment.Left;
                    Avalonia.Automation.AutomationProperties.SetName(hin,
                        string.Format(CultureInfo.InvariantCulture, VorleseTexte.AuftragHingehen, e.Titel, spiel.Name(ort)));
                    hin.Click += (_, _) =>
                    {
                        ziel = ort;
                        Dialog.AlleSchliessen();
                    };
                    knoepfe.Add(hin);
                    teil.Children.Add(hin);
                }
                var karte = Schrift.Karte(teil, "Pinsel.card");
                karte.Padding = new Thickness(12);
                liste.Children.Add(karte);
            }
            QuestlogHingehen = knoepfe;
        }
        reiterOffen.Click += (_, _) => Fuellen(HandyReiter.Offen);
        reiterErledigt.Click += (_, _) => Fuellen(HandyReiter.Erledigt);
        reiterKarte.Click += (_, _) => Fuellen(HandyReiter.Karte);
        reiterUebersicht.Click += (_, _) => Fuellen(HandyReiter.Uebersicht);
        Fuellen(start == HandyReiter.Uebersicht && !reiterUebersicht.IsVisible ? HandyReiter.Offen : start);
        // Aussehen eines Handys: dicker runder Rahmen, oben ein Lautsprecher-Schlitz
        var schlitz = new Border { Width = 56, Height = 6, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
        schlitz.Farbe(Border.BackgroundProperty, "Pinsel.border_hi");
        var geraet = new Border
        {
            CornerRadius = new CornerRadius(28),
            BorderThickness = new Thickness(6),
            Padding = new Thickness(12),
            MaxWidth = 460,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new StackPanel
            {
                Spacing = 8,
                Children = { schlitz, new WrapPanel { Children = { reiterOffen, reiterErledigt, reiterKarte, reiterUebersicht } }, liste },
            },
        };
        geraet.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border_hi");
        HandyOffen = true;
        try
        {
            // Welle A 0.81.0 (Sammelliste 348): X oben rechts, immer zu sehen; Weisung P12-272 (Nico): kein Knopf „Schließen“ unten mehr
            await Dialog.ZeigenMitKreuz(KarteTexte.HandyTitel, KarteTexte.HandySchliessen, geraet);
        }
        finally
        {
            HandyOffen = false;
            KarteGezeigt = null;
            UebersichtGezeigt = [];
            QuestlogEintraege = [];
            QuestlogHingehen = [];
        }
        if (danach is { } tafelSchritt)
        {
            tafelSchritt();
            return;
        }
        if (Spiel is not { } jetzt)
        {
            return;
        }
        if (busZiel is { } mitBus)
        {
            jetzt.MitBusZu(mitBus);
        }
        else if (ziel is { } hinZu)
        {
            jetzt.GeheZu(hinZu);
        }
        else
        {
            return;
        }
        if (_ersatz.IsVisible)
        {
            jetzt.SofortAnkommen();
        }
        Flaeche.AnfordernBild();
    }

    private Grid? _klappZeile;

    /// <summary>Start-Test: stehen die Knoepfe des Orts unter dem Handy-Knopf? Seit A 0.99.0 (Sammelliste 399) stehen sie immer in
    /// eigener Zeile ueber den mittigen Symbolen, nie gequetscht neben ihnen; die Antwort ist darum immer nein.</summary>
    public bool KnoepfeUnten => false;

    /// <summary>Start-Test, Fotos (Sammelliste 350): steht „Spiel verlassen“ in einer eigenen Zeile unten (zu wenig Platz neben dem Pfeil)?</summary>
    public bool VerlassenUnten => Grid.GetRow(VerlassenKnopf) == 3;

    /// <summary>
    /// Start-Test (Sammelliste 402): Rechtecke von „Ich-Sicht“ und „Spiel verlassen“ und die Mitte der zwei Symbole in Punkten der
    /// Seite (null, solange etwas fehlt oder nicht angeordnet ist).
    /// </summary>
    public (Rect Ich, Rect Verlassen, double Mitte)? SeitenKnoepfe()
    {
        if (!_ichKnopf.IsEffectivelyVisible || !VerlassenKnopf.IsEffectivelyVisible || LeisteMitten() is not { } m
            || _ichKnopf.TranslatePoint(default, this) is not { } i || VerlassenKnopf.TranslatePoint(default, this) is not { } v)
        {
            return null;
        }
        return (new Rect(i, _ichKnopf.Bounds.Size), new Rect(v, VerlassenKnopf.Bounds.Size), m.Symbole);
    }

    /// <summary>
    /// Start-Test (Weisung P12-398): Rechteck von „Ich-Sicht“ in Punkten der Seite und die Mitte der Symbole (null, solange nicht zu
    /// sehen oder nicht angeordnet). Seit „Spiel verlassen“ unten entfaellt, steht „Ich-Sicht“ allein links neben dem Handy-Knopf.
    /// </summary>
    public (Rect Ich, double Mitte)? IchKnopfLage() =>
        _ichKnopf.IsEffectivelyVisible && LeisteMitten() is { } m && _ichKnopf.TranslatePoint(default, this) is { } i
            ? (new Rect(i, _ichKnopf.Bounds.Size), m.Symbole) : null;

    /// <summary>
    /// Gegenprobe Sammelliste 399 (Start-Test, je Seite wie <see cref="OrtslisteAltFuerTests"/>): Leiste links wie bis A 0.98.0
    /// (Handy-Knopf am linken Rand, dort wo der linke Daumen liegt).
    /// </summary>
    internal bool LeisteLinksAltFuerTests { get; set; }

    /// <summary>
    /// Welle A 0.99.0 (Sammelliste 399): „Spiel verlassen“ steht rechts neben dem Pfeil; passt er dort nicht (schmales Handy, grosse
    /// Schrift: die rechte Spalte ist halb so breit wie die Leiste ohne die zwei Symbole), steht er in einer eigenen Zeile ganz
    /// unten in der Mitte. Aufgeklappt ist die Leiste so breit wie moeglich (hoechstens <see cref="UntenBreite"/>), zugeklappt
    /// nur so breit wie ihr Inhalt; sie steht immer mittig.
    /// </summary>
    private void QuestZeileSetzen()
    {
        if (_klappZeile is not { } zeile || _questKnopf is null)
        {
            return;
        }
        _unten.HorizontalAlignment = LeisteLinksAltFuerTests ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        var hoechstens = Math.Min(_unten.MaxWidth, Ebene.Bounds.Width);
        // Welle A 1.13.0 (Sammelliste 464): ohne Pfeil gibt es kein Zuklappen mehr; mit der Handlung (Text oben) ist die Karte nur so
        // breit wie ihr Inhalt (Anzeige „F: …“), sonst so breit wie moeglich wie bisher aufgeklappt
        var breite = HinweisObenAn || hoechstens <= 0 ? double.NaN : hoechstens;
        if (!(double.IsNaN(breite) && double.IsNaN(_unten.Width)) && breite != _unten.Width)
        {
            _unten.Width = breite;
        }
        if (zeile.Bounds.Width <= 0)
        {
            return;
        }
        var skala = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        // Welle A 1.13.0: ohne Pfeil ist der Handy-Knopf die einzige Breite in der Mitte; eben erst sichtbar geworden ist er noch
        // nicht gemessen (DesiredSize 0, „Spiel verlassen“ stand ohne 3D am schmalen Handy daneben und ragte aus der Karte)
        if (_questKnopf.IsVisible && _questKnopf.DesiredSize.Width <= 0)
        {
            _questKnopf.Measure(Size.Infinity);
        }
        var mitte = _questKnopf.IsVisible ? _questKnopf.DesiredSize.Width : 0;
        var innen = hoechstens - _unten.Padding.Left - _unten.Padding.Right - _unten.BorderThickness.Left - _unten.BorderThickness.Right
            - zeile.Margin.Left - zeile.Margin.Right;
        // Breite ohne den eigenen Rand (der wechselt mit der Lage) auf Bildpunkte gerundet, plus Rand in der Zeile (4)
        // Breite in einer Zeile: die Spalte rechts ist schmaler als der Knopf (Stern), sein DesiredSize waere schon umgebrochen
        // (Lauf lokal A 0.99.0: „Spiel verlassen“ getrennt bei 384 Punkten Breite ab Schrift gross)
        // Welle A 1.0.0 (Sammelliste 402, Nico c): links neben dem Handy-Knopf steht „Ich-Sicht“, gleich breit wie „Spiel verlassen“
        // rechts neben dem Pfeil; so ist die Reihe um die zwei mittigen Symbole symmetrisch
        var ichDa = _ichKnopf.IsVisible && _ichHuelle.IsVisible;
        var verlassenBreite = !VerlassenKnopf.IsVisible ? 0 : (Math.Ceiling(EinzeiligBreite(VerlassenKnopf) * skala) / skala) + 4;
        var ichBreite = !ichDa ? 0 : (Math.Ceiling(EinzeiligBreite(_ichKnopf) * skala) / skala) + 4;
        var seiteBreite = Math.Max(verlassenBreite, ichBreite);
        var unten = seiteBreite > 0 && seiteBreite > (innen - mitte) / 2;
        // beide gleich breit (ohne den eigenen Rand von 4)
        var knopfBreite = seiteBreite > 0 ? seiteBreite - 4 : double.NaN;
        foreach (var k in new[] { VerlassenKnopf, _ichKnopf })
        {
            if (!(double.IsNaN(knopfBreite) && double.IsNaN(k.Width)) && k.Width != knopfBreite)
            {
                k.Width = knopfBreite;
            }
        }
        // Reihe unten: beide nebeneinander, die Luecke zwischen ihnen genau unter der Mitte der Leiste (= Mitte der zwei Symbole);
        // passen beide nicht nebeneinander (schmales Handy, grosse Schrift), steht „Ich-Sicht“ mittig ueber „Spiel verlassen“
        var gestapelt = unten && ichDa && (2 * seiteBreite) + 4 > innen;
        var versatz = double.IsNaN(knopfBreite) ? 0 : knopfBreite + 8;
        Grid.SetRow(VerlassenKnopf, gestapelt ? 4 : unten ? 3 : 2);
        Grid.SetColumn(VerlassenKnopf, unten ? 0 : 3);
        Grid.SetColumnSpan(VerlassenKnopf, unten ? 4 : 1);
        VerlassenKnopf.HorizontalAlignment = unten ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        VerlassenKnopf.Margin = gestapelt ? new Thickness(0, 0, 0, 6) : unten ? new Thickness(versatz, 6, 0, 6) : new Thickness(4, 0, 0, 6);
        Grid.SetRow(_ichHuelle, unten ? 3 : 2);
        Grid.SetColumn(_ichHuelle, 0);
        Grid.SetColumnSpan(_ichHuelle, unten ? 4 : 1);
        _ichHuelle.HorizontalAlignment = unten ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        _ichKnopf.Margin = gestapelt ? new Thickness(0, 6, 0, 6) : unten ? new Thickness(0, 6, versatz, 6) : new Thickness(0, 0, 4, 6);
        if (unten && !ichDa)
        {
            // ohne „Ich-Sicht“ (kein 3D, Einrichten) steht „Spiel verlassen“ allein unten in der Mitte wie bis A 0.99.0
            VerlassenKnopf.Margin = new Thickness(0, 6, 0, 6);
        }
    }

    /// <summary>
    /// Start-Test (Sammelliste 399): Mitte der Symbole (seit A 1.13.0 nur noch der Handy-Knopf, ohne Pfeil) und Mitte der Leiste,
    /// waagerecht in Punkten der Seite; null, solange nicht angeordnet.
    /// </summary>
    public (double Symbole, double Leiste, double Seite)? LeisteMitten()
    {
        if (_questKnopf is not { } q || q.TranslatePoint(default, this) is not { } a || q.TranslatePoint(new Point(q.Bounds.Width, 0), this) is not { } e
            || _unten.TranslatePoint(default, this) is not { } u || Bounds.Width <= 0)
        {
            return null;
        }
        return ((a.X + e.X) / 2, u.X + (_unten.Bounds.Width / 2), Bounds.Width / 2);
    }

    /// <summary>Start-Test (Sammelliste 399): die Leiste (Karte unten) in Punkten der Seite.</summary>
    public Rect? LeisteRechteck() => _unten.IsVisible && _unten.TranslatePoint(default, this) is { } p ? new Rect(p, _unten.Bounds.Size) : null;

    /// <summary>Breite eines Knopfs, wenn sein ganzer Text in einer Zeile steht (ohne den eigenen Rand).</summary>
    private static double EinzeiligBreite(Button knopf)
    {
        var breit = 0.0;
        foreach (var t in knopf.GetLogicalDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text)))
        {
            var mass = new TextBlock { Text = t.Text, FontFamily = t.FontFamily, FontSize = t.FontSize, FontWeight = t.FontWeight, FontStyle = t.FontStyle };
            mass.Measure(Size.Infinity);
            breit = Math.Max(breit, mass.DesiredSize.Width + t.Margin.Left + t.Margin.Right);
        }
        return breit + knopf.Padding.Left + knopf.Padding.Right + knopf.BorderThickness.Left + knopf.BorderThickness.Right + 1;
    }

    private double LaengstesWort(TextBlock t)
    {
        var skala = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var breit = 0.0;
        foreach (var wort in (t.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var mass = new TextBlock { Text = wort, FontFamily = t.FontFamily, FontSize = t.FontSize, FontWeight = t.FontWeight, FontStyle = t.FontStyle };
            mass.Measure(Size.Infinity);
            breit = Math.Max(breit, (Math.Ceiling(mass.DesiredSize.Width * skala) / skala) + 1);
        }
        return breit;
    }

    /// <summary>Breite des breitesten Knopfs, wenn nur sein laengstes Wort in einer Zeile steht (mit Rand, auf Bildpunkte gerundet).</summary>
    private double LaengsterKnopf()
    {
        var skala = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var breit = 0.0;
        foreach (var knopf in _knoepfe.GetLogicalDescendants().OfType<Button>().Where(b => b.IsVisible))
        {
            foreach (var t in knopf.GetLogicalDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text)))
            {
                foreach (var wort in t.Text!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var mass = new TextBlock { Text = wort, FontFamily = t.FontFamily, FontSize = t.FontSize, FontWeight = t.FontWeight, FontStyle = t.FontStyle };
                    mass.Measure(Size.Infinity);
                    var w = Math.Ceiling(mass.DesiredSize.Width * skala) / skala + knopf.Padding.Left + knopf.Padding.Right
                        + knopf.BorderThickness.Left + knopf.BorderThickness.Right + knopf.Margin.Left + knopf.Margin.Right + t.Margin.Left + t.Margin.Right + 1;
                    breit = Math.Max(breit, w);
                }
            }
        }
        return breit;
    }

    private static void ReiterSetzen(Button knopf, bool gewaehlt)
    {
        knopf.Farbe(Button.BackgroundProperty, gewaehlt ? "VerlaufW.primary" : "Pinsel.card_alt");
        knopf.BorderThickness = new Thickness(gewaehlt ? 0 : 1);
        if (knopf.Content is TextBlock t)
        {
            t.Farbe(TextBlock.ForegroundProperty, gewaehlt ? "Pinsel.on_accent" : "Pinsel.text");
            Avalonia.Automation.AutomationProperties.SetName(knopf, t.Text + (gewaehlt ? ", gewählt" : ""));
        }
    }
}
