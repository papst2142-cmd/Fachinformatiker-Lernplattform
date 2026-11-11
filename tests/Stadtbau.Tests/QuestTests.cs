using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Tests;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle Q1 (S 0.40.0, Weisung P12-186, Kurzplan Meldung P12-111 Abschnitte 2–5 und 7): Quests, Questgeber und
/// Verkettungen im Werkzeug. Jede Pruefregel hat einen roten Fall und einen gruenen (die Gegenprobe zum roten: dieselbe
/// Quest ohne den Fehler). Export: die Abschnitte stehen in der Stadt-Datei, das Spiel laedt sie und bleibt unveraendert
/// (Fingerabdruck der Stadt gleich; Gegenprobe: die Datei ohne die Abschnitte ergibt denselben Stand).
/// </summary>
public class QuestTests
{
    internal static Projekt Stadt1()
    {
        var p = Projekt.Neu("Quests", Daten.Spiel, Daten.NeueAblage());
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        return p;
    }

    private static PyObjekt Inhalt => Daten.Spiel.Inhalt;

    /// <summary>Eine fertige, gruene neue Quest vom Typ Auswahl beim Questgeber <paramref name="qg"/>.</summary>
    private static string GuteAuswahl(Projekt p, string qg = "petra")
    {
        var id = p.QuestNeu("auswahl", qg);
        p.QuestFeld(id, "titel", "WLAN im Bahnhofscafé");
        p.QuestFeld(id, "ticket", QuestDaten.AusZeilen("Die Wirtin klagt: Gäste kommen ins WLAN, aber keine Seite lädt."));
        p.QuestFeld(id, "frage", "Was prüfst du zuerst?");
        p.QuestFeld(id, "optionen", new List<object?> { "DNS-Server im Router prüfen", "Router gegen ein neues Gerät tauschen", "WLAN-Kennwort ändern" });
        p.QuestFeld(id, "antwort", "DNS-Server im Router prüfen");
        p.QuestFeld(id, "hilfe", QuestDaten.AusZeilen("Verbindung da, Namen werden nicht aufgelöst."));
        p.QuestFeld(id, "erklaerung", QuestDaten.AusZeilen("Ohne DNS kein Name, ohne Name keine Seite."));
        return id;
    }

    private static string GuteZuordnung(Projekt p, string qg = "petra")
    {
        var id = p.QuestNeu("zuordnung", qg);
        p.QuestFeld(id, "titel", "Kabeltypen sortieren");
        p.QuestFeld(id, "ticket", QuestDaten.AusZeilen("Im Lager liegen Kabel durcheinander."));
        p.QuestFeld(id, "frage", "Ordne jedem Kabel den Stecker zu.");
        p.QuestFeld(id, "paare", new List<object?> { new List<object?> { "Netzwerk", "RJ45" }, new List<object?> { "Glasfaser", "LC" } });
        p.QuestFeld(id, "hilfe", QuestDaten.AusZeilen("Schau auf den Stecker."));
        p.QuestFeld(id, "erklaerung", QuestDaten.AusZeilen("RJ45 für Kupfer, LC für Glas."));
        return id;
    }

    private static List<QuestPruefung.Fund> Funde(Projekt p) => p.QuestFunde();

    private static List<string> Rot(Projekt p, string? quest = null) =>
        [.. Funde(p).Where(f => f.Rot && (quest is null || f.Quest == quest)).Select(f => f.Text)];

    private static List<string> Gelb(Projekt p) => [.. Funde(p).Where(f => !f.Rot).Select(f => f.Text)];

    private static void RotMit(Projekt p, string teil, string? quest = null) =>
        Assert.Contains(Rot(p, quest), t => t.Contains(teil, StringComparison.Ordinal));

    // ---- Daten, Bestand, Export ------------------------------------------------------------------------

    [Fact]
    public void OhneAbschnitte_KeinFund_StadtWieVorher()
    {
        var p = Stadt1();
        Assert.Empty(Funde(p));
        Assert.Null(SpielDaten.Get(Aussen.Stadtdaten(p.AussenStand), QuestDaten.Abschnitt));
        Assert.Equal(150, p.Quests().Count);
        // 17 Auftraggeber: 12 Kollegen, 5 Personen beim Kunden
        var qg = p.Questgeber();
        Assert.Equal(17, qg.Count);
        Assert.All(qg, q => Assert.True(q.Bestand));
        Assert.Equal(5, qg.Count(q => q.Stelle != SpielDaten.OrtBuero));
    }

