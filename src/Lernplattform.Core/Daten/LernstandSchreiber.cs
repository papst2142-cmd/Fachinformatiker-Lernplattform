using System.Globalization;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Speichert Antworten wie fisi_core.DBManager (log_card, log_quiz_answer, save_test_result,
/// log_trainer; Phase 3 R5 dazu trainer_stats):
/// Zeitstempel in Ortszeit, utc_offset aus demselben Augenblick, uid = uuid4 als Hex.
/// Jeder Vorgang ist eine Transaktion (Lernstand.Schreiben): die Zeile steht ganz oder gar nicht.
/// Wird nichts gespeichert (Lesefehler, Schreibschutz), lehnt Lernstand still ab.
/// Die Uhrabweichung (sync_meta "uhr_abweichung") misst und schreibt nur der Abgleich (L10);
/// die Uebernahme bringt sie nicht mit. Stempel rechnet sie ein wie Python _stamp (W4).
/// </summary>
public sealed class LernstandSchreiber(Lernstand lernstand, Uhr? uhr = null, Func<string>? kennung = null, Uhrabweichung? abweichung = null)
{
    private readonly Uhr _uhr = uhr ?? Uhr.System;
    private readonly Func<string> _kennung = kennung ?? NeueKennung;
    private readonly Uhrabweichung _abweichung = abweichung ?? Uhrabweichung.System;

    public Lernstand Lernstand { get; } = lernstand;

    /// <summary>_uid: uuid4().hex (32 Kleinbuchstaben/Ziffern).</summary>
    public static string NeueKennung() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// _stamp: (Ortszeit als Text, Abstand zu UTC in Minuten) aus demselben Augenblick. Wich die
    /// Uhr beim letzten Abgleich um mehr als 300 s von GitHub ab, enthaelt der Abstand diese
    /// Abweichung (dann ist Ortszeit minus Abstand die echte UTC-Zeit); nicht mehr, sobald die
    /// Uhr seither richtig gestellt wurde. Ging sie vor und ist ein Sprung seit der Messung nicht
    /// feststellbar, wird nichts eingerechnet (Python 0.63.2 S-1, Weg b). Die Ortszeit bleibt.
    /// </summary>
    public (string Zeit, int Abstand) Stempel()
    {
        var jetzt = _uhr.Jetzt();
        var abstand = Uhr.AbstandMinuten(jetzt);
        var wert = Lernstand.Abfragen("SELECT value FROM sync_meta WHERE key = $k", ("$k", Uhrabweichung.Schluessel)) is [var zeile, ..]
            ? zeile[0] : null;
        var abweichung = _abweichung.Abweichung(wert, jetzt);
        if (abweichung > Uhrabweichung.Toleranz && _abweichung.SprungUnbekannt(wert, jetzt))
        {
            abweichung = null;
        }
        if (abweichung is { } a && Math.Abs(a) > Uhrabweichung.Toleranz)
        {
            abstand += (int)Mathe.PythonRunden.Runde(a / 60.0);
        }
        return (Uhr.Text(jetzt), abstand);
    }

    /// <summary>log_card: richtig null = angesehen, nicht bewertet.</summary>
    public bool KarteSpeichern(string fachbereich, string frage, string modus, bool? richtig)
    {
        var (zeit, abstand) = Stempel();
        return Lernstand.Schreiben(
            "INSERT INTO card_events (timestamp, utc_offset, category, question, mode, correct, uid)"
            + " VALUES ($zeit, $abstand, $fachbereich, $frage, $modus, $richtig, $uid)",
            ("$zeit", zeit), ("$abstand", abstand), ("$fachbereich", fachbereich), ("$frage", frage),
            ("$modus", modus), ("$richtig", richtig is null ? null : richtig.Value ? 1 : 0), ("$uid", _kennung()));
    }

    /// <summary>log_quiz_answer.</summary>
    public bool QuizSpeichern(string fachbereich, string frage, bool richtig)
    {
        var (zeit, abstand) = Stempel();
        return Lernstand.Schreiben(
            "INSERT INTO quiz_answers (timestamp, utc_offset, category, question, correct, uid)"
            + " VALUES ($zeit, $abstand, $fachbereich, $frage, $richtig, $uid)",
            ("$zeit", zeit), ("$abstand", abstand), ("$fachbereich", fachbereich), ("$frage", frage),
            ("$richtig", richtig ? 1 : 0), ("$uid", _kennung()));
    }

    /// <summary>save_test_result: Ergebnis einer Session des Pruefungstrainers.</summary>
    public bool ErgebnisSpeichern(int punkte, int gesamt, double prozent, string note, int dauerSekunden)
    {
        var (zeit, abstand) = Stempel();
        return Lernstand.Schreiben(
            "INSERT INTO test_results (timestamp, utc_offset, score, total, percentage, note, duration_seconds, uid)"
            + " VALUES ($zeit, $abstand, $punkte, $gesamt, $prozent, $note, $dauer, $uid)",
            ("$zeit", zeit), ("$abstand", abstand), ("$punkte", punkte), ("$gesamt", gesamt), ("$prozent", prozent),
            ("$note", note), ("$dauer", dauerSekunden), ("$uid", _kennung()));
    }

    /// <summary>log_trainer: eine geprüfte Aufgabe des Rechen-Trainers (Art, Stufe, alles richtig).</summary>
    public bool TrainerSpeichern(string art, int stufe, bool richtig)
    {
        var (zeit, abstand) = Stempel();
        return Lernstand.Schreiben(
            "INSERT INTO trainer_aufgaben (timestamp, utc_offset, art, stufe, correct, uid)"
            + " VALUES ($zeit, $abstand, $art, $stufe, $richtig, $uid)",
            ("$zeit", zeit), ("$abstand", abstand), ("$art", art), ("$stufe", stufe), ("$richtig", richtig ? 1 : 0),
            ("$uid", _kennung()));
    }

    /// <summary>
    /// trainer_stats: je Art (geloest, richtig) ueber alle Trainer-Aufgaben; bei einem Lesefehler
    /// leer (Python default=[]). Nur lesend, auch wenn nichts gespeichert wird. Richtig bleibt wie
    /// SQLite SUM es liefert (ganze Zahl oder bei fremden Daten Kommazahl, NULL = 0); abgeschnitten
    /// wird erst die Summe ueber alle Arten (show_stats "%d").
    /// </summary>
    public IReadOnlyDictionary<string, (long Geloest, double Richtig)> TrainerStatistik()
    {
        var statistik = new Dictionary<string, (long, double)>(StringComparer.Ordinal);
        foreach (var zeile in Lernstand.Abfragen("SELECT art, COUNT(*), SUM(correct) FROM trainer_aufgaben GROUP BY art"))
        {
            statistik[Convert.ToString(zeile[0], CultureInfo.InvariantCulture) ?? ""] =
                (Convert.ToInt64(zeile[1] ?? 0L, CultureInfo.InvariantCulture), Convert.ToDouble(zeile[2] ?? 0L, CultureInfo.InvariantCulture));
        }
        return statistik;
    }
}
