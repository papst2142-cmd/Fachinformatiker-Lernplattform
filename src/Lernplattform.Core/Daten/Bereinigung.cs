using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Update;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Phase 11 FW5b (Sammelliste 186, Nico 02.11.2026 Antwort b): bis A 0.32.0 hat der Abschluss einer
/// AE-, DPA- oder DV-Klausur die Aufgaben als scenario_events mit der Stelle in der Liste der Fachrichtung
/// gebucht (Abschluss.cs, Sammelliste 172). Der Lernstand der Szenarien liest diese Zahl als Stelle in der
/// ganzen Liste, die Zeile traf also ein SI-Szenario. Diese Zeilen werden hier berichtigt: die Zahl wird auf
/// die richtige Stelle gesetzt (Nummer - 1, wie Abschluss seit A 0.32.0 bucht). Titel, Thema, Zeit, Ergebnis
/// und Kennung (uid) bleiben; so bringt ein Abgleich die alte Fassung nicht zurueck (gleiche uid wird nicht
/// noch einmal eingefuegt).
/// <para>
/// Nur eindeutig belegbare Zeilen: zu einer Klausurzeile in pruefungen, deren Daten eine Aufgabe mit Quelle
/// ap2_ae, ap2_dpa oder ap2_dv nennen, gehoert eine Zeile in scenario_events mit genau derselben Zeit und
/// demselben Abstand zu UTC (eine Buchung), der falschen Stelle (Index der Aufgabe) sowie Titel und Thema
/// genau dieses Szenarios. Traegt das SI-Szenario an der falschen Stelle denselben Titel, bleibt die Zeile
/// (im Zweifel nichts aendern). Andere Zeilen werden nie angefasst.
/// </para>
/// <para>
/// Ablauf: unter <see cref="Vorgaenge"/>; erst eine Kopie sicherungen/vor_bereinigung_*.db (wie vor_update_*,
/// nie von allein geloescht), ohne Kopie keine Bereinigung; dann alle Zeilen in einer Buchung. Gesucht wird
/// bei jedem Start (eine kurze Abfrage); geaendert und kopiert nur, wenn es solche Zeilen gibt. Auf einem
/// Geraet also einmal nach dem Update, dazu wieder, falls ein Abgleich mit einem Geraet vor A 0.32.0 oder
/// eine alte Sicherung solche Zeilen zurueckbringt.
/// </para>
/// </summary>
public static class Bereinigung
{
    public const string KopiePraefix = "vor_bereinigung_";

    /// <summary>Eine falsch gebuchte Zeile: rowid in scenario_events, falsche und richtige Stelle.</summary>
    public sealed record Fund(long Zeile, long Alt, long Neu, string Titel);

    /// <summary>Ausgang: Zahl der berichtigten Zeilen und die Kopie (null: nichts zu tun oder nicht geschafft).</summary>
    public sealed record Ergebnis(int Gefunden, int Berichtigt, string? Kopie, string Protokoll);

    /// <summary>Sucht die falsch gebuchten Zeilen (nur lesen).</summary>
    public static List<Fund> Suchen(Lernstand lernstand, Lerninhalte inhalte)
    {
        var funde = new List<Fund>();
        var gesehen = new HashSet<long>();
        var si = inhalte.Alle.Ap2SzenarienFuer(Fachrichtungen.Systemintegration);
        var pruefungen = lernstand.Abfragen(
            "SELECT timestamp, utc_offset, daten FROM pruefungen WHERE daten LIKE '%ap2!_%' ESCAPE '!' ORDER BY rowid");
        foreach (var pruefung in pruefungen)
        {
            if (pruefung[2] is not string daten || Aufgaben(daten) is not { } aufgaben)
            {
                continue;
            }
            foreach (var aufgabe in aufgaben.OfType<JsonObject>())
            {
                if (Text(aufgabe["quelle"]) is not { } quelle || Klausur.QuelleFachrichtung(quelle) is not { } fr
                    || Ganzzahl(aufgabe["index"]) is not { } index)
                {
                    continue;
                }
                var liste = inhalte.Alle.Ap2SzenarienFuer(fr);
                if (index < 0 || index >= liste.Count)
                {
                    continue;
                }
                var szenario = liste[index];
                var titel = szenario.Titel ?? "";
                var neu = szenario.Nummer - 1;
                // im Zweifel nichts aendern: dieselbe Stelle oder das getroffene SI-Szenario heisst genauso
                if (neu == index || (index < si.Count && (si[index].Titel ?? "") == titel))
                {
                    continue;
                }
                var zeilen = lernstand.Abfragen(
                    "SELECT rowid FROM scenario_events WHERE timestamp IS $zeit AND utc_offset IS $abstand"
                    + " AND scenario_index = $index AND title = $titel AND theme = $thema ORDER BY rowid",
                    ("$zeit", pruefung[0]), ("$abstand", pruefung[1]), ("$index", index), ("$titel", titel),
                    ("$thema", szenario.SzenarioThema ?? ""));
                foreach (var zeile in zeilen)
                {
                    if (zeile[0] is long rowid && gesehen.Add(rowid))
                    {
                        funde.Add(new Fund(rowid, index, neu, titel));
                    }
                }
            }
        }
        return funde;
    }

