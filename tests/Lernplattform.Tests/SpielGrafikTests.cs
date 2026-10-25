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
        var namen = auswahl.RootElement.GetProperty("modelle").EnumerateArray().Select(m => m.GetProperty("name").GetString()!).ToList();
        Assert.Equal(namen, index.RootElement.GetProperty("modelle").EnumerateArray().Select(m => m.GetProperty("name").GetString()!).ToList());
        Assert.Equal(namen.Count, bib.Modelle.Count);
        foreach (var eintrag in index.RootElement.GetProperty("modelle").EnumerateArray())
        {
            var m = bib.Modell(eintrag.GetProperty("name").GetString()!)!;
            Assert.Equal(eintrag.GetProperty("dreiecke").GetInt32(), m.Dreiecke);
            Assert.True(m.Dreiecke > 0);
            Assert.All(m.Ecken, e => Assert.True(float.IsFinite(e)));
            Assert.Equal(eintrag.GetProperty("min")[1].GetDouble(), m.Min.Y, 3);
        }
        // Groessenziel Plan Abschnitt 6: alle Spielgrafiken hoechstens 10 MB
        Assert.True(new FileInfo(Path.Combine(Ordner, "modelle.bin")).Length < 10_000_000);
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
        // nur genutzte Modelle im Repo (Plan Abschnitt 6): Bloecke, Baeume, Schienen, Zug
        var fest = new[] { "tree_large", "tree_small", "railroad-straight", "train-electric-city-a", "train-electric-city-b", "train-electric-city-c" };
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
        Assert.Contains("railroad-straight", namen);
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
