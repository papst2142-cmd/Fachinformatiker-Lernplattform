using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Spiel.Ton;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;
using Lernplattform.Spiel;

namespace Lernplattform.Views;

/// <summary>
/// Phase 8 W8 (Weisungen P8-15 bis P8-17b): Karten "Musik und Geräusche" und "Tastenbelegung" (SPIEL) und
/// "Vollbild" (AUSSEHEN), am PC und am Handy gleich (Nico 3a, 4b). Gespeichert ueber Einstellungen.Aendern
/// (Zustand.Aendere); Fehler zeigen wie die anderen Karten "Speichern fehlgeschlagen".
/// </summary>
public sealed partial class OptionenSeite
{
    /// <summary>Start-Test: die Regler, die Liedliste und die Knoepfe der Tastenbelegung.</summary>
    public IReadOnlyDictionary<string, Slider> TonRegler => _tonRegler;

    private readonly Dictionary<string, Slider> _tonRegler = new(StringComparer.Ordinal);
    private StackPanel? _liedListe;
    private readonly Dictionary<string, RadioButton> _orte = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _tastenKnoepfe = new(StringComparer.Ordinal);
    private TextBlock? _tastenMeldung;
    private string? _wartetAuf;
    private ToggleSwitch? _vollbild;
    private DispatcherTimer? _tonSpeichern;

    public IReadOnlyDictionary<string, Button> TastenKnoepfe => _tastenKnoepfe;

    public TextBlock? TastenMeldung => _tastenMeldung;

    public ToggleSwitch? VollbildSchalter => _vollbild;

    public IReadOnlyDictionary<string, RadioButton> LiedOrte => _orte;

    public StackPanel? LiedListe => _liedListe;

    private Control Ton()
    {
        var panel = new StackPanel { Spacing = 8 };
        var werte = TonEinstellungen.Lesen(Zustand.Werte);
        foreach (var (schluessel, name, wert) in new[]
                 {
                     (TonEinstellungen.Gesamt, TonTexte.Gesamt, werte.Gesamt),
                     (TonEinstellungen.Musik, TonTexte.Musik, werte.Musik),
                     (TonEinstellungen.Umgebung, TonTexte.Umgebung, werte.Umgebung),
                 })
        {
            panel.Children.Add(Regler(schluessel, name, wert));
        }
        panel.Children.Add(new LernText(TonTexte.ReglerSatz, "Klein", FontWeight.Normal, "Pinsel.muted"));

        panel.Children.Add(Abschnitt(TonTexte.Lieder));
        panel.Children.Add(new LernText(TonTexte.LiederSatz, "Klein", FontWeight.Normal, "Pinsel.text_dim"));
        _liedListe = new StackPanel { Spacing = 4 };
        panel.Children.Add(_liedListe);
        LiederNeu();
        var hinzu = Schrift.Knopf(TonTexte.LiedHinzufuegen);
        hinzu.HorizontalAlignment = HorizontalAlignment.Left;
        hinzu.Click += async (_, _) => await LiedHinzufuegenAsync();
        panel.Children.Add(hinzu);

        panel.Children.Add(Schrift.Text(TonTexte.OrteTitel, "Normal", FontWeight.Bold, "Pinsel.text"));
        var orte = new StackPanel { Spacing = 2 };
        foreach (var wert in TonEinstellungen.Orte)
        {
            var text = TonTexte.Ort(wert);
            var knopf = new RadioButton
            {
                Content = new LernText(text, "Normal", FontWeight.Normal, "Pinsel.text"),
                GroupName = "ton_musik_orte",
                IsChecked = werte.MusikOrte == wert,
            };
            knopf.Farbe(RadioButton.ForegroundProperty, "Pinsel.text");
            Avalonia.Automation.AutomationProperties.SetName(knopf, text);
            knopf.IsCheckedChanged += (_, _) =>
            {
                if (_setzen || knopf.IsChecked != true)
                {
                    return;
                }
                if (Ergebnis(Zustand.Aendere(w => TonEinstellungen.OrteWert(w, wert))))
                {
                    TonDienst.Regler(TonEinstellungen.Lesen(Zustand.Werte));
                }
                else
                {
                    OrteNeu();
                }
            };
            _orte[wert] = knopf;
            orte.Children.Add(knopf);
        }
        panel.Children.Add(orte);
        return panel;
    }