    [Fact]
    public void NeueQuest_Gruen_OhneGelb_EinSchrittJeAenderung()
    {
        var p = Stadt1();
        var vorher = p.Verlauf.Count;
        var id = GuteAuswahl(p);
        Assert.Equal("neu-1", id);
        Assert.Equal(vorher + 8, p.Verlauf.Count);
        Assert.Empty(Rot(p));
        Assert.Empty(Gelb(p));   // Welle Q2: das Spiel liest die Quests, kein Einbau-Hinweis mehr
        // gleicher Wert = kein Schritt
        p.QuestFeld(id, "frage", "Was prüfst du zuerst?");
        Assert.Equal(vorher + 8, p.Verlauf.Count);
        p.Rueckgaengig();
        Assert.Contains(Rot(p, id), t => t.Contains(Texte.QuestFeldErklaerung, StringComparison.Ordinal));
    }

    [Fact]
    public void Export_EnthaeltBeideAbschnitte_SpielLaedtUndBleibtGleich()
    {
        var p = Stadt1();
        var id = GuteAuswahl(p);
        GuteZuordnung(p, "ulla");
        var qg = p.QuestgeberNeu();
        p.QuestgeberPlatz(qg, InnenPlatz(p));
        p.QuestUmhaengen(id, qg);
        Assert.Empty(Rot(p));
        var st = StadtFassung.Lesen(p.Exportieren(DateTimeOffset.Now));
        Assert.IsType<List<object?>>(SpielDaten.Get(st.Stadtdaten, QuestDaten.Abschnitt));
        Assert.IsType<List<object?>>(SpielDaten.Get(st.Stadtdaten, QuestgeberDaten.Abschnitt));
        Assert.Equal([], StadtbauPruefung.Inhalt(st, Inhalt));
        Assert.Equal([], SpielPruefung.Welt(st.MitKarte(Inhalt)));
        // Gegenprobe: derselbe Stand ohne die Abschnitte ergibt fuer das Spiel dieselbe Stadt (Q1 ohne Wirkung im Spiel)
        var ohne = (PyObjekt)Py.Kopie(p.AussenStand)!;
        var sd = Aussen.Stadtdaten(ohne);
        var rest = sd.Where(x => x.Key is not (QuestDaten.Abschnitt or QuestgeberDaten.Abschnitt)).ToList();
        sd.Clear();
        sd.AddRange(rest);
        Assert.Equal(StadtSzeneHash.Stadt(Szene(ohne)), StadtSzeneHash.Stadt(Szene(p.AussenStand)));
        var mitStadt = Fassung(p.AussenStand).StadtFuer(st.MitKarte(Inhalt));
        var ohneStadt = Fassung(ohne).StadtFuer(st.MitKarte(Inhalt));
        foreach (var tage in new long[] { 0, 10, 40, 99 })
        {
            var stand = new Spielstand([], Inhalt) { TageFertig = tage };
            Assert.Equal(PythonJson.Standard(ohneStadt.Orte(stand)), PythonJson.Standard(mitStadt.Orte(stand)));
        }
    }

    private static StadtFassung Fassung(PyObjekt a) => Aussen.Fassung(a, Daten.Spiel.Fassung, Daten.Spiel.Fassung.Innenraeume, 1, 0, "test", "", "");

    private static StadtSzene Szene(PyObjekt a)
    {
        var f = Fassung(a);
        return StadtSzene.Bauen(f.StadtFuer(f.MitKarte(Inhalt)), null, Daten.Spiel.Grafik);
    }

    [Fact]
    public void Bestand_NurUmhaengenUndKopieren_TexteBleiben()
    {
        var p = Stadt1();
        const string alt = "phishing-fahrkarten";
        Assert.False(p.QuestFeld(alt, "titel", "anders"));
        Assert.False(p.QuestNach(alt, "neu-1"));
        p.QuestUmhaengen(alt, "ulla");
        var q = p.Quests().Single(x => x.Id == alt);
        Assert.True(q.Umgehaengt);
        Assert.Equal("ulla", q.Questgeber);
        Assert.Equal("Verdächtige Mail im Service-Center", q.Titel);
        Assert.Empty(Rot(p));
        // zurueck zum alten Questgeber = Eintrag weg, Abschnitt weg
        p.QuestUmhaengen(alt, "petra");
        Assert.Null(SpielDaten.Get(Aussen.Stadtdaten(p.AussenStand), QuestDaten.Abschnitt));
        // kopieren: neue Quest mit denselben Texten
        var kopie = p.QuestKopieren(alt)!;
        var k = p.Quests().Single(x => x.Id == kopie);
        Assert.False(k.Bestand);
        Assert.Equal("Verdächtige Mail im Service-Center" + Texte.QuestKopieZusatz, k.Titel);
        Assert.Equal(PythonJson.Standard(SpielDaten.Get(p.Quests().Single(x => x.Id == alt).Daten, "optionen")),
            PythonJson.Standard(SpielDaten.Get(k.Daten, "optionen")));
        Assert.Empty(Rot(p));
        // Terminal laesst sich in Q1 nicht kopieren (2a)
        var terminal = p.Quests().First(x => x.Typ == "terminal").Id;
        Assert.Null(p.QuestKopieren(terminal));
    }

