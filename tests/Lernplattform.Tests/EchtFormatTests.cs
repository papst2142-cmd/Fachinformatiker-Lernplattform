using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Fortschritt;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.76.0, Welle 1 "Pruefung im echten Format" (Weisung P12-262, Plan P12-152): Ladepruefung der Datei
/// inhalte_csharp/lernen/pruefungssaetze.json, Bewertung der Felder, die das Programm selbst bewertet, Ablauf
/// (Start, Uhr, Wiederaufnahme, Abgabe, Selbstbewertung, Abschluss einmal) und Abnahmesatz W1-7
/// (Gesamtergebnis unveraendert, eigene Liste). Jede Regel mit Gegenprobe.
/// </summary>
public class EchtFormatTests
{
    private static readonly string Datei = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen", EchtSaetze.Datei);

    /// <summary>Seit A 0.79.0 nur die Probesaetze (die 15 Saetze der W3 prueft <see cref="EchtInhalteW3Tests"/>).</summary>
    private static byte[] Roh => EchtInhalteW3Tests.NurProbesaetze();

    private static EchtBestand Bestand => EchtSaetze.Laden(Roh);

    /// <summary>Probesatz AP1 der Welle 1 (seit A 0.77.0 steht dahinter der Probesatz AP2 Konzeption si_b1_01).</summary>
    private static EchtSatz Probesatz => Bestand.Saetze.Single(s => s.Kennung == "alle_ap1_01");

    /// <summary>Gueltige Saetze der eingebauten Datei, in der Reihenfolge der Datei.</summary>
    private static readonly string[] Gueltig = ["alle_ap1_01", "si_b1_01"];

    private static JsonObject Wurzel() => JsonNode.Parse(Roh)!.AsObject();

    private static JsonObject ErsterSatz(JsonObject wurzel) => wurzel["saetze"]![0]!.AsObject();

    private static EchtBestand Laden(JsonObject wurzel) => EchtSaetze.Laden(Encoding.UTF8.GetBytes(wurzel.ToJsonString()));

    // -- Ladepruefung --------------------------------------------------------------------------

    [Fact]
    public void Probesatz_LaedtOhneFehler_UndGiltFuerAlleFachrichtungenNurInAp1()
    {
        var bestand = Bestand;
        Assert.Empty(bestand.Fehler);
        Assert.Equal(Gueltig, bestand.Saetze.Select(s => s.Kennung));
        var satz = Probesatz;
        Assert.Equal(100, satz.Aufgaben.Sum(a => a.Punkte));
        Assert.Equal(90, satz.Minuten);
        Assert.Equal(4, satz.Aufgaben.Count);
        // Antwort 4a: AP1 fuer alle Fachrichtungen, AP2 zuerst nur SI (seit A 0.77.0 ein Satz Konzeption, b2 noch ohne)
        foreach (var f in Fachrichtung.Alle)
        {
            Assert.Single(bestand.Fuer(EchtSaetze.Ap1, f.Id));
            Assert.Equal(f.Id == "si" ? 1 : 0, bestand.Fuer(EchtSaetze.B1, f.Id).Count);
            Assert.Empty(bestand.Fuer(EchtSaetze.B2, f.Id));
        }
        // alle sechs Antwortformen und alle vier Anlagen-Arten der Welle 1 kommen im Probesatz vor
        var formen = satz.Aufgaben.SelectMany(a => a.Teile).Select(t => t.Antwort.Form).ToHashSet();
        Assert.Equal(EchtSaetze.Formen.Order(), formen.Order());
        Assert.Equal(EchtSaetze.AnlagenArten.Order(), satz.Anlagen.Select(a => a.Art).ToHashSet().Order());
        Assert.Contains(satz.Aufgaben.SelectMany(a => a.Teile), t => t.ErsatzBezug is not null);
        // P12-261: der Platzhalter der Skizze ist nie ein Satz
        Assert.DoesNotContain("Sonnenhof", satz.Firma, StringComparison.Ordinal);
    }

    [Fact]
    public void Datei_FehltNeuerKaputt_KlareMeldungNieAbsturz()
    {
        Assert.Same(EchtBestand.Leer, EchtSaetze.Laden(null));
        var neuer = Wurzel();
        neuer["format"] = EchtSaetze.Format + 1;
        var fehler = Assert.Single(Laden(neuer).Fehler);
        Assert.Equal(EchtSaetze.Datei, fehler.Kennung);
        Assert.Contains("neuer als dieses Programm", fehler.Meldung, StringComparison.Ordinal);
        var alt = Wurzel();
        alt["format"] = 0;
        Assert.Contains("unbekanntes Format", Assert.Single(Laden(alt).Fehler).Meldung, StringComparison.Ordinal);
        var kaputt = EchtSaetze.Laden(Encoding.UTF8.GetBytes("{\"format\": 1, \"saetze\": ["));
        Assert.Empty(kaputt.Saetze);
        Assert.Contains("kein gültiges JSON", Assert.Single(kaputt.Fehler).Meldung, StringComparison.Ordinal);
        // Fehler der ganzen Datei betreffen jeden Bereich (Platzhalter mit Meldung, nie still leer)
        Assert.Single(Laden(neuer).FehlerFuer(EchtSaetze.B2, "ae"));
        // Gegenprobe: das heutige Format laedt
        Assert.Empty(Laden(Wurzel()).Fehler);
    }

