using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 13 Welle 4a (Weisung P12-28): das Spiel zeichnet, was das Stadtbau-Werkzeug baut. Grundlage ist die Testdatei
/// "Fassung 1" des Werkzeugs (<c>tests/Stadtbau.Tests/Testdaten</c>: Gehweg an der Strasse x 7,5, Auto und Stern draussen,
/// Stuhl und Stern im Moebelhaus, Laterne 20 frei) mit ihren mitgelieferten Modellen. Geprueft: Gehweg gezeichnet und frei
/// zum Gehen, Stuecke gezeichnet, versperren den Weg (ein Effekt nicht), der Effekt leuchtet, kein neuer Zeichenaufruf und die
/// Grenzen 160 000 / 40 bleiben; Ladepruefung mit Gegenprobe. Die mitgelieferte Stadt bleibt Fassung 0.
/// </summary>
public sealed class StadtbauEinbauTests
{
    private const string Testdaten = "tests/Stadtbau.Tests/Testdaten/";

    private static byte[] Datei(string pfad) => File.ReadAllBytes(Path.Combine(Pfade.Wurzel, pfad));

    private static readonly Lazy<StadtFassung> Fassung1 = new(() => StadtFassung.Lesen(Datei(Testdaten + "stadt1_fassung1.fistadt")));

    private static List<GrafikModell> Verbaut(string ordner) =>
        GrafikBibliothek.KatalogLaden(Datei(ordner + "katalog_verbaut.bin"), System.Text.Encoding.UTF8.GetString(Datei(ordner + "katalog_verbaut.json")));

    private static readonly Lazy<GrafikBibliothek> Grafik1 = new(() => SpielGrafikTests.Bibliothek.Value.Mit(Verbaut(Testdaten)));

    private static StadtSzene Szene(StadtFassung f, GrafikBibliothek grafik, string? fest = null, Jahreszeit j = Jahreszeit.Sommer, Spielstand? stand = null) =>
        StadtSzene.Bauen(f.StadtFuer(f.MitKarte(SpielInhaltTests.Inhalt)), stand, grafik, fest, FestDekoTests.Daten.Value, j);

    private static StadtFassung Fassung0 => StadtbauFassungTests.Fassung0.Value;

    private static readonly Lazy<StadtSzene> Szene0 = new(() => Szene(Fassung0, Grafik1.Value));

    private static readonly Lazy<StadtSzene> Szene1 = new(() => Szene(Fassung1.Value, Grafik1.Value));

    private const string Auto = "cc0:car_kit/sedan";

    private const string Stern = "cc0:particles/star_01";

    private static (long Dreiecke, int Aufrufe) Gezeichnet(StadtSzene s)
    {
        var fertig = s.Vorbereiten();
        Netz?[] netze = [fertig.Stadt, .. fertig.Haeuser.Values, fertig.Gruen, fertig.PaketNah, fertig.PaketFern];
        var da = netze.OfType<Netz>().Where(n => n.Indizes.Count > 0).ToList();
        return (da.Sum(n => (long)n.Indizes.Count / 3), da.Count);
    }

    // ---- Ladepruefung -------------------------------------------------------------------------------------

    [Fact]
    public void Mitgeliefert_HeuteKeinStueck_Fassung0GiltWeiter()
    {
        // die Lernplattform liefert heute kein Stueck aus dem Katalog mit (nur Verbautes; Fassung 0 verweist auf keins)
        Assert.Empty(Verbaut("inhalte_csharp/grafik/"));
        Assert.Equal(0, Fassung0.Nummer);
        Assert.Empty(StadtbauPruefung.Verbaut(Fassung0, GrafikBibliothek.Aus(Verbaut("inhalte_csharp/grafik/"))));
        Assert.Empty(StadtbauPruefung.Verbaut(Fassung0, null));
    }

    [Fact]
    public void Ladepruefung_Fassung1_MitModellenGruen_OhneRot_Gegenprobe()
    {
        Assert.Empty(StadtbauPruefung.Verbaut(Fassung1.Value, GrafikBibliothek.Aus(Verbaut(Testdaten))));
        var rot = StadtbauPruefung.Verbaut(Fassung1.Value, GrafikBibliothek.Aus(Verbaut("inhalte_csharp/grafik/")));
        Assert.Equal(4, rot.Count);
        Assert.All(rot, f => Assert.Contains("nicht mitgeliefert", f, StringComparison.Ordinal));
        // falscher Aufbau draussen ist ein Fund (dreh 4)
        var o = (PyObjekt)Py.Lesen(Datei(Testdaten + "stadt1_fassung1.fistadt"))!;
        var stadtdaten = (PyObjekt)o["stadtdaten"]!;
        Py.Setzen((PyObjekt)((List<object?>)stadtdaten["katalog"]!)[0]!, "dreh", 4L);
        Assert.Throws<InvalidDataException>(() => Katalogstueck.AusStadtdaten(stadtdaten));
    }

