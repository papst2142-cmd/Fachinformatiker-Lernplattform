using System.Globalization;
using System.Text;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Rechner;

namespace Lernplattform.Core.Lernen;

/// <summary>Ein Eintrag des Notizblocks (notebook_entries: source, key, item, title, status, level, time).</summary>
public sealed record NotizEintrag(string Quelle, object Schluessel, Lerninhalt Eintrag, string Titel, string Status, string Stufe, object Zeit);

/// <summary>
/// Phase 4, PB2: Notizblock wie fisi_core v0.63.2 (notebook_entries, notebook_summary,
/// notebook_search mit search_key = NFC + casefold + strip, _entry_search_text, model_answer,
/// item_title, source_items). Nur lesend, auf dem vorhandenen <see cref="StatusBuch"/>.
/// Vergleichswerte G33 (notizblock.json).
/// </summary>
public sealed class Notizblock(Lerninhalte inhalte)
{
    private readonly Dictionary<(string, object), string> _suchtext = [];

    /// <summary>source_items: (Schluessel, Eintrag) aller Fragen einer Quelle (Text bzw. Position als ganze Zahl).</summary>
    public IEnumerable<(object Schluessel, Lerninhalt Eintrag)> Fragen(string quelle) => quelle switch
    {
        Quellen.Karte => inhalte.Karteikarten.Select(k => ((object)(k.Frage ?? ""), k)),
        Quellen.Quiz => inhalte.Quizfragen.Select(q => ((object)(q.Frage ?? ""), q)),
        Quellen.Ap1 => inhalte.Ap1Szenarien.Select((e, i) => ((object)(long)i, e)),
        Quellen.Ap2 => inhalte.Ap2Szenarien.Select((e, i) => ((object)(long)i, e)),
        _ => inhalte.Testprojekte.Select((e, i) => ((object)(long)i, e)),
    };

    /// <summary>item_title: Frage (Karteikarte, Pruefungsfrage) bzw. Titel.</summary>
    public static string Titel(string quelle, Lerninhalt eintrag) =>
        (quelle is Quellen.Karte or Quellen.Quiz ? eintrag.Frage : eintrag.Titel) ?? "";

    /// <summary>model_answer: Musterantwort zum Nachlesen.</summary>
    public static string Musterantwort(string quelle, Lerninhalt eintrag)
    {
        switch (quelle)
        {
            case Quellen.Karte:
                return eintrag.Text("a_full") ?? "";
            case Quellen.Quiz:
                return "Richtig: " + eintrag.Antwort + "\n" + (eintrag.Text("exp") ?? "");
            case Quellen.Projekt:
                var aufgaben = eintrag.Liste("aufgaben");
                var hinweise = eintrag.Liste("hinweise");
                return string.Join("\n", Enumerable.Range(0, Math.Min(aufgaben.Count, hinweise.Count))
                    .Select(i => (i + 1).ToString(CultureInfo.InvariantCulture) + ". " + aufgaben[i] + "\n   Lösungsansatz: " + hinweise[i]));
            default:
                return eintrag.Text("solution") ?? "";
        }
    }

    /// <summary>
    /// notebook_entries: alle Fragen mit dem Status (Standard "Zu ueben"; null oder "Alle" = jeder),
    /// gefiltert nach Fachbereich und Thema; rot vor gelb, darin die zuletzt bearbeiteten zuerst
    /// (zwei stabile Sortierungen wie Python).
    /// </summary>
    public List<NotizEintrag> Eintraege(StatusBuch buch, string? status = FragenStatus.Ueben, string fachbereich = FragenStatus.Alle,
        string thema = FragenStatus.Alle, IReadOnlyList<string>? quellen = null)
    {
        var eintraege = new List<NotizEintrag>();
        foreach (var quelle in quellen is { Count: > 0 } ? quellen : Quellen.Alle)
        {
            foreach (var (schluessel, eintrag) in Fragen(quelle))
            {
                if (fachbereich != FragenStatus.Alle && eintrag.Fachbereich != fachbereich)
                {
                    continue;
                }
                if (thema != FragenStatus.Alle && eintrag.Thema != thema)
                {
                    continue;
                }
                var (wert, stufe) = buch.Status(quelle, schluessel);
                if (status is not (null or FragenStatus.Alle) && wert != status)
                {
                    continue;
                }
                eintraege.Add(new NotizEintrag(quelle, schluessel, eintrag, Titel(quelle, eintrag), wert, stufe, LetzteZeit(buch, quelle, schluessel)));
            }
        }
        return [.. eintraege.OrderBy(e => e.Stufe == FragenStatus.Rot ? 0 : 1).ThenByDescending(e => e.Zeit, ZeitVergleich.Instanz)];
    }

