using System.Globalization;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Projekt;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W3 (PB8): Kern des Abschlussprojekts gegen G34 (tests/Vergleich/abschlussprojekt.json,
/// Teile zahl, rechnen, export, projekte, fragen, rueckfrage, texte, grenze; vom echten Python
/// v0.63.2 erzeugt, zusaetzlich vom unabhaengigen Skript nur aus der Spezifikation in der CI).
/// Zahlen werden numerisch verglichen (Python schreibt bei Vorgaben int), alles andere genau.
/// </summary>
public class AbschlussprojektTests
{
    internal static readonly JsonObject G34 = JsonNode.Parse(File.ReadAllText(Pfade.Vergleich("abschlussprojekt.json")),
        documentOptions: new() { MaxDepth = 256 })!.AsObject();

    internal static readonly Lerninhalte Inhalte = Lerninhalte.Laden(Pfade.Inhalte);

    internal static readonly List<ProjektFrage> Fragen = Fachgespraech.Laden(Pfade.Inhalte);

    /// <summary>Gleich wie Python: Zahlen nach Wert (int 40 = 40.0), sonst Art und Inhalt genau.</summary>
    internal static void Gleich(JsonNode? erwartet, JsonNode? ist, string pfad)
    {
        if (erwartet is null || ist is null)
        {
            Assert.True(erwartet is null && ist is null, pfad + ": " + erwartet?.ToJsonString() + " / " + ist?.ToJsonString());
            return;
        }
        switch (erwartet)
        {
            case JsonObject o:
                var io = Assert.IsType<JsonObject>(ist);
                Assert.Equal(o.Select(p => p.Key).Order(StringComparer.Ordinal), io.Select(p => p.Key).Order(StringComparer.Ordinal));
                foreach (var (k, v) in o)
                {
                    Gleich(v, io[k], pfad + "/" + k);
                }
                return;
            case JsonArray a:
                var ia = Assert.IsType<JsonArray>(ist);
                Assert.True(a.Count == ia.Count, pfad + ": Laenge " + a.Count + " / " + ia.Count);
                for (var i = 0; i < a.Count; i++)
                {
                    Gleich(a[i], ia[i], pfad + "[" + i + "]");
                }
                return;
        }
        var e = erwartet.AsValue();
        var w = ist.AsValue();
        Assert.True(e.GetValueKind() == w.GetValueKind(), pfad + ": Art " + e.GetValueKind() + " / " + w.GetValueKind());
        if (e.GetValueKind() == System.Text.Json.JsonValueKind.Number)
        {
            Assert.True(D(e) == D(w), pfad + ": " + e.ToJsonString() + " / " + w.ToJsonString());
        }
        else
        {
            Assert.True(e.ToJsonString() == w.ToJsonString(), pfad + ": " + e.ToJsonString() + " / " + w.ToJsonString());
        }
    }

    /// <summary>Zahl eines JSON-Wertes (int oder Kommazahl) als double.</summary>
    internal static double D(JsonNode wert) => double.Parse(wert.ToJsonString(), CultureInfo.InvariantCulture);

    internal static JsonNode? J(object? wert) => wert switch
    {
        null => null,
        JsonNode n => n,
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        double d => JsonValue.Create(d),
        System.Numerics.BigInteger g => JsonValue.Create((double)g),
        _ => throw new InvalidDataException(wert.GetType().Name),
    };

    internal static JsonArray L(params object?[] werte) => [.. werte.Select(J)];

    internal static JsonArray L<T>(IEnumerable<T> werte, Func<T, object?> f) => [.. werte.Select(w => J(f(w)))];

    /// <summary>Felder eines Falls [[feld, wert], ...] in der Reihenfolge der Datei.</summary>
    internal static OrderedDictionary<string, string> Felder(JsonNode paare)
    {
        var felder = new OrderedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in paare.AsArray())
        {
            felder[(string)p![0]!] = (string)p[1]!;
        }
        return felder;
    }

    internal static IEnumerable<(string Name, OrderedDictionary<string, string> Felder)> Faelle() =>
        G34["rechnen"]!["faelle"]!.AsArray().Select(f => ((string)f![0]!, Felder(f[1]!)));

    [Fact]
    public void ZahlWiePython()
    {
        var ist = new JsonArray();
        foreach (var fall in G34["zahl"]!.AsArray())
        {
            var text = (string)fall![0]!;
            var vorgabe = D(fall[1]!);
            ist.Add(L(text, J(fall[1]!.DeepClone()), Abschlussprojekt.Zahl(text, vorgabe)));
        }
        Gleich(G34["zahl"], ist, "zahl");
        Assert.Equal(56, ist.Count);
    }

    internal static JsonObject Rechnung(OrderedDictionary<string, string> felder)
    {
        var (zeilen, summe) = Abschlussprojekt.Zeitplan(felder);
        var k = Abschlussprojekt.Kostenrechnung(felder);
        var (seiten, ziel) = Abschlussprojekt.Seitenschaetzung(felder);
        var (erledigt, gesamt) = Abschlussprojekt.Fortschritt(felder);
        return new JsonObject
        {
            ["zeitplan"] = L(zeilen, z => L(z.Schluessel, z.Name, z.Stunden)),
            ["summe"] = summe,
            ["warnungen"] = L(Abschlussprojekt.Warnungen(felder, 40), w => w),
            ["kosten"] = new JsonObject
            {
                ["stunden"] = k.Stunden,
                ["personal"] = k.Personal,
                ["material"] = L(k.Material, m => L(m.Name, m.Betrag)),
                ["material_summe"] = k.MaterialSumme,
                ["gesamt"] = k.Gesamt,
                ["netto_monat"] = k.NettoMonat,
                ["amortisation"] = J(k.Amortisation),
            },
            ["kostenzeilen"] = L(Abschlussprojekt.Kostenzeilen(felder), z => z),
            ["seiten"] = L(seiten, ziel),
            ["erledigt"] = L(erledigt, gesamt),
            ["antrag_offen"] = L(Abschlussprojekt.AntragFehlt(felder), a => a),
            ["schaetzung"] = J(Abschlussprojekt.Schaetzung(felder)),
            ["titel"] = Abschlussprojekt.Titel(felder),
            ["dateiname"] = L(Abschlussprojekt.ExportName(felder, "pdf"), Abschlussprojekt.ExportName(felder, "txt")),
        };
    }

    [Fact]
    public void RechnenWiePython()
    {
        var g = G34["rechnen"]!;
        var faelle = Faelle().ToList();
        Assert.Equal(["leer", "festwert", "warnung", "halb", "frei", "unicode", "gross", "lang"], faelle.Select(f => f.Name));
        Gleich(g["je_fall"], L(faelle, f => L(f.Name, Rechnung(f.Felder))), "je_fall");
        Gleich(g["stunden_text"], L(g["stunden_text"]!.AsArray(), r => L(r![0]!.DeepClone(), Abschlussprojekt.StundenText(D(r[0]!)))), "stunden_text");
        Gleich(g["euro"], L(g["euro"]!.AsArray(), r => L(r![0]!.DeepClone(), Abschlussprojekt.Euro(D(r[0]!)))), "euro");
        Gleich(g["dateiname"], L(g["dateiname"]!.AsArray(), r =>
        {
            var felder = new Dictionary<string, string> { ["titel"] = (string)r![0]! };
            return L((string)r[0]!, L(Abschlussprojekt.ExportName(felder, "pdf"), Abschlussprojekt.ExportName(felder, "txt")));
        }), "dateiname");
        // Belege der Wortliste: 5 von 8 Stunden "62 %" (halb gerade), Unterstreichung in Codepunkten
        Assert.Contains("Die Dokumentation nimmt 62 % der Zeit ein (üblich höchstens 15 bis 20 %).",
            Abschlussprojekt.Warnungen(faelle.First(f => f.Name == "halb").Felder, 40));
        Assert.Equal("0,12 €", Abschlussprojekt.Euro(0.125));
    }

    [Fact]
    public void ExportWiePython()
    {
        var ist = new JsonArray();
        foreach (var (name, felder) in Faelle())
        {
            var text = Abschlussprojekt.ExportText(felder, 40);
            var eintrag = new JsonObject
            {
                ["bloecke"] = L(Abschlussprojekt.ExportBloecke(felder, 40), b => L(b.Art, b.Text)),
                ["text"] = text,
            };
            if (name is "leer" or "unicode")
            {
                eintrag["datei_pc"] = Convert.ToHexStringLower(Abschlussprojekt.TextDatei(text, handy: false));
                eintrag["datei_handy"] = Convert.ToHexStringLower(Abschlussprojekt.TextDatei(text, handy: true));
            }
            ist.Add(L(name, eintrag));
        }
        Gleich(G34["export"], ist, "export");
        var unicode = Abschlussprojekt.ExportText(Faelle().First(f => f.Name == "unicode").Felder, 40).Split('\n');
        Assert.Equal(["Titel 😀 äöü", "===========", ""], unicode[..3]);
    }

    [Fact]
    public void GrenzeJeAusbildungWiePythonMitGesetzterGrenze()
    {
        var ist = new JsonArray();
        foreach (var grenze in new[] { 40, 80 })
        {
            foreach (var soll in new[] { 30, 40, 41, 80, 81 })
            {
                var felder = new OrderedDictionary<string, string> { ["phase_durchfuehrung"] = (soll - 26).ToString(CultureInfo.InvariantCulture) };
                ist.Add(L(grenze, soll, new JsonObject
                {
                    ["felder"] = L(felder, p => L(p.Key, p.Value)),
                    ["summe"] = Abschlussprojekt.Zeitplan(felder).Summe,
                    ["warnungen"] = L(Abschlussprojekt.Warnungen(felder, grenze), w => w),
                    ["bloecke"] = L(Abschlussprojekt.ExportBloecke(felder, grenze), b => L(b.Art, b.Text)),
                    ["text"] = Abschlussprojekt.ExportText(felder, grenze),
                }));
            }
        }
        Gleich(G34["grenze"], ist, "grenze");
        // Die Grenze kommt aus der Ausbildung (G36): AE 80, sonst 40
        Assert.Equal([40, 80, 40, 40], Ausbildung.Werte.Select(Abschlussprojekt.MaxStunden));
    }

    [Fact]
    public void FragenUndRueckfrageWiePython()
    {
        Assert.Equal(60, Fragen.Count);
        var auswahl = L(G34["fragen"]!["auswahl"]!.AsArray(), a =>
            L((string)a![0]!, L(Abschlussprojekt.FragenFuer(new Dictionary<string, string> { ["fachbereich"] = (string)a[0]! }, Fragen), f => f.Id)));
        var bewertung = L(G34["fragen"]!["bewertung"]!.AsArray(), b =>
        {
            var wert = (string?)b![0];
            var felder = wert is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["fg_a01"] = wert };
            return L(wert, J(Abschlussprojekt.Bewertung(felder, "a01")));
        });
        Gleich(G34["fragen"], new JsonObject { ["auswahl"] = auswahl, ["bewertung"] = bewertung }, "fragen");
        Gleich(G34["rueckfrage"], L(G34["rueckfrage"]!.AsArray(), r =>
            L((string)r![0]!, (bool)r[1]!, Abschlussprojekt.Rueckfrage(new Dictionary<string, string> { ["titel"] = (string)r[0]! }, (bool)r[1]!))),
            "rueckfrage");
    }

    [Fact]
    public void TexteWiePython()
    {
        var ist = new JsonObject
        {
            ["reiter"] = L(Abschlussprojekt.Reiter, r => L(r.Schluessel, r.Name)),
            ["uebersicht"] = L(Abschlussprojekt.Uebersicht, r => L(r.Schluessel, r.Name)),
            ["antrag"] = L(Abschlussprojekt.Antrag, f => L(f.Schluessel, f.Name, f.Zusatz)),
            ["phasen"] = L(Abschlussprojekt.Phasen, p => L(p.Schluessel, p.Name, p.Vorgabe)),
            ["kosten"] = L(Abschlussprojekt.Kostenfelder, f => L(f.Schluessel, f.Name, f.Zusatz)),
            ["kapitel"] = L(Abschlussprojekt.Kapitel, f => L(f.Schluessel, f.Name, f.Zusatz)),
            ["checklisten"] = L(new[] { Abschlussprojekt.ChecklisteUebersicht, Abschlussprojekt.ChecklisteAntrag(40), Abschlussprojekt.ChecklistePraesentation },
                liste => L(liste, c => L(c.Schluessel, c.Text))),
            ["zahlen"] = new JsonObject
            {
                ["max_stunden"] = Abschlussprojekt.MaxStunden("si"),
                ["doku_anteil"] = Abschlussprojekt.DokuAnteilMax,
                ["zeichen_je_seite"] = Abschlussprojekt.ZeichenJeSeite,
                ["seiten"] = Abschlussprojekt.SeitenVorgabe,
                ["sachkosten_zeilen"] = Abschlussprojekt.Sachkostenzeilen,
            },
            ["rueckfrage_titel"] = ProjektTexte.RueckfrageTitel,
        };
        Gleich(G34["texte"], ist, "texte");
    }

    /// <summary>Teil "projekte": der Ablauf der Spezifikation (Abschnitt 4) in einer Datenbank, feste Zeit, uid u01, u02, ...</summary>
    [Fact]
    public void ProjekteWiePython()
    {
        using var ordner = new TempOrdner();
        var jetzt = DateTimeOffset.MinValue;
        var zaehler = 0;
        var schreiber = new LernstandSchreiber(new Lernstand(Path.Combine(ordner.Pfad, "lernstand.db")),
            new Uhr(() => jetzt, TimeZoneInfo.Utc), () => "u" + (++zaehler).ToString("00", CultureInfo.InvariantCulture));
        void Setze(string uhrzeit) => jetzt = DateTimeOffset.ParseExact("2026-10-22 " + uhrzeit + "+00:00", "yyyy-MM-dd HH:mm:ss.ffffffzzz",
            CultureInfo.InvariantCulture);
        foreach (var (uhrzeit, projekt, titel) in new[] { ("08:00:00.000001", "p-b", "Beta"), ("08:00:00.000002", "p-a", "alpha"),
            ("08:00:00.000003", "p-c", ""), ("08:00:00.000004", "p-d", "Delta") })
        {
            Setze(uhrzeit);
            Assert.True(schreiber.ProjektFeldSpeichern(projekt, "titel", titel));
        }
        Setze("08:00:00.000005");
        schreiber.ProjektFeldSpeichern("p-d", "ziel", "Text D");
        var ergebnisse = new JsonArray();
        Setze("08:01:00.000000");
        ergebnisse.Add(Abschlussprojekt.Loeschen(schreiber, "p-d"));
        Setze("08:02:00.000000");
        ergebnisse.Add(Abschlussprojekt.VorlageUebernehmen(schreiber, "p-c", Inhalte.Testprojekte[0]));
        Setze("08:02:30.000000");
        ergebnisse.Add(Abschlussprojekt.VorlageUebernehmen(schreiber, "p-c", Inhalte.Testprojekte[0]));
        Setze("08:03:00.000000");
        ergebnisse.Add(Abschlussprojekt.VorlageUebernehmen(schreiber, "p-a", Inhalte.Testprojekte[49]));
        Setze("08:04:00.000000");
        ergebnisse.Add(Abschlussprojekt.Loeschen(schreiber, "p-b"));
        foreach (var (uhrzeit, projekt, titel) in new[] { ("08:05:00.000001", "p-h", "Alpha"), ("08:05:00.000002", "p-e", "Äpfel"),
            ("08:05:00.000003", "p-f", "😀 Emoji"), ("08:05:00.000004", "p-g", " Privat") })
        {
            Setze(uhrzeit);
            schreiber.ProjektFeldSpeichern(projekt, "titel", titel);
        }
        var projekte = Abschlussprojekt.Projekte(schreiber.ProjektFelder());
        using var db = VergleichsHilfe.Oeffnen(Path.Combine(ordner.Pfad, "lernstand.db"));
        var zeilen = VergleichsHilfe.Abfrage(db, "SELECT timestamp, projekt, feld, wert, uid FROM abschlussprojekt ORDER BY id");
        string Aktiv(string? aktiv, OrderedDictionary<string, OrderedDictionary<string, string>> p)
        {
            var ergebnis = Abschlussprojekt.AktivesProjekt(p, aktiv);
            return p.Count == 0 ? (ergebnis.Length == 12 && !p.ContainsKey(ergebnis) ? "neu" : ergebnis) : ergebnis;
        }
        var ist = new JsonObject
        {
            ["zeilen"] = L(zeilen, z => L(z)),
            ["ergebnisse"] = ergebnisse,
            ["projekte"] = L(projekte, p => L(p.Key, L(p.Value, f => L(f.Key, f.Value)))),
            ["menue"] = L(Abschlussprojekt.Menue(projekte), m => m),
            ["titel"] = L(projekte, p => L(p.Key, Abschlussprojekt.Titel(p.Value))),
            ["aktiv"] = L(Aktiv(null, projekte), Aktiv("p-d", projekte), Aktiv("p-c", projekte), Aktiv("p-b", projekte), Aktiv(null, [])),
        };
        Gleich(G34["projekte"], ist, "projekte");
    }

    /// <summary>
    /// Die reservierte Zeile der Ausbildung ist nie ein Projekt (Weisung P4-8, Auflage zu Diff A):
    /// nicht in der Liste, nicht im Menue, nie aktiv, nie geloescht, keine Vorlage (Kern-Ebene).
    /// </summary>
    [Fact]
    public void ZeileDerAusbildungIstNieEinProjekt()
    {
        using var ordner = new TempOrdner();
        var schreiber = new LernstandSchreiber(new Lernstand(Path.Combine(ordner.Pfad, "lernstand.db")));
        Assert.True(Ausbildung.Waehlen(schreiber, "ae"));
        Assert.Empty(Abschlussprojekt.Projekte(schreiber.ProjektFelder()));
        var neu = Abschlussprojekt.AktivesProjekt(Abschlussprojekt.Projekte(schreiber.ProjektFelder()), Ausbildung.Zeile);
        Assert.NotEqual(Ausbildung.Zeile, neu);
        Assert.False(Abschlussprojekt.Loeschen(schreiber, Ausbildung.Zeile));
        Assert.Equal(0, Abschlussprojekt.VorlageUebernehmen(schreiber, Ausbildung.Zeile, Inhalte.Testprojekte[0]));
        Assert.Equal("ae", Ausbildung.Gespeichert(schreiber));
        Assert.Equal(["fachrichtung"], schreiber.ProjektFelder()[Ausbildung.Zeile].Keys);
        // mit einem echten Projekt daneben
        Assert.True(schreiber.ProjektFeldSpeichern("p1", "titel", "Echt"));
        var projekte = Abschlussprojekt.Projekte(schreiber.ProjektFelder());
        Assert.Equal(["p1"], projekte.Keys);
        Assert.Equal(["p1"], Abschlussprojekt.Menue(projekte));
        Assert.Equal("p1", Abschlussprojekt.AktivesProjekt(projekte, Ausbildung.Zeile));
        Assert.Equal(80, Abschlussprojekt.MaxStunden(Ausbildung.Aktuell(schreiber)));
    }
}
