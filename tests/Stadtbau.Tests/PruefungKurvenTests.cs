using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle K1 (Weisung P12-55, Plan P12-36 Abschnitt 5): die Pruefregeln 1 bis 9 fuer Kurven, je Regel ein roter und ein
/// gruener Fall. Grundlage ist eine leere Stadt 24 × 16 mit gerader Strasse (2; 8)–(8; 8), Kurve 90° Radius 2 bis
/// (10; 10) und gerader Strasse bis (10; 15); sie hat keinen Fund.
/// </summary>
public class PruefungKurvenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static List<object?> P(double x, double y) => [x, y];

    private static PyObjekt Grundlage()
    {
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_kurven", 24, 16));
        Strasse(a, 2, 8, 8, 8);
        Kurve(a, "bogen", 90, 2, new Vector2(8, 8));
        Strasse(a, 10, 10, 10, 15);
        return a;
    }

    private static void Strasse(PyObjekt a, double x1, double y1, double x2, double y2) =>
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(x1, y1), P(x2, y2) } } });

    private static PyObjekt Kurve(PyObjekt a, string form, int winkel, double radius, Vector2 anfang, bool gespiegelt = false)
    {
        var punkte = Strassenkurve.Mittellinie(form, winkel, radius, gespiegelt).Select(q => (object?)P(Math.Round(q.X + anfang.X, 2), Math.Round(q.Y + anfang.Y, 2))).ToList();
        var l = new PyObjekt
        {
            { "typ", "strasse" },
            { "punkte", punkte },
            { "kurve", new PyObjekt { { "form", form }, { "winkel", (long)winkel }, { "radius", radius } } },
        };
        Aussen.Landschaft(a).Add(l);
        return l;
    }

    private static void Linie(PyObjekt a, string typ, params double[] xy)
    {
        var punkte = new List<object?>();
        for (var i = 0; i + 1 < xy.Length; i += 2)
        {
            punkte.Add(P(xy[i], xy[i + 1]));
        }
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", typ }, { "punkte", punkte } });
    }

    private static PyObjekt Letzte(PyObjekt a) => Objekt(Aussen.Landschaft(a)[^1]);

    private static List<StadtbauFund> Pruefen(PyObjekt a) =>
        StadtbauPruefung.Aussen(Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", ""), D.Inhalt, D.Grafik);

    private static string Text(List<StadtbauFund> f) => string.Join(" | ", f.Select(x => (x.Rot ? "rot: " : "gelb: ") + x.Text));

    private static void Gruen(PyObjekt a)
    {
        var f = Pruefen(a);
        Assert.True(f.Count == 0, Text(f));
    }

    private static void Rot(PyObjekt a, string anfang)
    {
        var f = Pruefen(a);
        Assert.True(f.Any(x => x.Rot && x.Text.StartsWith(anfang, StringComparison.Ordinal)), "erwartet rot „" + anfang + "“, gefunden: " + Text(f));
    }

    [Fact]
    public void Grundlage_OhneFund() => Gruen(Grundlage());

    [Fact]
    public void Regel1_RadiusNichtInDerListe()
    {
        var a = Grundlage();
        var k = Objekt(Aussen.Landschaft(a)[1]);
        Py.Setzen(Objekt(k["kurve"]), "radius", 1.5);
        Rot(a, "Die Kurve (Straße Nr. 2) hat eine Form oder einen Radius, die es nicht gibt.");
        Py.Setzen(Objekt(k["kurve"]), "radius", 2.0);
        Gruen(a);
        // Fluss: Radius je Eckpunkt nur 2, 3 oder 5; falsche Zahl von Werten ist falsch gespeichert
        Linie(a, "fluss", 14, 0, 18, 4, 22, 3);
        Py.Setzen(Letzte(a), "radien", new List<object?> { 3.0, 4.0, 3.0 });
        Rot(a, "Der Fluss Nr. 1 hat an Eckpunkt 2 den Radius 4, erlaubt sind 2, 3, 5.");
        Py.Setzen(Letzte(a), "radien", new List<object?> { 3.0, 4.0 });
        Rot(a, "Der Fluss Nr. 1 hat ein falsch gespeichertes Feld „radien“");
        Py.Setzen(Letzte(a), "radien", new List<object?> { 3.0, 5.0, 3.0 });
        Gruen(a);
    }

    [Fact]
    public void Regel2_KreuzungOderEinmuendungInDerKurve()
    {
        // eine Strasse muendet in den Bogen (Ende auf der Mittellinie mitten im Bogen)
        var a = Grundlage();
        var mitte = Strassenkurve.Mittellinie("bogen", 90, 2)[6] + new Vector2(8, 8);
        Strasse(a, mitte.X + 3, mitte.Y - 3, mitte.X, mitte.Y);
        Rot(a, "Straße Nr. 4 kreuzt die Kurve (Straße Nr. 2) oder mündet in ihren Bogen.");
        // eine Strasse quer durch den Bogen
        var b = Grundlage();
        Strasse(b, 7, 12, 12, 7);
        Rot(b, "Straße Nr. 4 kreuzt die Kurve (Straße Nr. 2)");
        // gruen: eine Einmuendung auf dem geraden Stueck daneben
        var c = Grundlage();
        Strasse(c, 5, 8, 5, 13);
        Gruen(c);
    }

    [Fact]
    public void Regel3_BahnuebergangNurAufGeradenStuecken()
    {
        // eine Kurve kreuzt das Gleis
        var a = Grundlage();
        Linie(a, "bahn", 0, 9, 24, 9);
        Rot(a, "Die Kurve (Straße Nr. 2) kreuzt die Bahn.");
        // gerade Strasse kreuzt ein Gleis mit Werkzeug-Radius in seinem Bogen
        var b = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_kurven", 24, 16));
        Linie(b, "bahn", 0, 4, 12, 4, 24, 12);
        Py.Setzen(Letzte(b), "radien", new List<object?> { 3.0, 4.5, 3.0 });
        Strasse(b, 12, 0, 12, 16);
        Rot(b, "Straße Nr. 1 kreuzt die Bahn in einer Kurve.");
        // gruen: die gerade Strasse kreuzt auf dem geraden Stueck
        var c = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_kurven", 24, 16));
        Linie(c, "bahn", 0, 4, 12, 4, 24, 12);
        Py.Setzen(Letzte(c), "radien", new List<object?> { 3.0, 4.5, 3.0 });
        Strasse(c, 4, 0, 4, 16);
        Gruen(c);
    }

    [Fact]
    public void Regel4_BahnradiusUnterDrei()
    {
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_kurven", 24, 16));
        Linie(a, "bahn", 0, 4, 12, 4, 24, 12);
        Py.Setzen(Letzte(a), "radien", new List<object?> { 3.0, 2.0, 3.0 });
        Rot(a, "Die Bahn hat an Eckpunkt 2 den Radius 2, kleiner als 3");
        Py.Setzen(Letzte(a), "radien", new List<object?> { 3.0, 4.5, 3.0 });
        Gruen(a);
    }

    [Fact]
    public void Regel5_UferUeberlapptSich()
    {
        // Spitzkehre mit kurzen Stuecken: der gezeichnete Bogen wird enger als das halbe Ufer
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_kurven", 24, 16));
        Linie(a, "fluss", 4, 2, 10, 2, 4, 2.6);
        Rot(a, "Das Ufer von Fluss Nr. 1 überlappt sich");
        var b = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_kurven", 24, 16));
        Linie(b, "fluss", 4, 2, 10, 2, 14, 8);
        Py.Setzen(Letzte(b), "radien", new List<object?> { 3.0, 2.0, 3.0 });
        Gruen(b);
    }

    [Fact]
    public void Regel6_KurveUeberHausBaumLaterneKatalog()
    {
        var bogen = Strassenkurve.Mittellinie("bogen", 90, 2)[6] + new Vector2(8, 8);
        // Haus (ohne Ort) auf der Kurve
        var h = Grundlage();
        Aussen.Landschaft(h).Add(new PyObjekt { { "typ", "gebaeude" }, { "modell", "stellwerk" }, { "x", Math.Round(bogen.X - 0.5, 2) }, { "y", Math.Round(bogen.Y - 0.5, 2) } });
        Assert.Contains(Pruefen(h), f => f.Rot && f.Text.EndsWith("steht auf einer Straße oder ihrem Gehweg.", StringComparison.Ordinal));
        // Baum auf der Kurve, daneben gruen
        var b = Grundlage();
        Linie(b, "baum", bogen.X, bogen.Y);
        Rot(b, "Ein Baum steht auf der Kurve (Straße Nr. 2).");
        var b2 = Grundlage();
        Linie(b2, "baum", 16, 3);
        Gruen(b2);
        // Laterne auf der Fahrbahn der Kurve, am Rand gruen
        var l = Grundlage();
        Aussen.Laternen(l).Add(new List<object?> { (double)bogen.X, (double)bogen.Y, false });
        Rot(l, "Laterne Nr. 1 steht auf der Fahrbahn der Kurve (Straße Nr. 2).");
        var l2 = Grundlage();
        var aussen = Strassenkurve.Mittellinie("bogen", 90, 2)[6];
        var rand = new Vector2(8, 8) + aussen + (Vector2.Normalize(aussen - new Vector2(0, 2)) * Nachtlicht.Rand);
        Aussen.Laternen(l2).Add(new List<object?> { Math.Round(rand.X, 2), Math.Round(rand.Y, 2), false });
        Gruen(l2);
        // Katalog-Stueck auf der Fahrbahn der Kurve
        var k = Grundlage();
        Aussen.KatalogStueckeAnlegen(k).Add(new PyObjekt { { "id", "car_kit/sedan" }, { "x", Math.Round(bogen.X, 2) }, { "y", Math.Round(bogen.Y, 2) }, { "w", 0.3 }, { "t", 0.3 }, { "dreh", 0L } });
        Assert.Contains(Pruefen(k), f => f.Rot && f.Text.EndsWith("steht auf der Fahrbahn.", StringComparison.Ordinal));
        var k2 = Grundlage();
        Aussen.KatalogStueckeAnlegen(k2).Add(new PyObjekt { { "id", "car_kit/sedan" }, { "x", 16.0 }, { "y", 3.0 }, { "w", 0.3 }, { "t", 0.3 }, { "dreh", 0L } });
        Gruen(k2);
    }

    [Fact]
    public void Regel7_ZweiStrassenAufeinanderEntlangDesBogens()
    {
        var a = Grundlage();
        Kurve(a, "bogen", 90, 2, new Vector2(8.1f, 8));
        Rot(a, "Straße Nr. 2 und Straße Nr. 4 liegen auf");
        // gruen: eine zweite Kurve weit daneben, angesetzt an eine eigene gerade Strasse
        var b = Grundlage();
        Strasse(b, 14, 2, 16, 2);
        Kurve(b, "bogen", 90, 3, new Vector2(16, 2));
        Gruen(b);
    }

    [Fact]
    public void Regel8_BrueckeNurAnGeraderStrasse()
    {
        var a = Grundlage();
        Linie(a, "fluss", 9, 0, 9, 16);
        Rot(a, "Die Kurve (Straße Nr. 2) führt über den Fluss.");
        // gruen: der Fluss quert nur die gerade Strasse (Bruecke)
        var b = Grundlage();
        Linie(b, "fluss", 5, 0, 5, 16);
        Assert.DoesNotContain(Pruefen(b), f => f.Rot);
    }

    [Fact]
    public void Regel9_OffenesKurvenendeNebenStrasse_Gelb()
    {
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_kurven", 24, 16));
        Strasse(a, 2, 8, 8, 8);
        Kurve(a, "bogen", 90, 2, new Vector2(8.6f, 8));
        Strasse(a, 10.6, 10, 10.6, 15);
        var f = Pruefen(a);
        Assert.Contains(f, x => !x.Rot && x.Text == "Ein Ende der Kurve (Straße Nr. 2) schließt nicht an Straße Nr. 1 an (0,6 daneben).");
        Assert.DoesNotContain(f, x => x.Rot);
        Gruen(Grundlage());
    }

    [Fact]
    public void Fassung0_OhneKurvenfund()
    {
        // Stadt 1 hat keine Kurve und kein Feld radien: die Regeln melden nichts (Fund Laterne 20 ist der bekannte)
        var a = Aussen.Aus(D.Fassung);
        var f = StadtbauPruefung.Aussen(Aussen.Fassung(a, D.Fassung, D.Fassung.Innenraeume, 1, 0, "t", "", ""), D.Inhalt, D.Grafik);
        Assert.True(f.Count == 1 && f[0].Text.StartsWith("Laterne Nr. 20 ", StringComparison.Ordinal), Text(f));
    }
}
