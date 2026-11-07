using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle K3 (A 0.45.0 / S 0.11.0, Weisungen P12-58/P12-59, Nico 1 = a, 2 = a): Kreisverkehr setzen, schieben, drehen,
/// kopieren und loeschen; im Spiel Ring ohne Stoss, Insel mit Rand, Rasen und Baum, Insel nicht begehbar, Mittellinie
/// rundum im Takt; die Pruefregeln 1 bis 8 (<see cref="StadtbauPruefung"/>, Teil Kreisverkehr) je rot und gruen.
/// Grundlage: leere Stadt 24 × 16, Kreisverkehr Radius 2 mit der Mitte (12; 8), wie ihn das Werkzeug setzt; ohne Fund.
/// </summary>
public class KreisverkehrTests
{
    private static Spieldaten D => Daten.Spiel;

    private static readonly Vector2 M = new(12, 8);

    private static List<object?> P(double x, double y) => [x, y];

    private static (PyObjekt A, AussenStelle Ring) Grundlage(double radius = 2, double breite = 24, double hoehe = 16)
    {
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_kreisverkehr", breite, hoehe));
        var s = AussenBearbeitung.Setzen(a, AussenArt.Kreisverkehr, M.X, M.Y, modell: AussenBearbeitung.Kennung(radius))!;
        return (a, s);
    }

    private static List<object?> Land(PyObjekt a) => Aussen.Landschaft(a);

    private static List<Vector2> Linie(PyObjekt a, int i) => Kreisverkehr.Linie(Objekt(Land(a)[i])["punkte"]);

