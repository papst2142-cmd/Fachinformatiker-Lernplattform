using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Pruefung;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 10 W1 (Weisung P9-13, Nico 5, Plan Phase 10 Abschnitt 2): beim Start nur die bisherigen Inhalte und die
/// gewaehlte Fachrichtung laden, eine andere Fachrichtung beim Wechsel nachladen. Jede Sicht nach dem Nachladen ist
/// gleich der Sicht beim Laden aller Fachrichtungen (Nummern, Daten, Themen, Rahmenplan), eine Klausur von vor einem
/// Wechsel findet ihre Szenarien, und die Lastprobe (+3 000 Inhalte je Fachrichtung, nur in einer Temp-Kopie)
/// schreibt die Ladezeiten ins Testprotokoll. Phase 10 W2 (Weisung P10-1): dasselbe fuer die Testprojekte und die neuen
/// Fragen fuers Fachgespraech; die Lastprobe fuellt jede Fachrichtung auf den Endstand von Phase 10 auf. Nur Testdaten (A3).
/// </summary>
public class LerninhalteNachladenTests(ITestOutputHelper ausgabe)
{
    private static readonly string Lernen = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen");
    private static readonly string[] Neue = ["ae", "dpa", "dv"];

    private static Func<string, byte[]?> Ordner(string ordner) => name =>
        Path.Combine(ordner, name + ".json") is var pfad && File.Exists(pfad) ? File.ReadAllBytes(pfad) : null;

