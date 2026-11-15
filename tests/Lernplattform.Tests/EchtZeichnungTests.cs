using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Pruefung.Echt;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.77.0, Welle 2 "Prüfung im echten Format" (Weisung P12-265): Format 2 der Pruefungssaetze mit gezeichneten Anlagen
/// (Netzplan, Monitor-Bild) und Zeichenaufgaben (UML-Aktivitaet, ER in Chen) samt Textfassung fuer den Screenreader
/// (Abnahmesatz W2-1), Probesatz AP2 Konzeption si_b1_01, echte Testdatei des Formats 1. Jede Regel mit Gegenprobe.
/// </summary>
public class EchtZeichnungTests
{
    private static readonly string Datei = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen", EchtSaetze.Datei);

    private static readonly string Format1 = Path.Combine(Pfade.Wurzel, "tests", "Lerninhalte", "echt", "pruefungssaetze_format1.json");

    /// <summary>Seit A 0.79.0 nur die Probesaetze (die 15 Saetze der W3 prueft <see cref="EchtInhalteW3Tests"/>).</summary>
    private static JsonObject Wurzel() => JsonNode.Parse(EchtInhalteW3Tests.NurProbesaetze())!.AsObject();

    private static EchtBestand Laden(JsonObject wurzel) => EchtSaetze.Laden(Encoding.UTF8.GetBytes(wurzel.ToJsonString()));

    private static EchtSatz B1 => EchtSaetze.Laden(File.ReadAllBytes(Datei)).Saetze.Single(s => s.Kennung == "si_b1_01");

    private static JsonObject B1Knoten(JsonObject wurzel) =>
        wurzel["saetze"]!.AsArray().Select(s => s!.AsObject()).Single(s => (string?)s["kennung"] == "si_b1_01");

    private static JsonObject Anlage(JsonObject satz, string nr) =>
        satz["anlagen"]!.AsArray().Select(a => a!.AsObject()).Single(a => (string?)a["nr"] == nr);

    private static JsonObject Antwort(JsonObject satz, int aufgabe, string nr) =>
        satz["aufgaben"]![aufgabe - 1]!["teile"]!.AsArray().Select(t => t!.AsObject()).Single(t => (string?)t["nr"] == nr)["antwort"]!.AsObject();

    /// <summary>Kleines vollstaendiges UML-Diagramm wie im Start ohne Bedienung.</summary>
    private const string Uml = """
        {"art": "uml", "elemente": [
          {"id": "s", "typ": "start", "x": 1, "y": 0},
          {"id": "a1", "typ": "aktion", "text": "Meldung aufnehmen", "x": 1, "y": 1},
          {"id": "v", "typ": "verzweigung", "x": 1, "y": 2},
          {"id": "a2", "typ": "aktion", "text": "Bereitschaft anrufen", "x": 0, "y": 3},
          {"id": "a3", "typ": "aktion", "text": "Ticket anlegen", "x": 2, "y": 3},
          {"id": "z", "typ": "zusammenfuehrung", "x": 1, "y": 4},
          {"id": "e", "typ": "ende", "x": 1, "y": 5}],
         "verbindungen": [
          {"von": "s", "nach": "a1"}, {"von": "a1", "nach": "v"},
          {"von": "v", "nach": "a2", "text": "[dringend]"}, {"von": "v", "nach": "a3", "text": "[sonst]"},
          {"von": "a2", "nach": "z"}, {"von": "a3", "nach": "z"}, {"von": "z", "nach": "e"}]}
        """;

    // -- Probesatz und Formate ---------------------------------------------------------------------

