using Lernplattform.Core.Diagnose;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Lernstand-Datenbank "lernplattform.db" im Datenordner. Gleiche 13 Tabellen,
/// Spalten und Indizes wie in der Python-Linie (fisi_core.DBManager, Nico
/// 20.10.2026 Frage 1 a; Vergleichswerte G11). Einziger Zusatz: die Version
/// des Aufbaus steht in PRAGMA user_version (Auflage K2, keine eigene Tabelle;
/// Python setzt sie nicht und hat dort 0).
/// <para>
/// Verhalten wie Python ab 0.62.5: Beim Oeffnen wird geprueft, ob Ordner und
/// Datei beschreibbar sind und ob die Datei heil ist (PRAGMA quick_check).
/// Sonst wird nichts angelegt und nichts gespeichert (<see cref="NurLesenGrund"/>),
/// die Datei bleibt unveraendert. Jeder Fehler landet in fehler.log neben der
/// Datenbank; die Oberflaeche bekommt je Fehlerart nur die erste Meldung.
/// </para>
/// </summary>
public sealed class Lernstand
{
    public const string DateiName = "lernplattform.db";

    /// <summary>Version des Aufbaus (PRAGMA user_version). 1 = Aufbau wie Python v0.63.2.</summary>
    public const int AufbauVersion = 1;

    /// <summary>Verstaendliche Texte je Fehlerart, PC und Handy gleich (Python DB_ERROR_TEXTS, Dateiname angepasst).</summary>
    public static readonly IReadOnlyDictionary<string, string> Fehlertexte = new Dictionary<string, string>
    {
        ["kaputt"] = "Die Datenbank mit deinem Lernfortschritt ist beschädigt. Damit sie nicht "
            + "weiter Schaden nimmt, speichert die App ab jetzt nichts mehr; angezeigte "
            + "Zahlen können unvollständig sein. Die Datei selbst bleibt unverändert. "
            + "Den Datenordner findest du unter Optionen > Problem melden.",
        ["schreibschutz"] = "Dein Lernfortschritt wird nicht gespeichert: Der Datenordner ist "
            + "schreibgeschützt. Die App läuft weiter, Antworten werden aber nicht "
            + "gespeichert. Gib dem Datenordner und der Datei " + DateiName + " "
            + "Schreibrechte und starte die App neu. Den Datenordner findest du "
            + "unter Optionen > Problem melden.",
        ["nicht_erreichbar"] = "Dein Lernfortschritt wird nicht gespeichert: Der Datenordner ist "
            + "nicht erreichbar. Die Datenbank lässt sich nicht öffnen, zum "
            + "Beispiel weil ein Laufwerk fehlt. Prüfe den Datenordner und "
            + "starte die App neu.",
        ["voll"] = "Der Speicherplatz ist voll. Dein Lernfortschritt wird erst wieder "
            + "gespeichert, wenn Platz frei ist.",
        ["gesperrt"] = "Die Datenbank ist gerade belegt, zum Beispiel durch ein anderes Programm. "
            + "Der letzte Schritt wurde vielleicht nicht gespeichert.",
        ["sonstige"] = "Beim Zugriff auf die Datenbank ist ein Fehler aufgetreten. Einzelheiten "
            + "stehen im Fehlerprotokoll (Optionen > Problem melden).",
    };

    /// <summary>Bleibende rote Zeile, solange nichts gespeichert wird.</summary>
    public static readonly IReadOnlyDictionary<string, string> NurLesenZeilen = new Dictionary<string, string>
    {
        ["kaputt"] = "Dein Lernfortschritt wird nicht gespeichert: Die Datenbank ist beschädigt.",
        ["schreibschutz"] = "Dein Lernfortschritt wird nicht gespeichert: Der Datenordner ist schreibgeschützt.",
        ["nicht_erreichbar"] = "Dein Lernfortschritt wird nicht gespeichert: Der Datenordner ist nicht erreichbar.",
    };

    // Fehlerart aus dem englischen Text von SQLite (erster Treffer gilt)
    private static readonly (string Art, string[] Stichworte)[] Fehlerarten =
    [
        ("schreibschutz", ["readonly", "read-only"]),
        ("kaputt", ["malformed", "not a database", "corrupt", "disk image"]),
        ("nicht_erreichbar", ["unable to open"]),
        ("voll", ["is full"]),
        ("gesperrt", ["locked", "busy"]),
    ];

    /// <summary>Tabellen mit Lern-Eintraegen, die abgeglichen werden (Python SYNC_TABLES, gleiche Reihenfolge).</summary>
    public static readonly IReadOnlyList<string> AbgleichTabellen =
    [
        "test_results", "card_events", "quiz_answers", "scenario_events", "project_events", "ap1_events",
        "trainer_aufgaben", "pruefungen", "spiel_ereignisse", "spiel_plaetze", "spiel_bestenliste", "abschlussprojekt",
    ];

