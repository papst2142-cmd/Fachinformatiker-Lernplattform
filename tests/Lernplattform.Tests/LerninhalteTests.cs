using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Projekt;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Zufall;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 9 W2 (Weisung P9-5, Plan Phase 9 Abschnitte 5 und 12.1): Ordner inhalte_csharp/lernen mit
/// Ladepruefung (Meldungen gleich dem unabhaengigen Skript werkzeuge/vergleich/pruefe_lerninhalte.py,
/// Vergleichswerte G52), Einteilung der bisherigen Inhalte (einteilung_bisher.json), Sicht je
/// Fachrichtung, Klausur b1/b2 fuer AE mit Probe-Inhalten, "SI unveraendert", "Wechsel ohne Verlust"
/// und Abgleich zweier Geraete mit verschiedener Wahl. Nur Testdaten (A3).
/// </summary>
public class LerninhalteTests
{
    private static readonly JsonElement G52 = VergleichsHilfe.Lade("lerninhalte_pruefung.json");
    private static readonly string Lernen = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen");
    private static readonly string Probe = Path.Combine(Pfade.Wurzel, "tests", "Lerninhalte", "probe");
    private static readonly IReadOnlyList<(string Tabelle, string Alt, string Neu)> Umbenennungen =
        Core.Daten.Umbenennungen.Laden(Pfade.Inhalte);

    /// <summary>Datei aus dem ersten Ordner, der sie hat (Name ohne ".json"), sonst null.</summary>
    private static Func<string, byte[]?> Ordner(params string[] ordner) => name =>
        ordner.Select(o => Path.Combine(o, name + ".json")).FirstOrDefault(File.Exists) is { } pfad ? File.ReadAllBytes(pfad) : null;

