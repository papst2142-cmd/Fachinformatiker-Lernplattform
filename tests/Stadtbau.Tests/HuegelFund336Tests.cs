using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.52.0 (Weisung P12-242 Punkt 3, P12-243 Punkt 3, Sammelliste 336): Nico konnte einmal keinen Huegel erstellen.
/// Nachgestellt in der leeren Stadt und in Stadt 1 (Fassung 1) ueber denselben Weg wie die Oberflaeche (ein Schritt
/// „Setzen“ mit <see cref="AussenBearbeitung.SetzenUndDrehen"/>): freie Stelle, Rand, nahe Strasse, Haus, Gleis, auf
/// gemaltem Gelaende, nach Rueckgaengig, bei 16 Huegeln. Je Fall: steht ein Huegel da, sieht man ihn (die Hoehe in der Mitte
/// steigt), und wenn nicht, sagt das Werkzeug vorher warum.
/// </summary>
public class HuegelFund336Tests
{
    private static Spieldaten D => Daten.Spiel;

    private static List<object?> P(double x, double y) => [x, y];

    internal record Fall(string Name, bool Gesetzt, float HoeheVorher, float HoeheNachher, IReadOnlyList<string> NeuRot);

    private static float Hoehe(Projekt p, double x, double y) =>
        Gelaende.Aus(Aussen.Stadtdaten(p.AussenStand)) is { } g ? g.Hoehe((float)x, (float)y) : 0f;

