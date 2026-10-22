using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, PB3: Verhaltenstabelle Speichern der Seiten AP1-/AP2-Szenarien und Testprojekt wie
/// app_gui v0.63.2 (toggle_solution, rate, _flush, load_scenario/load_project): genau eine Zeile je
/// Bewertung, angesehen ohne Bewertung = eine Zeile mit NULL beim Wechsel oder Beenden, nie
/// doppelt; nach jeder Zeile folgt der Abgleich (NachSpeichern).
/// </summary>
public class SzenarioablaufTests
{
    private static readonly Lerninhalte Inhalte = Lerninhalte.Laden(Pfade.Inhalte);

    private sealed class Probe : IDisposable
    {
        private readonly TempOrdner _ordner = new();

        public Probe(string quelle)
        {
            Pfad = Path.Combine(_ordner.Pfad, Lernstand.DateiName);
            var lernstand = new Lernstand(Pfad);
            var schreiber = new LernstandSchreiber(lernstand, SpeichernTests.FesteUhr("2026-10-21T08:00:00+00:00"));
            Ablauf = new Szenarioablauf(quelle, quelle switch
            {
                Quellen.Ap1 => Inhalte.Ap1Szenarien,
                Quellen.Ap2 => Inhalte.Ap2Szenarien,
                _ => Inhalte.Testprojekte,
            }, () => new StatusBuch(Antwortbuch.Lesen(lernstand)), schreiber);
            Ablauf.NachSpeichern += () => Abgleiche++;
            Tabelle = quelle switch
            {
                Quellen.Ap1 => "ap1_events",
                Quellen.Ap2 => "scenario_events",
                _ => "project_events",
            };
        }

        public string Pfad { get; }

        public string Tabelle { get; }

        public Szenarioablauf Ablauf { get; }

        public int Abgleiche { get; private set; }

        /// <summary>Zeilen als "Position:correct" (correct NULL = "-").</summary>
        public List<string> Zeilen()
        {
            using var db = VergleichsHilfe.Oeffnen(Pfad);
            var spalte = Tabelle == "project_events" ? "project_index" : "scenario_index";
            return [.. VergleichsHilfe.Abfrage(db, $"SELECT {spalte}, correct FROM {Tabelle} ORDER BY id")
                .Select(z => z[0] + ":" + (z[1] is null ? "-" : z[1]!.ToString()))];
        }

        public void Dispose() => _ordner.Dispose();
    }

    public static TheoryData<string> AlleQuellen => [Quellen.Ap1, Quellen.Ap2, Quellen.Projekt];

    [Theory]
    [MemberData(nameof(AlleQuellen))]
    public void VerhaltenstabelleSpeichern(string quelle)
    {
        using var p = new Probe(quelle);
        var a = p.Ablauf;

        // 1. Nur lesen und wechseln, ohne aufzudecken: nichts
        a.Laden(1);
        a.Laden(2);
        Assert.Empty(p.Zeilen());

        // 2. Aufdecken, bewerten: genau eine Zeile mit der Bewertung
        a.Umschalten();
        Assert.True(a.Sichtbar);
        Assert.Equal(2, a.Offen);
        a.Bewerten(true);
        Assert.Equal(["2:1"], p.Zeilen());
        Assert.Null(a.Offen);

        // 3. Noch einmal bewerten ohne neues Aufdecken: nichts (nie doppelt)
        a.Bewerten(false);
        a.Bewerten(true);
        Assert.Equal(["2:1"], p.Zeilen());

        // 4. Zu- und wieder aufdecken nach der Bewertung: neue offene Aufdeckung, beim Wechsel NULL
        a.Umschalten();
        Assert.False(a.Sichtbar);
        a.Umschalten();
        Assert.Equal(2, a.Offen);
        a.Laden(3);
        Assert.Equal(["2:1", "2:-"], p.Zeilen());
        Assert.False(a.Sichtbar);
        Assert.Null(a.Offen);

        // 5. Aufdecken, zudecken, aufdecken: eine offene Aufdeckung, beim Beenden eine Zeile NULL
        a.Umschalten();
        a.Umschalten();
        a.Umschalten();
        a.Speichern();
        a.Speichern();
        Assert.Equal(["2:1", "2:-", "3:-"], p.Zeilen());

        // 6. Aufdecken und falsch bewerten, danach wechseln: nur die Bewertung
        a.Laden(4);
        a.Umschalten();
        a.Bewerten(false);
        a.Laden(5);
        Assert.Equal(["2:1", "2:-", "3:-", "4:0"], p.Zeilen());

        // 7. Aufgedeckt, dann gleicher Eintrag neu geladen (Nächstes auf sich selbst): NULL
        a.Umschalten();
        a.Laden(5);
        Assert.Equal(["2:1", "2:-", "3:-", "4:0", "5:-"], p.Zeilen());

        // Nach jeder Zeile genau ein Abgleich
        Assert.Equal(p.Zeilen().Count, p.Abgleiche);
    }

