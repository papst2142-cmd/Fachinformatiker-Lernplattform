using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Rechner;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 3, R2: IP-Adressen und Subnetting genau wie Python v0.63.2 mit Python 3.12
/// (Vergleichswerte G25, werkzeuge/vergleich/erzeuge_subnetting.py). Jede Eingabe laeuft
/// durch alle drei Einstiege (Netzdaten, subnet_parse, Trainer-Pruefer). Kontrollfehler A1:
/// IPAddress.Parse, Mischform statt Hex in den Netzdaten, Zone abgelehnt, IPv6 ohne
/// Kuerzung der Nullfolge, Seite 2 ab Eintrag 8 statt 9, Aufteilen zaehlt alle Subnetze auf.
/// </summary>
public class SubnettingTests
{
    private static readonly JsonElement G25 = VergleichsHilfe.Lade("subnetting.json");

    private static string G(BigInteger wert) => wert.ToString(CultureInfo.InvariantCulture);

    private static JsonNode? G(BigInteger? wert) => wert is { } w ? JsonValue.Create(G(w)) : null;

    private static JsonNode? Gi(int? wert) => wert is { } w ? JsonValue.Create(w.ToString(CultureInfo.InvariantCulture)) : null;

    /// <summary>Gleiche Schreibweise fuer Python-JSON und C#-Knoten (Schluessel sortiert).</summary>
    internal static string Kanon(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", e.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => JsonSerializer.Serialize(p.Name) + ":" + Kanon(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", e.EnumerateArray().Select(Kanon)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(e.GetString()),
        _ => e.GetRawText(),
    };

    internal static string Kanon(JsonNode? knoten)
    {
        using var dok = JsonDocument.Parse(knoten?.ToJsonString() ?? "null");
        return Kanon(dok.RootElement);
    }

    private static string Erwartet(JsonElement e) =>
        e.TryGetProperty("fehler", out var f) ? "Fehler: " + f.GetString() : Kanon(e.GetProperty("wert"));

    private static string Versuch(Func<JsonNode?> aufruf)
    {
        try
        {
            return Kanon(aufruf());
        }
        catch (RechnerFehler fehler)
        {
            return "Fehler: " + fehler.Message;
        }
    }

    private static JsonNode? NetzInfo(IpNetz? netz) => netz is null ? null
        : new JsonArray(netz.Version, PythonIp.Text(netz), G(netz.Netzadresse.Wert), netz.Praefix);

    private static JsonNode? AdresseInfo(IpAdresse? adresse)
    {
        if (adresse is null)
        {
            return null;
        }
        string? lang;
        try
        {
            lang = PythonIp.Ausgeschrieben(adresse);
        }
        catch (FormatException)
        {
            lang = null;
        }
        return new JsonArray(adresse.Version, G(adresse.Wert), PythonIp.Text(adresse), lang);
    }

    private static JsonObject Gelesen(GelesenesNetz n) => new()
    {
        ["version"] = Gi(n.Version),
        ["breite"] = Gi(n.Breite),
        ["netz"] = G(n.Netz),
        ["praefix"] = Gi(n.Praefix),
        ["adresse"] = G(n.Adresse),
        ["hinweis"] = n.Hinweis,
    };

    private static JsonArray Texte(IEnumerable<string> texte) => [.. texte.Select(t => (JsonNode?)JsonValue.Create(t))];

    [Fact]
    public void KonstantenWiePython()
    {
        var a = new Abweichungen();
        var soll = G25.GetProperty("konstanten").GetProperty("IPV6_TYPES").EnumerateArray().ToList();
        a.Gleich(soll.Count.ToString(CultureInfo.InvariantCulture), Subnetting.V6Typen.Count.ToString(CultureInfo.InvariantCulture), "Anzahl IPV6_TYPES");
        for (int i = 0; i < Math.Min(soll.Count, Subnetting.V6Typen.Count); i++)
        {
            var (basis, laenge, text) = Subnetting.V6Typen[i];
            a.Gleich(Kanon(soll[i]), Kanon(new JsonArray(PythonIp.Hex(basis), laenge, text)), $"IPV6_TYPES[{i}]");
        }
        a.Pruefen(10);
    }

