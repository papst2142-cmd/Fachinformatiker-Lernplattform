using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Zufall;

namespace Lernplattform.Core.Lernen;

/// <summary>Rueckmeldung nach "Antwort pruefen" / "Loesung aufdecken".</summary>
/// <param name="Warnung">Statt einer Rueckmeldung ein Hinweis (nichts gespeichert).</param>
/// <param name="Farbe">Farbschluessel der Rueckmeldung: green, yellow, red, accent.</param>
/// <param name="Bewerten">"Wusstest du es?" zeigen (Aufdecken, noch nicht bewertet).</param>
public sealed record KartenRueckmeldung(string? Warnung, string Text, string Farbe, string Loesung, bool? Richtig, bool Bewerten);

/// <summary>Gemeinsame Regeln fuer Karteikarten und Pruefungstrainer.</summary>
public static class Lernregeln
{
    /// <summary>
    /// E1 (Plan Phase 2): Multiple-Choice-Antworten einer Karte in fester Reihenfolge,
    /// gemischt mit PythonZufall aus MD5 der Frage (letzte 16 Bit, wie Pythons "&amp; 0xFFFF").
    /// </summary>
    public static List<string> MehrfachwahlReihenfolge(string frage, IEnumerable<string> optionen)
    {
        var md5 = MD5.HashData(Encoding.UTF8.GetBytes(frage));
        var startwert = new BigInteger(md5, isUnsigned: true, isBigEndian: true) & 0xFFFF;
        var liste = optionen.ToList();
        new PythonZufall(startwert).Shuffle(liste);
        return liste;
    }

    /// <summary>Freitext richtig wie Python: card["a"].lower() in eingabe.lower().</summary>
    public static bool FreitextRichtig(string kurzantwort, string eingabe) =>
        RahmenplanGewicht.Klein(eingabe).Contains(RahmenplanGewicht.Klein(kurzantwort), StringComparison.Ordinal);

    // str.isspace in Python (auch die Trennzeichen 0x1C-0x1F)
    private static bool PyLeer(char z) => (int)z is (>= 0x09 and <= 0x0D) or (>= 0x1C and <= 0x20) or 0x85 or 0xA0 or 0x1680
        or (>= 0x2000 and <= 0x200A) or 0x2028 or 0x2029 or 0x202F or 0x205F or 0x3000;

    /// <summary>str.strip() wie Python.</summary>
    public static string Strip(string text)
    {
        var von = 0;
        var bis = text.Length;
        while (von < bis && PyLeer(text[von]))
        {
            von++;
        }
        while (bis > von && PyLeer(text[bis - 1]))
        {
            bis--;
        }
        return text[von..bis];
    }

    /// <summary>Themen fuer die Liste "Thema" (TopicMenu.set_category): alle oder die des Fachbereichs.</summary>
    public static IReadOnlyList<Thema> ThemenFuer(Lerninhalte inhalte, string fachbereich) =>
        fachbereich == KartenTexte.Alle
            ? Fachbereiche.Alle.SelectMany(f => inhalte.Themen.TryGetValue(f, out var t) ? t : []).ToList()
            : inhalte.Themen.TryGetValue(fachbereich, out var liste) ? liste : [];
}

/// <summary>
/// Phase 2, L4: Ablauf der Karteikarten wie app_gui.CardsView (Python v0.63.2), ohne Oberflaeche.
/// Gespeichert wird je Anzeige einer Karte hoechstens einmal: Freitext und Multiple Choice beim
/// ersten Pruefen, Aufdecken mit "Gewusst"/"Nicht gewusst"; aufgedeckt, aber nicht bewertet,
/// beim Weiterblaettern, Filter- oder Moduswechsel und beim Beenden als "angesehen" (correct NULL).
/// </summary>
public sealed class Karteikarten
{
    private readonly Lerninhalte _inhalte;
    private readonly Func<StatusBuch> _buchLesen;
    private readonly LernstandSchreiber? _schreiber;
    private readonly Func<Func<List<string>, List<string>>?> _neuReihenfolge;
    private readonly Dictionary<string, Lerninhalt> _nachFrage = new(StringComparer.Ordinal);
    private List<string> _zaehlSchluessel = [];
    private Lerninhalt? _offen;
    private bool _gespeichert;

