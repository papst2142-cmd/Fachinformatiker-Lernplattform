using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;

namespace Lernplattform.Tests;

/// <summary>
/// Weisung CC P0-3, Auflage 1 bis 3: Die Pruefung der echten Sicherung gibt nur
/// Zaehlwerte, Zusammenfassung, Pruefsumme ja/nein und SHA-256 aus. Kontrollfehler:
/// Marker in Inhalten, Tabellen- und Schluesselnamen, Pfad und Dateiname duerfen
/// nie in der Ausgabe stehen, auch nicht im Fehlerfall.
/// </summary>
public class SicherungsPruefungTests
{
    private const string Marker = "GEHEIM_MARKER_7Q";

    private static byte[] Gzip(string text)
    {
        using var aus = new MemoryStream();
        using (var gz = new GZipStream(aus, CompressionLevel.Fastest))
        {
            gz.Write(Encoding.UTF8.GetBytes(text));
        }
        return aus.ToArray();
    }

    private static JsonObject Testsicherung()
    {
        using var datei = File.OpenRead(Pfade.Vergleich("sicherung_test.json.gz"));
        using var gz = new GZipStream(datei, CompressionMode.Decompress);
        return JsonNode.Parse(gz)!.AsObject();
    }

    /// <summary>Schreibt die Bytes in einen Ordner und eine Datei mit Marker im Namen und liest sie von dort.</summary>
    private static IReadOnlyList<string> PruefeUeberDatei(byte[] daten)
    {
        var ordner = Directory.CreateTempSubdirectory(Marker + "_PFAD_");
        try
        {
            var pfad = Path.Combine(ordner.FullName, Marker + "_NAME_lernstand.json.gz");
            File.WriteAllBytes(pfad, daten);
            return SicherungsPruefung.Pruefe(File.ReadAllBytes(pfad));
        }
        finally
        {
            ordner.Delete(recursive: true);
        }
    }

    private static void OhneMarker(IReadOnlyList<string> zeilen)
    {
        Assert.NotEmpty(zeilen);
        foreach (var z in zeilen)
        {
            Assert.DoesNotContain(Marker, z, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TestsicherungNurZaehlwerte()
    {
        var zeilen = PruefeUeberDatei(File.ReadAllBytes(Pfade.Vergleich("sicherung_test.json.gz")));
        Assert.Equal("Pruefsumme stimmt: ja", zeilen[0]);
        Assert.Contains("Zusammenfassung lerneintraege: 24", zeilen);
        Assert.Contains("Weitere Tabellen (ohne Namen): 0", zeilen);
        Assert.StartsWith("SHA-256 Datei: ", zeilen[^1]);
        // Nur die festen Zeilen: Pruefsumme, 12 Tabellen, weitere, 4 Zusammenfassung, SHA-256
        Assert.Equal(1 + 12 + 1 + 4 + 1, zeilen.Count);
    }

    [Fact]
    public void MarkerInInhaltenNamenPfadUndDateinameErscheinenNie()
    {
        var s = Testsicherung();
        var tabellen = s["daten"]!["tables"]!.AsObject();
        var erste = tabellen.First(t => t.Value!.AsArray().Count > 0).Value!.AsArray();
        erste.Add(new JsonArray(Marker + "_INHALT", 1, Marker + "_INHALT2"));
        tabellen[Marker + "_TABELLE"] = new JsonArray(new JsonArray(Marker));
        s["zusammenfassung"]![Marker + "_ZUSAMMENFASSUNG"] = 7;
        s["einstellungen"]![Marker + "_EINSTELLUNG"] = Marker + "_WERT";
        s["app_version"] = Marker + "_VERSION";
        s["geraet"] = Marker + "_GERAET";
        var zeilen = PruefeUeberDatei(Gzip(s.ToJsonString()));
        OhneMarker(zeilen);
        Assert.Equal("Pruefsumme stimmt: nein", zeilen[0]);
        Assert.Contains("Weitere Tabellen (ohne Namen): 1", zeilen);
    }

    [Fact]
    public void FehlerNurMitFestemCode()
    {
        // kein gzip
        var z1 = PruefeUeberDatei(Encoding.UTF8.GetBytes(Marker + " kein gzip"));
        OhneMarker(z1);
        Assert.Equal("FEHLER " + SicherungsPruefung.FehlerKeinGzip, z1[0]);
        // gzip, aber kein JSON
        var z2 = PruefeUeberDatei(Gzip("{\"" + Marker + "\": kaputt"));
        OhneMarker(z2);
        Assert.Equal("FEHLER " + SicherungsPruefung.FehlerKeinJson, z2[0]);
        // zu neue Formatversion
        var s = Testsicherung();
        s["formatversion"] = 99;
        s["app_version"] = Marker;
        var z3 = PruefeUeberDatei(Gzip(s.ToJsonString()));
        OhneMarker(z3);
        Assert.Equal("FEHLER " + SicherungsPruefung.FehlerZuNeu, z3[0]);
        // falscher Aufbau
        var z4 = PruefeUeberDatei(Gzip("{\"formatversion\":1,\"" + Marker + "\":1}"));
        OhneMarker(z4);
        Assert.Equal("FEHLER " + SicherungsPruefung.FehlerAufbau, z4[0]);
        foreach (var z in new[] { z1, z2, z3, z4 })
        {
            Assert.Equal(2, z.Count);
        }
    }
}
