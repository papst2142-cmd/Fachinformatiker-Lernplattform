using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Zufall;

namespace Lernplattform.Core.Lernen;

/// <summary>Ergebnis nach "Antwort einreichen".</summary>
/// <param name="Warnung">Hinweis statt Bewertung (keine Antwort gewaehlt), nichts gespeichert.</param>
public sealed record UebungAntwort(string? Warnung, bool Richtig, string Erklaerung, string Knopf);

/// <summary>Ende einer Session (finish_quiz).</summary>
public sealed record UebungErgebnis(int Richtig, int Gesamt, double Prozent, string Note, int Sekunden, bool Gespeichert)
{
    public string Text => TrainerTexte.Ergebnis(Richtig, Gesamt, Prozent, Note, Sekunden, Gespeichert);
}

/// <summary>
/// Phase 2, L6: Pruefungstrainer, Art "Uebung", wie app_gui.QuizView (Python v0.63.2), ohne
/// Oberflaeche. Jede Antwort wird beim Einreichen sofort gespeichert (log_quiz_answer), das
/// Ergebnis erst am Ende (save_test_result); ein Abbruch speichert kein Ergebnis.
/// Gemischt wird wie Python: Runde und Antworten aus einem fortlaufenden Zufall (Pythons Modul
/// random), die Reihenfolge mit einem je Start neuen Zufall (random.Random()); feste Startwerte nur in Tests.
/// </summary>
public sealed class Uebung
{
    public const int AnzahlVorgabe = 10;
    public const int AnzahlMin = 5;
    public const int AnzahlSchritt = 5;
    public const int RundeMax = 50;

    private readonly Lerninhalte _inhalte;
    private readonly Func<StatusBuch> _buchLesen;
    private readonly LernstandSchreiber? _schreiber;
    private readonly Func<Func<List<string>, List<string>>?> _neuReihenfolge;
    private readonly PythonZufall _zufall;
    private readonly Func<PythonZufall> _neuerZufall;
    private readonly Func<int> _sekunden;
    private List<Lerninhalt> _pool = [];
    private List<Lerninhalt> _basis = [];
    private int _start;

    /// <param name="zufall">Fortlaufender Zufall fuer Runde und Antworten (Pythons random.shuffle/sample).</param>
    /// <param name="neuerZufall">Neuer Zufall je Start fuer preferred_order (random.Random()).</param>
    /// <param name="sekunden">Uhr in Sekunden (Vorgabe: seit Programmstart).</param>
    public Uebung(Lerninhalte inhalte, Func<StatusBuch> buchLesen, LernstandSchreiber? schreiber,
        Func<Func<List<string>, List<string>>?>? neuReihenfolge = null, PythonZufall? zufall = null, Func<PythonZufall>? neuerZufall = null, Func<int>? sekunden = null)
    {
        _inhalte = inhalte;
        _buchLesen = buchLesen;
        _schreiber = schreiber;
        _neuReihenfolge = neuReihenfolge ?? (() => null);
        _zufall = zufall ?? NeuerZufall();
        _neuerZufall = neuerZufall ?? NeuerZufall;
        var uhr = Stopwatch.StartNew();
        _sekunden = sekunden ?? (() => (int)uhr.Elapsed.TotalSeconds);
        Anzahl = AnzahlVorgabe;
        Hoechstens = inhalte.Quizfragen.Count;
        Buch = buchLesen();
        PoolNeu();
    }

    private static PythonZufall NeuerZufall() => new(new BigInteger(RandomNumberGenerator.GetBytes(32), isUnsigned: true));

    public StatusBuch Buch { get; private set; }

    public string Fachbereich { get; private set; } = KartenTexte.Alle;

    public string Thema { get; private set; } = KartenTexte.Alle;

    public string Status { get; private set; } = FragenStatus.Alle;

    public IReadOnlyList<Thema> Themen => Lernregeln.ThemenFuer(_inhalte, Fachbereich);

    /// <summary>Wert des Zaehlers "Fragenanzahl" (NumberStepper: 10, mindestens 5, Schritt 5).</summary>
    public int Anzahl { get; private set; }

