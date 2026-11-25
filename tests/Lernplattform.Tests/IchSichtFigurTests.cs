using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Weisung P12-302 (Fund Bild 04 zu Meldung P12-183): beim Blick nach unten in der Ich-Sicht ist in keinem Bild einer Bewegung und
/// bei keinem Aussehen ein Teil von Kopf, Haar, Bart, Brille, Kopfbedeckung oder die Innenseite des Halses zu sehen, und kein
/// Teil der eigenen Figur kommt dem Auge naeher als die nahe Ebene plus <see cref="IchSicht.FigurAbstand"/>.
/// </summary>
public class IchSichtFigurTests
{
    private static PyObjekt Daten => SpielGeschaefteTests.Daten.Value;

    /// <summary>Stehend und jedes Bild beider Bewegungen (Gehen drinnen, Rennen draussen); Treppen nutzen dieselben Bilder.</summary>
    public static IReadOnlyList<(string? Bewegung, int Bild)> Bewegungsbilder() =>
    [
        (null, -1),
        .. Enumerable.Range(0, Figur.Bilder).Select(i => ((string?)Figur.Gehen, i)),
        .. Enumerable.Range(0, Figur.Bilder).Select(i => ((string?)Figur.Rennen, i)),
        // Welle A 0.93.0 (Weisung P12-303): geduckt, kriechend und im Sprung (das Auge sinkt bzw. steigt mit dem Kopf)
        (Figur.Ducken, 0),
        (Figur.Kriechen, 0),
        (Figur.Springen, 0),
        // Welle A 1.7.0 (Sammelliste 447): Kriechen = flach auf dem Bauch robben, jedes Bild und still
        .. Enumerable.Range(0, Figur.RobbenBilder).Select(i => ((string?)Figur.Robben, i)),
        (Figur.RobbenStill, 0),
    ];

    /// <summary>
    /// Aussehen: jede Frisur x jeder Bart x (ohne und jede Kopfbedeckung der Datei, auch Festkleidung) als volles Kreuz (nur diese
    /// drei wirken aufeinander: Locken fallen unter Hut und Muetze weg, die Kopfbedeckung faerbt das Haar um); dazu jedes andere
    /// Stueck der Datei einzeln mit langem Haar und Vollbart sowie die zwei Paare, die aufeinander wirken (Kapuze mit Kleid, Kapuze
    /// mit Brille und Hut). Haut, Haarfarbe und Farbe des Oberteils aendern nur Farben, keine Ecke: je eine Wahl genuegt.
    /// </summary>
    public static IReadOnlyList<(string Name, PyObjekt Aussehen, List<Figur.Stueck>? Kleidung)> AlleAussehen()
    {
        var stuecke = Kleidung.Alle(Daten).GroupBy(k => (k.Slot, k.Form)).Select(g => new Figur.Stueck(g.Key.Slot, g.Key.Form, g.First().Farbe)).ToList();
        var kopf = stuecke.Where(s => s.Slot == "kopf").ToList();
        var liste = new List<(string, PyObjekt, List<Figur.Stueck>?)>();
        foreach (var frisur in Aussehen.WahlFuer("frisur").Select(w => w.Kennung))
        {
            foreach (var bart in Aussehen.WahlFuer("bart").Select(w => w.Kennung))
            {
                var a = new PyObjekt { { "frisur", frisur }, { "bart", bart }, { "haarfarbe", "braun" } };
                liste.Add(($"{frisur}/{bart}", a, null));
                liste.AddRange(kopf.Select(k => ($"{frisur}/{bart}/{k.Form}", a, (List<Figur.Stueck>?)[k])));
            }
        }
        var lang = new PyObjekt { { "frisur", "lang" }, { "bart", "vollbart" }, { "haarfarbe", "blond" } };
        liste.AddRange(stuecke.Where(s => s.Slot != "kopf").Select(s => ($"lang/vollbart/{s.Slot}:{s.Form}", lang, (List<Figur.Stueck>?)[s])));
        Figur.Stueck Erstes(string slot, string form) => stuecke.First(s => s.Slot == slot && s.Form == form);
        liste.Add(("lang/vollbart/kapuze+kleid", lang, [Erstes("oberteil", "kapuze"), Erstes("kleid", "kleid")]));
        liste.Add(("lang/vollbart/kapuze+brille+hut", lang, [Erstes("oberteil", "kapuze"), Erstes("brille", "brille"), Erstes("kopf", "hut")]));
        return liste;
    }

