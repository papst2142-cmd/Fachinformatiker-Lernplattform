using System.Globalization;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 12 Welle 1 (Plan Phase 12, Weisung P12-5): Charakter beim Anlegen, Name und Aussehen nur am Kleiderschrank,
/// vier Kleiderschraenke mit Plaetzen fuer Kleidung (8/16/28/unbegrenzt, der groesste zaehlt, auch im Karton, alter Schrank
/// = 16), ohne Schrank oder bei vollem Schrank kein Kauf, jedes Moebel zum halben Preis zurueckverkaufen. Jeder Test hat
/// eine Gegenprobe (derselbe Ablauf ohne die neue Regel waere rot), die Fingerabdruecke bleiben.
/// </summary>
public class SpielCharakterTests
{
    /// <summary>Die Spielinhalte wie im Programm: Python-Inhalte plus die Moebel aus geschaefte.json moebel_neu.</summary>
    internal static readonly Lazy<PyObjekt> InhaltMitNeuen = new(() =>
    {
        var kopie = (PyObjekt)Py.Kopie(SpielInhaltTests.Inhalt)!;
        return Geschaefte.NeueMoebelEinfuegen(kopie, SpielGeschaefteTests.Daten.Value);
    });

    private static PyObjekt Inhalt => InhaltMitNeuen.Value;

    private static PyObjekt Daten => SpielGeschaefteTests.Daten.Value;

    private static Geschaefte Laeden() => new(Inhalt, Daten);

    private static Stadt NeueStadt() => new(Inhalt, SpielStadtTests.Daten.Value);

    private static readonly PyObjekt Figur = new()
    {
        { "haut", "mittel" }, { "frisur", "locken" }, { "haarfarbe", "braun" }, { "oberteil", "gruen" }, { "extra", "brille" }, { "kreis", "cyan" },
    };

