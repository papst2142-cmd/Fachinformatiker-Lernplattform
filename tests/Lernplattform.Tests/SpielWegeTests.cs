using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W2: Vergleichswerte G48 Teil Wege (<c>spiel_wege.json</c>, Erzeuger
/// <c>werkzeuge/vergleich/erzeuge_spiel_wege.py</c>): Raster, Punkte und walk_path in allen
/// Grundrissen genau wie <c>fisi_game.py</c> (Python 0.63.2).
/// </summary>
public class SpielWegeTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_wege.json");

    /// <summary>Wie der Erzeuger: Buero, Kundenorte, Ausbaustufen der Firma und der Filiale.</summary>
    private static List<(string Name, LaufOrt Ort)> Grundrisse()
    {
        var inhalt = SpielInhaltTests.Inhalt;
        var liste = new List<(string, LaufOrt)> { ("buero", new LaufOrt((PyObjekt)inhalt["gebaeude"]!, (List<object?>)inhalt["kollegen"]!)) };
        foreach (var ort in (List<object?>)((PyObjekt)inhalt["kunden"]!)["orte"]!)
        {
            var o = (PyObjekt)ort!;
            liste.Add(("kunde_" + o["id"], new LaufOrt((PyObjekt)o["gebaeude"]!, o["personen"] as List<object?> ?? [])));
        }
        var firma = (PyObjekt)inhalt["firma"]!;
        var stufen = (List<object?>)((PyObjekt)firma["gebaeude"]!)["stufen"]!;
        liste.AddRange(stufen.Select((s, i) => ("firma_" + i, new LaufOrt((PyObjekt)((PyObjekt)s!)["gebaeude"]!, []))));
        var filiale = (List<object?>)((PyObjekt)firma["filiale"]!)["stufen"]!;
        liste.AddRange(filiale.Select((s, i) => ("filiale_" + i, new LaufOrt((PyObjekt)((PyObjekt)s!)["gebaeude"]!, []))));
        return liste;
    }

    private static (double, double) Punkt(JsonElement e) => (e[0].GetDouble(), e[1].GetDouble());

    private static string Text((double, double) p) => PythonJson.Zahl(p.Item1) + "," + PythonJson.Zahl(p.Item2);

    private static string Text(JsonElement p) => Text(Punkt(p));

    [Fact]
    public void HashWiePython()
    {
        foreach (var fall in Soll.GetProperty("hash").EnumerateArray())
        {
            Assert.Equal(fall[1].GetInt64(), PyMenge.Hash((fall[0][0].GetInt64(), fall[0][1].GetInt64())));
        }
    }

    [Fact]
    public void KonstantenWiePython()
    {
        var k = Soll.GetProperty("konstanten");
        Assert.Equal(Wege.Schritt, k.GetProperty("WALK_STEP").GetDouble());
        Assert.Equal(Wege.Koerper, k.GetProperty("BODY_RADIUS").GetDouble());
        Assert.Equal(Wege.PersonRadius, k.GetProperty("PERSON_RADIUS").GetDouble());
        Assert.Equal(Wege.Reichweite, k.GetProperty("REACH").GetDouble());
        Assert.Equal(k.GetProperty("WALK_FREE").EnumerateArray().Select(e => e.GetString()!), Wege.Begehbar);
    }

    [Fact]
    public void RasterInPythonsReihenfolge()
    {
        var orte = Grundrisse();
        var soll = Soll.GetProperty("grundrisse").EnumerateArray().ToList();
        Assert.Equal(soll.Select(g => g.GetProperty("name").GetString()!), orte.Select(o => o.Name));
        for (var i = 0; i < soll.Count; i++)
        {
            var raster = soll[i].GetProperty("raster");
            var ort = orte[i].Ort;
            Assert.Equal(raster.GetProperty("basis_anzahl").GetInt32(), ort.Basis.Count);
            Assert.Equal(raster.GetProperty("frei_anzahl").GetInt32(), ort.Frei.Count);
            Assert.Equal(raster.GetProperty("basis").EnumerateArray().Select(c => (c[0].GetInt64(), c[1].GetInt64())), ort.Basis.Select(c => (c.Spalte, c.Zeile)));
            Assert.Equal(raster.GetProperty("frei").EnumerateArray().Select(c => (c[0].GetInt64(), c[1].GetInt64())), ort.Frei.Select(c => (c.Spalte, c.Zeile)));
            Assert.Equal(Text(soll[i].GetProperty("start")), Text(Wege.StartPlatz(ort.Gebaeude)));
        }
    }

    [Fact]
    public void PunkteWiePython()
    {
        var orte = Grundrisse();
        var soll = Soll.GetProperty("grundrisse").EnumerateArray().ToList();
        for (var i = 0; i < soll.Count; i++)
        {
            var ort = orte[i].Ort;
            foreach (var p in soll[i].GetProperty("punkte").EnumerateArray())
            {
                var (x, y) = Punkt(p.GetProperty("p"));
                var wo = orte[i].Name + " " + Text(p.GetProperty("p"));
                Assert.True(p.GetProperty("stehen").GetBoolean() == Wege.KannStehen(x, y, ort), wo + " stehen");
                var n = p.GetProperty("naechste");
                Assert.Equal((n[0].GetInt64(), n[1].GetInt64()), Wege.NaechsteFrei(x, y, ort.Frei));
                Assert.Equal(p.GetProperty("raum").GetString(), Wege.RaumBei(x, y, ort.Gebaeude)?["id"] as string);
                Assert.Equal(p.GetProperty("person").GetString(), Wege.PersonBei(x, y, ort)?["id"] as string);
                Assert.Equal(p.GetProperty("nahe").GetString(), Wege.PersonNahe(x, y, ort)?["id"] as string);
            }
        }
    }

    [Fact]
    public void WegeWiePython()
    {
        var orte = Grundrisse();
        var soll = Soll.GetProperty("grundrisse").EnumerateArray().ToList();
        var anzahl = 0;
        for (var i = 0; i < soll.Count; i++)
        {
            foreach (var w in soll[i].GetProperty("wege").EnumerateArray())
            {
                var weg = Wege.Weg(Punkt(w.GetProperty("start")), Punkt(w.GetProperty("ziel")), w.GetProperty("reach").GetDouble(), orte[i].Ort);
                var wo = orte[i].Name + " " + Text(w.GetProperty("start")) + " -> " + Text(w.GetProperty("ziel")) + " reach " + w.GetProperty("reach").GetDouble();
                Assert.Equal(wo + ": " + string.Join(" ", w.GetProperty("weg").EnumerateArray().Select(Text)), wo + ": " + string.Join(" ", weg.Select(Text)));
                anzahl++;
            }
        }
        Assert.True(anzahl > 1000);
    }
}
