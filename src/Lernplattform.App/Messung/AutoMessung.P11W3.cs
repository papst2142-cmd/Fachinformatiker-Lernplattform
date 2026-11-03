using System.Text.RegularExpressions;
using Lernplattform.Core.Diagnose;
using Lernplattform.Programm;
using Lernplattform.Views;
using Lernplattform.Core.Texte;
using Update = Lernplattform.Aktualisierung.Aktualisierung;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 11 W3 (Weisung P11-21): die Messpunkte, die das Programm selbst aufschreibt (<see cref="Messpunkte"/>), stehen
/// nach dem Lauf in messpunkte.log und im Problembericht. Der Lernteil verlangt "Inhalte laden", "Erste Karte" und
/// "Fachrichtung wechseln" (der Start-Test wechselt die Fachrichtung, P9W2), der Spielteil "Spiel betreten", sobald ein
/// 3D-Bild gezeichnet wurde (ohne OpenGL gibt es kein erstes Bild). Jede Zeile hat genau die feste Form (Regel A3:
/// nur Zeit, Version, Name, Millisekunden; nie Inhalte des Lernstands).
/// </summary>
public static partial class AutoMessung
{
    private static readonly Regex MesspunktZeile = new(
        @"^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2} · [A-Za-z0-9. -]+ · (Inhalte laden|Erste Karte|Fachrichtung wechseln (si|ae|dpa|dv)|Spiel betreten): \d+ ms$",
        RegexOptions.CultureInvariant);

    private static async Task<int> P11W3MesspunkteAsync(MainView ansicht)
    {
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("Messpunkte (W3): FEHLER, " + text);
            funde++;
        }

        var erwartet = new List<string>();
        if (MitLernen)
        {
            erwartet.AddRange([Messpunkte.Name(Messpunkte.Punkt.InhalteLaden), Messpunkte.Name(Messpunkte.Punkt.ErsteKarte),
                Messpunkte.Name(Messpunkte.Punkt.FachrichtungWechseln)]);
        }
        if (MitSpiel && ansicht.GebauteSeiten.TryGetValue(Seiten.Spiel, out var seite) && seite is Spiel.SpielSeite { ErstesBildMs: not null })
        {
            erwartet.Add(Messpunkte.Name(Messpunkte.Punkt.SpielBetreten));
        }
        var pfad = Path.Combine(Core.Daten.Ablage.Datenordner(), Messpunkte.DateiName);
        string[] zeilen = [];
        // geschrieben wird im Hintergrund: hoechstens 5 s warten
        for (var i = 0; i < 50; i++)
        {
            zeilen = Fehlerprotokoll.Anzeigen(pfad, Messpunkte.DateiMax).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (erwartet.All(name => zeilen.Any(z => z.Contains(" · " + name, StringComparison.Ordinal))))
            {
                break;
            }
            await Task.Delay(100);
        }
        foreach (var name in erwartet.Where(name => !zeilen.Any(z => z.Contains(" · " + name, StringComparison.Ordinal))))
        {
            Fund("kein Messpunkt \"" + name + "\" in " + Messpunkte.DateiName);
        }
        foreach (var zeile in zeilen.Where(z => !MesspunktZeile.IsMatch(z)))
        {
            Fund("Zeile in fremder Form: \"" + zeile + "\"");
        }
        var bericht = Problembericht.Bauen(Zustand.Lernstand, Update.Anzeige, "PC", Zustand.Werte, DateTime.Now);
        if (zeilen.Length > 0 && !bericht.Contains("--- " + Messpunkte.DateiName + " ---", StringComparison.Ordinal))
        {
            Fund("Problembericht ohne Abschnitt " + Messpunkte.DateiName);
        }
        Messwerte.Notiere("Messpunkte (W3): " + zeilen.Length + " Zeilen, erwartet " + string.Join(", ", erwartet)
            + (funde == 0 ? " (gruen)" : " (rot)"));
        return funde;
    }
}

