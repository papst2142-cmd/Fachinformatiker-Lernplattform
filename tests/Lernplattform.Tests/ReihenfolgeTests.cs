using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Zufall;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L1 Teil "Status und Reihenfolge": Fragenstatus, bevorzugte Reihenfolge
/// (ohne Zufall, mit Zufall, mit Rahmenplan-Gewichtung), Punkte, Gewichte, Pruefungsphase
/// exakt wie Python (G16, fisi_core / fisi_rahmenplan v0.63.2).
/// </summary>
public class ReihenfolgeTests
{
    private static readonly JsonElement G16 = VergleichsHilfe.Lade("reihenfolge.json");
    private static readonly Lerninhalte Inhalte = Lerninhalte.Laden(Pfade.Inhalte);
    private static readonly RahmenplanGewicht Plan = new(Inhalte);

    private static DateOnly Tag(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static List<string> Liste(JsonElement werte) => werte.EnumerateArray().Select(w => w.GetString()!).ToList();

    private static RahmenplanWerte Werte(int nummer) =>
        RahmenplanWerte.Aus(JsonNode.Parse(G16.GetProperty("einstellungen_eingaben")[nummer].GetRawText())!.AsObject(), Plan);

    private static StatusBuch Buch(string quelle, IEnumerable<(string Schluessel, List<(string Zeit, bool? Wert)> Antworten)> faelle)
    {
        var buch = new Antwortbuch();
        foreach (var (schluessel, antworten) in faelle)
        {
            foreach (var (zeit, wert) in antworten)
            {
                buch.Hinzufuegen(quelle, schluessel, zeit, wert);
            }
        }
        return new StatusBuch(buch);
    }

    private static bool? Bool(JsonElement wert) => wert.ValueKind == JsonValueKind.Null ? null : wert.GetBoolean();

    [Fact]
    public void StatusWiePython()
    {
        var anzahl = 0;
        foreach (var fall in G16.GetProperty("status").EnumerateArray())
        {
            var werte = fall.GetProperty("werte").EnumerateArray().Select(Bool).ToList();
            var erwartet = fall.GetProperty("status");
            Assert.Equal((erwartet[0].GetString()!, erwartet[1].GetString()!), FragenStatus.Bestimmen(werte));
            var buch = Buch(Quellen.Quiz, [("x", werte.Select((w, i) => ($"2026-10-01 10:00:{i:00}", w)).ToList())]);
            Assert.Equal(fall.GetProperty("folge").GetInt32(), buch.Folge(Quellen.Quiz, "x"));
            Assert.Equal(fall.GetProperty("beruehrt").GetBoolean(), buch.Beruehrt(Quellen.Quiz, "x"));
            anzahl++;
        }
        Assert.Equal(364, anzahl);
    }

    [Fact]
    public void ReihenfolgeWiePython()
    {
        foreach (var fall in G16.GetProperty("reihenfolge").EnumerateArray())
        {
            var quelle = fall.GetProperty("quelle").GetString()!;
            var schluessel = Liste(fall.GetProperty("schluessel"));
            var heute = Tag(fall.GetProperty("heute").GetString()!);
            var buch = Buch(quelle, fall.GetProperty("antworten").EnumerateArray().Select(a => (a[0].GetString()!,
                a[1].EnumerateArray().Select(e => (e[0].GetString()!, Bool(e[1]))).ToList())));
            var startwert = fall.GetProperty("startwert").GetInt64();
            Assert.Equal(Liste(fall.GetProperty("ohne_zufall")), buch.BevorzugteReihenfolge(quelle, schluessel));
            Assert.Equal(Liste(fall.GetProperty("mit_zufall")), buch.BevorzugteReihenfolge(quelle, schluessel, new PythonZufall(startwert)));
            var gewichtet = Plan.NeuReihenfolge(quelle, RahmenplanWerte.Vorgabe, heute);
            Assert.Equal(Liste(fall.GetProperty("gewichtet")), buch.BevorzugteReihenfolge(quelle, schluessel, null, gewichtet));
            Assert.Equal(Liste(fall.GetProperty("zufall_und_gewichtet")),
                buch.BevorzugteReihenfolge(quelle, schluessel, new PythonZufall(startwert), gewichtet));
            var zaehlen = buch.Zaehlen(quelle, schluessel);
            foreach (var z in fall.GetProperty("zaehlen").EnumerateObject())
            {
                Assert.Equal(z.Value.GetInt32(), zaehlen[z.Name]);
            }
            Assert.Equal(fall.GetProperty("zaehlen").EnumerateObject().Count(), zaehlen.Count);
            foreach (var f in fall.GetProperty("filter").EnumerateObject())
            {
                Assert.Equal(Liste(f.Value), schluessel.Where(k => buch.Passt(quelle, k, f.Name)).ToList());
            }
        }
    }

    [Fact]
    public void VorgabeGleichLeereEinstellungen()
    {
        Assert.Equal(RahmenplanWerte.Vorgabe, Werte(0));
    }

    [Fact]
    public void PunkteWiePython()
    {
        var punkte = G16.GetProperty("punkte");
        Assert.Equal(punkte.GetProperty("karte").GetArrayLength(), Inhalte.Karteikarten.Count);
        Assert.Equal(punkte.GetProperty("quiz").GetArrayLength(), Inhalte.Quizfragen.Count);
        foreach (var (quelle, liste, json) in new[] { (Quellen.Karte, Inhalte.Karteikarten, "karte"), (Quellen.Quiz, Inhalte.Quizfragen, "quiz") })
        {
            var i = 0;
            foreach (var erwartet in punkte.GetProperty(json).EnumerateArray())
            {
                Assert.True(Liste(erwartet).SequenceEqual(Plan.Punkte(quelle, liste[i].Frage!)), $"{json} {i}");
                i++;
            }
        }
    }

    [Fact]
    public void EinstellungenWiePython()
    {
        var nummer = 0;
        foreach (var fall in G16.GetProperty("einstellungen").EnumerateArray())
        {
            var w = Werte(nummer++);
            var e = fall.GetProperty("werte");
            Assert.Equal(e.GetProperty("rp_abschnitt_b").GetBoolean(), w.AbschnittB);
            Assert.Equal(e.GetProperty("rp_abschnitt_d").GetBoolean(), w.AbschnittD);
            Assert.Equal(e.GetProperty("rp_abschnitt_e").GetBoolean(), w.AbschnittE);
            Assert.Equal(e.GetProperty("rp_gewichtung").GetBoolean(), w.Gewichtung);
            Assert.Equal(e.GetProperty("rp_lernfeld").GetString(), w.Lernfeld);
            Assert.Equal(e.GetProperty("rp_termin_ap1").GetString(), w.TerminAp1);
            Assert.Equal(e.GetProperty("rp_termin_ap2").GetString(), w.TerminAp2);
        }
    }

    private static void GewichteGleich(JsonElement erwartet, RahmenplanWerte werte, DateOnly heute, int schritt)
    {
        foreach (var (quelle, liste, json) in new[] { (Quellen.Karte, Inhalte.Karteikarten, "karte"), (Quellen.Quiz, Inhalte.Quizfragen, "quiz") })
        {
            var i = 0;
            foreach (var g in erwartet.GetProperty(json).EnumerateArray())
            {
                var ist = Plan.Gewicht(quelle, liste[i * schritt].Frage!, werte, heute);
                Assert.True(BitConverter.DoubleToInt64Bits(g.GetDouble()) == BitConverter.DoubleToInt64Bits(ist),
                    $"{json} {i * schritt}: Python {g.GetDouble():R}, C# {ist:R}");
                i++;
            }
        }
    }

    [Fact]
    public void GewichteWiePython()
    {
        var gewichte = G16.GetProperty("gewichte");
        GewichteGleich(gewichte.GetProperty("vorgabe_2026-10-20"), RahmenplanWerte.Vorgabe, new DateOnly(2026, 10, 20), 1);
        foreach (var probe in gewichte.GetProperty("stichproben").EnumerateArray())
        {
            GewichteGleich(probe, Werte(probe.GetProperty("einstellungen").GetInt32()), Tag(probe.GetProperty("heute").GetString()!), 7);
        }
    }

    [Fact]
    public void GewichteteReihenfolgeWiePython()
    {
        foreach (var fall in G16.GetProperty("gewichtet").EnumerateArray())
        {
            var quelle = fall.GetProperty("quelle").GetString()!;
            var heute = Tag(fall.GetProperty("heute").GetString()!);
            var schluessel = Liste(fall.GetProperty("schluessel"));
            var i = 0;
            foreach (var z in fall.GetProperty("zufall").EnumerateArray())
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(z.GetDouble()),
                    BitConverter.DoubleToInt64Bits(RahmenplanGewicht.Tageszufall(quelle, schluessel[i++], heute)));
            }
            Assert.Equal(Liste(fall.GetProperty("ergebnis")),
                Plan.GewichteteReihenfolge(quelle, schluessel, Werte(fall.GetProperty("einstellungen").GetInt32()), heute));
        }
    }

    [Fact]
    public void PruefungsphaseWiePython()
    {
        int[] monate = [0, 1, 6, 11, 12, 13, 25];
        foreach (var fall in G16.GetProperty("monate").EnumerateArray())
        {
            var tag = Tag(fall.GetProperty("tag").GetString()!);
            Assert.Equal(Liste(fall.GetProperty("monate_vorher")),
                monate.Select(m => RahmenplanGewicht.MonateVorher(tag, m).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList());
        }
        foreach (var fall in G16.GetProperty("pruefungsphase").EnumerateArray())
        {
            Assert.Equal(fall[2].ValueKind == JsonValueKind.Null ? null : fall[2].GetString(),
                RahmenplanGewicht.Pruefungsphase(Werte(fall[0].GetInt32()), Tag(fall[1].GetString()!)));
        }
    }

    /// <summary>Kontrollfehler: Math.Pow statt PythonPow in der gewichteten Reihenfolge wuerde Bits aendern koennen;
    /// hier wird gezeigt, dass schon ein vertauschtes Gewicht (1/w statt w) die Reihenfolge veraendert.</summary>
    [Fact]
    public void KontrollfehlerFalscheGewichtungFaellt()
    {
        var fall = G16.GetProperty("gewichtet")[0];
        var quelle = fall.GetProperty("quelle").GetString()!;
        var heute = Tag(fall.GetProperty("heute").GetString()!);
        var schluessel = Liste(fall.GetProperty("schluessel"));
        var werte = Werte(fall.GetProperty("einstellungen").GetInt32());
        var falsch = schluessel.OrderByDescending(k => Lernplattform.Core.Mathe.PythonPow.Pow(
            RahmenplanGewicht.Tageszufall(quelle, k, heute), Plan.Gewicht(quelle, k, werte, heute))).ToList();
        Assert.NotEqual(Liste(fall.GetProperty("ergebnis")), falsch);
    }
}
