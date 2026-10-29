using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W10 (Weisung P8-34, Nico 3b): Tag und Nacht in der Stadt. Licht nach der Tagesuhr der Stadt und der
/// Jahreszeit (am Tag genau wie vorher), nachts leuchten Fenster mit Licht und die Strassenlaternen (W10: 30 im Netz
/// der Stadt; seit W11 21 nach Nicos Skizze im eigenen Netz). Nur Darstellung.
/// </summary>
public sealed class SpielTageslichtTests
{
    private static readonly Jahreszeit[] Alle = [Jahreszeit.Fruehling, Jahreszeit.Sommer, Jahreszeit.Herbst, Jahreszeit.Winter];

    private static GrafikBibliothek Grafik => SpielGrafikTests.Bibliothek.Value;

    private static Stadt NeueStadt() => new(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);

    /// <summary>Spielstand mit allen Orten (wie StadtTuerenTests: Tag 201, Firma Stufe 5 mit Zweigstelle).</summary>
    private static Spielstand Spaet()
    {
        var ablauf = new Spielablauf(SpielInhaltTests.Inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        ablauf.Stand.TageFertig = 200;
        ablauf.Stand.Firma = new PyObjekt { { "stufe", 5L }, { "name", "Probe" }, { "tag", 1L } };
        ablauf.Stand.Zweigstelle = new PyObjekt { { "stufe", 1L } };
        return ablauf.Stand;
    }

    [Fact]
    public void AmTagWieVorher()
    {
        var tag = Tageslicht.Tag;
        Assert.Equal(1f, tag.Hell);
        Assert.Equal(Vector3.One, tag.Ton);
        Assert.Equal(0f, tag.Nacht);
        foreach (var j in Alle)
        {
            for (var uhr = Tageslicht.MorgenBis(j); uhr <= Tageslicht.DaemmerungAb(j); uhr += 5)
            {
                Assert.Equal(tag, Tageslicht.Fuer(uhr, j));
            }
        }
    }

    [Fact]
    public void GrenzenJeJahreszeit()
    {
        Assert.Equal(960, Tageslicht.DaemmerungAb(Jahreszeit.Winter));
        Assert.Equal(1050, Tageslicht.DaemmerungAb(Jahreszeit.Fruehling));
        Assert.Equal(1050, Tageslicht.DaemmerungAb(Jahreszeit.Herbst));
        Assert.Equal(1110, Tageslicht.DaemmerungAb(Jahreszeit.Sommer));
        Assert.Equal(510, Tageslicht.MorgenBis(Jahreszeit.Winter));
        Assert.Equal(480, Tageslicht.MorgenBis(Jahreszeit.Herbst));
        Assert.Equal(1200, Tageslicht.NachtAb);
        foreach (var j in Alle)
        {
            // Morgen dunkler und warm, Winter deutlich dunkler mit ersten Fenstern
            var morgen = Tageslicht.Fuer(420, j);
            Assert.True(morgen.Hell < 1f && morgen.Hell >= 0.6f);
            Assert.Equal(j == Jahreszeit.Winter ? 0.6f : 0f, morgen.Nacht);
            // Daemmerung wird stetig dunkler, die Fenster gehen ab der Haelfte an
            var vorher = 1f;
            for (var uhr = Tageslicht.DaemmerungAb(j) + 1; uhr < Tageslicht.NachtAb; uhr++)
            {
                var l = Tageslicht.Fuer(uhr, j);
                Assert.True(l.Hell < vorher && l.Hell > 0.42f, $"{j} {uhr}");
                vorher = l.Hell;
            }
            Assert.Equal(0f, Tageslicht.Fuer(Tageslicht.DaemmerungAb(j) + 1, j).Nacht);
            // Nacht von 20:00 bis die Uhr steht (23:00)
            for (var uhr = Tageslicht.NachtAb; uhr <= Stadt.UhrSteht; uhr++)
            {
                var n = Tageslicht.Fuer(uhr, j);
                Assert.Equal(0.42f, n.Hell);
                Assert.Equal(1f, n.Nacht);
            }
        }
        // Winter wird frueher dunkel als Sommer
        Assert.True(Tageslicht.Fuer(1080, Jahreszeit.Winter).Hell < Tageslicht.Fuer(1080, Jahreszeit.Sommer).Hell);
    }

    [Fact]
    public void Ueberblenden()
    {
        var tag = Tageslicht.Tag;
        var nacht = Tageslicht.Fuer(1300, Jahreszeit.Herbst);
        Assert.Equal(tag, Tageslicht.Mischen(tag, nacht, 0f));
        Assert.Equal(nacht, Tageslicht.Mischen(tag, nacht, 1f));
        Assert.Equal(nacht, Tageslicht.Mischen(tag, nacht, 3f));
        var mitte = Tageslicht.Mischen(tag, nacht, 0.5f);
        Assert.Equal((tag.Hell + nacht.Hell) / 2, mitte.Hell, 5);
        Assert.Equal(0.5f, mitte.Nacht, 5);
        Assert.Equal(TimeSpan.FromSeconds(2), Tageslicht.Ueberblenden);
    }

    /// <summary>
    /// W10 (3b), seit W11 (Nico P8-45): die Laternen stehen nicht mehr im Netz der Stadt, sondern im eigenen Netz
    /// (<see cref="StadtSzene.PaketNetzBauen"/>); im Netz der Stadt leuchtet nichts mehr (vorher genau die Lampen).
    /// Platzregeln, Anzahl und Leuchtmarke pruefen die W11-Tests (<see cref="StadtDarstellungTests"/>).
    /// </summary>
    [Fact]
    public void LaternenImEigenenNetz()
    {
        var stadt = NeueStadt();
        var stand = Spaet();
        var szene = StadtSzene.Bauen(stadt, stand, Grafik);
        var laternen = szene.Laternen;
        Assert.Equal(Nachtlicht.Anzahl, laternen.Count);
        // fest fuer dieselbe Stadt
        Assert.Equal(laternen, StadtSzene.Bauen(stadt, stand, Grafik).Laternen);
        var netz = szene.NetzBauen();
        for (var e = 0; e < netz.AnzahlEcken; e++)
        {
            Assert.False(Nachtlicht.Leuchtet(netz.FarbeAn(e)), "im Netz der Stadt leuchtet etwas");
        }
        var paket = szene.PaketNetzBauen(false);
        var leuchtend = 0;
        for (var e = 0; e < paket.AnzahlEcken; e++)
        {
            leuchtend += Nachtlicht.Leuchtet(paket.FarbeAn(e)) ? 1 : 0;
        }
        Assert.True(leuchtend > 0 && leuchtend < paket.AnzahlEcken / 4, $"{leuchtend} von {paket.AnzahlEcken} Ecken leuchten");
    }

    [Theory]
    [InlineData(Jahreszeit.Sommer)]
    [InlineData(Jahreszeit.Winter)]
    public void FensterMitLichtLeuchten(Jahreszeit j)
    {
        var szene = StadtSzene.Bauen(NeueStadt(), Spaet(), Grafik, jahreszeit: j);
        Assert.NotEmpty(szene.LichtTeile);
        foreach (var i in szene.LichtTeile)
        {
            Assert.NotEqual(szene.Teile[i].Farbe, Netz.Farbe("#5B6B8C"));
        }
        var markiert = 0;
        var gesamt = 0;
        foreach (var haus in szene.Haeuser)
        {
            var netz = szene.HausNetz(haus);
            gesamt += netz.AnzahlEcken;
            for (var e = 0; e < netz.AnzahlEcken; e++)
            {
                var f = netz.FarbeAn(e);
                if (Nachtlicht.Leuchtet(f))
                {
                    markiert++;
                    // ohne Marke eine gewoehnliche Farbe (am Tag wie vorher), nie zusaetzlich Schnee
                    var ohne = f - new Vector3(Nachtlicht.Marke);
                    Assert.True(ohne.X is >= 0f and <= 1f && ohne.Y is >= 0f and <= 1f && ohne.Z is >= 0f and <= 1f, $"{haus.Id}: {f}");
                }
            }
        }
        Assert.True(markiert > 0 && markiert < gesamt / 4, $"{markiert} von {gesamt}");
    }

    /// <summary>
    /// Weisung P8-34: kein gelber Strich an der Seitenkante (Bank): nachts leuchtet nur die Scheibe, nie die schmale
    /// Kante eines Fensterblocks oder einer Glasscheibe. Kein leuchtendes Dreieck liegt flach (Dachfenster der Modelle
    /// sind schraeg) und jedes ist quer zu seiner Normalen waagrecht mindestens <see cref="Nachtlicht.GlasMindestens"/> breit.
    /// </summary>
    [Theory]
    [InlineData(Jahreszeit.Herbst)]
    [InlineData(Jahreszeit.Winter)]
    public void SeitenkantenLeuchtenNie(Jahreszeit j)
    {
        var szene = StadtSzene.Bauen(NeueStadt(), Spaet(), Grafik, jahreszeit: j);
        var geprueft = 0;
        foreach (var haus in szene.Haeuser)
        {
            var netz = szene.HausNetz(haus);
            var ind = netz.IndizesFeld;
            for (var i = 0; i + 2 < ind.Length; i += 3)
            {
                var e = (int)ind[i];
                if (!Nachtlicht.Leuchtet(netz.FarbeAn(e)))
                {
                    continue;
                }
                var n = netz.Normale(e);
                Assert.True(MathF.Abs(n.Y) < 0.9f, $"{haus.Id}: leuchtende Flaeche liegt flach ({n})");
                var quer = Vector3.Normalize(new Vector3(-n.Z, 0, n.X));
                var a = Vector3.Dot(netz.Ort(e), quer);
                var b = Vector3.Dot(netz.Ort((int)ind[i + 1]), quer);
                var c = Vector3.Dot(netz.Ort((int)ind[i + 2]), quer);
                var breite = MathF.Max(a, MathF.Max(b, c)) - MathF.Min(a, MathF.Min(b, c));
                Assert.True(breite >= Nachtlicht.GlasMindestens - 0.001f, $"{haus.Id}: leuchtende Kante {breite:F3} breit bei {netz.Ort(e)}");
                geprueft++;
            }
        }
        Assert.True(geprueft > 100, geprueft.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void GlaskantenLeuchtenNie()
    {
        // eine Scheibe 0,3 x 0,3 (0,005 dick) und daneben die Kante einer Scheibe 0,02 x 0,3 (Seitenkante wie an der Bank)
        var glas = new Vector3(0.55f, 0.75f, 0.95f);
        Assert.True(StadtTueren.IstGlas(glas));
        var an = 0;
        for (var n = 0; n < 40; n++)
        {
            var netz = new Netz();
            var x = n * 2f;
            netz.Hinzu(new Teil(TeilArt.Quader, x, 1f, 0f, x + 0.3f, 1.3f, 0.005f, glas));
            var kante = netz.AnzahlEcken;
            netz.Hinzu(new Teil(TeilArt.Quader, x + 1f, 1f, 0f, x + 1.005f, 1.3f, 0.02f, glas));
            var ende = netz.AnzahlEcken;
            Nachtlicht.GlasMarkieren(netz, 0, "probe" + n);
            if (Enumerable.Range(0, kante).Any(e => Nachtlicht.Leuchtet(netz.FarbeAn(e))))
            {
                an++;
            }
            for (var e = kante; e < ende; e++)
            {
                Assert.False(Nachtlicht.Leuchtet(netz.FarbeAn(e)), "Glaskante leuchtet");
            }
        }
        // etwa jede zweite Scheibe (Wuerfel je Scheibe)
        Assert.InRange(an, 10, 30);
    }
}
