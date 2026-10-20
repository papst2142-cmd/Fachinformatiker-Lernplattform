using Lernplattform.Core.Daten;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Zufall;

namespace Lernplattform.Core.Lernen;

/// <summary>Quellen der Antworten wie fisi_core.SOURCES (Schluessel im Lernstand, nicht anzeigen).</summary>
public static class Quellen
{
    public const string Karte = "karte";
    public const string Quiz = "quiz";
    public const string Ap1 = "ap1";
    public const string Ap2 = "ap2";
    public const string Projekt = "projekt";

    public static readonly IReadOnlyList<string> Alle = [Karte, Quiz, Ap1, Ap2, Projekt];
}

/// <summary>Eine Antwort: Zeitstempel wie gespeichert und richtig/falsch, null = angesehen, nicht bewertet.</summary>
public readonly record struct Antwort(object? Zeit, bool? Wert);

/// <summary>Eine Frage: Quelle und Schluessel (Fragetext bei Karteikarte und Quiz, Position bei AP1, AP2, Testprojekt).</summary>
public readonly record struct FrageKennung(string Quelle, object? Schluessel);

/// <summary>Lernstand je Frage wie fisi_core.question_status (Offen, Zu ueben, Abgeschlossen).</summary>
public static class FragenStatus
{
    public const string Offen = "offen";           // Q_OPEN
    public const string Ueben = "ueben";           // Q_PRACTICE
    public const string Fertig = "fertig";         // Q_DONE
    public const string Rot = "rot";               // LEVEL_RED
    public const string Gelb = "gelb";             // LEVEL_YELLOW
    public const string Alle = "Alle";             // FILTER_ALL

    /// <summary>DONE_STREAK: so viele richtige Antworten zuletzt = abgeschlossen.</summary>
    public const int FertigFolge = 2;

    /// <summary>NEW_PER_REPEAT: nach so vielen neuen Fragen eine Wiederholung.</summary>
    public const int NeueJeWiederholung = 4;

    /// <summary>question_status: (Status, Stufe) aus den Antworten einer Frage, aelteste zuerst.</summary>
    public static (string Status, string Stufe) Bestimmen(IEnumerable<bool?> antworten)
    {
        var werte = antworten.Where(w => w is not null).Select(w => w!.Value).ToList();
        if (werte.Count >= FertigFolge && werte.TakeLast(FertigFolge).All(w => w))
        {
            return (Fertig, "");
        }
        if (werte.Count > 0 && !werte[^1])
        {
            return (Ueben, Rot);
        }
        if (werte.Contains(false))
        {
            return (Ueben, Gelb);
        }
        return (Offen, "");
    }
}

/// <summary>
/// Alle Antworten je Frage, aelteste zuerst (fisi_core.DBManager.question_results):
/// je Tabelle ORDER BY timestamp, id; eine gescheiterte Abfrage liefert keine Zeilen
/// (wie Python mit default=[]). Liest nur.
/// </summary>
public sealed class Antwortbuch
{
    /// <summary>Quelle, Tabelle, Spalte des Schluessels (Reihenfolge wie Python).</summary>
    public static readonly IReadOnlyList<(string Quelle, string Tabelle, string Spalte)> Tabellen =
    [
        (Quellen.Karte, "card_events", "question"),
        (Quellen.Quiz, "quiz_answers", "question"),
        (Quellen.Ap1, "ap1_events", "scenario_index"),
        (Quellen.Ap2, "scenario_events", "scenario_index"),
        (Quellen.Projekt, "project_events", "project_index"),
    ];

    private readonly Dictionary<FrageKennung, List<Antwort>> _ergebnisse = [];

    public IReadOnlyDictionary<FrageKennung, List<Antwort>> Ergebnisse => _ergebnisse;

    /// <summary>Antworten aus dem Lernstand (Fehler meldet Lernstand wie jede Abfrage).</summary>
    public static Antwortbuch Lesen(Lernstand lernstand)
    {
        var buch = new Antwortbuch();
        foreach (var (quelle, tabelle, spalte) in Tabellen)
        {
            foreach (var zeile in lernstand.Abfragen($"SELECT timestamp, {spalte}, correct FROM {tabelle} ORDER BY timestamp, id"))
            {
                buch.Hinzufuegen(quelle, zeile[1], zeile[0], zeile[2]);
            }
        }
        return buch;
    }

    /// <summary>Eine Zeile anhaengen (correct wie gespeichert: null, Zahl ...; bool() wie Python).</summary>
    public void Hinzufuegen(string quelle, object? schluessel, object? zeit, object? richtig)
    {
        var kennung = new FrageKennung(quelle, schluessel);
        if (!_ergebnisse.TryGetValue(kennung, out var liste))
        {
            _ergebnisse[kennung] = liste = [];
        }
        liste.Add(new Antwort(zeit, richtig switch
        {
            null => null,
            bool b => b,                // aus Tests und Oberflaeche; PyText.Wahr kennt nur Datenbankwerte
            _ => PyText.Wahr(richtig),
        }));
    }
}

