using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Zufall;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L6 (Weisung P2-4): Pruefungstrainer, Art "Uebung", wie app_gui.QuizView
/// (Python v0.63.2). Mischen mit festem Startwert wie Python (G22 "uebung"), Antworten
/// sofort gespeichert, Ergebnis nur am Ende, Abbruch ohne Ergebnis.
/// </summary>
public class UebungTests
{
    private static readonly JsonElement G22 = VergleichsHilfe.Lade("lerntexte.json");
    private static readonly Lerninhalte Inhalte = Lerninhalte.Laden(Pfade.Inhalte);

    private sealed class Aufbau : IDisposable
    {
        private readonly TempOrdner _ordner = new();

        public Aufbau(PythonZufall? zufall = null, Func<int>? sekunden = null)
        {
            Lernstand = new Lernstand(Path.Combine(_ordner.Pfad, Lernstand.DateiName));
            var n = 0;
            var schreiber = new LernstandSchreiber(Lernstand, SpeichernTests.FesteUhr("2026-10-20T12:00:00Z"), () => "u-" + ++n);
            Uebung = new Uebung(Inhalte, () => new StatusBuch(Antwortbuch.Lesen(Lernstand)), schreiber,
                zufall: zufall ?? new PythonZufall(99), neuerZufall: () => new PythonZufall(5), sekunden: sekunden);
        }

        public Lernstand Lernstand { get; }

        public Uebung Uebung { get; }

        public long Anzahl(string tabelle) => (long)Lernstand.Abfragen($"SELECT COUNT(*) FROM {tabelle}")[0][0]!;

        public void Dispose() => _ordner.Dispose();
    }

    private static List<string> Texte(JsonElement liste) => liste.EnumerateArray().Select(e => e.GetString()!).ToList();

    /// <summary>Runde und Antwortreihenfolge mit Startwert wie Python (start_quiz mit Statusfilter, practice).</summary>
    [Fact]
    public void MischenWiePython()
    {
        foreach (var fall in G22.GetProperty("uebung").EnumerateArray())
        {
            using var a = new Aufbau(new PythonZufall(fall.GetProperty("startwert").GetInt64()));
            if (fall.GetProperty("art").GetString() == "start")
            {
                if (fall.GetProperty("fachbereich").GetString() is { } bereich)
                {
                    a.Uebung.SetzeFachbereich(bereich);
                }
                a.Uebung.SetzeStatus(FragenStatus.Offen);
                a.Uebung.AnzahlAendern((fall.GetProperty("anzahl").GetInt32() - Uebung.AnzahlVorgabe) / Uebung.AnzahlSchritt);
                Assert.Equal(fall.GetProperty("anzahl").GetInt32(), a.Uebung.Anzahl);
                Assert.Null(a.Uebung.Starten());
            }
            else
            {
                a.Uebung.Ueben(Texte(fall.GetProperty("fragen")));
            }
            Assert.Equal(Texte(fall.GetProperty("runde")), a.Uebung.Runde.Select(q => q.Frage!).ToList());
            foreach (var antworten in fall.GetProperty("antworten").EnumerateArray())
            {
                Assert.Equal(Texte(antworten), a.Uebung.Optionen);
                Assert.NotNull(a.Uebung.Einreichen(a.Uebung.Optionen[0]));
                a.Uebung.Weiter();
            }
            Assert.False(a.Uebung.Laeuft);
        }
    }

    [Fact]
    public void AntwortSofortGespeichertErgebnisAmEnde()
    {
        var uhr = 100;
        using var a = new Aufbau(sekunden: () => uhr);
        a.Uebung.SetzeFachbereich(Fachbereiche.Alle[2]);
        a.Uebung.AnzahlAendern(-1);
        Assert.Equal(Uebung.AnzahlMin, a.Uebung.Anzahl);
        Assert.Null(a.Uebung.Starten());
        Assert.Equal(Uebung.AnzahlMin, a.Uebung.Runde.Count);
        Assert.Equal(a.Uebung.Runde.Count, a.Uebung.Runde.Distinct().Count());
        // Ohne Auswahl: Hinweis, nichts gespeichert
        Assert.Equal(KartenTexte.AuswahlFehlt, a.Uebung.Einreichen(null)!.Warnung);
        Assert.Equal(0, a.Anzahl("quiz_answers"));
        UebungErgebnis? ergebnis = null;
        for (var i = 0; i < Uebung.AnzahlMin; i++)
        {
            var frage = a.Uebung.Frage!;
            Assert.Equal(Beschriftungen.Einsetzen(TrainerTexte.Fortschritt, i + 1, Uebung.AnzahlMin), a.Uebung.Fortschritt);
            var richtig = i % 2 == 0;
            var antwort = a.Uebung.Einreichen(richtig ? frage.Antwort : a.Uebung.Optionen.First(o => o != frage.Antwort))!;
            Assert.Equal(richtig, antwort.Richtig);
            Assert.Equal(i == Uebung.AnzahlMin - 1 ? TrainerTexte.Auswertung : TrainerTexte.NaechsteFrage, antwort.Knopf);
            Assert.StartsWith(richtig ? TrainerTexte.ErklaerungRichtig : Beschriftungen.Einsetzen(TrainerTexte.ErklaerungFalsch, frage.Antwort!),
                antwort.Erklaerung, StringComparison.Ordinal);
            // Doppelt einreichen speichert nicht noch einmal
            Assert.Null(a.Uebung.Einreichen(frage.Antwort));
            Assert.Equal(i + 1, a.Anzahl("quiz_answers"));
            Assert.Equal(0, a.Anzahl("test_results"));
            uhr += 30;
            ergebnis = a.Uebung.Weiter();
        }
        Assert.NotNull(ergebnis);
        Assert.Equal((3, 5, 60.0, 150, true), (ergebnis!.Richtig, ergebnis.Gesamt, ergebnis.Prozent, ergebnis.Sekunden, ergebnis.Gespeichert));
        Assert.Equal(Trainer.Note(60.0), ergebnis.Note);
        var zeile = a.Lernstand.Abfragen("SELECT score, total, percentage, note, duration_seconds FROM test_results").Single();
        Assert.Equal([3L, 5L, 60.0, Trainer.Note(60.0), 150L], zeile);
        Assert.Equal(
            a.Lernstand.Abfragen("SELECT correct FROM quiz_answers ORDER BY id").Select(z => (long)z[0]!).ToList(),
            [1L, 0L, 1L, 0L, 1L]);
        Assert.False(a.Uebung.Laeuft);
    }

