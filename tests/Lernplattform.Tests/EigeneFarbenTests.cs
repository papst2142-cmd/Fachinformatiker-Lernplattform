using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Design;

namespace Lernplattform.Tests;

/// <summary>
/// Eigene Farben (Python fisi_theme ab 0.57) exakt gleich wie Python, Vergleichswerte G6b
/// (tests/Vergleich/eigene_farben.json, erzeugt von werkzeuge/vergleich/erzeuge_eigene_farben.py).
/// Jede Abweichung, auch um 1 je Farbkanal oder im letzten Bit eines Messwerts, ist ein Fehler.
/// </summary>
public class EigeneFarbenTests
{
    private static readonly Lazy<JsonElement> Daten = new(() =>
    {
        using var f = File.OpenRead(Pfade.Vergleich("eigene_farben.json"));
        return JsonDocument.Parse(f).RootElement.Clone();
    });

    private static JsonElement Liste(string name) => Daten.Value.GetProperty(name);

    private static TheoryData<int> Indizes(string name)
    {
        var daten = new TheoryData<int>();
        for (int i = 0; i < Liste(name).GetArrayLength(); i++)
        {
            daten.Add(i);
        }
        return daten;
    }

    public static TheoryData<int> StartFaelle() => Indizes("start");
    public static TheoryData<int> PalettenFaelle() => Indizes("paletten");
    public static TheoryData<int> PruefFaelle() => Indizes("pruefen");
    public static TheoryData<int> ZustandFaelle() => Indizes("zustand");
    public static TheoryData<int> SpeicherFaelle() => Indizes("speichern");

    private static Regler AlsRegler(JsonElement e) => new(e[0].GetInt32(), e[1].GetInt32(), e[2].GetInt32());

    private static EigeneWerte? Werte(JsonElement e) => e.ValueKind == JsonValueKind.Null
        ? null
        : new EigeneWerte(AlsRegler(e.GetProperty("akzent1")), AlsRegler(e.GetProperty("akzent2")),
            AlsRegler(e.GetProperty("hintergrund")));

    private static string Text(JsonElement e) => e.GetString()!;

    private static readonly (string, string)[] FesteVerlaeufe = [("danger", "#DC2626"), ("success", "#05875F")];

    private static void PaletteGleich(JsonElement soll, Palette ist, string fall)
    {
        Assert.True(soll.GetProperty("hell").GetBoolean() == ist.Hell, $"{fall}: hell");
        var farben = soll.GetProperty("farben").EnumerateObject().ToDictionary(p => p.Name, p => Text(p.Value));
        Assert.Equal(farben.Keys.Order(StringComparer.Ordinal), ist.Farben.Keys.Order(StringComparer.Ordinal));
        foreach (var (name, wert) in farben)
        {
            Assert.True(wert == ist.Farben[name], $"{fall} {name}: Python {wert}, C# {ist.Farben[name]}");
        }
        var verlaeufe = soll.GetProperty("verlaeufe").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        // Python custom_palette liefert nur primary/accent/hero; danger/success bleiben in
        // Python fest in GRADIENTS stehen, C# traegt sie in jeder Palette mit.
        Assert.Equal(verlaeufe.Keys.Union(["danger", "success"]).Order(StringComparer.Ordinal),
            ist.Verlaeufe.Keys.Order(StringComparer.Ordinal));
        foreach (var (name, v) in verlaeufe)
        {
            Assert.True((Text(v[0]), Text(v[1])) == ist.Verlaeufe[name], $"{fall} Verlauf {name}");
        }
        foreach (var (name, von) in FesteVerlaeufe)
        {
            Assert.Equal(von, ist.Verlaeufe[name].Von);
        }
        Assert.Equal("#BF5811", ist.Verlaeufe["danger"].Bis);
        Assert.Equal("#158293", ist.Verlaeufe["success"].Bis);
    }

