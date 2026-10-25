using Lernplattform.Core.Inhalte;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W3: die Themenlisten, die <c>fisi_game.py</c> aus <c>fisi_core</c> holt (Python 0.63.2,
/// Z. 34-50): CAT_ORDER, CAT_NAME, CAT_KEY, CAT_TOPICS, TOPIC_ORDER, TOPIC_CAT, TOPIC_SHORT.
/// Python liest sie beim Import aus <c>inhalte/themen.json</c>; hier stellt sie das Programm einmal
/// mit <see cref="Standard"/> bereit (wie ein Modulwert), Tests ebenso.
/// </summary>
public sealed class SpielThemen
{
    /// <summary>Die Themen fuer jeden Spielstand ohne eigene Angabe (Programm und Tests setzen sie einmal).</summary>
    public static SpielThemen? Standard { get; set; }

    /// <summary>Die gesetzten Themen (wie die Modulwerte in Python); ohne <see cref="Standard"/> ein Fehler.</summary>
    public static SpielThemen Jetzt => Standard ?? throw new InvalidOperationException("SpielThemen.Standard ist nicht gesetzt");

    /// <summary>CAT_ORDER: Kurznamen der Fachbereiche in fester Reihenfolge.</summary>
    public IReadOnlyList<string> Fachbereiche { get; }

    /// <summary>CAT_NAME: Kurzname -> voller Name.</summary>
    public IReadOnlyDictionary<string, string> Name { get; }

    /// <summary>CAT_KEY: voller Name -> Kurzname.</summary>
    public IReadOnlyDictionary<string, string> Kurz { get; }

    /// <summary>CAT_TOPICS: Kurzname -> Themen-Kennungen.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ThemenVon { get; }

    /// <summary>TOPIC_ORDER</summary>
    public IReadOnlyList<string> Reihenfolge { get; }

    /// <summary>TOPIC_CAT: Thema -> voller Name des Fachbereichs.</summary>
    public IReadOnlyDictionary<string, string> FachbereichVon { get; }

    /// <summary>TOPIC_SHORT</summary>
    public IReadOnlyDictionary<string, string> Kurzname { get; }

    /// <summary>CATEGORY_SHORT: voller Name -> Anzeige-Kurzname ("Netzwerk").</summary>
    public IReadOnlyDictionary<string, string> FachKurzname => Inhalte.Fachbereiche.Kurzname;

    private readonly Dictionary<string, int> platz;

    public SpielThemen(Lerninhalte lernen)
    {
        Fachbereiche = [.. Inhalte.Fachbereiche.Schluessel.Keys];
        Name = Inhalte.Fachbereiche.Schluessel;
        Kurz = Inhalte.Fachbereiche.Schluessel.ToDictionary(p => p.Value, p => p.Key);
        ThemenVon = Fachbereiche.ToDictionary(k => k, k => (IReadOnlyList<string>)(lernen.Themen.TryGetValue(Name[k], out var t)
            ? [.. t.Select(x => x.Id)] : []));
        Reihenfolge = lernen.ThemenReihenfolge;
        FachbereichVon = lernen.ThemaNach.ToDictionary(p => p.Key, p => p.Value.Fachbereich);
        Kurzname = lernen.ThemaNach.ToDictionary(p => p.Key, p => p.Value.Kurz);
        platz = Reihenfolge.Select((t, i) => (t, i)).ToDictionary(p => p.t, p => p.i);
    }

    /// <summary>TOPIC_ORDER.index(thema)</summary>
    public int Platz(string thema) => platz[thema];

    /// <summary>thema in TOPIC_CAT</summary>
    public bool IstThema(object? thema) => thema is string t && FachbereichVon.ContainsKey(t);
}
