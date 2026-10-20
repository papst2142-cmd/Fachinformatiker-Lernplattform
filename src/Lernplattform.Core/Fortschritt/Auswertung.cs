using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Texte;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Fortschritt;

/// <summary>Kachel oben im Fortschritt: Titel, grosser Wert, Unterzeile.</summary>
public sealed record FortschrittKachel(string Titel, string Wert, string Unter);

/// <summary>Ein Tag in "Richtig und falsch pro Tag": Prozent (null ohne Aufgaben) und Anzahlen.</summary>
public sealed record TagesAnteil(string Beschriftung, int? Richtig, int? Falsch, long AnzahlRichtig, long AnzahlFalsch);

/// <summary>"Aufgaben pro Tag" und "Richtig und falsch pro Tag" fuer einen Zeitraum (7 oder 30 Tage).</summary>
public sealed record Tagesreihe(int Tage, IReadOnlyList<string> Beschriftungen, IReadOnlyList<long> Werte, int? Ziel,
    string? ZielText, string Zusammenfassung, IReadOnlyList<TagesAnteil> Anteile, string AnteileText);

/// <summary>Zeile "Historie der Pruefungssessions".</summary>
public sealed record SitzungZeile(string Datum, string Ergebnis, string Quote, string Note, string Dauer);

/// <summary>Zeile "Pruefungen (Klausursimulation)"; Kurz = Kurzname bekannter Pruefungen, sonst null.</summary>
public sealed record PruefungZeile(string Datum, string Name, string? Kurz, string Punkte, string Note, string Dauer);

/// <summary>Schwache Themen der letzten Pruefung (fp.latest_weak).</summary>
public sealed record Schwaeche(string Titel, IReadOnlyList<string> Themen);

/// <summary>
/// Phase 2, L7: Werte des Fortschritts (app_gui.ProgressView.refresh/_paint_days) genau wie
/// Python v0.63.2 (Vergleichswerte G18), nur lesend aus der Lernstand-Datenbank (gleiche
/// SQL-Texte wie fisi_core.DBManager). Fehlt eine Tabelle, liefert die Abfrage wie in Python
/// ihren Vorgabewert. Abweichung von Python (Weisung P2-8, Sammelliste 22): jede Angabe
/// "% richtig" (Durchschnitt, Bestes Ergebnis, Erfolgsquote, Ergebniskurve) hoechstens 100.
/// </summary>
public sealed class Auswertung
{
    private readonly Dictionary<string, long> _aufgaben;
    private readonly Dictionary<string, (long Richtig, long Falsch)> _anteile;

    private Auswertung(Dictionary<string, long> aufgaben, Dictionary<string, (long, long)> anteile)
    {
        _aufgaben = aufgaben;
        _anteile = anteile;
    }

    public IReadOnlyList<FortschrittKachel> Kacheln { get; private init; } = [];

    /// <summary>Ergebniskurve: Beschriftungen "TT.MM." der letzten 20 Sessions, aelteste zuerst.</summary>
    public IReadOnlyList<string> ErgebnisBeschriftungen { get; private init; } = [];

    /// <summary>Ergebniskurve: % richtig je Session (hoechstens 100, P2-8).</summary>
    public IReadOnlyList<double> ErgebnisWerte { get; private init; } = [];

    public IReadOnlyList<SitzungZeile> Sitzungen { get; private init; } = [];

    public IReadOnlyList<PruefungZeile> Pruefungen { get; private init; } = [];

    public Schwaeche? Schwaeche { get; private init; }

    /// <summary>Werte aus der Datenbank (nur lesend; fehlt die Datei, wie leer).</summary>
    public static Auswertung Lesen(string dbPfad)
    {
        using var db = Leser.Oeffnen(dbPfad);
        return Lesen(db);
    }

