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
            [Einteilung.QuizErzeugt] = [.. bisher.Quizfragen.Skip(bisher.AnzahlQuizfragenDatei).Select(q => q.Frage!)],
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
        // erzeugte Subnetz-Aufgaben (nicht in quizfragen.json) sind gemeinsam (Weisung P9-6), alle 54 mit Eintrag
        var erzeugt = alle.Quizfragen.Skip(bisher.AnzahlQuizfragenDatei).Take(bisher.Quizfragen.Count - bisher.AnzahlQuizfragenDatei).ToList();
        Assert.Equal(54, erzeugt.Count);
        Assert.All(erzeugt, q => Assert.Equal(Fachrichtungen.Gemeinsam, q.Zugehoerigkeit));
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

    // -- Runde 1 AE (W3, Weisung P9-8) --------------------------------------------------------

    [Fact]
    public void AeRunde1_ZahlenJeArt_DpaDvSiSehenNichtsDavon()
    {
        var alle = Echt();
        Assert.Equal(300, alle.Karteikarten.Count(e => e.Zugehoerigkeit == "ae"));
        Assert.Equal(370, alle.Quizfragen.Count(e => e.Zugehoerigkeit == "ae"));
        Assert.Equal(330, alle.Ap2Szenarien.Count(e => e.Zugehoerigkeit == "ae"));
        Assert.Equal(330, alle.Ap2SzenarienFuer("ae").Count);
        var aeThemen = alle.NeueThemen.Where(p => p.Value == "ae").Select(p => p.Key).ToList();
        Assert.Equal(11, aeThemen.Count);
        foreach (var fr in new[] { "si", "dpa", "dv" })
        {
            var sicht = alle.Fuer(fr);
            Assert.DoesNotContain(sicht.Karteikarten, e => e.Zugehoerigkeit == "ae");
            Assert.DoesNotContain(sicht.Quizfragen, e => e.Zugehoerigkeit == "ae");
            Assert.DoesNotContain(sicht.Ap2Szenarien, e => e.Zugehoerigkeit == "ae");
            Assert.Empty(sicht.ThemenReihenfolge.Intersect(aeThemen));
            Assert.DoesNotContain(alle.Ap2SzenarienFuer(fr), e => e.Zugehoerigkeit == "ae");
        }
        var ae = alle.Fuer("ae");
        Assert.Equal(300, ae.Karteikarten.Count(e => e.Zugehoerigkeit == "ae"));
        Assert.All(aeThemen, t => Assert.Contains(t, ae.ThemenReihenfolge));
        // je Thema der Pruefungsbereiche mindestens 40 Aufgaben (165 je Bereich)
        foreach (var rolle in new[] { "b1", "b2" })
        {
            var themen = alle.RahmenplanZusatz["pruefungsbereiche"]!["ae"]![rolle]!["themen"]!.AsArray().Select(t => t!.GetValue<string>()).ToList();
            Assert.Equal(165, ae.Ap2Szenarien.Count(s => themen.Contains(s.SzenarioThema!)));
            Assert.All(themen, t => Assert.True(ae.Ap2Szenarien.Count(s => s.SzenarioThema == t) >= 40, t));
        }
    }

    // -- Klausur AE mit Probe-Inhalten ---------------------------------------------------------

    [Fact]
    public void KlausurAe_EchtSpielbar_DpaDvNoch_MitProbeVierAufgaben()
    {
        // W3 (Weisung P9-8): AE hat eigene AP2-Szenarien, DPA und DV noch nicht (Hinweis bleibt dort)
        foreach (var fr in new[] { "ae", "dpa", "dv" })
        {
            var echt = new KlausurInhalte(Echt().Fuer(fr), Umbenennungen);
            foreach (var art in Fachrichtung.Nach(fr).Bereiche.Where(b => b.Rolle is "b1" or "b2"))
            {
                Assert.Equal(fr == "ae", echt.Spielbar(art.Art));
                Assert.Equal(4, echt.Art(art.Art)!.Themen!.Count);
            }
        }
        var aeEcht = new KlausurInhalte(Echt().Fuer("ae"), Umbenennungen);
        foreach (var art in new[] { "softwareprodukt", "algorithmen" })
        {
            var zustand = Klausur.Neu(aeEcht, art, [], new PythonZufall(11), new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero), SpeichernTests.Berlin);
            var aufgaben = zustand["aufgaben"]!.AsArray();
            Assert.Equal(4, aufgaben.Count);
            for (var n = 0; n < aufgaben.Count; n++)
            {
                var details = Klausur.Details(aeEcht, zustand, n);
                Assert.NotNull(details);
                Assert.Equal(25, details!.Teile.Sum(t => t.Punkte));
            }
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
        ["karteikarten"] =
        [
            "b65357e6530a687d", "2d8af9e3645f6e20", "72e2cbeeeb9fcfb9", "187802f62df9e081", "79ce657c0b025c60", "3b07978c6c6f2c0d",
            "bd1f18f37e2b6748", "366ebe8f63e43961", "a653326428469c03", "34dc64876f48118a", "bb6ddf23487ea0b9", "a09e48d500aa8ad8",
            "16234337726932c7", "113d0c7a3106f09d", "a55eb7f37f4935b0", "188c176113e62e14", "6e1f2e871ddd4add", "1cad67fc4912f98c",
            "7ca97e6ab71f3dea", "6c443afaa3ed8a29", "3e94df7405e59864", "254b9764b86acf72", "2bdf11eed99d3e31", "6f2b9904c55d78dd",
            "6a3e65315bc2fb4f", "06a35e34cdeb4faf", "8a1e0baac745b464", "a5a8f1561c1457e6", "dcb42043f8192cac", "6e76f13358acd15d",
            "2da693da94aae388", "f018b44de4ccfc74", "e23da9117cb8104d", "12e768d57910cbfa", "f5fa83ceef7aaa50", "f085290fbc2a8122",
            "88302cc83eb738d2", "35f5a06e6db2b144", "1e5b45ad719b3ecd", "484dbc63c3fc388d", "54fac6069c736853", "7c89630ad263f0cb",
            "5724b93ddcf52090", "35c0f8b276eaadab", "d168fda460181a9d", "10398b90a1dcdc02", "b62f641838104107", "603231b0811c3c61",
            "de79ec2cb3885c37", "a233440e7e88e46e", "aef3ec05efbfb58a", "fad71483362a6dd4", "4fd18c0620306b72", "4b8747f0cd02c2fa",
            "03a8e168dc1609e0", "e2968714eeb34fa1", "90e406767b4bfe32", "4145ad9d60b963f0", "706c7ade767e1247", "1a1ce58c8e41d75b",
            "a15b3342ded33d73", "c9cdf11b047883a0", "d2d513a71ae28410", "4ae8853b4ed284df", "fd9f0a5b7ea0d6a7", "4cdf782a4e92e812",
            "768a7a656aea59c8", "9832803df0c19807", "2e5dfdf1112a76d5", "19025b627b21162d", "9a60da5218d19f04", "93866ff1d6412d73",
            "5aae961cc53a6717", "74a8972104799a0a", "d7e9cb561020d337", "35332c0a92428c1f", "d57e1abdbd4d06e2", "92393fb9dae97ffe",
            "5c06642e709e4e57", "2c405b8311149b54", "62ca0c33220ed206", "df311549e3f41b67", "fc947799e397a832", "c883d0c17aa43a47",
            "ca30634ad2275aec", "a43e476aa0707453", "5ce90bfe4c141307", "5f35a30348522da7", "cc5f20c9b5773289", "019ab7cce123fe76",
            "493ed80d9374baf9", "98675755b6c09a2c", "76f9f07ec17f5d67", "6084d8394007a2de", "5681dd4782e326bc", "f36f192c1bbe8ca5",
            "1a7f10894386c532", "7816e1471da38cab", "80dd247c18981018", "fc9c92aa14d59013", "0d41de31090057cf", "fd735e8bb5cfc147",
            "1cc6e10cfdb0eb4c", "d88c39d556c5a3bf", "20fce66b0d98977f", "26fb483889df5e11", "0abf6b197b94afca", "667f4d6ff05de6dd",
            "0479953659645888", "4430a3fbb00cbba7", "c6428f4ae1e8e1e1", "6cb535e79e3f7b99", "11479b4dcdd2d182", "39cf237aa68e5c8e",
            "fa0e100b60301ea8", "bbd0299565e1d2a7", "bd394e54d445bfa8", "fac6682d0345302c", "a1ed678396ac9cd0", "e91c5bd3c72b946f",
            "70ca2ed7dfd5dd64", "c9924772b5aba1e0", "877e064ab5aa50b2", "e9d0989d82de848d", "2bc3d92c6f0dc63a", "231863b0e97ad19a",
            "f94b3f02c8aa748e", "339c718371eb2aff", "7a27cc667c5e948c", "3941401b90a68efa", "cd005f862a356421", "3c0b60eefaed32fa",
            "15a93927f85f43f2", "fd2d9e7c0f0f753c", "28ea0864ed08bc6d", "558b8697024b35b4", "33cabe45aa644c14", "0a5855c807e60553",
            "7329bc7de1673567", "6ec851118f13898e", "351153881d200729", "ef85ce4a54e49eb9", "00124cac16ddf3ae", "79046b6b53a582b5",
            "3eebf75cdf2854c0", "f7271f354c008e46", "75538c736df67d13", "66cad0b33a66944a", "1dfaacea30cf151b", "da1851c3c77950a7",
            "e0abee156e8709da", "5cee70f06ed9c873", "109f761e2803e44d", "bf7d0094dd6fe3fa", "f70967095ee75262", "26a91d9767657a7c",
            "4847aa4341cbd9bc", "48289de20300e4fc", "9e53c48ac1283f38", "7fd4e2869a3b2528", "39204c245db91824", "5a28d66af76d009b",
            "5a824c9957f6868a", "239f0993f60f0095", "50cf89a104aa0695", "33dd678f304b5d9f", "52cf253977682a76", "ab9beb13af79d542",
            "91554f4561c4f679", "6429330955d0f73d", "f94d114838462cf4", "c32004de75208056", "2cbf6211cbcc5758", "b8de8ce55ace698b",
            "12300604cc32047f", "f2c60ebac64bf765", "97605559dc41d61f", "0feb3ac72d5118aa", "d2c9e241cfa9e70f", "3dec010e150504d0",
            "6d65af9486c3dcf4", "3d1915df2a71b75b", "b904c3dcda0f55df", "37e8f101c9ceb70d", "c1a6aa7403f6e928", "6ebf5d9357134324",
            "088cd8ed5ed0a37c", "9c23bdce6c378d19", "b0dba52b7d2c3961", "445aaaa3bea68748", "c20b575cfe27b557", "5096d3d91ea753bd",
            "832d3d7ffe6e4f9b", "978ba26974af036d", "10694b4325834697", "3a45565d6aa82d7a", "3824c09c97ee2c82", "337a4f869949a61f",
            "fa9da03a87d3b8f6", "7fe6d5d3f3dc066a", "7371880c085d591d", "e7b5782d8507cd83", "8c6e94ba46984055", "3ac8ce30fe4c5d4f",
            "a27dbcc6f5e49c1e", "c26bec6a9687bd96", "4f89638466dc0296", "5fe65859dac2f964", "2dd2d076baf6a7f9", "51651aa3f552eadb",
            "e7de1ed8f74ef606", "3175cc71ed5ced2a", "d12ee605199507ba", "579b2d5ded7a2b62", "a6a1030143efabfe", "faa7cd6204e36c2d",
            "2118dd6981d223d9", "29c094ddf5203b60", "cbde9027897abed3", "9fc94b010e723b61", "661c3e861bf39c5b", "27836b7e38f2f659",
            "3833a2c23ecaa20b", "e0cc18dd61ca6cad", "548564c21dc57b91", "699386826d302151", "2bb7a0c714599a32", "4f6ab9e3597f852c",
            "3c515b4cc0ccaa13", "445d034dcd4d2f42", "c7c3b6418f8f6fb1", "f55e54e555ed1bf4", "bb768eab902b393d", "c57ef1b12d126271",
            "f84ab56b1d88dfc4", "c5e5452962f18c35", "7ba38146a3119502", "aa4f40335f5b72d0", "99096fdefdca02c1", "1bd2a114a81e657f",
            "f1423e525f0f67ac", "8cb4b1e69f4c69ea", "3b22778c726aba13", "8f16466642d79bfc", "b35cd95969b450a2", "2090537de10d545f",
            "837ccad3d092d1b2", "71aa074c4c6a9cfa", "e4dcf957f0fc7f65", "dd9a37dbb8965f32", "56960f7af6aa9e7f", "606c95436fc1c869",
            "4930400bbab45cdf", "8c1cac4c806cb950", "db79e17b5527fec2", "82219914d04ee515", "22c5f83456915ec3", "51742d1c52a271d1",
            "bbe438c87ad947b4", "2f26fa2b09cfaea5", "698f33ef51aad276", "99a193a5a7122aa5", "909d2667e0439bd3", "328965f0da123dbd",
            "80f395f1cd251049", "32617ec7c6632fdb", "ea7f75cac286dfb8", "5608ea07a746c490", "255c19e760815b53", "77cde447be179792",
            "0998835f54ec98d8", "8a02bccfd7ae8ece", "ff55ec1ddc001004", "b19acf7077da4873", "c8cb597275f8bae5", "841d534d12ee7950",
            "5595d345e2a46a01", "366a34341ff26493", "335fe9e0645f0d84", "943941e26947af57", "c75f943bbee95ea6", "586481a17305fb3a",
            "a1c31aa0908d960d", "8de81c54c8afb2ce", "b1f7aed376ee30ea", "3af990efaf31c822", "f94fb8618153c94d", "0495c02e31e75f92",
            "f7c6d674bdea59d7", "c8f5c86525cd4faa", "0896b8ce594b395d", "8e187c10fcd0fef6", "e040633040f05821", "ca2dd0701339aefe",
            "1c86e3a2a6af0967", "b27731ceb248bb25", "c671c9f46c8a4c25", "6cb218094f899a35", "49453c7bde6e9316", "c7b24ad3cd6997a4",
        ],
        ["quizfragen"] =
        [
            "283a0c26114673e4", "91b2f5fe48fc146a", "0fff4054e3ecaa4b", "e6f099b483d5697d", "bfd42de9a5221de7", "321ae594e32a24f5",
            "cac6d9e3b76c9357", "256280bdd87904fe", "750a8eb9361db347", "3e40e48f7ef35220", "2a4b06527a1393d2", "bf6248b6002868e4",
            "dac7dde3fb3f7fe4", "30125f079a648758", "3027f35d2b63497c", "ad231f86817329c7", "e3c016980629bbb9", "1caaf0af6c267091",
            "63270ff255bb35a6", "7955ac69e29672b3", "b7dc174c35499641", "1c49e18d771bf362", "dac18eda3155ef27", "169ead65edd847b5",
            "47b1f26045ba17ea", "f9bfc4de1dd594c2", "64697dc0c82df228", "377de66047f7e4d2", "5c8021c4ba3c6f1a", "de4783b707f53909",
            "1b074211774f58f5", "5256744d5403cf3c", "0a42a09ddb0adc79", "7da1f44ca7383e78", "d48ed01ea703cb8f", "818d08e3f0b2907a",
            "7254789a23fda9ff", "59c9f754256edb4b", "4c3442ff5d8d41a0", "e176e70df10958e5", "67c3ea342be5f1b5", "bf113c5cd0f286e6",
            "04b3cd0f055dffb8", "a27befef187a0941", "71406a87ab9b418c", "afbe7af00aa584b8", "bbf114e313cc212b", "d12a189c73ec7349",
            "d4ccdcab61ac9f2c", "2f1e6a2827f45dc9", "0cc8f51cc40e8a5e", "d25193dbc56b278a", "14d7202e4c4d14c3", "eade716c0d75b937",
            "7b3239b137971dbd", "d0c5aa30a423b310", "305814f9df398aa6", "f5e343df4b10225f", "d76bf905f6fd491c", "12b97d7a8ede61de",
            "6962ea0a943af907", "16daf8153a68ca05", "6630e5679b306252", "8ffd16d74dadb653", "438b06f16108eb4f", "09d751dd167f5904",
            "770df32fe8b01bd4", "66689b7454915f18", "638125efbadbcf2a", "c9c6fd5f253ccc53", "feaa708077de13e6", "fde281a9a8b4ff04",
            "1af37b19fb822e4b", "52cca7cbc916717d", "bf9dbe42ffd2a1e8", "df540c9d87cd8b1c", "b557a733c499a32c", "e93504307bf7fac1",
            "d0bda0c7d1f221a0", "d5e2b249a6aac9b6", "86ee8c4767f5f761", "eae72e2f22ad0bcd", "84957440d2c91223", "51aa17393f30e38b",
            "c8339ebd18846b38", "c37864137eba1b3e", "60a019ee55209580", "ad2a4ca8f97d3d3a", "65354bb523edcf7e", "3534c801644b5c2a",
            "470064c219922dda", "a8a3a49ca61e3fef", "58c6999f0b7b2e00", "2c2d1c2b31a09091", "ce9f79f2bab3504e", "a93f801151309aef",
            "fc70baf48b64923d", "dcb3595131fdb2b8", "847226a832a6eccd", "d8704191af1f772d", "6804906e8c85a5ef", "e9f47635c3c525fe",
            "9278bdda98d6d36d", "884e4565f22e0d06", "e368ce997ebd223d", "f68ddf8b3d33513b", "201a596b0cd392df", "0533a5fbcb9a85ab",
            "ce451cf65cdb73ab", "7ce757237ac59730", "73d6990eb79a941f", "7ceffdf1558f9926", "30530c6783390208", "4e64e9f51d094a6a",
            "fa579a9fa3fd732a", "99d9a96ae94f018e", "1eb703c64d3ef5c2", "0533e731fd34179c", "ef87291cd76740de", "ececb333625bcd0a",
            "cc3a114a0c91822e", "44bc4616bb48a732", "c0301379a00d5ca5", "6446493f4136a19b", "8865a9eed93558a0", "add025aac47571c8",
            "866096ee20692feb", "06c61c254fd51b4a", "648592fd4864d92a", "545213c184793202", "98f6d386884de337", "90a3e4661f5d7f97",
            "dd7b1add1fb21a86", "a2aa18ba90cf69fb", "ec31f734da414055", "1bcdebe0ae6adbb2", "aafa906f2e9350e6", "8d3367f8c786cf55",
            "2439794c0574cd71", "afd2d44dfff3a4c5", "1b6df1f22b58520f", "fd6439df72acc74f", "24b6d3de121f1c3e", "f77a527673025530",
            "ecd81e87ecc21efa", "22628118c403aa7f", "93c3d3c6ad78aadc", "df124b6b297c121a", "b644e31b01b5045c", "edca065f1f904ac1",
            "bf3545ea8d96f18f", "90f22d8cf43d0a07", "57fba2d26ede8606", "72b5c58c86647ee5", "0dd4a387d44f0ddc", "0cc251da30a908ea",
            "fdda572df7322600", "f10bb1e48b2a2be4", "95a6527fc98d6f8c", "6de98d02f7c5e5ff", "47bcba8ad5101ce2", "c6d5107eca8aec57",
            "6eb3a51a59a4465b", "98836252fbc7b937", "e3dc0681f29d246a", "2e5cf207fe085aa5", "db8a86a079ac246d", "99e587a87c4649e5",
            "e60aef0fd43b4b6c", "e1a17846a9fdb56b", "a631da71060543f6", "9767610c6981820b", "38856ca273495d97", "d80377498a9e6e4b",
            "f8f63691c2ab7171", "10e300291abd8672", "94e49e6efe253cc4", "1a6632f29b3f0fdd", "2fee73d73e8c41b0", "2145d595a6d06409",
            "007f86f06346163e", "9beffa61f2ae8622", "b395b9d2e2f01dbb", "540dd79c96bcfce0", "bc2bd54dbb8a8132", "0b2a551ec3df2562",
            "c513d188618bacb9", "a9901b619da2d2e6", "687483b86444b652", "cba8ec315ffa4a29", "ce909c22e4554d6c", "19730b959219d519",
            "710fa9a0e8d58879", "014f43704bf34cc3", "26a4d321f0688aab", "b090e1bc6004af56", "575cccfe7ddad52a", "c35fe9dd2e429b9e",
            "10f371a04520d1e3", "c20b201b68ff5339", "72e9e47a0782507a", "0dd7b3f86d6d44b0", "de3a0a508e631d23", "06df456396ad12ef",
            "bc73c145a05bf283", "51593fbce9db6d46", "fb809ca327354ab2", "12fb72f98bc824d5", "fceef173cf7e3dbc", "e1372cae335f152f",
            "ca304bc754c6f636", "436b1c6871548b72", "25b0c4872f9be2df", "001ba33543de7973", "afb51f260da0d8c4", "013ab60a28aa1260",
            "94b4998bea72bfe3", "bd04fbc4d1a9dfa8", "0efbff2213ca588f", "0bdf3e3cb53dae04", "906935e57b5a15f0", "22f6726107bb732a",
            "8bceebf880785f8a", "fdd302801310f580", "56a78a05407d1656", "1f4594581f8d88e3", "5b869c01ee68d083", "4f6ae2bff00a0d3c",
            "7ae039dcdb277ae7", "83a00cd29a521050", "701888e05a5761c1", "30814d8c65a78160", "a9e557572bd9d329", "47f59eeb685e9565",
            "494c166142a6256f", "6ac7de9eca4919c4", "bad445e0aaf52a34", "29b97559ab72075a", "7f96ecd164d07411", "07f6fd81c42abd9a",
            "61438c83a468c3bd", "a5ba010df955d37d", "211f9af37b756900", "5c62c5d6154c6337", "6241248176f11bbe", "44a1f61ad02b7308",
            "41bac61a2752e7d6", "8bf3aac62cc68c1d", "8872b81da2f8ec41", "9fafd35a8462db02", "d14434204854e949", "72820474b9125c03",
            "c3cd67681ff2009d", "ad86ddb24ac51e34", "1559208c871b4cf3", "d399559c7d014125", "e4e35961fa321d28", "411c3fb1bb27680d",
            "6a0782ff04ba0f51", "bd7c91c318bd168e", "1e7e4b76cc9d5e68", "8046b9ff7bdc68de", "456f13fe84ee39d9", "da2f960f6cbb6740",
            "bc72db5538f189f8", "a4da59ec88023431", "f36482de11d2c2d1", "342b16e811212922", "bcacb88598b389ac", "ebe8aa319bedba3e",
            "4aaee3d69615d7ad", "c9efd69cb92e0863", "e85b1f3702e72b90", "c61563edbf47c8bf", "8ce46376f4b07966", "3623d45fb7b7c3d7",
            "279b728c87fa8293", "64f28b58c44239ba", "706b54d18327d69c", "415d7366fddfc746", "5d6d79de346cea41", "ead15c9cff0f2da3",
            "c434818d19f6976c", "d1400004140b247f", "6bc07163b5ead5f4", "bb66ac58373d62bf", "456866c67accfc3a", "13a5bbac75f41e54",
            "30d64bddb24971be", "b1231b92ef26d31d", "bad81a04fbe1cc7c", "90f1412b8ef6284a", "183cf1c86e285d6c", "5d95b73e50b9c5ae",
            "0d6bda093be0594d", "3b4b46b625b96207", "1aabe7b14f3f996e", "d4380d4d5344f5e3", "47d588b6b62d4662", "c71d61edc5bf64f7",
            "9b772acaab4c138a", "2bbe3c52d4a2d1f9", "85c81c890d475d30", "cfa471568acc545f", "e685da49a6d14561", "20f2ec5201dc8795",
            "b47adb048f1a6cbc", "9fd11c18d359edcf", "b466c03e335b3cd9", "2f83095dff492ec1", "dff388a2a372c022", "299572f3b3a0d28e",
            "2b7db64f9460eb4a", "89bee09d12076e66", "bb27ec9e044b9a79", "08c39e436cc603bc", "5b59c9157b746f6e", "839ad09c41555017",
            "3f532ed3f765d1ca", "9b5d7e5b9bcf6bb4", "5f0f232ccddd8c28", "7560e0fd8ee1352f", "0561dd900a52eba3", "a9b65a1e6e0162f9",
            "4ed47baadc2c670e", "0406f57201b83fc2", "4f4ca909cb60c3ab", "4f6389f4a805e86a", "e30241d020e0104d", "28ecff608ffa3388",
            "0ffa8c3b9ca7d8f7", "95ca4aba5e46028f", "56f6fe0e653b9a28", "50a584a5f356857d", "8503ced13bf5a209", "86b7246ca0e85b20",
            "7a4e316e701e2932", "d4968d49d5ab54d5", "8d4402c47c90ae24", "4583d8f313b13110", "885fe1d0d166566d", "a1731070527253a6",
            "c3f8b14ba382c2ad", "d70417296b18cfa4", "b8efb6edd508e8e2", "88d11a20e1ac3f5e", "772cff014988e050", "1cebe42f51c40479",
            "93dac00a117720cf", "e0e21c0289d9adb7", "7f8b5c1c5b932a77", "ff27badc28c9fe2a", "46079e379463defb", "9efe27a7ee5457b2",
            "aa277860d4ee8cb1", "3a55115e335a5f09", "764a70131fbc6bf0", "be25037b42978b0d", "89b1439cfe36267b", "4aa63f2c2b5af94f",
            "571680c57af62c7f", "26bad554e6fa4c3f", "0432ca3dccb083ba", "cb9b3bb2a8823480", "5ca8c1f6618f505d", "7b255a17cfb7de8b",
            "73f632ce9dece9f6", "c643387652759c77", "da560815745e8f08", "ad0777ce78e7c413",
        ],
        ["szenarien_ap2"] =
        [
            "9e34c0423e1a94f5", "6c84fc0caf69ce0a", "d6c5e174663ff985", "be7bf0f5a19199ef", "e16a0064a2dc6316", "d8ae6a65a77fc0ec",
            "287217e141580bc8", "8f34850452e13d21", "dce8f3d2fef7bdf0", "34f3282eabcc0c04", "f5155286d0184abb", "0f8f14ce0d880704",
            "065877dd1bd40d98", "a9a934bc580b3542", "45303ec33886bac0", "939dc72746783cb7", "30c58b277ed1e937", "d4ecdc159fad90e9",
            "aa3298480931db3d", "e7fdc2cb1e18404e", "d8fbcd1c7e969a6a", "0f870b1a263fa36b", "0f8cfa7b07d14b68", "3a181be57f117180",
            "84a149b0574e57c1", "440798f49fb3d48a", "6f9b4a9e10db8949", "e5b55c45acc90f23", "3732e3d9019b0fbd", "6558009ca21fe1fa",
            "614614709c90d3e4", "ab82ddf1800c848e", "e6a690564cfab955", "8a630594801d53f1", "4146702d190e528a", "3b2ef2c6faad5c30",
            "313735a862aa9c90", "14631e0b843177b8", "661155609cfa4e58", "67a025a9bdd10498", "dc5cd732db455bf9", "cc15440cb417064a",
            "fdcca9d054e9035c", "d87bf240ef10c284", "daaf0aace5e5bac9", "9c394803e48027a6", "ffeec0d78667383b", "5e40f325684b340a",
            "a111cdc10e076ca9", "55d100c04dc3bada", "05545da9f044e1f8", "093320b38698a94f", "1238384d79f1fabb", "08c405ea89d6683d",
            "548e908887066663", "72f6c2ecb4026ab9", "d6cc8632cd5d4a62", "7a46e02cdeb9b999", "b3f326be9063d9a8", "ebc037fd71da27f9",
            "3c3bd597a10d1953", "bc65953dcfc6fe8a", "83c980e88cdd26eb", "85e00482d4ca1b5f", "2c29c916703827a7", "37101cbb18b9200c",
            "92539474921169d1", "540b8414f542dbea", "8e6d7982c28d06c6", "24b86d1e31518150", "68a8eaf805824969", "443005e05fb839d6",
            "2e3737dfa2336ccb", "e9a577f9bb551554", "69e4cc6b33c6087a", "31bcb423186ffcbe", "d150d3b29ec5c294", "e7491d4d55d7035c",
            "b95496fffa8fb3a7", "141eb62327d9d4e8", "7dea8cfc45eab474", "9a5a290815754e89", "d034760de2693c71", "61cd20552b4cd5bb",
            "3151af239408ae4a", "7175e960584c8ad1", "086c7d5957b3c459", "49a20068fe0a1441", "2d9f8bc22014ae7f", "da8fc1dbf18a8a8e",
            "b2735d22b41fd8f2", "101f82ec7fec298d", "6a1c9aa91415669d", "6b15b700c178a8e7", "67abd51b56aefb4d", "0ce7283c0b359ed2",
            "d1e9101f038cd9e4", "b354a5c9307edfa3", "54919d0c3e948d5b", "e2f6b799067dff96", "d6d5ef1fef90ec3c", "2207fa435a2faa7e",
            "7ba848a3d3790d93", "66540e8f3ca11f7b", "794c501d15e0a96c", "2929cac5fa5a1403", "c370c643e651dca9", "58fdd206f870a627",
            "dec923a0650c7679", "678377b4f02056f1", "83b9a4fa46de3873", "9f809f25e0d1ce41", "c38036d87859b53c", "3d64e1cf110975a3",
            "f9bb8103af017b0c", "29ed176a8de40d25", "7a569ae959426d4c", "1dbaa7d05ee60b5c", "5c1f1fa48c582e90", "03f7317ca850f5f3",
            "22e6b7236ea867d4", "7bb74b91d0916924", "92d2054f6546a3a3", "3c7b09905f91b4d9", "ebe42f35e9586d18", "fbb6df2bcacd158f",
            "4d8862d71930a03c", "03dcff03e22e0c9d", "e70511005f8889c2", "f52c0b4bb0d7c3a6", "480f435ac3a503d1", "11f0d482fbb26059",
            "b7843173fa524074", "7bd1f624224dbfd9", "d4392be3f4cbbdc0", "ffd4dac65b30908d", "04a8956ca2932932", "e979d84d89559c91",
            "29b9842448830b9e", "50de3841034715cf", "2ebd54343addaa18", "8c5e81cb87325f58", "936053e208bf0eaf", "45213764f66f1a74",
            "a8fadc2fbbe0c8c5", "84315e6cb89fcafc", "b751b3a577169a8b", "3422a3c85bcef3f3", "c97a2fa41d70bac6", "d1bb188c7bcb51f1",
            "c6ed8e1fcbe68383", "4f26be982d4fcaba", "21de45447ccf6a78", "5ad70d835dee26b9", "95e7c1f5c06acbd0", "6461ffc2eabbe02c",
            "62d2a7e7080c5195", "9573ad08a89b8dfa", "a974a512a5ca69b4", "ee1437f907e2d358", "b8cdc97b9ffc5f1e", "62765766f74868cb",
            "44d9ffb0b5f7663d", "f93219ccc61da57c", "ceefded5e0cd1290", "d6967352972dde19", "dcd0e1a6fb395bcf", "ab8f571f4f22591c",
            "8211310d426ede37", "e24e771344fb431c", "f6a5da7a4e825a0b", "8bb162facf314cf3", "99cc5de72692eaae", "6cbbf9e508f2ef66",
            "a80191d3980eb816", "f5c211aa69df9da9", "839fc95085c7235d", "8067bde52ca6653b", "86c6ce3e3528e657", "6108fad16f6674ae",
            "d6e1ea2389e57bed", "f1ec1b5711a343e0", "9d819ba1fd5efe6b", "aef5503e8489a656", "bfd4604223ee6d58", "8553fba6bf75d246",
            "3498f8f19897bfab", "038fa97899b6956d", "bbeb8e3553049f48", "8b2cf67dd945e5b4", "295e699aabd20cee", "5f4a179b9085a341",
            "b5d46e1f0cf7893a", "cd693245f270973a", "a207c0e5041dbc21", "a77396c476f13121", "8e7f814019aca073", "ec056f0662be97b4",
            "46a4fed5d8a2a877", "b695d6747bb94672", "085cddab255923a3", "fe1511f65fa9b016", "04c820a083f53ec2", "28f9cb9359419702",
            "7fb1bf60d727150d", "a9b880a7bb626773", "147b21c644b2e208", "de486c4d955724cc", "4a70d1fdb6bad3cd", "0e4019dbe1b4f87c",
            "a736abd3ba4ead8c", "55ab8121743d97ee", "73ed732ec03b58e3", "598b12b0c01b379a", "cd5fc4821afaea3c", "34a8d3ea691663ef",
            "f4960ded54dd10ae", "e9f9cb0b1df4c73c", "51027682a161e34a", "ef03ccd8971bee6c", "88aebc2206da3671", "057bd4d5a48e7f19",
            "e4ee768405c92db7", "879d4033a96aa61f", "8c2754c76709aba5", "78dc027283b433c8", "f2a0d77547122779", "0c31f0b47cf9cca5",
            "e0f8245b392899f2", "aa40e1930db1380d", "042a7851e00da2d9", "28fde5050e8142b7", "cd121da9eada5c62", "022aa14ca2b717ef",
            "05190ca0af03371a", "d65958e136db7d00", "a07c6f1a23d5a46b", "9cde37a41d22fb03", "5b51d1bc9e9dd492", "ac261ae466d4a96b",
            "e6c7565bcce84cf1", "316c4185afb13417", "805ef48346e9f420", "2ea1f14d7f8d7f58", "be91d1ec10290ab9", "a3b5cd4cdb999fdd",
            "9996a1538d7a878f", "8c72ff3264e98709", "e75f9fe38dd53c94", "4a085c2c807e01ec", "d79b1d7da93861a0", "58798ef10e88ab7b",
            "2ab630f1f2d10c67", "71b937802c042d7a", "7eb875775a519383", "dbe63dc6f5506e81", "76172a2e90537eff", "231af3c4aa9fc299",
            "715b9e0c0e5de1f3", "fad9e30a537c6d31", "f7f58f545b2fe488", "aa072188cf59228d", "69c85c740fd4a945", "f923a1242b8a7982",
            "6da2392e229d7cde", "2513fe86c4f44ade", "9bbd7d353917e03f", "a078eec28ff67ced", "a5d77d6766f52aa5", "bb5583b1dd1b2759",
            "57b5d1a031835e81", "f59f908754b60c3a", "dc5ecf6f7cbd2017", "0200e57fc5edf680", "7acbd53f752ce11f", "daaaac4a3bdf8bb9",
            "1a1083f2b294bfcb", "b48fd835431b79fa", "9a0bb4256aa5ace4", "a4856822099b468e", "876577530bf0457f", "fa45905825923e3f",
            "c1e0f7ee4bb396fa", "54b3df9d41d04ce7", "f386e09d19b2b3b7", "83b4c18930c3a88d", "b7a71b47e87314fa", "5a998de65e05d3fa",
            "6f9f3670186c1467", "8f8cc8a445ab8b31", "910381aad3b901b4", "037eec8cedd8ec90", "92cf6f36b4c4ffc6", "8617be1db336cbf0",
            "f9d062513f76dbcc", "a7c91adfd483b1de", "f496a0b0da613f58", "7a2753e11d265176", "a23c14e4015dfaf9", "2ae819647146f266",
            "d11631230a4e8fd6", "cb9ea96a029e802e", "23fadfeada1f678d", "e17ade5b40ba4e21", "a0bcb9aef3e703d8", "ba7a916fb8bc1208",
            "b55b2d63b2a4fcbd", "dd7beb2e5dfc540f", "1581f30734abc77b", "d3eaafdce59848da", "156bbd8fcf3b2f87", "d7a60a0add639a5e",
            "3f6e22067997339c", "a4515d4e6ff7edf2", "c3007672cca9524e", "868ce5b724844fa3", "01cd8b26b2b4c014", "f082be5e1c1b022b",
            "096f5d90a09072b9", "6fb446703c71c308", "da3c38eee348acbc", "9e5e48bb263f54a8", "dfdd09c7d7c4c7ce", "593dfd7a7fc3dd64",
            "998619643f1f3490", "c16d2aaf51deabf8", "41034b01e8683c20", "bc5c1b55895b3398", "9d62b23742259e46", "a67fb157bd46fa4f",
        ],
        ["projektarbeiten"] = [],
        ["themen"] =
        [
            "ae4147ded187b517", "2e09c75c7f85c2dd", "ee85165aa1903362", "1164cc9de28fc66c",
        ],
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
