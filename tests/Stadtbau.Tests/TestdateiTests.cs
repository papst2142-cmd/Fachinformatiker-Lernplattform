using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Tests;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Phase 13 Welle 4a (Weisung P12-28): Einbau-Weg mit einer Testdatei "Fassung 1" (Stadt 1 mit Gehweg, Stuecken aus dem
/// Katalog innen und draussen, einem Effekt innen und draussen und der verschobenen Laterne 20). Die Datei entsteht mit dem
/// Werkzeug selbst (Export eines Projekts), die mitgelieferten Modelle dazu mit <c>werkzeuge/stadtbau/verbaut.py</c>
/// (Ordner <c>tests/Stadtbau.Tests/Testdaten</c>). Geprueft: Format, Pruefsumme, Ladepruefung des Spiels, aktiv.json = 1 nur
/// im Test (die mitgelieferte aktiv.json bleibt 0), die mitgelieferten Modelle gleich denen des Werkzeugs.
/// </summary>
public class TestdateiTests
{
    private static Spieldaten D => Daten.Spiel;

    private static readonly Lazy<Katalog> Geteilt = new(() => Katalog.Laden(Daten.Lesen)!);

    private static Katalog K => Geteilt.Value;

    public const string Ordner = "tests/Stadtbau.Tests/Testdaten/";

    public const string Datei = Ordner + "stadt1_fassung1.fistadt";

    /// <summary>Der Effekt der Testdatei (ein Stern aus dem Paket particles, fest benannt).</summary>
    public const string Effekt = "particles/star_01";

    private static readonly DateTimeOffset Zeit = DateTimeOffset.Parse("2026-11-06T15:00:00+01:00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Die Testdatei, wie das Werkzeug sie heute exportiert.</summary>
    public static byte[] Erzeugen()
    {
        var p = Projekt.Neu("Testdatei Fassung 1", D, Daten.NeueAblage(), null, null);
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        // Gehweg an der langen Strasse x 7,5 (an den anderen stuenden Haeuser auf dem Gehweg: Regel der Pruefung)
        var strasse = AussenBearbeitung.Alle(p.AussenStand).First(a => a.Stelle is { Art: AussenArt.Strasse, Nummer: 4 }).Stelle;
        p.AussenAendern("Gehweg", a => AussenBearbeitung.GehwegSetzen(a, strasse, AussenBearbeitung.NeuerGehweg));
        p.AussenAendern("Auto", a => AussenBearbeitung.Setzen(a, AussenArt.Katalog, 2, 7, modell: "car_kit/sedan"));
        p.AussenAendern("Effekt", a => AussenBearbeitung.Setzen(a, AussenArt.Katalog, 2, 12, modell: Effekt));
        p.Aendern("moebelhaus", "Stuhl", g => Bearbeitung.Setzen(g, Katalogstueck.Praefix + "furniture/chair", 1.5, 11.5));
        p.Aendern("moebelhaus", "Effekt", g => Bearbeitung.Setzen(g, Katalogstueck.Praefix + Effekt, 3.5, 11.5));
        return p.Exportieren(Zeit);
    }

    private static PyObjekt OhneWerkzeug(byte[] datei)
    {
        var o = (PyObjekt)Py.Lesen(datei)!;
        o.RemoveAll(e => e.Key is "werkzeug" or "pruefsumme");
        return o;
    }

    [Fact]
    public void Testdatei_GleichDemExportDesWerkzeugs()
    {
        var neu = Erzeugen();
        if (Environment.GetEnvironmentVariable("STADTBAU_TESTDATEI_SCHREIBEN") == "1")
        {
            File.WriteAllBytes(Path.Combine(Daten.Wurzel, Datei), neu);
        }
        var alt = Daten.Lesen(Datei) ?? throw new FileNotFoundException(Datei);
        // die Versionsnummer des Werkzeugs (und damit die Pruefsumme) darf sich aendern, alles andere nicht
        Assert.Equal(PythonJson.Text(OhneWerkzeug(neu), true), PythonJson.Text(OhneWerkzeug(alt), true));
    }

    [Fact]
    public void Testdatei_FormatPruefsummeInhalt_EnthaeltAllesDerWelle()
    {
        var datei = Daten.Lesen(Datei)!;
        Assert.Empty(StadtbauPruefung.Format(Py.Lesen(datei)));
        var f = StadtFassung.Lesen(datei);
        Assert.Equal(1, f.Nummer);
        Assert.Equal(0, f.Basis);
        Assert.Empty(StadtbauPruefung.Inhalt(f, D.Inhalt));
        var aussen = Aussen.Aus(f);
        Assert.Contains(Aussen.Landschaft(aussen), t => Py.Gleich(Objekt(t)["typ"], "strasse") && StadtSzene.GehwegBreite(Objekt(t)) > 0);
        var draussen = Katalogstueck.AusStadtdaten(f.Stadtdaten);
        Assert.Contains(draussen, k => k.Typ == "cc0:car_kit/sedan");
        Assert.Contains(draussen, k => k.Effekt);
        var innen = new SortedSet<string>(StringComparer.Ordinal);
        StadtbauPruefung.KatalogVerweise(f.Innenraeume, innen);
        Assert.Contains("cc0:furniture/chair", innen);
        Assert.Contains(Katalogstueck.Praefix + Effekt, innen);
        Assert.Equal(15.16, Py.F(((List<object?>)Aussen.Laternen(aussen)[19]!)[1]), 6);
        // Gegenprobe: eine falsche Pruefsumme ist ein Fund
        var falsch = (PyObjekt)Py.Lesen(datei)!;
        Py.Setzen(falsch, "pruefsumme", new string('0', 64));
        Assert.NotEmpty(StadtbauPruefung.Format(falsch));
    }

    /// <summary>Lesen wie das Spiel, nur aktiv.json zeigt im Test auf Fassung 1 (die mitgelieferte bleibt 0).</summary>
    private static byte[]? LesenMitFassung1(string pfad) => pfad switch
    {
        "inhalte_csharp/spiel/stadtbau/aktiv.json" => "{\"stadt1\": 1}"u8.ToArray(),
        "inhalte_csharp/spiel/stadtbau/stadt1_fassung1.fistadt" => Daten.Lesen(Datei),
        _ => Daten.Lesen(pfad),
    };

    private static GrafikBibliothek Verbaut(string ordner) =>
        GrafikBibliothek.Aus(GrafikBibliothek.KatalogLaden(Daten.Lesen(ordner + "katalog_verbaut.bin")!,
            System.Text.Encoding.UTF8.GetString(Daten.Lesen(ordner + "katalog_verbaut.json")!)));

    [Fact]
    public void Einbau_Aktiv1NurImTest_LaedtMitDenMitgeliefertenModellen_OhneRot()
    {
        // die mitgelieferte aktiv.json bleibt 0 (Weisung P12-28: Fassung 0 sichtbar unveraendert)
        Assert.Equal(0, AktiveFassung.Laden(Daten.Lesen).Nummer);
        Assert.Null(Daten.Lesen("inhalte_csharp/spiel/stadtbau/stadt1_fassung1.fistadt"));
        var f = AktiveFassung.Laden(LesenMitFassung1);
        Assert.Equal(1, f.Nummer);
        Assert.Empty(StadtbauPruefung.Verbaut(f, Verbaut(Ordner)));
        // Gegenprobe: mit den heute mitgelieferten Modellen (keine Stuecke) ist jedes Stueck ein roter Fund, nie still weg
        var rot = StadtbauPruefung.Verbaut(f, Verbaut("inhalte_csharp/grafik/"));
        Assert.Contains(rot, x => x.Contains("cc0:car_kit/sedan", StringComparison.Ordinal) && x.StartsWith("Draußen", StringComparison.Ordinal));
        Assert.Contains(rot, x => x.Contains("cc0:furniture/chair", StringComparison.Ordinal) && x.StartsWith("Innen", StringComparison.Ordinal));
        Assert.Contains(rot, x => x.Contains(Effekt, StringComparison.Ordinal));
        Assert.NotEmpty(StadtbauPruefung.Verbaut(f, null));
    }

    /// <summary>
    /// Die mitgelieferten Modelle in <paramref name="ordner"/> sind genau die Stuecke, auf die die Fassungen in
    /// <paramref name="fassungen"/> verweisen, mit genau den Ecken und Farben des Werkzeugs (Effekt = Stern des Spiels).
    /// </summary>
    private static void GleichDenFassungen(IEnumerable<StadtFassung> fassungen, string ordner)
    {
        var namen = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var f in fassungen)
        {
            StadtbauPruefung.KatalogVerweise(f.Innenraeume, namen);
            foreach (var k in Katalogstueck.AusStadtdaten(f.Stadtdaten))
            {
                namen.Add(k.Typ);
            }
        }
        var modelle = GrafikBibliothek.KatalogLaden(Daten.Lesen(ordner + "katalog_verbaut.bin")!,
            System.Text.Encoding.UTF8.GetString(Daten.Lesen(ordner + "katalog_verbaut.json")!));
        Assert.Equal(namen.ToList(), modelle.Select(m => m.Name).ToList());
        foreach (var m in modelle)
        {
            var soll = K.SpielModell(K.Eintrag(m.Name)!)!;
            Assert.Equal(soll.Ecken, m.Ecken);
            Assert.Equal(soll.Farben, m.Farben);
        }
    }

    [Fact]
    public void Verbaut_TestdateiGleichDemWerkzeug_Bytegleich()
    {
        GleichDenFassungen([StadtFassung.Lesen(Daten.Lesen(Datei)!)], Ordner);
        // der Effekt hat genau die zwei Dreiecke des Sterns
        var stern = GrafikBibliothek.KatalogLaden(Daten.Lesen(Ordner + "katalog_verbaut.bin")!,
            System.Text.Encoding.UTF8.GetString(Daten.Lesen(Ordner + "katalog_verbaut.json")!)).Single(m => Katalogstueck.IstEffekt(m.Name));
        Assert.Equal(Katalogstueck.EffektDreiecke, stern.Dreiecke);
    }

    [Fact]
    public void Verbaut_RepoGleichDenFassungen()
    {
        var ordner = Path.Combine(Daten.Wurzel, "inhalte_csharp/spiel/stadtbau");
        var fassungen = Directory.GetFiles(ordner, "*" + StadtFassung.Endung).Order(StringComparer.Ordinal)
            .Select(p => StadtFassung.Lesen(File.ReadAllBytes(p))).ToList();
        Assert.NotEmpty(fassungen);
        GleichDenFassungen(fassungen, "inhalte_csharp/grafik/");
    }

    [Fact]
    public void Verbaut_Gegenprobe_FalschePruefsummeOderFremderName_Wirft()
    {
        var bin = Daten.Lesen(Ordner + "katalog_verbaut.bin")!;
        var index = System.Text.Encoding.UTF8.GetString(Daten.Lesen(Ordner + "katalog_verbaut.json")!);
        Assert.NotEmpty(GrafikBibliothek.KatalogLaden(bin, index));
        var kaputt = (byte[])bin.Clone();
        kaputt[^1] ^= 1;
        Assert.ThrowsAny<InvalidDataException>(() => GrafikBibliothek.KatalogLaden(kaputt, index));
        Assert.ThrowsAny<InvalidDataException>(() => GrafikBibliothek.KatalogLaden(bin, index.Replace("cc0:car_kit/sedan", "cc0:car_kit/andere", StringComparison.Ordinal)));
    }

    [Fact]
    public void Effekte_GenauDasPaketParticles()
    {
        Assert.Contains(K.Eintraege, e => e.Art == KatalogArt.Effekt);
        foreach (var e in K.Eintraege)
        {
            Assert.Equal(e.Paket == "particles", e.Art == KatalogArt.Effekt);
            Assert.Equal(Katalogstueck.IstEffekt(e.Typ), e.Art == KatalogArt.Effekt);
            Assert.True(Katalogstueck.IstTyp(e.Typ));
        }
        Assert.Equal(Katalogstueck.Praefix, Katalog.Praefix);
    }

    /// <summary>
    /// Die 3D-Ansicht des Werkzeugs zeichnet die Testdatei genau wie das Spiel mit den mitgelieferten Modellen (gleiche
    /// Pruefsumme der ganzen Szene, Tag und Grenzen-Ansicht); Gegenprobe: ohne die Stuecke ist die Szene eine andere.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ansicht3D_GleichDemSpiel(bool grenzen)
    {
        var f = StadtFassung.Lesen(Daten.Lesen(Datei)!);
        var e = grenzen ? Ansicht3D.Einstellung.Grenzen : Ansicht3D.Einstellung.Standard;
        var werkzeug = Ansicht3D.Bauen(f, D, e).Szene;
        Assert.Contains(werkzeug.Stuecke, m => m.Name == "cc0:car_kit/sedan");
        Assert.Contains(werkzeug.Stuecke, m => m.Name == Katalogstueck.Praefix + Effekt);
        var stadt = f.StadtFuer(f.MitKarte(D.Inhalt));
        var stand = StadtbauPruefung.Probestand(stadt.Inhalt).Stand;
        var spiel = StadtSzene.Bauen(stadt, stand, D.Grafik!.Mit(GrafikBibliothek.KatalogLaden(Daten.Lesen(Ordner + "katalog_verbaut.bin")!,
            System.Text.Encoding.UTF8.GetString(Daten.Lesen(Ordner + "katalog_verbaut.json")!))), e.Fest, D.Feste, e.Jahreszeit);
        Assert.Equal(StadtSzeneHash.Stadt(spiel), StadtSzeneHash.Stadt(werkzeug));
        var ohne = StadtSzene.Bauen(stadt, stand, D.Grafik, e.Fest, D.Feste, e.Jahreszeit);
        Assert.NotEqual(StadtSzeneHash.Stadt(ohne), StadtSzeneHash.Stadt(werkzeug));
    }
}
