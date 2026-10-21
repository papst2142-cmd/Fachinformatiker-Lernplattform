using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, PB1: Schreibwege der Lernseiten wie Python (log_ap1, log_scenario, log_project,
/// save_exam, save_project_field mit purge_superseded_project_rows, project_fields; Vergleichswerte
/// G34 "speichern"). Pflichttests der Weisung P4-3: INSERT und beide DELETE in EINER Transaktion,
/// gleiche Mikrosekunde = groessere uid gilt, Zeilen gleich Python; dazu wie bei jedem Schreibweg
/// nach Lesefehler nichts, Schreibschutz, Absturz mitten im Schreiben, Dashboard zaehlt mit.
/// </summary>
public class LernseitenSpeichernTests
{
    private static readonly JsonElement G34 = VergleichsHilfe.Lade("abschlussprojekt.json").GetProperty("speichern");

    private static bool? Bewertung(JsonElement wert) => wert.ValueKind == JsonValueKind.Null ? null : wert.GetBoolean();

    /// <summary>Alle Aufrufe aus G34 mit fester Uhr und fester uid nachspielen.</summary>
    internal static LernstandSchreiber Nachspielen(string pfad, JsonElement aufrufe)
    {
        var jetzt = DateTimeOffset.MinValue;
        var uid = "";
        var schreiber = new LernstandSchreiber(new Lernstand(pfad), SpeichernTests.FesteUhr(() => jetzt), () => uid);
        foreach (var aufruf in aufrufe.EnumerateArray())
        {
            jetzt = DateTimeOffset.Parse(aufruf.GetProperty("utc").GetString()!, CultureInfo.InvariantCulture);
            uid = aufruf.GetProperty("uid").GetString()!;
            var art = aufruf.GetProperty("art").GetString();
            if (art == "feld")
            {
                Assert.True(schreiber.ProjektFeldSpeichern(aufruf.GetProperty("projekt").GetString()!,
                    aufruf.GetProperty("feld").GetString()!, aufruf.GetProperty("wert").GetString()!));
                continue;
            }
            var w = aufruf.GetProperty("werte");
            Assert.True(art switch
            {
                "ap1" => schreiber.Ap1Speichern(w[0].GetInt32(), w[1].GetString()!, w[2].GetString()!, Bewertung(w[3])),
                "szenario" => schreiber.SzenarioSpeichern(w[0].GetInt32(), w[1].GetString()!, w[2].GetString()!, Bewertung(w[3])),
                "projekt" => schreiber.ProjektSpeichern(w[0].GetInt32(), w[1].GetString()!, w[2].GetString()!, Bewertung(w[3])),
                // int(duration) wie Python: Nachkommastellen fallen weg
                _ => schreiber.PruefungSpeichern(w[0].GetString()!, w[1].GetDouble(), w[2].GetString()!, (int)Math.Truncate(w[3].GetDouble()),
                    aufruf.GetProperty("daten_text").GetString()!),
            });
        }
        return schreiber;
    }

    private static string Felder(OrderedDictionary<string, OrderedDictionary<string, string>> felder) =>
        string.Join("‖", felder.Select(p => p.Key + "=" + string.Join("¦", p.Value.Select(f => f.Key + ":" + f.Value))));

    private static string Felder(JsonElement e) =>
        string.Join("‖", e.EnumerateArray().Select(p => p[0].GetString() + "=" + string.Join("¦", p[1].EnumerateArray().Select(f => f[0].GetString() + ":" + f[1].GetString()))));

    [Fact]
    public void ZeilenWiePython()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var schreiber = Nachspielen(pfad, G34.GetProperty("aufrufe"));
        using (var db = VergleichsHilfe.Oeffnen(pfad))
        {
            foreach (var tabelle in G34.GetProperty("tabellen").EnumerateObject())
            {
                var spalten = tabelle.Value.GetProperty("spalten").EnumerateArray().Select(s => s.GetString()!).ToList();
                Assert.Equal(tabelle.Value.GetProperty("zeilen").EnumerateArray().Select(VergleichsHilfe.Zeile).ToList(),
                    VergleichsHilfe.Abfrage(db, $"SELECT {string.Join(", ", spalten)} FROM {tabelle.Name} ORDER BY id").Select(VergleichsHilfe.Zeile).ToList());
                Assert.Equal(tabelle.Value.GetProperty("typen").EnumerateArray().Select(VergleichsHilfe.Zeile).ToList(),
                    VergleichsHilfe.Abfrage(db, $"SELECT {string.Join(", ", spalten.Select(s => $"typeof({s})"))} FROM {tabelle.Name} ORDER BY id")
                        .Select(VergleichsHilfe.Zeile).ToList());
            }
        }
        Assert.Equal(Felder(G34.GetProperty("felder")), Felder(schreiber.ProjektFelder()));
    }

    /// <summary>Kontrollfehler: groben Sekunden-Stempel (zwei Fassungen gleich alt) oder Aufraeumen vergessen sieht der Vergleich.</summary>
    [Fact]
    public void KontrollfehlerSiehtDerVergleich()
    {
        var erwartet = G34.GetProperty("tabellen").GetProperty("abschlussprojekt").GetProperty("zeilen").EnumerateArray().Select(VergleichsHilfe.Zeile).ToList();
        // ohne Aufraeumen blieben alle Fassungen stehen
        var aufrufe = G34.GetProperty("aufrufe").EnumerateArray().Count(a => a.GetProperty("art").GetString() == "feld");
        Assert.NotEqual(aufrufe, erwartet.Count);
        // mit Sekunden statt Mikrosekunden fiele E4 (eine Mikrosekunde spaeter) nicht weg
        Assert.DoesNotContain(erwartet, z => z.Contains("eine Mikrosekunde spaeter", StringComparison.Ordinal));
        Assert.Contains(G34.GetProperty("aufrufe").EnumerateArray(), a => a.TryGetProperty("wert", out var w) && w.GetString() == "eine Mikrosekunde spaeter");
        // correct NULL beim Ansehen, nicht 0
        Assert.Contains(G34.GetProperty("tabellen").GetProperty("ap1_events").GetProperty("typen").EnumerateArray(), t => t[5].GetString() == "null");
    }

    /// <summary>
    /// Pflichttest P4-3 (a): INSERT und beide DELETE laufen in EINER Transaktion. Scheitert das
    /// Aufraeumen (Ausloeser in der Testdatei), steht auch das INSERT nicht da. Kontrollfehler:
    /// INSERT und DELETE als zwei Buchungen lassen die neue Zeile neben der alten stehen.
    /// </summary>
    [Fact]
    public void EineTransaktionFuerEinfuegenUndAufraeumen()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var lernstand = new Lernstand(pfad);
        var schreiber = new LernstandSchreiber(lernstand, SpeichernTests.FesteUhr("2026-10-21T08:00:00+00:00"));
        Assert.True(schreiber.ProjektFeldSpeichern("p", "ziel", "alt"));
        using (var db = VergleichsHilfe.Oeffnen(pfad))
        {
            VergleichsHilfe.Ausfuehren(db, "CREATE TRIGGER halt BEFORE DELETE ON abschlussprojekt BEGIN SELECT RAISE(ABORT, 'halt'); END");
        }
        var spaeter = new LernstandSchreiber(lernstand, SpeichernTests.FesteUhr("2026-10-21T09:00:00+00:00"));
        Assert.False(spaeter.ProjektFeldSpeichern("p", "ziel", "neu"));
        string[] Werte()
        {
            using var db = VergleichsHilfe.Oeffnen(pfad);
            return [.. VergleichsHilfe.Abfrage(db, "SELECT wert FROM abschlussprojekt ORDER BY id").Select(z => (string)z[0]!)];
        }
        Assert.Equal(["alt"], Werte());

        // Kontrollfehler: zwei getrennte Buchungen
        Assert.True(lernstand.Schreiben("INSERT INTO abschlussprojekt (timestamp, projekt, feld, wert, uid) VALUES ('2026-10-21 11:00:00.000000', 'p', 'ziel', 'neu', 'x')"));
        Assert.False(lernstand.Schreiben(LernstandSchreiber.ProjektAufraeumen));
        Assert.Equal(["alt", "neu"], Werte());
    }

    /// <summary>Pflichttest P4-3 (b): gleiche Mikrosekunde zweier Speicherungen, die groessere uid gilt (in beiden Reihenfolgen).</summary>
    [Fact]
    public void GleicheMikrosekundeGroessereUidGilt()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var uid = "";
        var schreiber = new LernstandSchreiber(new Lernstand(pfad), SpeichernTests.FesteUhr("2026-10-21T08:00:00.123456+00:00"), () => uid);
        for (var runde = 0; runde < 20; runde++)
        {
            var a = LernstandSchreiber.NeueKennung();
            var b = LernstandSchreiber.NeueKennung();
            uid = a;
            Assert.True(schreiber.ProjektFeldSpeichern("p", "feld " + runde, "a"));
            uid = b;
            Assert.True(schreiber.ProjektFeldSpeichern("p", "feld " + runde, "b"));
            var gewinner = string.CompareOrdinal(a, b) > 0 ? "a" : "b";
            Assert.Equal(gewinner, schreiber.ProjektFelder()["p"]["feld " + runde]);
            using var db = VergleichsHilfe.Oeffnen(pfad);
            Assert.Equal(1L, VergleichsHilfe.Abfrage(db, $"SELECT COUNT(*) FROM abschlussprojekt WHERE feld = 'feld {runde}'")[0][0]);
        }
    }

    [Fact]
    public void NachLesefehlerWirdNichtsGeschrieben()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        File.WriteAllBytes(pfad, [.. "keine Datenbank"u8, .. new byte[300]]);
        var vorher = SHA256.HashData(File.ReadAllBytes(pfad));
        var lernstand = new Lernstand(pfad);
        Assert.Equal("kaputt", lernstand.NurLesenGrund);
        var schreiber = new LernstandSchreiber(lernstand);
        Assert.False(schreiber.Ap1Speichern(0, "t", "x", true));
        Assert.False(schreiber.SzenarioSpeichern(0, "t", "x", null));
        Assert.False(schreiber.ProjektSpeichern(0, "t", "x", false));
        Assert.False(schreiber.PruefungSpeichern("ap1", 50, "4", 60, "{}"));
        Assert.False(schreiber.ProjektFeldSpeichern("p", "ziel", "x"));
        Assert.Equal(5, lernstand.AbgelehnteSchreibzugriffe);
        Assert.Empty(schreiber.ProjektFelder());
        Assert.Equal(vorher, SHA256.HashData(File.ReadAllBytes(pfad)));
        Assert.Equal([Fehlerprotokoll.DateiName, Lernstand.DateiName],
            Directory.EnumerateFileSystemEntries(ordner.Pfad).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>Schreibgeschuetzte Datei: nichts gespeichert, Datei bytegleich (gelingt nur ohne Root, CI laeuft ohne).</summary>
    [Fact]
    public void SchreibgeschuetzteDateiSpeichertNichts()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        Assert.True(new LernstandSchreiber(new Lernstand(pfad)).ProjektFeldSpeichern("p", "ziel", "alt"));
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(pfad, FileAttributes.ReadOnly);
        }
        else
        {
            File.SetUnixFileMode(pfad, UnixFileMode.UserRead);
        }
        var vorher = SHA256.HashData(File.ReadAllBytes(pfad));
        var lernstand = new Lernstand(pfad);
        Assert.Equal("schreibschutz", lernstand.NurLesenGrund);
        var schreiber = new LernstandSchreiber(lernstand);
        Assert.False(schreiber.ProjektFeldSpeichern("p", "ziel", "neu"));
        Assert.False(schreiber.Ap1Speichern(1, "t", "x", true));
        Assert.Equal("alt", schreiber.ProjektFelder()["p"]["ziel"]);
        Assert.Equal(vorher, SHA256.HashData(File.ReadAllBytes(pfad)));
    }

    /// <summary>
    /// Absturz mitten im Schreiben (Kindprozess, hart beendet): Datenbank in Ordnung, je Feld genau
    /// eine Zeile (INSERT nie ohne Aufraeumen), jede Zeile ganz, jedes gemeldete Feld vorhanden.
    /// </summary>
    [Fact]
    public void AbsturzMittenImSchreiben()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        _ = new Lernstand(pfad);
        for (var runde = 0; runde < 3; runde++)
        {
            var gemeldet = AbsturzProbe.Starten(AbsturzProbe.ArtProjektfeld, [pfad], 40 + runde * 37);
            using var db = VergleichsHilfe.Oeffnen(pfad);
            Assert.Equal("ok", (string)VergleichsHilfe.Abfrage(db, "PRAGMA quick_check")[0][0]!);
            var zeilen = VergleichsHilfe.Abfrage(db, "SELECT feld, wert, uid, timestamp FROM abschlussprojekt WHERE projekt = 'probe-projekt'");
            Assert.Equal(zeilen.Count, zeilen.Select(z => (string)z[0]!).Distinct().Count());
            Assert.Equal(5, zeilen.Count);
            foreach (var z in zeilen)
            {
                var n = ((string)z[2]!)["probe-".Length..];
                Assert.Equal("wert " + n + new string('.', 3000), z[1]);
                Assert.Equal(26, ((string)z[3]!).Length);
            }
            Assert.True(gemeldet.Count >= 40 + runde * 37);
        }
    }

    /// <summary>
    /// Dashboard (Tagesziel nur Bewertetes, Lerntage auch Angesehenes, wie activity_days und
    /// month_activity) und der Notizblock zaehlen die neuen Zeilen.
    /// </summary>
    [Fact]
    public void DashboardUndNotizblockZaehlenMit()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var heute = new DateOnly(2026, 10, 21);
        var schreiber = new LernstandSchreiber(new Lernstand(pfad), SpeichernTests.FesteUhr("2026-10-21T08:00:00+00:00"));
        DashboardWerte Dashboard() => DashboardWerte.Lesen(pfad, heute, new DashboardInhalte(Inhalte), Einstellungen.Ziel(new JsonObject()), null,
            Path.Combine(ordner.Pfad, "fehler.log"));
        Assert.True(schreiber.Ap1Speichern(0, Inhalte.Ap1Szenarien[0].Titel!, "", null));
        Assert.True(schreiber.SzenarioSpeichern(1, Inhalte.Ap2Szenarien[1].Titel!, "", null));
        Assert.True(schreiber.ProjektSpeichern(2, Inhalte.Testprojekte[2].Titel!, "", null));
        var werte = Dashboard();
        Assert.Equal(0, werte.Tagesziel.Erledigt);
        Assert.Contains(21, DashboardWerte.Monat(pfad, 2026, 10).Lerntage);
        Assert.True(schreiber.Ap1Speichern(0, Inhalte.Ap1Szenarien[0].Titel!, "", false));
        Assert.True(schreiber.SzenarioSpeichern(1, Inhalte.Ap2Szenarien[1].Titel!, "", true));
        Assert.True(schreiber.ProjektSpeichern(2, Inhalte.Testprojekte[2].Titel!, "", false));
        Assert.Equal(3, Dashboard().Tagesziel.Erledigt);
        var buch = new StatusBuch(Antwortbuch.Lesen(schreiber.Lernstand));
        var notiz = new Notizblock(Inhalte).Eintraege(buch);
        Assert.Equal([(Quellen.Ap1, (object)0L), (Quellen.Projekt, 2L)], notiz.Select(e => (e.Quelle, e.Schluessel)).ToList());
    }

    private static readonly Lerninhalte Inhalte = Lerninhalte.Laden(Pfade.Inhalte);
}
