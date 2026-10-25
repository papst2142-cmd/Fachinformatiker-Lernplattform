using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W7b (Weisung P7-13): Bank und Amt (nur C#). Orte, Oeffnungszeiten und Innenraeume mit
/// Schalter und Person; Kredite nur in der geoeffneten Bank, Gruendung nur auf dem geoeffneten Amt,
/// beides mit denselben Ereignissen wie die Firmen-Tafel (keine neuen Ereignisse). Die drei
/// Fingerabdruecke (f2b59ce8, 86ac8f19, 185dee1b) prueft weiter SpielGeschaefteTests.DritterFingerabdruck.
/// </summary>
public class SpielSchalterTests
{
    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static Stadt NeueStadt(PyObjekt? daten = null) => new(Inhalt, daten ?? SpielStadtTests.Daten.Value);

    private static Func<string> Uhr()
    {
        var zaehler = 0;
        return () =>
        {
            zaehler += 7;
            return new DateTime(2026, 10, 3).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        };
    }

    /// <summary>Die Ereignisse des Python-Szenarios 045 in einem neuen Speicher (bis vor <paramref name="bis"/>).</summary>
    private static Spielablauf Szenario045(string? bis = null)
    {
        var speicher = new SpielSpeicher(Uhr());
        foreach (var e in SpielFingerabdruckTests.Gespielt.Value.Stand.Verlauf)
        {
            if (e.Art == bis)
            {
                break;
            }
            speicher.Schreiben(e.Art, PythonJson.Standard(e.Daten));
        }
        return new Spielablauf(Inhalt, speicher);
    }

    /// <summary>Hingehen; ist der Ort noch zu, bis zum Oeffnen warten.</summary>
    private static void Hin(Stadt stadt, Spielablauf spiel, string ort)
    {
        Assert.Equal(StadtGrund.Ok, stadt.Gehen(spiel, ort).Grund);
        if (stadt.Betreten(spiel.Stand) == StadtGrund.Geschlossen)
        {
            Assert.Equal(StadtGrund.Ok, stadt.Warten(spiel).Grund);
        }
        Assert.Equal(StadtGrund.Ok, stadt.Betreten(spiel.Stand));
    }

    // -- Inhalt -------------------------------------------------------------------

    [Fact]
    public void InhaltOhneFehler()
    {
        var stadt = NeueStadt();
        Assert.Empty(stadt.Pruefen());
        Assert.Empty(Schalter.PruefenInhalt(stadt, SpielInnenraumTests.Innenraeume.Value));
        Assert.Equal(((long, long)?)(540L, 1080L), stadt.Oeffnungszeit(Schalter.Bank));
        Assert.Equal(((long, long)?)(480L, 960L), stadt.Oeffnungszeit(Schalter.Amt));
        // beide sind vom ersten Tag an frei (wie die Geschaefte)
        var stand = new Spielablauf(Inhalt).Stand;
        Assert.Contains(Stadt.Orte(stand, Inhalt, stadt.Regeln), o => Py.Gleich(o["id"], Schalter.Bank));
        Assert.Contains(Stadt.Orte(stand, Inhalt, stadt.Regeln), o => Py.Gleich(o["id"], Schalter.Amt));
    }

    [Fact]
    public void PruefungFindetFehler()
    {
        var innen = (PyObjekt)Py.Lesen(PythonJson.Text(SpielInnenraumTests.Innenraeume.Value))!;
        SpielDaten.Objekt(innen["bank"]).RemoveAll(p => p.Key == "personen");
        foreach (var raum in SpielDaten.Liste(SpielDaten.Objekt(SpielDaten.Objekt(innen["amt"])["gebaeude"])["raeume"]).Select(SpielDaten.Objekt))
        {
            ((List<object?>)raum["deko"]!).RemoveAll(d => Py.Str(SpielDaten.Objekt(d)["typ"]) == "tresen");
        }
        var fehler = Schalter.PruefenInhalt(NeueStadt(), innen);
        Assert.Contains("bank: keine Person am Schalter", fehler);
        Assert.Contains("amt: kein Schalter (tresen)", fehler);
        Assert.Contains("bank: kein Grundriss in innenraeume.json", Schalter.PruefenInhalt(NeueStadt(), null));
        var ohneZeit = (PyObjekt)Py.Lesen(PythonJson.Text(SpielStadtTests.Daten.Value))!;
        SpielDaten.Objekt(ohneZeit["oeffnungszeiten"]).RemoveAll(p => p.Key == "amt");
        Assert.Contains("amt: keine Oeffnungszeit", Schalter.PruefenInhalt(NeueStadt(ohneZeit), SpielInnenraumTests.Innenraeume.Value));
    }

    // -- Bank -----------------------------------------------------------------------

    [Fact]
    public void KreditNurInDerGeoeffnetenBank()
    {
        var stadt = NeueStadt();
        var spiel = Szenario045();
        Assert.True(Py.Wahr(spiel.Stand.Firma));
        var paket = Wirtschaft.KreditPakete(spiel.Stand, Inhalt).First(a => !Py.Wahr(a["problem"]));
        var zahl = spiel.Stand.Verlauf.Count;
        // zu Hause und vor dem Oeffnen (07:00 + Weg vor 09:00) geht es nicht, nichts geschrieben
        Assert.Equal(Schalter.NurInBank, Assert.Throws<SpielFehler>(() => spiel.BankKreditAufnehmen(stadt, 0L, 0L, paket["paket"])).Message);
        Assert.Equal(StadtGrund.Ok, stadt.Gehen(spiel, Schalter.Bank).Grund);
        Assert.Equal(StadtGrund.Geschlossen, stadt.Betreten(spiel.Stand));
        zahl = spiel.Stand.Verlauf.Count;
        Assert.Equal(Schalter.NurInBank, Assert.Throws<SpielFehler>(() => spiel.BankKreditAufnehmen(stadt, 0L, 0L, paket["paket"])).Message);
        Assert.Equal(zahl, spiel.Stand.Verlauf.Count);
        Assert.Equal(StadtGrund.Ok, stadt.Warten(spiel).Grund);
        // geoeffnet: dasselbe Ereignis wie take_loan in der Firmen-Tafel
        var soll = SpielAktionen.KreditAufnehmen(spiel.Stand, 0L, 0L, paket["paket"]);
        var geld = spiel.Stand.Geld;
        spiel.BankKreditAufnehmen(stadt, 0L, 0L, paket["paket"]);
        var s = spiel.Stand;
        // (danach haengt check_achievements hoechstens freigeschaltete Erfolge an)
        var neu = s.Verlauf.Skip(zahl + 1).ToList();
        Assert.Equal(Ereignisse.Kredit, neu[0].Art);
        Assert.All(neu.Skip(1), e => Assert.Equal(Ereignisse.Erfolg, e.Art));
        Assert.Equal(PythonJson.Standard(Assert.Single(soll).Daten), PythonJson.Standard(neu[0].Daten));
        Assert.Equal(geld + Py.Int(paket["summe"]), s.Geld);
        // abloesen: wie repay_loan
        var kredit = neu[0].Daten["kredit"];
        var abloesen = SpielAktionen.KreditAbloesen(s, kredit);
        zahl = s.Verlauf.Count;
        spiel.BankKreditAbloesen(stadt, kredit);
        neu = spiel.Stand.Verlauf.Skip(zahl).ToList();
        Assert.Equal(Ereignisse.KreditAbgeloest, neu[0].Art);
        Assert.All(neu.Skip(1), e => Assert.Equal(Ereignisse.Erfolg, e.Art));
        Assert.Equal(PythonJson.Standard(Assert.Single(abloesen).Daten), PythonJson.Standard(neu[0].Daten));
        Assert.Equal(0, spiel.Abweichungen);
        // auf dem Amt gibt es keine Kredite
        Hin(stadt, spiel, Schalter.Amt);
        Assert.Equal(Schalter.NurInBank, Assert.Throws<SpielFehler>(() => spiel.BankKreditAufnehmen(stadt, 0L, 0L, paket["paket"])).Message);
        // nach Feierabend der Bank (18:00) auch nicht mehr
        Assert.True(stadt.Stand(spiel.Stand).Uhr < 1080);
    }

    [Fact]
    public void BankOhneFirmaWieDieTafel()
    {
        var stadt = NeueStadt();
        var spiel = SpielStadtTests.StadtSzenario().Spiel;
        Assert.False(Py.Wahr(spiel.Stand.Firma));
        Hin(stadt, spiel, Schalter.Bank);
        var tafel = Assert.Throws<SpielFehler>(() => SpielAktionen.KreditAufnehmen(spiel.Stand, 1000L, 50L)).Message;
        var zahl = spiel.Stand.Verlauf.Count;
        Assert.Equal(tafel, Assert.Throws<SpielFehler>(() => spiel.BankKreditAufnehmen(stadt, 1000L, 50L)).Message);
        Assert.Equal(zahl, spiel.Stand.Verlauf.Count);
    }

    // -- Amt ------------------------------------------------------------------------

    [Fact]
    public void GruendungNurAufDemGeoeffnetenAmt()
    {
        var stadt = NeueStadt();
        // Szenario 045 bis vor der Gruendung: alle Voraussetzungen erfuellt, noch keine Firma
        var original = SpielFingerabdruckTests.Gespielt.Value.Stand.Verlauf.First(e => e.Art == Ereignisse.Gegruendet);
        var spiel = Szenario045(Ereignisse.Gegruendet);
        Assert.False(Py.Wahr(spiel.Stand.Firma));
        Assert.Empty(spiel.Stand.GruendungFehlt());
        var name = Py.Str(original.Daten["name"]);
        Assert.Equal(Schalter.NurAufAmt, Assert.Throws<SpielFehler>(() => spiel.AmtGruenden(stadt, name)).Message);
        Hin(stadt, spiel, Schalter.Bank);
        Assert.Equal(Schalter.NurAufAmt, Assert.Throws<SpielFehler>(() => spiel.AmtGruenden(stadt, name)).Message);
        Hin(stadt, spiel, Schalter.Amt);
        var zahl = spiel.Stand.Verlauf.Count;
        spiel.AmtGruenden(stadt, name);
        var s = spiel.Stand;
        var neu = s.Verlauf.Skip(zahl).ToList();
        Assert.Equal(Ereignisse.Gegruendet, neu[0].Art);
        Assert.All(neu.Skip(1), e => Assert.Equal(Ereignisse.Erfolg, e.Art));
        // genau die Daten der Gruendung aus Python (Tag, Name, Kosten)
        Assert.Equal(PythonJson.Standard(original.Daten), PythonJson.Standard(neu[0].Daten));
        Assert.True(Py.Wahr(s.Firma));
        Assert.Equal("Du hast schon eine Firma gegründet.", Assert.Throws<SpielFehler>(() => spiel.AmtGruenden(stadt, name)).Message);
        Assert.Equal(0, spiel.Abweichungen);
    }

    [Fact]
    public void AmtSchliesstUm16Uhr()
    {
        var stadt = NeueStadt();
        var spiel = Szenario045(Ereignisse.Gegruendet);
        Hin(stadt, spiel, Schalter.Amt);
        // hin und her bis nach 16:00: dann ist das Amt zu und die Gruendung abgelehnt
        while (stadt.Stand(spiel.Stand).Uhr < 960)
        {
            Assert.Equal(StadtGrund.Ok, stadt.Gehen(spiel, stadt.Stand(spiel.Stand).Ort == Schalter.Amt ? Schalter.Bank : Schalter.Amt).Grund);
        }
        if (stadt.Stand(spiel.Stand).Ort != Schalter.Amt)
        {
            Assert.Equal(StadtGrund.Ok, stadt.Gehen(spiel, Schalter.Amt).Grund);
        }
        Assert.NotEqual(StadtGrund.Ok, stadt.Betreten(spiel.Stand));
        var zahl = spiel.Stand.Verlauf.Count;
        Assert.Equal(Schalter.NurAufAmt, Assert.Throws<SpielFehler>(() => spiel.AmtGruenden(stadt, "Zu spaet")).Message);
        Assert.Equal(zahl, spiel.Stand.Verlauf.Count);
    }

    // -- Innenraum ------------------------------------------------------------------

    [Theory]
    [InlineData("bank")]
    [InlineData("amt")]
    public void InnenraumMitSchalterUndPerson(string ort)
    {
        var ohne = new Spielablauf(Inhalt);
        ohne.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        var innen = Innenraum.Bauen("schalter:" + ort, ohne.Stand, SpielInnenraumTests.Innenraeume.Value, SpielGrafikTests.Bibliothek.Value)!;
        Assert.NotNull(innen);
        Assert.Equal(ort, innen.Ort);
        Assert.True(Wege.KannStehen(innen.StartX, innen.StartZ, innen.Lauf), ort + ": Start nicht begehbar");
        var person = Assert.Single(innen.Personen);
        // die Person hinter dem Schalter ist erreichbar, am Ende des Wegs steht man in Reichweite
        var weg = innen.Weg(innen.StartX, innen.StartZ, person.X, person.Z, Wege.Reichweite);
        Assert.NotEmpty(weg);
        Assert.Same(person, innen.PersonNahe(weg[^1].X, weg[^1].Y));
        // ... und zwar vor dem Schalter, nicht dahinter bei der Person
        var tresen = innen.Gebaeude["raeume"] is List<object?> raeume
            ? raeume.Select(SpielDaten.Objekt).SelectMany(r => SpielDaten.ListeOderLeer(r, "deko")).Select(SpielDaten.Objekt).Single(d => Py.Str(d["typ"]) == "tresen")
            : throw new InvalidDataException();
        Assert.True(weg[^1].Y > Py.F(tresen["y"]) + Py.F(tresen["h"]), ort + ": Weg endet hinter dem Schalter");
        Assert.True(person.Z < Py.F(tresen["y"]), ort + ": Person nicht hinter dem Schalter");
        var netz = innen.NetzBauen();
        Assert.True(netz.AnzahlEcken > 500, ort);
        // Text und Knopf: am Eingang Ort und Person, am Schalter die Person
        var (titel, _, knoepfe) = innen.Nachricht(innen.StartX, innen.StartZ, ohne.Stand);
        Assert.Equal(ort == Schalter.Bank ? "Du bist in der Bank." : "Du bist auf dem Amt.", titel);
        var knopf = Assert.Single(knoepfe);
        Assert.Equal(Innenraum.AktionSchalter, knopf.Aktion);
        Assert.Equal(ort == Schalter.Bank ? "Kredite" : "Gründung", knopf.Name);
        var (amSchalter, text, _) = innen.Nachricht(weg[^1].X, weg[^1].Y, ohne.Stand);
        Assert.StartsWith(person.Name + " · ", amSchalter);
        Assert.Equal(Py.Str(person.Daten["text"]), text);
        // mit Firma: auf dem Amt "Steuern"
        if (ort == Schalter.Amt)
        {
            var mit = Szenario045().Stand;
            Assert.Equal("Steuern", Assert.Single(innen.Nachricht(innen.StartX, innen.StartZ, mit).Knoepfe).Name);
        }
    }

    [Fact]
    public void BetretenInDerStadt()
    {
        var spiel = new StadtSpiel(NeueStadt(), new Spielablauf(Inhalt), null, SpielInnenraumTests.Innenraeume.Value);
        spiel.Ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        foreach (var ort in Schalter.Orte)
        {
            Assert.True(spiel.HatInnenraum(ort), ort);
            Assert.Equal(StadtGrund.Ok, spiel.GeheZu(ort));
            spiel.SofortAnkommen();
            if (spiel.KannWarten)
            {
                Assert.Equal(StadtGrund.Ok, spiel.Warten());
            }
            Assert.True(spiel.Betreten(), ort);
            Assert.Equal("schalter:" + ort, spiel.FlaecheId);
            Assert.Contains(spiel.Knoepfe, k => k.Aktion == Innenraum.AktionSchalter);
            spiel.Verlassen();
        }
    }
}
