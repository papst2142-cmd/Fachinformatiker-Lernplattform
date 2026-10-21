using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L9 „Sicherung für die alte App erstellen“ (Weisung P2-11): Datei wie
/// fisi_sicherung.create_backup, Text wie json.dumps, eine Lesetransaktion (A1), atomares
/// Speichern, Fehlerarten fuer T3/T4, schreiber fest „0.62.4“ (A3). Kontrollfehler A5:
/// Schreiben waehrend des Exports, Speichern scheitert, Datei faellt bei der Probe durch.
/// Dass Python 0.63.2 die Datei liest und zusammenfuehrt, belegt die CI (R16, A4, F5).
/// Nur synthetische Daten (A3).
/// </summary>
public class RuecksicherungTests
{
    private static readonly DateTimeOffset Zeit = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    // -- PythonJson --------------------------------------------------------------

    [Theory]
    [InlineData(0.1, "0.1")]
    [InlineData(1e16, "1e+16")]
    [InlineData(1e15, "1000000000000000.0")]
    [InlineData(1.5e-5, "1.5e-05")]
    [InlineData(0.0001, "0.0001")]
    [InlineData(0.00001, "1e-05")]
    [InlineData(123456789012345678.0, "1.2345678901234568e+17")]
    [InlineData(-0.0, "-0.0")]
    [InlineData(2.0, "2.0")]
    [InlineData(1.0 / 3, "0.3333333333333333")]
    [InlineData(5e-324, "5e-324")]
    [InlineData(1.7976931348623157e308, "1.7976931348623157e+308")]
    [InlineData(100.0, "100.0")]
    [InlineData(1234.5, "1234.5")]
    [InlineData(double.NaN, "NaN")]
    [InlineData(double.NegativeInfinity, "-Infinity")]
    public void ZahlWiePythonRepr(double wert, string text) => Assert.Equal(text, PythonJson.Zahl(wert));

    [Fact]
    public void TextWieJsonDumps()
    {
        var wert = new PyObjekt
        {
            { "b", 1L },
            { "a", new List<object?> { 1L, "ä\u2028\u0001\u007f\"\\" } },
            { "\uffff", null },
            { "\U0001F600", true },
            { "B", 2.5 },
            { "é", -1 },
        };
        Assert.Equal("{\"B\":2.5,\"a\":[1,\"ä\u2028\\u0001\u007f\\\"\\\\\"],\"b\":1,\"é\":-1,\"\uffff\":null,\"\U0001F600\":true}",
            PythonJson.Text(wert, sortiert: true));
        Assert.StartsWith("{\"b\":1,\"a\":", PythonJson.Text(wert), StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => PythonJson.Text(new List<object?> { new byte[] { 1 } }));
        Assert.Throws<EncoderFallbackException>(() => PythonJson.Utf8("a\ud800b"));
    }

    // -- Datei ---------------------------------------------------------------------

    private static (TempOrdner Ordner, Lernstand Lernstand) Beispiel()
    {
        var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var uhr = SpeichernTests.FesteUhr(() => Zeit);
        var schreiber = new LernstandSchreiber(lernstand, uhr, null, new Uhrabweichung(() => 1.0, null, "p"));
        schreiber.KarteSpeichern("Netzwerk", "Frage A", "karte", true);
        schreiber.KarteSpeichern("Netzwerk", "Frage ä \"B\"", "karte", null);
        schreiber.QuizSpeichern("Netzwerk", "Quiz C", false);
        schreiber.ErgebnisSpeichern(30, 40, 75.0, "3", 600);
        Assert.True(lernstand.Schreiben("INSERT INTO spiel_plaetze (timestamp, platz, lauf, aktion, daten, geraet, uid) VALUES "
            + "('2026-10-20 11:00:00', 1, 'L1', 'angelegt', '{}', 'PC', 'p1')"));
        Assert.True(lernstand.Schreiben("INSERT INTO spiel_bestenliste (timestamp, utc_offset, durchgang, art, schluessel, wert, daten, uid) VALUES "
            + "('2026-10-20 11:00:00', 120, 'L1', 'erfolg', 'erster', 1, '{}', 'b1')"));
        Assert.True(lernstand.Schreiben("INSERT INTO abschlussprojekt (timestamp, projekt, feld, wert, uid) VALUES "
            + "('2026-10-20 11:00:00', 'P', 'titel', 'Mein Projekt', 'a1')"));
        Assert.True(lernstand.Schreiben("INSERT OR REPLACE INTO sync_meta (key, value) VALUES ('history_cleared_at', '2026-10-19 08:00:00+02:00')"));
        return (ordner, lernstand);
    }

