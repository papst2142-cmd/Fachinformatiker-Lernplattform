using System.Text.RegularExpressions;
using Lernplattform.Core.Diagnose;
using Lernplattform.Programm;
using Lernplattform.Views;
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