    /// <param name="buchLesen">Liest den Lernstand neu (StatusBook(db)).</param>
    /// <param name="schreiber">Speichert Antworten; null = nichts speichern.</param>
    /// <param name="neuReihenfolge">fresh_order(SRC_CARD): Rahmenplan-Reihenfolge der neuen Karten, je Aufruf neu.</param>
    public Karteikarten(Lerninhalte inhalte, Func<StatusBuch> buchLesen, LernstandSchreiber? schreiber,
        Func<Func<List<string>, List<string>>?>? neuReihenfolge = null)
    {
        _inhalte = inhalte;
        _buchLesen = buchLesen;
        _schreiber = schreiber;
        _neuReihenfolge = neuReihenfolge ?? (() => null);
        foreach (var karte in inhalte.Karteikarten)
        {
            _nachFrage.TryAdd(karte.Frage ?? "", karte);
        }
        Buch = buchLesen();
        FilterAnwenden();
    }

    public StatusBuch Buch { get; private set; }

    public string Fachbereich { get; private set; } = KartenTexte.Alle;

    /// <summary>Themen-Kennung oder "Alle".</summary>
    public string Thema { get; private set; } = KartenTexte.Alle;

    public string Status { get; private set; } = FragenStatus.Alle;

    public string Modus { get; private set; } = KartenTexte.Freitext;

    public IReadOnlyList<Lerninhalt> Gefiltert { get; private set; } = [];

    public int Index { get; private set; }

    public Lerninhalt? Karte => Gefiltert.Count > 0 ? Gefiltert[Index] : null;

    /// <summary>Wurde die gezeigte Karte schon gespeichert (Merker logged)?</summary>
    public bool Gespeichert => _gespeichert;

    /// <summary>Aufgedeckt, aber noch nicht bewertet (pending).</summary>
    public bool WartetAufBewertung => _offen is not null;

    /// <summary>Speichern gescheitert (Lernstand lehnt ab) beim letzten Speichern.</summary>
    public bool LetztesSpeichernFehlgeschlagen { get; private set; }

    public IReadOnlyList<Thema> Themen => Lernregeln.ThemenFuer(_inhalte, Fachbereich);

    /// <summary>count_text der Basis (vor dem Status-Filter).</summary>
    public string ZaehlText => KartenTexte.ZaehlText(Buch.Zaehlen(Quellen.Karte, _zaehlSchluessel.Cast<object?>()));

    public string Zaehler => Karte is null ? KartenTexte.ZaehlerLeer
        : Beschriftungen.Einsetzen(KartenTexte.Zaehler, Beschriftungen.MitPunkten(Index + 1), Beschriftungen.MitPunkten(Gefiltert.Count));

    public string Untertitel => Karte is not { } k ? ""
        : Beschriftungen.Einsetzen(KartenTexte.Untertitel, Fachbereiche.Kurzname[k.Fachbereich!],
            _inhalte.ThemaNach.TryGetValue(k.Thema ?? "", out var t) ? t.Kurz : k.Thema ?? "");

    public (string Text, string Farbe) StatusZeile => Karte is { } k ? KartenTexte.StatusZeile(Buch, Quellen.Karte, k.Frage!) : ("", KartenTexte.FarbeOffen);

    /// <summary>Text des Pruefknopfs je Modus.</summary>
    public string PruefKnopf => Modus == KartenTexte.Aufdecken ? KartenTexte.LoesungAufdecken : KartenTexte.Pruefen;

    /// <summary>Antworten fuer Multiple Choice in fester Reihenfolge (E1).</summary>
    public IReadOnlyList<string> Optionen => Karte is { } k ? Lernregeln.MehrfachwahlReihenfolge(k.Frage!, k.Optionen) : [];

    /// <summary>Fachbereich waehlen (Pille oder Seitenleiste); Thema zurueck auf "Alle", ausser es passt.</summary>
    public void SetzeFachbereich(string fachbereich, string? thema = null)
    {
        Fachbereich = fachbereich;
        Thema = thema is not null && Themen.Any(t => t.Id == thema) ? thema : KartenTexte.Alle;
        FilterAnwenden();
    }

    public void SetzeThema(string thema)
    {
        Thema = thema;
        FilterAnwenden();
    }

    public void SetzeStatus(string status)
    {
        Status = status;
        FilterAnwenden();
    }

    public void SetzeModus(string modus)
    {
        Flush();
        Modus = modus;
        NeueAnzeige();
    }

    /// <summary>apply_filter: Fachbereich, Thema und Status anwenden; ohne Status-Filter unbearbeitete zuerst.</summary>
    public void FilterAnwenden()
    {
        Flush();
        Buch = _buchLesen();
        var basis = _inhalte.Karteikarten
            .Where(k => (Fachbereich == KartenTexte.Alle || k.Fachbereich == Fachbereich)
                && (Thema == KartenTexte.Alle || k.Thema == Thema))
            .ToList();
        var schluessel = basis.Select(k => k.Frage!).ToList();
        schluessel = Status == FragenStatus.Alle
            ? Buch.BevorzugteReihenfolge(Quellen.Karte, schluessel, null, _neuReihenfolge())
            : schluessel.Where(s => Buch.Status(Quellen.Karte, s).Status == Status).ToList();
        Gefiltert = schluessel.Select(s => _nachFrage[s]).ToList();
        Index = 0;
        _zaehlSchluessel = basis.Select(k => k.Frage!).ToList();
        NeueAnzeige();
    }

