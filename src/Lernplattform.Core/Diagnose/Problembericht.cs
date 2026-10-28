using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Diagnose;

/// <summary>
/// Phase 5 BE5: Bericht fuer "Problem melden" wie fisi_diagnose.report_lines und build_report
/// (Python v0.63.2, Vergleichswerte G38): Kopf mit Version, Geraet, Laufzeit, Datenordner,
/// Datenbankgroesse, Zaehlwerten, Tagesziel und Abgleich ja/nein, dann fehler.log, update.log,
/// haenger.log und laeuft.info (nur wenn vorhanden). Danach werden Zugangsschluessel und
/// Benutzerpfade entfernt (Datenschutz, G10). Ohne Oberflaeche: PC und Handy zeigen, kopieren
/// und speichern denselben Text.
/// Abweichung (Wortliste W2): Zeile "Python: …" heisst "Laufzeit: …" (.NET); eine nicht lesbare
/// Datenbank nennt statt des Systemtexts nur die Fehlerart. Ab Nachtrag 1 zu P7-13 folgt, wenn vorhanden,
/// spiel_bilder.log (Bildzeiten des Spiels).
/// </summary>
public static class Problembericht
{
    public const string HaengerName = "haenger.log";
    public const int HaengerMax = 16 * 1024;
    public const string InfoName = "laeuft.info";
    public const int InfoMax = 4096;
    public const string UpdateName = "update.log";
    public const int UpdateMax = 16 * 1024;
    public const string Endung = ".txt";
    public const string Programmname = "Fachinformatiker Lernplattform";   // APP_DISPLAY_NAME

    /// <summary>GAME_TABLES (fisi_core).</summary>
    public static readonly IReadOnlyList<string> Spieltabellen = ["spiel_ereignisse"];

