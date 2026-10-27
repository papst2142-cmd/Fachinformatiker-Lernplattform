using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W5 (Weisung P8-8c, Teilplan P8-7a mit den Empfehlungen a): das Kaufhaus. Sortiment und Slots aus
/// geschaefte.json, Kauf nur im geoeffneten Kaufhaus (zieht gleich an), Anziehen und Ablegen ueberall, ein
/// zweiter Kauf desselben Stuecks (zwei Geraete) zaehlt nicht, nicht gekaufte Stuecke traegt die Figur nie.
/// Vierter Fingerabdruck <see cref="SollKaufhausHash"/>; die drei bisherigen bleiben gleich.
/// </summary>
public class SpielKaufhausTests
{
    /// <summary>Vierter Fingerabdruck (nur C#): Szenario <see cref="KaufhausSzenario"/>, Ereignisse und Werte.</summary>
    internal const string SollKaufhausHash = "a3ec3cdf792700e39471b756c0454183b74b21c20354e0da33e06c777a0d4144";

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static PyObjekt Daten => SpielGeschaefteTests.Daten.Value;

    private static Geschaefte Neu(PyObjekt? daten = null) => new(Inhalt, daten ?? Daten);

    private static Stadt NeueStadt() => new(Inhalt, SpielStadtTests.Daten.Value);

