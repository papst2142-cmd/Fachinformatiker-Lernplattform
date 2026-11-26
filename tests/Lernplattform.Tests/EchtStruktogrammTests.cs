using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// A 1.12.0 "Pruefung echt W2" (Weisung P12-390, Kurzplan P12-250, Antworten alle a, Auflagen A bis H): Struktogramm im
/// Baukasten. Abnahmesaetze: (1) sieben Bausteine (Anweisung, Verzweigung, Fallauswahl mit mehreren Faellen und "sonst",
/// Zaehlschleife, solange, wiederhole, Aufruf); (2) jede Aenderung nur ueber die Bausteine und die Knoepfe (Einfuegen an der
/// Stelle von "Neuer Block kommt", Nach oben/unten, Heraus, Entfernen, Fall hinzufuegen/entfernen), Text genau wie eingegeben;
/// (3) das Musterbild wird streng gelesen, jede Verletzung ist eine klare Meldung; die eigene Zeichnung stuerzt nie ab;
/// (4) nach dem Abgeben zaehlt das Programm Bausteine, Schachtelung, Faelle, leere Zweige und Tiefe gegen das Musterbild,
/// Texte nie; (5) Format 5, eine neue AE-Fassung (ae_b2_08; ae_b2_02 heraus nach Weisung P12-393, Nico 1b) nur angehaengt, Format 4 bleibt als echte Testdatei ladbar. Jede Regel mit
/// Gegenprobe.
/// </summary>
public class EchtStruktogrammTests
{
    private static readonly string Format4 = Path.Combine(Pfade.Wurzel, "tests", "Lerninhalte", "echt", "pruefungssaetze_format4.json");

    /// <summary>Die neue Fassung: Satz, Aufgabe, Teil (Weisung P12-390; ae_b2_02 seit P12-393 heraus, Nico 1b).</summary>
    public static readonly (string Kennung, int Aufgabe, string Nr)[] Neu = [("ae_b2_08", 2, "b")];

    private static IReadOnlyList<EchtBlock> Lesen(string json, bool vollstaendig = true) =>
        EchtStruktogramm.Lesen(JsonNode.Parse(json), "Test", vollstaendig);

    private static string Fehler(string json, bool vollstaendig = true) =>
        Assert.Throws<EchtLadefehler>(() => Lesen(json, vollstaendig)).Message;

    private static string Bloecke(string inhalt) => "{\"art\": \"struktogramm\", \"bloecke\": [" + inhalt + "]}";

    private const string Anw = "{\"typ\": \"anweisung\", \"text\": \"x ← 1\"}";

    private static JsonObject Roh(string kennung, int fassung) => JsonNode.Parse(File.ReadAllBytes(EchtTestDatei.Datei))!["saetze"]!.AsArray()
        .Select(s => s!.AsObject()).Single(s => (string?)s["kennung"] == kennung && ((int?)s["fassung"] ?? 1) == fassung).DeepClone().AsObject();

    private static byte[] Datei(int format, params JsonObject[] saetze) =>
        Encoding.UTF8.GetBytes(new JsonObject { ["format"] = format, ["saetze"] = new JsonArray([.. saetze.Select(s => s.DeepClone())]) }.ToJsonString());

    // ---- (5) Format 5 und die neue Fassung ------------------------------------------------------------------------

