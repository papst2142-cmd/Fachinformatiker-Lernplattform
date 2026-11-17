using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Texte;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.89.0 (Weisung P12-297 B, Nico b und 5a): Quests in Paket-Staedten. Die Probe-Stadt ist das Paket
/// <c>probe_klein</c> mit den Quest-Abschnitten der Quest-Tests (eigener Questgeber draussen, zwei neue Quests mit Kette, eine
/// umgehaengte Quest von Stadt 1). Geprueft: nur die neuen Quests, nur in ihrer Stadt; Loesen schreibt das vorhandene Ereignis
/// mit Vorsatz; zwei Geraete (eines ohne Paket) rechnen gleich; ohne Paket ruhen sie, mit Paket geht es weiter; Stadt 1 bleibt.
/// </summary>
public sealed class SpielPaketQuestsTests
{
    private const string Kennung = "probe_klein";
    private const string Name = "Probe: kleine Stadt";
    private const string Q1 = Kennung + ":neu-q-1";
    private const string Q2 = Kennung + ":neu-q-2";

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    /// <summary>Die Paket-Stadt mit Quests (Stadt-Datei aus dem Probe-Paket, dazu die Abschnitte der Quest-Tests).</summary>
    internal static Stadt PaketStadtMitQuests(Stadt haupt)
    {
        var f = StadtFassung.Lesen(SpielPaketStaedteTests.PaketStadtDatei());
        var daten = (PyObjekt)Py.Lesen(PythonJson.Text(f.Stadtdaten))!;
        foreach (var (k, v) in SpielQuestTests.Abschnitte())
        {
            Py.Setzen(daten, k, v);
        }
        var inhalt = Questebene.Anwenden(f.MitKarte(Inhalt), daten, f.Innenraeume);
        return new Stadt(inhalt, daten) { Fassung = f }.AlsPaket(Kennung, Name, haupt);
    }

    private static Spielablauf Neu(Stadt stadt1)
    {
        var zaehler = 0;
        var spiel = new Spielablauf(stadt1.Inhalt, new SpielSpeicher(() =>
        {
            zaehler += 7;
            return new DateTime(2026, 10, 1).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }));
        spiel.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        return spiel;
    }

    private static void InDiePaketStadt(Stadt stadt1, Spielablauf spiel)
    {
        Assert.True(stadt1.Gehen(spiel, Stadt.Bahnhof).Ok);
        Assert.True(stadt1.Zug(spiel, Stadt.BahnhofVon(Kennung), Name).Ok);
    }

    private static double Ruf(Spielstand s) => SpielDaten.Objekt(s.Reputation).Sum(p => Py.F(p.Value));

    private static List<string> Ids(IEnumerable<PyObjekt> liste) => [.. liste.Select(a => Py.Str(a["id"]))];

    private static object? Richtig(Stadt paket, string id) => Bewertung.Loesung(PaketQuests.Auftrag(paket, id)!, null, paket.Inhalt);

    [Fact]
    public void NurNeueQuests_MitVorsatz_NurInIhrerStadt_Stadt1Unveraendert()
    {
        var stadt1 = SpielPaketStaedteTests.Stadt1();
        var paket = PaketStadtMitQuests(stadt1);
        var spiel = Neu(stadt1);
        // Regel 1 und 2: nur die neuen Quests (die umgehaengte ports-serverraum bleibt in Stadt 1), Kennungen mit Vorsatz
        Assert.Equal([Q1, Q2], Ids(PaketQuests.Alle(paket)));
        Assert.Equal(Q1, PaketQuests.Auftrag(paket, Q2)!["nach"]);
        Assert.Empty(PaketQuests.Alle(stadt1));
        Assert.Equal(Kennung, PaketQuests.PaketVon(Q1));
        Assert.Null(PaketQuests.PaketVon("ports-serverraum"));
        Assert.False(PaketQuests.IstPaketAufgabe(SpielDaten.Falschlieferung + "1"));
        // Regel 3: offen ist nur die erste der Kette; sie steht nicht unter den Tickets von Stadt 1
        Assert.Equal([Q1], Ids(PaketQuests.Offen(paket, spiel.Stand)));
        Assert.DoesNotContain(spiel.Stand.OffeneTickets(), a => PaketQuests.IstPaketAufgabe(a["id"]));
        var auftraege = PaketQuests.Auftraege(paket, spiel.Stand, Questebene.OrtDraussen);
        Assert.True(auftraege.Get("neu-qg-1") is List<object?> { Count: 1 });
        // Stadt 1 sieht in ihren Spielinhalten nichts vom Paket (Inhalt, Tickets, Auftraege bleiben dasselbe)
        Assert.DoesNotContain(SpielDaten.Liste(stadt1.Inhalt["aufgaben"]).Select(SpielDaten.Objekt), a => Py.Str(a["id"]).StartsWith("neu-q", StringComparison.Ordinal));
        // Questlog und Zahl am Handy: in der Paket-Stadt mit der Quest, in Stadt 1 ohne
        var s1 = new StadtSpiel(stadt1, spiel);
        var vorher = Questlog.ZahlOffen(s1);
        InDiePaketStadt(stadt1, spiel);
        var sp = new StadtSpiel(paket, spiel);
        Assert.Contains(Questlog.Offen(sp), e => e.Id == Q1);
        Assert.DoesNotContain(Questlog.Offen(new StadtSpiel(stadt1, spiel)), e => e.Id == Q1);
        Assert.Equal(Questlog.ZahlOffen(new StadtSpiel(stadt1, spiel)) + 1, Questlog.ZahlOffen(sp));
        Assert.Equal(vorher, Questlog.ZahlOffen(new StadtSpiel(stadt1, spiel)));
        // die Person der Paket-Stadt bietet den Auftrag an (Knopf „Auftrag annehmen“)
        var greta = SpielDaten.Liste(paket.Inhalt["kollegen"]).Select(SpielDaten.Objekt).Single(p => Py.Gleich(p["id"], "neu-qg-1"));
        var (_, _, knoepfe) = PaketQuests.Nachricht(paket, spiel.Stand, greta, Questebene.OrtDraussen);
        Assert.Equal([("auftrag:" + Q1, "Auftrag annehmen")], knoepfe);
    }