    [Fact]
    public void AdressenWiePython()
    {
        var a = new Abweichungen();
        foreach (var fall in G25.GetProperty("adressen").EnumerateArray())
        {
            string text = fall.GetProperty("text").GetString()!;
            string f = JsonSerializer.Serialize(text);
            a.Gleich(Kanon(fall.GetProperty("netz")), Kanon(NetzInfo(PythonIp.Netz(PythonZeichen.Strip(text), streng: false))), f + " netz");
            a.Gleich(Erwartet(fall.GetProperty("bericht")), Versuch(() => Subnetting.Bericht(text)), f + " bericht");
            a.Gleich(Erwartet(fall.GetProperty("details")), Versuch(() =>
                new JsonArray([.. Subnetting.V6Details(text).Select(p => (JsonNode)new JsonArray(p.Beschriftung, p.Wert))])), f + " details");
            var nd = fall.GetProperty("netzdaten");
            a.Gleich(Erwartet(nd[0]), Versuch(() => Subnetting.Netzdaten(text, "ipv4")), f + " netzdaten ipv4");
            a.Gleich(Erwartet(nd[1]), Versuch(() => Subnetting.Netzdaten(text, "ipv6")), f + " netzdaten ipv6");
            int?[] versionen = [null, 4, 6];
            for (int i = 0; i < 3; i++)
            {
                int? v = versionen[i];
                a.Gleich(Erwartet(fall.GetProperty("parse")[i]), Versuch(() => Gelesen(Subnetting.Lesen(text, v))), $"{f} parse {v}");
            }
            a.Gleich(Kanon(fall.GetProperty("adresse")), Kanon(AdresseInfo(PythonIp.Adresse(text))), f + " ip_address");
            a.Gleich(Kanon(fall.GetProperty("streng")), Kanon(NetzInfo(PythonIp.Netz(text, streng: true))), f + " ip_network strict");
            var s = PythonIp.Schnittstelle(text);
            a.Gleich(Kanon(fall.GetProperty("schnittstelle")), Kanon(s is not { } sn ? null
                : new JsonArray(sn.Ip.Version, PythonIp.Text(sn.Ip) + "/" + sn.Netz.Praefix.ToString(CultureInfo.InvariantCulture),
                    G(sn.Ip.Wert), NetzInfo(sn.Netz))), f + " ip_interface");
        }
        a.Pruefen(1000 * 11);
    }

    [Fact]
    public void TypenUndAnzahlWiePython()
    {
        var a = new Abweichungen();
        foreach (var t in G25.GetProperty("typen").EnumerateArray())
        {
            var wert = BigInteger.Parse(t[0].GetString()!, CultureInfo.InvariantCulture);
            int praefix = t[1].GetInt32();
            a.Gleich(t[2].GetString(), Subnetting.V6Typ(wert, praefix), $"ipv6_type({t[0]}, {praefix})");
        }
        foreach (var t in G25.GetProperty("anzahl").EnumerateArray())
        {
            string text = t[0].GetString()!;
            a.Gleich(Erwartet(t[1]), Versuch(() => G(Subnetting.Anzahl(text))), "subnet_count " + JsonSerializer.Serialize(text));
        }
        a.Pruefen(500);
    }