    [Fact]
    public void W2_ProbesatzKonzeption_LaedtMitNetzplanMonitorUndErZeichnung()
    {
        var bestand = EchtSaetze.Laden(EchtInhalteW3Tests.NurProbesaetze());
        Assert.Empty(bestand.Fehler);
        var satz = B1;
        Assert.Equal(("si", "b1"), (satz.Fachrichtung, satz.Bereich));
        Assert.Equal(100, satz.Aufgaben.Sum(a => a.Punkte));
        // Strukturblatt AP2 Bereich 1: 24 bis 25 Punkte je Aufgabe, 5 bis 8 Teilaufgaben, 3 bis 9 Punkte je Teil
        Assert.All(satz.Aufgaben, a => Assert.InRange(a.Punkte, 24, 25));
        Assert.All(satz.Aufgaben, a => Assert.InRange(a.Teile.Count, 5, 8));
        Assert.All(satz.Aufgaben.SelectMany(a => a.Teile), t => Assert.InRange(t.Punkte, 3, 9));
        Assert.NotNull(satz.Anlagen.Single(a => a.Art == "netzplan").Netzplan);
        Assert.NotNull(satz.Anlagen.Single(a => a.Art == "monitor").Monitor);
        var zeichnen = Assert.Single(satz.Aufgaben.SelectMany(a => a.Teile), t => t.Antwort.Form == EchtSaetze.FormZeichnen);
        Assert.Equal(EchtZeichnung.Er, zeichnen.Antwort.Diagramm);
        Assert.NotNull(zeichnen.Antwort.Vorgabe);
        Assert.Equal(9, zeichnen.SelbstPunkte);
        Assert.Equal(zeichnen.Punkte, zeichnen.Hinweise.Sum(h => h.Punkte));
        // neue Firma, nie der Platzhalter der Skizze, nie die Firma des AP1-Probesatzes
        Assert.Equal("Lindgrün Pflegedienst GmbH", satz.Firma);
        Assert.NotEqual(bestand.Saetze.Single(s => s.Bereich == EchtSaetze.Ap1).Firma, satz.Firma);
    }

    [Fact]
    public void Format1_EchteTestdateiLaedt_UndKenntKeineZeichnungen()
    {
        var alt = File.ReadAllBytes(Format1);
        Assert.Equal(1, (int)JsonNode.Parse(alt)!["format"]!);
        var bestand = EchtSaetze.Laden(alt);
        Assert.Empty(bestand.Fehler);
        Assert.Equal("alle_ap1_01", Assert.Single(bestand.Saetze).Kennung);
        // der AP1-Probesatz ist in beiden Formaten gleich (nur angehaengt)
        var heute = EchtSaetze.Laden(File.ReadAllBytes(Datei)).Saetze.Single(s => s.Kennung == "alle_ap1_01");
        Assert.Equal(JsonNode.Parse(alt)!["saetze"]![0]!.ToJsonString(), Wurzel()["saetze"]![0]!.ToJsonString());
        Assert.Equal(heute.Aufgaben.Count, bestand.Saetze[0].Aufgaben.Count);
        // Format 1 mit gezeichneter Anlage oder Zeichenaufgabe: klare Meldung, nie still geladen
        var wurzel = JsonNode.Parse(alt)!.AsObject();
        wurzel["saetze"]!.AsArray().Add(B1Knoten(Wurzel()).DeepClone());
        var fehler = Assert.Single(Laden(wurzel).Fehler);
        Assert.Equal("si_b1_01", fehler.Kennung);
        Assert.Contains("erst ab Format 2", fehler.Meldung, StringComparison.Ordinal);
        // Gegenprobe: dieselbe Datei als Format 2 laedt beide Saetze
        wurzel["format"] = 2;
        Assert.Empty(Laden(wurzel).Fehler);
    }

    [Fact]
    public void Format1_ZeichenaufgabeAllein_IstImFormat1EinFehler()
    {
        var wurzel = Wurzel();
        var b1 = B1Knoten(wurzel);
        // nur die Zeichenaufgabe stoert (Netzplan und Monitor werden zu Text): Meldung nennt die Antwortform
        foreach (var nr in new[] { "1", "2" })
        {
            var anlage = Anlage(b1, nr);
            anlage["art"] = "text";
            anlage["zeilen"] = new JsonArray("Text statt Bild");
        }
        wurzel["format"] = 1;
        Assert.Contains("unbekannte Antwortform \"zeichnen\" (erst ab Format 2)", Assert.Single(Laden(wurzel).Fehler).Meldung, StringComparison.Ordinal);
        wurzel["format"] = 2;
        Assert.Empty(Laden(wurzel).Fehler);
    }

    // -- Ladepruefung der Zeichnungen: je Regel ein kaputter Satz ---------------------------------------