    /// <summary>
    /// Kopf der Figur im Bild: alle Dreiecke der Vorlage, deren Mitte stehend ab 0,68 der Hoehe liegt (dort beginnt der Kopf),
    /// und alle Teile am Kopf (Gesicht, Kopfbedeckung, Kapuze, Frisur, Bart, Brille), so wie die ganze Figur sie zeigt
    /// (Ecken im Bild der Bewegung, Hoehe 1). Unabhaengig von der Figur ohne Kopf gebaut.
    /// </summary>
    private static HashSet<(int, int, int, int, int, int, int, int, int)> KopfDreiecke(GrafikBibliothek g, PyObjekt aussehen, List<Figur.Stueck>? kleidung,
        string? bewegung, int bild)
    {
        var gm = g.Modell(Figur.Modell)!;
        var form = bild >= 0 && bewegung is not null ? g.Modell(Figur.BildName(bewegung, bild))! : gm;
        var groesse = gm.Max.Y - gm.Min.Y;
        var mitte = (gm.Min + gm.Max) / 2;
        Vector3 Lage(Vector3 p) => new((p.X - mitte.X) / groesse, (p.Y - gm.Min.Y) / groesse, (p.Z - mitte.Z) / groesse);
        var menge = new HashSet<(int, int, int, int, int, int, int, int, int)>();
        for (var d = 0; d < gm.Dreiecke; d++)
        {
            var y = (gm.Ecken[(d * 9) + 1] + gm.Ecken[(d * 9) + 4] + gm.Ecken[(d * 9) + 7]) / 3;
            if ((y - gm.Min.Y) / groesse < 0.68f)
            {
                continue;
            }
            var p = Enumerable.Range(0, 3).Select(e => Lage(new Vector3(form.Ecken[(d * 9) + (e * 3)], form.Ecken[(d * 9) + (e * 3) + 1], form.Ecken[(d * 9) + (e * 3) + 2]))).ToArray();
            menge.Add(Schluessel(p[0], p[1], p[2]));
        }
        var (min, max) = Figur.Kopf(gm, form)!.Value;
        var a = Aussehen.Normalisieren(aussehen);
        var haar = Netz.Farbe(Figur.Haare[Py.Str(a["haarfarbe"])]);
        var kopfStueck = kleidung?.FirstOrDefault(k => k.Slot == "kopf");
        var teile = Figur.Gesicht(min, max, Vector3.One);
        if (kopfStueck is not null)
        {
            teile.AddRange(Figur.Kopfbedeckung(min, max, gm.Max.Y, kopfStueck));
        }
        if (kleidung?.Any(k => k is { Slot: "oberteil", Form: "kapuze" }) == true)
        {
            // mit Kleid zeigt die ganze Figur keine Kapuze; hier trotzdem als Kopf gezaehlt (strenger)
            teile.AddRange(Figur.Kapuze(min, max, Vector3.One));
        }
        teile.AddRange(Figur.Frisur(min, max, Py.Str(a["frisur"]), haar, kopfStueck));
        teile.AddRange(Figur.Bart(min, max, Aussehen.Wert(a, "bart"), haar));
        if (kleidung?.FirstOrDefault(k => k.Slot == "brille") is { } brille)
        {
            teile.AddRange(Figur.Brille(min, max, Netz.Farbe(brille.Farbe)));
        }
        var netz = Netz.Aus(teile);
        for (var i = 0; i + 2 < netz.Indizes.Count; i += 3)
        {
            menge.Add(Schluessel(Lage(netz.Ort((int)netz.Indizes[i])), Lage(netz.Ort((int)netz.Indizes[i + 1])), Lage(netz.Ort((int)netz.Indizes[i + 2]))));
        }
        return menge;
    }

