using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W8b (Weisung P8-20, Nico 3a 4a 5a): jedes betretbare Haus hat eine Tuer genau am Ende seines Fusswegs
/// (eigene Tuer aus Kaesten oder die Tuer des CC0-Modells, dann steht das Modell in <c>darstellung.modell_tuer</c>),
/// das Stellwerk hat Gehweg, Tuer und Schild, eigene Haeuser haben Fenster ringsum. Nur Darstellung.
/// </summary>
public sealed class StadtTuerenTests
{
    private static GrafikBibliothek Grafik => SpielGrafikTests.Bibliothek.Value;

    private static Stadt NeueStadt() => new(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);

    /// <summary>
    /// Spielstand, in dem alle Orte mit Fussweg zu sehen sind (Tag 201, alle Kundenorte freigeschaltet; P8-28: mit
    /// Firma der Stufe 5 und Zweigstelle, damit auch Gewerbehof und Filiale dabei sind).
    /// </summary>
    private static Spielstand Spaet(bool firma = true)
    {
        var ablauf = new Spielablauf(SpielInhaltTests.Inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        ablauf.Stand.TageFertig = 200;
        if (!firma)
        {
            return ablauf.Stand;
        }
        ablauf.Stand.Firma = new PyObjekt { { "stufe", 5L }, { "name", "Probe" }, { "tag", 1L } };
        ablauf.Stand.Zweigstelle = new PyObjekt { { "stufe", 1L } };
        return ablauf.Stand;
    }

    /// <summary>Grenze fuer den Spalt zwischen Tuer und Wand (Weisung P8-28, Vorschlag: 0,02 = 10 cm).</summary>
    internal const float SpaltGrenze = 0.02f;

    public static TheoryData<bool> MitGrafik => [true, false];

    [Theory]
    [MemberData(nameof(MitGrafik))]
    public void JedesBetretbareHausHatTuer(bool mitGrafik)
    {
        var stadt = NeueStadt();
        var stand = Spaet();
        var szene = StadtSzene.Bauen(stadt, stand, mitGrafik ? Grafik : null);
        var sichtbar = szene.Orte.Select(o => o.Id).ToHashSet();
        var mitWeg = stadt.Zugaenge.Select(z => z.Key).Where(sichtbar.Contains).ToList();
        Assert.True(mitWeg.Count >= 14, "zu wenige Orte sichtbar: " + string.Join(", ", mitWeg));
        Assert.Contains("gewerbehof", mitWeg);
        foreach (var id in mitWeg)
        {
            var tuer = Assert.Single(szene.Tueren, t => t.Ort == id);
            // ohne Grafik liegt die Tuer auf einer Wand eines Blocks des Hauses (Abstand hoechstens 0,02); mit Grafik
            // auf der echten Wand des Modells, also auf der Wand des Blocks oder dahinter, innerhalb seiner Spanne
            // (der Spalt zum Modell: TuerSitztAnDerWand)
            var abstand = Bloecke(stadt, stand, id).Min(b => AufWand(b, tuer.Mitte));
            Assert.True(abstand <= (mitGrafik ? 0.4f : 0.02f), id + ": Tuer nicht auf der Wand (" + abstand + ")");
            Assert.True(mitGrafik || tuer.Hinten == 0f, id + ": ohne Grafik reicht der Rahmen nicht nach hinten");
            // Modell-Tuer nur mit Grafik und nur fuer Modelle aus darstellung.modell_tuer, sonst eine eigene aus Kaesten
            Assert.Equal(mitGrafik && StadtTueren.ModellVersatz(stadt, id) is not null, tuer.Modell);
            if (!tuer.Modell)
            {
                Assert.True(TuerTeile(szene, tuer) >= 7, id + ": keine eigene Tuer aus Kaesten");
            }
        }
        // jede Modell-Tuer aus stadt.json gehoert zu einem Ort mit Fussweg
        Assert.All(SpielDaten.Objekt(StadtTueren.Darstellung(stadt)["modell_tuer"]).Where(p => !p.Key.StartsWith('_')),
            p => Assert.Contains(p.Key, stadt.Zugaenge.Select(z => z.Key)));
    }

    [Theory]
    [MemberData(nameof(MitGrafik))]
    public void GehwegMuendetMittig(bool mitGrafik)
    {
        var szene = StadtSzene.Bauen(NeueStadt(), Spaet(), mitGrafik ? Grafik : null);
        Assert.NotEmpty(szene.Tueren);
        foreach (var tuer in szene.Tueren)
        {
            // Mitte der Tuer und Anfang (Tuer-Ende) des Fusswegs hoechstens 0,05 auseinander, quer zur Wand gemessen
            var quer = new Vector2(-tuer.Aussen.Y, tuer.Aussen.X);
            var versatz = MathF.Abs(Vector2.Dot(tuer.WegAnfang - tuer.Mitte, quer));
            Assert.True(versatz <= 0.05f, tuer.Ort + ": Gehweg nicht mittig (" + versatz + ")");
            // der Gehweg reicht bis an die Wand: liegt sein Anfang vor der Wand, fuehrt ein Stueck Gehweg bis zur Tuer
            var luecke = Vector2.Dot(tuer.WegAnfang - tuer.Mitte, tuer.Aussen);
            Assert.True(luecke >= -0.05f, tuer.Ort + ": Gehweg beginnt im Haus (" + luecke + ")");
            if (luecke > 0.02f)
            {
                Assert.Contains(szene.Teile, t => t.Y2 <= 0.05f && Drin(t, tuer.Mitte + (tuer.Aussen * 0.01f)) && Drin(t, tuer.WegAnfang));
            }
        }
        // Stellwerk (Nico 3a): Gehweg zur Strasse, Tuer, Schild; nicht betretbar (kein Ort)
        var stellwerk = Assert.Single(szene.Zierhaeuser);
        Assert.Equal("Stellwerk", stellwerk.Name);
        Assert.Null(szene.Ort("stellwerk"));
        Assert.Single(szene.Tueren, t => t.Ort == "stellwerk");
        Assert.True(szene.Laufraster.KannStehen(stellwerk.GehX, stellwerk.GehZ), "Ende des Gehwegs am Stellwerk nicht begehbar");
        Assert.True(stellwerk.Schild.Y > stellwerk.KastenMax.Y, "Schild nicht ueber dem Haus");
    }

    /// <summary>
    /// Weisung P8-28 (Nico: "es sieht so aus, als wuerden die Tueren nicht an den Gebaeuden dran sein"): hinter jeder
    /// Tuer (eigene und die des Modells) liegt die Wand des CC0-Modells hoechstens <see cref="SpaltGrenze"/> hinter dem
    /// Rahmen. Gemessen mit Strahlen von vorn auf 7 Stellen ueber die Glasbreite in 4 Hoehen; jeder muss treffen.
    /// </summary>
    [Fact]
    public void TuerSitztAnDerWand()
    {
        var szene = StadtSzene.Bauen(NeueStadt(), Spaet(), Grafik);
        var gemessen = 0;
        foreach (var tuer in szene.Tueren)
        {
            var quer = new Vector2(-tuer.Aussen.Y, tuer.Aussen.X);
            var stuecke = Stuecke(szene, tuer);
            if (stuecke.Count == 0)
            {
                continue;
            }
            var spalte = new List<string>();
            for (var i = -3; i <= 3; i++)
            {
                for (var j = 1; j <= 4; j++)
                {
                    var p = tuer.Mitte + (quer * (i * ((StadtSzene.TuerBreite / 2) - 0.03f) / 3)) + (tuer.Aussen * 0.3f);
                    var t = StadtTueren.Treffer(stuecke, Grafik, new Vector3(p.X, tuer.Hoehe * (0.15f + (j * 0.18f)), p.Y), new Vector3(-tuer.Aussen.X, 0, -tuer.Aussen.Y), 1.3f);
                    Assert.True(t is not null, tuer.Ort + ": hinter der Tuer keine Wand (Stelle " + i + "/" + j + ")");
                    // Spalt = Weg hinter der Rueckseite des Rahmens (davor stehende Pfeiler zaehlen negativ)
                    var spalt = t!.Value.T - 0.3f - tuer.Hinten;
                    Assert.True(spalt <= SpaltGrenze, tuer.Ort + ": Spalt zwischen Tuer und Wand " + spalt.ToString("0.000", CultureInfo.InvariantCulture)
                        + " (Stelle " + i + "/" + j + ", Grenze " + SpaltGrenze + ")");
                    gemessen++;
                }
            }
        }
        // alle Haeuser mit CC0-Modell, auch Stellwerk und Bahnhof Talheim (Nico P8-28)
        Assert.All(new[] { "stellwerk", "talheim", "gewerbehof", "lindenau", "moebelhaus", "kaufhaus", "zuhause", "bitweiche" },
            id => Assert.True(Stuecke(szene, Assert.Single(szene.Tueren, t => t.Ort == id)).Count > 0, id + ": ohne Modell gemessen"));
        Assert.True(gemessen >= 12 * 28, "zu wenige Tueren gemessen: " + gemessen / 28);
    }

    /// <summary>
    /// Weisung P8-28 (Nico: "Bitte dann die Fenster entfernen, wenn dort eine Tuer ist."): auf der Flaeche einer
    /// eigenen Tuer (Breite mit Rahmen und Rand 0,05, bis ueber das Vordach, von vor der Tuer bis hinter die Wand)
    /// liegt kein Fenster, weder Glas eines CC0-Modells noch eine eigene Fensterscheibe.
    /// </summary>
    [Theory]
    [MemberData(nameof(MitGrafik))]
    public void KeinFensterAnDerTuer(bool mitGrafik)
    {
        var szene = StadtSzene.Bauen(NeueStadt(), Spaet(), mitGrafik ? Grafik : null);
        var scheiben = new[] { Netz.Farbe("#5B6B8C"), Netz.Farbe("#FCE7A6"), Netz.Farbe("#22D3EE") };
        var geprueft = 0;
        foreach (var tuer in szene.Tueren.Where(t => !t.Modell))
        {
            for (var i = 0; i < szene.Stuecke.Count; i++)
            {
                var gm = szene.ModellAn(i)!;
                for (var d = 0; d < gm.Dreiecke; d++)
                {
                    var f = new Vector3(gm.Farben[d * 3], gm.Farben[(d * 3) + 1], gm.Farben[(d * 3) + 2]) / 255f;
                    if (StadtTueren.IstGlas(f))
                    {
                        var (a, b, c) = StadtTueren.Ecken(gm, d, szene.Stuecke[i].Lage);
                        Assert.False(StadtTueren.AufTuerFlaeche(a, b, c, tuer.Mitte, tuer.Aussen, tuer.Hoehe, tuer.Hinten),
                            tuer.Ort + ": Fenster von " + szene.Stuecke[i].Name + " an der Tuer");
                    }
                }
            }
            foreach (var t in szene.Teile.Where(t => scheiben.Contains(t.Farbe)))
            {
                Assert.False(StadtTueren.AufTuerFlaeche(new(t.X1, t.Y1, t.Z1), new(t.X2, t.Y2, t.Z2), new(t.X1, t.Y1, t.Z1), tuer.Mitte, tuer.Aussen, tuer.Hoehe, tuer.Hinten),
                    tuer.Ort + ": Fensterscheibe an der Tuer (" + t.X1 + "/" + t.Z1 + ")");
            }
            geprueft++;
        }
        Assert.True(geprueft >= 10, "zu wenige Tueren geprueft: " + geprueft);
    }

    /// <summary>Die CC0-Stuecke des Hauses einer Tuer (Kasten des Orts oder des Stellwerks), leer ohne Modell.</summary>
    private static List<ModellStueck> Stuecke(StadtSzene szene, StadtTuer tuer)
    {
        var haus = szene.Haeuser.FirstOrDefault(h => h.Id == tuer.Ort || h.Id.StartsWith(tuer.Ort + "#", StringComparison.Ordinal));
        return haus is null || szene.Grafik is null ? [] : [.. szene.Stuecke.Skip(haus.StueckeVon).Take(haus.StueckeBis - haus.StueckeVon)];
    }

    /// <summary>Jede Wand eines eigenen Blocks (ohne CC0-Grafik) mit mindestens 0,8 Laenge hat Fenster (Nico 5a: ringsum).</summary>
    [Fact]
    public void KeineFrontOhneFenster()
    {
        var stadt = NeueStadt();
        // Stand wie in Meldung P8-20 (ohne Firma): der Gewerbehof hat ohne Grafik mit Satteldach unter 1,4 Hoehe gar keine
        // Fenster (Sammelliste, Fund aus P8-28); die Ersatzdarstellung aendert diese Welle nicht
        var stand = Spaet(firma: false);
        var szene = StadtSzene.Bauen(stadt, stand);
        var gezaehlt = 0;
        foreach (var ort in szene.Orte)
        {
            foreach (var (x, z, w, t, h, teil) in Bloecke(stadt, stand, ort.Id))
            {
                if ((Py.Hat(teil, "fenster") && !Py.Wahr(teil["fenster"])) || h < 0.7f)
                {
                    continue;
                }
                // Fensterscheiben: 0,03 dick, aussen auf der Wand
                var waende = new (string Name, float Laenge, Func<Teil, bool> Scheibe)[]
                {
                    ("vorne", w, s => Nah(s.Z1, z + t) && Nah(s.Z2, z + t + 0.03f) && s.X1 >= x - 0.01f && s.X2 <= x + w + 0.01f),
                    ("hinten", w, s => Nah(s.Z1, z - 0.03f) && Nah(s.Z2, z) && s.X1 >= x - 0.01f && s.X2 <= x + w + 0.01f),
                    ("links", t, s => Nah(s.X1, x - 0.03f) && Nah(s.X2, x) && s.Z1 >= z - 0.01f && s.Z2 <= z + t + 0.01f),
                    ("rechts", t, s => Nah(s.X1, x + w) && Nah(s.X2, x + w + 0.03f) && s.Z1 >= z - 0.01f && s.Z2 <= z + t + 0.01f),
                };
                foreach (var (name, laenge, scheibe) in waende.Where(wd => wd.Laenge >= 0.8f))
                {
                    Assert.True(szene.Teile.Any(scheibe), ort.Id + " (" + x + "/" + z + "): " + name + " ohne Fenster");
                    gezaehlt++;
                }
            }
        }
        Assert.True(gezaehlt >= 40, "zu wenige Waende geprueft: " + gezaehlt);
    }

    /// <summary>Die sichtbaren Bloecke des Modells eines Orts: Ecke (x, z), Breite, Tiefe, Hoehe.</summary>
    private static List<(float X, float Z, float W, float T, float H, PyObjekt Teil)> Bloecke(Stadt stadt, Spielstand stand, string id)
    {
        var o = stadt.Regeln.First(r => Py.Gleich(r["id"], id));
        float ox = (float)Py.F(o["x"]), oz = (float)Py.F(o["y"]);
        var modelle = StadtModelle.Modelle(stadt.Inhalt, stadt.Daten);
        return [.. StadtModelle.Teile(modelle, Py.Str(o["modell"]), StadtModelle.Kontext(stand, stadt.Inhalt))
            .Where(t => Py.Gleich(t.Teil["art"], "block"))
            .Select(t => (ox + (float)Py.F(t.Teil["x"]), oz + (float)Py.F(t.Teil["y"]), (float)Py.F(t.Teil["w"]), (float)Py.F(t.Teil["t"]), (float)Py.F(t.Teil["h"]), t.Teil))];
    }

    /// <summary>Abstand eines Punkts zur naechsten Wand des Blocks (innerhalb ihrer Spanne, sonst gross).</summary>
    private static float AufWand((float X, float Z, float W, float T, float H, PyObjekt Teil) b, Vector2 p)
    {
        var inX = p.X >= b.X - 0.02f && p.X <= b.X + b.W + 0.02f;
        var inZ = p.Y >= b.Z - 0.02f && p.Y <= b.Z + b.T + 0.02f;
        var d = float.MaxValue;
        if (inX)
        {
            d = MathF.Min(d, MathF.Min(MathF.Abs(p.Y - b.Z), MathF.Abs(p.Y - (b.Z + b.T))));
        }
        if (inZ)
        {
            d = MathF.Min(d, MathF.Min(MathF.Abs(p.X - b.X), MathF.Abs(p.X - (b.X + b.W))));
        }
        return d;
    }

    private static bool Nah(float a, float b) => MathF.Abs(a - b) < 0.002f;

    private static bool Drin(Teil t, Vector2 p) => t.X1 - 0.01f <= p.X && p.X <= t.X2 + 0.01f && t.Z1 - 0.01f <= p.Y && p.Y <= t.Z2 + 0.01f;

    /// <summary>Kaesten der eigenen Tuer (Rahmen, Fluegel, Oberlichter, Stufe, Vordach) dicht an der Tuermitte.</summary>
    private static int TuerTeile(StadtSzene szene, StadtTuer tuer) => szene.Teile.Count(t =>
        Vector2.Distance(new Vector2((t.X1 + t.X2) / 2, (t.Z1 + t.Z2) / 2), tuer.Mitte) < 0.2f
        && (t.Farbe == StadtTueren.Rahmen || t.Farbe == StadtTueren.Glas || t.Farbe == StadtTueren.GlasHell || t.Farbe == StadtTueren.Stufe || t.Farbe == StadtTueren.Vordach));
}
