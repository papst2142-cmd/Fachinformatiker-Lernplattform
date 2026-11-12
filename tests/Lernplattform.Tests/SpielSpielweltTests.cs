using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 12 Welle 2 (Plan Phase 12 Abschnitte 2 bis 5, Weisungen P12-3/P12-4/P12-5/P12-8): PC zu Hause mit Online-Shop,
/// Lieferzeit nach Groesse, Paket vor der Haustuer (paket_abgeholt), Menues in der Spielwelt (Knoepfe an PC, Regal,
/// Wohnungstuer und Buero-Schreibtisch, Ortsliste ohne 3D), Firmen-Reiter ohne „Kredite“ (feste Ausnahme von Python),
/// Kartonstapel vor der Tuer und das Modell des begehbaren Kleiderschranks. Jeder Test hat eine Gegenprobe (ohne die neue
/// Regel waere er rot); die vier Fingerabdruecke bleiben (SpielFingerabdruckTests, SpielCharakterTests).
/// </summary>
public class SpielSpielweltTests
{
    private static PyObjekt Inhalt => SpielCharakterTests.InhaltMitNeuen.Value;

    private static PyObjekt Daten => SpielGeschaefteTests.Daten.Value;

    private static Geschaefte Laeden() => new(Inhalt, Daten);

    private static readonly PyObjekt Figur = new()
    {
        { "haut", "mittel" }, { "frisur", "locken" }, { "haarfarbe", "braun" }, { "oberteil", "gruen" }, { "extra", "brille" }, { "kreis", "cyan" },
    };

