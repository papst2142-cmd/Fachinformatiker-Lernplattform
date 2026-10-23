using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 5 BE7 "Leistungsmessung" wie fisi_leistung.Recorder (G39, tests/Vergleich/leistungsmessung.json).
/// Kontrollfehler: Datei entsteht bei ausgeschaltetem Schalter, Schalter wandert in den Abgleich,
/// Komma statt Punkt in Zahlen (bzw. umgekehrt).
/// </summary>
public class LeistungsmessungTests
{
    private static readonly JsonElement G39 = VergleichsHilfe.Lade("leistungsmessung.json");

    private static Func<DateTime> Uhr()
    {
        var start = DateTime.Parse(G39.GetProperty("start").GetString()!, CultureInfo.InvariantCulture);
        var schritt = G39.GetProperty("schritt_us").GetInt64();
        var i = 0L;
        return () => start.AddTicks(schritt * 10 * i++);
    }

    private static Leistungsmessung Neu(string pfad, string version, string geraet, string toolkit, bool windows, bool aktiv = true,
        Func<bool, bool>? schalter = null) =>
        new(version, geraet, pfad, aktiv, toolkit, schalter, Uhr(), G39.GetProperty("system").GetString(),
            () => windows ? (100.25, 50.75) : (123.45, null), () => windows ? 1022.5 : 2047.5);

    [Fact]
    public void SpaltenUndGrenzenWiePython()
    {
        Assert.Equal(G39.GetProperty("spalten").EnumerateArray().Select(s => s.GetString()), Leistungsmessung.Spalten);
        Assert.Equal(Leistungsmessung.MaxBytes, G39.GetProperty("max_bytes").GetInt32());
        Assert.Equal(Leistungsmessung.KuerzenAuf, G39.GetProperty("trim_to").GetInt32());
        Assert.Equal(Leistungsmessung.PufferZeilen, G39.GetProperty("flush_lines").GetInt32());
        Assert.Equal(LeistungTexte.KeineDatei, G39.GetProperty("texte").GetProperty("state_none").GetString());
        Assert.Equal(LeistungTexte.Datei, G39.GetProperty("texte").GetProperty("state_file").GetString());
    }