    private static Spielablauf NeuesSpiel(long erspart = 400)
    {
        var zaehler = 0;
        var spiel = new Spielablauf(Inhalt, new SpielSpeicher(() =>
        {
            zaehler += 7;
            return new DateTime(2026, 10, 3).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }));
        spiel.Profil("Nico", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } });
        if (erspart > 0)
        {
            spiel.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 1L }, { "richtig", false }, { "geld", erspart } });
        }
        return spiel;
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

    [Fact]
    public void SortimentWieTeilplan()
    {
        Assert.Empty(Neu().Pruefen(SpielInnenraumTests.Innenraeume.Value));
        Assert.Equal(["oberteil:Oberteil:Oberteile", "hose:Hose:Hosen", "schuhe:Schuhe:Schuhe", "kopf:Kopf:Kappen und Mützen"],
            Kleidung.Stellen(Daten).Select(s => s.Id + ":" + s.Name + ":" + s.Abteilung));
        var sortiment = Kleidung.Sortiment(Daten);
        Assert.Equal(20, sortiment.Count);
        Assert.Equal([7, 4, 4, 5], Kleidung.Stellen(Daten).Select(s => sortiment.Count(k => k.Slot == s.Id)));
        Assert.Equal(12, sortiment.Min(k => k.Preis));
        Assert.Equal(90, sortiment.Max(k => k.Preis));
        Assert.Equal(sortiment.Select(k => k.Id), Neu().Waren(Kleidung.Laden).Select(w => w.Id));
        Assert.All(Neu().Waren(Kleidung.Laden), w => Assert.Equal(Kleidung.Art, w.Art));
        // das Kaufhaus ist ein Geschaeft der Stadt mit Sortiment Kleidung, offen 09:30 bis 20:00
        Assert.Contains(Kleidung.Laden, Geschaefte.Laeden);
        Assert.Empty(NeueStadt().Pruefen());
    }

    /// <summary>
    /// Phase 8 W6 (Nico 2a): je Festzeit kommen Kopf und Oberteil der Festkleidung ans Ende des Sortiments; ohne
    /// Festzeit bleibt es wie in W5. Gekauft werden kann nur, was gerade im Sortiment steht; gekannt, getragen und
    /// dargestellt wird alles (Kleidung.Alle, Finden).
    /// </summary>
    [Fact]
    public void SortimentJeFestzeit()
    {
        var basis = Kleidung.Sortiment(Daten);
        Assert.Equal(28, Kleidung.Alle(Daten).Count);
        Assert.Equal(basis, Kleidung.Sortiment(Daten, null));
        Assert.All(basis, k => Assert.Null(k.Festzeit));
        var erwartet = new Dictionary<string, string[]>
        {
            [Festzeit.Halloween] = ["hexenhut", "kuerbis_pulli"],
            [Festzeit.Weihnachten] = ["weihnachtsmuetze", "weihnachtspulli"],
            [Festzeit.Silvester] = ["partyhut", "glitzerhemd"],
            [Festzeit.Ostern] = ["hasenohren", "fruehlingsshirt"],
        };
        foreach (var (fest, ids) in erwartet)
        {
            var mit = Kleidung.Sortiment(Daten, fest);
            Assert.Equal(basis, mit.Take(basis.Count));
            Assert.Equal(ids, mit.Skip(basis.Count).Select(k => k.Id));
            Assert.Equal(["kopf", "oberteil"], mit.Skip(basis.Count).Select(k => k.Slot));
            var g = Neu();
            g.Festzeit = fest;
            Assert.Equal(mit.Select(k => k.Id), g.Waren(Kleidung.Laden).Select(w => w.Id));
            foreach (var id in ids)
            {
                Assert.NotNull(Kleidung.Finden(Daten, id));
                Assert.NotNull(Kleidung.FigurStueck(Daten, id));
            }
        }
        Assert.Equal(basis.Select(k => k.Id), Neu().Waren(Kleidung.Laden).Select(w => w.Id));
        // die neuen Formen haben Geometrie an der Figur
        foreach (var form in new[] { "spitzhut", "zipfelmuetze", "partyhut", "hasenohren" })
        {
            var ohne = Figur.Netz(SpielGrafikTests.Bibliothek.Value, null, 1f).AnzahlEcken;
            var mit = Figur.Netz(SpielGrafikTests.Bibliothek.Value, null, 1f, [new Figur.Stueck("kopf", form, "#DC2626")]).AnzahlEcken;
            Assert.True(mit > ohne, form + " ohne Geometrie");
        }
    }

    /// <summary>Phase 8 W6: Festkleidung kaufen geht nur in ihrer Festzeit (sonst wie ein unbekanntes Stueck), nichts wird geschrieben.</summary>
    [Fact]
    public void FestkleidungNurInDerFestzeit()
    {
        var stadt = NeueStadt();
        var ablauf = NeuesSpiel();
        InsKaufhaus(stadt, ablauf);
        var g = Neu();
        var zeilen = ablauf.Stand.Verlauf.Count;
        var fehler = Assert.Throws<SpielFehler>(() => ablauf.KleidungKaufen(g, stadt, Kleidung.Laden, "weihnachtsmuetze"));
        Assert.Equal(Geschaefte.GibtEsNicht, fehler.Message);
        g.Festzeit = Festzeit.Halloween;
        Assert.Throws<SpielFehler>(() => ablauf.KleidungKaufen(g, stadt, Kleidung.Laden, "weihnachtsmuetze"));
        Assert.Equal(zeilen, ablauf.Stand.Verlauf.Count);
        g.Festzeit = Festzeit.Weihnachten;
        ablauf.KleidungKaufen(g, stadt, Kleidung.Laden, "weihnachtsmuetze");
        Assert.True(ablauf.Stand.Kleider.ContainsKey("weihnachtsmuetze"));
        Assert.Equal("weihnachtsmuetze", Kleidung.Angezogen(ablauf.Stand, Daten)["kopf"].Id);
        // nach der Festzeit: bleibt gekauft, ablegen und wieder anziehen geht
        g.Festzeit = null;
        ablauf.KleidungAnziehen(g, "kopf", null);
        ablauf.KleidungAnziehen(g, "kopf", "weihnachtsmuetze");
        Assert.Equal("weihnachtsmuetze", Kleidung.Angezogen(ablauf.Stand, Daten)["kopf"].Id);
    }

    [Fact]
    public void PruefungFindetFehler()
    {
        PyObjekt Kaputt(Action<PyObjekt> aendern)
        {
            var d = (PyObjekt)Py.Lesen(PythonJson.Text(Daten))!;
            aendern(SpielDaten.Objekt(d[Kleidung.Laden]));
            return d;
        }
        PyObjekt Erstes(PyObjekt k) => SpielDaten.Objekt(((List<object?>)k["kleidung"]!)[0]);
        var faelle = new (string Fehler, PyObjekt Daten)[]
        {
            ("kaufhaus: Preis fehlt oder ungueltig: shirt_weiss", Kaputt(k => Py.Setzen(Erstes(k), "preis", 0L))),
            ("kaufhaus: shirt_weiss: unbekannter Slot", Kaputt(k => Py.Setzen(Erstes(k), "slot", "guertel"))),
            ("kaufhaus: shirt_weiss: Farbe ungueltig", Kaputt(k => Py.Setzen(Erstes(k), "farbe", "weiss"))),
            // Phase 8 W6: das Feld festzeit ist erlaubt (Festkleidung), aber nur mit einer bekannten Festzeit
            ("kaufhaus: shirt_weiss: unbekanntes Feld rabatt", Kaputt(k => Py.Setzen(Erstes(k), "rabatt", 10L))),
            ("kaufhaus: shirt_weiss: unbekannte Festzeit", Kaputt(k => Py.Setzen(Erstes(k), "festzeit", "fasching"))),
            ("kaufhaus: shirt_weiss: unbekannte Festzeit", Kaputt(k => Py.Setzen(Erstes(k), "festzeit", 1L))),
            ("kaufhaus: doppelt: shirt_schwarz", Kaputt(k => Py.Setzen(Erstes(k), "id", "shirt_schwarz"))),
            ("kaufhaus: unbekanntes Feld moebel", Kaputt(k => Py.Setzen(k, "moebel", new List<object?>()))),
        };
        foreach (var (fehler, daten) in faelle)
        {
            Assert.Contains(fehler, Neu(daten).Pruefen(SpielInnenraumTests.Innenraeume.Value));
        }
    }

    [Fact]
    public void KaufenZiehtGleichAn()
    {
        var spiel = NeuesSpiel(200);
        var stadt = NeueStadt();
        var g = Neu();
        // nur im geoeffneten Kaufhaus
        Assert.Equal(Geschaefte.NurImLaden, Assert.Throws<SpielFehler>(() => spiel.KleidungKaufen(g, stadt, Kleidung.Laden, "kappe_blau")).Message);
        InsKaufhaus(stadt, spiel);
        Assert.Equal("09:30", Stadt.Uhrzeit(stadt.Stand(spiel.Stand).Uhr));
        var geld = spiel.Stand.Geld;
        var vorher = spiel.Stand.Verlauf.Count;
        spiel.KleidungKaufen(g, stadt, Kleidung.Laden, "hoodie_gruen");
        Assert.Equal([Kleidung.Gekauft, Kleidung.Getragen], spiel.Stand.Verlauf.Skip(vorher).Select(e => e.Art));
        Assert.Equal(geld - 45, spiel.Stand.Geld);
        Assert.Equal("hoodie_gruen", Kleidung.Angezogen(spiel.Stand, Daten)["oberteil"].Id);
        var heute = SpielDaten.Objekt(spiel.Stand.Kassenbuch[spiel.Stand.Tag]);
        Assert.Equal(45L, SpielDaten.Objekt(heute["aus"])[Kleidung.BuchKleidung]);
        // schon gekauft, Geld reicht nicht, gibt es nicht
        Assert.Equal(Kleidung.SchonGekauft, Assert.Throws<SpielFehler>(() => spiel.KleidungKaufen(g, stadt, Kleidung.Laden, "hoodie_gruen")).Message);
        Assert.Equal(Geschaefte.GibtEsNicht, Assert.Throws<SpielFehler>(() => spiel.KleidungKaufen(g, stadt, Kleidung.Laden, "sofa")).Message);
        spiel.KleidungKaufen(g, stadt, Kleidung.Laden, "jeans_schwarz");
        spiel.KleidungKaufen(g, stadt, Kleidung.Laden, "sneaker_rot");
        spiel.KleidungKaufen(g, stadt, Kleidung.Laden, "kappe_blau");
        Assert.Equal(["hoodie_gruen", "jeans_schwarz", "sneaker_rot", "kappe_blau"], Kleidung.Angezogen(spiel.Stand, Daten).Values.Select(k => k.Id));
        Assert.Equal(200 - 45 - 40 - 60 - 15, spiel.Stand.Geld);
        Assert.Contains("fehlen", Assert.Throws<SpielFehler>(() => spiel.KleidungKaufen(g, stadt, Kleidung.Laden, "stiefel")).Message);

        // ablegen und wieder anziehen geht ueberall, nur mit gekauften Stuecken am richtigen Slot
        Assert.Equal(StadtGrund.Ok, stadt.Gehen(spiel, "zuhause").Grund);
        spiel.KleidungAnziehen(g, "kopf", null);
        Assert.False(Kleidung.Angezogen(spiel.Stand, Daten).ContainsKey("kopf"));
        Assert.Equal(Kleidung.NichtGekauft, Assert.Throws<SpielFehler>(() => spiel.KleidungAnziehen(g, "kopf", "hut_braun")).Message);
        Assert.Equal(Kleidung.NichtGekauft, Assert.Throws<SpielFehler>(() => spiel.KleidungAnziehen(g, "hose", "kappe_blau")).Message);
        spiel.KleidungAnziehen(g, "kopf", "kappe_blau");
        Assert.Equal("kappe_blau", Kleidung.Angezogen(spiel.Stand, Daten)["kopf"].Id);
        Assert.Equal(200 - 45 - 40 - 60 - 15, spiel.Stand.Geld);
        // Neuaufbau aus den Ereignissen: derselbe Stand
        Assert.Empty(spiel.Stand.Unterschiede(spiel.Neuaufbau()));
    }

    /// <summary>
    /// Doppelkauf (Nico 5a, wie E-5): zwei Geraete kaufen offline dasselbe Stueck, nach dem Abgleich stehen beide
    /// Zeilen im Protokoll; der zweite Kauf zieht kein Geld ab und zaehlt nicht im Kassenbuch.
    /// </summary>
    [Fact]
    public void DoppelkaufZaehltEinmal()
    {
        var spiel = NeuesSpiel();
        var stadt = NeueStadt();
        InsKaufhaus(stadt, spiel);
        var geld = spiel.Stand.Geld;
        var tag = spiel.Stand.Tag;
        var kauf = new PyObjekt { { "stueck", "hut_braun" }, { "geld", -30L }, { "tag", tag }, { "laden", Kleidung.Laden } };
        spiel.Log(Kleidung.Gekauft, kauf);
        spiel.Log(Kleidung.Gekauft, Py.Flach(kauf));   // die Zeile des anderen Geraets
        Assert.Equal(2, spiel.Stand.Verlauf.Count(e => e.Art == Kleidung.Gekauft));
        Assert.Equal(geld - 30, spiel.Stand.Geld);
        Assert.Equal(30L, SpielDaten.Objekt(SpielDaten.Objekt(spiel.Stand.Kassenbuch[tag])["aus"])[Kleidung.BuchKleidung]);
        Assert.Single(spiel.Stand.Kleider);
    }

    /// <summary>Getragen gilt nur fuer gekaufte Stuecke am richtigen Slot (fremde oder kaputte Zeilen uebergeht der Stand).</summary>
    [Fact]
    public void NichtGekauftesTraegtDieFigurNie()
    {
        var spiel = NeuesSpiel();
        spiel.Log(Kleidung.Getragen, new PyObjekt { { "getragen", new PyObjekt { { "oberteil", "pulli_rot" }, { "kopf", "unbekannt" } } } });
        Assert.Empty(Kleidung.Angezogen(spiel.Stand, Daten));
        spiel.Log(Kleidung.Gekauft, new PyObjekt { { "stueck", "pulli_rot" }, { "geld", -40L }, { "tag", 1L }, { "laden", Kleidung.Laden } });
        Assert.Equal("pulli_rot", Kleidung.Angezogen(spiel.Stand, Daten)["oberteil"].Id);
        spiel.Log(Kleidung.Getragen, new PyObjekt { { "getragen", new PyObjekt { { "hose", "pulli_rot" } } } });
        Assert.Empty(Kleidung.Angezogen(spiel.Stand, Daten));
        spiel.Log(Kleidung.Getragen, new PyObjekt { { "getragen", "kaputt" } });
        Assert.Null(spiel.Stand.Getragen);
    }

    // -- vierter Fingerabdruck ---------------------------------------------------

    /// <summary>
    /// Viertes Szenario (nur C#): von zu Hause ins Kaufhaus, warten bis 09:30, vier Stuecke kaufen, die Kappe
    /// ablegen, den Kapuzenpulli gegen das T-Shirt tauschen und wieder anziehen, heim.
    /// </summary>
    internal static (Spielablauf Spiel, List<object?> Stadtstaende) KaufhausSzenario(PyObjekt? daten = null)
    {
        var spiel = NeuesSpiel();
        var stadt = NeueStadt();
        var g = Neu(daten);
        var staende = new List<object?>();
        void Merken(string was)
        {
            var s = stadt.Stand(spiel.Stand);
            staende.Add(new List<object?> { was, s.Tag, s.Ort, s.Uhr, spiel.Stand.Geld });
        }
        Merken("start");
        InsKaufhaus(stadt, spiel);
        Merken("kaufhaus");
        foreach (var stueck in new[] { "hoodie_gruen", "jeans_schwarz", "sneaker_rot", "kappe_blau" })
        {
            spiel.KleidungKaufen(g, stadt, Kleidung.Laden, stueck);
            Merken("gekauft " + stueck);
        }
        spiel.KleidungKaufen(g, stadt, Kleidung.Laden, "shirt_weiss");
        spiel.KleidungAnziehen(g, "kopf", null);
        spiel.KleidungAnziehen(g, "oberteil", "hoodie_gruen");
        Merken("umgezogen");
        Assert.Equal(StadtGrund.Ok, stadt.Gehen(spiel, "zuhause").Grund);
        Merken("zuhause");
        spiel.Neuaufbau();
        return (spiel, staende);
    }

    private static string KaufhausHash(Spielablauf spiel, List<object?> staende)
    {
        var s = spiel.Stand;
        var blob = new PyObjekt
        {
            { "events", s.Verlauf.Select(e => (object?)new List<object?> { e.Art, e.Daten }).ToList() },
            { "werte", SpielFingerabdruckTests.Werte(s) },
            { "buchungen", s.Kassenbuch.Select(p => (object?)new List<object?> { p.Key, p.Value }).ToList() },
            { "kleider", s.Kleider.Select(p => (object?)new List<object?> { p.Key, p.Value }).ToList() },
            { "getragen", Kleidung.Angezogen(s, Daten).Select(p => (object?)new List<object?> { p.Key, p.Value.Id }).ToList() },
            { "stadt", staende },
        };
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(SpielFingerabdruckTests.Dumps(blob, sortiert: true, ascii: false))));
    }

    [Fact]
    public void VierterFingerabdruck()
    {
        var (spiel, staende) = KaufhausSzenario();
        var s = spiel.Stand;
        Assert.Equal(5, s.Verlauf.Count(e => e.Art == Kleidung.Gekauft));
        Assert.Equal(["hoodie_gruen", "jeans_schwarz", "sneaker_rot"], Kleidung.Angezogen(s, Daten).Values.Select(k => k.Id));
        Assert.Equal(0, spiel.Abweichungen);
        var hash = KaufhausHash(spiel, staende);
        Assert.True(SollKaufhausHash == hash, "vierter Fingerabdruck: " + hash);
        // Kontrollfehler: ein Euro mehr fuer ein Stueck aendert den Fingerabdruck
        var teurer = (PyObjekt)Py.Lesen(PythonJson.Text(Daten))!;
        foreach (var k in ((List<object?>)SpielDaten.Objekt(teurer[Kleidung.Laden])["kleidung"]!).Select(SpielDaten.Objekt))
        {
            Py.Setzen(k, "preis", Py.Int(k["preis"]) + 1);
        }
        var (spiel2, staende2) = KaufhausSzenario(teurer);
        Assert.NotEqual(SollKaufhausHash, KaufhausHash(spiel2, staende2));
    }
}