    /// <summary>Je Regel ein kaputter Satz: abgewiesen mit Meldung, ein gueltiger zweiter Satz bleibt.</summary>
    public static TheoryData<string, string> Regeln() => new()
    {
        { "minuten=60", "90 Minuten" },
        { "aufgabe4-weg", "3 Aufgaben statt 4" },
        { "aufgabe1-punkte=27", "erlaubt sind 24 bis 26" },
        { "summe-falsch", "statt 100 Punkte" },
        { "teil-punkte=1", "erlaubt sind 2 bis 9" },
        { "teile-zu-wenig", "erlaubt sind 5 bis 10" },
        { "teilsumme-falsch", "Teilaufgaben ergeben" },
        { "operator-unbekannt", "unbekannter Operator" },
        { "nennen-punkte", "Nennen: 2 bis 3 Angaben" },
        { "begruenden-punkte", "Begründen: 3 Punkte" },
        { "form-zeichnen", "\"diagramm\" fehlt" },
        { "anlage-fehlt", "Anlage 9 gibt es nicht" },
        { "anlage-unbenutzt", "wird von keiner Teilaufgabe benutzt" },
        { "englisch-zwei", "englische Texte statt genau einem" },
        { "englisch-teil-fehlt", "keine Teilaufgabe zum englischen Text" },
        { "ersatz-vorwaerts", "das ist kein früherer Teil" },
        { "ersatz-ohne-ergebnis", "gehört zu einer Rechnung mit \"ersatz_ergebnis\"" },
        { "zeitfrage-eine", "mindestens zwei Antworten" },
        { "loesung-leer", "Musterlösung fehlt" },
        { "hinweise-summe", "die Hinweise ergeben nicht die Punkte" },
        { "luecke-ip-kaputt", "passt nicht zur Feldart ip" },
        { "tabelle-zellen", "Tabelle: die offenen Zellen ergeben nicht die Punkte" },
        { "auswahl-richtig", "gültige richtige Nummern" },
        { "kennung", "Kennung muss wie" },
        { "ap1-fachrichtung", "Fachrichtung und Bereich passen nicht" },
        { "kennung-doppelt", "Kennung doppelt" },
        { "ap2-alle", "AP1 gilt für alle Fachrichtungen" },
    };

    [Theory]
    [MemberData(nameof(Regeln))]
    public void Ladepruefung_JedeRegelWeistDenSatzAb(string fall, string meldung)
    {
        var wurzel = Wurzel();
        var liste = wurzel["saetze"]!.AsArray();
        var kaputt = ErsterSatz(wurzel).DeepClone().AsObject();
        kaputt["kennung"] = "alle_ap1_02";
        Verderben(fall, kaputt);
        liste.Add(kaputt);
        var bestand = Laden(wurzel);
        var fehler = Assert.Single(bestand.Fehler);
        Assert.Contains(meldung, fehler.Meldung, StringComparison.Ordinal);
        // der gueltige Satz bleibt, der kaputte fehlt nie still: er steht mit Kennung in den Fehlern
        Assert.Equal(Gueltig, bestand.Saetze.Select(s => s.Kennung));
        Assert.NotEqual(EchtSaetze.Datei, fehler.Kennung);
    }

    [Fact]
    public void Ladepruefung_Gegenprobe_ZweiterGueltigerSatzLaedt()
    {
        var wurzel = Wurzel();
        var zweiter = ErsterSatz(wurzel).DeepClone().AsObject();
        zweiter["kennung"] = "alle_ap1_02";
        wurzel["saetze"]!.AsArray().Add(zweiter);
        var bestand = Laden(wurzel);
        Assert.Empty(bestand.Fehler);
        Assert.Equal(Gueltig.Length + 1, bestand.Saetze.Count);
    }

    private static JsonObject Teil(JsonObject satz, int aufgabe, string nr) =>
        satz["aufgaben"]![aufgabe - 1]!["teile"]!.AsArray().Select(t => t!.AsObject()).First(t => (string?)t["nr"] == nr);

