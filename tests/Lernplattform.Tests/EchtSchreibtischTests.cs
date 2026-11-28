using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// A 1.20.0 "Pruefung echt W2" (Weisung P12-446 mit P12-445, Kurzplan P12-267, Nicos Bild-OK und Antworten 1a bis 5a laut
/// P12-419). Abnahmesaetze: (1) Antwortform "schreibtischtest" ab Format 6: Raster mit Spalte "Zeile", je Variable eine Spalte,
/// "Ausgabe"/"Rückgabe"; jede Zeile steht im Code, jede Spalte kommt im Code vor, die Code-Anlage gibt es und der Teil verweist
/// auf sie; (2) Nico 2a: "leer = unveraendert" nur, wenn der Satz es sagt, bewertet werden nur die offenen Zellen; Punkte je Zelle
/// oder je Schritt (nur wenn alle Zellen des Schritts stimmen); (3) Anlagen "ausgabe" (Eingabezeilen) und "daten" (CSV mit
/// Trennzeichen und Kopfzeile, JSON gueltig oder ausdruecklich fehlerhaft), im Text genannte Zeilen gibt es; (4) Nico 4a: die drei
/// vorhandenen Schreibtischtests sind eine neue Fassung im Raster, nur angehaengt, die alte Datei (Format 5) bleibt bytegleich und
/// ladbar, ein offener Lauf legt die alte Antwort beiseite; Ausgabe und Datenauszug nur im Probesatz. Jede Regel mit Gegenprobe;
/// ein kaputter Satz stuerzt nie ab.
/// </summary>
public sealed class EchtSchreibtischTests
{
    /// <summary>Die drei neuen Fassungen: Satz, Aufgabe, Teil (Nico 4a).</summary>
    public static readonly (string Kennung, int Aufgabe, string Nr)[] Neu = [("alle_ap1_03", 4, "b"), ("alle_ap1_05", 4, "a"), ("ae_b2_09", 2, "a")];

    private static JsonObject Roh(string kennung, int fassung) => JsonNode.Parse(File.ReadAllBytes(EchtTestDatei.Datei))!["saetze"]!.AsArray()
        .Select(s => s!.AsObject()).Single(s => (string?)s["kennung"] == kennung && ((int?)s["fassung"] ?? 1) == fassung).DeepClone().AsObject();

    private static byte[] Datei(int format, params JsonObject[] saetze) =>
        Encoding.UTF8.GetBytes(new JsonObject { ["format"] = format, ["saetze"] = new JsonArray([.. saetze.Select(s => s.DeepClone())]) }.ToJsonString());

    /// <summary>alle_ap1_03 Fassung 2 (ohne Feld "fassung", damit er allein laedt) mit Aenderung am Schreibtischtest 4 b.</summary>
    private static JsonObject Satz(Action<JsonObject>? antwort = null, Action<JsonObject>? satz = null)
    {
        var s = Roh("alle_ap1_03", 2);
        s.Remove("fassung");
        antwort?.Invoke(s["aufgaben"]![3]!["teile"]![1]!["antwort"]!.AsObject());
        satz?.Invoke(s);
        return s;
    }

    private static IReadOnlyList<EchtFehler> Fehler(JsonObject satz, int format = 6) => EchtSaetze.Laden(Datei(format, satz)).Fehler;

    private static void Abgewiesen(JsonObject satz, string meldung)
    {
        var f = Assert.Single(Fehler(satz));
        Assert.Equal("alle_ap1_03", f.Kennung);
        Assert.Contains(meldung, f.Meldung, StringComparison.Ordinal);
    }

    // ---- (4) Format 6 und die drei neuen Fassungen --------------------------------------------------------------------------------