    [Fact]
    public void DateiWieCreateBackup()
    {
        var (ordner, lernstand) = Beispiel();
        using var _ = ordner;
        var einstellungen = new JsonObject { ["ziel_anzahl"] = 30, ["darstellung"] = "hell", ["sync_token"] = "geheim123456" };
        var bytes = Ruecksicherung.Erstellen(lernstand, einstellungen, "A 0.5.0", "PC Test", new DateTime(2026, 10, 20, 12, 0, 5));

        var gelesen = Uebernahme.LeseSicherung(bytes);       // Pruefsumme, Typen, Probelauf wie read_backup
        Assert.Equal("A 0.5.0", gelesen.AppVersion);
        Assert.Equal(4, gelesen.Tabellen.Where(t => Uebernahme.Tabellen.Take(8).Any(e => e.Tabelle == t.Key)).Sum(t => t.Value.Count));

        var text = Entpacken(bytes);
        Assert.StartsWith("{\"art\":\"FISI-Sicherung\",\"formatversion\":1,\"app_version\":\"A 0.5.0\",\"erstellt\":\"2026-10-20T12:00:05\","
            + "\"geraet\":\"PC Test\",\"daten\":{\"format\":2,\"schreiber\":\"0.62.4\",\"gekappt\":{},\"columns\":{", text, StringComparison.Ordinal);
        Assert.DoesNotContain("geheim123456", text, StringComparison.Ordinal);      // nur das Tagesziel
        Assert.DoesNotContain("hell", text, StringComparison.Ordinal);
        var huelle = JsonNode.Parse(text)!.AsObject();
        Assert.Equal(["art", "formatversion", "app_version", "erstellt", "geraet", "daten", "einstellungen", "zusammenfassung", "pruefsumme"],
            huelle.Select(p => p.Key));
        Assert.Equal("{\"ziel_an\":true,\"ziel_anzahl\":30,\"serie_an\":true,\"erinnerung_an\":true,\"erinnerung_zeit\":\"18:00\"}",
            huelle["einstellungen"]!.ToJsonString());
        Assert.Equal("{\"lerneintraege\":4,\"spielstaende\":1,\"abzeichen\":1,\"projekte\":1}", huelle["zusammenfassung"]!.ToJsonString());
        Assert.Contains(",\"markers\":{\"history_cleared_at\":\"2026-10-19 08:00:00+02:00\"},\"tables\":{", text, StringComparison.Ordinal);
        Assert.Contains("\"Frage ä \\\"B\\\"\"", text, StringComparison.Ordinal);      // ensure_ascii=False
    }

