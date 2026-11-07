using System.Numerics;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.43.6 (Weisung P12-50, Nico c): Farbe je Ecke statt je Dreieck. Die Pakete von Kenney und KayKit malen Waende mit
/// einem Farbverlauf im Farbatlas; mit einer Farbe je Dreieck (Pixel an der UV-Mitte) zerfiel eine ebene Wand in zwei
/// Dreiecke mit verschiedenen Farben (Flecken in X-Form). Jetzt traegt modelle.bin (Version 2) die Farbe jeder Ecke, das
/// Netz gibt sie an die Grafikkarte, die dazwischen verlaeuft. Modelle ohne Verlauf bleiben bitgleich; Glas, Licht,
/// Schnee und Fest-Deko werden weiter an der Farbe des Dreiecks erkannt und sehen aus wie vorher.
/// </summary>
public sealed class SpielFarbverlaufTests
{
    private static GrafikBibliothek Grafik => SpielGrafikTests.Bibliothek.Value;

    private static string Ordner => Path.Combine(Pfade.Wurzel, "inhalte_csharp", "grafik");

    [Fact]
    public void Dateien_SindVersion2_MitEckfarbenJeModell()
    {
        foreach (var datei in new[] { "modelle.bin", "katalog_verbaut.bin" })
        {
            var daten = File.ReadAllBytes(Path.Combine(Ordner, datei));
            Assert.Equal("FLGM", Encoding.ASCII.GetString(daten, 0, 4));
            Assert.Equal(GrafikBibliothek.Version, BitConverter.ToUInt32(daten, 4));
        }
        Assert.All(Grafik.Modelle, m => Assert.Equal(m.Farben.Length * 3, m.EckFarben!.Length));
    }

    /// <summary>
    /// Gegenprobe der Weisung: eine ebene Kenney-Wand (Kaufhaus aus Modular Buildings). Je zwei Dreiecke mit gemeinsamer
    /// Kante in derselben Ebene, aber mit verschiedener Farbe des Dreiecks (dort sah man das X), haben an der gemeinsamen
    /// Kante im gezeichneten Netz dieselben Eckfarben. Wird wieder je Dreieck gefaerbt, ist der Test rot (siehe unten).
    /// </summary>
    [Fact]
    public void EbeneKenneyWand_NachbarnHabenAnDerKanteGleicheEckfarben()
    {
        var m = Grafik.Modell("kaufhaus-galerie")!;
        var (gleich, ungleich) = KantenPaare(m);
        Assert.True(gleich >= 2000, "zu wenige Paare: " + gleich);
        Assert.Equal(0, ungleich);
        // Gegenprobe: dasselbe Modell je Dreieck gefaerbt (so bis A 0.43.5) faellt durch
        var alt = KantenPaare(new GrafikModell(m.Name, m.Ecken, m.Farben));
        Assert.Equal(0, alt.Gleich);
        Assert.Equal(gleich, alt.Ungleich);
    }

    /// <summary>Paare (gleiche Ebene, gemeinsame Kante, verschiedene Farbe des Dreiecks) im gezeichneten Netz.</summary>
    private static (int Gleich, int Ungleich) KantenPaare(GrafikModell m)
    {
        var netz = new Netz();
        var flaechen = new List<Vector3>();
        netz.Hinzu(m, Matrix4x4.Identity, null, flaechen);
        var idx = netz.Indizes;
        (int, int, int) Punkt(Vector3 p) => ((int)MathF.Round(p.X * 10000), (int)MathF.Round(p.Y * 10000), (int)MathF.Round(p.Z * 10000));
        var kanten = new Dictionary<((int, int, int), (int, int, int)), List<int>>();
        for (var t = 0; t < idx.Count / 3; t++)
        {
            for (var k = 0; k < 3; k++)
            {
                var a = Punkt(netz.Ort((int)idx[(t * 3) + k]));
                var b = Punkt(netz.Ort((int)idx[(t * 3) + ((k + 1) % 3)]));
                var schluessel = a.CompareTo(b) < 0 ? (a, b) : (b, a);
                if (!kanten.TryGetValue(schluessel, out var liste))
                {
                    kanten[schluessel] = liste = [];
                }
                liste.Add(t);
            }
        }
        Vector3? FarbeAm(int t, (int, int, int) p)
        {
            for (var k = 0; k < 3; k++)
            {
                if (Punkt(netz.Ort((int)idx[(t * 3) + k])) == p)
                {
                    return netz.FarbeAn((int)idx[(t * 3) + k]);
                }
            }
            return null;
        }
        (int, int, int) Richtung(int t)
        {
            var n = netz.Normale((int)idx[t * 3]);
            return ((int)MathF.Round(n.X * 1000), (int)MathF.Round(n.Y * 1000), (int)MathF.Round(n.Z * 1000));
        }
        int gleich = 0, ungleich = 0;
        foreach (var ((a, b), liste) in kanten)
        {
            for (var i = 0; i < liste.Count; i++)
            {
                for (var j = i + 1; j < liste.Count; j++)
                {
                    int s = liste[i], u = liste[j];
                    if (Richtung(s) != Richtung(u) || flaechen[s] == flaechen[u])
                    {
                        continue;
                    }
                    if (FarbeAm(s, a) == FarbeAm(u, a) && FarbeAm(s, b) == FarbeAm(u, b))
                    {
                        gleich++;
                    }
                    else
                    {
                        ungleich++;
                    }
                }
            }
        }
        return (gleich, ungleich);
    }

