using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.52.0 (Weisung P12-89, Nico 08.11.2026 14:06 MEZ): Talheim-Nord heisst in einer neuen Fassung
/// „Stellwerk Talheim-Nord“. Das Spiel liest den Namen nur aus der Karte der Fassung; in Ereignissen und Spielstand steht nur
/// die Kennung. Geprueft: die Fassung laedt (Ladepruefung und gemeinsame Pruefung ohne Fund), die Stadt zeigt den neuen Namen
/// mit derselben Ansicht, alle Aufgaben bei Talheim-Nord sind da, der Stadt-Fingerabdruck 03d0a012 bleibt gleich.
/// Gegenprobe: ein doppelter Name ist in der Pruefung rot.
/// </summary>
public sealed class StadtbauOrtsnameTests
{
    private const string Neu = "Stellwerk Talheim-Nord";

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    /// <summary>Fassung 0 mit anderem Namen fuer Talheim-Nord (geschrieben und wieder gelesen, Pruefsumme neu).</summary>
    private static StadtFassung Umbenannt(string name)
    {
        var o = (PyObjekt)Py.Lesen(StadtbauFassungTests.Fassung0.Value.Schreiben())!;
        o.RemoveAll(p => p.Key == "pruefsumme");
        var ort = SpielDaten.ListeOderLeer(SpielDaten.Objekt(o["karte"]), "orte").OfType<PyObjekt>().Single(x => Py.Gleich(x["id"], "talheim_nord"));
        Py.Setzen(ort, "name", name);
        o.Add("pruefsumme", StadtFassung.Pruefsumme(o));
        return StadtFassung.Lesen(PythonJson.Utf8(PythonJson.Text(o)));
    }

    private static readonly Lazy<StadtFassung> Fassung = new(() => Umbenannt(Neu));

    [Fact]
    public void Umbenannt_Laedt_StadtZeigtNamen_AufgabenDa()
    {
        var f = Fassung.Value;
        Assert.Equal([], StadtbauPruefung.Format(Py.Lesen(f.Schreiben())));
        Assert.Equal([], StadtbauPruefung.Inhalt(f, Inhalt));
        Assert.Equal([], SpielPruefung.Welt(f.MitKarte(Inhalt)));
        var inhalt = f.MitKarte(Inhalt);
        var stand = new Spielstand([], inhalt) { TageFertig = 29 };
        var vorher = StadtbauFassungTests.Fassung0.Value;
        var alt = vorher.StadtFuer(vorher.MitKarte(Inhalt)).Orte(stand);
        var neu = f.StadtFuer(inhalt).Orte(stand);
        Assert.Equal(alt.Count, neu.Count);
        for (var i = 0; i < alt.Count; i++)
        {
            var (a, n) = (alt[i], neu[i]);
            Assert.Equal(a["id"], n["id"]);
            Assert.Equal(a["ansicht"], n["ansicht"]);
            Assert.Equal(Py.Gleich(a["id"], "talheim_nord") ? Neu : a["name"], n["name"]);
        }
        // Aufgaben, Kunde und Innenraum haengen an der Kennung und sind dieselben Objekte
        Assert.Same(Inhalt["aufgaben"], inhalt["aufgaben"]);
        Assert.Same(Inhalt["kunden"], inhalt["kunden"]);
        Assert.Contains(SpielDaten.Kundenorte(inhalt), k => Py.Gleich(k["id"], "talheim_nord"));
    }

    [Fact]
    public void Umbenannt_StadtFingerabdruckGleich()
    {
        var (spiel, staende) = SpielStadtTests.StadtSzenario(inhalt: Fassung.Value.MitKarte(Inhalt));
        Assert.Equal(SpielStadtTests.SollStadtHash, SpielStadtTests.StadtHash(spiel, staende));
    }

    [Fact]
    public void Gegenprobe_DoppelterName_Rot_EindeutigerGruen()
    {
        var doppelt = StadtbauPruefung.Aussen(Umbenannt("Bahnhof Talheim"), Inhalt, SpielGrafikTests.Bibliothek.Value);
        Assert.Contains(doppelt, x => x.Rot && x.Text.Contains("schon ein anderer Ort", StringComparison.Ordinal));
        var eindeutig = StadtbauPruefung.Aussen(Fassung.Value, Inhalt, SpielGrafikTests.Bibliothek.Value);
        Assert.DoesNotContain(eindeutig, x => x.Text.StartsWith("Ort „", StringComparison.Ordinal));
    }
}
