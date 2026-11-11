using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Tests;

/// <summary>
/// Welle Q2 (A 0.65.0, Weisung P12-190, Nico 1a 2a 3a 4a): das Spiel liest <c>stadtdaten.quests</c> und
/// <c>stadtdaten.questgeber</c> aus der Fassung. Geprueft: Fassung 0 ohne Abschnitte bleibt dasselbe Objekt (bitgleich);
/// alte Spielstaende (die vier Fingerabdruecke) ergeben mit den Abschnitten genau dieselben Werte; eine neue Quest kommt als
/// Ticket in den heutigen Ablauf (hinten angehaengt) und bringt Geld und Ruf; umgehaengte Quests wandern mit dem Questgeber,
/// ihre Texte bleiben; Questgeber draussen stehen im Paket-Netz und sprechen die Figur an; die Ladepruefung kennt beide
/// Abschnitte (jede Regel rot und gruen, Fehler nie still weg).
/// </summary>
public sealed class SpielQuestTests
{
    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static StadtFassung Fassung0 => StadtbauFassungTests.Fassung0.Value;

    private const string NeuQg = "neu-qg-1";

    private const string NeuQuest = "neu-q-1";

    private const string NeuZuordnung = "neu-q-2";

    private static PyObjekt Aussehen() => new() { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" } };

    private static PyObjekt Draussen(double x, double y, long blick = 180) =>
        new() { { "draussen", true }, { "x", x }, { "y", y }, { "blick", blick } };

    private static PyObjekt Drinnen(string ort, double x, double y, long etage = 0) =>
        new() { { "ort", ort }, { "etage", etage }, { "x", x }, { "y", y }, { "blick", 0L } };

    /// <summary>Die Probe-Abschnitte: Ulla ins Moebelhaus, Petra (beim Kunden) nach draussen, ein eigener Questgeber
    /// draussen vor Talheim, zwei neue Quests (Auswahl, Zuordnung), eine vorhandene Quest umgehaengt.</summary>
    internal static PyObjekt Abschnitte()
    {
        var questgeber = new List<object?>
        {
            new PyObjekt { { "id", "ulla" }, { "bestand", true }, { "platz", Drinnen("moebelhaus", 4, 3) } },
            new PyObjekt { { "id", "petra" }, { "bestand", true }, { "platz", Draussen(13.0, 10.5) } },
            new PyObjekt
            {
                { "id", NeuQg }, { "name", "Greta Sommer" }, { "rolle", "Bäckerei" }, { "aussehen", Aussehen() },
                { "platz", Draussen(19.4, 8.6, 0) },
            },
        };
        var quests = new List<object?>
        {
            new PyObjekt { { "id", "ports-serverraum" }, { "questgeber", "petra" } },
            new PyObjekt
            {
                { "id", NeuQuest }, { "typ", "auswahl" }, { "questgeber", NeuQg }, { "titel", "Kasse startet nicht" },
                { "frage", "Was prüfst du zuerst?" }, { "ticket", new List<object?> { "Greta ruft an: die Kasse startet nicht." } },
                { "hilfe", new List<object?> { "Zuerst das Einfachste prüfen." } }, { "erklaerung", new List<object?> { "Strom zuerst." } },
                { "cat", "systeme" }, { "belohnung", new PyObjekt { { "geld", 50L }, { "reputation", 3L } } }, { "ab_tag", 1L },
                { "optionen", new List<object?> { "Stromkabel und Netzteil", "Neues Betriebssystem" } }, { "antwort", "Stromkabel und Netzteil" },
            },
            new PyObjekt
            {
                { "id", NeuZuordnung }, { "typ", "zuordnung" }, { "questgeber", "ulla" }, { "titel", "Kabel sortieren" },
                { "frage", "Ordne zu." }, { "ticket", new List<object?> { "Ulla braucht Hilfe." } },
                { "hilfe", new List<object?> { "Farben." } }, { "erklaerung", new List<object?> { "So." } },
                { "cat", "netzwerk" }, { "belohnung", new PyObjekt { { "geld", 20L }, { "reputation", 1L } } }, { "ab_tag", 1L },
                { "paare", new List<object?> { new List<object?> { "RJ45", "Netzwerk" }, new List<object?> { "HDMI", "Bild" } } },
                { "nach", NeuQuest },
            },
        };
        return new PyObjekt { { Questebene.AbschnittQuestgeber, questgeber }, { Questebene.AbschnittQuests, quests } };
    }

    /// <summary>Stadtdaten der Fassung 0 mit Abschnitten (Kopie, Fassung 0 bleibt unberuehrt).</summary>
    internal static PyObjekt Stadtdaten(PyObjekt? abschnitte = null)
    {
        var s = (PyObjekt)Py.Lesen(PythonJson.Text(Fassung0.Stadtdaten))!;
        foreach (var (k, v) in abschnitte ?? Abschnitte())
        {
            Py.Setzen(s, k, v);
        }
        return s;
    }

    private static PyObjekt MitKarte => Fassung0.MitKarte(Inhalt);

    private static List<string> Funde(PyObjekt abschnitte) => Questebene.Pruefen(MitKarte, Stadtdaten(abschnitte), Fassung0.Innenraeume);

    internal static readonly Lazy<PyObjekt> Angewendet = new(() => Questebene.Anwenden(MitKarte, Stadtdaten(), Fassung0.Innenraeume));

    private static PyObjekt Aufgabe(PyObjekt inhalt, string id) =>
        SpielDaten.Liste(inhalt["aufgaben"]).Select(SpielDaten.Objekt).Single(a => Py.Gleich(a["id"], id));

    private static PyObjekt Person(PyObjekt inhalt, string id) =>
        SpielDaten.Liste(inhalt["kollegen"]).Select(SpielDaten.Objekt).Single(a => Py.Gleich(a["id"], id));

    // ---- bitgleich ohne Abschnitte und alte Spielstaende ------------------------------------------------------------

    [Fact]
    public void Fassung0_OhneAbschnitte_DasselbeObjekt()
    {
        var inhalt = MitKarte;
        Assert.False(Questebene.Hat(Fassung0.Stadtdaten));
        Assert.Same(inhalt, Questebene.Anwenden(inhalt, Fassung0.Stadtdaten, Fassung0.Innenraeume));
        Assert.Same(inhalt, Fassung0.StadtFuer(inhalt).Inhalt);
        Assert.Empty(Questebene.Pruefen(inhalt, Fassung0.Stadtdaten, Fassung0.Innenraeume));
        Assert.Empty(StadtSzene.Bauen(Fassung0.StadtFuer(inhalt), null, null, null, FestDekoTests.Daten.Value, Jahreszeit.Sommer).Questgeber);
        // Gegenprobe: mit Abschnitten ist es eine neue Kopie, das Original bleibt unveraendert
        var vorher = PythonJson.Text(inhalt);
        Assert.NotSame(inhalt, Angewendet.Value);
        Assert.Equal(vorher, PythonJson.Text(inhalt));
    }

    private static void GleicheWerte(Spielstand alt, PyObjekt inhalt)
    {
        var neu = new Spielstand(alt.Verlauf, inhalt);
        Assert.Equal(SpielFingerabdruckTests.Hash(alt), SpielFingerabdruckTests.Hash(neu));
        Assert.Equal(PythonJson.Text(SpielFingerabdruckTests.Werte(alt)), PythonJson.Text(SpielFingerabdruckTests.Werte(neu)));
        Assert.Equal(alt.Geloest.Count, neu.Geloest.Count);
    }

    [Fact]
    public void AlteSpielstaende_MitAbschnitten_GleicheWerte_VierFingerabdruecke()
    {
        var inhalt = Angewendet.Value;
        // G43 f2b59ce8
        Assert.Equal(SpielFingerabdruckTests.SollHash, SpielFingerabdruckTests.Hash(new Spielstand(SpielFingerabdruckTests.Gespielt.Value.Stand.Verlauf, inhalt)));
        GleicheWerte(SpielFingerabdruckTests.Gespielt.Value.Stand, inhalt);
        // Stadt 03d0a012: Werte und Stadt-Uhr gleich
        var (stadtSpiel, _) = SpielStadtTests.StadtSzenario();
        GleicheWerte(stadtSpiel.Stand, inhalt);
        var uhrAlt = new Stadt(Inhalt, SpielStadtTests.Daten.Value).Stand(stadtSpiel.Stand);
        var uhrNeu = new Stadt(inhalt, SpielStadtTests.Daten.Value).Stand(new Spielstand(stadtSpiel.Stand.Verlauf, inhalt));
        Assert.Equal((uhrAlt.Tag, uhrAlt.Ort, uhrAlt.Uhr), (uhrNeu.Tag, uhrNeu.Ort, uhrNeu.Uhr));
        // Einkauf 185dee1b und Kaufhaus a3ec3cdf (Inhalt mit den Moebeln aus Phase 12)
        var mitNeuen = Questebene.Anwenden(Fassung0.MitKarte(SpielCharakterTests.InhaltMitNeuen.Value), Stadtdaten(), Fassung0.Innenraeume);
        GleicheWerte(SpielGeschaefteTests.EinkaufSzenario().Spiel.Stand, mitNeuen);
        GleicheWerte(SpielKaufhausTests.KaufhausSzenario().Spiel.Stand, mitNeuen);
        // Die Szenarien selbst (ohne Abschnitte) bleiben bei ihren Sollwerten
        Assert.Equal(SpielStadtTests.SollStadtHash, SpielStadtTests.StadtHash(stadtSpiel, SpielStadtTests.StadtSzenario().Stadtstaende));
    }

    [Fact]
    public void Personen_AnDerselbenStelle_UmgehaengteQuestTexteBleiben()
    {
        var inhalt = Angewendet.Value;
        var alt = SpielDaten.Liste(Inhalt["kollegen"]).Select(p => Py.Str(SpielDaten.Objekt(p)["id"])).ToList();
        var neu = SpielDaten.Liste(inhalt["kollegen"]).Select(p => Py.Str(SpielDaten.Objekt(p)["id"])).ToList();
        // Ulla bleibt an ihrer Stelle, Petra (vorher beim Kunden) und der eigene Questgeber kommen hinten dazu
        Assert.Equal([.. alt, "petra", NeuQg], neu);
        Assert.DoesNotContain(SpielDaten.Liste(SpielDaten.Objekt(SpielDaten.Liste(SpielDaten.Objekt(inhalt["kunden"])["orte"])[0])["personen"]),
            p => Py.Gleich(SpielDaten.Objekt(p)["id"], "petra"));
        var ulla = Person(inhalt, "ulla");
        Assert.Equal("moebelhaus", ulla["ort"]);
        Assert.Null(ulla["raum"]);
        Assert.True(Questebene.IstQuestgeber(ulla));
        // umgehaengt (1a): nur der Questgeber und der Ort aendern sich, Texte und Belohnung bleiben
        var vorher = Aufgabe(Inhalt, "ports-serverraum");
        var nachher = Aufgabe(inhalt, "ports-serverraum");
        Assert.Equal("petra", nachher["auftraggeber"]);
        Assert.Equal(Questebene.OrtDraussen, nachher["ort"]);
        foreach (var feld in (string[])["titel", "ticket", "frage", "paare", "hilfe", "erklaerung", "belohnung", "ab_tag", "typ"])
        {
            Assert.Equal(PythonJson.Text(vorher[feld]), PythonJson.Text(nachher[feld]));
        }
        // Ullas andere Quests wandern mit ins Moebelhaus
        Assert.Equal("moebelhaus", SpielDaten.AuftragOrt(Aufgabe(inhalt, "subnetz-servicecenter"), inhalt));
        // die Reihenfolge der Auftraege bleibt, die neuen haengen hinten (3a)
        var ids = SpielDaten.Liste(inhalt["aufgaben"]).Select(a => Py.Str(SpielDaten.Objekt(a)["id"])).ToList();
        var altIds = SpielDaten.Liste(Inhalt["aufgaben"]).Select(a => Py.Str(SpielDaten.Objekt(a)["id"])).ToList();
        Assert.Equal([.. altIds, NeuQuest, NeuZuordnung], ids);
    }

    [Fact]
    public void QuestMitEigenemOrt_BleibtDort_WennNurDerQuestgeberUmzieht()
    {
        // wie die Spielgeschichte des Werkzeugs: Petras Quests beim Kunden (mit eigenem Ort) bleiben dort
        var inhalt = Angewendet.Value;
        foreach (var a in SpielDaten.Liste(Inhalt["aufgaben"]).Select(SpielDaten.Objekt)
            .Where(a => Py.Gleich(a["auftraggeber"], "petra") && Py.Hat(a, "ort") && !Py.Gleich(a["id"], "ports-serverraum")))
        {
            Assert.Equal(SpielDaten.AuftragOrt(a, Inhalt), SpielDaten.AuftragOrt(Aufgabe(inhalt, Py.Str(a["id"])), inhalt));
        }
    }

    // ---- neue Quest im Ablauf ---------------------------------------------------------------------------------------

    /// <summary>Spiel am Tag 2 mit allen bisherigen Auftraegen erledigt: heute kommen nur noch die neuen.</summary>
    internal static Spielablauf AllesErledigt(PyObjekt inhalt)
    {
        var zaehler = 0;
        var spiel = new Spielablauf(inhalt, new SpielSpeicher(() =>
        {
            zaehler += 7;
            return new DateTime(2026, 10, 1).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }));
        spiel.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        foreach (var a in SpielDaten.Liste(Inhalt["aufgaben"]).Select(SpielDaten.Objekt))
        {
            spiel.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", a["id"] }, { "tag", 1L }, { "richtig", true }, { "geld", 0L } });
        }
        spiel.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", 1L }, { "gehalt", 0L }, { "rang", "Praktikant" } });
        spiel.Neuaufbau();
        return spiel;
    }

    private static double Ruf(Spielstand s) => SpielDaten.Objekt(s.Reputation).Sum(p => Py.F(p.Value));

    [Fact]
    public void NeueQuest_KommtAlsTicket_UndBringtGeldUndRuf()
    {
        var inhalt = Angewendet.Value;
        var spiel = AllesErledigt(inhalt);
        Assert.Contains(spiel.Stand.OffeneTickets(), a => Py.Gleich(a["id"], NeuQuest));
        // "erst nach": die Zuordnung kommt erst nach der Auswahl
        Assert.DoesNotContain(spiel.Stand.OffeneTickets(), a => Py.Gleich(a["id"], NeuZuordnung));
        var auftraege = spiel.Stand.Auftraege(Questebene.OrtDraussen);
        Assert.True(auftraege.Get(NeuQg) is List<object?> { Count: 1 });
        var geld = spiel.Stand.Geld;
        var ruf = Ruf(spiel.Stand);
        var auftrag = Aufgabe(inhalt, NeuQuest);
        spiel.Loesen(NeuQuest, Bewertung.Loesung(auftrag, null, inhalt), false);
        Assert.Contains(NeuQuest, spiel.Stand.Geloest);
        var ereignis = spiel.Stand.Verlauf.Last(e => e.Art == Ereignisse.Erledigt);
        Assert.True(Py.F(ereignis.Daten["geld"]) >= 50, "Geld: " + PythonJson.Text(ereignis.Daten));
        Assert.Equal(Py.F(geld) + Py.F(ereignis.Daten["geld"]), Py.F(spiel.Stand.Geld));
        Assert.True(Ruf(spiel.Stand) > ruf, "Ruf");
        Assert.Contains(spiel.Stand.OffeneTickets(), a => Py.Gleich(a["id"], NeuZuordnung));
        Assert.Equal("im Möbelhaus", Questebene.Wo(SpielDaten.AuftragOrt(Aufgabe(inhalt, NeuZuordnung), inhalt)));
        // Gegenprobe: ohne die Abschnitte gibt es kein solches Ticket
        Assert.DoesNotContain(AllesErledigt(MitKarte).Stand.OffeneTickets(), a => Py.Gleich(a["id"], NeuQuest));
    }

    // ---- Darstellung: draussen und im Laden -------------------------------------------------------------------------

    [Fact]
    public void QuestgeberDraussen_ImNetzDesHausesIhresOrts_GegenprobeImPaket()
    {
        var stadt = Fassung0.StadtFuer(MitKarte);
        var stadtQ = new Stadt(Angewendet.Value, Stadtdaten()) { Fassung = Fassung0 };
        var grafik = SpielGrafikTests.Bibliothek.Value;
        StadtSzene Bauen(Stadt s) => StadtSzene.Bauen(s, null, grafik, null, FestDekoTests.Daten.Value, Jahreszeit.Sommer);
        var ohne = Bauen(stadt);
        var mit = Bauen(stadtQ);
        Assert.Equal(["petra", NeuQg], mit.Questgeber.Select(q => q.Id));
        Assert.Equal(13.0, mit.Questgeber[0].X);
        Assert.Equal(10.5, mit.Questgeber[0].Z);
        long D(Netz? n) => (n?.Indizes.Count ?? 0) / 3;
        long Summe(IReadOnlyDictionary<StadtHaus, Netz>? h) => h?.Values.Sum(D) ?? 0;
        var figur = D(StadtSzene.QuestgeberNetz(grafik, Aussehen(), fern: false));
        var kasten = D(StadtSzene.QuestgeberNetz(grafik, Aussehen(), fern: true));
        Assert.True(figur > kasten && kasten > 0);
        var a = ohne.Vorbereiten();
        var b = mit.Vorbereiten();
        // jeder Questgeber steht im Haus seines Orts (Sichtkegel und Fernform des Hauses, Welle D1)
        foreach (var q in mit.Questgeber)
        {
            var haus = mit.Haeuser.Single(h => h.Id == mit.QuestgeberOrt(q));
            var hausOhne = ohne.Haeuser.Single(h => h.Id == haus.Id);
            Assert.Contains(q, mit.QuestgeberJeHaus[haus.Id]);
            var zahl = mit.QuestgeberJeHaus[haus.Id].Count;
            Assert.Equal(D(a.Haeuser[hausOhne]) + (zahl * figur), D(b.Haeuser[haus]));
            Assert.Equal(D(a.HaeuserFern![hausOhne]) + (zahl * kasten), D(b.HaeuserFern![haus]));
        }
        Assert.Equal(Summe(a.Haeuser) + (2 * figur), Summe(b.Haeuser));
        // kein neuer Zeichenaufruf: Stadt und Paket-Netz unveraendert
        Assert.Equal(D(a.Stadt), D(b.Stadt));
        Assert.Equal(D(a.PaketNah), D(b.PaketNah));
        Assert.Equal(D(a.PaketFern), D(b.PaketFern));
        // Gegenprobe (Stand vor der Regel je Haus): alle im Paket-Netz, die Haeuser ohne Questgeber
        StadtSzene.QuestgeberImPaketFuerTests = true;
        try
        {
            var alt = Bauen(stadtQ).Vorbereiten();
            Assert.Equal(D(a.PaketNah) + (2 * figur), D(alt.PaketNah));
            Assert.Equal(Summe(a.Haeuser), Summe(alt.Haeuser));
        }
        finally
        {
            StadtSzene.QuestgeberImPaketFuerTests = false;
        }
    }

    [Fact]
    public void QuestgeberDraussen_Teilbau_NeuerPlatzBautNurDasHaus()
    {
        var grafik = SpielGrafikTests.Bibliothek.Value;
        StadtSzene Bauen(PyObjekt abschnitte)
        {
            var daten = Stadtdaten(abschnitte);
            var inhalt = Questebene.Anwenden(MitKarte, daten, Fassung0.Innenraeume);
            return StadtSzene.Bauen(new Stadt(inhalt, daten) { Fassung = Fassung0 }, null, grafik, null, FestDekoTests.Daten.Value, Jahreszeit.Sommer);
        }
        var erste = Bauen(Abschnitte());
        var greta = erste.Questgeber.Single(q => q.Id == NeuQg);
        var ort = erste.QuestgeberOrt(greta)!;
        // Greta einen halben Schritt weiter: nur das Haus ihres Orts aendert sich
        var geaendert = Abschnitte();
        Py.Setzen(SpielDaten.Objekt(Qg(geaendert, 2)["platz"]), "x", 19.9);
        var zweite = Bauen(geaendert);
        Assert.Equal(ort, zweite.QuestgeberOrt(zweite.Questgeber.Single(q => q.Id == NeuQg)));
        var vorher = erste.Vorbereiten();
        var nachher = zweite.Vorbereiten();
        foreach (var h in zweite.Haeuser)
        {
            var alt = vorher.Haeuser[erste.Haeuser.Single(x => x.Id == h.Id)];
            var neu = nachher.Haeuser[h];
            if (h.Id == ort)
            {
                Assert.False(alt.Ecken.SequenceEqual(neu.Ecken));
            }
            else
            {
                Assert.True(alt.Ecken.SequenceEqual(neu.Ecken), h.Id);
            }
        }
    }

    [Fact]
    public void QuestgeberDraussen_SprichtDieFigurAn_AmOrtUndNahe()
    {
        var inhalt = Angewendet.Value;
        var stadt = new Stadt(inhalt, Stadtdaten()) { Fassung = Fassung0 };
        var ablauf = AllesErledigt(inhalt);
        var spiel = new StadtSpiel(stadt, ablauf, null, SpielInnenraumTests.Innenraeume.Value);
        Assert.Equal("talheim", spiel.QuestgeberOrt(spiel.QuestgeberDraussen.Single(q => q.Id == NeuQg)));
        // ueber die Ortsliste (auch ohne 3D): vor Talheim steht der Knopf des Questgebers
        Assert.Equal(StadtGrund.Ok, spiel.GeheZu("talheim"));
        spiel.SofortAnkommen();
        Assert.Contains(spiel.Knoepfe, k => k.Aktion == "auftrag:" + NeuQuest);
        Assert.Contains("Greta", spiel.Zeile);
        // Gegenprobe: an einem anderen Ort nicht
        Assert.Equal(StadtGrund.Ok, spiel.GeheZu("bitweiche"));
        spiel.SofortAnkommen();
        Assert.DoesNotContain(spiel.Knoepfe, k => k.Aktion == "auftrag:" + NeuQuest);
        // im Freien nahe heran
        var q = spiel.QuestgeberDraussen.Single(x => x.Id == NeuQg);
        Assert.True(spiel.GeheZu(q.X, q.Z));
        spiel.SofortAnkommen();
        Assert.NotNull(spiel.QuestgeberNahe(spiel.FigurX, spiel.FigurZ));
        Assert.Contains(spiel.Knoepfe, k => k.Aktion == "auftrag:" + NeuQuest);
    }

    [Fact]
    public void QuestgeberImLaden_StehtMitAusrufezeichen()
    {
        var inhalt = Angewendet.Value;
        var spiel = AllesErledigt(inhalt);
        spiel.Loesen(NeuQuest, Bewertung.Loesung(Aufgabe(inhalt, NeuQuest), null, inhalt), false);
        var personen = SpielOrte.Personen("moebelhaus", spiel.Stand);
        Assert.Contains(personen, p => Py.Gleich(p["id"], "ulla"));
        Assert.True(spiel.Stand.Auftraege("moebelhaus").Get("ulla") is List<object?> { Count: > 0 });
        // Gegenprobe: ohne Abschnitte steht Ulla nicht im Moebelhaus
        Assert.DoesNotContain(SpielOrte.Personen("moebelhaus", AllesErledigt(MitKarte).Stand), p => Py.Gleich(p["id"], "ulla"));
    }

    // ---- Ladepruefung -----------------------------------------------------------------------------------------------

    [Fact]
    public void Ladepruefung_Gruen_UndRotWirftImmer()
    {
        Assert.Empty(Funde(Abschnitte()));
        var falsch = Abschnitte();
        SpielDaten.Objekt(SpielDaten.Liste(falsch[Questebene.AbschnittQuests])[1]).RemoveAll(p => p.Key == "frage");
        Assert.NotEmpty(Funde(falsch));
        // nie still weg: das Spiel laedt so nicht
        Assert.Throws<StadtbauFehler>(() => Questebene.Anwenden(MitKarte, Stadtdaten(falsch), Fassung0.Innenraeume));
        // das Werkzeug zeigt nachsichtig (ohne die fehlerhafte Quest)
        var nachsichtig = Questebene.Anwenden(MitKarte, Stadtdaten(falsch), Fassung0.Innenraeume, nachsichtig: true);
        Assert.DoesNotContain(SpielDaten.Liste(nachsichtig["aufgaben"]), a => Py.Gleich(SpielDaten.Objekt(a)["id"], NeuQuest));
    }

    private static PyObjekt Qg(PyObjekt abschnitte, int i) => SpielDaten.Objekt(SpielDaten.Liste(abschnitte[Questebene.AbschnittQuestgeber])[i]);

    private static PyObjekt Q(PyObjekt abschnitte, int i) => SpielDaten.Objekt(SpielDaten.Liste(abschnitte[Questebene.AbschnittQuests])[i]);

    public static TheoryData<string> Regeln =>
    [
        "unbekannte Person", "Kennung schon da", "Name zu lang", "Aussehen unbekannt", "draussen nicht auf der Karte",
        "selbst gebaut", "Innenraum unbekannt", "Etage fehlt", "ausserhalb des Raums", "Blick", "mehr als 12 draussen",
        "Questgeber doppelt", "umhaengen mit Text", "umhaengen unbekannter Questgeber", "Kennung eines Auftrags", "Typ terminal",
        "ohne Questgeber", "Ticket leer", "Hilfe leerer Text", "Thema", "Belohnung", "ab Tag", "Antwort fehlt", "Paare gleich", "erst nach unbekannt", "Kreis",
    ];

    [Theory]
    [MemberData(nameof(Regeln))]
    public void Ladepruefung_JedeRegelRot(string regel)
    {
        var a = Abschnitte();
        var qgListe = SpielDaten.Liste(a[Questebene.AbschnittQuestgeber]);
        switch (regel)
        {
            case "unbekannte Person": Py.Setzen(Qg(a, 0), "id", "niemand"); break;
            case "Kennung schon da": Py.Setzen(Qg(a, 2), "id", "sabine"); break;
            case "Name zu lang": Py.Setzen(Qg(a, 2), "name", new string('x', Questebene.NameHoechstens + 1)); break;
            case "Aussehen unbekannt": Py.Setzen(SpielDaten.Objekt(Qg(a, 2)["aussehen"]), "oberteil", "lila-gepunktet"); break;
            case "draussen nicht auf der Karte": Py.Setzen(SpielDaten.Objekt(Qg(a, 1)["platz"]), "x", 999.0); break;
            case "selbst gebaut": Py.Setzen(Qg(a, 0), "platz", Drinnen("bau:haus1", 1, 1)); break;
            case "Innenraum unbekannt": Py.Setzen(Qg(a, 0), "platz", Drinnen("gibtsnicht", 1, 1)); break;
            case "Etage fehlt": Py.Setzen(Qg(a, 0), "platz", Drinnen("moebelhaus", 1, 1, 3)); break;
            case "ausserhalb des Raums": Py.Setzen(Qg(a, 0), "platz", Drinnen("moebelhaus", 99, 1)); break;
            case "Blick": Py.Setzen(SpielDaten.Objekt(Qg(a, 1)["platz"]), "blick", 45L); break;
            case "mehr als 12 draussen":
                for (var i = 0; i < Questebene.DraussenHoechstens; i++)
                {
                    qgListe.Add(new PyObjekt
                    {
                        { "id", "neu-qg-x" + i }, { "name", "Person " + i }, { "aussehen", Aussehen() }, { "platz", Draussen(2 + i, 2) },
                    });
                }
                break;
            case "Questgeber doppelt": qgListe.Add(Py.Kopie(Qg(a, 2))); break;
            case "umhaengen mit Text": Py.Setzen(Q(a, 0), "titel", "Neu"); break;
            case "umhaengen unbekannter Questgeber": Py.Setzen(Q(a, 0), "questgeber", "niemand"); break;
            case "Kennung eines Auftrags": Py.Setzen(Q(a, 1), "id", "zf-drucker-zeichensalat"); Py.Setzen(Q(a, 2), "nach", null); break;
            case "Typ terminal": Py.Setzen(Q(a, 1), "typ", "terminal"); break;
            case "ohne Questgeber": Q(a, 1).RemoveAll(p => p.Key == "questgeber"); break;
            case "Ticket leer": Py.Setzen(Q(a, 1), "ticket", new List<object?> { " " }); break;
            case "Hilfe leerer Text": Py.Setzen(Q(a, 1), "hilfe", " \n "); break;
            case "Thema": Py.Setzen(Q(a, 1), "cat", "kochen"); break;
            case "Belohnung": Py.Setzen(SpielDaten.Objekt(Q(a, 1)["belohnung"]), "geld", Questebene.GeldHoechstens + 1); break;
            case "ab Tag": Py.Setzen(Q(a, 1), "ab_tag", 0L); break;
            case "Antwort fehlt": Py.Setzen(Q(a, 1), "antwort", "Etwas anderes"); break;
            case "Paare gleich": Py.Setzen(Q(a, 2), "paare", new List<object?> { new List<object?> { "A", "B" }, new List<object?> { "A", "C" } }); break;
            case "erst nach unbekannt": Py.Setzen(Q(a, 2), "nach", "gibtsnicht"); break;
            case "Kreis": Py.Setzen(Q(a, 1), "nach", NeuZuordnung); break;
            default: throw new InvalidOperationException(regel);
        }
        Assert.NotEmpty(Funde(a));
        Assert.Throws<StadtbauFehler>(() => Questebene.Anwenden(MitKarte, Stadtdaten(a), Fassung0.Innenraeume));
    }

    [Fact]
    public void Ladepruefung_ZwoelfDraussenGruen_KaputterAbschnittRot()
    {
        // Grenze: genau 12 draussen ist erlaubt (Gegenprobe zu "mehr als 12 draussen")
        var a = Abschnitte();
        var liste = SpielDaten.Liste(a[Questebene.AbschnittQuestgeber]);
        for (var i = 0; i < Questebene.DraussenHoechstens - 2; i++)
        {
            liste.Add(new PyObjekt { { "id", "neu-qg-x" + i }, { "name", "Person " + i }, { "aussehen", Aussehen() }, { "platz", Draussen(2 + i, 2) } });
        }
        Assert.Empty(Funde(a));
        // Texte als ein Text mit Zeilenumbruechen (Kopie einer geladenen Quest im Werkzeug) sind erlaubt
        Py.Setzen(Q(a, 1), "ticket", "Zeile 1\nZeile 2");
        Assert.Empty(Funde(a));
        Assert.Equal("Zeile 1\nZeile 2", Aufgabe(Questebene.Anwenden(MitKarte, Stadtdaten(a), Fassung0.Innenraeume), NeuQuest)["ticket"]);
        Assert.NotEmpty(Funde(new PyObjekt { { Questebene.AbschnittQuests, "kaputt" } }));
        Assert.NotEmpty(Funde(new PyObjekt { { Questebene.AbschnittQuestgeber, new List<object?> { new PyObjekt { { "name", "ohne Kennung" } } } } }));
    }

    [Fact]
    public void StadtbauPruefung_Inhalt_NenntDieQuestFunde()
    {
        var falsch = Abschnitte();
        Py.Setzen(Q(falsch, 1), "typ", "terminal");
        var o = (PyObjekt)Py.Lesen(Fassung0.Schreiben())!;
        o.RemoveAll(p => p.Key == "pruefsumme");
        Py.Setzen(o, "stadtdaten", Stadtdaten(falsch));
        o.Add("pruefsumme", StadtFassung.Pruefsumme(o));
        var f = StadtFassung.Lesen(PythonJson.Utf8(PythonJson.Text(o)));
        Assert.Contains(StadtbauPruefung.Inhalt(f, Inhalt), t => t.Contains("Auswahl oder Zuordnung", StringComparison.Ordinal));
        Assert.Throws<StadtbauFehler>(() => f.StadtFuer(f.MitKarte(Inhalt)));
    }
}
