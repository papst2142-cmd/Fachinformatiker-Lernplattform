using System.Text.Json.Nodes;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Zufall;

namespace Lernplattform.Core.Fortschritt;

/// <summary>Ein Wert der Abdeckung (0-100, null = kein Lerninhalt) mit Text und Ampel.</summary>
public readonly record struct AbdeckungWert(double? Wert)
{
    /// <summary>percent_text: "%d %%" % int(round(v)), ohne Wert "–".</summary>
    public string Text => Wert is { } v ? PythonRunden.Runde(v) + " %" : FortschrittTexte.OhneProzent;

    /// <summary>level: unter 50 rot, unter 80 orange, sonst gruen (null ohne Wert).</summary>
    public string? Stufe => Wert is { } v ? Abdeckung.Stufe(v) : null;
}

/// <summary>Zeile mit "Jetzt ueben": Rahmenplan-Punkt oder Lernthema.</summary>
public sealed record AbdeckungBlatt(string Id, string Titel, AbdeckungWert Wert, int Anzahl)
{
    public string AnzahlText => DashboardTexte.Plural(Anzahl, "Inhalt", "Inhalte");
}

/// <summary>Zeile zum Aufklappen: Abschnitt, Position, Ausbildungsjahr oder Lernfeld.</summary>
public sealed record AbdeckungAst(string Id, string Titel, AbdeckungWert Wert, string? Hinweis,
    IReadOnlyList<AbdeckungAst> Aeste, IReadOnlyList<AbdeckungBlatt> Blaetter, int Stunden = 0, bool Aktuell = false);

/// <summary>
/// Phase 2, L7: Rahmenplan-Abdeckung (fisi_rahmenplan.Coverage, app_gui.CoverageCard) genau wie
/// Python v0.63.2 (Vergleichswerte G18): Wissensstand je Punkt mit knowledge_from_answers und
/// den Parametern fuer den Wissensstand je Thema; Ansicht Rahmenplan (Abschnitt, Position,
/// Punkt, Filter) und Lernfeld (Jahr, Lernfeld, Lernthema). Nur lesend.
/// </summary>
public sealed class Abdeckung
{
    private sealed record Punkt(string Id, string Abschnitt, int Nr, string Position, string Text, double Wochen, bool Ap1, IReadOnlyList<string> Ap2);

    private readonly Lerninhalte _inhalte;
    private readonly DashboardInhalte _parameter;
    private readonly StatusBuch _buch;
    private readonly RahmenplanWerte _werte;
    private readonly List<Punkt> _punkte = [];
    private readonly Dictionary<string, string> _abschnittTitel = new(StringComparer.Ordinal);
    private readonly List<string> _abschnitte = [];
    private readonly Dictionary<string, (List<string> Quiz, List<string> Karte)> _punktInhalte = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double?> _wert = new(StringComparer.Ordinal);

    public Abdeckung(DashboardInhalte parameter, RahmenplanGewicht plan, StatusBuch buch, RahmenplanWerte werte)
    {
        _inhalte = parameter.Inhalte;
        _parameter = parameter;
        _buch = buch;
        _werte = werte;
        var rp = _inhalte.Rahmenplan;
        foreach (var abschnitt in rp["abschnitte"]!.AsArray())
        {
            _abschnitte.Add((string)abschnitt!["id"]!);
            _abschnittTitel[(string)abschnitt["id"]!] = (string)abschnitt["titel"]!;
        }
        foreach (var p in rp["punkte"]!.AsArray())
        {
            _punkte.Add(new Punkt((string)p!["id"]!, (string)p["abschnitt"]!, (int)p["nr"]!, (string)p["position"]!, (string)p["text"]!,
                (double)p["wochen_punkt"]!, Einstellungen.PythonBool(p["ap1"]),
                p["ap2"] is JsonArray ap2 ? ap2.Select(a => (string)a!).ToList() : []));
        }
        // point_items: Umkehrung von item_points (Reihenfolge wie Python: Karten, dann Quiz)
        foreach (var punkt in _punkte)
        {
            _punktInhalte[punkt.Id] = ([], []);
        }
        foreach (var ((quelle, schluessel), punkte) in plan.AllePunkte)
        {
            foreach (var id in punkte)
            {
                if (_punktInhalte.TryGetValue(id, out var liste))
                {
                    (quelle == Quellen.Quiz ? liste.Quiz : liste.Karte).Add(schluessel);
                }
            }
        }
        foreach (var punkt in _punkte)
        {
            var (quiz, karte) = _punktInhalte[punkt.Id];
            _wert[punkt.Id] = Gruppenwert(quiz, karte);
        }
        IrgendeineAntwort = plan.AllePunkte.Keys.Any(k => buch.Beruehrt(k.Quelle, k.Schluessel));
    }

