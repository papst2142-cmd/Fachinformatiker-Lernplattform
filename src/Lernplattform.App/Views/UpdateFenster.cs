using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Hilfe;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Update;
using Lernplattform.Design;
using Lernplattform.Programm;
using Aktuell = Lernplattform.Aktualisierung.Aktualisierung;

namespace Lernplattform.Views;

/// <summary>
/// Phase 5 BE10: Fenster "Update verfügbar" wie UpdateDialog (app_gui 9346) bzw. show_update_dialog am Handy
/// (main.py 6128), ein Aufbau fuer PC und Handy: installierte Version, Neuerungen (plain_notes), Zeile fuer
/// den Zustand, Hinweis zum Schutzprogramm. PC: "Jetzt aktualisieren" (Fortschritt, dann Neustart) bzw.
/// "Zur Download-Seite", dazu "Später". Handy: "Herunterladen" (Browser) und "Später".
/// </summary>
public sealed class UpdateFenster
{
    private readonly bool Pc;
    private readonly UpdateAngebot _angebot;
    private readonly bool _installierbar;
    private readonly TextBlock _zustand;
    private readonly ProgressBar _balken;
    private IReadOnlyList<Button> _knoepfe = [];
    private bool _laeuft;
    private long _geladen;
    private long _gesamt = 1;

    public Control Inhalt { get; }

