using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Lernplattform.Core.Daten;
using Lernplattform.Design;
using Update = Lernplattform.Aktualisierung.Aktualisierung;
using Lernplattform.Messung;
using Lernplattform.ViewModels;

namespace Lernplattform.Views;

/// <summary>
/// Phase 1, K6: Seitengeruest fuer PC und Handy in einem Aufbau. Breites Fenster:
/// Seitenleiste wie am PC; schmales Fenster (Handy hoch): untere Leiste mit 6
/// Eintraegen und Sammelseite "Lernen" wie am Handy. Hilfe und Suche als Symbole
/// in der Kopfleiste. Die Seiten werden beim ersten Oeffnen gebaut und danach
/// wiederverwendet; ein Wechsel von breit auf schmal baut nichts neu.
/// </summary>
public partial class MainView : UserControl
{
    /// <summary>Ab dieser Breite (Punkte) gilt der PC-Aufbau mit Seitenleiste.</summary>
    public const double BreitAb = 720;

    private readonly ContentControl _inhalt = new();
    private readonly Dictionary<string, Control> _seiten = new(StringComparer.Ordinal);
    private readonly TextBox _ergebnis = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 160 };
    private readonly TextBlock _zustand = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Navigationsleisten _leisten;
    // A 0.12.2: Kopfzeile wie _appbar am Handy (Brotkrumen "BEREICH / SEITE", Version nur auf Start, Logo-Ring)
    private readonly TextBlock _krumeBereich = Schrift.Text("", "Klein", FontWeight.Bold);
    private readonly TextBlock _krumeSeite = Schrift.Text("", "Klein", FontWeight.Bold, "Pinsel.accent");
    private readonly TextBlock _versionZeile = Schrift.Text(Core.Texte.Beschriftungen.Einsetzen(Core.Texte.Navigation.Fusszeile, Update.Anzeige), "Winzig", FontWeight.Normal, "Pinsel.muted");
    private readonly Control _logo = Logo();
    // Ab 160 % im schmalen Aufbau stehen die Brotkrumen in einer eigenen Zeile unter Logo und Symbolen
    // (LARGE_FACTOR): neben Logo, Hilfe und Suche blieben bei 384 Punkten nur etwa 200 fuer ein Wort
    // wie "ABSCHLUSSPROJEKT"
    private readonly Panel _kopfUnten = new StackPanel();
    private DockPanel? _kopf;
    private StackPanel? _kopfTitel;
    private bool? _breit;

    public static bool AutoModus => Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSUNG_AUTO") == "1";

    public string AktuelleSeite { get; private set; } = Seiten.Dashboard;

    public MainView()
    {
        InitializeComponent();
        // Datenordner-Probe (Phase 0, Spike D) beim Start wie frueher mit der Startseite,
        // damit die Android-Update-Probe nicht erst das Oeffnen der Optionen braucht
        try
        {
            Ablage.UpdateProbe(Update.PaketVersion);
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
        _leisten = new Navigationsleisten(id => _ = ZeigeAsync(id));

        // Wie am Handy: Hilfe vor Suche, Symbole text_dim, rechts 6 Punkte Abstand
        var hilfe = Symbolknopf("HELP_OUTLINE_ROUNDED", Seiten.Titel(Seiten.Hilfe), Seiten.Hilfe);
        var suche = Symbolknopf("SEARCH_ROUNDED", Seiten.Titel(Seiten.Suche), Seiten.Suche);
        var kopf = new DockPanel { Margin = new Thickness(0, 0, 6, 0) };
        var symbole = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { hilfe, suche } };
        DockPanel.SetDock(symbole, Dock.Right);
        kopf.Children.Add(symbole);
        DockPanel.SetDock(_logo, Dock.Left);
        kopf.Children.Add(_logo);
        var trenner = Schrift.Text("/", "Klein", FontWeight.Normal, "Pinsel.muted");
        var krumen = new WrapPanel { ItemSpacing = 7, LineSpacing = 1, Children = { _krumeBereich, trenner, _krumeSeite } };
        _kopfTitel = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 6, 12, 6), Children = { krumen, _versionZeile } };
        _kopf = kopf;
        kopf.Children.Add(_kopfTitel);
        var kopfleiste = new Border { Child = new StackPanel { Children = { kopf, _kopfUnten } } }.Farbe(Border.BackgroundProperty, "Pinsel.bg");
        Schrift.Geaendert += KopfAnordnen;
        // Flutter-Kopfzeile 56 hoch, waechst mit der Schrift
        kopfleiste.Bind(MinHeightProperty, kopfleiste.GetResourceObservable("Schrift.Normal", v => v is double d ? 56 * d / Schrift.Grundgroessen["Normal"] : 56));

        var raster = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
        };
        Grid.SetRowSpan(_leisten.Seitenleiste, 3);
        raster.Children.Add(_leisten.Seitenleiste);
        Grid.SetColumn(kopfleiste, 1);
        raster.Children.Add(kopfleiste);
        Grid.SetColumn(_inhalt, 1);
        Grid.SetRow(_inhalt, 1);
        raster.Children.Add(_inhalt);
        Grid.SetColumn(_leisten.UntereLeiste, 1);
        Grid.SetRow(_leisten.UntereLeiste, 2);
        raster.Children.Add(_leisten.UntereLeiste);

        // Ebene fuer Hinweis- und Rueckfragefenster ueber allen Seiten (Phase 2)
        var ebene = new Panel { IsVisible = false };
        Dialog.Ebene(ebene);
        // W3: Ebene fuer Kurzhinweise (show_badge_toast) unter den Fenstern, laesst Klicks daneben durch
        var hinweise = new Panel { IsVisible = false };
        Kurzhinweis.Ebene(hinweise);
        // W4: Ebene fuer den Rundgang ueber allen Seiten, unter den Hinweisfenstern
        var rundgang = new Panel { IsVisible = false };
        RundgangEbene.Ebene(rundgang);
        Content = new Panel { Children = { new Border { Child = raster }.Farbe(Border.BackgroundProperty, "Pinsel.bg"), hinweise, rundgang, ebene } };
        Programm.Zustand.Fehlermeldung = text => Dispatcher.UIThread.Post(() => _ = Dialog.Zeigen(Core.Texte.KartenTexte.Datenbankfehler, text, Dialog.Ok));
        _inhalt.Farbe(ContentControl.ForegroundProperty, "Pinsel.text");
        Zeige(Seiten.Dashboard);
        // L10 (refresh_after_sync): nach einem Abgleich mit neuen oder entfernten Eintraegen die offene Seite neu rechnen
        Programm.AbgleichDienst.Fertig += (ergebnis, _) =>
        {
            if (ergebnis is { } e && (e.Erhalten > 0 || e.Entfernt > 0))
            {
                NachAbgleich();
            }
            // W4: Fachrichtung von einem anderen Geraet (Merker ausbildung_zuletzt, Frage 2 a)
            if (ergebnis is not null)
            {
                Dispatcher.UIThread.Post(async () => await AusbildungAnderswoAsync());
            }
        };
        // Strg+F wie am PC (root.bind("<Control-f>")): Suche zeigen, Feld fokussieren
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control) && Dialog.Offen is null && RundgangEbene.Offen is null)
            {
                e.Handled = true;
                _ = ZeigeAsync(Seiten.Suche);
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        SizeChanged += (_, e) => AufbauWaehlen(e.NewSize.Width);
        Schrift.Geaendert += SchriftGeaendert;
        DetachedFromVisualTree += (_, _) => Schrift.Geaendert -= SchriftGeaendert;
        AttachedToVisualTree += (_, _) => TopLevel.GetTopLevel(this)?.RequestAnimationFrame(_ =>
        {
            Messwerte.ErstesBild ??= Messwerte.SeitStart.Elapsed.TotalMilliseconds;
            // Phase 5 W2: Startzeile der Messung, Herzschlag, Antworten auf einen zweiten Start
            Programm.Diagnosedienst.Verbinden(this);
            // Phase 5 BE10: Pruefung des letzten Updates und Suche beim Start (einmal je Programmstart)
            Programm.UpdateDienst.Starten();
            if (AutoModus)
            {
                Dispatcher.UIThread.Post(async () => await AutoLaufAsync(), DispatcherPriority.Background);
            }
            else if (!_startGelaufen)
            {
                _startGelaufen = true;
                Dispatcher.UIThread.Post(async () => await StartAblaufAsync(), DispatcherPriority.Background);
            }
        });
    }

    public bool Breit => _breit ?? true;

    private bool _startGelaufen;

    /// <summary>
    /// W4, Plan 2.3: beim Start erst bestimmen, ob der Rundgang faellig ist (vor der Frage, die eine Zeile
    /// anlegt), dann Ausbildungsfrage (mit Warnhinweis) bzw. Vergleich des Merkers, dann der Rundgang.
    /// Bei nur lesbaren Einstellungen und im Start ohne Bedienung erscheint nichts von selbst.
    /// </summary>
    private async Task StartAblaufAsync()
    {
        try
        {
            var schritte = await Core.Hilfe.AusbildungFrage.Ablauf(Programm.Zustand.Einstellungen.NurAnzeige, AutoModus, () => Programm.Zustand.Werte,
                () => Task.Run(() =>
                {
                    var lernstand = Programm.Zustand.Lernstand;
                    var schreiber = Programm.Zustand.Schreiber;
                    return new Core.Hilfe.StartLage(Core.Hilfe.Einrichten.HatDaten(lernstand.Pfad), lernstand.NurLesenGrund is null,
                        Core.Pruefung.Ausbildung.Gewaehlt(schreiber), Core.Pruefung.Ausbildung.Gespeichert(schreiber));
                }),
                async () =>
                {
                    await AusbildungTeil.FrageAsync();
                    Neu();
                },
                AusbildungAnderswoAsync, RundgangAsync);
            // Phase 6 W2 (maybe_ask_shortcut): unter Linux im AppImage einmal fragen, 400 ms nach dem Rundgang, sonst nach 2,5 s
            if (Programm.VerknuepfungDienst.Unterstuetzt)
            {
                await Task.Delay(schritte.Rundgang ? 400 : 2500);
                await Programm.VerknuepfungDienst.StartFrageAsync();
            }
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
    }

    /// <summary>W4: Merker ausbildung_zuletzt vergleichen (Start, nach Abgleich, Uebernahme, Ruecksicherung).</summary>
    public async Task AusbildungAnderswoAsync()
    {
        await AusbildungTeil.VergleichenAsync();
        Neu();
    }

    /// <summary>Rundgang starten (Optionen, Hilfe, Start); nach dem Speichern zeigen Optionen und Dashboard die neuen Werte.</summary>
    public async Task RundgangAsync()
    {
        if (await RundgangEbene.Starten())
        {
            Neu();
        }
    }

    /// <summary>Optionen (Name, Rahmenplan, Fachrichtung) und Dashboard (Begruessung) neu zeigen.</summary>
    private void Neu()
    {
        if (_seiten.TryGetValue(Seiten.Optionen, out var optionen))
        {
            ((OptionenSeite)optionen).Gezeigt();
        }
        if (AktuelleSeite == Seiten.Dashboard && _seiten.TryGetValue(Seiten.Dashboard, out var dashboard))
        {
            ((DashboardSeite)dashboard).Neu();
        }
    }

    private void SchriftGeaendert()
    {
        if (Bounds.Width > 0)
        {
            AufbauWaehlen(Bounds.Width);
        }
    }

    /// <summary>
    /// Die Seitenleiste braucht mit groesserer Schrift mehr Platz: breit erst ab
    /// BreitAb x Schriftfaktor (wie Umbruchpunkte in em), darunter der Handy-Aufbau.
    /// </summary>
    public static double BreitGrenze => BreitAb * Schrift.Faktor;

    private void AufbauWaehlen(double breite)
    {
        var breit = breite >= BreitGrenze;
        if (_breit == breit)
        {
            return;
        }
        _breit = breit;
        _leisten.Seitenleiste.IsVisible = breit;
        _leisten.UntereLeiste.IsVisible = !breit;
        // Logo-Ring nur im schmalen Aufbau (breit steht der Programmname in der Seitenleiste)
        _logo.IsVisible = !breit;
        KopfAnordnen();
        _leisten.Markiere(AktuelleSeite);
        Brotkrumen(AktuelleSeite);
    }

    /// <summary>Brotkrumen neben den Symbolen oder (ab 160 % im schmalen Aufbau) in einer Zeile darunter.</summary>
    private void KopfAnordnen()
    {
        if (_kopf is null || _kopfTitel is null)
        {
            return;
        }
        var unten = _breit == false && Schrift.Gross;
        if (unten == (_kopfTitel.Parent == _kopfUnten))
        {
            return;
        }
        (_kopfTitel.Parent as Panel)?.Children.Remove(_kopfTitel);
        _kopfTitel.Margin = unten ? new Thickness(16, 0, 12, 8) : new Thickness(16, 6, 12, 6);
        (unten ? _kopfUnten.Children : _kopf.Children).Add(_kopfTitel);
    }

    /// <summary>Symbolknopf wie ft.IconButton (48 x 48, Symbol 24, text_dim).</summary>
    private Button Symbolknopf(string symbol, string name, string ziel)
    {
        var knopf = new Button
        {
            Content = Symbole.Block(symbol, 24, "Pinsel.text_dim"),
            Padding = new Thickness(12),
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(999),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(knopf, name);
        Avalonia.Automation.AutomationProperties.SetName(knopf, name);
        knopf.Click += async (_, _) => await ZeigeAsync(ziel);
        return knopf;
    }

    /// <summary>Logo wie am Handy: Ring 30 (Dicke 4, Verlauf accent nach accent2) mit Punkt accent2 (10), links 16 Abstand.</summary>
    private static Control Logo()
    {
        var ring = new VerlaufRing(1, "accent", "accent2", 4) { Width = 30, Height = 30 };
        var punkt = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            .Farbe(Border.BackgroundProperty, "Pinsel.accent2");
        var logo = new Grid { Width = 30, Height = 30, Margin = new Thickness(16, 0, 6, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Children = { ring, punkt } };
        Avalonia.Automation.AutomationProperties.SetAccessibilityView(logo, Avalonia.Automation.AccessibilityView.Raw);
        return logo;
    }

    /// <summary>Brotkrumen der Seite (Navigation.KopfPc bzw. KopfHandy); Versionszeile nur auf der Startseite.</summary>
    private void Brotkrumen(string id)
    {
        var breit = _breit ?? true;
        var titel = Core.Texte.Geraetetexte.Kopfzeilen(breit).FirstOrDefault(k => k.Id == id)
            ?? Core.Texte.Geraetetexte.Kopfzeilen(!breit).FirstOrDefault(k => k.Id == id);
        _krumeBereich.Text = titel?.Bereich ?? "";
        _krumeSeite.Text = titel?.Seite ?? Seiten.Titel(id);
        _versionZeile.IsVisible = id == Seiten.Dashboard;
    }

    /// <summary>Wartet, bis das naechste Bild fertig gezeichnet ist (zwei Bildaufrufe).</summary>
    public Task BildFertigAsync()
    {
        var fertig = new TaskCompletionSource();
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            fertig.SetResult();
            return fertig.Task;
        }
        top.RequestAnimationFrame(_ => top.RequestAnimationFrame(_ => fertig.TrySetResult()));
        return fertig.Task;
    }

    private Control Seite(string id)
    {
        if (!_seiten.TryGetValue(id, out var seite))
        {
            seite = id switch
            {
                Seiten.Dashboard => new DashboardSeite(() => _ = WiederholenAsync()),
                Seiten.Optionen => new OptionenSeite(BaueSicherung, () => _ = RundgangAsync(), () => _ = ZeigeAsync(Seiten.Hilfe)),
                // Phase 4, W4: Hilfe
                Seiten.Hilfe => new HilfeSeite(() => _ = RundgangAsync()),
                Seiten.Lernen => LernenSeite.Bauen(ziel => _ = ZeigeAsync(ziel), Programm.Zustand.Inhalte),
                Seiten.Karteikarten => new KarteikartenSeite(),
                Seiten.Trainer => new TrainerSeite(ThemaUebenAsync),
                Seiten.Fortschritt => new FortschrittSeite(JetztUebenAsync),
                Seiten.Suche => new SucheSeite((treffer, anfrage) => _ = TrefferOeffnenAsync(treffer, anfrage)),
                // Phase 3, W2: Rechner (der Trainer im Umschalter kommt mit Welle 3)
                Seiten.Rechner => new RechnerSeite(),
                // Phase 4, W1: Lernseiten
                Seiten.Ap1Szenarien => new SzenarioSeite(Core.Lernen.Quellen.Ap1),
                Seiten.Ap2Szenarien => new SzenarioSeite(Core.Lernen.Quellen.Ap2),
                Seiten.Testprojekt => new SzenarioSeite(Core.Lernen.Quellen.Projekt, vorlage => _ = AlsVorlageAsync(vorlage)),
                // Phase 4, W3: Abschlussprojekt
                Seiten.Abschluss => new AbschlussprojektSeite(),
                Seiten.Notizblock => new NotizblockSeite((quelle, schluessel, erste) => _ = NotizblockUebenAsync(quelle, schluessel, erste)),
                // Unterpunkte der Karteikarten (ein Fachbereich): dieselbe Seite wie "Karteikarten"
                _ when Core.Inhalte.Fachbereiche.Alle.Contains(id) => Seite(Seiten.Karteikarten),
                _ => Platzhalter.Bauen(Seiten.Titel(id)),
            };
            _seiten[id] = seite;
        }
        return seite;
    }

    private void Zeige(string id)
    {
        // W3: beim Verlassen der Seite "Abschlussprojekt" offene Eingaben speichern
        if (AktuelleSeite != id && _seiten.TryGetValue(AktuelleSeite, out var vorher) && vorher is AbschlussprojektSeite verlassen)
        {
            verlassen.Speichern();
        }
        AktuelleSeite = id;
        var gebaut = _seiten.ContainsKey(id);
        _inhalt.Content = Seite(id);
        if (gebaut && _seiten[id] is DashboardSeite dashboard)
        {
            dashboard.Neu();
        }
        if (_seiten[id] is KarteikartenSeite karten)
        {
            if (Core.Inhalte.Fachbereiche.Alle.Contains(id))
            {
                karten.SetzeFachbereich(id);      // open_cards(category)
            }
            else if (gebaut)
            {
                karten.Gezeigt();
            }
        }
        if (gebaut && _seiten[id] is TrainerSeite trainer)
        {
            trainer.Gezeigt();
        }
        if (gebaut && _seiten[id] is FortschrittSeite fortschritt)
        {
            fortschritt.Neu();
        }
        if (gebaut && _seiten[id] is SzenarioSeite szenarien)
        {
            szenarien.Gezeigt();
        }
        if (gebaut && _seiten[id] is NotizblockSeite notizblock)
        {
            notizblock.Gezeigt();
        }
        if (gebaut && _seiten[id] is AbschlussprojektSeite abschluss)
        {
            abschluss.Gezeigt();
        }
        if (gebaut && _seiten[id] is OptionenSeite optionen)
        {
            optionen.Gezeigt();
        }
        if (gebaut && _seiten[id] is HilfeSeite hilfe)
        {
            hilfe.Gezeigt();
        }
        if (_seiten[id] is SucheSeite suche)
        {
            suche.Fokus();
        }
        Brotkrumen(id);
        _leisten.Markiere(id);
        Core.Diagnose.Haenger.Seite(id);   // Phase 5 W2: Seite fuer die Haenger-Diagnose
    }

    private void NachAbgleich()
    {
        if (!_seiten.TryGetValue(AktuelleSeite, out var seite))
        {
            return;
        }
        switch (seite)
        {
            case DashboardSeite dashboard:
                dashboard.Neu();
                break;
            case FortschrittSeite fortschritt:
                fortschritt.Neu();
                break;
            case KarteikartenSeite karten:
                karten.Gezeigt();
                break;
            case TrainerSeite trainer:
                trainer.Gezeigt();
                break;
            case SzenarioSeite szenarien:
                szenarien.Gezeigt();
                break;
            case NotizblockSeite notizblock:
                notizblock.Gezeigt();
                break;
            case OptionenSeite optionen:
                optionen.Gezeigt();
                break;
            case AbschlussprojektSeite abschluss:
                abschluss.Gezeigt();
                break;
        }
    }

    public async Task ZeigeAsync(string id)
    {
        var erstes = !_seiten.ContainsKey(id);
        var vorher = AktuelleSeite;
        var uhr = Stopwatch.StartNew();
        // Phase 5 W2 (BE8): Vorgang fuer die Haenger-Diagnose, solange die Seite gebaut wird
        var vorgang = Core.Diagnose.Haenger.Vorgang("seite:" + id);
        try
        {
            Zeige(id);
        }
        finally
        {
            Core.Diagnose.Haenger.Vorgang(vorgang);
        }
        await BildFertigAsync();
        var ms = uhr.Elapsed.TotalMilliseconds;
        Programm.Diagnosedienst.Seite(vorher, id, ms);
        if (erstes)
        {
            Messwerte.ErstesOeffnen[id] = ms;
        }
        else
        {
            Messwerte.Reihe("Seitenwechsel (gebaute Seite)", ms);
        }
    }

    /// <summary>use_as_template (Seite Testprojekt): erst das Abschlussprojekt zeigen, dann uebernehmen.</summary>
    private async Task AlsVorlageAsync(Core.Inhalte.Lerninhalt vorlage)
    {
        await ZeigeAsync(Seiten.Abschluss);
        ((AbschlussprojektSeite)_seiten[Seiten.Abschluss]).Vorlage(vorlage);
    }

    /// <summary>
    /// "Jetzt wiederholen" (start_review): erst faellige Karteikarten, dann faellige
    /// Pruefungstrainer-Fragen; ohne faellige Aufgaben Hinweis "Wiederholung".
    /// </summary>
    private async Task WiederholenAsync()
    {
        if (await Programm.Zustand.Inhalte is not { } inhalte)
        {
            return;
        }
        var plan = Core.Lernen.Wiederholungsplan.Aus(Programm.Zustand.BuchLesen(), DateOnly.FromDateTime(DateTime.Now), inhalte);
        if (plan.ErsteQuelle() is not { } quelle)
        {
            await Dialog.Zeigen(Core.Texte.KartenTexte.WiederholungTitel, Core.Texte.KartenTexte.NichtsFaellig, Dialog.Ok);
            return;
        }
        var ziel = quelle == Core.Lernen.Quellen.Karte ? Seiten.Karteikarten : Seiten.Trainer;
        await ZeigeAsync(ziel);
        switch (_seiten[ziel])
        {
            case KarteikartenSeite karten:
                karten.Ueben(plan.Sitzung(quelle));
                break;
            case TrainerSeite trainer:
                trainer.Ueben(plan.Sitzung(quelle));
                break;
        }
    }

    /// <summary>
    /// "Jetzt üben" (practice_topic, practice_point, _practice_part): ohne Teile Hinweis; erster
    /// Teil Karteikarten -> Karteikarten ueben; erster Teil Pruefungsfragen -> Pruefungstrainer in
    /// fester Reihenfolge und nach der Runde Rueckfrage zum naechsten Teil.
    /// </summary>
    private async Task JetztUebenAsync(List<(string Quelle, List<string> Schluessel)> teile)
    {
        if (teile.Count == 0)
        {
            await Dialog.Zeigen(Core.Texte.FortschrittTexte.Ueben, Core.Texte.FortschrittTexte.UebenKeine, Dialog.Ok);
            return;
        }
        var (quelle, schluessel) = teile[0];
        var rest = teile.Skip(1).ToList();
        if (quelle == Core.Lernen.Quellen.Karte)
        {
            await ZeigeAsync(Seiten.Karteikarten);
            ((KarteikartenSeite)_seiten[Seiten.Karteikarten]).Ueben(schluessel);
            return;
        }
        await ZeigeAsync(Seiten.Trainer);
        var trainer = (TrainerSeite)_seiten[Seiten.Trainer];
        Func<Task>? nachher = null;
        if (rest.Count > 0)
        {
            nachher = async () =>
            {
                var (naechste, fragen) = rest[0];
                if (await Dialog.Zeigen(Core.Texte.FortschrittTexte.WeiterTitel, Core.Texte.FortschrittTexte.WeiterText(naechste, fragen.Count),
                    Core.Texte.TrainerTexte.Ja, Core.Texte.TrainerTexte.Nein) == 0)
                {
                    await JetztUebenAsync(rest);
                }
            };
        }
        trainer.Ueben(schluessel, reihenfolgeBehalten: true, nachher: nachher);
    }

    /// <summary>practice_topic aus "Nach Themen" der Pruefung (Phase 4 W2): wie im Fortschritt.</summary>
    private async Task ThemaUebenAsync(string name)
    {
        if (await FortschrittSeite.ThemaTeileAsync(name) is { } teile)
        {
            await JetztUebenAsync(teile);
        }
    }

    /// <summary>open_search_hit: Hilfe (Abschnitt aufklappen), Optionen (Bereich aufklappen), Karteikarte, Pruefungsfrage, Szenarien.</summary>
    private async Task TrefferOeffnenAsync(Core.Suche.SuchTreffer treffer, string anfrage)
    {
        switch (treffer.Art)
        {
            case Core.Hilfe.RundgangTexte.SucheArt:
                // open_section: Abschnitt mit diesem Titel aufklappen (W4)
                await ZeigeAsync(Seiten.Hilfe);
                if (Core.Hilfe.HilfeTexte.Abschnitte.FirstOrDefault(a => a.Titel == treffer.Titel) is { } abschnitt)
                {
                    ((HilfeSeite)_seiten[Seiten.Hilfe]).Oeffnen(abschnitt.Id);
                }
                break;
            case Core.Texte.Optionen.SucheArt:
                var (bereich, vorlagen) = Core.Suche.GlobaleSuche.Ziel(treffer.Titel, anfrage);
                if (bereich is not null)
                {
                    await ZeigeAsync(Seiten.Optionen);
                    ((OptionenSeite)_seiten[Seiten.Optionen]).OeffneBereich(bereich, vorlagen);
                }
                break;
            case Core.Texte.SucheTexte.ArtKarte:
                await ZeigeAsync(Seiten.Karteikarten);
                ((KarteikartenSeite)_seiten[Seiten.Karteikarten]).Springen(treffer.Titel);
                break;
            case Core.Texte.SucheTexte.ArtQuiz:
                await ZeigeAsync(Seiten.Trainer);
                ((TrainerSeite)_seiten[Seiten.Trainer]).Springen(treffer.Titel);
                break;
            case Core.Texte.SucheTexte.ArtAp1:
                // das erste Szenario mit gleichem Titel (Phase 4, W1)
                await ZeigeAsync(Seiten.Ap1Szenarien);
                ((SzenarioSeite)_seiten[Seiten.Ap1Szenarien]).Springen(treffer.Titel);
                break;
            default:
                await ZeigeAsync(Seiten.Ap2Szenarien);
                ((SzenarioSeite)_seiten[Seiten.Ap2Szenarien]).Springen(treffer.Titel);
                break;
        }
    }

    /// <summary>
    /// Notizblock "Üben"/"Jetzt üben" (NotebookView.practice): Karteikarten bzw. Pruefungstrainer
    /// mit den Fragen der Liste ("Jetzt üben": diese Frage zuerst, Reihenfolge bleibt), Szenarien
    /// und Testprojekte oeffnen den ersten Eintrag.
    /// </summary>
    private async Task NotizblockUebenAsync(string quelle, List<object> schluessel, object? erste)
    {
        switch (quelle)
        {
            case Core.Lernen.Quellen.Karte:
                await ZeigeAsync(Seiten.Karteikarten);
                ((KarteikartenSeite)_seiten[Seiten.Karteikarten]).Ueben(schluessel.Cast<string>());
                break;
            case Core.Lernen.Quellen.Quiz:
                await ZeigeAsync(Seiten.Trainer);
                ((TrainerSeite)_seiten[Seiten.Trainer]).Ueben(schluessel.Cast<string>(), reihenfolgeBehalten: erste is not null);
                break;
            default:
                var ziel = quelle switch
                {
                    Core.Lernen.Quellen.Ap1 => Seiten.Ap1Szenarien,
                    Core.Lernen.Quellen.Ap2 => Seiten.Ap2Szenarien,
                    _ => Seiten.Testprojekt,
                };
                await ZeigeAsync(ziel);
                ((SzenarioSeite)_seiten[ziel]).Oeffnen((int)Convert.ToInt64(schluessel[0], CultureInfo.InvariantCulture));
                break;
        }
    }

    private static TextBlock Text(string text, double groesse = 14, FontWeight gewicht = FontWeight.Normal) =>
        new() { Text = text, FontSize = groesse, FontWeight = gewicht, TextWrapping = TextWrapping.Wrap };

    /// <summary>
    /// Phase 1, K9: "Alte Sicherung pruefen" und "Ergebnis kopieren" wie auf der frueheren
    /// Startseite, jetzt in Optionen &gt; Sicherung (Nico, Frage 4a). Knoepfe, Texte und die
    /// Pruefung selbst (SicherungPruefenAsync, SicherungsPruefung) sind unveraendert.
    /// </summary>
    private Control BaueSicherung()
    {
        var panel = new StackPanel { Spacing = 8 };
        // Phase 5 BE1/BE3 (_build_sicherung): Hilfe, dann [Sicherung erstellen] [Sicherung einspielen]
        panel.Children.Add(Schrift.Text(Core.Texte.SicherungTexte.Hilfe + " " + Core.Texte.SicherungTexte.HilfeAlteApp,
            "Normal", FontWeight.Normal, "Pinsel.text_dim"));
        var zustand = Schrift.Text("", "Normal", FontWeight.Normal, "Pinsel.text_dim");
        zustand.IsVisible = false;
        var erstellen = new RuecksicherungTeil(panel, zustand);
        var einspielen = new EinspielenTeil(panel, zustand, NachAbgleich);
        panel.Children.Add(new WrapPanel { Margin = new Thickness(0, 4, 0, 0), Children = { erstellen.Knopf, einspielen.Knopf } });
        panel.Children.Add(zustand);
        // Phase 2, L5: Lernstand übernehmen (Sicherung, Abgleich-Datei, alte Datenbank)
        panel.Children.Add(new UebernahmeTeil(panel).Teil);

        var kopieren = new Button { Content = "Ergebnis kopieren" };
        kopieren.Farbe(Button.BackgroundProperty, "Pinsel.card_alt").Farbe(Button.ForegroundProperty, "Pinsel.text");
        kopieren.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } ablage)
            {
                await ablage.SetTextAsync(_ergebnis.Text ?? "");
                _zustand.Text = "Ergebnis kopiert.";
            }
        };
        var sicherung = new Button { Content = "Alte Sicherung pruefen" };
        sicherung.Farbe(Button.BackgroundProperty, "Pinsel.card_alt").Farbe(Button.ForegroundProperty, "Pinsel.text");
        sicherung.Click += async (_, _) => await SicherungPruefenAsync();
        panel.Children.Add(new WrapPanel { Children = { sicherung, kopieren } });
        panel.Children.Add(Text("\"Alte Sicherung pruefen\" liest eine Kopie der Sicherung der alten App (.fisisicherung) nur lesend. Die Datei bleibt auf diesem Geraet; im Ergebnis stehen nur Zaehlwerte und Pruefsummen, keine Inhalte, kein Dateiname."));
        panel.Children.Add(Text("Linux: Zeigt das Fenster Bildfehler (z. B. ueber eine VNC-Konsole), das Programm mit --software starten (oder LERNPLATTFORM_SOFTWARE=1). Haengt das Tippen, mit --ohne-eingabemethode starten (oder LERNPLATTFORM_IME=0)."));
        panel.Children.Add(_zustand);
        _ergebnis.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card");
        panel.Children.Add(_ergebnis);
        _ergebnis.Text = BerichtText();
        return panel;
    }

    /// <summary>Alle schon gebauten Seiten (fuer die Layoutpruefung im Start ohne Bedienung).</summary>
    public IReadOnlyDictionary<string, Control> GebauteSeiten => _seiten;

    /// <summary>
    /// Weisung P1-3: Zustand und Ergebnisfeld der Sicherung haengen genau in der
    /// gebauten Optionen-Seite (BaueSicherung laeuft nur einmal, die Seite bleibt im Speicher).
    /// </summary>
    public bool SicherungInOptionen(Control optionen) =>
        _zustand.GetLogicalAncestors().Contains(optionen) && _ergebnis.GetLogicalAncestors().Contains(optionen);

    private string Geraet()
    {
        var top = TopLevel.GetTopLevel(this);
        return top is null ? "" : string.Create(CultureInfo.InvariantCulture,
            $"{top.Bounds.Width:F0}x{top.Bounds.Height:F0} Punkte, Skalierung {top.RenderScaling:F2}");
    }


    private string BerichtText()
    {
        string probe;
        try
        {
            probe = Ablage.UpdateProbe(Update.PaketVersion);
        }
        catch (Exception fehler)
        {
            probe = "nicht lesbar (" + fehler.GetType().Name + ")";
        }
        return Messwerte.Bericht(Geraet())
            + "Version: " + Update.Anzeige + " (Paketversion " + Update.PaketVersion + ")" + Environment.NewLine
            + "Updater: " + (Update.Zustand?.Invoke() ?? "keiner (Android: Update ueber neue APK)") + Environment.NewLine
            + "Datenordner-Probe: " + probe + Environment.NewLine;
    }


    /// <summary>
    /// Spike C mit echtem Lernstand (Auflage A3, Weisung CC P0-3): die Datei wird nur
    /// in den Speicher gelesen; ausgegeben wird nur, was SicherungsPruefung liefert.
    /// Kein Dateiname, kein Pfad, kein Fehlertext.
    /// </summary>
    private async Task SicherungPruefenAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }
        var auswahl = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Kopie der alten Sicherung (.fisisicherung) waehlen",
            AllowMultiple = false,
        });
        if (auswahl.Count == 0)
        {
            return;
        }
        IReadOnlyList<string> zeilen;
        try
        {
            using var puffer = new MemoryStream();
            await using (var lesen = await auswahl[0].OpenReadAsync())
            {
                await lesen.CopyToAsync(puffer);
            }
            zeilen = SicherungsPruefung.Pruefe(puffer.ToArray());
        }
        catch (Exception)
        {
            zeilen = ["FEHLER C0 (Datei nicht lesbar)"];
        }
        Messwerte.Notiere("ALTE SICHERUNG (nur Zaehlwerte):");
        foreach (var z in zeilen)
        {
            Messwerte.Notiere("  " + z);
        }
        _zustand.Text = "Alte Sicherung geprueft. Jetzt \"Ergebnis kopieren\".";
        _ergebnis.Text = BerichtText();
    }

    /// <summary>Start ohne Bedienung (Umgebungsvariable LERNPLATTFORM_MESSUNG_AUTO=1), fuer Xvfb und CI.</summary>
    private async Task AutoLaufAsync()
    {
        int fehler;
        try
        {
            fehler = await AutoMessung.LaufAsync(this);
        }
        catch (Exception ausnahme)
        {
            // Absturz im Start ohne Bedienung: festhalten und rot beenden statt haengen
            Core.Diagnose.Fehlerprotokoll.Schreiben(ausnahme);
            Messwerte.Notiere("URTEIL: rot (Ausnahme " + ausnahme.GetType().Name + ": " + ausnahme.Message + ")");
            fehler = 1;
        }
        _ergebnis.Text = BerichtText();
        var datei = Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSUNG_DATEI") is { Length: > 0 } d ? d : "messung_auto.txt";
        await File.WriteAllTextAsync(datei, _ergebnis.Text);
        Console.WriteLine(_ergebnis.Text);
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(fehler == 0 ? 0 : 3);
        }
    }
}
