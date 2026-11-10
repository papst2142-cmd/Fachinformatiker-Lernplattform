using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Weisung P12-175 Punkt 3 (Engine festigen Stufe 1): jedes Datenformat der Engine an einer Stelle abgesichert.
/// Je Format: (a) eine neuere Formatnummer ist ein klarer Fehler bzw. ein klarer Hinweis, nie still, (b) jede noch lesbare
/// alte Fassung hat eine echte Testdatei, die ein frueheres Programm geschrieben hat, und laesst sich lesen.
/// Uebersicht in <c>docs/ENGINE.md</c> Abschnitt „Formate“. Echte Testdateien in <see cref="TestdateiTests.Ordner"/>:
/// <c>projekt_S0.1.0.fisbau</c> (Format 1, mit dem Code von S 0.1.0, main 51d41b6, geschrieben), <c>projekt_S0.4.0.fisbau</c>
/// (Format 2), <c>blueprint_S0.8.0.fisblueprint</c> (1), <c>blueprint_S0.18.0.fisblueprint</c> (2),
/// <c>katalog_verbaut_S0.4.0.bin/.json</c> (FLGM 1, Testdatei aus S 0.4.0, main 365533e), <c>stadt1_fassung1.fistadt</c> und
/// die Fassung 0 im Repo (fistadt 1).
/// </summary>
public class EngineFormateTests
{
    private static Spieldaten D => Daten.Spiel;

    private static byte[] Testdatei(string name) => Daten.Lesen(TestdateiTests.Ordner + name) ?? throw new FileNotFoundException(name);

    private static string Ersetzt(string text, string alt, string neu)
    {
        Assert.Contains(alt, text, StringComparison.Ordinal);
        return text.Replace(alt, neu, StringComparison.Ordinal);
    }

    /// <summary>Stadt-Datei <c>.fistadt</c>: heute Format 1; beide echten Dateien lesbar, Format 2 ist ein Fehler.</summary>
    [Fact]
    public void Fistadt_NeuereNummerFehler_EchteDateienLesbar()
    {
        Assert.Equal(1, StadtFassung.FormatVersion);
        var f0 = Daten.Lesen(AktiveFassung.Ordner + StadtFassung.Dateiname(AktiveFassung.Stadt1, 0))!;
        foreach (var echt in new[] { f0, Testdatei("stadt1_fassung1.fistadt") })
        {
            Assert.Contains("\"formatversion\": 1,", Encoding.UTF8.GetString(echt), StringComparison.Ordinal);
            Assert.Equal(AktiveFassung.Stadt1, StadtFassung.Lesen(echt).StadtId);
        }
        var neuer = Ersetzt(Encoding.UTF8.GetString(f0), "\"formatversion\": 1,", "\"formatversion\": 2,");
        var fehler = Assert.Throws<StadtbauFehler>(() => StadtFassung.Lesen(Encoding.UTF8.GetBytes(neuer)));
        Assert.Contains(fehler.Funde, t => t.Contains("neueren Fassung des Formats", StringComparison.Ordinal));
    }

    /// <summary>
    /// Projekt <c>.fisbau</c>: heute Format 3; Format 1 (S 0.1.0) und 2 (S 0.4.0) oeffnen sich zum Bearbeiten mit dem alten
    /// Inhalt. Eine neuere Nummer oeffnet nur zum Lesen (<see cref="Projekt.NurLesen"/>, die Oberflaeche sagt es, die Datei
    /// bleibt Byte fuer Byte gleich), nie still ueberschrieben.
    /// </summary>
    [Fact]
    public void Fisbau_NeuereNummerNurLesen_EchteDateienLesbar()
    {
        Assert.Equal(3, Projekt.FormatVersion);
        var ablage = Daten.NeueAblage();
        Directory.CreateDirectory(ablage.Projekte);
        Projekt Oeffnen(string name, byte[] daten)
        {
            var datei = Path.Combine(ablage.Projekte, name + Projekt.Endung);
            File.WriteAllBytes(datei, daten);
            return Projekt.Oeffnen(datei, D, ablage);
        }

        var v1 = Testdatei("projekt_S0.1.0.fisbau");
        Assert.Contains("\"formatversion\": 1,", Encoding.UTF8.GetString(v1), StringComparison.Ordinal);
        Assert.Contains("Fachinformatiker Stadtbau S 0.1.0", Encoding.UTF8.GetString(v1), StringComparison.Ordinal);
        var p1 = Oeffnen("Probe S010", v1);
        Assert.False(p1.NurLesen);
        Assert.Equal("elektromarkt", p1.Ort);
        Assert.Equal(["elektromarkt"], p1.GeaenderteOrte);
        Assert.False(p1.AussenGeaendert);
        // der gespeicherte Schritt (Regal gedreht) ist da und laesst sich zuruecknehmen
        var geaendert = PythonJson.Standard(p1.Grundriss("elektromarkt"));
        Assert.True(p1.Rueckgaengig());
        Assert.NotEqual(geaendert, PythonJson.Standard(p1.Grundriss("elektromarkt")));
        Assert.Equal(PythonJson.Standard(p1.Original("elektromarkt")), PythonJson.Standard(p1.Grundriss("elektromarkt")));

        var v2 = Testdatei("projekt_S0.4.0.fisbau");
        Assert.Contains("\"formatversion\": 2,", Encoding.UTF8.GetString(v2), StringComparison.Ordinal);
        Assert.False(Oeffnen("Projekt aus S 040", v2).NurLesen);

        var neuText = Ersetzt(Encoding.UTF8.GetString(v1), "\"formatversion\": 1,", "\"formatversion\": " + (Projekt.FormatVersion + 1) + ",");
        var neu = Encoding.UTF8.GetBytes(neuText);
        var pn = Oeffnen("Neuer", neu);
        Assert.True(pn.NurLesen);
        Assert.Equal(neu, File.ReadAllBytes(pn.Datei));
    }

