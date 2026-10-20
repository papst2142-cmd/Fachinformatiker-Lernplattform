namespace Lernplattform.Core.Daten;

/// <summary>
/// Speichert Antworten wie fisi_core.DBManager (log_card, log_quiz_answer, save_test_result):
/// Zeitstempel in Ortszeit, utc_offset aus demselben Augenblick, uid = uuid4 als Hex.
/// Jeder Vorgang ist eine Transaktion (Lernstand.Schreiben): die Zeile steht ganz oder gar nicht.
/// Wird nichts gespeichert (Lesefehler, Schreibschutz), lehnt Lernstand still ab.
/// Die von Python nach einem Abgleich eingerechnete Uhrabweichung (sync_meta "uhr_abweichung")
/// kommt mit dem Abgleich (L10): Nur der Abgleich misst und schreibt sie, die Uebernahme
/// bringt sie nicht mit.
/// </summary>
public sealed class LernstandSchreiber(Lernstand lernstand, Uhr? uhr = null, Func<string>? kennung = null)
{
    private readonly Uhr _uhr = uhr ?? Uhr.System;
    private readonly Func<string> _kennung = kennung ?? NeueKennung;

    public Lernstand Lernstand { get; } = lernstand;

    /// <summary>_uid: uuid4().hex (32 Kleinbuchstaben/Ziffern).</summary>
    public static string NeueKennung() => Guid.NewGuid().ToString("N");

    /// <summary>_stamp: (Ortszeit als Text, Abstand zu UTC in Minuten) aus demselben Augenblick.</summary>
    public (string Zeit, int Abstand) Stempel()
    {
        var jetzt = _uhr.Jetzt();
        return (Uhr.Text(jetzt), Uhr.AbstandMinuten(jetzt));
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
}
