using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.49.0 (Weisung P12-231 Antwort 4c, Sammelliste 314): Tuer und Fussweg auch an schraeg (15-Grad-Schritte) gedrehten
/// Haeusern. Gerechnet wird im Rahmen des Hauses (so baut das Spiel das Haus, bevor es dreht): die Tuer sitzt auf der gedrehten
/// Wand, der Weg geht gerade (senkrecht zur Wand) aus der Tuer, Knicke nur parallel oder rechtwinklig zu den Waenden mit runden
/// Ecken, und es gilt der kuerzeste Weg zur Strasse. Gegenproben: die alte Sperre (bis S 0.48.0) und der Weg achsrecht in der Stadt.
/// </summary>
public class SchraegeTuerTests
{
    private static Spieldaten D => Daten.Spiel;

    /// <summary>Das Spiel zeichnet die Tuer eines Orts (Szene aus dem Stand wie im Spiel).</summary>
    private static StadtTuer SpielTuer(PyObjekt a, string ort)
    {
        var f = Aussen.Fassung(a, D.Fassung, D.Fassung.Innenraeume, 1, 0, "test", "", "");
        return StadtSzene.Bauen(f.StadtFuer(f.MitKarte(D.Inhalt)), null, D.Grafik).Tueren.Single(t => t.Ort == ort);
    }

    private static List<Vector2> Weg(PyObjekt zugang) => AussenBearbeitung.Linie(zugang["weg"]);

    /// <summary>Jedes Stueck ausser dem letzten (quer auf die Strasse) liegt im Rahmen des Hauses parallel zu einer Achse.</summary>
    private static void KnickeImRahmen(List<Vector2> weg, Hausdrehung.Feinlage lage)
    {
        for (var i = 0; i + 2 < weg.Count; i++)
        {
            var d = lage.Zurueck(weg[i + 1]) - lage.Zurueck(weg[i]);
            Assert.True(MathF.Min(MathF.Abs(d.X), MathF.Abs(d.Y)) < 0.01f, $"Stueck {i} ist im Rahmen des Hauses schraeg: {d}");
        }
    }

    [Theory]
    [InlineData("elektromarkt", 30, true)]
    [InlineData("bank", 15, true)]
    [InlineData("zuhause", 105, false)]
    public void Ort_SchraegGedreht_TuerSetzen_AufDerGedrehtenWand_WegGeradeHinaus_SpielZeichntSieDort(string ort, int grad, bool gegenueber)
    {
        var p = Projekt.Neu("Schraeg " + ort, D, Daten.NeueAblage());
        var s = new AussenStelle(AussenArt.Haus, -1, 0, ort);
        p.AussenAendern("Drehen", a => AussenBearbeitung.Drehen(a, s, grad));
        var lage = AussenBearbeitung.HausLage(p.AussenStand, s);
        Assert.True(lage.Aktiv);
        Assert.Null(TuerSetzen.Nicht(p.AussenStand, s));
        var f = TuerFassade.Rechnen(p, p.AussenStand, s);
        Assert.NotNull(f);
        Assert.Equal(lage, f.Fein);
        var rotVorher = p.PruefenAussen().Where(x => x.Rot).Select(x => x.Text).ToHashSet();

        // Tipp vor die Mitte der Wand gegenueber der bisherigen Tuer bzw. (Zuhause: dahinter liegt der Paketplatz) derselben Wand
        var seite = AussenBearbeitung.HausTuerSeite(p.AussenStand, s) * (gegenueber ? -1 : 1);
        var wand = f.Waende.Select((w, i) => (w, i)).Where(x => Vector2.Dot(x.w.Aussen, seite) > 0.99f).MaxBy(x => x.w.Laenge).w;
        var platz = f.Finden(wand.Bei(wand.Laenge / 2) + (wand.Aussen * 0.3f), null, einrasten: true);
        Assert.True(Vector2.Dot(platz.Aussen, seite) > 0.999f, $"Tuer zeigt {platz.Aussen}, soll {seite}");
        string? hinweis = null;
        p.AussenAendern("Tuer", a => hinweis = TuerSetzen.SetzenAn(a, s, platz));
        Assert.NotEqual(Texte.TuerSchraegNicht, hinweis);
        Assert.NotEqual(Texte.TuerWegOhneStrasse, hinweis);

        var zugang = AussenBearbeitung.Zugang(p.AussenStand, ort)!;
        var weg = Weg(zugang);
        // gerade (senkrecht zur Wand) aus der Tuer
        Assert.True(Vector2.Dot(Vector2.Normalize(weg[1] - weg[0]), seite) > 0.999f, $"erstes Stueck {weg[1] - weg[0]} nicht senkrecht zur Wand {seite}");
        KnickeImRahmen(weg, lage);
        Assert.True(AussenBearbeitung.Strassen(p.AussenStand).Min(l => Kurve.Abstand(weg[^1], l)) < 0.01f, "Weg endet nicht auf einer Strasse");
        Assert.Equal(weg.Count, ((List<object?>)zugang["radien"]!).Count);
        // das Spiel zeichnet die Tuer genau dort, nach aussen in dieselbe Richtung
        var t = SpielTuer(p.AussenStand, ort);
        Assert.True(Vector2.Distance(t.WegAnfang, weg[0]) < 0.02f, $"Spiel: Tuer {t.WegAnfang}, Daten {weg[0]}");
        Assert.True(Vector2.Dot(t.Aussen, seite) > 0.99f);
        // kein neuer roter Fund (die Tuer liegt buendig, der Weg ist frei)
        var neu = p.PruefenAussen().Where(x => x.Rot && !rotVorher.Contains(x.Text)).Select(x => x.Text).ToList();
        Assert.True(neu.Count == 0, string.Join(" | ", neu));
    }

