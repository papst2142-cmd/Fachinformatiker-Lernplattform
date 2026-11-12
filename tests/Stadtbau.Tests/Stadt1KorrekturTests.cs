using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Patch S 0.46.1 (Weisung P12-225, Nicos Stadt 1 aus S 0.46.0): (a) ein Ort mit dem alten Haltepunkt (Birkenhain) bekommt
/// das Aussehen „Haltepunkt mit freiem Bahnsteig“, ans Gleis gelegt wie ein Bahnhof; (b) „Eingang automatisch setzen“ an
/// einem Spielort (Betriebswerkstatt) legt die Tuer an einen Windfang statt ins Rolltor. Je Fall eine Gegenprobe; Innenraum
/// und Spielgeschichte bleiben, das Spiel laedt und die Figur kommt durch die neue Tuer.
/// </summary>
public class Stadt1KorrekturTests
{
    private static Spieldaten D => Daten.Spiel;

    private static readonly Lazy<byte[]> Datei = new(() =>
        File.ReadAllBytes(Path.Combine(Daten.Wurzel, "tests", "Stadtbau.Tests", "Testdaten", "nico", "stadt1_S0.46.0.fistadt")));

    private static Projekt Nico() => Projekt.AusStadtDatei(Datei.Value, D, Daten.NeueAblage(), "Stadt aus Datei");

    private static readonly AussenStelle Birkenhain = new(AussenArt.Haus, -1, 0, "birkenhain");
    private static readonly AussenStelle Werkstatt = new(AussenArt.Haus, -1, 0, "betriebswerkstatt");

