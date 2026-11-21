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
}