    /// <summary>
    /// Modelle ohne Verlauf (City Kit Suburban, house_type*) sind Pixel fuer Pixel wie vorher: jede Eckfarbe gleich der
    /// Farbe des Dreiecks, das gezeichnete Netz bitgleich mit dem Netz je Dreieck. Gegenprobe: das Kaufhaus ist es nicht.
    /// </summary>
    [Fact]
    public void Suburban_OhneVerlauf_NetzBitgleichWieJeDreieck()
    {
        var haeuser = Grafik.Modelle.Where(m => m.Name.StartsWith("house_type", StringComparison.Ordinal)).ToList();
        Assert.Equal(6, haeuser.Count);
        foreach (var m in haeuser)
        {
            for (var d = 0; d < m.Dreiecke; d++)
            {
                for (var k = 0; k < 3; k++)
                {
                    Assert.Equal(m.Farbe(d), m.EckFarbe(d, k));
                }
            }
            Assert.True(GleichesNetz(m), m.Name);
        }
        Assert.False(GleichesNetz(Grafik.Modell("kaufhaus-galerie")!));
    }

    private static bool GleichesNetz(GrafikModell m)
    {
        var lage = Matrix4x4.CreateRotationY(0.7f) * Matrix4x4.CreateTranslation(3, 0, 5);
        var neu = new Netz();
        neu.Hinzu(m, lage);
        var alt = new Netz();
        alt.Hinzu(new GrafikModell(m.Name, m.Ecken, m.Farben), lage);
        return neu.EckenFeld.SequenceEqual(alt.EckenFeld) && neu.IndizesFeld.SequenceEqual(alt.IndizesFeld);
    }

    /// <summary>
    /// Nachtlicht, Leuchtmarke (+4), Schnee (+2) und Fest-Deko wie vorher: die ganze Stadt (spaeter Stand, alle Orte) in
    /// jeder Jahreszeit und zu jedem Fest, gebaut mit Farbverlauf und je Dreieck. Gleiche Ecken, Normalen und Indizes in
    /// jedem Netz; jede Ecke hat dieselbe Marke; Schnee und jede Ecke eines Modells ohne Verlauf haben dieselbe Farbe.
    /// </summary>
    [Theory]
    [InlineData(null, Jahreszeit.Sommer)]
    [InlineData(null, Jahreszeit.Fruehling)]
    [InlineData(null, Jahreszeit.Herbst)]
    [InlineData(null, Jahreszeit.Winter)]
    [InlineData(Festzeit.Ostern, Jahreszeit.Fruehling)]
    [InlineData(Festzeit.Halloween, Jahreszeit.Herbst)]
    [InlineData(Festzeit.Weihnachten, Jahreszeit.Winter)]
    [InlineData(Festzeit.Silvester, Jahreszeit.Winter)]
    public void Stadt_MarkenUndSchneeWieJeDreieck(string? fest, Jahreszeit j)
    {
        var stadt = new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
        var stand = SpaeterStand();
        var neu = StadtSzene.Bauen(stadt, stand, Grafik, fest, jahreszeit: j).Vorbereiten();
        var alt = StadtSzene.Bauen(stadt, stand, Grafik.JeDreieck(), fest, jahreszeit: j).Vorbereiten();
        var leuchtend = 0;
        var verlauf = 0;
        void Vergleich(string name, Netz? a, Netz? b)
        {
            Assert.Equal(a is null, b is null);
            if (a is null || b is null)
            {
                return;
            }
            Assert.True(a.IndizesFeld.SequenceEqual(b.IndizesFeld), name);
            Assert.Equal(a.AnzahlEcken, b.AnzahlEcken);
            for (var e = 0; e < a.AnzahlEcken; e++)
            {
                Assert.Equal(a.Ort(e), b.Ort(e));
                Assert.Equal(a.Normale(e), b.Normale(e));
                var fa = a.FarbeAn(e);
                var fb = b.FarbeAn(e);
                Assert.True(Marke(fa) == Marke(fb), name + " Ecke " + e + ": " + fa + " / " + fb);
                if (Marke(fa) == 1)
                {
                    Assert.Equal(fb, fa);
                }
                leuchtend += Marke(fa) == 2 ? 1 : 0;
                verlauf += fa != fb ? 1 : 0;
            }
        }
        Vergleich("stadt", neu.Stadt, alt.Stadt);
        foreach (var (haus, netz) in neu.Haeuser)
        {
            Vergleich("haus " + haus.Id, netz, alt.Haeuser.Single(h => h.Key.Id == haus.Id).Value);
        }
        Vergleich("gruen", neu.Gruen, alt.Gruen);
        Vergleich("paket-nah", neu.PaketNah, alt.PaketNah);
        Vergleich("paket-fern", neu.PaketFern, alt.PaketFern);
        Assert.True(leuchtend > 0, "keine leuchtende Ecke");
        Assert.True(verlauf > 0, "kein Farbverlauf");
    }

