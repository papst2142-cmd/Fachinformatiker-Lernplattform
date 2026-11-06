using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Patch S 0.5.1 / A 0.43.1 (Weisungen P12-37 Punkt 1, P12-38, P12-39 Frage 1 = a): eine neue leere Stadt ist ganz
/// frei (Schalter stadtdaten.darstellung.kleingruen = false). Kein Kleingruen an Strassenraendern und auf freien
/// Flaechen, gesetzte Parks behalten Baeume und Bueschel. Aeltere Projekte ohne die Angabe bleiben wie sie waren.
/// </summary>
public class LeereStadtFreiTests
{
    private static Spieldaten D => Daten.Spiel;

    /// <summary>Leere Stadt 24 x 16 mit einer Strasse und einem Park, als Projekt (frei wie ab S 0.5.1).</summary>
    private static Projekt MitStrasseUndPark(Werkzeugordner ablage, string name)
    {
        var p = Projekt.Neu(name, D, ablage, leer: (24, 16));
        p.AussenAendern("Straße", a => AussenBearbeitung.Setzen(a, AussenArt.Strasse, 12, 8));
        p.AussenAendern("Park", a => AussenBearbeitung.Setzen(a, AussenArt.Park, 4, 3));
        return p;
    }

    private static List<(string Art, float X, float Z, float R)> Gruen(StadtFassung stand) =>
        Ansicht3D.Bauen(stand, D, Ansicht3D.Einstellung.Standard).Szene.GruenKlein!.Plaetze;

    private static bool ImPark(StadtFassung stand, float x, float z) =>
        ((List<object?>)((PyObjekt)stand.Karte["welt"]!)["landschaft"]!).OfType<PyObjekt>()
            .Where(l => Py.Gleich(l["typ"], "park"))
            .Any(l => x >= Py.F(l["x"]) - 0.5 && x <= Py.F(l["x"]) + Py.F(l["w"]) + 0.5 && z >= Py.F(l["y"]) - 0.5 && z <= Py.F(l["y"]) + Py.F(l["h"]) + 0.5);

    [Fact]
    public void NeueLeereStadt_GanzFrei_ParksBehaltenIhrGruen()
    {
        var p = MitStrasseUndPark(Daten.NeueAblage(), "Frei");
        Assert.True(p.Frei);
        var stand = p.Stand();
        Assert.Equal(false, ((PyObjekt)stand.Stadtdaten["darstellung"]!)["kleingruen"]);
        Assert.False(StadtSzene.KleingruenAn(stand.Stadtdaten));
        // Format und Inhalt der Datei fuer das Spiel kennen den Schalter (kein Fund)
        Assert.Empty(StadtbauPruefung.Format(Py.Lesen(stand.Schreiben())));
        Assert.DoesNotContain(Projekt.PruefenAussen(stand, D), f => f.Rot);
        var gruen = Gruen(stand);
        // nur im Park, nirgends sonst
        Assert.NotEmpty(gruen);
        Assert.All(gruen, g => Assert.True(ImPark(stand, g.X, g.Z), $"{g.Art} bei {g.X:F2}/{g.Z:F2} ausserhalb des Parks"));

        // Gegenprobe: dieselbe Stadt mit Kleingruen (Schalter entfernt) hat Gruen auch ausserhalb des Parks
        var mit = StadtFassung.Lesen(stand.Schreiben());
        var dar = (PyObjekt)mit.Stadtdaten["darstellung"]!;
        dar.RemoveAll(kv => kv.Key == "kleingruen");
        var gruenMit = Gruen(mit);
        Assert.True(StadtSzene.KleingruenAn(mit.Stadtdaten));
        Assert.Contains(gruenMit, g => !ImPark(mit, g.X, g.Z));
        Assert.True(gruenMit.Count > gruen.Count);
        // die Parks sind in beiden gleich
        Assert.Equal(gruen.Count, gruenMit.Count(g => ImPark(mit, g.X, g.Z) && gruen.Contains(g)));
    }

    [Fact]
    public void AeltereLeereStadt_BleibtWieSieWar()
    {
        var ablage = Daten.NeueAblage();
        var p = MitStrasseUndPark(ablage, "Alt");
        var text = File.ReadAllText(p.Datei);
        Assert.Contains("\"frei\": true", text, StringComparison.Ordinal);
        // Datei wie vor S 0.5.1 (ohne "frei")
        File.WriteAllText(p.Datei, text.Replace(", \"frei\": true", "", StringComparison.Ordinal).Replace(",\"frei\":true", "", StringComparison.Ordinal));
        Assert.DoesNotContain("frei", File.ReadAllText(p.Datei), StringComparison.Ordinal);
        var alt = Projekt.Oeffnen(p.Datei, D, ablage);
        Assert.False(alt.Frei);
        Assert.True(StadtSzene.KleingruenAn(alt.Stand().Stadtdaten));
        Assert.Contains(Gruen(alt.Stand()), g => !ImPark(alt.Stand(), g.X, g.Z));
        // die neue Datei oeffnet frei
        var neu = MitStrasseUndPark(ablage, "Neu");
        Assert.True(Projekt.Oeffnen(neu.Datei, D, ablage).Frei);
    }

    [Fact]
    public void Stadt1_UnveraendertMitKleingruen()
    {
        Assert.False(Py.Hat((PyObjekt)D.Fassung.Stadtdaten["darstellung"]!, "kleingruen"));
        Assert.True(StadtSzene.KleingruenAn(D.Fassung.Stadtdaten));
        Assert.False(Projekt.Neu("Stadt 1", D, Daten.NeueAblage()).Frei);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("\"nein\"")]
    [InlineData("null")]
    public void Pruefung_SchalterNurTrueOderFalse(string wert)
    {
        var stand = MitStrasseUndPark(Daten.NeueAblage(), "Pruefung").Stand();
        Assert.DoesNotContain(Projekt.PruefenAussen(stand, D), f => f.Rot && f.Text.Contains("kleingruen", StringComparison.Ordinal));
        var dar = (PyObjekt)stand.Stadtdaten["darstellung"]!;
        var i = dar.FindIndex(kv => kv.Key == "kleingruen");
        dar[i] = new KeyValuePair<string, object?>("kleingruen", Py.Lesen(wert));
        Assert.Contains(Projekt.PruefenAussen(stand, D), f => f.Rot && f.Text.Contains("kleingruen", StringComparison.Ordinal));
    }
}