    private static void PruefungenGleich(JsonElement soll, IReadOnlyList<KontrastPruefung> ist, string fall)
    {
        Assert.True(soll.GetArrayLength() == ist.Count, $"{fall}: Anzahl {soll.GetArrayLength()} / {ist.Count}");
        for (int i = 0; i < ist.Count; i++)
        {
            var s = soll[i];
            Assert.Equal(Text(s[0]), ist[i].Element);
            // exakt gleich (Python schreibt die kuerzeste exakte Darstellung)
            Assert.True(s[1].GetDouble() == ist[i].Wert, $"{fall} {ist[i].Element}: Python {s[1].GetDouble():R}, C# {ist[i].Wert:R}");
            Assert.Equal(s[2].GetDouble(), ist[i].Ziel);
        }
    }

    private static void ZeilenGleich(JsonElement soll, IReadOnlyList<string> ist) =>
        Assert.Equal(soll.EnumerateArray().Select(Text), ist);

    // ------------------------------------------------------------ Texte

    [Fact]
    public void TexteGleichWiePython()
    {
        var c = new Dictionary<string, string>
        {
            ["CUSTOM_CLASH"] = EigeneFarben.FachbereichNahText,
            ["CUSTOM_HINT"] = EigeneFarben.Hinweis,
            ["CUSTOM_KEY"] = EigeneFarben.Schluessel,
            ["CUSTOM_OK"] = EigeneFarben.AllesGut,
            ["CUSTOM_PREVIEW"] = EigeneFarben.Vorschau,
            ["CUSTOM_PREVIEW_BUTTON"] = EigeneFarben.VorschauKnopf,
            ["CUSTOM_PREVIEW_LINK"] = EigeneFarben.VorschauLink,
            ["CUSTOM_PREVIEW_MAP"] = EigeneFarben.VorschauKarte,
            ["CUSTOM_PREVIEW_MUTED"] = EigeneFarben.VorschauNebentext,
            ["CUSTOM_PREVIEW_TEXT"] = EigeneFarben.VorschauText,
            ["CUSTOM_PREVIEW_TITLE"] = EigeneFarben.VorschauUeberschrift,
            ["CUSTOM_RESET"] = EigeneFarben.ZuruecksetzenText,
            ["CUSTOM_SAVE"] = EigeneFarben.SpeichernText,
            ["CUSTOM_STATE_CHANGED"] = EigeneFarben.ZustandVeraendertText,
            ["CUSTOM_STATE_OFF"] = EigeneFarben.ZustandAusText,
            ["CUSTOM_STATE_ON"] = EigeneFarben.ZustandAnText,
            ["CUSTOM_TILE_HINT"] = EigeneFarben.KachelHinweis,
            ["CUSTOM_TITLE"] = EigeneFarben.Titel,
            ["CUSTOM_WARN_MORE"] = EigeneFarben.WarnWeitere,
            ["CUSTOM_WARN_TITLE"] = EigeneFarben.WarnTitel,
        };
        var python = Liste("texte").EnumerateObject().ToDictionary(p => p.Name, p => Text(p.Value));
        Assert.Equal(python.Keys.Order(StringComparer.Ordinal), c.Keys.Order(StringComparer.Ordinal));
        foreach (var (name, wert) in python)
        {
            // %s/%d der Reihe nach als {0}, {1} ...
            var teile = wert.Replace("%d", "%s", StringComparison.Ordinal).Split("%s");
            var erwartet = teile[0];
            for (int i = 1; i < teile.Length; i++)
            {
                erwartet += "{" + (i - 1) + "}" + teile[i];
            }
            Assert.True(erwartet == c[name], $"{name}: Python {wert}, C# {c[name]}");
        }
    }

