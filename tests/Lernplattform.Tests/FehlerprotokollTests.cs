using System.Text;
using System.Text.Json;
using Lernplattform.Core.Diagnose;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 1, K4 (R3): Fehlerprotokoll. Schwaerzen und Pfade ersetzen exakt wie
/// Python (G10); Verhalten von fehler.log (Kopfzeile, Kuerzen, nie scheitern).
/// </summary>
public class FehlerprotokollTests
{
    private static readonly JsonElement G10 = Lade();

    private static JsonElement Lade()
    {
        using var f = File.OpenRead(Pfade.Vergleich("fehlerprotokoll.json"));
        return JsonDocument.Parse(f).RootElement.Clone();
    }

    private static (Dictionary<string, string> Umgebung, string Heim) Umgebung(string name)
    {
        var u = G10.GetProperty("umgebungen").GetProperty(name);
        var env = u.GetProperty("env").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        return (env, u.GetProperty("home").GetString()!);
    }

    [Fact]
    public void KonstantenWiePython()
    {
        Assert.Equal(Datenschutz.Entfernt, G10.GetProperty("redacted").GetString());
        Assert.Equal(Fehlerprotokoll.Hoechstgroesse, G10.GetProperty("log_max").GetInt32());
    }

    [Fact]
    public void SchwaerzenGleichWiePython()
    {
        var faelle = G10.GetProperty("faelle").EnumerateArray().ToList();
        Assert.True(faelle.Count >= 25);
        foreach (var fall in faelle)
        {
            var text = fall.GetProperty("text").GetString()!;
            Assert.Equal(fall.GetProperty("scrub").GetString(), Datenschutz.Schwaerzen(text));
        }
    }

    [Fact]
    public void PfadeGleichWiePython()
    {
        foreach (var fall in G10.GetProperty("faelle").EnumerateArray())
        {
            var text = fall.GetProperty("text").GetString()!;
            foreach (var umg in fall.GetProperty("anonym").EnumerateObject())
            {
                var (env, heim) = Umgebung(umg.Name);
                Assert.Equal(umg.Value.GetString(), Datenschutz.PfadeAnonymisieren(text, env, heim));
            }
            foreach (var umg in fall.GetProperty("protokoll").EnumerateObject())
            {
                var (env, heim) = Umgebung(umg.Name);
                Assert.Equal(umg.Value.GetString(),
                    Datenschutz.PfadeAnonymisieren(Datenschutz.Schwaerzen(text.TrimEnd()), env, heim));
            }
        }
    }

    [Fact]
    public void GeheimnisseGleichWiePython()
    {
        var texte = G10.GetProperty("faelle").EnumerateArray().Select(f => f.GetProperty("text").GetString()!).ToList();
        foreach (var fall in G10.GetProperty("geheimnisse").EnumerateArray())
        {
            var einstellungen = fall.GetProperty("einstellungen").EnumerateObject()
                .Select(p => new KeyValuePair<string, object?>(p.Name, p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString()))
                .ToList();
            // Zahlen sind in Python keine Texte und zaehlen nie als Geheimnis
            einstellungen = einstellungen.Select(p => fall.GetProperty("einstellungen").GetProperty(p.Key).ValueKind == JsonValueKind.String
                ? p : new KeyValuePair<string, object?>(p.Key, 0)).ToList();
            var soll = fall.GetProperty("geheimnisse").EnumerateArray().Select(e => e.GetString()!).ToList();
            var ist = Datenschutz.Geheimnisse(einstellungen);
            Assert.Equal(soll, ist);
            var sollTexte = fall.GetProperty("scrub").EnumerateArray().Select(e => e.GetString()).ToList();
            for (var i = 0; i < sollTexte.Count; i++)
            {
                Assert.Equal(sollTexte[i], Datenschutz.Schwaerzen(texte[i], ist));
            }
        }
    }

    /// <summary>Kontrollfehler: Ein Schluessel, der nicht geschwaerzt wird, muss auffallen.</summary>
    [Fact]
    public void KontrollfehlerSchluesselBleibtNie()
    {
        var text = "Abgleich: token=ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 Pfad /home/nico/x";
        var ergebnis = Datenschutz.PfadeAnonymisieren(Datenschutz.Schwaerzen(text), new Dictionary<string, string>(), "/home/nico");
        Assert.DoesNotContain("ghp_", ergebnis);
        Assert.DoesNotContain("nico", ergebnis);
        // Gegenprobe: ohne Schwaerzen bleibt der Schluessel stehen (die Pruefung oben kann also rot werden)
        Assert.Contains("ghp_", Datenschutz.PfadeAnonymisieren(text, new Dictionary<string, string>(), "/home/nico"));
    }

