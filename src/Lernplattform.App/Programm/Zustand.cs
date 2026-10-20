using System.Diagnostics;
using System.Text.Json.Nodes;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;
using Lernplattform.Core.Inhalte;
using Lernplattform.Design;
using Lernplattform.Messung;

namespace Lernplattform.Programm;

/// <summary>
/// Phase 1: gemeinsamer Zustand der Oberflaeche. Liest beim Start einmal die
/// Einstellungen (K3), wendet Darstellung, Farbwelt und Schriftstufe an (K5) und
/// laedt die Lerninhalte im Hintergrund (Weisung P1-2, 3.3: die erste Ansicht
/// wartet nicht darauf).
/// </summary>
public static class Zustand
{
    public static Einstellungen Einstellungen { get; } = new();

    /// <summary>Zuletzt gelesene Einstellungen (mit Vorgaben).</summary>
    public static JsonObject Werte { get; private set; } = [];

    /// <summary>(Titel, Text), wenn einstellungen.json beim Start beschaedigt war (genau einmal).</summary>
    public static (string Titel, string Text)? DefektHinweis { get; private set; }

    /// <summary>Lerninhalte; laeuft im Hintergrund, null wenn das Laden scheiterte.</summary>
    public static Task<Lerninhalte?> Inhalte { get; private set; } = Task.FromResult<Lerninhalte?>(null);

    /// <summary>Gesamtzahlen und Zuordnungen fuer das Dashboard, nach den Lerninhalten.</summary>
    public static Task<DashboardInhalte?> DashboardInhalte { get; private set; } = Task.FromResult<DashboardInhalte?>(null);

    public static void Starten()
    {
        DefektHinweis = Einstellungen.DefektHinweis();
        Werte = Einstellungen.Laden();
        DesignAnwenden();
        Inhalte = Task.Run(InhalteLaden);
        DashboardInhalte = Inhalte.ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully || t.Result is null)
            {
                return null;
            }
            try
            {
                return new DashboardInhalte(t.Result, EingebauteDatei("spiel/balancing"));
            }
            catch (Exception fehler)
            {
                Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
                return null;
            }
        }, TaskScheduler.Default);
    }

    private static Lerninhalte? InhalteLaden()
    {
        var uhr = Stopwatch.StartNew();
        try
        {
            var inhalte = Lerninhalte.Laden(EingebauteDatei);
            Messwerte.Notiere(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"Lerninhalte im Hintergrund geladen: {uhr.Elapsed.TotalMilliseconds:F0} ms"));
            return inhalte;
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
            Messwerte.Notiere("Lerninhalte: FEHLER " + fehler.GetType().Name);
            return null;
        }
    }

    /// <summary>inhalte/&lt;name&gt;.json aus dem Programm selbst.</summary>
    private static byte[] EingebauteDatei(string name)
    {
        using var strom = typeof(Zustand).Assembly.GetManifestResourceStream("inhalte/" + name + ".json")
            ?? throw new FileNotFoundException("Eingebaute Inhalte fehlen", name + ".json");
        using var puffer = new MemoryStream();
        strom.CopyTo(puffer);
        return puffer.ToArray();
    }

    /// <summary>Farbwelt (mit eigenen Farben) und Schriftstufe aus den Einstellungen anwenden.</summary>
    public static void DesignAnwenden()
    {
        FarbenAnwenden.Anwenden(Palette(Werte));
        Schrift.Anwenden(SchriftFaktor(Einstellungen.Schriftgroesse(Werte)));
    }

    /// <summary>
    /// Farbwelt der gespeicherten Darstellung; sind fuer diese Darstellung gueltige eigene
    /// Farben gespeichert, gelten sie (am Startwert exakt die Farbwelt, wie values_palette).
    /// </summary>
    public static Palette Palette(JsonObject werte)
    {
        var (d, g, h) = (Einstellungen.Darstellung(werte), Einstellungen.Grundfarbe(werte), Einstellungen.Hintergrund(werte));
        return EigeneFarben.Gespeichert(werte[EigeneFarben.Schluessel]).TryGetValue(d, out var eigene)
            ? EigeneFarben.WertePalette(eigene, d, g, h)
            : Farbwelt.Berechne(d, g, h);
    }

    public static double SchriftFaktor(string id) => id switch
    {
        "gross" => 1.15,
        "sehr_gross" => 1.3,
        "extra_gross" => 1.6,
        "riesig" => 2.0,
        _ => 1.0,
    };

    /// <summary>
    /// Mehrere Werte auf einmal aendern (auch Schluessel entfernen) und speichern.
    /// false: nicht gespeichert, die Datei bleibt unveraendert.
    /// </summary>
    public static bool Aendere(Action<JsonObject> aenderung)
    {
        var werte = Einstellungen.Laden();
        if (Einstellungen.NurAnzeige)
        {
            return false;
        }
        aenderung(werte);
        if (!Einstellungen.Speichern(werte))
        {
            return false;
        }
        Werte = Einstellungen.Laden();
        return true;
    }

    /// <summary>
    /// Einen Wert speichern und anwenden. false: nicht gespeichert (z. B. Datei gesperrt,
    /// Weisung P1-2 3.1); die Oberflaeche zeigt dann "Speichern fehlgeschlagen".
    /// </summary>
    public static bool Setze(string schluessel, JsonNode? wert)
    {
        if (!Einstellungen.Setze(schluessel, wert))
        {
            return false;
        }
        Werte = Einstellungen.Laden();
        return true;
    }
}