    [Fact]
    public void Gegenprobe_AlteSperre_KeineTuerAmSchraegenHaus()
    {
        var p = Projekt.Neu("Schraeg alt", D, Daten.NeueAblage());
        var s = new AussenStelle(AussenArt.Haus, -1, 0, "elektromarkt");
        p.AussenAendern("Drehen", a => AussenBearbeitung.Drehen(a, s, 30));
        TuerFassade.SchraegAltFuerTests = true;
        try
        {
            Assert.Equal(Texte.TuerSchraegNicht, TuerSetzen.Nicht(p.AussenStand, s));
            Assert.Null(TuerFassade.Rechnen(p, p.AussenStand, s));
        }
        finally
        {
            TuerFassade.SchraegAltFuerTests = false;
        }
        // ohne feine Drehung: Fassade wie bisher (Rahmen = Stadt)
        var gerade = new AussenStelle(AussenArt.Haus, -1, 0, "bank");
        Assert.False(TuerFassade.Rechnen(p, p.AussenStand, gerade)!.Fein.Aktiv);
    }

    // ---- Weg zur Strasse (Rechnung allein) -------------------------------------------------------------

    private static readonly Hausdrehung.Feinlage Lage30 = new(new Vector2(10, 10), 30);

    /// <summary>Haus 2 × 2 um (10, 10), Tuer in der Mitte der Suedwand (Rahmen des Hauses), Zugang in der Stadt.</summary>
    private static PyObjekt Zugang(Hausdrehung.Feinlage lage)
    {
        var tuer = lage.Punkt(new Vector2(10, 11));
        var vor = lage.Punkt(new Vector2(10, 11 + GebaeudeTuer.WegStart));
        return new PyObjekt
        {
            { "tuer", new List<object?> { (double)tuer.X, (double)tuer.Y } },
            { "weg", new List<object?> { new List<object?> { (double)tuer.X, (double)tuer.Y }, new List<object?> { (double)vor.X, (double)vor.Y } } },
        };
    }

    /// <summary>Strasse parallel zur Suedwand im Abstand 3 (Rahmen des Hauses y = 14), in der Stadt gedreht.</summary>
    private static List<List<Vector2>> StrasseParallel(Hausdrehung.Feinlage lage) => [[lage.Punkt(new Vector2(0, 14)), lage.Punkt(new Vector2(20, 14))]];

