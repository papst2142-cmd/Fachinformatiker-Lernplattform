using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.2.0 (Weisung P12-334, Nico: Weg B „Neue Grundrisse als Daten“): die Datei <c>inhalte_csharp/spiel/grundrisse_breit.json</c>
/// ist genau das Ergebnis von <see cref="GrundrisseBreit.Erzeugen"/> aus den alten Grundrissen (Python v0.63.2 und innenraeume.json);
/// sie passt vollstaendig auf die alten Daten, ein zweites Einsetzen aendert nichts (kein Flur doppelt breiter), ein von Nico
/// geaenderter Raum bleibt, und die Ladepruefung findet in den breiten Inhalten nichts. Neu schreiben (nur nach einer Aenderung der
/// Rechnung, nie von Hand): <c>LERNPLATTFORM_GRUNDRISSE_SCHREIBEN=1</c>.
/// </summary>
public sealed class GrundrisseBreitTests
{
    private static string Pfad => Path.Combine(Pfade.Wurzel, "inhalte_csharp", "spiel", "grundrisse_breit.json");

    /// <summary>Die Daten aus der Datei im Repo.</summary>
    internal static readonly Lazy<GrundrisseBreit.Daten> Daten = new(() => GrundrisseBreit.Lesen(File.Exists(Pfad) ? File.ReadAllBytes(Pfad) : null));

    private static GrundrisseBreit.Daten Erzeugt() =>
        GrundrisseBreit.Erzeugen(SpielInhaltTests.InhaltPython, SpielInnenraumTests.InnenraeumeAlt.Value);

    [Fact]
    public void Datei_GenauDasErzeugte_PasstGanzAufDieAltenDaten()
    {
        var text = GrundrisseBreit.Schreiben(Erzeugt());
        if (Environment.GetEnvironmentVariable("LERNPLATTFORM_GRUNDRISSE_SCHREIBEN") == "1")
        {
            File.WriteAllText(Pfad, text, new System.Text.UTF8Encoding(false));
        }
        Assert.True(File.Exists(Pfad), "grundrisse_breit.json fehlt");
        Assert.Equal(text, File.ReadAllText(Pfad));
        var d = Daten.Value;
        var inhalt = d.Ersatz.Count(e => e.Bereich == GrundrisseBreit.Inhalt);
        var innen = d.Ersatz.Count(e => e.Bereich == GrundrisseBreit.Innenraeume);
        Assert.True(inhalt >= 20 && innen >= 1, $"zu wenige Ersatzstuecke: {inhalt} / {innen}");
        // jedes Stueck passt auf die alten Daten, nach dem Einsetzen keines mehr (zweites Einsetzen aendert nichts)
        Assert.Equal(inhalt, GrundrisseBreit.Passend(SpielInhaltTests.InhaltPython, GrundrisseBreit.Inhalt, d));
        Assert.Equal(innen, GrundrisseBreit.Passend(SpielInnenraumTests.InnenraeumeAlt.Value, GrundrisseBreit.Innenraeume, d));
        Assert.Equal(0, GrundrisseBreit.Passend(SpielInhaltTests.Inhalt, GrundrisseBreit.Inhalt, d));
        Assert.Equal(0, GrundrisseBreit.Passend(SpielInnenraumTests.Innenraeume.Value, GrundrisseBreit.Innenraeume, d));
        Assert.Equal(Py.Pruefsumme(SpielInhaltTests.Inhalt), Py.Pruefsumme(GrundrisseBreit.Einsetzen(SpielInhaltTests.Inhalt, GrundrisseBreit.Inhalt, d)));
        // die alten Daten bleiben unveraendert (Einsetzen arbeitet auf einer Kopie)
        Assert.Equal(2L, Py.Int(SpielDaten.Objekt(SpielDaten.Objekt(SpielInhaltTests.InhaltPython["gebaeude"])["flur"])["h"]));
        Assert.Equal(3L, Py.Int(SpielDaten.Objekt(SpielDaten.Objekt(SpielInhaltTests.Inhalt["gebaeude"])["flur"])["h"]));
        // Zahlen behalten ihren Python-Typ (ganze Zahl bleibt ganze Zahl, json 3 statt 3.0; Gegenprobe Kommazahl bleibt Kommazahl)
        Assert.IsType<long>(SpielDaten.Objekt(SpielDaten.Objekt(SpielInhaltTests.Inhalt["gebaeude"])["flur"])["h"]);
        var farm = SpielDaten.Objekt(SpielDaten.Objekt(SpielInnenraumTests.Innenraeume.Value["serverfarm"])["gebaeude"]);
        Assert.IsType<long>(farm["hoehe"]);
        Assert.IsType<double>(SpielDaten.Objekt(farm["flur"])["h"]);
        // alle vier Wohnungen haben ihre Umrechnung fuer die Spielstaende
        Assert.Equal(SpielDaten.Liste(SpielDaten.Objekt(SpielInhaltTests.InhaltPython["wohnungen"])["wohnungen"]).Count, d.Wohnungen.Count);
        Assert.All(d.Wohnungen.Values, u => Assert.True(u.Zuwachs > 0));
    }

