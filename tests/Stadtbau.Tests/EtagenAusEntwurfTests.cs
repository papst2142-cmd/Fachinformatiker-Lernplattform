using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.19.0 „Obergeschosse eigener Gebaeude“ (Weisung P12-80, Nico 1a 2a 3a 4a 5a, Plan Meldung P12-51 Abschnitt 2): Etagen
/// und Treppenhaus entstehen beim Speichern als Blueprint aus den Ebenen des Entwurfs; Pruefregeln rot und gruen.
/// </summary>
public class EtagenAusEntwurfTests
{
    private const string Tuer = Bauteile.Praefix + "building-door";
    private const string Fenster = Bauteile.Praefix + "building-window";
    private const string Dach = Bauteile.Praefix + "roof-flat-center";

    private static Spieldaten D => Daten.Spiel;

    private static string Json(object? o) => PythonJson.Standard(o);

    /// <summary>
    /// Haus <paramref name="w"/> x <paramref name="t"/> Felder, Tuer vorne (Sued) in Feld (1, t-1); Ebene k hat Waende auf den
    /// Feldern mit x in <paramref name="ebenen"/>[k] (von, bis), Flachdach darueber.
    /// </summary>
    private static Entwurf Haus(int w, int t, params (int Von, int Bis)[] ebenen)
    {
        var e = new Entwurf();
        for (var k = 0; k < ebenen.Length; k++)
        {
            for (var x = ebenen[k].Von; x <= ebenen[k].Bis; x++)
            {
                for (var y = 0; y < t; y++)
                {
                    e.Setzen(k == 0 && x == 1 && y == t - 1 ? Tuer : Fenster, x, y, k, y == t - 1 ? 0 : 2);
                    var oben = k + 1 < ebenen.Length && x >= ebenen[k + 1].Von && x <= ebenen[k + 1].Bis;
                    if (!oben)
                    {
                        e.Setzen(Dach, x, y, k + 1, 0);
                    }
                }
            }
        }
        Assert.True(e.EingangFestlegen(e.Teile.FindIndex(p => p.Id == Tuer)));
        Assert.DoesNotContain(e.Pruefen(Katalog.Bauteil), f => f.Rot);
        return e;
    }

    private static PyObjekt Innen(Entwurf e, out int etagen)
    {
        var b = e.AlsBlueprint("Probe", Katalog.Bauteil, "Projekt", "neu_x", out _, out etagen);
        return b.Innenraum!;
    }

    /// <summary>Alle roten Funde: Erdgeschoss, jede Etage (gemeinsamer Teil) und die Regeln zwischen den Etagen.</summary>
    private static List<string> Rot(PyObjekt eg)
    {
        var rot = StadtbauPruefung.Innen("bau:probe", eg, [], D.Grafik).Where(f => f.Rot).Select(f => "EG: " + f.Text).ToList();
        rot.AddRange(StadtbauPruefung.Etagen("bau:probe", eg, D.Grafik).Where(f => f.Fund.Rot).Select(f => f.Etage + ": " + f.Fund.Text));
        return rot;
    }

    /// <summary>Lage eines Stuecks im Grundriss des Erdgeschosses (Etage + Ursprung ihres Rechtecks).</summary>
    private static (double X, double Y) ImEg(PyObjekt d, EtagenAusEntwurf.Rechteck? r) =>
        (Py.F(d["x"]) + (r?.X0 ?? 0), Py.F(d["y"]) + (r?.Y0 ?? 0));