    private static Lerninhalte Voll(string ordner) => Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte), Ordner(ordner));

    private static Lerninhalte Start(string ordner, string fr) =>
        Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte), Ordner(ordner), fr == "si" ? ["si"] : ["si", fr]);

    private static readonly IReadOnlyList<Core.Projekt.ProjektFrage> BisherigeFachfragen = Core.Projekt.Fachgespraech.Laden(Pfade.Inhalte);

    private static List<string> Fachfragen(Lerninhalte sicht) =>
        [.. sicht.FachgespraechFuer(BisherigeFachfragen, sicht.Sicht).Select(f => f.Id + "|" + f.Kategorie + "|" + f.Frage + "|" + f.Hinweis)];

    private static List<string> Schluessel(IEnumerable<Lerninhalt> liste) =>
        [.. liste.Select(e => e.Nummer.ToString(CultureInfo.InvariantCulture) + "|" + e.Zugehoerigkeit + "|" + e.Daten.ToJsonString())];

    /// <summary>Zwei Sichten derselben Fachrichtung sind gleich (alles, was die Oberflaeche liest).</summary>
    private static void Gleich(Lerninhalte soll, Lerninhalte ist)
    {
        Assert.Equal(soll.Sicht, ist.Sicht);
        Assert.Equal(Schluessel(soll.Karteikarten), Schluessel(ist.Karteikarten));
        Assert.Equal(Schluessel(soll.Quizfragen), Schluessel(ist.Quizfragen));
        Assert.Equal(Schluessel(soll.Ap1Szenarien), Schluessel(ist.Ap1Szenarien));
        Assert.Equal(Schluessel(soll.Ap2Szenarien), Schluessel(ist.Ap2Szenarien));
        Assert.Equal(Schluessel(soll.Testprojekte), Schluessel(ist.Testprojekte));
        Assert.Equal(soll.AnzahlQuizfragenDatei, ist.AnzahlQuizfragenDatei);
        Assert.Equal(soll.ThemenReihenfolge, ist.ThemenReihenfolge);
        Assert.Equal(soll.Rahmenplan.ToJsonString(), ist.Rahmenplan.ToJsonString());
        Assert.Equal(soll.Bisher.Karteikarten.Count, ist.Bisher.Karteikarten.Count);
        Assert.Equal(Fachfragen(soll), Fachfragen(ist));
    }

    [Theory]
    [InlineData("si")]
    [InlineData("ae")]
    [InlineData("dpa")]
    [InlineData("dv")]
    public void Start_NurGewaehlte_SichtGleichVollLaden(string fr)
    {
        var start = Start(Lernen, fr);
        Gleich(Voll(Lernen).Fuer(fr), start.Fuer(fr));
        // von den neuen Inhalten ist nur die gewaehlte Fachrichtung geladen
        Assert.All(start.Karteikarten.Concat(start.Quizfragen).Concat(start.Ap2Szenarien).Concat(start.Testprojekte)
            .Where(e => e.Zugehoerigkeit is not ("si" or "gemeinsam")), e => Assert.Equal(fr, e.Zugehoerigkeit));
        Assert.All(start.NeueFachfragen, f => Assert.Equal(fr, f.Zugehoerigkeit));
        foreach (var andere in Neue.Where(n => n != fr))
        {
            Assert.False(start.HatFachrichtung(andere));
        }
        Assert.True(start.HatFachrichtung(fr));
    }

    [Fact]
    public void Nachladen_JedeReihenfolge_SichtGleichVollLaden()
    {
        var voll = Voll(Lernen);
        foreach (var reihe in new[] { new[] { "si", "ae", "dpa", "dv" }, ["dv", "si", "ae"], ["dpa", "dv", "ae", "si"] })
        {
            var geladen = Start(Lernen, reihe[0]);
            foreach (var fr in reihe)
            {
                geladen = geladen.MitFachrichtung(fr);
                Assert.True(geladen.HatFachrichtung(fr));
                Gleich(voll.Fuer(fr), geladen.Fuer(fr));
            }
            // zurueck: die zuerst geladene Sicht ist weiter gleich, nichts wird doppelt angehaengt
            Gleich(voll.Fuer(reihe[0]), geladen.Fuer(reihe[0]));
            Assert.Equal(geladen.Karteikarten.Select(e => e.Nummer).Distinct().Count(), geladen.Karteikarten.Count);
        }
    }

    [Fact]
    public void Nachladen_GleicheFachrichtungZweimal_DieselbenInhalte()
    {
        var start = Start(Lernen, "si");
        var erste = start.MitFachrichtung("ae");
        Assert.Same(erste, start.MitFachrichtung("ae"));
        Assert.Same(erste, erste.MitFachrichtung("ae"));
        Assert.Same(erste, erste.Fuer("ae").MitFachrichtung("si"));
    }

    [Theory]
    [InlineData("ae")]
    [InlineData("dpa")]
    [InlineData("dv")]
    public void KlausurVonVorEinemWechsel_FindetIhreSzenarien(string fr)
    {
        // eine Pruefung mit Quelle ap2_<fr> wird gelesen, waehrend SI gewaehlt ist (nicht geladen)
        var si = Start(Lernen, "si").Fuer("si");
        var soll = Voll(Lernen).Ap2SzenarienFuer(fr);
        var klausur = new KlausurInhalte(si, Core.Daten.Umbenennungen.Laden(Pfade.Inhalte));
        var ist = klausur.Quelle("ap2_" + fr);
        Assert.Equal(330, soll.Count);
        Assert.Equal(Schluessel(soll), Schluessel(ist));
    }

    [Fact]
    public void Lastprobe_DreitausendJeFachrichtung_ZeitenImProtokoll()
    {
        // Endstand Phase 10 je Fachrichtung: eigene 1 500 Karten, 2 120 Fragen, 50 Testprojekte, 18 Fragen fuers
        // Fachgespraech (Plan Abschnitt 3); was eine Fachrichtung schon hat, zaehlt mit (W2: AE ist schon fertig).
        // Probe-Inhalte nur in dieser Temp-Kopie, nie im Programm
        var ordner = Directory.CreateTempSubdirectory("lastprobe_").FullName;
        try
        {
            foreach (var datei in Directory.GetFiles(Lernen, "*.json"))
            {
                File.Copy(datei, Path.Combine(ordner, Path.GetFileName(datei)));
            }
            Vergroessern(ordner, "karteikarten", 1500, "q");
            Vergroessern(ordner, "quizfragen", 2120, "q");
            Projekte(ordner, 50);
            Fachfragen(ordner, 18);

            var zeiten = new List<string>();
            foreach (var fr in new[] { "si", "ae", "dpa", "dv" })
            {
                var uhr = Stopwatch.StartNew();
                var start = Start(ordner, fr);
                var sicht = start.Fuer(fr);
                var startMs = uhr.Elapsed.TotalMilliseconds;
                var andere = fr == "dv" ? "ae" : "dv";
                uhr.Restart();
                var nach = start.MitFachrichtung(andere).Fuer(andere);
                zeiten.Add(string.Create(CultureInfo.InvariantCulture, $"Start {fr} {startMs:F0} ms, nachladen {andere} {uhr.Elapsed.TotalMilliseconds:F0} ms"));
                if (fr != "si")
                {
                    Assert.Equal(1084 + 1500, sicht.Karteikarten.Count);
                    Assert.Equal(1615 + 2120, sicht.Quizfragen.Count);
                    Assert.Equal(50, sicht.Testprojekte.Count);
                    Assert.Equal(42 + 18, Fachfragen(sicht).Count);
                }
                Assert.Equal(1084 + 1500, nach.Karteikarten.Count);
                Assert.Equal(42 + 18, Fachfragen(nach).Count);
            }
            var voll = Stopwatch.StartNew();
            _ = Voll(ordner).Fuer("ae");
            zeiten.Add(string.Create(CultureInfo.InvariantCulture, $"zum Vergleich alle Fachrichtungen laden {voll.Elapsed.TotalMilliseconds:F0} ms"));
            ausgabe.WriteLine("Lastprobe (+3 000 je Fachrichtung, warm im Testlauf, nur Info): " + string.Join("; ", zeiten));
        }
        finally
        {
            Directory.Delete(ordner, recursive: true);
        }
    }

    /// <summary>Je Fachrichtung Kopien vorhandener Eintraege mit eindeutigem Text anhaengen, bis sie <paramref name="anzahl"/> eigene hat.</summary>
    private static void Vergroessern(string ordner, string name, int anzahl, string feld)
    {
        var pfad = Path.Combine(ordner, name + ".json");
        var liste = JsonNode.Parse(File.ReadAllBytes(pfad))!.AsArray();
        var vorlagen = liste.OfType<JsonObject>().ToList();
        foreach (var fr in Neue)
        {
            var eigene = vorlagen.Where(e => (string?)e["fachrichtung"] == fr).ToList();
            for (var i = eigene.Count; i < anzahl; i++)
            {
                var kopie = (JsonObject)eigene[i % eigene.Count].DeepClone();
                kopie[feld] = (string?)kopie[feld] + " (Lastprobe " + i.ToString(CultureInfo.InvariantCulture) + ")";
                liste.Add(kopie);
            }
        }
        File.WriteAllText(pfad, liste.ToJsonString());
    }

    private static void Projekte(string ordner, int anzahl)
    {
        var vorlagen = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Pfade.Inhalte, "projektarbeiten.json")))!.AsArray().OfType<JsonObject>().ToList();
        var pfad = Path.Combine(ordner, "projektarbeiten.json");
        var liste = File.Exists(pfad) ? JsonNode.Parse(File.ReadAllBytes(pfad))!.AsArray() : [];
        foreach (var fr in Neue)
        {
            for (var i = liste.OfType<JsonObject>().Count(e => (string?)e["fachrichtung"] == fr); i < anzahl; i++)
            {
                var kopie = (JsonObject)vorlagen[i % vorlagen.Count].DeepClone();
                kopie["title"] = (string?)kopie["title"] + " (Lastprobe " + fr + " " + i.ToString(CultureInfo.InvariantCulture) + ")";
                kopie["fachrichtung"] = fr;
                liste.Add(kopie);
            }
        }
        File.WriteAllText(pfad, liste.ToJsonString());
    }

    /// <summary>Fragen fuers Fachgespraech je Fachrichtung bis <paramref name="anzahl"/> eigene (Kopien der bisherigen mit eigener Kennung).</summary>
    private static void Fachfragen(string ordner, int anzahl)
    {
        var vorlagen = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Pfade.Inhalte, "fachgespraech.json")))!["fragen"]!.AsArray().OfType<JsonObject>().ToList();
        var pfad = Path.Combine(ordner, Lerninhalte.FachgespraechDatei + ".json");
        var datei = File.Exists(pfad) ? JsonNode.Parse(File.ReadAllBytes(pfad))!.AsObject() : new JsonObject { ["fragen"] = new JsonArray() };
        var liste = datei["fragen"]!.AsArray();
        foreach (var fr in Neue)
        {
            for (var i = liste.OfType<JsonObject>().Count(e => (string?)e["fachrichtung"] == fr); i < anzahl; i++)
            {
                var kopie = (JsonObject)vorlagen[i % vorlagen.Count].DeepClone();
                kopie["id"] = "last_" + fr + i.ToString(CultureInfo.InvariantCulture);
                kopie["q"] = (string?)kopie["q"] + " (Lastprobe " + fr + " " + i.ToString(CultureInfo.InvariantCulture) + ")";
                kopie["fachrichtung"] = fr;
                liste.Add(kopie);
            }
        }
        File.WriteAllText(pfad, datei.ToJsonString());
    }
}
