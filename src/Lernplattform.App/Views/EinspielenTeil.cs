using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 5 BE3: „Sicherung einspielen“ in Optionen &gt; Sicherung wie restore_backup (app_gui 8069):
/// Datei waehlen (Filter Lernstand-Sicherung / Alle Dateien, am Handy ohne Filter), lesen wie
/// read_backup, offene Eingaben speichern, Vorschau (backup_summary, PREVIEW_HINT) mit
/// „Zusammenführen“ / „Alles ersetzen“ / „Abbrechen“. Zusammenführen ueber den Weg von
/// „Lernstand übernehmen“ (Kopie im Ordner „sicherungen“), Alles ersetzen nach zwei Rueckfragen
/// (Vorgabe „Nein“) mit „Bitte warten“ (Core.Daten.Wiederherstellung). Danach alles neu zeigen
/// und Abgleich im ueblichen Takt nach dem Lernen. Nie ein Systemtext.
/// </summary>
public sealed class EinspielenTeil
{
    private static readonly bool Android = OperatingSystem.IsAndroid();
    private readonly Control _besitzer;
    private readonly Button _knopf;
    private readonly TextBlock _zustand;
    private readonly Action _danach;
    private bool _laeuft;

    /// <param name="danach">Nach dem Einspielen: offene Seiten neu zeigen (_after_restore).</param>
    public EinspielenTeil(Control besitzer, TextBlock zustand, Action danach)
    {
        _besitzer = besitzer;
        _zustand = zustand;
        _danach = danach;
        // Python: NeoButton(BTN_RESTORE, kind="ghost")
        _knopf = Schrift.Knopf(SicherungTexte.KnopfEinspielen);
        _knopf.Click += async (_, _) => await AblaufAsync();
    }

    public Button Knopf => _knopf;

    /// <summary>Gerade offenes Fenster des Ablaufs (Start ohne Bedienung): Vorschau mit dieser Sicherung zeigen.</summary>
    public static Task<int> VorschauZeigen(Sicherungsinhalt sicherung) =>
        Dialog.Zeigen(SicherungTexte.Titel, VorschauInhalt(sicherung), SicherungTexte.Zusammenfuehren, SicherungTexte.Ersetzen, SicherungTexte.Abbrechen);

    /// <summary>Langer Text der ersten Rueckfrage (ask_long_question, Bildlauf im Fenster), Vorgabe „Nein“.</summary>
    public static Task<int> FrageZeigen(Sicherungsinhalt sicherung) =>
        Dialog.Rueckfrage(SicherungTexte.ErsetzenTitel, SicherungTexte.ErsetzenFrage(sicherung), 1, TrainerTexte.Ja, TrainerTexte.Nein);

