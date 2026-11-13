using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;

namespace Stadtbau.Tests;

/// <summary>
/// Weisung P12-212 Punkt 5 (= a): das Werkzeug zeigt die Laufzeiten zwischen den Orten genau wie das Spiel sie rechnet
/// (2 Spielminuten je Schritt) und warnt gelb ab 60 Minuten; nie rot. Welle A 0.70.0 (Weisung P12-221): dazu die Zeit mit dem
/// Bus genau wie die Ortsliste im Spiel (<see cref="StadtSpiel.BusLohntVon"/>), gelb nach der kuerzeren Zeit; Fusswege zaehlen
/// im Weg-Raster des Spiels mit.
/// </summary>
public class LaufzeitenTests
{
    private static Spieldaten D => Daten.Spiel;

    [Fact]
    public void Stadt1_GleichDemSpiel_JedesPaarEinmal_LaengsteZuerst()
    {
        var p = Projekt.Neu("Laufzeiten", D, Daten.NeueAblage());
        var st = p.Stand();
        var wege = Laufzeiten.Rechnen(st, D.Inhalt, D.Grafik);
        var orte = Spielort.Stadt1Orte(D.Fassung).Count;
        Assert.Equal(orte * (orte - 1) / 2, wege.Count);
        Assert.Equal(wege.Count, wege.Select(w => (w.Von, w.Nach)).Distinct().Count());
        for (var i = 1; i < wege.Count; i++)
        {
            Assert.True((wege[i - 1].Kuerzer ?? long.MaxValue) >= (wege[i].Kuerzer ?? long.MaxValue));
        }
        // wie das Spiel: Schritte der Breitensuche mal 2 Minuten
        var stadt = (Stadt)SpielKern.Regeln.Stadt(st, st.MitKarte(D.Inhalt), nachsichtig: true)!;
        Assert.Equal(2, stadt.MinutenJeSchritt);
        foreach (var w in wege)
        {
            Assert.NotNull(w.Minuten);
            Assert.Equal(stadt.Weg(w.Von, w.Nach)!.Schritte * 2, w.Minuten);
        }
    }

    [Fact]
    public void Stadt1_EinGelberHinweis_ZahlUndLaengsterWeg_NieRot()
    {
        var p = Projekt.Neu("Laufzeiten", D, Daten.NeueAblage());
        var wege = Laufzeiten.Rechnen(p.Stand(), D.Inhalt, D.Grafik);
        var lang = wege.Where(w => w.Kuerzer >= Laufzeiten.GelbAb).ToList();
        Assert.NotEmpty(lang);
        // Stadt 1 hat keine Haltestellen: kein Bus, die kuerzere Zeit ist die zu Fuss
        Assert.All(wege, w => Assert.Null(w.Bus));
        var funde = p.PruefenAussen().Where(f => f.Text.Contains("Spielminuten oder länger", StringComparison.Ordinal)).ToList();
        var f = Assert.Single(funde);
        Assert.False(f.Rot);
        Assert.Equal(Texte.LaufzeitLang(lang.Count, lang[0].VonName, lang[0].NachName, lang[0].Minuten!.Value, lang[0].BusHin, lang[0].BusZurueck), f.Text);
        Assert.NotNull(f.X);
        // genau ab 60: ein Weg mit 58 Minuten zaehlt nicht, einer mit 60 schon
        Assert.DoesNotContain(wege.Where(w => w.Minuten < 60), w => lang.Contains(w));
        Assert.All(wege.Where(w => w.Minuten == 60), w => Assert.Contains(w, lang));
    }