    public static TheoryData<string, string> Regeln() => new()
    {
        { "geraet-unbekannt", "unbekanntes Gerät \"modem\"" },
        { "geraet-stelle", "\"x\" muss eine ganze Zahl von 0 bis 7 sein" },
        { "geraet-doppelt-stelle", "zwei Teile an derselben Stelle" },
        { "geraet-id-doppelt", "Gerät sw doppelt" },
        { "kante-unbekannt", "Verbindung sw–gibtsnicht passt zu keinem Teil" },
        { "kante-doppelt", "Verbindung sw–rt doppelt" },
        { "geraet-ohne-leitung", "hat keine Leitung" },
        { "monitor-werte", "je Zeitmarke einen Wert" },
        { "monitor-ueber-max", "je Zeitmarke einen Wert" },
        { "monitor-marke", "mindestens zwei Zeitmarken" },
        { "diagramm-art", "Diagramm muss \"uml\" oder \"er\" sein" },
        { "baustein-falsch", "Baustein \"aktion\" gibt es im ER-Modell in Chen-Notation nicht" },
        { "baustein-ohne-text", "ohne Text" },
        { "schluessel-entitaet", "nur ein Attribut kann Schlüssel sein" },
        { "vorgabe-fremd", "der Vorgabe steht nicht gleich im Musterbild" },
        { "vorgabe-linie", "der Vorgabe fehlt im Musterbild" },
        { "er-attribut-zwei", "hängt an genau einer Entität oder Beziehung" },
        { "er-kard-falsch", "Kardinalität 1, n oder m nur dort" },
        { "er-entitaet-entitaet", "erlaubt sind Attribut zu Entität" },
        { "er-ohne-schluessel", "braucht genau ein Schlüsselattribut" },
        { "er-ohne-kard", "ohne Kardinalität" },
        { "er-beziehung-eine", "verbindet mindestens zwei Entitäten" },
        { "er-text-an-linie", "tragen keinen Text" },
        { "zeichnen-operator", "Operator Zeichnen oder Erstellen" },
        { "zeichnen-hinweise", "die Hinweise zum Musterbild ergeben nicht die Punkte" },
        { "muster-fehlt", "Musterbild fehlt" },
    };

    [Theory]
    [MemberData(nameof(Regeln))]
    public void Ladepruefung_JedeZeichenregelWeistDenSatzAb(string fall, string meldung)
    {
        var wurzel = Wurzel();
        Verderben(fall, B1Knoten(wurzel));
        var bestand = Laden(wurzel);
        var fehler = Assert.Single(bestand.Fehler);
        Assert.Equal("si_b1_01", fehler.Kennung);
        Assert.Contains(meldung, fehler.Meldung, StringComparison.Ordinal);
        // der AP1-Satz bleibt, der kaputte steht mit Kennung in den Fehlern (P12-261: nie still weg, nie Absturz)
        Assert.Equal("alle_ap1_01", Assert.Single(bestand.Saetze).Kennung);
        Assert.Single(bestand.FehlerFuer(EchtSaetze.B1, "si"));
        Assert.Empty(bestand.FehlerFuer(EchtSaetze.B1, "ae"));
    }

