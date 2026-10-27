using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Diagnose;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W5 (Plan Phase 8 Abschnitt 11, Sammelliste 111, Weisung P8-8b Option b): Feierabend ueber das Bett.
/// Sind alle Aufgaben erledigt, fragt das Bett mit Nicos erstem Text. Ohne Firma und mit offenen Tickets fragt es,
/// ob trotzdem geschlafen wird; "Schlafen gehen" verschiebt dann alle offenen Tickets genau wie der Knopf
/// "Verschieben" (dieselben Ereignisse in fester Reihenfolge) und macht denselben Feierabend. Bricht es mittendrin
/// ab (Schreibfehler, Sperre belegt), gilt nichts als Feierabend. Mit Firma und offenen Entscheidungen nur ein Hinweis.
/// </summary>
public class SpielBettTests
{
    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static Spielablauf NeuesSpiel()
    {
        var zaehler = 0;
        var spiel = new Spielablauf(Inhalt, new SpielSpeicher(() =>
        {
            zaehler += 7;
            return new DateTime(2026, 10, 3).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }));
        spiel.Profil("Nico", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" } });
        return spiel;
    }

    [Fact]
    public void RueckfrageJeNachStand()
    {
        var spiel = NeuesSpiel();
        var offen = spiel.Stand.OffeneTickets().Count;
        Assert.True(offen > 1);
        Assert.Equal((Wohnung.Schlafart.TicketsVerschieben,
            $"Für heute sind noch {offen} Aufgaben offen. Willst du wirklich schlafen gehen und Feierabend machen?"), Wohnung.SchlafFrage(spiel.Stand));
        Assert.False(spiel.Stand.FeierabendMoeglich());

        while (spiel.Stand.OffeneTickets().Count > 1)
        {
            spiel.Verschieben(spiel.Stand.OffeneTickets()[0]["id"]);
        }
        Assert.Equal((Wohnung.Schlafart.TicketsVerschieben, "Für heute ist noch 1 Aufgabe offen. Willst du wirklich schlafen gehen und Feierabend machen?"),
            Wohnung.SchlafFrage(spiel.Stand));

        spiel.Verschieben(spiel.Stand.OffeneTickets()[0]["id"]);
        Assert.Equal((Wohnung.Schlafart.Fertig, "Du hast alle Aufgaben für heute erledigt. Willst du schlafen gehen und Feierabend machen?"),
            Wohnung.SchlafFrage(spiel.Stand));
        Assert.True(spiel.Stand.FeierabendMoeglich());

        // Schlafen gehen ist derselbe Feierabend (dasselbe Ereignis tag_beendet, neuer Tag)
        var tag = spiel.Stand.Tag;
        spiel.SchlafenGehen();
        Assert.Equal(Ereignisse.Feierabend, spiel.Stand.Verlauf[^1].Art);
        Assert.Equal(tag + 1, spiel.Stand.Tag);
    }

    /// <summary>Hinweistexte mit Firma (Option a): Einzahl und Mehrzahl.</summary>
    [Fact]
    public void HinweisMitFirma()
    {
        Assert.Equal("Für heute ist noch 1 Aufgabe offen. Erledige sie zuerst, dann kannst du schlafen gehen und Feierabend machen.", Wohnung.SchlafenOffen(1));
        Assert.Equal("Für heute sind noch 3 Aufgaben offen. Erledige sie zuerst, dann kannst du schlafen gehen und Feierabend machen.", Wohnung.SchlafenOffen(3));
        // Mit Firma gibt es keine Tickets zum Verschieben: das Bett fragt (alles erledigt) oder weist hin
        var stand = SpielFingerabdruckTests.Gespielt.Value.Stand;
        Assert.True(Py.Wahr(stand.Firma));
        var (art, text) = Wohnung.SchlafFrage(stand);
        var entscheidungen = stand.OffeneEntscheidungen().Count;
        Assert.Equal(entscheidungen == 0 ? (Wohnung.Schlafart.Fertig, Wohnung.SchlafenFertig) : (Wohnung.Schlafart.Hinweis, Wohnung.SchlafenOffen(entscheidungen)),
            (art, text));
    }

    /// <summary>
    /// "Schlafen gehen" mit offenen Tickets schreibt genau dieselben Ereignisse wie der Knopf "Verschieben" je
    /// Ticket (in der Reihenfolge der offenen Tickets, auch Zwischenfall und falsche Lieferung zuerst) und danach
    /// denselben Feierabend; es entstehen keine neuen Ereignisarten.
    /// </summary>
    [Fact]
    public void SchlafenGehenVerschiebtWieDerKnopf()
    {
        var bett = NeuesSpiel();
        var knopf = NeuesSpiel();
        var vorher = bett.Stand.Verlauf.Count;
        var offen = bett.Stand.OffeneTickets().Select(a => a["id"]).ToList();
        Assert.True(offen.Count > 1);

        bett.SchlafenGehen();
        while (knopf.Stand.OffeneTickets() is { Count: > 0 } tickets)
        {
            knopf.Verschieben(tickets[0]["id"]);
        }
        knopf.Feierabend();

        Assert.Equal(Zeilen(knopf), Zeilen(bett));
        var neu = bett.Stand.Verlauf.Skip(vorher).ToList();
        Assert.Equal(offen.Count + 1, neu.Count);
        Assert.Equal(offen, neu.Take(offen.Count).Select(e => e.Daten["aufgabe"]));
        Assert.All(neu.Take(offen.Count), e => Assert.Equal(Ereignisse.Verschoben, e.Art));
        Assert.Equal(Ereignisse.Feierabend, neu[^1].Art);
    }

    private static List<string> Zeilen(Spielablauf spiel) =>
        [.. spiel.Stand.Verlauf.Select(e => PythonJson.Text(new List<object?> { e.Zeit, e.Art, e.Daten }))];

    /// <summary>Uhr, die bei jedem Ablesen 7 s weiterlaeuft (wie SpielSchlangeTests).</summary>
    private static LernstandSchreiber Schreiber(Lernstand lernstand)
    {
        var start = DateTimeOffset.Parse("2026-10-02T08:00:00+00:00", CultureInfo.InvariantCulture);
        var n = 0;
        var k = 0;
        return new LernstandSchreiber(lernstand, SpeichernTests.FesteUhr(() => start.AddSeconds(7 * ++n)),
            () => "u" + (++k).ToString("D6", CultureInfo.InvariantCulture));
    }

    private static List<string> Ereignisarten(Lernstand l) =>
        [.. l.Abfragen("SELECT typ FROM spiel_ereignisse ORDER BY id").Select(z => (string)z[0]!)];

    /// <summary>
    /// Schreibfehler mittendrin (die zweite Zeile "verschoben" scheitert): die Spielschlange schreibt danach keine
    /// Zeile dieses Durchgangs mehr, also auch keinen Feierabend. In der Datenbank steht nur das erste verschobene
    /// Ticket, der Stand aus der Datenbank hat denselben Tag und der Feierabend gilt nicht. Mit Tor wartet der
    /// Schreiber, bis alle Zeilen angehaengt sind (die Schlange verwirft den Rest); ohne Tor kann der Fehler schon
    /// vor dem naechsten Schritt da sein (der Schritt ist nicht gespeichert, das Bett hoert auf). Beides endet gleich.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SchreibfehlerMittendrinIstKeinFeierabend(bool mitTor)
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var schlange = new Spielschlange();
        var zaehlen = false;
        var verschoben = 0;
        using var tor = new ManualResetEventSlim(true);
        schlange.VorZeile = art =>
        {
            tor.Wait(TimeSpan.FromMinutes(2));
            return !zaehlen || art != Ereignisse.Verschoben || ++verschoben != 2;
        };
        var gemeldet = new List<SpielSchreibfehler>();
        schlange.Fehler += f =>
        {
            lock (gemeldet)
            {
                gemeldet.Add(f);
            }
        };
        using var sperre = new SemaphoreSlim(1, 1);   // eigene Sperre: unabhaengig von parallelen Tests
        var spiel = new Spielablauf(Inhalt, new SpielDatenbank(Schreiber(lernstand), "PC", "lbett00000000001",
            () => sperre.Wait(0) ? new Freigabe(sperre) : null, schlange));
        spiel.Profil("Nico", new PyObjekt());
        schlange.Leeren();
        var tag = spiel.Stand.Tag;
        var vorher = Ereignisarten(lernstand);
        Assert.True(spiel.Stand.OffeneTickets().Count > 2);
        var zeilen = spiel.Stand.OffeneTickets().Count + 1;   // je Ticket "verschoben", dann "tag_beendet"
        zaehlen = true;
        Task? oeffnen = null;
        if (mitTor)
        {
            // der Schreiber wartet, bis alle Zeilen angehaengt sind (der Feierabend leert danach selbst die Schlange)
            tor.Reset();
            oeffnen = Task.Run(() =>
            {
                while (!tor.IsSet && schlange.Anzahl < zeilen)
                {
                    Thread.Sleep(1);
                }
                tor.Set();
            }, TestContext.Current.CancellationToken);
        }
        try
        {
            spiel.SchlafenGehen();   // gilt im Spiel sofort, die Zeilen schreibt der Hintergrund
        }
        catch (SpielFehler fehler)
        {
            Assert.False(mitTor);
            Assert.Equal(Core.Texte.SpielProbeTexte.SchrittNichtGespeichert, fehler.Message);
        }
        finally
        {
            tor.Set();
        }
        oeffnen?.Wait(TestContext.Current.CancellationToken);
        schlange.Leeren();

        Assert.Single(gemeldet);
        Assert.Equal(Ereignisse.Verschoben, gemeldet[0].Art);
        Assert.Equal([.. vorher, Ereignisse.Verschoben], Ereignisarten(lernstand));
        Assert.DoesNotContain(Ereignisse.Feierabend, Ereignisarten(lernstand).Skip(vorher.Count));
        var ausDatenbank = new Spielstand(spiel.Speicher.Ereignisse(), Inhalt);
        Assert.Equal(tag, ausDatenbank.Tag);
        Assert.False(ausDatenbank.FeierabendMoeglich());
        Assert.Contains("Spielschritt nicht gespeichert", Fehlerprotokoll.Anzeigen(Path.Combine(ordner.Pfad, Fehlerprotokoll.DateiName)));
    }

