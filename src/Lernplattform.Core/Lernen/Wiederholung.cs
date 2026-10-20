using System.Security.Cryptography;
using System.Text;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Inhalte;

namespace Lernplattform.Core.Lernen;

/// <summary>Was der Plan je Frage braucht (fisi_lernen.review_info).</summary>
/// <param name="Alt">Bewertet vor dem Start der Wiederholung (Altbestand).</param>
/// <param name="ErsterNeuerTag">Tag der ersten Bewertung ab dem Start oder null.</param>
public sealed record WiederholungInfo(int Stufe, DateOnly Faellig, bool Alt, DateOnly? ErsterNeuerTag);

/// <summary>
/// Wiederholungssystem wie fisi_lernen (Python v0.63.2): Abstaende 1, 3, 7, 14, 30 Tage,
/// richtig am oder nach dem Faelligkeitstag hebt die Stufe, ein Fehler setzt auf Anfang
/// (morgen wieder faellig). Altbestand vor dem 01.10.2026: hoechstens 20 am Tag.
/// Rechnet nur aus den Antworten, kein Zufall: PC und Handy rechnen gleich.
/// </summary>
public static class Wiederholung
{
    public static readonly IReadOnlyList<int> Abstaende = [1, 3, 7, 14, 30];       // SR_INTERVALS
    public static readonly DateOnly Start = new(2026, 10, 1);                     // SR_START
    public const int AltbestandJeTag = 20;                                        // SR_LEGACY_PER_DAY
    public const int SitzungMax = 50;                                             // SR_SESSION_MAX
    public static readonly IReadOnlyList<string> Quellen = [Lernen.Quellen.Karte, Lernen.Quellen.Quiz];   // SR_SOURCES

    /// <summary>_date: date.fromisoformat(str(timestamp)[:10]); anderes als JJJJ-MM-TT bricht ab wie in Python.</summary>
    public static DateOnly Tag(object? zeit) =>
        PyText.IsoTag(PyText.Teil(PyText.Str(zeit), 0, 10), out var tag) ? tag : throw new FormatException("Zeitstempel");

    /// <summary>review_state: (Stufe, faellig am) aus den Antworten (aelteste zuerst); null = nie bewertet.</summary>
    public static (int Stufe, DateOnly? Faellig) Zustand(IEnumerable<Antwort> antworten)
    {
        int stufe = 0;
        DateOnly? faellig = null;
        foreach (var (zeit, wert) in antworten)
        {
            if (wert is null)
            {
                continue;
            }
            var tag = Tag(zeit);
            if (!wert.Value)
            {
                (stufe, faellig) = (0, tag.AddDays(1));
            }
            else if (faellig is null || tag >= faellig)
            {
                stufe = Math.Min(stufe + 1, Abstaende.Count);
                faellig = tag.AddDays(Abstaende[stufe - 1]);
            }
        }
        return (stufe, faellig);
    }

    /// <summary>review_info: null, wenn keine Antwort bewertet ist.</summary>
    public static WiederholungInfo? Info(IEnumerable<Antwort> antworten, DateOnly start)
    {
        var bewertet = antworten.Where(a => a.Wert is not null).ToList();
        if (bewertet.Count == 0)
        {
            return null;
        }
        var (stufe, faellig) = Zustand(bewertet);
        var tage = bewertet.Select(a => Tag(a.Zeit)).ToList();
        DateOnly? ersterNeuer = tage.Where(t => t >= start).Select(t => (DateOnly?)t).FirstOrDefault();
        return new WiederholungInfo(stufe, faellig!.Value, tage.Any(t => t < start), ersterNeuer);
    }

    /// <summary>_stable_rank: MD5 nur fuer eine feste Reihenfolge, nicht fuer Sicherheit.</summary>
#pragma warning disable CA5351 // MD5 nur als feste Reihenfolge wie in Python, keine Sicherheitsfunktion
    public static string StabilerRang(string quelle, string schluessel) =>
        Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(quelle + "|" + schluessel)));
#pragma warning restore CA5351

    /// <summary>Gueltige Schluessel je Quelle: alle Karteikarten und alle Fragen des Pruefungstrainers (auch erzeugte).</summary>
    public static Dictionary<string, IReadOnlySet<string>> Gueltig(Lerninhalte inhalte) => new()
    {
        [Lernen.Quellen.Karte] = inhalte.Karteikarten.Select(k => k.Frage!).ToHashSet(StringComparer.Ordinal),
        [Lernen.Quellen.Quiz] = inhalte.Quizfragen.Select(f => f.Frage!).ToHashSet(StringComparer.Ordinal),
    };
}

/// <summary>
/// Faellige Wiederholungen fuer einen Tag (fisi_lernen.ReviewPlan): je Quelle die Schluessel,
/// zuerst die am laengsten ueberfaelligen, danach der heute freigegebene Altbestand.
/// </summary>
public sealed class Wiederholungsplan
{
    private readonly Dictionary<(string, string), (int Stufe, DateOnly Faellig)> _zustand = [];
    private readonly Dictionary<string, List<string>> _faellig;