    /// <summary>any_answers: Gibt es zu irgendeinem Inhalt mit Punkt schon eine Antwort?</summary>
    public bool IrgendeineAntwort { get; }

    /// <summary>Inhalte eines Punktes (fuer "Jetzt ueben").</summary>
    public (IReadOnlyList<string> Quiz, IReadOnlyList<string> Karte) PunktInhalte(string id) => _punktInhalte[id];

    /// <summary>level: unter 50 rot, unter 80 orange, sonst gruen.</summary>
    public static string Stufe(double wert) => wert < 50 ? "rot" : wert < 80 ? "orange" : "gruen";

    /// <summary>Coverage.group_value: Wissensstand 0-100 einer Gruppe, null ohne Inhalte.</summary>
    private double? Gruppenwert(IReadOnlyList<string> quiz, IReadOnlyList<string> karte)
    {
        var gesamt = quiz.Count + karte.Count;
        if (gesamt == 0)
        {
            return null;
        }
        var fenster = Math.Max(1, _parameter.WissenFenster);
        var eintraege = new List<(string Zeit, bool Wert)>();
        var beruehrt = 0;
        foreach (var (quelle, liste) in new[] { (Quellen.Quiz, quiz), (Quellen.Karte, karte) })
        {
            foreach (var schluessel in liste)
            {
                if (!_buch.Antworten.Ergebnisse.TryGetValue(new FrageKennung(quelle, schluessel), out var antworten) || antworten.Count == 0)
                {
                    continue;
                }
                beruehrt++;
                foreach (var antwort in antworten)
                {
                    if (antwort.Wert is { } w)
                    {
                        eintraege.Add((PyText.Str(antwort.Zeit), w));
                    }
                }
            }
        }
        // sort(key=timestamp, reverse=True): stabil, Gleichstand behaelt die Reihenfolge
        var juengste = eintraege.OrderByDescending(e => e.Zeit, PyStringVergleich.Instanz).Take(fenster).Select(e => e.Wert).ToList();
        var anteil = beruehrt / (double)gesamt * 100.0;
        // knowledge_from_answers mit keys=["x"]
        var mindest = Math.Max(1, _parameter.WissenMindest);
        var voll = Math.Max(1.0, _parameter.WissenVoll);
        double quote = 0.0;
        if (juengste.Count > 0)
        {
            long gewichtRichtig = 0, gewichtSumme = 0;
            for (var i = 0; i < juengste.Count; i++)
            {
                var gewicht = juengste.Count - i;
                gewichtSumme += gewicht;
                if (juengste[i])
                {
                    gewichtRichtig += gewicht;
                }
            }
            quote = gewichtRichtig / (double)gewichtSumme;
            quote *= Math.Min(1.0, juengste.Count / (double)mindest);
        }
        var share = Math.Min(1.0, Math.Max(0.0, anteil) / voll);
        return PyText.Runde1(100.0 * quote * PythonPow.Pow(share, _parameter.WissenExponent));
    }

