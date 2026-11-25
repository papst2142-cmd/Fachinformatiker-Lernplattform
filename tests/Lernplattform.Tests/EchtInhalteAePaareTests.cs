using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.94.0, Weisung P12-304: neue AE-Pruefungssaetze im berichtigten Format (Strukturblatt AP2 Abschnitt 7, gilt fuer AE vor
/// den Abschnitten 1 und 2). Abnahmesaetze in eigenen Worten, je einer oder mehrere Tests:
/// (1) Wer AE gewaehlt hat, bekommt je Bereich fuenf neue Saetze (fuenf Paare); die alten zehn AE-Saetze bietet die App nicht
///     mehr an, SI, DPA, DV und AP1 bleiben genau gleich, die Datei ist vor den neuen Saetzen bytegleich (nur angehaengt).
/// (2) Ein Paar = Bereich 1 und Bereich 2 derselben Firma, Bereich 2 fuehrt das Projekt fort.
/// (3) Die Grenzen aus Abschnitt 7 gelten fest in der Ladepruefung und in diesem Test (nicht nur im Skript): Aufgaben 20 bis 30
///     Punkte, 3 bis 6 Teilaufgaben mit 2 bis 14 Punkten, je Bereich 2 bis 3 grosse Teilaufgaben, hoechstens ein Rechenteil,
///     Englisch genau einmal je Paar bis 12 Punkte, Gewicht je Paar: Diagramme etwa ein Viertel, Pseudocode und SQL etwa ein
///     Drittel. Jede Regel mit Gegenprobe.
/// (4) Die Aufgabenarten aus Abschnitt 7 sind ueber die Paare verteilt; was der Baukasten noch nicht kann, steht "auf Papier"
///     mit Musterloesung.
/// (5) Ein gespeicherter, noch offener Lauf eines alten Satzes stuerzt nicht ab: klarer Hinweis, er laesst sich beenden und
///     abschliessen, der Lernstand bleibt.
/// (6) Fachrichtungs-Pakete tragen den Ausschluss der Saetze mit (Gegenprobe: falsche Kennung = Paket rot).
/// (7) Zusatz: die Messzeile "Inhalte laden" nennt die Teile Datei lesen, JSON lesen, Sicht aufbauen (nur Ausgabe).
/// </summary>
public class EchtInhalteAePaareTests
{
    /// <summary>Die 10 neuen Saetze in der Reihenfolge der Datei (hinter den 47 Saetzen bis A 0.93.0).</summary>
    public static readonly string[] Neu =
    [
        "ae_b1_06", "ae_b1_07", "ae_b1_08", "ae_b1_09", "ae_b1_10",
        "ae_b2_06", "ae_b2_07", "ae_b2_08", "ae_b2_09", "ae_b2_10",
    ];

    /// <summary>Laenge und SHA-256 der Datei (Kopf als Format 2) bis vor das schliessende "\n ]\n}" am Stand A 0.93.0 (main ba29d47).</summary>
    private const int VorherLaenge = 1975415;
    private const string VorherSha = "e4a4b991b9ba178e5244306005313b5799174cb03cc4a2eef5d1e44b1deb4169";

    private static readonly string Format2 = Path.Combine(Pfade.Wurzel, "tests", "Lerninhalte", "echt", "pruefungssaetze_format2.json");

    private static EchtBestand App => EchtTestDatei.App();

    private static List<EchtSatz> NeueSaetze => [.. Neu.Select(k => App.Satz(k)!)];

    private static IEnumerable<(EchtSatz B1, EchtSatz B2)> Paare
    {
        get
        {
            var app = App;
            return Neu.Take(5).Select(k => (app.Satz(k)!, app.Satz(EchtSaetze.Partner(k))!));
        }
    }

    private static string Alles(EchtSatz s) => string.Join("\n", s.Ausgangssituation.Concat(s.Anlagen.SelectMany(a => a.Zeilen.Append(a.Titel)))
        .Concat(s.Aufgaben.SelectMany(a => a.Einleitung.Append(a.Titel).Concat(a.Teile.SelectMany(t => t.Text.Concat(t.Loesung))))));

    // ---- (1) Zaehlen, Ausschluss, nur angehaengt ------------------------------------------------------------------------------

    [Fact]
    public void AE_FuenfNeuePaare_AlteZehnNichtMehrAngeboten_AndereGleich()
    {
        var bestand = App;
        Assert.Empty(bestand.Fehler);
        Assert.Equal(57, bestand.Saetze.Count);
        Assert.Equal(Neu, bestand.Saetze.Skip(47).Select(s => s.Kennung));
        Assert.Equal(EchtInhalteAeTests.Ae.Order(StringComparer.Ordinal), bestand.Ausgeschlossen.Order(StringComparer.Ordinal));
        foreach (var f in Fachrichtung.Alle)
        {
            Assert.Equal(6, bestand.Fuer(EchtSaetze.Ap1, f.Id).Count);
            Assert.Equal(f.Id switch { "si" => 6, "ae" or "dpa" or "dv" => 5, _ => 0 }, bestand.Fuer(EchtSaetze.B1, f.Id).Count);
            Assert.Equal(f.Id switch { "si" => 5, "ae" or "dpa" or "dv" => 5, _ => 0 }, bestand.Fuer(EchtSaetze.B2, f.Id).Count);
        }
        Assert.Equal(Neu.Take(5), bestand.Fuer(EchtSaetze.B1, "ae").Select(s => s.Kennung));
        Assert.Equal(Neu.Skip(5), bestand.Fuer(EchtSaetze.B2, "ae").Select(s => s.Kennung));
        // die alten bleiben fuer offene Laeufe und alte Ergebnisse geladen
        Assert.All(EchtInhalteAeTests.Ae, k => Assert.NotNull(bestand.Satz(k)));
        Assert.All(EchtInhalteAeTests.Ae, k => Assert.True(bestand.IstAusgeschlossen(k)));
        // Gegenprobe: ohne Ausschluss boete die App alle zehn AE-Saetze je Bereich an
        var ohne = EchtSaetze.Laden(File.ReadAllBytes(EchtTestDatei.Datei));
        Assert.Equal(10, ohne.Fuer(EchtSaetze.B1, "ae").Count);
        Assert.Empty(ohne.Ausgeschlossen);
    }

    [Fact]
    public void AE_Ausschluss_WirktNurMitStelleUndKennung()
    {
        var ordner = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen");
        byte[]? Lesen(string n, string? ausschluss) => n == "ausschluss" && ausschluss is not null ? Encoding.UTF8.GetBytes(ausschluss)
            : File.Exists(Path.Combine(ordner, n + ".json")) ? File.ReadAllBytes(Path.Combine(ordner, n + ".json")) : null;
        string Eintrag(int stelle, string kennung) => "{\"eintraege\": [{\"datei\": \"pruefungssaetze\", \"stelle\": "
            + stelle.ToString(CultureInfo.InvariantCulture) + ", \"kennung\": \"" + kennung + "\"}]}";
        Assert.Equal(["ae_b1_01"], new Core.Inhalte.LernenOrdner(n => Lesen(n, Eintrag(17, "ae_b1_01"))).AusgeschlosseneSaetze());
        // Gegenprobe: falsche Stelle oder andere Kennung an der Stelle wirkt nicht
        Assert.Empty(new Core.Inhalte.LernenOrdner(n => Lesen(n, Eintrag(18, "ae_b1_01"))).AusgeschlosseneSaetze());
        Assert.Empty(new Core.Inhalte.LernenOrdner(n => Lesen(n, Eintrag(17, "ae_b1_02"))).AusgeschlosseneSaetze());
        // ein Satz-Ausschluss nimmt nie eine Karte oder Frage heraus (andere Datei)
        Assert.Equal(EchtInhalteAeTests.Ae.Length, EchtTestDatei.Ausgeschlossen().Count);
    }

    [Fact]
    public void AE_NurAngehaengt_DateiDavorBytegleich_Format3()
    {
        var echt = File.ReadAllBytes(EchtTestDatei.Datei);
        // A 1.8.0 (Weisung P12-372): die Datei traegt Format 4 (neue Fassungen); geprueft wird weiter der Teil bis A 0.93.0
        Assert.Equal(4, (int)JsonNode.Parse(echt)!["format"]!);
        var roh = EchtTestDatei.AlsFormat2(echt);
        Assert.True(roh.Length > VorherLaenge);
        Assert.Equal(VorherSha, Convert.ToHexStringLower(SHA256.HashData(roh.AsSpan(0, VorherLaenge))));
        Assert.Contains("\"kennung\": \"ae_b1_06\"", Encoding.UTF8.GetString(roh, VorherLaenge, 400), StringComparison.Ordinal);
        // Gegenprobe: ein geaendertes Zeichen vorn faellt auf, ein anderer Kopf auch
        roh[100] ^= 1;
        Assert.NotEqual(VorherSha, Convert.ToHexStringLower(SHA256.HashData(roh.AsSpan(0, VorherLaenge))));
        var anders = (byte[])echt.Clone();
        anders[13] = (byte)'5';
        Assert.ThrowsAny<Exception>(() => EchtTestDatei.AlsFormat2(anders));
    }

    [Fact]
    public void Format2_EchteTestdateiLaedt_AufbauErstAbFormat3()
    {
        var alt = File.ReadAllBytes(Format2);
        Assert.Equal(2, (int)JsonNode.Parse(alt)!["format"]!);
        var bestand = EchtSaetze.Laden(alt);
        Assert.Empty(bestand.Fehler);
        Assert.Equal(["alle_ap1_01", "si_b1_01", "ae_b1_01", "ae_b2_01"], bestand.Saetze.Select(s => s.Kennung));
        Assert.All(bestand.Saetze, s => Assert.Null(s.Aufbau));
        // ein Satzpaar in einer Datei mit Format 2: klare Meldung, nie still geladen
        var wurzel = JsonNode.Parse(alt)!.AsObject();
        var liste = wurzel["saetze"]!.AsArray();
        foreach (var k in new[] { "ae_b1_06", "ae_b2_06" })
        {
            liste.Add(Roh(k));
        }
        var fehler = EchtSaetze.Laden(Encoding.UTF8.GetBytes(wurzel.ToJsonString())).Fehler;
        Assert.Contains(fehler, f => f.Kennung == "ae_b1_06" && f.Meldung.Contains("erst ab Format 3", StringComparison.Ordinal));
        // Gegenprobe: dieselbe Datei als Format 3 laedt alle sechs Saetze
        wurzel["format"] = 3;
        var neu = EchtSaetze.Laden(Encoding.UTF8.GetBytes(wurzel.ToJsonString()));
        Assert.Empty(neu.Fehler);
        Assert.Equal(6, neu.Saetze.Count);
    }

    // ---- (2) Paare --------------------------------------------------------------------------------------------------------

    [Fact]
    public void AE_JedesPaarEineFirma_Bereich2FuehrtFort_RichtigeFachrichtung()
    {
        var alle = App.Saetze;
        EchtTestDatei.FirmaJeSatzOderPaar(alle);
        foreach (var (b1, b2) in Paare)
        {
            Assert.True(b1.Paar && b2.Paar);
            Assert.Equal(b1.Firma, b2.Firma);
            Assert.Equal((EchtSaetze.B1, EchtSaetze.B2), (b1.Bereich, b2.Bereich));
            foreach (var s in new[] { b1, b2 })
            {
                var aus = string.Join(" ", s.Ausgangssituation);
                Assert.Contains("Anwendungsentwicklung", aus, StringComparison.Ordinal);
                Assert.Contains(s.Firma, aus, StringComparison.Ordinal);
                Assert.Contains("Auszubildende", aus, StringComparison.Ordinal);
                foreach (var fremd in new[] { "Systemintegration", "Daten- und Prozessanalyse", "Digitale Vernetzung" })
                {
                    Assert.DoesNotContain(fremd, aus, StringComparison.Ordinal);
                }
                Assert.DoesNotContain("IHK", Alles(s), StringComparison.Ordinal);
                for (var n = 0; n < 4; n++)
                {
                    Assert.Contains($"{n + 1}. {s.Aufgaben[n].Titel}", s.Ausgangssituation);
                }
            }
            // Bereich 2 fuehrt das Projekt aus Bereich 1 fort und sagt das in der Ausgangssituation
            Assert.Contains("Bereich 1", string.Join(" ", b2.Ausgangssituation), StringComparison.Ordinal);
        }
        // Gegenprobe: dieselbe Firma in zwei Saetzen, die kein Paar sind, waere ein Fund
        var fremdeFirma = alle.Select(s => s.Kennung == "si_b1_01" ? s with { Firma = alle.First(x => x.Kennung == "dv_b1_01").Firma } : s).ToList();
        Assert.ThrowsAny<Exception>(() => EchtTestDatei.FirmaJeSatzOderPaar(fremdeFirma));
    }

    // ---- (3) Grenzen aus Abschnitt 7 -----------------------------------------------------------------------------------------

    /// <summary>Gewicht je Paar: (Diagramme, Pseudocode und SQL) von 200 Punkten.</summary>
    private static (int Diagramm, int Code) Gewicht(EchtSatz b1, EchtSatz b2)
    {
        var teile = b1.Aufgaben.Concat(b2.Aufgaben).SelectMany(a => a.Teile).ToList();
        return (teile.Where(t => t.Gebiet == "diagramm").Sum(t => t.Punkte), teile.Where(t => t.Gebiet is "pseudocode" or "sql").Sum(t => t.Punkte));
    }

    /// <summary>Funde der festen Grenzen eines Paars (leer = alles in den Grenzen).</summary>
    private static List<string> GrenzenFunde(EchtSatz b1, EchtSatz b2)
    {
        var funde = new List<string>();
        foreach (var s in new[] { b1, b2 })
        {
            var teile = s.Aufgaben.SelectMany(a => a.Teile).ToList();
            if (s.Aufgaben.Sum(a => a.Punkte) != 100 || s.Aufgaben.Any(a => a.Punkte is < 20 or > 30))
            {
                funde.Add(s.Kennung + ": Punkte je Aufgabe");
            }
            foreach (var a in s.Aufgaben)
            {
                var gruppen = EchtSaetze.Teilaufgaben(a.Teile);
                if (gruppen.Count is < 3 or > 6 || gruppen.Any(g => g.Punkte is < 2 or > 14) || a.Teile.Count > 10)
                {
                    funde.Add(s.Kennung + " " + a.Titel + ": Teilaufgaben");
                }
            }
            var gross = s.Aufgaben.Sum(a => EchtSaetze.Teilaufgaben(a.Teile).Count(g => g.Punkte >= 8));
            if (gross is < 2 or > 3)
            {
                funde.Add(s.Kennung + ": " + gross + " grosse Teilaufgaben");
            }
            if (teile.Count(t => t.Antwort.Form == EchtSaetze.FormRechnen || t.Operator == "berechnen") > 1)
            {
                funde.Add(s.Kennung + ": mehr als ein Rechenteil");
            }
            if (teile.Where(t => t.Englisch).Sum(t => t.Punkte) > 12)
            {
                funde.Add(s.Kennung + ": Englisch ueber 12 Punkte");
            }
        }
        if (b1.Anlagen.Concat(b2.Anlagen).Count(a => a.Art == "englisch") != 1)
        {
            funde.Add(b1.Kennung + ": nicht genau ein englischer Text im Paar");
        }
        var (diagramm, code) = Gewicht(b1, b2);
        if (diagramm is < 40 or > 60)
        {
            funde.Add(b1.Kennung + ": Diagramme " + diagramm + " von 200 (etwa ein Viertel: 40 bis 60)");
        }
        if (code is < 55 or > 80)
        {
            funde.Add(b1.Kennung + ": Pseudocode und SQL " + code + " von 200 (etwa ein Drittel: 55 bis 80)");
        }
        return funde;
    }

    [Fact]
    public void AE_GrenzenAbschnitt7_JedesPaar_MitGegenprobe()
    {
        foreach (var (b1, b2) in Paare)
        {
            Assert.Empty(GrenzenFunde(b1, b2));
            // jeder Teil mit Diagramm, Pseudocode oder SQL ist so markiert (sonst stimmt das Gewicht nicht)
            foreach (var t in b1.Aufgaben.Concat(b2.Aufgaben).SelectMany(a => a.Teile))
            {
                var text = string.Join(" ", t.Text);
                if (t.Antwort.Form == EchtSaetze.FormZeichnen || text.Contains("auf Papier", StringComparison.Ordinal))
                {
                    Assert.Equal("diagramm", t.Gebiet);
                }
                if (text.Contains("SQL-Anweisung", StringComparison.Ordinal))
                {
                    Assert.Equal("sql", t.Gebiet);
                }
            }
        }
        // Gegenproben: ohne Gebiete fehlt das Gewicht; ein Paar mit zwei englischen Texten; vier grosse Teilaufgaben
        var (p1, p2) = Paare.First();
        var ohneGebiet = p1 with { Aufgaben = [.. p1.Aufgaben.Select(a => a with { Teile = [.. a.Teile.Select(t => t with { Gebiet = null })] })] };
        Assert.Contains(GrenzenFunde(ohneGebiet, p2 with { Aufgaben = [.. p2.Aufgaben.Select(a => a with { Teile = [.. a.Teile.Select(t => t with { Gebiet = null })] })] }),
            f => f.Contains("Diagramme", StringComparison.Ordinal));
        var englisch = p1.Anlagen.Concat(p2.Anlagen).First(a => a.Art == "englisch");
        Assert.Contains(GrenzenFunde(p1 with { Anlagen = [.. p1.Anlagen, englisch] }, p2 with { Anlagen = [.. p2.Anlagen, englisch] }),
            f => f.Contains("englischer Text", StringComparison.Ordinal));
    }

    /// <summary>Was der Baukasten nicht kann, steht auf Papier (Zeilen mit Musterloesung und Haekchen), im Baukasten nur UML und ER.</summary>
    [Fact]
    public void AE_ZeichnenWieHeute_PapierMitMusterloesung()
    {
        foreach (var s in NeueSaetze)
        {
            foreach (var t in s.Aufgaben.SelectMany(a => a.Teile))
            {
                var text = string.Join(" ", t.Text);
                if (t.Antwort.Form == EchtSaetze.FormZeichnen)
                {
                    // A 1.8.0 (Weisung P12-372): Klassen- und Use-Case-Diagramm nur in den neuen Fassungen und nur dort, wo die
                    // neue Fassung den Teil geaendert hat (EchtW1BaukastenTests prueft die fuenf Teile einzeln)
                    Assert.Contains(t.Antwort.Diagramm, t.Fassung >= 2
                        ? new[] { EchtZeichnung.Klasse, EchtZeichnung.UseCase } : new[] { EchtZeichnung.Uml, EchtZeichnung.Er });
                }
                if (t.Operator is "erstellen" or "zeichnen" && t.Antwort.Form != EchtSaetze.FormZeichnen
                    && (text.Contains("diagramm", StringComparison.OrdinalIgnoreCase) || text.Contains("Skizze", StringComparison.Ordinal)
                        || text.Contains("Struktogramm", StringComparison.Ordinal)))
                {
                    Assert.Contains("auf Papier", text, StringComparison.Ordinal);
                    Assert.Equal(EchtSaetze.FormZeilen, t.Antwort.Form);
                    Assert.True(t.Loesung.Count >= 3 && t.Hinweise.Sum(h => h.Punkte) == t.Punkte, s.Kennung + " " + t.Nr);
                }
            }
        }
    }

    // ---- (4) Aufgabenarten verteilt -----------------------------------------------------------------------------------------

    /// <summary>Wie viele Paare eine Aufgabenart enthalten (Suche in Text, Anlagen und Musterloesung beider Saetze).</summary>
    private static int PaareMit(Func<string, bool> art) => Paare.Count(p => art(Alles(p.B1) + "\n" + Alles(p.B2)));

    [Fact]
    public void AE_AufgabenartenAbschnitt7_UeberDiePaareVerteilt()
    {
        static bool Hat(string text, params string[] woerter) => woerter.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));
        Assert.True(PaareMit(t => Hat(t, "Sequenzdiagramm")) >= 2);
        Assert.True(PaareMit(t => Hat(t, "Schwimmbahn")) >= 2);
        Assert.True(PaareMit(t => Hat(t, "GET") && Hat(t, "POST") && Hat(t, "Statuscode")) >= 2);
        Assert.True(PaareMit(t => Hat(t, "Stakeholder")) >= 2);
        Assert.True(PaareMit(t => Hat(t, "Vergleichsfunktion")) >= 2);
        Assert.True(PaareMit(t => Hat(t, "Regressionstest")) >= 2);
        Assert.True(PaareMit(t => Hat(t, "Zweigüberdeckung")) >= 2);
        Assert.True(PaareMit(t => Hat(t, "UNION")) >= 1);
        Assert.True(PaareMit(t => Hat(t, "INSERT INTO") && Hat(t, "DELETE")) >= 2);
        Assert.True(PaareMit(t => Hat(t, "GROUP BY")) >= 3);
        Assert.True(PaareMit(t => Hat(t, "Klassendiagramm")) >= 2);
        Assert.True(PaareMit(t => Hat(t, "Passwort")) >= 1);
        // jedes Paar hat SQL in Bereich 2
        foreach (var (_, b2) in Paare)
        {
            Assert.Contains(b2.Aufgaben.SelectMany(a => a.Teile), t => t.Gebiet == "sql");
        }
        // Gegenprobe: eine Art, die es nicht gibt, findet die Suche nicht
        Assert.Equal(0, PaareMit(t => Hat(t, "Spanning-Tree-Wurzelbruecke")));
    }

    // ---- (3) Ladepruefung der Paar-Regeln: je Regel ein verdorbenes Paar -----------------------------------------------------

    /// <summary>Ein Satz des Paars 06 aus der eingebauten Datei als JSON.</summary>
    private static JsonObject Roh(string kennung) => JsonNode.Parse(File.ReadAllBytes(EchtTestDatei.Datei))!["saetze"]!.AsArray()
        .Select(s => s!.AsObject()).First(s => (string?)s["kennung"] == kennung).DeepClone().AsObject();

    private static EchtBestand LadenPaar(JsonObject b1, JsonObject b2, int format = 3) => EchtSaetze.Laden(Encoding.UTF8.GetBytes(
        new JsonObject { ["format"] = format, ["saetze"] = new JsonArray(b1, b2) }.ToJsonString()));

    private static JsonArray Teile(JsonObject satz, int aufgabe) => satz["aufgaben"]![aufgabe - 1]!["teile"]!.AsArray();

    public static TheoryData<string, string> PaarRegeln() => new()
    {
        { "aufgabe-31", "erlaubt sind 20 bis 30" },
        { "teil-15", "erlaubt sind 2 bis 14" },
        { "teilaufgaben-2", "erlaubt sind 3 bis 6" },
        { "b-und-ba", "allein und mit Unterpunkten" },
        { "nennen-7", "Nennen: 2 bis 6 Angaben" },
        { "zwei-rechenteile", "höchstens einer" },
        { "gross-4", "große Teilaufgaben" },
        { "englisch-13", "höchstens 12" },
        { "englisch-beide", "genau einen englischen Text" },
        { "englisch-keiner", "genau einen englischen Text" },
        { "firma-anders", "andere Firma" },
        { "partner-fehlt", "fehlt oder ist ungültig" },
        { "aufbau-unbekannt", "kennt nur \"paar\"" },
        { "gebiet-unbekannt", "\"gebiet\" gibt es nur im Satzpaar" },
        { "gebiet-ohne-paar", "\"gebiet\" gibt es nur im Satzpaar" },
    };

    [Theory]
    [MemberData(nameof(PaarRegeln))]
    public void Ladepruefung_PaarRegel_WeistDenSatzAb(string fall, string meldung)
    {
        var b1 = Roh("ae_b1_06");
        var b2 = Roh("ae_b2_06");
        // Gegenprobe zuerst: unveraendert laedt das Paar ohne Meldung
        Assert.Empty(LadenPaar(b1.DeepClone().AsObject(), b2.DeepClone().AsObject()).Fehler);
        Verderben(fall, b1, b2);
        var bestand = LadenPaar(b1, b2);
        Assert.Contains(bestand.Fehler, f => f.Meldung.Contains(meldung, StringComparison.Ordinal));
        // nie still: jeder fehlende Satz steht mit Kennung in den Fehlern (faellt ein Satz, faellt sein Partner mit)
        Assert.DoesNotContain(bestand.Saetze, s => s.Paar);
        Assert.Equal(2, bestand.Fehler.Select(f => f.Kennung).Distinct().Count());
    }

    private static void Verderben(string fall, JsonObject b1, JsonObject b2)
    {
        var englischSatz = b1["anlagen"]!.AsArray().Any(a => (string?)a!["art"] == "englisch") ? b1 : b2;
        var ohneEnglisch = englischSatz == b1 ? b2 : b1;
        JsonObject ErsterTeil(JsonObject s, Func<JsonObject, bool> wo) => s["aufgaben"]!.AsArray()
            .SelectMany(a => a!["teile"]!.AsArray()).Select(t => t!.AsObject()).First(wo);
        switch (fall)
        {
            case "aufgabe-31":
                // Aufgabe 1 auf 31 Punkte (ein Teil +x), Aufgabe 2 entsprechend weniger: Summe bleibt 100
                var a1 = b1["aufgaben"]![0]!.AsObject();
                var a2 = b1["aufgaben"]![1]!.AsObject();
                var plus = 31 - (int)a1["punkte"]!;
                a1["punkte"] = 31;
                a2["punkte"] = (int)a2["punkte"]! - plus;
                Teile(b1, 1).Add(new JsonObject { ["nr"] = "z" });
                Teile(b1, 1).RemoveAt(Teile(b1, 1).Count - 1);
                break;
            case "teil-15":
                ErsterTeil(b1, t => true)["punkte"] = 15;
                break;
            case "teilaufgaben-2":
                var aufgabe = b1["aufgaben"]![0]!.AsObject();
                var teile = Teile(b1, 1);
                while (EchtSaetze.Teilaufgaben([.. teile.Select(t => new EchtTeil((string)t!["nr"]!, "", [], (int)t["punkte"]!, 1,
                    new EchtAntwort("zeilen"), [], null, null, [], [], false))]).Count > 2)
                {
                    teile.RemoveAt(teile.Count - 1);
                }
                break;
            case "b-und-ba":
                var tl = Teile(b1, 1);
                var zweiter = tl[1]!.AsObject();
                var neu = zweiter.DeepClone().AsObject();
                neu["nr"] = ((string)tl[0]!["nr"]!)[..1] + "a";
                tl.Add(neu);
                break;
            case "nennen-7":
                var nennen = ErsterTeil(b1, t => (string?)t["operator"] == "nennen");
                nennen["angaben"] = 7;
                break;
            case "zwei-rechenteile":
                foreach (var t in b1["aufgaben"]!.AsArray().SelectMany(a => a!["teile"]!.AsArray()).Take(2))
                {
                    t!["operator"] = "berechnen";
                }
                break;
            case "gross-4":
                // aus zwei kleinen Teilen wird je eine grosse Teilaufgabe mit Unterpunkten (ae_b1_06: 1b+1c = 10, 4a+4b = 12),
                // dazu die grossen 2a und 3a: vier grosse Teilaufgaben in Bereich 1
                foreach (var (nrAufgabe, erster, buchstabe) in new[] { (0, 1, "b"), (3, 0, "a") })
                {
                    var tg = Teile(b1, nrAufgabe + 1);
                    tg[erster]!["nr"] = buchstabe + "a";
                    tg[erster + 1]!["nr"] = buchstabe + "b";
                }
                break;
            case "englisch-13":
                // weitere Teile derselben Aufgabe werden zum englischen Text gezaehlt, bis es mehr als 12 Punkte sind
                var engNr = (string)englischSatz["anlagen"]!.AsArray().First(a => (string?)a!["art"] == "englisch")!["nr"]!;
                var engAufgabe = englischSatz["aufgaben"]!.AsArray().First(a => a!["teile"]!.AsArray().Any(t => (bool?)t!["englisch"] == true))!;
                foreach (var t in engAufgabe["teile"]!.AsArray().Where(t => (bool?)t!["englisch"] != true))
                {
                    if (engAufgabe["teile"]!.AsArray().Where(x => (bool?)x!["englisch"] == true).Sum(x => (int)x!["punkte"]!) > 12)
                    {
                        break;
                    }
                    t!["englisch"] = true;
                    var verweise = t["anlagen"] as JsonArray ?? [];
                    verweise.Add(engNr);
                    t["anlagen"] = verweise.DeepClone();
                }
                break;
            case "englisch-beide":
                // jeder Satz fuer sich ist gueltig (ein Text, ein Teil dazu), das Paar hat aber zwei englische Texte
                ohneEnglisch["anlagen"]!.AsArray().Add(englischSatz["anlagen"]!.AsArray().First(a => (string?)a!["art"] == "englisch")!.DeepClone());
                ohneEnglisch["anlagen"]!.AsArray().Last()!["nr"] = "99";
                var engTeil = ErsterTeil(ohneEnglisch, t => (int)t["punkte"]! <= 12);
                var vw = engTeil["anlagen"] as JsonArray ?? [];
                vw.Add("99");
                engTeil["anlagen"] = vw.DeepClone();
                engTeil["englisch"] = true;
                break;
            case "englisch-keiner":
                var eng = englischSatz["anlagen"]!.AsArray().First(a => (string?)a!["art"] == "englisch")!.AsObject();
                eng["art"] = "text";
                foreach (var t in englischSatz["aufgaben"]!.AsArray().SelectMany(a => a!["teile"]!.AsArray()))
                {
                    t!.AsObject().Remove("englisch");
                }
                break;
            case "firma-anders":
                b2["firma"] = "Andere Firma GmbH";
                break;
            case "partner-fehlt":
                b2["kennung"] = "ae_b2_99";
                break;
            case "aufbau-unbekannt":
                b1["aufbau"] = "einzeln";
                break;
            case "gebiet-unbekannt":
                ErsterTeil(b1, t => true)["gebiet"] = "netz";
                break;
            case "gebiet-ohne-paar":
                b1.Remove("aufbau");
                ErsterTeil(b1, t => true)["gebiet"] = "sql";
                break;
            default:
                throw new ArgumentException(fall);
        }
    }

    // ---- (5) offener Lauf eines alten Satzes --------------------------------------------------------------------------------

    [Fact]
    public void AlterSatz_OffenerLauf_HinweisWeiterBeendenAbschliessen_LernstandBleibt()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var jetzt = DateTimeOffset.Parse("2026-11-19T08:00:00Z", CultureInfo.InvariantCulture);
        var schreiber = new LernstandSchreiber(lernstand, SpeichernTests.FesteUhr(() => jetzt));
        var werte = new JsonObject();
        bool Aendern(Action<JsonObject> a)
        {
            var neu = werte.DeepClone().AsObject();
            a(neu);
            werte = neu;
            return true;
        }
        var ohneAusschluss = EchtSaetze.Laden(File.ReadAllBytes(EchtTestDatei.Datei));
        var art = EchtArten.Art(Fachrichtung.Nach("ae"), EchtSaetze.B1);
        // vor dem Update: Lauf des alten Satzes ae_b1_01 begonnen und eine Antwort gespeichert
        var vorher = new EchtAblauf(() => ohneAusschluss, new EchtAblage(() => werte.DeepClone().AsObject(), Aendern), () => schreiber, () => jetzt, KlausurTests.Berlin);
        Assert.Equal(EchtAnsicht.Lauf, vorher.Starten(ohneAusschluss.Satz("ae_b1_01")!, art).Ansicht);
        var feld = EchtBewertung.Feld(1, ohneAusschluss.Satz("ae_b1_01")!.Aufgaben[0].Teile[0].Nr, "1");
        Assert.True(vorher.Festhalten([(feld, "meine Antwort")]));
        // nach dem Update (alte Saetze ausgeschlossen): kein Absturz, der Lauf geht mit Hinweis weiter
        jetzt = jetzt.AddMinutes(5);
        var nachher = new EchtAblauf(() => App, new EchtAblage(() => werte.DeepClone().AsObject(), Aendern), () => schreiber, () => jetzt, KlausurTests.Berlin);
        var schritt = nachher.Zeigen();
        Assert.Equal(EchtAnsicht.Lauf, schritt.Ansicht);
        Assert.Equal(EchtTexte.SatzErsetzt, schritt.Hinweis);
        Assert.Equal("meine Antwort", Json.Text(nachher.Zustand!["antworten"]![feld]));
        Assert.Equal("ae_b1_01", nachher.Satz!.Kennung);
        // beenden und abschliessen: genau ein Eintrag im Lernstand
        Assert.Equal(EchtAnsicht.Bewertung, nachher.Abgeben(auto: false).Ansicht);
        Assert.Equal(EchtTexte.SatzErsetzt, nachher.Zeigen().Hinweis);
        Assert.Equal(EchtAnsicht.Ergebnis, nachher.Abschliessen().Ansicht);
        Assert.Equal(1L, (long)lernstand.Abfragen("SELECT COUNT(*) FROM pruefungen")[0][0]!);
        Assert.Null(werte[EchtAblage.Schluessel]);
        // danach bietet die App den alten Satz nicht mehr an
        Assert.DoesNotContain(App.Fuer(EchtSaetze.B1, "ae"), s => s.Kennung == "ae_b1_01");
        Assert.NotEqual("ae_b1_01", EchtAblauf.NaechsterSatz(App.Fuer(EchtSaetze.B1, "ae"), art, [])!.Kennung);
        // Gegenprobe: ein Lauf eines neuen Satzes zeigt keinen Hinweis
        Assert.Equal(EchtAnsicht.Lauf, nachher.Starten(App.Satz("ae_b1_06")!, art).Ansicht);
        Assert.Null(nachher.Zeigen().Hinweis);
    }

    // ---- (7) Teilzeiten von "Inhalte laden" ---------------------------------------------------------------------------------

    [Fact]
    public void InhalteLaden_TeileNurAusgabe()
    {
        LadeTeile.Zuruecksetzen();
        Assert.Equal(0, LadeTeile.DateiMs);
        var bytes = LadeTeile.Datei(() => File.ReadAllBytes(EchtTestDatei.Datei));
        var json = LadeTeile.Json(() => JsonNode.Parse(bytes));
        Assert.NotNull(json);
        Assert.True(LadeTeile.DateiMs > 0 && LadeTeile.JsonMs > 0);
        Assert.Equal("Teile: Datei lesen 12 ms, JSON lesen 140 ms, Sicht aufbauen 31 ms (Datei und JSON: Arbeitszeit aller Threads)",
            LadeTeile.Text(12.4, 139.6, 30.6));
        // Gegenprobe: nach dem Zuruecksetzen wieder 0
        LadeTeile.Zuruecksetzen();
        Assert.Equal(0, LadeTeile.JsonMs);
    }

    [Fact]
    public void InhalteLaden_TeileMitHauptfaden_NurAusgabe()
    {
        // Welle A 0.97.0 (Weisung P12-312 Punkt 6, E1): Arbeit im Hintergrund zaehlt nur bei "alle Threads"
        LadeTeile.Zuruecksetzen();
        var bytes = LadeTeile.Datei(() => File.ReadAllBytes(EchtTestDatei.Datei));
        // eigener Faden (Task.Wait koennte die Aufgabe im wartenden Faden ausfuehren)
        var faden = new Thread(() => LadeTeile.Json(() => JsonNode.Parse(bytes)));
        faden.Start();
        faden.Join();
        Assert.True(LadeTeile.JsonMs > 0);
        Assert.Equal(0, LadeTeile.JsonHauptMs);
        Assert.True(LadeTeile.DateiHauptMs > 0 && LadeTeile.DateiHauptMs <= LadeTeile.DateiMs);
        LadeTeile.Json(() => JsonNode.Parse(bytes));
        Assert.True(LadeTeile.JsonHauptMs > 0 && LadeTeile.JsonHauptMs <= LadeTeile.JsonMs);
        Assert.Equal("Teile: Datei lesen 12 ms, JSON lesen 140 ms, Sicht aufbauen 31 ms (Datei und JSON: Arbeitszeit aller Threads;"
            + " davon im Hauptfaden: Datei lesen 3 ms, JSON lesen 61 ms)", LadeTeile.Text(12.4, 139.6, 30.6, 3.1, 60.6));
        LadeTeile.Zuruecksetzen();
        Assert.Equal(0, LadeTeile.JsonHauptMs);
    }
}