    [Fact]
    public void TeileKanaeleUndZieleGleichWiePython()
    {
        var teile = Liste("teile");
        Assert.Equal(teile.GetArrayLength(), EigeneFarben.Teile.Count);
        for (int i = 0; i < EigeneFarben.Teile.Count; i++)
        {
            Assert.Equal((Text(teile[i][0]), Text(teile[i][1])), EigeneFarben.Teile[i]);
        }
        var kanaele = Liste("kanaele");
        Assert.Equal(kanaele.GetArrayLength(), EigeneFarben.Kanaele.Count);
        for (int i = 0; i < EigeneFarben.Kanaele.Count; i++)
        {
            var k = kanaele[i];
            Assert.Equal(new ReglerKanal(Text(k[0]), Text(k[1]), k[2].GetInt32(), k[3].GetInt32(), Text(k[4]), Text(k[5])),
                EigeneFarben.Kanaele[i]);
        }
        foreach (var m in Liste("darstellungen").EnumerateArray())
        {
            Assert.Equal(Text(m[1]), EigeneFarben.DarstellungName(Text(m[0])));
        }
        var ziele = Liste("ziele");
        Assert.True(ziele.GetProperty("schrift").GetDouble() == EigeneFarben.SchriftZiel, "schrift");
        Assert.True(ziele.GetProperty("flaeche").GetDouble() == EigeneFarben.FlaechenZiel, "flaeche");
        Assert.True(ziele.GetProperty("mindestkontrast").GetDouble() == Farbwelt.MindestKontrast, "mindestkontrast");
        Assert.True(ziele.GetProperty("fachbereich_abstand").GetDouble() == EigeneFarben.FachbereichAbstand, "fachbereich_abstand");
        Assert.Throws<ArgumentException>(() => EigeneFarben.DarstellungName("gibtsnicht"));
    }

    // ------------------------------------------------------------ Startwerte

    [Fact]
    public void StartwerteFuerAlle72Farbwelten()
    {
        Assert.Equal(72, Liste("start").GetArrayLength());
    }

    [Theory]
    [MemberData(nameof(StartFaelle))]
    public void StartUndWertePaletteGleichWiePython(int index)
    {
        var s = Liste("start")[index];
        string d = Text(s.GetProperty("darstellung")), g = Text(s.GetProperty("grundfarbe")), h = Text(s.GetProperty("hintergrund"));
        var fall = $"{d}/{g}/{h}";
        var start = EigeneFarben.Start(d, g, h);
        Assert.Equal(Werte(s.GetProperty("start")), start);
        Assert.Equal(s.GetProperty("ist_start").GetBoolean(), EigeneFarben.IstStart(start, d, g, h));
        var palette = EigeneFarben.WertePalette(start, d, g, h);
        PaletteGleich(s.GetProperty("palette"), palette, fall);
        // am Startwert exakt die Farbwelt ("sanft")
        var welt = Farbwelt.Berechne(d, g, h);
        Assert.Equal(welt.Hell, palette.Hell);
        Assert.Equal(welt.Farben.OrderBy(x => x.Key, StringComparer.Ordinal), palette.Farben.OrderBy(x => x.Key, StringComparer.Ordinal));
        Assert.Equal(welt.Verlaeufe.OrderBy(x => x.Key, StringComparer.Ordinal), palette.Verlaeufe.OrderBy(x => x.Key, StringComparer.Ordinal));

        var daneben = Werte(s.GetProperty("daneben"))!;
        Assert.Equal(s.GetProperty("daneben_ist_start").GetBoolean(), EigeneFarben.IstStart(daneben, d, g, h));
        PaletteGleich(s.GetProperty("daneben_palette"), EigeneFarben.WertePalette(daneben, d, g, h), fall + " daneben");
    }

    [Fact]
    public void StartMitUnbekanntenKennungen()
    {
        foreach (var s in Liste("start_unbekannt").EnumerateArray())
        {
            Assert.Equal(Werte(s.GetProperty("start")),
                EigeneFarben.Start(Text(s.GetProperty("darstellung")), Text(s.GetProperty("grundfarbe")), Text(s.GetProperty("hintergrund"))));
        }
    }