    [Fact]
    public void SchreiberBleibtFest()
    {
        // A3: "schreiber" ist die Austauschfassung von fisi_sync (WRITER), nicht die Programmversion
        Assert.Equal("0.62.4", Ruecksicherung.SchreiberWert);
        var (ordner, lernstand) = Beispiel();
        using var _ = ordner;
        foreach (var version in new[] { "A 0.5.0", "B 0.9.0", "1.0.0" })
        {
            var text = Entpacken(Ruecksicherung.Erstellen(lernstand, [], version, "Handy", new DateTime(2026, 10, 20, 12, 0, 0)));
            Assert.Contains("\"schreiber\":\"0.62.4\"", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DateiName() =>
        Assert.Equal("Lernplattform-Sicherung_2026-10-01_1423.fisisicherung", Ruecksicherung.DateiName(new DateTime(2026, 10, 1, 14, 23, 59)));

    [Fact]
    public void NichtLesbarerLernstand()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        File.WriteAllBytes(pfad, [.. "Das ist keine Datenbank, sondern Text."u8, .. new byte[200]]);
        var vorher = File.ReadAllBytes(pfad);
        var kaputt = new Lernstand(pfad);
        Assert.Equal(RuecksicherungFehler.NichtLesbar,
            Assert.Throws<RuecksicherungAusnahme>(() => Ruecksicherung.Erstellen(kaputt, [], "A 0.5.0", "PC", DateTime.Now)).Fehler);
        Assert.Equal(vorher, File.ReadAllBytes(pfad));      // nichts veraendert
    }

    /// <summary>Kontrollfehler A5: ein anderer Schreiber waehrend des Exports kommt nicht in die Datei (eine Lesetransaktion).</summary>
    [Fact]
    public async Task SchreibenWaehrendDesExports()
    {
        var (ordner, lernstand) = Beispiel();
        using var _ = ordner;
        Task<bool>? schreiber = null;
        // nur dieser Faden: andere Tests lesen gleichzeitig (Abgleich, Sicherung) und rufen den Haken auch auf
        var faden = Environment.CurrentManagedThreadId;
        Ruecksicherung.NachTabelle = tabelle =>
        {
            if (Environment.CurrentManagedThreadId == faden && tabelle == "test_results" && schreiber is null)
            {
                var zweiter = new Lernstand(lernstand.Pfad);
                schreiber = Task.Run(() => zweiter.Schreiben(
                    "INSERT INTO card_events (timestamp, utc_offset, category, question, mode, correct, uid) VALUES "
                    + "('2026-10-20 12:30:00', 120, 'Netzwerk', 'Waehrenddessen', 'karte', 1, 'w1')"), TestContext.Current.CancellationToken);
                Thread.Sleep(400);
            }
        };
        PyObjekt daten;
        try
        {
            daten = Ruecksicherung.Lesen(lernstand);
        }
        finally
        {
            Ruecksicherung.NachTabelle = null;
        }
        Assert.DoesNotContain("Waehrenddessen", PythonJson.Text(daten), StringComparison.Ordinal);
        Assert.Equal(2, ((List<object?>)((PyObjekt)daten["tables"]!)["card_events"]!).Count);
        Assert.True(await schreiber!.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));      // der Schreiber kam danach ganz normal an die Reihe
        Assert.Equal(3, lernstand.Abfragen("SELECT 1 FROM card_events").Count);
    }

    [Fact]
    public void SpeichernAtomar()
    {
        var (ordner, lernstand) = Beispiel();
        using var _ = ordner;
        var bytes = Ruecksicherung.Erstellen(lernstand, [], "A 0.5.0", "PC", new DateTime(2026, 10, 20, 12, 0, 0));
        var ziel = Path.Combine(ordner.Pfad, "ausgabe", Ruecksicherung.DateiName(new DateTime(2026, 10, 20, 12, 0, 0)));
        Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);

        Ruecksicherung.Speichern(ziel, bytes);
        Assert.Equal(bytes, File.ReadAllBytes(ziel));
        Assert.Equal([ziel], Directory.GetFiles(Path.GetDirectoryName(ziel)!));

        // Kontrollfehler A5: Datei faellt bei der Probe durch -> vorhandene Datei bleibt, keine Hilfsdatei
        var kaputt = (byte[])bytes.Clone();
        kaputt[^10] ^= 0xFF;
        Assert.Equal(RuecksicherungFehler.Unbekannt, Assert.Throws<RuecksicherungAusnahme>(() => Ruecksicherung.Speichern(ziel, kaputt)).Fehler);
        Assert.Equal(bytes, File.ReadAllBytes(ziel));
        Assert.Equal([ziel], Directory.GetFiles(Path.GetDirectoryName(ziel)!));

        // Kontrollfehler A5: Ordner gibt es nicht -> Unbekannter Fehler, nichts angelegt
        var fehlt = Path.Combine(ordner.Pfad, "gibt_es_nicht", "x.fisisicherung");
        Assert.Equal(RuecksicherungFehler.Unbekannt, Assert.Throws<RuecksicherungAusnahme>(() => Ruecksicherung.Speichern(fehlt, bytes)).Fehler);
        Assert.False(Directory.Exists(Path.GetDirectoryName(fehlt)));
    }

    /// <summary>Handy (P2-11 A2): atomar im App-Ordner, pruefen, dann an den gewaehlten Ort kopieren; Arbeitsdatei weg.</summary>
    [Fact]
    public async Task SpeichernUeberKopie()
    {
        var (ordner, lernstand) = Beispiel();
        using var _ = ordner;
        var bytes = Ruecksicherung.Erstellen(lernstand, [], "A 0.5.0", "Handy", new DateTime(2026, 10, 20, 12, 0, 0));
        var arbeit = Path.Combine(ordner.Pfad, "ruecksicherung.fisisicherung");
        var ziel = new MemoryStream();
        await Ruecksicherung.SpeichernUeberAsync(arbeit, bytes, () => Task.FromResult<Stream>(new HalteOffen(ziel)), TestContext.Current.CancellationToken);
        Assert.Equal(bytes, ziel.ToArray());
        Assert.False(File.Exists(arbeit));
        Assert.False(File.Exists(arbeit + ".tmp"));
    }

