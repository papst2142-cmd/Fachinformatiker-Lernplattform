using System.Globalization;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Phase 7, W8 (Weisung P7-5, 2c; Plan Phase 7 Abschnitt 5 R3): das Lernspiel schreibt wie
/// fisi_core.DBManager (Python 0.63.2: log_game_event, log_slot, delete_run mit
/// purge_deleted_runs, log_record, reset_records). Nur angehaengte Zeilen in spiel_ereignisse,
/// spiel_plaetze und spiel_bestenliste, nie Lerntabellen; geloescht wird nur, was Python auch
/// loescht (Ereignisse geloeschter Durchgaenge, die Bestenliste mit Marke). Jeder Weg ist eine
/// Buchung ueber <see cref="Lernstand.Schreiben"/>. Der Aufrufer haelt dabei die Sperre aus
/// <see cref="Vorgaenge.Versuchen"/> (Core/Spiel/SpielDatenbank), damit nichts neben Abgleich,
/// Uebernahme, Update oder Sicherung einspielen geschrieben wird.
/// </summary>
public sealed partial class LernstandSchreiber
{
    /// <summary>LEGACY_RUN: der Durchgang von vor 0.48 (Spalte lauf leer).</summary>
    public const string AlterLauf = "alt";

    /// <summary>_now_fine: Ortszeit mit Mikrosekunden (ohne utc_offset, ohne Uhrabweichung).</summary>
    private string FeineZeit() => _uhr.Jetzt().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture);

    /// <summary>
    /// log_game_event: ein Ereignis des Lernspiels (daten ist JSON-Text). lauf leer oder "alt" =
    /// der alte Durchgang (Spalte lauf NULL). Rueckgabe: der Zeitstempel der Zeile, null = nichts
    /// gespeichert.
    /// </summary>
    public string? SpielEreignisSpeichern(string art, string daten, string geraet, string? lauf)
    {
        var (zeit, abstand) = Stempel();
        return Lernstand.Schreiben(
            "INSERT INTO spiel_ereignisse (timestamp, utc_offset, typ, daten, geraet, uid, lauf)"
            + " VALUES ($zeit, $abstand, $art, $daten, $geraet, $uid, $lauf)",
            ("$zeit", zeit), ("$abstand", abstand), ("$art", art), ("$daten", daten), ("$geraet", geraet ?? ""),
            ("$uid", _kennung()), ("$lauf", string.IsNullOrEmpty(lauf) || lauf == AlterLauf ? null : lauf))
            ? zeit : null;
    }

    /// <summary>log_slot: eine Zeile der Platz-Tabelle (angelegt, umbenannt, geloescht).</summary>
    public bool SpielPlatzSpeichern(long platz, string lauf, string aktion, string daten, string geraet) =>
        Lernstand.Schreiben(
            "INSERT INTO spiel_plaetze (timestamp, platz, lauf, aktion, daten, geraet, uid)"
            + " VALUES ($zeit, $platz, $lauf, $aktion, $daten, $geraet, $uid)",
            ("$zeit", FeineZeit()), ("$platz", platz), ("$lauf", lauf), ("$aktion", aktion),
            ("$daten", string.IsNullOrEmpty(daten) ? "{}" : daten), ("$geraet", geraet ?? ""), ("$uid", _kennung()));

    /// <summary>
    /// set_meta(ACTIVE_RUN_KEY, lauf) (Weisung P7-6, Sammelliste 94): der zuletzt auf diesem Geraet
    /// gewaehlte Durchgang ("" = keiner) in sync_meta "spiel_aktiver_lauf" (Spielstaende.AktiverLaufSchluessel).
    /// </summary>
    public bool SpielAktivMerken(string lauf) =>
        Lernstand.Schreiben("INSERT OR REPLACE INTO sync_meta (key, value) VALUES ($k, $v)",
            ("$k", "spiel_aktiver_lauf"), ("$v", lauf ?? ""));

    /// <summary>
    /// purge_deleted_runs: entfernt alle Ereignisse geloeschter Durchgaenge (der alte Durchgang
    /// sind die Ereignisse ohne lauf). Dieselbe Rechnung wie in Uebernahme.Zusammenfuehren.
    /// </summary>
    public const string GeloeschteLaeufeAufraeumen =
        "DELETE FROM spiel_ereignisse WHERE (lauf IS NULL OR lauf = '')"
        + " AND EXISTS (SELECT 1 FROM spiel_plaetze WHERE aktion = 'geloescht' AND lauf = '" + AlterLauf + "');"
        + " DELETE FROM spiel_ereignisse WHERE lauf IN"
        + " (SELECT lauf FROM spiel_plaetze WHERE aktion = 'geloescht' AND lauf != '" + AlterLauf + "')";

    /// <summary>
    /// delete_run: einen Durchgang endgueltig loeschen (auch auf den anderen Geraeten): die Zeile
    /// "geloescht" bleibt stehen, und alle Ereignisse geloeschter Durchgaenge gehen in derselben Buchung.
    /// </summary>
    public bool SpielLaufLoeschen(long platz, string lauf, string geraet) =>
        Lernstand.Schreiben(
            "INSERT INTO spiel_plaetze (timestamp, platz, lauf, aktion, daten, geraet, uid)"
            + " VALUES ($zeit, $platz, $lauf, 'geloescht', '{}', $geraet, $uid); " + GeloeschteLaeufeAufraeumen,
            ("$zeit", FeineZeit()), ("$platz", platz), ("$lauf", lauf), ("$geraet", geraet ?? ""), ("$uid", _kennung()));

    /// <summary>
    /// log_record: eine Zeile der Bestenliste (daten ist JSON-Text). Liegt der Eintrag nicht nach der
    /// Marke von "Bestenliste loeschen" (als Text, mit Zone in UTC), bekommt er die Marke + 1 s in
    /// deren Zone (Python 0.63.2 B-G-17, FG-100), sonst entfernte der naechste Abgleich ihn wieder.
    /// </summary>
    public bool BestenSpeichern(string? lauf, string art, string schluessel, object? wert, string daten)
    {
        var (zeit, abstand) = Stempel();
        var gelesen = Lernstand.Abfragen("SELECT value FROM sync_meta WHERE key = $k", ("$k", "bestenliste_reset_at")) is [var z, ..]
            ? z[0] : null;
        if (Loeschmarken.Lesen(gelesen) is { } marke)
        {
            var ort = Loeschmarken.Ortszeit(marke.Text[..19])!.Value;
            var eintragUtc = Loeschmarken.Ortszeit(zeit)!.Value.AddMinutes(-abstand);
            if (Loeschmarken.Vergleich(zeit, marke.Text) <= 0 || (marke.Utc is { } utc && eintragUtc <= utc))
            {
                zeit = ort.AddSeconds(1).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                if (marke.Utc is { } markeUtc)
                {
                    abstand = (int)Mathe.PythonRunden.Runde((ort - markeUtc).TotalSeconds / 60);
                }
            }
        }
        return Lernstand.Schreiben(
            "INSERT INTO spiel_bestenliste (timestamp, utc_offset, durchgang, art, schluessel, wert, daten, uid)"
            + " VALUES ($zeit, $abstand, $lauf, $art, $schluessel, $wert, $daten, $uid)",
            ("$zeit", zeit), ("$abstand", abstand), ("$lauf", lauf ?? ""), ("$art", art), ("$schluessel", schluessel),
            ("$wert", wert), ("$daten", string.IsNullOrEmpty(daten) ? "{}" : daten), ("$uid", _kennung()));
    }

    /// <summary>reset_records: die Bestenliste loeschen, Marke bestenliste_reset_at (eine Buchung).</summary>
    public bool BestenlisteLoeschen() => Loeschen(["spiel_bestenliste"], "bestenliste_reset_at");
}
