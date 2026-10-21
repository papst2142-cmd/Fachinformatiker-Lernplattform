using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Core.Rechner;

/// <summary>Zustand des Rechen-Trainers (Verhaltenstabelle R7, Meldung P3-11 Anlage).</summary>
public enum TrainerZustand
{
    /// <summary>A: noch keine Runde (tasks = []).</summary>
    KeineRunde,

    /// <summary>B: Aufgabe offen (checked = False).</summary>
    Offen,

    /// <summary>C: Aufgabe geprueft (checked = True).</summary>
    Geprueft,

    /// <summary>D: Runde beendet (tasks = [] nach der Auswertung).</summary>
    Beendet,
}

/// <summary>Rueckmeldung eines Feldes nach dem Pruefen: "richtig" oder "richtig wäre: …".</summary>
public sealed record TrainerMarke(string Schluessel, bool Richtig, string Text);

/// <summary>Ergebnis von "Prüfen": Marken je Feld, Rueckmeldung, Rechenweg, neuer Knopftext.</summary>
public sealed record TrainerPruefung(IReadOnlyList<TrainerMarke> Marken, bool AllesRichtig, string Rueckmeldung, string Rechenweg, string Knopf);

/// <summary>
/// Phase 3, W3 (Paket R7, Weisung P3-12): Ablauf des Rechen-Trainers wie TrainerPanel in Python
/// (app_gui 5490-5757, mobile/src/main.py 2950-3081), fuer PC und Handy gleich (ein Aufbau).
/// Gespeichert wird genau eine Zeile je "Prüfen" (log_trainer, auch bei leeren Feldern, dann
/// falsch; nur bei den W4a-Arten mit Zahlenfeldern ist eine ungueltige Eingabe ein
/// <see cref="Eingabefehler"/> ohne Speichern, Auflage P3-17 4d), danach genau einmal <see cref="NachSpeichern"/> (Abgleich anstossen, P3-12 F1 a,
/// auf beiden Geraeten). Zweiter Druck, "Nächste Aufgabe", "Auswertung", "Runde starten",
/// Art/Stufe waehlen schreiben nie. Ein abgelehnter Speicherversuch bleibt still (den Hinweis
/// zeigt der vorhandene Weg Lernstand -> "Datenbankfehler"); die Aufgabe zaehlt trotzdem in der
/// Runde. Die Statistikzeile wird nur beim ersten Zeigen und am Rundenende gelesen.
/// Die Runde wird mit demselben Startwert breit und schmal gebaut (gleiche Aufgaben, nur der
/// Rechenweg ist anders); welcher Rechenweg gezeigt wird, entscheidet der Aufbau beim Pruefen.
/// </summary>
public sealed class RechenTrainerAblauf
{
    private readonly LernstandSchreiber? _schreiber;
    private readonly Func<long> _startwert;
    private List<TrainerAufgabe> _breit = [];
    private List<TrainerAufgabe> _schmal = [];
    private readonly List<bool> _ergebnisse = [];
    private bool _statistikGelesen;

    /// <param name="schreiber">Speichern und Statistik (null: nichts speichern, Statistik leer).</param>
    /// <param name="startwert">Startwert je Runde (Vorgabe random.randrange(1 &lt;&lt; 30)).</param>
    public RechenTrainerAblauf(LernstandSchreiber? schreiber, Func<long>? startwert = null)
    {
        _schreiber = schreiber;
        _startwert = startwert ?? RechenTrainer.NeuerStartwert;
    }

    /// <summary>Nach jedem gespeicherten oder abgelehnten "Prüfen" genau einmal.</summary>
    public event Action? NachSpeichern;

    /// <summary>Gewaehlte Aufgabenart (Pille; Vorgabe die erste wie PillGroup).</summary>
    public string Art { get; private set; } = RechenTrainer.Arten[0].Art;

    /// <summary>Gewaehlte Stufe (Pille; Vorgabe die erste).</summary>
    public int Stufe { get; private set; } = RechenTrainer.Stufen[0].Stufe;