    [Theory]
    [MemberData(nameof(AlleQuellen))]
    public void StatusFolgtDerBewertung(string quelle)
    {
        using var p = new Probe(quelle);
        var a = p.Ablauf;
        a.Laden(0);
        Assert.Equal("Offen · noch nicht bearbeitet", a.StatusZeile.Text);
        a.Umschalten();
        a.Speichern();
        Assert.Equal("Offen · angesehen, noch nicht bewertet", a.StatusZeile.Text);
        a.Umschalten();
        a.Umschalten();
        a.Bewerten(false);
        Assert.Equal(("Zu üben · zuletzt falsch", FragenStatus.Rot), a.StatusZeile);
        a.Umschalten();
        a.Umschalten();
        a.Bewerten(true);
        a.Umschalten();
        a.Umschalten();
        a.Bewerten(true);
        Assert.Equal(("Abgeschlossen · 2-mal in Folge richtig", "fertig"), a.StatusZeile);
        Assert.Equal((FragenStatus.Fertig, ""), a.Stati()[0]);
    }

    /// <summary>Eigene Loesung nur in dieser Sitzung: bleibt beim Wechsel je Eintrag, kommt nie in die Datenbank.</summary>
    [Fact]
    public void EigeneLoesungNurInDerSitzung()
    {
        using var p = new Probe(Quellen.Ap2);
        var a = p.Ablauf;
        a.EigeneLoesungSetzen(0, "Mein Text äöü");
        a.Laden(1);
        Assert.Equal("", a.EigeneLoesung(1));
        Assert.Equal("Mein Text äöü", a.EigeneLoesung(0));
        using var db = VergleichsHilfe.Oeffnen(p.Pfad);
        foreach (var tabelle in VergleichsHilfe.Abfrage(db, "SELECT name FROM sqlite_master WHERE type = 'table'").Select(z => (string)z[0]!))
        {
            foreach (var zeile in VergleichsHilfe.Abfrage(db, $"SELECT * FROM {tabelle}"))
            {
                Assert.DoesNotContain(zeile, w => w is string s && s.Contains("Mein Text", StringComparison.Ordinal));
            }
        }
    }

    /// <summary>Titel und Thema der Zeile wie Python: AP1/AP2 theme, Testprojekt cat.</summary>
    [Fact]
    public void ZeileTraegtTitelUndThema()
    {
        using var p = new Probe(Quellen.Projekt);
        p.Ablauf.Laden(3);
        p.Ablauf.Umschalten();
        p.Ablauf.Bewerten(true);
        using var db = VergleichsHilfe.Oeffnen(p.Pfad);
        var zeile = VergleichsHilfe.Abfrage(db, "SELECT title, category FROM project_events")[0];
        Assert.Equal(Inhalte.Testprojekte[3].Titel, zeile[0]);
        Assert.Equal(Inhalte.Testprojekte[3].Fachbereich, zeile[1]);
        using var p2 = new Probe(Quellen.Ap1);
        p2.Ablauf.Laden(4);
        p2.Ablauf.Umschalten();
        p2.Ablauf.Speichern();
        using var db2 = VergleichsHilfe.Oeffnen(p2.Pfad);
        var zeile2 = VergleichsHilfe.Abfrage(db2, "SELECT title, theme FROM ap1_events")[0];
        Assert.Equal(Inhalte.Ap1Szenarien[4].Titel, zeile2[0]);
        Assert.Equal(Inhalte.Ap1Szenarien[4].Text("theme"), zeile2[1]);
    }
}
