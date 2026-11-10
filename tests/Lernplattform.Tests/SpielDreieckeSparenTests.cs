using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle D1 „Dreiecke sparen“ (Weisung P12-143, Nico P12-118 Frage 2 = a): Fernform je Haus und fuer die Fest-Deko
/// (<see cref="Fernform"/>, Gitter 1/32), Unterseiten am Boden weg (<see cref="Netz.UnterseiteBis"/>), Kugel je Haus fuer
/// den Sichtkegel (<see cref="Netz.Kugel"/>). Das Zeichnen selbst (Sichtkegel, nah/fern) prueft der Start-Test mit Gegenprobe.
/// </summary>
public sealed class SpielDreieckeSparenTests
{
    private static StadtSzene Weihnachten()
    {
        var stadt = new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
        var ablauf = new Spielablauf(stadt.Inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" } }, Ereignisse.Normal);
        return StadtSzene.Bauen(stadt, ablauf.Stand, SpielGrafikTests.Bibliothek.Value, Festzeit.Weihnachten, FestDekoTests.Daten.Value, Jahreszeit.Winter);
    }

    private static long Dreiecke(Netz n) => n.Indizes.Count / 3;

    private static byte[] Bytes(GrafikModell m) =>
        [.. m.Ecken.SelectMany(BitConverter.GetBytes), .. m.Farben, .. m.EckFarben ?? []];

    [Fact]
    public void Fernform_GleichBeiJedemBau_KleinerUndUmrissBleibt()
    {
        var grafik = SpielGrafikTests.Bibliothek.Value;
        var szene = Weihnachten();
        var namen = szene.Stuecke.Select(s => s.Name).Distinct().Where(n => grafik.Modell(n) is not null).ToList();
        Assert.NotEmpty(namen);
        long vorher = 0, nachher = 0;
        foreach (var name in namen)
        {
            var m = grafik.Modell(name)!;
            var a = Fernform.Bauen(m);
            Assert.Equal(Bytes(a), Bytes(Fernform.Bauen(m)));
            Assert.Same(Fernform.Von(m), Fernform.Von(m));
            Assert.True(a.Dreiecke <= m.Dreiecke, name);
            Assert.Equal(m.Name, a.Name);
            if (a.Dreiecke > 0)
            {
                // der Umriss bleibt (Ecken am Rand bleiben auf dem Rand), also auch der Fuss auf dem Boden
                Assert.Equal(m.Min, a.Min);
                Assert.Equal(m.Max, a.Max);
            }
            vorher += m.Dreiecke;
            nachher += a.Dreiecke;
        }
        Assert.True(nachher * 2 < vorher, $"{nachher} von {vorher}");
    }

    [Fact]
    public void Fernform_KleinesModellBleibt()
    {
        var m = new GrafikModell("klein", [0, 0, 0, 1, 0, 0, 0, 1, 0], [10, 20, 30]);
        Assert.Same(m, Fernform.Bauen(m));
    }

    [Fact]
    public void Unterseiten_NurNachUntenUndAmBoden_Gegenprobe()
    {
        // Dreieck 0: am Boden nach unten (faellt weg); 1: am Boden nach oben; 2: hoch oben nach unten (bleibt)
        float[] ecken =
        [
            0, 0, 0, 1, 0, 0, 0, 0, 1,
            0, 0, 0, 0, 0, 1, 1, 0, 0,
            0, 2, 0, 1, 2, 0, 0, 2, 1,
        ];
        var m = new GrafikModell("probe", ecken, [1, 1, 1, 2, 2, 2, 3, 3, 3]);
        Assert.True(Vector3.Cross(new Vector3(1, 0, 0), new Vector3(0, 0, 1)).Y < 0);
        var mit = new Netz { UnterseiteBis = StadtSzene.UnterseiteBis };
        mit.Hinzu(m, Matrix4x4.Identity);
        Assert.Equal(2, Dreiecke(mit));
        Assert.Equal(1, mit.UnterseitenWeg);
        var ohne = new Netz();
        ohne.Hinzu(m, Matrix4x4.Identity);
        Assert.Equal(3, Dreiecke(ohne));
        Assert.Equal(0, ohne.UnterseitenWeg);
        // hochgestellt (auf 0,5): nichts faellt weg
        var hoch = new Netz { UnterseiteBis = StadtSzene.UnterseiteBis };
        hoch.Hinzu(m, Matrix4x4.CreateTranslation(0, 0.5f, 0));
        Assert.Equal(3, Dreiecke(hoch));
    }

    [Fact]
    public void Stadt1_FernSpart_NahBleibtBisAufUnterseiten()
    {
        // Welle D2: die vollen Netze wie in D1 (das Instancing nimmt sonst Stuecke heraus, SpielInstanzenTests)
        Instanzen.AusFuerTests = true;
        var szene = Weihnachten();
        var f = szene.Vorbereiten();
        Assert.NotNull(f.StadtFern);
        Assert.NotNull(f.HaeuserFern);
        // Fest-Deko fern (Teil C) und Haeuser fern (Teil B)
        Assert.True(Dreiecke(f.StadtFern!) * 10 < Dreiecke(f.Stadt) * 8, $"Stadt {Dreiecke(f.Stadt)}, fern {Dreiecke(f.StadtFern!)}");
        long nah = f.Haeuser.Values.Sum(Dreiecke), fern = f.HaeuserFern!.Values.Sum(Dreiecke);
        Assert.True(fern * 2 < nah, $"Haeuser {nah}, fern {fern}");
        Assert.Equal(f.Haeuser.Keys.ToHashSet(), f.HaeuserFern.Keys.ToHashSet());
        foreach (var haus in szene.Haeuser)
        {
            Assert.True(Dreiecke(f.HaeuserFern[haus]) <= Dreiecke(f.Haeuser[haus]), haus.Id);
            Assert.True(szene.HausNetz(haus, fern: true).EckenFeld.SequenceEqual(f.HaeuserFern[haus].EckenFeld), haus.Id);
            // die Kugel des Sichtkegels enthaelt jede Ecke des Hauses (nah und fern)
            var (mitte, halb) = f.Haeuser[haus].Kugel();
            foreach (var netz in new[] { f.Haeuser[haus], f.HaeuserFern[haus] })
            {
                for (var e = 0; e < netz.AnzahlEcken; e++)
                {
                    var p = new Vector3(netz.Ecken[e * Netz.Zahlen], netz.Ecken[(e * Netz.Zahlen) + 1], netz.Ecken[(e * Netz.Zahlen) + 2]);
                    Assert.True(Vector3.Distance(p, mitte) <= halb * 1.0001f + 1e-4f, haus.Id);
                }
            }
        }
        Assert.True(szene.NetzBauen(fern: true).EckenFeld.SequenceEqual(f.StadtFern!.EckenFeld));
        // Teil D: Unterseiten am Boden fallen weg, sonst bleibt nah alles (nur nach unten zeigende Dreiecke fehlen)
        Assert.True(f.Stadt.UnterseitenWeg > 0);
        Assert.True(f.Haeuser.Values.Sum(n => n.UnterseitenWeg) > 0);
        Assert.Equal(-1f, new Netz().Kugel().Halb);
    }
}