    /// <summary>
    /// Blueprint <c>.fisblueprint</c>: heute Format 3; die echten Dateien 1 (S 0.8.0) und 2 (S 0.18.0) sind lesbar.
    /// Eine neuere Nummer ist ein klarer Fehler (<see cref="Texte.BlueprintNeuer"/>), die Datei bleibt in der Liste stehen.
    /// </summary>
    [Fact]
    public void Fisblueprint_NeuereNummerFehler_EchteDateienLesbar()
    {
        Assert.Equal(3, Blueprint.FormatVersion);
        var v1 = Testdatei("blueprint_S0.8.0.fisblueprint");
        var v2 = Testdatei("blueprint_S0.18.0.fisblueprint");
        Assert.Contains("\"formatversion\": 1,", Encoding.UTF8.GetString(v1), StringComparison.Ordinal);
        Assert.Contains("\"formatversion\": 2,", Encoding.UTF8.GetString(v2), StringComparison.Ordinal);
        Assert.NotNull(Blueprint.Lesen(v1).Modell);
        Assert.NotNull(Blueprint.Lesen(v2).Modell);

        var ablage = Daten.NeueAblage();
        Directory.CreateDirectory(ablage.Blueprints);
        var neu = Ersetzt(Encoding.UTF8.GetString(v2), "\"formatversion\": 2,", "\"formatversion\": " + (Blueprint.FormatVersion + 1) + ",");
        File.WriteAllText(Path.Combine(ablage.Blueprints, "neuer" + Blueprint.Endung), neu, new UTF8Encoding(false));
        var eintrag = Assert.Single(Blueprint.Alle(ablage));
        Assert.True(eintrag.Neuer);
        Assert.Equal(Texte.BlueprintNeuer, eintrag.Fehler);
        Assert.ThrowsAny<Exception>(() => Blueprint.Lesen(Encoding.UTF8.GetBytes(neu)));
    }

    /// <summary>
    /// Grafik FLGM (<c>modelle.bin</c>, <c>katalog_verbaut.bin</c>, Pakete des Katalogs): heute Version 2 mit Eckfarben.
    /// Die echte Datei der Version 1 (S 0.4.0) laedt, jede Ecke hat dort die Farbe ihres Dreiecks. Version 3 ist ein Fehler.
    /// </summary>
    [Fact]
    public void Flgm_NeuereVersionFehler_EchteDateiVersion1Lesbar()
    {
        Assert.Equal(2u, GrafikBibliothek.Version);
        var bin = Testdatei("katalog_verbaut_S0.4.0.bin");
        Assert.Equal("FLGM", Encoding.ASCII.GetString(bin, 0, 4));
        Assert.Equal(1u, BitConverter.ToUInt32(bin, 4));
        var modelle = GrafikBibliothek.KatalogLaden(bin, Encoding.UTF8.GetString(Testdatei("katalog_verbaut_S0.4.0.json")));
        Assert.Equal(["cc0:car_kit/sedan", "cc0:furniture/chair", "cc0:particles/star_01"], modelle.Select(m => m.Name));
        foreach (var m in modelle)
        {
            Assert.Null(m.EckFarben);
            Assert.True(m.Farben.Length > 0);
        }

        // heutige Dateien sind Version 2
        var heute = Daten.Lesen("inhalte_csharp/grafik/modelle.bin")!;
        Assert.Equal(GrafikBibliothek.Version, BitConverter.ToUInt32(heute, 4));

        var drei = (byte[])bin.Clone();
        BitConverter.GetBytes(3u).CopyTo(drei, 4);
        var f = Assert.Throws<InvalidDataException>(() => GrafikBibliothek.Lesen(drei, "probe.bin"));
        Assert.Contains("unbekannter Aufbau", f.Message, StringComparison.Ordinal);
        // Gegenprobe: dieselben Bytes mit Version 1 laden
        Assert.Equal(3, GrafikBibliothek.Lesen(bin, "probe.bin").Count);
    }