/// <summary>Lernstand aller Fragen, einmal berechnet (fisi_core.StatusBook).</summary>
public sealed class StatusBuch(Antwortbuch buch)
{
    private readonly Dictionary<FrageKennung, (string, string)> _zwischenspeicher = [];

    public Antwortbuch Antworten { get; } = buch;

    private IReadOnlyList<Antwort> Liste(string quelle, object? schluessel) =>
        Antworten.Ergebnisse.TryGetValue(new FrageKennung(quelle, schluessel), out var liste) ? liste : [];

    /// <summary>(Status, Stufe) einer Frage.</summary>
    public (string Status, string Stufe) Status(string quelle, object? schluessel)
    {
        var kennung = new FrageKennung(quelle, schluessel);
        if (!_zwischenspeicher.TryGetValue(kennung, out var wert))
        {
            _zwischenspeicher[kennung] = wert = FragenStatus.Bestimmen(Liste(quelle, schluessel).Select(a => a.Wert));
        }
        return wert;
    }

    /// <summary>Schon einmal bearbeitet (auch nur angesehen)?</summary>
    public bool Beruehrt(string quelle, object? schluessel) => Liste(quelle, schluessel).Count > 0;

    /// <summary>Richtige Antworten in Folge (zuletzt).</summary>
    public int Folge(string quelle, object? schluessel)
    {
        var anzahl = 0;
        foreach (var antwort in Liste(quelle, schluessel).Reverse())
        {
            if (antwort.Wert is null)
            {
                continue;
            }
            if (!antwort.Wert.Value)
            {
                break;
            }
            anzahl++;
        }
        return anzahl;
    }

    /// <summary>{Status: Anzahl} fuer die Fragen (counts).</summary>
    public Dictionary<string, int> Zaehlen(string quelle, IEnumerable<object?> schluessel)
    {
        var ergebnis = new Dictionary<string, int> { [FragenStatus.Offen] = 0, [FragenStatus.Ueben] = 0, [FragenStatus.Fertig] = 0 };
        foreach (var s in schluessel)
        {
            ergebnis[Status(quelle, s).Status]++;
        }
        return ergebnis;
    }

    /// <summary>matches: Status-Filter (null oder "Alle" = alles).</summary>
    public bool Passt(string quelle, object? schluessel, string? status) =>
        status is null or FragenStatus.Alle || Status(quelle, schluessel).Status == status;

    /// <summary>
    /// preferred_order: unbearbeitete Fragen zuerst, nach je 4 eine Wiederholung
    /// (rot, gelb, offen, abgeschlossen). Mit zufall werden beide Gruppen gemischt
    /// (erst die neuen, dann die Wiederholungen, wie Python); neuReihenfolge legt
    /// die Reihenfolge der neuen fest (Rahmenplan-Gewichtung).
    /// </summary>
    public List<T> BevorzugteReihenfolge<T>(string quelle, IEnumerable<T> schluessel, PythonZufall? zufall = null,
        Func<List<T>, List<T>>? neuReihenfolge = null)
    {
        var neu = new List<T>();
        var wiederholen = new List<(int Rang, T Schluessel)>();
        foreach (var s in schluessel)
        {
            if (!Beruehrt(quelle, s))
            {
                neu.Add(s);
                continue;
            }
            var rang = Status(quelle, s) switch
            {
                (FragenStatus.Ueben, FragenStatus.Rot) => 0,
                (FragenStatus.Ueben, FragenStatus.Gelb) => 1,
                (FragenStatus.Offen, "") => 2,
                _ => 3,
            };
            wiederholen.Add((rang, s));
        }
        if (zufall is not null)
        {
            zufall.Shuffle(neu);
            zufall.Shuffle(wiederholen);
        }
        if (neuReihenfolge is not null)
        {
            neu = neuReihenfolge(neu);
        }
        // sorted(..., key=Rang) ist stabil wie OrderBy
        var rest = new Queue<T>(wiederholen.OrderBy(w => w.Rang).Select(w => w.Schluessel));
        var ergebnis = new List<T>();
        var position = 0;
        while (position < neu.Count || rest.Count > 0)
        {
            var anzahl = Math.Min(FragenStatus.NeueJeWiederholung, neu.Count - position);
            ergebnis.AddRange(neu.GetRange(position, anzahl));
            position += anzahl;
            if (rest.Count > 0)
            {
                ergebnis.Add(rest.Dequeue());
            }
            if (position >= neu.Count)
            {
                ergebnis.AddRange(rest);
                rest.Clear();
            }
        }
        return ergebnis;
    }
}
