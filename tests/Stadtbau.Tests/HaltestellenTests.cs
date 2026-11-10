using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle A 0.61.0 / S 0.36.0 (Weisung P12-171, Kurzplan P12-102, Nico 1a 2a 3a 4b 5b): Haltestellen und Parkplaetze im
/// Werkzeug (setzen an eine gerade Strasse mit Gehweg, Seite bleibt, nur laengs schieben, mit der Strasse schieben und
/// loeschen), im Spiel (Bus faehrt alle Haltestellen rechts an, haelt 4 Sekunden, Auto dahinter wartet, wartende Figur
/// verschwindet und kommt wieder, voller Parkplatz in den Grenzen) und die Pruefregeln je rot und gruen.
/// Grundlage: leere Stadt 24 × 16, Ring aus vier Strassen mit Gehweg 0,35: (2; 4)–(22; 4)–(22; 12)–(2; 12)–(2; 4).
/// </summary>
public class HaltestellenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static List<object?> P(double x, double y) => [x, y];

    private static PyObjekt Grundlage()
    {
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_halt", 24, 16));
        void Strasse(double x1, double y1, double x2, double y2) =>
            Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(x1, y1), P(x2, y2) } }, { "gehweg", 0.35 } });
        Strasse(2, 4, 22, 4);
        Strasse(22, 4, 22, 12);
        Strasse(22, 12, 2, 12);
        Strasse(2, 12, 2, 4);
        return a;
    }

    private static AussenStelle H(int i) => new(AussenArt.Haltestelle, i);

    private static AussenStelle Pp(int i) => new(AussenArt.Parkplatz, i);

    private static StadtFassung Fassung(PyObjekt a) => Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", "");

    private static List<StadtbauFund> Pruefen(PyObjekt a) => StadtbauPruefung.Aussen(Fassung(a), D.Inhalt, D.Grafik);

    private static string Text(List<StadtbauFund> f) => string.Join(" | ", f.Select(x => (x.Rot ? "rot: " : "gelb: ") + x.Text));

    private static void Gruen(PyObjekt a)
    {
        var f = Pruefen(a);
        Assert.True(!f.Any(x => x.Rot), Text(f));
    }

    private static void Rot(PyObjekt a, string teil)
    {
        var f = Pruefen(a);
        Assert.True(f.Any(x => x.Rot && x.Text.Contains(teil, StringComparison.Ordinal)), "erwartet rot „" + teil + "“, gefunden: " + Text(f));
    }

    private static StadtSzene Szene(PyObjekt a) => Ansicht3D.Bauen(Fassung(a), D, Ansicht3D.Einstellung.Standard).Szene;

    private static Vector2 Punkt(PyObjekt a, AussenStelle s) =>
        Zebrastreifen.Lesen(AussenBearbeitung.HaltListe(a, s.Art)[s.Nummer])!.Value;

    // ---- Werkzeug ------------------------------------------------------------------------------------

    [Fact]
    public void Setzen_AnDieStrasseMitGehweg_SeiteBleibt()
    {
        var a = Grundlage();
        // innen am Ring unten (y 4, Seite +y) und aussen (Seite -y)
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 10, 4.9));
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 16, 3.2));
        var innen = AussenBearbeitung.HaltestelleVon(a, H(0))!;
        var aussen = AussenBearbeitung.HaltestelleVon(a, H(1))!;
        Assert.Equal(new Vector2(10, 4), innen.Mitte);
        Assert.Equal(new Vector2(0, 1), innen.Aussen);
        Assert.Equal(new Vector2(0, -1), aussen.Aussen);
        // der Bus faehrt so, dass die Haltestelle rechts liegt: entgegengesetzte Richtungen
        Assert.Equal(-innen.Fahrt, aussen.Fahrt);
        // gespeichert wird der Punkt auf dem Gehweg der Seite
        Assert.Equal(new Vector2(10, 4.5f), Punkt(a, H(0)));
        // fern jeder Strasse, an einer Strasse ohne Gehweg: nichts gesetzt
        Assert.Null(AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 12, 8));
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(6, 14), P(18, 14) } } });
        Assert.Null(AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 10, 14.5));
        Assert.Equal(2, AussenBearbeitung.HaltListe(a, AussenArt.Haltestelle).Count);
        Assert.Equal(Texte.HaltDrehenNicht, AussenBearbeitung.DrehenNicht(H(0)));
    }

    [Fact]
    public void Verschieben_NurLaengs_UndMitDerStrasse_UndLoeschen()
    {
        var a = Grundlage();
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 10, 4.9);
        AussenBearbeitung.Setzen(a, AussenArt.Parkplatz, 14, 4.9);
        AussenBearbeitung.Verschieben(a, H(0), 2, 3);
        Assert.Equal(new Vector2(12, 4), AussenBearbeitung.HaltestelleVon(a, H(0))!.Mitte);
        // die Strasse nach oben: beide wandern mit
        AussenBearbeitung.Verschieben(a, new AussenStelle(AussenArt.Strasse, 0), 0, 1);
        Assert.Equal(new Vector2(12, 5), AussenBearbeitung.HaltestelleVon(a, H(0))!.Mitte);
        var pv = AussenBearbeitung.ParkplatzVon(a, Pp(0))!;
        Assert.Equal(5f, (pv.Vorn - (pv.Aussen * pv.Halb)).Y, 3);
        // Kopie laengs daneben
        var k = AussenBearbeitung.Kopieren(a, H(0));
        Assert.NotNull(k);
        Assert.Equal(new Vector2(15, 5), AussenBearbeitung.HaltestelleVon(a, k!)!.Mitte);
        // die Strasse loeschen: alle drei fallen weg, die Abschnitte auch
        Assert.True(AussenBearbeitung.Loeschen(a, new AussenStelle(AussenArt.Strasse, 0)));
        Assert.False(Py.Hat(Aussen.Stadtdaten(a), Haltestellen.Abschnitt));
        Assert.False(Py.Hat(Aussen.Stadtdaten(a), Parkplaetze.Abschnitt));
    }

    [Fact]
    public void Parkplatz_PlaetzeUndReihen()
    {
        var a = Grundlage();
        AussenBearbeitung.Setzen(a, AussenArt.Parkplatz, 12, 4.9);
        var pp = AussenBearbeitung.ParkplatzVon(a, Pp(0))!;
        Assert.Equal((AussenBearbeitung.ParkplatzPlaetzeNeu, AussenBearbeitung.ParkplatzReihenNeu), (pp.Plaetze, pp.Reihen));
        Assert.True(AussenBearbeitung.ParkplatzAendern(a, Pp(0), 12, 2));
        pp = AussenBearbeitung.ParkplatzVon(a, Pp(0))!;
        Assert.Equal(12, pp.Stellplaetze().Count());
        Assert.False(AussenBearbeitung.ParkplatzAendern(a, Pp(0), 13, 2));
        Assert.False(AussenBearbeitung.ParkplatzAendern(a, Pp(0), 1, 1));
        Assert.False(AussenBearbeitung.ParkplatzAendern(a, Pp(0), 6, 3));
        Gruen(a);
    }

    [Fact]
    public void Auswahl_UndStueckBei()
    {
        var a = Grundlage();
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 10, 4.9);
        AussenBearbeitung.Setzen(a, AussenArt.Parkplatz, 16, 4.9);
        var h = AussenBearbeitung.HaltestelleVon(a, H(0))!;
        Assert.Equal(H(0), AussenBearbeitung.StueckBei(a, h.Haeuschen.X, h.Haeuschen.Y));
        var pp = AussenBearbeitung.ParkplatzVon(a, Pp(0))!;
        var mitte = pp.Punkt(0, pp.Tiefe / 2);
        Assert.Equal(Pp(0), AussenBearbeitung.StueckBei(a, mitte.X, mitte.Y));
    }

    // ---- Spiel ---------------------------------------------------------------------------------------

    [Fact]
    public void Szene_HaeuschenSperrt_ParkplatzVollImWerkzeug()
    {
        var a = Grundlage();
        var ohne = Szene(a);
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 10, 4.9);
        AussenBearbeitung.Setzen(a, AussenArt.Parkplatz, 16, 4.9);
        AussenBearbeitung.ParkplatzAendern(a, Pp(0), 12, 1);
        var szene = Szene(a);
        Assert.Single(szene.Haltestellen);
        Assert.Single(szene.Parkplaetze);
        var h = szene.Haltestellen[0];
        Assert.False(szene.Laufraster.KannStehen(h.Haeuschen.X, h.Haeuschen.Y), "im Haeuschen kann die Figur stehen");
        // der volle Parkplatz (Nico P12-170: 12 Plaetze, alle belegt) steht als 12 Autos in der Stadt und zaehlt in den Grenzen
        var autos = szene.Stuecke.Count(s => Verkehr.Strecken.Any(st => st.Modell == s.Name)) - ohne.Stuecke.Count(s => Verkehr.Strecken.Any(st => st.Modell == s.Name));
        Assert.Equal(12, autos);
        var mit = GrenzenLive.Fuer(Fassung(a), D).Gesamt;
        AussenBearbeitung.Loeschen(a, Pp(0));
        var vorher = GrenzenLive.Fuer(Fassung(a), D).Gesamt;
        var autoDreiecke = szene.Parkplaetze[0].Stellplaetze().Select((_, i) => Parkplaetze.Belegung(1, i, 1, true)!).Sum(m => (long)D.Grafik!.Modell(m)!.Dreiecke);
        // die Unterseiten der Autos fallen wie jede Unterseite weg (D1), alles andere zaehlt
        Assert.True(mit.Dreiecke - vorher.Dreiecke >= autoDreiecke * 0.9, $"Grenzen zaehlen den vollen Parkplatz nicht: {mit.Dreiecke} - {vorher.Dreiecke} < {autoDreiecke}");
    }

    private static (StadtSzene Szene, Verkehr Verkehr) MitBus(int haltestellen = 2)
    {
        var a = Grundlage();
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 10, 4.9);
        if (haltestellen >= 2)
        {
            AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 12, 11.1);
        }
        var szene = Szene(a);
        return (szene, Verkehr.Fuer(szene));
    }

    [Fact]
    public void Bus_FaehrtAlleHaltestellenRechtsAn_UndHaeltVierSekunden()
    {
        var (szene, v) = MitBus();
        var bus = Assert.Single(v.Autos, x => x.Nummer == Buslinie.ErsteNummer);
        Assert.Equal(Buslinie.Modell, bus.Modell);
        Assert.Equal(2, bus.Halte.Count);
        // jeder Halt liegt in der Fahrspur vor der Haltestelle (Front auf Hoehe des Schilds)
        foreach (var (front, nr) in bus.Halte)
        {
            var h = szene.Haltestellen.First(x => x.Nr == nr);
            Assert.True(Vector2.Distance(bus.Lage(front).Ort, h.Halt) < Buslinie.HaltNah + 0.01f, $"Halt {nr} liegt {Vector2.Distance(bus.Lage(front).Ort, h.Halt)} neben der Haltestelle");
            Assert.True(Vector2.Dot(bus.Lage(front).Richtung, h.Fahrt) > 0.99f, "Bus faehrt nicht mit der Haltestelle rechts");
        }
        // Halten zaehlen: zwei Halte je Umlauf, jeder rund 4 Sekunden
        var halte = new List<(int Nr, double Dauer)>();
        var (an, seit) = ((int?)null, 0.0);
        const double schritt = 1.0 / 20;
        for (var t = 0; t < 120 * 20; t++)
        {
            v.Weiter(schritt, -1000, -1000);
            if (bus.HaeltAn is { } nr && bus.HaltRest > 0)
            {
                (an, seit) = an == nr ? (an, seit + schritt) : (nr, schritt);
            }
            else if (an is { } fertig)
            {
                halte.Add((fertig, seit));
                an = null;
            }
        }
        Assert.Contains(halte, x => x.Nr == 1);
        Assert.Contains(halte, x => x.Nr == 2);
        Assert.All(halte, x => Assert.InRange(x.Dauer, Buslinie.HaltSekunden - 0.1, Buslinie.HaltSekunden + 0.1));
    }

    [Fact]
    public void Wartende_VerschwindetBeimHalt_KommtWieder_Gegenprobe()
    {
        var (_, v) = MitBus(haltestellen: 1);
        var w = Assert.Single(v.Wartende);
        var bus = v.Autos.First(x => x.Nummer == Buslinie.ErsteNummer);
        Assert.True(v.Sichtbar(w));
        const double schritt = 1.0 / 20;
        double? weg = null;
        double? wieder = null;
        for (var t = 0; t < 200 * 20 && wieder is null; t++)
        {
            v.Weiter(schritt, -1000, -1000);
            if (weg is null && !v.Sichtbar(w))
            {
                weg = v.Zeit;
                Assert.Equal(1, bus.HaeltAn);   // verschwindet genau, wenn der Bus an ihrer Haltestelle haelt
            }
            else if (weg is not null && v.Sichtbar(w))
            {
                wieder = v.Zeit;
            }
        }
        Assert.NotNull(weg);
        Assert.NotNull(wieder);
        Assert.InRange(wieder!.Value - weg!.Value, Verkehr.WiederkommenSekunden - 0.1, Verkehr.WiederkommenSekunden + 0.1);
        // Gegenprobe: ohne Bus bleibt die Figur immer sichtbar
        var (_, ohneBus) = MitBus(haltestellen: 1);
        ohneBus.Autos.RemoveAll(x => x.Nummer >= Buslinie.ErsteNummer);
        for (var t = 0; t < 120 * 20; t++)
        {
            ohneBus.Weiter(schritt, -1000, -1000);
            Assert.True(ohneBus.Sichtbar(ohneBus.Wartende[0]), "ohne Bus verschwindet die Figur");
        }
    }

    [Fact]
    public void Wartende_StehtImHaeuschen_NieAufDerStrasse()
    {
        var (szene, v) = MitBus();
        foreach (var w in v.Wartende)
        {
            var h = szene.Haltestellen.First(x => x.Nr == w.Haltestelle);
            Assert.True(Wasser.Drin(h.HaeuschenEcken(), w.Ort), "Figur steht nicht im Haeuschen");
            Assert.True(szene.Strassen.All(s => Kurve.Abstand(w.Ort, s) > h.Halb), "Figur steht auf Strasse oder Gehweg");
        }
    }

    [Fact]
    public void AutoHinterDemBus_Wartet()
    {
        var (_, v) = MitBus(haltestellen: 1);
        var bus = v.Autos.First(x => x.Nummer == Buslinie.ErsteNummer);
        var (front, _) = bus.Halte[0];
        // ein Auto auf derselben Spur kurz hinter dem Halt
        var auto = new Verkehr.Auto("car-taxi", bus.Strecke, 0) { Schnell = true, Weg = (front - Buslinie.Halb - 2.5f + bus.Laenge) % bus.Laenge };
        v.Autos.Add(auto);
        bus.Weg = (front - Buslinie.Halb - 0.3f + bus.Laenge) % bus.Laenge;
        const double schritt = 1.0 / 20;
        for (var t = 0; t < 3 * 20; t++)
        {
            v.Weiter(schritt, -1000, -1000);
        }
        Assert.True(bus.HaltRest > 0, "Bus haelt nicht");
        // Abstand Front des Autos zum Heck des Busses: nie in den Bus hinein
        var luecke = ((bus.Weg - auto.Weg + bus.Laenge) % bus.Laenge) - Buslinie.Halb - Verkehr.Front;
        Assert.True(auto.Haelt, "Auto hinter dem haltenden Bus faehrt weiter");
        Assert.True(luecke > 0.05f, $"Auto faehrt in den Bus: Luecke {luecke}");
    }

    [Fact]
    public void ZweiBusse_AbSiebenHaltestellen()
    {
        var a = Grundlage();
        for (var i = 0; i < 6; i++)
        {
            AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 4 + (3 * i), 4.9);
        }
        Assert.Single(Verkehr.Fuer(Szene(a)).Autos, x => x.Nummer >= Buslinie.ErsteNummer);
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 12, 11.1);
        var busse = Verkehr.Fuer(Szene(a)).Autos.Where(x => x.Nummer >= Buslinie.ErsteNummer).ToList();
        Assert.Equal(2, busse.Count);
        var halber = busse[0].Laenge / 2;
        var abstand = ((busse[1].Weg - busse[0].Weg) + busse[0].Laenge) % busse[0].Laenge;
        Assert.InRange(abstand, halber - 0.01f, halber + 0.01f);
    }

    [Fact]
    public void OhneHaltestelle_KeinBus_KeineFigur()
    {
        var v = Verkehr.Fuer(Szene(Grundlage()));
        Assert.DoesNotContain(v.Autos, x => x.Nummer >= Buslinie.ErsteNummer);
        Assert.Empty(v.Wartende);
    }

    // ---- Pruefung ------------------------------------------------------------------------------------

    [Fact]
    public void Pruefung_Gruen_MitHaltestellenUndParkplatz()
    {
        var a = Grundlage();
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 10, 4.9);
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 12, 11.1);
        AussenBearbeitung.Setzen(a, AussenArt.Parkplatz, 16, 4.9);
        Gruen(a);
    }

    private static PyObjekt MitEintrag(string abschnitt, PyObjekt eintrag)
    {
        var a = Grundlage();
        Py.Setzen(Aussen.Stadtdaten(a), abschnitt, new List<object?> { eintrag });
        return a;
    }

    [Fact]
    public void Regel_FalschGespeichert_AusserhalbDerKarte_KeineStrasse()
    {
        Rot(MitEintrag(Haltestellen.Abschnitt, new PyObjekt { { "x", "a" } }), "Haltestelle Nr. 1 ist falsch gespeichert");
        Rot(MitEintrag(Haltestellen.Abschnitt, new PyObjekt { { "x", 30.0 }, { "y", 4.5 } }), "Haltestelle Nr. 1 liegt außerhalb der Karte");
        Rot(MitEintrag(Haltestellen.Abschnitt, new PyObjekt { { "x", 12.0 }, { "y", 8.0 } }), "Haltestelle Nr. 1 steht an keiner geraden Straße mit Gehweg");
        Rot(MitEintrag(Parkplaetze.Abschnitt, new PyObjekt { { "x", 12.0 }, { "y", 4.5 } }), "Parkplatz Nr. 1 ist falsch gespeichert");
        Rot(MitEintrag(Parkplaetze.Abschnitt, new PyObjekt { { "x", 12.0 }, { "y", 4.5 }, { "plaetze", 13L }, { "reihen", 1L } }), "Parkplatz Nr. 1 hat 13 Plätze");
        Rot(MitEintrag(Parkplaetze.Abschnitt, new PyObjekt { { "x", 12.0 }, { "y", 8.0 }, { "plaetze", 6L }, { "reihen", 1L } }), "Parkplatz Nr. 1 liegt an keiner geraden Straße mit Gehweg");
        Gruen(MitEintrag(Parkplaetze.Abschnitt, new PyObjekt { { "x", 12.0 }, { "y", 4.5 }, { "plaetze", 12L }, { "reihen", 2L } }));
    }

    [Fact]
    public void Regel_NahAnKreuzung_RotUndGruen()
    {
        var a = Grundlage();
        // Kreuzung (12; 4) durch eine Strasse nach unten
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(12, 4), P(12, 0.5) } }, { "gehweg", 0.35 } });
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 13, 4.9);
        Rot(a, "zu nah an einer Kreuzung");
        AussenBearbeitung.Verschieben(a, H(0), 4, 0);
        var f = Pruefen(a);
        Assert.DoesNotContain(f, x => x.Text.Contains("zu nah an einer Kreuzung", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel_NahAnZebrastreifen_RotUndGruen()
    {
        var a = Grundlage();
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 12, 4.9);
        AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 13, 4);
        Rot(a, "zu nah an einem Zebrastreifen");
        AussenBearbeitung.Verschieben(a, new AussenStelle(AussenArt.Zebrastreifen, 0), 4, 0);
        Assert.DoesNotContain(Pruefen(a), x => x.Text.Contains("zu nah an einem Zebrastreifen", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel_ZweiAufDerselbenSeite_RotUndGruen()
    {
        var a = Grundlage();
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 10, 4.9);
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 12, 4.9);
        Rot(a, "zu nah an Haltestelle Nr. 1 auf derselben Seite");
        AussenBearbeitung.Verschieben(a, H(1), 2, 0);
        Assert.DoesNotContain(Pruefen(a), x => x.Text.Contains("auf derselben Seite", StringComparison.Ordinal));
        // gegenueber darf eine stehen (andere Seite)
        var b = Grundlage();
        AussenBearbeitung.Setzen(b, AussenArt.Haltestelle, 10, 4.9);
        AussenBearbeitung.Setzen(b, AussenArt.Haltestelle, 11, 3.1);
        Assert.DoesNotContain(Pruefen(b), x => x.Text.Contains("auf derselben Seite", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel_PlatzNichtFrei_RotUndGruen()
    {
        var a = Grundlage();
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 10, 4.9);
        var h = AussenBearbeitung.HaltestelleVon(a, H(0))!;
        AussenBearbeitung.Setzen(a, AussenArt.Baum, h.Haeuschen.X, h.Haeuschen.Y);
        Rot(a, "Wartehäuschens von Haltestelle Nr. 1 ist nicht frei");
        var b = Grundlage();
        AussenBearbeitung.Setzen(b, AussenArt.Haltestelle, 10, 4.9);
        AussenBearbeitung.Setzen(b, AussenArt.Parkplatz, 10, 4.9);
        Rot(b, "ist nicht frei: dort ist Haltestelle Nr. 1");
        AussenBearbeitung.Verschieben(b, Pp(0), 5, 0);
        Assert.DoesNotContain(Pruefen(b), x => x.Text.Contains("nicht frei", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel_EinfahrtNahAnHaltestelle_RotUndGruen()
    {
        var a = Grundlage();
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 10, 4.9);
        AussenBearbeitung.Setzen(a, AussenArt.Parkplatz, 8, 3.1);
        Rot(a, "Die Einfahrt von Parkplatz Nr. 1 liegt zu nah");
        AussenBearbeitung.Verschieben(a, Pp(0), -3, 0);
        Assert.DoesNotContain(Pruefen(a), x => x.Text.Contains("Einfahrt", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel_BusErreichtNicht_RotUndGruen()
    {
        // eine Sackgasse ohne Gegenrichtung gibt es nicht; nicht erreichbar ist eine Haltestelle an einer Strasse ohne
        // Anschluss an das Netz des Busses
        var a = Grundlage();
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 10, 4.9);
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(6, 14.5), P(18, 14.5) } }, { "gehweg", 0.35 } });
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 12, 15.1);
        Rot(a, "Der Bus erreicht Haltestelle Nr. 2 nicht");
        AussenBearbeitung.Loeschen(a, H(1));
        Gruen(a);
    }

    [Fact]
    public void Regel_MehrAlsAcht_Rot()
    {
        var a = Grundlage();
        foreach (var x in new[] { 4.0, 8, 12, 16, 20 })
        {
            AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, x, 4.9);
        }
        foreach (var x in new[] { 6.0, 12, 18 })
        {
            AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, x, 11.1);
        }
        Assert.DoesNotContain(Pruefen(a), x => x.Text.Contains("höchstens", StringComparison.Ordinal));
        AussenBearbeitung.Setzen(a, AussenArt.Haltestelle, 21.1, 8);
        Rot(a, "Es gibt 9 Haltestellen");
    }

    [Fact]
    public void Fassung0_OhneBusUndOhneFigur()
    {
        var szene = Ansicht3D.Bauen(D.Fassung, D, Ansicht3D.Einstellung.Standard).Szene;
        Assert.Empty(szene.Haltestellen);
        Assert.Empty(szene.Parkplaetze);
        var v = Verkehr.Fuer(szene);
        Assert.DoesNotContain(v.Autos, x => x.Nummer >= Buslinie.ErsteNummer);
        Assert.Empty(v.Wartende);
    }
}