    /// <summary>practice: gezielte Runde nur mit diesen Karten (Jetzt wiederholen); Filter auf "Alle".</summary>
    public void Ueben(IEnumerable<string> fragen)
    {
        Fachbereich = KartenTexte.Alle;
        Thema = KartenTexte.Alle;
        Status = FragenStatus.Alle;
        Flush();
        Buch = _buchLesen();
        Gefiltert = fragen.Where(_nachFrage.ContainsKey).Select(f => _nachFrage[f]).ToList();
        Index = 0;
        _zaehlSchluessel = Gefiltert.Select(k => k.Frage!).ToList();
        NeueAnzeige();
    }

    public void Weiter()
    {
        if (Gefiltert.Count > 0)
        {
            Flush();
            Index = (Index + 1) % Gefiltert.Count;
            NeueAnzeige();
        }
    }

    public void Zurueck()
    {
        if (Gefiltert.Count > 0)
        {
            Flush();
            Index = ((Index - 1) % Gefiltert.Count + Gefiltert.Count) % Gefiltert.Count;
            NeueAnzeige();
        }
    }

    /// <summary>check_answer. eingabe = Freitext, auswahl = gewaehlte Antwort (Multiple Choice).</summary>
    public KartenRueckmeldung? Pruefen(string? eingabe, string? auswahl)
    {
        if (Karte is not { } karte)
        {
            return null;
        }
        bool richtig;
        KartenRueckmeldung antwort;
        var voll = karte.Text("a_full") ?? "";
        if (Modus == KartenTexte.Freitext)
        {
            var text = Lernregeln.Strip(eingabe ?? "");
            if (text.Length == 0)
            {
                return new KartenRueckmeldung(KartenTexte.EingabeFehlt, "", "", "", null, false);
            }
            richtig = Lernregeln.FreitextRichtig(karte.Antwort ?? "", text);
            antwort = new KartenRueckmeldung(null, richtig ? KartenTexte.FreitextRichtig : KartenTexte.FreitextVergleich,
                richtig ? "green" : "yellow", KartenTexte.MusterloesungVor + voll, richtig, false);
        }
        else if (Modus == KartenTexte.Mehrfachwahl)
        {
            if (string.IsNullOrEmpty(auswahl))
            {
                return new KartenRueckmeldung(KartenTexte.AuswahlFehlt, "", "", "", null, false);
            }
            richtig = auswahl == karte.Antwort;
            antwort = new KartenRueckmeldung(null, richtig ? KartenTexte.McRichtig : KartenTexte.McFalschVor + karte.Antwort,
                richtig ? "green" : "red", voll, richtig, false);
        }
        else
        {
            var bewerten = !_gespeichert;
            if (bewerten)
            {
                _offen = karte;
            }
            return new KartenRueckmeldung(null, KartenTexte.Musterloesung, "accent", voll, null, bewerten);
        }
        if (!_gespeichert)
        {
            _gespeichert = true;
            Speichern(karte, Modus, richtig);
        }
        return antwort;
    }

    /// <summary>rate: Selbsteinschaetzung nach dem Aufdecken.</summary>
    public void Bewerten(bool richtig)
    {
        var karte = _offen;
        _offen = null;
        if (karte is null)
        {
            return;
        }
        _gespeichert = true;
        Speichern(karte, Modus, richtig);
    }

    /// <summary>on_show: Antworten neu lesen (z. B. nach einer Uebernahme); Auswahl und Karte bleiben.</summary>
    public void NeuLesen() => Buch = _buchLesen();

    /// <summary>Beim Beenden des Programms (flush_inputs): Aufgedecktes als angesehen speichern.</summary>
    public void Beenden() => Flush();

    private void Flush()
    {
        if (_offen is { } karte)
        {
            _offen = null;
            Speichern(karte, KartenTexte.Aufdecken, null, false);
        }
    }

    private void Speichern(Lerninhalt karte, string modus, bool? richtig, bool neuLesen = true)
    {
        LetztesSpeichernFehlgeschlagen = _schreiber is null || !_schreiber.KarteSpeichern(karte.Fachbereich!, karte.Frage!, modus, richtig);
        if (neuLesen)
        {
            Buch = _buchLesen();
        }
        NachSpeichern?.Invoke();
    }

    /// <summary>Nach jedem Speichern (notify_progress).</summary>
    public event Action? NachSpeichern;

    private void NeueAnzeige()
    {
        Flush();
        _gespeichert = false;
    }
}
