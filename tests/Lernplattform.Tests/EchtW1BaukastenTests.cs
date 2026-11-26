using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// A 1.8.0 "Pruefung echt W1" (Weisungen P12-371 und P12-372, Plan P12-232, Nico 1a-5a): Klassen- und Use-Case-Diagramm im
/// Baukasten, Format 4 mit neuer Fassung eines Satzes, fuenf neue AE-Fassungen. Abnahmesaetze: (1) Klassendiagramm mit allen
/// vier Beziehungen, Multiplizitaeten, Attributen und Methoden; (2) Use-Case-Diagramm mit «include», «extend» und
/// Generalisierung in richtiger Pfeilrichtung, Systemgrenze vorgegeben; (3) das Programm vergleicht gruen/rot mit Klartext, die
/// Punkte bleiben Haekchen; der Bewertungskopf nennt bei AE den AE-Bereich; (4) fuenf neue Fassungen nur angehaengt, die alten
/// bytegleich, kein Pruefungsthema verschwindet, Gewichte in den Grenzen; (5) Format 3 bleibt ladbar, neuere Nummer = klarer
/// Fehler, Kennung doppelt ohne hoehere Fassung = Fehler; ein offener Lauf behaelt seine alte Antwort gespeichert. Jede Regel
/// mit Gegenprobe.
/// </summary>
public class EchtW1BaukastenTests
{
    private static readonly string Format3 = Path.Combine(Pfade.Wurzel, "tests", "Lerninhalte", "echt", "pruefungssaetze_format3.json");

    /// <summary>Laenge und SHA-256 der Datei (Kopf als Format 3) bis vor das schliessende "\n ]\n}" am Stand A 1.7.0 (main 8e6fcb5).</summary>
    private const int VorherLaenge = 2387597;
    private const string VorherSha = "7e2280523d6bcba3ee4c55643e45bf428322f40a4a3ca3ed2aba395696f5eb06";

    /// <summary>Die fuenf neuen Fassungen: Satz, Aufgabe, Teil, Diagramm (Weisung P12-372, Frage 1 = a mit Bedingung).</summary>
    public static readonly (string Kennung, int Aufgabe, string Nr, string Diagramm)[] Neu =
    [
        ("ae_b1_06", 4, "a", EchtZeichnung.UseCase),
        ("ae_b1_07", 2, "c", EchtZeichnung.Klasse),
        ("ae_b1_08", 3, "a", EchtZeichnung.Klasse),
        ("ae_b1_09", 2, "ca", EchtZeichnung.Klasse),
        ("ae_b1_10", 1, "b", EchtZeichnung.UseCase),
    ];

    /// <summary>
    /// Stand dieser Welle: die ersten 62 Saetze der Datei (bis zu den fuenf Fassungen dieser Welle). Seit A 1.12.0 haengen dahinter
    /// die Struktogramm-Fassungen (Weisung P12-390, eigene Tests in <see cref="EchtStruktogrammTests"/>); so prueft diese Welle
    /// weiter genau ihren Stand.
    /// </summary>
    private static EchtBestand App => Bis(62);

    /// <summary>Die Datei ohne die neuen Fassungen (die ersten 57 Saetze): die alten Fassungen, wie sie A 1.7.0 lud.</summary>
    private static EchtBestand OhneNeueFassungen() => Bis(57);

    /// <summary>Die Datei mit nur den ersten <paramref name="anzahl"/> Saetzen (sonst unveraendert), geladen wie in der App.</summary>
    internal static EchtBestand Bis(int anzahl)
    {
        var wurzel = JsonNode.Parse(File.ReadAllBytes(EchtTestDatei.Datei))!.AsObject();
        var liste = wurzel["saetze"]!.AsArray();
        while (liste.Count > anzahl)
        {
            liste.RemoveAt(liste.Count - 1);
        }
        return EchtSaetze.Laden(Encoding.UTF8.GetBytes(wurzel.ToJsonString()), EchtTestDatei.Ausgeschlossen());
    }

    private static JsonObject Roh(string kennung, int fassung) => JsonNode.Parse(File.ReadAllBytes(EchtTestDatei.Datei))!["saetze"]!.AsArray()
        .Select(s => s!.AsObject()).Single(s => (string?)s["kennung"] == kennung && ((int?)s["fassung"] ?? 1) == fassung).DeepClone().AsObject();

    private static byte[] Datei(int format, params JsonObject[] saetze) =>
        Encoding.UTF8.GetBytes(new JsonObject { ["format"] = format, ["saetze"] = new JsonArray([.. saetze.Select(s => s.DeepClone())]) }.ToJsonString());

    private static EchtDiagramm Lesen(string art, string json, bool vollstaendig = true) =>
        EchtZeichnung.Diagramm(JsonNode.Parse(json), art, vollstaendig, "Test");

    private static string Fehler(string art, string json, bool vollstaendig = true)
    {
        var f = Assert.Throws<EchtLadefehler>(() => Lesen(art, json, vollstaendig));
        return f.Message;
    }

    // ---- (5) Format 4, Fassung ------------------------------------------------------------------------------------------------

    [Fact]
    public void Format3_EchteTestdateiLaedt_FassungUndKlasseErstAbFormat4_NeueresFormatKlar()
    {
        var alt = File.ReadAllBytes(Format3);
        Assert.Equal(3, (int)JsonNode.Parse(alt)!["format"]!);
        var bestand = EchtSaetze.Laden(alt);
        Assert.Empty(bestand.Fehler);
        Assert.Equal(["alle_ap1_01", "si_b1_01", "ae_b1_06", "ae_b2_06"], bestand.Saetze.Select(s => s.Kennung));
        Assert.All(bestand.Saetze, s => Assert.Equal(1, s.Fassung));
        // eine neue Fassung in einer Datei mit Format 3: klare Meldung, die alte Fassung bleibt geladen (nie still)
        var wurzel = JsonNode.Parse(alt)!.AsObject();
        wurzel["saetze"]!.AsArray().Add(Roh("ae_b1_06", 2));
        var mitFassung = EchtSaetze.Laden(Encoding.UTF8.GetBytes(wurzel.ToJsonString()));
        Assert.Contains(mitFassung.Fehler, f => f.Kennung == "ae_b1_06" && f.Meldung == "\"fassung\" erst ab Format 4");
        Assert.Equal(1, mitFassung.Satz("ae_b1_06")!.Fassung);
        // Gegenprobe: dieselbe Datei als Format 4 laedt die neue Fassung an der Stelle der alten
        wurzel["format"] = 4;
        var neu = EchtSaetze.Laden(Encoding.UTF8.GetBytes(wurzel.ToJsonString()));
        Assert.Empty(neu.Fehler);
        Assert.Equal(["alle_ap1_01", "si_b1_01", "ae_b1_06", "ae_b2_06"], neu.Saetze.Select(s => s.Kennung));
        Assert.Equal(2, neu.Satz("ae_b1_06")!.Fassung);
        // ein Klassendiagramm ohne Feld "fassung" in Format 3: klare Meldung; als Format 4 gut
        var klasse = Roh("ae_b1_07", 2);
        klasse.Remove("fassung");
        var paar = Datei(3, klasse, Roh("ae_b2_07", 1));
        Assert.Contains(EchtSaetze.Laden(paar).Fehler, f => f.Kennung == "ae_b1_07" && f.Meldung.EndsWith("Diagramm \"klasse\" erst ab Format 4", StringComparison.Ordinal));
        klasse = Roh("ae_b1_07", 2);
        klasse.Remove("fassung");
        Assert.Empty(EchtSaetze.Laden(Datei(4, klasse, Roh("ae_b2_07", 1))).Fehler);
        // neuere Formatnummer: klarer Fehler der ganzen Datei, nichts geladen
        // (seit A 1.12.0 kennt das Programm Format 5, Weisung P12-390; die naechste Nummer bleibt ein klarer Fehler)
        var sechs = EchtSaetze.Laden(Datei(6, Roh("alle_ap1_01", 1)));
        Assert.Empty(sechs.Saetze);
        Assert.Equal("Format 6 ist neuer als dieses Programm (kennt Format 5); bitte das Programm aktualisieren", Assert.Single(sechs.Fehler).Meldung);
        Assert.Equal(5, EchtSaetze.Format);
    }