    [Fact]
    public void DreiFassungen_NurAngehaengt_DateiDavorBytegleich_Format5()
    {
        var heute = File.ReadAllBytes(EchtTestDatei.Datei);
        var vorher = EchtTestDatei.NachL0;
        Assert.Equal(5, (int)JsonNode.Parse(vorher)!["format"]!);
        Assert.Equal(6, (int)JsonNode.Parse(heute)!["format"]!);
        // bis auf die Formatzahl (Stelle 13) und die angehaengten Saetze bytegleich
        var laenge = vorher.Length - "\n ]\n}".Length;
        var roh = (byte[])heute.Clone();
        Assert.Equal((byte)'6', roh[13]);
        roh[13] = (byte)'5';
        Assert.True(roh.AsSpan(0, laenge).SequenceEqual(vorher.AsSpan(0, laenge)), "Datei bis zu den neuen Fassungen nicht bytegleich mit Format 5");
        Assert.StartsWith(",\n  {\n   \"kennung\": \"alle_ap1_03\",\n   \"fassung\": 2,", Encoding.UTF8.GetString(roh, laenge, 80), StringComparison.Ordinal);
        // Gegenprobe: ein geaendertes Zeichen vorn faellt auf
        roh[400] ^= 1;
        Assert.False(roh.AsSpan(0, laenge).SequenceEqual(vorher.AsSpan(0, laenge)));
        var liste = JsonNode.Parse(heute)!["saetze"]!.AsArray();
        var alt = JsonNode.Parse(vorher)!["saetze"]!.AsArray().Count;
        Assert.Equal(alt + 3, liste.Count);
        Assert.Equal(Neu.Select(n => n.Kennung), liste.Skip(alt).Select(s => (string?)s!["kennung"]));
        Assert.All(liste.Skip(alt), s => Assert.Equal(2, (int)s!["fassung"]!));
    }

    [Fact]
    public void DreiFassungen_GenauDieserTeil_GleichePunkte_SonstNichtsGeaendert()
    {
        var app = EchtTestDatei.AppHeute();
        var alt = EchtSaetze.Laden(EchtTestDatei.NachL0, [], EchtTestDatei.Geloescht());
        Assert.Empty(app.Fehler);
        Assert.Equal(alt.Saetze.Select(s => s.Kennung), app.Saetze.Select(s => s.Kennung));
        foreach (var s in app.Saetze)
        {
            var a = alt.Satz(s.Kennung)!;
            var neu = Neu.Where(n => n.Kennung == s.Kennung).ToList();
            Assert.Equal(neu.Count > 0 ? 2 : a.Fassung, s.Fassung);
            Assert.Equal(a.Aufgaben.Select(x => x.Punkte), s.Aufgaben.Select(x => x.Punkte));
            for (var i = 0; i < s.Aufgaben.Count; i++)
            {
                Assert.Equal(a.Aufgaben[i].Teile.Select(t => (t.Nr, t.Punkte, t.Operator)), s.Aufgaben[i].Teile.Select(t => (t.Nr, t.Punkte, t.Operator)));
                for (var j = 0; j < s.Aufgaben[i].Teile.Count; j++)
                {
                    var (tn, ta) = (s.Aufgaben[i].Teile[j], a.Aufgaben[i].Teile[j]);
                    if (neu.Any(n => n.Aufgabe == i + 1 && n.Nr == tn.Nr))
                    {
                        // genau dieser Teil: vorher Tabelle, jetzt Raster; die Punkte zaehlt das Programm wie vorher
                        Assert.Equal(EchtSaetze.FormTabelle, ta.Antwort.Form);
                        Assert.Equal(EchtSaetze.FormSchreibtischtest, tn.Antwort.Form);
                        Assert.Equal(2, tn.Fassung);
                        Assert.True(tn.Antwort.LeerUnveraendert);
                        Assert.Equal(tn.Punkte, EchtSchreibtisch.Punkte(tn.Antwort));
                        Assert.Equal(ta.SelbstPunkte, tn.SelbstPunkte);
                        Assert.Contains(tn.Antwort.Code, tn.Anlagen);
                    }
                    else
                    {
                        Assert.Equal(string.Join("\n", ta.Text), string.Join("\n", tn.Text));
                        Assert.Equal(ta.Antwort.Form, tn.Antwort.Form);
                        Assert.Equal(1, tn.Fassung);
                    }
                }
            }
            Assert.Equal(a.Anlagen.Select(x => (x.Nr, x.Art, string.Join("\n", x.Zeilen))), s.Anlagen.Select(x => (x.Nr, x.Art, string.Join("\n", x.Zeilen))));
        }
    }

