using System.Diagnostics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W1 (3D-Stichprobe): Wegsuche wie fisi_game.walk_path, Kamera (Boden unter dem
/// Finger, Zoomgrenzen), Welt aus orte.json/gebaeude.json, Probe-Uhr und Oeffnungszeiten.
/// Der Vergleich der Wegsuche mit Python-Werten (G48) kommt mit W2.
/// </summary>
public class SpielProbeTests
{
    private static ProbeWelt Welt() =>
        ProbeWelt.Bauen(name => File.ReadAllBytes(Path.Combine(Pfade.Inhalte, "spiel", name + ".json")));

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
    public void WeltHatDreiHaeuserUndBegehbareFlaechen()
    {
        var welt = Welt();
        Assert.Equal(["zuhause", "bitweiche", "talheim"], welt.Haeuser.Select(h => h.Id));
        Assert.Single(welt.Haeuser, h => h.HatInnenraum);
        Assert.True(welt.StadtFlaeche.Teile.Count > 20);
        Assert.True(welt.BueroFlaeche.Teile.Count > 20);
        foreach (var haus in welt.Haeuser)
        {
            Assert.True(welt.StadtFlaeche.Wege.KannStehen(haus.TuerX, haus.TuerZ), haus.Id + ": vor der Tuer ist kein Platz");
        }
        Assert.True(welt.BueroFlaeche.Wege.KannStehen(welt.BueroFlaeche.StartX, welt.BueroFlaeche.StartZ));
        // Buero-Raster wie fisi_game: 30 x 16 Einheiten in Zellen zu 0,25
        Assert.True(welt.BueroFlaeche.Wege.AnzahlFrei is > 1000 and < 120 * 64);
        var netz = Netz.Aus(welt.StadtFlaeche.Teile);
        Assert.Equal(0, netz.Ecken.Count % Netz.Zahlen);
        Assert.All(netz.Indizes, i => Assert.True(i < netz.AnzahlEcken));
    }

    [Fact]
    public void AlleHaeuserSindVonZuhauseErreichbar()
    {
        var welt = Welt();
        var start = welt.Haeuser[0];
        foreach (var haus in welt.Haeuser.Skip(1))
        {
            var weg = welt.StadtFlaeche.Wege.Weg((start.TuerX, start.TuerZ), (haus.TuerX, haus.TuerZ));
            Assert.NotEmpty(weg);
        }
    }

    [Fact]
    public void WegSuchenUnterGrenze()
    {
        var welt = Welt();
        var (a, b) = (welt.Haeuser[0], welt.Haeuser[^1]);
        var beste = double.MaxValue;
        for (var i = 0; i < 3; i++)
        {
            var uhr = Stopwatch.StartNew();
            welt.StadtFlaeche.Wege.Weg((a.TuerX, a.TuerZ), (b.TuerX, b.TuerZ));
            beste = Math.Min(beste, uhr.Elapsed.TotalMilliseconds);
        }
        Assert.True(beste < 50, $"Weg suchen {beste:F1} ms");
    }

    [Fact]
    public void GehenKostetSpielzeitUndOeffnungszeitGilt()
    {
        var spiel = new ProbeSpiel(Welt());
        Assert.Equal("08:00", spiel.Uhrzeit);
        var bahnhof = spiel.Welt.Haeuser[^1];
        Assert.True(spiel.GeheZu(bahnhof));
        spiel.SofortAnkommen();
        Assert.True(spiel.Minute > ProbeWelt.StartMinute);
        Assert.Same(bahnhof, spiel.VorHaus);
        Assert.Contains("geschlossen, öffnet um 10:00 Uhr", spiel.Zeile);
        Assert.False(spiel.KannBetreten);
        spiel.Warten(120);
        Assert.Contains("geöffnet bis 18:00 Uhr", spiel.Zeile);
        Assert.False(spiel.KannBetreten);   // Bahnhof hat in der Probe keinen Innenraum
    }

    [Fact]
    public void BueroBetretenUndVerlassen()
    {
        var spiel = new ProbeSpiel(Welt());
        var buero = spiel.Welt.Haeuser.First(h => h.HatInnenraum);
        spiel.GeheZu(buero);
        spiel.SofortAnkommen();
        Assert.True(spiel.KannBetreten);
        Assert.True(spiel.Betreten());
        Assert.True(spiel.ImInnenraum);
        Assert.Equal(ProbeWelt.Buero, spiel.Flaeche.Id);
        Assert.True(spiel.GeheZu(20.0, 7.0));
        spiel.SofortAnkommen();
        spiel.Verlassen();
        Assert.False(spiel.ImInnenraum);
        Assert.Same(buero, spiel.VorHaus);
    }

    [Theory]
    [InlineData(480, true, 0)]
    [InlineData(1140, false, 720)]          // 19:00 geschlossen, oeffnet morgen um 7:00
    [InlineData(1440 + 420, true, 0)]       // naechster Tag 7:00
    [InlineData(1440 + 300, false, 120)]    // naechster Tag 5:00
    public void OeffnungszeitGiltJedenTag(int minute, bool offen, int bis)
    {
        var haus = new ProbeHaus("x", "X", 0, 0, 420, 1140, true);
        Assert.Equal(offen, haus.Offen(minute));
        Assert.Equal(bis, haus.MinutenBisOffen(minute));
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.01, 1)]
    [InlineData(0.5, 1)]
    [InlineData(0.51, 2)]
    [InlineData(10.0, 20)]
    public void MinutenNachWeglaenge(double laenge, int minuten)
    {
        Assert.Equal(minuten, ProbeWelt.Minuten(laenge));
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