/// <summary>
/// Phase 11 W3 (Weisung P11-21): eigener Lauf nur mit der Layoutpruefung (Teil "layout"), fuer die CI mit
/// Windows-Skalierung 100 % auf 1920 x 1080 (volle PC-Groesse 1360 x 880, Sammelliste 196) und 150 % (Punkt
/// "Windows > 100 % Skalierung"). Erst jede Seite einmal oeffnen wie im Lernteil, dann die ganze Layoutpruefung
/// (alle Schriftstufen, Groessen, Ansichten, Namen fuer den Screenreader). LERNPLATTFORM_SKALIERUNG_ERWARTET
/// (z. B. 1.5) prueft, dass das Programm die Skalierung wirklich sieht (sonst falsches Gruen). Zeiten sind hier nur
/// Angabe, geurteilt wird allein ueber Layoutfunde und die Skalierung.
/// </summary>
public static partial class AutoMessung
{
    public const string TeilLayout = "layout";

    /// <summary>Schalter: die PC-Groesse 1360 x 880 muss erreicht werden (lauf.sh unter Linux, ci.yml Windows 100 %).</summary>
    private static bool VollePcGroesse => Environment.GetEnvironmentVariable("LERNPLATTFORM_VOLLE_PC_GROESSE") == "1";

    private static async Task<int> P11W3LayoutLaufAsync(MainView ansicht)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var funde = 0;
        Messwerte.Notiere("Start-Test Teil: " + TeilLayout);
        var inhalte = await Zustand.Inhalte;
        foreach (var id in Texte.NavigationPc.SelectMany(p => p.Unter.Prepend(p)).Select(p => p.Id)
            .Concat(Texte.NavigationHandy.Select(p => p.Id)).Concat([Seiten.Lernen, Seiten.Hilfe, Seiten.Suche])
            .Where(id => id != Seiten.Spiel).Distinct().ToList())
        {
            await ansicht.ZeigeAsync(id);
        }
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        if (ansicht.GebauteSeiten[Seiten.Dashboard] is DashboardSeite dashboard)
        {
            await dashboard.Fertig.Task;
        }
        if (ansicht.GebauteSeiten.GetValueOrDefault(Seiten.Karteikarten) is KarteikartenSeite karten)
        {
            await karten.Geladen;
        }
        if (ansicht.GebauteSeiten.GetValueOrDefault(Seiten.Trainer) is TrainerSeite trainer)
        {
            await trainer.Geladen;
        }
        await ansicht.BildFertigAsync();
        var skalierung = Avalonia.Controls.TopLevel.GetTopLevel(ansicht)?.RenderScaling ?? 1;
        var bildschirm = Avalonia.Controls.TopLevel.GetTopLevel(ansicht) is Avalonia.Controls.Window fenster
            ? fenster.Screens.ScreenFromVisual(fenster) ?? fenster.Screens.Primary : null;
        Messwerte.Notiere(string.Create(inv, $"Skalierung des Programms: {skalierung * 100:0} %, Bildschirm {bildschirm?.Bounds.Width ?? 0}x{bildschirm?.Bounds.Height ?? 0}"));
        if (Environment.GetEnvironmentVariable("LERNPLATTFORM_SKALIERUNG_ERWARTET") is { Length: > 0 } soll)
        {
            if (!double.TryParse(soll, System.Globalization.NumberStyles.Float, inv, out var erwartet) || Math.Abs(erwartet - skalierung) > 0.01)
            {
                Messwerte.Notiere(string.Create(inv, $"  FEHLER: Skalierung {skalierung:0.##} statt {soll}"));
                funde++;
            }
        }
        funde += await LayoutAsync(ansicht, inhalte);
        Messwerte.Notiere(funde == 0 ? "URTEIL: gruen" : string.Create(inv, $"URTEIL: rot ({funde} Funde)"));
        return funde;
    }
}