    /// <param name="pc">nur fuer den Start ohne Bedienung (Ansicht des Handys am PC); sonst das Geraet.</param>
    public UpdateFenster(UpdateAngebot angebot, bool installierbar, bool? pc = null)
    {
        Pc = pc ?? UpdateDienst.Pc;
        _angebot = angebot;
        _installierbar = installierbar;
        var neuerungen = new TextBox
        {
            Text = UpdateNotizen.Lesbar(angebot.Notizen) is { Length: > 0 } text ? text : UpdateTexte.KeineBeschreibung,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            MaxHeight = 180,
        }.Farbe(TextBox.ForegroundProperty, "Pinsel.text_dim").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt");
        Avalonia.Automation.AutomationProperties.SetName(neuerungen, UpdateTexte.Neuerungen);
        var farbe = Pc ? "Pinsel.text_dim" : "Pinsel.accent";
        _zustand = Schrift.Text(Geraetetexte.UpdateHinweis(installierbar, angebot.DateiUrl is not null, Pc), "Klein", FontWeight.Normal, farbe);
        _zustand.IsVisible = !string.IsNullOrEmpty(_zustand.Text);
        _balken = new ProgressBar { Minimum = 0, Maximum = 1, Height = 10, IsVisible = false }
            .Farbe(ProgressBar.ForegroundProperty, "Pinsel.purple").Farbe(ProgressBar.BackgroundProperty, "Pinsel.ring_bg");
        Inhalt = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Schrift.Text(Beschriftungen.Einsetzen(UpdateTexte.Installiertes, Aktuell.Anzeige), "Klein", FontWeight.Normal, "Pinsel.muted"),
                Schrift.Text(Beschriftungen.Einsetzen(UpdateTexte.Ueberschrift, angebot.Version), "Mittel", FontWeight.Bold),
                Schrift.Text(Beschriftungen.Gross(UpdateTexte.Neuerungen), "Klein", FontWeight.Bold, "Pinsel.muted"),
                neuerungen,
                _zustand,
                _balken,
                Schrift.Text(RundgangTexte.UpdateSchutzHinweis, "Klein", FontWeight.Normal, "Pinsel.muted"),
            },
        };
    }

    public async Task ZeigenAsync()
    {
        try
        {
            await Dialog.Zeigen(UpdateTexte.FensterTitel, Inhalt, knoepfe => _knoepfe = knoepfe, Gewaehlt, Geraetetexte.UpdateKnopf(_installierbar, Pc), UpdateTexte.Spaeter);
        }
        finally
        {
            UpdateDienst.Fenster = null;
            if (!Pc)
            {
                UpdateDienst.Abgelehnt(_angebot.Version);
            }
        }
    }

    /// <summary>Knopf 0 ("Jetzt aktualisieren", "Zur Download-Seite") bleibt im Fenster; "Herunterladen", "Später" und Escape schliessen.</summary>
    private bool Gewaehlt(int nummer)
    {
        if (_laeuft)
        {
            return true;    // waehrend Laden und Installieren schliesst auch Escape nicht (Gegenpruefung W3 Fund 2)
        }
        if (nummer != 0)
        {
            return false;
        }
        if (_installierbar)
        {
            _ = AktualisierenAsync();
            return true;
        }
        // Download-Seite (PC) bzw. APK im Browser (Handy; ohne APK die Seite)
        _ = SeiteOeffnenAsync(_angebot.DateiUrl ?? _angebot.Seite);
        return Pc;
    }

    private async Task SeiteOeffnenAsync(string adresse)
    {
        try
        {
            if (TopLevel.GetTopLevel(Inhalt)?.Launcher is { } launcher)
            {
                await launcher.LaunchUriAsync(new Uri(adresse));
            }
        }
        catch (Exception e)
        {
            Fehlerprotokoll.Schreiben(e);
        }
    }

    /// <summary>start_update / _downloaded / _installed / _failed.</summary>
    private async Task AktualisierenAsync()
    {
        if (_laeuft)
        {
            return;
        }
        _laeuft = true;
        foreach (var knopf in _knoepfe)
        {
            knopf.IsEnabled = false;
        }
        _balken.Value = 0;
        _balken.IsVisible = true;
        Zeile(UpdateTexte.Laedt, "Pinsel.text_dim");
        _gesamt = Math.Max(1, _angebot.Groesse);
        var takt = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, (_, _) => Fortschritt());
        takt.Start();
        try
        {
            var text = await UpdateDienst.AktualisierenAsync(_angebot, (geladen, gesamt) =>
            {
                // im Hintergrund aufgerufen: nur Zahlen merken
                Interlocked.Exchange(ref _geladen, geladen);
                Interlocked.Exchange(ref _gesamt, Math.Max(1, gesamt));
            }, () =>
            {
                takt.Stop();
                _balken.Value = 1;
                Zeile(UpdateTexte.Installiere, "Pinsel.text_dim");
            });
            takt.Stop();
            Zeile(text, "Pinsel.green");
            // Programm schliessen, damit der Updater ersetzen kann; die 1,5 s sind zum Lesen der Meldung
            DispatcherTimer.RunOnce(UpdateDienst.Schliessen, TimeSpan.FromMilliseconds(1500));
        }
        catch (Exception e)
        {
            takt.Stop();
            Zeile(e is UpdateFehler ? e.Message : UpdateAblauf.Unerwartet(e), "Pinsel.red");
            if (e is not UpdateFehler)
            {
                Fehlerprotokoll.Schreiben(e);
            }
            if (_knoepfe.Count == 2)
            {
                ((TextBlock)_knoepfe[0].Content!).Text = UpdateTexte.Erneut;
            }
            foreach (var knopf in _knoepfe)
            {
                knopf.IsEnabled = true;
            }
            _balken.IsVisible = false;
            _laeuft = false;
        }
    }

    private void Fortschritt()
    {
        var geladen = Interlocked.Read(ref _geladen);
        var gesamt = Interlocked.Read(ref _gesamt);
        _balken.Value = Math.Min(1, (double)geladen / gesamt);
        Zeile(Beschriftungen.Einsetzen(UpdateTexte.LaedtMb, Zahlen.DeZahl(geladen / 1048576.0, 1), Zahlen.DeZahl(gesamt / 1048576.0, 1)), "Pinsel.text_dim");
    }

    /// <summary>Start ohne Bedienung: Zustand waehrend des Ladens (balken) oder nach einem Fehler (ohne balken).</summary>
    internal void Vorschau(string text, string farbe, double? balken)
    {
        Zeile(text, farbe);
        _balken.IsVisible = balken is not null;
        _balken.Value = balken ?? 0;
        foreach (var knopf in _knoepfe)
        {
            knopf.IsEnabled = balken is null;   // wie beim echten Laden gesperrt
        }
        if (balken is null && _knoepfe.Count == 2)
        {
            ((TextBlock)_knoepfe[0].Content!).Text = UpdateTexte.Erneut;
        }
    }

    private void Zeile(string text, string farbe)
    {
        _zustand.Text = text;
        _zustand.IsVisible = true;
        _zustand.Farbe(TextBlock.ForegroundProperty, farbe);
    }
}