    private static void Verderben(string fall, JsonObject s)
    {
        var aufgaben = s["aufgaben"]!.AsArray();
        switch (fall)
        {
            case "minuten=60":
                s["minuten"] = 60;
                break;
            case "aufgabe4-weg":
                aufgaben.RemoveAt(3);
                break;
            case "aufgabe1-punkte=27":
                aufgaben[0]!["punkte"] = 27;
                break;
            case "summe-falsch":
                // Aufgabe 1 in sich stimmig mit 24 Punkten (1h nennt 2 statt 3), die Summe ist dann 99
                aufgaben[0]!["punkte"] = 24;
                Teil(s, 1, "h")["punkte"] = 2;
                Teil(s, 1, "h")["angaben"] = 2;
                break;
            case "teil-punkte=1":
                Teil(s, 1, "a")["punkte"] = 1;
                break;
            case "teile-zu-wenig":
                var teile = aufgaben[2]!["teile"]!.AsArray();
                while (teile.Count > 4)
                {
                    teile.RemoveAt(teile.Count - 1);
                }
                break;
            case "teilsumme-falsch":
                aufgaben[0]!["punkte"] = 26;
                aufgaben[1]!["punkte"] = 25;
                break;
            case "operator-unbekannt":
                Teil(s, 1, "a")["operator"] = "raten";
                break;
            case "nennen-punkte":
                Teil(s, 1, "a")["angaben"] = 4;
                break;
            case "begruenden-punkte":
                Teil(s, 1, "d")["operator"] = "begruenden";
                Teil(s, 1, "d")["punkte"] = 4;
                break;
            case "form-zeichnen":
                Teil(s, 2, "f")["antwort"] = new JsonObject { ["form"] = "zeichnen" };
                break;
            case "anlage-fehlt":
                Teil(s, 1, "a")["anlagen"] = new JsonArray("9");
                break;
            case "anlage-unbenutzt":
                foreach (var nr in new[] { "a", "b", "c", "d", "g" })
                {
                    Teil(s, 1, nr).Remove("anlagen");
                }
                break;
            case "englisch-zwei":
                s["anlagen"]![1]!["art"] = "englisch";
                break;
            case "englisch-teil-fehlt":
                foreach (var a in aufgaben)
                {
                    foreach (var t in a!["teile"]!.AsArray())
                    {
                        t!.AsObject().Remove("englisch");
                    }
                }
                break;
            case "ersatz-vorwaerts":
                Teil(s, 4, "b")["ersatzwert"]!["bezug"] = "4c";
                break;
            case "ersatz-ohne-ergebnis":
                Teil(s, 4, "b")["antwort"]!.AsObject().Remove("ersatz_ergebnis");
                break;
            case "zeitfrage-eine":
                s["zeitfrage"]!["optionen"] = new JsonArray("nur eine");
                break;
            case "loesung-leer":
                Teil(s, 1, "a")["loesung"] = new JsonArray();
                break;
            case "hinweise-summe":
                var d = Teil(s, 1, "d");
                d["hinweise"]![0]!["punkte"] = (int)d["hinweise"]![0]!["punkte"]! + 1;
                break;
            case "luecke-ip-kaputt":
                Teil(s, 2, "a")["antwort"]!["felder"]![0]!["loesung"] = new JsonArray("10.24.8.300");
                break;
            case "tabelle-zellen":
                Teil(s, 1, "e")["antwort"]!["zeilen"]![1]![1]!["punkte"] = 5;
                break;
            case "auswahl-richtig":
                Teil(s, 1, "g")["antwort"]!["richtig"] = new JsonArray(7);
                break;
            case "kennung":
                s["kennung"] = "alle_ap1_x";
                break;
            case "ap1-fachrichtung":
                s["fachrichtung"] = "si";
                break;
            case "kennung-doppelt":
                s["kennung"] = "alle_ap1_01";
                break;
            case "ap2-alle":
                s["kennung"] = "alle_b1_01";
                s["bereich"] = "b1";
                break;
            default:
                throw new ArgumentException(fall);
        }
    }

    // -- Vergleich und Bewertung ----------------------------------------------------------------

    [Theory]
    [InlineData("ip", " 10.24.8.94 ", "10.24.8.94")]
    [InlineData("ip", "010.24.8.94", "10.24.8.94")]
    [InlineData("ip", "10.24.8.256", null)]
    [InlineData("ip", "10.24.8", null)]
    [InlineData("maske", "/27", "255.255.255.224")]
    [InlineData("maske", "255.255.255.224", "255.255.255.224")]
    [InlineData("maske", "255.255.255.225", null)]
    [InlineData("maske", "/33", null)]
    [InlineData("zahl", "4.821,07", "4821.07")]
    [InlineData("zahl", "4821,07", "4821.07")]
    [InlineData("zahl", "4821.07", "4821.07")]
    [InlineData("zahl", "abc", null)]
    [InlineData("text", "  Standard  GATEWAY ", "standard gateway")]
    [InlineData("text", "   ", null)]
    public void Normal_GleichtSchreibweisenAn(string art, string eingabe, string? erwartet) =>
        Assert.Equal(erwartet, EchtBewertung.Normal(art, eingabe));

    [Fact]
    public void Rechnen_ToleranzUndErsatzwert()
    {
        var b4 = Probesatz.Aufgaben[3].Teile.First(t => t.Nr == "b").Antwort;
        Assert.True(EchtBewertung.ErgebnisRichtig(b4, "13,3"));
        Assert.True(EchtBewertung.ErgebnisRichtig(b4, "13,34"));
        Assert.True(EchtBewertung.ErgebnisRichtig(b4, "12,5"));      // Folgerechnung mit dem Ersatzwert
        Assert.False(EchtBewertung.ErgebnisRichtig(b4, "13,4"));
        Assert.False(EchtBewertung.ErgebnisRichtig(b4, ""));
        var b1 = Probesatz.Aufgaben[0].Teile.First(t => t.Nr == "b").Antwort;
        Assert.True(EchtBewertung.ErgebnisRichtig(b1, "4.821,07"));
        Assert.False(EchtBewertung.ErgebnisRichtig(b1, "4821,08"));
        Assert.Null(b1.ErsatzErgebnis);
        Assert.False(EchtBewertung.ErgebnisRichtig(b1, "12,5"));
    }

