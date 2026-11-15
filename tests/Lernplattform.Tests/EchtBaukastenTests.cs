using System.Text.Json.Nodes;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.78.0 "Zeichen-Baukasten" (Weisung P12-266): eigene Zeichnung auf denselben Daten wie <see cref="EchtZeichnung"/>.
/// Abnahmesaetze: B-1 setzen, beschriften, verschieben, verbinden, loeschen, Vorgabe fest; B-2 das Programm zaehlt nach
/// dem Abgeben (Anzahl, Regeln, Kardinalitaeten, Schluessel), die Punkte bleiben Haekchen; B-3 Liste "verbunden mit …"
/// fuer den Screenreader; B-5 die Zeichnung uebersteht Speichern und Laden und steht im Ergebnis. Jede Regel mit Gegenprobe.
/// </summary>
public class EchtBaukastenTests
{
    private static readonly string Datei = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen", EchtSaetze.Datei);

    private static EchtSatz B1 => EchtSaetze.Laden(File.ReadAllBytes(Datei)).Saetze.Single(s => s.Kennung == "si_b1_01");

    private static EchtTeil ErTeil => B1.Aufgaben[2].Teile.Single(t => t.Antwort.Form == EchtSaetze.FormZeichnen);

    private static EchtDiagramm Uml(string json) => EchtZeichnung.Diagramm(JsonNode.Parse(json), EchtZeichnung.Uml, vollstaendig: true, "Test");

