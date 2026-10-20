using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Fortschritt;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Texte;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L7 Fortschritt: Kacheln, Aufgaben pro Tag (7 und 30 Tage), Richtig und falsch
/// pro Tag, Ergebniskurve, Tabellen, Schwaechen, Rahmenplan-Abdeckung, "Jetzt ueben",
/// Rundungen und Texte genau wie Python (G18). Kontrollfehler (siehe kontrollfehler.md der
/// Welle): Math.Round statt Python-Rundung, 7 statt 30 Tage, Kappung entfernt.
/// </summary>
public class FortschrittTests
{
    private static readonly JsonElement G18 = Lade();
    private static readonly Lerninhalte Lern = Lerninhalte.Laden(Pfade.Inhalte);
    private static readonly DashboardInhalte Parameter = new(Lern);
    private static readonly RahmenplanGewicht Plan = new(Lern);

    private static JsonElement Lade()
    {
        using var f = File.OpenRead(Pfade.Vergleich("auswertung.json"));
        return JsonDocument.Parse(f).RootElement.Clone();
    }

    public static TheoryData<string> Faelle() => Namen("faelle");

    public static TheoryData<string> Abdeckungen() => Namen("abdeckung");

    private static TheoryData<string> Namen(string liste)
    {
        var daten = new TheoryData<string>();
        foreach (var fall in G18.GetProperty(liste).EnumerateArray())
        {
            daten.Add(fall.GetProperty("name").GetString()!);
        }
        return daten;
    }

    private static JsonElement Fall(string liste, string name) =>
        G18.GetProperty(liste).EnumerateArray().Single(f => f.GetProperty("name").GetString() == name);

    // -- Datenbanken wie im Erzeuger ----------------------------------------

    internal static string BaueDatenbank(string db, string ordner)
    {
        var pfad = Path.Combine(ordner, db + ".db");
        switch (db)
        {
            case "leer":
                _ = new Lernstand(pfad);
                break;
            case "voll":
                File.Copy(Pfade.Vergleich("testdaten.db"), pfad);
                break;
            case "gemischt":
            case "sonder":
                _ = new Lernstand(pfad);
                FuelleZeilen(pfad, G18.GetProperty("datenbanken").GetProperty(db));
                break;
            case "ohne_tabelle":
                _ = new Lernstand(pfad);
                FuelleZeilen(pfad, G18.GetProperty("datenbanken").GetProperty("gemischt"));
                foreach (var tabelle in G18.GetProperty("datenbanken").GetProperty(db).GetProperty("ohne").EnumerateArray())
                {
                    using var verbindung = new SqliteConnection($"Data Source={pfad};Pooling=False");
                    verbindung.Open();
                    using var befehl = verbindung.CreateCommand();
                    befehl.CommandText = "DROP TABLE " + tabelle.GetString();
                    befehl.ExecuteNonQuery();
                }
                break;
            default:
                throw new ArgumentException(db);
        }
        return pfad;
    }

    private static void FuelleZeilen(string pfad, JsonElement tabellen)
    {
        using var verbindung = new SqliteConnection($"Data Source={pfad};Pooling=False");
        verbindung.Open();
        using var transaktion = verbindung.BeginTransaction();
        foreach (var tabelle in tabellen.EnumerateObject())
        {
            var spalten = tabelle.Value.GetProperty("spalten").EnumerateArray().Select(s => s.GetString()!).ToList();
            foreach (var zeile in tabelle.Value.GetProperty("zeilen").EnumerateArray())
            {
                using var befehl = verbindung.CreateCommand();
                befehl.CommandText = $"INSERT INTO {tabelle.Name} ({string.Join(", ", spalten)}) VALUES ("
                    + string.Join(", ", spalten.Select((_, i) => "$p" + i)) + ")";
                var i = 0;
                foreach (var wert in zeile.EnumerateArray())
                {
                    befehl.Parameters.AddWithValue("$p" + i++, wert.ValueKind switch
                    {
                        JsonValueKind.Null => DBNull.Value,
                        JsonValueKind.String => wert.GetString()!,
                        JsonValueKind.Number when wert.TryGetInt64(out var n) => n,
                        JsonValueKind.Number => wert.GetDouble(),
                        _ => throw new InvalidDataException(wert.ToString()),
                    });
                }
                befehl.ExecuteNonQuery();
            }
        }
        transaktion.Commit();
    }

