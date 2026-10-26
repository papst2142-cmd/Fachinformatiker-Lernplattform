using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Hilfe;
using Lernplattform.Core.Linux;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 6 W2 (VE5, Weisung P6-7): Linux-Verknuepfung im Anwendungsmenue und auf dem Desktop
/// (Wortliste W2 Spalte "C# a"), nachgebaut aus fisi_verknuepfung.py (Python 0.63.2). Alles in einem
/// eigenen Testordner als Benutzerordner; xdg-user-dir und gio sind ersetzt.
/// </summary>
public sealed class VerknuepfungTests : IDisposable
{
    private const string AppImage = "/opt/test/Fachinformatiker-Lernplattform.AppImage";
    private static readonly byte[] SymbolBytes = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    private readonly string _home = Directory.CreateTempSubdirectory("verknuepfung_").FullName;
    private readonly List<string> _gio = [];

    public void Dispose()
    {
        try
        {
            Directory.Delete(_home, true);
        }
        catch (IOException)
        {
            // Testordner bleibt dann liegen
        }
    }

    private string Desktop => Path.Combine(_home, "Schreibtisch");

    private VerknuepfungUmgebung Umgebung(string? sitzung = "KDE", bool gio = true, string? xdg = null, string? appImage = AppImage,
        bool desktop = true)
    {
        if (desktop)
        {
            Directory.CreateDirectory(Desktop);
        }
        return new(_home, null, null, appImage, sitzung, () => xdg ?? (desktop ? Desktop : _home), pfad =>
        {
            _gio.Add(pfad);
            return gio;
        }, () => SymbolBytes);
    }

    private string Menue => Path.Combine(_home, ".local", "share", "applications", Verknuepfung.DateiName);

    private string Symbol => Path.Combine(_home, ".local", "share", "icons", "hicolor", "512x512", "apps", "fachinformatiker-lernplattform.png");

    /// <summary>Wortliste W2 Spalte "C# a" woertlich (Weisung P6-7, W2-12 Nachtrag Nico 23.10.2026 20:07).</summary>
    [Fact]
    public void TexteWieWortliste()
    {
        Assert.Equal("Verknüpfung anlegen", VerknuepfungTexte.Anlegen);
        Assert.Equal("Noch keine Verknüpfung angelegt.", VerknuepfungTexte.Keine);
        Assert.Equal("Im Anwendungsmenü liegt eine Verknüpfung.", VerknuepfungTexte.Vorhanden);
        Assert.Equal("Verknüpfung", VerknuepfungTexte.FrageTitel);
        Assert.Equal("Soll eine Verknüpfung zur Fachinformatiker Lernplattform im Anwendungsmenü und auf dem Desktop angelegt werden? "
            + "Du kannst sie später auch in den Optionen unter „Updates“ anlegen.", VerknuepfungTexte.FrageText);
        Assert.Equal(["Ja", "Nein", "Nicht mehr fragen"], new[] { VerknuepfungTexte.Ja, VerknuepfungTexte.Nein, VerknuepfungTexte.NieFragen });
        Assert.Equal("Verknüpfung angelegt: %s", VerknuepfungTexte.Angelegt);
        Assert.Equal("Verknüpfung erneuert: %s", VerknuepfungTexte.Erneuert);
        Assert.Equal("Es wurde kein Desktop-Ordner gefunden. Die Verknüpfung liegt nur im Anwendungsmenü.", VerknuepfungTexte.KeinDesktop);
        Assert.Equal("Die Verknüpfung konnte nicht angelegt werden: %s", VerknuepfungTexte.Fehler);
        Assert.Equal("\nFalls sie sich nicht per Doppelklick starten lässt: Rechtsklick und „Start erlauben“ wählen.", VerknuepfungTexte.Unsicher);
        // W2-12: letzter Absatz im Hilfe-Artikel "Update"
        var update = HilfeTexte.Abschnitte.Single(a => a.Id == "update");
        Assert.EndsWith("\n\nUnter Linux legt „Verknüpfung anlegen“ eine Verknüpfung im Anwendungsmenü und auf dem Desktop an oder erneuert sie, "
            + "zum Beispiel nachdem du das AppImage verschoben hast.", update.Text, StringComparison.Ordinal);
    }