    private static Auswertung Lesen(Leser db)
    {
        // get_all_results (neueste zuerst) und get_stats
        var ergebnisse = db.Abfrage("SELECT timestamp, score, total, percentage, note, duration_seconds"
            + " FROM test_results ORDER BY id DESC") ?? [];
        var statistik = db.Abfrage("SELECT COUNT(*), AVG(percentage) FROM test_results");
        long anzahl = 0;
        double schnitt = 0.0;
        if (statistik is { Count: > 0 })
        {
            anzahl = statistik[0][0] is long n ? n : 0;
            schnitt = PyText.Zahl(statistik[0][1]);
        }
        var kacheln = new List<FortschrittKachel>
        {
            new(FortschrittTexte.Sessions, anzahl.ToString(CultureInfo.InvariantCulture), FortschrittTexte.SessionsUnter),
            new(FortschrittTexte.Durchschnitt, Prozent(schnitt), FortschrittTexte.DurchschnittUnter),
        };
        if (ergebnisse.Count > 0)
        {
            var bestes = Kappe(ergebnisse.Max(z => PyText.Zahl(z[3])));
            kacheln.Add(new(FortschrittTexte.Bestes, Prozent(bestes), Trainer.Note(bestes)));
        }
        else
        {
            kacheln.Add(new(FortschrittTexte.Bestes, FortschrittTexte.OhneWert, FortschrittTexte.BestesLeer));
        }

        // result_series: letzte 20, aelteste zuerst
        var kurve = Enumerable.Reverse(ergebnisse).TakeLast(20).ToList();
        var sitzungen = ergebnisse.Select(z => new SitzungZeile(
            PyText.Str(z[0]),
            Beschriftungen.Einsetzen(FortschrittTexte.ErgebnisMuster, (long)PyText.Zahl(z[1]), (long)PyText.Zahl(z[2])),
            Prozent(PyText.Zahl(z[3])),
            z[4] is null ? "" : PyText.Str(z[4]),
            Dauer(PyText.Zahl(z[5])))).ToList();

        // exams (neueste zuerst) und latest_weak
        var pruefungen = new List<PruefungZeile>();
        var erste = (Daten: (JsonNode?)null, Art: (object?)null, Zeit: (object?)null, Da: false);
        foreach (var z in db.Abfrage("SELECT timestamp, art, punkte, note, dauer, daten, uid"
                     + " FROM pruefungen ORDER BY timestamp DESC, id DESC") ?? [])
        {
            var art = PyText.Str(z[1]);
            var bekannt = z[1] is string a && FortschrittTexte.Pruefungen.ContainsKey(a);
            pruefungen.Add(new PruefungZeile(PyText.Str(z[0]),
                bekannt ? FortschrittTexte.Pruefungen[art].Name : art, bekannt ? FortschrittTexte.Pruefungen[art].Kurz : null,
                PythonRunden.PunkteText(PyText.Zahl(z[2])), z[3] is null ? "" : PyText.Str(z[3]), Dauer(PyText.Zahl(z[4]))));
            if (!erste.Da)
            {
                erste = (Json(z[5]), z[1], z[0], true);
            }
        }

        return new Auswertung(AufgabenJeTag(db), AnteileJeTag(db))
        {
            Kacheln = kacheln,
            ErgebnisBeschriftungen = kurve.Select(z =>
            {
                var zeit = PyText.Str(z[0]);
                return PyText.Teil(zeit, 8, 10) + "." + PyText.Teil(zeit, 5, 7) + ".";
            }).ToList(),
            ErgebnisWerte = kurve.Select(z => Kappe(PyText.Zahl(z[3]))).ToList(),
            Sitzungen = sitzungen,
            Pruefungen = pruefungen,
            Schwaeche = erste.Da ? Schwach(erste.Daten, erste.Art, erste.Zeit) : null,
        };
    }

    /// <summary>Weisung P2-8: "% richtig" nie ueber 100 (untere Grenze bleibt wie Python).</summary>
    internal static double Kappe(double prozent) => Math.Min(100.0, prozent);

    /// <summary>"%s %%" % de_number(x, 1), gekappt bei 100 (P2-8).</summary>
    private static string Prozent(double wert) =>
        Beschriftungen.Einsetzen(FortschrittTexte.ProzentMuster, Trainer.DeZahl(Kappe(wert), 1));

    /// <summary>"%02d:%02d min" % (d // 60, d % 60) mit Pythons Ganzzahl-Division.</summary>
    internal static string Dauer(double sekunden)
    {
        var d = (long)Math.Floor(sekunden);
        var minuten = (long)Math.Floor(d / 60.0);
        var rest = d - minuten * 60;
        return Zwei(minuten) + ":" + Zwei(rest) + " min";
    }

    private static string Zwei(long n) => n >= 0 ? n.ToString("00", CultureInfo.InvariantCulture) : n.ToString(CultureInfo.InvariantCulture);

