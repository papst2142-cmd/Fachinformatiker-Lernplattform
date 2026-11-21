using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Spiel.Probe;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.0.0 (Sammelliste 403, Nico: „die Figur geht ueber das Stegende aufs Wasser“). Nicos Ziel: an keinem Steg und
/// keinem Ufer der Stadt steht die Figur auf Wasser (Mitte der Figur in einem Teich und nicht auf dem Steg). Geprueft fuer die
/// aktive Stadt (Fassung 2) auf drei Wegen: jede Stelle rund um jeden Teich im Abstand 0,02, Gehen mit gehaltener Richtung
/// ueber das Ende jedes Stegs hinaus und am Ufer entlang, Antippen eines Ziels im Wasser. Gegenprobe ueber
/// <see cref="StadtSzene.StegAltFuerTests"/> (Steg wie bis A 0.99.0).
/// </summary>
[Collection("Haenger")]
public sealed class SpielStegTests
{
    private static StadtSpiel Stadt() => SpielMitfahrtTests.StadtNachFassung(2);

    /// <summary>Steht die Mitte der Figur auf Wasser (in einem Teich und auf keinem Steg)?</summary>
    internal static bool AufWasser(StadtSzene sz, Vector2 p) =>
        sz.Teiche.Any(t => Wasser.Drin(t.Rund, p))
        && !sz.Stege.Any(s => s.Rechteck is var (x1, z1, x2, z2) && p.X >= x1 && p.X <= x2 && p.Y >= z1 && p.Y <= z2);

    private static bool Steht(StadtSzene sz, double x, double z) => sz.Laufraster.KannStehen(x, z) && !sz.Laufraster.FeinGesperrt(x, z);

    /// <summary>Alle Funde der drei Wege (hoechstens 10 je Weg) und die Zahl der Proben.</summary>
    private static (List<string> Funde, int Proben, int Stege) Pruefen(StadtSpiel spiel)
    {
        var sz = spiel.Szene;
        var funde = new List<string>();
        var proben = 0;
        string F(string art, Vector2 p) => string.Create(CultureInfo.InvariantCulture, $"{art}: auf Wasser bei {p.X:F2}/{p.Y:F2}");
        // 1) jede Stelle rund um jeden Teich (0,5 ueber das Ufer hinaus), an der die Figur stehen darf
        foreach (var t in sz.Teiche)
        {
            var (x1, x2) = (t.MitUfer.Min(p => p.X) - 0.5f, t.MitUfer.Max(p => p.X) + 0.5f);
            var (z1, z2) = (t.MitUfer.Min(p => p.Y) - 0.5f, t.MitUfer.Max(p => p.Y) + 0.5f);
            var raster = 0;
            for (var x = x1; x <= x2; x += 0.02f)
            {
                for (var z = z1; z <= z2; z += 0.02f)
                {
                    proben++;
                    if (Steht(sz, x, z) && AufWasser(sz, new Vector2(x, z)) && raster++ < 10)
                    {
                        funde.Add(F("Stelle", new Vector2(x, z)));
                    }
                }
            }
        }
        // 2) Gehen: vom Landende jedes Stegs 6 s geradeaus ueber das andere Ende hinaus, dann 6 s schraeg; und am Ufer entlang
        var gehen = 0;
        void Pruefe(string art)
        {
            var p = new Vector2(spiel.FigurX, spiel.FigurZ);
            proben++;
            if (AufWasser(sz, p) && gehen++ < 10)
            {
                funde.Add(F(art, p));
            }
        }
        foreach (var steg in sz.Stege)
        {
            var (a, b) = steg.Enden;
            var land = StadtSzene.StegEndeAmLand(steg, sz.Teiche, true) ? a : b;
            var wasser = land == a ? b : a;
            var richtung = Vector2.Normalize(wasser - land);
            var start = land + (richtung * 0.2f);
            foreach (var (rx, rz) in new[] { (richtung.X, richtung.Y), (richtung.X + 0.4f, richtung.Y), (richtung.X - 0.4f, richtung.Y), (richtung.X, richtung.Y + 0.4f), (richtung.X, richtung.Y - 0.4f) })
            {
                spiel.StelleFuerTests(start.X, start.Y);
                var laenge = Math.Sqrt((rx * rx) + (rz * rz));
                for (var i = 0; i < 180; i++)
                {
                    spiel.FreiGehen(rx / laenge, rz / laenge, 1.0 / 30);
                    Pruefe("Gehen ueber das Stegende");
                }
                spiel.FreiGehen(0, 0, 1);
            }
            // 3) Antippen eines Ziels hinter dem Ende im Wasser: der Weg endet nie auf Wasser
            var ziel = wasser + (richtung * 0.3f);
            var weg = sz.Laufraster.Weg((start.X, start.Y), (ziel.X, ziel.Y));
            foreach (var w in weg)
            {
                proben++;
                if (AufWasser(sz, new Vector2((float)w.X, (float)w.Y)) && funde.Count < 40)
                {
                    funde.Add(F("Antippen", new Vector2((float)w.X, (float)w.Y)));
                }
            }
        }
        // am Ufer entlang: von jeder Ecke des Ufers ein Stueck nach aussen, dann in Richtung Teichmitte und quer dazu gehen
        foreach (var t in sz.Teiche)
        {
            var mitte = new Vector2(t.Rund.Average(p => p.X), t.Rund.Average(p => p.Y));
            foreach (var ecke in t.MitUfer.Where((_, i) => i % 2 == 0))
            {
                var aussen = Vector2.Normalize(ecke - mitte);
                var start = ecke + (aussen * 0.4f);
                if (sz.Laufraster.NaechsteFreie(start.X, start.Y) is not { } zelle)
                {
                    continue;
                }
                var (sx, sz0) = sz.Laufraster.Mitte(zelle);
                foreach (var r in new[] { -aussen, new Vector2(-aussen.Y, aussen.X) - (aussen * 0.5f), new Vector2(aussen.Y, -aussen.X) - (aussen * 0.5f) })
                {
                    var n = Vector2.Normalize(r);
                    spiel.StelleFuerTests(sx, sz0);
                    for (var i = 0; i < 120; i++)
                    {
                        spiel.FreiGehen(n.X, n.Y, 1.0 / 30);
                        Pruefe("Am Ufer");
                    }
                    spiel.FreiGehen(0, 0, 1);
                }
            }
        }
        return (funde, proben, sz.Stege.Count);
    }