    [Fact]
    public void GemeinsamePruefungInnen_KatalogNurMitModell()
    {
        var f = Fassung1.Value;
        var g = InnenGrundriss.Grundriss(f.Innenraeume, "moebelhaus");
        var personen = InnenGrundriss.Personen(f.Innenraeume, "moebelhaus");
        var mit = StadtbauPruefung.Innen("moebelhaus", g, personen, Grafik1.Value, null, f.Innenraeume);
        Assert.DoesNotContain(mit, x => x.Rot && x.Text.Contains("cc0:", StringComparison.Ordinal));
        // Gegenprobe: ohne mitgeliefertes Modell ist das Stueck rot
        var ohne = StadtbauPruefung.Innen("moebelhaus", g, personen, SpielGrafikTests.Bibliothek.Value, null, f.Innenraeume);
        Assert.Contains(ohne, x => x.Rot && x.Text.Contains("cc0:furniture/chair", StringComparison.Ordinal));
    }

    // ---- draussen ----------------------------------------------------------------------------------------

    [Fact]
    public void Gehweg_Gezeichnet_FreiZumGehen()
    {
        static int Gehwege(StadtSzene s) => s.Teile.Count(t => t.Art == TeilArt.Band && t.Y1 == StadtSzene.Hoehe.Gehweg);
        Assert.Equal(0, Gehwege(Szene0.Value));
        Assert.True(Gehwege(Szene1.Value) > 0);
        var band = Szene1.Value.Teile.First(t => t.Art == TeilArt.Band && t.Y1 == StadtSzene.Hoehe.Gehweg);
        Assert.Equal(StadtSzene.StrassenBreite + (2 * 0.35f), band.Y2, 3);
        // auf dem Gehweg (neben der Fahrbahn der Strasse x 7,5) kann die Figur gehen
        Assert.True(Szene1.Value.Laufraster.KannStehen(7.5 + (StadtSzene.StrassenBreite / 2) + 0.2, 4));
    }