    [Fact]
    public void AufteilenWiePython()
    {
        var a = new Abweichungen();
        foreach (var fall in G25.GetProperty("aufteilen").EnumerateArray())
        {
            string net = fall.GetProperty("net").GetString()!;
            string count = fall.GetProperty("count").GetString()!;
            var ve = fall.GetProperty("version");
            int? version = ve.ValueKind == JsonValueKind.Null ? null : ve.GetInt32();
            string f = $"split_network({JsonSerializer.Serialize(net)}, {JsonSerializer.Serialize(count)}, {version})";
            Aufteilung r;
            try
            {
                r = Subnetting.Aufteilen(net, count, version);
            }
            catch (RechnerFehler fehler)
            {
                a.Gleich(fall.TryGetProperty("fehler", out var fe) ? fe.GetString() : "<kein Fehler>", fehler.Message, f);
                continue;
            }
            if (!fall.TryGetProperty("kennwerte", out var kenn))
            {
                a.Gleich("Fehler: " + fall.GetProperty("fehler").GetString(), "kein Fehler", f);
                continue;
            }
            var ist = Gelesen(r.Basis);
            ist["gewuenscht"] = G(r.Gewuenscht);
            ist["k"] = Gi(r.K);
            ist["neu"] = Gi(r.Neu);
            ist["anzahl"] = G(r.Anzahl);
            ist["groesse"] = G(r.Groesse);
            ist["seiten"] = G(r.Seiten);
            if (r.Version == 4)
            {
                ist["maske"] = G(r.Maske); // Python setzt "maske" nur bei IPv4
            }
            ist["hosts"] = G(r.Hosts);
            ist["nibble"] = Gi(r.Nibble);
            ist["nibble_anzahl"] = G(r.NibbleAnzahl);
            ist["hinweise"] = Texte(r.Hinweise);
            a.Gleich(Kanon(kenn), Kanon(ist), f + " Kennwerte");
            foreach (var seite in fall.GetProperty("seiten").EnumerateObject())
            {
                var nr = BigInteger.Parse(seite.Name, CultureInfo.InvariantCulture);
                a.Gleich(seite.Value[0].GetString(), Subnetting.AufteilenBericht(r, nr), $"{f} split_report Seite {nr}");
                a.Gleich(seite.Value[1].GetString(), Subnetting.AufteilenBericht(r, nr, schmal: true), $"{f} split_report Seite {nr} schmal");
            }
            var weg = fall.GetProperty("rechenweg");
            a.Gleich(weg[0].GetString(), Rechenweg.Aufteilen(r), f + " split_explain");
            a.Gleich(weg[1].GetString(), Rechenweg.Aufteilen(r, schmal: true), f + " split_explain schmal");
        }
        a.Pruefen(400);
    }

