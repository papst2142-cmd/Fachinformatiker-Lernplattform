using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W9 (Weisungen P8-31, P8-32, Nico 1a 2a 3c 5b): Farben der Stadt je Jahreszeit, nur Darstellung. Sommer
/// bitgleich wie vorher, jede Jahreszeit dieselben Ecken (nur andere Farben), Fest-Deko, Waende und Strassen
/// unveraendert, Schnee auf Daechern und Pflanzen, Nadelbaeume im Herbst gruen, an jedem Tag gleich; Szene,
/// Laufraster und Signatur haengen nicht an der Jahreszeit.
/// </summary>
public sealed class SpielJahreszeitFarbenTests
{
    private static GrafikBibliothek Grafik => SpielGrafikTests.Bibliothek.Value;

    private static Stadt NeueStadt() => new(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);

    private static StadtSzene Bauen(Jahreszeit j, string? fest = null, bool mitGrafik = true) =>
        StadtSzene.Bauen(NeueStadt(), null, mitGrafik ? Grafik : null, fest, FestDekoTests.Daten.Value, j);

    public static TheoryData<Jahreszeit> Andere => [Jahreszeit.Fruehling, Jahreszeit.Herbst, Jahreszeit.Winter];

    private static List<Vector3> Farben(Netz n)
    {
        var e = n.EckenFeld;
        var liste = new List<Vector3>(n.AnzahlEcken);
        for (var i = 0; i < e.Length; i += Netz.Zahlen)
        {
            liste.Add(new Vector3(e[i + 6], e[i + 7], e[i + 8]));
        }
        return liste;
    }

    private static float[] OhneFarben(Netz n)
    {
        var e = n.EckenFeld.ToArray();
        for (var i = 0; i < e.Length; i += Netz.Zahlen)
        {
            e[i + 6] = e[i + 7] = e[i + 8] = 0;
        }
        return e;
    }

    private static IEnumerable<Netz> AlleNetze(StadtSzene s) =>
        new[] { s.NetzBauen(), s.GruenNetzBauen()! }.Concat(s.Haeuser.Select(s.HausNetz));

    /// <summary>Sommer = genau die Netze wie vor W9 (ohne Jahreszeit gebaut, Vorgabe), auch mit Festzeit und ohne Grafik.</summary>
    [Fact]
    public void SommerBitgleich()
    {
        foreach (var (fest, grafik) in new (string?, GrafikBibliothek?)[] { (null, Grafik), (Festzeit.Weihnachten, Grafik), (null, null) })
        {
            var vorher = StadtSzene.Bauen(NeueStadt(), null, grafik, fest, FestDekoTests.Daten.Value);
            var sommer = StadtSzene.Bauen(NeueStadt(), null, grafik, fest, FestDekoTests.Daten.Value, Jahreszeit.Sommer);
            Assert.Equal(Jahreszeit.Sommer, vorher.Jahreszeit);
            Assert.Equal(vorher.NetzBauen().EckenFeld.ToArray(), sommer.NetzBauen().EckenFeld.ToArray());
            Assert.Equal(vorher.NetzBauen().IndizesFeld.ToArray(), sommer.NetzBauen().IndizesFeld.ToArray());
            foreach (var (a, b) in vorher.Haeuser.Zip(sommer.Haeuser))
            {
                Assert.Equal(vorher.HausNetz(a).EckenFeld.ToArray(), sommer.HausNetz(b).EckenFeld.ToArray());
            }
            Assert.Equal(vorher.GruenNetzBauen()?.EckenFeld.ToArray(), sommer.GruenNetzBauen()?.EckenFeld.ToArray());
        }
    }

    /// <summary>Jede Jahreszeit: dieselbe Szene (Teile, Modelle, Laufraster, Signatur) und dieselben Ecken, nur die Farben sind anders.</summary>
    [Theory]
    [MemberData(nameof(Andere))]
    public void NurFarbenAnders(Jahreszeit j)
    {
        var sommer = Bauen(Jahreszeit.Sommer);
        var szene = Bauen(j);
        Assert.Equal(sommer.Signatur, szene.Signatur);
        Assert.Equal(sommer.Teile, szene.Teile);
        Assert.Equal(sommer.Stuecke, szene.Stuecke);
        Assert.Equal(sommer.Laufraster.AnzahlFrei, szene.Laufraster.AnzahlFrei);
        for (var x = 0.1; x < sommer.Breite; x += 0.37)
        {
            for (var z = 0.1; z < sommer.Tiefe; z += 0.37)
            {
                Assert.Equal(sommer.Laufraster.KannStehen(x, z), szene.Laufraster.KannStehen(x, z));
            }
        }
        var geaendert = 0;
        foreach (var (a, b) in AlleNetze(sommer).Zip(AlleNetze(szene)))
        {
            Assert.Equal(OhneFarben(a), OhneFarben(b));
            Assert.Equal(a.IndizesFeld.ToArray(), b.IndizesFeld.ToArray());
            geaendert += Farben(a).Zip(Farben(b)).Count(p => p.First != p.Second);
        }
        Assert.True(geaendert > 10_000, j + ": zu wenig gefaerbt: " + geaendert);
    }

