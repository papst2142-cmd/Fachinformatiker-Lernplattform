using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Patch A 0.75.2 (Weisung P12-263, Nico 14.11.2026: „Das ist die Stadt 1. Version 2.“): Stadt 1 Fassung 2
/// (<c>stadt1_fassung2.fistadt</c>, Herkunft <c>fi-fehlerberichte</c> main c08f44d
/// <c>stadtbau/2026-11-14_182607_PC_S0.53.1.fistadt</c>, Kopie <c>tests/Stadtbau.Tests/Testdaten/nico/stadt1_S0.53.1.fistadt</c>).
/// Gegenueber Nicos Datei ist nur der Kopf anders (Fassung 2, Pruefsumme; Grundlage 0 wie bei Fassung 1, damit das Werkzeug sie als Stadt 1 oeffnet). Geprueft wird Nicos Ziel je Punkt der
/// Weisung: neue Zebrastreifen werden wirklich gequert und Autos halten dort, gemalte Hoehen liegen nie unter Haus, Strasse,
/// Weg, Gleis, Laterne, Haltestelle, Zebrastreifen oder Ampel, der neue Zugang Talheim fuehrt von der Tuer auf der Wand bis
/// zum Gehweg, und die Grenzen der Grafikstufen halten (nie gelockert).
/// </summary>
public sealed class StadtbauFassung2Tests
{
    private static byte[]? Lesen(string pfad)
    {
        var datei = Path.Combine(Pfade.Wurzel, pfad);
        return File.Exists(datei) ? File.ReadAllBytes(datei) : null;
    }

    internal static readonly Lazy<StadtFassung> Fassung2 = new(() => AktiveFassung.LadenNummer(Lesen, 2));

    private static readonly Lazy<StadtFassung> Fassung1 = new(() => AktiveFassung.LadenNummer(Lesen, 1));

    private static PyObjekt Roh(string pfad) => (PyObjekt)Py.Lesen(File.ReadAllBytes(Path.Combine(Pfade.Wurzel, pfad)))!;

    private static StadtSpiel Spiel(StadtFassung f, GrafikBibliothek? grafik = null)
    {
        var inhalt = f.MitKarte(SpielInhaltTests.Inhalt);
        return new StadtSpiel(f.StadtFuer(inhalt), SpielQuestTests.AllesErledigt(inhalt), grafik, SpielInnenraumTests.Innenraeume.Value);
    }

    [Fact]
    public void Aktiv_IstFassung2_MitKopfStadt1Grundlage0()
    {
        var f = AktiveFassung.Laden(Lesen);
        Assert.Equal(2, f.Nummer);
        Assert.Equal(AktiveFassung.Stadt1, f.StadtId);
        Assert.Empty(StadtbauPruefung.Format(Py.Lesen(File.ReadAllBytes(Path.Combine(Pfade.Wurzel, AktiveFassung.Ordner + "stadt1_fassung2.fistadt")))));
        var kopf = (PyObjekt)Roh(AktiveFassung.Ordner + "stadt1_fassung2.fistadt")["stadt"]!;
        Assert.Equal("stadt1", kopf["id"]);
        Assert.Equal(2L, Py.Int(kopf["fassung"]));
        Assert.Equal(0L, Py.Int(kopf["basis"]));
    }

    /// <summary>Alles ausser Kopf und Pruefsumme ist Nicos Datei (auch die Uhr: Nicos Datei hat schon 1 Minute je Schritt).</summary>
    [Fact]
    public void Fassung2_GleichNicosDatei_BisAufKopf()
    {
        var eigen = Roh(AktiveFassung.Ordner + "stadt1_fassung2.fistadt");
        var nico = Roh("tests/Stadtbau.Tests/Testdaten/nico/stadt1_S0.53.1.fistadt");
        Assert.Equal(nico.Select(p => p.Key), eigen.Select(p => p.Key));
        foreach (var (feld, wert) in nico.Where(p => p.Key is not ("stadt" or "pruefsumme")))
        {
            Assert.True(Py.Gleich(wert, eigen[feld]), feld);
        }
        var kopfNico = (PyObjekt)nico["stadt"]!;
        Assert.Equal(1L, Py.Int(kopfNico["fassung"]));
        // Nicos Datei ist selbst in Ordnung (eigene Pruefsumme stimmt)
        Assert.Empty(StadtbauPruefung.Format(nico));
    }