    /// <summary>
    /// P2-12 A3: ein leerer oder sehr kurzer Zugangsschluessel (0 bis 3 Zeichen, auch mit Leerzeichen)
    /// wird nie als Geheimnis genommen; die Fehlerdatei bleibt lesbar.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("e")]
    [InlineData("ab")]
    [InlineData("Feh")]
    [InlineData("  ler  ")]
    public void KurzerTokenNieGeheimnis(string token)
    {
        Assert.Empty(Datenschutz.Geheimnisse([new("sync_token", token)]));
        const string text = "Fehler beim Lesen der Datei lernstand.db";
        Assert.Equal(text, Datenschutz.Schwaerzen(text, Datenschutz.Geheimnisse([new("sync_token", token)])));
    }

    /// <summary>Kontrollfehler A3: wuerde ein kurzer Wert doch als Geheimnis genommen, waere der Text zerstoert.</summary>
    [Fact]
    public void KontrollfehlerKurzerTokenMachtLogUnlesbar()
    {
        const string text = "Fehler beim Lesen der Datei lernstand.db";
        Assert.NotEqual(text, Datenschutz.Schwaerzen(text, ["e"]));
        Assert.DoesNotContain("Fehler", Datenschutz.Schwaerzen(text, ["e"]));
    }

    [Fact]
    public void SchreibenMitKopfzeileOhneGeheimnisse()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Fehlerprotokoll.DateiName);
        Fehlerprotokoll.Schreiben("Fehler mit token=geheim12345\n\n", pfad);
        Fehlerprotokoll.Schreiben(new InvalidOperationException("zweiter Fehler"), pfad);
        var inhalt = File.ReadAllText(pfad, Encoding.UTF8).Replace("\r\n", "\n");
        var zeilen = inhalt.Split('\n');
        Assert.Matches(@"^=== \d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \| Version .+ \| (win32|linux|darwin|android) ===$", zeilen[0]);
        Assert.Equal("Fehler mit token=[entfernt]", zeilen[1]);
        Assert.StartsWith("=== ", zeilen[2]);
        Assert.Contains("System.InvalidOperationException: zweiter Fehler", inhalt);
        Assert.DoesNotContain("geheim12345", inhalt);
        Assert.Equal(inhalt.Trim(), Fehlerprotokoll.Anzeigen(pfad).Replace("\r\n", "\n"));
    }

    /// <summary>Windows-Zeilenenden im Fehlertext (Exception.ToString) ergeben keine Leerzeilen.</summary>
    [Fact]
    public void ZeilenendenEinheitlich()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Fehlerprotokoll.DateiName);
        Fehlerprotokoll.Schreiben("erste\r\nzweite\ndritte", pfad);
        var roh = File.ReadAllText(pfad, Encoding.UTF8);
        Assert.DoesNotContain("\r\r", roh);
        var zeilen = roh.Split(Environment.NewLine);
        Assert.Equal(["erste", "zweite", "dritte", ""], zeilen[1..]);
        if (!OperatingSystem.IsWindows())
        {
            Assert.DoesNotContain("\r", roh);
        }
    }

    [Fact]
    public void GrosseDateiWirdGekuerzt()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Fehlerprotokoll.DateiName);
        var zeile = new string('x', 99) + "\n";
        File.WriteAllText(pfad, string.Concat(Enumerable.Repeat(zeile, 3000)));
        Assert.True(new FileInfo(pfad).Length > Fehlerprotokoll.Hoechstgroesse);
        Fehlerprotokoll.Schreiben("neu", pfad);
        var laenge = new FileInfo(pfad).Length;
        Assert.InRange(laenge, Fehlerprotokoll.Hoechstgroesse / 2, Fehlerprotokoll.Hoechstgroesse / 2 + 200);
        Assert.EndsWith("neu" + Environment.NewLine, File.ReadAllText(pfad));
    }

    [Fact]
    public void AnzeigenKuerztAmZeilenanfang()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Fehlerprotokoll.DateiName);
        File.WriteAllText(pfad, "erste Zeile\nzweite Zeile\ndritte Zeile\n");
        var text = Fehlerprotokoll.Anzeigen(pfad, 20);
        Assert.Equal("… (ältere Einträge gekürzt, gezeigt werden die letzten 0 KB)\ndritte Zeile", text);
        Assert.Equal("", Fehlerprotokoll.Anzeigen(Path.Combine(ordner.Pfad, "fehlt.log")));
    }

    [Fact]
    public void SchreibenScheitertNie()
    {
        using var ordner = new TempOrdner();
        // Pfad, dessen "Ordner" eine Datei ist: Schreiben muss still scheitern
        var datei = Path.Combine(ordner.Pfad, "datei");
        File.WriteAllText(datei, "x");
        Fehlerprotokoll.Schreiben("Text", Path.Combine(datei, Fehlerprotokoll.DateiName));
        Assert.Equal("x", File.ReadAllText(datei));
    }
}