    [Fact]
    public void Regel_BestandGeaendert_Rot_UmhaengenGruen()
    {
        var p = Stadt1();
        p.QuestUmhaengen("phishing-fahrkarten", "ulla");
        Assert.Empty(Rot(p));
        p.AussenAendern("kaputt", a => Py.Setzen((PyObjekt)QuestDaten.Liste(a)[0]!, "titel", "Neu"));
        RotMit(p, "wurde geändert");
    }

    [Fact]
    public void Regel_DatenKaputt_KennungDoppelt_UmhaengenUnbekannt()
    {
        var p = Stadt1();
        var id = GuteAuswahl(p);
        Assert.Empty(Rot(p));
        p.AussenAendern("doppelt", a => QuestDaten.Liste(a).Add(Py.Kopie(QuestDaten.Liste(a)[0])));
        RotMit(p, "zweimal");
        p.Rueckgaengig();
        p.AussenAendern("Spiel-Kennung", a => Py.Setzen((PyObjekt)QuestDaten.Liste(a)[0]!, "id", "auftrag-vorlage-x"));
        p.Rueckgaengig();
        p.AussenAendern("unbekannt", a => QuestDaten.Liste(a).Add(new PyObjekt { { "id", "gibt-es-nicht" }, { "questgeber", "ulla" } }));
        RotMit(p, "die es nicht gibt");
        p.Rueckgaengig();
        p.AussenAendern("kaputt", a => QuestDaten.Liste(a).Add("text"));
        RotMit(p, "beschädigt");
        p.Rueckgaengig();
        Assert.Empty(Rot(p));
        _ = id;
    }

    // ---- Regeln je neuer Quest ---------------------------------------------------------------------------

    [Theory]
    [InlineData("titel", Texte.QuestFeldTitel)]
    [InlineData("frage", Texte.QuestFeldFrage)]
    public void Regel_PflichtfeldLeer_Rot(string feld, string name)
    {
        var p = Stadt1();
        var id = GuteAuswahl(p);
        Assert.Empty(Rot(p));
        p.QuestFeld(id, feld, "  ");
        RotMit(p, "„" + name + "“ ist leer", id);
    }

    [Theory]
    [InlineData("ticket", Texte.QuestFeldTicket)]
    [InlineData("hilfe", Texte.QuestFeldHilfe)]
    [InlineData("erklaerung", Texte.QuestFeldErklaerung)]
    public void Regel_ZeilenLeer_Rot(string feld, string name)
    {
        var p = Stadt1();
        var id = GuteAuswahl(p);
        p.QuestFeld(id, feld, QuestDaten.AusZeilen("\n \n"));
        RotMit(p, "„" + name + "“ ist leer", id);
        p.QuestFeld(id, feld, QuestDaten.AusZeilen("Text"));
        Assert.Empty(Rot(p));
    }

    [Fact]
    public void Regel_TextLang_Gelb()
    {
        var p = Stadt1();
        var id = GuteAuswahl(p);
        p.QuestFeld(id, "frage", new string('a', QuestDaten.TextHoechstens));
        Assert.DoesNotContain(Gelb(p), t => t.Contains("länger als", StringComparison.Ordinal));
        p.QuestFeld(id, "frage", new string('a', QuestDaten.TextHoechstens + 1));
        Assert.Contains(Gelb(p), t => t.Contains("länger als", StringComparison.Ordinal));
        Assert.Empty(Rot(p));
    }

    [Fact]
    public void Regel_OhneQuestgeber_Rot_UnbekannterQuestgeber_Rot()
    {
        var p = Stadt1();
        var id = GuteAuswahl(p);
        p.QuestUmhaengen(id, null);
        RotMit(p, "hat keinen Questgeber", id);
        p.QuestUmhaengen(id, "niemand");
        RotMit(p, "den es nicht gibt", id);
        p.QuestUmhaengen(id, "ulla");
        Assert.Empty(Rot(p));
    }

