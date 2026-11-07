using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle K3b (A 0.46.0 / S 0.12.0, Weisungen P12-59/P12-61): Autostrecken automatisch aus dem Strassennetz
/// (<see cref="Autostrecken"/>). Stadt 1 behaelt die festen Strecken; sonst Rechtsverkehr, Kreisverkehr gegen den
/// Uhrzeigersinn mit Vorfahrt im Ring, Wenden am Ende jeder Sackgasse, kein Auto bleibt stecken; Pruefregeln rot und
/// gruen je mit Gegenprobe.
/// </summary>
public class StreckenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static List<object?> P(double x, double y) => [x, y];

    private static PyObjekt Leer(double breite = 32, double hoehe = 20) => Aussen.Aus(Aussen.Leer(D.Fassung, "neu_strecken", breite, hoehe));

    private static void Strasse(PyObjekt a, double x1, double y1, double x2, double y2) =>
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(x1, y1), P(x2, y2) } } });

    private static StadtFassung Fassung(PyObjekt a) => Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", "");

    private static StadtSzene Szene(PyObjekt a) => Ansicht3D.Bauen(Fassung(a), D, Ansicht3D.Einstellung.Standard).Szene;

    private static List<StadtbauFund> Pruefen(PyObjekt a) => StadtbauPruefung.Aussen(Fassung(a), D.Inhalt, D.Grafik);

    private static string Text(List<StadtbauFund> f) => string.Join(" | ", f.Select(x => (x.Rot ? "rot: " : "gelb: ") + x.Text));

    private static PyObjekt MitKreisverkehr(double radius)
    {
        var a = Leer();
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Kreisverkehr, 12, 10, modell: AussenBearbeitung.Kennung(radius)));
        return a;
    }

    /// <summary>Viertelkreis (Punkt alle 7,5 Grad) von <paramref name="von"/> in Richtung <paramref name="d"/>, nach rechts (+1) oder links (-1).</summary>
    private static List<Vector2> Bogen(Vector2 von, Vector2 d, float r, int seite)
    {
        var quer = Autostrecken.Rechts(d) * seite;
        var m = von + (quer * r);
        var l = new List<Vector2>();
        for (var g = 0; g <= 12; g++)
        {
            var t = g * 7.5f * MathF.PI / 180;
            l.Add(m - (quer * (r * MathF.Cos(t))) + (d * (r * MathF.Sin(t))));
        }
        return l;
    }

    private static List<IReadOnlyList<Vector2>> Gitter() =>
    [
        [new(0, 5), new(40, 5)], [new(0, 15), new(40, 15)], [new(0, 25), new(40, 25)],
        [new(5, 0), new(5, 30)], [new(20, 0), new(20, 30)], [new(35, 0), new(35, 30)],
    ];

    /// <summary>Gerade, Kurve nach rechts, Gerade: (2;4)-(10;4), Bogen Radius 2 bis (12;6), (12;6)-(12;16).</summary>
    private static List<IReadOnlyList<Vector2>> MitKurve() =>
        [[new(2, 4), new(10, 4)], Bogen(new(10, 4), Vector2.UnitX, 2, 1), [new(12, 6), new(12, 16)]];

    /// <summary>Einmuendung: (0;10)-(30;10), (15;10)-(15;20).</summary>
    private static List<IReadOnlyList<Vector2>> Einmuendung() => [[new(0, 10), new(30, 10)], [new(15, 10), new(15, 20)]];

    private static IEnumerable<(Vector2 Ort, Vector2 Richtung)> Punkte(List<Vector2> spur)
    {
        for (var i = 1; i < spur.Count; i++)
        {
            yield return (Vector2.Lerp(spur[i - 1], spur[i], 0.5f), Vector2.Normalize(spur[i] - spur[i - 1]));
        }
    }

    // ---- Regel: wann automatisch ----------------------------------------------------------------------

    [Fact]
    public void Stadt1_BehaeltDieFestenStrecken_BitGleich()
    {
        var szene = Ansicht3D.Bauen(D.Fassung, D, Ansicht3D.Einstellung.Standard).Szene;
        Assert.False(Autostrecken.Automatisch(szene));
        var v = Verkehr.Fuer(szene);
        Assert.False(v.Rundweg);
        Assert.Empty(v.Ringe);
        Assert.Equal(Verkehr.Strecken.Length, v.Autos.Count);
        for (var i = 0; i < v.Autos.Count; i++)
        {
            var (modell, punkte, start) = Verkehr.Strecken[i];
            var soll = Kurve.Versetzt(Kurve.Glatt([.. punkte.Select(q => new Vector2(q.X, q.Z))], 0.6f), Verkehr.Spur);
            Assert.Equal(modell, v.Autos[i].Modell);
            Assert.Equal(start, v.Autos[i].Weg);
            Assert.False(v.Autos[i].Schnell);
            Assert.Equal(soll, v.Autos[i].Strecke);
        }
        Assert.Equal(Verkehr.KreuzungenAus(szene.Strassen), v.Kreuzungen);
    }

    [Fact]
    public void Automatisch_MitKreisverkehrOderKurve_OderOhnePassendeFesteStrecke()
    {
        Assert.True(Autostrecken.Automatisch(Szene(MitKreisverkehr(2))));
        // Gegenprobe: leere Stadt mit einer geraden Strasse ohne feste Strecke darauf
        var a = Leer();
        Strasse(a, 2, 4, 20, 4);
        Assert.True(Autostrecken.Automatisch(Szene(a)));
        Assert.Single(Verkehr.Fuer(Szene(a)).Autos);
    }

    [Fact]
    public void Passt_NurZuDenselbenStrassen_ImWerkzeugNeuerVerkehrNachAenderung()
    {
        var a = MitKreisverkehr(2);
        var v = Verkehr.Fuer(Szene(a));
        Assert.True(v.Passt(Szene(a)));
        // Gegenprobe: eine Strasse mehr, ein anderer Punkt
        Strasse(a, 26, 4, 30, 4);
        Assert.False(v.Passt(Szene(a)));
        var stadt1 = Ansicht3D.Bauen(D.Fassung, D, Ansicht3D.Einstellung.Standard).Szene;
        Assert.True(Verkehr.Fuer(stadt1).Passt(Ansicht3D.Bauen(D.Fassung, D, Ansicht3D.Einstellung.Standard).Szene));
        Assert.False(Verkehr.Fuer(stadt1).Passt(Szene(MitKreisverkehr(2))));
    }

    [Fact]
    public void LeereStadt_OhneAuto() => Assert.Empty(Verkehr.Fuer(Szene(Leer())).Autos);

    // ---- Fahren ---------------------------------------------------------------------------------------

    [Fact]
    public void GeradeStrasse_Rechtsverkehr_WendetAnBeidenEnden_BleibtAufDerFahrbahn()
    {
        var e = Autostrecken.Anlegen([[new(0, 5), new(20, 5)]]);
        Assert.Equal(2, e.Strecken.Count);
        foreach (var (_, spur) in e.Strecken)
        {
            Assert.Equal(spur[0], spur[^1]);
            var hin = false;
            var zurueck = false;
            foreach (var (o, r) in Punkte(spur))
            {
                Assert.True(MathF.Abs(o.Y - 5) <= (StadtSzene.StrassenBreite / 2) + 1e-4f, $"neben der Fahrbahn: {o}");
                Assert.InRange(o.X, Autostrecken.WendeRand - 1e-3f, 20 - Autostrecken.WendeRand + 1e-3f);
                if (o.X is > 2 and < 18)
                {
                    // rechte Spur: nach +x unterhalb (z groesser), nach -x oberhalb
                    Assert.Equal(5 + (MathF.Sign(r.X) * Verkehr.Spur), o.Y, 3);
                    hin |= r.X > 0.99f;
                    zurueck |= r.X < -0.99f;
                }
            }
            Assert.True(hin && zurueck, "Auto faehrt nicht hin und zurueck (Wenden fehlt)");
        }
    }

    [Theory]
    [InlineData(2.0, false)]
    [InlineData(3.0, false)]
    [InlineData(2.0, true)]
    public void Kreisverkehr_ImRingGegenDenUhrzeigersinn_InselLinks(double radius, bool ringAndersHerum)
    {
        var szene = Szene(MitKreisverkehr(radius));
        var strassen = szene.Strassen.Select(s => (IReadOnlyList<Vector2>)(ringAndersHerum && s.Count > 2 ? s.Reverse().ToList() : s)).ToList();
        var e = Autostrecken.Anlegen(strassen);
        Assert.NotEmpty(e.Strecken);
        Assert.Single(e.Ringe);
        var (m, r) = e.Ringe[0];
        Assert.Equal((float)radius, r, 2);
        var imRing = 0;
        foreach (var (_, spur) in e.Strecken)
        {
            foreach (var (o, d) in Punkte(spur))
            {
                if (MathF.Abs(Vector2.Distance(o, m) - r) < 0.2f && Vector2.Distance(o, m) < r + 0.25f)
                {
                    imRing++;
                    // rechte Seite zeigt nach aussen = Insel links; auf der Karte (x nach rechts, z nach unten) gegen den Uhrzeigersinn
                    Assert.True(Vector2.Dot(Autostrecken.Rechts(d), o - m) > 0, $"falsche Richtung im Ring bei {o}");
                    Assert.True(((o.X - m.X) * d.Y) - ((o.Y - m.Y) * d.X) < 0, $"im Uhrzeigersinn bei {o}");
                }
            }
        }
        Assert.True(imRing > 20, "kein Auto faehrt im Ring");
    }

    [Fact]
    public void JedeStrecke_BleibtAufDerFahrbahn_KurveEinmuendungGitterKreisverkehr()
    {
        foreach (var (name, strassen) in new[]
        {
            ("Kurve", MitKurve()), ("Einmuendung", Einmuendung()), ("Gitter", Gitter()),
            ("Kreisverkehr", Szene(MitKreisverkehr(2)).Strassen.ToList()),
        })
        {
            var e = Autostrecken.Anlegen(strassen);
            Assert.NotEmpty(e.Strecken);
            foreach (var (nr, spur) in e.Strecken)
            {
                foreach (var (o, _) in Punkte(spur))
                {
                    Assert.True(strassen.Min(s => Kurve.Abstand(o, s)) <= (StadtSzene.StrassenBreite / 2) + 1e-3f, $"{name}, Auto {nr}: neben der Fahrbahn bei {o}");
                }
            }
        }
    }

    [Fact]
    public void Kurve_AutoFaehrtDurchDieKurve_InBeideRichtungen()
    {
        var e = Autostrecken.Anlegen(MitKurve());
        var bogen = MitKurve()[1];
        var m = new Vector2(10, 6);
        var (rechts, links) = (false, false);
        foreach (var (_, spur) in e.Strecken)
        {
            foreach (var (o, d) in Punkte(spur))
            {
                if (Kurve.Abstand(o, bogen) < 0.3f && o.X > 10.3f && o.Y < 5.7f)
                {
                    // Rechtskurve (von West nach Sued): Spur innen, Linkskurve (Sued nach West): aussen
                    var abstand = Vector2.Distance(o, m);
                    rechts |= MathF.Abs(abstand - (2 - Verkehr.Spur)) < 0.05f && d.X > 0;
                    links |= MathF.Abs(abstand - (2 + Verkehr.Spur)) < 0.05f && d.X < 0;
                }
            }
        }
        Assert.True(rechts && links, $"Kurve nicht in beiden Richtungen auf der rechten Spur (rechts {rechts}, links {links})");
    }

    [Fact]
    public void Einmuendung_IstKreuzungsfeld_AutosBiegenEinUndFahrenDurch()
    {
        var e = Autostrecken.Anlegen(Einmuendung());
        Assert.Contains(e.Kreuzungen, k => Vector2.Distance(k, new Vector2(15, 10)) < 0.01f);
        Assert.Single(e.Kreuzungen);
        Assert.Contains(e.Strecken, s => s.Spur.Any(q => q.Y > 15));
        Assert.Contains(e.Strecken, s => s.Spur.Any(q => q.X < 5) && s.Spur.Any(q => q.X > 25));
    }

    [Fact]
    public void HoechstensSechsAutos_ModelleWieStadt1_Gleichbleibend()
    {
        var e = Autostrecken.Anlegen(Gitter());
        Assert.Equal(Autostrecken.HoechstensAutos, e.Strecken.Count);
        var v = Autostrecken.VerkehrAus(e, null, Verkehr.Abstand);
        Assert.Equal(Verkehr.Strecken.Select(s => s.Modell), v.Autos.Select(x => x.Modell));
        Assert.True(v.Rundweg);
        // gleiche Strassen = gleiche Strecken und Starts (kein Zufall)
        var w = Autostrecken.VerkehrAus(Autostrecken.Anlegen(Gitter()), null, Verkehr.Abstand);
        Assert.Equal(v.Autos.Select(x => x.Weg), w.Autos.Select(x => x.Weg));
        Assert.All(v.Autos.Zip(w.Autos), p => Assert.Equal(p.First.Strecke, p.Second.Strecke));
        // keine zwei Autos am Start aufeinander
        for (var i = 0; i < v.Autos.Count; i++)
        {
            for (var j = i + 1; j < v.Autos.Count; j++)
            {
                Assert.True(Vector2.Distance(v.Autos[i].Lage().Ort, v.Autos[j].Lage().Ort) >= 1.5f);
            }
        }
    }

    [Fact]
    public void Festzeit_GestaltenAufDenAutomatischenStrecken()
    {
        var szene = Szene(MitKreisverkehr(3));
        var v = FestVerkehr.VerkehrFuer(szene, FestVerkehr.Satz.Weihnachten);
        Assert.All(v.Autos, x => Assert.Equal(2, x.Nummer));
        Assert.All(v.Autos, x => Assert.Equal(FestVerkehr.Tempo("mann"), x.Tempo));
    }

    // ---- Vorfahrt und Stecken ---------------------------------------------------------------------------

    [Fact]
    public void Vorfahrt_ImRing_EinfahrenderWartet_GegenprobeOhneRing()
    {
        Verkehr Bauen(bool mitRing)
        {
            var v = new Verkehr { Rundweg = true };
            v.Kreuzungen.Add(new Vector2(2, 0));
            if (mitRing)
            {
                v.Ringe.Add((Vector2.Zero, 2));
            }
            // im Ring: Radius 2,17 mit fallendem Winkel (Insel links), 1,5 vor dem Feld (noch ohne Anspruch auf das Feld)
            var ring = Enumerable.Range(0, 97).Select(i => 2.17f * new Vector2(MathF.Cos(0.75f - (i * MathF.Tau / 96)), MathF.Sin(0.75f - (i * MathF.Tau / 96)))).ToList();
            ring[^1] = ring[0];
            v.Autos.Add(new Verkehr.Auto("car-taxi", ring, 0) { Schnell = true });
            v.Autos.Add(new Verkehr.Auto("car-van", [new(6, -0.17f), new(-6, -0.17f)], 3) { Schnell = true });
            return v;
        }
        var mit = Bauen(true);
        mit.Weiter(1.0 / 60, -1000, -1000);
        Assert.True(mit.Autos[1].Haelt, "Einfahrender faehrt trotz Auto im Ring");
        Assert.False(mit.Autos[0].Haelt);
        var ohne = Bauen(false);
        ohne.Weiter(1.0 / 60, -1000, -1000);
        Assert.False(ohne.Autos[1].Haelt);
    }

    [Fact]
    public void KeinAutoBleibtStecken_InAllenProbestaedten()
    {
        foreach (var (name, strassen) in new[]
        {
            ("Kurve", MitKurve()), ("Einmuendung", Einmuendung()), ("Gitter", Gitter()),
            ("Kreisverkehr 2", Szene(MitKreisverkehr(2)).Strassen.ToList()), ("Kreisverkehr 3", Szene(MitKreisverkehr(3)).Strassen.ToList()),
        })
        {
            var v = Autostrecken.VerkehrAus(Autostrecken.Anlegen(strassen), null, Verkehr.Abstand);
            var start = v.Autos.Select(x => x.Weg).ToList();
            Assert.True(Autostrecken.Stecken(v).Count == 0, name + ": ein Auto bleibt stecken");
            Assert.All(v.Autos.Zip(start), p => Assert.NotEqual(p.Second, p.First.Weg));
        }
    }

    [Fact]
    public void Stecken_FindetZweiAutosDieAufeinanderWarten_Gegenprobe()
    {
        // zwei Felder nah beieinander; jedes Auto steht in einem und will in das andere
        var v = new Verkehr { Rundweg = true };
        v.Kreuzungen.Add(new Vector2(0, 0));
        v.Kreuzungen.Add(new Vector2(1, 0));
        v.Autos.Add(new Verkehr.Auto("car-taxi", [new(-5, 0.17f), new(6, 0.17f), new(-5, 0.17f)], 5.2f) { Schnell = true });
        v.Autos.Add(new Verkehr.Auto("car-van", [new(6, -0.17f), new(-5, -0.17f), new(6, -0.17f)], 5.2f) { Schnell = true });
        Assert.Equal(2, Autostrecken.Stecken(v).Count);
    }

    // ---- Pruefung im Werkzeug -------------------------------------------------------------------------

    [Fact]
    public void Pruefung_KreisverkehrUndKurze_Strasse()
    {
        var gut = MitKreisverkehr(2);
        Assert.DoesNotContain(Pruefen(gut), x => x.Text.Contains("Auto", StringComparison.Ordinal) || x.Text.Contains("Sackgasse", StringComparison.Ordinal));
        var lang = Leer();
        Strasse(lang, 4, 4, 4 + Autostrecken.WendeMindest + 0.1, 4);
        Assert.DoesNotContain(Pruefen(lang), x => x.Text.Contains("Sackgasse", StringComparison.Ordinal));
        // Gegenprobe: zu kurz zum Wenden
        var kurz = Leer();
        Strasse(kurz, 4, 4, 5.2, 4);
        var f = Pruefen(kurz);
        Assert.True(f.Any(x => x.Rot && x.Text.StartsWith("Diese Sackgasse ist zu kurz", StringComparison.Ordinal)), Text(f));
        Assert.Empty(Verkehr.Fuer(Szene(kurz)).Autos);
    }

    [Fact]
    public void Fassung0_OhneFundDerStrecken() =>
        Assert.DoesNotContain(StadtbauPruefung.Aussen(D.Fassung, D.Inhalt, D.Grafik), x => x.Text.Contains("Sackgasse", StringComparison.Ordinal) || x.Text.Contains("Auto bleibt", StringComparison.Ordinal));
}
