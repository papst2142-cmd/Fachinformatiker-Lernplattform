using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 1, K7 Teil "Dashboard-Werte": alle Zahlen und Texte des Dashboards
/// (PC und Handy) exakt wie Python (G13), nur lesend aus der Datenbank.
/// Verhaltenstabelle: leere, teilweise, volle, kaputte und fehlende Datenbank;
/// die Datei bleibt bytegleich bzw. wird nicht angelegt. Nur Testdaten.
/// </summary>
public class DashboardWerteTests
{
    private static readonly JsonElement G13 = Lade();
    private static readonly DashboardInhalte Inhalte = new(Lerninhalte.Laden(Pfade.Inhalte));

    private static JsonElement Lade()
    {
        using var f = File.OpenRead(Pfade.Vergleich("dashboard.json"));
        return JsonDocument.Parse(f).RootElement.Clone();
    }

    public static TheoryData<string> Faelle()
    {
        var daten = new TheoryData<string>();
        foreach (var fall in G13.GetProperty("faelle").EnumerateArray())
        {
            daten.Add(fall.GetProperty("name").GetString()!);
        }
        return daten;
    }

    private static JsonElement Fall(string name) =>
        G13.GetProperty("faelle").EnumerateArray().Single(f => f.GetProperty("name").GetString() == name);

    // -- Datenbanken wie im Erzeuger ----------------------------------------

