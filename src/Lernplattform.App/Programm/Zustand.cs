using System.Diagnostics;
using System.Text.Json.Nodes;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
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

    /// <summary>
    /// Lerninhalte der gewaehlten Fachrichtung (Phase 9 W2: gemeinsame plus eigene, <see cref="Lerninhalte.Fuer"/>);
    /// laeuft im Hintergrund, null wenn das Laden scheiterte. Das Spiel nimmt davon nur <see cref="Lerninhalte.Bisher"/>.
    /// </summary>
    public static Task<Lerninhalte?> Inhalte { get; private set; } = Task.FromResult<Lerninhalte?>(null);

    /// <summary>Phase 9 W2: alle Inhalte (inhalte und inhalte_csharp/lernen), Grundlage jeder Sicht.</summary>
    public static Task<Lerninhalte?> AlleInhalte { get; private set; } = Task.FromResult<Lerninhalte?>(null);

    /// <summary>Fachrichtung der aktuellen <see cref="Inhalte"/> (si, ae, dpa, dv).</summary>
    public static string Sicht { get; private set; } = Core.Inhalte.Fachrichtungen.Vorgabe;

    /// <summary>Messpunkt "Inhalte laden" (alle Ordner, kalt, einmal je Programmstart); null bis fertig oder bei Fehler.</summary>
    public static double? InhalteLadenMs { get; private set; }

    /// <summary>Gesamtzahlen und Zuordnungen fuer das Dashboard, nach den Lerninhalten.</summary>
    public static Task<DashboardInhalte?> DashboardInhalte { get; private set; } = Task.FromResult<DashboardInhalte?>(null);

    private static Lernstand? _lernstand;
    private static LernstandSchreiber? _schreiber;

    /// <summary>Phase 2: gemeinsamer Lernstand (lernplattform.db im Datenordner), beim ersten Gebrauch geoeffnet.</summary>
    public static Lernstand Lernstand => LazyInitializer.EnsureInitialized(ref _lernstand, ref _lernstandSperre,
        () => new Lernstand(null, text => Fehlermeldung?.Invoke(text)));

    private static object? _lernstandSperre;

    /// <summary>Speichert Antworten in den gemeinsamen Lernstand.</summary>
    public static LernstandSchreiber Schreiber => LazyInitializer.EnsureInitialized(ref _schreiber, ref _schreiberSperre,
        () => new LernstandSchreiber(Lernstand));

    private static object? _schreiberSperre;

    /// <summary>Verstaendliche Meldung je Fehlerart des Lernstands (Oberflaeche zeigt "Datenbankfehler").</summary>
    public static Action<string>? Fehlermeldung { get; set; }

    /// <summary>Beim Beenden des Programms (z. B. Aufgedecktes als angesehen speichern, flush_inputs).</summary>
    public static event Action? BeimBeenden;

    /// <summary>
    /// W3: offene Eingaben speichern (flush_inputs(revealed=False)): das Abschlussprojekt vor Sicherung
    /// und Uebernahme (Diff B), beim Verlassen der App am Handy und beim Beenden.
    /// </summary>
    public static event Action? BeiEingabenSpeichern;

    public static void EingabenSpeichern() => BeiEingabenSpeichern?.Invoke();

    public static void Beenden()
    {
        EingabenSpeichern();
        BeimBeenden?.Invoke();
    }

    private static IReadOnlyList<(string Tabelle, string Alt, string Neu)>? _umbenennungen;

    /// <summary>inhalte/umbenennungen.json (apply_question_renames bei der Uebernahme).</summary>
    public static IReadOnlyList<(string Tabelle, string Alt, string Neu)> Umbenennungen =>
        _umbenennungen ??= Core.Daten.Umbenennungen.Laden(EingebauteDatei);

    /// <summary>StatusBook(db): Antworten neu lesen.</summary>
    public static StatusBuch BuchLesen() => new(Antwortbuch.Lesen(Lernstand));

    /// <summary>
    /// Zuordnung zum Rahmenplan (rechnet einige hundert Millisekunden), wie Pythons frp.warm_up
    /// gleich nach den Lerninhalten im Hintergrund vorbereitet; null wenn die Inhalte fehlen.
    /// </summary>
    public static Task<RahmenplanGewicht?> Gewicht { get; private set; } = Task.FromResult<RahmenplanGewicht?>(null);

    /// <summary>fresh_order(quelle): Rahmenplan-Reihenfolge der neuen Inhalte, je Aufruf mit den aktuellen Einstellungen.</summary>
    public static Func<List<string>, List<string>>? NeuReihenfolge(RahmenplanGewicht? gewicht, string quelle) =>
        gewicht?.NeuReihenfolge(quelle, RahmenplanWerte.Aus(Werte, gewicht), DateOnly.FromDateTime(DateTime.Now));

    /// <summary>
    /// Phase 2, L8: Suchtext der Lerninhalte, einmal je Programmlauf im Hintergrund vorbereitet
    /// (content_search_index); null wenn die Inhalte fehlen.
    /// </summary>
    public static Task<Core.Suche.Suchtext?> Suchtext { get; private set; } = Task.FromResult<Core.Suche.Suchtext?>(null);

    public static void Starten()
    {
        // Phase 5 BE2 (remove_stale_temp): Hilfsdatei eines abgebrochenen „Alles ersetzen“ entfernen
        Einstellungen.HilfsdateiEntfernen();
        DefektHinweis = Einstellungen.DefektHinweis();
        Werte = Einstellungen.Laden();
        Core.Daten.Abgleich.GeheimnisseSetzen(Werte);   // A7: Zugangsschluessel nie in die Fehlerdatei
        DesignAnwenden();
        AlleInhalte = Task.Run(InhalteLaden);
        Inhalte = AlleInhalte.ContinueWith(t => !t.IsCompletedSuccessfully || t.Result is not { } alle ? null
            : _ersteSicht is { } erste && erste.Sicht == Sicht ? erste : alle.Fuer(Sicht), TaskScheduler.Default);
        AbgeleiteteStarten();
    }

    /// <summary>
    /// Phase 9 W2: Sicht auf eine andere Fachrichtung umstellen (nach der Wahl in den Optionen, beim Start und
    /// nach Abgleich, Uebernahme oder Ruecksicherung). Nur Anzeige: der Lernstand bleibt, wie er ist.
    /// false: schon diese Sicht.
    /// </summary>
    public static bool SichtSetzen(string fachrichtung)
    {
        if (fachrichtung == Sicht || !Core.Inhalte.Fachrichtungen.Werte.Contains(fachrichtung))
        {
            return false;
        }
        // fertige Sicht merken: ein Wechsel zurueck braucht Inhalte, Rahmenplan-Zuordnung, Suchtext und Dashboard nicht neu
        if (Inhalte.IsCompletedSuccessfully && Inhalte.Result is not null)
        {
            Gemerkt[Sicht] = (Inhalte, Gewicht, Suchtext, DashboardInhalte);
        }
        Sicht = fachrichtung;
        if (Gemerkt.TryGetValue(fachrichtung, out var gemerkt))
        {
            (Inhalte, Gewicht, Suchtext, DashboardInhalte) = gemerkt;
            return true;
        }
        Inhalte = AlleInhalte.ContinueWith(t => t.IsCompletedSuccessfully ? t.Result?.Fuer(fachrichtung) : null, TaskScheduler.Default);
        AbgeleiteteStarten();
        return true;
    }

    private static readonly Dictionary<string, (Task<Lerninhalte?> Inhalte, Task<RahmenplanGewicht?> Gewicht,
        Task<Core.Suche.Suchtext?> Suchtext, Task<DashboardInhalte?> Dashboard)> Gemerkt = new(StringComparer.Ordinal);

    /// <summary>Start-Test: gemerkte Sichten vergessen, damit jede Messung des Wechsels alles neu baut (streng).</summary>
    public static void SichtenVergessen() => Gemerkt.Clear();

    /// <summary>Phase 9 W2: die Fachrichtung wurde gespeichert (Optionen); die Oberflaeche stellt die Sicht um.</summary>
    public static event Action? AusbildungGeaendert;

    public static void AusbildungGespeichert() => AusbildungGeaendert?.Invoke();

    /// <summary>Rahmenplan-Zuordnung, Suchtext und Dashboard-Zahlen zur aktuellen Sicht im Hintergrund.</summary>
    private static void AbgeleiteteStarten()
    {
        Gewicht = Inhalte.ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully || t.Result is null)
            {
                return null;
            }
            try
            {
                var uhr = Stopwatch.StartNew();
                var gewicht = new RahmenplanGewicht(t.Result);
                Messwerte.Notiere(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"Rahmenplan-Zuordnung im Hintergrund vorbereitet: {uhr.Elapsed.TotalMilliseconds:F0} ms"));
                return gewicht;
            }
            catch (Exception fehler)
            {
                Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
                return null;
            }
        }, TaskScheduler.Default);
        Suchtext = Inhalte.ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully || t.Result is null)
            {
                return null;
            }
            try
            {
                var uhr = Stopwatch.StartNew();
                var text = new Core.Suche.Suchtext(t.Result);
                Messwerte.Notiere(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"Suchtext im Hintergrund vorbereitet: {uhr.Elapsed.TotalMilliseconds:F0} ms"));
                return text;
            }
            catch (Exception fehler)
            {
                Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
                return null;
            }
        }, TaskScheduler.Default);
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

    private static Lerninhalte? _ersteSicht;

    private static Lerninhalte? InhalteLaden()
    {
        try
        {
            // Phase 9 W2: gewaehlte Fachrichtung vor dem Laden lesen (nicht gemessen), dann alle Ordner kalt
            Sicht = Core.Pruefung.Ausbildung.Aktuell(Schreiber);
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
        var uhr = Stopwatch.StartNew();
        try
        {
            var inhalte = Lerninhalte.MitLernenSicher(() => Lerninhalte.Laden(EingebauteDatei), LernenDatei, fehler =>
            {
                Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
                Messwerte.Notiere("Lerninhalte: FEHLER in inhalte_csharp/lernen " + fehler.GetType().Name + ", nur die bisherigen Inhalte");
            });
            _ersteSicht = inhalte.Fuer(Sicht);
            InhalteLadenMs = uhr.Elapsed.TotalMilliseconds;
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

    /// <summary>W3: Fragen fuers Fachgespraech (inhalte/fachgespraech.json, _load_questions); leer bei einem Lesefehler.</summary>
    public static IReadOnlyList<Core.Projekt.ProjektFrage> Fachfragen() => LazyInitializer.EnsureInitialized(ref _fachfragen, () =>
    {
        try
        {
            return Core.Projekt.Fachgespraech.Laden(EingebauteDatei("fachgespraech"));
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
            return [];
        }
    });

    private static List<Core.Projekt.ProjektFrage>? _fachfragen;

    /// <summary>
    /// Phase 9 W2: Fragen fuers Fachgespraech der aktuellen Sicht (SI alle, sonst die gemeinsamen nach
    /// einteilung_bisher.json); die Liste liest bei jedem Zugriff die aktuelle Sicht.
    /// </summary>
    public static IReadOnlyList<Core.Projekt.ProjektFrage> FachfragenSicht { get; } = new Core.Projekt.SichtListe<Core.Projekt.ProjektFrage>(() =>
    {
        var alle = Fachfragen();
        if (Sicht == Core.Inhalte.Fachrichtungen.Systemintegration || !AlleInhalte.IsCompletedSuccessfully || AlleInhalte.Result is not { } inhalte)
        {
            return alle;
        }
        return alle.Where(f => inhalte.Einteilung.Wert(Einteilung.Fachgespraech, f.Id) == Core.Inhalte.Fachrichtungen.Gemeinsam).ToList();
    });

    /// <summary>inhalte_csharp/lernen/&lt;name&gt;.json aus dem Programm selbst, null wenn es die Datei nicht gibt.</summary>
    private static byte[]? LernenDatei(string name)
    {
        using var strom = typeof(Zustand).Assembly.GetManifestResourceStream("inhalte_csharp/lernen/" + name + ".json");
        if (strom is null)
        {
            return null;
        }
        using var puffer = new MemoryStream();
        strom.CopyTo(puffer);
        return puffer.ToArray();
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

    /// <summary>Einstellungen neu lesen (nach dem Abgleich: sync_last), Geheimnisfilter setzen.</summary>
    public static void EinstellungenNeuLesen()
    {
        Werte = Einstellungen.Laden();
        Core.Daten.Abgleich.GeheimnisseSetzen(Werte);   // A7: Zugangsschluessel nie in die Fehlerdatei
    }

    /// <summary>
    /// Mehrere Werte auf einmal aendern (auch Schluessel entfernen) und speichern.
    /// false: nicht gespeichert, die Datei bleibt unveraendert.
    /// </summary>
    public static bool Aendere(Action<JsonObject> aenderung)
    {
        if (!Einstellungen.Aendern(aenderung))
        {
            return false;
        }
        Werte = Einstellungen.Laden();
        Core.Daten.Abgleich.GeheimnisseSetzen(Werte);   // A7: Zugangsschluessel nie in die Fehlerdatei
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
        Core.Daten.Abgleich.GeheimnisseSetzen(Werte);   // A7: Zugangsschluessel nie in die Fehlerdatei
        return true;
    }
}