    /// <summary>quote_exec: Werte von python3.12 -I mit fisi_verknuepfung.quote_exec (v0.63.2).</summary>
    [Theory]
    [InlineData("/home/nico/Fachinformatiker-Lernplattform.AppImage", "/home/nico/Fachinformatiker-Lernplattform.AppImage")]
    [InlineData("/home/nico/Mein Ordner/Lernplattform.AppImage", "\"/home/nico/Mein Ordner/Lernplattform.AppImage\"")]
    [InlineData("/home/a\"b$c`d\\e/x.AppImage", "\"/home/a\\\"b\\$c\\`d\\\\e/x.AppImage\"")]
    [InlineData("/tmp/ä(1)#x", "\"/tmp/ä(1)#x\"")]
    public void ExecZeileWiePython(string pfad, string soll) => Assert.Equal(soll, Verknuepfung.ExecZeile(pfad));

    [Fact]
    public void InhaltWieWortliste()
    {
        Assert.Equal("[Desktop Entry]\nType=Application\nName=Fachinformatiker Lernplattform\nGenericName=Lernprogramm\n"
            + "Comment=Lernprogramm fuer die Pruefungsvorbereitung zum Fachinformatiker (Schwerpunkt Systemintegration)\n"
            + "Exec=\"/a b/x.AppImage\"\nIcon=/i.png\nTerminal=false\nCategories=Education;\n"
            + "Keywords=Fachinformatiker;FI;FISI;IHK;Lernen;Netzwerk;Subnetting;RAID;\nStartupWMClass=Lernplattform\n"
            + "X-Lernplattform-Verknuepfung=true\n", Verknuepfung.Inhalt("/a b/x.AppImage", "/i.png"));
        Assert.All(Verknuepfung.Inhalt(AppImage, "/i.png"), z => Assert.True(z < 128));
    }