    [Fact]
    public void Probehaus_EgUndZweiObergeschosse_EtagenMitTreppenhaus_Gruen()
    {
        var e = Entwurf.Probehaus(3);
        var eg = Innen(e, out var neu);
        Assert.Equal(2, neu);
        Assert.Equal(2, Etagen.Anzahl(eg));
        for (var k = 1; k <= 2; k++)
        {
            var g = Etagen.Gebaeude(eg, k);
            Assert.Equal(Py.F(eg["breite"]), Py.F(g["breite"]));
            Assert.Equal(Py.F(eg["hoehe"]), Py.F(g["hoehe"]));
            Assert.Empty(Liste(g["raeume"]));
            Assert.False(Py.Hat(Objekt(g["flur"]), "eingang"));
        }
        var u = Etagen.Uebergaenge(eg, "bau:probe");
        Assert.Equal(["u1", "u2"], u.Select(x => x.Id));
        Assert.All(u, x => Assert.Equal(Etagen.Treppe, x.Art));
        Assert.Equal([0, 1], u[0].Halte.Select(h => h.Etage));
        Assert.Equal([1, 2], u[1].Halte.Select(h => h.Etage));
        Assert.Empty(Rot(eg));
        // jede Etage ist erreichbar und hat ihre Ankunft vor der Treppe
        Assert.Equal(Etagen.Vor(Etagen.Stuecke(eg, "u1")[0]), (u[0].Auf(0)!.X, u[0].Auf(0)!.Y, u[0].Auf(0)!.Blick));
    }

    [Fact]
    public void Gegenprobe_OhneTreppenhaus_EtagenRot()
    {
        EtagenAusEntwurf.OhneTreppeFuerTests = true;
        try
        {
            var eg = Innen(Entwurf.Probehaus(3), out _);
            Assert.Equal(2, Etagen.Anzahl(eg));
            Assert.NotEmpty(Rot(eg));
        }
        finally
        {
            EtagenAusEntwurf.OhneTreppeFuerTests = false;
        }
    }

    [Fact]
    public void DachEbene_WirdKeineEtage_EinStockwerk_KeineEtagen()
    {
        Assert.Equal(1, Etagen.Anzahl(Innen(Entwurf.Probehaus(2), out var n2)));
        Assert.Equal(1, n2);
        var eg = Innen(Entwurf.Probehaus(1), out var n1);
        Assert.Equal(0, n1);
        Assert.False(Etagen.Hat(eg));
    }

    [Fact]
    public void Treppenhaus_AnDerselbenStelle_JedeZweiteGestapelt_VierObergeschosse_Gruen()
    {
        var e = Entwurf.Probehaus(5);
        var eg = Innen(e, out var neu);
        Assert.Equal(4, neu);
        Assert.Empty(Rot(eg));
        var ebenen = EtagenAusEntwurf.Obergeschosse(e.Teile, e.Eingaenge);
        EtagenAusEntwurf.Rechteck? R(int k) => k == 0 ? null : ebenen[k - 1];
        var lage = new Dictionary<string, (double, double)>();
        foreach (var u in Etagen.Uebergaenge(eg, ""))
        {
            var unten = ImEg(Etagen.Stuecke(Etagen.Gebaeude(eg, u.Halte[0].Etage), u.Id).Single(), R(u.Halte[0].Etage));
            var oben = ImEg(Etagen.Stuecke(Etagen.Gebaeude(eg, u.Halte[1].Etage), u.Id).Single(), R(u.Halte[1].Etage));
            Assert.Equal(unten, oben);
            lage[u.Id] = unten;
        }
        Assert.Equal(lage["u1"], lage["u3"]);
        Assert.Equal(lage["u2"], lage["u4"]);
        Assert.NotEqual(lage["u1"], lage["u2"]);
    }

    [Fact]
    public void ObergeschossZurueckgesetzt_EigenerUrsprung_GroesseDerEbene_Gruen()
    {
        // EG 3 x 3 Felder, 1. OG nur auf x = 1..2 (links zurueckgesetzt)
        var e = Haus(3, 3, (0, 2), (1, 2));
        var r = EtagenAusEntwurf.Obergeschosse(e.Teile, e.Eingaenge)[0]!.Value;
        // Eingang Sued: x im Plan = Tiefe (3 Felder), y = laengs der Fassade (Feld 1..2)
        Assert.Equal(new EtagenAusEntwurf.Rechteck(0, 4, 12, 12), r);
        var eg = Innen(e, out _);
        var g = Etagen.Gebaeude(eg, 1);
        Assert.Equal(12.0, Py.F(g["breite"]));
        Assert.Equal(8.0, Py.F(g["hoehe"]));
        Assert.Empty(Rot(eg));
        Assert.Empty(EtagenAusEntwurf.Pruefen(eg, e.Teile, e.Eingaenge));
    }

