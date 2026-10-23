using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Diagnose;

/// <summary>
/// Phase 5 BE8: Sperre gegen Mehrfachstart wie fisi_einzelstart (Python v0.63.2, nur PC unter Linux
/// und Windows). Ein zweiter Start oeffnet kein zweites Fenster, sondern holt das laufende nach vorn.
/// Sperre: "laeuft.lock" im Datenordner, ohne Teilen geoeffnet (Linux flock, Windows ohne
/// Mitbenutzung); das Betriebssystem gibt sie mit dem Prozess frei, auch nach einem Absturz.
/// Kanal: der zweite Start legt "vordergrund-&lt;token&gt;.signal" an und wartet auf
/// "vordergrund-&lt;token&gt;.antwort": "da" (nach vorn geholt, still beenden; wird die Sperre in den
/// naechsten 3 s doch frei, selbst starten), "endet" (Programm schliesst: auf die Sperre warten) oder
/// keine Antwort (Signal bleibt liegen, bis 25 s auf Sperre oder spaete Antwort achten; danach Start
/// ohne Sperre mit Eintrag in fehler.log). Wer die Sperre bekommt, schreibt "laeuft.info".
/// Grundsatz: Die Sperre verhindert den Start nie. Beendet wird ein zweiter Start nur nach "da".
/// </summary>
public static class Einzelstart
{
    public const string SperrDatei = "laeuft.lock";
    public const string InfoDatei = "laeuft.info";
    public const string SignalEndung = ".signal";
    public const string AntwortEndung = ".antwort";
    public const string Praefix = "vordergrund-";
    public const string AntwortDa = "da";
    public const string AntwortEndet = "endet";

    public const double AntwortSekunden = 3;
    public const double SperrSekunden = 25;    // 12 s Abgleich beim Beenden + 8 s Notausgang + Reserve
    public const double SchrittSekunden = 0.1;
    public const int AbfrageMs = 400;          // POLL_MS
    public const double AltSekunden = 60;      // liegengebliebene Signal-/Antwortdateien

    private static readonly object Sperre = new();
    private static FileStream? _sperre;        // bleibt bis zum Prozessende offen (= Sperre)
    private static (string Ordner, CancellationTokenSource Stopp)? _startAntwort;
    private static CancellationTokenSource? _ende;
    private static Action<string> _fehlerlog = text => Fehlerprotokoll.Schreiben(text);

    private sealed class Belegt : Exception;

    /// <summary>supported: nur Linux und Windows (macOS startet eine laufende App ohnehin nicht zweimal).</summary>
    public static bool Unterstuetzt(string? plattform = null)
    {
        plattform ??= Fehlerprotokoll.System();
        return plattform.StartsWith("linux", StringComparison.Ordinal) || plattform == "win32";
    }

    /// <summary>Ordner, in dem das laufende Programm Signale beantwortet (nach <see cref="Beanspruchen"/>), sonst null.</summary>
    public static string? Ordner => _startAntwort?.Ordner ?? _hoerOrdner;

    private static string? _hoerOrdner;

    /// <summary>Haelt dieser Prozess die Sperre?</summary>
    public static bool Gesperrt => _sperre is not null;