    /// <summary>Immer frisch geladen: MitLernen setzt die Zugehoerigkeit der bisherigen Eintraege.</summary>
    private static Lerninhalte Echt() => Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte), Ordner(Lernen));

    private static Lerninhalte MitProbe() => Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte), Ordner(Probe, Lernen));

    private static List<string> Texte(JsonElement liste) => [.. liste.EnumerateArray().Select(e => e.GetString()!)];

    // -- Ladepruefung (G52) --------------------------------------------------------------------

    [Fact]
    public void EchteUndProbeOhneMeldung_GleichSkript()
    {
        Assert.Equal(Texte(G52.GetProperty("echt")), InhaltePruefung.PruefenLernen(Echt(), Umbenennungen));
        Assert.Empty(InhaltePruefung.PruefenLernen(Echt(), Umbenennungen));
        Assert.Equal(Texte(G52.GetProperty("probe")), InhaltePruefung.PruefenLernen(MitProbe(), Umbenennungen));
        Assert.Empty(InhaltePruefung.PruefenLernen(MitProbe(), Umbenennungen));
    }

    public static TheoryData<string> Faelle()
    {
        var daten = new TheoryData<string>();
        foreach (var fall in FaelleDatei()["faelle"]!.AsArray())
        {
            daten.Add(fall!["name"]!.GetValue<string>());
        }
        return daten;
    }

    private static JsonNode FaelleDatei() => JsonNode.Parse(File.ReadAllBytes(Path.Combine(Pfade.Wurzel, "tests", "Lerninhalte", "faelle.json")))!;

    /// <summary>Jeder Fehlerfall (Aenderung an einer Kopie der Probe) liefert genau die Meldungen des Skripts, mindestens eine.</summary>
    [Theory]
    [MemberData(nameof(Faelle))]
    public void Fehlerfall_GleichSkript(string name)
    {
        var fall = FaelleDatei()["faelle"]!.AsArray().Single(f => f!["name"]!.GetValue<string>() == name)!;
        var dateien = new Dictionary<string, JsonNode>();
        JsonNode Datei(string n) => dateien.TryGetValue(n, out var d) ? d
            : dateien[n] = JsonNode.Parse(Ordner(Probe, Lernen)(n) ?? throw new InvalidOperationException(n))!;
        foreach (var aenderung in fall["aenderungen"]!.AsArray())
        {
            var teile = aenderung!.AsArray();
            JsonNode ziel = Datei(teile[0]!.GetValue<string>());
            var pfad = teile[1]!.AsArray();
            for (var i = 0; i < pfad.Count - 1; i++)
            {
                ziel = Schritt(ziel, pfad[i]!)!;
            }
            var letzter = pfad[^1]!;
            var art = teile[2]!.GetValue<string>();
            var wert = teile[3]?.DeepClone();
            if (ziel is JsonArray liste)
            {
                var index = letzter.GetValue<int>();
                if (art == "setze") { liste[index] = wert; } else { liste.RemoveAt(index); }
            }
            else
            {
                var schluessel = letzter.GetValue<string>();
                if (art == "setze") { ziel.AsObject()[schluessel] = wert; } else { Assert.True(ziel.AsObject().Remove(schluessel), schluessel); }
            }
        }
        var lesen = Ordner(Probe, Lernen);
        var inhalte = Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte),
            n => dateien.TryGetValue(n, out var d) ? Encoding.UTF8.GetBytes(d.ToJsonString()) : lesen(n));
        var soll = Texte(G52.GetProperty("faelle").GetProperty(name));
        Assert.NotEmpty(soll);
        Assert.Equal(soll, InhaltePruefung.PruefenLernen(inhalte, Umbenennungen));
    }

    private static JsonNode? Schritt(JsonNode knoten, JsonNode teil) =>
        knoten is JsonArray liste ? liste[teil.GetValue<int>()] : knoten.AsObject()[teil.GetValue<string>()];

    [Fact]
    public void AlleFehlerfaelleAuchImSkript()
    {
        Assert.Equal(FaelleDatei()["faelle"]!.AsArray().Select(f => f!["name"]!.GetValue<string>()).Order(StringComparer.Ordinal),
            G52.GetProperty("faelle").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void KaputteLernenDatei_BisherigeBleiben()
    {
        var fehler = new List<Exception>();
        var lesen = Ordner(Probe, Lernen);
        var inhalte = Lerninhalte.MitLernenSicher(() => Lerninhalte.Laden(Pfade.Inhalte),
            n => n == "karteikarten" ? Encoding.UTF8.GetBytes("[{\"cat\": \"gibtsnicht\", \"q\": \"x\"}]") : lesen(n), fehler.Add);
        Assert.Single(fehler);
        var vorher = Lerninhalte.Laden(Pfade.Inhalte);
        Assert.Equal(Schluessel(vorher.Karteikarten), Schluessel(inhalte.Fuer("si").Karteikarten));
        Assert.Equal(Schluessel(vorher.Quizfragen), Schluessel(inhalte.Fuer("si").Quizfragen));
        Assert.Empty(inhalte.Fuer("ae").Karteikarten);     // ohne Einteilung gilt si
        Assert.Empty(InhaltePruefung.PruefenLernen(Lerninhalte.MitLernenSicher(() => Lerninhalte.Laden(Pfade.Inhalte), Ordner(Lernen), fehler.Add), Umbenennungen));
        Assert.Single(fehler);
    }

    // -- Einteilung und Sicht ------------------------------------------------------------------

    private static int Fachfragen(Lerninhalte inhalte, string fr) => Fachgespraech.Laden(Pfade.Inhalte)
        .Count(f => inhalte.Einteilung.Wert(Einteilung.Fachgespraech, f.Id) is var w && (w == Fachrichtungen.Gemeinsam || w == fr));

    [Theory]
    [InlineData("echt")]
    [InlineData("probe")]
    public void SichtJeFachrichtung_GleichSkript(string welche)
    {
        var alle = welche == "echt" ? Echt() : MitProbe();
        foreach (var fr in Fachrichtungen.Werte)
        {
            var soll = G52.GetProperty("sichten").GetProperty(welche).GetProperty(fr);
            var sicht = alle.Fuer(fr);
            Assert.Equal(fr, sicht.Sicht);
            Assert.Equal(soll.GetProperty("karteikarten").GetInt32(), sicht.Karteikarten.Count);
            Assert.Equal(soll.GetProperty("quizfragen").GetInt32(), sicht.Quizfragen.Count);
            Assert.Equal(soll.GetProperty("szenarien_ap1").GetInt32(), sicht.Ap1Szenarien.Count);
            Assert.Equal(soll.GetProperty("szenarien_ap2").GetInt32(), sicht.Ap2Szenarien.Count);
            Assert.Equal(soll.GetProperty("projektarbeiten").GetInt32(), sicht.Testprojekte.Count);
            Assert.Equal(soll.GetProperty("fachgespraech").GetInt32(), Fachfragen(alle, fr));
        }
    }

    [Fact]
    public void Einteilung_ZaehlungGleichSkript_JederBisherigeGenauEinmal()
    {
        var alle = Echt();
        var e = alle.Einteilung;
        foreach (var art in G52.GetProperty("einteilung").EnumerateObject())
        {
            foreach (var wert in art.Value.EnumerateObject())
            {
                Assert.Equal(wert.Value.GetInt32(), e.Anzahl(art.Name, wert.Name));
            }
        }
        var bisher = alle.Bisher;
        var schluessel = new Dictionary<string, List<string>>
        {
            [Einteilung.Karteikarten] = [.. bisher.Karteikarten.Select(k => k.Frage!)],
            [Einteilung.Quizfragen] = [.. bisher.Quizfragen.Take(bisher.AnzahlQuizfragenDatei).Select(q => q.Frage!)],
            [Einteilung.Ap1] = [.. bisher.Ap1Szenarien.Select(s => s.Titel!)],
            [Einteilung.Ap2] = [.. bisher.Ap2Szenarien.Select(s => s.Titel!)],
            [Einteilung.Projekte] = [.. bisher.Testprojekte.Select(s => s.Titel!)],
            [Einteilung.Fachgespraech] = [.. Fachgespraech.Laden(Pfade.Inhalte).Select(f => f.Id)],
        };
        Assert.Equal(Einteilung.Arten.Order(StringComparer.Ordinal), schluessel.Keys.Order(StringComparer.Ordinal));
        foreach (var (art, liste) in schluessel)
        {
            // jeder Schluessel der Datei gibt es in inhalte/ (keine Leichen), Werte nur si oder gemeinsam
            Assert.All(e.Schluessel(art), s => Assert.Contains(s, liste));
            Assert.Equal(liste.Distinct().Count(), e.Anzahl(art, "si") + e.Anzahl(art, "gemeinsam"));
            Assert.Equal(e.Schluessel(art).Count, e.Anzahl(art, "si") + e.Anzahl(art, "gemeinsam"));
        }
        // AP2-Szenarien und Testprojekte nie gemeinsam: die SI-Klausur nimmt Ap2SzenarienFuer("si"), ihre gespeicherten
        // Indizes (aufgaben[].index) blieben sonst nicht stabil (Gegenpruefung W2)
        Assert.Equal(0, e.Anzahl(Einteilung.Ap2, Fachrichtungen.Gemeinsam));
        Assert.Equal(0, e.Anzahl(Einteilung.Projekte, Fachrichtungen.Gemeinsam));
        Assert.Equal(bisher.Ap2Szenarien.Count, alle.Ap2SzenarienFuer("si").Count);
        // erzeugte Subnetz-Aufgaben (nicht in quizfragen.json) bleiben si
        Assert.All(alle.Quizfragen.Skip(bisher.AnzahlQuizfragenDatei).Take(bisher.Quizfragen.Count - bisher.AnzahlQuizfragenDatei),
            q => Assert.Equal(Fachrichtungen.Systemintegration, q.Zugehoerigkeit));
    }

    [Fact]
    public void EinteilungDatei_ErzeugerStehtFest()
    {
        var text = File.ReadAllText(Path.Combine(Lernen, "einteilung_bisher.json"));
        Assert.Contains("werkzeuge/inhalte/einteilen.py", text);
        Assert.EndsWith("}\n", text);
        Assert.DoesNotContain("\r", text);
    }

    // -- SI unveraendert -----------------------------------------------------------------------

    [Fact]
    public void SiUnveraendert_ListenThemenRahmenplanGleichBisher()
    {
        var alle = Echt();
        var vorher = Lerninhalte.Laden(Pfade.Inhalte);
        foreach (var fr in new string?[] { "si", null, "xx" })
        {
            var si = alle.Fuer(fr);
            Assert.Equal("si", si.Sicht);
            Assert.Equal(Schluessel(vorher.Karteikarten), Schluessel(si.Karteikarten));
            Assert.Equal(Schluessel(vorher.Quizfragen), Schluessel(si.Quizfragen));
            Assert.Equal(Schluessel(vorher.Ap1Szenarien), Schluessel(si.Ap1Szenarien));
            Assert.Equal(Schluessel(vorher.Ap2Szenarien), Schluessel(si.Ap2Szenarien));
            Assert.Equal(Schluessel(vorher.Testprojekte), Schluessel(si.Testprojekte));
            Assert.Equal(vorher.AnzahlQuizfragenDatei, si.AnzahlQuizfragenDatei);
            Assert.Equal(vorher.ThemenReihenfolge, si.ThemenReihenfolge);
            Assert.True(JsonNode.DeepEquals(vorher.Rahmenplan, si.Rahmenplan));
            Assert.Equal(vorher.InhalteJeFachbereich(), si.InhalteJeFachbereich());
            Assert.Equal(vorher.Ap2SzenarienJeBlock(), si.Ap2SzenarienJeBlock());
            Assert.Equal(FachrichtungTexte.Zaehlteil(vorher, "si").Split('\n')[2..], FachrichtungTexte.Zaehlteil(si, "si").Split('\n')[2..]);
        }
        // die Probe-Inhalte (AE, DPA, DV) aendern an SI nichts
        var probeSi = MitProbe().Fuer("si");
        Assert.Equal(Schluessel(vorher.Karteikarten), Schluessel(probeSi.Karteikarten));
        Assert.Equal(Schluessel(vorher.Quizfragen), Schluessel(probeSi.Quizfragen));
        Assert.Equal(Schluessel(vorher.Ap2Szenarien), Schluessel(probeSi.Ap2Szenarien));
        Assert.Equal(Schluessel(vorher.Testprojekte), Schluessel(probeSi.Testprojekte));
        Assert.Equal(vorher.ThemenReihenfolge, probeSi.ThemenReihenfolge);
    }

    private static List<string> Schluessel(IEnumerable<Lerninhalt> liste) =>
        [.. liste.Select(e => e.Nummer.ToString(CultureInfo.InvariantCulture) + "|" + e.Daten.ToJsonString())];

    [Fact]
    public void Dashboard_SiGleichBisher_AeZaehltNurDieSicht()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, "voll.db");
        File.Copy(Pfade.Vergleich("testdaten.db"), pfad);
        var heute = new DateOnly(2026, 10, 20);
        var ziel = Einstellungen.Ziel(new JsonObject());
        string Lies(Lerninhalte inhalte) => JsonSerializer.Serialize(
            DashboardWerte.Lesen(pfad, heute, new DashboardInhalte(inhalte), ziel, null, Path.Combine(ordner.Pfad, "fehler.log")));
        var alle = Echt();
        Assert.Equal(Lies(Lerninhalte.Laden(Pfade.Inhalte)), Lies(alle.Fuer("si")));
        var ae = alle.Fuer("ae");
        var aeInhalte = new DashboardInhalte(ae);
        var werte = DashboardWerte.Lesen(pfad, heute, aeInhalte, ziel, null, Path.Combine(ordner.Pfad, "fehler.log"));
        // Seitenleiste (LERNSTATUS) zaehlt wie die Kachel nur die Sicht
        Assert.Equal((werte.Bearbeitet, werte.InhalteGesamt), Lernstatus.Lesen(pfad, aeInhalte));
        var siInhalte = new DashboardInhalte(alle.Fuer("si"));
        Assert.True(Lernstatus.Lesen(pfad, siInhalte).Bearbeitet > werte.Bearbeitet);
        Assert.Equal(ae.InhalteJeFachbereich().Values.Sum(), werte.InhalteGesamt);
        Assert.All(werte.Kennzahlen, k => Assert.InRange(k.Wert, 0, k.Gesamt));
        Assert.Contains(werte.Kennzahlen, k => k.Gesamt == ae.Karteikarten.Count);
    }

    // -- Klausur AE mit Probe-Inhalten ---------------------------------------------------------

    [Fact]
    public void KlausurAe_OhneInhalteNichtSpielbar_MitProbeVierAufgaben()
    {
        var echt = new KlausurInhalte(Echt().Fuer("ae"), Umbenennungen);
        foreach (var art in Klausur.FachrichtungsArten)
        {
            Assert.False(echt.Spielbar(art.Art), art.Art);
            Assert.Equal(4, echt.Art(art.Art)!.Themen!.Count);
        }
        var probe = new KlausurInhalte(MitProbe().Fuer("ae"), Umbenennungen);
        Assert.False(probe.Spielbar("prozessanalyse"));
        foreach (var art in new[] { "softwareprodukt", "algorithmen" })
        {
            Assert.True(probe.Spielbar(art), art);
            var zustand = Klausur.Neu(probe, art, [], new PythonZufall(7), new DateTimeOffset(2026, 10, 29, 20, 0, 0, TimeSpan.Zero), SpeichernTests.Berlin);
            var aufgaben = zustand["aufgaben"]!.AsArray();
            Assert.Equal(4, aufgaben.Count);
            Assert.All(aufgaben, a => Assert.Equal("ap2_ae", a!["quelle"]!.GetValue<string>()));
            for (var n = 0; n < aufgaben.Count; n++)
            {
                Assert.NotNull(Klausur.Details(probe, zustand, n));
            }
        }
        // SI-Klausur bleibt wie heute
        Assert.True(new KlausurInhalte(MitProbe().Fuer("si"), Umbenennungen).Spielbar(Klausur.Konzeption));
        Assert.Equal("ap2", Klausur.Art(Klausur.Konzeption)!.Quelle);
    }

    // -- Szenarien: gespeicherte Position = Nummer - 1 -----------------------------------------

    [Fact]
    public void Szenarien_PositionUeberNummer_SiIdentisch_AeNurEigene()
    {
        var alle = MitProbe();
        var si = alle.Fuer("si");
        var ablaufSi = new Szenarioablauf(Quellen.Ap2, si.Ap2Szenarien, () => new StatusBuch(new Antwortbuch()), null);
        for (var i = 0; i < si.Ap2Szenarien.Count; i++)
        {
            Assert.Equal(i, ablaufSi.Position(i));
        }
        var ae = alle.Fuer("ae");
        var ablaufAe = new Szenarioablauf(Quellen.Ap2, ae.Ap2Szenarien, () => new StatusBuch(new Antwortbuch()), null);
        Assert.Equal(8, ae.Ap2Szenarien.Count);
        Assert.Equal(-1, ablaufAe.Position(0));                       // SI-Szenario: in der Sicht AE nicht zu sehen
        Assert.Equal(0, ablaufAe.Position(si.Ap2Szenarien.Count));     // erstes angehaengtes = erstes der Sicht AE
        Assert.Equal(si.Ap2Szenarien.Count + 1, ae.Ap2Szenarien[0].Nummer);
    }

    // -- Nur anhaengen -------------------------------------------------------------------------

    /// <summary>
    /// SHA-256 (erste 16 Zeichen) jedes Eintrags in inhalte_csharp/lernen, in Reihenfolge. Neue Inhalte
    /// werden hier angehaengt; ein geaenderter oder geloeschter Eintrag macht den Test rot.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> Eingetragen = new Dictionary<string, string[]>
    {
        ["karteikarten"] = [],
        ["quizfragen"] = [],
        ["szenarien_ap2"] = [],
        ["projektarbeiten"] = [],
        ["themen"] = [],
    };

    [Fact]
    public void NurAnhaengen_EintraegeGleichDerTabelle()
    {
        foreach (var (datei, soll) in Eingetragen)
        {
            var daten = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Lernen, datei + ".json")))!;
            var eintraege = datei == "themen"
                ? daten["themen"]!.AsObject().Select(p => (JsonNode)new JsonObject { [p.Key] = p.Value!.DeepClone() }).ToList()
                : [.. daten.AsArray().Select(e => e!)];
            var ist = eintraege.Select(e => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(e.ToJsonString())))[..16]).ToList();
            Assert.True(ist.Count >= soll.Length, datei + ": Eintraege geloescht");
            Assert.Equal(soll, ist.Take(soll.Length));
            Assert.True(ist.Count == soll.Length, datei + ": neue Eintraege in die Tabelle Eingetragen anhaengen: " + string.Join(", ", ist.Skip(soll.Length)));
        }
        Assert.Equal(Lerninhalte.LernenListen.Select(l => l.Name).Append(Lerninhalte.ThemenDatei).Order(StringComparer.Ordinal),
            Eingetragen.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ProgrammEnthaeltDenOrdner()
    {
        var projekt = File.ReadAllText(Path.Combine(Pfade.Wurzel, "src", "Lernplattform.App", "Lernplattform.App.csproj"));
        Assert.Contains(@"inhalte_csharp\lernen\*.json", projekt.Replace('/', '\\'));
        Assert.Contains("inhalte_csharp/lernen/", projekt);
        foreach (var name in Lerninhalte.LernenListen.Select(l => l.Name)
            .Concat([Lerninhalte.ThemenDatei, Lerninhalte.EinteilungDatei, Lerninhalte.ZusatzDatei]))
        {
            Assert.True(File.Exists(Path.Combine(Lernen, name + ".json")), name);
        }
    }

    // -- Wechsel ohne Verlust und Abgleich -----------------------------------------------------

    private const string Repo = "pruefer/lernstand-test";
    private const string Token = "ghp_TestSchluessel0123456789abcdefghijklmn";
    private static readonly DateTimeOffset Zeit = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

    private sealed class Geraet : IDisposable
    {
        private readonly TempOrdner _ordner = new();

        public Geraet(string name)
        {
            Name = name;
            Uhr = SpeichernTests.FesteUhr(() => Jetzt);
            Lernstand = new Lernstand(Path.Combine(_ordner.Pfad, Lernstand.DateiName));
            Einstellungen = new Einstellungen(_ordner.Pfad);
            Einstellungen.Setze(Abgleich.RepoSchluessel, Repo);
            Einstellungen.Setze(Abgleich.TokenSchluessel, Token);
            Schreiber = new LernstandSchreiber(Lernstand, Uhr);
        }

        public string Name { get; }

        public DateTimeOffset Jetzt { get; set; } = Zeit;

        public Uhr Uhr { get; }

        public Lernstand Lernstand { get; }

        public Einstellungen Einstellungen { get; }

        public LernstandSchreiber Schreiber { get; }

        public string Pfad => Path.Combine(_ordner.Pfad, Lernstand.DateiName);

        public void Abgleichen(GitHubAttrappe attrappe)
        {
            using var netz = new GitHubZugang(attrappe.Basis, TimeSpan.FromSeconds(10));
            Abgleich.Ausfuehren(Lernstand, Einstellungen, Name, netz, [], Uhr);
        }

        /// <summary>Alle Zeilen aller Tabellen ohne die Zeile der Fachrichtung (zz-einstellungen) und ohne den Zaehler sqlite_sequence.</summary>
        public List<string> Lerndaten()
        {
            using var db = VergleichsHilfe.Oeffnen(Pfad);
            var zeilen = new List<string>();
            foreach (var tabelle in VergleichsHilfe.Abfrage(db, "SELECT name FROM sqlite_master WHERE type = 'table' AND name <> 'sqlite_sequence' ORDER BY name").Select(z => (string)z[0]!))
            {
                var sql = tabelle == "abschlussprojekt"
                    ? "SELECT * FROM abschlussprojekt WHERE projekt <> 'zz-einstellungen' ORDER BY rowid"
                    : $"SELECT * FROM \"{tabelle}\" ORDER BY rowid";
                zeilen.AddRange(VergleichsHilfe.Abfrage(db, sql).Select(z => tabelle + ": " + VergleichsHilfe.Zeile(z)));
            }
            return zeilen;
        }

        public void Dispose() => _ordner.Dispose();
    }

    /// <summary>Lernt je eine SI-Karte, eine gemeinsame Frage, ein SI-Szenario und ein angehaengtes AE-Szenario.</summary>
    private static void Lernen_(Geraet g, Lerninhalte alle)
    {
        var siKarte = alle.Karteikarten.First(k => k.Zugehoerigkeit == "si");
        var gemeinsam = alle.Quizfragen.First(q => q.Zugehoerigkeit == "gemeinsam");
        var siSzenario = alle.Ap2Szenarien.First(s => s.Zugehoerigkeit == "si");
        var aeSzenario = alle.Ap2Szenarien.First(s => s.Zugehoerigkeit == "ae");
        Assert.True(g.Schreiber.KarteSpeichern(siKarte.Fachbereich!, siKarte.Frage!, "lernen", true));
        g.Jetzt = g.Jetzt.AddSeconds(1);
        Assert.True(g.Schreiber.QuizSpeichern(gemeinsam.Fachbereich!, gemeinsam.Frage!, false));
        g.Jetzt = g.Jetzt.AddSeconds(1);
        Assert.True(g.Schreiber.SzenarioSpeichern(siSzenario.Nummer - 1, siSzenario.Titel!, siSzenario.Daten["theme"]!.GetValue<string>(), true));
        g.Jetzt = g.Jetzt.AddSeconds(1);
        Assert.True(g.Schreiber.SzenarioSpeichern(aeSzenario.Nummer - 1, aeSzenario.Titel!, aeSzenario.Daten["theme"]!.GetValue<string>(), null));
        g.Jetzt = g.Jetzt.AddSeconds(1);
    }

    [Fact]
    public void WechselSiAeSi_OhneVerlust_DatenbankZeilengleich()
    {
        var alle = MitProbe();
        using var g = new Geraet("PC");
        Lernen_(g, alle);
        var vorher = g.Lerndaten();
        Assert.NotEmpty(vorher);
        foreach (var fr in new[] { "si", "ae", "si" })
        {
            Assert.True(Ausbildung.Waehlen(g.Schreiber, fr));
            g.Jetzt = g.Jetzt.AddSeconds(1);
            Assert.Equal(fr, Ausbildung.Aktuell(g.Schreiber));
            Assert.Equal(vorher, g.Lerndaten());
            // die Sicht liest nur: SI sieht das SI-Szenario, AE das angehaengte AE-Szenario
            var sicht = alle.Fuer(fr);
            var ablauf = new Szenarioablauf(Quellen.Ap2, sicht.Ap2Szenarien, () => new StatusBuch(Antwortbuch.Lesen(g.Lernstand)), g.Schreiber);
            Assert.Single(ablauf.Stati());
            Assert.Equal(0, ablauf.Stati().Keys.Single());
        }
        Assert.Equal(vorher, g.Lerndaten());
    }

    [Fact]
    public void ZweiGeraete_VerschiedeneWahl_AbgleichBehaeltAlleLerndaten()
    {
        var alle = MitProbe();
        using var attrappe = new GitHubAttrappe { Datum = () => Zeit };
        using var pc = new Geraet("PC");
        using var handy = new Geraet("Handy");
        Assert.True(Ausbildung.Waehlen(pc.Schreiber, "ae"));
        pc.Jetzt = pc.Jetzt.AddSeconds(1);
        Lernen_(pc, alle);
        handy.Jetzt = Zeit.AddMinutes(5);
        Assert.True(Ausbildung.Waehlen(handy.Schreiber, "si"));
        handy.Jetzt = handy.Jetzt.AddSeconds(1);
        Assert.True(handy.Schreiber.KarteSpeichern(alle.Karteikarten[0].Fachbereich!, alle.Karteikarten[0].Frage!, "lernen", false));
        var pcVorher = pc.Lerndaten();
        Assert.Equal(4, pcVorher.Count(Lernzeile));
        var handyVorher = handy.Lerndaten();
        pc.Abgleichen(attrappe);
        handy.Abgleichen(attrappe);
        pc.Abgleichen(attrappe);
        foreach (var g in new[] { pc, handy })
        {
            Assert.Equal("si", Ausbildung.Aktuell(g.Schreiber));      // juengere Wahl gewinnt (Phase 4)
            var daten = g.Lerndaten();
            Assert.All(pcVorher.Concat(handyVorher).Where(Lernzeile).Select(Ohne), z => Assert.Contains(z, daten.Select(Ohne)));
        }
        Assert.Equal(pc.Lerndaten().Where(Lernzeile).Select(Ohne).Order(StringComparer.Ordinal),
            handy.Lerndaten().Where(Lernzeile).Select(Ohne).Order(StringComparer.Ordinal));
    }

    private static bool Lernzeile(string zeile) =>
        zeile.StartsWith("card_events: ", StringComparison.Ordinal) || zeile.StartsWith("quiz_answers: ", StringComparison.Ordinal)
        || zeile.StartsWith("scenario_events: ", StringComparison.Ordinal);

    /// <summary>
    /// Lernzeile ohne die erste Spalte (id ist je Geraet verschieden) und ohne die letzte (Abstand der
    /// Uhrabweichung, beim Zusammenfuehren neu geschaetzt); Zeit, Inhalt, Ergebnis und uid bleiben.
    /// </summary>
    private static string Ohne(string zeile)
    {
        var doppelpunkt = zeile.IndexOf(": ", StringComparison.Ordinal);
        var spalten = zeile[(doppelpunkt + 2)..].Split(" | ");
        return Lernzeile(zeile) ? zeile[..(doppelpunkt + 2)] + string.Join(" | ", spalten[1..^1]) : zeile;
    }
}