    [Theory]
    [InlineData(Bauteile.Seiten.Nord, 0, 2)]
    [InlineData(Bauteile.Seiten.Ost, 2, 1)]
    [InlineData(Bauteile.Seiten.West, 0, 0)]
    [InlineData(Bauteile.Seiten.Sued, 1, 0)]
    public void Ebene_LageJeEingangsseite(Bauteile.Seiten seite, int erwarteteTiefe, int erwartetSeitlich)
    {
        // Grundflaeche 3 x 2 (x 0..2, y 0..1), Ebene 1 nur Feld (0, 0)
        var teile = new List<Bauteile.Teil>();
        for (var x = 0; x < 3; x++)
        {
            for (var y = 0; y < 2; y++)
            {
                teile.Add(new Bauteile.Teil(Fenster, x, y, 0, 0));
            }
        }
        teile.Add(new Bauteile.Teil(Fenster, 0, 0, 1, 0));
        var eingang = seite switch
        {
            Bauteile.Seiten.Nord => new Bauteile.Eingang(1, 0, seite),
            Bauteile.Seiten.Ost => new Bauteile.Eingang(2, 0, seite),
            Bauteile.Seiten.West => new Bauteile.Eingang(0, 0, seite),
            _ => new Bauteile.Eingang(1, 1, seite),
        };
        var r = EtagenAusEntwurf.Ebene(teile, [eingang], 1)!.Value;
        Assert.Equal(new EtagenAusEntwurf.Rechteck(erwarteteTiefe * 4, erwartetSeitlich * 4, (erwarteteTiefe + 1) * 4, (erwartetSeitlich + 1) * 4), r);
        // das Erdgeschoss ist so gross wie der Vorschlag des Erdgeschosses
        var eg = EtagenAusEntwurf.Ebene(teile, [eingang], 0)!.Value;
        var tiefe = seite is Bauteile.Seiten.Nord or Bauteile.Seiten.Sued ? 2 : 3;
        Assert.Equal(new EtagenAusEntwurf.Rechteck(0, 0, tiefe * 4, (5 - tiefe) * 4), eg);
    }