    [Fact]
    public void Stadt_AnKeinemStegUndUfer_FigurAufWasser_Gegenprobe()
    {
        var (funde, proben, stege) = Pruefen(Stadt());
        Assert.True(stege >= 1, "die Stadt hat keinen Steg mehr: Test pruefte nichts");
        Assert.True(proben >= 10000, "zu wenige Proben: " + proben);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        StadtSzene.StegAltFuerTests = true;
        try
        {
            var (alt, _, _) = Pruefen(Stadt());
            Assert.True(alt.Count > 0, "Gegenprobe: wie bis A 0.99.0 steht die Figur hinter dem Stegende auf dem Wasser");
        }
        finally
        {
            StadtSzene.StegAltFuerTests = false;
        }
    }

    [Fact]
    public void Steg_LandendeErkannt_FigurKommtBisAnsEnde()
    {
        var spiel = Stadt();
        var sz = spiel.Szene;
        foreach (var steg in sz.Stege)
        {
            // genau ein Ende am Land (der Steg der Stadt ragt vom Ufer hinein)
            Assert.NotEqual(StadtSzene.StegEndeAmLand(steg, sz.Teiche, true), StadtSzene.StegEndeAmLand(steg, sz.Teiche, false));
            var (a, b) = steg.Enden;
            var land = StadtSzene.StegEndeAmLand(steg, sz.Teiche, true) ? a : b;
            var wasser = land == a ? b : a;
            var richtung = Vector2.Normalize(wasser - land);
            var start = land + (richtung * 0.2f);
            spiel.StelleFuerTests(start.X, start.Y);
            for (var i = 0; i < 180; i++)
            {
                spiel.FreiGehen(richtung.X, richtung.Y, 1.0 / 30);
            }
            spiel.FreiGehen(0, 0, 1);
            // die Figur kommt bis nahe ans Ende (nicht schon frueher gesperrt) und nie darueber hinaus
            var bisEnde = Vector2.Dot(wasser - new Vector2(spiel.FigurX, spiel.FigurZ), richtung);
            Assert.True(bisEnde is >= -0.02f and <= 0.3f, string.Create(CultureInfo.InvariantCulture, $"Abstand zum Ende {bisEnde:F3}"));
        }
    }
}
