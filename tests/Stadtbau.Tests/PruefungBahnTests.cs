using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle K2 (Weisung P12-72): die Pruefregeln 1 bis 5 fuer die Bahn (<c>StadtbauPruefung.Bahn.cs</c>), je Regel ein roter und
/// ein gruener Fall. Grundlage: leere Stadt 32 × 20 mit einer Bahn aus dem Werkzeug (Feld <c>radien</c>) von (0; 4) ueber
/// (14; 4) nach (24; 14) und weiter bis (24; 20) und einer geraden Strasse, die das Gleis bei x = 6 rechtwinklig quert; sie
/// hat keinen Fund.
/// </summary>
public class PruefungBahnTests
{
    private static Spieldaten D => Daten.Spiel;

    private static List<object?> P(double x, double y) => [x, y];

    private static PyObjekt Leer() => Aussen.Aus(Aussen.Leer(D.Fassung, "neu_bahn", 32, 20));

    private static PyObjekt Grundlage()
    {
        var a = Leer();
        Bahn(a, [3, 4.5, 3, 3], 0, 4, 14, 4, 24, 14, 24, 20);
        Strasse(a, 6, 0, 6, 12);
        return a;
    }

    private static PyObjekt Bahn(PyObjekt a, double[]? radien, params double[] xy)
    {
        var punkte = new List<object?>();
        for (var i = 0; i + 1 < xy.Length; i += 2)
        {
            punkte.Add(P(xy[i], xy[i + 1]));
        }
        var l = new PyObjekt { { "typ", "bahn" }, { "punkte", punkte } };
        if (radien is not null)
        {
            l.Add("radien", radien.Select(r => (object?)r).ToList());
        }
        Aussen.Landschaft(a).Add(l);
        return l;
    }

