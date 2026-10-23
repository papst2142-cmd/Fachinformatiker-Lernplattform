using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Diagnose;

/// <summary>
/// Phase 5 BE7: Leistungsmessung wie fisi_leistung.Recorder (Python v0.63.2, Vergleichswerte G39).
/// Zeichnet auf Wunsch auf, wie lange Seitenwechsel, Darstellungswechsel und Aenderungen der
/// Fenstergroesse dauern und wie viel Arbeitsspeicher das Programm braucht. Die Zeilen sammeln
/// sich im Speicher und werden gebuendelt in leistungsmessung.csv geschrieben (hoechstens 1 MB,
/// danach bleiben die neuesten 80 %). Der Schalter "leistungsmessung" liegt nur in
/// einstellungen.json, nie im Abgleich. Ist die Messung aus, kehrt <see cref="Aufzeichnen"/> sofort
/// zurueck und es entsteht keine Datei. Die Messung stoert das Programm nie (Fehler werden
/// geschluckt, die Zeilen sind dann verloren).
/// Abweichung (Wortliste W2): Spalte "customtkinter" traegt die Avalonia-Version; "privat_mb" nur
/// unter Windows wie in Python.
/// </summary>
public sealed class Leistungsmessung
{
    public const string Schluessel = "leistungsmessung";   // SETTING_KEY
    public const string DateiName = "leistungsmessung.csv";
    public const string Dateityp = "text/csv";
    public const int MaxBytes = 1024 * 1024;
    public const int KuerzenAuf = (int)(MaxBytes * 0.8);   // TRIM_TO
    public const int PufferZeilen = 25;                    // FLUSH_LINES
    public const int PufferSekunden = 5;                   // FLUSH_SECONDS
    public const string Trenner = ";";
    public const int MessartAlt = 1;
    public const int MessartGezeichnet = 2;

    public static readonly IReadOnlyList<string> Spalten =
    [
        "zeit", "version", "geraet", "system", "ereignis", "von", "nach", "dauer_ms",
        "max_ms", "anzahl", "ereignisse", "speicher_mb", "privat_mb", "elemente",
        "aufgaben", "customtkinter", "verfuegbar_mb", "vorgang", "messart",
    ];

    // Ereignisse
    public const string EreignisStart = "start";
    public const string EreignisSeite = "seite";
    public const string EreignisDarstellung = "darstellung";
    public const string EreignisGroesse = "groesse";
    public const string EreignisEnde = "ende";
    public const string EreignisBlockiert = "blockiert";
    public const string EreignisVorladen = "vorladen";
    public const string EreignisVorladenFertig = "vorladen_fertig";

    private readonly object _sperre = new();
    private readonly Func<DateTime> _uhr;
    private readonly Func<(double? Speicher, double? Privat)> _speicher;
    private readonly Func<double?> _verfuegbar;
    private readonly Func<bool, bool>? _schalterSpeichern;
    private List<string> _puffer = [];
    private bool _kopfGeprueft;

    /// <param name="aktiv">Schalter beim Start (aus einstellungen.json, <see cref="Eingeschaltet"/>).</param>
    /// <param name="schalterSpeichern">set_enabled: speichert den Schalter in einstellungen.json.</param>
    /// <param name="uhr">Ortszeit (nur Tests); system, speicher, verfuegbar nur fuer Tests.</param>
    public Leistungsmessung(string version, string geraet, string pfad, bool aktiv, string toolkit = "",
        Func<bool, bool>? schalterSpeichern = null, Func<DateTime>? uhr = null, string? system = null,
        Func<(double?, double?)>? speicher = null, Func<double?>? verfuegbar = null)
    {
        Version = version;
        Geraet = geraet;
        Toolkit = toolkit;
        Pfad = pfad;
        Aktiv = aktiv;
        _schalterSpeichern = schalterSpeichern;
        _uhr = uhr ?? (() => DateTime.Now);
        Systemname = system ?? SystemText();
        _speicher = speicher ?? SpeicherMb;
        _verfuegbar = verfuegbar ?? VerfuegbarMb;
    }

    public string Version { get; }
    public string Geraet { get; }
    public string Toolkit { get; }
    public string Pfad { get; }
    public string Systemname { get; }
    public bool Aktiv { get; private set; }

    /// <summary>Liefert den laufenden Vorgang (PC, Haenger-Diagnose), sonst leer.</summary>
    public Func<string>? Vorgangsquelle { get; set; }

    /// <summary>Fester Eintrag fuer "vorgang" in den Zeilen "start" (sonst leer).</summary>
    public string StartVorgang { get; set; } = "";

