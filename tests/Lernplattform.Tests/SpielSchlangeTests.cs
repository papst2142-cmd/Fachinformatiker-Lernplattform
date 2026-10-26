using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Tests;

/// <summary>
/// Weisung P7-17 Teil 2 (Nico 26.10.2026, Weg b): Spielschritte im Hintergrund speichern. Ein einziger
/// Schreiber in fester Reihenfolge; dieselben Zeilen wie beim sofortigen Schreiben (R3, nur angehaengt);
/// die Sperre der Vorgaenge bleibt, bis alles geschrieben ist; belegt aendert nichts; ein Schreibfehler
/// verliert nichts still und der Stand kommt neu aus der Datenbank; bricht das Programm nach einer Aktion
/// ab, fehlen hoechstens die offenen Zeilen und die Datenbank ist heil.
/// </summary>
public class SpielSchlangeTests
{
    private const string Lauf = "lprobe0000000001";

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    /// <summary>Uhr, die bei jedem Ablesen 7 s weiterlaeuft (wie SpielDatenbankTests).</summary>
    private static LernstandSchreiber Schreiber(Lernstand lernstand, string startUtc = "2026-10-02T08:00:00+00:00")
    {
        var start = DateTimeOffset.Parse(startUtc, CultureInfo.InvariantCulture);
        var n = 0;
        var k = 0;
        return new LernstandSchreiber(lernstand, SpeichernTests.FesteUhr(() => start.AddSeconds(7 * ++n)),
            () => "u" + (++k).ToString("D6", CultureInfo.InvariantCulture));
    }

    private static Lernstand Neu(TempOrdner ordner, string name = Lernstand.DateiName) => new(Path.Combine(ordner.Pfad, name));

    private static string Tabellen(Lernstand l) => PythonJson.Text(new List<object?>
    {
        Tabelle(l, "SELECT id, timestamp, utc_offset, typ, daten, geraet, uid, lauf FROM spiel_ereignisse ORDER BY id"),
        Tabelle(l, "SELECT id, timestamp, utc_offset, durchgang, art, schluessel, wert, daten, uid FROM spiel_bestenliste ORDER BY id"),
        Tabelle(l, "SELECT key, value FROM sync_meta ORDER BY key"),
    });

    private static List<object?> Tabelle(Lernstand l, string sql) =>
        [.. l.Abfragen(sql).Select(z => (object?)z.Select(w => w is DBNull ? null : w).ToList())];

    private static List<string> Ereigniszeilen(Lernstand l) =>
        [.. l.Abfragen("SELECT id, timestamp, typ, daten, uid FROM spiel_ereignisse ORDER BY id").Select(z => PythonJson.Text(z.ToList()))];

    /// <summary>
    /// Fingerabdruck f2b59ce8 im Hintergrund unter Last (der Schreiber ist langsamer als das Spiel, die
    /// Schlange fuellt sich): alle Zeilen beider Tabellen genau wie beim sofortigen Schreiben, gleicher
    /// Hash, gleich viele schrittweise Aktionen, keine Abweichung.
    /// </summary>
    [Fact]
    public void FingerabdruckImHintergrundUnterLast()
    {
        using var ordner = new TempOrdner();
        var sofortStand = Neu(ordner, "sofort.db");
        var sofort = SpielFingerabdruckTests.Spielen(new SpielDatenbank(Schreiber(sofortStand), "PC", Lauf, Vorgaenge.Sperren));

        var lernstand = Neu(ordner);
        var schlange = new Spielschlange();
        var hoechstens = 0;
        var zeilen = 0;
        schlange.VorZeile = _ =>
        {
            hoechstens = Math.Max(hoechstens, schlange.Anzahl);
            if (++zeilen % 25 == 0)
            {
                Thread.Sleep(5);   // Last: der Schreiber haengt hinterher
            }
            return true;
        };
        var spiel = SpielFingerabdruckTests.Spielen(new SpielDatenbank(Schreiber(lernstand), "PC", Lauf, Vorgaenge.Sperren, schlange));
        schlange.Leeren();

        Assert.Equal(SpielFingerabdruckTests.SollHash, SpielFingerabdruckTests.Hash(spiel.Stand));
        Assert.Equal(0, spiel.Abweichungen);
        Assert.Equal(0, spiel.NichtGespeichert);
        Assert.Equal(Tabellen(sofortStand), Tabellen(lernstand));
        Assert.True(hoechstens > 10, "die Schlange hat sich nicht gefuellt: " + hoechstens);
        Assert.Equal(0, schlange.Anzahl);
        // der Stand aus der Datenbank ist derselbe
        Assert.Empty(spiel.Stand.Unterschiede(new Spielstand(spiel.Speicher.Ereignisse(), Inhalt)));
        Assert.Equal(sofort.Stand.Verlauf.Count, spiel.Stand.Verlauf.Count);
    }