    // ------------------------------------------------------------ Paletten und Kontrolle

    [Fact]
    public void GenugPalettenFaelle()
    {
        Assert.True(Liste("paletten").GetArrayLength() >= 100);
    }

    [Theory]
    [MemberData(nameof(PalettenFaelle))]
    public void PaletteUndKontrolleGleichWiePython(int index)
    {
        var s = Liste("paletten")[index];
        var werte = Werte(s.GetProperty("werte"))!;
        var fall = $"Fall {index} {werte}";
        var palette = EigeneFarben.Palette(werte);
        PaletteGleich(s.GetProperty("palette"), palette, fall);
        PruefungenGleich(s.GetProperty("pruefungen"), EigeneFarben.Pruefungen(palette), fall);
        PruefungenGleich(s.GetProperty("probleme"), EigeneFarben.Probleme(palette), fall);
        ZeilenGleich(s.GetProperty("warnung"), EigeneFarben.Warnung(palette));
        var nah = s.GetProperty("fachbereich_nah");
        var istNah = EigeneFarben.FachbereichNah(palette);
        if (nah.ValueKind == JsonValueKind.Null)
        {
            Assert.Null(istNah);
        }
        else
        {
            Assert.NotNull(istNah);
            Assert.Equal(Text(nah[0]), istNah.Value.Erste);
            Assert.Equal(Text(nah[1]), istNah.Value.Zweite);
            Assert.True(nah[2].GetDouble() == istNah.Value.Abstand, $"{fall}: Abstand {nah[2].GetDouble():R} / {istNah.Value.Abstand:R}");
        }
        if (s.TryGetProperty("karte", out var k))
        {
            var karte = new KartenFarben(Text(k.GetProperty("boden")), Text(k.GetProperty("schrift")),
                Text(k.GetProperty("strasse")), Text(k.GetProperty("wasser")));
            PruefungenGleich(s.GetProperty("pruefungen_karte"), EigeneFarben.Pruefungen(palette, karte), fall + " Karte");
            PruefungenGleich(s.GetProperty("probleme_karte"), EigeneFarben.Probleme(palette, karte), fall + " Karte");
            ZeilenGleich(s.GetProperty("warnung_karte"), EigeneFarben.Warnung(palette, karte));
        }
    }

    [Fact]
    public void WarnZeilenUndVerhaeltnisseGleichWiePython()
    {
        foreach (var s in Liste("warnzeilen").EnumerateArray())
        {
            var probleme = s.GetProperty("probleme").EnumerateArray()
                .Select(p => new KontrastPruefung(Text(p[0]), p[1].GetDouble(), p[2].GetDouble())).ToList();
            ZeilenGleich(s.GetProperty("zeilen"), EigeneFarben.WarnZeilen(probleme));
        }
        foreach (var s in Liste("verhaeltnisse").EnumerateArray())
        {
            Assert.Equal(Text(s[1]), EigeneFarben.Verhaeltnis(s[0].GetDouble()));
        }
    }

    [Fact]
    public void HslHilfenGleichWiePython()
    {
        foreach (var s in Liste("hsl").EnumerateArray())
        {
            if (s.TryGetProperty("hsl", out var hsl))
            {
                Assert.Equal(Text(s.GetProperty("hex")), EigeneFarben.HslZuHex(hsl[0].GetDouble(), hsl[1].GetDouble(), hsl[2].GetDouble()));
            }
            else
            {
                Assert.Equal(AlsRegler(s.GetProperty("zurueck")), EigeneFarben.HexZuHsl(Text(s.GetProperty("hex"))));
            }
        }
    }

    [Fact]
    public void FarbabstandGleichWiePython()
    {
        Assert.True(Liste("farbabstaende").GetArrayLength() >= 400);
        foreach (var s in Liste("farbabstaende").EnumerateArray())
        {
            var ist = EigeneFarben.Farbabstand(Text(s[0]), Text(s[1]));
            Assert.True(s[2].GetDouble() == ist, $"{Text(s[0])}/{Text(s[1])}: Python {s[2].GetDouble():R}, C# {ist:R}");
        }
    }