    [Fact]
    public void Format4_EchteTestdateiLaedt_StruktogrammErstAbFormat5_NeueresFormatKlar()
    {
        var alt = File.ReadAllBytes(Format4);
        Assert.Equal(4, (int)JsonNode.Parse(alt)!["format"]!);
        var bestand = EchtSaetze.Laden(alt, EchtTestDatei.Ausgeschlossen());
        Assert.Empty(bestand.Fehler);
        Assert.Equal(EchtW1BaukastenTests.Bis(62).Saetze.Select(s => (s.Kennung, s.Fassung)), bestand.Saetze.Select(s => (s.Kennung, s.Fassung)));
        Assert.DoesNotContain(bestand.Saetze.SelectMany(s => s.Aufgaben).SelectMany(a => a.Teile), t => t.Antwort.Struktogramm is not null);
        // ein Struktogramm in einer Datei mit Format 4: klare Meldung, die alte Fassung bleibt geladen (nie still)
        var wurzel = JsonNode.Parse(alt)!.AsObject();
        wurzel["saetze"]!.AsArray().Add(Roh("ae_b2_08", 2));
        var mit = EchtSaetze.Laden(Encoding.UTF8.GetBytes(wurzel.ToJsonString()));
        Assert.Contains(mit.Fehler, f => f.Kennung == "ae_b2_08" && f.Meldung.EndsWith("Diagramm \"struktogramm\" erst ab Format 5", StringComparison.Ordinal));
        Assert.Equal(1, mit.Satz("ae_b2_08")!.Fassung);
        // Gegenprobe: dieselbe Datei als Format 5 laedt die neue Fassung an der Stelle der alten
        wurzel["format"] = 5;
        var neu = EchtSaetze.Laden(Encoding.UTF8.GetBytes(wurzel.ToJsonString()));
        Assert.Empty(neu.Fehler);
        Assert.Equal(2, neu.Satz("ae_b2_08")!.Fassung);
        Assert.Equal(bestand.Saetze.Select(s => s.Kennung), neu.Saetze.Select(s => s.Kennung));
        // neuere Formatnummer: klarer Fehler der ganzen Datei
        var sechs = EchtSaetze.Laden(Datei(6, Roh("alle_ap1_01", 1)));
        Assert.Empty(sechs.Saetze);
        Assert.Equal("Format 6 ist neuer als dieses Programm (kennt Format 5); bitte das Programm aktualisieren", Assert.Single(sechs.Fehler).Meldung);
        Assert.Equal(5, EchtSaetze.Format);
    }

    [Fact]
    public void Struktogramm_MitVorgabe_IstFehler_OhneMusterIstFehler()
    {
        var satz = Roh("ae_b2_08", 2);
        var teil = satz["aufgaben"]![1]!["teile"]![1]!.AsObject();
        teil["antwort"]!["vorgabe"] = JsonNode.Parse(Bloecke(Anw));
        Assert.Contains(EchtSaetze.Laden(Datei(5, satz, Roh("ae_b1_08", 1))).Fehler,
            f => f.Kennung == "ae_b2_08" && f.Meldung.EndsWith("ein Struktogramm hat keine Vorgabe", StringComparison.Ordinal));
        satz = Roh("ae_b2_08", 2);
        satz["aufgaben"]![1]!["teile"]![1]!["antwort"]!.AsObject().Remove("muster");
        Assert.Contains(EchtSaetze.Laden(Datei(5, satz, Roh("ae_b1_08", 1))).Fehler,
            f => f.Kennung == "ae_b2_08" && f.Meldung.EndsWith("Musterbild fehlt", StringComparison.Ordinal));
        // Gegenprobe: der echte Satz laedt ohne Fehler
        Assert.Empty(EchtSaetze.Laden(Datei(5, Roh("ae_b2_08", 2), Roh("ae_b1_08", 1))).Fehler);
    }