    private static DateOnly Tag(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static JsonArray Liste<T>(IEnumerable<T> werte, Func<T, JsonNode?> umwandeln) => new(werte.Select(umwandeln).ToArray());

    /// <summary>Werte in derselben Form wie fall() im Erzeuger.</summary>
    internal static JsonObject AlsJson(Auswertung a, DateOnly heute, int? ziel)
    {
        var tage = new JsonObject();
        foreach (var (anzahl, _) in FortschrittTexte.Zeitraeume)
        {
            var r = a.Reihe(anzahl, heute, ziel);
            tage[anzahl.ToString(CultureInfo.InvariantCulture)] = new JsonObject
            {
                ["beschriftungen"] = Liste(r.Beschriftungen, s => s),
                ["werte"] = Liste(r.Werte, w => w),
                ["ziel"] = r.Ziel,
                ["ziel_text"] = r.ZielText,
                ["zusammenfassung"] = r.Zusammenfassung,
                ["anteile"] = Liste(r.Anteile, t => new JsonArray(t.Beschriftung, t.Richtig, t.Falsch, t.AnzahlRichtig, t.AnzahlFalsch)),
                ["anteile_text"] = r.AnteileText,
            };
        }
        return new JsonObject
        {
            ["kacheln"] = new JsonObject
            {
                ["sessions"] = a.Kacheln[0].Wert, ["sessions_unter"] = a.Kacheln[0].Unter,
                ["durchschnitt"] = a.Kacheln[1].Wert, ["durchschnitt_unter"] = a.Kacheln[1].Unter,
                ["bestes"] = a.Kacheln[2].Wert, ["bestes_unter"] = a.Kacheln[2].Unter,
            },
            ["tage"] = tage,
            ["ergebnisse"] = new JsonObject
            {
                ["beschriftungen"] = Liste(a.ErgebnisBeschriftungen, s => s),
                ["werte"] = Liste(a.ErgebnisWerte, w => w),
            },
            ["sessions"] = Liste(a.Sitzungen, s => new JsonArray(s.Datum, s.Ergebnis, s.Quote, s.Note, s.Dauer)),
            ["pruefungen"] = Liste(a.Pruefungen, p => new JsonArray(p.Datum, p.Name, p.Kurz, p.Punkte, p.Note, p.Dauer)),
            ["schwach"] = a.Schwaeche is { } s ? new JsonObject { ["titel"] = s.Titel, ["themen"] = Liste(s.Themen, t => t) } : null,
        };
    }

    private static JsonObject Wert(AbdeckungWert w) => new() { ["wert"] = w.Wert, ["text"] = w.Text, ["level"] = w.Stufe };

    private static JsonObject MitWert(JsonObject knoten, AbdeckungWert w)
    {
        foreach (var (k, v) in Wert(w))
        {
            knoten[k] = v?.DeepClone();
        }
        return knoten;
    }

    private static JsonArray Blaetter(IEnumerable<AbdeckungBlatt> blaetter, bool punkt) => Liste(blaetter, b => MitWert(new JsonObject
    {
        ["id"] = b.Id, ["titel"] = b.Titel, ["anzahl"] = b.Anzahl, ["anzahl_text"] = b.AnzahlText,
    }, b.Wert));

    internal static JsonObject AbdeckungAlsJson(Abdeckung a, bool alleFilter)
    {
        var sections = new JsonObject();
        foreach (var (filter, _) in alleFilter ? FortschrittTexte.Filter : [(FortschrittTexte.FilterAlle, "")])
        {
            sections[filter] = Liste(a.Abschnitte(filter), s => MitWert(new JsonObject
            {
                ["id"] = s.Id, ["titel"] = s.Titel,
                ["positionen"] = Liste(s.Aeste, p => MitWert(new JsonObject
                {
                    ["id"] = p.Id, ["titel"] = p.Titel, ["punkte"] = Blaetter(p.Blaetter, true),
                }, p.Wert)),
            }, s.Wert));
        }
        return new JsonObject
        {
            ["any_answers"] = a.IrgendeineAntwort,
            ["sections"] = sections,
            ["fields"] = Liste(a.Lernfelder(), j => MitWert(new JsonObject
            {
                ["jahr"] = int.Parse(j.Id, CultureInfo.InvariantCulture), ["titel"] = j.Titel,
                ["felder"] = Liste(j.Aeste, f => MitWert(new JsonObject
                {
                    ["id"] = f.Id, ["titel"] = f.Titel, ["stunden"] = f.Stunden, ["aktuell"] = f.Aktuell,
                    ["themen"] = Blaetter(f.Blaetter, false),
                }, f.Wert)),
            }, j.Wert)),
        };
    }

    /// <summary>Vergleicht zwei JSON-Baeume; Zahlen als double exakt, sonst exakt; Reihenfolge der Schluessel egal.</summary>
    internal static void Vergleiche(JsonElement soll, JsonNode? ist, string ort, List<string> fehler)
    {
        if (fehler.Count > 20)
        {
            return;
        }
        switch (soll.ValueKind)
        {
            case JsonValueKind.Object:
                if (ist is not JsonObject objekt)
                {
                    fehler.Add($"{ort}: Objekt erwartet, ist {ist?.ToJsonString()}");
                    return;
                }
                foreach (var eintrag in soll.EnumerateObject())
                {
                    Vergleiche(eintrag.Value, objekt[eintrag.Name], ort + "." + eintrag.Name, fehler);
                }
                foreach (var (name, _) in objekt)
                {
                    if (!soll.TryGetProperty(name, out _))
                    {
                        fehler.Add($"{ort}.{name}: fehlt in G18");
                    }
                }
                break;
            case JsonValueKind.Array:
                if (ist is not JsonArray feld || feld.Count != soll.GetArrayLength())
                {
                    fehler.Add($"{ort}: Liste mit {soll.GetArrayLength()} erwartet, ist {ist?.ToJsonString()}");
                    return;
                }
                var i = 0;
                foreach (var element in soll.EnumerateArray())
                {
                    Vergleiche(element, feld[i], $"{ort}[{i}]", fehler);
                    i++;
                }
                break;
            case JsonValueKind.Number:
                if (ist is not JsonValue zahl || zahl.GetValueKind() != JsonValueKind.Number
                    || double.Parse(zahl.ToJsonString(), CultureInfo.InvariantCulture) != soll.GetDouble())
                {
                    fehler.Add($"{ort}: {soll.GetRawText()} != {ist?.ToJsonString()}");
                }
                break;
            case JsonValueKind.Null:
                if (ist is not null)
                {
                    fehler.Add($"{ort}: null != {ist.ToJsonString()}");
                }
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                if (ist is not JsonValue b || !b.TryGetValue<bool>(out var wahr) || wahr != soll.GetBoolean())
                {
                    fehler.Add($"{ort}: {soll.GetRawText()} != {ist?.ToJsonString()}");
                }
                break;
            default:
                if (ist is not JsonValue text || !text.TryGetValue<string>(out var s) || s != soll.GetString())
                {
                    fehler.Add($"{ort}: {soll.GetRawText()} != {ist?.ToJsonString()}");
                }
                break;
        }
    }

    private static int? Ziel(JsonElement einstellungen)
    {
        var ziel = Einstellungen.Ziel(JsonNode.Parse(einstellungen.GetRawText())!.AsObject());
        return ziel.ZielAn ? ziel.ZielAnzahl : null;
    }

    private static StatusBuch Buch(string pfad) => new(Antwortbuch.Lesen(new Lernstand(pfad)));

    // -- Tests ------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Faelle))]
    public void AuswertungWiePython(string name)
    {
        var fall = Fall("faelle", name);
        using var ordner = new TempOrdner();
        var pfad = BaueDatenbank(fall.GetProperty("db").GetString()!, ordner.Pfad);
        var vorher = File.ReadAllBytes(pfad);
        var ist = AlsJson(Auswertung.Lesen(pfad), Tag(fall.GetProperty("heute").GetString()!), Ziel(fall.GetProperty("einstellungen")));
        var fehler = new List<string>();
        Vergleiche(fall.GetProperty("werte"), ist, name, fehler);
        Assert.True(fehler.Count == 0, string.Join("\n", fehler));
        Assert.Equal(vorher, File.ReadAllBytes(pfad));        // nur gelesen
    }

    [Theory]
    [MemberData(nameof(Abdeckungen))]
    public void AbdeckungWiePython(string name)
    {
        var fall = Fall("abdeckung", name);
        using var ordner = new TempOrdner();
        var pfad = BaueDatenbank(fall.GetProperty("db").GetString()!, ordner.Pfad);
        var werte = RahmenplanWerte.Aus(JsonNode.Parse(fall.GetProperty("einstellungen").GetRawText())!.AsObject(), Plan);
        var abdeckung = new Abdeckung(Parameter, Plan, Buch(pfad), werte);
        var soll = fall.GetProperty("werte");
        var ist = AbdeckungAlsJson(abdeckung, soll.GetProperty("sections").EnumerateObject().Count() > 1);
        var fehler = new List<string>();
        Vergleiche(soll, ist, name, fehler);
        Assert.True(fehler.Count == 0, string.Join("\n", fehler));
    }

    [Fact]
    public void JetztUebenWiePython()
    {
        using var ordner = new TempOrdner();
        var buecher = new Dictionary<string, StatusBuch>
        {
            ["leer"] = Buch(BaueDatenbank("leer", ordner.Pfad)),
            ["gemischt"] = Buch(BaueDatenbank("gemischt", ordner.Pfad)),
        };
        var abdeckung = new Abdeckung(Parameter, Plan, buecher["leer"], RahmenplanWerte.Vorgabe);
        var fehler = new List<string>();
        foreach (var fall in G18.GetProperty("ueben").EnumerateArray())
        {
            var buch = buecher[fall.GetProperty("db").GetString()!];
            var name = fall.GetProperty("name").GetString()!;
            List<(string Quelle, List<string> Schluessel)> teile;
            if (fall.GetProperty("art").GetString() == "thema")
            {
                var themen = JetztUeben.Themen(Lern, name);
                Vergleiche(fall.GetProperty("themen"), Liste(themen, t => t), name + ".themen", fehler);
                teile = JetztUeben.ThemenTeile(Lern, buch, themen);
            }
            else
            {
                var (quiz, karte) = abdeckung.PunktInhalte(name);
                teile = JetztUeben.Teile(buch, quiz, karte);
            }
            Vergleiche(fall.GetProperty("teile"), Liste(teile, t => new JsonObject
            {
                ["quelle"] = t.Quelle, ["schluessel"] = Liste(t.Schluessel, s => s),
            }), fall.GetProperty("db").GetString() + "." + name, fehler);
        }
        Assert.True(fehler.Count == 0, string.Join("\n", fehler));
    }

    [Fact]
    public void RundungenWiePython()
    {
        var r = G18.GetProperty("runden");
        foreach (var paar in r.GetProperty("round").EnumerateArray())
        {
            Assert.Equal(paar[1].GetInt64(), PythonRunden.Runde(paar[0].GetDouble()));
        }
        foreach (var paar in r.GetProperty("round1").EnumerateArray())
        {
            Assert.Equal(paar[1].GetDouble(), PythonRunden.Runde1(paar[0].GetDouble()));
        }
        foreach (var paar in r.GetProperty("de_number1").EnumerateArray())
        {
            Assert.Equal(paar[1].GetString(), Trainer.DeZahl(paar[0].GetDouble(), 1));
        }
        foreach (var paar in r.GetProperty("percent_text").EnumerateArray())
        {
            double? wert = paar[0].ValueKind == JsonValueKind.Null ? null : paar[0].GetDouble();
            Assert.Equal(paar[1].GetString(), new AbdeckungWert(wert).Text);
        }
        foreach (var paar in r.GetProperty("level").EnumerateArray())
        {
            Assert.Equal(paar[1].GetString(), Abdeckung.Stufe(paar[0].GetDouble()));
        }
        foreach (var paar in r.GetProperty("punkte_text").EnumerateArray())
        {
            var wert = paar[0].ValueKind == JsonValueKind.String
                ? double.Parse(paar[0].GetString()!.Replace("inf", "Infinity", StringComparison.Ordinal).Replace("nan", "NaN", StringComparison.Ordinal), CultureInfo.InvariantCulture)
                : paar[0].GetDouble();
            Assert.Equal(paar[1].GetString(), PythonRunden.PunkteText(wert));
        }
        foreach (var zeile in r.GetProperty("split_percent").EnumerateArray())
        {
            var (richtig, falsch) = Auswertung.Anteil(zeile[0].GetInt64(), zeile[1].GetInt64());
            Assert.Equal(zeile[2].ValueKind == JsonValueKind.Null ? null : zeile[2].GetInt32(), richtig);
            Assert.Equal(zeile[3].ValueKind == JsonValueKind.Null ? null : zeile[3].GetInt32(), falsch);
        }
    }

    [Fact]
    public void MathRoundWaereFalsch()
    {
        // Kontrollfehler sichtbar gemacht: .NET-Rundungen weichen bei G18-Werten von Python ab
        var r = G18.GetProperty("runden");
        Assert.Contains(r.GetProperty("round1").EnumerateArray(), p => Math.Round(p[0].GetDouble(), 1) != p[1].GetDouble());
        Assert.Contains(r.GetProperty("split_percent").EnumerateArray(), z => z[2].ValueKind != JsonValueKind.Null
            && (int)Math.Round(z[0].GetInt64() * 100.0 / (z[0].GetInt64() + z[1].GetInt64()), MidpointRounding.AwayFromZero) != z[2].GetInt32());
    }

    [Fact]
    public void TexteWiePython()
    {
        var t = G18.GetProperty("texte");
        var erwartet = new Dictionary<string, string>
        {
            ["Sessions"] = FortschrittTexte.Sessions, ["Durchschnitt"] = FortschrittTexte.Durchschnitt,
            ["Bestes"] = FortschrittTexte.Bestes, ["SessionsTitel"] = FortschrittTexte.SessionsTitel,
            ["SpalteDatum"] = FortschrittTexte.SpalteDatum, ["SpalteErgebnis"] = FortschrittTexte.SpalteErgebnis,
            ["SpalteQuote"] = FortschrittTexte.SpalteQuote, ["SpalteDauer"] = FortschrittTexte.SpalteDauer,
            ["PruefungenTitel"] = FortschrittTexte.PruefungenTitel, ["PruefungenUnter"] = FortschrittTexte.PruefungenUnter,
            ["SpaltePruefung"] = FortschrittTexte.SpaltePruefung, ["SpaltePunkte"] = FortschrittTexte.SpaltePunkte,
            ["Aktualisieren"] = FortschrittTexte.Aktualisieren, ["SessionsUnter"] = FortschrittTexte.SessionsUnter,
            ["ProzentMuster"] = FortschrittTexte.ProzentMuster, ["DurchschnittUnter"] = FortschrittTexte.DurchschnittUnter,
            ["OhneWert"] = FortschrittTexte.OhneWert, ["BestesLeer"] = FortschrittTexte.BestesLeer,
            ["ErgebnisMuster"] = FortschrittTexte.ErgebnisMuster, ["DauerMuster"] = FortschrittTexte.DauerMuster,
            ["BalkenLegende"] = FortschrittTexte.BalkenLegende, ["SchwachTitel"] = FortschrittTexte.SchwachTitel,
            ["NoteKurz"] = FortschrittTexte.SpalteNote,
        };
        Assert.Equal(erwartet.Keys.Order(StringComparer.Ordinal), t.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        foreach (var (schluessel, text) in erwartet)
        {
            Assert.Equal(t.GetProperty(schluessel).GetString(), text);
        }
        var k = G18.GetProperty("konstanten");
        string K(string name) => k.GetProperty(name).GetString()!;
        Assert.Equal(FortschrittTexte.TageTitel, K("DAY_CHART_TITLE"));
        Assert.Equal(FortschrittTexte.TageUnter, K("DAY_CHART_SUBTITLE"));
        Assert.Equal(FortschrittTexte.TageReihe, K("DAY_CHART_SERIES"));
        Assert.Equal(k.GetProperty("DAY_CHART_RANGES").EnumerateArray().Select(z => (z[0].GetInt32(), z[1].GetString()!)), FortschrittTexte.Zeitraeume);
        Assert.Equal(FortschrittTexte.AnteilTitel, K("SPLIT_CHART_TITLE"));
        Assert.Equal(FortschrittTexte.AnteilRichtig, K("SPLIT_RIGHT"));
        Assert.Equal(FortschrittTexte.AnteilFalsch, K("SPLIT_WRONG"));
        Assert.Equal(FortschrittTexte.AnteilLeerTag, K("SPLIT_EMPTY_DAY"));
        Assert.Equal(FortschrittTexte.ErgebnisTitel, K("RESULT_CHART_TITLE"));
        Assert.Equal(FortschrittTexte.ErgebnisUnter, K("RESULT_CHART_SUBTITLE"));
        Assert.Equal(FortschrittTexte.ErgebnisReihe, K("RESULT_CHART_SERIES"));
        Assert.Equal(FortschrittTexte.ErgebnisLeer, K("RESULT_CHART_EMPTY"));
        Assert.Equal(JetztUeben.Runde, k.GetProperty("PRACTICE_ROUND").GetInt32());
        Assert.Equal(FortschrittTexte.Ueben, K("PRACTICE_BUTTON"));
        Assert.Equal(FortschrittTexte.UebenKeine, K("PRACTICE_NONE"));
        Assert.Equal(FortschrittTexte.WeiterTitel, K("PRACTICE_NEXT_TITLE"));
        Assert.Equal(k.GetProperty("WEEKDAYS").EnumerateArray().Select(w => w.GetString()!), FortschrittTexte.Wochentage);
        Assert.Equal(FortschrittTexte.AbdeckungTitel, K("COVERAGE_TITLE"));
        Assert.Equal(FortschrittTexte.AbdeckungUnter, K("COVERAGE_SUBTITLE"));
        Assert.Equal(k.GetProperty("COVERAGE_VIEWS").EnumerateArray().Select(z => (z[0].GetString()!, z[1].GetString()!)), FortschrittTexte.Ansichten);
        Assert.Equal(k.GetProperty("COVERAGE_FILTERS").EnumerateArray().Select(z => (z[0].GetString()!, z[1].GetString()!)), FortschrittTexte.Filter);
        Assert.Equal(FortschrittTexte.KeinInhalt, K("NO_CONTENT"));
        Assert.Equal(FortschrittTexte.KeineAntworten, K("NO_ANSWERS"));
        Assert.Equal(FortschrittTexte.AktuellesLernfeld, K("CURRENT_FIELD_TEXT"));
        Assert.Equal(FortschrittTexte.FilterLeer, K("FILTER_EMPTY"));
        foreach (var thema in k.GetProperty("EXAM_THEME_TOPICS").EnumerateObject())
        {
            Assert.Equal(thema.Value.EnumerateArray().Select(t => t.GetString()!), JetztUeben.PruefungsThemen[thema.Name]);
        }
        Assert.Equal(k.GetProperty("EXAM_THEME_TOPICS").EnumerateObject().Count(), JetztUeben.PruefungsThemen.Count);
        foreach (var art in k.GetProperty("EXAMS").EnumerateObject())
        {
            Assert.Equal((art.Value[0].GetString()!, art.Value[1].GetString()!), FortschrittTexte.Pruefungen[art.Name]);
        }
        Assert.Equal(k.GetProperty("EXAMS").EnumerateObject().Count(), FortschrittTexte.Pruefungen.Count);
        foreach (var zeile in G18.GetProperty("folgetexte").EnumerateArray())
        {
            Assert.Equal(zeile[2].GetString(), FortschrittTexte.WeiterText(zeile[0].GetString()!, zeile[1].GetInt32()));
        }
        foreach (var zeile in G18.GetProperty("jahre").EnumerateArray())
        {
            Assert.Equal(zeile[1].GetString(), Beschriftungen.Einsetzen(FortschrittTexte.Jahr, zeile[0].GetInt32()));
        }
    }

    // -- Weisung P2-8/P2-9: "% richtig" nie ueber 100 (je Stelle ein Test) --

    /// <summary>Datenbank mit Sessions; die erste mit kaputtem Wert (15 von 1 richtig = 1500 %).</summary>
    private static Auswertung KaputteSessions(TempOrdner ordner)
    {
        var pfad = Path.Combine(ordner.Pfad, "kaputt.db");
        _ = new Lernstand(pfad);
        using (var verbindung = new SqliteConnection($"Data Source={pfad};Pooling=False"))
        {
            verbindung.Open();
            foreach (var (zeit, richtig, gesamt, prozent) in new[] { ("2026-10-19 10:00:00", 15, 1, 1500.0), ("2026-10-20 10:00:00", 1, 2, 50.0) })
            {
                using var befehl = verbindung.CreateCommand();
                befehl.CommandText = "INSERT INTO test_results (timestamp, score, total, percentage, note, duration_seconds)"
                    + " VALUES ($t, $s, $g, $p, 'x', 60)";
                befehl.Parameters.AddWithValue("$t", zeit);
                befehl.Parameters.AddWithValue("$s", richtig);
                befehl.Parameters.AddWithValue("$g", gesamt);
                befehl.Parameters.AddWithValue("$p", prozent);
                befehl.ExecuteNonQuery();
            }
        }
        return Auswertung.Lesen(pfad);
    }

    [Fact]
    public void KappungDurchschnitt()
    {
        using var ordner = new TempOrdner();
        Assert.Equal("100,0 %", KaputteSessions(ordner).Kacheln[1].Wert);   // Python: 775,0 %
    }

    [Fact]
    public void KappungBestesErgebnis()
    {
        using var ordner = new TempOrdner();
        var bestes = KaputteSessions(ordner).Kacheln[2];
        Assert.Equal("100,0 %", bestes.Wert);
        Assert.Equal(Trainer.Note(100), bestes.Unter);
    }

    [Fact]
    public void KappungErfolgsquoteDerSession()
    {
        using var ordner = new TempOrdner();
        var a = KaputteSessions(ordner);
        Assert.Equal("100,0 %", a.Sitzungen[1].Quote);
        Assert.Equal("50,0 %", a.Sitzungen[0].Quote);
    }

    [Fact]
    public void KappungErgebniskurve()
    {
        using var ordner = new TempOrdner();
        Assert.Equal([100.0, 50.0], KaputteSessions(ordner).ErgebnisWerte);
    }

    [Fact]
    public void JetztUebenBehaeltReihenfolge()
    {
        // practice(keys, keep_order=True): die Runde laeuft in der Rangfolge, ohne Mischen
        using var ordner = new TempOrdner();
        var buch = Buch(BaueDatenbank("leer", ordner.Pfad));
        var fragen = Lern.Quizfragen.Take(12).Select(q => q.Frage!).Reverse().ToList();
        var uebung = new Uebung(Lern, () => buch, null);
        uebung.Ueben(fragen, reihenfolgeBehalten: true);
        Assert.Equal(fragen.Take(Uebung.RundeMax), uebung.Runde.Select(q => q.Frage!));
        // Gegenprobe: ohne keep_order wird gemischt (12 Fragen, gleiche Reihenfolge fast unmoeglich)
        var gemischt = new Uebung(Lern, () => buch, null, zufall: new Core.Zufall.PythonZufall(7));
        gemischt.Ueben(fragen);
        Assert.NotEqual(fragen, gemischt.Runde.Select(q => q.Frage!));
    }

    [Fact]
    public void FehlendeDateiWieLeerOhneAnlegen()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, "fehlt.db");
        var a = Auswertung.Lesen(pfad);
        Assert.False(File.Exists(pfad));
        Assert.Equal("0", a.Kacheln[0].Wert);
        Assert.Equal(FortschrittTexte.OhneWert, a.Kacheln[2].Wert);
        Assert.Equal(30, a.Reihe(30, new DateOnly(2026, 10, 20), 20).Werte.Count);
    }
}
