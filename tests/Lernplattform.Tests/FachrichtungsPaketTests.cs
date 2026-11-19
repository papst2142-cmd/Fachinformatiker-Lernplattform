using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Pakete;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.91.0 (Weisung P12-300, Kurzplan P12-180, Nico 1b 2c 3a 4a 5a, Zusatz a): Fachrichtungs-Pakete (Art lernen).
/// Abnahmesaetze: (1) Art lernen mit eigener Regel und echter Testdatei, (2) Schnitt aus einer Quelle (unabhaengig nachgerechnet,
/// Gegenproben), (3) „nur angehaengt“: verschobene Stelle, geaenderter oder fremder Eintrag, Paket aelter als die App = rot, der
/// Lernstand bleibt bytegleich, fehlende Eintraege ruhen und kommen zurueck, (4) der Lader geht auch OHNE eingebaute Inhalte einer
/// Fachrichtung (Platzhalter, Vorbereitung 386), (5) Zeilen, Hinweise, Sperre, Hinweis nach Abgleich. Nur Testdaten (A3).
/// </summary>
public sealed class FachrichtungsPaketTests : IDisposable
{
    private const string App = "A 0.91.0";
    private static readonly string Lernen = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen");
    private static readonly string Echt = Path.Combine(Pfade.Wurzel, "tests/Lernplattform.Tests/Testdaten/pakete_echt");
    private static readonly string[] Frs = ["ae", "dpa", "dv"];
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "frpaket_tests_" + Guid.NewGuid().ToString("N"));

    public FachrichtungsPaketTests() => Directory.CreateDirectory(_tmp);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tmp, true);
        }
        catch (IOException)
        {
        }
    }

    private static Func<string, byte[]?> Repo => name =>
        Path.Combine(Lernen, name + ".json") is var pfad && File.Exists(pfad) ? File.ReadAllBytes(pfad) : null;

    private static readonly Lazy<LernenOrdner> RepoOrdner = new(() => new LernenOrdner(Repo));

    private static JsonArray Liste(JsonNode wurzel, string name) => name switch
    {
        Lerninhalte.FachgespraechDatei => wurzel["fragen"]!.AsArray(),
        EchtSaetze.DateiName => wurzel["saetze"]!.AsArray(),
        _ => wurzel.AsArray(),
    };

    private static readonly string[] AlleDateien = [.. FachrichtungsPaket.Listen, EchtSaetze.DateiName];

    /// <summary>App-Dateien, in denen alle Eintraege von <paramref name="fr"/> Platzhalter sind (Inhalte nur im Paket).</summary>
    private static Func<string, byte[]?> MitPlatzhaltern(string fr, Func<string, byte[]?>? quelle = null)
    {
        quelle ??= Repo;
        var geaendert = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var name in AlleDateien)
        {
            var wurzel = JsonNode.Parse(quelle(name)!)!;
            var liste = Liste(wurzel, name);
            for (var i = 0; i < liste.Count; i++)
            {
                if ((string?)liste[i]!["fachrichtung"] == fr)
                {
                    liste[i] = new JsonObject { ["fachrichtung"] = fr, ["im_paket"] = true };
                }
            }
            geaendert[name] = Encoding.UTF8.GetBytes(wurzel.ToJsonString());
        }
        return name => geaendert.TryGetValue(name, out var b) ? b : quelle(name);
    }

    private static string Sha(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));

    /// <summary>
    /// Unabhaengiger Nachbau des Schnitts (paket_bauen.py lernen) aus den Dateien des Repos: alle Eintraege der Fachrichtung
    /// mit ihrer Stelle, die Ausschluesse darauf, die Saetze mit Stelle; Kopf mit Pruefsummen.
    /// </summary>
    private static Dictionary<string, byte[]> Schnitt(string fr, long stand = 1, Func<string, byte[]?>? quelle = null,
        Action<JsonObject, JsonObject>? aendern = null)
    {
        quelle ??= Repo;
        var listen = new JsonObject();
        foreach (var name in FachrichtungsPaket.Listen)
        {
            var eintraege = new JsonArray();
            var liste = Liste(JsonNode.Parse(quelle(name)!)!, name);
            for (var i = 0; i < liste.Count; i++)
            {
                if ((string?)liste[i]!["fachrichtung"] == fr && liste[i]!["im_paket"] is null)
                {
                    eintraege.Add(new JsonObject { ["stelle"] = i, ["eintrag"] = liste[i]!.DeepClone() });
                }
            }
            listen[name] = eintraege;
        }
        var lernen = new JsonObject { ["format"] = 1, ["fachrichtung"] = fr, ["listen"] = listen };
        var saetzeQuelle = JsonNode.Parse(quelle(EchtSaetze.DateiName)!)!;
        var saetze = new JsonArray();
        var sl = saetzeQuelle["saetze"]!.AsArray();
        for (var i = 0; i < sl.Count; i++)
        {
            if ((string?)sl[i]!["fachrichtung"] == fr && sl[i]!["im_paket"] is null)
            {
                saetze.Add(new JsonObject { ["stelle"] = i, ["satz"] = sl[i]!.DeepClone() });
            }
        }
        var saetzeDatei = new JsonObject { ["format"] = saetzeQuelle["format"]!.DeepClone(), ["saetze"] = saetze };
        aendern?.Invoke(lernen, saetzeDatei);
        var dateien = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [FachrichtungsPaket.LernenDatei] = Encoding.UTF8.GetBytes(lernen.ToJsonString()),
            [FachrichtungsPaket.SaetzeDatei] = Encoding.UTF8.GetBytes(saetzeDatei.ToJsonString()),
        };
        var kopf = new JsonObject
        {
            ["format"] = 1, ["art"] = "lernen", ["kennung"] = "lernen_" + fr, ["name"] = "Fachrichtungs-Paket " + fr.ToUpperInvariant(),
            ["stand"] = stand, ["braucht_app"] = App, ["text"] = "",
            ["dateien"] = new JsonObject(dateien.Select(d => KeyValuePair.Create(d.Key,
                (JsonNode?)new JsonObject { ["sha256"] = Sha(d.Value), ["groesse"] = d.Value.Length }))),
        };
        dateien[Paket.KopfDatei] = Encoding.UTF8.GetBytes(kopf.ToJsonString());
        return dateien;
    }

    private static byte[] Zip(IReadOnlyDictionary<string, byte[]> dateien)
    {
        using var puffer = new MemoryStream();
        using (var zip = new ZipArchive(puffer, ZipArchiveMode.Create, true))
        {
            foreach (var (name, inhalt) in dateien)
            {
                using var s = zip.CreateEntry(name).Open();
                s.Write(inhalt);
            }
        }
        return puffer.ToArray();
    }

    private static PaketErgebnis Pruefen(IReadOnlyDictionary<string, byte[]> dateien, LernenOrdner? eingebaut = null) =>
        Paket.Pruefen(dateien, App, eingebaut ?? RepoOrdner.Value);

    private static Lerninhalte Laden(LernenOrdner ordner, IReadOnlyCollection<string>? frs = null) =>
        Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte), ordner, frs);

    private static readonly IReadOnlyList<Core.Projekt.ProjektFrage> BisherigeFachfragen = Core.Projekt.Fachgespraech.Laden(Pfade.Inhalte);

    private static List<string> Abdruck(Lerninhalte alle, string fr)
    {
        var sicht = alle.Fuer(fr);
        static IEnumerable<string> S(IEnumerable<Lerninhalt> l) => l.Select(e => e.Nummer.ToString(CultureInfo.InvariantCulture) + "|"
            + e.Zugehoerigkeit + "|" + e.Daten.ToJsonString());
        return [.. S(sicht.Karteikarten), .. S(sicht.Quizfragen), .. S(sicht.Ap2Szenarien), .. S(sicht.Testprojekte), .. S(sicht.Ap1Szenarien),
            .. sicht.FachgespraechFuer(BisherigeFachfragen, fr).Select(f => f.Id + "|" + f.Frage + "|" + f.Hinweis),
            "quiz-datei " + sicht.AnzahlQuizfragenDatei];
    }

    private static List<string> Saetze(LernenOrdner ordner) =>
        [.. EchtSaetze.Laden(ordner.Saetze()) is var b ? b.Saetze.Select(s => s.Kennung + "|" + s.Firma + "|" + s.Minuten).Concat(b.Fehler.Select(f => "FEHLER " + f.Meldung)) : []];

    // ---- (1) Art lernen, echte Testdatei -------------------------------------------------------------------------------------

    [Fact]
    public void A1_EchteTestdatei_LernenAe_IstGut_UndGleichDemSchnitt()
    {
        var bytes = File.ReadAllBytes(Path.Combine(Echt, "lernen_ae_1.fipaket"));
        var e = Paket.PruefenArchiv(new MemoryStream(bytes), App, RepoOrdner.Value);
        Assert.True(e.Gut, e.Grund);
        Assert.Equal((Paket.ArtLernen, "lernen_ae", "Fachrichtungs-Paket AE", 1L), (e.Kopf!.Art, e.Kopf.Kennung, e.Kopf.Name, e.Kopf.Stand));
        Assert.NotNull(e.Lernen);
        // der C#-Nachbau des Schnitts bringt genau dieselben Eintraege an denselben Stellen
        var nachbau = Pruefen(Schnitt("ae"));
        Assert.True(nachbau.Gut, nachbau.Grund);
        foreach (var name in FachrichtungsPaket.Listen)
        {
            Assert.Equal(nachbau.Lernen!.Listen[name].Select(x => x.Stelle), e.Lernen!.Listen[name].Select(x => x.Stelle));
            Assert.True(nachbau.Lernen.Listen[name].Zip(e.Lernen.Listen[name]).All(p => JsonElement.DeepEquals(p.First.Eintrag, p.Second.Eintrag)), name);
        }
        Assert.Equal(nachbau.Lernen!.Saetze.Select(s => s.Stelle), e.Lernen!.Saetze.Select(s => s.Stelle));
        // A 0.94.0 (P12-304): 10 alte und 10 neue AE-Saetze; die 10 alten stehen mit Stelle und Kennung im Ausschluss
        Assert.Equal(20, e.Lernen.Saetze.Count);
        Assert.Equal(EchtInhalteAeTests.Ae, e.Lernen.Ausschluss.Where(a => a.Datei == "pruefungssaetze").Select(a => a.Frage).Order(StringComparer.Ordinal));
        Assert.Equal(1500, e.Lernen.Listen["karteikarten"].Count);
        Assert.Equal(330, e.Lernen.Listen["szenarien_ap2"].Count);
    }

    [Fact]
    public void A1_ArtUndKennung_GehoerenZusammen_Gegenprobe()
    {
        // Kennung lernen_* nur mit Art lernen, Art lernen nur mit lernen_ae/dpa/dv
        var d = Schnitt("dpa");
        var kopf = JsonNode.Parse(d[Paket.KopfDatei])!.AsObject();
        Assert.True(Pruefen(d).Gut, Pruefen(d).Grund);
        foreach (var (art, kennung) in new[] { ("stadt", "lernen_dpa"), ("lernen", "lernen_si"), ("lernen", "probe_lernen"), ("lernen", "lernen_xx") })
        {
            var k = (JsonObject)kopf.DeepClone();
            k["art"] = art;
            k["kennung"] = kennung;
            var neu = new Dictionary<string, byte[]>(d) { [Paket.KopfDatei] = Encoding.UTF8.GetBytes(k.ToJsonString()) };
            Assert.Equal(PaketTexte.Format(PaketTexte.KennungArt, kennung), Pruefen(neu).Grund);
        }
        // die alte Probe-Datei (Art lernen, Kennung probe_lernen) ist jetzt rot mit klarem Grund, nie ein Absturz
        var alt = Paket.PruefenArchiv(File.OpenRead(Path.Combine(Echt, "probe_lernen_1.fipaket")), App);
        Assert.Equal(PaketTexte.Format(PaketTexte.KennungArt, "probe_lernen"), alt.Grund);
        // eine Art, die auch diese App nicht kennt: weiter „unbekannte Art“
        Assert.Equal(PaketTexte.ArtUnbekannt, Paket.PruefenArchiv(File.OpenRead(Path.Combine(Echt, "probe_grafik_1.fipaket")), App).Grund);
        Assert.Equal(["stadt", "lernen"], Paket.Arten);
    }

    // ---- (2)/(3) Gegenproben „nur angehaengt“ und Aufbau ---------------------------------------------------------------------

    [Fact]
    public void A3_NurAngehaengt_Gegenproben_RotMitGrund()
    {
        // Stelle verschoben auf den Platz eines Eintrags einer anderen Fachrichtung (der zweite AE-Block der Quizfragen beginnt
        // hinter dem DPA-Block; der erste Eintrag des Blocks rueckt auf eine DPA-Stelle): rot
        var zweiterBlock = Enumerable.Range(0, 2000).First(i => Stelle("quizfragen", "ae", i + 1) != Stelle("quizfragen", "ae", i) + 1) + 1;
        var dpaStelle = Stelle("quizfragen", "dpa", 30);
        Assert.InRange(dpaStelle, Stelle("quizfragen", "ae", zweiterBlock - 1) + 1, Stelle("quizfragen", "ae", zweiterBlock) - 1);
        Assert.Equal(PaketTexte.Format(PaketTexte.StelleBelegt, "quizfragen", dpaStelle),
            Pruefen(Schnitt("ae", aendern: (l, _) => l["listen"]!["quizfragen"]![zweiterBlock]!["stelle"] = dpaStelle)).Grund);
        // Eintrag der App geaendert
        Assert.Equal(PaketTexte.Format(PaketTexte.EintragAnders, "karteikarten", Stelle("karteikarten", "ae", 0)),
            Pruefen(Schnitt("ae", aendern: (l, _) => l["listen"]!["karteikarten"]![0]!["eintrag"]!["q"] = "geaendert")).Grund);
        // Eintrag fehlt (Paket aelter als die App)
        Assert.Equal(PaketTexte.Format(PaketTexte.EintragFehlt, "projektarbeiten", Stelle("projektarbeiten", "dv", 49)),
            Pruefen(Schnitt("dv", aendern: (l, _) => l["listen"]!["projektarbeiten"]!.AsArray().RemoveAt(49))).Grund);
        // fremde Fachrichtung im Paket
        Assert.Equal(PaketTexte.Format(PaketTexte.LernenAufbau, FachrichtungsPaket.LernenDatei, "quizfragen: " + PaketTexte.Format(PaketTexte.StelleFremd, Stelle("quizfragen", "ae", 0))),
            Pruefen(Schnitt("ae", aendern: (l, _) => l["listen"]!["quizfragen"]![0]!["eintrag"]!["fachrichtung"] = "dpa")).Grund);
        // Stelle doppelt
        Assert.StartsWith("Der Inhalt des Pakets ist fehlerhaft", Pruefen(Schnitt("ae", aendern: (l, _) =>
            l["listen"]!["quizfragen"]![1]!["stelle"] = (int)l["listen"]!["quizfragen"]![0]!["stelle"]!)).Grund, StringComparison.Ordinal);
        // Satz geaendert / Satz einer anderen Fachrichtung
        Assert.Equal(PaketTexte.Format(PaketTexte.EintragAnders, FachrichtungsPaket.SaetzeDatei, SatzStelle("ae", 0)),
            Pruefen(Schnitt("ae", aendern: (_, s) => s["saetze"]![0]!["satz"]!["firma"] = "Andere GmbH")).Grund);
        // Pruefsumme falsch, Datei fehlt
        var d = Schnitt("ae");
        var kaputt = new Dictionary<string, byte[]>(d) { [FachrichtungsPaket.LernenDatei] = [.. d[FachrichtungsPaket.LernenDatei].Append((byte)' ')] };
        Assert.Equal(PaketTexte.Format(PaketTexte.Pruefsumme, FachrichtungsPaket.LernenDatei), Pruefen(kaputt).Grund);
        var ohne = new Dictionary<string, byte[]>(d);
        ohne.Remove(FachrichtungsPaket.SaetzeDatei);
        Assert.Equal(PaketTexte.Format(PaketTexte.DateiFehlt, FachrichtungsPaket.SaetzeDatei), Pruefen(ohne).Grund);
        // Gegenprobe: unveraendert gut, je Fachrichtung
        Assert.All(Frs, fr => Assert.True(Pruefen(Schnitt(fr)).Gut, fr));
    }

    [Fact]
    public void A3_AppNeuerAlsPaket_EintragFehlt_PaketNeuerAlsApp_Gut()
    {
        // die App hat hinten einen weiteren AE-Eintrag (neuere App): das alte Paket ist rot „aelter als die App“
        var mehr = Angehaengt("karteikarten", "ae");
        var appNeu = new LernenOrdner(mehr);
        var grund = Pruefen(Schnitt("ae"), appNeu).Grund;
        Assert.Equal(PaketTexte.Format(PaketTexte.EintragFehlt, "karteikarten", JsonNode.Parse(Repo("karteikarten")!)!.AsArray().Count), grund);
        // das Paket bringt einen neuen Eintrag hinten mit (neuerer Stand): gut, und die Sicht zeigt ihn mit der naechsten Nummer
        var paket = Pruefen(Schnitt("ae", 2, mehr), RepoOrdner.Value);
        Assert.True(paket.Gut, paket.Grund);
        var mit = Laden(RepoOrdner.Value.MitPaketen([paket.Lernen!]), ["si", "ae"]);
        var ohne = Laden(RepoOrdner.Value, ["si", "ae"]);
        Assert.Equal(ohne.Fuer("ae").Karteikarten.Count + 1, mit.Fuer("ae").Karteikarten.Count);
        Assert.Equal(Abdruck(ohne, "si"), Abdruck(mit, "si"));
        // ein neuer Eintrag an einer Stelle, die die App fuer SI benutzt, waere eine Verschiebung: rot
        var siHinten = Angehaengt("karteikarten", "si");
        Assert.StartsWith("Das Paket verschiebt Inhalte", Pruefen(Schnitt("ae", 2, mehr), new LernenOrdner(siHinten)).Grund, StringComparison.Ordinal);
    }

    // ---- Gleichheit: Grundstand + Paket = alles aus dem Repo ----------------------------------------------------------------

    [Theory]
    [InlineData("ae")]
    [InlineData("dpa")]
    [InlineData("dv")]
    public void A2_MitPaket_GleicheInhalte_WieAllesAusDemRepo(string fr)
    {
        var paket = Pruefen(Schnitt(fr));
        Assert.True(paket.Gut, paket.Grund);
        var soll = Laden(RepoOrdner.Value);
        var mit = RepoOrdner.Value.MitPaketen([paket.Lernen!]);
        var ist = Laden(mit);
        foreach (var sicht in new[] { "si", "ae", "dpa", "dv" })
        {
            Assert.Equal(Abdruck(soll, sicht), Abdruck(ist, sicht));
        }
        // auch beim Start nur mit der gewaehlten Fachrichtung und beim Nachladen
        Assert.Equal(Abdruck(soll, fr), Abdruck(Laden(mit, ["si", fr]), fr));
        Assert.Equal(Abdruck(soll, fr), Abdruck(Laden(mit, ["si"]).MitFachrichtung(fr), fr));
        Assert.Equal(Saetze(RepoOrdner.Value), Saetze(mit));
    }

    // ---- (4) ohne eingebaute Inhalte (Platzhalter, Vorbereitung 386) ------------------------------------------------------

    [Theory]
    [InlineData("ae")]
    [InlineData("dpa")]
    [InlineData("dv")]
    public void A4_OhneEingebauteInhalte_FehltRuht_MitPaketAllesWieder(string fr)
    {
        var soll = Laden(RepoOrdner.Value);
        var ohneOrdner = new LernenOrdner(MitPlatzhaltern(fr));
        Assert.True(ohneOrdner.Fehlt(fr));
        Assert.True(ohneOrdner.Gemischt);
        Assert.All(Frs.Where(f => f != fr), f => Assert.False(ohneOrdner.Fehlt(f)));
        // ohne Paket: kein Absturz, die Fachrichtung sieht nur die gemeinsamen Inhalte, die anderen alles wie vorher
        var ohne = Laden(ohneOrdner);
        Assert.DoesNotContain(ohne.Fuer(fr).Karteikarten, e => e.Zugehoerigkeit == fr);
        Assert.DoesNotContain(ohne.Fuer(fr).Ap2Szenarien, e => e.Zugehoerigkeit == fr);
        Assert.DoesNotContain(ohne.Fuer(fr).NeueFachfragen, f => f.Zugehoerigkeit == fr);
        Assert.Empty(ohne.Ap2SzenarienFuer(fr));
        Assert.NotEmpty(ohne.Fuer(fr).Karteikarten);    // gemeinsame Inhalte bleiben
        foreach (var andere in new[] { "si" }.Concat(Frs.Where(f => f != fr)))
        {
            Assert.Equal(Abdruck(soll, andere), Abdruck(ohne, andere));   // gleiche Nummern, gleiche Inhalte
        }
        Assert.DoesNotContain(Saetze(ohneOrdner), s => s.StartsWith(fr + "_", StringComparison.Ordinal) || s.StartsWith("FEHLER", StringComparison.Ordinal));
        // auch beim Start nur mit der gewaehlten Fachrichtung
        Assert.DoesNotContain(Laden(ohneOrdner, ["si", fr]).Fuer(fr).Testprojekte, e => e.Zugehoerigkeit == fr);
        // das Paket wird gegen die App mit Platzhaltern geprueft und bringt alles zurueck: gleich dem Repo
        var paket = Pruefen(Schnitt(fr), ohneOrdner);
        Assert.True(paket.Gut, paket.Grund);
        var mit = ohneOrdner.MitPaketen([paket.Lernen!]);
        Assert.False(mit.Fehlt(fr));
        var wieder = Laden(mit);
        foreach (var sicht in new[] { "si", "ae", "dpa", "dv" })
        {
            Assert.Equal(Abdruck(soll, sicht), Abdruck(wieder, sicht));
        }
        Assert.Equal(Saetze(RepoOrdner.Value), Saetze(mit));
        // Gegenprobe: ein Paket ohne den Eintrag eines Platzhalters ist rot (es fehlt etwas, das nur im Paket liegt)
        Assert.StartsWith("Das Paket ist älter als die App", Pruefen(Schnitt(fr, aendern: (l, _) => l["listen"]!["szenarien_ap2"]!.AsArray().RemoveAt(0)), ohneOrdner).Grund,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A4_RueckfrageText_WieInDerWeisung()
    {
        Assert.Equal("Für AE fehlt das Fachrichtungs-Paket (1,2 MB). Jetzt laden?",
            PaketTexte.Format(PaketTexte.FrFehltFrage, PaketTexte.Fr("ae"), PaketTexte.Mb(1_258_291)));
        Assert.Equal("Fachrichtungs-Paket DPA", PaketTexte.Format(PaketTexte.FrName, PaketTexte.Fr("dpa")));
    }

    // ---- Lernstand bleibt bytegleich, ruhen und zurueckkommen --------------------------------------------------------------

    [Fact]
    public async Task A3_Lernstand_Bytegleich_BeiLadenEntfernenFehler_RuhenUndZurueck()
    {
        var bisher = Lerninhalte.Laden(Pfade.Inhalte);
        var ae = Laden(RepoOrdner.Value, ["si", "ae"]).Ap2SzenarienFuer("ae");
        var pfad = Path.Combine(_tmp, "lernplattform.db");
        var szenario = ae[3];
        {
            var lernstand = new Lernstand(pfad);
            Assert.True(lernstand.Schreiben("INSERT INTO scenario_events (timestamp, scenario_index, title, theme, correct) VALUES ($z, $i, $t, 'x', 1)",
                ("$z", "2026-11-18T09:00:00"), ("$i", (long)(szenario.Nummer - 1)), ("$t", szenario.Titel ?? "")));
        }
        var vorher = Sha(File.ReadAllBytes(pfad));
        var quelle = Path.Combine(_tmp, "quelle");
        Directory.CreateDirectory(quelle);
        File.Copy(Path.Combine(Echt, "lernen_ae_1.fipaket"), Path.Combine(quelle, "lernen_ae_1.fipaket"));
        File.WriteAllBytes(Path.Combine(quelle, "lernen_dpa_1.fipaket"), [1, 2, 3]);   // Fehler: kein Archiv
        // die App ohne eingebaute AE-Inhalte
        var ohneOrdner = new LernenOrdner(MitPlatzhaltern("ae"));
        var v = new PaketVerwaltung(new PaketAblage(Path.Combine(_tmp, "daten"), ohneOrdner), () => new OrdnerQuelle(quelle), App);
        // ruhen: ohne Paket fehlt das Szenario in der Sicht, der Hinweis nennt AE, der Lernstand bleibt
        {
            var lernstand = new Lernstand(pfad);
            var ohne = Lerninhalte.MitLernen(bisher, ohneOrdner, ["si", "ae"]);
            Assert.DoesNotContain(ohne.Fuer("ae").Ap2Szenarien, e => e.Nummer == szenario.Nummer);
            Assert.Equal(["ae"], FachrichtungsPaket.FehlenNachAbgleich(lernstand, ohne, ohneOrdner, "si"));
            Assert.Empty(Bereinigung.Suchen(lernstand, ohne));
        }
        await v.SuchenAsync(TestContext.Current.CancellationToken);
        Assert.Null(await v.LadenAsync(v.Angebote.Single(a => a.Kennung == "lernen_ae"), null, TestContext.Current.CancellationToken));
        Assert.NotNull(await v.LadenAsync(v.Angebote.Single(a => a.Kennung == "lernen_dpa"), null, TestContext.Current.CancellationToken));
        Assert.Equal(vorher, Sha(File.ReadAllBytes(pfad)));
        // zurueck: mit dem Paket ist das Szenario wieder da, mit derselben Nummer, kein Hinweis mehr
        var pakete = v.Fachrichtungen();
        Assert.Equal(["ae"], pakete.Select(p => p.Fachrichtung));
        var mitOrdner = ohneOrdner.MitPaketen(pakete);
        {
            var lernstand = new Lernstand(pfad);
            var mit = Lerninhalte.MitLernen(bisher, mitOrdner, ["si", "ae"]);
            Assert.Contains(mit.Fuer("ae").Ap2Szenarien, e => e.Nummer == szenario.Nummer && e.Titel == szenario.Titel);
            Assert.Empty(FachrichtungsPaket.FehlenNachAbgleich(lernstand, mit, mitOrdner, "ae"));
        }
        Assert.True(v.Entfernen("lernen_ae"));
        Assert.Empty(v.Fachrichtungen());
        Assert.Equal(vorher, Sha(File.ReadAllBytes(pfad)));
        // Gegenprobe: eine Stelle hinter allem, was das Geraet kennt (anderes Geraet mit neuerem Paket), meldet die Sicht
        {
            var lernstand = new Lernstand(pfad);
            Assert.True(lernstand.Schreiben("INSERT INTO project_events (timestamp, project_index, title, category, correct) VALUES ('t', $i, 'neu', 'x', 1)",
                ("$i", (long)(bisher.Testprojekte.Count + RepoOrdner.Value.Laenge("projektarbeiten") + 5))));
            var alle = Lerninhalte.MitLernen(bisher, RepoOrdner.Value, ["si", "dv"]);
            Assert.Equal(["dv"], FachrichtungsPaket.FehlenNachAbgleich(lernstand, alle, RepoOrdner.Value, "dv"));
        }
    }

    // ---- (5) Zeilen, Hinweis, Sperre ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A5_Zeilen_JeFachrichtung_HinweisNeuerStand_NurAufKnopfdruck()
    {
        var quelle = Path.Combine(_tmp, "quelle5");
        Directory.CreateDirectory(quelle);
        var v = new PaketVerwaltung(new PaketAblage(Path.Combine(_tmp, "daten5"), RepoOrdner.Value), () => new OrdnerQuelle(quelle), App);
        var zeilen = v.Zeilen();
        Assert.Equal(["Fachrichtungs-Paket AE", "Fachrichtungs-Paket DPA", "Fachrichtungs-Paket DV"], zeilen.Select(z => z.Name));
        Assert.All(zeilen, z => Assert.Equal((PaketZustand.Geladen, PaketTexte.ZustandEingebaut, 0), (z.Zustand, z.ZustandText, z.Knoepfe.Count)));
        Assert.All(zeilen, z => Assert.False(z.ImFahrplan));
        Assert.Empty(v.NeueFachrichtungsStaende());
        // nach einer Suche mit Angebot: Hinweis „neuer Stand da“ und Knopf Laden; nichts ist geladen
        File.Copy(Path.Combine(Echt, "lernen_ae_1.fipaket"), Path.Combine(quelle, "lernen_ae_1.fipaket"));
        await v.SuchenAsync(TestContext.Current.CancellationToken);
        var ae = v.Zeilen().Single(z => z.Kennung == "lernen_ae");
        Assert.Equal((PaketZustand.NeuerStand, PaketTexte.Format(PaketTexte.ZustandEingebautNeu, 1)), (ae.Zustand, ae.ZustandText));
        Assert.Equal([PaketKnopf.Laden], ae.Knoepfe);
        Assert.Equal([("ae", 1L)], v.NeueFachrichtungsStaende().Select(n => (n.Fachrichtung, n.Angebot.Stand)));
        Assert.Empty(v.Fachrichtungen());
        Assert.False(Directory.Exists(Path.Combine(_tmp, "daten5", "pakete")));
        Assert.Null(await v.LadenAsync(ae.Angebot!, null, TestContext.Current.CancellationToken));
        Assert.Equal((PaketZustand.Geladen, PaketTexte.Format(PaketTexte.ZustandGeladen, 1)),
            v.Zeilen().Single(z => z.Kennung == "lernen_ae") is var g ? (g.Zustand, g.ZustandText) : default);
        Assert.Empty(v.NeueFachrichtungsStaende());
        // ohne eingebaute Inhalte: „Nicht geladen“
        var ohne = new PaketVerwaltung(new PaketAblage(Path.Combine(_tmp, "daten6"), new LernenOrdner(MitPlatzhaltern("dv"))), () => new OrdnerQuelle(quelle), App);
        Assert.Equal(PaketTexte.ZustandNicht, ohne.Zeilen().Single(z => z.Kennung == "lernen_dv").ZustandText);
        // Gegenprobe: ohne Inhalte der App (Werkzeug, alte Tests) keine Zeilen fuer Fachrichtungen
        Assert.DoesNotContain(new PaketVerwaltung(new PaketAblage(Path.Combine(_tmp, "daten7")), () => new OrdnerQuelle(quelle), App).Zeilen(),
            z => z.Kennung.StartsWith("lernen_", StringComparison.Ordinal) && z.Angebot is null);
    }

    [Fact]
    public void A5_EntfernenGesperrt_SolangeEinSatzOffen_SatzFehltText()
    {
        Assert.Equal("Du schreibst gerade einen AE-Prüfungssatz. Erst beenden oder abbrechen.", FachrichtungsPaket.EntfernenSperre("lernen_ae", "ae_b1_02"));
        Assert.Null(FachrichtungsPaket.EntfernenSperre("lernen_ae", "dpa_b1_02"));
        Assert.Null(FachrichtungsPaket.EntfernenSperre("lernen_ae", null));
        Assert.Null(FachrichtungsPaket.EntfernenSperre("lernen_ae", "alle_ap1_01"));
        Assert.Null(FachrichtungsPaket.EntfernenSperre("probe_klein", "ae_b1_02"));
        Assert.Equal(PaketTexte.Format(PaketTexte.FrSatzFehlt, "DV"), FachrichtungsPaket.SatzFehltText("dv_b2_05"));
        Assert.Null(FachrichtungsPaket.SatzFehltText("si_b2_05"));
        // die Sperre greift in der Verwaltung: Entfernen tut nichts
        var v = new PaketVerwaltung(new PaketAblage(Path.Combine(_tmp, "daten8"), RepoOrdner.Value), () => new OrdnerQuelle(_tmp), App)
        {
            EntfernenSperre = k => FachrichtungsPaket.EntfernenSperre(k, "dpa_b2_01"),
        };
        Assert.NotNull(v.EntfernenGesperrt("lernen_dpa"));
        Assert.False(v.Entfernen("lernen_dpa"));
    }

    [Fact]
    public void A5_OffenerSatzFehlt_ZustandBleibt_HinweisNenntPaket()
    {
        var gespeichert = new JsonObject
        {
            [EchtAblage.Schluessel] = new JsonObject { ["art"] = "echt_b1", ["satz"] = "ae_b1_01", ["phase"] = "laeuft", ["start_utc"] = 0.0 },
        };
        var vorher = gespeichert.ToJsonString();
        var ablage = new EchtAblage(() => gespeichert, aendern => { aendern(gespeichert); return true; });
        var ohne = new LernenOrdner(MitPlatzhaltern("ae"));
        var ablauf = new EchtAblauf(() => EchtSaetze.Laden(ohne.Saetze()), ablage, () => throw new InvalidOperationException(),
            () => DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc);
        var schritt = ablauf.Zeigen();
        Assert.Equal(EchtAnsicht.Auswahl, schritt.Ansicht);
        Assert.Equal(PaketTexte.Format(PaketTexte.FrSatzFehlt, "AE"), schritt.Hinweis);
        Assert.Equal(vorher, gespeichert.ToJsonString());
    }

    [Theory]
    [InlineData("ae")]
    [InlineData("dpa")]
    [InlineData("dv")]
    public void A1_EchteTestdateien_JeFachrichtung_Gut(string fr)
    {
        var e = Paket.PruefenArchiv(File.OpenRead(Path.Combine(Echt, Paket.Dateiname("lernen_" + fr, 1))), App, RepoOrdner.Value);
        Assert.True(e.Gut, e.Grund);
        Assert.Equal(fr, e.Lernen!.Fachrichtung);
        // Gegenprobe: eine aeltere App (A 0.90.0) nimmt es nicht (braucht_app), Lern-Pakete gibt es erst ab A 0.91.0
        Assert.Equal(PaketTexte.Format(PaketTexte.AppAlt, App), Paket.PruefenArchiv(File.OpenRead(Path.Combine(Echt, Paket.Dateiname("lernen_" + fr, 1))), "A 0.90.0").Grund);
    }

    /// <summary>
    /// Belastungsprobe (Kurzplan P12-180 Abschnitt 5): Laden und Entfernen eines Fachrichtungs-Pakets, waehrend gelernt wird (Quiz
    /// liest die Sicht) und eine Pruefung im echten Format offen ist: nie ein Absturz, jede Sicht gleich, der offene Satz immer
    /// da, sein gespeicherter Zustand unberuehrt.
    /// </summary>
    [Fact]
    public async Task Belastung_LadenEntfernen_WaehrendQuizUndPruefung()
    {
        var paket = Pruefen(Schnitt("ae"));
        Assert.True(paket.Gut, paket.Grund);
        var ohne = RepoOrdner.Value;
        var mit = ohne.MitPaketen([paket.Lernen!]);
        var bisher = Lerninhalte.Laden(Pfade.Inhalte);
        var soll = string.Join("\n", Abdruck(Lerninhalte.MitLernen(bisher, ohne, ["si", "ae"]), "ae"));
        var offen = new JsonObject { ["art"] = "echt_ae_b1", ["satz"] = "ae_b1_01", ["eingaben"] = new JsonObject { ["1a"] = "Antwort" } };
        var offenVorher = offen.ToJsonString();
        var sicht = Lerninhalte.MitLernen(bisher, ohne, ["si", "ae"]).Fuer("ae");
        using var stopp = new CancellationTokenSource();
        // Weisung P12-310 (Lauf 1093): das Laden startet erst, wenn das Quiz nachweislich laeuft (Signal nach der ersten vollen
        // Runde); sonst konnten die Lade-Aufgaben auf einem 2-Kern-Rechner fertig sein, bevor das Quiz anfing (n = 0, Test prueft
        // nichts). Das Quiz laeuft bis nach Task.WhenAll; haengt es, macht die Zeitgrenze rot statt zu warten.
        var quizLaeuft = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var quiz = Task.Run(() =>
        {
            var n = 0;
            do
            {
                foreach (var f in sicht.Quizfragen.Take(200))
                {
                    n += f.Frage?.Length ?? 0;
                }
                quizLaeuft.TrySetResult();
            }
            while (!stopp.IsCancellationRequested);
            return n;
        }, TestContext.Current.CancellationToken);
        await quizLaeuft.Task.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        var laeufe = Enumerable.Range(0, 12).Select(i => Task.Run(() =>
        {
            var ordner = i % 2 == 0 ? mit : ohne;
            var inhalte = Lerninhalte.MitLernen(bisher, ordner, ["si", "ae"]);
            var bestand = EchtSaetze.Laden(ordner.Saetze());
            return (string.Join("\n", Abdruck(inhalte, "ae")), bestand.Satz((string?)offen["satz"]) is not null, bestand.Fehler.Count);
        }, TestContext.Current.CancellationToken)).ToList();
        var ergebnisse = await Task.WhenAll(laeufe);
        await stopp.CancelAsync();
        Assert.True(await quiz > 0);
        Assert.All(ergebnisse, e => Assert.Equal((soll, true, 0), e));
        Assert.Equal(offenVorher, offen.ToJsonString());
    }

    /// <summary>
    /// Messpunkt „Fachrichtungs-Paket einsetzen“ (Nico 4a): nach Laden und Entfernen wandelt <c>MitPaketNeu</c> nur die Eintraege
    /// der einen Fachrichtung neu um. Das Ergebnis ist fuer jede Sicht gleich dem ganzen Neubau (Gegenprobe: der Schalter baut
    /// alles neu und ergibt dasselbe; ein Neubau ohne neuen Ausschluss waere nicht gleich, siehe A3).
    /// </summary>
    [Theory]
    [InlineData("ae")]
    [InlineData("dpa")]
    [InlineData("dv")]
    public void MitPaketNeu_GleichDemGanzenNeubau(string fr)
    {
        var paket = Pruefen(Schnitt(fr));
        Assert.True(paket.Gut, paket.Grund);
        var ohne = RepoOrdner.Value;
        var mit = ohne.MitPaketen([paket.Lernen!]);
        var bisher = Lerninhalte.Laden(Pfade.Inhalte);
        string[] geladen = ["si", "ae", "dpa", "dv"];
        string Alle(Lerninhalte l) => string.Join("\n", geladen.SelectMany(s => Abdruck(l, s)));
        var vorher = Lerninhalte.MitLernen(bisher, ohne, geladen);
        var geladenMit = vorher.MitPaketNeu(mit, fr);
        Assert.Equal(Alle(Lerninhalte.MitLernen(bisher, mit, geladen)), Alle(geladenMit));
        var entfernt = geladenMit.MitPaketNeu(ohne, fr);
        Assert.Equal(Alle(vorher), Alle(entfernt));
        // wiederverwendet: die Eintraege der anderen Fachrichtungen sind dieselben Objekte
        var andere = geladen.First(g => g != fr && g != "si");
        Assert.Same(vorher.Neue[InhaltArt.Quizfrage].First(e => e.Zugehoerigkeit == andere),
            geladenMit.Neue[InhaltArt.Quizfrage].First(e => e.Zugehoerigkeit == andere));
        Lerninhalte.PaketNeuGanzFuerTests = true;
        try
        {
            var ganz = vorher.MitPaketNeu(mit, fr);
            Assert.Equal(Alle(geladenMit), Alle(ganz));
            Assert.NotSame(vorher.Neue[InhaltArt.Quizfrage].First(e => e.Zugehoerigkeit == andere),
                ganz.Neue[InhaltArt.Quizfrage].First(e => e.Zugehoerigkeit == andere));
        }
        finally
        {
            Lerninhalte.PaketNeuGanzFuerTests = false;
        }
    }

    // ---- Helfer ---------------------------------------------------------------------------------------------------------------

    private static int Stelle(string name, string fr, int nr)
    {
        var liste = Liste(JsonNode.Parse(Repo(name)!)!, name);
        return Enumerable.Range(0, liste.Count).Where(i => (string?)liste[i]!["fachrichtung"] == fr).ElementAt(nr);
    }

    private static int SatzStelle(string fr, int nr) => Stelle(EchtSaetze.DateiName, fr, nr);

    /// <summary>App-Dateien mit einem weiteren Eintrag von <paramref name="fr"/> hinten in <paramref name="name"/>.</summary>
    private static Func<string, byte[]?> Angehaengt(string name, string fr)
    {
        var liste = JsonNode.Parse(Repo(name)!)!.AsArray();
        var vorlage = liste.First(e => (string?)e!["fachrichtung"] == "ae")!.DeepClone().AsObject();
        vorlage["q"] = "Neue Karte " + fr + " (nur Test)";
        vorlage["fachrichtung"] = fr;
        liste.Add(vorlage);
        var bytes = Encoding.UTF8.GetBytes(liste.ToJsonString());
        return n => n == name ? bytes : Repo(n);
    }
}