    /// <summary>default_name: z. B. Lernplattform-Problembericht_2026-10-02_1423.txt.</summary>
    public static string StandardName(DateTime jetzt) =>
        "Lernplattform-Problembericht_" + jetzt.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture) + Endung;

    /// <summary>_size_text: Bytes, KB oder MB mit einer Nachkommastelle und Komma.</summary>
    public static string GroesseText(long groesse)
    {
        if (groesse < 1024)
        {
            return groesse.ToString(CultureInfo.InvariantCulture) + " Bytes";
        }
        if (groesse < 1024 * 1024)
        {
            return Zahltext.Komma(groesse / 1024.0, 1) + " KB";
        }
        return Zahltext.Komma(groesse / 1024.0 / 1024.0, 1) + " MB";
    }

    /// <summary>_db_size: Datenbank plus -wal und -journal (fehlende Dateien zaehlen 0).</summary>
    public static long DatenbankGroesse(string dbPfad)
    {
        long summe = 0;
        foreach (var endung in (ReadOnlySpan<string>)["", "-wal", "-journal"])
        {
            try
            {
                var datei = new FileInfo(dbPfad + endung);
                if (datei.Exists)
                {
                    summe += datei.Length;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
        return summe;
    }

    /// <summary>
    /// device_text: z. B. "PC · Linux 6.8.0-45-generic (x86_64)", ohne Rechnernamen. system, release,
    /// machine wie Pythons platform.system(), release(), machine(); ohne Angabe vom laufenden System.
    /// </summary>
    /// <param name="plattform">Ersatz, wenn system leer ist (Pythons sys.platform); Vorgabe vom laufenden System.</param>
    public static string GeraetText(string geraet, (string System, string Release, string Machine)? angaben = null, string? plattform = null)
    {
        var (system, release, machine) = angaben ?? Systemangaben();
        if (system.Length == 0)
        {
            system = plattform ?? Fehlerprotokoll.System();   // platform.system() or sys.platform
        }
        var text = string.Join(' ', new[] { system, release }.Where(t => t.Length > 0));
        if (machine.Length > 0)
        {
            text += " (" + machine + ")";
        }
        return geraet.Length > 0 ? geraet + " · " + text : text;
    }

    /// <summary>platform.system(), release(), machine() des laufenden Systems (Python-Schreibweise).</summary>
    public static (string System, string Release, string Machine) Systemangaben()
    {
        try
        {
            var arch = RuntimeInformation.OSArchitecture;
            if (OperatingSystem.IsWindows())
            {
                var v = Environment.OSVersion.Version;
                var release = v.Major == 10 && v.Build >= 22000 ? "11" : v.Major == 10 ? "10" : v.Major.ToString(CultureInfo.InvariantCulture);
                return ("Windows", release, arch switch { Architecture.X64 => "AMD64", Architecture.Arm64 => "ARM64", Architecture.X86 => "x86", _ => arch.ToString() });
            }
            // Linux, Android und macOS: uname -s / -r wie in RuntimeInformation.OSDescription ("Linux 6.8.0-45-generic #45 …")
            var teile = RuntimeInformation.OSDescription.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var name = OperatingSystem.IsMacOS() ? "Darwin" : "Linux";
            var kern = teile.Length > 1 ? teile[1] : "";
            var maschine = arch switch
            {
                Architecture.X64 => "x86_64",
                Architecture.Arm64 => OperatingSystem.IsMacOS() ? "arm64" : "aarch64",
                Architecture.X86 => "i686",
                _ => arch.ToString(),
            };
            return (name, kern, maschine);
        }
        catch (Exception)
        {
            return ("", "", "");
        }
    }

    /// <summary>Laufzeit statt der Python-Version (Wortliste W2), z. B. ".NET 10.0.0".</summary>
    public static string Laufzeit() => RuntimeInformation.FrameworkDescription;

    /// <summary>
    /// report_lines: Kopfteil des Berichts (ohne Protokolle). db null: ohne Datenordner und Zaehlwerte.
    /// angaben/laufzeit nur fuer Tests (sonst vom laufenden System).
    /// </summary>
    public static List<string> Zeilen(Lernstand? db, string version, string geraet, JsonObject? einstellungen, DateTime jetzt,
        (string System, string Release, string Machine)? angaben = null, string? laufzeit = null)
    {
        einstellungen ??= [];
        var zeilen = new List<string>
        {
            Programmname + " – Problembericht",
            "Erstellt: " + jetzt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
            "Programmversion: " + (version.Length > 0 ? version : "?"),
            "Gerät: " + GeraetText(geraet, angaben),
            "Laufzeit: " + (laufzeit ?? Laufzeit()),
        };
        if (db is not null)
        {
            zeilen.Add("Datenordner: " + Path.GetDirectoryName(Path.GetFullPath(db.Pfad)));
            zeilen.Add("Datenbankgröße: " + GroesseText(DatenbankGroesse(db.Pfad)));
            try
            {
                var lernen = Wiederherstellung.Ereignistabellen.Sum(t => Anzahl(db, t));
                var spiel = Spieltabellen.Sum(t => Anzahl(db, t));
                zeilen.Add("Ereignisse: " + DashboardTexte.Plural(lernen, "Lern-Eintrag", "Lern-Einträge") + ", "
                    + DashboardTexte.Plural(spiel, "Spielereignis", "Spielereignisse"));
            }
            catch (Exception e)
            {
                // Bericht soll auch bei kaputter Datenbank gehen; kein Systemtext (Wortliste W2)
                zeilen.Add("Ereignisse: nicht lesbar (" + (e is SqliteException s
                    ? "SQLite-Fehler " + s.SqliteErrorCode.ToString(CultureInfo.InvariantCulture) : e.GetType().Name) + ")");
            }
        }
        var ziel = Einstellungen.Ziel(einstellungen);
        zeilen.Add("Tagesziel: " + (ziel.ZielAn ? DashboardTexte.Plural(ziel.ZielAnzahl, "Aufgabe", "Aufgaben") : "aus"));
        zeilen.Add("Abgleich eingerichtet: " + (Abgleich.Eingerichtet(einstellungen) ? "ja" : "nein"));
        return zeilen;
    }

    /// <summary>
    /// Sammelliste 142 (Phase 8 W8b, ohne Vorlage in Python): Zeile zum Ton des Spiels unter dem Kopfteil, gesetzt von der
    /// App (der Core kennt das Audiogeraet nicht); null (Tests, Vergleich G38) = keine Zeile.
    /// </summary>
    public static Func<string>? TonZeile { get; set; }

    /// <summary>Text der Ton-Zeile: ms = Dauer des letzten Oeffnens, keinGeraet = still ohne Audiogeraet.</summary>
    public static string TonText(double ms, bool keinGeraet, bool vorbereitet) =>
        ms >= 0 ? "Ton: Gerät geöffnet in " + ms.ToString("F0", CultureInfo.InvariantCulture) + " ms"
        : keinGeraet ? "Ton: kein Gerät"
        : vorbereitet ? "Ton: Gerät noch nicht geöffnet" : "Ton: Spiel noch nicht geöffnet";

    /// <summary>
    /// build_report: der ganze Bericht, schon gefiltert. logOrdner: Ordner mit fehler.log, haenger.log und
    /// laeuft.info (Vorgabe Datenordner); updateLog: Pfad von update.log (Vorgabe im Datenordner).
    /// umgebung/heim nur fuer Tests (Pfade anonymisieren).
    /// </summary>
    public static string Bauen(Lernstand? db, string version, string geraet, JsonObject? einstellungen, DateTime jetzt,
        string? logOrdner = null, string? updateLog = null, (string System, string Release, string Machine)? angaben = null,
        string? laufzeit = null, IReadOnlyDictionary<string, string>? umgebung = null, string? heim = null)
    {
        einstellungen ??= [];
        var zeilen = Zeilen(db, version, geraet, einstellungen, jetzt, angaben, laufzeit);
        if (TonZeile is { } ton)
        {
            zeilen.Add(ton());
        }
        logOrdner ??= Ablage.Datenordner();
        var log = Fehlerprotokoll.Anzeigen(Path.Combine(logOrdner, Fehlerprotokoll.DateiName));
        zeilen.AddRange(["", "--- fehler.log ---", log.Length > 0 ? log : Fehlerprotokoll.KeinProtokoll]);
        var update = UpdateLogLesen(updateLog ?? Path.Combine(Ablage.Datenordner(), UpdateName));
        if (update.Length > 0)
        {
            zeilen.AddRange(["", "--- update.log ---", update]);
        }
        var haenger = Fehlerprotokoll.Anzeigen(Path.Combine(logOrdner, HaengerName), HaengerMax);
        if (haenger.Length > 0)
        {
            zeilen.AddRange(["", "--- " + HaengerName + " ---", haenger]);
        }
        var info = Fehlerprotokoll.Anzeigen(Path.Combine(logOrdner, InfoName), InfoMax);
        if (info.Length > 0)
        {
            zeilen.AddRange(["", "--- " + InfoName + " ---", info]);
        }
        // Nachtrag 1 zu P7-13 (Wunsch 4, ohne Vorlage in Python): Bildzeiten des Spiels, nur wenn vorhanden
        var bilder = Fehlerprotokoll.Anzeigen(Path.Combine(logOrdner, Spiel.Probe.BildZeiten.DateiName), Spiel.Probe.BildZeiten.DateiMax);
        if (bilder.Length > 0)
        {
            zeilen.AddRange(["", "--- " + Spiel.Probe.BildZeiten.DateiName + " ---", bilder]);
        }
        var text = Datenschutz.Schwaerzen(string.Join("\n", zeilen), Datenschutz.Geheimnisse(Paare(einstellungen)));
        return Datenschutz.PfadeAnonymisieren(text, umgebung, heim);
    }

    /// <summary>read_update_log: Ende von update.log (hoechstens grenze Bytes, ab einer ganzen Zeile), "" ohne Datei.</summary>
    public static string UpdateLogLesen(string pfad, int grenze = UpdateMax)
    {
        byte[] roh;
        long groesse;
        try
        {
            using var datei = File.OpenRead(pfad);
            groesse = datei.Length;
            datei.Seek(Math.Max(0, groesse - grenze), SeekOrigin.Begin);
            using var puffer = new MemoryStream();
            datei.CopyTo(puffer);
            roh = puffer.ToArray();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return "";
        }
        var text = new UTF8Encoding(false, false).GetString(roh);
        if (groesse > grenze)
        {
            text = text[(text.IndexOf('\n') + 1)..];
        }
        return text.Trim();
    }

    private static long Anzahl(Lernstand db, string tabelle)
    {
        var zeilen = db.Abfragen("SELECT COUNT(*) FROM " + tabelle);
        return zeilen.Count > 0 && zeilen[0][0] is { } wert ? Convert.ToInt64(wert, CultureInfo.InvariantCulture) : 0;
    }

    private static IEnumerable<KeyValuePair<string, object?>> Paare(JsonObject werte) =>
        werte.Select(p => new KeyValuePair<string, object?>(p.Key, p.Value is JsonValue v && v.TryGetValue<string>(out var s) ? s : null));
}