    [Fact]
    public void Fassung2_LaedtOhneFund_VerbautLeer_EineMinuteJeSchritt()
    {
        Assert.Empty(StadtbauPruefung.Inhalt(Fassung2.Value, SpielInhaltTests.Inhalt));
        Assert.Empty(StadtbauPruefung.Verbaut(Fassung2.Value, null));
        Assert.Equal(1, Fassung2.Value.StadtFuer(Fassung2.Value.MitKarte(SpielInhaltTests.Inhalt)).MinutenJeSchritt);
    }

    /// <summary>Die Zebrastreifen von Fassung 2, die es in Fassung 1 nicht gab (Lage weiter als 0,1 von jedem alten).</summary>
    private static List<Zebra> NeueZebras(StadtSzene neu, StadtSzene alt) =>
        neu.Zebras.Where(z => alt.Zebras.All(a => Vector2.Distance(a.Mitte, z.Mitte) > 0.1f)).ToList();

    /// <summary>
    /// Jeder neue Zebrastreifen (Weisung Punkt 3): die Figur geht vom Gehweg der einen Seite zur anderen ueber genau diesen
    /// Zebrastreifen (Regel 342, <see cref="StadtSzene.LaufWeg"/>), quer ueber seine Mitte, ohne die Fahrbahn daneben zu
    /// betreten; sie kommt dabei wirklich auf den Zebrastreifen.
    /// </summary>
    [Fact]
    public void NeueZebrastreifen_FigurQuertDort()
    {
        var sz = Spiel(Fassung2.Value).Szene;
        var alt = Spiel(Fassung1.Value).Szene;
        Assert.Equal(9, sz.Zebras.Count);
        Assert.Equal(5, alt.Zebras.Count);
        var neue = NeueZebras(sz, alt);
        Assert.Equal(4, neue.Count);
        foreach (var q in neue)
        {
            var halb = (StadtSzene.StrassenBreite / 2) + 0.3f;
            var a = q.Mitte + (q.Quer * halb);
            var b = q.Mitte - (q.Quer * halb);
            var weg = sz.LaufWeg((a.X, a.Y), (b.X, b.Y));
            Assert.True(weg.Count > 0, $"Zebrastreifen {q.Nr}: kein Weg");
            var punkte = new List<Vector2> { a };
            punkte.AddRange(weg.Select(p => new Vector2((float)p.X, (float)p.Y)));
            var drauf = false;
            for (var i = 0; i + 1 < punkte.Count; i++)
            {
                for (var k = 0; k <= 50; k++)
                {
                    var p = Vector2.Lerp(punkte[i], punkte[i + 1], k / 50f);
                    Assert.Null(SpielQuerenTests.Verboten(sz, p));
                    drauf |= q.Drauf(p);
                    if (MathF.Abs(Vector2.Dot(p - q.Mitte, q.Quer)) < StadtSzene.StrassenBreite / 2)
                    {
                        Assert.True(MathF.Abs(Vector2.Dot(p - q.Mitte, q.Richtung)) < 0.01f,
                            string.Create(CultureInfo.InvariantCulture, $"Zebrastreifen {q.Nr}: {Vector2.Dot(p - q.Mitte, q.Richtung):F3} neben der Mitte"));
                    }
                }
            }
            Assert.True(drauf, $"Zebrastreifen {q.Nr}: die Figur betritt ihn nicht");
            TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture, $"neuer Zebrastreifen {q.Nr} bei {q.Mitte.X:F2}/{q.Mitte.Y:F2}: gequert"));
        }
    }

    /// <summary>
    /// Jeder neue Zebrastreifen (Weisung Punkt 3): ein Auto des echten Verkehrs von Fassung 2, dessen Spur ueber den
    /// Zebrastreifen fuehrt, haelt davor, solange die Figur darauf steht; Gegenprobe: ohne Figur faehrt es.
    /// </summary>
    [Fact]
    public void NeueZebrastreifen_AutosHalten_GegenprobeOhneFigurFaehrt()
    {
        var sz = Spiel(Fassung2.Value).Szene;
        var neue = NeueZebras(sz, Spiel(Fassung1.Value).Szene);
        var v = Verkehr.Fuer(sz, null, Verkehr.Abstand);
        Assert.NotEmpty(v.Autos);
        // Ampeln aus, damit nur der Zebrastreifen das Auto anhalten kann (Gegenprobe ohne Figur)
        Verkehr.AmpelnAusFuerTests = true;
        try
        {
            Pruefen();
        }
        finally
        {
            Verkehr.AmpelnAusFuerTests = false;
        }

        void Pruefen()
        {
        foreach (var q in neue)
        {
            var geprueft = 0;
            foreach (var auto in v.Autos)
            {
                // Stelle der Spur am Zebrastreifen (Mitte des Autos naeher als 0,3 an der Mitte des Zebrastreifens, laengs der Strasse)
                float? an = null;
                for (var s = 0f; s < auto.Laenge; s += 0.02f)
                {
                    var (ort, richtung) = auto.Lage(s);
                    if (Vector2.Distance(ort, q.Mitte) < 0.3f && MathF.Abs(Vector2.Dot(richtung, q.Richtung)) > 0.9f)
                    {
                        an = s;
                        break;
                    }
                }
                if (an is not { } stelle || stelle < 1.5f)
                {
                    continue;
                }
                Verkehr Allein()
                {
                    var w = Verkehr.Fuer(sz, null, Verkehr.Abstand);
                    var kopie = w.Autos.Single(x => x.Nummer == auto.Nummer);
                    w.Autos.Clear();
                    w.Autos.Add(kopie);
                    kopie.Weg = stelle - kopie.Halb - 0.6f;
                    return w;
                }
                foreach (var seite in (float[])[-0.2f, 0.2f])
                {
                    var figur = q.Mitte + (q.Quer * seite);
                    var w = Allein();
                    w.Weiter(0.1, figur.X, figur.Y);
                    Assert.True(w.Autos[0].Haelt, string.Create(CultureInfo.InvariantCulture, $"Zebrastreifen {q.Nr}: Auto {auto.Nummer} faehrt bei Figur {seite} darauf"));
                }
                var ohne = Allein();
                ohne.Weiter(0.1, -1000, -1000);
                Assert.False(ohne.Autos[0].Haelt, $"Gegenprobe Zebrastreifen {q.Nr}: Auto {auto.Nummer} haelt ohne Figur");
                geprueft++;
            }
            TestContext.Current.SendDiagnosticMessage($"neuer Zebrastreifen {q.Nr}: {geprueft} Autos halten");
            Assert.True(geprueft > 0, $"Zebrastreifen {q.Nr}: kein Auto faehrt darueber");
        }
        }
    }

    /// <summary>
    /// Gemalte Hoehen (Weisung Punkt 4): wo das Gelaende hoeher als 0 ist, steht kein Haus, keine Fahrbahn mit Gehweg, kein
    /// Fussweg, kein Gleis, keine Laterne, keine Haltestelle, kein Zebrastreifen und keine Ampel (sonst stuende es in der Luft
    /// oder im Boden). Eigene Rechnung (nicht die Pruefung des Werkzeugs) auf einem Raster 0,1 ueber dem ganzen Gelaende.
    /// Gegenprobe: dieselbe Rechnung trifft ein Haus, wenn eine Hoehe unter das Haus gemalt ist.
    /// </summary>
    [Fact]
    public void GemalteHoehen_NieUnterHausStrasseWegGleisLaterneHaltestelle_Gegenprobe()
    {
        var f = Fassung2.Value;
        var sz = Spiel(f).Szene;
        var g = sz.Gelaende;
        Assert.NotNull(g);
        Assert.Empty(g.Huegel);
        Assert.Equal(317, g.Gemalt.Count);
        var laternen = f.Laternen.Select(l => new Vector2(l.X, l.Z)).ToList();
        Assert.Equal(74, laternen.Count);

        List<string> Treffer(Func<float, float, float> hoehe)
        {
            var treffer = new List<string>();
            var punkte = 0;
            for (var x = 0.05f; x < sz.Breite; x += 0.1f)
            {
                for (var z = 0.05f; z < sz.Tiefe; z += 0.1f)
                {
                    if (hoehe(x, z) <= 0.001f)
                    {
                        continue;
                    }
                    punkte++;
                    var p = new Vector2(x, z);
                    string? was =
                        sz.Haeuser.FirstOrDefault(h => h.Abstand(p) <= 0f) is { } h ? "Haus " + h.Id
                        : sz.Strassen.Any(s => s.Count >= 2 && Kurve.Abstand(p, s) < (StadtSzene.StrassenBreite / 2) + 0.3f) ? "Strasse mit Gehweg"
                        : sz.GehWege.Any(w => w.Count >= 2 && Kurve.Abstand(p, w) < StadtSzene.WegBreite / 2) ? "Fussweg"
                        : sz.Gleise.Any(w => w.Count >= 2 && Kurve.Abstand(p, w) < StadtSzene.GleisBreite / 2) ? "Gleis"
                        : laternen.Any(l => Vector2.Distance(l, p) < 0.3f) ? "Laterne"
                        : sz.Haltestellen.Any(hs => Vector2.Distance(hs.Mitte, p) < hs.Halb + 0.5f) ? "Haltestelle"
                        : sz.Zebras.Any(q => q.Drauf(p, 0.1f, 0.3f)) ? "Zebrastreifen"
                        : sz.Ampeln.SelectMany(a => a.Furten).Any(q => q.Drauf(p, 0.1f, 0.3f)) ? "Ampel"
                        : null;
                    if (was is not null && treffer.Count < 10)
                    {
                        treffer.Add(string.Create(CultureInfo.InvariantCulture, $"{was} bei {x:F2}/{z:F2} Hoehe {hoehe(x, z):F2}"));
                    }
                }
            }
            Assert.True(punkte > 0);
            return treffer;
        }

        var treffer = Treffer(g.Hoehe);
        Assert.True(treffer.Count == 0, string.Join("\n", treffer));

        // Gegenprobe: dieselbe Rechnung mit einer Hoehe von 0,5 unter dem ersten Haus
        var haus = sz.Haeuser[0];
        Assert.NotEmpty(Treffer((x, z) => haus.Abstand(new Vector2(x, z)) <= 0f ? 0.5f : g.Hoehe(x, z)));
    }

    /// <summary>
    /// Gemalte Hoehen: jedes Stueck (Baeume, Kleingruen, Parkbaeume), das auf dem Gelaende hoeher als 0 steht, steht auf der Hoehe
    /// seiner Mitte und eben (Fuss am Stamm hoechstens <c>Gelaende.FussSpiel</c> neben der Mitte), alle anderen Stuecke stehen wie
    /// ohne Gelaende; Tueren und Laufwege sind dieselben wie ohne Gelaende (die gemalten Hoehen sind flach genug zum Gehen).
    /// </summary>
    [Fact]
    public void GemalteHoehen_StueckeAufDemHangEben_TuerenUndWegeGleich()
    {
        var mit = Spiel(Fassung2.Value, SpielGrafikTests.Bibliothek.Value).Szene;
        StadtSzene ohne;
        Gelaende.Weglassen = true;
        try
        {
            ohne = Spiel(Fassung2.Value, SpielGrafikTests.Bibliothek.Value).Szene;
        }
        finally
        {
            Gelaende.Weglassen = false;
        }
        Assert.Null(ohne.Gelaende);
        var g = mit.Gelaende!;
        Assert.Equal(ohne.Stuecke.Count, mit.Stuecke.Count);
        var angehoben = new Dictionary<string, int>();
        for (var i = 0; i < mit.Stuecke.Count; i++)
        {
            var a = mit.Stuecke[i].Lage;
            var b = ohne.Stuecke[i].Lage;
            Assert.Equal(ohne.Stuecke[i].Name, mit.Stuecke[i].Name);
            Assert.Equal(b.M41, a.M41);
            Assert.Equal(b.M43, a.M43);
            Assert.Equal(b.M42 + g.Boden(a.M41, a.M43), a.M42, 4);
        }
        Assert.NotNull(mit.GruenKlein);
        foreach (var s in mit.Stuecke.Concat(mit.GruenKlein!.Stuecke).Concat(mit.GruenKlein.Baeume))
        {
            var (x, z) = (s.Lage.M41, s.Lage.M43);
            if (g.Boden(x, z) > 0f)
            {
                Assert.True(g.Flach(x, z, StadtbauPruefung.BaumStamm), string.Create(CultureInfo.InvariantCulture, $"{s.Name} bei {x:F2}/{z:F2} steht schief"));
                angehoben[s.Name] = angehoben.GetValueOrDefault(s.Name) + 1;
            }
        }
        TestContext.Current.SendDiagnosticMessage("auf dem Gelaende: " + string.Join(", ", angehoben.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + " " + p.Value)));
        Assert.NotEmpty(angehoben);
        Assert.Equal(ohne.Orte.Select(o => (o.Id, o.TuerX, o.TuerZ, o.StehX, o.StehZ)), mit.Orte.Select(o => (o.Id, o.TuerX, o.TuerZ, o.StehX, o.StehZ)));
        foreach (var o in mit.Orte)
        {
            foreach (var o2 in mit.Orte.Where(x => x.Id != o.Id))
            {
                Assert.Equal(ohne.LaufWeg((o.StehX, o.StehZ), (o2.StehX, o2.StehZ)), mit.LaufWeg((o.StehX, o.StehZ), (o2.StehX, o2.StehZ)));
            }
        }
    }

    /// <summary>
    /// Zugang Talheim (Weisung Punkt 5): neu in Fassung 2 (<c>zugaenge_neu.talheim</c>); die Tuer liegt auf der Wand des
    /// Hauses (Grenze 0,02, Regel <c>StadtTueren</c>), der Weg fuehrt ohne Luecke von der Tuer bis zum Gehweg einer Strasse,
    /// und die Figur kommt von jedem anderen Ort zu Fuss dorthin. Gegenprobe: in Fassung 1 gab es den Eintrag nicht.
    /// </summary>
    [Fact]
    public void ZugangTalheim_TuerAufDerWand_WegBisZumGehweg_Erreichbar()
    {
        var zn = (PyObjekt)Fassung2.Value.Stadtdaten["zugaenge_neu"]!;
        Assert.Contains(zn, p => p.Key == "talheim");
        Assert.DoesNotContain(((PyObjekt)Fassung1.Value.Stadtdaten["zugaenge_neu"]!), p => p.Key == "talheim");
        var sz = Spiel(Fassung2.Value).Szene;
        var ort = sz.Orte.Single(o => o.Id == "talheim");
        var haus = sz.Haeuser.Where(h => h.Id == "talheim").ToList();
        Assert.NotEmpty(haus);
        var tuer = new Vector2(ort.TuerX, ort.TuerZ);
        var wand = haus.Min(h => h.Abstand(tuer));
        Assert.True(wand <= 0.02f, string.Create(CultureInfo.InvariantCulture, $"Tuer {wand:F3} von der Wand"));
        var z = (PyObjekt)zn["talheim"]!;
        var weg = SpielDaten.Liste(z["weg"]).Select(p => SpielDaten.Liste(p)).Select(p => new Vector2((float)Py.F(p[0]), (float)Py.F(p[1]))).ToList();
        Assert.True(weg.Count >= 2);
        Assert.True(Vector2.Distance(weg[0], tuer) <= StadtSzene.WegBreite, string.Create(CultureInfo.InvariantCulture, $"Weg beginnt {Vector2.Distance(weg[0], tuer):F2} von der Tuer"));
        var ende = weg[^1];
        var zurStrasse = sz.Strassen.Where(s => s.Count >= 2).Min(s => Kurve.Abstand(ende, s));
        Assert.True(zurStrasse <= (StadtSzene.StrassenBreite / 2) + StadtSzene.WegBreite, string.Create(CultureInfo.InvariantCulture, $"Weg endet {zurStrasse:F2} von der Strassenmitte"));
        foreach (var andere in sz.Orte.Where(o => o.Id != "talheim"))
        {
            Assert.NotEmpty(sz.LaufWeg((andere.StehX, andere.StehZ), (ort.StehX, ort.StehZ)));
        }
        TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture,
            $"Talheim: Tuer {tuer.X:F2}/{tuer.Y:F2} ({wand:F3} von der Wand), Weg {weg.Count} Punkte bis {ende.X:F2}/{ende.Y:F2} ({zurStrasse:F2} von der Strassenmitte)"));
    }

    /// <summary>
    /// Grenzen (Weisung Punkt 2): teuerstes Nahbild jeder Grafikstufe in Fassung 2 (Abstaende 4 bis 16, alle 4 Einheiten,
    /// Drehung 0 und 180, fest plus bewegt) unter der Grenze der Stufe (nie gelockert); die Werte stehen in der Meldung.
    /// </summary>
    [Fact]
    public void Grenzen_JeStufe_TeuerstesNahbildUnterDerGrenze()
    {
        var spiel = Spiel(Fassung2.Value, SpielGrafikTests.Bibliothek.Value);
        var szene = spiel.Szene;
        var zaehlung = new StadtbauGrenzen.Draussen(szene, szene.Vorbereiten());
        foreach (var (stufe, werte) in new[] { (Grafikstufe.Niedrig, Grafikstufen.Niedrig), (Grafikstufe.Mittel, Grafikstufen.Mittel), (Grafikstufe.Hoch, Grafikstufen.Hoch) })
        {
            var (grenzeDreiecke, grenzeAufrufe) = Grafikstufen.Grenze(stufe);
            (long Dreiecke, int Aufrufe, string Wo) t = (0, 0, "");
            var aufrufe = 0;
            foreach (var ab in (float[])[4f, 6f, 10f, 16f])
            {
                var b = StadtbauSpiel.Bewegt(spiel, werte.Fern(ab), halb: werte.AutosHalb);
                foreach (var dreh in (float[])[0f, 180f])
                {
                    for (var x = 1f; x < szene.Breite; x += 4)
                    {
                        for (var z = 1f; z < szene.Tiefe; z += 4)
                        {
                            var k = new Kamera(x, z, ab) { Drehung = dreh };
                            var bild = zaehlung.Bild(k.Sicht * k.Projektion(1600, 900), ab, werte: werte);
                            aufrufe = Math.Max(aufrufe, bild.Aufrufe + b.Aufrufe);
                            if (bild.Dreiecke + b.Dreiecke > t.Dreiecke)
                            {
                                t = (bild.Dreiecke + b.Dreiecke, 0, string.Create(CultureInfo.InvariantCulture, $"({x};{z}) Abstand {ab} Drehung {dreh}"));
                            }
                        }
                    }
                }
            }
            TestContext.Current.SendDiagnosticMessage($"Fassung 2 {stufe}: teuerstes Nahbild {t.Dreiecke} Dreiecke {t.Wo}, hoechstens {aufrufe} Aufrufe (Grenze {grenzeDreiecke}/{grenzeAufrufe})");
            Assert.True(t.Dreiecke <= grenzeDreiecke && aufrufe <= grenzeAufrufe, $"{stufe} {t.Dreiecke}/{aufrufe} {t.Wo}");
        }
    }
}