    /// <summary>Kontrollfehler A5 „Android-Kopie scheitert“: T4 mit Grund, Arbeitsdatei entfernt.</summary>
    [Fact]
    public async Task KopieScheitert()
    {
        var (ordner, lernstand) = Beispiel();
        using var _ = ordner;
        var bytes = Ruecksicherung.Erstellen(lernstand, [], "A 0.5.0", "Handy", new DateTime(2026, 10, 20, 12, 0, 0));
        var arbeit = Path.Combine(ordner.Pfad, "ruecksicherung.fisisicherung");
        var abbruch = TestContext.Current.CancellationToken;

        // Ziel laesst sich nicht oeffnen (keine Berechtigung)
        var a = await Assert.ThrowsAsync<RuecksicherungAusnahme>(() =>
            Ruecksicherung.SpeichernUeberAsync(arbeit, bytes, () => throw new UnauthorizedAccessException(), abbruch));
        Assert.Equal(RuecksicherungFehler.KeineBerechtigung, a.Fehler);
        Assert.False(File.Exists(arbeit));

        // Schreiben bricht mitten ab (Platte voll)
        var voll = OperatingSystem.IsWindows() ? unchecked((int)0x80070070) : 28;
        var b = await Assert.ThrowsAsync<RuecksicherungAusnahme>(() =>
            Ruecksicherung.SpeichernUeberAsync(arbeit, bytes, () => Task.FromResult<Stream>(new Bricht(voll)), abbruch));
        Assert.Equal(RuecksicherungFehler.KeinPlatz, b.Fehler);
        Assert.False(File.Exists(arbeit));

        // App-Ordner fehlt: schon der erste Schritt scheitert, das Ziel wird nie geoeffnet
        var geoeffnet = false;
        var c = await Assert.ThrowsAsync<RuecksicherungAusnahme>(() => Ruecksicherung.SpeichernUeberAsync(
            Path.Combine(ordner.Pfad, "fehlt", "r.fisisicherung"), bytes, () => { geoeffnet = true; return Task.FromResult<Stream>(new MemoryStream()); }, abbruch));
        Assert.Equal(RuecksicherungFehler.Unbekannt, c.Fehler);
        Assert.False(geoeffnet);
    }

    [Fact]
    public void Texte()
    {
        Assert.Equal(("Sicherung nicht erstellt", "Der Lernstand kann nicht gelesen werden, deshalb wurde keine Sicherung erstellt. Es wurde nichts verändert."),
            RuecksicherungTexte.Fehler(RuecksicherungFehler.NichtLesbar));
        Assert.Equal(("Speichern fehlgeschlagen", "Die Sicherung konnte nicht gespeichert werden: Kein Speicherplatz mehr. Wähle einen anderen Ordner oder schaffe Platz und versuche es noch einmal."),
            RuecksicherungTexte.Fehler(RuecksicherungFehler.KeinPlatz));
        Assert.EndsWith(": Keine Berechtigung für diesen Ordner. Wähle einen anderen Ordner oder schaffe Platz und versuche es noch einmal.",
            RuecksicherungTexte.Fehler(RuecksicherungFehler.KeineBerechtigung).Text);
        Assert.Contains(": Unbekannter Fehler.", RuecksicherungTexte.Fehler(RuecksicherungFehler.Unbekannt).Text);
        Assert.Equal("Gespeichert: Lernplattform-Sicherung_2026-10-20_1200.fisisicherung",
            RuecksicherungTexte.Erfolg(Ruecksicherung.DateiName(new DateTime(2026, 10, 20, 12, 0, 0))));
        Assert.Equal("Bitte warten \u2013 die Sicherung wird erstellt.", RuecksicherungTexte.Warten);
        // T4 sagt nichts ueber eine alte Datei, die bleibt (Wortlaut P2-11, auf Android nicht belegbar)
        Assert.DoesNotContain("bleibt", RuecksicherungTexte.SpeichernText);
        // T6: keine Zusage „geschützt“, „sicher“, „verschlüsselt“
        foreach (var wort in new[] { "geschützt", " sicher", "verschlüsselt" })
        {
            Assert.DoesNotContain(wort, AbgleichTexte.Zugangsschluessel);
        }
        Assert.DoesNotContain("fisi-lernstand", AbgleichTexte.RepositoryBeispiel);
    }

    private sealed class HalteOffen(MemoryStream innen) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => innen.Length;
        public override long Position { get => innen.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => innen.Write(buffer, offset, count);
    }

