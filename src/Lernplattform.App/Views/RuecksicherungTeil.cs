using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 2, L9: „Sicherung für die alte App erstellen“ in Optionen &gt; Sicherung (Weisung P2-11).
/// Speichern-Dialog mit Vorschlag wie default_name, Wartehinweis wie Python, Datei wie
/// create_backup (Core.Daten.Ruecksicherung). PC: atomar am gewaehlten Ort. Handy: atomar im
/// App-Ordner schreiben und pruefen, dann an den gewaehlten Ort kopieren (A2). Fehler: T3/T4,
/// Erfolg „Gespeichert: &lt;Dateiname&gt;“. Der Lernstand wird nur gelesen.
/// </summary>
public sealed class RuecksicherungTeil
{
    private static readonly bool Android = OperatingSystem.IsAndroid();
    private readonly Control _besitzer;
    private readonly Button _knopf;
    private readonly TextBlock _zustand;
    private bool _laeuft;

    public RuecksicherungTeil(Control besitzer)
    {
        _besitzer = besitzer;
        _knopf = Schrift.Knopf(RuecksicherungTexte.Knopf);
        _knopf.Click += async (_, _) => await AblaufAsync();
        _zustand = Schrift.Text("", "Normal", FontWeight.Normal, "Pinsel.text_dim");
        _zustand.IsVisible = false;
        Teil = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new WrapPanel { Children = { _knopf } },
                Schrift.Text(RuecksicherungTexte.Erklaerung, "Normal", FontWeight.Normal, "Pinsel.text_dim"),
                _zustand,
            },
        };
    }

    public Control Teil { get; }

    private void Zeige(string text)
    {
        _zustand.Text = text;
        _zustand.IsVisible = text.Length > 0;
    }

    private async Task AblaufAsync()
    {
        if (_laeuft || TopLevel.GetTopLevel(_besitzer) is not { } top)
        {
            return;
        }
        _laeuft = true;
        _knopf.IsEnabled = false;
        Zeige("");
        try
        {
            await AblaufInnenAsync(top);
        }
        catch (RuecksicherungAusnahme fehler)
        {
            Zeige("");
            var (titel, text) = RuecksicherungTexte.Fehler(fehler.Fehler);
            await Dialog.Zeigen(titel, text, Dialog.Ok);
        }
        catch (Exception fehler)
        {
            // Nie einen Systemtext zeigen; Einzelheiten ins Fehlerprotokoll
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
            Zeige("");
            var (titel, text) = RuecksicherungTexte.Fehler(RuecksicherungFehler.Unbekannt);
            await Dialog.Zeigen(titel, text, Dialog.Ok);
        }
        finally
        {
            _laeuft = false;
            _knopf.IsEnabled = true;
        }
    }

    private async Task AblaufInnenAsync(TopLevel top)
    {
        if (Vorgaenge.Belegt)
        {
            // P2-12 A2: nicht warten, solange ein Abgleich oder eine Uebernahme laeuft
            await Dialog.Zeigen(RuecksicherungTexte.Knopf, AbgleichTexte.Belegt, Dialog.Ok);
            return;
        }
        // W3 (flush_inputs(revealed=False) vor dem Datei-Dialog, app_gui 8043, main.py 4686): offene Eingaben im Abschlussprojekt zuerst speichern
        Zustand.EingabenSpeichern();
        var vorschlag = Ruecksicherung.DateiName(DateTime.Now);
        var datei = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = RuecksicherungTexte.Knopf,
            SuggestedFileName = vorschlag,
            DefaultExtension = Ruecksicherung.Endung,
            FileTypeChoices = [new FilePickerFileType(RuecksicherungTexte.DateiTyp) { Patterns = ["*" + Ruecksicherung.Endung] }],
        });
        if (datei is null)
        {
            return;
        }
        Zeige(RuecksicherungTexte.Warten);
        var lernstand = Zustand.Lernstand;
        var einstellungen = Zustand.Werte;
        var geraet = AbgleichDienst.Geraet();
        var bytes = await Task.Run(() => Ruecksicherung.Erstellen(lernstand, einstellungen, Aktualisierung.Aktualisierung.Anzeige, geraet, DateTime.Now));
        var pfad = datei.TryGetLocalPath();
        if (!Android && pfad is not null)
        {
            await Task.Run(() => Ruecksicherung.Speichern(pfad, bytes));
        }
        else
        {
            var arbeitsdatei = Path.Combine(Ablage.Datenordner(), "ruecksicherung" + Ruecksicherung.Endung);
            await Ruecksicherung.SpeichernUeberAsync(arbeitsdatei, bytes, datei.OpenWriteAsync);
        }
        Zeige(RuecksicherungTexte.Erfolg(datei.Name));
        await AusbildungTeil.VergleichenAsync();          // W4: Vergleich des Merkers wie nach Abgleich und Uebernahme
    }
}