    private static void Verderben(string fall, JsonObject s)
    {
        var plan = Anlage(s, "2");
        var knoten = plan["knoten"]!.AsArray();
        var kanten = plan["kanten"]!.AsArray();
        var monitor = Anlage(s, "1");
        var zeichnen = Antwort(s, 3, "a");
        var muster = zeichnen["muster"]!.AsObject();
        var elemente = muster["elemente"]!.AsArray();
        var linien = muster["verbindungen"]!.AsArray();
        JsonObject Element(JsonArray liste, string id) => liste.Select(e => e!.AsObject()).Single(e => (string?)e["id"] == id);
        JsonObject Linie(string von, string nach) => linien.Select(e => e!.AsObject()).Single(e => (string?)e["von"] == von && (string?)e["nach"] == nach);
        switch (fall)
        {
            case "geraet-unbekannt":
                Element(knoten, "sw")["typ"] = "modem";
                break;
            case "geraet-stelle":
                Element(knoten, "sw")["x"] = 8;
                break;
            case "geraet-doppelt-stelle":
                Element(knoten, "nas")["x"] = 4;
                break;
            case "geraet-id-doppelt":
                Element(knoten, "nas")["id"] = "sw";
                break;
            case "kante-unbekannt":
                kanten.Add(new JsonObject { ["von"] = "sw", ["nach"] = "gibtsnicht" });
                break;
            case "kante-doppelt":
                kanten.Add(new JsonObject { ["von"] = "sw", ["nach"] = "rt" });
                break;
            case "geraet-ohne-leitung":
                knoten.Add(new JsonObject { ["id"] = "neu", ["typ"] = "server", ["name"] = "Server ohne Kabel", ["x"] = 0, ["y"] = 0 });
                break;
            case "monitor-werte":
                monitor["reihen"]![0]!["werte"]!.AsArray().RemoveAt(0);
                break;
            case "monitor-ueber-max":
                monitor["reihen"]![0]!["werte"]![3] = 101;
                break;
            case "monitor-marke":
                monitor["marken"] = new JsonArray("07:00");
                break;
            case "diagramm-art":
                zeichnen["diagramm"] = "uml2";
                break;
            case "baustein-falsch":
                Element(elemente, "uhr")["typ"] = "aktion";
                break;
            case "baustein-ohne-text":
                Element(elemente, "pat").Remove("text");
                break;
            case "schluessel-entitaet":
                Element(elemente, "pat")["schluessel"] = true;
                break;
            case "vorgabe-fremd":
                Element(zeichnen["vorgabe"]!["elemente"]!.AsArray(), "tour")["text"] = "Fahrt";
                break;
            case "vorgabe-linie":
                // die Vorgabe haengt "Name" an die Tour, das Musterbild an die Pflegekraft
                var vorgabe = zeichnen["vorgabe"]!["verbindungen"]!.AsArray();
                vorgabe.Remove(vorgabe.Select(e => e!.AsObject()).Single(e => (string?)e["nach"] == "pname"));
                vorgabe.Add(new JsonObject { ["von"] = "tour", ["nach"] = "pname" });
                break;
            case "er-attribut-zwei":
                linien.Add(new JsonObject { ["von"] = "uhr", ["nach"] = "tour" });
                break;
            case "er-kard-falsch":
                Linie("faehrt", "pk")["kard"] = "x";
                break;
            case "er-entitaet-entitaet":
                linien.Add(new JsonObject { ["von"] = "pk", ["nach"] = "pat", ["kard"] = "1" });
                break;
            case "er-ohne-schluessel":
                Element(elemente, "patnr")["schluessel"] = false;
                break;
            case "er-ohne-kard":
                Linie("besucht", "pat").Remove("kard");
                break;
            case "er-beziehung-eine":
                linien.Remove(Linie("besucht", "pat"));
                linien.Add(new JsonObject { ["von"] = "patnr", ["nach"] = "pat" });
                linien.Remove(linien.Select(e => e!.AsObject()).Single(e => (string?)e["von"] == "pat" && (string?)e["nach"] == "patnr"));
                break;
            case "er-text-an-linie":
                Linie("faehrt", "pk")["text"] = "fährt";
                break;
            case "zeichnen-operator":
                s["aufgaben"]![2]!["teile"]![0]!["operator"] = "vergleichen";
                s["aufgaben"]![2]!["teile"]![0]!["punkte"] = 5;     // Vergleichen erlaubt 4 bis 6
                break;
            case "zeichnen-hinweise":
                s["aufgaben"]![2]!["teile"]![0]!["hinweise"]!.AsArray().RemoveAt(0);
                break;
            case "muster-fehlt":
                zeichnen.Remove("muster");
                break;
            default:
                throw new ArgumentException(fall);
        }
    }

    [Fact]
    public void Ladepruefung_UmlRegeln_MitGegenprobe()
    {
        EchtDiagramm Lesen(Action<JsonObject>? aendern, bool vollstaendig = true)
        {
            var d = JsonNode.Parse(Uml)!.AsObject();
            aendern?.Invoke(d);
            return EchtZeichnung.Diagramm(d, EchtZeichnung.Uml, vollstaendig, "Test");
        }
        JsonArray Linien(JsonObject d) => d["verbindungen"]!.AsArray();
        // Gegenprobe: das vollstaendige Diagramm ist gueltig
        Assert.Equal(7, Lesen(null).Elemente.Count);
        var faelle = new (Action<JsonObject> Aendern, string Meldung)[]
        {
            (d => Linien(d)[2]!["text"] = "", "jeder mit Bedingung"),
            (d => Linien(d)[0]!["text"] = "[immer]", "eine Bedingung steht nur an Pfeilen aus einer Verzweigung"),
            (d => Linien(d).Add(new JsonObject { ["von"] = "e", ["nach"] = "a1" }), "kein Pfeil führt in den Start oder aus dem Ende"),
            (d => Linien(d)[5]!["nach"] = "e", "eine Zusammenführung hat mindestens zwei Pfeile hinein"),
            (d => d["elemente"]!.AsArray().Add(new JsonObject { ["id"] = "s2", ["typ"] = "start", ["x"] = 0, ["y"] = 0 }), "genau ein Start"),
            (d => Linien(d)[0]!["kard"] = "1", "Kardinalitäten gibt es nur im ER-Modell"),
            (d => d["elemente"]![0]!["typ"] = "entitaet", "gibt es im UML-Aktivitätsdiagramm nicht"),
        };
        foreach (var (aendern, meldung) in faelle)
        {
            var fehler = Assert.Throws<EchtLadefehler>(() => Lesen(aendern));
            Assert.Contains(meldung, fehler.Message, StringComparison.Ordinal);
        }
        // eine Vorgabe darf unvollstaendig sein (nur Start und erste Aktion), ein Musterbild nicht
        void NurAnfang(JsonObject d)
        {
            d["elemente"] = new JsonArray(d["elemente"]![0]!.DeepClone(), d["elemente"]![1]!.DeepClone());
            d["verbindungen"] = new JsonArray(Linien(d)[0]!.DeepClone());
        }
        Assert.Equal(2, Lesen(NurAnfang, vollstaendig: false).Elemente.Count);
        Assert.Throws<EchtLadefehler>(() => Lesen(NurAnfang));
    }