    [Fact]
    public void Fassung_KennungDoppeltNurMitHoehererFassung_GegenProben()
    {
        var a = Roh("alle_ap1_01", 1);
        JsonObject Mit(int? fassung, Action<JsonObject>? aendern = null)
        {
            var s = a.DeepClone().AsObject();
            if (fassung is { } f)
            {
                s["fassung"] = f;
            }
            aendern?.Invoke(s);
            return s;
        }
        Assert.Equal("Kennung doppelt", Assert.Single(EchtSaetze.Laden(Datei(4, Mit(null), Mit(null))).Fehler).Meldung);
        Assert.Equal("Kennung doppelt", Assert.Single(EchtSaetze.Laden(Datei(4, Mit(null), Mit(1))).Fehler).Meldung);
        Assert.Equal("Fassung 2 ist nicht höher als die vorige Fassung 2", Assert.Single(EchtSaetze.Laden(Datei(4, Mit(null), Mit(2), Mit(2))).Fehler).Meldung);
        Assert.Equal("Fassung 2 ist nicht höher als die vorige Fassung 3", Assert.Single(EchtSaetze.Laden(Datei(4, Mit(null), Mit(3), Mit(2))).Fehler).Meldung);
        Assert.Equal("\"fassung\" muss eine ganze Zahl ab 1 sein", Assert.Single(EchtSaetze.Laden(Datei(4, Mit(0))).Fehler).Meldung);
        Assert.Equal("\"fassung\" muss eine ganze Zahl ab 1 sein", Assert.Single(EchtSaetze.Laden(Datei(4, Mit(null, s => s["fassung"] = "2"))).Fehler).Meldung);
        // gut: hoehere Fassung ersetzt die alte an ihrer Stelle; nur der geaenderte Teil traegt die neue Fassung
        var b = Roh("si_b1_01", 1);
        var neu = Mit(2, s => s["aufgaben"]![0]!["teile"]![1]!["text"] = new JsonArray("Geänderter Text der Teilaufgabe (nur Test)."));
        var bestand = EchtSaetze.Laden(Datei(4, Mit(null), b, neu));
        Assert.Empty(bestand.Fehler);
        Assert.Equal(["alle_ap1_01", "si_b1_01"], bestand.Saetze.Select(s => s.Kennung));
        var satz = bestand.Satz("alle_ap1_01")!;
        Assert.Equal(2, satz.Fassung);
        var teile = satz.Aufgaben.SelectMany((x, n) => x.Teile.Select(t => (n, t))).ToList();
        Assert.Equal([(0, teile.Where(x => x.n == 0).ElementAt(1).t.Nr)], teile.Where(x => x.t.Fassung == 2).Select(x => (x.n, x.t.Nr)));
        Assert.All(teile.Where(x => x.t.Fassung != 2), x => Assert.Equal(1, x.t.Fassung));
        // Fassung 3 ohne weitere Aenderung: der in Fassung 2 geaenderte Teil behaelt 2, die anderen 1
        var drei = EchtSaetze.Laden(Datei(4, Mit(null), neu, Mit(3, s => s["aufgaben"]![0]!["teile"]![1]!["text"] = new JsonArray("Geänderter Text der Teilaufgabe (nur Test)."))));
        Assert.Empty(drei.Fehler);
        Assert.Equal(1, drei.Saetze.Single().Aufgaben.SelectMany(x => x.Teile).Count(t => t.Fassung == 2));
        Assert.Equal(3, drei.Saetze.Single().Fassung);
        // Gegenprobe: ohne Aenderung hat kein Teil die neue Fassung
        Assert.All(EchtSaetze.Laden(Datei(4, Mit(null), Mit(2))).Saetze.Single().Aufgaben.SelectMany(x => x.Teile), t => Assert.Equal(1, t.Fassung));
    }

    // ---- (4) die fuenf neuen Fassungen ----------------------------------------------------------------------------------------