    /// <summary>Sperrt ohne zu warten. Belegt = ein anderer Prozess haelt sie; jeder andere Fehler: Sperre unbekannt.</summary>
    private static FileStream Versuchen(string ordner)
    {
        var pfad = Path.Combine(ordner, SperrDatei);
        try
        {
            return new FileStream(pfad, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException e) when (e is not (FileNotFoundException or DirectoryNotFoundException or PathTooLongException)
            && File.Exists(pfad) && IstBelegt(e))
        {
            throw new Belegt();
        }
    }

    /// <summary>Windows: Freigabe- oder Sperrverletzung; Linux/macOS: flock meldet "wird benutzt" (EWOULDBLOCK, Linux 11, macOS 35).</summary>
    private static bool IstBelegt(IOException e)
    {
        if (OperatingSystem.IsWindows())
        {
            var code = e.HResult & 0xFFFF;
            return code is 32 or 33;   // ERROR_SHARING_VIOLATION, ERROR_LOCK_VIOLATION
        }
        // .NET traegt unter Unix die errno ein (Versuch: Linux 11): nur EWOULDBLOCK ist "belegt",
        // ein anderer E/A-Fehler heisst "Sperre nicht verfuegbar" (Gegenpruefung W2 Fund 8)
        return e.HResult == (OperatingSystem.IsMacOS() ? 35 : 11);
    }

    private static string AntwortPfad(string ordner, string token) => Path.Combine(ordner, Praefix + token + AntwortEndung);

    private static void AntwortSchreiben(string ordner, string token, string text)
    {
        var pfad = AntwortPfad(ordner, token);
        var temp = pfad + ".tmp";
        File.WriteAllText(temp, text, new UTF8Encoding(false));
        File.Move(temp, pfad, true);
    }

    private static void Entfernen(string pfad)
    {
        try
        {
            File.Delete(pfad);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>_signals: Tokens der wartenden zweiten Starts.</summary>
    private static List<string> Signale(string ordner)
    {
        try
        {
            return Directory.GetFiles(ordner, Praefix + "*" + SignalEndung)
                .Select(p => Path.GetFileName(p)!)
                .Select(n => n[Praefix.Length..^SignalEndung.Length]).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>answer_signals: beantwortet alle wartenden zweiten Starts. true = Fenster nach vorn holen.</summary>
    public static bool SignaleBeantworten(string ordner, bool schliessend)
    {
        var tokens = Signale(ordner);
        foreach (var token in tokens)
        {
            Entfernen(Path.Combine(ordner, Praefix + token + SignalEndung));
            try
            {
                AntwortSchreiben(ordner, token, schliessend ? AntwortEndet : AntwortDa);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
        return tokens.Count > 0 && !schliessend;
    }

    /// <summary>_clean_stale: Signal- und Antwortdateien, die laenger als 60 s liegen, entfernen.</summary>
    private static void AlteEntfernen(string ordner, DateTime? jetzt = null)
    {
        var nun = jetzt ?? DateTime.UtcNow;
        foreach (var muster in new[] { Praefix + "*" + SignalEndung, Praefix + "*" + AntwortEndung, Praefix + "*" + AntwortEndung + ".tmp" })
        {
            try
            {
                foreach (var pfad in Directory.GetFiles(ordner, muster))
                {
                    if ((nun - File.GetLastWriteTimeUtc(pfad)).TotalSeconds > AltSekunden)
                    {
                        Entfernen(pfad);
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static string? AntwortWarten(string ordner, string token, double sekunden)
    {
        var pfad = AntwortPfad(ordner, token);
        var ende = Stopwatch.GetTimestamp() + (long)(sekunden * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < ende)
        {
            if (AntwortLesen(pfad) is { } text)
            {
                return text;
            }
            Thread.Sleep(TimeSpan.FromSeconds(SchrittSekunden));
        }
        return null;
    }

    private static string? AntwortLesen(string pfad)
    {
        try
        {
            var text = File.ReadAllText(pfad).Trim();
            Entfernen(pfad);
            return text;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>_wait_for_lock: bis sekunden lang die Sperre versuchen.</summary>
    private static FileStream? SperreWarten(string ordner, double sekunden)
    {
        var ende = Stopwatch.GetTimestamp() + (long)(sekunden * Stopwatch.Frequency);
        while (true)
        {
            try
            {
                return Versuchen(ordner);
            }
            catch (Belegt)
            {
                if (Stopwatch.GetTimestamp() >= ende)
                {
                    return null;
                }
                Thread.Sleep(TimeSpan.FromSeconds(SchrittSekunden));
            }
        }
    }

    /// <summary>Linux: (Zustand, Startzeit in Ticks) aus /proc/&lt;pid&gt;/stat oder null.</summary>
    private static (string Zustand, string Start)? ProzessStat(string pid)
    {
        try
        {
            if (!long.TryParse(pid, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var nummer))
            {
                return null;
            }
            var stat = File.ReadAllText("/proc/" + nummer.ToString(CultureInfo.InvariantCulture) + "/stat");
            var felder = stat[(stat.LastIndexOf(')') + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return (felder[0], felder[19]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>write_info: Angaben zum Halter der Sperre nach laeuft.info (erst ueber eine Hilfsdatei, sonst direkt).</summary>
    public static bool InfoSchreiben(string ordner, string version = "", DateTime? jetzt = null)
    {
        try
        {
            var zeit = (jetzt ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var zeilen = new List<string>
            {
                "pid=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                "start=" + zeit,
                "version=" + (version.Length > 0 ? version : "?"),
                "system=" + Fehlerprotokoll.System(),
            };
            if (ProzessStat(Environment.ProcessId.ToString(CultureInfo.InvariantCulture)) is { } stat)
            {
                zeilen.Add("startticks=" + stat.Start);
            }
            var text = string.Join("\n", zeilen) + "\n";
            var pfad = Path.Combine(ordner, InfoDatei);
            var temp = pfad + ".tmp";
            try
            {
                File.WriteAllText(temp, text, new UTF8Encoding(false));
                File.Move(temp, pfad, true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Entfernen(temp);
                File.WriteAllText(pfad, text, new UTF8Encoding(false));
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>read_info: laeuft.info als Woerterbuch (leer ohne Datei oder bei Fehlern).</summary>
    public static Dictionary<string, string> InfoLesen(string ordner)
    {
        var werte = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var zeile in Leistungsmessung.PythonZeilen(File.ReadAllText(Path.Combine(ordner, InfoDatei), new UTF8Encoding(false, false))))
            {
                var teil = zeile.IndexOf('=');
                if (teil >= 0)
                {
                    werte[zeile[..teil].Trim()] = zeile[(teil + 1)..].Trim();
                }
            }
        }
        catch (Exception)
        {
            return [];
        }
        return werte;
    }

    /// <summary>holder_text: eine Zeile zum Halter der Sperre fuer den Eintrag in fehler.log.</summary>
    public static string HalterText(string ordner)
    {
        try
        {
            var info = InfoLesen(ordner);
            if (!info.TryGetValue("pid", out var pid) || pid.Length == 0)
            {
                return "Halter: unbekannt (laeuft.info fehlt oder ist nicht lesbar).";
            }
            var text = "Halter: pid " + pid + ", gestartet " + info.GetValueOrDefault("start", "?") + ", Version " + info.GetValueOrDefault("version", "?");
            if (Directory.Exists("/proc/self"))
            {
                var stat = ProzessStat(pid);
                if (stat is null)
                {
                    text += "; Linux: Prozess läuft nicht mehr (Angaben veraltet)";
                }
                else if (info.TryGetValue("startticks", out var ticks) && ticks.Length > 0 && stat.Value.Start != ticks)
                {
                    text += "; Linux: Prozessnummer neu vergeben (Angaben veraltet)";
                }
                else
                {
                    string wchan;
                    try
                    {
                        wchan = File.ReadAllText("/proc/" + long.Parse(pid, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + "/wchan").Trim();
                        wchan = wchan.Length > 0 ? wchan : "-";
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or OverflowException)
                    {
                        wchan = "?";
                    }
                    text += "; Linux: Zustand " + stat.Value.Zustand + ", wchan " + wchan;
                }
            }
            return text + ".";
        }
        catch (Exception)
        {
            return "Halter: unbekannt.";
        }
    }

    /// <summary>
    /// claim: beim Start vor allem anderen. true = normal starten, false = ein laufendes Programm wurde
    /// nach vorn geholt, still beenden.
    /// </summary>
    public static bool Beanspruchen(string? ordner = null, double? antwortSekunden = null, double? sperrSekunden = null, string version = "")
    {
        lock (Sperre)
        {
            if (_sperre is not null)
            {
                return true;
            }
        }
        var antwort = antwortSekunden ?? AntwortSekunden;
        var sperre = sperrSekunden ?? SperrSekunden;
        try
        {
            ordner ??= Ablage.Datenordner();
            Directory.CreateDirectory(ordner);
            FileStream? halter;
            try
            {
                halter = Versuchen(ordner);
            }
            catch (Belegt)
            {
                var ergebnis = ZweiterStart(ordner, antwort, sperre);
                if (ergebnis.Beenden)
                {
                    return false;
                }
                halter = ergebnis.Sperre;
            }
            if (halter is not null)
            {
                lock (Sperre)
                {
                    _sperre = halter;
                }
                InfoSchreiben(ordner, version);
                AlteEntfernen(ordner);
                StartAntwortStarten(ordner);
            }
        }
        catch (Exception e)
        {
            // Grundsatz: nie den Start verhindern
            _fehlerlog("Sperre gegen Mehrfachstart nicht verfuegbar, Start ohne Sperre: " + e.GetType().Name);
        }
        return true;
    }

    /// <summary>_second_start: die Sperre ist belegt. Ergebnis: beenden, die Sperre oder null (ohne Sperre starten).</summary>
    private static (bool Beenden, FileStream? Sperre) ZweiterStart(string ordner, double antwortSekunden, double sperrSekunden)
    {
        var token = Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-"
            + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var signal = Path.Combine(ordner, Praefix + token + SignalEndung);
        VordergrundErlauben();
        File.WriteAllBytes(signal, []);
        var antwort = AntwortWarten(ordner, token, antwortSekunden);
        if (antwort is null)
        {
            // E2: Signal liegen lassen und bis sperrSekunden auf Sperre und spaete Antwort achten
            var (halter, spaet) = SperreOderAntwort(ordner, token, sperrSekunden);
            Entfernen(signal);
            if (halter is not null)
            {
                return (false, halter);
            }
            if (spaet == AntwortDa)
            {
                var frei = SperreWarten(ordner, antwortSekunden);
                return frei is not null ? (false, frei) : (true, null);
            }
            if (spaet is null)
            {
                BelegtNotieren(ordner, "keine", sperrSekunden);
                return (false, null);
            }
            antwort = spaet;   // spaete Antwort "endet": nur noch auf die Sperre warten
        }
        Entfernen(signal);
        if (antwort == AntwortDa)
        {
            // Sicherheitsnetz: endet das laufende Programm gerade doch (Update), ist die Sperre gleich frei
            var frei = SperreWarten(ordner, antwortSekunden);
            return frei is not null ? (false, frei) : (true, null);
        }
        var sperre = SperreWarten(ordner, sperrSekunden);
        if (sperre is null)
        {
            BelegtNotieren(ordner, antwort.Length > 0 ? antwort : "keine", sperrSekunden);
        }
        return (false, sperre);
    }

    private static void BelegtNotieren(string ordner, string antwort, double sperrSekunden) =>
        _fehlerlog("Mehrfachstart: Das laufende Programm hat nicht geantwortet (Antwort: " + antwort
            + ") und die Sperre war nach " + ((int)sperrSekunden).ToString(CultureInfo.InvariantCulture)
            + " s noch belegt. Start ohne Sperre. " + HalterText(ordner));

    /// <summary>E2: bis sekunden lang die Sperre versuchen und nach einer spaeten Antwort sehen.</summary>
    private static (FileStream? Sperre, string? Antwort) SperreOderAntwort(string ordner, string token, double sekunden)
    {
        var ende = Stopwatch.GetTimestamp() + (long)(sekunden * Stopwatch.Frequency);
        var pfad = AntwortPfad(ordner, token);
        while (true)
        {
            try
            {
                return (Versuchen(ordner), null);
            }
            catch (Belegt)
            {
            }
            if (AntwortLesen(pfad) is { } text)
            {
                return (null, text);
            }
            if (Stopwatch.GetTimestamp() >= ende)
            {
                return (null, null);
            }
            Thread.Sleep(TimeSpan.FromSeconds(SchrittSekunden));
        }
    }

    /// <summary>_start_answering: bis die Oberflaeche uebernimmt, zweite Starts im Hintergrund mit "da" beantworten.</summary>
    private static void StartAntwortStarten(string ordner)
    {
        var stopp = new CancellationTokenSource();
        var faden = new Thread(() =>
        {
            while (!stopp.Token.WaitHandle.WaitOne(AbfrageMs))
            {
                try
                {
                    SignaleBeantworten(ordner, false);
                }
                catch (Exception)
                {
                }
            }
        })
        { IsBackground = true, Name = "lp-einzelstart" };
        faden.Start();
        _startAntwort = (ordner, stopp);
    }

    /// <summary>
    /// listen (erster Teil): die Oberflaeche uebernimmt das Antworten (alle <see cref="AbfrageMs"/> ms
    /// <see cref="SignaleBeantworten"/>). Liefert den Ordner oder null, wenn dieser Prozess keine Sperre hat.
    /// </summary>
    public static string? Uebernehmen()
    {
        if (_startAntwort is not { } laufend)
        {
            return null;
        }
        laufend.Stopp.Cancel();
        _startAntwort = null;
        _hoerOrdner = laufend.Ordner;
        return laufend.Ordner;
    }

    /// <summary>
    /// Beim Schliessen (closing() in listen): ab jetzt beantwortet ein Hintergrund-Faden jeden zweiten
    /// Start mit "endet", auch waehrend der Oberflaechen-Faden den letzten Abgleich abwartet. Der zweite
    /// Start wartet dann auf die Sperre, die mit dem Prozess frei wird.
    /// </summary>
    public static void Schliessen()
    {
        var ordner = _hoerOrdner ?? _startAntwort?.Ordner;
        _startAntwort?.Stopp.Cancel();
        _startAntwort = null;
        _hoerOrdner = null;
        if (ordner is null || _sperre is null)
        {
            return;
        }
        var stopp = new CancellationTokenSource();
        _ende = stopp;
        var faden = new Thread(() =>
        {
            do
            {
                try
                {
                    SignaleBeantworten(ordner, true);
                }
                catch (Exception)
                {
                }
            }
            while (!stopp.Token.WaitHandle.WaitOne(AbfrageMs));
        })
        { IsBackground = true, Name = "lp-einzelstart-ende" };
        faden.Start();
    }

    /// <summary>_allow_foreground: Windows laesst nur den Vordergrund-Prozess ein Fenster nach vorn holen; der zweite Start reicht das Recht weiter.</summary>
    private static void VordergrundErlauben()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        try
        {
            AllowSetForegroundWindow(-1);   // ASFW_ANY
        }
        catch (Exception)
        {
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int prozess);

    /// <summary>Nur fuer Tests: Sperre freigeben, Antwort-Faden anhalten, fehler.log ersetzen.</summary>
    internal static void FuerTests(Action<string>? fehlerlog = null)
    {
        lock (Sperre)
        {
            _sperre?.Dispose();
            _sperre = null;
        }
        _startAntwort?.Stopp.Cancel();
        _startAntwort = null;
        _ende?.Cancel();
        _ende = null;
        _hoerOrdner = null;
        _fehlerlog = fehlerlog ?? (text => Fehlerprotokoll.Schreiben(text));
    }
}
