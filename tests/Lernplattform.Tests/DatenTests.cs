using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Lernplattform.Core.Daten;

namespace Lernplattform.Tests;

/// <summary>
/// Spike C: Python-Daten in C# lesen. Prueft Inhalte (G1), die von Python
/// angelegte Testdatenbank und die Python-Sicherungsdatei. Nur Testdaten,
/// nie Nicos echter Lernstand (Auflage A3).
/// </summary>
public class DatenTests(ITestOutputHelper ausgabe)
{
    private static JsonElement Lade(string datei)
    {
        using var f = File.OpenRead(Pfade.Vergleich(datei));
        return JsonDocument.Parse(f).RootElement.Clone();
    }

    [Fact]
    public void InhalteGleichWiePython()
    {
        var soll = Lade("inhalte.json").GetProperty("dateien");
        var uhr = Stopwatch.StartNew();
        var alle = Inhalte.LadeAlle(Pfade.Inhalte);
        uhr.Stop();
        ausgabe.WriteLine($"Laden aller Inhalte: {uhr.ElapsedMilliseconds} ms ({alle.Count} Dateien)");

        Assert.Equal(soll.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal),
            alle.Keys.Order(StringComparer.Ordinal));
        foreach (var datei in soll.EnumerateObject())
        {
            var wurzel = alle[datei.Name].RootElement;
            var bytes = File.ReadAllBytes(Path.Combine(Pfade.Inhalte, datei.Name));
            Assert.Equal(datei.Value.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
            Assert.Equal(datei.Value.GetProperty("art").GetString(),
                wurzel.ValueKind == JsonValueKind.Array ? "liste" : "objekt");
            Assert.Equal(datei.Value.GetProperty("eintraege").GetInt32(), Inhalte.Eintraege(wurzel));
            var (texte, textBytes) = Inhalte.ZaehleTexte(wurzel);
            Assert.Equal(datei.Value.GetProperty("texte").GetInt64(), texte);
            Assert.Equal(datei.Value.GetProperty("text_bytes_utf8").GetInt64(), textBytes);
        }
    }

    [Fact]
    public void KernmengenStimmen()
    {
        var alle = Inhalte.LadeAlle(Pfade.Inhalte);
        Assert.Equal(1565, alle["karteikarten.json"].RootElement.GetArrayLength());
        Assert.Equal(2120, alle["quizfragen.json"].RootElement.GetArrayLength());
        Assert.Equal(265, alle["szenarien_ap1.json"].RootElement.GetArrayLength());
        Assert.Equal(330, alle["szenarien_ap2.json"].RootElement.GetArrayLength());
        Assert.Equal(144, alle["rahmenplan.json"].RootElement.GetProperty("punkte").GetArrayLength());
    }

    [Fact]
    public void DatenbankGleichWiePython()
    {
        var soll = Lade("testdaten.json").GetProperty("tabellen");
        var ist = AlterLernstand.LeseDatenbank(Pfade.Vergleich("testdaten.db"));
        Assert.Equal(13, ist.Count);
        Assert.Equal(soll.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal),
            ist.Select(t => t.Name).Order(StringComparer.Ordinal));
        foreach (var tabelle in ist)
        {
            var s = soll.GetProperty(tabelle.Name);
            Assert.Equal(s.GetProperty("spalten").EnumerateArray().Select(e => e.GetString()), tabelle.Spalten);
            var zeilen = s.GetProperty("zeilen").EnumerateArray().ToList();
            Assert.Equal(zeilen.Count, tabelle.Zeilen.Count);
            for (int z = 0; z < zeilen.Count; z++)
            {
                var sollZeile = zeilen[z].EnumerateArray().ToList();
                for (int i = 0; i < sollZeile.Count; i++)
                {
                    var wert = tabelle.Zeilen[z][i];
                    var erwartet = sollZeile[i];
                    switch (erwartet.ValueKind)
                    {
                        case JsonValueKind.Null:
                            Assert.Null(wert);
                            break;
                        case JsonValueKind.String:
                            Assert.Equal(erwartet.GetString(), Assert.IsType<string>(wert));
                            break;
                        case JsonValueKind.Number when erwartet.TryGetInt64(out var ganz):
                            Assert.Equal(ganz, Convert.ToInt64(wert, System.Globalization.CultureInfo.InvariantCulture));
                            break;
                        default:
                            Assert.Equal(erwartet.GetDouble(), Convert.ToDouble(wert, System.Globalization.CultureInfo.InvariantCulture));
                            break;
                    }
                }
            }
        }
    }

    [Fact]
    public void SicherungGleichWiePython()
    {
        var soll = Lade("testdaten.json").GetProperty("sicherung");
        using var datei = File.OpenRead(Pfade.Vergleich("sicherung_test.json.gz"));
        using var sicherung = AlterLernstand.LeseSicherung(datei);
        var wurzel = sicherung.RootElement;
        Assert.Equal(soll.GetProperty("formatversion").GetInt32(), wurzel.GetProperty("formatversion").GetInt32());
        Assert.Equal(soll.GetProperty("format_abgleich").GetInt32(), wurzel.GetProperty("daten").GetProperty("format").GetInt32());
        var tabellen = wurzel.GetProperty("daten").GetProperty("tables");
        foreach (var t in soll.GetProperty("zeilen_je_tabelle").EnumerateObject())
        {
            Assert.Equal(t.Value.GetInt32(), tabellen.GetProperty(t.Name).GetArrayLength());
        }
        foreach (var z in soll.GetProperty("zusammenfassung").EnumerateObject())
        {
            Assert.Equal(z.Value.GetInt32(), wurzel.GetProperty("zusammenfassung").GetProperty(z.Name).GetInt32());
        }
    }

    [Fact]
    public void SicherungPruefsummeWiePython()
    {
        using var datei = File.OpenRead(Pfade.Vergleich("sicherung_test.json.gz"));
        using var sicherung = AlterLernstand.LeseSicherung(datei);
        Assert.True(AlterLernstand.PruefsummeStimmt(sicherung));
    }

    [Fact]
    public void PruefsummeSonderzeichenWiePython()
    {
        var text = File.ReadAllText(Pfade.Vergleich("pruefsumme_sonderfall.json"));
        using var gut = System.Text.Json.JsonDocument.Parse(text);
        Assert.True(AlterLernstand.PruefsummeStimmt(gut));
        using var veraendert = System.Text.Json.JsonDocument.Parse(text.Replace("cyan_pink", "cyan_pinK"));
        Assert.False(AlterLernstand.PruefsummeStimmt(veraendert));
    }
}
