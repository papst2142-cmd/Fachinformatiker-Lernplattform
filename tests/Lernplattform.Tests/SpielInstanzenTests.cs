using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle D2 (Weisung P12-157, Nico 1a): Instancing. Die Netze der Stadt ohne die Saetze plus die Saetze an ihren Lagen
/// ergeben genau das volle Netz wie bis A 0.58.0 (gleiche Dreiecke an gleicher Stelle, gleiche Farben bis auf
/// 1/<see cref="Instanzen.FarbStufen"/>); hoechstens 8 Saetze, jeder mit mindestens 8 Lagen.
/// </summary>
public sealed class SpielInstanzenTests
{
    internal static StadtSzene Szene(Jahreszeit j, string? fest)
    {
        var stadt = new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
        var ablauf = new Spielablauf(stadt.Inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" } }, Ereignisse.Normal);
        return StadtSzene.Bauen(stadt, ablauf.Stand, SpielGrafikTests.Bibliothek.Value, fest, FestDekoTests.Daten.Value, j);
    }

    public static TheoryData<Jahreszeit, string?> Zeiten => new()
    {
        { Jahreszeit.Sommer, null },
        { Jahreszeit.Herbst, null },
        { Jahreszeit.Fruehling, null },
        { Jahreszeit.Winter, Festzeit.Weihnachten },
    };

    /// <summary>Jedes Dreieck: drei Orte genau als Text (zum Sortieren), dazu die neun Farbwerte.</summary>
    private static List<(string Ort, float[] Farbe)> Dreiecke(Netz n, Matrix4x4? lage = null)
    {
        var liste = new List<(string, float[])>(n.Indizes.Count / 3);
        var e = n.EckenFeld;
        var idx = n.IndizesFeld;
        for (var i = 0; i < idx.Length; i += 3)
        {
            var teile = new string[3];
            var farbe = new float[9];
            for (var k = 0; k < 3; k++)
            {
                var o = (int)idx[i + k] * Netz.Zahlen;
                var p = new Vector3(e[o], e[o + 1], e[o + 2]);
                if (lage is { } l)
                {
                    p = Vector3.Transform(p, l);
                }
                teile[k] = $"{BitConverter.SingleToInt32Bits(p.X)},{BitConverter.SingleToInt32Bits(p.Y)},{BitConverter.SingleToInt32Bits(p.Z)}";
                farbe[(k * 3) + 0] = e[o + 6];
                farbe[(k * 3) + 1] = e[o + 7];
                farbe[(k * 3) + 2] = e[o + 8];
            }
            liste.Add((string.Join("|", teile), farbe));
        }
        return liste;
    }

    private static int Vergleich((string Ort, float[] Farbe) a, (string Ort, float[] Farbe) b)
    {
        var c = string.CompareOrdinal(a.Ort, b.Ort);
        for (var i = 0; c == 0 && i < 9; i++)
        {
            c = a.Farbe[i].CompareTo(b.Farbe[i]);
        }
        return c;
    }

    private static void GleicheDreiecke(Netz voll, Netz ohne, IEnumerable<InstanzSatz> saetze, bool fern, string wer)
    {
        var soll = Dreiecke(voll);
        var ist = Dreiecke(ohne);
        foreach (var s in saetze)
        {
            foreach (var l in s.Lagen)
            {
                ist.AddRange(Dreiecke(fern ? s.Fern! : s.Nah, l));
            }
        }
        Assert.Equal(soll.Count, ist.Count);
        soll.Sort(Vergleich);
        ist.Sort(Vergleich);
        // Orte genau gleich, Farben hoechstens 1/FarbStufen verschieden (Stuecke eines Satzes teilen die Farben des ersten)
        var abweichend = soll.Zip(ist).Count(p => p.First.Ort != p.Second.Ort
            || p.First.Farbe.Zip(p.Second.Farbe).Any(f => MathF.Abs(f.First - f.Second) > 1f / Instanzen.FarbStufen));
        Assert.True(abweichend == 0, $"{wer}: {abweichend} Dreiecke verschieden");
    }

    [Theory]
    [MemberData(nameof(Zeiten))]
    public void OhnePlusSaetze_GleichDemVollenNetz(Jahreszeit j, string? fest)
    {
        var szene = Szene(j, fest);
        var f = szene.Vorbereiten();
        var plan = f.Instanzen!;
        Assert.NotEmpty(plan.Saetze);
        Assert.True(plan.Saetze.Count <= Instanzen.HoechstensSaetze);
        Assert.All(plan.Saetze, s => Assert.True(s.Lagen.Length >= Instanzen.MindestLagen, s.Modell));
        var stadt = plan.Saetze.Where(s => s.Ziel == Instanzen.Stadt).ToList();
        var gruen = plan.Saetze.Where(s => s.Ziel == Instanzen.Gruen).ToList();
        GleicheDreiecke(szene.NetzBauen(), f.Stadt, stadt, false, "Stadt nah");
        GleicheDreiecke(szene.GruenNetzBauen()!, f.Gruen!, gruen, false, "Kleingruen");
        // fern: die Stuecke der Saetze in ihrer Fernform (Nico 1a), alles andere wie das volle Fern-Netz
        var fernVoll = szene.NetzBauen(true, Instanzplan.Leer);
        Assert.True(Dreiecke(f.StadtFern!).Count + stadt.Sum(s => s.Lagen.Length * s.DreieckeFern) <= Dreiecke(fernVoll).Count);
        Assert.All(gruen, s => Assert.Null(s.Fern));
    }

    [Fact]
    public void Gegenprobe_AusFuerTests_AllesImNetz()
    {
        Instanzen.AusFuerTests = true;
        try
        {
            var szene = Szene(Jahreszeit.Winter, Festzeit.Weihnachten);
            var f = szene.Vorbereiten();
            Assert.Null(f.Instanzen);
            Assert.True(szene.NetzBauen().EckenFeld.SequenceEqual(f.Stadt.EckenFeld));
            Assert.True(szene.GruenNetzBauen()!.EckenFeld.SequenceEqual(f.Gruen!.EckenFeld));
        }
        finally
        {
            Instanzen.AusFuerTests = false;
        }
    }

    [Fact]
    public void Gegenprobe_FalscheLage_ZeigtAbweichung()
    {
        var szene = Szene(Jahreszeit.Sommer, null);
        var f = szene.Vorbereiten();
        var s = f.Instanzen!.Saetze[0];
        var falsch = new InstanzSatz(s.Ziel, s.Modell, s.Nah, s.Fern, [.. s.Lagen.Select(l => l * Matrix4x4.CreateTranslation(0.001f, 0, 0))], s.Kugeln);
        var voll = s.Ziel == Instanzen.Stadt ? szene.NetzBauen() : szene.GruenNetzBauen()!;
        var ohne = s.Ziel == Instanzen.Stadt ? f.Stadt : f.Gruen!;
        var andere = f.Instanzen.Saetze.Where(x => x.Ziel == s.Ziel && x != s).Append(falsch);
        Assert.ThrowsAny<Exception>(() => GleicheDreiecke(voll, ohne, andere, false, "falsch"));
    }

    [Fact]
    public void Kugeln_EnthaltenJedeEcke_NahUndFern()
    {
        var f = Szene(Jahreszeit.Winter, Festzeit.Weihnachten).Vorbereiten();
        foreach (var s in f.Instanzen!.Saetze)
        {
            for (var i = 0; i < s.Lagen.Length; i++)
            {
                foreach (var netz in new[] { s.Nah, s.Fern ?? s.Nah })
                {
                    for (var e = 0; e < netz.AnzahlEcken; e++)
                    {
                        var p = Vector3.Transform(netz.Ort(e), s.Lagen[i]);
                        Assert.True(Vector3.Distance(p, s.Kugeln[i].Mitte) <= s.Kugeln[i].Halb, s.Modell);
                    }
                }
            }
        }
    }

    [Fact]
    public void ImBild_NurStueckeImSichtkegel()
    {
        var f = Szene(Jahreszeit.Sommer, null).Vorbereiten();
        var s = f.Instanzen!.Saetze[0];
        var k = new Kamera(s.Kugeln[0].Mitte.X, s.Kugeln[0].Mitte.Z, 4);
        var sichtProj = k.Sicht * k.Projektion(1440, 810);
        var ziel = new Matrix4x4[s.Lagen.Length];
        var n = s.ImBild(sichtProj, ziel);
        Assert.InRange(n, 1, s.Lagen.Length);
        Assert.Equal(s.Lagen.Length, s.ImBild(sichtProj, null, alle: true));
        // weit weg geschaut: keins
        var weg = new Kamera(-500, -500, 4);
        Assert.Equal(0, s.ImBild(weg.Sicht * weg.Projektion(1440, 810), null));
    }

    [Fact]
    public void Geeignet_NurDrehungUndGleicheGroesse()
    {
        Assert.True(Instanzen.Geeignet(Matrix4x4.CreateScale(0.4f) * Matrix4x4.CreateRotationY(1.1f) * Matrix4x4.CreateTranslation(3, 0, 4)));
        Assert.False(Instanzen.Geeignet(Matrix4x4.CreateScale(0.4f, 0.5f, 0.4f)));
        Assert.False(Instanzen.Geeignet(Matrix4x4.CreateScale(-1f, 1f, 1f)));
        Assert.False(Instanzen.Geeignet(Matrix4x4.CreateScale(0f)));
    }

    /// <summary>
    /// Weisung P12-157 Frage 4 = a: groebere Baum-Fernform nur messen (das Spiel bleibt bei 1/32). Summe der Dreiecke aller
    /// Baeume (Kleingruen und Baum-Modelle der Stadt) fern bei 1/32, 1/16 und 1/8; Zahlen in der Testausgabe.
    /// </summary>
    [Fact]
    public void Messung_GroebereBaumFernform()
    {
        var szene = Szene(Jahreszeit.Winter, Festzeit.Weihnachten);
        var grafik = SpielGrafikTests.Bibliothek.Value;
        var modelle = new List<GrafikModell>();
        foreach (var b in szene.GruenKlein?.Baeume ?? [])
        {
            if (grafik.Modell(b.Name) is { } m)
            {
                modelle.Add(m);
            }
        }
        foreach (var st in szene.Stuecke)
        {
            if (st.Name.Contains("tree", StringComparison.OrdinalIgnoreCase) && grafik.Modell(st.Name) is { } m)
            {
                modelle.Add(m);
            }
        }
        long Summe(int teile) => modelle.Sum(m => (long)Fernform.Bauen(m, teile).Dreiecke);
        var (nah, f32, f16, f8) = (modelle.Sum(m => (long)m.Dreiecke), Summe(32), Summe(16), Summe(8));
        var zeile = $"Baeume {modelle.Count}: nah {nah}, fern 1/32 {f32}, 1/16 {f16}, 1/8 {f8}";
        TestContext.Current.TestOutputHelper?.WriteLine(zeile);
        if (Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSDATEI_BAEUME") is { Length: > 0 } datei)
        {
            File.WriteAllText(datei, zeile);
        }
        Assert.True(modelle.Count > 0);
        Assert.Equal(Summe(Fernform.Teile), modelle.Sum(m => (long)Fernform.Von(m).Dreiecke));
        Assert.True(f16 <= f32 && f8 <= f16 && f32 <= nah);
    }
}