    private static Spielablauf NeuesSpiel(long erspart = 5000, bool profil = true)
    {
        var zaehler = 0;
        var spiel = new Spielablauf(Inhalt, new SpielSpeicher(() =>
        {
            zaehler += 7;
            return new DateTime(2026, 11, 5).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }));
        if (profil)
        {
            spiel.Profil("Nico", Figur);
        }
        if (erspart > 0)
        {
            spiel.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 1L }, { "richtig", false }, { "geld", erspart } });
        }
        return spiel;
    }

    private static string Kaufen(Spielablauf spiel, string moebel)
    {
        var stueck = "m" + Guid.NewGuid().ToString("N")[..10];
        spiel.Ausfuehren(SpielAktionen.MoebelKaufen(spiel.Stand, moebel, stueck));
        return stueck;
    }

    /// <summary>Ein Moebel an die erste freie Stelle der Wohnung stellen (Raster in 0,5er-Schritten).</summary>
    private static void Aufstellen(Spielablauf spiel, string stueck)
    {
        for (var y = 1.0; y < 12; y += 0.5)
        {
            for (var x = 1.0; x < 16; x += 0.5)
            {
                try
                {
                    spiel.Ausfuehren(SpielAktionen.AufstellenBei(spiel.Stand, stueck, x, y, 0L));
                    return;
                }
                catch (SpielFehler)
                {
                    // naechste Stelle
                }
            }
        }
        Assert.Fail("kein Platz fuer " + stueck);
    }

    private static void Kleidung_Loggen(Spielablauf spiel, IEnumerable<string> stuecke)
    {
        foreach (var k in stuecke)
        {
            spiel.Log(Kleidung.Gekauft, new PyObjekt { { "stueck", k }, { "geld", -1L }, { "tag", 1L }, { "laden", Kleidung.Laden } });
        }
    }

    private static void InsKaufhaus(Stadt stadt, Spielablauf spiel)
    {
        Assert.Equal(StadtGrund.Ok, stadt.Gehen(spiel, Kleidung.Laden).Grund);
        if (stadt.Betreten(spiel.Stand) == StadtGrund.Geschlossen)
        {
            Assert.Equal(StadtGrund.Ok, stadt.Warten(spiel).Grund);
        }
        Assert.Equal(StadtGrund.Ok, stadt.Betreten(spiel.Stand));
    }

    // -- Texte woertlich -----------------------------------------------------------

    [Fact]
    public void HinweisUndFensterWoertlich()
    {
        // Nico 30.10.2026 (Weisung P10-2 Abschnitt 3), woertlich, Rechtschreibung nur mit Nico aendern
        Assert.Equal("Hier kannst du einmalig deinen Charakter designen und diesen später im Spiel über einen gekauften Kleiderschrank ändern. "
            + "Sowie in einer anderen Stadt die Frisur beim Friseur.", Charakter.HinweisAnlegen);
        // Wortlaut aus dem freigegebenen Plan (P12-2 Abschnitt 1.7, Nico 5a)
        Assert.Equal("Neu: Kleiderschrank", Charakter.UpdateTitel);
        Assert.Equal("Name und Aussehen deiner Figur änderst du jetzt am Kleiderschrank in deiner Wohnung. Kleiderschränke gibt es im Möbelhaus, "
            + "vom kleinen für 100 € bis zum begehbaren für 1.000 €. Dein Aussehen bleibt, wie es ist.", Charakter.UpdateText);
        Assert.Equal("Dein Kleiderschrank ist voll. Im Möbelhaus gibt es größere Schränke. Was du schon hast, bleibt dir.", Charakter.Voll);
        Assert.Equal("Für neue Kleidung brauchst du zuerst einen Kleiderschrank aus dem Möbelhaus.", Charakter.OhneSchrankKaufen);
        Assert.Equal("Name und Aussehen änderst du am Kleiderschrank in deiner Wohnung (zu kaufen im Möbelhaus).", Charakter.HinweisUebersicht);
        Assert.Equal(["haut", "frisur", "haarfarbe"], Charakter.GesperrteTeile);
    }

    // -- Inhalt und Ladepruefung ------------------------------------------------------

    [Fact]
    public void VierSchraenkeImMoebelhaus()
    {
        var g = Laeden();
        Assert.Empty(g.Pruefen(SpielInnenraumTests.Innenraeume.Value));
        var schraenke = g.Waren("moebelhaus").Where(w => w.Typ == Charakter.SchrankTyp).ToList();
        Assert.Equal(["Kleiderschrank klein:100:8", "Kleiderschrank mittel:400:16", "Kleiderschrank groß:600:28", "Begehbarer Kleiderschrank:1000:"],
            schraenke.Select(w => w.Name + ":" + w.Preis + ":" + Charakter.Plaetze(Wohnung.MoebelStueck(w.Id, Inhalt)!)));
        // der alte Schrank (340 EUR) ist nicht mehr im Laden, zaehlt aber wie der mittlere (Nico 4a)
        Assert.DoesNotContain(g.Waren("moebelhaus"), w => w.Id == Charakter.AlterSchrank);
        Assert.Equal(16, Charakter.Plaetze(Wohnung.MoebelStueck(Charakter.AlterSchrank, Inhalt)!));
        // Gegenprobe: ohne "ohne" stuende er noch im Sortiment, ohne Einfuegen gaebe es die neuen nicht
        var ohneAbschnitt = new Geschaefte(Inhalt, SpielGeschaefteTests.VorPhase12.Value);
        Assert.Contains(ohneAbschnitt.Waren("moebelhaus"), w => w.Id == Charakter.AlterSchrank);
        Assert.DoesNotContain(new Geschaefte(SpielInhaltTests.Inhalt, Daten).Waren("moebelhaus"), w => w.Typ == Charakter.SchrankTyp);
        // in 3D stehen die vier als Auslage, der alte nicht mehr
        var auslage = Geschaefte.Auslage(new PyObjekt { { "gebaeude", Innenraum.LadenGebaeude(SpielInnenraumTests.Innenraeume.Value, "moebelhaus") } })
            .Select(d => Py.Str(d["ware"])).ToList();
        Assert.Equal(schraenke.Select(w => w.Id), auslage.Where(a => a.StartsWith(Charakter.SchrankTyp, StringComparison.Ordinal)));
        // Elektromarkt und Kaufhaus unveraendert
        Assert.Equal(new Geschaefte(Inhalt, SpielGeschaefteTests.VorPhase12.Value).Waren("elektromarkt").Select(w => w.Id), g.Waren("elektromarkt").Select(w => w.Id));
    }

    [Fact]
    public void EinfuegenZweimalAendertNichts()
    {
        var kopie = (PyObjekt)Py.Kopie(SpielInhaltTests.Inhalt)!;
        var vorher = ((List<object?>)SpielDaten.Objekt(kopie["wohnungen"])["moebel"]!).Count;
        Geschaefte.NeueMoebelEinfuegen(kopie, Daten);
        Geschaefte.NeueMoebelEinfuegen(kopie, Daten);
        Assert.Equal(vorher + 4, ((List<object?>)SpielDaten.Objekt(kopie["wohnungen"])["moebel"]!).Count);
        // die Python-Inhalte selbst bleiben unberuehrt (G42)
        Assert.Equal(vorher, ((List<object?>)SpielDaten.Objekt(SpielInhaltTests.Inhalt["wohnungen"])["moebel"]!).Count);
    }

    [Fact]
    public void LadepruefungFindetFehler()
    {
        PyObjekt Kaputt(Action<PyObjekt, List<object?>> aendern)
        {
            var kopie = (PyObjekt)Py.Lesen(PythonJson.Text(Daten))!;
            var neu = SpielDaten.Objekt(kopie[Geschaefte.NeueMoebelAbschnitt]);
            aendern(neu, (List<object?>)neu["moebel"]!);
            return kopie;
        }
        List<string> Fehler(PyObjekt daten) => new Geschaefte(Inhalt, daten).Pruefen(SpielInnenraumTests.Innenraeume.Value);
        Assert.Contains("moebel_neu kleiderschrank_klein: unbekannter Typ schrank",
            Fehler(Kaputt((_, m) => Py.Setzen(SpielDaten.Objekt(m[0]), "typ", "schrank"))));
        Assert.Contains("moebel_neu kleiderschrank_klein: Preis fehlt oder ungueltig",
            Fehler(Kaputt((_, m) => Py.Setzen(SpielDaten.Objekt(m[0]), "preis", 0L))));
        Assert.Contains("moebel_neu kleiderschrank_klein: Plaetze fehlen oder ungueltig",
            Fehler(Kaputt((_, m) => Py.Setzen(SpielDaten.Objekt(m[0]), "plaetze", 0L))));
        Assert.Contains("moebel_neu kleiderschrank_begehbar: Plaetze fehlen oder ungueltig",
            Fehler(Kaputt((_, m) => Py.Setzen(SpielDaten.Objekt(m[3]), "plaetze", "viel"))));
        Assert.Contains("moebel_neu kleiderschrank_klein: Groesse fehlt oder ungueltig",
            Fehler(Kaputt((_, m) => Py.Setzen(SpielDaten.Objekt(m[0]), "w", 0L))));
        Assert.Contains("moebel_neu kleiderschrank_klein: unbekanntes Geschaeft baumarkt",
            Fehler(Kaputt((_, m) => Py.Setzen(SpielDaten.Objekt(m[0]), "geschaeft", "baumarkt"))));
        Assert.Contains("moebel_neu sofa: Kennung doppelt oder schon in wohnungen.json",
            Fehler(Kaputt((_, m) => Py.Setzen(SpielDaten.Objekt(m[0]), "id", "sofa"))));
        Assert.Contains("moebel_neu ohne moebelhaus: nicht im Sortiment sofa_xl",
            Fehler(Kaputt((n, _) => Py.Setzen(n, "ohne", new PyObjekt { { "moebelhaus", new List<object?> { "sofa_xl" } } }))));
        // Plaetze nur bei Kleiderschraenken
        Assert.Contains("moebel_neu kleiderschrank_klein: Plaetze nur bei Kleiderschraenken",
            Fehler(Kaputt((_, m) => Py.Setzen(SpielDaten.Objekt(m[0]), "typ", "kommode"))));
        // Auslage: ohne Schrank in der Ausstellung fehlt er dort
        var ohneAuslage = (PyObjekt)Py.Lesen(PythonJson.Text(SpielInnenraumTests.Innenraeume.Value))!;
        ohneAuslage.RemoveAll(p => p.Key == "zusatz_schraenke");
        var fehler = new Geschaefte(Inhalt, Daten).Pruefen(ohneAuslage);
        Assert.Contains("moebelhaus: Moebel nicht ausgestellt: kleiderschrank_klein", fehler);
        Assert.Contains("moebelhaus: Auslage ohne Ware im Sortiment: kleiderschrank", fehler);
    }

    // -- Plaetze ---------------------------------------------------------------------

    [Fact]
    public void PlaetzeGroessterSchrankZaehltAuchImKarton()
    {
        var spiel = NeuesSpiel();
        Assert.False(Charakter.Plaetze(spiel.Stand).HatSchrank);
        Assert.Equal(Charakter.OhneSchrankKaufen, Charakter.KaufGrund(spiel.Stand));
        var klein = Kaufen(spiel, "kleiderschrank_klein");
        Assert.Equal(8, Charakter.Plaetze(spiel.Stand).Plaetze);
        Assert.Equal("", Charakter.KaufGrund(spiel.Stand));   // im Karton zaehlt er schon
        Kaufen(spiel, "kleiderschrank_gross");
        Assert.Equal(28, Charakter.Plaetze(spiel.Stand).Plaetze);
        Assert.Equal("Kleiderschrank groß", Charakter.Plaetze(spiel.Stand).SchrankName);
        Kaufen(spiel, "kleiderschrank_mittel");
        Assert.Equal(28, Charakter.Plaetze(spiel.Stand).Plaetze);   // nur der groesste zaehlt, nicht die Summe
        Kaufen(spiel, "kleiderschrank_begehbar");
        Assert.Null(Charakter.Plaetze(spiel.Stand).Plaetze);
        Assert.True(Charakter.Plaetze(spiel.Stand).Frei);
        Assert.Equal("Plätze: 0 belegt, unbegrenzt (Begehbarer Kleiderschrank)", Charakter.Belegt(Charakter.Plaetze(spiel.Stand)));
        // alter Schrank allein = 16
        var alt = NeuesSpiel();
        Kaufen(alt, Charakter.AlterSchrank);
        Assert.Equal(16, Charakter.Plaetze(alt.Stand).Plaetze);
        Assert.Equal(8, Charakter.Plaetze(NeuMitSchrank(klein: true).Stand).Plaetze);
    }

    private static Spielablauf NeuMitSchrank(bool klein)
    {
        var spiel = NeuesSpiel();
        Kaufen(spiel, klein ? "kleiderschrank_klein" : "kleiderschrank_mittel");
        return spiel;
    }

    [Fact]
    public void VollerSchrankKauftNichtsNeuesUndNichtsGehtVerloren()
    {
        var g = Laeden();
        var stadt = NeueStadt();
        var spiel = NeuesSpiel();
        InsKaufhaus(stadt, spiel);
        // ohne Schrank: abgelehnt, nichts geschrieben (Gegenprobe: der alte Weg kauft)
        var zeilen = spiel.Stand.Verlauf.Count;
        Assert.Equal(Charakter.OhneSchrankKaufen, Assert.Throws<SpielFehler>(() => spiel.KleidungKaufenMitPlatz(g, stadt, Kleidung.Laden, "hoodie_gruen")).Message);
        Assert.Equal(zeilen, spiel.Stand.Verlauf.Count);
        // Schrank klein (8): acht Stuecke gehen, das neunte nicht
        Kaufen(spiel, "kleiderschrank_klein");
        var sortiment = Kleidung.Sortiment(Daten).Select(k => k.Id).ToList();
        foreach (var id in sortiment.Take(8))
        {
            spiel.KleidungKaufenMitPlatz(g, stadt, Kleidung.Laden, id);
        }
        Assert.Equal(8, spiel.Stand.Kleider.Count);
        zeilen = spiel.Stand.Verlauf.Count;
        var geld = spiel.Stand.Geld;
        Assert.Equal(Charakter.Voll, Assert.Throws<SpielFehler>(() => spiel.KleidungKaufenMitPlatz(g, stadt, Kleidung.Laden, sortiment[8])).Message);
        Assert.Equal(zeilen, spiel.Stand.Verlauf.Count);
        Assert.Equal(geld, spiel.Stand.Geld);
        // schon gekauft: Hinweis auf den Schrank
        Assert.Equal(Charakter.SchonGekauft, Assert.Throws<SpielFehler>(() => spiel.KleidungKaufenMitPlatz(g, stadt, Kleidung.Laden, sortiment[0])).Message);
        // Gegenprobe: der alte Weg ohne Platzpruefung (Kaufhaus-Fingerabdruck) kauft weiter
        spiel.KleidungKaufen(g, stadt, Kleidung.Laden, sortiment[8]);
        Assert.Equal(9, spiel.Stand.Kleider.Count);
        // groesserer Schrank: wieder Platz
        Kaufen(spiel, "kleiderschrank_mittel");
        spiel.KleidungKaufenMitPlatz(g, stadt, Kleidung.Laden, sortiment[9]);
        Assert.Equal(10, spiel.Stand.Kleider.Count);
    }

    [Fact]
    public void MehrKleidungAlsPlaetzeBleibtAnziehbar()
    {
        // ein alter Spielstand mit 12 Stuecken und nur dem kleinen Schrank (8): alles bleibt, alles ist anziehbar
        var spiel = NeuMitSchrank(klein: true);
        var alle = Kleidung.Alle(Daten).Take(12).ToList();
        Kleidung_Loggen(spiel, alle.Select(k => k.Id));
        Assert.Equal(12, spiel.Stand.Kleider.Count);
        Assert.False(Charakter.Plaetze(spiel.Stand).Frei);
        foreach (var k in alle)
        {
            spiel.Ausfuehren(Kleidung.Anziehen(spiel.Stand, Daten, k.Slot, k.Id));
            Assert.Equal(k.Id, Kleidung.Angezogen(spiel.Stand, Daten)[k.Slot].Id);
        }
        Assert.Equal("Plätze: 12 von 8 belegt (Kleiderschrank klein)", Charakter.Belegt(Charakter.Plaetze(spiel.Stand)));
    }

    // -- Name und Aussehen am Kleiderschrank ------------------------------------------------

    [Fact]
    public void AmSchrankNurMitAufgestelltemSchrankUndOhneGesperrteTeile()
    {
        var spiel = NeuesSpiel();
        var neu = (PyObjekt)Py.Kopie(Figur)!;
        Py.Setzen(neu, "oberteil", "pink");
        // ohne Schrank, und auch mit Schrank im Karton: abgelehnt
        Assert.Equal(Charakter.KeinSchrankAufgestellt, Assert.Throws<SpielFehler>(() => spiel.AmSchrank("Nico", neu)).Message);
        var stueck = Kaufen(spiel, "kleiderschrank_klein");
        Assert.Equal(Charakter.KeinSchrankAufgestellt, Assert.Throws<SpielFehler>(() => spiel.AmSchrank("Nico", neu)).Message);
        Aufstellen(spiel, stueck);
        Assert.True(Charakter.SchrankAufgestellt(spiel.Stand));
        // gesperrt: Hautton, Frisur, Haarfarbe (Gegenprobe: SpielAktionen.Profil nimmt sie an)
        foreach (var (teil, wert) in new[] { ("haut", "dunkel"), ("frisur", "kurz"), ("haarfarbe", "blond") })
        {
            var falsch = (PyObjekt)Py.Kopie(neu)!;
            Py.Setzen(falsch, teil, wert);
            var zeilen = spiel.Stand.Verlauf.Count;
            Assert.Equal(Charakter.NurBeimFriseur, Assert.Throws<SpielFehler>(() => spiel.AmSchrank("Nico", falsch)).Message);
            Assert.Equal(zeilen, spiel.Stand.Verlauf.Count);
            Assert.NotEmpty(SpielAktionen.Profil(spiel.Stand, "Nico", falsch, null));
        }
        // erlaubt: Name, Oberteil, Accessoire, Kreis; eine Zeile profil_gesetzt, kein zweiter Schwierigkeitsgrad
        Py.Setzen(neu, "extra", "headset");
        Py.Setzen(neu, "kreis", "pink");
        var vorher = spiel.Stand.Verlauf.Count;
        spiel.AmSchrank("Niko", neu);
        Assert.Equal(vorher + 1, spiel.Stand.Verlauf.Count);
        Assert.Equal(Ereignisse.Profil, spiel.Stand.Verlauf[^1].Art);
        Assert.Equal("Niko", spiel.Stand.Profil!["name"]);
        Assert.Equal("pink", SpielDaten.Objekt(spiel.Stand.Profil["aussehen"])["oberteil"]);
        Assert.Equal("mittel", SpielDaten.Objekt(spiel.Stand.Profil["aussehen"])["haut"]);
        Assert.Equal("Bitte gib deiner Spielfigur einen Namen.", Assert.Throws<SpielFehler>(() => spiel.AmSchrank("  ", neu)).Message);
        // ohne Figur geht es nicht
        Assert.Equal(Charakter.KeinProfil, Assert.Throws<SpielFehler>(() => NeuesSpiel(profil: false).AmSchrank("Nico", neu)).Message);
    }

    [Fact]
    public void KnopfAmSchrankInDerWohnung()
    {
        var spiel = NeuesSpiel();
        var stueck = Kaufen(spiel, "kleiderschrank_mittel");
        Aufstellen(spiel, stueck);
        var schrank = Wohnung.AufgestellteMoebel(spiel.Stand).Single(m => m.Stueck == stueck);
        Assert.NotNull(Charakter.SchrankNahe(spiel.Stand, schrank.X - 0.5, schrank.Y));
        Assert.Null(Charakter.SchrankNahe(spiel.Stand, schrank.X - 2.0, schrank.Y - 2.0));
    }

    // -- Hinweis nach dem Update ---------------------------------------------------------

    [Fact]
    public void UpdateFensterEinmalJeSpielstand()
    {
        var werte = new JsonObject();
        Assert.False(Charakter.UpdateGezeigt(werte, "lauf-a"));
        Charakter.UpdateMerken(werte, "lauf-a");
        Charakter.UpdateMerken(werte, "lauf-a");
        Assert.True(Charakter.UpdateGezeigt(werte, "lauf-a"));
        Assert.False(Charakter.UpdateGezeigt(werte, "lauf-b"));
        Assert.Single((JsonArray)werte[Charakter.UpdateMerker]!);
        // ein kaputter Wert wird eine neue Liste
        werte[Charakter.UpdateMerker] = "kaputt";
        Assert.False(Charakter.UpdateGezeigt(werte, "lauf-a"));
        Charakter.UpdateMerken(werte, "lauf-b");
        Assert.Equal("[\"lauf-b\"]", werte[Charakter.UpdateMerker]!.ToJsonString());
    }

    // -- Zurueckverkaufen ---------------------------------------------------------------

    [Fact]
    public void JedesMoebelZumHalbenPreisZurueck()
    {
        var g = Laeden();
        var moebel = new[] { "moebelhaus", "elektromarkt" }.SelectMany(l => g.Waren(l)).Where(w => w.Art == Geschaefte.ArtMoebel).Select(w => w.Id)
            .Append(Charakter.AlterSchrank).ToList();
        Assert.Equal(27, moebel.Count);   // Moebelhaus 19 + 4 Schraenke, Elektromarkt 3, dazu der alte Schrank
        var spiel = NeuesSpiel(erspart: 20000);
        foreach (var id in moebel)
        {
            var m = Wohnung.MoebelStueck(id, Inhalt)!;
            var stueck = Kaufen(spiel, id);
            var geld = spiel.Stand.Geld;
            var preis = Wohnung.Rueckkauf(m, Inhalt);
            Assert.Equal(Py.Int(m["preis"]) / 2, preis);   // alle heutigen Preise sind ganze Euro, abgerundet = /2
            spiel.MoebelVerkaufen(stueck);
            Assert.Equal(Ereignisse.MoebelVerkauft, spiel.Stand.Verlauf[^1].Art);
            Assert.Equal(geld + preis, spiel.Stand.Geld);
            Assert.False(spiel.Stand.Moebel.ContainsKey(stueck));
        }
        Assert.Equal([50L, 200L, 300L, 500L, 170L], new[] { "kleiderschrank_klein", "kleiderschrank_mittel", "kleiderschrank_gross", "kleiderschrank_begehbar", "kleiderschrank" }
            .Select(id => Wohnung.Rueckkauf(Wohnung.MoebelStueck(id, Inhalt)!, Inhalt)));
        // abgerundet wie Python int(): 35 EUR -> 17 EUR (Zimmerpflanze)
        Assert.Equal(17, Wohnung.Rueckkauf(Wohnung.MoebelStueck("pflanze", Inhalt)!, Inhalt));
        // Weisung P12-7 (Nico 1b): die Startmatratze bringt 0 EUR
        Assert.Equal(0, Wohnung.Rueckkauf(Wohnung.MoebelStueck("matratze", Inhalt)!, Inhalt));
    }

    /// <summary>
    /// Weisung P12-7 (Nico 1b, 3a): die Startmatratze laesst sich beim Einrichten fuer 0 EUR zurueckverkaufen wie Python
    /// sell_furniture (moebel_verkauft mit geld 0, kein Eintrag im Kassenbuch, aus Einrichtung und Moebeln weg); danach
    /// hat man kein Bett, nichts sperrt.
    /// </summary>
    [Fact]
    public void StartmatratzeFuerNullEuro()
    {
        var spiel = NeuesSpiel();
        var geld = spiel.Stand.Geld;
        var buch = spiel.Stand.Kassenbuch.Count;
        Assert.Contains(Wohnung.AufgestellteMoebel(spiel.Stand), m => m.Stueck == "start-matratze");
        var stadt = StadtSpielMit(spiel);
        stadt.EinrichtenUmschalten();
        stadt.Waehlen("start-matratze");
        Assert.Equal(0, stadt.AusgewaehltRueckkauf);
        Assert.True(stadt.Verkaufen());
        var letzte = spiel.Stand.Verlauf[^1];
        Assert.Equal(Ereignisse.MoebelVerkauft, letzte.Art);
        Assert.Equal(0L, letzte.Daten["geld"]);
        Assert.Equal("matratze", letzte.Daten["moebel"]);
        Assert.Equal(geld, spiel.Stand.Geld);
        Assert.Equal(buch, spiel.Stand.Kassenbuch.Count);   // Python _book: Betrag 0 bucht nichts
        Assert.False(spiel.Stand.Moebel.ContainsKey("start-matratze"));
        Assert.DoesNotContain(Wohnung.AufgestellteMoebel(spiel.Stand), m => m.Stueck == "start-matratze");
        Assert.Empty(spiel.Stand.Unterschiede(spiel.Neuaufbau()));
    }

    [Fact]
    public void NachDemVerkaufZaehltDerNaechstgroesste()
    {
        var spiel = NeuesSpiel();
        Kaufen(spiel, "kleiderschrank_klein");
        var mittel = Kaufen(spiel, "kleiderschrank_mittel");
        var gross = Kaufen(spiel, "kleiderschrank_gross");
        Aufstellen(spiel, gross);
        Assert.Equal(28, Charakter.Plaetze(spiel.Stand).Plaetze);
        spiel.MoebelVerkaufen(gross);   // steht er, wird er vorher aus der Einrichtung genommen (wie Python)
        Assert.DoesNotContain(Wohnung.AufgestellteMoebel(spiel.Stand), m => m.Stueck == gross);
        Assert.Equal(16, Charakter.Plaetze(spiel.Stand).Plaetze);
        spiel.MoebelVerkaufen(mittel);
        Assert.Equal(8, Charakter.Plaetze(spiel.Stand).Plaetze);
        // alle weg: kein Schrank, Kleidung bleibt
        Kleidung_Loggen(spiel, ["hoodie_gruen"]);
        spiel.MoebelVerkaufen(spiel.Stand.Moebel.Select(p => Py.Str(p.Key)).Single(s => Py.Gleich(spiel.Stand.Moebel.Get(s), "kleiderschrank_klein")));
        Assert.False(Charakter.Plaetze(spiel.Stand).HatSchrank);
        Assert.Equal(Charakter.OhneSchrankKaufen, Charakter.KaufGrund(spiel.Stand));
        Assert.Single(spiel.Stand.Kleider);
        // Neuaufbau aus den Ereignissen: derselbe Stand
        Assert.Empty(spiel.Stand.Unterschiede(spiel.Neuaufbau()));
    }

    [Fact]
    public void VerkaufenBeimEinrichtenInDerStadt()
    {
        var spiel = NeuesSpiel();
        var stueck = Kaufen(spiel, "kleiderschrank_gross");
        var stadt = StadtSpielMit(spiel);
        Assert.Null(stadt.AusgewaehltRueckkauf);   // nichts gewaehlt
        stadt.EinrichtenUmschalten();
        Assert.True(stadt.Einrichten);
        stadt.Waehlen(stueck);
        Assert.Equal(300, stadt.AusgewaehltRueckkauf);
        var geld = spiel.Stand.Geld;
        Assert.True(stadt.Verkaufen());
        Assert.Equal(geld + 300, stadt.Ablauf.Stand.Geld);
        Assert.Null(stadt.Ausgewaehlt);
        Assert.False(stadt.Verkaufen());
    }

    private static StadtSpiel StadtSpielMit(Spielablauf spiel)
    {
        var stadt = new Stadt(Inhalt, SpielStadtTests.Daten.Value);
        var s = new StadtSpiel(stadt, spiel, null, SpielInnenraumTests.Innenraeume.Value, Daten, null, () => null, () => Jahreszeit.Sommer);
        Assert.True(s.Betreten());
        Assert.Equal("zuhause", s.FlaecheId);
        return s;
    }

    // -- Fingerabdruecke und alte Staende -------------------------------------------------

    /// <summary>
    /// Die vier Fingerabdruecke mit den Inhalten wie im Programm (mit den neuen Moebeln): derselbe Ereignisverlauf ergibt
    /// denselben Spielstand wie mit den Python-Inhalten, G43 f2b59ce8 bleibt.
    /// </summary>
    [Fact]
    public void FingerabdrueckeMitNeuenMoebelnGleich()
    {
        var g43 = SpielFingerabdruckTests.Gespielt.Value.Stand;
        Assert.Equal(SpielFingerabdruckTests.SollHash, SpielFingerabdruckTests.Hash(new Spielstand(g43.Verlauf, Inhalt)));
        foreach (var stand in new[] { SpielStadtTests.StadtSzenario().Spiel.Stand, SpielGeschaefteTests.EinkaufSzenario().Spiel.Stand,
            SpielKaufhausTests.KaufhausSzenario().Spiel.Stand })
        {
            var mit = new Spielstand(stand.Verlauf, Inhalt);
            Assert.Empty(stand.Unterschiede(mit));
            Assert.Equal(SpielFingerabdruckTests.Hash(stand), SpielFingerabdruckTests.Hash(mit));
        }
    }

    /// <summary>
    /// Ein Geraet mit A 0.39.0 kennt die neuen Schraenke nicht (Python-Inhalte ohne moebel_neu): Kauf und Aufstellen eines
    /// neuen Schranks auf dem anderen Geraet bringen den Stand dort nicht zum Absturz; Geld stimmt, der Schrank fehlt in der
    /// Einrichtung (Grenze aus dem Plan, Chat E2).
    /// </summary>
    [Fact]
    public void AltesGeraetKenntNeueSchraenkeNicht()
    {
        var spiel = NeuesSpiel();
        var stueck = Kaufen(spiel, "kleiderschrank_klein");
        Aufstellen(spiel, stueck);
        var alt = new Spielstand(spiel.Stand.Verlauf, SpielInhaltTests.Inhalt);
        Assert.Equal(spiel.Stand.Geld, alt.Geld);
        Assert.Contains(Wohnung.AufgestellteMoebel(spiel.Stand), m => m.Stueck == stueck);   // Gegenprobe: neues Geraet zeigt ihn
        Assert.DoesNotContain(Wohnung.AufgestellteMoebel(alt), m => m.Stueck == stueck);
        Assert.NotNull(Wohnung.ImKarton(alt));   // kein Absturz beim Lesen des Kartons
        Assert.False(Charakter.Plaetze(alt).HatSchrank);
    }

    /// <summary>Alte Staende (vor A 0.40.0) behalten Name, Aussehen und Kleidung (Nico 1a): nichts wird umgeschrieben.</summary>
    [Fact]
    public void AlterStandBehaeltAlles()
    {
        var spiel = NeuesSpiel();
        Kleidung_Loggen(spiel, ["hoodie_gruen", "jeans_blau"]);
        var vorher = PyAusgabe.Wert(spiel.Stand.Felder());
        var neu = spiel.Neuaufbau();
        Assert.Equal(vorher, PyAusgabe.Wert(neu.Felder()));
        Assert.Equal("Nico", neu.Profil!["name"]);
        Assert.Equal(2, neu.Kleider.Count);
    }
}