    /// <summary>Volle Antworten aus der Musterloesung des Satzes: alles richtig und alle Haekchen ergibt 100.</summary>
    private static JsonObject Voll(EchtSatz satz, bool haken)
    {
        var antworten = new JsonObject();
        var alle = new JsonObject();
        for (var n = 0; n < satz.Aufgaben.Count; n++)
        {
            foreach (var t in satz.Aufgaben[n].Teile)
            {
                var a = t.Antwort;
                switch (a.Form)
                {
                    case EchtSaetze.FormRechnen:
                        antworten[EchtBewertung.Feld(n + 1, t.Nr, EchtBewertung.Ergebnis)] = a.Ergebnis.ToString(CultureInfo.GetCultureInfo("de-DE"));
                        antworten[EchtBewertung.Feld(n + 1, t.Nr, EchtBewertung.Weg)] = "Rechnung";
                        break;
                    case EchtSaetze.FormTabelle:
                        for (var z = 0; z < a.Tabelle.Count; z++)
                        {
                            for (var sp = 0; sp < a.Tabelle[z].Count; sp++)
                            {
                                if (a.Tabelle[z][sp].Offen)
                                {
                                    antworten[EchtBewertung.Feld(n + 1, t.Nr, EchtBewertung.Zelle(z, sp))] = a.Tabelle[z][sp].Loesung[0];
                                }
                            }
                        }
                        break;
                    case EchtSaetze.FormLuecke:
                        for (var f = 0; f < a.Felder.Count; f++)
                        {
                            antworten[EchtBewertung.Feld(n + 1, t.Nr, EchtBewertung.Luecke(f))] = a.Felder[f].Loesung[0];
                        }
                        break;
                    case EchtSaetze.FormAuswahl:
                        antworten[EchtBewertung.Feld(n + 1, t.Nr, EchtBewertung.Wahl)] = string.Join(",", a.Richtig);
                        break;
                    default:
                        antworten[EchtBewertung.Feld(n + 1, t.Nr, EchtBewertung.Text)] = "Antwort";
                        break;
                }
                if (haken)
                {
                    alle[EchtBewertung.Schluessel(n + 1, t.Nr)] = new JsonArray([.. Enumerable.Range(0, EchtBewertung.HakenAnzahl(t)).Select(_ => (JsonNode?)JsonValue.Create(true))]);
                }
            }
        }
        return new JsonObject { ["antworten"] = antworten, ["haken"] = alle, ["dauer"] = 1234 };
    }

    [Fact]
    public void Auswerten_VollIst100_ProgrammteilOhneHaken_LeerIstNull()
    {
        var satz = Probesatz;
        var voll = EchtBewertung.Auswerten(satz, Voll(satz, haken: true));
        Assert.Equal(100, voll.Punkte);
        Assert.Equal("1 (Sehr gut)", voll.Note);
        Assert.Equal(1234, voll.Dauer);
        Assert.Equal(satz.Aufgaben.Select(a => a.Punkte), voll.Aufgaben.Select(a => a.Erreicht));
        // ohne Haekchen nur, was das Programm bewertet: 100 minus alle Selbstpunkte
        var programm = EchtBewertung.Auswerten(satz, Voll(satz, haken: false));
        var selbst = satz.Aufgaben.SelectMany(a => a.Teile).Sum(t => t.SelbstPunkte);
        Assert.Equal(100 - selbst, programm.Punkte);
        Assert.InRange(programm.Punkte, 30, 70);
        var leer = EchtBewertung.Auswerten(satz, new JsonObject());
        Assert.Equal(0, leer.Punkte);
        Assert.Equal("6 (Ungenügend)", leer.Note);
        // Daten: Satz, Themen je Aufgabe, nie der Schluessel "aufgaben" der heutigen Klausur (Bereinigung, Gestellt)
        Assert.Equal(satz.Kennung, (string?)voll.Daten["satz"]);
        Assert.Equal(4, voll.Daten["themen"]!.AsObject().Count);
        Assert.False(voll.Daten.ContainsKey("aufgaben"));
        // Haekchen deckeln nie ueber die Selbstpunkte hinaus (zu viele Haekchen gespeichert)
        var zuviel = Voll(satz, haken: true);
        foreach (var p in zuviel["haken"]!.AsObject().ToList())
        {
            p.Value!.AsArray().Add(true);
            p.Value!.AsArray().Add(true);
        }
        Assert.Equal(100, EchtBewertung.Auswerten(satz, zuviel).Punkte);
    }

