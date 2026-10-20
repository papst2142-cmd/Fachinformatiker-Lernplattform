using System.Globalization;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 1, K6/K8 Teil "Beschriftungen": Navigation, Kopfleiste, Sammelseite
/// "Lernen", Optionen-Bereiche und die Texte der Bereiche, die Phase 1 baut,
/// exakt wie Python (Vergleichswerte G9, tests/Vergleich/beschriftungen.json).
/// </summary>
public class BeschriftungenTests
{
    private static readonly JsonElement G9 = Lade();

    private static JsonElement Lade()
    {
        using var f = File.OpenRead(Pfade.Vergleich("beschriftungen.json"));
        return JsonDocument.Parse(f).RootElement.Clone();
    }

    private static string S(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private static string? SN(JsonElement e, string name) =>
        e.GetProperty(name).ValueKind == JsonValueKind.Null ? null : e.GetProperty(name).GetString();

    // Vergleich ueber eine Hilfsmethode: Python-Wert (soll) zuerst, C#-Konstante (ist) danach
    private static void Gleich(string? soll, string? ist) => Assert.Equal(soll, ist);

    private static void GleichZahl(int soll, int ist) => Assert.Equal(soll, ist);

    private static List<string> Liste(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetString()!)];

    private static List<string> Liste(JsonElement e, string name) => Liste(e.GetProperty(name));

    [Fact]
    public void NavigationPcWiePython()
    {
        var soll = G9.GetProperty("navigation_pc").EnumerateArray().ToList();
        Assert.Equal(13, soll.Count);
        Assert.Equal(soll.Count, Navigation.Pc.Count);
        for (var i = 0; i < soll.Count; i++)
        {
            Vergleiche(soll[i], Navigation.Pc[i]);
            var unter = soll[i].GetProperty("unterpunkte").EnumerateArray().ToList();
            Assert.Equal(unter.Count, Navigation.Pc[i].Unterpunkte.Count);
            for (var j = 0; j < unter.Count; j++)
            {
                Vergleiche(unter[j], Navigation.Pc[i].Unterpunkte[j]);
                Assert.Empty(Navigation.Pc[i].Unterpunkte[j].Unterpunkte);
            }
        }
        // Karteikarten: ein Unterpunkt je Fachbereich in fester Reihenfolge
        Assert.Equal(Fachbereiche.Alle, Navigation.Pc.Single(n => n.Id == "cards").Unterpunkte.Select(u => u.Id));

        static void Vergleiche(JsonElement soll, NavEintrag ist)
        {
            Gleich(S(soll, "id"), ist.Id);
            Gleich(S(soll, "zeichen"), ist.Zeichen);
            Gleich(S(soll, "symbol"), ist.Symbol);
            Gleich(S(soll, "titel"), ist.Titel);
            Assert.Null(ist.SymbolAktiv);
        }
    }

    [Fact]
    public void SeitenleisteUndKopfleistePcWiePython()
    {
        var seite = G9.GetProperty("seitenleiste_pc");
        Gleich(S(seite, "logo"), Navigation.Logo);
        Assert.Equal(Liste(seite, "texte"),
            [Navigation.Logo, Navigation.LogoZusatz, Navigation.Fusszeile, Navigation.Menue, Navigation.Lernstatus]);
        var kopf = G9.GetProperty("kopfleiste_pc");
        Gleich(S(kopf, "suche"), Navigation.SuchePc);
        Assert.Equal(Kopf(kopf), Navigation.KopfPc);
    }

    private static List<Kopftitel> Kopf(JsonElement kopf) =>
        [.. kopf.GetProperty("titel").EnumerateArray().Select(t => new Kopftitel(t[0].GetString()!, t[1].GetString()!, t[2].GetString()!))];