    /// <summary>Zweiter Fingerabdruck 86ac8f19 (Stadt) in der Datenbank im Hintergrund, Zeilen wie sofort.</summary>
    [Fact]
    public void StadtFingerabdruckImHintergrund()
    {
        using var ordner = new TempOrdner();
        var sofortStand = Neu(ordner, "sofort.db");
        // dieselben Zeitstempel wie die Uhr des Szenarios (Ortszeit ab 2026-10-03 00:00, je Ablesen 7 s)
        const string Start = "2026-10-02T22:00:00+00:00";
        var (sofort, sofortStaende) = SpielStadtTests.StadtSzenario(speicher: new SpielDatenbank(Schreiber(sofortStand, Start), "PC", Lauf, Vorgaenge.Sperren));
        Assert.Equal(SpielStadtTests.SollStadtHash, SpielStadtTests.StadtHash(sofort, sofortStaende));
        var lernstand = Neu(ordner);
        var schlange = new Spielschlange();
        var (spiel, staende) = SpielStadtTests.StadtSzenario(speicher: new SpielDatenbank(Schreiber(lernstand, Start), "PC", Lauf, Vorgaenge.Sperren, schlange));
        schlange.Leeren();

        Assert.Equal(SpielStadtTests.SollStadtHash, SpielStadtTests.StadtHash(spiel, staende));
        Assert.Equal(0, spiel.Abweichungen);
        Assert.Equal(Tabellen(sofortStand), Tabellen(lernstand));
    }

    private sealed class Merker(IDisposable innen) : IDisposable
    {
        public bool Frei { get; private set; }

        public void Dispose()
        {
            Frei = true;
            innen.Dispose();
        }
    }

    /// <summary>Ein Weg in der Stadt: ein Ereignis ohne Erfolge und ohne Zeile in der Bestenliste.</summary>
    private static void Gehen(Spielablauf spiel, int n) =>
        spiel.Log(Stadt.Gegangen, new PyObjekt
        {
            { "tag", 1L }, { "von", "zuhause" }, { "nach", "bitweiche" }, { "schritte", 30L + n }, { "minuten", 60L + 2 * n },
        });

    /// <summary>Tor fuer den Schreiber: geschlossen wartet er vor jeder Zeile (hoechstens 2 Minuten, dann schreibt er).</summary>
    private static Func<string, bool> Warten(ManualResetEventSlim tor) => _ =>
    {
        tor.Wait(TimeSpan.FromMinutes(2));
        return true;
    };

