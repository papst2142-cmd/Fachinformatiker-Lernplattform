using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lernplattform.Core.Projekt;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W3: PDF-Export und Zeichentabelle gegen G34 (Teile pdf und zeichen, echtes Python
/// v0.63.2 mit festem Datum 14.10.2026). Die gepackten Stroeme weichen ab (zlib gegen .NET),
/// verglichen wird entpackt: Seiteninhalte byte-gleich, alle Objekte ohne Strom gleich bis auf
/// "/Length", Kopf, Info, Seitenzahl; jede xref-Stelle zeigt auf ihr Objekt.
/// </summary>
public class AbschlussprojektPdfTests
{
    private static readonly JsonObject G34 = AbschlussprojektTests.G34;

    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>Objekte ueber die xref-Stellen zerlegen, Stroeme entpacken (wie der Erzeuger).</summary>
    internal static (List<string> Objekte, List<string> Inhalte) Zerlegen(byte[] daten)
    {
        var text = Latin1.GetString(daten);
        var start = text.LastIndexOf("startxref\n", StringComparison.Ordinal);
        var xref = int.Parse(text[(start + 10)..].Split('\n')[0], System.Globalization.CultureInfo.InvariantCulture);
        Assert.StartsWith("xref\n", text[xref..], StringComparison.Ordinal);
        var teile = text[xref..].Split('\n');
        var anzahl = int.Parse(teile[1].Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
        var stellen = teile[3..(3 + anzahl - 1)].Select(t => int.Parse(t[..10], System.Globalization.CultureInfo.InvariantCulture)).ToList();
        var objekte = new List<string>();
        var inhalte = new List<string>();
        for (var n = 1; n <= stellen.Count; n++)
        {
            var ende = n < stellen.Count ? stellen[n] : xref;
            var roh = text[stellen[n - 1]..ende];
            var kopf = n + " 0 obj\n";
            Assert.StartsWith(kopf, roh, StringComparison.Ordinal);
            Assert.EndsWith("\nendobj\n", roh, StringComparison.Ordinal);
            var inhalt = roh[kopf.Length..^"\nendobj\n".Length];
            var trenner = inhalt.IndexOf("\nstream\n", StringComparison.Ordinal);
            if (trenner < 0)
            {
                objekte.Add(inhalt);
                continue;
            }
            var strom = inhalt[(trenner + 8)..];
            Assert.EndsWith("\nendstream", strom, StringComparison.Ordinal);
            var gepackt = Latin1.GetBytes(strom[..^"\nendstream".Length]);
            var laenge = Regex.Match(inhalt[..trenner], "/Length (\\d+)");
            Assert.Equal(gepackt.Length, int.Parse(laenge.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            using var zlib = new ZLibStream(new MemoryStream(gepackt), CompressionMode.Decompress);
            using var aus = new MemoryStream();
            zlib.CopyTo(aus);
            inhalte.Add(Latin1.GetString(aus.ToArray()));
            objekte.Add(Regex.Replace(inhalt[..trenner], "/Length \\d+", "/Length #"));
        }
        return (objekte, inhalte);
    }

    [Fact]
    public void PdfWiePython()
    {
        var faelle = AbschlussprojektTests.Faelle().ToDictionary(f => f.Name, f => f.Felder);
        var ist = new JsonArray();
        foreach (var name in new[] { "leer", "festwert", "unicode", "lang" })
        {
            var daten = Abschlussprojekt.ExportPdf(faelle[name], 40, new DateTime(2026, 10, 14));
            var (objekte, inhalte) = Zerlegen(daten);
            ist.Add(AbschlussprojektTests.L(name, new JsonObject
            {
                ["seiten"] = inhalte.Count,
                ["inhalte"] = AbschlussprojektTests.L(inhalte, i => i),
                ["objekte"] = AbschlussprojektTests.L(objekte, o => o),
                ["kopf"] = Convert.ToHexStringLower(daten[..15]),
                ["info"] = objekte[^1],
            }));
        }
        AbschlussprojektTests.Gleich(G34["pdf"], ist, "pdf");
        Assert.Equal(3, (int)G34["pdf"]![1]![1]!["seiten"]!);
    }

    /// <summary>Jeder Codepunkt: _clean(chr(c)) wie Python 3.12 (Laeufe gleich/leer/frage, einzelne Ergebnisse, Pruefsumme).</summary>
    [Fact]
    public void ZeichentabelleWiePython()
    {
        var g = G34["zeichen"]!;
        Assert.Equal("15.0.0", (string)g["unicode"]!);
        var erwartet = new Dictionary<int, string>();
        foreach (var e in g["einzeln"]!.AsArray())
        {
            erwartet[(int)e![0]!] = (string)e[1]!;
        }
        var arten = new Dictionary<int, string>();
        foreach (var lauf in g["laeufe"]!.AsArray())
        {
            for (var c = (int)lauf![0]!; c <= (int)lauf[1]!; c++)
            {
                arten[c] = (string)lauf[2]!;
            }
        }
        using var summe = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var geprueft = 0;
        for (var c = 0; c < 0x110000; c++)
        {
            if (c is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }
            var ist = PdfZeichen.Zeichen(c);
            var soll = erwartet.TryGetValue(c, out var t) ? t : arten[c] switch
            {
                "gleich" => char.ConvertFromUtf32(c),
                "leer" => "",
                _ => "?",
            };
            Assert.True(soll == ist, "U+" + c.ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
            summe.AppendData(Encoding.UTF8.GetBytes(ist + "\0"));
            geprueft++;
        }
        Assert.Equal(0x110000 - 0x800, geprueft);
        Assert.Equal((string)g["sha256"]!, Convert.ToHexStringLower(summe.GetHashAndReset()));
    }

    /// <summary>NFC ueber den ganzen Text (Spezifikation Abschnitt 8): zerlegte Zeichen werden zusammengesetzt.</summary>
    [Fact]
    public void NfcUeberDenGanzenText()
    {
        Assert.Equal("Ä", PdfZeichen.Bereinigen("Ä"));
        Assert.Equal("é", PdfZeichen.Bereinigen("é"));
        // U+1E9B U+0323: NFC bleibt so, Grundform "s", der Punkt (Mn) entfaellt (Python 3.12: "s")
        Assert.Equal("s", PdfZeichen.Bereinigen("\u1e9b\u0323"));
        Assert.Equal([0x80], PdfZeichen.Kodieren("€"));
        Assert.Equal([0x20], PdfZeichen.Kodieren(" "));
    }
}
