using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle A 0.51.0 / S 0.20.0 (Weisung P12-84, Nico 1a 2a, Sammelliste 237, Kurzplan P12-53 Abschnitt 2): die Autos der
/// automatischen Strecken waehlen an jeder Abzweigung zufaellig, fest aus Auto-Nummer und Spieltag
/// (<see cref="Autostrecken.RundwegZufall"/>). Gleicher Startwert = gleicher Weg, zwei Autos und zwei Tage verschieden
/// (Gegenproben), Wenden nur in der Sackgasse, mindestens 8 Abzweigungen, kein Auto bleibt stecken ueber 50 Startwerte.
/// Stadt 1 behaelt die festen Strecken (<see cref="StreckenTests.Stadt1_BehaeltDieFestenStrecken_BitGleich"/>).
/// </summary>
public class ZufallStreckenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static List<object?> P(double x, double y) => [x, y];

    private static PyObjekt Leer(double breite = 32, double hoehe = 20) => Aussen.Aus(Aussen.Leer(D.Fassung, "neu_zufall", breite, hoehe));

    private static StadtFassung Fassung(PyObjekt a) => Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", "");

    private static PyObjekt MitKreisverkehr(double radius)
    {
        var a = Leer();
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Kreisverkehr, 12, 10, modell: AussenBearbeitung.Kennung(radius)));
        return a;
    }

    private static List<IReadOnlyList<Vector2>> Gitter() =>
    [
        [new(0, 5), new(40, 5)], [new(0, 15), new(40, 15)], [new(0, 25), new(40, 25)],
        [new(5, 0), new(5, 30)], [new(20, 0), new(20, 30)], [new(35, 0), new(35, 30)],
    ];

    /// <summary>Gitter ohne Sackgassen: geschlossenes Rechteck mit Mittelkreuz.</summary>
    private static List<IReadOnlyList<Vector2>> Rahmen() =>
    [
        [new(5, 5), new(35, 5)], [new(35, 5), new(35, 25)], [new(35, 25), new(5, 25)], [new(5, 25), new(5, 5)],
        [new(5, 15), new(35, 15)], [new(20, 5), new(20, 25)],
    ];

    private static List<IReadOnlyList<Vector2>> Einmuendung() => [[new(0, 10), new(30, 10)], [new(15, 10), new(15, 20)]];

    private static int Start(Autostrecken.Netz netz) =>
        Enumerable.Range(0, netz.Kanten.Count).OrderByDescending(k => netz.Kanten[k].Laenge).ThenBy(k => k).First();

    private static List<(string Name, List<IReadOnlyList<Vector2>> Strassen)> Probestaedte() =>
    [
        ("Gitter", Gitter()), ("Rahmen", Rahmen()), ("Einmuendung", Einmuendung()),
        ("Kreisverkehr 2", Ansicht3D.Bauen(Fassung(MitKreisverkehr(2)), D, Ansicht3D.Einstellung.Standard).Szene.Strassen.ToList()),
        ("Kreisverkehr 3", Ansicht3D.Bauen(Fassung(MitKreisverkehr(3)), D, Ansicht3D.Einstellung.Standard).Szene.Strassen.ToList()),
    ];

    /// <summary>Der Weg ist ein geschlossener Kreis aus erlaubten Folgen; Wende genau dort, wo es nicht weitergeht.</summary>
    private static void PruefeKreis(Autostrecken.Netz netz, List<(int Kante, bool Wende)> weg, string name)
    {
        Assert.NotEmpty(weg);
        for (var i = 0; i < weg.Count; i++)
        {
            var (k, wende) = weg[i];
            var naechste = weg[(i + 1) % weg.Count].Kante;
            var (wege, sackgasse) = Autostrecken.Folgen(netz, k);
            Assert.True(wege.Contains(naechste), $"{name}: Kante {k} fuehrt nicht zu {naechste}");
            Assert.Equal(sackgasse, wende);
            // kein Wenden ohne Sackgasse: gewendet wird nur, wenn keine andere Kante weitergeht
            if (wende)
            {
                Assert.Empty(Autostrecken.Weiter(netz, k));
                Assert.False(netz.Kanten[k].Ring);
            }
        }
    }

    private static int Abzweigungen(Autostrecken.Netz netz, List<(int Kante, bool Wende)> weg) =>
        weg.Count(w => Autostrecken.Folgen(netz, w.Kante).Wege.Count > 1);

    [Fact]
    public void GleicherStartwert_GleicherWeg()
    {
        var netz = Autostrecken.NetzAus(Gitter());
        var start = Start(netz);
        Assert.Equal(Autostrecken.RundwegZufall(netz, start, 3, 17), Autostrecken.RundwegZufall(netz, start, 3, 17));
        var a = Autostrecken.Anlegen(Gitter(), tag: 17);
        var b = Autostrecken.Anlegen(Gitter(), tag: 17);
        Assert.Equal(a.Strecken.Count, b.Strecken.Count);
        Assert.All(a.Strecken.Zip(b.Strecken), p => Assert.Equal(p.First.Spur, p.Second.Spur));
        // derselbe Zufall auf jedem Geraet: feste Zahlen der Folge
        var z = new Autostrecken.Zufall(0, 1);
        var z2 = new Autostrecken.Zufall(0, 1);
        Assert.Equal(Enumerable.Range(0, 5).Select(_ => z.Weiter()), Enumerable.Range(0, 5).Select(_ => z2.Weiter()));
    }

    [Fact]
    public void ZweiAutosMitGleichemStart_NehmenNichtDenselbenWeg_Gegenprobe()
    {
        foreach (var (name, strassen) in new[] { ("Gitter", Gitter()), ("Rahmen", Rahmen()) })
        {
            var netz = Autostrecken.NetzAus(strassen);
            var start = Start(netz);
            for (var tag = 1L; tag <= 10; tag++)
            {
                var wege = Enumerable.Range(0, Autostrecken.HoechstensAutos).Select(nr => Autostrecken.RundwegZufall(netz, start, nr, tag)).ToList();
                for (var i = 0; i < wege.Count; i++)
                {
                    for (var j = i + 1; j < wege.Count; j++)
                    {
                        Assert.False(wege[i].SequenceEqual(wege[j]), $"{name}, Tag {tag}: Auto {i} und {j} fahren denselben Weg");
                    }
                }
            }
        }
        // Gegenprobe: derselbe Startwert (gleiches Auto, gleicher Tag) gibt denselben Weg, die Pruefung oben greift also
        var n = Autostrecken.NetzAus(Gitter());
        Assert.Equal(Autostrecken.RundwegZufall(n, Start(n), 1, 3), Autostrecken.RundwegZufall(n, Start(n), 1, 3));
    }

    [Fact]
    public void AndererTag_AndererWeg()
    {
        var verschieden = 0;
        for (var tag = 1L; tag <= 20; tag++)
        {
            var heute = Autostrecken.Anlegen(Gitter(), tag: tag);
            var morgen = Autostrecken.Anlegen(Gitter(), tag: tag + 1);
            Assert.Equal(heute.Strecken.Count, morgen.Strecken.Count);
            // je Auto ein anderer Weg
            verschieden += heute.Strecken.Zip(morgen.Strecken).Count(p => !p.First.Spur.SequenceEqual(p.Second.Spur));
        }
        Assert.Equal(20 * Autostrecken.HoechstensAutos, verschieden);
        // Gegenprobe: mit dem alten Rundweg reihum faehrt jedes Auto an jedem Tag denselben Weg (Sammelliste 237)
        Autostrecken.ReihumFuerTests = true;
        try
        {
            var alt1 = Autostrecken.Anlegen(Gitter(), tag: 1);
            var alt2 = Autostrecken.Anlegen(Gitter(), tag: 2);
            Assert.All(alt1.Strecken.Zip(alt2.Strecken), p => Assert.Equal(p.First.Spur, p.Second.Spur));
            Assert.NotEmpty(alt1.Strecken);
        }
        finally
        {
            Autostrecken.ReihumFuerTests = false;
        }
    }

    [Fact]
    public void KeinWendenOhneSackgasse_MindestensAchtAbzweigungen_GeschlossenerKreis()
    {
        foreach (var (name, strassen) in Probestaedte())
        {
            var netz = Autostrecken.NetzAus(strassen);
            for (var tag = 1L; tag <= 50; tag++)
            {
                for (var nr = 0; nr < Autostrecken.HoechstensAutos; nr++)
                {
                    var weg = Autostrecken.RundwegZufall(netz, Start(netz), nr, tag);
                    PruefeKreis(netz, weg, $"{name} Tag {tag} Auto {nr}");
                    Assert.True(Abzweigungen(netz, weg) >= Autostrecken.ZufallAbzweigungen, $"{name} Tag {tag} Auto {nr}: zu wenige Abzweigungen");
                    Assert.Equal(Start(netz), weg[0].Kante);
                }
            }
        }
        // ohne Sackgasse wird nie gewendet
        var rahmen = Autostrecken.NetzAus(Rahmen());
        for (var tag = 1L; tag <= 50; tag++)
        {
            Assert.DoesNotContain(Autostrecken.RundwegZufall(rahmen, Start(rahmen), 0, tag), w => w.Wende);
        }
    }

    [Fact]
    public void OhneAbzweigung_DerEineWeg()
    {
        // gerade Strasse: hin, wenden, zurueck, wenden - kein Zufall moeglich
        var netz = Autostrecken.NetzAus([[new(2, 4), new(20, 4)]]);
        var weg = Autostrecken.RundwegZufall(netz, 0, 0, 5);
        Assert.Equal(2, weg.Count);
        Assert.All(weg, w => Assert.True(w.Wende));
        Assert.Equal(weg, Autostrecken.RundwegZufall(netz, 0, 4, 99));
    }

    [Fact]
    public void KeinAutoBleibtStecken_Ueber50Startwerte()
    {
        var staedte = Probestaedte();
        var funde = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(1, 51, tag =>
        {
            foreach (var (name, strassen) in staedte)
            {
                var v = Autostrecken.VerkehrAus(Autostrecken.Anlegen(strassen, tag: tag), null, Verkehr.Abstand);
                if (v.Autos.Count == 0 || Autostrecken.Stecken(v).Count > 0)
                {
                    funde.Add($"{name} Tag {tag}");
                }
            }
        });
        Assert.True(funde.IsEmpty, "stecken: " + string.Join(", ", funde.OrderBy(x => x, StringComparer.Ordinal)));
    }

    [Fact]
    public void Spieltag_ImSpielAusDemSpielstand_ImWerkzeugTag1()
    {
        var a = MitKreisverkehr(2);
        var fassung = Fassung(a);
        var werkzeug = Ansicht3D.Bauen(fassung, D, Ansicht3D.Einstellung.Standard).Szene;
        Assert.Equal(Autostrecken.WerkzeugTag, werkzeug.Spieltag);
        // im Spiel: Tag des Spielstands (der Probe-Spielstand steht auf einem spaeten Tag)
        var stadt = fassung.StadtFuer(fassung.MitKarte(D.Inhalt));
        var stand = StadtbauPruefung.Probestand(stadt.Inhalt).Stand;
        var spiel = StadtSzene.Bauen(stadt, stand, D.Grafik);
        Assert.Equal(stand.Tag, spiel.Spieltag);
        Assert.NotEqual(Autostrecken.WerkzeugTag, spiel.Spieltag);
        // ohne Spielstand: Tag 1
        Assert.Equal(1, StadtSzene.Bauen(stadt, null, D.Grafik).Spieltag);
        // der Verkehr faehrt die Wege seines Tages
        var vw = Verkehr.Fuer(werkzeug);
        var soll = Autostrecken.Anlegen(werkzeug.Strassen, tag: Autostrecken.WerkzeugTag);
        Assert.Equal(soll.Strecken.Select(s => s.Spur), vw.Autos.Select(x => x.Strecke));
        var vs = Verkehr.Fuer(spiel);
        Assert.Equal(Autostrecken.Anlegen(spiel.Strassen, tag: stand.Tag).Strecken.Select(s => s.Spur), vs.Autos.Select(x => x.Strecke));
        // neuer Tag = neuer Verkehr (gleiche Strassen): Passt ist falsch; Gegenprobe gleicher Tag
        Assert.True(vw.Passt(werkzeug));
        Assert.False(vw.Passt(spiel));
        Assert.False(vs.Passt(werkzeug));
    }

    [Fact]
    public void Stadt1_FesteStrecken_PassenAnJedemTag()
    {
        var stadt1 = Ansicht3D.Bauen(D.Fassung, D, Ansicht3D.Einstellung.Standard).Szene;
        var v = Verkehr.Fuer(stadt1);
        Assert.Null(v.FuerTag);
        var stadt = D.Fassung.StadtFuer(D.Fassung.MitKarte(D.Inhalt));
        var spaet = StadtSzene.Bauen(stadt, StadtbauPruefung.Probestand(stadt.Inhalt).Stand, D.Grafik);
        Assert.NotEqual(stadt1.Spieltag, spaet.Spieltag);
        Assert.True(v.Passt(spaet));
        Assert.Equal(v.Autos.Select(x => x.Strecke), Verkehr.Fuer(spaet).Autos.Select(x => x.Strecke));
    }
}