    /// <summary>last_time: Zeit der letzten Antwort oder "" (Antwortbuch, aelteste zuerst).</summary>
    public static object LetzteZeit(StatusBuch buch, string quelle, object schluessel) =>
        buch.Antworten.Ergebnisse.TryGetValue(new FrageKennung(quelle, schluessel), out var liste) && liste.Count > 0
            ? liste[^1].Zeit ?? "" : "";

    /// <summary>notebook_summary: {Quelle: {Status: Anzahl}} ueber den ganzen Fragenpool.</summary>
    public Dictionary<string, Dictionary<string, int>> Uebersicht(StatusBuch buch, string fachbereich = FragenStatus.Alle, string thema = FragenStatus.Alle)
    {
        var uebersicht = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (var quelle in Quellen.Alle)
        {
            var schluessel = Fragen(quelle)
                .Where(f => (fachbereich == FragenStatus.Alle || f.Eintrag.Fachbereich == fachbereich)
                    && (thema == FragenStatus.Alle || f.Eintrag.Thema == thema))
                .Select(f => (object?)f.Schluessel);
            uebersicht[quelle] = buch.Zaehlen(quelle, schluessel);
        }
        return uebersicht;
    }

    /// <summary>search_key: NFC, casefold (auch ss fuer ß), Leerraum an den Raendern weg (str.strip).</summary>
    public static string Suchform(string? text) =>
        PythonZeichen.Strip(PythonZeichen.Falten((text ?? "").Normalize(NormalizationForm.FormC)));

    /// <summary>
    /// _entry_search_text: Titel, Musterantwort, Themenname, Themenkurzname, Szenario-Thema (ohne
    /// Fachbereich), je Frage einmal gebaut.
    /// </summary>
    public string Suchtext(NotizEintrag eintrag)
    {
        var kennung = (eintrag.Quelle, eintrag.Schluessel);
        if (!_suchtext.TryGetValue(kennung, out var text))
        {
            var thema = eintrag.Eintrag.Thema;
            var bekannt = thema is not null && inhalte.ThemaNach.TryGetValue(thema, out var t) ? t : null;
            var teile = new[]
            {
                eintrag.Titel, Musterantwort(eintrag.Quelle, eintrag.Eintrag), bekannt?.Name ?? "", bekannt?.Kurz ?? "",
                eintrag.Eintrag.Text("theme") ?? (eintrag.Eintrag.Daten["theme"] is { } roh ? roh.ToJsonString() : ""),
            };
            _suchtext[kennung] = text = Suchform(string.Join(" ", teile));
        }
        return text;
    }

    /// <summary>_apply_search: gleiche Anfrage, wenn sie ohne Leerraum an den Raendern gleich ist (query.strip()).</summary>
    public static bool GleicheAnfrage(string? a, string? b) => PythonZeichen.Strip(a ?? "") == PythonZeichen.Strip(b ?? "");

    /// <summary>notebook_search: Eintraege, deren Suchtext das Stichwort enthaelt; leeres Stichwort = alle. Reihenfolge bleibt.</summary>
    public List<NotizEintrag> Suchen(IEnumerable<NotizEintrag> eintraege, string? anfrage)
    {
        var nadel = Suchform(anfrage);
        return nadel.Length == 0 ? [.. eintraege] : [.. eintraege.Where(e => Suchtext(e).Contains(nadel, StringComparison.Ordinal))];
    }

    /// <summary>Zeiten wie Python vergleichen: Texte nach Codepunkten.</summary>
    private sealed class ZeitVergleich : IComparer<object>
    {
        public static readonly ZeitVergleich Instanz = new();

        public int Compare(object? a, object? b)
        {
            var x = (Convert.ToString(a, CultureInfo.InvariantCulture) ?? "").EnumerateRunes().Select(r => r.Value).ToArray();
            var y = (Convert.ToString(b, CultureInfo.InvariantCulture) ?? "").EnumerateRunes().Select(r => r.Value).ToArray();
            for (var i = 0; i < Math.Min(x.Length, y.Length); i++)
            {
                if (x[i] != y[i])
                {
                    return x[i].CompareTo(y[i]);
                }
            }
            return x.Length.CompareTo(y.Length);
        }
    }
}
