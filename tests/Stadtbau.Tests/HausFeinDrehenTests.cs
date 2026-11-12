using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Tests;
using Stadtbau.Core;
using Xunit;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle A 0.70.0 / S 0.46.0 (Sammelliste 314, Weisung P12-212 Punkt 2 = a, P12-217): alle Gebaeude drehen in Schritten von
/// 15 Grad (Felder „grad“ am Gebaeude ohne Ort, „darstellung.hausgrad“ je Ort), das Spiel baut das Haus im viertelgedrehten
/// Rahmen und dreht es danach als Ganzes (Teile, Stuecke, Tueren; das Laufraster sperrt die gedrehte Grundflaeche mit
/// schmalen Streifen). Ein Bahnhof rastet auch an einem schraegen geraden Gleis in einer 15-Grad-Richtung ein. Je Fall eine
/// Gegenprobe (ohne Feld bitgleich, Haeuser nur in Vierteln, Bahnhof nur am achsparallelen Gleis).
/// </summary>
public class HausFeinDrehenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static PyObjekt Stadt1() => Aussen.Aus(D.Fassung);

    private static readonly AussenStelle Zuhause = new(AussenArt.Haus, -1, 0, "zuhause");

    private static StadtSzene Szene(PyObjekt a)
    {
        var f = Aussen.Fassung(a, D.Fassung, D.Fassung.Innenraeume, 1, 0, "test", "", "");
        return StadtSzene.Bauen(f.StadtFuer(f.MitKarte(D.Inhalt)), null, D.Grafik);
    }

    private static List<StadtbauFund> Funde(PyObjekt a) =>
        StadtbauPruefung.Aussen(Aussen.Fassung(a, D.Fassung, D.Fassung.Innenraeume, 1, 0, "test", "", ""), D.Inhalt, D.Grafik);

    private static HashSet<string> Rot(PyObjekt a) => [.. Funde(a).Where(f => f.Rot).Select(f => f.Text)];

    /// <summary>Abstand eines Punkts im Rahmen des Hauses zum Rand des Rechtecks (0 = auf der Wand).</summary>
    private static float AbstandZurWand(Vector2 p, Vector3 min, Vector3 max)
    {
        var innen = p.X >= min.X && p.X <= max.X && p.Y >= min.Z && p.Y <= max.Z;
        var dx = MathF.Max(MathF.Max(min.X - p.X, p.X - max.X), 0);
        var dz = MathF.Max(MathF.Max(min.Z - p.Y, p.Y - max.Z), 0);
        return innen ? MathF.Min(MathF.Min(p.X - min.X, max.X - p.X), MathF.Min(p.Y - min.Z, max.Z - p.Y)) : MathF.Sqrt((dx * dx) + (dz * dz));
    }

    // ---- Rechnen ------------------------------------------------------------------------------------

    [Fact]
    public void Feinlage_HinUndZurueck_UndMatrixWiePunkt()
    {
        var lage = new Hausdrehung.Feinlage(new Vector2(3.5f, -1.25f), 30);
        var p = new Vector2(7, 2);
        Assert.True(Vector2.Distance(p, lage.Zurueck(lage.Punkt(p))) < 1e-5f);
        // 30 Grad im Uhrzeigersinn von oben (x nach Osten, y nach Sueden): Osten zeigt danach nach Ost-Sued
        var r = lage.Richtung(new Vector2(1, 0));
        Assert.Equal(MathF.Cos(MathF.PI / 6), r.X, 5);
        Assert.Equal(MathF.Sin(MathF.PI / 6), r.Y, 5);
        var w = Vector3.Transform(new Vector3(p.X, 0.4f, p.Y), lage.Matrix);
        var q = lage.Punkt(p);
        Assert.Equal(q.X, w.X, 4);
        Assert.Equal(q.Y, w.Z, 4);
        Assert.Equal(0.4f, w.Y, 4);
        // 6 Schritte von 15 Grad sind eine Vierteldrehung (gleiche Richtung wie Hausdrehung.Drehen)
        Assert.True(Vector2.Distance(new Hausdrehung.Feinlage(Vector2.Zero, 90).Richtung(new Vector2(0, 1)), Hausdrehung.Drehen(new Vector2(0, 1), Vector2.Zero, 1)) < 1e-5f);
        Assert.Equal((1, 15), Hausdrehung.Zerlegen(105));
        Assert.Equal((3, 75), Hausdrehung.Zerlegen(-15));
        Assert.Equal((0, 30), Hausdrehung.Zerlegen(37));
        Assert.True(Hausdrehung.GradErlaubt(45L));
        Assert.False(Hausdrehung.GradErlaubt(90L));
        Assert.False(Hausdrehung.GradErlaubt(20L));
        Assert.False(Hausdrehung.GradErlaubt(30.0));
    }

    [Fact]
    public void Ueberlappung_UndStreifen_DeckenDieGedrehteFlaeche()
    {
        // zwei achsenparallele Rechtecke: das Kleinere der Ueberdeckungen in x und z
        Assert.Equal(0.5f, Hausdrehung.Ueberlappung(Hausdrehung.Rechteck(0, 0, 2, 2), Hausdrehung.Rechteck(1.5f, 0.5f, 4, 3)), 5);
        // ein um 45 Grad gedrehtes Quadrat und ein Rechteck in der Ecke seines Kastens: beruehren sich nicht
        var lage = new Hausdrehung.Feinlage(new Vector2(5, 5), 45);
        var raute = lage.Ecken(4, 4, 6, 6);
        var ecke = Hausdrehung.Rechteck(6.2f, 6.2f, 6.4f, 6.4f);
        Assert.True(Hausdrehung.Ueberlappung(raute, ecke) <= 0);
        // Gegenprobe: der achsenparallele Kasten um die Raute ueberdeckt die Ecke
        Assert.True(Hausdrehung.Ueberlappung(Hausdrehung.Rechteck(raute.Min(p => p.X), raute.Min(p => p.Y), raute.Max(p => p.X), raute.Max(p => p.Y)), ecke) > 0);
        // jeder Punkt der Raute liegt in einem Streifen; die Streifen ragen hoechstens eine Streifenbreite heraus
        var streifen = Hausdrehung.Streifen(raute);
        var zufall = new Random(314);
        for (var i = 0; i < 2000; i++)
        {
            var p = lage.Punkt(new Vector2(4 + (2 * (float)zufall.NextDouble()), 4 + (2 * (float)zufall.NextDouble())));
            Assert.Contains(streifen, s => p.X >= s.Item1 - 1e-4 && p.X <= s.Item3 + 1e-4 && p.Y >= s.Item2 - 1e-4 && p.Y <= s.Item4 + 1e-4);
        }
        var flaeche = streifen.Sum(s => (s.Item3 - s.Item1) * (s.Item4 - s.Item2));
        Assert.InRange(flaeche, 4.0, 4.0 + (8 * Hausdrehung.StreifenBreite * 1.5));
    }

    // ---- Werkzeug -----------------------------------------------------------------------------------

    [Fact]
    public void Haus_15GradSchritte_SechsSindEineViertel_WinkelfeldRundet()
    {
        var a = Stadt1();
        Assert.Equal(15, AussenBearbeitung.DrehSchritt(AussenArt.Haus));
        Assert.Equal(15, AussenBearbeitung.DrehSchritt(AussenArt.Gebaeude));
        Assert.True(AussenBearbeitung.DrehenSchritt(a, Zuhause));
        Assert.Equal(0, AussenBearbeitung.HausDreh(a, Zuhause));
        Assert.Equal(15, AussenBearbeitung.HausGrad(a, Zuhause));
        Assert.Equal(15, AussenBearbeitung.Winkel(a, Zuhause));
        for (var i = 0; i < 5; i++)
        {
            Assert.True(AussenBearbeitung.DrehenSchritt(a, Zuhause));
        }
        // sechs Schritte = eine Viertel: nur noch hausdreh, der Abschnitt hausgrad faellt weg
        Assert.Equal(1, AussenBearbeitung.HausDreh(a, Zuhause));
        Assert.Equal(0, AussenBearbeitung.HausGrad(a, Zuhause));
        Assert.Null(Get(Objekt(Aussen.Stadtdaten(a)["darstellung"]), Hausdrehung.GradAbschnitt));
        // das Winkelfeld rundet auf 15 Grad
        Assert.True(AussenBearbeitung.WinkelSetzen(a, Zuhause, 37));
        Assert.Equal(30, AussenBearbeitung.Winkel(a, Zuhause));
        Assert.Equal((0, 30), (AussenBearbeitung.HausDreh(a, Zuhause), AussenBearbeitung.HausGrad(a, Zuhause)));
        Assert.False(AussenBearbeitung.WinkelSetzen(a, Zuhause, 31));
        Assert.True(AussenBearbeitung.DrehenSchritt(a, Zuhause, rueckwaerts: true));
        Assert.True(AussenBearbeitung.DrehenSchritt(a, Zuhause, rueckwaerts: true));
        Assert.True(AussenBearbeitung.DrehenSchritt(a, Zuhause, rueckwaerts: true));
        Assert.Equal(345, AussenBearbeitung.Winkel(a, Zuhause));
        Assert.Equal((3, 75), (AussenBearbeitung.HausDreh(a, Zuhause), AussenBearbeitung.HausGrad(a, Zuhause)));
    }

    [Fact]
    public void Haus_NurViertel_Gegenprobe()
    {
        AussenBearbeitung.HausViertelAltFuerTests = true;
        try
        {
            var a = Stadt1();
            Assert.Equal(90, AussenBearbeitung.DrehSchritt(AussenArt.Haus));
            Assert.False(AussenBearbeitung.Drehen(a, Zuhause, 15));
            Assert.Null(AussenBearbeitung.Winkel(a, Zuhause));
            Assert.True(AussenBearbeitung.DrehenSchritt(a, Zuhause));
            Assert.Equal((1, 0), (AussenBearbeitung.HausDreh(a, Zuhause), AussenBearbeitung.HausGrad(a, Zuhause)));
        }
        finally
        {
            AussenBearbeitung.HausViertelAltFuerTests = false;
        }
    }

    [Fact]
    public void Haus_FeinGedreht_TreffenUndFlaeche_Genau()
    {
        var a = Stadt1();
        Assert.True(AussenBearbeitung.Drehen(a, Zuhause, 45));
        var e = AussenBearbeitung.HausEcken(a, Zuhause);
        var mitte = AussenBearbeitung.HausMitte(a, Zuhause);
        Assert.True(Vector2.Distance((e[0] + e[2]) / 2, mitte) < 1e-4f);
        // die Kanten stehen 45 Grad schraeg
        var k = e[1] - e[0];
        Assert.Equal(45, Math.Abs(Math.Atan2(k.Y, k.X) * 180 / Math.PI) % 90, 1);
        // Treffer: Mitte ja, Ecke des umschliessenden Kastens nein (Gegenprobe: der Kasten enthaelt sie)
        Assert.Equal(Zuhause, AussenBearbeitung.StueckBei(a, mitte.X, mitte.Y));
        var (x, y, w, h) = AussenBearbeitung.HausFlaeche(a, Zuhause);
        var kastenEcke = new Vector2((float)(x + 0.05), (float)(y + 0.05));
        Assert.False(AussenBearbeitung.HausDrin(a, Zuhause, kastenEcke));
        Assert.True(kastenEcke.X >= x && kastenEcke.Y >= y);
        // 3D-Umriss = die gedrehten Ecken
        Assert.Equal(e, Bauen3D.Umriss(a, Zuhause).Single());
    }

    // ---- Spiel --------------------------------------------------------------------------------------

    [Fact]
    public void Fassung0_OhneFeld_SzeneBitgleich_UndVierMalViertelZurueck()
    {
        var vorher = Stadt1();
        var a = Stadt1();
        // 24 Schritte von 15 Grad ergeben einen ganzen Kreis: Felder weg, Lage der Tuer bis auf die Rundung der Daten (0,01)
        for (var i = 0; i < 24; i++)
        {
            Assert.True(AussenBearbeitung.DrehenSchritt(a, Zuhause));
        }
        Assert.Equal((0, 0), (AussenBearbeitung.HausDreh(a, Zuhause), AussenBearbeitung.HausGrad(a, Zuhause)));
        Assert.Null(Get(Objekt(Aussen.Stadtdaten(a)["darstellung"]), Hausdrehung.GradAbschnitt));
        Assert.Null(Get(Objekt(Aussen.Stadtdaten(a)["darstellung"]), Hausdrehung.Abschnitt));
        // ohne Feld baut das Spiel genau wie vorher (Fassung 0 bleibt bitgleich)
        Assert.Equal(StadtSzeneHash.Stadt(Szene(vorher)), StadtSzeneHash.Stadt(Szene(Stadt1())));
        Assert.All(Szene(vorher).Haeuser, h => Assert.False(h.Fein.Aktiv));
    }

    [Theory]
    [InlineData("zuhause", 30)]
    [InlineData("zuhause", 105)]
    [InlineData("elektromarkt", 15)]
    [InlineData("talheim", 60)]
    public void Ort_FeinGedreht_SpielZeichntTuerAufDerGedrehtenWand_FigurKommtHin(string ort, int grad)
    {
        var a = Stadt1();
        var s = new AussenStelle(AussenArt.Haus, -1, 0, ort);
        var vorher = Szene(a);
        var haus0 = vorher.Haeuser.Single(h => h.Id == ort);
        var tuer0 = vorher.Tueren.Single(t => t.Ort == ort);
        Assert.True(AussenBearbeitung.Drehen(a, s, grad));
        var szene = Szene(a);
        var haus = szene.Haeuser.Single(h => h.Id == ort);
        Assert.True(haus.Fein.Aktiv);
        Assert.Equal(grad % 90, haus.Fein.Grad);
        // Groesse im Rahmen des Hauses wie vorher (mit Vierteln getauscht), der Kasten in der Welt ist groesser
        var g0 = haus0.Max - haus0.Min;
        var g1 = haus.LokalMax - haus.LokalMin;
        var (gw, gt) = (grad / 90) % 2 == 1 ? (g0.Z, g0.X) : (g0.X, g0.Z);
        Assert.Equal(gw, g1.X, 2);
        Assert.Equal(gt, g1.Z, 2);
        Assert.True((haus.Max - haus.Min).X > g1.X + 0.01f);
        // die Tuer zeigt um den Winkel gedreht hinaus und sitzt auf der gedrehten Wand
        var tuer = szene.Tueren.Single(t => t.Ort == ort);
        var soll = new Hausdrehung.Feinlage(Vector2.Zero, grad).Richtung(tuer0.Aussen);
        Assert.True(Vector2.Distance(soll, tuer.Aussen) < 0.01f, $"Tuer zeigt {tuer.Aussen}, soll {soll}");
        Assert.True(Vector2.Distance(tuer0.Aussen, tuer.Aussen) > 0.2f, "Gegenprobe: Tuer zeigt wie vorher");
        // im Rahmen des Hauses sitzt sie wie ohne feine Drehung (gleicher Abstand zur Wand wie vorher)
        var lokal = haus.Fein.Zurueck(tuer.Mitte);
        var wandVorher = AbstandZurWand(tuer0.Mitte, haus0.Min, haus0.Max);
        Assert.True(MathF.Abs(AbstandZurWand(lokal, haus.LokalMin, haus.LokalMax) - wandVorher) < 0.03f,
            $"Tuer {lokal} sitzt anders an der Wand {haus.LokalMin}..{haus.LokalMax} als vorher ({wandVorher})");
        // die Seite der Tuer in den Daten ist die Seite, zu der das Spiel die Tuer zeichnet
        Assert.True(Vector2.Dot(AussenBearbeitung.HausTuerSeite(a, s), tuer.Aussen) > 0.99f);
        // Laufraster: die Mitte des Hauses ist gesperrt, vor der Tuer kann die Figur stehen und kommt vom Weg dorthin
        var raster = szene.Laufraster;
        var m = haus.Fein.Mitte;
        Assert.False(raster.KannStehen(m.X, m.Y));
        var vor = tuer.WegAnfang + (tuer.Aussen * 0.1f);
        Assert.True(raster.KannStehen(vor.X, vor.Y), "vor der Tuer kann die Figur nicht stehen");
        var weg = AussenBearbeitung.Linie(AussenBearbeitung.Zugang(a, ort)!["weg"]);
        var pfad = raster.Weg((weg[^1].X, weg[^1].Y), (vor.X, vor.Y));
        Assert.NotEmpty(pfad);
        Assert.True(Vector2.Distance(new Vector2((float)pfad[^1].X, (float)pfad[^1].Y), vor) <= 0.3f, "Weg endet nicht vor der Tuer");
        // der Ort betritt man durch die gedrehte Tuer
        var o = szene.Ort(ort)!;
        Assert.True(Vector2.Distance(new Vector2(o.TuerX, o.TuerZ), tuer.WegAnfang) <= 0.6f);
    }

    /// <summary>Zahl der Ecken mit der Marke „leuchtet nachts“ im Netz eines Hauses.</summary>
    private static int Leuchtend(StadtSzene szene, string ort)
    {
        var netz = szene.HausNetz(szene.Haeuser.Single(h => h.Id == ort));
        return Enumerable.Range(0, netz.AnzahlEcken).Count(e => netz.FarbeAn(e).X >= Nachtlicht.Marke - 0.5f);
    }

    [Fact]
    public void Ort_FeinGedreht_FensterLeuchtenNachts_GegenprobeDuenneSeiteUngedreht()
    {
        // jedes Haus der Stadt 1 um 30 Grad: leuchtet es ungedreht, leuchtet es auch gedreht; Fenster aus Kaesten leuchten mit
        // der gedrehten duennen Seite (welche Scheiben eines Modells leuchten, entscheidet der Wuerfel nach der Lage)
        var vorher = Szene(Stadt1());
        var kaesten = 0;
        foreach (var haus in vorher.Haeuser.Where(h => AussenBearbeitung.Ort(Stadt1(), h.Id) is not null))
        {
            var a = Stadt1();
            Assert.True(AussenBearbeitung.Drehen(a, new AussenStelle(AussenArt.Haus, -1, 0, haus.Id), 30));
            var szene = Szene(a);
            var neu = Leuchtend(szene, haus.Id);
            Assert.True(Leuchtend(vorher, haus.Id) == 0 || neu > 0, $"{haus.Id}: gedreht leuchtet nichts");
            // Gegenprobe: mit der ungedrehten duennen Seite leuchten gedrehte Fenster aus Kaesten nicht
            StadtSzene.LichtDrehAltFuerTests = true;
            try
            {
                var alt = Leuchtend(Szene(a), haus.Id);
                Assert.True(alt <= neu);
                var gedreht = szene.Haeuser.Single(h => h.Id == haus.Id);
                if (szene.LichtTeile.Any(i => i >= gedreht.TeileVon && i < gedreht.TeileBis))
                {
                    kaesten++;
                    Assert.True(alt < neu, $"{haus.Id}: Gegenprobe leuchtet gleich ({alt})");
                }
            }
            finally
            {
                StadtSzene.LichtDrehAltFuerTests = false;
            }
        }
        Assert.True(kaesten > 0, "kein Ort mit Fenstern aus Kaesten");
    }

    [Fact]
    public void Gebaeude_OhneOrt_FeinGedreht_DrehtTeileUndStuecke()
    {
        var a = Stadt1();
        var stellwerk = new AussenStelle(AussenArt.Gebaeude, 13);
        var vorher = Szene(a);
        Assert.True(AussenBearbeitung.Drehen(a, stellwerk, 45));
        Assert.Equal(45L, Objekt(Aussen.Landschaft(a)[13])[Hausdrehung.GradFeld]);
        var szene = Szene(a);
        var neu = szene.Haeuser.Where(h => h.Fein.Aktiv).ToList();
        Assert.Single(neu);
        Assert.Equal(45, neu[0].Fein.Grad);
        Assert.NotEqual(StadtSzeneHash.Stadt(vorher), StadtSzeneHash.Stadt(szene));
        // die CC0-Stuecke des Hauses stehen um 45 Grad gedreht (Lage mal Feindrehung); vorher in Vierteln
        static bool Schraeg(ModellStueck st) => MathF.Abs(st.Lage.M13) > 0.1f && MathF.Abs(MathF.Abs(st.Lage.M11) - MathF.Abs(st.Lage.M13)) < 1e-3f;
        var stuecke = szene.Stuecke.Skip(neu[0].StueckeVon).Take(neu[0].StueckeBis - neu[0].StueckeVon).ToList();
        Assert.NotEmpty(stuecke);
        Assert.All(stuecke, st => Assert.True(Schraeg(st), $"{st.Name} nicht schraeg"));
        Assert.DoesNotContain(vorher.Stuecke, Schraeg);
    }

    // ---- Pruefung -----------------------------------------------------------------------------------

    [Fact]
    public void Pruefung_UngueltigeFeineDrehung_Rot()
    {
        var a = Stadt1();
        var gruen = Rot(a);
        Assert.True(AussenBearbeitung.Drehen(a, new AussenStelle(AussenArt.Gebaeude, 13), 30));
        var gedreht = Rot(a);
        Py.Setzen(Objekt(Aussen.Landschaft(a)[13]), Hausdrehung.GradFeld, 20L);
        Assert.Contains(Rot(a), f => !gedreht.Contains(f) && !gruen.Contains(f));
        var b = Stadt1();
        var d = Objekt(Aussen.Stadtdaten(b)["darstellung"]);
        Py.Setzen(d, Hausdrehung.GradAbschnitt, new PyObjekt { { "gibt_es_nicht", 15L } });
        Assert.Contains(Rot(b), f => f.Contains("gibt_es_nicht", StringComparison.Ordinal));
        Py.Setzen(d, Hausdrehung.GradAbschnitt, new PyObjekt { { "zuhause", 90L } });
        Assert.Contains(Rot(b), f => f.Contains("feine Drehung 90", StringComparison.Ordinal));
        // Gegenprobe: gueltiger Wert ohne diesen Fund
        Py.Setzen(d, Hausdrehung.GradAbschnitt, new PyObjekt { { "zuhause", 15L } });
        Assert.DoesNotContain(Rot(b), f => f.Contains("feine Drehung", StringComparison.Ordinal));
    }

    [Fact]
    public void Pruefung_GedrehtesHausInEinemAnderen_RotNurWennEsSichUeberdeckt()
    {
        // zwei Haeuser dicht nebeneinander: um 45 Grad gedreht ragt die Ecke ins Nachbarhaus (rot); in Vierteln nicht
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_fein", 32, 20));
        var modell = AussenBearbeitung.HausModelle(a)[0];
        var s1 = AussenBearbeitung.SetzenUndDrehen(a, AussenArt.Gebaeude, 10, 10, true, modell, 0)!;
        var (x1, _, w, _) = AussenBearbeitung.HausFlaeche(a, s1);
        var s2 = AussenBearbeitung.SetzenUndDrehen(a, AussenArt.Gebaeude, 10 + w + 0.2, 10, true, modell, 0)!;
        // 0,05 neben dem ersten Haus (die Ecke eines um 45 Grad gedrehten Hauses ragt weiter hinaus)
        Py.Setzen(Objekt(Aussen.Landschaft(a)[s2.Nummer]), "x", Bearbeitung.Zahl(x1 + w + 0.05));
        var vorher = Rot(a);
        Assert.DoesNotContain(vorher, f => f.Contains("ineinander", StringComparison.Ordinal));
        Assert.True(AussenBearbeitung.Drehen(a, s2, 90));
        Assert.DoesNotContain(Rot(a), f => f.Contains("ineinander", StringComparison.Ordinal) && !vorher.Contains(f));
        Assert.True(AussenBearbeitung.Drehen(a, s2, 45));
        Assert.Contains(Rot(a), f => f.Contains("ineinander", StringComparison.Ordinal));
    }

    // ---- Bahnhof am schraegen Gleis ---------------------------------------------------------------------

    private static readonly Lazy<byte[]> Nico = new(() =>
        File.ReadAllBytes(Path.Combine(Daten.Wurzel, "tests", "Stadtbau.Tests", "Testdaten", "nico", "teststadt_S0.43.0.fistadt")));

    private static Projekt NicoProjekt() => Projekt.AusStadtDatei(Nico.Value, D, Daten.NeueAblage(), "Nico Teststadt");

    [Fact]
    public void Bahnhof_AmSchraegenGleis_RastetParallelEin_GegenprobeNurAchsparallel()
    {
        // Nicos Gleis laeuft von (4,78; 40) nach (24,10; 28,85) unter -30 Grad; der Haltepunkt steht ungedreht knapp noerdlich
        // daneben (Mitte rund 0,9 vor der Gleismitte)
        var p = NicoProjekt();
        var s = new AussenStelle(AussenArt.Gebaeude, Aussen.Landschaft(p.AussenStand).FindIndex(t => t is PyObjekt g && Py.Gleich(g["modell"], "haltepunkt")));
        var a = (PyObjekt)Py.Kopie(p.AussenStand)!;
        var g = Objekt(Aussen.Landschaft(a)[s.Nummer]);
        Py.Setzen(g, "x", 12.0);
        Py.Setzen(g, "y", 34.2);
        Assert.True(AussenBearbeitung.BahnhofAnGleis(a, s));
        // -30 Grad = 330: nach den Vierteln bleiben 60 Grad feine Drehung
        Assert.Equal(60, AussenBearbeitung.HausGrad(a, s));
        // Bahnsteigkante parallel zum Gleis im Abstand BahnsteigKante von der Gleismitte
        var teile = Get(AussenBearbeitung.Modelle(a), "haltepunkt");
        var lage = AussenBearbeitung.HausLage(a, s);
        var r = Bahnsteig.Rechtecke(teile, Py.F(g["x"]), Py.F(g["y"]), AussenBearbeitung.HausDreh(a, s)).Single();
        var (ga, gb) = (new Vector2(4.7776f, 40f), new Vector2(24.0951f, 28.847f));
        var u = Vector2.Normalize(gb - ga);
        var n = new Vector2(-u.Y, u.X);
        var abstaende = lage.Ecken(r.Min.X, r.Min.Y, r.Max.X, r.Max.Y).Select(e => MathF.Abs(Vector2.Dot(e - ga, n))).OrderBy(d => d).ToList();
        Assert.Equal(abstaende[0], abstaende[1], 2);
        Assert.Equal(AussenBearbeitung.BahnsteigKante, abstaende[0], 0.012f);
        // Gegenprobe: wie bis S 0.45.0 nur achsparallel
        AussenBearbeitung.BahnhofSchraegAltFuerTests = true;
        try
        {
            var b = (PyObjekt)Py.Kopie(p.AussenStand)!;
            Py.Setzen(Objekt(Aussen.Landschaft(b)[s.Nummer]), "x", 12.0);
            Py.Setzen(Objekt(Aussen.Landschaft(b)[s.Nummer]), "y", 34.2);
            Assert.False(AussenBearbeitung.BahnhofAnGleis(b, s));
        }
        finally
        {
            AussenBearbeitung.BahnhofSchraegAltFuerTests = false;
        }
    }

    [Fact]
    public void Bahnhof_GleisNichtIn15GradRichtung_RastetNicht()
    {
        // das Stueck unter -52,5 Grad (24,10; 28,85) bis (31,98; 18,58) liegt zwischen zwei 15-Grad-Richtungen
        var p = NicoProjekt();
        var s = new AussenStelle(AussenArt.Gebaeude, Aussen.Landschaft(p.AussenStand).FindIndex(t => t is PyObjekt g && Py.Gleich(g["modell"], "haltepunkt")));
        var a = (PyObjekt)Py.Kopie(p.AussenStand)!;
        var g = Objekt(Aussen.Landschaft(a)[s.Nummer]);
        Py.Setzen(g, "x", 26.0);
        Py.Setzen(g, "y", 22.0);
        Assert.False(AussenBearbeitung.BahnhofAnGleis(a, s));
    }
}