    /// <summary>_weighted_mean: Werte ohne null, gewichtet; Summe der Gewichte 0 -> null.</summary>
    public static double? GewichtetesMittel(IEnumerable<(double? Wert, double Gewicht)> paare)
    {
        // sum() wie Python 3.12 (PythonSumme), nicht als einfache Schleife
        var liste = paare.Where(p => p.Wert is not null).ToList();
        var summe = PythonSumme.Summe(liste.Select(p => p.Gewicht));
        if (summe == 0)
        {
            return null;
        }
        return PythonSumme.Summe(liste.Select(p => p.Wert!.Value * p.Gewicht)) / summe;
    }

    private static double Gewicht(Punkt punkt) => punkt.Wochen != 0 ? punkt.Wochen : 1.0;

    private static bool PasstFilter(Punkt punkt, string? filter) =>
        filter is null || filter == FortschrittTexte.FilterAlle || (filter == FortschrittTexte.FilterAp1 ? punkt.Ap1 : punkt.Ap2.Contains(filter));

    private List<string> EingeschalteteAbschnitte() => _abschnitte.Where(a => a switch
    {
        "B" => _werte.AbschnittB,
        "D" => _werte.AbschnittD,
        "E" => _werte.AbschnittE,
        _ => true,
    }).ToList();

    /// <summary>Coverage.sections(filter): Abschnitte mit Positionen und Punkten (Ansicht Rahmenplan).</summary>
    public List<AbdeckungAst> Abschnitte(string filter = FortschrittTexte.FilterAlle)
    {
        var positionen = new List<(string Abschnitt, int Nr, string Titel)>();
        foreach (var punkt in _punkte)
        {
            if (!positionen.Any(p => p.Abschnitt == punkt.Abschnitt && p.Nr == punkt.Nr))
            {
                positionen.Add((punkt.Abschnitt, punkt.Nr, punkt.Position));
            }
        }
        var baum = new List<AbdeckungAst>();
        foreach (var abschnitt in EingeschalteteAbschnitte())
        {
            var aeste = new List<(AbdeckungAst Ast, List<Punkt> Punkte)>();
            foreach (var (sec, nr, titel) in positionen.Where(p => p.Abschnitt == abschnitt))
            {
                var punkte = _punkte.Where(p => p.Abschnitt == sec && p.Nr == nr && PasstFilter(p, filter)).ToList();
                if (punkte.Count == 0)
                {
                    continue;
                }
                var blaetter = punkte.Select(p => new AbdeckungBlatt(p.Id, p.Id + " " + p.Text, new AbdeckungWert(_wert[p.Id]),
                    _punktInhalte[p.Id].Quiz.Count + _punktInhalte[p.Id].Karte.Count)).ToList();
                var id = sec + nr.ToString(System.Globalization.CultureInfo.InvariantCulture);
                aeste.Add((new AbdeckungAst(id, id + " " + titel,
                    new AbdeckungWert(GewichtetesMittel(punkte.Select(p => (_wert[p.Id], Gewicht(p))))), null, [], blaetter), punkte));
            }
            if (aeste.Count == 0)
            {
                continue;
            }
            var wert = GewichtetesMittel(aeste.SelectMany(a => a.Punkte).Select(p => (_wert[p.Id], Gewicht(p))));
            baum.Add(new AbdeckungAst(abschnitt, abschnitt + " " + _abschnittTitel[abschnitt], new AbdeckungWert(wert), null,
                aeste.Select(a => a.Ast).ToList(), []));
        }
        return baum;
    }

    /// <summary>topic_items: Quizfragen und Karteikarten eines Lernthemas (mit Doppelten wie Python).</summary>
    public (List<string> Quiz, List<string> Karte) ThemaInhalte(string thema) =>
        (_inhalte.Quizfragen.Where(q => q.Thema == thema).Select(q => q.Frage!).ToList(),
         _inhalte.Karteikarten.Where(k => k.Thema == thema).Select(k => k.Frage!).ToList());

