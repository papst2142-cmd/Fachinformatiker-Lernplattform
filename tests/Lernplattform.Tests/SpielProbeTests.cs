using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W1 (3D-Stichprobe): Wegsuche wie fisi_game.walk_path (Buero), Kamera (Boden unter dem
/// Finger, Zoomgrenzen). Ab W4 (Weisung P7-9): die 3D-Stadt aus dem Stadt-Kern (<see cref="StadtSzene"/>,
/// <see cref="StadtSpiel"/>): alle Orte, Wege kosten Spielzeit, Oeffnungszeiten, Warten, Buero,
/// Tippen trifft ein Haus, Kamera drehen und zurueckrechnen. Feste C#-Faelle (keine Python-Vorlage).
/// </summary>
public class SpielProbeTests
{
    private static readonly Lazy<ProbeFlaeche> BueroFlaeche = new(() =>
        ProbeWelt.BueroBauen(name => File.ReadAllBytes(Path.Combine(Pfade.Inhalte, "spiel", name + ".json"))));

    private static Stadt NeueStadt() => new(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);

    private static StadtSpiel NeuesSpiel()
    {
        var stadt = NeueStadt();
        var ablauf = new Spielablauf(stadt.Inhalt);
        ablauf.Profil("Probe", new Lernplattform.Core.Daten.PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        return new StadtSpiel(stadt, ablauf, BueroFlaeche.Value);
    }

    private static Wegsuche Leer(int spalten, int zeilen, params (int, int)[] belegt)
    {
        var frei = new List<(int, int)>();
        for (var s = 0; s < spalten; s++)
        {
            for (var z = 0; z < zeilen; z++)
            {
                if (!belegt.Contains((s, z)))
                {
                    frei.Add((s, z));
                }
            }
        }
        return new Wegsuche(frei, 1.0);
    }

    [Fact]
    public void GeraderWegWirdGeglaettet()
    {
        var w = Leer(10, 3);
        var weg = w.Weg((0.5, 1.5), (8.5, 1.5));
        // freie gerade Strecke: nur der Zielpunkt bleibt
        Assert.Equal([(8.5, 1.5)], weg);
    }

    [Fact]
    public void WegGehtUmHindernisHerum()
    {
        // Wand in Spalte 3, Zeilen 0..3; nur Zeile 4 ist frei
        var w = Leer(7, 5, (3, 0), (3, 1), (3, 2), (3, 3));
        var weg = w.Weg((0.5, 0.5), (6.5, 0.5));
        Assert.NotEmpty(weg);
        Assert.Equal((6.5, 0.5), weg[^1]);
        var von = (X: 0.5, Y: 0.5);
        foreach (var p in weg)
        {
            Assert.True(w.StreckeFrei(von, p), $"Strecke {von} -> {p} schneidet die Wand");
            von = p;
        }
        Assert.True(Wegsuche.Laenge((0.5, 0.5), weg) > 6.0);
    }

    [Fact]
    public void DiagonaleSchneidetKeineEcke()
    {
        // (1,0) belegt: von (0,0) nach (1,1) darf nicht diagonal an der Ecke vorbei
        var w = Leer(2, 2, (1, 0));
        var weg = w.Weg((0.5, 0.5), (1.5, 1.5));
        // die BFS geht ueber (0,1); geglaettet bleibt die Diagonale, die (1,0) nicht beruehrt (wie Python)
        Assert.Equal([(1.5, 1.5)], weg);
        Assert.False(w.StreckeFrei((0.5, 0.5), (1.6, 0.4)));
    }

    [Fact]
    public void UnerreichbarUndSchonDaSindLeer()
    {
        var w = Leer(5, 1, (2, 0));
        Assert.Empty(w.Weg((0.5, 0.5), (4.5, 0.5)));
        Assert.Empty(w.Weg((0.5, 0.5), (0.6, 0.6)));
    }

    [Fact]
    public void NaechsteFreieBeiGleicherWeiteKleinsteZelle()
    {
        var w = Leer(3, 3, (1, 1));
        Assert.Equal((0, 1), w.NaechsteFreie(1.5, 1.5));
    }

    [Fact]
    public void SzeneHatAlleSichtbarenOrteUndEinGueltigesNetz()
    {
        var stadt = NeueStadt();
        var szene = StadtSzene.Bauen(stadt, null);
        Assert.Equal(stadt.Orte(null).Select(o => (string)o["id"]!), szene.Orte.Select(o => o.Id));
        Assert.Contains(szene.Orte, o => o.Id == "moebelhaus");
        Assert.Contains(szene.Orte, o => o.Id == "elektromarkt");
        Assert.DoesNotContain(szene.Orte, o => o.Id == "lokschuppen");   // noch nicht frei (Python place_unlocked)
        Assert.True(szene.Teile.Count > 500);
        foreach (var ort in szene.Orte)
        {
            Assert.True(ort.KastenMin.X < ort.KastenMax.X && ort.KastenMin.Y < ort.KastenMax.Y && ort.KastenMin.Z < ort.KastenMax.Z, ort.Id);
            Assert.Contains(stadt.ZelleBei(ort.TuerX, ort.TuerZ), stadt.Begehbar);
            Assert.InRange(ort.TuerX, 0, szene.Breite);
            Assert.InRange(ort.TuerZ, 0, szene.Tiefe);
        }
        var netz = Netz.Aus(szene.Teile);
        Assert.Equal(0, netz.Ecken.Count % Netz.Zahlen);
        Assert.All(netz.Indizes, i => Assert.True(i < netz.AnzahlEcken));
        Assert.All(netz.Ecken, z => Assert.True(float.IsFinite(z)));
        // gleiche Eingaben, gleiche Szene (keine Zufallswerte)
        Assert.Equal(netz.Ecken, Netz.Aus(StadtSzene.Bauen(stadt, null).Teile).Ecken);
        Assert.True(BueroFlaeche.Value.Wege.KannStehen(BueroFlaeche.Value.StartX, BueroFlaeche.Value.StartZ));
        // Buero-Raster wie fisi_game: 30 x 16 Einheiten in Zellen zu 0,25
        Assert.True(BueroFlaeche.Value.Wege.AnzahlFrei is > 1000 and < 120 * 64);
    }

    [Fact]
    public void BandLiegtFlachUndQuer()
    {
        var netz = Netz.Aus([new Teil(TeilArt.Band, 0, 0.01f, 0, 4, 1, 0, Vector3.One)]);
        Assert.Equal(4, netz.AnzahlEcken);
        var y = Enumerable.Range(0, 4).Select(i => netz.Ecken[(i * Netz.Zahlen) + 1]).Distinct().ToList();
        Assert.Equal([0.01f], y);
        var z = Enumerable.Range(0, 4).Select(i => netz.Ecken[(i * Netz.Zahlen) + 2]).OrderBy(v => v).ToList();
        Assert.Equal([-0.5f, -0.5f, 0.5f, 0.5f], z);
        Assert.Equal(1f, netz.Ecken[4]);   // Normale nach oben
    }

    [Fact]
    public void ModellteileJeSpielstand()
    {
        var inhalt = SpielInhaltTests.Inhalt;
        var modelle = StadtModelle.Modelle(inhalt, SpielStadtTests.Daten.Value);
        var ohne = StadtModelle.Kontext(null, inhalt);
        // Startwohnung "apartment": nur ihr Block und die zwei Baeume
        Assert.Equal([0, 1, 2], StadtModelle.Teile(modelle, "wohnung", ohne).Select(t => t.Nr));
        // Gewerbehof ohne Firma: nur der dunkle Block (gegruendet false)
        Assert.Equal([0], StadtModelle.Teile(modelle, "gewerbehof", ohne).Select(t => t.Nr).Take(1));
        Assert.DoesNotContain(1, StadtModelle.Teile(modelle, "gewerbehof", ohne).Select(t => t.Nr));
        Assert.True(Py.Hat(modelle, "moebelhaus") && Py.Hat(modelle, "elektromarkt"));
        var box = StadtModelle.Box(StadtModelle.Teile(modelle, "bitweiche", ohne), 11.9, 17.4);
        Assert.Equal(19.8, box.Y2, 9);
        Assert.Equal(11.9, box.X1);
    }

    [Fact]
    public void GehenKostetSpielzeitUndOeffnungszeitGilt()
    {
        var spiel = NeuesSpiel();
        Assert.Equal("Tag 1 · 07:00 Uhr", spiel.Kopfzeile);
        Assert.Equal("zuhause", spiel.Jetzt.Ort);
        // am ersten Tag sind nur Zuhause, Bitweiche, Bahnhof Talheim und die zwei Geschaefte frei (Python place_unlocked)
        Assert.Equal(StadtGrund.Gesperrt, spiel.GeheZu("lindenau"));
        var weg = spiel.Stadt.Weg("zuhause", "moebelhaus")!;
        Assert.Equal(StadtGrund.Ok, spiel.GeheZu("moebelhaus"));
        Assert.True(spiel.Geht);
        Assert.Contains(weg.Schritte + " Schritte, " + (weg.Schritte * 2) + " Minuten", spiel.Zeile);
        Assert.Equal(420 + (weg.Schritte * 2), spiel.Jetzt.Uhr);   // die Uhr springt sofort (Ereignis geschrieben)
        Assert.Equal(Stadt.Gegangen, spiel.Ablauf.Stand.Verlauf[^1].Art);
        spiel.SofortAnkommen();
        Assert.False(spiel.Geht);
        var tuer = spiel.Szene.Ort("moebelhaus")!;
        Assert.Equal((tuer.TuerX, tuer.TuerZ), (spiel.FigurX, spiel.FigurZ));
        Assert.True(spiel.Jetzt.Uhr < 600, "Moebelhaus oeffnet um 10:00");
        Assert.Equal("Möbelhaus: Geschlossen. Öffnet um 10:00 Uhr.", spiel.Zeile);
        Assert.True(spiel.KannWarten);
        Assert.False(spiel.KannBetreten);
        Assert.Equal(StadtGrund.Ok, spiel.Warten());
        Assert.Equal(600, spiel.Jetzt.Uhr);
        Assert.Equal(Stadt.Gewartet, spiel.Ablauf.Stand.Verlauf[^1].Art);
        Assert.StartsWith("Möbelhaus: geöffnet bis 20:00 Uhr.", spiel.Zeile);
        Assert.Contains("Innenraum kommt später", spiel.Zeile);
        Assert.False(spiel.KannWarten);
        Assert.False(spiel.KannBetreten);   // das Moebelhaus hat bis W7 keinen Innenraum
        Assert.Equal(StadtGrund.Gesperrt, spiel.GeheZu("lokschuppen"));
        Assert.Equal(StadtTexte.Gesperrt, spiel.Zeile);
        Assert.Equal(StadtGrund.SchonDa, spiel.GeheZu("moebelhaus"));
        Assert.Equal(StadtTexte.SchonDa, spiel.Zeile);
    }

    [Fact]
    public void NeuesZielWaehrendDesGehens()
    {
        var spiel = NeuesSpiel();
        spiel.GeheZu("bitweiche");
        spiel.Fortschreiten(0.2);
        Assert.True(spiel.Geht);
        Assert.Equal(StadtGrund.Ok, spiel.GeheZu("elektromarkt"));
        // der erste Weg zaehlt ganz (Figur springt an sein Ziel), dann der zweite von dort
        var gegangen = spiel.Ablauf.Stand.Verlauf.Where(e => e.Art == Stadt.Gegangen).ToList();
        Assert.Equal(2, gegangen.Count);
        Assert.Equal("bitweiche", gegangen[1].Daten["von"]);
        spiel.SofortAnkommen();
        Assert.Equal("elektromarkt", spiel.Jetzt.Ort);
    }

    [Fact]
    public void BueroBetretenUndVerlassen()
    {
        var spiel = NeuesSpiel();
        Assert.False(spiel.Betreten());   // zu Hause gibt es bis W5 keinen Innenraum
        spiel.GeheZu("bitweiche");
        spiel.SofortAnkommen();
        Assert.True(spiel.KannBetreten);
        Assert.True(spiel.Betreten());
        Assert.True(spiel.ImInnenraum);
        Assert.Equal(ProbeWelt.Buero, spiel.FlaecheId);
        var uhr = spiel.Jetzt.Uhr;
        Assert.True(spiel.GeheZu(20.0, 7.0));
        spiel.SofortAnkommen();
        Assert.Equal(uhr, spiel.Jetzt.Uhr);   // im Buero vergeht (bis W5) keine Spielzeit
        spiel.Verlassen();
        Assert.False(spiel.ImInnenraum);
        Assert.Equal("bitweiche", spiel.Jetzt.Ort);
        // aus dem Buero direkt zu einem anderen Ort: erst hinaus, dann gehen
        Assert.True(spiel.Betreten());
        Assert.Equal(StadtGrund.Ok, spiel.GeheZu("zuhause"));
        Assert.False(spiel.ImInnenraum);
    }

    [Fact]
    public void AbendsNurNochNachHause()
    {
        var spiel = NeuesSpiel();
        // hin und her bis zum Abend: irgendwann geht nur noch der Weg nach Hause
        var grund = StadtGrund.Ok;
        for (var i = 0; i < 200 && grund == StadtGrund.Ok; i++)
        {
            grund = spiel.GeheZu(spiel.Jetzt.Ort == "elektromarkt" ? "talheim" : "elektromarkt");
            spiel.SofortAnkommen();
        }
        Assert.Equal(StadtGrund.Abend, grund);
        Assert.Equal(StadtTexte.Abend, spiel.Zeile);
        Assert.Equal(StadtGrund.Ok, spiel.GeheZu("zuhause"));
    }

    [Fact]
    public void TippenTrifftDasHausUnterDemFinger()
    {
        var stadt = NeueStadt();
        var szene = StadtSzene.Bauen(stadt, null);
        var ort = szene.Ort("bitweiche")!;
        var k = new Kamera((ort.KastenMin.X + ort.KastenMax.X) / 2, (ort.KastenMin.Z + ort.KastenMax.Z) / 2, 17f);
        var mitte = (ort.KastenMin + ort.KastenMax) / 2;
        var p = k.Bildpunkt(mitte, 800, 450)!.Value;
        var strahl = k.Strahl(p.X, p.Y, 800, 450)!.Value;
        Assert.Equal("bitweiche", szene.OrtBei(strahl.Start, strahl.Richtung)?.Id);
        // Blick in den Himmel trifft nichts
        Assert.Null(szene.OrtBei(new Vector3(-50, 5, -50), Vector3.Normalize(new Vector3(-1, 1, 0))));
        // Kasten-Treffer: von vorn ja, daneben nein
        Assert.NotNull(StadtSzene.Treffer(new Vector3(0.5f, 0.5f, 5), new Vector3(0, 0, -1), Vector3.Zero, Vector3.One));
        Assert.Null(StadtSzene.Treffer(new Vector3(2, 0.5f, 5), new Vector3(0, 0, -1), Vector3.Zero, Vector3.One));
    }

    [Fact]
    public void KameraDrehenUndZurueckrechnen()
    {
        var k = new Kamera(10, 5, 15);
        k.Drehen(-30);
        Assert.Equal(330f, k.Drehung);
        k.Drehen(400);
        Assert.Equal(10f, k.Drehung, 3);
        foreach (var (x, y) in new[] { (400f, 300f), (100f, 500f), (700f, 200f) })
        {
            var boden = k.Boden(x, y, 800, 600)!.Value;
            var zurueck = k.Bildpunkt(new Vector3(boden.X, 0, boden.Y), 800, 600)!.Value;
            // hin und zurueck auf 1/20 Bildpunkt genau (float; macOS arm64 rechnet mit FMA, dort 499,994 statt 500)
            Assert.Equal(x, zurueck.X, 0.05f);
            Assert.Equal(y, zurueck.Y, 0.05f);
        }
    }

    [Fact]
    public void FensterLichtIstFest()
    {
        Assert.Equal(StadtSzene.Licht("bitweiche|1", 3, 0.5), StadtSzene.Licht("bitweiche|1", 3, 0.5));
        Assert.False(StadtSzene.Licht("a", 0, 0));
        Assert.True(StadtSzene.Licht("a", 0, 1));
        var an = Enumerable.Range(0, 400).Count(i => StadtSzene.Licht("x", i, 0.5));
        Assert.InRange(an, 150, 250);
    }

    [Fact]
    public void KameraTrifftDenBodenUnterDerMitte()
    {
        var k = new Kamera(10, 5, 15);
        var boden = k.Boden(400, 300, 800, 600);
        Assert.NotNull(boden);
        Assert.InRange(boden!.Value.X, 9.9, 10.1);
        Assert.InRange(boden.Value.Y, 4.9, 5.1);
    }

    [Fact]
    public void KameraZiehenUndZoomen()
    {
        var k = new Kamera(10, 5, 15);
        k.Verschieben(100, 0, 600);
        Assert.True(k.ZielX < 10);   // Boden folgt dem Finger nach rechts, das Ziel wandert nach links
        k.Verschieben(0, 100, 600);
        Assert.True(k.ZielZ < 5);
        k.Zoomen(100);
        Assert.Equal(Kamera.AbstandMin, k.Abstand);
        k.Zoomen(0.001f);
        Assert.Equal(Kamera.AbstandMax, k.Abstand);
        var begrenzt = new Kamera(0, 0, 10) { Grenzen = (0, 0, 5, 5) };
        begrenzt.Setzen(-3, 9);
        Assert.Equal((0f, 5f), (begrenzt.ZielX, begrenzt.ZielZ));
    }
}