    [Fact]
    public void GeaenderterRaum_Bleibt_ZweitesEinsetzenAendertNichts_Gegenprobe()
    {
        var d = Daten.Value;
        // Nico hat die Bitweiche im Werkzeug geaendert: der Ersatz greift nicht, sein Grundriss bleibt
        var geaendert = (PyObjekt)Py.Kopie(SpielInhaltTests.InhaltPython)!;
        Py.Setzen(SpielDaten.Objekt(geaendert["gebaeude"]), "breite", 31L);
        var ergebnis = GrundrisseBreit.Einsetzen(geaendert, GrundrisseBreit.Inhalt, d);
        Assert.Equal(31L, Py.Int(SpielDaten.Objekt(ergebnis["gebaeude"])["breite"]));
        Assert.Equal(2L, Py.Int(SpielDaten.Objekt(SpielDaten.Objekt(ergebnis["gebaeude"])["flur"])["h"]));
        // die anderen Orte werden trotzdem breit
        var w = SpielDaten.Objekt(SpielDaten.Liste(SpielDaten.Objekt(ergebnis["wohnungen"])["wohnungen"])[0]);
        Assert.Equal(2.2, Py.F(SpielDaten.Objekt(SpielDaten.Objekt(w["gebaeude"])["flur"])["h"]), 6);
        // Gegenprobe: ohne Datei bleibt alles wie in Python
        Assert.Equal(Py.Pruefsumme(SpielInhaltTests.InhaltPython),
            Py.Pruefsumme(GrundrisseBreit.Einsetzen(SpielInhaltTests.InhaltPython, GrundrisseBreit.Inhalt, GrundrisseBreit.Daten.Leer)));
        // neuere Formatnummer = klarer Fehler
        Assert.Throws<InvalidDataException>(() => GrundrisseBreit.Lesen("""{"format": 2, "wohnungen": {}, "ersatz": []}"""u8.ToArray()));
    }

    /// <summary>
    /// Die Ladepruefung (wie beim Start des Spiels) findet in den breiten Inhalten nichts, und jede Stelle eines Auftrags liegt im
    /// breiten Grundriss im selben Raum an derselben Stelle des Raums wie im alten (Abstand zur Raumecke gleich, +-0,01).
    /// Gegenprobe: breite Grundrisse ohne umgerechnete Stellen ergeben Abweichungen (die Ladepruefung prueft keine Lage).
    /// </summary>
    [Fact]
    public void Ladepruefung_BreiteInhalte_OhneFunde_StellenImRaum_Gegenprobe()
    {
        Assert.Equal("[]", PythonJson.Text(SpielPruefung.Pruefen((PyObjekt)Py.Kopie(SpielInhaltTests.Inhalt)!, SpielPruefungTests.Lernen).Cast<object?>().ToList()));
        var (anzahl, falsch) = StellenVergleich(SpielInhaltTests.InhaltPython, SpielInhaltTests.Inhalt);
        Assert.True(anzahl >= 20, $"zu wenige Stellen: {anzahl}");
        Assert.Empty(falsch);
        var ohneStellen = new GrundrisseBreit.Daten([.. Daten.Value.Ersatz.Where(e => e.Pfad[0] is not ("aufgaben" or "zwischenfaelle"))], Daten.Value.Wohnungen);
        var halb = GrundrisseBreit.Einsetzen(SpielInhaltTests.InhaltPython, GrundrisseBreit.Inhalt, ohneStellen);
        Assert.NotEmpty(StellenVergleich(SpielInhaltTests.InhaltPython, halb).Falsch);
    }