    [Fact]
    public void Gegenprobe_AlleUnterSechzig_KeinHinweis()
    {
        // Nicos Teststadt hat keine Spielorte: keine Wege, kein Hinweis
        var p = TorEingangTests.Nico();
        Assert.Empty(Laufzeiten.Pruefen(p.Stand(), D.Inhalt, p.AussenStand));
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Text.Contains("Spielminuten", StringComparison.Ordinal));
    }

    [Fact]
    public void Grenze_GenauSechzig()
    {
        Assert.Equal(60, Laufzeiten.GelbAb);
    }

    [Fact]
    public void WenigerAlsZweiOrte_Leer()
    {
        var p = TorEingangTests.Nico();
        Assert.Empty(Laufzeiten.Rechnen(p.Stand(), D.Inhalt));
        Assert.Null(SpielortTests.Festlegen(p, "wohnung", "zuhause"));
        Assert.Empty(Laufzeiten.Rechnen(p.Stand(), D.Inhalt));
    }

    // ---- Welle A 0.70.0 (Weisung P12-221) ----

    /// <summary>Haltestellen in Nicos Teststadt (an geraden Strassen mit Gehweg, nahe Bank, Amt, Werkstatt, Lindenau, Kaufhaus, Gewerbehof, Elektromarkt).</summary>
    internal static readonly (double X, double Y)[] Halte = [(27, 16.9), (36, 16.4), (61.4, 30), (24, 45.4), (13.9, 11), (42, 45.4), (16, 3)];

    /// <summary>Nicos Teststadt (S 0.43.0) mit allen Spielorten und sieben Haltestellen.</summary>
    internal static Projekt NicoMitBus(bool mitHalte = true)
    {
        var p = TorEingangTests.Nico();
        foreach (var (modell, ort) in SpielortTests.Zuordnung)
        {
            Assert.Null(SpielortTests.Festlegen(p, modell, ort));
        }
        if (mitHalte)
        {
            p.AussenAendern("Haltestellen", a =>
            {
                foreach (var (x, y) in Halte)
                {
                    Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, x, y));
                }
            });
        }
        return p;
    }

    /// <summary>Nicos RC-Kandidat fuer Stadt 1 (12.11.2026, S 0.45.0, 6 Haltestellen, 5 Zebrastreifen, 1 Fussweg).</summary>
    /// <remarks>
    /// W4b (Weisung P12-228): seit S 0.48.0 oeffnet eine Stadt-Datei mit 1 Minute je Schritt; dann lohnt der Bus in dieser
    /// Stadt nirgends mehr. Fuer den Vergleich Werkzeug = Spiel mit Busfahrten behaelt sie ihre Uhr (2 Minuten, wie gesendet).
    /// </remarks>
    internal static Projekt Rc()
    {
        var vorher = Aussen.UhrAltFuerTests;
        Aussen.UhrAltFuerTests = true;
        try
        {
            return Projekt.AusStadtDatei(
                File.ReadAllBytes(Path.Combine(Daten.Wurzel, "tests", "Stadtbau.Tests", "Testdaten", "nico", "rc_S0.45.0.fistadt")), D, Daten.NeueAblage(), "RC");
        }
        finally
        {
            Aussen.UhrAltFuerTests = vorher;
        }
    }

    /// <summary>Das Spiel zu dieser Stadt, gebaut wie im Spiel (Stadt streng, nicht nachsichtig), Figur zu Hause um 7:00 Uhr.</summary>
    private static StadtSpiel SpielZu(StadtFassung st) =>
        new(st.StadtFuer(st.MitKarte(D.Inhalt)), StadtbauSpiel.Probestand(D.Inhalt), Ansicht3D.MitGebaeuden(D.Grafik, st), st.Innenraeume);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NicosTeststadt_JederWeg_WerkzeugGleichSpiel_BeideRichtungen(bool rc)
    {
        var st = (rc ? Rc() : NicoMitBus()).Stand();
        var wege = Laufzeiten.Rechnen(st, D.Inhalt, D.Grafik);
        var spiel = SpielZu(st);
        var orte = spiel.Szene.Orte.Count;
        Assert.Equal(orte * (orte - 1) / 2, wege.Count);
        foreach (var w in wege)
        {
            Assert.Equal(spiel.MinutenVon(w.Von, w.Nach), w.Minuten);
            Assert.Equal(spiel.MinutenVon(w.Nach, w.Von), w.Minuten);
            Assert.Equal(spiel.BusLohntVon(w.Von, w.Nach, spiel.Stadt.Beginn)?.Bus, w.BusHin);
            Assert.Equal(spiel.BusLohntVon(w.Nach, w.Von, spiel.Stadt.Beginn)?.Bus, w.BusZurueck);
            Assert.True(w.BusHin is null || w.BusHin < w.Minuten);
            Assert.True(w.BusZurueck is null || w.BusZurueck < w.Minuten);
        }
        // der Bus lohnt sich auf vielen Wegen, die Linie faehrt im Kreis: hin und zurueck sind nicht immer gleich
        Assert.Contains(wege, w => w.Bus is not null);
        Assert.Contains(wege, w => w.BusHin != w.BusZurueck);
        // sortiert nach der Zeit, die zaehlt; gelb danach
        for (var i = 1; i < wege.Count; i++)
        {
            Assert.True((wege[i - 1].Kuerzer ?? long.MaxValue) >= (wege[i].Kuerzer ?? long.MaxValue));
        }
    }

    [Fact]
    public void NicosTeststadt_OrtslisteImSpiel_GleichDerKarte()
    {
        var st = NicoMitBus().Stand();
        var wege = Laufzeiten.Rechnen(st, D.Inhalt, D.Grafik);
        var spiel = SpielZu(st);
        Assert.Equal(spiel.Stadt.Beginn, spiel.Jetzt.Uhr);
        var hier = spiel.Jetzt.Ort;
        var verglichen = 0;
        foreach (var w in wege.Where(w => w.Von == hier || w.Nach == hier))
        {
            var ziel = w.Von == hier ? w.Nach : w.Von;
            var bus = w.Von == hier ? w.BusHin : w.BusZurueck;
            // genau das, was die Ortsliste neben „Hingehen“ zeigt
            Assert.Equal(bus is { } b ? (w.Minuten!.Value, b) : null, spiel.BusLohnt(ziel));
            Assert.Equal(w.Minuten, spiel.MinutenZuFuss(ziel));
            verglichen++;
        }
        Assert.Equal(spiel.Szene.Orte.Count - 1, verglichen);
    }

    [Fact]
    public void Gegenprobe_OhneBus_WeichtVomSpielAb()
    {
        var st = NicoMitBus().Stand();
        var spiel = SpielZu(st);
        List<Laufzeiten.Weg> alt;
        Laufzeiten.OhneBusAltFuerTests = true;
        try
        {
            alt = Laufzeiten.Rechnen(st, D.Inhalt, D.Grafik);
        }
        finally
        {
            Laufzeiten.OhneBusAltFuerTests = false;
        }
        Assert.All(alt, w => Assert.Null(w.BusHin ?? w.BusZurueck));
        // das Spiel faehrt auf diesen Wegen mit dem Bus, die alte Karte nannte nur „zu Fuß“
        Assert.Contains(alt, w => spiel.BusLohntVon(w.Von, w.Nach, spiel.Stadt.Beginn) is not null);
        // ohne Haltestellen rechnet das Werkzeug keine Szene und zeigt keinen Bus
        Assert.All(Laufzeiten.Rechnen(NicoMitBus(mitHalte: false).Stand(), D.Inhalt, D.Grafik), w => Assert.Null(w.Bus));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NicosTeststadt_DreiStichproben_UhrImSpielGleichDerKarte(bool rc)
    {
        var st = (rc ? Rc() : NicoMitBus()).Stand();
        var wege = Laufzeiten.Rechnen(st, D.Inhalt, D.Grafik);
        // ein neuer Spielstand wie im Spiel (Tag 1, 7:00 Uhr zu Hause; frei sind Zuhause, Bitweiche, Bahnhof, Moebelhaus, Elektromarkt)
        var stadt = st.StadtFuer(st.MitKarte(D.Inhalt));
        var ablauf = new Spielablauf(stadt.Inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        var spiel = new StadtSpiel(stadt, ablauf, Ansicht3D.MitGebaeuden(D.Grafik, st), st.Innenraeume);
        Assert.Equal(stadt.Beginn, spiel.Jetzt.Uhr);
        Laufzeiten.Weg Zeile(string a, string b) => wege.Single(w => w.Von == a && w.Nach == b || w.Von == b && w.Nach == a);
        long? BusKarte(string a, string b) => Zeile(a, b) is { } w && w.Von == a ? w.BusHin : Zeile(a, b).BusZurueck;
        var mitBus = 0;
        // drei Wege zwischen den freien Orten; einer mit dem Bus (der erste, auf dem er lohnt), die anderen zu Fuss
        string[] frei = ["zuhause", "bitweiche", "talheim", "moebelhaus", "elektromarkt"];
        for (var schritt = 0; schritt < 3; schritt++)
        {
            var von = spiel.Jetzt.Ort;
            var ziele = frei.Where(o => o != von).ToList();
            var ziel = mitBus == 0 && ziele.FirstOrDefault(o => spiel.BusLohnt(o) is not null) is { } mitDemBus ? mitDemBus : ziele[schritt % ziele.Count];
            var vorher = spiel.Jetzt.Uhr;
            if (spiel.BusLohnt(ziel) is { } lohnt)
            {
                // die Ortsliste nennt dieselben Minuten wie die Karte (um 7:00 Uhr genau die Karte, sonst dieselbe Rechnung zur Uhrzeit)
                Assert.Equal((lohnt.ZuFuss, lohnt.Bus), spiel.BusLohntVon(von, ziel, vorher));
                if (vorher == stadt.Beginn)
                {
                    Assert.Equal(BusKarte(von, ziel), lohnt.Bus);
                }
                Assert.Equal(StadtGrund.Ok, spiel.MitBusZu(ziel));
                Assert.Equal(lohnt.Bus, spiel.Jetzt.Uhr - vorher);
                mitBus++;
            }
            else
            {
                Assert.Null(BusKarte(von, ziel) is { } && vorher == stadt.Beginn ? "Karte nennt Bus, Spiel nicht" : null);
                Assert.Equal(StadtGrund.Ok, spiel.GeheZu(ziel));
                // die Figur geht den Weg, die Uhr springt um die Minuten der Karte
                Assert.Equal(Zeile(von, ziel).Minuten, spiel.Jetzt.Uhr - vorher);
            }
            spiel.SofortAnkommen();
            Assert.Equal(ziel, spiel.Jetzt.Ort);
        }
        Assert.True(mitBus >= 1, "keine Stichprobe mit dem Bus");
    }

    [Fact]
    public void Fussweg_ZaehltImWegDesSpiels_Gegenprobe()
    {
        // ein gerader Fussweg von Tuer zu Tuer (Lokschuppen und Wohnung liegen in Nicos Teststadt weit auseinander)
        var p = NicoMitBus(mitHalte: false);
        var vorher = Laufzeiten.Rechnen(p.Stand(), D.Inhalt, D.Grafik).Single(w => w.Von == "zuhause" && w.Nach == "lokschuppen" || w.Von == "lokschuppen" && w.Nach == "zuhause");
        var st0 = p.Stand();
        var stadt0 = st0.StadtFuer(st0.MitKarte(D.Inhalt));
        var a = ((List<object?>)SpielDaten.Objekt(stadt0.Zugaenge["zuhause"])["tuer"]!).Select(Py.F).ToArray();
        var b = ((List<object?>)SpielDaten.Objekt(stadt0.Zugaenge["lokschuppen"])["tuer"]!).Select(Py.F).ToArray();
        p.AussenAendern("Fussweg", x => Py.Setzen(Aussen.Stadtdaten(x), Fusswege.Abschnitt,
            new List<object?> { new PyObjekt { { "punkte", new List<object?> { new List<object?> { a[0], a[1] }, new List<object?> { b[0], b[1] } } } } }));
        var st = p.Stand();
        var nachher = Laufzeiten.Rechnen(st, D.Inhalt, D.Grafik).Single(w => w.Von == vorher.Von && w.Nach == vorher.Nach);
        var luft = Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));
        Assert.True(nachher.Minuten < vorher.Minuten, nachher.Minuten + " statt " + vorher.Minuten);
        // hoechstens die Treppenstufen des Rasters (4 Nachbarn) ueber der Luftlinie: 2 Minuten je halbes Feld
        Assert.True(nachher.Minuten <= Math.Ceiling(luft * Math.Sqrt(2) / 0.5 + 2) * 2, nachher.Minuten + " Min. bei " + luft + " Feldern Luftlinie");
        // Gegenprobe: ohne Fusswege im Raster (Stand vor A 0.70.0) bleibt der Umweg
        Stadt.FusswegeAltFuerTests = true;
        try
        {
            Assert.Equal(vorher.Minuten, Laufzeiten.Rechnen(st, D.Inhalt, D.Grafik).Single(w => w.Von == vorher.Von && w.Nach == vorher.Nach).Minuten);
        }
        finally
        {
            Stadt.FusswegeAltFuerTests = false;
        }
    }

    [Fact]
    public void Zeile_UndHinweis_NennenBeideZeiten()
    {
        Assert.Equal("A – B: zu Fuß 120 Min.", Texte.Laufzeit("A", "B", 120));
        Assert.Equal("A – B: zu Fuß 120 Min., mit dem Bus 50 Min.", Texte.Laufzeit("A", "B", 120, 50, 50));
        Assert.Equal("A – B: zu Fuß 120 Min., mit dem Bus hin 50 Min., zurück nicht schneller", Texte.Laufzeit("A", "B", 120, 50, null));
        Assert.Equal("A – B: kein Weg", Texte.Laufzeit("A", "B", null));
        var w = new Laufzeiten.Weg("a", "b", "A", "B", 120, 50, 70);
        Assert.Equal(70, w.Kuerzer);
        Assert.Equal(70, w.Bus);
        Assert.Null(new Laufzeiten.Weg("a", "b", "A", "B", 120, 50, null).Bus);
        Assert.Equal(120, new Laufzeiten.Weg("a", "b", "A", "B", 120, 50, null).Kuerzer);
        // gelb nach der Zeit, die zaehlt: 120 Min. zu Fuss, 50 mit dem Bus in beide Richtungen = kein Hinweis
        Assert.Empty(Laufzeiten.Pruefen([new Laufzeiten.Weg("a", "b", "A", "B", 120, 50, 50)], TorEingangTests.Nico().AussenStand));
        var f = Assert.Single(Laufzeiten.Pruefen([new Laufzeiten.Weg("a", "b", "A", "B", 120, 70, 65)], TorEingangTests.Nico().AussenStand));
        Assert.EndsWith("zu Fuß 120 Minuten, mit dem Bus hin 70 Minuten, zurück 65 Minuten.", f.Text, StringComparison.Ordinal);
    }
}
