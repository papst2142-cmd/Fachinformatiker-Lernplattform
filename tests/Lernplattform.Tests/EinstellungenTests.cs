using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 1, K3 (R3): Einstellungen. Vorgaben und gelesene Werte exakt wie
/// Python (G11); Pflichttests der Auflage K3: unbekannte Schluessel bleiben,
/// halb geschriebene Datei, Dateirecht 0600 (Linux/macOS).
/// </summary>
public class EinstellungenTests
{
    private static readonly JsonElement G11 = Lade();

    private static JsonElement Lade()
    {
        using var f = File.OpenRead(Pfade.Vergleich("datenbank.json"));
        return JsonDocument.Parse(f).RootElement.Clone();
    }

    [Fact]
    public void VorgabeWiePython()
    {
        using var ordner = new TempOrdner();
        var werte = new Einstellungen(ordner.Pfad).Laden();
        Assert.Equal(JsonNode.Parse(G11.GetProperty("einstellungen_vorgabe").GetRawText())!.ToJsonString(), werte.ToJsonString());
        Assert.Equal(G11.GetProperty("darstellungen").EnumerateArray().Select(e => e.GetString()), Einstellungen.Darstellungen);
        Assert.Equal(G11.GetProperty("schriftgroessen").EnumerateArray().Select(e => e[0].GetString()), Einstellungen.Schriftgroessen);
        var ziel = Einstellungen.Ziel(new JsonObject());
        var soll = G11.GetProperty("tagesziel_vorgabe");
        Assert.Equal(soll.GetProperty("ziel_an").GetBoolean(), ziel.ZielAn);
        Assert.Equal(soll.GetProperty("ziel_anzahl").GetInt32(), ziel.ZielAnzahl);
        Assert.Equal(soll.GetProperty("serie_an").GetBoolean(), ziel.SerieAn);
        Assert.Equal(soll.GetProperty("erinnerung_an").GetBoolean(), ziel.ErinnerungAn);
        Assert.Equal(soll.GetProperty("erinnerung_zeit").GetString(), ziel.ErinnerungZeit);
    }

    [Fact]
    public void GeleseneWerteWiePython()
    {
        var faelle = G11.GetProperty("einstellungen").EnumerateArray().ToList();
        Assert.True(faelle.Count >= 22);
        foreach (var fall in faelle)
        {
            using var ordner = new TempOrdner();
            File.WriteAllText(Path.Combine(ordner.Pfad, Einstellungen.DateiName), fall.GetProperty("einstellungen").GetRawText());
            var werte = new Einstellungen(ordner.Pfad).Laden();
            var text = fall.GetProperty("einstellungen").GetRawText();
            Assert.True(fall.GetProperty("darstellung").GetString() == Einstellungen.Darstellung(werte), text);
            Assert.True(fall.GetProperty("grundfarbe").GetString() == Einstellungen.Grundfarbe(werte), text);
            Assert.True(fall.GetProperty("hintergrund").GetString() == Einstellungen.Hintergrund(werte), text);
            Assert.True(fall.GetProperty("schriftgroesse").GetString() == Einstellungen.Schriftgroesse(werte), text);
            var soll = fall.GetProperty("tagesziel");
            var ist = Einstellungen.Ziel(werte);
            Assert.Equal(
                new Einstellungen.Tagesziel(soll.GetProperty("ziel_an").GetBoolean(), soll.GetProperty("ziel_anzahl").GetInt32(),
                    soll.GetProperty("serie_an").GetBoolean(), soll.GetProperty("erinnerung_an").GetBoolean(),
                    soll.GetProperty("erinnerung_zeit").GetString()!),
                ist);
        }
    }

    [Fact]
    public void SpeichernUndLaden()
    {
        using var ordner = new TempOrdner();
        var einstellungen = new Einstellungen(ordner.Pfad);
        Assert.True(einstellungen.Setze(Einstellungen.DarstellungSchluessel, "hell"));
        Assert.True(einstellungen.Setze("ziel_anzahl", 35));
        var werte = einstellungen.Laden();
        Assert.Equal("hell", Einstellungen.Darstellung(werte));
        Assert.Equal(35, Einstellungen.Ziel(werte).ZielAnzahl);
        Assert.True((bool)werte[Einstellungen.AutoCheckSchluessel]!);
        Assert.False(File.Exists(einstellungen.Pfad + ".tmp"));
    }