    [Fact]
    public void NavigationHandyWiePython()
    {
        var soll = G9.GetProperty("navigation_handy").EnumerateArray().ToList();
        Assert.Equal(6, soll.Count);
        Assert.Equal(soll.Count, Navigation.Handy.Count);
        for (var i = 0; i < soll.Count; i++)
        {
            var ist = Navigation.Handy[i];
            Gleich(S(soll[i], "id"), ist.Id);
            Gleich(S(soll[i], "symbol"), ist.Symbol);
            Gleich(S(soll[i], "symbol_aktiv"), ist.SymbolAktiv);
            Gleich(S(soll[i], "titel"), ist.Titel);
            Assert.Null(ist.Zeichen);
            Assert.Empty(ist.Unterpunkte);
        }
        var kopf = G9.GetProperty("kopfleiste_handy");
        Gleich(S(kopf, "suche"), Navigation.SucheHandy);
        Assert.Equal(Kopf(kopf), Navigation.KopfHandy);
        var knoepfe = kopf.GetProperty("knoepfe").EnumerateArray().ToList();
        Assert.Equal(knoepfe.Count, Navigation.KopfKnoepfeHandy.Count);
        for (var i = 0; i < knoepfe.Count; i++)
        {
            Gleich(S(knoepfe[i], "symbol"), Navigation.KopfKnoepfeHandy[i].Symbol);
            Gleich(S(knoepfe[i], "titel"), Navigation.KopfKnoepfeHandy[i].Titel);
        }
        // Hilfe heisst an PC und Handy gleich
        Assert.Equal(Navigation.Hilfe, Navigation.Pc.Single(n => n.Id == "help").Titel);
    }

    [Fact]
    public void LernenSammelseiteWiePython()
    {
        var lernen = G9.GetProperty("lernen");
        var kopf = Liste(lernen, "kopf");
        Assert.Equal(kopf, [Navigation.LernenKopf.Bereich, Navigation.LernenKopf.Seite]);
        Gleich(S(lernen, "note_kurz"), Navigation.NoteKurz);
        var inhalte = Lerninhalte.Laden(Pfade.Inhalte);
        var anzahl = lernen.GetProperty("anzahlen");
        Assert.Equal(anzahl.GetProperty("karteikarten").GetInt32(), inhalte.Karteikarten.Count);
        Assert.Equal(anzahl.GetProperty("fachbereiche").GetInt32(), Fachbereiche.Alle.Count);
        Assert.Equal(anzahl.GetProperty("quizfragen").GetInt32(), inhalte.Quizfragen.Count);
        Assert.Equal(anzahl.GetProperty("ap1").GetInt32(), inhalte.Ap1Szenarien.Count);
        Assert.Equal(anzahl.GetProperty("ap2").GetInt32(), inhalte.Ap2Szenarien.Count);
        Assert.Equal(anzahl.GetProperty("testprojekte").GetInt32(), inhalte.Testprojekte.Count);

        var soll = lernen.GetProperty("eintraege").EnumerateArray().ToList();
        Assert.Equal(7, soll.Count);
        Assert.Equal(soll.Count, Navigation.Lernen.Count);
        for (var i = 0; i < soll.Count; i++)
        {
            var ist = Navigation.Lernen[i];
            Gleich(S(soll[i], "id"), ist.Id);
            Gleich(S(soll[i], "symbol"), ist.Symbol);
            Gleich(S(soll[i], "titel"), ist.Titel);
            Gleich(S(soll[i], "muster"), ist.Muster);
            Assert.Equal(Liste(soll[i], "farben"), ist.Farben);
            Gleich(S(soll[i], "text"), Navigation.LernenUntertitel(ist, inhalte));
        }
    }

    [Fact]
    public void OptionenBereicheWiePython()
    {
        foreach (var pc in new[] { true, false })
        {
            var soll = G9.GetProperty("optionen").GetProperty(pc ? "bereiche_pc" : "bereiche_handy").EnumerateArray().ToList();
            var ist = Optionen.BereicheFuer(pc);
            Assert.Equal(pc ? 12 : 11, soll.Count);
            Assert.Equal(soll.Count, ist.Count);
            for (var i = 0; i < soll.Count; i++)
            {
                var b = ist[i];
                var text = (pc ? "PC " : "Handy ") + b.Id;
                Assert.True(S(soll[i], "id") == b.Id, text);
                Assert.True(S(soll[i], "titel") == b.Titel, text);
                Assert.True(SN(soll[i], "untertitel") == b.Untertitel(pc), text);
                Assert.True(soll[i].GetProperty("nur_pc").GetBoolean() == b.NurPc, text);
                Assert.True(soll[i].GetProperty("offen").GetBoolean() == b.Offen, text);
                Assert.True(soll[i].GetProperty("aufgeklappt").GetBoolean() == b.Aufgeklappt, text);
                var abschnitte = soll[i].GetProperty("abschnitte").EnumerateArray()
                    .Select(a => new OptionenAbschnitt(S(a, "id"), S(a, "titel"), a.GetProperty("nur_pc").GetBoolean())).ToList();
                Assert.Equal(abschnitte, b.AbschnitteFuer(pc));
                Assert.Equal(Liste(soll[i], "stichwoerter"), b.StichwoerterFuer(pc));
            }
        }
        var o = G9.GetProperty("optionen");
        Gleich(S(o, "speichern_fehlgeschlagen_titel"), Optionen.SpeichernFehlgeschlagenTitel);
        Gleich(S(o, "speichern_fehlgeschlagen"), Optionen.SpeichernFehlgeschlagen);
        Gleich(S(o, "suche_art"), Optionen.SucheArt);
        Assert.Equal(Liste(o, "updates_pc"), [Optionen.UpdatesSuchen, Optionen.UpdatesAutomatisch]);
        Assert.Equal(Liste(o, "updates_handy"), [Optionen.UpdatesSuchen, Optionen.UpdatesAutomatisch]);
        Assert.Equal(Optionen.UeberTitel, Optionen.Bereich(Optionen.Ueber).Titel);
        Assert.Throws<KeyNotFoundException>(() => Optionen.Bereich("gibt_es_nicht"));
    }