    [Fact]
    public void Auswahl_NurGanzRichtigZaehlt()
    {
        var satz = Probesatz;
        var teil = satz.Aufgaben[0].Teile.First(t => t.Antwort.Form == EchtSaetze.FormAuswahl);
        var schluessel = EchtBewertung.Feld(1, teil.Nr, EchtBewertung.Wahl);
        JsonObject Mit(string wahl) => new() { ["antworten"] = new JsonObject { [schluessel] = wahl } };
        Assert.Equal(teil.Punkte, EchtBewertung.Teil(Mit(string.Join(",", teil.Antwort.Richtig.Reverse())), 1, teil).Erreicht);
        var falsch = Enumerable.Range(0, teil.Antwort.Optionen.Count).First(i => !teil.Antwort.Richtig.Contains(i));
        Assert.Equal(0, EchtBewertung.Teil(Mit(string.Join(",", teil.Antwort.Richtig.Append(falsch))), 1, teil).Erreicht);
        Assert.Equal(0, EchtBewertung.Teil(Mit(""), 1, teil).Erreicht);
    }

    [Theory]
    [InlineData(100, "1 (Sehr gut)")]
    [InlineData(92, "1 (Sehr gut)")]
    [InlineData(91, "2 (Gut)")]
    [InlineData(81, "2 (Gut)")]
    [InlineData(80, "3 (Befriedigend)")]
    [InlineData(67, "3 (Befriedigend)")]
    [InlineData(66, "4 (Ausreichend)")]
    [InlineData(50, "4 (Ausreichend)")]
    [InlineData(49, "5 (Mangelhaft)")]
    [InlineData(30, "5 (Mangelhaft)")]
    [InlineData(29, "6 (Ungenügend)")]
    public void Note_IhkSchluesselAnDenGrenzen(int punkte, string note)
    {
        // Punkte ueber die Haekchen genau einstellen: Programmteil leer, Selbstteile der Reihe nach
        var satz = Probesatz;
        var alle = new JsonObject();
        var rest = punkte;
        for (var n = 0; n < satz.Aufgaben.Count && rest > 0; n++)
        {
            foreach (var t in satz.Aufgaben[n].Teile.Where(t => EchtBewertung.HakenAnzahl(t) > 0))
            {
                var liste = new JsonArray();
                for (var i = 0; i < EchtBewertung.HakenAnzahl(t); i++)
                {
                    var p = EchtBewertung.HakenPunkte(t, i);
                    liste.Add(p <= rest);
                    rest -= p <= rest ? p : 0;
                }
                alle[EchtBewertung.Schluessel(n + 1, t.Nr)] = liste;
            }
        }
        var zustand = Voll(satz, haken: false);
        // Programmteil dazu, bis die Zahl genau stimmt
        var ohne = new JsonObject { ["antworten"] = new JsonObject(), ["haken"] = alle };
        var ergebnis = EchtBewertung.Auswerten(satz, ohne);
        if (ergebnis.Punkte != punkte)
        {
            ohne["antworten"] = zustand["antworten"]!.DeepClone();
            ohne["haken"] = new JsonObject();
            ergebnis = EchtBewertung.Auswerten(satz, ohne);
            Assert.True(ergebnis.Punkte <= punkte, "Programmteil allein ueber " + punkte);
            rest = punkte - ergebnis.Punkte;
            var haken = new JsonObject();
            for (var n = 0; n < satz.Aufgaben.Count; n++)
            {
                foreach (var t in satz.Aufgaben[n].Teile.Where(t => EchtBewertung.HakenAnzahl(t) > 0))
                {
                    var liste = new JsonArray();
                    for (var i = 0; i < EchtBewertung.HakenAnzahl(t); i++)
                    {
                        var p = EchtBewertung.HakenPunkte(t, i);
                        liste.Add(p <= rest);
                        rest -= p <= rest ? p : 0;
                    }
                    haken[EchtBewertung.Schluessel(n + 1, t.Nr)] = liste;
                }
            }
            ohne["haken"] = haken;
            ergebnis = EchtBewertung.Auswerten(satz, ohne);
        }
        Assert.Equal(punkte, ergebnis.Punkte);
        Assert.Equal(note, ergebnis.Note);
    }

    // -- Ablauf ----------------------------------------------------------------------------------

    /// <summary>Einstellungen und Lernstand eines Geraets wie in KlausurAblaufTests.</summary>
    private sealed class Geraet : IDisposable
    {
        private readonly TempOrdner _ordner = new();
        public JsonObject Werte { get; set; } = [];
        public DateTimeOffset Jetzt { get; set; } = DateTimeOffset.Parse("2026-11-14T08:00:00Z", CultureInfo.InvariantCulture);
        public Func<JsonObject, bool> Darf { get; set; } = _ => true;
        public LernstandSchreiber Schreiber { get; }
        public Lernstand Lernstand { get; }

        public Geraet()
        {
            Lernstand = new Lernstand(Path.Combine(_ordner.Pfad, Lernstand.DateiName));
            Schreiber = new LernstandSchreiber(Lernstand, SpeichernTests.FesteUhr(() => Jetzt));
        }