    /// <summary>Coverage.fields() und CoverageCard.paint (Jahreswert gewichtet mit Stunden): Ansicht Lernfeld.</summary>
    public List<AbdeckungAst> Lernfelder()
    {
        var themenFelder = _inhalte.RahmenplanZuordnung["themen_lernfelder"]!.AsObject()
            .ToDictionary(t => t.Key, t => t.Value!.AsArray().Select(f => (string)f!).ToList(), StringComparer.Ordinal);
        var abschnitte = EingeschalteteAbschnitte();
        var jahre = new SortedDictionary<int, List<AbdeckungAst>>();
        foreach (var feld in _inhalte.Rahmenplan["lernfelder"]!.AsArray())
        {
            var abschnitt = (string?)feld!["abschnitt"] ?? "";
            if (abschnitt.Length > 0 && !abschnitte.Contains(abschnitt))
            {
                continue;
            }
            var id = (string)feld["id"]!;
            var blaetter = new List<AbdeckungBlatt>();
            foreach (var thema in _inhalte.ThemenReihenfolge)
            {
                if (!themenFelder.TryGetValue(thema, out var felder) || !felder.Contains(id))
                {
                    continue;
                }
                var (quiz, karte) = ThemaInhalte(thema);
                blaetter.Add(new AbdeckungBlatt(thema, _inhalte.ThemaNach[thema].Name, new AbdeckungWert(Gruppenwert(quiz, karte)), quiz.Count + karte.Count));
            }
            var aktuell = id == _werte.Lernfeld;
            var stunden = (int)feld["stunden"]!;
            var jahr = (int)feld["jahr"]!;
            if (!jahre.TryGetValue(jahr, out var liste))
            {
                jahre[jahr] = liste = [];
            }
            liste.Add(new AbdeckungAst(id, id + " " + (string)feld["titel"]!,
                new AbdeckungWert(GewichtetesMittel(blaetter.Select(b => (b.Wert.Wert, (double)b.Anzahl)))),
                aktuell ? FortschrittTexte.AktuellesLernfeld : null, [], blaetter, stunden, aktuell));
        }
        return jahre.Select(j =>
        {
            var felder = j.Value.OrderBy(f => !f.Aktuell).ToList();    // stabil: aktuelles Lernfeld zuerst
            return new AbdeckungAst(j.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Beschriftungen.Einsetzen(FortschrittTexte.Jahr, j.Key),
                new AbdeckungWert(GewichtetesMittel(felder.Select(f => (f.Wert.Wert, (double)f.Stunden)))), null, felder, []);
        }).ToList();
    }
}

/// <summary>
/// Phase 2, L7: "Jetzt ueben" (fisi_lernen.topic_practice_parts / key_practice_parts,
/// fisi_rahmenplan.point_practice_parts): erst Pruefungsfragen, dann Karteikarten, je
/// hoechstens 10, Reihenfolge rot, gelb, unbearbeitet, angefangen, abgeschlossen.
/// </summary>
public static class JetztUeben
{
    public const int Runde = 10;     // PRACTICE_ROUND

