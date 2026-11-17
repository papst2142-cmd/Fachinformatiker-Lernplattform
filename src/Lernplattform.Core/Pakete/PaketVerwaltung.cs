using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Pakete;

/// <summary>Zustand einer Zeile der Karte „Inhaltspakete“.</summary>
public enum PaketZustand
{
    Geladen,
    NeuerStand,
    NichtGeladen,
    Fehlerhaft,
}

/// <summary>Knopf einer Zeile.</summary>
public enum PaketKnopf
{
    Laden,
    Aktualisieren,
    NeuLaden,
    Entfernen,
}

/// <summary>Eine Zeile der Liste (geladen und/oder angeboten).</summary>
/// <param name="Art">Sammelliste 376: Art des Pakets (geladen: aus dem Kopf, sonst laut Angebot; null = unbekannt).</param>
public sealed record PaketZeile(string Kennung, string Name, string Text, long Groesse, PaketZustand Zustand, string ZustandText,
    IReadOnlyList<PaketKnopf> Knoepfe, PaketAngebot? Angebot, string? Art = null)
{
    /// <summary>
    /// Sammelliste 376 (A 0.89.0, Weisung P12-297 D): steht die Zeile im Fahrplan am Bahnhof? Nur Pakete, deren Art vor dem
    /// Laden als <see cref="Paket.ArtStadt"/> bekannt ist (Kopf eines geladenen Pakets oder Angebot); ein Paket unbekannter
    /// Art oder ein Angebot ohne Art steht nur in den Optionen. Gegenprobe <see cref="ArtAltFuerTests"/> (Stand A 0.88.0).
    /// </summary>
    public bool ImFahrplan => ArtAlt.Value ? !ZustandText.Contains(PaketTexte.ArtUnbekannt, StringComparison.Ordinal) : Art == Paket.ArtStadt;

    private static readonly AsyncLocal<bool> ArtAlt = new();

    /// <summary>Nur Gegenprobe Sammelliste 376: Fahrplan wie bis A 0.88.0 (jede Zeile ausser „unbekannte Art“).</summary>
    public static bool ArtAltFuerTests
    {
        get => ArtAlt.Value;
        set => ArtAlt.Value = value;
    }

    public static string KnopfText(PaketKnopf knopf) => knopf switch
    {
        PaketKnopf.Laden => PaketTexte.Laden,
        PaketKnopf.Aktualisieren => PaketTexte.Aktualisieren,
        PaketKnopf.NeuLaden => PaketTexte.NeuLaden,
        _ => PaketTexte.Entfernen,
    };
}

/// <summary>
/// Welle A 0.87.0 (Plan P12-175 Abschnitte 2 und 3): verbindet Ablage (Datenordner) und Quelle zu den Zeilen der Karte.
/// Geladenes geht immer ohne Netz (<see cref="Zeilen"/> liest nur den Datenordner und das letzte Angebot); nichts laedt von
/// allein, nur <see cref="LadenAsync"/> auf Knopfdruck. Ein Fehler beim Laden steht rot mit Grund an der Zeile (nie Absturz).
/// </summary>
public sealed class PaketVerwaltung
{
    private readonly PaketAblage _ablage;
    private readonly Func<IPaketQuelle> _quelle;
    private readonly string _app;
    private readonly Dictionary<string, string> _fehler = new(StringComparer.Ordinal);
    private IReadOnlyList<PaketAngebot> _angebote = [];

    public PaketVerwaltung(PaketAblage ablage, Func<IPaketQuelle> quelle, string app)
    {
        _ablage = ablage;
        _quelle = quelle;
        _app = app;
        _ablage.Aufraeumen();
    }

    public PaketAblage Ablage => _ablage;

    /// <summary>Zeitpunkt der letzten erfolgreichen Suche (null = noch nie).</summary>
    public DateTime? Gesucht { get; private set; }

    /// <summary>Letzte Suche ohne Verbindung.</summary>
    public bool OhneVerbindung { get; private set; }

    /// <summary>Text der letzten fehlgeschlagenen Suche (null = ok).</summary>
    public string? SuchFehler { get; private set; }

    public IReadOnlyList<PaketAngebot> Angebote => _angebote;

    /// <summary>Fragt die Quelle ab (nur auf Knopfdruck); ohne Netz bleibt das alte Angebot und <see cref="OhneVerbindung"/> ist wahr.</summary>
    public async Task SuchenAsync(CancellationToken abbruch = default)
    {
        try
        {
            _angebote = await _quelle().ListeAsync(abbruch).ConfigureAwait(false);
            Gesucht = DateTime.Now;
            OhneVerbindung = false;
            SuchFehler = null;
        }
        catch (PaketFehler f)
        {
            OhneVerbindung = f.KeineVerbindung;
            SuchFehler = f.KeineVerbindung ? PaketTexte.KeineVerbindung : f.Message;
        }
    }