    // -- Textfassung fuer den Screenreader (Abnahmesatz W2-1) ----------------------------------------

    [Fact]
    public void W2_1_Netzplan_TextfassungNenntJedesGeraetUndJedeLeitung()
    {
        var plan = B1.Anlagen.Single(a => a.Art == "netzplan").Netzplan!;
        var text = EchtZeichnung.Textfassung(plan);
        Assert.Equal(plan.Knoten.Count, text.Count);
        Assert.Equal("Router RT-W (Router): G0/0: 198.51.100.18/30, G0/1.10: 172.20.10.1/24, G0/1.20: 172.20.20.1/24, "
            + "G0/1.30: 172.20.30.1/26; verbunden mit Internet über Glasfaser, 500 Mbit/s; Switch SW-W1 über Port 24, Trunk mit VLAN 10, 20 und 30.",
            text[1]);
        // jede Leitung steht bei beiden Geraeten, jeder Leitungstext steht im Text
        foreach (var kante in plan.Kanten)
        {
            Assert.Equal(2, text.Count(z => z.Contains(kante.Text, StringComparison.Ordinal)));
        }
        Assert.All(plan.Knoten, k => Assert.Contains(text, z => z.StartsWith(k.Name + " (" + EchtZeichnung.Geraete[k.Typ] + ")", StringComparison.Ordinal)));
        // beschriftete Leitungen in Reihenfolge (Nummern an den Linien = Legende)
        Assert.Equal(6, plan.Beschriftet.Count);
    }

    [Fact]
    public void W2_1_Monitor_TextfassungNenntAlleWerteUndDenHoechstwert()
    {
        var monitor = B1.Anlagen.Single(a => a.Art == "monitor").Monitor!;
        var text = EchtZeichnung.Textfassung(monitor);
        Assert.Equal(4, text.Count);
        Assert.StartsWith("Datenträger D: aktive Zeit: 07:00 15 %, 07:30 40 %, 08:00 78 %, 08:30 96 %, 09:00 99 %", text[2], StringComparison.Ordinal);
        Assert.EndsWith("Höchster Wert 99 % um 09:00.", text[2], StringComparison.Ordinal);
        Assert.EndsWith("Höchster Wert 280 Mbit/s um 09:00.", text[3], StringComparison.Ordinal);
        Assert.All(text.Zip(monitor.Reihen), p => Assert.Equal(monitor.Marken.Count, p.First.Split(", ").Length));
    }

    [Fact]
    public void W2_1_Diagramme_TextfassungMitVerbundenMit()
    {
        var muster = B1.Aufgaben[2].Teile[0].Antwort.Muster!;
        var er = EchtZeichnung.Textfassung(muster);
        // Attribute stehen bei ihrer Entitaet oder Beziehung, keine eigene Zeile
        Assert.Equal(5, er.Count);
        Assert.Contains("Entität „Pflegekraft“; Attribute: Personalnummer (Schlüssel), Name, Qualifikation.", er);
        Assert.Contains("Beziehung „fährt“; zwischen Pflegekraft (1) und Tour (n).", er);
        Assert.Contains("Beziehung „besucht“; Attribute: geplante Uhrzeit; zwischen Tour (n) und Patient (m).", er);
        var uml = EchtZeichnung.Textfassung(EchtZeichnung.Diagramm(JsonNode.Parse(Uml), EchtZeichnung.Uml, true, "Test"));
        Assert.Equal("Start; weiter zu Aktion „Meldung aufnehmen“.", uml[0]);
        Assert.Contains("Verzweigung; weiter zu [dringend] Aktion „Bereitschaft anrufen“; [sonst] Aktion „Ticket anlegen“.", uml);
        Assert.Equal("Ende.", uml[^1]);
    }