    [Fact]
    public void SchraegesHaus_SchraegeStrasse_GeraderWegSenkrecht()
    {
        var z = Zugang(Lage30);
        Assert.True(GebaeudeTuer.WegRechtwinklig(z, (9, 9, 2, 2), StrasseParallel(Lage30), [], Lage30));
        var weg = Weg(z);
        Assert.Equal(2, weg.Count);
        var soll = Lage30.Richtung(new Vector2(0, 1));
        Assert.True(Vector2.Dot(Vector2.Normalize(weg[1] - weg[0]), soll) > 0.9999f);
        Assert.True(MathF.Abs(Vector2.Distance(weg[0], weg[1]) - 3) < 0.01f, string.Join(" ", weg.Select(q => Lage30.Zurueck(q))));

        // Gegenprobe: achsrecht in der Stadt (um den Kasten des gedrehten Hauses) ist der Weg nicht gerade hinaus
        var alt = Zugang(Lage30);
        GebaeudeTuer.SchraegWeltAltFuerTests = true;
        bool erreicht;
        try
        {
            erreicht = GebaeudeTuer.WegRechtwinklig(alt, (9, 9, 2, 2), StrasseParallel(Lage30), [], Lage30);
        }
        finally
        {
            GebaeudeTuer.SchraegWeltAltFuerTests = false;
        }
        // achsrecht in der Stadt passt das erste (schraege) Stueck nie: der Weg erreicht keine Strasse
        Assert.False(erreicht, "Gegenprobe: auch achsrecht in der Stadt erreicht der Weg die Strasse");
    }

    [Fact]
    public void Hindernis_KuerzesterWegMitKnickImRahmenDesHauses_RundeEcken()
    {
        var z = Zugang(Lage30);
        // Laterne genau dort, wo der gerade Weg auf die Strasse trifft
        var laterne = Lage30.Punkt(new Vector2(10, 14));
        Assert.True(GebaeudeTuer.WegRechtwinklig(z, (9, 9, 2, 2), StrasseParallel(Lage30), [laterne], Lage30));
        var weg = Weg(z);
        Assert.True(weg.Count >= 3, "Weg ohne Knick trotz Laterne: " + string.Join(" ", weg.Select(q => Lage30.Zurueck(q))));
        // Ende ausserhalb von LaterneFrei (gespeichert auf 0,001 gerundet)
        Assert.True(Vector2.Distance(weg[^1], laterne) > GebaeudeTuer.LaterneFrei - 0.002f);
        Assert.True(Vector2.Dot(Vector2.Normalize(weg[1] - weg[0]), Lage30.Richtung(new Vector2(0, 1))) > 0.9999f, "nicht gerade aus der Tuer");
        KnickeImRahmen(weg, Lage30);
        var radien = ((List<object?>)z["radien"]!).Select(r => Py.F(r)).ToList();
        Assert.Equal(0, radien[0]);
        Assert.Equal(0, radien[^1]);
        Assert.All(radien.Skip(1).SkipLast(1), r => Assert.Equal(StadtTuerweg.Radius, r, 3));
        // kuerzester: nur so weit seitlich, wie die Laterne verlangt (Ende knapp ausserhalb von LaterneFrei)
        var l = 0f;
        for (var i = 0; i + 1 < weg.Count; i++)
        {
            l += Vector2.Distance(weg[i], weg[i + 1]);
        }
        Assert.True(l <= 3 + GebaeudeTuer.LaterneFrei + GebaeudeTuer.WegRaster + 0.01f, $"Weg {l} laenger als noetig");
    }

    /// <summary>Patch S 0.49.1 (Sammelliste 331): Strasse achsrecht in der Stadt (y = 15), das Haus um 30 Grad gedreht.</summary>
    private static List<List<Vector2>> StrasseInDerStadt() => [[new Vector2(0, 15), new Vector2(20, 15)]];

    private static float Laenge(List<Vector2> weg)
    {
        var l = 0f;
        for (var i = 0; i + 1 < weg.Count; i++)
        {
            l += Vector2.Distance(weg[i], weg[i + 1]);
        }
        return l;
    }