    private sealed class Bricht(int code) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("voll", code);
    }

    [Fact]
    public void Fehlerarten()
    {
        Assert.Equal(RuecksicherungFehler.KeineBerechtigung, Ruecksicherung.Fehlerart(new UnauthorizedAccessException()));
        Assert.Equal(RuecksicherungFehler.Unbekannt, Ruecksicherung.Fehlerart(new IOException("x")));
        Assert.Equal(RuecksicherungFehler.Unbekannt, Ruecksicherung.Fehlerart(new InvalidOperationException()));
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(RuecksicherungFehler.KeinPlatz, Ruecksicherung.Fehlerart(new IOException("voll", unchecked((int)0x80070070))));
            Assert.Equal(RuecksicherungFehler.KeineBerechtigung, Ruecksicherung.Fehlerart(new IOException("schutz", unchecked((int)0x80070013))));
        }
        else
        {
            Assert.Equal(RuecksicherungFehler.KeinPlatz, Ruecksicherung.Fehlerart(new IOException("voll", 28)));
            Assert.Equal(RuecksicherungFehler.KeineBerechtigung, Ruecksicherung.Fehlerart(new IOException("nur lesen", 30)));
        }
    }

    [Fact]
    public async Task WartetAufAnderenVorgang()
    {
        var (ordner, lernstand) = Beispiel();
        using var _ = ordner;
        Task<PyObjekt> lauf;
        using (Vorgaenge.Sperren())
        {
            lauf = Task.Run(() => Ruecksicherung.Lesen(lernstand), TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);
            Assert.False(lauf.IsCompleted);
        }
        await lauf.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Beleg fuer die CI (R16, A3, A4, F5): drei Sicherungen fuer die alte App in
    /// beleg_ruecksicherung/ neben den Tests; werkzeuge/vergleich/pruefe_ruecksicherung.py liest
    /// sie mit Python 0.63.2 ein (read_backup, backup_summary, merge_backup).
    /// </summary>
    [Fact]
    public void BelegDateienFuerPython()
    {
        var beleg = Path.Combine(AppContext.BaseDirectory, "beleg_ruecksicherung");
        Directory.CreateDirectory(beleg);
        var erstellt = new DateTime(2026, 10, 20, 12, 0, 0);
        var (ordner, lernstand) = Beispiel();
        using (ordner)
        {
            Ruecksicherung.Speichern(Path.Combine(beleg, "voll.fisisicherung"),
                Ruecksicherung.Erstellen(lernstand, new JsonObject { ["ziel_anzahl"] = 30 }, "A 0.5.0", "PC Beleg", erstellt));
        }
        // Verlauf geleert bzw. alles zurueckgesetzt (Marke mit Zone wie die App sie setzt), danach ein neuer Eintrag
        foreach (var (name, marke) in new[] { ("verlauf_geleert", "history_cleared_at"), ("zurueckgesetzt", "reset_at") })
        {
            using var o = new TempOrdner();
            var l = new Lernstand(Path.Combine(o.Pfad, Lernstand.DateiName));
            Assert.True(l.Schreiben("INSERT INTO sync_meta (key, value) VALUES ($k, $v)", ("$k", marke), ("$v", "2026-10-20 12:00:00+02:00")));
            var schreiber = new LernstandSchreiber(l, SpeichernTests.FesteUhr(() => Zeit.AddMinutes(5)), () => "beleg-" + name,
                new Uhrabweichung(() => 1.0, null, "p"));
            schreiber.KarteSpeichern("Netzwerk", "Nach der Marke", "karte", true);
            var bytes = Ruecksicherung.Erstellen(l, [], "A 0.5.0", "Handy", erstellt);
            Ruecksicherung.Speichern(Path.Combine(beleg, name + ".fisisicherung"), bytes);
            Assert.Contains("\"" + marke + "\":\"2026-10-20 12:00:00+02:00\"", Entpacken(bytes), StringComparison.Ordinal);
        }
        foreach (var datei in Directory.GetFiles(beleg, "*.fisisicherung"))
        {
            Assert.Equal("A 0.5.0", Uebernahme.LeseSicherung(File.ReadAllBytes(datei)).AppVersion);
        }
        Assert.Equal(3, Directory.GetFiles(beleg, "*.fisisicherung").Length);
    }

    internal static string Entpacken(byte[] bytes)
    {
        using var ein = new MemoryStream(bytes);
        using var gz = new GZipStream(ein, CompressionMode.Decompress);
        using var aus = new MemoryStream();
        gz.CopyTo(aus);
        return Encoding.UTF8.GetString(aus.ToArray());
    }
}
