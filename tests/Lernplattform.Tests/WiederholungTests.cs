using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L1 Teil "Wiederholung": Stufen, Faelligkeit, Altbestand und Tagesplan exakt
/// wie Python (G15, fisi_lernen v0.63.2). Dazu: Dashboard und Wiederholungsplan liefern
/// dieselbe Faelligkeit (DashboardWerte bleibt unveraendert).
/// </summary>
public class WiederholungTests
{
    private static readonly JsonElement G15 = VergleichsHilfe.Lade("wiederholung.json");
    private static readonly Lerninhalte Inhalte = Lerninhalte.Laden(Pfade.Inhalte);
    private static readonly DashboardInhalte DashInhalte = new(Inhalte);

    private static DateOnly Tag(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? Iso(DateOnly? tag) => tag?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static List<Antwort> Antworten(JsonElement eintraege) =>
        eintraege.EnumerateArray().Select(e => new Antwort(e[0].GetString(), e[1].ValueKind == JsonValueKind.Null ? null : e[1].GetBoolean())).ToList();

    [Fact]
    public void KonstantenWiePython()
    {
        var k = G15.GetProperty("konstanten");
        Assert.Equal(k.GetProperty("abstaende").EnumerateArray().Select(a => a.GetInt32()), Wiederholung.Abstaende);
        Assert.Equal(k.GetProperty("start").GetString(), Iso(Wiederholung.Start));
        Assert.Equal(Wiederholung.AltbestandJeTag, k.GetProperty("altbestand_je_tag").GetInt32());
        Assert.Equal(Wiederholung.SitzungMax, k.GetProperty("sitzung_max").GetInt32());
    }

    [Fact]
    public void ZustandWiePython()
    {
        var anzahl = 0;
        foreach (var fall in G15.GetProperty("zustand").EnumerateArray())
        {
            var name = fall.GetProperty("name").GetString();
            var antworten = Antworten(fall.GetProperty("eintraege"));
            var (stufe, faellig) = Wiederholung.Zustand(antworten);
            Assert.True(fall.GetProperty("stufe").GetInt32() == stufe, name);
            Assert.True(fall.GetProperty("faellig").GetString() == Iso(faellig), name);
            var info = Wiederholung.Info(antworten, Wiederholung.Start);
            var erwartet = fall.GetProperty("info");
            if (erwartet.ValueKind == JsonValueKind.Null)
            {
                Assert.True(info is null, name);
            }
            else
            {
                Assert.True(info is not null, name);
                Assert.Equal(Normal(erwartet), JsonSerializer.Serialize(new object?[]
                    { info!.Stufe, Iso(info.Faellig), info.Alt, Iso(info.ErsterNeuerTag) }));
            }
            anzahl++;
        }
        Assert.Equal(73, anzahl);
    }

    /// <summary>Werte eines Plans in derselben Form wie plan_werte() im Erzeuger.</summary>
    private static string Werte(Wiederholungsplan plan)
    {
        var stufen = plan.Zustaende.Select(z => new object?[] { z.Key.Quelle, z.Key.Schluessel, z.Value.Stufe, Iso(z.Value.Faellig) }).ToList();
        stufen.Sort((a, b) =>
        {
            var d = VergleichsHilfe.Codepunkte((string)a[0]!, (string)b[0]!);
            return d != 0 ? d : VergleichsHilfe.Codepunkte((string)a[1]!, (string)b[1]!);
        });
        var werte = new JsonObject
        {
            ["karte"] = new JsonArray([.. plan.Faellig(Quellen.Karte).Select(s => (JsonNode?)s)]),
            ["quiz"] = new JsonArray([.. plan.Faellig(Quellen.Quiz).Select(s => (JsonNode?)s)]),
            ["anzahl"] = plan.Anzahl(),
            ["sitzung_karte"] = new JsonArray([.. plan.Sitzung(Quellen.Karte).Select(s => (JsonNode?)s)]),
            ["sitzung_quiz"] = new JsonArray([.. plan.Sitzung(Quellen.Quiz).Select(s => (JsonNode?)s)]),
            ["altbestand_wartet"] = plan.AltbestandWartet,
            ["erste_quelle"] = plan.ErsteQuelle(),
            ["text"] = plan.Text,
            ["stufen"] = JsonSerializer.SerializeToNode(stufen),
        };
        return werte.ToJsonString();
    }

    private static string Normal(JsonElement wert) => JsonNode.Parse(wert.GetRawText())!.ToJsonString();

    [Fact]
    public void PlaeneWiePython()
    {
        foreach (var fall in G15.GetProperty("plaene").EnumerateArray())
        {
            var buch = new Antwortbuch();
            var schluessel = new HashSet<string>(StringComparer.Ordinal);
            foreach (var eintrag in fall.GetProperty("antworten").EnumerateArray())
            {
                var quelle = eintrag[0].GetString()!;
                var s = eintrag[1].GetString()!;
                schluessel.Add(s);
                foreach (var a in eintrag[2].EnumerateArray())
                {
                    buch.Hinzufuegen(quelle, s, a[0].GetString(), a[1].ValueKind == JsonValueKind.Null ? null : a[1].GetBoolean());
                }
            }
            var gueltig = new Dictionary<string, IReadOnlySet<string>> { [Quellen.Karte] = schluessel, [Quellen.Quiz] = schluessel };
            var status = new StatusBuch(buch);
            var plan = new Wiederholungsplan(status, Tag(fall.GetProperty("heute").GetString()!), gueltig);
            Assert.Equal(Normal(fall.GetProperty("werte")), Werte(plan));
        }
    }

    /// <summary>Datenbank "voll" (testdaten.db) oder "gemischt" (Zeilen aus G15) im Ordner.</summary>
    internal static string BaueDatenbank(string name, string ordner)
    {
        var pfad = Path.Combine(ordner, name + ".db");
        if (name == "voll")
        {
            File.Copy(Pfade.Vergleich("testdaten.db"), pfad);
            return pfad;
        }
        _ = new Lernstand(pfad);
        using var db = VergleichsHilfe.Oeffnen(pfad);
        using var tx = db.BeginTransaction();
        VergleichsHilfe.FuelleTabellen(db, G15.GetProperty("zeilen").GetProperty(name));
        tx.Commit();
        return pfad;
    }

    [Fact]
    public void DatenbankenWiePythonUndGleichWieDashboard()
    {
        using var ordner = new TempOrdner();
        var pfade = new Dictionary<string, string>();
        var anzahl = 0;
        foreach (var fall in G15.GetProperty("datenbanken").EnumerateArray())
        {
            var name = fall.GetProperty("db").GetString()!;
            if (!pfade.TryGetValue(name, out var pfad))
            {
                pfade[name] = pfad = BaueDatenbank(name, ordner.Pfad);
            }
            var heute = Tag(fall.GetProperty("heute").GetString()!);
            var status = new StatusBuch(Antwortbuch.Lesen(new Lernstand(pfad)));
            var plan = Wiederholungsplan.Aus(status, heute, Inhalte);
            Assert.True(Normal(fall.GetProperty("werte")) == Werte(plan), name + " " + Iso(heute));

            // Gleiche Faelligkeit im Dashboard (eigene, nur lesende Rechnung in DashboardWerte)
            var dash = DashboardWerte.Lesen(pfad, heute, DashInhalte, Einstellungen.Ziel(new JsonObject())).Faellig;
            Assert.Equal(plan.Anzahl(Quellen.Karte), dash.Karten);
            Assert.Equal(plan.Anzahl(Quellen.Quiz), dash.Fragen);
            Assert.Equal(plan.Text, dash.Text);
            Assert.Equal(plan.AltbestandWartet, dash.AltbestandWartet);
            Assert.Equal(plan.ErsteQuelle(), dash.ErsteQuelle);
            anzahl++;
        }
        Assert.Equal(18, anzahl);
    }

    /// <summary>Kontrollfehler: ein anderer Abstand (7 statt 8 Tage usw.) faellt im Vergleich auf.</summary>
    [Fact]
    public void KontrollfehlerFalscherAbstandFaellt()
    {
        var fall = G15.GetProperty("zustand").EnumerateArray().First(f => f.GetProperty("name").GetString() == "intervalle_3");
        var (_, faellig) = Wiederholung.Zustand(Antworten(fall.GetProperty("eintraege")));
        Assert.Equal(fall.GetProperty("faellig").GetString(), Iso(faellig));
        Assert.NotEqual(fall.GetProperty("faellig").GetString(), Iso(faellig!.Value.AddDays(1)));
    }
}
