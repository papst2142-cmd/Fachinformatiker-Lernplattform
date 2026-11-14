using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle E4 (S 0.53.0, Weisung P12-247, Kurzplan P12-147 Abschnitt 4): Formatnummern nur mit Arbeitsort. Echte Dateien
/// der alten Nummern (S 0.52.0 mit einer neuen Quest: <c>projekt_S0.52.0.fisbau</c> Format 3,
/// <c>stadt_S0.52.0_quests.fistadt</c> Format 1) oeffnen sich unveraendert; die Stadt-Datei schreibt sich bitgleich zurueck.
/// Echte Dateien der neuen Nummern (<c>projekt_S0.53.0_arbeitsort.fisbau</c> Format 4,
/// <c>stadt_S0.53.0_arbeitsort.fistadt</c> Format 2) gleichen dem, was das Werkzeug heute schreibt (neu schreiben mit
/// <c>STADTBAU_TESTDATEI_SCHREIBEN=1</c>). Nummer und Inhalt muessen zusammenpassen (sonst rot, nie still weg).
/// </summary>
public class ArbeitsortFormatTests
{
    private const string AltProjekt = "projekt_S0.52.0.fisbau";
    private const string AltStadt = "stadt_S0.52.0_quests.fistadt";
    private const string NeuProjekt = "projekt_S0.53.0_arbeitsort.fisbau";
    private const string NeuStadt = "stadt_S0.53.0_arbeitsort.fistadt";

    private static byte[] Testdatei(string name) => Daten.Lesen(TestdateiTests.Ordner + name) ?? throw new FileNotFoundException(name);

    /// <summary>Das Projekt der neuen Testdateien: die Quest der alten Datei mit Arbeitsort bei Talheim (wartet nicht).</summary>
    private static Projekt NeuesProjekt(Werkzeugordner ablage)
    {
        File.WriteAllBytes(Path.Combine(Directory.CreateDirectory(ablage.Projekte).FullName, "Quests S0520" + Projekt.Endung), Testdatei(AltProjekt));
        var p = Projekt.Oeffnen(Path.Combine(ablage.Projekte, "Quests S0520" + Projekt.Endung), Daten.Spiel, ablage);
        var id = p.Quests().Single(q => !q.Bestand).Id;
        Assert.True(p.QuestArbeitsort(id, "talheim"));
        Assert.True(p.QuestArbeitsortRaum(id, "technikraum"));
        Assert.True(p.QuestArbeitsortWartet(id, false));
        p.Speichern(vonHand: true);
        return p;
    }

    private static readonly DateTimeOffset Zeit = new(2026, 11, 14, 9, 0, 0, TimeSpan.FromHours(1));

    [Fact]
    public void AlteDateien_S0520_OeffnenUnveraendert_StadtBitgleich()
    {
        var alt = Testdatei(AltProjekt);
        Assert.Contains("\"formatversion\": 3,", Encoding.UTF8.GetString(alt), StringComparison.Ordinal);
        Assert.Contains("Fachinformatiker Stadtbau S 0.52.0", Encoding.UTF8.GetString(alt), StringComparison.Ordinal);
        var ablage = Daten.NeueAblage();
        Directory.CreateDirectory(ablage.Projekte);
        var datei = Path.Combine(ablage.Projekte, "Alt" + Projekt.Endung);
        File.WriteAllBytes(datei, alt);
        var p = Projekt.Oeffnen(datei, Daten.Spiel, ablage);
        Assert.False(p.NurLesen);
        var neu = p.Quests().Single(q => !q.Bestand);
        Assert.Equal("WLAN im Bahnhofscafe", neu.Titel);
        Assert.Null(Questebene.ArbeitsortLesen(neu.Daten));
        Assert.DoesNotContain(p.QuestFunde(), f => f.Rot);
        p.Speichern(vonHand: true);
        Assert.Contains("\"formatversion\": 3,", File.ReadAllText(datei, Encoding.UTF8), StringComparison.Ordinal);

        var stadt = Testdatei(AltStadt);
        Assert.Contains("\"formatversion\": 1,", Encoding.UTF8.GetString(stadt), StringComparison.Ordinal);
        var f = StadtFassung.Lesen(stadt);
        Assert.Equal(stadt, f.Schreiben());
        Assert.False(Questebene.HatArbeitsort(f.Stadtdaten));
        Assert.True(Questebene.Hat(f.Stadtdaten));
    }