        private bool Aendern(Action<JsonObject> aendern)
        {
            var neu = Werte.DeepClone().AsObject();
            aendern(neu);
            if (!Darf(neu))
            {
                return false;
            }
            Werte = neu;
            return true;
        }

        public EchtAblauf Neu() => new(() => Bestand, new EchtAblage(() => Werte.DeepClone().AsObject(), Aendern), () => Schreiber,
            () => Jetzt, KlausurTests.Berlin);

        public KlausurAblauf Klausur() => new(KlausurTests.Klausurinhalte, new LaufendePruefung(() => Werte.DeepClone().AsObject(), Aendern),
            () => Schreiber, () => Jetzt, KlausurTests.Berlin, () => new Lernplattform.Core.Zufall.PythonZufall(7));

        public long Zeilen() => (long)Lernstand.Abfragen("SELECT COUNT(*) FROM pruefungen")[0][0]!;

        public JsonObject? Gespeichert => Werte[EchtAblage.Schluessel] as JsonObject;

        public void Dispose() => _ordner.Dispose();
    }

    private static readonly string Art = EchtArten.Art(Fachrichtung.Alle[0], "ap1");

    [Fact]
    public void Ablauf_StartUhrWiederaufnahmeAbgabeAbschlussEinmal()
    {
        using var g = new Geraet();
        var a = g.Neu();
        Assert.Equal(EchtAnsicht.Auswahl, a.Zeigen().Ansicht);
        Assert.Equal("echt_ap1", Art);
        Assert.Equal(EchtAnsicht.Lauf, a.Starten(Probesatz, Art).Ansicht);
        Assert.Equal(90 * 60, a.Rest);
        Assert.False(string.IsNullOrEmpty(Json.Text(g.Gespeichert![EchtAblage.KennungSchluessel])));
        // eigener Schluessel: die heutige laufende Pruefung bleibt unberuehrt
        Assert.False(g.Werte.ContainsKey(LaufendePruefung.Schluessel));
        var ergebnis = EchtBewertung.Feld(1, "b", EchtBewertung.Ergebnis);
        Assert.True(a.Festhalten([(ergebnis, "4821,07"), (EchtBewertung.Feld(1, "a", "1"), "Stromverbrauch")]));
        Assert.False(a.Festhalten([(ergebnis, "4821,07")]));
        // Programm schliessen und wieder oeffnen: Antworten und Uhr bleiben
        g.Jetzt = g.Jetzt.AddMinutes(30);
        var b = g.Neu();
        Assert.Equal(EchtAnsicht.Lauf, b.Zeigen().Ansicht);
        Assert.Equal(60 * 60, b.Rest);
        Assert.Equal("4821,07", EchtBewertung.Antwort(b.Zustand!, ergebnis));
        Assert.Equal("2 von 32 Teilaufgaben bearbeitet", b.Fortschritt());
        // Haekchen erst in der Bewertung
        b.Haken(1, Probesatz.Aufgaben[0].Teile[0], 0, true);
        Assert.Empty(EchtBewertung.Haken(b.Zustand!, "1.a"));
        Assert.Equal(EchtAnsicht.Bewertung, b.Abgeben(auto: false).Ansicht);
        Assert.False(b.Festhalten([(ergebnis, "1")]));       // nach der Abgabe gesperrt
        b.Haken(1, Probesatz.Aufgaben[0].Teile[0], 0, true);
        Assert.Equal([true, false], EchtBewertung.Haken(g.Gespeichert!, "1.a"));
        b.Zeitfrage(1);
        Assert.Equal(EchtAnsicht.Bewertung, g.Neu().Zeigen().Ansicht);
        // Abschluss: eine Zeile, danach keine laufende Pruefung; erneuter Abschluss bucht nichts doppelt
        var kennung = Json.Text(g.Gespeichert![EchtAblage.KennungSchluessel]);
        var gespeichert = g.Gespeichert!.DeepClone().AsObject();
        Assert.Equal(EchtAnsicht.Ergebnis, b.Abschliessen().Ansicht);
        Assert.Equal(1, g.Zeilen());
        Assert.Null(g.Gespeichert);
        Assert.Equal(3, b.Ergebnis!.Punkte);   // 2 (Ergebnis 1b) + 1 (Haekchen 1a)
        g.Werte[EchtAblage.Schluessel] = gespeichert;       // Entfernen sei misslungen
        var c = g.Neu();
        Assert.Equal(EchtAnsicht.Bewertung, c.Zeigen().Ansicht);
        Assert.Equal(EchtAnsicht.Ergebnis, c.Abschliessen().Ansicht);
        Assert.Equal(1, g.Zeilen());
        var verlauf = Gesamtstand.Verlauf(g.Lernstand);
        var zeile = Assert.Single(verlauf);
        Assert.Equal(kennung, zeile.Kennung);
        Assert.Equal("echt_ap1", zeile.Art);
        Assert.Equal(3, zeile.Punkte);
        Assert.Equal(1, (int)zeile.Daten!["zeitfrage"]!);
        // naechster Satz: der einzige, auch nach der Bearbeitung
        Assert.Equal("alle_ap1_01", EchtAblauf.NaechsterSatz(Bestand.Fuer(EchtSaetze.Ap1, "si"), Art, verlauf)!.Kennung);
    }

