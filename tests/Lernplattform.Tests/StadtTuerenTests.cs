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

    /// <summary>Spielstand, in dem alle Orte mit Fussweg zu sehen sind (Tag 201, alle Kundenorte freigeschaltet).</summary>
    private static Spielstand Spaet()
    {
        var ablauf = new Spielablauf(SpielInhaltTests.Inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        ablauf.Stand.TageFertig = 200;
        return ablauf.Stand;
    }

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
        Assert.True(mitWeg.Count >= 12, "zu wenige Orte sichtbar: " + string.Join(", ", mitWeg));
        foreach (var id in mitWeg)
        {
            var tuer = Assert.Single(szene.Tueren, t => t.Ort == id);
            // die Tuer liegt auf einer Wand eines Blocks des Hauses (Abstand hoechstens 0,02)
            var abstand = Bloecke(stadt, stand, id).Min(b => AufWand(b, tuer.Mitte));
            Assert.True(abstand <= 0.02f, id + ": Tuer nicht auf der Wand (" + abstand + ")");
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

    /// <summary>Jede Wand eines eigenen Blocks (ohne CC0-Grafik) mit mindestens 0,8 Laenge hat Fenster (Nico 5a: ringsum).</summary>
    [Fact]
    public void KeineFrontOhneFenster()
    {
        var stadt = NeueStadt();
        var stand = Spaet();
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
