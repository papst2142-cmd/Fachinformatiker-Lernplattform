using Avalonia.Controls;
using Avalonia.Interactivity;
using Lernplattform.Core.Update;
using Lernplattform.Messung;
using Lernplattform.Views;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Messung;

/// <summary>
/// Patch S 0.5.1 Nachbesserung (Weisung P12-42 Punkt 1): Knopf "Nach Updates suchen" im Startbild mit einer Testquelle.
/// Jedes Ergebnis bekommt ein Fenster mit der richtigen Antwort (neu, neueste Version, nicht installiert, Fehler); beim
/// Start fragt das Werkzeug nur bei einer neuen Version (Gegenprobe: die Testquelle sagt beim Start "aktuell", es kam
/// kein Fenster).
/// </summary>
public static partial class StadtbauMessung
{
    /// <summary>Antwort der Testquelle.</summary>
    private static UpdateErgebnis UpdateTestErgebnis { get; set; } = new(UpdateArt.Aktuell);

    private static List<string> DialogTexte() =>
        Dialog.Offen is { } d ? [.. Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(d).OfType<TextBlock>().Select(t => t.Text ?? "")] : [];

    private static async Task DialogKnopfAsync(string text)
    {
        if (Dialog.Offen is { } d)
        {
            foreach (var k in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(d).OfType<Button>().Where(k => k.Content is TextBlock { Text: var t } && t == text).ToList())
            {
                k.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }
        await Task.Delay(200);
    }

    private static async Task UpdateKnopfAsync(Hauptansicht a, Window? f)
    {
        if (Dialog.Offen is not null)
        {
            Fund("Update: beim Start kam ein Fenster, obwohl die Testquelle \"aktuell\" sagt: " + string.Join(" ", DialogTexte()));
            await DialogKnopfAsync(Texte.Ok);
        }
        a.StartZeigenFuerTests();
        await BildAsync(a);
        var knopf = a.KnoepfeMit(Texte.UpdateSuchen).FirstOrDefault(k => k.IsVisible);
        if (knopf is null)
        {
            Fund("Update: Knopf \"" + Texte.UpdateSuchen + "\" fehlt im Startbild");
            return;
        }
        var faelle = new (UpdateErgebnis Ergebnis, string Erwartet, string Schliessen)[]
        {
            (new UpdateErgebnis(UpdateArt.Aktuell), Texte.UpdateAktuell(Werkzeug.Anzeige), Texte.Ok),
            (new UpdateErgebnis(UpdateArt.Neu, "9.9.9", () => Task.CompletedTask), Texte.UpdateNeuText("9.9.9"), Texte.UpdateSpaeter),
            (new UpdateErgebnis(UpdateArt.NichtInstalliert), Texte.UpdateNichtInstalliert, Texte.Ok),
            (new UpdateErgebnis(UpdateArt.Fehler, Grund: "Test"), Texte.UpdateFehler, Texte.Ok),
        };
        foreach (var (ergebnis, erwartet, schliessen) in faelle)
        {
            UpdateTestErgebnis = ergebnis;
            knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var i = 0; i < 50 && !DialogTexte().Contains(erwartet); i++)
            {
                await Task.Delay(50);
            }
            if (!DialogTexte().Contains(erwartet))
            {
                Fund("Update (" + ergebnis.Art + "): Fenster zeigt \"" + string.Join(" ", DialogTexte()) + "\" statt \"" + erwartet + "\"");
            }
            else
            {
                Messwerte.Notiere("Update (" + ergebnis.Art + "): " + erwartet);
            }
            if (ergebnis.Art == UpdateArt.Aktuell)
            {
                await AlleGroessenAsync(a, f, "Update aktuell");
            }
            await DialogKnopfAsync(schliessen);
        }
        await TabletFensterAsync(a, f, knopf);
        UpdateTestErgebnis = new UpdateErgebnis(UpdateArt.Aktuell);
        // Gegenprobe: beim Start nur bei einer neuen Version ein Fenster
        if (UpdateSuche.BeimStartZeigen(new UpdateErgebnis(UpdateArt.Aktuell)) || !UpdateSuche.BeimStartZeigen(new UpdateErgebnis(UpdateArt.Neu)))
        {
            Fund("Update: Regel fuer das Fenster beim Start falsch");
        }
    }

    /// <summary>Attrappe des Tablets: mobile Daten, ohne Erlaubnis, die Einstellung gibt sie nicht.</summary>
    private sealed class TestTablet : IHandyGeraet
    {
        public string LadeOrdner => Path.Combine(StadtbauApp.Ordner.Wurzel, "_tablet");

        public long FreierSpeicher => 1L << 34;

        public bool? ImWlan { get; set; } = false;

        public bool DarfInstallieren => false;

        public int ErlaubnisGefragt { get; private set; }

        public Task<bool> ErlaubnisHolenAsync()
        {
            ErlaubnisGefragt++;
            return Task.FromResult(false);
        }

        public string EigenesPaket => "io.github.papst2142cmd.stadtbau";

        public long EigenerVersionscode => 1;

        public ApkAngaben? ApkLesen(string pfad) => null;

