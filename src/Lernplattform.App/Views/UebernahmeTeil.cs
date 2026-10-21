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
/// Phase 2, L5: "Lernstand übernehmen" in Optionen &gt; Sicherung. Datei waehlen, Vorschau
/// (Rechnung auf einer Kopie im Arbeitsspeicher, aendert nichts), "Zusammenführen" mit
/// automatischer Kopie im Ordner "sicherungen", Ergebnis. Texte nach dem Wortlaut
/// P2-4_Wortlaut_L5.md (Core.Texte.UebernahmeTexte); nie ein Systemtext.
/// </summary>
public sealed class UebernahmeTeil
{
    private static readonly bool Android = OperatingSystem.IsAndroid();
    private readonly Control _besitzer;
    private readonly Button _knopf;
    private readonly TextBlock _grund;
    private bool _laeuft;

    public UebernahmeTeil(Control besitzer)
    {
        _besitzer = besitzer;
        _knopf = Schrift.Knopf(UebernahmeTexte.Knopf);
        _knopf.Click += async (_, _) => await AblaufAsync();
        _grund = Schrift.Text("", "Normal", FontWeight.Normal, "Pinsel.red");
        _grund.IsVisible = false;
        Teil = new StackPanel { Spacing = 4, Children = { new WrapPanel { Children = { _knopf } }, _grund } };
        besitzer.AttachedToVisualTree += (_, _) => GrundZeigen();
    }

    public Control Teil { get; }

    /// <summary>Nur lesen (z. B. beschaedigte Datenbank): Knopf gesperrt, Grund daneben.</summary>
    private void GrundZeigen()
    {
        var grund = Zustand.Lernstand.NurLesenGrund;
        _knopf.IsEnabled = grund is null && !_laeuft;
        _grund.IsVisible = grund is not null;
        _grund.Text = grund is null ? "" : UebernahmeTexte.NurLesenText(grund);
    }