    public Wiederholungsplan(StatusBuch buch, DateOnly heute, IReadOnlyDictionary<string, IReadOnlySet<string>> gueltig)
    {
        Heute = heute;
        var start = Wiederholung.Start;
        var regulaer = new List<(DateOnly Faellig, string Quelle, string Schluessel)>();
        var altbestand = new List<(string Quelle, string Schluessel)>();
        var heuteUmgestellt = 0;
        foreach (var (kennung, antworten) in buch.Antworten.Ergebnisse)
        {
            if (!Wiederholung.Quellen.Contains(kennung.Quelle) || kennung.Schluessel is not string schluessel
                || !gueltig[kennung.Quelle].Contains(schluessel))
            {
                continue;
            }
            if (Wiederholung.Info(antworten, start) is not { } info)
            {
                continue;
            }
            _zustand[(kennung.Quelle, schluessel)] = (info.Stufe, info.Faellig);
            if (info.Alt && info.ErsterNeuerTag is null)
            {
                altbestand.Add((kennung.Quelle, schluessel));
                continue;
            }
            if (info.Alt && info.ErsterNeuerTag == heute)
            {
                heuteUmgestellt++;
            }
            if (info.Faellig <= heute)
            {
                regulaer.Add((info.Faellig, kennung.Quelle, schluessel));
            }
        }
        regulaer = regulaer.OrderBy(r => r.Faellig)
            .ThenBy(r => Wiederholung.StabilerRang(r.Quelle, r.Schluessel), StringComparer.Ordinal).ToList();
        var freigegeben = new List<(string Quelle, string Schluessel)>();
        if (heute >= start)
        {
            // Altbestand: zu Uebende (rot vor gelb) zuerst, sonst feste Reihenfolge
            freigegeben = altbestand
                .OrderBy(a => buch.Status(a.Quelle, a.Schluessel) switch
                {
                    (FragenStatus.Ueben, FragenStatus.Rot) => 0,
                    (FragenStatus.Ueben, _) => 1,
                    (FragenStatus.Fertig, _) => 3,
                    _ => 2,
                })
                .ThenBy(a => Wiederholung.StabilerRang(a.Quelle, a.Schluessel), StringComparer.Ordinal)
                .Take(Math.Max(0, Wiederholung.AltbestandJeTag - heuteUmgestellt))
                .ToList();
        }
        AltbestandWartet = altbestand.Count - freigegeben.Count;
        _faellig = Wiederholung.Quellen.ToDictionary(q => q, _ => new List<string>());
        foreach (var (_, quelle, schluessel) in regulaer)
        {
            _faellig[quelle].Add(schluessel);
        }
        foreach (var (quelle, schluessel) in freigegeben)
        {
            _faellig[quelle].Add(schluessel);
        }
    }

    /// <summary>Plan aus dem Lernstand fuer die Inhalte (gueltige Schluessel).</summary>
    public static Wiederholungsplan Aus(StatusBuch buch, DateOnly heute, Lerninhalte inhalte) =>
        new(buch, heute, Wiederholung.Gueltig(inhalte));

    public DateOnly Heute { get; }

    /// <summary>legacy_waiting: Altbestand, der heute noch nicht freigegeben ist.</summary>
    public int AltbestandWartet { get; }

    /// <summary>Faellige Schluessel je Quelle (Reihenfolge der Wiederholung).</summary>
    public IReadOnlyList<string> Faellig(string quelle) => _faellig.TryGetValue(quelle, out var liste) ? liste : [];

    /// <summary>count: Anzahl faellig (je Quelle oder gesamt).</summary>
    public int Anzahl(string? quelle = null) => quelle is null ? _faellig.Values.Sum(l => l.Count) : Faellig(quelle).Count;

    public bool IstFaellig(string quelle, string schluessel) => Faellig(quelle).Contains(schluessel);

    /// <summary>Stufe und Faelligkeit aller gueltigen Fragen mit Antworten (ReviewPlan.state).</summary>
    public IReadOnlyDictionary<(string Quelle, string Schluessel), (int Stufe, DateOnly Faellig)> Zustaende => _zustand;

    /// <summary>Stufe einer Frage (0 = ohne Antwort oder ungueltig).</summary>
    public int Stufe(string quelle, string schluessel) => _zustand.TryGetValue((quelle, schluessel), out var z) ? z.Stufe : 0;

    /// <summary>Schluessel fuer eine Wiederholungs-Sitzung (hoechstens limit).</summary>
    public List<string> Sitzung(string quelle, int limit = Wiederholung.SitzungMax) => Faellig(quelle).Take(limit).ToList();

    /// <summary>first_source: zuerst Karteikarten, dann Pruefungstrainer; null = nichts faellig.</summary>
    public string? ErsteQuelle() => Wiederholung.Quellen.FirstOrDefault(q => Faellig(q).Count > 0);

    /// <summary>due_text (gleicher Text wie die Kachel "Heute").</summary>
    public string Text => DashboardTexte.FaelligText(Anzahl(Quellen.Karte), Anzahl(Quellen.Quiz));
}
