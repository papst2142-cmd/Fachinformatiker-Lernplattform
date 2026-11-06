using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Phase 13 Welle 3 (Weisung P12-18): jede Regel der Pruefung draussen (<see cref="StadtbauPruefung.Aussen"/>) mit einem
/// roten und einem gruenen Fall. Der rote Fall aendert Stadt 1 so, dass genau diese Regel greift; der gruene Fall ist die
/// naechstliegende erlaubte Aenderung (gleiche Stelle, innerhalb der Grenze) und darf diese Regel nicht melden.
/// Fassung 0 selbst hat draussen keinen Fund.
/// </summary>
public class PruefungAussenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static List<StadtbauFund> Pruefen(Action<PyObjekt> aenderung)
    {
        var a = Aussen.Aus(D.Fassung);
        aenderung(a);
        var f = Aussen.Fassung(a, D.Fassung, D.Fassung.Innenraeume, 1, 0, "test", "", "");
        return StadtbauPruefung.Aussen(f, D.Inhalt, D.Grafik);
    }

    private static PyObjekt Land(PyObjekt a, int i) => Objekt(Aussen.Landschaft(a)[i]);

    private static List<object?> P(double x, double y) => [x, y];

    private static PyObjekt Wohnweg(PyObjekt a) => AussenBearbeitung.Zugang(a, "zuhause")!;

    private static void Gebaeude(PyObjekt a, double x, double y)
    {
        Py.Setzen(Land(a, 13), "x", x);
        Py.Setzen(Land(a, 13), "y", y);
    }

    /// <summary>Regel (Textstueck des Funds), roter Fall, gruener Fall.</summary>
    public static readonly TheoryData<string, string> Faelle = new()
    {
        { "Strasse_ZweiPunkte", "hat weniger als zwei Punkte" },
        { "Strasse_Rand", "reicht über den Rand der Karte" },
        { "Gehweg_Breite", "Der Gehweg an Straße" },
        { "Gebaeude_Modell", "gibt es im Spiel nicht" },
        { "Gebaeude_Karte", "Ein Haus („stellwerk“) liegt außerhalb der Karte" },
        { "Baum_Karte", "Ein Baum liegt außerhalb der Karte" },
        { "Park_Karte", "Ein Park liegt nicht ganz auf der Karte" },
        { "Ort_Karte", "„Zuhause“ liegt außerhalb der Karte" },
        { "Laterne_Karte", "Laterne Nr. 1 liegt außerhalb der Karte" },
        { "Beet_Karte", "Beet Nr. 1 liegt außerhalb der Karte" },
        { "Fussweg_Fehlt", "hat keinen Fußweg" },
        { "Fussweg_Strasse", "erreicht keine Straße" },
        { "Tuer_ImHaus", "beginnt im Haus" },
        { "Laterne_Verschoben", "verschoben werden, damit sie frei steht" },
        { "Laterne_Lichtraum", "steht im Lichtraum der Bahn" },
        { "Andreaskreuz_Lichtraum", "Andreaskreuz am Bahnübergang" },
        { "Baum_Lichtraum", "Ein Baum steht im Lichtraum der Bahn" },
        { "Beet_Lichtraum", "Ein Beet liegt im Lichtraum der Bahn" },
        { "Haus_Gleis", "steht auf dem Gleis" },
        { "Haeuser_Ineinander", "stehen ineinander" },
        { "Haus_Strasse", "steht auf einer Straße oder ihrem Gehweg" },
        { "Baum_Haus", "Ein Baum steht im Haus" },
        { "Beet_Haus", "Ein Beet liegt im Haus" },
        { "Laterne_Haus", "steht im Haus „Zuhause“" },
        { "Paket_ImHaus", "Der Paketplatz liegt im Haus" },
        { "Paket_Fussweg", "Der Paketplatz liegt auf dem Fußweg" },
        { "Paket_Baum", "Der Paketplatz liegt in einem Baum" },
        { "Paket_HinterTuer", "Der Paketplatz liegt hinter der Haustür" },
        { "Stadt_Laden", "Die Stadt lässt sich nicht laden" },
        { "Katalog_Haus", "„cc0:car_kit/sedan“ steht im Haus „Zuhause“" },
        { "Effekt_Haus", "„cc0:particles/flame_05_rotated“ steht im Haus „Zuhause“" },
        { "Katalog_Baum", "„cc0:car_kit/sedan“ steht auf einem Baum" },
        { "Katalog_Laterne", "„cc0:car_kit/sedan“ steht an einer Laterne" },
        { "Katalog_Fahrbahn", "„cc0:car_kit/sedan“ steht auf der Fahrbahn" },
        { "Katalog_Gleis", "„cc0:car_kit/sedan“ steht auf dem Gleis" },
    };

    /// <summary>Ein Stueck aus dem Katalog draussen (Mitte x, y; Breite und Tiefe 0,3).</summary>
    private static void Kat(PyObjekt a, string id, List<object?> p) =>
        Aussen.KatalogStueckeAnlegen(a).Add(new PyObjekt { { "id", id }, { "x", p[0] }, { "y", p[1] }, { "w", 0.3 }, { "t", 0.3 }, { "dreh", 0L } });

    private static (Action<PyObjekt> Rot, Action<PyObjekt> Gruen) Fall(string regel) => regel switch
    {
        "Strasse_ZweiPunkte" => (a => Py.Setzen(Land(a, 7), "punkte", new List<object?> { P(7.5, 12.6) }), a => Py.Setzen(Land(a, 7), "punkte", new List<object?> { P(7.5, 12.6), P(18.4, 12.6) })),
        "Strasse_Rand" => (a => Liste(Land(a, 3)["punkte"])[1] = P(32.5, 20.8), a => Liste(Land(a, 3)["punkte"])[1] = P(32, 20.8)),
        "Gehweg_Breite" => (a => Py.Setzen(Land(a, 7), "gehweg", 0.61), a => Py.Setzen(Land(a, 7), "gehweg", 0.6)),
        "Gebaeude_Modell" => (a => Py.Setzen(Land(a, 13), "modell", "raumschiff"), a => Py.Setzen(Land(a, 13), "modell", "stellwerk")),
        "Gebaeude_Karte" => (a => Gebaeude(a, 32.5, 12.9), a => Gebaeude(a, 16.6, 12.9)),
        "Baum_Karte" => (a => Liste(Land(a, BaumNr(a))["punkte"]).Add(P(32.3, 1)), a => Liste(Land(a, BaumNr(a))["punkte"]).Add(P(31.7, 1))),
        "Park_Karte" => (a => Py.Setzen(Land(a, 0), "x", 24.6), a => Py.Setzen(Land(a, 0), "x", 24.4)),
        "Ort_Karte" => (a => Py.Setzen(AussenBearbeitung.Ort(a, "zuhause")!, "x", 33.0), a => Py.Setzen(AussenBearbeitung.Ort(a, "zuhause")!, "x", 26.0)),
        "Laterne_Karte" => (a => Liste(Aussen.Laternen(a)[0])[1] = -0.1, a => Liste(Aussen.Laternen(a)[0])[1] = 0.65),
        "Beet_Karte" => (a => Liste(Aussen.Beete(a)[0])[0] = 32.2, a => Liste(Aussen.Beete(a)[0])[0] = Py.F(Liste(Aussen.Beete(a)[0])[0])),
        "Fussweg_Fehlt" => (a => Py.Setzen(Wohnweg(a), "weg", new List<object?>()), a => Py.Setzen(Wohnweg(a), "weg", new List<object?> { P(27.7, 19.8), P(27.7, 20.8) })),
        "Fussweg_Strasse" => (a => Liste(Wohnweg(a)["weg"])[1] = P(27.7, 20.7), a => Liste(Wohnweg(a)["weg"])[1] = P(27.7, 20.81)),
        "Tuer_ImHaus" => (a => Liste(Wohnweg(a)["weg"])[0] = P(27.7, 19.6), a => Liste(Wohnweg(a)["weg"])[0] = P(27.7, 19.8)),
        "Laterne_Verschoben" => (a => Liste(Land(a, BaumNr(a))["punkte"]).Add(P(8.0, 0.75)), a => Liste(Land(a, BaumNr(a))["punkte"]).Add(P(9.5, 0.75))),
        "Laterne_Lichtraum" => (a => Aussen.Laternen(a)[3] = new List<object?> { 7.0, 6.6, false }, a => Aussen.Laternen(a)[3] = new List<object?> { 7.0, 7.72, false }),
        "Andreaskreuz_Lichtraum" => (a => Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(9.0, 7.0), P(15.0, 9.7) } } }),
            a => Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(12.0, 7.0), P(12.0, 11.5) } } })),
        "Baum_Lichtraum" => (a => Liste(Land(a, BaumNr(a))["punkte"]).Add(P(18.5, 10.5)), a => Liste(Land(a, BaumNr(a))["punkte"]).Add(P(18.5, 11.6))),
        "Beet_Lichtraum" => (a => Aussen.Beete(a).Add(P(12, 9.3)), a => Aussen.Beete(a).Add(P(12, 10.2))),
        "Haus_Gleis" => (a => Gebaeude(a, 12, 8.6), a => Gebaeude(a, 12, 10.6)),
        "Haeuser_Ineinander" => (a => Aussen.Landschaft(a).Add(Py.Kopie(Land(a, 13))), a =>
        {
            var k = (PyObjekt)Py.Kopie(Land(a, 13))!;
            Py.Setzen(k, "x", 22.0);
            Py.Setzen(k, "y", 1.0);
            Aussen.Landschaft(a).Add(k);
        }),
        "Haus_Strasse" => (a => Gebaeude(a, 16.6, 12.4), a => Gebaeude(a, 16.6, 12.9)),
        "Baum_Haus" => (a => Liste(Land(a, BaumNr(a))["punkte"]).Add(WohnungMitte(a)), a => Liste(Land(a, BaumNr(a))["punkte"]).Add(P(22, 2))),
        "Beet_Haus" => (a => Aussen.Beete(a).Add(WohnungMitte(a)), a => Aussen.Beete(a).Add(P(22, 2))),
        "Laterne_Haus" => (a => Aussen.Laternen(a).Add(new List<object?> { WohnungMitte(a)[0], WohnungMitte(a)[1], false }), a => { }),
        "Paket_ImHaus" => (a => Paket(a, -1.0, 0.42), a => Paket(a, 0.5, 0.42)),
        "Paket_Fussweg" => (a => Paket(a, 0.5, 0.05), a => Paket(a, 0.5, 0.42)),
        "Paket_Baum" => (a => Liste(Land(a, BaumNr(a))["punkte"]).Add(PaketMitte(a)), a => Liste(Land(a, BaumNr(a))["punkte"]).Add(P(22, 2))),
        "Paket_HinterTuer" => (a => Paket(a, -0.05, 0.6), a => Paket(a, 0.1, 0.6)),
        "Stadt_Laden" => (a => Liste(Aussen.Karte(a)["orte"]).Add(new PyObjekt { { "id", "kaputt" } }), a => { }),
        // Sammelliste 223: Stuecke aus dem Katalog nicht im Haus (auch kein Effekt), Modelle nicht auf Baum, Laterne, Fahrbahn
        "Katalog_Haus" => (a => Kat(a, "car_kit/sedan", WohnungMitte(a)), a => Kat(a, "car_kit/sedan", P(22, 2))),
        "Effekt_Haus" => (a => Kat(a, "particles/flame_05_rotated", WohnungMitte(a)), a => Kat(a, "particles/flame_05_rotated", P(24, 20.8))),
        "Katalog_Baum" => (a => { Liste(Land(a, BaumNr(a))["punkte"]).Add(P(22, 2)); Kat(a, "car_kit/sedan", P(22.1, 2)); },
            a => { Liste(Land(a, BaumNr(a))["punkte"]).Add(P(22, 2)); Kat(a, "car_kit/sedan", P(22.7, 2)); }),
        "Katalog_Laterne" => (a => Kat(a, "car_kit/sedan", P(8.0, 0.75)), a => Kat(a, "car_kit/sedan", P(22, 2))),
        "Katalog_Fahrbahn" => (a => Kat(a, "car_kit/sedan", P(24, 20.8)), a => Kat(a, "car_kit/sedan", P(22, 2))),
        "Katalog_Gleis" => (a => Kat(a, "car_kit/sedan", P(2, 5.97)), a => Kat(a, "car_kit/sedan", P(2, 6.6))),
        _ => throw new ArgumentException(regel),
    };

    private static int BaumNr(PyObjekt a) => Aussen.Landschaft(a).FindIndex(t => t is PyObjekt o && Py.Gleich(o["typ"], "baum"));

    private static void Paket(PyObjekt a, double vor, double seitlich) =>
        Py.Setzen(Objekt(Aussen.Stadtdaten(a)["darstellung"]), "paket_platz", new PyObjekt { { "vor", vor }, { "seitlich", seitlich } });

    private static List<object?> PaketMitte(PyObjekt a) => AussenBearbeitung.PaketLage(a)!.Value.Mitte is var m ? P(m.X, m.Y) : [];

    private static List<object?> WohnungMitte(PyObjekt a)
    {
        var (x, y, w, h) = AussenBearbeitung.HausFlaeche(a, new AussenStelle(AussenArt.Haus, -1, 0, "zuhause"));
        return P(x + (w / 2), y + (h / 2));
    }

    [Fact]
    public void Laternen_OhneStrasse_RotMitStrasseGruen()
    {
        // leere Stadt: Laterne ohne Strasse ist rot, mit einer Strasse daneben gruen
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_test", 20, 16));
        Aussen.Laternen(a).Add(new List<object?> { 5.0, 5.5, false });
        var leer = new PyObjekt();
        var rot = StadtbauPruefung.Aussen(Aussen.Fassung(a, D.Fassung, leer, 1, null, "t", "", ""), D.Inhalt, D.Grafik);
        Assert.Contains(rot, f => f.Rot && f.Text == "Es gibt Laternen, aber keine Straße.");
        AussenBearbeitung.Setzen(a, AussenArt.Strasse, 5, 5);
        var gruen = StadtbauPruefung.Aussen(Aussen.Fassung(a, D.Fassung, leer, 1, null, "t", "", ""), D.Inhalt, D.Grafik);
        Assert.True(gruen.Count == 0, string.Join(" | ", gruen.Select(f => f.Text)));
    }

    [Fact]
    public void TuerMitte_UndTuerAufDerWand_RotUndGruen()
    {
        // Gehwegmitte = Tuermitte: das Spiel setzt die Tuer genau auf den Anfang des Fusswegs (W8b), darum greift die
        // Regel nur, wenn sich das aendert; geprueft wird die Rechnung selbst (Grenze 0,02 quer zur Tuer)
        var tuer = new StadtTuer("x", new System.Numerics.Vector2(5, 5), new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(5.03f, 5.2f), false);
        Assert.True(StadtbauPruefung.TuerVersatz(tuer) > StadtbauPruefung.MittigGrenze);
        Assert.True(StadtbauPruefung.TuerVersatz(tuer with { WegAnfang = new System.Numerics.Vector2(5.01f, 5.2f) }) <= StadtbauPruefung.MittigGrenze);
        // Tuer auf der Wand eines Blocks 4 × 2 bei (2, 3): auf der Vorderwand 0, 0,5 davor zu weit
        Assert.Equal(0f, StadtbauPruefung.AufWand(2, 3, 4, 2, new System.Numerics.Vector2(3, 5)), 3);
        Assert.True(StadtbauPruefung.AufWand(2, 3, 4, 2, new System.Numerics.Vector2(3, 5.5f)) > StadtbauPruefung.WandAbstandModell);
        Assert.True(StadtbauPruefung.AufWand(2, 3, 4, 2, new System.Numerics.Vector2(3, 5.3f)) <= StadtbauPruefung.WandAbstandModell);
        // neben dem Block (ausserhalb der Spanne beider Waende): weit weg
        Assert.True(StadtbauPruefung.AufWand(2, 3, 4, 2, new System.Numerics.Vector2(9, 9)) > 1);
    }

    /// <summary>
    /// Weisung P12-30 (Sammelliste 220): Fassung 0 draussen hat genau einen Fund, Laterne 20 im spaeten Spielstand (vor der
    /// Tuer der Betriebswerkstatt, die erst ab Tag 22 steht). Nico verschiebt sie in seinem Projekt; sonst kein Fund.
    /// </summary>
    [Fact]
    public void Fassung0_DraussenNurLaterne20()
    {
        var funde = Pruefen(_ => { });
        Assert.True(funde.Count == 1 && funde[0].Rot && funde[0].Text == "Laterne Nr. 20 muss um 0,4 verschoben werden, sobald alle Orte zu sehen sind (erlaubt 0,2).",
            string.Join(" | ", funde.Select(f => f.Text)));
        // verschoben (wie Nico im Werkzeug): kein Fund mehr
        var frei = Pruefen(Daten.Laterne20Frei);
        Assert.True(frei.Count == 0, string.Join(" | ", frei.Select(f => f.Text)));
    }

    /// <summary>Gegenprobe zu 220: ohne die Pruefung im spaeten Spielstand faende das Werkzeug Laterne 20 nicht (nur neuer Stand).</summary>
    [Fact]
    public void Laternen_SpaeterStand_FindetWasDerNeueStandNichtFindet()
    {
        var d = Daten.Spiel;
        var stadt = d.Fassung.StadtFuer(d.Fassung.MitKarte(d.Inhalt));
        var neu = StadtSzene.Bauen(stadt, null, d.Grafik).Laternen[19];
        var spaet = StadtSzene.Bauen(stadt, StadtbauPruefung.Probestand(stadt.Inhalt).Stand, d.Grafik).Laternen[19];
        Assert.True(neu.Verschoben <= 0.2f);
        Assert.True(spaet.Verschoben > 0.2f);
    }

    [Theory]
    [MemberData(nameof(Faelle))]
    public void Regel_RotUndGruen(string regel, string text)
    {
        var (rot, gruen) = Fall(regel);
        var r = Pruefen(rot);
        Assert.True(r.Any(f => f.Rot && f.Text.Contains(text, StringComparison.Ordinal)), regel + " rot: " + string.Join(" | ", r.Select(f => f.Text)));
        var g = Pruefen(gruen);
        Assert.False(g.Any(f => f.Text.Contains(text, StringComparison.Ordinal)), regel + " grün: " + string.Join(" | ", g.Select(f => f.Text)));
    }
}
