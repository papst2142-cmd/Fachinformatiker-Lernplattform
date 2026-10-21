using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;

namespace Lernplattform.Core.Lernen;

/// <summary>
/// Phase 4, PB3: Ablauf der Seiten AP1-/AP2-Szenarien und Testprojekt wie app_gui v0.63.2
/// (ScenarioViewBase 4154, ProjectView 4375; main.py gleich), ohne Oberflaeche:
/// Musterloesung bzw. Loesungsansaetze aufdecken, "Gewusst"/"Nicht gewusst" (eine Zeile je
/// Bewertung), beim Wechsel ohne Bewertung als angesehen speichern (_flush, richtig = null),
/// eigene Loesungstexte nur fuer die Sitzung (Nico F4 a). Gespeichert wird ueber
/// <see cref="LernstandSchreiber"/> (log_ap1, log_scenario, log_project).
/// </summary>
public sealed class Szenarioablauf
{
    private readonly Func<StatusBuch> _buchLesen;
    private readonly LernstandSchreiber? _schreiber;
    private readonly Dictionary<int, string> _eigene = [];
    private StatusBuch? _buch;

    public Szenarioablauf(string quelle, IReadOnlyList<Lerninhalt> eintraege, Func<StatusBuch> buchLesen, LernstandSchreiber? schreiber)
    {
        Quelle = quelle;
        Eintraege = eintraege;
        _buchLesen = buchLesen;
        _schreiber = schreiber;
    }

    /// <summary>ap1, ap2 oder projekt (<see cref="Quellen"/>).</summary>
    public string Quelle { get; }

    public IReadOnlyList<Lerninhalt> Eintraege { get; }

    /// <summary>Position des gezeigten Eintrags (ab 0).</summary>
    public int Index { get; private set; }

    /// <summary>Musterloesung bzw. Loesungsansaetze sichtbar.</summary>
    public bool Sichtbar { get; private set; }

    /// <summary>Aufgedeckt, aber noch nicht bewertet (pending); dann ist "Wusstest du es?" zu sehen.</summary>
    public int? Offen { get; private set; }

    /// <summary>Nach jeder gespeicherten Zeile (Status, Liste und Abgleich auffrischen).</summary>
    public event Action? NachSpeichern;

    /// <summary>Lernstand (einmal gelesen, nach dem Speichern neu).</summary>
    public StatusBuch Buch => _buch ??= _buchLesen();

    /// <summary>Lernstand neu lesen (on_show, nach einem Abgleich).</summary>
    public void NeuLesen() => _buch = null;

    /// <summary>position_statuses der Quelle.</summary>
    public Dictionary<int, (string Status, string Stufe)> Stati() => Listen.PositionsStati(Buch, Quelle);

    /// <summary>status_label des gezeigten Eintrags.</summary>
    public (string Text, string Farbe) StatusZeile => Texte.KartenTexte.StatusZeile(Buch, Quelle, (long)Index);

    public Lerninhalt Eintrag => Eintraege[Index];

    /// <summary>load_scenario / load_project: erst offene Aufdeckung speichern, dann wechseln (Loesung ausgeblendet).</summary>
    public void Laden(int position)
    {
        Speichern();
        Index = position;
        Sichtbar = false;
    }

    /// <summary>toggle_solution / toggle_hints: beim Aufdecken ohne offene Bewertung wird sie offen.</summary>
    public void Umschalten()
    {
        if (Sichtbar)
        {
            Sichtbar = false;
            return;
        }
        Sichtbar = true;
        Offen ??= Index;
    }

    /// <summary>rate: Selbsteinschaetzung; ohne offene Aufdeckung geschieht nichts (nie doppelt).</summary>
    public void Bewerten(bool richtig)
    {
        if (Offen is not { } position)
        {
            return;
        }
        Offen = null;
        Schreiben(position, richtig);
    }

    /// <summary>_flush: aufgedeckt, aber nicht bewertet = als angesehen speichern (beim Wechsel und Beenden).</summary>
    public void Speichern()
    {
        if (Offen is { } position)
        {
            Offen = null;
            Schreiben(position, null);
        }
    }

    /// <summary>Eigener Loesungstext (nur in dieser Sitzung, nie gespeichert).</summary>
    public string EigeneLoesung(int position) => _eigene.GetValueOrDefault(position, "");

    public void EigeneLoesungSetzen(int position, string text) => _eigene[position] = text;

    private void Schreiben(int position, bool? richtig)
    {
        var eintrag = Eintraege[position];
        var titel = eintrag.Titel ?? "";
        _ = Quelle switch
        {
            Quellen.Ap1 => _schreiber?.Ap1Speichern(position, titel, eintrag.SzenarioThema ?? "", richtig),
            Quellen.Ap2 => _schreiber?.SzenarioSpeichern(position, titel, eintrag.SzenarioThema ?? "", richtig),
            _ => _schreiber?.ProjektSpeichern(position, titel, eintrag.Fachbereich ?? "", richtig),
        };
        _buch = null;
        NachSpeichern?.Invoke();
    }
}