    /// <summary>
    /// Sucht und berichtigt (Kopie vorher). ordner: Datenordner (Vorgabe: neben der Datenbank). Schreibt eine Zeile
    /// ins Update-Protokoll, wenn etwas gefunden wurde.
    /// </summary>
    public static Ergebnis Ausfuehren(Lernstand lernstand, Lerninhalte inhalte, Uhr? uhr = null, string? ordner = null)
    {
        ordner ??= Path.GetDirectoryName(lernstand.Pfad)!;
        using var sperre = Vorgaenge.Sperren();
        Spielschlange.AlleLeeren();
        if (lernstand.NurLesenGrund is not null)
        {
            return new Ergebnis(0, 0, null, "");
        }
        var funde = Suchen(lernstand, inhalte);
        if (funde.Count == 0)
        {
            return new Ergebnis(0, 0, null, "");
        }
        string? kopie;
        try
        {
            kopie = UpdateAblauf.KopieAnlegen(lernstand.Pfad, Path.Combine(ordner, Uebernahme.SicherungsOrdner), uhr ?? Uhr.System, KopiePraefix);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            kopie = null;
        }
        if (kopie is null)
        {
            var text = Texte.KopieFehlt(funde.Count);
            Updateprotokoll.Schreiben(text, ordner);
            return new Ergebnis(funde.Count, 0, null, text);
        }
        var sql = new StringBuilder();
        var parameter = new List<(string, object?)>();
        for (var i = 0; i < funde.Count; i++)
        {
            var n = i.ToString(CultureInfo.InvariantCulture);
            sql.Append($"UPDATE scenario_events SET scenario_index = $neu{n} WHERE rowid = $zeile{n} AND scenario_index = $alt{n}; ");
            parameter.AddRange([($"$neu{n}", funde[i].Neu), ($"$zeile{n}", funde[i].Zeile), ($"$alt{n}", funde[i].Alt)]);
        }
        var name = Uebernahme.SicherungsOrdner + "/" + Path.GetFileName(kopie);
        var gespeichert = lernstand.Schreiben(sql.ToString(), [.. parameter]);
        var protokoll = gespeichert ? Texte.Berichtigt(funde.Count, name) : Texte.NichtGespeichert(funde.Count, name);
        Updateprotokoll.Schreiben(protokoll, ordner);
        return new Ergebnis(funde.Count, gespeichert ? funde.Count : 0, kopie, protokoll);
    }

    /// <summary>Zeilen fuers Update-Protokoll (Datei update.log im Datenordner).</summary>
    public static class Texte
    {
        public static string Berichtigt(int anzahl, string kopie) =>
            $"Lernstand bereinigt: {anzahl} alte Einträge aus Klausuren von AE, DPA oder DV zeigten auf ein Szenario der "
            + $"Systemintegration und zeigen jetzt auf das richtige Szenario. Kopie vorher: {kopie}";

        public static string NichtGespeichert(int anzahl, string kopie) =>
            $"Lernstand nicht bereinigt: {anzahl} alte Einträge aus Klausuren von AE, DPA oder DV konnten nicht gespeichert "
            + $"werden, der Lernstand ist unverändert. Kopie: {kopie}";

        public static string KopieFehlt(int anzahl) =>
            $"Lernstand nicht bereinigt: {anzahl} alte Einträge aus Klausuren von AE, DPA oder DV gefunden, aber die Kopie "
            + "vorher ist gescheitert. Ohne Kopie wird nichts geändert.";
    }

    private static JsonArray? Aufgaben(string daten)
    {
        try
        {
            return JsonNode.Parse(daten) is JsonObject objekt ? objekt["aufgaben"] as JsonArray : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonNode? wert) =>
        wert is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;

    private static int? Ganzzahl(JsonNode? wert) =>
        wert is JsonValue v && v.TryGetValue<long>(out var zahl) && zahl is >= 0 and <= int.MaxValue ? (int)zahl : null;
}
