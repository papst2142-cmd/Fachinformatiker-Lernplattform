using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W6 (Weisung P8-12, Teilplan P8-11 Abschnitte 2 und 5): Deko je Festzeit. Ohne Festzeit ist jede Szene
/// genau wie vorher (Teile, Stuecke, Signatur), mit Festzeit kommt nur etwas dazu; in der Stadt steht nichts auf
/// Strasse, Fussweg, Gleis oder Wasser; innen bleibt alles erreichbar, was vorher erreichbar war (jede Stelle, jede
/// Person, jede Ware, der Ausgang). Die Festzeit kommt als Wert, kein Test liest Datum oder Schalter.
/// </summary>
public class FestDekoTests
{
    internal static readonly Lazy<PyObjekt> Daten = new(() =>
        (PyObjekt)Py.Lesen(File.ReadAllBytes(Path.Combine(Pfade.Wurzel, "inhalte_csharp", "spiel", "festzeiten.json")))!);

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static GrafikBibliothek Grafik => SpielGrafikTests.Bibliothek.Value;

    private static Spielablauf NeuerAblauf()
    {
        var ablauf = new Spielablauf(Inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        return ablauf;
    }

    public static TheoryData<string> Feste => [.. Festzeit.Alle];

    /// <summary>Alle Innenraeume: neuer Spielstand (mit Laeden, Bank, Amt, Kunden) und der gespielte (Firmenbuero, Filiale, eingerichtete Wohnung).</summary>
    private static IEnumerable<(string Name, string Ansicht, Spielstand Stand)> Innenraeume()
    {
        var neu = NeuerAblauf().Stand;
        var ansichten = new List<string> { "buero", "zuhause", "serverfarm", "geschaeft:moebelhaus", "geschaeft:elektromarkt", "geschaeft:kaufhaus", "schalter:bank", "schalter:amt" };
        ansichten.AddRange(SpielDaten.Kundenorte(Inhalt).Select(k => "kunde:" + Py.Str(k["id"])));
        foreach (var a in ansichten)
        {
            yield return (a, a, neu);
        }
        var gespielt = SpielFingerabdruckTests.Gespielt.Value.Stand;
        foreach (var a in new[] { "buero", "filiale", "zuhause" })
        {
            yield return (a + " (gespielt)", a, gespielt);
        }
    }

    private static Innenraum Bauen(string ansicht, Spielstand stand, string? fest) =>
        Innenraum.Bauen(ansicht, stand, SpielInnenraumTests.Innenraeume.Value, Grafik, SpielGeschaefteTests.Daten.Value, fest, Daten.Value)!;

    [Fact]
    public void DateiGueltigUndModelleDa()
    {
        Assert.Empty(FestDeko.Pruefen(Daten.Value, Grafik));
        var modelle = FestDeko.Modelle(Daten.Value);
        Assert.Equal(18, modelle.Count);
        Assert.All(modelle, m => Assert.NotNull(Grafik.Modell(m)));
        Assert.Equal(2, FestDeko.Puppen(Daten.Value));
        // jede Festzeit hat Tueren, Plaetze und alle drei Plaetze innen
        foreach (var fest in Festzeit.Alle)
        {
            var muster = FestDeko.Muster(Daten.Value, fest);
            Assert.NotNull(muster);
            Assert.NotEmpty(SpielDaten.ListeOderLeer(muster!, "tueren"));
            Assert.NotEmpty(SpielDaten.ListeOderLeer(muster!, "plaetze"));
            Assert.All(FestDeko.InnenPlaetze, p => Assert.NotEmpty(SpielDaten.ListeOderLeer(SpielDaten.Objekt(muster!["innen"]), p)));
        }
        Assert.Null(FestDeko.Muster(Daten.Value, null));
        Assert.Null(FestDeko.Muster(Daten.Value, Festzeit.Ostern, "andere-stadt"));
    }

    [Fact]
    public void PruefungFindetFehler()
    {
        var kaputt = (PyObjekt)Py.Kopie(Daten.Value)!;
        // Zeitraum anders als im Code
        Py.Setzen(SpielDaten.Objekt(SpielDaten.Objekt(kaputt["zeitraeume"])["halloween"]), "von", new List<object?> { 10L, 25L });
        var muster = SpielDaten.Objekt(SpielDaten.Objekt(SpielDaten.Objekt(kaputt["staedte"])["standard"])["ostern"]);
        SpielDaten.Liste(muster["tueren"]).Add(new PyObjekt { { "art", "zauber" }, { "an", "links" } });
        SpielDaten.Liste(muster["plaetze"]).Add(new PyObjekt { { "art", "modell" }, { "name", "gibt-es-nicht" }, { "x", 1.0 }, { "z", 1.0 }, { "hoehe", 0.2 }, { "farbe", 1L } });
        Py.Setzen(SpielDaten.Objekt(SpielDaten.Objekt(kaputt["staedte"])["standard"]), "fasching", new PyObjekt());
        var fehler = FestDeko.Pruefen(kaputt, Grafik);
        Assert.Contains("festzeiten: zeitraeume ungleich Festzeit.cs", fehler);
        Assert.Contains(fehler, f => f.Contains("unbekannte Art zauber", StringComparison.Ordinal));
        Assert.Contains(fehler, f => f.Contains("Modell fehlt gibt-es-nicht", StringComparison.Ordinal));
        Assert.Contains(fehler, f => f.Contains("modell: unbekanntes Feld farbe", StringComparison.Ordinal));
        Assert.Contains(fehler, f => f.Contains("unbekannte Festzeit standard/fasching", StringComparison.Ordinal));
    }

    /// <summary>Ohne Festzeit (auch mit geladener festzeiten.json) ist die Stadt genau wie vorher; mit Festzeit nur angehaengt.</summary>
    [Fact]
    public void StadtOhneFestzeitUnveraendertMitNurAngehaengt()
    {
        var stadt = new Stadt(Inhalt, SpielStadtTests.Daten.Value);
        var stand = NeuerAblauf().Stand;
        var vorher = StadtSzene.Bauen(stadt, stand, Grafik);
        var ohne = StadtSzene.Bauen(stadt, stand, Grafik, null, Daten.Value);
        Assert.Equal(vorher.Teile, ohne.Teile);
        Assert.Equal(vorher.Stuecke.Select(s => (s.Name, s.Lage)), ohne.Stuecke.Select(s => (s.Name, s.Lage)));
        Assert.Equal(vorher.Signatur, ohne.Signatur);
        Assert.Equal(StadtSzene.SignaturFuer(stadt, stand), vorher.Signatur);
        Assert.Equal(StadtSzene.SignaturFuer(stadt, stand, null), vorher.Signatur);
        Assert.Empty(ohne.FestGesetzt);
        Assert.Null(ohne.Fest);
        foreach (var fest in Festzeit.Alle)
        {
            var mit = StadtSzene.Bauen(stadt, stand, Grafik, fest, Daten.Value);
            Assert.Equal(vorher.Teile, mit.Teile.Take(vorher.Teile.Count));
            Assert.Equal(vorher.Stuecke.Select(s => (s.Name, s.Lage)), mit.Stuecke.Take(vorher.Stuecke.Count).Select(s => (s.Name, s.Lage)));
            Assert.True(mit.Teile.Count + mit.Stuecke.Count > vorher.Teile.Count + vorher.Stuecke.Count, fest + ": keine Deko");
            Assert.Equal(vorher.Signatur + ";fest:" + fest, mit.Signatur);
            Assert.Equal(mit.Signatur, StadtSzene.SignaturFuer(stadt, stand, fest));
            Assert.Equal(fest, mit.Fest);
        }
    }

    private static readonly Dictionary<string, string[]> OhneTuerDeko = new()
    {
        // W8b (Weisung P8-20, Nico 4a): die Tuer sitzt jetzt genau am Ende des Fusswegs auf der Wand (vorher Mitte der
        // Rasterzelle); am Haltepunkt Birkenhain ist daneben kein Platz mehr fuer die Osterbeete (vorher ohne birkenhain)
        // W11 (Weisung P8-48): die Deko steht an der Wand der gezeichneten Tuer (vorher an der naechsten Seite des Hauskastens);
        // Talheim-Nord, Gewerbehof und Elektromarkt haben jetzt Platz, am Kaufhaus reicht er zu Ostern nicht mehr fuer die Beete
        [Festzeit.Ostern] = ["birkenhain", "amt", "bank", "kaufhaus"],
        [Festzeit.Halloween] = ["birkenhain"],
        [Festzeit.Weihnachten] = [],
        [Festzeit.Silvester] = [],
    };

    /// <summary>Kein Stueck auf Strasse, Fussweg, Gleis oder Wasser; Deko an den Tueren, wo Platz ist; stehende Stuecke sperren das freie Gehen.</summary>
    [Theory]
    [MemberData(nameof(Feste))]
    public void StadtNieAufWegen(string fest)
    {
        var stadt = new Stadt(Inhalt, SpielStadtTests.Daten.Value);
        var stand = SpielFingerabdruckTests.Gespielt.Value.Stand;
        var szene = StadtSzene.Bauen(stadt, stand, Grafik, fest, Daten.Value);
        Assert.NotEmpty(szene.FestGesetzt);
        foreach (var g in szene.FestGesetzt.Where(g => g.IstHindernis))
        {
            foreach (var (linie, halb) in szene.BaumSperren)
            {
                Assert.True(Kurve.Abstand(new Vector2(g.X, g.Z), linie) >= halb + g.R + FestDeko.WegRand - 1e-4f, fest + ": " + (g.Name ?? g.Art) + " auf einem Weg bei " + g.X + "/" + g.Z);
            }
            foreach (var o in szene.Orte)
            {
                Assert.True(Vector2.Distance(new Vector2(g.X, g.Z), new Vector2(o.StehX, o.StehZ)) >= FestDeko.StehRand + g.R + FestDeko.WegRand - 1e-4f,
                    fest + ": " + (g.Name ?? g.Art) + " auf dem Stehplatz von " + o.Id);
            }
            // die Figur geht nie hindurch: keine freie Zelle hat ihre Mitte im Stueck (Wege bleiben frei, das Stueck steht daneben)
            var raster = szene.Laufraster;
            var (s0, z0) = raster.Zelle(g.X - g.R - 0.5, g.Z - g.R - 0.5);
            var (s1, z1) = raster.Zelle(g.X + g.R + 0.5, g.Z + g.R + 0.5);
            for (var sp = s0; sp <= s1; sp++)
            {
                for (var ze = z0; ze <= z1; ze++)
                {
                    var (mx, mz) = raster.Mitte((sp, ze));
                    Assert.False(raster.IstFrei((sp, ze)) && Vector2.Distance(new Vector2((float)mx, (float)mz), new Vector2(g.X, g.Z)) < g.R,
                        fest + ": Figur geht durch " + (g.Name ?? g.Art) + " bei " + g.X + "/" + g.Z);
                }
            }
        }
        // an jeder Tuer haengt oder steht Deko; ohne Deko bleiben nur die Tueren, vor deren ganzer Fassade ein Gleis oder
        // eine Strasse liegt (feste Liste, steht in der Meldung P8-12), zu Ostern auch die mit zu wenig Platz fuer die Beete; Kranz (Weihnachten) und Konfetti (Silvester) passen an jede Tuer
        var ohne = szene.Orte.Where(o => !szene.FestGesetzt.Any(g => Vector2.Distance(new Vector2(g.X, g.Z), new Vector2(o.TuerX, o.TuerZ)) < 1.6f))
            .Select(o => o.Id).ToList();
        Assert.Equal(OhneTuerDeko[fest], ohne);
    }

    /// <summary>Ohne Festzeit ist jeder Innenraum genau wie vorher; mit Festzeit nur angehaengt und alles erreichbar.</summary>
    [Theory]
    [MemberData(nameof(Feste))]
    public void InnenNurAngehaengtUndAllesErreichbar(string fest)
    {
        foreach (var (name, ansicht, stand) in Innenraeume())
        {
            var vorher = Innenraum.Bauen(ansicht, stand, SpielInnenraumTests.Innenraeume.Value, Grafik, SpielGeschaefteTests.Daten.Value)!;
            var ohne = Bauen(ansicht, stand, null);
            Assert.Equal(vorher.Teile, ohne.Teile);
            Assert.Equal(vorher.Stuecke.Select(s => (s.Name, s.Lage)), ohne.Stuecke.Select(s => (s.Name, s.Lage)));
            Assert.Equal(vorher.Signatur, ohne.Signatur);
            Assert.Equal([.. vorher.Lauf.Frei], [.. ohne.Lauf.Frei]);
            Assert.Empty(ohne.FestGesetzt);

            var mit = Bauen(ansicht, stand, fest);
            Assert.Equal(fest, mit.Fest);
            Assert.Equal(vorher.Teile, mit.Teile.Take(vorher.Teile.Count));
            Assert.Equal(vorher.Stuecke.Select(s => (s.Name, s.Lage)), mit.Stuecke.Take(vorher.Stuecke.Count).Select(s => (s.Name, s.Lage)));
            Assert.StartsWith(vorher.Signatur + ";fest:" + fest, mit.Signatur, StringComparison.Ordinal);
            // der Grundriss selbst bleibt (Texte, Waren, Wohnung sehen die Deko nie)
            Assert.Equal(PythonJson.Standard(vorher.Gebaeude), PythonJson.Standard(mit.Gebaeude));

            // erreichbar bleibt jede Stelle, die vorher erreichbar war und nicht unter der Deko liegt
            var alt = FestDeko.Erreichbar(vorher.Lauf.Frei, vorher.StartX, vorher.StartZ);
            var neu = FestDeko.Erreichbar(mit.Lauf.Frei, mit.StartX, mit.StartZ);
            Assert.Equal(alt.Where(mit.Lauf.Frei.Contains).Count(), neu.Count);
            Assert.True(neu.IsSubsetOf(alt), name);
            // jede Person, die vorher erreichbar war, ist es noch (Weg wie beim Tippen)
            foreach (var p in vorher.Personen)
            {
                var vorherWeg = vorher.Weg(vorher.StartX, vorher.StartZ, p.X, p.Z, Wege.Reichweite).Count > 0 || vorher.PersonNahe(vorher.StartX, vorher.StartZ) == p;
                if (vorherWeg)
                {
                    Assert.True(mit.Weg(mit.StartX, mit.StartZ, p.X, p.Z, Wege.Reichweite).Count > 0, fest + ", " + name + ": Person " + p.Id + " nicht erreichbar");
                }
            }
            // jede Ware des Geschaefts
            foreach (var d in Geschaefte.Laeden.Contains(Innenraum.OrtFuer(ansicht)!) ? Geschaefte.Auslage(new PyObjekt { { "gebaeude", vorher.Gebaeude } }) : [])
            {
                double x = Py.F(d["x"]) + (Py.F(d["w"]) / 2), z = Py.F(d["y"]) + (Py.F(d["h"]) / 2);
                if (alt.Any(c => Nahe(c, d)))
                {
                    Assert.True(neu.Any(c => Nahe(c, d)), fest + ", " + name + ": Ware " + Py.Str(d["ware"]) + " nicht erreichbar");
                }
            }
            // die Deko steht nie auf einer Stelle, an der die Figur stehen kann
            foreach (var g in mit.FestGesetzt.Where(g => g.IstHindernis))
            {
                Assert.False(Wege.KannStehen(g.X, g.Z, mit.Lauf), fest + ", " + name + ": Figur kann in " + (g.Name ?? g.Art) + " stehen");
            }
        }
    }

    private static bool Nahe((long, long) zelle, PyObjekt d)
    {
        var (x, z) = Wege.Mitte(zelle);
        double x1 = Py.F(d["x"]), z1 = Py.F(d["y"]), x2 = x1 + Py.F(d["w"]), z2 = z1 + Py.F(d["h"]);
        var dx = Math.Max(Math.Max(x1 - x, 0), x - x2);
        var dz = Math.Max(Math.Max(z1 - z, 0), z - z2);
        return Math.Sqrt((dx * dx) + (dz * dz)) <= Wohnung.MoebelReichweite + 0.3;
    }

    /// <summary>Kaufhaus wie auf den Entwurfsfotos (Nico 5a): Ecke, links und rechts gesetzt, zwei Puppen in Festkleidung.</summary>
    [Theory]
    [MemberData(nameof(Feste))]
    public void KaufhausWieEntwurf(string fest)
    {
        var stand = NeuerAblauf().Stand;
        var innen = Bauen("geschaeft:kaufhaus", stand, fest);
        Assert.Equal(["ecke", "links", "rechts"], innen.FestGesetzt.Count > 0
            ? innen.Signatur.Split(";fest:")[1].Split(',').Skip(1).Select(p => p.Split('@')[0]).ToList()
            : []);
        var ohne = Bauen("geschaeft:kaufhaus", stand, null);
        // die Puppen aendern nur die Kleidung (gleiche Zahl), die Netze der ersten zwei sind andere
        Assert.True(innen.NetzBauen().AnzahlEcken > ohne.NetzBauen().AnzahlEcken, fest + ": keine Deko im Netz");
    }

    /// <summary>Wohnung (Nico 4a): fester Platz am Eingang, steht dort ein Moebel, entfaellt die Deko (keine Suche).</summary>
    [Fact]
    public void WohnungFesterPlatzOderNichts()
    {
        var stand = NeuerAblauf().Stand;
        foreach (var fest in Festzeit.Alle)
        {
            var innen = Bauen("zuhause", stand, fest);
            Assert.All(innen.Signatur.Split(";fest:")[1].Split(',').Skip(1), p => Assert.StartsWith("links@", p, StringComparison.Ordinal));
        }
        // ein Moebelstueck genau auf dem Platz: die Deko entfaellt
        var leer = Bauen("zuhause", stand, Festzeit.Weihnachten);
        if (leer.FestGesetzt.FirstOrDefault(g => g.IstHindernis) is { } stueck)
        {
            var gebaeude = (PyObjekt)Py.Kopie(leer.Gebaeude)!;
            var flur = SpielDaten.Objekt(gebaeude["flur"]);
            var deko = SpielDaten.ListeOderLeer(flur, "deko").ToList();
            deko.Add(new PyObjekt { { "typ", "regal" }, { "x", (double)stueck.X - 0.3 }, { "y", (double)stueck.Z - 0.3 }, { "w", 0.6 }, { "h", 0.6 } });
            Py.Setzen(flur, "deko", deko);
            var plan = FestDeko.InnenPlanen("zuhause", gebaeude, [], Grafik, Daten.Value, FestDeko.Muster(Daten.Value, Festzeit.Weihnachten), true, []);
            Assert.Null(plan);
        }
    }
}