    private static void Strasse(PyObjekt a, double x1, double y1, double x2, double y2) =>
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(x1, y1), P(x2, y2) } } });

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

    private static void OhneFund(PyObjekt a, string anfang)
    {
        var f = Pruefen(a);
        Assert.False(f.Any(x => x.Text.StartsWith(anfang, StringComparison.Ordinal)), "unerwartet „" + anfang + "“: " + Text(f));
    }

    [Fact]
    public void Grundlage_OhneFund() => Gruen(Grundlage());

    [Fact]
    public void Fassung0_TalbahnOhneNeuenFund()
    {
        // das Gleis der Stadt 1 hat kein Feld radien und keinen Fund der Bahn-Regeln
        Assert.DoesNotContain(Pruefen(Aussen.Aus(D.Fassung)), f => f.Text.Contains("Bahn Nr.", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel1_DatenDerBahn()
    {
        var a = Leer();
        Bahn(a, null, 4, 4);
        Rot(a, "Bahn Nr. 1 hat weniger als zwei Punkte.");
        var b = Leer();
        Bahn(b, [3, 3], 0, 4, 33, 4);
        Rot(b, "Bahn Nr. 1 reicht über den Rand der Karte (33; 4).");
        var c = Leer();
        Bahn(c, [3, 3], 0, 4, 32, 4);
        Gruen(c);
    }

    [Fact]
    public void Regel2_GleiseKreuzenSich_KeineWeiche()
    {
        var a = Grundlage();
        Bahn(a, [3, 3], 10, 0, 10, 12);
        Rot(a, "Bahn Nr. 1 und Bahn Nr. 2 kreuzen sich oder liegen zu nah beieinander");
        // parallel naeher als der doppelte Lichtraum
        var b = Grundlage();
        Bahn(b, [3, 3], 0, 4.8, 10, 4.8);
        Rot(b, "Bahn Nr. 1 und Bahn Nr. 2 kreuzen sich oder liegen zu nah beieinander");
        // gruen: parallel mit genug Abstand, und Ende an Ende in gleicher Richtung
        var c = Grundlage();
        Bahn(c, [3, 3], 0, 5.2, 4, 5.2);
        Gruen(c);
        var d = Leer();
        Bahn(d, [3, 3], 0, 4, 10, 4);
        Bahn(d, [3, 3], 10, 4, 20, 4);
        Gruen(d);
        // Ende an Ende mit Knick
        var e = Leer();
        Bahn(e, [3, 3], 0, 4, 10, 4);
        Bahn(e, [3, 3], 10, 4, 18, 10);
        Rot(e, "Bahn Nr. 1 und Bahn Nr. 2 stoßen mit einem Knick von 37° aneinander.");
    }

    [Fact]
    public void Regel2_GleisKreuztSichSelbst_RingErlaubt()
    {
        var a = Leer();
        Bahn(a, [3, 3, 3, 3, 3], 2, 10, 28, 10, 20, 2, 12, 16, 12, 18);
        Rot(a, "Bahn Nr. 1 kreuzt sich selbst");
        // geschlossener Ring (erster = letzter Punkt): erlaubt
        var b = Leer();
        Bahn(b, [3, 3, 3, 3, 3], 4, 4, 28, 4, 28, 16, 4, 16, 4, 4);
        OhneFund(b, "Bahn Nr. 1 kreuzt sich selbst");
    }

    [Fact]
    public void Regel3_BogenZuEngGezeichnet()
    {
        // die Stuecke an der Ecke (14; 4) sind fuer Radius 4,5 bei 90 Grad zu kurz
        var a = Leer();
        Bahn(a, [3, 4.5, 3], 10, 4, 14, 4, 14, 8);
        Rot(a, "Der Bogen von Bahn Nr. 1 an der Ecke (14; 4) ist enger als 3 gezeichnet");
        var b = Leer();
        Bahn(b, [3, 4.5, 3], 6, 4, 14, 4, 14, 12);
        Gruen(b);
        // das Gleis der Stadt 1 (ohne radien) prueft die Regel nicht
        var c = Leer();
        Bahn(c, null, 10, 4, 14, 4, 14, 8);
        OhneFund(c, "Der Bogen von Bahn Nr. 1");
    }

    [Fact]
    public void Regel4_DurchWasser()
    {
        var a = Grundlage();
        Liste(Aussen.WasserAnlegen(a)["teiche"]).Add(new PyObjekt { { "punkte", new List<object?> { P(8, 3), P(11, 3), P(11, 6), P(8, 6) } } });
        Rot(a, "Bahn Nr. 1 führt durch Wasser.");
        var b = Grundlage();
        Aussen.Landschaft(b).Add(new PyObjekt { { "typ", "fluss" }, { "punkte", new List<object?> { P(10, 0), P(10, 20) } } });
        Rot(b, "Bahn Nr. 1 führt durch Wasser.");
        var c = Grundlage();
        Liste(Aussen.WasserAnlegen(c)["teiche"]).Add(new PyObjekt { { "punkte", new List<object?> { P(8, 7), P(11, 7), P(11, 10), P(8, 10) } } });
        OhneFund(c, "Bahn Nr. 1 führt durch Wasser.");
    }

    [Fact]
    public void Regel5_StrasseQuertZuFlach()
    {
        var a = Leer();
        Bahn(a, [3, 3], 0, 8, 32, 8);
        Strasse(a, 4, 6, 14, 10);
        Rot(a, "Straße Nr. 1 quert Bahn Nr. 1 zu flach (22°, mindestens 45°).");
        var b = Leer();
        Bahn(b, [3, 3], 0, 8, 32, 8);
        Strasse(b, 8, 4, 12, 12);
        OhneFund(b, "Straße Nr. 1 quert Bahn Nr. 1 zu flach");
        // das Gleis der Stadt 1 (ohne radien) prueft die Regel nicht
        var c = Leer();
        Bahn(c, null, 0, 8, 32, 8);
        Strasse(c, 4, 6, 14, 10);
        OhneFund(c, "Straße Nr. 1 quert Bahn Nr. 1 zu flach");
    }
}