    /// <summary>Baut die Datenbank des Falls im Ordner auf (Schema von Lernstand, Zeilen aus G13).</summary>
    private static string BaueDatenbank(string db, string ordner)
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
            case "teilweise":
            case "gemischt":
                _ = new Lernstand(pfad);
                FuelleZeilen(pfad, G13.GetProperty("datenbanken").GetProperty(db));
                break;
            case "ohne_tabelle":
                var info = G13.GetProperty("datenbanken").GetProperty(db);
                _ = new Lernstand(pfad);
                FuelleZeilen(pfad, G13.GetProperty("datenbanken").GetProperty(info.GetProperty("basis").GetString()!));
                foreach (var tabelle in info.GetProperty("ohne").EnumerateArray())
                {
                    Ausfuehren(pfad, "DROP TABLE " + tabelle.GetString());
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

    private static void Ausfuehren(string pfad, string sql)
    {
        using var verbindung = new SqliteConnection($"Data Source={pfad};Pooling=False");
        verbindung.Open();
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText = sql;
        befehl.ExecuteNonQuery();
    }

    private static string Sha256(string pfad) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pfad)));

    private static string[] Dateien(string ordner) =>
        Directory.EnumerateFileSystemEntries(ordner).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;

    private static DashboardWerte Lies(string pfad, JsonElement fall) => DashboardWerte.Lesen(pfad,
        DateOnly.ParseExact(fall.GetProperty("heute").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture), Inhalte,
        Einstellungen.Ziel(JsonNode.Parse(fall.GetProperty("einstellungen").GetRawText())!.AsObject()),
        fall.GetProperty("nutzername").ValueKind == JsonValueKind.Null ? null : fall.GetProperty("nutzername").GetString());

    // -- Umwandlung in die Form von G13 -------------------------------------

    private static JsonNode? Zahl(double wert) => JsonValue.Create(wert);

    /// <summary>Werte des Dashboards in derselben Form wie werte() im Erzeuger.</summary>
    internal static JsonObject AlsJson(DashboardWerte w, DashboardKalender vormonat, DashboardKalender folgemonat)
    {
        JsonNode Kalender(DashboardKalender k) => new JsonObject
        {
            ["jahr"] = k.Jahr, ["monat"] = k.Monat, ["titel"] = k.Titel,
            ["lerntage"] = new JsonArray(k.Lerntage.Select(t => (JsonNode)t).ToArray()),
            ["wochen"] = new JsonArray(k.Wochen.Select(woche => (JsonNode)new JsonArray(woche.Select(t => (JsonNode)t).ToArray())).ToArray()),
        };
        JsonNode Themen(IReadOnlyList<DashboardThema> liste) => new JsonArray(liste.Select(t => (JsonNode)new JsonObject
        {
            ["name"] = t.Name, ["prozent"] = Zahl(t.Prozent), ["ring_text"] = t.RingText,
        }).ToArray());
        return new JsonObject
        {
            ["zustand"] = w.Zustand.ToString(),
            ["banner"] = new JsonObject
            {
                ["titel"] = w.Banner.Titel, ["text_pc"] = w.Banner.TextPc, ["text_handy"] = w.Banner.TextHandy,
                ["gross"] = w.Banner.Gross, ["gross_unter"] = w.Banner.GrossUnter,
            },
            ["bearbeitet"] = w.Bearbeitet,
            ["inhalte_gesamt"] = w.InhalteGesamt,
            ["lernserie"] = w.Lernserie,
            ["tagesziel"] = new JsonObject
            {
                ["sichtbar"] = w.Tagesziel.Sichtbar, ["erledigt"] = w.Tagesziel.Erledigt, ["ziel"] = w.Tagesziel.Ziel,
                ["erreicht"] = w.Tagesziel.Erreicht, ["anteil"] = Zahl(w.Tagesziel.Anteil), ["text"] = w.Tagesziel.Text,
                ["ring_text"] = w.Tagesziel.RingText,
            },
            ["faellig"] = new JsonObject
            {
                ["karten"] = w.Faellig.Karten, ["fragen"] = w.Faellig.Fragen, ["text"] = w.Faellig.Text,
                ["wiederholen"] = w.Faellig.Wiederholen, ["altbestand_wartet"] = w.Faellig.AltbestandWartet,
                ["erste_quelle"] = w.Faellig.ErsteQuelle,
            },
            ["kennzahlen"] = new JsonArray(w.Kennzahlen.Select(k => (JsonNode)new JsonObject
            {
                ["titel"] = k.Titel, ["wert"] = k.Wert, ["gesamt"] = k.Gesamt, ["anteil"] = Zahl(k.Anteil),
                ["gross"] = k.Gross, ["unter"] = k.Unter, ["balken_prozent"] = Zahl(k.BalkenProzent),
                ["balken_text"] = k.BalkenText,
            }).ToArray()),
            ["quote"] = new JsonObject
            {
                ["wert"] = Zahl(w.Quote.Wert), ["richtig"] = Zahl(w.Quote.Richtig), ["antworten"] = w.Quote.Antworten,
                ["text"] = w.Quote.Text, ["unter"] = w.Quote.Unter,
            },
            ["verlauf"] = new JsonArray(w.Verlauf.Select(t => (JsonNode)new JsonArray(
                t.Tag.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), t.Beschriftung, t.Anzahl)).ToArray()),
            ["fachbereiche"] = new JsonArray(w.Fachbereiche.Select(f => (JsonNode)new JsonObject
            {
                ["name"] = f.Name, ["kurz"] = f.Kurz, ["abdeckung"] = Zahl(f.Abdeckung), ["ring_text"] = f.RingText,
                ["ring_text_handy"] = f.RingTextHandy, ["antworten"] = f.Antworten, ["richtig"] = Zahl(f.Richtig),
                ["detail"] = f.Detail,
            }).ToArray()),
            ["aktivitaeten"] = new JsonArray(w.Aktivitaeten.Select(a => (JsonNode)new JsonObject
            {
                ["zeit"] = a.Zeit, ["art"] = a.Art, ["detail"] = a.Detail, ["extra"] = a.Extra,
                ["pc1"] = a.Pc1, ["pc2"] = a.Pc2, ["handy1"] = a.Handy1, ["handy2"] = a.Handy2,
            }).ToArray()),
            ["kalender"] = new JsonArray(Kalender(w.Kalender), Kalender(vormonat), Kalender(folgemonat)),
            ["ap1_themen"] = Themen(w.Ap1Themen),
            ["ap2_themen"] = Themen(w.Ap2Themen),
            ["reinzoom"] = new JsonArray(w.Reinzoom.Select(r => (JsonNode)new JsonObject
            {
                ["fachbereich"] = r.Fachbereich, ["titel"] = r.Titel,
                ["themen"] = new JsonArray(r.Themen.Select(t => (JsonNode)new JsonObject
                {
                    ["id"] = t.Id, ["name"] = t.Name, ["kurz"] = t.Kurz,
                    ["heatmap"] = new JsonArray(t.Heatmap.Select(h => (JsonNode)h).ToArray()),
                    ["wissen"] = Zahl(t.Wissen), ["antworten"] = t.Antworten, ["richtig"] = Zahl(t.Richtig),
                    ["notiz"] = t.Notiz,
                }).ToArray()),
            }).ToArray()),
        };
    }

    /// <summary>Vergleicht zwei JSON-Baeume; Zahlen als double (exakt gleich), sonst exakt.</summary>
    private static void Vergleiche(JsonElement soll, JsonNode? ist, string ort, List<string> fehler)
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
                    fehler.Add($"{ort}: kein Objekt");
                    return;
                }
                var namen = soll.EnumerateObject().Select(p => p.Name).ToHashSet();
                if (!namen.SetEquals(objekt.Select(p => p.Key)))
                {
                    fehler.Add($"{ort}: Schluessel {string.Join(",", namen.Order())} != {string.Join(",", objekt.Select(p => p.Key).Order())}");
                }
                foreach (var p in soll.EnumerateObject())
                {
                    Vergleiche(p.Value, objekt[p.Name], ort + "." + p.Name, fehler);
                }
                return;
            case JsonValueKind.Array:
                if (ist is not JsonArray liste || liste.Count != soll.GetArrayLength())
                {
                    fehler.Add($"{ort}: Liste {soll.GetArrayLength()} != {ist?.ToJsonString()}");
                    return;
                }
                var i = 0;
                foreach (var e in soll.EnumerateArray())
                {
                    Vergleiche(e, liste[i], $"{ort}[{i}]", fehler);
                    i++;
                }
                return;
            case JsonValueKind.Number:
                if (ist is not JsonValue zahl || zahl.GetValueKind() != JsonValueKind.Number
                    || double.Parse(zahl.ToJsonString(), CultureInfo.InvariantCulture) != soll.GetDouble())
                {
                    fehler.Add($"{ort}: {soll.GetRawText()} != {ist?.ToJsonString()}");
                }
                return;
            case JsonValueKind.String:
                if (ist is not JsonValue text || text.GetValueKind() != JsonValueKind.String || text.GetValue<string>() != soll.GetString())
                {
                    fehler.Add($"{ort}: {soll.GetRawText()} != {ist?.ToJsonString()}");
                }
                return;
            case JsonValueKind.True:
            case JsonValueKind.False:
                if (ist is not JsonValue wahr || wahr.GetValueKind() != soll.ValueKind)
                {
                    fehler.Add($"{ort}: {soll.GetRawText()} != {ist?.ToJsonString()}");
                }
                return;
            case JsonValueKind.Null:
                if (ist is not null)
                {
                    fehler.Add($"{ort}: null != {ist.ToJsonString()}");
                }
                return;
            default:
                fehler.Add($"{ort}: unbekannte Art {soll.ValueKind}");
                return;
        }
    }

    private static JsonObject LiesAlsJson(string pfad, JsonElement fall)
    {
        var werte = Lies(pfad, fall);
        var kalender = fall.GetProperty("werte").GetProperty("kalender");
        DashboardKalender Monat(int i) => DashboardWerte.Monat(pfad,
            kalender[i].GetProperty("jahr").GetInt32(), kalender[i].GetProperty("monat").GetInt32());
        return AlsJson(werte, Monat(1), Monat(2));
    }

    // -- Tests ------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Faelle))]
    public void WerteWiePython(string name)
    {
        var fall = Fall(name);
        using var ordner = new TempOrdner();
        var pfad = BaueDatenbank(fall.GetProperty("db").GetString()!, ordner.Pfad);
        var vorher = Sha256(pfad);
        var dateienVorher = Dateien(ordner.Pfad);

        var ist = LiesAlsJson(pfad, fall);

        var fehler = new List<string>();
        Vergleiche(fall.GetProperty("werte"), ist, name, fehler);
        Assert.True(fehler.Count == 0, string.Join("\n", fehler));
        // Nur gelesen: Datei bytegleich, keine weiteren Dateien (Journal o. a.)
        Assert.Equal(vorher, Sha256(pfad));
        Assert.Equal(dateienVorher, Dateien(ordner.Pfad));
    }

    /// <summary>Verhaltenstabelle: Datei fehlt / kaputt -> Zustand, Werte wie leere Datenbank, nichts geschrieben.</summary>
    public static TheoryData<string> Sonderfaelle() => ["fehlt", "ordner", "zufallsbytes", "null_bytes", "abgeschnitten", "text"];

    [Theory]
    [MemberData(nameof(Sonderfaelle))]
    public void FehltOderKaputtWieLeer(string art)
    {
        var leer = Fall("leer_2026-10-20_0");
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, "lernplattform.db");
        var erwartet = DashboardZustand.Kaputt;
        switch (art)
        {
            case "fehlt":
                erwartet = DashboardZustand.Fehlt;
                break;
            case "ordner":
                Directory.CreateDirectory(pfad);
                erwartet = DashboardZustand.Fehlt;
                break;
            case "zufallsbytes":
                var zufall = new byte[8192];
                new Random(1313).NextBytes(zufall);
                File.WriteAllBytes(pfad, zufall);
                break;
            case "null_bytes":
                File.WriteAllBytes(pfad, []);
                break;
            case "abgeschnitten":
                var voll = File.ReadAllBytes(Pfade.Vergleich("testdaten.db"));
                File.WriteAllBytes(pfad, voll[..(voll.Length / 3)]);
                break;
            case "text":
                File.WriteAllText(pfad, "Das ist keine Datenbank.\n");
                break;
        }
        var vorher = File.Exists(pfad) ? Sha256(pfad) : null;
        var dateienVorher = Dateien(ordner.Pfad);

        var ist = LiesAlsJson(pfad, leer);

        Assert.Equal(erwartet.ToString(), (string?)ist["zustand"]);
        // Weisung P1-4 (N1): Hinweiskarte nur bei Kaputt, nicht bei Fehlt
        Assert.Equal(erwartet == DashboardZustand.Kaputt ? DashboardTexte.LernstandNichtLesbar : null, DashboardTexte.Hinweis(erwartet));
        ist["zustand"] = "Leer";
        var fehler = new List<string>();
        Vergleiche(leer.GetProperty("werte"), ist, art, fehler);
        Assert.True(fehler.Count == 0, string.Join("\n", fehler));
        Assert.Equal(dateienVorher, Dateien(ordner.Pfad));
        if (vorher is null)
        {
            Assert.False(File.Exists(pfad));
        }
        else
        {
            Assert.Equal(vorher, Sha256(pfad));
        }
    }

    [Fact]
    public void HinweisKarteNurBeiKaputt()
    {
        foreach (var zustand in Enum.GetValues<DashboardZustand>())
        {
            Assert.Equal(zustand == DashboardZustand.Kaputt ? DashboardTexte.LernstandNichtLesbar : null, DashboardTexte.Hinweis(zustand));
        }
        Assert.Equal("Der Lernstand konnte nicht vollst\u00e4ndig gelesen werden. Er wurde nicht ver\u00e4ndert.", DashboardTexte.LernstandNichtLesbar);
    }

    [Fact]
    public void ProgrammfehlerInsFehlerprotokoll()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, "lernplattform.db");
        _ = new Lernstand(pfad);
        Ausfuehren(pfad, "INSERT INTO card_events (timestamp, category, question, mode, correct) VALUES ('kaputt', 'Netzwerk & Protokolle', 'Was ist ein MPO-Stecker?', 'lernen', 1)");
        Assert.Equal(DashboardZustand.Kaputt, DashboardWerte.Lesen(pfad, new DateOnly(2026, 10, 20), Inhalte, Einstellungen.Ziel(new JsonObject()), null, Path.Combine(ordner.Pfad, "fehler.log")).Zustand);
        Assert.Contains("Dashboard: FormatException", File.ReadAllText(Path.Combine(ordner.Pfad, "fehler.log")), StringComparison.Ordinal);
    }

    [Fact]
    public void ZustandJeDatenbank()
    {
        // Verhaltenstabelle (Teil mit Python-Werten): leer -> Leer, teilweise/voll/gemischt -> Daten,
        // Tabelle fehlt -> Kaputt (Werte der uebrigen Abfragen wie Python)
        var zustand = G13.GetProperty("faelle").EnumerateArray()
            .GroupBy(f => f.GetProperty("db").GetString()!)
            .ToDictionary(g => g.Key, g => g.Select(f => f.GetProperty("werte").GetProperty("zustand").GetString()).Distinct().Single());
        Assert.Equal("Leer", zustand["leer"]);
        Assert.Equal("Daten", zustand["teilweise"]);
        Assert.Equal("Daten", zustand["voll"]);
        Assert.Equal("Daten", zustand["gemischt"]);
        Assert.Equal("Kaputt", zustand["ohne_tabelle"]);
    }

    [Fact]
    public void TexteWiePython()
    {
        var konstanten = G13.GetProperty("konstanten");
        var dash = new Dictionary<string, string>
        {
            ["DASH_LEARNED"] = DashboardTexte.Gelernt, ["DASH_RATE"] = DashboardTexte.Rate, ["DASH_TOTAL"] = DashboardTexte.Gesamt,
            ["DASH_RING_CARDS"] = DashboardTexte.RingKarten, ["DASH_RING_QUIZ"] = DashboardTexte.RingQuiz,
            ["DASH_RING_AP1"] = DashboardTexte.RingAp1, ["DASH_RING_AP2"] = DashboardTexte.RingAp2,
            ["DASH_CARDS_SUB"] = DashboardTexte.KartenUnter, ["DASH_QUIZ_SUB"] = DashboardTexte.QuizUnter,
            ["DASH_SCEN_SUB"] = DashboardTexte.SzenarienUnter, ["DASH_QUOTE_TITLE"] = DashboardTexte.QuoteTitel,
            ["DASH_QUOTE_SUBTITLE"] = DashboardTexte.QuoteUntertitel, ["DASH_QUOTE_EMPTY"] = DashboardTexte.QuoteLeer,
            ["DASH_SIDEBAR_BAR"] = DashboardTexte.SeitenleisteBalken, ["LEARN_CHART_SERIES"] = DashboardTexte.VerlaufSerie,
        };
        Assert.Equal(konstanten.EnumerateObject().Select(p => p.Name).Order(), dash.Keys.Order());
        foreach (var p in konstanten.EnumerateObject())
        {
            Assert.Equal(p.Value.GetString(), dash[p.Name]);
        }
        var karten = new Dictionary<string, string>
        {
            ["heute_titel"] = DashboardTexte.HeuteTitel, ["heute_unter"] = DashboardTexte.HeuteUnter,
            ["wiederholen"] = DashboardTexte.Wiederholen, ["verlauf_titel"] = DashboardTexte.VerlaufTitel,
            ["verlauf_unter"] = DashboardTexte.VerlaufUnter, ["abdeckung_titel"] = DashboardTexte.AbdeckungTitel,
            ["abdeckung_unter"] = DashboardTexte.AbdeckungUnter, ["fach_titel"] = DashboardTexte.FachTitel,
            ["fach_unter"] = DashboardTexte.FachUnter, ["fach_detail"] = DashboardTexte.FachDetail,
            ["fach_detail_leer"] = DashboardTexte.FachDetailLeer, ["aktivitaeten_titel"] = DashboardTexte.AktivitaetenTitel,
            ["aktivitaeten_unter"] = DashboardTexte.AktivitaetenUnter, ["aktivitaeten_leer_pc"] = DashboardTexte.AktivitaetenLeerPc,
            ["aktivitaeten_leer_handy"] = DashboardTexte.AktivitaetenLeerHandy, ["kalender_titel"] = DashboardTexte.KalenderTitel,
            ["kalender_unter"] = DashboardTexte.KalenderUnter, ["ap1_titel"] = DashboardTexte.Ap1Titel,
            ["ap1_unter"] = DashboardTexte.Ap1Unter, ["ap2_titel"] = DashboardTexte.Ap2Titel, ["ap2_unter"] = DashboardTexte.Ap2Unter,
            ["reinzoom_titel"] = DashboardTexte.ReinzoomTitel, ["reinzoom_unter"] = DashboardTexte.ReinzoomUnter,
            ["reinzoom_aktivitaet"] = DashboardTexte.ReinzoomAktivitaet, ["reinzoom_wissen"] = DashboardTexte.ReinzoomWissen,
            ["reinzoom_schliessen"] = DashboardTexte.ReinzoomSchliessen, ["reinzoom_notiz"] = DashboardTexte.ReinzoomNotiz,
            ["reinzoom_leer"] = DashboardTexte.ReinzoomLeer, ["banner_ohne_name"] = DashboardTexte.BannerOhneName,
            ["lernserie_pc"] = DashboardTexte.LernseriePc, ["lernserie_handy"] = DashboardTexte.LernserieHandy,
        };
        var gesehen = new HashSet<string>();
        foreach (var geraet in new[] { "pc", "handy" })
        {
            foreach (var p in G13.GetProperty("kartentexte").GetProperty(geraet).EnumerateObject())
            {
                Assert.Equal(p.Value.GetString(), karten[p.Name]);
                gesehen.Add(p.Name);
            }
        }
        Assert.Equal(karten.Keys.Order(), gesehen.Order());
        Assert.Equal(DashboardWerte.Tage, G13.GetProperty("tage").GetInt32());
        Assert.Equal(DashboardWerte.AnzahlAktivitaeten, G13.GetProperty("aktivitaeten").GetInt32());
    }

    [Fact]
    public void HilfsrechnungenWiePython()
    {
        foreach (var paar in G13.GetProperty("runden").EnumerateArray())
        {
            Assert.Equal(paar[1].GetDouble(), PyText.Runde1(paar[0].GetDouble()));
        }
        foreach (var paar in G13.GetProperty("float_text").EnumerateArray())
        {
            Assert.Equal(paar[1].GetString(), PyText.Str(paar[0].GetDouble()));
        }
        foreach (var paar in G13.GetProperty("letzte_worte").EnumerateArray())
        {
            Assert.Equal(paar[1].GetString(), DashboardTexte.LetzteWorteZusammen(paar[0].GetString()!));
        }
    }
}