    /// <summary>Sperre belegt (anderer Vorgang): nichts verschoben, kein Feierabend, nichts geschrieben.</summary>
    [Fact]
    public void BelegtVerschiebtNichts()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var schlange = new Spielschlange();
        using var sperre = new SemaphoreSlim(1, 1);
        IDisposable? Versuchen() => sperre.Wait(0) ? new Freigabe(sperre) : null;
        var spiel = new Spielablauf(Inhalt, new SpielDatenbank(Schreiber(lernstand), "PC", "lbett00000000002", Versuchen, schlange));
        spiel.Profil("Nico", new PyObjekt());
        schlange.Leeren();
        var vorher = Ereignisarten(lernstand);
        var stand = spiel.Stand;
        using (Versuchen())
        {
            Assert.Throws<SpielBelegtAusnahme>(() => spiel.SchlafenGehen());
        }
        schlange.Leeren();
        Assert.Same(stand, spiel.Stand);
        Assert.Equal(vorher, Ereignisarten(lernstand));
    }

    private sealed class Freigabe(SemaphoreSlim s) : IDisposable
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

    [Fact]
    public void BettHatDenKnopfSchlafen()
    {
        // Die Wohnung zeigt am Bett weiter den Knopf "Schlafen" (Python home_message), die Rueckfrage kommt danach
        foreach (var id in new[] { "matratze", "bett_einzel", "bett_doppel" })
        {
            Assert.Equal("schlafen", Wohnung.MoebelStueck(id, Inhalt)!["aktion"]);
        }
        Assert.Equal(("Feierabend", "Schlafen gehen", "Noch nicht"), (Wohnung.SchlafenTitel, Wohnung.SchlafenJa, Wohnung.SchlafenNein));
    }
}