    [Fact]
    public void Stuecke_Draussen_Gezeichnet_AutoVersperrt_SternNicht()
    {
        var s1 = Szene1.Value;
        Assert.True(s1.Laufraster.KannStehen(2, 12), "Stern");
        Assert.Contains(s1.Stuecke, m => m.Name == Auto);
        Assert.Contains(s1.Stuecke, m => m.Name == Stern);
        Assert.DoesNotContain(Szene0.Value.Stuecke, m => Katalogstueck.IstTyp(m.Name));
        Assert.True(Szene0.Value.Laufraster.KannStehen(2, 7));
        Assert.False(s1.Laufraster.KannStehen(2, 7));
        Assert.True(s1.Laufraster.KannStehen(2, 12));
        // der Stern schwebt ueber dem Boden in der Hoehe seiner Breite
        var stern = s1.Stuecke.First(m => m.Name == Stern);
        Assert.True(stern.Lage.Translation.Y >= Katalogstueck.EffektHoehe(0.3f, 0.3f) - 0.01f, stern.Lage.Translation.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        // Gegenprobe: ohne mitgelieferte Modelle zeichnet die Szene nichts (die Ladepruefung des Spiels laesst das nie zu)
        Assert.DoesNotContain(Szene(Fassung1.Value, SpielGrafikTests.Bibliothek.Value).Stuecke, m => Katalogstueck.IstTyp(m.Name));
    }

    [Theory]
    [InlineData(null, Jahreszeit.Sommer)]
    [InlineData(Festzeit.Weihnachten, Jahreszeit.Winter)]
    public void Effekt_Leuchtet_InJederJahreszeit(string? fest, Jahreszeit j)
    {
        static int Leuchtend(StadtSzene s)
        {
            var n = s.NetzBauen();
            return Enumerable.Range(0, n.AnzahlEcken).Count(e => Nachtlicht.Leuchtet(n.FarbeAn(e)));
        }
        var ohne = Leuchtend(Szene(Fassung0, Grafik1.Value, fest, j));
        var mit = Leuchtend(Szene(Fassung1.Value, Grafik1.Value, fest, j));
        // genau die 2 Dreiecke des Sterns kommen leuchtend dazu (das Auto leuchtet nicht)
        Assert.Equal(ohne + (Katalogstueck.EffektDreiecke * 3), mit);
    }

    [Fact]
    public void Grenzen_KeinNeuerZeichenaufruf_HoechstensGrenze()
    {
        // Gesamtansicht fuer die Grenzen: Winter, Weihnachten, spaeter Stand (alle Orte)
        var stand = StadtDarstellungTests.SpaeterStandFuerTests();
        var (d0, a0) = Gezeichnet(Szene(Fassung0, Grafik1.Value, Festzeit.Weihnachten, Jahreszeit.Winter, stand));
        var (d1, a1) = Gezeichnet(Szene(Fassung1.Value, Grafik1.Value, Festzeit.Weihnachten, Jahreszeit.Winter, stand));
        Assert.Equal(a0, a1);
        // dazu Auto und Stern, auf dem Gehweg faellt Kleingruen weg (deshalb kein fester Unterschied)
        Assert.NotEqual(d0, d1);
        Assert.True(d1 <= StadtbauGrenzen.Dreiecke, d1.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(a1 <= StadtbauGrenzen.Aufrufe);
    }

    // ---- innen ---------------------------------------------------------------------------------------------

    [Fact]
    public void Stuecke_Innen_Gezeichnet_StuhlVersperrt_SternNicht()
    {
        var f = Fassung1.Value;
        var stand = new Spielablauf(SpielInhaltTests.Inhalt).Stand;
        var innen = Innenraum.Bauen("geschaeft:moebelhaus", stand, f.Innenraeume, Grafik1.Value, SpielGeschaefteTests.Daten.Value)!;
        Assert.Contains(innen.Stuecke, m => m.Name == "cc0:furniture/chair");
        Assert.Contains(innen.Stuecke, m => m.Name == Stern);
        var vorher = Innenraum.Bauen("geschaeft:moebelhaus", stand, Fassung0.Innenraeume, Grafik1.Value, SpielGeschaefteTests.Daten.Value)!;
        Assert.Equal(StadtbauGrenzen.Innen(vorher).Aufrufe, StadtbauGrenzen.Innen(innen).Aufrufe);
        var g = InnenGrundriss.Grundriss(f.Innenraeume, "moebelhaus");
        var hindernisse = Wege.Hindernisse(g);
        var g0 = InnenGrundriss.Grundriss(Fassung0.Innenraeume, "moebelhaus");
        // genau der Stuhl kommt als Hindernis dazu, der Stern nicht
        Assert.Equal(Wege.Hindernisse(g0).Count + 1, hindernisse.Count);
        Assert.Contains(hindernisse, h => Math.Abs(h.Item1 - 1.0) < 1e-9 && Math.Abs(h.Item2 - 11.0) < 1e-9);
    }

    // ---- Laterne 20 ---------------------------------------------------------------------------------------

    [Fact]
    public void Fassung1_Laterne20ImSpaetenStandFrei()
    {
        var laternen = Szene(Fassung1.Value, Grafik1.Value, stand: StadtDarstellungTests.SpaeterStandFuerTests()).Laternen;
        Assert.Equal(22, laternen.Count);
        Assert.Empty(StadtDarstellungTests.SpaetZuWeit(laternen, "stadt1", 1));
        Assert.True(laternen[19].Verschoben <= StadtbauPruefung.LaterneVerschoben);
        Assert.True(Vector2.Distance(new Vector2(30.30f, 15.16f), laternen[19].Ziel) < 0.01f);
    }

    // ---- Lizenzen ------------------------------------------------------------------------------------------

    /// <summary>
    /// Pakete aus katalog_verbaut.json in <paramref name="ordner"/>, fuer die die Lernplattform keinen Lizenz-Bestandteil hat
    /// (gilt: "Grafik &lt;Titel&gt;" oder "Katalog &lt;Titel&gt;" ohne Feld programme bzw. mit "Lernplattform").
    /// </summary>
    private static List<string> OhneLizenz(string ordner)
    {
        using var index = System.Text.Json.JsonDocument.Parse(Datei(ordner + "katalog_verbaut.json"));
        using var bestandteile = System.Text.Json.JsonDocument.Parse(Datei("werkzeuge/lizenzen/bestandteile.json"));
        var fuerLernplattform = bestandteile.RootElement.GetProperty("zusaetze").EnumerateArray()
            .Where(e => !e.TryGetProperty("programme", out var p) || p.EnumerateArray().Any(x => x.GetString() == "Lernplattform"))
            .Select(e => e.GetProperty("id").GetString()!).ToHashSet(StringComparer.Ordinal);
        return [.. index.RootElement.GetProperty("pakete").EnumerateObject().Select(p => p.Value.GetString()!)
            .Where(t => !fuerLernplattform.Contains("Grafik " + t) && !fuerLernplattform.Contains("Katalog " + t))];
    }

    [Fact]
    public void Lizenz_JedesMitgelieferteStueckHatEinenBestandteil_Gegenprobe()
    {
        Assert.Empty(OhneLizenz("inhalte_csharp/grafik/"));
        // Gegenprobe mit der Testdatei: Auto und Stuhl haben einen Bestandteil der Lernplattform, der Stern (Particle Pack)
        // noch nicht; kommt ein Effekt ins Spiel, braucht die Lizenzliste vorher einen Eintrag
        Assert.Equal(["Kenney Particle Pack"], OhneLizenz(Testdaten));
    }
}