    [Fact]
    public void KlapptexteWiePython()
    {
        var k = G9.GetProperty("klappen");
        Gleich(S(k, "auf"), KlappTexte.Aufklappen);
        Gleich(S(k, "zu"), KlappTexte.Zuklappen);
        Gleich(S(k, "zustand_zu"), KlappTexte.ZustandZu);
        Gleich(S(k, "zustand_auf"), KlappTexte.ZustandAuf);
        Gleich(S(k, "lernen_auf"), KlappTexte.Aufklappen);
        Gleich(S(k, "lernen_zu"), KlappTexte.Zuklappen);
        foreach (var b in k.GetProperty("beispiele").EnumerateArray())
        {
            Assert.Equal(b[2].GetString(), KlappTexte.Name(b[0].GetString()!, b[1].GetBoolean()));
        }
    }

    [Fact]
    public void DarstellungUndFarbenWiePython()
    {
        var d = G9.GetProperty("darstellung");
        Assert.Equal(d.GetProperty("modi").EnumerateArray().Select(m => (m[0].GetString()!, m[1].GetString()!)), DarstellungTexte.Modi);
        Assert.Equal(DarstellungTexte.Modi.Select(m => m.Id), Einstellungen.Darstellungen);
        Assert.Equal(Liste(d, "pc"), [Beschriftungen.Gross(DarstellungTexte.Beschriftung)]);
        Assert.Equal(Liste(d, "handy"), [SchriftTexte.Titel, Optionen.FarbenTitel, DarstellungTexte.Beschriftung]);

        var f = G9.GetProperty("farbwelten");
        Assert.Equal(f.GetProperty("grundfarben").EnumerateArray().Select(g => (g[0].GetString(), g[1].GetString())),
            Farbwelt.Grundfarben.Select(g => ((string?)g.Id, (string?)g.Name)));
        Assert.Equal(f.GetProperty("hintergruende").EnumerateArray().Select(h => (h[0].GetString(), h[1].GetString(), h[2].GetString())),
            Farbwelt.Hintergruende.Select(h => ((string?)h.Id, (string?)h.NameDunkel, (string?)h.NameHell)));
        var schwarz = Farbwelt.Hintergruende.Single(h => h.Id == "schwarz");
        Assert.Equal("Weiß", FarbenTexte.HintergrundName(schwarz, Farbwelt.Hell));
        Assert.Equal("Schwarz", FarbenTexte.HintergrundName(schwarz, Farbwelt.Dunkel));
        Gleich(S(f, "vorlagen_titel"), FarbenTexte.VorlagenTitel);
        Gleich(S(f, "vorlagen_hinweis"), FarbenTexte.VorlagenHinweis);
        Gleich(S(f, "vorlagen_text"), FarbenTexte.VorlagenText);
        Gleich(S(f, "farben_titel"), Optionen.FarbenTitel);
        Gleich(S(f, "optik_titel"), Optionen.OptikTitel);
        Gleich(S(f, "beschaeftigt_titel"), FarbenTexte.BeschaeftigtTitel);
        Gleich(S(f, "beschaeftigt_text"), FarbenTexte.BeschaeftigtText);
        Gleich(S(f, "laden_text"), FarbenTexte.Laden);
        Assert.Equal(Liste(f, "pc"),
            [Beschriftungen.Gross(FarbenTexte.Grundfarbe), Beschriftungen.Gross(FarbenTexte.Hintergrund), FarbenTexte.VorlagenText]);
        Assert.Equal(Liste(f, "handy"), [FarbenTexte.Grundfarbe, FarbenTexte.Hintergrund, FarbenTexte.VorlagenText]);
    }