    // ------------------------------------------------------------ Pruefen (valid_custom)

    // Marker {"__zahl__": "NaN"} (kein gueltiges JSON) -> echte Gleitkommazahl
    private static JsonNode? MitZahlen(JsonNode? knoten) => knoten switch
    {
        JsonObject o when o.Count == 1 && o["__zahl__"] is JsonValue v => JsonValue.Create(v.GetValue<string>() switch
        {
            "NaN" => double.NaN,
            "Infinity" => double.PositiveInfinity,
            _ => double.NegativeInfinity,
        }),
        JsonObject o => new JsonObject(o.Select(p => KeyValuePair.Create(p.Key, MitZahlen(p.Value)))),
        JsonArray a => new JsonArray(a.Select(MitZahlen).ToArray()),
        null => null,
        _ => knoten.DeepClone(),
    };

    [Theory]
    [MemberData(nameof(PruefFaelle))]
    public void PruefenGleichWiePython(int index)
    {
        var s = Liste("pruefen")[index];
        var soll = Werte(s.GetProperty("ergebnis"));
        var eingabe = MitZahlen(JsonNode.Parse(s.GetProperty("eingabe").GetRawText()));
        Assert.Equal(soll, EigeneFarben.Pruefen(eingabe));
        // Der Originaltext so, wie er in einstellungen.json stuende (NaN/Infinity kann System.Text.Json nicht lesen)
        var text = Text(s.GetProperty("text"));
        if (!text.Contains("NaN", StringComparison.Ordinal) && !text.Contains("Infinity", StringComparison.Ordinal))
        {
            Assert.Equal(soll, EigeneFarben.Pruefen(JsonNode.Parse(text)));
        }
    }

    [Fact]
    public void PruefenOhneWert()
    {
        Assert.Null(EigeneFarben.Pruefen(null));
        Assert.Null(EigeneFarben.Pruefen(JsonValue.Create(5)));
        var w = new JsonObject
        {
            ["akzent1"] = new JsonArray(1, 2.5, 3),
            ["akzent2"] = new JsonArray(4L, 5, 6.5m),
            ["hintergrund"] = new JsonArray(7, 8, true),
        };
        Assert.Null(EigeneFarben.Pruefen(w));
        w["hintergrund"] = new JsonArray(7, 8, 9);
        Assert.Equal(new EigeneWerte(new(1, 2, 3), new(4, 5, 6), new(7, 8, 9)), EigeneFarben.Pruefen(w));
    }

    // ------------------------------------------------------------ Zustand

    [Theory]
    [MemberData(nameof(ZustandFaelle))]
    public void ZustandGleichWiePython(int index)
    {
        var s = Liste("zustand")[index];
        string d = Text(s.GetProperty("darstellung")), g = Text(s.GetProperty("grundfarbe")), h = Text(s.GetProperty("hintergrund"));
        var gespeichert = Werte(s.GetProperty("gespeichert"));
        var regler = Werte(s.GetProperty("regler"))!;
        var klick = s.GetProperty("klick");
        bool geklickt = klick.ValueKind != JsonValueKind.Null;
        string kg = geklickt ? Text(klick[0]) : g, kh = geklickt ? Text(klick[1]) : h;
        Assert.Equal(Text(s.GetProperty("zustand")), EigeneFarben.Zustand(regler, gespeichert, d, g, h));
        Assert.Equal(Text(s.GetProperty("zustand_text")), EigeneFarben.ZustandText(regler, gespeichert, d, g, h));
        Assert.Equal(s.GetProperty("ist_start").GetBoolean(), EigeneFarben.IstStart(regler, d, kg, kh));
        Assert.Equal(Werte(s.GetProperty("zu_speichern")), EigeneFarben.ZuSpeichern(regler, d, kg, kh));
        Assert.Equal(Werte(s.GetProperty("zu_speichern_ohne")), EigeneFarben.ZuSpeichern(null, d, kg, kh));
        Assert.Equal(s.GetProperty("kachel_setzt_regler").GetBoolean(),
            EigeneFarben.KachelSetztRegler(d, regler, gespeichert is not null, geklickt, g, h));
    }