    public int Hoechstens { get; private set; }

    public IReadOnlyList<Lerninhalt> Pool => _pool;

    public bool Laeuft { get; private set; }

    public bool Beantwortet { get; private set; }

    public IReadOnlyList<Lerninhalt> Runde { get; private set; } = [];

    public int Index { get; private set; }

    public int Punkte { get; private set; }

    public Lerninhalt? Frage => Laeuft && Runde.Count > 0 ? Runde[Index] : null;

    /// <summary>Antworten der aktuellen Frage, je Frage neu gemischt wie Python.</summary>
    public IReadOnlyList<string> Optionen { get; private set; } = [];

    public string Katalog => TrainerTexte.Katalog(_inhalte.Quizfragen.Count);

    public string PoolText => TrainerTexte.Pool(_pool.Count, Buch.Zaehlen(Quellen.Quiz, _basis.Select(q => (object?)q.Frage)));

    public string Fortschritt => Laeuft ? Beschriftungen.Einsetzen(TrainerTexte.Fortschritt, Index + 1, Runde.Count) : TrainerTexte.FortschrittLeer;

    public string RichtigText => Beschriftungen.Einsetzen(TrainerTexte.Richtig, Punkte);

    public string Uhr => Laeuft ? TrainerTexte.Uhr(_sekunden() - _start) : TrainerTexte.UhrLeer;

    public string FrageText => Frage is { } f ? Beschriftungen.Einsetzen(TrainerTexte.FrageNummer, Index + 1, f.Frage!) : "";

    public void SetzeFachbereich(string fachbereich)
    {
        Fachbereich = fachbereich;
        Thema = KartenTexte.Alle;
        PoolNeu();
    }

    public void SetzeThema(string thema)
    {
        Thema = thema;
        PoolNeu();
    }

    public void SetzeStatus(string status)
    {
        Status = status;
        PoolNeu();
    }

    /// <summary>NumberStepper._change: in Schritten, zwischen 5 und dem Hoechstwert.</summary>
    public void AnzahlAendern(int schritte)
    {
        Anzahl = Math.Max(AnzahlMin, Math.Min(Hoechstens, Anzahl + schritte * AnzahlSchritt));
    }

    /// <summary>_update_pool: Fachbereich, Thema und Status anwenden.</summary>
    public void PoolNeu()
    {
        Buch = _buchLesen();
        _basis = _inhalte.Quizfragen
            .Where(q => (Fachbereich == KartenTexte.Alle || q.Fachbereich == Fachbereich)
                && (Thema == KartenTexte.Alle || q.Thema == Thema))
            .ToList();
        _pool = _basis.Where(q => Buch.Passt(Quellen.Quiz, q.Frage, Status)).ToList();
        // set_maximum: max(1, max(5, pool)), dann _change(0)
        Hoechstens = Math.Max(1, Math.Max(AnzahlMin, _pool.Count));
        Anzahl = Math.Max(AnzahlMin, Math.Min(Hoechstens, Anzahl));
    }

    /// <summary>start_quiz. Rueckgabe: Hinweis, wenn es keine Fragen gibt (nichts gestartet).</summary>
    public string? Starten()
    {
        if (_pool.Count == 0)
        {
            return TrainerTexte.KeineFragen;
        }
        var anzahl = Math.Min(Anzahl, _pool.Count);
        if (Status == FragenStatus.Alle)
        {
            var nachFrage = _pool.ToDictionary(q => q.Frage!, StringComparer.Ordinal);
            var schluessel = Buch.BevorzugteReihenfolge(Quellen.Quiz, _pool.Select(q => q.Frage!), _neuerZufall(), _neuReihenfolge());
            var runde = schluessel.Take(anzahl).Select(s => nachFrage[s]).ToList();
            _zufall.Shuffle(runde);
            Runde = runde;
        }
        else
        {
            Runde = _zufall.Sample(_pool, anzahl);
        }
        Beginnen();
        return null;
    }

