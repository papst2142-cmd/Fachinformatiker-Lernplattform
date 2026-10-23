using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Suche;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Linux;

/// <summary>Ergebnis von <see cref="Verknuepfung.Anlegen"/>: Ok, wenn der Eintrag im Anwendungsmenue liegt; Text fuer das Fenster.</summary>
public sealed record VerknuepfungErgebnis(bool Ok, string Text);

/// <summary>
/// Umgebung der Verknuepfung (fuer Tests austauschbar): Benutzerordner, XDG-Variablen, Pfad des AppImage,
/// Desktop-Sitzung, Abfrage "xdg-user-dir DESKTOP", Markierung "gio set ... metadata::trusted true" und
/// das Programmsymbol (PNG).
/// </summary>
public sealed record VerknuepfungUmgebung(
    string Home,
    string? DatenHome,
    string? KonfigHome,
    string? AppImage,
    string? Sitzung,
    Func<string?> XdgDesktop,
    Func<string, bool> GioVertrauen,
    Func<byte[]?> Symbol)
{
    /// <summary>Die echte Umgebung des laufenden Programms.</summary>
    public static VerknuepfungUmgebung Echt(Func<byte[]?> symbol) => new(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
        Environment.GetEnvironmentVariable("APPIMAGE"),
        Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"),
        () => Ausfuehren("xdg-user-dir", ["DESKTOP"], liesAusgabe: true),
        pfad => Ausfuehren("gio", ["set", pfad, "metadata::trusted", "true"], liesAusgabe: false) is not null,
        symbol);

    /// <summary>Befehl mit 5 s Zeitgrenze; Ausgabe (oder "" ohne Lesen) bei Rueckgabe 0, sonst null.</summary>
    private static string? Ausfuehren(string befehl, string[] argumente, bool liesAusgabe)
    {
        try
        {
            var start = new ProcessStartInfo(befehl)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in argumente)
            {
                start.ArgumentList.Add(argument);
            }
            using var prozess = Process.Start(start);
            if (prozess is null)
            {
                return null;
            }
            var ausgabe = prozess.StandardOutput.ReadToEndAsync();
            _ = prozess.StandardError.ReadToEndAsync();
            if (!prozess.WaitForExit(5000))
            {
                prozess.Kill(true);
                return null;
            }
            return prozess.ExitCode == 0 ? (liesAusgabe ? ausgabe.Result.Trim() : "") : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>
/// Phase 6 W2 (VE5, Wortliste W2 Spalte "C# a"): Verknuepfung unter Linux wie fisi_verknuepfung.py in
/// Python 0.63.2, aber im Anwendungsmenue (~/.local/share/applications, immer) und auf dem Desktop (nur,
/// wenn es einen Desktop-Ordner gibt). Nur im installierten AppImage; die Datei startet das AppImage an
/// seinem aktuellen Ort (Variable APPIMAGE). Der Knopf erneuert eine vorhandene Verknuepfung (gleicher
/// Dateiname, nie eine zweite). Es wird nichts ausserhalb des Benutzerordners geschrieben; die
/// Python-Verknuepfung fisi-lernplattform.desktop bleibt unberuehrt. renew_own entfaellt (Wortliste 3).
/// </summary>
public static class Verknuepfung
{
    public const string DateiName = "fachinformatiker-lernplattform.desktop";
    public const string SymbolName = "fachinformatiker-lernplattform";
    public const string Merker = "X-Lernplattform-Verknuepfung=true";

    /// <summary>
    /// Fensterklasse (WM_CLASS) des Programms, gemessen unter X11 (Xvfb, Phase 6 W2): Name und Klasse
    /// "Lernplattform" (Avalonia nimmt den Namen der Programmdatei). Damit ordnet die Taskleiste das Fenster zu.
    /// </summary>
    public const string Fensterklasse = "Lernplattform";

    /// <summary>Merker in einstellungen.json wie Python: "fragen" (Vorgabe), "nie", "erledigt".</summary>
    public const string Schluessel = "desktop_verknuepfung";
    public const string Fragen = "fragen";
    public const string Nie = "nie";
    public const string Erledigt = "erledigt";

    // Inhalt wie ENTRY in fisi_verknuepfung.py (ASCII), Name/Datei/Symbol/Fensterklasse der C#-Linie
    private const string Eintrag = "[Desktop Entry]\n"
        + "Type=Application\n"
        + "Name=Fachinformatiker Lernplattform\n"
        + "GenericName=Lernprogramm\n"
        + "Comment=Lernprogramm fuer die Pruefungsvorbereitung zum Fachinformatiker (Schwerpunkt Systemintegration)\n"
        + "Exec={exec}\n"
        + "Icon={icon}\n"
        + "Terminal=false\n"
        + "Categories=Education;\n"
        + "Keywords=Fachinformatiker;FI;FISI;IHK;Lernen;Netzwerk;Subnetting;RAID;\n"
        + "StartupWMClass=" + Fensterklasse + "\n"
        + Merker + "\n";

    /// <summary>Knopf und Rueckfrage nur unter Linux im installierten AppImage (Velopack, Variable APPIMAGE gesetzt).</summary>
    public static bool Unterstuetzt(bool linux, bool installiert, string? appImage) => linux && installiert && !string.IsNullOrEmpty(appImage);

    /// <summary>answer(): gespeicherter Merker, unbekannte Werte gelten als "fragen".</summary>
    public static string Antwort(JsonObject werte) =>
        werte[Schluessel] is JsonValue wert && wert.TryGetValue<string>(out var text) && text is Nie or Erledigt ? text : Fragen;

    /// <summary>set_answer() ueber Einstellungen.Aendern (Sperre wie alle Schreibzugriffe auf einstellungen.json).</summary>
    public static void Setzen(JsonObject werte, string antwort) => werte[Schluessel] = antwort;

    /// <summary>
    /// should_ask(): fragen, solange weder "Ja" noch "Nicht mehr fragen" gewaehlt wurde und keine
    /// Verknuepfung im Anwendungsmenue liegt ("Nein" fragt beim naechsten Start wieder).
    /// </summary>
    public static bool SollFragen(JsonObject werte, VerknuepfungUmgebung umgebung) =>
        Antwort(werte) == Fragen && !File.Exists(MenuePfad(umgebung));

    /// <summary>status_text(): Zeile neben dem Knopf (W2-2, W2-3).</summary>
    public static string StatusText(VerknuepfungUmgebung umgebung) =>
        File.Exists(MenuePfad(umgebung)) ? VerknuepfungTexte.Vorhanden : VerknuepfungTexte.Keine;

    private static string Daten(VerknuepfungUmgebung umgebung) =>
        string.IsNullOrEmpty(umgebung.DatenHome) ? Path.Combine(umgebung.Home, ".local", "share") : umgebung.DatenHome;

    /// <summary>Ordner des Anwendungsmenues des Benutzers ($XDG_DATA_HOME/applications).</summary>
    public static string MenueOrdner(VerknuepfungUmgebung umgebung) => Path.Combine(Daten(umgebung), "applications");

    public static string MenuePfad(VerknuepfungUmgebung umgebung) => Path.Combine(MenueOrdner(umgebung), DateiName);

    /// <summary>
    /// desktop_dir(): Desktop-Ordner aus "xdg-user-dir DESKTOP", sonst aus user-dirs.dirs, sonst ~/Desktop;
    /// null, wenn er fehlt oder der Benutzerordner selbst ist (zaehlt nicht als Desktop).
    /// </summary>
    public static string? DesktopOrdner(VerknuepfungUmgebung umgebung)
    {
        var home = umgebung.Home;
        var pfad = umgebung.XdgDesktop();
        if (string.IsNullOrEmpty(pfad))
        {
            pfad = AusUserDirs(umgebung);
        }
        if (string.IsNullOrEmpty(pfad))
        {
            pfad = Path.Combine(home, "Desktop");
        }
        if (pfad == "~" || pfad.StartsWith("~/", StringComparison.Ordinal))
        {
            pfad = home + pfad[1..];
        }
        pfad = Path.GetFullPath(pfad);
        return Normal(pfad) == Normal(Path.GetFullPath(home)) || !Directory.Exists(pfad) ? null : pfad;
    }

    private static string Normal(string pfad) => pfad.Length > 1 ? pfad.TrimEnd('/') : pfad;

    // _from_user_dirs: ~/.config/user-dirs.dirs, Zeile XDG_DESKTOP_DIR="$HOME/..."
    private static string? AusUserDirs(VerknuepfungUmgebung umgebung)
    {
        var konfig = string.IsNullOrEmpty(umgebung.KonfigHome) ? Path.Combine(umgebung.Home, ".config") : umgebung.KonfigHome;
        try
        {
            foreach (var roh in File.ReadLines(Path.Combine(konfig, "user-dirs.dirs"), Encoding.UTF8))
            {
                var zeile = roh.Trim();
                if (zeile.StartsWith("XDG_DESKTOP_DIR=", StringComparison.Ordinal))
                {
                    return zeile[(zeile.IndexOf('=') + 1)..].Trim().Trim('"').Replace("$HOME", umgebung.Home, StringComparison.Ordinal);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        return null;
    }

    /// <summary>quote_exec(): bei Leer- oder Sonderzeichen in Anfuehrungszeichen, mit \ vor " ` $ \ (Desktop-Entry-Spezifikation).</summary>
    public static string ExecZeile(string pfad)
    {
        const string besonders = " \t\n\"'\\><~|&;$*?#()`";
        if (pfad.IndexOfAny(besonders.ToCharArray()) < 0)
        {
            return pfad;
        }
        var text = new StringBuilder("\"");
        foreach (var zeichen in pfad)
        {
            if (zeichen is '"' or '`' or '$' or '\\')
            {
                text.Append('\\');
            }
            text.Append(zeichen);
        }
        return text.Append('"').ToString();
    }

    /// <summary>entry_text(): Inhalt der .desktop-Datei.</summary>
    public static string Inhalt(string exec, string symbol) =>
        Eintrag.Replace("{exec}", ExecZeile(exec), StringComparison.Ordinal).Replace("{icon}", symbol, StringComparison.Ordinal);

    /// <summary>
    /// icon_path() fuer das AppImage: Kopie des Symbols im Benutzerordner (fester Pfad, auch wenn das AppImage
    /// wandert); misslingt die Kopie, nur der Name wie Python.
    /// </summary>
    public static string SymbolPfad(VerknuepfungUmgebung umgebung)
    {
        var ordner = Path.Combine(Daten(umgebung), "icons", "hicolor", "512x512", "apps");
        var kopie = Path.Combine(ordner, SymbolName + ".png");
        try
        {
            if (umgebung.Symbol() is not { Length: > 0 } bytes)
            {
                return SymbolName;
            }
            Directory.CreateDirectory(ordner);
            File.WriteAllBytes(kopie, bytes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SymbolName;
        }
        return kopie;
    }

    /// <summary>desktop_session(): "gnome", "kde" oder "andere" (XDG_CURRENT_DESKTOP).</summary>
    public static string Sitzung(string? namen)
    {
        var klein = PythonKlein.Klein(namen ?? "");
        if (klein.Contains("gnome", StringComparison.Ordinal) || klein.Contains("unity", StringComparison.Ordinal) || klein.Contains("ubuntu", StringComparison.Ordinal))
        {
            return "gnome";
        }
        return klein.Contains("kde", StringComparison.Ordinal) || klein.Contains("plasma", StringComparison.Ordinal) ? "kde" : "andere";
    }

    // _trust(): GNOME markiert per gio; KDE genuegt das Ausfuehrrecht; sonst unsicher
    private static bool Vertrauen(string pfad, VerknuepfungUmgebung umgebung) => Sitzung(umgebung.Sitzung) switch
    {
        "gnome" => umgebung.GioVertrauen(pfad),
        "kde" => true,
        _ => false,
    };

    /// <summary>
    /// create() + message(): legt die Verknuepfung im Anwendungsmenue und (wenn vorhanden) auf dem Desktop an
    /// oder erneuert sie. Je Ort eine Zeile W2-7/W2-8 mit dem Pfad; ohne Desktop-Ordner W2-9; liess GNOME die
    /// Desktop-Datei nicht als vertrauenswuerdig markieren, W2-11; ein Fehler W2-10 mit der Fehlerart.
    /// Ok = der Eintrag im Anwendungsmenue liegt (dann "erledigt").
    /// </summary>
    public static VerknuepfungErgebnis Anlegen(VerknuepfungUmgebung umgebung)
    {
        if (string.IsNullOrEmpty(umgebung.AppImage))
        {
            return new(false, Einsetzen(VerknuepfungTexte.Fehler, VerknuepfungTexte.ProgrammUnbekannt));
        }
        var inhalt = Inhalt(umgebung.AppImage, SymbolPfad(umgebung));
        var zeilen = new List<string>();
        var menue = MenuePfad(umgebung);
        try
        {
            zeilen.Add(Schreiben(menue, inhalt, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new(false, Einsetzen(VerknuepfungTexte.Fehler, e.GetType().Name));
        }
        var text = string.Join("\n", zeilen);
        if (DesktopOrdner(umgebung) is not { } desktop)
        {
            return new(true, text + "\n" + VerknuepfungTexte.KeinDesktop);
        }
        var pfad = Path.Combine(desktop, DateiName);
        try
        {
            // Ausfuehrrecht: GNOME und KDE verlangen es auf dem Desktop
            text += "\n" + Schreiben(pfad, inhalt, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new(true, text + "\n" + Einsetzen(VerknuepfungTexte.Fehler, e.GetType().Name));
        }
        return new(true, Vertrauen(pfad, umgebung) ? text : text + VerknuepfungTexte.Unsicher);
    }

    // Erst .tmp schreiben, Rechte setzen, dann ersetzen (os.replace); Zeile W2-7 oder W2-8
    private static string Schreiben(string pfad, string inhalt, UnixFileMode rechte)
    {
        var erneuert = File.Exists(pfad);
        var temp = pfad + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
            File.WriteAllText(temp, inhalt, new UTF8Encoding(false));
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temp, rechte);
            }
            File.Move(temp, pfad, true);
        }
        catch (Exception)
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // wie Python: die Hilfsdatei bleibt dann liegen
            }
            throw;
        }
        return Einsetzen(erneuert ? VerknuepfungTexte.Erneuert : VerknuepfungTexte.Angelegt, pfad);
    }

    private static string Einsetzen(string vorlage, string wert) => vorlage.Replace("%s", wert, StringComparison.Ordinal);
}