    /// <summary>Dreieck als Schluessel (Ecken auf 1/10000 gerundet, Reihenfolge egal).</summary>
    private static (int, int, int, int, int, int, int, int, int) Schluessel(Vector3 a, Vector3 b, Vector3 c)
    {
        var p = new[] { a, b, c }.Select(v => (X: (int)MathF.Round(v.X * 1e4f), Y: (int)MathF.Round(v.Y * 1e4f), Z: (int)MathF.Round(v.Z * 1e4f)))
            .OrderBy(v => v.X).ThenBy(v => v.Y).ThenBy(v => v.Z).ToArray();
        return (p[0].X, p[0].Y, p[0].Z, p[1].X, p[1].Y, p[1].Z, p[2].X, p[2].Y, p[2].Z);
    }

    /// <summary>Auge in der Figur (Hoehe 1, Blick der Figur nach +Z) wie <see cref="IchSicht.Auge"/>.</summary>
    private static readonly Vector3 AugeImModell = new(0f, IchSicht.AugeAnteil, IchSicht.AugeVor);

    /// <summary>Welle A 0.93.0: Auge in der Figur in der Haltung des Bildes (gesenkt um die Senkung des Kopfes, wie <see cref="IchSicht.AugeHoehe"/>; der Sprung hebt Figur und Auge gleich).</summary>
    private static Vector3 AugeIn(GrafikBibliothek g, string? bewegung) =>
        AugeImModell - new Vector3(0f, AugeGesenkt ? Figur.KopfSenkung(g, bewegung) : 0f, 0f);

    /// <summary>Gegenprobe (nur in diesem Test): false = Auge in jeder Haltung in Stehhoehe.</summary>
    private static bool AugeGesenkt = true;

    /// <summary>
    /// Liegt <paramref name="p"/> (Figur, Hoehe 1) im Sichtkegel des Auges bei <paramref name="neigung"/> Grad nach unten? Sichtfeld
    /// 40 Grad hoch, Bildseiten bis 2,5:1 (breiter als PC und Handy), ohne nahe und ferne Ebene (strenger: auch was die nahe Ebene
    /// abschneiden wuerde, zaehlt).
    /// </summary>
    private static bool ImKegel(Vector3 p, float neigung, Vector3? auge = null)
    {
        var n = neigung * MathF.PI / 180f;
        var vor = new Vector3(0f, -MathF.Sin(n), MathF.Cos(n));
        var hoch = new Vector3(0f, MathF.Cos(n), MathF.Sin(n));
        var v = p - (auge ?? AugeImModell);
        var tiefe = Vector3.Dot(v, vor);
        if (tiefe <= 0f)
        {
            return false;
        }
        var halb = MathF.Tan(20f * MathF.PI / 180f);
        return MathF.Abs(Vector3.Dot(v, hoch)) <= tiefe * halb && MathF.Abs(v.X) <= tiefe * halb * 2.5f;
    }

    private sealed record Fund(string Wo, int KopfImKegel, int KopfDrin, float Naechste, int HalsOffen);