    /// <summary>Alle Grundrisse eines Inhalts in fester Reihenfolge (Bitweiche, Kunden, Wohnungen, Firma und Filiale je Stufe).</summary>
    private static List<PyObjekt> Grundrisse(PyObjekt inhalt)
    {
        var grundrisse = new List<PyObjekt> { SpielDaten.Objekt(inhalt["gebaeude"]) };
        foreach (var o in SpielDaten.Liste(SpielDaten.Objekt(inhalt["kunden"])["orte"]))
        {
            if (SpielDaten.Objekt(o)["gebaeude"] is PyObjekt g) grundrisse.Add(g);
        }
        foreach (var w in SpielDaten.Liste(SpielDaten.Objekt(inhalt["wohnungen"])["wohnungen"]))
        {
            if (SpielDaten.Objekt(w)["gebaeude"] is PyObjekt g) grundrisse.Add(g);
        }
        foreach (var teil in new[] { "gebaeude", "filiale" })
        {
            if (SpielDaten.Objekt(inhalt["firma"])[teil] is PyObjekt f && f["stufen"] is List<object?> stufen)
            {
                grundrisse.AddRange(stufen.OfType<PyObjekt>().Select(st => st["gebaeude"]).OfType<PyObjekt>());
            }
        }
        return grundrisse;
    }

    /// <summary>
    /// Stellen der Auftraege, die im alten Inhalt in ihrem Raum liegen: Anzahl und die, die im neuen nicht im selben Raum an
    /// derselben Stelle des Raums liegen.
    /// </summary>
    private static (int Anzahl, List<string> Falsch) StellenVergleich(PyObjekt alt, PyObjekt neu)
    {
        var ga = Grundrisse(alt);
        var gn = Grundrisse(neu);
        Assert.Equal(ga.Count, gn.Count);
        var anzahl = 0;
        var falsch = new List<string>();
        foreach (var liste in new[] { "aufgaben", "zwischenfaelle" })
        {
            var la = alt[liste] as List<object?> ?? [];
            var ln = neu[liste] as List<object?> ?? [];
            for (var i = 0; i < la.Count; i++)
            {
                if (la[i] is not PyObjekt a || a["stelle"] is not List<object?> p || a["raum"] is not string raum)
                {
                    continue;
                }
                var q = (List<object?>)((PyObjekt)ln[i]!)["stelle"]!;
                for (var g = 0; g < ga.Count; g++)
                {
                    if (Wege.RaumBei(Py.F(p[0]), Py.F(p[1]), ga[g]) is not { } ra || Py.Str(ra["id"]) != raum)
                    {
                        continue;
                    }
                    anzahl++;
                    var rn = Wege.RaumBei(Py.F(q[0]), Py.F(q[1]), gn[g]);
                    if (rn is null || Py.Str(rn["id"]) != raum
                        || Math.Abs(Py.F(q[0]) - Py.F(rn["x"]) - (Py.F(p[0]) - Py.F(ra["x"]))) > 0.01
                        || Math.Abs(Py.F(q[1]) - Py.F(rn["y"]) - (Py.F(p[1]) - Py.F(ra["y"]))) > 0.01)
                    {
                        falsch.Add($"{liste}[{i}] {raum} {Py.F(p[0])}/{Py.F(p[1])} -> {Py.F(q[0])}/{Py.F(q[1])}");
                    }
                    break;
                }
            }
        }
        return (anzahl, falsch);
    }
}
