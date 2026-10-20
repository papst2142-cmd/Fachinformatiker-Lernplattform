using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L10 Abgleich (Weisung P2-11): Ablauf wie fisi_sync.sync gegen die Attrappe,
/// Fehlerfaelle wie die Python-Attrappe, automatische Sicherung vor dem Abgleich (A6, ein
/// Kontrollfehler je Ausloeser), Zugangsschluessel nie in Fehlertexten, Antworten oder Dateien
/// (A7), kein altes Repository und feste Adresse (A9), gegenseitiger Ausschluss (A1).
/// Nur synthetische Daten (A3).
/// </summary>
public class AbgleichTests
{
    private const string Repo = "pruefer/lernstand-test";
    private const string Token = "ghp_TestSchluessel0123456789abcdefghijklmn";

    private static readonly IReadOnlyList<(string Tabelle, string Alt, string Neu)> KeineUmbenennungen = [];

    /// <summary>Ein Geraet: eigener Datenordner, Lernstand, Einstellungen, feste Uhr (Berlin).</summary>
    private sealed class Geraet : IDisposable
    {
        private readonly TempOrdner _ordner = new();

        public Geraet(string name, DateTimeOffset jetzt, string repo = Repo, string token = Token)
        {
            Name = name;
            Jetzt = jetzt;
            Uhr = SpeichernTests.FesteUhr(() => Jetzt);
            Lernstand = new Lernstand(Path.Combine(_ordner.Pfad, Lernstand.DateiName));
            Einstellungen = new Einstellungen(_ordner.Pfad);
            Einstellungen.Setze(Abgleich.RepoSchluessel, repo);
            Einstellungen.Setze(Abgleich.TokenSchluessel, token);
            Schreiber = new LernstandSchreiber(Lernstand, Uhr, null, Abweichung);
        }

        public string Name { get; }

        public DateTimeOffset Jetzt { get; set; }

        public Uhr Uhr { get; }

        public Uhrabweichung Abweichung { get; } = new(() => 1000.0, "start-test", "prozess-test");

        public Lernstand Lernstand { get; }

        public Einstellungen Einstellungen { get; }

        public LernstandSchreiber Schreiber { get; }

        public string Ordner => _ordner.Pfad;

        public string Sicherungen => Path.Combine(_ordner.Pfad, Uebernahme.SicherungsOrdner);

        public AbgleichErgebnis Abgleichen(GitHubAttrappe attrappe, IReadOnlyList<(string, string, string)>? umbenennungen = null,
            string? sicherungsordner = null, TimeSpan? warten = null)
        {
            using var netz = new GitHubZugang(attrappe.Basis, warten ?? TimeSpan.FromSeconds(10));
            return Abgleich.Ausfuehren(Lernstand, Einstellungen, Name, netz, umbenennungen ?? KeineUmbenennungen, Uhr, Abweichung,
                sicherungsordner);
        }

        public List<object?[]> Zeilen(string sql) => Lernstand.Abfragen(sql);

        public string? Meta(string schluessel) =>
            Lernstand.Abfragen("SELECT value FROM sync_meta WHERE key = $k", ("$k", schluessel)) is [var z, ..] ? z[0] as string : null;

        public void Ausfuehren(string sql, params (string, object?)[] werte) => Assert.True(Lernstand.Schreiben(sql, werte));

        public void Dispose() => _ordner.Dispose();
    }

    private static readonly DateTimeOffset Zeit = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    private static GitHubAttrappe Attrappe(DateTimeOffset? server = null)
    {
        var a = new GitHubAttrappe();
        var s = server ?? Zeit;
        a.Datum = () => s;
        return a;
    }

    private static string[] Fragen(Geraet g) =>
        [.. g.Zeilen("SELECT question FROM card_events ORDER BY question").Select(z => (string)z[0]!)];

    // -- Ablauf ----------------------------------------------------------------