    [Fact]
    public void Format5_EchteTestdateiLaedt_SchreibtischtestAusgabeDatenErstAbFormat6()
    {
        var alt = EchtSaetze.Laden(EchtTestDatei.NachL0);
        Assert.Empty(alt.Fehler);
        Assert.DoesNotContain(alt.Saetze.SelectMany(s => s.Aufgaben).SelectMany(a => a.Teile), t => t.Antwort.Form == EchtSaetze.FormSchreibtischtest);
        // Schreibtischtest in Format 5: klare Meldung
        Assert.Contains("unbekannte Antwortform \"schreibtischtest\" (erst ab Format 6)", Assert.Single(Fehler(Satz(), 5)).Meldung, StringComparison.Ordinal);
        // Ausgabe und Datenauszug in Format 5: klare Meldung
        var probe = JsonNode.Parse(EchtProbesatz.Datei(File.ReadAllBytes(EchtTestDatei.Datei)))!.AsObject();
        probe["format"] = 5;
        var f = Assert.Single(EchtSaetze.Laden(Encoding.UTF8.GetBytes(probe.ToJsonString())).Fehler);
        Assert.Contains("(erst ab Format 6)", f.Meldung, StringComparison.Ordinal);
        // Gegenprobe: als Format 6 laden beide
        Assert.Empty(Fehler(Satz()));
        Assert.Empty(EchtSaetze.Laden(EchtProbesatz.Datei(File.ReadAllBytes(EchtTestDatei.Datei))).Fehler);
        Assert.Equal(6, EchtSaetze.Format);
    }

    // ---- (1)/(2) Ladepruefung des Schreibtischtests --------------------------------------------------------------------------------

    public static TheoryData<string, string> Regeln => new()
    {
        { "code-fehlt", "\"code\" fehlt" },
        { "code-unbekannt", "Code-Anlage 9 gibt es nicht" },
        { "code-keine-code-anlage", "Code-Anlage 1 gibt es nicht (oder sie ist keine Code-Anlage)" },
        { "teil-ohne-verweis", "verweist nicht auf seine Code-Anlage 6" },
        { "zeile-nicht-im-code", "Zeile 12 gibt es im Code der Anlage 6 nicht" },
        { "zeile-0", "\"zeile\" fehlt oder ist kleiner als 1" },
        { "spalte-nicht-im-code", "Spalte „summe“ kommt im Code der Anlage 6 nicht vor" },
        { "spalten-doppelt", "1 bis 9 verschiedene Spalten" },
        { "spalten-zehn", "1 bis 9 verschiedene Spalten" },
        { "werte-zu-wenig", "\"werte\" braucht so viele Zellen wie Spalten" },
        { "leer-ohne-regel", "leere Zelle, aber \"leer_unveraendert\" gilt nicht" },
        { "ohne-offene", "ohne offene Zelle" },
        { "punkte-beide", "Punkte stehen entweder beim Schritt oder bei den Zellen" },
        { "schritt-punkte-ohne-offen", "Schritt mit Punkten ohne offene Zelle" },
        { "punkte-summe", "Schreibtischtest: die offenen Zellen (oder Schritte mit Punkten) ergeben nicht die Punkte" },
        { "zu-viele-schritte", "Schritte, höchstens 40" },
        { "kein-objekt", "Zelle ist weder Text noch offen" },
    };

