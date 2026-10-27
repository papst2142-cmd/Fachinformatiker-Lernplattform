using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W7 (Weisung P7-12): die Geschaefte Moebelhaus und Elektromarkt (nur C#).
/// Inhalt (<c>inhalte_csharp/spiel/geschaefte.json</c> und die Laeden in <c>innenraeume.json</c>),
/// Kaufregeln (nur im geoeffneten Geschaeft, Teile nur mit Firma, Geld wie Python) und der dritte
/// Fingerabdruck: das Python-Szenario 045 (f2b59ce8) plus drei Einkaufstage in der Stadt.
/// Ohne die neuen Ereignisse bleibt f2b59ce8 gleich, der Stadt-Fingerabdruck 86ac8f19 ebenso.
/// </summary>
public class SpielGeschaefteTests
{
    /// <summary>Dritter Fingerabdruck (nur C#): Szenario <see cref="EinkaufSzenario"/>, Ereignisse und Werte.</summary>
    internal const string SollEinkaufHash = "185dee1b2feb625cd9b2d4f9af285b82800f82b8eb868e1b96025e0da01ae237";

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static string Ordner => Path.Combine(Pfade.Wurzel, "inhalte_csharp", "spiel");

    internal static readonly Lazy<PyObjekt> Daten = new(() => Geschaefte.Laden(Ordner));

    private static readonly Lazy<PyObjekt> Innenraeume = new(() =>
        (PyObjekt)Py.Lesen(File.ReadAllBytes(Path.Combine(Ordner, "innenraeume.json")))!);

    private static Geschaefte Neu(PyObjekt? daten = null) => new(Inhalt, daten ?? Daten.Value);

    private static Stadt NeueStadt() => new(Inhalt, SpielStadtTests.Daten.Value);

    private static Func<string> Uhr()
    {
        var zaehler = 0;
        return () =>
        {
            zaehler += 7;
            return new DateTime(2026, 10, 3).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        };
    }

    /// <summary>Hingehen und, wenn noch zu, bis zum Oeffnen warten.</summary>
    private static void Hin(Stadt stadt, Spielablauf spiel, string laden)
    {
        Assert.Equal(StadtGrund.Ok, stadt.Gehen(spiel, laden).Grund);
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
        var g = Neu();
        Assert.Empty(g.Pruefen(Innenraeume.Value));
        foreach (var laden in Geschaefte.Laeden)
        {
            Assert.NotEmpty(g.Waren(laden));
        }
        // Moebel kosten wie in Python (wohnungen.json), Teile im Laden nie billiger als beim Versand
        foreach (var w in Geschaefte.Laeden.SelectMany(g.Waren))
        {
            if (w.Art == Geschaefte.ArtMoebel)
            {
                var m = Wohnung.MoebelStueck(w.Id, Inhalt)!;
                Assert.Equal(Py.Int(m["preis"]), w.Preis);
                Assert.True(Py.Wahr(SpielDaten.Get(m, "laden", true)), w.Id);
            }
            else if (w.Art == Geschaefte.ArtTeil)
            {
                var teil = SpielDaten.Teil(w.Id, Inhalt)!;
                Assert.True(w.Preis >= Py.F(teil["preis"]), w.Id + " billiger als beim Versand");
            }
        }
        // jedes Moebel aus dem Laden der Handy-App gibt es in genau einem Geschaeft
        var alle = Geschaefte.Laeden.SelectMany(g.Waren).Where(w => w.Art == Geschaefte.ArtMoebel).Select(w => w.Id).ToList();
        Assert.Equal(alle.Count, alle.Distinct().Count());
        var python = SpielDaten.ListeOderLeer(SpielDaten.Objekt(Inhalt["wohnungen"]), "moebel").Select(SpielDaten.Objekt)
            .Where(m => Py.Wahr(SpielDaten.Get(m, "laden", true))).Select(m => Py.Str(m["id"])).Order(StringComparer.Ordinal);
        Assert.Equal(python, alle.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void PruefungFindetFehler()
    {
        var kaputt = (PyObjekt)Py.Lesen(PythonJson.Text(Daten.Value))!;
        var moebel = (List<object?>)SpielDaten.Objekt(kaputt["moebelhaus"])["moebel"]!;
        moebel.Add("gibt_es_nicht");
        moebel.Add(moebel[0]);
        var teile = (List<object?>)SpielDaten.Objekt(kaputt["elektromarkt"])["teile"]!;
        Py.Setzen(SpielDaten.Objekt(teile[0]), "preis", 0L);
        kaputt.Add("baumarkt", new PyObjekt());   // seit Phase 8 W5 ist "kaufhaus" ein Geschaeft
        var fehler = Neu(kaputt).Pruefen(Innenraeume.Value);
        Assert.Contains("moebelhaus: Moebel nicht im Laden: gibt_es_nicht", fehler);
        Assert.Contains(fehler, f => f.StartsWith("moebelhaus: doppelt: ", StringComparison.Ordinal));
        Assert.Contains(fehler, f => f.StartsWith("elektromarkt: Preis fehlt oder ungueltig: ", StringComparison.Ordinal));
        Assert.Contains("unbekanntes Geschaeft: baumarkt", fehler);
        Assert.Contains(Neu().Pruefen(null), f => f.EndsWith("kein Grundriss in innenraeume.json", StringComparison.Ordinal));
    }

    // -- Kaufregeln -------------------------------------------------------------

    [Fact]
    public void MoebelKommtInDenKarton()
    {
        var stadt = NeueStadt();
        var g = Neu();
        // mit dem Startgeld reicht es fuer kein Moebel: sechs Arbeitstage aus dem Stadt-Szenario
        var spiel = SpielStadtTests.StadtSzenario().Spiel;
        var ware = g.Waren("moebelhaus").Where(w => w.Art == Geschaefte.ArtMoebel).MinBy(w => w.Preis)!;
        // zu Hause geht nichts
        Assert.Equal(Geschaefte.NurImLaden, Assert.Throws<SpielFehler>(() => spiel.MoebelKaufen(g, stadt, "moebelhaus", ware.Id, "mtest000001")).Message);
        // vor dem Oeffnen auch nicht
        Assert.Equal(StadtGrund.Ok, stadt.Gehen(spiel, "moebelhaus").Grund);
        if (stadt.Betreten(spiel.Stand) == StadtGrund.Geschlossen)
        {
            Assert.Equal(Geschaefte.NurImLaden, Assert.Throws<SpielFehler>(() => spiel.MoebelKaufen(g, stadt, "moebelhaus", ware.Id, "mtest000001")).Message);
            stadt.Warten(spiel);
        }
        var geld = spiel.Stand.Geld;
        var zahl = spiel.Stand.Verlauf.Count;
        Assert.Equal("mtest000001", spiel.MoebelKaufen(g, stadt, "moebelhaus", ware.Id, "mtest000001"));
        var s = spiel.Stand;
        Assert.Equal(geld - ware.Preis, s.Geld);
        Assert.Equal(ware.Id, s.Moebel.Get("mtest000001"));
        Assert.Contains(Wohnung.ImKarton(s), k => Py.Gleich(k.Stueck, "mtest000001"));
        Assert.Equal(zahl + 1, s.Verlauf.Count);
        Assert.Equal(Ereignisse.MoebelGekauft, s.Verlauf[^1].Art);
        // Moebel des Elektromarkts gibt es nicht im Moebelhaus
        var fremd = g.Waren("elektromarkt").First(w => w.Art == Geschaefte.ArtMoebel);
        Assert.Equal(Geschaefte.GibtEsNicht, Assert.Throws<SpielFehler>(() => spiel.MoebelKaufen(g, stadt, "moebelhaus", fremd.Id)).Message);
        // zu teuer: Meldung wie Python
        var teuer = g.Waren("moebelhaus").Where(w => w.Art == Geschaefte.ArtMoebel).MaxBy(w => w.Preis)!;
        while (spiel.Stand.Geld >= teuer.Preis)
        {
            spiel.MoebelKaufen(g, stadt, "moebelhaus", teuer.Id);
        }
        var fehlt = teuer.Preis - Math.Max(0, spiel.Stand.Geld);
        Assert.Equal(PyFormat.Prozent("Dafür reicht dein Geld noch nicht (%d € fehlen).", fehlt),
            Assert.Throws<SpielFehler>(() => spiel.MoebelKaufen(g, stadt, "moebelhaus", teuer.Id)).Message);
        Assert.Equal(0, spiel.Abweichungen);
    }

    [Fact]
    public void TeileNurMitFirma()
    {
        var stadt = NeueStadt();
        var g = Neu();
        var spiel = SpielStadtTests.StadtSzenario().Spiel;
        var teil = g.Waren("elektromarkt").First(w => w.Art == Geschaefte.ArtTeil);
        Hin(stadt, spiel, "elektromarkt");
        var zahl = spiel.Stand.Verlauf.Count;
        Assert.Equal(Geschaefte.NurMitFirma, Assert.Throws<SpielFehler>(() => spiel.TeilKaufen(g, stadt, "elektromarkt", teil.Id)).Message);
        Assert.Equal(zahl, spiel.Stand.Verlauf.Count);
        // im Moebelhaus gibt es keine Teile
        Hin(stadt, spiel, "moebelhaus");
        Assert.Equal(Geschaefte.GibtEsNicht, Assert.Throws<SpielFehler>(() => spiel.TeilKaufen(g, stadt, "moebelhaus", teil.Id)).Message);
    }

    [Fact]
    public void TeilLiegtSofortImLager()
    {
        var (spiel, stadt, g) = MitFirma();
        Hin(stadt, spiel, "elektromarkt");
        var teil = g.Waren("elektromarkt").First(w => w.Art == Geschaefte.ArtTeil);
        var geld = spiel.Stand.Geld;
        var vorher = Py.F(spiel.Stand.Lager().Get(teil.Id, 0L));
        spiel.TeilKaufen(g, stadt, "elektromarkt", teil.Id);
        var s = spiel.Stand;
        Assert.Equal(geld - teil.Preis, s.Geld);
        Assert.Equal(vorher + 1, Py.F(s.Lager().Get(teil.Id, 0L)));
        Assert.Equal(Geschaefte.TeilGekauft, s.Verlauf[^1].Art);
        Assert.Equal(PythonJson.Standard(new PyObjekt
        {
            { "teil", teil.Id }, { "menge", 1L }, { "geld", -teil.Preis }, { "tag", s.Tag }, { "laden", "elektromarkt" },
        }), PythonJson.Standard(s.Verlauf[^1].Daten));
        Assert.Equal(0, spiel.Abweichungen);
    }

    /// <summary>Das Python-Szenario 045 (mit Firma) in einem neuen Speicher, nur seine Ereignisse.</summary>
    private static (Spielablauf Spiel, Stadt Stadt, Geschaefte G) MitFirma()
    {
        var speicher = new SpielSpeicher(Uhr());
        foreach (var e in SpielFingerabdruckTests.Gespielt.Value.Stand.Verlauf)
        {
            speicher.Schreiben(e.Art, PythonJson.Standard(e.Daten));
        }
        var spiel = new Spielablauf(Inhalt, speicher);
        Assert.True(Py.Wahr(spiel.Stand.Firma));
        return (spiel, NeueStadt(), Neu());
    }

    // -- dritter Fingerabdruck ---------------------------------------------------

    /// <summary>
    /// Drittes Szenario (nur C#): Szenario 045 mit Firma, dann drei Tage Einkauf: Moebelhaus (zwei
    /// Moebel), Elektromarkt (ein Moebel, drei Teile), heim, Feierabend.
    /// </summary>
    internal static (Spielablauf Spiel, List<object?> Stadtstaende) EinkaufSzenario(PyObjekt? daten = null)
    {
        var (spiel, stadt, _) = MitFirma();
        var g = Neu(daten);
        var staende = new List<object?>();
        void Merken(string was)
        {
            var s = stadt.Stand(spiel.Stand);
            staende.Add(new List<object?> { was, s.Tag, s.Ort, s.Uhr, spiel.Stand.Geld });
        }
        for (var tag = 1; tag <= 3; tag++)
        {
            Hin(stadt, spiel, "moebelhaus");
            Merken("moebelhaus");
            var moebel = g.Waren("moebelhaus").Where(w => w.Art == Geschaefte.ArtMoebel).ToList();
            for (var n = 0; n < 2; n++)
            {
                var ware = moebel[(tag * 5 + n * 3) % moebel.Count];
                spiel.MoebelKaufen(g, stadt, "moebelhaus", ware.Id, PyFormat.Prozent("mw7t%dn%d0000", tag, n)[..11]);
                Merken("gekauft " + ware.Id);
            }
            Hin(stadt, spiel, "elektromarkt");
            Merken("elektromarkt");
            var elektro = g.Waren("elektromarkt");
            var geraet = elektro.Where(w => w.Art == Geschaefte.ArtMoebel).ToList()[tag % 3];
            spiel.MoebelKaufen(g, stadt, "elektromarkt", geraet.Id, PyFormat.Prozent("mw7t%de00000", tag)[..11]);
            Merken("gekauft " + geraet.Id);
            var teile = elektro.Where(w => w.Art == Geschaefte.ArtTeil).ToList();
            for (var n = 0; n < 3; n++)
            {
                var teil = teile[(tag * 11 + n * 7) % teile.Count];
                spiel.TeilKaufen(g, stadt, "elektromarkt", teil.Id);
                Merken("gekauft " + teil.Id);
            }
            Assert.Equal(StadtGrund.Ok, stadt.Gehen(spiel, "zuhause").Grund);
            // wie szenario045.play_day: offene Entscheidungen mit der ersten Wahl
            foreach (var entscheidung in spiel.Stand.OffeneEntscheidungen())
            {
                spiel.Entscheiden(entscheidung["id"], ((PyObjekt)((List<object?>)entscheidung["optionen"]!)[0]!)["id"]);
            }
            spiel.Feierabend();
            Merken("feierabend");
        }
        return (spiel, staende);
    }

    private static string EinkaufHash(Spielablauf spiel, List<object?> staende)
    {
        var s = spiel.Stand;
        var blob = new PyObjekt
        {
            { "events", s.Verlauf.Select(e => (object?)new List<object?> { e.Art, e.Daten }).ToList() },
            { "werte", SpielFingerabdruckTests.Werte(s) },
            { "lager", s.Lager().Select(p => (object?)new List<object?> { p.Key, p.Value }).OrderBy(p => Py.Str(((List<object?>)p!)[0]), StringComparer.Ordinal).ToList() },
            { "moebel", s.Moebel.Select(p => (object?)new List<object?> { p.Key, p.Value }).OrderBy(p => Py.Str(((List<object?>)p!)[0]), StringComparer.Ordinal).ToList() },
            { "stadt", staende },
        };
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(SpielFingerabdruckTests.Dumps(blob, sortiert: true, ascii: false))));
    }

    [Fact]
    public void DritterFingerabdruck()
    {
        // ohne die neuen Ereignisse: f2b59ce8 bleibt (Szenario 045 neu aufgebaut)
        var (ohne, _, _) = MitFirma();
        Assert.Equal(SpielFingerabdruckTests.SollHash, SpielFingerabdruckTests.Hash(ohne.Stand));
        var (stadtSpiel, stadtStaende) = SpielStadtTests.StadtSzenario();
        Assert.Equal(SpielStadtTests.SollStadtHash, SpielStadtTests.StadtHash(stadtSpiel, stadtStaende));

        var (spiel, staende) = EinkaufSzenario();
        var s = spiel.Stand;
        Assert.Equal(9, s.Verlauf.Count(e => e.Art == Ereignisse.MoebelGekauft) - ohne.Stand.Verlauf.Count(e => e.Art == Ereignisse.MoebelGekauft));
        Assert.Equal(9, s.Verlauf.Count(e => e.Art == Geschaefte.TeilGekauft));
        Assert.Equal(ohne.Stand.Tag + 3, s.Tag);
        Assert.Equal(0, spiel.Abweichungen);
        // nur angehaengt: die Ereignisse des Python-Szenarios stehen unveraendert vorn
        for (var i = 0; i < ohne.Stand.Verlauf.Count; i++)
        {
            Assert.Equal(PythonJson.Standard(ohne.Stand.Verlauf[i].Daten), PythonJson.Standard(s.Verlauf[i].Daten));
        }
        var hash = EinkaufHash(spiel, staende);
        Assert.True(SollEinkaufHash == hash, "dritter Fingerabdruck: " + hash);
        // Kontrollfehler: ein Euro mehr fuer ein Teil aendert den Fingerabdruck
        var teurer = (PyObjekt)Py.Lesen(PythonJson.Text(Daten.Value))!;
        foreach (var t in ((List<object?>)SpielDaten.Objekt(teurer["elektromarkt"])["teile"]!).Select(SpielDaten.Objekt))
        {
            Py.Setzen(t, "preis", Py.Int(t["preis"]) + 1);
        }
        var (spiel2, staende2) = EinkaufSzenario(teurer);
        Assert.NotEqual(SollEinkaufHash, EinkaufHash(spiel2, staende2));
    }
}
