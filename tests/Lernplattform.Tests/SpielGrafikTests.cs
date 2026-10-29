using System.Numerics;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7 W4 (Weisung P7-9 Nachtrag 3/4): CC0-Modelle aus inhalte_csharp/grafik (Erzeuger
/// werkzeuge/grafik/wandle_modelle.py, die Original-ZIPs liegen nicht im Repo). Pruefsumme, Auswahl,
/// Zuordnung nur auf Bloecke, Lizenz je Paket, Stadt mit Modellen, Drehung.
/// </summary>
public class SpielGrafikTests
{
    private static string Ordner => Path.Combine(Pfade.Wurzel, "inhalte_csharp", "grafik");

    internal static readonly Lazy<GrafikBibliothek> Bibliothek = new(() => GrafikBibliothek.Laden(
        File.ReadAllBytes(Path.Combine(Ordner, "modelle.bin")),
        File.ReadAllText(Path.Combine(Ordner, "modelle.json")),
        File.ReadAllText(Path.Combine(Ordner, "zuordnung.json"))));

    [Fact]
    public void ModelleStimmenMitListeUndAuswahl()
    {
        var bib = Bibliothek.Value;
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(Ordner, "modelle.json")));
        using var auswahl = JsonDocument.Parse(File.ReadAllText(Path.Combine(Pfade.Wurzel, "werkzeuge", "grafik", "auswahl.json")));
        // seit Phase 8 W7: ein Eintrag mit "bilder" ergibt so viele Modelle (Name plus zwei Ziffern)
        var namen = auswahl.RootElement.GetProperty("modelle").EnumerateArray().SelectMany(m => m.TryGetProperty("bilder", out var b)
            ? Enumerable.Range(0, b.GetInt32()).Select(i => m.GetProperty("name").GetString()! + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture))
            : [m.GetProperty("name").GetString()!]).ToList();
        Assert.Equal(namen, index.RootElement.GetProperty("modelle").EnumerateArray().Select(m => m.GetProperty("name").GetString()!).ToList());
        Assert.Equal(namen.Count, bib.Modelle.Count);
        foreach (var eintrag in index.RootElement.GetProperty("modelle").EnumerateArray())
        {
            var m = bib.Modell(eintrag.GetProperty("name").GetString()!)!;
            Assert.Equal(eintrag.GetProperty("dreiecke").GetInt32(), m.Dreiecke);
            Assert.True(m.Dreiecke > 0);
            Assert.All(m.Ecken, e => Assert.True(float.IsFinite(e)));
            // modelle.json rundet auf 4 Stellen: hoechstens 0,00005 Abstand (strenger als der fruehere Vergleich auf 3 gerundete
            // Stellen, der an der Rundungsgrenze faelschlich rot war: hw-skull_candle 0,0575 gegen 0,057455)
            Assert.True(Math.Abs(eintrag.GetProperty("min")[1].GetDouble() - m.Min.Y) <= 0.00005 + 1e-6, eintrag.GetProperty("name").GetString());
        }
        // Groessenziel Plan Abschnitt 6: alle Spielgrafiken hoechstens 10 MB
        Assert.True(new FileInfo(Path.Combine(Ordner, "modelle.bin")).Length < 10_000_000);
    }

    /// <summary>
    /// Gegenprobe zum eigenen FBX-Leser (Weisung P7-10 Frage 1a): characterMedium.fbx ergibt genau 1604
    /// Dreiecke (wie die Polygonliste der Datei, Vierecke geteilt), steht auf dem Boden, ist etwa
    /// 3,77 Einheiten hoch und schaut nach +z (Gesicht vorn); Pruefsumme ueber Ecken und Farben fest.
    /// </summary>
    [Fact]
    public void FigurAusFbxWieGelesen()
    {
        var figur = Bibliothek.Value.Modell(Figur.Modell)!;
        Assert.Equal(1604, figur.Dreiecke);
        Assert.InRange(figur.Min.Y, 0f, 0.01f);
        Assert.InRange(figur.Max.Y, 3.76f, 3.77f);
        var daten = new byte[(figur.Ecken.Length * 4) + figur.Farben.Length];
        Buffer.BlockCopy(figur.Ecken, 0, daten, 0, figur.Ecken.Length * 4);
        figur.Farben.CopyTo(daten, figur.Ecken.Length * 4);
        Assert.Equal("bb2d00fa62d2a38c5e669924deda1d74fac1b72e072a669e36f01b3fee569634",
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(daten)));
        // Gesicht (Hautfarbe ueber Kinnhoehe) liegt vorn
        var gesicht = new List<float>();
        for (var d = 0; d < figur.Dreiecke; d++)
        {
            var y = (figur.Ecken[(d * 9) + 1] + figur.Ecken[(d * 9) + 4] + figur.Ecken[(d * 9) + 7]) / 3;
            if (y > 2.9f && Figur.Erkennen(figur.Farben[d * 3], figur.Farben[(d * 3) + 1], figur.Farben[(d * 3) + 2], y / 3.77f) == Figur.Bereich.Haut)
            {
                gesicht.Add((figur.Ecken[(d * 9) + 2] + figur.Ecken[(d * 9) + 5] + figur.Ecken[(d * 9) + 8]) / 3);
            }
        }
        Assert.True(gesicht.Count > 10 && gesicht.Average() > 0.2f, "Gesicht vorn: " + gesicht.Count);
    }

    /// <summary>Aussehen faerbt Hemd, Haut und Haare (SHIRT_COLORS, SKIN_COLORS, HAIR_COLORS), der Rest bleibt.</summary>
    [Fact]
    public void FigurTraegtDasAussehen()
    {
        var gruen = Figur.Netz(Bibliothek.Value, new PyObjekt { { "oberteil", "gruen" }, { "haut", "dunkel" }, { "haarfarbe", "blond" } }, 1f);
        var pink = Figur.Netz(Bibliothek.Value, new PyObjekt { { "oberteil", "pink" }, { "haut", "dunkel" }, { "haarfarbe", "blond" } }, 1f);
        Assert.Equal(gruen.AnzahlEcken, pink.AnzahlEcken);
        int anders = 0, gleich = 0;
        for (var i = 0; i < gruen.Ecken.Count; i += Netz.Zahlen)
        {
            // Hoehe 1: Ursprung unten, hoechster Punkt 1
            Assert.InRange(gruen.Ecken[i + 1], -0.001f, 1.001f);
            var a = new Vector3(gruen.Ecken[i + 6], gruen.Ecken[i + 7], gruen.Ecken[i + 8]);
            var b = new Vector3(pink.Ecken[i + 6], pink.Ecken[i + 7], pink.Ecken[i + 8]);
            if (Vector3.Distance(a, b) > 0.05f)
            {
                anders++;
                Assert.True(a.Y > a.X, "Hemd gruen");
                Assert.True(b.X > b.Y, "Hemd pink");
            }
            else
            {
                gleich++;
            }
        }
        Assert.True(anders > 100, "Hemd-Ecken: " + anders);
        Assert.True(gleich > anders, "Rest bleibt");
    }

    /// <summary>
    /// R3 (Phase 8 W7): modelle.bin waechst nur hinten. Die 79 Modelle bis W6 (A 0.16.0) stehen Byte fuer Byte
    /// unveraendert vorn (Pruefsumme ueber alles nach dem Kopf), nur die Anzahl im Kopf ist neu.
    /// </summary>
    [Fact]
    public void ModelleNurAngehaengt()
    {
        var bin = File.ReadAllBytes(Path.Combine(Ordner, "modelle.bin"));
        const int laengeW6 = 4182209;
        Assert.True(bin.Length > laengeW6);
        Assert.Equal("464c474d01000000", Convert.ToHexStringLower(bin.AsSpan(0, 8)));
        Assert.Equal("76fc0177abd57897a02061c1fbb2f0ae1f20d2ad256308a9da3c82f48e96f022",
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bin.AsSpan(12, laengeW6 - 12))));
        Assert.Equal(Bibliothek.Value.Modelle.Count, BitConverter.ToInt32(bin, 8));
    }

    [Fact]
    public void FalschePruefsummeWirdAbgelehnt()
    {
        var bin = File.ReadAllBytes(Path.Combine(Ordner, "modelle.bin"));
        bin[^1] ^= 1;
        Assert.Throws<InvalidDataException>(() => GrafikBibliothek.Laden(bin,
            File.ReadAllText(Path.Combine(Ordner, "modelle.json")), File.ReadAllText(Path.Combine(Ordner, "zuordnung.json"))));
    }

    [Fact]
    public void ZuordnungNurAufBloeckeUndJederGenutzt()
    {
        var bib = Bibliothek.Value;
        var modelle = StadtModelle.Modelle(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
        foreach (var (schluessel, name) in bib.Bloecke)
        {
            var teile = schluessel.Split(':');
            var liste = SpielDaten.Liste(modelle[teile[0]]);
            var teil = SpielDaten.Objekt(liste[int.Parse(teile[1], System.Globalization.CultureInfo.InvariantCulture)]);
            Assert.True(Py.Gleich(teil["art"], "block"), schluessel + " ist kein Block");
            Assert.NotNull(bib.Modell(name));
        }
        // nur genutzte Modelle im Repo (Plan Abschnitt 6): Bloecke, Baeume, Zug (Schienen seit P7-10 Nachtrag 1 eigene Geometrie),
        // seit W5 Moebel der Innenraeume und die Figur, seit Phase 8 W6 die Fest-Deko aus festzeiten.json,
        // seit Phase 8 W7 die Schrittbilder der Figur, die Autos und das Gruen
        var fest = new[] { "tree_large", "tree_small", "train-electric-city-a", "train-electric-city-b", "train-electric-city-c", Figur.Modell }
            .Concat(Innenraum.Modelle).Concat(FestDeko.Modelle(FestDekoTests.Daten.Value))
            .Concat(new[] { Figur.Gehen, Figur.Rennen }.SelectMany(b => Enumerable.Range(0, Figur.Bilder).Select(i => Figur.BildName(b, i))))
            .Concat(Verkehr.Strecken.Select(s => s.Modell)).Concat(Gruen.Modelle)
            // seit Phase 8 W11 die Laternen, Tannen (im Herbst gefaerbt) und Kuerbisse aus dem Graveyard Kit und die Gestalten der Festzeiten
            .Concat(Nachtlicht.Skizze.Select((l, i) => Nachtlicht.Modell(new Nachtlicht.Laterne(i + 1, default, l.Doppelt, default, default))))
            .Concat(new[] { "gy-pine", "gy-pine-crooked" }.SelectMany(t => new[] { t, StadtSzene.TanneIn(t, Jahreszeit.Herbst) }))
            .Concat(FestDeko.Modelle(FestDekoTests.Daten.Value).Select(FestDeko.Kuerbis))
            // seit Phase 9 W1 (Weisung P9-3) das Sitzbild der Figur (Personen am Schreibtisch)
            .Append(Figur.BildName(Figur.Sitzen, 0))
            .Concat(["hw-lantern_standing", "hw-pumpkin", "hw-pumpkin_orange_jackolantern", "os-animal-bunny", "os-animal-chick", "xm-present_A_red", "xm-present_A_yellow", "xm-present_B_green"])
            .ToList();
        foreach (var m in bib.Modelle)
        {
            Assert.True(bib.Bloecke.Values.Contains(m.Name) || fest.Contains(m.Name), m.Name + " wird nicht genutzt");
        }
    }

    [Fact]
    public void JedesPaketHatEinenLizenzBestandteil()
    {
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(Ordner, "modelle.json")));
        var bestandteile = File.ReadAllText(Path.Combine(Pfade.Wurzel, "werkzeuge", "lizenzen", "bestandteile.json"));
        var hinweise = File.ReadAllText(Path.Combine(Pfade.Wurzel, "THIRD_PARTY_NOTICES.txt"));
        var genutzt = index.RootElement.GetProperty("modelle").EnumerateArray().Select(m => m.GetProperty("paket").GetString()!).Distinct().ToList();
        var namen = new Dictionary<string, string>
        {
            ["commercial"] = "Grafik Kenney City Kit Commercial",
            ["suburban"] = "Grafik Kenney City Kit Suburban",
            ["train"] = "Grafik Kenney Train Kit",
            ["furniture"] = "Grafik Kenney Furniture Kit",
            ["characters"] = "Grafik Kenney Animated Characters 3",
            ["kaykit_moebel"] = "Grafik KayKit Furniture Bits",
            ["modular"] = "Grafik Kenney Modular Buildings",
            ["kaykit_halloween"] = "Grafik KayKit Halloween Bits",
            ["kaykit_holiday"] = "Grafik KayKit Holiday Bits",
            ["cube_pets"] = "Grafik Kenney Cube Pets",
            ["car_kit"] = "Grafik Kenney Car Kit",
            ["kaykit_wald"] = "Grafik KayKit Forest Nature Pack",
            ["graveyard"] = "Grafik Kenney Graveyard Kit",
        };
        foreach (var paket in genutzt)
        {
            Assert.True(namen.ContainsKey(paket), "Paket " + paket + " ohne Bestandteil");
            Assert.Contains("\"" + namen[paket] + "\"", bestandteile);
            Assert.Contains("- " + namen[paket] + " (", hinweise);
            Assert.Equal("CC0 1.0", index.RootElement.GetProperty("pakete").GetProperty(paket).GetProperty("lizenz").GetString());
        }
    }

    [Fact]
    public void StadtMitModellenIstVollstaendigUndFest()
    {
        var stadt = new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
        var szene = StadtSzene.Bauen(stadt, null, Bibliothek.Value);
        var namen = szene.Stuecke.Select(s => s.Name).ToHashSet();
        Assert.DoesNotContain("railroad-straight", namen);
        Assert.True(szene.Streifen.Count > 100, "Gleis und Fluss als Streifen");
        Assert.Contains("train-electric-city-a", namen);
        Assert.True(namen.Contains("tree_large") || namen.Contains("tree_small"));
        // Bitweiche und Zuhause stehen als CC0-Haeuser
        Assert.Contains(Bibliothek.Value.Bloecke["bitweiche:0"], namen);
        var netz = szene.NetzBauen();
        Assert.True(netz.AnzahlEcken > Netz.Aus(szene.Teile).AnzahlEcken);
        Assert.All(netz.Ecken, e => Assert.True(float.IsFinite(e)));
        Assert.Equal(netz.Ecken, StadtSzene.Bauen(stadt, null, Bibliothek.Value).NetzBauen().Ecken);
        // gleiche Orte, Tueren und Signatur wie ohne Grafik (nur die Darstellung aendert sich)
        var ohne = StadtSzene.Bauen(stadt, null);
        Assert.Equal(ohne.Signatur, szene.Signatur);
        Assert.Equal(ohne.Orte.Select(o => (o.Id, o.TuerX, o.TuerZ)), szene.Orte.Select(o => (o.Id, o.TuerX, o.TuerZ)));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    [InlineData(0.6, -0.8)]
    public void DrehungLegtVorderseiteInDieRichtung(float x, float z)
    {
        var v = Vector3.TransformNormal(Vector3.UnitZ, StadtSzene.Drehung(new Vector2(x, z)));
        Assert.Equal(x, v.X, 4);
        Assert.Equal(0, v.Y, 4);
        Assert.Equal(z, v.Z, 4);
    }

    [Fact]
    public void BlockFuelltGenauDieGrundflaeche()
    {
        var gm = Bibliothek.Value.Modell(Bibliothek.Value.Bloecke["moebelhaus:0"])!;
        var stuecke = new List<ModellStueck>();
        var hoehe = StadtSzene.GrafikBlock(stuecke, gm, 14.2f, 21.4f, 3.4f, 2.0f, 1.4f);
        Vector3 min = new(float.MaxValue), max = new(float.MinValue);
        foreach (var s in stuecke)
        {
            for (var i = 0; i + 2 < gm.Ecken.Length; i += 3)
            {
                var p = Vector3.Transform(new Vector3(gm.Ecken[i], gm.Ecken[i + 1], gm.Ecken[i + 2]), s.Lage);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }
        Assert.Equal(14.2f, min.X, 3);
        Assert.Equal(17.6f, max.X, 3);
        Assert.Equal(21.4f, min.Z, 3);
        Assert.Equal(23.4f, max.Z, 3);
        Assert.Equal(0f, min.Y, 3);
        Assert.Equal(hoehe, max.Y, 3);
    }
}
