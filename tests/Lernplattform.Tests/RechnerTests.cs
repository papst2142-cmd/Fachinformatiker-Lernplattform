using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 3, R3: RAID, Bildschirm und USV genau wie Python v0.63.2 (Vergleichswerte G26,
/// werkzeuge/vergleich/erzeuge_rechner.py). Kontrollfehler A1: RAID 10 mit ungerader
/// Plattenzahl angenommen, fps "23.976" als 23,976 gelesen, USV-Laufzeit ohne Alterung,
/// Bruch als Kommazahl statt "1/6".
/// </summary>
public class RechnerTests
{
    private static readonly JsonElement G26 = VergleichsHilfe.Lade("rechner.json");

    private static string Versuch(Func<string> aufruf) => Abweichungen.Versuch(aufruf);

    private static string F(double wert) => "f:" + BitConverter.DoubleToInt64Bits(wert).ToString(CultureInfo.InvariantCulture);

    private static double Zahl(JsonElement e) =>
        BitConverter.Int64BitsToDouble(long.Parse(e.GetString()!, CultureInfo.InvariantCulture));

    [Fact]
    public void RaidWiePython()
    {
        var a = new Abweichungen();
        foreach (var f in G26.GetProperty("raid").EnumerateArray())
        {
            string level = f[0].GetString()!, platten = f[1].GetString()!, groesse = f[2].GetString()!;
            a.Gleich(Abweichungen.Erwartet(f[3]), Versuch(() => Raid.Bericht(level, platten, groesse)),
                $"raid_report({level}, {JsonSerializer.Serialize(platten)}, {JsonSerializer.Serialize(groesse)})");
        }
        foreach (var f in G26.GetProperty("raid_werte").EnumerateArray())
        {
            string level = f[0].GetString()!;
            int platten = f[1].GetInt32();
            double groesse = Zahl(f[2]);
            string fall = $"raid_values({level}, {platten}, {groesse.ToString("R", CultureInfo.InvariantCulture)})";
            var werte = Raid.Werte(level, platten, groesse);
            a.Gleich(SubnettingTests.Kanon(f[3]), SubnettingTests.Kanon(werte is null ? null : new JsonObject
            {
                ["brutto"] = F(werte.Brutto),
                ["netto"] = F(werte.Netto),
                ["verlust"] = F(werte.Verlust),
                ["effizienz"] = F(werte.Effizienz),
                ["toleranz"] = werte.Toleranz,
            }), fall);
            a.Gleich(f[4].GetInt32().ToString(CultureInfo.InvariantCulture),
                Raid.MindestPlatten(level).ToString(CultureInfo.InvariantCulture), fall + " raid_min_disks");
            a.Gleich(f[5].GetString(), Raid.EingabeProblem(level, platten), fall + " raid_input_problem");
            if (werte is not null && f[6].ValueKind != JsonValueKind.Null)
            {
                a.Gleich(Abweichungen.Liste(f[6]), Abweichungen.Liste(Raid.ToleranzZeilen(level, platten, werte.Toleranz)),
                    fall + " raid_tolerance_lines");
            }
        }
        a.Pruefen(2000);
    }

    [Fact]
    public void BildschirmWiePython()
    {
        var a = new Abweichungen();
        foreach (var f in G26.GetProperty("bildschirm").EnumerateArray())
        {
            string b = f[0].GetString()!, h = f[1].GetString()!, t = f[2].GetString()!, fps = f[3].GetString()!;
            a.Gleich(Abweichungen.Erwartet(f[4]), Versuch(() => Bildschirm.Bericht(b, h, t, fps)),
                "screen_report" + JsonSerializer.Serialize(new[] { b, h, t, fps }));
        }
        a.Pruefen(400);
    }

    private static JsonObject BildKnoten(UsvBild bild) => new()
    {
        ["last"] = bild.Last,
        ["last_detail"] = bild.LastDetail,
        ["usv"] = bild.Usv,
        ["usv_detail"] = bild.UsvDetail,
        ["akku"] = bild.Akku,
        ["akku_detail"] = bild.AkkuDetail,
        ["auslastung"] = bild.Auslastung is { } s ? F(s) : null,
        ["grenze"] = bild.Grenze,
        ["balken"] = new JsonArray([.. bild.Balken.Select(b => (JsonNode)new JsonObject
        {
            ["label"] = b.Beschriftung,
            ["minuten"] = F(b.Minuten),
            ["art"] = b.Art,
            ["anteil"] = F(b.Anteil),
            ["text"] = b.Text,
        })]),
        ["urteil"] = bild.Urteil,
    };

    [Fact]
    public void UsvWiePython()
    {
        var a = new Abweichungen();
        foreach (var f in G26.GetProperty("usv").EnumerateArray())
        {
            string modus = f.GetProperty("modus").GetString()!, einheit = f.GetProperty("einheit").GetString()!;
            var felder = f.GetProperty("felder").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString(), StringComparer.Ordinal);
            string fall = $"ups_calculate({modus}, {JsonSerializer.Serialize(felder)}, {einheit})";
            UsvErgebnis r;
            try
            {
                r = Usv.Berechnen(modus, felder, einheit);
            }
            catch (RechnerFehler fehler)
            {
                a.Gleich(f.TryGetProperty("fehler", out var fe) ? fe.GetString() : "<kein Fehler>", fehler.Message, fall);
                continue;
            }
            if (f.TryGetProperty("fehler", out var erwartet))
            {
                a.Gleich("Fehler: " + erwartet.GetString(), "kein Fehler", fall);
                continue;
            }
            a.Gleich(f.GetProperty("text").GetString(), r.Text, fall + " text");
            a.Gleich(SubnettingTests.Kanon(f.GetProperty("bild")), SubnettingTests.Kanon(BildKnoten(r.Bild)), fall + " bild");
            a.Gleich(f.GetProperty("passend").GetRawText(), r.Passend is { } p ? (p ? "true" : "false") : "null", fall + " passend");
            a.Gleich(f.GetProperty("vorlesen").GetString(), Usv.Vorlesetext(r.Bild), fall + " ups_picture_summary");
        }
        a.Pruefen(600);
    }

    [Fact]
    public void AufgabenWiePython()
    {
        var a = new Abweichungen();
        foreach (var f in G26.GetProperty("aufgaben").EnumerateArray())
        {
            int i = f[0].GetInt32();
            a.Gleich(f[1].GetString(), Usv.AufgabeText(i), $"ups_task_text({i})");
            a.Gleich(f[2].GetString(), Usv.AufgabeText(i, loesung: true), $"ups_task_text({i}, True)");
        }
        foreach (var f in G26.GetProperty("straenge").EnumerateArray())
        {
            a.Gleich(f[2].GetInt32().ToString(CultureInfo.InvariantCulture),
                Usv.StraengeNoetig(Zahl(f[0]), Zahl(f[1])).ToString(CultureInfo.InvariantCulture), "ups_strings_needed " + f.GetRawText());
        }
        a.Pruefen(40);
    }

    [Fact]
    public void RechenwegeWiePython()
    {
        var a = new Abweichungen();
        foreach (var p in G26.GetProperty("rechenwege").EnumerateObject())
        {
            var feld = typeof(RechnerTexte).GetField(p.Name, BindingFlags.Public | BindingFlags.Static);
            a.Gleich(p.Value.GetString(), feld?.GetValue(null) as string, p.Name);
        }
        a.Pruefen(10);
    }
}
