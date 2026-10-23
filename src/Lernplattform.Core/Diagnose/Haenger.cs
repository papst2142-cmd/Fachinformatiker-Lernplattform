using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Diagnose;

/// <summary>
/// Phase 5 BE8: Haenger-Diagnose wie fisi_haenger (Python v0.63.2, nur PC). Haelt fest, wo das
/// Programm steht, wenn der Oberflaechen-Faden haengt oder lange blockiert ist; nur Diagnose,
/// kein anderes Verhalten.
/// <list type="bullet">
/// <item>Herzschlag: die Oberflaeche ruft <see cref="Herzschlag"/> alle 500 ms auf.</item>
/// <item>Waechter (eigener Hintergrund-Faden, liest nur): prueft jede Sekunde das Alter des
/// Herzschlags. Ab 5 s (bei bekannten langen Vorgaengen ab 30 s) eine Zeile in fehler.log und
/// haenger.log mit Seite, Vorgang und (Linux) den Wartepunkten aller Faeden; endet die
/// Blockade, eine Zeile "wieder frei". Hoechstens 10 Blockaden je Sitzung in fehler.log.</item>
/// <item>Stelle (D6, statt des Python-Stapels): steht der Herzschlag 30 s, schreibt der Waechter
/// Seite, Vorgang und Wartepunkte nach haenger.log, nach weiteren 90 s noch einmal; je Sitzung
/// hoechstens 3 Mal. Einen Code-Stapel gibt es nicht (keine Fremdbibliothek).</item>
/// <item>Abruf (Linux, macOS): kill -USR1 schreibt dieselben Angaben, das Programm laeuft weiter.</item>
/// <item>Beim Schliessen: lebt der Prozess 5 s danach noch, eine Zeile mit den Angaben.</item>
/// </list>
/// Grundsatz: Die Diagnose zeigt nie einen Fehler und stoppt nie das Programm; jede Funktion
/// faengt alle Fehler ab, geht die Datei nicht auf, bleibt sie still aus.
/// </summary>
public static class Haenger
{
    public const string LogName = "haenger.log";
    public const int LogMax = 512 * 1024;      // groesser -> beim Start auf LogBehalten kuerzen
    public const int LogBehalten = 256 * 1024;
    public const int BerichtMax = 16 * 1024;
    public const int TaktMs = 500;             // BEAT_MS
    public const double NeuSetzenSekunden = 5.0;   // ARM_SECONDS
    public const int StelleNach = 30;          // DUMP_AFTER
    public const int StelleWieder = 90;        // DUMP_AGAIN
    public const int StellenJeSitzung = 3;     // MAX_AUTO_DUMPS
    public const double PruefSekunden = 1.0;   // CHECK_SECONDS
    public const int BlockSekunden = 5;
    public const int BekanntSekunden = 30;
    public static readonly IReadOnlyList<string> Bekannt = ["seite", "darstellung", "schrift", "vorladen", "abgleich"];
    public const int ZeilenJeSitzung = 10;     // MAX_LINES
    public const double FreiSekunden = 2.0;
    public const double MessSekunden = 2.0;    // Leistungsmessung: Ereignis "blockiert" ab ... s
    public const string Bereit = "bereit";     // IDLE

    private static readonly object Sperre = new();
    private static FileStream? _log;
    private static PosixSignalRegistration? _signal;
    private static Thread? _waechter;

    private sealed class Blockade
    {
        public double Start;
        public bool Gemeldet;
        public bool Notiert;
        public int Stellen;
        public string Seite = "";
        public string Vorgang = Bereit;
    }

    private static double? _herzschlag;
    private static double? _gesetzt;
    private static int _gesetztFuer;
    private static int _stellen;
    private static volatile string _seite = "";
    private static volatile string _vorgang = Bereit;
    private static Func<bool>? _schliessend;
    private static int _zeilen;
    private static Blockade? _blockade;
    private static (string Vorgang, double Ende)? _zuletzt;
    private static Func<string> _wartepunkte = () => FaedenText();
    private static Action<string> _fehlerlog = text => Fehlerprotokoll.Schreiben(text);

    /// <summary>Monotone Uhr in Sekunden (time.monotonic).</summary>
    public static double Jetzt() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    /// <summary>log_path: haenger.log im Datenordner neben der Datenbank.</summary>
    public static string Pfad(string? ordner = null) => Path.Combine(ordner ?? Ablage.Datenordner(), LogName);

