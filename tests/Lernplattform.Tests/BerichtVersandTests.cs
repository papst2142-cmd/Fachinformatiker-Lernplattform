using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 11 (Weisungen P11-29, P11-30, Sammelliste 202): "Bericht senden" gegen die GitHub-Attrappe auf 127.0.0.1
/// (nie das echte GitHub, nie ein echter Schluessel). Die echte Strecke mit dem eingebauten Schluessel prueft der
/// Dispatch-Lauf "voll" (pakete linux-x64, Datei berichte/_probe_&lt;Lauf&gt;.md).
/// </summary>
public sealed class BerichtVersandTests : IDisposable
{
    private const string Schluessel = "github_pat_PROBE_nur_fuer_tests_123456";
    private static readonly DateTime Zeit = new(2026, 11, 4, 10, 15, 0, DateTimeKind.Local);
    private readonly string _ordner = Directory.CreateTempSubdirectory("bericht_").FullName;

    private string Log => Path.Combine(_ordner, Fehlerprotokoll.DateiName);

    public void Dispose() => Directory.Delete(_ordner, true);

    private VersandErgebnis Senden(GitHubAttrappe attrappe, string inhalt = "Bericht", string? schluessel = Schluessel) =>
        BerichtVersand.Senden(inhalt, Zeit, "Handy", "A 0.36.0",
            new BerichtVersand.Zugriff(new GitHubZugang(attrappe.Basis, TimeSpan.FromSeconds(5)), schluessel, Log));

    [Fact]
    public void DateiName_NachMuster()
    {
        Assert.Equal("berichte/2026-11-04_101500_Handy_A0.36.0.md", BerichtVersand.DateiName(Zeit, "Handy", "A 0.36.0"));
        Assert.Equal("berichte/2026-11-04_101500_PC_A0.36.0_2.md", BerichtVersand.DateiName(Zeit, "PC", "A 0.36.0", 2));
        Assert.Equal("berichte/_probe_471.md", BerichtVersand.ProbeName("471"));
        // nichts ausser Buchstaben, Ziffern, Punkt, Minus, Unterstrich
        Assert.Equal("berichte/_probe_..x.md", BerichtVersand.ProbeName("../x"));   // kein Schraegstrich: bleibt im Ordner
    }

