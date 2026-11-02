using System.Text.Json.Nodes;
using Lernplattform.Core.Inhalte;

namespace Lernplattform.Tests;

/// <summary>
/// Sammelliste 177 (Nico 02.11.2026 Antwort 177a, Weisung P11-4): eingerueckte Tabellenzeilen der Szenarien
/// erkennen, damit die Oberflaeche sie in fester Zeichenbreite zeigt.
/// </summary>
public class TabellenzeilenTests
{
    private static string[] Zeilen(string text) =>
        [.. Tabellenzeilen.Bereiche(text).Select(b => text.Substring(b.Start, b.Laenge))];

    [Fact]
    public void TabelleMitLeerzeichenSpalten()
    {
        var text = "Vergleich:\n  Kriterium  Gewicht  A\n  Preis 30 % 6\n  Kosten     20 %     4\nFazit";
        Assert.Equal(["  Kriterium  Gewicht  A", "  Preis 30 % 6", "  Kosten     20 %     4"], Zeilen(text));
        var bereich = Tabellenzeilen.Bereiche(text)[0];
        Assert.Equal(text.IndexOf("  Kriterium", StringComparison.Ordinal), bereich.Start);
    }

    [Fact]
    public void TabelleMitSenkrechtemStrich()
    {
        Assert.Equal(["  1001 | Hansen | 2"], Zeilen("Ergebnis:\n  1001 | Hansen | 2"));
    }

    [Fact]
    public void AufzaehlungUndFliesstextBleiben()
    {
        Assert.Empty(Tabellenzeilen.Bereiche("Netze:\n  - A (Verwaltung): 172.16.0.0/23\n  - B (Produktion): 172.16.2.0/24"));
        Assert.Empty(Tabellenzeilen.Bereiche("Ein Satz  mit  doppelten Leerzeichen, nicht eingerueckt."));
        Assert.Empty(Tabellenzeilen.Bereiche("  eingerueckt, aber nur einfache Leerzeichen   "));
        Assert.Empty(Tabellenzeilen.Bereiche(""));
        Assert.Empty(Tabellenzeilen.Bereiche(null));
    }

    [Fact]
    public void ZwischenzeileTrenntBloecke()
    {
        var text = "  a  b\n\n  - Punkt eins\n  - Punkt zwei\r\nText\r\n  x | y\r\n";
        Assert.Equal(["  a  b", "  x | y"], Zeilen(text));
        Assert.Equal([1, 1], Tabellenzeilen.Bloecke(text).Select(b => b.Count));
        Assert.Equal([3], Tabellenzeilen.Bloecke("Vergleich:\n  K  G\n  P 3\n  K  2\nFazit").Select(b => b.Count));
    }

    /// <summary>In jeder Fachrichtung gibt es Szenarien mit Tabellen; eingerueckte Aufzaehlungen bleiben normal.</summary>
    [Fact]
    public void BestandJeFachrichtung()
    {
        var treffer = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var datei in new[] { Path.Combine(Pfade.Inhalte, "szenarien_ap2.json"),
            Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen", "szenarien_ap2.json") })
        {
            foreach (var eintrag in JsonNode.Parse(File.ReadAllBytes(datei))!.AsArray())
            {
                var fachrichtung = eintrag!["fachrichtung"]?.GetValue<string>() ?? "si";
                foreach (var feld in new[] { "text", "solution" })
                {
                    var text = eintrag[feld] is JsonArray liste ? string.Join("\n", liste.Select(z => z!.GetValue<string>())) : eintrag[feld]!.GetValue<string>();
                    foreach (var zeile in Zeilen(text))
                    {
                        Assert.StartsWith(" ", zeile, StringComparison.Ordinal);
                        Assert.DoesNotContain('\n', zeile);
                    }
                    if (Zeilen(text).Length > 0)
                    {
                        treffer[fachrichtung] = treffer.GetValueOrDefault(fachrichtung) + 1;
                    }
                }
            }
        }
        Assert.Equal(["ae", "dpa", "dv", "si"], treffer.Keys.Order(StringComparer.Ordinal));
    }
}