    /// <summary>EXAM_THEME_TOPICS (fisi_lernen 617).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PruefungsThemen = new Dictionary<string, IReadOnlyList<string>>
    {
        ["Subnetting & Routing"] = ["ipv4", "ipv6", "routing", "wan_vpn"],
        ["IT-Sicherheit"] = ["isms", "krypto", "zugriff", "angriffe", "netzsicherheit", "haertung", "notfall", "datenschutz"],
        ["Storage & RAID"] = ["storage", "notfall", "db_betrieb"],
        ["Netzwerkdesign"] = ["switching", "wlan", "verkabelung", "wan_vpn", "grundlagen", "dienste", "routing"],
        ["Wirtschaft & Beratung"] = ["kalkulation", "beschaffung", "beratung", "service", "recht"],
        ["Virtualisierung"] = ["virtualisierung"],
        ["Projektmanagement"] = ["projekt", "service"],
        ["Systemadministration"] = ["windows", "linux", "zugriff", "haertung", "it_betrieb"],
        ["Automatisierung & Skripte"] = ["programmierung", "windows", "linux"],
        ["Rechnertechnik & Zahlensysteme"] = ["hardware", "zahlen", "programmierung", "it_betrieb", "storage"],
        ["Rechnernetze Grundlagen"] = ["grundlagen", "ipv4", "dienste", "verkabelung", "wan_vpn", "wlan", "fehlersuche", "switching"],
        ["Datenschutz & Sicherheit"] = ["datenschutz", "zugriff", "angriffe", "notfall", "isms", "krypto", "haertung", "netzsicherheit"],
        ["Projektplanung"] = ["projekt", "beschaffung", "kalkulation"],
        ["Arbeitsplatz einrichten: Bedarf, Beschaffung, Übergabe"] = ["beschaffung", "beratung", "hardware", "it_betrieb", "kalkulation", "projekt"],
        ["Wirtschafts- und Sozialkunde"] = ["recht", "arbeitswelt", "kalkulation"],
        ["Recht & Verträge"] = ["recht"],
        ["Arbeitswelt"] = ["arbeitswelt"],
    };

    /// <summary>practice_topics: Pruefungsthema, Themen-Kennung oder Themenname -> Kennungen.</summary>
    public static List<string> Themen(Lerninhalte inhalte, string name)
    {
        if (PruefungsThemen.TryGetValue(name, out var liste))
        {
            return [.. liste];
        }
        if (inhalte.ThemaNach.ContainsKey(name))
        {
            return [name];
        }
        return inhalte.ThemaNach.Where(t => t.Value.Name == name).Select(t => t.Key).ToList();
    }

    /// <summary>topic_practice_parts: Teile (Quelle, Fragen) zu den Themen.</summary>
    public static List<(string Quelle, List<string> Schluessel)> ThemenTeile(Lerninhalte inhalte, StatusBuch buch,
        IReadOnlyCollection<string> themen, PythonZufall? zufall = null) =>
        Teile(buch, inhalte.Quizfragen.Where(q => q.Thema is { } t && themen.Contains(t)).Select(q => q.Frage!).ToList(),
            inhalte.Karteikarten.Where(k => k.Thema is { } t && themen.Contains(t)).Select(k => k.Frage!).ToList(), zufall);

    /// <summary>key_practice_parts: je Bereich gemischt (falls Zufall), nach Rang sortiert, hoechstens 10.</summary>
    public static List<(string Quelle, List<string> Schluessel)> Teile(StatusBuch buch, IReadOnlyList<string> quiz,
        IReadOnlyList<string> karte, PythonZufall? zufall = null)
    {
        var teile = new List<(string, List<string>)>();
        foreach (var (quelle, liste) in new[] { (Quellen.Quiz, quiz), (Quellen.Karte, karte) })
        {
            var schluessel = liste.ToList();
            zufall?.Shuffle(schluessel);
            if (schluessel.Count > 0)
            {
                teile.Add((quelle, schluessel.OrderBy(s => Rang(buch, quelle, s)).Take(Runde).ToList()));
            }
        }
        return teile;
    }

    /// <summary>_practice_rank: rot 0, gelb 1, unbearbeitet 2, angefangen 3, abgeschlossen 4.</summary>
    private static int Rang(StatusBuch buch, string quelle, string schluessel)
    {
        if (!buch.Beruehrt(quelle, schluessel))
        {
            return 2;
        }
        var (status, stufe) = buch.Status(quelle, schluessel);
        if (status == FragenStatus.Ueben)
        {
            return stufe == FragenStatus.Rot ? 0 : 1;
        }
        return status == FragenStatus.Fertig ? 4 : 3;
    }
}