    /// <summary>Auflage K3: unbekannte Schluessel (z. B. aus einem spaeteren Programmstand) bleiben erhalten.</summary>
    [Fact]
    public void UnbekannteSchluesselBleibenErhalten()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Einstellungen.DateiName);
        File.WriteAllText(pfad, """{"zukunft_a": {"x": [1, 2, "drei"]}, "zukunft_b": 1.5, "auto_check": false, "eigene_farben": {"dunkel": {"akzent1": [200, 50, 60]}}}""");
        var einstellungen = new Einstellungen(ordner.Pfad);
        Assert.True(einstellungen.Setze(Einstellungen.SchriftSchluessel, "gross"));
        var gelesen = JsonNode.Parse(File.ReadAllText(pfad))!.AsObject();
        Assert.Equal("""{"x":[1,2,"drei"]}""", gelesen["zukunft_a"]!.ToJsonString());
        Assert.Equal(1.5, (double)gelesen["zukunft_b"]!);
        Assert.False((bool)gelesen["auto_check"]!);
        Assert.Equal("""{"dunkel":{"akzent1":[200,50,60]}}""", gelesen["eigene_farben"]!.ToJsonString());
        Assert.Equal("gross", (string)gelesen[Einstellungen.SchriftSchluessel]!);
    }

    /// <summary>Auflage K3: halb geschriebene Datei (Absturz beim Schreiben).</summary>
    [Fact]
    public void HalbGeschriebeneDatei()
    {
        using var ordner = new TempOrdner();
        var einstellungen = new Einstellungen(ordner.Pfad);
        Assert.True(einstellungen.Setze(Einstellungen.GrundfarbeSchluessel, "rot_pink"));
        // Fall 1: Absturz waehrend des Schreibens der Hilfsdatei -> die echte Datei ist unberuehrt
        File.WriteAllText(einstellungen.Pfad + ".tmp", """{"grundfarbe": "gruen_l""");
        Assert.Equal("rot_pink", Einstellungen.Grundfarbe(einstellungen.Laden()));
        Assert.Null(einstellungen.DefektHinweis());
        Assert.True(einstellungen.Setze(Einstellungen.HintergrundSchluessel, "schwarz"));
        Assert.False(File.Exists(einstellungen.Pfad + ".tmp"));
        Assert.Equal("rot_pink", Einstellungen.Grundfarbe(einstellungen.Laden()));

        // Fall 2: die Datei selbst ist abgeschnitten (z. B. altes Programm ohne atomares Speichern)
        var abgeschnitten = """{"grundfarbe": "rot_pink", "hintergr""";
        File.WriteAllText(einstellungen.Pfad, abgeschnitten);
        var hinweis = einstellungen.DefektHinweis();
        Assert.Equal((Einstellungen.DefektTitel, Einstellungen.DefektText), hinweis);
        Assert.Equal(abgeschnitten, File.ReadAllText(Path.Combine(ordner.Pfad, Einstellungen.DefektName)));
        Assert.Equal("""{"auto_check":true}""", JsonNode.Parse(File.ReadAllText(einstellungen.Pfad))!.ToJsonString());
        // Die Meldung kommt genau einmal
        Assert.Null(einstellungen.DefektHinweis());
    }

    [Theory]
    [InlineData("[1, 2, 3]")]
    [InlineData("\"nur Text\"")]
    [InlineData("")]
    [InlineData("{\"a\": }")]
    public void KaputteDateiWirdAufgehoben(string inhalt)
    {
        using var ordner = new TempOrdner();
        var einstellungen = new Einstellungen(ordner.Pfad);
        File.WriteAllText(einstellungen.Pfad, inhalt);
        Assert.Equal("""{"auto_check":true}""", einstellungen.Laden().ToJsonString());
        Assert.Equal(inhalt, File.ReadAllText(Path.Combine(ordner.Pfad, Einstellungen.DefektName)));
    }

    [Fact]
    public void KaputteBytesWerdenAufgehoben()
    {
        using var ordner = new TempOrdner();
        var einstellungen = new Einstellungen(ordner.Pfad);
        byte[] roh = [(byte)'{', (byte)'"', 0xC3, 0x28, (byte)'"', (byte)':', (byte)'1', (byte)'}'];
        File.WriteAllBytes(einstellungen.Pfad, roh);
        Assert.Equal("""{"auto_check":true}""", einstellungen.Laden().ToJsonString());
        Assert.Equal(roh, File.ReadAllBytes(Path.Combine(ordner.Pfad, Einstellungen.DefektName)));
    }

    /// <summary>Auflage K3: Dateirecht 0600 unter Linux/macOS (auch wenn die Hilfsdatei schon da war).</summary>
    [Fact]
    public void DateirechtNurEigenesKonto()
    {
        using var ordner = new TempOrdner();
        var einstellungen = new Einstellungen(ordner.Pfad);
        if (OperatingSystem.IsWindows())
        {
            // Unter Windows gibt es kein 0600; dort genuegt das Speichern im Benutzerprofil.
            Assert.True(einstellungen.Setze("a", 1));
            return;
        }
        File.WriteAllText(einstellungen.Pfad + ".tmp", "alt");
        File.SetUnixFileMode(einstellungen.Pfad + ".tmp", (UnixFileMode)0b110_110_110);
        Assert.True(einstellungen.Setze("a", 1));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(einstellungen.Pfad));
    }

    [Fact]
    public void SpeichernScheitertOhneSchaden()
    {
        using var ordner = new TempOrdner();
        var einstellungen = new Einstellungen(ordner.Pfad);
        Assert.True(einstellungen.Setze("a", 1));
        var vorher = File.ReadAllText(einstellungen.Pfad);
        // Die Hilfsdatei ist ein Ordner: Speichern muss scheitern, die Datei bleibt
        Directory.CreateDirectory(einstellungen.Pfad + ".tmp");
        Assert.False(einstellungen.Setze("a", 2));
        Assert.Equal(vorher, File.ReadAllText(einstellungen.Pfad));
    }

    [Theory]
    [InlineData("7:5", "07:05")]
    [InlineData(" 23:59 ", "23:59")]
    [InlineData("24:00", null)]
    [InlineData("12:60", null)]
    [InlineData("1800", null)]
    [InlineData("08:00:00", null)]
    [InlineData("-0:05", "00:05")]
    [InlineData("+8:+5", "08:05")]
    [InlineData("1_2:3_0", "12:30")]
    [InlineData("1__2:30", null)]
    [InlineData("ab:cd", null)]
    public void UhrzeitWiePython(string text, string? soll) => Assert.Equal(soll, Einstellungen.Uhrzeit(text));
}