        public void Installieren(string pfad) => throw new InvalidOperationException("Start-Test installiert nie");
    }

    private static async Task<bool> WartenAufAsync(Func<List<string>, bool> bedingung)
    {
        for (var i = 0; i < 60 && !bedingung(DialogTexte()); i++)
        {
            await Task.Delay(50);
        }
        return bedingung(DialogTexte());
    }

    private static bool KnopfImFenster(string text) =>
        Dialog.Offen is { } d && Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(d).OfType<Button>()
            .Any(k => k.IsVisible && k.Content is TextBlock { Text: var t } && t == text);

    /// <summary>
    /// S 0.6.0 (Weisung P12-42 Punkt 2, P12-44 Punkt 2): die Fenster des Updates am Tablet (Angebot mit Hinweis auf
    /// mobile Daten, Erlaubnis "Unbekannte Apps installieren" mit eigenem Fenster, Fehler mit "Zur Download-Seite") in
    /// allen Groessen hell und dunkel. Mit einer Attrappe des Tablets; geladen und installiert wird nichts. Gegenprobe:
    /// im WLAN fehlt der Hinweis auf mobile Daten.
    /// </summary>
    private static async Task TabletFensterAsync(Hauptansicht a, Window? f, Button knopf)
    {
        var geraetVorher = TabletUpdate.Geraet;
        var quelleVorher = TabletUpdate.Quelle;
        var tablet = new TestTablet();
        TabletUpdate.Geraet = tablet;
        TabletUpdate.Quelle = () => throw new InvalidOperationException("Start-Test laedt nie");
        try
        {
            const long groesse = 84_000_000;
            var mb = HandyPruefung.Mb(groesse);
            var angebot = new UpdateAngebot("S 9.9.9", "", "https://github.com/" + Werkzeug.Repository + "/releases/tag/vS9.9.9",
                "Fachinformatiker-Stadtbau-S-9.9.9-android-arm64.apk", Groesse: groesse);
            UpdateTestErgebnis = new UpdateErgebnis(UpdateArt.Neu, "9.9.9", Angebot: angebot);
            foreach (var wlan in new bool?[] { true, false })
            {
                tablet.ImWlan = wlan;
                knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!await WartenAufAsync(t => t.Any(x => x.Contains(Texte.TabletJetztHinweis(mb), StringComparison.Ordinal))))
                {
                    Fund("Tablet-Update: Angebot zeigt \"" + string.Join(" ", DialogTexte()) + "\"");
                    await DialogKnopfAsync(Texte.UpdateSpaeter);
                    return;
                }
                var mobil = DialogTexte().Any(x => x.Contains(Texte.TabletMobileDaten(mb), StringComparison.Ordinal));
                if (mobil == (wlan == true))
                {
                    Fund("Tablet-Update: Hinweis auf mobile Daten " + (mobil ? "im WLAN" : "fehlt ohne WLAN"));
                }
                if (wlan == true)
                {
                    await DialogKnopfAsync(Texte.UpdateSpaeter);
                }
            }
            Messwerte.Notiere("Tablet-Update: Angebot mit Hinweis auf mobile Daten (" + mb + " MB), im WLAN ohne");
            await AlleGroessenAsync(a, f, "Tablet-Update Angebot");
            await DialogKnopfAsync(Texte.UpdateJetzt);
            if (!await WartenAufAsync(t => t.Contains(Texte.TabletErlaubnisTitel)) || !KnopfImFenster(Texte.TabletZurEinstellung))
            {
                Fund("Tablet-Update: Fenster zur Erlaubnis fehlt (" + string.Join(" ", DialogTexte()) + ")");
                return;
            }
            Messwerte.Notiere("Tablet-Update: Fenster " + Texte.TabletErlaubnisTitel);
            await AlleGroessenAsync(a, f, "Tablet-Update Erlaubnis");
            await DialogKnopfAsync(Texte.TabletZurEinstellung);
            if (!await WartenAufAsync(t => t.Contains(Texte.TabletKeineErlaubnis)) || !KnopfImFenster(Texte.TabletZurSeite))
            {
                Fund("Tablet-Update: Fehlerfenster ohne \"" + Texte.TabletZurSeite + "\" (" + string.Join(" ", DialogTexte()) + ")");
                return;
            }
            if (tablet.ErlaubnisGefragt != 1)
            {
                Fund("Tablet-Update: Einstellung " + tablet.ErlaubnisGefragt + " Mal geoeffnet statt einmal");
            }
            Messwerte.Notiere("Tablet-Update: Fehlerfenster mit " + Texte.TabletZurSeite);
            await AlleGroessenAsync(a, f, "Tablet-Update Fehler");
            await DialogKnopfAsync(Texte.Ok);
        }
        finally
        {
            for (var i = 0; i < 5 && Dialog.Offen is not null && KnopfImFenster(Texte.Ok); i++)
            {
                await DialogKnopfAsync(Texte.Ok);
            }
            TabletUpdate.Geraet = geraetVorher;
            TabletUpdate.Quelle = quelleVorher;
        }
    }
}