    private static List<string> RoteFunde(Projekt p) => [.. p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text)];

    private static string? Haltepunkt(Projekt p)
    {
        var platz = HaltepunktTausch.Platz(p, (PyObjekt)Py.Kopie(p.AussenStand)!, "birkenhain");
        string? h = null;
        p.AussenAendern(Texte.HaltepunktNeu, a => h = HaltepunktTausch.Tauschen(a, "birkenhain", platz));
        return h;
    }

    private static string? Eingang(Projekt p)
    {
        var platz = GebaeudeTuer.EingangPlatz(p, (PyObjekt)Py.Kopie(p.AussenStand)!, Werkstatt);
        string? h = null;
        p.AussenAendern(Texte.EingangAutomatisch, a => h = GebaeudeTuer.EingangAn(a, Werkstatt, platz));
        return h;
    }

    private static PyObjekt Zugang(Projekt p, string ort) => (PyObjekt)Get((PyObjekt)Get(Aussen.Stadtdaten(p.AussenStand), "zugaenge_neu")!, ort)!;

    private static List<Vector2> Weg(PyObjekt z) => [.. ((List<object?>)z["weg"]!).OfType<List<object?>>().Select(q => new Vector2((float)Py.F(q[0]), (float)Py.F(q[1])))];

    private static StadtSzene Szene(Projekt p) => Ansicht3D.Bauen(p.Stand(), D, Ansicht3D.Einstellung.Standard).Szene;

    /// <summary>Innenraeume, Aufgaben und Personen der Stadt (alles, was die Spielgeschichte betrifft).</summary>
    private static string Geschichte(Projekt p)
    {
        var st = p.Stand();
        var sd = Aussen.Stadtdaten(p.AussenStand);
        return PythonJson.Text(st.Innenraeume, sortiert: true) + PythonJson.Text(Get(sd, "quests"), sortiert: true) + PythonJson.Text(Get(sd, "questgeber"), sortiert: true)
            + string.Join(",", ((List<object?>)Aussen.Karte(p.AussenStand)["orte"]!).OfType<PyObjekt>().Select(o => Py.Str(o["id"]) + "|" + Py.Str(o["name"]) + "|" + Py.Str(o["ansicht"])));
    }

    /// <summary>Die Figur kommt vom Ende des Wegs an der Strasse bis vor die Tuer des Orts im Spiel.</summary>
    private static void FigurKommtHinein(Projekt p, string ort)
    {
        var szene = Szene(p);
        var tuer = Assert.Single(szene.Tueren, t => t.Ort == ort);
        var o = szene.Ort(ort)!;
        Assert.True(Vector2.Distance(new Vector2(o.TuerX, o.TuerZ), tuer.WegAnfang) <= 0.6f, $"Ort betritt man nicht durch die Tuer: {o.TuerX} {o.TuerZ}, Tuer {tuer.Mitte}");
        var weg = Weg(Zugang(p, ort));
        var vor = tuer.WegAnfang + (tuer.Aussen * 0.1f);
        Assert.True(szene.Laufraster.KannStehen(vor.X, vor.Y), "vor der Tuer kann die Figur nicht stehen");
        var pfad = szene.Laufraster.Weg((weg[^1].X, weg[^1].Y), (vor.X, vor.Y));
        Assert.NotEmpty(pfad);
        Assert.True(Vector2.Distance(new Vector2((float)pfad[^1].X, (float)pfad[^1].Y), vor) <= 0.3f, "Weg endet nicht vor der Tuer");
    }

    [Fact]
    public void Vorher_HaltepunktAlt_WerkstattImRolltor()
    {
        var p = Nico();
        Assert.True(HaltepunktTausch.KnopfZeigen(p.AussenStand, "birkenhain"));
        Assert.Equal(HaltepunktTausch.Alt, Py.Str(AussenBearbeitung.Ort(p.AussenStand, "birkenhain")!["modell"]));
        Assert.Equal(0L, Get((PyObjekt)Get((PyObjekt)Get(Aussen.Stadtdaten(p.AussenStand), "darstellung")!, "modell_tuer")!, "betriebswerkstatt"));
        Assert.Null(GebaeudeTuer.EingangNicht(p.AussenStand, Werkstatt));
        // nur Orte bekommen den Knopf, ein anderer Ort ohne alten Haltepunkt nicht
        Assert.False(HaltepunktTausch.KnopfZeigen(p.AussenStand, "talheim"));
    }

    [Fact]
    public void Haltepunkt_NeuesAussehen_AmSchraegenGleis_WegZurStrasse_OhneRotenFund_GegenprobeWegAlt()
    {
        var p = Nico();
        var geschichte = Geschichte(p);
        var schritte = p.Verlauf.Count;
        Assert.Null(Haltepunkt(p));
        Assert.Equal(schritte + 1, p.Verlauf.Count);
        var a = p.AussenStand;
        var o = AussenBearbeitung.Ort(a, "birkenhain")!;
        Assert.Equal(Zusatzmodelle.HaltepunktFrei, Py.Str(o["modell"]));
        Assert.Equal("Birkenhain", Py.Str(o["name"]));
        Assert.False(HaltepunktTausch.KnopfZeigen(a, "birkenhain"));
        // Bahnsteigkante parallel zum naechsten Gleis im Abstand BahnsteigKante von der Gleismitte
        var teile = Get(AussenBearbeitung.Modelle(a), Zusatzmodelle.HaltepunktFrei);
        var lage = AussenBearbeitung.HausLage(a, Birkenhain);
        var r = Bahnsteig.Rechtecke(teile, Py.F(o["x"]), Py.F(o["y"]), AussenBearbeitung.HausDreh(a, Birkenhain)).Single();
        var ecken = lage.Ecken(r.Min.X, r.Min.Y, r.Max.X, r.Max.Y);
        var mitte = ecken.Aggregate(Vector2.Zero, (s, e) => s + e) / 4;
        var gleis = Aussen.Landschaft(a).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "bahn"))
            .SelectMany(t =>
            {
                var q = ((List<object?>)t["punkte"]!).OfType<List<object?>>().Select(x => new Vector2((float)Py.F(x[0]), (float)Py.F(x[1]))).ToList();
                return Enumerable.Range(0, q.Count - 1).Select(i => (A: q[i], B: q[i + 1]));
            })
            .MinBy(g => Kurve.Abstand(mitte, [g.A, g.B]));
        var u = Vector2.Normalize(gleis.B - gleis.A);
        var n = new Vector2(-u.Y, u.X);
        var abstaende = ecken.Select(e => MathF.Abs(Vector2.Dot(e - gleis.A, n))).OrderBy(d => d).ToList();
        Assert.Equal(abstaende[0], abstaende[1], 2);
        Assert.Equal(AussenBearbeitung.BahnsteigKante, abstaende[0], 0.012f);
        // der Weg endet auf einer Strasse, kein roter Fund, Spielgeschichte unveraendert
        var weg = Weg(Zugang(p, "birkenhain"));
        Assert.Contains(AussenBearbeitung.Strassen(a), l => l.Count >= 2 && Kurve.Abstand(weg[^1], l) <= 0.02f);
        Assert.Empty(RoteFunde(p));
        Assert.Equal(geschichte, Geschichte(p));
        // im Spiel: blaue Zugzielanzeige am Haus, die Figur kommt durch die neue Tuer
        var sz = Szene(p);
        var netz = sz.HausNetz(sz.Haeuser.Single(h => h.Id.StartsWith("birkenhain", StringComparison.Ordinal)));
        Assert.Contains(Enumerable.Range(0, netz.AnzahlEcken), e => Vector3.Distance(netz.FarbeAn(e), StadtSzene.AnzeigeBlau) < 0.02f);
        FigurKommtHinein(p, "birkenhain");
        Assert.Null(BelastungsprobenTests.SpielLaedt(p.Stand()));
        // Gegenprobe: wie im ersten Entwurf Tuer vor dem Ruecken ans Gleis, Weg nicht neu: er erreicht die Strasse nicht
        HaltepunktTausch.EndlageAltFuerTests = true;
        try
        {
            var q = Nico();
            Haltepunkt(q);
            Assert.Contains(RoteFunde(q), f => f.Contains("Birkenhain", StringComparison.Ordinal) && f.Contains("Straße", StringComparison.Ordinal));
        }
        finally
        {
            HaltepunktTausch.EndlageAltFuerTests = false;
        }
    }

    [Fact]
    public void Haltepunkt_OrtOhneCc0Grafik_HatFassade_GegenprobeProzeduralOrtAlt()
    {
        var p = Nico();
        Assert.NotNull(HaltepunktTausch.Platz(p, (PyObjekt)Py.Kopie(p.AussenStand)!, "birkenhain"));
        // Gegenprobe: wie bis S 0.46.0 hat ein Ort nur aus Bloecken keine Fassade, also keinen Platz fuer die Tuer
        TuerFassade.ProzeduralOrtAltFuerTests = true;
        try
        {
            Assert.Null(HaltepunktTausch.Platz(p, (PyObjekt)Py.Kopie(p.AussenStand)!, "birkenhain"));
        }
        finally
        {
            TuerFassade.ProzeduralOrtAltFuerTests = false;
        }
    }

    [Fact]
    public void Werkstatt_EingangMitWindfang_EinSchritt_SpielZeichnetIhn_FigurKommtHinein_GegenprobeImTor()
    {
        var p = Nico();
        var geschichte = Geschichte(p);
        var schritte = p.Verlauf.Count;
        var vorher = TuerSetzen.Tuer(p.AussenStand, Werkstatt)!.Value;
        Assert.Null(Eingang(p));
        Assert.Equal(schritte + 1, p.Verlauf.Count);
        var sd = Aussen.Stadtdaten(p.AussenStand);
        var d = (PyObjekt)Get(sd, "darstellung")!;
        Assert.True(Py.Wahr(Get((PyObjekt)Get(d, "windfang")!, "betriebswerkstatt")));
        Assert.Null(Get((PyObjekt)Get(d, "modell_tuer")!, "betriebswerkstatt"));
        var neu = TuerSetzen.Tuer(p.AussenStand, Werkstatt)!.Value;
        Assert.True(Vector2.Distance(vorher, neu) > 1f);
        // die Tuer liegt auf einer Wand ohne Tor
        var f = TuerFassade.Rechnen(p, (PyObjekt)Py.Kopie(p.AussenStand)!, Werkstatt)!;
        Assert.DoesNotContain(f.WandFuer(neu, null), GebaeudeTuer.TorWaende(p.AussenStand, Werkstatt, f));
        Assert.Empty(RoteFunde(p));
        Assert.Equal(geschichte, Geschichte(p));
        // im Spiel: Tuer mit Windfang, Vorbau als Teile, die Figur kommt durch die neue Tuer
        var sz = Szene(p);
        var t = Assert.Single(sz.Tueren, x => x.Ort == "betriebswerkstatt");
        Assert.True(t.Windfang);
        Assert.False(t.Modell);
        Assert.Contains(sz.Teile, x => x.Farbe == StadtTueren.WindfangWand);
        FigurKommtHinein(p, "betriebswerkstatt");
        Assert.Null(BelastungsprobenTests.SpielLaedt(p.Stand()));
        // Gegenprobe: wie bis S 0.45.0 (ohne Windfang) bleibt die Tuer im Rolltor
        GebaeudeTuer.WindfangAltFuerTests = true;
        try
        {
            var q = Nico();
            Assert.Equal(Texte.TuerImTorKeineSeite, Eingang(q));
            Assert.Null(Get((PyObjekt?)Get((PyObjekt)Get(Aussen.Stadtdaten(q.AussenStand), "darstellung")!, "windfang") ?? new PyObjekt(), "betriebswerkstatt"));
        }
        finally
        {
            GebaeudeTuer.WindfangAltFuerTests = false;
        }
    }

    /// <summary>Kreuzt ein Stueck des Wegs (ohne das letzte, das auf der Strassenmitte endet) die Mitte einer Strasse?</summary>
    private static bool WegUeberStrasse(Projekt p, string ort)
    {
        var w = Weg(Zugang(p, ort));
        static float K(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);
        foreach (var l in AussenBearbeitung.Strassen(p.AussenStand))
        {
            for (var i = 0; i + 1 < w.Count; i++)
            {
                for (var j = 0; j + 1 < l.Count; j++)
                {
                    var (r, e) = (w[i + 1] - w[i], l[j + 1] - l[j]);
                    var n = K(r, e);
                    if (MathF.Abs(n) < 1e-9f)
                    {
                        continue;
                    }
                    var t = K(l[j] - w[i], e) / n;
                    var u = K(l[j] - w[i], r) / n;
                    if (t > 0.01f && t < 0.99f && u >= 0 && u <= 1)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>Selbst gefunden: an der schraegen Strasse vor der Werkstatt lief das gerade Stueck ueber die Strassenmitte hinaus.</summary>
    [Fact]
    public void Werkstatt_WegGehtNichtUeberDieStrasse_GegenprobeAlt()
    {
        var p = Nico();
        Assert.Null(Eingang(p));
        Assert.False(WegUeberStrasse(p, "betriebswerkstatt"));
        Assert.Empty(RoteFunde(p));
        GebaeudeTuer.UeberStrasseAltFuerTests = true;
        try
        {
            var q = Nico();
            Eingang(q);
            Assert.True(WegUeberStrasse(q, "betriebswerkstatt"));
        }
        finally
        {
            GebaeudeTuer.UeberStrasseAltFuerTests = false;
        }
    }

    [Fact]
    public void Beides_DatenAendernNurBirkenhainUndWerkstatt()
    {
        var p = Nico();
        var vorher = (PyObjekt)Py.Kopie(p.AussenStand)!;
        Assert.Null(Haltepunkt(p));
        Assert.Null(Eingang(p));
        Assert.Empty(RoteFunde(p));
        // ausser Birkenhain, der Werkstatt und dem neuen Modell bleibt jede Zeile der Stadt gleich
        static PyObjekt Ohne(PyObjekt a)
        {
            var k = (PyObjekt)Py.Kopie(a)!;
            var sd = Aussen.Stadtdaten(k);
            foreach (var ort in new[] { "birkenhain", "betriebswerkstatt" })
            {
                ((PyObjekt)Get(sd, "zugaenge_neu")!).RemoveAll(e => e.Key == ort);
            }
            var d = (PyObjekt)Get(sd, "darstellung")!;
            d.RemoveAll(e => e.Key == "windfang");
            ((PyObjekt)Get(d, "modell_tuer")!).RemoveAll(e => e.Key == "betriebswerkstatt");
            ((PyObjekt)Get(Aussen.Karte(k), "modelle")!).RemoveAll(e => e.Key == Zusatzmodelle.HaltepunktFrei);
            ((List<object?>)Aussen.Karte(k)["orte"]!).RemoveAll(o => o is PyObjekt x && Py.Gleich(x["id"], "birkenhain"));
            return k;
        }
        Assert.Equal(PythonJson.Text(Ohne(vorher), sortiert: true), PythonJson.Text(Ohne(p.AussenStand), sortiert: true));
    }
}
