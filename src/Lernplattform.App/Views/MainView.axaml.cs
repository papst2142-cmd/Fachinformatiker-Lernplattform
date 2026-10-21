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
    private readonly TextBlock _kopfTitel = Schrift.Text("", "Gross", FontWeight.SemiBold);
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

        var suche = Symbolknopf(Symbole.Suche, Seiten.Titel(Seiten.Suche), Seiten.Suche);
        var hilfe = Symbolknopf(Symbole.Hilfe, Seiten.Titel(Seiten.Hilfe), Seiten.Hilfe);
        var kopf = new DockPanel { Margin = new Thickness(16, 10, 8, 10) };
        var symbole = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { suche, hilfe } };
        DockPanel.SetDock(symbole, Dock.Right);
        kopf.Children.Add(symbole);
        _kopfTitel.VerticalAlignment = VerticalAlignment.Center;
        _kopfTitel.TextWrapping = TextWrapping.Wrap;
        kopf.Children.Add(_kopfTitel);
        var kopfleiste = new Border { Child = kopf, BorderThickness = new Thickness(0, 0, 0, 1) }
            .Farbe(Border.BackgroundProperty, "Pinsel.bg").Farbe(Border.BorderBrushProperty, "Pinsel.border");

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
        Content = new Panel { Children = { new Border { Child = raster }.Farbe(Border.BackgroundProperty, "Pinsel.bg"), ebene } };
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
        };
        // Strg+F wie am PC (root.bind("<Control-f>")): Suche zeigen, Feld fokussieren
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control) && Dialog.Offen is null)
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
            if (AutoModus)
            {
                Dispatcher.UIThread.Post(async () => await AutoLaufAsync(), DispatcherPriority.Background);
            }
        });
    }

    public bool Breit => _breit ?? true;

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
        _leisten.Markiere(AktuelleSeite);
    }

    private Button Symbolknopf(Geometry symbol, string name, string ziel)
    {
        var knopf = new Button
        {
            Content = new PathIcon { Data = symbol, Width = 20, Height = 20 }.Farbe(PathIcon.ForegroundProperty, "Pinsel.text"),
            Padding = new Thickness(10),
            Background = Brushes.Transparent,
        };
        ToolTip.SetTip(knopf, name);
        Avalonia.Automation.AutomationProperties.SetName(knopf, name);
        knopf.Click += async (_, _) => await ZeigeAsync(ziel);
        return knopf;
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
                Seiten.Optionen => new OptionenSeite(BaueSicherung),
                Seiten.Lernen => LernenSeite.Bauen(ziel => _ = ZeigeAsync(ziel), Programm.Zustand.Inhalte),
                Seiten.Karteikarten => new KarteikartenSeite(),
                Seiten.Trainer => new TrainerSeite(),
                Seiten.Fortschritt => new FortschrittSeite(JetztUebenAsync),
                Seiten.Suche => new SucheSeite((treffer, anfrage) => _ = TrefferOeffnenAsync(treffer, anfrage)),
                // Phase 3, W2: Rechner (der Trainer im Umschalter kommt mit Welle 3)
                Seiten.Rechner => new RechnerSeite(),
                // Phase 4, W1: Lernseiten
                Seiten.Ap1Szenarien => new SzenarioSeite(Core.Lernen.Quellen.Ap1),
                Seiten.Ap2Szenarien => new SzenarioSeite(Core.Lernen.Quellen.Ap2),
                Seiten.Testprojekt => new SzenarioSeite(Core.Lernen.Quellen.Projekt),
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
        if (_seiten[id] is SucheSeite suche)
        {
            suche.Fokus();
        }
        _kopfTitel.Text = Seiten.Titel(id);
        _leisten.Markiere(id);
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
        }
    }

    public async Task ZeigeAsync(string id)
    {
        var erstes = !_seiten.ContainsKey(id);
        var uhr = Stopwatch.StartNew();
        Zeige(id);
        await BildFertigAsync();
        var ms = uhr.Elapsed.TotalMilliseconds;
        if (erstes)
        {
            Messwerte.ErstesOeffnen[id] = ms;
        }
        else
        {
            Messwerte.Reihe("Seitenwechsel (gebaute Seite)", ms);
        }
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

    /// <summary>open_search_hit: Optionen (Bereich aufklappen), Karteikarte, Pruefungsfrage, Szenarien.</summary>
    private async Task TrefferOeffnenAsync(Core.Suche.SuchTreffer treffer, string anfrage)
    {
        switch (treffer.Art)
        {
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
        // Phase 2, L5: Lernstand übernehmen (Sicherung, Abgleich-Datei, alte Datenbank)
        panel.Children.Add(new UebernahmeTeil(panel).Teil);
        // Phase 2, L9: Sicherung für die alte App erstellen
        panel.Children.Add(new RuecksicherungTeil(panel).Teil);
        panel.Children.Add(Schrift.Text(Core.Texte.Beschriftungen.Platzhalter, "Normal", FontWeight.Normal, "Pinsel.text_dim"));

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