    [Fact]
    public void ErsterAbgleichLaedtHochUndZweitesGeraetUebernimmt()
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC Test", Zeit);
        using var handy = new Geraet("Handy", Zeit);
        pc.Schreiber.KarteSpeichern("Netzwerk", "Frage A", "karte", true);
        pc.Schreiber.KarteSpeichern("Netzwerk", "Frage B", "karte", false);

        var erstes = pc.Abgleichen(attrappe);
        Assert.Equal((0, 2, 0), (erstes.Erhalten, erstes.Gesendet, erstes.Entfernt));
        Assert.Single(erstes.Sicherungen);      // erster Abgleich: immer gesichert
        Assert.StartsWith(Abgleich.SicherungsPraefix, Path.GetFileName(erstes.Sicherungen[0]));
        Assert.Equal(1, attrappe.Commits);
        var put = attrappe.PutKoerper.Single();
        Assert.Equal("Lernstand von PC Test (20.10.2026 12:00)", (string?)put["message"]);
        Assert.Null(put["sha"]);
        Assert.Equal("1+pruef-attrappe@users.noreply.github.com", (string?)put["committer"]!["email"]);
        Assert.Equal("pruef-attrappe", (string?)put["author"]!["name"]);
        Assert.Equal("2026-10-20 12:00:00", JsonNode.Parse(File.ReadAllText(pc.Einstellungen.Pfad))![Abgleich.ZuletztSchluessel]!.GetValue<string>());

        // Hochgeladen ist genau export_local (wie Ruecksicherung, Austauschformat 2)
        using (var db = pc.Lernstand.Verbindung())
        {
            Assert.Equal(PythonJson.Text(Ruecksicherung.Austausch(db, null)), attrappe.Text());
        }

        var zweites = handy.Abgleichen(attrappe);
        Assert.Equal((2, 0), (zweites.Erhalten, zweites.Gesendet));
        Assert.Equal(["Frage A", "Frage B"], Fragen(handy));
        Assert.Equal(1, attrappe.Commits);      // nichts Neues: kein Hochladen

        handy.Schreiber.QuizSpeichern("Netzwerk", "Quiz C", true);
        var drittes = handy.Abgleichen(attrappe);
        Assert.Equal((0, 1), (drittes.Erhalten, drittes.Gesendet));
        Assert.NotNull(attrappe.PutKoerper[^1]["sha"]);