    [Fact]
    public void SchriftstufenWiePython()
    {
        var s = G9.GetProperty("schrift");
        var stufen = s.GetProperty("stufen").EnumerateArray()
            .Select(x => new Schriftstufe(x[0].GetString()!, x[1].GetString()!, x[2].GetDouble())).ToList();
        Assert.Equal(stufen, SchriftTexte.Stufen);
        // Ids muessen zu den gespeicherten Einstellungen passen (Daten.Einstellungen)
        Assert.Equal(Einstellungen.Schriftgroessen, SchriftTexte.Stufen.Select(x => x.Id));
        Assert.Equal(Einstellungen.SchriftNormal, SchriftTexte.Stufe("unbekannt").Id);
        Assert.Equal(Einstellungen.SchriftNormal, SchriftTexte.Stufe(null).Id);
        Assert.Equal(2.0, SchriftTexte.Stufe("riesig").Faktor);
        Gleich(S(s, "titel"), SchriftTexte.Titel);
        Gleich(S(s, "untertitel"), SchriftTexte.Untertitel);
        Gleich(S(s, "hinweis"), SchriftTexte.Hinweis);
        Gleich(S(s, "beschaeftigt_titel"), SchriftTexte.BeschaeftigtTitel);
        Gleich(S(s, "beschaeftigt_text"), SchriftTexte.BeschaeftigtText);
        Assert.Equal(Liste(s, "pc"), [SchriftTexte.Hinweis]);
        // Angezeigt wird nur der erste Satz des Python-Hinweises (Weisung P1-3, Frage 2)
        Assert.StartsWith(SchriftTexte.HinweisKurz + " ", S(s, "hinweis"), StringComparison.Ordinal);
        Assert.EndsWith(".", SchriftTexte.HinweisKurz, StringComparison.Ordinal);
    }

    [Fact]
    public void TageszielWiePython()
    {
        var t = G9.GetProperty("tagesziel");
        GleichZahl(t.GetProperty("vorgabe").GetInt32(), Einstellungen.ZielVorgabe);
        GleichZahl(t.GetProperty("min").GetInt32(), Einstellungen.ZielMin);
        GleichZahl(t.GetProperty("max").GetInt32(), Einstellungen.ZielMax);
        GleichZahl(t.GetProperty("schritt").GetInt32(), TageszielTexte.Schritt);
        Gleich(S(t, "erinnerung_vorgabe"), Einstellungen.ErinnerungVorgabe);
        Assert.Equal(t.GetProperty("schalter_pc").EnumerateArray().Select(x => (x[0].GetString()!, x[1].GetString()!)),
            TageszielTexte.Schalter);
        Assert.Equal(Liste(t, "schalter_handy"), TageszielTexte.Schalter.Select(x => x.Text));
        Assert.Equal(Liste(t, "pc"), [TageszielTexte.AufgabenProTag, TageszielTexte.ErinnerungUm, TageszielTexte.Hinweis(true)]);
        Assert.Equal(Liste(t, "handy"), [TageszielTexte.AufgabenProTag, TageszielTexte.ErinnerungUm, TageszielTexte.Hinweis(false)]);
        // Angezeigt wird nur der erste Satz, PC und Handy gleich (Weisung P1-3, Frage 3)
        Assert.StartsWith(TageszielTexte.HinweisGezaehlt + " ", Liste(t, "pc")[2], StringComparison.Ordinal);
        Assert.StartsWith(TageszielTexte.HinweisGezaehlt + " ", Liste(t, "handy")[2], StringComparison.Ordinal);
        Assert.Single(TageszielTexte.HinweisGezaehlt.Split(". "));
        Gleich(S(t, "erinnerung_hinweis_handy"), TageszielTexte.ErinnerungPlatzhalter);
    }

