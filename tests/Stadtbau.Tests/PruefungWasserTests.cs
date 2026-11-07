using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.8.0 (Weisung P12-47, Plan P12-26b Abschnitt 10): jede Regel fuer Wasser (<see cref="StadtbauPruefung.Aussen"/>) mit
/// einem roten und einem gruenen Fall auf Stadt 1. Der Probeteich liegt auf freier Wiese oben rechts
/// (<see cref="TeichX"/>, <see cref="TeichY"/>, 3 × 2); Fassung 0 mit ihm hat keinen Fund ausser Laterne 20.
/// </summary>
public class PruefungWasserTests
{
    private static Spieldaten D => Daten.Spiel;

    /// <summary>Ecke oben links des Probeteichs (3 breit, 2 tief).</summary>
    public const double TeichX = 22.5;

    public const double TeichY = 1.5;

    private static List<StadtbauFund> Pruefen(Action<PyObjekt> aenderung)
    {
        var a = Aussen.Aus(D.Fassung);
        Daten.Laterne20Frei(a);
        aenderung(a);
        var f = Aussen.Fassung(a, D.Fassung, D.Fassung.Innenraeume, 1, 0, "test", "", "");
        return StadtbauPruefung.Aussen(f, D.Inhalt, D.Grafik);
    }

    private static List<object?> P(double x, double y) => [x, y];

    private static PyObjekt W(PyObjekt a) => Aussen.WasserAnlegen(a);

    private static void Teich(PyObjekt a, params double[] xy)
    {
        var punkte = new List<object?>();
        for (var i = 0; i + 1 < xy.Length; i += 2)
        {
            punkte.Add(P(xy[i], xy[i + 1]));
        }
        Liste(W(a)["teiche"]).Add(new PyObjekt { { "punkte", punkte } });
    }

    /// <summary>Rechteckiger Teich (Ecke x, y; Breite w, Tiefe t).</summary>
    private static void Rechteck(PyObjekt a, double x, double y, double w, double t) => Teich(a, x, y, x + w, y, x + w, y + t, x, y + t);

    private static void Probeteich(PyObjekt a) => Rechteck(a, TeichX, TeichY, 3, 2);

    private static void Steg(PyObjekt a, double x, double y, double w, double t, object dreh) =>
        Liste(W(a)["stege"]).Add(new PyObjekt { { "x", x }, { "y", y }, { "w", w }, { "t", t }, { "dreh", dreh } });

    private static void Kat(PyObjekt a, string id, double x, double y) =>
        Aussen.KatalogStueckeAnlegen(a).Add(new PyObjekt { { "id", id }, { "x", x }, { "y", y }, { "w", 0.3 }, { "t", 0.3 }, { "dreh", 0L } });

    private static PyObjekt Land(PyObjekt a, int i) => Objekt(Aussen.Landschaft(a)[i]);

    private static int BaumNr(PyObjekt a) => Aussen.Landschaft(a).FindIndex(t => t is PyObjekt o && Py.Gleich(o["typ"], "baum"));