    /// <summary>
    /// Die Aktion gilt sofort, ihre Zeilen kommen spaeter; die Sperre der Vorgaenge bleibt, bis alles
    /// geschrieben ist, und jeder Vorgang (AlleLeeren) wartet darauf und sieht danach alle Zeilen.
    /// </summary>
    [Fact]
    public async Task SperreBleibtBisAllesGeschrieben()
    {
        using var ordner = new TempOrdner();
        var lernstand = Neu(ordner);
        var schlange = new Spielschlange();
        using var tor = new ManualResetEventSlim(true);
        schlange.VorZeile = Warten(tor);
        var merker = new List<Merker>();
        var speicher = new SpielDatenbank(Schreiber(lernstand), "PC", Lauf, () =>
        {
            var m = new Merker(Vorgaenge.Sperren());
            merker.Add(m);
            return m;
        }, schlange);
        var spiel = new Spielablauf(Inhalt, speicher);
        spiel.Profil("Probe", new PyObjekt());
        schlange.Leeren();
        Assert.True(merker[^1].Frei);
        var vorher = Ereigniszeilen(lernstand).Count;
        var geholt = merker.Count;
        tor.Reset();
        try
        {
            Gehen(spiel, 1);
            Gehen(spiel, 2);

            // im Spiel sofort, in der Datenbank noch nicht; die Sperre wird gehalten (je Aktion nicht neu geholt)
            Assert.Equal(vorher + 2, spiel.Stand.Verlauf.Count);
            Assert.Equal(vorher, Ereigniszeilen(lernstand).Count);
            Assert.True(schlange.Anzahl >= 1);
            Assert.Equal(geholt + 1, merker.Count);
            Assert.False(merker[^1].Frei);
            Assert.Equal(2, spiel.Schritte);   // schrittweise aus dem Zwischenstand, ohne zu warten

            var leeren = Task.Run(Spielschlange.AlleLeeren, TestContext.Current.CancellationToken);
            Assert.False(await Fertig(leeren, 200));
        }
        finally
        {
            tor.Set();
        }
        Spielschlange.AlleLeeren();
        Assert.True(merker[^1].Frei);
        Assert.Equal(spiel.Stand.Verlauf.Count, Ereigniszeilen(lernstand).Count);
        Assert.Empty(spiel.Stand.Unterschiede(new Spielstand(speicher.Ereignisse(), Inhalt)));
        // naechste Aktion: neue Sperre, weiter schrittweise
        Gehen(spiel, 3);
        Assert.Equal(3, spiel.Schritte);
        schlange.Leeren();
        Assert.Equal(geholt + 2, merker.Count);
        Assert.True(merker[^1].Frei);
    }

    /// <summary>Platzverwaltung: jede eigene Aktion wartet, bis die Zeilen des Spiels geschrieben sind (G44 im Hintergrund in SpielVerwaltungTests).</summary>
    [Fact]
    public async Task VerwaltungLeertVorJederAktion()
    {
        using var ordner = new TempOrdner();
        var lernstand = Neu(ordner);
        var schlange = new Spielschlange();
        using var tor = new ManualResetEventSlim(true);
        schlange.VorZeile = Warten(tor);
        var verwaltung = new Spielverwaltung(Schreiber(lernstand), "PC", Inhalt, sperre: Vorgaenge.Sperren, schlange: schlange);
        verwaltung.ZumSchreiben().Profil("Anna", new PyObjekt());
        schlange.Leeren();
        var vorher = Ereigniszeilen(lernstand).Count;
        tor.Reset();
        try
        {
            Gehen(verwaltung.ZumSchreiben(), 1);
            var neu = Task.Run(() => verwaltung.NeuerLauf(2), TestContext.Current.CancellationToken);
            Assert.False(await Fertig(neu, 200));   // wartet, bis die Zeile geschrieben ist
            tor.Set();
            Assert.True(await Fertig(neu, 60_000));
        }
        finally
        {
            tor.Set();
        }
        Assert.Equal(vorher + 1, Ereigniszeilen(lernstand).Count);
    }

    /// <summary>Andere Sperre, damit der Test nicht von parallelen Tests mit der echten Sperre abhaengt.</summary>
    private sealed class EigeneSperre
    {
        private readonly SemaphoreSlim sperre = new(1, 1);

        public IDisposable? Versuchen() => sperre.Wait(0) ? new Frei(sperre) : null;

        public bool Belegt => sperre.CurrentCount == 0;