    [Fact]
    public void Regel_Typ_Thema_Belohnung_AbTag()
    {
        var p = Stadt1();
        var id = GuteAuswahl(p);
        p.AussenAendern("Typ", a => Py.Setzen(QuestDaten.NeueQuest(a, id)!, "typ", "terminal"));
        RotMit(p, "Typ", id);
        p.Rueckgaengig();
        p.QuestFeld(id, "cat", "kochen");
        RotMit(p, "kein bekanntes Thema", id);
        p.QuestFeld(id, "cat", "netzwerk");
        p.QuestFeld(id, "belohnung", new PyObjekt { { "geld", QuestDaten.GeldHoechstens + 1 }, { "reputation", 3L } });
        RotMit(p, "Geld 0 bis", id);
        p.QuestFeld(id, "belohnung", new PyObjekt { { "geld", QuestDaten.GeldHoechstens }, { "reputation", QuestDaten.RufHoechstens + 1 } });
        RotMit(p, "Geld 0 bis", id);
        p.QuestFeld(id, "belohnung", new PyObjekt { { "geld", QuestDaten.GeldHoechstens }, { "reputation", QuestDaten.RufHoechstens } });
        p.QuestFeld(id, "ab_tag", 0L);
        RotMit(p, "Frei ab Tag", id);
        p.QuestFeld(id, "ab_tag", QuestDaten.AbTagHoechstens);
        Assert.Empty(Rot(p));
    }