    [Fact]
    public void AbbruchSpeichertKeinErgebnis()
    {
        using var a = new Aufbau();
        Assert.Null(a.Uebung.Starten());
        a.Uebung.Einreichen(a.Uebung.Optionen[0]);
        a.Uebung.Weiter();
        a.Uebung.Einreichen(a.Uebung.Optionen[0]);
        a.Uebung.Abbrechen();
        Assert.False(a.Uebung.Laeuft);
        Assert.Equal(2, a.Anzahl("quiz_answers"));
        Assert.Equal(0, a.Anzahl("test_results"));
        Assert.Equal(TrainerTexte.FortschrittLeer, a.Uebung.Fortschritt);
    }

    /// <summary>NumberStepper: Vorgabe 10, mindestens 5, Schritt 5, hoechstens max(5, Pool).</summary>
    [Fact]
    public void FragenanzahlWiePython()
    {
        using var a = new Aufbau();
        Assert.Equal(Uebung.AnzahlVorgabe, a.Uebung.Anzahl);
        Assert.Equal(Inhalte.Quizfragen.Count, a.Uebung.Hoechstens);
        a.Uebung.AnzahlAendern(-5);
        Assert.Equal(5, a.Uebung.Anzahl);
        a.Uebung.AnzahlAendern(100000);
        Assert.Equal(Inhalte.Quizfragen.Count, a.Uebung.Anzahl);
        // Kleiner Pool (3 falsch beantwortete Fragen, Status "Zu üben"): Hoechstwert 5, Runde = Pool
        var schreiber = new LernstandSchreiber(a.Lernstand);
        foreach (var q in Inhalte.Quizfragen.Take(3))
        {
            Assert.True(schreiber.QuizSpeichern(q.Fachbereich!, q.Frage!, false));
        }
        a.Uebung.SetzeStatus(FragenStatus.Ueben);
        Assert.Equal(3, a.Uebung.Pool.Count);
        Assert.Equal(5, a.Uebung.Hoechstens);
        Assert.Equal(5, a.Uebung.Anzahl);
        Assert.Null(a.Uebung.Starten());
        Assert.Equal(Inhalte.Quizfragen.Take(3).Select(q => q.Frage).Order(), a.Uebung.Runde.Select(q => q.Frage).Order());
    }

    [Fact]
    public void LeererPoolStartetNicht()
    {
        using var a = new Aufbau();
        a.Uebung.SetzeStatus(FragenStatus.Fertig);
        Assert.Empty(a.Uebung.Pool);
        Assert.Equal(TrainerTexte.KeineFragen, a.Uebung.Starten());
        Assert.False(a.Uebung.Laeuft);
        Assert.Equal(Beschriftungen.Einsetzen(TrainerTexte.Verfuegbar, "0 Fragen",
            KartenTexte.ZaehlText(new Dictionary<string, int>
            {
                [FragenStatus.Offen] = Inhalte.Quizfragen.Count, [FragenStatus.Ueben] = 0, [FragenStatus.Fertig] = 0,
            })), a.Uebung.PoolText);
    }

    /// <summary>Ohne Statusfilter: Runde aus der bevorzugten Reihenfolge, ohne doppelte Fragen.</summary>
    [Fact]
    public void RundeOhneStatusfilterOhneDoppelte()
    {
        using var a = new Aufbau();
        a.Uebung.AnzahlAendern(2);
        Assert.Null(a.Uebung.Starten());
        Assert.Equal(20, a.Uebung.Runde.Count);
        Assert.Equal(20, a.Uebung.Runde.Select(q => q.Frage).Distinct().Count());
    }
}