    private static void Strasse(PyObjekt a, double x1, double y1, double x2, double y2) =>
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(x1, y1), P(x2, y2) } } });

    private static void Gebaeude(PyObjekt a, double x, double y)
    {
        Py.Setzen(Land(a, 13), "x", x);
        Py.Setzen(Land(a, 13), "y", y);
    }

    /// <summary>
    /// Filiale eingeschlossen: Teich A als Wand westlich von Strasse 2, Teich B noerdlich, dazu Kartenrand oben und links.
    /// Rot: der Fussweg endet vor Teich A (<paramref name="wegKurz"/>); gruen: der Fussweg fuehrt wie bisher bis zur Strasse.
    /// </summary>
    private static void FilialeEingeschlossen(PyObjekt a, bool wegKurz)
    {
        if (wegKurz)
        {
            var weg = Liste(AussenBearbeitung.Zugang(a, "filiale")!["weg"]);
            weg[^1] = P(5.5, 3.8);
        }
        Rechteck(a, 5.9, -1, 1, 8);
        Rechteck(a, -1, 6, 7.9, 1);
    }

    public static readonly TheoryData<string, string> Faelle = new()
    {
        { "Eis_Wert", "Der Schalter „eis“ hat den Wert" },
        { "Wasser_Objekt", "Der Teil „wasser“ der Stadt ist falsch gespeichert" },
        { "Teich_Daten", "Teich Nr. 1 ist falsch gespeichert" },
        { "Teich_Rand", "Teich Nr. 1 reicht über den Rand der Karte" },
        { "Teich_Klein", "Teich Nr. 1 ist kleiner als 1 × 1" },
        { "Teich_Kreuzt", "Der Rand von Teich Nr. 1 kreuzt sich selbst" },
        { "Steg_Daten", "Steg Nr. 1 ist falsch gespeichert" },
        { "Steg_Rand", "Steg Nr. 1 reicht über den Rand der Karte" },
        { "Haus_Teich", "„stellwerk“ steht im Wasser oder auf dem Ufer" },
        { "Haus_Ufer", "„stellwerk“ steht im Wasser oder auf dem Ufer" },
        { "Haus_Fluss", "„stellwerk“ steht im Wasser oder auf dem Ufer" },
        { "Ort_Teich", "„Zuhause“ steht im Wasser oder auf dem Ufer" },
        { "Katalog_Teich", "„cc0:car_kit/sedan“ steht im Wasser oder auf dem Ufer" },
        { "Katalog_Fluss", "„cc0:car_kit/sedan“ steht im Wasser oder auf dem Ufer" },
        { "Strasse_Teich", "führt durch Teich Nr. 1" },
        { "Strasse_FlussLaengs", "lang im Fluss (quer als Brücke höchstens" },
        { "Laterne_Teich", "steht im Wasser." },
        { "Laterne_Fluss", "steht im Wasser." },
        { "Baum_Teich", "Ein Baum steht im Wasser" },
        { "Baum_Fluss", "Ein Baum steht im Wasser" },
        { "Beet_Teich", "Beet Nr. " },
        { "Park_Teich", "Ein Park liegt im Wasser" },
        { "Park_Fluss", "Ein Park liegt im Wasser" },
        { "Steg_Ufer", "Steg Nr. 1 berührt mit keinem Ende das Ufer" },
        { "Steg_Land", "Steg Nr. 1 berührt mit keinem Ende das Ufer" },
        { "Steg_Fluss", "Steg Nr. 1 berührt mit keinem Ende das Ufer" },
        { "Ort_Erreichbar", "„filiale“ ist zu Fuß nicht erreichbar" },
    };

    private static (Action<PyObjekt> Rot, Action<PyObjekt> Gruen) Fall(string regel) => regel switch
    {
        "Eis_Wert" => (a => Py.Setzen(W(a), "eis", "ja"), a => Py.Setzen(W(a), "eis", true)),
        "Wasser_Objekt" => (a => Py.Setzen(Aussen.Stadtdaten(a), Wasser.Abschnitt, new List<object?>()), a => W(a)),
        "Teich_Daten" => (a => Teich(a, 23, 2, 25, 2), a => Teich(a, 23, 2, 25, 2, 24, 3.5)),
        "Teich_Rand" => (a => Rechteck(a, 30, -0.5, 2, 2), a => Rechteck(a, 29.5, 0, 2, 2)),
        "Teich_Klein" => (a => Rechteck(a, TeichX, TeichY, 0.9, 2), a => Rechteck(a, TeichX, TeichY, 1, 2)),
        "Teich_Kreuzt" => (a => Teich(a, 22.5, 1.5, 25.5, 3.5, 25.5, 1.5, 22.5, 3.5), a => Teich(a, 22.5, 1.5, 25.5, 1.5, 25.5, 3.5, 22.5, 3.5)),
        "Steg_Daten" => (a => Steg(a, 24, 2.5, 2, 0.5, 5L), a => Steg(a, 24, 2.5, 2, 0.5, 3L)),
        "Steg_Rand" => (a => Steg(a, 31.5, 2, 2, 0.5, 0L), a => Steg(a, 31, 2, 2, 0.5, 0L)),
        // Stellwerk (Land 13) mitten im Teich, auf dem Ufer, im Fluss; gruen: daneben
        "Haus_Teich" => (a => { Probeteich(a); Gebaeude(a, 23.5, 2); }, a => { Probeteich(a); Gebaeude(a, 27, 1.5); }),
        "Haus_Ufer" => (a => { Rechteck(a, 22.5, 3.1, 3, 2); Gebaeude(a, 23.5, 1.95); }, a => { Rechteck(a, 22.5, 3.3, 3, 2); Gebaeude(a, 23.5, 1.95); }),
        "Haus_Fluss" => (a => Gebaeude(a, 2.5, 13.2), a => Gebaeude(a, 2.5, 11.8)),
        "Ort_Teich" => (a => Rechteck(a, 26.5, 17.5, 2, 2), a => Rechteck(a, 26.5, 15.0, 2, 2)),
        "Katalog_Teich" => (a => { Probeteich(a); Kat(a, "car_kit/sedan", 24, 2.5); }, a => { Probeteich(a); Kat(a, "car_kit/sedan", 24, 4.0); }),
        "Katalog_Fluss" => (a => Kat(a, "car_kit/sedan", 2.2, 13.8), a => Kat(a, "car_kit/sedan", 2.2, 12.8)),
        "Strasse_Teich" => (a => { Probeteich(a); Strasse(a, 22, 2.5, 26, 2.5); }, a => { Probeteich(a); Strasse(a, 22, 0.4, 26, 0.4); }),
        // laengs im Fluss (seinem ersten Stueck entlang) rot, quer darueber (Bruecke) gruen
        "Strasse_FlussLaengs" => (a => Strasse(a, 0.5, 13.5, 4.0, 14.1), a => Strasse(a, 2.2, 11.5, 2.4, 16.0)),
        "Laterne_Teich" => (a => { Probeteich(a); Aussen.Laternen(a).Add(new List<object?> { 24.0, 2.5, false }); }, a => Probeteich(a)),
        "Laterne_Fluss" => (a => Aussen.Laternen(a).Add(new List<object?> { 2.2, 13.8, false }), a => { }),
        "Baum_Teich" => (a => { Probeteich(a); Liste(Land(a, BaumNr(a))["punkte"]).Add(P(24, 2.5)); }, a => { Probeteich(a); Liste(Land(a, BaumNr(a))["punkte"]).Add(P(24, 4.5)); }),
        "Baum_Fluss" => (a => Liste(Land(a, BaumNr(a))["punkte"]).Add(P(2.2, 13.8)), a => Liste(Land(a, BaumNr(a))["punkte"]).Add(P(2.2, 12.5))),
        "Beet_Teich" => (a => { Probeteich(a); Aussen.Beete(a).Add(P(24, 2.5)); }, a => { Probeteich(a); Aussen.Beete(a).Add(P(24, 4.5)); }),
        // Park 0 liegt bei x 8,6 bis 16,2 und y 4,6 bis 12: Teich mitten im Park rot, daneben gruen
        "Park_Teich" => (a => Rechteck(a, 10, 6, 2, 2), a => Probeteich(a)),
        "Park_Fluss" => (a => Py.Setzen(Land(a, 0), "y", 12.8), a => { }),
        // Steg ganz im Teich (beide Enden im Wasser) rot, ein Ende auf dem Ufer gruen (Teich 22,5 bis 25,5)
        "Steg_Ufer" => (a => { Probeteich(a); Steg(a, 24, 2.5, 1.5, 0.5, 0L); }, a => { Probeteich(a); Steg(a, 23.2, 2.5, 1.5, 0.5, 0L); }),
        "Steg_Land" => (a => { Probeteich(a); Steg(a, 27.5, 2.5, 1.5, 0.5, 0L); }, a => { Probeteich(a); Steg(a, 26.3, 2.5, 1.5, 0.5, 0L); }),
        // Steg ueber den Fluss (Fussgaengerbruecke von Ufer zu Ufer) gruen, daneben auf der Wiese rot
        "Steg_Fluss" => (a => Steg(a, 2.5, 11.5, 1.2, 0.4, 1L), a => Steg(a, 2.3, 13.8, 1.2, 0.4, 1L)),
        "Ort_Erreichbar" => (a => FilialeEingeschlossen(a, true), a => FilialeEingeschlossen(a, false)),
        _ => throw new ArgumentException(regel),
    };

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

    /// <summary>Stadt 1 mit Probeteich und Steg (wie die Bilder und die Grenzen der Meldung): kein Fund.</summary>
    [Fact]
    public void Stadt1_MitTeichUndSteg_OhneFund()
    {
        var f = Pruefen(a =>
        {
            Probeteich(a);
            Steg(a, 23.2, 2.5, 1.5, 0.5, 0L);
            Py.Setzen(W(a), "eis", true);
        });
        Assert.True(f.Count == 0, string.Join(" | ", f.Select(x => x.Text)));
    }

    /// <summary>Fassung 0 hat kein Wasser: keine Regel fuer Wasser meldet etwas (gleich der Probe ohne die Regeln).</summary>
    [Fact]
    public void Fassung0_OhneWasserFund()
    {
        Assert.False(Py.Hat(D.Fassung.Stadtdaten, Wasser.Abschnitt));
        var f = Pruefen(_ => { });
        Assert.True(f.Count == 0, string.Join(" | ", f.Select(x => x.Text)));
    }

    /// <summary>Die Bruecken von Stadt 1 liegen weit unter der Grenze fuer „laengs im Fluss“ (Gegenprobe zur Grenze).</summary>
    [Fact]
    public void Bruecken_Stadt1_UnterDerGrenze()
    {
        var stadt = D.Fassung.StadtFuer(D.Fassung.MitKarte(D.Inhalt));
        var szene = StadtSzene.Bauen(stadt, null);
        var laengen = szene.Strassen.SelectMany(s => szene.Fluesse.Select(f => StadtbauPruefung.LaengsIm(s, f, (StadtSzene.FlussBreite + 0.3f) / 2).Laenge)).Where(l => l > 0).ToList();
        Assert.Equal(2, laengen.Count);
        Assert.All(laengen, l => Assert.InRange(l, 0.9f, 1.2f));
        Assert.True(StadtbauPruefung.FlussQuerHoechstens > 2 * laengen.Max());
    }
}