    [Fact]
    public void Regel_EtageRagtUeberIhreEbene_Rot_Gegenprobe_Gruen()
    {
        var e = Entwurf.Probehaus(3);
        var eg = Innen(e, out _);
        Assert.Empty(EtagenAusEntwurf.Pruefen(eg, e.Teile, e.Eingaenge));
        // gruen bleibt auch die W5-Regel (nie groesser als das EG): die Etage ist nur zu breit fuer ihre Ebene
        var schmal = Haus(3, 2, (0, 2), (1, 2));
        var eg2 = Innen(schmal, out _);
        Py.Setzen(Etagen.Gebaeude(eg2, 1), "hoehe", Py.F(eg2["hoehe"]));
        var f = Assert.Single(EtagenAusEntwurf.Pruefen(eg2, schmal.Teile, schmal.Eingaenge));
        Assert.True(f.Fund.Rot);
        Assert.Equal(1, f.Etage);
        Assert.Contains("ragt über die Grundfläche", f.Fund.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(StadtbauPruefung.Etagen("bau:probe", eg2, D.Grafik), x => x.Fund.Text.Contains("größer als das Erdgeschoss", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel_EbeneOhneEtage_Gelb_EtageKleiner_Gelb_VorschlagNeu_Gruen()
    {
        var zwei = Entwurf.Probehaus(2);
        var eg = Innen(zwei, out _);
        // der Entwurf bekommt ein 2. OG: die Ebene hat Waende, aber keine Etage
        var drei = Entwurf.Probehaus(3);
        var f = Assert.Single(EtagenAusEntwurf.Pruefen(eg, drei.Teile, drei.Eingaenge));
        Assert.False(f.Fund.Rot);
        Assert.Equal(0, f.Etage);
        Assert.Contains("noch nicht begehbar", f.Fund.Text, StringComparison.Ordinal);
        // Etage kleiner als die Ebene: gelb
        var kleiner = (PyObjekt)Py.Kopie(eg)!;
        Py.Setzen(Etagen.Gebaeude(kleiner, 1), "breite", Py.F(eg["breite"]) - 4);
        var g = Assert.Single(EtagenAusEntwurf.Pruefen(kleiner, zwei.Teile, zwei.Eingaenge));
        Assert.False(g.Fund.Rot);
        Assert.Contains("passt nicht mehr", g.Fund.Text, StringComparison.Ordinal);
        // Vorschlag neu: zwei Etagen, gruen, die Einrichtung des Erdgeschosses bleibt
        var flurDeko = Liste(Objekt(eg["flur"])["deko"]);
        flurDeko.Add(new PyObjekt { { "typ", "pflanze" }, { "x", 3.0 }, { "y", Py.F(Objekt(eg["flur"])["y"]) + 0.3 }, { "w", 0.6 }, { "h", 0.6 } });
        Assert.Equal(2, EtagenAusEntwurf.Neu(eg, drei.Teile, drei.Eingaenge));
        Assert.Empty(EtagenAusEntwurf.Pruefen(eg, drei.Teile, drei.Eingaenge));
        Assert.Equal([], Rot(eg));
        Assert.Contains(Liste(Objekt(eg["flur"])["deko"]), d => Py.Gleich(Get(Objekt(d), "typ"), "pflanze"));
        Assert.Equal(1, Liste(Objekt(eg["flur"])["deko"]).Count(d => Py.Gleich(Get(Objekt(d), "typ"), Etagen.Treppe)));
    }

    [Fact]
    public void Weiterbauen_InnenraumMitEtagen_Bleibt_OhneEtagen_BekommtSie()
    {
        var e = Entwurf.Probehaus(3);
        var b = e.AlsBlueprint("Probe", Katalog.Bauteil, "P", "neu_x", out _, out _);
        // Nico richtet das 1. OG ein; danach wird weitergebaut und gespeichert: alles bleibt (3a)
        var innen = (PyObjekt)Py.Kopie(b.Innenraum)!;
        Liste(Objekt(Etagen.Gebaeude(innen, 1)["flur"])["deko"]).Add(new PyObjekt { { "typ", "pflanze" }, { "x", 1.0 }, { "y", 1.0 }, { "w", 0.6 }, { "h", 0.6 } });
        var weiter = Entwurf.Aus(new Blueprint
        {
            Name = b.Name, Herkunft = b.Herkunft, Modell = b.Modell, Teile = b.Teile, Tuer = b.Tuer, Weg = b.Weg, Schild = b.Schild,
            Innenraum = innen, Personen = b.Personen, Bauteile = b.Bauteile, Eingang = b.Eingang,
        })!;
        var b2 = weiter.AlsBlueprint("Probe", Katalog.Bauteil, "P", "neu_x", out var grundrissNeu, out var etagenNeu);
        Assert.False(grundrissNeu);
        Assert.Equal(0, etagenNeu);
        Assert.Equal(Json(innen), Json(b2.Innenraum));
        // ohne Etagen (Blueprint aus S 0.18.0): sie kommen dazu, das Erdgeschoss bleibt
        var ohne = (PyObjekt)Py.Kopie(innen)!;
        ohne.RemoveAll(p => p.Key is Etagen.FeldEtagen or Etagen.FeldUebergaenge);
        Liste(Objekt(ohne["flur"])["deko"]).RemoveAll(d => Py.Gleich(Get(Objekt(d), "typ"), Etagen.Treppe));
        var alt = Entwurf.Aus(new Blueprint
        {
            Name = b.Name, Herkunft = b.Herkunft, Modell = b.Modell, Teile = b.Teile, Tuer = b.Tuer, Weg = b.Weg, Schild = b.Schild,
            Innenraum = ohne, Personen = b.Personen, Bauteile = b.Bauteile, Eingang = b.Eingang,
        })!;
        var b3 = alt.AlsBlueprint("Probe", Katalog.Bauteil, "P", "neu_x", out _, out var n3);
        Assert.Equal(2, n3);
        Assert.Empty(Rot(b3.Innenraum!));
    }

    [Fact]
    public void Editor_EntwurfGeaendert_GelberHinweis_SpeichernBehaelt_VorschlagNeu_LegtNeuAn()
    {
        // 3 Stockwerke gespeichert, dann im Editor das 2. OG schmaler gebaut (Ebene 2 nur noch x = 1..2)
        var b = Entwurf.Probehaus(3).AlsBlueprint("Probe", Katalog.Bauteil, "P", "neu_x", out _, out _);
        var weiter = Entwurf.Aus(b)!;
        Assert.Empty(weiter.EtagenHinweise());
        foreach (var i in Enumerable.Range(0, weiter.Teile.Count).Reverse().Where(i => weiter.Teile[i].Stockwerk >= 2 && weiter.Teile[i].X == 0))
        {
            weiter.Loeschen(i);
        }
        weiter.Setzen(Dach, 0, 0, 2, 0);
        weiter.Setzen(Dach, 0, 1, 2, 0);
        var hinweis = Assert.Single(weiter.EtagenHinweise());
        Assert.False(hinweis.Rot);
        Assert.Contains("ragt über die Grundfläche", hinweis.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(weiter.Pruefen(Katalog.Bauteil), f => f.Rot);
        // Speichern behaelt den Innenraum (nichts geht verloren)
        var b2 = weiter.AlsBlueprint("Probe", Katalog.Bauteil, "P", "neu_x", out _, out var n);
        Assert.Equal(0, n);
        Assert.Equal(Json(b.Innenraum), Json(b2.Innenraum));
        // im gesetzten Gebaeude ist die Regel 5a rot
        Assert.Contains(EtagenAusEntwurf.Pruefen(b2.Innenraum!, weiter.Teile, weiter.Eingaenge), f => f.Fund.Rot);
        // Vorschlag neu: kein Hinweis mehr, Speichern legt die Etagen passend neu an
        weiter.EtagenNeu = true;
        Assert.Empty(weiter.EtagenHinweise());
        var b3 = weiter.AlsBlueprint("Probe", Katalog.Bauteil, "P", "neu_x", out _, out var n3);
        Assert.Equal(2, n3);
        Assert.Empty(EtagenAusEntwurf.Pruefen(b3.Innenraum!, weiter.Teile, weiter.Eingaenge));
        Assert.Equal(8.0, Py.F(Etagen.Gebaeude(b3.Innenraum!, 2)["hoehe"]));
        Assert.Empty(Rot(b3.Innenraum!));
    }

    [Fact]
    public void Projekt_GesetztesGebaeude_EtagenBegehbar_AlterInnenraumGelb_VorschlagNeuEinSchritt()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Eigenbau", Daten.Spiel, ablage, leer: (32, 24));
        p.AussenAendern("Straße", a =>
        {
            var s = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 4, 12)!;
            AussenBearbeitung.LaengeSetzen(a, s, 28);
        });
        var b = Entwurf.Probehaus(3).AlsBlueprint("Turm", Katalog.Bauteil, p.Name, p.Basis.StadtId, out _, out _);
        p.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, 10, 8.5, out _));
        const string ort = "bau:bau:turm";
        Assert.Contains(ort, p.Innenorte);
        Assert.Equal([ort, ort + "@1", ort + "@2"], p.EtagenVon(ort));
        Assert.All(p.EtagenVon(ort), o => Assert.DoesNotContain(p.Pruefen(o), f => f.Rot));
        Assert.Empty(p.EtagenEntwurfFunde(ort));
        // alter Innenraum ohne Etagen (Projekt aus S 0.18.0): gelb „noch nicht begehbar“, Vorschlag neu legt sie an
        p.EtagenAendern(ort, "alt", (eg, _) =>
        {
            foreach (var k in new[] { 2, 1 })
            {
                foreach (var u in Etagen.Uebergaenge(eg, "").Where(u => u.Auf(k) is not null).ToList())
                {
                    foreach (var g in Enumerable.Range(0, Etagen.Anzahl(eg) + 1))
                    {
                        Liste(Objekt(Etagen.Gebaeude(eg, g)["flur"])["deko"]).RemoveAll(d => Get(Objekt(d), Etagen.FeldLink) as string == u.Id);
                    }
                }
            }
            eg.RemoveAll(x => x.Key is Etagen.FeldEtagen or Etagen.FeldUebergaenge);
        });
        Assert.Equal([ort], p.EtagenVon(ort));
        var gelb = p.Pruefen(ort).Where(f => f.Text.Contains("noch nicht begehbar", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, gelb.Count);
        Assert.All(gelb, f => Assert.False(f.Rot));
        Assert.Equal(2, p.EtagenVorschlagNeu(ort, Texte.EtagenVorschlagNeu));
        Assert.Empty(p.EtagenEntwurfFunde(ort));
        Assert.Equal(3, p.EtagenVon(ort).Count);
        Assert.All(p.EtagenVon(ort), o => Assert.DoesNotContain(p.Pruefen(o), f => f.Rot));
        // ein Schritt: Rueckgaengig nimmt alles zurueck
        Assert.True(p.Rueckgaengig());
        Assert.Equal([ort], p.EtagenVon(ort));
        // Regel 5a im gesetzten Gebaeude rot: das 2. OG zu breit fuer seine Ebene; Gegenprobe passend gruen
        p.Wiederholen();
        p.EtagenAendern(ort, "breiter", (eg, _) => Py.Setzen(Etagen.Gebaeude(eg, 2), "hoehe", Py.F(eg["hoehe"]) + 4));
        Assert.Contains(p.Pruefen(ort + "@2"), f => f.Rot && f.Text.Contains("ragt über die Grundfläche", StringComparison.Ordinal));
        Assert.Contains(p.RoteFunde(), r => r.Ort == ort + "@2");
        // ohne Entwurf (Ort der Stadt): kein Knopf, keine Funde
        Assert.Equal(-1, p.EtagenVorschlagNeu("moebelhaus", "x"));
    }

    [Fact]
    public void EchteDateiAusS0180_BleibtLesbar_Format2_EtagenBeimNaechstenSpeichern()
    {
        var pfad = Path.Combine(Daten.Wurzel, "tests", "Stadtbau.Tests", "Testdaten", "blueprint_S0.18.0.fisblueprint");
        var text = File.ReadAllText(pfad);
        Assert.Contains("\"formatversion\": 2", text, StringComparison.Ordinal);
        Assert.Contains("S 0.18.0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"etagen\"", text, StringComparison.Ordinal);
        var b = Blueprint.Lesen(File.ReadAllBytes(pfad));
        Assert.False(Etagen.Hat(b.Innenraum!));
        var ablage = Daten.NeueAblage();
        File.Copy(pfad, Path.Combine(ablage.Blueprints, Path.GetFileName(pfad)));
        var eintrag = Assert.Single(Blueprint.Alle(ablage));
        Assert.Null(eintrag.Fehler);
        var e = Entwurf.Aus(eintrag.Vorlage!)!;
        var neu = e.AlsBlueprint(b.Name, Katalog.Bauteil, "P", "neu_x", out var grundrissNeu, out var etagen);
        Assert.False(grundrissNeu);
        Assert.Equal(1, etagen);
        Assert.Empty(Rot(neu.Innenraum!));
        // die Etagen stehen im Innenraum (kein eigenes Feld); seit S 0.31.0 schreibt das Werkzeug Format 3 (Feld farben)
        Assert.Contains("\"formatversion\": 3", System.Text.Encoding.UTF8.GetString(neu.Schreiben()), StringComparison.Ordinal);
    }
}
