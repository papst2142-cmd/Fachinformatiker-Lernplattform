using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.18.0 (L0, Weisung P12-424 Nico 1b/5a, P12-437 4a/5a): Pruefungssaetze nach dem Loeschen. L1: die heutige Datei
/// ist genau die Datei vor L0 ohne die Saetze der Loeschliste (Kennung, Fassung und hash16 je Stelle). L2: die App bietet
/// heute genau dieselben Saetze an wie vorher. 5a/4a: eine offene Pruefung eines geloeschten Satzes oder einer geloeschten
/// aelteren Fassung endet beim Oeffnen mit Hinweis und wird nicht gewertet; fertige Pruefungen bleiben im Verlauf.
/// </summary>
public sealed class EchtL0Tests
{
    private static List<LerninhalteTests.Geloescht> Saetze() =>
        [.. LerninhalteTests.GeloeschtListe().Where(g => g.Datei == EchtSaetze.DateiName)];

    /// <summary>hash16 wie loeschliste.py: SHA-256 von json.dumps(sort_keys=True, ensure_ascii=False, kompakt), 16 Zeichen.</summary>
    private static string Hash16(JsonNode satz) =>
        Convert.ToHexStringLower(SHA256.HashData(PythonJson.Utf8(PythonJson.Text(AlsPython(satz), sortiert: true))))[..16];

