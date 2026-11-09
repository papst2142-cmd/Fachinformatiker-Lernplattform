using Lernplattform.Core.Update;
using Lernplattform.Desktop;
using Stadtbau.Core;
using Stadtbau.Desktop;
using BS = Stadtbau.Core.Werkzeugordner.Betriebssystem;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.25.1 (Weisung P12-107): Fund 250 (Datenordner unter Windows = Installationsordner von Velopack, Setup und
/// Deinstallation leeren ihn) und Fund 249 (Update am PC fand nie etwas: Adresse ohne https://github.com/). Je Pruefung
/// eine Gegenprobe mit dem alten Verhalten. Ein Programmfehler der Suche faengt die Oberflaeche (Aktualisierung) und schreibt ihn in fehler.log; UpdateSuche laesst ihn wie seit S 0.5.1 durch (UpdateSucheTests).
/// </summary>
public class DatenordnerUpdateTests
{
    private static string Ordner(Environment.SpecialFolder f) => f switch
    {
        Environment.SpecialFolder.LocalApplicationData => @"C:\Users\Nico\AppData\Local",
        Environment.SpecialFolder.ApplicationData => @"C:\Users\Nico\AppData\Roaming",
        Environment.SpecialFolder.UserProfile => "/home/nico",
        _ => throw new ArgumentOutOfRangeException(nameof(f)),
    };

    private static string? OhneUmgebung(string _) => null;

    [Fact]
    public void Windows_DatenordnerNichtImInstallationsordner()
    {
        var pfad = Werkzeugordner.StandardPfad(BS.Windows, Ordner, OhneUmgebung);
        var installation = Werkzeugordner.InstallationWindows(Ordner);
        Assert.Equal(Path.Combine(@"C:\Users\Nico\AppData\Roaming", "Fachinformatiker-Stadtbau"), pfad);
        Assert.Equal(Path.Combine(@"C:\Users\Nico\AppData\Local", "Fachinformatiker-Stadtbau"), installation);
        Assert.NotEqual(installation, pfad);
        Assert.False(pfad.StartsWith(installation, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Windows_Gegenprobe_AlterPfadIstDerInstallationsordner()
    {
        Werkzeugordner.StandardAltFuerTests.Value = true;
        try
        {
            Assert.Equal(Werkzeugordner.InstallationWindows(Ordner), Werkzeugordner.StandardPfad(BS.Windows, Ordner, OhneUmgebung));
        }
        finally
        {
            Werkzeugordner.StandardAltFuerTests.Value = false;
        }
    }

    [Fact]
    public void AndereSysteme_Unveraendert_UndSchalterGilt()
    {
        Assert.Equal(Path.Combine("/home/nico", "Library", "Application Support", "Fachinformatiker-Stadtbau"),
            Werkzeugordner.StandardPfad(BS.Mac, Ordner, OhneUmgebung));
        Assert.Equal(Path.Combine(@"C:\Users\Nico\AppData\Local", "Fachinformatiker-Stadtbau"),
            Werkzeugordner.StandardPfad(BS.Android, Ordner, OhneUmgebung));
        Assert.Equal(Path.Combine("/home/nico", ".local", "share", "fachinformatiker-stadtbau"),
            Werkzeugordner.StandardPfad(BS.Linux, Ordner, OhneUmgebung));
        Assert.Equal(Path.Combine("/xdg", "fachinformatiker-stadtbau"),
            Werkzeugordner.StandardPfad(BS.Linux, Ordner, n => n == "XDG_DATA_HOME" ? "/xdg" : null));
        Assert.Equal("/eigen", Werkzeugordner.StandardPfad(BS.Windows, Ordner, n => n == Werkzeugordner.Umgebung ? "/eigen" : null));
    }

    [Fact]
    public void Umzug_KopiertAlles_UeberschreibtNichts_LoeschtNichts()
    {
        using var temp = new Temp();
        var alt = Path.Combine(temp.Pfad, "alt");
        var neu = new Werkzeugordner(Path.Combine(temp.Pfad, "neu"));
        Schreiben(alt, "projekte/Stadt 1.fisbau", "projekt");
        Schreiben(alt, "sicherungen/Stadt 1/2026-11-09_0500.fisbau", "sicherung");
        Schreiben(alt, "blueprints/Haus.fisblueprint", "blueprint");
        Schreiben(alt, "exporte/stadt.fistadt", "export");
        Schreiben(alt, "einstellungen.json", "{\"vollbild\": true}");
        Schreiben(alt, "fehler.log", "alt-fehler\n");
        Schreiben(alt, "update.log", "alt-update\n");
        Schreiben(alt, "current/Stadtbau.exe", "programm");
        Schreiben(alt, "packages/x.nupkg", "paket");
        // im neuen Ordner liegt schon eine eigene Einstellung: sie bleibt
        Schreiben(neu.Wurzel, "einstellungen.json", "{\"neu\": 1}");

        var e = Datenumzug.Ausfuehren(alt, neu, new DateTime(2026, 11, 9, 7, 0, 0));

        Assert.Equal(new Datenumzug.Ergebnis(true, 6, 1, 0), e);
        Assert.Equal("projekt", Lesen(neu.Wurzel, "projekte/Stadt 1.fisbau"));
        Assert.Equal("sicherung", Lesen(neu.Wurzel, "sicherungen/Stadt 1/2026-11-09_0500.fisbau"));
        Assert.Equal("blueprint", Lesen(neu.Wurzel, "blueprints/Haus.fisblueprint"));
        Assert.Equal("export", Lesen(neu.Wurzel, "exporte/stadt.fistadt"));
        Assert.Equal("{\"neu\": 1}", Lesen(neu.Wurzel, "einstellungen.json"));
        Assert.Equal("alt-fehler\n", Lesen(neu.Wurzel, "fehler.log"));
        Assert.False(File.Exists(Path.Combine(neu.Wurzel, "current", "Stadtbau.exe")));
        Assert.False(Directory.Exists(Path.Combine(neu.Wurzel, "packages")));
        var log = Lesen(neu.Wurzel, "update.log");
        Assert.StartsWith("alt-update\n", log.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("6 Dateien aus dem alten Ordner kopiert, 1 lagen schon da", log, StringComparison.Ordinal);
        // alter Ordner unveraendert
        Assert.Equal("projekt", Lesen(alt, "projekte/Stadt 1.fisbau"));
        Assert.Equal("programm", Lesen(alt, "current/Stadtbau.exe"));
        Assert.Equal(9, Directory.GetFiles(alt, "*", SearchOption.AllDirectories).Length);

        // genau einmal: auch eine neue Datei im alten Ordner kommt danach nicht mehr
        Schreiben(alt, "projekte/Spaeter.fisbau", "spaeter");
        Assert.False(Datenumzug.Ausfuehren(alt, neu, DateTime.Now).Gelaufen);
        Assert.False(File.Exists(Path.Combine(neu.Wurzel, "projekte", "Spaeter.fisbau")));
    }

    [Fact]
    public void Umzug_NeuerOrdnerHatGleicheDatei_BleibtUnveraendert()
    {
        using var temp = new Temp();
        var alt = Path.Combine(temp.Pfad, "alt");
        var neu = new Werkzeugordner(Path.Combine(temp.Pfad, "neu"));
        Schreiben(alt, "projekte/Stadt 1.fisbau", "alt");
        Schreiben(neu.Wurzel, "projekte/Stadt 1.fisbau", "neu");
        var e = Datenumzug.Ausfuehren(alt, neu, DateTime.Now);
        Assert.Equal(new Datenumzug.Ergebnis(true, 0, 1, 0), e);
        Assert.Equal("neu", Lesen(neu.Wurzel, "projekte/Stadt 1.fisbau"));
    }

    [Fact]
    public void Umzug_OhneAlteDaten_KeineMerkdatei_SpaeterKommtNoch()
    {
        using var temp = new Temp();
        var alt = Path.Combine(temp.Pfad, "alt");
        var neu = new Werkzeugordner(Path.Combine(temp.Pfad, "neu"));
        Schreiben(alt, "current/Stadtbau.exe", "programm");
        Assert.False(Datenumzug.Ausfuehren(alt, neu, DateTime.Now).Gelaufen);
        Assert.False(File.Exists(Path.Combine(neu.Wurzel, Datenumzug.Merkdatei)));
        Schreiben(alt, "projekte/A.fisbau", "a");
        Assert.Equal(new Datenumzug.Ergebnis(true, 1, 0, 0), Datenumzug.Ausfuehren(alt, neu, DateTime.Now));
    }

    [Fact]
    public void Umzug_GleicherOrdner_TutNichts()
    {
        using var temp = new Temp();
        Schreiben(temp.Pfad, "projekte/A.fisbau", "a");
        Assert.False(Datenumzug.Ausfuehren(temp.Pfad + Path.DirectorySeparatorChar, new Werkzeugordner(temp.Pfad), DateTime.Now).Gelaufen);
        Assert.False(File.Exists(Path.Combine(temp.Pfad, Datenumzug.Merkdatei)));
    }

    [Fact]
    public void Umzug_DateiScheitert_KeineMerkdatei_NeuerVersuch()
    {
        using var temp = new Temp();
        var alt = Path.Combine(temp.Pfad, "alt");
        var neu = new Werkzeugordner(Path.Combine(temp.Pfad, "neu"));
        Schreiben(alt, "projekte/A.fisbau", "a");
        Schreiben(alt, "projekte/B.fisbau", "b");
        // an der Stelle von B liegt im neuen Ordner ein Ordner: Kopieren scheitert
        Directory.CreateDirectory(Path.Combine(neu.Wurzel, "projekte", "B.fisbau"));
        var e = Datenumzug.Ausfuehren(alt, neu, DateTime.Now);
        Assert.Equal(new Datenumzug.Ergebnis(true, 1, 0, 1), e);
        Assert.False(File.Exists(Path.Combine(neu.Wurzel, Datenumzug.Merkdatei)));
        Assert.Contains("Datenumzug", Lesen(neu.Wurzel, "fehler.log"), StringComparison.Ordinal);
        Directory.Delete(Path.Combine(neu.Wurzel, "projekte", "B.fisbau"));
        Assert.Equal(new Datenumzug.Ergebnis(true, 1, 1, 0), Datenumzug.Ausfuehren(alt, neu, DateTime.Now));
        Assert.Equal("b", Lesen(neu.Wurzel, "projekte/B.fisbau"));
        Assert.True(File.Exists(Path.Combine(neu.Wurzel, Datenumzug.Merkdatei)));
    }

    [Fact]
    public void Updatequelle_VolleAdresse_LaesstSichBauen()
    {
        Assert.Equal("https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform", Updatequelle.RepositoryUrl);
        var quelle = Updatequelle.Quelle();
        Assert.IsType<StoerungsfesteQuelle>(quelle);
        Assert.Equal("stadtbau-win", Updatequelle.Kanal);
    }

    [Fact]
    public void Updatequelle_Gegenprobe_OhneHttps_WirftWieBisS0250()
    {
        Updatequelle.AltFuerTests.Value = true;
        try
        {
            Assert.ThrowsAny<UriFormatException>(() => Updatequelle.Quelle());
        }
        finally
        {
            Updatequelle.AltFuerTests.Value = false;
        }
    }

    [Fact]
    public void Stoerung_504_WirdWiederholt_DannErfolg()
    {
        var versuche = 0;
        var zeilen = new List<string>();
        var pausen = new List<TimeSpan>();
        var ergebnis = Stoerung.Wiederholen("Feed werkzeug", () =>
        {
            if (++versuche < 3)
            {
                throw new HttpRequestException("Response status code does not indicate success: 504 (Gateway Time-out).", null, System.Net.HttpStatusCode.GatewayTimeout);
            }
            return "0.25.0";
        }, zeilen.Add, pausen.Add);
        Assert.Equal("0.25.0", ergebnis);
        Assert.Equal(3, versuche);
        Assert.Equal([TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)], pausen);
        Assert.Equal(2, zeilen.Count);
        Assert.StartsWith("WIEDERHOLUNG Feed werkzeug Versuch 2 nach 15 s (HttpRequestException", zeilen[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Stoerung_HoechstensVierVersuche_DannFehlerUnveraendert()
    {
        var versuche = 0;
        var x = Assert.Throws<HttpRequestException>(() => Stoerung.Wiederholen<int>("Suche",
            () => { versuche++; throw new HttpRequestException("504", null, System.Net.HttpStatusCode.GatewayTimeout); }, _ => { }, _ => { }));
        Assert.Equal(4, versuche);
        Assert.Equal(System.Net.HttpStatusCode.GatewayTimeout, x.StatusCode);
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.NotFound)]
    [InlineData(System.Net.HttpStatusCode.Forbidden)]
    public void Gegenprobe_4xx_UndProgrammfehler_NieWiederholt(System.Net.HttpStatusCode code)
    {
        var versuche = 0;
        Assert.Throws<HttpRequestException>(() => Stoerung.Wiederholen<int>("Suche",
            () => { versuche++; throw new HttpRequestException("x", null, code); }, _ => { }, _ => { }));
        Assert.Equal(1, versuche);
        versuche = 0;
        Assert.Throws<UriFormatException>(() => Stoerung.Wiederholen<int>("Suche",
            () => { versuche++; throw new UriFormatException(); }, _ => { }, _ => { }));
        Assert.Equal(1, versuche);
    }

    [Fact]
    public void Voruebergehend_AuchAlsInnereAusnahme_UndOhneAntwort()
    {
        Assert.True(Stoerung.Voruebergehend(new InvalidOperationException("x", new HttpRequestException("y", null, System.Net.HttpStatusCode.BadGateway))));
        Assert.True(Stoerung.Voruebergehend(new HttpRequestException("kein Netz")));
        Assert.True(Stoerung.Voruebergehend(new TaskCanceledException()));
        Assert.False(Stoerung.Voruebergehend(new InvalidOperationException("x")));
        Assert.False(Stoerung.Voruebergehend(null));
    }

    private static void Schreiben(string wurzel, string rel, string text)
    {
        var pfad = Path.Combine(wurzel, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        File.WriteAllText(pfad, text);
    }

    private static string Lesen(string wurzel, string rel) => File.ReadAllText(Path.Combine(wurzel, rel));

    private sealed class Temp : IDisposable
    {
        public string Pfad { get; } = Directory.CreateTempSubdirectory("stadtbau-umzug-").FullName;

        public void Dispose()
        {
            try
            {
                Directory.Delete(Pfad, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