    [Fact]
    public void EineFassung_NurAngehaengt_AlteBytegleich_GenauDieserTeil()
    {
        var echt = File.ReadAllBytes(EchtTestDatei.Datei);
        Assert.Equal(5, (int)JsonNode.Parse(echt)!["format"]!);
        var vorher = File.ReadAllBytes(Format4);
        var laenge = vorher.Length - "\n ]\n}".Length;
        var roh = EchtTestDatei.AlsFormat(echt, 4);
        Assert.True(roh.AsSpan(0, laenge).SequenceEqual(vorher.AsSpan(0, laenge)), "Datei bis zu den neuen Fassungen nicht bytegleich mit Format 4");
        Assert.StartsWith(",\n  {\n   \"kennung\": \"ae_b2_08\",\n   \"fassung\": 2,", Encoding.UTF8.GetString(roh, laenge, 80), StringComparison.Ordinal);
        // Gegenprobe: ein geaendertes Zeichen vorn faellt auf
        roh[300] ^= 1;
        Assert.False(roh.AsSpan(0, laenge).SequenceEqual(vorher.AsSpan(0, laenge)));
        var liste = JsonNode.Parse(echt)!["saetze"]!.AsArray();
        Assert.Equal(63, liste.Count); // P12-393: nur ae_b2_08 (ae_b2_02 heraus)
        Assert.Equal(Neu.Select(n => n.Kennung), liste.Skip(62).Select(s => (string?)s!["kennung"]));
        Assert.All(liste.Skip(62), s => Assert.Equal(2, (int)s!["fassung"]!));

        var app = EchtTestDatei.App();
        var alt = EchtW1BaukastenTests.Bis(62);
        Assert.Empty(app.Fehler);
        Assert.Equal(alt.Saetze.Select(s => s.Kennung), app.Saetze.Select(s => s.Kennung));
        foreach (var s in app.Saetze)
        {
            var a = alt.Satz(s.Kennung)!;
            var neu = Neu.Where(n => n.Kennung == s.Kennung).ToList();
            Assert.Equal(neu.Count > 0 ? 2 : a.Fassung, s.Fassung);
            Assert.Equal(a.Aufgaben.Select(x => x.Punkte), s.Aufgaben.Select(x => x.Punkte));
            Assert.Equal(a.Aufgaben.Select(x => x.Titel), s.Aufgaben.Select(x => x.Titel));
            for (var i = 0; i < s.Aufgaben.Count; i++)
            {
                for (var j = 0; j < s.Aufgaben[i].Teile.Count; j++)
                {
                    var (tn, ta) = (s.Aufgaben[i].Teile[j], a.Aufgaben[i].Teile[j]);
                    Assert.Equal((ta.Nr, ta.Punkte, ta.Operator, ta.Gebiet), (tn.Nr, tn.Punkte, tn.Operator, tn.Gebiet));
                    if (neu.Any(n => n.Aufgabe == i + 1 && n.Nr == tn.Nr))
                    {
                        // genau dieser Teil: jetzt im Baukasten, vorher auf Papier, Loesung gleich
                        Assert.Equal(EchtSaetze.FormZeichnen, tn.Antwort.Form);
                        Assert.NotEqual(EchtSaetze.FormZeichnen, ta.Antwort.Form);
                        Assert.NotNull(tn.Antwort.Struktogramm);
                        Assert.Equal(ta.Loesung, tn.Loesung);
                        Assert.Contains("im Baukasten", string.Join(" ", tn.Text), StringComparison.Ordinal);
                    }
                    else
                    {
                        Assert.Equal(string.Join("\n", ta.Text), string.Join("\n", tn.Text));
                        Assert.Equal(ta.Antwort.Form, tn.Antwort.Form);
                        Assert.Null(tn.Antwort.Struktogramm);
                    }
                }
            }
        }
        // die Musterbilder: alle Bausteine, die die Aufgaben brauchen, und mindestens zwei Ebenen
        foreach (var (kennung, aufgabe, nr) in Neu)
        {
            var muster = app.Satz(kennung)!.Aufgaben[aufgabe - 1].Teile.Single(t => t.Nr == nr).Antwort.Struktogramm!;
            Assert.InRange(EchtStruktogramm.Tiefe(muster), 3, EchtStruktogramm.TiefeMax);
            Assert.Contains(muster, b => b.Typ == EchtStruktogramm.Zaehlschleife);
        }
    }

    // ---- (3) strenges Lesen ------------------------------------------------------------------------------------------------