    private static Task Hinweis(string text) => Dialog.Zeigen(UebernahmeTexte.Titel, text, Dialog.Ok);

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
            await Hinweis(UebernahmeTexte.DateiNichtLesbar);
        }
        finally
        {
            _laeuft = false;
            GrundZeigen();
        }
    }

    private async Task AblaufInnenAsync(TopLevel top)
    {
        if (Vorgaenge.Belegt)
        {
            // P2-12 A2: nicht warten, solange ein Abgleich oder eine andere Uebernahme laeuft
            await Hinweis(AbgleichTexte.Belegt);
            return;
        }
        var ordner = Ablage.Datenordner();
        UebernahmeAblauf.Aufraeumen(ordner);
        var lernstand = Zustand.Lernstand;
        if (lernstand.NurLesenGrund is not null)
        {
            await Hinweis(UebernahmeTexte.NurLesenText(lernstand.NurLesenGrund));
            return;
        }
        var optionen = new FilePickerOpenOptions { Title = UebernahmeTexte.DateiwahlTitel, AllowMultiple = false };
        if (!Android)
        {
            // Android kennt fuer .fisisicherung keinen Dateityp: dort ohne Filter
            optionen.FileTypeFilter =
            [
                new FilePickerFileType(UebernahmeTexte.FilterSicherung) { Patterns = [UebernahmeTexte.FilterSicherungMuster] },
                new FilePickerFileType(UebernahmeTexte.FilterAlle) { Patterns = ["*"] },
            ];
        }
        var auswahl = await top.StorageProvider.OpenFilePickerAsync(optionen);
        if (auswahl.Count == 0)
        {
            return;
        }
        byte[] inhalt;
        string? pfad;
        try
        {
            pfad = auswahl[0].TryGetLocalPath();
            using var puffer = new MemoryStream();
            await using (var lesen = await auswahl[0].OpenReadAsync())
            {
                await lesen.CopyToAsync(puffer);
            }
            inhalt = puffer.ToArray();
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
            await Hinweis(UebernahmeTexte.DateiNichtLesbar);
            return;
        }

        var quelle = UebernahmeAblauf.Erkennen(inhalt);
        Austausch daten;
        UebernahmeBericht vorschau;
        try
        {
            daten = await Task.Run(() => UebernahmeAblauf.Lesen(inhalt, pfad, ordner));
            vorschau = await Task.Run(() =>
            {
                try
                {
                    return Uebernahme.Vorschau(lernstand, daten, Zustand.Umbenennungen);
                }
                catch (Exception fehler) when (fehler is not UebernahmeAusnahme)
                {
                    // Jeder andere Rechenfehler der Vorschau: "Vorschau scheitert" (Wortlaut 5)
                    Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
                    throw new UebernahmeAusnahme(UebernahmeFehler.SchreibenGescheitert);
                }
            });
        }
        catch (UebernahmeAusnahme fehler)
        {
            await Hinweis(UebernahmeTexte.Fehler(fehler.Fehler, fehler.Version, lernstand.NurLesenGrund, true, quelle));
            return;
        }

        if (await Dialog.Zeigen(UebernahmeTexte.Titel, VorschauInhalt(vorschau, quelle == UebernahmeQuelle.Sicherung ? inhalt : null),
                UebernahmeTexte.Zusammenfuehren, UebernahmeTexte.Abbrechen) != 0)
        {
            return;
        }

        try
        {
            var bericht = await Task.Run(() =>
            {
                try
                {
                    return Uebernahme.Uebernehmen(lernstand, daten, Zustand.Umbenennungen);
                }
                catch (Exception fehler) when (fehler is not UebernahmeAusnahme)
                {
                    // Jeder andere Fehler kommt vor tx.Commit(): die Transaktion wird
                    // zurueckgenommen, also "Übernehmen fehlgeschlagen" statt "Datei nicht lesbar"
                    Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
                    throw new UebernahmeAusnahme(UebernahmeFehler.SchreibenGescheitert);
                }
            });
            await Hinweis(UebernahmeTexte.Ergebnis(bericht, bericht.Sicherungsdatei is { } datei ? UebernahmeTexte.KopieName(Path.GetFileName(datei), Android) : null));
        }
        catch (UebernahmeAusnahme fehler)
        {
            await Hinweis(UebernahmeTexte.Fehler(fehler.Fehler, fehler.Version, lernstand.NurLesenGrund, false, quelle));
        }
    }

    /// <summary>Inhalt des Vorschau-Fensters (Wortlaut Abschnitt 2).</summary>
    public static Control VorschauInhalt(UebernahmeBericht vorschau, byte[]? sicherung)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(Schrift.Text(UebernahmeTexte.Vorschau, "Mittel", FontWeight.Bold));
        panel.Children.Add(Schrift.Text(UebernahmeTexte.QuelleZeile(vorschau.Quelle), "Normal", FontWeight.SemiBold));
        if (sicherung is not null)
        {
            foreach (var zeile in UebernahmeAblauf.SicherungsZeilen(sicherung))
            {
                panel.Children.Add(Schrift.Text(zeile, "Normal", FontWeight.Normal, "Pinsel.text_dim"));
            }
        }
        var zahlen = new StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(0, 6, 0, 6) };
        foreach (var zeile in UebernahmeTexte.Zahlzeilen(vorschau))
        {
            zahlen.Children.Add(Schrift.Text(zeile));
        }
        panel.Children.Add(zahlen);
        foreach (var satz in new[] { UebernahmeTexte.HinweisDoppelt, UebernahmeTexte.HinweisNichts, UebernahmeTexte.HinweisKopie })
        {
            panel.Children.Add(Schrift.Text(satz, "Normal", FontWeight.Normal, "Pinsel.text_dim"));
        }
        panel.HorizontalAlignment = HorizontalAlignment.Stretch;
        return panel;
    }
}
