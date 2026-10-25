using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Hilfe;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Suche;
using Lernplattform.Core.Texte;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W4: Hilfe, Rundgang und Einrichten gegen G35 (tests/Vergleich/hilfe.json, erzeugt von
/// werkzeuge/vergleich/erzeuge_hilfe.py aus Python 0.63.2; die CI vergleicht mit dem unabhaengigen
/// pruefe_hilfe.py). Jede Abweichung C#/Python steht in der festen Tabelle (Paare, Plan 2.7).
/// </summary>
public class HilfeTests
{
    private static readonly JsonElement G35 = VergleichsHilfe.Lade("hilfe.json");
    private static readonly Lazy<RahmenplanGewicht> Plan = new(() => new RahmenplanGewicht(Lerninhalte.Laden(Pfade.Inhalte)));

    /// <summary>Name der Wortliste -> C#-Text (k: Wortliste A, r: Wortliste B).</summary>
    internal static readonly IReadOnlyDictionary<string, string> Texte = new Dictionary<string, string>
    {
        ["k:TOUR_TITLE"] = RundgangTexte.Titel,
        ["k:TOUR_STEP"] = RundgangTexte.Schritt,
        ["k:TOUR_SKIP"] = RundgangTexte.Ueberspringen,
        ["k:TOUR_BACK"] = RundgangTexte.Zurueck,
        ["k:TOUR_NEXT"] = RundgangTexte.Weiter,
        ["k:TOUR_FINISH"] = RundgangTexte.Fertig,
        ["k:TOUR_KEYS"] = HilfeTexte.TourTasten,
        ["k:SETUP_NAME"] = RundgangTexte.Name,
        ["k:SETUP_NAME_HINT"] = RundgangTexte.NameZusatz,
        ["k:SETUP_PLAN"] = RundgangTexte.Plan,
        ["k:SETUP_PLAN_HINT"] = RundgangTexte.PlanHinweis,
        ["k:SETUP_DATES"] = RundgangTexte.Termine,
        ["k:SETUP_DATES_HINT"] = HilfeTexte.TermineHinweis,
        ["k:GREETING"] = DashboardTexte.Begruessung,
        ["k:NAME_OPTION_HINT"] = HilfeTexte.NameHinweis,
        ["k:OPTIONS_TITLE"] = RundgangTexte.OptionenTitel,
        ["k:OPTIONS_SUBTITLE"] = RundgangTexte.OptionenUntertitel,
        ["k:OPTIONS_TEXT"] = RundgangTexte.OptionenText,
        ["k:BTN_TOUR"] = RundgangTexte.RundgangStarten,
        ["k:BTN_HELP"] = RundgangTexte.HilfeOeffnen,
        ["k:HELP_TITLE"] = RundgangTexte.HilfeTitel,
        ["k:HELP_SUBTITLE"] = RundgangTexte.HilfeUntertitel,
        ["k:HELP_INTRO"] = HilfeTexte.Einleitung,
        ["k:SEARCH_KIND"] = RundgangTexte.SucheArt,
        ["k:UPDATE_PROTECTION_HINT"] = RundgangTexte.UpdateSchutzHinweis,
        ["r:OPTIONS_TITLE"] = RahmenplanTexte.Titel,
        ["r:OPTIONS_SUBTITLE"] = RahmenplanTexte.Untertitel,
        ["r:WEIGHT_OPTION_TEXT"] = RahmenplanTexte.Gewichtung,
        ["r:WEIGHT_OPTION_HINT"] = RahmenplanTexte.GewichtungHinweis,
        ["r:FIELD_OPTION_TEXT"] = RahmenplanTexte.Lernfeld,
        ["r:FIELD_OPTION_OFF"] = RahmenplanTexte.LernfeldAus,
        ["r:AP1_DATE_TEXT"] = RahmenplanTexte.TerminAp1,
        ["r:AP2_DATE_TEXT"] = RahmenplanTexte.TerminAp2,
        ["r:DATE_HINT"] = RahmenplanTexte.DatumFormat,
        ["r:DATE_INVALID"] = RahmenplanTexte.DatumUngueltig,
        ["r:NUR_DIESES_GERAET"] = RahmenplanTexte.NurDiesesGeraet,
        ["r:SECTION_OPTION_TEXT.B"] = RahmenplanTexte.AbschnittText("B"),
        ["r:SECTION_OPTION_TEXT.D"] = RahmenplanTexte.AbschnittText("D"),
        ["r:SECTION_OPTION_TEXT.E"] = RahmenplanTexte.AbschnittText("E"),
    };

    /// <summary>Python-Text mit eingesetzten Paaren der festen Tabelle (jedes "alt" genau einmal).</summary>
    private static string MitPaaren(string thema, string text)
    {
        foreach (var paar in G35.GetProperty("paare").EnumerateArray().Where(p => p.GetProperty("thema").GetString() == thema))
        {
            var alt = paar.GetProperty("alt").GetString()!;
            Assert.Equal(1, (text.Length - text.Replace(alt, "", StringComparison.Ordinal).Length) / alt.Length);
            text = text.Replace(alt, paar.GetProperty("neu").GetString(), StringComparison.Ordinal);
        }
        return text;
    }

    [Fact]
    public void TexteWiePython()
    {
        var texte = G35.GetProperty("texte");
        var namen = new List<string>();
        foreach (var teil in new[] { ("k:", "konstanten"), ("r:", "rahmenplan") })
        {
            foreach (var eintrag in texte.GetProperty(teil.Item2).EnumerateObject())
            {
                if (eintrag.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var a in eintrag.Value.EnumerateObject())
                    {
                        Assert.Equal(a.Value.GetString(), Texte[teil.Item1 + eintrag.Name + "." + a.Name]);
                        namen.Add(teil.Item1 + eintrag.Name + "." + a.Name);
                    }
                    continue;
                }
                Assert.True(Texte.TryGetValue(teil.Item1 + eintrag.Name, out var ist), eintrag.Name);
                Assert.Equal(eintrag.Value.GetString(), ist);
                namen.Add(teil.Item1 + eintrag.Name);
            }
        }
        Assert.Equal(namen.Order(), Texte.Keys.Order());
        Assert.Equal(25 + 14, namen.Count);
        Assert.Equal(texte.GetProperty("options_subtitle").GetString(), Optionen.Bereich(Optionen.Rundgang).UntertitelPc);
        Assert.Equal(RundgangTexte.OptionenTitel, Optionen.Bereich(Optionen.Rundgang).Titel);

        var seiten = texte.GetProperty("seiten").EnumerateArray().ToList();
        Assert.Equal(seiten.Count, HilfeTexte.Seiten.Count);
        for (var i = 0; i < seiten.Count; i++)
        {
            Assert.Equal(seiten[i].GetProperty("id").GetString(), HilfeTexte.Seiten[i].Id);
            Assert.Equal(seiten[i].GetProperty("titel").GetString(), HilfeTexte.Seiten[i].Titel);
            Assert.Equal(seiten[i].GetProperty("text").GetString(), HilfeTexte.Seiten[i].Text);
        }

        var abschnitte = texte.GetProperty("abschnitte").EnumerateArray().ToList();
        Assert.Equal(16, abschnitte.Count);
        Assert.Equal(abschnitte.Count, HilfeTexte.Abschnitte.Count);
        for (var i = 0; i < abschnitte.Count; i++)
        {
            var id = abschnitte[i].GetProperty("id").GetString()!;
            Assert.Equal(id, HilfeTexte.Abschnitte[i].Id);
            Assert.Equal(abschnitte[i].GetProperty("titel").GetString(), HilfeTexte.Abschnitte[i].Titel);
            Assert.Equal(abschnitte[i].GetProperty("reiter").EnumerateArray().Select(r => r.GetString()), HilfeTexte.Abschnitte[i].Reiter);
            Assert.Equal(MitPaaren(id, abschnitte[i].GetProperty("text").GetString()!), HilfeTexte.Abschnitte[i].Text);
        }
        Assert.Equal(MitPaaren("RELEASE_PAGE_TEXT", texte.GetProperty("release_page_text").GetString()!),
            G35.GetProperty("paare").EnumerateArray().Last().GetProperty("neu").GetString());
    }

    [Fact]
    public void FesteTabelleWieG35()
    {
        var soll = G35.GetProperty("paare").EnumerateArray()
            .Select(p => (p.GetProperty("nr").GetString(), p.GetProperty("thema").GetString(), p.GetProperty("alt").GetString(), p.GetProperty("neu").GetString()))
            .ToList();
        Assert.Equal(soll, HilfePaare.Paare.Select(p => ((string?)p.Nr, (string?)p.Thema, (string?)p.Alt, (string?)p.Neu)).ToList());
        Assert.Equal(10, soll.Count); // 9 aus Phase 4 und S1 Spielzeit (Phase 7 W6, StadtTexte.HilfeSatz)
        // keine Python-Eigenheiten in der gezeigten Hilfe (Weisung P4-10 Punkt 2)
        foreach (var verboten in new[] { "fisi-lernplattf", "FISI_XIM", "kill -USR1", "0.59.", "fisi-lernstand", "Fisi-Lernplattform" })
        {
            Assert.DoesNotContain(HilfeTexte.Abschnitte, a => a.Text.Contains(verboten, StringComparison.Ordinal));
            Assert.DoesNotContain(HilfeTexte.Seiten, s => s.Text.Contains(verboten, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void DatumWiePython()
    {
        var anzahl = 0;
        foreach (var fall in G35.GetProperty("datum").EnumerateObject())
        {
            var iso = Einrichten.Datum(fall.Name) is { } tag ? Einrichten.Iso(tag) : null;
            Assert.True(fall.Value[0].GetString() == iso, $"parse_date {JsonSerializer.Serialize(fall.Name)}: {iso}");
            Assert.True(fall.Value[1].GetString() == Einrichten.DatumText(fall.Name), $"date_text {JsonSerializer.Serialize(fall.Name)}");
            var (pIso, pFehler) = Einrichten.PruefeDatum(fall.Name);
            Assert.True(fall.Value[2][0].GetString() == pIso && fall.Value[2][1].GetString() == pFehler, $"check_date {JsonSerializer.Serialize(fall.Name)}");
            anzahl++;
        }
        Assert.Equal(29, anzahl);
    }

    /// <summary>
    /// Kleinschreibung nur mit PythonKlein: "İ" wird in Python zu "i" + U+0307 und findet nichts
    /// (python3.12 -I, fisi_hilfe.search_help an fbc657d: "İ" 0, "İnternet" 0, "i" 16 Treffer).
    /// </summary>
    [Fact]
    public void SucheTuerkischesI()
    {
        Assert.Empty(HilfeTexte.Treffer("İ", PythonKlein.Klein));
        Assert.Empty(HilfeTexte.Treffer("İnternet", PythonKlein.Klein));
        Assert.Equal(16, HilfeTexte.Treffer("i", PythonKlein.Klein).Count);
        Assert.DoesNotContain(GlobaleSuche.SchnelleTreffer("İ", true), t => t.Art == RundgangTexte.SucheArt);
        // "İ" ist das einzige Zeichen, bei dem ToLowerInvariant von Python abweicht, und es steht in keinem
        // Hilfe-Text: darum zusaetzlich die Quelle (nur die uebergebene Kleinschreibung, kein ToLower)
        var quelle = File.ReadAllText(Path.Combine(Pfade.Wurzel, "src", "Lernplattform.Core", "Hilfe", "HilfeSuche.cs"));
        Assert.DoesNotContain("ToLower", quelle, StringComparison.Ordinal);
        Assert.DoesNotContain("ToUpper", quelle, StringComparison.Ordinal);
    }

    [Fact]
    public void SucheWiePython()
    {
        var anzahl = 0;
        foreach (var fall in G35.GetProperty("suche").EnumerateObject())
        {
            var soll = fall.Value.EnumerateArray().Select(t => string.Join("|", t.EnumerateArray().Select(VergleichsHilfe.Text))).ToList();
            // search_help liefert (id, titel, text); der Treffer der Suche ist (SEARCH_KIND, None, titel, text)
            var treffer = HilfeTexte.Treffer(fall.Name, PythonKlein.Klein);
            Assert.All(treffer, t => Assert.True(t.Art == RundgangTexte.SucheArt && t.Fachbereich is null));
            var ist = treffer.Select(t => string.Join("|", "s:" + HilfeTexte.Abschnitte.Single(a => a.Titel == t.Titel).Id, "s:" + t.Titel, "s:" + t.Detail)).ToList();
            Assert.True(soll.SequenceEqual(ist), $"{JsonSerializer.Serialize(fall.Name)}:\n" + VergleichsHilfe.Unterschied(soll, ist));
            anzahl++;
        }
        Assert.Equal(32, anzahl);
    }

    /// <summary>Plan W4 2.7: Kommazahl, Liste und Objekt gelten in C# als "kein Name" (feste Tabelle, Spezifikation 4).</summary>
    private static readonly string[] NameAbweichungen = ["1.0", "[1]", "{\"a\":1}"];

    [Fact]
    public void NameWiePython()
    {
        var abweichend = 0;
        foreach (var fall in G35.GetProperty("name").EnumerateArray())
        {
            var werte = new JsonObject { [Einrichten.NameSchluessel] = JsonNode.Parse(fall[0].GetRawText()) };
            var roh = fall[0].GetRawText();
            var name = Einrichten.NameLesen(werte);
            if (NameAbweichungen.Contains(roh))
            {
                abweichend++;
                Assert.Equal("", name);
                Assert.Equal("", DashboardTexte.Gruss(name));
                continue;
            }
            Assert.Equal(fall[1].GetString(), Einrichten.NameBereinigen(name));
            Assert.Equal(fall[2].GetString(), DashboardTexte.Gruss(name));
        }
        Assert.Equal(NameAbweichungen.Length, abweichend);
    }

    [Fact]
    public void RahmenplanWerteWiePython()
    {
        var faelle = new[]
        {
            "{}",
            "{\"rp_abschnitt_b\": \"ja\", \"rp_gewichtung\": 0, \"rp_lernfeld\": \"LF 10a\", \"rp_termin_ap1\": 20270101, \"rp_termin_ap2\": \"1.2.2028\"}",
            "{\"rp_abschnitt_e\": [], \"rp_lernfeld\": 5, \"rp_termin_ap2\": \"2028-02-30\"}",
            "{\"rp_termin_ap1\": \"1.2.2027\\u001c\", \"rp_termin_ap2\": \"1.2.２０２７\"}",
        };
        var soll = G35.GetProperty("einstellungen").GetProperty("rp_settings").EnumerateArray().ToList();
        Assert.Equal(faelle.Length, soll.Count);
        for (var i = 0; i < faelle.Length; i++)
        {
            var w = RahmenplanWerte.Aus(JsonNode.Parse(faelle[i])!.AsObject(), Plan.Value);
            var ist = new JsonObject
            {
                ["rp_abschnitt_b"] = w.AbschnittB, ["rp_abschnitt_d"] = w.AbschnittD, ["rp_abschnitt_e"] = w.AbschnittE,
                ["rp_gewichtung"] = w.Gewichtung, ["rp_lernfeld"] = w.Lernfeld, ["rp_termin_ap1"] = w.TerminAp1, ["rp_termin_ap2"] = w.TerminAp2,
            };
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(soll[i].GetRawText()), ist), $"Fall {i}: {ist.ToJsonString()}");
        }
    }

    private static readonly (string? Start, string Name, string Abschnitte, string Ap1, string Ap2)[] SetupFaelle =
    [
        (null, "  Alex  ", "{\"B\": true, \"D\": false, \"E\": true}", "29.09.2027", "26.04.2028"),
        ("{\"darstellung\": \"hell\", \"rp_abschnitt_d\": true, \"x\": [1]}", "Kim", "{\"B\": true}", "", " "),
        ("{\"darstellung\": \"hell\"}", "Kim", "{\"B\": true}", "31.02.2027", "1.1.2028"),
        ("{\"rp_abschnitt_b\": \"ja\", \"rp_termin_ap1\": \"kaputt\", \"rp_lernfeld\": \"LF 99\"}", "", "{}", "1.2.2027", "2028-4-26"),
        (null, "x", "{\"B\": true}", "", "29.02.2027"),
    ];

    private static JsonNode? Datei(string ordner) =>
        File.Exists(Path.Combine(ordner, Einstellungen.DateiName)) ? JsonNode.Parse(File.ReadAllText(Path.Combine(ordner, Einstellungen.DateiName))) : null;

    private static void Schreibe(string ordner, string? inhalt)
    {
        var pfad = Path.Combine(ordner, Einstellungen.DateiName);
        if (inhalt is null)
        {
            File.Delete(pfad);
        }
        else
        {
            File.WriteAllText(pfad, inhalt);
        }
    }

    /// <summary>Wie Zustand.Aendere: laden, aendern, einmal speichern.</summary>
    private static bool Aendere(Einstellungen einstellungen, Action<JsonObject> aenderung)
    {
        var werte = einstellungen.Laden();
        if (einstellungen.NurAnzeige)
        {
            return false;
        }
        aenderung(werte);
        return einstellungen.Speichern(werte);
    }

    private static JsonObject Startwerte(EinrichtenWerte w) => new()
    {
        ["name"] = w.Name,
        ["abschnitte"] = new JsonObject { ["B"] = w.B, ["D"] = w.D, ["E"] = w.E },
        ["rp_termin_ap1"] = w.TerminAp1,
        ["rp_termin_ap2"] = w.TerminAp2,
    };

    [Fact]
    public void EinrichtenWiePython()
    {
        using var ordner = new TempOrdner();
        var einstellungen = new Einstellungen(ordner.Pfad);
        var soll = G35.GetProperty("einstellungen");
        var faelle = soll.GetProperty("faelle").EnumerateArray().ToList();
        Assert.Equal(SetupFaelle.Length, faelle.Count);
        for (var i = 0; i < faelle.Count; i++)
        {
            var (start, name, abschnitte, ap1, ap2) = SetupFaelle[i];
            Schreibe(ordner.Pfad, start);
            var auswahl = JsonNode.Parse(abschnitte)!.AsObject().ToDictionary(a => a.Key, a => (bool)a.Value!);
            var (fehler, aenderung) = Einrichten.Speichern(name, auswahl, ap1, ap2);
            Assert.Equal(faelle[i][0].GetString(), fehler);
            Assert.Equal(fehler is null, aenderung is not null);
            if (aenderung is not null)
            {
                Assert.True(Aendere(einstellungen, aenderung));
            }
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(faelle[i][1].GetRawText()), Datei(ordner.Pfad)), $"Fall {i + 1}: {Datei(ordner.Pfad)?.ToJsonString()}");
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(faelle[i][2].GetRawText()), Startwerte(Einrichten.Startwerte(einstellungen.Laden()))), $"Fall {i + 1} setup_values");
        }

        Schreibe(ordner.Pfad, "{\"a\": 1}");
        Assert.True(Aendere(einstellungen, w =>
        {
            Assert.True(Einrichten.RahmenplanSetzen(w, "rp_abschnitt_b", true));
            Assert.False(Einrichten.RahmenplanSetzen(w, "foo", 1));
            Assert.True(Einrichten.RahmenplanSetzen(w, "rp_lernfeld", "LF 3"));
        }));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(soll.GetProperty("rp_speichern").GetRawText()), Datei(ordner.Pfad)));
        Assert.True(Aendere(einstellungen, RundgangAblauf.Ueberspringen(new DateOnly(2026, 10, 22))));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(soll.GetProperty("rundgang_merker").GetRawText()), Datei(ordner.Pfad)));
    }

    private static readonly string[] Merker = ["{}", "{\"rundgang_gesehen\": \"2026-10-22\"}", "{\"rundgang_gesehen\": \"\"}",
        "{\"rundgang_gesehen\": 0}", "{\"rundgang_gesehen\": true}", "{\"rundgang_gesehen\": []}", "{\"rundgang_gesehen\": \"0\"}"];

    /// <summary>Die fuenf Datenbanken von G35 (leer, Tabellen ohne Zeilen, Zeile in der zweiten Tabelle, Name mit ", kaputt).</summary>
    internal static string Datenbank(string ordner, string name)
    {
        var pfad = Path.Combine(ordner, name + ".db");
        if (name == "kaputt")
        {
            File.WriteAllBytes(pfad, Enumerable.Repeat((byte)'x', 100).ToArray());
            return pfad;
        }
        string[] sql = name switch
        {
            "leer" => [],
            "tabellen" => ["CREATE TABLE t1 (a)", "CREATE TABLE t2 (a)"],
            "zweite" => ["CREATE TABLE t1 (a)", "CREATE TABLE t2 (a)", "INSERT INTO t2 VALUES (1)"],
            _ => ["CREATE TABLE \"a\"\"b\" (x)", "INSERT INTO \"a\"\"b\" VALUES (1)"],
        };
        using var db = VergleichsHilfe.Oeffnen(pfad);
        foreach (var s in sql)
        {
            using var befehl = db.CreateCommand();
            befehl.CommandText = s;
            befehl.ExecuteNonQuery();
        }
        return pfad;
    }

    [Fact]
    public void RundgangWiePython()
    {
        using var ordner = new TempOrdner();
        var soll = G35.GetProperty("rundgang");
        var merker = Merker.Select(m => JsonNode.Parse(m)!.AsObject()).ToList();
        Assert.Equal(soll.GetProperty("gesehen").EnumerateArray().Select(g => g.GetBoolean()), merker.Select(Einrichten.RundgangGesehen));
        var anzahl = 0;
        foreach (var db in soll.GetProperty("daten").EnumerateObject())
        {
            var pfad = Datenbank(ordner.Pfad, db.Name);
            var hatDaten = Einrichten.HatDaten(pfad);
            Assert.True(db.Value.GetBoolean() == hatDaten, db.Name);
            Assert.Equal(soll.GetProperty("faellig").GetProperty(db.Name).EnumerateArray().Select(g => g.GetBoolean()),
                merker.Select(m => Einrichten.RundgangFaellig(m, hatDaten)));
            anzahl++;
        }
        Assert.Equal(5, anzahl);
        Assert.False(Einrichten.HatDaten(Path.Combine(ordner.Pfad, "fehlt.db")));
        Assert.False(File.Exists(Path.Combine(ordner.Pfad, "fehlt.db")));
    }

    [Fact]
    public void OberflaecheWiePython()
    {
        foreach (var ansicht in G35.GetProperty("oberflaeche").EnumerateObject())
        {
            var pc = ansicht.Value.GetProperty("pc").EnumerateArray().Select(n => Texte[n.GetString()!]).ToList();
            var handy = ansicht.Value.GetProperty("handy").EnumerateArray().Select(n => Texte[n.GetString()!]).ToList();
            if (ansicht.Name == "rundgang_tasten")
            {
                Assert.Equal([Geraetetexte.RundgangTasten(true)!], pc);
                Assert.Empty(handy);
                Assert.Null(Geraetetexte.RundgangTasten(false));
                continue;
            }
            Assert.Equal(pc, handy);
        }
        Assert.Equal(7, G35.GetProperty("oberflaeche").EnumerateObject().Count());
    }
}