    private static object? AlsPython(JsonNode? knoten) => knoten switch
    {
        null => null,
        JsonObject o => Objekt(o),
        JsonArray a => a.Select(AlsPython).ToList(),
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.String => v.GetValue<string>(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => v.ToJsonString() is var t && long.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)
                ? n : (object)double.Parse(t, CultureInfo.InvariantCulture),
            _ => null,
        },
        _ => throw new InvalidOperationException(),
    };

    private static PyObjekt Objekt(JsonObject o)
    {
        var ergebnis = new PyObjekt();
        foreach (var (schluessel, wert) in o)
        {
            ergebnis.Add(schluessel, AlsPython(wert));
        }
        return ergebnis;
    }

    [Fact]
    public void L1_HeutigeDatei_GenauVorherOhneLoeschliste()
    {
        var vorher = JsonNode.Parse(EchtTestDatei.Vorher)!["saetze"]!.AsArray();
        var heute = JsonNode.Parse(EchtTestDatei.NachL0)!.AsObject();
        var weg = Saetze();
        Assert.Equal(16, weg.Count);
        foreach (var g in weg)
        {
            var satz = vorher[g.Stelle]!;
            Assert.Equal(g.Kennung, (string)satz["kennung"]!);
            Assert.Equal(g.Fassung, satz["fassung"] is { } f ? (long)f : 1L);
            Assert.Equal(g.Hash16, Hash16(satz));
        }
        var stellen = weg.Select(g => g.Stelle).ToHashSet();
        var soll = new JsonArray([.. vorher.Select((s, i) => (s, i)).Where(p => !stellen.Contains(p.i)).Select(p => p.s!.DeepClone())]);
        Assert.True(JsonNode.DeepEquals(soll, heute["saetze"]), "heutige Saetze sind nicht die alten ohne die Loeschliste");
        // ausser der Liste "saetze" ist alles gleich (Format, Hinweise)
        var kopfVorher = JsonNode.Parse(EchtTestDatei.Vorher)!.AsObject();
        kopfVorher.Remove("saetze");
        heute.Remove("saetze");
        Assert.True(JsonNode.DeepEquals(kopfVorher, heute));
        // jede Kennung steht heute genau einmal (keine aeltere Fassung neben der neueren)
        var kennungen = soll.Select(s => (string)s!["kennung"]!).ToList();
        Assert.Equal(kennungen.Count, kennungen.Distinct(StringComparer.Ordinal).Count());
        // Gegenprobe: um eine Stelle verschoben passt der Hash nicht mehr
        Assert.Contains(weg, g => Hash16(vorher[g.Stelle + 1]!) != g.Hash16);
    }

    [Fact]
    public void L2_AppBietetGenauDieselbenSaetzeAn()
    {
        var vorher = EchtTestDatei.App();
        // Stand nach L0 bis A 1.19.1 (seit A 1.20.0 kommen neue Fassungen dazu, EchtSchreibtischTests)
        var heute = EchtSaetze.Laden(EchtTestDatei.NachL0, [], EchtTestDatei.Geloescht());
        Assert.Empty(heute.Fehler);
        Assert.Empty(heute.Ausgeschlossen);
        foreach (var f in Fachrichtung.Alle)
        {
            foreach (var bereich in new[] { EchtSaetze.Ap1, EchtSaetze.B1, EchtSaetze.B2 })
            {
                Assert.Equal(vorher.Fuer(bereich, f.Id).Select(s => (s.Kennung, s.Fassung)),
                    heute.Fuer(bereich, f.Id).Select(s => (s.Kennung, s.Fassung)));
            }
        }
        // jede geloeschte Fassung steht in der Loeschliste der App, keine angebotene
        Assert.Equal(Saetze().Select(g => (g.Kennung!, (int)g.Fassung!.Value)), EchtTestDatei.Geloescht());
        Assert.All(heute.Saetze, s => Assert.False(heute.IstGeloescht(s.Kennung, s.Fassung), s.Kennung));
        // Gegenprobe: vorher stand der alte Satz ae_b1_01 noch im Bestand
        Assert.NotNull(vorher.Satz("ae_b1_01"));
        Assert.Null(heute.Satz("ae_b1_01"));
    }

    /// <summary>Datei vor L0 ohne die Saetze mit Feld "fassung" (Stand bis A 1.7.0, alle Saetze in Fassung 1).</summary>
    private static EchtBestand OhneNeueFassungen()
    {
        var wurzel = JsonNode.Parse(EchtTestDatei.Vorher)!.AsObject();
        var saetze = wurzel["saetze"]!.AsArray();
        foreach (var s in saetze.Where(s => s!["fassung"] is not null).ToList())
        {
            saetze.Remove(s);
        }
        return EchtSaetze.Laden(Encoding.UTF8.GetBytes(wurzel.ToJsonString()), EchtTestDatei.Ausgeschlossen());
    }

    private sealed class Lauf
    {
        public JsonObject Werte = new();
        public DateTimeOffset Jetzt = DateTimeOffset.Parse("2026-11-27T08:00:00Z", CultureInfo.InvariantCulture);
        public readonly Lernstand Lernstand;
        private readonly LernstandSchreiber _schreiber;

        public Lauf(string ordner)
        {
            Lernstand = new Lernstand(Path.Combine(ordner, Lernstand.DateiName));
            _schreiber = new LernstandSchreiber(Lernstand, SpeichernTests.FesteUhr(() => Jetzt));
        }

        public EchtAblauf Ablauf(EchtBestand bestand) =>
            new(() => bestand, new EchtAblage(() => Werte.DeepClone().AsObject(), a =>
            {
                var neu = Werte.DeepClone().AsObject();
                a(neu);
                Werte = neu;
                return true;
            }), () => _schreiber, () => Jetzt, KlausurTests.Berlin);

        public long Pruefungen => (long)Lernstand.Abfragen("SELECT COUNT(*) FROM pruefungen")[0][0]!;
    }

    [Theory]
    [InlineData("ae_b1_01", 1)] // geloeschter Satz (alte FIAE-Struktur)
    [InlineData("ae_b1_07", 1)] // geloeschte aeltere Fassung, Fassung 2 bleibt
    [InlineData("ae_b2_08", 1)]
    public void OffenerLauf_GeloeschterSatz_EndetMitHinweis_NichtGewertet(string kennung, int fassung)
    {
        using var ordner = new TempOrdner();
        var lauf = new Lauf(ordner.Pfad);
        var alt = EchtTestDatei.App();
        var art = EchtArten.Art(Fachrichtung.Nach("ae"), kennung.Contains("_b1_", StringComparison.Ordinal) ? EchtSaetze.B1 : EchtSaetze.B2);
        // eine fertige Pruefung vorher bleibt im Verlauf
        var fertig = lauf.Ablauf(alt);
        var neuer = alt.Fuer(EchtSaetze.B1, "ae")[0];
        Assert.Equal(EchtAnsicht.Lauf, fertig.Starten(neuer, EchtArten.Art(Fachrichtung.Nach("ae"), EchtSaetze.B1)).Ansicht);
        Assert.Equal(EchtAnsicht.Bewertung, fertig.Abgeben(auto: false).Ansicht);
        Assert.Equal(EchtAnsicht.Ergebnis, fertig.Abschliessen().Ansicht);
        Assert.Equal(1L, lauf.Pruefungen);
        // vor dem Update (A 1.17.0): Lauf der alten Fassung begonnen, eine Antwort gespeichert
        // Fassung 1 stand bis A 1.7.0 allein in der Datei (danach trat Fassung 2 an ihre Stelle): Datei vor L0 ohne die neuen Fassungen
        var stand = fassung == 1 && alt.Satz(kennung)!.Fassung > 1 ? OhneNeueFassungen() : alt;
        var satz = stand.Satz(kennung)!;
        Assert.Equal(fassung, satz.Fassung);
        var vorher = lauf.Ablauf(stand);
        Assert.Equal(EchtAnsicht.Lauf, vorher.Starten(satz, art).Ansicht);
        Assert.True(vorher.Festhalten([(EchtBewertung.Feld(1, satz.Aufgaben[0].Teile[0].Nr, "1"), "meine Antwort")]));
        Assert.NotNull(lauf.Werte[EchtAblage.Schluessel]);
        // nach dem Update (A 1.18.0): beim Oeffnen beendet, Hinweis, nicht gewertet
        lauf.Jetzt = lauf.Jetzt.AddMinutes(5);
        var nachher = lauf.Ablauf(EchtTestDatei.AppHeute());
        var schritt = nachher.Zeigen();
        Assert.Equal(EchtAnsicht.Auswahl, schritt.Ansicht);
        Assert.Equal(EchtTexte.SatzEntfernt, schritt.Hinweis);
        Assert.Equal(KartenTexte.HinweisTitel, schritt.HinweisTitel);
        Assert.Null(nachher.Zustand);
        Assert.Null(lauf.Werte[EchtAblage.Schluessel]);
        Assert.Equal(1L, lauf.Pruefungen);
        // beim naechsten Oeffnen kein Hinweis mehr
        Assert.Null(lauf.Ablauf(EchtTestDatei.AppHeute()).Zeigen().Hinweis);
    }

    [Fact]
    public void OffenerLauf_NeueFassung_GehtWeiter_Gegenprobe()
    {
        using var ordner = new TempOrdner();
        var lauf = new Lauf(ordner.Pfad);
        var heute = EchtTestDatei.AppHeute();
        var satz = heute.Satz("ae_b1_07")!;
        Assert.Equal(2, satz.Fassung);
        var art = EchtArten.Art(Fachrichtung.Nach("ae"), EchtSaetze.B1);
        var vorher = lauf.Ablauf(EchtTestDatei.App());
        Assert.Equal(EchtAnsicht.Lauf, vorher.Starten(EchtTestDatei.App().Satz("ae_b1_07")!, art).Ansicht);
        var feld = EchtBewertung.Feld(1, satz.Aufgaben[0].Teile[0].Nr, "1");
        Assert.True(vorher.Festhalten([(feld, "meine Antwort")]));
        var nachher = lauf.Ablauf(heute);
        var schritt = nachher.Zeigen();
        Assert.Equal(EchtAnsicht.Lauf, schritt.Ansicht);
        Assert.NotEqual(EchtTexte.SatzEntfernt, schritt.Hinweis);
        Assert.Equal("meine Antwort", Json.Text(nachher.Zustand!["antworten"]![feld]));
    }
}