    private static Task Hinweis(string text) => Dialog.Zeigen(SicherungTexte.Titel, text, Dialog.Ok);

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
        try
        {
            await AblaufInnenAsync(top);
        }
        catch (Exception fehler)
        {
            // Nie einen Systemtext zeigen; Einzelheiten ins Fehlerprotokoll
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
            Zeige("");
            await Hinweis(SicherungTexte.EinspielenGescheitert);
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
            await Hinweis(AbgleichTexte.Belegt);
            return;
        }
        var lernstand = Zustand.Lernstand;
        if (lernstand.NurLesenGrund is { } grund)
        {
            await Hinweis(SicherungTexte.NurLesenText(grund));
            return;
        }
        var optionen = new FilePickerOpenOptions { Title = SicherungTexte.KnopfEinspielen, AllowMultiple = false };
        // Android kennt fuer .fisisicherung keinen Dateityp: dort ohne Filter (derselbe Filter wie bei der Uebernahme, B10)
        var filter = Geraetetexte.UebernahmeDateifilter(!Android);
        if (filter.Count > 0)
        {
            optionen.FileTypeFilter = [.. filter.Select(f => new FilePickerFileType(f.Name) { Patterns = [f.Muster] })];
        }
        var auswahl = await top.StorageProvider.OpenFilePickerAsync(optionen);
        if (auswahl.Count == 0)
        {
            return;
        }
        Sicherungsinhalt sicherung;
        try
        {
            using var puffer = new MemoryStream();
            await using (var lesen = await auswahl[0].OpenReadAsync())
            {
                await lesen.CopyToAsync(puffer);
            }
            var roh = puffer.ToArray();
            sicherung = await Task.Run(() => Sicherungsinhalt.Lesen(roh));
        }
        catch (UebernahmeAusnahme fehler)
        {
            await Hinweis(SicherungTexte.LesenFehler(fehler));
            return;
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
            await Hinweis(UebernahmeTexte.DateiNichtLesbar);
            return;
        }
        // flush_inputs(revealed=False) nach dem Lesen, vor der Vorschau (app_gui 8082)
        Zustand.EingabenSpeichern();
        switch (await VorschauZeigen(sicherung))
        {
            case 0:
                await ZusammenfuehrenAsync(lernstand, sicherung);
                break;
            case 1:
                await ErsetzenAsync(lernstand, sicherung);
                break;
        }
    }

    /// <summary>_merge_backup: Weg von „Lernstand übernehmen“ (deleted_runs erst mit dem Spiel, Plan BE3).</summary>
    private async Task ZusammenfuehrenAsync(Lernstand lernstand, Sicherungsinhalt sicherung)
    {
        if (Vorgaenge.Belegt)
        {
            await Hinweis(AbgleichTexte.Belegt);
            return;
        }
        UebernahmeBericht bericht;
        try
        {
            bericht = await Task.Run(() =>
            {
                try
                {
                    return Uebernahme.Uebernehmen(lernstand, sicherung.Daten, Zustand.Umbenennungen);
                }
                catch (Exception fehler) when (fehler is not UebernahmeAusnahme)
                {
                    // vor tx.Commit(): die Buchung wird zurueckgenommen
                    Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
                    throw new UebernahmeAusnahme(UebernahmeFehler.SchreibenGescheitert);
                }
            });
        }
        catch (UebernahmeAusnahme fehler)
        {
            await Hinweis(SicherungTexte.ZusammenfuehrenFehler(fehler.Fehler, lernstand.NurLesenGrund));
            return;
        }
        // Ab hier ist zusammengefuehrt: ein Fehler beim Neu-Zeigen aendert die Rueckmeldung nicht (Gegenpruefung W1 Fund 1)
        Nachher();
        await Hinweis(SicherungTexte.Zusammengefuehrt(bericht,
            bericht.Sicherungsdatei is { } datei ? Geraetetexte.KopieName(Path.GetFileName(datei), !Android) : null));
        await NurProtokollAsync(AusbildungTeil.VergleichenAsync);
    }

    /// <summary>_replace_backup: zwei Rueckfragen (Vorgabe „Nein“), Bitte warten, ersetzen, Meldung.</summary>
    private async Task ErsetzenAsync(Lernstand lernstand, Sicherungsinhalt sicherung)
    {
        if (await FrageZeigen(sicherung) != 0
            || await Dialog.Rueckfrage(SicherungTexte.RueckfrageTitel, SicherungTexte.Rueckfrage, 1, TrainerTexte.Ja, TrainerTexte.Nein) != 0)
        {
            return;
        }
        if (Vorgaenge.Belegt)
        {
            await Hinweis(AbgleichTexte.Belegt);
            return;
        }
        // show_wait: „Bitte warten“ ueber dem ganzen Fenster, solange ersetzt und neu angezeigt wird (vor jeder
        // Meldung wieder weg); nichts darunter laesst sich bedienen, also auch keine Antwort zwischen Kopie und commit
        string? kopie = null;
        string? fehlerText = null;
        using (Dialog.Warten(SicherungTexte.WartenErsetzen))
        {
            try
            {
                var einstellungen = Zustand.Einstellungen;
                var geraet = AbgleichDienst.Geraet();
                kopie = await Task.Run(() => Wiederherstellung.Ersetzen(lernstand, sicherung, einstellungen, Aktualisierung.Aktualisierung.Anzeige, geraet));
            }
            catch (WiederherstellungAusnahme fehler)
            {
                fehlerText = SicherungTexte.ErsetzenFehler(fehler, lernstand.NurLesenGrund);
            }
            if (kopie is not null)
            {
                // Ab hier ist ersetzt: ein Fehler beim Neu-Zeigen aendert die Rueckmeldung nicht (Gegenpruefung W1 Fund 1)
                NurProtokoll(Zustand.EinstellungenNeuLesen);
                Nachher();
            }
        }
        if (kopie is null)
        {
            await Hinweis(fehlerText ?? SicherungTexte.EinspielenGescheitert);
            return;
        }
        await Hinweis(SicherungTexte.ErsetztMeldung(sicherung, Geraetetexte.KopieName(Path.GetFileName(kopie), !Android)));
        await NurProtokollAsync(AusbildungTeil.VergleichenAsync);
    }

    /// <summary>_after_restore: Seiten neu zeigen, Abgleich wie nach dem Lernen (notify_progress); Fehler nur ins Protokoll.</summary>
    private void Nachher()
    {
        NurProtokoll(_danach);
        NurProtokoll(AbgleichDienst.NachDemLernen);
    }

    private static void NurProtokoll(Action schritt)
    {
        try
        {
            schritt();
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
    }

    private static async Task NurProtokollAsync(Func<Task> schritt)
    {
        try
        {
            await schritt();
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
    }

    /// <summary>Inhalt des Vorschau-Fensters (BackupDialog): Zeilen von backup_summary, Leerzeile, PREVIEW_HINT.</summary>
    public static Control VorschauInhalt(Sicherungsinhalt sicherung)
    {
        var panel = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var zeile in sicherung.Zeilen)
        {
            panel.Children.Add(Schrift.Text(zeile));
        }
        var hinweis = Schrift.Text(SicherungTexte.VorschauHinweis, "Normal", FontWeight.Normal, "Pinsel.text_dim");
        hinweis.Margin = new Avalonia.Thickness(0, 10, 0, 0);
        panel.Children.Add(hinweis);
        return panel;
    }
}