    [Fact]
    public void Lesen_AlleSiebenBausteine_GutUndRunderUmweg()
    {
        var json = Bloecke(Anw + ","
            + "{\"typ\": \"verzweigung\", \"text\": \"x > 0\", \"ja\": [" + Anw + "], \"nein\": []},"
            + "{\"typ\": \"fallauswahl\", \"text\": \"note\", \"faelle\": [{\"text\": \"1\", \"bloecke\": [" + Anw + "]}, {\"text\": \"2\", \"bloecke\": []}, {\"text\": \"3\", \"bloecke\": []}], \"sonst\": [" + Anw + "]},"
            + "{\"typ\": \"zaehlschleife\", \"text\": \"für i von 1 bis 3\", \"rumpf\": [" + Anw + "]},"
            + "{\"typ\": \"solange\", \"text\": \"solange x < 9\", \"rumpf\": [" + Anw + "]},"
            + "{\"typ\": \"wiederhole\", \"text\": \"bis x > 9\", \"rumpf\": [{\"typ\": \"aufruf\", \"text\": \"druck(x)\"}]},"
            + "{\"typ\": \"aufruf\", \"text\": \"ende()\"}");
        var b = Lesen(json);
        Assert.Equal(EchtStruktogramm.Bausteine, b.Select(x => x.Typ));
        Assert.Equal(["fall", "fall", "fall", "sonst"], b[2].Zweige.Select(z => z.Art));
        Assert.Equal(["1", "2", "3", ""], b[2].Zweige.Select(z => z.Text));
        Assert.Equal(["ja", "nein"], b[1].Zweige.Select(z => z.Art));
        // runder Umweg: als JSON und wieder gelesen ist alles gleich
        var wieder = EchtStruktogramm.Laden(EchtStruktogramm.AlsJson(b).ToJsonString());
        Assert.False(wieder.Unlesbar);
        Assert.Equal(EchtStruktogramm.AlsJson(b).ToJsonString(), EchtStruktogramm.AlsJson(wieder.Bloecke).ToJsonString());
        Assert.Equal(EchtStruktogramm.AlsJson(b).ToJsonString(), System.Text.Json.Nodes.JsonNode.Parse(wieder.Json())!.ToJsonString());
    }

    [Fact]
    public void Lesen_Musterbild_JederVerstossKlar_EigeneZeichnungNachsichtig()
    {
        Assert.Equal("Test fehlt", Assert.Throws<EchtLadefehler>(() => EchtStruktogramm.Lesen(null, "Test", true)).Message);
        Assert.Equal("Test: Art muss \"struktogramm\" sein", Fehler("{\"art\": \"uml\", \"bloecke\": []}"));
        Assert.Equal("Test: das Struktogramm hat keinen Block", Fehler(Bloecke("")));
        Assert.Equal("Test: Liste der Blöcke fehlt", Fehler("{\"art\": \"struktogramm\"}"));
        Assert.Equal("Test: Baustein \"schleife\" gibt es im Struktogramm nicht", Fehler(Bloecke("{\"typ\": \"schleife\", \"text\": \"a\"}")));
        Assert.Equal("Test: Anweisung ohne Text", Fehler(Bloecke("{\"typ\": \"anweisung\", \"text\": \" \"}")));
        Assert.Equal("Test: Text von Anweisung länger als 120 Zeichen oder mit Zeilenumbruch", Fehler(Bloecke("{\"typ\": \"anweisung\", \"text\": \"" + new string('a', 121) + "\"}")));
        Assert.Equal("Test: Text von Anweisung länger als 120 Zeichen oder mit Zeilenumbruch", Fehler(Bloecke("{\"typ\": \"anweisung\", \"text\": \"a\\nb\"}")));
        Assert.Equal("Test: eine Fallauswahl hat 2 bis 6 Fälle", Fehler(Bloecke("{\"typ\": \"fallauswahl\", \"text\": \"a\", \"faelle\": [{\"text\": \"1\", \"bloecke\": []}], \"sonst\": []}")));
        var sieben = string.Join(",", Enumerable.Range(1, 7).Select(i => "{\"text\": \"" + i + "\", \"bloecke\": []}"));
        Assert.Equal("Test: eine Fallauswahl hat 2 bis 6 Fälle", Fehler(Bloecke("{\"typ\": \"fallauswahl\", \"text\": \"a\", \"faelle\": [" + sieben + "], \"sonst\": []}")));
        Assert.Equal("Test: Fall ohne Text", Fehler(Bloecke("{\"typ\": \"fallauswahl\", \"text\": \"a\", \"faelle\": [{\"text\": \"\", \"bloecke\": []}, {\"text\": \"2\", \"bloecke\": []}], \"sonst\": []}")));
        Assert.Equal("Test: Zählschleife „i“ mit leerem Rumpf", Fehler(Bloecke("{\"typ\": \"zaehlschleife\", \"text\": \"i\", \"rumpf\": []}")));
        Assert.Equal("Test: Liste der Blöcke fehlt", Fehler(Bloecke("{\"typ\": \"verzweigung\", \"text\": \"a\", \"ja\": []}")));
        // Tiefe: 8 Ebenen gut, 9 klarer Fehler
        string Tief(int ebenen) => ebenen == 1 ? Anw : "{\"typ\": \"solange\", \"text\": \"s\", \"rumpf\": [" + Tief(ebenen - 1) + "]}";
        Assert.Equal(8, EchtStruktogramm.Tiefe(Lesen(Bloecke(Tief(8)))));
        Assert.Equal("Test: tiefer als 8 Ebenen geschachtelt", Fehler(Bloecke(Tief(9))));
        // Zahl: 60 Bloecke gut, 61 klarer Fehler
        Assert.Equal(60, Lesen(Bloecke(string.Join(",", Enumerable.Repeat(Anw, 60)))).Count);
        Assert.Equal("Test: mehr als 60 Blöcke", Fehler(Bloecke(string.Join(",", Enumerable.Repeat(Anw, 61)))));
        // eigene Zeichnung (nicht vollstaendig): leere Texte, leerer Rumpf und kein Block sind erlaubt
        Assert.Empty(Lesen(Bloecke(""), vollstaendig: false));
        Assert.Single(Lesen(Bloecke("{\"typ\": \"zaehlschleife\", \"text\": \"\", \"rumpf\": []}"), vollstaendig: false));
        // ... aber ein unbekannter Baustein nie
        Assert.Equal("Test: Baustein \"x\" gibt es im Struktogramm nicht", Fehler(Bloecke("{\"typ\": \"x\", \"text\": \"\"}"), vollstaendig: false));
    }