    /// <summary>0 = ohne Marke, 1 = Schnee (+2), 2 = leuchtet nachts (+4).</summary>
    private static int Marke(Vector3 f) => Nachtlicht.Leuchtet(f) ? 2 : f.X >= Jahreszeitfarben.SchneeMarke - 0.5f ? 1 : 0;

    private static Spielstand SpaeterStand()
    {
        var ablauf = new Spielablauf(SpielInhaltTests.Inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        ablauf.Stand.TageFertig = 200;
        ablauf.Stand.Firma = new PyObjekt { { "stufe", 5L }, { "name", "Probe" }, { "tag", 1L } };
        ablauf.Stand.Zweigstelle = new PyObjekt { { "stufe", 1L } };
        return ablauf.Stand;
    }

    /// <summary>Ein neu gefaerbtes Dreieck (Tuer zugemalt) ist einfarbig, alle anderen behalten ihren Verlauf.</summary>
    [Fact]
    public void MitFarben_NurGeaenderteDreieckeEinfarbig()
    {
        var m = Grafik.Modell("kaufhaus-galerie")!;
        var farben = (byte[])m.Farben.Clone();
        farben[0] = (byte)(farben[0] ^ 0x40);
        var neu = m.MitFarben(farben);
        Assert.Equal(neu.Farbe(0), neu.EckFarbe(0, 0));
        Assert.Equal(neu.Farbe(0), neu.EckFarbe(0, 2));
        Assert.Equal(m.EckFarben!.AsSpan(9).ToArray(), neu.EckFarben!.AsSpan(9).ToArray());
        Assert.Contains(Enumerable.Range(1, m.Dreiecke - 1), d => m.EckFarbe(d, 0) != m.Farbe(d));
    }

    /// <summary>Formatkennung: Version 1 (ohne Eckfarben) bleibt lesbar, jede andere Version ist ein Fehler.</summary>
    [Fact]
    public void Leser_PrueftDieVersion()
    {
        static byte[] Datei(uint version, bool eck)
        {
            using var s = new MemoryStream();
            using var w = new BinaryWriter(s);
            w.Write("FLGM"u8);
            w.Write(version);
            w.Write(1u);
            w.Write((ushort)1);
            w.Write((byte)'a');
            w.Write(1u);
            for (var i = 0; i < 9; i++)
            {
                w.Write(i == 3 || i == 7 ? 1f : 0f);
            }
            w.Write(new byte[] { 10, 20, 30, 0 });
            if (eck)
            {
                w.Write(new byte[] { 1, 2, 3, 0, 4, 5, 6, 0, 7, 8, 9, 0 });
            }
            w.Flush();
            return s.ToArray();
        }
        var v1 = GrafikBibliothek.Lesen(Datei(1, false), "probe")["a"];
        Assert.Null(v1.EckFarben);
        Assert.Equal(v1.Farbe(0), v1.EckFarbe(0, 1));
        var v2 = GrafikBibliothek.Lesen(Datei(2, true), "probe")["a"];
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, v2.EckFarben);
        Assert.Throws<InvalidDataException>(() => GrafikBibliothek.Lesen(Datei(3, true), "probe"));
        Assert.Throws<InvalidDataException>(() => GrafikBibliothek.Lesen(Datei(1, true), "probe"));
        Assert.Throws<InvalidDataException>(() => GrafikBibliothek.Lesen(Datei(2, false), "probe"));
    }
}
