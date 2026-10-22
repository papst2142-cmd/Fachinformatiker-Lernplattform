using System.Globalization;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Suche;

namespace Lernplattform.Core.Lernen;

/// <summary>
/// Phase 4, PB2: Listen der AP1-/AP2-Szenarien und Testprojekte wie fisi_core v0.63.2
/// (filter_positions, page_slice, group_values, position_statuses) und PagedList.next_after
/// (app_gui 739, main.py gleich). Nur lesend. Vergleichswerte G33 (notizblock.json, "listen").
/// </summary>
public static class Listen
{
    /// <summary>LIST_PAGE_SIZE (fisi_core 293).</summary>
    public const int Seitengroesse = 15;

    /// <summary>STATUS_FILTERS (fisi_core 300): Werte der Status-Auswahl.</summary>
    public static readonly IReadOnlyList<string> StatusFilter = [FragenStatus.Alle, "Offen", "Zu üben", "Abgeschlossen"];

    /// <summary>Q_STATUS_NAME (fisi_core 2282).</summary>
    public static readonly IReadOnlyDictionary<string, string> StatusName = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [FragenStatus.Offen] = "Offen", [FragenStatus.Ueben] = "Zu üben", [FragenStatus.Fertig] = "Abgeschlossen",
    };

    /// <summary>group_values: vorkommende Werte eines Feldes in fester Reihenfolge (leere fallen weg).</summary>
    public static List<string> Gruppenwerte(IReadOnlyList<Lerninhalt> eintraege, string feld)
    {
        var werte = new List<string>();
        foreach (var eintrag in eintraege)
        {
            if (eintrag.Text(feld) is { Length: > 0 } wert && !werte.Contains(wert, StringComparer.Ordinal))
            {
                werte.Add(wert);
            }
        }
        return werte;
    }

    /// <summary>
    /// position_statuses: {Position: (Status, Stufe)} der bearbeiteten Eintraege einer Quelle.
    /// Der Lernstand speichert die Position als ganze Zahl; andere Schluessel passen zu keiner Position
    /// (wie Pythons statuses.get(position)).
    /// </summary>
    public static Dictionary<int, (string Status, string Stufe)> PositionsStati(StatusBuch buch, string quelle)
    {
        var stati = new Dictionary<int, (string, string)>();
        foreach (var kennung in buch.Antworten.Ergebnisse.Keys)
        {
            if (kennung.Quelle == quelle && kennung.Schluessel is long position && position is >= int.MinValue and <= int.MaxValue)
            {
                stati[(int)position] = buch.Status(quelle, kennung.Schluessel);
            }
        }
        return stati;
    }

    /// <summary>
    /// filter_positions: Positionen, die zu Suchtext und Filtern passen. suche durchsucht Titel und
    /// Gruppenfeld (str.lower, wie Python ohne casefold); eine Zahl findet auch die Nummer (Position + 1).
    /// fachbereich ist ein Fachbereich (cat) oder "Alle", status ein Wert aus <see cref="StatusFilter"/>.
    /// </summary>
    public static List<int> Positionen(IReadOnlyList<Lerninhalt> eintraege, string? suche = "", string fachbereich = FragenStatus.Alle,
        string? gruppenFeld = null, string gruppe = FragenStatus.Alle, string status = FragenStatus.Alle,
        IReadOnlyDictionary<int, (string Status, string Stufe)>? stati = null)
    {
        var nadel = PythonKlein.Klein(PythonZeichen.Strip(suche ?? ""));
        var positionen = new List<int>();
        for (var position = 0; position < eintraege.Count; position++)
        {
            var eintrag = eintraege[position];
            if (fachbereich != FragenStatus.Alle && eintrag.Fachbereich != fachbereich)
            {
                continue;
            }
            if (gruppenFeld is { Length: > 0 } && gruppe != FragenStatus.Alle && eintrag.Text(gruppenFeld) != gruppe)
            {
                continue;
            }
            if (status != FragenStatus.Alle)
            {
                var eigen = stati is not null && stati.TryGetValue(position, out var s) ? s.Status : FragenStatus.Offen;
                if (StatusName[eigen] != status)
                {
                    continue;
                }
            }
            if (nadel.Length > 0)
            {
                var heu = PythonKlein.Klein(Str(eintrag, "title") + " " + Str(eintrag, gruppenFeld is { Length: > 0 } ? gruppenFeld : "title"));
                if (!heu.Contains(nadel, StringComparison.Ordinal) && nadel != (position + 1).ToString(CultureInfo.InvariantCulture))
                {
                    continue;
                }
            }
            positionen.Add(position);
        }
        return positionen;
    }

    // str(item.get(key, "")): Text, sonst der JSON-Text wie Python str() fuer Zahlen (in den Inhalten nur Text)
    private static string Str(Lerninhalt eintrag, string feld) =>
        eintrag.Text(feld) ?? (eintrag.Daten[feld] is { } wert ? wert.ToJsonString() : "");

    /// <summary>page_slice: (Positionen der Seite, gueltige Seite ab 0, Seitenanzahl).</summary>
    public static (List<T> Sichtbar, int Seite, int Seiten) Seite<T>(IReadOnlyList<T> positionen, int seite, int groesse = Seitengroesse)
    {
        var seiten = Math.Max(1, (positionen.Count + groesse - 1) / groesse);
        seite = Math.Max(0, Math.Min(seite, seiten - 1));
        return (positionen.Skip(seite * groesse).Take(groesse).ToList(), seite, seiten);
    }

    /// <summary>
    /// next_after: naechste Position in der Auswahl (am Ende wieder die erste); ohne Treffer der naechste
    /// Eintrag der Gesamtliste. Ohne Status-Filter geht es bevorzugt mit nicht Abgeschlossenem weiter.
    /// </summary>
    public static int NaechsteNach(int position, IReadOnlyList<int> gefiltert, int anzahl, string status,
        IReadOnlyDictionary<int, (string Status, string Stufe)> stati)
    {
        if (gefiltert.Count == 0)
        {
            return (position + 1) % anzahl;
        }
        var reihe = gefiltert.Where(p => p > position).Concat(gefiltert.Where(p => p <= position)).ToList();
        if (status == FragenStatus.Alle)
        {
            foreach (var kandidat in reihe)
            {
                if (kandidat != position && (stati.TryGetValue(kandidat, out var s) ? s.Status : FragenStatus.Offen) != FragenStatus.Fertig)
                {
                    return kandidat;
                }
            }
        }
        return reihe[0];
    }
}