    [Fact]
    public void Laden_KaputterText_StuerztNie_BeginntLeerUndMeldetUnlesbar()
    {
        foreach (var kaputt in new[] { "{", "[]", "{\"art\": \"uml\", \"bloecke\": []}", Bloecke("{\"typ\": \"x\"}"), "\"text\"" })
        {
            var s = EchtStruktogramm.Laden(kaputt);
            Assert.True(s.Unlesbar, kaputt);
            Assert.Empty(s.Bloecke);
            Assert.Equal("", s.Json());
        }
        // Gegenprobe: leer und gute Zeichnung sind lesbar
        Assert.False(EchtStruktogramm.Laden("").Unlesbar);
        Assert.False(EchtStruktogramm.Laden(null).Unlesbar);
        var gut = EchtStruktogramm.Laden(Bloecke(Anw));
        Assert.False(gut.Unlesbar);
        Assert.Equal("x ← 1", Assert.Single(gut.Bloecke).Text);
    }

    // ---- (2) Aendern nur ueber Bausteine und Knoepfe ---------------------------------------------------------------------------

    [Fact]
    public void Einfuegen_ZieleWieNeuerBlockKommt_JederBausteinMitSeinenZweigen()
    {
        var s = EchtStruktogramm.Neu();
        Assert.Equal([EchtTexte.KommtEnde], s.Ziele(null));
        Assert.Equal("0", s.Einfuegen(EchtStruktogramm.Anweisung, null));
        Assert.Equal([EchtTexte.KommtEnde, EchtTexte.KommtAnfang], s.Ziele(null));
        Assert.Equal("1", s.Einfuegen(EchtStruktogramm.Verzweigung, "0"));            // nach dem gewaehlten Block
        Assert.Equal("0", s.Einfuegen(EchtStruktogramm.Aufruf, "0", 1));              // vor dem gewaehlten Block
        Assert.Equal([EchtStruktogramm.Aufruf, EchtStruktogramm.Anweisung, EchtStruktogramm.Verzweigung], s.Bloecke.Select(b => b.Typ));
        Assert.Equal([EchtTexte.KommtNach, EchtTexte.KommtVor, "in Ja-Zweig (ans Ende)", "in Nein-Zweig (ans Ende)"], s.Ziele("2"));
        Assert.Equal("2.1.0", s.Einfuegen(EchtStruktogramm.Anweisung, "2", 3));       // in den Nein-Zweig
        Assert.Equal("2.1.0", s.Einfuegen(EchtStruktogramm.Fallauswahl, "2.1", 1));   // an den Anfang des gewaehlten Zweigs
        Assert.Equal("2.1.2", s.Einfuegen(EchtStruktogramm.Zaehlschleife, "2.1", 0)); // ans Ende des gewaehlten Zweigs
        Assert.Equal([EchtTexte.KommtZweigEnde, EchtTexte.KommtZweigAnfang], s.Ziele("2.0"));
        Assert.Equal(["fall", "fall", "sonst"], s.Block("2.1.0")!.Zweige.Select(z => z.Art));
        Assert.Equal(["rumpf"], s.Block("2.1.2")!.Zweige.Select(z => z.Art));
        Assert.Equal("2.1.2.0.0", s.Einfuegen(EchtStruktogramm.Solange, "2.1.2", 2));
        Assert.Equal("2.1.2.0.1", s.Einfuegen(EchtStruktogramm.Wiederhole, "2.1.2.0.0", 0));
        Assert.Equal(8, s.Zeilen().Count(z => s.IstBlock(z.Pfad)));
        // Gegenproben: falsches Ziel, falsche Stelle, unbekannter Baustein
        Assert.Null(s.Einfuegen(EchtStruktogramm.Anweisung, "0", 2));     // ein Aufruf hat keinen Zweig
        Assert.Null(s.Einfuegen(EchtStruktogramm.Anweisung, "2.0", 2));   // ein Zweig hat nur Ende und Anfang
        Assert.Null(s.Einfuegen(EchtStruktogramm.Anweisung, "7"));
        Assert.Null(s.Einfuegen("schleife", null));
        Assert.Equal(8, s.Zeilen().Count(z => s.IstBlock(z.Pfad)));
    }

