using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Lernplattform.Core.Update;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// S 0.6.0 (Weisung P12-42 Punkt 2): das Werkzeug aktualisiert sich am Android-Tablet selbst, wie die Lernplattform am
/// Handy seit A 0.39.0. Gemeinsamer Teil: <see cref="HandyQuelle"/> mit Linie Stadtbau (Suche ueber die Liste der
/// Releases, nur vS, nie ein Release der Lernplattform), <see cref="HandyPruefung"/> (SHA-256 aus zwei Quellen, Paketname,
/// genau ein Zertifikat gleich android_fingerabdruck.txt, hoeherer Versionscode, Platz) und der Android-Teil HandyInstaller
/// (verlinkt aus Lernplattform.Android). Hier nur die Fenster: Angebot, einmalig die Erlaubnis "Unbekannte Apps
/// installieren" mit eigenem Fenster, Laden, und bei jedem Fehler ein Fenster mit "Zur Download-Seite". Projekte sind
/// nach jeder Aenderung gespeichert, deshalb keine Kopie wie in der Lernplattform. Protokoll update.log im Werkzeugordner.
/// </summary>
public static class TabletUpdate
{
    /// <summary>Seite mit allen Releases (das Werkzeug wird nie "latest"); nur wenn GitHub keine Release-Seite nennt.</summary>
    public const string AlleReleases = "https://github.com/" + Werkzeug.Repository + "/releases";

    /// <summary>Android-Teil (setzt MainActivity des Werkzeugs); null = kein Update am Tablet (PC, Tests).</summary>
    public static IHandyGeraet? Geraet { get; set; }

    /// <summary>Update-Quelle (setzt MainActivity des Werkzeugs).</summary>
    public static Func<HandyQuelle>? Quelle { get; set; }

    private static UpdateAngebot? _angebot;

    private static string Protokollordner => StadtbauApp.Ordner.Wurzel;

    /// <summary>Suche fuer <see cref="Aktualisierung.Pruefen"/> (beim Start und mit "Nach Updates suchen").</summary>
    public static async Task<UpdateErgebnis> SuchenAsync() =>
        UpdateSuche.AusAngebot(await UpdateAblauf.SuchenAsync(Quelle!()).ConfigureAwait(false));

    /// <summary>Fenster "Neue Version" mit "Jetzt aktualisieren": Erlaubnis, Laden, Pruefen, Uebergabe an Android.</summary>
    internal static async Task AnbietenAsync(UpdateErgebnis e, Action<string> fehler)
    {
        if (e.Angebot is not { } angebot || Geraet is not { } geraet || Quelle is not { } neu)
        {
            return;
        }
        var mb = HandyPruefung.Mb(angebot.Groesse);
        var text = Texte.UpdateNeuText(e.Version) + "\n\n" + Texte.TabletJetztHinweis(mb)
            + (geraet.ImWlan == false ? "\n\n" + Texte.TabletMobileDaten(mb) : "");
        if (await Dialog.Zeigen(Texte.UpdateNeuTitel, text, Texte.UpdateJetzt, Texte.UpdateSpaeter) != 0)
        {
            return;
        }
        if (!await ErlaubnisAsync(geraet))
        {
            await FehlerZeigenAsync(Texte.TabletKeineErlaubnis, angebot.Seite);
            return;
        }
        string? problem;
        using (Dialog.Warten(Texte.UpdateLaedt))
        {
            problem = await Task.Run(() => UebergebenAsync(neu(), angebot, fehler));
        }
        if (problem is null)
        {
            await Dialog.Zeigen(Texte.UpdateNeuTitel, Texte.TabletLaeuft, Texte.Ok);
        }
        else
        {
            await FehlerZeigenAsync(problem, angebot.Seite);
        }
    }

    /// <summary>Ohne Fenster (auch fuer die Emulator-Probe): siehe <see cref="TabletAktualisierung.UebergebenAsync"/>.</summary>
    public static Task<string?> UebergebenAsync(HandyQuelle quelle, UpdateAngebot angebot, Action<string> fehler)
    {
        _angebot = angebot;
        return TabletAktualisierung.UebergebenAsync(quelle, angebot, Protokollordner, fehler);
    }

    /// <summary>
    /// Rueckmeldung von Android zur Installation (HandyInstaller.Empfaenger, auf dem UI-Faden): Zeile in update.log,
    /// bei einem Fehler Datei weg und das Fenster mit "Zur Download-Seite".
    /// </summary>
    public static void Rueckmeldung(int status, string? meldung)
    {
        try
        {
            var text = TabletAktualisierung.Rueckmeldung(status, meldung, _angebot?.Groesse ?? 0,
                Geraet?.LadeOrdner ?? Path.Combine(Path.GetTempPath(), "stadtbau-update"), Protokollordner);
            if (text is not null)
            {
                Dispatcher.UIThread.Post(() => _ = FehlerZeigenAsync(text, _angebot?.Seite));
            }
        }
        catch (Exception e)
        {
            StadtbauApp.Ordner.Fehler("Update: " + e);
        }
    }

    /// <summary>Einmalig die Erlaubnis "Unbekannte Apps installieren": eigenes Fenster, dann die Einstellung von Android.</summary>
    private static async Task<bool> ErlaubnisAsync(IHandyGeraet geraet)
    {
        if (geraet.DarfInstallieren)
        {
            return true;
        }
        Updateprotokoll.Schreiben("Erlaubnis „Unbekannte Apps installieren“ fehlt, Erklärung gezeigt.", Protokollordner);
        if (await Dialog.Zeigen(Texte.TabletErlaubnisTitel, Texte.TabletErlaubnisText, Texte.TabletZurEinstellung, Texte.TabletAbbrechen) != 0)
        {
            return false;
        }
        var erlaubt = await geraet.ErlaubnisHolenAsync();
        Updateprotokoll.Schreiben(erlaubt ? "Erlaubnis erteilt." : "Erlaubnis nicht erteilt.", Protokollordner);
        return erlaubt;
    }

    /// <summary>Jeder Fehler am Tablet: Fenster mit dem Grund und dem Knopf "Zur Download-Seite".</summary>
    private static async Task FehlerZeigenAsync(string text, string? seite)
    {
        if (await Dialog.Zeigen(Texte.TabletFehlerTitel, text, Texte.TabletZurSeite, Texte.Ok) != 0)
        {
            return;
        }
        try
        {
            var ansicht = Application.Current?.ApplicationLifetime switch
            {
                ISingleViewApplicationLifetime einzeln => einzeln.MainView,
                IClassicDesktopStyleApplicationLifetime fenster => fenster.MainWindow,
                _ => null,
            };
            if (ansicht is not null && TopLevel.GetTopLevel(ansicht)?.Launcher is { } launcher)
            {
                await launcher.LaunchUriAsync(new Uri(string.IsNullOrEmpty(seite) ? AlleReleases : seite));
            }
        }
        catch (Exception e)
        {
            StadtbauApp.Ordner.Fehler("Update: " + e.GetType().Name + ": " + e.Message);
        }
    }
}
