using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>Phase 13 Welle 2 (Plan Abschnitt 3): Raster 0,5 (fein 0,1), Drehen in Vierteln um die Mitte, Umzug zwischen Bereichen.</summary>
public class BearbeitungTests
{
    private static PyObjekt Grundriss(string ort) => InnenGrundriss.Grundriss(Daten.Spiel.Fassung.Innenraeume, ort);

    [Fact]
    public void Setzen_AufRasterMitMitteImBereich_AusserhalbNichts()
    {
        var g = Grundriss("moebelhaus");
        var s = Bearbeitung.Setzen(g, "pflanze", 3.33, 2.21)!;
        var d = Bearbeitung.Deko(g, s);
        Assert.Equal(3.0, Py.F(d["x"]));
        Assert.Equal(2.0, Py.F(d["y"]));
        Assert.Equal(0.8, Py.F(d["w"]));
        var fein = Bearbeitung.Setzen(g, "pflanze", 3.33, 2.21, raster: false)!;
        Assert.Equal(2.9, Py.F(Bearbeitung.Deko(g, fein)["x"]));
        Assert.Null(Bearbeitung.Setzen(g, "pflanze", -5, -5));
        Assert.Throws<ArgumentException>(() => Bearbeitung.Setzen(g, "raumschiff", 3, 3));
    }

    [Fact]
    public void Drehen_ViermalWieVorher_MitteBleibt()
    {
        var g = Grundriss("bank");
        var (s, d) = Bearbeitung.Alle(g).First(x => Py.F(x.Deko["w"]) != Py.F(x.Deko["h"]));
        double x = Py.F(d["x"]), y = Py.F(d["y"]), w = Py.F(d["w"]), h = Py.F(d["h"]);
        Bearbeitung.Drehen(g, s);
        Assert.Equal(h, Py.F(d["w"]));
        Assert.Equal(w, Py.F(d["h"]));
        Assert.Equal(Bearbeitung.Runden(x + (w / 2)), Bearbeitung.Runden(Py.F(d["x"]) + (Py.F(d["w"]) / 2)));
        for (var i = 0; i < 3; i++)
        {
            Bearbeitung.Drehen(g, s);
        }
        Assert.Equal((x, y, w, h), (Py.F(d["x"]), Py.F(d["y"]), Py.F(d["w"]), Py.F(d["h"])));
        Assert.Equal(0L, Py.Int(d["dreh"]) % 4);
    }

    [Fact]
    public void Verschieben_GanzeRasterschritte_UndUmzugInDenFlur()
    {
        var g = Grundriss("elektromarkt");
        var s = Bearbeitung.Setzen(g, "pflanze", 5, 5)!;
        var x0 = Py.F(Bearbeitung.Deko(g, s)["x"]);
        s = Bearbeitung.Verschieben(g, s, 0.7, 0);
        Assert.Equal(x0 + 0.5, Py.F(Bearbeitung.Deko(g, s)["x"]));
        s = Bearbeitung.Verschieben(g, s, 0.2, 0, raster: false);
        Assert.Equal(Bearbeitung.Runden(x0 + 0.7), Py.F(Bearbeitung.Deko(g, s)["x"]));
        Assert.NotEqual("flur", s.Bereich);
        s = Bearbeitung.Platzieren(g, s, 5, 11.5);
        Assert.Equal("flur", s.Bereich);
        Assert.Equal(5.0, Py.F(Bearbeitung.Deko(g, s)["x"]));
    }

    [Fact]
    public void Kopieren_Loeschen_Ware_Raum_Tuer()
    {
        var g = Grundriss("moebelhaus");
        var (s, d) = Bearbeitung.Alle(g).First(x => Py.Hat(x.Deko, "ware"));
        var anzahl = Bearbeitung.Alle(g).Count();
        var k = Bearbeitung.Kopieren(g, s);
        Assert.Equal(anzahl + 1, Bearbeitung.Alle(g).Count());
        Assert.Equal(d["ware"], Bearbeitung.Deko(g, k)["ware"]);
        Bearbeitung.Ware(g, k, null);
        Assert.False(Py.Hat(Bearbeitung.Deko(g, k), "ware"));
        Bearbeitung.Loeschen(g, k);
        Assert.Equal(anzahl, Bearbeitung.Alle(g).Count());
        var raum = SpielDaten.Liste(g["raeume"]).OfType<PyObjekt>().First();
        var id = Py.Str(raum["id"]);
        Bearbeitung.Tuer(g, id, 4.2, 2.1);
        Assert.Equal(2.0, Py.F(SpielDaten.Objekt(raum["tuer"])["von"]));
        Assert.Equal(4.0, Py.F(SpielDaten.Objekt(raum["tuer"])["bis"]));
        Bearbeitung.Raum(g, id, 0.5, 0, Py.F(raum["w"]) - 0.5, Py.F(raum["h"]));
        Assert.Equal(2.5, Py.F(SpielDaten.Objekt(raum["tuer"])["von"]));
    }
}
