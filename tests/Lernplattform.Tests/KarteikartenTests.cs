using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L4 (Weisung P2-4): Speichern der Karteikarten genau wie app_gui.CardsView
/// (Python v0.63.2). Je Modus: was wann in card_events landet (mode, correct), hoechstens
/// einmal je Anzeige, Aufgedecktes ohne Bewertung als "angesehen" (correct NULL).
/// </summary>
public class KarteikartenTests
{
    private static readonly Lerninhalte Inhalte = Lerninhalte.Laden(Pfade.Inhalte);

    private sealed class Aufbau : IDisposable
    {
        private readonly TempOrdner _ordner = new();

        public Aufbau()
        {
            Lernstand = new Lernstand(Path.Combine(_ordner.Pfad, Lernstand.DateiName));
            var n = 0;
            Schreiber = new LernstandSchreiber(Lernstand, SpeichernTests.FesteUhr("2026-10-20T12:00:00Z"), () => "k-" + ++n);
            Karten = new Karteikarten(Inhalte, () => new StatusBuch(Antwortbuch.Lesen(Lernstand)), Schreiber);
        }

        public Lernstand Lernstand { get; }

        public LernstandSchreiber Schreiber { get; }

        public Karteikarten Karten { get; }

        /// <summary>Alle Zeilen aus card_events: (Frage, Modus, correct).</summary>
        public List<(string Frage, string Modus, long? Richtig)> Zeilen() =>
            Lernstand.Abfragen("SELECT question, mode, correct FROM card_events ORDER BY id")
                .Select(z => ((string)z[0]!, (string)z[1]!, (long?)z[2])).ToList();

        public void Dispose() => _ordner.Dispose();
    }

    [Fact]
    public void FreitextLeerSpeichertNichtsUndWarnt()
    {
        using var a = new Aufbau();
        var antwort = a.Karten.Pruefen("  \t\n ", null)!;
        Assert.Equal(KartenTexte.EingabeFehlt, antwort.Warnung);
        Assert.Empty(a.Zeilen());
        Assert.False(a.Karten.Gespeichert);
    }

    [Fact]
    public void FreitextSpeichertBeimErstenPruefenEinmal()
    {
        using var a = new Aufbau();
        a.Karten.Ueben([Inhalte.Karteikarten.First(k => k.Antwort!.Any(char.IsLower)).Frage!]);
        var karte = a.Karten.Karte!;
        // Gross/klein egal, Kernloesung irgendwo in der Eingabe (Python: a.lower() in eingabe.lower())
        var antwort = a.Karten.Pruefen("  Meine Antwort: " + karte.Antwort!.ToUpperInvariant() + " oder so ", null)!;
        Assert.Null(antwort.Warnung);
        Assert.True(antwort.Richtig);
        Assert.Equal(KartenTexte.FreitextRichtig, antwort.Text);
        Assert.Equal(KartenTexte.MusterloesungVor + karte.Text("a_full"), antwort.Loesung);
        // Zweites Pruefen (auch falsch) speichert nicht noch einmal
        Assert.False(a.Karten.Pruefen("ganz anders", null)!.Richtig);
        Assert.Equal([(karte.Frage!, KartenTexte.Freitext, (long?)1)], a.Zeilen());
        // Naechste Karte: wieder neu
        a.Karten.Weiter();
        Assert.False(a.Karten.Pruefen("ganz anders", null)!.Richtig);
        Assert.Equal(2, a.Zeilen().Count);
        Assert.Equal(0, a.Zeilen()[1].Richtig);
    }

    [Fact]
    public void MehrfachwahlWarntOhneAuswahlUndSpeichertEinmal()
    {
        using var a = new Aufbau();
        a.Karten.SetzeModus(KartenTexte.Mehrfachwahl);
        var karte = a.Karten.Karte!;
        Assert.Equal(KartenTexte.AuswahlFehlt, a.Karten.Pruefen(null, null)!.Warnung);
        Assert.Empty(a.Zeilen());
        var falsch = a.Karten.Optionen.First(o => o != karte.Antwort);
        var antwort = a.Karten.Pruefen(null, falsch)!;
        Assert.False(antwort.Richtig);
        Assert.Equal(KartenTexte.McFalschVor + karte.Antwort, antwort.Text);
        Assert.True(a.Karten.Pruefen(null, karte.Antwort)!.Richtig);
        Assert.Equal([(karte.Frage!, KartenTexte.Mehrfachwahl, (long?)0)], a.Zeilen());
    }

    [Fact]
    public void AufdeckenSpeichertMitBewertung()
    {
        using var a = new Aufbau();
        a.Karten.SetzeModus(KartenTexte.Aufdecken);
        var karte = a.Karten.Karte!;
        Assert.Equal(KartenTexte.LoesungAufdecken, a.Karten.PruefKnopf);
        var antwort = a.Karten.Pruefen(null, null)!;
        Assert.True(antwort.Bewerten);
        Assert.Empty(a.Zeilen());     // erst die Bewertung speichert
        a.Karten.Bewerten(false);
        Assert.Equal([(karte.Frage!, KartenTexte.Aufdecken, (long?)0)], a.Zeilen());
        // Erneut aufdecken: keine zweite Bewertung
        Assert.False(a.Karten.Pruefen(null, null)!.Bewerten);
        a.Karten.Weiter();
        Assert.Single(a.Zeilen());
    }