    /// <summary>An jedem Bau gleich (Laub und Blueten je Baum fest nach seiner Lage).</summary>
    [Theory]
    [MemberData(nameof(Andere))]
    public void JedesMalGleich(Jahreszeit j)
    {
        foreach (var (a, b) in AlleNetze(Bauen(j)).Zip(AlleNetze(Bauen(j))))
        {
            Assert.Equal(a.EckenFeld.ToArray(), b.EckenFeld.ToArray());
        }
    }

    /// <summary>Die Fest-Deko behaelt in jeder Jahreszeit ihre Farben (Netz der Stadt ab dem ersten Fest-Teil gleich wie im Sommer).</summary>
    [Theory]
    [MemberData(nameof(Andere))]
    public void FestDekoUnveraendert(Jahreszeit j)
    {
        foreach (var fest in Festzeit.Alle)
        {
            var sommer = Bauen(Jahreszeit.Sommer, fest);
            var szene = Bauen(j, fest);
            Assert.True(szene.FestTeileVon < szene.Teile.Count || szene.FestStueckeVon < szene.Stuecke.Count, fest + ": keine Deko");
            // Netze nur aus der Deko: mit und ohne Jahreszeit gleich
            var deko = Deko(sommer);
            var dekoJ = Deko(szene);
            Assert.True(deko.AnzahlEcken > 0);
            Assert.Equal(Farben(deko), Farben(dekoJ));
            // und im Netz der Stadt steht die Deko am Ende der eigenen Teile bzw. Modelle in diesen Farben
            var farbenStadt = Farben(szene.NetzBauen()).ToHashSet();
            Assert.All(Farben(deko).Distinct(), f => Assert.Contains(f, farbenStadt));
            // W11: die Deko aus dem Graveyard Kit (Kuerbisse) steht im eigenen Netz, ebenso unveraendert (ohne Leuchtmarke)
            var paket = Deko(sommer, paket: true);
            Assert.Equal(Farben(paket), Farben(Deko(szene, paket: true)));
            var farbenPaket = Farben(szene.PaketNetzBauen(false)).Select(f => Nachtlicht.Leuchtet(f) ? f - new Vector3(Nachtlicht.Marke) : f).ToHashSet();
            Assert.All(Farben(paket).Distinct(), f => Assert.Contains(f, farbenPaket));
        }
    }

    /// <summary>Netz nur aus der Fest-Deko: eigene Teile und Modelle der Stadt, mit <paramref name="paket"/> nur die Modelle im eigenen Netz.</summary>
    private static Netz Deko(StadtSzene s, bool paket = false)
    {
        var n = paket ? new Netz() : Netz.Aus(s.Teile.Skip(s.FestTeileVon));
        foreach (var st in s.Stuecke.Skip(s.FestStueckeVon))
        {
            if (StadtSzene.ImPaket(st.Name) == paket && Grafik.Modell(st.Name) is { } m)
            {
                n.Hinzu(m, st.Lage);
            }
        }
        return n;
    }