    /// <summary>
    /// setup: beim Start einmal aufrufen. true, wenn haenger.log offen ist. schliessend(): true, waehrend
    /// das Programm schliesst (der Waechter schweigt dann). waechter false nur fuer Tests (dort ruft der
    /// Test <see cref="Pruefen"/> selbst auf).
    /// </summary>
    public static bool Einrichten(string version, string? ordner = null, Func<bool>? schliessend = null, string? plattform = null,
        string zusatz = "", bool waechter = true)
    {
        plattform ??= Fehlerprotokoll.System();
        _schliessend = schliessend;
        try
        {
            var pfad = Pfad(ordner);
            lock (Sperre)
            {
                if (_log is null)
                {
                    Kuerzen(pfad);
                    _log = new FileStream(pfad, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                    var zeile = "Start | Version " + (version.Length > 0 ? version : "?") + " | " + plattform
                        + " | pid " + Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
                    if (zusatz.Length > 0)
                    {
                        zeile += " | " + zusatz;
                    }
                    Schreiben(zeile);
                }
            }
            if (plattform != "win32" && (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) && _signal is null)
            {
                // SIGUSR1: Linux 10, macOS 30 (rohe Signalnummer, .NET kennt den Namen nicht)
                var nummer = OperatingSystem.IsMacOS() ? 30 : 10;
                _signal = PosixSignalRegistration.Create((PosixSignal)nummer, kontext =>
                {
                    kontext.Cancel = true;   // nicht beenden
                    Abruf();
                });
            }
        }
        catch (Exception)
        {
            lock (Sperre)
            {
                _log?.Dispose();
                _log = null;
            }
        }
        if (waechter)
        {
            WaechterStarten();
        }
        return _log is not null;
    }

    public static bool Eingerichtet => _log is not null;

    /// <summary>_trim: groesser als <see cref="LogMax"/> -> die letzten <see cref="LogBehalten"/> Bytes ab einem Zeilenanfang.</summary>
    private static void Kuerzen(string pfad)
    {
        try
        {
            var info = new FileInfo(pfad);
            if (!info.Exists || info.Length <= LogMax)
            {
                return;
            }
            byte[] rest;
            using (var datei = File.OpenRead(pfad))
            {
                datei.Seek(-LogBehalten, SeekOrigin.End);
                rest = new byte[LogBehalten];
                datei.ReadExactly(rest);
            }
            var schnitt = Array.IndexOf(rest, (byte)'\n');
            File.WriteAllBytes(pfad, schnitt >= 0 ? rest[(schnitt + 1)..] : rest);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>_write: eine Zeile nach haenger.log (mit Uhrzeit); still bei Fehlern.</summary>
    private static void Schreiben(string text)
    {
        lock (Sperre)
        {
            if (_log is null)
            {
                return;
            }
            try
            {
                var zeit = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                var bytes = new UTF8Encoding(false).GetBytes("=== " + zeit + " | " + text + " ===\n");
                _log.Write(bytes);
                _log.Flush(true);
            }
            catch (Exception)
            {
            }
        }
    }

    private static void WaechterStarten()
    {
        lock (Sperre)
        {
            if (_waechter is not null)
            {
                return;
            }
            try
            {
                _waechter = new Thread(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(PruefSekunden));
                        try
                        {
                            Pruefen();
                        }
                        catch (Exception)
                        {
                        }
                    }
                })
                { IsBackground = true, Name = "lp-waechter" };
                _waechter.Start();
            }
            catch (Exception)
            {
                _waechter = null;
            }
        }
    }

    /// <summary>Stelle fuer den naechsten Waechter-Durchlauf vormerken (statt faulthandler.dump_traceback_later).</summary>
    private static void Setzen(int sekunden, double jetzt)
    {
        lock (Sperre)
        {
            if (_log is null || _stellen >= StellenJeSitzung)
            {
                return;
            }
            _gesetzt = jetzt;
            _gesetztFuer = sekunden;
        }
    }

    /// <summary>beat: vom Herzschlag im Oberflaechen-Faden. Liefert die Verspaetung in Sekunden gegenueber dem Takt.</summary>
    public static double Herzschlag(double? jetzt = null)
    {
        try
        {
            var nun = jetzt ?? Jetzt();
            var letzter = _herzschlag;
            _herzschlag = nun;
            if (_gesetzt is not { } gesetzt || nun - gesetzt >= NeuSetzenSekunden || _gesetztFuer != StelleNach)
            {
                Setzen(StelleNach, nun);
            }
            return letzter is { } l ? Math.Max(0.0, nun - l - TaktMs / 1000.0) : 0.0;
        }
        catch (Exception)
        {
            return 0.0;
        }
    }

    /// <summary>seite: angezeigte Seite merken.</summary>
    public static void Seite(string? name) => _seite = name ?? "";

    /// <summary>vorgang: laufenden Vorgang merken (z. B. "darstellung", "seite:optionen"). Liefert den vorherigen.</summary>
    public static string Vorgang(string? name, double? jetzt = null)
    {
        var vorher = _vorgang;
        name = string.IsNullOrEmpty(name) ? Bereit : name;
        _vorgang = name;
        if (vorher != Bereit && name != vorher)
        {
            _zuletzt = (vorher, jetzt ?? Jetzt());
        }
        return vorher;
    }