    [Fact]
    public void HostbedarfWiePython()
    {
        var a = new Abweichungen();
        foreach (var z in G25.GetProperty("zeilen").EnumerateArray())
        {
            string text = z[0].GetString()!;
            a.Gleich(Erwartet(z[1]), Versuch(() => new JsonArray([.. Subnetting.BedarfLesen(text)
                .Select(p => (JsonNode)new JsonArray(p.Name, G(p.Bedarf)))])), "read_host_lines " + JsonSerializer.Serialize(text));
        }
        foreach (var fall in G25.GetProperty("bedarf").EnumerateArray())
        {
            string net = fall.GetProperty("net").GetString()!;
            string needs = fall.GetProperty("needs").GetString()!;
            string f = $"subnet_by_hosts({JsonSerializer.Serialize(net)}, {JsonSerializer.Serialize(needs)})";
            Hostbedarf r;
            try
            {
                r = Subnetting.NachBedarf(net, needs, 4);
            }
            catch (RechnerFehler fehler)
            {
                a.Gleich(fall.TryGetProperty("fehler", out var fe) ? fe.GetString() : "<kein Fehler>", fehler.Message, f);
                continue;
            }
            if (!fall.TryGetProperty("kennwerte", out var kenn))
            {
                a.Gleich("Fehler: " + fall.GetProperty("fehler").GetString(), "kein Fehler", f);
                continue;
            }
            var ist = Gelesen(r.Basis);
            ist["bedarfe"] = new JsonArray([.. r.Subnetze.Select(s => (JsonNode)new JsonArray(s.Name, G(s.Bedarf)))]);
            ist["subnetze"] = new JsonArray([.. r.Subnetze.Select(s =>
            {
                var o = new JsonObject
                {
                    ["name"] = s.Name,
                    ["bedarf"] = G(s.Bedarf),
                    ["h"] = Gi(s.H),
                    ["praefix"] = Gi(s.Praefix),
                    ["groesse"] = G(s.Groesse),
                    ["maske"] = G(s.Maske),
                };
                if (r.Passt)
                {
                    o["netz"] = G(s.Netz);
                    o["erste"] = G(s.Erste);
                    o["letzte"] = G(s.Letzte);
                    o["broadcast"] = G(s.Broadcast);
                }
                return (JsonNode)o;
            })]);
            ist["reihenfolge"] = new JsonArray([.. r.Reihenfolge.Select(i => (JsonNode)i)]);
            ist["kapazitaet"] = G(r.Kapazitaet);
            ist["summe"] = G(r.Summe);
            ist["passt"] = r.Passt;
            ist["fehlt"] = G(r.Fehlt);
            ist["noetig"] = Gi(r.Noetig);
            ist["meldung"] = r.Meldung;
            ist["rest"] = G(r.Rest);
            ist["rest_von"] = G(r.RestVon);
            ist["rest_bis"] = G(r.RestBis);
            ist["bloecke"] = new JsonArray([.. r.Bloecke.Select(b => (JsonNode)new JsonArray(G(b.Netz), b.Praefix))]);
            ist["hinweise"] = Texte(r.Hinweise);
            a.Gleich(Kanon(kenn), Kanon(ist), f + " Kennwerte");
            var bericht = fall.GetProperty("bericht");
            a.Gleich(bericht[0].GetString(), Subnetting.BedarfBericht(r), f + " hosts_report");
            a.Gleich(bericht[1].GetString(), Subnetting.BedarfBericht(r, schmal: true), f + " hosts_report schmal");
            var weg = fall.GetProperty("rechenweg");
            a.Gleich(weg[0].GetString(), Rechenweg.Bedarf(r), f + " hosts_explain");
            a.Gleich(weg[1].GetString(), Rechenweg.Bedarf(r, schmal: true), f + " hosts_explain schmal");
        }
        a.Pruefen(100);
    }

    /// <summary>
    /// 2^64 Subnetze: Aufteilen, letzte Seite und Rechenweg sofort (nie alle Subnetze aufzaehlen).
    /// Die Arbeit laeuft nebenher, damit ein Aufzaehlen den Test rot macht statt ihn anzuhalten.
    /// </summary>
    [Fact]
    public async Task GrosseAufteilungSofort()
    {
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var arbeit = Task.Run(() =>
        {
            foreach (var (net, count) in new[] { ("::/0", "18446744073709551616"), ("2001:db8::/32", "18446744073709551617"), ("0.0.0.0/0", "4294967296") })
            {
                var r = Subnetting.Aufteilen(net, count);
                foreach (var seite in new[] { BigInteger.One, r.Seiten / 2 + 1, r.Seiten })
                {
                    Assert.NotEmpty(Subnetting.AufteilenBericht(r, seite));
                    Assert.NotEmpty(Subnetting.AufteilenBericht(r, seite, schmal: true));
                }
                Assert.NotEmpty(Rechenweg.Aufteilen(r));
            }
        }, TestContext.Current.CancellationToken);
        var fertig = await Task.WhenAny(arbeit, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.True(fertig == arbeit, "Aufteilen mit 2^64 Subnetzen nach 10 s nicht fertig");
        await arbeit;
        Assert.True(uhr.ElapsedMilliseconds < 400, $"Aufteilen mit 2^64 Subnetzen dauerte {uhr.ElapsedMilliseconds} ms (Grenze 400 ms)");
    }
}