    public TrainerZustand Zustand { get; private set; } = TrainerZustand.KeineRunde;

    /// <summary>Index der aktuellen Aufgabe in der Runde.</summary>
    public int Index { get; private set; }

    /// <summary>Aufgaben der laufenden Runde (0 ohne Runde).</summary>
    public int Anzahl => _breit.Count;

    /// <summary>Ergebnisse der laufenden Runde (results).</summary>
    public IReadOnlyList<bool> Ergebnisse => _ergebnisse;

    /// <summary>Aktuelle Aufgabe (B und C), sonst null.</summary>
    public TrainerAufgabe? Aufgabe => Zustand is TrainerZustand.Offen or TrainerZustand.Geprueft ? _breit[Index] : null;

    /// <summary>Statistikzeile "bisher …" (leer, solange nichts geloest oder noch nicht gelesen).</summary>
    public string Statistik { get; private set; } = "";

    /// <summary>Wie oft die Statistik gelesen wurde (Beleg Tabellenzeile 1 und 12).</summary>
    public int StatistikLesungen { get; private set; }

    /// <summary>
    /// Kopfzeile der Aufgabe "IPv4-Subnetz · Leicht" (subtitle/lbl_head): vor der ersten Runde
    /// leer; nach dem Rundenende bleibt die letzte stehen (advance setzt sie in Python nicht zurueck).
    /// </summary>
    public string Kopf { get; private set; } = "";

    /// <summary>Aufgabentext (lbl_task): Start-Satz, Aufgabe oder "Runde beendet: …".</summary>
    public string AufgabeText { get; private set; } = Beschriftungen.Einsetzen(TrainerStart, RechenTrainer.Runde);

    /// <summary>Rueckmeldung unter den Feldern (lbl_feedback).</summary>
    public string Rueckmeldung { get; private set; } = "";

    /// <summary>"Aufgabe 3 / 10" (lbl_counter), ohne Runde leer.</summary>
    public string Zaehler => Aufgabe is null ? "" : Beschriftungen.Einsetzen(AufgabeVon, Index + 1, Anzahl);

    /// <summary>Text des Knopfes: "Prüfen", nach dem Pruefen "Nächste Aufgabe" oder "Auswertung".</summary>
    public string Knopf => Zustand == TrainerZustand.Geprueft
        ? (Index >= Anzahl - 1 ? Auswertung : NaechsteAufgabe)
        : Pruefen;

    /// <summary>Knopf bedienbar (B, C); ohne Runde gesperrt, auf PC und Handy (P3-12 Punkt 4).</summary>
    public bool KnopfAktiv => Zustand is TrainerZustand.Offen or TrainerZustand.Geprueft;

    /// <summary>
    /// W4a (Auflage P3-17 4d): roter Hinweis nach einer ungueltigen Eingabe in Zahlenfeldern
    /// (keine Zahl, leer, mit Einheit); nichts gespeichert, die Aufgabe bleibt offen. Sonst leer.
    /// </summary>
    public string Eingabefehler { get; private set; } = "";

    /// <summary>Letzte Pruefung der aktuellen Aufgabe (nur in C), sonst null.</summary>
    public TrainerPruefung? Pruefung { get; private set; }

    /// <summary>Pille "Aufgabenart": nur die Wahl, eine laufende Runde bleibt.</summary>
    public void SetzeArt(string art)
    {
        if (RechenTrainer.AlleArten.Any(a => a.Art == art))
        {
            Art = art;
        }
    }

    /// <summary>Pille "Schwierigkeit": nur die Wahl, eine laufende Runde bleibt.</summary>
    public void SetzeStufe(int stufe)
    {
        if (RechenTrainer.Stufen.Any(s => s.Stufe == stufe))
        {
            Stufe = stufe;
        }
    }

