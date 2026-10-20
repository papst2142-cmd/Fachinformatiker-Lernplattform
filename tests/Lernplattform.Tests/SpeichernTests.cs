using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Lernen;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L2 "Antworten speichern" und Ergebnis des Pruefungstrainers: Zeilen, Zeitstempel
/// und Noten exakt wie Python (G17). Pflichttests: Absturz mitten im Schreiben, nach einem
/// Lesefehler nichts schreiben, schreibgeschuetzte Datei.
/// </summary>
public class SpeichernTests
{
    private static readonly JsonElement G17 = VergleichsHilfe.Lade("trainer.json");

    internal static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    internal static Uhr FesteUhr(Func<DateTimeOffset> jetzt) => new(jetzt, Berlin);

    internal static Uhr FesteUhr(string utc) =>
        FesteUhr(() => DateTimeOffset.Parse(utc, CultureInfo.InvariantCulture, DateTimeStyles.None));

    [Fact]
    public void NotenWiePython()
    {
        var anzahl = 0;
        foreach (var fall in G17.GetProperty("noten").EnumerateArray())
        {
            var prozent = Trainer.Prozent(fall[0].GetInt32(), fall[1].GetInt32());
            Assert.Equal(BitConverter.DoubleToInt64Bits(fall[2].GetDouble()), BitConverter.DoubleToInt64Bits(prozent));
            Assert.Equal(fall[3].GetString(), Trainer.Note(prozent));
            Assert.Equal(fall[4].GetString(), Trainer.DeZahl(prozent, 1));
            anzahl++;
        }
        Assert.Equal(1890, anzahl);
        foreach (var fall in G17.GetProperty("notengrenzen").EnumerateArray())
        {
            Assert.Equal(fall[1].GetString(), Trainer.Note(fall[0].GetDouble()));
            Assert.Equal(fall[2].GetString(), Trainer.DeZahl(fall[0].GetDouble(), 1));
        }
    }

    [Fact]
    public void ZahlenWiePython()
    {
        foreach (var fall in G17.GetProperty("zahlen").EnumerateArray())
        {
            // -0.0 steht in JSON als -0.0; GetDouble liefert das Vorzeichen mit
            var x = fall[0].GetDouble();
            Assert.True(fall[2].GetString() == Trainer.DeZahl(x, fall[1].GetInt32()), $"{fall[0].GetRawText()} mit {fall[1].GetInt32()} Stellen");
        }
    }

    [Fact]
    public void StempelWiePython()
    {
        foreach (var fall in G17.GetProperty("stempel").EnumerateArray())
        {
            var uhr = FesteUhr(fall.GetProperty("utc").GetString()!);
            var schreiber = new LernstandSchreiber(new Lernstand(Path.Combine(Path.GetTempPath(), "unbenutzt_" + Guid.NewGuid().ToString("N"), "x.db")), uhr);
            var (zeit, abstand) = schreiber.Stempel();
            Assert.Equal(fall.GetProperty("zeit").GetString(), zeit);
            Assert.Equal(fall.GetProperty("abstand").GetInt32(), abstand);
            Assert.Equal(fall.GetProperty("mit_zone").GetString(), Uhr.MitZone(uhr.Jetzt()));
        }
    }

