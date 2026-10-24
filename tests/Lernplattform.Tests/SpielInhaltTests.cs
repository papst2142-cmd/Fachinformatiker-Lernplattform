using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W2: Vergleichswerte G42 (<c>spiel_inhalte.json</c>, Erzeuger
/// <c>werkzeuge/vergleich/erzeuge_spiel_inhalte.py</c>): die Spielinhalte laden genau wie
/// <c>fisi_game.load_game_content</c> (Python 0.63.2).
/// </summary>
public class SpielInhaltTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_inhalte.json");

    internal static readonly PyObjekt Inhalt = SpielInhalt.Laden(Path.Combine(Pfade.Inhalte, "spiel"),
        Lerninhalte.Laden(Pfade.Inhalte).Testprojekte.Select(p => Py.AusKnoten(p.Daten)).ToList());

    private static object? PyWert(JsonElement e) => Py.Lesen(e.GetRawText());

    [Fact]
    public void SchluesselInPythonsReihenfolge()
    {
        Assert.Equal(Soll.GetProperty("schluessel").EnumerateArray().Select(s => s.GetString()!), Inhalt.Select(p => p.Key));
        Assert.Equal(SpielInhalt.Schluessel, Inhalt.Select(p => p.Key));
    }

    [Fact]
    public void JederAuftragGleich()
    {
        foreach (var liste in new[] { "aufgaben", "zwischenfaelle" })
        {
            var soll = Soll.GetProperty(liste).EnumerateArray().ToList();
            var ist = (List<object?>)Inhalt[liste]!;
            Assert.Equal(soll.Count, ist.Count);
            for (var i = 0; i < soll.Count; i++)
            {
                var a = (PyObjekt)ist[i]!;
                var wo = liste + " Nr. " + (i + 1) + " " + soll[i].GetProperty("id").GetRawText();
                Assert.True(Py.Gleich(PyWert(soll[i].GetProperty("id")), a["id"]), wo);
                Assert.Equal(soll[i].GetProperty("felder").EnumerateArray().Select(f => f.GetString()!), a.Select(p => p.Key));
                Assert.True(soll[i].GetProperty("sha256").GetString() == Py.Pruefsumme(a), wo);
            }
        }
    }

    [Fact]
    public void JederSchluesselGleich()
    {
        foreach (var (schluessel, soll) in Soll.GetProperty("werte").EnumerateObject().Select(p => (p.Name, p.Value)))
        {
            var ist = Inhalt[schluessel];
            Assert.Equal(soll.GetProperty("typ").GetString(), ist is PyObjekt ? "dict" : "list");
            Assert.Equal(soll.GetProperty("anzahl").GetInt32(), Py.Len(ist));
            Assert.True(soll.GetProperty("sha256").GetString() == Py.Pruefsumme(ist), schluessel);
        }
        foreach (var p in Soll.GetProperty("story").EnumerateObject())
        {
            Assert.True(p.Value.GetString() == Py.Pruefsumme(((PyObjekt)Inhalt["story"]!)[p.Name]), "story " + p.Name);
        }
    }

    [Fact]
    public void VariantenWiePython()
    {
        foreach (var fall in Soll.GetProperty("faelle").GetProperty("varianten").EnumerateArray())
        {
            var ergebnis = SpielInhalt.VariantenAufloesen(PyWert(fall.GetProperty("eintraege")), fall.GetProperty("vorlage").GetBoolean());
            Assert.Equal(fall.GetProperty("name").GetString() + " " + PythonJson.Text(PyWert(fall.GetProperty("ergebnis"))),
                fall.GetProperty("name").GetString() + " " + PythonJson.Text(ergebnis.Cast<object?>().ToList()));
        }
    }

    [Fact]
    public void ZeilenUndStoryWiePython()
    {
        foreach (var fall in Soll.GetProperty("faelle").GetProperty("zeilen").EnumerateArray())
        {
            Assert.Equal(PythonJson.Text(PyWert(fall.GetProperty("ergebnis"))),
                PythonJson.Text(SpielInhalt.Zeilen(PyWert(fall.GetProperty("eingabe")))));
        }
        foreach (var fall in Soll.GetProperty("faelle").GetProperty("story").EnumerateArray())
        {
            Assert.Equal(PythonJson.Text(PyWert(fall.GetProperty("ergebnis"))),
                PythonJson.Text(SpielInhalt.StoryWert(PyWert(fall.GetProperty("eingabe")))));
        }
    }

    [Fact]
    public void KeinPlatzhalterUebrig()
    {
        Assert.False(SpielInhalt.PlatzhalterUebrig(Inhalt["aufgaben"]));
        Assert.True(SpielInhalt.PlatzhalterUebrig(Py.Lesen("""[{"a": ["x {{y}}"]}]""")));
    }

    [Fact]
    public void PyGleichWiePython()
    {
        Assert.True(Py.Gleich(1L, 1.0));
        Assert.True(Py.Gleich(true, 1L));
        Assert.False(Py.Gleich("1", 1L));
        Assert.True(Py.Gleich(Py.Lesen("""{"a":[1,2.0]}"""), Py.Lesen("""{"a":[1.0,2]}""")));
        Assert.True(Py.Enthaelt(Py.Lesen("""["a", 3.0]"""), 3L));
        Assert.Equal(1.0, Py.Plus(1L, Py.Mal(0L, 2.5)));
        Assert.Equal(42L, Py.Plus(18L, Py.Mal(2L, 12L)));
    }
}