    [Fact]
    public void Loesen_SchreibtErledigtMitVorsatz_KetteGehtWeiter_ZaehltNichtZumTag_Gegenprobe()
    {
        var stadt1 = SpielPaketStaedteTests.Stadt1();
        var paket = PaketStadtMitQuests(stadt1);
        var spiel = Neu(stadt1);
        // ausserhalb der Paket-Stadt: Grund, keine Zeile
        var zeilen = spiel.Stand.Verlauf.Count;
        Assert.Equal(ZugTexte.QuestAndereStadt, Assert.Throws<SpielFehler>(() => spiel.PaketLoesen(paket, Q1, Richtig(paket, Q1), false)).Message);
        Assert.Equal(zeilen, spiel.Stand.Verlauf.Count);
        InDiePaketStadt(stadt1, spiel);
        var tickets = Ids(spiel.Stand.OffeneTickets());
        var geld = Py.F(spiel.Stand.Geld);
        var ruf = Ruf(spiel.Stand);
        spiel.PaketLoesen(paket, Q1, Richtig(paket, Q1), false);
        var e = spiel.Stand.Verlauf[^1];
        Assert.Equal(Ereignisse.Erledigt, e.Art);
        Assert.Equal(Q1, e.Daten["aufgabe"]);
        Assert.True(Py.Wahr(e.Daten["richtig"]));
        Assert.True(Py.F(e.Daten["geld"]) >= 50, PythonJson.Text(e.Daten));
        Assert.Equal(geld + Py.F(e.Daten["geld"]), Py.F(spiel.Stand.Geld));
        Assert.True(Ruf(spiel.Stand) > ruf);
        Assert.Contains(Q1, spiel.Stand.Geloest);
        // heute nicht noch einmal; die Kette geht weiter
        Assert.Throws<SpielFehler>(() => spiel.PaketLoesen(paket, Q1, Richtig(paket, Q1), false));
        Assert.Equal([Q2], Ids(PaketQuests.Offen(paket, spiel.Stand)));
        // Regel 3: die Tickets des Tages in Stadt 1 bleiben genau gleich (die Quest nimmt keinen Platz)
        Assert.Equal(tickets, Ids(spiel.Stand.OffeneTickets()));
        // Gegenprobe: ohne die Regel naehme die Quest der Bitweiche einen Platz
        PaketQuests.ImTagAltFuerTests = true;
        try
        {
            Assert.NotEqual(tickets, Ids(spiel.Stand.OffeneTickets()));
        }
        finally
        {
            PaketQuests.ImTagAltFuerTests = false;
        }
        // Neuaufbau aus dem Verlauf rechnet gleich
        var geldNachher = spiel.Stand.Geld;
        spiel.Neuaufbau();
        Assert.Equal(geldNachher, spiel.Stand.Geld);
        Assert.Contains(Q1, spiel.Stand.Geloest);
        // falsche Antwort: richtig = nein, Quest bleibt offen fuer morgen (wie ein Ticket)
        spiel.PaketLoesen(paket, Q2, new PyObjekt { { "RJ45", "Bild" }, { "HDMI", "Netzwerk" } }, false);
        Assert.False(Py.Wahr(spiel.Stand.Verlauf[^1].Daten["richtig"]));
        Assert.DoesNotContain(Q2, spiel.Stand.Geloest);
        Assert.Empty(PaketQuests.Offen(paket, spiel.Stand));
    }