    [Fact]
    public void Grenzen_HoechstensSechzigBloeckeUndAchtEbenen_NieStill()
    {
        var s = EchtStruktogramm.Neu();
        string? p = null;
        for (var i = 0; i < 7; i++)
        {
            p = s.Einfuegen(EchtStruktogramm.Solange, p, p is null ? 0 : 2);
            Assert.NotNull(p);
        }
        // Ebene 8 traegt nur noch einfache Bloecke
        Assert.Null(s.Einfuegen(EchtStruktogramm.Solange, p, 2));
        Assert.NotNull(s.Einfuegen(EchtStruktogramm.Anweisung, p, 2));
        Assert.Equal(8, EchtStruktogramm.Tiefe(s.Bloecke));
        while (s.Einfuegen(EchtStruktogramm.Anweisung, null) is not null)
        {
        }
        Assert.Equal(EchtStruktogramm.BloeckeMax, s.Zeilen().Count(z => s.IstBlock(z.Pfad)));
        Assert.Null(s.Einfuegen(EchtStruktogramm.Aufruf, "0", 1));
        // gespeichert und wieder geladen: dieselbe Zeichnung (die eigene Zeichnung haelt dieselben Grenzen wie das Lesen)
        Assert.Equal(s.Json(), EchtStruktogramm.Laden(s.Json()).Json());
    }

    [Fact]
    public void Beschriften_TextGenauWieEingegeben_OhneZeilenumbruch()
    {
        var s = EchtStruktogramm.Neu();
        var v = s.Einfuegen(EchtStruktogramm.Verzweigung, null)!;
        Assert.True(s.Beschriften(v, "differenz > 0"));
        Assert.Equal("differenz > 0", s.Block(v)!.Text);  // Auflage C: kein automatisches "?"
        Assert.True(s.Beschriften(v, "  a  "));
        Assert.Equal("  a  ", s.Block(v)!.Text);
        Assert.False(s.Beschriften(v, "a\nb"));
        Assert.False(s.Beschriften(v, "a\rb"));
        Assert.False(s.Beschriften(v, new string('x', EchtStruktogramm.TextLaenge + 1)));
        Assert.True(s.Beschriften(v, new string('x', EchtStruktogramm.TextLaenge)));
        Assert.False(s.Beschriften(v + ".0", "Zweig"));   // Ja-/Nein-Zweig haben keinen eigenen Text
        var f = s.Einfuegen(EchtStruktogramm.Fallauswahl, v)!;
        Assert.True(s.Beschriften(f + ".1", "B"));         // Wert eines Falls
        Assert.Equal("B", s.Zweig(f + ".1")!.Text);
        Assert.False(s.Beschriften(f + ".2", "sonst"));    // "sonst" hat keinen Wert
        Assert.False(s.Beschriften("9", "x"));
        Assert.False(s.Beschriften(null, "x"));
    }