    /// <summary>Spalten, die erst spaeter dazukamen (Python OPTIONAL_COLUMNS inkl. utc_offset), gleiche Reihenfolge.</summary>
    private static readonly (string Tabelle, string Spalte, string Typ)[] SpaetereSpalten =
    [
        ("scenario_events", "correct", "INTEGER"), ("scenario_events", "utc_offset", "INTEGER"),
        ("project_events", "correct", "INTEGER"), ("project_events", "utc_offset", "INTEGER"),
        ("ap1_events", "correct", "INTEGER"), ("ap1_events", "utc_offset", "INTEGER"),
        ("spiel_ereignisse", "lauf", "TEXT"), ("spiel_ereignisse", "utc_offset", "INTEGER"),
        ("test_results", "utc_offset", "INTEGER"), ("card_events", "utc_offset", "INTEGER"),
        ("quiz_answers", "utc_offset", "INTEGER"), ("trainer_aufgaben", "utc_offset", "INTEGER"),
        ("pruefungen", "utc_offset", "INTEGER"), ("spiel_bestenliste", "utc_offset", "INTEGER"),
    ];

    // Gleiche CREATE-Anweisungen wie Python init_db (Reihenfolge und Spalten)
    private static readonly string[] Tabellen =
    [
        """
        CREATE TABLE IF NOT EXISTS test_results (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            score INTEGER NOT NULL,
            total INTEGER NOT NULL,
            percentage REAL NOT NULL,
            note TEXT NOT NULL,
            duration_seconds INTEGER NOT NULL
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS card_events (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            category TEXT NOT NULL,
            question TEXT NOT NULL,
            mode TEXT NOT NULL,
            correct INTEGER
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS quiz_answers (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            category TEXT NOT NULL,
            question TEXT NOT NULL,
            correct INTEGER NOT NULL
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS scenario_events (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            scenario_index INTEGER NOT NULL,
            title TEXT NOT NULL,
            theme TEXT NOT NULL,
            correct INTEGER
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS project_events (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            project_index INTEGER NOT NULL,
            title TEXT NOT NULL,
            category TEXT NOT NULL,
            correct INTEGER
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS ap1_events (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            scenario_index INTEGER NOT NULL,
            title TEXT NOT NULL,
            theme TEXT NOT NULL,
            correct INTEGER
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS trainer_aufgaben (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            art TEXT NOT NULL,
            stufe INTEGER NOT NULL,
            correct INTEGER NOT NULL
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS pruefungen (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            art TEXT NOT NULL,
            punkte REAL NOT NULL,
            note TEXT NOT NULL,
            dauer INTEGER NOT NULL,
            daten TEXT NOT NULL DEFAULT '{}'
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS abschlussprojekt (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            projekt TEXT NOT NULL,
            feld TEXT NOT NULL,
            wert TEXT NOT NULL DEFAULT ''
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS spiel_ereignisse (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            typ TEXT NOT NULL,
            daten TEXT NOT NULL,
            geraet TEXT NOT NULL DEFAULT '',
            uid TEXT,
            lauf TEXT
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS spiel_plaetze (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            platz INTEGER NOT NULL,
            lauf TEXT NOT NULL,
            aktion TEXT NOT NULL,
            daten TEXT NOT NULL DEFAULT '{}',
            geraet TEXT NOT NULL DEFAULT '',
            uid TEXT
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS spiel_bestenliste (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            durchgang TEXT NOT NULL,
            art TEXT NOT NULL,
            schluessel TEXT NOT NULL,
            wert REAL,
            daten TEXT NOT NULL DEFAULT '{}',
            uid TEXT
        )
        """,
    ];

    private readonly Action<string>? _fehlermeldung;
    private readonly Dictionary<string, int> _fehlerzaehler = new(StringComparer.Ordinal);