    private void OrteNeu()
    {
        var orte = TonEinstellungen.Lesen(Zustand.Werte).MusikOrte;
        _setzen = true;
        foreach (var (wert, knopf) in _orte)
        {
            knopf.IsChecked = wert == orte;
        }
        _setzen = false;
    }

    private Control Regler(string schluessel, string name, int wert)
    {
        var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        var titel = Schrift.Text(name);
        titel.VerticalAlignment = VerticalAlignment.Center;
        titel.MinWidth = 90 * Schrift.Faktor;
        var regler = new Slider
        {
            Minimum = 0, Maximum = 100, Value = wert, SmallChange = 5, LargeChange = 10,
            TickFrequency = 5, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center,
        };
        Avalonia.Automation.AutomationProperties.SetName(regler, name);
        if (Application.Current!.TryFindResource("Pinsel." + Optionen.Farbe(Optionen.Ton), out var farbe))
        {
            foreach (var n in (string[])["SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed",
                         "SliderThumbBackground", "SliderThumbBackgroundPointerOver", "SliderThumbBackgroundPressed"])
            {
                regler.Resources[n] = farbe;
            }
        }
        var zahl = Schrift.Text(string.Format(TonTexte.Prozent, wert), "Normal", FontWeight.Bold, "Pinsel.text");
        zahl.VerticalAlignment = VerticalAlignment.Center;
        zahl.MinWidth = 48 * Schrift.Faktor;
        zahl.TextAlignment = TextAlignment.Right;
        regler.ValueChanged += (_, e) =>
        {
            zahl.Text = string.Format(TonTexte.Prozent, (int)e.NewValue);
            if (_setzen)
            {
                return;
            }
            // die Lautstaerke folgt sofort, geschrieben wird erst, wenn der Regler ruht (nicht jeder Zwischenwert)
            TonDienst.Regler(Aktuell());
            // Phase 8 W9: Hoerprobe in der neuen Lautstaerke (Weisung P8-29)
            if (Hoerprobe.Fuer(schluessel) is { } probe)
            {
                TonDienst.Hoerprobe(probe);
            }
            _tonSpeichern ??= new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => TonSpeichern());
            _tonSpeichern.Stop();
            _tonSpeichern.Start();
        };
        _tonRegler[schluessel] = regler;
        Grid.SetColumn(regler, 1);
        Grid.SetColumn(zahl, 2);
        zeile.Children.Add(titel);
        zeile.Children.Add(regler);
        zeile.Children.Add(zahl);
        return zeile;
    }

    private TonWerte Aktuell()
    {
        int Wert(string s, int vorgabe) => _tonRegler.TryGetValue(s, out var r) ? (int)Math.Round(r.Value) : vorgabe;
        var gespeichert = TonEinstellungen.Lesen(Zustand.Werte);
        return gespeichert with
        {
            Gesamt = Wert(TonEinstellungen.Gesamt, gespeichert.Gesamt),
            Musik = Wert(TonEinstellungen.Musik, gespeichert.Musik),
            Umgebung = Wert(TonEinstellungen.Umgebung, gespeichert.Umgebung),
        };
    }

    /// <summary>Regler speichern (auch sofort fuer den Start-Test).</summary>
    public bool TonSpeichern()
    {
        _tonSpeichern?.Stop();
        var werte = Aktuell();
        return Ergebnis(Zustand.Aendere(w =>
        {
            TonEinstellungen.Wert(w, TonEinstellungen.Gesamt, werte.Gesamt);
            TonEinstellungen.Wert(w, TonEinstellungen.Musik, werte.Musik);
            TonEinstellungen.Wert(w, TonEinstellungen.Umgebung, werte.Umgebung);
        }));
    }

    /// <summary>Liste der eigenen Lieder mit "Entfernen" neu aufbauen.</summary>
    public void LiederNeu()
    {
        if (_liedListe is null)
        {
            return;
        }
        _liedListe.Children.Clear();
        var lieder = TonDienst.Lieder?.Liste() ?? [];
        if (lieder.Count == 0)
        {
            _liedListe.Children.Add(Schrift.Text(TonTexte.KeineLieder, "Klein", FontWeight.Normal, "Pinsel.muted"));
            return;
        }
        foreach (var name in lieder)
        {
            var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
            var text = new WortText(name, "Normal", FontWeight.Normal, "Pinsel.text") { VerticalAlignment = VerticalAlignment.Center };
            var weg = Schrift.Knopf(TonTexte.Entfernen);
            Avalonia.Automation.AutomationProperties.SetName(weg, TonTexte.Entfernen + " " + name);
            weg.Click += async (_, _) => await LiedEntfernenAsync(name);
            Grid.SetColumn(weg, 1);
            zeile.Children.Add(text);
            zeile.Children.Add(weg);
            _liedListe.Children.Add(zeile);
        }
    }

    private async Task LiedHinzufuegenAsync()
    {
        if (TopLevel.GetTopLevel(this) is not { } top || TonDienst.Lieder is not { } lieder)
        {
            return;
        }
        // Endungen fuer PC, Bauart (MIME) fuer Android, Kennungen fuer macOS: dieselbe Auswahl auf jedem Geraet
        var optionen = new FilePickerOpenOptions
        {
            Title = TonTexte.LiedWaehlen,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(TonTexte.Dateiart)
            {
                Patterns = [.. Tondekoder.Endungen.Select(e => "*" + e)],
                MimeTypes = ["audio/*"],
                AppleUniformTypeIdentifiers = ["public.audio"],
            }],
        };
        var auswahl = await top.StorageProvider.OpenFilePickerAsync(optionen);
        if (auswahl.Count == 0)
        {
            return;
        }
        try
        {
            var name = auswahl[0].Name;
            await using var lesen = await auswahl[0].OpenReadAsync();
            await Task.Run(() => lieder.Hinzufuegen(lesen, name));
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben("Ton: Lied nicht hinzugefügt: " + fehler.GetType().Name);
            await Dialog.Zeigen(TonTexte.LiedHinzufuegen, TonTexte.NichtHinzugefuegt, Dialog.Ok);
            return;
        }
        LiederNeu();
        TonDienst.LiederGeaendert();
    }

    private async Task LiedEntfernenAsync(string name)
    {
        if (await Dialog.Zeigen(TonTexte.EntfernenTitel, string.Format(TonTexte.EntfernenFrage, name), TrainerTexte.Ja, TrainerTexte.Nein) != 0)
        {
            return;
        }
        try
        {
            TonDienst.Lieder?.Entfernen(name);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben("Ton: Lied nicht entfernt: " + fehler.GetType().Name);
        }
        LiederNeu();
        TonDienst.LiederGeaendert();
    }

    // -- Vollbild -------------------------------------------------------------------------------

    private Control VollbildKarte()
    {
        var panel = new StackPanel { Spacing = 6 };
        var schalter = new ToggleSwitch { Content = Schrift.Text(TonTexte.Vollbild), IsChecked = Vollbild.Dauerhaft, OnContent = null, OffContent = null };
        Avalonia.Automation.AutomationProperties.SetName(schalter, TonTexte.Vollbild);
        schalter.IsCheckedChanged += (_, _) =>
        {
            if (_setzen)
            {
                return;
            }
            var an = schalter.IsChecked == true;
            if (Ergebnis(Zustand.Aendere(w => VollbildWahl.Wert(w, an))))
            {
                Vollbild.DauerhaftSetzen(an);
            }
            else
            {
                VollbildNeu();
            }
        };
        void Geaendert() => Dispatcher.UIThread.Post(VollbildNeu);
        schalter.AttachedToVisualTree += (_, _) => Vollbild.Geaendert += Geaendert;
        schalter.DetachedFromVisualTree += (_, _) => Vollbild.Geaendert -= Geaendert;
        _vollbild = schalter;
        panel.Children.Add(schalter);
        panel.Children.Add(new LernText(TonTexte.VollbildSatz, "Klein", FontWeight.Normal, "Pinsel.muted"));
        return panel;
    }

    private void VollbildNeu()
    {
        if (_vollbild is null)
        {
            return;
        }
        _setzen = true;
        _vollbild.IsChecked = Vollbild.Dauerhaft;
        _setzen = false;
    }

    // -- Tastenbelegung -------------------------------------------------------------------------

    private Control Tasten()
    {
        var panel = new StackPanel { Spacing = 6 };
        foreach (var (aufgabe, text, _) in Tastenbelegung.Aufgaben)
        {
            var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
            var titel = new LernText(text, "Normal", FontWeight.Normal, "Pinsel.text") { VerticalAlignment = VerticalAlignment.Center };
            var knopf = Schrift.Knopf(Tastenbelegung.Aktuell.AnzeigeFuer(aufgabe));
            knopf.MinWidth = 150 * Schrift.Faktor;
            Avalonia.Automation.AutomationProperties.SetName(knopf, text);
            knopf.Click += (_, _) => TasteWarten(aufgabe);
            knopf.AddHandler(KeyDownEvent, (_, e) => TasteGedrueckt(aufgabe, e), RoutingStrategies.Tunnel);
            knopf.LostFocus += (_, _) =>
            {
                if (_wartetAuf == aufgabe)
                {
                    TastenWartenEnde();
                }
            };
            _tastenKnoepfe[aufgabe] = knopf;
            Grid.SetColumn(knopf, 1);
            zeile.Children.Add(titel);
            zeile.Children.Add(knopf);
            panel.Children.Add(zeile);
        }
        _tastenMeldung = new LernText("", "Klein", FontWeight.Normal, "Pinsel.red") { IsVisible = false };
        panel.Children.Add(_tastenMeldung);
        panel.Children.Add(new LernText(TonTexte.TastenSatz, "Klein", FontWeight.Normal, "Pinsel.muted"));
        var standard = Schrift.Knopf(TonTexte.Standard);
        standard.HorizontalAlignment = HorizontalAlignment.Left;
        standard.Click += (_, _) => TastenStandard();
        panel.Children.Add(standard);
        return panel;
    }

    /// <summary>Knopf einer Aufgabe gedrueckt: wartet auf die naechste Taste (auch fuer den Start-Test).</summary>
    public void TasteWarten(string aufgabe)
    {
        TastenWartenEnde();
        _wartetAuf = aufgabe;
        Vollbild.TasteWartet = true;
        if (_tastenKnoepfe.TryGetValue(aufgabe, out var knopf))
        {
            KnopfText(knopf, TonTexte.TasteDruecken);
            knopf.Focus();
        }
    }

    private void TasteGedrueckt(string aufgabe, KeyEventArgs e)
    {
        if (_wartetAuf != aufgabe)
        {
            return;
        }
        e.Handled = true;
        TasteNehmen(e.Key.ToString());
    }

    /// <summary>Die gedrueckte Taste uebernehmen (Esc bricht ab); Rueckgabe wie Tastenbelegung.Setzen.</summary>
    public Tastenbelegung.Ergebnis? TasteNehmen(string taste)
    {
        if (_wartetAuf is not { } aufgabe)
        {
            return null;
        }
        if (taste == Tastenbelegung.Abbrechen)
        {
            TastenWartenEnde();
            return null;
        }
        var neu = Tastenbelegung.Aktuell.Kopie();
        var ergebnis = neu.Setzen(aufgabe, taste, out var andere);
        if (ergebnis == Tastenbelegung.Ergebnis.Ok)
        {
            if (!Ergebnis(Zustand.Aendere(neu.Schreiben)))
            {
                TastenWartenEnde();
                return ergebnis;
            }
            Tastenbelegung.Aktuell = neu;
            Meldung(null);
        }
        else
        {
            Meldung(ergebnis == Tastenbelegung.Ergebnis.Doppelt
                ? Tastenbelegung.DoppeltText(taste, andere!)
                : string.Format(TonTexte.Gesperrt, Tastenbelegung.Anzeige(taste)));
        }
        TastenWartenEnde();
        return ergebnis;
    }

    private void Meldung(string? text)
    {
        if (_tastenMeldung is null)
        {
            return;
        }
        _tastenMeldung.Text = text ?? "";
        _tastenMeldung.IsVisible = text is not null;
    }

    private void TastenWartenEnde()
    {
        _wartetAuf = null;
        Vollbild.TasteWartet = false;
        foreach (var (aufgabe, knopf) in _tastenKnoepfe)
        {
            KnopfText(knopf, Tastenbelegung.Aktuell.AnzeigeFuer(aufgabe));
        }
    }

    /// <summary>"Standard wiederherstellen" (auch fuer den Start-Test).</summary>
    public bool TastenStandard()
    {
        var neu = new Tastenbelegung();
        if (!Ergebnis(Zustand.Aendere(neu.Schreiben)))
        {
            return false;
        }
        Tastenbelegung.Aktuell = neu;
        Meldung(null);
        TastenWartenEnde();
        return true;
    }

    private static void KnopfText(Button knopf, string text)
    {
        if (knopf.Content is TextBlock block)
        {
            block.Text = text;
        }
        else if (knopf.Content is Decorator { Child: TextBlock innen })
        {
            innen.Text = text;
        }
        else
        {
            knopf.Content = text;
        }
    }
}
