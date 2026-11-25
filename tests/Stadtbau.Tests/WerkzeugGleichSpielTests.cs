using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle A 1.2.0 (Weisung P12-333 Punkt 4 mit P12-334, Nico: „Das muss ja schon gleich sein zum Spiel“): fuer jeden Innenraum
/// liegen Waende, Tueren und Moebelplaetze im Werkzeug (2D-Plan und 3D) und im Spiel an denselben Stellen (+-0,01). Das Spiel
/// liest wie <c>SpielStart</c> die Inhalte mit den breiten Grundrissen (<see cref="GrundrisseBreit"/>), das Werkzeug ueber
/// <see cref="Spieldaten.Laden"/>. Gegenprobe: das Werkzeug ohne die Datei (2D-Plan wie bis S 0.58.0) weicht ab. Rundlauf:
/// Raum im Werkzeug oeffnen, exportieren, im Spiel laden: gleiche Masse, kein Flur doppelt breiter.
/// </summary>
public class WerkzeugGleichSpielTests
{
    private const double Genau = 0.01;

    private static Spieldaten D => Daten.Spiel;

    private static GrundrisseBreit.Daten Breit => GrundrisseBreit.Lesen(Daten.Lesen(GrundrisseBreit.Datei));

    /// <summary>Inhalt und Innenraeume, wie das Spiel sie laedt (SpielStart: Inhalt mit Moebeln, aktive Fassung, breite Grundrisse).</summary>
    private static readonly Lazy<(PyObjekt Inhalt, PyObjekt Innen)> Spiel = new(() =>
    {
        var breit = Breit;
        var fassung = GrundrisseBreit.Fassung(AktiveFassung.Laden(Daten.Lesen), breit);
        var karte = PythonJson.Utf8(PythonJson.Standard(fassung.Karte));
        var geschaefte = (PyObjekt)Py.Lesen(Daten.Lesen("inhalte_csharp/spiel/geschaefte.json")!)!;
        var inhalt = Geschaefte.NeueMoebelEinfuegen(SpielInhalt.Laden(n => n == "orte" ? karte : Daten.Lesen("inhalte/spiel/" + n + ".json"), null), geschaefte);
        return (GrundrisseBreit.Einsetzen(inhalt, GrundrisseBreit.Inhalt, breit), fassung.Innenraeume);
    });

    /// <summary>Alle Innenraeume als Ansicht (Buero, Filiale, Serverfarm, jede Wohnung, jeder Kunde, jedes Geschaeft, jeder Schalter).</summary>
    private static List<(string Ansicht, object? Wohnung)> Ansichten(PyObjekt inhalt, PyObjekt innen)
    {
        var liste = new List<(string, object?)> { ("buero", null), ("filiale", null), (Innenraum.Serverfarm, null) };
        foreach (var w in SpielDaten.Liste(SpielDaten.Objekt(inhalt["wohnungen"])["wohnungen"]))
        {
            liste.Add(("zuhause", SpielDaten.Objekt(w)["id"]));
        }
        foreach (var k in SpielDaten.Liste(SpielDaten.Objekt(inhalt["kunden"])["orte"]))
        {
            liste.Add(("kunde:" + Py.Str(SpielDaten.Objekt(k)["id"]), null));
        }
        foreach (var ort in InnenGrundriss.Orte(innen))
        {
            liste.Add(((SpielKern.Laeden.Contains(ort) ? "geschaeft:" : "schalter:") + ort, null));
        }
        return liste;
    }

    private static Innenraum? Bauen(string ansicht, object? wohnung, PyObjekt inhalt, PyObjekt innen)
    {
        var stand = StadtbauSpiel.Probestand(inhalt).Stand;
        if (wohnung is not null)
        {
            stand.Wohnung = wohnung;
        }
        return Innenraum.Bauen(ansicht, stand, innen, D.Grafik, D.Geschaefte.Daten);
    }

