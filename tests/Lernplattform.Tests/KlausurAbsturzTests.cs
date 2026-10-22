using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Pruefung;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W2 (Weisung P4-6, Testpflichten): Absturzproben der Klausur und der Fachrichtung.
/// Das Kind (AbsturzProbe) schreibt, der Test beendet es hart und prueft die Datei: ein Abschluss
/// ist eine Buchung (Pruefung und alle Antworten oder nichts); haelt das Programm nach der Buchung,
/// aber vor dem Entfernen der laufenden Pruefung (clear_running), bucht ein zweiter Abschluss
/// nichts doppelt; das Waehlen der Fachrichtung laesst genau eine ganze Zeile zurueck.
/// </summary>
public class KlausurAbsturzTests
{
    /// <summary>Laufende Pruefung in einer Datei wie einstellungen.json; vorher sieht jede Aenderung.</summary>
    internal static LaufendePruefung DateiAblage(string datei, Action<JsonObject>? vorher = null) => new(
        () => File.Exists(datei) ? JsonNode.Parse(File.ReadAllText(datei))!.AsObject() : [],
        aendern =>
        {
            var neu = File.Exists(datei) ? JsonNode.Parse(File.ReadAllText(datei))!.AsObject() : [];
            aendern(neu);
            vorher?.Invoke(neu);
            File.WriteAllText(datei, neu.ToJsonString());
            return true;
        });

    internal static KlausurAblauf Ablauf(string db, LaufendePruefung ablage) => new(KlausurTests.Klausurinhalte, ablage,
        () => new LernstandSchreiber(new Lernstand(db)), () => DateTimeOffset.Now, KlausurTests.Berlin);

    /// <summary>Mitten im Abschluss hart beendet: je Pruefung genau 30 ganze Antworten, nie ein Teil.</summary>
    [Fact]
    public void AbsturzMittenImAbschluss()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        _ = new Lernstand(pfad);
        for (var runde = 0; runde < 3; runde++)
        {
            var gemeldet = AbsturzProbe.Starten(AbsturzProbe.ArtAbschluss, [pfad], 10 + runde * 7);
            using var db = VergleichsHilfe.Oeffnen(pfad);
            Assert.Equal("ok", (string)VergleichsHilfe.Abfrage(db, "PRAGMA quick_check")[0][0]!);
            var pruefungen = VergleichsHilfe.Abfrage(db, "SELECT uid FROM pruefungen").Select(z => (string)z[0]!).ToList();
            var antworten = VergleichsHilfe.Abfrage(db, "SELECT question FROM quiz_answers").Select(z => (string)z[0]!).ToList();
            Assert.Equal(pruefungen.Count, pruefungen.Distinct().Count());
            Assert.Equal(30 * pruefungen.Count, antworten.Count);
            foreach (var kennung in pruefungen)
            {
                var eigene = antworten.Where(a => a.StartsWith("Frage " + kennung + " ", StringComparison.Ordinal)).ToList();
                Assert.Equal(30, eigene.Count);
                Assert.All(eigene, a => Assert.EndsWith(new string('.', 3000), a, StringComparison.Ordinal));
            }
            Assert.All(gemeldet, k => Assert.Contains(k, pruefungen));
            Assert.True(gemeldet.Count >= 10 + runde * 7);
        }
    }

    /// <summary>
    /// Nach der Buchung, vor clear_running hart beendet (Aufgabe und WiSo): die Pruefung bleibt in
    /// Phase "bewertung"; der zweite Abschluss nach dem Neustart ergibt genau eine Zeile und einen
    /// Satz Antworten und entfernt dann die laufende Pruefung.
    /// </summary>
    [Theory]
    [InlineData(Klausur.Konzeption, "scenario_events")]
    [InlineData(Klausur.Ap1, "ap1_events")]
    [InlineData(Klausur.Wiso, "quiz_answers")]
    public void AbsturzVorDemEntfernen_ZweiterAbschlussBuchtEinmal(string art, string tabelle)
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var datei = Path.Combine(ordner.Pfad, "einstellungen.json");
        var lernstand = new Lernstand(pfad);
        var ablauf = Ablauf(pfad, DateiAblage(datei));
        Assert.Equal(KlausurAnsicht.Lauf, ablauf.Starten(art).Ansicht);
        if (art != Klausur.Wiso)
        {
            Assert.Equal(KlausurAnsicht.Bewertung, ablauf.Abgeben(auto: false).Ansicht);
            ablauf.Haken(0, 0, 0, true);
        }
        AbsturzProbe.Starten(AbsturzProbe.ArtVorLoeschen, [pfad, datei]);
        long Zeilen(string t) => (long)lernstand.Abfragen($"SELECT COUNT(*) FROM {t}")[0][0]!;
        var antworten = Zeilen(tabelle);
        Assert.Equal(1, Zeilen("pruefungen"));
        Assert.True(antworten > 0);
        var stehen = DateiAblage(datei).Laden(KlausurTests.Klausurinhalte);
        Assert.NotNull(stehen);                                         // clear_running kam nicht mehr
        Assert.Equal(Klausur.Bewertung, Json.Text(stehen["phase"]));

        var neu = Ablauf(pfad, DateiAblage(datei));
        Assert.Equal(KlausurAnsicht.Bewertung, neu.Zeigen().Ansicht);
        Assert.Equal(KlausurAnsicht.Ergebnis, neu.Abschliessen().Ansicht);
        Assert.Equal(1, Zeilen("pruefungen"));
        Assert.Equal(antworten, Zeilen(tabelle));
        Assert.Null(DateiAblage(datei).Laden(KlausurTests.Klausurinhalte));
        if (art == Klausur.Wiso)
        {
            Assert.Equal(30, antworten);
        }
    }

    /// <summary>Mitten im Waehlen der Fachrichtung hart beendet: genau eine ganze Zeile, ein gueltiger Wert.</summary>
    [Fact]
    public void AbsturzMittenImWaehlen()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        _ = new Lernstand(pfad);
        for (var runde = 0; runde < 3; runde++)
        {
            var gemeldet = AbsturzProbe.Starten(AbsturzProbe.ArtAusbildung, [pfad], 40 + runde * 37);
            using var db = VergleichsHilfe.Oeffnen(pfad);
            Assert.Equal("ok", (string)VergleichsHilfe.Abfrage(db, "PRAGMA quick_check")[0][0]!);
            var zeilen = VergleichsHilfe.Abfrage(db, "SELECT feld, wert, timestamp FROM abschlussprojekt WHERE projekt = 'zz-einstellungen'");
            Assert.Single(zeilen);
            Assert.Equal(Ausbildung.Feld, zeilen[0][0]);
            Assert.Equal(26, ((string)zeilen[0][2]!).Length);
            // ein gueltiger Wert (das Kind kann weitere Wahlen gebucht haben, deren Meldung der Test nicht mehr las)
            Assert.Contains((string)zeilen[0][1]!, Ausbildung.Werte);
            Assert.True(gemeldet.Count >= 40 + runde * 37);
            Assert.Equal(0L, (long)VergleichsHilfe.Abfrage(db, "SELECT COUNT(*) FROM abschlussprojekt WHERE projekt != 'zz-einstellungen'")[0][0]!);
        }
    }
}
