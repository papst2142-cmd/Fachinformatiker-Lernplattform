using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>Allein, nicht neben anderen Tests: die Zahl <see cref="Vorgaenge.Wechsel"/> gilt im ganzen Prozess.</summary>
[CollectionDefinition("Uhrabweichung", DisableParallelization = true)]
public sealed class UhrabweichungSammlung;

/// <summary>
/// Weisung P8-6 (Sammelliste 108): das Spiel liest die Uhrabweichung aus sync_meta einmal je Sitzung statt je
/// Aktion. Nach jedem Vorgang mit der Sperre der Vorgaenge (Abgleich, Sicherung einspielen, Uebernahme) gilt der
/// Wert, der dann in der Datenbank steht; dazwischen bleibt der gelesene Wert, auch wenn jemand ohne die Sperre
/// in sync_meta schriebe (das tut im Programm niemand, hier nur, um das Zwischenspeichern zu zeigen).
/// </summary>
[Collection("Uhrabweichung")]
public sealed class SpielUhrabweichungTests
{
    private const string Lauf = "lprobe0000000001";
    private const string Repo = "pruefer/lernstand-test";
    private const string Token = "ghp_TestSchluessel0123456789abcdefghijklmn";

    private static readonly DateTimeOffset Zeit = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Wartet auf die Sperre wie <see cref="Vorgaenge.Sperren"/>, zaehlt aber nicht als fremder Vorgang (wie das Spiel).</summary>
    private static IDisposable? Warten()
    {
        while (true)
        {
            if (Vorgaenge.Versuchen() is { } sperre)
            {
                return sperre;
            }
            Thread.Sleep(5);
        }
    }

    private static void Gehen(Spielablauf spiel, int n) =>
        spiel.Log(Stadt.Gegangen, new PyObjekt
        {
            { "tag", 1L }, { "von", "zuhause" }, { "nach", "bitweiche" }, { "schritte", 30L + n }, { "minuten", 60L + 2 * n },
        });

    /// <summary>utc_offset der zuletzt geschriebenen Zeile des Spiels.</summary>
    private static long LetzterAbstand(Lernstand l) =>
        (long)l.Abfragen("SELECT utc_offset FROM spiel_ereignisse ORDER BY id DESC LIMIT 1")[0][0]!;

    [Fact]
    public void NachJedemVorgangGiltDerNeueWert()
    {
        using var ordner = new TempOrdner();
        var uhr = SpeichernTests.FesteUhr(() => Zeit);
        var abweichung = new Uhrabweichung(() => 1000.0, "start-test", "prozess-test");
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var einstellungen = new Einstellungen(ordner.Pfad);
        einstellungen.Setze(Abgleich.RepoSchluessel, Repo);
        einstellungen.Setze(Abgleich.TokenSchluessel, Token);
        var schreiber = new LernstandSchreiber(lernstand, uhr, null, abweichung);
        var schlange = new Spielschlange();
        var db = new SpielDatenbank(schreiber, "PC", Lauf, Warten, schlange);
        var spiel = new Spielablauf(SpielInhaltTests.Inhalt, db);
        var n = 0;

        long Schritt()
        {
            Gehen(spiel, n++);
            schlange.Leeren();
            return LetzterAbstand(lernstand);
        }

        void OhneSperreSetzen(double sekunden) =>
            Assert.True(lernstand.Schreiben("INSERT OR REPLACE INTO sync_meta (key, value) VALUES ($k, $v)",
                ("$k", Uhrabweichung.Schluessel), ("$v", abweichung.Messung(sekunden, uhr))));

        // Berlin, Sommerzeit: 120 Minuten; noch kein Abgleich, also keine Abweichung
        Assert.Equal(120, Schritt());
        Assert.Equal(1, db.UhrabweichungGelesen);
        Assert.Equal(120, Schritt());
        Assert.Equal(120, Schritt());
        Assert.Equal(1, db.UhrabweichungGelesen);   // je Sitzung einmal, nicht je Aktion

        // ohne Sperre geschrieben: das Spiel behaelt den gelesenen Wert (niemand schreibt so im Programm)
        OhneSperreSetzen(-7200);
        Assert.Equal(120, Schritt());
        Assert.Equal(1, db.UhrabweichungGelesen);

        // 1. Abgleich: misst die Uhr neu (Geraeteuhr eine Stunde vor GitHub) und schreibt sie in sync_meta
        using (var attrappe = new GitHubAttrappe())
        {
            attrappe.Datum = () => Zeit.AddHours(-1);
            using var netz = new GitHubZugang(attrappe.Basis, TimeSpan.FromSeconds(10));
            var wechsel = Vorgaenge.Wechsel;
            Abgleich.Ausfuehren(lernstand, einstellungen, "PC", netz, [], uhr, abweichung);
            Assert.True(Vorgaenge.Wechsel > wechsel);
        }
        Assert.Equal(3600.0, JsonNode.Parse((string)schreiber.UhrabweichungLesen()!)!["s"]!.GetValue<double>());
        Assert.Equal(180, Schritt());   // 120 + 60
        Assert.Equal(2, db.UhrabweichungGelesen);
        Assert.Equal(schreiber.Stempel().Abstand, LetzterAbstand(lernstand));
        Assert.Equal(180, Schritt());
        Assert.Equal(2, db.UhrabweichungGelesen);

        // 2. Sicherung einspielen: danach gilt, was in sync_meta steht
        // Sicherung erstellen liest mit der Sperre: danach wird ebenfalls neu gelesen (derselbe Wert)
        var sicherung = Ruecksicherung.Erstellen(lernstand, einstellungen.Laden(), "A 0.14.1", "PC", Zeit.UtcDateTime);
        Assert.Equal(180, Schritt());
        Assert.Equal(3, db.UhrabweichungGelesen);
        OhneSperreSetzen(-7200);
        Assert.Equal(180, Schritt());
        Wiederherstellung.Ersetzen(lernstand, Sicherungsinhalt.Lesen(sicherung), einstellungen, "A 0.14.1", "PC", null, uhr);
        Assert.Equal(0, Schritt());     // 120 - 120
        Assert.Equal(4, db.UhrabweichungGelesen);
        Assert.Equal(schreiber.Stempel().Abstand, LetzterAbstand(lernstand));

        // 3. Uebernahme (bringt keine Uhrabweichung mit): danach gilt, was in sync_meta steht
        var datei = Path.Combine(ordner.Pfad, "quelle.json.gz");
        File.WriteAllBytes(datei, sicherung);
        OhneSperreSetzen(7200);
        Assert.Equal(0, Schritt());
        Uebernahme.Uebernehmen(lernstand, Uebernahme.Lesen(UebernahmeQuelle.Sicherung, datei), [], uhr,
            Path.Combine(ordner.Pfad, "sicherungen"));
        Assert.Equal(240, Schritt());   // 120 + 120
        Assert.Equal(5, db.UhrabweichungGelesen);
        Assert.Equal(schreiber.Stempel().Abstand, LetzterAbstand(lernstand));
        Assert.Equal(0, schlange.Anzahl);
    }
}