    /// <summary>Waende bleiben (nur Flaechen nach oben werden im Winter Schnee); Strassen bleiben in jeder Jahreszeit (Nico 2a).</summary>
    [Theory]
    [MemberData(nameof(Andere))]
    public void WaendeUndStrassenBleiben(Jahreszeit j)
    {
        var sommer = Bauen(Jahreszeit.Sommer);
        var szene = Bauen(j);
        var strasse = Netz.Farbe("#4A4560");
        foreach (var (a, b) in AlleNetze(sommer).Zip(AlleNetze(szene)))
        {
            var fa = Farben(a);
            var fb = Farben(b);
            for (var i = 0; i < fa.Count; i++)
            {
                if (fa[i] == strasse)
                {
                    Assert.Equal(strasse, fb[i]);
                }
            }
        }
        foreach (var (ha, hb) in sommer.Haeuser.Zip(szene.Haeuser))
        {
            var a = sommer.HausNetz(ha);
            var b = szene.HausNetz(hb);
            var e = a.EckenFeld.ToArray();
            var fa = Farben(a);
            var fb = Farben(b);
            for (var i = 0; i < fa.Count; i++)
            {
                var obenAuf = MathF.Abs(e[(i * Netz.Zahlen) + 4]);
                var hoehe = e[(i * Netz.Zahlen) + 1];
                // Waende (Normale waagrecht) ueber den Pflanzen am Haus bleiben immer
                if (obenAuf < 0.1f && hoehe >= Jahreszeitfarben.PflanzeAmHausBis)
                {
                    if (fa[i] != fb[i])
                    {
                        // nur Baeume am Haus (CC0-Modell mit "tree") duerfen sich aendern
                        Assert.True(Jahreszeitfarben.IstPflanze(fa[i]), ha.Id + ": Wand umgefaerbt " + fa[i] + " -> " + fb[i]);
                    }
                }
            }
        }
    }

    /// <summary>Winter: jede Dachflaeche (nach oben, ueber 0,3) jedes Hauses ist Schnee, auch das steile Stellwerk-Dach.</summary>
    [Fact]
    public void WinterSchneeAufDaechern()
    {
        foreach (var mitGrafik in (bool[])[true, false])
        {
            var szene = Bauen(Jahreszeit.Winter, mitGrafik: mitGrafik);
            var sommer = Bauen(Jahreszeit.Sommer, mitGrafik: mitGrafik);
            foreach (var (haus, hs) in szene.Haeuser.Zip(sommer.Haeuser))
            {
                var n = szene.HausNetz(haus);
                var e = n.EckenFeld.ToArray();
                var f = Farben(n);
                var fs = Farben(sommer.HausNetz(hs));
                // Baeume am Haus (Modellname mit "tree") sind keine Daecher: ihr Stamm bleibt wie die Baeume der Stadt
                var baumfarben = new HashSet<Vector3>();
                for (var s = haus.StueckeVon; s < haus.StueckeBis; s++)
                {
                    if (szene.Stuecke[s].Name.Contains("tree", StringComparison.Ordinal) && szene.ModellAn(s) is { } baum)
                    {
                        for (var d = 0; d < baum.Dreiecke; d++)
                        {
                            baumfarben.Add(new Vector3(baum.Farben[d * 3], baum.Farben[(d * 3) + 1], baum.Farben[(d * 3) + 2]) / 255f);
                        }
                    }
                }
                var dach = 0;
                for (var i = 0; i < f.Count; i++)
                {
                    var oben = MathF.Abs(e[(i * Netz.Zahlen) + 4]);
                    if (oben > Jahreszeitfarben.SchneeDach && e[(i * Netz.Zahlen) + 1] > Jahreszeitfarben.DachAb && !baumfarben.Contains(fs[i]))
                    {
                        Assert.True(Jahreszeitfarben.IstSchnee(f[i]), haus.Id + ": Dach ohne Schnee " + f[i]);
                        dach++;
                    }
                }
                Assert.True(dach > 0, haus.Id + ": kein Dach gefunden");
            }
        }
    }

    /// <summary>Herbst: Nadelbaeume bleiben gruen, Laubbaeume nicht; Winter: Kleingruen (Beete, Steine) oben Schnee.</summary>
    [Fact]
    public void NadelbaeumeUndBeete()
    {
        var nadel = Gruen.Baeume.Select(b => Grafik.Modell(b.Name)!).Where(Jahreszeitfarben.Nadel).ToList();
        Assert.NotEmpty(nadel);
        Assert.Contains(Gruen.Baeume, b => !Jahreszeitfarben.Nadel(Grafik.Modell(b.Name)!));
        foreach (var m in nadel)
        {
            var sommer = new Netz();
            sommer.Hinzu(m, Matrix4x4.Identity);
            var herbst = new Netz();
            herbst.Hinzu(m, Matrix4x4.Identity, Jahreszeitfarben.FuerModell(Jahreszeit.Herbst, Faerbeart.Stadt, m, 7, true));
            Assert.Equal(sommer.EckenFeld.ToArray(), herbst.EckenFeld.ToArray());
        }
        var winter = Bauen(Jahreszeit.Winter).GruenNetzBauen()!;
        var e = winter.EckenFeld.ToArray();
        var f = Farben(winter);
        var oben = Enumerable.Range(0, f.Count).Where(i => MathF.Abs(e[(i * Netz.Zahlen) + 4]) > 0.9f).ToList();
        Assert.NotEmpty(oben);
        Assert.All(oben, i => Assert.True(Jahreszeitfarben.IstSchnee(f[i]), "Kleingruen oben ohne Schnee: " + f[i]));
    }