    private const string UmlMuster = """
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

    /// <summary>Das UML-Muster im Baukasten nachbauen (ohne Vorgabe), so wie man es mit Knoepfen tut.</summary>
    private static EchtBaukasten UmlNachgebaut()
    {
        var b = EchtBaukasten.Neu(EchtZeichnung.Uml, null);
        var s = b.Setzen("start")!;
        var a1 = b.Setzen("aktion", s)!;
        b.Beschriften(a1, "Meldung aufnehmen");
        var v = b.Setzen("verzweigung", a1)!;
        var a2 = b.Setzen("aktion", v)!;
        b.Beschriften(a2, "Bereitschaft anrufen");
        var a3 = b.Setzen("aktion", v)!;
        b.Beschriften(a3, "Ticket anlegen");
        var z = b.Setzen("zusammenfuehrung", a2)!;
        var e = b.Setzen("ende", z)!;
        foreach (var (von, nach) in new[] { (s, a1), (a1, v), (v, a2), (v, a3), (a2, z), (a3, z), (z, e) })
        {
            Assert.Null(b.Verbinden(von, nach));
        }
        Assert.True(b.Bedingung(v, a2, "[dringend]"));
        Assert.True(b.Bedingung(v, a3, "[sonst]"));
        return b;
    }

    // -- B-1: setzen, beschriften, verschieben, verbinden, loeschen ---------------------------------

    [Fact]
    public void B1_Setzen_NaechsteFreieStelle_UnterDemGewaehlten_SonstZeileFuerZeile()
    {
        var b = EchtBaukasten.Neu(EchtZeichnung.Uml, null);
        var s = b.Setzen("start")!;
        Assert.Equal((0, 0), (b.Element(s)!.X, b.Element(s)!.Y));
        var a = b.Setzen("aktion", s)!;
        Assert.Equal((0, 1), (b.Element(a)!.X, b.Element(a)!.Y));
        var c = b.Setzen("aktion")!;
        Assert.Equal((1, 0), (b.Element(c)!.X, b.Element(c)!.Y));
        // Gegenprobe: Baustein der anderen Art gibt es nicht
        Assert.Null(b.Setzen("entitaet"));
        Assert.Equal(3, b.Diagramm.Elemente.Count);
    }

    [Fact]
    public void B1_RasterVoll_KeinWeitererBaustein()
    {
        var b = EchtBaukasten.Neu(EchtZeichnung.Er, null);
        for (var i = 0; i < EchtZeichnung.RasterBreite * EchtZeichnung.RasterHoehe; i++)
        {
            Assert.NotNull(b.Setzen("attribut"));
        }
        Assert.Null(b.Setzen("attribut"));
        Assert.Null(b.Frei(null));
    }

    [Fact]
    public void B1_Verschieben_NurAufFreiesFeldImRaster()
    {
        var b = EchtBaukasten.Neu(EchtZeichnung.Uml, null);
        var s = b.Setzen("start")!;
        var a = b.Setzen("aktion", s)!;
        Assert.True(b.Verschieben(a, 3, 4));
        Assert.Equal((3, 4), (b.Element(a)!.X, b.Element(a)!.Y));
        Assert.False(b.Verschieben(a, 0, 0));    // belegt
        Assert.False(b.Verschieben(a, EchtZeichnung.RasterBreite, 0));
        Assert.False(b.Verschieben(a, 0, EchtZeichnung.RasterHoehe));
        Assert.False(b.Verschieben(a, -1, 0));
        Assert.Equal((3, 4), (b.Element(a)!.X, b.Element(a)!.Y));
    }

    [Fact]
    public void B1_Beschriften_Einzeilig_HoechstensSechzigZeichen_NurBausteineMitText()
    {
        var b = EchtBaukasten.Neu(EchtZeichnung.Uml, null);
        var a = b.Setzen("aktion")!;
        Assert.True(b.Beschriften(a, "  Ticket\nanlegen  "));
        Assert.Equal("Ticket anlegen", b.Element(a)!.Text);
        Assert.True(b.Beschriften(a, new string('x', 80)));
        Assert.Equal(EchtBaukasten.TextLaenge, b.Element(a)!.Text.Length);
        var s = b.Setzen("start")!;
        Assert.False(b.Beschriften(s, "Start"));
        Assert.Equal("", b.Element(s)!.Text);
    }

    [Fact]
    public void B1_Vorgabe_BleibtFest_EigenesNicht()
    {
        var teil = ErTeil;
        var b = EchtBaukasten.Neu(EchtZeichnung.Er, teil.Antwort.Vorgabe);
        Assert.False(b.Eigenes);
        Assert.Equal("", b.Json());
        Assert.False(b.Verschieben("pk", 1, 5));
        Assert.False(b.Beschriften("pk", "Mitarbeiter"));
        Assert.False(b.Entfernen("pk"));
        Assert.False(b.Schluessel("pname", true));
        Assert.False(b.Trennen("faehrt", "pk"));
        Assert.Equal(teil.Antwort.Vorgabe!.Elemente, b.Diagramm.Elemente);
        // die Vorgabe hat an "fährt" keine Kardinalitaet: die setzt man selbst
        Assert.True(b.Kardinalitaet("faehrt", "pk", "1"));
        Assert.True(b.Eigenes);
        // eigener Baustein: alles erlaubt
        var p = b.Setzen("entitaet", "tour")!;
        Assert.True(b.Beschriften(p, "Patient"));
        Assert.True(b.Entfernen(p));
        Assert.Null(b.Element(p));
    }

    [Fact]
    public void B1_Entfernen_NimmtVerbindungenMit()
    {
        var b = UmlNachgebaut();
        var v = b.Diagramm.Elemente.Single(e => e.Typ == "verzweigung").Id;
        Assert.Equal(3, b.VerbindungenVon(v).Count);
        Assert.True(b.Entfernen(v));
        Assert.DoesNotContain(b.Diagramm.Verbindungen, x => x.Von == v || x.Nach == v);
        Assert.Equal(4, b.Diagramm.Verbindungen.Count);
    }

    [Fact]
    public void B1_Verbinden_UmlRegeln_MitGegenprobe()
    {
        var b = EchtBaukasten.Neu(EchtZeichnung.Uml, null);
        var s = b.Setzen("start")!;
        var a = b.Setzen("aktion", s)!;
        var e = b.Setzen("ende", a)!;
        Assert.Equal(EchtTexte.NichtStartEnde, b.Verbinden(a, s));
        Assert.Equal(EchtTexte.NichtStartEnde, b.Verbinden(e, a));
        Assert.Equal(EchtTexte.NichtMitSich, b.Verbinden(a, a));
        Assert.Null(b.Verbinden(s, a));
        Assert.Equal(EchtTexte.SchonVerbunden, b.Verbinden(s, a));
        Assert.Equal(EchtTexte.SchonVerbunden, b.Verbinden(a, s));
        Assert.Null(b.Verbinden(a, e));
        // Bedingung nur an Pfeilen aus einer Verzweigung
        Assert.False(b.Bedingung(s, a, "[ja]"));
        Assert.True(b.Trennen(a, e));
        Assert.False(b.Trennen(a, e));
        Assert.Single(b.Diagramm.Verbindungen);
    }

    [Fact]
    public void B1_Verbinden_ErRegeln_MitGegenprobe()
    {
        var b = EchtBaukasten.Neu(EchtZeichnung.Er, null);
        var k = b.Setzen("entitaet")!;
        var r = b.Setzen("entitaet")!;
        var h = b.Setzen("beziehung")!;
        var at = b.Setzen("attribut")!;
        Assert.Equal(EchtTexte.ErNichtErlaubt, b.Verbinden(k, r));    // Entitaet zu Entitaet
        Assert.Null(b.Verbinden(h, k));
        Assert.Null(b.Verbinden(r, h));
        Assert.Null(b.Verbinden(at, k));
        Assert.Equal(EchtTexte.AttributBelegt, b.Verbinden(at, h));    // ein Attribut haengt an genau einer Stelle
        var at2 = b.Setzen("attribut")!;
        Assert.Equal(EchtTexte.ErNichtErlaubt, b.Verbinden(at, at2));
        // Kardinalitaet nur an Beziehung–Entitaet, nur 1, n, m oder leer
        Assert.True(b.Kardinalitaet(k, h, "1"));    // Richtung egal
        Assert.False(b.Kardinalitaet(h, k, "x"));
        Assert.False(b.Kardinalitaet(at, k, "n"));
        Assert.True(b.Kardinalitaet(h, r, "n"));
        Assert.True(b.Schluessel(at, true));
        Assert.False(b.Schluessel(k, true));
        Assert.Equal("1", b.Diagramm.Verbindungen.Single(v => v.Von == h && v.Nach == k).Kard);
    }

    [Fact]
    public void B1_JedeErlaubteZeichnung_BestehtDieLeseRegelnDerVorgabe()
    {
        // was der Baukasten erlaubt, liest auch die Ladepruefung (Regeln fuer eine Vorgabe, nicht vollstaendig)
        var b = UmlNachgebaut();
        Assert.Empty(EchtZeichnung.Verstoesse(b.Diagramm, vollstaendig: false));
        Assert.Empty(EchtZeichnung.Verstoesse(b.Diagramm, vollstaendig: true));
        var json = JsonNode.Parse(b.Json());
        Assert.NotNull(EchtZeichnung.Diagramm(json, EchtZeichnung.Uml, vollstaendig: true, "Test"));
    }

    // -- B-5: Speichern und Laden ------------------------------------------------------------------

    [Fact]
    public void B5_JsonUndLaden_GleicheZeichnung()
    {
        var b = UmlNachgebaut();
        var text = b.Json();
        var wieder = EchtBaukasten.Laden(EchtZeichnung.Uml, null, text);
        Assert.False(wieder.Unlesbar);
        Assert.Equal(b.Diagramm.Elemente, wieder.Diagramm.Elemente);
        Assert.Equal(b.Diagramm.Verbindungen, wieder.Diagramm.Verbindungen);
        Assert.Equal(text, wieder.Json());
    }

    [Fact]
    public void B5_LadenMitVorgabe_VorgabeBleibtFest_EigeneKardinalitaetBleibt()
    {
        var vorgabe = ErTeil.Antwort.Vorgabe;
        var b = EchtBaukasten.Neu(EchtZeichnung.Er, vorgabe);
        var p = b.Setzen("entitaet", "tour")!;
        b.Beschriften(p, "Patient");
        b.Kardinalitaet("faehrt", "tour", "n");
        var wieder = EchtBaukasten.Laden(EchtZeichnung.Er, vorgabe, b.Json());
        Assert.False(wieder.Unlesbar);
        Assert.True(wieder.IstFest("pk"));
        Assert.False(wieder.IstFest(p));
        Assert.Equal("n", wieder.Diagramm.Verbindungen.Single(v => v.Von == "faehrt" && v.Nach == "tour").Kard);
    }

    [Theory]
    [InlineData("kein json")]
    [InlineData("[]")]
    [InlineData("""{"art": "er", "elemente": [], "verbindungen": []}""")]
    [InlineData("""{"art": "uml", "elemente": [{"id": "a", "typ": "aktion", "x": 9, "y": 0}], "verbindungen": []}""")]
    [InlineData("""{"art": "uml", "elemente": [{"id": "a", "typ": "attribut", "x": 0, "y": 0}], "verbindungen": []}""")]
    [InlineData("""{"art": "uml", "elemente": [{"id": "a", "typ": "aktion", "x": 0, "y": 0}, {"id": "a", "typ": "ende", "x": 1, "y": 0}], "verbindungen": []}""")]
    [InlineData("""{"art": "uml", "elemente": [{"id": "s", "typ": "start", "x": 0, "y": 0}, {"id": "a", "typ": "aktion", "x": 1, "y": 0}], "verbindungen": [{"von": "a", "nach": "s"}]}""")]
    [InlineData("""{"art": "uml", "elemente": [{"id": "a", "typ": "aktion", "x": 0, "y": 0}], "verbindungen": [{"von": "a", "nach": "x"}]}""")]
    public void B5_Unlesbar_StuerztNie_BeginntMitVorgabe(string text)
    {
        var b = EchtBaukasten.Laden(EchtZeichnung.Uml, null, text);
        Assert.True(b.Unlesbar);
        Assert.Empty(b.Diagramm.Elemente);
        // Gegenprobe: leer ist kein Fehler
        Assert.False(EchtBaukasten.Laden(EchtZeichnung.Uml, null, "").Unlesbar);
    }

    [Fact]
    public void B5_Unlesbar_WennDieVorgabeVeraendertWurde()
    {
        var vorgabe = ErTeil.Antwort.Vorgabe;
        var b = EchtBaukasten.Neu(EchtZeichnung.Er, vorgabe);
        b.Setzen("entitaet");
        var text = b.Json().Replace("Pflegekraft", "Fahrer", StringComparison.Ordinal);
        Assert.True(EchtBaukasten.Laden(EchtZeichnung.Er, vorgabe, text).Unlesbar);
        Assert.False(EchtBaukasten.Laden(EchtZeichnung.Er, vorgabe, b.Json()).Unlesbar);
    }

    [Fact]
    public void B5_ZeichnungZaehltAlsBearbeitet_UndStehtImErgebnis()
    {
        var satz = B1;
        var zustand = EchtAblauf.Neu(satz, "echt_si_b1", DateTimeOffset.Now, TimeZoneInfo.Utc);
        var teil = ErTeil;
        Assert.False(EchtBewertung.Bearbeitet(zustand, 3, teil.Nr));
        var b = EchtBaukasten.Neu(EchtZeichnung.Er, teil.Antwort.Vorgabe);
        // nur die Vorgabe: nichts Eigenes, zaehlt nicht
        ((JsonObject)zustand["antworten"]!)[EchtBewertung.Feld(3, teil.Nr, EchtBaukasten.Feld)] = b.Json();
        Assert.False(EchtBewertung.Bearbeitet(zustand, 3, teil.Nr));
        b.Setzen("entitaet", "tour");
        ((JsonObject)zustand["antworten"]!)[EchtBewertung.Feld(3, teil.Nr, EchtBaukasten.Feld)] = b.Json();
        Assert.True(EchtBewertung.Bearbeitet(zustand, 3, teil.Nr));
        var ergebnis = EchtBewertung.Auswerten(satz, zustand);
        Assert.Equal(b.Json(), (string?)ergebnis.Daten["antworten"]![EchtBewertung.Feld(3, teil.Nr, EchtBaukasten.Feld)]);
        // die Punkte bleiben Haekchen: eine Zeichnung allein bringt keinen Punkt
        Assert.Equal(0, EchtBewertung.Teil(zustand, 3, teil).Erreicht);
    }

    // -- B-2: das Programm zaehlt ------------------------------------------------------------------

    [Fact]
    public void B2_Zaehlen_RichtigNachgebaut_AllesGruen()
    {
        var zeilen = EchtBaukasten.Zaehlen(UmlNachgebaut().Diagramm, Uml(UmlMuster));
        Assert.All(zeilen, z => Assert.True(z.Ok, z.Text));
        Assert.Contains(zeilen, z => z.Text == "Aktion: 3, im Musterbild 3");
        Assert.Contains(zeilen, z => z.Text == "Verbindungen: 7, im Musterbild 7");
        Assert.Contains(zeilen, z => z.Text == EchtTexte.ZaehlRegelnOk);
    }

    [Fact]
    public void B2_Zaehlen_FehlendeAktionUndBedingung_Rot()
    {
        var b = UmlNachgebaut();
        var d = b.Diagramm;
        var v = d.Elemente.Single(e => e.Typ == "verzweigung").Id;
        var a3 = d.Elemente.Single(e => e.Text == "Ticket anlegen").Id;
        b.Bedingung(v, a3, "");
        b.Setzen("aktion");    // Aktion ohne Text und ohne Pfeile
        var zeilen = EchtBaukasten.Zaehlen(b.Diagramm, Uml(UmlMuster));
        Assert.Contains(zeilen, z => !z.Ok && z.Text == "Aktion: 4, im Musterbild 3");
        Assert.Contains(zeilen, z => !z.Ok && z.Text.StartsWith("Ohne Text: Aktion", StringComparison.Ordinal));
        Assert.Contains(zeilen, z => !z.Ok && z.Text.Contains("jeder mit Bedingung", StringComparison.Ordinal));
        Assert.DoesNotContain(zeilen, z => z.Text == EchtTexte.ZaehlRegelnOk);
        // Meldungen nennen Bausteine mit Namen, nie mit der inneren Kennung
        Assert.DoesNotContain(zeilen, z => z.Text.Contains(" b1", StringComparison.Ordinal) || z.Text.Contains(" b9", StringComparison.Ordinal));
    }

    [Fact]
    public void B2_Zaehlen_Er_KardinalitaetenUndSchluessel()
    {
        var teil = ErTeil;
        var muster = teil.Antwort.Muster!;
        // nur die Vorgabe: Patient, besucht und Attribute fehlen, Kardinalitaeten fehlen
        var nurVorgabe = EchtBaukasten.Zaehlen(teil.Antwort.Vorgabe!, muster);
        Assert.Contains(nurVorgabe, z => !z.Ok && z.Text == "Entität: 2, im Musterbild 3");
        Assert.Contains(nurVorgabe, z => !z.Ok && z.Text == "Beziehungen 1:n: 0, im Musterbild 1");
        Assert.Contains(nurVorgabe, z => !z.Ok && z.Text == "Beziehungen n:m: 0, im Musterbild 1");
        Assert.Contains(nurVorgabe, z => !z.Ok && z.Text == "Beziehungen ?:?: 1, im Musterbild 0");
        Assert.Contains(nurVorgabe, z => !z.Ok && z.Text == "Schlüsselattribute: 2, im Musterbild 3");
        // das Musterbild selbst: alles gruen
        Assert.All(EchtBaukasten.Zaehlen(muster, muster), z => Assert.True(z.Ok, z.Text));
    }

    [Fact]
    public void B2_Zaehlen_ErVollstaendigImBaukastenNachgebaut_Gruen()
    {
        var teil = ErTeil;
        var b = EchtBaukasten.Neu(EchtZeichnung.Er, teil.Antwort.Vorgabe);
        string Neu(string typ, string text, string an)
        {
            var id = b.Setzen(typ, an)!;
            b.Beschriften(id, text);
            return id;
        }
        var qual = Neu("attribut", "Qualifikation", "pk");
        Assert.Null(b.Verbinden("pk", qual));
        var pat = Neu("entitaet", "Patient", "tour");
        var besucht = Neu("beziehung", "besucht", "tour");
        Assert.Null(b.Verbinden(besucht, "tour"));
        Assert.Null(b.Verbinden(besucht, pat));
        Assert.True(b.Kardinalitaet(besucht, "tour", "m"));
        Assert.True(b.Kardinalitaet(besucht, pat, "n"));    // m:n wie n:m
        Assert.True(b.Kardinalitaet("faehrt", "pk", "1"));
        Assert.True(b.Kardinalitaet("faehrt", "tour", "n"));
        foreach (var (text, an, schluessel) in new[] { ("Datum", "tour", false), ("Startzeit", "tour", false), ("geplante Uhrzeit", besucht, false),
            ("Patientennummer", pat, true), ("Name", pat, false), ("Adresse", pat, false) })
        {
            var a = Neu("attribut", text, an);
            Assert.Null(b.Verbinden(a, an));
            b.Schluessel(a, schluessel);
        }
        var zeilen = EchtBaukasten.Zaehlen(b.Diagramm, teil.Antwort.Muster!);
        Assert.All(zeilen, z => Assert.True(z.Ok, z.Text));
    }

    // -- B-3: Liste fuer den Screenreader ------------------------------------------------------------

    [Fact]
    public void B3_Zeilen_JederBausteinMitStelleUndVerbunden()
    {
        var b = UmlNachgebaut();
        var zeilen = b.Zeilen();
        Assert.Equal(b.Diagramm.Elemente.Count, zeilen.Count);
        var v = zeilen.Single(z => z.Zeile.StartsWith("Verzweigung", StringComparison.Ordinal)).Zeile;
        Assert.Equal("Verzweigung, Spalte 1, Zeile 3; weiter zu [dringend] Aktion „Bereitschaft anrufen“; [sonst] Aktion „Ticket anlegen“; "
            + "kommt von Aktion „Meldung aufnehmen“.", v);
        var er = EchtBaukasten.Neu(EchtZeichnung.Er, ErTeil.Antwort.Vorgabe);
        er.Kardinalitaet("faehrt", "pk", "1");
        var pk = er.Zeilen().Single(z => z.Id == "pk").Zeile;
        Assert.Contains(EchtTexte.VerbundenMit + " Attribut „Personalnummer“", pk, StringComparison.Ordinal);
        Assert.Contains("Beziehung „fährt“ (1)", pk, StringComparison.Ordinal);
        Assert.Contains("(Schlüssel)", er.Zeilen().Single(z => z.Id == "pnr").Zeile, StringComparison.Ordinal);
        // Gegenprobe: ein Baustein ohne Verbindung sagt es
        var neu = er.Setzen("entitaet")!;
        Assert.EndsWith(EchtTexte.OhneVerbindung + ".", er.Zeilen().Single(z => z.Id == neu).Zeile, StringComparison.Ordinal);
    }

    [Fact]
    public void Verstoesse_ErsterIstDieMeldungDerLadepruefung()
    {
        // der Umbau auf eine Liste aendert die Meldungen der Ladepruefung nicht
        var kaputt = UmlMuster.Replace("""{"von": "z", "nach": "e"}""", """{"von": "e", "nach": "z"}""", StringComparison.Ordinal);
        var fehler = Assert.Throws<EchtLadefehler>(() => Uml(kaputt));
        Assert.Equal("Test: kein Pfeil führt in den Start oder aus dem Ende", fehler.Message);
        var richtig = Uml(UmlMuster);
        var d = richtig with { Verbindungen = [.. richtig.Verbindungen.Take(6), new EchtVerbindung("e", "z", "", "")] };
        Assert.Equal("kein Pfeil führt in den Start oder aus dem Ende", EchtZeichnung.Verstoesse(d, vollstaendig: false).First());
    }
}