    /// <summary>json.loads(daten or "{}"); ungueltig -> {}.</summary>
    private static JsonNode? Json(object? daten)
    {
        var text = PyText.Wahr(daten) ? PyText.Str(daten) : "{}";
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    /// <summary>fisi_pruefung.latest_weak fuer die juengste Pruefung.</summary>
    private static Schwaeche? Schwach(JsonNode? daten, object? art, object? zeit)
    {
        if (daten is not JsonObject objekt)
        {
            return null;    // .get auf Liste/Zahl: AttributeError -> (None, [])
        }
        var themen = objekt["themen"];
        if (!PyWahr(themen))
        {
            themen = new JsonObject();
        }
        if (themen is not JsonObject liste)
        {
            return null;    // .items() fehlt
        }
        var zeilen = new List<(double Anteil, string Name)>();
        foreach (var (name, wert) in liste)
        {
            if (wert is not JsonArray paar || paar.Count != 2 || !PyZahl(paar[0], out var erreicht) || !PyZahl(paar[1], out var hoechst))
            {
                return null;
            }
            zeilen.Add((hoechst != 0 ? erreicht / hoechst : 0, name));
        }
        var schwach = zeilen.OrderBy(z => z.Anteil).ThenBy(z => z.Name, PyStringVergleich.Instanz)
            .Where(z => z.Anteil < 0.5).Select(z => z.Name).ToList();
        if (schwach.Count == 0)
        {
            return null;
        }
        var kurz = art is string a && FortschrittTexte.Pruefungen.TryGetValue(a, out var p) ? p.Kurz : PyText.Str(art);
        var stempel = zeit is null ? "" : PyText.Str(zeit);
        var tag = PyText.Laenge(stempel) >= 10
            ? PyText.Teil(stempel, 8, 10) + "." + PyText.Teil(stempel, 5, 7) + "." + PyText.Teil(stempel, 0, 4) : "";
        return new Schwaeche(Beschriftungen.Einsetzen(FortschrittTexte.SchwachTitel, kurz, tag.Length > 0 ? ", " + tag : ""), schwach);
    }

    private static bool PyWahr(JsonNode? wert) => wert switch
    {
        null => false,
        JsonObject o => o.Count > 0,
        JsonArray a => a.Count > 0,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<double>(out var d) => d != 0,
        JsonValue v when v.TryGetValue<string>(out var s) => s.Length > 0,
        _ => true,
    };

    private static bool PyZahl(JsonNode? wert, out double zahl)
    {
        zahl = 0;
        if (wert is JsonValue v)
        {
            if (v.TryGetValue<bool>(out var b))
            {
                zahl = b ? 1 : 0;
                return true;
            }
            return v.TryGetValue(out zahl);
        }
        return false;
    }

    // -- Tageswerte -----------------------------------------------------------

    /// <summary>DBManager.activity_days.</summary>
    private static Dictionary<string, long> AufgabenJeTag(Leser db)
    {
        var tage = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var zeile in db.Abfrage(
            "SELECT substr(timestamp, 1, 10) AS tag, COUNT(*) FROM ("
            + "  SELECT timestamp FROM card_events WHERE correct IS NOT NULL"
            + "  UNION ALL SELECT timestamp FROM quiz_answers"
            + "  UNION ALL SELECT timestamp FROM scenario_events WHERE correct IS NOT NULL"
            + "  UNION ALL SELECT timestamp FROM project_events WHERE correct IS NOT NULL"
            + "  UNION ALL SELECT timestamp FROM ap1_events WHERE correct IS NOT NULL"
            + "  UNION ALL SELECT timestamp FROM trainer_aufgaben"
            + ") GROUP BY tag") ?? [])
        {
            if (zeile[0] is string tag)
            {
                tage[tag] = (long)zeile[1]!;
            }
        }
        return tage;
    }

    /// <summary>DBManager.activity_split_days.</summary>
    private static Dictionary<string, (long, long)> AnteileJeTag(Leser db)
    {
        var tage = new Dictionary<string, (long, long)>(StringComparer.Ordinal);
        foreach (var zeile in db.Abfrage(
            "SELECT substr(timestamp, 1, 10) AS tag, SUM(correct = 1), SUM(correct = 0)"
            + " FROM ("
            + "  SELECT timestamp, correct FROM card_events WHERE correct IS NOT NULL"
            + "  UNION ALL SELECT timestamp, correct FROM quiz_answers"
            + "  UNION ALL SELECT timestamp, correct FROM scenario_events WHERE correct IS NOT NULL"
            + "  UNION ALL SELECT timestamp, correct FROM project_events WHERE correct IS NOT NULL"
            + "  UNION ALL SELECT timestamp, correct FROM ap1_events WHERE correct IS NOT NULL"
            + "  UNION ALL SELECT timestamp, correct FROM trainer_aufgaben"
            + ") GROUP BY tag") ?? [])
        {
            if (zeile[0] is string tag)
            {
                tage[tag] = ((long)PyText.Zahl(zeile[1]), (long)PyText.Zahl(zeile[2]));
            }
        }
        return tage;
    }

