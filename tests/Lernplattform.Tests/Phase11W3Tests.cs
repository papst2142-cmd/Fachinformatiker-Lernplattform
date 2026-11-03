using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Diagnose;

namespace Lernplattform.Tests;

[CollectionDefinition("Messpunkte", DisableParallelization = true)]
public sealed class MesspunkteSammlung;

/// <summary>
/// Phase 11 W3 (Weisung P11-21): Messpunkte, die das Programm fuer "Problem melden" aufschreibt (messpunkte.log).
/// Nur feste Namen, Version, Zeit und Millisekunden (Regel A3), je Programmstart einmal je Messpunkt, Datei begrenzt,
/// im Problembericht nur wenn vorhanden. Eigene Sammlung: der Merker "schon geschrieben" gilt fuer den ganzen Prozess.
/// </summary>
[Collection("Messpunkte")]
public class Phase11W3Tests
{
    private static readonly DateTime Zeit = new(2026, 11, 3, 12, 40, 59);

    [Fact]
    public void ZeileHatFesteForm()
    {
        Assert.Equal("03.11.2026 12:40 · A 0.34.0 · Inhalte laden: 312 ms", Messpunkte.Zeile(Zeit, "A 0.34.0", Messpunkte.Punkt.InhalteLaden, 312.4));
        Assert.Equal("03.11.2026 12:40 · A 0.34.0 · Erste Karte: 1001 ms", Messpunkte.Zeile(Zeit, "A 0.34.0", Messpunkte.Punkt.ErsteKarte, 1000.6));
        Assert.Equal("03.11.2026 12:40 · A 0.34.0 · Fachrichtung wechseln ae: 95 ms",
            Messpunkte.Zeile(Zeit, "A 0.34.0", Messpunkte.Punkt.FachrichtungWechseln, 95, "ae"));
        Assert.Equal("03.11.2026 12:40 · A 0.34.0 · Spiel betreten: 2400 ms", Messpunkte.Zeile(Zeit, "A 0.34.0", Messpunkte.Punkt.SpielBetreten, 2400));
    }

    [Fact]
    public void NurFesteWerteKommenInDieZeile()
    {
        // Regel A3: eine fremde Fachrichtung, ein ungueltiger Wert oder ein Pfad in der Version kommen nie in die Datei
        Assert.EndsWith("Fachrichtung wechseln ?: 5 ms", Messpunkte.Zeile(Zeit, "A 0.34.0", Messpunkte.Punkt.FachrichtungWechseln, 5, "Nico/geheim"),
            StringComparison.Ordinal);
        Assert.EndsWith("Fachrichtung wechseln ?: 5 ms", Messpunkte.Zeile(Zeit, "A 0.34.0", Messpunkte.Punkt.FachrichtungWechseln, 5), StringComparison.Ordinal);
        foreach (var wert in new[] { double.NaN, double.PositiveInfinity, -1 })
        {
            Assert.EndsWith("Inhalte laden: ? ms", Messpunkte.Zeile(Zeit, "A 0.34.0", Messpunkte.Punkt.InhalteLaden, wert), StringComparison.Ordinal);
        }
        var zeile = Messpunkte.Zeile(Zeit, "/home/nico/Lernstand: Frage?", Messpunkte.Punkt.InhalteLaden, 1);
        Assert.DoesNotContain("/", zeile, StringComparison.Ordinal);
        Assert.DoesNotContain("Frage?", zeile, StringComparison.Ordinal);
        Assert.Equal("03.11.2026 12:40 · ? · Inhalte laden: 1 ms", Messpunkte.Zeile(Zeit, "///", Messpunkte.Punkt.InhalteLaden, 1));
    }

    [Fact]
    public void JeProgrammstartEinmalJeMesspunkt()
    {
        using var ordner = new TempOrdner();
        Messpunkte.VergessenFuerTests();
        Assert.True(Messpunkte.Notieren(ordner.Pfad, Zeit, "A 0.34.0", Messpunkte.Punkt.InhalteLaden, 300));
        Assert.False(Messpunkte.Notieren(ordner.Pfad, Zeit, "A 0.34.0", Messpunkte.Punkt.InhalteLaden, 20));
        Assert.True(Messpunkte.Notieren(ordner.Pfad, Zeit, "A 0.34.0", Messpunkte.Punkt.FachrichtungWechseln, 90, "ae"));
        Assert.True(Messpunkte.Notieren(ordner.Pfad, Zeit, "A 0.34.0", Messpunkte.Punkt.FachrichtungWechseln, 80, "dv"));
        Assert.False(Messpunkte.Notieren(ordner.Pfad, Zeit, "A 0.34.0", Messpunkte.Punkt.FachrichtungWechseln, 70, "ae"));
        var text = File.ReadAllText(Path.Combine(ordner.Pfad, Messpunkte.DateiName));
        Assert.Equal("03.11.2026 12:40 · A 0.34.0 · Inhalte laden: 300 ms\n03.11.2026 12:40 · A 0.34.0 · Fachrichtung wechseln ae: 90 ms\n"
            + "03.11.2026 12:40 · A 0.34.0 · Fachrichtung wechseln dv: 80 ms\n", text);
        Messpunkte.VergessenFuerTests();
    }

    [Fact]
    public void NichtSchreibbarerOrdnerStoertNie()
    {
        using var ordner = new TempOrdner();
        Messpunkte.VergessenFuerTests();
        // eine Datei an Stelle des Ordners: Schreiben scheitert still
        var datei = Path.Combine(ordner.Pfad, "kein_ordner");
        File.WriteAllText(datei, "x");
        Assert.False(Messpunkte.Notieren(datei, Zeit, "A 0.34.0", Messpunkte.Punkt.SpielBetreten, 1));
        Messpunkte.VergessenFuerTests();
    }

    [Fact]
    public void DateiBleibtBegrenztUndBehaeltDieNeuesten()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Messpunkte.DateiName);
        for (var i = 0; i < 400; i++)
        {
            Messpunkte.Anhaengen(pfad, Messpunkte.Zeile(Zeit, "A 0.34.0", Messpunkte.Punkt.ErsteKarte, i));
        }
        var roh = File.ReadAllBytes(pfad);
        Assert.True(roh.Length <= Messpunkte.DateiMax, roh.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var zeilen = Encoding.UTF8.GetString(roh).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.EndsWith("Erste Karte: 399 ms", zeilen[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(zeilen, z => z.EndsWith("Erste Karte: 0 ms", StringComparison.Ordinal));
        // nur ganze Zeilen (die erste beginnt mit dem Datum)
        Assert.StartsWith("03.11.2026 12:40 · ", zeilen[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ImProblemberichtNurWennVorhanden()
    {
        using var ordner = new TempOrdner();
        var ohne = Problembericht.Bauen(null, "A 0.34.0", "Handy", [], Zeit, ordner.Pfad, Path.Combine(ordner.Pfad, "update.log"));
        Assert.DoesNotContain(Messpunkte.DateiName, ohne, StringComparison.Ordinal);
        Messpunkte.Anhaengen(Path.Combine(ordner.Pfad, Messpunkte.DateiName), Messpunkte.Zeile(Zeit, "A 0.34.0", Messpunkte.Punkt.InhalteLaden, 250));
        var mit = Problembericht.Bauen(null, "A 0.34.0", "Handy", new JsonObject(), Zeit, ordner.Pfad, Path.Combine(ordner.Pfad, "update.log"));
        Assert.Contains("--- messpunkte.log ---\n03.11.2026 12:40 · A 0.34.0 · Inhalte laden: 250 ms", mit, StringComparison.Ordinal);
    }
}
