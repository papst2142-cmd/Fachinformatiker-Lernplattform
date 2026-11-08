using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.22.0 „Spielgeschichte ansehen“ (Weisung P12-91, Kurzplan Meldung P12-59, Nico 1a 2a 3a 4a 5a): welche
/// Aufgaben der Spielgeschichte, wiederkehrenden Auftraege und Zwischenfaelle an welchem Ort der Stadt haengen. Nur
/// lesen: das Werkzeug aendert daran nichts, das Spiel bleibt unveraendert.
/// <para>Der Ort einer Aufgabe kommt aus derselben Regel wie im Spiel (<see cref="SpielDaten.AuftragOrt"/>: Feld
/// <c>ort</c>, sonst der Ort der Person, die sie stellt, sonst das Buero). Diese Stelle im Spiel heisst wie ein
/// Innenraum (<c>buero</c>, Kunden-Ort); der Ort der Karte ist der mit dieser Ansicht
/// (<see cref="Innenraum.OrtFuer"/>), so wie die Figur im Spiel hineingeht.</para>
/// </summary>
public sealed class Spielgeschichte
{
    /// <summary>Was fuer ein Eintrag: Aufgabe der Spielgeschichte, wiederkehrender Auftrag, Zwischenfall.</summary>
    public enum Art
    {
        Aufgabe,
        Auftrag,
        Zwischenfall,
    }

    /// <summary>Ein Eintrag mit Ort der Karte (null = kein Ort dieser Stadt, z. B. leere Stadt).</summary>
    public sealed record Eintrag(string Id, Art Sorte, string Typ, string Titel, string Stelle, string? Ort, long AbTag,
        string? Nach, string? NachTitel, string Auftraggeber);

    /// <summary>Alle Eintraege in der Reihenfolge der Dateien.</summary>
    public IReadOnlyList<Eintrag> Alle { get; }

    /// <summary>Nur die Aufgaben der Spielgeschichte (aufgaben.json, ohne Vorlagen).</summary>
    public IReadOnlyList<Eintrag> Aufgaben { get; }

    /// <summary>Ort der Karte je Stelle des Spiels (buero → bitweiche, talheim → talheim, ...).</summary>
    public IReadOnlyDictionary<string, string> OrtJeStelle { get; }

    private readonly Dictionary<string, long?> _freiAb;

    private Spielgeschichte(List<Eintrag> alle, Dictionary<string, string> orte, Dictionary<string, long?> freiAb)
    {
        Alle = alle;
        Aufgaben = [.. alle.Where(e => e.Sorte == Art.Aufgabe)];
        OrtJeStelle = orte;
        _freiAb = freiAb;
    }