        private sealed class Frei(SemaphoreSlim s) : IDisposable
        {
            private int frei;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref frei, 1) == 0)
                {
                    s.Release();
                }
            }
        }
    }

    /// <summary>Belegt: die Aktion wirft vorher, nichts im Speicher geaendert, nichts in der Schlange oder Datenbank.</summary>
    [Fact]
    public void BelegtAendertNichts()
    {
        using var ordner = new TempOrdner();
        var lernstand = Neu(ordner);
        var schlange = new Spielschlange();
        var sperre = new EigeneSperre();
        var spiel = new Spielablauf(Inhalt, new SpielDatenbank(Schreiber(lernstand), "PC", Lauf, sperre.Versuchen, schlange));
        spiel.Profil("Probe", new PyObjekt());
        schlange.Leeren();
        Assert.False(sperre.Belegt);
        var vorher = spiel.Stand;
        var zeilen = Tabellen(lernstand);
        using (sperre.Versuchen())
        {
            Assert.Throws<SpielBelegtAusnahme>(() => spiel.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", 1L } }));
            Assert.Throws<SpielBelegtAusnahme>(() => spiel.BestenlisteLoeschen());
        }
        Assert.Same(vorher, spiel.Stand);
        Assert.Equal(0, schlange.Anzahl);
        Assert.Equal(zeilen, Tabellen(lernstand));
    }

    /// <summary>
    /// Haelt eine andere Datenbank derselben Schlange (alter Durchgang nach dem Wechsel) die Sperre nur noch,
    /// bis ihre Zeilen geschrieben sind, wird einmal geleert statt belegt gemeldet.
    /// </summary>
    [Fact]
    public async Task FremdeOffeneZeilenWerdenGeleert()
    {
        using var ordner = new TempOrdner();
        var lernstand = Neu(ordner);
        var schlange = new Spielschlange();
        var sperre = new EigeneSperre();
        using var tor = new ManualResetEventSlim(true);
        schlange.VorZeile = Warten(tor);
        var schreiber = Schreiber(lernstand);
        var alt = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, "PC", "lalt000000000001", sperre.Versuchen, schlange));
        alt.Profil("Alt", new PyObjekt());
        schlange.Leeren();
        tor.Reset();
        try
        {
            Gehen(alt, 1);
            Assert.True(sperre.Belegt);
            var neu = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, "PC", "lneu000000000002", sperre.Versuchen, schlange));
            var oeffnen = Task.Run(async () =>
            {
                await Task.Delay(150);
                tor.Set();
            }, TestContext.Current.CancellationToken);
            neu.Profil("Neu", new PyObjekt());
            await oeffnen;
            schlange.Leeren();
            Assert.False(sperre.Belegt);
            Assert.Equal(alt.Stand.Verlauf.Count + neu.Stand.Verlauf.Count, Ereigniszeilen(lernstand).Count);
        }
        finally
        {
            tor.Set();
        }
    }

    /// <summary>
    /// Schreibfehler: nie still. Ereignis mit der Zahl der verworfenen Zeilen, Eintrag in fehler.log, die
    /// Datenbank enthaelt genau den Anfang wie beim sofortigen Schreiben. Bis der Stand neu aus der Datenbank
    /// kommt, schreibt keine Aktion auf die Luecke (nicht gespeichert, neu aufgebaut); danach geht es weiter.
    /// </summary>
    [Fact]
    public void SchreibfehlerVerliertNichtsStill()
    {
        using var ordner = new TempOrdner();
        var sofortStand = Neu(ordner, "sofort.db");
        var sofort = new Spielablauf(Inhalt, new SpielDatenbank(Schreiber(sofortStand), "PC", Lauf, Vorgaenge.Sperren));
        sofort.Profil("Probe", new PyObjekt());
        for (var i = 1; i <= 5; i++)
        {
            Gehen(sofort, i);
        }
        var alleZeilen = Ereigniszeilen(sofortStand);

        var lernstand = Neu(ordner);
        var schlange = new Spielschlange();
        using var tor = new ManualResetEventSlim(true);
        var gezaehlt = false;
        var n = 0;
        schlange.VorZeile = _ =>
        {
            tor.Wait(TimeSpan.FromMinutes(2));
            return !gezaehlt || ++n != 2;   // die zweite Zeile nach dem Profil scheitert
        };
        var gemeldet = new List<SpielSchreibfehler>();
        schlange.Fehler += f =>
        {
            lock (gemeldet)
            {
                gemeldet.Add(f);
            }
        };
        var spiel = new Spielablauf(Inhalt, new SpielDatenbank(Schreiber(lernstand), "PC", Lauf, Vorgaenge.Sperren, schlange));
        spiel.Profil("Probe", new PyObjekt());
        schlange.Leeren();
        var vorFehler = Ereigniszeilen(lernstand).Count;
        gezaehlt = true;
        tor.Reset();
        int offen;
        try
        {
            for (var i = 1; i <= 5; i++)
            {
                Gehen(spiel, i);
            }
            offen = schlange.Anzahl;
            Assert.Equal(5, offen);
        }
        finally
        {
            tor.Set();
        }
        schlange.Leeren();

        var fehler = Assert.Single(gemeldet);
        Assert.Equal(Lauf, fehler.Lauf);
        Assert.Equal(Stadt.Gegangen, fehler.Art);
        Assert.Equal(offen - 2, fehler.Verworfen);
        Assert.Contains("Spielschritt nicht gespeichert", Fehlerprotokoll.Anzeigen(Path.Combine(ordner.Pfad, Fehlerprotokoll.DateiName)));
        Assert.Equal(alleZeilen.Take(vorFehler + 1), Ereigniszeilen(lernstand));   // genau der Anfang, nichts durcheinander

        // ohne Neuaufbau: die naechste Aktion schreibt nicht auf die Luecke, der Stand kommt aus der Datenbank
        Gehen(spiel, 6);
        schlange.Leeren();
        Assert.Equal(1, spiel.NichtGespeichert);
        Assert.Equal(vorFehler + 1, Ereigniszeilen(lernstand).Count);
        Assert.Empty(spiel.Stand.Unterschiede(new Spielstand(spiel.Speicher.Ereignisse(), Inhalt)));
        Assert.Equal(vorFehler + 1, spiel.Stand.Verlauf.Count);
        // danach wird wieder geschrieben, und der Neuaufbau gleicht der Datenbank
        Gehen(spiel, 7);
        schlange.Leeren();
        Assert.Equal(vorFehler + 2, Ereigniszeilen(lernstand).Count);
        var neu = spiel.Neuaufbau();
        Assert.Empty(neu.Unterschiede(new Spielstand(spiel.Speicher.Ereignisse(), Inhalt)));
        Assert.Equal(vorFehler + 2, neu.Verlauf.Count);
        Assert.Single(gemeldet);
    }

    /// <summary>
    /// Abbruch nach einer Aktion: zwischen je zwei Zeilen wird die Datenbankdatei kopiert (so sieht sie aus,
    /// wenn das Programm dort endet). Jede Kopie ist heil, enthaelt einen Anfang der Zeilen und laedt.
    /// </summary>
    [Fact]
    public void AbbruchNachAktionLaesstDieDatenbankHeil()
    {
        using var ordner = new TempOrdner();
        var lernstand = Neu(ordner);
        var schlange = new Spielschlange();
        var kopien = new List<string>();
        schlange.VorZeile = _ =>
        {
            var ziel = Path.Combine(ordner.Pfad, "abbruch_" + kopien.Count.ToString("D3", CultureInfo.InvariantCulture) + ".db");
            File.Copy(lernstand.Pfad, ziel);
            kopien.Add(ziel);
            return true;
        };
        var spiel = new Spielablauf(Inhalt, new SpielDatenbank(Schreiber(lernstand), "PC", Lauf, Vorgaenge.Sperren, schlange));
        Spielen(spiel);
        schlange.Leeren();
        var alle = Ereigniszeilen(lernstand);
        Assert.True(kopien.Count >= alle.Count);   // dazu die Zeilen der Bestenliste
        var zuvor = 0;
        for (var i = 0; i < kopien.Count; i++)
        {
            using (var verbindung = new SqliteConnection("Data Source=" + kopien[i] + ";Mode=ReadOnly;Pooling=False"))
            {
                verbindung.Open();
                using var befehl = verbindung.CreateCommand();
                befehl.CommandText = "PRAGMA integrity_check";
                Assert.Equal("ok", befehl.ExecuteScalar());
            }
            var kopie = new Lernstand(kopien[i]);
            var zeilen = Ereigniszeilen(kopie);
            Assert.Equal(alle.Take(zeilen.Count), zeilen);   // ein Anfangsstueck, Zeile fuer Zeile
            Assert.True(zeilen.Count >= zuvor && zeilen.Count <= zuvor + 1);
            zuvor = zeilen.Count;
            var speicher = new SpielDatenbank(Schreiber(kopie), "PC", Lauf, Vorgaenge.Sperren);
            Assert.Equal(zeilen.Count, new Spielstand(speicher.Ereignisse(), Inhalt).Verlauf.Count);
        }
        Assert.InRange(zuvor, alle.Count - 1, alle.Count);   // hoechstens die letzte Zeile kam nach der letzten Kopie
    }

    private static async Task<bool> Fertig(Task aufgabe, int ms) =>
        await Task.WhenAny(aufgabe, Task.Delay(ms, TestContext.Current.CancellationToken)) == aufgabe;

    /// <summary>Ein paar Aktionen mit Erfolgen und Bestenliste.</summary>
    private static void Spielen(Spielablauf spiel)
    {
        spiel.Profil("Probe", new PyObjekt());
        for (var i = 0; i < 4; i++)
        {
            spiel.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-" + i }, { "tag", 1L }, { "richtig", true }, { "geld", 60L } });
        }
        spiel.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", 1L } });
    }

    /// <summary>Vor jedem Vorgang leert die Oberflaeche alle Spielschlangen (vor der Belegt-Pruefung).</summary>
    [Fact]
    public void OberflaecheLeertVorJedemVorgang()
    {
        var wurzel = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(wurzel.FullName, "Directory.Build.props")))
        {
            wurzel = wurzel.Parent!;
        }
        string Lesen(params string[] teile) => File.ReadAllText(Path.Combine([wurzel.FullName, "src", .. teile])).Replace("\r\n", "\n");
        string Text(string datei) => Lesen("Lernplattform.App", datei);
        foreach (var datei in new[] { "Views/RuecksicherungTeil.cs", "Views/EinspielenTeil.cs", "Views/UebernahmeTeil.cs", "Programm/UpdateDienst.cs" })
        {
            var text = Text(datei);
            var belegt = text.Split("if (Vorgaenge.Belegt)").Length - 1;
            Assert.True(belegt > 0, datei);
            Assert.Equal(belegt, text.Split("Spielschlange.AlleLeeren();   // Weisung P7-17: Spielschritte zuerst fertig schreiben\n        if (Vorgaenge.Belegt)").Length - 1);
        }
        foreach (var datei in new[] { "Views/OptionenSeite.cs", "Programm/AbgleichDienst.cs", "Spiel/SpielSeite.cs", "App.axaml.cs" })
        {
            Assert.Contains("Spielschlange.AlleLeeren();", Text(datei));
        }
        var android = Lesen("Lernplattform.Android", "MainActivity.cs");
        Assert.Contains("protected override void OnPause()\n    {\n        Lernplattform.Core.Spiel.Spielschlange.AlleLeeren();", android);
        Assert.Contains("protected override void OnStop()\n    {\n        Lernplattform.Core.Spiel.Spielschlange.AlleLeeren();", android);
    }
}