    [Fact]
    public void FuenfFassungen_NurAngehaengt_AlteBytegleich_Format4()
    {
        var echt = File.ReadAllBytes(EchtTestDatei.Datei);
        Assert.Equal(EchtSaetze.Format, (int)JsonNode.Parse(echt)!["format"]!);
        var roh = EchtTestDatei.AlsFormat(echt, 3);
        Assert.True(roh.Length > VorherLaenge);
        Assert.Equal(VorherSha, Convert.ToHexStringLower(SHA256.HashData(roh.AsSpan(0, VorherLaenge))));
        Assert.StartsWith(",\n  {\n   \"kennung\": \"ae_b1_06\",\n   \"fassung\": 2,", Encoding.UTF8.GetString(roh, VorherLaenge, 80), StringComparison.Ordinal);
        // Gegenprobe: ein geaendertes Zeichen vorn faellt auf
        roh[200] ^= 1;
        Assert.NotEqual(VorherSha, Convert.ToHexStringLower(SHA256.HashData(roh.AsSpan(0, VorherLaenge))));
        var liste = JsonNode.Parse(echt)!["saetze"]!.AsArray();
        Assert.Equal(63, liste.Count); // seit A 1.12.0 eine Struktogramm-Fassung dahinter (ae_b2_08, P12-393; EchtStruktogrammTests)
        Assert.Equal(Neu.Select(n => n.Kennung), liste.Skip(57).Take(5).Select(s => (string?)s!["kennung"]));
        Assert.All(liste.Skip(57), s => Assert.Equal(2, (int)s!["fassung"]!));
        Assert.All(liste.Take(57), s => Assert.Null(s!["fassung"]));
        // der Bestand: 57 Saetze wie vorher, die fuenf an ihrer alten Stelle mit Fassung 2
        var app = App;
        Assert.Empty(app.Fehler);
        Assert.Equal(OhneNeueFassungen().Saetze.Select(s => s.Kennung), app.Saetze.Select(s => s.Kennung));
        Assert.Equal(Neu.Select(n => n.Kennung).Order(StringComparer.Ordinal), app.Saetze.Where(s => s.Fassung == 2).Select(s => s.Kennung).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void FuenfFassungen_GenauDieseTeile_GleichePunkte_SonstNichtsGeaendert()
    {
        var app = App;
        var alt = OhneNeueFassungen();
        var geaendert = app.Saetze.SelectMany(s => s.Aufgaben.SelectMany((a, n) => a.Teile.Where(t => t.Fassung == 2)
            .Select(t => (s.Kennung, n + 1, t.Nr, t.Antwort.Diagramm)))).ToList();
        // Gegenpruefung 1 (Fund 2): in ae_b1_09 ist 2c geteilt in 2ca (Zeichnung) und 2cb (Abhaengigkeit, Text), gleiche Punkte
        List<(string, int, string, string)> soll = [.. Neu];
        soll.Insert(soll.IndexOf(("ae_b1_09", 2, "ca", EchtZeichnung.Klasse)) + 1, ("ae_b1_09", 2, "cb", ""));
        Assert.Equal(soll, geaendert);
        foreach (var (kennung, aufgabe, nr, diagramm) in Neu)
        {
            var neu = app.Satz(kennung)!.Aufgaben[aufgabe - 1].Teile.Single(t => t.Nr == nr);
            var vorher = alt.Satz(kennung)!.Aufgaben[aufgabe - 1].Teile.Single(t => t.Nr == nr[..1]);
            Assert.Equal(vorher.Punkte, app.Satz(kennung)!.Aufgaben[aufgabe - 1].Teile.Where(t => t.Nr[..1] == nr[..1]).Sum(t => t.Punkte));
            Assert.Equal(EchtSaetze.FormZeichnen, neu.Antwort.Form);
            Assert.Equal(diagramm, neu.Antwort.Diagramm);
            Assert.Equal("diagramm", neu.Gebiet);
            Assert.Contains(neu.Operator, new[] { "zeichnen", "erstellen" });
            Assert.Equal(neu.Punkte, neu.Hinweise.Sum(h => h.Punkte));
            Assert.DoesNotContain("auf Papier", string.Join(" ", neu.Text), StringComparison.Ordinal);
            Assert.NotEqual(EchtSaetze.FormZeichnen, vorher.Antwort.Form);
            if (diagramm == EchtZeichnung.UseCase)
            {
                // Systemgrenze vorgegeben, alle drei Beziehungsarten im Musterbild
                Assert.NotNull(neu.Antwort.Vorgabe!.Grenze);
                Assert.Equal(["assoziation", "extend", "generalisierung", "include"],
                    neu.Antwort.Muster!.Verbindungen.Select(v => v.Typ).Distinct().Order(StringComparer.Ordinal));
            }
        }
        // die drei Klassendiagramme zusammen zeigen alle vier Beziehungen, Attribute, Methoden und Multiplizitaeten
        var klassen = Neu.Where(n => n.Diagramm == EchtZeichnung.Klasse)
            .Select(n => app.Satz(n.Kennung)!.Aufgaben[n.Aufgabe - 1].Teile.Single(t => t.Nr == n.Nr).Antwort.Muster!).ToList();
        Assert.Equal(["assoziation", "komposition"], klassen.SelectMany(d => d.Verbindungen).Select(v => v.Typ).Distinct().Order(StringComparer.Ordinal));
        Assert.All(klassen, d => Assert.All(d.Verbindungen, v => Assert.True(v.MultVon.Length > 0 && v.MultNach.Length > 0)));
        Assert.All(klassen, d => Assert.Contains(d.Elemente, e => e.AttributZeilen.Count > 0 && e.MethodenZeilen.Count > 0));
        // ausser dem Teil, der Einleitung von Aufgabe 4 und Anlage 9 in ae_b1_06 ist jeder Satz gleich seiner alten Fassung
        foreach (var (kennung, aufgabe, nr, _) in Neu)
        {
            Assert.Equal(Abweichungen(Roh(kennung, 1), Roh(kennung, 2)),
                kennung switch
                {
                    "ae_b1_06" => ["anlagen/9", "aufgaben/" + aufgabe + "/" + nr, "aufgaben/" + aufgabe + "/einleitung", "fassung"],
                    "ae_b1_09" => ["aufgaben/2/c", "aufgaben/2/ca", "aufgaben/2/cb", "fassung"],
                    _ => ["aufgaben/" + aufgabe + "/" + nr, "fassung"],
                });
        }
        // Gegenprobe: eine Aenderung an einem anderen Teil faellt auf
        var anders = Roh("ae_b1_07", 2);
        anders["aufgaben"]![0]!["teile"]![0]!["punkte"] = 5;
        Assert.Contains("aufgaben/1/a", Abweichungen(Roh("ae_b1_07", 1), anders));
    }

    /// <summary>Stellen, an denen sich zwei Satz-Fassungen unterscheiden: Feld, Anlage (Nummer), Aufgabe/Einleitung, Aufgabe/Teil.</summary>
    private static List<string> Abweichungen(JsonObject alt, JsonObject neu)
    {
        var funde = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var feld in alt.Select(p => p.Key).Union(neu.Select(p => p.Key)).Where(k => k is not ("aufgaben" or "anlagen")))
        {
            if (!JsonNode.DeepEquals(alt[feld], neu[feld]))
            {
                funde.Add(feld);
            }
        }
        var anlagenAlt = alt["anlagen"]!.AsArray();
        var anlagenNeu = neu["anlagen"]!.AsArray();
        for (var i = 0; i < Math.Max(anlagenAlt.Count, anlagenNeu.Count); i++)
        {
            if (i >= anlagenAlt.Count || i >= anlagenNeu.Count || !JsonNode.DeepEquals(anlagenAlt[i], anlagenNeu[i]))
            {
                funde.Add("anlagen/" + (string?)(i < anlagenNeu.Count ? anlagenNeu[i] : anlagenAlt[i])!["nr"]);
            }
        }
        var aufgabenAlt = alt["aufgaben"]!.AsArray();
        var aufgabenNeu = neu["aufgaben"]!.AsArray();
        Assert.Equal(aufgabenAlt.Count, aufgabenNeu.Count);
        for (var n = 0; n < aufgabenAlt.Count; n++)
        {
            var a = aufgabenAlt[n]!.AsObject();
            var b = aufgabenNeu[n]!.AsObject();
            foreach (var feld in a.Select(p => p.Key).Union(b.Select(p => p.Key)).Where(k => k != "teile"))
            {
                if (!JsonNode.DeepEquals(a[feld], b[feld]))
                {
                    funde.Add("aufgaben/" + (n + 1) + "/" + feld);
                }
            }
            var ta = a["teile"]!.AsArray();
            var tb = b["teile"]!.AsArray();
            // je Nummer: fehlt in einer Fassung oder ist anders (eine geteilte Teilaufgabe nennt alte und neue Nummern)
            var nachNr = (JsonArray teile) => teile.ToDictionary(t => (string)t!["nr"]!, t => t, StringComparer.Ordinal);
            var na = nachNr(ta);
            var nb = nachNr(tb);
            foreach (var nr in na.Keys.Union(nb.Keys))
            {
                if (!na.TryGetValue(nr, out var x) || !nb.TryGetValue(nr, out var y) || !JsonNode.DeepEquals(x, y))
                {
                    funde.Add("aufgaben/" + (n + 1) + "/" + nr);
                }
            }
        }
        return [.. funde];
    }

    [Fact]
    public void KeinThemaVerschwindet_StakeholderUndLastenheft_GewichteInDenGrenzen()
    {
        var app = App;
        var angeboten = app.Fuer(EchtSaetze.B1, "ae").Concat(app.Fuer(EchtSaetze.B2, "ae")).ToList();
        Assert.Equal(10, angeboten.Count);
        static IEnumerable<(string Kennung, string Teil, string Text)> Teile(IEnumerable<EchtSatz> saetze) => saetze.SelectMany(s =>
            s.Aufgaben.SelectMany((a, n) => a.Teile.Select(t => (s.Kennung, (n + 1) + t.Nr, string.Join(" ", t.Text)))));
        // ae_b1_06 4a war die Stakeholder-Tabelle: Stakeholder bleibt als eigene Teilaufgabe in ae_b1_06 4d und ae_b1_08 3b
        var stakeholder = Teile(angeboten).Where(t => t.Text.Contains("Stakeholder", StringComparison.Ordinal)).Select(t => t.Kennung + " " + t.Teil).ToList();
        Assert.Contains("ae_b1_06 4d", stakeholder);
        Assert.Contains("ae_b1_08 3b", stakeholder);
        // ae_b1_10 1b war die Zuordnung Lastenheft/Pflichtenheft: das Thema bleibt mit 1a, 1c, 1d und 1e in ae_b1_10
        var lastenheft = Teile(angeboten).Where(t => t.Text.Contains("Lastenheft", StringComparison.Ordinal) || t.Text.Contains("Pflichtenheft", StringComparison.Ordinal))
            .Select(t => t.Kennung + " " + t.Teil).ToList();
        Assert.Equal(["ae_b1_10 1a", "ae_b1_10 1b", "ae_b1_10 1c", "ae_b1_10 1d", "ae_b1_10 1e"], lastenheft);
        // ae_b1_09 2c hatte die Abhaengigkeit bei reiner Parameterbenutzung (keine Beziehung zeichnen): sie bleibt als 2cb
        // (Gegenpruefung 1, Fund 2); die Assoziation des Musterbilds kommt aus Anlage 5 (Durchgaenge Ticket - Drehkreuz, Fund 1)
        var neun = app.Satz("ae_b1_09")!.Aufgaben[1].Teile;
        var cb = neun.Single(t => t.Nr == "cb");
        Assert.Contains("Parameter", string.Join(" ", cb.Text), StringComparison.Ordinal);
        Assert.Contains("Abhängigkeit", string.Join(" ", cb.Loesung), StringComparison.Ordinal);
        var ca = neun.Single(t => t.Nr == "ca");
        Assert.Contains("5", ca.Anlagen);
        Assert.Equal([("dreh", "lift", "komposition"), ("ticket", "dreh", "assoziation")],
            ca.Antwort.Muster!.Verbindungen.Select(v => (v.Von, v.Nach, v.Typ)));
        // jedes Thema (Titel der Aufgaben) der alten Fassungen gibt es noch
        var alt = OhneNeueFassungen();
        Assert.Equal(alt.Fuer(EchtSaetze.B1, "ae").SelectMany(s => s.Aufgaben.Select(a => a.Titel)),
            app.Fuer(EchtSaetze.B1, "ae").SelectMany(s => s.Aufgaben.Select(a => a.Titel)));
        // Gewicht der Diagramme je Paar (Strukturblatt Abschnitt 7: 40 bis 60 von 200)
        foreach (var (nummer, soll) in new[] { ("06", 48), ("07", 45), ("08", 42), ("09", 41), ("10", 48) })
        {
            var punkte = new[] { "ae_b1_" + nummer, "ae_b2_" + nummer }.Sum(k => app.Satz(k)!.Aufgaben.SelectMany(a => a.Teile).Where(t => t.Gebiet == "diagramm").Sum(t => t.Punkte));
            Assert.Equal(soll, punkte);
            Assert.InRange(punkte, 40, 60);
        }
    }

    // ---- (5) offener Lauf einer alten Fassung --------------------------------------------------------------------------------

    [Fact]
    public void OffenerLauf_AlteFassung_AntwortBleibtGespeichert_NichtsStillVerworfen()
    {
        var art = EchtArten.Art(Fachrichtung.Alle.Single(f => f.Id == "ae"), EchtSaetze.B1);
        var alt = OhneNeueFassungen().Satz("ae_b1_07")!;
        var neu = App.Satz("ae_b1_07")!;
        Assert.Equal((1, 2), (alt.Fassung, neu.Fassung));
        var zustand = EchtAblauf.Neu(alt, art, new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        Assert.Null(zustand[EchtAblauf.FassungSchluessel]);   // Fassung 1: Zustand wie bisher
        var antworten = (JsonObject)zustand["antworten"]!;
        antworten[EchtBewertung.Feld(2, "c", EchtBewertung.Text)] = "Klasse Erinnerung auf Papier (alte Antwort)";
        antworten[EchtBewertung.Feld(2, "a", EchtBewertung.Text)] = "Antwort 2a bleibt";
        zustand["haken"] = new JsonObject { ["2.c"] = new JsonArray(true, false), ["2.a"] = new JsonArray(true) };
        var vorher = zustand.ToJsonString();
        Assert.True(EchtAblauf.Angleichen(zustand, neu));
        Assert.Equal(2, (int)zustand[EchtAblauf.FassungSchluessel]!);
        Assert.False(antworten.ContainsKey(EchtBewertung.Feld(2, "c", EchtBewertung.Text)));
        Assert.Equal("Antwort 2a bleibt", (string?)antworten[EchtBewertung.Feld(2, "a", EchtBewertung.Text)]);
        var beiseite = zustand[EchtAblauf.AlteFassungen]!["1"]!;
        Assert.Equal("Klasse Erinnerung auf Papier (alte Antwort)", (string?)beiseite["antworten"]![EchtBewertung.Feld(2, "c", EchtBewertung.Text)]);
        Assert.Equal("[true,false]", beiseite["haken"]!["2.c"]!.ToJsonString());
        Assert.Equal("[true]", zustand["haken"]!["2.a"]!.ToJsonString());
        // der neue Teil gilt als nicht bearbeitet, die Punkte des alten Haekchens zaehlen nicht fuer das neue Musterbild
        Assert.False(EchtBewertung.Bearbeitet(zustand, 2, "c"));
        Assert.True(EchtBewertung.Bearbeitet(zustand, 2, "a"));
        var c = neu.Aufgaben[1].Teile.Single(t => t.Nr == "c");
        Assert.Equal(0, EchtBewertung.Teil(zustand, 2, c).Erreicht);
        // das Ergebnis traegt die beiseite gelegten Antworten mit
        var daten = EchtBewertung.Auswerten(neu, zustand).Daten;
        Assert.Equal("Klasse Erinnerung auf Papier (alte Antwort)", (string?)daten[EchtAblauf.AlteFassungen]!["1"]!["antworten"]![EchtBewertung.Feld(2, "c", EchtBewertung.Text)]);
        // ein zweites Mal aendert nichts; Gegenprobe: ein Zustand der neuen Fassung bleibt unberuehrt
        var nachher = zustand.ToJsonString();
        Assert.False(EchtAblauf.Angleichen(zustand, neu));
        Assert.Equal(nachher, zustand.ToJsonString());
        Assert.NotEqual(vorher, nachher);
        var frisch = EchtAblauf.Neu(neu, art, DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc);
        Assert.Equal(2, (int)frisch[EchtAblauf.FassungSchluessel]!);
        var frischText = frisch.ToJsonString();
        Assert.False(EchtAblauf.Angleichen(frisch, neu));
        Assert.Equal(frischText, frisch.ToJsonString());
        Assert.Null(EchtBewertung.Auswerten(neu, frisch).Daten[EchtAblauf.AlteFassungen]);
    }

    [Fact]
    public void OffenerLauf_GeteilteTeilaufgabe_AlteAntwortBeiseite()
    {
        // ae_b1_09: 2c gibt es in Fassung 2 nicht mehr (geteilt in 2ca und 2cb); Antwort und Haekchen von 2c kommen beiseite
        var art = EchtArten.Art(Fachrichtung.Alle.Single(f => f.Id == "ae"), EchtSaetze.B1);
        var alt = OhneNeueFassungen().Satz("ae_b1_09")!;
        var neu = App.Satz("ae_b1_09")!;
        var zustand = EchtAblauf.Neu(alt, art, new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        var antworten = (JsonObject)zustand["antworten"]!;
        antworten[EchtBewertung.Feld(2, "c", "papier")] = "ja";
        antworten[EchtBewertung.Feld(2, "d", EchtBewertung.Text)] = "2d bleibt";
        zustand["haken"] = new JsonObject { ["2.c"] = new JsonArray(true), ["2.d"] = new JsonArray(true) };
        Assert.True(EchtAblauf.Angleichen(zustand, neu));
        Assert.Equal("[\"2.d/text\"]", new JsonArray([.. antworten.Select(p => (JsonNode?)p.Key)]).ToJsonString());
        Assert.Equal("ja", (string?)zustand[EchtAblauf.AlteFassungen]!["1"]!["antworten"]![EchtBewertung.Feld(2, "c", "papier")]);
        Assert.Equal("[true]", zustand[EchtAblauf.AlteFassungen]!["1"]!["haken"]!["2.c"]!.ToJsonString());
        Assert.Equal("[true]", zustand["haken"]!["2.d"]!.ToJsonString());
        // Gegenprobe: ohne Angleichen bliebe die Antwort von 2c ungesehen unter den Antworten liegen
        var ohne = EchtAblauf.Neu(alt, art, DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc);
        ((JsonObject)ohne["antworten"]!)[EchtBewertung.Feld(2, "c", "papier")] = "ja";
        Assert.DoesNotContain(neu.Aufgaben[1].Teile, t => t.Nr == "c");
    }

    [Fact]
    public void OffenerLauf_Zeigen_GleichtAn_Speichert_MitHinweis()
    {
        var art = EchtArten.Art(Fachrichtung.Alle.Single(f => f.Id == "ae"), EchtSaetze.B1);
        var alt = OhneNeueFassungen().Satz("ae_b1_10")!;
        var jetzt = DateTimeOffset.UtcNow;
        var zustand = EchtAblauf.Neu(alt, art, jetzt, TimeZoneInfo.Utc);
        ((JsonObject)zustand["antworten"]!)[EchtBewertung.Feld(1, "b", EchtBewertung.Zelle(1, 1))] = "Pflichtenheft";
        var einstellungen = new JsonObject { [EchtAblage.Schluessel] = zustand };
        var geschrieben = 0;
        var ablage = new EchtAblage(() => einstellungen, a => { a(einstellungen); geschrieben++; return true; });
        var ablauf = new EchtAblauf(() => App, ablage, () => throw new InvalidOperationException("kein Abschluss im Test"), () => jetzt, TimeZoneInfo.Utc);
        var schritt = ablauf.Zeigen();
        Assert.Equal((EchtAnsicht.Lauf, EchtTexte.TeilNeuGefasst), (schritt.Ansicht, schritt.Hinweis));
        Assert.Equal(1, geschrieben);
        var gespeichert = (JsonObject)einstellungen[EchtAblage.Schluessel]!;
        Assert.Equal(2, (int)gespeichert[EchtAblauf.FassungSchluessel]!);
        Assert.Equal("Pflichtenheft", (string?)gespeichert[EchtAblauf.AlteFassungen]!["1"]!["antworten"]![EchtBewertung.Feld(1, "b", EchtBewertung.Zelle(1, 1))]);
        // Gegenprobe: beim zweiten Zeigen kein Hinweis, nichts geschrieben
        var zweites = ablauf.Zeigen();
        Assert.Equal((EchtAnsicht.Lauf, null), (zweites.Ansicht, zweites.Hinweis));
        Assert.Equal(1, geschrieben);
    }

    // ---- (3) Bewertungskopf mit AE-Bereich -----------------------------------------------------------------------------------

    [Fact]
    public void Bewertungskopf_AeSatz_NenntAeBereich()
    {
        var ae = Fachrichtung.Alle.Single(f => f.Id == "ae");
        foreach (var (kennung, _, _, _) in Neu)
        {
            var satz = App.Satz(kennung)!;
            var name = EchtArten.Namen(EchtArten.Art(ae, satz.Bereich))!.Value.Name;
            Assert.Equal("AP2 · Planen eines Softwareproduktes" + EchtTexte.NameZusatz, name);
        }
        // Gegenprobe: die SI-Art nennt den SI-Bereich, nie den AE-Bereich
        var si = Fachrichtung.Alle.Single(f => f.Id == "si");
        Assert.DoesNotContain("Softwareproduktes", EchtArten.Namen(EchtArten.Art(si, EchtSaetze.B1))!.Value.Name, StringComparison.Ordinal);
    }

    // ---- (1) Klassendiagramm: Lesen, Regeln, Baukasten -----------------------------------------------------------------------

    private const string KlasseGut = """
        {"art": "klasse", "elemente": [
          {"id": "fz", "typ": "klasse", "text": "Fahrzeug", "x": 0, "y": 0, "attribute": ["- kennzeichen: String"], "methoden": ["+ fahren()"]},
          {"id": "lkw", "typ": "klasse", "text": "Lkw", "x": 0, "y": 2},
          {"id": "rad", "typ": "klasse", "text": "Rad", "x": 2, "y": 0},
          {"id": "flotte", "typ": "klasse", "text": "Flotte", "x": 2, "y": 2}],
         "verbindungen": [
          {"von": "lkw", "nach": "fz", "typ": "vererbung"},
          {"von": "rad", "nach": "fz", "typ": "komposition", "mult_von": "4", "mult_nach": "1"},
          {"von": "fz", "nach": "flotte", "typ": "aggregation", "mult_von": "0..*", "mult_nach": "0..1"},
          {"von": "flotte", "nach": "rad", "typ": "assoziation", "mult_von": "1", "mult_nach": "*"}]}
        """;

    private static string Klasse(Func<string, string> aendern) => aendern(KlasseGut.Replace("\"mult_von\": \"4\"", "\"mult_von\": \"1..*\"", StringComparison.Ordinal));

    [Fact]
    public void Klasse_LesenUndRegeln_JedeRegelMitGegenprobe()
    {
        var gut = Lesen(EchtZeichnung.Klasse, Klasse(s => s));
        Assert.Equal(["vererbung", "komposition", "aggregation", "assoziation"], gut.Verbindungen.Select(v => v.Typ));
        Assert.Equal(["- kennzeichen: String"], gut.Element("fz").AttributZeilen);
        Assert.Equal(["+ fahren()"], gut.Element("fz").MethodenZeilen);
        Assert.EndsWith("Multiplizität nur 1, 0..1, *, 0..* oder 1..*", Fehler(EchtZeichnung.Klasse, KlasseGut), StringComparison.Ordinal);
        Assert.EndsWith("eine Vererbung hat keine Multiplizität", Fehler(EchtZeichnung.Klasse,
            Klasse(s => s.Replace("\"typ\": \"vererbung\"}", "\"typ\": \"vererbung\", \"mult_von\": \"1\"}", StringComparison.Ordinal))), StringComparison.Ordinal);
        Assert.EndsWith("bei einer Komposition gehört ein Teil zu höchstens einem Ganzen (Multiplizität am Ganzen 1 oder 0..1)", Fehler(EchtZeichnung.Klasse,
            Klasse(s => s.Replace("\"mult_von\": \"1..*\", \"mult_nach\": \"1\"", "\"mult_von\": \"1..*\", \"mult_nach\": \"*\"", StringComparison.Ordinal))), StringComparison.Ordinal);
        Assert.EndsWith("Art muss Assoziation, Vererbung, Aggregation oder Komposition sein", Fehler(EchtZeichnung.Klasse,
            Klasse(s => s.Replace("\"typ\": \"aggregation\"", "\"typ\": \"include\"", StringComparison.Ordinal))), StringComparison.Ordinal);
        Assert.EndsWith("Multiplizität an beiden Enden fehlt", Fehler(EchtZeichnung.Klasse,
            Klasse(s => s.Replace("\"mult_von\": \"1\", \"mult_nach\": \"*\"", "\"mult_von\": \"1\"", StringComparison.Ordinal))), StringComparison.Ordinal);
        // ohne Multiplizitaet ist eine Vorgabe (unvollstaendig) gut
        Lesen(EchtZeichnung.Klasse, Klasse(s => s.Replace("\"mult_von\": \"1\", \"mult_nach\": \"*\"", "\"mult_von\": \"1\"", StringComparison.Ordinal)), vollstaendig: false);
        Assert.Equal("Test: zwei Klassen heißen „Rad“", Fehler(EchtZeichnung.Klasse, Klasse(s => s.Replace("\"Flotte\"", "\"rad \"", StringComparison.Ordinal))));
        Assert.EndsWith("Vererbung im Kreis bei Klasse „Fahrzeug“", Fehler(EchtZeichnung.Klasse,
            Klasse(s => s.Replace("{\"von\": \"fz\", \"nach\": \"flotte\", \"typ\": \"aggregation\", \"mult_von\": \"0..*\", \"mult_nach\": \"0..1\"}",
                "{\"von\": \"fz\", \"nach\": \"flotte\", \"typ\": \"vererbung\"}, {\"von\": \"flotte\", \"nach\": \"lkw\", \"typ\": \"vererbung\"}", StringComparison.Ordinal))), StringComparison.Ordinal);
        Assert.EndsWith("Attribute und Methoden gibt es nur an einer Klasse", Fehler(EchtZeichnung.Er,
            """{"art": "er", "elemente": [{"id": "e", "typ": "entitaet", "text": "Kunde", "x": 0, "y": 0, "attribute": ["x"]}]}""", vollstaendig: false), StringComparison.Ordinal);
        Assert.EndsWith("Beziehungsarten und Multiplizitäten gibt es nur im Klassen- und Use-Case-Diagramm", Fehler(EchtZeichnung.Er,
            """{"art": "er", "elemente": [{"id": "e", "typ": "entitaet", "text": "Kunde", "x": 0, "y": 0}, {"id": "b", "typ": "beziehung", "text": "hat", "x": 1, "y": 0}], "verbindungen": [{"von": "e", "nach": "b", "typ": "assoziation"}]}""",
            vollstaendig: false), StringComparison.Ordinal);
        Assert.EndsWith("höchstens 8 Zeilen, jede nicht leer und höchstens 60 Zeichen", Fehler(EchtZeichnung.Klasse,
            Klasse(s => s.Replace("[\"+ fahren()\"]", "[\"\"]", StringComparison.Ordinal))), StringComparison.Ordinal);
        Assert.EndsWith("eine Systemgrenze gibt es nur im Use-Case-Diagramm", Fehler(EchtZeichnung.Klasse,
            Klasse(s => s.Replace("{\"art\": \"klasse\",", "{\"art\": \"klasse\", \"grenze\": {\"name\": \"x\", \"x\": 0, \"y\": 0, \"breite\": 1, \"hoehe\": 1},", StringComparison.Ordinal))), StringComparison.Ordinal);
    }

    [Fact]
    public void Klasse_Baukasten_VierBeziehungen_RichtungMultiplizitaetZeilen()
    {
        var b = EchtBaukasten.Neu(EchtZeichnung.Klasse, null);
        var fz = b.Setzen("klasse")!;
        var lkw = b.Setzen("klasse", fz)!;
        var rad = b.Setzen("klasse")!;
        Assert.True(b.Beschriften(fz, "Fahrzeug") && b.Beschriften(lkw, "Lkw") && b.Beschriften(rad, "Rad"));
        Assert.Equal(0, b.ZeileHinzufuegen(fz, methode: false));
        Assert.True(b.ZeileSetzen(fz, false, 0, "- kennzeichen: String"));
        Assert.Equal(0, b.ZeileHinzufuegen(fz, methode: true));
        Assert.True(b.ZeileSetzen(fz, true, 0, "+ fahren()"));
        Assert.Equal(["assoziation", "vererbung", "aggregation", "komposition"], b.ArtenZwischen(lkw, fz));
        Assert.Null(b.Verbinden(lkw, fz, "vererbung"));
        Assert.Equal(EchtTexte.KeinKreis, b.Verbinden(fz, lkw, "vererbung") == EchtTexte.SchonVerbunden ? EchtTexte.KeinKreis : "anders");
        Assert.Null(b.Verbinden(rad, fz, "komposition"));
        // Multiplizitaet: nicht an der Vererbung; an der Komposition am Ganzen nur 1 oder 0..1
        Assert.False(b.MultSetzen(lkw, fz, true, "1"));
        Assert.False(b.MultSetzen(rad, fz, false, "*"));
        Assert.True(b.MultSetzen(rad, fz, false, "1"));
        Assert.True(b.MultSetzen(rad, fz, true, "1..*"));
        Assert.False(b.MultSetzen(rad, fz, true, "4"));
        // umkehren: Multiplizitaeten wandern mit ihrem Ende; am neuen Ganzen faellt 1..* weg
        Assert.Null(b.Umkehren(rad, fz));
        var v = b.Diagramm.Verbindungen.Single(x => x.Typ == "komposition");
        Assert.Equal((fz, rad, "", "1"), (v.Von, v.Nach, v.MultNach, v.MultVon));
        Assert.Null(b.Umkehren(fz, rad));
        // Art aendern: aus der Komposition wird eine Aggregation, aus der Aggregation eine Assoziation (ungerichtet)
        Assert.Null(b.ArtSetzen(rad, fz, "aggregation"));
        Assert.Null(b.ArtSetzen(rad, fz, "assoziation"));
        Assert.Equal(EchtTexte.NichtsGewaehlt, b.Umkehren(rad, fz));
        Assert.Equal(EchtTexte.ArtNichtErlaubt, b.ArtSetzen(rad, fz, "include"));
        // Kreis aus Vererbung: Fahrzeug erbt von Lkw geht nicht (Lkw erbt schon von Fahrzeug)
        var dritte = b.Setzen("klasse")!;
        b.Beschriften(dritte, "Anhaenger");
        Assert.Null(b.Verbinden(fz, dritte, "vererbung"));
        Assert.Equal(EchtTexte.KeinKreis, b.Verbinden(dritte, lkw, "vererbung"));
        // Speichern und Laden: gleiche Zeichnung
        var json = b.Json();
        var geladen = EchtBaukasten.Laden(EchtZeichnung.Klasse, null, json);
        Assert.False(geladen.Unlesbar);
        Assert.Equal(json, geladen.Json());
        Assert.Equal(b.Diagramm.Verbindungen, geladen.Diagramm.Verbindungen);
        Assert.Equal(["- kennzeichen: String"], geladen.Element(fz)!.AttributZeilen);
    }

    [Fact]
    public void Klasse_EchteVorgabe_FestUndNichtVeraenderbar_Zaehlen()
    {
        var teil = App.Satz("ae_b1_07")!.Aufgaben[1].Teile.Single(t => t.Nr == "c");
        var b = EchtBaukasten.Neu(EchtZeichnung.Klasse, teil.Antwort.Vorgabe);
        var termin = b.Diagramm.Elemente.Single(e => e.Text == "Termin").Id;
        var tier = b.Diagramm.Elemente.Single(e => e.Text == "Tier").Id;
        Assert.True(b.IstFest(termin));
        Assert.Null(b.ZeileHinzufuegen(termin, false));
        Assert.False(b.Beschriften(termin, "Anders"));
        Assert.False(b.Entfernen(tier));
        // nachbauen wie im Musterbild
        var er = b.Setzen("klasse", termin)!;
        b.Beschriften(er, "Erinnerung");
        foreach (var z in new[] { "- kanal: String", "- zeitpunkt: int", "- versendet: boolean" })
        {
            b.ZeileSetzen(er, false, b.ZeileHinzufuegen(er, false)!.Value, z);
        }
        b.ZeileSetzen(er, true, b.ZeileHinzufuegen(er, true)!.Value, "+ senden(): boolean");
        Assert.Null(b.Verbinden(termin, tier, "assoziation"));
        Assert.True(b.MultSetzen(termin, tier, true, "0..*") && b.MultSetzen(termin, tier, false, "1"));
        Assert.Null(b.Verbinden(termin, er, "assoziation"));
        Assert.True(b.MultSetzen(termin, er, true, "1") && b.MultSetzen(termin, er, false, "0..*"));
        var zeilen = EchtBaukasten.Zaehlen(b.Diagramm, teil.Antwort.Muster!);
        Assert.All(zeilen, z => Assert.True(z.Ok, z.Text));
        Assert.Contains(zeilen, z => z.Text == "Klassen: 3 von 3 (Namen stimmen)");
        Assert.Contains(zeilen, z => z.Text == "Attribute: 10 von 10, Methoden: 6 von 6");
        Assert.Contains(zeilen, z => z.Text == EchtTexte.ZaehlRegelnOk);
        // Gegenprobe: Multiplizitaet vertauscht und ein Attribut zu wenig -> rote Zeilen in Klartext
        b.MultSetzen(termin, er, true, "0..*");
        b.MultSetzen(termin, er, false, "1");
        b.ZeileEntfernen(er, false, 2);
        var rot = EchtBaukasten.Zaehlen(b.Diagramm, teil.Antwort.Muster!);
        Assert.Contains(rot, z => !z.Ok && z.Text == "Attribute: 9 von 10, Methoden: 6 von 6");
        Assert.Contains(rot, z => !z.Ok && z.Text == "Erinnerung – Termin: Multiplizität 1 – 0..*, erwartet 0..* – 1");
    }

    [Fact]
    public void Klasse_Zaehlen_FehltArtRichtungZuViel_Klartext()
    {
        var muster = Lesen(EchtZeichnung.Klasse, Klasse(s => s));
        var b = EchtBaukasten.Neu(EchtZeichnung.Klasse, null);
        string K(string name)
        {
            var id = b.Setzen("klasse")!;
            b.Beschriften(id, name);
            return id;
        }
        var fz = K("fahrzeug ");          // Namen tolerant: Gross/klein und Leerraum
        var lkw = K("LKW");
        var rad = K("Rad");
        var bus = K("Bus");
        Assert.Null(b.Verbinden(fz, lkw, "vererbung"));    // Richtung falsch
        Assert.Null(b.Verbinden(rad, fz, "aggregation"));  // Art falsch
        Assert.Null(b.Verbinden(bus, rad, "assoziation")); // nicht im Musterbild
        var z = EchtBaukasten.Zaehlen(b.Diagramm, muster);
        Assert.Contains(z, x => !x.Ok && x.Text == "Klassen: 4 von 4; fehlt: Flotte; nicht im Musterbild: Bus");
        Assert.Contains(z, x => !x.Ok && x.Text == "Vererbung Fahrzeug → Lkw: Richtung falsch, erwartet Lkw → Fahrzeug");
        Assert.Contains(z, x => !x.Ok && x.Text == "Rad – Fahrzeug: Aggregation, erwartet Komposition");
        Assert.Contains(z, x => !x.Ok && x.Text.StartsWith("Fehlt: ", StringComparison.Ordinal) && x.Text.Contains("Flotte", StringComparison.Ordinal));
        Assert.Contains(z, x => !x.Ok && x.Text.StartsWith("Nicht im Musterbild: ", StringComparison.Ordinal) && x.Text.Contains("Bus", StringComparison.Ordinal));
        // Gegenprobe: das Musterbild selbst zaehlt ganz gruen
        Assert.All(EchtBaukasten.Zaehlen(muster, muster), x => Assert.True(x.Ok, x.Text));
    }

    // ---- (2) Use-Case-Diagramm ----------------------------------------------------------------------------------------------

    [Fact]
    public void UseCase_LesenUndRegeln_PfeilrichtungUndGrenze_MitGegenprobe()
    {
        var teil = App.Satz("ae_b1_06")!.Aufgaben[3].Teile.Single(t => t.Nr == "a");
        var muster = teil.Antwort.Muster!;
        Assert.Equal(new EchtGrenze("Ausleih-App", 1, 0, 5, 5), muster.Grenze);
        // «include» zeigt vom Basisfall auf den eingebundenen Fall, «extend» vom erweiternden Fall auf den Basisfall,
        // die Generalisierung vom speziellen Akteur auf den allgemeinen
        string Name(string id) => muster.Element(id).Text;
        Assert.Equal(("Rad ausleihen", "Anmelden"), muster.Verbindungen.Single(v => v.Typ == "include") is var i ? (Name(i.Von), Name(i.Nach)) : default);
        Assert.Equal(("Schaden melden", "Rad zurückgeben"), muster.Verbindungen.Single(v => v.Typ == "extend") is var e ? (Name(e.Von), Name(e.Nach)) : default);
        Assert.Equal(("Abokunde", "Kunde"), muster.Verbindungen.Single(v => v.Typ == "generalisierung") is var g ? (Name(g.Von), Name(g.Nach)) : default);
        Assert.True(EchtZeichnung.Gerichtet("include") && EchtZeichnung.Gerichtet("extend") && EchtZeichnung.Gerichtet("generalisierung"));
        Assert.False(EchtZeichnung.Gerichtet("assoziation"));
        const string Basis = """
            {"art": "usecase", "grenze": {"name": "App", "x": 1, "y": 0, "breite": 3, "hoehe": 3}, "elemente": [
              {"id": "k", "typ": "akteur", "text": "Kunde", "x": 0, "y": 0},
              {"id": "f", "typ": "anwendungsfall", "text": "Bestellen", "x": 2, "y": 0},
              {"id": "g", "typ": "anwendungsfall", "text": "Anmelden", "x": 2, "y": 2}],
             "verbindungen": [{"von": "k", "nach": "f", "typ": "assoziation"}, {"von": "f", "nach": "g", "typ": "include"}]}
            """;
        Lesen(EchtZeichnung.UseCase, Basis);
        Assert.EndsWith("ein Akteur steht außerhalb der Systemgrenze", Fehler(EchtZeichnung.UseCase, Basis.Replace("\"x\": 0, \"y\": 0}", "\"x\": 1, \"y\": 1}", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.EndsWith("ein Anwendungsfall steht innerhalb der Systemgrenze", Fehler(EchtZeichnung.UseCase, Basis.Replace("\"x\": 2, \"y\": 2}", "\"x\": 5, \"y\": 2}", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.EndsWith("«include» und «extend» verbinden nur Anwendungsfälle", Fehler(EchtZeichnung.UseCase, Basis.Replace("{\"von\": \"k\", \"nach\": \"f\", \"typ\": \"assoziation\"}", "{\"von\": \"k\", \"nach\": \"f\", \"typ\": \"extend\"}", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.EndsWith("eine Assoziation verbindet einen Akteur mit einem Anwendungsfall", Fehler(EchtZeichnung.UseCase, Basis.Replace("\"typ\": \"include\"", "\"typ\": \"assoziation\"", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.EndsWith("eine Generalisierung verbindet nur gleiche Bausteine (Akteur mit Akteur, Anwendungsfall mit Anwendungsfall)", Fehler(EchtZeichnung.UseCase, Basis.Replace("{\"von\": \"k\", \"nach\": \"f\", \"typ\": \"assoziation\"}", "{\"von\": \"k\", \"nach\": \"f\", \"typ\": \"generalisierung\"}", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.EndsWith("im Use-Case-Diagramm tragen Beziehungen keinen Text und keine Multiplizität", Fehler(EchtZeichnung.UseCase, Basis.Replace("\"typ\": \"include\"", "\"typ\": \"include\", \"mult_von\": \"1\"", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.EndsWith("ein Use-Case-Diagramm braucht die Systemgrenze (\"grenze\")", Fehler(EchtZeichnung.UseCase, Basis.Replace("\"grenze\": {\"name\": \"App\", \"x\": 1, \"y\": 0, \"breite\": 3, \"hoehe\": 3}, ", "", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.EndsWith("die Systemgrenze liegt nicht ganz im Raster", Fehler(EchtZeichnung.UseCase, Basis.Replace("\"breite\": 3", "\"breite\": 8", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.EndsWith("Anwendungsfall „Anmelden“ hat keine Beziehung", Fehler(EchtZeichnung.UseCase, Basis.Replace(", {\"von\": \"f\", \"nach\": \"g\", \"typ\": \"include\"}", "", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public void UseCase_Baukasten_GrenzeVorgegeben_AkteurDraussen_PfeileRichtig_Zaehlen()
    {
        var teil = App.Satz("ae_b1_10")!.Aufgaben[0].Teile.Single(t => t.Nr == "b");
        var muster = teil.Antwort.Muster!;
        var b = EchtBaukasten.Neu(EchtZeichnung.UseCase, teil.Antwort.Vorgabe);
        Assert.Equal(muster.Grenze, b.Grenze);
        var g = b.Grenze!;
        // Akteur nur ausserhalb, Anwendungsfall nur innerhalb der Grenze (Setzen und Verschieben)
        var hv = b.Setzen("akteur")!;
        Assert.False(g.Enthaelt(b.Element(hv)!.X, b.Element(hv)!.Y));
        var fall = b.Setzen("anwendungsfall")!;
        Assert.True(g.Enthaelt(b.Element(fall)!.X, b.Element(fall)!.Y));
        Assert.False(b.Verschieben(hv, 2, 1));
        Assert.False(b.Verschieben(fall, 7, 1));
        Assert.True(b.Verschieben(hv, 0, 4));
        string Id(string text) => b.Diagramm.Elemente.Single(e => e.Text == text).Id;
        var kunde = Id("Kunde");
        var anfrage = Id("Anfrage stellen");
        Assert.True(b.IstFest(kunde) && b.IstFest(anfrage));
        // erste erlaubte Art: Akteur–Fall Assoziation, Fall–Fall «include», Akteur–Akteur Generalisierung
        Assert.Equal(["assoziation"], b.ArtenZwischen(hv, fall));
        Assert.Equal(["include", "extend", "generalisierung"], b.ArtenZwischen(anfrage, fall));
        Assert.Equal(["generalisierung"], b.ArtenZwischen(hv, kunde));
        Assert.Equal(EchtTexte.NurAkteurFall, b.Verbinden(hv, kunde, "assoziation"));
        Assert.Equal(EchtTexte.NurFallFall, b.Verbinden(hv, fall, "include"));
        Assert.Equal(EchtTexte.NurGleiche, b.Verbinden(hv, fall, "generalisierung"));
        // nachbauen wie das Musterbild (mit einem Fehler in der Pfeilrichtung beim «extend»)
        b.Beschriften(hv, "Hausverwaltung");
        b.Beschriften(fall, "Gewerk wählen");
        string F(string text)
        {
            var id = b.Setzen("anwendungsfall")!;
            b.Beschriften(id, text);
            return id;
        }
        var fotos = F("Fotos hochladen");
        var zuschlag = F("Zuschlag erteilen");
        var anlagen = F("Wohnanlagen verwalten");
        var angebot = F("Angebot abgeben");
        var betrieb = b.Setzen("akteur")!;
        b.Beschriften(betrieb, "Handwerksbetrieb");
        Assert.Null(b.Verbinden(anfrage, fall, "include"));
        Assert.Null(b.Verbinden(anfrage, fotos, "extend"));          // falsch herum
        Assert.Null(b.Verbinden(hv, kunde, "generalisierung"));
        Assert.Equal(EchtTexte.KeinKreis, b.Verbinden(kunde, hv, "generalisierung") == EchtTexte.SchonVerbunden ? EchtTexte.KeinKreis : "anders");
        Assert.Null(b.Verbinden(kunde, zuschlag, "assoziation"));
        Assert.Null(b.Verbinden(hv, anlagen, "assoziation"));
        Assert.Null(b.Verbinden(betrieb, angebot, "assoziation"));
        var rot = EchtBaukasten.Zaehlen(b.Diagramm, muster);
        Assert.Contains(rot, z => !z.Ok && z.Text == "«extend» Anfrage stellen → Fotos hochladen: Richtung falsch, erwartet Fotos hochladen → Anfrage stellen");
        Assert.Contains(rot, z => z.Ok && z.Text == "Akteure: 3 von 3 (Namen stimmen)");
        Assert.Contains(rot, z => z.Ok && z.Text == "Anwendungsfälle: 6 von 6 (Namen stimmen)");
        // Richtung umkehren: alles gruen
        Assert.Null(b.Umkehren(anfrage, fotos));
        var gruen = EchtBaukasten.Zaehlen(b.Diagramm, muster);
        Assert.All(gruen, z => Assert.True(z.Ok, z.Text));
        // Speichern und Laden mit Vorgabe: Grenze und feste Bausteine bleiben
        var geladen = EchtBaukasten.Laden(EchtZeichnung.UseCase, teil.Antwort.Vorgabe, b.Json());
        Assert.False(geladen.Unlesbar);
        Assert.Equal(b.Json(), geladen.Json());
        Assert.Equal(muster.Grenze, geladen.Diagramm.Grenze);
    }

    [Fact]
    public void AlleNeuenMusterbilder_ImBaukastenNachgebaut_GanzGruen()
    {
        foreach (var (kennung, aufgabe, nr, art) in Neu)
        {
            var teil = App.Satz(kennung)!.Aufgaben[aufgabe - 1].Teile.Single(t => t.Nr == nr);
            var muster = teil.Antwort.Muster!;
            var b = EchtBaukasten.Neu(art, teil.Antwort.Vorgabe);
            var ids = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var e in muster.Elemente)
            {
                if (teil.Antwort.Vorgabe?.Elemente.Any(v => v.Id == e.Id) == true)
                {
                    ids[e.Id] = e.Id;
                    continue;
                }
                var id = b.Setzen(e.Typ)!;
                Assert.True(b.Verschieben(id, e.X, e.Y) || (b.Element(id)!.X, b.Element(id)!.Y) == (e.X, e.Y), kennung + " " + e.Text);
                Assert.True(b.Beschriften(id, e.Text));
                foreach (var (zeilen, methode) in new[] { (e.AttributZeilen, false), (e.MethodenZeilen, true) })
                {
                    foreach (var z in zeilen)
                    {
                        Assert.True(b.ZeileSetzen(id, methode, b.ZeileHinzufuegen(id, methode)!.Value, z));
                    }
                }
                ids[e.Id] = id;
            }
            foreach (var v in muster.Verbindungen)
            {
                if (b.Diagramm.Verbindungen.Any(x => (x.Von, x.Nach) == (ids[v.Von], ids[v.Nach])))
                {
                    continue;   // Vorgabe
                }
                Assert.Null(b.Verbinden(ids[v.Von], ids[v.Nach], v.Typ));
                if (v.MultVon.Length > 0)
                {
                    Assert.True(b.MultSetzen(ids[v.Von], ids[v.Nach], true, v.MultVon));
                    Assert.True(b.MultSetzen(ids[v.Von], ids[v.Nach], false, v.MultNach));
                }
            }
            var zeilen2 = EchtBaukasten.Zaehlen(b.Diagramm, muster);
            Assert.All(zeilen2, z => Assert.True(z.Ok, kennung + ": " + z.Text));
            Assert.Empty(EchtZeichnung.Verstoesse(b.Diagramm, vollstaendig: true));
        }
    }
}