    /// <summary>Fruehling: jeder 3. Laubbaum blueht (Nico 3c); die Bodenfarben je Jahreszeit; Schnee mit Marke.</summary>
    [Fact]
    public void BlueteBodenUndSchneemarke()
    {
        Assert.Equal(3, Jahreszeitfarben.BlueteJeder);
        var gruen = Netz.Farbe("#5DA04A");
        Assert.True(Jahreszeitfarben.IstPflanze(gruen));
        var bluehend = Jahreszeitfarben.Faerben(Jahreszeit.Fruehling, Faerbeart.Stadt, gruen, 0.5f, 1f, 3);
        var nicht = Jahreszeitfarben.Faerben(Jahreszeit.Fruehling, Faerbeart.Stadt, gruen, 0.5f, 1f, 4);
        Assert.True(bluehend.X > bluehend.Y, "Bluete rosa");
        Assert.True(nicht.Y > nicht.X, "kein Bluehen gruen");
        // Kleingruen (Bueschel, Graeser) blueht nie, auch nicht mit dem Samen eines bluehenden Baums
        var klein = Jahreszeitfarben.Faerben(Jahreszeit.Fruehling, Faerbeart.Klein, gruen, 0.5f, 1f, 3);
        Assert.True(klein.Y > klein.X, "Kleingruen bleibt gruen");
        Assert.Equal(Jahreszeitfarben.Boden, Jahreszeitfarben.Faerben(Jahreszeit.Sommer, Faerbeart.Stadt, Jahreszeitfarben.Boden, 1, 0));
        Assert.True(Jahreszeitfarben.IstSchnee(Jahreszeitfarben.Faerben(Jahreszeit.Winter, Faerbeart.Stadt, Jahreszeitfarben.Boden, 1, 0)));
        Assert.False(Jahreszeitfarben.IstSchnee(Jahreszeitfarben.Faerben(Jahreszeit.Herbst, Faerbeart.Stadt, Jahreszeitfarben.Boden, 1, 0)));
        // Fluss im Winter hellblau (Nico 2a), aber kein Schnee
        var fluss = Jahreszeitfarben.Faerben(Jahreszeit.Winter, Faerbeart.Stadt, Jahreszeitfarben.Wasser, 1, 0);
        Assert.True(fluss.Z > fluss.X && Jahreszeitfarben.IstSchnee(fluss), "Wasser im Winter: " + fluss);
        // Marke abgezogen ist Schnee hell (Shader)
        var s = Jahreszeitfarben.Schnee(1f) - new Vector3(Jahreszeitfarben.SchneeMarke);
        Assert.True(s.X > 0.9f && s.Y > 0.9f && s.Z > 0.9f);
    }

    /// <summary>Die Wiese um die Innenraeume in der Jahreszeitfarbe, im Sommer wie vorher.</summary>
    [Fact]
    public void InnenraumWiese()
    {
        var stand = SpielFingerabdruckTests.Gespielt.Value.Stand;
        var sommer = Innenraum.Bauen("buero", stand, SpielInnenraumTests.Innenraeume.Value, Grafik)!;
        var vorher = sommer.NetzBauen().EckenFeld.ToArray();
        Assert.Equal(vorher, Innenraum.Bauen("buero", stand, SpielInnenraumTests.Innenraeume.Value, Grafik, jahreszeit: Jahreszeit.Sommer)!.NetzBauen().EckenFeld.ToArray());
        var winter = Innenraum.Bauen("buero", stand, SpielInnenraumTests.Innenraeume.Value, Grafik, jahreszeit: Jahreszeit.Winter)!;
        Assert.Equal(Jahreszeit.Winter, winter.Jahreszeit);
        Assert.Contains(Farben(winter.NetzBauen()), Jahreszeitfarben.IstSchnee);
        Assert.DoesNotContain(Farben(sommer.NetzBauen()), Jahreszeitfarben.IstSchnee);
    }
}
