using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Update;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Welle A 1.18.0 (L0, Weisung P12-424, Nico 2b; Plan P12-269 Abschnitt 3): <c>inhalte/umbenennungen.json</c> ist weg, die
/// Zuordnungen alt -&gt; neu wirken nicht mehr bei Uebernahme, Abgleich oder in Pruefungen. Stattdessen schreibt dieser Schritt
/// einen Lernstand mit alten Fragetexten EINMAL um: dieselben 55 Zuordnungen in derselben Reihenfolge wie bisher, dazu die
/// FIAE-Quizfrage „Fabrikmethode“ (alte Fassung geloescht, neue mit anderem Text), Datei <c>alte_fragetexte.json</c> (eingebettet).
/// <para>
/// Umgeschrieben wird genau wie bisher bei jeder Uebernahme (apply_question_renames): <c>UPDATE card_events/quiz_answers SET
/// question = neu WHERE question = alt</c>, Zeit und uid bleiben, keine Zeile kommt dazu oder geht weg. Dazu in gespeicherten
/// Pruefungen (<c>pruefungen.daten</c>) jeder JSON-Text, der genau einem alten Fragetext gleicht, so wie die Klausur ihn bisher
/// beim Anzeigen und Zaehlen umgerechnet hat (<c>KlausurInhalte.AktuelleFrage</c>); eine Zeile, die danach kein gueltiges JSON
/// mehr waere, bleibt unveraendert.
/// </para>
/// <para>
/// Ablauf wie <see cref="Bereinigung"/>: unter <see cref="Vorgaenge"/>; gesucht wird bei jedem Start (eine kurze Abfrage),
/// geaendert nur, wenn es alte Texte gibt; erst eine Kopie <c>sicherungen/vor_umschreiben_*.db</c> (nie von allein geloescht),
/// ohne Kopie nichts; dann alles in einer Buchung und eine Zeile ins Update-Protokoll. Kein Merker in sync_meta (der Abgleich
/// zaehlt jeden Schluessel dort zum Stand): ein zweiter Start findet nichts mehr und aendert nichts.
/// </para>
/// </summary>
public static class AlteFragetexte
{
    public const string KopiePraefix = "vor_umschreiben_";

    /// <summary>Name der eingebetteten Datei.</summary>
    public const string Datei = "alte_fragetexte.json";

    private static readonly Lazy<IReadOnlyList<(string Tabelle, string Alt, string Neu)>> _zuordnungen = new(Laden);

    /// <summary>(Tabelle, alter Text, neuer Text) in der Reihenfolge der Datei: erst Karten (card_events), dann Quiz (quiz_answers).</summary>
    public static IReadOnlyList<(string Tabelle, string Alt, string Neu)> Zuordnungen => _zuordnungen.Value;

    /// <summary>Was beim Start umzuschreiben ist (nur lesen).</summary>
    public sealed record Fund(int Karten, int Fragen, IReadOnlyList<(long Zeile, string Daten)> Pruefungen)
    {
        public int Zeilen => Karten + Fragen + Pruefungen.Count;
    }

    /// <summary>Ausgang: Zahl der umgeschriebenen Zeilen und die Kopie (null: nichts zu tun oder nicht geschafft).</summary>
    public sealed record Ergebnis(int Gefunden, int Umgeschrieben, string? Kopie, string Protokoll);

    /// <summary>Sucht Zeilen mit alten Fragetexten (nur lesen).</summary>
    public static Fund Suchen(Lernstand lernstand)
    {
        int Zaehlen(string tabelle)
        {
            var alte = Zuordnungen.Where(z => z.Tabelle == tabelle).Select(z => z.Alt).ToList();
            var namen = alte.Select((_, i) => "$a" + i.ToString(CultureInfo.InvariantCulture)).ToList();
            var zeilen = lernstand.Abfragen($"SELECT COUNT(*) FROM {tabelle} WHERE question IN ({string.Join(", ", namen)})",
                [.. alte.Select((a, i) => (namen[i], (object?)a))]);
            return zeilen is [var z, ..] && z[0] is long n ? (int)n : 0;
        }

        var pruefungen = new List<(long, string)>();
        foreach (var zeile in lernstand.Abfragen("SELECT rowid, daten FROM pruefungen ORDER BY rowid"))
        {
            if (zeile[0] is long rowid && zeile[1] is string daten && DatenUmschreiben(daten) is { } neu)
            {
                pruefungen.Add((rowid, neu));
            }
        }
        return new Fund(Zaehlen("card_events"), Zaehlen("quiz_answers"), pruefungen);
    }