    /// <summary>Erstes Zeigen des Reiters "Trainer": Statistik lesen (show_stats im Bau), danach nie wieder.</summary>
    public void ErstesZeigen()
    {
        if (!_statistikGelesen)
        {
            StatistikLesen();
        }
    }

    /// <summary>"Runde starten" (auch mitten in einer Runde): neue Runde, alte verworfen, schreibt nichts.</summary>
    public void RundeStarten()
    {
        var startwert = _startwert();
        _breit = RechenTrainer.RundeBauen(Art, Stufe, startwert);
        _schmal = RechenTrainer.RundeBauen(Art, Stufe, startwert, schmal: true);
        Index = 0;
        _ergebnisse.Clear();
        AufgabeLaden();
    }

    /// <summary>
    /// Der eine Knopf (PC auch Eingabetaste): in B pruefen, in C weiter (advance), ohne Runde
    /// nichts. Liefert die Pruefung nur, wenn gerade geprueft wurde.
    /// </summary>
    public TrainerPruefung? Druecken(IReadOnlyDictionary<string, string> antworten, bool schmal)
    {
        switch (Zustand)
        {
            case TrainerZustand.Offen:
                Eingabefehler = _breit[Index].EingabeFehler(antworten) ?? "";
                return Eingabefehler.Length > 0 ? null : PruefenUndSpeichern(antworten, schmal);
            case TrainerZustand.Geprueft:
                Weiter();
                return null;
            default:
                return null;
        }
    }

    /// <summary>Rechenweg der aktuellen Aufgabe nach dem Aufbau (schmal = Handy-Fassung).</summary>
    public string Rechenweg(bool schmal) => Aufgabe is null ? "" : string.Join("\n", schmal ? _schmal[Index].HandySchritte : _breit[Index].Schritte);

    private TrainerPruefung PruefenUndSpeichern(IReadOnlyDictionary<string, string> antworten, bool schmal)
    {
        var aufgabe = _breit[Index];
        var marken = aufgabe.Pruefen(antworten)
            .Select(p => new TrainerMarke(p.Schluessel, p.Richtig, p.Richtig ? Richtig : Beschriftungen.Einsetzen(RichtigWaere, aufgabe.Loesung[p.Schluessel])))
            .ToList();
        var alle = marken.All(m => m.Richtig);
        _ergebnisse.Add(alle);
        _schreiber?.TrainerSpeichern(aufgabe.Art, aufgabe.Stufe, alle);
        NachSpeichern?.Invoke();
        Rueckmeldung = alle ? AllesRichtig : NochNichtGanz;
        Zustand = TrainerZustand.Geprueft;
        Pruefung = new TrainerPruefung(marken, alle, Rueckmeldung, Rechenweg(schmal), Knopf);
        return Pruefung;
    }

    private void Weiter()
    {
        if (Index < Anzahl - 1)
        {
            Index++;
            AufgabeLaden();
            return;
        }
        Eingabefehler = "";
        var zusammenfassung = RechenTrainer.Zusammenfassung(_ergebnisse);
        _breit = [];
        _schmal = [];
        Index = 0;
        Pruefung = null;
        Zustand = TrainerZustand.Beendet;
        AufgabeText = Beschriftungen.Einsetzen(RundeBeendet, zusammenfassung);
        Rueckmeldung = NeueRunde;
        StatistikLesen();
    }

    private void AufgabeLaden()
    {
        Eingabefehler = "";
        Zustand = TrainerZustand.Offen;
        Pruefung = null;
        AufgabeText = _breit[Index].Text;
        Kopf = Beschriftungen.Einsetzen(ArtUndStufe, RechenTrainer.ArtName(_breit[Index].Art), RechenTrainer.StufeName(_breit[Index].Stufe));
        Rueckmeldung = "";
    }

    private void StatistikLesen()
    {
        _statistikGelesen = true;
        StatistikLesungen++;
        Statistik = _schreiber is null ? "" : RechenTrainer.StatistikZeile(_schreiber.TrainerStatistik());
    }
}
