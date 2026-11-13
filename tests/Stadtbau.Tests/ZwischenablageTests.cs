using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.47.0 W1 (Weisung P12-226): Stuecke aus einem Projekt kopieren und in ein anderes einfuegen. Ein Schritt, Lage
/// zueinander bleibt, Haeuser mit Innenraum und Etagen, gleiche Modelle bekommen einen Kopienamen, gleicher Name mit anderem
/// Inhalt = Grund, Spielorte nie doppelt.
/// </summary>
public class ZwischenablageTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Projekt Leer(string name, Werkzeugordner? ablage = null) => Projekt.Neu(name, D, ablage ?? Daten.NeueAblage(), null, (40, 30));

    private static AussenStelle Setzen(Projekt p, AussenArt art, double x, double y, string? modell = null) =>
        p.AussenAendern("Setzen", a => AussenBearbeitung.Setzen(a, art, x, y, modell: modell))!;

    private static AussenBearbeitung.EinfuegenErgebnis Einfuegen(Projekt p, Zwischenablage z, double x, double y) =>
        p.AussenAendern(Texte.EinfuegenSchritt, a => AussenBearbeitung.Einfuegen(a, z, x, y, true, D.Fassung));

    private static Zwischenablage Kopieren(Projekt p, params AussenStelle[] gruppe) =>
        AussenBearbeitung.InZwischenablage(p, gruppe, out _) ?? throw new InvalidOperationException("nichts kopiert");

    private static List<string> Rote(Projekt p) => [.. p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text)];

    private static readonly AussenStelle Moebelhaus = new(AussenArt.Haus, -1, 0, "moebelhaus");

    [Fact]
    public void Strassen_Kreisverkehr_Baum_Katalog_KommenMit_LageZueinanderBleibt_EinSchritt()
    {
        var q = Leer("Quelle");
        var ring = Setzen(q, AussenArt.Kreisverkehr, 12, 12, AussenBearbeitung.Kennung(2));
        var baum = Setzen(q, AussenArt.Baum, 20, 6);
        var auto = Setzen(q, AussenArt.Katalog, 6, 20, "car_kit/sedan");
        var park = Setzen(q, AussenArt.Park, 22, 20);
        var a = q.AussenStand;
        var vorherStrassen = Aussen.Landschaft(a).OfType<PyObjekt>().Count(t => Py.Gleich(t["typ"], "strasse"));
        var z = Kopieren(q, ring, baum, auto, park);
        // Zufahrten und Laternen des Kreisverkehrs kommen mit
        var laternen = z.Stuecke.OfType<PyObjekt>().Count(st => Py.Gleich(st["art"], nameof(AussenArt.Laterne)));
        Assert.Equal(4, laternen);
        Assert.Equal(vorherStrassen + laternen + 3, z.Anzahl);

        var ziel = Leer("Ziel");
        var schritte = ziel.SchritteZurueck;
        var vorher = Aussen.TeileText(ziel.AussenStand);
        var e = Einfuegen(ziel, z, 20, 15);
        Assert.Null(e.Grund);
        Assert.Equal(z.Anzahl, e.Erledigt);
        Assert.Equal(schritte + 1, ziel.SchritteZurueck);
        var b = ziel.AussenStand;
        Assert.Equal(vorherStrassen, Aussen.Landschaft(b).OfType<PyObjekt>().Count(t => Py.Gleich(t["typ"], "strasse")));
        Assert.True(Aussen.Laternen(b).Count >= laternen);
        Assert.Single(Aussen.KatalogStuecke(b));
        // gleicher Versatz fuer alle Stuecke (Lage zueinander bleibt)
        var dx = Py.F(Objekt(Aussen.KatalogStuecke(b)[0])["x"]) - Py.F(Objekt(Aussen.KatalogStuecke(a)[0])["x"]);
        var dy = Py.F(Objekt(Aussen.KatalogStuecke(b)[0])["y"]) - Py.F(Objekt(Aussen.KatalogStuecke(a)[0])["y"]);
        Assert.Equal(20 - z.Mitte.X, dx, 0.26);
        Assert.Equal(15 - z.Mitte.Y, dy, 0.26);
        var ringA = AussenBearbeitung.MitteVon(a, ring)!.Value;
        var ringB = AussenBearbeitung.MitteVon(b, e.Auswahl.First(s => AussenBearbeitung.IstKreisverkehr(b, s)))!.Value;
        Assert.Equal(dx, ringB.X - ringA.X, 0.001);
        Assert.Equal(dy, ringB.Y - ringA.Y, 0.001);
        Assert.Empty(Rote(ziel));
        // ein Schritt: Rueckgaengig nimmt alles zurueck
        Assert.True(ziel.Rueckgaengig());
        Assert.Equal(vorher, Aussen.TeileText(ziel.AussenStand));
    }

    [Fact]
    public void UeberDenRand_Grund_NichtsGeaendert_KeinSchritt()
    {
        var q = Leer("Quelle");
        var park = Setzen(q, AussenArt.Park, 20, 15);
        var z = Kopieren(q, park);
        var ziel = Leer("Ziel");
        var vorher = Aussen.TeileText(ziel.AussenStand);
        var e = Einfuegen(ziel, z, 0.5, 0.5);
        Assert.Equal(Texte.EinfuegenAusserhalb, e.Grund);
        Assert.Equal(0, ziel.SchritteZurueck);
        Assert.Equal(vorher, Aussen.TeileText(ziel.AussenStand));
    }

    [Fact]
    public void Haus_MitInnenraumUndEtage_OrtSchonDa_AlsGebaeudeOhneOrt_MitKopiename_Hinweis()
    {
        var q = Projekt.Neu("Quelle", D, Daten.NeueAblage());
        q.EtagenAendern("moebelhaus", "Etage", (eg, _) => EtagenBearbeitung.Hinzufuegen(eg));
        var z = Kopieren(q, Moebelhaus);
        var ziel = Leer("Ziel");
        Assert.Null(Einfuegen(ziel, z, 10, 8).Grund);
        var e = Einfuegen(ziel, z, 28, 8);
        Assert.Null(e.Grund);
        Assert.Contains(Texte.EinfuegenOrtSchonDa(Spielort.Name(AussenBearbeitung.Ort(q.AussenStand, "moebelhaus")!)), e.Hinweise);
        var b = ziel.AussenStand;
        var g = Objekt(Aussen.Landschaft(b)[e.Auswahl.Single().Nummer]);
        Assert.Equal(AussenArt.Gebaeude, e.Auswahl.Single().Art);
        Assert.Equal("moebelhaus_2", Py.Str(g["modell"]));
        Assert.True(GebaeudeTuer.IstKopie(b, "moebelhaus_2"));
        // Innenraum mit Etage kommt mit (Teil "bauten")
        var bau = Objekt(Aussen.Bauten(b)["moebelhaus_2"]);
        Assert.Single(Liste(Objekt(bau["grundriss"])["etagen"]));
        Assert.Equal(PythonJson.Standard(q.Grundriss("moebelhaus")), PythonJson.Standard(bau["grundriss"]));
        // der Spielort bleibt einmal
        Assert.Single(AussenBearbeitung.Orte(b), o => Py.Gleich(o["id"], "moebelhaus"));
    }

    [Fact]
    public void GleichesGebaeudeDreimal_Kopienamen_KeinNeuerRoterFund_Gegenprobe()
    {
        var q = Projekt.Neu("Quelle", D, Daten.NeueAblage());
        var z = Kopieren(q, Moebelhaus);
        var ziel = Leer("Ziel");
        Assert.Null(Einfuegen(ziel, z, 10, 8).Grund);
        Assert.Null(Einfuegen(ziel, z, 28, 8).Grund);
        Assert.Null(Einfuegen(ziel, z, 10, 22).Grund);
        Assert.DoesNotContain(Rote(ziel), t => t.StartsWith(Texte.BlueprintSchonDa, StringComparison.Ordinal));
        Assert.Contains(Aussen.Landschaft(ziel.AussenStand).OfType<PyObjekt>(), t => Py.Gleich(t["modell"], "moebelhaus_3"));

        // Gegenprobe: ohne Kopienamen steht dasselbe Modell mit Tuer zweimal (rot)
        AussenBearbeitung.ZwischenablageAltFuerTests = true;
        try
        {
            var alt = Leer("Alt");
            Assert.Null(Einfuegen(alt, z, 10, 8).Grund);
            Assert.Null(Einfuegen(alt, z, 28, 8).Grund);
            Assert.Null(Einfuegen(alt, z, 10, 22).Grund);
            Assert.Contains(Rote(alt), t => t.StartsWith(Texte.BlueprintSchonDa, StringComparison.Ordinal));
        }
        finally
        {
            AussenBearbeitung.ZwischenablageAltFuerTests = false;
        }
    }

    [Fact]
    public void GleicherNameAndererInhalt_Grund_NieUeberschrieben()
    {
        var q = Projekt.Neu("Quelle", D, Daten.NeueAblage());
        var z = Kopieren(q, Moebelhaus);
        var ziel = Leer("Ziel");
        // im Ziel heisst ein anderes Modell genauso
        ziel.AussenAendern("Modell", a =>
        {
            foreach (var (teil, schluessel) in new[] { (Aussen.Karte(a), "modelle"), (Aussen.Stadtdaten(a), "neue_modelle") })
            {
                if (Get(teil, schluessel) is not PyObjekt m)
                {
                    m = [];
                    Py.Setzen(teil, schluessel, m);
                }
                Py.Setzen(m, "moebelhaus", new List<object?> { new PyObjekt { { "art", "block" }, { "x", 0.0 }, { "y", 0.0 }, { "w", 2.0 }, { "t", 2.0 }, { "h", 1.0 } } });
            }
        });
        var vorher = Aussen.TeileText(ziel.AussenStand);
        var schritte = ziel.SchritteZurueck;
        var e = Einfuegen(ziel, z, 20, 15);
        Assert.Equal(Texte.BlueprintModellAnders("moebelhaus"), e.Grund);
        Assert.Equal(schritte, ziel.SchritteZurueck);
        Assert.Equal(vorher, Aussen.TeileText(ziel.AussenStand));
    }

    [Fact]
    public void Spielort_FehltImZiel_WirdSpielort_InnenraumDerStadt1()
    {
        var q = Projekt.Neu("Quelle", D, Daten.NeueAblage());
        var z = Kopieren(q, Moebelhaus);
        var ziel = Leer("Ziel");
        var e = Einfuegen(ziel, z, 20, 15);
        Assert.Null(e.Grund);
        Assert.Equal(new AussenStelle(AussenArt.Haus, -1, 0, "moebelhaus"), e.Auswahl.Single());
        Assert.NotNull(AussenBearbeitung.Ort(ziel.AussenStand, "moebelhaus"));
        Assert.Contains("moebelhaus", ziel.Orte);
        Assert.Empty(Aussen.Bauten(ziel.AussenStand));
        // ein zweites Einfuegen macht den Ort nicht doppelt
        var e2 = Einfuegen(ziel, z, 30, 15);
        Assert.Equal(AussenArt.Gebaeude, e2.Auswahl.Single().Art);
        Assert.Single(AussenBearbeitung.Orte(ziel.AussenStand), o => Py.Gleich(o["id"], "moebelhaus"));
    }

    [Fact]
    public void GedrehtesHaus_BehaeltSeinenWinkel()
    {
        var q = Projekt.Neu("Quelle", D, Daten.NeueAblage());
        Assert.True(q.AussenAendern("Drehen", a => AussenBearbeitung.Drehen(a, Moebelhaus, 120)));
        var winkel = (AussenBearbeitung.HausDreh(q.AussenStand, Moebelhaus) * 90) + AussenBearbeitung.HausGrad(q.AussenStand, Moebelhaus);
        Assert.Equal(120, winkel);
        var z = Kopieren(q, Moebelhaus);
        var ziel = Leer("Ziel");
        var e = Einfuegen(ziel, z, 20, 15);
        var s = e.Auswahl.Single();
        Assert.Equal(120, (AussenBearbeitung.HausDreh(ziel.AussenStand, s) * 90) + AussenBearbeitung.HausGrad(ziel.AussenStand, s));
        var m = AussenBearbeitung.MitteVon(ziel.AussenStand, s)!.Value;
        Assert.Equal(20, m.X, 0.3);
        Assert.Equal(15, m.Y, 0.3);
    }

    [Fact]
    public void Datei_HinUndZurueck_KaputtIstLeer()
    {
        var ablage = Daten.NeueAblage();
        var q = Leer("Quelle", ablage);
        var z = Kopieren(q, Setzen(q, AussenArt.Baum, 4, 4), Setzen(q, AussenArt.Beet, 6, 4));
        z.Ablegen(ablage);
        var wieder = Zwischenablage.Laden(ablage)!;
        Assert.Equal(z.Schreiben(), wieder.Schreiben());
        Assert.Equal("Quelle", wieder.Projekt);
        File.WriteAllText(Path.Combine(ablage.Wurzel, Zwischenablage.Dateiname), "{kaputt");
        Assert.Null(Zwischenablage.Laden(ablage));
        Assert.Null(Zwischenablage.Lesen("{\"format\": \"fiszwischenablage\", \"formatversion\": 2}"u8.ToArray()));
    }

    [Fact]
    public void Paketplatz_NichtKopierbar()
    {
        var q = Projekt.Neu("Quelle", D, Daten.NeueAblage());
        Assert.Null(AussenBearbeitung.InZwischenablage(q, [new AussenStelle(AussenArt.Paketplatz, 0)], out var ueber));
        Assert.Equal(1, ueber);
    }
}