    /// <summary>Prueft eine Figur ohne Kopf gegen den Kopf der ganzen Figur im selben Bild.</summary>
    private static Fund Pruefen(GrafikBibliothek g, string name, PyObjekt aussehen, List<Figur.Stueck>? kleidung, string? bewegung, int bild,
        float bis = IchSicht.KoerperBis)
    {
        var ohne = Figur.OhneKopf(g, aussehen, kleidung, bewegung, bild, bis);
        var kopf = KopfDreiecke(g, aussehen, kleidung, bewegung, bild);
        // die Figur ohne Kopf steht um den Versatz des Halses verschoben (der Hals bleibt unter dem Auge); zum Vergleich mit der
        // ganzen Figur zurueckschieben
        var v = Figur.OhneKopfAltFuerTests ? Vector3.Zero : Figur.HalsVerschiebung(g, aussehen, kleidung, bewegung, bild, bis);
        int imKegel = 0, drin = 0;
        var dreiecke = ohne.Indizes.Count / 3;
        // die letzten 12 Dreiecke sind der Deckel am Hals
        for (var d = 0; d < dreiecke - 12; d++)
        {
            var p = Enumerable.Range(0, 3).Select(e => ohne.Ort((int)ohne.Indizes[(d * 3) + e])).ToArray();
            if (!kopf.Contains(Schluessel(p[0] - v, p[1] - v, p[2] - v)))
            {
                continue;
            }
            drin++;
            if (p.Any(q => Enumerable.Range(0, 18).Any(i => ImKegel(q, i * 5f, AugeIn(g, bewegung)))))
            {
                imKegel++;
            }
        }
        var auge = AugeIn(g, bewegung);
        var naechste = ohne.Indizes.Distinct().Min(e => Vector3.Distance(ohne.Ort((int)e), auge));
        return new Fund($"{name} {bewegung ?? "stehend"} {bild}", imKegel, drin, naechste, HalsOffen(ohne, bewegung is Figur.Robben or Figur.RobbenStill));
    }

    /// <summary>
    /// Offene Kanten oben am Hals (Kanten nur eines Dreiecks, hoeher als 0,1 unter dem Deckel, nah an der Mitte), die nicht im
    /// Deckel (letzte 12 Dreiecke) liegen: durch sie saehe man in die Figur hinein.
    /// </summary>
    private static int HalsOffen(Netz ohne, bool liegend = false)
    {
        var dreiecke = ohne.Indizes.Count / 3;
        var deckel = Enumerable.Range((dreiecke - 12) * 3, 36).Select(i => ohne.Ort((int)ohne.Indizes[i])).ToList();
        Vector3 dMin = deckel.Aggregate(Vector3.Min), dMax = deckel.Aggregate(Vector3.Max);
        var kanten = new Dictionary<((int, int, int), (int, int, int)), int>();
        (int, int, int) R(Vector3 v) => ((int)MathF.Round(v.X * 1e4f), (int)MathF.Round(v.Y * 1e4f), (int)MathF.Round(v.Z * 1e4f));
        for (var d = 0; d < dreiecke - 12; d++)
        {
            for (var e = 0; e < 3; e++)
            {
                var a = R(ohne.Ort((int)ohne.Indizes[(d * 3) + e]));
                var b = R(ohne.Ort((int)ohne.Indizes[(d * 3) + ((e + 1) % 3)]));
                var k = a.CompareTo(b) < 0 ? (a, b) : (b, a);
                kanten[k] = kanten.GetValueOrDefault(k) + 1;
            }
        }
        var offen = 0;
        foreach (var ((a, b), zahl) in kanten)
        {
            if (zahl != 1)
            {
                continue;
            }
            foreach (var (x, y, z) in new[] { a, b })
            {
                var p = new Vector3(x, y, z) / 1e4f;
                if (p.Y < dMax.Y - 0.1f || MathF.Abs(p.X) > Figur.HalsSeitlich)
                {
                    continue;
                }
                // Welle A 1.7.0: flach auf dem Bauch liegt der ganze Ruecken in Halshoehe; dort zaehlt nur der Rand nahe am Hals (laengs
                // so weit wie zur Seite), nicht die Kante eines Kleids hinten am Ruecken (0,3 hinter dem Hals)
                if (liegend && MathF.Abs(p.Z - ((dMin.Z + dMax.Z) / 2f)) > Figur.HalsSeitlich)
                {
                    continue;
                }
                // nicht unter dem Deckel (von oben gesehen) oder hoeher als er: offen
                if (p.X < dMin.X - 0.002f || p.X > dMax.X + 0.002f || p.Z < dMin.Z - 0.002f || p.Z > dMax.Z + 0.002f || p.Y > dMax.Y + 0.002f)
                {
                    offen++;
                }
            }
        }
        return offen;
    }