    /// <param name="pfad">Datei der Datenbank; Vorgabe: Datenordner/lernplattform.db.</param>
    /// <param name="fehlermeldung">Bekommt je Fehlerart die erste verstaendliche Meldung (Oberflaeche).</param>
    public Lernstand(string? pfad = null, Action<string>? fehlermeldung = null)
    {
        if (pfad is null)
        {
            // wie Python resolve_db_path: den eigenen Datenordner anlegen
            try
            {
                Directory.CreateDirectory(Ablage.Datenordner());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
        Pfad = Path.GetFullPath(pfad ?? Path.Combine(Ablage.Datenordner(), DateiName));
        _fehlermeldung = fehlermeldung;
        Einrichten();
    }

    public string Pfad { get; }

    /// <summary>Grund, warum nichts gespeichert wird (Schluessel von <see cref="NurLesenZeilen"/>), oder null.</summary>
    public string? NurLesenGrund { get; private set; }

    /// <summary>Gezaehlte Fehler je Art.</summary>
    public IReadOnlyDictionary<string, int> Fehlerzaehler => _fehlerzaehler;

    /// <summary>Abgelehnte Schreibzugriffe, waehrend nichts gespeichert wird.</summary>
    public int AbgelehnteSchreibzugriffe { get; private set; }

    /// <summary>Fehlerart (Schluessel von <see cref="Fehlertexte"/>) zum Text eines SQLite-Fehlers.</summary>
    public static string Fehlerart(string text)
    {
        text = text.ToLowerInvariant();
        foreach (var (art, stichworte) in Fehlerarten)
        {
            if (stichworte.Any(s => text.Contains(s, StringComparison.Ordinal)))
            {
                return art;
            }
        }
        return "sonstige";
    }

    private void Melden(string nachricht, string? art = null)
    {
        art ??= Fehlerart(nachricht);
        Fehlerprotokoll.Schreiben($"Datenbankfehler ({art}): {nachricht}",
            Path.Combine(Path.GetDirectoryName(Pfad)!, Fehlerprotokoll.DateiName));
        var erste = !_fehlerzaehler.ContainsKey(art);
        _fehlerzaehler[art] = _fehlerzaehler.GetValueOrDefault(art) + 1;
        var schonNurLesen = NurLesenGrund is not null;
        if (!schonNurLesen && NurLesenZeilen.ContainsKey(art))
        {
            NurLesenGrund = art;
        }
        if (erste && !schonNurLesen)
        {
            _fehlermeldung?.Invoke(Fehlertexte[art]);
        }
    }

    /// <summary>Neue Verbindung; wird nichts gespeichert, scheitern auch direkte Schreibzugriffe.</summary>
    public SqliteConnection Verbindung()
    {
        var verbindung = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Pfad,
            Pooling = false,
        }.ToString());
        verbindung.Open();
        if (NurLesenGrund is not null)
        {
            using var befehl = verbindung.CreateCommand();
            befehl.CommandText = "PRAGMA query_only = ON";
            befehl.ExecuteNonQuery();
        }
        return verbindung;
    }

    /// <summary>
    /// Schreibvorgang mit zentraler Fehlerbehandlung. Rueckgabe true bei Erfolg.
    /// Wird nichts gespeichert, wird er still abgelehnt (die Meldung kam schon).
    /// </summary>
    public bool Schreiben(string sql, params (string Name, object? Wert)[] parameter)
    {
        if (NurLesenGrund is not null)
        {
            AbgelehnteSchreibzugriffe++;
            return false;
        }
        try
        {
            using var verbindung = Verbindung();
            using var transaktion = verbindung.BeginTransaction();
            using var befehl = Befehl(verbindung, sql, parameter);
            befehl.Transaction = transaktion;
            befehl.ExecuteNonQuery();
            transaktion.Commit();
            return true;
        }
        catch (SqliteException fehler)
        {
            Melden(fehler.Message);
            return false;
        }
    }

    /// <summary>Abfrage mit zentraler Fehlerbehandlung; im Fehlerfall eine leere Liste.</summary>
    public List<object?[]> Abfragen(string sql, params (string Name, object? Wert)[] parameter)
    {
        var zeilen = new List<object?[]>();
        try
        {
            using var verbindung = Verbindung();
            using var befehl = Befehl(verbindung, sql, parameter);
            using var leser = befehl.ExecuteReader();
            while (leser.Read())
            {
                var zeile = new object?[leser.FieldCount];
                for (var i = 0; i < leser.FieldCount; i++)
                {
                    zeile[i] = leser.IsDBNull(i) ? null : leser.GetValue(i);
                }
                zeilen.Add(zeile);
            }
        }
        catch (SqliteException fehler)
        {
            Melden(fehler.Message);
            zeilen.Clear();
        }
        return zeilen;
    }

    private static SqliteCommand Befehl(SqliteConnection verbindung, string sql, (string Name, object? Wert)[] parameter)
    {
        var befehl = verbindung.CreateCommand();
        befehl.CommandText = sql;
        foreach (var (name, wert) in parameter)
        {
            befehl.Parameters.AddWithValue(name, wert ?? DBNull.Value);
        }
        return befehl;
    }

