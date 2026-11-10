using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle A 0.60.0 / S 0.35.0 (Weisung P12-166, Kurzplan P12-79 Wunsch 6): Ampeln im Werkzeug (setzen nur auf eine Kreuzung,
/// nie am Kreisverkehr, nie zwei an einer Kreuzung; mit der Strasse schieben und loeschen), im Spiel (Furten im Laufraster
/// frei wie ein Zebrastreifen, Autos halten bei Rot und fahren bei Gruen, Takt der beiden Richtungen, Lampen) und die
/// Pruefregeln je rot und gruen.
/// Grundlage: leere Stadt 24 × 16, Strasse (2; 8)–(22; 8) und Strasse (12; 2)–(12; 14), beide mit Gehweg 0,35, Kreuzung (12; 8).
/// </summary>
public class AmpelTests
{
    private static Spieldaten D => Daten.Spiel;

    private static List<object?> P(double x, double y) => [x, y];

    private static PyObjekt Grundlage(bool ampel = true, double suedEnde = 14)
    {
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_ampel", 24, 16));
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(2, 8), P(22, 8) } }, { "gehweg", 0.35 } });
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(12, 2), P(12, suedEnde) } }, { "gehweg", 0.35 } });
        if (ampel)
        {
            Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Ampel, 12.5, 8.6));
        }
        return a;
    }

    private static List<object?> Liste(PyObjekt a) => AussenBearbeitung.AmpelListe(a);

    private static Vector2 Mitte(PyObjekt a, int i) => new((float)Py.F(Objekt(Liste(a)[i])["x"]), (float)Py.F(Objekt(Liste(a)[i])["y"]));

    private static List<StadtbauFund> Pruefen(PyObjekt a) =>
        StadtbauPruefung.Aussen(Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", ""), D.Inhalt, D.Grafik);

    private static string Text(List<StadtbauFund> f) => string.Join(" | ", f.Select(x => (x.Rot ? "rot: " : "gelb: ") + x.Text));

    private static void Gruen(PyObjekt a)
    {
        var f = Pruefen(a);
        Assert.True(!f.Any(x => x.Text.Contains("Ampel", StringComparison.Ordinal)), Text(f));
    }

    private static void Rot(PyObjekt a, string teil)
    {
        var f = Pruefen(a);
        Assert.True(f.Any(x => x.Rot && x.Text.Contains(teil, StringComparison.Ordinal)), "erwartet rot „" + teil + "“, gefunden: " + Text(f));
    }

    private static StadtSzene Szene(PyObjekt a) =>
        Ansicht3D.Bauen(Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", ""), D, Ansicht3D.Einstellung.Standard).Szene;

    // ---- Werkzeug ------------------------------------------------------------------------------------

    [Fact]
    public void Setzen_SpringtAufDieKreuzung_EineJeKreuzung()
    {
        var a = Grundlage();
        Assert.Single(Liste(a));
        Assert.Equal(new Vector2(12, 8), Mitte(a, 0));
        var amp = AussenBearbeitung.AmpelVon(a, new AussenStelle(AussenArt.Ampel, 0))!;
        Assert.Equal(4, amp.Arme.Count);
        Assert.Equal(4, amp.Masten.Count());
        // zweite Ampel an derselben Kreuzung: nichts gesetzt
        Assert.Null(AussenBearbeitung.Setzen(a, AussenArt.Ampel, 12, 8));
        Assert.Single(Liste(a));
        // fern jeder Kreuzung: nichts gesetzt
        Assert.Null(AussenBearbeitung.Setzen(a, AussenArt.Ampel, 5, 8));
        Assert.Single(Liste(a));
        // Kopieren und Drehen gibt es nicht
        Assert.Null(AussenBearbeitung.Kopieren(a, new AussenStelle(AussenArt.Ampel, 0)));
        Assert.Equal(Texte.AmpelDrehenNicht, AussenBearbeitung.DrehenNicht(new AussenStelle(AussenArt.Ampel, 0)));
    }

    [Fact]
    public void Setzen_AmKreisverkehr_Nie()
    {
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_ampel", 24, 16));
        AussenBearbeitung.Setzen(a, AussenArt.Kreisverkehr, 12, 8, modell: AussenBearbeitung.Kennung(2));
        // Zufahrt nach Osten trifft den Ring bei (14; 8)
        Assert.Null(AussenBearbeitung.Setzen(a, AussenArt.Ampel, 14, 8));
        Assert.Null(AussenBearbeitung.Setzen(a, AussenArt.Ampel, 14.3, 8));
        Assert.Empty(Liste(a));
    }

    [Fact]
    public void Einmuendung_ReichtFuerEineAmpel()
    {
        var a = Grundlage(ampel: false, suedEnde: 8);
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Ampel, 12, 8));
        Assert.Equal(3, AussenBearbeitung.AmpelVon(a, new AussenStelle(AussenArt.Ampel, 0))!.Arme.Count);
        Gruen(a);
    }

    [Fact]
    public void MitDerStrasseSchieben_UndLoeschen()
    {
        var a = Grundlage();
        // die senkrechte Strasse 2 nach Osten: die Ampel rueckt mit
        AussenBearbeitung.Verschieben(a, new AussenStelle(AussenArt.Strasse, 1), 2, 0);
        Assert.Equal(new Vector2(14, 8), Mitte(a, 0));
        Gruen(a);
        // Ampel selbst schieben: nur auf eine Kreuzung, sonst bleibt sie stehen
        AussenBearbeitung.Verschieben(a, new AussenStelle(AussenArt.Ampel, 0), -6, 0);
        Assert.Equal(new Vector2(14, 8), Mitte(a, 0));
        // die Strasse loeschen: keine Kreuzung mehr, die Ampel faellt weg (und mit ihr der Abschnitt)
        Assert.True(AussenBearbeitung.Loeschen(a, new AussenStelle(AussenArt.Strasse, 1)));
        Assert.Empty(Liste(a));
        Assert.False(Py.Hat(Aussen.Stadtdaten(a), Ampeln.Abschnitt));
    }

    [Fact]
    public void Mehrfach_Kopieren_NimmtDieAmpelMit()
    {
        var a = Grundlage();
        var gruppe = new List<AussenStelle> { new(AussenArt.Strasse, 0), new(AussenArt.Strasse, 1), new(AussenArt.Ampel, 0) };
        var a2 = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_ampel", 60, 16));
        Aussen.Landschaft(a2).AddRange(Aussen.Landschaft(a).Select(Py.Kopie));
        Py.Setzen(Aussen.Stadtdaten(a2), Ampeln.Abschnitt, Py.Kopie(Liste(a)));
        var e = AussenBearbeitung.Kopieren(a2, gruppe);
        Assert.Equal(2, Liste(a2).Count);
        Assert.Contains(e.Auswahl, s => s.Art == AussenArt.Ampel && s.Nummer == 1);
        Assert.Equal(8, Mitte(a2, 1).Y);
        Assert.True(Mitte(a2, 1).X > 12);
    }

    // ---- Spiel ---------------------------------------------------------------------------------------

    [Fact]
    public void Laufraster_FurtenFrei_FahrbahnSonstGesperrt_Gegenprobe()
    {
        var szene = Szene(Grundlage());
        Assert.Single(szene.Ampeln);
        foreach (var furt in szene.Ampeln[0].Furten)
        {
            Assert.True(szene.Laufraster.KannStehen(furt.Mitte.X, furt.Mitte.Y), $"Furt bei {furt.Mitte} ist nicht begehbar");
        }
        Assert.False(szene.Laufraster.KannStehen(16, 8), "Fahrbahn neben der Ampel ist begehbar");
        // Gegenprobe: ohne Ampel ist dieselbe Stelle der Furt gesperrt
        var ohne = Szene(Grundlage(ampel: false));
        Assert.False(ohne.Laufraster.KannStehen(12 + Ampeln.FurtMitte, 8));
    }

    private static Verkehr Auto(StadtSzene szene, bool nachOsten, float frontVorMitte)
    {
        var v = new Verkehr { FuerStrassen = szene.Strassen };
        v.Ampeln.AddRange(szene.Ampeln);
        IReadOnlyList<Vector2> linie = nachOsten ? [new Vector2(0, 8), new Vector2(24, 8)] : [new Vector2(12, 16), new Vector2(12, 0)];
        var spur = Kurve.Versetzt(linie, Verkehr.Spur);
        // Mitte des Autos 0,3 hinter der Front
        var weg = (nachOsten ? 12 : 8) - frontVorMitte - 0.3f;
        v.Autos.Add(new Verkehr.Auto("car-taxi", spur, weg));
        return v;
    }

    [Fact]
    public void Auto_HaeltBeiRot_FaehrtBeiGruen_BeideRichtungen_Gegenprobe()
    {
        var szene = Szene(Grundlage());
        // Achse 0 = die Strasse des ersten Arms (waagrecht): Gruen ab 0, dann rot; Achse 1 genau umgekehrt
        foreach (var (osten, gruen, rot) in new[] { (true, 1.0, 12.0), (false, 12.0, 1.0) })
        {
            var g = Auto(szene, osten, 1.2f);
            g.ZeitSetzen(gruen);
            g.Weiter(0.1, 0, 0);
            Assert.False(g.Autos[0].Haelt, $"haelt bei Gruen (nach {(osten ? "Osten" : "Norden")})");
            var r = Auto(szene, osten, 1.2f);
            r.ZeitSetzen(rot);
            r.Weiter(0.1, 0, 0);
            Assert.True(r.Autos[0].Haelt, $"faehrt bei Rot (nach {(osten ? "Osten" : "Norden")})");
            // weit vor der Haltelinie haelt es nicht (es faehrt bis an die Linie)
            var w = Auto(szene, osten, 3f);
            w.ZeitSetzen(rot);
            w.Weiter(0.1, 0, 0);
            Assert.False(w.Autos[0].Haelt);
        }
        // Gegenprobe: ohne Ampel-Regel faehrt das Auto bei Rot
        Verkehr.AmpelnAusFuerTests = true;
        try
        {
            var r = Auto(szene, true, 1.2f);
            r.ZeitSetzen(12);
            r.Weiter(0.1, 0, 0);
            Assert.False(r.Autos[0].Haelt);
        }
        finally
        {
            Verkehr.AmpelnAusFuerTests = false;
        }
    }

    [Fact]
    public void Auto_FaehrtBeiRotBisAnDieLinie_UndWiederBeiGruen()
    {
        var szene = Szene(Grundlage());
        var v = Auto(szene, true, 3f);
        v.ZeitSetzen(Ampeln.GruenZeit + Ampeln.GelbZeit);   // Rot fuer die waagrechte Strasse
        for (var i = 0; i < 40; i++)
        {
            v.Weiter(0.05, 0, 0);
        }
        var (front, _) = v.Autos[0].Lage(v.Autos[0].Weg + 0.3f);
        Assert.True(v.Autos[0].Haelt);
        Assert.InRange(12 - front.X, Ampeln.HalteVon, Ampeln.HalteBis);
        // bis zum naechsten Gruen weiter: es faehrt ueber die Kreuzung
        v.ZeitSetzen(Ampeln.Umlauf + 0.5);
        for (var i = 0; i < 60; i++)
        {
            v.Weiter(0.05, 0, 0);
        }
        Assert.True(v.Autos[0].Lage().Ort.X > 12.5f, "das Auto ist bei Gruen nicht ueber die Kreuzung gefahren");
    }

    [Fact]
    public void Lampen_SechsGruppen_ImTakt()
    {
        var szene = Szene(Grundlage());
        var netze = Ampeln.LichtNetze(szene.Ampeln)!;
        Assert.Equal(Ampeln.Gruppen, netze.Count);
        Assert.All(netze, n => Assert.True(n.Indizes.Count > 0));
        Assert.Null(Ampeln.LichtNetze([]));
        for (var t = 0.0; t < Ampeln.Umlauf; t += 0.5)
        {
            // je Achse leuchtet mindestens eine Lampe; nie zeigen beide Achsen Gruen oder Gelb zugleich
            for (var achse = 0; achse < 2; achse++)
            {
                Assert.Contains(Enumerable.Range(0, 3), f => Ampeln.GruppeAn((achse * 3) + f, t));
            }
            var z0 = Ampeln.Zustand(t, 0);
            var z1 = Ampeln.Zustand(t, 1);
            Assert.True(z0 == AmpelZustand.Rot || z1 == AmpelZustand.Rot, $"bei {t} s: {z0} / {z1}");
        }
    }

    // ---- Pruefregeln ---------------------------------------------------------------------------------

    [Fact]
    public void Pruefung_Grundlage_Gruen() => Gruen(Grundlage());

    [Fact]
    public void Regel1_FalschGespeichert_Rot()
    {
        var a = Grundlage();
        Py.Setzen(Objekt(Liste(a)[0]), "x", "zwölf");
        Rot(a, "Ampel Nr. 1 ist falsch gespeichert");
    }

    [Fact]
    public void Regel2_AusserhalbDerKarte_Rot()
    {
        var a = Grundlage();
        Liste(a).Add(new PyObjekt { { "x", 30.0 }, { "y", 8.0 } });
        Rot(a, "Ampel Nr. 2 liegt außerhalb der Karte");
    }

    [Fact]
    public void Regel3_AmKreisverkehr_Rot()
    {
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_ampel", 24, 16));
        AussenBearbeitung.Setzen(a, AussenArt.Kreisverkehr, 12, 8, modell: AussenBearbeitung.Kennung(2));
        Py.Setzen(Aussen.Stadtdaten(a), Ampeln.Abschnitt, new List<object?> { new PyObjekt { { "x", 14.0 }, { "y", 8.0 } } });
        Rot(a, "Ampel Nr. 1 steht am Kreisverkehr");
    }

    [Fact]
    public void Regel4_KeineKreuzung_Rot()
    {
        var a = Grundlage();
        Liste(a).Add(new PyObjekt { { "x", 5.0 }, { "y", 8.0 } });
        Rot(a, "Ampel Nr. 2 steht an keiner Kreuzung");
    }

    [Fact]
    public void Regel5_ZweiAnDerselbenKreuzung_Rot()
    {
        var a = Grundlage();
        Liste(a).Add(new PyObjekt { { "x", 12.0 }, { "y", 8.0 } });
        Rot(a, "Ampel Nr. 2 steht an derselben Kreuzung wie Ampel Nr. 1");
    }

    [Fact]
    public void Regel6_ArmZuKurz_Rot_UndLangGenug_Gruen()
    {
        var kurz = Grundlage(ampel: false, suedEnde: 9.0);
        Assert.NotNull(AussenBearbeitung.Setzen(kurz, AussenArt.Ampel, 12, 8));
        Rot(kurz, "An Ampel Nr. 1 ist eine Straße kürzer als");
        var lang = Grundlage(ampel: false, suedEnde: 8 + StadtbauPruefung.AmpelArmMindest + 0.1);
        Assert.NotNull(AussenBearbeitung.Setzen(lang, AussenArt.Ampel, 12, 8));
        Gruen(lang);
    }

    [Fact]
    public void Regel8_ZebrastreifenZuNah_Rot_UndWeitGenug_Gruen()
    {
        var a = Grundlage();
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 13.6, 8));
        Rot(a, "liegt zu nah an Ampel Nr. 1");
        var b = Grundlage();
        Assert.NotNull(AussenBearbeitung.Setzen(b, AussenArt.Zebrastreifen, 15, 8));
        Gruen(b);
    }

    [Fact]
    public void Regel9_LaterneAufDemMast_Rot_UndDaneben_Gruen()
    {
        var a = Grundlage();
        var mast = AussenBearbeitung.AmpelVon(a, new AussenStelle(AussenArt.Ampel, 0))!.Masten.First().Fuss;
        Aussen.Laternen(a).Add(new List<object?> { (double)mast.X + 0.05, (double)mast.Y, false });
        Rot(a, "steht auf dem Platz eines Masts von Ampel Nr. 1");
        Aussen.Laternen(a).Clear();
        Gruen(a);
    }

    [Fact]
    public void LaternenAutomatik_MeidetDieMasten()
    {
        var p = Projekt.Neu("Ampel Laternen", D, Daten.NeueAblage(), null, (24, 16));
        p.AussenAendern("Strasse", a => Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(2, 8), P(22, 8) } }, { "gehweg", 0.35 } }));
        p.AussenAendern("Strasse", a => Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(12, 2), P(12, 14) } }, { "gehweg", 0.35 } }));
        p.AussenAendern("Ampel", a => AussenBearbeitung.Setzen(a, AussenArt.Ampel, 12, 8));
        p.LaternenVerteilen();
        var a = p.AussenStand;
        var masten = AussenBearbeitung.AmpelVon(a, new AussenStelle(AussenArt.Ampel, 0))!.Masten.Select(m => m.Fuss).ToList();
        Assert.NotEmpty(Aussen.Laternen(a));
        foreach (var l in Aussen.Laternen(a).OfType<List<object?>>())
        {
            var q = new Vector2((float)Py.F(l[0]), (float)Py.F(l[1]));
            Assert.All(masten, m => Assert.True(Vector2.Distance(q, m) >= Ampeln.MastGrundriss, $"Laterne {q} auf Mast {m}"));
        }
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot && f.Text.Contains("Ampel", StringComparison.Ordinal));
    }
}
