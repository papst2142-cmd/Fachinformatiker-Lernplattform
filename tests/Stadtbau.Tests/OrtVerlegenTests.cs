using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Weisung P12-84 Frage 4 = c (Sammelliste 239): Talheim-Nord und Birkenhain bleiben, Nico verlegt sie beim Bau von Stadt 1
/// mit dem Werkzeug. Ein Ort mit Kunden laesst sich verschieben, ohne dass Innenraum, Pruefung oder die Aufgaben brechen:
/// Kennung, Rolle, Freischaltung und Ansicht bleiben, die Innenraeume der Fassung bleiben gleich, und der Export laedt die
/// Stadt mit allen Inhalten des Spiels (<see cref="StadtbauPruefung.Inhalt"/>). Ein anderes Modell fuer einen Ort kann das
/// Werkzeug noch nicht geben (Liste „Werkzeug komplett?“, Meldung P12-54).
/// </summary>
public class OrtVerlegenTests
{
    private static string Json(object? o) => PythonJson.Standard(o);

    private static Projekt Stadt1()
    {
        var p = Projekt.Neu("Orte verlegen", Daten.Spiel, Daten.NeueAblage());
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        return p;
    }

    [Theory]
    [InlineData("talheim_nord", -8.0, 0.0, 20.3, 3.0)]
    [InlineData("birkenhain", 0.0, -2.0, 11.4, 6.3)]
    public void OrtMitKunden_Verschieben_InnenraumUndPruefungUndExportBleiben(string ort, double dx, double dy, double x, double y)
    {
        var p = Stadt1();
        var stelle = new AussenStelle(AussenArt.Haus, -1, 0, ort);
        // Innenraeume der Kunden baut das Spiel selbst (Raeume des Kunden), sie sind im Werkzeug kein bearbeitbarer Innenraum
        Assert.Null(p.InnenortVon(stelle));
        var innenraeume = Json(Daten.Spiel.Fassung.Innenraeume);
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot);

        p.AussenAendern("Ort verlegen", a => AussenBearbeitung.Verschieben(a, stelle, dx, dy));

        var o = AussenBearbeitung.Ort(p.AussenStand, ort)!;
        Assert.Equal((x, y), (Py.F(o["x"]), Py.F(o["y"])));
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot);
        var neu = StadtFassung.Lesen(p.Exportieren(DateTimeOffset.Now));
        var ausgeliefert = Liste(neu.Karte["orte"]).OfType<PyObjekt>().Single(t => Py.Gleich(t["id"], ort));
        Assert.Equal((x, y), (Py.F(ausgeliefert["x"]), Py.F(ausgeliefert["y"])));
        Assert.Equal(Py.Str(o["rolle"]), Py.Str(ausgeliefert["rolle"]));
        Assert.Equal(Json(o["frei"]), Json(ausgeliefert["frei"]));
        Assert.Equal(Json(o["ansicht"]), Json(ausgeliefert["ansicht"]));
        Assert.Equal(innenraeume, Json(neu.Innenraeume));
    }

    [Fact]
    public void Gegenprobe_OrtAufDieStrasse_RotUndExportGesperrt()
    {
        var p = Stadt1();
        p.AussenAendern("Ort verlegen", a => AussenBearbeitung.Verschieben(a, new AussenStelle(AussenArt.Haus, -1, 0, "talheim_nord"), -4, 0));
        Assert.Contains(p.PruefenAussen(), f => f.Rot);
        Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
    }
}