    /// <summary>practice: gezielte Runde (Jetzt wiederholen), gemischt, hoechstens 50.</summary>
    /// <param name="reihenfolgeBehalten">keep_order (Jetzt ueben, Phase 2 L7): nicht mischen.</param>
    public void Ueben(IEnumerable<string> fragen, bool reihenfolgeBehalten = false)
    {
        var nachFrage = new Dictionary<string, Lerninhalt>(StringComparer.Ordinal);
        foreach (var q in _inhalte.Quizfragen)
        {
            nachFrage.TryAdd(q.Frage!, q);
        }
        var runde = fragen.Where(nachFrage.ContainsKey).Select(f => nachFrage[f]).ToList();
        if (runde.Count == 0)
        {
            return;
        }
        if (!reihenfolgeBehalten)
        {
            _zufall.Shuffle(runde);
        }
        Runde = runde.Take(RundeMax).ToList();
        _pool = [.. Runde];
        Beginnen();
    }

    private void Beginnen()
    {
        Index = 0;
        Punkte = 0;
        Laeuft = true;
        _start = _sekunden();
        FrageLaden();
    }

    private void FrageLaden()
    {
        Beantwortet = false;
        var optionen = Runde[Index].Optionen.ToList();
        _zufall.Shuffle(optionen);
        Optionen = optionen;
    }

    /// <summary>submit_answer: bewerten und sofort speichern; ist schon beantwortet, weiter (null).</summary>
    public UebungAntwort? Einreichen(string? auswahl)
    {
        if (!Laeuft || Frage is not { } frage)
        {
            return null;
        }
        if (Beantwortet)
        {
            return null;
        }
        if (string.IsNullOrEmpty(auswahl))
        {
            return new UebungAntwort(KartenTexte.AuswahlFehlt, false, "", TrainerTexte.Einreichen);
        }
        var richtig = auswahl == frage.Antwort;
        if (richtig)
        {
            Punkte++;
        }
        Beantwortet = true;
        var vor = richtig ? TrainerTexte.ErklaerungRichtig : Beschriftungen.Einsetzen(TrainerTexte.ErklaerungFalsch, frage.Antwort!);
        _schreiber?.QuizSpeichern(frage.Fachbereich!, frage.Frage!, richtig);
        NachSpeichern?.Invoke();
        var letzte = Index >= Runde.Count - 1;
        return new UebungAntwort(null, richtig, vor + (frage.Text("exp") ?? ""), letzte ? TrainerTexte.Auswertung : TrainerTexte.NaechsteFrage);
    }

    /// <summary>_advance: naechste Frage oder Ende (dann das Ergebnis).</summary>
    public UebungErgebnis? Weiter()
    {
        if (!Laeuft || !Beantwortet)
        {
            return null;
        }
        if (Index >= Runde.Count - 1)
        {
            return Beenden();
        }
        Index++;
        FrageLaden();
        return null;
    }

    private UebungErgebnis Beenden()
    {
        var gesamt = Runde.Count;
        var prozent = Trainer.Prozent(Punkte, gesamt);
        var sekunden = _sekunden() - _start;
        var note = Trainer.Note(prozent);
        var gespeichert = _schreiber?.ErgebnisSpeichern(Punkte, gesamt, prozent, note, sekunden) ?? false;
        var ergebnis = new UebungErgebnis(Punkte, gesamt, prozent, note, sekunden, gespeichert);
        Zuruecksetzen();
        NachSpeichern?.Invoke();
        return ergebnis;
    }

    /// <summary>cancel_quiz nach "Ja": kein Ergebnis, beantwortete Fragen bleiben gespeichert.</summary>
    public void Abbrechen()
    {
        if (Laeuft)
        {
            Zuruecksetzen();
        }
    }

    private void Zuruecksetzen()
    {
        Laeuft = false;
        Beantwortet = false;
        Optionen = [];
        PoolNeu();
    }

    /// <summary>Nach jedem Speichern (notify_progress).</summary>
    public event Action? NachSpeichern;
}