    /// <summary>
    /// <c>katalog_verbaut.json</c>: hat keine eigene Formatnummer (Fund Sammelliste 286). Geschuetzt ist es heute durch
    /// die Pruefsumme der bin-Datei, die FLGM-Version darin und die Liste der Modelle; jede Abweichung ist ein klarer Fehler.
    /// </summary>
    [Fact]
    public void KatalogVerbaut_PruefsummeUndListe_KlarerFehler()
    {
        var bin = Testdatei("katalog_verbaut_S0.4.0.bin");
        var json = Encoding.UTF8.GetString(Testdatei("katalog_verbaut_S0.4.0.json"));
        Assert.Throws<InvalidDataException>(() => GrafikBibliothek.KatalogLaden(bin, Ersetzt(json, "\"sha256\": \"d", "\"sha256\": \"e")));
        Assert.Throws<InvalidDataException>(() => GrafikBibliothek.KatalogLaden(bin, Ersetzt(json, "  \"cc0:furniture/chair\",\n", "")));
        // heute ohne Formatnummer: das Feld wuerde still uebergangen (darum Fund 286)
        Assert.DoesNotContain("format", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>aktiv.json</c>: hat keine eigene Formatnummer (Fund Sammelliste 286). Jeder Wert, der keine ganze Zahl ist, ist ein
    /// klarer Fehler; eine fehlende Fassung auch.
    /// </summary>
    [Fact]
    public void AktivJson_UnbekannterWert_KlarerFehler()
    {
        var aktiv = (PyObjekt)Py.Lesen(Daten.Lesen(AktiveFassung.Ordner + AktiveFassung.Datei)!)!;
        Assert.Equal(0, AktiveFassung.Nummer(aktiv, AktiveFassung.Stadt1));
        foreach (var falsch in new[] { "{\"stadt1\": {\"nummer\": 0}}", "{\"stadt1\": \"0\"}", "{\"stadt1\": 0.0}", "{}" })
        {
            Assert.Throws<StadtbauFehler>(() => AktiveFassung.Nummer((PyObjekt)Py.Lesen(falsch)!, AktiveFassung.Stadt1));
        }
        var fehlt = Assert.Throws<StadtbauFehler>(() => AktiveFassung.Laden(p => p.EndsWith(AktiveFassung.Datei, StringComparison.Ordinal)
            ? Encoding.UTF8.GetBytes("{\"stadt1\": 7}") : Daten.Lesen(p)));
        Assert.Contains(fehlt.Funde, t => t.Contains("stadt1_fassung7.fistadt", StringComparison.Ordinal));
    }

    /// <summary>
    /// Katalog des Werkzeugs (<c>katalog.json</c>, <c>namen_de.json</c>): heute Format 1, jede andere Nummer ist ein Fehler.
    /// Es gibt keine aeltere Fassung.
    /// </summary>
    [Fact]
    public void Katalog_AndereNummerFehler()
    {
        Assert.NotNull(Katalog.Laden(Daten.Lesen));
        Func<string, byte[]?> Mit(string datei, string alt, string neu) => p => p.EndsWith(datei, StringComparison.Ordinal)
            ? Encoding.UTF8.GetBytes(Ersetzt(Encoding.UTF8.GetString(Daten.Lesen(p)!), alt, neu)) : Daten.Lesen(p);
        var a = Assert.Throws<InvalidDataException>(() => Katalog.Laden(Mit("katalog.json", "\"format\": 1", "\"format\": 2")));
        Assert.Contains("unbekanntes Format", a.Message, StringComparison.Ordinal);
        var b = Assert.Throws<InvalidDataException>(() => Katalog.Laden(Mit(Katalog.NamenDatei, "\"format\": 1", "\"format\": 2")));
        Assert.Contains("unbekanntes Format", b.Message, StringComparison.Ordinal);
    }
}