    /// <summary>Alle Zeilen: geladene Pakete und Angebote, nach Name.</summary>
    public IReadOnlyList<PaketZeile> Zeilen()
    {
        var geladen = _ablage.Liste(_app).ToDictionary(g => g.Kennung, StringComparer.Ordinal);
        var angebote = _angebote.ToDictionary(a => a.Kennung, StringComparer.Ordinal);
        var zeilen = new List<PaketZeile>();
        foreach (var kennung in geladen.Keys.Union(angebote.Keys).Union(_fehler.Keys))
        {
            geladen.TryGetValue(kennung, out var g);
            angebote.TryGetValue(kennung, out var a);
            if (g is null && a is null)
            {
                continue;
            }
            var name = g?.Kopf?.Name ?? a?.Name ?? kennung;
            var text = g?.Kopf?.Text ?? a?.Text ?? "";
            var groesse = g?.Kopf?.Dateien.Values.Sum(d => d.Groesse) ?? a?.Groesse ?? 0;
            var fehler = _fehler.GetValueOrDefault(kennung) ?? g?.Grund;
            var art = g?.Kopf?.Art ?? a?.Art;
            PaketZeile Zeile(PaketZustand z, string zText, params PaketKnopf[] knoepfe) => new(kennung, name, text, groesse, z, zText, knoepfe, a, art);
            if (fehler is not null)
            {
                var knoepfe = new List<PaketKnopf>();
                // Sammelliste 374: „Neu laden“ nur, wenn ein neues Laden den Grund loesen kann (sonst hilft nur ein App-Update)
                if (a is not null && PaketTexte.NeuLadenHilft(fehler))
                {
                    knoepfe.Add(PaketKnopf.NeuLaden);
                }
                if (g is not null)
                {
                    knoepfe.Add(PaketKnopf.Entfernen);
                }
                zeilen.Add(Zeile(PaketZustand.Fehlerhaft, PaketTexte.Format(PaketTexte.ZustandFehler, fehler), [.. knoepfe]));
            }
            else if (g is not null && a is not null && a.Stand > g.Stand)
            {
                zeilen.Add(Zeile(PaketZustand.NeuerStand, PaketTexte.Format(PaketTexte.ZustandNeu, g.Stand, a.Stand), PaketKnopf.Aktualisieren, PaketKnopf.Entfernen));
            }
            else if (g is not null)
            {
                zeilen.Add(Zeile(PaketZustand.Geladen, PaketTexte.Format(PaketTexte.ZustandGeladen, g.Stand), PaketKnopf.Entfernen));
            }
            else
            {
                zeilen.Add(Zeile(PaketZustand.NichtGeladen, PaketTexte.ZustandNicht, PaketKnopf.Laden));
            }
        }
        return [.. zeilen.OrderBy(z => z.Name, StringComparer.CurrentCulture).ThenBy(z => z.Kennung, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Laedt ein Angebot: erst nach <c>_laden/</c>, dann pruefen und in einem Schritt einsetzen. Abbruch (Knopf, Netz weg)
    /// laesst den alten Stand unberuehrt. Gibt null bei Erfolg zurueck, sonst den Grund (steht dann rot an der Zeile).
    /// </summary>
    public async Task<string?> LadenAsync(PaketAngebot angebot, IProgress<long>? fortschritt, CancellationToken abbruch)
    {
        _fehler.Remove(angebot.Kennung);
        string? grund;
        try
        {
            var ziel = _ablage.Ladedatei(angebot.Kennung, angebot.Stand);
            await _quelle().LadenAsync(angebot, ziel, fortschritt, abbruch).ConfigureAwait(false);
            abbruch.ThrowIfCancellationRequested();
            grund = _ablage.Einsetzen(ziel, angebot.Kennung, angebot.Stand, _app).Grund;
        }
        catch (OperationCanceledException) when (abbruch.IsCancellationRequested)
        {
            _ablage.Aufraeumen();
            return PaketTexte.Abgebrochen;
        }
        catch (PaketFehler f)
        {
            grund = f.Message;
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            grund = PaketTexte.Format(PaketTexte.Speichern, x.Message);
        }
        _ablage.Aufraeumen();
        if (grund is not null)
        {
            _fehler[angebot.Kennung] = grund;
        }
        return grund;
    }

    /// <summary>
    /// Welle A 0.88.0 (Plan P12-175 Abschnitt 5, Nico 5a): Grund, warum ein Paket gerade nicht entfernt werden darf (die Figur ist
    /// in seiner Stadt), sonst null. Setzt das Spiel; ohne Spiel (Tests) nie gesperrt.
    /// </summary>
    public Func<string, string?>? EntfernenSperre { get; set; }

    /// <summary>Grund der Sperre fuer <paramref name="kennung"/> (null = Entfernen geht).</summary>
    public string? EntfernenGesperrt(string kennung) => EntfernenSperre?.Invoke(kennung);

    /// <summary>
    /// Welle A 0.88.0: die geladenen, gueltigen Staedte (Art <c>stadt</c>) mit dem Pfad ihrer Stadt-Datei im Datenordner.
    /// Liest nur den Datenordner (geht ohne Netz).
    /// </summary>
    public IReadOnlyList<(PaketKopf Kopf, string Datei)> Staedte() =>
        [.. _ablage.Liste(_app).Where(g => g.Kopf is { Art: Paket.ArtStadt })
            .Select(g => (g.Kopf!, Path.Combine(_ablage.Ordner, g.Kennung, g.Stand.ToString(System.Globalization.CultureInfo.InvariantCulture), Paket.StadtDatei)))];

    /// <summary>Entfernt ein Paket aus dem Datenordner (auch ein fehlerhaftes); true bei Erfolg. Gesperrt (<see cref="EntfernenGesperrt"/>) = false, nichts geaendert.</summary>
    public bool Entfernen(string kennung)
    {
        if (EntfernenGesperrt(kennung) is not null)
        {
            return false;
        }
        _fehler.Remove(kennung);
        return _ablage.Entfernen(kennung);
    }
}
