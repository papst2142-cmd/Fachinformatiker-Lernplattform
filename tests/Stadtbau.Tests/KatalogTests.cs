using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Phase 13 Welle 3b (Weisung P12-19 Abschnitt 2, Antworten P12-23 1a 2a 3a): voller CC0-Katalog. Alle 23 Pakete mit
/// allen Modellen, je Paket eine Datei im Repo, Vorschaubilder, Partikel als Effekt; der Katalog laedt nur, was gezeigt
/// wird, prueft jede Datei mit ihrer Pruefsumme, die Modelle sind genau wie die Spielgrafik gewandelt; Pruefung und
/// Grenzen kennen die Katalog-Stuecke (Effekte versperren nichts, echte Dreiecke zaehlen).
/// </summary>
public class KatalogTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Katalog Neu(Func<string, byte[]?>? lesen = null) => Katalog.Laden(lesen ?? Daten.Lesen)!;

    private static readonly Lazy<Katalog> Geteilt = new(() => Neu());

    private static Katalog K => Geteilt.Value;

    [Fact]
    public void AllePakete_AlleGruppen_JedesPaketEineDatei()
    {
        Assert.Equal(23, K.PaketTitel.Count);
        Assert.Equal(1521, K.Eintraege.Count);
        Assert.Equal(11, K.Gruppen.Count);
        Assert.Equal(K.Eintraege.Count, K.Gruppen.Sum(K.AnzahlIn));
        foreach (var paket in K.PaketTitel.Keys)
        {
            Assert.NotNull(Daten.Lesen(Katalog.Ordner + paket + ".png"));
            var modelle = K.Eintraege.Where(e => e.Paket == paket).ToList();
            Assert.NotEmpty(modelle);
            if (modelle.All(e => e.Art == KatalogArt.Effekt))
            {
                Assert.Null(Daten.Lesen(Katalog.Ordner + paket + ".bin"));
            }
            else
            {
                Assert.NotNull(Daten.Lesen(Katalog.Ordner + paket + ".bin"));
            }
        }
        // Partikel sind Effekte (Antwort 3a), alles andere Modelle mit Dreiecken
        Assert.All(K.Eintraege.Where(e => e.Paket == "particles"), e => Assert.Equal(KatalogArt.Effekt, e.Art));
        Assert.All(K.Eintraege.Where(e => e.Art == KatalogArt.Modell), e => Assert.True(e.Dreiecke > 0, e.Id));
        Assert.Equal(K.Eintraege.Count, K.Eintraege.Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void Laden_LiestNurDasVerzeichnis()
    {
        var gelesen = new List<string>();
        var k = Neu(p =>
        {
            gelesen.Add(p);
            return Daten.Lesen(p);
        });
        Assert.Equal([Katalog.Ordner + "katalog.json", Katalog.NamenDatei], gelesen);
        Assert.Empty(k.GeleseneModellPakete);
        Assert.Empty(k.GeleseneVorschauPakete);
        _ = k.Vorschaubild("furniture");
        Assert.Equal(["furniture"], k.GeleseneVorschauPakete);
        Assert.Empty(k.GeleseneModellPakete);
        _ = k.Modell(k.Eintrag("furniture/chair")!);
        Assert.Equal(["furniture"], k.GeleseneModellPakete);
        Assert.Equal(4, gelesen.Count);
    }

    [Fact]
    public void Pruefsumme_FalscheDatei_Fehler_Gegenprobe()
    {
        var k = Neu(p => p.EndsWith("car_kit.bin", StringComparison.Ordinal) ? [.. Daten.Lesen(p)!, 0] : Daten.Lesen(p));
        Assert.Throws<InvalidDataException>(() => k.Modelle("car_kit"));
        Assert.NotEmpty(Neu().Modelle("car_kit"));
    }

    [Fact]
    public void Modelle_GenauWieDieSpielgrafik()
    {
        // jedes Modell, das auch das Spiel nutzt, ist im Katalog bytegleich (gleiche Wandlung, wandle_modelle.py)
        var index = JsonDocument.Parse(Daten.Lesen("inhalte_csharp/grafik/modelle.json")!);
        var gleich = 0;
        foreach (var m in index.RootElement.GetProperty("modelle").EnumerateArray())
        {
            var name = m.GetProperty("name").GetString()!;
            if (K.Eintrag(m.GetProperty("paket").GetString() + "/" + name) is not { } e)
            {
                continue;
            }
            var spiel = D.Grafik!.Modell(name)!;
            var katalog = K.Modell(e)!;
            Assert.Equal(spiel.Dreiecke, e.Dreiecke);
            Assert.Equal(spiel.Ecken, katalog.Ecken);
            Assert.Equal(spiel.Farben, katalog.Farben);
            Assert.Equal(spiel.EckFarben, katalog.EckFarben);   // S 0.9.1: auch die Farbe je Ecke (Weisung P12-50)
            gleich++;
        }
        Assert.Equal(49, gleich);
    }

    [Fact]
    public void Dreiecke_GleichDenModellen()
    {
        foreach (var paket in new[] { "car_kit", "kaykit_holiday", "characters", "roads" })
        {
            foreach (var e in K.Eintraege.Where(x => x.Paket == paket))
            {
                Assert.Equal(e.Dreiecke, K.Modell(e)!.Dreiecke);
            }
        }
    }

    [Theory]
    [InlineData("Stuhl", "furniture/chair")]
    [InlineData("chair", "furniture/chair")]
    [InlineData("Baum", "suburban/tree_large")]
    [InlineData("tree large", "suburban/tree_large")]
    [InlineData("Auto", "car_kit/sedan")]
    [InlineData("Möbel", "furniture/chair")]
    [InlineData("moebel", "furniture/chair")]
    public void Suche_DeutschUndEnglisch(string eingabe, string erwartet)
    {
        Assert.Contains(K.Suchen(eingabe), e => e.Id == erwartet);
    }

    [Fact]
    public void Suche_JedesWortMussPassen_LeerIstNichts()
    {
        Assert.Empty(K.Suchen(""));
        Assert.Empty(K.Suchen("stuhl xyzgibtesnicht"));
        Assert.All(K.Suchen("Baum", "Feste"), e => Assert.Equal("Feste", e.Gruppe));
    }

    // ---- Pruefung und Grenzen innen ----------------------------------------------------------------

    private static PyObjekt Grundriss(string ort) => InnenGrundriss.Grundriss(D.Fassung.Innenraeume, ort);

    private static (List<StadtbauFund> Funde, (long Dreiecke, int Aufrufe) Grenzen) Innen(Katalog? k, string ort, PyObjekt g) =>
        KatalogPruefung.Innen(k, ort, g, InnenGrundriss.Personen(D.Fassung.Innenraeume, ort), D.Grafik, D.Geschaefte, D.Fassung.Innenraeume);

    private static PyObjekt Stueck(string typ, double x, double y, double w, double h) => new()
    {
        { "typ", typ }, { "x", x }, { "y", y }, { "w", w }, { "h", h },
    };

    private static void Hinzu(PyObjekt g, PyObjekt d) => Liste(Objekt(g["flur"])["deko"]).Add(d);

    [Fact]
    public void Innen_BekanntesStueck_Gruen_Unbekanntes_Rot()
    {
        var g = Grundriss("moebelhaus");
        var vorher = Innen(K, "moebelhaus", g);
        Hinzu(g, Stueck("cc0:furniture/chair", 1.5, 11.5, 0.7, 0.7));
        var mit = Innen(K, "moebelhaus", g);
        Assert.DoesNotContain(mit.Funde, f => f.Rot);
        Assert.DoesNotContain(mit.Funde, f => f.Text.Contains("cc0:", StringComparison.Ordinal));
        // Grenzen: echte Dreiecke dazu, kein eigener Zeichenaufruf
        Assert.Equal(vorher.Grenzen.Dreiecke + 168, mit.Grenzen.Dreiecke);
        Assert.Equal(vorher.Grenzen.Aufrufe, mit.Grenzen.Aufrufe);
        // Gegenprobe: unbekannter Name rot, ohne Katalog jedes Katalog-Stueck rot
        Hinzu(g, Stueck("cc0:furniture/gibtesnicht", 3.5, 11.5, 0.7, 0.7));
        Assert.Contains(Innen(K, "moebelhaus", g).Funde, f => f.Rot && f.Text.Contains("cc0:furniture/gibtesnicht", StringComparison.Ordinal));
        Assert.Contains(Innen(null, "moebelhaus", g).Funde, f => f.Rot && f.Text.Contains("cc0:furniture/chair", StringComparison.Ordinal));
    }

    [Fact]
    public void Innen_StueckSteht_Ueberschneidung_Rot()
    {
        var g = Grundriss("moebelhaus");
        var (_, regal) = Bearbeitung.Alle(g).First(s => Py.Str(s.Deko["typ"]) == "regal" && s.Stelle.Bereich != "flur");
        var (stelle, _) = Bearbeitung.Alle(g).First(s => ReferenceEquals(s.Deko, regal));
        Liste(Bearbeitung.Bereich(g, stelle.Bereich)["deko"]).Add(Stueck("cc0:furniture/chair", Py.F(regal["x"]), Py.F(regal["y"]), 0.7, 0.7));
        var funde = Innen(K, "moebelhaus", g).Funde;
        Assert.True(funde.Any(f => f.Rot && f.Text.Contains("überschneiden", StringComparison.Ordinal) && f.Text.Contains("„" + K.Eintrag("furniture/chair")!.Anzeige + "“", StringComparison.Ordinal)
            && !f.Text.Contains("Chair", StringComparison.Ordinal)),
            string.Join(" | ", funde.Select(f => f.Text)));
    }

    [Fact]
    public void Innen_EffektVersperrtNichts_MussImHausLiegen()
    {
        var g = Grundriss("moebelhaus");
        var (_, regal) = Bearbeitung.Alle(g).First(s => Py.Str(s.Deko["typ"]) == "regal" && s.Stelle.Bereich != "flur");
        var (stelle, _) = Bearbeitung.Alle(g).First(s => ReferenceEquals(s.Deko, regal));
        var effekt = K.Eintraege.First(e => e.Art == KatalogArt.Effekt);
        var vorher = Innen(K, "moebelhaus", g);
        // genau auf dem Regal: keine Ueberschneidung, kein Weg zu
        Liste(Bearbeitung.Bereich(g, stelle.Bereich)["deko"]).Add(Stueck(effekt.Typ, Py.F(regal["x"]), Py.F(regal["y"]), 1, 1));
        var mit = Innen(K, "moebelhaus", g);
        Assert.DoesNotContain(mit.Funde, f => f.Rot);
        Assert.Equal(vorher.Grenzen.Dreiecke + KatalogPruefung.EffektDreiecke, mit.Grenzen.Dreiecke);
        // Gegenprobe: ausserhalb des Hauses rot
        Hinzu(g, Stueck(effekt.Typ, -3, 1, 1, 1));
        Assert.Contains(Innen(K, "moebelhaus", g).Funde, f => f.Rot && f.Text.Contains("nicht ganz im Haus", StringComparison.Ordinal));
    }

    [Fact]
    public void Innen_SetzenMitKatalogGroesse()
    {
        var g = Grundriss("moebelhaus");
        var stuhl = K.Eintrag("furniture/chair")!;
        var s = Bearbeitung.Setzen(g, stuhl.Typ, 1.5, 11.5);
        Assert.NotNull(s);
        var d = Bearbeitung.Alle(g).First(x => x.Stelle == s).Deko;
        Assert.Equal(stuhl.Typ, Py.Str(d["typ"]));
        Assert.Equal(Math.Round(stuhl.Innen[0], 2), Py.F(d["w"]), 2);
        // Sammelliste 222: der deutsche Name, nicht der englische
        Assert.Equal(stuhl.Anzeige, Bearbeitung.TeilName(stuhl.Typ));
        Assert.NotEqual(stuhl.Englisch, stuhl.Anzeige);
    }

    // ---- draussen ----------------------------------------------------------------------------------

    private static PyObjekt Stadt1() => Aussen.Aus(D.Fassung);

    [Fact]
    public void Aussen_SetzenSchiebenDrehenKopierenLoeschen()
    {
        var a = Stadt1();
        var baum = K.Eintrag("suburban/tree_large")!;
        var s = AussenBearbeitung.Setzen(a, AussenArt.Katalog, 16, 12, modell: baum.Id)!;
        Assert.Equal(AussenArt.Katalog, s.Art);
        Assert.Equal(baum.Id, AussenBearbeitung.KatalogId(a, s));
        var (w, t) = AussenBearbeitung.KatalogGroesse(a, s);
        Assert.Equal(Math.Round(baum.Aussen[0], 2), w, 2);
        Assert.Equal(Math.Round(baum.Aussen[1], 2), t, 2);
        Assert.Empty(KatalogPruefung.Aussen(K, a));
        Assert.Equal(baum.Dreiecke, KatalogPruefung.DreieckeAussen(K, a));
        AussenBearbeitung.Verschieben(a, s, 1, 0);
        Assert.Equal((17.0, 12.0), AussenBearbeitung.Lage(a, s));
        Assert.True(AussenBearbeitung.Drehen(a, s));
        Assert.Equal((t, w), AussenBearbeitung.KatalogGroesse(a, s));
        var kopie = AussenBearbeitung.Kopieren(a, s);
        Assert.NotNull(kopie);
        Assert.Equal(2 * baum.Dreiecke, KatalogPruefung.DreieckeAussen(K, a));
        Assert.True(AussenBearbeitung.Loeschen(a, kopie!));
        Assert.True(AussenBearbeitung.Loeschen(a, s));
        Assert.Empty(Aussen.KatalogStuecke(a));
        // die Fassung 0 hat keinen Abschnitt "katalog" (die Lernplattform sieht unveraendert aus)
        Assert.False(Py.Hat(Aussen.Stadtdaten(Stadt1()), "katalog"));
    }

    [Fact]
    public void Aussen_Pruefung_RandGroesseName_Rot_Gegenprobe()
    {
        var a = Stadt1();
        var s = AussenBearbeitung.Setzen(a, AussenArt.Katalog, 16, 12, modell: "car_kit/sedan")!;
        Assert.Empty(KatalogPruefung.Aussen(K, a));
        var (breite, _) = Aussen.Groesse(a);
        AussenBearbeitung.Platzieren(a, s, breite - 0.1, 12);
        Assert.Contains(KatalogPruefung.Aussen(K, a), f => f.Rot && f.Text.Contains("ragt über den Rand", StringComparison.Ordinal));
        AussenBearbeitung.Platzieren(a, s, 16, 12);
        AussenBearbeitung.KatalogGroesseSetzen(a, s, 0, 1);
        Assert.Contains(KatalogPruefung.Aussen(K, a), f => f.Rot && f.Text.Contains("keine Größe", StringComparison.Ordinal));
        AussenBearbeitung.KatalogGroesseSetzen(a, s, 1, 1);
        Assert.Empty(KatalogPruefung.Aussen(K, a));
        Py.Setzen(Objekt(Aussen.KatalogStuecke(a)[0]), "id", "car_kit/gibtesnicht");
        Assert.Contains(KatalogPruefung.Aussen(K, a), f => f.Rot && f.Text.Contains("gibt es im Katalog nicht", StringComparison.Ordinal));
    }

    [Fact]
    public void Aussen_ProjektPrueftKatalogMit_UndExportBestehtDieEinbaupruefung()
    {
        var p = Projekt.Neu("Katalog draussen", D, Daten.NeueAblage(), null, null);
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        p.AussenAendern("Setzen", a => AussenBearbeitung.Setzen(a, AussenArt.Katalog, 2, 7, modell: "car_kit/sedan"));
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot);
        var datei = p.Exportieren(DateTimeOffset.Parse("2026-11-06T10:00:00+01:00", System.Globalization.CultureInfo.InvariantCulture));
        var f = StadtFassung.Lesen(datei);
        Assert.Empty(StadtbauPruefung.Format(Py.Lesen(datei)));
        Assert.Empty(StadtbauPruefung.Inhalt(f, D.Inhalt));
        Assert.Single(Aussen.KatalogStuecke(Aussen.Aus(f)));
        // Gegenprobe: ein unbekanntes Stueck sperrt den Export
        p.AussenAendern("Setzen", a => AussenBearbeitung.Setzen(a, AussenArt.Katalog, 2, 13, modell: "car_kit/sedan"));
        p.AussenAendern("Name", a => Py.Setzen(Objekt(Aussen.KatalogStuecke(a)[1]), "id", "car_kit/gibtesnicht"));
        Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
    }

    // ---- Beschriftung in der Draufsicht (Fund Chat: TALHEIM) ----------------------------------------

    [Fact]
    public void Beschriftung_RueckenInDieKarte_Gegenprobe()
    {
        // TALHEIM am Tablet: Punkt 30/23,3 von 32 x 24 bei 19,7 Bildpunkten je Einheit, Text 55 x 16
        const double z = 19.7;
        var (x, y, b) = Beschriftung.InKarte(30 * z, 23.3 * z, 55, 16, 0, 0, 32 * z, 24 * z);
        Assert.True(Beschriftung.Innen(x, y, b, 16, 0, 0, 32 * z, 24 * z));
        Assert.Equal(55, b);
        // zu breit fuer die Karte: gekuerzt auf die Breite der Karte
        var (x2, _, b2) = Beschriftung.InKarte(10, 10, 500, 16, 0, 0, 200, 100);
        Assert.Equal((0.0, 200.0), (x2, b2));
        Beschriftung.OhneRandFuerTests = true;
        try
        {
            var (ox, oy, ob) = Beschriftung.InKarte(30 * z, 23.3 * z, 55, 16, 0, 0, 32 * z, 24 * z);
            Assert.False(Beschriftung.Innen(ox, oy, ob, 16, 0, 0, 32 * z, 24 * z));
        }
        finally
        {
            Beschriftung.OhneRandFuerTests = false;
        }
    }
    /// <summary>
    /// Weisung P12-25 Frage 1 = a: der Katalog-Ordner gehoert nur zum Werkzeug. Kein Projekt der Lernplattform bindet ihn
    /// ein oder liest ihn (csproj, Code, gemeinsame Bau-Dateien), und keine Lernplattform-DLL traegt eine Datei daraus.
    /// Gegenprobe: Stadtbau.Core bindet ihn ein und traegt die Dateien.
    /// </summary>
    [Fact]
    public void KatalogOrdner_NurImWerkzeug_NieInDerLernplattform()
    {
        const string ordner = "inhalte_stadtbau";
        var wurzel = Daten.Wurzel;
        var endungen = new[] { ".csproj", ".cs", ".props", ".targets", ".axaml", ".xml", ".json" };
        bool Nennt(string datei) => File.ReadAllText(datei).Contains(ordner, StringComparison.OrdinalIgnoreCase);
        IEnumerable<string> Dateien(string projekt) => Directory.EnumerateFiles(projekt, "*", SearchOption.AllDirectories)
            .Where(d => endungen.Contains(Path.GetExtension(d), StringComparer.OrdinalIgnoreCase))
            .Where(d => !d.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(t => t is "bin" or "obj"));
        var projekte = Directory.GetDirectories(Path.Combine(wurzel, "src"), "Lernplattform.*");
        Assert.Equal(4, projekte.Length);
        var funde = projekte.SelectMany(Dateien).Where(Nennt).Select(d => Path.GetRelativePath(wurzel, d)).ToList();
        foreach (var gemeinsam in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props" })
        {
            var d = Path.Combine(wurzel, gemeinsam);
            if (File.Exists(d) && Nennt(d))
            {
                funde.Add(gemeinsam);
            }
        }
        Assert.Empty(funde);
        // Gegenprobe: das Werkzeug bindet den Ordner ein (sonst prueft die Suche oben nichts)
        Assert.Contains(Dateien(Path.Combine(wurzel, "src", "Stadtbau.Core")), Nennt);

        // gebaute DLLs (die Tests binden Lernplattform.Core ein; App, Desktop und Android deckt die Suche in den Quellen
        // oben ab): Lernplattform ohne Datei aus dem Ordner, Stadtbau.Core mit allen
        var lp = Directory.GetFiles(AppContext.BaseDirectory, "Lernplattform.*.dll");
        Assert.Contains(lp, d => Path.GetFileName(d) == "Lernplattform.Core.dll");
        foreach (var dll in lp)
        {
            var namen = System.Reflection.Assembly.LoadFrom(dll).GetManifestResourceNames();
            Assert.DoesNotContain(namen, n => n.Contains(ordner, StringComparison.OrdinalIgnoreCase));
        }
        var werkzeug = typeof(Katalog).Assembly.GetManifestResourceNames();
        Assert.Contains(Katalog.Ordner + "katalog.json", werkzeug);
        Assert.Equal(K.PaketTitel.Count, werkzeug.Count(n => n.StartsWith(Katalog.Ordner, StringComparison.Ordinal) && n.EndsWith(".png", StringComparison.Ordinal)));
    }
}