    [Fact]
    public void ZuSpeichernUngueltigeWerte()
    {
        var werte = new EigeneWerte(new(360, 0, 0), new(0, 0, 0), new(0, 0, 0));
        Assert.False(EigeneFarben.IstGueltig(werte));
        Assert.Null(EigeneFarben.ZuSpeichern(werte, Farbwelt.Dunkel, Farbwelt.StandardGrundfarbe, Farbwelt.StandardHintergrund));
        Assert.False(EigeneFarben.IstGueltig(werte with { Akzent1 = new(0, -1, 0) }));
        Assert.False(EigeneFarben.IstGueltig(werte with { Akzent1 = new(0, 0, 101) }));
        Assert.True(EigeneFarben.IstGueltig(werte with { Akzent1 = new(359, 100, 100) }));
    }

    // ------------------------------------------------------------ Speichern

    [Theory]
    [MemberData(nameof(SpeicherFaelle))]
    public void SpeicherWertGleichWiePython(int index)
    {
        var s = Liste("speichern")[index];
        var bisher = JsonNode.Parse(s.GetProperty("bisher").GetRawText());
        var vorher = bisher?.ToJsonString();
        var werte = Werte(s.GetProperty("werte"));
        var ist = EigeneFarben.SpeicherWert(bisher, Text(s.GetProperty("darstellung")), werte);
        Assert.Equal(s.GetProperty("vorhanden").GetBoolean(), ist is not null);
        if (ist is not null)
        {
            // gleiche Werte in gleicher Reihenfolge
            Assert.Equal(JsonNode.Parse(s.GetProperty("ergebnis").GetRawText())!.ToJsonString(), ist.ToJsonString());
        }
        Assert.Equal(Werte(s.GetProperty("rueckgabe")), werte is not null && EigeneFarben.IstGueltig(werte) ? werte : null);
        // der bisherige Wert bleibt unveraendert
        Assert.Equal(vorher, bisher?.ToJsonString());
    }

    [Fact]
    public void GespeichertGleichWiePython()
    {
        foreach (var s in Liste("gespeichert").EnumerateArray())
        {
            var ist = EigeneFarben.Gespeichert(JsonNode.Parse(s.GetProperty("bisher").GetRawText()));
            var soll = s.GetProperty("ergebnis").EnumerateObject().ToDictionary(p => p.Name, p => Werte(p.Value)!);
            Assert.Equal(soll.OrderBy(x => x.Key, StringComparer.Ordinal), ist.OrderBy(x => x.Key, StringComparer.Ordinal));
        }
    }

    // ------------------------------------------------------------ Regler-Texte

    [Fact]
    public void ReglerTexteGleichWiePython()
    {
        int anzahl = 0;
        foreach (var s in Liste("regler_texte").EnumerateArray())
        {
            var teil = EigeneFarben.Teile.Single(t => t.Id == Text(s.GetProperty("teil")));
            var kanal = EigeneFarben.Kanaele.Single(k => k.Id == Text(s.GetProperty("kanal")));
            var wert = s.GetProperty("wert").GetInt32();
            Assert.Equal(Text(s.GetProperty("beschriftung")), EigeneFarben.ReglerBeschriftung(teil.Name, kanal, wert));
            Assert.Equal(Text(s.GetProperty("text")), EigeneFarben.ReglerText(kanal, wert));
            anzahl++;
        }
        Assert.True(anzahl >= 27);
    }
}
