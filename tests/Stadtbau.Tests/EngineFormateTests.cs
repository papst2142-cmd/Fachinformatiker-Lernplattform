using System.Globalization;
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
/// <c>katalog_verbaut_S0.4.0.bin/.json</c> (FLGM 1 und Liste ohne Formatnummer, Testdatei aus S 0.4.0, main 365533e),
/// <c>aktiv_A0.62.0.json</c> (ohne Formatnummer, aus main a808a3a), <c>stadt1_fassung1.fistadt</c> und die Fassung 0 im Repo
/// (fistadt 1).
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

    /// <summary>
    /// Stadt-Datei <c>.fistadt</c>: heute hoechstens Format 3 (nur Karten ueber 64 oder mit Stadtteil-Namen, Welle Nachladen;
    /// 2 nur mit Arbeitsort, Welle E4, Einzelheiten in <c>ArbeitsortFormatTests</c>); beide echten Dateien der Nummer 1 und die
    /// echte Datei der Nummer 2 (S 0.53.0) lesbar. Seit S 0.57.0 (Weisung P12-297) Format 4 nur fuer eine Paket-Stadt; die echte
    /// Datei der Nummer 3 (S 0.55.0) bleibt lesbar. Seit S 0.64.0 (Weisung P12-381) Format 5 nur mit geaenderten Raeumen des
    /// Spiels; die echte Datei der Nummer 4 (Paket-Stadt, mit dem Code von S 0.63.0, main 5913abc, geschrieben) bleibt lesbar,
    /// Format 6 ist ein Fehler.
    /// </summary>
    [Fact]
    public void Fistadt_NeuereNummerFehler_EchteDateienLesbar()
    {
        Assert.Equal(5, StadtFassung.FormatVersion);
        var vier = Testdatei("stadt_S0.63.0_paket.fistadt");
        Assert.Contains("\"formatversion\": 4,", Encoding.UTF8.GetString(vier), StringComparison.Ordinal);
        Assert.Contains("Fachinformatiker Stadtbau S 0.63.0", Encoding.UTF8.GetString(vier), StringComparison.Ordinal);
        Assert.Equal(4, StadtFassung.FormatFuer(StadtFassung.Lesen(vier).Stadtdaten, StadtFassung.Lesen(vier).Karte));
        Assert.Equal(vier, StadtFassung.Lesen(vier).Schreiben());
        var drei = Testdatei("stadt_S0.55.0_namen.fistadt");
        Assert.Contains("\"formatversion\": 3,", Encoding.UTF8.GetString(drei), StringComparison.Ordinal);
        Assert.Equal(3, StadtFassung.FormatFuer(StadtFassung.Lesen(drei).Stadtdaten, StadtFassung.Lesen(drei).Karte));
        var f0 = Daten.Lesen(AktiveFassung.Ordner + StadtFassung.Dateiname(AktiveFassung.Stadt1, 0))!;
        foreach (var echt in new[] { f0, Testdatei("stadt1_fassung1.fistadt") })
        {
            Assert.Contains("\"formatversion\": 1,", Encoding.UTF8.GetString(echt), StringComparison.Ordinal);
            Assert.Equal(AktiveFassung.Stadt1, StadtFassung.Lesen(echt).StadtId);
        }
        var zwei = Testdatei("stadt_S0.53.0_arbeitsort.fistadt");
        Assert.Contains("\"formatversion\": 2,", Encoding.UTF8.GetString(zwei), StringComparison.Ordinal);
        Assert.Equal(2, StadtFassung.FormatFuer(StadtFassung.Lesen(zwei).Stadtdaten, StadtFassung.Lesen(zwei).Karte));
        var neuer = Ersetzt(Encoding.UTF8.GetString(f0), "\"formatversion\": 1,", "\"formatversion\": 6,");
        var fehler = Assert.Throws<StadtbauFehler>(() => StadtFassung.Lesen(Encoding.UTF8.GetBytes(neuer)));
        Assert.Contains(fehler.Funde, t => t.Contains("neueren Fassung des Formats", StringComparison.Ordinal));
    }

    /// <summary>
    /// Projekt <c>.fisbau</c>: heute hoechstens Format 5 (nur leere Stadt ueber 64, Welle Nachladen), 4 nur mit Arbeitsort
    /// (Welle E4, echte Datei S 0.53.0 oeffnet sich zum Bearbeiten), sonst 3; Format 1 (S 0.1.0) und 2 (S 0.4.0) oeffnen sich zum Bearbeiten mit dem alten
    /// Inhalt. Eine neuere Nummer oeffnet nur zum Lesen (<see cref="Projekt.NurLesen"/>, die Oberflaeche sagt es, die Datei
    /// bleibt Byte fuer Byte gleich), nie still ueberschrieben.
    /// </summary>
    [Fact]
    public void Fisbau_NeuereNummerNurLesen_EchteDateienLesbar()
    {
        Assert.Equal(7, Projekt.FormatVersion);
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

        var v4 = Testdatei("projekt_S0.53.0_arbeitsort.fisbau");
        Assert.Contains("\"formatversion\": 4,", Encoding.UTF8.GetString(v4), StringComparison.Ordinal);
        Assert.False(Oeffnen("Projekt aus S 0530", v4).NurLesen);

        var v5 = Testdatei("projekt_S0.55.0_namen.fisbau");
        Assert.Contains("\"formatversion\": 5,", Encoding.UTF8.GetString(v5), StringComparison.Ordinal);
        Assert.False(Oeffnen("Projekt aus S 0550", v5).NurLesen);

        // S 0.64.0 (Weisung P12-381): echte Datei der Nummer 6 (Paket-Stadt, S 0.63.0) oeffnet sich zum Bearbeiten
        var v6 = Testdatei("projekt_S0.63.0_paket.fisbau");
        Assert.Contains("\"formatversion\": 6,", Encoding.UTF8.GetString(v6), StringComparison.Ordinal);
        var p6 = Oeffnen("Lindenau", v6);
        Assert.False(p6.NurLesen);
        Assert.Equal(("lindenau", "Lindenau am See"), p6.Paket);

        var neuText = Ersetzt(Encoding.UTF8.GetString(v1), "\"formatversion\": 1,", "\"formatversion\": " + (Projekt.FormatVersion + 1) + ",");
        var neu = Encoding.UTF8.GetBytes(neuText);
        var pn = Oeffnen("Neuer", neu);
        Assert.True(pn.NurLesen);
        Assert.Equal(neu, File.ReadAllBytes(pn.Datei));
    }

    /// <summary>
    /// Welle Nachladen (S 0.54.0, Weisung P12-280, Nico 2b): eine leere Stadt bis 256 je Seite. Nur ueber 64 bekommt das
    /// Projekt die Formatnummer 5 (ein aelteres Werkzeug oeffnet es dann nur zum Lesen); bis 64 bleibt 3 wie bisher. Die
    /// gespeicherte grosse Stadt oeffnet sich wieder mit ihrer Groesse.
    /// </summary>
    [Fact]
    public void Fisbau_GrosseLeereStadt_Format5_BisVierundsechzigUnveraendert()
    {
        var ablage = Daten.NeueAblage();
        long Nummer(Projekt p)
        {
            p.Speichern(vonHand: true);
            var text = Encoding.UTF8.GetString(File.ReadAllBytes(p.Datei));
            var i = text.IndexOf("\"formatversion\": ", StringComparison.Ordinal) + "\"formatversion\": ".Length;
            return long.Parse(text[i..text.IndexOf(',', i)], CultureInfo.InvariantCulture);
        }
        Assert.Equal(3, Nummer(Projekt.Neu("Klein64", D, ablage, leer: (64, 64))));
        Assert.Equal(5, Nummer(Projekt.Neu("Breit", D, ablage, leer: (64.5, 16))));
        var gross = Projekt.Neu("Gross256", D, ablage, leer: (256, 128));
        Assert.Equal(5, Nummer(gross));
        var wieder = Projekt.Oeffnen(gross.Datei, D, ablage);
        Assert.False(wieder.NurLesen);
        Assert.Equal((256d, 128d), wieder.Leer);
        Assert.Throws<ArgumentException>(() => Projekt.Neu("Zu gross", D, ablage, leer: (256.5, 16)));
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
    /// <c>katalog_verbaut.json</c>: seit A 0.62.1 Format 1 im Feld „format“ (Weisung P12-176, Sammelliste 286). Eine
    /// neuere Nummer und ein falscher Wert sind klare Fehler; die echte Datei aus S 0.4.0 (ohne Feld) liest sich als Format 1.
    /// Dazu wie bisher: Pruefsumme der bin-Datei und Liste der Modelle. Gegenprobe: mit dem alten Leser
    /// (<see cref="GrafikBibliothek.KatalogFormatAltFuerTests"/>) ginge Format 2 still durch.
    /// </summary>
    [Fact]
    public void KatalogVerbaut_NeuereNummerFehler_EchteDateiOhneNummerLesbar()
    {
        var bin = Testdatei("katalog_verbaut_S0.4.0.bin");
        var json = Encoding.UTF8.GetString(Testdatei("katalog_verbaut_S0.4.0.json"));
        Assert.DoesNotContain("\"format\"", json, StringComparison.Ordinal);
        Assert.Equal(3, GrafikBibliothek.KatalogLaden(bin, json).Count);
        Assert.Throws<InvalidDataException>(() => GrafikBibliothek.KatalogLaden(bin, Ersetzt(json, "\"sha256\": \"d", "\"sha256\": \"e")));
        Assert.Throws<InvalidDataException>(() => GrafikBibliothek.KatalogLaden(bin, Ersetzt(json, "  \"cc0:furniture/chair\",\n", "")));
        string Mit(string wert) => Ersetzt(json, "\"sha256\":", "\"format\": " + wert + ",\n \"sha256\":");
        Assert.Equal(3, GrafikBibliothek.KatalogLaden(bin, Mit("1")).Count);
        var neuer = Assert.Throws<InvalidDataException>(() => GrafikBibliothek.KatalogLaden(bin, Mit("2")));
        Assert.Contains("Format 2", neuer.Message, StringComparison.Ordinal);
        Assert.Contains("aktualisieren", neuer.Message, StringComparison.Ordinal);
        foreach (var falsch in new[] { "0", "1.0", "\"1\"", "null", "-1" })
        {
            Assert.Throws<InvalidDataException>(() => GrafikBibliothek.KatalogLaden(bin, Mit(falsch)));
        }
        // die Dateien im Repo und die Testdatei des Werkzeugs tragen die Nummer (verbaut.py schreibt sie)
        foreach (var datei in new[] { "inhalte_csharp/grafik/katalog_verbaut.json", TestdateiTests.Ordner + "katalog_verbaut.json" })
        {
            Assert.Contains("\"format\": 1,", Encoding.UTF8.GetString(Daten.Lesen(datei)!), StringComparison.Ordinal);
        }
        // Gegenprobe: der alte Leser uebergeht das Feld still
        GrafikBibliothek.KatalogFormatAltFuerTests = true;
        try
        {
            Assert.Equal(3, GrafikBibliothek.KatalogLaden(bin, Mit("2")).Count);
        }
        finally
        {
            GrafikBibliothek.KatalogFormatAltFuerTests = false;
        }
    }

    /// <summary>
    /// <c>aktiv.json</c>: seit A 0.62.1 Format 1 im Feld „format“ (Weisung P12-176, Sammelliste 286, Sollwert-Aenderung
    /// Nr. 15). Eine neuere Nummer und ein falscher Wert sind klare Fehler; die echte Datei bis A 0.62.0
    /// (<c>aktiv_A0.62.0.json</c>, ohne Feld) liest sich als Format 1. Jeder Wert einer Stadt, der keine ganze Zahl ist, und
    /// eine fehlende Fassung sind ebenfalls Fehler. Gegenprobe: mit dem alten Leser ginge Format 2 still durch.
    /// </summary>
    [Fact]
    public void AktivJson_NeuereNummerFehler_EchteDateiOhneNummerLesbar()
    {
        var aktiv = (PyObjekt)Py.Lesen(Daten.Lesen(AktiveFassung.Ordner + AktiveFassung.Datei)!)!;
        Assert.Equal(1, AktiveFassung.FormatVon(aktiv));
        Assert.Equal(2, AktiveFassung.Nummer(aktiv, AktiveFassung.Stadt1));   // seit Patch A 0.75.2 (Sollwert-Aenderung Nr. 17) Nicos Stadt 1 Fassung 2
        var alt = Testdatei("aktiv_A0.62.0.json");
        Assert.DoesNotContain("\"format\"", Encoding.UTF8.GetString(alt), StringComparison.Ordinal);
        Assert.Equal(1, AktiveFassung.FormatVon((PyObjekt)Py.Lesen(alt)!));
        Func<string, byte[]?> Mit(byte[] inhalt) => p => p.EndsWith(AktiveFassung.Datei, StringComparison.Ordinal) ? inhalt : Daten.Lesen(p);
        Assert.Equal(0, AktiveFassung.Laden(Mit(alt)).Nummer);
        var zwei = Encoding.UTF8.GetBytes("{\"format\": 2, \"stadt1\": 0}");
        var neuer = Assert.Throws<StadtbauFehler>(() => AktiveFassung.Laden(Mit(zwei)));
        Assert.Contains(neuer.Funde, t => t.Contains("Format 2", StringComparison.Ordinal) && t.Contains("aktualisieren", StringComparison.Ordinal));
        foreach (var wert in new[] { "0", "1.0", "\"1\"", "null", "-1" })
        {
            Assert.Throws<StadtbauFehler>(() => AktiveFassung.Laden(Mit(Encoding.UTF8.GetBytes("{\"format\": " + wert + ", \"stadt1\": 0}"))));
        }
        Assert.Throws<StadtbauFehler>(() => AktiveFassung.Nummer(aktiv, AktiveFassung.FormatFeld));
        foreach (var falsch in new[] { "{\"stadt1\": {\"nummer\": 0}}", "{\"stadt1\": \"0\"}", "{\"stadt1\": 0.0}", "{}" })
        {
            Assert.Throws<StadtbauFehler>(() => AktiveFassung.Nummer((PyObjekt)Py.Lesen(falsch)!, AktiveFassung.Stadt1));
        }
        var fehlt = Assert.Throws<StadtbauFehler>(() => AktiveFassung.Laden(Mit(Encoding.UTF8.GetBytes("{\"stadt1\": 7}"))));
        Assert.Contains(fehlt.Funde, t => t.Contains("stadt1_fassung7.fistadt", StringComparison.Ordinal));
        // Gegenprobe: der alte Leser uebergeht das Feld still
        AktiveFassung.FormatAltFuerTests = true;
        try
        {
            Assert.Equal(0, AktiveFassung.Laden(Mit(zwei)).Nummer);
        }
        finally
        {
            AktiveFassung.FormatAltFuerTests = false;
        }
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