    /// <summary>Aufgedeckt, nicht bewertet: Weiter, Zurueck, Filter, Modus und Beenden speichern "angesehen" (NULL).</summary>
    [Fact]
    public void AufgedecktOhneBewertungWirdAlsAngesehenGespeichert()
    {
        using var a = new Aufbau();
        a.Karten.SetzeModus(KartenTexte.Aufdecken);
        var aktionen = new Action[]
        {
            a.Karten.Weiter, a.Karten.Zurueck, () => a.Karten.SetzeStatus(FragenStatus.Alle),
            () => a.Karten.SetzeModus(KartenTexte.Aufdecken), a.Karten.Beenden,
        };
        var erwartet = new List<(string, string, long?)>();
        foreach (var aktion in aktionen)
        {
            var frage = a.Karten.Karte!.Frage!;
            a.Karten.Pruefen(null, null);
            aktion();
            erwartet.Add((frage, KartenTexte.Aufdecken, null));
            Assert.Equal(erwartet, a.Zeilen());
            Assert.False(a.Karten.WartetAufBewertung);
        }
        // Beenden ohne offene Karte speichert nichts
        a.Karten.Beenden();
        Assert.Equal(erwartet, a.Zeilen());
    }

    [Fact]
    public void BlaetternLaeuftImKreis()
    {
        using var a = new Aufbau();
        var anzahl = a.Karten.Gefiltert.Count;
        Assert.Equal(Inhalte.Karteikarten.Count, anzahl);
        var erste = a.Karten.Karte;
        a.Karten.Zurueck();
        Assert.Equal(anzahl - 1, a.Karten.Index);
        a.Karten.Weiter();
        Assert.Same(erste, a.Karten.Karte);
        Assert.Equal(Beschriftungen.Einsetzen(KartenTexte.Zaehler, "1", Beschriftungen.MitPunkten(anzahl)), a.Karten.Zaehler);
        Assert.Empty(a.Zeilen());
    }

    /// <summary>Statusfilter wie apply_filter; der Zaehltext bleibt bei der Basis vor dem Statusfilter.</summary>
    [Fact]
    public void StatusfilterUndZaehltext()
    {
        using var a = new Aufbau();
        var bereich = Fachbereiche.Alle[0];
        a.Karten.SetzeFachbereich(bereich);
        var basis = Inhalte.Karteikarten.Count(k => k.Fachbereich == bereich);
        Assert.Equal(basis, a.Karten.Gefiltert.Count);
        var karte = a.Karten.Karte!;
        a.Karten.Pruefen("ganz anders", null);
        a.Karten.SetzeStatus(FragenStatus.Ueben);
        Assert.Equal([karte], a.Karten.Gefiltert);
        Assert.Equal(KartenTexte.ZaehlText(new Dictionary<string, int>
        {
            [FragenStatus.Offen] = basis - 1, [FragenStatus.Ueben] = 1, [FragenStatus.Fertig] = 0,
        }), a.Karten.ZaehlText);
        Assert.Equal(("Zu üben · zuletzt falsch", FragenStatus.Rot), a.Karten.StatusZeile);
        a.Karten.SetzeStatus(FragenStatus.Fertig);
        Assert.Empty(a.Karten.Gefiltert);
        Assert.Equal(KartenTexte.ZaehlerLeer, a.Karten.Zaehler);
        Assert.Null(a.Karten.Pruefen("x", null));
        // Ohne Statusfilter kommen unbearbeitete Karten zuerst, die falsch beantwortete danach
        a.Karten.SetzeStatus(FragenStatus.Alle);
        Assert.NotSame(karte, a.Karten.Karte);
    }

    [Fact]
    public void UebenZeigtNurDieGewaehltenKarten()
    {
        using var a = new Aufbau();
        var fragen = new[] { Inhalte.Karteikarten[5].Frage!, "gibt es nicht", Inhalte.Karteikarten[2].Frage! };
        a.Karten.Ueben(fragen);
        Assert.Equal([Inhalte.Karteikarten[5], Inhalte.Karteikarten[2]], a.Karten.Gefiltert);
        Assert.Equal(KartenTexte.Alle, a.Karten.Fachbereich);
    }

    [Fact]
    public void ThemaPasstZumFachbereich()
    {
        using var a = new Aufbau();
        var bereich = Fachbereiche.Alle[1];
        var thema = a.Karten.Themen.First(t => Inhalte.Karteikarten.Any(k => k.Thema == t.Id && k.Fachbereich == bereich));
        a.Karten.SetzeFachbereich(bereich, thema.Id);
        Assert.Equal(thema.Id, a.Karten.Thema);
        Assert.All(a.Karten.Gefiltert, k => Assert.Equal(thema.Id, k.Thema));
        // Thema eines anderen Fachbereichs faellt auf "Alle" zurueck
        a.Karten.SetzeFachbereich(Fachbereiche.Alle[0], thema.Id);
        Assert.Equal(KartenTexte.Alle, a.Karten.Thema);
    }

    /// <summary>Lernstand lehnt das Schreiben ab: Merker gesetzt, Anzeige laeuft weiter.</summary>
    [Fact]
    public void SpeichernGescheitertWirdGemeldet()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        File.WriteAllBytes(pfad, [.. "keine Datenbank"u8, .. new byte[300]]);
        var lernstand = new Lernstand(pfad);
        var karten = new Karteikarten(Inhalte, () => new StatusBuch(new Antwortbuch()), new LernstandSchreiber(lernstand));
        Assert.True(karten.Pruefen("x", null)!.Richtig is false);
        Assert.True(karten.LetztesSpeichernFehlgeschlagen);
    }
}
