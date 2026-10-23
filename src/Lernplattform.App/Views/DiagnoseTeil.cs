using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;
using Update = Lernplattform.Aktualisierung.Aktualisierung;

namespace Lernplattform.Views;

/// <summary>
/// Phase 5 W2: Bereich "Diagnose und Werkzeuge" wie _build_diagnose (app_gui 7598) und am Handy
/// (main.py 4106): Problem melden (BE6), Leistungsmessung (BE7) und Hänger-Diagnose (BE8, nur PC).
/// Der Bericht wird nur neu gebaut, wenn sich eine der Dateien geaendert hat, und das im Hintergrund,
/// damit der Wechsel in die Optionen schnell bleibt. Nie ein Systemtext in einem Fenster.
/// </summary>
public sealed class DiagnoseTeil
{
    private static readonly bool Pc = !OperatingSystem.IsAndroid();

    private readonly Func<bool, bool> _ergebnis;
    private readonly TextBox _bericht;
    private readonly ToggleSwitch _messung;
    private readonly TextBlock _messZustand;
    private readonly TextBlock? _haengerZustand;
    private string? _schluessel;
    private bool _baut;
    private bool _setzen;

    public Control Teil { get; }

    /// <param name="ergebnis">Zeigt "Speichern fehlgeschlagen" (false) bzw. blendet es aus (true).</param>
    public DiagnoseTeil(OptionenBereich bereich, Func<bool, bool> ergebnis)
    {
        _ergebnis = ergebnis;
        var panel = new StackPanel { Spacing = 10 };
        _bericht = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            MaxHeight = 320,
            FontFamily = new FontFamily("monospace"),
        }.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt");
        Avalonia.Automation.AutomationProperties.SetName(_bericht, DiagnoseTexte.Titel);
        _messung = new ToggleSwitch
        {
            Content = Schrift.Text(LeistungTexte.Schalter),
            IsChecked = Diagnosedienst.Messung.Aktiv,
            OnContent = null,
            OffContent = null,
        };
        Avalonia.Automation.AutomationProperties.SetName(_messung, LeistungTexte.Schalter);
        _messung.IsCheckedChanged += (_, _) => MessungSchalten();
        _messZustand = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.muted");
        var erster = true;
        foreach (var abschnitt in Geraetetexte.OptionenAbschnitte(bereich, Pc))
        {
            var kopf = Schrift.Text(Beschriftungen.Gross(abschnitt.Titel), "Klein", FontWeight.Bold, "Pinsel.muted");
            if (!erster)
            {
                kopf.Margin = new Thickness(0, 14, 0, 0);
            }
            erster = false;
            panel.Children.Add(kopf);
            switch (abschnitt.Id)
            {
                case Optionen.AbschnittProblem:
                    panel.Children.Add(Hilfe(DiagnoseTexte.Hilfe));
                    panel.Children.Add(_bericht);
                    var reihe = new WrapPanel();
                    foreach (var text in DiagnoseTexte.Knoepfe(Pc))
                    {
                        var knopf = Schrift.Knopf(text, betont: text == DiagnoseTexte.Kopieren);
                        knopf.Margin = new Thickness(0, 0, 10, 6);
                        knopf.Click += async (_, _) => await ProblemKnopfAsync(text);
                        reihe.Children.Add(knopf);
                    }
                    panel.Children.Add(reihe);
                    break;
                case Optionen.AbschnittLeistung:
                    panel.Children.Add(Hilfe(LeistungTexte.Hilfe));
                    panel.Children.Add(_messung);
                    panel.Children.Add(_messZustand);
                    var zeigen = Schrift.Knopf(LeistungTexte.Zeigen, betont: true);
                    zeigen.Margin = new Thickness(0, 0, 10, 6);
                    zeigen.Click += async (_, _) => await MessdateiZeigenAsync();
                    var loeschen = Schrift.Knopf(LeistungTexte.Loeschen);
                    loeschen.Margin = new Thickness(0, 0, 10, 6);
                    loeschen.Click += (_, _) => MessdateiLoeschen();
                    panel.Children.Add(new WrapPanel { Children = { zeigen, loeschen } });
                    break;
                case Optionen.AbschnittHaenger:
                    panel.Children.Add(Hilfe(HaengerTexte.Hilfe));
                    _haengerZustand = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.muted");
                    panel.Children.Add(_haengerZustand);
                    if (OperatingSystem.IsLinux())
                    {
                        var signal = Schrift.Text(HaengerTexte.SignalHilfe, "Klein", FontWeight.Normal, "Pinsel.muted");
                        signal.Margin = new Thickness(0, 6, 0, 0);
                        panel.Children.Add(signal);
                        var befehl = Schrift.Text(HaengerTexte.Befehl, "Klein", FontWeight.Normal, "Pinsel.text_dim");
                        befehl.FontFamily = new FontFamily("monospace");
                        befehl.VerticalAlignment = VerticalAlignment.Center;
                        befehl.Margin = new Thickness(0, 0, 16, 6);
                        var kopieren = Schrift.Knopf(HaengerTexte.BefehlKopieren);
                        kopieren.Margin = new Thickness(0, 0, 0, 6);
                        kopieren.Click += async (_, _) =>
                        {
                            if (await KopierenAsync(HaengerTexte.Befehl))
                            {
                                Kurzhinweis.Zeigen(HaengerTexte.BefehlKopiert);
                            }
                        };
                        panel.Children.Add(new WrapPanel { Children = { befehl, kopieren } });
                    }
                    break;
            }
        }
        Teil = panel;
        Neu();
    }

    private static TextBlock Hilfe(string text) => Schrift.Text(text, "Klein", FontWeight.Normal, "Pinsel.text_dim");

    /// <summary>on_show/prepare: Bericht (nur nach einer Aenderung), Zustand der Messung und der Hänger.</summary>
    public void Neu()
    {
        _messZustand.Text = Diagnosedienst.Messung.ZustandText();
        if (_haengerZustand is { } haenger)
        {
            haenger.Text = Haenger.ZustandText();
        }
        Bericht = BerichtZeigenAsync();
    }

    /// <summary>Laufendes bzw. letztes Eintragen des Berichts (Start ohne Bedienung wartet darauf).</summary>
    public Task Bericht { get; private set; } = Task.CompletedTask;

    // -- Problem melden -------------------------------------------------------

    private static string BerichtText() =>
        Problembericht.Bauen(Zustand.Lernstand, Update.Anzeige, Pc ? "PC" : "Handy", Zustand.Werte, DateTime.Now);

    /// <summary>_show_report: Schluessel aus Groesse und Zeit der Protokolle und der Datenbank, nur bei Aenderung neu bauen.</summary>
    private async Task BerichtZeigenAsync()
    {
        if (_baut)
        {
            return;
        }
        _baut = true;
        try
        {
            var (schluessel, text) = await Task.Run(() =>
            {
                var s = Schluessel();
                return (s, s == _schluessel ? null : BerichtText());
            });
            if (text is not null)
            {
                _schluessel = schluessel;
                _bericht.Text = text;
            }
        }
        catch (Exception fehler)
        {
            Fehlerprotokoll.Schreiben(fehler);
        }
        finally
        {
            _baut = false;
        }
    }

    private static string Schluessel()
    {
        var ordner = Core.Daten.Ablage.Datenordner();
        var teile = new List<string>();
        var db = Zustand.Lernstand.Pfad;
        foreach (var pfad in new[]
        {
            Path.Combine(ordner, Fehlerprotokoll.DateiName), Path.Combine(ordner, Problembericht.UpdateName),
            Path.Combine(ordner, Problembericht.HaengerName), Path.Combine(ordner, Problembericht.InfoName),
            db, db + "-wal", db + "-journal",
        })
        {
            try
            {
                var info = new FileInfo(pfad);
                teile.Add(info.Exists ? info.Length + "@" + info.LastWriteTimeUtc.Ticks : "-");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                teile.Add("?");
            }
        }
        // Tagesziel und Abgleich stehen im Bericht: auch die Einstellungen gehoeren zum Schluessel
        teile.Add(Zustand.Werte.ToJsonString());
        return string.Join("|", teile);
    }

    private async Task ProblemKnopfAsync(string knopf)
    {
        try
        {
            switch (knopf)
            {
                case DiagnoseTexte.Kopieren:
                    if (await KopierenAsync(await Task.Run(BerichtText)))
                    {
                        Kurzhinweis.Zeigen(DiagnoseTexte.Kopiert);
                    }
                    break;
                case DiagnoseTexte.Speichern:
                    var text = await Task.Run(BerichtText);
                    await SpeichernAsync(DiagnoseTexte.Speichern, Problembericht.StandardName(DateTime.Now), Problembericht.Endung,
                        DiagnoseTexte.Dateityp, "text/plain", System.Text.Encoding.UTF8.GetBytes(text + "\n"));
                    break;
                case DiagnoseTexte.OrdnerOeffnen:
                    var ordner = Path.GetDirectoryName(Path.GetFullPath(Zustand.Lernstand.Pfad))!;
                    if (!await OrdnerOeffnenAsync(ordner, null))
                    {
                        await Dialog.Zeigen(DiagnoseTexte.OrdnerOeffnen, Beschriftungen.Einsetzen(DiagnoseTexte.OrdnerFehler, DiagnoseW2Texte.GrundImProtokoll), Dialog.Ok);
                    }
                    break;
            }
        }
        catch (Exception fehler)
        {
            Fehlerprotokoll.Schreiben(fehler);
        }
    }

    private async Task<bool> KopierenAsync(string text)
    {
        if (TopLevel.GetTopLevel(Teil)?.Clipboard is not { } ablage)
        {
            return false;
        }
        await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(ablage, text);
        return true;
    }

    /// <summary>
    /// save_report / save_file: Speicherort waehlen und schreiben. Handy (und Orte ohne lokalen Pfad):
    /// ueber den Datenstrom, danach zuruecklesen; erst bei gleichem Inhalt "Gespeichert: …".
    /// </summary>
    private async Task SpeichernAsync(string titel, string vorschlag, string endung, string typ, string mime, byte[] daten)
    {
        if (TopLevel.GetTopLevel(Teil) is not { } top)
        {
            return;
        }
        var datei = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = titel,
            SuggestedFileName = vorschlag,
            DefaultExtension = endung.TrimStart('.'),
            FileTypeChoices = [new FilePickerFileType(typ) { Patterns = ["*" + endung], MimeTypes = [mime] }],
        });
        if (datei is null)
        {
            return;
        }
        var name = datei.Name;
        try
        {
            var pfad = datei.TryGetLocalPath();
            if (Pc && pfad is not null)
            {
                await File.WriteAllBytesAsync(pfad, daten);
            }
            else
            {
                await using (var strom = await datei.OpenWriteAsync())
                {
                    if (strom.CanSeek)
                    {
                        strom.SetLength(0);
                    }
                    await strom.WriteAsync(daten);
                }
                // wie die Sicherung (Sammelliste 26): die Kopie am gewaehlten Ort zuruecklesen
                await using var lesen = await datei.OpenReadAsync();
                using var puffer = new MemoryStream();
                await lesen.CopyToAsync(puffer);
                if (!puffer.ToArray().AsSpan().SequenceEqual(daten))
                {
                    throw new IOException("Zurueckgelesene Datei weicht ab");
                }
            }
        }
        catch (Exception fehler)
        {
            Fehlerprotokoll.Schreiben(fehler);
            await Dialog.Zeigen(RuecksicherungTexte.SpeichernTitel, ProjektTexte.DateiNichtGespeichert, Dialog.Ok);
            return;
        }
        finally
        {
            datei.Dispose();
        }
        Kurzhinweis.Zeigen(Beschriftungen.Einsetzen(ProjektTexte.Gespeichert, name));
    }

    /// <summary>open_data_folder: Ordner im Dateimanager; unter Windows auf Wunsch mit markierter Datei.</summary>
    private async Task<bool> OrdnerOeffnenAsync(string ordner, string? markieren)
    {
        try
        {
            if (OperatingSystem.IsWindows() && markieren is not null && File.Exists(markieren))
            {
                using var _ = Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { "/select,", markieren }, UseShellExecute = false });
                return true;
            }
            if (TopLevel.GetTopLevel(Teil)?.Launcher is { } launcher)
            {
                return await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(ordner));
            }
        }
        catch (Exception fehler)
        {
            Fehlerprotokoll.Schreiben(fehler);
        }
        return false;
    }

    // -- Leistungsmessung -------------------------------------------------------

    /// <summary>_toggle_perf: Schalter erst speichern (nur einstellungen.json, gemeinsame Sperre), dann schalten.</summary>
    private void MessungSchalten()
    {
        if (_setzen)
        {
            return;
        }
        var an = _messung.IsChecked == true;
        if (!_ergebnis(Zustand.Setze(Leistungsmessung.Schluessel, an)))
        {
            _setzen = true;
            _messung.IsChecked = Diagnosedienst.Messung.Aktiv;
            _setzen = false;
            return;
        }
        Diagnosedienst.Schalten(an);
        _messZustand.Text = Diagnosedienst.Messung.ZustandText();
    }

    /// <summary>show_perf_file: PC Datenordner (Windows mit markierter Datei), Handy die Datei speichern.</summary>
    private async Task MessdateiZeigenAsync()
    {
        var messung = Diagnosedienst.Messung;
        try
        {
            if (Pc)
            {
                messung.Schreiben();
                var ordner = Path.GetDirectoryName(Path.GetFullPath(messung.Pfad))!;
                if (!await OrdnerOeffnenAsync(ordner, messung.Pfad))
                {
                    await Dialog.Zeigen(LeistungTexte.Zeigen, Beschriftungen.Einsetzen(LeistungTexte.ZeigenFehler, DiagnoseW2Texte.GrundImProtokoll), Dialog.Ok);
                }
            }
            else if (messung.Info() is null)
            {
                Kurzhinweis.Zeigen(LeistungTexte.KeineDatei);
            }
            else
            {
                byte[] daten;
                try
                {
                    daten = messung.Lesen();
                }
                catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
                {
                    Fehlerprotokoll.Schreiben(fehler);
                    Kurzhinweis.Zeigen(Beschriftungen.Einsetzen(LeistungTexte.ZeigenFehler, DiagnoseW2Texte.GrundImProtokoll));
                    return;
                }
                await SpeichernAsync(LeistungTexte.Zeigen, Leistungsmessung.DateiName, ".csv", "CSV", Leistungsmessung.Dateityp, daten);
            }
        }
        catch (Exception fehler)
        {
            Fehlerprotokoll.Schreiben(fehler);
        }
        _messZustand.Text = messung.ZustandText();
    }

    private void MessdateiLoeschen()
    {
        if (Diagnosedienst.Messung.Loeschen())
        {
            Kurzhinweis.Zeigen(LeistungTexte.Geloescht);
        }
        _messZustand.Text = Diagnosedienst.Messung.ZustandText();
    }
}