    [Fact]
    public void Ablauf_ZeitAbgelaufen_AbgabeMitHinweisAuchNachNeustart()
    {
        using var g = new Geraet();
        g.Neu().Starten(Probesatz, Art);
        g.Jetzt = g.Jetzt.AddMinutes(91);
        var schritt = g.Neu().Zeigen();
        Assert.Equal(EchtAnsicht.Bewertung, schritt.Ansicht);
        Assert.Equal(PruefungTexte.ZeitOffen, schritt.Hinweis);
        Assert.Equal(Klausur.Bewertung, Json.Text(g.Gespeichert!["phase"]));
    }

    [Fact]
    public void Ablauf_AbbrechenVerwirft_StartUeberschreibtNie_OhneSpeichernKeinStart()
    {
        using var g = new Geraet();
        var a = g.Neu();
        a.Starten(Probesatz, Art);
        var kennung = Json.Text(g.Gespeichert![EchtAblage.KennungSchluessel]);
        var zweiter = g.Neu();
        Assert.Equal(EchtAnsicht.Lauf, zweiter.Starten(Probesatz, "echt_konzeption").Ansicht);
        Assert.Equal(Art, zweiter.Art);
        Assert.Equal(kennung, Json.Text(g.Gespeichert![EchtAblage.KennungSchluessel]));
        Assert.Equal(EchtAnsicht.Auswahl, zweiter.Abbrechen().Ansicht);
        Assert.Null(g.Gespeichert);
        Assert.Equal(0, g.Zeilen());
        g.Darf = _ => false;
        var dritter = g.Neu();
        var schritt = dritter.Starten(Probesatz, Art);
        Assert.Equal(EchtAnsicht.Auswahl, schritt.Ansicht);
        Assert.Equal(Lernplattform.Core.Texte.Optionen.SpeichernFehlgeschlagen, schritt.Hinweis);
        Assert.False(dritter.Laeuft);
    }

    [Fact]
    public void Ablauf_SatzFehlt_BleibtGespeichertMitHinweis()
    {
        using var g = new Geraet();
        g.Neu().Starten(Probesatz, Art);
        g.Werte[EchtAblage.Schluessel]!["satz"] = "alle_ap1_99";
        var schritt = g.Neu().Zeigen();
        Assert.Equal(EchtAnsicht.Auswahl, schritt.Ansicht);
        Assert.Equal(EchtTexte.SatzFehlt, schritt.Hinweis);
        Assert.NotNull(g.Gespeichert);
    }

    [Fact]
    public void Ablauf_HeutigeKlausurUndEchtesFormatStoerenSichNicht()
    {
        using var g = new Geraet();
        g.Klausur().Starten(Klausur.Konzeption);
        var heute = g.Werte[LaufendePruefung.Schluessel]!.ToJsonString();
        // die heutige Klausur sieht die Pruefung im echten Format nie als ihre an und umgekehrt
        Assert.False(g.Neu().Laeuft);
        Assert.Equal(EchtAnsicht.Auswahl, g.Neu().Zeigen().Ansicht);
        g.Werte[EchtAblage.Schluessel] = EchtAblauf.Neu(Probesatz, Art, g.Jetzt, KlausurTests.Berlin);
        Assert.Equal(KlausurAnsicht.Lauf, g.Klausur().Zeigen().Ansicht);
        var klausur = g.Klausur();
        klausur.Zeigen();
        Assert.Equal(Klausur.Konzeption, klausur.Art);
        Assert.Equal(heute, g.Werte[LaufendePruefung.Schluessel]!.ToJsonString());
        // eine fremde Art im eigenen Schluessel gilt nicht als Pruefung im echten Format
        g.Werte[EchtAblage.Schluessel]!["art"] = Klausur.Ap1;
        Assert.False(g.Neu().Laeuft);
    }

    [Fact]
    public void Probe_SpeichertNichts()
    {
        using var g = new Geraet();
        var a = g.Neu();
        a.ProbeSetzen(EchtAblauf.Neu(Probesatz, Art, g.Jetzt, KlausurTests.Berlin));
        Assert.True(a.ImLauf);
        a.Festhalten([(EchtBewertung.Feld(1, "b", EchtBewertung.Ergebnis), "1")]);
        a.Abgeben(auto: false);
        a.Haken(1, Probesatz.Aufgaben[0].Teile[0], 0, true);
        Assert.Equal(TrainerTexte.NichtGespeichert, a.Abschliessen().Hinweis);
        Assert.Empty(g.Werte);
        Assert.Equal(0, g.Zeilen());
    }

    // -- W1-7: Gesamtergebnis unveraendert, eigene Liste (Antwort 2a) ----------------------------