    [Fact]
    public void ZahlenUndFelderWiePython()
    {
        var alt = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");   // Kontrollfehler: nie Punkt aus der Ortskultur
            foreach (var fall in G39.GetProperty("zahlen").EnumerateArray())
            {
                var wert = fall[0];
                if (wert.ValueKind == JsonValueKind.Null)
                {
                    Assert.Equal("", Leistungsmessung.Zahl((double?)null));
                    continue;
                }
                var text = wert.GetRawText();
                if (!text.Contains('.', StringComparison.Ordinal) && !text.Contains('e', StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Equal(fall[1].GetString(), Leistungsmessung.Zahl(wert.GetInt32()));
                    continue;
                }
                Assert.Equal(fall[1].GetString(), Leistungsmessung.Zahl(wert.GetDouble()));
                Assert.Equal(fall[2].GetString(), Leistungsmessung.Zahl(wert.GetDouble(), 0));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = alt;
        }
        foreach (var fall in G39.GetProperty("sauber").EnumerateArray())
        {
            if (fall[0].ValueKind == JsonValueKind.String)
            {
                Assert.Equal(fall[1].GetString(), Leistungsmessung.Sauber(fall[0].GetString()));
            }
        }
        Assert.Equal("", Leistungsmessung.Sauber(null));
    }

    [Fact]
    public void MessdateiWiePython()
    {
        var anzahl = 0;
        foreach (var fall in G39.GetProperty("faelle").EnumerateArray())
        {
            using var temp = new TempOrdner();
            var pfad = Path.Combine(temp.Pfad, Leistungsmessung.DateiName);
            if (fall.GetProperty("alt").ValueKind == JsonValueKind.String)
            {
                File.WriteAllText(pfad, fall.GetProperty("alt").GetString(), new UTF8Encoding(false));
            }
            var m = Neu(pfad, fall.GetProperty("version").GetString()!, fall.GetProperty("geraet").GetString()!,
                fall.GetProperty("toolkit").GetString()!, fall.GetProperty("windows").GetBoolean(), schalter: _ => true);
            m.StartVorgang = fall.GetProperty("start_task").GetString()!;
            var vorgang = "";
            m.Vorgangsquelle = () => vorgang;
            foreach (var aufruf in fall.GetProperty("aufrufe").EnumerateArray())
            {
                vorgang = aufruf[2].GetString()!;
                var ereignis = aufruf[0].GetString()!;
                if (ereignis is "AN" or "AUS")
                {
                    m.AktivSetzen(ereignis == "AN");
                    continue;
                }
                var a = aufruf[1];
                string Text(string n) => a.TryGetProperty(n, out var w) ? w.GetString()! : "";
                double? Kommazahl(string n) => a.TryGetProperty(n, out var w) ? w.GetDouble() : null;
                int? Ganz(string n) => a.TryGetProperty(n, out var w) ? w.GetInt32() : null;
                m.Aufzeichnen(ereignis, Text("von"), Text("nach"), Kommazahl("dauer_ms"), Kommazahl("max_ms"), Ganz("anzahl"),
                    Ganz("ereignisse"), Ganz("elemente"), Ganz("aufgaben"), !a.TryGetProperty("speicher", out var sp) || sp.GetBoolean(),
                    a.TryGetProperty("vorgang", out var vg) ? vg.GetString() : null, Ganz("messart"));
            }
            Assert.True(m.Schreiben());
            Assert.Equal(fall.GetProperty("inhalt").GetString(), File.ReadAllText(pfad), StringComparer.Ordinal);
            var info = m.Info();
            Assert.Equal(fall.GetProperty("info")[0].GetInt32(), info!.Value.Zeilen);
            Assert.Equal(fall.GetProperty("info")[1].GetInt64(), info.Value.Groesse);
            Assert.Equal(fall.GetProperty("zustand").GetString(), m.ZustandText());
            anzahl++;
        }
        Assert.Equal(7, anzahl);
    }

    [Fact]
    public void KuerzenUeberEinemMegabyteWiePython()
    {
        var k = G39.GetProperty("kuerzen");
        using var temp = new TempOrdner();
        var pfad = Path.Combine(temp.Pfad, Leistungsmessung.DateiName);
        var m = Neu(pfad, "A 0.10.0", "PC", "Avalonia 12.1.0", false);
        m.Vorgangsquelle = () => "vorladen:settings";
        var inv = CultureInfo.InvariantCulture;
        for (var i = 0; i < k.GetProperty("anzahl").GetInt32(); i++)
        {
            m.Aufzeichnen("seite", "seite" + (i % 997).ToString("D4", inv), "ziel" + (i * 7 % 1009).ToString("D4", inv), i * 0.25,
                elemente: i, aufgaben: i % 50, messart: i % 3 != 0 ? 2 : null);
        }
        Assert.True(m.Schreiben());
        var roh = File.ReadAllBytes(pfad);
        var zeilen = Encoding.UTF8.GetString(roh).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(k.GetProperty("zeilen").GetInt32(), zeilen.Length - 1);
        Assert.Equal(k.GetProperty("erste").GetString(), zeilen[1]);
        Assert.Equal(k.GetProperty("letzte").GetString(), zeilen[^1]);
        Assert.Equal(k.GetProperty("groesse").GetInt32(), roh.Length);
        Assert.Equal(k.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(roh)));
        Assert.Equal(k.GetProperty("zustand").GetString(), m.ZustandText());
    }

