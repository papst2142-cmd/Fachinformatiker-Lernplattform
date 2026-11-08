using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Phase 13 Welle 5 „Etagen“ (Weisung P12-73, Plan P12-23b, Nico 1a 2b 3a 4a): Bearbeitung der Etagen und Uebergaenge im
/// Werkzeug und die Pruefregeln des gemeinsamen Teils (je Regel rot und gruen, mit Gegenprobe).
/// </summary>
public class EtagenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static readonly DateTime Zeit = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Local);

    private static string Json(object? o) => PythonJson.Standard(o);

    /// <summary>Grundriss des Moebelhauses (Kopie aus Stadt 1, ohne Etagen).</summary>
    private static PyObjekt Eg() => Daten.Kopie(InnenGrundriss.Grundriss(D.Fassung.Innenraeume, "moebelhaus"));

    /// <summary>Treppe im Flur (vorn nach +x), Stelle im Flur.</summary>
    private static Stelle TreppeSetzen(PyObjekt g, string typ = Etagen.Treppe, double y = 12.2)
    {
        var s = Bearbeitung.Setzen(g, typ, 9, 13)!;
        var d = Bearbeitung.Deko(g, s);
        var (w, h) = typ == Etagen.Treppe ? (3.0, 1.6) : (2.2, 2.2);
        Py.Setzen(d, "x", 8.0);
        Py.Setzen(d, "y", typ == Etagen.Treppe ? y : 12.8);
        Py.Setzen(d, "w", w);
        Py.Setzen(d, "h", h);
        Py.Setzen(d, "dreh", 3L);
        return s;
    }

    /// <summary>Moebelhaus mit <paramref name="etagen"/> Obergeschossen und einer Treppe EG ↔ 1. OG (gruen).</summary>
    private static PyObjekt MitTreppe(int etagen = 1)
    {
        var eg = Eg();
        for (var k = 0; k < etagen; k++)
        {
            EtagenBearbeitung.Hinzufuegen(eg);
        }
        var s = TreppeSetzen(eg);
        Assert.True(EtagenBearbeitung.ZielSetzen(eg, 0, s, 1));
        EtagenBearbeitung.Aufraeumen(eg);
        return eg;
    }

    private static List<string> Rot(PyObjekt eg) =>
        [.. StadtbauPruefung.Etagen("moebelhaus", eg, D.Grafik, null).Where(f => f.Fund.Rot).Select(f => f.Fund.Text)];

    [Fact]
    public void Hinzufuegen_HoechstensVierObergeschosse()
    {
        var eg = Eg();
        for (var k = 1; k <= Etagen.Hoechstens; k++)
        {
            Assert.Equal(k, EtagenBearbeitung.Hinzufuegen(eg));
        }
        Assert.Equal(-1, EtagenBearbeitung.Hinzufuegen(eg));
        Assert.Equal(4, Etagen.Anzahl(eg));
        // Vorschlag: Aussenwaende wie das Erdgeschoss, keine Eingangstuer
        var oben = Etagen.Gebaeude(eg, 1);
        Assert.Equal(Json(eg["breite"]), Json(oben["breite"]));
        Assert.Equal(Json(eg["hoehe"]), Json(oben["hoehe"]));
        Assert.Null(Get(oben, "eingang"));
    }

    [Fact]
    public void Regel_HoechstensVier_RotUndGruen()
    {
        var eg = MitTreppe(4);
        Assert.DoesNotContain(Rot(eg), t => t.Contains("mehr als 4 gehen nicht", StringComparison.Ordinal));
        // Gegenprobe: eine fuenfte Etage von Hand (das Werkzeug legt sie nicht an)
        Liste(eg[Etagen.FeldEtagen]).Add(new PyObjekt { { "gebaeude", Etagen.Vorschlag(eg) } });
        Assert.Contains(Rot(eg), t => t.Contains("mehr als 4 gehen nicht", StringComparison.Ordinal));
    }

    [Fact]
    public void Umbenennen_UndStandardname()
    {
        var eg = Eg();
        EtagenBearbeitung.Hinzufuegen(eg);
        Assert.Equal("1. Obergeschoss", Etagen.Name(eg, 1));
        EtagenBearbeitung.Umbenennen(eg, 1, "  Galerie ");
        Assert.Equal("Galerie", Etagen.Name(eg, 1));
        EtagenBearbeitung.Umbenennen(eg, 1, "");
        Assert.Equal("1. Obergeschoss", Etagen.Name(eg, 1));
        Assert.Null(Get(Objekt(Etagen.Liste(eg)[0]), "name"));
    }

    [Fact]
    public void Loeschen_NichtDasErdgeschoss_NichtMitUebergang()
    {
        var eg = MitTreppe(2);
        Assert.False(EtagenBearbeitung.Loeschen(eg, 0));
        Assert.False(EtagenBearbeitung.Loeschen(eg, 1));   // die Treppe fuehrt hierher
        Assert.True(EtagenBearbeitung.Loeschen(eg, 2));
        Assert.Equal(1, Etagen.Anzahl(eg));
        Assert.Empty(Rot(eg));
    }

    [Fact]
    public void ZielSetzen_Treppe_GegenstueckUndAnkunft_NeuesZielErsetzt()
    {
        var eg = Eg();
        EtagenBearbeitung.Hinzufuegen(eg);
        EtagenBearbeitung.Hinzufuegen(eg);
        var s = TreppeSetzen(eg);
        // Treppe: nur zur Nachbar-Etage
        Assert.Equal([1], EtagenBearbeitung.MoeglicheZiele(eg, 0, Bearbeitung.Deko(eg, s)));
        Assert.False(EtagenBearbeitung.ZielSetzen(eg, 0, s, 2));
        Assert.True(EtagenBearbeitung.ZielSetzen(eg, 0, s, 1));
        var u = Etagen.Von(eg, "moebelhaus", "u1")!;
        Assert.Equal([0, 1], u.Halte.Select(h => h.Etage).Order());
        var oben = Etagen.Stuecke(Etagen.Gebaeude(eg, 1), "u1");
        Assert.Single(oben);
        Assert.Equal((8.0, 12.2), (Py.F(oben[0]["x"]), Py.F(oben[0]["y"])));
        // Ankunft einen Schritt vor dem Stueck (vorn nach +x)
        Assert.Equal((11.6, 13.0, 90L), (u.Auf(1)!.X, u.Auf(1)!.Y, (long)u.Auf(1)!.Blick));
        Assert.Equal(["Das 2. Obergeschoss ist vom Erdgeschoss aus nicht erreichbar."], Rot(eg));
        // vom 1. OG aus nach oben: das neue Ziel ersetzt das alte (Gegenstueck im EG faellt weg)
        var obenStelle = Bearbeitung.Alle(Etagen.Gebaeude(eg, 1)).Single(a => Get(a.Deko, Etagen.FeldLink) as string == "u1").Stelle;
        Assert.True(EtagenBearbeitung.ZielSetzen(eg, 1, obenStelle, 2));
        EtagenBearbeitung.Aufraeumen(eg);
        u = Etagen.Von(eg, "moebelhaus", "u1")!;
        Assert.Equal([1, 2], u.Halte.Select(h => h.Etage).Order());
        Assert.Empty(Etagen.Stuecke(eg, "u1"));
    }

    [Fact]
    public void ZielSetzen_Fahrstuhl_ZieleAnUndAbwaehlen()
    {
        var eg = Eg();
        for (var k = 0; k < 3; k++)
        {
            EtagenBearbeitung.Hinzufuegen(eg);
        }
        var s = TreppeSetzen(eg, Etagen.Fahrstuhl);
        Assert.Equal([1, 2, 3], EtagenBearbeitung.MoeglicheZiele(eg, 0, Bearbeitung.Deko(eg, s)));
        Assert.True(EtagenBearbeitung.ZielSetzen(eg, 0, s, 1));
        Assert.True(EtagenBearbeitung.ZielSetzen(eg, 0, s, 3));
        Assert.Equal([1, 3], EtagenBearbeitung.Ziele(eg, 0, Bearbeitung.Deko(eg, s)).Order());
        Assert.Contains(Rot(eg), t => t.Contains("2. Obergeschoss", StringComparison.Ordinal));   // nicht erreichbar
        Assert.True(EtagenBearbeitung.ZielSetzen(eg, 0, s, 2));
        Assert.Empty(Rot(eg));
        // nochmal 3 = abwaehlen, das Stueck dort faellt weg
        Assert.True(EtagenBearbeitung.ZielSetzen(eg, 0, s, 3));
        Assert.Empty(Etagen.Stuecke(Etagen.Gebaeude(eg, 3), "u1"));
        Assert.Equal([1, 2], EtagenBearbeitung.Ziele(eg, 0, Bearbeitung.Deko(eg, s)).Order());
    }

    [Fact]
    public void Aufraeumen_TreppeWeg_GanzerUebergangWeg_FahrstuhlNurDerHalt()
    {
        var eg = MitTreppe();
        var s = Bearbeitung.Alle(eg).Single(a => Get(a.Deko, Etagen.FeldLink) is string).Stelle;
        Bearbeitung.Loeschen(eg, s);
        EtagenBearbeitung.Aufraeumen(eg);
        Assert.Null(Get(eg, Etagen.FeldUebergaenge));
        Assert.Empty(Etagen.Stuecke(Etagen.Gebaeude(eg, 1), "u1"));

        var f = Eg();
        EtagenBearbeitung.Hinzufuegen(f);
        EtagenBearbeitung.Hinzufuegen(f);
        var fs = TreppeSetzen(f, Etagen.Fahrstuhl);
        EtagenBearbeitung.ZielSetzen(f, 0, fs, 1);
        EtagenBearbeitung.ZielSetzen(f, 0, fs, 2);
        var oben = Bearbeitung.Alle(Etagen.Gebaeude(f, 2)).Single(a => Get(a.Deko, Etagen.FeldLink) is string).Stelle;
        Bearbeitung.Loeschen(Etagen.Gebaeude(f, 2), oben);
        EtagenBearbeitung.Aufraeumen(f);
        Assert.Equal([0, 1], Etagen.Von(f, "moebelhaus", "u1")!.Halte.Select(h => h.Etage).Order());
    }

    [Fact]
    public void Ankunft_WandertMitDemStueck()
    {
        var eg = MitTreppe();
        var s = Bearbeitung.Alle(eg).Single(a => Get(a.Deko, Etagen.FeldLink) is string).Stelle;
        var vorher = Etagen.Von(eg, "moebelhaus", "u1")!.Auf(0)!;
        Bearbeitung.Verschieben(eg, s, -2, 0);
        EtagenBearbeitung.Aufraeumen(eg);
        var nachher = Etagen.Von(eg, "moebelhaus", "u1")!.Auf(0)!;
        Assert.Equal(vorher.X - 2, nachher.X, 6);
        Assert.Equal(vorher.Y, nachher.Y, 6);
        // Gegenprobe: das Stueck oben bleibt, wo es war, mit seiner Ankunft
        Assert.Equal(11.6, Etagen.Von(eg, "moebelhaus", "u1")!.Auf(1)!.X, 6);
    }

    [Fact]
    public void Ankunft_ZiehenUndBlickDrehen()
    {
        var eg = MitTreppe();
        Assert.True(EtagenBearbeitung.AnkunftSetzen(eg, "u1", 0, 12.004, 13.5));
        Assert.True(EtagenBearbeitung.BlickDrehen(eg, "u1", 0));
        var h = Etagen.Von(eg, "moebelhaus", "u1")!.Auf(0)!;
        Assert.Equal((12.0, 13.5, 180.0), (h.X, h.Y, h.Blick));
        Assert.False(EtagenBearbeitung.AnkunftSetzen(eg, "u9", 0, 1, 1));
        Assert.Single(EtagenBearbeitung.Ankuenfte(eg, 0));
    }

    [Fact]
    public void Kopieren_OhneVerweis()
    {
        var eg = MitTreppe();
        var s = Bearbeitung.Alle(eg).Single(a => Get(a.Deko, Etagen.FeldLink) is string).Stelle;
        var kopie = Bearbeitung.Kopieren(eg, s);
        Assert.Null(Get(Bearbeitung.Deko(eg, kopie), Etagen.FeldLink));
        // die Kopie fuehrt noch nirgends hin: rot, bis ein Ziel gewaehlt ist (Gegenprobe: ohne Kopie gruen)
        Assert.Contains(Rot(eg), t => t.Contains("führt noch nirgends hin", StringComparison.Ordinal));
        Bearbeitung.Loeschen(eg, kopie);
        Assert.Empty(Rot(eg));
    }

    [Fact]
    public void Regel_EtageGroesserAlsErdgeschoss_RotUndGruen()
    {
        var eg = MitTreppe();
        Assert.Empty(Rot(eg));
        // gleich gross und kleiner: gruen
        Py.Setzen(Etagen.Gebaeude(eg, 1), "breite", Py.F(eg["breite"]) - 4);
        Assert.DoesNotContain(Rot(eg), t => t.Contains("größer", StringComparison.Ordinal));
        Py.Setzen(Etagen.Gebaeude(eg, 1), "breite", Py.F(eg["breite"]) + 1);
        Assert.Contains(Rot(eg), t => t.Contains("größer als das Erdgeschoss", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel_TreppeNurZurNachbarEtage_RotUndGruen()
    {
        const string Text = "Eine Treppe verbindet genau zwei Etagen direkt übereinander.";
        var eg = MitTreppe(2);
        Assert.DoesNotContain(Rot(eg), t => t.Contains(Text, StringComparison.Ordinal));
        // Halt von Etage 1 auf Etage 2 umgehaengt (von Hand): Treppe ueberspringt eine Etage
        var u = Objekt(Liste(eg[Etagen.FeldUebergaenge])[0]);
        var halt = Liste(u["halte"]).OfType<PyObjekt>().Single(h => Py.Int(h["etage"]) == 1);
        Py.Setzen(halt, "etage", 2L);
        var stueck = Etagen.Stuecke(Etagen.Gebaeude(eg, 1), "u1")[0];
        Liste(Objekt(Etagen.Gebaeude(eg, 1)["flur"])["deko"]).Remove(stueck);
        Liste(Objekt(Etagen.Gebaeude(eg, 2)["flur"])["deko"]).Add(stueck);
        Assert.Contains(Rot(eg), t => t.Contains(Text, StringComparison.Ordinal));
    }

    [Fact]
    public void Regel_AnkunftNichtFrei_RotUndGruen()
    {
        var eg = MitTreppe();
        Assert.Empty(Rot(eg));
        // Ankunft ausserhalb des Hauses
        EtagenBearbeitung.AnkunftSetzen(eg, "u1", 1, -3, 13);
        Assert.Contains(Rot(eg), t => t.Contains("ist nicht frei", StringComparison.Ordinal));
        // Ankunft im Stueck selbst
        EtagenBearbeitung.AnkunftSetzen(eg, "u1", 1, 9, 13);
        Assert.Contains(Rot(eg), t => t.Contains("ist nicht frei", StringComparison.Ordinal));
        EtagenBearbeitung.AnkunftSetzen(eg, "u1", 1, 11.6, 13);
        Assert.Empty(Rot(eg));
    }

    [Fact]
    public void Regel_EtageOhneUebergang_NichtErreichbar()
    {
        var eg = MitTreppe(2);
        Assert.Contains(Rot(eg), t => t.Contains("2. Obergeschoss", StringComparison.Ordinal));
        // Gegenprobe: ohne die zweite Etage gruen
        EtagenBearbeitung.Loeschen(eg, 2);
        Assert.Empty(Rot(eg));
    }

    [Fact]
    public void Regel_UnbekannteArt_UndDatenform()
    {
        var eg = MitTreppe();
        Py.Setzen(Objekt(Liste(eg[Etagen.FeldUebergaenge])[0]), "art", "zug");
        Assert.Contains(Rot(eg), t => t.Contains("„zug“ ist keine bekannte Art", StringComparison.Ordinal));
        var kaputt = MitTreppe();
        Py.Setzen(kaputt, Etagen.FeldEtagen, "oben");
        Assert.Contains(Rot(kaputt), t => t == "Die Etagen sind falsch gespeichert.");
        Assert.Empty(Rot(MitTreppe()));
    }

    [Fact]
    public void Projekt_EinSchrittJeAenderung_RueckgaengigUndExport()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Etagen " + Guid.NewGuid().ToString("N")[..6], D, ablage, "moebelhaus");
        Assert.Equal(1, p.EtagenAendern("moebelhaus", "Etage hinzufügen", (eg, _) => EtagenBearbeitung.Hinzufuegen(eg)));
        Assert.Single(p.Verlauf);
        Assert.Equal(["moebelhaus", "moebelhaus@1"], p.EtagenVon("moebelhaus"));
        Assert.True(p.GibtInnenort("moebelhaus@1"));
        Assert.Equal("Möbelhaus, 1. Obergeschoss", p.OrtName("moebelhaus@1"));
        p.EtagenAendern("moebelhaus", "Treppe", (eg, _) =>
        {
            var s = TreppeSetzen(eg);
            EtagenBearbeitung.ZielSetzen(eg, 0, s, 1);
        });
        Assert.Equal(2, p.Verlauf.Count);
        Assert.Empty(p.RoteFunde());
        // Export: die Fassung traegt Etagen und Uebergaenge und besteht die Ladepruefung
        var datei = p.Exportieren(new DateTimeOffset(Zeit));
        var f = StadtFassung.Lesen(datei);
        var eg2 = Objekt(Objekt(f.Innenraeume["moebelhaus"])["gebaeude"]);
        Assert.Equal(1, Etagen.Anzahl(eg2));
        Assert.Single(Etagen.Uebergaenge(eg2, "moebelhaus"));
        Assert.Empty(StadtbauPruefung.EtagenAlle(f.Innenraeume, D.Grafik));
        // rot sperrt den Export (Gegenprobe): Etage groesser als das Erdgeschoss
        p.Aendern("moebelhaus@1", "größer", g => Py.Setzen(g, "breite", Py.F(g["breite"]) + 1));
        Assert.Contains(p.RoteFunde(), r => r.Ort == "moebelhaus@1");
        Assert.Throws<StadtbauFehler>(() => p.Exportieren(new DateTimeOffset(Zeit)));
        // Rueckgaengig: alles wieder wie vorher
        Assert.True(p.Rueckgaengig());
        Assert.True(p.Rueckgaengig());
        Assert.True(p.Rueckgaengig());
        Assert.False(Etagen.Hat(p.Grundriss("moebelhaus")));
        Assert.False(p.GibtInnenort("moebelhaus@1"));
    }

    [Fact]
    public void Projekt_GespeichertUndWiederGeoeffnet_MitEtagen()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Etagen " + Guid.NewGuid().ToString("N")[..6], D, ablage, "moebelhaus");
        p.EtagenAendern("moebelhaus", "Treppe", (eg, _) =>
        {
            EtagenBearbeitung.Hinzufuegen(eg);
            EtagenBearbeitung.ZielSetzen(eg, 0, TreppeSetzen(eg), 1);
        });
        var wieder = Projekt.Oeffnen(p.Datei, D, ablage);
        Assert.Equal(Json(p.Grundriss("moebelhaus")), Json(wieder.Grundriss("moebelhaus")));
        Assert.Equal(Json(p.Grundriss("moebelhaus@1")), Json(wieder.Grundriss("moebelhaus@1")));
    }

    [Fact]
    public void Blueprint_NimmtEtagenMit()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Quelle " + Guid.NewGuid().ToString("N")[..6], D, ablage, "moebelhaus");
        p.EtagenAendern("moebelhaus", "Treppe", (eg, _) =>
        {
            EtagenBearbeitung.Hinzufuegen(eg);
            EtagenBearbeitung.ZielSetzen(eg, 0, TreppeSetzen(eg), 1);
        });
        var b = Blueprint.Aus(p, new AussenStelle(AussenArt.Haus, -1, 0, "moebelhaus"), "Möbelhaus mit Etage")!;
        var gelesen = Blueprint.Lesen(b.Schreiben());
        Assert.NotNull(gelesen.Innenraum);
        Assert.Equal(1, Etagen.Anzahl(gelesen.Innenraum!));
        Assert.Equal(Json(b.Innenraum), Json(gelesen.Innenraum));
    }
}