    [Fact]
    public void NeueDateien_S0530_GleichDemWerkzeug_Format4Und2()
    {
        var p = NeuesProjekt(Daten.NeueAblage());
        var projekt = File.ReadAllBytes(p.Datei);
        var stadt = p.Exportieren(Zeit);
        if (Environment.GetEnvironmentVariable("STADTBAU_TESTDATEI_SCHREIBEN") == "1")
        {
            File.WriteAllBytes(Path.Combine(Daten.Wurzel, TestdateiTests.Ordner + NeuProjekt), projekt);
            File.WriteAllBytes(Path.Combine(Daten.Wurzel, TestdateiTests.Ordner + NeuStadt), stadt);
        }
        Assert.Contains("\"formatversion\": 4,", Encoding.UTF8.GetString(projekt), StringComparison.Ordinal);
        Assert.Contains("\"formatversion\": 2,", Encoding.UTF8.GetString(stadt), StringComparison.Ordinal);
        // die Projektdatei traegt die Kennzeile des Werkzeugs (Version), darum nur bis auf diese Zeile gleich
        Assert.Equal(OhneWerkzeug(Testdatei(NeuProjekt)), OhneWerkzeug(projekt));
        Assert.Equal(OhneWerkzeug(Testdatei(NeuStadt)), OhneWerkzeug(stadt));
    }

    private static string OhneWerkzeug(byte[] daten) =>
        string.Join("\n", Encoding.UTF8.GetString(daten).Split('\n').Where(z => !z.StartsWith(" \"werkzeug\": ", StringComparison.Ordinal) && !z.StartsWith(" \"pruefsumme\": ", StringComparison.Ordinal)));

    [Fact]
    public void NeueDateien_Lesbar_ArbeitsortDa_SpielLaedt()
    {
        var f = StadtFassung.Lesen(Testdatei(NeuStadt));
        Assert.True(Questebene.HatArbeitsort(f.Stadtdaten));
        Assert.Equal(Testdatei(NeuStadt), f.Schreiben());
        var stadt = f.StadtFuer(f.MitKarte(Daten.Spiel.Inhalt));
        var aufgabe = SpielDaten.Liste(stadt.Inhalt["aufgaben"]).Select(SpielDaten.Objekt)
            .Single(a => Py.Gleich(a["id"], "neu-1"));
        Assert.Equal("talheim", aufgabe["ort"]);
        Assert.Equal("technikraum", aufgabe["raum"]);
        Assert.True(Py.Wahr(aufgabe[Questebene.FeldOhneQuestgeber]));
        Assert.False(Py.Hat(aufgabe, "stelle"));

        var ablage = Daten.NeueAblage();
        Directory.CreateDirectory(ablage.Projekte);
        var datei = Path.Combine(ablage.Projekte, "Neu" + Projekt.Endung);
        File.WriteAllBytes(datei, Testdatei(NeuProjekt));
        var p = Projekt.Oeffnen(datei, Daten.Spiel, ablage);
        Assert.False(p.NurLesen);
        Assert.Equal("talheim", Questebene.ArbeitsortLesen(p.Quests().Single(q => !q.Bestand).Daten)!.Ort);
    }

    /// <summary>Nummer und Inhalt passen nicht: rot mit Grund (Gegenproben in beide Richtungen), neuere Nummer = klarer Fehler.</summary>
    [Fact]
    public void Fistadt_NummerPasstNichtZumInhalt_Rot()
    {
        var neu = Encoding.UTF8.GetString(Testdatei(NeuStadt));
        var alt = Encoding.UTF8.GetString(Testdatei(AltStadt));
        foreach (var text in new[] { neu.Replace("\"formatversion\": 2,", "\"formatversion\": 1,", StringComparison.Ordinal),
            alt.Replace("\"formatversion\": 1,", "\"formatversion\": 2,", StringComparison.Ordinal) })
        {
            var fehler = Assert.Throws<StadtbauFehler>(() => StadtFassung.Lesen(Encoding.UTF8.GetBytes(text)));
            Assert.Contains(fehler.Funde, t => t.Contains("passt aber nicht zum Inhalt", StringComparison.Ordinal));
        }
        var drei = Assert.Throws<StadtbauFehler>(() => StadtFassung.Lesen(Encoding.UTF8.GetBytes(neu.Replace("\"formatversion\": 2,", "\"formatversion\": 3,", StringComparison.Ordinal))));
        Assert.Contains(drei.Funde, t => t.Contains("neueren Fassung des Formats", StringComparison.Ordinal));
    }
}