    [Fact]
    public void Regel_Auswahl_OptionenUndAntwort()
    {
        var p = Stadt1();
        var id = GuteAuswahl(p);
        p.QuestFeld(id, "optionen", new List<object?> { "DNS-Server im Router prüfen" });
        RotMit(p, "braucht 2 bis", id);
        p.QuestFeld(id, "optionen", new List<object?> { "DNS-Server im Router prüfen", "" });
        RotMit(p, "ist leer", id);
        p.QuestFeld(id, "optionen", new List<object?> { "DNS-Server im Router prüfen", "dns-server im router prüfen" });
        RotMit(p, "zwei gleiche Antworten", id);
        p.QuestFeld(id, "optionen", new List<object?> { "DNS-Server im Router prüfen", "Router NICHT neu starten" });
        RotMit(p, "„NICHT“", id);
        p.QuestFeld(id, "optionen", new List<object?> { "Router prüfen", "Kennwort ändern" });
        RotMit(p, "steht nicht unter den Antworten", id);
        p.QuestFeld(id, "antwort", "");
        RotMit(p, "ist leer", id);
        p.QuestFeld(id, "optionen", new List<object?> { "DNS-Server im Router prüfen", "Router nicht neu starten", "Kennwort ändern" });
        p.QuestFeld(id, "antwort", "DNS-Server im Router prüfen");
        Assert.Empty(Rot(p));
        // gelb: richtige Antwort ist die laengste (die Gegenprobe ist die Zeile davor: eine falsche ist gleich lang oder laenger)
        Assert.Contains(Gelb(p), t => t.Contains("ist die längste", StringComparison.Ordinal));
        p.QuestFeld(id, "optionen", new List<object?> { "DNS-Server im Router prüfen", "Router ganz ausschalten und eine Stunde warten" });
        Assert.DoesNotContain(Gelb(p), t => t.Contains("ist die längste", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel_Zuordnung_Paare()
    {
        var p = Stadt1();
        var id = GuteZuordnung(p);
        Assert.Empty(Rot(p));
        p.QuestFeld(id, "paare", new List<object?> { new List<object?> { "Netzwerk", "RJ45" } });
        RotMit(p, "Paare", id);
        p.QuestFeld(id, "paare", new List<object?> { new List<object?> { "Netzwerk", "RJ45" }, new List<object?> { "", "LC" } });
        RotMit(p, "ist leer", id);
        p.QuestFeld(id, "paare", new List<object?> { new List<object?> { "Netzwerk", "RJ45" }, new List<object?> { "netzwerk", "LC" } });
        RotMit(p, "zwei gleiche", id);
        p.QuestFeld(id, "paare", new List<object?> { new List<object?> { "Netzwerk", "RJ45" }, new List<object?> { "Glasfaser", "KEIN Kupfer" } });
        RotMit(p, "„KEIN“", id);
        p.QuestFeld(id, "paare", new List<object?> { new List<object?> { "Netzwerk", "RJ45" }, new List<object?> { "Glasfaser", "kein Kupfer" } });
        Assert.Empty(Rot(p));
    }

    [Fact]
    public void Typ_Wechseln_FelderDesAnderenTypsWeg()
    {
        var p = Stadt1();
        var id = GuteAuswahl(p);
        p.QuestFeld(id, "typ", "zuordnung");
        var d = QuestDaten.NeueQuest(p.AussenStand, id)!;
        Assert.False(Py.Hat(d, "optionen"));
        Assert.True(Py.Hat(d, "paare"));
        RotMit(p, "ist leer", id);
    }

    // ---- Verkettungen ----------------------------------------------------------------------------------

    [Fact]
    public void Regel_NachUnbekannt_Kreis_KetteLang()
    {
        var p = Stadt1();
        var a = GuteAuswahl(p);
        var b = GuteAuswahl(p);
        Assert.True(p.QuestNach(b, a));
        Assert.Empty(Rot(p));
        Assert.True(p.QuestNach(a, "gibt-es-nicht"));
        RotMit(p, "die es nicht gibt", a);
        Assert.True(p.QuestNach(a, b));
        var kreis = Rot(p).Where(t => t.Contains("im Kreis", StringComparison.Ordinal)).ToList();
        Assert.Single(kreis);
        p.QuestNach(a, null);
        Assert.Empty(Rot(p));
        // Kette: 11 Quests hintereinander = gelb, 10 nicht
        var vor = b;
        for (var i = 0; i < 8; i++)
        {
            var n = GuteAuswahl(p);
            p.QuestNach(n, vor);
            vor = n;
        }
        Assert.DoesNotContain(Gelb(p), t => t.Contains("Kette", StringComparison.Ordinal));
        var letzte = GuteAuswahl(p);
        p.QuestNach(letzte, vor);
        Assert.Contains(Gelb(p), t => t.Contains("Kette aus 11", StringComparison.Ordinal));
        Assert.Empty(Rot(p));
    }

    [Fact]
    public void Regel_FrueherFreiAlsOrt_Rot()
    {
        var p = Stadt1();
        // Wendt steht in Lindenau; der Ort kommt erst spaeter ins Spiel
        var g = Spielgeschichte.Aus(Inhalt, p.AussenStand);
        var lindenau = g.OrtJeStelle["lindenau"];
        var frei = g.FreiAbTag(lindenau)!.Value;
        Assert.True(frei > 1);
        var id = GuteAuswahl(p, "wendt");
        p.QuestFeld(id, "ab_tag", frei - 1);
        RotMit(p, "erst ab Tag " + frei, id);
        p.QuestFeld(id, "ab_tag", frei);
        Assert.Empty(Rot(p));
    }

    [Fact]
    public void Loeschen_NachfolgerVerlierenDieBedingung_EinSchritt()
    {
        var p = Stadt1();
        var a = GuteAuswahl(p);
        var b = GuteAuswahl(p);
        p.QuestNach(b, a);
        var schritte = p.Verlauf.Count;
        Assert.True(p.QuestLoeschen(a));
        Assert.Equal(schritte + 1, p.Verlauf.Count);
        Assert.Null(p.Quests().Single(q => q.Id == b).Nach);
        Assert.Empty(Rot(p));
        Assert.False(p.QuestLoeschen("phishing-fahrkarten"));
    }

    [Fact]
    public void JeQuestgeber_GruppenNachPerson_NeueQuestDabei()
    {
        var p = Stadt1();
        var id = GuteAuswahl(p, "ulla");
        var ohne = GuteZuordnung(p);
        p.QuestUmhaengen(ohne, null);
        p.QuestUmhaengen("phishing-fahrkarten", "ulla");
        var g = Spielgeschichte.Aus(Inhalt, p.AussenStand);
        Assert.Equal(152, g.Aufgaben.Count);
        Assert.True(g.Aufgaben.Single(e => e.Id == id).Neu);
        // umgehaengt: die Quest wandert ins Buero (Ulla ist Kollegin)
        Assert.Equal(g.OrtJeStelle[SpielDaten.OrtBuero], g.Aufgaben.Single(e => e.Id == "phishing-fahrkarten").Ort);
        var je = g.JeQuestgeber();
        Assert.Equal(Spielgeschichte.QgGruppe + "ulla", je.Aufgaben.Single(e => e.Id == id).Ort);
        Assert.Equal(Spielgeschichte.QgGruppe, je.Aufgaben.Single(e => e.Id == ohne).Ort);
        Assert.Equal(Texte.VerkettungOhne, je.QuestgeberName(Spielgeschichte.QgGruppe));
        Assert.Equal(1, je.FreiAbTag(Spielgeschichte.QgGruppe + "ulla"));
        Assert.Equal(g.Aufgaben.Count, je.Zahlen().Values.Sum());
        // die Zusammenhaenge bauen je Questgeber ohne Kreuzung
        var z = Zusammenhaenge.Bauen(je, je.QuestgeberName, new HashSet<string>(), ZusammenhaengeTests.Masse);
        Assert.Equal(je.Zahlen().Count, z.Kaesten.Count);
        Assert.Contains(z.Kaesten, k => k.Name == Texte.VerkettungOhne);

        // Weisung P12-187: je Questgeber hoechstens 4 Kaesten je Spalte, alle Kaesten bleiben; Gegenprobe ohne Grenze = eine
        // Spalte mit mehr als 4 (fast alle Personen ab Tag 1)
        var jeSpalte = Zusammenhaenge.Bauen(je, je.QuestgeberName, new HashSet<string>(), ZusammenhaengeTests.Masse,
            jeSpalte: Zusammenhaenge.QuestgeberJeSpalte, breiteZu: Zusammenhaenge.QuestgeberBreiteZu);
        Assert.Equal(je.Zahlen().Count, jeSpalte.Kaesten.Count);
        Assert.All(jeSpalte.Kaesten, k => Assert.Equal(Zusammenhaenge.QuestgeberBreiteZu, k.Koerper.B));
        Assert.All(jeSpalte.Kaesten.GroupBy(k => k.Spalte), s => Assert.True(s.Count() <= Zusammenhaenge.QuestgeberJeSpalte));
        Assert.Empty(jeSpalte.DurchKaesten());
        Assert.Contains(z.Kaesten.GroupBy(k => k.Spalte), s => s.Count() > Zusammenhaenge.QuestgeberJeSpalte);
        Assert.True(jeSpalte.Hoehe < z.Hoehe);
        // je Ort bleibt wie bisher (ohne Grenze)
        Assert.Equal(Zusammenhaenge.Spalten(g, p.OrtName), Zusammenhaenge.Spalten(g, p.OrtName, null));
    }

    /// <summary>Weisung P12-187 Punkt 4: Texte zu Personen sind neutral (keine Frau „an seinem Platz“); Gegenprobe alter Text.</summary>
    [Fact]
    public void Texte_PersonenNeutral()
    {
        static bool Maennlich(string t) => System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(sein|seine|seinem|seinen|seiner|seines)\b");
        var texte = typeof(Texte).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string) && (f.Name.StartsWith("Qg", StringComparison.Ordinal)
                || f.Name.StartsWith("Quest", StringComparison.Ordinal) || f.Name.StartsWith("Verkettung", StringComparison.Ordinal)))
            .Select(f => (f.Name, Text: (string)f.GetRawConstantValue()!)).ToList();
        Assert.NotEmpty(texte);
        Assert.DoesNotContain(texte, t => Maennlich(t.Text));
        Assert.Equal("am Platz im Spiel", Texte.QgAmAltenPlatz);
        Assert.True(Maennlich("an seinem Platz im Spiel"));
    }

    // ---- Questgeber ------------------------------------------------------------------------------------

    /// <summary>
    /// Ein freier, erreichbarer Platz im Moebelhaus: neben einer Person des Raums (die steht erreichbar), die erste Stelle
    /// ohne roten Fund.
    /// </summary>
    internal static QuestgeberDaten.Platz InnenPlatz(Projekt p, string ort = "moebelhaus")
    {
        var g = p.Grundriss(ort);
        var person = p.Personen(ort).Select(Wege.Platz).OfType<(double X, double Y)>().First();
        var probe = Projekt.Neu("probe", Daten.Spiel, Daten.NeueAblage());
        var qg = probe.QuestgeberNeu();
        foreach (var (dx, dy) in new[] { (1.0, 0.0), (-1.0, 0.0), (0.0, 1.0), (0.0, -1.0), (1.5, 1.5), (-1.5, 1.5), (1.5, -1.5), (-1.5, -1.5), (2.0, 0.0), (0.0, 2.0) })
        {
            var platz = new QuestgeberDaten.Platz(false, ort, 0, person.X + dx, person.Y + dy, 0);
            if (Bearbeitung.StueckBei(g, platz.X, platz.Y) is not null)
            {
                continue;
            }
            probe.QuestgeberPlatz(qg, platz);
            if (!probe.QuestFunde().Any(f => f.Rot))
            {
                return platz;
            }
        }
        throw new InvalidOperationException("kein Platz");
    }

    /// <summary>Platz draussen: dort, wo die Figur am Start-Ort steht (frei und erreichbar).</summary>
    internal static QuestgeberDaten.Platz DraussenPlatz(Projekt p, double dx = 0)
    {
        var (sz, start) = Projekt.QuestSzene(p.AussenStand, p.Basis, p.Innenraeume(), p.Daten)!.Value;
        var o = sz.Ort(start)!;
        return new QuestgeberDaten.Platz(true, "", 0, Math.Round(o.StehX + dx, 2), Math.Round(o.StehZ, 2), 0);
    }

    [Fact]
    public void EigenerQuestgeber_NameAussehenPlatz_RotUndGruen()
    {
        var p = Stadt1();
        var qg = p.QuestgeberNeu();
        Assert.Equal("qg-1", qg);
        RotMit(p, "noch keinen Platz");
        p.QuestgeberPlatz(qg, InnenPlatz(p));
        Assert.Empty(Rot(p));
        p.QuestgeberFeld(qg, "name", "");
        Assert.Contains(Rot(p), t => t == Texte.QgNameLeer);
        p.QuestgeberFeld(qg, "name", new string('x', QuestgeberDaten.NameHoechstens + 1));
        RotMit(p, "länger als");
        p.QuestgeberFeld(qg, "name", "ulla brandt");
        RotMit(p, "zweimal");
        p.QuestgeberFeld(qg, "name", "Café-Wirtin");
        p.QuestgeberAussehen(qg, "frisur", "gibt-es-nicht");
        RotMit(p, "Aussehen");
        p.QuestgeberAussehen(qg, "frisur", Lernplattform.Core.Spiel.Aussehen.Teile.First(t => t.Teil == "frisur").Wahl[1].Kennung);
        Assert.Empty(Rot(p));
        // Bestand: Name und Aussehen bleiben wie im Spiel
        Assert.False(p.QuestgeberFeld("ulla", "name", "Anders"));
        Assert.False(p.QuestgeberLoeschen("ulla"));
    }

    [Fact]
    public void Questgeber_Innen_AufStueck_AusserhalbDesRaums_OrtFehlt_Rot()
    {
        var p = Stadt1();
        var qg = p.QuestgeberNeu();
        var gut = InnenPlatz(p);
        p.QuestgeberPlatz(qg, gut);
        Assert.Empty(Rot(p));
        var g = p.Grundriss("moebelhaus");
        var stueck = Enumerable.Range(2, 200).Select(i => (X: (i % 20) * 0.5, Y: (i / 20) * 0.5 + 1)).First(xy => Bearbeitung.StueckBei(g, xy.X, xy.Y) is not null);
        p.QuestgeberPlatz(qg, gut with { X = stueck.X, Y = stueck.Y });
        RotMit(p, "Möbelstück");
        p.QuestgeberPlatz(qg, gut with { X = Py.F(g["breite"]) + 2 });
        RotMit(p, "außerhalb des Grundrisses");
        p.QuestgeberPlatz(qg, gut with { Ort = "gibt-es-nicht" });
        RotMit(p, "den es in dieser Stadt nicht gibt");
        p.QuestgeberPlatz(qg, gut with { Blick = 45 });
        RotMit(p, "schaut schräg");
        p.QuestgeberPlatz(qg, gut);
        Assert.Empty(Rot(p));
    }

    [Fact]
    public void Questgeber_Draussen_FreiErreichbar_HoechstensZwoelf()
    {
        var p = Stadt1();
        var platz = DraussenPlatz(p);
        var ids = new List<string>();
        for (var i = 0; i < QuestgeberDaten.DraussenHoechstens; i++)
        {
            var qg = p.QuestgeberNeu();
            p.QuestgeberFeld(qg, "name", "Person " + i);
            p.QuestgeberPlatz(qg, platz);
            ids.Add(qg);
        }
        Assert.Empty(Rot(p));
        Assert.Equal(QuestgeberDaten.DraussenHoechstens, QuestgeberDaten.Draussen(Inhalt, p.AussenStand));
        // Bestand draussen: der 13. ist zu viel
        p.QuestgeberPlatz("ulla", platz);
        RotMit(p, "13 Questgeber stehen draußen");
        p.QuestgeberPlatzZurueck("ulla");
        Assert.Empty(Rot(p));
        // auf einem Haus: nicht frei
        var (sz, _) = Projekt.QuestSzene(p.AussenStand, p.Basis, p.Innenraeume(), p.Daten)!.Value;
        var besetzt = Enumerable.Range(0, 400).Select(i => (X: platz.X + ((i % 20) - 10) * 0.25, Y: platz.Y + ((i / 20) - 10) * 0.25))
            .First(xy => xy.X > 0 && xy.Y > 0 && !sz.Laufraster.KannStehen(xy.X, xy.Y));
        p.QuestgeberPlatz(ids[0], platz with { X = besetzt.X, Y = besetzt.Y });
        RotMit(p, "steht auf einem Haus, Wasser");
        p.QuestgeberPlatz(ids[0], platz with { X = -1 });
        RotMit(p, "außerhalb der Karte");
        p.QuestgeberPlatz(ids[0], platz);
        Assert.Empty(Rot(p));
    }

    [Fact]
    public void Questgeber_Draussen_NichtErreichbar_Rot()
    {
        // vier Teiche als Ring um ein freies Stueck Wiese: die Mitte ist frei, aber nicht zu erreichen; Gegenprobe: am Start-Ort
        var p = Stadt1();
        var (sz, _) = Projekt.QuestSzene(p.AussenStand, p.Basis, p.Innenraeume(), p.Daten)!.Value;
        var l = sz.Laufraster;
        (double X, double Y)? mitte = null;
        for (var y = 3.0; y < sz.Tiefe - 3 && mitte is null; y += 0.5)
        {
            for (var x = 3.0; x < sz.Breite - 3; x += 0.5)
            {
                var frei = true;
                for (var dy = -2.5; dy <= 2.5 && frei; dy += 0.25)
                {
                    for (var dx = -2.5; dx <= 2.5 && frei; dx += 0.25)
                    {
                        frei = l.KannStehen(x + dx, y + dy);
                    }
                }
                if (frei)
                {
                    mitte = (x, y);
                    break;
                }
            }
        }
        Assert.NotNull(mitte);
        var (mx, my) = mitte.Value;
        static List<object?> Rechteck(double x1, double y1, double x2, double y2) =>
        [
            new List<object?> { x1, y1 }, new List<object?> { x2, y1 }, new List<object?> { x2, y2 }, new List<object?> { x1, y2 },
        ];
        p.AussenAendern("Teiche", a => Py.Setzen(Aussen.Stadtdaten(a), "wasser", new PyObjekt
        {
            {
                "teiche", new List<object?>
                {
                    new PyObjekt { { "punkte", Rechteck(mx - 2.4, my - 2.4, mx + 2.4, my - 1.2) } },
                    new PyObjekt { { "punkte", Rechteck(mx - 2.4, my + 1.2, mx + 2.4, my + 2.4) } },
                    new PyObjekt { { "punkte", Rechteck(mx - 2.4, my - 2.4, mx - 1.2, my + 2.4) } },
                    new PyObjekt { { "punkte", Rechteck(mx + 1.2, my - 2.4, mx + 2.4, my + 2.4) } },
                }
            },
        }));
        var qg = p.QuestgeberNeu();
        p.QuestgeberPlatz(qg, new QuestgeberDaten.Platz(true, "", 0, mx, my, 0));
        RotMit(p, "zu Fuß nicht erreichbar");
        p.QuestgeberPlatz(qg, DraussenPlatz(p));
        Assert.DoesNotContain(Rot(p), t => t.Contains("Questgeber", StringComparison.Ordinal));
    }

    [Fact]
    public void Questgeber_Loeschen_QuestsBleibenOhneQuestgeber_Rot()
    {
        var p = Stadt1();
        var qg = p.QuestgeberNeu();
        p.QuestgeberPlatz(qg, InnenPlatz(p));
        var id = GuteAuswahl(p, qg);
        p.QuestUmhaengen("phishing-fahrkarten", qg);
        Assert.Empty(Rot(p));
        var schritte = p.Verlauf.Count;
        Assert.True(p.QuestgeberLoeschen(qg));
        Assert.Equal(schritte + 1, p.Verlauf.Count);
        RotMit(p, "hat keinen Questgeber", id);
        RotMit(p, "hat keinen Questgeber", "phishing-fahrkarten");
        p.Rueckgaengig();
        Assert.Empty(Rot(p));
    }

    [Fact]
    public void Questgeber_Verschieben_QuestsWandernMit()
    {
        var p = Stadt1();
        p.QuestgeberPlatz("petra", InnenPlatz(p));
        var g = Spielgeschichte.Aus(Inhalt, p.AussenStand);
        Assert.All(g.Aufgaben.Where(e => e.Questgeber == "petra" && !Py.Hat(QuestDaten.BestandQuests(Inhalt).First(a => Py.Gleich(a["id"], e.Id)), "ort")),
            e => Assert.Equal("moebelhaus", e.Ort));
        Assert.Empty(Rot(p));
        p.QuestgeberPlatzZurueck("petra");
        Assert.Null(SpielDaten.Get(Aussen.Stadtdaten(p.AussenStand), QuestgeberDaten.Abschnitt));
    }

    [Fact]
    public void Grenzen_QuestgeberDraussen_ZaehlenMit()
    {
        var p = Stadt1();
        Assert.Equal(0, GrenzenLive.QuestgeberDreieckeDraussen(p.Stand(), p.Daten));
        var qg = p.QuestgeberNeu();
        p.QuestgeberPlatz(qg, DraussenPlatz(p));
        var je = GrenzenLive.QuestgeberDreieckeDraussen(p.Stand(), p.Daten);
        Assert.True(je > 100);
        // drinnen zaehlt nicht draussen (Gegenprobe)
        p.QuestgeberPlatz(qg, InnenPlatz(p));
        Assert.Equal(0, GrenzenLive.QuestgeberDreieckeDraussen(p.Stand(), p.Daten));
    }
}
