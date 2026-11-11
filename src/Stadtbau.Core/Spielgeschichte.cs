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
    /// <remarks>
    /// Welle Q1 (S 0.40.0): <paramref name="Questgeber"/> = Kennung der Person, die die Aufgabe stellt (nach Umhaengen),
    /// <paramref name="Neu"/> = neue Quest aus dem Werkzeug (Abschnitt <c>stadtdaten.quests</c>).
    /// </remarks>
    public sealed record Eintrag(string Id, Art Sorte, string Typ, string Titel, string Stelle, string? Ort, long AbTag,
        string? Nach, string? NachTitel, string Auftraggeber, string? Questgeber = null, bool Neu = false);

    /// <summary>Welle Q1: Praefix der Gruppen in der Ansicht „je Questgeber“ (<see cref="JeQuestgeber"/>).</summary>
    public const string QgGruppe = "qg:";

    /// <summary>Alle Eintraege in der Reihenfolge der Dateien.</summary>
    public IReadOnlyList<Eintrag> Alle { get; }

    /// <summary>Nur die Aufgaben der Spielgeschichte (aufgaben.json, ohne Vorlagen).</summary>
    public IReadOnlyList<Eintrag> Aufgaben { get; }

    /// <summary>Ort der Karte je Stelle des Spiels (buero → bitweiche, talheim → talheim, ...).</summary>
    public IReadOnlyDictionary<string, string> OrtJeStelle { get; }

    private readonly Dictionary<string, long?> _freiAb;

    private readonly Dictionary<string, long?> _freiAbQg = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _qgNamen = new(StringComparer.Ordinal);

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
        // Welle Q1: Quests des Werkzeugs (umgehaengt, neu) und Questgeber mit neuem Platz
        var quests = QuestDaten.Alle(inhalt, aussen);
        var questJeId = quests.GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var qgJeId = QuestgeberDaten.Alle(inhalt, aussen).GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var q in quests.Where(q => !q.Bestand))
        {
            titel.TryAdd(q.Id, q.Titel);
        }
        var alle = new List<Eintrag>();
        void Hinzu(PyObjekt a, Art sorte)
        {
            var stelle = Py.Str(AuftragOrt(a, inhalt));
            var nach = Get(a, "nach") as string;
            var person = Person(a["auftraggeber"], inhalt);
            var name = person is not null && Get(person, "name") is string n ? n : Py.Str(Get(a, "auftraggeber", ""));
            string? qgId = sorte == Art.Aufgabe ? Py.Str(Get(a, "auftraggeber", "")) : null;
            if (sorte == Art.Aufgabe && questJeId.TryGetValue(Py.Str(a["id"]), out var q))
            {
                qgId = q.Questgeber;
                var qg = qgId is null ? null : qgJeId.GetValueOrDefault(qgId);
                if (q.Umgehaengt || qg?.Platz is not null)
                {
                    // die Aufgabe wandert mit der Person (ein eigenes Feld "ort" bleibt, solange nicht umgehaengt)
                    stelle = !q.Umgehaengt && Py.Hat(a, "ort") ? stelle : qg is null ? "" : QuestgeberDaten.Stelle(qg) ?? "";
                    name = qg?.Name ?? "";
                }
            }
            alle.Add(new Eintrag(Py.Str(a["id"]), sorte, Py.Str(Get(a, "typ", "")), Py.Str(a["titel"]), stelle,
                orte.TryGetValue(stelle, out var ort) ? ort : null, Py.Int(Get(a, "ab_tag", 1L) ?? 1L), nach,
                nach is not null && titel.TryGetValue(nach, out var nt) ? nt : null, name, qgId));
        }
        foreach (var a in SpielDaten.Liste(inhalt["aufgaben"]).Select(Objekt))
        {
            Hinzu(a, Py.Hat(a, "vorlage") ? Art.Auftrag : Art.Aufgabe);
        }
        // doppelte Kennung (rot in der Pruefung): nur der erste Eintrag zaehlt
        foreach (var q in quests.Where(q => !q.Bestand).GroupBy(q => q.Id).Select(g => g.First()).Where(q => alle.All(e => e.Id != q.Id)))
        {
            var qg = q.Questgeber is null ? null : qgJeId.GetValueOrDefault(q.Questgeber);
            var stelle = qg is null ? "" : QuestgeberDaten.Stelle(qg) ?? "";
            alle.Add(new Eintrag(q.Id, Art.Aufgabe, q.Typ, q.Titel, stelle, orte.TryGetValue(stelle, out var ort) ? ort : null, q.AbTag,
                q.Nach, q.Nach is not null && titel.TryGetValue(q.Nach, out var nt) ? nt : null, qg?.Name ?? "", q.Questgeber, true));
        }
        foreach (var a in ListeOderLeer(inhalt, "zwischenfaelle").Select(Objekt))
        {
            Hinzu(a, Art.Zwischenfall);
        }
        var g = new Spielgeschichte(alle, orte, freiAb);
        foreach (var (id, qg) in qgJeId)
        {
            var stelle = QuestgeberDaten.Stelle(qg);
            g._freiAbQg[id] = stelle is not null && orte.TryGetValue(stelle, out var o) ? freiAb.GetValueOrDefault(o) : qg.Platz is { Draussen: true } ? 1 : null;
            g._qgNamen[id] = qg.Name;
        }
        return g;
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

    /// <summary>
    /// Welle Q1 (Weisung P12-186, Umschaltung „je Ort | je Questgeber“): dieselben Aufgaben, gruppiert nach der Person, die
    /// sie stellt. Die Gruppe heisst „qg:&lt;Kennung&gt;“ (ohne Questgeber „qg:“) und steht dort, wo sonst der Ort steht, damit
    /// <see cref="Zusammenhaenge"/> unveraendert baut; ab Tag = Tag des Orts der Person (draussen Tag 1).
    /// Auftraege und Zwischenfaelle haben dort keine Gruppe.
    /// </summary>
    public Spielgeschichte JeQuestgeber()
    {
        var alle = Alle.Select(e => e with { Ort = e.Sorte == Art.Aufgabe ? QgGruppe + (e.Questgeber ?? "") : null }).ToList();
        var freiAb = _freiAbQg.ToDictionary(x => QgGruppe + x.Key, x => x.Value, StringComparer.Ordinal);
        var g = new Spielgeschichte(alle, new Dictionary<string, string>(OrtJeStelle, StringComparer.Ordinal), freiAb);
        foreach (var (k, v) in _qgNamen)
        {
            g._qgNamen[k] = v;
        }
        return g;
    }

    /// <summary>Name einer Gruppe „qg:…“ (Name der Person, ohne Kennung: <see cref="Texte.VerkettungOhne"/>).</summary>
    public string QuestgeberName(string gruppe)
    {
        var id = gruppe.StartsWith(QgGruppe, StringComparison.Ordinal) ? gruppe[QgGruppe.Length..] : gruppe;
        return id.Length == 0 ? Texte.VerkettungOhne : _qgNamen.TryGetValue(id, out var n) && n.Trim().Length > 0 ? n : id;
    }

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