    [Fact]
    public void Patch331_SchraegesHaus_NurKurzGeradeAusDerTuer_DannKuerzesterWeg_GegenprobeLang()
    {
        var z = Zugang(Lage30);
        Assert.True(GebaeudeTuer.WegRechtwinklig(z, (9, 9, 2, 2), StrasseInDerStadt(), [], Lage30));
        var weg = Weg(z);
        // genau zwei Stuecke: kurz gerade aus der Tuer (WegStart, wie am geraden Haus), dann geradewegs (quer) auf die Strasse
        Assert.Equal(3, weg.Count);
        Assert.True(MathF.Abs(Vector2.Distance(weg[0], weg[1]) - GebaeudeTuer.WegStart) < 0.01f, string.Join(" ", weg));
        Assert.True(Vector2.Dot(Vector2.Normalize(weg[1] - weg[0]), Lage30.Richtung(new Vector2(0, 1))) > 0.9999f, "nicht gerade aus der Tuer");
        Assert.True(MathF.Abs(weg[2].Y - 15) < 0.01f && MathF.Abs(weg[2].X - weg[1].X) < 0.01f, "Ende nicht senkrecht auf der Strasse: " + weg[2]);
        var radien = ((List<object?>)z["radien"]!).Select(r => Py.F(r)).ToList();
        Assert.Equal([0, StadtTuerweg.Radius, 0], radien.Select(r => Math.Round(r, 3)));
        var kurz = Laenge(weg);

        // Gegenprobe S 0.49.0: lange rechtwinklig zur gedrehten Wand, erst vor der Strasse abbiegen (laenger, erstes Stueck lang)
        var alt = Zugang(Lage30);
        GebaeudeTuer.SchraegLangAltFuerTests = true;
        try
        {
            Assert.True(GebaeudeTuer.WegRechtwinklig(alt, (9, 9, 2, 2), StrasseInDerStadt(), [], Lage30));
        }
        finally
        {
            GebaeudeTuer.SchraegLangAltFuerTests = false;
        }
        var wegAlt = Weg(alt);
        Assert.True(Laenge(wegAlt) > kurz + 0.2f, $"Gegenprobe: alter Weg {Laenge(wegAlt)} nicht laenger als {kurz}");
        Assert.True(Vector2.Distance(wegAlt[0], wegAlt[1]) > 1f, "Gegenprobe: altes erstes Stueck kurz");
    }

    [Fact]
    public void Patch331_SchraegesHaus_TuerAnDerSchmalseite_WegStreiftDasHausNicht_GegenprobeOhneAbstand()
    {
        // Nachbau des Fotos P12-233: Tuer in der Mitte der Westwand, Strasse unterhalb; der Weg darf die Hausecke nicht streifen
        PyObjekt Westtuer()
        {
            var tuer = Lage30.Punkt(new Vector2(9, 10));
            var vor = Lage30.Punkt(new Vector2(9 - GebaeudeTuer.WegStart, 10));
            return new PyObjekt
            {
                { "tuer", new List<object?> { (double)tuer.X, (double)tuer.Y } },
                { "weg", new List<object?> { new List<object?> { (double)tuer.X, (double)tuer.Y }, new List<object?> { (double)vor.X, (double)vor.Y } } },
            };
        }
        float Abstand(List<Vector2> weg)
        {
            // kleinster Abstand der Wegmitte (ohne das erste Stueck aus der Tuer) zur Flaeche des Hauses im Rahmen des Hauses
            var am = float.MaxValue;
            for (var i = 1; i + 1 < weg.Count; i++)
            {
                var (a, b) = (Lage30.Zurueck(weg[i]), Lage30.Zurueck(weg[i + 1]));
                for (var k = 0; k <= 400; k++)
                {
                    var q = Vector2.Lerp(a, b, k / 400f);
                    var dx = MathF.Max(MathF.Max(9 - q.X, q.X - 11), 0);
                    var dy = MathF.Max(MathF.Max(9 - q.Y, q.Y - 11), 0);
                    am = MathF.Min(am, MathF.Sqrt((dx * dx) + (dy * dy)));
                }
            }
            return am;
        }
        var z = Westtuer();
        Assert.True(GebaeudeTuer.WegRechtwinklig(z, (9, 9, 2, 2), StrasseInDerStadt(), [], Lage30));
        var weg = Weg(z);
        Assert.True(Abstand(weg) >= (StadtSzene.WegBreite / 2) - 0.001f, $"Weg streift das Haus: {Abstand(weg)} ({string.Join(" ", weg)})");
        Assert.True(MathF.Abs(Vector2.Distance(weg[0], weg[1]) - GebaeudeTuer.WegStart) < 0.01f, "erstes Stueck nicht kurz gerade");

        // Gegenprobe: nur die Mittellinie gegen das Haus geprueft (erster Stand dieses Patches) streift die Ecke
        var ohne = Westtuer();
        GebaeudeTuer.KurzOhneAbstandFuerTests = true;
        try
        {
            Assert.True(GebaeudeTuer.WegRechtwinklig(ohne, (9, 9, 2, 2), StrasseInDerStadt(), [], Lage30));
        }
        finally
        {
            GebaeudeTuer.KurzOhneAbstandFuerTests = false;
        }
        Assert.True(Abstand(Weg(ohne)) < (StadtSzene.WegBreite / 2) - 0.01f, $"Gegenprobe: Weg ohne Abstand streift nicht ({Abstand(Weg(ohne))})");
    }