    [Fact]
    public void W1_7_GesamtergebnisUnveraendert_EigeneListe()
    {
        using var g = new Geraet();
        foreach (var (art, punkte) in new[] { ("ap1", 60.0), ("konzeption", 70.0), ("netzwerke", 55.0), ("wiso", 80.0) })
        {
            Assert.True(g.Schreiber.PruefungAbschliessen(LernstandSchreiber.NeueKennung(), art, punkte, Trainer.Note(punkte), 60, "{}", []));
        }
        var vorher = Gesamtstand.Verlauf(g.Lernstand);
        var si = Fachrichtung.Alle[0];
        var kachelVorher = Gesamtstand.Kachel(si, vorher, 75);
        var letzteVorher = Gesamtstand.Letzte(vorher);
        // danach drei Pruefungen im echten Format, die neueste mit 100 Punkten
        g.Jetzt = g.Jetzt.AddHours(1);
        foreach (var (art, punkte) in new[] { ("echt_ap1", 10.0), ("echt_konzeption", 20.0), ("echt_ap1", 100.0) })
        {
            g.Jetzt = g.Jetzt.AddMinutes(1);
            Assert.True(g.Schreiber.PruefungAbschliessen(LernstandSchreiber.NeueKennung(), art, punkte, Trainer.Note(punkte), 60,
                "{\"satz\": \"alle_ap1_01\"}", []));
        }
        var nachher = Gesamtstand.Verlauf(g.Lernstand);
        Assert.Equal(7, nachher.Count);
        var kachelNachher = Gesamtstand.Kachel(si, EchtArten.Ohne(nachher), 75);
        Assert.Equal(kachelVorher.Zeilen, kachelNachher.Zeilen);
        Assert.Equal(kachelVorher.GesamtZeile, kachelNachher.GesamtZeile);
        Assert.Equal(kachelVorher.Ergebnis.Gesamt, kachelNachher.Ergebnis.Gesamt);
        // auch ohne Filter aendert eine Art "echt_*" die Kachel nie (keine Rolle hat diese Art)
        Assert.Equal(kachelVorher.Zeilen, Gesamtstand.Kachel(si, nachher, 75).Zeilen);
        Assert.Equal(letzteVorher, Gesamtstand.Letzte(EchtArten.Ohne(nachher)));
        // Gegenprobe: ungefiltert stuenden sie in "Letzte Prüfungen"
        Assert.NotEqual(letzteVorher, Gesamtstand.Letzte(nachher));
        // eigene Liste: nur die drei, neueste zuerst
        var eigene = EchtArten.Nur(nachher);
        Assert.Equal(["echt_ap1", "echt_konzeption", "echt_ap1"], eigene.Select(e => e.Art));
        Assert.Equal(100, eigene[0].Punkte);
        // Oberflaeche: die heutigen Karten bekommen den Verlauf nur gefiltert
        var teil = File.ReadAllText(Path.Combine(Pfade.Wurzel, "src", "Lernplattform.App", "Views", "KlausurTeil.cs"));
        Assert.DoesNotContain("Gesamtstand.Letzte(verlauf)", teil, StringComparison.Ordinal);
        Assert.DoesNotContain("Gesamtstand.Kachel(fachrichtung, verlauf", teil, StringComparison.Ordinal);
        Assert.Contains("EchtArten.Ohne(", teil, StringComparison.Ordinal);
    }

    [Fact]
    public void Namen_FortschrittZeigtDieArtenImEchtenFormat()
    {
        Assert.Equal(("AP1 · Einrichten eines IT-gestützten Arbeitsplatzes (echtes Format)", "AP1 (Teil 1) echt"), Auswertung.PruefungName("echt_ap1"));
        Assert.Equal(("AP2 · Analyse und Entwicklung von Netzwerken (echtes Format)", "Netzwerke echt"), Auswertung.PruefungName("echt_netzwerke"));
        Assert.Equal("Algorithmen echt", Auswertung.PruefungName("echt_algorithmen")!.Value.Kurz);
        // die heutigen Arten bleiben wie Python, Unbekanntes bleibt null
        Assert.Equal(Auswertung.PruefungName("ap1"), Auswertung.PruefungName("ap1"));
        Assert.DoesNotContain("echt", Auswertung.PruefungName("ap1")!.Value.Name, StringComparison.Ordinal);
        Assert.Null(Auswertung.PruefungName("echt_projekt"));
        Assert.Null(Auswertung.PruefungName("echt_wiso"));
        Assert.Null(Auswertung.PruefungName("echt_"));
        Assert.Null(EchtArten.Bereich("ap1"));
    }

    [Fact]
    public void KeinPausenwegImCode()
    {
        foreach (var typ in new[] { typeof(EchtAblauf), typeof(EchtAblage) })
        {
            foreach (var m in typ.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                Assert.DoesNotMatch("(?i)pause|anhalt|fortsetz|unterbrech|startsetz", m.Name);
            }
        }
    }

    [Fact]
    public void Texte_PlatzhalterUndKeinDoppelleerzeichen()
    {
        foreach (var f in typeof(EchtTexte).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral))
        {
            var text = (string)f.GetRawConstantValue()!;
            Assert.False(text.Contains("  ", StringComparison.Ordinal), f.Name);
            Assert.Equal(text.TrimEnd(), text);
            Assert.DoesNotMatch("%[^sd%]", text);
        }
    }
}