    [Fact]
    public void Inhalt_BerichtUnveraendert_BeschreibungDarueber()
    {
        const string bericht = "Version: A 0.36.0\n\n--- fehler.log ---\nZeile";
        var nur = BerichtVersand.Inhalt(bericht, "  ");
        Assert.Equal("```text\n" + bericht + "\n```\n", nur);
        var mit = BerichtVersand.Inhalt(bericht, "Knopf reagiert nicht");
        Assert.Equal("## Beschreibung\n\nKnopf reagiert nicht\n\n## Bericht\n\n```text\n" + bericht + "\n```\n", mit);
        // hoechstens 2000 Zeichen Beschreibung
        var lang = BerichtVersand.Inhalt(bericht, new string('x', 2500));
        Assert.Contains("\n\n" + new string('x', BerichtSendenTexte.FeldHoechst) + "\n\n## Bericht", lang, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', BerichtSendenTexte.FeldHoechst + 1), lang, StringComparison.Ordinal);
        // ein Zaun im Bericht beendet den Block nicht
        Assert.StartsWith("````text\na ``` b\n````", BerichtVersand.Inhalt("a ``` b", null), StringComparison.Ordinal);
    }

    [Fact]
    public void Senden_LegtDateiAn_SchluesselNurImKopf()
    {
        using var attrappe = new GitHubAttrappe();
        var ergebnis = Senden(attrappe, "Inhalt äöü");
        Assert.Equal(VersandArt.Gesendet, ergebnis.Art);
        Assert.Equal("berichte/2026-11-04_101500_Handy_A0.36.0.md", ergebnis.Datei);
        var (ort, inhalt) = Assert.Single(attrappe.Berichte);
        Assert.Equal("repos/papst2142-cmd/fi-fehlerberichte/contents/berichte/2026-11-04_101500_Handy_A0.36.0.md", ort);
        Assert.Equal("Inhalt äöü", Encoding.UTF8.GetString(inhalt));
        var koerper = Assert.Single(attrappe.PutKoerper);
        Assert.Equal("main", (string?)koerper["branch"]);
        Assert.Equal("Problembericht Handy A 0.36.0", (string?)koerper["message"]);
        var anfrage = Assert.Single(attrappe.Anfragen);
        Assert.Contains("Authorization: Bearer " + Schluessel, anfrage, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(anfrage, Regex.Escape(Schluessel)));
        Assert.False(File.Exists(Log));
        Assert.Equal((BerichtSendenTexte.GesendetTitel, "Danke! Der Bericht ist angekommen. Dateiname: 2026-11-04_\u200B101500_\u200BHandy_\u200BA0.36.0.md"),
            BerichtVersand.Rueckmeldung(ergebnis));
    }

    [Fact]
    public void Senden_NameVergeben_NaechsteNummer()
    {
        using var attrappe = new GitHubAttrappe();
        Assert.Equal(VersandArt.Gesendet, Senden(attrappe, "eins").Art);
        var zwei = Senden(attrappe, "zwei");
        Assert.Equal("berichte/2026-11-04_101500_Handy_A0.36.0_2.md", zwei.Datei);
        Assert.Equal(2, attrappe.Berichte.Count);
        Assert.Equal("eins", Encoding.UTF8.GetString(attrappe.Berichte["repos/papst2142-cmd/fi-fehlerberichte/contents/berichte/2026-11-04_101500_Handy_A0.36.0.md"]));
    }

    [Theory]
    [InlineData(401, VersandArt.Abgelehnt)]
    [InlineData(403, VersandArt.Abgelehnt)]
    [InlineData(404, VersandArt.Abgelehnt)]
    [InlineData(500, VersandArt.Fehler)]
    public void Senden_Abgelehnt_DeutscherText_LogOhneSchluessel(int code, VersandArt art)
    {
        using var attrappe = new GitHubAttrappe { PutStatus = code, KopfZurueck = true };
        var ergebnis = Senden(attrappe);
        Assert.Equal(new VersandErgebnis(art, null, code), ergebnis);
        var log = File.ReadAllText(Log);
        Assert.Contains("Bericht senden fehlgeschlagen: " + art + " (HTTP " + code.ToString(CultureInfo.InvariantCulture) + ")", log, StringComparison.Ordinal);
        Assert.DoesNotContain(Schluessel, log, StringComparison.Ordinal);
        Assert.DoesNotContain("PROBE", log, StringComparison.Ordinal);
        var (titel, text) = BerichtVersand.Rueckmeldung(ergebnis);
        Assert.Equal(BerichtSendenTexte.FehlerTitel, titel);
        Assert.Equal(art == VersandArt.Abgelehnt
            ? "Der Bericht ließ sich nicht senden: Der Speicher hat den Bericht abgelehnt (Zugang ungültig oder abgelaufen). Du kannst ihn stattdessen als Datei speichern oder kopieren und weitergeben."
            : "Der Bericht ließ sich nicht senden: Der Speicher meldet einen Fehler (HTTP 500). Du kannst ihn stattdessen als Datei speichern oder kopieren und weitergeben.", text);
        Assert.DoesNotContain(Schluessel, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Senden_KeineVerbindung()
    {
        var frei = new TcpListener(IPAddress.Loopback, 0);
        frei.Start();
        var port = ((IPEndPoint)frei.LocalEndpoint).Port;
        frei.Stop();
        var ergebnis = BerichtVersand.Senden("x", Zeit, "PC", "A 0.36.0", new BerichtVersand.Zugriff(
            new GitHubZugang(new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture)), TimeSpan.FromSeconds(2)), Schluessel, Log));
        Assert.Equal(VersandArt.KeineVerbindung, ergebnis.Art);
        Assert.Contains("Bericht senden fehlgeschlagen: KeineVerbindung", File.ReadAllText(Log), StringComparison.Ordinal);
        Assert.Equal("Der Bericht ließ sich nicht senden: Keine Verbindung zum Internet oder Zeit überschritten. Du kannst ihn stattdessen als Datei speichern oder kopieren und weitergeben.",
            BerichtVersand.Rueckmeldung(ergebnis).Text);
    }

    [Fact]
    public void OhneSchluessel_NichtsGesendet()
    {
        using var attrappe = new GitHubAttrappe();
        Assert.Equal(VersandArt.NichtVerfuegbar, Senden(attrappe, schluessel: null).Art);
        Assert.Empty(attrappe.Anfragen);
        Assert.Equal((BerichtSendenTexte.RueckfrageTitel, BerichtSendenTexte.NichtVerfuegbar),
            BerichtVersand.Rueckmeldung(new VersandErgebnis(VersandArt.NichtVerfuegbar, null, 0)));
    }

    /// <summary>Tests werden nie mit Schluessel gebaut (nur release.yml und die Paket-Jobs der CI setzen ihn).</summary>
    [Fact]
    public void TestbauOhneSchluessel()
    {
        Assert.Null(BerichtSchluessel.Wert());
        Assert.False(BerichtVersand.Verfuegbar);
        Assert.Equal(VersandArt.NichtVerfuegbar, BerichtVersand.Probe("test", "A 0.36.0", Zeit).Art);
    }

    /// <summary>Der Bau maskiert genau wie BerichtSchluessel.Maske; kein lesbarer Schluessel im Programm.</summary>
    [Fact]
    public void BauMaskiertWieDasProgramm()
    {
        var projekt = File.ReadAllText(Path.Combine(Pfade.Wurzel, "src", "Lernplattform.Core", "Lernplattform.Core.csproj"));
        Assert.Contains("(byte)(roh[i] ^ (byte)((0xA7 ^ (i * 151 + 29)) & 0xFF))", projekt, StringComparison.Ordinal);
        Assert.Contains("GetEnvironmentVariable(\"FEHLERBERICHT_TOKEN\")", projekt, StringComparison.Ordinal);
        for (var i = 0; i < 300; i++)
        {
            Assert.Equal((byte)((0xA7 ^ (i * 151 + 29)) & 0xFF), BerichtSchluessel.Maske(i));
        }
        // die erzeugte Datei liegt nur im obj-Ordner und ist nie eingecheckt
        Assert.DoesNotContain(Directory.GetFiles(Path.Combine(Pfade.Wurzel, "src"), "BerichtSchluessel.g.cs", SearchOption.AllDirectories),
            p => !p.Split(Path.DirectorySeparatorChar).Contains("obj"));
    }

    /// <summary>
    /// BEFRISTET bis 1.0 (Weisung P11-29, Nico): der eingebaute Schluessel darf nicht in 1.0 bleiben. Ohne Stufe (ab 1.0)
    /// wird dieser Test rot, solange BerichtSchluessel.cs noch da ist.
    /// </summary>
    [Fact]
    public void Befristet_Bis_1_0()
    {
        var props = File.ReadAllText(Path.Combine(Pfade.Wurzel, "Directory.Build.props"));
        var stufe = Regex.Match(props, "<Stufe>([^<]*)</Stufe>").Groups[1].Value;
        var datei = Path.Combine(Pfade.Wurzel, "src", "Lernplattform.Core", "Diagnose", "BerichtSchluessel.cs");
        Assert.True(stufe is "A" or "B" || !File.Exists(datei),
            "Version 1.0 mit eingebautem Schluessel fuer \"Bericht senden\": vor 1.0 umbauen (Weisung P11-29)");
    }
}