    [Theory]
    [MemberData(nameof(Regeln))]
    public void Ladepruefung_JedeRegelWeistDenSatzAb(string fall, string meldung)
    {
        static JsonArray Werte(JsonObject a, int schritt) => a["schritte"]![schritt]!["werte"]!.AsArray();
        JsonObject satz = fall switch
        {
            "code-fehlt" => Satz(a => a.Remove("code")),
            "code-unbekannt" => Satz(a => a["code"] = "9"),
            "code-keine-code-anlage" => Satz(a => a["code"] = "1", s => s["aufgaben"]![3]!["teile"]![1]!["anlagen"] = new JsonArray("1")),
            "teil-ohne-verweis" => Satz(null, s => s["aufgaben"]![3]!["teile"]![1]!.AsObject().Remove("anlagen")),
            "zeile-nicht-im-code" => Satz(a => a["schritte"]![0]!["zeile"] = 12),
            "zeile-0" => Satz(a => a["schritte"]![0]!["zeile"] = 0),
            "spalte-nicht-im-code" => Satz(a => a["spalten"]![0] = "summe"),
            "spalten-doppelt" => Satz(a => a["spalten"]![1] = "bedarf"),
            "spalten-zehn" => Satz(a =>
            {
                a["spalten"]!.AsArray().Add("leisten2");
                a["spalten"]!.AsArray().Add("leisten3");
                foreach (var s in a["schritte"]!.AsArray())
                {
                    s!["werte"]!.AsArray().Add("");
                    s["werte"]!.AsArray().Add("");
                }
            }),
            "werte-zu-wenig" => Satz(a => Werte(a, 0).RemoveAt(7)),
            "leer-ohne-regel" => Satz(a => a["leer_unveraendert"] = false),
            "ohne-offene" => Satz(a =>
            {
                foreach (var s in a["schritte"]!.AsArray())
                {
                    var w = s!["werte"]!.AsArray();
                    for (var i = 0; i < w.Count; i++)
                    {
                        w[i] = w[i] is JsonObject o ? (string)o["loesung"]![0]! : w[i]!.DeepClone();
                    }
                }
            }),
            "punkte-beide" => Satz(a => a["schritte"]![3]!["punkte"] = 1),
            "schritt-punkte-ohne-offen" => Satz(a => a["schritte"]![0]!["punkte"] = 1),
            "punkte-summe" => Satz(a => Werte(a, 3)[3] = new JsonObject { ["loesung"] = new JsonArray("4"), ["punkte"] = 2 }),
            "zu-viele-schritte" => Satz(a =>
            {
                var schritte = a["schritte"]!.AsArray();
                while (schritte.Count <= EchtSchreibtisch.SchritteHoechstens)
                {
                    schritte.Insert(0, schritte[0]!.DeepClone());
                }
            }),
            "kein-objekt" => Satz(a => Werte(a, 0)[0] = 23),
            _ => throw new ArgumentException(fall),
        };
        Abgewiesen(satz, meldung);
        // Gegenprobe: der echte Satz laedt ohne Fehler (nie ein Absturz)
        Assert.Empty(Fehler(Satz()));
    }

    [Fact]
    public void Ausgabe_Und_Rueckgabe_AlsSchluesselwort_GrossKleinEgal()
    {
        // "Ausgabe" steht im Code als "AUSGABE": zaehlt (Sonderspalte, gross oder klein)
        Assert.Empty(Fehler(Satz()));
        Assert.Contains("Ausgabe", EchtTestDatei.AppHeute().Satz("alle_ap1_03")!.Aufgaben[3].Teile[1].Antwort.Spalten);
        // Gegenprobe: eine normale Spalte muss genau so geschrieben im Code stehen
        Abgewiesen(Satz(a => a["spalten"]![0] = "BEDARF"), "Spalte „BEDARF“ kommt im Code");
    }

    // ---- (2) Bewertung -------------------------------------------------------------------------------------------------------------