    private static Spielablauf NeuesSpiel(long erspart = 8000)
    {
        var zaehler = 0;
        var spiel = new Spielablauf(Inhalt, new SpielSpeicher(() =>
        {
            zaehler += 7;
            return new DateTime(2026, 11, 6).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }));
        spiel.Profil("Nico", Figur);
        spiel.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 1L }, { "richtig", false }, { "geld", erspart } });
        return spiel;
    }

    private static string Kaufen(Spielablauf spiel, string moebel)
    {
        var stueck = "m" + Guid.NewGuid().ToString("N")[..10];
        spiel.Ausfuehren(SpielAktionen.MoebelKaufen(spiel.Stand, moebel, stueck));
        return stueck;
    }

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

    private static Spielablauf MitPc()
    {
        var spiel = NeuesSpiel();
        Aufstellen(spiel, Kaufen(spiel, Spielwelt.PcTyp));
        return spiel;
    }

    /// <summary>Feierabend bis der Arbeitstag <paramref name="tag"/> erreicht ist (Ereignis tag_beendet wie Python).</summary>
    private static void BisTag(Spielablauf spiel, long tag)
    {
        while (spiel.Stand.Tag < tag)
        {
            spiel.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", spiel.Stand.Tag }, { "gehalt", 0L } });
        }
    }

    // -- Lieferzeit -------------------------------------------------------------------

    /// <summary>Nico 1a: Lieferstufe je Moebel des Online-Shops wie die Tabelle im Plan (Abschnitt 2), Regel nur nach w × h.</summary>
    [Fact]
    public void LiefertageJeMoebelWieImPlan()
    {
        var soll = new Dictionary<string, long>
        {
            ["pflanze"] = 1, ["stehlampe"] = 1, ["grill"] = 1, ["bild"] = 1, ["spielkonsole"] = 1, ["waschmaschine"] = 1,
            ["sessel"] = 2, ["sitzsack"] = 2, ["couchtisch"] = 2, ["esstisch"] = 2, ["schreibtisch"] = 2, ["regal"] = 2, ["kommode"] = 2,
            ["aquarium"] = 2, ["liegestuhl"] = 2, ["sonnenschirm"] = 2, ["hochbeet"] = 2, ["fernseher"] = 2,
            ["kleiderschrank_klein"] = 2, ["kleiderschrank_mittel"] = 2,
            ["bett_einzel"] = 3, ["bett_doppel"] = 3, ["sofa"] = 3, ["teppich"] = 3, ["kleiderschrank_gross"] = 3, ["kleiderschrank_begehbar"] = 3,
        };
        var ist = Spielwelt.Sortiment(Laeden()).ToDictionary(p => p.Ware.Id, p => Spielwelt.Liefertage(Wohnung.MoebelStueck(p.Ware.Id, Inhalt)!));
        Assert.Equal(soll.OrderBy(p => p.Key, StringComparer.Ordinal), ist.OrderBy(p => p.Key, StringComparer.Ordinal));
        // Grenzen: genau 1 m² und genau 3 m² zaehlen zur kleineren Stufe (Gegenprobe: 1,01 m² ist schon 2 Tage)
        Assert.Equal(1, Spielwelt.Liefertage(new PyObjekt { { "w", 1.0 }, { "h", 1.0 } }));
        Assert.Equal(2, Spielwelt.Liefertage(new PyObjekt { { "w", 1.01 }, { "h", 1.0 } }));
        Assert.Equal(2, Spielwelt.Liefertage(new PyObjekt { { "w", 3.0 }, { "h", 1.0 } }));
        Assert.Equal(3, Spielwelt.Liefertage(new PyObjekt { { "w", 3.0 }, { "h", 1.01 } }));
        Assert.Equal("Lieferung am nächsten Arbeitstag", Spielwelt.LieferText(1));
        Assert.Equal("Lieferung in 3 Arbeitstagen", Spielwelt.LieferText(3));
    }

    /// <summary>Sortiment: Moebel aus Moebelhaus und Elektromarkt, keine Kleidung, keine Teile, kein alter Kleiderschrank.</summary>
    [Fact]
    public void ShopOhneKleidungUndOhneTeile()
    {
        var g = Laeden();
        var sortiment = Spielwelt.Sortiment(g);
        Assert.All(sortiment, p => Assert.Equal(Geschaefte.ArtMoebel, p.Ware.Art));
        Assert.Equal(g.Waren("moebelhaus").Count(w => w.Art == Geschaefte.ArtMoebel) + g.Waren("elektromarkt").Count(w => w.Art == Geschaefte.ArtMoebel), sortiment.Count);
        Assert.DoesNotContain(sortiment, p => p.Ware.Id == Charakter.AlterSchrank);
        // Gegenprobe: das Kaufhaus und die Teile gibt es, nur nicht online
        Assert.NotEmpty(g.Waren(Kleidung.Laden));
        var spiel = MitPc();
        Assert.Equal(Spielwelt.GibtEsNichtOnline, Assert.Throws<SpielFehler>(() => spiel.Bestellen(g, Kleidung.Laden, "hoodie_gruen")).Message);
        var teil = g.Waren("elektromarkt").First(w => w.Art == Geschaefte.ArtTeil);
        Assert.Equal(Spielwelt.GibtEsNichtOnline, Assert.Throws<SpielFehler>(() => spiel.Bestellen(g, "elektromarkt", teil.Id)).Message);
        Assert.Equal(Spielwelt.GibtEsNichtOnline, Assert.Throws<SpielFehler>(() => spiel.Bestellen(g, "moebelhaus", Charakter.AlterSchrank)).Message);
    }

    // -- Bestellen und Paket -----------------------------------------------------------

    /// <summary>
    /// Bestellen: Geld sofort weg (gleicher Preis wie im Laden, Kassenbuch Wohnen), das Stueck liegt erst nach dem Abholen im
    /// Karton; vor dem Liefertag kein Paket, ab dem Liefertag vor der Haustuer, ein Knopf nimmt alle mit (Nico 4a).
    /// Gegenprobe: ohne die neuen Felder legt moebel_gekauft das Stueck sofort in die Moebel.
    /// </summary>
    [Fact]
    public void BestellenGeldSofortStueckErstNachDemAbholen()
    {
        var spiel = MitPc();
        var g = Laeden();
        var geld = spiel.Stand.Geld;
        var tag = spiel.Stand.Tag;
        var sofa = spiel.Bestellen(g, "moebelhaus", "sofa");
        var pflanze = spiel.Bestellen(g, "moebelhaus", "pflanze");
        var letzte = spiel.Stand.Verlauf[^1];
        Assert.Equal(Ereignisse.MoebelGekauft, letzte.Art);
        Assert.Equal(Spielwelt.Online, letzte.Daten["weg"]);
        Assert.Equal(tag + 1, letzte.Daten["liefertag"]);
        Assert.Equal(geld - 450 - 35, spiel.Stand.Geld);
        Assert.False(spiel.Stand.Moebel.ContainsKey(sofa));
        Assert.False(spiel.Stand.Moebel.ContainsKey(pflanze));
        Assert.Equal(2, spiel.Stand.Pakete.Count);
        Assert.Empty(Spielwelt.Angekommen(spiel.Stand));
        Assert.Equal(Spielwelt.KeinPaket, Assert.Throws<SpielFehler>(() => spiel.PaketAbholen()).Message);
        BisTag(spiel, tag + 1);
        Assert.Equal([pflanze], Spielwelt.Angekommen(spiel.Stand));
        BisTag(spiel, tag + 3);
        Assert.Equal(new[] { sofa, pflanze }.Order(StringComparer.Ordinal), Spielwelt.Angekommen(spiel.Stand));
        spiel.PaketAbholen();
        Assert.Equal(Spielwelt.PaketAbgeholt, spiel.Stand.Verlauf[^1].Art);
        Assert.True(spiel.Stand.Moebel.ContainsKey(sofa));
        Assert.Equal("pflanze", spiel.Stand.Moebel.Get(pflanze));
        Assert.Empty(spiel.Stand.Pakete);
        Assert.Contains(Wohnung.ImKarton(spiel.Stand), k => Py.Gleich(k.Stueck, sofa));
        Assert.Empty(spiel.Stand.Unterschiede(spiel.Neuaufbau()));
        // Gegenprobe: dieselbe Zeile ohne „weg“ ist ein Kauf im Laden (sofort im Karton)
        var kopie = Py.Flach(letzte.Daten);
        var ohne = new Spielstand([.. spiel.Stand.Verlauf.Where(e => e.Art != Spielwelt.PaketAbgeholt)
            .Select(e => e.Art == Ereignisse.MoebelGekauft && Py.Gleich(e.Daten["stueck"], sofa)
                ? e with { Daten = new PyObjekt { { "stueck", sofa }, { "moebel", "sofa" }, { "geld", -450L } } } : e)], Inhalt);
        Assert.True(ohne.Moebel.ContainsKey(sofa));
        Assert.NotNull(kopie);
    }

    /// <summary>Ohne aufgestellten PC (auch nur im Karton) bestellt man nicht; nichts wird geschrieben.</summary>
    [Fact]
    public void BestellenNurMitAufgestelltemPc()
    {
        var spiel = NeuesSpiel();
        var pc = Kaufen(spiel, Spielwelt.PcTyp);
        var zeilen = spiel.Stand.Verlauf.Count;
        Assert.Equal(Spielwelt.NurAmPc, Assert.Throws<SpielFehler>(() => spiel.Bestellen(Laeden(), "moebelhaus", "sofa")).Message);
        Assert.Equal(zeilen, spiel.Stand.Verlauf.Count);
        Aufstellen(spiel, pc);
        spiel.Bestellen(Laeden(), "moebelhaus", "sofa");
        Assert.Single(spiel.Stand.Pakete);
    }

    /// <summary>Zu wenig Geld: wie im Laden die Meldung mit dem fehlenden Betrag, nichts geschrieben.</summary>
    [Fact]
    public void BestellenOhneGenugGeld()
    {
        var spiel = MitPc();
        spiel.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-ausgeben" }, { "tag", 1L }, { "richtig", false }, { "geld", -spiel.Stand.Geld + 100 } });
        var zeilen = spiel.Stand.Verlauf.Count;
        Assert.Contains("350 € fehlen", Assert.Throws<SpielFehler>(() => spiel.Bestellen(Laeden(), "moebelhaus", "sofa")).Message);
        Assert.Equal(zeilen, spiel.Stand.Verlauf.Count);
    }

    /// <summary>
    /// Zwei Geraete ohne Abgleich holen dasselbe Paket ab: die zweite Zeile zaehlt nicht (das Stueck ist schon im Karton),
    /// kein Fehler. Unsinnige Daten (keine Liste, unbekanntes Stueck) stuerzen nicht ab und aendern nichts.
    /// </summary>
    [Fact]
    public void DoppeltAbholenZaehltEinmal()
    {
        var spiel = MitPc();
        var stueck = spiel.Bestellen(Laeden(), "moebelhaus", "bild");
        BisTag(spiel, spiel.Stand.Tag + 1);
        var zeile = Spielwelt.Abholen(spiel.Stand)[0];
        spiel.Log(zeile.Art, zeile.Daten);
        var moebel = spiel.Stand.Moebel.Count;
        spiel.Log(zeile.Art, Py.Flach(zeile.Daten));   // die Zeile des anderen Geraets
        spiel.Log(Spielwelt.PaketAbgeholt, new PyObjekt { { "stuecke", "kaputt" } });
        spiel.Log(Spielwelt.PaketAbgeholt, new PyObjekt { { "stuecke", new List<object?> { "gibt-es-nicht", 3L } } });
        Assert.Equal(moebel, spiel.Stand.Moebel.Count);
        Assert.Equal("bild", spiel.Stand.Moebel.Get(stueck));
        Assert.Equal(3, spiel.Stand.Verlauf.Count(e => e.Art == Spielwelt.PaketAbgeholt) - 1);
        Assert.Empty(spiel.Stand.Unterschiede(spiel.Neuaufbau()));
    }

    /// <summary>
    /// Ein altes Geraet (A 0.40.0) kennt „weg“ nicht: es haette das Stueck sofort im Karton. Der neue Stand ohne Abholzeile
    /// hat es im Paket; mit dem Abholen stimmen beide wieder (Geld gleich, nichts doppelt).
    /// </summary>
    [Fact]
    public void AltesGeraetHatDasGleicheGeld()
    {
        var spiel = MitPc();
        spiel.Bestellen(Laeden(), "elektromarkt", "fernseher");
        var alt = new Spielstand([.. spiel.Stand.Verlauf.Select(e => e.Art == Ereignisse.MoebelGekauft && Spielwelt.IstBestellung(e.Daten)
            ? e with { Daten = new PyObjekt { { "stueck", e.Daten["stueck"] }, { "moebel", e.Daten["moebel"] }, { "geld", e.Daten["geld"] } } } : e)], Inhalt);
        Assert.Equal(alt.Geld, spiel.Stand.Geld);
        Assert.Equal(alt.Moebel.Count, spiel.Stand.Moebel.Count + 1);
        BisTag(spiel, spiel.Stand.Tag + 2);
        spiel.PaketAbholen();
        Assert.Equal(alt.Moebel.Count, spiel.Stand.Moebel.Count);
    }

    /// <summary>Beim Bestellen zaehlt ein Kleiderschrank erst nach dem Abholen (vorher liegt er nicht im Karton).</summary>
    [Fact]
    public void BestellterSchrankZaehltNachDemAbholen()
    {
        var spiel = MitPc();
        spiel.Bestellen(Laeden(), "moebelhaus", "kleiderschrank_klein");
        Assert.False(Charakter.Plaetze(spiel.Stand).HatSchrank);
        BisTag(spiel, spiel.Stand.Tag + 2);
        spiel.PaketAbholen();
        Assert.Equal(8, Charakter.Plaetze(spiel.Stand).Plaetze);
    }

    // -- Menues in der Spielwelt -------------------------------------------------------

    /// <summary>
    /// Feste Ausnahme von Python (P12-3, Nico 1): G47 und <see cref="Betrieb.Reiter"/> nennen den Reiter „kredite“; die
    /// Firmen-Tafel zeigt ihn nicht mehr (Kredite am Bank-Schalter und am PC zu Hause). Sonst gleiche Reiter in gleicher Folge.
    /// </summary>
    [Fact]
    public void Firma_ReiterOhneKredite_AusnahmeVonPython()
    {
        var stand = SpielFingerabdruckTests.Gespielt.Value.Stand;
        var python = Betrieb.Reiter(stand);
        Assert.Contains(python, r => r.Id == "kredite");
        var ohne = Spielwelt.FirmaReiter(stand);
        Assert.DoesNotContain(ohne, r => r.Id == "kredite");
        Assert.Equal(python.Where(r => r.Id != "kredite"), ohne);
        Assert.Equal(python.Count - 1, ohne.Count);
    }

    /// <summary>
    /// Ortsliste ohne 3D: in der Wohnung stehen Kleiderschrank, PC, Meine Reise, Schlafen und Paket mitnehmen nur mit dem
    /// aufgestellten Moebel bzw. angekommenem Paket; mit Firma im Buero „Firma öffnen“. Vorhandene Knoepfe kommen nicht doppelt.
    /// </summary>
    [Fact]
    public void OrtslisteErreichtJedenPlatz()
    {
        var spiel = NeuesSpiel();
        // Startmatratze: nur Schlafen
        Assert.Equal(["schlafen"], Spielwelt.ErsatzKnoepfe(spiel.Stand, SpielDaten.OrtZuhause, []).Select(k => k.Aktion));
        Assert.Empty(Spielwelt.ErsatzKnoepfe(spiel.Stand, SpielDaten.OrtZuhause, ["schlafen"]));
        var pc = Kaufen(spiel, Spielwelt.PcTyp);
        var regal = Kaufen(spiel, Spielwelt.RegalTyp);
        var schrank = Kaufen(spiel, "kleiderschrank_klein");
        Assert.Equal(["schlafen"], Spielwelt.ErsatzKnoepfe(spiel.Stand, SpielDaten.OrtZuhause, []).Select(k => k.Aktion));   // im Karton: nichts
        Aufstellen(spiel, pc);
        Aufstellen(spiel, regal);
        Aufstellen(spiel, schrank);
        spiel.Bestellen(Laeden(), "moebelhaus", "bild");
        BisTag(spiel, spiel.Stand.Tag + 1);
        Assert.Equal([Charakter.Aktion, Spielwelt.AktionComputer, Spielwelt.AktionReise, "schlafen", Spielwelt.AktionPaket],
            Spielwelt.ErsatzKnoepfe(spiel.Stand, SpielDaten.OrtZuhause, []).Select(k => k.Aktion));
        Assert.DoesNotContain(Spielwelt.ErsatzKnoepfe(spiel.Stand, SpielDaten.OrtBuero, []), k => k.Aktion == Spielwelt.AktionFirma);
        var firma = SpielFingerabdruckTests.Gespielt.Value.Stand;
        Assert.Contains(Spielwelt.ErsatzKnoepfe(firma, SpielDaten.OrtBuero, []), k => k.Aktion == Spielwelt.AktionFirma);
        Assert.Contains(Spielwelt.ErsatzKnoepfe(firma, SpielDaten.OrtFiliale, []), k => k.Aktion == Spielwelt.AktionFirma);
        Assert.Equal(firma.FeierabendMoeglich(), Spielwelt.ErsatzKnoepfe(firma, SpielDaten.OrtBuero, []).Any(k => k.Aktion == "feierabend"));
    }

    /// <summary>
    /// 3D: am Schreibtisch mit PC „PC benutzen“ (neben „Lernen“), am Buecherregal „Meine Reise“, an der Wohnungstuer mit
    /// angekommenem Paket „Paket mitnehmen“. Gegenprobe: weit weg vom Moebel kein Knopf.
    /// </summary>
    [Fact]
    public void KnoepfeAmMoebelInDerWohnung()
    {
        var spiel = NeuesSpiel();
        var pc = Kaufen(spiel, Spielwelt.PcTyp);
        var regal = Kaufen(spiel, Spielwelt.RegalTyp);
        Aufstellen(spiel, pc);
        Aufstellen(spiel, regal);
        spiel.Bestellen(Laeden(), "moebelhaus", "pflanze");
        BisTag(spiel, spiel.Stand.Tag + 1);
        var stadt = StadtSpielMit(spiel);
        Assert.Contains(stadt.Knoepfe, k => k.Aktion == "buero");
        Assert.Contains(stadt.Knoepfe, k => k.Aktion == Spielwelt.AktionPaket);
        Assert.Contains(Spielwelt.PaketText(1), stadt.Zeile);
        foreach (var (stueck, aktion) in new[] { (pc, Spielwelt.AktionComputer), (regal, Spielwelt.AktionReise) })
        {
            var m = Wohnung.AufgestellteMoebel(spiel.Stand).Single(a => a.Stueck == stueck);
            Assert.True(stadt.GeheZu(m.X + (Py.F(m.W) / 2), m.Y + (Py.F(m.H) / 2)) || stadt.Knoepfe.Count > 0);
            for (var i = 0; i < 400 && stadt.Fortschreiten(0.05); i++)
            {
            }
            Assert.Contains(stadt.Knoepfe, k => k.Aktion == aktion);
            Assert.DoesNotContain(stadt.Knoepfe, k => k.Aktion == Spielwelt.AktionPaket);
        }
        Assert.NotNull(Spielwelt.Nahe(spiel.Stand, Spielwelt.PcTyp, Wohnung.AufgestellteMoebel(spiel.Stand).Single(a => a.Stueck == pc).X, Wohnung.AufgestellteMoebel(spiel.Stand).Single(a => a.Stueck == pc).Y));
        Assert.Null(Spielwelt.Nahe(spiel.Stand, Spielwelt.PcTyp, -5, -5));
        // Paket mitnehmen draussen und drinnen: danach kein Knopf mehr
        spiel.PaketAbholen();
        stadt.PaketGenommen(1);
        Assert.DoesNotContain(stadt.Knoepfe, k => k.Aktion == Spielwelt.AktionPaket);
        Assert.Equal(0, stadt.PaketeVorDerTuer);
    }

    /// <summary>Draussen vor der Wohnung mit Paket: Zeile und Knopf; geht die Figur weiter, ist der Knopf weg.</summary>
    [Fact]
    public void PaketKnopfDraussenVorDerWohnung()
    {
        var spiel = MitPc();
        spiel.Bestellen(Laeden(), "moebelhaus", "bild");
        BisTag(spiel, spiel.Stand.Tag + 1);
        var stadt = StadtSpielMit(spiel);
        stadt.Verlassen();
        Assert.False(stadt.ImInnenraum);
        Assert.Equal([(Spielwelt.AktionPaket, Spielwelt.KnopfPaket)], stadt.Knoepfe);
        Assert.Contains(Spielwelt.PaketText(1), stadt.Zeile);
        Assert.True(stadt.Knopf(Spielwelt.AktionPaket));
        stadt.GeheZu("bitweiche");
        Assert.Empty(stadt.Knoepfe);
    }

    /// <summary>Im eigenen Buero an einem freien Schreibtisch „Firma öffnen“ (DekoNahe), weit weg keiner.</summary>
    [Fact]
    public void BueroSchreibtischHatDieFirma()
    {
        var stand = SpielFingerabdruckTests.Gespielt.Value.Stand;
        var innen = Innenraum.Bauen("buero", stand, SpielInnenraumTests.Innenraeume.Value, null, Daten, null, null, Jahreszeit.Sommer);
        Assert.NotNull(innen);
        var tisch = innen.Gebaeude["raeume"] is List<object?> raeume
            ? raeume.Select(SpielDaten.Objekt).SelectMany(r => SpielDaten.ListeOderLeer(r, "deko").Select(SpielDaten.Objekt)).First(d => Py.Gleich(d["typ"], "schreibtisch"))
            : null;
        Assert.NotNull(tisch);
        Assert.NotNull(innen.DekoNahe(Spielwelt.BueroTischTyp, Py.F(tisch["x"]) + 0.5, Py.F(tisch["y"]) + Py.F(tisch["h"]) + 0.4));
        Assert.Null(innen.DekoNahe(Spielwelt.BueroTischTyp, Py.F(tisch["x"]) + 0.5, Py.F(tisch["y"]) + Py.F(tisch["h"]) + 3));
        Assert.Null(innen.DekoNahe("gibt-es-nicht", Py.F(tisch["x"]), Py.F(tisch["y"])));
    }

    // -- Darstellung -------------------------------------------------------------------

    /// <summary>
    /// Kartonstapel vor der Haustuer (Chat-Fund P12-5): Platz aus darstellung.paket_platz, ausserhalb des Hauses und neben
    /// dem Fussweg (die Figur geht nicht hindurch), frei von Fest-Deko jeder Festzeit, Laternen und Baeumen; Stapel bis 6.
    /// </summary>
    [Fact]
    public void KartonstapelNebenDerTuer()
    {
        var stadt = new Stadt(Inhalt, SpielStadtTests.Daten.Value);
        Assert.Equal((0.5f, 0.42f), Lieferpaket.Platz(stadt));
        var szene = StadtSzene.Bauen(stadt, NeuesSpiel().Stand, SpielGrafikTests.Bibliothek.Value, null, FestDekoTests.Daten.Value);
        var lage = Lieferpaket.Lage(szene, stadt);
        Assert.NotNull(lage);
        var (mitte, aussen) = lage.Value;
        var halb = Lieferpaket.Breite * 1.6f;
        // nicht im Haus
        foreach (var haus in szene.Haeuser)
        {
            Assert.False(mitte.X + halb > haus.Min.X && mitte.X - halb < haus.Max.X && mitte.Y + halb > haus.Min.Z && mitte.Y - halb < haus.Max.Z,
                "Karton im Haus " + haus.Id + " " + haus.Min + haus.Max + " bei " + mitte + " aussen " + aussen);
        }
        // neben dem Fussweg der Wohnung (halbe Wegbreite plus halber Stapel)
        var weg = SpielDaten.Liste(SpielDaten.Objekt(stadt.Zugaenge["zuhause"])["weg"]).Select(p => SpielDaten.Liste(p))
            .Select(p => new Vector2((float)Py.F(p[0]), (float)Py.F(p[1]))).ToList();
        Assert.True(Kurve.Abstand(mitte, weg) >= (StadtSzene.WegBreite / 2) + halb - 0.05f, "Karton auf dem Fussweg: " + Kurve.Abstand(mitte, weg));
        foreach (var (p, r) in szene.FestPlaetzeAlle)
        {
            Assert.True(Vector2.Distance(p, mitte) >= r + halb, "Karton in der Fest-Deko bei " + p + " r " + r);
        }
        foreach (var (p, r) in szene.Baeume)
        {
            Assert.True(Vector2.Distance(p, mitte) >= r + halb, "Karton im Baum bei " + p);
        }
        Assert.True(Vector2.Dot(aussen, mitte - szene.Tueren.Single(t => t.Ort == "zuhause").Mitte) > 0, "Karton steht vor der Tuer");
        Assert.Equal(6, Lieferpaket.Stapel(9).Count);
        Assert.Single(Lieferpaket.Stapel(1));
        Assert.True(Lieferpaket.Bauen(SpielGrafikTests.Bibliothek.Value, 3).AnzahlEcken > Lieferpaket.Bauen(SpielGrafikTests.Bibliothek.Value, 1).AnzahlEcken);
        Assert.NotNull(SpielGrafikTests.Bibliothek.Value.Modell(Lieferpaket.Modell));
    }

    /// <summary>
    /// Nico 2b: der begehbare Kleiderschrank hat ein eigenes Bild, die anderen bleiben beim alten. Seit Welle 2 Kleidung (Weisung
    /// P12-206, Entwurf B „Schrankwand mit Spiegel“): zwei geschlossene Schraenke, Spiegel, Kommode, kein Garderobenstaender mehr.
    /// </summary>
    [Fact]
    public void BegehbarerSchrankEigenesModell()
    {
        var spiel = NeuesSpiel();
        var begehbar = Kaufen(spiel, Begehbar.Id);
        Aufstellen(spiel, begehbar);
        var grafik = SpielGrafikTests.Bibliothek.Value;
        Assert.NotNull(grafik.Modell(Begehbar.Modell));
        var innen = Innenraum.Bauen("zuhause", spiel.Stand, SpielInnenraumTests.Innenraeume.Value, grafik, Daten, null, null, Jahreszeit.Sommer);
        Assert.NotNull(innen);
        Assert.DoesNotContain(innen.Stuecke, s => s.Name == Begehbar.Modell);
        Assert.Equal(2, innen.Stuecke.Count(s => s.Name == "bookcaseClosedDoors"));
        Assert.Single(innen.Stuecke, s => s.Name == "sideTableDrawers");
        // Gegenprobe: der grosse Schrank hat das alte Bild, ein Schrank ohne Kommode
        var zwei = NeuesSpiel();
        Aufstellen(zwei, Kaufen(zwei, "kleiderschrank_gross"));
        var innen2 = Innenraum.Bauen("zuhause", zwei.Stand, SpielInnenraumTests.Innenraeume.Value, grafik, Daten, null, null, Jahreszeit.Sommer);
        Assert.DoesNotContain(innen2!.Stuecke, s => s.Name == "sideTableDrawers");
        Assert.Single(innen2.Stuecke, s => s.Name == "bookcaseClosedDoors");
    }

    private static StadtSpiel StadtSpielMit(Spielablauf spiel)
    {
        var stadt = new Stadt(Inhalt, SpielStadtTests.Daten.Value);
        var s = new StadtSpiel(stadt, spiel, null, SpielInnenraumTests.Innenraeume.Value, Daten, null, () => null, () => Jahreszeit.Sommer);
        Assert.True(s.Betreten());
        Assert.Equal("zuhause", s.FlaecheId);
        return s;
    }
}