    /// <summary>daily_series, day_labels, daily_summary, daily_split_series, split_percent, split_summary.</summary>
    /// <param name="ziel">Tagesziel (ziel_anzahl), null wenn ausgeschaltet.</param>
    public Tagesreihe Reihe(int tage, DateOnly heute, int? ziel)
    {
        var start = heute.AddDays(-(tage - 1));
        var tageListe = Enumerable.Range(0, tage).Select(start.AddDays).ToList();
        var werte = tageListe.Select(t => _aufgaben.GetValueOrDefault(Iso(t))).ToList();
        var beschriftungen = tageListe.Select(t => tage <= 7
            ? FortschrittTexte.Wochentage[((int)t.DayOfWeek + 6) % 7]
            : t.ToString("dd.MM", CultureInfo.InvariantCulture)).ToList();
        var summe = werte.Sum();
        var text = DashboardTexte.Plural(tage, "Tag", "Tage") + ": " + DashboardTexte.Plural(summe, "Aufgabe", "Aufgaben");
        if (ziel is { } z && z != 0)
        {
            text += " · Tagesziel an " + werte.Count(w => w >= z).ToString(CultureInfo.InvariantCulture) + " von "
                + DashboardTexte.Plural(tage, "Tag", "Tagen") + " erreicht";
        }
        var anteile = tageListe.Select((t, i) =>
        {
            var (richtig, falsch) = _anteile.GetValueOrDefault(Iso(t));
            var (r, f) = Anteil(richtig, falsch);
            return new TagesAnteil(beschriftungen[i], r, f, richtig, falsch);
        }).ToList();
        long alleRichtig = anteile.Sum(a => a.AnzahlRichtig), alleFalsch = anteile.Sum(a => a.AnzahlFalsch);
        var kopf = DashboardTexte.Plural(tage, "Tag", "Tage") + ": ";
        string anteileText;
        if (alleRichtig + alleFalsch == 0)
        {
            anteileText = kopf + "noch keine bewerteten Aufgaben";
        }
        else
        {
            var (r, f) = Anteil(alleRichtig, alleFalsch);
            anteileText = kopf + Inv($"{r} % richtig, {f} % falsch ({alleRichtig} von {alleRichtig + alleFalsch})");
        }
        return new Tagesreihe(tage, beschriftungen, werte, ziel is { } zz && zz != 0 ? zz : null,
            ziel is { } g && g != 0 ? Inv($"Tagesziel {g}") : null, text, anteile, anteileText);
    }

    /// <summary>split_percent: (richtig %, falsch %) mit round() halb gerade, Summe 100; ohne Aufgaben (null, null).</summary>
    public static (int? Richtig, int? Falsch) Anteil(long richtig, long falsch)
    {
        var gesamt = richtig + falsch;
        if (gesamt == 0)
        {
            return (null, null);
        }
        var anteil = (int)PythonRunden.Runde(richtig * 100.0 / gesamt);
        return (anteil, 100 - anteil);
    }

    private static string Iso(DateOnly tag) => tag.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    // -- Lese-Verbindung (wie DashboardWerte: nur lesend, gescheiterte Abfrage = null) --

    internal sealed class Leser(SqliteConnection? verbindung) : IDisposable
    {
        public static Leser Oeffnen(string pfad)
        {
            if (!File.Exists(pfad))
            {
                return new Leser(null);
            }
            var verbindung = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = pfad,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                DefaultTimeout = Lernstand.Wartezeit,
            }.ToString());
            try
            {
                verbindung.Open();
                using var befehl = verbindung.CreateCommand();
                befehl.CommandText = "PRAGMA query_only = ON";
                befehl.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                verbindung.Dispose();
                return new Leser(null);
            }
            return new Leser(verbindung);
        }

        public List<object?[]>? Abfrage(string sql)
        {
            if (verbindung is null)
            {
                return null;
            }
            try
            {
                using var befehl = verbindung.CreateCommand();
                befehl.CommandText = sql;
                using var leser = befehl.ExecuteReader();
                var zeilen = new List<object?[]>();
                while (leser.Read())
                {
                    var zeile = new object?[leser.FieldCount];
                    for (var i = 0; i < leser.FieldCount; i++)
                    {
                        zeile[i] = leser.IsDBNull(i) ? null : leser.GetValue(i);
                    }
                    zeilen.Add(zeile);
                }
                return zeilen;
            }
            catch (SqliteException)
            {
                return null;
            }
        }

        public void Dispose() => verbindung?.Dispose();
    }
}

/// <summary>Textvergleich wie Python (nach Unicode-Codepunkten, nicht UTF-16-Einheiten).</summary>
internal sealed class PyStringVergleich : IComparer<string>
{
    public static readonly PyStringVergleich Instanz = new();

    public int Compare(string? x, string? y)
    {
        var a = (x ?? "").EnumerateRunes().GetEnumerator();
        var b = (y ?? "").EnumerateRunes().GetEnumerator();
        while (true)
        {
            var hatA = a.MoveNext();
            var hatB = b.MoveNext();
            if (!hatA || !hatB)
            {
                return hatA ? 1 : hatB ? -1 : 0;
            }
            var unterschied = a.Current.Value.CompareTo(b.Current.Value);
            if (unterschied != 0)
            {
                return unterschied;
            }
        }
    }
}