    /// <summary>Abweichungen zweier gebauter Innenraeume (Grundriss mit Waenden und Tueren, jedes Teil mit Lage und Farbe).</summary>
    private static List<string> Abweichungen(string name, Innenraum? a, Innenraum? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null ? [] : [$"{name}: nur auf einer Seite gebaut"];
        }
        var funde = new List<string>();
        Vergleichen(name + " Grundriss", a.Gebaeude, b.Gebaeude, funde);
        if (a.Teile.Count != b.Teile.Count)
        {
            funde.Add($"{name}: {a.Teile.Count} gegen {b.Teile.Count} Teile");
            return funde;
        }
        for (var i = 0; i < a.Teile.Count; i++)
        {
            Teil s = a.Teile[i], t = b.Teile[i];
            if (s.Art != t.Art || Weit(s.X1, t.X1) || Weit(s.Y1, t.Y1) || Weit(s.Z1, t.Z1) || Weit(s.X2, t.X2) || Weit(s.Y2, t.Y2) || Weit(s.Z2, t.Z2)
                || Weit(s.Farbe.X, t.Farbe.X) || Weit(s.Farbe.Y, t.Farbe.Y) || Weit(s.Farbe.Z, t.Farbe.Z))
            {
                funde.Add($"{name}: Teil {i} {s} gegen {t}");
            }
        }
        // Welle A 1.3.1 (Weisung P12-348 Punkt 2): auch die Modelle (Lage) und die Plaetze zum Sitzen und Liegen (Stelle, Blick)
        // sind gleich; Bilder und ihre Staffelei sind Teile (oben)
        if (a.Stuecke.Count != b.Stuecke.Count)
        {
            funde.Add($"{name}: {a.Stuecke.Count} gegen {b.Stuecke.Count} Modelle");
        }
        else
        {
            for (var i = 0; i < a.Stuecke.Count; i++)
            {
                ModellStueck s = a.Stuecke[i], t = b.Stuecke[i];
                if (s.Name != t.Name || Weit(s.Lage.M41, t.Lage.M41) || Weit(s.Lage.M42, t.Lage.M42) || Weit(s.Lage.M43, t.Lage.M43)
                    || Weit(s.Lage.M11, t.Lage.M11) || Weit(s.Lage.M13, t.Lage.M13) || Weit(s.Lage.M22, t.Lage.M22) || Weit(s.Lage.M33, t.Lage.M33))
                {
                    funde.Add($"{name}: Modell {i} {s.Name} gegen {t.Name}");
                }
            }
        }
        if (a.Sitzgelegenheiten.Count != b.Sitzgelegenheiten.Count)
        {
            funde.Add($"{name}: {a.Sitzgelegenheiten.Count} gegen {b.Sitzgelegenheiten.Count} Sitzplaetze");
        }
        else
        {
            for (var i = 0; i < a.Sitzgelegenheiten.Count; i++)
            {
                Sitzgelegenheit s = a.Sitzgelegenheiten[i], t = b.Sitzgelegenheiten[i];
                if (s.Art != t.Art || s.Modell != t.Modell || Weit(s.Ort.X, t.Ort.X) || Weit(s.Ort.Y, t.Ort.Y) || Weit(s.Ort.Z, t.Ort.Z) || Weit(s.Blick, t.Blick))
                {
                    funde.Add($"{name}: Sitzplatz {i} {s.Modell} {s.Art} {s.Ort} gegen {t.Modell} {t.Art} {t.Ort}");
                }
            }
        }
        return funde;
    }

    private static bool Weit(double a, double b) => Math.Abs(a - b) > Genau;

    /// <summary>Zwei JSON-Werte gleich (Zahlen +-0,01, sonst genau gleich).</summary>
    private static void Vergleichen(string pfad, object? a, object? b, List<string> funde)
    {
        switch (a, b)
        {
            case (PyObjekt oa, PyObjekt ob):
                var schluessel = oa.Select(p => p.Key).Union(ob.Select(p => p.Key)).ToList();
                foreach (var s in schluessel)
                {
                    Vergleichen(pfad + "." + s, oa[s], ob[s], funde);
                }
                break;
            case (List<object?> la, List<object?> lb):
                if (la.Count != lb.Count)
                {
                    funde.Add($"{pfad}: {la.Count} gegen {lb.Count} Eintraege");
                    break;
                }
                for (var i = 0; i < la.Count; i++)
                {
                    Vergleichen(pfad + "[" + i + "]", la[i], lb[i], funde);
                }
                break;
            case (long or double, long or double):
                if (Weit(Py.F(a), Py.F(b)))
                {
                    funde.Add($"{pfad}: {Py.F(a)} gegen {Py.F(b)}");
                }
                break;
            default:
                if (PythonJson.Text(a) != PythonJson.Text(b))
                {
                    funde.Add($"{pfad}: {PythonJson.Text(a)} gegen {PythonJson.Text(b)}");
                }
                break;
        }
    }

    [Fact]
    public void JederInnenraum_Werkzeug2DUnd3D_GleichSpiel_GegenprobeOhneDatei()
    {
        var (spielInhalt, spielInnen) = Spiel.Value;
        var p = Projekt.Neu("Werkzeug gleich Spiel", D, Daten.NeueAblage(), "moebelhaus");
        var werkzeugInnen = p.Innenraeume();
        var ansichten = Ansichten(spielInhalt, spielInnen);
        Assert.True(ansichten.Count >= 15, $"zu wenige Innenraeume: {ansichten.Count}");
        var funde = new List<string>();
        var gebaut = 0;
        // Laeden und Schalter baut Nico im Werkzeug: das Spiel zeigt sie aus der Datei, die das Werkzeug schreibt (geladen wie im
        // Spiel); alle anderen Innenraeume aus der Fassung, die heute im Spiel ist (aktiv.json)
        var ausWerkzeug = GrundrisseBreit.Fassung(StadtFassung.Lesen(p.Stand("2026-11-21T12:00:00+01:00").Schreiben()), Breit).Innenraeume;
        foreach (var (ansicht, wohnung) in ansichten)
        {
            var name = ansicht + (wohnung is null ? "" : "/" + wohnung);
            var laden = Innenraum.OrtFuer(ansicht) is { } o && (SpielKern.Laeden.Contains(o) || SpielKern.SchalterOrte.Contains(o));
            var spiel = Bauen(ansicht, wohnung, spielInhalt, laden ? ausWerkzeug : spielInnen);
            // Werkzeug 3D: derselbe Bau aus den Daten des Werkzeugs (Probestand, Innenraeume des Projekts)
            var w3d = Bauen(ansicht, wohnung, D.Inhalt, werkzeugInnen);
            funde.AddRange(Abweichungen(name + " 3D", w3d, spiel));
            gebaut += spiel is null ? 0 : 1;
            // Werkzeug 2D: der Plan (Arbeitsort bzw. Laden/Schalter), Grundriss wie der Spiel-Innenraum ihn baut
            var ort = Innenraum.OrtFuer(ansicht)!;
            if (ort is not (SpielDaten.OrtZuhause or SpielDaten.OrtFiliale or Innenraum.Serverfarm) && spiel is not null)
            {
                var plan = SpielKern.Laeden.Contains(ort) || SpielKern.SchalterOrte.Contains(ort) ? p.Grundriss(ort) : p.ArbeitsortGrundriss(ort, 0);
                Assert.NotNull(plan);
                var spielPlan = Innenraum.ArbeitsGrundriss(ort, 0, spielInhalt, laden ? ausWerkzeug : spielInnen);
                Vergleichen(name + " 2D", Innenraum.ArbeitsGrundriss(ort, 0, D.Inhalt, werkzeugInnen), spielPlan, funde);
                Vergleichen(name + " 2D-Plan", plan, spielPlan, funde);
            }
        }
        Assert.True(gebaut >= 15, $"zu wenige gebaut: {gebaut}");
        Assert.Empty(funde);

        // Gegenprobe: das Werkzeug ohne die breiten Grundrisse (Stand bis S 0.58.0) zeigt Buero und Wohnung anders als das Spiel
        var alt = Spieldaten.Laden(pfad => pfad == GrundrisseBreit.Datei ? null : Daten.Lesen(pfad));
        var altFunde = new List<string>();
        Vergleichen("buero 2D", Innenraum.ArbeitsGrundriss(SpielDaten.OrtBuero, 0, alt.Inhalt, alt.Fassung.Innenraeume),
            Innenraum.ArbeitsGrundriss(SpielDaten.OrtBuero, 0, spielInhalt, spielInnen), altFunde);
        Assert.Contains(altFunde, f => f.StartsWith("buero 2D.flur.h", StringComparison.Ordinal));
        var wohnung0 = SpielDaten.Objekt(SpielDaten.Liste(SpielDaten.Objekt(spielInhalt["wohnungen"])["wohnungen"])[0])["id"];
        Assert.NotEmpty(Abweichungen("zuhause alt", Bauen("zuhause", wohnung0, alt.Inhalt, alt.Fassung.Innenraeume), Bauen("zuhause", wohnung0, spielInhalt, spielInnen)));
        Assert.NotEmpty(Abweichungen("serverfarm alt", Bauen(Innenraum.Serverfarm, null, alt.Inhalt, alt.Fassung.Innenraeume), Bauen(Innenraum.Serverfarm, null, spielInhalt, spielInnen)));
    }

    [Fact]
    public void Rundlauf_OeffnenExportierenImSpielLaden_GleicheMasse_KeinFlurDoppeltBreiter()
    {
        var breit = Breit;
        var p = Projekt.Neu("Rundlauf", D, Daten.NeueAblage(), "moebelhaus");
        // unveraendert: genau der Stand, den der Export schreiben wuerde (ohne Aenderung sperrt der Knopf)
        var unveraendert = StadtFassung.Lesen(p.Stand("2026-11-21T12:00:00+01:00").Schreiben());
        // mit einer kleinen Aenderung im Laden: der echte Export
        p.Aendern("moebelhaus", "Boden", g => Bearbeitung.Boden(g, "flur", "teppich"));
        var export = StadtFassung.Lesen(p.Exportieren(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.FromHours(1))));
        foreach (var (name, datei) in new[] { ("unveraendert", unveraendert), ("export", export) })
        {
            // im Werkzeug stehen die breiten Grundrisse schon in der Datei: das Spiel setzt nichts ein (nichts passt mehr)
            Assert.Equal(0, GrundrisseBreit.Passend(datei.Innenraeume, GrundrisseBreit.Innenraeume, breit));
            var imSpiel = GrundrisseBreit.Fassung(datei, breit);
            Assert.Equal(GrundrisseBreit.Pruefsumme(datei.Innenraeume), GrundrisseBreit.Pruefsumme(imSpiel.Innenraeume));
            // Serverfarm: Flur so breit wie im Werkzeug, nicht doppelt
            var farmWerkzeug = SpielDaten.Objekt(SpielDaten.Objekt(SpielDaten.Objekt(D.Fassung.Innenraeume[Innenraum.Serverfarm])["gebaeude"])["flur"]);
            var farmSpiel = SpielDaten.Objekt(SpielDaten.Objekt(SpielDaten.Objekt(imSpiel.Innenraeume[Innenraum.Serverfarm])["gebaeude"])["flur"]);
            Assert.True(Math.Abs(Py.F(farmWerkzeug["h"]) - Py.F(farmSpiel["h"])) <= Genau, name);
            foreach (var ort in InnenGrundriss.Orte(imSpiel.Innenraeume))
            {
                var funde = new List<string>();
                Vergleichen(name + " " + ort, p.Grundriss(ort), InnenGrundriss.Grundriss(imSpiel.Innenraeume, ort), funde);
                Assert.True(funde.Count == 0 || (name == "unveraendert" && ort == "moebelhaus"), string.Join("\n", funde));
            }
        }
        // die alte Fassung (ohne Werkzeug) wird im Spiel genau einmal breit: Serverfarm vorher schmaler, danach wie im Werkzeug
        var alt = AktiveFassung.LadenNummer(Daten.Lesen, Spieldaten.BasisFassung);
        var farmAlt = SpielDaten.Objekt(SpielDaten.Objekt(SpielDaten.Objekt(alt.Innenraeume[Innenraum.Serverfarm])["gebaeude"])["flur"]);
        var farmNeu = SpielDaten.Objekt(SpielDaten.Objekt(SpielDaten.Objekt(D.Fassung.Innenraeume[Innenraum.Serverfarm])["gebaeude"])["flur"]);
        Assert.True(Py.F(farmNeu["h"]) > Py.F(farmAlt["h"]) + 0.5);
        var zweimal = GrundrisseBreit.Fassung(GrundrisseBreit.Fassung(alt, breit), breit);
        Assert.Equal(GrundrisseBreit.Pruefsumme(D.Fassung.Innenraeume), GrundrisseBreit.Pruefsumme(zweimal.Innenraeume));
    }

    /// <summary>
    /// Welle S 0.60.0 (Weisung P12-337 Punkt 2): die Raeume des Spiels, die das Werkzeug jetzt zeigt (jede Wohnung, Buero der
    /// Bitweiche, eigene Firma jeder Stufe, Serverfarm, Zweigstelle jeder Stufe, jeder Kunde), liegen im Werkzeug-Grundriss
    /// (2D), im Werkzeug-3D und im Spiel an denselben Stellen (+-0,01). Das Spiel baut hier mit einem eigenen Spielstand (nicht
    /// ueber <see cref="SpielRaeume.Stand"/>), damit der Test nicht nur die Regel des Werkzeugs mit sich selbst vergleicht.
    /// Gegenprobe: das Werkzeug ohne die breiten Grundrisse weicht in Wohnung, Bitweiche, Firma, Zweigstelle und Serverfarm ab.
    /// </summary>
    [Fact]
    public void SpielRaeume_Werkzeug2DUnd3D_GleichSpiel_GegenprobeOhneDatei()
    {
        var (spielInhalt, spielInnen) = Spiel.Value;
        var p = Projekt.Neu("Spiel-Raeume", D, Daten.NeueAblage(), "moebelhaus");
        var raeume = p.SpielRaeumeListe();
        // Abnahmesatz: alle Wohnungsarten, Bitweiche, eigene Firma (5 Stufen), Serverfarm, Zweigstelle (2 Stufen), alle Kunden
        var wohnungen = SpielDaten.Liste(SpielDaten.Objekt(spielInhalt["wohnungen"])["wohnungen"]).Select(w => Py.Str(SpielDaten.Objekt(w)["id"])).ToList();
        var kunden = SpielDaten.Liste(SpielDaten.Objekt(spielInhalt["kunden"])["orte"]).Select(k => Py.Str(SpielDaten.Objekt(k)["id"])).ToList();
        var erwartet = wohnungen.Select(w => "spiel:zuhause:" + w)
            .Concat(["spiel:buero:bitweiche", "spiel:buero:firma1", "spiel:buero:firma2", "spiel:buero:firma3", "spiel:buero:firma4", "spiel:buero:firma5",
                "spiel:serverfarm", "spiel:filiale1", "spiel:filiale2"])
            .Concat(kunden.Select(k => "spiel:kunde:" + k)).ToList();
        Assert.Equal(erwartet, raeume.Select(r => r.Schluessel).ToList());
        Assert.Equal(4, wohnungen.Count);

        Innenraum? ImSpiel(SpielRaeume.Raum r)
        {
            var stand = StadtbauSpiel.Probestand(spielInhalt).Stand;
            switch (r.Ansicht)
            {
                case "zuhause":
                    stand.Wohnung = r.Wohnung;
                    break;
                case "buero" when r.Schluessel.EndsWith(":bitweiche", StringComparison.Ordinal):
                    stand.Firma = null;
                    break;
                case "buero":
                    stand.Firma = new PyObjekt { { "stufe", long.Parse(r.Schluessel[^1..], System.Globalization.CultureInfo.InvariantCulture) }, { "name", "Probe" }, { "tag", 1L } };
                    break;
                case "filiale":
                    stand.Zweigstelle = new PyObjekt { { "stufe", long.Parse(r.Schluessel[^1..], System.Globalization.CultureInfo.InvariantCulture) }, { "tag", 1L } };
                    break;
            }
            return Innenraum.Bauen(r.Ansicht, stand, spielInnen, D.Grafik, D.Geschaefte.Daten);
        }

        var funde = new List<string>();
        var gebaut = new Dictionary<string, Innenraum>();
        foreach (var r in raeume)
        {
            var spiel = ImSpiel(r);
            Assert.True(spiel is not null, r.Schluessel + ": im Spiel nicht gebaut");
            gebaut[r.Schluessel] = spiel!;
            // Werkzeug 3D (Bau ueber das Projekt) und Werkzeug 2D (der Grundriss, den die Flaeche zeichnet, und die Personen)
            funde.AddRange(Abweichungen(r.Schluessel + " 3D", p.SpielRaum(r.Schluessel), spiel));
            Vergleichen(r.Schluessel + " 2D", p.Grundriss(r.Schluessel), spiel!.Gebaeude, funde);
            var personen = p.Personen(r.Schluessel);
            if (personen.Count != spiel.Personen.Count)
            {
                funde.Add($"{r.Schluessel} 2D: {personen.Count} gegen {spiel.Personen.Count} Personen");
            }
            for (var i = 0; i < Math.Min(personen.Count, spiel.Personen.Count); i++)
            {
                var pl = (List<object?>)personen[i]["platz"]!;
                if (Weit(Py.F(pl[0]), spiel.Personen[i].X) || Weit(Py.F(pl[1]), spiel.Personen[i].Z))
                {
                    funde.Add($"{r.Schluessel} 2D: Person {spiel.Personen[i].Id} an anderer Stelle");
                }
            }
        }
        Assert.Empty(funde);
        // die Bitweiche ist wirklich ein anderes Haus als die eigene Firma (sonst waere der Vergleich oben leer)
        Assert.NotEmpty(Abweichungen("bitweiche gegen firma5", gebaut["spiel:buero:bitweiche"], gebaut["spiel:buero:firma5"]));

        // Werkzeug 3D ueber den Weg der Oberflaeche (Innen3D, mit dem Spiel der 3D-Ansicht) fuer die Raeume der Bilder
        Innen3D.Ergebnis? vorher = null;
        foreach (var s in new[] { "spiel:zuhause:" + wohnungen[0], "spiel:buero:bitweiche", "spiel:serverfarm" })
        {
            vorher = Innen3D.Bauen(Innen3D.Vorbereiten(p, s), vorher);
            Assert.Empty(Abweichungen(s + " Innen3D", vorher.Innen, gebaut[s]));
        }

        // Gegenprobe: das Werkzeug ohne die breiten Grundrisse (Stand bis S 0.58.0) zeigt diese Raeume anders als das Spiel
        var alt = Spieldaten.Laden(pfad => pfad == GrundrisseBreit.Datei ? null : Daten.Lesen(pfad));
        var pAlt = Projekt.Neu("Spiel-Raeume alt", alt, Daten.NeueAblage(), "moebelhaus");
        foreach (var s in new[] { "spiel:zuhause:" + wohnungen[0], "spiel:buero:bitweiche", "spiel:buero:firma5", "spiel:filiale1", "spiel:serverfarm" })
        {
            Assert.True(Abweichungen(s + " alt", pAlt.SpielRaum(s), gebaut[s]).Count > 0, s + ": Gegenprobe ohne Abweichung");
        }
    }

    /// <summary>
    /// Welle S 0.60.0 (Weisung P12-337 Punkt 3) mit S 0.64.0 (Weisung P12-381, Nico 2c): Raeume des Spiels ansehen und zum
    /// Aendern oeffnen aendert nichts an der Stadt-Datei und am Projekt (bitgleich, keiner der Schluessel steht darin, Projekt
    /// bleibt Format 3). In einer leeren Stadt lehnt das Projekt jede Aenderung ab (das Spiel nimmt die Raeume nur aus Stadt 1).
    /// </summary>
    [Fact]
    public void SpielRaeume_OhneAenderung_ExportUnveraendert_LeereStadtNurAnsehen()
    {
        var p = Projekt.Neu("Spiel-Raeume ansehen", D, Daten.NeueAblage(), "moebelhaus");
        var vorher = p.Stand("2026-11-21T12:00:00+01:00").Schreiben();
        var projektVorher = p.Schreiben();
        foreach (var r in p.SpielRaeumeListe())
        {
            Assert.NotNull(p.SpielRaum(r.Schluessel));
            Assert.NotNull(p.Grundriss(r.Schluessel));
            Assert.False(p.GibtInnenort(r.Schluessel));
            Assert.True(p.SpielRaumBearbeiten(r.Schluessel));
        }
        Assert.Equal(0, p.SchritteZurueck);
        Assert.Empty(p.GeaenderteSpielRaeume);
        Assert.DoesNotContain(p.Innenorte, o => SpielRaeume.Ist(o));
        var nachher = p.Stand("2026-11-21T12:00:00+01:00").Schreiben();
        Assert.Equal(vorher, nachher);
        Assert.Equal(projektVorher, p.Schreiben());
        Assert.DoesNotContain("spiel:", System.Text.Encoding.UTF8.GetString(nachher), StringComparison.Ordinal);
        Assert.Contains("\"formatversion\": 3,", System.Text.Encoding.UTF8.GetString(p.Schreiben()), StringComparison.Ordinal);

        var leer = Projekt.Neu("Leere Stadt", D, Daten.NeueAblage(), leer: (32, 24));
        foreach (var r in leer.SpielRaeumeListe())
        {
            Assert.False(leer.SpielRaumBearbeiten(r.Schluessel));
            Assert.Throws<InvalidOperationException>(() => leer.Aendern(r.Schluessel, "Boden", g => Bearbeitung.Boden(g, "flur", "teppich")));
        }
        Assert.Equal(0, leer.SchritteZurueck);
    }

    /// <summary>Wohnung „apartment“: Wand zwischen Wohnraum und Kueche um 0,5 nach links (Wohnraum kleiner, Kueche groesser).</summary>
    internal static void WandZiehen(PyObjekt g)
    {
        var raeume = ((List<object?>)g["raeume"]!).OfType<PyObjekt>().ToList();
        var wohn = raeume.First(r => Py.Str(r["id"]) == "wohnraum");
        var kueche = raeume.First(r => Py.Str(r["id"]) == "kueche");
        Py.Setzen(wohn, "w", Py.F(wohn["w"]) - 0.5);
        Py.Setzen(kueche, "x", Py.F(kueche["x"]) - 0.5);
        Py.Setzen(kueche, "w", Py.F(kueche["w"]) + 0.5);
    }

    /// <summary>
    /// Welle S 0.64.0 / A 1.10.0 (Weisung P12-381, Abnahme A3 „Werkzeug = Spiel 1:1“): geaenderte Raeume (Wohnung: Wand
    /// gezogen; Bitweiche und Serverfarm: Tuer verschoben) exportieren, im Spiel einsetzen wie <c>SpielStart</c> (Ersatz an der
    /// Quelle) und bauen: Waende, Tueren, Moebel und Personen im Spiel gleich dem Werkzeug in 2D und 3D. Gegenprobe: das Spiel
    /// ohne den Teil „spielraeume“ weicht ab. Nur die geaenderten Raeume stehen in der Datei.
    /// </summary>
    [Fact]
    public void SpielRaeume_Geaendert_ExportIneSpiel_WerkzeugGleichSpiel_MitGegenprobe()
    {
        var (spielInhalt, spielInnen) = Spiel.Value;
        var p = Projekt.Neu("Spiel-Raeume aendern", D, Daten.NeueAblage(), "moebelhaus");
        p.Aendern("spiel:zuhause:apartment", "Wand ziehen", WandZiehen);
        foreach (var s in new[] { "spiel:buero:bitweiche", "spiel:serverfarm" })
        {
            p.Aendern(s, "Tür setzen", g =>
            {
                // die erste Tuer zur Mitte der Raumwand hin um 0,5 verschieben (sie bleibt auf der Wand zum Flur)
                var r = ((List<object?>)g["raeume"]!).OfType<PyObjekt>().First(r => r["tuer"] is PyObjekt t && t.Count > 0
                    && Py.F(t["von"]) >= Py.F(r["x"]) && Py.F(t["bis"]) <= Py.F(r["x"]) + Py.F(r["w"]));
                var t = (PyObjekt)r["tuer"]!;
                var schritt = (Py.F(t["von"]) + Py.F(t["bis"])) / 2 < Py.F(r["x"]) + (Py.F(r["w"]) / 2) ? 0.5 : -0.5;
                Py.Setzen(t, "von", Py.F(t["von"]) + schritt);
                Py.Setzen(t, "bis", Py.F(t["bis"]) + schritt);
            });
        }
        var geaendert = new[] { "spiel:zuhause:apartment", "spiel:buero:bitweiche", "spiel:serverfarm" };
        Assert.Equal(geaendert, p.GeaenderteSpielRaeume);
        var datei = p.Exportieren(new DateTimeOffset(2026, 11, 25, 17, 0, 0, TimeSpan.FromHours(1)));
        var f = StadtFassung.Lesen(datei);
        Assert.Contains("\"formatversion\": 5,", System.Text.Encoding.UTF8.GetString(datei), StringComparison.Ordinal);
        var stuecke = SpielRaumErsatz.Lesen(f.Stadtdaten);
        Assert.Equal(geaendert.Select(Projekt.SpielRaumKennung), stuecke.Select(s => s.Raum));
        Assert.Empty(StadtbauPruefung.Inhalt(f, D.Inhalt));
        Assert.Empty(SpielRaumErsatz.Unpassend(stuecke, spielInhalt, spielInnen));

        // Spiel: Ersatz wie SpielStart (Inhalt an der Quelle, Serverfarm in den Innenraeumen der Fassung)
        var inhalt = SpielRaumErsatz.Inhalt(spielInhalt, stuecke);
        var innen = SpielRaumErsatz.Innenraeume(spielInnen, stuecke);
        var funde = new List<string>();
        foreach (var r in p.SpielRaeumeListe())
        {
            var spiel = ImSpiel(r, inhalt, innen);
            funde.AddRange(Abweichungen(r.Schluessel + " 3D", p.SpielRaum(r.Schluessel), spiel));
            if (geaendert.Contains(r.Schluessel))
            {
                Assert.True(Abweichungen(r.Schluessel + " Gegenprobe", ImSpiel(r, spielInhalt, spielInnen), spiel).Count > 0, r.Schluessel + ": Gegenprobe ohne Abweichung");
            }
        }
        Assert.Empty(funde);
        var innen3D = Innen3D.Bauen(Innen3D.Vorbereiten(p, "spiel:zuhause:apartment"), null);
        Assert.Empty(Abweichungen("apartment Innen3D", innen3D.Innen, ImSpiel(p.SpielRaeumeListe()[0], inhalt, innen)));
    }

    /// <summary>Den Raum bauen, wie das Spiel ihn beim Betreten baut (eigener Weg ueber den Spielstand, nicht ueber das Werkzeug).</summary>
    private static Innenraum? ImSpiel(SpielRaeume.Raum r, PyObjekt inhalt, PyObjekt innen)
    {
        var stand = StadtbauSpiel.Probestand(inhalt).Stand;
        switch (r.Ansicht)
        {
            case "zuhause":
                stand.Wohnung = r.Wohnung;
                break;
            case "buero" when r.Schluessel.EndsWith(":bitweiche", StringComparison.Ordinal):
                stand.Firma = null;
                break;
            case "buero":
                stand.Firma = new PyObjekt { { "stufe", long.Parse(r.Schluessel[^1..], System.Globalization.CultureInfo.InvariantCulture) }, { "name", "Probe" }, { "tag", 1L } };
                break;
            case "filiale":
                stand.Zweigstelle = new PyObjekt { { "stufe", long.Parse(r.Schluessel[^1..], System.Globalization.CultureInfo.InvariantCulture) }, { "tag", 1L } };
                break;
        }
        return Innenraum.Bauen(r.Ansicht, stand, innen, D.Grafik, D.Geschaefte.Daten);
    }
}