    [Fact]
    public void AnlegenUndErneuern()
    {
        var python = Path.Combine(Desktop, "fisi-lernplattform.desktop");
        var umgebung = Umgebung();
        File.WriteAllText(python, "fremd");
        Assert.Equal(VerknuepfungTexte.Keine, Verknuepfung.StatusText(umgebung));
        var neu = Verknuepfung.Anlegen(umgebung);
        var desktop = Path.Combine(Desktop, Verknuepfung.DateiName);
        Assert.True(neu.Ok);
        Assert.Equal("Verknüpfung angelegt: " + Menue + "\nVerknüpfung angelegt: " + desktop, neu.Text);
        var inhalt = Verknuepfung.Inhalt(AppImage, Symbol);
        Assert.Equal(inhalt, File.ReadAllText(Menue));
        Assert.Equal(inhalt, File.ReadAllText(desktop));
        Assert.Equal(SymbolBytes, File.ReadAllBytes(Symbol));
        Assert.Equal(VerknuepfungTexte.Vorhanden, Verknuepfung.StatusText(umgebung));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead, File.GetUnixFileMode(Menue));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute, File.GetUnixFileMode(desktop));
        }
        var erneuert = Verknuepfung.Anlegen(umgebung);
        Assert.Equal("Verknüpfung erneuert: " + Menue + "\nVerknüpfung erneuert: " + desktop, erneuert.Text);
        // nie eine zweite Datei, keine Hilfsdatei, die Python-Verknuepfung bleibt unberuehrt
        Assert.Equal([desktop, python], Directory.GetFiles(Desktop).Order(StringComparer.Ordinal));
        Assert.Equal([Menue], Directory.GetFiles(Path.GetDirectoryName(Menue)!));
        Assert.Equal("fremd", File.ReadAllText(python));
        Assert.Empty(_gio);   // KDE: Ausfuehrrecht genuegt
    }

    [Theory]
    [InlineData("ubuntu:GNOME", true, false)]
    [InlineData("GNOME", false, true)]
    [InlineData("KDE", false, false)]
    [InlineData("XFCE", true, true)]
    [InlineData(null, true, true)]
    public void VertrauenJeSitzung(string? sitzung, bool gio, bool hinweis)
    {
        var ergebnis = Verknuepfung.Anlegen(Umgebung(sitzung, gio));
        Assert.True(ergebnis.Ok);
        Assert.Equal(hinweis, ergebnis.Text.EndsWith(VerknuepfungTexte.Unsicher, StringComparison.Ordinal));
        // gio nur unter GNOME und nur fuer die Datei auf dem Desktop
        Assert.Equal(Verknuepfung.Sitzung(sitzung) == "gnome" ? [Path.Combine(Desktop, Verknuepfung.DateiName)] : [], _gio);
    }

    [Fact]
    public void OhneDesktopNurAnwendungsmenue()
    {
        var ergebnis = Verknuepfung.Anlegen(Umgebung(desktop: false));
        Assert.True(ergebnis.Ok);
        Assert.Equal("Verknüpfung angelegt: " + Menue + "\n" + VerknuepfungTexte.KeinDesktop, ergebnis.Text);
        Assert.True(File.Exists(Menue));
        Assert.False(Directory.Exists(Path.Combine(_home, "Desktop")));
    }

    [Fact]
    public void FehlerNennenNurDieFehlerart()
    {
        var ohne = Verknuepfung.Anlegen(Umgebung(appImage: null));
        Assert.Equal(new VerknuepfungErgebnis(false, "Die Verknüpfung konnte nicht angelegt werden: Programmdatei unbekannt"), ohne);
        Assert.False(Directory.Exists(Path.Combine(_home, ".local")));
        // "applications" ist eine Datei: das Anlegen scheitert, der Text nennt nur den Namen der Ausnahme
        Directory.CreateDirectory(Path.Combine(_home, ".local", "share"));
        File.WriteAllText(Path.Combine(_home, ".local", "share", "applications"), "");
        var fehler = Verknuepfung.Anlegen(Umgebung());
        Assert.False(fehler.Ok);
        Assert.Matches("^Die Verknüpfung konnte nicht angelegt werden: [A-Za-z]+Exception$", fehler.Text);
        Assert.False(File.Exists(Path.Combine(Desktop, Verknuepfung.DateiName)));
    }

    [Fact]
    public void DesktopOrdnerWiePython()
    {
        Directory.CreateDirectory(Desktop);
        // xdg-user-dir liefert den Benutzerordner selbst: zaehlt nicht als Desktop
        Assert.Null(Verknuepfung.DesktopOrdner(Umgebung(xdg: _home)));
        Assert.Null(Verknuepfung.DesktopOrdner(Umgebung(xdg: _home + "/")));
        Assert.Equal(Desktop, Verknuepfung.DesktopOrdner(Umgebung(xdg: Desktop)));
        // ohne xdg-user-dir: user-dirs.dirs, sonst ~/Desktop
        Assert.Null(Verknuepfung.DesktopOrdner(Umgebung(xdg: "")));
        Directory.CreateDirectory(Path.Combine(_home, ".config"));
        File.WriteAllText(Path.Combine(_home, ".config", "user-dirs.dirs"), "# Kommentar\nXDG_DESKTOP_DIR=\"$HOME/Schreibtisch\"\n");
        Assert.Equal(Desktop, Verknuepfung.DesktopOrdner(Umgebung(xdg: "")));
        File.Delete(Path.Combine(_home, ".config", "user-dirs.dirs"));
        Directory.CreateDirectory(Path.Combine(_home, "Desktop"));
        Assert.Equal(Path.Combine(_home, "Desktop"), Verknuepfung.DesktopOrdner(Umgebung(xdg: "")));
        Assert.Equal(Path.Combine(_home, "Desktop"), Verknuepfung.DesktopOrdner(Umgebung(xdg: "~/Desktop")));
    }

    [Fact]
    public void RueckfrageNurBeiFragenUndOhneVerknuepfung()
    {
        var umgebung = Umgebung();
        Assert.True(Verknuepfung.SollFragen([], umgebung));
        Assert.True(Verknuepfung.SollFragen(new JsonObject { [Verknuepfung.Schluessel] = "fragen" }, umgebung));
        Assert.True(Verknuepfung.SollFragen(new JsonObject { [Verknuepfung.Schluessel] = "unbekannt" }, umgebung));
        Assert.True(Verknuepfung.SollFragen(new JsonObject { [Verknuepfung.Schluessel] = 1 }, umgebung));
        Assert.False(Verknuepfung.SollFragen(new JsonObject { [Verknuepfung.Schluessel] = "nie" }, umgebung));
        Assert.False(Verknuepfung.SollFragen(new JsonObject { [Verknuepfung.Schluessel] = "erledigt" }, umgebung));
        // eine Verknuepfung im Anwendungsmenue (z. B. von Hand angelegt) beendet das Fragen
        Directory.CreateDirectory(Path.GetDirectoryName(Menue)!);
        File.WriteAllText(Menue, "");
        Assert.False(Verknuepfung.SollFragen([], umgebung));
    }

    [Theory]
    [InlineData(true, true, AppImage, true)]
    [InlineData(false, true, AppImage, false)]
    [InlineData(true, false, AppImage, false)]
    [InlineData(true, true, "", false)]
    [InlineData(true, true, null, false)]
    public void NurLinuxImInstalliertenAppImage(bool linux, bool installiert, string? appImage, bool soll) =>
        Assert.Equal(soll, Verknuepfung.Unterstuetzt(linux, installiert, appImage));

    /// <summary>
    /// Sammelliste 77 (Weisung P8-3): Das Anlegen laeuft im Hintergrund; haengt xdg-user-dir, laeuft der
    /// Aufrufer weiter (Ergebnis wie vorher).
    /// Die Oberflaeche ruft es nur ueber Task.Run auf und sperrt den Knopf solange.
    /// </summary>
    [Fact]
    public async Task AnlegenImHintergrund()
    {
        using var drin = new ManualResetEventSlim();
        using var frei = new ManualResetEventSlim();
        var umgebung = Umgebung();
        var langsam = umgebung with
        {
            XdgDesktop = () =>
            {
                drin.Set();
                Assert.True(frei.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));   // xdg-user-dir haengt, bis der Test freigibt
                return Desktop;
            },
        };
        var aufgabe = Task.Run(() => Verknuepfung.Anlegen(langsam));
        // der Aufrufer (die Oberflaeche) laeuft weiter, waehrend xdg-user-dir haengt
        Assert.True(drin.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.False(aufgabe.IsCompleted);
        frei.Set();
        var ergebnis = await aufgabe;
        Assert.True(ergebnis.Ok);
        Assert.True(File.Exists(Menue));
        Assert.True(File.Exists(Path.Combine(Desktop, Verknuepfung.DateiName)));
        var dienst = File.ReadAllText(Path.Combine(Pfade.Wurzel, "src", "Lernplattform.App", "Programm", "VerknuepfungDienst.cs"));
        Assert.Contains("await Task.Run(() => Verknuepfung.Anlegen(umgebung));", dienst, StringComparison.Ordinal);
        Assert.DoesNotContain("AnlegenUndMelden()", dienst, StringComparison.Ordinal);
        var optionen = File.ReadAllText(Path.Combine(Pfade.Wurzel, "src", "Lernplattform.App", "Views", "OptionenSeite.cs"));
        Assert.Contains("stand.Text = await VerknuepfungDienst.AnlegenUndMeldenAsync();", optionen, StringComparison.Ordinal);
        Assert.Contains("verknuepfung.IsEnabled = false;", optionen, StringComparison.Ordinal);
    }

    /// <summary>Merker nur ueber Einstellungen.Aendern; andere Schluessel bleiben.</summary>
    [Fact]
    public void MerkerUeberEinstellungenAendern()
    {
        var einstellungen = new Einstellungen(_home);
        Assert.True(einstellungen.Setze(Einstellungen.SchriftSchluessel, "gross"));
        Assert.True(einstellungen.Aendern(werte => Verknuepfung.Setzen(werte, Verknuepfung.Nie)));
        var werte = einstellungen.Laden();
        Assert.Equal("nie", Verknuepfung.Antwort(werte));
        Assert.Equal("gross", werte[Einstellungen.SchriftSchluessel]!.GetValue<string>());
        Assert.False(Verknuepfung.SollFragen(werte, Umgebung()));
    }
}