    private static EchtTeil Teil(string kennung, int aufgabe, string nr) =>
        EchtTestDatei.AppHeute().Satz(kennung)!.Aufgaben[aufgabe - 1].Teile.Single(t => t.Nr == nr);

    [Fact]
    public void Bewertung_JeZelle_NurOffeneZellen_ZahlMitTausenderpunkt_TextMitVarianten()
    {
        var teil = Teil("alle_ap1_03", 4, "b");
        var a = teil.Antwort;
        var offen = a.Schritte.SelectMany((s, z) => s.Werte.Select((c, sp) => (z, sp, c))).Where(x => x.c.Offen).ToList();
        Assert.Equal(6, offen.Count);
        // alles richtig: volle Punkte; Ausgabe "6, 240" auch als "6 und 240"
        var richtig = offen.ToDictionary(x => (x.z, x.sp), x => x.c.Loesung[0]);
        richtig[(offen[^1].z, offen[^1].sp)] = "6 und 240";
        var (felder, punkte) = EchtSchreibtisch.Bewerten(a, (z, s) => richtig.GetValueOrDefault((z, s), "egal"));
        Assert.Equal((6, 6), (felder.Count(f => f), punkte));
        // eine falsch, eine leer: 4 Punkte; die vorgegebenen Zellen zaehlen nie (dort steht "egal")
        var zwei = new Dictionary<(int, int), string>(richtig) { [(offen[0].z, offen[0].sp)] = "5", [(offen[1].z, offen[1].sp)] = "" };
        (felder, punkte) = EchtSchreibtisch.Bewerten(a, (z, s) => zwei.GetValueOrDefault((z, s), "egal"));
        Assert.Equal([false, false, true, true, true, true], felder);
        Assert.Equal(4, punkte);
        // Zahl mit Tausenderpunkt und Leerzeichen gilt als Zahl
        var zelle = new EchtZelle(null, ["1200"], 1);
        Assert.True(EchtSchreibtisch.Richtig(zelle, " 1.200 "));
        Assert.True(EchtSchreibtisch.Richtig(zelle, "1200"));
        Assert.False(EchtSchreibtisch.Richtig(zelle, "1,2"));
        Assert.False(EchtSchreibtisch.Richtig(zelle, ""));
    }

    [Fact]
    public void Bewertung_JeSchritt_NurWennAlleZellenDesSchrittsStimmen()
    {
        var satz = Satz(a =>
        {
            // Schritt 4 (Zeile 4) bekommt zwei offene Zellen und 2 Punkte fuer den ganzen Schritt
            var w = a["schritte"]![3]!["werte"]!.AsArray();
            w[3] = new JsonObject { ["loesung"] = new JsonArray("4") };
            w[2] = new JsonObject { ["loesung"] = new JsonArray("600") };
            a["schritte"]![3]!["punkte"] = 2;
            // Schritt 5 dafuer vorgegeben (Summe bleibt 6)
            a["schritte"]![4]!["werte"]![4] = "40";
        });
        var bestand = EchtSaetze.Laden(Datei(6, satz));
        Assert.Empty(bestand.Fehler);
        var a = bestand.Satz("alle_ap1_03")!.Aufgaben[3].Teile[1].Antwort;
        Assert.Equal(6, EchtSchreibtisch.Punkte(a));
        string Wert(int z, int s, string z3s2, string z3s3) => (z, s) switch
        {
            (3, 2) => z3s2,
            (3, 3) => z3s3,
            _ => a.Schritte[z].Werte[s] is { Offen: true } c ? c.Loesung[0] : "",
        };
        Assert.Equal(6, EchtSchreibtisch.Bewerten(a, (z, s) => Wert(z, s, "600", "4")).Punkte);
        // eine Zelle des Schritts falsch: der ganze Schritt zaehlt nicht (Gegenprobe: je Zelle gaebe es 1 Punkt)
        var (felder, punkte) = EchtSchreibtisch.Bewerten(a, (z, s) => Wert(z, s, "600", "5"));
        Assert.Equal(4, punkte);
        Assert.Equal(1, felder.Count(f => !f));
    }

