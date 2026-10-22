using System.Globalization;
using System.Text;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Speichert Antworten wie fisi_core.DBManager (log_card, log_quiz_answer, save_test_result,
/// log_trainer; Phase 3 R5 dazu trainer_stats; Phase 4 PB1 log_ap1, log_scenario, log_project,
/// save_exam, save_project_field, project_fields):
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

    /// <summary>log_ap1: AP1-Szenario (Position in szenarien_ap1.json); richtig null = angesehen, nicht bewertet.</summary>
    public bool Ap1Speichern(int position, string titel, string thema, bool? richtig) =>
        SzenarioZeile("ap1_events", "scenario_index", "theme", position, titel, thema, richtig);

    /// <summary>log_scenario: AP2-Szenario (Position in szenarien_ap2.json); richtig null = angesehen.</summary>
    public bool SzenarioSpeichern(int position, string titel, string thema, bool? richtig) =>
        SzenarioZeile("scenario_events", "scenario_index", "theme", position, titel, thema, richtig);

    /// <summary>log_project: Testprojekt (Position in projektarbeiten.json, Fachbereich); richtig null = angesehen.</summary>
    public bool ProjektSpeichern(int position, string titel, string fachbereich, bool? richtig) =>
        SzenarioZeile("project_events", "project_index", "category", position, titel, fachbereich, richtig);

    // Gemeinsamer Weg von log_ap1, log_scenario und log_project (Tabellen- und Spaltennamen fest, nie vom Nutzer).
    private bool SzenarioZeile(string tabelle, string spalteIndex, string spalteThema, int position, string titel, string thema, bool? richtig)
    {
        var (zeit, abstand) = Stempel();
        return Lernstand.Schreiben(
            $"INSERT INTO {tabelle} (timestamp, utc_offset, {spalteIndex}, title, {spalteThema}, correct, uid)"
            + " VALUES ($zeit, $abstand, $position, $titel, $thema, $richtig, $uid)",
            ("$zeit", zeit), ("$abstand", abstand), ("$position", position), ("$titel", titel), ("$thema", thema),
            ("$richtig", richtig is null ? null : richtig.Value ? 1 : 0), ("$uid", _kennung()));
    }

    /// <summary>
    /// save_exam: Ergebnis einer Klausursimulation. daten ist der fertige JSON-Text wie Pythons
    /// json.dumps(data, ensure_ascii=False) (den baut die Klausur in W2); punkte als Kommazahl
    /// (float(points)), dauer in ganzen Sekunden (int(duration)).
    /// </summary>
    public bool PruefungSpeichern(string art, double punkte, string note, int dauerSekunden, string daten)
    {
        var (zeit, abstand) = Stempel();
        return Lernstand.Schreiben(
            "INSERT INTO pruefungen (timestamp, utc_offset, art, punkte, note, dauer, daten, uid)"
            + " VALUES ($zeit, $abstand, $art, $punkte, $note, $dauer, $daten, $uid)",
            ("$zeit", zeit), ("$abstand", abstand), ("$art", art), ("$punkte", punkte), ("$note", note),
            ("$dauer", dauerSekunden), ("$daten", daten), ("$uid", _kennung()));
    }

    /// <summary>
    /// finish (E6, bewusste Abweichung von Python): Antwortzeilen und Ergebniszeile in EINER
    /// Buchung. kennung ist die uid der Ergebniszeile, erzeugt beim Start der Pruefung und in der
    /// laufenden Pruefung abgelegt. Steht diese uid schon in pruefungen, schreibt der Weg nichts,
    /// auch keine Antworten (jede Zeile prueft das selbst; die Ergebniszeile kommt zuletzt).
    /// Ein Stempel fuer alle Zeilen. Rueckgabe true: das Ergebnis liegt jetzt vor (neu oder schon
    /// vorher); false: nichts gespeichert (Lesefehler, Schreibschutz, Fehler), alles oder nichts.
    /// </summary>
    public bool PruefungAbschliessen(string kennung, string art, double punkte, string note, int dauerSekunden,
        string daten, IReadOnlyList<Abschlusszeile> antworten)
    {
        const string Neu = " WHERE NOT EXISTS (SELECT 1 FROM pruefungen WHERE uid = $kennung)";
        var (zeit, abstand) = Stempel();
        var sql = new StringBuilder();
        var parameter = new List<(string Name, object? Wert)> { ("$zeit", zeit), ("$abstand", abstand), ("$kennung", kennung) };
        for (var i = 0; i < antworten.Count; i++)
        {
            var n = i.ToString(CultureInfo.InvariantCulture);
            switch (antworten[i])
            {
                case QuizAntwort quiz:
                    sql.Append("INSERT INTO quiz_answers (timestamp, utc_offset, category, question, correct, uid)"
                        + $" SELECT $zeit, $abstand, $a{n}, $b{n}, $r{n}, $u{n}" + Neu + "; ");
                    parameter.AddRange([($"$a{n}", quiz.Fachbereich), ($"$b{n}", quiz.Frage)]);
                    parameter.Add(($"$r{n}", quiz.Richtig ? 1 : 0));
                    break;
                case AufgabeAntwort aufgabe:
                    sql.Append($"INSERT INTO {(aufgabe.Ap1 ? "ap1_events" : "scenario_events")}"
                        + " (timestamp, utc_offset, scenario_index, title, theme, correct, uid)"
                        + $" SELECT $zeit, $abstand, $p{n}, $a{n}, $b{n}, $r{n}, $u{n}" + Neu + "; ");
                    parameter.AddRange([($"$p{n}", aufgabe.Position), ($"$a{n}", aufgabe.Titel), ($"$b{n}", aufgabe.Thema)]);
                    parameter.Add(($"$r{n}", aufgabe.Gewusst ? 1 : 0));
                    break;
                default:
                    throw new ArgumentException("Unbekannte Antwortzeile", nameof(antworten));
            }
            parameter.Add(($"$u{n}", _kennung()));
        }
        sql.Append("INSERT INTO pruefungen (timestamp, utc_offset, art, punkte, note, dauer, daten, uid)"
            + " SELECT $zeit, $abstand, $art, $punkte, $note, $dauer, $daten, $kennung" + Neu);
        parameter.AddRange([("$art", art), ("$punkte", punkte), ("$note", note), ("$dauer", dauerSekunden), ("$daten", daten)]);
        return Lernstand.Schreiben(sql.ToString(), [.. parameter]);
    }

    /// <summary>
    /// purge_superseded_project_rows (Python v0.63.2 woertlich, dieselben zwei DELETE wie in
    /// Uebernahme.Zusammenfuehren): erst spaetere nicht leere Fassungen in geloeschten Projekten,
    /// dann jede Fassung, die eine neuere desselben Feldes hat (gleiche Zeit: groessere uid gilt).
    /// </summary>
    public const string ProjektAufraeumen =
        "DELETE FROM abschlussprojekt WHERE feld != '_geloescht' AND wert != ''"
        + " AND EXISTS ("
        + " SELECT 1 FROM abschlussprojekt AS loesch"
        + " WHERE loesch.projekt = abschlussprojekt.projekt"
        + " AND loesch.feld = '_geloescht' AND loesch.wert = '1'"
        + " AND loesch.timestamp < abschlussprojekt.timestamp);"
        + " DELETE FROM abschlussprojekt WHERE EXISTS ("
        + " SELECT 1 FROM abschlussprojekt AS neu"
        + " WHERE neu.projekt = abschlussprojekt.projekt"
        + " AND neu.feld = abschlussprojekt.feld"
        + " AND (neu.timestamp > abschlussprojekt.timestamp"
        + "  OR (neu.timestamp = abschlussprojekt.timestamp"
        + "      AND neu.uid > abschlussprojekt.uid)))";

    /// <summary>
    /// save_project_field: anders als alle anderen Wege. Zeit wie _now_fine (Ortszeit mit
    /// Mikrosekunden, ohne utc_offset, ohne Uhrabweichung), danach in derselben Buchung
    /// <see cref="ProjektAufraeumen"/>. Wird nichts gespeichert (Lesefehler, Schreibschutz),
    /// lehnt Lernstand still ab wie bei jedem anderen Weg (Python: query_only laesst das INSERT
    /// scheitern, ohne refused_writes zu zaehlen; sichtbar ist fuer den Nutzer beides nicht).
    /// </summary>
    public bool ProjektFeldSpeichern(string projekt, string feld, string wert)
    {
        var zeit = _uhr.Jetzt().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
        return Lernstand.Schreiben(
            "INSERT INTO abschlussprojekt (timestamp, projekt, feld, wert, uid)"
            + " VALUES ($zeit, $projekt, $feld, $wert, $uid); " + ProjektAufraeumen,
            ("$zeit", zeit), ("$projekt", projekt), ("$feld", feld), ("$wert", wert), ("$uid", _kennung()));
    }

    /// <summary>
    /// project_fields: {projekt: {feld: wert}}, je Feld die neueste Fassung (ORDER BY timestamp, uid;
    /// die letzte Zeile gewinnt). Bei einem Lesefehler leer (Python default=[]). Reihenfolge wie
    /// Python-dict: Projekte und Felder in der Reihenfolge ihres ersten Auftretens.
    /// </summary>
    public OrderedDictionary<string, OrderedDictionary<string, string>> ProjektFelder()
    {
        var projekte = new OrderedDictionary<string, OrderedDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var zeile in Lernstand.Abfragen("SELECT projekt, feld, wert FROM abschlussprojekt ORDER BY timestamp, uid"))
        {
            var projekt = Convert.ToString(zeile[0], CultureInfo.InvariantCulture) ?? "";
            if (!projekte.TryGetValue(projekt, out var felder))
            {
                projekte[projekt] = felder = new OrderedDictionary<string, string>(StringComparer.Ordinal);
            }
            felder[Convert.ToString(zeile[1], CultureInfo.InvariantCulture) ?? ""] =
                Convert.ToString(zeile[2], CultureInfo.InvariantCulture) ?? "";
        }
        return projekte;
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

/// <summary>Eine Antwortzeile des Pruefungsabschlusses (finish).</summary>
public abstract record Abschlusszeile;

/// <summary>WiSo: log_quiz_answer(category, question, ok) je bekannter Frage.</summary>
public sealed record QuizAntwort(string Fachbereich, string Frage, bool Richtig) : Abschlusszeile;

/// <summary>Offene Aufgabe: log_ap1 (quelle "ap1") oder log_scenario; Gewusst = punkte * 2 >= max.</summary>
public sealed record AufgabeAntwort(bool Ap1, int Position, string Titel, string Thema, bool Gewusst) : Abschlusszeile;