    /// <summary>
    /// Text einer gespeicherten Pruefung mit heutigen Fragetexten oder null (kein alter Text darin, oder das Ergebnis waere kein
    /// gueltiges JSON). Ersetzt wird jeder JSON-Text "alt" (so geschrieben, wie Python json.dumps(..., ensure_ascii=False) ihn
    /// schreibt) durch "neu", in der Reihenfolge der Zuordnungen.
    /// </summary>
    internal static string? DatenUmschreiben(string daten)
    {
        var neu = daten;
        // wie KlausurInhalte.AktuelleFrage bisher: nur die Quizfragen (eine Klausur enthaelt keine Karteikarten)
        foreach (var (_, alt, ersatz) in Zuordnungen.Where(z => z.Tabelle == "quiz_answers"))
        {
            neu = Ersetzen(neu, PythonJson.Text(alt), PythonJson.Text(ersatz));
        }
        if (neu == daten)
        {
            return null;
        }
        try
        {
            JsonNode.Parse(neu);
            return neu;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Ersetzt jeden ganzen JSON-Text token (nicht hinter einem Rueckstrich, also nie mitten in einem anderen Text).</summary>
    private static string Ersetzen(string text, string token, string ersatz)
    {
        var teile = new StringBuilder();
        var ab = 0;
        for (var stelle = text.IndexOf(token, StringComparison.Ordinal); stelle >= 0; stelle = text.IndexOf(token, stelle + 1, StringComparison.Ordinal))
        {
            if (stelle < ab || (stelle > 0 && text[stelle - 1] == '\\'))
            {
                continue;
            }
            teile.Append(text, ab, stelle - ab).Append(ersatz);
            ab = stelle + token.Length;
        }
        return ab == 0 ? text : teile.Append(text, ab, text.Length - ab).ToString();
    }

    /// <summary>
    /// Sucht und schreibt um (Kopie vorher). ordner: Datenordner (Vorgabe: neben der Datenbank). Schreibt eine Zeile ins
    /// Update-Protokoll, wenn etwas gefunden wurde.
    /// </summary>
    public static Ergebnis Ausfuehren(Lernstand lernstand, Uhr? uhr = null, string? ordner = null)
    {
        ordner ??= Path.GetDirectoryName(lernstand.Pfad)!;
        using var sperre = Vorgaenge.Sperren();
        Spiel.Spielschlange.AlleLeeren();
        if (lernstand.NurLesenGrund is not null)
        {
            return new Ergebnis(0, 0, null, "");
        }
        var fund = Suchen(lernstand);
        if (fund.Zeilen == 0)
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
            var text = Texte.KopieFehlt(fund.Zeilen);
            Updateprotokoll.Schreiben(text, ordner);
            return new Ergebnis(fund.Zeilen, 0, null, text);
        }
        // apply_question_renames wie bisher in Uebernahme.Zusammenfuehren: je Zuordnung ein UPDATE, Reihenfolge der Datei
        var sql = new StringBuilder();
        var parameter = new List<(string, object?)>();
        for (var i = 0; i < Zuordnungen.Count; i++)
        {
            var (tabelle, alt, neu) = Zuordnungen[i];
            var n = i.ToString(CultureInfo.InvariantCulture);
            sql.Append($"UPDATE {tabelle} SET question = $neu{n} WHERE question = $alt{n}; ");
            parameter.AddRange([($"$neu{n}", neu), ($"$alt{n}", alt)]);
        }
        for (var i = 0; i < fund.Pruefungen.Count; i++)
        {
            var (zeile, daten) = fund.Pruefungen[i];
            var n = i.ToString(CultureInfo.InvariantCulture);
            sql.Append($"UPDATE pruefungen SET daten = $daten{n} WHERE rowid = $zeile{n}; ");
            parameter.AddRange([($"$daten{n}", daten), ($"$zeile{n}", zeile)]);
        }
        var name = Uebernahme.SicherungsOrdner + "/" + Path.GetFileName(kopie);
        var gespeichert = lernstand.Schreiben(sql.ToString(), [.. parameter]);
        var protokoll = gespeichert ? Texte.Umgeschrieben(fund.Zeilen, name) : Texte.NichtGespeichert(fund.Zeilen, name);
        Updateprotokoll.Schreiben(protokoll, ordner);
        return new Ergebnis(fund.Zeilen, gespeichert ? fund.Zeilen : 0, kopie, protokoll);
    }

    /// <summary>
    /// Laufende WiSo-Pruefung in einstellungen.json (Schluessel <see cref="Pruefung.LaufendePruefung.Schluessel"/>): alte
    /// Fragetexte in "fragen" und als Schluessel in "antworten" und "optionen" auf die heutigen, genau wie bisher
    /// migrate_running beim Laden (ein schon vorhandener neuer Schluessel bleibt). Aendert <paramref name="werte"/> an Ort und
    /// Stelle; true: etwas geaendert. Andere Pruefungsarten und fremde Formen bleiben unberuehrt.
    /// </summary>
    public static bool LaufendeUmschreiben(JsonObject werte)
    {
        if (werte[Pruefung.LaufendePruefung.Schluessel] is not JsonObject zustand || Pruefung.Json.Text(zustand["art"]) != Pruefung.Klausur.Wiso
            || zustand["fragen"] is not JsonArray fragen)
        {
            return false;
        }
        var quiz = Zuordnungen.Where(z => z.Tabelle == "quiz_answers").GroupBy(z => z.Alt).ToDictionary(g => g.Key, g => g.First().Neu, StringComparer.Ordinal);
        var geaendert = false;
        for (var i = 0; i < fragen.Count; i++)
        {
            if (Pruefung.Json.Text(fragen[i]) is not { } alt || !quiz.TryGetValue(alt, out var neu))
            {
                continue;
            }
            fragen[i] = neu;
            foreach (var feld in new[] { "antworten", "optionen" })
            {
                if (zustand[feld] is JsonObject werteJeFrage && werteJeFrage.ContainsKey(alt) && !werteJeFrage.ContainsKey(neu))
                {
                    var wert = werteJeFrage[alt]?.DeepClone();
                    werteJeFrage.Remove(alt);
                    werteJeFrage[neu] = wert;
                }
            }
            geaendert = true;
        }
        return geaendert;
    }

    /// <summary>Zeilen fuers Update-Protokoll (Datei update.log im Datenordner).</summary>
    public static class Texte
    {
        public static string Umgeschrieben(int anzahl, string kopie) =>
            $"Lernstand umgeschrieben: {anzahl} Einträge mit alten Fragetexten tragen jetzt den heutigen Text. Kopie vorher: {kopie}";

        public static string NichtGespeichert(int anzahl, string kopie) =>
            $"Lernstand nicht umgeschrieben: {anzahl} Einträge mit alten Fragetexten konnten nicht gespeichert werden, "
            + $"der Lernstand ist unverändert. Kopie: {kopie}";

        public static string KopieFehlt(int anzahl) =>
            $"Lernstand nicht umgeschrieben: {anzahl} Einträge mit alten Fragetexten gefunden, aber die Kopie vorher ist "
            + "gescheitert. Ohne Kopie wird nichts geändert.";
    }

    /// <summary>Liest <see cref="Datei"/> (Aufbau wie frueher inhalte/umbenennungen.json: "karte" und "quiz", je alt -&gt; neu).</summary>
    private static IReadOnlyList<(string Tabelle, string Alt, string Neu)> Laden()
    {
        using var strom = Assembly.GetExecutingAssembly().GetManifestResourceStream("Lernplattform.Core.Daten." + Datei)
            ?? throw new InvalidOperationException(Datei + " fehlt im Programm");
        var daten = JsonNode.Parse(strom)!.AsObject();
        var liste = new List<(string, string, string)>();
        foreach (var (quelle, tabelle) in new[] { ("karte", "card_events"), ("quiz", "quiz_answers") })
        {
            foreach (var (alt, neu) in daten[quelle]!.AsObject())
            {
                liste.Add((tabelle, alt, neu!.GetValue<string>()));
            }
        }
        return liste;
    }
}