    [Fact]
    public void Bewertung_UeberDenZustand_Schluessel_ZeileSpalte()
    {
        var satz = EchtTestDatei.AppHeute().Satz("alle_ap1_05")!;
        var teil = satz.Aufgaben[3].Teile.Single(t => t.Nr == "a");
        var art = EchtArten.Praefix + EchtSaetze.Ap1;
        var zustand = EchtAblauf.Neu(satz, art, DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc);
        var antworten = (JsonObject)zustand["antworten"]!;
        foreach (var (s, z) in teil.Antwort.Schritte.Select((s, z) => (s, z)))
        {
            for (var sp = 0; sp < s.Werte.Count; sp++)
            {
                if (s.Werte[sp].Offen)
                {
                    antworten[EchtBewertung.Feld(4, "a", EchtBewertung.Zelle(z, sp))] = s.Werte[sp].Loesung[0];
                }
            }
        }
        var wertung = EchtBewertung.Teil(zustand, 4, teil);
        Assert.Equal((6, 6), (wertung.Programm, wertung.Erreicht));
        Assert.All(wertung.Felder, Assert.True);
        Assert.True(EchtBewertung.Bearbeitet(zustand, 4, "a"));
        // Gegenprobe: leer = nichts erreicht, nicht bearbeitet
        var leer = EchtAblauf.Neu(satz, art, DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc);
        Assert.Equal(0, EchtBewertung.Teil(leer, 4, teil).Erreicht);
        Assert.False(EchtBewertung.Bearbeitet(leer, 4, "a"));
    }

    [Fact]
    public void OffenerLauf_AlteFassung_TabellenAntwortKommtBeiseite()
    {
        var alt = EchtSaetze.Laden(EchtTestDatei.NachL0).Satz("alle_ap1_03")!;
        var neu = EchtTestDatei.AppHeute().Satz("alle_ap1_03")!;
        var zustand = EchtAblauf.Neu(alt, EchtArten.Praefix + EchtSaetze.Ap1, DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc);
        var antworten = (JsonObject)zustand["antworten"]!;
        // in der alten Tabelle hiess z0s1 etwas anderes als im Raster: die Antwort darf nicht still ins Raster rutschen
        antworten[EchtBewertung.Feld(4, "b", EchtBewertung.Zelle(0, 1))] = "4";
        antworten[EchtBewertung.Feld(4, "a", EchtBewertung.Luecke(0))] = "3";
        Assert.True(EchtAblauf.Angleichen(zustand, neu));
        Assert.False(antworten.ContainsKey(EchtBewertung.Feld(4, "b", EchtBewertung.Zelle(0, 1))));
        Assert.Equal("4", (string?)zustand[EchtAblauf.AlteFassungen]!["1"]!["antworten"]![EchtBewertung.Feld(4, "b", EchtBewertung.Zelle(0, 1))]);
        Assert.Equal("3", (string?)antworten[EchtBewertung.Feld(4, "a", EchtBewertung.Luecke(0))]);
        Assert.False(EchtBewertung.Bearbeitet(zustand, 4, "b"));
    }

    [Fact]
    public void ZellName_UndZeilennummern_FuerScreenreaderUndMarkierung()
    {
        var teil = Teil("alle_ap1_03", 4, "b");
        Assert.Equal("Schritt 4, Zeile 4, jeLeiste", EchtSchreibtisch.ZellName(teil.Antwort, 3, 3));
        var code = EchtTestDatei.AppHeute().Satz("alle_ap1_03")!.Anlage("6")!;
        var nummern = EchtSchreibtisch.Zeilennummern(code);
        Assert.All(teil.Antwort.Schritte, s => Assert.True(nummern.ContainsKey(s.Zeile), "Zeile " + s.Zeile));
        Assert.Equal(EchtTexte.SchreibtischZeile, "Zeile");
    }