    /// <summary>
    /// Spielgeschichte zu den Spielinhalten (<paramref name="inhalt"/>, wie <see cref="Spieldaten.Inhalt"/>) und der Stadt
    /// draussen (<paramref name="aussen"/>, Karte und neue Orte des Projekts).
    /// </summary>
    public static Spielgeschichte Aus(PyObjekt inhalt, PyObjekt aussen)
    {
        var orte = new Dictionary<string, string>(StringComparer.Ordinal);
        var freiAb = new Dictionary<string, long?>(StringComparer.Ordinal);
        foreach (var o in AussenBearbeitung.Orte(aussen))
        {
            if (o["id"] is not string id)
            {
                continue;
            }
            if (Innenraum.OrtFuer(o["ansicht"] as string) is { } stelle)
            {
                orte.TryAdd(stelle, id);
            }
            freiAb[id] = FreiAb(o, inhalt);
        }
        // Titel je Kennung fuer "erst nach" (auch Vorlagen und Zwischenfaelle haben eine Kennung)
        var titel = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in SpielDaten.Liste(inhalt["aufgaben"]).Concat(ListeOderLeer(inhalt, "zwischenfaelle")).Select(Objekt))
        {
            if (a["id"] is string k)
            {
                titel.TryAdd(k, Py.Str(a["titel"]));
            }
        }
        var alle = new List<Eintrag>();
        void Hinzu(PyObjekt a, Art sorte)
        {
            var stelle = Py.Str(AuftragOrt(a, inhalt));
            var nach = Get(a, "nach") as string;
            var person = Person(a["auftraggeber"], inhalt);
            alle.Add(new Eintrag(Py.Str(a["id"]), sorte, Py.Str(Get(a, "typ", "")), Py.Str(a["titel"]), stelle,
                orte.TryGetValue(stelle, out var ort) ? ort : null, Py.Int(Get(a, "ab_tag", 1L) ?? 1L), nach,
                nach is not null && titel.TryGetValue(nach, out var nt) ? nt : null,
                person is not null && Get(person, "name") is string n ? n : Py.Str(Get(a, "auftraggeber", ""))));
        }
        foreach (var a in SpielDaten.Liste(inhalt["aufgaben"]).Select(Objekt))
        {
            Hinzu(a, Py.Hat(a, "vorlage") ? Art.Auftrag : Art.Aufgabe);
        }
        foreach (var a in ListeOderLeer(inhalt, "zwischenfaelle").Select(Objekt))
        {
            Hinzu(a, Art.Zwischenfall);
        }
        return new Spielgeschichte(alle, orte, freiAb);
    }

    /// <summary>
    /// Ab welchem Spieltag ein Ort der Karte da ist: immer = Tag 1, Kunde = <c>ab_tag</c> des Kunden-Orts; null =
    /// haengt vom Spiel ab (Gruendung, Filiale, Serverfarm, Geschaefte ohne Bedingung zaehlen als Tag 1).
    /// </summary>
    private static long? FreiAb(PyObjekt ort, PyObjekt inhalt)
    {
        if (Get(ort, "frei") is not PyObjekt frei || Py.Wahr(Get(frei, "immer")))
        {
            return 1;
        }
        if (Get(frei, "kundenort") is { } kunde)
        {
            var k = Kundenorte(inhalt).FirstOrDefault(o => Py.Gleich(o["id"], kunde));
            return k is null ? null : Py.Int(Get(k, "ab_tag", 1L) ?? 1L);
        }
        return null;
    }

    /// <summary>Ab welchem Spieltag der Ort da ist (null = haengt vom Spiel ab oder unbekannter Ort).</summary>
    public long? FreiAbTag(string ort) => _freiAb.TryGetValue(ort, out var t) ? t : null;

    /// <summary>Eintraege an einem Ort der Karte, nach Tag (stabil in der Reihenfolge der Dateien).</summary>
    public IReadOnlyList<Eintrag> AnOrt(string ort, Art sorte) =>
        [.. Alle.Where(e => e.Sorte == sorte && e.Ort == ort).OrderBy(e => e.AbTag)];

    /// <summary>Zahl am Haus (Frage 3 = a): nur die Aufgaben der Spielgeschichte; 0 = keine Zahl.</summary>
    public int Zahl(string ort) => Aufgaben.Count(e => e.Ort == ort);

    /// <summary>Zahl am Haus je Ort (nur Orte mit wenigstens einer Aufgabe).</summary>
    public IReadOnlyDictionary<string, int> Zahlen() =>
        Aufgaben.Where(e => e.Ort is not null).GroupBy(e => e.Ort!).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

    /// <summary>Hat der Ort irgendetwas aus der Spielgeschichte (Aufgabe, Auftrag oder Zwischenfall)?</summary>
    public bool HatEtwas(string ort) => Alle.Any(e => e.Ort == ort);

    /// <summary>Liste „Aufgabe → Ort“ (Bild 2): Aufgaben der Spielgeschichte nach Tag, gefiltert.</summary>
    public IReadOnlyList<Eintrag> Liste(string? ort = null, long? abTag = null, string? person = null, string? suche = null,
        Func<string, string>? ortName = null)
    {
        var s = string.IsNullOrWhiteSpace(suche) ? null : Katalog.Normal(suche.Trim());
        return [.. Aufgaben.Where(e => (ort is null || e.Ort == ort) && (abTag is null || e.AbTag >= abTag)
                && (person is null || e.Auftraggeber == person)
                && (s is null || Katalog.Normal(e.Titel).Contains(s, StringComparison.Ordinal)
                    || Katalog.Normal(e.Auftraggeber).Contains(s, StringComparison.Ordinal)
                    || (ortName is not null && e.Ort is not null && Katalog.Normal(ortName(e.Ort)).Contains(s, StringComparison.Ordinal))))
            .OrderBy(e => e.AbTag)];
    }

    /// <summary>Personen, die Aufgaben der Spielgeschichte stellen (fuer den Filter), sortiert.</summary>
    public IReadOnlyList<string> Personen() => [.. Aufgaben.Select(e => e.Auftraggeber).Distinct().Order(StringComparer.Ordinal)];
}