    [Fact]
    public void Schieben_Heraus_Entfernen_NurInnerhalbDerRegeln()
    {
        var s = EchtStruktogramm.Neu();
        var z = s.Einfuegen(EchtStruktogramm.Zaehlschleife, null)!;
        var a = s.Einfuegen(EchtStruktogramm.Anweisung, z, 2)!;
        var b = s.Einfuegen(EchtStruktogramm.Aufruf, a)!;
        s.Beschriften(a, "a");
        s.Beschriften(b, "b");
        Assert.Equal(("0.0.0", "0.0.1"), (a, b));
        Assert.False(s.Schiebbar(a, -1));
        Assert.True(s.Schiebbar(a, 1));
        Assert.Equal("0.0.0", s.Schieben(b, -1));
        Assert.Equal(["b", "a"], s.Block(z)!.Zweige[0].Bloecke.Select(x => x.Text));
        Assert.Null(s.Schieben("0.0.0", -1));
        // Heraus: direkt hinter den Block, in dessen Zweig er stand
        Assert.False(s.Herausbar(z));
        Assert.True(s.Herausbar("0.0.1"));
        Assert.Equal("1", s.Heraus("0.0.1"));
        Assert.Equal(["zaehlschleife", "anweisung"], s.Bloecke.Select(x => x.Typ));
        Assert.Equal("a", s.Block("1")!.Text);
        Assert.Null(s.Heraus("1"));
        // Entfernen: Inhalt zaehlt alles, was darin steht (fuer die Rueckfrage)
        Assert.Equal(1, s.Inhalt(z));
        Assert.Equal(0, s.Inhalt("1"));
        Assert.True(s.Entfernen(z));
        Assert.Equal(["a"], s.Bloecke.Select(x => x.Text));
        Assert.False(s.Entfernen("5"));
        Assert.False(s.Entfernen(""));
        Assert.True(s.Entfernen("0"));
        Assert.False(s.Eigenes);
        Assert.Equal("", s.Json());
    }

    [Fact]
    public void Faelle_ZweiBisSechs_SonstImmerAmEnde()
    {
        var s = EchtStruktogramm.Neu();
        var f = s.Einfuegen(EchtStruktogramm.Fallauswahl, null)!;
        Assert.Equal(2, s.Block(f)!.Zweige.Count(z => z.Art == EchtStruktogramm.Fall));
        Assert.False(s.FallEntfernbar(f + ".0"));
        for (var n = 3; n <= EchtStruktogramm.FaelleMax; n++)
        {
            Assert.Equal(f + "." + (n - 1), s.FallNeu(f));
            Assert.Equal(EchtStruktogramm.Sonst, s.Block(f)!.Zweige[^1].Art);
        }
        Assert.False(s.FallMoeglich(f));
        Assert.Null(s.FallNeu(f));
        Assert.Equal(EchtStruktogramm.FaelleMax, s.Block(f)!.Zweige.Count(z => z.Art == EchtStruktogramm.Fall));
        Assert.False(s.FallEntfernbar(f + "." + EchtStruktogramm.FaelleMax)); // "sonst"
        s.Beschriften(f + ".0", "eins");
        Assert.Equal(f, s.FallEntfernen(f + ".0"));
        Assert.DoesNotContain(s.Block(f)!.Zweige, z => z.Text == "eins");
        while (s.FallEntfernbar(f + ".0"))
        {
            s.FallEntfernen(f + ".0");
        }
        Assert.Equal(EchtStruktogramm.FaelleMin, s.Block(f)!.Zweige.Count(z => z.Art == EchtStruktogramm.Fall));
        Assert.Equal(EchtStruktogramm.Sonst, s.Block(f)!.Zweige[^1].Art);
        // Gegenprobe: an einer Anweisung gibt es keine Faelle
        var a = s.Einfuegen(EchtStruktogramm.Anweisung, f)!;
        Assert.False(s.FallMoeglich(a));
        Assert.Null(s.FallNeu(a));
    }