    [Fact]
    public void Patch331_SchraegesHaus_StrasseHinterDemHaus_UmDieEckeDannGerade()
    {
        // Strasse hinter dem Haus (Tuer zeigt weg): an der Wand entlang um die Ecke (parallel zu den Waenden), dann gerade zur Strasse
        var z = Zugang(Lage30);
        Assert.True(GebaeudeTuer.WegRechtwinklig(z, (9, 9, 2, 2), [[new Vector2(0, 5), new Vector2(20, 5)]], [], Lage30));
        var weg = Weg(z);
        Assert.True(weg.Count >= 4, string.Join(" ", weg));
        KnickeImRahmen(weg, Lage30);
        Assert.True(MathF.Abs(weg[^1].Y - 5) < 0.01f);
        Assert.True(Laenge(weg) <= GebaeudeTuer.WegHoechstens);
    }

    [Fact]
    public void GeradesHaus_Unveraendert_WieBisher()
    {
        // ohne feine Drehung genau die bisherige Rechnung (Rahmen = Stadt)
        var lage = default(Hausdrehung.Feinlage);
        var neu = Zugang(lage);
        var alt = Zugang(lage);
        Assert.True(GebaeudeTuer.WegRechtwinklig(neu, (9, 9, 2, 2), StrasseParallel(lage), [new Vector2(10, 14)], lage));
        Assert.True(GebaeudeTuer.WegRechtwinklig(alt, (9, 9, 2, 2), StrasseParallel(lage), [new Vector2(10, 14)]));
        Assert.Equal(PythonJson.Text(alt), PythonJson.Text(neu));
    }

    [Fact]
    public void GebaeudeOhneOrt_SchraegGedreht_TuerDazu()
    {
        var p = Projekt.Neu("Schraeg dazu", D, Daten.NeueAblage());
        // ein Gebaeude ohne Ort mit Tuer (Stellwerk): Tuer weg, schraeg drehen, „Tür dazu“
        var s = Enumerable.Range(0, Aussen.Landschaft(p.AussenStand).Count).Select(i => new AussenStelle(AussenArt.Gebaeude, i))
            .First(x => Py.Gleich(Objekt(Aussen.Landschaft(p.AussenStand)[x.Nummer])["typ"], "gebaeude") && GebaeudeTuer.DazuNicht(p.AussenStand, x) == Texte.TuerSchonDa);
        p.AussenAendern("Tuer weg", a => GebaeudeTuer.Entfernen(a, s));
        p.AussenAendern("Drehen", a => AussenBearbeitung.Drehen(a, s, 45));
        Assert.Null(GebaeudeTuer.DazuNicht(p.AussenStand, s));
        var platz = GebaeudeTuer.DazuPlatz(p, p.AussenStand, s, einrasten: true);
        Assert.NotNull(platz);
        p.AussenAendern("Tuer dazu", a => GebaeudeTuer.DazuAn(a, s, true, platz));
        var lage = AussenBearbeitung.HausLage(p.AussenStand, s);
        var z = Blueprint.ZierwegVon(p.AussenStand, s)!;
        var weg = Weg(z);
        var r = lage.RichtungZurueck(Vector2.Normalize(weg[1] - weg[0]));
        Assert.True(MathF.Min(MathF.Abs(r.X), MathF.Abs(r.Y)) < 0.001f, $"erstes Stueck {r} im Rahmen des Hauses schraeg");
        KnickeImRahmen(weg, lage);
        // die Tuer liegt auf der gedrehten Wand (im Rahmen des Hauses auf dem Rand der Flaeche)
        var (x, y, w, h) = AussenBearbeitung.HausViertel(p.AussenStand, s);
        var tl = lage.Zurueck(weg[0]);
        var rand = MathF.Min(MathF.Min(MathF.Abs(tl.X - (float)x), MathF.Abs(tl.X - (float)(x + w))), MathF.Min(MathF.Abs(tl.Y - (float)y), MathF.Abs(tl.Y - (float)(y + h))));
        Assert.True(rand < 0.6f, $"Tuer {tl} nicht an der Wand ({x}, {y}, {w}, {h})");
    }
}