    // -- Bewertung der Zeichenaufgabe (Stufe 1: Papier, Musterbild, Haekchen) --------------------------

    [Fact]
    public void Zeichnen_NurHaekchenZaehlen_PapierZaehltAlsBearbeitet()
    {
        var satz = B1;
        var teil = satz.Aufgaben[2].Teile[0];
        var schluessel = EchtBewertung.Feld(3, teil.Nr, "papier");
        var zustand = new JsonObject { ["antworten"] = new JsonObject { [schluessel] = "Ja" }, ["haken"] = new JsonObject() };
        Assert.True(EchtBewertung.Bearbeitet(zustand, 3, teil.Nr));
        Assert.Equal(0, EchtBewertung.Teil(zustand, 3, teil).Erreicht);
        Assert.Equal(teil.Hinweise.Count, EchtBewertung.HakenAnzahl(teil));
        zustand["haken"]![EchtBewertung.Schluessel(3, teil.Nr)] = new JsonArray(true, false, true, false, false, false);
        var wertung = EchtBewertung.Teil(zustand, 3, teil);
        Assert.Equal(teil.Hinweise[0].Punkte + teil.Hinweise[2].Punkte, wertung.Erreicht);
        Assert.Equal(0, wertung.Programm);
        // Gegenprobe: ohne Haken "papier" nicht bearbeitet
        Assert.False(EchtBewertung.Bearbeitet(new JsonObject { ["antworten"] = new JsonObject() }, 3, teil.Nr));
    }

    [Fact]
    public void ProbesatzKonzeption_VollIst100_LeerIst0()
    {
        var satz = B1;
        var antworten = new JsonObject();
        var haken = new JsonObject();
        for (var n = 0; n < satz.Aufgaben.Count; n++)
        {
            foreach (var t in satz.Aufgaben[n].Teile)
            {
                var a = t.Antwort;
                string F(string feld) => EchtBewertung.Feld(n + 1, t.Nr, feld);
                switch (a.Form)
                {
                    case EchtSaetze.FormRechnen:
                        antworten[F(EchtBewertung.Ergebnis)] = a.Ergebnis.ToString(System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
                        break;
                    case EchtSaetze.FormTabelle:
                        for (var z = 0; z < a.Tabelle.Count; z++)
                        {
                            for (var sp = 0; sp < a.Tabelle[z].Count; sp++)
                            {
                                if (a.Tabelle[z][sp].Offen)
                                {
                                    antworten[F(EchtBewertung.Zelle(z, sp))] = a.Tabelle[z][sp].Loesung[^1];
                                }
                            }
                        }
                        break;
                    case EchtSaetze.FormLuecke:
                        for (var f = 0; f < a.Felder.Count; f++)
                        {
                            antworten[F(EchtBewertung.Luecke(f))] = a.Felder[f].Loesung[0];
                        }
                        break;
                    case EchtSaetze.FormAuswahl:
                        antworten[F(EchtBewertung.Wahl)] = string.Join(",", a.Richtig);
                        break;
                }
                haken[EchtBewertung.Schluessel(n + 1, t.Nr)] = new JsonArray([.. Enumerable.Range(0, EchtBewertung.HakenAnzahl(t)).Select(_ => (JsonNode?)true)]);
            }
        }
        var voll = EchtBewertung.Auswerten(satz, new JsonObject { ["antworten"] = antworten, ["haken"] = haken, ["dauer"] = 60 });
        Assert.Equal(100, voll.Punkte);
        Assert.Equal(0, EchtBewertung.Auswerten(satz, new JsonObject()).Punkte);
        // Ersatzwert-Weg in 1e: 7 Jahre gilt auch, 6 nicht
        var e = satz.Aufgaben[0].Teile.Single(t => t.Nr == "e").Antwort;
        Assert.True(EchtBewertung.ErgebnisRichtig(e, "7"));
        Assert.True(EchtBewertung.ErgebnisRichtig(e, "5"));
        Assert.False(EchtBewertung.ErgebnisRichtig(e, "6"));
        // Schreibweisen in 3c: beide Reihenfolgen der zusammengesetzten Schluessel
        var c = satz.Aufgaben[2].Teile.Single(t => t.Nr == "c").Antwort;
        Assert.True(EchtBewertung.Passt("text", "patientennummer,  Tournummer", c.Tabelle[3][1].Loesung));
        Assert.True(EchtBewertung.Passt("text", "Tournummer und Patientennummer", c.Tabelle[3][1].Loesung));
    }
}