    private void Einrichten()
    {
        if (!SpeicherPruefen())
        {
            return;
        }
        try
        {
            using var verbindung = Verbindung();
            using var transaktion = verbindung.BeginTransaction();
            void Ausfuehren(string sql)
            {
                using var befehl = verbindung.CreateCommand();
                befehl.Transaction = transaktion;
                befehl.CommandText = sql;
                befehl.ExecuteNonQuery();
            }
            List<string> Spalten(string tabelle)
            {
                using var befehl = verbindung.CreateCommand();
                befehl.Transaction = transaktion;
                befehl.CommandText = $"PRAGMA table_info({tabelle})";
                using var leser = befehl.ExecuteReader();
                var namen = new List<string>();
                while (leser.Read())
                {
                    namen.Add(leser.GetString(1));
                }
                return namen;
            }

            foreach (var sql in Tabellen)
            {
                Ausfuehren(sql);
            }
            // Nachruesten wie Python _migrate (Aufbau). Das Nachtragen fehlender uid-Werte
            // alter Zeilen und die Fragetext-Umbenennungen gehoeren zur Uebernahme des alten
            // Lernstands (Phase 2); eine neue Datenbank hat keine solchen Zeilen.
            foreach (var tabelle in AbgleichTabellen)
            {
                if (!Spalten(tabelle).Contains("uid"))
                {
                    Ausfuehren($"ALTER TABLE {tabelle} ADD COLUMN uid TEXT");
                }
                Ausfuehren($"CREATE UNIQUE INDEX IF NOT EXISTS ix_{tabelle}_uid ON {tabelle} (uid)");
            }
            foreach (var (tabelle, spalte, typ) in SpaetereSpalten)
            {
                if (!Spalten(tabelle).Contains(spalte))
                {
                    Ausfuehren($"ALTER TABLE {tabelle} ADD COLUMN {spalte} {typ}");
                }
            }
            Ausfuehren("CREATE TABLE IF NOT EXISTS sync_meta ( key TEXT PRIMARY KEY, value TEXT)");
            Ausfuehren("CREATE INDEX IF NOT EXISTS ix_spiel_ereignisse_lauf ON spiel_ereignisse (lauf)");
            Ausfuehren($"PRAGMA user_version = {AufbauVersion}");
            transaktion.Commit();
        }
        catch (SqliteException fehler)
        {
            Melden($"Datenbank konnte nicht initialisiert werden: {fehler.Message}");
        }
    }

    /// <summary>
    /// Beim Start einmal pruefen, ob Ordner und Datei beschreibbar sind und ob
    /// die Datei heil ist (PRAGMA quick_check, nur lesend). false = nichts schreiben.
    /// </summary>
    private bool SpeicherPruefen()
    {
        var ordner = Path.GetDirectoryName(Pfad)!;
        if (Directory.Exists(ordner) && !(OrdnerBeschreibbar(ordner) && DateiBeschreibbar(Pfad)))
        {
            Melden($"Schreibprobe beim Start gescheitert: Datenordner oder {Path.GetFileName(Pfad)} ist schreibgeschuetzt",
                "schreibschutz");
            return false;
        }
        if (!File.Exists(Pfad) || new FileInfo(Pfad).Length == 0)
        {
            return true;
        }
        List<string> ergebnis;
        try
        {
            using var verbindung = new SqliteConnection($"Data Source={Pfad};Pooling=False");
            verbindung.Open();
            using var befehl = verbindung.CreateCommand();
            befehl.CommandText = "PRAGMA quick_check(1)";
            using var leser = befehl.ExecuteReader();
            ergebnis = [];
            while (leser.Read())
            {
                ergebnis.Add(leser.GetString(0));
            }
        }
        catch (SqliteException fehler)
        {
            if (Fehlerart(fehler.Message) != "kaputt")
            {
                return true; // z. B. gesperrt: der normale Weg meldet es
            }
            Melden($"Pruefung beim Start (quick_check): {fehler.Message}", "kaputt");
            return false;
        }
        if (ergebnis is not ["ok"])
        {
            Melden($"Pruefung beim Start (quick_check) meldet Schaden: {(ergebnis.Count > 0 ? ergebnis[0] : "keine Antwort")}",
                "kaputt");
            return false;
        }
        return true;
    }

    /// <summary>Echte Probe: eine Wegwerf-Datei anlegen, sie verschwindet beim Schliessen.</summary>
    private static bool OrdnerBeschreibbar(string ordner)
    {
        try
        {
            var probe = Path.Combine(ordner, ".lernplattform_schreibprobe_" + Guid.NewGuid().ToString("N"));
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Datei zum Schreiben oeffnen, ohne etwas zu aendern (fehlt sie: ja).</summary>
    private static bool DateiBeschreibbar(string pfad)
    {
        if (!File.Exists(pfad))
        {
            return true;
        }
        try
        {
            using (new FileStream(pfad, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