        var viertes = pc.Abgleichen(attrappe);
        Assert.Equal((1, 0, 0), (viertes.Erhalten, viertes.Gesendet, viertes.Entfernt));
        Assert.Empty(viertes.Sicherungen);      // nichts Vorhandenes geaendert: keine Sicherung
        Assert.Single(pc.Zeilen("SELECT 1 FROM quiz_answers"));
    }

    [Fact]
    public void GrosseDateiUeberRohdaten()
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC Test", Zeit);
        using var handy = new Geraet("Handy", Zeit);
        pc.Schreiber.KarteSpeichern("Netzwerk", "Frage A", "karte", true);
        pc.Abgleichen(attrappe);
        attrappe.Gross = true;
        Assert.Equal(1, handy.Abgleichen(attrappe).Erhalten);
        Assert.Contains(attrappe.Anfragen, a => a.Contains("Accept: application/vnd.github.raw", StringComparison.Ordinal));
    }

    [Fact]
    public void Konflikte()
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC Test", Zeit);
        pc.Schreiber.KarteSpeichern("Netzwerk", "Frage A", "karte", true);

        attrappe.PutStatus = 409;
        attrappe.PutStatusN = 1;
        Assert.Equal(1, pc.Abgleichen(attrappe).Gesendet);      // zweiter Versuch gelingt
        Assert.Equal(2, attrappe.PutKoerper.Count);

        pc.Schreiber.KarteSpeichern("Netzwerk", "Frage B", "karte", true);
        attrappe.PutStatusN = 3;
        Assert.Equal(AbgleichFehler.Gleichzeitig, Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe)).Fehler);
        attrappe.PutStatus = 422;
        attrappe.PutStatusN = 3;
        Assert.Equal(AbgleichFehler.HochladenAbgelehnt, Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe)).Fehler);
        attrappe.PutStatus = null;

        // Ein anderes Geraet laedt bei jedem Lesen hoch: sha passt nie, dreimal 409
        attrappe.FremdProGet = 1;
        Assert.Equal(AbgleichFehler.Gleichzeitig, Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe)).Fehler);
        attrappe.FremdProGet = 0;
        // Versuch 2 und 3 haben je einen fremden Eintrag uebernommen, jetzt kommt der dritte
        var danach = pc.Abgleichen(attrappe);
        Assert.Equal(1, danach.Erhalten);
        Assert.Equal(1, danach.Gesendet);       // Frage B
        Assert.Equal(5, pc.Zeilen("SELECT 1 FROM card_events").Count);
        Assert.Equal(5, JsonNode.Parse(attrappe.Text()!)!["tables"]!["card_events"]!.AsArray().Count);
    }

    [Theory]
    [InlineData(401, AbgleichFehler.TokenUngueltig)]
    [InlineData(403, AbgleichFehler.KeinSchreibrecht)]
    [InlineData(500, AbgleichFehler.Abgelehnt)]
    public void HttpFehlerBeimLesen(int code, AbgleichFehler fehler)
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC Test", Zeit);
        attrappe.GetStatus = code;
        var e = Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe));
        Assert.Equal((fehler, code), (e.Fehler, e.Code));
        Assert.Empty(attrappe.PutKoerper);
        Assert.False(Directory.Exists(pc.Sicherungen));
    }

    [Fact]
    public void RepositoryFehltUndDateiFehlt()
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC Test", Zeit);
        attrappe.RepoStatus = 404;
        Assert.Equal(AbgleichFehler.NichtGefunden, Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe)).Fehler);
        attrappe.RepoStatus = 403;
        Assert.Equal(AbgleichFehler.KeinSchreibrecht, Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe)).Fehler);
        attrappe.RepoStatus = null;
        Assert.Equal(0, pc.Abgleichen(attrappe).Gesendet);      // Datei fehlt: erster Abgleich legt sie an
        Assert.NotNull(attrappe.Roh);
    }

    [Theory]
    [InlineData("kein_json", AbgleichFehler.Unvollstaendig)]
    [InlineData("html", AbgleichFehler.Unvollstaendig)]
    [InlineData("liste", AbgleichFehler.Unvollstaendig)]
    [InlineData("abgeschnitten", AbgleichFehler.Unvollstaendig)]
    public void KaputteAntworten(string art, AbgleichFehler fehler)
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC Test", Zeit);
        attrappe.GetBody = art;
        Assert.Equal(fehler, Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe)).Fehler);
        Assert.Empty(attrappe.PutKoerper);
    }

    [Theory]
    [InlineData("kein gzip", AbgleichFehler.DateiKaputt)]
    [InlineData("{\"format\":3,\"tables\":{}}", AbgleichFehler.ZuNeu)]
    [InlineData("[1]", AbgleichFehler.DateiKaputt)]
    [InlineData("{\"format\":2,\"tables\":{\"fremd\":[[]]}}", AbgleichFehler.DateiKaputt)]
    public void KaputteDatei(string inhalt, AbgleichFehler fehler)
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC Test", Zeit);
        attrappe.Speichern(inhalt == "kein gzip" ? Encoding.UTF8.GetBytes(inhalt) : GitHubAttrappe.Packen(inhalt));
        Assert.Equal(fehler, Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe)).Fehler);
        Assert.Empty(attrappe.PutKoerper);
    }

    [Fact]
    public void KeineVerbindungUndZeitueberschreitung()
    {
        using var pc = new Geraet("PC Test", Zeit);
        // Anschluss ohne Server
        var frei = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        frei.Start();
        var port = ((System.Net.IPEndPoint)frei.LocalEndpoint).Port;
        frei.Stop();
        using (var netz = new GitHubZugang(new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture)), TimeSpan.FromSeconds(2)))
        {
            Assert.Equal(AbgleichFehler.KeineVerbindung, Assert.Throws<AbgleichAusnahme>(() =>
                Abgleich.Ausfuehren(pc.Lernstand, pc.Einstellungen, pc.Name, netz, KeineUmbenennungen, pc.Uhr, pc.Abweichung)).Fehler);
        }
        using var attrappe = Attrappe();
        attrappe.Verzoegerung = TimeSpan.FromSeconds(2);
        Assert.Equal(AbgleichFehler.KeineVerbindung,
            Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe, warten: TimeSpan.FromMilliseconds(500))).Fehler);
    }

    [Theory]
    [InlineData("", Token, AbgleichFehler.NichtEingerichtet)]
    [InlineData(Repo, "  ", AbgleichFehler.NichtEingerichtet)]
    [InlineData("nurname", Token, AbgleichFehler.RepositoryFormat)]
    [InlineData("a/b/c", Token, AbgleichFehler.RepositoryFormat)]
    public void Einrichtung(string repo, string token, AbgleichFehler fehler)
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC Test", Zeit, repo, token);
        Assert.Equal(fehler, Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe)).Fehler);
        Assert.Empty(attrappe.Anfragen);
    }

    [Fact]
    public void RepositoryMitSchraegstrichenAussen()
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC Test", Zeit, " /" + Repo + "/ ");
        pc.Abgleichen(attrappe);
        Assert.Contains(attrappe.Log, l => l.Pfad == "/repos/" + Repo + "/contents/" + GitHubAttrappe.Datei);
    }

    [Fact]
    public void NurLesenSetztAus()
    {
        using var attrappe = Attrappe();
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        File.WriteAllBytes(pfad, [.. "Das ist keine Datenbank, sondern Text."u8, .. new byte[200]]);
        var lernstand = new Lernstand(pfad);
        var einstellungen = new Einstellungen(ordner.Pfad);
        einstellungen.Setze(Abgleich.RepoSchluessel, Repo);
        einstellungen.Setze(Abgleich.TokenSchluessel, Token);
        using var netz = new GitHubZugang(attrappe.Basis, TimeSpan.FromSeconds(5));
        var e = Assert.Throws<AbgleichAusnahme>(() => Abgleich.Ausfuehren(lernstand, einstellungen, "PC", netz, KeineUmbenennungen));
        Assert.Equal((AbgleichFehler.Ausgesetzt, "kaputt"), (e.Fehler, e.Grund));
        Assert.Empty(attrappe.Anfragen);    // vor jedem Zugriff aufs Netz
    }

    // -- Uhr -------------------------------------------------------------------

    [Fact]
    public void UhrabweichungWirdGemessenUndEingerechnet()
    {
        // Geraeteuhr geht eine Stunde vor
        using var attrappe = Attrappe(Zeit.AddHours(-1));
        using var pc = new Geraet("PC Test", Zeit);
        pc.Abgleichen(attrappe);
        var messung = JsonNode.Parse(pc.Meta(Uhrabweichung.Schluessel)!)!;
        Assert.Equal(3600.0, messung["s"]!.GetValue<double>());
        Assert.Equal("2026-10-20T12:00:00+02:00", (string?)messung["gemessen"]);
        Assert.Equal("{\"s\": 3600.0, \"gemessen\": \"2026-10-20T12:00:00+02:00\", \"lauf\": 1000.0, \"start\": \"start-test\", \"prozess\": \"prozess-test\"}",
            pc.Meta(Uhrabweichung.Schluessel));
        // _stamp: Abstand enthaelt die Abweichung (120 + 60 Minuten)
        Assert.Equal(180, pc.Schreiber.Stempel().Abstand);
    }

    [Fact]
    public void OhneDatumKopfKeineMessungUndEineZeile()
    {
        using var attrappe = Attrappe();
        attrappe.Datum = null;
        using var pc = new Geraet("PC Test", Zeit);
        Interlocked.Exchange(ref Abgleich._ohneDatumGemeldet, 0);
        pc.Abgleichen(attrappe);
        pc.Abgleichen(attrappe);
        Assert.Null(pc.Meta(Uhrabweichung.Schluessel));
        var log = File.ReadAllText(Path.Combine(pc.Ordner, Fehlerprotokoll.DateiName));
        Assert.Equal(1, CountOf(log, Abgleich.OhneDatumZeile));
    }

    private static int CountOf(string text, string teil)
    {
        var n = 0;
        for (var i = text.IndexOf(teil, StringComparison.Ordinal); i >= 0; i = text.IndexOf(teil, i + 1, StringComparison.Ordinal))
        {
            n++;
        }
        return n;
    }

    // -- A6: Sicherung vor dem Abgleich, ein Kontrollfehler je Ausloeser ------------

    /// <summary>Zustand des Lernstands als Text (alle Zeilen, alle Metawerte) fuer "unveraendert".</summary>
    private static string Abbild(Geraet g) => Abbild(g.Lernstand);

    private static string Abbild(Lernstand g)
    {
        var text = new StringBuilder();
        foreach (var (tabelle, _) in Uebernahme.Tabellen)
        {
            foreach (var z in g.Abfragen($"SELECT * FROM {tabelle} ORDER BY id"))
            {
                text.Append(tabelle).Append(':').AppendJoin('|', z.Select(w => w?.ToString())).Append('\n');
            }
        }
        foreach (var z in g.Abfragen("SELECT key, value FROM sync_meta ORDER BY key"))
        {
            text.Append("meta:").AppendJoin('|', z.Select(w => w?.ToString())).Append('\n');
        }
        return text.ToString();
    }

    /// <summary>Datei im Repository mit den uebergebenen Tabellen und Marken (Austauschformat 2, schreiber).</summary>
    private static void Datei(GitHubAttrappe attrappe, JsonObject tabellen, JsonObject? marken = null)
    {
        var spalten = new JsonObject();
        foreach (var (t, s) in Uebernahme.Tabellen)
        {
            spalten[t] = new JsonArray([.. s.Select(x => (JsonNode)x)]);
        }
        attrappe.Speichern(GitHubAttrappe.Packen(new JsonObject
        {
            ["format"] = 2, ["schreiber"] = "0.62.4", ["gekappt"] = new JsonObject(), ["columns"] = spalten,
            ["markers"] = marken ?? new JsonObject(), ["tables"] = tabellen,
        }.ToJsonString()));
    }

    public static TheoryData<string> Ausloeser() => ["loeschmarke", "geloeschter_durchgang", "projekt_fassung", "fragetext", "uhr_nachtrag"];

    /// <summary>Bereitet Geraet und Datei so vor, dass der Abgleich vorhandene Eintraege aendert.</summary>
    private static (IReadOnlyList<(string, string, string)> Umbenennungen, Func<Geraet, bool> Geaendert) Vorbereiten(string fall,
        Geraet pc, GitHubAttrappe attrappe)
    {
        pc.Einstellungen.Setze(Abgleich.ZuletztSchluessel, "2026-10-19 10:00:00");   // nicht der erste Abgleich
        switch (fall)
        {
            case "loeschmarke":
                pc.Schreiber.KarteSpeichern("Netzwerk", "Frage A", "karte", true);
                Datei(attrappe, new JsonObject(), new JsonObject { ["reset_at"] = "2026-10-20 12:30:00+02:00" });
                return (KeineUmbenennungen, g => g.Zeilen("SELECT 1 FROM card_events").Count == 0);
            case "geloeschter_durchgang":
                pc.Ausfuehren("INSERT INTO spiel_ereignisse (timestamp, utc_offset, typ, daten, geraet, lauf, uid) VALUES "
                    + "('2026-10-20 11:00:00', 120, 'zug', '{}', 'PC', 'L1', 'e1')");
                Datei(attrappe, new JsonObject
                {
                    ["spiel_plaetze"] = new JsonArray(new JsonArray("p1", "2026-10-20 11:30:00", 1, "L1", "geloescht", "{}", "Handy")),
                });
                return (KeineUmbenennungen, g => g.Zeilen("SELECT 1 FROM spiel_ereignisse").Count == 0);
            case "projekt_fassung":
                pc.Ausfuehren("INSERT INTO abschlussprojekt (timestamp, projekt, feld, wert, uid) VALUES "
                    + "('2026-10-20 11:00:00', 'P', 'titel', 'alt', 'a1')");
                Datei(attrappe, new JsonObject
                {
                    ["abschlussprojekt"] = new JsonArray(new JsonArray("a2", "2026-10-20 11:30:00", "P", "titel", "neu")),
                });
                return (KeineUmbenennungen, g => g.Zeilen("SELECT 1 FROM abschlussprojekt WHERE uid = 'a1'").Count == 0);
            case "fragetext":
                pc.Schreiber.KarteSpeichern("Netzwerk", "Alter Text", "karte", true);
                Datei(attrappe, new JsonObject());
                return ([("card_events", "Alter Text", "Neuer Text")], g => Fragen(g) is ["Neuer Text"]);
            case "uhr_nachtrag":
                // Geraeteuhr eine Stunde vor; eigener, noch nicht hochgeladener Eintrag bekommt die Abweichung
                pc.Schreiber.KarteSpeichern("Netzwerk", "Frage A", "karte", true);
                attrappe.Datum = () => Zeit.AddHours(-1);
                Datei(attrappe, new JsonObject());
                return (KeineUmbenennungen, g => g.Zeilen("SELECT utc_offset FROM card_events") is [[180L]]);
            default:
                throw new ArgumentException(fall);
        }
    }

    [Theory]
    [MemberData(nameof(Ausloeser))]
    public void SicherungVorDemAbgleich(string fall)
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC Test", Zeit);
        var (umbenennungen, geaendert) = Vorbereiten(fall, pc, attrappe);
        var vorher = Abbild(pc);

        var ergebnis = pc.Abgleichen(attrappe, umbenennungen);
        Assert.True(geaendert(pc));
        var sicherung = Assert.Single(ergebnis.Sicherungen);
        Assert.Matches(@"^vor_abgleich_\d{4}-\d{2}-\d{2}_\d{6}\.db$", Path.GetFileName(sicherung));
        Assert.Equal(pc.Sicherungen, Path.GetDirectoryName(sicherung));
        // Die Sicherung enthaelt den Stand VOR dem Abgleich
        var kopie = Path.Combine(pc.Ordner, "kopie", Lernstand.DateiName);
        Directory.CreateDirectory(Path.GetDirectoryName(kopie)!);
        File.Copy(sicherung, kopie);
        Assert.Equal(vorher, Abbild(new Lernstand(kopie)));
    }

    /// <summary>Kontrollfehler A6: Sicherung scheitert (Ordner ist eine Datei) = nichts geaendert, nichts hochgeladen, T5.</summary>
    [Theory]
    [MemberData(nameof(Ausloeser))]
    public void SicherungScheitertNichtsGeaendert(string fall)
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC Test", Zeit);
        var (umbenennungen, geaendert) = Vorbereiten(fall, pc, attrappe);
        var blockiert = Path.Combine(pc.Ordner, "blockiert");
        File.WriteAllText(blockiert, "keine Ordner");
        var vorher = Abbild(pc);
        var commits = attrappe.Commits;
        var zuletzt = File.ReadAllText(pc.Einstellungen.Pfad);

        var e = Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe, umbenennungen, blockiert));
        Assert.Equal(AbgleichFehler.SicherungGescheitert, e.Fehler);
        Assert.Equal(vorher, Abbild(pc));
        Assert.False(geaendert(pc));
        Assert.Empty(attrappe.PutKoerper);
        Assert.Equal(commits, attrappe.Commits);
        Assert.Equal(zuletzt, File.ReadAllText(pc.Einstellungen.Pfad));     // sync_last unveraendert
    }

    [Fact]
    public void ErsterAbgleichOhneSicherungsmoeglichkeitAendertNichts()
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC Test", Zeit);
        pc.Schreiber.KarteSpeichern("Netzwerk", "Frage A", "karte", true);
        var blockiert = Path.Combine(pc.Ordner, "blockiert");
        File.WriteAllText(blockiert, "x");
        var vorher = Abbild(pc);
        Assert.Equal(AbgleichFehler.SicherungGescheitert,
            Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe, sicherungsordner: blockiert)).Fehler);
        Assert.Equal(vorher, Abbild(pc));
        Assert.Null(attrappe.Roh);
    }

    [Fact]
    public void ZaehlerBeiGleicherSekunde()
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC Test", Zeit);
        var (umbenennungen, _) = Vorbereiten("loeschmarke", pc, attrappe);
        Directory.CreateDirectory(pc.Sicherungen);
        var vorhanden = Path.Combine(pc.Sicherungen, "vor_abgleich_2026-10-20_120000.db");
        File.WriteAllText(vorhanden, "bleibt");
        var ergebnis = pc.Abgleichen(attrappe, umbenennungen);
        Assert.Equal("vor_abgleich_2026-10-20_120000_2.db", Path.GetFileName(Assert.Single(ergebnis.Sicherungen)));
        Assert.Equal("bleibt", File.ReadAllText(vorhanden));
    }

    // -- A7: Zugangsschluessel nie in Fehlertexten, Antworten oder Dateien ----------

    [Fact]
    public void ZugangsschluesselNirgendsAusserInDenEinstellungen()
    {
        using var attrappe = Attrappe();
        attrappe.KopfZurueck = true;        // Fehlerantworten enthalten die Anfrage samt Authorization
        using var pc = new Geraet("PC Test", Zeit);
        pc.Schreiber.KarteSpeichern("Netzwerk", "Frage A", "karte", true);
        var texte = new List<string>();
        foreach (var code in new[] { 401, 403, 404, 500 })
        {
            attrappe.GetStatus = code;
            attrappe.RepoStatus = code;     // bei 404 auch das Repository nicht erreichbar
            texte.Add(Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe)).ToString());
        }
        attrappe.GetStatus = null;
        attrappe.RepoStatus = null;
        foreach (var art in new[] { "kein_json", "abgeschnitten" })
        {
            attrappe.GetBody = art;
            texte.Add(Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe)).ToString());
        }
        attrappe.GetBody = null;
        attrappe.PutStatus = 500;
        texte.Add(Assert.Throws<AbgleichAusnahme>(() => pc.Abgleichen(attrappe)).ToString());
        attrappe.PutStatus = null;
        pc.Abgleichen(attrappe);
        // Beleg, dass der Schluessel tatsaechlich in den Antworten stand
        Assert.Contains(attrappe.Antworten, a => a.Contains(Token, StringComparison.Ordinal));
        Assert.All(texte, t => Assert.DoesNotContain(Token, t, StringComparison.Ordinal));
        Assert.DoesNotContain(Token, attrappe.Text()!, StringComparison.Ordinal);
        // Fehlerdatei: auch eine Zeile mit dem Schluessel wird gefiltert (K4, A7)
        var log = Path.Combine(pc.Ordner, Fehlerprotokoll.DateiName);
        Fehlerprotokoll.Schreiben("Probe " + Token + " Ende", log);
        foreach (var datei in Directory.EnumerateFiles(pc.Ordner, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(datei) == Einstellungen.DateiName)
            {
                continue;       // der einzige Ort (T6)
            }
            Assert.DoesNotContain(Token, Encoding.UTF8.GetString(File.ReadAllBytes(datei)), StringComparison.Ordinal);
        }
        Assert.Contains("Probe [entfernt] Ende", File.ReadAllText(log), StringComparison.Ordinal);
        Assert.Contains(Token, File.ReadAllText(pc.Einstellungen.Pfad), StringComparison.Ordinal);
    }

    // -- A9 -----------------------------------------------------------------------

    [Fact]
    public void FesteAdresseUndKeinAltesRepository()
    {
        using (var netz = new GitHubZugang())
        {
            Assert.Equal(new Uri("https://api.github.com"), netz.Basis);
        }
        // Kein oeffentlicher Weg, die Adresse zu aendern (nur der interne Konstruktor fuer Tests)
        Assert.DoesNotContain(typeof(GitHubZugang).GetConstructors(), k => k.GetParameters().Length > 0);
        Assert.Null(typeof(GitHubZugang).GetProperty(nameof(GitHubZugang.Basis))!.SetMethod);
        // Nichts im Programm nennt das alte Repository (als Vorgabe "Benutzer/…" oder Beispiel;
        // "art":"fisi-lernstand" ist die Kennung der alten Lernstand-Datei, kein Repository)
        var alt = "/fisi" + "-lernstand";
        foreach (var datei in Directory.EnumerateFiles(Path.Combine(Wurzel(), "src"), "*", SearchOption.AllDirectories)
                     .Where(d => d.EndsWith(".cs", StringComparison.Ordinal) || d.EndsWith(".axaml", StringComparison.Ordinal))
                     .Where(d => !d.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
        {
            Assert.DoesNotContain(alt, File.ReadAllText(datei), StringComparison.OrdinalIgnoreCase);
        }
        // Einstellungen ohne Eintrag: Repository leer
        using var ordner = new TempOrdner();
        var werte = new Einstellungen(ordner.Pfad).Laden();
        Assert.Null(werte[Abgleich.RepoSchluessel]);
        Assert.False(Abgleich.Eingerichtet(werte));
    }

    private static string Wurzel()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(ordner.FullName, "Directory.Build.props")))
        {
            ordner = ordner.Parent!;
        }
        return ordner.FullName;
    }

    // -- A1: gegenseitiger Ausschluss --------------------------------------------

    [Fact]
    public async Task WartetAufAnderenVorgang()
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC Test", Zeit);
        var ct = TestContext.Current.CancellationToken;
        Task<AbgleichErgebnis> lauf;
        using (Vorgaenge.Sperren())
        {
            lauf = Task.Run(() => pc.Abgleichen(attrappe), ct);
            await Task.Delay(500, ct);
            Assert.False(lauf.IsCompleted);
            Assert.Empty(attrappe.Anfragen);
        }
        await lauf.WaitAsync(TimeSpan.FromSeconds(30), ct);
        Assert.NotNull(attrappe.Roh);
    }

    [Fact]
    public void AbstaendeBeiZeitumstellung()
    {
        var berlin = SpeichernTests.FesteUhr(() => Zeit).Zone;
        Assert.Equal([120.0, 60.0], Abgleich.Abstaende(new DateTime(2026, 3, 29, 2, 30, 0), berlin).Order().Reverse());
        Assert.Equal([120.0, 60.0], Abgleich.Abstaende(new DateTime(2026, 10, 25, 2, 30, 0), berlin).Order().Reverse());
        Assert.Equal([120.0], Abgleich.Abstaende(new DateTime(2026, 7, 1, 12, 0, 0), berlin));
    }
}