    [Fact]
    public void AuswertungWiePython()
    {
        var a = G39.GetProperty("auswertung");
        using var temp = new TempOrdner();
        var pfad = Path.Combine(temp.Pfad, "auswertung.csv");
        File.WriteAllText(pfad, a.GetProperty("datei").GetString(), new UTF8Encoding(false));
        var zeilen = Leistungsmessung.ZeilenLesen(pfad);
        var erwartet = a.GetProperty("zeilen").EnumerateArray().ToList();
        Assert.Equal(erwartet.Count, zeilen.Count);
        for (var i = 0; i < erwartet.Count; i++)
        {
            Assert.Equal(erwartet[i].EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!), zeilen[i]);
            Assert.Equal(a.GetProperty("messart")[i].GetInt32(), Leistungsmessung.Messart(zeilen[i]));
        }
        foreach (var art in new[] { 1, 2 })
        {
            var ist = Leistungsmessung.Seitenzeiten(zeilen, art).Select(p => (p.Key, p.Value)).ToList();
            var soll = a.GetProperty("seiten").GetProperty(art.ToString(CultureInfo.InvariantCulture)).EnumerateArray()
                .Select(p => (p[0].GetString()!, p[1].EnumerateArray().Select(w => w.GetDouble()).ToList())).ToList();
            Assert.Equal(soll.Count, ist.Count);
            for (var i = 0; i < soll.Count; i++)
            {
                Assert.Equal(soll[i].Item1, ist[i].Key);
                Assert.Equal(soll[i].Item2, ist[i].Value);
            }
        }
    }

    /// <summary>Kontrollfehler: bei ausgeschaltetem Schalter entsteht keine Datei und nichts wird gemerkt.</summary>
    [Fact]
    public void AusErzeugtKeineDatei()
    {
        using var temp = new TempOrdner();
        var pfad = Path.Combine(temp.Pfad, Leistungsmessung.DateiName);
        var m = Neu(pfad, "A", "PC", "", false, aktiv: false);
        for (var i = 0; i < 100; i++)
        {
            m.Aufzeichnen(Leistungsmessung.EreignisSeite, nach: "x", dauerMs: i);
        }
        Assert.True(m.Schreiben());
        Assert.False(File.Exists(pfad));
        Assert.Null(m.Info());
        Assert.Equal(LeistungTexte.KeineDatei, m.ZustandText());
    }

    [Fact]
    public void LoeschenVerwirftDateiUndPuffer()
    {
        using var temp = new TempOrdner();
        var pfad = Path.Combine(temp.Pfad, Leistungsmessung.DateiName);
        var m = Neu(pfad, "A", "PC", "", false);
        m.Aufzeichnen(Leistungsmessung.EreignisStart);
        Assert.True(m.Schreiben());
        m.Aufzeichnen(Leistungsmessung.EreignisSeite);
        Assert.True(m.Loeschen());
        Assert.False(File.Exists(pfad));
        Assert.True(m.Schreiben());
        Assert.False(File.Exists(pfad));
        Assert.True(m.Loeschen());   // fehlende Datei: kein Fehler
    }

    /// <summary>enabled(): nur ein gespeichertes true gilt.</summary>
    [Fact]
    public void NurEchtesTrueSchaltetEin()
    {
        Assert.True(Leistungsmessung.Eingeschaltet(new JsonObject { ["leistungsmessung"] = true }));
        foreach (var wert in new JsonNode?[] { false, 1, "true", "ja", null, new JsonObject() })
        {
            Assert.False(Leistungsmessung.Eingeschaltet(new JsonObject { ["leistungsmessung"] = wert }));
        }
        Assert.False(Leistungsmessung.Eingeschaltet([]));
    }

    /// <summary>Kontrollfehler "Schalter wandert in den Abgleich": der Schluessel gehoert zu keiner Abgleich-Tabelle und steht in keiner Sicherung.</summary>
    [Fact]
    public void SchalterNieImAbgleich()
    {
        Assert.DoesNotContain(Lernstand.AbgleichTabellen, t => t.Contains("leistung", StringComparison.OrdinalIgnoreCase));
        using var temp = new TempOrdner();
        var einstellungen = new Einstellungen(temp.Pfad);
        Assert.True(einstellungen.Setze(Leistungsmessung.Schluessel, true));
        var lernstand = new Lernstand(Path.Combine(temp.Pfad, Lernstand.DateiName));
        var sicherung = Ruecksicherung.Erstellen(lernstand, einstellungen.Laden(), "A 0.10.0", "PC", DateTime.Now);
        using var gz = new System.IO.Compression.GZipStream(new MemoryStream(sicherung), System.IO.Compression.CompressionMode.Decompress);
        using var leser = new StreamReader(gz, Encoding.UTF8);
        Assert.DoesNotContain(Leistungsmessung.Schluessel, leser.ReadToEnd(), StringComparison.Ordinal);
    }
}
