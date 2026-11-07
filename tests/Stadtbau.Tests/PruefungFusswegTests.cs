using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle Z (Weisung P12-71, Nico P12-67 Frage 4 = a): die Pruefregeln 1 bis 7 fuer eigene Fusswege
/// (<see cref="StadtbauPruefung"/>, Datei <c>StadtbauPruefung.Fusswege.cs</c>), je Regel ein roter und ein gruener Fall,
/// dazu Regel 1 der Kurven fuer gezogene Boegen. Grundlage: leere Stadt 24 × 16 mit gerader Strasse (2; 4)–(22; 4) und einem
/// Fussweg von der Fahrbahnkante (6; 4,5) nach (6; 10) und (14; 10); sie hat keinen Fund.
/// </summary>
public class PruefungFusswegTests
{
    private static Spieldaten D => Daten.Spiel;

    private static List<object?> P(double x, double y) => [x, y];

    private static PyObjekt Grundlage(params double[] weg)
    {
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_fusswege", 24, 16));
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(2, 4), P(22, 4) } } });
        Weg(a, weg.Length > 0 ? weg : [6, 4.5, 6, 10, 14, 10]);
        return a;
    }

    private static PyObjekt Weg(PyObjekt a, double[] xy, List<object?>? radien = null)
    {
        var punkte = new List<object?>();
        for (var i = 0; i + 1 < xy.Length; i += 2)
        {
            punkte.Add(P(xy[i], xy[i + 1]));
        }
        var w = new PyObjekt { { "punkte", punkte } };
        if (radien is not null)
        {
            w.Add("radien", radien);
        }
        Aussen.FusswegeAnlegen(a).Add(w);
        return w;
    }

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

    /// <summary>Gruen fuer diese Regel (ein Fund einer anderen Regel stoert hier nicht).</summary>
    private static void OhneFund(PyObjekt a, string anfang)
    {
        var f = Pruefen(a);
        Assert.False(f.Any(x => x.Text.StartsWith(anfang, StringComparison.Ordinal)), "unerwartet „" + anfang + "“: " + Text(f));
    }

    [Fact]
    public void Grundlage_OhneFund() => Gruen(Grundlage());

    [Fact]
    public void Fassung0_OhneFusswege_KeinFund()
    {
        Assert.False(Py.Hat(D.Fassung.Stadtdaten, Fusswege.Abschnitt));
        Assert.Empty(Fusswege.Alle(D.Fassung.Stadtdaten));
        Assert.DoesNotContain(Pruefen(Aussen.Aus(D.Fassung)), f => f.Text.Contains("Fußweg", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel1_FalschGespeichert()
    {
        var a = Grundlage();
        Weg(a, [8, 12]);
        Rot(a, "Fußweg Nr. 2 ist falsch gespeichert");
        var b = Grundlage();
        Weg(b, [16, 6, 16, 9, 20, 9], [1.0, 1.5, 1.0]);
        Rot(b, "Fußweg Nr. 2 ist falsch gespeichert");
        var c = Grundlage();
        Weg(c, [16, 6, 16, 9, 20, 9], [1.0, 2.0]);
        Rot(c, "Fußweg Nr. 2 ist falsch gespeichert");
        var d = Grundlage();
        Weg(d, [16, 6, 16, 9, 20, 9], [1.0, 2.0, 1.0]);
        Gruen(d);
    }

    [Fact]
    public void Regel2_NichtAufDerKarte()
    {
        Rot(Grundlage(6, 4.5, 6, 10, 26, 10), "Fußweg Nr. 1 liegt nicht ganz auf der Karte.");
        Gruen(Grundlage(6, 4.5, 6, 10, 24, 10));
    }

    [Fact]
    public void Regel3_DurchEinHaus()
    {
        var a = Grundlage();
        var s = AussenBearbeitung.Setzen(a, AussenArt.Gebaeude, 18, 13, raster: false, modell: "filiale")!;
        var m = AussenBearbeitung.Alle(a).First(z => z.Stelle == s);
        Weg(a, [m.X - 3, m.Y, m.X + 3, m.Y]);
        Rot(a, "Fußweg Nr. 2 führt durch das Haus");
        var b = Grundlage();
        AussenBearbeitung.Setzen(b, AussenArt.Gebaeude, 18, 13, raster: false, modell: "filiale");
        Weg(b, [2, 14, 8, 14]);
        OhneFund(b, "Fußweg Nr. 2 führt durch das Haus");
    }

    [Fact]
    public void Regel4_DurchWasser()
    {
        var a = Grundlage();
        Liste(Aussen.WasserAnlegen(a)["teiche"]).Add(new PyObjekt { { "punkte", new List<object?> { P(16, 7), P(19, 7), P(19, 9), P(16, 9) } } });
        Weg(a, [15, 8, 20, 8]);
        Rot(a, "Fußweg Nr. 2 führt durch Wasser.");
        var b = Grundlage();
        Liste(Aussen.WasserAnlegen(b)["teiche"]).Add(new PyObjekt { { "punkte", new List<object?> { P(16, 7), P(19, 7), P(19, 9), P(16, 9) } } });
        Weg(b, [15, 10, 20, 10]);
        Gruen(b);
        // Fluss quer
        var c = Grundlage();
        Aussen.Landschaft(c).Add(new PyObjekt { { "typ", "fluss" }, { "punkte", new List<object?> { P(18, 6), P(18, 16) } } });
        Weg(c, [16, 12, 20, 12]);
        Rot(c, "Fußweg Nr. 2 führt durch Wasser.");
    }

    [Fact]
    public void Regel5_UeberDasGleis()
    {
        var a = Aussen.Aus(D.Fassung);
        var f = Aussen.Fassung(a, D.Fassung, D.Fassung.Innenraeume, 1, 0, "test", "", "");
        var gleis = StadtSzene.Bauen(f.StadtFuer(f.MitKarte(D.Inhalt)), null, D.Grafik).Gleise[0];
        var i = gleis.Count / 2;
        var m = gleis[i];
        var u = Vector2.Normalize(gleis[i + 1] - gleis[i - 1]);
        var n = new Vector2(-u.Y, u.X);
        Weg(a, [m.X - (2 * n.X), m.Y - (2 * n.Y), m.X + (2 * n.X), m.Y + (2 * n.Y)]);
        Rot(a, "Fußweg Nr. 1 kreuzt das Gleis.");
        var b = Aussen.Aus(D.Fassung);
        Weg(b, [m.X + (1.2 * n.X), m.Y + (1.2 * n.Y), m.X + (2 * n.X), m.Y + (2 * n.Y)]);
        OhneFund(b, "Fußweg Nr. 1 kreuzt das Gleis.");
    }

    [Fact]
    public void Regel6_BaumLaterneKatalogAufDemWeg()
    {
        var a = Grundlage();
        AussenBearbeitung.Setzen(a, AussenArt.Baum, 10, 10);
        Rot(a, "Ein Baum steht auf Fußweg Nr. 1.");
        var b = Grundlage();
        AussenBearbeitung.Setzen(b, AussenArt.Baum, 10, 11.5);
        Gruen(b);

        var c = Grundlage(8, 4.6, 14, 4.6);
        AussenBearbeitung.Setzen(c, AussenArt.Laterne, 10, 4.6);
        Rot(c, "Eine Laterne steht auf Fußweg Nr. 1.");
        var d = Grundlage(8, 4.6, 14, 4.6);
        AussenBearbeitung.Setzen(d, AussenArt.Laterne, 10, 3.4);
        OhneFund(d, "Eine Laterne steht auf Fußweg Nr. 1.");

        var stueck = Katalog.Aktiv!.Eintraege.First(k => k.Art == KatalogArt.Modell && k.Aussen[1] <= 0.6 && k.Aussen[0] <= 0.6);
        var e = Grundlage();
        AussenBearbeitung.Setzen(e, AussenArt.Katalog, 10, 10, false, stueck.Id);
        Rot(e, "„");
        Assert.Contains(Pruefen(e), x => x.Rot && x.Text.EndsWith("steht auf Fußweg Nr. 1.", StringComparison.Ordinal));
        var g = Grundlage();
        AussenBearbeitung.Setzen(g, AussenArt.Katalog, 10, 12.5, false, stueck.Id);
        Assert.DoesNotContain(Pruefen(g), x => x.Text.EndsWith("steht auf Fußweg Nr. 1.", StringComparison.Ordinal));
        // Effekte versperren nichts
        var effekt = Katalog.Aktiv!.Eintraege.First(k => k.Art == KatalogArt.Effekt);
        var h = Grundlage();
        AussenBearbeitung.Setzen(h, AussenArt.Katalog, 10, 10, false, effekt.Id);
        Assert.DoesNotContain(Pruefen(h), x => x.Text.EndsWith("steht auf Fußweg Nr. 1.", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel7_ZuEngeEcke()
    {
        Rot(Grundlage(6, 4.5, 6, 10, 6.3, 10, 6.3, 10.3, 14, 10.3), "Fußweg Nr. 1 ist an der Ecke");
        Gruen(Grundlage(6, 4.5, 6, 10, 8, 10, 8, 12, 14, 12));
    }

    [Fact]
    public void Spiel_FusswegIstBegehbarUndGezeichnet()
    {
        var a = Grundlage();
        var f = Aussen.Fassung(a, D.Fassung, D.Fassung.Innenraeume, 1, 0, "test", "", "");
        var mit = StadtSzene.Bauen(f.StadtFuer(f.MitKarte(D.Inhalt)), null, D.Grafik);
        Fusswege.Alle(Objekt(a["stadtdaten"]))[0].Glatt.ForEach(q => Assert.True(mit.Laufraster.KannStehen(q.X, q.Y), $"nicht begehbar bei {q}"));
        // Gegenprobe ohne Fussweg: weniger Dreiecke im Stadtnetz
        var b = Grundlage();
        Py.Setzen(Objekt(b["stadtdaten"]), Fusswege.Abschnitt, new List<object?>());
        var g = Aussen.Fassung(b, D.Fassung, D.Fassung.Innenraeume, 1, 0, "test", "", "");
        var ohne = StadtSzene.Bauen(g.StadtFuer(g.MitKarte(D.Inhalt)), null, D.Grafik);
        Assert.True(mit.NetzBauen().AnzahlEcken > ohne.NetzBauen().AnzahlEcken, "Fussweg nicht gezeichnet");
    }
}