    /// <summary>set_closing.</summary>
    public static void Schliessend(Func<bool>? schliessend) => _schliessend = schliessend;

    /// <summary>recent: laufender Vorgang oder, wenn keiner laeuft, der letzte, der nach seit endete; sonst bereit.</summary>
    public static string Kuerzlich(double seit)
    {
        var vorgang = _vorgang;
        if (vorgang != Bereit)
        {
            return vorgang;
        }
        return _zuletzt is { } z && z.Ende >= seit ? z.Vorgang : Bereit;
    }

    /// <summary>current: (Seite, Vorgang).</summary>
    public static (string Seite, string Vorgang) Aktuell() => (_seite, _vorgang);

    private static bool IstBekannt(string name) => Bekannt.Contains(name.Split(':', 2)[0]);

    /// <summary>threads_text: Linux "tid name Zustand Wartepunkt; ..." aller Faeden, sonst "".</summary>
    public static string FaedenText(string pid = "self")
    {
        var basis = "/proc/" + pid + "/task";
        if (!Directory.Exists(basis))
        {
            return "";
        }
        var teile = new List<string>();
        try
        {
            var ids = Directory.GetDirectories(basis).Select(Path.GetFileName)
                .Select(n => long.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out var z) ? z : -1)
                .Where(z => z >= 0).Order();
            foreach (var id in ids)
            {
                try
                {
                    var ordner = Path.Combine(basis, id.ToString(CultureInfo.InvariantCulture));
                    var name = File.ReadAllText(Path.Combine(ordner, "comm")).Trim();
                    var stat = File.ReadAllText(Path.Combine(ordner, "stat"));
                    var zustand = stat[(stat.LastIndexOf(')') + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                    var wchan = File.ReadAllText(Path.Combine(ordner, "wchan")).Trim();
                    teile.Add(id.ToString(CultureInfo.InvariantCulture) + " " + name + " " + zustand + " " + (wchan.Length > 0 ? wchan : "-"));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or IndexOutOfRangeException)
                {
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return "";
        }
        return string.Join("; ", teile);
    }

    private static bool Schliesst()
    {
        try
        {
            return _schliessend?.Invoke() ?? false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string Meldung(string text, double alter, string seite, string vorgang)
    {
        var zeile = "Hänger-Diagnose: " + text.Replace("%(s)s", Zahltext.Komma(alter, 1), StringComparison.Ordinal)
            + " (Seite: " + (seite.Length > 0 ? seite : "-") + ", Vorgang: " + vorgang + ")";
        var warten = _wartepunkte();
        if (warten.Length > 0)
        {
            zeile += "\nWartepunkte: " + warten;
        }
        return zeile;
    }

    /// <summary>check: ein Durchlauf des Waechters. Ergebnis "frei", "blockiert", "stelle" oder null.</summary>
    public static string? Pruefen(double? jetzt = null)
    {
        var nun = jetzt ?? Jetzt();
        if (_herzschlag is not { } letzter)
        {
            return null;   // Herzschlag noch nicht gestartet (Programmstart)
        }
        if (Schliesst())
        {
            _blockade = null;
            return null;
        }
        var alter = nun - letzter;
        var blockade = _blockade;
        if (alter < FreiSekunden)
        {
            if (blockade is not null)
            {
                _blockade = null;
                if (blockade.Gemeldet || blockade.Stellen > 0)
                {
                    var text = Meldung("Hauptfaden wieder frei nach %(s)s s", nun - blockade.Start, blockade.Seite, blockade.Vorgang);
                    Schreiben(text);
                    if (blockade.Notiert)
                    {
                        _fehlerlog(text);
                    }
                    return "frei";
                }
            }
            return null;
        }
        if (blockade is null)
        {
            blockade = new Blockade { Start = letzter, Seite = _seite, Vorgang = Kuerzlich(letzter) };
            _blockade = blockade;
        }
        string? ergebnis = null;
        if (blockade.Vorgang == Bereit)
        {
            blockade.Vorgang = _vorgang;
        }
        var grenze = IstBekannt(blockade.Vorgang) ? BekanntSekunden : BlockSekunden;
        if (!blockade.Gemeldet && alter >= grenze)
        {
            blockade.Gemeldet = true;
            var text = Meldung("Hauptfaden seit %(s)s s ohne Rückruf", alter, blockade.Seite, blockade.Vorgang);
            Schreiben(text);
            if (_zeilen < ZeilenJeSitzung)
            {
                _fehlerlog(text);
                blockade.Notiert = true;
            }
            else if (_zeilen == ZeilenJeSitzung)
            {
                _fehlerlog("Hänger-Diagnose: Weitere Blockaden in dieser Sitzung nicht einzeln notiert.");
            }
            _zeilen++;
            ergebnis = "blockiert";
        }
        // Stelle: die vorgemerkte Zeit ist abgelaufen, ohne dass der Herzschlag sie neu gesetzt hat
        lock (Sperre)
        {
            if (_gesetzt is { } gesetzt && nun - gesetzt >= _gesetztFuer)
            {
                _gesetzt = null;
                _stellen++;
                blockade.Stellen++;
                var stelle = "Stelle " + _stellen.ToString(CultureInfo.InvariantCulture) + " (Hauptfaden seit "
                    + ((long)alter).ToString(CultureInfo.InvariantCulture) + " s still, Seite: "
                    + (blockade.Seite.Length > 0 ? blockade.Seite : "-") + ", Vorgang: " + blockade.Vorgang + ")";
                var warten = _wartepunkte();
                Schreiben(warten.Length > 0 ? stelle + "\nWartepunkte: " + warten : stelle);
                if (blockade.Stellen == 1)
                {
                    Setzen(StelleWieder, nun);   // der Herzschlag steht: der Waechter merkt die zweite Stelle vor
                }
                ergebnis ??= "stelle";
            }
        }
        return ergebnis;
    }

    /// <summary>Abruf ueber das Signal USR1 (Linux, macOS): Seite, Vorgang und Wartepunkte nach haenger.log.</summary>
    public static void Abruf()
    {
        try
        {
            var (seite, vorgang) = Aktuell();
            var text = "Abruf (Signal USR1): Seite: " + (seite.Length > 0 ? seite : "-") + ", Vorgang: " + vorgang;
            var warten = _wartepunkte();
            Schreiben(warten.Length > 0 ? text + "\nWartepunkte: " + warten : text);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// dump_on_close: "schliessen" notieren; lebt der Prozess sekunden danach noch, eine Zeile mit Seite,
    /// Vorgang und Wartepunkten. Aendert nichts am Ablauf.
    /// </summary>
    public static void BeimSchliessen(int sekunden = 5)
    {
        try
        {
            lock (Sperre)
            {
                if (_log is null)
                {
                    return;
                }
                _gesetzt = null;   // keine automatische Stelle mehr
            }
            Schreiben("schliessen");
            new Thread(() =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(sekunden));
                var (seite, vorgang) = Aktuell();
                var text = "Noch offen " + sekunden.ToString(CultureInfo.InvariantCulture) + " s nach dem Schliessen (Seite: "
                    + (seite.Length > 0 ? seite : "-") + ", Vorgang: " + vorgang + ")";
                var warten = _wartepunkte();
                Schreiben(warten.Length > 0 ? text + "\nWartepunkte: " + warten : text);
            })
            { IsBackground = true, Name = "lp-schliessen" }.Start();
        }
        catch (Exception)
        {
        }
    }

    /// <summary>state_text: Zustand fuer die Optionen.</summary>
    public static string ZustandText(string? pfad = null)
    {
        try
        {
            pfad ??= Pfad();
            var text = Lesen(pfad, BerichtMax * 4);
            if (text.Length == 0 || !text.Contains("Hauptfaden", StringComparison.Ordinal))
            {
                return HaengerTexte.Keine;
            }
            var info = new FileInfo(pfad);
            return HaengerTexte.Datei
                .Replace("%(kb)s", Zahltext.Komma(info.Length / 1024.0, 1), StringComparison.Ordinal)
                .Replace("%(zeit)s", info.LastWriteTime.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return HaengerTexte.Keine;
        }
    }

    private static string Lesen(string pfad, int grenze)
    {
        try
        {
            using var datei = new FileStream(pfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (datei.Length > grenze)
            {
                datei.Seek(-grenze, SeekOrigin.End);
            }
            using var puffer = new MemoryStream();
            datei.CopyTo(puffer);
            return new UTF8Encoding(false, false).GetString(puffer.ToArray());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    /// <summary>Nur fuer Tests: Zustand zuruecksetzen, Datei schliessen, Wartepunkte und fehler.log ersetzen.</summary>
    internal static void FuerTests(Func<string>? wartepunkte = null, Action<string>? fehlerlog = null)
    {
        lock (Sperre)
        {
            _log?.Dispose();
            _log = null;
            _herzschlag = null;
            _gesetzt = null;
            _gesetztFuer = 0;
            _stellen = 0;
            _seite = "";
            _vorgang = Bereit;
            _schliessend = null;
            _zeilen = 0;
            _blockade = null;
            _zuletzt = null;
            _wartepunkte = wartepunkte ?? (() => FaedenText());
            _fehlerlog = fehlerlog ?? (text => Fehlerprotokoll.Schreiben(text));
        }
    }
}