    private static void Strasse(PyObjekt a, double x1, double y1, double x2, double y2) =>
        Land(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(x1, y1), P(x2, y2) } } });

    private static List<StadtbauFund> Pruefen(PyObjekt a) =>
        StadtbauPruefung.Aussen(Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", ""), D.Inhalt, D.Grafik);

    private static string Text(List<StadtbauFund> f) => string.Join(" | ", f.Select(x => (x.Rot ? "rot: " : "gelb: ") + x.Text));

    private static void Gruen(PyObjekt a)
    {
        var f = Pruefen(a);
        Assert.True(f.Count == 0, Text(f));
    }

    private static void Rot(PyObjekt a, string anfang)
    {
        var f = Pruefen(a);
        Assert.True(f.Any(x => x.Rot && x.Text.StartsWith(anfang, StringComparison.Ordinal)), "erwartet rot „" + anfang + "“, gefunden: " + Text(f));
    }

    private static StadtSzene Szene(PyObjekt a) =>
        Ansicht3D.Bauen(Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", ""), D, Ansicht3D.Einstellung.Standard).Szene;

    // ---- Werkzeug ------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2.0)]
    [InlineData(3.0)]
    public void Setzen_RingVierZufahrtenVierLaternen_OhneFund(double radius)
    {
        var (a, s) = Grundlage(radius);
        Assert.Equal(AussenArt.Strasse, s.Art);
        Assert.True(AussenBearbeitung.IstKreisverkehr(a, s));
        Assert.Equal(radius, AussenBearbeitung.KreisverkehrVon(a, s));
        var ring = Linie(a, s.Nummer);
        Assert.Equal(49, ring.Count);
        Assert.Equal(ring[0], ring[^1]);
        Assert.True(Kreisverkehr.Rund(ring, radius));
        Assert.True(Vector2.Distance(Kreisverkehr.Mitte(ring), M) < 0.01f);
        var strassen = Land(a).OfType<PyObjekt>().Select(l => Kreisverkehr.Linie(l["punkte"])).ToList();
        var zufahrten = Kreisverkehr.Zufahrten(ring, strassen, s.Nummer);
        Assert.Equal([0.0, 90, 180, 270], zufahrten.Select(z => Math.Round(z.Grad)).Order());
        Assert.All(zufahrten, z => Assert.True(Kreisverkehr.ZieltAufMitte(strassen[z.Strasse], z.AmAnfang, M)));
        Assert.Equal(4, Aussen.Laternen(a).Count);
        Assert.StartsWith(Texte.KreisverkehrName(radius), AussenBearbeitung.Beschreibung(a, s, x => x), StringComparison.Ordinal);
        Gruen(a);
        // Kennung nur mit erlaubtem Radius, zu kleine Karte: nichts gesetzt
        Assert.Null(AussenBearbeitung.KreisverkehrRadius("kreisverkehr:2.5"));
        Assert.Null(AussenBearbeitung.Setzen(Aussen.Aus(Aussen.Leer(D.Fassung, "neu_klein", 8, 5)), AussenArt.Kreisverkehr, 4, 2.5, modell: AussenBearbeitung.Kennung(3)));
    }

    [Fact]
    public void Schieben_Drehen_NehmenZufahrtenUndLaternenMit()
    {
        var (a, s) = Grundlage();
        AussenBearbeitung.Verschieben(a, s, 1, -1);
        Assert.True(Vector2.Distance(Kreisverkehr.Mitte(Linie(a, s.Nummer)), M + new Vector2(1, -1)) < 0.01f);
        Gruen(a);
        var laterne = Liste(Aussen.Laternen(a)[0]);
        var vorher = new Vector2((float)Py.F(laterne[0]), (float)Py.F(laterne[1]));
        Assert.True(AussenBearbeitung.Drehen(a, s));
        Assert.True(Vector2.Distance(Kreisverkehr.Mitte(Linie(a, s.Nummer)), M + new Vector2(1, -1)) < 0.01f);
        var nachher = new Vector2((float)Py.F(Liste(Aussen.Laternen(a)[0])[0]), (float)Py.F(Liste(Aussen.Laternen(a)[0])[1]));
        Assert.True(Vector2.Distance(vorher, nachher) > 1f, "Laterne nicht mitgedreht");
        Gruen(a);
        // Gegenprobe: nur den Ring schieben (Zufahrten bleiben stehen) ist rot
        var (b, t) = Grundlage();
        foreach (var p in Liste(Objekt(Land(b)[t.Nummer])["punkte"]))
        {
            var q = Liste(p);
            q[0] = Py.F(q[0]) + 1;
        }
        Rot(b, "Der Kreisverkehr (Straße Nr. 1) hat 0 Zufahrten");
    }

    [Fact]
    public void Kopieren_ZweiterKreisverkehrDaneben_Loeschen_NurDerRing()
    {
        var (a, s) = Grundlage(breite: 32);
        var kopie = AussenBearbeitung.Kopieren(a, s)!;
        Assert.True(AussenBearbeitung.IstKreisverkehr(a, kopie));
        Assert.Equal(10, Land(a).Count);
        Assert.Equal(8, Aussen.Laternen(a).Count);
        Gruen(a);
        Assert.True(AussenBearbeitung.Loeschen(a, kopie));
        Assert.Equal(9, Land(a).Count);
    }

    // ---- Spiel ---------------------------------------------------------------------------------------

    [Fact]
    public void Spiel_RingOhneStoss_InselMitRandRasenBaum()
    {
        var (a, s) = Grundlage();
        var szene = Szene(a);
        var ring = Linie(a, s.Nummer);
        var band = szene.Streifen.Single(st => st.Breite == StadtSzene.StrassenBreite && Math.Abs(st.Oben - StadtSzene.Hoehe.Strasse) < 1e-6 && st.Punkte.Count == ring.Count + 2);
        // geschlossen: ein Stueck ueber den Stoss hinaus auf beiden Seiten (Gehrung auch am Stoss)
        Assert.Equal(ring[^2], band.Punkte[0]);
        Assert.Equal(ring[1], band.Punkte[^1]);
        var insel = Kreisverkehr.InselRadius(2);
        Assert.Contains(szene.Flaechen, f => f.Farbe == StadtSzene.InselRand && Math.Abs(f.Hoehe - Kreisverkehr.RandHoehe) < 1e-6
            && f.Punkte.All(q => Math.Abs(Vector2.Distance(q, M) - insel) < 0.01f));
        Assert.Contains(szene.Flaechen, f => Math.Abs(f.Hoehe - Kreisverkehr.RasenHoehe) < 1e-6
            && f.Punkte.All(q => Math.Abs(Vector2.Distance(q, M) - (insel - Kreisverkehr.RandBreite)) < 0.01f));
        // der Baum steht in der Mitte (Modell oder Ersatz-Teile)
        Assert.True(szene.Stuecke.Any(st => Vector2.Distance(new Vector2(st.Lage.Translation.X, st.Lage.Translation.Z), M) < 0.5f)
            || szene.Teile.Any(t => t.Art != TeilArt.Band && Vector2.Distance(new Vector2((t.X1 + t.X2) / 2, (t.Z1 + t.Z2) / 2), M) < 0.2f), "kein Baum auf der Insel");
    }

    [Fact]
    public void Spiel_InselNichtBegehbar_RingUndGehwegBegehbar_GegenprobeOhneFeld()
    {
        var (a, s) = Grundlage();
        var szene = Szene(a);
        Assert.False(szene.Laufraster.KannStehen(M.X, M.Y));
        Assert.False(szene.Laufraster.KannStehen(M.X + 1.2, M.Y));
        Assert.True(szene.Laufraster.KannStehen(M.X + 2, M.Y + 0.3));
        Assert.True(szene.Laufraster.KannStehen(M.X - 2.5, M.Y + 0.6));
        // Gegenprobe: derselbe Ring ohne Feld kreisverkehr (nur eine runde Strasse) hat keine Insel, die Mitte ist Wiese
        var l = Objekt(Land(a)[s.Nummer]);
        var rest = l.Where(p => p.Key != Kreisverkehr.Feld).ToList();
        l.Clear();
        l.AddRange(rest);
        Assert.True(Szene(a).Laufraster.KannStehen(M.X, M.Y));
    }

    [Fact]
    public void Spiel_MittellinieRundumImTakt_LueckeNurAnDenZufahrten()
    {
        var (a, s) = Grundlage();
        var strassen = Land(a).OfType<PyObjekt>().Select(l => Kreisverkehr.Linie(l["punkte"])).ToList();
        var striche = StadtSzene.MittellinienStriche(strassen);
        var ring = striche.Where(st => MathF.Abs(Vector2.Distance(new Vector2(st.X1, st.Z1), M) - 2) < 0.02f).ToList();
        var winkel = ring.Select(st => Math.Atan2(st.Z1 - M.Y, st.X1 - M.X)).Select(w => w < 0 ? w + (2 * Math.PI) : w).Order().ToList();
        var schritte = winkel.Zip([.. winkel.Skip(1), winkel[0] + (2 * Math.PI)], (x, y) => (y - x) * 2).ToList();
        var takt = 2 * Math.PI * 2 / Math.Round(2 * Math.PI * 2 / 0.45);
        // jeder Schritt ein Takt, ausser an den vier Zufahrten (dort fehlen Striche wie an jeder Einmuendung)
        Assert.Equal(4, schritte.Count(x => x > takt * 1.5));
        Assert.All(schritte.Where(x => x <= takt * 1.5), x => Assert.Equal(takt, x, 1));
        // geschlossen: kein Strich doppelt am Stoss
        Assert.True(schritte.Min() > takt * 0.9);
        // Gegenprobe: die einfachen Striche ohne Anpassung treffen am Stoss nicht den Takt
        var roh = StadtSzene.Striche([.. strassen[s.Nummer].Select(q => (q.X, q.Y))], 0.2f, 0.25f);
        var ende = Vector2.Distance(new Vector2(roh[^1].X1, roh[^1].Z1), strassen[s.Nummer][0]);
        Assert.True(MathF.Abs(ende - 0.45f) > 0.01f, $"Gegenprobe: Stoss passt zufaellig ({ende})");
    }

    [Fact]
    public void Fassung0_OhneKreisverkehr() => Assert.Empty(Kreisverkehr.Alle(Liste(Objekt(D.Fassung.Karte["welt"])["landschaft"])));

    // ---- Pruefregeln ---------------------------------------------------------------------------------

    [Fact]
    public void Regel1_RadiusUndPunkte()
    {
        var (a, s) = Grundlage();
        var feld = Objekt(Objekt(Land(a)[s.Nummer])[Kreisverkehr.Feld]);
        Py.Setzen(feld, "radius", 2.5);
        Rot(a, "Der Kreisverkehr (Straße Nr. 1) hat einen Radius, den es nicht gibt.");
        Py.Setzen(feld, "radius", 3.0);
        Rot(a, "Der Kreisverkehr (Straße Nr. 1) ist falsch gespeichert");
        Py.Setzen(feld, "radius", 2.0);
        Gruen(a);
    }

    [Fact]
    public void Regel2_DreiOderVierZufahrten()
    {
        var (a, s) = Grundlage();
        Land(a).RemoveAt(2);
        Gruen(a);
        Land(a).RemoveAt(2);
        Rot(a, "Der Kreisverkehr (Straße Nr. 1) hat 2 Zufahrten. Erlaubt sind 3 oder 4.");
        // fuenf Zufahrten: vier plus eine schraeg (zu nah an zweien)
        var (b, _) = Grundlage();
        var d = new Vector2(MathF.Cos(MathF.PI / 4), MathF.Sin(MathF.PI / 4));
        Strasse(b, M.X + (d.X * 5), M.Y + (d.Y * 5), M.X + (d.X * 2), M.Y + (d.Y * 2));
        Rot(b, "Der Kreisverkehr (Straße Nr. 1) hat 5 Zufahrten.");
    }

    [Fact]
    public void Regel3_ZufahrtGeradeUndAufDieMitte()
    {
        var (a, _) = Grundlage();
        // Zufahrt 1 (rechts) schraeg: das aeussere Ende 1 zur Seite
        var p = Liste(Liste(Objekt(Land(a)[1])["punkte"])[0]);
        p[1] = Py.F(p[1]) + 1;
        Rot(a, "Die Zufahrt Straße Nr. 2 zielt nicht auf die Mitte des Kreisverkehrs (Straße Nr. 1).");
        p[1] = Py.F(p[1]) - 1;
        Gruen(a);
        // eine Kurve als Zufahrt: Ende auf dem Ring
        var (b, _) = Grundlage();
        Land(b).RemoveAt(1);
        var kurve = Strassenkurve.Mittellinie("bogen", 90, 2).Select(q => new Vector2(-q.Y, q.X)).Select(q => q + new Vector2(M.X + 2, M.Y - 2)).Reverse().ToList();
        // Ende genau rechts auf dem Ring (14; 8)
        kurve = [.. kurve.Select(q => q - kurve[^1] + new Vector2(M.X + 2, M.Y))];
        Land(b).Add(new PyObjekt
        {
            { "typ", "strasse" },
            { "punkte", new List<object?>(kurve.Select(q => (object?)P(Math.Round(q.X, 3), Math.Round(q.Y, 3)))) },
            { "kurve", new PyObjekt { { "form", "bogen" }, { "winkel", 90L }, { "radius", 2.0 } } },
        });
        Rot(b, $"Straße Nr. {Land(b).Count} führt in einer Kurve auf den Kreisverkehr (Straße Nr. 1).");
    }

    [Fact]
    public void Regel4_ZufahrtenMindestens60Grad()
    {
        var (a, _) = Grundlage();
        Land(a).RemoveAt(2);
        var d = new Vector2(MathF.Cos(MathF.PI / 6), MathF.Sin(MathF.PI / 6));
        Strasse(a, M.X + (d.X * 5), M.Y + (d.Y * 5), M.X + (d.X * 2), M.Y + (d.Y * 2));
        Rot(a, "Zwei Zufahrten des Kreisverkehrs (Straße Nr. 1) liegen nur 30° auseinander");
        // Gegenprobe 60 Grad: erlaubt
        var (b, _) = Grundlage();
        Land(b).RemoveAt(2);
        var e = new Vector2(MathF.Cos(MathF.PI / 3), MathF.Sin(MathF.PI / 3));
        Strasse(b, M.X + (e.X * 5), M.Y + (e.Y * 5), M.X + (e.X * 2), M.Y + (e.Y * 2));
        Assert.DoesNotContain(Pruefen(b), f => f.Text.StartsWith("Zwei Zufahrten", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel5_StrasseKreuztDenRing()
    {
        var (a, _) = Grundlage();
        Strasse(a, 2, 7, 22, 7);
        Rot(a, $"Straße Nr. {Land(a).Count} kreuzt den Kreisverkehr (Straße Nr. 1).");
        var (b, _) = Grundlage();
        Strasse(b, 2, 14, 22, 14);
        Assert.DoesNotContain(Pruefen(b), f => f.Text.Contains("kreuzt den Kreisverkehr", StringComparison.Ordinal));
    }

    [Fact]
    public void Regel6_BaumFlussTeichBahnAufDemKreisverkehr()
    {
        var (a, _) = Grundlage();
        Land(a).Add(new PyObjekt { { "typ", "baum" }, { "punkte", new List<object?> { P(M.X + 1, M.Y) } } });
        Rot(a, "Ein Baum steht auf dem Kreisverkehr (Straße Nr. 1).");
        var (b, _) = Grundlage();
        Land(b).Add(new PyObjekt { { "typ", "fluss" }, { "punkte", new List<object?> { P(2, 9), P(22, 9) } } });
        Rot(b, "Der Fluss führt über den Kreisverkehr (Straße Nr. 1).");
        var (c, _) = Grundlage();
        Land(c).Add(new PyObjekt { { "typ", "bahn" }, { "punkte", new List<object?> { P(13, 0), P(13, 16) } } });
        Rot(c, "Die Bahn führt über den Kreisverkehr (Straße Nr. 1).");
        var (t, _) = Grundlage();
        Py.Setzen(Aussen.Stadtdaten(t), "wasser", new PyObjekt { { "teiche", new List<object?> { new PyObjekt { { "punkte", new List<object?> { P(12.5, 8.5), P(13.5, 8.5), P(13.5, 9.5), P(12.5, 9.5) } } } } } });
        Rot(t, "Ein Teich liegt auf dem Kreisverkehr (Straße Nr. 1).");
        // Gegenprobe: Baum neben dem Kreisverkehr
        var (g, _) = Grundlage();
        Land(g).Add(new PyObjekt { { "typ", "baum" }, { "punkte", new List<object?> { P(4, 3) } } });
        Gruen(g);
    }

    [Fact]
    public void Regel7_LaterneAufInselRingOderZufahrt()
    {
        foreach (var (x, y, text) in new[] { (12.5, 8.0, "auf der Insel"), (12.0, 10.0, "auf der Fahrbahn des Kreisverkehrs"), (15.5, 8.0, "in einer Zufahrt") })
        {
            var (a, _) = Grundlage();
            Aussen.Laternen(a).Add(new List<object?> { x, y, false });
            Rot(a, $"Laterne Nr. 5 steht {text}");
        }
    }

    [Fact]
    public void Regel8_RagtUeberDieKarte()
    {
        var (a, s) = Grundlage();
        // alles 9,5 nach links: die Mitte bei 2,5, der Gehweg reicht bis -0,2
        AussenBearbeitung.Verschieben(a, s, -9.5, 0);
        Rot(a, "Der Kreisverkehr (Straße Nr. 1) ragt über den Rand der Karte.");
    }
}