    /// <summary>Huegel setzen wie die Oberflaeche (Knopf „Hügel“, dann auf die Karte tippen).</summary>
    internal static Fall Setzen(Projekt p, string name, double x, double y)
    {
        var vorher = p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text).ToHashSet(StringComparer.Ordinal);
        var h0 = Hoehe(p, x, y);
        var zahl = AussenBearbeitung.HuegelListe(p.AussenStand).Count;
        var s = p.AussenAendern("Setzen", a => AussenBearbeitung.SetzenUndDrehen(a, AussenArt.Huegel, x, y, true, null, 0));
        var gesetzt = s is not null && AussenBearbeitung.HuegelListe(p.AussenStand).Count == zahl + 1;
        var neu = p.PruefenAussen().Where(f => f.Rot && !vorher.Contains(f.Text)).Select(f => f.Text).ToList();
        return new Fall(name, gesetzt, h0, Hoehe(p, x, y), neu);
    }

    internal static Projekt LeereStadt(string name)
    {
        var p = Projekt.Neu(name, D, Daten.NeueAblage(), leer: (40, 30));
        p.AussenAendern("Grundlage", a =>
        {
            var land = Aussen.Landschaft(a);
            land.Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(2, 3), P(38, 3) } }, { "gehweg", 0.35 } });
            land.Add(new PyObjekt { { "typ", "bahn" }, { "punkte", new List<object?> { P(0, 27), P(40, 27) } }, { "radien", new List<object?> { 3.0, 3.0 } } });
            return AussenBearbeitung.Setzen(a, AussenArt.Gebaeude, 30, 5, modell: "filiale");
        });
        return p;
    }

    internal static Projekt Stadt1(string name) =>
        Projekt.AusStadtDatei(File.ReadAllBytes(Path.Combine(Daten.Wurzel, "inhalte_csharp", "spiel", "stadtbau", "stadt1_fassung1.fistadt")), D, Daten.NeueAblage(), name);

    [Fact]
    public void Stadt1_HatKeineHuegel()
    {
        var p = Stadt1("Stadt1 Huegel");
        Assert.Empty(AussenBearbeitung.HuegelListe(p.AussenStand));
        Assert.Null(Gelaende.Aus(Aussen.Stadtdaten(p.AussenStand)));
        Assert.False(AussenBearbeitung.HuegelVoll(p.AussenStand));
    }

    private const string Saum = "näher als 1,5";

    private static void Sichtbar(Fall f)
    {
        Assert.True(f.Gesetzt, f.Name);
        Assert.True(f.HoeheNachher - f.HoeheVorher >= 0.7f, $"{f.Name}: Hoehe {f.HoeheVorher} -> {f.HoeheNachher}");
    }

    /// <summary>
    /// Alle Faelle aus P12-242 Punkt 3 in der leeren Stadt und in Stadt 1. Ergebnis: an jeder Stelle steht ein Huegel; am Rand
    /// und nah an Strasse, Haus oder Gleis mit rotem Fund samt Grund; bei 16 Huegeln keiner (der Knopf ist vorher grau, Start-Test
    /// „Huegel Hoechstzahl“); auf hoch gemaltem Gelaende steht er, ist aber verdeckt und das Werkzeug sagt es.
    /// </summary>
    [Theory]
    [InlineData("leer")]
    [InlineData("Stadt 1")]
    public void Nachstellen_JederFall_HuegelOderGrund(string stadt)
    {
        var leer = stadt == "leer";
        var p = leer ? LeereStadt("Fall leer") : Stadt1("Fall stadt1");
        var a = p.AussenStand;
        var (fx, fy) = leer ? (20.0, 15.0) : (55.0, 55.0);
        var hf = leer
            ? AussenBearbeitung.HausFlaeche(a, new AussenStelle(AussenArt.Gebaeude, Aussen.Landschaft(a).Count - 1))
            : AussenBearbeitung.HausFlaeche(a, new AussenStelle(AussenArt.Haus, -1, 0, Py.Str(AussenBearbeitung.Orte(a).First()["id"])));
        var zeilen = new List<string>();
        Fall F(string name, double x, double y)
        {
            var f = Setzen(p, name, x, y);
            zeilen.Add($"{stadt} | {f.Name} | gesetzt {f.Gesetzt} | Hoehe {f.HoeheVorher:F2} -> {f.HoeheNachher:F2} | neu rot: {string.Join(" / ", f.NeuRot)}");
            return f;
        }

        var frei = F("frei", fx, fy);
        Sichtbar(frei);
        Assert.Empty(frei.NeuRot);
        Assert.False(AussenBearbeitung.HuegelVerdeckt(p.AussenStand, new AussenStelle(AussenArt.Huegel, 0)));
        Assert.True(p.Rueckgaengig());

        var rand = F("Rand", 0.5, leer ? 15.0 : 30.0);
        Sichtbar(rand);
        Assert.Contains(rand.NeuRot, t => t.Contains("über den Rand der Karte", StringComparison.Ordinal));
        Assert.True(p.Rueckgaengig());

        foreach (var (name, x, y, art) in new[]
        {
            ("nahe Strasse", leer ? 20.0 : 15.5, leer ? 4.5 : 30.0, "Straße"),
            ("nahe Haus", hf.X + hf.W + 1, hf.Y + (hf.H / 2), leer ? "Haus" : "Fußweg"),
            ("nahe Gleis", leer ? 20.0 : 34.5, leer ? 25.5 : 10.0, "Gleis"),
        })
        {
            var f = F(name, x, y);
            Sichtbar(f);
            Assert.Contains(f.NeuRot, t => t.Contains(Saum, StringComparison.Ordinal) && t.Contains(art, StringComparison.Ordinal));
            Assert.True(p.Rueckgaengig());
        }

        // auf gemaltem Gelaende, das schon 1,5 hoch ist: der Huegel steht, ist aber verdeckt, und das Werkzeug sagt es
        for (var i = 0; i < 15; i++)
        {
            p.AussenAendern("Malen", aa => AussenBearbeitung.Pinselzug(aa, PinselArt.Heben, [new Vector2((float)fx, (float)fy)], 2f));
        }
        var gemalt = F("auf gemaltem Gelaende", fx, fy);
        Assert.True(gemalt.Gesetzt);
        Assert.Equal(gemalt.HoeheVorher, gemalt.HoeheNachher);
        Assert.True(AussenBearbeitung.HuegelVerdeckt(p.AussenStand, new AussenStelle(AussenArt.Huegel, 0)));
        Assert.True(p.Rueckgaengig());

        // nach Rueckgaengig: daneben wieder ein ganz normaler Huegel
        var danach = F("nach Rueckgaengig", fx - 5, fy);
        Sichtbar(danach);
        Assert.Empty(danach.NeuRot);

        // bei 16 Huegeln: kein weiterer (der Knopf ist vorher grau und nennt den Grund)
        for (var i = AussenBearbeitung.HuegelListe(p.AussenStand).Count; i < Gelaende.HuegelHoechstens; i++)
        {
            Assert.True(Setzen(p, "viele", 3 + (i % 6 * 5), 9 + (i / 6 * 4)).Gesetzt);
        }
        Assert.True(AussenBearbeitung.HuegelVoll(p.AussenStand));
        var voll = F("bei 16 Huegeln", fx + 4, fy);
        Assert.False(voll.Gesetzt);
        Assert.Equal(Gelaende.HuegelHoechstens, AussenBearbeitung.HuegelListe(p.AussenStand).Count);
        TestContext.Current.SendDiagnosticMessage(string.Join("\n", zeilen));
    }

    /// <summary>
    /// Verdeckt nur, wenn der Huegel nirgends ueber das uebrige Gelaende ragt: auf niedrig Gemaltem (0,3) und neben einem
    /// anderen Huegel sieht man ihn, in einem groesseren Huegel nicht. Gegenprobe: wie bis S 0.51.0 meldet nichts (stumm).
    /// </summary>
    [Fact]
    public void Verdeckt_NurWennGanzUnterDemGelaende_GegenprobeStumm()
    {
        var p = LeereStadt("Verdeckt");
        for (var i = 0; i < 3; i++)
        {
            p.AussenAendern("Malen", aa => AussenBearbeitung.Pinselzug(aa, PinselArt.Heben, [new Vector2(10, 15)], 2f));
        }
        var niedrig = p.AussenAendern("Setzen", a => AussenBearbeitung.SetzenUndDrehen(a, AussenArt.Huegel, 10, 15, true, null, 0))!;
        Assert.False(AussenBearbeitung.HuegelVerdeckt(p.AussenStand, niedrig));

        // ein grosser hoher Huegel und darin ein kleiner: der kleine ist verdeckt, der grosse nicht
        var gross = p.AussenAendern("Setzen", a => AussenBearbeitung.SetzenUndDrehen(a, AussenArt.Huegel, 25, 15, true, null, 0))!;
        p.AussenAendern("Werte", a => { AussenBearbeitung.HuegelWerteSetzen(a, gross, 3, 1.5, false, 0); return true; });
        var klein = p.AussenAendern("Setzen", a => AussenBearbeitung.SetzenUndDrehen(a, AussenArt.Huegel, 25, 15, true, null, 0))!;
        p.AussenAendern("Werte", a => { AussenBearbeitung.HuegelWerteSetzen(a, klein, 1, 0.5, false, 0); return true; });
        Assert.True(AussenBearbeitung.HuegelVerdeckt(p.AussenStand, klein));
        Assert.False(AussenBearbeitung.HuegelVerdeckt(p.AussenStand, gross));
        // der kleine daneben: sichtbar
        p.AussenAendern("Schieben", a => { AussenBearbeitung.Verschieben(a, klein, 0, -4.5); return true; });
        Assert.False(AussenBearbeitung.HuegelVerdeckt(p.AussenStand, klein));
        p.Rueckgaengig();
        Assert.True(AussenBearbeitung.HuegelVerdeckt(p.AussenStand, klein));

        // Gegenprobe: ohne die Pruefung (wie bis S 0.51.0) waere der verdeckte Huegel stumm
        AussenBearbeitung.HuegelVerdecktAltFuerTests = true;
        try
        {
            Assert.False(AussenBearbeitung.HuegelVerdeckt(p.AussenStand, klein));
        }
        finally
        {
            AussenBearbeitung.HuegelVerdecktAltFuerTests = false;
        }
    }
}
