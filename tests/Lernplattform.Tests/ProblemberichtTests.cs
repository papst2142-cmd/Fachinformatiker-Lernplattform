using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 5 BE5 "Problem melden": Bericht wie fisi_diagnose.build_report (G38,
/// tests/Vergleich/problembericht.json). Kontrollfehler: Token im Bericht, Rechnername im Geraet,
/// Schnitt mitten in der Zeile, Groessen mit Ortskultur.
/// </summary>
[Collection("Haenger")]
public class ProblemberichtTests
{
    private static readonly JsonElement G38 = VergleichsHilfe.Lade("problembericht.json");

    private const string Laufzeit = ".NET Testlauf";

    [Fact]
    public void GroessenWiePython()
    {
        var alt = CultureInfo.CurrentCulture;
        try
        {
            // Kontrollfehler "ToString mit Ortskultur": mit deutscher Kultur darf sich nichts aendern
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            foreach (var fall in G38.GetProperty("groessen").EnumerateArray())
            {
                Assert.Equal(fall[1].GetString(), Problembericht.GroesseText(fall[0].GetInt64()));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = alt;
        }
        Assert.Equal("1,2 KB", Problembericht.GroesseText(1280));   // genau halb: gerade Ziffer wie Python
    }

    [Fact]
    public void DatenbankgroesseMitWalUndJournal()
    {
        foreach (var fall in G38.GetProperty("dbgroesse").EnumerateArray())
        {
            using var ordner = new TempOrdner();
            var pfad = Path.Combine(ordner.Pfad, "x.db");
            foreach (var (endung, name) in new[] { ("", "db"), ("-wal", "wal"), ("-journal", "journal") })
            {
                if (fall.GetProperty(name).ValueKind == JsonValueKind.Number)
                {
                    File.WriteAllBytes(pfad + endung, new byte[fall.GetProperty(name).GetInt32()]);
                }
            }
            Assert.Equal(fall.GetProperty("summe").GetInt64(), Problembericht.DatenbankGroesse(pfad));
            Assert.Equal(fall.GetProperty("text").GetString(), Problembericht.GroesseText(Problembericht.DatenbankGroesse(pfad)));
        }
    }

    [Fact]
    public void GeraetUndDateinameWiePython()
    {
        foreach (var fall in G38.GetProperty("geraet").EnumerateArray())
        {
            Assert.Equal(fall[4].GetString(), Problembericht.GeraetText(fall[0].GetString()!,
                (fall[1].GetString()!, fall[2].GetString()!, fall[3].GetString()!), "linux"));
        }
        foreach (var fall in G38.GetProperty("namen").EnumerateArray())
        {
            Assert.Equal(fall[1].GetString(), Problembericht.StandardName(DateTime.Parse(fall[0].GetString()!, CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>Kontrollfehler "Rechnername in device_text": das laufende System liefert nie den Rechnernamen.</summary>
    [Fact]
    public void GeraetOhneRechnernamen()
    {
        using var temp = new TempOrdner();
        var text = Problembericht.GeraetText("PC");
        Assert.StartsWith("PC · ", text);
        Assert.DoesNotContain(Environment.MachineName, text, StringComparison.OrdinalIgnoreCase);
        var bericht = Problembericht.Bauen(null, "A", "PC", [], DateTime.Now, temp.Pfad, Path.Combine(temp.Pfad, "update.log"));
        Assert.Contains("Gerät: " + text + "\n", bericht, StringComparison.Ordinal);
        Assert.DoesNotContain(Environment.MachineName, bericht, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BerichtWiePython()
    {
        var umgebung = G38.GetProperty("umgebung").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        var angaben = G38.GetProperty("angaben");
        var tabellen = G38.GetProperty("tabellen");
        var anzahl = 0;
        foreach (var fall in G38.GetProperty("berichte").EnumerateArray())
        {
            using var temp = new TempOrdner();
            var heim = Path.Combine(temp.Pfad, "home", "kim");
            var daten = Path.Combine(heim, "daten");
            var logs = Path.Combine(heim, "logs");
            Directory.CreateDirectory(daten);
            Directory.CreateDirectory(logs);
            var dbPfad = Path.Combine(daten, Lernstand.DateiName);
            Lernstand? db = null;
            var dbFall = fall.GetProperty("db");
            if (dbFall.ValueKind == JsonValueKind.String)
            {
                File.WriteAllBytes(dbPfad, Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("Das ist keine Datenbank. ", 40))));
                db = new Lernstand(dbPfad);
            }
            else if (dbFall.ValueKind == JsonValueKind.Object)
            {
                db = new Lernstand(dbPfad);
                foreach (var tabelle in dbFall.EnumerateObject())
                {
                    var spalten = tabellen.GetProperty(tabelle.Name).EnumerateArray().Select(s => s.GetString()!).ToList();
                    using var verbindung = db.Verbindung();
                    using var buchung = verbindung.BeginTransaction();
                    for (var i = 0; i < tabelle.Value.GetInt32(); i++)
                    {
                        using var befehl = verbindung.CreateCommand();
                        befehl.Transaction = buchung;
                        befehl.CommandText = $"INSERT INTO {tabelle.Name} ({string.Join(", ", spalten)}) VALUES ({string.Join(", ", spalten.Select((_, n) => "$p" + n))})";
                        for (var n = 0; n < spalten.Count; n++)
                        {
                            befehl.Parameters.AddWithValue("$p" + n, spalten[n] == "timestamp" ? "2026-10-20 10:00:00" : "x");
                        }
                        befehl.ExecuteNonQuery();
                    }
                    buchung.Commit();
                }
            }
            var update = Path.Combine(daten, Problembericht.UpdateName);
            foreach (var datei in fall.GetProperty("dateien").EnumerateObject())
            {
                var pfad = datei.Name == "update.log" ? update : Path.Combine(logs, datei.Name);
                File.WriteAllText(pfad, datei.Value.GetString()!.Replace("{HEIM}", heim, StringComparison.Ordinal), new UTF8Encoding(false));
            }
            var einstellungen = JsonNode.Parse(fall.GetProperty("einstellungen").GetRawText())!.AsObject();
            var text = Problembericht.Bauen(db, fall.GetProperty("version").GetString()!, fall.GetProperty("geraet").GetString()!, einstellungen,
                DateTime.Parse(fall.GetProperty("jetzt").GetString()!, CultureInfo.InvariantCulture), logs, update,
                (angaben[0].GetString()!, angaben[1].GetString()!, angaben[2].GetString()!), Laufzeit, umgebung, heim);

            var erwartet = fall.GetProperty("bericht").GetString()!.Split('\n').ToList();
            var ist = text.Split('\n').ToList();
            // Wortliste W2: "Python: …" heisst in C# "Laufzeit: …"
            Assert.Equal("Python: " + G38.GetProperty("python").GetString(), erwartet[4]);
            erwartet[4] = "Laufzeit: " + Laufzeit;
            for (var i = 0; i < erwartet.Count; i++)
            {
                if (erwartet[i].StartsWith("Datenbankgröße: ", StringComparison.Ordinal))
                {
                    // haengt an der Datei, die Python bzw. C# beim Oeffnen anlegt: gegen die eigene Groesse (G38 "groessen")
                    erwartet[i] = "Datenbankgröße: " + Problembericht.GroesseText(Problembericht.DatenbankGroesse(dbPfad));
                }
                if (OperatingSystem.IsWindows() && erwartet[i].StartsWith("Datenordner: ", StringComparison.Ordinal))
                {
                    ist[i] = ist[i].Replace('\\', '/');
                }
            }
            Assert.Equal(string.Join("\n", erwartet), string.Join("\n", ist));
            anzahl++;
        }
        Assert.Equal(8, anzahl);
    }

    /// <summary>Kontrollfehler "Token im Bericht": der eingetragene Schluessel steht nie im Bericht, auch nicht aus fehler.log.</summary>
    [Fact]
    public void TokenNieImBericht()
    {
        using var temp = new TempOrdner();
        const string token = "mein-geheimer-zugang-42";
        File.WriteAllText(Path.Combine(temp.Pfad, Fehlerprotokoll.DateiName), "Fehler mit " + token + " in der Adresse\n");
        File.WriteAllText(Path.Combine(temp.Pfad, Problembericht.HaengerName), "haenger " + token + "\n");
        var text = Problembericht.Bauen(null, "A 0.10.0", "PC", new JsonObject { ["sync_token"] = token, ["sync_repo"] = "a/b" },
            DateTime.Now, temp.Pfad, Path.Combine(temp.Pfad, "update.log"));
        Assert.DoesNotContain(token, text, StringComparison.Ordinal);
        Assert.Contains("Abgleich eingerichtet: ja", text, StringComparison.Ordinal);
    }

    /// <summary>Gegenpruefung W2 Fund 1: haenger.log steht im Bericht, auch waehrend die Haenger-Diagnose sie offen haelt (Windows).</summary>
    [Fact]
    public void OffeneHaengerDateiImBericht()
    {
        using var temp = new TempOrdner();
        var fehlerlog = new List<string>();
        try
        {
            Haenger.FuerTests(() => "", fehlerlog.Add);
            Assert.True(Haenger.Einrichten("A 0.10.0", temp.Pfad, plattform: "linux", waechter: false));
            var text = Problembericht.Bauen(null, "A 0.10.0", "PC", [], DateTime.Now, temp.Pfad, Path.Combine(temp.Pfad, "update.log"));
            Assert.Contains("--- haenger.log ---\n=== ", text, StringComparison.Ordinal);
            Assert.Contains("| Start | Version A 0.10.0 | linux | pid ", text, StringComparison.Ordinal);
        }
        finally
        {
            Haenger.FuerTests();
        }
    }

    /// <summary>Kontrollfehler "Schnitt mitten in der Zeile": gekuerzte Protokolle beginnen mit einer ganzen Zeile.</summary>
    [Fact]
    public void GekuerztAbGanzerZeile()
    {
        using var temp = new TempOrdner();
        var zeilen = string.Concat(Enumerable.Range(0, 2000).Select(i => "zeile " + i.ToString("D5", CultureInfo.InvariantCulture) + "\n"));
        File.WriteAllText(Path.Combine(temp.Pfad, "update.log"), zeilen);
        var ende = Problembericht.UpdateLogLesen(Path.Combine(temp.Pfad, "update.log"));
        Assert.Matches(@"^zeile \d{5}$", ende.Split('\n')[0]);
        Assert.True(Encoding.UTF8.GetByteCount(ende) <= Problembericht.UpdateMax);
        Assert.EndsWith("zeile 01999", ende, StringComparison.Ordinal);
    }

    [Fact]
    public void ZahltextRundetWiePython()
    {
        foreach (var (wert, stellen, text) in new[]
        {
            (1.25, 1, "1.2"), (1.35, 1, "1.4"), (2.5, 0, "2"), (3.5, 0, "4"), (0.05, 1, "0.1"), (-1.25, 1, "-1.2"),
            (-0.05, 0, "-0"), (1e-7, 1, "0.0"), (123456.789, 1, "123456.8"), (0.0, 1, "0.0"), (1023.5, 0, "1024"),
        })
        {
            Assert.Equal(text, Zahltext.Punkt(wert, stellen));
        }
    }

    /// <summary>
    /// Sammelliste 142 (W8b): Zeile zum Ton; ohne gesetzte Zeile (Tests, G38) bleibt der Bericht wie Python.
    /// Sammelliste 145 (P8-28): statt nur zu pruefen, dass die statische Zeile leer ist (rot, sobald ein anderer Test
    /// <c>DiagnoseTeil</c> laedt), wird der Bericht mit und ohne Zeile gebaut und verglichen; danach gilt der alte
    /// Wert wieder. G38 laeuft in derselben Klasse, also nie gleichzeitig.
    /// </summary>
    [Fact]
    public void TonZeile()
    {
        Assert.Equal("Ton: Gerät geöffnet in 43 ms", Problembericht.TonText(42.6, false, true));
        Assert.Equal("Ton: Gerät geöffnet in 0 ms", Problembericht.TonText(0, true, true));
        Assert.Equal("Ton: kein Gerät", Problembericht.TonText(-1, true, true));
        Assert.Equal("Ton: Gerät noch nicht geöffnet", Problembericht.TonText(-1, false, true));
        Assert.Equal("Ton: Spiel noch nicht geöffnet", Problembericht.TonText(-1, false, false));
        using var temp = new TempOrdner();
        string Bericht() => Problembericht.Bauen(null, "A", "PC", [], new DateTime(2026, 10, 28, 9, 0, 0), temp.Pfad, Path.Combine(temp.Pfad, "update.log"));
        var vorher = Problembericht.TonZeile;
        try
        {
            Problembericht.TonZeile = null;
            var ohne = Bericht().Split('\n').ToList();
            Assert.DoesNotContain(ohne, z => z.StartsWith("Ton:", StringComparison.Ordinal));
            Problembericht.TonZeile = () => Problembericht.TonText(42.6, false, true);
            var mit = Bericht().Split('\n').ToList();
            // genau eine Zeile mehr, am Ende des Kopfteils (vor der Leerzeile vor fehler.log), sonst alles gleich
            var stelle = ohne.IndexOf("--- fehler.log ---") - 1;
            Assert.True(stelle > 0 && ohne[stelle].Length == 0, "Kopfteil endet nicht vor fehler.log");
            ohne.Insert(stelle, "Ton: Gerät geöffnet in 43 ms");
            Assert.Equal(ohne, mit);
        }
        finally
        {
            Problembericht.TonZeile = vorher;
        }
    }
}