    // ---- (3) Ausgabe und Datenauszug -------------------------------------------------------------------------------------------------

    private static JsonObject Probe(Action<JsonArray> anlagen)
    {
        var wurzel = JsonNode.Parse(EchtProbesatz.Datei(File.ReadAllBytes(EchtTestDatei.Datei)))!.AsObject();
        var satz = wurzel["saetze"]![0]!.AsObject();
        satz.Remove("fassung");
        anlagen(satz["anlagen"]!.AsArray());
        return satz;
    }

    private static JsonObject Anlage(JsonArray anlagen, string nr) => anlagen.Single(a => (string?)a!["nr"] == nr)!.AsObject();

    [Fact]
    public void Probesatz_LaedtOhneFehler_AusgabeCsvJson()
    {
        var bestand = EchtSaetze.Laden(EchtProbesatz.Datei(File.ReadAllBytes(EchtTestDatei.Datei)));
        Assert.Empty(bestand.Fehler);
        var satz = Assert.Single(bestand.Saetze);
        var ausgabe = satz.Anlage("7")!;
        Assert.Equal((EchtDaten.Ausgabe, 11), (ausgabe.Art, ausgabe.Zeilen.Count));
        Assert.Equal([1, 6, 9], ausgabe.Eingaben);
        var csv = satz.Anlage("8")!;
        Assert.Equal((EchtDaten.Csv, ";", 6), (csv.DatenFormat, csv.Trennzeichen, csv.Felder.Count));
        Assert.All(csv.Felder, f => Assert.Equal(6, f.Count));
        Assert.Equal("", csv.Felder[2][3]);
        var json = satz.Anlage("9")!;
        Assert.Equal((EchtDaten.Json, false), (json.DatenFormat, json.Fehlerhaft));
        Assert.All(EchtProbesatz.Verweise, v => Assert.Contains(v.Anlage, satz.Aufgaben[v.Aufgabe - 1].Teile.Single(t => t.Nr == v.Teil).Anlagen));
    }

    public static TheoryData<string, string> DatenRegeln => new()
    {
        { "eingabe-zu-gross", "Eingabezeile 12 gibt es nicht" },
        { "eingabe-doppelt", "Eingabezeile 6 gibt es nicht (oder doppelt)" },
        { "eingaben-kein-feld", "\"eingaben\" ist keine Liste" },
        { "ausgabe-61", "1 bis 60 Zeilen" },
        { "csv-ohne-trenner", "\"trennzeichen\" fehlt" },
        { "csv-trenner-zwei", "\"trennzeichen\" muss genau ein Zeichen sein" },
        { "csv-feldzahl", "Zeile 4 hat 7 Felder, die Kopfzeile 6" },
        { "csv-kopf-leer", "Kopfzeile (Zeile 1) braucht in jeder Spalte einen Namen" },
        { "csv-nur-kopf", "Kopfzeile und mindestens eine Datenzeile" },
        { "csv-offenes-zitat", "Zeile 2 hat ein offenes Anführungszeichen" },
        { "csv-fehlerhaft", "\"fehlerhaft\" gibt es nur bei JSON" },
        { "json-kaputt", "kein gültiges JSON" },
        { "json-fehlerhaft-aber-gut", "ist aber gültiges JSON" },
        { "format-unbekannt", "\"format\" muss \"csv\" oder \"json\" sein" },
        { "zeile-im-text", "Zeile 12 der Anlage 7 gibt es nicht (sie hat 11 Zeilen)" },
    };

