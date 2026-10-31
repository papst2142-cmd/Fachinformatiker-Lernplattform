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

    private static int Fachfragen(Lerninhalte inhalte, string fr) => inhalte.FachgespraechFuer(Fachgespraech.Laden(Pfade.Inhalte), fr).Count;

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

    // -- Runde 1 AE (W3, Weisung P9-8), DPA (W4, Weisung P9-10) und DV (W5, Weisung P9-11) ------

    [Theory]
    [InlineData("ae")]
    [InlineData("dpa")]
    [InlineData("dv")]
    public void Runde1_ZahlenJeArt_AndereSehenNichtsDavon(string eigen)
    {
        var alle = Echt();
        // Runde 1 = die ersten 900 Karten und 1 110 Fragen in inhalte_csharp/lernen/ (je Fachrichtung 300 und 370);
        // AE Runden 2 bis 4 (Phase 10 W2) haengen dahinter an und zaehlen hier nicht
        Assert.Equal(300, NeueEintraege(alle.Karteikarten).Take(900).Count(e => e.Zugehoerigkeit == eigen));
        Assert.Equal(370, NeueEintraege(alle.Quizfragen).Take(1110).Count(e => e.Zugehoerigkeit == eigen));
        Assert.Equal(330, alle.Ap2Szenarien.Count(e => e.Zugehoerigkeit == eigen));
        Assert.Equal(330, alle.Ap2SzenarienFuer(eigen).Count);
        var eigeneThemen = alle.NeueThemen.Where(p => p.Value == eigen).Select(p => p.Key).ToList();
        Assert.Equal(11, eigeneThemen.Count);
        foreach (var fr in new[] { "si", "ae", "dpa", "dv" }.Where(f => f != eigen))
        {
            var sicht = alle.Fuer(fr);
            Assert.DoesNotContain(sicht.Karteikarten, e => e.Zugehoerigkeit == eigen);
            Assert.DoesNotContain(sicht.Quizfragen, e => e.Zugehoerigkeit == eigen);
            Assert.DoesNotContain(sicht.Ap2Szenarien, e => e.Zugehoerigkeit == eigen);
            Assert.Empty(sicht.ThemenReihenfolge.Intersect(eigeneThemen));
            Assert.DoesNotContain(alle.Ap2SzenarienFuer(fr), e => e.Zugehoerigkeit == eigen);
        }
        var sichtEigen = alle.Fuer(eigen);
        Assert.Equal(alle.Karteikarten.Count(e => e.Zugehoerigkeit == eigen), sichtEigen.Karteikarten.Count(e => e.Zugehoerigkeit == eigen));
        Assert.All(eigeneThemen, t => Assert.Contains(t, sichtEigen.ThemenReihenfolge));
        // je Thema der Pruefungsbereiche mindestens 40 Aufgaben (165 je Bereich)
        foreach (var rolle in new[] { "b1", "b2" })
        {
            var themen = alle.RahmenplanZusatz["pruefungsbereiche"]![eigen]![rolle]!["themen"]!.AsArray().Select(t => t!.GetValue<string>()).ToList();
            Assert.Equal(165, sichtEigen.Ap2Szenarien.Count(s => themen.Contains(s.SzenarioThema!)));
            Assert.All(themen, t => Assert.True(sichtEigen.Ap2Szenarien.Count(s => s.SzenarioThema == t) >= 40, t));
        }
    }

    [Fact]
    public void Runde1Dpa_AeBleibtWieW3()
    {
        // W4 (Weisung P9-10): die Sicht AE zaehlte nach W3 1 084 + 300 Karten, 1 615 + 370 Fragen, 330 AP2; seit
        // Phase 10 W2 (Weisung P10-1) kommen genau 1 200 Karten und 1 750 Fragen dazu, die AP2-Szenarien bleiben
        var ae = Echt().Fuer("ae");
        Assert.Equal(1384 + 1200, ae.Karteikarten.Count);
        Assert.Equal(1985 + 1750, ae.Quizfragen.Count);
        Assert.Equal(330, ae.Ap2Szenarien.Count);
        Assert.Equal(ae.Ap2Szenarien.Count, ae.Ap2Szenarien.Count(s => s.Zugehoerigkeit == "ae"));
    }

    [Fact]
    public void Runde1Dv_AeUndDpaBleibenWieW4()
    {
        // W5 (Weisung P9-11): die Sichten DPA und DV zaehlen genau wie nach W4/W5 (1 084 + 300 Karten,
        // 1 615 + 370 Fragen, 330 eigene AP2); AE dazu mit den Runden 2 bis 4 (Phase 10 W2: +1 200 / +1 750),
        // DPA ebenso (Phase 10 W3)
        var alle = Echt();
        foreach (var fr in new[] { "ae", "dpa", "dv" })
        {
            var sicht = alle.Fuer(fr);
            var dazu = fr is "ae" or "dpa" ? (Karten: 1200, Fragen: 1750) : (Karten: 0, Fragen: 0);
            Assert.Equal(1384 + dazu.Karten, sicht.Karteikarten.Count);
            Assert.Equal(1985 + dazu.Fragen, sicht.Quizfragen.Count);
            Assert.Equal(330, sicht.Ap2Szenarien.Count);
            Assert.Equal(330, sicht.Ap2Szenarien.Count(s => s.Zugehoerigkeit == fr));
            Assert.Equal(300 + dazu.Karten, sicht.Karteikarten.Count(e => e.Zugehoerigkeit == fr));
        }
    }

    // -- AE Runden 2 bis 4 (Phase 10 W2, Weisung P10-1) -------------------------------------------

    [Fact]
    public void AeRunden2bis4_ZahlenJeArt_AndereBleibenGleich()
    {
        var alle = Echt();
        // die neuen Eintraege haengen hinter Runde 1 an (900 Karten, 1 110 Fragen) und gehoeren alle zu AE
        // (Phase 10 W3 haengt DPA dahinter an)
        Assert.Equal(1200, NeueEintraege(alle.Karteikarten).Skip(900).Take(1200).Count());
        Assert.All(NeueEintraege(alle.Karteikarten).Skip(900).Take(1200), e => Assert.Equal("ae", e.Zugehoerigkeit));
        Assert.Equal(1750, NeueEintraege(alle.Quizfragen).Skip(1110).Take(1750).Count());
        Assert.All(NeueEintraege(alle.Quizfragen).Skip(1110).Take(1750), e => Assert.Equal("ae", e.Zugehoerigkeit));
        Assert.Equal(50, alle.Testprojekte.Count(e => e.Zugehoerigkeit == "ae"));
        Assert.Equal(18, alle.NeueFachfragen.Take(18).Count());
        Assert.All(alle.NeueFachfragen.Take(18), f => Assert.Equal("ae", f.Zugehoerigkeit));
        // Sicht AE: 2 584 Karten, 3 735 Fragen, 50 Testprojekte, 42 + 18 Fachgespraech-Fragen
        var ae = alle.Fuer("ae");
        Assert.Equal(2584, ae.Karteikarten.Count);
        Assert.Equal(3735, ae.Quizfragen.Count);
        Assert.Equal(50, ae.Testprojekte.Count);
        Assert.All(ae.Testprojekte, e => Assert.Equal("ae", e.Zugehoerigkeit));
        Assert.Equal(60, Fachfragen(alle, "ae"));
        // SI, DPA und DV sehen nichts davon; SI und DV zaehlen wie vor der Welle (DPA siehe Phase 10 W3)
        var si = alle.Fuer("si");
        Assert.Equal((1565, 2174, 50, 60), (si.Karteikarten.Count, si.Quizfragen.Count, si.Testprojekte.Count, Fachfragen(alle, "si")));
        Assert.DoesNotContain(si.Testprojekte, e => e.Zugehoerigkeit == "ae");
        var dv = alle.Fuer("dv");
        Assert.Equal((1384, 1985, 0, 42), (dv.Karteikarten.Count, dv.Quizfragen.Count, dv.Testprojekte.Count, Fachfragen(alle, "dv")));
        Assert.DoesNotContain(alle.Fuer("dpa").Karteikarten, e => e.Zugehoerigkeit == "ae");
        Assert.DoesNotContain(alle.Fuer("dpa").Testprojekte, e => e.Zugehoerigkeit == "ae");
    }

    // -- DPA Runden 2 bis 4 (Phase 10 W3, Weisung P10-2) ------------------------------------------

    [Fact]
    public void DpaRunden2bis4_ZahlenJeArt_AndereBleibenGleich()
    {
        var alle = Echt();
        // die neuen Eintraege haengen hinter AE Runden 2 bis 4 an (2 100 Karten, 2 860 Fragen) und gehoeren alle zu DPA
        Assert.Equal(1200, NeueEintraege(alle.Karteikarten).Skip(2100).Count());
        Assert.All(NeueEintraege(alle.Karteikarten).Skip(2100), e => Assert.Equal("dpa", e.Zugehoerigkeit));
        Assert.Equal(1750, NeueEintraege(alle.Quizfragen).Skip(2860).Count());
        Assert.All(NeueEintraege(alle.Quizfragen).Skip(2860), e => Assert.Equal("dpa", e.Zugehoerigkeit));
        Assert.Equal(50, alle.Testprojekte.Count(e => e.Zugehoerigkeit == "dpa"));
        Assert.Equal(36, alle.NeueFachfragen.Count);
        Assert.All(alle.NeueFachfragen.Skip(18), f => Assert.Equal("dpa", f.Zugehoerigkeit));
        Assert.Equal(Enumerable.Range(1, 18).Select(i => "dpa" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture)), alle.NeueFachfragen.Skip(18).Select(f => f.Frage.Id));
        // Sicht DPA: 2 584 Karten, 3 735 Fragen, 50 Testprojekte, 42 + 18 Fachgespraech-Fragen
        var dpa = alle.Fuer("dpa");
        Assert.Equal(2584, dpa.Karteikarten.Count);
        Assert.Equal(3735, dpa.Quizfragen.Count);
        Assert.Equal(50, dpa.Testprojekte.Count);
        Assert.All(dpa.Testprojekte, e => Assert.Equal("dpa", e.Zugehoerigkeit));
        Assert.Equal(60, Fachfragen(alle, "dpa"));
        // SI, AE und DV sehen nichts davon und zaehlen wie vor der Welle
        var si = alle.Fuer("si");
        Assert.Equal((1565, 2174, 50, 60), (si.Karteikarten.Count, si.Quizfragen.Count, si.Testprojekte.Count, Fachfragen(alle, "si")));
        Assert.DoesNotContain(si.Testprojekte, e => e.Zugehoerigkeit == "dpa");
        var ae = alle.Fuer("ae");
        Assert.Equal((2584, 3735, 50, 60), (ae.Karteikarten.Count, ae.Quizfragen.Count, ae.Testprojekte.Count, Fachfragen(alle, "ae")));
        Assert.DoesNotContain(ae.Karteikarten, e => e.Zugehoerigkeit == "dpa");
        Assert.DoesNotContain(ae.Testprojekte, e => e.Zugehoerigkeit == "dpa");
        var dv = alle.Fuer("dv");
        Assert.Equal((1384, 1985, 0, 42), (dv.Karteikarten.Count, dv.Quizfragen.Count, dv.Testprojekte.Count, Fachfragen(alle, "dv")));
    }

    /// <summary>Nur die Eintraege aus inhalte_csharp/lernen/ (die bisherigen sind si oder gemeinsam), in Dateireihenfolge.</summary>
    private static IEnumerable<Lerninhalt> NeueEintraege(IEnumerable<Lerninhalt> liste) =>
        liste.Where(e => e.Zugehoerigkeit is "ae" or "dpa" or "dv");

    // -- Klausur AE mit Probe-Inhalten ---------------------------------------------------------

    [Fact]
    public void KlausurAeDpaDv_EchtSpielbar_MitProbeVierAufgaben()
    {
        // W3 (Weisung P9-8) AE, W4 (Weisung P9-10) DPA, W5 (Weisung P9-11) DV: eigene AP2-Szenarien, alle spielbar
        // (der Hinweis "noch keine Aufgaben" kommt bei keiner Fachrichtung mehr)
        foreach (var fr in new[] { "ae", "dpa", "dv" })
        {
            var echt = new KlausurInhalte(Echt().Fuer(fr), Umbenennungen);
            foreach (var art in Fachrichtung.Nach(fr).Bereiche.Where(b => b.Rolle is "b1" or "b2"))
            {
                Assert.True(echt.Spielbar(art.Art), fr + " " + art.Art);
                Assert.Equal(4, echt.Art(art.Art)!.Themen!.Count);
            }
        }
        foreach (var (fr, arten) in new[] { ("ae", new[] { "softwareprodukt", "algorithmen" }), ("dpa", new[] { "prozessanalyse", "datenqualitaet" }), ("dv", new[] { "diagnose", "betrieb" }) })
        {
            var echt = new KlausurInhalte(Echt().Fuer(fr), Umbenennungen);
            foreach (var art in arten)
            {
                var zustand = Klausur.Neu(echt, art, [], new PythonZufall(11), new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero), SpeichernTests.Berlin);
                var aufgaben = zustand["aufgaben"]!.AsArray();
                Assert.Equal(4, aufgaben.Count);
                Assert.All(aufgaben, a => Assert.Equal("ap2_" + fr, a!["quelle"]!.GetValue<string>()));
                for (var n = 0; n < aufgaben.Count; n++)
                {
                    var details = Klausur.Details(echt, zustand, n);
                    Assert.NotNull(details);
                    Assert.Equal(25, details!.Teile.Sum(t => t.Punkte));
                }
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
            "1ba2544710128a28", "dbc247129aeb5b92", "182484ac80c7f561", "5d1be974d9f6f3f7", "05c6b3127551e3a4", "386f7f7136a84a0d",
            "eae477090dce5742", "5dee16e60fd0a716", "32bbb362b4a0513d", "ceac7cb3d8b4b745", "69493e0558750f5f", "0bded2072f7db020",
            "5c6e65986fe61498", "086cfb8a013f5f10", "08d41182771239ad", "e4318e6a76d20c00", "aa70c4ea74544d5b", "535bed655eb852df",
            "e5acc0181bd5cddc", "0466ce9dffa44916", "5c5e5af0ca1bfcba", "040ce34a237aacde", "4df8f9509e1a2152", "9c1fda105e635887",
            "c2c2a9afb7f05d17", "50067cf282b52260", "527d024be3efea78", "996b0ea7e2a09d97", "0a9d2b00b1080eda", "ec3dd62541a59690",
            "6cc2f885375128a1", "a12531ad94f268aa", "dfcc26433d919fa0", "04b00ecdf59b770b", "d0103790d510239c", "88bdc65c9f74a4c8",
            "6448b77c1549f9b5", "1ff280061d40a223", "9d7712a113f80ced", "24121a2a1ad46d81", "5649daab05a013d1", "569067c72fb5347f",
            "a22e8a33232b305d", "c4ae0b017056fd64", "464a05a5f8cdc1aa", "04ffdcebbd6b190b", "a6ae35ccdcd951eb", "fec3e8938039783f",
            "b884952e1ebeff9e", "3cc077bad888ec01", "e61b857724624ec8", "091f55629b6bc55f", "d8146ef87baed3be", "8f655063d49e25f6",
            "4f9d9c751ab10da3", "3a89274ec0a2dc48", "3d72b91c6930c5f1", "37ffaf9960765100", "a911b54ff1c85bd1", "fadd783a3e8fea3a",
            "941aa27a5de4a9e5", "b6bcd61e3daa631e", "75a508189d099e3f", "24ca63134797cd75", "bfb834131348311b", "f86152e5e5ea60c5",
            "1e7145aa9651e973", "ff527dcd46627c33", "1f0baa385ef81093", "d8c5628a356dbe6e", "b9f3cc77b7e02e29", "b79fa9a8377fce7a",
            "d02ed513521e6448", "f21735be7a0dc184", "acd300a7c0d8edb3", "3ea3ea49e13fc1ce", "32021a0d71dd7c4a", "ca2ab30e742d4967",
            "045067c724b1ab84", "e4d418fdacca6492", "b1fa1f522b9d5890", "1d7ea19a50907db3", "11eefdcaa46cb4a0", "1ae5de4380e756c5",
            "ecda898af6a09fd8", "0e8302d618c21f6b", "df0faf00b25c45e0", "4b315c0cd635c42d", "275bc4b5e8a95e65", "23d07d93e1f1ade2",
            "ee3b7603c5e383a2", "ffed884edec3b0a3", "537d95d3c9a82dcb", "3dfa654e13ed3b9e", "211432b05a5af1e8", "4bf24142cbb68c77",
            "5a837dd2dbcfd79d", "a8b7bdd1b2e8248a", "93836e0155b78bc2", "8c1c6c1ae15e6b2a", "213301211be79991", "450754509835f2a7",
            "d44b681c98bd90b8", "4fbf089ccbfc549f", "5e07b6b5bdce2171", "21d8a1e4b15bb8f5", "ec99d8bdcb99abef", "42067a34d5973439",
            "55fb0150e90be997", "e4ff2f2b05446eaa", "9bb70cdf717c6ca8", "0334abb703672cfe", "2091e21133129041", "0bffdb52c2b48e82",
            "ee9eca234564c979", "5f56a46a93be78a4", "b40702bd275abaab", "52c781610706fb56", "ef2849115f5545ca", "cc0ee29bb4ab4287",
            "afc28dd4a3772320", "3b22982eeb61dbea", "f1bdac9026e1f2d0", "2047dce5a0f75b91", "0097bc61a38ac81f", "5cc1f2beb4404485",
            "0bfad73f1a81d73c", "522ed6be585a1410", "f65a09c4206c6b64", "4cead4efb8d17ddf", "e7c393b3a19efed6", "c9db7632e1abaa2f",
            "5d0863eb687f0a7d", "739e2b9a3ac1952b", "47ac66811768ab08", "258fff2de523a491", "ce50d0848cac385c", "689ba3b3b3440411",
            "b69a587354543432", "390233bba4430bb8", "38f883b371e1d00e", "1bcc294764044f8f", "3e710edc79948dd1", "399663bf6bb59727",
            "1a27300835ba528b", "d2830c078427d369", "c25b0e080d0b669a", "bc6dec9d3c79cd9d", "abc0cee9cfbc98c4", "e49e6baf75871ebb",
            "5fca4b78fd2612c1", "d73a78daad9c355b", "591b0c3abb9fb290", "271657a9259f2c8d", "652743a439639db6", "c182d1ea9eb5478c",
            "6faa42b4bafb055d", "1906c72ebd5030d1", "f76a2688f0d4558e", "699af82d48450ac8", "f3e7d51dc10445c6", "8c151e7762549673",
            "91b8f90c02febfeb", "b6b7c2fc621639bf", "05c0fd30717ca9ae", "7ab8b0ea229c1fcf", "eaae90e5268ec17c", "ab195374d2d61bfc",
            "89c0d6ed69909812", "030a485b71d9291b", "402ea1dbe69d7a77", "8626d717991e304d", "b11aeda74abbb7de", "ec317005c8830485",
            "8be0bceeb7dbee9c", "f9e8c8fbea772569", "a30854588a7d2804", "4775a57bc92d0cd3", "f5b54178af9f8d49", "fb8b63a1ec5e2bca",
            "23003c2894cc8ed7", "620aa28b851716fd", "e85daaf594498302", "e446da9c33066e92", "5168d04bc93309c2", "2c4d1ff889de7d69",
            "11ed278d99d96d7e", "a94fe8d9f8c44eea", "e1e7a742826501cb", "2e79642d2e47d3ee", "f56b776db30a2c4f", "a69ab145d798b05f",
            "513a91d41167e292", "1ae8b94b927a4d30", "7e883fbe264c1c84", "105dbe2a1144749f", "63158f415308faae", "fda051d814882464",
            "ec62e2aba171ee4e", "139908f1595922d1", "612a77fc6c4f5b82", "e06efa63546b3b64", "5b4713eaaa778592", "f102c3b20ab35c52",
            "b0b2e5fa344362c3", "740c710576e59dee", "7957e05691318086", "0b08a5eda88a955a", "083023d8a37e5caa", "b6fb53d847034149",
            "17aa9c4a2ac98112", "a787c81559b9ab23", "03c640b0442cc65f", "46709dfb2ae3b7f7", "7131adbd7ef94977", "272f58f7a77a8623",
            "28f9344a1d8a9b99", "bd64d3aebf81ee5d", "4c46537b840cf8f4", "6049bf9614cf2a94", "a4fce010df705498", "e23528bc778648c3",
            "a7026ec9c44776e8", "5a04063a31e8d1bc", "3bb477a14309a28b", "29432373454919b6", "bb6954dc2891cbeb", "b92f6d078b8e64ba",
            "7a0371761ed4ede3", "adb57d2e74620462", "72b960068db981e7", "e550156b82e1bb19", "2424fb0c3d006d37", "098f2d52b76ac2c7",
            "cbc366b5e52f4833", "9a445f3cdd7f1a5f", "365d1789fc21632a", "1fb47977e5d4023a", "c4aa7ff129b2ed97", "bda7355bea9eb889",
            "019e82ab6dac8cf4", "c14a76de0f0e1885", "a885618388d66ed6", "0fca7a479ceb3dec", "7ca01e96dafb1f90", "498052d84a557cdb",
            "79f31862b0df09da", "792baa3a43731a83", "e913f9cd84d70a6d", "47ba54bc79c2f1b9", "fdae3f02a1a2f777", "182689ef9bb674ba",
            "5d7f57e2de80143d", "468e602ac1d7e747", "7f285addb55c3e83", "87551f92b19fab2d", "847e880b6bfda181", "0321e0062e7b4bbd",
            "3a8b671186d4d18a", "f3a054b4a1a0a706", "f2a2ffc79372ef40", "9385a48cd0b29568", "eb7475360d1aea9e", "479aed7c392b31cc",
            "007052724d54894d", "e9e7e3b2b8b6c23b", "279efb7963eba618", "c85bc297ebd1632a", "feadc541b8a4360d", "b876c8079e48812c",
            "48910c91002a4ab6", "fa5fcd82e1fcb074", "c88675c84e5f68f6", "1b19e65fee307ef3", "2272ab8795e6cfbe", "7e2b21d50200cc5d",
            "7f1092ceb26ebf09", "01f82445798c5dd0", "250bdf467974fc9d", "0a761240eb883538", "ed2b7b5302a9405d", "123ae26a33bcd8d1",
            "6abdc9648952e19c", "c85f9609c39121ff", "60a7581396a0a80d", "313ca843d50d3fe0", "58db33840ff4bbaf", "1fbdf53df44e160e",
            "1c211e5bdd0f9893", "6330fe9918351bb1", "f453432115bbfe63", "3a898db99a65010b", "2ee5dadb1bd220d8", "671b8efae467761b",
            "c1c04ab73a2e0fa8", "5c01dc9a2919207a", "a1d26f5185f81038", "346545a05b7bbd90", "697ba97857872aad", "785c7eb28ae9dde6",
            "ed0877df855f0055", "c758383c7cfb7f5a", "e4360427daf6516e", "366e1b3ae3437052", "c44ffd558794ec51", "6ef08bf2b2c5855e",
            "e2bc6202814ccdad", "5119bf118f8ba189", "48b8827152db9594", "20a68be6d45d909d", "dcfb3008c35dab31", "1df3051ba2a8dbfc",
            "dbb02103df099aea", "35a99a91f9fee0db", "ee4f43b62e14b4c3", "3300df7a4329d5a0", "046b70a5e3d82e20", "ac8e29864701fc0d",
            "8991f6c8fa7ca586", "fa2267568a0e5eb0", "1071698abd3b7fe3", "011543c0993d0ab5", "c9b1af83256ab31b", "41432c257f8dbe9d",
            "a68b3d36fefef668", "ffed12a5d275f539", "1a1207091441fa7c", "88520b75c05173f1", "2be31eb9cef51005", "92d32e5a0c7503ce",
            "f5b7dfc6139bd231", "867b41b1fe3f5d6e", "b79dae113bb410b5", "5efd7397150b7ac0", "1f584b6f95672f00", "ed9c92173226a4fc",
            "4aea7c961d691a5f", "1f7143f248dad233", "420619c3d00e41c6", "31a4c340479f131b", "331e67ad7709a818", "ba3e294968c238a4",
            "fcd0956c0d21f75b", "23d5ea5dee6dafbc", "46dafcddf523f629", "e68c4af04e77ee48", "cf179a3cee74b3f0", "e26b86f806d92577",
            "d8158d1ccc2350d2", "de30a441308542d4", "3e58a6673d90f6f3", "ac2d49dd86ef9139", "ab9b7d80ec8787a1", "4f9bcb3783ffdaf0",
            "79d02640b6e5a1c4", "119b22f37b85a4c3", "4957524ad91e3641", "ac77fbc7376e0ede", "19ff57509a954786", "c380953cdace18c7",
            "11d89e3e503851f9", "94eaf6ebe43f6c7b", "e04300c397210bfd", "72fdbb0dd85be2ab", "a69a097a2d6224f2", "98fa6a7cfef4f535",
            "66f9b8c158634d66", "6636a1a59e57ed26", "b49c4ea992e03c9b", "6016a0cdcb2c99b3", "f854fbdd3d6c5a4b", "55d023de635b33b7",
            "eb42a9ab55e6c388", "8b52e84c0f5f0de4", "2e41320c8615e718", "86ca109655671faf", "1e3c3232ce7d828c", "dd566267e7783b4b",
            "967caa6d6a4103b1", "5f9ef95ded172dbe", "17a84f2954b7119b", "e2b48810dfc6a06f", "209441edbdd62733", "a795b31f970e5f7b",
            "cbceb46a52614766", "5790d2d5af8b496a", "5a585b455c0a0c17", "70ab188e72f5d42a", "1a4754f61cf7bf25", "8d47d6d46549701b",
            "cb2f044a30416faf", "57e5dac8e73dadf4", "58eb62570fdac47a", "3401fb615ed3df97", "b46bcfee10315ecf", "0072b1d0b8e159b5",
            "e8fbdf8ec3faf562", "f70031a01545128d", "e4dbccbc593daa21", "c45799514e5aab5e", "6592157e4f8ceb06", "e767d19b7b58a1bf",
            "5c0bac11e85bd5a8", "9b991ecdc7400889", "e8c6e0008dc3a8a9", "d7f5be7859651ccb", "8342806c06a4639e", "563bbfa7473f560e",
            "2960df1c2c148df0", "c5908a989c49c58e", "3787863e4abe14e0", "7257ff0d70d85ab3", "a8b9fda33b5b0ebc", "204a3eb249954fd1",
            "b3820721ed65d984", "e9d459f60461ac6d", "8addcc12b0b5343b", "d16dc6a3ad93884a", "6b890763ff45f437", "51f78604c42f7c29",
            "b5be707b0cec3472", "baf711447bdc31ce", "95c0b32cb574c562", "338ec51048164c3b", "edafab8d3661c4e7", "24ad0d5bd96ab100",
            "cbb52e6dc1eba18f", "ea28dbe27f97b1be", "7561db5bcb6b4dad", "34226cf4f0586441", "84aab13d8a336fdf", "092af5344d48915d",
            "3259a42c87b88ace", "c407afd34f300cac", "e43f30c8ae62d88f", "4c2f3069fec47fe4", "c0ecd118e2f7d0c6", "b78bf729720fde55",
            "e2b6a92499b5d9ad", "d41786960fc66b7d", "72323db269db1f39", "6523000e98697c6b", "1b12b2f5c8c429dc", "70a9925d804fe29e",
            "d46dfe9c4e3b7841", "aaf855f8c609ce04", "8d21f7cf7d6f11f3", "8f39a4c5ce906550", "6f1c0e3bc38b1118", "526a2c35d131f785",
            "0052b3218f37d727", "5520ddbca7d35c45", "4c41a206aa15d4dd", "644a0657c7048acd", "61ebea8342cb31f1", "2bb18008af43f706",
            "4627fedd1754937f", "905540b1e9d9f55e", "212bfbbe4a1a933d", "6e6f0309ba16cdcf", "843e5a6cd2676534", "e3f239d08cb4f1c9",
            "9ea7d50df88d0db2", "fb52787f4d2a6652", "e3cdbd3b0fffbf9e", "4f795bf9e936f60b", "13e5c3c17dc55d85", "b67db6c423ea6e77",
            "c2a1de4bb755d59f", "eb026258235dbef5", "681ae90f6e18b3ba", "85774100484f6052", "a50589626c209307", "a43f9bf5c57bbe7c",
            "5cc60e72cdd85f5e", "8238a65359147546", "dcc6ae43cc943693", "aeba53e0df441fd9", "5c3534e3cc43042a", "09936f410ee7e7fb",
            "e65640c9c8919114", "34337aa54e48ced3", "bbc01d13b0ec7d74", "adceca515784ab0e", "5340585bbd39f985", "c6d85a49f258d517",
            "e594e852af0c0b42", "264bb5132e02e903", "d5b86b73cae55c6f", "dc86232483946912", "d492786c6e3762b6", "da521df815aec34a",
            "3e51a2896e500956", "4d18015d4cb540f7", "995168ca01c2d26b", "7184ba4a615f5eec", "e80336e58ad41c83", "bbbeae9ac54cf611",
            "96c19f10d9f9d084", "88264766e1a508b7", "5c3204d256b6e0b9", "858019dd83118518", "aec9ab0633fbefa4", "5632cb2c020263dd",
            "01eae7162064517e", "bd6264ecb758ba80", "f0abda8784cd2707", "6de30ae4a0511923", "31e27cc1e0b8044f", "e61a82fd5c85bd12",
            "7017ebed008ff3fd", "309b9cc304f8164d", "a0b021aa196d4591", "52ea8438722cbd47", "cb8b82004576126a", "7f17d36f79ff971d",
            "cd5c2400b21604ba", "bde292548fc539ab", "ed43b20959cb6ba0", "2002c88171a287cd", "06cfd75a28994213", "79fdbb04ea11b66e",
            "28e5401776659e81", "8b44485775fff671", "304ac44f18b55fc9", "e5cde4e690f525d8", "c4c78b33a41f3c72", "3d0471a03041b9e7",
            "98595f5af2a18261", "89052922c76b7759", "c880bdf16af03983", "e7cccd211cde49e6", "627cd2061489f3e9", "c2c4a35f8933a263",
            "a9d7bafd964288b5", "543e498e7b51b7e2", "ddd555434f2371c4", "9f690e5b5a01f0b0", "c8e243730150c5a1", "ade090734a3b18dc",
            "4cc456016b4be9f8", "7e6c3c46585e2452", "1d46054b473675a2", "2bce4d50c29dcf56", "797ea25d489fa7e2", "fc0dd914a61c476a",
            "e4608bebe36937dd", "fa3ec6aed0799ef6", "ead17f77d5644052", "9a786eda53909239", "965e9130b08649d6", "042a7b3040fa683a",
            "55402ddf386abfa6", "d35ed23486bda474", "282ea65e9a9bb331", "cb3e8c70815b666f", "991b495d673d58a6", "11bdfe3193b442e3",
            "94c55c68afce4bfe", "b88733fa417e8e1c", "a36c58af3b50fed7", "213a316710925b6c", "c9230277be5bbd80", "811418429192ee00",
            "8ddd370897add1e0", "7e5140221de6f90c", "50570c3e8d04f75b", "af8ef485053b60cc", "10192624e9402015", "7bb37b13ec6868b6",
            "b3015bf496ed3d5b", "618b886a8c7a4f8a", "6f93e09594a788d6", "d0c1e4e8dc18264d", "879ccb2ee6cfa1ca", "78b1f47ab6f7d9c2",
            "b9f6754b5d85d8e0", "3a43782dec30a3fe", "492bca001a41f913", "3e7f5a46d8c881a1", "08faf37889bbafd4", "b2e150fc323d7b76",
            "6ed9f510ba5f7a36", "3a6e12000d1e005b", "0601d4bf22ddf4ea", "5fdf5d525a297633", "d3e23e06e94aae96", "87ec5934d56aad7f",
            "2ccf0c9291ae9d0f", "52e60e2833d02dd5", "bab61ec6234fc969", "0d8d2f1aae1787b9", "6b3b25093db6ff73", "d32d599acd3a47a3",
            "020ac6d956868b9f", "b3e54d12fe3d2ee0", "322d2a03cb415b42", "4e4c33396b9bbe11", "b6af17fa0147fa92", "bfd2bbd99ddd9d33",
            "0f78d556f80e29ff", "a3ea62ae2599b80b", "d05eff1859e22c6c", "f42e3a733cd3e821", "a1f58071e7f8e69b", "5a7eb26414cc9013",
            "a336fa0a8fa1f139", "8e7c2f49c007f60b", "54ab42dcee94f750", "3eae545c8e34e758", "b05f14093e0b384e", "73f39eccd0bd7ac8",
            "b840cea890001ec4", "1cbbf42df3f80978", "b01728700a8ae59f", "f2fb0f1faf329e59", "dbd9beb97b269a7c", "d6daa5b55b01f26c",
            "3a1bc77ebdd62008", "52747783309964b8", "2c3ecd75e786a019", "9b635bcdd93af62f", "a8ce2ef3ec3553c7", "9ee9ffb9fa913f55",
            "06f07bbea0d966f8", "abb2bd2ac4991bbc", "b72135719e238255", "8f6d7e7ad5de42fa", "285b4e0b1b91dda9", "8adaed770dd98d97",
            "9a850625fd6ae075", "b807179fe1379cee", "f8684e7ddd19aad7", "431b57c3ca4276a1", "e426cc46d6ad376a", "75055642c5f89cf2",
            "f13c596f78404b5a", "7ab98c0f9a34cffc", "8f76812fb69547a8", "a67c970a518d5636", "743cbd11fd847cd1", "bdf5fcd5cc21f25f",
            "a87400c951754e4d", "41234aae725f54dd", "09b8bebdcdbd0de9", "02a40a82c29dd491", "1e64e1aa42ef5337", "7e30fd0c15e9da89",
            "f92d0381893ae14c", "8a6144563d2048cd", "797e19a8eefddc96", "dd0efac12934f51d", "2a426d53acab4432", "1e0ced4c7ab53c6a",
            "aec3cae4c6aa5cd1", "1404e8ba89492980", "ce72a5c411b1c0fb", "9424163a152932d9", "6f77c8dc0987026c", "7790cd2c11307327",
            "44636d99add5b57f", "fca2ebd79e2c2326", "093ccbec1f622bbd", "32a6141763e0d4da", "5a0e222ac1d16d67", "72a2955ee793690d",
            "69a5156991166087", "1d0bebdc463410ea", "60881a90496a28e1", "5514f0266353c0a4", "5741eeb620e8d538", "a132c6b3e1c59b9f",
            "4fd9931cfbde1472", "3eb3cd1e220e5d30", "ea64b8766d413609", "3d20b195f1cd37cc", "aa08c3b341bdb000", "9811589ea2fb1ea7",
            "7c6d53bec47420cf", "ef9049ebf3508a6f", "b4052d41dada5154", "57a8649b2df993d6", "19306ffa251eae60", "1461dac049153e3d",
            "db2d80dbab7efcad", "dd73b1d318e4c5a5", "b7a3038ec9f1d057", "44079a29abe6d7ec", "deb95bb0062093e0", "45bcca7d20a444db",
            "b44d364b033ddcfc", "b758976e5237188f", "fba977696369a15b", "13151e6286962c17", "77fa2762c2b54fc8", "f4796eb3c7e14efd",
            "e39035dfd08c3cb1", "afb7de7ef631e91f", "27ca1304eea75bd0", "8ec506dc93cbcfb3", "6061800be6d4a1a3", "597e8a479dc6c88b",
            "6c8907cc1baf249c", "1e1ff34a4cf5590f", "9be897d320ce2642", "df9f11930b73d563", "6250a9636473cbe2", "3953fabac5d8af47",
            "5075d367d39ccefc", "d08df229104f4885", "efa7201eb19b5744", "8570d75f18f38c98", "cfc2cda592e2177a", "fe5cb71cdc3d4d5c",
            "2d46423ab11cec06", "1e4dc177c93450a6", "f388aa038499b313", "6c7ce514ac27906f", "ebd13a9583979eaa", "2ca0769264654118",
            "693007fa601e7ddf", "e8f48eebccd5784f", "f27d835a500b5032", "c36421ba53e5073e", "b447595378672d41", "49538302904edb2e",
            "6ea3bd69fcb1d04e", "ccf5453379c30058", "f258ad4fb62f727c", "bac387e8449c3175", "62061c1baca4bb3c", "c423beeaf4cf7327",
            "70e57af34e90c827", "651790a944b3643a", "e0af7bf264f5c222", "0fcd018841416d15", "5e297f76ae998db5", "2050c093c673fce7",
            "afff63d7c54f1a1e", "53be71c9ffceff03", "b7b231d67973cf4d", "76578c213d1fb37e", "426a74e01f13804c", "aaa86071349f634c",
            "a60705799fd22d27", "7c808b6de271b130", "20b8420b12347267", "5cccf9d8e877312d", "e606a5a31c8a4e72", "1a4f6345627f96e6",
            "6078c62a255ed76c", "12b652965f8845cd", "3bd9d8424179abe3", "095491668c93aa4f", "efaf0e41143404aa", "0ee360ca89dce352",
            "7759a87262ac7c33", "3cfe77fc74a38b36", "edbc2005bc025fb5", "d304e380869d7d35", "fd76ee5e6d9a8f9f", "6a8719bfc43d6e9b",
            "7c45fcc9b2b50508", "e30ad137c2bcc679", "0e4d1d26e2c2acc3", "0b4e176c9720e4d6", "6b3cc22aecc77dd6", "af357b164c2ed949",
            "69ad9329fd50b3b6", "2187e624de2ab32d", "31d6a0834a92e8f5", "3e1f2786bb4528cf", "12329f138bb03d2b", "2c4dee283a31a2c5",
            "be7e27f5d898e22e", "e902d088d567195b", "dba299902b8f94a9", "159338aa6b75808c", "67a6cc4abc2e442a", "63fe8a8aa3376729",
            "9b9398fb431e55b6", "8668191ce7bb15e7", "f9cd2fe7a0228cd1", "a209190bf0d6f082", "ff53db41e4a778fd", "41c931b3fdf468fc",
            "7d987a2a370d400f", "07115f6f3f26a56e", "e547e69c82804239", "5b8b7753c2e3fccd", "4cf569272e65455d", "8d8cd74d569cfc0c",
            "d414e7f50055fae2", "52588d5cc36a8002", "6f239d9a9f7df6a9", "8e978faa6f0f6644", "21f524c19bcc852d", "1a4c7957b0e9093f",
            "83812057a5c0bb24", "753ab94a2d44de90", "0c5521869cfb8f9d", "516046e41cfce308", "0881393cf09a995a", "5984cf4124ade639",
            "99556b7c8ed262d8", "279387c69487c951", "42fd6969333ae409", "78dcbc5b7c7c3f58", "ade947100b0d9084", "18e6c8cd8f3979c6",
            "1c58fd922f78490b", "725cc4f622305943", "f7df8a9ce1b149f3", "3259ce61cea712b1", "865f3950682f11bb", "ac07a2c618dddc47",
            "04a8b77dc8f1134e", "11d7a003ecc78efa", "c11a306c64a09c27", "e118492221624a24", "5457008c5ff7341e", "a910e50484431799",
            "206cd1197c8c4a66", "bfc59144e5a30c56", "a1dc64c351ef754c", "549ee3fdcfc658bb", "56e453f2af84afd9", "1cc2b3ae0819cb99",
            "2e441c598f7796bc", "8a7c328a557280dc", "37152a3d6a618f62", "0340fe3633175337", "7981fd7526656af9", "a2e33fb47893dba9",
            "f1f2b101e8979bf2", "f36c91ed271e105c", "6abc9498f9a2e730", "a3b5e2ee91e234d5", "dcd67584517a350a", "2f407646709f7fe2",
            "580fc089c784bd4a", "51a0737388cb6a91", "25d74297451fbd03", "3ac645fce6cebd24", "a8d6981704433520", "f02e2af8df5459a4",
            "c8d71d2c4f2706d1", "05c585e4f1bc04ae", "780b3ac3bea69218", "2c9208f696bd7c4a", "94a16907b5dfe4b4", "93e829865586b59c",
            "1b3ec6f93f902a16", "c4eb505884fb54fa", "b08fffbd86f0a4bb", "35a87c50e313ecbe", "e72ffc0995fcda8d", "b4b55408cbd9f623",
            "656a057807fbcb42", "af3980d8ec680149", "08288c8213502b8e", "42c9a3aa863999d8", "f2585524b3439aa6", "c904c03925a15f1c",
            "bd93c82915f39564", "0091c2faba560517", "415ecd4668363efe", "235cec31be61eb71", "b68cc3e6c2b190f5", "f1b8f57d9cb5df45",
            "ba3e3344c52aa710", "cecd25264abe2a54", "a841321fa3a21fac", "691365b1939c87bf", "a0651d6ec4ad488c", "fcdcbe022f7d01ec",
            "09ea6df536198799", "349dc1c6781cb2d6", "566b73865a92869c", "2e34919aa1a8b588", "fe3c1ce1fa087be9", "25ce7733e31311c6",
            "646cec287e9ab25b", "a319aff6d928b4fe", "ea4c521ba0c91ad6", "df0af3c93089b86d", "0c9d9a0cbd13c510", "b067c51d2938873b",
            "f5a4699422c0634f", "0fa79537a2a95c3c", "0ecd354495a6222b", "33a2f3c9f90edb3d", "5ea903a9ffff0156", "543301c57fe34a9d",
            "14dd0079a49aae7b", "7b7c39d6c7b02124", "b98c66db97037f91", "283fdfd4f2b672f0", "ad472e02a6a6eb58", "99f834b3ffaecd5d",
            "0218ef311e6ee695", "929ec02750ac6fd4", "5703ef93ffe8e3a0", "e1bf50fcb2df08a6", "c4003937b8245846", "6ea7e56f6e3d39b9",
            "560b2e3ea18f2091", "2cd05b38a5b4a540", "e601b7072af6e965", "8cee7947e4bec1df", "709631035b11d832", "bf23e970c9f125a5",
            "a8a25e69eeb5a887", "9a9ba6ef393bbca9", "4464343e7b13e991", "9f7d52331a1e3ee2", "4466c2d392846d38", "8527f2b68efdf7a2",
            "da17f311f860657c", "d9559f59e27b93a8", "0ebd41272cfc8010", "edaa9e08d71191a1", "6d475f94fc44ab98", "e50dfd3057bd1409",
            "14473973172f6610", "82f6c9dd4abd5641", "04525aca9299670e", "523433031d8529f9", "752fce773c7364f5", "5c9e9d1d2e7dd19f",
            "c9acdf33974bd63c", "0614fcae582b2b16", "75e04c4b86fa2c5d", "3a91be49c771400e", "356ed58f568f3118", "b554537fe2ae24db",
            "1d5d5c27239a9213", "e4c6bbc781199532", "b027d391b650cc85", "3b90a82cbfa56330", "c219485375d3cc73", "7048598e8b71ae6a",
            "373ecbb831ad14a7", "cd5ffa233b6c7202", "3b431161f8e51d17", "c890c9fa682b5300", "ae03bfeff79e2daa", "90d0c7333b22d345",
            "50677ce3dd0a9687", "d30e02640c5cfcca", "072513b6d474726d", "2579d56b638599e0", "83739e6b3c95461c", "c72524bc76b2c8a1",
            "b99f7955b0b17760", "b0e737a6612e6801", "583781922d217804", "1ca5c2f88259cdc5", "b4083f5d91d41c21", "c1fbe9d9ddcf8964",
            "4ae8048cb14b827d", "7841d20bf65c85cb", "ea5cc000e605ec80", "197efe45c8b1ff40", "d1e7ebf2d8c653ad", "91808598ba7cc940",
            "889a68662d7d9ca0", "5f264b29cbae2223", "cc4b03fc87a152f4", "d786b2afa27d31b5", "138e960058fd8250", "314c0508490b49c3",
            "0f8df33c84d02034", "579453342c1c74ee", "1faff4e96a02e877", "5c1442d35d52ed07", "e5ed9ec0e827394a", "cd5259af24981395",
            "c74eff5eb99486fe", "623fdbcb55baaa40", "4b23005299c60eb8", "62a31215b34077c3", "442059d8001a92a5", "03f923526ee0becb",
            "8bb69de35840c988", "698e5582fe275332", "9cd686901d2f3782", "117c218140f59595", "14e0bde52d77fc4e", "541c88405f776778",
            "f78f06265061d861", "a08fbcf25ba408de", "b8aca0e6b822d892", "1d382b7a85388977", "98d44a1e9844e5f9", "9327ac5807b5575d",
            "57ce5eb417920baa", "966e96941a907439", "ae34562b6d885917", "52175b1c49f28049", "f08bb34df551bee0", "caf984fcd873cbcd",
            "b8883006405a90db", "9e0acd6bbd7e1e12", "f92bcfa1886528f9", "dd91e70df0031370", "7a2db031dfac5c85", "cb3c07ba3bcf4776",
            "611e4561508f9dc4", "da89dd11335f464b", "5ec7b99c873c9303", "d9a88f9e196f9fd2", "9e85f024cc6db11e", "610a005d48b860d5",
            "cfaa7b7d921c17e5", "192d349554ca2ed4", "7c98336628ceba66", "6d75eae3e550cb45", "0b82c6ef7205d8c7", "f109852e286b4e57",
            "491b061c4c23905b", "5b3ee1595c352a80", "3167283aa67c9bc9", "adae14ff47ba0188", "28f576a0109d6df6", "80867595651817a2",
            "aa4b39a96e64cee5", "4b719f528d9e2420", "b3572f72878ff144", "d41fea7a980b6474", "09fe00bbf1ad5f4f", "b0e8a852454c1118",
            "b34156467cef2e94", "a1204deb26a3a388", "99d8288735e6200d", "deebe3825843f818", "6e2a194f8a4e67df", "d24272878618df23",
            "5cc6531b1a241bb6", "2ffde1e79cfd16ea", "171542cef53235b4", "d7bdb54ae2fd5a80", "3a5285d1c31b9347", "99a0cd5260980509",
            "f6c3c7f8552bb133", "a2ef3c0da92772a7", "6e3b4c56b095feb0", "de4687fd16b258bc", "836d97bb352aceae", "47eda32723bcb71f",
            "f9ddae51b3a7f83c", "e6ed6aaef4ce6aec", "beaa851e7c713b5d", "33c195a8f0b86b1c", "873adae24bb2160f", "dbbc13e5c2c515c6",
            "895fed11a3a2383f", "2dd3f36a7ca7e2d5", "0f9c11040ee6bba7", "0ebc8c50d1623f41", "db2d49c2eeed0c3f", "2d4387a4fd834303",
            "0999519094144c16", "2ebdd54e88fcd8f7", "ecc92f88628cf1a4", "4a482dc47c332f74", "f7a3d2e353e8bd01", "31a0a1f0c12346d8",
            "3fe9370a777f7742", "baee60bf4b99f7c2", "c9b5e250dc05baf9", "9d63815657046ab3", "c0a6db68fbc348a8", "a3166a510a484909",
            "db9255d8e7f2e0ef", "7bbd9f3424b135a2", "529067b198d408dc", "ee08e101d7310e60", "0b336f81b6913b7e", "b612f510d11260b7",
            "6d206d7bf08e82fd", "188795d61650c75a", "bd69137ddd339b23", "e038997af467f836", "44e63cdb50fa3118", "ac1d1f31a69d9afd",
            "b1a6e33d1fe7e1ae", "2a8822e3d2ad68a1", "ef27e049ae77b535", "b28d01fcf02dbffc", "ecac1a6941e7604a", "ff295679d38f1c61",
            "f2a363842ee9bdb7", "0ead07d93fcd727c", "403e863c5b8dd893", "0b5f0c1a81f307b1", "b9463166d7be9778", "ac2c6876ad8953ac",
            "c990dd5207f53d0a", "28d3d5ac97647d3b", "2bddaaf5d0991f59", "bd6d34e4af2a10d2", "a54471e7b303c628", "62f76ef4299609f5",
            "5d1a369a55c264c6", "43373005e6a45e8b", "df60d6dd967b95a0", "80c586e0b146ee52", "b3cf88a59559bd7c", "83c9c27b5385e4ea",
            "6005eacb2306b363", "2ee708e9ffbee1cf", "1e8e16ce6d62c021", "ee51577c3933dc74", "d027ee810048b6bb", "58e3f1d8f497a903",
            "067820058a9af5c6", "0bc3c3531dc91e9d", "c7df30b7399f08ac", "b9bfd13986267642", "bcbd897cec69deb7", "9682e8b68e15a160",
            "4817f8468529daaf", "3c7efce290d7ac1f", "bba5bb3ffa057eda", "ce6b11993c6d2d00", "b49435e741f520fe", "bf163275468f80dc",
            "f2d20e7686dabd63", "5320a7718ecb0449", "939481acff0af044", "bb57f3c1efdbbf77", "b5928c3c28e59561", "dcfc6b62f990e8b5",
            "a444691d7d098b4b", "40465f8017e6fb51", "52e0f2778f272431", "8436c696e375817f", "3911298b8dd168f5", "91dba8097e7a5768",
            "10470aee5983ad38", "ddad5f5618e9e5ad", "93db1f62dc464ced", "c797b133df551781", "4ab6f77daa39cd11", "1833855a0b379800",
            "17870a94809dfc8c", "2e6e025bce8c5dfd", "8746194933cc34c1", "f007282ad095cfd4", "18ae09e72cf1b7f0", "3a464ca66264e13f",
            "55c059c15e588abb", "7012af92d1ff2a49", "921f7d351a493109", "8b6a924bf9e0dff8", "8cfe5772893c4881", "5f7395e89c2a54f4",
            "e715c40fde47f93d", "68d5df9be21720bf", "e9157accf0f86a51", "0a94b59edfb5e236", "bb3e752214c4d1e1", "5911bc3b7b15713b",
            "9e84e4ccfad02f8f", "12c95fb94e9fb880", "d579654f68e40ae4", "a95207c4bce4aa06", "673c0a241a85219f", "2867e798d588ebc1",
            "1cd3715058a8cae6", "efe92d28f543f5aa", "0533895fa8c8f3d4", "6c3b583bf651d2fb", "05b311381cdc6aa6", "04d05cb1dac92114",
            "7b9ead153d61900e", "d7e5c7922ef100df", "30453cf067bd47e7", "d5c7c0dd0d84676d", "23578301daaa3e95", "8b3fd7cac84f5849",
            "c3d75a5026769fc8", "94c0a6cc179804e1", "7e64b80866319573", "a1258fcae1488ad7", "9983fbab29c73714", "08a30d935d11401c",
            "ee86c10792d1f62e", "db41f866b54bce34", "fbbb12ec473a77fb", "56f15bed58777a88", "45d2d93dfd64d743", "3542a52dcd34b202",
            "5385dbe49f1cef69", "7c113c47a0fa70f8", "efde7fe838fca1e9", "1aea2c678e23d682", "80201f419e28493e", "1317295634407a07",
            "322e8199cc27c0b2", "158587d6b44ee2b4", "b05a6a12c774603f", "7bac6132a903c34d", "82adda3c62439f7a", "f8a71760d94b636f",
            "aea3aa9dc3addcaa", "6ea9743956b6dee9", "eb9ed0b2dbb6149f", "7a6a066097f4c817", "626f891b182de51d", "acf15681a4d50963",
            "823e78a74e564a56", "dfbce5e4ddf16cdf", "c114c16d50f6a88f", "ddfa7069e53722ec", "524dc78fb8f14a3a", "cb6f4459524264e9",
            "949c87e7f65c087a", "0c3398e1636f039a", "35c9228022451209", "73ed49a3cfc1072a", "d3cb8eda265a3a0c", "b09de41ada493dcc",
            "e3be8aa5ae03b887", "214025fae14d29a6", "1af4f8543225fda3", "76d1111fc7f05b4b", "421d09df40f3e47c", "da5143624b84ab6e",
            "5cd7de9c13f7c148", "3791eb1f385582ee", "02b911bc6bb0c1b6", "44b0a88eafa6e962", "dc11a27189d2e823", "2b991667f1d3d6c6",
            "1cc2ff6a97535a5a", "dc94fe849a77ce42", "ae8ec187ae923e1b", "1e081c6e696953cf", "c469d2eecb5e0e18", "587741497ab63ec4",
            "c24b21b30c8830a9", "bb5e4c0594cebea3", "23e5b34003ea3804", "11add1cc4602c070", "2cc37c45d686abd6", "c496a562ff4cbee0",
            "8ef078deaf1d23cb", "26f2a50e5facd012", "cb1082e91703ea00", "b7fbc7f8c36d3476", "9394ad1339c3c731", "88f273a95ea199b9",
            "17cf8c613abe4ffa", "23d5f2e090782231", "e2064bfb56760c93", "171f05a77b97a3d8", "319f07bacb7775b8", "351ae909e4201e74",
            "136116c2be2fc8d2", "8e59f10db2f700d9", "7fbaf0f5b8c2d1c0", "e5def52bc5c2fe3b", "6a23ac2c635fceb4", "3c52f90f16f1f573",
            "b35b72b142d465b9", "e1a89c70ca5ba35e", "131ff45e510b0ba8", "a7b5db2554f98f6b", "f64bfc8cafe6e4c7", "9007e1f8469287b2",
            "7e9adb00c36153a6", "7d29e2a4615dca2d", "a8e13e3c32b8b710", "8c782b27336bb664", "ce7b6f58661975c3", "34f65bdcda0a6de7",
            "5356b76dc3e87a1e", "9631067fc929a0f4", "bdfc4107f72e738d", "1750460be4181464", "ccc319daa6891d18", "5204921a1d30e443",
            "2c196e4dd55504bd", "f39e278502fc294d", "39f6ac46a914b439", "9735b35fb758253b", "a1c79f17653dd0b8", "dd99dfeac90a84ad",
            "74c577dd3c9a9b26", "6e5f799285f05051", "fff9892f5ac35546", "98d27b2679480fcf", "84b459ab8f9548db", "513bc715081b28fe",
            "a6c6d0bda4002d4a", "176215e292ca1fa3", "2ab6cfa048f19558", "3422bfc989517589", "c15753bd234bb65b", "47075163a5887519",
            "6ab7b2095eedc090", "f0ecf2ef364b6800", "9c5c1f3bd463413c", "130dc0ba60fe8ded", "fff052c5fc07b99b", "d3f2871cec1f83a8",
            "7559d7376f68b57b", "325c2ed191afa8db", "ff25ddabb43fff26", "2772cb77ca709420", "7704755a55cf29c2", "536d98a1b6659366",
            "dba731fe1d095dec", "223759b4e8ffa4fe", "482bd39dff8771ea", "7e6bc739864d7ea1", "3ab1d75d43177dc5", "98a5c3c55ac38e70",
            "7346483e9a677c7e", "f713e97d94c1cf0a", "996d2a66ddea8257", "b030521ec5f4ec98", "22415afbbc499bf1", "665b8d6c56d2c921",
            "fde6b257a5c06356", "a066a2f529d44dbf", "771dc32815dc7e3a", "26f9d29f4b0eba18", "649f9e6479ecb1a2", "e2c0398aceb67b0a",
            "9c5b2a29f5cc1916", "21edca3efbf94ae3", "ba0a86bb2fd7bff3", "73f58d06df94bbd8", "f634e887bbccb6ba", "a91c6ab422dddce7",
            "d5f5dd399147a7a5", "8746174f0b3cb79d", "395e8717249179f7", "a59f9d51494a9c7c", "77e26c1a41c48943", "820ec8a9bb710f7f",
            "aa423f7a295527ba", "caf874717b3c086d", "9f062e7db9adfd30", "1e5fdc07611a21bf", "fa1c652d97c2f76a", "33ce0d82ad2cd8d2",
            "e426f88d9c986357", "c5f3f8e1b8fe9ac7", "c168dd90377e8085", "fe79570abbdd23a1", "71fd2cd424f589f1", "20d1f1928b9d5a6e",
            "c5bd4490221cc5b7", "c8f9dbb2cd2ecb1d", "eb6e437da698e591", "c36725c10baf8fe1", "28cc6d7a3303fb4b", "52b97b907ac24a17",
            "818eaa970be137c7", "2fe807a5598abe82", "619b6a06bad15256", "bdc836a1ee45e1ab", "453493e91fe0331c", "73025bdfc573fb94",
            "5df9e28531ca69c7", "1d65b9dc8832fbf0", "8651d29f28a3c22e", "6ec658ee9efab060", "2b8a9d7f607dac2e", "91fe01f0fcbc0c6d",
            "73b44feeeb237b4f", "6f5d342eb1813c38", "8f0a0a9de1f244d8", "ab0cba0a37984b46", "b67fcbfbe7176ee3", "e4a3c70a3ddfec71",
            "eb76c9b4b13c6373", "2d2e554fce9122a1", "4fe636b5445fc1dd", "1ab193afbc81c68e", "a1c8ef5a6f3e9ba8", "80f8e20c78ba7f9b",
            "cffe828594da7abb", "a3e07007b7b8a9b6", "d0780d8925775056", "51b1985471a11ff7", "33271943a04a7d8c", "2b605143f83b472b",
            "55fe0e1d3885a2e3", "81dc8648f67d6e68", "fcca0f06f8406216", "f5bb963f33202aba", "97490e4392ede3c4", "0d22ac46639099b3",
            "e64e9a4b5a88de70", "b4587ac0e60dbee2", "79dd0655515461d0", "38d6859dac3af637", "8fabf1b4e516d7d0", "58000b850cda83dc",
            "ffcf0c2c61d23ded", "c1ff2c948f0ddd45", "4b00de3ab3adf8cd", "23aa6449a026d8e3", "09a8d11a12988cb9", "93422e552e93e32d",
            "f357460af5497dbb", "397f3df5ed73d256", "10bf98cd3bb1a296", "25caf801af9381d1", "a9beb87416be810e", "2bfeaf3c98a25ea6",
            "08422dea2ffbfa99", "c6bb06feb5153858", "2b025642d790ea8d", "973fffb54fbf491b", "94e03a151ff3d329", "a849a48bfc7fa874",
            "62982d4ecaa1e698", "1d5a9f1df0514239", "099f472c5fb91faf", "6b9fc166a0d3b3cf", "e3875fb5ee5fb666", "7e15195f73f1591a",
            "c41fae75a199a226", "db9950bc341ae53d", "6f1e0723f1d09311", "92312c6a2c088122", "4a39f495dca35a05", "37bcb6d2d0192b61",
            "6cea197d17b2b748", "e003baf97aeb0aba", "2dcf934bc39358ed", "ac84576f14401908", "a6ae5115624afc0f", "6c21bf71b8784fc0",
            "a33af5f615b67e8f", "dec746686b9760f8", "0f6a7670c3ed0d94", "dd77cada63269de1", "b53fec50ca5534e5", "cd31eee097689af8",
            "645dd7678ad62f98", "9eeb77109c74e29b", "1599e898a923318d", "1cfd344b5794f513", "dc8b89436fe7803a", "ee9ae0c5ba4f970c",
            "eeecebefa6ce5561", "a26a5cfc526e3da0", "b612700442023da3", "7d9bf0169178be76", "652f227036fb049e", "7695854a21f5d405",
            "2c9abb2d181f3b8d", "fb40fe8b0a0a3b2c", "88bd668052a92e1a", "5ff6879ec62bc8c0", "825abec9b2679479", "91592e76c61c0e27",
            "cd8f5b8f552bf150", "faf85d350e262d5e", "848281be147745bf", "407d3f08804f2758", "24cd86f4eaabce45", "e23c915ce4b35052",
            "2c4cdebf47314d25", "328a8c23f8c5d2c7", "e5f439b83a6c54eb", "c091fc401f95a7f3", "9cd6679f0bfa2163", "6e3f9c94fd05d6c8",
            "bec26b7bd9eae1c8", "fcf0eb1705dd3540", "9d6b8d2b330e1fbe", "eac0218348cc2d1c", "4b3f1a062d09ccac", "c5853120d1077020",
            "3c9a946901702bd2", "c938b95843aec9c6", "faaef3b2b4aea7f2", "8e3e99523bd93e4e", "add959a3bee78e0e", "681659de5672fb9c",
            "0ae508f74eff1485", "0a327733b3eb7037", "acc3cb9f1469041b", "c862132c173c0b12", "721db4ffa03ea4a2", "efa514665f0a08df",
            "0af6664af5d29fce", "fd5710274d913e99", "ea3a7248c36a56e5", "35fdb976e31383ed", "aff930cd6c8b42dc", "834cf8538680ab08",
            "fa137d0e884d0b5a", "ce9b94ffde491569", "2af8ca1e402922f5", "6024e247f924be11", "d4ce03e57ab8a38e", "75e64811b137e2ff",
            "1e84b0dc1b329a3d", "a6222b4e1f310b6b", "9ee829026696f7ee", "07c3361c28214b84", "2fc9055eb97567d2", "8f7c0e06f09f954c",
            "18dc661122b9fa7c", "04189b1de4fd49f1", "fbe4ced74bd90d26", "008db66b5263dbfe", "cf1a1e74bfa809db", "1d2b0482a6503414",
            "41389dd4c23d8a38", "702248134fb2df0b", "8a424dd186f3ddb4", "b99fa9fd37a1204c", "4f9375cbe91cecd3", "9e61144e045654de",
            "a290005e761a6538", "1a2fb7f9acf432e8", "1a7703a74810ba60", "681821771753db23", "b33b623ba4cbbe7c", "96063545d78170e7",
            "703fb954da213cb9", "0f8a857805e2c124", "e7993ca5d18049f3", "5686b4b33878c6a5", "370f0974d51a58a5", "7e03586b6620bc8a",
            "e12a9f49220f0b1e", "aa9c28df38473a62", "2b5758b4e1377f4b", "0aa13d01c23175c4", "a574d22c8d91d2e2", "0382ddc347c54088",
            "b38d3c1beb53c1f3", "4fdfba7d3b950638", "b19f71fbed08f389", "c9076fb721e35719", "299f47321846726b", "c854bcd1542fd266",
            "f2e390abc1194201", "105147b7b8208303", "3dbe64c2809467d7", "49cd996c9a2f041f", "68d7707dbe4dcbd2", "dcdd950b3966543f",
            "fee08121b3bda117", "7219cfd2c15f7526", "07a899683540bdf3", "79ea1c389605da2f", "da8f391d9488a141", "a4148e166228b08c",
            "06d714ab9d5cc299", "3f91b6ef4fa2f6d5", "9702e4d755b1745a", "ed49296735bd1428", "c2a3bd63065f36dd", "c4065960ef05167e",
            "17e79983a649bad8", "367620b6cffb8d5f", "f09a78834b7f9d99", "354af1af094a974a", "e9e3b22e90f1e394", "fe90636e23cf0a7f",
            "a9ecf708419d8ce3", "43bdc7d356511ef4", "5a3a57b89a8d5224", "2f168ae732537099", "7626bdaf492636ea", "532d51c33e83e630",
            "de0a09ab9df30ff5", "40e4928b583581a2", "18d93dbf649ffaca", "3c8818563ad4c36b", "a0b9efbaa3829dab", "661e769c87bd4d73",
            "275529cadac84c63", "a4f95c2c51876918", "f91b089af1bd65b6", "de2eedf4e3bbd030", "8bdc47667d30f0a8", "503ce69ed795a6c0",
            "b5645e8efa5489ee", "1cd157c00a292948", "e9e7d89ee6be42e3", "6908045da7c68428", "70819d2e457f4b04", "4c8d5c60543b2b26",
            "245f0cfd0f251fbb", "649e89737abc1373", "f24664ef3a1f9d95", "8c970290fdcaf7d5", "755d9a66d81d2322", "466de440bf6058d0",
            "da4638b50607877e", "cd0fac4bf4081660", "c74d8d05707f94d3", "6e884326ce69833b", "2f875324e91c3672", "fe4ac27202b8f355",
            "0817562ae57eb59e", "0c9ea8ffa5701353", "88731ba7947ef749", "b8d2d7af002d2216", "eb27b06935105b40", "6a19ca6caccb9f0a",
            "92896fb7df867511", "65ce86b3a181a2db", "0a12ec83dbf8a282", "b21f773b8a83cf64", "e97692a091c6bf3e", "8a0d79d30572704c",
            "f8ee5939fb78d5cc", "197aae24512caea7", "1bd326468a27736d", "c8a3bfdc11b882c0", "9587b05ccd14d49e", "9108e9e30cc84569",
            "455af46a0df2afc7", "37a8886a502b4ba1", "c58f1b557dfa85ea", "bd279118a0fe0957", "c65a8ac3fd156152", "c686218c1fc857f3",
            "672addb180bbd758", "021011e82a3af063", "27cbd36184ec1a83", "701f5082a74c8b98", "b8e29c9215c8e28a", "6073785bc358ada4",
            "5bb1e44788003ca4", "6778292864a232e9", "8da62fdc61731536", "8171cc8722988271", "9a8a58ad979c2b34", "42e3f0528fc3a86b",
            "6869324cdc63ecd7", "4a22dc14441370de", "07022d82d76fb9e0", "8f0744f90aafa486", "47c720e932da4169", "032a283b95977c86",
            "fe253b067ab7e771", "737b84d8a7d836c2", "3f2237b9a0b1f5f0", "ed4992ae477022b5", "530476c119beb152", "feb6876081c286d4",
            "a637b402babbe660", "18c2b2304e13b194", "260edd5de9e7bd64", "20497cc388d51680", "37c90c99c666139a", "9e9ede77ef71325b",
            "8c97a4a857bbbff2", "966399fa6328092d", "59c30969d00a4ce0", "70d5d377e6784617", "2de098c69492daff", "0ce92aecd0611eac",
            "bcdea9bda6115970", "35f33f2eab037c4d", "c638bd3a78024e3b", "3a43724f2326286d", "407b57e4544f7583", "83cc0aaaf2460c68",
            "3a58c1fe2c590742", "b058683ce454737f", "d447cda110d83f09", "6334a2feeb37e1cf", "f786f49a45373cd4", "6725d89fd695d8f6",
            "3e07017175db517f", "9e7848e8efc870f2", "f73c2718277a315d", "28d01135e817f1ff", "e9249df2ea7b40f8", "8f785ff2ebe17696",
            "db3ef96337d9dc50", "eada4f76ca9dd84d", "56b079c18ab13ed0", "157caba693271d02", "4fb56c62ff3dd65b", "488a26555494a1e2",
            "70df4e2e281fd3d4", "834c7a5e03dee0c1", "dbf072737b27bd71", "46a6f33158ad7aac", "91dddd5d9a80e76c", "14af926a21002021",
            "7ec1049eda261919", "90d445ef88925757", "71c8e2e45086c366", "76068f8dc6260c59", "b97e7ee9b945bb4e", "a66ee7b6f7660c4f",
            "5d5b0655a1131187", "768fb8643a115351", "ee6069533f9368f0", "83e78dc7cd60d3ec", "9f527d1222cb2561", "9e4bc4d0fdf8c3c8",
            "c69be32126d39be9", "0a0db53d40749aa0", "0055ee1aeea68121", "0c84737b7a5a0947", "457cd0fe3380f3c5", "345936ffd8da95e9",
            "11218cbb5586a79f", "d8bace7d6959eaf3", "5ac8bb9e9e04e92a", "f50e4f2979ec7fa7", "cd46380bc03b8a40", "5187aa6959b3fd19",
            "88229b24bb81e6b2", "e8837b7c2ee47d89", "45a2fd6830219d80", "955b729a72fc3527", "704f356ee15290a3", "b9c29b57d026f1b4",
            "3257a21cfc038a95", "79ce40ca2838b59d", "37021fd1b017bb16", "b753b5df1177846d", "25da028636b85417", "e4188ea80e3d4d1c",
            "5f8b28e45988898f", "3e819ff3ef9b8f92", "f2f39465fd737754", "1331ef616fdbb473", "c79ee55a2d2d8f84", "b70faec5936d038f",
            "2610454b79f63942", "4cb684bc7839ae70", "9684200ad2d999cd", "21254744c28a780e", "c5cca42b9096be9c", "8b5acf36f532a6d3",
            "6f4cddae0edffdec", "5a74394695f02057", "67eb0efcb56b761f", "cf838651e2b24c61", "55a2a75c8212831f", "f5312aa7a57eea12",
            "deedf6025ba5a77d", "99f48a9819b309a8", "44a98529c8ceb2a0", "866ad2b62cb352c3", "32d8fb585a5f4b57", "607cb19292b1e75f",
            "d67277c60700cc8d", "1067b3e3dd5b3965", "f674516b2734a5f2", "2908db4ca18ff439", "b221af56ae81574f", "b6452f3e2bd6133e",
            "07defd0a98db72a1", "643a0ba94e4c023e", "6379941c57191825", "32f6a6ef6af4366b", "e6e544549182b150", "7821b10bf3f6bcbd",
            "b56a41f0eb5414bf", "092990fd7c8c7ea8", "c03c591e488bf161", "9e7b2c00a84f777f", "1e0f1731e4f7cb13", "d65a267ce60f158a",
            "ebf39b0ba28178a5", "6cfd785075a1b5ed", "84a58e7dbd4587d0", "a38dad3ceb419b9d", "ddbc37b211bc1448", "4c02f4cf4bd56fe3",
            "8a350c7aca63e7a9", "4be2d1bae8927695", "64dc7edf6583a40c", "1f33a11a2e0a2633", "d971333d41bb9fcc", "b8fd41ff487ecd24",
            "8711c6d565c2c902", "3a183ba4019ae81b", "e0fcc9f714e4b482", "c2a721207eb57959", "fed2b63efd3baed3", "ddc2eb496fdb327e",
            "aa02dd70cc99763d", "939ec6ebcb95185a", "4db60f809e479517", "0b05d0e0986b4513", "6bb77d526d16b5cb", "6e309ded07fb8fc7",
            "e6b0c7140e5efe6f", "19422dbf7571cfa9", "70af80cb77026120", "68f2d73a4857e98b", "a460f606e8145f7d", "eef1576a0556cc2e",
            "f6f4bcf36f4ad7eb", "66fa552f8295650d", "2005c8cc8b554c98", "26e4eee585aa7b22", "1a7bf5a429b2d658", "2e96dfb5b663553e",
            "0e8a9469a17e9390", "51c50a27470ed70a", "847c585d47ef90a6", "68c48ac963726f15", "567b72cda5e61392", "e22ae02d67c8d571",
            "bfa6123a8d2bf3d5", "cc3232b64f53bace", "e96e20816bdc89c8", "c63cc5c3f5e14f6c", "bb10d2c859accfc2", "29188a9993da8b6a",
            "9e263fcd6d1bcffe", "d624b85a686cacac", "972b2dacf24c2fad", "f56c705c69e0ab42", "447aee1de14d8901", "d99d108b9c621485",
            "18c655bdb0926906", "116291f95edbac0c", "3dcba12291b0bb64", "93eda2f03253584c", "36ab9e7579acc7ef", "f0713d5bb6fda2aa",
            "5176a89ab5b859f4", "68a2d3f97df3b5ab", "806b3668badc568b", "4e1ec9cd52273a0d", "ecaee8620e9fecd2", "efa41d3bc1315519",
            "1f0a07027197e3a5", "163e4294611dcca3", "303b4e0691dca59b", "d03599a27445dc9a", "28e63758f293d234", "eb09bfcb4058f535",
            "90bd3f55f6fb62ee", "ed20fd2d500849a0", "b5a1eca7a7ca1358", "f1a21188480e12ae", "dee84ee1a02c0f97", "df51f4b07bd94656",
            "42492a52fe0a51fc", "4d93a65ad25f3cb5", "f4da44d315ab29ae", "c7349858baf49534", "8b9e0ca16292ec3c", "214a82304da04ef4",
            "ca1dd60cc5789fb9", "d888f844588a9c85", "f962aa2bed9969b9", "5b395f21e643227a", "77a9d76043cdf744", "0fc45de7ac4815cd",
            "244652fa7ab586a6", "9581f9ce953b5354", "d330feeb15b6c0c4", "d8a8e70b3057c2a6", "12670128d9e89f57", "903d4dbbcecc9455",
            "16056ab5fae61dc5", "350c0616f34bb59c", "3915d061462cd28d", "44d6b546f1eda23b", "8eb66b89c1fff2eb", "4611ffab360dac34",
            "646fbb20537b6625", "348edd880957580a", "b356eba07e9d6aa0", "f1ded55858e7537f", "e3070e5a0ddc8bf7", "74f73b35776c8236",
            "499a733f39f91cbd", "117f3369ea2a6b64", "09ebc30c85e0429a", "e34d57b76f704b40", "c97c08cf59979f17", "030e4d56e7f097b6",
            "6d74b910df7840a3", "56db72f1924e32f2", "516002abd1968892", "ddac1ae35703af52", "7ff5296487ac7e48", "192af7d54dbe18b6",
            "b58e4b20e60eaa7b", "01636042b0a00a76", "e53ad3b77413b3cc", "72366fc17427cd84", "75abda07c0b83b07", "9699a65ecfcfa114",
            "a2d64a520fde3865", "499ff4366fafe94b", "bb92281f1cbe4d83", "15099b850cbb6306", "8389cfb48061e82e", "76c0bd454f64b2d6",
            "b821af630217fafd", "866ae70580899903", "553abed2fcfcea8e", "1473829ed1c54536", "21fcf2bc38c721be", "ccf2765228f08d17",
            "59a43c72f32a9811", "15171eb208d2f627", "c38ef393ddd4595a", "a4e74f78efd3cdd5", "c4ca4e2d2b09e4a6", "cdd5ef4ef6d3210e",
            "056c8b98cc961fc6", "6c87e1c0a06e31c3", "bb50bdff57aa391b", "76e93f7fa952dbc9", "984dd0ea8e6b7521", "600c759a291e6871",
            "c94e0a84de999945", "71b9aaae8b1baf18", "0426687b7b6d6be9", "96efbe8216909ef6", "cdc87ac89a899716", "53246244480eeae3",
            "b90e507ff596a33c", "e2f8080aeadfd4b3", "2702f3ec45eb9fb6", "3c59c206cb36d38c", "bb93f3edec765dd7", "89947aefb4934a31",
            "0f0c4c56b1f416de", "835b2bbab8999a20", "2837a907e5eb7e27", "d646f125d37ae0fd", "fbc5bd8e7fdb998e", "6dfe02ea8d3e7e0d",
            "7ed20c0c0e19ceeb", "0aaef36d07cd7650", "9e56dfd1bb898983", "9ce3e4a488a119e4", "e59b21b88957b4f2", "a758fc26e74b69a3",
            "21d7e92143721787", "7373e810a09d8941", "81def84206dd8c22", "cfab1d584ffd87aa", "48317c9e3a395bc5", "774649c8c5d9e75d",
            "c4783ce1faaf056c", "c22243716c3a1e89", "4e373c7dd9d648ad", "978714a1e3911931", "d4a9d52db763b944", "a9ba4ea36ee91732",
            "c3905270f65c86e3", "d656f749a127322d", "ae85710402e9c8b8", "f22f3fe168245e4c", "b4dc8b04b3cfbdb5", "c2905e562a1ab343",
            "6732fa889caabe74", "c5fbde3ee99be385", "4da50b2ae8425926", "9088a02f6d8d1064", "d88c02e8d5f9f5a0", "e2673afd18a42293",
            "bfae8b4ffac6889a", "4a27ba4f8f451fab", "affb21beffbf0de3", "bdcf1ae5a2845a5d", "baf8af9e7d910fe3", "10dfafb4e8f6ce2f",
            "cf01a7489b6e8104", "a9d80bd02e6c56f5", "f43bf99d2299180d", "ec36d86510d8300c", "af5bd891fbfbf0c0", "eeac0cbc146f21f8",
            "97e43b0009699d1f", "a225259065aec48a", "05d94c0000043489", "b7f431bd1b58e471", "1d2f3c61b23cafa7", "58531f56bb6ff2da",
            "a510126d43f7c1cf", "2c158edce292cf31", "ee1f9ec08a4982d0", "4bccced6deac1d1c", "a925d60ec40d166b", "b65c7c5caadfa953",
            "4af572ccd96d70d5", "51896eb9b77a6020", "9e99dea106798c17", "b55763b1d513aaad", "b8de20783aec4d76", "d9c0f27879640dad",
            "1b67eb1f9d910552", "b6d6862865a7ef7d", "432bb6d1d18be53f", "61b2cf407d038b97", "e9fc656fd92f6df2", "e91aa14a2f597e1a",
            "eceeb1224097d2ae", "7c18944a7fd67a65", "0a6e4253f4bb6472", "681735e7bf5b7465", "64d0bc4b0d3822fb", "a834e71dd10ef7a4",
            "702ce5e027418a5f", "e3d001a3bab7d932", "1de55bc5a54940c7", "2111d18b6e21dc80", "adb032aec335e5d6", "27999ec92021f315",
            "7de69fbccd5d692c", "4029c9aef2eab140", "d121d73c0981147f", "9c76e638b4771eb5", "a79d3c17961b186e", "a8b46600a78155f1",
            "a3801e9aa742282f", "a098f25119841c7a", "d14c40a5f135be2e", "042a8eadfa3d372d", "fd180c150f487f8e", "7a4f1396c3e50316",
            "3e01d5939af12a26", "20d72ec5c84a2f58", "b2adf962c75b4231", "85496ce68bfc5c67", "614f9a409622d9de", "54faf7873f4ee074",
            "1d8ef3d973f23036", "2fbb1fc106ffe731", "cfda58d372a356ce", "c92c1f2e198e2426", "53e4e472bef040bd", "91f0882e197229de",
            "48b118c724ebae43", "de32a31e3800cac9", "16532facc5ddee43", "1d9292d393b0d841", "046a7b340c00db9d", "3445fb281c83dc6c",
            "8eb1867578c5b808", "f9b4d2fc59fbf048", "fdfbf5cbe8459243", "feb790816150bfca", "5e07a002b53f544c", "2252417b735edadf",
            "025807c4a9417849", "fb3526f4def3063e", "b6be72003b59b61d", "f0de90f5bcf7df4e", "86efffde2723cc41", "4a51408a73164872",
            "d13f42c81134b907", "7aeaea452081771e", "304aaaa88e68fafa", "669f3c24a3048955", "5fcb8503c3ccba8f", "e1d81949ab2160ef",
            "55dc681ef8685d38", "15e6847b9fd5390d", "62fde3fb7ceb0f53", "c3616f84c4936387", "d5bed2f77f78b8aa", "edcc9b4e9d7b2845",
            "844eaf984d376407", "265a9f8861738f08", "aa276b3ad5846b8b", "2568de6b12af3e1b", "75e355b91b43e34f", "1c46fa025a0e6136",
            "6a29f2b9247902ee", "a6000ee4d6b331a2", "77e7cebc2b11bfef", "1d5bdc139b15bca6", "a4f7d81366574442", "79492b54f923e988",
            "9cc8cbf25c984940", "ee8aff5270bdea49", "9a1540ff42185e8b", "0d52cdab38999c20", "0c9dc365ea300ee5", "5e6cb0f87d4718c8",
            "228dc10cae03deb3", "6e1bc6434c273d8f", "d3bd1d88250b33e1", "f080c3ad1aadeda6", "5bcd68b9ccd83224", "e8a0e23cf7d7a4b7",
            "d9a8637234836f9c", "5f133e9f6b5ff131", "376d8ac0aeccf5ce", "811e3d507e7803a5", "a33438cad6397d55", "cac62bdb5c322c7d",
            "927304321adb097f", "667e61a3ffc45ea9", "b8f8197c9b180fc2", "d6279e541930e070", "581008c85bab548e", "c8054cfedf7d8770",
            "2acea654fdc96b56", "ac851bfd64f60359", "e4ce3b61109cb929", "902775375d7e15d6", "c954e31f59a377be", "45fd67fcaa6713df",
            "0ba869ce325ad453", "e34c1d71c9c0bd27", "396efa15c584665a", "66e641e52edc40df", "b23c115ce89f9f95", "7c30a7fd8e92e5ca",
            "29342f622ed772ba", "eb8c723f8b98b8b5", "a6f270a79bcaf9d3", "eac8652a1499a439", "a8e133320df02cbc", "7b62c50f578fc200",
            "8f5d2eae0b9a5476", "4127e4d6ddf18e2b", "47f56c48004fb6f2", "feca4e07c87a54d0", "6596b54703721ed4", "2c195b2df56b8f24",
            "216f3346d7f63ad8", "6d4b107194293ae2", "2682dc7ed36cd1c8", "bf26dac472e3ef62", "c53a4409e4f78a68", "edc51cec423af9f4",
            "8f261d59f68ed735", "7adbb0f8d0cd567c", "f5164e7fa0484842", "fc0ddfec5ad94969", "eee24ebb54f2139c", "6d36d8c58565bed7",
            "f56c4db2589a0f1e", "cce482aee2de287a", "9860c585d5c2169d", "5fa5f42615d7f1f0", "7a69e3f2f44b49c3", "4789657ece6e0c97",
            "cf93adc950197bf7", "81ce477cf46507b4", "d603252b8424effb", "22638d39ba4e643f", "3e881ffed978b2c0", "8414b1a86754be07",
            "677f713a06291cf9", "a06fe605788d8a1a", "531f2c388be935e8", "43478f0570b42c1e", "1e332729db322c49", "6fa2a68724a03ab3",
            "0539591d7e63f3f4", "520b29206e167e6a", "3b102d36a3e8fa48", "a1c819eb7da9198d", "2296e78418da1a8d", "369c75b0a7f90ee2",
            "53c0886bed9b8f42", "1c079143ce03ce3f", "f2a51a0cbc61b295", "cb0ee7739ddce08d", "9e0bd6d824f98222", "284aa6db64e9202b",
            "7d0cf1f1fc7803a2", "7e2d70d0ed619d0d", "d962e9c56e7e5dc5", "bbcc6f2466065b7c", "98d03d3458d72f9b", "ae334713e844d58d",
            "bdf2bb473e261cc9", "9832dc89acaf83c7", "de728cc7a6c8eb6a", "4f82f3231fa8a0f3", "b61f8b29e66583f0", "37fc9feac92f5427",
            "2b167d8ccc399401", "12504936ededcb78", "a4ed4577b3002282", "f3b74fc5032e5825", "c93164de1d75c053", "e1e2cced5349aa86",
            "89454abf568e33b7", "a038e0c0632bef24", "5b72ec1c6b05934d", "53e4a3df87054a04", "bd38ad08f68f33f9", "ef2a5bd862058eab",
            "3b83b7902ff9a760", "d460f6cad0dc2376", "11a1e48e23313d15", "4c1dbdc0fe327b18", "529f527b47dec46c", "7c6e386ef2102e97",
            "f8c30a4dc22f7d22", "89c70b4b0321ac78", "3f3d534a57f45ea5", "b4c4970c324a5d07", "07ee3359a4662728", "f05e86191b2fc628",
            "324c77e848c6103e", "47bb447773efdbe9", "d26444029f3820dd", "7ac34116285c622b", "ceed406159a86e9b", "854d73faa2b3c893",
            "5f670ee291252032", "246d9d4107eec43e", "1f67dcb85642267f", "13160dc3ddc67847", "959b1597df2b1129", "1baca49b769cd04b",
            "daccf602d3375689", "3477b58a4f6f8b0a", "56e416f0223aef2d", "3771b755d028aaef", "e0b22c606d29d3aa", "b080a87c05457320",
            "bd9214ec59070176", "c78e4ed5d3767e0a", "2b1026d56fa66bfa", "796d5b621b8d8f4b", "266bbc88b9ce08df", "3bdfdc458c65032f",
            "f2fbe2d546064e0c", "5adad2d22e3d7547", "9da83327755ed4e1", "2de9ff119516315c", "3998ba6afe37957c", "79c03c9e7bc8ddaa",
            "68dacf81a7f59e8b", "ed04017aa8862e57", "54655f376fb69f68", "e35bf43b515399e5", "eee3f74f7d45d5a9", "c14c078947453218",
            "252fb883d62dac1e", "79b72ddedad393d4", "1e8be691e67a6049", "246d31990379960e", "f2efd6a82c42b2bf", "bf357b83a507a2a7",
            "247e772ba6211212", "bb623404526d4b2b", "8c26756061aa7d50", "d9d07460021761a0", "16362ed9012273f0", "7b077b7b4a422a4b",
            "2dd60498434acf53", "5ca99df821d52340", "b77ac94737c66ac7", "dca864e0f83a64b2", "ecce373a1eaec52d", "68ec1f4f928ef63d",
            "06ac425947814ed2", "b84422e357f646f7", "6d81d820f2da14e1", "c96134db789066e5", "b31537e065c4848b", "736ff05fa523e482",
            "1dff225168b9acdd", "777fc08127237f98", "46311badf7e90d90", "fa1654b311831969", "6341bcdb5c70dc46", "bb5d9311be21f5f0",
            "9fbe232f0feb679f", "a5d1f3f84fa457da", "8faab3bd75644414", "3d41aed3c06d6654", "f24c1fd0d9284b6d", "04e0cc5bbf2155ee",
            "0af37e8a5686e19e", "43b565f1b2775c8b", "f5a9ae07d4f303ba", "b692eefe4dad41a9", "abb58feea782e2f8", "66b2d14bc31f0933",
            "adbf855531fc4a2e", "1971699a83407bcb", "c42907eaf09a70bd", "bb77bba03bdede39", "f7cd3d20092dcd95", "efd6e5746c6674b7",
            "65e45f84d7eb10d5", "9058c1868915a4e1", "c0f7737efa3cace8", "9786afff215c2cf8", "b2d6adf3456ea2cf", "77171aaf3534e6e6",
            "4bcd028ebe662a20", "66e493aa67d2a7a9", "567f0a1c6daa832a", "b148a4c592cc80c6", "3a98d2f7e0ff68fb", "518089dd7489787e",
            "f2e27acaba6d8b5c", "c83a9e6881cebf56", "e490e390f3fd6d95", "bdb78d0d61a826d1", "771f997c781333ca", "59e8496f920cc696",
            "a69d1828f74e8165", "5e97759c9ec81433", "57e40617b06fea67", "e591bf27915ebf75", "87473980cf3898a2", "8e46d488f9bf8109",
            "87dfe62859415155", "4c0db4320906050c", "79703a1650838a60", "0d6da5b47680bc4f", "1fc6d431f6bf8aad", "4a334904c1fa4ff6",
            "52d6f020d9b714a3", "64af201d09a45d52", "9bf80696c1996427", "aab670c61f8d74ec", "19bb8c61f7f52111", "db8b4a9cefa5e373",
            "b657d8cdc23c65e9", "ba729dad3d04029f", "c8e623d4bacc72ad", "4c84cded9a68ec99", "3ef4b6041f729802", "707edabb1b0dca35",
            "ef4d1cdcfa3546e2", "bffe5de048ac7660", "bbf8ac08223d658a", "532112843df2a439", "bd47f28dfe964fe8", "bcc59a1b09e8db68",
            "3edeeb1afcd034c6", "062f6a989c1d0a26", "08c55e917c455ccd", "f85b32ff7e3687e4", "d50b4b77923bfacd", "0929a5e216d7283d",
            "dc5a0fac64a971d0", "3a7ff4415c0d3f61", "0c799f3c3a68db70", "251a5d1a07c679a2", "b3c76155fe2ddae4", "dc77bdf9166f865b",
            "4a6db1433b5e5ace", "6c1bf428b476962a", "415c14535b157220", "7d13c02888ea2adb", "eb0fccb3c9698c94", "647efed05c27bcce",
            "ab706b6581aa7597", "a812b32900dba179", "b07dee073abaf439", "286c575a2df9a3be", "5520fea3227d79f8", "77def3dae05aa9e3",
            "e8883454675bdad6", "7fe1b9047762e833", "544ccf66b85031fc", "e5949eab8f055bbf", "d507ebc1c9e31b31", "42157a0164ab1328",
            "f82f976f331ad30c", "a0862d0806e2a230", "9af8aa00ff41023d", "8c70c2c6e7256135", "b093b9f0e43ad36d", "a656892c0522ed2c",
            "e681dce9ceabafd3", "16b7edbd41b70224", "dd44421662cfd80c", "25ba3c769760907e", "990a4b4a4d4313de", "c3bb4e6f9e5f035f",
            "754b248c610ff1cb", "7c5d0202043fc7cd", "080203ce841f2bc8", "428bb241cfe569ca", "a41fedc6cbf34094", "647433a0ddb883de",
            "49ab6c00c9df5412", "f7bbd337e7121e8b", "eb0b97b4fcb51f12", "93df1c1f13c90957", "60644dd52e865eb3", "fcd99e1dc11b51c0",
            "8e3538e717509d51", "6fba665f322d8e9c", "603249cb788647f6", "514ea0cfd3230693", "82693e2d6b636ba3", "6d5aea384103194c",
            "497617dec3f87e4d", "317eed8969d3529f", "919c0d4b828dcca7", "12156787f275241d", "196f72cccd023751", "efb3e5a5ea8bece5",
            "be14da036ecbaee3", "6d943abeadf0c1a9", "4298a96b3b8ab938", "e614ef3c99cf1c5a", "5b36c8b82310c07e", "6e8d369cc7b7ea7d",
            "bc18154ef46b900a", "01197e5f85c5e659", "3b7e9fbafa8f8161", "0b809e30d3989ec8", "27deb79d92e00b17", "f0b23d5e00e41bc2",
            "339eebc9dfdaa1b2", "b58fa751278dde47", "10ae844a40190dea", "b7c637bcaf8c9cf0", "0689d0c90153c8f8", "0873e46dc4dab78f",
            "649806c622c462ae", "beebf3de3576573d", "40d31303f39db804", "f3db973025d4084e", "a540cb41d3f5d4a0", "4edb5b5748027386",
            "946c30feeb7a090b", "46352f1251f35abf", "6cd3eba169922b3b", "e1abd5f76a7283eb", "4657172c381da956", "ec579dab176a7679",
            "f88faad213e5c4ef", "eca4dc680cc274da", "91e5dbe3429339f8", "106698a88e1d533e", "9b1b83d464d4ad14", "6b57ec4011f959ab",
            "f365259822d1084d", "0bce21414505c09b", "d7bb17875e01c3bf", "d7dd9285ec728a6d", "4c3841b9e73d3ab8", "d248739b0f88490d",
            "7ee805208e55e272", "e48e05c4882be9a2", "158e1cd5680e2015", "a6505fb7ce39ed56", "af0fb06a3baa620f", "3f2d98c7bb54bd43",
            "7eea59d156e7e5a0", "06c6653ee8bd93a2", "67e0518d0b222ddd", "a1f185ba4ea8a178", "16da23689ff94442", "d4a93c30b83ba7f4",
            "f80ba6f627eb35e2", "149194fc58697515", "274487fa392811d5", "e34b92167ff0dd18", "590e80d5e6c981a1", "bf2621939f304928",
            "e6701ca6ae53ddce", "1700af00cbdcdc6a", "bb6edbf62e99640a", "6eebd77c33443ee4", "8520468972ab408d", "b5f284e7ff7924ac",
            "0f7221f45366e257", "ad7072db0e15597a", "ea39655e656f0e6f", "6667ba4c7e856935", "c26872c5eabd6f66", "82bf9b0cc2811d61",
            "4e0fb09f7c2f7a2e", "eedb2f90e52bb1f8", "c2c90ab829cac54b", "61999045c53eae6e", "d4811db14d4daaf9", "ffe092e6c16b6599",
            "7d9eb4fb79d7a9f0", "cd2d72a1eb746914", "64e4561a4fc7dd9f", "de085317ec411512", "96b975b96037ccdd", "59301d6ce06d454b",
            "d659b5a47951f50f", "9ba0e28f01059b5d", "42f8ac5e5691467f", "a69a33a228bd9798", "511cc213e0d1b4de", "a8c296db4ef888f2",
            "f0785a55bb573fdb", "8c11c11aba3acbac", "2c6e4155b5362087", "7e547a2f7ed11fcf", "122b73598a5f2f24", "ccf2e493a5dd72bd",
            "5ce55f01f31a51cb", "eee1ea798ed7af98", "516d037f418d6bc5", "76f59632ae68cf27", "d46e91fd81cab8de", "1030b1f6d1051db2",
            "f1195a5b9b3cc292", "99d81364d7cc6964", "53f5a8d04e6ba9f4", "97dd3f4d38e49ebd", "53a1c4821d154ae3", "513d37909df03016",
            "93900310cc5e7bc4", "dbf320b692930cff", "3aa0691edfed6bd9", "0337d6ac9a145dda", "255e934176afa4e9", "dbfe358ed407be07",
            "e3f8cd817de24f54", "13a34738b0ec44ad", "440bc68977fb4393", "314624caea8d24cf", "e3a5912e4a2173e5", "b392d15417c2b858",
            "82181ac664f1f127", "dd6038bda5d3f9b0", "66222dc528d9255b", "e6fb8aaaabb40f7f", "917638ab41a9f6d5", "9538a91cc78001d3",
            "ae6a754347092107", "3a84e3c6b27eebd4", "783c614bf81d7502", "e86b157a5c5a71a8", "8e64633cedda1987", "2942c30b3a693ffd",
            "6a40608743fa6cba", "541bb28fe89a730d", "7409f66f79adfcae", "79fbae2cab79a0cb", "6e1896e13248f050", "e46f1a6898a77234",
            "6b0eda25cd175310", "0ac37806ae9c7ee1", "9d66cfb8de0a7386", "4c3c76f0fad65edb", "0d46fb68c769d343", "8cf932f3b2c5e20a",
            "82242e6a768f0a28", "d0d76bb9bd189323", "45c3ab1cf11fccd0", "9fdd54a50c2284dd", "19b8d9ed987aed2c", "ae4ad2b94bbb9a0e",
            "81cdffd73bb08618", "025999813c4e80ab", "708094e82d0fb717", "1142c28992c5785a", "8d75187936424975", "eeb13488462d8265",
            "615e9eb5a0ce6112", "84cd33c12faf1ee9", "79522826b82e2357", "57c4e98305187a65", "50adcc54e76f514f", "aecaf91452fbced6",
            "fdf86158c27ea9c5", "1ab574b800ae3f33", "980dcf2dee1115de", "397603eb0b9a70db", "57594e6561e67799", "6f0c8228ae39fec7",
            "ed0759093970efb8", "08cbe6b914526e52", "60ca9b148afb83d3", "6cd2f28464875878", "df345795462be881", "c22ce43f2ce18c4d",
            "aa532a7bdde1fa29", "39e4ffe2df7051ac", "dc04f3374f8a7b29", "9e7f044e5ea4b4c6", "b9bc23ed9923ba10", "ba95a09e3c1179e9",
            "3a7aa307d41fa681", "65c12b9a21a50f8b", "bd6f4ce453778cc8", "75d5e188799a6ed6", "a5968beb4530ae41", "c53faefff1574e25",
            "a7024b689d696867", "ce1edf284e1fb775", "d60f8f8050b4a2b0", "f013b917b50c4490", "3c6b67f899924011", "b99cece497c4c4c8",
            "36725f86d8b3f825", "5eb05d7d7ee4e8b9", "55f6fb6270a03b02", "98abc18034c504e8", "066c614ed0e11a20", "3b69662e3a8fd4f4",
            "88ba5e15ba53ce49", "aa076918af954350", "aa93c682a524a2e3", "77b1aa6d48e9c6a0", "d63c23a087cc8ed6", "e7027f0dddf96b00",
            "f7272aa88e0f7405", "843810914341ad3f", "fbd10bf39c75ea41", "8a7bf5d8a2054199", "1e1bb98be1c23301", "411e8870311e66a2",
            "b6bf322309c04bc9", "bafe6ad25a178b46", "199f790864a89442", "06430133948068ce", "3fd1e549647b36ed", "aac70f6bdede811e",
            "7eb22fb951c2056b", "db82b41d0c25bda2", "323ae60d62828f7b", "715e10d364ced485", "8c1cbdcad5b68609", "03cbe1a358a03b84",
            "ca685c3a5d8b2814", "1c9f2745a04d9df9", "396ab74a57cc83d6", "ca8666283ae70e41", "a2d342bbb2580922", "3eeb662d3ac85555",
            "788fe1ab190b57d4", "bfe13b6c1b157fc6", "09aa20ff9aa6d833", "07bff86fe92896af", "40856f64306193c8", "baadb0380047e568",
            "746dbaddc514419c", "937083ce5df47b14", "1a699000f7125d47", "5caef93d2e9d46ce", "3d52d3823c48cb62", "ad08c019d38f8d0c",
            "a018943da7e9a020", "f05cb6d25b72e99b", "09e86f216403b9ec", "3026e6b6e448e27d", "fa2356d144047740", "b0a18d35c0398732",
            "bc87a633f4d31c7b", "7fe5f0d77d277e3b", "ab91d3bb7a98e653", "421733a6cab4a30c", "0408feaf373d0b10", "f5231d22c410c558",
            "498a6073bd29ed05", "234d155aebd384e9", "d8055dd78664eb78", "62f1d4e0673b11bb", "89ef7de22f1c38a1", "4ddba4382e1234e2",
            "4b721467b917ba79", "36f028709d6e68eb", "83299f6896cc9be8", "d85e8a2a976069d1", "9006bae0ba698615", "923c596a0ba1efff",
            "82830f44aec6d4c0", "3dd7eaf50bfdedf6", "fc12558f67f0100f", "f228a03a786547ae", "299c2721f99574e9", "684768e1100be365",
            "1c8b60f4d323a695", "27fd31a9006128fc", "d6aa725d22d0942b", "bb992bfb3228317f", "348b0de8e53267e8", "a126fb0e7d82aecd",
            "1f126ae9700ad096", "9786bffa8010f799", "0da56769792adc3d", "7d22846c37ca4f89", "36c6c4da1aeed083", "214526c9e78184e2",
            "b30f1f0b4090a5f0", "9105e8979efda7c9", "59d168920a868107", "26cbc036141ee631", "3580f22a71b74c41", "31329d0263bd5143",
            "e5a50e4fe2407129", "bc080ff25a2eade4", "3102d16519d26de7", "8a1096071cf69296", "fdefe6eb83b9ef2f", "713a6dc5f22b1ced",
            "7ce7d2b4e6d4d22d", "7d4d208ef53397e0", "b05b93e675b7ae28", "4bd454ddfc5323ac", "22d5bd786a9ad838", "370cd8428786e7de",
            "716080d859d65249", "644f065b0bebf71f", "030ccb7b69264938", "fdc6a9781376bbdc", "cf4f2704a66db487", "ba83af46e83de38e",
            "bbbd02bdfa922b75", "b9e2d33bd25e741b", "c5353f4dfab46e30", "111b49cff58a78a8", "5e67137befda0cea", "2f77e5ebb978f62f",
            "e781768e194eb19d", "e10796d96000da91", "9dbe8d08918505fe", "f1ee3d9b06be3fd1", "a125002dda3f4274", "24ec25d57524c889",
            "c253934add643e7b", "09e52bbb099d93a4", "8502d6cca9682dfa", "6654c3757aaaf95c", "ad342b1dd6be92ff", "de4f2d8d68e68651",
            "50e82ef16f04c5eb", "27d608b32230764f", "13ee992c849f8dda", "c196473612a51c74", "0fa677cfe3a3d5b1", "a04a4e0fdc39708f",
            "01b614691a0a9cf3", "7a0cc04a27ab9d9f", "d9283fa2b6d1fa25", "9887c897b0a1ca27", "f02e2958c900b5d9", "fd54560b989a6ffe",
            "00e97b033e7ef633", "2a1c7c63ae9308aa", "795234f9d6c08b5d", "8996a200094cabe6", "53baf2110fa81f29", "e9afcc451de58169",
            "556d41d1c3380eab", "6ecd5ad612521363", "1c35194476cce280", "0d2414a0269d267d", "34cc7a3c357a9ffa", "6f1a769df95d90d8",
            "a4f79456c4111edb", "689ca487efaae916", "637d23d91022322e", "c2a4fb081e58021e", "79d678006446fc3c", "91b656622eb91b5f",
            "bb4fbb59be988943", "fe06a365835ce1ed", "ca550bd8863456fc", "1e2ebfd0fadf9e43", "c23cda40720f6748", "2c4d99b94d84cd5a",
            "9073256d8f038021", "4b056823134dcf8e", "f7447e832646a9fd", "eadd5eba3f4cca6b", "26f88433f76157ad", "1504bf42a9314286",
            "6b9a791fbc5684fb", "599adea17ac1690f", "061d3b104bd55f9f", "61e37f2d02404197", "7694b5cb41769bc0", "c886b25b6153f881",
            "6b3d83778f0da628", "78d9d819b1f685ab", "433823b9797cce3a", "38bd35b74927693c", "7fac715128fd38b7", "21959155985642c5",
            "5055bbddb8e1fba5", "89ba2e7fc7445850", "d9f49e0e78b24e9f", "732bfbaf6de31399", "720496ff24fd0a91", "c1ff03ef17ff9790",
            "eea76be17b355c0d", "ea9c4bf627d27b08", "a3499baa8d0e9dba", "65ad4555d8fa1283", "526942d298f432a2", "b3d230f103d219f7",
            "9ede8169389a3407", "8921b225850257b7", "57feb63425524bad", "db9ce633b7c9f1ac", "b427b539a8cbcd5b", "576d9a53ffa3943b",
            "955caa3fae5b8102", "8a3623738942aa29", "e44d5e1d035917c2", "990cdadd33fffe7d", "dd23b106998f428f", "3b0a9609f2351d4e",
            "a4761284ddffb2ac", "25b649130858dcbd", "3a404f0ab1c11d28", "228366df16aed662", "7caceb87d4af0b4b", "5b90e2c3b06a5857",
            "df4d80a35b88ec37", "4dc0a754c007b3f3", "cf2c21dece3680c9", "c5ddeea7346c889d", "9eaa7758387eb16d", "4f9f33bd7430f1ac",
            "1dd0ede728e2261f", "f77756e4f3d50ebb", "79b5a47f7192e751", "eda8422c5fff02be", "d5344bb1066c73da", "fa3c73e5a342cb84",
            "2c202a2f04aef54c", "9b969aedaaf4740d", "96c566948d5a1a51", "6677a9ec0464583b", "b521dab2a0040757", "1abf1ac024420c32",
            "9adef5b8b4fdd801", "94e4b3cc8d40efe5", "3a8449e623534435", "a9b08b83b78add65", "6db53e3870f21078", "f68e124d79dcecca",
            "3bc8fc4d3ad4b3bb", "a51dfa928aab6e7f", "f553e99b31e1cbe4", "151ef5a69ec30f11", "0f1f40303bd2eac7", "3ff71acca658bc69",
            "c6042e928d4fbbc5", "ace604adf48abdb1", "e6e07116fd20c09f", "069c8342b5657897", "49a7e84a0786075c", "568e5b70e61529f1",
            "adb4e5572f8d2a06", "ab132b452ca15f08", "b2e98c9f0924adeb", "80d953f5b9fa0811", "1a294b5afec836d1", "675c026f06d7b271",
            "934248b6e419be1b", "ada412ae79e7e16a", "dc89e23ad093550c", "d498445d32f66491", "a12b9c921a993274", "43a562c9b8b496fe",
            "a49b15d433a55d7b", "6cee621a22d25d31", "ce4b9b747be85493", "ce241cef6c0c2dcc", "4d9dc722bd2ea158", "7308a50955a69241",
            "77ed916319268eab", "ddd2d77b4d4ece7d", "22533e70bc916825", "ddd7a7f2a69fbf49", "5364a7207c3c3b06", "28528a452a814f97",
            "d095515b10b2ea49", "7df99c5cdfc226ab", "9acf026d5e78e37b", "9e5da00e85487283", "3f779696ae0a6eeb", "9811fb16058b31c5",
            "32a6b14bd62df5bb", "d12810d15c7608ff", "f1a4b7a85acb0034", "bfb4dbb2c3a09656", "8470033b7c923f0c", "bb4c81446108ba5b",
            "f92f4a163c0bfaba", "1c88a1074ddbde6e", "f16422f852d931b9", "24c9f2350cac1c58", "c09d850dcbb1f008", "c578cee61094ed9e",
            "973ad24c382ca80a", "9c1312e0eccf818c", "08c737ca053bec48", "a6f0f13facb9eceb", "e50e796202904b5f", "1f37e36fb8ef6a7a",
            "f9f4990d3724f71f", "fa0753a0b680c601", "67308daffd262e8a", "612a4e037e76361a", "73f9012272368a29", "e996b78b8b128a67",
            "a3f9ece0054a5793", "f74d3e01b83932d8", "06f781b6734490f5", "c37ab7cba85dbc3d", "49a75e1845949324", "545a96441cba6671",
            "f5c7b4232552422e", "cefc6d4a7e64865c", "0113a2558ca78a6e", "550d3e12acd03238", "9d6db6445ece552d", "6da9fdcdbf7a6971",
            "bed746c661834271", "cbec55a46dbc0dff", "8351d89fceefbec8", "0eff380c4af5122d", "a118d5c468e75648", "650e856f609c2155",
            "1f99da7b1b94323c", "61c3967137067f6c", "7c09fc92d309a7fc", "28acf936677389ee", "72b1e8ba61a0f5c9", "4e02cdf88a458263",
            "0381c597d25567ac", "09194df0f55a0252", "77d0214ec2c37145", "c7341248f19102a6", "9be5fb3a8c7d1e16", "ba6210a21fee9b75",
            "03be0bad3a19b798", "71dcbe676a9b4f0c", "61fa6740f675171e", "9b28f79c9538e236", "b7b2832a2be511a1", "1e0853df8a8728fc",
            "1b0d4c848f4db38a", "e22e7434edbdfde6", "20d948eabe113e0d", "6e19df5c24a7e626", "6fe1ff299e3f1c5f", "2e00bd68415e1456",
            "1b4e21b0e880d625", "c0b333eb0f2803ab", "4b772432da157618", "223248d2ceafa477", "24d8d3155ac1ad32", "1141d98d098cff85",
            "fa93d2a3ca5fce38", "21816dbfacb98927", "60041040b33e0328", "2c0b903137cd1577", "79f96e535c0fc324", "ab1b2eb3e796b956",
            "174fe1fc4861715e", "0c2d8fc372663b0a", "1c3e849a634590ec", "cf2efac4d427a98a", "a203d0c483745a55", "ba41ecb1087a5cda",
            "b546de7f84f8bbc4", "ad507187b91d5ef6", "5c86de868981c4fd", "e7c9889009b02e22", "c9c1169c7ded820c", "a47c0bd47a684833",
            "c6c45f868e1b3e08", "2f314114cc58308e", "e8569c20ba659468", "295c8d27073c7666", "53defc673abff194", "6fb4e9f4ec5ea3ff",
            "ac7a71903fbc4a67", "dd36949560f7d633", "7989b40f144fd94f", "712f9313cbbd307f", "fd902ab244fc7713", "e960cf8c21c9537e",
            "1d49bc7360fd15b1", "91800b7e08d1533f", "b10570de43320246", "c04333013d21aab7", "d6237fdc73d111dc", "5506973cc6d0a45c",
            "9e6c728a84e72cd5", "d59336aa0df6e594", "60384a5c18824d7c", "166a8ad87e87ef22", "f32b491208929b70", "5090acfc788ee11c",
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
            "73f632ce9dece9f6", "c643387652759c77", "da560815745e8f08", "ad0777ce78e7c413", "2e9ecce9bd38ecd0", "ec7d915de38b9647",
            "5994e072eb411891", "74c11314212b212f", "03294d0cb241efe8", "0b6cb2308e065ea1", "981b7f34a5562220", "18e66cc342a44d24",
            "d6caab64428405dd", "4a424d6f1480b7af", "00f94ea90c16a091", "08e506c48a54179f", "e821f7bf43d7d600", "2a5b5591458abd63",
            "fe058181c09b2334", "7b850ef78c046ee8", "fe091e000b789d0f", "c677784bb569ea75", "97bb779e64b6b69b", "05abac98388d68a8",
            "b3dd2859e4adfa38", "0aed7f0624423fbe", "32354483aa252f16", "059461ef47759f45", "e206e1b4f1aae1ee", "e6ced54009dc624f",
            "03a16ca67b6dfeae", "fc3f1ea6ba3f1317", "99fba378be10cb63", "25e02b4b2233cac2", "0ae18841d39722d0", "59d580882864b67f",
            "aff631072ba0786c", "1d79bbd21abe645a", "2ce64e2637f1dd90", "5050c2b3ceb86320", "a01266ddbf032eb2", "8620afb6cadb77d5",
            "f9c4d3eda5c5a9ca", "6ce696c0c5cd7a30", "3cbdf135090bf5a4", "3e7df379ccc8f910", "a41226e5543d1a1c", "b3bb3ca9b0fb64f1",
            "f75848549a9766eb", "4ad8db56a7c617a3", "eac9be399c03f523", "de558d5626d05249", "16ce736bcb3db34e", "6a45e5454a9a7070",
            "505aaf76c33c66ab", "7074259ca5818cbd", "e82c6a934ac426f8", "efc7197a4d551a2f", "14c8284cb1caf272", "8601a6be4efe9e19",
            "af170a271e2b7b86", "843c454fe15af5dc", "ecaaa84f2ca5c101", "077af02ecdfcb3eb", "2af2f6f6bd39ffb1", "7aa6c31c75305b4d",
            "5f90875b7a349b6d", "314e0b6676f6d707", "69f72193a2a93d62", "dc9be0194003bd2b", "6eabb0da33302119", "f8479bbf16fd6f98",
            "15df67718ab0b915", "2d0accc170c68efb", "447d2663afa673df", "b1be953fc4daf770", "78f13f3e11ea4f64", "27ec86e5bf008081",
            "722dd1c220a90efd", "2de4e0cef0617840", "56f96db524096e66", "3dc3c06b818169f3", "4d66f68f96a9cf27", "b7103a2eb0305a5b",
            "6ac337d0bad17acf", "ca259d90159c102d", "1dbad587b1f2b10b", "f7403637235dd3b3", "da933d2a522de38b", "e5e5c9ae37f8b1c6",
            "835a6f347077970c", "fb313bee1e51697f", "3d17258d63ce83f8", "a7b27074c9c580ca", "3e9d39ef0816d81c", "876233d0c355be47",
            "93aabaea7fbb3e5b", "aa2f0274a5f0cd29", "6b9f22b98290dfa6", "2186b6e9995c5640", "999b26a3ee095b2e", "dcd300f6b15df464",
            "c0425d58cd89336b", "1a597f42b43028dc", "4c7fe234ddf2497b", "a2153d2b7fb28689", "5faa2c6d997c085f", "ade74cc9e1c8e47f",
            "352cedde2176499b", "4e132b0e80cfd327", "50c8c578311a097b", "dcf671ab56b26d51", "a35880d6f9ef7af2", "71ed5c78cc859249",
            "78aeab17aaf643e7", "e285728c03525c60", "ff82f711caf3e1c2", "ded4c32f1fc7248c", "4a561506750df484", "aeb366b6076d50d2",
            "6670b30b4f295821", "d31af853075cb634", "746b3bdd92b3cced", "5a752760f9050767", "a5f215906774613b", "75cea13d11c0b8d5",
            "a9fd5599aa15eca6", "f2c35edb48817713", "2c3d41a834f42831", "4de19b213f7d0f13", "5748243f72321e85", "5889eabf684f0d46",
            "a3193f8a34221cec", "4d53268e4e446ad4", "ec19f659f6d7bb92", "f5340e07aae53e23", "c72f0a92dda58b89", "77b1b8f119ee6589",
            "6ab013d83cf45203", "2d147435af45c962", "45946ae463b23e02", "08c57ca6a4294e14", "694b7058eeee10bb", "6a4548d06fee6ce0",
            "d8190771ebc924e7", "03a0c1d7a96b9cb0", "9b214d9016ff279d", "66d83b2790fb2491", "1f723dd3e7e608ff", "5112c96e54878aad",
            "e8260753a4248790", "211970f645c4ac84", "bb81824a2b4a24b9", "89d69bb56c3ad4c9", "f4f04d5037e5e190", "6985cd36ab2ae280",
            "6a93a798dd933ce8", "fa25246a18a02f38", "bf88360ced8cac97", "369de68320e5d7a3", "9a9be0bc8735c4a4", "072ac9d921abb517",
            "281923ef4361fc17", "f86899e8a0f3b4eb", "26cd98c5a11e5dc7", "38755012eeea30cf", "56162ee59c9a773d", "371a11d821a41c97",
            "ae622573ea4a7e8b", "efc38d5e0eccec81", "ce6a611416585bba", "5a4572cc735a35cf", "87b669ad9a61d295", "7f2bfc1ca75ea538",
            "3b4f7bdfcbb94810", "8b7a02fb9c65b679", "35e1dc537b99d235", "df5b91bbaa7ab48f", "01430f599fa51863", "0e082fc683ecdc7f",
            "4ca2369a25391496", "2e8dbf34c0ce23d6", "b2466b753827bd62", "b9e9308363e67019", "c060682c631e9e89", "d3134185a2f3d4aa",
            "5451db6f2deb392a", "d640ec3ccb15317e", "8bc09f554824e93a", "483fb3556981b026", "77561a8b54f3ea0c", "1786fac5e994993b",
            "a5063041767e5d2e", "cc367fc143cb17e7", "404422046c3657b8", "4584ba9cd3d296b4", "ff422117784b8f8e", "167aaa3aa695d042",
            "7585e64032cf76cc", "78f7409b55a01eb8", "fb965dae5e7e3d89", "977b1cbfa94bb808", "d3ff3e9bd725a3ab", "bf657c77c5e61b3c",
            "4a768b933671cacc", "ce5b294bde1bae35", "80e247e4ac02c062", "5eaa438ceaa7ad25", "1fb3b5dddb6d1ba8", "54ea9f992f443df6",
            "c8a00401ba82ba8c", "26c3ba17f86f4832", "04b6e994cf5f12cd", "0fab19abd85cc44e", "3146911a751107d4", "a066bf7fcdc196f1",
            "53b37320b4b8d9ca", "4412dcc7438336aa", "2c6e7fc9882b45a3", "e65c44d19351a5ba", "d022901f28936960", "ee74f95e22b3fb0c",
            "9046ed81f5b8aa02", "affa554ded9645bd", "2e175ec661aa154f", "a59cb9a514e96f1f", "9d5d93b99bbccd38", "e1412db56ae8d1a0",
            "6eb82d96707c50d2", "a336bf8c83030ca7", "69986f35f70f18f1", "abf03ebcee0a55e8", "a446db32b6c2d3b2", "56f630c7e3f5e826",
            "0312798e1a7b1568", "c6dfcd4d8c571d44", "2b6f0de110f2cc2e", "4fa42167a5c40a29", "732e249fdc96f59b", "0e7d6857f9a76a06",
            "a3fa227fd862ec91", "2d5cc076020c73d8", "4f09acd934f952d6", "ecf8fc471218da92", "4321ca9fd1df5354", "69ed288226202625",
            "4a2ee8a67894f750", "3241f38bf09aa5cf", "01c9f251dcfa5576", "42c41edb731b0c61", "30f03fdb291efc67", "96cf709b3b0ebbc9",
            "e7fb01e21e8ea29c", "f31e56f7cad885de", "91ceef78a4958019", "b962168b2efb895c", "a334d95ea1bdffe2", "1f6dcab6b12aa81f",
            "0475bf781c7b82a3", "929c238b9cef4028", "e1c53a9dc515d964", "0a2d18e324ae25a6", "c0b3531a22720955", "bdd1d6064ec05033",
            "8a1a32ad80ce24dc", "71edb49fbd364e41", "cbb1b88beccd47b8", "60fd4f0c5a5ae629", "88e91445ff4a5c58", "ab8e7045cf2d2c1b",
            "26b729cfba252f84", "9ed331fe796703b0", "1644d6f31ade8f24", "4e7f374caaeece1f", "d6efdb12111bdcdc", "73ccd45dae73754c",
            "fe51afa3edb2aaa6", "6c0079962738c476", "395e171a73e9beaa", "e6dbf47e6f713546", "5ff48894c6273f74", "05a1cc853cd2049e",
            "b5e5c3000756ac07", "29aaa312d4700436", "a566a7b141e7faef", "9fc984ca2689ae2a", "0777ef5cd97680a0", "d6b50a131198cde0",
            "e9001defb87a192c", "da89b958abad8001", "2b43c53a0759f44a", "c30c81a4502d5b6c", "7558d33e42beb7bd", "25b72d4e01082364",
            "6ec2055986b392c3", "7d3a99338536fc3f", "1692a6c437619b79", "4cda5bee559231c6", "d77910f18db4a474", "5721e8122670f343",
            "86ddc684db2379be", "aa63a0a4c2ee3887", "d93c5d8fbb923f98", "424600628b6ec676", "784cf070251ce60c", "2a4c2e7d16700447",
            "ecfc2dcd0a426938", "ad80ac785db81f78", "64c1cbe17c47b967", "3fb6ee1e0f1c4900", "10d8ba340e3403cb", "df45c78ce41c949a",
            "46dab552e2e7acaa", "3534e4ff78bcbdc0", "3659e7294b7551e0", "be798fded2218ae8", "2541b2dc1faf1d25", "20d5c417abaa3c47",
            "2b6873debddfaabe", "9907c2d8cef8e060", "a490d62c301aa426", "774140d1e847e491", "fc2df1b09b4600a1", "3095f34911c5eb96",
            "c2cea9b08dd5f01a", "ef6714bbc6d4a655", "5472d1050f0879e5", "952a709bd5f829e8", "1095d332ae02479a", "667a6296e6ac309a",
            "1329ef1cc1c637da", "caf9602e4d4b1745", "cb94c8849b8a9507", "9331cca1e49ffa4e", "a0ef09623286cef3", "53a99971046c292a",
            "240bbf874884ad92", "d0e30f81af349083", "8f89322b11b3d0ff", "5cbfa21f9971462e", "7772f2b29e384f58", "e1d4b673892d6620",
            "30606cb551fc84c2", "1017fa473ec80fbc", "42c25a090ca2e3f9", "5138692e0c967722", "ef7fcfa7da34fced", "4d83b9865606521d",
            "1d57adfc70816c38", "3f6d28976078f2a9", "4b324b122cea0a67", "86c0c9de665b5f6b", "4dd6effafa3758f5", "bfb7bcccc9c36e63",
            "7263eaeb0fa4e113", "7e4162d501783d45", "9fe3d71449d4cff7", "97e939153eb417ea", "0549569e32b1862c", "9a70227d8c36774d",
            "fe5d093cf650bf23", "f04385ee80e361e9", "abd9ecc8d5d0f688", "3d579579929537e0", "c8f1c084ed5f6c50", "0159f0237085738b",
            "7a62b183236d7736", "8eb689bc69441fd5", "e0d05aaef39b463a", "81df02b6c5a087fd", "bffc3ca2a9b81e5f", "af5d562be0e1d0a0",
            "efcf62f8dd11bbe8", "6da0c9d7603bb78f", "4253db59c1f17c5d", "6ff147e8eabb017c", "c95a78b8fb93e842", "de6d1f34be523850",
            "438db4ad5e59c4e0", "4e7835906ce2a682", "998250589bd2f74d", "5862fc4b9a611b33", "8d3435de49861df6", "46843b76cf8fbfee",
            "eca7096fb896878c", "2fe67a3ee8066445", "fd3f73f898b993be", "d69f0cb2f5eed2ae", "30e83f12c52cad42", "a992897ec2f334b2",
            "b227ed22b2d68224", "a8853346f5c24e1c", "c8d829be889f7e43", "a29b0a2fbaab17ef", "8b6e41a3ee104a16", "578740400146a1fc",
            "e88f2c07f04c8c4b", "c27d12cef857c471", "804219cad3209926", "16cb3c8064bc4d0a", "87f0b6c024d3eb51", "c410da7026dd3b69",
            "b721cffa64b05c50", "99b1595ef9042c1f", "2f49afe1793bb702", "7374bb3744459073", "95b0f127acc30bb3", "28fb46f045efaa71",
            "8671c2ee14c68bb5", "054bdf2c4ed4104f", "3cb76738616394e4", "27eb1da82ce47149", "302770728544366a", "b089250784dc0efa",
            "4caa9bc12763aed1", "a4ce5b35e222728f", "fdb6b4247cf16c3c", "78a64ee7b34d07fa", "ecabcb6191f3411f", "9b7160bb22f24228",
            "7d5ea4c25928bc23", "e13c7ff99747c390", "e4c9c0f123485470", "57afaede6a173c04", "1c8a69ec2481fac5", "0fc22568fa749951",
            "87b41a1f1943d4f0", "c9b08fa24b152c5b", "fe80b8752b2bec59", "aab3df111a765c9f", "549354edb2b673a9", "8ec7ca6a1e498478",
            "09ba0f5512ed0fc6", "ebc2fc51b732bb64", "feeda5407ec4f435", "db53b8db93e455ba", "5a258f68f3288d90", "2404a167f677ef07",
            "5e8a02e2586c2c11", "a9f482cfe8349e69", "5c0b8dda06a270e7", "aafb8629d1c1ba84", "1823f5bf3b1f572d", "69b7ee346d5eb9aa",
            "16373af656e58dff", "7d6ac54d5ca8f853", "334b0feefd234bae", "7289ae30bb59b98c", "d1b845aea9cafc34", "9ec92107acc805e7",
            "ec146dcf9a835a99", "9416d789a60092ab", "31a7d664f8fc918c", "4d92a2438e4c0b36", "b2b3f340f5939426", "7899b93a00917b7b",
            "3c66f48b985940b4", "f8c8e6ff3402527c", "1ffc1cb2211d8e54", "52fe81dabd11f007", "83e9d6a97594e416", "0e82ee4ee0b3cc75",
            "75dbbdd50b782ac6", "3f0d0618e0b6cd8d", "87e935d1f84d0365", "4349cfb569df9a48", "0a4a7f058f194ab8", "dae3fa0e7598e89a",
            "188d574ed4a8871a", "154c74b0fc76244e", "69cbe64724a3c654", "6d941056dec8f459", "865fd67f9a27831d", "256db0564a80a1b1",
            "dcfbc460b475adce", "9acb87ad3de500e4", "f880f7fdd435e1f3", "753a103fe148f09f", "36c6845f6af1125d", "9fb0673e69ed5bf6",
            "49a9d816dc36615b", "c734f93af6bcd250", "2da1cbd5641f2f6d", "f2b99f16b5aaeb58", "9d99f07236887676", "55b399ca7508012c",
            "f48030c44310111c", "1d5576469f7819bc", "9e6fc4adf0f40218", "1720d904e8d3138e", "d846a69a2d658202", "b0c9f2126a96fec6",
            "706904a0cc5b9be0", "663a3b134293b77a", "ce4f55221b2baee3", "70a2b7cfb14ff5c7", "2aa4419071242da1", "742106300b037529",
            "e83f46048192a530", "1ea174b31c5f482d", "9243b74ffd2f2293", "2a8768e5d19f696d", "f70d92b5bcfe2670", "98f8f175ab56c3b8",
            "3fdfae91f8c9c1d0", "21a7ae0e8b0d2665", "5b992b3ae7673af4", "e48ae5f121d6f306", "297b54818ba43818", "2748ba77b36f71b6",
            "c7640fc36f51ef06", "d490884a4c247f56", "9c0f355423378c1e", "6379deb14afae5cd", "5ce0560fe7ade1d4", "84ff130863b9385c",
            "443cc37c160b862c", "3e765ccc401bfab1", "cad2f0578f392a52", "44cb93f76ae912c1", "db33972064bb017a", "12ccdeb323076221",
            "ac95be8159c350ce", "00a29fdb4ed969de", "e3c454717ef1cb03", "3da2b516d5e4b4ef", "e2d5692cc6386868", "6882c2f86873fff8",
            "da00c74b641ef2c5", "6f9c8237804c4672", "1d45071891846f7a", "1eb4e4aa759e08cf", "9c89ecf8d87e298e", "8d8701296ec446b9",
            "390ff11417f14f36", "2fda18f315861a6a", "655d05b3811c3fa2", "68cd36220ca5cea2", "620efbf8463191b0", "36224ef9eb291afd",
            "b66c7a77b3df43b4", "2e587dbd5caaefe7", "6a2c04810b7dcc69", "76ff50e411badf7a", "7aac3f892b172928", "8a6bbdcb3382a5b7",
            "74c47dd7308c044a", "f5028acd670c33ab", "20d18f32fc55bc0a", "6700a28c72a0984d", "9438bf58cd1f0521", "43fc103853c2a1ed",
            "19e672b45f7d2ad8", "25f6fd2305795360", "a4babe8d9b6c27d4", "af0b88599bb4904c", "137859f0b36b998d", "4110aaab1b2a4aa8",
            "b2566491f6be4077", "dec601ca52513dac", "b0e77cbd779e973e", "25d46117813993c7", "06d54f3e316c5a7a", "cb5614ad99f5758a",
            "34754f29cd76abeb", "0c31512d1b49a4fd", "acfd8a72dbb08cfd", "671834040624fa8a", "456b317d29af0666", "34d6d8000b318539",
            "c27caf48808a9bcf", "8c6c39044af5cf45", "ae8621509448b0f7", "b669a3b66fc3cc95", "fbebf6f24c402a78", "fbafd9dc745f8f69",
            "abe52a191fdfb1d4", "434129885ff401cc", "4a728c9e3369e14f", "95b24b7f21119634", "440c9eae79c7f4d1", "29fa2e6661875172",
            "34e071164a653d8e", "de15cb7f03739960", "4cb6eb2b9b03d305", "613c723478f16685", "9c331fdff9f496c0", "1cd0dc8f6aa905d7",
            "fd9030dcfa0ed098", "e529a63f654d4fae", "c2caa0caf1e9a8eb", "40c3ce190e74f866", "7f335631d8dda74e", "e9d91c320a4b9622",
            "97471594ef153ea6", "a1bbaad2e9bb0280", "c684f4ad10313a1d", "47010b1709ff6cc4", "8e961462016c4996", "b9503416b33d1290",
            "106e68bcec8f5668", "53c1bacb9edc4d4c", "8eb3e506c3d2104a", "d309831bc45bcb97", "d92dff09494e0571", "ece182b27b29a84a",
            "bc48ddf9e7f6359c", "fbbd199f1e4beb44", "d2d7a1866b60bdaa", "92b0f97f0b0e3485", "f84a0908d59349e6", "bc872b46df50a208",
            "443eadfa0ec50957", "d142cfbdff6ae282", "dd21409aff0a8b26", "8165939eb25dd97c", "2730d6d5c8c202d3", "5a5a4fa513117fba",
            "d1578cd3067325e5", "2843e06b37660d28", "6105453b8574cb66", "bcba27a1b118d89a", "ad7e1bc2d7bee805", "a6368267c251379e",
            "82e16a52b26d3d65", "98f3a9d94238c9ab", "f108047a51d4d267", "3d455f5a2af64572", "528ba1089ef2770c", "f2d11fa447722f73",
            "7357700b48354684", "ef95d256487eda2a", "8769174540049a0c", "571cd002dbce972d", "f13c8a3ed4b5e935", "876e81bdbdbc6127",
            "e00e378e328474d9", "54c7ce845eaeff19", "0621c3c71562eade", "0f426117331f7294", "f5bb542a3c13d527", "c942284db39af422",
            "931fec0f09066fb4", "89d28c121e6b8dec", "616b35abed4379bf", "cda8f82c6e0df4b4", "d101503299fcc739", "d3d5fcae6aa6fa91",
            "beeec0db9cc5c731", "4017f7f21ee7802a", "cdf8157d17888c8f", "3d5be73f75280e74", "c8b7417fbbcabc2a", "99269e99c165cb93",
            "cbbcf1dbf76666be", "bda82656806f39f5", "0a031033e2ad0a72", "3a3ddcd9fa5d9895", "fd7db0213f5a3409", "58b673b69ae9b300",
            "476fdc0d5901ba67", "4bba47086f07fc1e", "e65b9b22f3556c6c", "3bf3ac45cd5b2ea4", "72815ffe7b890d98", "cfbed863089f5c67",
            "420b777801d2766d", "d7c9476605e3891e", "e58d987450c2ba10", "8539adf30fad526f", "3dcf92595093298b", "109bfe060ee74252",
            "f6722544483fedfc", "021e4bf709687a36", "f08918d4c3d85336", "1568652c380f0010", "278ab679537bc36a", "1e09f992b786a90c",
            "c6214a0a196434ff", "f33862b9f5f2930e", "28c4437bb1e45f30", "dd5a240a1b7b25c7", "e37da0476da488bf", "8574291891e04e4e",
            "7673a6cb62307687", "68fcbd1ac3dfa866", "1213e2714d1e6a71", "2cf7538b4f88cf42", "0f717c9cd6e35a65", "9b8ffcecf2cbecde",
            "672ffd8f22b257b9", "bc64a18f0cb85095", "10237bc5779f521e", "1645cf94e5afff85", "bf26a4cf03b2808e", "432b6d0cd5d43c83",
            "9301bd95541764b6", "4179bd1a30987f55", "9c6c46f658fcb894", "456587d20877343d", "f52d3c4f69234d5b", "30f8badae2359003",
            "de6b87235377dfb2", "cf1644bf9bc27291", "4b8655d93726e61e", "0684bd79e1cea3b2", "cd1375383fd820cf", "d64593ad3f70eae8",
            "fe59c138acd27578", "4d121ddd091a6245", "55b14deaa3453b9b", "bcdfe6d8e780f8db", "b2c2a6ee7d6a070f", "8d8348e2cc80efd0",
            "6dcba5a3640e2dc8", "f70263ccace8acf9", "2f7475d1898a1ed6", "6ca3c66830634bf0", "8bfd12e7e229d9e4", "72c7361ebe629187",
            "9abec21268fc373f", "33c704aa8fc4b29e", "614d46ee8313d1f2", "bc64fa1c80817654", "418de7e209977756", "5404c93fc9d790ef",
            "38dc42d6ff484ea7", "ec6386a5bd62fef5", "9fb13e79c477542c", "5d88786b5aa4d92f", "a1a1ea17a2d27111", "a1bbec6a1a08eeb7",
            "fd3bb9151fe26c71", "51e0493547be7783", "334247b7d876e6cc", "e4b0ad1e19db729c", "d27278bbc53f9dda", "bb9d0251b98e45a9",
            "7cfaacbadc9811b7", "d704fdcc3ea7b302", "3ea0a3f4650bf68b", "37508622861855ab", "a78c82c2df6f9b7c", "ea7749a4b6b1d82d",
            "eaa3dbd09d3f5453", "47acfefcc3bf1c59", "cdc5cab0fd1606af", "9dbc5cf81295c410", "df831133a1f3dacb", "e532559c07015bde",
            "731db09fc2b6ea82", "f199e62a2de4ab9c", "34f99361b224110b", "6cbbfdcb673c8aa4", "31388b7b9ae8c4d2", "5c4404ca3aff5896",
            "0d179c37667d55e6", "7612f82551a16e3a", "522781a2f65ce968", "d86d4add82e97031", "3b68a3784e59b085", "67a334b7fffe4890",
            "832a444eaa3c8afd", "9acaa781d118c525", "b9af5697c9403fad", "484f876c98fb9178", "6617092f2ea41233", "580bedec3ffe69a7",
            "d28c7afebe3f2584", "a114f7f88df89751", "07367f0a9e6f6840", "5f1d54d0563a4aa1", "934d730947cfaac3", "92022dec11e945ab",
            "af4fdd4bcbaa253e", "1b07cbc203435c19", "59d0ec50b67a5e4d", "1cce1a19579d351c", "ff5527af66f44324", "15c4b741962b08f5",
            "a28456dc3528068d", "11b23adc7a067e14", "6eac5b844b007972", "50dcac522d2f253c", "e83b43e259c7e295", "33b5bafb9ebfcc99",
            "4f34a1dcab8292e7", "455a29065577684c", "34719e13cd390fa1", "32979f2fb0c8d053", "7600cd4642101ab9", "62ae6e2f77615dcb",
            "4c4b9d8245a9659c", "dd62d9f462bd2f6d", "24921e5cc8997231", "a078d9098ce5f93e", "cc5e45059f989a4f", "6629c851712c5b61",
            "2d1563d01f7ba78e", "c3c5ee4611a28c16", "09e6695c4345c089", "36b846a2be07e1e6", "4ee29ea2d5041fac", "2e359e3b7bf1aa1b",
            "0a5a14f4f06ed863", "676ddf7775dbd69f", "e15878885c60f4a6", "1aed8128886ec886", "f0e82df84ecd8100", "59bcb3b91ef6b6e4",
            "9b75017f513b55d4", "5be89dc0b4545705", "a7bedda86f39d3d3", "ffba89131bed9180", "697d59bb64c04fb2", "b6925972dab88be3",
            "fab8917ba6a98833", "9b48ad14907380d2", "2eef6714f345fec7", "5ade12e27afc54b5", "28acd345d79d6336", "9c85a70ca7e73dae",
            "c6a35cae98e707b2", "2e6ec5a56c69bbbb", "eb1a0d8af1991d34", "2e2ebc3ee13f72f5", "11eef797b5a3b8f1", "bdc89bfdc234d361",
            "5bcffd8aea74e67d", "b9ffe1538861f907", "30a76ed23c547b2f", "66231cea3b0ecad3", "4598907bedf960a4", "6da8fc18ba1f3365",
            "dd177d39eead4ce8", "df9fa970dc350a2e", "fb34aa8c1bb42c2b", "c82cb86c2b3cfc02", "203aee223e2aaa1e", "78f523ac95252de6",
            "0499f5e0707fbc89", "8b91f18f014d8aaf", "0044df31f71727e6", "499dc6fa4cb1c05d", "e36288a910e9ef01", "f8d3d1d184d7685a",
            "0e6498687eeb616b", "f0358b33fbc7b314", "39e2199d5b1b50d7", "47ef1e0993ef993b", "291e91801847cc3e", "6a93f8c6b8b052d3",
            "06857ca22d428b65", "6a542bf07a7ef051", "6e76beb774206829", "5c338b52863fc60b", "211eb072b183847a", "144a3093ff3ba618",
            "9d64017fb3a28904", "57c039927ce6c7f0", "6dcb898ceb74bcc8", "8f11735fbe2ade33", "9e41b4e8bc2cbe95", "31a9c6f6d7c891fd",
            "5ddfd9af79df32f7", "88e77e36e3e852de", "694f1fb5122724c7", "24ddab2d2a4c5c23", "6239621e82dfac56", "d6d22d1a1e2de18e",
            "611e11ad35ab38e6", "07c186b6d9350dcf", "af1164599a027843", "b606869ae639cd68", "71a78791f2101cd7", "1030f21edf364ac7",
            "c962663e7093e92d", "75260ca4731dae16", "2fc22e7029f0d971", "248d3ba609e06014", "d36bee9997cb1090", "9816e103f3552334",
            "995ebcf25988600b", "cb15d6db18ae09fe", "0c68df5816aad981", "f21e2d0f76559937", "4e7aab5e4b49d9d1", "21a643e465c296bb",
            "7e4556dcdbb00e81", "da91ebc3b12cce00", "971477259ad3af8f", "d1a1a55a6ca8418f", "04576ebcb68ea783", "044d66e4b81dec5a",
            "136f02eebc6ac2c6", "78c73d65443de1ba", "f2ecffbc09c8ccdf", "7c7878e590e48909", "c9ba82704e03e9c8", "2beee2a998d329dc",
            "7cd0dc43e01880e0", "04f9419fc66c657c", "e107cc4ef8bd8f08", "c6d43a522519f56b", "3f520bcd872ef29b", "4feeaab119a6f69d",
            "54dec8cb13288929", "159e9bf314dd5631", "8a8cdf0f93b4d878", "89cc7098c4b0d284", "6934e02f186e49b9", "250a0a7c8aeeac9d",
            "b163222a5a7f2bfc", "8a7ee45472740f4f", "db563c51ab2d90a0", "6905054f7b7a853c", "9ec2bcc156b8faec", "7037d18b47cb0bcb",
            "4851aabf2e8956fd", "df1f7266be6a7cf8", "b948606461eac13e", "10134ecec6f4a62b", "3b0042ecee31b545", "7d28f604304dfed3",
            "1820a27c41b09c02", "2375d942e08e87ea", "d4f34e76891b79d9", "69aa54a193ae5a20", "76ef964205219ab2", "e0fe51b29b37526e",
            "7633500eaca2ec86", "0fcc4f180bd9ed38", "9e9ea361bd958eb7", "b6925e4483609b38", "eae5923c0ed60d40", "f3f3f9b9161444e4",
            "5ec6c8f3ee52c60f", "24f22fd1d0d40e3a", "d004a0b84b02a5d5", "fbd8d8f66e058652", "acf83a8dab86b6f5", "656216a07a17e10f",
            "4d976ea926dc0561", "ad7f43cd783a8fff", "d708b84e69a1883b", "7a98df5ec8f94436", "a48a76272ee6482a", "13390485b353503a",
            "1dabd012ee62e044", "814a683b943a775b", "7ada5f8961c91183", "e2776e7f2b64aeb6", "705a345ede786675", "15a13b7fe0f440c9",
            "2c6b5fdfa4665fca", "3efc04cfb12b1a86", "228c2eb70ce06d09", "84cea1684f198521", "1621f6a19e928d25", "446c0d142dbc9049",
            "d5e7ed5a7fececc3", "d60ba15e40dcc486", "0c2f38105e68fcdc", "e685564d1ec59b27", "fa00afae8abd7a54", "e804678c2558bdeb",
            "c6d19b37f723a81b", "ccd431ac8c27ca8c", "a59e63a97a44c4a4", "3a1ddd9b9c1150d7", "b3c5bcbe7810f434", "47aca4195a0162b0",
            "62feefd74b332cbb", "4167240aa3d3c63f", "6958ac41ead3827f", "5ef659e260423dc5", "1827ea47244dd221", "f82e7ac8b14ef223",
            "f56e26ba0f9adf7f", "4bda381c523ad517", "ba0e12b0e451d3c5", "8e9f464462a04da1", "b80dcf52dad090ef", "092c8e0d9b42e28c",
            "c7883cf897e1cd5f", "7a5fad32e860f78c", "c1080bd89eee9fee", "0fe3022448d2bc1a", "fb1d51c55a0f4dc0", "e929509e30af6af8",
            "cc950c045653aed6", "e33922d53b28e256", "176c97a26945c0cd", "5943ffad5e2b0515", "62a3eaa61627e4ad", "aba6ac0253bd5adb",
            "9151df15130b21cf", "95b2de9385c8a5fc", "ee0358585ea6a28b", "66ee04862441c10b", "bf16bebe2cbf555e", "65b7e53777eb266f",
            "1d84c736732eea15", "c349b677c8ef9bd1", "759975ae16d895e8", "a98c9ca2e3204b71", "0a1945aea9ae4aa4", "2a65a575d50a331b",
            "b1f294184c9173d3", "fbcadbda4b797268", "6d7fb4099cb9ef27", "02f746a47d908ae2", "f441131ac5a9276f", "ab09eb5aa4bb3a11",
            "b884832cb8f163fb", "4b366d33c3c1cadf", "80ba06e9ed40ee81", "f951f6ed2f69afc5", "4d0742a55679278b", "351feda92b3c38cf",
            "d41cdb12baab8305", "db607720964b56c7", "6aaec210c98eeb4a", "3d1f3604b28367b1", "619c451638d80466", "3f03585bd0bfb47c",
            "c07156bfc297af0f", "5cad9e942ec3d583", "6c477ecd963f39ad", "68589cb5a921821d", "ca5ef89a40d4a959", "027f3ebfb19fde1d",
            "9feaf726d9288a82", "23de356e2a29d533", "18ce85b1a141707c", "43ce77cf1c70df2f", "77fa140e570ec99b", "e1ba3d536ffbee7a",
            "d2e7edac505ed88f", "b98ef632554340b4", "b88488a80ba032fb", "4ed27ce9b604cf18", "58d6bf1cffac9564", "595bace336ecab66",
            "216ab6f303f15a35", "5525ddd3e57f013d", "dac524b9931e69ea", "9c91cbe2ba7b7a8c", "912eaff66f5aacea", "35619a369bd4b562",
            "ede12e0ec20d4a50", "9869ec9648f4778a", "2f9dda69b8301367", "488c97468ce9dd09", "a38f7b657999740b", "6f0b56195b9d8fa5",
            "9b65f017161f8e8c", "3892c48832baea7a", "097b2f60488b0a49", "d4e009226e3f9816", "91dfd72a53fa763c", "1ab98cf261bf46fc",
            "eb86e6020a79b3e1", "a9a7ea01b975dba9", "5a7583a157a96f70", "c493623e5cb8841a", "aa4949ceba53623f", "87f8fcb83a620ffb",
            "71e7480121bfecd3", "b852573fce751a46", "c6e0f1e67b39836f", "ede611c5c895f5e3", "9ad74d11ff5ef2cf", "d42a6bab6631e045",
            "ca3dfa526f32ad41", "b2e0049b8693dc07", "d1c7adcd8c42257f", "dad03387f42a8f51", "fe98638250a96b86", "651fa19bad475532",
            "bfe7a08a4baf6122", "21e4a1ccbd28f8e8", "283bf8702857b2c0", "62ee9b748c7a7204", "7db480536a0ab9ff", "491815fd69f6cc52",
            "b2ac1ae66cf9b8dd", "6a1ba060e10f8c49", "43212f4460e0f512", "8869ace3a31a77e2", "d00ee9ff847cb808", "29d66ecbc64e8dcc",
            "67726174b96a2a49", "5add0185c54bcfe9", "bb6295bf19e36dc7", "b37ef7266f268665", "9f2f769e473883d6", "2e256165610989b2",
            "2ff9c21e0a0efbd4", "ca3f464265cfc969", "be40aa99472b3984", "143ab283ee2a68de", "9382966c0a899a4f", "af63c95e37aee2dd",
            "23ba67a0e4fadd3c", "134528f78af2bd56", "b2673df4aac6a8c3", "058a6e7d888d33c4", "d10f9090348a5b63", "36173379b2a3c253",
            "9843e3f74730ef3d", "d0afcd8add435b0e", "856a91a438bbf31f", "c1aad8440405dce3", "50bbd5bd6b71ceac", "1c4419a07d409e45",
            "1195c2184a3f3690", "1697599c62bd13cf", "467347a03a0ace43", "f984900ed21716c7", "84aff0f44aeedf0d", "15a9880c3e5f9ab2",
            "785f393385103d25", "c7efdc1b192a92a5", "9193323018667a33", "39d402eb94dd400a", "dcae74a79a0cf872", "3ec021dc99bb76b6",
            "0317c300a94ef4ca", "0621b14be7565903", "4044fbd46461caf1", "d9823b57e11ad691", "f1d20fb70b5e339c", "bb5ed85fc47cf07c",
            "b631d6baaf5ffb43", "c7652a9a67d196f3", "2bf8d3816a9fd18e", "3570b1a12f5c4a24", "6d1a6b2537c8d354", "471388947a88bb83",
            "bd12c2a1a0757cb3", "30848d9eb49519d5", "e528112e782e6f29", "f2ae616c571091ad", "409fb568a214f15c", "98de251177368c4b",
            "cb4f08f78ec031ae", "8af362bd8e8dc532", "28d9cd20036ddc19", "f1eb04cb19ddc832", "b1dc28080b1ba8d8", "929625541d912184",
            "92b6396c5324fccf", "92481d9ea3ce6398", "9f9dd675354392a3", "7f6d86e2a680fac1", "9b6ba7feb7f51fb1", "b550cb2783f96002",
            "cbf46a116aff623a", "9b033f0b1d620025", "8486bb7321e921df", "b5dbd5c3c54010c3", "cc11ce797484b2c1", "fb02d7b593e3ffdd",
            "374f19fb5876688d", "d6b7a273161a80b4", "37cb8f48ae9a93a9", "7418daefd8f6187c", "5940488a9d7bfa87", "f633e4298e849be1",
            "bbc67a36676f40a4", "814519ba48490fb1", "2e5218f9808537ab", "db11a718de6ec62d", "aab04f7640222e91", "2eee868d7b81c45c",
            "2987cd19779652a7", "1afb60f21d6797ae", "b79de0169ccc8055", "50a62d04d2a24932", "afa266d8d066003e", "a1fd8b4080255f30",
            "ed68190e321aa23f", "9c861f25a0fd6127", "a828d016d13045fb", "601e7d13aa4d621b", "09d87a545ef40189", "030e82dccda59ec0",
            "a2427fe86e5e9e16", "d34b26983b1fbe1b", "c178c03920b72dc9", "fe12b5389e86541c", "3b9fa32b29164d30", "d02622a5ded6ac6e",
            "358bcee375bc55d2", "f6f108967432d756", "bf4e3510ffb8f823", "7057248dc7e809f0", "15e531df22592fb4", "7c5b0c304c4d0cbf",
            "6370d98767e9916e", "929199498df6a321", "9a02e24e32b17741", "22cd7a33e03fe111", "dc54743000ed9989", "b1de021e687d2f83",
            "f208c7d2d6785761", "5897b0d923c99ae8", "acd29001ad8496b4", "0d0162b0153bb60a", "ce4729ffddb47004", "5987cd2946fc7ef2",
            "7a90e69f95ab890b", "e78d7b9e765081ac", "1bbe700e1df423b0", "acf96b99139dc7ee", "e35b0deb1bc74aab", "e17a513a556ee2c9",
            "688ef5c8e6337e08", "b4e34c02b045bee9", "7d4fe06bd3ebe29c", "6e8f543e823eda12", "dcdd95f823267f75", "0d5a7f3703cddf5b",
            "eca9d403329cacc8", "855ae758b353990d", "155d0130ced2e9e3", "bd2f20bfbe414da4", "9ac94dab3b71e243", "76bd28b4a42b4c55",
            "d5d73c3a9929793e", "9e7c1722c48324bd", "3591b1898eaed2e8", "39fb337375ed6cb2", "5537f24a1599be33", "f2064951014ddc90",
            "705ca10e56f972e6", "2eb975e5eb1a28f5", "59a5c07db4be44a0", "e31f16b5da7aba39", "aff42e1d7d61ea61", "783f8e65e6fe8646",
            "2b55b8f3f1a5a6d1", "e6460e2b270bcb02", "efd3acadf0178f4d", "9ee0169b7dbe0940", "49230e15bb95c6af", "163c4764523ac85c",
            "df03d02cf2e8615f", "7f42f9e7c3a62429", "81a4c24fd8462309", "73393245f5cb1457", "34d107bc18578151", "0691e092a77b2fc8",
            "de11dc4d4fc4e135", "e0c542900904247d", "633f05a47082277e", "eaf4170d435c1617", "c398a7c46146fe0f", "51ec77ac3165c2e7",
            "04eb42882e2892f0", "7d6f6e404cd7627a", "cb6475a518ca99f5", "75aeb5e5af5de280", "247e4ae7db4f3ade", "e900f64111d9696c",
            "864c805304624b09", "597f53049282e97e", "b11e3e0e3f55fa83", "ad72a20e2073f9e6", "df8de43002d53133", "e2c950da8f579cf5",
            "d616d084a1615046", "cf9af57591384740", "2f9133711c51dab2", "3d410f2ed4ab9c6e", "80c1da0987873003", "5b42d21d41743a0e",
            "339f93a049f1f9b5", "40944b50593076c0", "389770458cb93fbf", "f5beab9ccedbd5fc", "13c1de110541cb84", "1866596454ae366f",
            "37e959132c2e7c96", "b85c81c2eada832f", "c1ded4f4cf70e6ce", "43c29370fe70e38c", "84ee8651e4a91204", "97de9dbc3697510f",
            "3502739785303e64", "7f2df4434809a991", "7de49cc8518099f5", "9483cfed1da95aee", "485d4ebdc0ff6e4c", "a900c334dee20699",
            "63009251721168e7", "25aeebff4500bebb", "dfff99ac30558d31", "d8fb2b48e0b20184", "9185bf41450d5574", "58ac7d8629d632b3",
            "f34d2ac642e3ec01", "d0a88d337d03e5e3", "a461293da8f7783a", "0203ef5a162ba916", "5e1a2e6ef00c1ae8", "9a67f62034d65368",
            "d4155ee32991da5b", "e8ecace5a9d56947", "6f4e76c8bf07d5c1", "05d8e1384b251be6", "1f66767b9bfd4849", "20998d61d4305b07",
            "29f17edbef501db5", "7ba446e114628ed2", "e53ba20b5728b93a", "44d1a508e43f07f6", "47e76c0227aeb380", "78436a0e80ea6856",
            "0c3d6b3af4247f92", "ac89c37b0a95d932", "d57480a03c82e3a9", "4b3ff2dcaebdb8ad", "3c0452bd1d67f643", "6049797696aa4fbb",
            "84486c6de55a4da3", "ea8e280c018861fc", "b3aab0d99038bc6a", "9438ebdb5a269298", "93ba51c6630c6a5b", "40fc9ef2015df9cc",
            "283a2dec894f6ae6", "571bf92d05ce1746", "96ac82c6f11af3fa", "82344aff3c7d31f0", "8f3377cba704a702", "0f6c80ef8da6f90f",
            "7769681b8c7c348b", "f0af6bf21d22cf76", "b26f0c876bbdd100", "390ebab0b2d04982", "96d35f298f4dc08d", "c26fb72a6c44c844",
            "dbf7d2face047b32", "1cb57b38131ae976", "f1b71707a81f2f2a", "61ccc31564b25e92", "b618f7d108c67ad7", "3406910470d388bf",
            "a452345f38bfd75d", "f8e03ca69d798a9d", "aaaed36d4a623c3d", "b8aad85a0fab38de", "43ec4d54e6a48e03", "9b6d335963eeb393",
            "8a649d1564518534", "cf2d5ef78361f5f7", "cd1e466b61d3d711", "42036ac805a24466", "8ee4b067974cf5da", "c3fd93879602b08f",
            "f4d3ed2b181c7e5c", "178a632df5bd96c5", "6e43007127bdee9d", "9f0ec407fd6c17c0", "66d504da2f8ed794", "2fae43dc1dc3dc73",
            "f912bdfa471d1ed8", "22102c429b68f57c", "5d0fe64c91eec3e4", "1eb97531d0d4af4a", "fa49ad29062976bd", "b5d1ae63eb55e670",
            "e82567eb03c5eccf", "16d5a036d7f9d115", "1e7df22202b70a3c", "5f613c639611a894", "45367cfe2a8f9646", "22b5651ab06b3a58",
            "bb2b91e904b56022", "9b35a39365a47db9", "2303159e22fdcc57", "71a4431f5b1fa086", "e45cef145bef8d6d", "ecb60e240bf30b70",
            "f34f56fdaf7461fb", "3f7990621b6327be", "b3bff49e83af6be0", "3d6aa3c1201400f4", "fd305eef5646eba6", "f954ad1904c834d0",
            "c33698d972d8ed40", "1abf47b50cf52e94", "6e5ce4f63ffcc576", "70021e7a020d805b", "00c280c7f39ac172", "f17c234a1b125334",
            "efcd1790f917f38f", "effee70d817d42d2", "48fd31f243626011", "40636cf8f06c3277", "e3191712cd011d40", "7feae0a03c67983e",
            "db01f9d08d2895d1", "f18f4339ea0a4cc5", "ca42da6f6b1d0b70", "d958bf04fc64025b", "72b6c6cc85c5dc08", "f052ac512d5cee2d",
            "8c55951d53f41c00", "6971d90ebb38186d", "fc1dc2ce86bca198", "df87efd1704b369e", "7776fb8dc04e5c52", "0fd95d8952045ce5",
            "8ea0fdde52f4fe6c", "6327566ce5d8e686", "1b8aa9f5d75d02f7", "a8e66c86db3781b6", "110569f35c9cc2d5", "e2162178f91a0a03",
            "0718fabd936ff42f", "70fcc13214804c94", "b409737c547759d1", "e5cb5fdcb7f4d5ae", "8e0e6ae438217604", "a51dbd55c587a356",
            "cad6a6f6c01edadc", "067174a3b0077453", "f9567e471d22bda2", "8269d40a990e35d4", "8a659fff46982fd2", "e3f8b1869a999aa5",
            "f42582675ac57a1a", "5518f5c9239044e9", "e42243d2da3ca715", "8732a3af4e72374d", "891c9a6bcca50083", "a4258d9be057e6c7",
            "bb37ba3cd2b60ed3", "d7e33359c22b7a6a", "fd5c0c2b79b89394", "f63f257b4fa42be8", "38afefaacb0cb195", "96a710ad4f42b5da",
            "636f061e9dbeb393", "21100d3e99fc8077", "9ad7fc848309b19b", "595e229a0fb549b7", "1ada3e9ea5b0a44f", "e26d3dfee4a0d094",
            "00d7c0449264cc87", "b24b9e1eb4d063f9", "fe3e7c876e3a2805", "6543c5420bbb0872", "89b82d1e6c90113d", "15638b509de53cc6",
            "44df5b3cfc7d0da5", "40e2a4fd03f7ba9c", "678246fe80281f12", "cd05e3cb88a5012e", "e9da0b098447296a", "99b50a697a418580",
            "79503ef119dd65c9", "00d37167d40b046f", "24834de447191314", "4dcb3a6a0b4145e0", "42b53fa239e20d3d", "d1ffde2b6409c340",
            "f9d0c25f3ce5423b", "a573b7eba0e254d6", "f710531e75a02d33", "e6e758622ad11e0c", "67f6dd36042743df", "1c07ece2a5015acc",
            "c4e70a08fc906ade", "a7ccbd7e8d618ecf", "4e157f1595c8d922", "9cba5dcee3f69a28", "36a42029d9b89750", "e51b527ef1308ba1",
            "b4c2e99d1a12624b", "e21d35e77b304a53", "df293724313ad241", "83acb4cdb956d4cd", "17ad5cc8a0153912", "975078cb0db24a56",
            "7850f42e090a3455", "df87b6000bfda166", "31738419b3c2dd1f", "aa07ae5a2c361961", "3fd83d48a8f4127d", "7625f14fdf6d3887",
            "3ecdb14939d921fe", "318e7e11a481b692", "135ce11f1464a710", "b37befbecbb60dc7", "44682e312472f45a", "8da0014a0dfa14bd",
            "1a2af9fcac328a55", "476b68bb357a0a8e", "2d1c77b5afc3e014", "57c03f4a0db8e29a", "c3b57b6bda4c2786", "e3151ca2f59c16a2",
            "88a79948b3d4c84f", "7d020722a350062b", "0278a4c3777ecd1e", "79b3ee585a2817fa", "c89098c20243fce1", "e2f7a587f1a930de",
            "ad517fac7bf2011f", "c739e9ef5c1dc217", "49a024530978bf78", "56ecd81d64fa5f96", "2a86fd4d94910c71", "7654064ea6e9b6d0",
            "11477fe150986fc3", "1a13b25fc61bb472", "96e69866f5b22829", "b1a92cf1d4549e5f", "5e345c4b81db976d", "6557e3fcfeebac7c",
            "6a3ab8c29deb33d1", "31ba7fa932201b26", "20560c12d881fef3", "04b23abf7089c280", "a88c09c8cbc50d21", "85e2c520f7ff55a2",
            "01777a3cf5ba4f60", "0af461f3067fc0cd", "88d433cd92064c35", "75a2d44e69ed85af", "e396ec595c256f25", "163f29130954e5f5",
            "6b6116032e39c00d", "e406fa1d54bc546e", "8ae9a7401081a468", "5f65cfaad1556680", "01bf4c011533da8a", "7384180e4c8444c6",
            "20581f565c4aed27", "facf907143488e7e", "784fb917d95663aa", "8c0ee8f581dd7b2f", "6b8ba24fddac2b23", "3bb01dded663cc47",
            "57df85cfd915074c", "04b7e4576963cb31", "f7090fcddd831789", "6a881a1ea2d8b605", "0f9920d2fca4338c", "0b3a085fc04b8ec7",
            "10cc04ab1be34557", "078fb45da3c8f0ba", "201817459c457338", "53ce6d5f75e205d1", "c9d8f3d4eee9dfb1", "47888a3f138d4431",
            "ba18384255f9ce9c", "6cd840a5a5b7e3c3", "dba37effd12bb563", "0866ad09687d1277", "d5e48cb34d91b013", "0e8a37bbc8ba3b40",
            "3f2b12df2d1151bf", "6eef343f0299bf43", "1dd05f8b935f7d02", "6aad4736b132f989", "c1d38f4803879745", "9942682f46352c83",
            "68c099646c910d01", "17330084c84ec3ae", "27d97ab6d9dee7f5", "fd122ed6680115f9", "2cf59a59a89cc3bd", "2dec4c4ba6f6b8e5",
            "9b5d6b0a1cc19bae", "87252114dd4b40b2", "b236cb4b996abccd", "5ff484e3e8b06f27", "c862d7809c712be5", "a1e2312e9a1e1537",
            "66744b1d2ccc25e0", "5542dae76ddfcd94", "d009369355131fa5", "d2bf63d996fa9554", "0c81bacd2a79db7f", "1a170804f09c1b3a",
            "1d1e2d200baea071", "ba3eaaeb6cf45fc9", "b8e71551d2b6ab6e", "1ef163e40a4dfea1", "f22ccb513fb2d57f", "e1d090ad9d0d63e8",
            "19a13445772e0d5b", "77c7449171e91858", "62b56d5bc5ee4ee2", "c78006bd449c4c56", "6a66dd4e34aac4c5", "33c39a8eeb553718",
            "883c19e7f8ea75f2", "58e3dd99b6410ce6", "c62fc92ac1bb803e", "f168d56d533796ee", "99f95fe9341a212a", "46eededcd7bb2358",
            "4d25908a29ae8e05", "9b8857b433fff035", "47cb285a48ade98a", "35fcac1f1694c76b", "c9ff9c92b1bd9479", "6fa58643d0928e60",
            "9ca4e005f104bd49", "cab809748fc6858a", "e48a06085df018b3", "52fb1e5db5c895a3", "4f7d88b93a88e5de", "1e39d4e27f6f056a",
            "c0a65ebf36c454a6", "e0eae5d5e578042e", "c06c144097506b6a", "0bad6a5810c1331f", "5cb4f95d03b62b5a", "62a2a9303d290a25",
            "95a0e2122d200e6b", "ba2b3991e222fcd5", "adb7c2e64057143b", "ba8618747950b8a1", "4347e92b9b593453", "6e55190607ae7464",
            "37d0ed1dfc554709", "92a57db8cb4df56c", "84a85e2853defd44", "c77a7c9e96535ae0", "ab85cfd9553b3338", "7457cd66379b9dcc",
            "96832d4eaaed38c4", "042a44853435032a", "724925928291da56", "ce27d194963d1d16", "93db1b09606ed835", "b509cdf1a4c45d78",
            "570c26f6ee7ee106", "f07309b7f10df2f5", "ff43ad7847931387", "9bfa4e47573d3ba9", "19eceb1bb5b5de3c", "268a92741aff2b6d",
            "1434a5ef12e45229", "a9bb50f998761abe", "c990abfd81cb2711", "0ceaeb48daa4fbd1", "7bf8f14a494d1c34", "f1a9dd2f24aa7216",
            "7f4a159f28a002f7", "3ad739fd91fa7926", "39f8bd0f1a07214d", "6ec228f997120598", "ef20b577f5e1f3ad", "6dfab813b314dd5e",
            "ce09229183fb570c", "2255730074fee2f8", "8e6df202d4f8d771", "5eabafb172db80aa", "6b69367e44a939c5", "2188e89937151362",
            "f3ee791a4d66d0c5", "2ebd1aca0ca46833", "f0dee7da2f40e023", "a9fa35f8e445cf0c", "f1cc8c3a1917d2cb", "904c935fa3137863",
            "a4f6a8276e9f913c", "83662041133900d5", "4d59f9c47a1b49ae", "08ba0ed1bebe5a2d", "8278deefd70742a2", "f49e43a627017cec",
            "e0727166acae1fae", "1480cf7efa64ce3a", "bfeef1d2ad711e4a", "33bc6a101c8818bd", "ee99de1800b87a47", "51634f99de99d026",
            "ae668a9b25cf5874", "6ef873aa47147c29", "bc371fc4e751357a", "6b0e9814c6286751", "72d1f21da3a80807", "fe4489fe1a6e4c16",
            "9acda118ef114b7f", "90434e2558ac1b0e", "a792aa3bd6c3ddcd", "7129caaf77da24c7", "72c88e9a87bd4506", "089547d84f6c53a2",
            "bbe0c6fd4263ca3e", "e45bb099bcfd3bd3", "5c1c7de555d7d79b", "b40b849419fb6cc9", "26d66db9b851d147", "2aad03bb6b533677",
            "18dcef95bde2d36e", "cd87ad560d5f6a84", "76e456f5cd6c97a8", "8fd42a34cdb057dc", "e5ffab5be82431c6", "e07fb53138aabc52",
            "351f922ef9b528e2", "7877e6d7e8bff069", "8ca54dfe89174d45", "a00c0a50159e2a75", "571804b8a6ec81ce", "d2fdc5779ccc2043",
            "c13fb328615b329e", "05571e3bdbf9782f", "dbecac2a40ce66c6", "0fb84b0f2410d58e", "6e78093ce89dc400", "42798d612729ea94",
            "29b6270b6a7bd46f", "03c40f0460f16b42", "6d2ef67eb33bee47", "1d62fc0c4f3d70a7", "c74a74a0019b3042", "de0d5f744ca4b012",
            "0f0dc2a766f293ad", "b73abe8e2fb342c4", "06549ed5dc334bed", "bc6786dfe802cb13", "eeee4b0c40500338", "8d3af2c10fc0cbba",
            "b8b7ebe6703fb9f7", "7718c2ea1901bb03", "9bf2aa726d17a904", "645d4f6c644fcb44", "2e0825a251bc6f8c", "f137ceb1adb2f917",
            "39e14ff3458a3c9d", "597b37804d2a50b7", "97e296795ea026cb", "5869ceacc9c653aa", "fb14d3105054c8d7", "f06932fc264c7403",
            "0e5e50f9278865c8", "5e7f02833d730972", "17132bad2912e01c", "fcfffa434f105cd1", "448fe507d2f68de7", "be57cf769d290c42",
            "f0e4ff6076417e02", "ba6490904fd1de05", "43ef344fc6cbe315", "7d7ea7d8ae972c2e", "63ed081bdab03956", "0f74aa7c07541027",
            "189cc0ca32875827", "efd5817feb0cc87e", "6f2511b4afef22d4", "b67f7e5a4eba9337", "becc2df184ebadaa", "f05d414ef1b45321",
            "0c0e311a6198a8d8", "fe615cb55a2c5d8b", "455a1f5e28b14f4a", "c0d40f1d27f5fb62", "9e918a7f76a0053b", "6654b795ae2e7cab",
            "cc65b6e9d9850719", "04e325948c292000", "d05f0b74be2b168d", "bcf078dccb593f6b", "d7e5312444c22386", "1246c70902e14df0",
            "f309146214c881db", "fff3b18006db5f4c", "ffb29d4a5b75e6b1", "05de0e271c2489b4", "350fcfdcd860d6a9", "26a8c715fe08ae89",
            "d5b290004e3984e8", "da9a5cea26d7a9a9", "cbf5d7819482c594", "2d15dafd77f51e99", "1563e7b6de8e7d80", "12f59752e2f3a340",
            "45f2d452a78a748a", "f2e492d8aed31fc7", "8dd62ff7c7522467", "dd7dc40484373a79", "9bca02cc75adb0f5", "a8a9478be0ce83a1",
            "8b21ec1f971b7d8f", "f0cdaaaa9c60fd98", "064d0c85ec9aa6fe", "9835f6d43ec67d35", "4592ec9a3a6df5f5", "af858ca2b3c1a13c",
            "9db49dc797a0a37f", "68443ad7ab80814e", "1d1de014b5397c10", "e541830b48fa3c0f", "e7e9f8ade57652d1", "0cf38c54bd2e714b",
            "92c689270b55af74", "353531e0a5709d23", "61ecd1d6b742c2c2", "a53521d19206dbdc", "21888a51f9f48cbd", "ff7d9b883ad56ead",
            "01fb47c19c4a4bae", "cdb0c0d07f31aea0", "afd5f608dbe1635c", "69c6951ed8e225c5", "614883a149943939", "114491f798ea0cc1",
            "ab9ac19ef4de2831", "397a87f9c03eb274", "379287841e7118ce", "7aabba605eceb136", "6b95eb710f2724f3", "bc15efe5e9ffc204",
            "a89316b0e8c1c48b", "397913641c20d5b9", "38c660a1426dff88", "b2775343ed200538", "73bf59c187ecd28a", "ade28f369b630092",
            "618702cd2fc0671b", "f43eccae902d7239", "748eba2529cea590", "677bc77e6df97a57", "d378fe13964207f0", "b5903b36ad668741",
            "a2df7466e6a629f9", "04b1572d4835a1be", "4046a80a24b62841", "e417675cd9eaa99d", "a9283d36d2b13ae2", "c5bd467fa6676835",
            "4b6bf7c8d43b84f9", "890df3057596e4c8", "284434b7bcb21f90", "a068569d73920be1", "19d7bba8795ef0fe", "da23953fba735804",
            "23ab3b00f1f40fc9", "d742688dd8eb634a", "f025de8f50f9018f", "6bb8d99fe06d2b42", "dc779db82b590e08", "338bc41c220869af",
            "1849fc6b0fec06c1", "d2421ebbe464df9e", "ec24680409852ec5", "774d877fcc4b2ff1", "997c08b90ca91b8a", "b262bb83142193ef",
            "4f65db32514163ff", "efcbd5838c4886db", "9dff36a7c3233886", "8af8e5c3be332309", "52d3c8a801528d17", "593bbb8c35ff2475",
            "d67e18eed1bb17d1", "b12ece9585dcbcb1", "9daeb07833d7d254", "6c8e050c8b17881f", "d9407ca5c0df410f", "0ca4ee8c318e2896",
            "e1c6cf6ee31ec726", "69b3ccfdc7871b70", "904f875717c888a9", "dd8a745bf6d18915", "3ac797a5601a8e5a", "7468a717e6379eeb",
            "713005d520cee581", "f4fc9f491c8981ec", "56a02b3a30f7fcf5", "f732c401d90e59c1", "4cb2453acaf3757a", "f929566ee79d8543",
            "8208f6875b3271cd", "1857028018a30e95", "feb504f0f20a223f", "8543caa42c626dc0", "6cc86944f0ba9aa1", "6d54f4fa48be18ae",
            "7301d3369af1ec98", "63602f9147c24dd1", "e523d6917896fc4e", "bf845b7c4743d1fe", "52d00a1a3960df22", "ffe7c133647294b4",
            "77369d379f8e48fb", "34af11c7e2d6f374", "979738c0eb11c269", "5e6f501706f325ae", "ff7ac5eef75148b6", "dd5c9f7154ded9f2",
            "586929a3d6e44817", "2680b2e2f2dfc9c7", "2c73c88d3ebad144", "2b9baa7e3580071d", "3138680d0c3d8169", "010991d108aa6d2d",
            "dfd9d918a3af622b", "95e86672a3c6eb2a", "6d59299bd937397e", "3b52a0f9ba0d196e", "bdef6958622a2e44", "f8e4600b22d49296",
            "c5c0162d90c1e8ec", "91d2bd5e2bf8a4c3", "e2eb9a9e845ae6e5", "4d33f1cceb237105", "63526070b5589b5e", "d65654bb592670a9",
            "4481469fa1aab32a", "a552dfaaaff72448", "4257f5f68a609889", "75b72f0f02544616", "2094c3db6df47a88", "7fee0d0c4fe5cad8",
            "0149e5c1ea7e8298", "4d8d93d856835f8d", "9ce9225be00e12c1", "99ede7664657230e", "c5cafe68f1cb34a5", "05c77fd3ab258637",
            "d690d3e72a755f92", "7cc84ee112f7b9e9", "e850722dc8b30520", "084f3430802cda37", "a8d07127a4c2f787", "ef9fb29c770998e9",
            "7abffd68fa06bb38", "afed16d2241e1956", "fd39cf6632e198c6", "a72dff804f46fb00", "c64ad39689a0592a", "b757142fbb699eb2",
            "1449786783e80062", "8326eaf4f22bd53e", "2fef8aa182b2c6e5", "0f3de684644ef743", "759d6edc03f5223d", "3be8d9ab334042ab",
            "58fbd7aefc0b298e", "13fb4e4ba6a86a8c", "997aa9b450ff0129", "00dcb729afc1b929", "0fef14621b8f41f3", "23a67e5a7c353462",
            "93c206afa2d3e2b9", "17279f96b2cbcd48", "dafffebb1dd27563", "ad017b838b8450e9", "7705eca1ce83bfb8", "c850f9c299419dd6",
            "3cfb06c5a6b394a1", "7ded7db865df366b", "8e7cf7ca3e18f8c0", "452ce34e0b8ecf4b", "7321f7be7fc3bcf6", "d9fee94a286f86f0",
            "2efdd2288a8a76c1", "01c6afde8dafbc80", "2ce7075291a0a3d1", "1289175d47da5a17", "749003e4933eceda", "583fc701d98badf9",
            "b7b75e1055ba241f", "caf6a2962f830373", "9881aeca741aea80", "36ce933684522b23", "78fce01ed0949a15", "fee397c986256833",
            "5642e171729679bb", "4f09210557e3a9bb", "5ccacc0a45a1be60", "0ff635df11b6388f", "ac580651ddbeb51a", "301788bcce12a80d",
            "d4bbbadf08e95729", "0f61c2506bb05c87", "82a0247bb32131e9", "b68d43b6a2b492dd", "cf9b5870f035e2f3", "30e2fc33cd05b72d",
            "2642df5b0c74ff36", "f87cb2b023da4b48", "bd190d319ae25426", "b25940e8961e1b4f", "615566f1aa143735", "c5b00ada0b9bb23b",
            "e9c9ceba146844dc", "d272d60a9eebe336", "0c7958d54edddada", "8e276130e5525d22", "7720a6dcbf45c6e0", "78b69db96c3304fa",
            "e4f34eea2fadef5b", "412ce057db192eb4", "f7ed42ddf5f22a93", "e7be4d75e328207c", "299ab244d71ba2f7", "b94f7b62b75229ce",
            "fbca3aa89a725681", "680f4468fe24e566", "d5a0855142bab533", "fd417c2b4c7f194d", "f1ecaf635c5bf308", "ddbbd9c6115161df",
            "5a096efb29dff370", "0ae95f1b56e90c96", "cd78afbab0ade1ea", "f53c3280eb0593ac", "a01d0394794d7ebe", "ddb8ce427dc07874",
            "fe0ee771e5943662", "2518537f675cd7c1", "4dc02200cb2d12e9", "580bd1beb8d8229a", "033cdad982a7a5af", "8352a0ff20678d6d",
            "52ac3318013ff75d", "1babae5db8161320", "b66734bcf134aba6", "d02719832ebbb1eb", "c37bfe66117c9b26", "ff934b0c65d4ff41",
            "ff92b57f1e731ec7", "754fa18bfd888227", "7ac344e6fdd0ab9c", "349a622f55a49042", "0f398931e29916d8", "8af67a3f60d73ca2",
            "7c6a43b4b9c94c65", "453a65a79e8a78c0", "7d1a78764648a262", "adc9c98beadf9b11", "5b452fff05da92f5", "745e96e891023913",
            "12d744f3e4fbe60a", "14e0733344368b64", "7c41a62d90321ba4", "8953e7cad5f2cd6a", "b21774ef47a93188", "434fc3e4ea6dc719",
            "6afb20d773e6f047", "b6daa9828799104e", "9088d8b7e8a2d0f6", "eabc2442b5c5bc96", "28b50e66726944c6", "34d7576267e1e597",
            "7276aecb8712fa2b", "45797a6af43f495d", "52957e1a33324441", "c0ed0a021478308d", "07f4cd93b89a8861", "868e34d7fd363c12",
            "d88978472e3863cd", "7c5bf9dbc5501e24", "fa50171ddb31e59f", "2fbb7838ef5ef2dd", "e634f155742d964f", "aad34fb029e41e22",
            "6b43953335179c63", "3933e4521b9423c7", "e113aac336be7c69", "bd28a1373e73a126", "3dd6a8034a657340", "eabb8f35696f05f8",
            "39d724e9781f68b0", "f263dabd72081a74", "13d2cb0e01b5c6ee", "bc3150713f858bd5", "7b2ab1a1a42f14e1", "87f816e5ee55567d",
            "465f97107f23e4e4", "658d01c0e43db3bc", "f044a609c0441178", "d4cb827023d7de17", "b5e4f16a9766bfa7", "e41266573e4e8be4",
            "8e381263468f1884", "94a43426a973e522", "de3c3e7ef0562a05", "2ced58e261378749", "a975ff5fd6a844ec", "606dce7d939fab41",
            "3db4f23706228b66", "8cd0840b4715b201", "c92982beb828fc76", "627d23d737308032", "b107d8969416f57e", "6a1a95e6236674b5",
            "c77f200606404496", "e7603d7b57dec5a6", "3defca3c57f8cb06", "16f43b52a8fa6ecc", "e3f151921cc549f9", "39dd290614c9138d",
            "05cc15086465d210", "2d930494ea983b56", "34351de5551d5573", "07fd598a0b8b5aa1", "eff7681b80cb2a27", "1318437161203237",
            "4619fd1de36715e9", "580d53d2760845a8", "45b6fffe5f9b9d76", "6c6b0a313c3e7880", "0a153a0bfc66d3bf", "729bd511465e7217",
            "8f2b4f9e878f5861", "0e90fd00ef983017", "5fc3a0b651cd51bd", "ccf27ceba40b42ab", "8980d8edea16ed10", "68a91fe1a12e7aaa",
            "261e4b95abb375eb", "da066431e0d378af", "1a235c7e001ae8bf", "fc8b6e6c9f980f0e", "41c6998eb0465e3a", "1099eafeaf7f2a5b",
            "1b0fd1a4bf8d36cf", "ed4a58157768721e", "9a6523e3e56bb753", "d31797dae9366ef7", "662c284d9168bc1c", "1e2725ed74d5beb3",
            "c4b6a96a6413cee2", "dd281d9499766bb4", "34382ba6cae056a9", "17447b169d4737eb", "0614fb2a026c82c8", "5780475d6fef1ee2",
            "a7042805ed785858", "da00a25428e6bb96", "016fd111ef443c23", "48b544ab4aff0b56", "b463c66f6d244757", "cb16f40af414ac93",
            "baa5c797647c1b83", "9d1941f7ca6b592e", "5fb0b2fb3c23a804", "00b3e43041386a39", "7e622add288c8ca4", "c82d31feecc6dab4",
            "984ca64d55e47e31", "1b81685cd6cf34d4", "443696241f6ca95a", "d301c4a2ed4faa2f", "be950f553d8b2f97", "aaa9bea5373bed41",
            "4cc6e30d9ff968a5", "665e3d5845221b79", "e0b135b829043ce7", "4350d116723f9ae8", "14efc0505d52705b", "ebc94da2d2788932",
            "ee101b56168a58cd", "f4265f90bf83895d", "358d1e71065f769f", "5748c24b33eb0ea7", "a67ab5298cde3025", "6f18a747bc4a11fd",
            "abc3d59ada029549", "1f8252bdd1dcb467", "34e7eb21e948bd15", "6a261b3bd37434ea", "7daa29d0cb3878f2", "c67235cb08d61a87",
            "0615da55512572e9", "84f7a702ae427519", "8dec4a1ba41ca38a", "3dbab1dfcf4671ee", "9bc1b56f96d948a2", "e0c1736085fd544d",
            "ede1a3b4f9e3f867", "a1f5e66311c0df46", "37637af17b227bdf", "2c2d67fe7b5d7062", "a3122f4bb4b161ee", "2ef987ae3e1d7fa0",
            "bfa9118fd8b9a4a1", "3267cd7d8f10d293", "436de2d6d81a73f4", "8458bb2871715c18", "c0386ca848348fb9", "b4a2ee3c8ce8a079",
            "c6248e11c4098dac", "8a56b5900355b9b6", "3a6e51da3e1657f7", "cf25c62ceb362eb4", "ed3cfe1e8f0a16d6", "b34dc20d300c83f1",
            "17a26316c5bf3c15", "eb25c57b91ed4f6a", "32b0c61cb4121835", "da28b12b122cc722", "60b35c3012fe4c6b", "3e69d6332de33407",
            "d3e88d1e7903ebe1", "ad5c6560a4b5f538", "16449d620b6caaec", "f4c40208771683df", "2a246632df967faa", "4d193c1dd252a8fe",
            "6c69def8a7f10cea", "b1cf8b20868cf43d", "00acb703f892126b", "e8be42fad6dd5e81", "3d18c3499597177c", "f8ab543997e2ac8f",
            "fe3c1e99b5ebd880", "e363bd661093abfe", "df7cb5f91eaab16c", "fb6d2982f2093996", "ae60234d02fe53a3", "1ad87bc78a57d351",
            "4b08d8f44c3d5528", "05887df945d27999", "c6e00e559bf8569f", "8baf1ef215ba65f5", "08fa3486fcd06525", "64ba9f869d378fbc",
            "91fe17812650bab2", "04874b1b26736252", "598d272c0281b89e", "5df1c31f92b8061a", "aa8d777d75100e20", "f94438909e89fe01",
            "2258ea00806fe3fe", "18b76a2c04c5a3c8", "2debd9699504f28f", "e19002269e59fdf5", "ba4e9f380f783d10", "3180f335233ec25b",
            "391cc9ecfadcc1df", "9eb50c46bbbb182c", "8bde0c3bc53568a2", "c04ee58cdea1813b", "0c712ca8706265a9", "d9ec6fa398785c9a",
            "9745bab552f2ac1e", "477902a2c560c0ef", "f684763c5640ed5e", "d23ba56ef03fd4b0", "b971b62efd303bf0", "354fe80c085d804c",
            "b3857d34311380e7", "02bc2695cb7e7070", "6f6dd2b95c332156", "091786f3af19114a", "568f67a82c608c9f", "fc004c964983caad",
            "c656af5ecb6d9b90", "b892a8e2f048e19b", "d2fcf237d7513015", "ebdbb9891120ed7c", "90a24249ec4950cf", "f406c00a1c214714",
            "3b63a86bfca12b71", "cd89581376fd3561", "3f171c7fc796e43d", "73da197558fa398f", "869a9d9ba2ed18f4", "06b11fb5d361c074",
            "177766478b738cb6", "0db93fdbceba9059", "47aa0a57d23f1285", "b1b3a7b4dc758245", "d1f308e53c8dbb7a", "a2fd340700876613",
            "c1873473d5f4b4e4", "1a2cae6777265422", "a527e766d225f0b2", "7a2c6b9508a50c18", "444e1188c96ccb33", "eebdc137b03c2777",
            "b6d6760b3510ef35", "8e9888a2a683f42a", "a3c27fb919a6ea40", "8450be9e681f007c", "9c67f962cf48605a", "76158c277097796e",
            "e7a0f4f4afa41b4c", "aed3d6f08baadaa5", "5adb8ae17be1c844", "2f1cc8eaabd14415", "34e6b90281b48ac4", "a6358cccbc1b2607",
            "07cdaaf55ecc50ec", "85fd06028718e26b", "5ca800b4262f0d7f", "7daebfc3850e0a1c", "5a73206669943810", "f48b79181f75a298",
            "70cc2230726855f9", "7614d991051fec29", "ed09b24bb4428c34", "aa20a6d31f7f1a83", "9e0d9c06d34ed5cd", "2e0a95a41b89aa6d",
            "53debce560aa4171", "c3e2f4a511482309", "06c7343e6c62720f", "49663828d2c8c665", "36455ed99dc01a1c", "5675b54a89f4ef25",
            "3241df61dd6c58ca", "e14927ab689737b6", "62eb78f012aaf377", "2271e8efb2861938", "d728ecfb8646b369", "9518315a7724e869",
            "b1cb5976f1eb903e", "b4d50b7d6fb576dc", "5215046e5b415caa", "ada4a48e8553f340", "d2240eb577613887", "d8681906d33ea787",
            "c8ed3bc7917153f5", "d7449821738afd73", "665a5708201a5930", "975d5987988dba19", "91c2dc0866f73b4c", "16978b018f1a15de",
            "94b99de2cb65c947", "b6b77df8462232c2", "62a40a5717be1ab1", "f595a24dde036489", "b6e5e7a0fda36ed6", "684bb71464329de1",
            "64c4adc7cb42007d", "aae4618da2a3b114", "02670126390eed90", "3d69cd87d170eac1", "34ee6ce3e30c5cb7", "5d7556016b9e573d",
            "29c2e53c9c866ace", "228224cff17cba37", "724b962ee319f1f0", "5d12003c066f7696", "aa6dafe81ff04bd5", "db754c7d0af7e0ad",
            "d4fee3726c50d9d1", "d39bb10494265bdd", "ec08fd7a0f1e3d93", "d913541a23ffb0d9", "861c86ded28013a7", "f66a72f594b6adc2",
            "d4aa6768750e97d6", "e1b98f64cf662d5c", "16578711c0183cf7", "4568b181bed73b93", "9fefdf70a0190c12", "ca28a1d14c72a267",
            "560c7387102d2d45", "306e15c32c7b5f62", "f6f9d305276cffaf", "8ee8078a6205d77a", "e303fb22b4417ba8", "23cb194048b96b62",
            "ef8692a612df0835", "ce953fbabce72b68", "f2d8c6c3b44e8ab4", "8792f40ebc0a54a7", "973f571d5a6348b9", "7155b9280614b748",
            "254b3be2dd9d8d93", "696c97ce64e7e297", "c5d3c69cd219b626", "569cd6b9d98acbab", "5df033cf7c450555", "889d34aa0068ee03",
            "9d8601c79c7c9a2f", "61e5d7b6b036b8a9", "6a614a0b6f51a251", "1f15e34068ad5c1a", "8393a2f94dd07ace", "e8f397d5b3135d54",
            "593d2a7e0148166a", "f8ba3ac04bfad613", "d20e845056cc5839", "40aaf5184094ccad", "b40e7a5c3487331b", "b065f72e230013ac",
            "9d13e94d3f70c0fb", "ae84012f9a2439ee", "a0c81d3d657227a7", "321da43420b6ee3d", "6a0da90fef189232", "34f4bcd14a3eebd9",
            "2699233a46419b9c", "46882baf3a774c6c", "3f1cc5096134881a", "691d841aa528fbc7", "2e99e10a3859f50c", "da832f10e2698b28",
            "4d981cabe643e456", "45b4d51cbc08d396", "c3af8064f786a3a0", "508e4921b842460b", "e0a5d57dfa1a809e", "2bccdb4c0fd1158f",
            "e36e4c8b61d86711", "cc55611f7ddfa5e8", "f31fc915fc546c09", "56b4f3753710af4d", "7a665b8ad5cec6aa", "1852cb233542fea9",
            "8b1f6b5c06cc50d6", "1b536e2a24a3b0c7", "0c1ffa994f2b8939", "fa7b09ee1c391c7a", "78b3d66dbcff2127", "c0f7d8817ed9d073",
            "ac2bcb2d0b33c249", "88fcc613364df019", "8f016c2e3d1501ea", "8131b3ffa16f60eb", "d68973373b1dec25", "36a120db902268e8",
            "e1ac9ef4a592108c", "ee24c1c133383d8d", "802fc3e5c3332676", "f5dcfe5dfca2fd59", "7f149877638a8b90", "dfaa67c1f3eefb8d",
            "72b6161122dd40d5", "7419a3666def7589", "f47f934e8518e9b7", "e814232663873294", "b8a9c9030ed79cf0", "93884db92a8da3cf",
            "6430a4619876c13d", "c2058d50f72a4f38", "761c82e8070783e4", "07cdb7aa9cf62bbf", "15b720d6a4a380bc", "785bf91ffb024c32",
            "98fc78bcadd8e870", "0fabf333024d895b", "6186ef08955ea6ac", "3dd3b0edd4289cc8", "d41de19199287d65", "9263757688cbda61",
            "cacf4079c707f4d4", "9877a263f53d497f", "7ec2e6a36330f90a", "deec20f8f8fb001e", "b022914f026948d6", "5040f20517f245ae",
            "5f61e939cf33ff68", "82ddf665ab28b103", "88561a979b8831fd", "9c887c1f821dad28", "f9252f0bcdce185b", "bf2281c2b5459f13",
            "e82edc15176a1de7", "d02b1159c88f3c50", "5685bbc172083959", "630f4959c013de36", "6ced1bed6156b8a2", "adb2c74c4cb68554",
            "df8b772fb1f1318c", "bd6c0ab400c0bdd0", "2f77e015060d4a69", "b156786b02501925", "cd9150510099da18", "de4a2e198adb1d6d",
            "321a209915f71e70", "065f0645a529f7f0", "9811da0ff7c2b659", "e99510845b086298", "a065916189215f8e", "b1341e520354c319",
            "2e2a0e7fe824e9c4", "1d50eb27cf7e123a", "a6884c23e73f6bc9", "40354889e1918803", "2966cfa52711d104", "388369500b5e4227",
            "974459d61dffe492", "3a930bf01291b0b9", "1ee667f87594009e", "48930cc02a724786", "fe3289215e8a263e", "bc0a5c201fc9f62d",
            "28632195525b412b", "6b8a47982aba0a76", "67495fd8859ca60e", "75961893ebf148f0", "b5f3ceef3998b714", "31da0e3f0789ad72",
            "ce4a6e990d33faa8", "21241689c1d1228b", "fc6913d97f421202", "c2ed23cc22466a54", "7c5ac6bc16e32d4a", "c6410b90c78b3c67",
            "55abbc8aee63453c", "446ba2176e75b47f", "38b343b627b6e0ab", "5598eb68a000cdbd", "d38ed11d4fc3d601", "e3b30c1dcd7cb6d9",
            "343020dccac407c3", "d234fb1026d21c35", "fdffd8e899cc5799", "8f25eb4b61439af6", "49650af5e5b82301", "7c6a184f23dfada7",
            "b09de4390042fbf1", "233dbb58ac305fb6", "7892b45598a05526", "aa4dd63f88ac30f5", "cce2d6e3a524e962", "aec6b2cc3f09cd31",
            "9b60259ce436e8c8", "841226849e74dfe8", "f62d424a0001f155", "b65b5144ecb8408d", "8d825b3f88f4b2a3", "b9dcde76d6ec15a4",
            "0342996b8fcf2761", "0ba08dec9ebeb3c0", "a0b60eca408b5c52", "a162f68b97910c23", "33f7c6e23fc80193", "c3c62099ba89cf9e",
            "ec173db95df36b2c", "7e895ea35ebaa3b2", "ec75d9cd381983ab", "44bdbb1c0993a411", "d1b91052ab55cd89", "033d50d81c2c4976",
            "db9ff3f560c042c0", "f3c72f1b31ba68fd", "8d43455c30252ac3", "40a77ca6d51c5249", "85d225c62f0f7fb6", "1f582500e4fb91aa",
            "52bda9a17b25ac4b", "3f1ddc67fcd8f2be", "290d7b03389e5782", "c7657f903f95e14a", "c4bd14862399e0a0", "9f08428083252daa",
            "a9d45661b5fee71f", "cbac1543a28c6684", "110059d28d2d3340", "14df835f15b03fbd", "475e39d6a6bd63c6", "de75ec695cd211e0",
            "bceec9b9b8d3101f", "11718b8955684d04", "811427c1b1854aba", "cc45b4be7609761a", "88ff569aa988719c", "edee36b095d3cdd5",
            "672d205424d4ed2d", "2a00e158fd0c87c6", "e967ae5774f1f5aa", "34679386a0f95b74", "5cd9368d903cec76", "4a4c06f26608f2ca",
            "e16c4bfe518c78e1", "577696f2ed52bbad", "f32f5843fca9ff1f", "734838d1d33b4d63", "f83a2ac1bc3dce71", "e79777613e8ed937",
            "047e9a0a13ee60c4", "91edb0908178f96c", "68bc26c4e7ea4315", "450e4bbb456b30c0", "5aa76ed213873592", "f2fc914de38b5a7b",
            "2a250835e595545e", "e8341e360568da0b", "6a97d252b787ecf2", "2cf5c03164319abb", "4de0e7d329abc96c", "9214a2145346eb18",
            "77714aecc869439f", "16aef358120eb6e2", "e6ed1ac9191d22be", "cbfa5741ea37457c", "181ee2aa03abb96a", "7793e56513fec598",
            "389c200bf5c57e64", "21e0a8a5f9760385", "312b5c5d5d39af87", "ef44c17ff19f56d9", "ffbb5253c4051867", "b783c63a0ca02031",
            "6f23d9b8861c5c86", "469b6b6f8fc7065e", "73a791b29e145646", "71a7a446ec2ad3f4", "e320adf450596727", "318731370a720025",
            "ce019e1d06c591ed", "ebb90dd280933c85", "62fab1322a9e3b9f", "4440378cad30290c", "39ccf84471bd5fd8", "8f1173d4d7b4dabf",
            "1c7e1c8a174d6fc4", "bebfd021d7e6fff5", "4369c1b47424d302", "1bfaaf824aace3fe", "65d58d1852480e28", "a9457ddd81e0042f",
            "115988b85d694e5d", "c78b90ac080a97e3", "cdc06c00ab28beae", "f2ee323d70e84633", "a9913a89e5bb51c5", "caf999a427d71bdc",
            "793788f603cbe397", "8147180f99e657ad", "5d305f04ec146d38", "d356bd304520b119", "6e3c5a1a52acb7e4", "3175f8b786f267ba",
            "c93ddeb62c2111fd", "eefc8fcb57d82464", "101e5b1477bb43bf", "e9f45d60d3286b3c", "2e1f72828010cd38", "ff806b17256f5658",
            "9a434e2c3fce4b79", "ee24fbe168c4ee68", "93c9772f3b5e282b", "b6d813cfcfc81cab", "5608dd03cf4ef008", "8b9f14fcd03e1ce7",
            "aa35bcc6e99aabde", "886cce7ec50d26ea", "fc5a7be2af1d4b1c", "503d01cffcb96d35", "204099a6a3958b8a", "d317217b43c3d1e5",
            "651e0a11f24f4240", "83492c1662239c9a", "d76b0e386f190e10", "d6c65d87a9be7da9", "74d4d4511cf44db3", "ba1421fd547649fc",
            "0a4f114c0f5fc769", "48ab2628cdd3a712", "d5f54ea69fe14e1b", "c346629bb8431657", "f633e44bf7b2a2aa", "a5e457934b2d5b76",
            "d55b4f9b54de4a7a", "b20d4db94ef8ff51", "a44cf75748ecd86d", "c7c3dd2cf46be457", "5bc40b4fd6808671", "33812e4eac283c13",
            "af6963810a0090b9", "9836024f91abbdd3", "ce731b7185a3fa0a", "6c7141de13b62d50", "80169be51a170c9a", "9e16af8530f66d1b",
            "c20db7e834c3d5c8", "5b175a0f9d86debe", "74dfd5d5abdf97fe", "ed078d3ed3104751", "6c04cab14ce98f4e", "00720b405b2ab7db",
            "0d4129c913745ec9", "cd3799b5ea8692c1", "8d66d441fe7ce9ee", "27dfc62d1f193a8b", "cb3726c054fb942d", "251bb22ccc9605f6",
            "30f0fc9b424db111", "5111240c84857155", "f2c3a642179f3f25", "d95607a8034faace", "da5701f0216ee759", "4ae9f90e8419e7c4",
            "7746b368219a787a", "3f47755c5a69dc92", "c0e2628882e3c46c", "a01af66662e52997", "aa05cf12a14cbe84", "2b5ff1bb793d316e",
            "a1c56b2c4d724961", "2934372a6a9d3e08", "c922d54e67916bea", "347a40aaf598901a", "29e24116fe9cd07c", "3c7d14fa7284f3ce",
            "d4a10d0775d28097", "9fd15cf06aef7f0c", "3e6bedde57917de3", "ae21e00104fe12f9", "93484c0593377886", "269496f30db80f39",
            "20cd92eb65ea9bba", "d6a0e345a2e09ca7", "cacc66e81669a0bf", "8acf702412e7e4a0", "0727603c34cb8367", "d54c22e3f7c31ef5",
            "88d7de6ae872e1cb", "6ab006f9c8bcc202", "04c71c54ad4fd323", "3ca1f99677e2a2be", "c3af72ee9e43d5bf", "7334fec729dae8b6",
            "e1be509676bbffe9", "821a92f5db04010b", "ac87d35abff0a028", "fec25444bbe45bb3", "2090ca43acdbc583", "b7fc13543a89add2",
            "d7f80aea5fd503a9", "ab4281ee86827693", "9ce010eea78b33cc", "25619ab1e2fb0768", "f397b9e5e7539458", "a52402ef5cab9854",
            "c10b908acca0ced8", "8913bacaaf1a26df", "2bc2629be25fdc60", "313385e38e3b3006", "90d93faa83571d82", "0d323ebaf2facf52",
            "94edf61a51c27b70", "45b2ec5cc28127e3", "9aa0b8b5f4fc0509", "2d1e9e5a701bd29b", "090793dafd862f59", "ea3f032d759e3784",
            "afe6f59d1c7829b7", "d9790fa0ce79e3f1", "a89e70309e350bd0", "97a459ba63dacc48", "4e89e89b13bf245f", "005f0e3f475ef5eb",
            "d29296c7ea9d38a6", "6a0904cdc4088e41", "8beb57aba0331501", "35f8f4f3e3886842", "4b625ddc446d7cb4", "2dc79b78d0de6321",
            "ad11abbf5107f232", "383e5cb214f863b1", "3068cad713a35255", "e848df9b94db1139", "4c374857d78511ec", "a3e4d0d40e9f74ed",
            "8ca2da688d7e1396", "819506c52758aaca", "a946acab0b9b26c3", "9ce89c0560be5e35", "0c482c41a55027ba", "3e14b051326bde82",
            "071c9868fb39ecbd", "31345050b49be1be", "b7ef50a2f714019d", "d6801f7b4827b649", "d98a87d84f9fe287", "9d10238edda2c320",
            "a717128282b4599d", "dd38110933afb54c", "c68421055b8054c9", "1f805162f7b3fe49", "e30cf244acf86898", "8adac9bbf268ba5f",
            "b3093dac58f2c689", "df32adbc3dd2dc21", "2d99538a3df0255d", "175cbde358243c04", "230a21adb68afd29", "1a991851691dec9a",
            "d2d3f35fd7193a0d", "41477bf4baaaa243", "1c6b6c3201ecefab", "dbe91213feca4345", "cb9c2861d6a93503", "80354859ccde145a",
            "681efb6c57094ab0", "297caaf8ad83b326", "4f14055b6340f661", "b6572e01c366e41e", "34a7efcc5c8694a5", "ee91f09720837220",
            "c474bb1ad5ad80a9", "2a793bc71aa98180", "c6203dae71302299", "684a0fb4e51899b0", "4abf95e6110fe0e7", "ac66f146d8b5606e",
            "b4fe4e8b3e47a1d0", "367e59f338524746", "d10f053a40ad4377", "e94c4e6c50729fec", "99d91fb1c46ab333", "875bc5d1c95dfcc4",
            "52d9fb77ddbcd6b0", "9ab87ec105b706b2", "4486a66ce30f481e", "af55519abb47d0e6", "1206414522a07276", "dbd83c789d6e5bc0",
            "0e54cacb5bda9f76", "0d9d140bf1307ad3", "b8f13458d6c8fd3c", "6066e2c8bc5c818c", "822a4bf5fd1f845c", "da2470f38e2571a1",
            "0f48f5348bd4eb6c", "bc6c2286e87852dd", "90bcd578634331c3", "c0a61b5efd697bd3", "47f87db53067cbc9", "868a93b733290d02",
            "060351548fc62095", "4c86063684b27d76", "2996a2d811660f76", "96f741e216665817", "81b20a9fce8132d8", "f48dd490ce23b51e",
            "b5f0e3e204e29ec9", "e07649f692d8ccf2", "391c976baf5a9577", "7c3198c3f2b3a755", "e1ebe9d7100af8ab", "d25784ffc5d120d5",
            "d7293df3a35993fb", "109e1ac8469be7d7", "f93b99922c1a29ca", "3e32b37872aed947", "46a7cd5c9aea6821", "9d4465c6703df96b",
            "11939e030b245e58", "7a99d1852179692b", "d7b1673e5dfc4943", "64c890a67d6da183", "afae50b72adaaea5", "423b949beb049b08",
            "6d04d8963187fe02", "48991a658d251315", "ef600aa6c853591f", "e8149a430db59aa8", "fd5130c1bddbb78f", "6c63d2dfa4f8be95",
            "e8b04dfa1ab0cd37", "050d9eb13dd9cd97", "b89b7524c8cc9dcb", "f97f5d60bcb42cc7", "871993c34ee9c797", "8b1a7fd47003cf1c",
            "71a2a66f17b3c598", "de15c0d1da6a43b1", "2ceda9177e2ac521", "c62fc2f0f1ed7474", "a37d4c0693f70d25", "a9f4f149d3e254bd",
            "119af2d0a78d9964", "50c0fe2765874eb5", "3ef7769ca4472848", "9c4e61f4d9034846", "d468ed6d0a0c3704", "63668930d70bdf86",
            "ed52763ebd978b8f", "1cbc395a2fa9e652", "c7d6dfe20c39655d", "1e5794913eca406b", "76e28b9f06c9407d", "acff8cc39e2af171",
            "b1e87b7757b7972c", "7b6348381657224a", "0198f548ecd4984e", "ef745f9fd9260f06", "c34ddb8bd5693d1a", "194615ecc91e4186",
            "8e7e50c6ce3d3e12", "24cbbcb58d0c0c39", "81461296476c9eba", "546eba2dc5654d64", "ce376a958c47bf9b", "704fb6b5b3bd3688",
            "f68f8256a65336be", "9f05fc3f982e04e3", "1746a7589a7e486a", "2fc513d598d52a98", "5f5be2e4672599f9", "113a6e8c41dd2a94",
            "825bd61d14bdb3ac", "f9243f0fc1cbb3de", "6a03afb36168c18c", "ada389edbe238ea2", "7e8c40f9e74bf4ee", "58d385abbbba865a",
            "0d08e95f4713bbb2", "1f33ad726f2ff344", "d4c4adaa1b70feb8", "6a176f7c0202411c", "d6abe4d94e82f456", "f0242e5f93b1a39b",
            "64ba89f1037153e7", "6bf79a72aaaf6548", "cabcc4c0b8791aef", "90361a7b4e1525b2", "6e80c1f808984cb6", "32d451b6e65b034c",
            "5f0d0367a04c00ca", "26bc02c29391a8b4", "8243a0df825b0965", "3efa460fbe901998", "3b237d09287f7466", "f489df0d927a9ff1",
            "d0d91de14730e8b0", "617a6e610610d8a4", "eb6c723f93efd1e1", "076dea9f9e777eaa", "c6cae8ff67d24eb0", "2edceb3021b11bc3",
            "5cc4a8e9c69cee36", "fde8024a6fb636c3", "a86261628793ed86", "b0c51978e7568eb2", "e421c0b6b7d4c1a1", "7a3bed575d9ea7ad",
            "07fc0a32df5e1a50", "32bcafa3486297c7", "747bd1b0966ae018", "1ebe401766e07a78", "a0bdd3481aac9cb2", "b07067f9bb95b135",
            "026d73f2d3498286", "1ad0b3ef1e9cdab5", "6f325a388b6f6599", "f1f4504da0bb626d", "64dfac15adb02cce", "4b4cbaecd0b073cb",
            "25dae8466c004afe", "a116799c85235670", "25159b1f88da1534", "ba8e1976c1244939", "1176009f7fa54bc2", "a511e3d0c4dcdcb3",
            "03dbebcc3d306a9b", "c1bda4af63687d5f", "149a5b8750fce080", "e27a80f9696bbe7c", "5d91cde3ef7558bb", "ec46fffb8400914b",
            "5c5ce89bf43e6d45", "225958204853fd65", "1e0f175a285bdc48", "61f166a818ba189a", "f2b41042ccb876a8", "bd63bf232e75dc5f",
            "17df50b51a4b63ce", "41a4fa9bcfe2cfcb", "19df942cb8ff38f2", "4d3fbbbe956b4e82", "9f413dff045acba6", "60b2332e9cc1c20d",
            "3359fc4ca523ac9a", "c08d852afd5affdf", "3de52bf5fe7830cd", "19eaa4b86a0972ca", "8fbd318ef135fea6", "d414ff6d3e1fe68a",
            "79cbfceccb0df9b7", "4bfd03c0b6f34876", "71d6e44c652a4b1e", "3c199bbb08a2ef5b", "08f03e02b70ae42c", "0b30dd61f7d7ba97",
            "ae58075503cb0646", "f405e1b440ade8b4", "96d1750309202dc1", "8a2799df1e41655e", "cb1da2193a7aedfb", "d4ed698bb62849b3",
            "23cfdd67cc4fcc75", "f9da16db0fd9a81c", "3d0778dd038d5a49", "40beb0e897b0b095", "c0d6214e660a2fb7", "fb3873af80faa009",
            "7002f2e345eac15c", "0f5fb3cae74680d6", "f22a628ea2f8692a", "58af65446c39eb33", "2ac5f69e71232518", "596214eaf9f9f072",
            "5c4807763efee9ea", "52fab2906553cb7e", "6daa138fed5160fb", "f8a19ee5ae02ea06", "4477d84539a28fc7", "802a91c9302fad76",
            "711278cd91fdb6f6", "b21480e6b956bfab", "2ce8b0e554dc8c77", "5917effb45fad126", "75e525efd6e21b23", "4cc9aef5de2a1f2d",
            "eee7ae8d18e94a44", "2d9afa7c7eface31", "4ff1967ad5a6c6bf", "59a1e7a6ae762198", "1adc6836c0ea2ebf", "be6c260e187db5a9",
            "60d5e638f63e32ed", "14d7e490481ecb16", "38d0c5701060bb22", "b2ea53a5aa019e66", "c12618583069714f", "7f0c38d565a45965",
            "f1d995a7219e783f", "e061273351f30ca5", "fe60b66ed8c88e41", "ec5d357d111e9495", "f6783a56a8eb732b", "19145894e30a01be",
            "bc83e05e42ede1ec", "a8048cd7c53ca84e", "e53a02a1d37442d0", "bf0a0dcdeb7de74b", "c837193b2d2930ff", "b79e18262364e1ba",
            "3a3be674d2e91b17", "8bf0908316548218", "34ca9f96bdc899c3", "38105f831817697d", "cd497253c7c2efe8", "53b37339397ba19b",
            "5410395ba3686160", "4e3df9315dfaa35b", "657b70faf89b5e5b", "00f945b778435b64", "5af3e174b0220f7a", "51323e90b2af48cb",
            "d626b6cdbb083f68", "80ad92cb22cb19a2", "e844a10bde178a92", "07aa72c80030fc0b", "372047e9a486cb30", "a4418a4b28189749",
            "11c3a73f817af943", "f36828e791448172", "08888be095689ec9", "eb9ba29a9ed38a52", "d62913084609ce73", "ac5039cc221bbdc5",
            "d37a00138a6054fd", "a7a0f554047fc5f9", "545b701fe2271b93", "220c3fac2f9afc8f", "23ebce503e2df526", "b0db8d66d7d41145",
            "f936637661da97f9", "afd50d7f8cd6df6f", "cf50eaca4e57f0ee", "16802fd0214333a4", "dec609a5081695b1", "1cca586b1e698980",
            "e74e88e99ab432e7", "b6959769b2f496e6", "bbc7463f4a2ac4a9", "f67aed360f54793f", "9f1cbfb8dbb815f7", "5aee22d9d5216460",
            "63f3c33648d75836", "2314c054e702b43d", "57b6558f0fe9b60a", "e15808e5f761a687", "66df047e578c4137", "8afd448083c85a54",
            "c7977158b08b3f5c", "d6c9f61da5c51c6c", "c8f789c276b0fc6d", "43a18e4e769b1806", "a5445925382614e5", "caaf9eb50dd5c3f0",
            "19c5ed6c8733961b", "6a703350bfb83e31", "ddf7a2554813c496", "558cac7811760e68", "d98330393746d90b", "dfa3c6571d96c475",
            "4011ca6c02db21b1", "cbd0a4ccc93b3e2f", "d1314dbc96ca3662", "413be0ec689f240a", "015a5e7ae08a9d69", "8597770cdfd796d0",
            "ad459c0cb5ecaa6a", "3524ae319d51d802", "0be25482df2dc2fa", "b04aa9c396616f5a", "74df22dfc3a16489", "f648f19e80d396f5",
            "bdbff13e952276dd", "a53ac44ffd673519", "cdee2938e4030362", "09210f6d37e842a4", "519975358cdbaf1c", "aceada65e2970655",
            "5a7c043581e11eeb", "17c784802d34d080", "71d451aa7a5b092e", "28b15d6359d6202a", "ba682fef242e8217", "88c224c377aeeda5",
            "a0cb78e0e907bb23", "eab44adbd46776cc", "745ece0904bfd8f0", "7d8d1b7b7a1de0e4", "798d077b0d044ef4", "2ce68f7adf39e80d",
            "d41262b6c7b6b520", "1028e62cadb77bc6", "db727ca3b010a5ef", "1090e36571df6cc1", "c9f0c2a762bb1eb9", "039db9e822e1d464",
            "527ff5dd65d7609a", "4a74ba1211299eaf", "8ab4f0a8ef0f426f", "7664e040417eecad", "1eefea2e6c23d849", "8c17e9497bff9954",
            "da92a1e799b98347", "02cf6810a349854b", "3f66ccb24f7eec3c", "60225bca1f7914a8", "02fdf30334e5227e", "41bace9a53e0dba9",
            "9e77e5c2ad6aa79e", "eb175be0506149ad", "13b662591ad66dc1", "8e91744ac2fa50d8", "7afb851f51ea6716", "bce23f5fb49b63ed",
            "ad021afda6ebe037", "d356cc8f8d03ef99", "3154a0d9548f0e6d", "bf449ae54afea1ee", "4b5c674dd7fc9411", "fd7e0cc3e73b1c5f",
            "dc9937f89b913db1", "ff63cd6f6e64f48b", "732be1326de822b8", "d0e44f20f8b1871a", "3e98d359c2c14690", "31273c5ca1850b38",
            "88037915676532c8", "0c2bdc6fa6ccf7ec", "448c9aaeffb09d1b", "043ae770f8075688", "75c57caafb322e49", "8551c271ae4caa3e",
            "6609551ab75cc524", "23bf1295a75ba5de", "4e1be0139fbb76bd", "3d14108eb7fc4859", "b1729ed0c5865489", "69c7d654cf2745f5",
            "3ef5cf74811bcdab", "188b8482262729aa", "af6fa6fa8c8f33dc", "825afa12ffb5b7ab", "d40200e941590108", "fcf90a5bacdffc33",
            "e37a45aaecaef63c", "ac5162476b1297a2", "3e084ef1a359eac7", "a561f86f0bc9515f", "215ef078f1ac390c", "a2b4a62821cdf083",
            "a77c67d1567d8acb", "3b516522a9e3a890", "07cbbca362c0cc33", "469092f936b8f0e7", "dee7b6cbd523b753", "af9fcae9ed0a229a",
            "b1ba02ca4296b143", "eeb62096a3931568", "07cf28fadd22c98d", "0f8ac15f6491750d", "cb498c6487e67b26", "58f2124a6ffc98fd",
            "7b6b7d8f14fb0164", "64d9218a3b35e03d", "a115ec1e11dcc7ae", "9e0b5df6f979528f", "d82f6e4f3a1dfb9a", "86ffb4b74a4cad4f",
            "08070074b3a84597", "ed2ffc9a9284d947", "17025fcf16eb2ab4", "859285495c2d6d56", "90cbd1325e8aa599", "e5b4b87c71f04e67",
            "94a1745bf4281f45", "8f904a3a9f094114", "83d1c5b99cb7199b", "34250379554cb3f4", "6901dd3b7eafa18c", "72a48d0ba2a699b2",
            "47c130ab5e2e3ec2", "f343e300f804d5de", "afe375284732cf3b", "05b2a36b520dd782", "8f1a47305b95ee4f", "9e0990cab4a060af",
            "0c8712ac5a4ad3b6", "0cfcc0a75c496869", "25cda61aaa92dfe6", "9090096a7df2da6f", "aa7ff65d480c1a7d", "40257d1897762165",
            "a5eabff2db017b10", "0528deb864e59610", "3c515179604bd08f", "0a3f8c5b955137f9", "945d18ae38682ce5", "84c11799fccbbc63",
            "86b546f812d05a9c", "47a74d5698f9c2fc", "179a091349056c75", "008b41b50c0a8059", "28923721a7c8bef4", "af902ab3719bd720",
            "260852307ecaa3b1", "9c84aa0a212f4724", "e816f3fea2979d6b", "8673bb6497944df4", "0e50b71b902bda5a", "435e0891d2271313",
            "0fd518e6e017805f", "c3b398d09ec5a11c", "aef118e343310c50", "e0f1aab458ad1acb", "c13c3da02975a8b9", "fb3b8c246be32952",
            "b516b664082cca1b", "7679f01f18a0556c", "a1b684b53ed359c4", "0cb158bd8a1224c7", "bc32924455140c96", "e1bcfc9c922920b5",
            "11e6241284e8f281", "db24f0813cc30a1f", "535c09e7ed92c0f9", "b7a9ff8b4d8682d9", "e5c7d54624a84488", "6c0b1779591e1ff4",
            "c9e95b0652387b66", "d5d44f8a36b250d8", "7294475588fca288", "c06b4a9c994caba6", "13cc93081abf767e", "15040a00902db678",
            "757c0acfb4cdcf16", "45c690c364c81a63", "6ca031511b6137a8", "62d9a240590318d1", "888ef8c5e5c17793", "55409af1f9d44c7a",
            "56f401306e42cfe5", "6d3abd3961d33b87", "c641e0336bd92dba", "7134d900939eb1f5", "91cabd61f5572ec0", "2d15663d85701967",
            "8095b9cf7eaa43c3", "e6e283683ee91264", "92890eb6c3f2da88", "a2bc1ab14437657a", "54d7cc0d867706d4", "823f54af4a978bcb",
            "13c6951352824054", "e8791e3c1590bd9c", "3d12e87485bd743b", "f028adbdf0348863", "6095b699a46e97c9", "bf4a95c909e0dd3c",
            "abca95ca2ac03f4a", "14aaecf35b68c3ff", "216ff48b1677070e", "a87870bb677fa087", "95e9ebe5ac1b7e9f", "58f4ccc6cb047261",
            "0ac035c5d5fe7043", "159b5b390c1dc1ed", "ffe818399c39b16d", "8f904e2f5bba7ebc", "66af5164d404e1e5", "01b0a9d5d77509f5",
            "e7af3ed98b45c963", "fc52adf6d722af9a", "78b5c23a18db45f1", "281778d97e99b10c", "e63d4c0079bd21c8", "e207b2beaf107ed1",
            "20cb1d273ca02885", "5ff90b3e76644c46", "63a9b4188731f2de", "006d27c5045df487", "14954575b850e680", "edf60cda5ff1cbf6",
            "55bff6fee40140c5", "85de09af9ae68263", "7c0e5a036b2c3ff6", "79c4cf70a4260c19", "72ce3765d7a70107", "c3d4190ae1d2c0c2",
            "675ba7e67c57e325", "f2f9432ce45285a4", "ef5f8006a9d22d72", "7bce8c75a23cb006", "36ec49a73c7e070f", "c395aab0b8ad4997",
            "37cf429e09537603", "e1c1033c707d7667", "8ae45aede691af8d", "9a5a4d787b615239", "076b5c1eea4791d9", "6499ede5c2f340fd",
            "61b31923f5c57b1d", "937db2f18e09a82c", "68925410df68835a", "f8006e434be508a5", "e21a3ec116dfe337", "9785a943b1e7fee2",
            "a08b0dc085452b4e", "04a8571f8a440e62", "9cfe1f592870aa74", "b7aaf71053fe8f05", "ffc2a933b73423c4", "522182d8e78b5a95",
            "afc71f80ba471e18", "6ec948d3933e3623", "56be084da6db7718", "85378dd3c2019f7b", "8a19ecda710ee4b3", "c849aeccb28e1027",
            "d0c5f28a2e8809fd", "40f3f527fd37e53e", "dbcff381286918f9", "b47eea00ed32aa5f", "85a2ccda0cc08ae2", "8dc1898e3bac18a0",
            "798e05da4b500496", "ba98a3af8037bebc", "b7e40e2d7c86f80b", "5ca9fe9bcf0e3e93", "97f2cd630815dd9c", "026cac138b165961",
            "ca680f5240c341df", "9bd5e4f125371d33", "ecd17eb50bec21cd", "91a4f34fdd132410", "600456e0c2227abb", "d83640104d877f9c",
            "4fc6acc87dcb8140", "8ab5fcd1b706708b", "123a81dab89cbc5a", "905f223cfb4f9af4", "fd6cf3f271e62bb5", "6282e19c9792d09e",
            "339179e590992a79", "5379104476001de4", "494cd1ed99a54262", "17a8fb406026ce73", "ea0e587708bf745d", "73873d2803c7b631",
            "a7e08fbf5ee45600", "8436c23795862ed9", "d78881fff872d7c9", "322af1d39fb51a40", "265a5f485ea10bea", "2ede24664efd78a2",
            "0ed8263c9b3d778b", "64f01d127f8f4328", "dddb8042190c35bd", "4261de8f96d91695", "a9a3d17040a92e58", "d8052a244c14eeeb",
            "f7b6b6ceed8e2e26", "6f6161537f861b67", "dd5dea7771c5683e", "7420220cd556bdf3", "775fd15584226a17", "02b93df548dc386b",
            "f7f17f85f7e61ff2", "5d0daa021b162ad5", "a88ae2390a571a27", "d5d4f1a4bd9a96a2", "7fe14ca240fa48f2", "fc76ff056184184b",
            "29bf6a6b7ef6bf8f", "eacb4a330d75d67f", "a49ec7a2faf9fe03", "7a3beae381e516ad", "7b7758b9fca97ce5", "5ee0b404a1f9d28a",
            "0a09e3c64e887a2d", "978f1b9a5ea8c1ee", "66f231b3c3a79c94", "3b7124e16dd96246", "37532b76d80e6b8c", "bf64e175701a6b3c",
            "25dcd1001f7eeffd", "a8603710aea91b39", "68b2f9f2bddd3ee0", "63b342ece4174bcc", "e33cdab7a74e2e76", "d0b1e7f5344fde70",
            "203952ee27c4d106", "812082b657172834", "f602ebb550e1fa30", "73917a91114577ec", "e2b681c88b42bb33", "b01d46198f2aa288",
            "8d4baab255a2691f", "d101d901134649d3", "cf588966ff6d1fa8", "1b8dca1abdc7e248", "009636bba12ccbb1", "4ec6e668009e34fd",
            "d67fc8fb6540110c", "11770168be40722a", "b8335d30984a3884", "f137af1aaf47d99a", "8effcaf70a0bcccf", "72dc5c96cc6c709d",
            "cd3dbe13d22b690a", "e33bf99a5d6a1142", "45efd2f68d02f498", "d7b3a00ab06d13b6", "a5048d1e54833cbc", "a5c214c3465b1c92",
            "cae6acbb1db0e4d2", "54220a09f9d9d515", "daa1c91b827414b1", "344ff67fceea494c", "300fb6b402e6c2f1", "c070edb4585e4b2e",
            "9b1301b963aa1b63", "602d696c16f552ab", "8c03a8de650546bd", "e89aa48f36998981", "68f208c3430fea01", "0e189b977fd0c98b",
            "c99c54b3e9428dea", "37af26633edfcbc5", "1ce4f37c7b378429", "e4295035f15958c8", "51e7ab24710acae8", "97a5b1cdea3ad73d",
            "35487794cb26864f", "b2a96f86d474b7b9", "a95d11c3e536d1e5", "b390dc3f26b86e1a", "6fee14fb75fb3e22", "7572ba08cfc7f8e7",
            "2ab7ec6c1688cbc9", "031e678ae9324bc0", "6cbf3d3bf5825818", "e3528620f8bc0fa0", "e1b9f00d85708897", "5eb14063053333ea",
            "b41706160abdc066", "511807a63e66965f", "ab3ca129f3eec184", "78a8a9562bad335f", "c3bf28e33c3f963c", "3e6fd9f03b4281c6",
            "82657cdb5887b5fb", "6ba165d408da27e7", "8dc0b0544b4b2006", "f8c47f4f24f58177", "72813fe90b601e85", "69f3cdff3f323b1d",
            "1c07d02993d2e47f", "380f2f557ac33f39", "613f78c6a3dac017", "d1a8988adcf7e61d", "8e669293dc9f2c67", "c46cbf70a6b50c04",
            "b2bd2d489c3f7d85", "013b6e92b962e024", "3da245d4ef0f1e91", "1fb83f4c0f4fdb52", "f46b6eccd8bfc75b", "cb03a71c1ced8112",
            "33dd76032ed500bf", "f7e1251232f2c2af", "d73965cf2ee56335", "9def865da9ecb069", "8162060ae95ab266", "7f1e4c469d696f7d",
            "eea1f4f4e60fe51a", "ece61e83d2acf9ef", "468f064f59cad9df", "1e9d2968cafb312e", "ebace98b6b9fa73a", "b3edb3c030f81fd1",
            "1dd547b19ec35684", "1d298781765f8f50", "2ad7b21821e15888", "bb17240002d6d0a2", "254f056416d68c31", "f2d3d3d038ef5d64",
            "34d1b60cb0cec74b", "e789d0d239f37dc1", "0cb52557aab883b4", "77fb6cffe2a62458", "72f247a06aec326c", "4e0567c4508de83c",
            "49f1bc6d9832def3", "ee1ca3e9f763e6f2", "c7e80d769cf884d8", "411a2471c10d9785", "b37034608433cb91", "faba52882f31aa10",
            "349ceece490f1a3a", "1667ca1a785cc05e", "12d3cbdd7e10313d", "3c8340ca108db702", "77fb4009dd5060f9", "d59b654bf8b30530",
            "e629dcba99b851d1", "e02a4e81115bb8b7", "8a01ed0bc45b67c8", "9b041b2fb452c111", "8d8a4d4d177c6dbb", "16625cef44745325",
            "36ae187dd1f6e65b", "77b0ba608a1b7d39", "b856e54542479924", "e67b5db9557125a3", "d7791be04fef18eb", "010d8d01496ef4ba",
            "84176aa5fa2b81ea", "699e59df23f7ebbf", "bc8b21adb2c40740", "e534a8b168a18cd0", "050532dfb36fea90", "e87862c57ae7d3b5",
            "a2f6bdd1ce39f7c0", "c006452afa4047a1", "658efa943e7b8bfb", "34662198f6e643bd", "7db699a9a96cf300", "fec4caa8b7cb5d01",
            "84b604d08648befc", "520df1a452fbc037", "6fd1e7fb07f6fdce", "dcf01003b5a6b8d3", "e7551917fd66b4e8", "05b198abacaf1c34",
            "56efc982e5f3e2e5", "65393fdc2f2d8f69", "95106217240483b1", "f0c1a1d47f92cda0", "9e381fb9243588a8", "69d5247d83610908",
            "d5fd263d88149d49", "05beaaf6daf0ddd0", "391b81a4e8045371", "8a6589bd1bfce106", "9cc0a813facdbf1c", "fd9d230c4c9fa244",
            "129d5ad7150853cd", "618bf9ee5b7c2e01", "7ce948de11c97b80", "7bc16ef48bae5c56", "698d33e3a27e6185", "7901b2b99955d2bf",
            "90257b8aa8e8a4e9", "b322635664e86588", "675406556c9e1178", "0b519fe721234880", "2c9dfcf06d1ec87f", "f48366b4f5c7fc79",
            "686e318169303abf", "f7dc85d4e46367f8", "7b7fb035fac326f3", "239d3a99ff3dad69", "440e51d26a78030f", "48ccf44be0d18fca",
            "dbb99636864c3f4a", "d7d9f32994ac9ee8", "7322d9c02b223bf7", "fbd87a8606ddfbf0", "263cf923ec1ed380", "8fc4e6f9f31d917b",
            "a9b407111a755ee8", "a80a220d0448997a", "db131bcaf9816dcf", "0de3738459d56712", "2719e748e03d5d26", "ee9b1e7e5258820e",
            "6aa5c6ad5424beb7", "818f20ff2bdab104", "fab8b5ad29e41a12", "b9a249b487e03164", "fe24ba8f90bad16d", "39d9386a059bfdb2",
            "391292aa4d4b861d", "e9314f1a2fa10f27", "200c12e37044a499", "f92e19c5411f2f68", "956766658c58fb9e", "73394d050ff39c11",
            "1f11b4c0e0f1cec7", "d504db7b9c1a35e9", "bc56f38b8023b19e", "5ed44786e00390d3", "3846527c3a079255", "f2b03e57a5d7821a",
            "e33d373395d709a5", "20dfb2a3490377aa", "836117b7a6ee0d25", "1979463a622d8a5e", "e018953d071a3280", "7163165f5b81deb7",
            "d8e272df951d9f67", "d14074202fa495df", "0559b78d9de74645", "beadede49959d283", "0185593bc29c8526", "b42ab30ed48b3551",
            "d75ed10f376ac91f", "ca6d1a506444025d", "58fa2da5f12afbe5", "6b21273bc2bda432", "ae6f570c89863e64", "d9a25f4087db8fff",
            "f2d82400640e8ce4", "5e2f290b7c939e12", "e12f61809887ee51", "8859351c315e8f57", "e17b448a9bf61783", "0169f0efc54cc45c",
            "d3e7cdcfea34fecf", "6a187ad2c4ff77ab", "dc8d9a6d02ffe6fa", "6921468e0aa04981", "55968dadddd264c7", "e4659e64675416d1",
            "1ea5003b0a0d29f0", "803db5cc68cc99e1", "51441abdb9bb3305", "fe025a2c15b7a7f7", "dc0e770db3b1adf3", "6e00d4e24a8ab9ca",
            "a6a2cc0ac5f5fe60", "05143f4492e4d4b4", "b4b23f3d6c432202", "6190dcda7e993c7c", "5c0d8776c0dd964b", "0f74a16539b61062",
            "f5f9f71b787a8f33", "41cce69e87a23bd1", "e1ba8885b9a2793b", "e0c4533ca0c498c9", "4bf7b38823e5a3f9", "bef13a59ae095bc5",
            "a31003d0f1b2faad", "00cfea16ae7fbf50", "66292779b04fdbca", "0d1a0e0e469c4b9d", "3edc2e716e1f0fcd", "05e1a48e83768d32",
            "13e84d15c1c3aeff", "f77650fac4790eab", "8095d2e1b1e40a57", "ece1887194f3856b", "1c61aae781c792d8", "ccbcc968f1235fbc",
            "9f5936177ff5f83d", "dc7c8eb1ab00800c", "e6fd0638fd6427ab", "e85215f038afdd58", "78e587db927f05af", "10c76919f6e44e15",
            "5cc699f803b433aa", "6c846cc327c902dd", "cb368e139dc08ca3", "19f99ea8fc348660", "dca3f02737d53b96", "10a09108fd7e83c0",
            "8998d7efc703dd13", "6bbc3bc9524ddfa2", "3f821ca6386230c5", "495e6517e0e3fb8e", "10199571cb27adf6", "23030a63ed578de6",
            "832ff12d45fb7a24", "101774a19a60f744", "91ad4830ad0b3c7f", "e4642ee4c55f0132", "b28c87531b5ae7e7", "c18d1d617561c680",
            "c23d21b493085c09", "9932edf661df8d4f", "d8b52f8cdbfec780", "8e45aefbdd0bb707", "bc8237c59e08ab88", "0da90f2be0abbc10",
            "5a34f9556c9c801d", "8d3b772c2baa1306", "c0a9206e53c02d4e", "89ac9951b06b74e9", "be8c869c3b7fea91", "e18ab300c5a192ed",
            "973f5f54fb2e9f94", "d49fc614e20a1171", "6f86dfb0c55960ae", "95febdf095111f1a", "201134eca4c5ddc9", "b70f231dc3c9f796",
            "6923207503cf4922", "3b697e937d8bed98", "29c1f4ff68aac07d", "50c8e797002b5028", "9d8112b8f9bce648", "1eb8b88b0b596ed7",
            "3dbc6fe758f30689", "e463e7bea14fe7aa", "62e46991d3615155", "eb8ad1e74965d1d0", "7031cbe7f7e2defe", "a6934750eb02c297",
            "6db49e71f75c184b", "0e2dc4de2bed2837", "4e1441a72ff1c7ea", "4982d12fad79b5e1", "c38e0b19fbc890eb", "d88466acf3c81cda",
            "1343581e29e30f07", "eb13cea398b5cdfe", "802046dbed7a5459", "f46e5e231cf92eb3", "e5c1b4a2cb3d4fd4", "10057817d0c528b8",
            "419c89754e5d1efd", "1aa9d2971097bf97", "a30b055979aceb09", "8743ef85c05a9d8e", "c59e09b18e266f1d", "efd1cd8e8be1609d",
            "51ab3d36a901f841", "abd0f6565ad5b12a", "aa1bea06395dddd9", "80e915ff50e4e4c4", "f8c1bb8f0b13320d", "7b5b9a150c5cc1a3",
            "0f268e5b9e7d91f5", "84eeecf440934b4a", "115ec132d66e078d", "a9a72af040c0c60f", "8e6f752aaa2d4602", "46e82744af052bf3",
            "9c06bb13511518e5", "2818bf4bfe74e50d", "3d6f4652a24b1943", "5f792ae2f199f4ed", "faed988ea81f68d0", "354cf0dd3d520044",
            "27b373149051ca61", "e0ebf87a4b72fa8a", "65560d5853257694", "86260f5695e098ee", "2c0c10d878c4c470", "cff073361783eb73",
            "d2dadca0a6952692", "44692f969a5bff99", "4a5df5edd6f49f42", "2c3b4f7c1854e4db", "b8f3ae522151ae8f", "8d2b60018c96df7c",
            "606c1d746e32ff1c", "9f1b75aabefe1f07", "0b38e84a4458d62b", "e4467131fb6f25d1", "b45b171a125357a6", "043c34bdc97b35af",
            "1651af16b5c30838", "a908df043f887698", "ff866b8e66b5d777", "1212a07586cd2964", "10cd841dab4ab71e", "f32e452b46251ca0",
            "2ad31fc6321d5015", "ab8fca559d43a981", "88f8ad42b50337f6", "9b93e5309f262408", "8998b1f27f8cbfa9", "cbd1f8f9f1418cbc",
            "31c89b3c1c84db1d", "d71a328df9786206", "7c5de436b5bb2972", "9157c49e3506379a", "3f01eef20c8de15f", "d8841cfe170af8b7",
            "c484676e66f590f4", "c5dd9fe30861a584", "e3af715f1a772f52", "edc369536ee86f5d", "365622ee44c0fca8", "b7f498b9ca6eec4e",
            "ab5eaaf18eb3dae1", "8f45007df6bee31f", "86df2a0274df000c", "a784f082a3c2d2e7", "34c24858f571af5d", "98561f18bfc395f9",
            "c1cbfea452f6775c", "8e33bf57dd696a4e", "0223b45756c863f3", "1e09467ea6e3cf7d", "afa1e7a224dcd8f9", "a0901b1b505e0f8a",
            "71f9c29ac10ebd5a", "a9a9e246a1ccdc58", "8cc7ecd8a3017247", "3f46b7867ed52340", "de3be4d668368b54", "2ccbfbb0075d8476",
            "17f4c854d9e0ff61", "533360b528ab38e9", "91bdf58d9ea35c35", "4ac6c970698be020", "931b1b4049b6f165", "dbb60fab908acc5a",
            "8f250d580049bb94", "536be1d39fe60a06", "259365b2bcab4d08", "5ecaaf509b875874", "3685a7c8d5e65344", "bdf389237cfa4383",
            "d9a7ce481611050b", "5e87308cc9463940", "c4a515bf864dde0e", "b34aaee6fe0ec626", "c61dbba228a2a16c", "d6a426e7a7d5e889",
            "5c3a24fa5871f101", "07d8ca86b381db2f", "7c2a88fc57d26a61", "94d4bf5d906b3ef8", "f3bc1f1e6da6db63", "0eada98136dfda42",
            "64c42481c69acf33", "23d48d448d87393d", "432926aee17e5b06", "033d1b0ea8aba57f", "e3b8d8e55228d7b1", "76ca511a6942ce11",
            "622cd3163b632c95", "37999ee506404085", "a428bda9caf5dfb1", "c3f32376bbaacd3e", "7c75c20390a08c4e", "b60fdb546b67f975",
            "b96b191a451c7211", "3a3e5fb4f115e0a6", "5302f27a92ee5dfa", "8cfb91cefba3122b", "0b4c17723fb1e08c", "5bd63f333427d45c",
            "92152c99fbd71531", "1dd4c07cf6ec0159", "0af17882a4a34eb4", "d346e75698c6ab9b", "5f1a284adf9940ea", "008999a242e61dc9",
            "3641248107300c02", "ada661937138cf00", "50a4522293f2ea5c", "a56576f5dc2b9e4e", "1f5f7f59e60a6a9a", "a61633076220949b",
            "5d6ed4e891c7513f", "f5b167e9aeab2c12", "9c6a5120e16fdb09", "bb3c415f953b5f7f", "3e36306e4b495c29", "8f3d80f42fe085b8",
            "9f82175c29567ae0", "25408e582baac433", "a910fb41d4f75a67", "8d4c0d5f351e327f", "cfe2c8f270fab67d", "51483f3a024d55be",
            "419886bc939b1fd3", "0342a357f2ef493b", "6800b5cae8513c7e", "b777c8152edb1aef", "4d9554482f885df4", "da79a3316b4af078",
            "21566bf778dd9586", "ef2f398e25ffa102", "5230f6b6855220db", "48550b4b5a914056", "d94461226de60844", "793b31107d608af8",
            "174c8ac03fdc9ea7", "cb3ee71985d4c9c3", "674b1673658fdfcd", "8da4c42f5c4b9c25", "d1410c3ba5eb3699", "637268b23c728e0b",
            "1df3dbf1a83d9606", "c8e4d2de72ac14ca", "79ea53199c691882", "c8c4c703095e3a18", "f968225d9ed07a82", "6f7acca9e8955950",
            "2f87c8e9a3e06a67", "b1e1b3969051545b", "5cd228f1f38365eb", "927342eef73fb285", "0deeea609032cbb7", "350f2cffd04e08ba",
            "ddfcd8768be2d5ee", "79baf8813c3cde20", "471eb3457941a565", "4e5e311317f3d9b5", "4f4f9c5d84a0d148", "36473ab3d7d1bb4b",
            "b3f93c018ff93eb5", "279ba7647592623d", "b56893bb2de80037", "29c61cc7a9744db6", "16def92d8bb82587", "622f4427d2441bce",
            "da88b8cd8c341632", "01c3fffd0d105799", "f7d827e9bea42435", "dcb579cf6144cf1a", "882774d6e62c09bf", "7c21f83da027cca0",
            "3a9b532ba6d94270", "29057c71befaf5cb", "3008b3427c62f856", "367c2e67f3961fc1", "91f9506538156ea6", "fbb20d8bef67a60a",
            "ad494fe0c2a71c3b", "696dfd041bb1076f", "ea73dae831caf679", "62f34ade1cb505dd", "51de7a5c28fee763", "51191a1ae595eba3",
            "cacba0927ec59ebe", "7eff4b889a412da0", "b82fc9abbfd6df2d", "2154ff7b713b8254", "8b440f77def1de57", "80c571ea545c4308",
            "893f3036764e2bd9", "fd606617c2f28714", "079dfeb664b7a4a3", "3a4e9e1068805860", "f1a21295554afeb1", "663b64e403c2529c",
            "e57d402d6748f230", "ada35b429c34c775", "6fb81e324f99e65f", "2a514f74a7eb6713", "96cf402b379288e5", "dd887bf2bc16c909",
            "07513010d50b7ada", "772fb396653b99ac", "72c418cae26dc0df", "7b6b50f53c193720", "94c2bd2eea2502eb", "4744078dc12201aa",
            "43333de4f18de125", "683bdd9c51d7654b", "ad80c0423430b475", "e3d32807468efc4f", "146bf8ecc174191a", "931e57ec58132e2e",
            "d0daa1bf9405afbc", "05c8dafb5b48c003", "5b955bbcfc038160", "d0dc9fc140b21062", "e6df0e12a62aba3d", "bbfe985217a5e14a",
            "c78d383e78299cd4", "f7106c52d011d14f", "1a50ee1b58660bfc", "ff640dbff54110da", "c0ffa21fe71d89af", "7803e976e7739cbb",
            "5c6e0b1cc8118ae1", "4a7086e3e449c4fc", "ebca30f9267ee5ea", "cd9ca4e14d376484", "a27c1ee34350365a", "ef3f8e8dd0688fb6",
            "bb60525e2a118ad2", "07d8b228fc72f043", "18e51eb155788efd", "d79be7bb8b1601e3", "96ba91a409d6b641", "7b0695cd2c9b8a68",
            "e50cf1be05d826c7", "23270f93b115b523", "29628e8923b26fd2", "7802826ea5bfeb80", "f44460b709bdf426", "a519de65a83a7df5",
            "def419f9c720afb1", "7860111e900f84ae", "54d1750f75cd47f0", "2ee181f35744b143", "f60b9d949d930521", "0bb1c526f368da86",
            "b39a16041841b5f2", "03434db2182eb48a", "6dd27ff0fa74aff6", "47160c97a659e640", "495072b7936712ea", "8b3c7a3269166659",
            "4347c01aa349b61a", "b11ab2bafaab0af7", "20acb0e9d44218a1", "027425e77846a21f", "bffa4974c3ee5967", "fdaab7a28dad547e",
            "ec765bb6ed2ad3b4", "af677566ffe0de31", "bedc4c57c0d703ae", "f3299aea04494c22", "afb0183afcfd79fa", "98b6ed74273a19b5",
            "e5e1e229d8e905e1", "2eea82e2d5c44b7f", "1da0360dc32943c1", "2ca9db38b9c4938d", "f6a38b364891821e", "eba3fce28d1ff399",
            "89ee1e16f71b4491", "34ab672623315c05", "76146a8036739020", "d61d01298cb49877", "a1e3d797096b919b", "eef3288aca9aed28",
            "bdb7cff253469086", "7640bd27162800e1", "34cd006ece5edcbf", "7fa2cb382a76e020", "eb71e274c79c0660", "77281a7dec9a5ea2",
            "5b273a3bc1e59da2", "e6c659c740241f33", "430d4986786ac68b", "8b70dd738eb8a476", "6fffbb687d33586e", "85ef38fefd43eeee",
            "4b4a819dbc31cc70", "0e2f3805306d8c07", "58adf22d5e68617d", "89343f05ee6561c3", "dc32dfbf1845f546", "b72a4c0916573bc3",
            "76b2678725e5f5e2", "e06f78bc4889d899", "cb40a7f03caa2d3a", "6be68c06205ff7e1", "846c80a0baea2e83", "9788513761a73dbc",
            "6983ed205cf0071b", "37413118d0001bdf", "edaf795430b88e78", "e4110d65ccdc6ad2", "4011308aac08d52b", "900873b4213c41fa",
            "be2b33c11cf0c183", "a8447d7fd40bd916", "06a2a57edd2fb9bf", "882eae435f861013", "576204403b486765", "a0bcc07ef8df8265",
            "1b80036b1aff7da5", "4a1742daac6fdd75", "f00cd58035908e7d", "bafb068c183c43e8", "9db7f4a21958c39a", "025c6dc0668b6152",
            "a5eaf003e68b3068", "0d1d8fe52fe79002", "5096ea6b73bb3e44", "dc4c415e3a9801c9", "84c7173005fcefd5", "2f7873dfafcb6786",
            "c57fabe0b69f714a", "0441e4d97a92d637", "25321f5d3faffe09", "f8fd52e81787d18c", "a2896be470eec196", "f70587969a4b3fec",
            "747b220daceb1c3c", "c1559f412daedb0d", "5a9f3c040a65af1a", "6bfa0d68f2274042", "facd2e2c3ebf862a", "13bd5902a27c6311",
            "ad1216bdc90a1607", "f08b45a9fece5dfc", "fddfcec0934a80a9", "33fb5709e977d34e", "6fb845323c258fdd", "28e71aba02cfc0d9",
            "92384664252cf64f", "81232af0253782d8", "08829f2948b16aa9", "6876ea9cd1276ec4", "f0cc655f87023bbf", "0a0690fe71e51c39",
            "6c1ee366b510e04c", "8f474c1902c26eaf", "13e035e041e9c7d0", "22eb0e48d4ab5c0e", "baedfeb9aca30998", "c37e675470f64c9d",
            "94896715181d0a14", "3351faf207c7abe7", "67c9afd1e4e80f09", "852ff6a895ce20f1", "7f6f5a062048ff58", "7e02fc7641d1b780",
            "0a296ec8a2058bfd", "3cf521221ae3b927", "81a03558fba98a58", "e82f6ed3ea025b6b", "762726aeb6400ad6", "b0f3e1d1eb3a0d9d",
            "2afd4c1bad644650", "b3b77850e6db36d6", "d8e5e456d8df0d92", "b7e7dbcbb786007c", "819ff0764983736c", "40057aff931748a0",
            "127d98714dc3423b", "3c11601ef8f49092", "2d3cb70d7e250690", "68dd1353338cbc12", "6fc4439df07b1dff", "0eddbc132e91aa4f",
            "c86d73f790abe037", "fb314aaa5a3e9975", "d6c43608227214bd", "bee43b1484584e8f", "12c269cefa7619d2", "1d9f0663996ae353",
            "a508cb16eb878a97", "2b1b3c8bf2f8ef9d", "c3c2d4aa48acba59", "f1ce8ef0425807a3", "041beac18f986256", "fca9d1ffc08e88d1",
            "7cfb96c0974a827f", "7da2c6a8c1e72eb8", "53d9f4a855986fad", "d413a50915f5db1b", "ee367f74952671ed", "c095008dd78ce3b0",
            "e100938987bda9aa", "e873b262844fdb7b", "4c94edcc28f43ed3", "957b649cad9e3585", "8732944585f51492", "469e2a6b7cd58651",
            "79267950b76a5410", "71b1b8e86049b334", "5bab07857ae62eaa", "e93076d22e183c95", "e49c750312e4a30a", "b052949706ee92d3",
            "6816a30a83b9f674", "27ef9719084b142b", "c9b148cda15b606d", "76946cc939e0cd76", "e6e57ac00bcdafb1", "b679b96b421c241d",
            "e29a4527c7966b97", "0d5602a5fbea56df", "7c59f948f88e28d1", "0e87e4dad8ade827", "be5513813ee08ea5", "7e24a67099c7b925",
            "7f2c9e16739c29b0", "21eceb2fd714bd34", "2a11fb95a116c6f8", "37f9d61bc4624933", "62401bd30c4e6402", "b87710efb74ad4c7",
            "425f9764b69dbe8d", "60b4565b306b58d7", "c4c809a832668d45", "7394498c485ca848", "b9f3c692ad85c20b", "6e826a48ba041ee1",
            "a94e94999d6a7430", "016e487d7d3a04f8", "52a18ded3cdadb1f", "3826e210bd5d585e", "51972fa858c54489", "ddffa3c088f9bfca",
            "1f376325c719294e", "75c545586df838b6", "d143a732f59980b9", "2416b7a58ccabfc1", "65191db18fad4c6f", "0d9287070c092061",
            "d93ccb34669fc30a", "743c3370b0c434f0", "8dfe54b636386f2b", "2cf8aba997f773c6", "9b58d8e6988c7e65", "49a59a4bf9d0ba2e",
            "90a41b869d611faa", "b5fdf9e086c94ccc",
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
            "9e5edc2e25c8e445", "94e9cd1df650bca4", "38f29680732108e1", "fba40ea11ac01d6c", "229be78cace8d47a", "8b4118e261ab3a96",
            "507c55c0cf002be9", "53dc305212b25872", "5e4e36b37a346c97", "ee743fd0b381f0bb", "2815ee3cc15fe001", "5ce2ca77e0d0eb52",
            "e21c08a9bb883073", "9201f7e887d2e920", "8a2b6dbdf73dd932", "f9fd9a03d098f947", "599b4f78c28702ab", "267da775c7611f4d",
            "3bee47333c466484", "8dd66025b25082aa", "0470fdcceb25eb6d", "3eb6a1f1692010a3", "1b080fb989bf2032", "286efa56f66a0f10",
            "e8307e9ce39194dc", "00d14977b38a0279", "2c0f7c2f08256e5d", "8b84b5a4a103612c", "a65691cbabf54e59", "78bdb8a0d6d25913",
            "f360f2b83a2082aa", "d2aae05f671f6f79", "9d8beb78848de9c3", "2bb0ba5f00b17738", "472df5037e663965", "60d921e9101ace66",
            "9ae4c7402d347b24", "8d9c8f6948ea9d93", "e8c0a1bd863479c1", "1fe0314ba48fc338", "4a0dcb6baa44d101", "f2c6333c3c1dba3a",
            "00bcc84eb9e471dc", "edb82624ff5bfaa3", "bb7c9cf5312301d9", "c8d27aaa1ebe5be4", "4c98abe9ed4d6521", "b0afa2eceed23c26",
            "d524d98d68dba12a", "1b16e928ba599baf", "b07aff88af477cf6", "0de043c46048e34b", "6a90bf98b426cc1a", "a564212bdcced099",
            "5625a876a97ebef9", "c085fd3fcd70a220", "5e969ed73bd0ff29", "40c86800d715391c", "cd3263e64e21632c", "885e925028a01b10",
            "e4a208af3c8064e7", "ec3dd9048d04885f", "3a590a96db8c4436", "78d2c76e9a247515", "deb2604ea9365ea6", "565bd9e1ac7585ce",
            "a2b2b92e40e45aa2", "87c3b6f48f0ba6ea", "5f475106ca981888", "335ed5fa0907a8b5", "4b8bad1e0f0c3861", "f9e662c9c7f87736",
            "52035decabcdee36", "763502addad25f3a", "a09630780ec51dfb", "e9fd4f4724da2ef6", "8a8fbaf90eb40e56", "0b307940a47c96ed",
            "8cdce2fd8663e3cb", "fa4daca4033a2c7c", "b4bd12e976deb190", "48ecd6b0e40f73ba", "281aec5468a94af2", "108e41f079e6734b",
            "e95630b147f3cd7d", "42eb515c9c8879b5", "faf0b81081380788", "f683acad962fe1bf", "6932243ba7626c6e", "d7cdebec87e7bca9",
            "d7011e686bf33357", "8a6d4b7fa933b881", "b4349281a254ebd2", "c26a37bad65a1705", "305ceb2172de7e11", "efad5bb1e6c7f536",
            "2c66fa1e07d1a70a", "e7983b804200f14f", "b4927898fb7b1764", "aca945df7b105709", "3b7c3b6a74e2eaf9", "f59d5d8e28260bc0",
            "68564380b596a1f5", "23593ce88764ba00", "cb7cbc374a615a50", "63a9e65fdab470a4", "b48987758e3a3266", "0da5ae158e07e241",
            "c045c059b94ce286", "bc4834b5455bb190", "eb051a4a2a19a7f7", "ad91aeba5f1b0e04", "d3d8be84e3ef86c2", "254ebcf50b2af135",
            "6e5cd277daf3aa72", "11d711a278f8dc83", "b45990002f0b416e", "5263f892b244732b", "7f1b7b99595b0a52", "6ee72859ae7f3f97",
            "3fc8d2d020727ad0", "3e89f58f358c049a", "c1b665f2b2325512", "6554a90026139900", "46bd43ebe3617db3", "5a7bf8f4772d24ff",
            "43193c8bd6c968b4", "4defcbe8d03722e5", "8497099587022643", "527b381ca2f88ab9", "502057735b18b099", "520c48cf949dc989",
            "92b3bbc10083e105", "f07dccc2dde8e8a0", "189f7c8aa1bd3af7", "3c9772e346df5bc9", "f2e67c22f3b70fa8", "c3fa59ac0218031e",
            "b8ecfaf16e306195", "f2806555780d5b95", "14d606aaef2a23f3", "df5c5120c3ed9f29", "6057ca3a3e4b2370", "16bf124834699aea",
            "3b79a28258cde3d5", "0521f985f2991520", "166034150733c53b", "6d5d71564a258e38", "7626dba3314b5b51", "d8b3a695b157e467",
            "674c005a9f9f29d4", "4069de48062d0fac", "774208d5635385d9", "f2141df6964431c3", "a52e5bcffc345363", "cead1acf437fa063",
            "579a6149bbf361a5", "320d900c70312be6", "9af85f3269f2a185", "2fd95315374d837b", "ea554a6db8868ae9", "b6a298a2bf0ca921",
            "a1aa69f197fcda84", "2916139dfb204502", "159c29d3630e2174", "9720294295f2dad5", "45b4c9d2e30cb9e7", "1a7be65cab95fb5d",
            "100d2590332d5651", "c50da9e1db712479", "48769424837c5aa6", "405f759bf3c2d4b0", "9e0479e6f4a8d5d3", "233541c388d5a204",
            "723599299bbbdfb5", "04448e63e29203ca", "5fdf4db145e767b6", "c8ed7e86007dcee2", "ab1aca248dafcc7f", "6e53b769e49a0e84",
            "889e9d59332c365d", "73801ff5ad807a69", "46530f12656494f6", "210883fad16a5d0f", "d3497aa635bb16aa", "440fdf35eaa9500d",
            "128f69ce178c4a52", "79a32d1936d900fe", "e5f4420b426d5c63", "cbec8bf17b03791f", "98700319be58417c", "2d465415f72fade4",
            "30a379367c2b9af3", "fd49b4437f75538c", "69a50dfc1a7d12c5", "812c577bbb388eb3", "3c622ca1171cd2e2", "a260b58e5f380a5e",
            "cb83b8d516b4dc60", "3fb462bbcb14607c", "5e4ecf96e843fabd", "2b7500494306ac6c", "8b65aa16ad1c740e", "8e499626e8284085",
            "7afb6a1a17914e87", "8c0a48e5d1caa2f5", "7b83ac4267df6577", "26e8e17e57187f36", "7416207599b059ad", "f398aa85bc4598f7",
            "7efac61cc9523bca", "91f50e3737dc1fc8", "cf03ae52f28e1f5f", "4c8fa3960d2342e9", "c8f8f7b49916ca85", "96b627ff6d937525",
            "66551586b83d275a", "241f17d72a58062e", "2c85a97af601cf09", "ce339b267b33002b", "eb96efbf797d88b7", "da1ad28aad3f1fb4",
            "a2aaca29bad41dfe", "b7257d5a0268e800", "34f2926932418b79", "2efd46af3bd9d4bd", "83a2584e68badcf5", "147e27866bf81998",
            "a5dfa8fed00075c0", "eb3f4c8749448ead", "d891bd973e623f2d", "f142d59da62d59f6", "867fe8bb9573c811", "0bc4a82a45de8dab",
            "6f34d9258763bb4c", "a2a747310e54e0d7", "273bc453d7d91c76", "438be2ef153cf60c", "3a066998d248775b", "53c6250f7102e6c3",
            "3edffc25b1d79b1b", "400a969c59db620d", "83dedae20e4ec2f5", "d0312cfcf44bee8b", "77bfcc4212031769", "5c02b084b60fd1a5",
            "df7f4a80caefc776", "11fd3547082d494f", "3bbc8ca2f6921cb6", "ef5b3b99f0e172af", "2a6f3e1b6dd36ffe", "1f614841ee7d75ad",
            "2200d8b1bfb3b00c", "c005b6c5cda349d1", "07538acbe35eb7cc", "bbc0a9fec03e47cb", "9acabb87bec5f3bd", "e6323f8a4f5e67b3",
            "f4833408009daf93", "fc6e6ea0c74102b5", "ecec101338f91e9f", "390f6831aecd163f", "35cf60319b390321", "711eda4b53877817",
            "fd0aba1661a2125f", "c26deb6588ba19bf", "3a15e3eef62312f7", "2afc1f79aab74fbf", "73b65ba475c15c97", "a6ba991c0e20daf8",
            "2cad484d42098072", "f4a70adcbd545c56", "c9bdafea66747563", "629b1b3d2bac1ca0", "6c9e4ae8e342054a", "77a1312273fb0d62",
            "82bcfeb3adca1c72", "744f1ebedec000ed", "39516267f3e9ee2f", "6b94b6bf32da13c8", "9c8128c956229068", "8ee1f71dc8d30f16",
            "bc8ab607c04dd651", "a61a8190a3cdc0c8", "f8d70448d5fbff4a", "c450a04a88e6f57c", "11577d53357176bb", "864960b79d580621",
            "d41cbd5c135b7570", "89443737081a0913", "64298c98e87a48e1", "f0280d2639ecfbfc", "8523914641951f02", "a95539a521a14dad",
            "509c3e85bcace4a2", "78810faeb0da36d5", "c8d1b1c9a0581d4b", "d7e9be5e69944a07", "44526ae1dda11559", "eed257ae17fe4806",
            "86ca49cd6c910e19", "7878059415a2b72a", "ed7e4f572fdf42af", "c9154c45848e4da5", "c7c57b8583ebadf9", "c1791769636a5aef",
            "d991aad947f08328", "8d0d186c38413349", "5b32d5093b710b40", "58fa9f6bd6330ec7", "dc7c2df019fe1f31", "e9a2a6853efcd6c9",
            "77cf18bf5c19df13", "8c4db985ad2d6e53", "d2421be9db0e3fe4", "11e45a14eb283d66", "8e77fddb4f419354", "7820d1c48e47370f",
            "62e6fabeb05fbc46", "9c884d9c5c431c17", "b56080120800fda5", "f649acefb9121900", "a5d5c06149541bf3", "91349dfeada15702",
            "6f67f1e5fed61185", "a45d0b9492825529", "616ca096e16afd6c", "042506a3fa5b0eff", "7402848d201c8c5b", "7f01da77a1c3a867",
            "407b1d8df727cd4b", "c2d32fc406e1479c", "e58af411336f4090", "ff385da7c1452806", "3517e571b560e8df", "930941419d3c64f8",
            "f649bb75b69b73cc", "0076b80a59292a4c", "7bf9859d40fe82bb", "2ea1b1653ea2959a", "9df7a8ff7a3a9eb9", "25c296fa2818329f",
            "5e3b7fc6a1e6efda", "2869fd138c651957", "c37cfc5badf65f56", "a723f0d2c390367d", "e238d0f5aef77d14", "8992b3de07d1ec2e",
            "5f8d9f50206d1509", "20e3f31c44dc6603", "7d606f5ce1544c55", "d2d6fe0173ae64be", "6861bb346ae6acea", "ba52d3e60eb38f23",
            "f38005b0d8396c02", "0a305ad63ecf5872", "0840b8301dcc5a05", "cad20087c4ec77d9", "2cc0d7a6c770efaf", "fb57187b0122cd16",
            "a6570d400474463b", "949dd8da4d99c2e5", "b7b18a4ec0f362ae", "f5f290822429a720", "697c7dbd77d6f61d", "7641859f4364a745",
            "74f032bf990da699", "a4fd8d4b2753a450", "b0a12015d01001d3", "6981a9d59e9136a6", "f84cbeea61947f19", "ead542b02add845a",
            "cafb7f8b8783cc5c", "6102fe28906e548a", "12a3cb3a494b0374", "156e066dafd98cfe", "7dd0c11ca96c50a7", "8b99949998369c59",
            "63092f5ca0d78244", "890eb4c54b57ff93", "30b458dd724d5779", "fd1269537fc57d33", "6dc109c4fba17c32", "929b3100c8456378",
            "b1804abc25fd66da", "88f14ef8a2a1b5da", "0923d8036b97d733", "f826afac87aceb88", "4f82493a692eb053", "9d435bd73b27ea54",
            "0e6509073f662562", "29c4125031d53061", "268a4f8212ae2b15", "9556fc7856a4ab95", "c77c5f99dfb1049c", "1ffe2d01f0b0f4bb",
            "54c598d5acd3d260", "1bdf60916e5396ce", "919595e9b723a1d4", "72c6f74e852c519f", "d58f27da39a5eb92", "3b0d254199f403e9",
            "1e8d00112ba7bf91", "34a7dccfbd6b0413", "cb3f72858e7ca3b1", "6b4f04ec5966a017", "c003eb2971c7aaa7", "6f9aca892381d44b",
            "f4c5a44eefe6736a", "19f5ab5708b58140", "1b183b9e2a7b5833", "4989d04f8bd12676", "f2832e13dc094efe", "45f39d4ccf4f6799",
            "9018f2b36d1d38f3", "f0aa98de15c90ddb", "5ee516430bd59dbe", "0c140c6b0e8b18c1", "6f51920c90c9e0ca", "16a30517234d54a5",
            "8ea5cf3b77284c69", "8d93d26ffafdd7f5", "d279a24da6919685", "3a3e4c2a4676a98c", "753e3878a25d6965", "b24c82afd8a35141",
            "e04d8ff330029a64", "d6c6032f6b43642e", "49f8a45b6a61a1be", "ff9d2db284beeee3", "ac81cf20e18a98b2", "538009528f448c82",
            "4f4b24303b3f26b8", "b65a497bb660254f", "7bda2d39d8c9d696", "236a8991d75fa2df", "0e8118f3e0f15fdf", "03888793ec6f5326",
            "17dc05b778318667", "129dd5212019c123", "832bcea0b389162c", "ca27d0868f50d483", "2931810f1f7d8b1f", "41db38daac672cf3",
            "6d00010d073b6c45", "31a9543f727783b5", "e4d305335852b2f7", "f3c63b5b4da63383", "1c2ddb9c44e3feb7", "b34fc780cb4fee77",
            "78598d85f8984d1f", "65a3763e37178248", "6f40d81fc7b300d1", "1f1b9ae512a9f4f3", "1f8ea8d9e515491e", "2844503dc13b9240",
            "d22faa38afd8b8a0", "ee1689cb179352f9", "f5234ae506506451", "62af5ab1e14509b2", "a093741d04eb2c36", "00664efbc0a9daf6",
            "addb0ada0224c450", "e93274777cae2a30", "21057e341b186461", "fdca1ade13b101e2", "7ec3efeacf22f043", "7ee1621f114bc662",
            "8264482b7c24faaa", "6f2c2bd6c614dbff", "7568636dba2c672a", "7fb10e6e9a422e73", "10903747bc6857ef", "9b3508dc251319be",
            "c3a5a3197b83df48", "9d1008a158b8a8c8", "59ad753f16b6683d", "6ca0d82bae1cc762", "ca65d0259ed1dbc3", "3e84828213f67503",
            "7eb47723692fe06c", "72237baa32d09f3c", "708f9d3c4ae0106d", "b5bf6b3e980a6ff0", "49dc48a6802ebddd", "5c203ab3b0a4e12f",
            "d64ca879f517e375", "e3006b972b0c01c1", "f08df9b5061a0b72", "ee50810a029cd8fd", "89a4cb5e02b925c1", "0841d1e25566916c",
            "67d3f9f1ad1ff9d8", "aae1e76720da5af4", "c210d1c88d5f943d", "7b17503561aaf451", "58832e9ac9b5318b", "b85a5710f3e236c2",
            "a12611c7c3d83e11", "006ad44f04388145", "bde0f713402db444", "41daf16e1bac5744", "ed2af389b504c59f", "082e67746c1b7f1d",
            "f3e6e8f606217bc8", "e77745294038237e", "6f7c201080f27609", "8939c7d511d8a49f", "619884e9a108bfdf", "fb0a837aed6338ed",
            "7ebec460bb7b1c1f", "2c2b8eac265fca1c", "9130af5d3ac4605b", "2550491e1ba52404", "0955682dce9b4caa", "ad4a4d7433481d49",
            "d9efec319d0d49a1", "32eb36d8ceddabfe", "eb350bb7662519f9", "b1a35c2db0bfafac", "007a45667b6da7d4", "12f2878db26772c6",
            "a81bea8a5556d431", "d5857178aeb2b570", "e0a3fde92836b65d", "5e3533e8c73473e8", "03f946f4edfba382", "90d67dab89aa32a6",
            "bb8dfd4a91c33b3b", "18543169a0c91ab6", "1db63b166c02885e", "fb855af5a3c63f9d", "30824e15896f8a18", "6d98f4d28f20b0be",
            "85d42be7e276ef88", "d8653de260612ffe", "5fa6fc5bffbd9afe", "b7c6762f5993d513", "4234002f809e62fe", "ff9669968227c2cf",
            "07b4e948d95d0a65", "0251a5899a7d3c36", "1e72045c6f44e61b", "86b6085379f75df4", "087f69622ad6379c", "a848ac32886701e5",
            "f886f5121f149cb2", "06c1f11a4ea4f066", "bd4eced27d9bddb8", "ed740d4cfea3380a", "d46ee4d8fc3d0344", "04fbf1be6295d461",
            "74acdb9861c305fd", "7ff4253848151307", "c0451c3141479549", "342a9a14168d2559", "b808e0e473260292", "17fa1a4a21c964b3",
            "c0c3aa741401383c", "ed0e17b52fbcca6d", "f90c854167af49f7", "a9c1673f554b573f", "56600e640d4303d7", "aabd8c65a2cb67dc",
            "74c5f44c960119bc", "9a6c8ce9c64422c3", "55d4bf4b3c8de339", "c9649bc32da86dcf", "af8715d6a4f2081b", "c94a471ff07f7d10",
            "2e21879fb34daa32", "4bc25b68ef56eb7a", "cf975d24cb1411f5", "6ef7c5dcfc6195cd", "e49de711fe843f41", "27a1198731e0537f",
            "0a2683fad2c208e4", "495d2093a5776369", "4a629c177ef413a7", "3b959827230a5038", "e2facd37c8f00e8e", "9437728b4b2468ea",
            "aed22778df2a761f", "d193967fef7b4915", "34c9f0835f57704e", "e2074290f9fb6d2f", "62d88e96948a863c", "6ab14593af03795a",
            "ace6d987550064ae", "f83cead6a61479a7", "58b106f732ea9431", "48632b0234b5d0c1", "b27141408852bc35", "56eb6df73f696673",
            "2c7a4644c178f38f", "45c8ef5d04c82115", "b76208ae08d15baa", "47078c102951f16d", "ffdd00b7dfc86394", "c81ff251342e93bf",
            "07a67b7047539a5a", "717bb52950c782ae", "5a21ff4a0995df63", "965cf0d446418f17", "3f089324ceab7ff8", "a782bda9acf71969",
            "e9010f606f59f90b", "c4f4c3ca3b8b7f0f", "8cfbe93cc58d390e", "015910a79b84a671", "7163c89bdd83e552", "3c21cb95c63bc374",
            "355a9d2f2bd31eb5", "30d51949332eceac", "307ee49b92094ac3", "1175ba6002d8b24c", "a4dde104fe8c349f", "7b2c4e7fba0df626",
            "3ff4d8927ecf195d", "58a59921c3a48aa4", "adc9a002cf380679", "c21ed5848f9880a8", "eb3becc9db5c7100", "3e0f8df9a1cd36ae",
            "4c35d5eb0a421b0d", "2be09acae757d13c", "07fb73b2938d0895", "4a55436e1dba3007", "c2d4d323d55c557d", "6e0c9ef9bbe958eb",
            "39dfb654235e2bc2", "962b614585c0cb4f", "f1edcb2aacd8f856", "6fa527f7d59cffcd", "55b9da50b1a8342d", "af20ec94d08887f1",
            "42e7783fbcadb757", "5e09fc2577566f58", "994ee0f12a2950e5", "30f9bc31d5907197", "f62f3cfe23d22cdb", "36627398541306e0",
            "79adafdcb5bc86eb", "4636eb35eca819eb", "ddbbfe8a1a6a10f9", "e88e554c2c4682e9", "4bd489017ded3c01", "ee9fe0568230f686",
            "bd88767b151cf9cc", "5ccf9f319d94ca2c", "3897a75864bbd37d", "49ed0951fa4345e9", "48f92e8255b3a651", "165a2407d1a60b4f",
            "e7f7e3c48fbabc01", "a64dd6eab27ba4f2", "30163df24638dcee", "9e7521fd8e32d830", "b8c56a83702caf13", "8c7565dc8f95980b",
        ],
        ["projektarbeiten"] =
        [
            "65ac6d42be1fa2cb", "4df4352104f071bc", "28472cf3e7ec3b28", "7363c4eb747088dd", "88a10bf9d397b6e8", "7d591dff89d84271",
            "4e6ff6929cacceb9", "8c34b6d8e5427796", "e45d4ba08a740ae9", "d19c0e9ee6e83c69", "083274212a700147", "774b3bca95057d4c",
            "86ab4e5ddfc64609", "1d6dfc3c6ef89944", "9a0bc96cf07cbd20", "4d9c6f5eafd78cbd", "5c80deafe50d43ab", "005b1fce688bf2c6",
            "eef1740d3f0ee524", "a79f727f0ebe73af", "47c2854c5d748ead", "215ee84d0ffc23f3", "43d3a7eb36f9fd1a", "3a60b034dff6983e",
            "47a51543778b54aa", "fbeb52bb2f987fe6", "2f420baf90a5cfd8", "ea6bc1623a69d0c8", "33b5d82126909135", "dbeecf3b321fa1c9",
            "db42e3fbbdd63f51", "c5ab5892e0fcd77e", "ac135f94e12d48ec", "2a66a04feef59f71", "5723332566255af5", "0aecdbd69000b26c",
            "4b978abb0047d26e", "e01caa8c44a91183", "935cc1be2f882e76", "59efa198c1ddf06e", "12683fff7e6d31d8", "9ded3351cc8bd646",
            "146f0c3134f1e6b2", "37eff202da21c9b3", "6e7c639722dddc7b", "531751ff3421419e", "e95c48c110a427cd", "daf64dd383056ec4",
            "6d4bdc39b1751d41", "7fed85ac4ee96369", "93d1abddd83a4886", "257b032ae26a4629", "4e887a6773be8392", "11f9430ed325c9e9",
            "8606e4163fb565a3", "ae96681dad4dd46e", "a43e25bf2f3fa904", "e7619676bec69473", "6a7e5e0428e1dfa6", "131dcd85cf1905b0",
            "cef0451be51d85d9", "7d6c1ea572af29fb", "154b1c2aff4e8eef", "c5b8f5f3e8596eb0", "170456f18b7d6dce", "e2219839b8843d37",
            "a73ee8392cd2761f", "d542ccdea97d120c", "16bac985276fd54e", "ca9ba5dc8dcce42c", "1d520f8590546352", "81508b320ebe6af7",
            "de229dddb0014dc7", "116c946c8039dcb2", "859fefad927c9189", "a49b8c6221544ba6", "09b553499baa7e1c", "c4283021fb9a4389",
            "4574a6dfd6b8edda", "72baf60dcfcffe4b", "c2e0446345f7b599", "f32a16a64ec85742", "b6eec4cbe4312a0e", "148cf74eb78abcb0",
            "7cdb392199eace64", "8406db84f9ac5a03", "2d2bc290580b33c0", "6c8dad7bd36276c3", "a4698400b9659b12", "9fc973a968b2bd61",
            "20bab20ab6095ce7", "d10e04d0773c49b0", "9107ef5f7bee0d04", "b9362b287efc365c", "0812a5b3eca5248f", "c2cd686edd86b617",
            "3015fbf94f8d1392", "babdde91a744cee1", "0ad3ddee021a8add", "81dc35fa5f424ba3",
        ],
        ["fachgespraech"] =
        [
            "8de5a12ca436f9f9", "aa621bbb857d9b8d", "e273606640105966", "0e0035628ead5ee0", "2f73c7ab5b1ccc87", "5cbe2581cff3dc76",
            "3cadd1adad51071b", "051b58b10819f17b", "b06a624c15472f0d", "1c8d0c97983b80f8", "80bbd098dee255e9", "ee83e6d57f682cfa",
            "38485414ed5337f4", "300f476c7cebdad0", "5fa2f948c2daf305", "f8b34ca965707f66", "7c71d90e741ae929", "67ac190d2389345f",
            "c4977cdd39c48433", "f3c48698d0f4d3d7", "149eb63fe1918e58", "478655e6063e5c6a", "5c10909edbff9931", "eeb09fa22b87e214",
            "ec7be1ab079d5cca", "b0623e766142aa3e", "4594301ca7f96be0", "a8d6ef847c5db008", "089d54957f2b6270", "bb654547298bba32",
            "5350b13b416a4023", "a0105d83515d8e4f", "ccdfc7477f18ea3c", "dfd1b910a4fdfadd", "1364a0d0033c3b77", "5c753a36e93b6d91",
        ],
        ["themen"] =
        [
            "sicherheit/bbf579a239078c3f", "systeme/847eac4918c65b96", "systeme/5974b7a21f25eded", "systeme/9952945dec1f519a",
            "systeme/026d1414c35b61aa", "systeme/01d98780ec310382", "systeme/dc57e6730821a891", "systeme/45dfbd37f2413b48",
            "systeme/fbda39307f0aea56", "wirtschaft/b30c1d23f01656f8", "datenbanken/efaa526411d5adf0", "sicherheit/d14e4c1756d6c7e5",
            "systeme/35008919a3e48fdc", "systeme/d98b5a81d6c0a881", "systeme/3625b34fc4104db5", "systeme/330e059ce7870b71",
            "wirtschaft/31287aff87a0b156", "wirtschaft/9f2c7dd9d43c8562", "wirtschaft/69c3bbe745446294", "datenbanken/543220eea22dfc66",
            "datenbanken/f92ae8c4ac138d82", "datenbanken/7a2179fcbde911cd", "sicherheit/cf352f93e189daac", "sicherheit/044fad1404a7eaf9",
            "systeme/05e00dec4d8ffcbf", "systeme/89b58edc44e36007", "systeme/e8017b61c3851add", "systeme/c3d09f1870040a0b",
            "systeme/f49dbf48a34f479c", "systeme/2687f04e3f61997d", "netzwerk/337ffa1ef21fc5fd", "netzwerk/eb32f15571a761f3",
            "netzwerk/1347d70e91cc0a41",
        ],
    };

    [Fact]
    public void NurAnhaengen_EintraegeGleichDerTabelle()
    {
        foreach (var (datei, soll) in Eingetragen)
        {
            var daten = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Lernen, datei + ".json")))!;
            static string Hash(JsonNode e) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(e.ToJsonString())))[..16];
            if (datei == "themen")
            {
                // je Lernbereich eine Liste: neue Themen kommen ans Ende ihres Bereichs (Tabelle "bereich/hash" je Thema)
                var bereiche = daten["themen"]!.AsObject().ToList();
                Assert.True(soll.Select(s => s.Split('/')[0]).All(b => bereiche.Any(p => p.Key == b)), "themen: Lernbereich geloescht");
                foreach (var (bereich, liste) in bereiche)
                {
                    var istB = liste!.AsArray().Select(t => Hash(t!)).ToList();
                    var sollB = soll.Where(s => s.StartsWith(bereich + "/", StringComparison.Ordinal)).Select(s => s[(bereich.Length + 1)..]).ToList();
                    Assert.True(istB.Count >= sollB.Count, "themen/" + bereich + ": Eintraege geloescht");
                    Assert.Equal(sollB, istB.Take(sollB.Count));
                    Assert.True(istB.Count == sollB.Count, "themen: neue Eintraege in die Tabelle Eingetragen anhaengen: "
                        + string.Join(", ", istB.Skip(sollB.Count).Select(h => bereich + "/" + h)));
                }
                continue;
            }
            // Phase 10 W2: fachgespraech.json ist ein Objekt mit der Liste "fragen" (wie inhalte/fachgespraech.json)
            var ist = (datei == Lerninhalte.FachgespraechDatei ? daten["fragen"]! : daten).AsArray().Select(e => Hash(e!)).ToList();
            Assert.True(ist.Count >= soll.Length, datei + ": Eintraege geloescht");
            Assert.Equal(soll, ist.Take(soll.Length));
            Assert.True(ist.Count == soll.Length, datei + ": neue Eintraege in die Tabelle Eingetragen anhaengen: " + string.Join(", ", ist.Skip(soll.Length)));
        }
        Assert.Equal(Lerninhalte.LernenListen.Select(l => l.Name).Append(Lerninhalte.ThemenDatei).Append(Lerninhalte.FachgespraechDatei).Order(StringComparer.Ordinal),
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