    [Fact]
    public void Liste_Und_Textfassung_ZeigenJedeStelle_LeereZweigeMitLeerzeichen()
    {
        var s = EchtStruktogramm.Neu();
        var v = s.Einfuegen(EchtStruktogramm.Verzweigung, null)!;
        s.Beschriften(v, "x > 0");
        var a = s.Einfuegen(EchtStruktogramm.Anweisung, v, 2)!;
        s.Beschriften(a, "y ← x");
        var zeilen = s.Zeilen();
        Assert.Equal(["0", "0.0", "0.0.0", "0.1"], zeilen.Select(z => z.Pfad));
        Assert.Equal([0, 1, 2, 1], zeilen.Select(z => z.Ebene));
        Assert.All(zeilen, z => Assert.False(string.IsNullOrWhiteSpace(s.Beschreibung(z.Pfad))));
        Assert.Contains("leer", s.Beschreibung("0.1"), StringComparison.Ordinal);
        var text = EchtStruktogramm.Textfassung(s.Bloecke);
        Assert.Equal(EchtTexte.StruktogrammKopf, text[0]);
        Assert.Contains(text, z => z.Trim() == EchtTexte.LeerText);
        Assert.Contains(text, z => z.EndsWith("Anweisung: y ← x", StringComparison.Ordinal));
        Assert.Equal([EchtTexte.StruktogrammKopf, EchtTexte.LeerText], EchtStruktogramm.Textfassung([]));
        Assert.Equal("∅", EchtTexte.LeerZeichen);
    }

    // ---- (4) Zaehlung nach dem Abgeben ---------------------------------------------------------------------------------------

    [Fact]
    public void Zaehlen_GleicheStruktur_AllesGruen_TexteZaehlenNie_GegenProben()
    {
        var app = EchtTestDatei.App();
        foreach (var (kennung, aufgabe, nr) in Neu)
        {
            var muster = app.Satz(kennung)!.Aufgaben[aufgabe - 1].Teile.Single(t => t.Nr == nr).Antwort.Struktogramm!;
            Assert.All(EchtStruktogramm.Zaehlen(muster, muster), z => Assert.True(z.Ok, z.Text));
            // andere Texte, gleiche Struktur: weiter alles gruen (Texte werden nie verglichen)
            var eigen = EchtStruktogramm.Laden(EchtStruktogramm.AlsJson(muster).ToJsonString());
            foreach (var (pfad, _, _) in eigen.Zeilen().Where(z => eigen.IstBlock(z.Pfad)).ToList())
            {
                eigen.Beschriften(pfad, "anders");
            }
            Assert.All(EchtStruktogramm.Zaehlen(eigen.Bloecke, muster), z => Assert.True(z.Ok, z.Text));
            // Gegenprobe 1: eine Verzweigung eine Ebene nach aussen -> Schachtelung rot, Anzahl gruen
            var heraus = EchtStruktogramm.Laden(EchtStruktogramm.AlsJson(muster).ToJsonString());
            var verzweigung = heraus.Zeilen().First(z => heraus.Block(z.Pfad)?.Typ == EchtStruktogramm.Verzweigung && heraus.Herausbar(z.Pfad)).Pfad;
            heraus.Heraus(verzweigung);
            var zeilen = EchtStruktogramm.Zaehlen(heraus.Bloecke, muster);
            Assert.Contains(zeilen, z => !z.Ok);
            Assert.Contains(zeilen, z => z.Ok && z.Text.StartsWith("Verzweigungen", StringComparison.Ordinal));
            // Gegenprobe 2: leere Zeichnung -> jede Anzahl, jeder Ort und die Tiefe rot (leere Zweige zaehlen eigen)
            Assert.All(EchtStruktogramm.Zaehlen([], muster).Where(z => !z.Text.StartsWith("Leere Zweige", StringComparison.Ordinal)),
                z => Assert.False(z.Ok, z.Text));
            // Gegenprobe 3: ein Block ohne Text wird genannt
            var ohne = EchtStruktogramm.Laden(EchtStruktogramm.AlsJson(muster).ToJsonString());
            ohne.Beschriften("0", "");
            Assert.Contains(EchtStruktogramm.Zaehlen(ohne.Bloecke, muster), z => !z.Ok && z.Text.StartsWith("Ohne Text", StringComparison.Ordinal));
        }
    }
}