    [Fact]
    public void BlickNachUnten_JedesBewegungsbild_JedesAussehen_KeinKopfNichtsVorDerKamera()
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        var aussehen = AlleAussehen();
        Assert.True(aussehen.Count >= 5 * 3 * 8, "Aussehen fehlen");
        Assert.Contains(aussehen, a => a.Name.Contains("lang/vollbart/hasenohren", StringComparison.Ordinal));
        // nahe Ebene plus Abstand, in Einheiten der Figur (Hoehe 1)
        var grenze = (Kamera.IchNah + IchSicht.FigurAbstand) / Figur.StadtHoehe;
        var funde = new List<string>();
        var naechste = float.MaxValue;
        var zahl = 0;
        // Sammelliste 453: dieselben Faelle (Aussehen x Bewegungsbild), je Fall fuer sich nebeneinander gerechnet; gesammelt wird danach
        // in derselben Reihenfolge wie nacheinander
        var faelle = (from x in aussehen from b in Bewegungsbilder() select (x.Name, x.Aussehen, x.Kleidung, b.Bewegung, b.Bild)).ToList();
        var ergebnisse = new (float Naechste, string? Fund)[faelle.Count];
        Nebeneinander.Fuer(faelle.Count, i =>
        {
            var (name, a, kleidung, bew, bild) = faelle[i];
            var f = Pruefen(g, name, a, kleidung, bew, bild);
            ergebnisse[i] = (f.Naechste, f.KopfDrin > 0 || f.KopfImKegel > 0 || f.Naechste < grenze || f.HalsOffen > 0
                ? $"{f.Wo}: Kopf {f.KopfDrin} (im Blick {f.KopfImKegel}), naechste Ecke {f.Naechste:0.000} (Grenze {grenze:0.000}), Hals offen {f.HalsOffen}"
                : null);
        });
        foreach (var (n, fund) in ergebnisse)
        {
            zahl++;
            naechste = MathF.Min(naechste, n);
            if (fund is not null)
            {
                funde.Add(fund);
            }
        }
        Assert.True(zahl == aussehen.Count * ((2 * Figur.Bilder) + 4 + Figur.RobbenBilder + 1));
        Assert.True(funde.Count == 0, string.Join("\n", funde.Take(20)));
        Assert.True(naechste >= grenze, $"naechste Ecke {naechste}");
    }

    [Fact]
    public void Gegenprobe_StandVorDemFund_ZeigtKopfBeimRennen()
    {
        // Stand 629d530 (Bild 04 zu P12-183): Schnitt im Bild der Bewegung, Deckel fest; beim Rennen bleiben Kinn, Nacken und
        // Haar unter dem Auge stehen, der Hals ist offen
        var g = SpielGrafikTests.Bibliothek.Value;
        var lang = new PyObjekt { { "frisur", "lang" }, { "bart", "vollbart" } };
        Figur.OhneKopfAltFuerTests = true;
        try
        {
            var rennen = Enumerable.Range(0, Figur.Bilder).Select(i => Pruefen(g, "kurz", new PyObjekt(), null, Figur.Rennen, i)).ToList();
            Assert.Contains(rennen, f => f.KopfImKegel > 0);
            Assert.Contains(rennen, f => f.HalsOffen > 0);
            // langes Haar und Bart: ihre Unterseite liegt unter dem Schnitt und bliebe stehen, schon im Stehen
            Assert.True(Pruefen(g, "lang", lang, null, null, -1).KopfImKegel > 0);
        }
        finally
        {
            Figur.OhneKopfAltFuerTests = false;
        }
        // die Zuordnung zur ganzen Figur greift auch mit dem Versatz: ohne Schnitt (Figur bis 2) faende sie den ganzen Kopf
        Assert.True(Figur.HalsVerschiebung(g, lang, null, Figur.Rennen, 2, IchSicht.KoerperBis).Length() > 0.05f);
        Assert.True(Pruefen(g, "ganz", new PyObjekt(), null, Figur.Rennen, 2, 2f).KopfDrin > 100);
        // neu: dieselben Bilder sauber
        Assert.All(Enumerable.Range(0, Figur.Bilder), i => Assert.Equal(0, Pruefen(g, "kurz", new PyObjekt(), null, Figur.Rennen, i).KopfImKegel));
        Assert.Equal(0, Pruefen(g, "lang", lang, null, null, -1).KopfImKegel);
    }

    [Fact]
    public void AchtRichtungen_MitDerEchtenKamera_KeinKopfImBild()
    {
        // Welt wie die Spielflaeche: Figur gedreht wie IchSicht.FigurDrehung (Koerper schaut mit dem Blick), Auge wie IchSicht.Auge,
        // Bild aus Kamera.Sicht und Kamera.Projektion (PC und Handy); kein Kopf-Dreieck mit einer Ecke im Bild, keine Ecke vor
        // der nahen Ebene plus Abstand
        var g = SpielGrafikTests.Bibliothek.Value;
        var lang = new PyObjekt { { "frisur", "lang" }, { "bart", "vollbart" } };
        var h = Figur.StadtHoehe;
        var faelle = 0;
        foreach (var bild in new[] { -1, 0, 2, 3, 10 })
        {
            var bew = bild < 0 ? null : Figur.Rennen;
            var ohne = Figur.OhneKopf(g, lang, null, bew, bild, IchSicht.KoerperBis);
            var kopf = KopfDreiecke(g, lang, null, bew, bild);
            var v = Figur.HalsVerschiebung(g, lang, null, bew, bild, IchSicht.KoerperBis);
            foreach (var dreh in Enumerable.Range(0, 8).Select(i => i * 45f))
            {
                var figurDreh = IchSicht.DrehungZu(dreh);
                var lage = Matrix4x4.CreateScale(h) * Matrix4x4.CreateRotationY(figurDreh * MathF.PI / 180f) * Matrix4x4.CreateTranslation(3f, 0f, 5f);
                var r = figurDreh * MathF.PI / 180f;
                var auge = new Vector3(3f, h * IchSicht.AugeAnteil, 5f) + (new Vector3(MathF.Sin(r), 0f, MathF.Cos(r)) * IchSicht.AugeVor * h);
                foreach (var neigung in Enumerable.Range(0, 18).Select(i => i * 5f))
                {
                    var k = new Kamera(3f, 5f, 7f) { IchErlaubt = true, Drehung = dreh, IchAuge = auge };
                    k.IchNeigungBereich = (SpielKamera.IchNeigungMin, SpielKamera.IchNeigungMax);
                    k.IchNeigung = neigung;
                    k.IchZiel = 1f;
                    k.IchAnteil = 1f;
                    foreach (var (b, hh) in new[] { (1280f, 800f), (384f, 832f) })
                    {
                        var m = k.Sicht * k.Projektion(b, hh);
                        for (var d = 0; d < (ohne.Indizes.Count / 3) - 12; d++)
                        {
                            var p = Enumerable.Range(0, 3).Select(e => ohne.Ort((int)ohne.Indizes[(d * 3) + e])).ToArray();
                            if (!kopf.Contains(Schluessel(p[0] - v, p[1] - v, p[2] - v)))
                            {
                                continue;
                            }
                            Assert.False(p.Any(q => Sichtbar(Vector4.Transform(new Vector4(Vector3.Transform(q, lage), 1f), m))), $"Kopf im Bild: Bild {bild}, Drehung {dreh}, Neigung {neigung}");
                        }
                        faelle++;
                    }
                    Assert.Equal(auge, k.Auge);
                    var naechste = ohne.Indizes.Distinct().Min(e => Vector3.Distance(Vector3.Transform(ohne.Ort((int)e), lage), k.Auge));
                    Assert.True(naechste >= Kamera.IchNah + IchSicht.FigurAbstand, $"naechste Ecke {naechste} bei Bild {bild}, Drehung {dreh}");
                }
            }
        }
        Assert.Equal(5 * 8 * 18 * 2, faelle);
        // die Figur ohne Kopf bewegt sich wirklich (nicht still die stehende): beim Rennen schwingen die Arme nach vorn
        float VornMax(Netz n) => n.Indizes.Max(i => n.Ort((int)i).Z);
        Assert.True(VornMax(Figur.OhneKopf(g, lang, null, Figur.Rennen, 6, IchSicht.KoerperBis)) > VornMax(Figur.OhneKopf(g, lang, null, null, -1, IchSicht.KoerperBis)) + 0.1f);
        // Gegenprobe der Pruefung selbst: die ganze Figur (mit Kopf) waere beim Blick geradeaus im Bild nicht, nach unten aber
        // laege ihr Kopf um das Auge (Ecken hinter und vor ihm), also findet die Pruefung ihn
        var ganz = Figur.Netz(g, lang, 1f, null, null, -1);
        Assert.Contains(Enumerable.Range(0, ganz.AnzahlEcken), e => ImKegel(ganz.Ort(e), 85f) && ganz.Ort(e).Y > IchSicht.KoerperBis);
    }

    private static bool Sichtbar(Vector4 c) => c.W > 0f && MathF.Abs(c.X) <= c.W && MathF.Abs(c.Y) <= c.W;

    /// <summary>
    /// Welle A 0.93.0 (Weisung P12-303: „in der Ich-Sicht sinkt das Auge mit“): geduckt und kriechend sinkt der Kopf um gut ein
    /// Fuenftel der Figur, das Auge mit ihm. Gegenprobe: bliebe das Auge in Stehhoehe, saesse es ueber dem gesenkten Kopf, und der
    /// Kopf laege im Blick nach unten (Weisung: kein Kopf im Bild).
    /// </summary>
    [Fact]
    public void Haltungen_AugeSinktMit_Gegenprobe_StehhoeheSiehtDenKopf()
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        Assert.InRange(Figur.KopfSenkung(g, Figur.Ducken), 0.15f, 0.35f);
        Assert.InRange(Figur.KopfSenkung(g, Figur.Kriechen), 0.15f, 0.35f);
        var lang = new PyObjekt { { "frisur", "lang" }, { "bart", "vollbart" }, { "haarfarbe", "blond" } };
        foreach (var bew in new[] { Figur.Ducken, Figur.Kriechen })
        {
            var f = Pruefen(g, "lang", lang, null, bew, 0);
            Assert.True(f.KopfImKegel == 0 && f.KopfDrin == 0, f.ToString());
            AugeGesenkt = false;
            try
            {
                // das Auge in Stehhoehe: der Rest des Koerpers (Hals, Kragen) steht unter ihm, aber es waere nicht im Kopf
                var stehend = AugeIn(g, bew);
                Assert.True(stehend.Y - Figur.KopfSenkung(g, bew) < IchSicht.AugeAnteil - 0.15f);
                var ganz = Figur.Netz(g, lang, 1f, null, bew, 0);
                var kopfOben = Enumerable.Range(0, ganz.AnzahlEcken).Max(e => ganz.Ort(e).Y);
                Assert.True(stehend.Y > kopfOben, $"Gegenprobe: Auge {stehend.Y:0.000} nicht ueber dem gesenkten Kopf {kopfOben:0.000}");
            }
            finally
            {
                AugeGesenkt = true;
            }
        }
    }
}