    [Fact]
    public void ZeilenWiePython()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var jetzt = DateTimeOffset.MinValue;
        var nummer = 0;
        var schreiber = new LernstandSchreiber(new Lernstand(pfad), FesteUhr(() => jetzt),
            () => "trainer-uid-" + (++nummer).ToString("0000", CultureInfo.InvariantCulture));
        foreach (var aufruf in G17.GetProperty("aufrufe").EnumerateArray())
        {
            jetzt = DateTimeOffset.Parse(aufruf.GetProperty("utc").GetString()!, CultureInfo.InvariantCulture);
            var a = aufruf.GetProperty("argumente");
            var ok = aufruf.GetProperty("art").GetString() switch
            {
                "karte" => schreiber.KarteSpeichern(a[0].GetString()!, a[1].GetString()!, a[2].GetString()!,
                    a[3].ValueKind == JsonValueKind.Null ? null : a[3].GetBoolean()),
                "quiz" => schreiber.QuizSpeichern(a[0].GetString()!, a[1].GetString()!, a[2].GetBoolean()),
                _ => schreiber.ErgebnisSpeichern(a[0].GetInt32(), a[1].GetInt32(), a[2].GetDouble(), a[3].GetString()!, a[4].GetInt32()),
            };
            Assert.True(ok);
        }
        using var db = VergleichsHilfe.Oeffnen(pfad);
        foreach (var tabelle in G17.GetProperty("zeilen").EnumerateObject())
        {
            var spalten = tabelle.Value.GetProperty("spalten").EnumerateArray().Select(s => s.GetString()!).ToList();
            Assert.Equal(tabelle.Value.GetProperty("zeilen").EnumerateArray().Select(VergleichsHilfe.Zeile).ToList(),
                VergleichsHilfe.Abfrage(db, $"SELECT {string.Join(", ", spalten)} FROM {tabelle.Name} ORDER BY id").Select(VergleichsHilfe.Zeile).ToList());
            Assert.Equal(tabelle.Value.GetProperty("typen").EnumerateArray().Select(VergleichsHilfe.Zeile).ToList(),
                VergleichsHilfe.Abfrage(db, $"SELECT {string.Join(", ", spalten.Select(s => $"typeof({s})"))} FROM {tabelle.Name} ORDER BY id")
                    .Select(VergleichsHilfe.Zeile).ToList());
        }
    }

    /// <summary>Kontrollfehler: UTC statt Ortszeit im Zeitstempel faellt im Vergleich auf.</summary>
    [Fact]
    public void KontrollfehlerUtcStattOrtszeitFaellt()
    {
        var fall = G17.GetProperty("stempel")[0];
        var utc = DateTimeOffset.Parse(fall.GetProperty("utc").GetString()!, CultureInfo.InvariantCulture);
        Assert.NotEqual(fall.GetProperty("zeit").GetString(), Uhr.Text(utc));
        Assert.Equal(fall.GetProperty("zeit").GetString(), Uhr.Text(FesteUhr(() => utc).Jetzt()));
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
        Assert.False(schreiber.KarteSpeichern("a", "b", "lernen", true));
        Assert.False(schreiber.QuizSpeichern("a", "b", false));
        Assert.False(schreiber.ErgebnisSpeichern(1, 2, 50.0, "4", 3));
        Assert.Equal(3, lernstand.AbgelehnteSchreibzugriffe);
        Assert.Equal(vorher, SHA256.HashData(File.ReadAllBytes(pfad)));
        // Nur das Fehlerprotokoll kommt dazu (wie Python fehler.log), keine zweite Datenbank, kein Journal
        Assert.Equal([Fehlerprotokoll.DateiName, Lernstand.DateiName],
            Directory.EnumerateFileSystemEntries(ordner.Pfad).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>Schreibgeschuetzte Datei: nichts gespeichert, Datei bytegleich (gelingt nur ohne Root, CI laeuft ohne).</summary>
    [Fact]
    public void SchreibgeschuetzteDateiSpeichertKeineAntwort()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        _ = new Lernstand(pfad);
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
        Assert.False(new LernstandSchreiber(lernstand).KarteSpeichern("a", "b", "lernen", true));
        Assert.Equal(vorher, SHA256.HashData(File.ReadAllBytes(pfad)));
    }

    /// <summary>
    /// Absturz mitten im Schreiben: ein Kindprozess speichert Antworten in einer Schleife und wird
    /// hart beendet. Danach ist die Datenbank in Ordnung (quick_check), jede Zeile ist ganz da,
    /// und alles, was vor dem Abbruch als gespeichert gemeldet wurde, ist vorhanden.
    /// </summary>
    [Fact]
    public void AbsturzMittenImSchreiben()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        _ = new Lernstand(pfad);
        for (var runde = 0; runde < 3; runde++)
        {
            var gemeldet = AbsturzProbe.Starten(AbsturzProbe.ArtSpeichern, [pfad], 40 + runde * 37);
            using var db = VergleichsHilfe.Oeffnen(pfad);
            Assert.Equal("ok", (string)VergleichsHilfe.Abfrage(db, "PRAGMA quick_check")[0][0]!);
            var zeilen = VergleichsHilfe.Abfrage(db, "SELECT question, uid, category, mode, correct, timestamp, utc_offset FROM card_events WHERE uid LIKE 'probe-%'");
            foreach (var z in zeilen)
            {
                // Zeile ganz: alle Felder passen zur laufenden Nummer der Frage
                var n = ((string)z[0]!)["Frage ".Length..].TrimEnd('.');
                Assert.Equal("Frage " + n + new string('.', 3000), z[0]);
                Assert.Equal("probe-" + n, z[1]);
                Assert.Equal("Fachbereich " + n, z[2]);
                Assert.Equal("lernen", z[3]);
                Assert.NotNull(z[5]);
                Assert.NotNull(z[6]);
            }
            var vorhanden = zeilen.Select(z => (string)z[1]!).ToHashSet();
            foreach (var uid in gemeldet)
            {
                Assert.Contains(uid, vorhanden);
            }
            Assert.True(gemeldet.Count >= 40 + runde * 37);
        }
    }
}