    [Fact]
    public void DiagnoseWiePython()
    {
        var d = G9.GetProperty("diagnose");
        Gleich(S(d, "titel"), DiagnoseTexte.Titel);
        Gleich(S(d, "untertitel"), DiagnoseTexte.Untertitel);
        Gleich(S(d, "hilfe"), DiagnoseTexte.Hilfe);
        Gleich(S(d, "kopieren"), DiagnoseTexte.Kopieren);
        Gleich(S(d, "speichern"), DiagnoseTexte.Speichern);
        Gleich(S(d, "ordner"), DiagnoseTexte.OrdnerOeffnen);
        Gleich(S(d, "kopiert"), DiagnoseTexte.Kopiert);
        Gleich(S(d, "ordner_fehler"), DiagnoseTexte.OrdnerFehler);
        Gleich(S(d, "dateityp"), DiagnoseTexte.Dateityp);
        Gleich(S(d, "bereich_titel"), Optionen.DiagnoseTitel);
        Gleich(S(d, "bereich_untertitel"), Optionen.DiagnoseUntertitel);
        // Fehlerprotokoll-Texte stehen in Diagnose.Fehlerprotokoll (G10); hier nur gleich wie Python
        Gleich(S(d, "kein_protokoll"), Fehlerprotokoll.KeinProtokoll);
        Assert.Equal(Beschriftungen.Einsetzen(S(d, "gekuerzt"), 128),
            string.Format(CultureInfo.InvariantCulture, Fehlerprotokoll.Gekuerzt, 128));
        Assert.Equal(Liste(d, "pc"), DiagnoseTexte.Knoepfe(true));
        Assert.Equal(Liste(d, "handy"), DiagnoseTexte.Knoepfe(false));
    }

    [Fact]
    public void UeberDasProgrammWiePython()
    {
        var u = G9.GetProperty("ueber");
        Gleich(S(u, "titel"), Optionen.UeberTitel);
        Gleich(S(u, "programmname"), UeberTexte.Programmname);
        Gleich(S(u, "copyright"), UeberTexte.Copyright);
        Gleich(S(u, "hinweis"), UeberTexte.Hinweis);
        Gleich(S(u, "claude"), UeberTexte.ClaudeHinweis);
        Gleich(S(u, "plattform_pc"), UeberTexte.PlattformPython);
        Gleich(S(u, "plattform_handy"), UeberTexte.PlattformPythonHandy);
        var version = S(u, "beispielversion");
        Gleich(S(u, "text_pc"), UeberTexte.Text(version, UeberTexte.PlattformPython));
        Gleich(S(u, "text_handy"), UeberTexte.Text(version, UeberTexte.PlattformPythonHandy));
        Assert.Equal(Liste(u, "knoepfe"), [UeberTexte.KnopfLizenz, UeberTexte.KnopfFremd]);
        Assert.Equal(Liste(u, "fenster"), [UeberTexte.TitelLizenz, UeberTexte.TitelFremd]);
    }

    [Fact]
    public void GrossschreibungZahlwoerterTausenderWiePython()
    {
        var gross = G9.GetProperty("gross").EnumerateArray().ToList();
        Assert.True(gross.Count >= 30);
        Assert.Contains(gross, g => g[0].GetString()!.Contains('ß'));
        foreach (var g in gross)
        {
            Assert.Equal(g[1].GetString(), Beschriftungen.Gross(g[0].GetString()!));
        }
        foreach (var z in G9.GetProperty("zahlwoerter").EnumerateArray())
        {
            Assert.Equal(z[1].GetString(), Beschriftungen.Zahlwort(z[0].GetInt32()));
        }
        foreach (var z in G9.GetProperty("tausender").EnumerateArray())
        {
            Assert.Equal(z[1].GetString(), Beschriftungen.MitPunkten(z[0].GetInt64()));
        }
    }

    [Fact]
    public void EinsetzenWiePythonProzent()
    {
        Assert.Equal("installierte Version A 0.1.0", Beschriftungen.Einsetzen(Optionen.UpdatesUntertitel, "A 0.1.0"));
        Assert.Equal("1234 Aufgaben, 5 %", Beschriftungen.Einsetzen("%d Aufgaben, %s %%", 1234, 5));
        Assert.Equal("ohne", Beschriftungen.Einsetzen("ohne"));
        Assert.Throws<FormatException>(() => Beschriftungen.Einsetzen("%s und %s", "a"));
        Assert.Throws<FormatException>(() => Beschriftungen.Einsetzen("%s", "a", "b"));
        Assert.Throws<FormatException>(() => Beschriftungen.Einsetzen("%x", 1));
    }

    /// <summary>Vorgabe Nico (gibt es in Python nicht): genau dieser Wortlaut, ASCII ohne Umlaut.</summary>
    [Fact]
    public void PlatzhalterGenauNachVorgabe()
    {
        Assert.Equal("Kommt in einer spaeteren Ausbaustufe", Beschriftungen.Platzhalter);
        Assert.Equal(36, Beschriftungen.Platzhalter.Length);
        Assert.All(Beschriftungen.Platzhalter, c => Assert.True(c < 128));
    }
}