    /// <summary>enabled(): nur ein gespeichertes true gilt, alles andere ist aus.</summary>
    public static bool Eingeschaltet(JsonObject werte) =>
        werte[Schluessel] is JsonValue v && v.GetValueKind() == JsonValueKind.True;

    /// <summary>file_path(): im Datenordner neben der Datenbank.</summary>
    public static string StandardPfad() => Path.Combine(Ablage.Datenordner(), DateiName);

    /// <summary>set_active: aus schreibt "ende" und alles Gesammelte, an beginnt mit "start".</summary>
    public void AktivSetzen(bool an)
    {
        if (an == Aktiv)
        {
            return;
        }
        if (!an)
        {
            Aufzeichnen(EreignisEnde);
            Schreiben();
        }
        Aktiv = an;
        try
        {
            _schalterSpeichern?.Invoke(an);
        }
        catch (Exception)
        {
            // die Messung darf das Programm nie stoeren
        }
        if (an)
        {
            Aufzeichnen(EreignisStart);
        }
    }

    /// <summary>record: eine Zeile merken (nur bei eingeschalteter Messung).</summary>
    public void Aufzeichnen(string ereignis, string von = "", string nach = "", double? dauerMs = null, double? maxMs = null,
        int? anzahl = null, int? ereignisse = null, int? elemente = null, int? aufgaben = null, bool speicher = true,
        string? vorgang = null, int? messart = null)
    {
        if (!Aktiv)
        {
            return;
        }
        var (mb, privat) = speicher ? Sicher(_speicher, (null, null)) : (null, null);
        var jetzt = _uhr().ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture);
        if (vorgang is null && ereignis == EreignisStart && StartVorgang.Length > 0)
        {
            vorgang = StartVorgang;
        }
        if (vorgang is null)
        {
            vorgang = Sicher(() => Vorgangsquelle?.Invoke() ?? "", "");
        }
        var start = ereignis == EreignisStart;
        string[] werte =
        [
            jetzt, Version, Geraet, Systemname, ereignis, von, nach,
            Zahl(dauerMs), Zahl(maxMs), Zahl(anzahl), Zahl(ereignisse), Zahl(mb), Zahl(privat),
            Zahl(elemente), Zahl(aufgaben), start ? Toolkit : "",
            start ? Zahl(Sicher(_verfuegbar, null), 0) : "", vorgang, Zahl(messart),
        ];
        var zeile = string.Join(Trenner, werte.Select(Sauber));
        bool voll;
        lock (_sperre)
        {
            _puffer.Add(zeile);
            voll = _puffer.Count >= PufferZeilen;
        }
        if (voll)
        {
            Schreiben();
        }
    }

    /// <summary>flush: gesammelte Zeilen anhaengen und die Datei auf <see cref="MaxBytes"/> begrenzen.</summary>
    public bool Schreiben()
    {
        lock (_sperre)
        {
            if (_puffer.Count == 0)
            {
                return true;
            }
            var zeilen = _puffer;
            _puffer = [];
            try
            {
                var neu = !File.Exists(Pfad);
                if (!neu && !_kopfGeprueft)
                {
                    KopfErneuern();
                }
                _kopfGeprueft = true;
                var text = new StringBuilder();
                if (neu)
                {
                    text.Append(string.Join(Trenner, Spalten)).Append('\n');
                }
                text.Append(string.Join("\n", zeilen)).Append('\n');
                File.AppendAllText(Pfad, text.ToString(), new UTF8Encoding(false));
                if (new FileInfo(Pfad).Length > MaxBytes)
                {
                    Kuerzen();
                }
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;   // z. B. Datenordner schreibgeschuetzt: Messung verloren, Programm laeuft
            }
        }
    }

    /// <summary>_update_header: eine Messdatei einer aelteren Version bekommt die neue Kopfzeile.</summary>
    private void KopfErneuern()
    {
        var kopf = string.Join(Trenner, Spalten);
        var inhalt = File.ReadAllText(Pfad, new UTF8Encoding(false, false));
        var ende = inhalt.IndexOf('\n');
        var erste = ende >= 0 ? inhalt[..ende] : inhalt;
        if (erste == kopf)
        {
            return;
        }
        var rest = ende >= 0 ? inhalt[(ende + 1)..] : "";
        Ersetzen(kopf + "\n" + rest);
    }

    /// <summary>_trim: von hinten so viele ganze Zeilen behalten, dass die Datei hoechstens <see cref="KuerzenAuf"/> Bytes hat.</summary>
    private void Kuerzen()
    {
        var zeilen = PythonZeilen(File.ReadAllText(Pfad, new UTF8Encoding(false, false)));
        var kopf = zeilen[0];
        long groesse = Encoding.UTF8.GetByteCount(kopf) + 1;
        var behalten = new List<string>();
        for (var i = zeilen.Count - 1; i >= 1; i--)
        {
            groesse += Encoding.UTF8.GetByteCount(zeilen[i]) + 1;
            if (groesse > KuerzenAuf)
            {
                break;
            }
            behalten.Add(zeilen[i]);
        }
        behalten.Reverse();
        Ersetzen(string.Join("\n", behalten.Prepend(kopf)) + "\n");
    }

    private void Ersetzen(string inhalt)
    {
        var temp = Pfad + ".tmp";
        File.WriteAllText(temp, inhalt, new UTF8Encoding(false));
        File.Move(temp, Pfad, true);
    }

    /// <summary>delete: Messdatei und noch nicht geschriebene Zeilen verwerfen.</summary>
    public bool Loeschen()
    {
        lock (_sperre)
        {
            _puffer = [];
            try
            {
                File.Delete(Pfad);   // fehlende Datei: kein Fehler
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    /// <summary>info: (Anzahl Eintraege, Groesse in Byte) oder null ohne Datei.</summary>
    public (int Zeilen, long Groesse)? Info()
    {
        Schreiben();
        try
        {
            var inhalt = File.ReadAllText(Pfad, new UTF8Encoding(false, false));
            var zeilen = inhalt.Count(z => z == '\n') + (inhalt.Length > 0 && inhalt[^1] != '\n' ? 1 : 0);
            return (Math.Max(0, zeilen - 1), new FileInfo(Pfad).Length);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>state_text.</summary>
    public string ZustandText()
    {
        if (Info() is not { } info)
        {
            return LeistungTexte.KeineDatei;
        }
        return LeistungTexte.Datei
            .Replace("%(zeilen)d", info.Zeilen.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("%(kb)s", Zahl(info.Groesse / 1024.0), StringComparison.Ordinal);
    }

    /// <summary>read_bytes: Inhalt der Messdatei (Handy: zum Speichern). Wirft IOException ohne Datei.</summary>
    public byte[] Lesen()
    {
        Schreiben();
        return File.ReadAllBytes(Pfad);
    }

    // -- Zahlen und Felder ------------------------------------------------------------------

    /// <summary>_number: leer ohne Wert, ganze Zahl wie str(), Kommazahl mit Komma.</summary>
    public static string Zahl(double? wert, int stellen = 1) => wert is { } w ? Zahltext.Komma(w, stellen) : "";

    public static string Zahl(int? wert) => wert?.ToString(CultureInfo.InvariantCulture) ?? "";

    /// <summary>_clean: keine Trennzeichen oder Zeilenumbrueche in einem Feld.</summary>
    public static string Sauber(string? wert) =>
        (wert ?? "").Replace(Trenner, ",", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ');

    private static T Sicher<T>(Func<T> wert, T vorgabe)
    {
        try
        {
            return wert();
        }
        catch (Exception)
        {
            return vorgabe;
        }
    }

    // -- Speicher und System ----------------------------------------------------------------

    /// <summary>
    /// memory_mb: (Arbeitsspeicher, privater Speicher) des Programms in MB. Windows: Arbeitssatz und
    /// zugesicherter privater Speicher; Linux und Android: belegter Speicher (RSS); macOS: hoechster
    /// bisher belegter Speicher (wie Pythons ru_maxrss). Nicht ermittelbar: (null, null).
    /// </summary>
    public static (double?, double?) SpeicherMb()
    {
        try
        {
            using var prozess = Process.GetCurrentProcess();
            if (OperatingSystem.IsWindows())
            {
                return (prozess.WorkingSet64 / 1048576.0, prozess.PrivateMemorySize64 / 1048576.0);
            }
            if (OperatingSystem.IsMacOS())
            {
                return (prozess.PeakWorkingSet64 / 1048576.0, null);
            }
            return (prozess.WorkingSet64 / 1048576.0, null);
        }
        catch (Exception)
        {
            return (null, null);
        }
    }

    /// <summary>available_mb: verfuegbarer Arbeitsspeicher des Geraets in MB (Linux/Android MemAvailable, Windows ullAvailPhys).</summary>
    public static double? VerfuegbarMb()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var status = new Speicherstatus { Laenge = (uint)Marshal.SizeOf<Speicherstatus>() };
                return GlobalMemoryStatusEx(ref status) ? status.Verfuegbar / 1048576.0 : null;
            }
            foreach (var zeile in File.ReadLines("/proc/meminfo"))
            {
                if (zeile.StartsWith("MemAvailable:", StringComparison.Ordinal))
                {
                    return long.Parse(zeile.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1], CultureInfo.InvariantCulture) / 1024.0;
                }
            }
        }
        catch (Exception)
        {
            return null;
        }
        return null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Speicherstatus
    {
        public uint Laenge;
        public uint Last;
        public ulong Gesamt;
        public ulong Verfuegbar;
        public ulong AuslagerungGesamt;
        public ulong AuslagerungFrei;
        public ulong VirtuellGesamt;
        public ulong VirtuellFrei;
        public ulong VirtuellErweitert;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref Speicherstatus status);

    /// <summary>system_text: Betriebssystem kurz, z. B. "Windows 11" oder "Linux 6.8.0-45-generic".</summary>
    public static string SystemText()
    {
        try
        {
            var (name, release, _) = Problembericht.Systemangaben();
            if (OperatingSystem.IsAndroid())
            {
                name = "Android";
            }
            return (name + " " + release).Trim();
        }
        catch (Exception)
        {
            return Fehlerprotokoll.System();
        }
    }

    // -- Auswertung (read_rows, messart_of, page_times) ------------------------------------

    /// <summary>read_rows: Zeilen einer Messdatei als Woerterbuecher (Spalten aus der eigenen Kopfzeile).</summary>
    public static List<Dictionary<string, string>> ZeilenLesen(string pfad)
    {
        var zeilen = PythonZeilen(File.ReadAllText(pfad, new UTF8Encoding(false, false)));
        if (zeilen.Count == 0)
        {
            return [];
        }
        var kopf = zeilen[0].Split(Trenner);
        var ergebnis = new List<Dictionary<string, string>>();
        foreach (var zeile in zeilen.Skip(1).Where(z => z.Length > 0))
        {
            var felder = zeile.Split(Trenner);
            var eintrag = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < Math.Min(kopf.Length, felder.Length); i++)
            {
                eintrag[kopf[i]] = felder[i];   // dict(zip(...)): bei doppeltem Namen gilt der letzte
            }
            ergebnis.Add(eintrag);
        }
        return ergebnis;
    }

    /// <summary>messart_of: Messart einer Zeile; leer oder fehlend = <see cref="MessartAlt"/>.</summary>
    public static int Messart(IReadOnlyDictionary<string, string> zeile)
    {
        var wert = (zeile.GetValueOrDefault("messart") ?? "").Trim();
        return wert.Length > 0 && wert.All(char.IsAsciiDigit) && int.TryParse(wert, NumberStyles.None, CultureInfo.InvariantCulture, out var art)
            ? art : MessartAlt;
    }

    /// <summary>page_times: Dauer der Seitenwechsel (ms) einer Messart je Zielseite, in der Reihenfolge des ersten Auftretens.</summary>
    public static OrderedDictionary<string, List<double>> Seitenzeiten(IEnumerable<IReadOnlyDictionary<string, string>> zeilen, int messart)
    {
        var ergebnis = new OrderedDictionary<string, List<double>>(StringComparer.Ordinal);
        foreach (var zeile in zeilen)
        {
            if (zeile.GetValueOrDefault("ereignis") != EreignisSeite || Messart(zeile) != messart)
            {
                continue;
            }
            var text = (zeile.GetValueOrDefault("dauer_ms") ?? "").Replace(',', '.').Trim();
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var dauer))
            {
                continue;
            }
            var seite = zeile.GetValueOrDefault("nach") ?? "";
            if (!ergebnis.TryGetValue(seite, out var liste))
            {
                ergebnis[seite] = liste = [];
            }
            liste.Add(dauer);
        }
        return ergebnis;
    }

    /// <summary>str.splitlines(): trennt an \n, \r, \r\n und den weiteren Zeilenenden von Python.</summary>
    internal static List<string> PythonZeilen(string text)
    {
        var zeilen = new List<string>();
        var anfang = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var z = text[i];
            if (z is '\n' or '\r' or '\v' or '\f' or '\x1c' or '\x1d' or '\x1e' or '\x85' or '\u2028' or '\u2029')
            {
                zeilen.Add(text[anfang..i]);
                if (z == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
                anfang = i + 1;
            }
        }
        if (anfang < text.Length)
        {
            zeilen.Add(text[anfang..]);
        }
        return zeilen;
    }
}