    [Fact]
    public void Ruhen_OhnePaket_NichtsGehtVerloren_MitPaketWeiter()
    {
        var stadt1 = SpielPaketStaedteTests.Stadt1();
        var paket = PaketStadtMitQuests(stadt1);
        var spiel = Neu(stadt1);
        InDiePaketStadt(stadt1, spiel);
        spiel.PaketLoesen(paket, Q1, Richtig(paket, Q1), false);
        Assert.Equal(Stadt.Bahnhof, paket.Stand(spiel.Stand).Ort);
        Assert.True(paket.Zug(spiel, Stadt.Bahnhof, ZugTexte.Stadt1).Ok);
        // Paket entfernt: nur Stadt 1; die Quest ist geloest, aber nirgends zu sehen, kein Fehler
        var ohne = new StadtSpiel(stadt1, spiel);
        Assert.Contains(Q1, spiel.Stand.Geloest);
        Assert.DoesNotContain(Questlog.Offen(ohne), e => PaketQuests.IstPaketAufgabe(e.Id));
        Assert.DoesNotContain(Questlog.Erledigte(ohne), e => PaketQuests.IstPaketAufgabe(e.Id));
        // naechster Tag, Paket wieder geladen (neue Stadt aus der Datei): geloest bleibt geloest, die naechste ist da
        spiel.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", 1L }, { "gehalt", 0L } });
        var wieder = PaketStadtMitQuests(stadt1);
        Assert.Equal([Q2], Ids(PaketQuests.Offen(wieder, spiel.Stand)));
        InDiePaketStadt(stadt1, spiel);
        var sp = new StadtSpiel(wieder, spiel);
        Assert.Contains(Questlog.Erledigte(sp), e => e.Id == Q1);
        Assert.Contains(Questlog.Offen(sp), e => e.Id == Q2);
        spiel.PaketLoesen(wieder, Q2, Richtig(wieder, Q2), false);
        Assert.Contains(Q2, spiel.Stand.Geloest);
    }

    /// <summary>Zwei Geraete wie FW8: der PC loest die Quest, das Handy hat das Paket nicht und rechnet genau gleich.</summary>
    [Fact]
    public void ZweiGeraete_EinesOhnePaket_RechnenGleich()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var n = 0;
        var start = DateTimeOffset.Parse("2026-10-03T06:00:00+00:00", CultureInfo.InvariantCulture);
        var schreiber = new LernstandSchreiber(lernstand, SpeichernTests.FesteUhr(() => start.AddSeconds(5 * ++n)),
            () => "pq" + n.ToString("D6", CultureInfo.InvariantCulture));
        var stadt1 = SpielPaketStaedteTests.Stadt1();
        var paket = PaketStadtMitQuests(stadt1);
        const string LaufId = "lpq0000000000001";
        Assert.True(schreiber.SpielPlatzSpeichern(1, LaufId, Spielstaende.Angelegt, "{}", "PC"));
        Spielablauf Geraet(string g) => new(stadt1.Inhalt, new SpielDatenbank(schreiber, g, LaufId, Vorgaenge.Sperren));
        var pc = Geraet("PC");
        pc.Profil("Nico", new PyObjekt());
        InDiePaketStadt(stadt1, pc);
        pc.PaketLoesen(paket, Q1, Richtig(paket, Q1), false);
        var handy = Geraet("Handy");
        Assert.Equal(pc.Stand.Geld, handy.Stand.Geld);
        Assert.Equal(PythonJson.Text(pc.Stand.Reputation), PythonJson.Text(handy.Stand.Reputation));
        Assert.Equal(pc.Stand.Geloest.OrderBy(x => Py.Str(x), StringComparer.Ordinal), handy.Stand.Geloest.OrderBy(x => Py.Str(x), StringComparer.Ordinal));
        Assert.Equal(Ids(pc.Stand.OffeneTickets()), Ids(handy.Stand.OffeneTickets()));
        Assert.Contains(Q1, handy.Stand.Geloest);
        // mit dem Paket auf dem Handy: dieselbe Quest ist offen wie am PC
        Assert.Equal(Ids(PaketQuests.Offen(paket, pc.Stand)), Ids(PaketQuests.Offen(PaketStadtMitQuests(stadt1), handy.Stand)));
    }
}