    [Theory]
    [MemberData(nameof(DatenRegeln))]
    public void Ladepruefung_AusgabeUndDaten_JedeRegelMitGegenprobe(string fall, string meldung)
    {
        JsonObject satz = Probe(a =>
        {
            switch (fall)
            {
                case "eingabe-zu-gross":
                    Anlage(a, "7")["eingaben"] = new JsonArray(1, 12);
                    break;
                case "eingabe-doppelt":
                    Anlage(a, "7")["eingaben"] = new JsonArray(1, 6, 6);
                    break;
                case "eingaben-kein-feld":
                    Anlage(a, "7")["eingaben"] = "1";
                    break;
                case "ausgabe-61":
                    Anlage(a, "7")["zeilen"] = new JsonArray([.. Enumerable.Range(1, 61).Select(i => (JsonNode?)("Zeile " + i))]);
                    break;
                case "csv-ohne-trenner":
                    Anlage(a, "8").Remove("trennzeichen");
                    break;
                case "csv-trenner-zwei":
                    Anlage(a, "8")["trennzeichen"] = ";;";
                    break;
                case "csv-feldzahl":
                    Anlage(a, "8")["zeilen"]![3] = "K-1003;becker gmbh;34117;Kassel;14.03.2026;980,00;x";
                    break;
                case "csv-kopf-leer":
                    Anlage(a, "8")["zeilen"]![0] = "kundennr;;plz;ort;letzte_bestellung;umsatz_2025";
                    break;
                case "csv-nur-kopf":
                    Anlage(a, "8")["zeilen"] = new JsonArray("kundennr;name");
                    break;
                case "csv-offenes-zitat":
                    Anlage(a, "8")["zeilen"]![1] = "K-1001;\"Becker GmbH;34117;Kassel;2026-03-14;12480,50";
                    break;
                case "csv-fehlerhaft":
                    Anlage(a, "8")["fehlerhaft"] = true;
                    break;
                case "json-kaputt":
                    Anlage(a, "9")["zeilen"]![9] = "";
                    break;
                case "json-fehlerhaft-aber-gut":
                    Anlage(a, "9")["fehlerhaft"] = true;
                    break;
                case "format-unbekannt":
                    Anlage(a, "9")["format"] = "xml";
                    break;
            }
        });
        if (fall == "zeile-im-text")
        {
            var t = satz["aufgaben"]![0]!["teile"]!.AsArray().Single(x => (string?)x!["nr"] == "f")!;
            t["text"]!.AsArray().Add("Nennen Sie die Ursache aus Zeile 12 der Anlage 7.");
        }
        var f = Assert.Single(EchtSaetze.Laden(Datei(6, satz)).Fehler);
        Assert.Contains(meldung, f.Meldung, StringComparison.Ordinal);
        // Gegenprobe: der Probesatz selbst laedt
        Assert.Empty(EchtSaetze.Laden(Datei(6, Probe(_ => { }))).Fehler);
    }

    [Fact]
    public void Daten_JsonAbsichtlichFehlerhaft_CsvMitZitat_ZeilenImTextGut()
    {
        var satz = Probe(a =>
        {
            Anlage(a, "9")["zeilen"]![9] = "";
            Anlage(a, "9")["fehlerhaft"] = true;
            Anlage(a, "8")["zeilen"]![1] = "K-1001;\"Becker; Söhne \"\"Nord\"\"\";34117;Kassel;2026-03-14;12480,50";
        });
        var t = satz["aufgaben"]![0]!["teile"]!.AsArray().Single(x => (string?)x!["nr"] == "f")!;
        t["text"]!.AsArray().Add("Die Ursache steht in Zeile 9 bis 11 der Anlage 7.");
        var bestand = EchtSaetze.Laden(Datei(6, satz));
        Assert.Empty(bestand.Fehler);
        var s = bestand.Saetze.Single();
        Assert.True(s.Anlage("9")!.Fehlerhaft);
        Assert.Equal("Becker; Söhne \"Nord\"", s.Anlage("8")!.Felder[1][1]);
        Assert.Equal(6, s.Anlage("8")!.Felder[1].Count);
        Assert.Equal(["a", "b,c", ""], EchtDaten.CsvZeile("a,\"b,c\",", ','));
        Assert.Null(EchtDaten.CsvZeile("a,\"b", ','));
    }
}
