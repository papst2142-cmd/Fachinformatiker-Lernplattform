using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Fund 320 (Weisung P12-226): die Pruefung draussen rechnet mit dem Probestand (<see cref="StadtbauSpiel.Probestand"/>, alle
/// Orte sichtbar). Dort heissen manche Orte wie im Spiel nach dem Spielstand: der Gewerbehof nach der Firma des Probestands
/// („Probe“), die Filiale nach dem Vorschlag der Regeln. Im Werkzeug soll ein Fund den Namen der Karte nennen
/// („Gewerbehof Am Stellwerk“, „Filiale Lindenau“), wie seit Sammelliste 274 die Funde an Tueren. Dafuer ersetzt
/// <see cref="Umbenennen"/> in jedem Fundtext „Name im Probestand“ durch „Name der Karte“, wenn beide verschieden sind.
/// Nur Werkzeug; das Spiel und seine Pruefung bleiben unveraendert.
/// </summary>
public static class ProbeNamen
{
    /// <summary>Gegenprobe: Funde nennen die Namen des Probestands wie bis S 0.46.1 (nur Tests, je Ablauf).</summary>
    public static bool AltFuerTests { get => Alt.Value; set => Alt.Value = value; }
    private static readonly AsyncLocal<bool> Alt = new();

    /// <summary>Name im Probestand → Name der Karte, fuer jeden Ort, bei dem beide verschieden sind (leer, wenn die Stadt nicht laedt).</summary>
    public static Dictionary<string, string> Ersatz(StadtFassung stand, PyObjekt inhalt)
    {
        var ersatz = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Alt.Value)
        {
            return ersatz;
        }
        try
        {
            var stadt = stand.StadtFuer(stand.MitKarte(inhalt), nachsichtig: true);
            var probe = StadtbauSpiel.Probestand(stadt.Inhalt).Stand;
            foreach (var o in stadt.Orte(probe))
            {
                var id = Py.Str(o["id"]);
                var probeName = o["name"] as string ?? id;
                var karte = TuerBuendig.OrtName(stand, id) ?? Lesbar(id);
                if (probeName.Length > 0 && probeName != karte)
                {
                    ersatz.TryAdd(probeName, karte);
                }
            }
        }
        catch (Exception e) when (e is StadtbauFehler or KeyNotFoundException or InvalidCastException or ArgumentException or InvalidOperationException)
        {
            // laedt die Stadt nicht, meldet das die Pruefung selbst; die Namen bleiben dann wie sie sind
        }
        return ersatz;
    }

    /// <summary>Lesbarer Name einer Kennung ohne Namen in der Karte: „filiale“ → „Filiale“, „bahnhof_neu“ → „Bahnhof neu“.</summary>
    public static string Lesbar(string id) =>
        id.Length == 0 ? id : char.ToUpperInvariant(id[0]) + id[1..].Replace('_', ' ');

    /// <summary>Fundtext mit den Namen der Karte (jedes „Name“ in Anfuehrungszeichen, das im Ersatz steht).</summary>
    public static StadtbauFund Umbenennen(IReadOnlyDictionary<string, string> ersatz, StadtbauFund f)
    {
        if (ersatz.Count == 0)
        {
            return f;
        }
        var text = f.Text;
        foreach (var (alt, neu) in ersatz)
        {
            text = text.Replace("„" + alt + "“", "„" + neu + "“", StringComparison.Ordinal);
        }
        return text == f.Text ? f : f with { Text = text };
    }
}
